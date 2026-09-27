using UnityEngine;

namespace TerrainGeneration
{
    public abstract class NoiseMapBase : ScriptableObject
    {
        [Header("Base Settings")]
        [field: SerializeField]
        public float Frequency { get; private set; } = 0.2f;

        [field: SerializeField]
        public Vector2 Scale { get; private set; } = Vector2.one;

        [field: SerializeField]
        public Vector2 Offset { get; private set; } = Vector2.zero;

        /// <summary>
        /// Fills an array with noise values for a terrain grid.
        /// </summary>
        public abstract void GetNoise(
            FastNoiseLite noise,
            float[] values,
            Vector2Int size,
            Vector2 terrainScale,
            Vector2 terrainOffset);

        /// <summary>
        /// Fills an array with noise values for a world region.
        /// </summary>
        public abstract void GetNoiseRegion(
            FastNoiseLite noise,
            float[] values,
            Vector2Int size,
            Vector2 worldOrigin,
            Vector2 sampleSpacing,
            Vector2 terrainScale,
            Vector2 terrainOffset);

        /// <summary>
        /// Applies the base settings to a noise generator.
        /// </summary>
        public virtual void Initialize(FastNoiseLite noise)
        {
            noise.SetFrequency(Frequency);
        }
    }
}
