using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 图鉴面板：主菜单进入，两页切换。
///   敌人页 —— 5 属性杂兵 + 5 属性 BOSS，记录累计击杀数；
///   道具页 —— 4 种掉落物，记录累计拾取数。
/// 未解锁（杀过/捡过才有）的条目显示黑色剪影 + "???"。
/// 自带独立 Canvas，不依赖 UIManager 面板字段，从菜单按钮直接 Show()。
/// </summary>
public class CollectionPanel : MonoBehaviour
{
    public static CollectionPanel Instance;

    Canvas canvas;
    Text pageTitle;
    Transform grid;
    readonly List<GameObject> cells = new List<GameObject>();
    Button enemyTab, itemTab;

    void Awake()
    {
        Instance = this;
        Build();
        gameObject.SetActive(false);
    }

    public static void Show()
    {
        if (Instance == null)
        {
            var go = new GameObject("CollectionPanel");
            go.AddComponent<CollectionPanel>();
        }
        Instance.gameObject.SetActive(true);
        Instance.ShowTab(true);
    }

    void Build()
    {
        var go = new GameObject("CodexCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(transform, false);
        var cv = go.GetComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 900;
        UITheme.EnsureScaler(cv);
        canvas = cv;

        float W = GameConfig.ScreenWidth, H = GameConfig.ScreenHeight;

        // 半透明黑底（点空白处不关闭，避免误触）
        var bgGo = new GameObject("BG", typeof(RectTransform));
        bgGo.transform.SetParent(go.transform, false);
        var bg = bgGo.AddComponent<Image>();
        bg.color = new Color(0.03f, 0.03f, 0.08f, 0.92f);
        Stretch(bgGo.GetComponent<RectTransform>());

        var font = AssetLibrary.GetMainFont();

        // 标题
        pageTitle = NewText(go.transform, "Title", "图鉴", font, 44, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(400f, 60f));
        pageTitle.color = new Color32(255, 214, 90, 255);

        // 页签
        enemyTab = MakeTab("敌人图鉴", new Vector2(0.5f, 1f), new Vector2(-230f, -86f));
        itemTab = MakeTab("道具图鉴", new Vector2(0.5f, 1f), new Vector2(230f, -86f));
        enemyTab.onClick.AddListener(() => ShowTab(true));
        itemTab.onClick.AddListener(() => ShowTab(false));

        // 网格容器
        var gridGo = new GameObject("Grid", typeof(RectTransform));
        gridGo.transform.SetParent(go.transform, false);
        var grt = gridGo.GetComponent<RectTransform>();
        grt.anchorMin = new Vector2(0.5f, 0.5f);
        grt.anchorMax = new Vector2(0.5f, 0.5f);
        grt.pivot = new Vector2(0.5f, 0.5f);
        grt.anchoredPosition = new Vector2(0f, -40f);
        grt.sizeDelta = new Vector2(W - 120f, H - 260f);
        grid = gridGo.transform;

        // 返回按钮
        var back = MakeTextButton("返回", new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(220f, 52f),
            new Color32(70, 60, 110, 235));
        back.onClick.AddListener(() =>
        {
            SaveSystem.Save();
            gameObject.SetActive(false);
            if (UIManager.Instance != null) UIManager.Instance.ShowState(GameState.Menu);
        });
    }

    Button MakeTab(string label, Vector2 anchor, Vector2 pos)
    {
        var b = MakeTextButton(label, anchor, pos, new Vector2(240f, 54f), new Color32(52, 48, 86, 235));
        return b;
    }

    Button MakeTextButton(string label, Vector2 anchor, Vector2 pos, Vector2 size, Color32 bg)
    {
        var go = new GameObject("Btn_" + label, typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        var img = go.AddComponent<Image>();
        img.color = bg;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        var t = NewText(go.transform, "Label", label, AssetLibrary.GetMainFont(), 22, TextAnchor.MiddleCenter,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        t.color = new Color32(235, 235, 250, 255);
        return btn;
    }

    // ---------------------------------------------------------------
    public void ShowTab(bool enemyPage)
    {
        // 页签高亮
        SetTabColor(enemyTab, enemyPage);
        SetTabColor(itemTab, !enemyPage);

        foreach (var c in cells) if (c != null) Destroy(c);
        cells.Clear();
        pageTitle.text = enemyPage ? "敌 人 图 鉴" : "道 具 图 鉴";

        if (enemyPage) BuildEnemyPage();
        else BuildItemPage();
    }

    void SetTabColor(Button tab, bool on)
    {
        var img = tab.targetGraphic as Image;
        if (img != null) img.color = on ? new Color32(96, 84, 160, 245) : new Color32(52, 48, 86, 235);
    }

    void BuildEnemyPage()
    {
        int col = 0, row = 0;
        var elements = ElementDatabase.AllElements;

        foreach (var el in elements)
        {
            string baseName = ElementDatabase.GetName(el);
            // 杂兵
            AddCell("敌人/" + ElementDatabase.EnemyFile(el), baseName,
                baseName, row, col);
            Next(ref col, ref row, 5);
            // BOSS
            AddCell("敌人/" + baseName + "属性boss", baseName + " BOSS",
                baseName + "BOSS", row, col);
            Next(ref col, ref row, 5);
        }
    }

    void BuildItemPage()
    {
        int col = 0, row = 0;
        foreach (var info in ItemDB.All)
        {
            AddCell(info.spritePath, info.name, info.key, row, col, info);
            Next(ref col, ref row, 4);
        }
    }

    void Next(ref int col, ref int row, int perRow)
    {
        col++;
        if (col >= perRow) { col = 0; row++; }
    }

    /// <summary>放一个图鉴格子。count>0 视为已解锁，否则剪影。</summary>
    void AddCell(string spritePath, string displayName, string codexKey, int row, int col, ItemDB.Info? item = null)
    {
        int count = item.HasValue ? Codex.ItemPicked(item.Value.key) : Codex.EnemyKills(codexKey);
        bool unlocked = count > 0;

        var cell = new GameObject("Cell_" + codexKey, typeof(RectTransform));
        cell.transform.SetParent(grid, false);
        var rt = cell.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        float cellW = 300f, cellH = 230f;
        int perRow = item.HasValue ? 4 : 5;
        int cols = perRow;
        // 居中排布
        float totalW = cols * cellW;
        float x0 = -totalW / 2f + cellW / 2f;
        float y0 = 150f;
        rt.anchoredPosition = new Vector2(x0 + col * cellW, y0 - row * cellH);
        rt.sizeDelta = new Vector2(cellW - 14f, cellH - 14f);
        cells.Add(cell);

        var font = AssetLibrary.GetMainFont();

        // 卡片底
        var bg = NewImage(cell.transform, "Bg", new Color32(24, 24, 40, 235));
        Stretch(bg.rectTransform);

        // 图标
        var iconGo = new GameObject("Icon", typeof(RectTransform));
        iconGo.transform.SetParent(cell.transform, false);
        var icon = iconGo.AddComponent<Image>();
        var sp = AssetLibrary.GetSprite(spritePath);
        if (sp != null)
        {
            icon.sprite = sp;
            icon.preserveAspect = true;
        }
        icon.color = unlocked ? Color.white : new Color32(10, 10, 14, 255);   // 未解锁 = 剪影
        icon.raycastTarget = false;
        var irt = iconGo.GetComponent<RectTransform>();
        irt.anchorMin = new Vector2(0.5f, 1f);
        irt.anchorMax = new Vector2(0.5f, 1f);
        irt.pivot = new Vector2(0.5f, 1f);
        irt.anchoredPosition = new Vector2(0f, -12f);
        irt.sizeDelta = new Vector2(120f, 120f);

        // 名字
        var nameT = NewText(cell.transform, "Name", unlocked ? displayName : "？？？",
            font, 20, TextAnchor.MiddleCenter, new Vector2(0f, 0f), new Vector2(0.5f, 1f),
            new Vector2(0f, -140f), new Vector2(cellW - 30f, 26f));
        nameT.color = unlocked ? new Color32(240, 240, 250, 255) : new Color32(120, 120, 135, 255);

        // 计数
        string countStr = unlocked ? (item.HasValue ? "已拾取 × " : "已击杀 × ") + count : "尚未遇见";
        var countT = NewText(cell.transform, "Count", countStr,
            font, 16, TextAnchor.MiddleCenter, new Vector2(0f, 0f), new Vector2(0.5f, 1f),
            new Vector2(0f, -168f), new Vector2(cellW - 30f, 22f));
        countT.color = unlocked ? new Color32(150, 220, 160, 255) : new Color32(95, 95, 110, 255);

        // 道具页附加说明
        if (item.HasValue && unlocked)
        {
            var descT = NewText(cell.transform, "Desc", item.Value.desc,
                font, 15, TextAnchor.MiddleCenter, new Vector2(0f, 0f), new Vector2(0.5f, 1f),
                new Vector2(0f, -192f), new Vector2(cellW - 30f, 20f));
            descT.color = new Color32(180, 180, 210, 255);
        }
    }

    // ---------------------------------------------------------------
    void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    Image NewImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    Text NewText(Transform parent, string name, string txt, Font f, int fontSize, TextAnchor align,
                 Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.text = txt;
        t.font = f;
        t.fontSize = fontSize;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return t;
    }
}
