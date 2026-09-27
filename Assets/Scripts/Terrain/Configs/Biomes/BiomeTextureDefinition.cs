using NaughtyAttributes;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TerrainGeneration
{
    [CreateAssetMenu(fileName = "NewBiomeTextureDefinition", menuName = "Terrain Settings/BiomeTextureDefinition")]
    public class BiomeTextureDefinition : ScriptableObject, IEnumerable<Texture2D>
    {
        [field: Header("Underwater")]
        [field: SerializeField, ShowAssetPreview] public Texture2D DeepOcean { get; private set; }
        [field: SerializeField, ShowAssetPreview] public Texture2D ShallowWater { get; private set; }

        [field: Header("Land")]
        [field: SerializeField, ShowAssetPreview] public Texture2D Shore { get; private set; }
        [field: SerializeField, ShowAssetPreview] public Texture2D Lowland { get; private set; }
        [field: SerializeField, ShowAssetPreview] public Texture2D Highland { get; private set; }

        [field: Header("Mountains")]
        [field: SerializeField, ShowAssetPreview] public Texture2D MountainBase { get; private set; }
        [field: SerializeField, ShowAssetPreview] public Texture2D Mountain { get; private set; }
        [field: SerializeField, ShowAssetPreview] public Texture2D MountainPeak { get; private set; }

        public Texture2D GetTexture(ElevationType elevation)
        {
            return elevation switch
            {
                ElevationType.DeepOcean => DeepOcean,
                ElevationType.ShallowWater => ShallowWater,
                ElevationType.Shore => Shore,
                ElevationType.Lowland => Lowland,
                ElevationType.Highland => Highland,
                ElevationType.MountainBase => MountainBase,
                ElevationType.Mountain => Mountain,
                ElevationType.MountainPeak => MountainPeak,

                _ => throw new System.ArgumentOutOfRangeException(
                    nameof(elevation), elevation, null)
            };
        }

        public IEnumerator<Texture2D> GetTextures()
        {
            yield return DeepOcean;
            yield return ShallowWater;

            yield return Shore;
            yield return Lowland;
            yield return Highland;

            yield return MountainBase;
            yield return Mountain;
            yield return MountainPeak;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<Texture2D> GetEnumerator() => GetTextures();
    }
}