using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 编辑器工具：一键在 Canvas 下生成开场界面全部面板（登录/菜单/排行榜/规则/结算/致谢），
/// 并把各脚本的字段自动连好。生成后仍可在场景里自由拖动、改样式。
/// 用法：菜单栏 → 工具 → 生成开场界面
/// </summary>
public static class UILayoutBuilder
{
    const string UndoName = "生成开场界面";

    // =====================================================================
    // 资源
    // =====================================================================
    static Font _font;
    static Font MainFont
    {
        get
        {
            if (_font == null) _font = Resources.Load<Font>("Fonts/SourceHanSansCN-Regular");
            if (_font == null) _font = Resources.Load<Font>("Fonts/乐米像素体");
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return _font;
        }
    }

    static Sprite LoadSprite(string path) => Resources.Load<Sprite>("Sprites/" + path);

    // =====================================================================
    // 入口
    // =====================================================================
    [MenuItem("工具/生成开场界面")]
    public static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("生成开场界面",
                "播放模式下不能生成界面。\n\n请先退出播放模式（停止运行），再使用本工具。", "好");
            return;
        }

        var canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("生成开场界面",
                "场景里没有 Canvas。\n\n请先：游戏对象 → UI → 画布，然后再运行本工具。", "好");
            return;
        }

        if (!EditorUtility.DisplayDialog("生成开场界面",
            "将在场景中生成 8 个面板（登录 / 菜单 / 选关 / 排行榜 / 规则 / 结算 / 胜利 / 致谢），\n" +
            "同名旧面板会被替换，各脚本字段会自动连好。\n\n继续吗？", "继续", "取消"))
            return;

        // ---- 统一画布设置 ----
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1024f, 768f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (canvas.GetComponent<GraphicRaycaster>() == null)
            canvas.gameObject.AddComponent<GraphicRaycaster>();

        if (Object.FindObjectOfType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(es, UndoName);
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UndoName);

        // ---- 清理旧面板 ----
        string[] names = { "LoginPanel", "MenuPanel", "RankingPanel", "RulesPanel", "GameOverPanel", "CreditsPanel" };
        foreach (var n in names)
        {
            var old = canvas.transform.Find(n);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        }

        // ---- 逐个生成 ----
        var login = BuildLogin(canvas.transform);
        var menu = BuildMenu(canvas.transform);
        var ranking = BuildRanking(canvas.transform);
        var rules = BuildRules(canvas.transform);
        var gameOver = BuildGameOver(canvas.transform);
        var credits = BuildCredits(canvas.transform);

        // 选关 / 胜利结算面板的 UI 由各自脚本 Awake 动态生成，这里只确保场景里有对象
        var levelSelect = EnsurePanel<LevelSelectPanel>("LevelSelectPanel");
        var victory = EnsurePanel<VictoryPanel>("VictoryPanel");

        // ---- UIManager ----
        var ui = Object.FindObjectOfType<UIManager>();
        if (ui == null)
        {
            var go = new GameObject("UIManager");
            Undo.RegisterCreatedObjectUndo(go, UndoName);
            ui = go.AddComponent<UIManager>();
        }
        ui.loginPanel = login.gameObject;
        ui.menuPanel = menu.gameObject;
        ui.rankingPanel = ranking.gameObject;
        ui.rulesPanel = rules.gameObject;
        ui.gameOverPanel = gameOver.gameObject;
        ui.creditsPanel = credits.gameObject;
        ui.levelSelectPanel = levelSelect;
        ui.victoryPanel = victory;
        ui.startState = GameState.Login;

        // 场景里只显示登录面板，其余隐藏（运行时由 UIManager 控制）
        menu.gameObject.SetActive(false);
        ranking.gameObject.SetActive(false);
        rules.gameObject.SetActive(false);
        gameOver.gameObject.SetActive(false);
        credits.gameObject.SetActive(false);
        login.gameObject.SetActive(true);

        // ---- GameManager 检查 ----
        var gm = Object.FindObjectOfType<GameManager>();
        if (gm == null)
            Debug.LogWarning("[UILayoutBuilder] 场景里没有 GameManager，开始游戏时会报错。");
        else if (gm.player == null)
            Debug.LogWarning("[UILayoutBuilder] GameManager 的 player 字段还没拖入，请在检查器里拖 Player。");

        EditorUtility.SetDirty(canvas);
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Undo.CollapseUndoOperations(group);
        Selection.activeGameObject = login.gameObject;

        Debug.Log("[UILayoutBuilder] 开场界面已生成。按 Play 验收：登录 → 菜单选角色 → 开始游戏。");
    }

    // =====================================================================
    // 六个面板
    // =====================================================================
    static LoginPanel BuildLogin(Transform canvas)
    {
        var root = NewPanel(canvas, "LoginPanel");
        var rt = root.transform;

        var bg = FullBackground(rt, "界面/主页面背景");
        var title = MakeText(rt, "Title", "元素战机", 64, new Vector2(0f, 250f), new Vector2(700f, 90f),
            TextAnchor.MiddleCenter, new Color32(255, 220, 50, 255));
        var subtitle = MakeText(rt, "Subtitle", "ELEMENT FIGHTER", 26, new Vector2(0f, 185f), new Vector2(700f, 36f),
            TextAnchor.MiddleCenter, new Color32(100, 200, 255, 255));
        var welcome = MakeText(rt, "Welcome", "请输入您的名字", 30, new Vector2(0f, 120f), new Vector2(600f, 40f));
        var input = MakeInputField(rt, "NameInput", new Vector2(0f, 55f), new Vector2(520f, 60f));
        var switchHint = MakeText(rt, "SwitchHint", "已有存档时可直接点下面的按钮继续", 18,
            new Vector2(0f, 8f), new Vector2(600f, 26f), TextAnchor.MiddleCenter, new Color32(150, 150, 150, 255));

        Text quickLabel, loginLabel, rankLabel;
        var quick = MakeButton(rt, "QuickLogin", "继续使用", 26, new Vector2(0f, -55f), new Vector2(460f, 60f),
            out quickLabel, "界面/按钮条");
        var loginBtn = MakeButton(rt, "Login", "进入游戏", 26, new Vector2(0f, -130f), new Vector2(320f, 70f),
            out loginLabel, "界面/按钮条", new Color32(50, 200, 50, 255));
        var rankingBtn = MakeButton(rt, "Ranking", "排行榜", 24, new Vector2(0f, -205f), new Vector2(200f, 50f),
            out rankLabel, null, new Color32(60, 90, 140, 255));

        var message = MakeText(rt, "Message", "", 20, new Vector2(0f, -265f), new Vector2(600f, 30f));
        var history = MakeText(rt, "History", "", 18, new Vector2(0f, -305f), new Vector2(700f, 26f),
            TextAnchor.MiddleCenter, new Color32(160, 160, 180, 255));
        var version = MakeText(rt, "Version", "Version 1.0 | Made with Unity", 14,
            new Vector2(-20f, 20f), new Vector2(400f, 24f), TextAnchor.LowerRight, new Color32(60, 60, 80, 255),
            new Vector2(1f, 0f), new Vector2(1f, 0f));

        var p = root.AddComponent<LoginPanel>();
        p.backgroundImage = bg;
        p.titleText = title;
        p.subtitleText = subtitle;
        p.welcomeText = welcome;
        p.nameInput = input;
        p.switchHintText = switchHint;
        p.quickLoginButton = quick;
        p.quickLoginLabel = quickLabel;
        p.loginButton = loginBtn;
        p.loginLabel = loginLabel;
        p.rankingButton = rankingBtn;
        p.messageText = message;
        p.historyText = history;
        p.versionText = version;
        return p;
    }

    static MenuPanel BuildMenu(Transform canvas)
    {
        var root = NewPanel(canvas, "MenuPanel");
        var rt = root.transform;

        var bg = FullBackground(rt, "界面/主页面背景");
        var titleImage = MakeImage(rt, "TitleImage", new Vector2(0f, 255f), new Vector2(600f, 120f),
            Color.white, "界面/元素战机", true);
        var title = MakeText(rt, "Title", "", 64, new Vector2(0f, 255f), new Vector2(600f, 90f),
            TextAnchor.MiddleCenter, new Color32(255, 220, 50, 255));
        var playerText = MakeText(rt, "PlayerText", "", 24, new Vector2(30f, -30f), new Vector2(400f, 36f),
            TextAnchor.MiddleLeft, new Color32(60, 200, 255, 255), new Vector2(0f, 1f), new Vector2(0f, 1f));
        var highScoreText = MakeText(rt, "HighScoreText", "", 24, new Vector2(-30f, -30f), new Vector2(400f, 36f),
            TextAnchor.MiddleRight, new Color32(255, 220, 50, 255), new Vector2(1f, 1f), new Vector2(1f, 1f));
        var avatarRoot = Place("Avatars", rt, new Vector2(0f, 40f), new Vector2(500f, 90f));
        var avatars = new ElementAvatar[5];
        for (int i = 0; i < avatars.Length; i++)
        {
            var el = ElementDatabase.AllElements[i];
            var itemRT = Place("Avatar_" + ElementDatabase.GetName(el), avatarRoot, new Vector2(-200f + i * 100f, 0f),
                new Vector2(72f, 72f));

            var itemImg = itemRT.gameObject.AddComponent<Image>();
            itemImg.color = new Color(1f, 1f, 1f, 0.08f);
            var btn = itemRT.gameObject.AddComponent<Button>();
            btn.targetGraphic = itemImg;

            // Highlight 先建，渲染在头像背后
            var hlRT = Place("Highlight", itemRT, Vector2.zero, new Vector2(84f, 84f));
            var hl = hlRT.gameObject.AddComponent<Image>();
            hl.color = new Color32(255, 215, 0, 200);
            hl.raycastTarget = false;
            hlRT.gameObject.SetActive(false);

            var iconRT = Place("Icon", itemRT, Vector2.zero, new Vector2(56f, 56f));
            var icon = iconRT.gameObject.AddComponent<Image>();
            icon.raycastTarget = false;
            var sp = LoadSprite("头像/" + ElementDatabase.AvatarFile(el));
            if (sp != null) { icon.sprite = sp; icon.type = Image.Type.Simple; }

            var nameText = MakeText(itemRT, "Name", ElementDatabase.GetName(el), 16,
                new Vector2(0f, -52f), new Vector2(100f, 26f), TextAnchor.MiddleCenter,
                new Color32(180, 180, 180, 255));

            avatars[i] = new ElementAvatar
            {
                button = btn,
                image = icon,
                highlight = hlRT.gameObject,
                nameText = nameText,
            };
        }

        Text startLabel, switchLabel;
        var startBtn = MakeButton(rt, "StartBtn", "开始游戏", 30, new Vector2(0f, -110f), new Vector2(320f, 70f),
            out startLabel, "界面/按钮条");
        // 规则入口：保留“游戏规则”精灵图作为可见按钮，不再叠加白色文字标签
        var rulesBtn = MakeButton(rt, "RulesBtn", "", 30, new Vector2(0f, -195f), new Vector2(200f, 70f),
            out _, "界面/游戏规则", Color.white, null, null, true);
        // 呼吸微光（由 MenuPanel.Update 驱动），只显示、不拦截点击
        var glowRT = Rect("Glow", rulesBtn.transform);
        glowRT.anchorMin = Vector2.zero;
        glowRT.anchorMax = Vector2.one;
        glowRT.offsetMin = Vector2.zero;
        glowRT.offsetMax = Vector2.zero;
        var rulesGlow = glowRT.gameObject.AddComponent<Image>();
        rulesGlow.color = new Color(1f, 1f, 1f, 0f);
        rulesGlow.raycastTarget = false;
        var switchBtn = MakeButton(rt, "SwitchBtn", "切换玩家", 22, new Vector2(0f, -265f), new Vector2(160f, 50f),
            out switchLabel, null, new Color32(70, 80, 110, 255));

        var p = root.AddComponent<MenuPanel>();
        p.backgroundImage = bg;
        p.titleImage = titleImage;
        p.titleText = title;
        p.playerText = playerText;
        p.highScoreText = highScoreText;
        p.avatars = avatars;
        p.startButton = startBtn;
        p.startLabel = startLabel;
        p.rulesButton = rulesBtn;
        p.rulesGlow = rulesGlow;
        p.switchButton = switchBtn;
        p.switchLabel = switchLabel;
        return p;
    }

    static RankingPanel BuildRanking(Transform canvas)
    {
        var root = NewPanel(canvas, "RankingPanel");
        var rt = root.transform;

        var bg = FullBackground(rt, "界面/主页面背景");
        var title = MakeText(rt, "Title", "排行榜", 64, new Vector2(0f, 300f), new Vector2(600f, 90f),
            TextAnchor.MiddleCenter, new Color32(255, 220, 50, 255));
        var empty = MakeText(rt, "EmptyText", "暂无排行数据", 30, new Vector2(0f, 0f), new Vector2(400f, 50f),
            TextAnchor.MiddleCenter, new Color32(150, 150, 150, 255));

        var container = Place("RowContainer", rt, new Vector2(0f, -130f), new Vector2(600f, 540f),
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

        // ---- 行预制体（建完隐藏） ----
        var rowRT = Place("RowPrefab", rt, new Vector2(0f, -130f), new Vector2(560f, 45f),
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
        var rowBg = FullImage(rowRT, "BG", new Color32(40, 40, 60, 180), null, false);
        var rankText = MakeText(rowRT, "RankText", "#1", 22, new Vector2(12f, 0f), new Vector2(80f, 45f),
            TextAnchor.MiddleLeft, Color.white, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
        var nameText = MakeText(rowRT, "NameText", "玩家", 22, new Vector2(100f, 0f), new Vector2(260f, 45f),
            TextAnchor.MiddleLeft, Color.white, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
        var scoreText = MakeText(rowRT, "ScoreText", "0", 22, new Vector2(-12f, 0f), new Vector2(200f, 45f),
            TextAnchor.MiddleRight, new Color32(255, 220, 50, 255), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));

        var row = rowRT.gameObject.AddComponent<RankingRow>();
        row.background = rowBg;
        row.rankText = rankText;
        row.nameText = nameText;
        row.scoreText = scoreText;
        rowRT.gameObject.SetActive(false);

        // 返回按钮：只显示图标（原图是正方形，别再叠"返回"两个字，也别压成长方形）
        var back = MakeButton(rt, "BackBtn", "", 24, new Vector2(28f, 24f), new Vector2(64f, 64f),
            out _, "界面/返回", null, new Vector2(0f, 0f), new Vector2(0f, 0f), true);

        var p = root.AddComponent<RankingPanel>();
        p.backgroundImage = bg;
        p.titleText = title;
        p.emptyText = empty;
        p.rowContainer = container;
        p.rowPrefab = row;
        p.backButton = back;
        return p;
    }

    static RulesPanel BuildRules(Transform canvas)
    {
        var root = NewPanel(canvas, "RulesPanel");
        var rt = root.transform;

        var bg = FullBackground(rt, "界面/主页面背景");
        var title = MakeText(rt, "Title", "游戏规则", 40, new Vector2(0f, 300f), new Vector2(600f, 70f),
            TextAnchor.MiddleCenter, new Color32(255, 220, 50, 255));

        // ---- ScrollView ----
        var scrollRT = Place("ScrollView", rt, new Vector2(0f, -30f), new Vector2(900f, 540f));
        var scrollBg = scrollRT.gameObject.AddComponent<Image>();
        scrollBg.color = new Color(0f, 0f, 0f, 0.35f);
        var scroll = scrollRT.gameObject.AddComponent<ScrollRect>();

        var viewportRT = Rect("Viewport", scrollRT);
        viewportRT.anchorMin = Vector2.zero;
        viewportRT.anchorMax = Vector2.one;
        viewportRT.offsetMin = Vector2.zero;
        viewportRT.offsetMax = Vector2.zero;
        var vpImg = viewportRT.gameObject.AddComponent<Image>();
        vpImg.color = new Color(1f, 1f, 1f, 0.02f);
        viewportRT.gameObject.AddComponent<RectMask2D>();

        var contentRT = Rect("Content", viewportRT);
        contentRT.anchorMin = new Vector2(0.5f, 1f);
        contentRT.anchorMax = new Vector2(0.5f, 1f);
        contentRT.pivot = new Vector2(0.5f, 1f);
        contentRT.sizeDelta = new Vector2(880f, 100f);
        contentRT.anchoredPosition = new Vector2(0f, -10f);

        var fitter = contentRT.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var contentText = contentRT.gameObject.AddComponent<Text>();
        contentText.font = MainFont;
        contentText.fontSize = 20;
        contentText.text = RulesPanel.RulesText;
        contentText.color = Color.white;
        contentText.alignment = TextAnchor.UpperLeft;
        contentText.horizontalOverflow = HorizontalWrapMode.Wrap;
        contentText.verticalOverflow = VerticalWrapMode.Overflow;
        contentText.raycastTarget = false;

        scroll.content = contentRT;
        scroll.viewport = viewportRT;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;

        // 返回按钮：只显示图标（原图是 850×850 正方形，压成 120×50 会变椭圆）
        var back = MakeButton(rt, "BackBtn", "", 24, new Vector2(28f, 24f), new Vector2(64f, 64f),
            out _, "界面/返回", null, new Vector2(0f, 0f), new Vector2(0f, 0f), true);

        var p = root.AddComponent<RulesPanel>();
        p.backgroundImage = bg;
        p.titleText = title;
        p.scrollRect = scroll;
        p.contentText = contentText;
        p.backButton = back;
        return p;
    }

    static GameOverPanel BuildGameOver(Transform canvas)
    {
        var root = NewPanel(canvas, "GameOverPanel");
        var rt = root.transform;

        var bg = FullBackground(rt, "界面/主页面背景");
        var title = MakeText(rt, "Title", "游戏结束", 64, new Vector2(0f, 300f), new Vector2(600f, 90f),
            TextAnchor.MiddleCenter, new Color32(255, 80, 80, 255));
        var playerText = MakeText(rt, "PlayerText", "", 26, new Vector2(0f, 205f), new Vector2(500f, 40f),
            TextAnchor.MiddleCenter, new Color32(60, 200, 255, 255));
        var scoreText = MakeText(rt, "ScoreText", "0", 48, new Vector2(0f, 135f), new Vector2(600f, 70f),
            TextAnchor.MiddleCenter, new Color32(255, 220, 50, 255));
        var newRecordText = MakeText(rt, "NewRecordText", "新纪录", 30, new Vector2(0f, 78f), new Vector2(400f, 40f),
            TextAnchor.MiddleCenter, new Color32(255, 215, 0, 255));
        var highLabel = MakeText(rt, "HighLabelText", "历史最高", 20, new Vector2(0f, 40f), new Vector2(300f, 26f),
            TextAnchor.MiddleCenter, new Color32(150, 150, 150, 255));
        var highScore = MakeText(rt, "HighScoreText", "0", 26, new Vector2(0f, 8f), new Vector2(300f, 36f),
            TextAnchor.MiddleCenter, new Color32(255, 220, 50, 255));

        var levelLabel = MakeText(rt, "LevelLabelText", "等级", 20, new Vector2(-70f, -45f), new Vector2(120f, 30f),
            TextAnchor.MiddleRight, new Color32(150, 150, 150, 255));
        var levelText = MakeText(rt, "LevelText", "1", 28, new Vector2(40f, -45f), new Vector2(80f, 30f),
            TextAnchor.MiddleLeft, new Color32(100, 200, 100, 255));
        var comboLabel = MakeText(rt, "ComboLabelText", "最高连击", 20, new Vector2(-70f, -85f), new Vector2(120f, 30f),
            TextAnchor.MiddleRight, new Color32(150, 150, 150, 255));
        var comboText = MakeText(rt, "ComboText", "0", 28, new Vector2(40f, -85f), new Vector2(80f, 30f),
            TextAnchor.MiddleLeft, new Color32(255, 150, 30, 255));
        var unlockedLabel = MakeText(rt, "UnlockedLabelText", "解锁属性", 20, new Vector2(-70f, -125f), new Vector2(120f, 30f),
            TextAnchor.MiddleRight, new Color32(150, 150, 150, 255));
        var unlockedText = MakeText(rt, "UnlockedText", "1/5", 28, new Vector2(40f, -125f), new Vector2(80f, 30f),
            TextAnchor.MiddleLeft, new Color32(60, 200, 255, 255));

        // ---- 属性图标 ----
        var iconsRoot = Place("ElementIcons", rt, new Vector2(0f, -190f), new Vector2(420f, 60f));
        var icons = new ElementIcon[5];
        for (int i = 0; i < icons.Length; i++)
        {
            var el = ElementDatabase.AllElements[i];
            var itemRT = Place("Icon_" + ElementDatabase.GetName(el), iconsRoot,
                new Vector2(-160f + i * 80f, 0f), new Vector2(48f, 48f));

            var img = itemRT.gameObject.AddComponent<Image>();
            img.color = ElementDatabase.GetColor(el);
            img.raycastTarget = false;

            var label = MakeText(itemRT, "Label", ElementDatabase.GetName(el), 16,
                new Vector2(0f, -32f), new Vector2(100f, 22f), TextAnchor.MiddleCenter, Color.white);

            icons[i] = new ElementIcon { icon = img, label = label };
        }

        // 返回按钮：只显示图标
        var back = MakeButton(rt, "BackBtn", "", 24, new Vector2(28f, 20f), new Vector2(64f, 64f),
            out _, "界面/返回", null, new Vector2(0f, 0f), new Vector2(0f, 0f), true);

        var hint = MakeText(rt, "HintText", "按任意键或点击按钮返回...", 20, new Vector2(0f, 40f), new Vector2(600f, 26f),
            TextAnchor.MiddleCenter, new Color32(120, 120, 150, 255));

        var p = root.AddComponent<GameOverPanel>();
        p.backgroundImage = bg;
        p.titleText = title;
        p.playerText = playerText;
        p.scoreText = scoreText;
        p.newRecordText = newRecordText;
        p.highLabelText = highLabel;
        p.highScoreText = highScore;
        p.levelLabelText = levelLabel;
        p.levelText = levelText;
        p.comboLabelText = comboLabel;
        p.comboText = comboText;
        p.unlockedLabelText = unlockedLabel;
        p.unlockedText = unlockedText;
        p.elementIcons = icons;
        p.backButton = back;
        p.hintText = hint;
        return p;
    }

    static CreditsPanel BuildCredits(Transform canvas)
    {
        var root = NewPanel(canvas, "CreditsPanel");
        var rt = root.transform;

        var bg = FullBackground(rt, "界面/主页面背景");
        var title = MakeText(rt, "Title", "特别感谢", 64, new Vector2(0f, 250f), new Vector2(600f, 90f),
            TextAnchor.MiddleCenter, new Color32(255, 215, 0, 255));

        var names = new Text[4];
        for (int i = 0; i < names.Length; i++)
        {
            names[i] = MakeText(rt, "Name" + i, "", 36, new Vector2(0f, 130f - i * 60f), new Vector2(500f, 50f),
                TextAnchor.MiddleCenter, Color.white);
        }

        var hint = MakeText(rt, "HintText", "按任意键返回主菜单...", 20, new Vector2(0f, -150f), new Vector2(600f, 26f),
            TextAnchor.MiddleCenter, new Color32(120, 120, 150, 255));

        var p = root.AddComponent<CreditsPanel>();
        p.backgroundImage = bg;
        p.titleText = title;
        p.nameTexts = names;
        p.hintText = hint;
        return p;
    }

    // =====================================================================
    // 构造工具
    // =====================================================================
    /// <summary>场景里已有（含隐藏）就复用，没有就在根节点新建一个挂脚本的空对象。</summary>
    static GameObject EnsurePanel<T>(string name) where T : Component
    {
        var existing = Object.FindObjectOfType<T>(true);
        if (existing != null) return existing.gameObject;
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, UndoName);
        go.AddComponent<T>();
        return go;
    }

    static GameObject NewPanel(Transform canvas, string name)
    {
        var rt = Rect(name, canvas);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        return rt.gameObject;
    }

    static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        int uiLayer = LayerMask.NameToLayer("UI");
        go.layer = uiLayer >= 0 ? uiLayer : 5;
        Undo.RegisterCreatedObjectUndo(go, UndoName);
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
        return rt;
    }

    /// <summary>全屏背景：加 BackgroundCover，保证任何窗口比例下都不拉变形。</summary>
    static Image FullBackground(Transform parent, string spritePath)
    {
        var img = FullImage(parent, "BG", Color.white, spritePath);
        if (img.gameObject.GetComponent<BackgroundCover>() == null)
            img.gameObject.AddComponent<BackgroundCover>();
        return img;
    }

    static RectTransform Place(string name, Transform parent, Vector2 pos, Vector2 size,
                               Vector2? anchor = null, Vector2? pivot = null)
    {
        var rt = Rect(name, parent);
        var a = anchor ?? new Vector2(0.5f, 0.5f);
        rt.anchorMin = a;
        rt.anchorMax = a;
        rt.pivot = pivot ?? a;
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        return rt;
    }

    static Image FullImage(Transform parent, string name, Color? color = null, string spritePath = null,
                           bool raycast = false)
    {
        var rt = Rect(name, parent);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var img = rt.gameObject.AddComponent<Image>();
        img.color = color ?? Color.white;
        img.raycastTarget = raycast;
        if (!string.IsNullOrEmpty(spritePath))
        {
            var sp = LoadSprite(spritePath);
            if (sp != null) { img.sprite = sp; img.type = Image.Type.Simple; }
        }
        return img;
    }

    static Image MakeImage(Transform parent, string name, Vector2 pos, Vector2 size,
                           Color? color = null, string spritePath = null, bool preserveAspect = false,
                           Vector2? anchor = null, Vector2? pivot = null, bool raycast = false)
    {
        var rt = Place(name, parent, pos, size, anchor, pivot);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color ?? Color.white;
        img.raycastTarget = raycast;
        if (!string.IsNullOrEmpty(spritePath))
        {
            var sp = LoadSprite(spritePath);
            if (sp != null)
            {
                img.sprite = sp;
                img.type = Image.Type.Simple;
                img.preserveAspect = preserveAspect;
            }
        }
        return img;
    }

    static Text MakeText(Transform parent, string name, string content, int fontSize,
                         Vector2 pos, Vector2 size, TextAnchor align = TextAnchor.MiddleCenter,
                         Color? color = null, Vector2? anchor = null, Vector2? pivot = null)
    {
        var rt = Place(name, parent, pos, size, anchor, pivot);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = MainFont;
        t.text = content;
        t.fontSize = fontSize;
        t.alignment = align;
        t.color = color ?? Color.white;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    static Button MakeButton(Transform parent, string name, string label, int fontSize,
                             Vector2 pos, Vector2 size, out Text labelText, string spritePath = null,
                             Color? bgColor = null, Vector2? anchor = null, Vector2? pivot = null,
                             bool preserveAspect = false)
    {
        var rt = Place(name, parent, pos, size, anchor, pivot);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = bgColor ?? new Color(0.78f, 0.80f, 0.85f, 1f);
        img.raycastTarget = true;
        if (!string.IsNullOrEmpty(spritePath))
        {
            var sp = LoadSprite(spritePath);
            if (sp != null)
            {
                img.sprite = sp;
                img.type = Image.Type.Simple;
                img.preserveAspect = preserveAspect;   // 图标按钮不要被拉扁
            }
        }

        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;

        labelText = MakeText(rt, "Label", label, fontSize, Vector2.zero, size,
            TextAnchor.MiddleCenter, Color.white);
        return btn;
    }

    static InputField MakeInputField(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        var rt = Place(name, parent, pos, size);
        var bg = rt.gameObject.AddComponent<Image>();
        bg.color = new Color(0.12f, 0.14f, 0.20f, 0.95f);

        // 显示文本
        var textRT = Rect("Text", rt);
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(12f, 6f);
        textRT.offsetMax = new Vector2(-12f, -6f);
        var text = textRT.gameObject.AddComponent<Text>();
        text.font = MainFont;
        text.fontSize = 28;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleLeft;
        text.supportRichText = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;

        // 占位文字
        var phRT = Rect("Placeholder", rt);
        phRT.anchorMin = Vector2.zero;
        phRT.anchorMax = Vector2.one;
        phRT.offsetMin = new Vector2(12f, 6f);
        phRT.offsetMax = new Vector2(-12f, -6f);
        var ph = phRT.gameObject.AddComponent<Text>();
        ph.font = MainFont;
        ph.fontSize = 26;
        ph.color = new Color(0.55f, 0.55f, 0.60f, 1f);
        ph.alignment = TextAnchor.MiddleLeft;
        ph.text = "输入名字...";
        ph.raycastTarget = false;

        var input = rt.gameObject.AddComponent<InputField>();
        input.targetGraphic = bg;
        input.textComponent = text;
        input.placeholder = ph;
        input.characterLimit = 12;
        input.lineType = InputField.LineType.SingleLine;
        return input;
    }
}
