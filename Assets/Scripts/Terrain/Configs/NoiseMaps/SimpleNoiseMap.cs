using UnityEngine;

namespace TerrainGeneration
{
    [CreateAssetMenu(fileName = "NewSimpleNoiseMap", menuName = "Terrain Settings/SimpleNoiseMap")]
    public class SimpleNoiseMap : NoiseMapBase
    {
        [Header("Noise Settings")]
        [field: SerializeField]
        public FastNoiseLite.NoiseType NoiseType { get; private set; }

        public override void Initialize(FastNoiseLite noise)
        {
            base.Initialize(noise);
            noise.SetNoiseType(NoiseType);
        }

        public override void GetNoise(
            FastNoiseLite noise,
            float[] values,
            Vector2Int size,
            Vector2Int offset)
        {
            int width = size.x;
            int height = size.y;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;

                    float sampleX = (x + offset.x) * Scale.x + Offset.x;
                    float sampleY = (y + offset.y) * Scale.y + Offset.y;

                    float sample = noise.GetNoise(sampleX, sampleY);

                    values[index] = Mathf.Clamp01(sample * 0.5f + 0.5f);
                }
            }
        }
    }
}