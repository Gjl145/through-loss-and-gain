using UnityEngine;

/// <summary>
/// 轨道出生点：绕中心公转，像行星绕恒星。
///
/// 第 3 步：出生点不再是静止分布，而是——
///   · 中心一个（静止不动，作为"恒星"）
///   · 其余 4 个各自在一条轨道上绕中心公转
///
/// 每条轨道的半径、角速度、初始相位、轨道倾角都可以独立配置，
/// 这样四条轨道不会同步，形成错落的公转节奏。
/// </summary>
public class OrbitSpawnPoint : MonoBehaviour
{
    [Header("轨道")]
    [Tooltip("公转半径（离中心的距离）")]
    public float radius = 6f;

    [Tooltip("角速度（弧度/秒）。负值 = 逆行")]
    public float angularSpeed = 0.35f;

    [Tooltip("初始相位（弧度）。错开各轨道，避免同步")]
    public float phase = 0f;

    [Tooltip("轨道平面倾角（度）。0 = 与演出平面平行")]
    public float inclination = 0f;

    [Header("中心")]
    [Tooltip("公转中心（一般就是房间中心）")]
    public Transform center;

    [Header("演出平面")]
    [Tooltip("轨道平面基准高度（与鱼同层）")]
    public float planeY = 1.2f;

    /// <summary>当前公转角度（只读，供调试与外部查询）。</summary>
    public float CurrentAngle { get; private set; }

    void Awake()
    {
        CurrentAngle = phase;
        ApplyPosition();      // 立即摆位，避免首帧位置错误（玩家可能在第一帧就注视它）
    }

    void Update()
    {
        CurrentAngle += angularSpeed * Time.deltaTime;
        ApplyPosition();
    }

    /// <summary>按当前角度计算并写入位置。</summary>
    public void ApplyPosition()
    {
        if (center == null) return;

        // 轨道平面内的圆周位置
        float x = Mathf.Cos(CurrentAngle) * radius;
        float z = Mathf.Sin(CurrentAngle) * radius;

        // 绕 X 轴倾斜轨道平面
        float y = 0f;
        if (inclination != 0f)
        {
            float rad = inclination * Mathf.Deg2Rad;
            float cosI = Mathf.Cos(rad);
            float sinI = Mathf.Sin(rad);
            float ny = z * sinI;
            float nz = z * cosI;
            y = ny;
            z = nz;
        }

        transform.position = center.position + new Vector3(x, y + planeY, z);
    }

    void OnDrawGizmosSelected()
    {
        var c = center != null ? center : transform;
        Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.6f);

        // 画出轨道椭圆（用采样折线近似）
        const int segments = 48;
        Vector3 prev = c.position + new Vector3(radius, planeY, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float a = (Mathf.PI * 2f * i / segments) + phase;
            float x = Mathf.Cos(a) * radius;
            float z = Mathf.Sin(a) * radius;
            float y = 0f;
            if (inclination != 0f)
            {
                float rad = inclination * Mathf.Deg2Rad;
                y = z * Mathf.Sin(rad);
                z = z * Mathf.Cos(rad);
            }
            Vector3 p = c.position + new Vector3(x, y + planeY, z);
            Gizmos.DrawLine(prev, p);
            prev = p;
        }
    }
}
