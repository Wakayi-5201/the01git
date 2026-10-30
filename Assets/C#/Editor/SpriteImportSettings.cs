using UnityEditor;
using UnityEngine;

/// <summary>
/// 让 Assets/Resources/Sprites 下的图片统一按“Sprite / 单图 / FullRect / 居中”导入。
/// 目的：保留完整矩形（不裁透明边），使代码按世界尺寸缩放时精确可控。
/// 若素材看起来尺寸异常，右键 Sprites 文件夹 -> Reimport 即可重新应用。
/// </summary>
public class SpriteImportSettings : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        string path = assetPath.Replace('\\', '/');
        if (!path.Contains("/Resources/Sprites/")) return;

        var importer = assetImporter as TextureImporter;
        if (importer == null) return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Bilinear;
        importer.maxTextureSize = 4096;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        importer.SetTextureSettings(settings);
    }
}