using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TerrainGeneration
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class TerrainChunkView : MonoBehaviour
    {
        private static readonly int TerrainTexturesId = Shader.PropertyToID("_TerrainTextures");

        private readonly List<TerrainMeshGenerator.TerrainFace> _faces = new List<TerrainMeshGenerator.TerrainFace>();
        private readonly List<Vector2> _surfaceUvs = new List<Vector2>();
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private MaterialPropertyBlock _propertyBlock;
        private Mesh _mesh;
        private GameObject _waterObject;
        private MeshRenderer _waterRenderer;
        private MeshFilter _waterFilter;
        private Mesh _waterMesh;
        private readonly List<Vector3> _waterVertices = new List<Vector3>();
        private readonly List<Vector3> _waterNormals = new List<Vector3>();
        private readonly List<Vector2> _waterUvs = new List<Vector2>();
        private readonly List<int> _waterTriangles = new List<int>();

        public Vector2Int Coordinate { get; private set; }
        public int Lod { get; private set; } = -1;
        public int GenerationVersion { get; private set; }
        public TerrainChunkData Data { get; } = new TerrainChunkData();

        private void Awake()
        {
            EnsureResources();
        }

        public int Assign(Vector2Int coordinate, Vector3 localPosition, Material material)
        {
            EnsureResources();
            Coordinate = coordinate;
            Lod = -1;
            GenerationVersion++;
            transform.localPosition = localPosition;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            _meshRenderer.sharedMaterial = material;
            gameObject.name = $"Terrain Chunk ({coordinate.x}, {coordinate.y})";
            gameObject.SetActive(true);
            return GenerationVersion;
        }

        public int RequestLod(int lod)
        {
            Lod = lod;
            return ++GenerationVersion;
        }

        public void Apply(TerrainMeshGenerator meshGenerator, TerrainSurfaceGenerator surfaceGenerator,
            TerrainChunkRequest request, int version, Material waterMaterial, int waterResolution,
            int maximumWaterResolution)
        {
            if (version != GenerationVersion || request.Coordinate != Coordinate)
                return;

            meshGenerator.Generate(Data, _mesh, _faces);
            ApplySurfaceIds(Data.SurfaceIds);
            _meshFilter.sharedMesh = _mesh;

            surfaceGenerator.EnsureTextureArray();
            _meshRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetTexture(TerrainTexturesId, surfaceGenerator.TextureArray);
            _meshRenderer.SetPropertyBlock(_propertyBlock);
            ApplyWater(meshGenerator, surfaceGenerator.Definition, request, waterMaterial,
                waterResolution, maximumWaterResolution);
        }

        public void Deactivate()
        {
            GenerationVersion++;
            Coordinate = default;
            Lod = -1;
            gameObject.SetActive(false);
        }

        private void ApplySurfaceIds(int[] surfaceIds)
        {
            _surfaceUvs.Clear();
            for (int i = 0; i < _mesh.vertexCount; i++)
                _surfaceUvs.Add(Vector2.zero);

            for (int i = 0; i < _faces.Count; i++)
            {
                TerrainMeshGenerator.TerrainFace face = _faces[i];
                Vector2 id = new Vector2(surfaceIds[face.CellIndex], 0f);
                for (int vertex = 0; vertex < 4; vertex++)
                    _surfaceUvs[face.VertexStart + vertex] = id;
            }

            _mesh.SetUVs(1, _surfaceUvs);
        }

        private void EnsureResources()
        {
            if (_meshFilter == null) _meshFilter = GetComponent<MeshFilter>();
            if (_meshRenderer == null) _meshRenderer = GetComponent<MeshRenderer>();
            if (_propertyBlock == null) _propertyBlock = new MaterialPropertyBlock();
            if (_mesh == null)
            {
                _mesh = new Mesh { name = "Terrain Chunk Mesh" };
                _mesh.MarkDynamic();
            }
        }

        private void ApplyWater(TerrainMeshGenerator meshGenerator, SurfaceDefinition surfaceDefinition,
            TerrainChunkRequest request, Material waterMaterial, int waterResolution,
            int maximumWaterResolution)
        {
            if (waterMaterial == null || surfaceDefinition == null ||
                !surfaceDefinition.TryGetWaterElevation(out float waterElevation) ||
                !ContainsWater(waterElevation))
            {
                if (_waterObject != null) _waterObject.SetActive(false);
                return;
            }

            EnsureWaterResources();
            float size = request.WorldSize;
            float height = meshGenerator.EvaluateHeight(waterElevation);
            BuildWaterMesh(size, height, Mathf.Max(1, waterResolution),
                Mathf.Max(1, maximumWaterResolution));

            _waterMesh.Clear();
            _waterMesh.indexFormat = _waterVertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            _waterMesh.SetVertices(_waterVertices);
            _waterMesh.SetNormals(_waterNormals);
            _waterMesh.SetUVs(0, _waterUvs);
            _waterMesh.SetTriangles(_waterTriangles, 0);
            _waterMesh.RecalculateBounds();
            _waterRenderer.sharedMaterial = waterMaterial;
            _waterObject.SetActive(true);
        }

        private void BuildWaterMesh(float size, float height, int resolution, int edgeResolution)
        {
            _waterVertices.Clear();
            _waterNormals.Clear();
            _waterUvs.Clear();
            _waterTriangles.Clear();

            edgeResolution = Mathf.Max(edgeResolution, resolution);
            if (edgeResolution == resolution)
            {
                BuildRegularWaterGrid(size, height, resolution);
                return;
            }

            float inset = size / edgeResolution;
            float innerSize = Mathf.Max(0f, size - inset * 2f);
            int innerWidth = resolution + 1;

            for (int z = 0; z < innerWidth; z++)
            {
                for (int x = 0; x < innerWidth; x++)
                {
                    float px = inset + innerSize * x / resolution;
                    float pz = inset + innerSize * z / resolution;
                    AddWaterVertex(new Vector3(px, height, pz));
                }
            }

            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int start = z * innerWidth + x;
                    AddWaterTriangle(start, start + innerWidth, start + innerWidth + 1);
                    AddWaterTriangle(start, start + innerWidth + 1, start + 1);
                }
            }

            StitchWaterEdge(size, height, edgeResolution, resolution, 0, innerWidth, 0);
            StitchWaterEdge(size, height, edgeResolution, resolution,
                resolution * innerWidth, innerWidth, 1);
            StitchWaterEdge(size, height, edgeResolution, resolution, 0, innerWidth, 2);
            StitchWaterEdge(size, height, edgeResolution, resolution, resolution, innerWidth, 3);
        }

        private void BuildRegularWaterGrid(float size, float height, int resolution)
        {
            int width = resolution + 1;
            for (int z = 0; z < width; z++)
            {
                for (int x = 0; x < width; x++)
                    AddWaterVertex(new Vector3(size * x / resolution, height, size * z / resolution));
            }

            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int start = z * width + x;
                    AddWaterTriangle(start, start + width, start + width + 1);
                    AddWaterTriangle(start, start + width + 1, start + 1);
                }
            }
        }

        private void StitchWaterEdge(float size, float height, int edgeResolution,
            int innerResolution, int innerStart, int innerWidth, int side)
        {
            int outerStart = _waterVertices.Count;
            for (int i = 0; i <= edgeResolution; i++)
            {
                float offset = size * i / edgeResolution;
                Vector3 position = side switch
                {
                    0 => new Vector3(offset, height, 0f),
                    1 => new Vector3(offset, height, size),
                    2 => new Vector3(0f, height, offset),
                    _ => new Vector3(size, height, offset)
                };
                AddWaterVertex(position);
            }

            int outer = 0;
            int inner = 0;
            while (outer < edgeResolution || inner < innerResolution)
            {
                float outerNext = outer < edgeResolution
                    ? (outer + 1f) / edgeResolution : float.PositiveInfinity;
                float innerNext = inner < innerResolution
                    ? (inner + 1f) / innerResolution : float.PositiveInfinity;
                int innerIndex = GetInnerEdgeIndex(innerStart, innerWidth, inner, side);

                if (outerNext <= innerNext)
                {
                    AddWaterTriangle(outerStart + outer, outerStart + outer + 1, innerIndex);
                    outer++;
                }
                else
                {
                    int nextInner = GetInnerEdgeIndex(innerStart, innerWidth, inner + 1, side);
                    AddWaterTriangle(outerStart + outer, nextInner, innerIndex);
                    inner++;
                }
            }
        }

        private static int GetInnerEdgeIndex(int start, int width, int offset, int side)
        {
            return side < 2 ? start + offset : start + offset * width;
        }

        private void AddWaterVertex(Vector3 position)
        {
            _waterVertices.Add(position);
            _waterNormals.Add(Vector3.up);
            _waterUvs.Add(new Vector2(position.x, position.z));
        }

        private void AddWaterTriangle(int a, int b, int c)
        {
            Vector3 cross = Vector3.Cross(_waterVertices[b] - _waterVertices[a],
                _waterVertices[c] - _waterVertices[a]);
            _waterTriangles.Add(a);
            if (cross.y >= 0f)
            {
                _waterTriangles.Add(b);
                _waterTriangles.Add(c);
            }
            else
            {
                _waterTriangles.Add(c);
                _waterTriangles.Add(b);
            }
        }

        private bool ContainsWater(float waterElevation)
        {
            TerrainPoint[] points = Data.Points;
            if (points == null) return false;
            for (int i = 0; i < points.Length; i++)
                if (points[i].Elevation < waterElevation) return true;
            return false;
        }

        private void EnsureWaterResources()
        {
            if (_waterObject != null) return;
            _waterObject = new GameObject("Water");
            _waterObject.transform.SetParent(transform, false);
            _waterFilter = _waterObject.AddComponent<MeshFilter>();
            _waterRenderer = _waterObject.AddComponent<MeshRenderer>();
            _waterRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _waterRenderer.receiveShadows = false;
            _waterMesh = new Mesh { name = "Terrain Chunk Water Mesh" };
            _waterFilter.sharedMesh = _waterMesh;
        }

        private void OnDestroy()
        {
            if (Application.isPlaying)
            {
                if (_mesh != null) Destroy(_mesh);
                if (_waterMesh != null) Destroy(_waterMesh);
            }
            else
            {
                if (_mesh != null) DestroyImmediate(_mesh);
                if (_waterMesh != null) DestroyImmediate(_waterMesh);
            }
        }
    }
}
