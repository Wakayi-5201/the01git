using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 关卡参数表。四关 + 无尽模式的所有配置都在这里。
/// 地图世界尺寸 1920×1080（16:9，与素材同比例）；
/// playArea 是地图贴图"最里面的框"（牌桌内沿）对应的世界矩形，
/// 玩家/敌人/Boss 的活动范围都被限制在 playArea 内。
/// 内沿坐标由各地图贴图实测得出（归一化 0~1，y 自底向上）。
/// </summary>
public static class LevelConfig
{
    public const float WorldWidth = GameConfig.MapWidth;
    public const float WorldHeight = GameConfig.MapHeight;

    public enum Id : int { Level1 = 0, Level2, Level3, Level4, Endless }

    /// <summary>
    /// 某一波的刷怪配置。
    /// 设计约束（用户要求）：一波必须比上一波"更多、更强"，
    /// 因此数量(maxAlive)、血量(hpMul)、速度(speedMul)、
    /// 射击怪比例(shooterChance)、精英率(eliteChance)、
    /// 刷新频率(spawnInterval，越小越快) 六项都逐波递增。
    /// 用 W() 工厂方法创建，避免漏字段。
    /// </summary>
    public struct Wave
    {
        public float duration;        // 本波持续秒数
        public int maxAlive;          // 同屏最大数量
        public bool withElite;        // 是否可能出现精英（由 eliteChance 决定）
        public float hpMul;           // 血量倍数
        public float speedMul;        // 速度倍数
        public float shooterChance;   // 远程怪出现概率 0~1
        public float eliteChance;     // 精英怪出现概率 0~1
        public int spawnInterval;     // 生成间隔（帧，越小刷得越快）
    }

    /// <summary>波次工厂：一次写全六个递增维度。</summary>
    public static Wave W(float duration, int maxAlive, float hpMul, float speedMul,
                         float shooterChance, float eliteChance, int spawnInterval)
    {
        return new Wave
        {
            duration = duration,
            maxAlive = maxAlive,
            hpMul = hpMul,
            speedMul = speedMul,
            shooterChance = shooterChance,
            eliteChance = eliteChance,
            spawnInterval = spawnInterval,
            withElite = eliteChance > 0f,
        };
    }

    public class LevelData
    {
        public Id id;
        public string displayName;
        public string mapFile;           // Resources/Sprites/地图/...
        public string bossFile;          // Resources/Sprites/敌人/...
        public Element bossElement;
        public int bossHp;               // Boss 血量下限（玩家没升级时的保底值）
        public int bossDamage;           // Boss 碰撞伤害下限
        public float bossHpMul = 12f;    // Boss 血量 = 玩家 maxHp × 此倍数（与 bossHp 取大）
        public float bossDamagePct;      // Boss 碰撞伤害 = 玩家 maxHp × 此比例（与 bossDamage 取大）
        public float bossSize = 220f;    // Boss 世界尺寸（会再乘 GameConfig.EntityScale）

        // Boss 弹幕（生成时写入 BossController）
        public float bossBulletSpeed = 5.2f;  // 弹速
        public int bossShootP1 = 95;        // 阶段1 射击间隔（帧）
        public int bossShootP2 = 72;        // 阶段2
        public int bossShootP3 = 55;        // 阶段3
        public int bossRingP1 = 10;         // 阶段1 每轮环形弹数量
        public int bossRingP2 = 12;         // 阶段2
        public int bossRingP3 = 14;         // 阶段3
        public float bossBulletDamageMul = 0.40f;  // 弹幕单发伤害 = 碰撞伤害 × 此比例
        public Element[] enemyPool;      // 本关普通敌人属性池
        public bool poolRestricted;      // true = 只刷某个元素能克制的属性（首关避免玩家免疫）
        public Waves waves;
        public Rect playArea;            // 活动区（世界坐标），即地图贴图最里侧边框的内沿
    }

    public class Waves
    {
        public Wave wave1;
        public Wave wave2;
        public Wave wave3;
    }

    // -----------------------------------------------------------------------
    /// <summary>把归一化内沿坐标(x0,x1,y0,y1，y自底)转换为世界矩形。</summary>
    static Rect PA(float x0, float x1, float y0, float y1) =>
        new Rect(x0 * WorldWidth, y0 * WorldHeight,
                 (x1 - x0) * WorldWidth, (y1 - y0) * WorldHeight);

    public static readonly LevelData[] All =
    {
        // 关 1：熔核巨像（火 Boss），首关只刷火克制的敌人，避免玩家免疫
        new LevelData
        {
            id = Id.Level1,
            displayName = "熔岩裂谷",
            mapFile = "火地图",
            bossFile = "火属性boss",
            bossElement = Element.Fire,
            bossHp = 1200,
            bossDamage = 20,
            bossHpMul = 12f,
            bossDamagePct = 0.20f,
            bossSize = 220f,
            enemyPool = new[] { Element.Ice, Element.Water },   // 火克制冰/水
            poolRestricted = true,
            playArea = PA(0.200f, 0.799f, 0.252f, 0.745f),      // 火地图牌桌内沿(实测)
            waves = new Waves
            {
                //          时长  数量  血量   速度   远程率 精英率 刷新间隔(帧)
                wave1 = W(  20f,   5,  1.00f, 1.00f, 0.20f, 0.00f, 70 ),
                wave2 = W(  35f,   7,  1.50f, 1.12f, 0.35f, 0.10f, 52 ),
                wave3 = W(  45f,   9,  2.10f, 1.25f, 0.50f, 0.22f, 38 ),
            },
        },

        // 关 2：深渊守望者（水 Boss）
        new LevelData
        {
            id = Id.Level2,
            displayName = "深海遗迹",
            mapFile = "水地图",
            bossFile = "水属性boss",
            bossElement = Element.Water,
            bossHp = 1800,
            bossDamage = 24,
            bossHpMul = 15f,
            bossDamagePct = 0.22f,
            bossSize = 235f,
            bossBulletSpeed = 5.5f, bossShootP1 = 85, bossShootP2 = 65, bossShootP3 = 50,
            bossRingP1 = 10, bossRingP2 = 12, bossRingP3 = 14,
            enemyPool = new[] { Element.Fire, Element.Thunder },
            poolRestricted = false,
            playArea = PA(0.199f, 0.800f, 0.260f, 0.735f),      // 水地图内沿(实测)
            waves = new Waves
            {
                wave1 = W(  25f,   6,  1.15f, 1.05f, 0.25f, 0.05f, 66 ),
                wave2 = W(  40f,   8,  1.65f, 1.18f, 0.38f, 0.14f, 50 ),
                wave3 = W(  45f,   9,  2.30f, 1.30f, 0.52f, 0.26f, 36 ),
            },
        },

        // 关 3：雷霆之主（雷 Boss）
        new LevelData
        {
            id = Id.Level3,
            displayName = "雷鸣高塔",
            mapFile = "雷地图",
            bossFile = "雷属性boss",
            bossElement = Element.Thunder,
            bossHp = 2400,
            bossDamage = 28,
            bossHpMul = 18f,
            bossDamagePct = 0.25f,
            bossSize = 250f,
            bossBulletSpeed = 5.8f, bossShootP1 = 78, bossShootP2 = 60, bossShootP3 = 46,
            bossRingP1 = 11, bossRingP2 = 13, bossRingP3 = 15,
            enemyPool = new[] { Element.Fire, Element.Water, Element.Ice },
            poolRestricted = false,
            playArea = PA(0.190f, 0.820f, 0.240f, 0.760f),      // 雷地图内沿(实测)
            waves = new Waves
            {
                wave1 = W(  30f,   7,  1.30f, 1.08f, 0.28f, 0.08f, 62 ),
                wave2 = W(  45f,   9,  1.90f, 1.22f, 0.42f, 0.18f, 46 ),
                wave3 = W(  50f,  10,  2.60f, 1.34f, 0.55f, 0.30f, 34 ),
            },
        },

        // 关 4：碎岩巨像（岩 Boss）
        new LevelData
        {
            id = Id.Level4,
            displayName = "岩山废矿",
            mapFile = "岩地图",
            bossFile = "岩属性boss",
            bossElement = Element.Rock,
            bossHp = 3200,
            bossDamage = 32,
            bossHpMul = 22f,
            bossDamagePct = 0.28f,
            bossSize = 270f,
            bossBulletSpeed = 6.2f, bossShootP1 = 72, bossShootP2 = 55, bossShootP3 = 42,
            bossRingP1 = 12, bossRingP2 = 14, bossRingP3 = 16,
            enemyPool = new[] { Element.Fire, Element.Water, Element.Thunder, Element.Ice },
            poolRestricted = false,
            playArea = PA(0.159f, 0.835f, 0.168f, 0.772f),      // 岩地图内沿(实测)
            waves = new Waves
            {
                wave1 = W(  35f,   8,  1.45f, 1.12f, 0.32f, 0.12f, 58 ),
                wave2 = W(  50f,  10,  2.10f, 1.26f, 0.46f, 0.22f, 44 ),
                wave3 = W(  55f,  10,  2.90f, 1.38f, 0.58f, 0.34f, 32 ),
            },
        },

        // 无尽模式（参数参考关 4，轮替地图由 EndlessManager 额外处理）
        new LevelData
        {
            id = Id.Endless,
            displayName = "无尽模式",
            mapFile = "火地图",
            bossFile = "火属性boss",
            bossElement = Element.Fire,
            bossHp = 2000,
            bossDamage = 24,
            bossHpMul = 18f,
            bossDamagePct = 0.24f,
            bossSize = 240f,
            bossBulletSpeed = 5.8f, bossShootP1 = 80, bossShootP2 = 62, bossShootP3 = 48,
            bossRingP1 = 11, bossRingP2 = 13, bossRingP3 = 15,
            enemyPool = new[] { Element.Fire, Element.Ice, Element.Water, Element.Thunder, Element.Rock },
            poolRestricted = false,
            playArea = PA(0.200f, 0.799f, 0.252f, 0.745f),      // 无尽用火地图，同关1
            waves = new Waves
            {
                wave1 = W(  30f,   8,  1.40f, 1.10f, 0.30f, 0.10f, 60 ),
                wave2 = W(  45f,  10,  2.00f, 1.24f, 0.45f, 0.20f, 46 ),
                wave3 = W(  55f,  10,  2.80f, 1.36f, 0.56f, 0.32f, 34 ),
            },
        },
    };

    public static LevelData Get(Id id) => All[(int)id];

    /// <summary>普通关卡数（不含无尽）。</summary>
    public const int RegularLevelCount = 4;
}
