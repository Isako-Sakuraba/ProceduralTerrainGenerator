using UnityEngine;

namespace TerrainGeneration
{
    /// <summary>
    /// Data class that stores single chunk's data
    /// </summary>
    public sealed class TerrainChunkData
    {
        public Vector2Int Coordinate { get; private set; } // World coordinate
        public int Lod { get; private set; } // LOD level
        public int CellsPerEdge { get; private set; }
        public int SampleResolution => CellsPerEdge + 2;
        public float SampleSpacing { get; private set; }
        public float CellTextureSizeMultiplier { get; private set; }
        public TerrainChunkEdges SkirtEdges { get; private set; }
        public TerrainPoint[] Points { get; private set; } // Stored points
        public int[] SurfaceIds { get; private set; } // Resolved surface Ids for biome textures

        /// <summary>
        /// Prepares this data container for a chunk request.
        /// </summary>
        public void Prepare(TerrainChunkRequest request)
        {
            Coordinate = request.Coordinate;
            Lod = request.Lod;
            CellsPerEdge = request.CellsPerEdge;
            SampleSpacing = request.SampleSpacing;
            CellTextureSizeMultiplier = request.CellTextureSizeMultiplier;
            SkirtEdges = request.SkirtEdges;

            int count = request.SampleResolution * request.SampleResolution;
            if (Points == null || Points.Length != count)
                Points = new TerrainPoint[count];
            if (SurfaceIds == null || SurfaceIds.Length != count)
                SurfaceIds = new int[count];
        }
    }
}
