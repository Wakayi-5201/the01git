using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BOSS 专属技能系统（按属性定制，素材在 Resources/Sprites/敌人/）：
///   火 — 熔岩火柱：地面熔岩圈预警 → 火柱喷发，踩中受伤并附加 3 秒灼烧（水大招可熄灭）
///   水 — 海底漩涡：把玩家持续拉向漩涡中心，核心内持续受伤（拉力小于移速，反向跑可挣脱）
///   雷 — 落雷：天空预警圈 → 闪电劈下，一次爆发伤害
///   岩 — 岩石突刺：一整排/列石刺从一端向另一端波浪式弹出，命中伤害 + 击退
///
/// 所有召唤物统一三段式生命周期：
///   预警 1.0 秒（半透明闪烁、无伤害）→ 活性（造成伤害）→ 消散 0.4 秒（淡出后销毁）
/// 由 GameManager 固定步长驱动 Step()（与敌人/子弹一致，暂停时自然冻结）。
/// </summary>
public abstract class BossSummon
{
    public bool Done;
    protected GameObject go;
    protected SpriteRenderer sr;
    protected Color baseColor = Color.white;

    protected int phase;                    // 0=预警 1=活性 2=消散
    protected int t;                        // 当前阶段内帧计数（子类技能动画要用）
    protected const int TelegraphFrames = 60;   // 预警 1.0s（用户确认标准值）
    protected const int FadeFrames = 24;        // 消散 0.4s
    protected readonly int activeFrames;        // 活性时长由各技能自定

    protected const int GroundOrder = 5;        // 地面效果（画在角色脚下的圈/漩涡/裂纹）
    protected static GameManager Gm => GameManager.Instance;

    protected BossSummon(int activeFrames) { this.activeFrames = activeFrames; }

    /// <summary>每固定步调用。返回 false 表示已销毁，调用方应移除。</summary>
    public bool Step(PlayerController player)
    {
        t++;
        if (phase == 0)
        {
            TelegraphTick(player);
            if (t >= TelegraphFrames) EnterActive();
        }
        else if (phase == 1)
        {
            ActiveTick(player);
            if (t >= activeFrames) EnterFade();
        }
        else
        {
            if (sr != null)
                sr.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * (1f - (float)t / FadeFrames));
            if (t >= FadeFrames)
            {
                Done = true;
                if (go != null) Object.Destroy(go);
            }
        }
        return !Done;
    }

    protected virtual void TelegraphTick(PlayerController p)
    {
        if (sr != null)
        {
            float a = 0.22f + 0.14f * Mathf.Sin(t * 0.5f);   // 呼吸闪烁提醒"这里要出事"
            sr.color = new Color(baseColor.r, baseColor.g, baseColor.b, a);
        }
    }

    protected virtual void EnterActive()
    {
        phase = 1; t = 0;
        if (sr != null) sr.color = baseColor;
        OnEnterActive();
    }

    protected virtual void OnEnterActive() { }
    protected abstract void ActiveTick(PlayerController p);

    protected virtual void EnterFade() { phase = 2; t = 0; }

    /// <summary>水大招熄灭：默认等同于提前进入消散。火柱也走这条路径。</summary>
    public virtual void Douse()
    {
        if (phase == 0 || phase == 1) { phase = 2; t = 0; }
    }

    public virtual void DestroyNow()
    {
        Done = true;
        if (go != null) Object.Destroy(go);
    }

    // ---------------- 工具 ----------------
    protected static SpriteRenderer MakeSprite(string path, Vector2 pos, float worldSize, int order, float yOffset = 0f)
    {
        var sprite = AssetLibrary.GetSprite(path);
        var g = new GameObject(path, typeof(SpriteRenderer));
        g.transform.position = new Vector3(pos.x, pos.y + yOffset, 0f);
        var r = g.GetComponent<SpriteRenderer>();
        if (sprite != null)
        {
            r.sprite = sprite;
            var b = sprite.bounds;
            float m = Mathf.Max(b.size.x, b.size.y);
            if (m > 0.001f) g.transform.localScale = Vector3.one * (worldSize / m);
        }
        r.sortingOrder = order;
        return r;
    }

    protected static bool InRadius(Vector2 p, Vector2 c, float r) => (p - c).sqrMagnitude <= r * r;

    /// <summary>玩家当前位置（安全判空）。</summary>
    protected static Vector2 PlayerPos(PlayerController p) => p != null ? (Vector2)p.transform.position : Vector2.zero;

    protected static void Hurt(PlayerController p, int amount)
    {
        if (p != null && Gm != null)
            Gm.DamagePlayerByBoss(amount, p.transform.position);
    }
}

// =====================================================================
// 火 — 熔岩火柱
// =====================================================================
public class LavaPillar : BossSummon
{
    readonly Vector2 pos;
    readonly BossController boss;
    int hitCd;

    const float HitRadius = 85f;

    public LavaPillar(Vector2 pos, BossController boss) : base(150)   // 活性 2.5s
    {
        this.pos = pos;
        this.boss = boss;
        // 火柱素材竖长，中心抬高让"底座熔岩圈"落在 pos 上
        sr = MakeSprite("敌人/熔岩喷发", pos, 190f, GameConfig.OrderEffect, yOffset: 65f);
        go = sr.gameObject;
    }

    protected override void OnEnterActive()
    {
        if (Gm != null) Gm.PlaySfx("explosion");
    }

    protected override void ActiveTick(PlayerController p)
    {
        if (p == null) return;
        hitCd--;
        if (hitCd > 0) return;
        if (!InRadius(PlayerPos(p), pos, HitRadius)) return;

        hitCd = 30;   // 每 0.5 秒判定一次
        Hurt(p, Mathf.RoundToInt(boss.bulletDamage * 1.5f));
        // 附加灼烧：3 秒持续扣血，水大招可解除
        p.burnTimer = 180;
    }
}

// =====================================================================
// 水 — 海底漩涡
// =====================================================================
public class Whirlpool : BossSummon
{
    readonly Vector2 center;
    readonly BossController boss;
    int hitCd;

    const float PullRadius = 280f;
    const float CoreRadius = 100f;
    // 峰值拉力 3.4 < 玩家移速 6 → 反向移动可以挣脱（用户确认"可挣脱"）

    public Whirlpool(Vector2 center, BossController boss) : base(360)   // 活性 6s
    {
        this.center = center;
        this.boss = boss;
        sr = MakeSprite("敌人/海底漩涡", center, 480f, GroundOrder);
        go = sr.gameObject;
        baseColor = new Color(1f, 1f, 1f, 0.9f);
        if (sr != null) sr.color = new Color(1f, 1f, 1f, 0.15f);   // 预警期几乎透明地浮现
    }

    protected override void TelegraphTick(PlayerController p)
    {
        // 漩涡预警：从透明缓缓浮现，而不是闪烁
        if (sr != null)
            sr.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.15f, 0.55f, (float)t / TelegraphFrames));
    }

    protected override void ActiveTick(PlayerController p)
    {
        if (p == null) return;

        // 旋转动画（贴图本身是静态的，转起来就是活漩涡）
        if (go != null) go.transform.Rotate(0f, 0f, 1.6f);

        Vector2 pp = p.transform.position;
        float d = Vector2.Distance(pp, center);

        // 吸附：越近拉力越大，但始终小于玩家移速 → 可挣脱
        if (d < PullRadius && d > 2f)
        {
            float pull = Mathf.Lerp(3.4f, 0.9f, d / PullRadius);
            p.transform.position = Vector2.MoveTowards(pp, center, pull);
        }

        // 核心持续伤害
        if (d < CoreRadius)
        {
            hitCd--;
            if (hitCd <= 0)
            {
                hitCd = 30;
                Hurt(p, boss.bulletDamage + 4);
            }
        }
    }
}

// =====================================================================
// 雷 — 落雷（一波多道，先预警后齐劈）
// =====================================================================
public class ThunderWave : BossSummon
{
    class Bolt
    {
        public Vector2 pos;
        public SpriteRenderer sr;
        public bool hit;
    }

    readonly List<Bolt> bolts = new List<Bolt>();
    readonly BossController boss;
    const int StrikeFrame = 6;   // 进入活性后 0.1 秒劈下

    public ThunderWave(List<Vector2> positions, BossController boss) : base(45)   // 活性 0.75s
    {
        this.boss = boss;
        foreach (var pos in positions)
        {
            var b = new Bolt { pos = pos };
            // 预警期就显示落雷贴图的淡影（云+闪电轮廓），让玩家知道会劈哪
            b.sr = MakeSprite("敌人/落雷", pos, 200f, GameConfig.OrderEffect, yOffset: 55f);
            if (b.sr != null) b.sr.color = new Color(1f, 1f, 1f, 0.25f);
            bolts.Add(b);
        }
        go = bolts.Count > 0 ? bolts[0].sr.gameObject : null;
        // 各道闪电独立 GameObject，销毁时逐个删
    }

    protected override void TelegraphTick(PlayerController p)
    {
        foreach (var b in bolts)
            if (b.sr != null)
                b.sr.color = new Color(1f, 1f, 1f, 0.25f + 0.15f * Mathf.Sin(t * 0.5f));
    }

    protected override void OnEnterActive()
    {
        if (Gm != null) Gm.PlaySfx("explosion");
    }

    protected override void ActiveTick(PlayerController p)
    {
        // 劈下瞬间：全亮 + 一次性判定
        if (t == StrikeFrame)
        {
            int dmg = Mathf.RoundToInt(boss.bulletDamage * 2f);
            foreach (var b in bolts)
            {
                if (b.sr != null) b.sr.color = Color.white;
                if (p != null && !b.hit && InRadius(PlayerPos(p), b.pos, 95f))
                {
                    b.hit = true;
                    Hurt(p, dmg);
                }
            }
        }
    }

    public override void Douse()
    {
        // 落雷不受水熄灭影响，但也提前消散以免残留
        base.Douse();
    }

    public override void DestroyNow()
    {
        foreach (var b in bolts)
            if (b.sr != null) Object.Destroy(b.sr.gameObject);
        base.DestroyNow();
    }
}

// =====================================================================
// 岩 — 岩石突刺（一整排或列，从一端向另一端波浪式弹出，命中击退）
// =====================================================================
public class RockWave : BossSummon
{
    class Spike
    {
        public Vector2 pos;
        public SpriteRenderer sr;
        public bool hit;
        public int hotAt;      // 进入活性后第几帧开始弹出
    }

    readonly List<Spike> spikes = new List<Spike>();
    readonly BossController boss;
    readonly Vector2 waveDir;          // 波浪推进方向（也是击退方向）
    const int Spacing = 6;             // 每根石刺错开的帧数

    public RockWave(Vector2 playerPos, BossController boss) : base(110)   // 活性 ~1.8s
    {
        this.boss = boss;
        var pa = LevelManager.CurrentPlayArea;
        bool isRow = Random.value < 0.5f;

        int n;
        if (isRow)
        {
            n = 10;
            float y = Mathf.Clamp(playerPos.y, pa.yMin + 80f, pa.yMax - 80f);
            float x0 = pa.xMin + 70f, x1 = pa.xMax - 70f;
            bool ltr = Random.value < 0.5f;
            waveDir = ltr ? Vector2.right : Vector2.left;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                float x = ltr ? Mathf.Lerp(x0, x1, t) : Mathf.Lerp(x1, x0, t);
                Add(new Vector2(x, y), i);
            }
        }
        else
        {
            n = 7;
            float x = Mathf.Clamp(playerPos.x, pa.xMin + 80f, pa.xMax - 80f);
            bool topDown = Random.value < 0.5f;
            waveDir = topDown ? Vector2.down : Vector2.up;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                float y = topDown ? Mathf.Lerp(pa.yMax - 70f, pa.yMin + 70f, t)
                                  : Mathf.Lerp(pa.yMin + 70f, pa.yMax - 70f, t);
                Add(new Vector2(x, y), i);
            }
        }
        go = spikes.Count > 0 ? spikes[0].sr.gameObject : null;
    }

    void Add(Vector2 pos, int i)
    {
        var s = new Spike { pos = pos, hotAt = i * Spacing };
        s.sr = MakeSprite("敌人/岩石突刺", pos, 150f, GameConfig.OrderEffect, yOffset: 40f);
        if (s.sr != null) s.sr.color = new Color(1f, 1f, 1f, 0.18f);
        spikes.Add(s);
    }

    protected override void TelegraphTick(PlayerController p)
    {
        foreach (var s in spikes)
            if (s.sr != null)
                s.sr.color = new Color(1f, 1f, 1f, 0.18f + 0.12f * Mathf.Sin(t * 0.5f));
    }

    protected override void ActiveTick(PlayerController p)
    {
        int dmg = Mathf.RoundToInt(boss.bulletDamage * 2f);
        Vector2 pp = p != null ? PlayerPos(p) : Vector2.zero;

        foreach (var s in spikes)
        {
            if (s.sr == null) continue;
            int local = t - s.hotAt;
            if (local < 0) { s.sr.color = new Color(1f, 1f, 1f, 0.18f); continue; }   // 还没轮到

            if (local < 48)   // 弹出后 0.8 秒是危险期
            {
                s.sr.color = Color.white;
                if (p != null && !s.hit && InRadius(pp, s.pos, 62f))
                {
                    s.hit = true;
                    Hurt(p, dmg);
                    p.ApplyKnockback(waveDir, 420f);   // 沿突刺推进方向击退
                    if (Gm != null)
                        Gm.FloatingTextAt(pp + new Vector2(0f, 55f), "被击退!", new Color32(255, 170, 80, 255), false);
                }
            }
            else if (local < 60)
            {
                s.sr.color = new Color(1f, 1f, 1f, Mathf.Lerp(1f, 0.25f, (local - 48) / 12f));
            }
        }
    }

    public override void DestroyNow()
    {
        foreach (var s in spikes)
            if (s.sr != null) Object.Destroy(s.sr.gameObject);
        base.DestroyNow();
    }
}
