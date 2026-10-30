using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 升级三选一：**底部横条**（不居中、不遮挡战场中央）。
/// 三张卡片横向排在屏幕底部，3 秒内选择，超时默认第 1 项。
/// 选择方式：**鼠标点击卡片** 或 数字键 1/2/3（数字键 1-5 已让给切换属性，见 PlayerController）。
/// 弹出期间 timeScale=0.4（敌人减速），通过 LevelManager.RequestSlowdown 管理。
/// 前两次升级必出"解锁新属性"。
/// 注意：不要 using System（会与 UnityEngine.Random 冲突）。
/// </summary>
public class UpgradePanel : MonoBehaviour
{
    public static UpgradePanel Instance;

    const float CardW = 310f;
    const float CardH = 134f;
    const float CardGap = 16f;
    const float StripH = 198f;

    Canvas canvas;
    Text timerText;
    List<GameObject> cards = new List<GameObject>();
    List<UpgradeOption> currentOptions = new List<UpgradeOption>();
    float timer;
    bool showing;
    int upgradeCount;

    class UpgradeOption
    {
        public string name;
        public string desc;
        public System.Action apply;
    }

    void Awake()
    {
        Instance = this;
        BuildCanvas();
        gameObject.SetActive(false);
    }

    void BuildCanvas()
    {
        var go = new GameObject("UpgradeCanvas");
        go.transform.SetParent(transform, false);
        canvas = go.AddComponent<Canvas>();
        canvas.sortingOrder = 500;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        UITheme.EnsureScaler(canvas);
        go.AddComponent<GraphicRaycaster>().ignoreReversedGraphics = true;

        var font = AssetLibrary.GetMainFont();

        // 底部条：只压住屏幕下方一条，战场中央完全不被遮挡
        var strip = NewRect(go.transform, "Strip", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 0f), new Vector2(GameConfig.ScreenWidth, StripH));
        var stripImg = strip.gameObject.AddComponent<Image>();
        stripImg.color = new Color(0.04f, 0.04f, 0.07f, 0.82f);
        stripImg.raycastTarget = true;      // 吃掉条内空白处的点击，避免误触战斗 UI

        // 标题（贴条顶，居中）
        var title = NewText(strip, "Title", "等级提升！选择一项", font, 26, TextAnchor.MiddleCenter);
        title.color = GameConfig.Yellow;
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -12f), new Vector2(400f, 34f));

        // 倒计时（贴条顶，靠右）
        timerText = NewText(strip, "TimerText", "", font, 18, TextAnchor.MiddleRight);
        timerText.color = new Color32(200, 200, 200, 255);
        Place(timerText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-18f, -16f), new Vector2(300f, 28f));
    }

    void Update()
    {
        if (!showing) return;

        timer -= Time.unscaledDeltaTime;
        UpdateTimerText();

        // 键盘：1/2/3 直接选择（鼠标点击由卡片上的 Button 处理）
        if (Input.GetKeyDown(KeyCode.Alpha1)) Pick(0);
        else if (Input.GetKeyDown(KeyCode.Alpha2)) Pick(1);
        else if (Input.GetKeyDown(KeyCode.Alpha3)) Pick(2);

        if (timer <= 0f) Pick(0);
    }

    // ---------------------------------------------------------------
    public void ShowUpgrade()
    {
        if (showing) return;
        showing = true;
        gameObject.SetActive(true);
        timer = 3f;
        LevelManager.Instance?.RequestSlowdown(true);

        currentOptions = RollOptions();
        BuildCards();
        UpdateTimerText();
    }

    /// <summary>随机 3 个升级项，前两次必含"解锁新属性"。</summary>
    List<UpgradeOption> RollOptions()
    {
        var pool = BuildPool();
        var result = new List<UpgradeOption>();

        // 前两次升级保证有解锁新属性（如果还有未解锁的）
        bool forceUnlock = upgradeCount < 2 && HasUnlockable();
        if (forceUnlock)
        {
            var unlockOpt = pool.Find(o => o.name.StartsWith("解锁"));
            if (unlockOpt != null)
            {
                result.Add(unlockOpt);
                pool.Remove(unlockOpt);
            }
        }

        // 随机补足 3 个
        while (result.Count < 3 && pool.Count > 0)
        {
            int idx = Random.Range(0, pool.Count);
            result.Add(pool[idx]);
            pool.RemoveAt(idx);
        }

        // 兜底：极端情况（池子被清空）下至少保证有一张卡，否则面板空着没法选
        if (result.Count == 0) result.Add(HealOption());
        return result;
    }

    /// <summary>永远可用的保底升级项：恢复 40% 最大生命。</summary>
    UpgradeOption HealOption()
    {
        var p = GameManager.Instance.player;
        return new UpgradeOption
        {
            name = "生命回复",
            desc = "恢复 40% 最大生命",
            apply = () => p.hp = Mathf.Min(p.maxHp, p.hp + Mathf.RoundToInt(p.maxHp * 0.4f)),
        };
    }

    /// <summary>
    /// 可选升级项。每项都带"当前值 / 上限"进度，让上限这件事对玩家是可见的；
    /// 已到上限的项不再进池子。"生命回复"永远可选，用来兜底（其余全满级时也有卡可选）。
    /// </summary>
    List<UpgradeOption> BuildPool()
    {
        var p = GameManager.Instance.player;
        var list = new List<UpgradeOption>();

        if (p.damage < PlayerGrowth.MaxDamage)
            list.Add(new UpgradeOption
            {
                name = "攻击强化",
                desc = "伤害 +5 " + PlayerGrowth.Progress(p.damage, PlayerGrowth.MaxDamage),
                apply = () => p.IncreaseDamage(5),
            });

        if (p.bulletRows < PlayerGrowth.MaxBulletRows)
            list.Add(new UpgradeOption
            {
                name = "多重射击",
                desc = "子弹行数 +1 " + PlayerGrowth.Progress(p.bulletRows, PlayerGrowth.MaxBulletRows),
                apply = () => p.IncreaseBulletRows(),
            });

        if (p.attackSpeedLevel < PlayerGrowth.MaxAttackSpeedLevel)
            list.Add(new UpgradeOption
            {
                name = "极速射击",
                desc = "射击间隔 " + p.GetShootDelay() + " → " + Mathf.Max(2, 9 - (p.attackSpeedLevel + 1)) + " 帧 "
                       + PlayerGrowth.Progress(p.attackSpeedLevel, PlayerGrowth.MaxAttackSpeedLevel),
                apply = () => p.IncreaseAttackSpeed(),
            });

        if (p.maxSpeed < PlayerGrowth.MaxMoveSpeed - 0.01f)
            list.Add(new UpgradeOption
            {
                name = "推进引擎",
                desc = "移动速度 +0.5 " + PlayerGrowth.Progress(p.maxSpeed, PlayerGrowth.MaxMoveSpeed),
                apply = () => p.IncreaseMoveSpeed(0.5f),
            });

        if (p.maxHp < PlayerGrowth.MaxHp)
            list.Add(new UpgradeOption
            {
                name = "生命上限",
                desc = "最大 HP +20（立刻回满这 20）" + PlayerGrowth.Progress(p.maxHp, PlayerGrowth.MaxHp),
                apply = () => { p.IncreaseMaxHp(20); p.hp = Mathf.Min(p.maxHp, p.hp + 20); },
            });

        if (p.maxEnergy < PlayerGrowth.MaxEnergy)
            list.Add(new UpgradeOption
            {
                name = "能量扩展",
                desc = "能量上限 +50（大招攒得更快）" + PlayerGrowth.Progress(p.maxEnergy, PlayerGrowth.MaxEnergy),
                apply = () => { p.IncreaseMaxEnergy(50); p.energy = p.maxEnergy; },
            });

        if (HasUnlockable())
            list.Add(new UpgradeOption
            {
                name = "解锁属性",
                desc = "获得一个新元素属性（还剩 " + RemainingLocks() + " 个未解锁）",
                apply = ApplyUnlock,
            });

        // 兜底项：永远可选
        list.Add(new UpgradeOption
        {
            name = "生命回复",
            desc = "恢复 40% 最大生命",
            apply = () => p.hp = Mathf.Min(p.maxHp, p.hp + Mathf.RoundToInt(p.maxHp * 0.4f)),
        });

        return list;
    }

    /// <summary>还有多少个属性没解锁。</summary>
    int RemainingLocks()
    {
        var p = GameManager.Instance.player;
        int n = 0;
        foreach (Element e in ElementDatabase.AllElements)
            if (!p.unlockedElements.Contains(e)) n++;
        return n;
    }

    bool HasUnlockable() => RemainingLocks() > 0;

    void ApplyUnlock()
    {
        var p = GameManager.Instance.player;
        foreach (Element e in ElementDatabase.AllElements)
        {
            if (!p.unlockedElements.Contains(e))
            {
                p.unlockedElements.Add(e);
                SaveSystem.UnlockElement(e);
                FloatingText.Create(p.transform.position, "解锁 " + ElementDatabase.GetName(e) + "!", GameConfig.Cyan, true);
                return;
            }
        }
    }

    // ---------------------------------------------------------------
    void BuildCards()
    {
        ClearCards();
        var font = AssetLibrary.GetMainFont();

        var strip = canvas.transform.Find("Strip");
        var parent = strip != null ? strip : canvas.transform;

        // 三张卡片横向排成一行，贴在底部条里
        float total = currentOptions.Count * CardW + (currentOptions.Count - 1) * CardGap;
        float startX = -total * 0.5f + CardW * 0.5f;

        for (int i = 0; i < currentOptions.Count; i++)
        {
            var opt = currentOptions[i];

            var card = NewRect(parent, "Card" + i, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(startX + i * (CardW + CardGap), 16f), new Vector2(CardW, CardH));

            var bg = card.gameObject.AddComponent<Image>();
            bg.color = new Color(0.12f, 0.12f, 0.18f, 0.92f);
            bg.raycastTarget = true;

            // 名称：卡片上半部
            var nameText = NewText(card, "Name", (i + 1) + ". " + opt.name, font, 22, TextAnchor.MiddleLeft);
            nameText.color = GameConfig.Yellow;
            AnchorFill(nameText.rectTransform, Vector2.zero, Vector2.one, new Vector2(14, 76), new Vector2(-14, -10));

            // 描述：卡片下半部（带"当前值/上限"，所以要允许换行）
            var descText = NewText(card, "Desc", opt.desc, font, 16, TextAnchor.UpperLeft);
            descText.color = Color.white;
            descText.horizontalOverflow = HorizontalWrapMode.Wrap;
            descText.verticalOverflow = VerticalWrapMode.Truncate;
            AnchorFill(descText.rectTransform, Vector2.zero, Vector2.one, new Vector2(14, 10), new Vector2(-14, -52));

            // 鼠标点击选择（闭包拷贝索引，避免循环结束后 i 越界）
            int idx = i;
            var btn = card.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.transition = Selectable.Transition.ColorTint;
            var cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color32(255, 220, 130, 255);
            cb.pressedColor = new Color32(200, 165, 85, 255);
            cb.selectedColor = Color.white;
            cb.colorMultiplier = 1f;
            btn.colors = cb;
            btn.onClick.AddListener(() => Pick(idx));

            cards.Add(card.gameObject);
        }
    }

    void UpdateTimerText()
    {
        if (timerText != null)
            timerText.text = "点击卡片 或 按 1/2/3 选择    " + Mathf.CeilToInt(timer) + "s";
    }

    void Pick(int idx)
    {
        if (!showing) return;
        if (idx < 0 || idx >= currentOptions.Count) idx = 0;
        currentOptions[idx].apply?.Invoke();
        upgradeCount++;
        GameManager.Instance.PlaySfx("powerup");
        Close();
    }

    void Close()
    {
        ClearCards();
        showing = false;
        LevelManager.Instance?.RequestSlowdown(false);
        gameObject.SetActive(false);
    }

    void ClearCards()
    {
        foreach (var c in cards) if (c != null) Destroy(c);
        cards.Clear();
    }

    // ---------------------------------------------------------------
    // UI 构造小工具（与 HUD 保持一致的实现）
    RectTransform NewRect(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    void AnchorFill(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)
    {
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = offMin;
        rt.offsetMax = offMax;
    }

    void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    Text NewText(Transform parent, string name, string txt, Font f, int size, TextAnchor align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.text = txt;
        t.font = f;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        return t;
    }
}
