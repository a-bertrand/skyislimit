using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Réglages d'import automatiques pour tout le pixel art de Assets/_Game/Art/Sprites :
/// sprite 16 px par unité, filtre Point (pixels nets), sans compression ni mipmaps.
/// Conventions de nommage :
///  - nom_n.png    = normal map de nom.png (relief pour les lumières 2D), branchée automatiquement
///  - nom_glow.png = parties lumineuses de nom.png (affichées sans subir l'éclairage)
///  - Tiles/       = pivot au centre ; le reste = pivot en bas au centre (les pieds)
/// </summary>
public class PixelArtImporter : AssetPostprocessor
{
    public const string ArtRoot = "Assets/_Game/Art/Sprites/";
    public const int PixelsPerUnit = 16;
    private const string NormalSuffix = "_n";
    private const string NormalMapName = "_NormalMap";

    private static bool IsPixelArt(string path) =>
        path.Replace('\\', '/').StartsWith(ArtRoot) && path.EndsWith(".png");

    private static bool IsNormalMap(string path) =>
        Path.GetFileNameWithoutExtension(path).EndsWith(NormalSuffix);

    public static string NormalMapPathFor(string spritePath) =>
        spritePath.Substring(0, spritePath.Length - 4) + NormalSuffix + ".png";

    // Polices pixel (Assets/_Game/Resources/Fonts) : rendu "Hinted Raster", sans flou
    private void OnPreprocessAsset()
    {
        if (assetImporter is TrueTypeFontImporter fontImporter && assetPath.StartsWith("Assets/_Game/Resources/Fonts/"))
            fontImporter.fontRenderingMode = FontRenderingMode.HintedRaster;
    }

    private void OnPreprocessTexture()
    {
        if (!IsPixelArt(assetPath)) return;

        var importer = (TextureImporter)assetImporter;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = TextureWrapMode.Clamp;

        if (IsNormalMap(assetPath))
        {
            // Une normal map contient des directions, pas des couleurs : pas de correction gamma
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.alphaIsTransparency = false;
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.alphaIsTransparency = true;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        var isTile = assetPath.Replace('\\', '/').Contains("/Tiles/") || Path.GetFileName(assetPath).StartsWith("shadow");
        settings.spriteAlignment = (int)(isTile ? SpriteAlignment.Center : SpriteAlignment.BottomCenter);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteExtrude = 0;
        importer.SetTextureSettings(settings);

        // Branche la normal map comme "texture secondaire" du sprite (utilisée par les lumières 2D)
        var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalMapPathFor(assetPath));
        if (normal != null)
        {
            importer.secondarySpriteTextures = new[]
            {
                new SecondarySpriteTexture { name = NormalMapName, texture = normal }
            };
        }
    }

    // Si la normal map a été importée APRÈS le sprite, on réimporte le sprite pour la brancher
    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        foreach (var path in imported)
        {
            if (!IsPixelArt(path) || !IsNormalMap(path)) continue;

            var spritePath = path.Substring(0, path.Length - NormalSuffix.Length - 4) + ".png";
            if (!File.Exists(spritePath)) continue;
            if (!HasNormalMap(spritePath))
                AssetDatabase.ImportAsset(spritePath, ImportAssetOptions.ForceUpdate);
        }
    }

    public static bool HasNormalMap(string spritePath)
    {
        var importer = AssetImporter.GetAtPath(spritePath) as TextureImporter;
        if (importer == null) return false;
        var secondaries = importer.secondarySpriteTextures;
        if (secondaries == null) return false;
        foreach (var s in secondaries)
            if (s.name == NormalMapName && s.texture != null) return true;
        return false;
    }
}
