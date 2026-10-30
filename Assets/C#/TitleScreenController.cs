using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 开机标题页：《元素战机》首页图 + 「按任意键开始」呼吸闪烁。
///
/// 特点：
///  * 零配置 —— RuntimeInitializeOnLoadMethod 自动创建，不用手动挂到场景里；
///  * 非侵入 —— 只是一层覆盖 Canvas，不改动 GameManager / UIManager 的任何流程；
///  * 按任意键（或点击）后整层淡出，并调用 UIManager.ShowState(GameState.Login)
///    进入登录界面；若场景里没有 UIManager 则只淡出（露出原有主菜单）。
///
/// 首页图来自 Resources/Sprites/界面/首页背景.png（Tools/compose_title_screen.py 生成）。
/// </summary>
public class TitleScreenController : MonoBehaviour
{
    const float MinInputDelay = 0.6f;   // 防止启动瞬间的按键直接跳过
    const float FadeDuration = 0.5f;
    const float PulseSpeed = 2.2f;      // 呼吸速度（弧度/秒）
    const float PulseMinAlpha = 0.25f;  // 若隐若现的最低透明度
    const float PulseMaxAlpha = 0.90f;

    CanvasGroup group;
    Text prompt;
    float timer;
    bool fading;
    float fadeT;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var go = new GameObject("TitleScreen");
        go.AddComponent<TitleScreenController>();
    }

    void Awake()
    {
        // ---- 覆盖用 Canvas ----
        var canvasGo = new GameObject("TitleCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 3000; // 盖在所有 UI 之上

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        scaler.referenceResolution = new Vector2(GameConfig.ScreenWidth, GameConfig.ScreenHeight);

        group = canvasGo.AddComponent<CanvasGroup>();

        // ---- 全屏首页图（cover 铺满，不变形）----
        var bgGo = new GameObject("Background", typeof(Image));
        bgGo.transform.SetParent(canvasGo.transform, false);
        var bg = bgGo.GetComponent<Image>();
        UITheme.Background(bg, "界面/首页背景");

        // ---- 呼吸提示文字 ----
        var txtGo = new GameObject("PressAnyKey", typeof(Text), typeof(Outline));
        txtGo.transform.SetParent(canvasGo.transform, false);
        prompt = txtGo.GetComponent<Text>();
        prompt.text = "—— 按 任 意 键 开 始 ——";
        prompt.alignment = TextAnchor.MiddleCenter;
        prompt.raycastTarget = false;
        UITheme.Font(prompt, 30);

        var outline = txtGo.GetComponent<Outline>();
        outline.effectColor = new Color32(90, 60, 160, 255);
        outline.effectDistance = new Vector2(2f, -2f);

        // 参考分辨率 1920x1080：LOGO 底部约在 y=350，文字放其下方一点
        var rt = (RectTransform)txtGo.transform;
        rt.sizeDelta = new Vector2(700f, 50f);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, -145f);

        Time.timeScale = 1f;
    }

    void Update()
    {
        timer += Time.unscaledDeltaTime;

        if (!fading)
        {
            // 若隐若现：正弦呼吸
            float a = 0.5f + 0.5f * Mathf.Sin(timer * PulseSpeed);
            if (prompt != null)
            {
                var c = prompt.color;
                c.a = Mathf.Lerp(PulseMinAlpha, PulseMaxAlpha, a);
                prompt.color = c;
            }

            if (timer > MinInputDelay && (Input.anyKeyDown || Input.GetMouseButtonDown(0)))
            {
                fading = true;
                fadeT = 0f;
            }
        }
        else
        {
            fadeT += Time.unscaledDeltaTime;
            group.alpha = 1f - Mathf.Clamp01(fadeT / FadeDuration);
            if (fadeT >= FadeDuration)
            {
                if (UIManager.Instance != null)
                    UIManager.Instance.ShowState(GameState.Login);
                Destroy(gameObject);
            }
        }
    }
}
