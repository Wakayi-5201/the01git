using UnityEngine;
using UnityEngine.UI;

/// <summary>结算页里的一个属性图标（已解锁=彩色+名字，未解锁=暗色+?）。</summary>
[System.Serializable]
public class ElementIcon
{
    public Image icon;
    public Text label;
}

/// <summary>
/// 结算界面。对应 pygame 的 draw_gameover（玩家名/最终得分/新纪录/等级/最高连击/解锁属性）。
/// 停留 5 秒后自动切到“特别感谢”。按任意键或点按钮返回主菜单。
/// </summary>
public class GameOverPanel : MonoBehaviour
{
    [Header("背景")]
    public Image backgroundImage;
    public Text titleText;

    [Header("玩家 / 得分")]
    public Text playerText;
    public Text scoreText;
    public Text newRecordText;
    public Text highLabelText;
    public Text highScoreText;

    [Header("统计")]
    public Text levelLabelText;
    public Text levelText;
    public Text comboLabelText;
    public Text comboText;
    public Text unlockedLabelText;
    public Text unlockedText;

    [Header("属性图标（顺序：火 冰 水 雷 岩）")]
    public ElementIcon[] elementIcons;

    [Header("返回")]
    public Button backButton;
    public Text hintText;

    const float CreditsDelay = 5f;
    float timer;

    // ---------------------------------------------------------------
    void Awake()
    {
        UITheme.Background(backgroundImage, "界面/主页面背景");
        UITheme.Font(titleText, 64);
        UITheme.Font(playerText, 26);
        UITheme.Font(scoreText, 48);
        UITheme.Font(newRecordText, 30);
        UITheme.Font(highLabelText, 20);
        UITheme.Font(highScoreText, 26);
        UITheme.Font(levelLabelText, 20);
        UITheme.Font(levelText, 28);
        UITheme.Font(comboLabelText, 20);
        UITheme.Font(comboText, 28);
        UITheme.Font(unlockedLabelText, 20);
        UITheme.Font(unlockedText, 28);
        UITheme.Font(hintText, 20);

        if (titleText != null) { titleText.text = "游戏结束"; titleText.color = new Color32(255, 80, 80, 255); }
        if (playerText != null) playerText.color = new Color32(60, 200, 255, 255);
        if (scoreText != null) scoreText.color = new Color32(255, 220, 50, 255);
        if (newRecordText != null) { newRecordText.text = "新纪录"; newRecordText.color = new Color32(255, 215, 0, 255); }
        if (highLabelText != null) { highLabelText.text = "历史最高"; highLabelText.color = new Color32(150, 150, 150, 255); }
        if (highScoreText != null) highScoreText.color = new Color32(255, 220, 50, 255);
        foreach (var label in new[] { levelLabelText, comboLabelText, unlockedLabelText })
            if (label != null) label.color = new Color32(150, 150, 150, 255);
        if (levelText != null) levelText.color = new Color32(100, 200, 100, 255);
        if (comboText != null) comboText.color = new Color32(255, 150, 30, 255);
        if (unlockedText != null) unlockedText.color = new Color32(60, 200, 255, 255);
        if (hintText != null) { hintText.text = "按任意键或点击按钮返回..."; hintText.color = new Color32(120, 120, 150, 255); }

        if (levelLabelText != null && string.IsNullOrEmpty(levelLabelText.text)) levelLabelText.text = "等级";
        if (comboLabelText != null && string.IsNullOrEmpty(comboLabelText.text)) comboLabelText.text = "最高连击";
        if (unlockedLabelText != null && string.IsNullOrEmpty(unlockedLabelText.text)) unlockedLabelText.text = "解锁属性";

        // 只留返回图标（去掉与图标重叠的“返回”文字，并按正方形等比显示）
        UITheme.IconButton(backButton, "界面/返回", 64f);
        if (backButton != null)
        {
            var brt = (RectTransform)backButton.transform;
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.zero;
            brt.pivot = Vector2.zero;
            brt.anchoredPosition = new Vector2(28f, 20f);
        }
    }

    void Start()
    {
        if (backButton != null) backButton.onClick.AddListener(() => UIManager.Instance.ReturnFromGameOver());
    }

    void OnEnable()
    {
        timer = 0f;
    }

    void Update()
    {
        timer += Time.deltaTime;

        // 稍作延迟后再接受“任意键返回”，避免死亡瞬间的按键顺延触发
        if (timer > 0.6f && (Input.anyKeyDown))
        {
            UIManager.Instance.ReturnFromGameOver();
            return;
        }

        if (timer >= CreditsDelay)
            UIManager.Instance.ShowCredits();
    }

    // ---------------------------------------------------------------
    void RefreshUI()
    {
        var gm = GameManager.Instance;
        var p = gm != null ? gm.player : null;
        string cur = SaveSystem.Data.currentPlayer;

        if (playerText != null) playerText.text = cur;
        if (scoreText != null) scoreText.text = (gm != null ? gm.score : 0).ToString("N0");

        bool isNew = gm != null && gm.isNewHighScore;
        if (newRecordText != null) newRecordText.gameObject.SetActive(isNew);
        if (highLabelText != null) highLabelText.gameObject.SetActive(!isNew);
        if (highScoreText != null)
        {
            highScoreText.gameObject.SetActive(!isNew);
            highScoreText.text = SaveSystem.GetHighScore(cur).ToString("N0");
        }

        if (levelText != null) levelText.text = (p != null ? p.level : 1).ToString();
        if (comboText != null) comboText.text = (gm != null ? gm.maxCombo : 0).ToString();
        if (unlockedText != null)
            unlockedText.text = (p != null ? p.unlockedElements.Count : 1) + "/5";

        if (elementIcons != null)
        {
            for (int i = 0; i < elementIcons.Length; i++)
            {
                if (i >= ElementDatabase.AllElements.Length) break;
                var icon = elementIcons[i];
                if (icon == null) continue;

                var el = ElementDatabase.AllElements[i];
                bool unlocked = p != null && p.unlockedElements.Contains(el);

                if (icon.icon != null)
                    icon.icon.color = unlocked ? ElementDatabase.GetColor(el) : new Color32(40, 40, 40, 255);
                if (icon.label != null)
                {
                    UITheme.Font(icon.label, 16);
                    icon.label.text = unlocked ? ElementDatabase.GetName(el) : "?";
                    icon.label.color = unlocked ? Color.white : new Color32(60, 60, 60, 255);
                }
            }
        }
    }
}