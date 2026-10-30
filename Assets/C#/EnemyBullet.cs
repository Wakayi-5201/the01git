using UnityEngine;

/// <summary>
/// 敌人子弹（红色圆点）。对应 pygame 的 EnemyBullet 类。
/// </summary>
public class EnemyBullet : MonoBehaviour
{
    public string direction = "up";
    public int damage;
    public float speed = 8f;
    public int lifetime = 150;
    public Vector2 halfExtents = new Vector2(8f, 8f);
    public bool Dead;

    /// <summary>若非空，则用此向量直接移动（支持任意角度，已含速度），否则用 direction 四方向 × speed。</summary>
    public Vector2? velocityVector;

    /// <summary>
    /// 覆盖飞行速度。
    /// 两种模式都支持：direction 模式直接改 speed；
    /// velocityVector 模式则保持方向、只替换长度。
    /// </summary>
    public void SetSpeed(float s)
    {
        speed = s;
        if (velocityVector.HasValue)
        {
            Vector2 v = velocityVector.Value;
            if (v.sqrMagnitude > 0.000001f) velocityVector = v.normalized * s;
        }
    }

    static Sprite _sprite;

    public static EnemyBullet Create(Vector2 position, string direction, int damage)
    {
        if (_sprite == null) _sprite = SpriteFactory.CircleSprite(32, new Color32(255, 40, 40, 255));

        var go = new GameObject("EnemyBullet");
        go.transform.position = new Vector3(position.x, position.y, 0f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = _sprite;
        sr.sortingOrder = GameConfig.OrderBullet;
        SpriteFactory.ApplyWorldSize(go.transform, _sprite, new Vector2(16f, 16f) * GameConfig.EntityScale);

        var b = go.AddComponent<EnemyBullet>();
        b.direction = direction;
        b.damage = damage;
        return b;
    }

    /// <summary>任意方向子弹（Boss 弹幕用）。velocity 已包含方向*速度。</summary>
    public static EnemyBullet Create(Vector2 position, Vector2 velocity, int damage)
    {
        var b = Create(position, "up", damage);
        b.velocityVector = velocity;
        return b;
    }

    public void Step()
    {
        lifetime--;
        if (lifetime <= 0)
        {
            Kill();
            return;
        }

        // 注意：velocityVector 已含速度，direction 模式必须自己乘 speed。
        // 之前这里漏乘 speed，导致敌人子弹速度恒为 1（比玩家子弹慢 10 倍）。
        Vector2 d = velocityVector ?? (Bullet.DirectionVector(direction) * speed);
        transform.position += new Vector3(d.x, d.y, 0f);

        Vector3 p = transform.position;
        if (p.x < 0f || p.x > GameConfig.MapWidth || p.y < 0f || p.y > GameConfig.MapHeight)
            Kill();
    }

    public void Kill()
    {
        Dead = true;
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }
}