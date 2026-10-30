using System.Collections.Generic;
using UnityEngine;

public enum Element { Fire, Ice, Water, Thunder, Rock }

/// <summary>
/// 元素数据：名称、颜色、贴图文件名、伤害倍率表、元素反应表。
/// 对应 pygame 的 ELEMENTS / ENEMY_ELEMENTS / DAMAGE_TABLE / REACTION_NAMES。
/// </summary>
public static class ElementDatabase
{
    public static readonly Element[] AllElements = { Element.Fire, Element.Ice, Element.Water, Element.Thunder, Element.Rock };
    public static readonly Element[] EnemyElements = { Element.Fire, Element.Ice, Element.Water, Element.Thunder };

    // ---- 名称 ----
    static readonly Dictionary<Element, string> Names = new Dictionary<Element, string>
    {
        { Element.Fire, "火" }, { Element.Ice, "冰" }, { Element.Water, "水" },
        { Element.Thunder, "雷" }, { Element.Rock, "岩" }
    };

    // ---- 颜色 ----
    static readonly Dictionary<Element, Color> Colors = new Dictionary<Element, Color>
    {
        { Element.Fire,    new Color32(255, 150, 30, 255) },
        { Element.Ice,     new Color32(150, 220, 255, 255) },
        { Element.Water,   new Color32(60, 120, 255, 255) },
        { Element.Thunder, new Color32(200, 60, 255, 255) },
        { Element.Rock,    new Color32(139, 90, 43, 255) },
    };

    // ---- 贴图文件名（相对 Resources/Sprites，不含扩展名） ----
    public static string PlayerIdleFile(Element e)   => Names[e] + "角色";
    public static string PlayerActionFile(Element e) => Names[e] + "角色动作";
    public static string EnemyFile(Element e)        => Names[e] + "敌人";
    public static string AvatarFile(Element e)       => Names[e] + "角色头像";

    public static string BulletFile(Element e)
    {
        switch (e)
        {
            case Element.Fire:    return "火焰子弹";
            case Element.Ice:     return "雪花子弹";
            case Element.Water:   return "水滴子弹";
            case Element.Thunder: return "闪电子弹";
            default:              return "岩石子弹";
        }
    }

    public static string UltimateFile(Element e)
    {
        switch (e)
        {
            case Element.Fire:    return "火角色大招";
            case Element.Ice:     return "冰角色大招";
            case Element.Thunder: return "雷角色大招";
            case Element.Rock:    return "岩角色大招";
            default:              return "水角色大招召唤物1"; // 水属性有 1/2/3 三个召唤物
        }
    }

    // ---- 伤害倍率表（攻击方 -> 防守方） ----
    static readonly Dictionary<Element, Dictionary<Element, float>> DamageTable =
        new Dictionary<Element, Dictionary<Element, float>>
    {
        { Element.Fire, new Dictionary<Element, float> {
            { Element.Water, 1.5f }, { Element.Ice, 2.0f }, { Element.Thunder, 1.5f } } },
        { Element.Ice, new Dictionary<Element, float> {
            { Element.Fire, 1.5f }, { Element.Water, 1.5f }, { Element.Thunder, 2.0f } } },
        { Element.Water, new Dictionary<Element, float> {
            { Element.Fire, 2.0f }, { Element.Ice, 1.5f }, { Element.Thunder, 1.5f } } },
        { Element.Thunder, new Dictionary<Element, float> {
            { Element.Fire, 2.0f }, { Element.Ice, 1.5f }, { Element.Water, 1.5f } } },
        { Element.Rock, new Dictionary<Element, float> {
            { Element.Fire, 1.5f }, { Element.Ice, 1.5f }, { Element.Water, 1.5f }, { Element.Thunder, 1.5f } } },
    };

    // ---- 元素反应表 ----
    static readonly Dictionary<(Element, Element), string> Reactions =
        new Dictionary<(Element, Element), string>
    {
        { (Element.Fire, Element.Ice),     "融化" },
        { (Element.Ice, Element.Fire),     "融化" },
        { (Element.Fire, Element.Water),   "蒸发" },
        { (Element.Water, Element.Fire),   "蒸发" },
        { (Element.Fire, Element.Thunder), "超载" },
        { (Element.Thunder, Element.Fire), "超载" },
        { (Element.Water, Element.Ice),    "冻结" },
        { (Element.Ice, Element.Water),    "冻结" },
        { (Element.Water, Element.Thunder),"感电" },
        { (Element.Thunder, Element.Water),"感电" },
        { (Element.Ice, Element.Thunder),  "超导" },
        { (Element.Thunder, Element.Ice),  "超导" },
    };

    public const string ReactionCrystal = "结晶";   // 岩 -> 任意
    public const string ReactionFreeze = "冻结";
    public const string ReactionKnockback = "超载";

    public static string GetName(Element e) => Names[e];
    public static Color GetColor(Element e) => Colors[e];

    public static float GetMultiplier(Element attacker, Element defender)
    {
        if (DamageTable.TryGetValue(attacker, out var row) && row.TryGetValue(defender, out var m))
            return m;
        return 1.0f;
    }

    /// <summary>返回反应名，没有反应返回 null。岩属性攻击任意敌人都是“结晶”。</summary>
    public static string GetReaction(Element attacker, Element defender)
    {
        if (attacker == Element.Rock) return ReactionCrystal;
        if (Reactions.TryGetValue((attacker, defender), out var name)) return name;
        return null;
    }
}