using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 游玩 HUD。界面结构对齐 pygame 原版 draw_playing：
///   左侧信息面板（玩家/HP/能量/冷却/等级/经验/分数/难度）
///   右侧属性面板（暂停按钮/当前属性头像/攻击·弹道/已解锁属性）
///   连击框、小地图（含敌人·BOSS·玩家·视野框）、波次提示、Boss 血条、底部操作提示、暂停遮罩
/// 全部在 Awake 时动态生成到独立 Canvas，无需手动摆。
/// 注意：不要 using System（会与 UnityEngine.Random 冲突）。
/// </summary>
public class HUD : MonoBehaviour
{
    const int MinimapSize = 140;

    // 程序化 UI 纹理（一次生成，全局复用；后面换成美术素材时只改这里）
    static Sprite panelFrame, barFrame, barFill;

    /// <summary>一条数值条：底框 + 渐变填充 + 描边数值。</summary>
    class BarUI
    {
        public Image frame, fill;
        public Text label;
        public float fullW;      // 可填充宽度（已扣除左右内缩）
        public bool art;         // true = 使用美术切片（Filled 模式），false = 程序化条
    }

    Canvas canvas;
    GameObject playRoot;        // 玩法 UI 根节点（开场动画期间整体隐藏）
    GameObject pauseOverlay;

    // 左面板（Hades 式组合：圆形头像+元素色光圈 在左，血条/能量条在右）
    BarUI hpBar, energyBar, expBar;
    Image cooldownFill;
    Text hpText, energyText, expText, levelText, scoreText, diffText, buffText;
    GameObject cooldownRoot;
    float pulseTime;            // 低血 / 满能量的呼吸计时
    Image elemAvatar, elemRing;
    Image[] shieldPips = new Image[5];   // 岩系护盾点（血条上方，每层护盾亮一颗）
    Image vignette;                      // 低血红色渐晕（屏幕边缘脉动）

    // 右面板（精简版：暂停按钮 + 已解锁属性小头像切换行）
    Image[] unlockAvatars = new Image[5];
    Image[] unlockFrames = new Image[5];

    // 其它
    Image bossHpFill;
    Text bossName, bossHpLabel, waveHint, comboText, comboLabel;
    GameObject bossBarRoot, comboRoot;
    float waveHintTimer;

    // Boss 信息卡（固定左上角）：卡片宽 / 血条高 / 填充内缩
    const float BossCardW = 330f;
    const float BossBarH = 16f;
    const float BossBarInset = 2f;
    float bossHpShown = -1f;     // 上一次刷进 UI 的血量比例（变化才重拼字符串）

    // 小地图
    RawImage minimapImage;
    Texture2D minimapTex;
    Color32[] minimapBase, minimapBuf;
    float minimapTimer;

    LevelConfig.LevelData level;
    Element lastElement = (Element)(-1);

    /// <summary>HUD 单例：道具拾取等系统需要弹提示时用。</summary>
    public static HUD Instance { get; private set; }

    // 道具拾取提示条（拾取瞬间显示"道具名 + 效果"，2.6 秒淡出）
    Text toastTitle, toastDesc;
    Outline toastTitleOutline, toastDescOutline;
    GameObject toastRoot;
    RectTransform toastRT;
    float toastTimer;
    float toastBaseY;
    const float ToastLife = 2.2f;      // 总显示时长（含最后 0.6s 淡出）
    float worldW = GameConfig.MapWidth;
    float worldH = GameConfig.MapHeight;

    // ---------------------------------------------------------------
    void Awake()
    {
        Instance = this;
        BuildUI();
        gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void BuildUI()
    {
        var go = new GameObject("HUDCanvas");
        go.transform.SetParent(transform, false);
        canvas = go.AddComponent<Canvas>();
        canvas.sortingOrder = 100;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        UITheme.EnsureScaler(canvas);
        go.AddComponent<GraphicRaycaster>().ignoreReversedGraphics = true;

        var root = NewRect(go.GetComponent<RectTransform>(), "PlayRoot", Vector2.zero, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
        playRoot = root.gameObject;
        Stretch(root);

        BuildVignette(root);    // 低血红色渐晕（最先创建 = 压在所有面板底下）
        BuildTopHud(root);      // 顶部：全宽经验条 + 分数/难度 + 连击（survivor 风格）
        BuildLeftPanel(root);
        BuildRightPanel(root);
        BuildMinimap(root);
        BuildTopCenter(root);
        BuildBottomBar(root);
        BuildItemToast(root);
        BuildPauseOverlay(go.transform);
    }

    // ---------------- 道具拾取提示 ----------------
    // 玩家不知道道具干嘛用：拾取瞬间在屏幕中下方弹出"道具名 + 效果说明"。
    // 刻意不做背景板：战斗画面本来就满，一块黑底会挡住敌人和弹幕。
    // 改为纯文字浮层 —— 靠加粗黑描边保证可读，配上浮+缩放动效让它显眼又不挡视线。
    void BuildItemToast(RectTransform parent)
    {
        toastBaseY = 165f;
        toastRT = NewRect(parent, "ItemToast", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, toastBaseY), new Vector2(560f, 72f));
        toastRoot = toastRT.gameObject;
        toastRoot.SetActive(false);

        var font = AssetLibrary.GetMainFont();

        // 道具名：本属性色 + 粗描边，比原来更大更醒目
        toastTitle = NewText(toastRT, "Name", "", font, 27, TextAnchor.UpperCenter);
        Place(toastTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -2f), new Vector2(540f, 40f));
        AddOutline(toastTitle, new Color32(0, 0, 0, 235), new Vector2(1.8f, -1.8f));
        toastTitleOutline = toastTitle.GetComponent<Outline>();

        // 效果说明：小一号、淡白色，同样带描边
        toastDesc = NewText(toastRT, "Desc", "", font, 17, TextAnchor.UpperCenter);
        Place(toastDesc.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -42f), new Vector2(540f, 30f));
        toastDesc.color = new Color32(235, 235, 245, 255);
        AddOutline(toastDesc, new Color32(0, 0, 0, 215), new Vector2(1.3f, -1.3f));
        toastDescOutline = toastDesc.GetComponent<Outline>();
    }

    /// <summary>弹出道具提示（带颜色）。无背景板，2.2 秒后自动上浮淡出。</summary>
    public void ShowItemToast(string name, string desc, Color color)
    {
        if (toastRoot == null) return;
        toastTitle.text = name;
        toastTitle.color = color;
        toastDesc.text = desc;
        toastRoot.SetActive(true);
        toastRT.anchoredPosition = new Vector2(0f, toastBaseY);
        toastRT.localScale = Vector3.one;
        SetToastAlpha(1f);
        toastTimer = ToastLife;
    }

    /// <summary>整体透明度（文字 + 描边一起淡，避免描边残留成黑块）。</summary>
    void SetToastAlpha(float a)
    {
        toastTitle.color = new Color(toastTitle.color.r, toastTitle.color.g, toastTitle.color.b, a);
        toastDesc.color = new Color(toastDesc.color.r, toastDesc.color.g, toastDesc.color.b, a);
        if (toastTitleOutline != null)
            toastTitleOutline.effectColor = new Color(0f, 0f, 0f, 0.92f * a);
        if (toastDescOutline != null)
            toastDescOutline.effectColor = new Color(0f, 0f, 0f, 0.84f * a);
    }

    // ---------------- 左侧信息面板（Hades 式组合） ----------------
    // 圆形元素头像（元素色光圈）在左，HP/能量条在右；护盾点叠在血条上方；
    // 冷却、增益状态收在下方。经验、分数、难度已移到顶部。
    // 用户反馈：不要面板灰底 —— 用透明容器，元素全部自带描边保证可读；
    // 整体挂到左下角（避开底部 35px 操作提示条）。
    void BuildLeftPanel(RectTransform parent)
    {
        var panel = NewRect(parent, "LeftPanel", new Vector2(0, 0), new Vector2(0, 0),
            new Vector2(15, 42), new Vector2(305, 175));

        var font = AssetLibrary.GetMainFont();

        // 玩家名（顶部一行，右侧留给护盾点）
        var nameText = NewText(panel, "PlayerText", "", font, 18, TextAnchor.UpperLeft);
        Place(nameText.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -10), new Vector2(180, 24));
        nameText.color = GameConfig.Cyan;
        AddOutline(nameText, new Color32(0, 0, 0, 200));

        // ---- 头像组合：元素色光圈（圆环）→ 圆底 → 元素头像 ----
        // 光圈颜色跟随当前属性（火红/水蓝/冰白/雷紫/岩棕），大招就绪时脉冲发光
        elemRing = NewImage(panel, "AvatarRing", Color.white);
        Place(elemRing.rectTransform, new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(42, -75), new Vector2(68, 68));
        elemRing.sprite = SpriteFactory.RingSprite(96, Color.white, 0.12f);

        var avatarBg = NewImage(panel, "AvatarCircle", Color.white);
        Place(avatarBg.rectTransform, new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(42, -75), new Vector2(56, 56));
        UITheme.Sprite(avatarBg, "界面/按钮条", false);

        elemAvatar = NewImage(panel, "Avatar", Color.white);
        Place(elemAvatar.rectTransform, new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(42, -75), new Vector2(48, 48));
        elemAvatar.preserveAspect = true;

        // ---- 岩系护盾点（血条上方，每层护盾亮一颗）----
        var pipSprite = SpriteFactory.CircleSprite(24, Color.white);
        for (int i = 0; i < 5; i++)
        {
            var pip = NewImage(panel, "ShieldPip_" + i, new Color32(210, 180, 140, 255));
            Place(pip.rectTransform, new Vector2(0, 1), new Vector2(0.5f, 0.5f),
                new Vector2(230 + i * 17, -16), new Vector2(12, 12));
            pip.sprite = pipSprite;
            pip.gameObject.SetActive(false);
            shieldPips[i] = pip;
        }

        // HP：美术血条（❤️图标 + 红色填充），数值压在轨道上
        hpBar = NewArtBar(panel, "HPBar", new Vector2(84, -42), new Vector2(205, 34),
            "界面/血条_底", "界面/血条_填充",
            fillX: 39f, fillW: 161f, fillCy: -2f, fillH: 20f, labelSize: 13);
        hpText = hpBar.label;

        // 能量：美术能量条（🔥图标 + 蓝色填充）
        energyBar = NewArtBar(panel, "EnergyBar", new Vector2(84, -80), new Vector2(205, 51),
            "界面/能量条_底", "界面/能量条_填充",
            fillX: 32f, fillW: 169f, fillCy: -8f, fillH: 21f, labelSize: 0);

        energyText = NewText(panel, "EnergyLabel", "", font, 14, TextAnchor.UpperLeft);
        Place(energyText.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(86, -134), new Vector2(206, 20));
        AddOutline(energyText, new Color32(0, 0, 0, 200));

        // 冷却（细条，仅冷却中显示）
        cooldownRoot = NewRect(panel, "CooldownBar", new Vector2(0, 1), new Vector2(0, 1), new Vector2(86, -156), new Vector2(200, 5)).gameObject;
        var cdBg = NewImage(cooldownRoot.GetComponent<RectTransform>(), "Bg", new Color(0, 0, 0, 0.85f));
        Stretch(cdBg.rectTransform);
        cooldownFill = NewImage(cooldownRoot.GetComponent<RectTransform>(), "Fill", GameConfig.Gray);
        AnchorFill(cooldownFill.rectTransform, new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(200, 5));

        // 大招增益状态（水系回血 / 冰系无敌 / 雷系充能 / 火系攻击 / 岩系护盾）
        // 没有状态时不占地方：整行隐藏，避免面板上一直挂着空行。
        buffText = NewText(panel, "BuffText", "", font, 12, TextAnchor.UpperLeft);
        Place(buffText.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -168), new Vector2(280, 40));
        buffText.supportRichText = true;
        buffText.horizontalOverflow = HorizontalWrapMode.Wrap;
        // 这是唯一一处多行堆叠：保持裁切，否则增益一多就会溢出面板盖住底部提示条
        buffText.verticalOverflow = VerticalWrapMode.Truncate;
        AddOutline(buffText, new Color32(0, 0, 0, 190));
    }

    // ---------------- 低血红色渐晕 ----------------
    // 程序化生成一张"边缘暗红、中心全透明"的径向渐变纹理，
    // 低血（<30%）时整体透明度脉动，替代纯文字的低血提示（FPS 常见的危险反馈）。
    void BuildVignette(RectTransform parent)
    {
        const int S = 256;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        float c = (S - 1) * 0.5f;
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                float dx = (x - c) / c, dy = (y - c) / c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);          // 0=中心 1=边角方向
                float t = Mathf.Clamp01((d - 0.62f) / 0.55f);     // 62% 半径外开始渐入
                byte a = (byte)(t * t * 255f);
                tex.SetPixel(x, y, new Color32(150, 10, 10, a));
            }
        }
        tex.Apply();

        vignette = NewImage(parent, "LowHpVignette", Color.white);
        Stretch(vignette.rectTransform);
        vignette.sprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        vignette.color = new Color(1f, 1f, 1f, 0f);
        vignette.rectTransform.SetAsFirstSibling();   // 保证压在所有面板底下
    }

    // ---------------- 顶部 HUD（survivor 风格） ----------------
    // 最顶端：全宽经验条（Lv 徽章嵌在左端）；其下：居中分数/难度；再下：浮动连击
    void BuildTopHud(RectTransform parent)
    {
        var font = AssetLibrary.GetMainFont();
        float W = GameConfig.ScreenWidth;

        // 全宽经验条（NewBar 用左上角锚点：pos 从左上角起算）
        expBar = NewBar(parent, "ExpBar", new Vector2(8f, -8f),
            new Vector2(W - 16f, 10f), new Color32(80, 200, 80, 255), 0);

        // Lv 徽章压在经验条左端
        levelText = NewText(parent, "LevelText", "Lv.1", font, 15, TextAnchor.MiddleCenter);
        Place(levelText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -4f), new Vector2(64f, 18f));
        levelText.color = new Color32(20, 24, 34, 255);
        AddOutline(levelText, new Color32(160, 255, 170, 220), new Vector2(1f, -1f));

        expText = NewText(parent, "ExpLabel", "", font, 12, TextAnchor.UpperLeft);
        Place(expText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(300f, 16f));
        expText.color = new Color32(170, 230, 175, 255);
        expText.alignment = TextAnchor.MiddleCenter;
        AddOutline(expText, new Color32(0, 0, 0, 200));

        // 分数 / 难度（居中）
        scoreText = NewText(parent, "ScoreText", "分数: 0", font, 26, TextAnchor.MiddleCenter);
        Place(scoreText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -42f), new Vector2(420f, 34f));
        scoreText.color = new Color32(255, 214, 90, 255);
        AddOutline(scoreText, new Color32(0, 0, 0, 230), new Vector2(1.5f, -1.5f));

        diffText = NewText(parent, "DiffText", "难度: 1", font, 15, TextAnchor.MiddleCenter);
        Place(diffText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -76f), new Vector2(300f, 20f));
        diffText.color = new Color32(255, 120, 110, 255);
        AddOutline(diffText, new Color32(0, 0, 0, 210));

        // 连击（浮动文字，不再用面板框）
        // 高度 56：上面给 30px 大数字留够行高（≈43px），下面一条 20px 高的 COMBO 小标。
        comboRoot = NewRect(parent, "ComboBox", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -100f), new Vector2(300f, 56f)).gameObject;
        comboRoot.SetActive(false);

        comboText = NewText((RectTransform)comboRoot.transform, "Combo", "", font, 28, TextAnchor.MiddleCenter);
        var crt = comboText.rectTransform;
        crt.anchorMin = new Vector2(0f, 0.36f);
        crt.anchorMax = new Vector2(1f, 1f);
        crt.offsetMin = crt.offsetMax = Vector2.zero;
        comboText.color = new Color32(255, 200, 0, 255);
        AddOutline(comboText, new Color32(60, 30, 0, 230), new Vector2(2f, -2f));

        comboLabel = NewText((RectTransform)comboRoot.transform, "Label", "COMBO", font, 13, TextAnchor.MiddleCenter);
        var lrt = comboLabel.rectTransform;
        lrt.anchorMin = new Vector2(0f, 0f);
        lrt.anchorMax = new Vector2(1f, 0.36f);
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        comboLabel.color = new Color32(255, 160, 40, 255);
        AddOutline(comboLabel, new Color32(60, 30, 0, 210));
    }

    // ---------------------------------------------------------------
    /// <summary>
    /// 造一条「底框 + 渐变填充 + 居中描边数值」的条。
    /// 填充相对底框四周内缩 inset 像素，所以即使填到 100% 也能看见边框，不会糊成一块纯色。
    /// </summary>
    BarUI NewBar(RectTransform parent, string name, Vector2 pos, Vector2 size,
                 Color fillColor, int labelSize = 12, float inset = 2f)
    {
        var root = NewRect(parent, name, new Vector2(0, 1), new Vector2(0, 1), pos, size);

        var frame = NewImage(root, "Frame", Color.white);
        Stretch(frame.rectTransform);
        frame.sprite = barFrame ?? (barFrame = SpriteFactory.FrameSprite(48, 7,
            new Color32(132, 140, 170, 255), new Color32(8, 8, 16, 245), new Color32(16, 16, 26, 215)));
        frame.type = Image.Type.Sliced;

        var fill = NewImage(root, "Fill", fillColor);
        fill.sprite = barFill ?? (barFill = SpriteFactory.BarFillSprite(64, 16));
        fill.type = Image.Type.Simple;
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = new Vector2(0f, 1f);
        fill.rectTransform.pivot = new Vector2(0f, 0.5f);
        fill.rectTransform.offsetMin = new Vector2(inset, inset);
        fill.rectTransform.offsetMax = new Vector2(size.x - inset, -inset);

        var b = new BarUI { frame = frame, fill = fill, fullW = size.x - inset * 2f };

        if (labelSize > 0)
        {
            b.label = NewText(root, "Label", "", AssetLibrary.GetMainFont(), labelSize, TextAnchor.MiddleCenter);
            Stretch(b.label.rectTransform);
            AddOutline(b.label, new Color32(0, 0, 0, 225));
        }
        return b;
    }

    /// <summary>
    /// 美术切片数值条：底图（图标 + 空轨道）+ Filled 填充图叠加在轨道区域上。
    /// fillX/fillW/fillCy/fillH 为填充矩形相对底图（左下角原点、中心参考）的摆放，
    /// 数值来自 Tools/slice_bars.py 对素材的像素测量。
    /// 素材缺失时自动回退到程序化条，保证游戏不会因为少一张图而开天窗。
    /// </summary>
    BarUI NewArtBar(RectTransform parent, string name, Vector2 pos, Vector2 size,
                    string framePath, string fillPath,
                    float fillX, float fillW, float fillCy, float fillH, int labelSize)
    {
        var root = NewRect(parent, name, new Vector2(0, 1), new Vector2(0, 1), pos, size);

        var frame = NewImage(root, "Frame", Color.white);
        Stretch(frame.rectTransform);
        var frameSp = AssetLibrary.GetSprite(framePath);
        frame.sprite = frameSp;

        var fill = NewImage(root, "Fill", Color.white);
        var fillSp = AssetLibrary.GetSprite(fillPath);
        fill.sprite = fillSp;

        if (frameSp != null && fillSp != null)
        {
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            var frt = fill.rectTransform;
            frt.anchorMin = frt.anchorMax = new Vector2(0f, 0.5f);
            frt.pivot = new Vector2(0f, 0.5f);
            frt.anchoredPosition = new Vector2(fillX, fillCy);
            frt.sizeDelta = new Vector2(fillW, fillH);
        }
        else
        {
            // 回退：程序化底框 + 简单填充
            Debug.LogWarning("[HUD] 缺少 " + framePath + " / " + fillPath + "，血条能量条回退为程序化样式");
            frame.sprite = barFrame ?? (barFrame = SpriteFactory.FrameSprite(48, 7,
                new Color32(132, 140, 170, 255), new Color32(8, 8, 16, 245), new Color32(16, 16, 26, 215)));
            frame.type = Image.Type.Sliced;
            fill.color = fillPath.Contains("血条") ? new Color32(220, 60, 60, 255) : GameConfig.Cyan;
            fill.sprite = barFill ?? (barFill = SpriteFactory.BarFillSprite(64, 16));
            fill.type = Image.Type.Simple;
            var frt = fill.rectTransform;
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = new Vector2(0f, 1f);
            frt.pivot = new Vector2(0f, 0.5f);
            frt.offsetMin = new Vector2(2f, 2f);
            frt.offsetMax = new Vector2(size.x - 2f, -2f);
        }

        var b = new BarUI { frame = frame, fill = fill, fullW = fillW,
                            art = frameSp != null && fillSp != null };

        if (labelSize > 0)
        {
            b.label = NewText(root, "Label", "", AssetLibrary.GetMainFont(), labelSize, TextAnchor.MiddleCenter);
            var lrt = b.label.rectTransform;
            // 数值压在填充轨道上方（避开左侧图标区）
            lrt.anchorMin = lrt.anchorMax = new Vector2(0f, 0.5f);
            lrt.pivot = new Vector2(0f, 0.5f);
            lrt.anchoredPosition = new Vector2(fillX, fillCy);
            lrt.sizeDelta = new Vector2(fillW, size.y - 6f);
            AddOutline(b.label, new Color32(0, 0, 0, 225));
        }
        return b;
    }

    /// <summary>设置条的填充比例（0~1）与颜色。</summary>
    static void SetBar(BarUI b, float frac, Color color)
    {
        if (b == null || b.fill == null) return;
        frac = Mathf.Clamp01(frac);
        if (b.art)
        {
            b.fill.fillAmount = frac;
            b.fill.color = color;   // color 通常带 alpha，用于低血/满能量呼吸
            return;
        }
        b.fill.color = color;
        var rt = b.fill.rectTransform;
        rt.offsetMax = new Vector2(rt.offsetMin.x + b.fullW * frac, rt.offsetMax.y);
    }

    /// <summary>给文本加黑色描边，压在亮色条/亮背景上也读得清。</summary>
    static void AddOutline(Text t, Color c, Vector2? dist = null)
    {
        if (t == null) return;
        var o = t.gameObject.AddComponent<Outline>();
        o.effectColor = c;
        o.effectDistance = dist ?? new Vector2(1f, -1f);
    }

    // ---------------- 右侧属性面板（精简版） ----------------
    // 元素头像已并入左面板血条旁，这里只留：暂停按钮 + 已解锁属性小头像切换行。
    // 去掉了元素名 / 攻击 / 弹道 / 移速文字，减少右上的视觉噪音。
    void BuildRightPanel(RectTransform parent)
    {
        var panel = Panel(parent, "RightPanel", new Vector2(1, 1), new Vector2(1, 1),
            new Vector2(-15, -15), new Vector2(186, 92));

        // 暂停按钮（右上角）
        var pauseBtn = NewRect(panel, "PauseBtn", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-8, -6), new Vector2(40, 40));
        var pauseImg = NewImage(pauseBtn, "Icon", Color.white);
        Stretch(pauseImg.rectTransform);
        UITheme.Sprite(pauseImg, "界面/暂停按钮", false);
        pauseImg.raycastTarget = true;   // 按钮图必须接收射线，否则点不到
        var pb = pauseBtn.gameObject.AddComponent<Button>();
        pb.targetGraphic = pauseImg;
        pb.onClick.AddListener(() => { if (LevelManager.Instance != null) LevelManager.Instance.TogglePause(); });

        // 已解锁属性小头像（点击切换，配合 Q/R 键）
        for (int i = 0; i < 5; i++)
        {
            var el = ElementDatabase.AllElements[i];
            var frame = NewImage(panel, "Frame_" + el, new Color(0.31f, 0.31f, 0.31f, 1f));
            Place(frame.rectTransform, new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(168 - i * 34, -62), new Vector2(30, 30));
            unlockFrames[i] = frame;

            var img = NewImage(panel, "Unlock_" + el, Color.white);
            Place(img.rectTransform, new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(168 - i * 34, -62), new Vector2(26, 26));
            img.preserveAspect = true;
            img.raycastTarget = true;   // 按钮图必须接收射线，否则点不到
            var sp = AssetLibrary.GetSprite("头像/" + ElementDatabase.AvatarFile(el));
            if (sp != null) img.sprite = sp;
            unlockAvatars[i] = img;

            // 点击头像直接切换属性（配合 Q/R 键）
            Element target = el;
            var ub = img.gameObject.AddComponent<Button>();
            ub.targetGraphic = img;
            ub.transition = Selectable.Transition.ColorTint;
            var uc = ub.colors;
            uc.normalColor = Color.white;
            uc.highlightedColor = new Color32(255, 230, 160, 255);
            uc.pressedColor = new Color32(200, 170, 100, 255);
            uc.selectedColor = Color.white;
            uc.colorMultiplier = 1f;
            ub.colors = uc;
            ub.onClick.AddListener(() =>
            {
                var pl = GameManager.Instance != null ? GameManager.Instance.player : null;
                if (pl != null) pl.SwitchElement(target);
            });
        }
    }

    // ---------------- 连击框（已并入 BuildTopHud 的浮动连击文字） ----------------

    // ---------------- 小地图 ----------------
    // 右侧面板精简后（92 高），小地图上移补齐空位，与右面板连成一个纵向信息区
    void BuildMinimap(RectTransform parent)
    {
        var root = Panel(parent, "MinimapPanel", new Vector2(1, 1), new Vector2(1, 1),
            new Vector2(-15, -119), new Vector2(150, 150));

        minimapTex = new Texture2D(MinimapSize, MinimapSize, TextureFormat.RGBA32, false);
        minimapTex.filterMode = FilterMode.Point;
        minimapTex.wrapMode = TextureWrapMode.Clamp;

        minimapBase = new Color32[MinimapSize * MinimapSize];
        minimapBuf = new Color32[MinimapSize * MinimapSize];
        var bg = new Color32(15, 15, 25, 220);
        for (int i = 0; i < minimapBase.Length; i++) minimapBase[i] = bg;
        DrawMinimapGrid();
        minimapTex.SetPixels32(minimapBase);
        minimapTex.Apply();

        var mmGO = new GameObject("Minimap", typeof(RectTransform));
        mmGO.transform.SetParent(root, false);
        var img = mmGO.AddComponent<RawImage>();
        img.texture = minimapTex;
        img.raycastTarget = false;
        minimapImage = img;
        Place(mmGO.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(0, 0), new Vector2(5, 5), new Vector2(MinimapSize, MinimapSize));

        var legend = NewText(root, "Legend", "● 玩家  ● 敌人  ● BOSS", AssetLibrary.GetMainFont(), 11, TextAnchor.MiddleCenter);
        Place(legend.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, -12), new Vector2(150, 16));
        legend.color = new Color32(110, 110, 110, 255);
    }

    void DrawMinimapGrid()
    {
        var line = new Color32(40, 45, 55, 255);
        var border = new Color32(50, 50, 80, 255);
        for (int i = 1; i < 4; i++)
        {
            int p = i * MinimapSize / 4;
            for (int k = 0; k < MinimapSize; k++)
            {
                minimapBase[k * MinimapSize + p] = line;
                minimapBase[p * MinimapSize + k] = line;
            }
        }
        for (int k = 0; k < MinimapSize; k++)
        {
            minimapBase[k] = border;
            minimapBase[k * MinimapSize] = border;
            minimapBase[(MinimapSize - 1) * MinimapSize + k] = border;
            minimapBase[k * MinimapSize + (MinimapSize - 1)] = border;
        }
    }

    // ---------------- 顶部居中（波次提示） + 左上角（Boss 信息卡） ----------------
    void BuildTopCenter(RectTransform parent)
    {
        var font = AssetLibrary.GetMainFont();

        // 顶部 HUD（经验条/分数/连击）已占用 y=-8 ~ -148，这里从更低处开始
        waveHint = NewText(parent, "WaveHint", "", font, 40, TextAnchor.MiddleCenter);
        Place(waveHint.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -165), new Vector2(600, 60));
        waveHint.color = GameConfig.Yellow;

        // ---- Boss 信息卡：固定左上角（经验条下方），不居中、不跟随镜头 ----
        // 布局：第一行 [名字 左对齐] ......... [当前血 / 上限 右对齐]，第二行整条血槽。
        bossBarRoot = NewRect(parent, "BossBar", new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(14, -30), new Vector2(BossCardW, 46)).gameObject;
        bossBarRoot.SetActive(false);

        var bbrt = bossBarRoot.GetComponent<RectTransform>();

        // 名字底板：半透明黑压住战场，保证名字在任何地形上都读得清
        var namePanel = NewImage(bbrt, "NameBg", new Color(0, 0, 0, 0.6f));
        Place(namePanel.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 0), new Vector2(BossCardW, 26));

        // 名字：左上角左对齐，元素配色
        bossName = NewText(bbrt, "BossName", "", font, 17, TextAnchor.MiddleLeft);
        Place(bossName.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, 0), new Vector2(196, 26));
        AddOutline(bossName, new Color32(0, 0, 0, 240), new Vector2(1.6f, -1.6f));

        // 血量数值：同一行右对齐（4000 / 4000）
        bossHpLabel = NewText(bbrt, "HpLabel", "", font, 13, TextAnchor.MiddleRight);
        Place(bossHpLabel.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-10, 0), new Vector2(150, 26));
        AddOutline(bossHpLabel, new Color32(0, 0, 0, 235), new Vector2(1.2f, -1.2f));

        // 血条轨道底
        var bg = NewImage(bbrt, "Bg", new Color(0, 0, 0, 0.8f));
        Place(bg.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -28), new Vector2(BossCardW, BossBarH));

        // 红色血量填充：左上锚定，从左边往右缩
        bossHpFill = NewImage(bbrt, "Fill", new Color32(220, 60, 60, 255));
        Place(bossHpFill.rectTransform, new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(BossBarInset, -28 - BossBarInset),
            new Vector2(BossCardW - BossBarInset * 2, BossBarH - BossBarInset * 2));
    }

    // ---------------- 底部提示条 ----------------
    void BuildBottomBar(RectTransform parent)
    {
        var bar = Panel(parent, "BottomBar", new Vector2(0, 0), new Vector2(0, 0), Vector2.zero, new Vector2(0, 35));
        // 横铺整屏、贴底 35 像素高
        bar.anchorMin = new Vector2(0f, 0f);
        bar.anchorMax = new Vector2(1f, 0f);
        bar.pivot = new Vector2(0f, 0f);
        bar.offsetMin = new Vector2(0f, 0f);
        bar.offsetMax = new Vector2(0f, 35f);

        var font = AssetLibrary.GetMainFont();
        var t = NewText(bar, "Hint", "WASD 移动 | 方向键转向射击 | F 大招 | Q/R 切换属性 | 1-3 选升级 | P 暂停 | ESC 返回菜单",
            font, 16, TextAnchor.MiddleCenter);
        Stretch(t.rectTransform);
        t.color = new Color32(235, 235, 245, 255);
        AddOutline(t, new Color32(0, 0, 0, 230), new Vector2(1.5f, -1.5f));
    }

    // ---------------- 暂停遮罩 ----------------
    void BuildPauseOverlay(Transform canvasRoot)
    {
        pauseOverlay = new GameObject("PauseOverlay", typeof(RectTransform));
        pauseOverlay.transform.SetParent(canvasRoot, false);
        var img = pauseOverlay.AddComponent<Image>();
        img.color = new Color(0, 0, 0, 0.47f);
        img.raycastTarget = false;
        Stretch(pauseOverlay.GetComponent<RectTransform>());

        var font = AssetLibrary.GetMainFont();
        var t = NewText(pauseOverlay.GetComponent<RectTransform>(), "PauseText", "游戏暂停", font, 56, TextAnchor.MiddleCenter);
        Place(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(500, 80));

        var t2 = NewText(pauseOverlay.GetComponent<RectTransform>(), "PauseHint", "按 P 或点击暂停按钮继续",
            font, 20, TextAnchor.MiddleCenter);
        Place(t2.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -30), new Vector2(500, 40));
        t2.color = new Color32(180, 180, 180, 255);

        pauseOverlay.SetActive(false);
    }

    // ---------------------------------------------------------------
    public void SetLevel(LevelConfig.LevelData ld)
    {
        level = ld;
        worldW = GameConfig.MapWidth;
        worldH = GameConfig.MapHeight;
        bossBarRoot.SetActive(false);
        if (pauseOverlay != null) pauseOverlay.SetActive(false);
        if (comboRoot != null) comboRoot.SetActive(false);
    }

    public void SetBoss(BossController b)
    {
        bossBarRoot.SetActive(true);
        // 名字用元素配色（火橙 / 冰蓝 / 水蓝 / 雷紫 / 岩棕），一眼看出这关 Boss 的属性
        // 名字用元素配色（火橙 / 冰蓝 / 水蓝 / 雷紫 / 岩棕），一眼看出这关 Boss 的属性；
        // 往白色提一点亮度，保证岩系这种深色在黑色底板上也看得清。
        bossName.text = ElementDatabase.GetName(b.element) + "属性 BOSS";
        bossName.color = Color.Lerp(ElementDatabase.GetColor(b.element), Color.white, 0.25f);
        bossName.transform.SetAsLastSibling();   // 名字永远压在最上层，不会被底板/血条遮住
        bossHpShown = -1f;                       // 让下一帧强制刷一次血量数值
    }

    public void ClearBoss() => bossBarRoot.SetActive(false);

    public void ShowWaveHint(string text)
    {
        waveHint.text = text;
        waveHint.gameObject.SetActive(true);
        waveHintTimer = 2f;
    }

    /// <summary>暂停时隐藏玩法 UI 并显示遮罩。</summary>
    public void SetPaused(bool on)
    {
        if (pauseOverlay != null) pauseOverlay.SetActive(on);
        if (playRoot != null) playRoot.SetActive(!on);
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        var p = gm.player;
        if (p == null) return;

        // 呼吸计时用真实时间，暂停 / 减速时不会卡住不动
        pulseTime += Time.unscaledDeltaTime;

        UpdateLeftPanel(gm, p);
        UpdateRightPanel(gm, p);

        // 连击
        bool comboOn = gm.combo > 1;
        comboRoot.SetActive(comboOn);
        if (comboOn) comboText.text = "x" + gm.combo;

        // Boss 血条：填充宽度 + 血量数值（值没变就不重拼字符串）
        var boss = gm.boss;
        if (boss != null && !boss.Dead)
        {
            float hpFrac = boss.HpFraction;
            if (!Mathf.Approximately(hpFrac, bossHpShown))
            {
                bossHpShown = hpFrac;
                bossHpFill.rectTransform.sizeDelta =
                    new Vector2((BossCardW - BossBarInset * 2) * hpFrac, BossBarH - BossBarInset * 2);
                bossHpLabel.text = Mathf.CeilToInt(boss.hp) + " / " + Mathf.CeilToInt(boss.maxHp);
            }
        }

        // 波次提示淡出
        if (waveHintTimer > 0f)
        {
            waveHintTimer -= Time.unscaledDeltaTime;
            if (waveHintTimer <= 0f) waveHint.gameObject.SetActive(false);
            else waveHint.color = new Color(1f, 0.85f, 0f, Mathf.Clamp01(waveHintTimer));
        }

        // 道具提示：弹出上浮 → 停留 → 淡出（无背景板，不挡视野）
        if (toastTimer > 0f)
        {
            toastTimer -= Time.unscaledDeltaTime;
            if (toastTimer <= 0f) toastRoot.SetActive(false);
            else if (toastRT != null)
            {
                // 起始 0.16 秒：从大到小回弹 + 向上浮 14px，吸引注意但不挡画面
                float age = ToastLife - toastTimer;
                float pop = Mathf.Clamp01(age / 0.16f);
                float e = 1f - (1f - pop) * (1f - pop);          // easeOut
                float s = Mathf.Lerp(1.22f, 1f, e);
                toastRT.localScale = new Vector3(s, s, 1f);
                toastRT.anchoredPosition = new Vector2(0f, toastBaseY + 14f * e);

                float a = Mathf.Clamp01(toastTimer / 0.6f);       // 最后 0.6 秒渐隐
                SetToastAlpha(a);
            }
        }

        // 小地图（限帧刷新，避免每帧刷 140×140 的像素）
        minimapTimer -= Time.unscaledDeltaTime;
        if (minimapTimer <= 0f)
        {
            minimapTimer = 0.08f;
            RefreshMinimap(gm, p);
        }
    }

    void UpdateLeftPanel(GameManager gm, PlayerController p)
    {
        // 玩家名
        var nameGO = transform.Find("HUDCanvas/PlayRoot/LeftPanel/PlayerText");
        if (nameGO != null)
        {
            var nt = nameGO.GetComponent<Text>();
            if (nt != null) nt.text = SaveSystem.Data.currentPlayer;
        }

        // HP：美术填充自带红色。低血（<30%）时血条呼吸 + 数值变红 + 屏幕边缘红色渐晕脉动
        float hpFrac = Mathf.Clamp01((float)p.hp / Mathf.Max(1, p.maxHp));
        Color hpCol = Color.white;
        bool lowHp = hpFrac < 0.3f;
        if (lowHp)
        {
            float k = 0.55f + 0.45f * (0.5f + 0.5f * Mathf.Sin(pulseTime * 7f));
            hpCol = new Color(1f, 1f, 1f, k);
        }
        SetBar(hpBar, hpFrac, hpCol);
        hpText.text = "HP: " + p.hp + "/" + p.maxHp;
        hpText.color = lowHp
            ? new Color(1f, 0.4f, 0.4f, 0.7f + 0.3f * (0.5f + 0.5f * Mathf.Sin(pulseTime * 7f)))
            : Color.white;

        // 低血红色渐晕（屏幕四周暗红脉动，危险感直接糊脸上）
        if (vignette != null)
        {
            float va = lowHp ? 0.28f + 0.22f * (0.5f + 0.5f * Mathf.Sin(pulseTime * 7f)) : 0f;
            vignette.color = new Color(1f, 1f, 1f, va);
        }

        // 头像光圈：平时染当前属性色，大招就绪时向亮黄脉冲（"可以放大招了"）
        if (elemRing != null)
        {
            Color ringCol = ElementDatabase.GetColor(p.element);
            if (p.skillReady && p.skillCooldown <= 0)
            {
                float k = 0.5f + 0.5f * Mathf.Sin(pulseTime * 6f);
                ringCol = Color.Lerp(ringCol, new Color32(255, 230, 90, 255), k);
            }
            elemRing.color = ringCol;
        }

        // 岩系护盾点：每层护盾亮一颗，超过 5 层全亮
        if (shieldPips != null && shieldPips[0] != null)
        {
            bool anyShield = p.rockShieldCount > 0;
            for (int i = 0; i < 5; i++)
            {
                shieldPips[i].gameObject.SetActive(anyShield);
                shieldPips[i].color = i < p.rockShieldCount
                    ? new Color32(225, 195, 150, 255)
                    : new Color32(80, 80, 92, 150);
            }
        }

        // 能量：满能量且不在冷却时整条呼吸发光（透明度脉冲），提示"可以放大招了"
        float enFrac = Mathf.Clamp01((float)p.energy / Mathf.Max(1, p.maxEnergy));
        Color enCol = Color.white;
        if (p.skillReady && p.skillCooldown <= 0)
        {
            float k = 0.72f + 0.28f * (0.5f + 0.5f * Mathf.Sin(pulseTime * 6f));
            enCol = new Color(1f, 1f, 1f, k);
        }
        SetBar(energyBar, enFrac, enCol);

        if (p.skillReady)
            energyText.text = p.skillCooldown > 0
                ? "冷却中 " + (p.skillCooldown / 60 + 1) + "s"
                : "大招就绪 (F)";
        else
            energyText.text = "能量: " + p.energy + "/" + p.maxEnergy;

        bool cd = p.skillCooldown > 0;
        cooldownRoot.SetActive(cd);
        if (cd)
        {
            float k = 1f - (float)p.skillCooldown / p.skillMaxCooldown;
            cooldownFill.rectTransform.sizeDelta = new Vector2(200 * Mathf.Clamp01(k), 5);
        }

        // 等级 / 经验
        levelText.text = "Lv." + p.level;
        SetBar(expBar, (float)p.exp / Mathf.Max(1, p.expToNext), new Color32(80, 200, 80, 255));
        expText.text = "EXP: " + p.exp + "/" + p.expToNext;

        // 分数 / 难度
        scoreText.text = "分数: " + gm.score.ToString("#,0");
        diffText.text = "难度: " + gm.difficultyLevel;

        UpdateBuffText(p);
    }

    /// <summary>把大招释放后的元素专属增益实时显示出来（原来全靠玩家自己猜）。</summary>
    void UpdateBuffText(PlayerController p)
    {
        if (buffText == null) return;

        string s = "";
        if (p.waterHealTimer > 0)
            s += "<color=#78FFBE>治愈 " + Sec(p.waterHealTimer) + "s (+2%/秒)</color>\n";
        if (p.iceAuraTimer > 0)
            s += "<color=#8FD8FF>无敌 " + Sec(p.iceAuraTimer) + "s</color>\n";
        if (p.thunderChargeTimer > 0)
            s += "<color=#C8A0FF>充能 x2 " + Sec(p.thunderChargeTimer) + "s</color>\n";
        if (p.firePowerTimer > 0)
            s += "<color=#FF9A50>攻击 +50% " + Sec(p.firePowerTimer) + "s</color>\n";
        if (p.burnTimer > 0)
            s += "<color=#FF7828>灼烧 " + Sec(p.burnTimer) + "s（水大招可解除）</color>\n";
        if (p.itemAtkTimer > 0)
            s += "<color=#FFC860>烈焰之羽 攻击+" + Mathf.RoundToInt(p.itemAtkBonus * 100f) + "% " + Sec(p.itemAtkTimer) + "s</color>\n";
        if (p.rockShieldCount > 0)
            s += "<color=#D2B48C>护盾 x" + p.rockShieldCount + "</color>";

        bool on = !string.IsNullOrEmpty(s);
        buffText.gameObject.SetActive(on);
        if (on) buffText.text = s.TrimEnd('\n');
    }

    static int Sec(int frames) => Mathf.Max(1, Mathf.CeilToInt(frames / 60f));

    void UpdateRightPanel(GameManager gm, PlayerController p)
    {
        // 元素头像在左面板：切换属性时刷新头像贴图
        if (p.element != lastElement)
        {
            lastElement = p.element;
            UITheme.Sprite(elemAvatar, "头像/" + ElementDatabase.AvatarFile(p.element), false);
        }

        // 已解锁属性小头像：未解锁的隐藏，当前属性金色描边
        for (int i = 0; i < 5; i++)
        {
            var el = ElementDatabase.AllElements[i];
            bool owned = p.unlockedElements.Contains(el);
            unlockAvatars[i].gameObject.SetActive(owned);
            unlockFrames[i].gameObject.SetActive(owned);
            if (owned)
                unlockFrames[i].color = p.element == el ? GameConfig.Yellow : new Color32(80, 80, 80, 255);
        }
    }

    // ---------------------------------------------------------------
    void RefreshMinimap(GameManager gm, PlayerController p)
    {
        if (minimapTex == null) return;

        System.Array.Copy(minimapBase, minimapBuf, MinimapSize * MinimapSize);

        var enemyCol = new Color32(200, 100, 100, 255);
        var bossCol = new Color32(255, 50, 50, 255);
        var shooterCol = new Color32(255, 150, 50, 255);
        var playerCol = new Color32(0, 255, 255, 255);

        for (int i = 0; i < gm.enemies.Count; i++)
        {
            var e = gm.enemies[i];
            if (e == null || e.Dead) continue;
            Plot(e.transform.position, e.isBoss ? 3 : e.isShooter ? 2 : 1,
                e.isBoss ? bossCol : e.isShooter ? shooterCol : enemyCol);
        }

        if (gm.boss != null && !gm.boss.Dead)
            Plot(gm.boss.transform.position, 3, bossCol);

        // 玩家：白色描边 + 青色芯，比纯色点更醒目
        Plot(p.transform.position, 3, new Color32(255, 255, 255, 255));
        Plot(p.transform.position, 1, playerCol);
        DrawCameraFrame();

        minimapTex.SetPixels32(minimapBuf);
        minimapTex.Apply();
    }

    /// <summary>小地图上的相机视野框（白色细框）。</summary>
    void DrawCameraFrame()
    {
        var cam = Camera.main;
        if (cam == null) return;

        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        float cx = cam.transform.position.x, cy = cam.transform.position.y;

        float left = cx - halfW, right = cx + halfW;
        float top = cy + halfH, bottom = cy - halfH;

        var col = new Color32(255, 255, 255, 200);
        int x0 = ToMM(left, worldW), x1 = ToMM(right, worldW);
        int y0 = ToMM(bottom, worldH), y1 = ToMM(top, worldH);

        for (int x = x0; x <= x1; x++)
        {
            SetMM(x, y0, col); SetMM(x, y1, col);
        }
        for (int y = y0; y <= y1; y++)
        {
            SetMM(x0, y, col); SetMM(x1, y, col);
        }
    }

    int ToMM(float worldCoord, float worldMax)
    {
        float k = Mathf.Clamp01(worldCoord / Mathf.Max(1f, worldMax));
        return Mathf.Clamp(Mathf.RoundToInt(k * (MinimapSize - 1)), 0, MinimapSize - 1);
    }

    void Plot(Vector2 worldPos, int radius, Color32 col)
    {
        int cx = ToMM(worldPos.x, worldW), cy = ToMM(worldPos.y, worldH);
        for (int y = cy - radius; y <= cy + radius; y++)
            for (int x = cx - radius; x <= cx + radius; x++)
                SetMM(x, y, col);
    }

    void SetMM(int x, int y, Color32 col)
    {
        if (x < 0 || y < 0 || x >= MinimapSize || y >= MinimapSize) return;
        minimapBuf[y * MinimapSize + x] = col;
    }

    // ---------------------------------------------------------------
    // UI 构造小工具
    RectTransform NewRect(RectTransform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        if (parent != null) go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    RectTransform Panel(RectTransform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size,
                        Color? tint = null)
    {
        var rt = NewRect(parent, name, anchor, pivot, pos, size);

        var bg = NewImage(rt, "Bg", tint ?? Color.white);
        Stretch(bg.rectTransform);
        bg.sprite = panelFrame ?? (panelFrame = SpriteFactory.FrameSprite(48, 10,
            new Color32(88, 94, 122, 235), new Color32(10, 10, 18, 235), new Color32(16, 18, 28, 200)));
        bg.type = Image.Type.Sliced;
        bg.rectTransform.SetAsFirstSibling();   // 保证背景在最底层

        return rt;
    }

    /// <summary>左上角为原点的摆放（用于面板内部元素）。</summary>
    static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static void AnchorFill(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    /// <summary>四角对齐父节点。</summary>
    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void Stretch(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    Image NewImage(RectTransform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    Text NewText(RectTransform parent, string name, string txt, Font f, int size, TextAnchor align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.text = txt;
        t.font = f ?? AssetLibrary.GetMainFont();
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        // 关键：必须给垂直方向 Overflow。
        // Unity Text 默认 verticalOverflow=Truncate，而思源黑体的行高 ≈ 字号 × 1.45，
        // 只要 Rect 高度小于行高，整行文字会被直接丢弃（不是被裁一半，是彻底不渲染）。
        // 之前 Boss 名 / 分数 / 难度 / Lv / EXP 等小字号 + 紧贴高度的小框就是这么"消失"的。
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }
}
