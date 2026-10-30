using UnityEngine;
using UnityEngine.UI;

/// <summary>菜单里的一个角色头像（按钮 + 图 + 选中高亮 + 名字）。</summary>
[System.Serializable]
public class ElementAvatar
{
    public Button button;
    public Image image;
    public GameObject highlight;   // 选中时显示的金色边框对象
    public Text nameText;
}

/// <summary>
/// 主菜单：显示玩家/最高分、选择初始角色（5 元素）、开始/规则/切换玩家。
/// 对应 pygame 的 draw_menu + handle_menu_events。
/// avatars 按顺序填：火、冰、水、雷、岩。
/// </summary>
public class MenuPanel : MonoBehaviour
{
    [Header("背景 / 标题")]
    public Image backgroundImage;
    public Image titleImage;          // 元素战机.png（可留空，留空则用标题文字）
    public Text titleText;

    [Header("玩家信息")]
    public Text playerText;
    public Text highScoreText;

    [Header("角色头像（顺序：火 冰 水 雷 岩）")]
    public ElementAvatar[] avatars;

    [Header("按钮")]
    public Button startButton;
    public Text startLabel;
    [Tooltip("游戏规则按钮，点击弹出规则面板")]
    public Button rulesButton;
    [Tooltip("规则触发器上的呼吸微光（可选）：缓慢明暗闪烁，提示玩家此处可点击")]
    public Image rulesGlow;
    public Button switchButton;
    public Text switchLabel;

    public Element selectedElement = Element.Fire;

    void OnEnable()
    {
        selectedElement = SaveSystem.GetStartElement();
    }

    void Update()
    {
        // 呼吸微光：提醒玩家背景上的“游戏规则”可以点击
        if (rulesGlow != null)
        {
            float a = 0.1f + 0.1f * Mathf.Sin(Time.unscaledTime * 2.5f);
            rulesGlow.color = new Color(1f, 1f, 1f, Mathf.Max(0f, a));
        }
    }

    // ---------------------------------------------------------------
    void Awake()
    {
        UITheme.Background(backgroundImage, "界面/主页面背景");
        if (titleImage != null) UITheme.Sprite(titleImage, "界面/元素战机", false);
        UITheme.Font(titleText, 64);
        UITheme.Font(playerText, 24);
        UITheme.Font(highScoreText, 24);
        UITheme.Font(startLabel, 30);
        UITheme.Font(switchLabel, 22);

        if (titleText != null) titleText.color = new Color32(255, 220, 50, 255);
        if (playerText != null) playerText.color = new Color32(60, 200, 255, 255);
        if (highScoreText != null) highScoreText.color = new Color32(255, 220, 50, 255);

        if (startButton != null) UITheme.Sprite(startButton.GetComponent<Image>(), "界面/按钮条");
        if (rulesButton != null) UITheme.Sprite(rulesButton.GetComponent<Image>(), "界面/游戏规则");

        // 头像按固定顺序（火冰水雷岩）自动贴图
        if (avatars != null)
        {
            for (int i = 0; i < avatars.Length; i++)
            {
                if (i >= ElementDatabase.AllElements.Length) break;
                var a = avatars[i];
                if (a == null) continue;
                var el = ElementDatabase.AllElements[i];

                if (a.image != null) UITheme.Sprite(a.image, "头像/" + ElementDatabase.AvatarFile(el));
                if (a.nameText != null)
                {
                    UITheme.Font(a.nameText, 16);
                    a.nameText.text = ElementDatabase.GetName(el);
                }
                if (a.button != null)
                {
                    int idx = i;
                    a.button.onClick.AddListener(() =>
                    {
                        // 未解锁的属性不能选
                        if (!SaveSystem.IsElementUnlocked(ElementDatabase.AllElements[idx])) return;
                        selectedElement = ElementDatabase.AllElements[idx];
                        RefreshUI();
                    });
                }
            }
        }
    }

    void Start()
    {
        if (startButton != null) startButton.onClick.AddListener(OnStartClicked);
        if (rulesButton != null) rulesButton.onClick.AddListener(() => UIManager.Instance.OpenRules());
        if (switchButton != null) switchButton.onClick.AddListener(() => UIManager.Instance.SwitchPlayer());

        BuildResetButton();
        BuildCodexButton();

        if (playerText != null) playerText.text = "";
        RefreshUI();
    }

    /// <summary>
    /// 动态生成一个"重置游戏进度"小按钮贴在菜单右下角。
    /// 用途：历史存档里可能已经把五个属性全解锁了（脏数据），
    /// 点一下即可回到"只有火属性、只有第 1 关"的初始锁定状态。
    /// 做成动态创建，避免还要去 Inspector 里拖引用。
    /// </summary>
    void BuildResetButton()
    {
        var host = transform.Find("ResetProgressBtn");
        if (host != null) return;   // 已建过

        var canvas = GetComponentInChildren<Canvas>();
        if (canvas == null) return;
        UITheme.EnsureScaler(canvas);

        var go = new GameObject("ResetProgressBtn", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-18f, 16f);
        rt.sizeDelta = new Vector2(170f, 42f);

        var img = go.AddComponent<Image>();
        img.color = new Color(0.35f, 0.18f, 0.18f, 0.9f);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() =>
        {
            SaveSystem.ResetProgress();
            selectedElement = Element.Fire;
            RefreshUI();
        });

        var font = AssetLibrary.GetMainFont();
        var label = new GameObject("Label", typeof(RectTransform));
        label.transform.SetParent(go.transform, false);
        var t = label.AddComponent<Text>();
        t.text = "重置游戏进度";
        t.font = font;
        t.fontSize = 18;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = new Color32(255, 200, 200, 255);
        t.raycastTarget = false;
        var lrt = label.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
    }

    /// <summary>
    /// 动态生成"图鉴"按钮贴在菜单左下角（与右下角的重置按钮对称）。
    /// 点击打开图鉴面板（敌人击杀数 / 道具拾取数）。
    /// </summary>
    void BuildCodexButton()
    {
        var host = transform.Find("CodexBtn");
        if (host != null) return;   // 已建过

        var canvas = GetComponentInChildren<Canvas>();
        if (canvas == null) return;
        UITheme.EnsureScaler(canvas);

        var go = new GameObject("CodexBtn", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(18f, 16f);
        rt.sizeDelta = new Vector2(170f, 42f);

        var img = go.AddComponent<Image>();
        img.color = new Color(0.18f, 0.28f, 0.4f, 0.9f);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => CollectionPanel.Show());

        var font = AssetLibrary.GetMainFont();
        var label = new GameObject("Label", typeof(RectTransform));
        label.transform.SetParent(go.transform, false);
        var t = label.AddComponent<Text>();
        t.text = "图 鉴";
        t.font = font;
        t.fontSize = 18;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = new Color32(190, 225, 255, 255);
        t.raycastTarget = false;
        var lrt = label.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
    }

    void OnStartClicked()
    {
        if (string.IsNullOrEmpty(SaveSystem.Data.currentPlayer))
        {
            UIManager.Instance.SwitchPlayer();
            return;
        }
        UIManager.Instance.StartGame(selectedElement);
    }

    // ---------------------------------------------------------------
    void RefreshUI()
    {
        string cur = SaveSystem.Data.currentPlayer;

        if (playerText != null)
            playerText.text = string.IsNullOrEmpty(cur) ? "" : "玩家: " + cur;
        if (highScoreText != null)
            highScoreText.text = string.IsNullOrEmpty(cur)
                ? ""
                : "最高分: " + SaveSystem.GetHighScore(cur).ToString("N0");

        if (avatars != null)
        {
            for (int i = 0; i < avatars.Length; i++)
            {
                if (i >= ElementDatabase.AllElements.Length) break;
                var a = avatars[i];
                if (a == null) continue;
                var el = ElementDatabase.AllElements[i];
                bool unlocked = SaveSystem.IsElementUnlocked(el);
                bool sel = el == selectedElement && unlocked;

                if (a.image != null)
                    a.image.color = unlocked ? Color.white : new Color(0.4f, 0.4f, 0.4f, 1f);
                if (a.button != null) a.button.interactable = unlocked;
                if (a.highlight != null) a.highlight.SetActive(sel);
                if (a.nameText != null)
                {
                    a.nameText.color = sel ? new Color32(255, 220, 50, 255)
                        : unlocked ? new Color32(180, 180, 180, 255)
                        : new Color32(100, 100, 100, 255);
                    a.nameText.text = unlocked ? ElementDatabase.GetName(el) : ElementDatabase.GetName(el) + " 🔒";
                }
            }
        }
    }
}