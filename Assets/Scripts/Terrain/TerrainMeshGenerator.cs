using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TerrainGeneration
{
    public sealed class TerrainMeshGenerator : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float _cellScale = 1f;
        [SerializeField] private float _textureScale = 1f;
        [SerializeField] private float _bottomHeight;
        [SerializeField] private float _topHeight = 16f;

        public enum FaceDirection
        {
            Top,
            North,
            South,
            East,
            West
        }

        public readonly struct TerrainFace
        {
            public readonly int CellIndex;
            public readonly int VertexStart;
            public readonly FaceDirection Direction;

            public TerrainFace(int cellIndex, int vertexStart, FaceDirection direction)
            {
                CellIndex = cellIndex;
                VertexStart = vertexStart;
                Direction = direction;
            }
        }

        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Vector3> _normals = new List<Vector3>();
        private readonly List<Vector2> _uvs = new List<Vector2>();
        private readonly List<int> _triangles = new List<int>();
        private readonly List<TerrainFace> _faces = new List<TerrainFace>();

        private Mesh _mesh;
        private TerrainFace[] _faceMetadata = new TerrainFace[0];
        private int _settingsVersion;

        public Mesh Mesh => _mesh;
        public TerrainFace[] FaceMetadata => _faceMetadata;
        public int SettingsVersion => _settingsVersion;

        private void OnValidate()
        {
            _cellScale = Mathf.Max(0.01f, _cellScale);
            _textureScale = Mathf.Max(0.01f, _textureScale);

            if (_topHeight < _bottomHeight)
                _topHeight = _bottomHeight;

            _settingsVersion++;
        }

        public Mesh Generate(System.ReadOnlySpan<TerrainPoint> points, Vector2Int size)
        {
            ClearBuffers();

            for (int y = 0; y < size.y; y++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    int index = GetIndex(x, y, size.x);
                    float height = GetHeight(points[index]);

                    if (height <= _bottomHeight)
                        continue;

                    AddTopFace(index, x, y, height);
                    AddSideFaces(points, size, index, x, y, height);
                }
            }

            EnsureMesh();

            _mesh.Clear();
            _mesh.indexFormat = _vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            _mesh.SetVertices(_vertices);
            _mesh.SetNormals(_normals);
            _mesh.SetUVs(0, _uvs);
            _mesh.SetTriangles(_triangles, 0);
            _mesh.RecalculateBounds();

            _faceMetadata = _faces.ToArray();

            return _mesh;
        }

        public void Release()
        {
            if (_mesh != null)
            {
                Destroy(_mesh);
                _mesh = null;
            }

            _faceMetadata = new TerrainFace[0];
            ClearBuffers();
        }

        private void AddSideFaces(System.ReadOnlySpan<TerrainPoint> points, Vector2Int size, int index, int x, int y, float height)
        {
            float northHeight = GetNeighborHeight(points, size, x, y + 1);
            float southHeight = GetNeighborHeight(points, size, x, y - 1);
            float eastHeight = GetNeighborHeight(points, size, x + 1, y);
            float westHeight = GetNeighborHeight(points, size, x - 1, y);

            if (northHeight < height)
                AddNorthFace(index, x, y, northHeight, height);

            if (southHeight < height)
                AddSouthFace(index, x, y, southHeight, height);

            if (eastHeight < height)
                AddEastFace(index, x, y, eastHeight, height);

            if (westHeight < height)
                AddWestFace(index, x, y, westHeight, height);
        }

        private void AddTopFace(int index, int x, int y, float height)
        {
            int start = _vertices.Count;
            float x0 = x * _cellScale;
            float x1 = (x + 1) * _cellScale;
            float z0 = y * _cellScale;
            float z1 = (y + 1) * _cellScale;
            float u0 = x0 / _textureScale;
            float u1 = x1 / _textureScale;
            float v0 = z0 / _textureScale;
            float v1 = z1 / _textureScale;

            _vertices.Add(new Vector3(x0, height, z0));
            _vertices.Add(new Vector3(x0, height, z1));
            _vertices.Add(new Vector3(x1, height, z1));
            _vertices.Add(new Vector3(x1, height, z0));

            AddNormals(Vector3.up);

            _uvs.Add(new Vector2(u0, v0));
            _uvs.Add(new Vector2(u0, v1));
            _uvs.Add(new Vector2(u1, v1));
            _uvs.Add(new Vector2(u1, v0));

            AddTriangles(start);
            _faces.Add(new TerrainFace(index, start, FaceDirection.Top));
        }

        private void AddNorthFace(int index, int x, int y, float bottom, float top)
        {
            int start = _vertices.Count;
            float x0 = x * _cellScale;
            float x1 = (x + 1) * _cellScale;
            float z1 = (y + 1) * _cellScale;

            _vertices.Add(new Vector3(x1, bottom, z1));
            _vertices.Add(new Vector3(x1, top, z1));
            _vertices.Add(new Vector3(x0, top, z1));
            _vertices.Add(new Vector3(x0, bottom, z1));

            AddNormals(Vector3.forward);
            AddSideUvs(bottom, top);
            AddTriangles(start);
            _faces.Add(new TerrainFace(index, start, FaceDirection.North));
        }

        private void AddSouthFace(int index, int x, int y, float bottom, float top)
        {
            int start = _vertices.Count;
            float x0 = x * _cellScale;
            float x1 = (x + 1) * _cellScale;
            float z0 = y * _cellScale;

            _vertices.Add(new Vector3(x0, bottom, z0));
            _vertices.Add(new Vector3(x0, top, z0));
            _vertices.Add(new Vector3(x1, top, z0));
            _vertices.Add(new Vector3(x1, bottom, z0));

            AddNormals(Vector3.back);
            AddSideUvs(bottom, top);
            AddTriangles(start);
            _faces.Add(new TerrainFace(index, start, FaceDirection.South));
        }

        private void AddEastFace(int index, int x, int y, float bottom, float top)
        {
            int start = _vertices.Count;
            float x1 = (x + 1) * _cellScale;
            float z0 = y * _cellScale;
            float z1 = (y + 1) * _cellScale;

            _vertices.Add(new Vector3(x1, bottom, z0));
            _vertices.Add(new Vector3(x1, top, z0));
            _vertices.Add(new Vector3(x1, top, z1));
            _vertices.Add(new Vector3(x1, bottom, z1));

            AddNormals(Vector3.right);
            AddSideUvs(bottom, top);
            AddTriangles(start);
            _faces.Add(new TerrainFace(index, start, FaceDirection.East));
        }

        private void AddWestFace(int index, int x, int y, float bottom, float top)
        {
            int start = _vertices.Count;
            float x0 = x * _cellScale;
            float z0 = y * _cellScale;
            float z1 = (y + 1) * _cellScale;

            _vertices.Add(new Vector3(x0, bottom, z1));
            _vertices.Add(new Vector3(x0, top, z1));
            _vertices.Add(new Vector3(x0, top, z0));
            _vertices.Add(new Vector3(x0, bottom, z0));

            AddNormals(Vector3.left);
            AddSideUvs(bottom, top);
            AddTriangles(start);
            _faces.Add(new TerrainFace(index, start, FaceDirection.West));
        }

        private void AddNormals(Vector3 normal)
        {
            _normals.Add(normal);
            _normals.Add(normal);
            _normals.Add(normal);
            _normals.Add(normal);
        }

        private void AddSideUvs(float bottom, float top)
        {
            float width = _cellScale / _textureScale;
            float bottomUv = (bottom - _bottomHeight) / _textureScale;
            float topUv = (top - _bottomHeight) / _textureScale;

            _uvs.Add(new Vector2(0f, bottomUv));
            _uvs.Add(new Vector2(0f, topUv));
            _uvs.Add(new Vector2(width, topUv));
            _uvs.Add(new Vector2(width, bottomUv));
        }

        private void AddTriangles(int start)
        {
            _triangles.Add(start);
            _triangles.Add(start + 1);
            _triangles.Add(start + 2);
            _triangles.Add(start);
            _triangles.Add(start + 2);
            _triangles.Add(start + 3);
        }

        private void EnsureMesh()
        {
            if (_mesh != null)
                return;

            _mesh = new Mesh { name = "Generated Terrain Mesh" };
        }

        private void ClearBuffers()
        {
            _vertices.Clear();
            _normals.Clear();
            _uvs.Clear();
            _triangles.Clear();
            _faces.Clear();
        }

        private float GetNeighborHeight(System.ReadOnlySpan<TerrainPoint> points, Vector2Int size, int x, int y)
        {
            if (x < 0 || y < 0 || x >= size.x || y >= size.y)
                return _bottomHeight;

            int index = GetIndex(x, y, size.x);
            return GetHeight(points[index]);
        }

        private float GetHeight(TerrainPoint point)
        {
            float elevation = Mathf.Clamp01(point.Elevation);
            return Mathf.Lerp(_bottomHeight, _topHeight, elevation);
        }

        private static int GetIndex(int x, int y, int width)
        {
            return y * width + x;
        }
    }
}
