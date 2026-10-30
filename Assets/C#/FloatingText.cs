using UnityEngine;

/// <summary>
/// 世界空间飘字（伤害数字 / 反应名 / 得分等）。对应 pygame 的 FloatingText。
/// </summary>
public class FloatingText : MonoBehaviour
{
    TextMesh tm;
    float vx, vy;
    int timer;
    int lifetime;
    public bool Dead;

    public static FloatingText Create(Vector2 position, string text, Color color, bool large)
    {
        var go = new GameObject("FloatingText");
        go.transform.position = new Vector3(position.x, position.y, 0f);

        var tm = go.AddComponent<TextMesh>();
        var font = AssetLibrary.GetMainFont();
        if (font != null) tm.font = font;
        tm.text = text;
        tm.fontSize = 40;
        tm.characterSize = (large ? 8f : 6f) * GameConfig.EntityScale;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = color;

        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null) mr.sortingOrder = GameConfig.OrderText;

        var ft = go.AddComponent<FloatingText>();
        ft.tm = tm;
        ft.vx = Random.Range(-1.5f, 1.5f);
        ft.vy = 3f;
        ft.lifetime = 70;
        return ft;
    }

    public void Step()
    {
        timer++;
        transform.position += new Vector3(vx, vy, 0f);
        vy -= 0.08f;

        if (timer > lifetime * 0.5f)
        {
            float a = Mathf.Clamp01(1f - (timer - lifetime * 0.5f) / (lifetime * 0.5f));
            var c = tm.color; c.a = a; tm.color = c;
        }

        if (timer >= lifetime) Dead = true;
    }
}