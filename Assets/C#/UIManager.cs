using UnityEngine;
using UnityEngine.UI;

/// <summary>UI 小工具：给 Text 套中文字体、给 Image 按名字加载精灵。</summary>
public static class UITheme
{
    public static void Font(Text t, int size)
    {
        if (t == null) return;
        var f = AssetLibrary.GetMainFont();
        if (f != null) t.font = f;
        t.fontSize = size;
    }

    /// <summary>
    /// 保证 Canvas 带 UI 自适应缩放。没有它时 UI 按裸像素摆放，
    /// 窗口小于设计尺寸会把边缘的内容（例如第三行关卡卡片）裁掉。
    /// </summary>
    public static void EnsureScaler(Canvas canvas)
    {
        if (canvas == null) return;
        var scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        scaler.referenceResolution = new Vector2(GameConfig.ScreenWidth, GameConfig.ScreenHeight);
    }

    /// <summary>按 Resources/Sprites 下的名字给 Image 赋精灵。stretch=false 时保持比例。</summary>
    public static void Sprite(Image img, string relativePath, bool stretch = true)
    {
        if (img == null) return;
        var s = AssetLibrary.GetSprite(relativePath);
        if (s == null) return;
        img.sprite = s;
        img.type = Image.Type.Simple;
        img.preserveAspect = !stretch;
    }

    /// <summary>
    /// 全屏背景图：保持原始比例铺满（cover），不再因为画布变宽/变高而把美术图拉变形。
    /// </summary>
    public static void Background(Image img, string relativePath)
    {
        if (img == null) return;
        Sprite(img, relativePath);
        if (img.sprite == null) return;

        var cover = img.GetComponent<BackgroundCover>();
        if (cover == null) cover = img.gameObject.AddComponent<BackgroundCover>();
        cover.Apply();
    }

    /// <summary>
    /// 纯图标按钮：清掉自动生成的文字标签，并按原图比例压成正方形，
    /// 避免「图标 + 文字叠在一起」以及图标被拉扁（例如 850×850 的返回图标塞进 120×50 的框里）。
    /// </summary>
    public static void IconButton(Button btn, string relativePath, float size)
    {
        if (btn == null) return;

        var img = btn.GetComponent<Image>();
        if (img != null)
        {
            Sprite(img, relativePath, false);
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
            img.color = Color.white;
        }

        // 隐藏按钮下的文字标签（只留图标）
        var texts = btn.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            texts[i].text = "";
            if (texts[i].gameObject != btn.gameObject) texts[i].gameObject.SetActive(false);
        }

        var rt = (RectTransform)btn.transform;
        rt.sizeDelta = new Vector2(size, size);
    }
}

/// <summary>
/// 界面总控：管理各面板的显隐，并协调“登录→菜单→规则/排行榜→游玩→结算→致谢”的流程。
/// 各面板脚本通过调用这里的公开方法来切换状态。
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("把场景里各面板对象拖到这里")]
    public GameObject loginPanel;
    public GameObject menuPanel;
    public GameObject rankingPanel;
    public GameObject rulesPanel;
    public GameObject gameOverPanel;
    public GameObject creditsPanel;
    public GameObject levelSelectPanel;
    public GameObject victoryPanel;

    [Header("启动时显示的面板")]
    public GameState startState = GameState.Login;

    public GameState Current { get; private set; } = GameState.Login;

    void Awake()
    {
        Instance = this;

        // 场景 Canvas：补齐 UI 自适应缩放，避免窗口偏小时界面被裁切
        var canvases = GetComponentsInChildren<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
            UITheme.EnsureScaler(canvases[i]);
    }

    void Start()
    {
        ShowState(startState);
    }

    // ---------------------------------------------------------------
    public void ShowState(GameState s)
    {
        Current = s;

        SetActive(loginPanel, s == GameState.Login);
        SetActive(menuPanel, s == GameState.Menu);
        SetActive(rankingPanel, s == GameState.Ranking);
        SetActive(rulesPanel, s == GameState.Rules);
        SetActive(levelSelectPanel, s == GameState.LevelSelect);
        SetActive(gameOverPanel, s == GameState.GameOver);
        SetActive(victoryPanel, s == GameState.Victory);
        SetActive(creditsPanel, s == GameState.Credits);

        if (s == GameState.Login) Refresh<LoginPanel>(loginPanel);
        if (s == GameState.Menu) Refresh<MenuPanel>(menuPanel);
        if (s == GameState.Ranking) Refresh<RankingPanel>(rankingPanel);
        if (s == GameState.LevelSelect) Refresh<LevelSelectPanel>(levelSelectPanel);
        if (s == GameState.GameOver) Refresh<GameOverPanel>(gameOverPanel);
        if (s == GameState.Victory) Refresh<VictoryPanel>(victoryPanel);
        if (s == GameState.Credits) Refresh<CreditsPanel>(creditsPanel);
    }

    static void SetActive(GameObject go, bool on)
    {
        if (go != null) go.SetActive(on);
    }

    static void Refresh<T>(GameObject go) where T : MonoBehaviour
    {
        if (go == null) return;
        var c = go.GetComponent<T>();
        if (c == null) c = go.GetComponentInChildren<T>(true);
        if (c != null) c.SendMessage("RefreshUI", SendMessageOptions.DontRequireReceiver);
    }

    // ------------------- 供各面板调用 -------------------
    /// <summary>登录成功：写入存档并进入主菜单。</summary>
    public void ConfirmLogin(string name)
    {
        SaveSystem.RegisterPlayer(name);
        SaveSystem.Data.currentPlayer = name;
        SaveSystem.Save();
        ShowState(GameState.Menu);
    }

    /// <summary>“继续使用 XXX”快速登录。</summary>
    public void ContinueExisting()
    {
        if (string.IsNullOrEmpty(SaveSystem.Data.currentPlayer)) return;
        ShowState(GameState.Menu);
    }

    public void OpenRanking() => ShowState(GameState.Ranking);
    public void CloseRanking() => ShowState(GameState.Login);

    public void OpenRules() => ShowState(GameState.Rules);
    public void CloseRules() => ShowState(GameState.Menu);

    /// <summary>切换玩家：回到登录界面。</summary>
    public void SwitchPlayer() => ShowState(GameState.Login);

    /// <summary>菜单选完角色后：保存初始角色，进入关卡选择。</summary>
    public void StartGame(Element startElement)
    {
        SaveSystem.SetStartElement(startElement);
        ShowState(GameState.LevelSelect);
    }

    /// <summary>选关面板点击某一关。</summary>
    public void StartLevel(LevelConfig.Id id)
    {
        if (LevelManager.Instance == null)
        {
            Debug.LogError("[UIManager] 找不到 LevelManager。");
            return;
        }
        ShowState(GameState.Playing);
        LevelManager.Instance.StartLevel(id);
    }

    public void ReturnFromLevelSelect() => ShowState(GameState.Menu);

    /// <summary>
    /// 胜利结算点"返回选关"：回到关卡选择界面，由玩家自己决定下一关，
    /// 不再自动/一键跳进下一关（会看不到刚解锁的新关卡和结算数据）。
    /// </summary>
    public void ReturnFromVictoryToLevelSelect()
    {
        if (LevelManager.Instance != null) LevelManager.Instance.StopLevel();
        ShowState(GameState.LevelSelect);
    }

    public void ReturnFromVictory()
    {
        if (LevelManager.Instance != null) LevelManager.Instance.StopLevel();
        ShowState(GameState.Menu);
    }

    /// <summary>Boss 死亡后由 LevelManager 调用。</summary>
    public void NotifyVictory()
    {
        if (GameManager.Instance != null)
        {
            string cur = SaveSystem.Data.currentPlayer;
            if (!string.IsNullOrEmpty(cur))
                SaveSystem.UpdateHighScore(cur, GameManager.Instance.score, out _);
        }
        ShowState(GameState.Victory);
    }

    /// <summary>结算界面点“返回主菜单”。</summary>
    public void ReturnFromGameOver()
    {
        if (GameManager.Instance != null) GameManager.Instance.StopGame();
        ShowState(GameState.Menu);
    }

    /// <summary>玩家死亡时由 GameManager 调用。</summary>
    public void NotifyGameOver() => ShowState(GameState.GameOver);

    /// <summary>结算停留一段时间后切到致谢页。</summary>
    public void ShowCredits() => ShowState(GameState.Credits);

    /// <summary>游玩中按 ESC / 暂停返回主菜单。</summary>
    public void QuitToMenu()
    {
        if (GameManager.Instance != null) GameManager.Instance.StopGame();
        ShowState(GameState.Menu);
    }
}