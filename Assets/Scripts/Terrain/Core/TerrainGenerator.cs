using NaughtyAttributes;
using System;
using System.Buffers;
using UnityEngine;

namespace TerrainGeneration
{
    public class TerrainGenerator : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private int _seed = 335;
        [SerializeField, Required] private TerrainDefinition _definition;

        private FastNoiseLite _noise;
        private TerrainPoint[] _points;

        public TerrainDefinition Definition => _definition;
        public ReadOnlySpan<TerrainPoint> Points => _points;
        public event Action<TerrainGenerator> Generated = delegate { };

        private void Awake()
        {
            if (!_definition.HasNoiseMaps())
                throw new NullReferenceException("Some NoiseMaps are null! Assign them in the inspector!");

            _noise = new FastNoiseLite(_seed);
            _points = new TerrainPoint[_definition.Count];
        }

        [Button("Generate")]
        public void Generate()
        {
            _noise.SetSeed(_seed);

            int count = _definition.Count;

            float[] temperature = ArrayPool<float>.Shared.Rent(count);
            float[] humidity = ArrayPool<float>.Shared.Rent(count);
            float[] elevation = ArrayPool<float>.Shared.Rent(count);

            try
            {
                GenerateNoise(_definition.TemperatureNoiseMap, temperature);
                GenerateNoise(_definition.HumidityNoiseMap, humidity);
                GenerateNoise(_definition.ElevationNoiseMap, elevation);

                for (int i = 0; i < count; i++)
                {
                    float t = _definition.EvaluateTemperature(temperature[i]);
                    float h = _definition.EvaluateHumidity(humidity[i]);
                    float e = _definition.EvaluateElevation(elevation[i]);

                    _points[i] = new TerrainPoint() { Temperature = t, Humidity = h, Elevation = e };
                }
            }
            finally
            {
                ArrayPool<float>.Shared.Return(temperature);
                ArrayPool<float>.Shared.Return(humidity);
                ArrayPool<float>.Shared.Return(elevation);
            }

            Generated.Invoke(this);
        }

        private void GenerateNoise(NoiseMapBase map, float[] values)
        {
            map.Initialize(_noise);
            map.GetNoise(_noise, values, _definition.Size, Vector2Int.zero);
        }
    }
}
