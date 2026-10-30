using UnityEngine;

/// <summary>
/// 跨关永久成长档。
///
/// 设计要点：把「永久成长」和「本局状态」拆开——
///   永久成长：生命上限 / 攻击 / 弹道行数 / 攻速等级 / 移速 / 能量上限 / 等级 / 经验
///   本局状态：当前 HP（进关满血）/ 能量 / 大招冷却 / 大招临时增益 / 分数 / 连击
///
/// 写入时机：**只有通关才 Commit 回存档**。死亡、ESC 回菜单、中途退出都丢弃本关收益，
/// 否则玩家可以在第 1 关反复送死刷属性，越刷越强。
///
/// 所有属性都有硬上限（见下方常量），保证弹道不会铺满屏幕、移速不会快到失控。
/// </summary>
[System.Serializable]
public class GrowthData
{
    // 值为 0 表示"尚未记录"，此时回退到 PlayerController 上的 start* 基准值
    public int maxHp = 0;
    public int damage = 0;
    public int bulletRows = 0;
    public int attackSpeedLevel = 0;
    public float moveSpeed = 0f;
    public int maxEnergy = 0;

    public int level = 0;
    public int exp = 0;
    public int expToNext = 0;
}

public static class PlayerGrowth
{
    // ---------------------------------------------------------------
    // ★ 永久成长硬上限（想放宽只改这里）
    // ---------------------------------------------------------------
    public const int MaxHp = 400;                 // 生命上限
    public const int MaxDamage = 200;            // 每发子弹伤害
    public const int MaxBulletRows = 5;          // 弹道行数（再多会铺满屏幕）
    public const int MaxAttackSpeedLevel = 8;    // 攻速等级（间隔 = Max(2, 9-lv)，8 级=2 帧/发）
    public const float MaxMoveSpeed = 9f;        // 移速（再高会在弹幕里"闪现"，手感失控）
    public const int MaxEnergy = 500;            // 能量上限（防大招放太频繁）

    // ---------------------------------------------------------------
    // ★ 敌人成长：玩家继承属性后杂兵必须跟上，否则后期就是割草
    // ---------------------------------------------------------------
    public const float EnemyHpPerClear = 0.35f;      // 每通关 1 关，杂兵血量 +35%
    public const float EnemyDamagePerClear = 0.18f;  // 伤害涨幅小于血量，避免被秒
    public const float EnemyDamageMulMax = 1.8f;     // 伤害倍数封顶

    /// <summary>已通关的普通关卡数（0~4）。</summary>
    public static int ClearedLevels =>
        Mathf.Clamp(SaveSystem.Data.maxUnlockedLevel, 0, LevelConfig.RegularLevelCount);

    /// <summary>杂兵血量倍数。</summary>
    public static float EnemyHpMul => 1f + ClearedLevels * EnemyHpPerClear;

    /// <summary>杂兵伤害倍数（封顶）。</summary>
    public static float EnemyDamageMul =>
        Mathf.Min(1f + ClearedLevels * EnemyDamagePerClear, EnemyDamageMulMax);

    static GrowthData Data => SaveSystem.Data.growth;

    // ---------------------------------------------------------------
    /// <summary>
    /// 进关时把存档里的永久成长应用到玩家。返回 true 表示确实继承了历史成长
    /// （false = 首次游玩，用的是 start* 基准值）。
    /// 注意：只写永久成长，HP 固定满血、能量清零等本局状态仍由 ResetPlayer 负责。
    /// </summary>
    public static bool Apply(PlayerController p)
    {
        if (p == null) return false;
        var g = Data;
        bool has = g.maxHp > 0;

        p.maxHp = Mathf.Clamp(has ? Mathf.Max(g.maxHp, p.startHp) : p.startHp, 1, MaxHp);
        p.damage = Mathf.Clamp(has ? Mathf.Max(g.damage, p.startDamage) : p.startDamage, 1, MaxDamage);
        p.bulletRows = Mathf.Clamp(has ? Mathf.Max(g.bulletRows, p.startBulletRows) : p.startBulletRows, 1, MaxBulletRows);
        p.attackSpeedLevel = Mathf.Clamp(has ? Mathf.Max(g.attackSpeedLevel, p.startAttackSpeedLevel)
                                             : p.startAttackSpeedLevel, 1, MaxAttackSpeedLevel);
        p.maxSpeed = Mathf.Clamp(has ? Mathf.Max(g.moveSpeed, p.startMaxSpeed) : p.startMaxSpeed, 1f, MaxMoveSpeed);
        p.maxEnergy = Mathf.Clamp(has ? Mathf.Max(g.maxEnergy, p.startMaxEnergy) : p.startMaxEnergy, 1, MaxEnergy);

        p.level = Mathf.Max(1, g.level);
        p.exp = Mathf.Max(0, g.exp);
        p.expToNext = g.expToNext > 0 ? g.expToNext : 100;

        return has;
    }

    /// <summary>
    /// 通关时把玩家当前属性写回存档（唯一写入口）。
    /// 只记录永久成长，本局临时增益（火系攻击加成、岩系护盾等）不写。
    /// </summary>
    public static void Commit(PlayerController p)
    {
        if (p == null) return;
        var g = Data;

        g.maxHp = Mathf.Clamp(p.maxHp, 1, MaxHp);
        g.damage = Mathf.Clamp(p.damage, 1, MaxDamage);
        g.bulletRows = Mathf.Clamp(p.bulletRows, 1, MaxBulletRows);
        g.attackSpeedLevel = Mathf.Clamp(p.attackSpeedLevel, 1, MaxAttackSpeedLevel);
        g.moveSpeed = Mathf.Clamp(p.maxSpeed, 1f, MaxMoveSpeed);
        g.maxEnergy = Mathf.Clamp(p.maxEnergy, 1, MaxEnergy);

        g.level = Mathf.Max(1, p.level);
        g.exp = Mathf.Max(0, p.exp);
        g.expToNext = Mathf.Max(1, p.expToNext);

        SaveSystem.Save();
    }

    /// <summary>清档：成长档一起归零（由 SaveSystem.ResetProgress 调用）。</summary>
    public static void Reset()
    {
        SaveSystem.Data.growth = new GrowthData();
    }

    /// <summary>给升级面板用的进度文案：如 "（2 / 上限 5）"。</summary>
    public static string Progress(float cur, float max)
    {
        return "（当前 " + Fmt(cur) + " / 上限 " + Fmt(max) + "）";
    }

    static string Fmt(float v) =>
        Mathf.Abs(v - Mathf.Round(v)) < 0.01f
            ? Mathf.RoundToInt(v).ToString()
            : v.ToString("0.0");
}
