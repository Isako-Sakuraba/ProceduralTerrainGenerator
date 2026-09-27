using NaughtyAttributes;
using UnityEngine;

namespace TerrainGeneration
{
    [CreateAssetMenu(fileName = "TerrainDefinition", menuName = "Terrain Settings/TerrainDefinition")]
    public class TerrainDefinition : ScriptableObject
    {
        [field: SerializeField]
        public Vector2Int Size { get; private set; } = new Vector2Int(256, 256);

        [field: SerializeField, Required]
        public NoiseMapBase TemperatureNoiseMap { get; private set; }

        [field: SerializeField, Required]
        public NoiseMapBase HumidityNoiseMap { get; private set; }

        [field: SerializeField, Required]
        public NoiseMapBase ElevationNoiseMap { get; private set; }

        [field: Header("Shape Curves")]
        [field: SerializeField, CurveRange(0f, 0f, 1f, 1f, color: EColor.Orange)]
        public AnimationCurve TemperatureShape { get; private set; } = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [field: SerializeField, CurveRange(0f, 0f, 1f, 1f, color: EColor.Blue)]
        public AnimationCurve HumidityShape { get; private set; } = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [field: SerializeField, CurveRange(0f, 0f, 1f, 1f, color: EColor.White)]
        public AnimationCurve ElevationShape { get; private set; } = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        public int Count => Size.x * Size.y;

        public float EvaluateTemperature(float temperature)
        {
            return Mathf.Clamp01(TemperatureShape.Evaluate(temperature));
        }

        public float EvaluateHumidity(float humidity)
        {
            return Mathf.Clamp01(HumidityShape.Evaluate(humidity));
        }

        public float EvaluateElevation(float elevation)
        {
            return Mathf.Clamp01(ElevationShape.Evaluate(elevation));
        }

        public bool HasNoiseMaps()
        {
            return TemperatureNoiseMap != null
                && HumidityNoiseMap != null
                && ElevationNoiseMap != null;
        }
    }
}
