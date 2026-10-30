using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 特别感谢。对应 pygame 的 draw_credits —— 名字依次淡入，按任意键返回主菜单。
/// 老问题：名字用的是浅紫/浅绿/浅蓝/浅黄，压在明亮的主页面背景上几乎看不见，
/// 而且原版 5 秒后才开始出现、按任意键就返回 —— 玩家往往还没看到名字就跳过了。
/// 这里改为：名字区垫一层暗色衬板 + 黑色描边 + 更亮的用色，并把淡入提前、放慢跳过时机。
/// </summary>
public class CreditsPanel : MonoBehaviour
{
    [Header("背景 / 标题")]
    public Image backgroundImage;
    public Text titleText;

    [Header("致谢名单（按顺序：张剑老师 / 是我莉丝哒 / 褚少微老师 / Genshen）")]
    public Text[] nameTexts;

    [Header("提示")]
    public Text hintText;

    static readonly string[] Names = { "张剑老师", "是我莉丝哒", "褚少微老师", "Genshen" };

    // 提亮后的名单配色：原来是 180/150/255 这类浅色，在亮背景上几乎糊掉
    static readonly Color32[] NameColors =
    {
        new Color32(214, 176, 255, 255),   // 淡紫
        new Color32(150, 255, 214, 255),   // 薄荷绿
        new Color32(150, 216, 255, 255),   // 天蓝
        new Color32(255, 228, 150, 255),   // 金黄
    };

    const float FirstDelay = 0.7f;   // 原版 5 秒太久了
    const float PerName = 0.45f;     // 每名间隔
    const float FadeSpeed = 4f;

    float timer;

    // ---------------------------------------------------------------
    void Awake()
    {
        UITheme.Background(backgroundImage, "界面/主页面背景");
        UITheme.Font(titleText, 64);
        UITheme.Font(hintText, 20);
        if (titleText != null) { titleText.text = "特别感谢"; titleText.color = new Color32(255, 215, 0, 255); }
        if (hintText != null) { hintText.text = "按任意键返回主菜单..."; hintText.color = new Color32(120, 120, 150, 255); }

        ApplyNames();
        BuildPlate();
    }

    /// <summary>给名字区垫一层暗色衬板，保证任何亮度背景上都看得清。</summary>
    void BuildPlate()
    {
        if (nameTexts == null || nameTexts.Length == 0) return;
        if (nameTexts[0] == null) return;

        var parent = nameTexts[0].transform.parent;
        if (parent == null || parent.Find("NamePlate") != null) return;

        var go = new GameObject("NamePlate", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.55f);
        img.raycastTarget = false;

        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(560f, 280f);
        // 名字块中心：第 0 行 y=130，第 3 行 y=-50 → 中心约 40
        rt.anchoredPosition = new Vector2(0f, 40f);

        go.transform.SetSiblingIndex(1);   // 只压在背景(0)之上、名字之下
    }

    /// <summary>写入名单文本、字号、颜色，并加黑色描边（淡入前 alpha 归零）。</summary>
    void ApplyNames()
    {
        if (nameTexts == null) return;

        for (int i = 0; i < nameTexts.Length; i++)
        {
            if (nameTexts[i] == null) continue;

            UITheme.Font(nameTexts[i], 40);
            nameTexts[i].text = i < Names.Length ? Names[i] : "";
            nameTexts[i].alignment = TextAnchor.MiddleCenter;
            nameTexts[i].horizontalOverflow = HorizontalWrapMode.Overflow;
            nameTexts[i].verticalOverflow = VerticalWrapMode.Overflow;

            var c = i < NameColors.Length ? NameColors[i] : (Color32)Color.white;
            c.a = 0;
            nameTexts[i].color = c;

            // 描边：压住背景亮度，名字不再"融"进画面里
            if (nameTexts[i].GetComponent<Outline>() == null)
            {
                var outline = nameTexts[i].gameObject.AddComponent<Outline>();
                outline.effectColor = new Color32(0, 0, 0, 220);
                outline.effectDistance = new Vector2(2f, -2f);
                outline.useGraphicAlpha = true;
            }
        }
    }

    void OnEnable()
    {
        timer = 0f;
        ApplyNames();
        BuildPlate();
    }

    void Update()
    {
        timer += Time.deltaTime;

        if (nameTexts != null)
        {
            for (int i = 0; i < nameTexts.Length; i++)
            {
                if (nameTexts[i] == null) continue;
                float start = FirstDelay + i * PerName;
                if (timer < start) continue;

                float a = Mathf.Clamp01((timer - start) * FadeSpeed);
                var c = nameTexts[i].color;
                c.a = a;
                nameTexts[i].color = c;
            }
        }

        // 等名字基本都露出来了才允许按键跳过，避免"还没看清就返回"
        float allShown = FirstDelay + (NameCount - 1) * PerName + 0.8f;
        if (timer > allShown && Input.anyKeyDown)
            UIManager.Instance.ReturnFromGameOver();
    }

    int NameCount => nameTexts != null && nameTexts.Length > 0 ? nameTexts.Length : Names.Length;
}
