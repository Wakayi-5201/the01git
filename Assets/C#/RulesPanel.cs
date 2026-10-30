using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 游戏规则（可滚动）。对应 pygame 的 draw_rules。
/// 规则文字已内嵌在代码里，不需要手工逐行摆放。
/// </summary>
public class RulesPanel : MonoBehaviour
{
    [Header("背景 / 标题")]
    public Image backgroundImage;
    public Text titleText;

    [Header("滚动视图")]
    public ScrollRect scrollRect;
    public Text contentText;

    [Header("返回")]
    public Button backButton;

    public const string RulesText =
@"【操作方式】
  WASD - 移动战机
  方向键(↑↓←→) - 控制射击方向，子弹自动发射
  F / E / X / J - 释放元素大招（能量满时可用）
  Q / R - 切换角色属性（也可点击右侧属性头像）
  数字键 1-3 - 选择升级卡片（也可直接点击卡片）
  P - 暂停   ESC - 返回主菜单

【角色属性】
  火  冰  水  雷  岩
  初始只有火属性，升级时可选择解锁其它属性

【属性克制】（攻击方 → 被攻击方）
  火 → 冰(融化 2.0倍)   火 → 水(蒸发 1.5倍)   火 → 雷(超载 1.5倍)
  冰 → 火(融化 1.5倍)   冰 → 水(冻结 1.5倍)   冰 → 雷(超导 2.0倍)
  水 → 火(蒸发 2.0倍)   水 → 冰(冻结 1.5倍)   水 → 雷(感电 1.5倍)
  雷 → 火(超载 2.0倍)   雷 → 冰(超导 1.5倍)   雷 → 水(感电 2.0倍)
  岩 → 所有属性(结晶 1.5倍)
  注意：同属性攻击无效！

【反应附加效果】
  冻结 - 敌人无法移动 2 秒
  减速 - 敌人移动速度减半 2.5 秒
  击退 - 敌人被击飞

【敌人】
  普通敌人 - 只会冲撞攻击
  射击者（红点标记）- 会发射子弹，注意躲避
  精英怪 - 血更厚、更大只
  BOSS - 每关关底出现，见下方说明

【BOSS 战】
  BOSS 出场时从地图上方滑入，分三个阶段：
    阶段1（100%~60% 血）：环形弹幕
    阶段2（60%~30% 血）：弹幕加密 + 追踪弹 + 召唤小怪
    阶段3（30% 以下）  ：弹幕最密 + 向你冲刺
  血条显示在屏幕顶部中央
  弹幕单发伤害较低，真正危险的是被 BOSS 本体撞到

  BOSS 专属技能（一轮弹幕后放一轮技能，交替进行）：
    火Boss 熔岩喷发 - 地面熔岩圈预警后喷出火柱，
      踩中受伤并附加 3 秒灼烧（持续扣血，水大招可解除）
    水Boss 海底漩涡 - 漩涡把你拉向中心，核心内持续受伤，
      拉力小于移速，反向移动可以挣脱
    雷Boss 落雷 - 天空预警后闪电劈下，落点一次爆发伤害
    岩Boss 岩石突刺 - 一整排/列石刺从一端向另一端弹出，
      被击中会受到伤害并被击退
  所有技能都有 1 秒预警（半透明闪烁），看到预警立刻走位！
  换阶段和低血量时 BOSS 会强制放技能，别赌它放不出来！
  关卡 BOSS：熔岩裂谷(火) → 深海遗迹(水) → 雷鸣高塔(雷) → 岩山废矿(岩)

【难度】
  每 60 秒难度提升一级：敌人血量、速度、伤害、射击者比例都会上升

【道具】（敌人有概率掉落，BOSS 必掉；靠近自动拾取）
  烤鸡大餐   - 回复 30% 最大生命
  能量水晶   - 能量 +50（大招充能）
  黄金磁币   - 分数 +200、经验 +60，并吸走场上所有掉落物
  烈焰之羽   - 攻击力 +30%，持续 45 秒
  生命之花   - 生命上限 +20 并立即回复 20（本局有效）
  守护羽帽   - 获得 3 层护盾，可抵消 3 次伤害
  道具停留 15 秒，消失前会闪烁；掉落概率：越强越稀有
  拾取时屏幕下方会弹出该道具的效果说明

【升级系统】
  击杀敌人获得经验，升级时游戏暂停并随机出现 3 张卡片：
  解锁新属性 / 增加攻击力 / 增加子弹行数 / 提升移速 /
  提升攻速 / 增加生命上限 / 增加能量获取

【元素大招】（能量满时按 F）
  火 - 幽灵冲撞，秒杀沿途敌人并清除敌方子弹；之后 8 秒攻击 +50%
  水 - 召唤三个水精灵环绕持续伤害；之后 10 秒每秒回复 2% 最大生命
  冰 - 指定范围降下冰锥雨并清弹；之后 5 秒无敌
  雷 - 释放巨型光波，秒杀路径上的敌人并清弹；之后 8 秒充能速度翻倍
  岩 - 召唤巨岩砸落，清空屏内杂兵（BOSS 受一半伤害）；之后获得 3 次护盾

【其它】
  连击：短时间内连续击杀会累计连击数，中断则清零
  图鉴：主菜单「图鉴」可查看敌人与道具的收集进度和说明
  关卡进度与图鉴数据会自动保存

【提示】
  同属性攻击无效，合理切换属性应对不同敌人！
  优先击杀射击者，它的子弹最烦人！
  残血时优先捡烤鸡，被围住时用守护羽帽保命！";

    // ---------------------------------------------------------------
    void Awake()
    {
        UITheme.Background(backgroundImage, "界面/主页面背景");
        UITheme.Font(titleText, 40);
        UITheme.Font(contentText, 20);
        if (titleText != null) { titleText.text = "游戏规则"; titleText.color = new Color32(255, 220, 50, 255); }
        if (contentText != null)
        {
            contentText.text = RulesText;
            contentText.color = Color.white;
            contentText.alignment = TextAnchor.UpperLeft;
            contentText.horizontalOverflow = HorizontalWrapMode.Wrap;
            contentText.verticalOverflow = VerticalWrapMode.Overflow;
        }
        // 返回按钮：只保留图标（原图 850×850 是正方形，旧版塞进 120×50 的框会被拉扁，
        // 而且图标上还叠了“返回”两个字 —— 这里改成等比方形图标，文字标签去掉）。
        UITheme.IconButton(backButton, "界面/返回", 64f);
        if (backButton != null)
        {
            var brt = (RectTransform)backButton.transform;
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.zero;
            brt.pivot = Vector2.zero;
            brt.anchoredPosition = new Vector2(28f, 24f);
        }
    }

    void Start()
    {
        if (backButton != null) backButton.onClick.AddListener(() => UIManager.Instance.CloseRules());
    }

    void OnEnable()
    {
        // 每次打开都回到顶部，并强制刷新布局以计算内容高度
        Canvas.ForceUpdateCanvases();
        if (scrollRect != null) scrollRect.verticalNormalizedPosition = 1f;
    }
}