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

        public int Count => Size.x * Size.y;

        public bool HasNoiseMaps()
        {
            return TemperatureNoiseMap != null
                && HumidityNoiseMap != null
                && ElevationNoiseMap != null;
        }
    }
}