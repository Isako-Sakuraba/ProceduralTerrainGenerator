# Terrain Generation

This document describes the terrain pipeline implemented under `Assets/Scripts/Terrain`, including noise generation, terrain data, biome and elevation classification, surface textures, mesh creation, and runtime chunk streaming.

## Pipeline Overview

The system first creates normalized environmental data, then independently turns that data into geometry and surface choices:

```text
TerrainDefinition
  +-- temperature NoiseMapBase --+
  +-- humidity NoiseMapBase -----+--> TerrainGenerator --> TerrainPoint[]
  +-- elevation NoiseMapBase ----+                         |
  +-- three shape curves                                  |
                                                            +--> TerrainMeshGenerator --> geometry
SurfaceDefinition                                           |
  +-- BiomeDefinition[] ------------------------------------+--> TerrainSurfaceGenerator
  |     +-- climate/elevation response curves                    +--> surface texture IDs
  |     +-- BiomeTextureDefinition                               +--> Texture2DArray
  +-- ElevationSettings

surface texture IDs --> mesh UV1 --> TerrainTextureArrayLit shader
elevation ----------> world height
Shore threshold ----> streamed water-plane height
```

There are two consumers of this pipeline:

- The **finite-grid path** calls `TerrainGenerator.Generate()`, uses `TerrainDefinition.Size`, and is displayed by `TerrainView`, `SimpleTerrainView`, or `BiomeTerrainView`.
- The **streamed path** is managed by `TerrainChunkManager`. Each chunk has its own requested resolution, padded terrain data, mesh, surface IDs, and optional water mesh.

Both paths use the same seed, noise maps, shape curves, biome definitions, elevation settings, and textures.

## Core Terrain Data

### `TerrainPoint`

File: `Assets/Scripts/Terrain/Core/TerrainPoint.cs`

Every generated sample contains three values:

| Value | Range | Use |
|---|---:|---|
| `Temperature` | `[0, 1]` | Biome suitability |
| `Humidity` | `[0, 1]` | Biome suitability |
| `Elevation` | `[0, 1]` | Biome suitability, elevation type, mesh height, and water checks |

These values are normalized data rather than world-space measurements. For example, elevation `0.5` only becomes a world height when `TerrainMeshGenerator` maps it between `_bottomHeight` and `_topHeight`.

## Terrain Definitions

### `TerrainDefinition`

File: `Assets/Scripts/Terrain/Configs/TerrainDefinition.cs`

`TerrainDefinition` is the top-level ScriptableObject for environmental data generation.

| Setting | Effect |
|---|---|
| `Size` | Width and height of the finite grid. `Count` is `Size.x * Size.y`. This does **not** control streamed chunk resolution. |
| `Scale` | Global X/Y multiplier for all noise sampling coordinates. Larger values move through noise space faster and usually produce smaller world features. |
| `Offset` | Global X/Y translation applied to all noise sampling coordinates. It moves the sampled area without changing the seed. |
| `TemperatureNoiseMap` | Produces the raw temperature field. Required. |
| `HumidityNoiseMap` | Produces the raw humidity field. Required. |
| `ElevationNoiseMap` | Produces the raw elevation field. Required. |
| `TemperatureShape` | Remaps normalized temperature after noise generation. |
| `HumidityShape` | Remaps normalized humidity after noise generation. |
| `ElevationShape` | Remaps normalized elevation after noise generation. This directly changes terrain height distribution and elevation bands. |

All shape-curve results are clamped to `[0, 1]`. Curves can therefore bias a field without changing its noise map. An elevation curve can create broad lowlands, emphasize peaks, or flatten part of the range; temperature and humidity curves change how much of the world falls into different biome climate ranges.

`HasNoiseMaps()` requires all three maps. Generation cannot proceed with only an elevation map.

## Noise Maps

Noise maps derive from `NoiseMapBase` and write normalized values into a supplied buffer. They support a finite grid through `GetNoise()` and arbitrary world regions through `GetNoiseRegion()`.

### `NoiseMapBase`

File: `Assets/Scripts/Terrain/Configs/NoiseMaps/NoiseMapBase.cs`

| Setting | Effect |
|---|---|
| `Frequency` | Base frequency passed to `FastNoiseLite`. Higher frequency generally creates more frequent/smaller variation. |
| `Scale` | Per-map coordinate multiplier, combined component-wise with `TerrainDefinition.Scale`. This allows climate and elevation to use different feature scales. |
| `Offset` | Per-map coordinate translation, added to the terrain offset. Separate offsets help decorrelate temperature, humidity, and elevation fields. |

The effective coordinates for a simple map are conceptually:

```text
sample = worldOrGridPosition * TerrainDefinition.Scale * NoiseMap.Scale
       + TerrainDefinition.Offset + NoiseMap.Offset
```

### `SimpleNoiseMap`

File: `Assets/Scripts/Terrain/Configs/NoiseMaps/SimpleNoiseMap.cs`

Adds one setting:

| Setting | Effect |
|---|---|
| `NoiseType` | Selects the `FastNoiseLite.NoiseType` algorithm. |

It samples one noise function. The expected signed result is converted with `sample * 0.5 + 0.5`, then clamped to `[0, 1]`.

### `LayeredNoiseMap`

Files:

- `Assets/Scripts/Terrain/Configs/NoiseMaps/LayeredNoiseMap.cs`
- `Assets/Scripts/Terrain/Configs/NoiseMaps/NoiseLayer.cs`

A layered map combines active `NoiseLayer` entries like configurable octaves.

`LayeredNoiseMap` settings:

| Setting | Effect |
|---|---|
| `Layers` | Ordered noise layers. Null or inactive layers are skipped. |
| `Lacunarity` | Multiplies frequency after each active layer. Values above `1` make later layers progressively finer. |
| `AmplitudeMultiplier` | Multiplies amplitude after each active layer. Values below `1` make later layers contribute less. |

`NoiseLayer` settings:

| Setting | Effect |
|---|---|
| `Active` | Includes or excludes the layer. Inactive layers also do not advance frequency or amplitude. |
| `NoiseType` | Noise algorithm used by this layer. |
| `NoiseMultiplier` | Multiplies this layer's sampled value. Negative values invert it; large values can cause final clamping. |
| `Scale` | Additional component-wise coordinate scale for this layer. |
| `Offset` | Additional coordinate translation for this layer. |

The combination is approximately:

```text
value = sum(layerNoise * layer.NoiseMultiplier * octaveAmplitude)
      / sum(octaveAmplitude)
normalized = clamp01(value * 0.5 + 0.5)
```

`NoiseMultiplier` is not included in the normalization denominator. Consequently, high multipliers can saturate the result at `0` or `1`. If no active layers contribute, the map outputs `0.5` everywhere.

## `TerrainGenerator`

File: `Assets/Scripts/Terrain/Core/TerrainGenerator.cs`

| Component setting | Effect |
|---|---|
| `_seed` | Seed assigned to `FastNoiseLite`. A common seed is used for all three fields, while map/layer offsets and algorithms can decorrelate them. |
| `_definition` | Terrain definition containing the three maps, global transforms, dimensions, and shape curves. |

### Finite-grid generation

`Generate()` performs the following:

1. Validates the definition and all three noise maps.
2. Sets the noise seed.
3. Generates temperature, humidity, and elevation arrays at `TerrainDefinition.Size`.
4. Applies the corresponding shape curve to every value.
5. Combines the values into `TerrainPoint[]`.
6. Raises the `Generated` event.

Temporary scalar arrays are rented from `ArrayPool<float>.Shared`.

### Chunk-region generation

`Generate(TerrainChunkRequest, TerrainChunkData)` samples a world-space region. It uses a resolution of `CellsPerEdge + 2` and starts one sample spacing before the chunk origin. The extra row on every side lets mesh generation inspect terrain immediately outside the rendered area.

This overload writes to the chunk's data and does not raise the finite-grid `Generated` event.

## Biomes

### `BiomeDefinition`

File: `Assets/Scripts/Terrain/Configs/Biomes/BiomeDefinition.cs`

Each biome describes where it is suitable and which elevation-dependent texture set it uses.

| Setting | Effect |
|---|---|
| `Temperature` | Response curve evaluated with `TerrainPoint.Temperature`. |
| `Humidity` | Response curve evaluated with `TerrainPoint.Humidity`. |
| `Elevation` | Response curve evaluated with `TerrainPoint.Elevation`. This affects biome selection, independently of elevation-band selection. |
| `TextureDefinition` | `BiomeTextureDefinition` used after this biome wins. Surface generation skips biomes without one. |

The biome score is:

```text
score = clamp01(temperature response)
      * clamp01(humidity response)
      * clamp01(elevation response)
```

The multiplication makes each axis a gate: a zero response on any axis makes the final score zero. Curves may overlap to create gradual competition between biomes, but the output is still a single winning biome per sample; there is no biome texture blending.

`TerrainSurfaceGenerator` picks the strictly highest score. Equal scores remain with the biome encountered first, so the order in `SurfaceDefinition.Biomes` is a priority/tie-break rule.

## Elevation Settings and Types

### `ElevationType`

File: `Assets/Scripts/Terrain/Core/ElevationType.cs`

The available bands are:

1. `DeepOcean`
2. `ShallowWater`
3. `Shore`
4. `Lowland`
5. `Highland`
6. `MountainBase`
7. `Mountain`
8. `MountainPeak`

### `ElevationSettings`

File: `Assets/Scripts/Terrain/Configs/Biomes/ElevationSettings.cs`

`ElevationSettings` is embedded in a `SurfaceDefinition`. Each `Point` has:

| Setting | Effect |
|---|---|
| `_type` | The resulting `ElevationType`. |
| `_elevation` | Normalized lower threshold for that type, clamped to `[0, 1]`. |

Code defaults are:

| Type | Threshold |
|---|---:|
| `DeepOcean` | `0.00` |
| `ShallowWater` | `0.20` |
| `Shore` | `0.32` |
| `Lowland` | `0.38` |
| `Highland` | `0.58` |
| `MountainBase` | `0.72` |
| `Mountain` | `0.82` |
| `MountainPeak` | `0.93` |

`Evaluate()` selects the point with the greatest threshold less than or equal to the sample elevation. The array does not have to be sorted. If input lies below every threshold, the first configured point's type is returned. An empty array returns the enum default, `DeepOcean`.

With duplicate threshold values, the last matching point encountered wins in `Evaluate()`. In contrast, `TryGetElevation(type)` returns the first configured point with that type.

The custom inspector in `Assets/Scripts/Terrain/Editor/ElevationSettingsDrawer.cs` provides draggable thresholds and a colored range display.

### Elevation has three distinct roles

It is important not to treat these as one operation:

1. `BiomeDefinition.Elevation` scores whether a biome belongs at a height.
2. `ElevationSettings` chooses a named band after the biome has won.
3. `TerrainMeshGenerator` converts normalized elevation into world-space height.

Changing an elevation response curve can change the biome without moving geometry. Changing an elevation threshold can change textures and water height without changing the terrain points. Changing mesh top/bottom height moves geometry without changing biome or band classification.

## Surface and Texture Definitions

### `SurfaceDefinition`

File: `Assets/Scripts/Terrain/Configs/Biomes/SurfaceDefinition.cs`

| Setting | Effect |
|---|---|
| `Biomes` | Ordered biome candidates. Order breaks equal-score ties and influences texture-array slice ordering. |
| `ElevationSettings` | Converts normalized elevation to `ElevationType`. |

`TryGetWaterElevation()` returns the configured `Shore` threshold. This couples the beginning of the shore band to the streamed water-plane elevation.

### `BiomeTextureDefinition`

File: `Assets/Scripts/Terrain/Configs/Biomes/BiomeTextureDefinition.cs`

Each biome texture definition contains one `Texture2D` slot for every `ElevationType`:

- `DeepOcean`
- `ShallowWater`
- `Shore`
- `Lowland`
- `Highland`
- `MountainBase`
- `Mountain`
- `MountainPeak`

After biome selection, the point's elevation type selects one of these slots. This allows, for example, plains and desert to use different lowland textures while both use a mountain-specific texture at high elevation.

Null slots are permitted but resolve to surface texture ID `0`.

### `TerrainSurfaceGenerator`

File: `Assets/Scripts/Terrain/TerrainSurfaceGenerator.cs`

| Component setting | Effect |
|---|---|
| `_definition` | Supplies biome candidates and elevation thresholds. |

For every `TerrainPoint`, surface generation:

1. Scores all valid biomes.
2. Selects the highest-scoring biome.
3. Classifies the point's elevation with `ElevationSettings`.
4. Gets that elevation slot from the biome's `BiomeTextureDefinition`.
5. Writes the texture's array-slice ID to the output.

Before assigning IDs, it collects unique, non-null textures by biome order and elevation-slot order, then creates one `Texture2DArray`. Slice IDs are transient and order-dependent, not stable asset identifiers.

The first texture determines array width, height, format, and whether mipmaps are enabled. Every other texture must match its dimensions and format. A mismatch logs a warning and copies the first texture into that slice. The generated array uses point filtering and repeat wrapping.

ID `0` is also the fallback for a missing biome, missing texture definition, null texture, or unknown texture. If no textures are configured, no texture array is created.

The texture array is cached. `OnValidate()` or `Release()` destroys it so it can be rebuilt.

## Mesh Generation

### `TerrainMeshGenerator`

File: `Assets/Scripts/Terrain/TerrainMeshGenerator.cs`

This system creates stepped, block-like terrain rather than a smooth triangulated height field. Each visible cell receives an independent flat top quad. Vertical quads are added wherever an adjacent cell is lower.

| Setting | Effect |
|---|---|
| `_cellScale` | Finite-grid cell width/depth. In chunk mode, physical cell size comes from chunk sample spacing; `_cellScale` remains the base reference for UV density. Minimum `0.01`. |
| `_textureScale` | Texture world-size/tiling control. It divides generated UV coordinates; smaller values repeat textures more frequently. Minimum `0.01`. |
| `_bottomHeight` | Height for normalized elevation `0`, lower extent of side walls and skirts, and omission threshold. |
| `_topHeight` | Height for normalized elevation `1`. It cannot be below `_bottomHeight`. |

World height is:

```text
height = lerp(bottomHeight, topHeight, clamp01(elevation))
```

Cells at or below `_bottomHeight` are omitted. Each face has four unshared vertices and a fixed normal, producing hard edges. UV0 stores repeated texture coordinates. In chunk mode, UV1.x stores the integer texture-array slice ID on all four vertices of each face.

`Assets/Shaders/TerrainTextureArrayLit.shader` rounds UV1.x, samples that slice from `_TerrainTextures`, and applies URP lighting and fog.

### Finite `TerrainView`

File: `Assets/Scripts/Terrain/TerrainView.cs`

`TerrainView` listens to `TerrainGenerator.Generated`, generates surface IDs, creates the mesh, assigns one surface ID to every generated face, and binds the texture array using a `MaterialPropertyBlock`.

It caches elevations for geometry invalidation. Climate-only changes can refresh surface choices without rebuilding geometry; elevation or mesh-setting changes rebuild the mesh.

### Diagnostic views

- `SimpleTerrainView` previews combined fields or individual temperature, humidity, and elevation values as a texture.
- `BiomeTerrainView` previews the winning biome using configured debug colors. It shows biome selection, not elevation texture bands.

## Runtime Chunking

### Chunk data model

Files:

- `Assets/Scripts/Terrain/Core/TerrainChunkRequest.cs`
- `Assets/Scripts/Terrain/Core/TerrainChunkData.cs`

`TerrainChunkRequest` contains:

| Value | Effect |
|---|---|
| `Coordinate` | Integer chunk coordinate in X/Z. |
| `Lod` | Index into the manager's LOD array. Larger indices are assumed to be coarser. |
| `WorldOrigin` | World X/Z origin used for noise sampling. |
| `WorldSize` | Physical width/depth of the chunk. |
| `CellsPerEdge` | Number of rendered cells along each edge. |
| `CellTextureSizeMultiplier` | LOD-dependent adjustment to UV density. |
| `SkirtEdges` | Flags indicating which outer edges should extend down to `_bottomHeight`. |
| `Seed` | Noise seed. |

Derived values are:

```text
SampleSpacing    = WorldSize / CellsPerEdge
SampleResolution = CellsPerEdge + 2
```

`TerrainChunkData` stores request metadata plus padded `TerrainPoint[]` and parallel `SurfaceIds[]`. The rendered cell at `(x, z)` reads padded sample `(x + 1, z + 1)`.

### `TerrainChunkManager` settings

File: `Assets/Scripts/Terrain/TerrainChunkManager.cs`

Pipeline references:

| Setting | Effect |
|---|---|
| `_terrainGenerator` | Produces padded environmental samples. |
| `_surfaceGenerator` | Produces texture-array slice IDs. |
| `_meshGenerator` | Converts chunk data into block geometry. |

Rendering references:

| Setting | Effect |
|---|---|
| `_chunkPrefab` | View prefab instantiated when the pool has no available view. A basic object is created if absent. |
| `_material` | Terrain material assigned to chunk renderers. |
| `_waterMaterial` | Material for generated water children. No water is built when absent. |
| `_target` | Transform around which chunks are loaded. `Camera.main` is used at startup when this is null. |

Streaming settings:

| Setting | Effect |
|---|---|
| `_chunkSize` | Constant physical X/Z size of every chunk. LOD changes cell count, not physical size. |
| `_loadingDistance` | Maximum target-to-chunk-footprint distance for an active chunk. |
| `_generationsPerFrame` | Maximum valid full-generation or seam-remesh operations processed per frame. Work remains synchronous on the main thread. |
| `_lodUpdateDistance` | Target movement required before reevaluating selection/LOD when it remains in the same chunk. |
| `_lodHysteresis` | Margin around LOD distance thresholds that reduces rapid switching. |
| `_lodLevels` | Ordered detail configurations, expected from finest/nearest to coarsest/farthest. |

Each LOD level has:

| Setting | Effect |
|---|---|
| `CellsPerEdge` | Terrain resolution. Cell spacing becomes `_chunkSize / CellsPerEdge`. |
| `WaterResolution` | Interior water-grid resolution for this LOD. |
| `CellTextureSizeMultiplier` | Changes texture density at this LOD. Values are clamped to at least `0.01`. |
| `MaximumDistance` | Maximum footprint distance for this LOD. The first matching level is selected. |

LOD array order is semantically important: selection uses the first matching threshold, and seam ownership assumes a higher array index means lower detail.

### Selection, pooling, and scheduling

Chunk coordinates use floor division of manager-local target position by `_chunkSize`, including at negative coordinates. A chunk is placed at local `(coordinate.x * chunkSize, 0, coordinate.y * chunkSize)`.

Selection measures the shortest 2D distance from the target to each chunk's rectangular footprint, not its center. The manager:

1. Determines desired coordinates within `_loadingDistance`.
2. Acquires views from a pool or instantiates them.
3. Selects LOD with hysteresis.
4. Queues the nearest pending operation first.
5. Generates up to `_generationsPerFrame` valid operations in `Update()`.
6. Deactivates and pools views no longer required.

Version tokens reject stale queue entries when a view is reassigned, deactivated, or changes LOD. There is no background thread, job, Burst path, or persistent coordinate-based terrain cache.

The manager transforms a chunk's local origin and uses the resulting world X/Z for noise sampling. Identity rotation and scale are safest: arbitrary manager rotation or non-uniform scale can make the transformed local mesh and X/Z-only sample region disagree.

### Neighbor samples and terrain seams

World-position sampling makes adjacent chunks query the same continuous noise field. The one-sample padded border lets each edge cell compare its height with an outside neighbor sample without directly referencing another chunk's data.

At loaded-area boundaries or mixed LOD boundaries, the manager assigns `SkirtEdges`:

- A missing neighbor causes this chunk to own the edge.
- When LODs differ, the chunk with the larger/coarser LOD index owns the edge.

An owned edge generates vertical faces down to `_bottomHeight`. These skirts hide gaps; they do not weld vertices or stitch coarse and fine topologies. Neighbor creation, removal, or LOD changes queue seam-only remeshes that reuse existing point and surface arrays when possible.

### `TerrainChunkView` and water

File: `Assets/Scripts/Terrain/TerrainChunkView.cs`

Each view owns a dynamic terrain mesh and reusable `TerrainChunkData`. Applying a chunk:

1. Rejects stale request versions.
2. Generates the terrain mesh from chunk data.
3. Assigns the mesh and surface texture array.
4. Builds or hides its optional water child.

Water level comes from the `Shore` threshold in `SurfaceDefinition.ElevationSettings`, converted through `TerrainMeshGenerator.EvaluateHeight()`. If any padded point lies below that threshold, a flat water mesh spans the whole chunk. Terrain above it hides it through depth testing.

Water interior resolution follows the current LOD. Outer water edges use the maximum configured water resolution, allowing different-LOD water meshes to align at boundaries. The water shader uses world coordinates for wave displacement, helping waves remain continuous between chunks.

The current chunk prefab has no terrain collider. Chunk pooling retains meshes, water objects, and arrays, but returning to a coordinate still regenerates it because pooled views are not keyed by coordinate.

## How the Configurations Mix Together

For one sample, the complete decision process is:

1. The terrain definition, map, and optional layer coordinate transforms are combined.
2. The seed and selected noise algorithms produce three raw fields.
3. Noise outputs are normalized and passed through terrain shape curves.
4. The resulting temperature, humidity, and elevation form a `TerrainPoint`.
5. Every valid biome scores that point using three response curves.
6. The highest-scoring biome supplies a `BiomeTextureDefinition`.
7. `ElevationSettings` maps the same normalized elevation to an `ElevationType`.
8. That elevation type selects one texture from the winning biome's texture definition.
9. The texture becomes an integer array-slice ID.
10. Elevation is separately converted to world height and used to generate top/side faces.
11. The ID is written into mesh UV1 so the shader can sample the correct texture-array slice.
12. In chunk mode, the `Shore` threshold also controls water height and low-enough samples determine whether a water mesh exists.

Example:

```text
TerrainPoint(temperature=0.85, humidity=0.20, elevation=0.45)
    -> Desert wins the biome score
    -> 0.45 falls in the configured Highland band
    -> DesertTextures.Highland supplies the surface
    -> 0.45 is mapped independently into mesh world height
```

## Active Project Example

The main scene is `Assets/Scenes/ProceduralMap.unity`. At the time this document was written it uses:

- Seed `4`
- `Assets/Configs/Terrains/Generated/DesertBadlandsTerrain.asset`
- `Assets/Configs/Surfaces/DefaultSurface.asset`
- Chunk size `128`
- Loading distance `2048`
- One queued operation per frame
- Mesh height range `0` to `256`

Its chunk LOD table is:

| LOD | Cells/edge | Cell spacing | Water resolution | Texture multiplier | Maximum distance |
|---:|---:|---:|---:|---:|---:|
| 0 | 64 | 2 | 16 | 1.0 | 256 |
| 1 | 32 | 4 | 8 | 1.0 | 512 |
| 2 | 16 | 8 | 4 | 1.2 | 1024 |
| 3 | 8 | 16 | 2 | 1.6 | 2048 |
| 4 | 4 | 32 | 1 | 2.0 | `float.MaxValue` |

`DefaultSurface.asset` currently orders its biomes as Sea, Plains, Desert, Taiga, Tundra, Mountains, and Volcanic. That order controls equal-score priority and first-occurrence texture slice ordering.

Its serialized elevation thresholds differ from the code defaults:

| Type | Threshold |
|---|---:|
| `DeepOcean` | `0` |
| `ShallowWater` | `0.090183556` |
| `Shore` | `0.39984035` |
| `Lowland` | `0.4142059` |
| `Highland` | `0.4796489` |
| `MountainBase` | `0.49241823` |
| `Mountain` | `0.82` |
| `MountainPeak` | `0.93` |

With the scene's `0` to `256` height range, the shore/water plane is approximately `102.36` world-height units before any transform.

## Practical Configuration Guidance

- Change a noise map's frequency/scale to change feature size; change offsets to move or decorrelate fields.
- Use terrain shape curves to alter global value distribution before biome and elevation classification.
- Use biome response curves to define ecological suitability without altering geometry.
- Use elevation thresholds to move texture-band boundaries; remember that `Shore` also moves water.
- Use mesh bottom/top heights to change vertical scale without changing normalized biome decisions.
- Keep all biome textures identical in width, height, and texture format.
- Put more important biome first when equal scores should favor it.
- Keep chunk LOD levels ordered from fine/near to coarse/far with sensible increasing maximum distances.
- Expect stepped terrain, hard normals, discrete surface transitions, and skirted rather than welded LOD seams.
- Treat `TerrainDefinition.Size` as finite-preview resolution, not streamed chunk resolution.

## Source Index

| Area | Files |
|---|---|
| Terrain data/generation | `Assets/Scripts/Terrain/Core/TerrainPoint.cs`, `TerrainGenerator.cs` |
| Terrain config | `Assets/Scripts/Terrain/Configs/TerrainDefinition.cs` |
| Noise config | `Assets/Scripts/Terrain/Configs/NoiseMaps/*.cs` |
| Biome/surface config | `Assets/Scripts/Terrain/Configs/Biomes/*.cs` |
| Mesh generation | `Assets/Scripts/Terrain/TerrainMeshGenerator.cs` |
| Surface generation | `Assets/Scripts/Terrain/TerrainSurfaceGenerator.cs` |
| Finite views | `TerrainView.cs`, `SimpleTerrainView.cs`, `BiomeTerrainView.cs` |
| Chunking | `TerrainChunkManager.cs`, `TerrainChunkView.cs`, `Core/TerrainChunkRequest.cs`, `Core/TerrainChunkData.cs` |
| Rendering | `Assets/Shaders/TerrainTextureArrayLit.shader`, `Assets/Shaders/ProceduralWater.shader` |
| Example assets | `Assets/Configs/Terrains`, `Assets/Configs/NoiseMaps`, `Assets/Configs/Biomes`, `Assets/Configs/Surfaces` |
