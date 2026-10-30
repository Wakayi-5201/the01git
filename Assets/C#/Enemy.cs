using UnityEngine;

/// <summary>
/// 敌人。对应 pygame 的 Enemy 类（普通 / 射击者 / BOSS 用字段区分）。
/// 结构：Enemy 根对象（scale=1，负责移动）+ 子对象 Sprite（缩放）+ 血条。
/// </summary>
public class Enemy : MonoBehaviour
{
    public Element element;
    public bool isBoss;
    public bool isShooter;

    public float hp;
    public float maxHp;
    public int damageValue;
    public float speed;

    public Vector2 halfExtents;
    Vector2 target;
    int changeDirectionTimer;

    public int frozenTimer;
    public int slowTimer;
    public int knockbackTimer;
    Vector2 knockbackVel;

    // ---------------------------------------------------------------
    // ★ 敌人总调区（public，可在 Inspector 或由 WaveSpawner 逐波覆盖）
    // ---------------------------------------------------------------
    [Header("敌人 — 射击")]
    [Tooltip("是否带射击能力")]
    public bool hasShooter;
    [Tooltip("射击间隔帧数。<=0 时沿用 Create 里的默认公式")]
    public int shootDelayOverride = 0;
    [Tooltip("敌人子弹飞行速度。<=0 时沿用 EnemyBullet 自身的默认速度")]
    public float bulletSpeedOverride = 0f;

    int shootTimer;
    int shootDelay;
    int bulletDamage;

    public bool Dead;

    SpriteRenderer sr;
    SpriteRenderer hpBarBg;
    SpriteRenderer hpBarFill;
    float hpBarWidth;
    Color baseColor = Color.white;

    public bool IsFrozen => frozenTimer > 0;
    public bool IsSlowed => slowTimer > 0;

    // ---------------------------------------------------------------
    public static Enemy Create(Element element, int baseHp, bool isBoss, bool isShooter, int gameTime, int playerLevel)
    {
        // 本局内的成长：战斗时长 + 玩家当前等级
        float runScale = 1f + (gameTime / 5000f) * 0.4f + (playerLevel - 1) * 0.15f;
        // 跨关成长：玩家带着通关属性进下一关后，杂兵必须跟着变强，否则后期就是纯割草。
        // 伤害涨幅刻意小于血量涨幅，避免玩家一进场就被秒。
        float growthHp = PlayerGrowth.EnemyHpMul;
        float growthDmg = PlayerGrowth.EnemyDamageMul;

        var root = new GameObject("Enemy");
        float worldSize = (isBoss ? 110f : 70f) * GameConfig.EntityScale;
        var sprite = AssetLibrary.GetSprite("敌人/" + ElementDatabase.EnemyFile(element));

        // 子对象：精灵（缩放）
        var sr = SpriteFactory.NewSprite("Sprite", sprite, new Vector2(worldSize, worldSize),
            GameConfig.OrderEnemy, root.transform);

        var e = root.AddComponent<Enemy>();
        e.element = element;
        e.isBoss = isBoss;
        e.isShooter = isShooter;
        e.sr = sr;
        e.halfExtents = new Vector2(worldSize * 0.5f * 0.8f, worldSize * 0.5f * 0.8f);

        // 速度只跟本局成长走：移速上限锁死后，敌人再加速玩家就躲不掉了
        e.speed = isBoss
            ? 2.8f * Mathf.Min(runScale, 2.0f)
            : Random.Range(1.8f, 3.5f) * Mathf.Min(runScale, 1.8f);

        // 出生点：玩家「视野外一圈」。
        // 地图放大 3 倍后活动区很大，若仍从活动区边缘出生，敌人要走十几秒才进场，波次会明显拖沓。
        var pa = LevelManager.CurrentPlayArea;
        var pl = GameManager.Instance != null ? GameManager.Instance.player : null;
        Vector2 around = pl != null ? (Vector2)pl.transform.position : pa.center;

        var cam = Camera.main;
        float halfH = cam != null ? cam.orthographicSize : GameConfig.BaseMapHeight * 0.25f;
        float halfW = halfH * (cam != null ? cam.aspect : 16f / 9f);
        float ring = Mathf.Sqrt(halfH * halfH + halfW * halfW) + 90f;

        float ang = Random.Range(0f, Mathf.PI * 2f);
        var p = around + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * ring;

        // 贴回活动区，避免出生在牌桌外框上
        p.x = Mathf.Clamp(p.x, pa.xMin + 40f, pa.xMax - 40f);
        p.y = Mathf.Clamp(p.y, pa.yMin + 40f, pa.yMax - 40f);
        root.transform.position = new Vector3(p.x, p.y, 0f);

        e.hp = Mathf.RoundToInt(baseHp * runScale * growthHp);
        e.maxHp = e.hp;
        e.damageValue = Mathf.RoundToInt((isBoss ? 12f : 6f) * Mathf.Min(runScale, 1.6f) * growthDmg);
        e.changeDirectionTimer = Random.Range(60, 180);
        e.Retarget();

        if (isShooter)
        {
            e.hasShooter = true;
            e.shootTimer = Random.Range(60, 180);
            e.shootDelay = Mathf.Max(40, 120 - Mathf.RoundToInt(runScale * 20f));
            e.bulletDamage = Mathf.RoundToInt(e.damageValue * 0.9f);
        }

        // 外部（WaveSpawner / Inspector）指定的覆盖值优先生效
        if (e.shootDelayOverride > 0) e.shootDelay = e.shootDelayOverride;

        e.CreateHpBar(worldSize);
        return e;
    }

    void CreateHpBar(float worldSize)
    {
        hpBarWidth = (isBoss ? 110f : 65f) * GameConfig.EntityScale;
        float barH = 6f;
        var white = SpriteFactory.WhiteSprite();

        hpBarBg = SpriteFactory.NewSprite("HPBg", white, new Vector2(hpBarWidth, barH),
            GameConfig.OrderHpBar, transform);
        hpBarBg.color = Color.black;

        hpBarFill = SpriteFactory.NewSprite("HPFill", white, new Vector2(hpBarWidth, barH),
            GameConfig.OrderHpBar + 1, transform);
        hpBarFill.color = ElementDatabase.GetColor(element);

        UpdateHpBar();
    }

    void UpdateHpBar()
    {
        if (hpBarFill == null) return;
        float frac = maxHp > 0f ? Mathf.Clamp01(hp / maxHp) : 0f;
        float y = halfExtents.y + 12f;

        hpBarBg.transform.localPosition = new Vector3(0f, y, 0f);

        Vector3 s = hpBarFill.transform.localScale;
        s.x = hpBarWidth * frac;
        hpBarFill.transform.localScale = s;
        hpBarFill.transform.localPosition = new Vector3(-hpBarWidth * 0.5f * (1f - frac), y, 0f);
    }

    void Retarget()
    {
        var pa = LevelManager.CurrentPlayArea;
        target = new Vector2(Random.Range(pa.xMin + 60f, pa.xMax - 60f),
                             Random.Range(pa.yMin + 60f, pa.yMax - 60f));
    }

    // ---------------------------------------------------------------
    public void Step(Vector2 playerPos)
    {
        if (frozenTimer > 0)
        {
            frozenTimer--;
            UpdateVisual();
            return;
        }

        float speedMul = 1f;
        if (slowTimer > 0)
        {
            slowTimer--;
            speedMul = 0.5f;
        }

        if (knockbackTimer > 0)
        {
            knockbackTimer--;
            transform.position += new Vector3(knockbackVel.x, knockbackVel.y, 0f);
            knockbackVel *= 0.95f;
            UpdateVisual();
            UpdateHpBar();
            return;
        }

        changeDirectionTimer--;
        if (changeDirectionTimer <= 0)
        {
            changeDirectionTimer = Random.Range(60, 180);
            Retarget();
        }

        if (Random.value < 0.02f) target = playerPos;

        Vector2 cur = transform.position;
        Vector2 d = target - cur;
        float dist = d.magnitude;
        if (dist > 0.001f)
            transform.position += (Vector3)(d / dist) * speed * speedMul;

        ClampToMap();

        if (hasShooter)
        {
            shootTimer--;
            if (shootTimer <= 0)
            {
                shootTimer = shootDelay;
                ShootAt(playerPos);
            }
        }

        UpdateVisual();
        UpdateHpBar();
    }

    void ClampToMap()
    {
        var pa = LevelManager.CurrentPlayArea;
        Vector3 p = transform.position;
        p.x = Mathf.Clamp(p.x, pa.xMin, pa.xMax);
        p.y = Mathf.Clamp(p.y, pa.yMin, pa.yMax);
        transform.position = p;
    }

    void ShootAt(Vector2 playerPos)
    {
        Vector2 cur = transform.position;
        float dx = playerPos.x - cur.x;
        float dy = playerPos.y - cur.y;
        string dir;
        if (Mathf.Abs(dx) > Mathf.Abs(dy)) dir = dx > 0f ? "right" : "left";
        else dir = dy > 0f ? "up" : "down";

        var b = GameManager.Instance.SpawnEnemyBullet(cur, dir, bulletDamage);
        if (b != null && bulletSpeedOverride > 0f) b.SetSpeed(bulletSpeedOverride);
    }

    void UpdateVisual()
    {
        if (sr == null) return;
        if (frozenTimer > 0) sr.color = new Color32(150, 220, 255, 255);
        else if (slowTimer > 0) sr.color = new Color32(200, 240, 255, 255);
        else sr.color = baseColor;
    }

    public void ApplyFreeze(int duration) => frozenTimer = duration;
    public void ApplySlow(int duration) => slowTimer = duration;

    /// <summary>覆盖射击间隔（帧），并立即重置计时器。由 WaveSpawner 逐波调用。</summary>
    public void SetShootDelay(int frames)
    {
        shootDelay = Mathf.Max(1, frames);
        shootTimer = shootDelay;
    }

    public void ApplyKnockback(float force)
    {
        knockbackTimer = 30;
        knockbackVel = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * force;
    }
}