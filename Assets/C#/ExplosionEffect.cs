using UnityEngine;

/// <summary>
/// 爆炸特效。对应 pygame 的 Explosion 类。
/// </summary>
public class ExplosionEffect : MonoBehaviour
{
    SpriteRenderer sr;
    int timer;
    int lifetime = 35;
    Vector2 baseWorldSize;
    Color baseColor;
    public bool Dead;

    public static ExplosionEffect Create(Vector2 position, Color color, float sizeScale)
    {
        var go = new GameObject("Explosion");
        go.transform.position = new Vector3(position.x, position.y, 0f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = GameConfig.OrderEffect;

        var sprite = AssetLibrary.GetSprite("特效/爆炸素材");
        sr.sprite = sprite;

        float size = (sizeScale > 1.5f ? 180f : 100f) * sizeScale * GameConfig.EntityScale;
        SpriteFactory.ApplyWorldSize(go.transform, sprite, new Vector2(size, size));

        var ex = go.AddComponent<ExplosionEffect>();
        ex.sr = sr;
        ex.baseWorldSize = new Vector2(size, size);
        ex.baseColor = color;
        return ex;
    }

    public void Step()
    {
        timer++;
        float p = (float)timer / lifetime;

        float s = p < 0.2f ? Mathf.Lerp(0.3f, 1f, p / 0.2f) : Mathf.Lerp(1f, 1.6f, (p - 0.2f) / 0.8f);
        float a = Mathf.Clamp01(1f - p * 1.2f);

        sr.color = new Color(baseColor.r, baseColor.g, baseColor.b, a);

        var sprite = sr.sprite;
        if (sprite != null)
        {
            Vector2 b = sprite.bounds.size;
            transform.localScale = new Vector3(
                baseWorldSize.x * s / Mathf.Max(b.x, 0.0001f),
                baseWorldSize.y * s / Mathf.Max(b.y, 0.0001f), 1f);
        }

        if (timer >= lifetime) Dead = true;
    }
}