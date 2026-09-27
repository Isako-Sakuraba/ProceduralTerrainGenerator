using NaughtyAttributes;
using UnityEngine;

namespace TerrainGeneration
{
    [CreateAssetMenu(fileName = "NewSurfaceDefinition", menuName = "Terrain Settings/SurfaceDefinition")]
    public class SurfaceDefinition : ScriptableObject
    {
        [field: SerializeField, Required]
        public BiomeDefinition[] Biomes { get; private set; } = new BiomeDefinition[0];

        [field: SerializeField]
        public ElevationSettings ElevationSettings { get; private set; } = new ElevationSettings();

        public bool HasBiomes()
        {
            return Biomes != null && Biomes.Length > 0;
        }

        public ElevationType GetElevationType(float elevation)
        {
            if (ElevationSettings == null)
                return default;

            return ElevationSettings.Evaluate(elevation);
        }
    }
}
