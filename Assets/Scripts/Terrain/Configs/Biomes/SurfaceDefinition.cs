using NaughtyAttributes;
using UnityEngine;

namespace TerrainGeneration
{
    [CreateAssetMenu(fileName = "NewSurfaceDefinition", menuName = "Terrain Settings/SurfaceDefinition")]
    public class SurfaceDefinition : ScriptableObject
    {
        [field: SerializeField, Required]
        public BiomeDefinition[] Biomes { get; private set; } = new BiomeDefinition[0];

        public bool HasBiomes()
        {
            return Biomes != null && Biomes.Length > 0;
        }

        public ElevationType GetElevationType(float elevation)
        {
            if (elevation < 0.2f)
                return ElevationType.DeepOcean;
            if (elevation < 0.32f)
                return ElevationType.ShallowWater;
            if (elevation < 0.38f)
                return ElevationType.Shore;
            if (elevation < 0.58f)
                return ElevationType.Lowland;
            if (elevation < 0.72f)
                return ElevationType.Highland;
            if (elevation < 0.82f)
                return ElevationType.MountainBase;
            if (elevation < 0.93f)
                return ElevationType.Mountain;

            return ElevationType.MountainPeak;
        }
    }
}
