using UnityEditor;

/// <summary>
/// Forces pixel-art-correct import settings (point filtering, no compression, no mip maps) for
/// every texture under Assets/Resources/Art -- otherwise Unity's default bilinear-filtered,
/// compressed import blurs the small hand-cropped 16x16-sourced sprites used by the UI Toolkit
/// screens. Runs automatically on import, so future art drops into that folder need no manual
/// per-file configuration.
/// </summary>
public sealed class ArtImportPostprocessor : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (!assetPath.Replace('\\', '/').Contains("/Resources/Art/"))
            return;

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Default;
        importer.filterMode = UnityEngine.FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
    }
}
