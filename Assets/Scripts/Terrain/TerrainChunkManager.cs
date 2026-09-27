using System;
using System.Collections.Generic;
using UnityEngine;

namespace TerrainGeneration
{
    public sealed class TerrainChunkManager : MonoBehaviour
    {
        /// <summary>
        /// Stores the settings for one terrain detail level.
        /// </summary>
        [Serializable]
        private struct LodLevel
        {
            [Min(1)] public int CellsPerEdge;
            [Min(1)] public int WaterResolution;
            [Min(0.01f)] public float CellTextureSizeMultiplier;
            [Min(0f)] public float MaximumDistance;
        }

        /// <summary>
        /// Stores the state of an active terrain chunk.
        /// </summary>
        private sealed class ActiveChunk
        {
            public TerrainChunkView View;
            public int Lod;
            public int Version;
        }

        /// <summary>
        /// Stores a pending chunk generation request.
        /// </summary>
        private readonly struct QueuedGeneration
        {
            public readonly Vector2Int Coordinate;
            public readonly int Lod;
            public readonly int Version;
            public readonly float Distance;
            public readonly bool GenerateData;

            /// <summary>
            /// Creates a queued generation request.
            /// </summary>
            public QueuedGeneration(Vector2Int coordinate, int lod, int version, float distance, bool generateData)
            {
                Coordinate = coordinate;
                Lod = lod;
                Version = version;
                Distance = distance;
                GenerateData = generateData;
            }
        }

        [Header("Pipeline")]
        [SerializeField] private TerrainGenerator _terrainGenerator;
        [SerializeField] private TerrainSurfaceGenerator _surfaceGenerator;
        [SerializeField] private TerrainMeshGenerator _meshGenerator;

        [Header("Rendering")]
        [SerializeField] private TerrainChunkView _chunkPrefab;
        [SerializeField] private Material _material;
        [SerializeField] private Material _waterMaterial;
        [SerializeField] private Transform _target;

        [Header("Streaming")]
        [SerializeField, Min(1f)] private float _chunkSize = 64f;
        [SerializeField, Min(0f)] private float _loadingDistance = 192f;
        [SerializeField, Min(1)] private int _generationsPerFrame = 1;
        [SerializeField, Min(0.01f)] private float _lodUpdateDistance = 1f;
        [SerializeField, Min(0f)] private float _lodHysteresis = 4f;
        [SerializeField] private LodLevel[] _lodLevels =
        {
            new LodLevel { CellsPerEdge = 64, WaterResolution = 16, CellTextureSizeMultiplier = 1f, MaximumDistance = 64f },
            new LodLevel { CellsPerEdge = 32, WaterResolution = 8, CellTextureSizeMultiplier = 1f, MaximumDistance = 128f },
            new LodLevel { CellsPerEdge = 16, WaterResolution = 4, CellTextureSizeMultiplier = 1f, MaximumDistance = 256f },
            new LodLevel { CellsPerEdge = 8, WaterResolution = 2, CellTextureSizeMultiplier = 1f, MaximumDistance = float.MaxValue }
        };

        private readonly Dictionary<Vector2Int, ActiveChunk> _active = new Dictionary<Vector2Int, ActiveChunk>();
        private readonly Stack<TerrainChunkView> _pool = new Stack<TerrainChunkView>();
        private readonly Dictionary<Vector2Int, QueuedGeneration> _queue =
            new Dictionary<Vector2Int, QueuedGeneration>();
        private readonly HashSet<Vector2Int> _desired = new HashSet<Vector2Int>();
        private readonly List<Vector2Int> _release = new List<Vector2Int>();
        private readonly HashSet<Vector2Int> _seamRefresh = new HashSet<Vector2Int>();
        private Vector2Int _lastTargetChunk = new Vector2Int(int.MinValue, int.MinValue);
        private Vector3 _lastLodPosition = new Vector3(float.PositiveInfinity, 0f, 0f);

        /// <summary>
        /// Finds the target and creates the initial chunk selection.
        /// </summary>
        private void Start()
        {
            if (_target == null && Camera.main != null)
                _target = Camera.main.transform;
            RefreshSelection(true);
        }

        /// <summary>
        /// Updates chunk selection and processes queued generation work.
        /// </summary>
        private void Update()
        {
            if (_target == null) return;

            Vector3 local = transform.InverseTransformPoint(_target.position);
            Vector2Int targetChunk = WorldToChunk(local);
            float lodMove = new Vector2(local.x - _lastLodPosition.x, local.z - _lastLodPosition.z).sqrMagnitude;
            if (targetChunk != _lastTargetChunk || lodMove >= _lodUpdateDistance * _lodUpdateDistance)
                RefreshSelection(false);

            ProcessQueue();
        }

        /// <summary>
        /// Refreshes the chunks and detail levels around the target.
        /// </summary>
        public void RefreshSelection(bool force)
        {
            if (!ValidateConfiguration() || _target == null) return;

            Vector3 targetLocal3 = transform.InverseTransformPoint(_target.position);
            Vector2 targetLocal = new Vector2(targetLocal3.x, targetLocal3.z);
            Vector2Int center = WorldToChunk(targetLocal3);
            if (!force && center == _lastTargetChunk &&
                (targetLocal3 - _lastLodPosition).sqrMagnitude < _lodUpdateDistance * _lodUpdateDistance)
                return;

            _lastTargetChunk = center;
            _lastLodPosition = targetLocal3;
            _desired.Clear();
            _seamRefresh.Clear();
            int radius = Mathf.CeilToInt(_loadingDistance / _chunkSize) + 1;

            for (int z = center.y - radius; z <= center.y + radius; z++)
            for (int x = center.x - radius; x <= center.x + radius; x++)
            {
                Vector2Int coordinate = new Vector2Int(x, z);
                float distance = DistanceToChunk(targetLocal, coordinate);
                if (distance > _loadingDistance) continue;

                _desired.Add(coordinate);
                if (!_active.TryGetValue(coordinate, out ActiveChunk chunk))
                {
                    TerrainChunkView view = Acquire(coordinate);
                    chunk = new ActiveChunk { View = view, Lod = -1 };
                    _active.Add(coordinate, chunk);
                    MarkNeighborsForSeamRefresh(coordinate);
                }

                int lod = SelectLod(distance, chunk.Lod);
                if (lod != chunk.Lod)
                {
                    chunk.Lod = lod;
                    chunk.Version = chunk.View.RequestLod(lod);
                    QueueGeneration(coordinate, chunk, distance, true);
                    MarkNeighborsForSeamRefresh(coordinate);
                }
            }

            _release.Clear();
            foreach (KeyValuePair<Vector2Int, ActiveChunk> pair in _active)
                if (!_desired.Contains(pair.Key)) _release.Add(pair.Key);
            for (int i = 0; i < _release.Count; i++)
            {
                MarkNeighborsForSeamRefresh(_release[i]);
                Release(_release[i]);
            }

            foreach (Vector2Int coordinate in _seamRefresh)
            {
                if (!_active.TryGetValue(coordinate, out ActiveChunk chunk)) continue;
                QueueGeneration(coordinate, chunk, DistanceToChunk(targetLocal, coordinate), false);
            }
        }

        /// <summary>
        /// Generates the nearest queued chunks within the frame budget.
        /// </summary>
        private void ProcessQueue()
        {
            int budget = _generationsPerFrame;
            while (budget > 0 && _queue.Count > 0)
            {
                QueuedGeneration queued = default;
                bool found = false;
                foreach (QueuedGeneration candidate in _queue.Values)
                {
                    if (found && candidate.Distance >= queued.Distance) continue;
                    queued = candidate;
                    found = true;
                }
                if (!found) break;
                _queue.Remove(queued.Coordinate);

                if (!_active.TryGetValue(queued.Coordinate, out ActiveChunk chunk) ||
                    chunk.Version != queued.Version || chunk.Lod != queued.Lod)
                    continue;

                budget--;

                Vector3 localOrigin = new Vector3(queued.Coordinate.x * _chunkSize, 0f, queued.Coordinate.y * _chunkSize);
                Vector3 worldOrigin3 = transform.TransformPoint(localOrigin);
                TerrainChunkEdges skirtEdges = GetSkirtEdges(queued.Coordinate, queued.Lod);
                TerrainChunkRequest request = new TerrainChunkRequest(queued.Coordinate, queued.Lod,
                    new Vector2(worldOrigin3.x, worldOrigin3.z), _chunkSize,
                    _lodLevels[queued.Lod].CellsPerEdge,
                    Mathf.Max(0.01f, _lodLevels[queued.Lod].CellTextureSizeMultiplier),
                    _terrainGenerator.Seed, skirtEdges);

                if (queued.GenerateData)
                {
                    _terrainGenerator.Generate(request, chunk.View.Data);
                    _surfaceGenerator.Generate(chunk.View.Data.Points, chunk.View.Data.SurfaceIds);
                }
                else
                {
                    chunk.View.Data.Prepare(request);
                }
                chunk.View.Apply(_meshGenerator, _surfaceGenerator, request, queued.Version,
                    _waterMaterial, Mathf.Max(1, _lodLevels[queued.Lod].WaterResolution),
                    GetMaximumWaterResolution(), queued.GenerateData);
            }
        }

        /// <summary>
        /// Adds or updates a chunk generation request.
        /// </summary>
        private void QueueGeneration(Vector2Int coordinate, ActiveChunk chunk, float distance,
            bool generateData)
        {
            if (_queue.TryGetValue(coordinate, out QueuedGeneration pending))
                generateData |= pending.GenerateData;

            chunk.Version = chunk.View.RequestLod(chunk.Lod);
            _queue[coordinate] = new QueuedGeneration(coordinate, chunk.Lod, chunk.Version,
                distance, generateData);
        }

        /// <summary>
        /// Gets a chunk view from the pool or creates one.
        /// </summary>
        private TerrainChunkView Acquire(Vector2Int coordinate)
        {
            TerrainChunkView view;
            if (_pool.Count > 0) view = _pool.Pop();
            else if (_chunkPrefab != null) view = Instantiate(_chunkPrefab, transform);
            else
            {
                GameObject instance = new GameObject("Terrain Chunk");
                instance.transform.SetParent(transform, false);
                view = instance.AddComponent<TerrainChunkView>();
            }

            view.transform.SetParent(transform, false);
            view.Assign(coordinate, new Vector3(coordinate.x * _chunkSize, 0f, coordinate.y * _chunkSize), _material);
            return view;
        }

        /// <summary>
        /// Deactivates a chunk and returns its view to the pool.
        /// </summary>
        private void Release(Vector2Int coordinate)
        {
            ActiveChunk chunk = _active[coordinate];
            _active.Remove(coordinate);
            _queue.Remove(coordinate);
            chunk.View.Deactivate();
            _pool.Push(chunk.View);
        }

        /// <summary>
        /// Finds the edges that need skirts for a chunk.
        /// </summary>
        private TerrainChunkEdges GetSkirtEdges(Vector2Int coordinate, int lod)
        {
            TerrainChunkEdges skirtEdges = TerrainChunkEdges.None;
            AddOwnedEdge(ref skirtEdges, TerrainChunkEdges.North, coordinate + Vector2Int.up, lod);
            AddOwnedEdge(ref skirtEdges, TerrainChunkEdges.South, coordinate + Vector2Int.down, lod);
            AddOwnedEdge(ref skirtEdges, TerrainChunkEdges.East, coordinate + Vector2Int.right, lod);
            AddOwnedEdge(ref skirtEdges, TerrainChunkEdges.West, coordinate + Vector2Int.left, lod);
            return skirtEdges;
        }

        /// <summary>
        /// Adds an edge when this chunk owns the seam.
        /// </summary>
        private void AddOwnedEdge(ref TerrainChunkEdges skirtEdges, TerrainChunkEdges edge,
            Vector2Int neighborCoordinate, int lod)
        {
            if (!_active.TryGetValue(neighborCoordinate, out ActiveChunk neighbor) || lod > neighbor.Lod)
                skirtEdges |= edge;
        }

        /// <summary>
        /// Marks neighboring chunks for seam updates.
        /// </summary>
        private void MarkNeighborsForSeamRefresh(Vector2Int coordinate)
        {
            _seamRefresh.Add(coordinate + Vector2Int.up);
            _seamRefresh.Add(coordinate + Vector2Int.down);
            _seamRefresh.Add(coordinate + Vector2Int.right);
            _seamRefresh.Add(coordinate + Vector2Int.left);
        }

        /// <summary>
        /// Selects a detail level for the given distance.
        /// </summary>
        private int SelectLod(float distance, int current)
        {
            int selected = _lodLevels.Length - 1;
            for (int i = 0; i < _lodLevels.Length; i++)
                if (distance <= _lodLevels[i].MaximumDistance) { selected = i; break; }

            if (current < 0 || current >= _lodLevels.Length || selected == current) return selected;
            if (selected > current && distance < _lodLevels[current].MaximumDistance + _lodHysteresis) return current;
            if (selected < current && distance > _lodLevels[selected].MaximumDistance - _lodHysteresis) return current;
            return selected;
        }

        /// <summary>
        /// Gets the highest configured water resolution.
        /// </summary>
        private int GetMaximumWaterResolution()
        {
            int resolution = 1;
            for (int i = 0; i < _lodLevels.Length; i++)
                resolution = Mathf.Max(resolution, _lodLevels[i].WaterResolution);
            return resolution;
        }

        /// <summary>
        /// Measures the distance from a point to a chunk boundary.
        /// </summary>
        private float DistanceToChunk(Vector2 point, Vector2Int coordinate)
        {
            float minX = coordinate.x * _chunkSize;
            float minY = coordinate.y * _chunkSize;
            float x = Mathf.Max(minX - point.x, 0f, point.x - (minX + _chunkSize));
            float y = Mathf.Max(minY - point.y, 0f, point.y - (minY + _chunkSize));
            return Mathf.Sqrt(x * x + y * y);
        }

        /// <summary>
        /// Converts a local position to chunk coordinates.
        /// </summary>
        private Vector2Int WorldToChunk(Vector3 localPosition)
        {
            return new Vector2Int(Mathf.FloorToInt(localPosition.x / _chunkSize),
                Mathf.FloorToInt(localPosition.z / _chunkSize));
        }

        /// <summary>
        /// Checks that the required terrain settings are available.
        /// </summary>
        private bool ValidateConfiguration()
        {
            return _terrainGenerator != null && _surfaceGenerator != null && _meshGenerator != null &&
                _lodLevels != null && _lodLevels.Length > 0;
        }

        /// <summary>
        /// Keeps inspector values within valid limits.
        /// </summary>
        private void OnValidate()
        {
            _chunkSize = Mathf.Max(1f, _chunkSize);
            _generationsPerFrame = Mathf.Max(1, _generationsPerFrame);
            if (_lodLevels == null) return;
            for (int i = 0; i < _lodLevels.Length; i++)
            {
                _lodLevels[i].CellsPerEdge = Mathf.Max(1, _lodLevels[i].CellsPerEdge);
                if (_lodLevels[i].WaterResolution < 1)
                    _lodLevels[i].WaterResolution = Mathf.Max(1, _lodLevels[i].CellsPerEdge / 4);
                _lodLevels[i].CellTextureSizeMultiplier = Mathf.Max(0.01f,
                    _lodLevels[i].CellTextureSizeMultiplier);
            }
        }

        /// <summary>
        /// Clears generated chunk state when the manager is destroyed.
        /// </summary>
        private void OnDestroy()
        {
            _queue.Clear();
            _active.Clear();
            _pool.Clear();
            if (_surfaceGenerator != null)
                _surfaceGenerator.Release();
        }
    }
}
