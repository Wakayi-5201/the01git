using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 背景图「等比铺满」（cover）：保持美术图原始宽高比放大到刚好盖住父节点，
/// 多出来的部分溢出到屏幕外被裁掉。
/// 解决老问题：4:3 的主页面背景被拉伸到 16:9（甚至更宽）的画布时会横向变形。
/// 用法：把脚本挂在背景 Image 上即可，会自动跟随父节点尺寸变化。
/// </summary>
[RequireComponent(typeof(Image))]
public class BackgroundCover : MonoBehaviour
{
    Image img;
    RectTransform rt;
    RectTransform parentRT;
    Vector2 lastSize = new Vector2(-1f, -1f);

    void Awake() => Cache();

    void OnEnable()
    {
        Cache();
        lastSize = new Vector2(-1f, -1f);
        Apply();
    }

    void OnDisable()
    {
        lastSize = new Vector2(-1f, -1f);
    }

    void LateUpdate()
    {
        Cache();
        Apply();
    }

    void Cache()
    {
        if (img == null) img = GetComponent<Image>();
        if (rt == null) rt = (RectTransform)transform;
        if (parentRT == null && transform.parent != null) parentRT = transform.parent as RectTransform;
    }

    /// <summary>尺寸变化时按父节点大小重新计算一次（尺寸没变就不动，省开销）。</summary>
    public void Apply()
    {
        if (img == null || img.sprite == null || rt == null) return;

        var p = parentRT != null ? parentRT : rt;
        Vector2 ps = p.rect.size;
        if (ps.x <= 1f || ps.y <= 1f) return;
        if (Mathf.Approximately(ps.x, lastSize.x) && Mathf.Approximately(ps.y, lastSize.y)) return;
        lastSize = ps;

        img.type = Image.Type.Simple;
        img.preserveAspect = true;

        float spriteAspect = img.sprite.rect.width / Mathf.Max(1f, img.sprite.rect.height);
        float parentAspect = ps.x / ps.y;

        float w, h;
        if (parentAspect > spriteAspect)
        {
            // 父节点更宽 → 以宽为准，上下溢出
            w = ps.x;
            h = ps.x / spriteAspect;
        }
        else
        {
            // 父节点更高 → 以高为准，左右溢出
            h = ps.y;
            w = ps.y * spriteAspect;
        }

        // 居中摆放：铺满父节点，超出的部分落到屏幕外被裁掉
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(w, h);
    }
}
