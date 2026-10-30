using UnityEngine;

/// <summary>
/// 波次生成器：管理 3 波杂兵的生成与清场判定。
/// 关键判定（来自经验 397960）：用 aliveCount（订阅 GameManager.OnEnemyKilled）做清场判定，
/// 波次结束条件 = duration 已到（停止新生成） && aliveCount == 0。
/// </summary>
public class WaveSpawner : MonoBehaviour
{
    LevelConfig.LevelData level;
    int currentWave = 0;            // 0 = wave1, 1 = wave2, 2 = wave3
    float waveTimer;                // 当前波剩余时间
    int aliveCount;
    float spawnDelay;

    // ---------------------------------------------------------------
    // ★ 杂兵总调区：全部可在 Inspector 里直接改（改完重开一局生效）。
    //   逐波取数组第 [currentWave] 项，保证"一波比一波强、一波比一波多"。
    // ---------------------------------------------------------------
    [Header("杂兵 — 基础血量（会被下面的波次倍数放大）")]
    // 爽感调整 B：怪血 ×0.6（30/80/90 → 18/48/54），数量换质量
    public int hpMin = 18;
    public int hpMax = 48;
    public int eliteHp = 54;

    [Header("杂兵 — 基础速度（会被下面的波次倍数放大）")]
    public float speedMin = 1.8f;
    public float speedMax = 3.5f;

    [Header("杂兵 — 远程怪子弹参数")]
    [Tooltip("敌人子弹飞行速度")]
    public float enemyBulletSpeed = 8f;
    [Tooltip("射击间隔帧数（越小打得越勤）")]
    public int enemyShootDelay = 120;
    [Tooltip("射击间隔下限，防止后期过密")]
    public int enemyShootDelayMin = 40;

    [Header("逐波强度递增（第 1 / 2 / 3 波）")]
    public float[] waveHpMul       = { 1.00f, 1.50f, 2.10f };   // 血量倍数递增
    public float[] waveSpeedMul    = { 1.00f, 1.12f, 1.25f };   // 速度倍数递增
    [Range(0f, 1f)] public float[] waveShooterChance = { 0.20f, 0.35f, 0.50f }; // 远程怪比例递增
    [Range(0f, 1f)] public float[] waveEliteChance  = { 0.00f, 0.10f, 0.22f };  // 精英率递增
    public int[]   waveSpawnInterval = { 45, 34, 25 };            // 爽感调整 B：间隔 ×0.65，越来越快
    public float[] waveShootDelayMul = { 1.00f, 0.85f, 0.70f };  // 远程怪射击间隔系数递减 = 打得越勤

    [Header("开局爆发刷怪")]
    [Tooltip("爽感调整 B：同屏上限倍数（作用于每波 maxAlive，2 = 数量翻倍）")]
    public float aliveMul = 2f;
    [Tooltip("每波开始时按同屏上限的这个比例先刷一批，让数量感更明显")]
    [Range(0f, 1f)] public float burstRatio = 0.6f;

    /// <summary>当前波索引对应的血量倍数。</summary>
    float HpMul       => Safe(waveHpMul, 1f);
    float SpeedMul    => Safe(waveSpeedMul, 1f);
    float ShooterChance => Safe(waveShooterChance, 0.3f);
    float EliteChance  => Safe(waveEliteChance, 0f);
    int   SpawnInterval => Safe(waveSpawnInterval, 60);
    float ShootDelayMul => Safe(waveShootDelayMul, 1f);

    /// <summary>按当前波次取数组值，索引越界时退回最后一个 / 默认值。</summary>
    float Safe(float[] arr, float fallback)
    {
        if (arr == null || arr.Length == 0) return fallback;
        int i = Mathf.Clamp(currentWave, 0, arr.Length - 1);
        return arr[i];
    }

    int Safe(int[] arr, int fallback)
    {
        if (arr == null || arr.Length == 0) return fallback;
        int i = Mathf.Clamp(currentWave, 0, arr.Length - 1);
        return arr[i];
    }

    public int CurrentWave => currentWave + 1;
    public int AliveCount => aliveCount;
    public bool WaveActive => currentWave < 3;

    public event System.Action<int> OnWaveStarted;    // waveIndex (0..2)
    public event System.Action<int> OnWaveCleared;    // waveIndex (0..2)
    public event System.Action OnAllWavesCleared;     // 第 3 波清场

    // ---------------------------------------------------------------
    public void StartLevel(LevelConfig.LevelData ld)
    {
        level = ld;
        currentWave = 0;
        aliveCount = 0;
        GameManager.Instance.OnEnemyKilled -= HandleEnemyKilled;
        GameManager.Instance.OnEnemyKilled += HandleEnemyKilled;
        StartWave(0);
    }

    public void Stop()
    {
        GameManager.Instance.OnEnemyKilled -= HandleEnemyKilled;
        currentWave = 99;
    }

    void StartWave(int idx)
    {
        currentWave = idx;
        var w = GetWave(idx);
        waveTimer = w.duration;
        aliveCount = 0;

        // 开局先铺一批，避免每波都要等第一只慢慢走进场
        int burst = Mathf.Max(1, Mathf.RoundToInt(w.maxAlive * aliveMul * burstRatio));
        for (int i = 0; i < burst; i++) SpawnOne(UnityEngine.Random.value < EliteChance);

        spawnDelay = (w.spawnInterval > 0 ? w.spawnInterval : SpawnInterval) / 60f;
        OnWaveStarted?.Invoke(idx);
    }

    LevelConfig.Wave GetWave(int idx)
    {
        return idx switch
        {
            0 => level.waves.wave1,
            1 => level.waves.wave2,
            _ => level.waves.wave3,
        };
    }

    // ---------------------------------------------------------------
    void Update()
    {
        if (level == null || !WaveActive) return;

        var w = GetWave(currentWave);

        // 生成敌人（duration 内且场上未到上限；aliveMul = 爽感调整 B 的数量倍数）
        if (waveTimer > 0f && aliveCount < Mathf.RoundToInt(w.maxAlive * aliveMul))
        {
            spawnDelay -= Time.deltaTime;
            if (spawnDelay <= 0f)
            {
                spawnDelay = (w.spawnInterval > 0 ? w.spawnInterval : SpawnInterval) / 60f;
                SpawnOne(UnityEngine.Random.value < EliteChance);
            }
        }

        waveTimer -= Time.deltaTime;

        // 波次结束：duration 到 且 清场
        if (waveTimer <= 0f && aliveCount == 0)
        {
            int cleared = currentWave;
            OnWaveCleared?.Invoke(cleared);
            GameManager.Instance.PlaySfx("wave_clear");
            if (currentWave >= 2)
            {
                currentWave = 99;   // 标记结束
                OnAllWavesCleared?.Invoke();
            }
            else
            {
                StartWave(currentWave + 1);
            }
        }
    }

    void SpawnOne(bool elite)
    {
        var pool = level.enemyPool;
        Element element = pool[UnityEngine.Random.Range(0, pool.Length)];
        int baseHp = elite ? eliteHp : UnityEngine.Random.Range(hpMin, hpMax + 1);
        bool shooter = !elite && UnityEngine.Random.value < ShooterChance;

        var e = Enemy.Create(element, baseHp, false, shooter,
            GameManager.Instance.gameTime,
            GameManager.Instance.player != null ? GameManager.Instance.player.level : 1);

        // 逐波递增：血量 / 速度
        e.hp = Mathf.RoundToInt(e.hp * HpMul);
        e.maxHp = e.hp;
        e.speed *= SpeedMul;

        // 覆盖敌人子弹速度，并让远程怪的射速也逐波加快
        e.bulletSpeedOverride = enemyBulletSpeed;
        if (e.hasShooter && e.shootDelayOverride <= 0)
        {
            int d = Mathf.RoundToInt(enemyShootDelay * ShootDelayMul);
            e.SetShootDelay(Mathf.Max(enemyShootDelayMin, d));
        }

        if (elite)
        {
            e.transform.localScale = Vector3.one * 1.5f;
            e.hp *= 2.5f; e.maxHp = e.hp;
        }

        GameManager.Instance.enemies.Add(e);
        aliveCount++;
    }

    void HandleEnemyKilled(Enemy e)
    {
        if (aliveCount > 0) aliveCount--;
    }
}
