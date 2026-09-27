using UnityEditor;
using UnityEngine;

public sealed class PixelArtTexturePostprocessor : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Textures/Terrain"))
            return;

        var importer = (TextureImporter)assetImporter;

        importer.textureType = TextureImporterType.Default;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.npotScale = TextureImporterNPOTScale.None;
    }
}
