using UnityEngine;

namespace TerrainGeneration
{
    public sealed class TerrainChunkData
    {
        public Vector2Int Coordinate { get; private set; }
        public int Lod { get; private set; }
        public int CellsPerEdge { get; private set; }
        public int SampleResolution => CellsPerEdge + 2;
        public float SampleSpacing { get; private set; }
        public float CellTextureSizeMultiplier { get; private set; }
        public TerrainChunkEdges SkirtEdges { get; private set; }
        public TerrainPoint[] Points { get; private set; }
        public int[] SurfaceIds { get; private set; }

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
