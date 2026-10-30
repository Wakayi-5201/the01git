using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 关卡选择面板：动态生成 4 关 + 无尽模式的卡片。
/// 未解锁的卡片灰色 + 锁，点击已解锁卡片调用 UIManager.StartLevel。
/// </summary>
public class LevelSelectPanel : MonoBehaviour
{
    Canvas canvas;
    Transform grid;
    Text progressText;

    void Awake()
    {
        // 只负责生成 UI；显隐统一由 UIManager 控制
        BuildUI();
    }

    void BuildUI()
    {
        if (canvas != null) return;   // 避免重复构建

        var go = new GameObject("LevelSelectCanvas");
        go.transform.SetParent(transform, false);
        canvas = go.AddComponent<Canvas>();
        canvas.sortingOrder = 200;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        UITheme.EnsureScaler(canvas);
        go.AddComponent<GraphicRaycaster>().ignoreReversedGraphics = true;

        var font = AssetLibrary.GetMainFont();

        // 背景遮罩
        var bg = new GameObject("BG", typeof(RectTransform));
        bg.transform.SetParent(go.transform, false);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.7f);
        bgImg.raycastTarget = true;
        var bgrt = bg.GetComponent<RectTransform>();
        bgrt.anchorMin = Vector2.zero;
        bgrt.anchorMax = Vector2.one;
        bgrt.offsetMin = bgrt.offsetMax = Vector2.zero;

        // 标题
        var title = MakeText(go.transform, "Title", "选择关卡", font, 48, TextAnchor.MiddleCenter);
        title.color = GameConfig.Yellow;
        title.rectTransform.anchorMin = new Vector2(0.5f, 1);
        title.rectTransform.anchorMax = new Vector2(0.5f, 1);
        title.rectTransform.pivot = new Vector2(0.5f, 1);
        title.rectTransform.anchoredPosition = new Vector2(0, -60);
        title.rectTransform.sizeDelta = new Vector2(500, 70);

        // 进度说明（已通关几关 / 无尽模式是否解锁）
        progressText = MakeText(go.transform, "Progress", "", font, 22, TextAnchor.MiddleCenter);
        progressText.color = GameConfig.Cyan;
        progressText.rectTransform.anchorMin = new Vector2(0.5f, 1);
        progressText.rectTransform.anchorMax = new Vector2(0.5f, 1);
        progressText.rectTransform.pivot = new Vector2(0.5f, 1);
        progressText.rectTransform.anchoredPosition = new Vector2(0, -128);
        progressText.rectTransform.sizeDelta = new Vector2(700, 30);

        // 卡片容器（2 列 3 行）
        grid = new GameObject("Grid", typeof(RectTransform)).transform;
        grid.SetParent(go.transform, false);
        var grt = grid.GetComponent<RectTransform>();
        grt.anchorMin = new Vector2(0.5f, 0.5f);
        grt.anchorMax = new Vector2(0.5f, 0.5f);
        grt.pivot = new Vector2(0.5f, 0.5f);
        grt.anchoredPosition = new Vector2(0, 10);
        grt.sizeDelta = new Vector2(700, 500);

        // 返回按钮
        var backBtn = MakeButton(go.transform, "返回", font, new Vector2(0, -362), new Vector2(160, 50));
        backBtn.onClick.AddListener(() => UIManager.Instance.ReturnFromLevelSelect());
    }

    public void RefreshUI()
    {
        BuildUI();   // 兜底：万一 Awake 未执行（对象被外部激活）也能正常生成
        if (grid == null) return;

        // 清空旧卡片
        for (int i = grid.childCount - 1; i >= 0; i--) Destroy(grid.GetChild(i).gameObject);

        var font = AssetLibrary.GetMainFont();
        var ids = new[]
        {
            LevelConfig.Id.Level1, LevelConfig.Id.Level2,
            LevelConfig.Id.Level3, LevelConfig.Id.Level4,
            LevelConfig.Id.Endless,
        };

        float cardW = 300, cardH = 140, gapX = 40, gapY = 26;
        for (int i = 0; i < ids.Length; i++)
        {
            int col = i % 2;
            int row = i / 2;
            float x = (col - 0.5f) * (cardW + gapX);
            float y = (0.5f - row) * (cardH + gapY);
            MakeCard(grid, ids[i], new Vector2(x, y), new Vector2(cardW, cardH), font);
        }

        UpdateProgressText();
    }

    void UpdateProgressText()
    {
        if (progressText == null) return;

        int cleared = 0;
        for (int i = 0; i < LevelConfig.RegularLevelCount; i++)
            if (SaveSystem.IsLevelCleared((LevelConfig.Id)i)) cleared++;

        string endless = SaveSystem.IsLevelUnlocked(LevelConfig.Id.Endless)
            ? "无尽模式：已解锁"
            : "无尽模式：未解锁（通关四关后开启）";

        progressText.text = "已通关 " + cleared + " / " + LevelConfig.RegularLevelCount + " 关    " + endless;
    }

    void MakeCard(Transform parent, LevelConfig.Id id, Vector2 pos, Vector2 size, Font font)
    {
        bool unlocked = SaveSystem.IsLevelUnlocked(id);
        bool cleared = SaveSystem.IsLevelCleared(id);
        var data = LevelConfig.Get(id);

        string statusMsg, hint;
        if (unlocked)
        {
            statusMsg = cleared ? "✓ 已通关" : "可挑战";
            hint = id == LevelConfig.Id.Endless ? "无限波次 · 越高越强" : "BOSS：" + ElementDatabase.GetName(data.bossElement) + "属性";
        }
        else if (id == LevelConfig.Id.Endless)
        {
            statusMsg = "[锁定]";
            hint = "通关全部四关后解锁";
        }
        else
        {
            statusMsg = "[锁定]";
            hint = "通关第 " + (int)id + " 关后解锁";
        }

        var card = new GameObject("Card_" + id, typeof(RectTransform));
        card.transform.SetParent(parent, false);
        var rt = card.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        var img = card.AddComponent<Image>();
        img.color = unlocked
            ? (cleared ? new Color(0.2f, 0.35f, 0.2f, 0.95f) : new Color(0.15f, 0.15f, 0.25f, 0.95f))
            : new Color(0.1f, 0.1f, 0.1f, 0.8f);
        img.raycastTarget = true;

        // 名称带：卡片上部 84~132（140 高的卡片）
        var name = MakeText(card.transform, "Name", data.displayName, font, 26, TextAnchor.MiddleCenter);
        name.color = unlocked ? Color.white : new Color(0.5f, 0.5f, 0.5f, 1f);
        name.rectTransform.anchorMin = Vector2.zero;
        name.rectTransform.anchorMax = Vector2.one;
        name.rectTransform.offsetMin = new Vector2(8, 84);
        name.rectTransform.offsetMax = new Vector2(-8, -8);

        // 状态带：中部 46~76
        var status = MakeText(card.transform, "Status", statusMsg, font, 17, TextAnchor.MiddleCenter);
        status.color = unlocked ? (cleared ? GameConfig.Green : GameConfig.Yellow) : GameConfig.Gray;
        status.rectTransform.anchorMin = Vector2.zero;
        status.rectTransform.anchorMax = Vector2.one;
        status.rectTransform.offsetMin = new Vector2(8, 46);
        status.rectTransform.offsetMax = new Vector2(-8, -64);

        // 提示带：下部 10~42
        var hintText = MakeText(card.transform, "Hint", hint, font, 14, TextAnchor.MiddleCenter);
        hintText.color = unlocked ? GameConfig.Cyan : new Color32(150, 130, 130, 255);
        hintText.rectTransform.anchorMin = Vector2.zero;
        hintText.rectTransform.anchorMax = Vector2.one;
        hintText.rectTransform.offsetMin = new Vector2(8, 10);
        hintText.rectTransform.offsetMax = new Vector2(-8, -98);

        if (unlocked)
        {
            var btn = card.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => UIManager.Instance.StartLevel(id));
        }
    }

    Text MakeText(Transform parent, string name, string txt, Font f, int size, TextAnchor align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.text = txt;
        t.font = f;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        return t;
    }

    Button MakeButton(Transform parent, string label, Font font, Vector2 pos, Vector2 size)
    {
        var go = new GameObject("Btn", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0);
        rt.anchorMax = new Vector2(0.5f, 0);
        rt.pivot = new Vector2(0.5f, 0);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.color = new Color(0.2f, 0.2f, 0.3f, 0.95f);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var txt = MakeText(go.transform, "Label", label, font, 20, TextAnchor.MiddleCenter);
        txt.rectTransform.anchorMin = Vector2.zero;
        txt.rectTransform.anchorMax = Vector2.one;
        txt.rectTransform.offsetMin = txt.rectTransform.offsetMax = Vector2.zero;
        return btn;
    }
}
