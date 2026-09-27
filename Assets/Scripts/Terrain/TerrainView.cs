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

        private void Awake()
        {
            EnsureReferences();
        }

        private void OnValidate()
        {
            EnsureReferences();

            if (isActiveAndEnabled)
                Refresh();
        }

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

        private void Start()
        {
            if (Application.isPlaying && _generateOnStart && _terrain != null)
                _terrain.Generate();
        }

        private void OnEnable()
        {
            EnsureReferences();

            if (_terrain != null)
                _terrain.Generated += OnTerrainGenerated;

            if (!Application.isPlaying && _generateOnStart && _terrain != null)
                _terrain.Generate();
        }

        private void OnDisable()
        {
            if (_terrain != null)
                _terrain.Generated -= OnTerrainGenerated;
        }

        private void OnDestroy()
        {
            if (_meshGenerator != null)
                _meshGenerator.Release();

            if (_surfaceGenerator != null)
                _surfaceGenerator.Release();
        }

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

        private void OnTerrainGenerated(TerrainGenerator terrain)
        {
            Refresh();
        }

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

        private void CacheHeights(ReadOnlySpan<TerrainPoint> points, int count)
        {
            if (_heightCache == null || _heightCache.Length != count)
                _heightCache = new float[count];

            for (int i = 0; i < count; i++)
                _heightCache[i] = points[i].Elevation;
        }

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
