using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 排行榜（前 10 名）。对应 pygame 的 draw_ranking + get_top_scores(10)。
/// 需要：一个 rowPrefab（带 RankingRow 组件）+ 一个 rowContainer（空物体）用来摆行。
/// </summary>
public class RankingPanel : MonoBehaviour
{
    [Header("背景 / 标题 / 返回")]
    public Image backgroundImage;
    public Text titleText;
    public Button backButton;
    public Text emptyText;

    [Header("行模板与容器")]
    public RankingRow rowPrefab;
    public RectTransform rowContainer;
    public float rowHeight = 45f;
    public float rowSpacing = 50f;
    public float rowOffsetY = -20f;

    // ---------------------------------------------------------------
    void Awake()
    {
        UITheme.Background(backgroundImage, "界面/主页面背景");
        UITheme.Font(titleText, 64);
        UITheme.Font(emptyText, 30);
        if (titleText != null) { titleText.text = "排行榜"; titleText.color = new Color32(255, 220, 50, 255); }
        if (emptyText != null) { emptyText.text = "暂无排行数据"; emptyText.color = new Color32(150, 150, 150, 255); }
        // 只留返回图标（去掉与图标重叠的“返回”文字，并按正方形等比显示）
        UITheme.IconButton(backButton, "界面/返回", 64f);
        if (backButton != null)
        {
            var brt = (RectTransform)backButton.transform;
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.zero;
            brt.pivot = Vector2.zero;
            brt.anchoredPosition = new Vector2(28f, 24f);
        }
    }

    void Start()
    {
        if (backButton != null) backButton.onClick.AddListener(() => UIManager.Instance.CloseRanking());
    }

    // ---------------------------------------------------------------
    void RefreshUI()
    {
        if (rowContainer == null || rowPrefab == null) return;

        // 清空旧行
        for (int i = rowContainer.childCount - 1; i >= 0; i--)
            Destroy(rowContainer.GetChild(i).gameObject);

        var top = SaveSystem.GetTopScores(10);

        if (emptyText != null) emptyText.gameObject.SetActive(top.Count == 0);

        for (int i = 0; i < top.Count; i++)
        {
            var row = Instantiate(rowPrefab, rowContainer);
            row.gameObject.SetActive(true);

            var rt = row.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(560f, rowHeight);
            rt.anchoredPosition = new Vector2(0f, rowOffsetY - i * rowSpacing);

            if (row.background != null)
                row.background.color = (i % 2 == 0) ? new Color32(40, 40, 60, 180) : new Color32(30, 30, 50, 180);

            // 前三名用金银铜色（原版是奖牌 emoji，这里用颜色区分以保证中文正常显示）
            Color32 medal;
            if (i == 0) medal = new Color32(255, 215, 0, 255);
            else if (i == 1) medal = new Color32(192, 192, 192, 255);
            else if (i == 2) medal = new Color32(205, 127, 50, 255);
            else medal = new Color32(255, 255, 255, 255);

            if (row.rankText != null)
            {
                UITheme.Font(row.rankText, 22);
                row.rankText.text = "#" + (i + 1);
                row.rankText.color = medal;
            }
            if (row.nameText != null)
            {
                UITheme.Font(row.nameText, 22);
                row.nameText.text = top[i].name;
                row.nameText.color = Color.white;
            }
            if (row.scoreText != null)
            {
                UITheme.Font(row.scoreText, 22);
                row.scoreText.text = top[i].score.ToString("N0");
                row.scoreText.color = new Color32(255, 220, 50, 255);
            }
        }
    }
}