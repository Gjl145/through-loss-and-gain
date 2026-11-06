using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// 场景生成器（第 3 步 · 球形房间）
///
/// 结构：
///   玩家 = 第一人称相机（无实体，相机即玩家）
///   房间 = 一个球壳（SphericalRoom），玩家与鱼都在球内
///   出生点 = 中心 1 个静止 + 4 条公转轨道（行星轨道，轨道不可见）
///   鱼 = 同一个 prefab（造型完全相同），只有大小不同
///   FishSpawner = 挂在相机上，统管"注视显性 / 不注视消散 / 随机出生点"
/// </summary>
public static class DemoSceneBuilder
{
    const string MENU = "Tools/生成大鱼吃小鱼演示场景";
    const string ScenePath = "Assets/Scenes/EatDemo.unity";
    const string PrefabPath = "Assets/Scenes/FishTemplate.prefab";

    const int SpawnPointCount = 5;   // 出生点数量（沿用原设定）
    const float RoomRadius = 14f;    // 球形房间半径
    const float PlayerMargin = 1f;   // 玩家距球壁的最小距离

    // 轨道参数：4 条公转轨道（半径 / 角速度 / 初始相位 / 倾角）
    // 半径与角速度刻意不等 → 内圈快、外圈慢，像真实天体
    // 轨道平面全部穿过球心（真正的行星系统）
    struct OrbitSpec
    {
        public float radius, speed, phase, inclination;
        public OrbitSpec(float r, float s, float p, float i)
        { radius = r; speed = s; phase = p; inclination = i; }
    }

    static readonly OrbitSpec[] OrbitSpecs =
    {
        new OrbitSpec(3.5f,  0.50f, 0.0f,               0f),   // 内圈：最快（周期 12.6s）
        new OrbitSpec(5.5f,  0.34f, Mathf.PI * 0.5f,     5f),   // 周期 18.5s
        new OrbitSpec(7.5f,  0.24f, Mathf.PI,           -5f),   // 周期 26.2s
        new OrbitSpec(9.5f,  0.17f, Mathf.PI * 1.5f,     3f),   // 外圈：最慢（周期 37.0s）
    };

    [MenuItem(MENU)]
    public static void Build()
    {
        EnsureFolder("Assets/Scenes");

        var scene = EditorSceneManager.NewScene(
            NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // ---- 球心（恒星位置，也是轨道公转中心）----
        var centerGo = new GameObject("RoomCenter");
        centerGo.transform.position = Vector3.zero;

        // ---- 球壳（可见的球形内壁）----
        var shell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        shell.name = "RoomShell";
        shell.transform.position = Vector3.zero;
        shell.transform.localScale = Vector3.one * (RoomRadius * 2f);
        shell.GetComponent<Collider>().enabled = false;
        // 内壁可见用双面材质，不用负缩放（负缩放会翻转法线导致光照反掉）
        SetDoubleSidedColor(shell, new Color(0.10f, 0.12f, 0.16f));

        // ---- 球形房间约束组件 ----
        var room = shell.AddComponent<SphericalRoom>();
        room.radius = RoomRadius;
        room.playerMargin = PlayerMargin;
        room.center = centerGo.transform;

        // ---- 玩家 = 第一人称相机（相机即玩家）----
        var cam = GameObject.Find("Main Camera");
        cam.transform.position = new Vector3(0, 0, -11f);
        cam.transform.rotation = Quaternion.identity;
        cam.GetComponent<Camera>().nearClipPlane = 0.05f;
        cam.GetComponent<Camera>().farClipPlane = RoomRadius * 3f;
        var swim = cam.AddComponent<PlayerSwim>();   // 相机位置 = 玩家位置
        swim.room = room;

        // ---- 注视核心挂在相机上 ----
        var spawner = cam.AddComponent<FishSpawner>();
        spawner.room = room;

        // ---- 鱼 prefab（同一套造型，所有鱼共用）----
        var fishPrefab = BuildFishPrefab();

        // ---- 出生点 ×5：中心 1 个静止 + 4 个绕中心公转 ----
        var points = new Transform[SpawnPointCount];

        var hubGo = new GameObject("SpawnPoint_Center");
        hubGo.transform.position = centerGo.transform.position;
        points[0] = hubGo.transform;

        for (int i = 0; i < OrbitSpecs.Length && i + 1 < points.Length; i++)
        {
            var spec = OrbitSpecs[i];
            var go = new GameObject("SpawnPoint_Orbit_" + i);
            var orbit = go.AddComponent<OrbitSpawnPoint>();
            orbit.center = centerGo.transform;
            orbit.radius = spec.radius;
            orbit.angularSpeed = spec.speed;
            orbit.phase = spec.phase;
            orbit.inclination = spec.inclination;
            points[i + 1] = go.transform;
        }
        spawner.spawnPoints = points;

        // ---- 参数 ----
        spawner.fishPrefab = fishPrefab;
        spawner.fishScaleMin = 0.35f;      // 同一造型，只有大小不同
        spawner.fishScaleMax = 0.85f;

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorUtility.DisplayDialog("大鱼吃小鱼 · 第 3 步（球形房间）",
            "场景已重建：" + ScenePath + "\n\n" +
            "▶ 播放 → 第一人称视角：\n" +
            "   WASD 移动 · Space/E 上升 · Q/Ctrl 下降 · 左键按住朝视线深处前进\n\n" +
            "【规则】\n" +
            "· 注视出生点 → 鱼渐渐显形\n" +
            "· 移开视线 → 0.5 秒宽限后消散\n" +
            "· 部分鱼看够 6 秒会自己散掉\n" +
            "· 消散后新鱼在随机出生点出现\n" +
            "· 鱼造型完全相同，只有大小不同\n\n" +
            "【第 3 步】球形房间\n" +
            "· 半径 " + RoomRadius + "，玩家与鱼都在球壳内\n" +
            "· 出生点：中心 1 个静止 + 4 条公转轨道（行星式，轨道不可见）\n" +
            "· 鱼分头/身/尾三段，消散时逐段崩解（头→身→尾）", "好");
    }

    static GameObject BuildFishPrefab()
    {
        // 鱼 = 一个空父物体 + 三段（头/身/尾），各段独立缩放淡出 → 分段消散
        // 段位置经过计算，保证首尾交叠相连（不会散成三个球）
        var root = new GameObject("FishTemplate");

        var head = MakeSegment(root.transform, "Head", new Vector3(0f, 0f, 0.36f),
                               new Vector3(0.30f, 0.28f, 0.30f), PrimitiveType.Sphere);
        var body = MakeSegment(root.transform, "Body", new Vector3(0f, 0f, 0f),
                               new Vector3(0.44f, 0.40f, 0.44f), PrimitiveType.Sphere);
        var tail = MakeSegment(root.transform, "Tail", new Vector3(0f, 0f, -0.36f),
                               new Vector3(0.12f, 0.46f, 0.32f), PrimitiveType.Cube);

        // 把 FishActor 挂到父物体上（漂移 + 消散 + 显形/高亮）
        var actor = root.AddComponent<FishActor>();
        actor.fullScale = 1f;               // 实际尺寸由 FishSpawner 的随机大小决定
        actor.baseColor = new Color(0.4f, 0.7f, 1f);
        actor.highlightColor = new Color(1f, 0.95f, 0.6f);
        actor.shrinkDuration = 0.9f;
        actor.segmentStagger = 0.18f;
        // 数组顺序 = 消散顺序：头先散，然后身，最后尾
        actor.segments = new[] { head, body, tail };

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
    }

    /// <summary>做一段鱼身：几何体 + 渲染器 + FishSegment。</summary>
    static FishSegment MakeSegment(Transform parent, string name, Vector3 localPos,
                                   Vector3 localScale, PrimitiveType type)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;
        go.GetComponent<Collider>().enabled = false;   // 鱼不参与碰撞
        SetColor(go, new Color(0.4f, 0.7f, 1f));

        var seg = go.AddComponent<FishSegment>();
        seg.target = go.transform;
        seg.bodyRenderer = go.GetComponent<Renderer>();
        return seg;
    }

    static void SetColor(GameObject go, Color c)
    {
        var r = go.GetComponent<Renderer>();
        if (r == null) return;
        var m = new Material(Shader.Find("Standard"));
        m.color = c;
        r.sharedMaterial = m;
    }

    /// <summary>
    /// 双面材质：球壳内壁要从里面看见。
    /// 不用负缩放（会翻转法线导致光照反掉），改用 _Cull=Off。
    /// </summary>
    static void SetDoubleSidedColor(GameObject go, Color c)
    {
        var r = go.GetComponent<Renderer>();
        if (r == null) return;
        var m = new Material(Shader.Find("Standard"));
        m.color = c;
        // Standard shader 的剔除属性
        if (m.HasProperty("_Cull")) m.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        m.EnableKeyword("_DOUBLESIDED_ON");
        r.sharedMaterial = m;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parts = path.Split('/');
        var cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            var next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }
}
