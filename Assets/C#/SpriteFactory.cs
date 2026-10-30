using UnityEngine;

/// <summary>
/// 运行时创建精灵对象的工具。
/// </summary>
public static class SpriteFactory
{
    static Sprite _white;

    /// <summary>1x1 纯白精灵（PPU=1，bounds 为 1x1），用于血条/纯色块。</summary>
    public static Sprite WhiteSprite()
    {
        if (_white != null) return _white;
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.filterMode = FilterMode.Bilinear;
        tex.Apply();
        _white = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        return _white;
    }

    /// <summary>运行时生成一个圆形精灵（用于敌人子弹等）。</summary>
    public static Sprite CircleSprite(int size, Color color)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float r = size * 0.5f;
        float r2 = r * r;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - r + 0.5f;
                float dy = y - r + 0.5f;
                bool inside = dx * dx + dy * dy <= r2;
                tex.SetPixel(x, y, inside ? color : new Color(0, 0, 0, 0));
            }
        }
        tex.filterMode = FilterMode.Bilinear;
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 1f);
    }

    /// <summary>柔光环精灵：外圈亮环 + 内圈淡光，用于大招增益光环（治愈/冰霜）。</summary>
    public static Sprite RingSprite(int size, Color color, float thickness = 0.10f)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float r = size * 0.5f;
        float ringAt = 1f - thickness;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - r + 0.5f;
                float dy = y - r + 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / r;   // 0=圆心 1=边缘

                float ring = 1f - Mathf.Clamp01(Mathf.Abs(d - ringAt) / thickness);
                ring *= ring;                                  // 让环更实一点
                float glow = Mathf.Clamp01(1f - d) * 0.22f;    // 内圈淡淡的填充光

                float a = Mathf.Clamp01(ring + glow);
                tex.SetPixel(x, y, d > 1f ? new Color(0, 0, 0, 0)
                                          : new Color(color.r, color.g, color.b, a));
            }
        }

        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 1f);
    }

    // ---------------------------------------------------------------
    // UI 纹理（供 uGUI 的 Image 使用）
    // ---------------------------------------------------------------

    /// <summary>
    /// 条状填充纹理：顶部一条高光 + 上亮下暗渐变，整体为白色，
    /// 交给 Image.color 染色即可一套纹理出多种颜色（血条红/绿、能量蓝/黄等）。
    /// </summary>
    public static Sprite BarFillSprite(int w = 64, int h = 16)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int y = 0; y < h; y++)
        {
            float t = h > 1 ? (float)y / (h - 1) : 1f;   // 0 = 底边，1 = 顶边
            float a = t > 0.70f ? 1f                     // 顶部高光带
                                : 0.52f + 0.48f * t;     // 主体渐变：上亮下暗
            var c = new Color(1f, 1f, 1f, a);
            for (int x = 0; x < w; x++) tex.SetPixel(x, y, c);
        }
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 1f);
    }

    /// <summary>
    /// 九宫格底框：最外 2px 亮边 + 边框暗底 + 中间半透明填充。
    /// 返回的 Sprite 已带 border，配合 Image.Type.Sliced 可任意拉伸不变形。
    /// </summary>
    public static Sprite FrameSprite(int size = 48, int border = 8,
                                     Color? edge = null, Color? inner = null, Color? fill = null)
    {
        Color e = edge ?? (Color)new Color32(120, 128, 155, 255);
        Color i = inner ?? (Color)new Color32(14, 14, 24, 235);
        Color f = fill ?? (Color)new Color32(22, 22, 34, 190);

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        int b = Mathf.Clamp(border, 2, size / 2);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool outer = x < 2 || y < 2 || x >= size - 2 || y >= size - 2;
                bool inBorder = x < b || y < b || x >= size - b || y >= size - b;
                tex.SetPixel(x, y, outer ? e : inBorder ? i : f);
            }
        }

        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.Apply();

        // 注意：Sprite.border 是只读属性，不能事后赋值，必须在 Create 的重载里传。
        // 另外九宫格拉伸必须用 FullRect（默认的 Tight 会按 alpha 裁剪网格，Sliced 会花）。
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 1f,
            0u, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
    }

    /// <summary>创建一个带 SpriteRenderer 的 GameObject，并缩放到指定世界尺寸。</summary>
    public static SpriteRenderer NewSprite(string goName, Sprite sprite, Vector2 worldSize, int sortingOrder, Transform parent = null)
    {
        var go = new GameObject(goName);
        if (parent != null) go.transform.SetParent(parent, false);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = sortingOrder;

        ApplyWorldSize(go.transform, sprite, worldSize);
        return sr;
    }

    /// <summary>把一个对象缩放到“精灵原始尺寸 -> 目标世界尺寸”。</summary>
    public static void ApplyWorldSize(Transform t, Sprite sprite, Vector2 worldSize)
    {
        if (sprite == null) return;
        Vector2 b = sprite.bounds.size;
        float sx = b.x > 0.0001f ? worldSize.x / b.x : 1f;
        float sy = b.y > 0.0001f ? worldSize.y / b.y : 1f;
        t.localScale = new Vector3(sx, sy, 1f);
    }
}