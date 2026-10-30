using System.Collections;
using UnityEngine;

/// <summary>
/// 关卡管理器：关卡状态机 + timeScale 唯一管理者。
/// 流程：倒计时 → 波次1 → 波次2 → 波次3 → Boss警告(freeze 2.5s) → Boss战 → 胜利/失败
/// timeScale 规则：正常=1，Boss警告=0，升级卡片=0.4。
/// </summary>
public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance;

    public MapRenderer mapRenderer;
    public WaveSpawner waveSpawner;
    public HUD hud;
    public CameraFollow cameraFollow;

    LevelConfig.LevelData currentLevel;
    public LevelConfig.Id currentLevelId;
    BossController boss;

    Coroutine bossWarnRoutine;
    Coroutine introRoutine;
    bool frozen;
    bool paused;
    int slowdownRequests;   // 升级卡片的减速请求计数

    void Awake() => Instance = this;

    /// <summary>当前关卡的活动区（地图内沿）；无关卡时退化为整张地图。</summary>
    public static Rect CurrentPlayArea =>
        Instance != null && Instance.currentLevel != null
            ? Instance.currentLevel.playArea
            : new Rect(0f, 0f, GameConfig.MapWidth, GameConfig.MapHeight);

    void Update()
    {
        // 暂停键（原版为 P 键，timeScale=0 时 Update 仍会执行，所以这里能正常收输入）
        if (Input.GetKeyDown(KeyCode.P)) TogglePause();
    }

    // ---------------------------------------------------------------
    /// <summary>从 UIManager/菜单调用，开始某一关。</summary>
    public void StartLevel(LevelConfig.Id id)
    {
        currentLevelId = id;
        currentLevel = LevelConfig.Get(id);

        // 地图
        if (mapRenderer != null)
        {
            mapRenderer.SetMap(currentLevel.mapFile);
            mapRenderer.transform.position = new Vector3(GameConfig.MapWidth * 0.5f, GameConfig.MapHeight * 0.5f, 0f);
        }

        // 玩家
        var gm = GameManager.Instance;
        gm.boss = null;
        gm.StartGame(SaveSystem.GetStartElement(), currentLevel.id);

        // 相机：把跟随目标指认到玩家（场景里没手拖也不怕），随后播开场动画
        if (cameraFollow != null)
        {
            cameraFollow.SetWorldBounds(GameConfig.MapWidth, GameConfig.MapHeight);
            if (gm.player != null) cameraFollow.SetTarget(gm.player.transform);
        }

        // HUD：先建立关卡信息，真正显示等开场动画结束
        if (hud != null)
        {
            hud.SetLevel(currentLevel);
            hud.gameObject.SetActive(false);
        }

        // 波次
        if (waveSpawner != null)
        {
            waveSpawner.OnAllWavesCleared -= OnAllWavesCleared;
            waveSpawner.OnAllWavesCleared += OnAllWavesCleared;
            waveSpawner.OnWaveStarted -= OnWaveStarted;
            waveSpawner.OnWaveStarted += OnWaveStarted;
            waveSpawner.StartLevel(currentLevel);
        }

        // 彩蛋：哆啦A梦固定站在地图右下角（不攻击，玩家靠近掉 3 个道具，每关一次）
        DoraemonEgg.Create();

        paused = false;
        slowdownRequests = 0;

        // 开场：冻结逻辑 → 全图展示 → 拉近到玩家 → 解冻开局
        BeginIntro();
    }

    public void StopLevel()
    {
        if (bossWarnRoutine != null) StopCoroutine(bossWarnRoutine);
        bossWarnRoutine = null;
        if (introRoutine != null) StopCoroutine(introRoutine);
        introRoutine = null;
        if (waveSpawner != null) waveSpawner.Stop();
        DoraemonEgg.Clear();   // 彩蛋随关卡结束销毁
        if (hud != null)
        {
            hud.SetPaused(false);
            hud.gameObject.SetActive(false);
        }
        frozen = false;
        paused = false;
        slowdownRequests = 0;
        ApplyTimeScale();
    }

    // ---------------------------------------------------------------
    // 开场镜头：全图 → 拉近到玩家 → 解冻开局
    void BeginIntro()
    {
        frozen = true;
        ApplyTimeScale();

        var gm = GameManager.Instance;
        bool canPlay = cameraFollow != null && cameraFollow.playIntro && gm != null && gm.player != null;

        if (!canPlay)
        {
            if (cameraFollow != null && gm != null && gm.player != null) cameraFollow.SnapToTarget();
            EndIntro();
            return;
        }

        if (introRoutine != null) StopCoroutine(introRoutine);
        introRoutine = StartCoroutine(IntroRoutine(cameraFollow.PlayIntro(gm.player.transform)));
    }

    System.Collections.IEnumerator IntroRoutine(System.Collections.IEnumerator inner)
    {
        yield return inner;
        introRoutine = null;
        EndIntro();
    }

    void EndIntro()
    {
        frozen = false;
        ApplyTimeScale();

        if (hud != null)
        {
            hud.gameObject.SetActive(true);
            hud.SetPaused(false);
            hud.ShowWaveHint("第 1 波");
        }

        // 开局提示：本关继承了上次通关的成长，让"上一关没白打"这件事是可见的
        var p = GameManager.Instance != null ? GameManager.Instance.player : null;
        if (p != null && p.inheritedGrowth)
            GameManager.Instance.FloatingTextAt(
                (Vector2)p.transform.position + new Vector2(0f, -70f),
                "继承成长  Lv." + p.level + "   HP " + p.maxHp +
                "   攻击 " + p.damage + "   弹道 " + p.bulletRows,
                GameConfig.Yellow, true);
    }

    // ---------------------------------------------------------------
    // 暂停（P 键 / 暂停按钮）
    public void TogglePause()
    {
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.state != GameState.Playing) return;
        if (introRoutine != null) return;   // 开场动画期间不允许暂停

        paused = !paused;
        ApplyTimeScale();
        if (hud != null) hud.SetPaused(paused);
    }

    public bool IsPaused => paused;

    // ---------------------------------------------------------------
    void OnWaveStarted(int idx)
    {
        if (hud != null) hud.ShowWaveHint("第 " + (idx + 1) + " 波");
    }

    void OnAllWavesCleared()
    {
        bossWarnRoutine = StartCoroutine(BossWarningRoutine());
    }

    IEnumerator BossWarningRoutine()
    {
        SetFrozen(true);
        GameManager.Instance.PlaySfx("boss_warning");
        var overlay = new GameObject("BossWarning").AddComponent<BossWarningOverlay>();
        overlay.Init();

        yield return new WaitForSecondsRealtime(BossWarningOverlay.Duration);

        // 二次校验：仍在关卡内才生成 Boss（避免切场景后生成）
        if (this != null && GameManager.Instance.state == GameState.Playing)
        {
            SpawnBoss();
        }
        SetFrozen(false);
    }

    void SpawnBoss()
    {
        // Boss 强度跟随玩家当时的成长：血量 = 玩家 maxHp × bossHpMul，
        // 碰撞伤害 = 玩家 maxHp × bossDamagePct，并保证不低于关卡配置的保底值。
        // 这样玩家越肥，Boss 也越硬，不会到后期被一刀秒掉没了压迫感。
        int hp = currentLevel.bossHp;
        int dmg = currentLevel.bossDamage;
        var p = GameManager.Instance != null ? GameManager.Instance.player : null;

        if (p != null)
        {
            if (currentLevel.bossHpMul > 0f)
                hp = Mathf.Max(hp, Mathf.RoundToInt(p.maxHp * currentLevel.bossHpMul));
            if (currentLevel.bossDamagePct > 0f)
                dmg = Mathf.Max(dmg, Mathf.RoundToInt(p.maxHp * currentLevel.bossDamagePct));
        }

        boss = BossController.Create(currentLevel.bossElement, hp, dmg,
            currentLevel.bossFile, currentLevel.bossSize, currentLevel.bossBulletDamageMul);

        // 灌入本关的弹幕参数
        boss.bulletSpeed = currentLevel.bossBulletSpeed;
        boss.shootIntervalP1 = currentLevel.bossShootP1;
        boss.shootIntervalP2 = currentLevel.bossShootP2;
        boss.shootIntervalP3 = currentLevel.bossShootP3;
        boss.ringCountP1 = currentLevel.bossRingP1;
        boss.ringCountP2 = currentLevel.bossRingP2;
        boss.ringCountP3 = currentLevel.bossRingP3;

        GameManager.Instance.boss = boss;
        GameManager.Instance.PlaySfx("boss_appear");
        if (hud != null) hud.SetBoss(boss);
    }

    // Boss 死亡由 GameManager 碰撞检测后回调到这里
    public void NotifyBossKilled()
    {
        if (hud != null) hud.ClearBoss();
        LevelVictory();
    }

    public void NotifyPlayerDeath()
    {
        // 由 GameManager.OnPlayerDeath 调用
        if (currentLevel.id == LevelConfig.Id.Endless)
        {
            // 无尽模式：更新无尽排行榜，回菜单
            UIManager.Instance.ReturnFromGameOver();
        }
    }

    /// <summary>
    /// 本次胜利是否把成长写回了存档。只有「首次通关该关」才为 true，
    /// 结算面板据此显示"成长已带走"还是"重玩不累积"。
    /// </summary>
    public bool lastVictorySavedGrowth;

    void LevelVictory()
    {
        // 只有「首次通关」才把本关成长写回存档：
        //   - 死亡 / ESC 回菜单 / 中途退出 → 不写，本关收益作废
        //   - 重玩已通关的关卡 → 只记分数，不再累积属性（堵死反复刷关卡变强）
        //   - 无尽模式 → 没有"通关"概念，不写
        // 判定必须在 UnlockNextLevel 之前，否则 maxUnlockedLevel 已经 +1，永远算作"已通关"。
        lastVictorySavedGrowth =
            currentLevel.id != LevelConfig.Id.Endless && !SaveSystem.IsLevelCleared(currentLevel.id);

        // 必须在 StopLevel 之前抓：此时玩家属性还是本关结束时的值
        var p = GameManager.Instance != null ? GameManager.Instance.player : null;
        if (p != null && lastVictorySavedGrowth) PlayerGrowth.Commit(p);

        StopLevel();
        SaveSystem.UnlockNextLevel(currentLevel.id);
        UIManager.Instance.NotifyVictory();
    }

    // ---------------------------------------------------------------
    // timeScale 管理（唯一入口）
    public void SetFrozen(bool on)
    {
        frozen = on;
        ApplyTimeScale();
    }

    public void RequestSlowdown(bool on)
    {
        if (on) slowdownRequests++;
        else if (slowdownRequests > 0) slowdownRequests--;
        ApplyTimeScale();
    }

    void ApplyTimeScale()
    {
        if (frozen || paused) Time.timeScale = 0f;
        else if (slowdownRequests > 0) Time.timeScale = 0.4f;
        else Time.timeScale = 1f;
    }
}
