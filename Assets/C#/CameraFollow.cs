using System.Collections;
using UnityEngine;

/// <summary>
/// 相机跟随玩家并限制在当前关卡地图范围内。
/// 开场流程：先拉到能看全整张地图的高度 → 停留 → 平滑拉近并聚焦玩家 → 交还给跟随逻辑。
/// 注意：不要 using System，否则 UnityEngine.Random 会与 System.Random 二义性冲突。
/// </summary>
public class CameraFollow : MonoBehaviour
{
    public Transform target;

    /// <summary>当前关卡的世界范围（关卡初始化时由 LevelManager 设置）。</summary>
    public float worldWidth = GameConfig.MapWidth;
    public float worldHeight = GameConfig.MapHeight;

    [Header("开场镜头")]
    [Tooltip("勾选后，每关开始会播放“全图 → 拉近”的开场动画")]
    public bool playIntro = true;
    [Tooltip("全图展示停留时间（秒，不受 timeScale 影响）")]
    public float introHold = 1.2f;
    [Tooltip("从全图拉近到玩家的动画时长（秒，不受 timeScale 影响）")]
    public float introZoom = 2.2f;
    /// <summary>
    /// 跟随时的正交视野半高。270 → 可视区域 960×540。
    /// 地图已整体放大 3 倍（5760×3240），所以一屏只看到地图的 1/6，
    /// 屏幕上的地图就是放大前的 3 倍大。想看得更广就调大这个值。
    /// </summary>
    [Tooltip("跟随时的正交视野半高（270 → 可视 960×540）")]
    public float followOrthoSize = 270f;

    Camera cam;
    bool introPlaying;

    // ---------------------------------------------------------------
    void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
    }

    void LateUpdate()
    {
        if (introPlaying) return;          // 开场动画期间由协程接管镜头
        if (target == null || cam == null) return;

        Vector3 p = FollowPos(target.position, cam.orthographicSize);
        transform.position = p;
    }

    /// <summary>关卡切换时由 LevelManager 调用。</summary>
    public void SetWorldBounds(float width, float height)
    {
        worldWidth = width;
        worldHeight = height;
    }

    /// <summary>把跟随目标设为玩家（LevelManager 每关开始时会重新指认一次）。</summary>
    public void SetTarget(Transform t) { target = t; }

    // ---------------------------------------------------------------
    /// <summary>计算限制在地图边界内的相机位置。</summary>
    Vector3 FollowPos(Vector3 p, float orthoSize)
    {
        float halfH = orthoSize;
        float halfW = halfH * (cam != null ? cam.aspect : 16f / 9f);

        float minX = halfW, maxX = worldWidth - halfW;
        float minY = halfH, maxY = worldHeight - halfH;

        float x = maxX > minX ? Mathf.Clamp(p.x, minX, maxX) : worldWidth * 0.5f;
        float y = maxY > minY ? Mathf.Clamp(p.y, minY, maxY) : worldHeight * 0.5f;

        return new Vector3(x, y, -10f);
    }

    /// <summary>能完整展示整张地图所需的正交尺寸（宽和高都要装得下）。</summary>
    float FullMapOrthoSize()
    {
        float aspect = cam != null ? cam.aspect : 16f / 9f;
        float halfH = worldHeight * 0.5f;
        float halfW = worldWidth * 0.5f;
        return Mathf.Max(halfH, halfW / aspect);
    }

    /// <summary>把镜头瞬间摆到目标玩家身上（用于跳过开场动画）。</summary>
    public void SnapToTarget()
    {
        if (cam == null) cam = GetComponent<Camera>();
        if (cam == null) return;
        cam.orthographicSize = followOrthoSize;
        if (target != null) transform.position = FollowPos(target.position, followOrthoSize);
    }

    /// <summary>
    /// 开场协程：先全图，再拉近聚焦玩家。由 LevelManager 用 StartCoroutine 启动。
    /// 全程使用真实时间，因此 timeScale=0（关卡冻结）时动画依然正常播放。
    /// </summary>
    public IEnumerator PlayIntro(Transform followTarget)
    {
        if (followTarget != null) target = followTarget;
        if (cam == null) cam = GetComponent<Camera>();
        if (cam == null || target == null) yield break;

        introPlaying = true;

        float fullSize = FullMapOrthoSize();
        Vector3 fullPos = new Vector3(worldWidth * 0.5f, worldHeight * 0.5f, -10f);

        cam.orthographicSize = fullSize;
        transform.position = fullPos;

        if (introHold > 0f)
            yield return new WaitForSecondsRealtime(introHold);

        float elapsed = 0f;
        float zoomTime = Mathf.Max(0.01f, introZoom);
        while (elapsed < zoomTime)
        {
            elapsed += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(elapsed / zoomTime);
            k = SmoothStep(k);

            cam.orthographicSize = Mathf.Lerp(fullSize, followOrthoSize, k);
            Vector3 dest = FollowPos(target.position, cam.orthographicSize);
            transform.position = Vector3.Lerp(fullPos, dest, k);
            yield return null;
        }

        cam.orthographicSize = followOrthoSize;
        transform.position = FollowPos(target.position, followOrthoSize);
        introPlaying = false;
    }

    static float SmoothStep(float t) => t * t * (3f - 2f * t);
}
