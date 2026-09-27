using System;
using System.Collections.Generic;
using UnityEngine;

namespace TerrainGeneration
{
    [ExecuteAlways]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class BiomeTerrainView : MonoBehaviour
    {
        /// <summary>
        /// Pairs a biome with its preview color.
        /// </summary>
        [Serializable]
        private struct BiomeColor
        {
            public BiomeDefinition Biome;
            public Color Color;
        }

        [Header("References")]
        [SerializeField] private TerrainGenerator _terrain;
        [SerializeField] private TerrainSurfaceGenerator _surfaceGenerator;

        [Header("Colors")]
        [SerializeField] private List<BiomeColor> _biomeColors = new();
        [SerializeField] private Color _fallbackColor = Color.magenta;

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        private MeshRenderer _renderer;
        private MaterialPropertyBlock _propertyBlock;

        private Texture2D _texture;
        private Color32[] _pixels;

        public Texture2D Texture => _texture;

        /// <summary>
        /// Finds the components used by the biome view.
        /// </summary>
        private void Awake()
        {
            EnsureReferences();
        }

        /// <summary>
        /// Finds any missing component references.
        /// </summary>
        private void EnsureReferences()
        {
            _renderer = GetComponent<MeshRenderer>();

            if (_propertyBlock == null)
                _propertyBlock = new MaterialPropertyBlock();

            if (_terrain == null)
                _terrain = GetComponent<TerrainGenerator>();

            if (_surfaceGenerator == null)
                _surfaceGenerator = GetComponent<TerrainSurfaceGenerator>();
        }

        /// <summary>
        /// Starts listening for terrain generation and refreshes the view.
        /// </summary>
        private void OnEnable()
        {
            EnsureReferences();

            if (_terrain == null)
                return;

            _terrain.Generated += OnTerrainGenerated;

            Refresh();
        }

        /// <summary>
        /// Stops listening for terrain generation.
        /// </summary>
        private void OnDisable()
        {
            if (_terrain != null)
                _terrain.Generated -= OnTerrainGenerated;
        }

        /// <summary>
        /// Cleans up the generated preview texture.
        /// </summary>
        private void OnDestroy()
        {
            if (_texture != null)
                DestroyGeneratedObject(_texture);
        }

        /// <summary>
        /// Refreshes the biome view after editor changes.
        /// </summary>
        private void OnValidate()
        {
            EnsureReferences();

            if (isActiveAndEnabled)
                Refresh();
        }

        /// <summary>
        /// Refreshes the view when the terrain is generated.
        /// </summary>
        private void OnTerrainGenerated(TerrainGenerator terrain)
        {
            Refresh();
        }

        /// <summary>
        /// Rebuilds the biome preview from the terrain points.
        /// </summary>
        public void Refresh()
        {
            EnsureReferences();

            SurfaceDefinition definition = _surfaceGenerator != null ? _surfaceGenerator.Definition : null;

            if (_terrain == null || _terrain.Definition == null || definition == null || !definition.HasBiomes())
                return;

            ReadOnlySpan<TerrainPoint> points = _terrain.Points;
            Vector2Int size = _terrain.Definition.Size;
            int count = size.x * size.y;

            if (size.x <= 0 || size.y <= 0 || points.Length < count)
                return;

            EnsureTexture(size);

            for (int i = 0; i < count; i++)
            {
                BiomeDefinition biome = FindBiome(points[i], definition);
                _pixels[i] = GetBiomeColor(biome);
            }

            _texture.SetPixels32(_pixels);
            _texture.Apply(false, false);

            ApplyTexture();
        }

        /// <summary>
        /// Creates a preview texture with the requested size when needed.
        /// </summary>
        private void EnsureTexture(Vector2Int size)
        {
            int count = size.x * size.y;

            if (_pixels == null || _pixels.Length != count)
                _pixels = new Color32[count];

            if (_texture != null && _texture.width == size.x && _texture.height == size.y)
                return;

            if (_texture != null)
                DestroyGeneratedObject(_texture);

            _texture = new Texture2D(
                size.x,
                size.y,
                TextureFormat.RGBA32,
                false,
                true)
            {
                name = "Biome Preview",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
        }

        /// <summary>
        /// Finds the best matching biome for a terrain point.
        /// </summary>
        private BiomeDefinition FindBiome(TerrainPoint point, SurfaceDefinition definition)
        {
            BiomeDefinition bestBiome = null;
            float bestScore = -1f;

            foreach (BiomeDefinition biome in definition.Biomes)
            {
                if (biome == null)
                    continue;

                float score = biome.Evaluate(
                    point.Temperature,
                    point.Humidity,
                    point.Elevation);

                if (score <= bestScore)
                    continue;

                bestScore = score;
                bestBiome = biome;
            }

            return bestBiome;
        }

        /// <summary>
        /// Gets the preview color assigned to a biome.
        /// </summary>
        private Color32 GetBiomeColor(BiomeDefinition biome)
        {
            foreach (BiomeColor entry in _biomeColors)
            {
                if (entry.Biome == biome)
                    return entry.Color;
            }

            return _fallbackColor;
        }

        /// <summary>
        /// Applies the preview texture to the renderer.
        /// </summary>
        private void ApplyTexture()
        {
            if (_renderer == null)
                return;

            _renderer.GetPropertyBlock(_propertyBlock);

            _propertyBlock.SetTexture(BaseMapId, _texture);
            _propertyBlock.SetTexture(MainTexId, _texture);

            _renderer.SetPropertyBlock(_propertyBlock);
        }

        /// <summary>
        /// Destroys a generated object safely in play mode or edit mode.
        /// </summary>
        private static void DestroyGeneratedObject(UnityEngine.Object generatedObject)
        {
            if (Application.isPlaying)
                Destroy(generatedObject);
            else
                DestroyImmediate(generatedObject);
        }
    }
}
