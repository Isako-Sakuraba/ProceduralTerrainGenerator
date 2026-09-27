using UnityEngine;

namespace TerrainGeneration
{
    /// <summary>
    /// What edges should be visible for a chunk.
    /// </summary>
    [System.Flags]
    public enum TerrainChunkEdges
    {
        None = 0,
        North = 1,
        South = 2,
        East = 4,
        West = 8,
        All = North | South | East | West
    }

    /// <summary>
    /// A data class that represents a calculation request for generators.
    /// </summary>
    public readonly struct TerrainChunkRequest
    {
        public readonly Vector2Int Coordinate;
        public readonly int Lod;
        public readonly Vector2 WorldOrigin;
        public readonly float WorldSize;
        public readonly int CellsPerEdge;
        public readonly float CellTextureSizeMultiplier;
        public readonly TerrainChunkEdges SkirtEdges;
        public readonly int Seed;

        public float SampleSpacing => WorldSize / CellsPerEdge;
        public int SampleResolution => CellsPerEdge + 2;

        /// <summary>
        /// Creates a chunk request with skirts on every edge.
        /// </summary>
        public TerrainChunkRequest(Vector2Int coordinate, int lod, Vector2 worldOrigin,
            float worldSize, int cellsPerEdge, float cellTextureSizeMultiplier, int seed)
            : this(coordinate, lod, worldOrigin, worldSize, cellsPerEdge,
                cellTextureSizeMultiplier, seed, TerrainChunkEdges.All)
        {
        }

        /// <summary>
        /// Creates a chunk request with the chosen skirt edges.
        /// </summary>
        public TerrainChunkRequest(Vector2Int coordinate, int lod, Vector2 worldOrigin,
            float worldSize, int cellsPerEdge, float cellTextureSizeMultiplier, int seed,
            TerrainChunkEdges skirtEdges)
        {
            Coordinate = coordinate;
            Lod = lod;
            WorldOrigin = worldOrigin;
            WorldSize = worldSize;
            CellsPerEdge = cellsPerEdge;
            CellTextureSizeMultiplier = Mathf.Max(0.01f, cellTextureSizeMultiplier);
            SkirtEdges = skirtEdges;
            Seed = seed;
        }
    }
}
