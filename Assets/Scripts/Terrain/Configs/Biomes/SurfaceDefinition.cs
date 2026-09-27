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

        /// <summary>
        /// Checks whether any biomes are assigned.
        /// </summary>
        public bool HasBiomes()
        {
            return Biomes != null && Biomes.Length > 0;
        }

        /// <summary>
        /// Finds the terrain type for an elevation value.
        /// </summary>
        public ElevationType GetElevationType(float elevation)
        {
            if (ElevationSettings == null)
                return default;

            return ElevationSettings.Evaluate(elevation);
        }

        /// <summary>
        /// Tries to get the elevation where the shore begins.
        /// </summary>
        public bool TryGetWaterElevation(out float elevation)
        {
            if (ElevationSettings != null &&
                ElevationSettings.TryGetElevation(ElevationType.Shore, out elevation))
                return true;

            elevation = 0f;
            return false;
        }
    }
}
