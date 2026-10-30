using System.Collections.Generic;
using System.IO;
using UnityEngine;

[System.Serializable]
public class ScoreEntry
{
    public string name;
    public int score;
}

/// <summary>图鉴计数条目（JsonUtility 不支持字典，用 键值对列表 存）。</summary>
[System.Serializable]
public class CountEntry
{
    public string key;
    public int count;
}

[System.Serializable]
public class SaveData
{
    public List<ScoreEntry> highScores = new List<ScoreEntry>();
    public string currentPlayer = "";
    public List<string> totalPlayers = new List<string>();

    // 关卡进度：已解锁到第几关（0=只有第1关，1=第2关解锁，...，4=四关全通+无尽解锁）
    public int maxUnlockedLevel = 0;
    public bool endlessUnlocked = false;
    // 已解锁的角色属性（枚举 int 值），默认含火
    public List<int> unlockedElements = new List<int> { (int)Element.Fire };
    public int lastSelectedElement = (int)Element.Fire;

    // 图鉴：敌人击杀数（key=火/火BOSS...）、道具拾取数（key=血包/能量水晶...）
    public List<CountEntry> enemyKills = new List<CountEntry>();
    public List<CountEntry> itemPickups = new List<CountEntry>();

    /// <summary>跨关永久成长档。只有通关才写回，死亡/中途退出丢弃本关收益。</summary>
    public GrowthData growth = new GrowthData();
}

/// <summary>
/// 存档 + 排行榜。对应 pygame 的 load_player_data / save_player_data /
/// update_high_score / get_top_scores（原来写 save_data.json，这里写 persistentDataPath）。
/// </summary>
public static class SaveSystem
{
    static string FilePath => Path.Combine(Application.persistentDataPath, "save_data.json");

    static SaveData _cache;
    public static SaveData Data
    {
        get
        {
            if (_cache == null) _cache = Load();
            return _cache;
        }
    }

    public static SaveData Load()
    {
        if (!File.Exists(FilePath)) return new SaveData();
        try
        {
            string json = File.ReadAllText(FilePath);
            var data = JsonUtility.FromJson<SaveData>(json);
            if (data == null) return new SaveData();
            if (data.highScores == null) data.highScores = new List<ScoreEntry>();
            if (data.totalPlayers == null) data.totalPlayers = new List<string>();
            if (data.currentPlayer == null) data.currentPlayer = "";
            if (data.unlockedElements == null || data.unlockedElements.Count == 0)
                data.unlockedElements = new List<int> { (int)Element.Fire };
            if (data.growth == null) data.growth = new GrowthData();
            if (data.enemyKills == null) data.enemyKills = new List<CountEntry>();
            if (data.itemPickups == null) data.itemPickups = new List<CountEntry>();
            return data;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[SaveSystem] 读档失败: " + e.Message);
            return new SaveData();
        }
    }

    public static void Save()
    {
        try
        {
            File.WriteAllText(FilePath, JsonUtility.ToJson(Data, true));
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[SaveSystem] 存档失败: " + e.Message);
        }
    }

    public static int GetHighScore(string name)
    {
        var e = Data.highScores.Find(x => x.name == name);
        return e != null ? e.score : 0;
    }

    // ---------------------------------------------------------------
    // 图鉴计数
    public static int GetCount(List<CountEntry> list, string key)
    {
        var e = list.Find(x => x.key == key);
        return e != null ? e.count : 0;
    }

    public static void BumpCount(List<CountEntry> list, string key)
    {
        var e = list.Find(x => x.key == key);
        if (e == null) list.Add(new CountEntry { key = key, count = 1 });
        else e.count++;
    }

    /// <summary>更新最高分。返回是否为“新纪录”，best 为更新后的最高分。</summary>
    public static bool UpdateHighScore(string name, int score, out int best)
    {
        if (string.IsNullOrEmpty(name)) { best = 0; return false; }

        var e = Data.highScores.Find(x => x.name == name);
        if (e == null)
        {
            e = new ScoreEntry { name = name, score = score };
            Data.highScores.Add(e);
            Save();
            best = score;
            return true;
        }

        best = e.score;
        if (score > e.score)
        {
            e.score = score;
            Save();
            return true;
        }
        return false;
    }

    public static void RegisterPlayer(string name)
    {        if (string.IsNullOrEmpty(name)) return;
        if (Data.highScores.Find(x => x.name == name) == null)
            Data.highScores.Add(new ScoreEntry { name = name, score = 0 });

        if (!Data.totalPlayers.Contains(name))
        {
            Data.totalPlayers.Add(name);
            if (Data.totalPlayers.Count > 20)
                Data.totalPlayers.RemoveRange(0, Data.totalPlayers.Count - 20);
        }
    }

    /// <summary>排行榜前 limit 名（按分数降序）。</summary>
    public static List<ScoreEntry> GetTopScores(int limit = 10)
    {
        var list = new List<ScoreEntry>(Data.highScores.FindAll(x => x.score > 0));
        list.Sort((a, b) => b.score.CompareTo(a.score));
        if (list.Count > limit) list.RemoveRange(limit, list.Count - limit);
        return list;
    }

    // ---------------------------------------------------------------
    /// <summary>
    /// 重置游戏进度：清空关卡解锁、角色解锁与永久成长档，
    /// 回到"只有第 1 关、只有火属性、满级初始值"的状态。
    /// 排行榜和玩家名保留。用于清掉历史脏存档（例如误把五个属性全解锁了）。
    /// </summary>
    public static void ResetProgress()
    {
        var keep = _cache;
        _cache = new SaveData
        {
            highScores = keep != null && keep.highScores != null
                ? new List<ScoreEntry>(keep.highScores) : new List<ScoreEntry>(),
            totalPlayers = keep != null && keep.totalPlayers != null
                ? new List<string>(keep.totalPlayers) : new List<string>(),
            currentPlayer = keep != null ? keep.currentPlayer : "",
        };
        PlayerGrowth.Reset();
        Save();
    }

    // 关卡进度
    public static bool IsLevelUnlocked(LevelConfig.Id id)
    {
        if (id == LevelConfig.Id.Endless) return Data.endlessUnlocked;
        return (int)id <= Data.maxUnlockedLevel;
    }

    /// <summary>通关 currentId 后解锁下一关。</summary>
    public static void UnlockNextLevel(LevelConfig.Id currentId)
    {
        if (currentId == LevelConfig.Id.Endless) return;
        int next = (int)currentId + 1;
        if (next > Data.maxUnlockedLevel) Data.maxUnlockedLevel = next;
        // 四关全通后解锁无尽
        if (Data.maxUnlockedLevel >= LevelConfig.RegularLevelCount)
            Data.endlessUnlocked = true;
        Save();
    }

    /// <summary>该关是否已通关。</summary>
    public static bool IsLevelCleared(LevelConfig.Id id)
    {
        if (id == LevelConfig.Id.Endless) return false;
        return (int)id < Data.maxUnlockedLevel;
    }

    // ---------------------------------------------------------------
    // 角色属性解锁
    public static bool IsElementUnlocked(Element e) => Data.unlockedElements.Contains((int)e);

    public static void UnlockElement(Element e)
    {
        int v = (int)e;
        if (!Data.unlockedElements.Contains(v))
        {
            Data.unlockedElements.Add(v);
            Save();
        }
    }

    /// <summary>菜单选关时玩家选的初始角色。</summary>
    public static Element GetStartElement()
    {
        int v = Data.lastSelectedElement;
        if (!Data.unlockedElements.Contains(v)) v = (int)Element.Fire;
        return (Element)v;
    }

    public static void SetStartElement(Element e)
    {
        Data.lastSelectedElement = (int)e;
        Save();
    }
}