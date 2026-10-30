using UnityEngine;

/// <summary>
/// 玩家子弹。对应 pygame 的 Bullet 类。
/// </summary>
public class Bullet : MonoBehaviour
{
    public Element element;
    public string direction = "up";
    public float damage;
    public float speed = 10f;
    public Vector2 halfExtents = new Vector2(17f, 17f);
    public bool Dead;

    public static Bullet Create(Element element, Vector2 position, string direction, float damage)
    {
        var go = new GameObject("Bullet");
        go.transform.position = new Vector3(position.x, position.y, 0f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = GameConfig.OrderBullet;

        var sprite = AssetLibrary.GetSprite("子弹/" + ElementDatabase.BulletFile(element));
        sr.sprite = sprite;
        SpriteFactory.ApplyWorldSize(go.transform, sprite, new Vector2(35f, 35f) * GameConfig.EntityScale);

        // 贴图默认朝上，按方向旋转
        float z = 0f;
        switch (direction)
        {
            case "down":  z = 180f; break;
            case "left":  z = 90f; break;
            case "right": z = -90f; break;
        }
        go.transform.rotation = Quaternion.Euler(0f, 0f, z);

        var b = go.AddComponent<Bullet>();
        b.element = element;
        b.direction = direction;
        b.damage = damage;
        b.halfExtents = new Vector2(13f, 13f);
        return b;
    }

    public void Step()
    {
        Vector2 d = DirectionVector(direction);
        transform.position += new Vector3(d.x, d.y, 0f) * speed;

        Vector3 p = transform.position;
        if (p.x < -40f || p.x > GameConfig.MapWidth + 40f ||
            p.y < -40f || p.y > GameConfig.MapHeight + 40f)
            Kill();
    }

    public void Kill()
    {
        Dead = true;
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }

    public static Vector2 DirectionVector(string dir)
    {
        switch (dir)
        {
            case "down":  return Vector2.down;
            case "left":  return Vector2.left;
            case "right": return Vector2.right;
            default:      return Vector2.up;
        }
    }
}