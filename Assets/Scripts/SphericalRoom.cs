using UnityEngine;

/// <summary>
/// 球形房间：玩家在球壳内游动，边界是球面而不是平面盒。
///
/// 第 3 步：地图从方形房间改为球形。
/// 玩家位置被约束在球壳内侧（radius - margin 内），
/// 不再锁 Y 平面 —— 球形空间里没有"演出平面"的概念。
/// </summary>
public class SphericalRoom : MonoBehaviour
{
    [Header("球体")]
    [Tooltip("房间半径（球心到内壁）")]
    public float radius = 14f;

    [Header("玩家")]
    [Tooltip("玩家距球壁的最小距离（防止穿墙）")]
    public float playerMargin = 1f;

    [Header("中心")]
    [Tooltip("球心。轨道系统也以此为中心")]
    public Transform center;

    /// <summary>球心世界坐标。</summary>
    public Vector3 CenterPosition => center != null ? center.position : transform.position;

    /// <summary>玩家可活动的最大半径。</summary>
    public float PlayerMaxRadius => Mathf.Max(0.1f, radius - playerMargin);

    /// <summary>
    /// 把一个点约束进球壳内侧。超出则投影回球面。
    /// </summary>
    public Vector3 ClampInside(Vector3 worldPos)
    {
        Vector3 c = CenterPosition;
        Vector3 v = worldPos - c;
        float max = PlayerMaxRadius;
        if (v.magnitude <= max) return worldPos;
        if (v.sqrMagnitude < 1e-8f) return c;
        return c + v.normalized * max;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.35f);
        Gizmos.DrawWireSphere(CenterPosition, radius);
    }
}
