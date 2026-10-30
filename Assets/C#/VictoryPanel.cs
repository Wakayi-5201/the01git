using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 关卡胜利结算面板：显示得分、用时、解锁的下一关/属性，提供"下一关 / 返回选关 / 返回菜单"。
/// 动态生成 UI，无需手动摆。
///
/// 老 bug：三个按钮都用「屏幕中心 + anchoredPosition(±110, -400)」摆放，
/// 画布只有 768（16:9 下实际约 665）高，-400 已经掉到可视区外面，
/// 于是通关后只看得到两行字、点不到任何按钮 —— 就“卡”住了。
/// 现在改成：一块居中的结算面板（锚点定位，任何分辨率都在屏内）+ 下一关倒计时自动开始。
/// </summary>
public class VictoryPanel : MonoBehaviour
{
    [Header("下一关倒计时（秒），<=0 表示不倒计时")]
    public float autoNextSeconds = 6f;

    const float PanelW = 760f;
    const float PanelH = 520f;
    const float BtnW = 210f;
    const float BtnH = 62f;

    Canvas canvas;
    Image panelBg;
    Text scoreText, timeText, unlockText, growthText;
    Button nextBtn, selectBtn, menuBtn;
    Text nextLabel;

    float timer;
    bool counting;
    LevelConfig.Id nextId = LevelConfig.Id.Endless;
    bool hasNext;

    void Awake()
    {
        // 只负责生成 UI；显隐统一由 UIManager 控制
        BuildUI();
    }

    // ---------------------------------------------------------------
    void BuildUI()
    {
        var go = new GameObject("VictoryCanvas");
        go.transform.SetParent(transform, false);
        canvas = go.AddComponent<Canvas>();
        canvas.sortingOrder = 300;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        UITheme.EnsureScaler(canvas);
        go.AddComponent<GraphicRaycaster>().ignoreReversedGraphics = true;

        var font = AssetLibrary.GetMainFont();

        // 全屏压暗
        var bg = new GameObject("BG", typeof(RectTransform));
        bg.transform.SetParent(go.transform, false);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.72f);
        bgImg.raycastTarget = true;                  // 吃掉底下的点击，避免误触战斗界面
        var bgrt = bg.GetComponent<RectTransform>();
        bgrt.anchorMin = Vector2.zero;
        bgrt.anchorMax = Vector2.one;
        bgrt.offsetMin = bgrt.offsetMax = Vector2.zero;

        // 居中结算面板（锚点居中 → 永远在屏内）
        var panel = NewRect(go.transform, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(PanelW, PanelH));
        panelBg = panel.gameObject.AddComponent<Image>();
        panelBg.color = new Color(0.07f, 0.08f, 0.12f, 0.96f);
        panelBg.raycastTarget = true;

        // 顶部高亮条
        var accent = NewRect(panel, "Accent", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -2f), new Vector2(PanelW, 5f));
        accent.gameObject.AddComponent<Image>().color = GameConfig.Yellow;
        accent.GetComponent<Image>().raycastTarget = false;

        var title = MakeText(panel, "Title", "关卡通关!", font, 52, TextAnchor.MiddleCenter);
        title.color = GameConfig.Yellow;
        SetTop(title.rectTransform, -40f, 640f, 66f);

        // 分隔线
        var line = NewRect(panel, "Line", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -118f), new Vector2(600f, 2f));
        var lineImg = line.gameObject.AddComponent<Image>();
        lineImg.color = new Color(1f, 1f, 1f, 0.18f);
        lineImg.raycastTarget = false;

        scoreText = MakeText(panel, "Score", "", font, 34, TextAnchor.MiddleCenter);
        scoreText.color = Color.white;
        SetTop(scoreText.rectTransform, -142f, 700f, 46f);

        timeText = MakeText(panel, "Time", "", font, 24, TextAnchor.MiddleCenter);
        timeText.color = new Color32(190, 210, 235, 255);
        SetTop(timeText.rectTransform, -196f, 700f, 34f);

        unlockText = MakeText(panel, "Unlock", "", font, 22, TextAnchor.MiddleCenter);
        unlockText.color = GameConfig.Green;
        SetTop(unlockText.rectTransform, -240f, 700f, 34f);

        // 永久成长回执：通关时已写回存档，这里把带走的属性直接摊开给玩家看
        growthText = MakeText(panel, "Growth", "", font, 17, TextAnchor.MiddleCenter);
        growthText.color = new Color32(255, 226, 150, 255);
        SetTop(growthText.rectTransform, -282f, 700f, 26f);

        // 下一关提示
        var nextHint = MakeText(panel, "NextHint", "", font, 16, TextAnchor.MiddleCenter);
        nextHint.color = new Color32(140, 150, 175, 255);
        SetTop(nextHint.rectTransform, -330f, 700f, 26f);
        nextHint.name = "NextHint";
        nextHint.text = "回车/空格 = 立即下一关    ESC = 返回选关";

        // ---- 按钮行：贴在面板底部（锚点定位，不会跑到屏幕外） ----
        nextBtn = MakeButton(panel, "下一关", font, new Vector2(-230f, 60f), new Vector2(BtnW, BtnH),
            new Color32(40, 130, 70, 255));
        nextLabel = nextBtn.GetComponentInChildren<Text>();
        nextBtn.onClick.AddListener(GoNextLevel);

        selectBtn = MakeButton(panel, "返回选关", font, new Vector2(0f, 60f), new Vector2(BtnW, BtnH),
            new Color32(45, 60, 95, 255));
        selectBtn.onClick.AddListener(() => UIManager.Instance.ReturnFromVictoryToLevelSelect());

        menuBtn = MakeButton(panel, "返回菜单", font, new Vector2(230f, 60f), new Vector2(BtnW, BtnH),
            new Color32(60, 50, 80, 255));
        menuBtn.onClick.AddListener(() => UIManager.Instance.ReturnFromVictory());
    }

    // ---------------------------------------------------------------
    public void RefreshUI()
    {
        var gm = GameManager.Instance;
        if (gm != null)
        {
            scoreText.text = "得分: " + gm.score.ToString("N0");
            timeText.text = "用时: " + Mathf.FloorToInt(gm.gameTime / 60f) + " 秒";
        }

        var lm = LevelManager.Instance;
        var cur = lm != null ? lm.currentLevelId : LevelConfig.Id.Level1;

        // 下一关是否存在（Level4 之后没有普通关；无尽模式不自动进入下一关）
        hasNext = cur != LevelConfig.Id.Level4 && cur != LevelConfig.Id.Endless;
        nextId = hasNext ? (LevelConfig.Id)((int)cur + 1) : LevelConfig.Id.Endless;

        string msg = "";
        if (hasNext)
        {
            msg = nextId == LevelConfig.Id.Endless
                ? "已解锁无尽模式!"
                : "解锁: " + LevelConfig.Get(nextId).displayName;
        }
        else if (cur == LevelConfig.Id.Endless)
        {
            msg = "无尽模式没有尽头，随时可以再来一局";
        }
        else
        {
            msg = "四关全部通关，无尽模式已解锁!";
        }
        unlockText.text = msg;

        // 成长回执：区分「首次通关 = 已永久保存」与「重玩 = 只记分数」
        if (growthText != null)
        {
            if (cur == LevelConfig.Id.Endless)
            {
                growthText.text = "无尽模式不保存成长，下一局仍从上次通关的存档开始";
                growthText.color = new Color32(150, 158, 178, 255);
            }
            else if (lm != null && !lm.lastVictorySavedGrowth)
            {
                growthText.text = "重玩已通关的关卡只记分数，成长不再累积";
                growthText.color = new Color32(160, 168, 190, 255);
            }
            else
            {
                var g = SaveSystem.Data.growth;
                growthText.text = "已带走:  HP " + g.maxHp + " / 攻击 " + g.damage
                    + " / 弹道 " + g.bulletRows + " / 攻速 Lv." + g.attackSpeedLevel
                    + " / 移速 " + g.moveSpeed.ToString("0.0");
                growthText.color = new Color32(255, 226, 150, 255);
            }
        }

        // 按钮布局：有下一关 = 三按钮；没下一关 = 两个按钮居中
        nextBtn.gameObject.SetActive(hasNext);
        selectBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(hasNext ? 0f : -115f, 60f);
        menuBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(hasNext ? 230f : 115f, 60f);

        // 倒计时：有下一关才自动走
        timer = hasNext ? Mathf.Max(0f, autoNextSeconds) : 0f;
        counting = hasNext && timer > 0f;
        UpdateNextLabel();
    }

    void Update()
    {
        if (!counting) return;

        // 结算时 timeScale 已恢复正常，但仍用 unscaled 保证万无一失
        timer -= Time.unscaledDeltaTime;
        UpdateNextLabel();

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
            Input.GetKeyDown(KeyCode.Space))
        {
            GoNextLevel();
            return;
        }
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            counting = false;
            UIManager.Instance.ReturnFromVictoryToLevelSelect();
            return;
        }

        if (timer <= 0f)
        {
            counting = false;
            GoNextLevel();
        }
    }

    void UpdateNextLabel()
    {
        if (nextLabel == null) return;
        nextLabel.text = counting
            ? "下一关 (" + Mathf.CeilToInt(Mathf.Max(0f, timer)) + "s)"
            : "下一关";
    }

    void GoNextLevel()
    {
        counting = false;
        if (!hasNext) { UIManager.Instance.ReturnFromVictoryToLevelSelect(); return; }
        UIManager.Instance.StartLevel(nextId);
    }

    void OnDisable() => counting = false;

    // ---------------------------------------------------------------
    // 构造小工具
    void SetTop(RectTransform rt, float y, float w, float h)
    {
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    RectTransform NewRect(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
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
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        return t;
    }

    Button MakeButton(Transform parent, string label, Font font, Vector2 pos, Vector2 size, Color bgColor)
    {
        var go = new GameObject("Btn_" + label, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        // 锚在面板底部，向上偏移 → 无论窗口多矮都在面板里
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        var img = go.AddComponent<Image>();
        img.color = bgColor;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.ColorTint;
        var cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color32(255, 240, 190, 255);
        cb.pressedColor = new Color32(190, 180, 150, 255);
        cb.selectedColor = Color.white;
        btn.colors = cb;

        var txt = MakeText(go.transform, "Label", label, font, 24, TextAnchor.MiddleCenter);
        txt.rectTransform.anchorMin = Vector2.zero;
        txt.rectTransform.anchorMax = Vector2.one;
        txt.rectTransform.offsetMin = txt.rectTransform.offsetMax = Vector2.zero;
        return btn;
    }
}
