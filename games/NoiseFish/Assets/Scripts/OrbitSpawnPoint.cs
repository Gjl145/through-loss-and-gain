using UnityEngine;

/// <summary>
/// 轨道出生点：绕中心公转，像行星绕恒星。
///
/// 第 3 步：地图改为球形房间后，轨道系统变成真正的"星系统"——
///   · 所有轨道平面都**穿过球心**（球心 = 恒星 = 静止的中心出生点）
///   · 每条轨道有自己的半径、角速度、初始相位、轨道倾角
///   · 轨道不可见（不渲染任何轨道线），只有出生点在那里
///
/// 四条轨道参数刻意不等：半径递增、角速度递减 → 内圈快外圈慢，像真实天体。
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

    [Tooltip("轨道平面倾角（度）。0 = 与世界 XZ 平面平行")]
    public float inclination = 0f;

    [Header("中心")]
    [Tooltip("公转中心 = 球心 = 恒星所在处")]
    public Transform center;

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

    /// <summary>按当前角度计算并写入位置。轨道平面穿过中心。</summary>
    public void ApplyPosition()
    {
        if (center == null) return;

        // 轨道平面内的圆周位置（XZ 平面）
        float x = Mathf.Cos(CurrentAngle) * radius;
        float z = Mathf.Sin(CurrentAngle) * radius;
        float y = 0f;

        // 绕 X 轴倾斜轨道平面 → 平面仍然穿过中心
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

        transform.position = center.position + new Vector3(x, y, z);
    }

    void OnDrawGizmosSelected()
    {
        // 仅编辑器辅助：标出出生点位置。轨道线不画（轨道不可见）
        var c = center != null ? center : transform;
        Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.6f);
        Gizmos.DrawWireSphere(c.position, 0.25f);
    }
}
