using System;
using System.Collections.Generic;
using UnityEngine;

namespace TerrainGeneration
{
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(TerrainMeshGenerator))]
    [RequireComponent(typeof(TerrainSurfaceGenerator))]
    public sealed class TerrainView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TerrainGenerator _terrain;
        [SerializeField] private TerrainMeshGenerator _meshGenerator;
        [SerializeField] private TerrainSurfaceGenerator _surfaceGenerator;
        [SerializeField] private Material _material;

        [Header("Settings")]
        [SerializeField] private bool _generateOnStart = true;

        private static readonly int TerrainTexturesId = Shader.PropertyToID("_TerrainTextures");

        private readonly List<Vector2> _textureUvs = new List<Vector2>();

        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private MaterialPropertyBlock _propertyBlock;
        private float[] _heightCache;
        private int _meshSettingsVersion = -1;

        /// <summary>
        /// Finds the components used by the terrain view.
        /// </summary>
        private void Awake()
        {
            EnsureReferences();
        }

        /// <summary>
        /// Refreshes the terrain view after editor changes.
        /// </summary>
        private void OnValidate()
        {
            EnsureReferences();

            if (isActiveAndEnabled)
                Refresh();
        }

        /// <summary>
        /// Finds any missing component references.
        /// </summary>
        private void EnsureReferences()
        {
            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();

            if (_propertyBlock == null)
                _propertyBlock = new MaterialPropertyBlock();

            if (_meshGenerator == null)
                _meshGenerator = GetComponent<TerrainMeshGenerator>();

            if (_surfaceGenerator == null)
                _surfaceGenerator = GetComponent<TerrainSurfaceGenerator>();

            if (_terrain == null)
                _terrain = GetComponent<TerrainGenerator>();

            if (_material != null)
                _meshRenderer.sharedMaterial = _material;
        }

        /// <summary>
        /// Generates the terrain at startup when enabled.
        /// </summary>
        private void Start()
        {
            if (Application.isPlaying && _generateOnStart && _terrain != null)
                _terrain.Generate();
        }

        /// <summary>
        /// Starts listening for terrain generation and handles editor generation.
        /// </summary>
        private void OnEnable()
        {
            EnsureReferences();

            if (_terrain != null)
                _terrain.Generated += OnTerrainGenerated;

            if (!Application.isPlaying && _generateOnStart && _terrain != null)
                _terrain.Generate();
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
        /// Releases the generated mesh and surface resources.
        /// </summary>
        private void OnDestroy()
        {
            if (_meshGenerator != null)
                _meshGenerator.Release();

            if (_surfaceGenerator != null)
                _surfaceGenerator.Release();
        }

        /// <summary>
        /// Rebuilds the terrain mesh and surface when needed.
        /// </summary>
        public void Refresh()
        {
            EnsureReferences();

            if (_terrain == null || _terrain.Definition == null)
                return;

            ReadOnlySpan<TerrainPoint> points = _terrain.Points;
            Vector2Int size = _terrain.Definition.Size;
            int count = size.x * size.y;

            if (points.Length < count)
                return;

            bool meshSettingsChanged = _meshSettingsVersion != _meshGenerator.SettingsVersion;
            bool heightsChanged = HaveHeightsChanged(points, count);

            _surfaceGenerator.Generate(points);

            if (heightsChanged || meshSettingsChanged || _meshGenerator.Mesh == null)
            {
                Mesh mesh = _meshGenerator.Generate(points, size);
                _meshFilter.sharedMesh = mesh;
                CacheHeights(points, count);
                _meshSettingsVersion = _meshGenerator.SettingsVersion;
            }

            ApplySurfaceIds();
            ApplyTextureArray();
        }

        /// <summary>
        /// Refreshes the view when the terrain is generated.
        /// </summary>
        private void OnTerrainGenerated(TerrainGenerator terrain)
        {
            Refresh();
        }

        /// <summary>
        /// Checks whether the terrain heights differ from the cached values.
        /// </summary>
        private bool HaveHeightsChanged(ReadOnlySpan<TerrainPoint> points, int count)
        {
            if (_heightCache == null || _heightCache.Length != count)
                return true;

            for (int i = 0; i < count; i++)
            {
                if (!Mathf.Approximately(_heightCache[i], points[i].Elevation))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Stores the current terrain heights for later comparisons.
        /// </summary>
        private void CacheHeights(ReadOnlySpan<TerrainPoint> points, int count)
        {
            if (_heightCache == null || _heightCache.Length != count)
                _heightCache = new float[count];

            for (int i = 0; i < count; i++)
                _heightCache[i] = points[i].Elevation;
        }

        /// <summary>
        /// Writes each face's surface ID into the mesh UVs.
        /// </summary>
        private void ApplySurfaceIds()
        {
            Mesh mesh = _meshGenerator.Mesh;
            TerrainMeshGenerator.TerrainFace[] faces = _meshGenerator.FaceMetadata;
            int[] surfaceIds = _surfaceGenerator.SurfaceIds;

            if (mesh == null || surfaceIds == null)
                return;

            _textureUvs.Clear();

            for (int i = 0; i < mesh.vertexCount; i++)
                _textureUvs.Add(Vector2.zero);

            for (int i = 0; i < faces.Length; i++)
            {
                TerrainMeshGenerator.TerrainFace face = faces[i];
                int surfaceId = surfaceIds[face.CellIndex];
                Vector2 uv = new Vector2(surfaceId, 0f);

                _textureUvs[face.VertexStart] = uv;
                _textureUvs[face.VertexStart + 1] = uv;
                _textureUvs[face.VertexStart + 2] = uv;
                _textureUvs[face.VertexStart + 3] = uv;
            }

            mesh.SetUVs(1, _textureUvs);
        }

        /// <summary>
        /// Applies the generated terrain textures to the renderer.
        /// </summary>
        private void ApplyTextureArray()
        {
            if (_meshRenderer == null || _surfaceGenerator.TextureArray == null)
                return;

            _meshRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetTexture(TerrainTexturesId, _surfaceGenerator.TextureArray);
            _meshRenderer.SetPropertyBlock(_propertyBlock);
        }
    }
}
