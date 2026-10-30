/// <summary>
/// 全局界面/流程状态。对应 pygame 的 self.state 以及 show_rules / show_ranking / show_credits 几个开关。
/// </summary>
public enum GameState
{
    Login,      // 登录（含排行榜入口）
    Ranking,    // 排行榜
    Menu,       // 主菜单（选初始角色）
    Rules,      // 游戏规则
    LevelSelect,// 关卡选择
    Playing,    // 游玩中
    Victory,    // 关卡胜利结算
    GameOver,   // 失败结算
    Credits     // 特别感谢
}