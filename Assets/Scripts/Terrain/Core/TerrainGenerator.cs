using NaughtyAttributes;
using System;
using System.Buffers;
using UnityEngine;

namespace TerrainGeneration
{
    [ExecuteAlways]
    public class TerrainGenerator : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private int _seed = 335;
        [SerializeField, Required] private TerrainDefinition _definition;

        private FastNoiseLite _noise;
        private TerrainPoint[] _points;

        public TerrainDefinition Definition => _definition;
        public int Seed => _seed;
        public Vector2 Scale => _definition != null ? _definition.Scale : Vector2.one;
        public Vector2 Offset => _definition != null ? _definition.Offset : Vector2.zero;
        public ReadOnlySpan<TerrainPoint> Points => _points;
        public event Action<TerrainGenerator> Generated = delegate { };

        /// <summary>
        /// Initializes the generator when it wakes up.
        /// </summary>
        private void Awake()
        {
            Initialize();
        }

        /// <summary>
        /// Initializes the generator after inspector values change.
        /// </summary>
        private void OnValidate()
        {
            Initialize();
        }

        /// <summary>
        /// Prepares the noise generator and point storage.
        /// </summary>
        private void Initialize()
        {
            if (_definition == null || !_definition.HasNoiseMaps())
                return;

            if (_noise == null)
                _noise = new FastNoiseLite(_seed);

            if (_points == null || _points.Length != _definition.Count)
                _points = new TerrainPoint[_definition.Count];
        }

        /// <summary>
        /// Checks that the terrain definition can generate noise.
        /// </summary>
        private void ValidateDefinition()
        {
            if (_definition == null || !_definition.HasNoiseMaps())
                throw new NullReferenceException("Some NoiseMaps are null! Assign them in the inspector!");
        }

        /// <summary>
        /// Generates TerrainPoints and stores them.
        /// </summary>
        [Button("Generate")]
        public void Generate()
        {
            ValidateDefinition();
            Initialize();

            _noise.SetSeed(_seed);

            int count = _definition.Count;

            // Get array from the pool.
            float[] temperature = ArrayPool<float>.Shared.Rent(count);
            float[] humidity = ArrayPool<float>.Shared.Rent(count);
            float[] elevation = ArrayPool<float>.Shared.Rent(count);

            // Generate the noise using Noise Maps
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

        /// <summary>
        /// Generates TerrainPoints based on request and puts result into the output.
        /// </summary>
        /// <param name="request">Request data-class with relevant information.</param>
        /// <param name="output">Where the generated values will be applied to.</param>
        /// <exception cref="ArgumentOutOfRangeException">Throws if chunk size or resolution are not positive</exception>
        public void Generate(TerrainChunkRequest request, TerrainChunkData output)
        {
            ValidateDefinition();
            Initialize();

            if (request.CellsPerEdge <= 0 || request.WorldSize <= 0f)
                throw new ArgumentOutOfRangeException(nameof(request), "Chunk size and resolution must be positive.");

            // Warm up the TerrainChunkData
            output.Prepare(request);
            int resolution = request.SampleResolution;
            int count = resolution * resolution;

            // Prepare the value arrays
            float[] temperature = ArrayPool<float>.Shared.Rent(count);
            float[] humidity = ArrayPool<float>.Shared.Rent(count);
            float[] elevation = ArrayPool<float>.Shared.Rent(count);

            // Generate the values using NoiseMaps
            try
            {
                _noise.SetSeed(request.Seed);
                Vector2 spacing = Vector2.one * request.SampleSpacing;
                Vector2 sampleOrigin = request.WorldOrigin - spacing;
                GenerateNoise(_definition.TemperatureNoiseMap, temperature, resolution, spacing, sampleOrigin);
                GenerateNoise(_definition.HumidityNoiseMap, humidity, resolution, spacing, sampleOrigin);
                GenerateNoise(_definition.ElevationNoiseMap, elevation, resolution, spacing, sampleOrigin);

                for (int i = 0; i < count; i++)
                {
                    output.Points[i] = new TerrainPoint
                    {
                        Temperature = _definition.EvaluateTemperature(temperature[i]),
                        Humidity = _definition.EvaluateHumidity(humidity[i]),
                        Elevation = _definition.EvaluateElevation(elevation[i])
                    };
                }
            }
            finally
            {
                ArrayPool<float>.Shared.Return(temperature);
                ArrayPool<float>.Shared.Return(humidity);
                ArrayPool<float>.Shared.Return(elevation);
            }
        }

        /// <summary>
        /// Helper method for applying a NoiseMap to an array with values
        /// </summary>
        /// <param name="map">NoiseMap.</param>
        /// <param name="values">Output array.</param>
        private void GenerateNoise(NoiseMapBase map, float[] values)
        {
            map.Initialize(_noise);
            map.GetNoise(_noise, values, _definition.Size, Scale, Offset);
        }

        /// <summary>
        /// Advanced helper method for applying a NoiseMap to an array with values, with some extra data.
        /// </summary>
        /// <param name="map">NoiseMap.</param>
        /// <param name="values">Output array.</param>
        /// <param name="resolution">Region resolution.</param>
        /// <param name="sampleSpacing">Spacing between points.</param>
        /// <param name="worldOrigin">World origin position.</param>
        private void GenerateNoise(NoiseMapBase map, float[] values, int resolution,
            Vector2 sampleSpacing, Vector2 worldOrigin)
        {
            map.Initialize(_noise);
            map.GetNoiseRegion(_noise, values, new Vector2Int(resolution, resolution),
                worldOrigin, sampleSpacing, Scale, Offset);
        }
    }
}
