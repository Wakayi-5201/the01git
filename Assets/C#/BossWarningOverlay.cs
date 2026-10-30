using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Boss 出场警告覆盖层：全屏红色闪烁文字 "⚠ 警告：BOSS 即将出场 ⚠"。
/// 由 LevelManager 在波次 3 清场后动态生成并调用 Show()，2.5 秒后自动销毁。
/// - timeScale=0：完全冻结画面（由 LevelManager 管理，不在这里改）
/// - pointer-events:none：不阻挡玩家操作
/// - 幂等：同一时刻只允许存在一个
/// </summary>
public class BossWarningOverlay : MonoBehaviour
{
    public const float Duration = 2.5f;

    float timer;
    Text text;
    GameObject canvasGO;

    /// <summary>由 LevelManager 调用。注意：timeScale=0 由 LevelManager 设置。</summary>
    public void Init()
    {
        timer = Duration;

        // 创建覆盖层 Canvas（独立 Canvas，Sorting Order 很高，盖在所有 UI 之上）
        // 必须挂在本对象下面，否则本对象销毁后画布会永久残留在屏幕上。
        canvasGO = new GameObject("BossWarningCanvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.sortingOrder = 1000;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        UITheme.EnsureScaler(canvas);
        canvasGO.AddComponent<GraphicRaycaster>().ignoreReversedGraphics = true;

        // 半透明黑色背景
        var bgGO = new GameObject("BG", typeof(RectTransform));
        bgGO.transform.SetParent(canvasGO.transform, false);
        var bg = bgGO.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.55f);
        var bgRT = bg.rectTransform;
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
        bg.raycastTarget = false;

        // 警告文字
        var textGO = new GameObject("Text", typeof(RectTransform));
        textGO.transform.SetParent(canvasGO.transform, false);
        text = textGO.AddComponent<Text>();
        text.text = "⚠  警告  \n\nBOSS 即将出场  \n\n⚠";
        text.font = AssetLibrary.GetMainFont();
        text.fontSize = 72;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color32(255, 60, 60, 255);
        text.raycastTarget = false;
        var trt = text.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
    }

    void Update()
    {
        timer -= Time.unscaledDeltaTime;   // 用 unscaled，timeScale=0 时仍正常计时
        if (text != null)
        {
            float a = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 12f);
            text.color = new Color(1f, 0.25f, 0.25f, a);
        }
        if (timer <= 0f)
        {
            if (canvasGO != null) Destroy(canvasGO);
            Destroy(gameObject);
        }
    }
}
