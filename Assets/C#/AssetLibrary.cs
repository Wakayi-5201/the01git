using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 运行时按名字加载素材（Resources），带缓存。
/// 素材放在 Assets/Resources/Sprites、Assets/Resources/Audio、Assets/Resources/Fonts 下。
/// </summary>
public static class AssetLibrary
{
    static readonly Dictionary<string, Sprite> SpriteCache = new Dictionary<string, Sprite>();
    static readonly Dictionary<string, AudioClip> AudioCache = new Dictionary<string, AudioClip>();
    static Font _font;

    /// <summary>相对 Resources/Sprites 的路径，如 "角色/火角色"。</summary>
    public static Sprite GetSprite(string relativePath)
    {
        if (SpriteCache.TryGetValue(relativePath, out var cached))
            return cached;

        var sprite = Resources.Load<Sprite>("Sprites/" + relativePath);
        if (sprite == null)
            Debug.LogWarning("[AssetLibrary] 找不到精灵: Sprites/" + relativePath);

        SpriteCache[relativePath] = sprite;
        return sprite;
    }

    public static AudioClip GetAudio(string name)
    {
        if (AudioCache.TryGetValue(name, out var cached))
            return cached;

        var clip = Resources.Load<AudioClip>("Audio/" + name);
        AudioCache[name] = clip;
        return clip;
    }

    public static Font GetMainFont()
    {
        if (_font != null) return _font;
        _font = Resources.Load<Font>("Fonts/SourceHanSansCN-Regular");
        if (_font == null) _font = Resources.Load<Font>("Fonts/乐米像素体");
        if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return _font;
    }
}