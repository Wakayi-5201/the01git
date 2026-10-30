using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Boss 基础控制器（第一版：通用三阶段骨架，不含每关专属行为）。
/// 阶段1 环形弹幕 → 阶段2 环形+追踪弹+召唤小怪 → 阶段3 更密+冲刺。
/// 出场时从地图上边缘滑入。
/// </summary>
public class BossController : MonoBehaviour
{
    public Element element;
    public float hp;
    public float maxHp;
    public int damageValue;
    public Vector2 halfExtents;
    public bool Dead;

    /// <summary>弹幕单发伤害 = 碰撞伤害 × 此比例（默认 40%，可在 LevelConfig 按关调整）。</summary>
    public const float BossBulletDamageMul = 0.40f;

    // ---------------------------------------------------------------
    // ★ Boss 弹幕参数。Boss 是运行时创建的，Inspector 里看不到，
    //   所以统一写在 LevelConfig.LevelData 里，由 LevelManager 生成时灌进来。
    //   想改 Boss 的弹速 / 射速 / 弹量，直接去 LevelConfig.cs 改即可。
    // ---------------------------------------------------------------
    [Header("Boss — 弹幕")]
    public float bulletSpeed = 5f;      // 子弹飞行速度
    public int shootIntervalP1 = 95;    // 阶段 1 射击间隔（帧，越小越密）
    public int shootIntervalP2 = 72;    // 阶段 2
    public int shootIntervalP3 = 55;    // 阶段 3
    public int ringCountP1 = 10;        // 阶段 1 每轮环形弹数量
    public int ringCountP2 = 12;        // 阶段 2
    public int ringCountP3 = 14;        // 阶段 3

    /// <summary>弹幕单发伤害。默认取碰撞伤害的 40%（弹幕量大，打满伤害会秒杀玩家）。</summary>
    public int bulletDamage = 8;

    public enum Phase { Entering, P1, P2, P3, Dead }
    public Phase phase = Phase.Entering;

    Vector2 enterTarget;
    const float EnterSpeed = 3.5f;

    int phase2Threshold = 0;   // hp <= maxHp*0.6 进 P2
    int phase3Threshold = 0;   // hp <= maxHp*0.3 进 P3

    int shootTimer;
    bool clockwise;
    float ringAngleOffset;

    // ---- 专属技能循环（用户需求：一轮常规弹幕 → 一轮专属技能，交替；死前必放技能） ----
    enum CastState { Volleys, SkillPause }
    CastState castState = CastState.Volleys;
    int castPauseTimer;          // 技能窗口期（不放弹幕）
    bool forcedLowHpSkill;       // 10% 血保底是否已触发
    public int SkillsCast { get; private set; }

    int summonTimer;
    int dashTimer;
    bool isDashing;
    Vector2 dashDir;
    int dashFrames;

    SpriteRenderer sr;
    Color baseColor;

    // ---------------------------------------------------------------
    /// <param name="size">Boss 世界尺寸基准值（会再乘 GameConfig.EntityScale）。玩家为 70。</param>
    public static BossController Create(Element element, int hp, int damage, string spriteFile,
                                       float size = 220f, float bulletDamageMul = BossBulletDamageMul)
    {
        var root = new GameObject("Boss");
        var sprite = AssetLibrary.GetSprite("敌人/" + spriteFile);
        float worldSize = size * GameConfig.EntityScale;
        var sr = SpriteFactory.NewSprite("Sprite", sprite, new Vector2(worldSize, worldSize),
            GameConfig.OrderEnemy, root.transform);

        var boss = root.AddComponent<BossController>();
        boss.element = element;
        boss.maxHp = hp;
        boss.hp = hp;
        boss.damageValue = damage;
        boss.bulletDamage = Mathf.Max(1, Mathf.RoundToInt(damage * bulletDamageMul));
        boss.halfExtents = new Vector2(worldSize * 0.4f, worldSize * 0.4f);
        boss.sr = sr;
        boss.baseColor = Color.white;

        // 出场位置：活动区（地图内沿）上边缘外侧。停留点按 Boss 半高内缩，避免大头漏在框外
        var pa = LevelManager.CurrentPlayArea;
        float half = worldSize * 0.5f;
        root.transform.position = new Vector3(pa.center.x, pa.yMax + half + 140f, 0f);
        boss.enterTarget = new Vector2(pa.center.x,
            Mathf.Clamp(pa.yMax - half - 20f, pa.yMin + half, pa.yMax - half));

        boss.phase2Threshold = Mathf.RoundToInt(hp * 0.6f);
        boss.phase3Threshold = Mathf.RoundToInt(hp * 0.3f);
        boss.shootTimer = 120;
        boss.summonTimer = 600;
        boss.dashTimer = 480;

        return boss;
    }

    // ---------------------------------------------------------------
    public void Step(Vector2 playerPos)
    {
        if (Dead) return;

        if (phase == Phase.Entering)
        {
            Vector2 cur = transform.position;
            Vector2 d = enterTarget - cur;
            float dist = d.magnitude;
            if (dist < EnterSpeed)
            {
                transform.position = enterTarget;
                phase = Phase.P1;
            }
            else
            {
                transform.position += (Vector3)(d / dist) * EnterSpeed;
            }
            return;
        }

        // 阶段切换（换阶段时强制放一轮专属技能）
        var prevPhase = phase;
        if (phase == Phase.P1 && hp <= phase2Threshold) phase = Phase.P2;
        if (phase == Phase.P2 && hp <= phase3Threshold) phase = Phase.P3;
        if (phase != prevPhase && phase != Phase.Dead)
            StartCast(playerPos);

        // 10% 血保底：确保 Boss 被打死之前至少放过一轮技能
        if (!forcedLowHpSkill && hp <= maxHp * 0.10f)
        {
            forcedLowHpSkill = true;
            StartCast(playerPos);
        }

        // 移动：阶段1/2 在场地中央附近游荡，阶段3 偶尔冲刺
        if (!isDashing)
        {
            Vector2 cur = transform.position;
            Rect pa = LevelManager.CurrentPlayArea;
            Vector2 center = new Vector2(pa.center.x, pa.yMin + pa.height * 0.55f);
            Vector2 d = center - cur;
            transform.position += (Vector3)d.normalized * 0.8f;
        }
        else
        {
            transform.position += (Vector3)(dashDir * 12f);
            dashFrames--;
            if (dashFrames <= 0) isDashing = false;
        }
        ClampToMap();

        // 射击 / 技能窗口
        if (castState == CastState.Volleys)
        {
            shootTimer--;
            if (shootTimer <= 0)
            {
                Shoot(playerPos);
                // 一轮常规弹幕放完 → 立刻放一轮专属技能（出场后第一轮弹幕就触发）
                StartCast(playerPos);
            }
        }
        else
        {
            castPauseTimer--;
            if (castPauseTimer <= 0)
            {
                castState = CastState.Volleys;
                shootTimer = 45;      // 技能结束后稍等片刻再开弹幕
            }
        }

        // 召唤小怪（P2/P3）
        if (phase != Phase.P1)
        {
            summonTimer--;
            if (summonTimer <= 0)
            {
                summonTimer = phase == Phase.P3 ? 480 : 720;
                GameManager.Instance.SummonMinion(element);
            }
        }

        // 冲刺（P3）
        if (phase == Phase.P3)
        {
            dashTimer--;
            if (dashTimer <= 0 && !isDashing)
            {
                dashTimer = 360;
                isDashing = true;
                dashFrames = 45;
                Vector2 d = playerPos - (Vector2)transform.position;
                dashDir = d.normalized;
            }
        }

        UpdateVisual();
    }

    void Shoot(Vector2 playerPos)
    {
        Vector2 cur = transform.position;

        // 环形弹幕（任意角度）
        int count = phase == Phase.P3 ? ringCountP3 : (phase == Phase.P2 ? ringCountP2 : ringCountP1);
        ringAngleOffset += clockwise ? 15f : -15f;
        for (int i = 0; i < count; i++)
        {
            float a = (i * 360f / count + ringAngleOffset) * Mathf.Deg2Rad;
            Vector2 vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * bulletSpeed;
            GameManager.Instance.SpawnEnemyBullet(cur, vel, bulletDamage);
        }

        // P2/P3 追踪弹
        if (phase != Phase.P1)
        {
            Vector2 d = (playerPos - cur).normalized * bulletSpeed * 1.2f;
            GameManager.Instance.SpawnEnemyBullet(cur, d, bulletDamage);
        }
    }

    // ---------------- 专属技能 ----------------
    void StartCast(Vector2 playerPos)
    {
        castState = CastState.SkillPause;
        castPauseTimer = 220;     // 技能窗口约 3.7 秒（期间不放弹幕，让玩家专注躲技能）
        CastSkill(playerPos);
    }

    void CastSkill(Vector2 playerPos)
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        var pa = LevelManager.CurrentPlayArea;

        string skillName;
        switch (element)
        {
            case Element.Fire:
            {
                // 熔岩火柱：玩家脚下 1 根 + 周围散布，P3 加密
                int n = phase == Phase.P3 ? 6 : 4;
                for (int i = 0; i < n; i++)
                {
                    Vector2 pos = i == 0 ? playerPos
                        : playerPos + new Vector2(Random.Range(-260f, 260f), Random.Range(-200f, 200f));
                    pos = new Vector2(Mathf.Clamp(pos.x, pa.xMin + 90f, pa.xMax - 90f),
                                      Mathf.Clamp(pos.y, pa.yMin + 90f, pa.yMax - 90f));
                    gm.AddBossSummon(new LavaPillar(pos, this));
                }
                skillName = "熔岩喷发!";
                break;
            }
            case Element.Water:
                // 海底漩涡：放在玩家当前位置附近，逼玩家走位
                Vector2 wc = new Vector2(
                    Mathf.Clamp(playerPos.x, pa.xMin + 280f, pa.xMax - 280f),
                    Mathf.Clamp(playerPos.y, pa.yMin + 240f, pa.yMax - 240f));
                gm.AddBossSummon(new Whirlpool(wc, this));
                skillName = "海底漩涡!";
                break;

            case Element.Thunder:
            {
                // 落雷：玩家附近 2 道 + 场地随机，P3 更多
                int n = phase == Phase.P3 ? 8 : 5;
                var pts = new List<Vector2>();
                for (int i = 0; i < n; i++)
                {
                    Vector2 pos = i < 2
                        ? playerPos + new Vector2(Random.Range(-140f, 140f), Random.Range(-120f, 120f))
                        : new Vector2(Random.Range(pa.xMin + 90f, pa.xMax - 90f),
                                      Random.Range(pa.yMin + 90f, pa.yMax - 90f));
                    pts.Add(new Vector2(Mathf.Clamp(pos.x, pa.xMin + 80f, pa.xMax - 80f),
                                        Mathf.Clamp(pos.y, pa.yMin + 80f, pa.yMax - 80f)));
                }
                gm.AddBossSummon(new ThunderWave(pts, this));
                skillName = "落雷!";
                break;
            }
            case Element.Rock:
                // 岩石突刺：一整排/列打向玩家位置
                gm.AddBossSummon(new RockWave(playerPos, this));
                skillName = "岩石突刺!";
                break;

            default:
                skillName = "";
                break;
        }

        SkillsCast++;
        if (skillName != "")
        {
            gm.FloatingTextAt(transform.position + new Vector3(0f, 100f, 0f),
                skillName, ElementDatabase.GetColor(element), true);
            gm.PlaySfx("boss_appear");
        }
    }

    void ClampToMap()
    {
        var pa = LevelManager.CurrentPlayArea;
        Vector3 p = transform.position;
        p.x = Mathf.Clamp(p.x, pa.xMin + 60f, pa.xMax - 60f);
        p.y = Mathf.Clamp(p.y, pa.yMin + 60f, pa.yMax - 60f);
        transform.position = p;
    }

    void UpdateVisual()
    {
        if (sr == null) return;
        sr.color = phase == Phase.P3 ? new Color32(255, 180, 180, 255) : baseColor;
    }

    // 被玩家子弹命中
    public bool TakeDamage(float dmg, out bool killed)
    {
        killed = false;
        if (Dead) return false;
        hp -= dmg;
        if (hp <= 0f)
        {
            hp = 0f;
            Dead = true;
            killed = true;
            phase = Phase.Dead;
        }
        return true;
    }

    public float HpFraction => maxHp > 0f ? Mathf.Clamp01(hp / maxHp) : 0f;
    public bool IsEntering() => phase == Phase.Entering;
}
