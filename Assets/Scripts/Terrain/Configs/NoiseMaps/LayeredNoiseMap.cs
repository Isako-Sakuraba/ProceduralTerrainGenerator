using UnityEngine;

namespace TerrainGeneration
{
    [CreateAssetMenu(fileName = "NewLayeredNoiseMap", menuName = "Terrain Settings/LayeredNoiseMap")]
    public class LayeredNoiseMap : NoiseMapBase
    {
        [Header("Layers Settings")]
        [field: SerializeField]
        public NoiseLayer[] Layers { get; private set; } = new NoiseLayer[0];

        [Header("Octave Settings")]
        [field: SerializeField]
        public float Lacunarity { get; private set; } = 1f;

        [field: SerializeField]
        public float AmplitudeMultiplier { get; private set; } = 1f;

        public override void GetNoise(
            FastNoiseLite noise,
            float[] values,
            Vector2Int size,
            Vector2Int offset)
        {
            int width = size.x;
            int height = size.y;
            int count = width * height;

            System.Array.Clear(values, 0, count);

            float frequency = Frequency;
            float amplitude = 1f;
            float amplitudeSum = 0f;

            foreach (NoiseLayer layer in Layers)
            {
                if (layer == null || !layer.Active)
                    continue;

                noise.SetNoiseType(layer.NoiseType);
                noise.SetFrequency(frequency);

                float scaleX = layer.Scale.x * Scale.x;
                float scaleY = layer.Scale.y * Scale.y;

                float offsetX = layer.Offset.x + Offset.x;
                float offsetY = layer.Offset.y + Offset.y;

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int index = y * width + x;

                        float sampleX = (x + offset.x) * scaleX + offsetX;
                        float sampleY = (y + offset.y) * scaleY + offsetY;

                        float sample = noise.GetNoise(sampleX, sampleY);

                        values[index] += sample * amplitude;
                    }
                }

                amplitudeSum += amplitude;

                frequency *= Lacunarity;
                amplitude *= AmplitudeMultiplier;
            }

            if (Mathf.Approximately(amplitudeSum, 0f))
            {
                for (int i = 0; i < count; i++)
                    values[i] = 0.5f;

                return;
            }

            float inverseAmplitude = 1f / amplitudeSum;

            for (int i = 0; i < count; i++)
            {
                float normalized = values[i] * inverseAmplitude;

                values[i] = Mathf.Clamp01(normalized * 0.5f + 0.5f);
            }
        }
    }
}