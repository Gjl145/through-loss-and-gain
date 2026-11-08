using UnityEngine;

/// <summary>
/// 观察者 = 第一人称相机本体（无实体）。
/// WASD 移动 + 左键按住朝屏幕深处前进。
/// 相机的位置就是"触碰点"：相机进入粒子物的范围，粒子物消散。
///
/// 第 3 步：地图改为球形房间后，玩家在球壳内自由游动 ——
/// 不再锁 Y 平面（球形空间没有"演出平面"），改为由 SphericalRoom 约束在球内。
/// </summary>
public class PlayerSwim : MonoBehaviour
{
    [Header("移动")]
    public float speed = 5f;

    [Header("球形房间")]
    [Tooltip("房间约束。留空则不约束")]
    public SphericalRoom room;

    void Update()
    {
        Vector3 move = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) move += Vector3.forward;
        if (Input.GetKey(KeyCode.S)) move += Vector3.back;
        if (Input.GetKey(KeyCode.A)) move += Vector3.left;
        if (Input.GetKey(KeyCode.D)) move += Vector3.right;
        if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.Space)) move += Vector3.up;   // 上升
        if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.LeftControl)) move += Vector3.down;
        if (Input.GetMouseButton(0)) move += Vector3.forward;   // 左键按住 = 朝视线深处前进

        // 球形空间里允许任意方向移动 → 不再把 Y 分量清零
        if (move.sqrMagnitude > 1e-6f)
        {
            transform.position += move.normalized * speed * Time.deltaTime;
        }

        // 约束在球壳内侧
        if (room != null)
        {
            transform.position = room.ClampInside(transform.position);
        }
    }
}
