using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 登录界面。对应 pygame 的 draw_login + handle_login_events + do_login。
/// </summary>
public class LoginPanel : MonoBehaviour
{
    [Header("背景 / 标题")]
    public Image backgroundImage;
    public Text titleText;
    public Text subtitleText;

    [Header("欢迎 / 输入框")]
    public Text welcomeText;
    public InputField nameInput;
    public Text switchHintText;

    [Header("快速登录按钮")]
    public Button quickLoginButton;
    public Text quickLoginLabel;

    [Header("按钮")]
    public Button loginButton;
    public Text loginLabel;
    public Button rankingButton;

    [Header("提示 / 历史玩家 / 版本")]
    public Text messageText;
    public Text historyText;
    public Text versionText;

    string message = "";
    float messageTimer;

    // ---------------------------------------------------------------
    void Awake()
    {
        UITheme.Background(backgroundImage, "界面/主页面背景");
        UITheme.Font(titleText, 64);
        UITheme.Font(subtitleText, 26);
        UITheme.Font(welcomeText, 30);
        UITheme.Font(switchHintText, 18);
        UITheme.Font(quickLoginLabel, 26);
        UITheme.Font(loginLabel, 26);
        UITheme.Font(rankingButton != null ? rankingButton.GetComponentInChildren<Text>() : null, 24);
        UITheme.Font(messageText, 20);
        UITheme.Font(historyText, 18);
        UITheme.Font(versionText, 14);

        if (titleText != null) titleText.color = new Color32(255, 220, 50, 255);
        if (subtitleText != null) subtitleText.color = new Color32(100, 200, 255, 255);
        if (switchHintText != null) switchHintText.color = new Color32(150, 150, 150, 255);
        if (messageText != null) messageText.text = "";
        if (versionText != null)
        {
            versionText.text = "Version 1.0 | Made with Unity";
            versionText.color = new Color32(60, 60, 80, 255);
        }
        if (nameInput != null) nameInput.characterLimit = 12;
    }

    void Start()
    {
        if (loginButton != null) loginButton.onClick.AddListener(OnLoginClicked);
        if (rankingButton != null) rankingButton.onClick.AddListener(() => UIManager.Instance.OpenRanking());
        if (quickLoginButton != null) quickLoginButton.onClick.AddListener(() => UIManager.Instance.ContinueExisting());
        if (nameInput != null) nameInput.onValueChanged.AddListener(_ => RefreshUI());
        RefreshUI();
    }

    void Update()
    {
        if (messageTimer > 0f)
        {
            messageTimer -= Time.deltaTime;
            if (messageTimer <= 0f) { message = ""; if (messageText) messageText.text = ""; }
        }

        if (nameInput == null) return;
        if (nameInput.isFocused && Input.GetKeyDown(KeyCode.Return))
        {
            OnLoginClicked();
        }
        else if (!nameInput.isFocused && Input.GetKeyDown(KeyCode.Return))
        {
            // 输入框未激活：有存档玩家且未输入时，回车直接进菜单
            if (!string.IsNullOrEmpty(SaveSystem.Data.currentPlayer) && string.IsNullOrEmpty(nameInput.text))
                UIManager.Instance.ContinueExisting();
        }
    }

    // ---------------------------------------------------------------
    void RefreshUI()
    {
        string cur = SaveSystem.Data.currentPlayer;
        bool typing = nameInput != null && !string.IsNullOrEmpty(nameInput.text);

        if (welcomeText != null)
            welcomeText.text = string.IsNullOrEmpty(cur) ? "请输入您的名字" : "欢迎回来, " + cur;

        bool showQuick = !string.IsNullOrEmpty(cur) && !typing;
        if (quickLoginButton != null) quickLoginButton.gameObject.SetActive(showQuick);
        if (showQuick && quickLoginLabel != null) quickLoginLabel.text = "继续使用 " + cur;
        if (switchHintText != null) switchHintText.gameObject.SetActive(showQuick);

        // 历史玩家（仅未登录时显示，最多 5 个）
        if (historyText != null)
        {
            var all = SaveSystem.Data.totalPlayers;
            if (string.IsNullOrEmpty(cur) && all != null && all.Count > 0)
            {
                int n = Mathf.Min(5, all.Count);
                string names = string.Join(", ", all.GetRange(0, n).ToArray());
                if (all.Count > 5) names += "...";
                historyText.text = "历史玩家: " + names;
                historyText.gameObject.SetActive(true);
            }
            else
            {
                historyText.gameObject.SetActive(false);
            }
        }

        // 登录按钮随输入状态变色
        bool hasText = nameInput != null && !string.IsNullOrEmpty(nameInput.text.Trim());
        if (loginLabel != null) loginLabel.color = hasText ? Color.white : new Color32(150, 150, 150, 255);
        if (loginButton != null)
        {
            var img = loginButton.GetComponent<Image>();
            if (img != null) img.color = hasText ? new Color32(50, 200, 50, 255) : new Color32(60, 100, 60, 255);
        }
    }

    void OnLoginClicked()
    {
        string name = nameInput != null ? nameInput.text.Trim() : "";

        if (string.IsNullOrEmpty(name))
        {
            string cur = SaveSystem.Data.currentPlayer;
            if (!string.IsNullOrEmpty(cur)) { UIManager.Instance.ContinueExisting(); return; }
            ShowMessage("名字不能为空!");
            return;
        }
        if (name.Length < 2) { ShowMessage("名字至少2个字符!"); return; }

        if (nameInput != null) nameInput.text = "";
        UIManager.Instance.ConfirmLogin(name);
    }

    void ShowMessage(string m)
    {
        message = m;
        messageTimer = 2f;
        if (messageText != null)
        {
            messageText.text = m;
            bool good = m.Contains("成功") || m.Contains("欢迎");
            messageText.color = good ? new Color32(100, 255, 100, 255) : new Color32(255, 100, 100, 255);
        }
    }
}