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
    }
}
