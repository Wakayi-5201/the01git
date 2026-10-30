using UnityEngine;

/// <summary>
/// 彩蛋：哆啦A梦。
/// 每一关开始固定站在地图右下角（活动区内角），不攻击玩家、也不受伤害
/// （子弹直接穿过，因为不注册进 GameManager.enemies）。
/// 玩家靠近一定距离后，在它周围随机掉落 3 个不重复的道具，每关仅一次。
/// 生命周期由 LevelManager 控制：StartLevel 生成，StopLevel / 回菜单时销毁。
/// </summary>
public class DoraemonEgg : MonoBehaviour
{
    // ================================================================
    // ★★★ 哆啦A梦调参区 —— 想改大小 / 站位 / 触发距离，只改这 4 行 ★★★
    // 世界坐标单位：相机视野高 540，玩家身高约 49，普通小怪约 49、Boss 约 154。
    // 想让它在屏幕上更大/更小，只改 DisplaySize（当前 150 ≈ 屏幕高度的 28%）。
    // ================================================================
    const float DisplaySize    = 150f;   // 显示边长（原 90，现在约 1.7 倍）
    const float TriggerRadius  = 340f;   // 玩家走近多少距离掉落礼物
    const float EdgeInset      = 140f;   // 离活动区右下角的内缩（越大越靠里）
    const float BobAmplitude   = 5f;     // 原地上下浮动幅度
    // ================================================================

    [Header("哆啦A梦 — 彩蛋调参（运行时由上方常量覆盖）")]
    [Tooltip("玩家靠近多少距离触发掉落")]
    public float triggerRadius = TriggerRadius;
    [Tooltip("哆啦A梦的世界尺寸")]
    public float worldSize = DisplaySize;

    /// <summary>当前场上唯一的哆啦A梦（null = 没有）。</summary>
    public static DoraemonEgg Current;

    SpriteRenderer sr;
    Vector3 basePos;
    bool delivered;      // 本关是否已送过礼物
    float bobPhase;

    // ---------------------------------------------------------------
    /// <summary>关卡开始时调用：在活动区右下角生成哆啦A梦。</summary>
    public static DoraemonEgg Create()
    {
        // 保险：先清掉上一只
        if (Current != null) Destroy(Current.gameObject);
        Current = null;

        var sprite = AssetLibrary.GetSprite("敌人/哆啦A梦");
        if (sprite == null)
        {
            Debug.LogWarning("[DoraemonEgg] 找不到 Sprites/敌人/哆啦A梦。" +
                "请确认图片在 Resources/Sprites/敌人 下，且导入类型为 Sprite (2D and UI)。");
            return null;
        }

        var root = new GameObject("DoraemonEasterEgg");
        var spriteRenderer = SpriteFactory.NewSprite("Sprite", sprite,
            new Vector2(DisplaySize, DisplaySize), GameConfig.OrderEnemy, root.transform);

        // 站位：活动区右下角，内缩 EdgeInset 避免贴边被地图边框压住
        var pa = LevelManager.CurrentPlayArea;
        Vector2 p = new Vector2(pa.xMax - EdgeInset, pa.yMin + EdgeInset);
        root.transform.position = new Vector3(p.x, p.y, 0f);

        var egg = root.AddComponent<DoraemonEgg>();
        egg.sr = spriteRenderer;
        egg.basePos = root.transform.position;
        egg.bobPhase = Random.value * Mathf.PI * 2f;
        egg.triggerRadius = TriggerRadius;
        egg.worldSize = DisplaySize;
        Current = egg;
        return egg;
    }

    /// <summary>关卡结束 / 回菜单时调用：销毁彩蛋。</summary>
    public static void Clear()
    {
        if (Current != null) Destroy(Current.gameObject);
        Current = null;
    }

    void OnDestroy()
    {
        if (Current == this) Current = null;
    }

    // ---------------------------------------------------------------
    void Update()
    {
        var gm = GameManager.Instance;

        // 关卡已结束（含 ESC 回菜单这类不经过 StopLevel 的路径）：自我清理
        if (gm == null || gm.state != GameState.Playing)
        {
            Destroy(gameObject);
            return;
        }

        var player = gm.player;
        if (player == null || !player.gameObject.activeInHierarchy) return;

        // 原地轻微上下浮动，让它看起来"活着"，但不会离开右下角
        if (sr != null)
        {
            transform.position = basePos + Vector3.up * (Mathf.Sin(Time.time * 2f + bobPhase) * BobAmplitude);
        }

        // 玩家靠近 → 送礼物（每关一次）
        if (!delivered)
        {
            float dist = Vector2.Distance(transform.position, player.transform.position);
            if (dist <= triggerRadius) DeliverGifts();
        }
    }

    // ---------------------------------------------------------------
    /// <summary>在哆啦A梦周围随机掉落 3 个不重复的道具。</summary>
    void DeliverGifts()
    {
        delivered = true;

        var gm = GameManager.Instance;
        var pa = LevelManager.CurrentPlayArea;
        Vector2 center = transform.position;

        // 6 种道具洗牌后取前 3 个：保证 3 个道具不重复
        var types = new[]
        {
            ItemType.Heal, ItemType.Energy, ItemType.Magnet,
            ItemType.AtkUp, ItemType.MaxHpUp, ItemType.Guard
        };
        for (int i = types.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (types[i], types[j]) = (types[j], types[i]);
        }

        for (int i = 0; i < 3; i++)
        {
            // 哆啦A梦四周随机方向、随机半径落点，并夹回活动区内
            // 半径随体型一起放大，道具才不会全挤在身体底下
            float ang = Random.Range(0f, Mathf.PI * 2f);
            float r = Random.Range(DisplaySize * 0.65f, DisplaySize * 1.35f);
            Vector2 pos = center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
            pos.x = Mathf.Clamp(pos.x, pa.xMin + 40f, pa.xMax - 40f);
            pos.y = Mathf.Clamp(pos.y, pa.yMin + 40f, pa.yMax - 40f);
            ItemPickup.Create(pos, types[i]);
        }

        if (gm != null)
        {
            gm.FloatingTextAt(center + new Vector2(0f, DisplaySize * 0.55f), "叮咚~ 哆啦A梦的礼物!", GameConfig.Cyan, true);
            gm.PlaySfx("hit");
        }
    }
}
