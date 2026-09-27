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

        public abstract void GetNoise(
            FastNoiseLite noise,
            float[] values,
            Vector2Int size,
            Vector2 terrainScale,
            Vector2 terrainOffset);

        public virtual void Initialize(FastNoiseLite noise)
        {
            noise.SetFrequency(Frequency);
        }
    }
}
