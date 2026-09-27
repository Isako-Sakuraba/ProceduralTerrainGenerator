using NaughtyAttributes;
using UnityEngine;

namespace TerrainGeneration
{
    [CreateAssetMenu(fileName = "NewBiomeDefinition", menuName = "Terrain Settings/BiomeDefinition")]
    public class BiomeDefinition : ScriptableObject
    {
        [field: SerializeField, CurveRange(0f, 0f, 1f, 1f, color: EColor.Orange)]
        public AnimationCurve Temperature { get; private set; } = AnimationCurve.Linear(0, 1, 1, 1);

        [field: SerializeField, CurveRange(0f, 0f, 1f, 1f, color: EColor.Blue)]
        public AnimationCurve Humidity { get; private set; } = AnimationCurve.Linear(0, 1, 1, 1);

        [field: SerializeField, CurveRange(0f, 0f, 1f, 1f, color: EColor.White)]
        public AnimationCurve Elevation { get; private set; } = AnimationCurve.Linear(0, 1, 1, 1);

        [field: SerializeField]
        public BiomeTextureDefinition TextureDefinition { get; private set; }

        /// <summary>
        /// Calculates how well the given conditions match this biome.
        /// </summary>
        public float Evaluate(
            float temperature,
            float humidity,
            float elevation)
        {
            float t = Mathf.Clamp01(Temperature.Evaluate(temperature));
            float h = Mathf.Clamp01(Humidity.Evaluate(humidity));
            float e = Mathf.Clamp01(Elevation.Evaluate(elevation));

            return t * h * e;
        }
    }
}
