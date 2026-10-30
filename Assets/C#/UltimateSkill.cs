using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 元素大招。对应 pygame 的 FireGhostSkill / WaterSummon / IceRainSkill / ThunderWaveSkill / RockFallSkill，
/// 由 PlayerController.TryUseSkill() 在能量满时释放（F 键）。
///   火：幽灵冲撞，秒杀撞到的敌人并清弹
///   水：三个召唤物环绕玩家持续伤害
///   冰：范围冰锥雨，范围内随机落锥并清弹
///   雷：巨型光波，秒杀路径上的敌人并清弹
///   岩：巨岩从天而降，落地清空屏内杂兵（Boss 受半伤）
/// 逻辑按固定 60 帧/秒推进（由 GameManager.Step 调用），数值直接沿用原版。
/// 注意：不要 using System（会与 UnityEngine.Random 冲突）。
/// </summary>
public class UltimateSkill : MonoBehaviour
{
    public enum Kind { FireGhost, WaterSummon, IceRain, ThunderWave, RockFall }

    public Kind kind;
    public Element element;
    public float damage;
    public int lifetime;

    public Vector2 halfExtents;
    public float radius;        // 冰雨：作用半径
    public bool Dead;

    /// <summary>释放方向（水/冰大招不使用）。</summary>
    public string direction = "up";

    Vector2 dirVec;
    float speed;

    // ---- 冰雨 ----
    struct Spike { public Vector2 pos; public int timer; public GameObject go; }
    readonly List<Spike> spikes = new List<Spike>();
    Vector2 center;
    int attackTimer;

    // ---- 水环绕 ----
    Transform orbitTarget;
    float angle;
    float orbitRadius;

    // ---- 岩 ----
    float fallSpeed;
    float targetY;
    bool landed;

    SpriteRenderer sr;

    const float SpikeRadius = 30f;      // 单个冰锥的判定半径（世界单位）

    // ---------------------------------------------------------------
    /// <summary>释放大招。origin=玩家位置，dir=当前射击方向，level=玩家等级。</summary>
    public static void Cast(Element e, Vector2 origin, string dir, int level)
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        if (e == Element.Water)
        {
            // 水克火：水大招释放瞬间清除自身灼烧，并熄灭场上所有火系召唤物（熔岩火柱）
            if (gm.player != null && gm.player.burnTimer > 0)
            {
                gm.player.burnTimer = 0;
                gm.FloatingTextAt(origin + new Vector2(0f, 60f), "火焰被熄灭!", new Color32(130, 210, 255, 255), true);
            }
            gm.ExtinguishFire();

            for (int i = 0; i < 3; i++)
                Create(gm, Kind.WaterSummon, e, origin, dir, level, i);
            return;
        }

        switch (e)
        {
            case Element.Fire:    Create(gm, Kind.FireGhost,   e, origin, dir, level, 0); break;
            case Element.Ice:     Create(gm, Kind.IceRain,    e, origin, dir, level, 0); break;
            case Element.Thunder: Create(gm, Kind.ThunderWave, e, origin, dir, level, 0); break;
            case Element.Rock:    Create(gm, Kind.RockFall,  e, origin, dir, level, 0); break;
        }
    }

    static void Create(GameManager gm, Kind k, Element e, Vector2 origin, string dir, int level, int index)
    {
        var go = new GameObject("Ultimate_" + k);
        go.transform.position = new Vector3(origin.x, origin.y, 0f);

        var s = go.AddComponent<UltimateSkill>();
        s.kind = k;
        s.element = e;
        s.direction = dir;
        s.dirVec = Bullet.DirectionVector(dir);

        float scale = GameConfig.EntityScale;
        string file = null;
        Vector2 size = Vector2.zero;
        float halfK = 0.4f;

        switch (k)
        {
            case Kind.FireGhost:
                file = "角色/" + ElementDatabase.UltimateFile(Element.Fire);
                size = Vector2.one * (100f + level * 10f) * scale;
                s.speed = 8f + level;
                s.lifetime = 90;
                s.damage = 9999f;
                break;

            case Kind.ThunderWave:
                file = "角色/" + ElementDatabase.UltimateFile(Element.Thunder);
                size = new Vector2((300f + level * 30f) * scale, (80f + level * 10f) * scale);
                s.speed = 12f + level;
                s.lifetime = 60;
                s.damage = 9999f;
                break;

            case Kind.WaterSummon:
                file = "角色/水角色大招召唤物" + (index % 3 + 1);
                size = Vector2.one * 70f * scale;
                s.orbitRadius = 80f + level * 5f;
                s.angle = (360f / 3f) * index;
                s.lifetime = 300 + level * 30;
                s.damage = 30f + level * 8f;
                if (gm.player != null) s.orbitTarget = gm.player.transform;
                break;

            case Kind.IceRain:
                file = null;                       // 用程序生成的圆环
                s.radius = 200f + level * 20f;
                s.lifetime = 180 + level * 20;
                s.damage = 40f + level * 10f;
                s.center = origin;
                break;

            case Kind.RockFall:
                file = "角色/" + ElementDatabase.UltimateFile(Element.Rock);
                size = Vector2.one * (200f + level * 20f) * scale;
                s.lifetime = 90;
                s.damage = 200f + level * 50f;
                s.targetY = origin.y;
                go.transform.position = new Vector3(origin.x, origin.y + 400f, 0f);
                break;
        }

        var sprite = file != null ? AssetLibrary.GetSprite(file) : RingSprite();
        s.sr = SpriteFactory.NewSprite("Sprite", sprite, size.sqrMagnitude > 0f ? size : Vector2.one,
            GameConfig.OrderEffect, go.transform);
        s.halfExtents = new Vector2(Mathf.Max(8f, s.halfExtents.x), Mathf.Max(8f, s.halfExtents.y));

        if (k == Kind.IceRain)
        {
            // 圆环精灵按直径缩放
            s.sr.transform.localScale = Vector3.one * (s.radius * 2f / RingPixels);
            s.halfExtents = Vector2.one * s.radius;
        }
        else
        {
            // 按各自的宽高取碰撞盒（雷光波很宽，盒子也要跟着宽）
            s.halfExtents = new Vector2(Mathf.Max(8f, size.x * halfK), Mathf.Max(8f, size.y * halfK));
        }

        gm.ultimates.Add(s);
    }

    // ---------------------------------------------------------------
    public void Step()
    {
        if (Dead) return;

        switch (kind)
        {
            case Kind.FireGhost:
            case Kind.ThunderWave:
                transform.position += (Vector3)(dirVec * speed);
                if (OutOfBounds(150f)) { Kill(); return; }
                DamageTouching(kind == Kind.FireGhost);
                ClearBulletsTouching();
                break;

            case Kind.WaterSummon:
                UpdateOrbit();
                DamageTouching(false);
                break;

            case Kind.IceRain:
                UpdateIceRain();
                break;

            case Kind.RockFall:
                UpdateRockFall();
                break;
        }

        if (lifetime > 0) lifetime--;
        if (lifetime <= 0) Kill();
    }

    /// <summary>清除与自身重叠的敌方子弹（火幽灵 / 雷光波）。</summary>
    void ClearBulletsTouching()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        Vector2 c = transform.position;

        for (int i = gm.enemyBullets.Count - 1; i >= 0; i--)
        {
            var b = gm.enemyBullets[i];
            if (b == null || b.Dead) continue;
            if (!GameManager.Overlap(c, halfExtents, b.transform.position, b.halfExtents)) continue;
            b.Kill();
        }
    }

    bool OutOfBounds(float margin)
    {
        Vector3 p = transform.position;
        return p.x < -margin || p.x > GameConfig.MapWidth + margin ||
               p.y < -margin || p.y > GameConfig.MapHeight + margin;
    }

    void DamageTouching(bool bigExplosion)
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        Vector2 c = transform.position;

        for (int i = gm.enemies.Count - 1; i >= 0; i--)
        {
            var e = gm.enemies[i];
            if (e == null || e.Dead) continue;
            if (!GameManager.Overlap(c, halfExtents, e.transform.position, e.halfExtents)) continue;
            gm.DamageEnemy(e, damage, bigExplosion);
            if (gm == null || gm.state != GameState.Playing) return;   // 关卡已结束
        }

        var bs = gm.boss;
        if (bs != null && !bs.Dead && GameManager.Overlap(c, halfExtents, bs.transform.position, bs.halfExtents))
            gm.DamageBoss(damage);
    }

    void UpdateOrbit()
    {
        if (orbitTarget == null)
        {
            var g = GameManager.Instance;
            orbitTarget = g != null && g.player != null ? g.player.transform : null;
        }
        if (orbitTarget == null) return;

        angle += 3f;                       // 每帧 3 度（与原版一致）
        if (angle >= 360f) angle -= 360f;

        float rad = angle * Mathf.Deg2Rad;
        var p = (Vector2)orbitTarget.position + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * orbitRadius;
        transform.position = new Vector3(p.x, p.y, 0f);
    }

    void UpdateIceRain()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        attackTimer++;
        if (attackTimer % 6 == 0)
        {
            float a = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float d = Random.Range(0f, radius);
            var p = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;

            var go = new GameObject("Spike");
            go.transform.position = new Vector3(p.x, p.y, 0f);
            var srr = go.AddComponent<SpriteRenderer>();
            srr.sprite = SpikeSprite();
            srr.sortingOrder = GameConfig.OrderEffect;
            float k = SpikeRadius * 2f / SpikePixels;
            go.transform.localScale = new Vector3(k, k, 1f);

            spikes.Add(new Spike { pos = p, timer = 15, go = go });

            gm.DamageEnemiesInRadius(p, SpikeRadius, damage);
            if (gm.state != GameState.Playing) { Kill(); return; }
        }

        for (int i = spikes.Count - 1; i >= 0; i--)
        {
            var sp = spikes[i];
            sp.timer--;
            if (sp.timer <= 0)
            {
                if (sp.go != null) Destroy(sp.go);
                spikes.RemoveAt(i);
            }
        }

        // 范围内敌方子弹全部清除
        gm.ClearEnemyBullets(center, radius);
    }

    void UpdateRockFall()
    {
        if (landed) return;

        fallSpeed += 0.8f;
        var p = transform.position;
        p.y -= fallSpeed;
        transform.position = p;

        if (p.y <= targetY)
        {
            p.y = targetY;
            transform.position = p;
            landed = true;
            OnRockLanded();
        }
    }

    void OnRockLanded()
    {
        var gm = GameManager.Instance;
        var cam = Camera.main;
        if (gm == null || cam == null) return;

        float hh = cam.orthographicSize;
        float hw = hh * cam.aspect;
        var c = (Vector2)cam.transform.position;
        var view = new Rect(c.x - hw, c.y - hh, hw * 2f, hh * 2f);

        gm.RockFallDamage(view, damage);
        gm.SpawnExplosion(transform.position, ElementDatabase.GetColor(Element.Rock), 3f);
        gm.PlaySfx("explosion");
    }

    public void Kill()
    {
        if (Dead) return;
        ClearSpikes();
        Dead = true;
        if (gameObject != null) Destroy(gameObject);
    }

    void ClearSpikes()
    {
        foreach (var sp in spikes) if (sp.go != null) Destroy(sp.go);
        spikes.Clear();
    }

    void OnDestroy() => ClearSpikes();

    // ---------------------------------------------------------------
    // 程序生成的圆环 / 冰锥贴图（避免额外美术资源依赖）
    const int RingPixels = 256;
    const int SpikePixels = 64;

    static Sprite _ringSprite;
    static Sprite RingSprite()
    {
        if (_ringSprite != null) return _ringSprite;
        int S = RingPixels;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        float r = S * 0.5f;
        float outer = r - 2f;
        float inner = r - 8f;
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
        {
                float dx = x - r + 0.5f;
                float dy = y - r + 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = (d <= outer && d >= inner) ? 0.45f : 0f;
                tex.SetPixel(x, y, new Color(0.59f, 0.86f, 1f, a));
            }
        }
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.Apply();
        _ringSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 1f);
        return _ringSprite;
    }

    static Sprite _spikeSprite;
    static Sprite SpikeSprite()
    {
        if (_spikeSprite != null) return _spikeSprite;
        _spikeSprite = SpriteFactory.CircleSprite(SpikePixels, new Color32(200, 240, 255, 255));
        return _spikeSprite;
    }
}
