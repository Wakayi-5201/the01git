using UnityEngine;

/// <summary>
/// 关卡地图渲染：运行时加载关卡地图精灵，铺满世界尺寸 (1920×1080，16:9 与素材同比例)。
/// 切关时调用 SetMap 即可换图，不需要手动摆对象。
/// </summary>
public class MapRenderer : MonoBehaviour
{
    public SpriteRenderer sr;

    // 渲染层级（背景最靠后）
    public int sortingOrder = GameConfig.OrderMap;

    /// <summary>把地图换成 Resources/Sprites/地图/ 下对应名字的那张。</summary>
    public void SetMap(string mapFile)
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();

        var sprite = AssetLibrary.GetSprite("地图/" + mapFile);
        if (sprite == null)
        {
            Debug.LogError("[MapRenderer] 找不到地图: 地图/" + mapFile);
            return;
        }

        sr.sprite = sprite;
        sr.sortingOrder = sortingOrder;

        // 把图片拉伸到目标世界尺寸（素材 16:9 与世界同比例，实际是等比缩放）
        var t = transform;
        var texSize = sprite.bounds.size;
        float sx = 1f, sy = 1f;
        if (texSize.x > 0.0001f && texSize.y > 0.0001f)
        {
            sx = GameConfig.MapWidth / texSize.x;
            sy = GameConfig.MapHeight / texSize.y;
        }
        t.localScale = new Vector3(sx, sy, 1f);
    }
}
