using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家战机。对应 pygame 的 Player 类。
/// </summary>
public class PlayerController : MonoBehaviour
{
    [Header("初始属性（可在 Inspector 选择）")]
    public Element startElement = Element.Fire;

    // ---------------------------------------------------------------
    // ★ 玩家手感总调区：全部可在 Inspector 里直接改，改完开局即生效。
    //   （ResetPlayer 用的是下面这几个 start* 字段，所以改这里不会被重置覆盖）
    // ---------------------------------------------------------------
    [Header("玩家 — 血量 / 攻击")]
    [Tooltip("开局初始血量，也是 maxHp 的初始值")]
    public int startHp = 100;
    [Tooltip("每发子弹的基础伤害")]
    public int startDamage = 20;
    [Tooltip("每次升级额外增加的最大血量上限")]
    public int hpPerLevel = 20;

    [Header("玩家 — 移动")]
    [Tooltip("最大移动速度（世界单位 / 帧，原版为 6）")]
    public float startMaxSpeed = 6f;
    [Tooltip("加速度：越大起步越快")]
    public float acceleration = 0.5f;
    [Tooltip("减速度：越大刹车越干脆，越小越飘")]
    public float deceleration = 0.3f;

    [Header("玩家 — 射速 / 弹道")]
    [Tooltip("攻击速度等级。实际射击间隔 = Max(2, 9 - attackSpeedLevel) 帧")]
    public int startAttackSpeedLevel = 1;
    [Tooltip("同时发射的子弹排数")]
    public int startBulletRows = 1;

    [Header("玩家 — 大招")]
    [Tooltip("攒满多少能量才能释放大招")]
    public int startMaxEnergy = 200;
    [Tooltip("大招冷却帧数（60 帧 = 1 秒）")]
    public int skillMaxCooldown = 300;

    public Element element = Element.Fire;
    public readonly HashSet<Element> unlockedElements = new HashSet<Element>();

    SpriteRenderer sr;
    string animationState = "idle";  // idle / action
    int actionTimer = 0;
    const int ActionDuration = 30;

    public Vector2 velocity;
    public float maxSpeed = 6f;

    public int hp = 100;
    public int maxHp = 100;
    public int damage = 20;
    public int bulletRows = 1;
    public int attackSpeedLevel = 1;
    int shootTimer = 0;
    public string shootDirection = "up";

    public int energy = 0;
    public int maxEnergy = 200;
    public bool skillReady = false;
    public int skillCooldown = 0;
    public int energyRegenBonus = 0;

    public int level = 1;
    public int exp = 0;
    public int expToNext = 100;

    public bool invincible = false;
    public int invincibleTimer = 0;

    // 元素专属增益（大招释放后触发，大招系统稍后接入）
    public int waterHealTimer = 0;
    public int iceAuraTimer = 0;
    public int thunderChargeTimer = 0;
    public int firePowerTimer = 0;
    public float fireDamageBonus = 0f;
    public int rockShieldCount = 0;
    // 道具增益：烈焰之羽（攻击力提升，限时）
    public int itemAtkTimer = 0;
    public float itemAtkBonus = 0f;
    // BOSS 火柱附加的灼烧（水大招可解除）；被岩石突刺命中的击退
    public int burnTimer = 0;
    public Vector2 knockbackVel = Vector2.zero;

    /// <summary>被岩石突刺击退：沿 dir 方向推开，随时间衰减。</summary>
    public void ApplyKnockback(Vector2 dir, float force) => knockbackVel = dir.normalized * force;

    /// <summary>水系大招每秒回复的最大生命百分比。</summary>
    public const float WaterHealPercent = 0.02f;

    // 大招增益的可视化：脚下光环（水系=治愈绿、冰系=冰霜蓝）
    GameObject auraGo;
    SpriteRenderer auraSr;
    float auraPulse;
    static Sprite waterAuraSprite, iceAuraSprite;

    public Vector2 halfExtents = new Vector2(26f, 26f);

    // ---------------------------------------------------------------
    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sortingOrder = GameConfig.OrderPlayer;

        element = startElement;
        unlockedElements.Add(startElement);
        maxSpeed = startMaxSpeed;
        UpdateSprite();
    }

    // ---------------------------------------------------------------
    // 一次性按键缓存
    // 重要：Input.GetKeyDown 只能在 Unity 的真实每帧（Update）里读。
    // 逻辑推进（Step）由 GameManager 的固定步长循环驱动，高刷新率下
    // 有些帧不会执行 Step，那一帧的按键就被永久吞掉了——
    // 表现就是"按 F 大招有时没反应"。这里在真实帧里采集、在 Step 里消费。
    // ---------------------------------------------------------------
    bool pendingSkill, pendingPrevElement, pendingNextElement;

    void Update()
    {
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.state != GameState.Playing) { ClearPendingInput(); return; }

        if (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.E) ||
            Input.GetKeyDown(KeyCode.X) || Input.GetKeyDown(KeyCode.J))
            pendingSkill = true;

        if (Input.GetKeyDown(KeyCode.Q)) pendingPrevElement = true;
        if (Input.GetKeyDown(KeyCode.R)) pendingNextElement = true;
    }

    void ClearPendingInput()
    {
        pendingSkill = false;
        pendingPrevElement = false;
        pendingNextElement = false;
    }

    public void UpdateSprite()
    {
        string file = animationState == "action"
            ? ElementDatabase.PlayerActionFile(element)
            : ElementDatabase.PlayerIdleFile(element);
        var sprite = AssetLibrary.GetSprite("角色/" + file);
        sr.sprite = sprite;

        float targetH = (animationState == "action" ? 78f : 70f) * GameConfig.EntityScale;
        if (sprite != null)
        {
            float ratio = sprite.bounds.size.y > 0.0001f ? sprite.bounds.size.x / sprite.bounds.size.y : 1f;
            float w = targetH * ratio;
            SpriteFactory.ApplyWorldSize(transform, sprite, new Vector2(w, targetH));
            halfExtents = new Vector2(w * 0.5f, targetH * 0.5f);
        }
    }

    /// <summary>本次进关是否继承了历史成长（供 HUD/提示使用）。</summary>
    public bool inheritedGrowth;

    /// <summary>
    /// 开始新一局：重置本局状态并放到地图中心。对应 pygame 的 Player 初始化。
    /// inheritGrowth=true 时，永久属性（血量/攻击/弹道/攻速/移速/能量/等级）从存档恢复，
    /// 也就是"通关后带着成长进下一关"；本局状态（HP/能量/冷却/临时增益）一律重置。
    /// </summary>
    public void ResetPlayer(Element start, bool inheritGrowth = true)
    {
        element = start;

        // 已解锁的属性是永久的，从存档恢复；否则每开一局都会丢掉之前解锁的角色
        unlockedElements.Clear();
        foreach (Element e in ElementDatabase.AllElements)
            if (SaveSystem.IsElementUnlocked(e)) unlockedElements.Add(e);
        if (!unlockedElements.Contains(start)) unlockedElements.Add(start);

        velocity = Vector2.zero;

        // 永久成长：从存档恢复（首次游玩时回退到 start* 基准值）；本局满血进关
        inheritedGrowth = inheritGrowth && PlayerGrowth.Apply(this);
        if (!inheritGrowth)
        {
            maxHp = startHp;
            damage = startDamage;
            bulletRows = startBulletRows;
            attackSpeedLevel = startAttackSpeedLevel;
            maxSpeed = startMaxSpeed;
            maxEnergy = startMaxEnergy;
            level = 1; exp = 0; expToNext = 100;
        }
        hp = maxHp;

        shootTimer = 0; shootDirection = "up";
        energy = 0; skillReady = false; skillCooldown = 0; energyRegenBonus = 0;

        invincible = true; invincibleTimer = 90;
        waterHealTimer = 0; iceAuraTimer = 0; thunderChargeTimer = 0;
        firePowerTimer = 0; fireDamageBonus = 0f; rockShieldCount = 0;
        itemAtkTimer = 0; itemAtkBonus = 0f;
        burnTimer = 0; knockbackVel = Vector2.zero;

        animationState = "idle"; actionTimer = 0;

        var pa = LevelManager.CurrentPlayArea;
        transform.position = new Vector3(pa.center.x, pa.center.y, 0f);
        transform.rotation = Quaternion.identity;
        if (sr != null) sr.color = Color.white;

        UpdateSprite();
    }

    public int GetShootDelay() => Mathf.Max(2, 9 - attackSpeedLevel);
    public int GetTotalDamage() => Mathf.RoundToInt(damage * (1f + fireDamageBonus + itemAtkBonus));

    // ---------------------------------------------------------------
    public void Step()
    {
        if (actionTimer > 0)
        {
            actionTimer--;
            if (actionTimer == 0)
            {
                animationState = "idle";
                UpdateSprite();
            }
        }

        // 元素专属增益计时
        if (waterHealTimer > 0)
        {
            waterHealTimer--;
            // 水系：每秒回复 2% 最大生命（原版 1% 太弱，血量条几乎不动，看不出效果）
            if (waterHealTimer % 60 == 0 && hp < maxHp)
                WaterHealTick();
        }
        if (iceAuraTimer > 0)
        {
            // 冰系：无敌 5 秒。无敌时长统一管理在 invincibleTimer 里，
            // 这里只负责计时与结束时收尾，避免被下面的闪烁块立刻清掉。
            iceAuraTimer--;
            if (iceAuraTimer == 0 && invincibleTimer <= 0)
            {
                invincible = false;
                sr.color = Color.white;
            }
        }
        if (thunderChargeTimer > 0) thunderChargeTimer--;
        if (firePowerTimer > 0)
        {
            firePowerTimer--;
            if (firePowerTimer == 0) fireDamageBonus = 0f;
        }
        if (itemAtkTimer > 0)
        {
            itemAtkTimer--;
            if (itemAtkTimer == 0) itemAtkBonus = 0f;
        }

        // ---- 移动 ----
        float tvx = 0f, tvy = 0f;
        if (Input.GetKey(KeyCode.W)) tvy = maxSpeed;
        if (Input.GetKey(KeyCode.S)) tvy = -maxSpeed;
        if (Input.GetKey(KeyCode.A)) tvx = -maxSpeed;
        if (Input.GetKey(KeyCode.D)) tvx = maxSpeed;

        if (tvx > velocity.x) velocity.x += acceleration;
        else if (tvx < velocity.x) velocity.x -= acceleration;
        else velocity.x *= (1f - deceleration);

        if (tvy > velocity.y) velocity.y += acceleration;
        else if (tvy < velocity.y) velocity.y -= acceleration;
        else velocity.y *= (1f - deceleration);

        if (Mathf.Abs(velocity.x) < 0.1f) velocity.x = 0f;
        if (Mathf.Abs(velocity.y) < 0.1f) velocity.y = 0f;

        Vector3 pos = transform.position + new Vector3(velocity.x, velocity.y, 0f);

        // 击退位移（岩石突刺命中）：叠加在移动之上并逐帧衰减
        if (knockbackVel.sqrMagnitude > 0.01f)
        {
            pos += (Vector3)(knockbackVel * Time.deltaTime * 60f);
            knockbackVel = Vector2.Lerp(knockbackVel, Vector2.zero, 0.10f);
        }

        var pa = LevelManager.CurrentPlayArea;
        pos.x = Mathf.Clamp(pos.x, pa.xMin + halfExtents.x, pa.xMax - halfExtents.x);
        pos.y = Mathf.Clamp(pos.y, pa.yMin + halfExtents.y, pa.yMax - halfExtents.y);
        transform.position = pos;

        // ---- 射击方向（方向键） ----
        if (Input.GetKey(KeyCode.UpArrow)) shootDirection = "up";
        else if (Input.GetKey(KeyCode.DownArrow)) shootDirection = "down";
        else if (Input.GetKey(KeyCode.LeftArrow)) shootDirection = "left";
        else if (Input.GetKey(KeyCode.RightArrow)) shootDirection = "right";

        // ---- 切换属性（Q 上一个 / R 下一个；也可点击 HUD 右侧头像） ----
        // 数字键 1-5 已让给升级三选一，避免同一组键既切角色又选卡片
        // 按键在 Update 里缓存，这里只消费，避免固定步长循环吞输入
        if (pendingPrevElement) { pendingPrevElement = false; CycleElement(-1); }
        if (pendingNextElement) { pendingNextElement = false; CycleElement(1); }

        if (shootTimer > 0) shootTimer--;
        if (skillCooldown > 0) skillCooldown--;

        // ---- 自动开火 ----
        TryShoot();

        // ---- 大招（F 键；E/X/J 作为兼容别名保留） ----
        if (pendingSkill) { pendingSkill = false; TryUseSkill(); }

        // ---- 无敌闪烁 ----
        if (invincible)
        {
            invincibleTimer--;
            if (invincibleTimer <= 0)
            {
                invincible = false;
                sr.color = Color.white;
            }
            else
            {
                sr.color = (invincibleTimer % 6 < 3) ? new Color(1f, 1f, 1f, 0.4f) : Color.white;
            }
        }
        else if (burnTimer > 0)
        {
            // 灼烧视觉：角色持续橙红色脉冲（全套反馈之一）
            float k = 0.5f + 0.5f * Mathf.Sin(Time.time * 10f);
            sr.color = Color.Lerp(Color.white, new Color(1f, 0.42f, 0.12f), 0.35f + 0.35f * k);
        }
        else if (sr != null && sr.color != Color.white)
        {
            sr.color = Color.white;
        }

        UpdateAura();
    }

    // ---------------------------------------------------------------
    // 大招增益的可视化
    // ---------------------------------------------------------------

    /// <summary>水系每秒回血一次：真正加血 + 飘一个绿色的 +N。</summary>
    void WaterHealTick()
    {
        int heal = Mathf.Max(2, Mathf.RoundToInt(maxHp * WaterHealPercent));
        int before = hp;
        hp = Mathf.Min(maxHp, hp + heal);
        int actual = hp - before;
        if (actual <= 0) return;

        var gm = GameManager.Instance;
        if (gm != null)
            gm.FloatingTextAt(transform.position + new Vector3(0f, 40f, 0f),
                "+" + actual, new Color32(120, 255, 190, 255), false);
    }

    /// <summary>脚下光环：水系治愈中显示绿色环、冰系无敌中显示蓝色环，带呼吸缩放。</summary>
    void UpdateAura()
    {
        bool water = waterHealTimer > 0;
        bool ice = iceAuraTimer > 0;

        if (!water && !ice)
        {
            if (auraGo != null && auraGo.activeSelf) auraGo.SetActive(false);
            return;
        }

        EnsureAura();
        if (!auraGo.activeSelf) auraGo.SetActive(true);

        var sprite = water ? WaterAura() : IceAura();
        if (auraSr.sprite != sprite) auraSr.sprite = sprite;

        auraPulse += 0.09f;
        float pulse = Mathf.Sin(auraPulse);

        float baseSize = Mathf.Max(halfExtents.x, halfExtents.y) * 3.4f;
        float k = 1f + pulse * 0.07f;
        SpriteFactory.ApplyWorldSize(auraGo.transform, sprite, Vector2.one * baseSize * k);
        auraGo.transform.position = new Vector3(transform.position.x, transform.position.y, 0f);

        var c = water ? new Color(0.42f, 1f, 0.72f, 1f) : new Color(0.55f, 0.88f, 1f, 1f);
        c.a = 0.45f + pulse * 0.18f;
        auraSr.color = c;
    }

    void EnsureAura()
    {
        if (auraGo != null) return;
        auraGo = new GameObject("PlayerAura");
        auraSr = auraGo.AddComponent<SpriteRenderer>();
        auraSr.sortingOrder = GameConfig.OrderPlayer - 1;   // 压在玩家后面一点
        auraGo.SetActive(false);
    }

    static Sprite WaterAura()
    {
        if (waterAuraSprite == null)
            waterAuraSprite = SpriteFactory.RingSprite(128, new Color32(120, 255, 200, 255));
        return waterAuraSprite;
    }

    static Sprite IceAura()
    {
        if (iceAuraSprite == null)
            iceAuraSprite = SpriteFactory.RingSprite(128, new Color32(160, 225, 255, 255));
        return iceAuraSprite;
    }

    void OnDisable()
    {
        if (auraGo != null) auraGo.SetActive(false);
    }

    void OnDestroy()
    {
        if (auraGo != null) Destroy(auraGo);
    }

    void TryShoot()
    {
        if (shootTimer != 0) return;
        shootTimer = GetShootDelay();
        GameManager.Instance.PlaySfx("shoot");

        float spacing = 25f;
        Vector3 basePos = transform.position;
        for (int row = 0; row < bulletRows; row++)
        {
            float offset = (row - (bulletRows - 1) * 0.5f) * spacing;
            Vector2 spawn;
            switch (shootDirection)
            {
                case "down":  spawn = new Vector2(basePos.x + offset, basePos.y - halfExtents.y); break;
                case "left":  spawn = new Vector2(basePos.x - halfExtents.x, basePos.y + offset); break;
                case "right": spawn = new Vector2(basePos.x + halfExtents.x, basePos.y + offset); break;
                default:      spawn = new Vector2(basePos.x + offset, basePos.y + halfExtents.y); break;
            }
            GameManager.Instance.SpawnBullet(element, spawn, shootDirection, GetTotalDamage());
        }
    }

    /// <summary>
    /// 释放元素大招。对应 pygame 的 Player.use_skill()：
    /// 能量清零 → 进入冷却 → 触发动作动画 → 播音效 → 触发元素专属增益 → 生成大招实体。
    /// </summary>
    public bool TryUseSkill()
    {
        if (!skillReady || skillCooldown > 0) return false;

        var gm = GameManager.Instance;
        if (gm == null) return false;

        energy = 0;
        skillReady = false;
        skillCooldown = skillMaxCooldown;

        TriggerAction();
        gm.PlaySfx("skill");

        // 元素专属增益
        ApplyElementBuff(element);

        // 生成大招实体
        UltimateSkill.Cast(element, transform.position, shootDirection, level);

        // 原版的元素专属提示文字
        switch (element)
        {
            case Element.Water:
                gm.FloatingTextAt(transform.position + new Vector3(0f, -30f, 0f),
                    "治愈之泉! 每秒+2%生命", new Color32(100, 255, 200, 255), true); break;
            case Element.Ice:
                gm.FloatingTextAt(transform.position + new Vector3(0f, -30f, 0f),
                    "冰霜领域! 无敌5秒", new Color32(100, 200, 255, 255), true); break;
            case Element.Thunder:
                gm.FloatingTextAt(transform.position + new Vector3(0f, -30f, 0f),
                    "雷霆之力! 充能翻倍", new Color32(200, 200, 255, 255), true); break;
            case Element.Fire:
                gm.FloatingTextAt(transform.position + new Vector3(0f, -30f, 0f),
                    "烈焰爆发! 攻击+50%", new Color32(255, 150, 50, 255), true); break;
            case Element.Rock:
                gm.FloatingTextAt(transform.position + new Vector3(0f, -30f, 0f),
                    "岩石护盾! 抵挡3次", new Color32(180, 160, 130, 255), true); break;
        }

        return true;
    }

    /// <summary>触发动作动画（对应 pygame 的 trigger_action）。</summary>
    void TriggerAction()
    {
        animationState = "action";
        actionTimer = ActionDuration;
        UpdateSprite();
    }

    public void ApplyElementBuff(Element e)
    {
        switch (e)
        {
            case Element.Water:   waterHealTimer = 600; break;                       // 10 秒回血
            case Element.Ice:     iceAuraTimer = 300; invincible = true; invincibleTimer = 300; break;  // 5 秒无敌
            case Element.Thunder: thunderChargeTimer = 480; break;                   // 8 秒充能翻倍
            case Element.Fire:    firePowerTimer = 480; fireDamageBonus = 0.5f; break; // 8 秒攻击 +50%
            case Element.Rock:    rockShieldCount = 3; break;                         // 抵挡 3 次伤害
        }
    }

    public void AddEnergy(int amount)
    {
        float bonus = 1f + energyRegenBonus / 100f;
        if (thunderChargeTimer > 0) bonus *= 2f;
        energy = Mathf.Min(maxEnergy, energy + Mathf.RoundToInt(amount * bonus));
        if (energy >= maxEnergy) skillReady = true;
    }

    /// <summary>返回是否升级（每次调用最多升一级，与原版一致）。</summary>
    public bool AddExp(int amount)
    {
        exp += amount;
        if (exp >= expToNext)
        {
            exp -= expToNext;
            level++;
            expToNext = Mathf.RoundToInt(expToNext * 1.5f);
            // 升级带来的成长同样受永久成长上限约束，否则继承 + 升级会双重溢出
            maxHp = Mathf.Min(PlayerGrowth.MaxHp, maxHp + hpPerLevel);
            hp = Mathf.Min(maxHp, hp + hpPerLevel);
            maxEnergy = Mathf.Min(PlayerGrowth.MaxEnergy, maxEnergy + 30);
            GameManager.Instance.PlaySfx("levelup");
            return true;
        }
        return false;
    }

    /// <summary>受击结果：无敌免疫 / 护盾抵消 / 真正掉血（调用方据此决定飘什么字）。</summary>
    public enum PlayerHit { Immune, Shielded, Damaged }

    public PlayerHit TakeDamage(int amount)
    {
        if (invincible) return PlayerHit.Immune;

        if (rockShieldCount > 0)
        {
            rockShieldCount--;
            var gm = GameManager.Instance;
            if (gm != null)
                gm.FloatingTextAt(transform.position + new Vector3(0f, 40f, 0f),
                    "护盾 -1 (剩" + rockShieldCount + ")", new Color32(200, 180, 140, 255), false);
            return PlayerHit.Shielded;
        }

        hp -= amount;
        invincible = true;
        invincibleTimer = 60;
        GameManager.Instance.PlaySfx("hurt");
        return PlayerHit.Damaged;
    }

    /// <summary>在已解锁的属性之间循环切换：dir=1 下一个，dir=-1 上一个。</summary>
    public void CycleElement(int dir)
    {
        var all = ElementDatabase.AllElements;
        int cur = System.Array.IndexOf(all, element);
        if (cur < 0) return;

        // 按固定顺序找下一个已解锁的属性（跳过未解锁的）
        for (int step = 1; step <= all.Length; step++)
        {
            int i = ((cur + dir * step) % all.Length + all.Length) % all.Length;
            if (unlockedElements.Contains(all[i]))
            {
                SwitchElement(all[i]);
                return;
            }
        }
    }

    public bool SwitchElement(Element e)
    {
        if (!unlockedElements.Contains(e)) return false;
        if (element == e) return true;
        element = e;
        UpdateSprite();
        return true;
    }

    public bool UnlockElement(Element e)
    {
        if (unlockedElements.Contains(e)) return false;
        unlockedElements.Add(e);
        element = e;
        UpdateSprite();
        return true;
    }

    public void IncreaseDamage(int amount) => damage += amount;

    /// <summary>移速升级，受 PlayerGrowth.MaxMoveSpeed 封顶。</summary>
    public void IncreaseMoveSpeed(float amount) =>
        maxSpeed = Mathf.Min(PlayerGrowth.MaxMoveSpeed, maxSpeed + amount);

    /// <summary>攻击速度升级，受 PlayerGrowth.MaxAttackSpeedLevel 封顶。</summary>
    public void IncreaseAttackSpeed() =>
        attackSpeedLevel = Mathf.Min(PlayerGrowth.MaxAttackSpeedLevel, attackSpeedLevel + 1);

    /// <summary>弹道行数升级，受 PlayerGrowth.MaxBulletRows 封顶。</summary>
    public void IncreaseBulletRows() =>
        bulletRows = Mathf.Min(PlayerGrowth.MaxBulletRows, bulletRows + 1);

    /// <summary>生命上限升级，受 PlayerGrowth.MaxHp 封顶。</summary>
    public void IncreaseMaxHp(int amount) =>
        maxHp = Mathf.Min(PlayerGrowth.MaxHp, maxHp + amount);

    /// <summary>能量上限升级，受 PlayerGrowth.MaxEnergy 封顶。</summary>
    public void IncreaseMaxEnergy(int amount) =>
        maxEnergy = Mathf.Min(PlayerGrowth.MaxEnergy, maxEnergy + amount);
}