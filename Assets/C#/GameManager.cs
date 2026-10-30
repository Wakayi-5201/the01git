using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 游戏主管理器。对应 pygame 的 Game 类（本阶段先实现“游玩”核心循环）。
/// 固定 60 帧/秒推进逻辑，因此原版所有“每帧”数值可原样使用。
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public GameState state = GameState.Playing;

    [Tooltip("把场景里的玩家对象拖到这里")]
    public PlayerController player;

    public readonly List<Bullet> bullets = new List<Bullet>();
    public readonly List<EnemyBullet> enemyBullets = new List<EnemyBullet>();
    public readonly List<Enemy> enemies = new List<Enemy>();
    public readonly List<FloatingText> texts = new List<FloatingText>();
    public readonly List<ExplosionEffect> explosions = new List<ExplosionEffect>();
    public readonly List<UltimateSkill> ultimates = new List<UltimateSkill>();
    public readonly List<BossSummon> bossSummons = new List<BossSummon>();   // BOSS 专属技能召唤物

    /// <summary>BOSS 生成一个技能召唤物。</summary>
    public void AddBossSummon(BossSummon s) => bossSummons.Add(s);

    /// <summary>BOSS 技能对玩家造成伤害（走护盾/无敌判定，含死亡检查）。</summary>
    public void DamagePlayerByBoss(int amount, Vector2 at)
    {
        if (player == null) return;
        if (player.TakeDamage(amount) == PlayerController.PlayerHit.Damaged)
        {
            FloatingTextAt(at + new Vector2(0f, 30f), "-" + amount, GameConfig.Red, false);
            combo = 0;
            if (player.hp <= 0) OnPlayerDeath();
        }
    }

    /// <summary>水大招熄灭火焰：清掉场上所有火系召唤物（火柱提前消散）。</summary>
    public void ExtinguishFire()
    {
        for (int i = 0; i < bossSummons.Count; i++) bossSummons[i].Douse();
    }

    /// <summary>当前 Boss（由 LevelManager 生成后赋值，null 表示无 Boss）。</summary>
    public BossController boss;

    public int score;
    public int combo;
    public int comboTimer;
    public int maxCombo;
    public int gameTime;
    public int difficultyLevel = 1;
    public bool isNewHighScore;
    int enemySpawnTimer;

    /// <summary>敌人死亡事件（杂兵，不含 Boss）。WaveSpawner 订阅以做清场判定。</summary>
    public event System.Action<Enemy> OnEnemyKilled;

    AudioSource sfxSource;
    AudioSource musicSource;
    float accumulator;
    const float StepDt = 1f / 60f;

    void Awake()
    {
        Instance = this;
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.volume = 0.3f;
    }

    void Start()
    {
        if (player == null)
        {
            Debug.LogError("[GameManager] 请在 Inspector 里把玩家对象拖到 Player 字段。");
        }
        else
        {
            // 开局先隐藏玩家，等玩家点“开始游戏”再出场
            player.gameObject.SetActive(false);
        }

        state = GameState.Menu;
        PlayMusic("menu_bgm");
    }

    /// <summary>开始某一关。由 LevelManager 调用。关卡模式下由 WaveSpawner 控制刷怪。</summary>
    public void StartGame(Element startElement, LevelConfig.Id levelId, bool inheritGrowth = true)
    {
        if (player == null)
        {
            Debug.LogError("[GameManager] 请在 Inspector 里把玩家对象拖到 Player 字段。");
            return;
        }

        ClearAll();
        boss = null;

        score = 0; combo = 0; comboTimer = 0; maxCombo = 0;
        gameTime = 0; difficultyLevel = 1; enemySpawnTimer = 0; accumulator = 0f;
        isNewHighScore = false;

        // 关卡模式启用 WaveSpawner，禁用 GameManager 自带刷怪
        useWaveSpawner = levelId != LevelConfig.Id.Endless ? true : false;

        player.gameObject.SetActive(true);
        player.ResetPlayer(startElement, inheritGrowth);

        state = GameState.Playing;
        PlayMusic("bgm");
    }

    /// <summary>返回菜单 / 重开前清场：销毁所有运行时生成的对象。</summary>
    public void StopGame()
    {
        ClearAll();
        if (player != null) player.gameObject.SetActive(false);
        accumulator = 0f;
        state = GameState.Menu;
        PlayMusic("menu_bgm");
        SaveSystem.Save();   // 图鉴击杀数落盘
    }

    void ClearAll()
    {
        for (int i = bullets.Count - 1; i >= 0; i--) if (bullets[i] != null) Destroy(bullets[i].gameObject);
        bullets.Clear();
        for (int i = enemyBullets.Count - 1; i >= 0; i--) if (enemyBullets[i] != null) Destroy(enemyBullets[i].gameObject);
        enemyBullets.Clear();
        for (int i = enemies.Count - 1; i >= 0; i--) if (enemies[i] != null) Destroy(enemies[i].gameObject);
        enemies.Clear();
        for (int i = texts.Count - 1; i >= 0; i--) if (texts[i] != null) Destroy(texts[i].gameObject);
        texts.Clear();
        for (int i = explosions.Count - 1; i >= 0; i--) if (explosions[i] != null) Destroy(explosions[i].gameObject);
        explosions.Clear();
        for (int i = ultimates.Count - 1; i >= 0; i--) if (ultimates[i] != null) Destroy(ultimates[i].gameObject);
        ultimates.Clear();
        if (boss != null) { Destroy(boss.gameObject); boss = null; }
        for (int i = bossSummons.Count - 1; i >= 0; i--) bossSummons[i].DestroyNow();
        bossSummons.Clear();
    }

    // ---------------------------------------------------------------
    void Update()
    {
        if (player == null) return;

        // 结算期间让爆炸/飘字继续播完
        if (state == GameState.GameOver)
        {
            for (int i = 0; i < explosions.Count; i++) explosions[i].Step();
            for (int i = 0; i < texts.Count; i++) texts[i].Step();
            Cull();
            return;
        }

        if (state != GameState.Playing) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (UIManager.Instance != null) UIManager.Instance.QuitToMenu();
            else StopGame();
            return;
        }

        accumulator += Time.deltaTime;
        int steps = 0;
        while (accumulator >= StepDt && steps < 5)
        {
            accumulator -= StepDt;
            Step();
            steps++;
        }
        if (accumulator > StepDt) accumulator = 0f;
    }

    void Step()
    {
        gameTime++;

        // 难度随时间提升：每 3600 帧（60 秒）一级
        if (gameTime % 3600 == 0)
        {
            difficultyLevel++;
            FloatingTextAt(player.transform.position + new Vector3(0f, 40f, 0f),
                "难度提升! 等级" + difficultyLevel, GameConfig.Red, true);
        }

        if (comboTimer > 0)
        {
            comboTimer--;
            if (comboTimer == 0) combo = 0;
        }

        player.Step();

        for (int i = 0; i < bullets.Count; i++) bullets[i].Step();
        for (int i = 0; i < enemyBullets.Count; i++) enemyBullets[i].Step();

        Vector2 playerPos = player.transform.position;
        for (int i = 0; i < enemies.Count; i++) enemies[i].Step(playerPos);

        if (boss != null && !boss.Dead) boss.Step(playerPos);

        // BOSS 技能召唤物（火柱/漩涡/落雷/突刺）
        for (int i = bossSummons.Count - 1; i >= 0; i--)
            if (!bossSummons[i].Step(player)) bossSummons.RemoveAt(i);

        // 灼烧 DoT（火柱附加）：每 60 帧扣 2% 最大生命，水大招可解除
        if (player.burnTimer > 0)
        {
            player.burnTimer--;
            if (player.burnTimer % 60 == 0 && player.hp > 0)
            {
                int burnDmg = Mathf.Max(1, Mathf.RoundToInt(player.maxHp * 0.02f));
                player.hp -= burnDmg;
                FloatingTextAt(player.transform.position + new Vector3(0f, 50f, 0f),
                    "-" + burnDmg + " 灼烧", new Color32(255, 120, 40, 255), false);
                if (player.hp <= 0) OnPlayerDeath();
            }
        }

        for (int i = 0; i < texts.Count; i++) texts[i].Step();
        for (int i = 0; i < explosions.Count; i++) explosions[i].Step();
        for (int i = 0; i < ultimates.Count; i++) ultimates[i].Step();

        HandleCollisions();
        Cull();

        // 关卡模式下由 WaveSpawner 控制刷怪，禁用 GameManager 自带刷怪
        if (!useWaveSpawner)
        {
            enemySpawnTimer++;
            // 爽感调整 B：刷怪间隔 ×0.65
            int spawnDelay = Mathf.Max(10, Mathf.RoundToInt((90 - player.level * 10 - difficultyLevel * 8) * 0.65f));
            if (enemySpawnTimer >= spawnDelay)
            {
                SpawnEnemy();
                enemySpawnTimer = 0;
            }
            // 爽感调整 B：同屏上限 8 → 16
            if (enemies.Count < 16) SpawnEnemy();
        }
    }

    /// <summary>关卡模式下设 true，由 WaveSpawner 控制刷怪。</summary>
    public bool useWaveSpawner;

    // ---------------------------------------------------------------
    public static bool Overlap(Vector2 aPos, Vector2 aHalf, Vector2 bPos, Vector2 bHalf)
    {
        return Mathf.Abs(aPos.x - bPos.x) <= aHalf.x + bHalf.x &&
               Mathf.Abs(aPos.y - bPos.y) <= aHalf.y + bHalf.y;
    }

    void HandleCollisions()
    {
        // 玩家子弹 vs 敌人
        for (int i = bullets.Count - 1; i >= 0; i--)
        {
            var b = bullets[i];
            if (b.Dead) continue;
            for (int j = enemies.Count - 1; j >= 0; j--)
            {
                var e = enemies[j];
                if (e.Dead) continue;
                if (!Overlap(b.transform.position, b.halfExtents, e.transform.position, e.halfExtents)) continue;
                OnBulletHitEnemy(b, e);
                break;
            }
        }

        // 玩家子弹 vs Boss
        if (boss != null && !boss.Dead)
        {
            for (int i = bullets.Count - 1; i >= 0; i--)
            {
                var b = bullets[i];
                if (b.Dead) continue;
                if (!Overlap(b.transform.position, b.halfExtents, boss.transform.position, boss.halfExtents)) continue;
                OnBulletHitBoss(b, boss);
                break;
            }
        }

        Vector2 playerPos = player.transform.position;

        // 敌人子弹 vs 玩家
        for (int i = enemyBullets.Count - 1; i >= 0; i--)
        {
            var b = enemyBullets[i];
            if (b.Dead) continue;
            if (!Overlap(b.transform.position, b.halfExtents, playerPos, player.halfExtents)) continue;
            if (player.TakeDamage(b.damage) == PlayerController.PlayerHit.Damaged)
            {
                FloatingTextAt(playerPos + new Vector2(0f, 30f), "-" + b.damage, GameConfig.Red, false);
                combo = 0;
                if (player.hp <= 0) OnPlayerDeath();
            }
            b.Kill();
        }

        // 敌人直接碰撞 vs 玩家
        for (int j = enemies.Count - 1; j >= 0; j--)
        {
            var e = enemies[j];
            if (e.Dead) continue;
            if (!Overlap(e.transform.position, e.halfExtents, playerPos, player.halfExtents)) continue;
            if (player.TakeDamage(e.damageValue) == PlayerController.PlayerHit.Damaged)
            {
                FloatingTextAt(playerPos + new Vector2(0f, 30f), "-" + e.damageValue, GameConfig.Red, false);
                combo = 0;
                if (player.hp <= 0) OnPlayerDeath();
            }
        }

        // Boss 直接碰撞 vs 玩家
        if (boss != null && !boss.Dead && !boss.IsEntering())
        {
            if (Overlap(boss.transform.position, boss.halfExtents, playerPos, player.halfExtents))
            {
                if (player.TakeDamage(boss.damageValue) == PlayerController.PlayerHit.Damaged)
                {
                    FloatingTextAt(playerPos + new Vector2(0f, 30f), "-" + boss.damageValue, GameConfig.Red, false);
                    combo = 0;
                    if (player.hp <= 0) OnPlayerDeath();
                }
            }
        }
    }

    void OnBulletHitBoss(Bullet b, BossController bs)
    {
        float dmg = 0f;
        if (b.element == bs.element)
        {
            FloatingTextAt(bs.transform.position, "免疫", GameConfig.Gray, false);
        }
        else
        {
            dmg = b.damage * ElementDatabase.GetMultiplier(b.element, bs.element);
            if (dmg > 0f)
            {
                bs.TakeDamage(dmg, out bool killed);
                FloatingTextAt(bs.transform.position + new Vector3(UnityEngine.Random.Range(-15f, 15f), 40f, 0f),
                    "-" + Mathf.RoundToInt(dmg), GameConfig.Red, false);
                player.AddEnergy(2);
                if (killed)
                {
                    score += 500;
                    SpawnExplosion(bs.transform.position, ElementDatabase.GetColor(bs.element), 2.5f);
                    PlaySfx("explosion");
                    Destroy(bs.gameObject);
                    boss = null;
                    if (LevelManager.Instance != null) LevelManager.Instance.NotifyBossKilled();
                }
            }
        }
        b.Kill();
    }

    void OnBulletHitEnemy(Bullet b, Enemy e)
    {
        float dmg = 0f;
        string reaction = null;

        if (b.element == e.element)
        {
            FloatingTextAt(e.transform.position, "免疫", GameConfig.Gray, false);
        }
        else if (b.element == Element.Rock)
        {
            dmg = b.damage * ElementDatabase.GetMultiplier(Element.Rock, e.element);
            reaction = ElementDatabase.ReactionCrystal;
        }
        else
        {
            dmg = b.damage * ElementDatabase.GetMultiplier(b.element, e.element);
            reaction = ElementDatabase.GetReaction(b.element, e.element);
        }

        if (dmg > 0f)
        {
            e.hp -= dmg;
            GameManager.Instance.PlaySfx("hit");
            FloatingTextAt(e.transform.position + new Vector3(UnityEngine.Random.Range(-15f, 15f), 30f, 0f),
                "-" + Mathf.RoundToInt(dmg), GameConfig.Red, false);
            player.AddEnergy(2);
        }

        if (reaction != null)
        {
            FloatingTextAt(e.transform.position, reaction, ElementDatabase.GetColor(b.element), true);
            if (reaction == ElementDatabase.ReactionFreeze) e.ApplyFreeze(120);
            else if (reaction == ElementDatabase.ReactionKnockback) e.ApplyKnockback(10f);
            else if (reaction == "超导" || reaction == "融化") e.ApplySlow(150);
            player.AddEnergy(5);
        }

        if (e.hp <= 0f) KillEnemy(e);
        b.Kill();
    }

    // ---------------------------------------------------------------
    // 大招伤害结算（与子弹伤害共用 KillEnemy 的分数/经验/升级结算）
    /// <summary>对一只杂兵造成伤害；致死时走完整击杀结算。</summary>
    public void DamageEnemy(Enemy e, float dmg, bool bigExplosion)
    {
        if (e == null || e.Dead) return;
        e.hp -= dmg;
        PlaySfx("hit");
        FloatingTextAt(e.transform.position + new Vector3(Random.Range(-15f, 15f), 30f, 0f),
            "-" + Mathf.RoundToInt(dmg), GameConfig.Red, false);
        if (e.hp <= 0f) KillEnemy(e);
    }

    /// <summary>对 Boss 造成伤害；致死时走与子弹击杀一致的收尾。</summary>
    public void DamageBoss(float dmg)
    {
        var bs = boss;
        if (bs == null || bs.Dead) return;

        bs.TakeDamage(dmg, out bool killed);
        FloatingTextAt(bs.transform.position + new Vector3(Random.Range(-15f, 15f), 40f, 0f),
            "-" + Mathf.RoundToInt(dmg), GameConfig.Red, false);
        if (player != null) player.AddEnergy(2);

        if (!killed) return;

        score += 500;
        SpawnExplosion(bs.transform.position, ElementDatabase.GetColor(bs.element), 2.5f);
        PlaySfx("explosion");
        Destroy(bs.gameObject);
        boss = null;
        LevelManager.Instance?.NotifyBossKilled();
    }

    /// <summary>圆形范围伤害（用于冰雨的落锥）。</summary>
    public void DamageEnemiesInRadius(Vector2 c, float r, float dmg)
    {
        float r2 = r * r;
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            var e = enemies[i];
            if (e == null || e.Dead) continue;
            if (((Vector2)e.transform.position - c).sqrMagnitude > r2) continue;
            DamageEnemy(e, dmg, false);
            if (state != GameState.Playing) return;      // 关卡已结束，停止结算
        }

        if (boss != null && !boss.Dead && ((Vector2)boss.transform.position - c).sqrMagnitude <= r2)
            DamageBoss(dmg);
    }

    /// <summary>清除半径内的敌方子弹。</summary>
    public void ClearEnemyBullets(Vector2 c, float r)
    {
        float r2 = r * r;
        for (int i = enemyBullets.Count - 1; i >= 0; i--)
        {
            var b = enemyBullets[i];
            if (b == null || b.Dead) continue;
            if (((Vector2)b.transform.position - c).sqrMagnitude <= r2) b.Kill();
        }
    }

    /// <summary>岩系大招落地：清屏（视野内杂兵受满伤，Boss 受半伤）。</summary>
    public void RockFallDamage(Rect view, float dmg)
    {
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            var e = enemies[i];
            if (e == null || e.Dead) continue;
            if (!view.Contains(e.transform.position)) continue;

            if (e.isBoss) DamageEnemy(e, dmg * 0.5f, true);
            else DamageEnemy(e, dmg, true);

            if (state != GameState.Playing) return;      // 关卡已结束，停止结算
        }

        if (boss != null && !boss.Dead && view.Contains(boss.transform.position))
            DamageBoss(dmg * 0.5f);
    }

    void KillEnemy(Enemy e)
    {
        if (e.Dead) return;
        e.Dead = true;

        int points = e.isBoss ? 200 : 50;
        float expF = points * (1f + combo * 0.1f);
        int comboBonus = Mathf.RoundToInt(points * combo * 0.1f);
        int total = points + comboBonus;

        score += total;
        AddCombo();

        if (player.AddExp(Mathf.RoundToInt(expF)))
        {
            FloatingTextAt(player.transform.position + new Vector3(0f, 50f, 0f), "升级!", GameConfig.Yellow, true);
            UpgradePanel.Instance?.ShowUpgrade();
        }

        if (combo > 1)
            FloatingTextAt(e.transform.position + new Vector3(0f, 30f, 0f), combo + "连击!", GameConfig.Yellow, false);

        FloatingTextAt(e.transform.position, "+" + total, GameConfig.Yellow, false);
        FloatingTextAt(e.transform.position + new Vector3(0f, -20f, 0f),
            "+" + Mathf.RoundToInt(expF) + "经验", GameConfig.Green, false);

        PlaySfx("explosion");
        SpawnExplosion(e.transform.position, ElementDatabase.GetColor(e.element), e.isBoss ? 1.6f : 0.6f);
        player.AddEnergy(e.isBoss ? 15 : 8);

        Codex.RecordEnemy(e);                       // 图鉴：击杀计数
        ItemPickup.TryDrop(e.transform.position, e.isBoss);   // 道具掉落（BOSS 必掉）

        OnEnemyKilled?.Invoke(e);
    }

    void OnPlayerDeath()
    {
        state = GameState.GameOver;
        SpawnExplosion(player.transform.position, new Color32(255, 100, 100, 255), 1.6f);
        player.gameObject.SetActive(false);
        SaveSystem.Save();   // 图鉴击杀数落盘

        // 更新最高分（对应 pygame 的 update_high_score）
        string cur = SaveSystem.Data.currentPlayer;
        if (!string.IsNullOrEmpty(cur))
        {
            int best;
            isNewHighScore = SaveSystem.UpdateHighScore(cur, score, out best);
        }

        PlayMusic("menu_bgm");
        LevelManager.Instance?.StopLevel();

        if (UIManager.Instance != null) UIManager.Instance.NotifyGameOver();
    }

    public void AddCombo()
    {
        combo++;
        comboTimer = 120;
        if (combo > maxCombo) maxCombo = combo;
    }

    void Cull()
    {
        for (int i = bullets.Count - 1; i >= 0; i--)
            if (bullets[i] == null || bullets[i].Dead) { if (bullets[i] != null) Destroy(bullets[i].gameObject); bullets.RemoveAt(i); }

        for (int i = enemyBullets.Count - 1; i >= 0; i--)
            if (enemyBullets[i] == null || enemyBullets[i].Dead) { if (enemyBullets[i] != null) Destroy(enemyBullets[i].gameObject); enemyBullets.RemoveAt(i); }

        for (int i = enemies.Count - 1; i >= 0; i--)
            if (enemies[i] == null || enemies[i].Dead) { if (enemies[i] != null) Destroy(enemies[i].gameObject); enemies.RemoveAt(i); }

        for (int i = texts.Count - 1; i >= 0; i--)
            if (texts[i] == null || texts[i].Dead) { if (texts[i] != null) Destroy(texts[i].gameObject); texts.RemoveAt(i); }

        for (int i = explosions.Count - 1; i >= 0; i--)
            if (explosions[i] == null || explosions[i].Dead) { if (explosions[i] != null) Destroy(explosions[i].gameObject); explosions.RemoveAt(i); }

        for (int i = ultimates.Count - 1; i >= 0; i--)
            if (ultimates[i] == null || ultimates[i].Dead) { if (ultimates[i] != null) Destroy(ultimates[i].gameObject); ultimates.RemoveAt(i); }
    }

    // ---------------------------------------------------------------
    public void SpawnBullet(Element element, Vector2 position, string direction, float damage)
        => bullets.Add(Bullet.Create(element, position, direction, damage));

    public EnemyBullet SpawnEnemyBullet(Vector2 position, string direction, int damage)
    {
        var b = EnemyBullet.Create(position, direction, damage);
        enemyBullets.Add(b);
        return b;
    }

    /// <summary>任意方向子弹（Boss 弹幕用）。velocity 已包含方向*速度。</summary>
    public EnemyBullet SpawnEnemyBullet(Vector2 position, Vector2 velocity, int damage)
    {
        var b = EnemyBullet.Create(position, velocity, damage);
        enemyBullets.Add(b);
        return b;
    }

    /// <summary>Boss 召唤小怪：在 Boss 附近生成一只普通敌人。</summary>
    public void SummonMinion(Element element)
    {
        if (boss == null) return;
        var e = Enemy.Create(element, 30, false, false, gameTime, player != null ? player.level : 1);
        e.transform.position = boss.transform.position + new Vector3(UnityEngine.Random.Range(-120f, 120f), 0f, 0f);
        enemies.Add(e);
    }

    public void SpawnEnemy()
    {
        Element element = ElementDatabase.EnemyElements[UnityEngine.Random.Range(0, ElementDatabase.EnemyElements.Length)];
        // 爽感调整 B：怪血 ×0.6（30~80 → 18~48），数量换质量
        int baseHp = UnityEngine.Random.Range(18, 49) + (player != null ? player.level * 6 : 0);
        bool isBoss = UnityEngine.Random.value < 0.05f;
        if (isBoss) baseHp *= 5;
        float shooterChance = Mathf.Min(0.1f + gameTime / 10000f, 0.4f);
        bool isShooter = !isBoss && UnityEngine.Random.value < shooterChance;

        enemies.Add(Enemy.Create(element, baseHp, isBoss, isShooter, gameTime, player != null ? player.level : 1));
    }

    public void SpawnExplosion(Vector2 position, Color color, float sizeScale)
        => explosions.Add(ExplosionEffect.Create(position, color, sizeScale));

    public FloatingText FloatingTextAt(Vector2 position, string text, Color color, bool large)
    {
        var ft = FloatingText.Create(position, text, color, large);
        texts.Add(ft);
        return ft;
    }

    // ---------------------------------------------------------------
    // 同名音效 30ms 内只播一次：避免同一帧多个敌人同时死亡时爆炸音叠加削波。
    readonly Dictionary<string, float> sfxLastPlayed = new Dictionary<string, float>();
    const float SfxThrottle = 0.03f;

    public void PlaySfx(string name)
    {
        var clip = AssetLibrary.GetAudio(name);
        if (clip == null) { Debug.LogWarning("[GameManager] 找不到音效: Audio/" + name); return; }

        float now = Time.unscaledTime;
        if (sfxLastPlayed.TryGetValue(name, out var last) && now - last < SfxThrottle) return;
        sfxLastPlayed[name] = now;

        sfxSource.PlayOneShot(clip, 0.5f);
    }

    public void PlayMusic(string name)
    {
        var clip = AssetLibrary.GetAudio(name);
        if (clip == null) { Debug.LogWarning("[GameManager] 找不到音乐: Audio/" + name); return; }
        if (musicSource.clip == clip && musicSource.isPlaying) return;  // 已在播放则不重启，避免断音
        musicSource.clip = clip;
        musicSource.Play();
    }
}