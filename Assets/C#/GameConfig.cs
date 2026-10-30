using UnityEngine;

/// <summary>
/// 全局常量。对应 pygame 里的 SCREEN_WIDTH / MAP_* / 颜色等。
/// 坐标单位说明：本移植保持“1 世界单位 = 原版 1 像素”，逻辑固定按 60 帧/秒推进，
/// 因此原版所有“每帧”数值（速度 6、冷却 300 帧等）可以原样照搬。
/// </summary>
public static class GameConfig
{
    public const float ScreenWidth = 1024f;
    public const float ScreenHeight = 768f;

    /// <summary>
    /// 实体（玩家 / 敌人 / Boss / 子弹 / 特效）的整体缩放系数。
    /// 世界改成 16:9 后一屏看到的地图范围变大，按原尺寸人物会显得过大、地图显小，
    /// 这里统一缩小。想再调大调小只改这一个值即可。
    /// </summary>
    public const float EntityScale = 0.7f;

    /// <summary>
    /// 地图整体放大倍数。相机跟随视野（FollowOrthoSize）不变，
    /// 因此把地图放大 N 倍 = 屏幕上的地图看起来就是原来的 N 倍大，
    /// 同时人物/敌人的屏幕占比保持不变（因为它们的世界尺寸没变）。
    /// 想要更大/更小的地图，只改这一个值即可。
    /// </summary>
    public const float MapScale = 3f;

    /// <summary>基准地图尺寸 1920×1080（16:9，与 1067×600 的地图素材同比例，不会拉伸变形）。</summary>
    public const float BaseMapWidth = 1920f;
    public const float BaseMapHeight = 1080f;

    /// <summary>关卡地图实际世界尺寸（基准 × MapScale）。</summary>
    public const float MapWidth = BaseMapWidth * MapScale;
    public const float MapHeight = BaseMapHeight * MapScale;

    public const int TargetFps = 60;

    // 渲染层级（Sorting Order）
    public const int OrderMap = -100;
    public const int OrderPowerUp = 0;
    public const int OrderEnemy = 10;
    public const int OrderHpBar = 12;
    public const int OrderBullet = 20;
    public const int OrderPlayer = 30;
    public const int OrderEffect = 40;
    public const int OrderText = 60;

    // 常用颜色
    public static readonly Color White = Color.white;
    public static readonly Color Black = Color.black;
    public static readonly Color Red = new Color32(255, 60, 60, 255);
    public static readonly Color Green = new Color32(50, 200, 50, 255);
    public static readonly Color Yellow = new Color32(255, 220, 50, 255);
    public static readonly Color Cyan = new Color32(60, 200, 255, 255);
    public static readonly Color Gray = new Color32(150, 150, 150, 255);
    public static readonly Color Orange = new Color32(255, 150, 30, 255);
}