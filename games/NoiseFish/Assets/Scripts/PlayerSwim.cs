using UnityEngine;

/// <summary>观察者 = 第一人称相机本体（无实体）。
/// WASD 移动 + 左键按住朝屏幕深处前进 + Y 锁定演出平面。
/// 相机的位置就是"触碰点"：相机进入粒子物的范围，粒子物消散。</summary>
public class PlayerSwim : MonoBehaviour
{
    [Header("移动")]
    public float speed = 5f;

    [Header("演出平面高度（与鱼同层，防穿帮）")]
    public float planeY = 0f;

    [Header("活动范围")]
    public Vector3 boundaryCenter = Vector3.zero;
    public Vector3 boundarySize = new Vector3(40, 2, 40);

    Bounds bounds;

    void Start()
    {
        bounds = new Bounds(boundaryCenter, boundarySize);
        var p = transform.position; p.y = planeY; transform.position = p;
    }

    void Update()
    {
        Vector3 move = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) move += Vector3.forward;
        if (Input.GetKey(KeyCode.S)) move += Vector3.back;
        if (Input.GetKey(KeyCode.A)) move += Vector3.left;
        if (Input.GetKey(KeyCode.D)) move += Vector3.right;
        if (Input.GetMouseButton(0)) move += Vector3.forward;   // 左键按住 = 朝视线深处前进

        transform.position += move.normalized * speed * Time.deltaTime;

        var pos = bounds.ClosestPoint(transform.position);
        pos.y = planeY;                                         // Y 锁定演出平面
        transform.position = pos;
    }
}
