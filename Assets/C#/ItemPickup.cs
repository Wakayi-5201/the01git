using System.Collections.Generic;
using UnityEngine;

/// <summary>道具类型。图鉴/掉落/拾取共用。</summary>
public enum ItemType { Heal = 0, Energy = 1, Magnet = 2, AtkUp = 3, MaxHpUp = 4, Guard = 5 }

/// <summary>
/// 道具静态数据表。素材从 Resources/Sprites/道具/&lt;key&gt;.png 读取；
/// 缺失时自动回退到子弹素材 + 染色占位，保证随时可玩。
/// </summary>
public static class ItemDB
{
    public struct Info
    {
        public ItemType type;
        public string key;          // 存档/图鉴/素材文件名共用
        public string name;
        public string desc;
        public string spritePath;   // 正式素材路径
        public string fallbackPath; // 占位素材路径（现有子弹图）
        public Color32 tint;        // 占位图染色
    }

    public static readonly Info[] All =
    {
        new Info{ type=ItemType.Heal,    key="血包",     name="烤鸡大餐", desc="回复 30% 最大生命",
                  spritePath="道具/血包",       fallbackPath="子弹/雪花子弹", tint=new Color32(255,150,90,255) },
        new Info{ type=ItemType.Energy,  key="能量水晶", name="能量水晶", desc="能量 +50",
                  spritePath="道具/能量水晶",   fallbackPath="子弹/水滴子弹", tint=new Color32(120,230,255,255) },
        new Info{ type=ItemType.Magnet,  key="磁铁",     name="黄金磁币", desc="分数 +200、经验 +60，并吸走场上掉落物",
                  spritePath="道具/磁铁",       fallbackPath="子弹/闪电子弹", tint=new Color32(255,225,100,255) },
        new Info{ type=ItemType.AtkUp,   key="攻击力",   name="烈焰之羽", desc="攻击力 +30%，持续 45 秒",
                  spritePath="道具/攻击力",     fallbackPath="子弹/火焰子弹", tint=new Color32(255,200,80,255) },
        new Info{ type=ItemType.MaxHpUp, key="生命上限", name="生命之花", desc="生命上限 +20 并回复 20（本局）",
                  spritePath="道具/生命上限",   fallbackPath="子弹/岩石子弹", tint=new Color32(255,110,120,255) },
        new Info{ type=ItemType.Guard,   key="伤害减免", name="守护羽帽", desc="获得 3 层护盾，抵消 3 次伤害",
                  spritePath="道具/伤害减免",   fallbackPath="子弹/雪花子弹", tint=new Color32(140,190,255,255) },
    };

    public static Info Get(ItemType t) => All[(int)t];

    /// <summary>取渲染用精灵；返回 (sprite, 是否占位图)。</summary>
    public static (Sprite, bool) Load(Info info)
    {
        var s = AssetLibrary.GetSprite(info.spritePath);
        if (s != null) return (s, false);
        return (AssetLibrary.GetSprite(info.fallbackPath), true);
    }
}

/// <summary>图鉴计数器：敌人击杀 / 道具拾取，写入 SaveSystem。</summary>
public static class Codex
{
    public static void RecordEnemy(Enemy e)
    {
        string key = ElementDatabase.GetName(e.element) + (e.isBoss ? "BOSS" : "");
        SaveSystem.BumpCount(SaveSystem.Data.enemyKills, key);
    }

    public static void RecordItem(ItemType t)
        => SaveSystem.BumpCount(SaveSystem.Data.itemPickups, ItemDB.Get(t).key);

    public static int EnemyKills(string key) => SaveSystem.GetCount(SaveSystem.Data.enemyKills, key);
    public static int ItemPicked(string key) => SaveSystem.GetCount(SaveSystem.Data.itemPickups, key);
}

/// <summary>
/// 场景掉落物：敌人死亡时按概率掉落，靠近自动拾取。
/// 上下漂浮 + 快消失时闪烁；磁石可把全场掉落物吸向玩家。
/// </summary>
public class ItemPickup : MonoBehaviour
{
    public const float PickupRadius = 85f;   // 世界单位，拾取距离（原 45 太小，经常擦肩而过）
    public const float LifeSeconds = 15f;    // 存留时间
    const float BobAmp = 6f;                 // 上下漂浮幅度
    const float MagnetSpeed = 520f;          // 被磁化后的飞行速度

    static readonly List<ItemPickup> active = new List<ItemPickup>();

    public ItemType Type;
    SpriteRenderer sr;
    Vector3 basePos;
    float life, phase;
    bool magnetized;

    // ---------------- 掉落 ----------------
    /// <summary>敌人死亡调用。普通怪 12% 掉率，BOSS 必掉。</summary>
    public static void TryDrop(Vector2 pos, bool isBoss)
    {
        float chance = isBoss ? 1f : 0.12f;
        if (Random.value > chance) return;

        // 权重：血包 35 / 能量 30 / 磁铁 15 / 攻击羽 10 / 生命花 6 / 羽帽 4（越强越稀有）
        int roll = Random.Range(0, 100);
        ItemType t = roll < 35 ? ItemType.Heal
                   : roll < 65 ? ItemType.Energy
                   : roll < 80 ? ItemType.Magnet
                   : roll < 90 ? ItemType.AtkUp
                   : roll < 96 ? ItemType.MaxHpUp
                   : ItemType.Guard;
        Create(pos, t);
    }

    public static void Create(Vector2 pos, ItemType type)
    {
        var info = ItemDB.Get(type);
        var (sprite, isFallback) = ItemDB.Load(info);
        if (sprite == null) return;

        var go = new GameObject("Item_" + info.key, typeof(SpriteRenderer));
        go.transform.position = pos;
        var sr = go.GetComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = isFallback ? (Color)info.tint : Color.white;   // 正式素材不染色
        sr.sortingOrder = 6;

        // 世界尺寸归一：按精灵实际大小缩到约 40 单位
        var b = sprite.bounds;
        float maxSize = Mathf.Max(b.size.x, b.size.y);
        if (maxSize > 0.001f) go.transform.localScale = Vector3.one * (40f / maxSize);

        var p = go.AddComponent<ItemPickup>();
        p.Type = type;
        p.sr = sr;
        p.basePos = pos;
        p.life = LifeSeconds;
        p.phase = Random.value * Mathf.PI * 2f;
        active.Add(p);
    }

    /// <summary>磁石效果：全场掉落物飞向玩家。</summary>
    public static void MagnetizeAll(ItemPickup source)
    {
        for (int i = active.Count - 1; i >= 0; i--)
            if (active[i] != null && active[i] != source) active[i].magnetized = true;
    }

    void OnDestroy()
    {
        active.Remove(this);
    }

    void Update()
    {
        var gm = GameManager.Instance;
        var player = gm != null ? gm.player : null;
        if (player == null) return;

        life -= Time.deltaTime;
        if (life <= 0f) { Destroy(gameObject); return; }

        // 快消失时闪烁提醒
        sr.enabled = life > 4f || Mathf.FloorToInt(life * 8f) % 2 == 0;

        Vector3 pos;
        if (magnetized)
        {
            pos = Vector3.MoveTowards(transform.position, player.transform.position, MagnetSpeed * Time.deltaTime);
        }
        else
        {
            // 原地漂浮
            pos = basePos + Vector3.up * (Mathf.Sin(Time.time * 3f + phase) * 2.5f);
            transform.position = pos;
        }

        // 拾取判定
        float dist = Vector2.Distance(transform.position, player.transform.position);
        if (dist < PickupRadius || (magnetized && dist < PickupRadius * 2f))
        {
            Pickup(player);
            Destroy(gameObject);
        }
    }

    void Pickup(PlayerController player)
    {
        var info = ItemDB.Get(Type);
        var gm = GameManager.Instance;
        if (gm == null) return;

        switch (Type)
        {
            case ItemType.Heal:
                int amount = Mathf.RoundToInt(player.maxHp * 0.3f);
                player.hp = Mathf.Min(player.maxHp, player.hp + amount);
                gm.FloatingTextAt(player.transform.position + new Vector3(0f, 40f, 0f),
                    "回血 +" + amount, new Color32(120, 255, 140, 255), true);
                break;

            case ItemType.Energy:
                player.AddEnergy(50);
                gm.FloatingTextAt(player.transform.position + new Vector3(0f, 40f, 0f),
                    "能量 +50", new Color32(120, 210, 255, 255), true);
                break;

            case ItemType.Magnet:
                MagnetizeAll(this);
                gm.score += 200;
                player.AddExp(60);
                gm.FloatingTextAt(player.transform.position + new Vector3(0f, 40f, 0f),
                    "金币 +200分 +60经验", new Color32(255, 225, 110, 255), true);
                break;

            case ItemType.AtkUp:
                player.itemAtkTimer = 45 * 60;      // 45 秒（按 60fps 计帧，与其它计时器一致）
                player.itemAtkBonus = 0.30f;
                gm.FloatingTextAt(player.transform.position + new Vector3(0f, 40f, 0f),
                    "攻击 +30%!", new Color32(255, 200, 90, 255), true);
                break;

            case ItemType.MaxHpUp:
                player.maxHp += 20;
                player.hp += 20;
                gm.FloatingTextAt(player.transform.position + new Vector3(0f, 40f, 0f),
                    "生命上限 +20", new Color32(255, 120, 130, 255), true);
                break;

            case ItemType.Guard:
                player.rockShieldCount += 3;        // 复用岩系护盾机制
                gm.FloatingTextAt(player.transform.position + new Vector3(0f, 40f, 0f),
                    "守护羽帽: 护盾 +3", new Color32(150, 200, 255, 255), true);
                break;
        }

        gm.PlaySfx("hit");
        Codex.RecordItem(Type);

        // 屏幕中下方弹提示：让玩家知道刚捡的东西有什么用
        if (HUD.Instance != null)
            HUD.Instance.ShowItemToast(info.name, info.desc, info.tint);
    }
}
