using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// 场景生成器（第 1 步）
///
/// 结构：
///   玩家 = 第一人称相机（无实体，相机即玩家）
///   房间 = 一个地面 + 四面墙的空房间
///   出生点 = N 个隐形空物体，分布在房间里
///   FishSpawner = 挂在相机上，统管"注视显性 / 不注视消散 / 随机出生点"
/// </summary>
public static class DemoSceneBuilder
{
    const string MENU = "Tools/生成大鱼吃小鱼演示场景";
    const string ScenePath = "Assets/Scenes/EatDemo.unity";
    const string PrefabPath = "Assets/Scenes/FishTemplate.prefab";

    const int SpawnPointCount = 5;      // 出生点数量（沿用原设定）
    const float SpawnPointHeight = 1.6f; // 轨道平面基准高度（与鱼同层，且留出倾角余量）

    // 轨道参数：4 条公转轨道（半径 / 角速度 / 初始相位 / 倾角）
    // 半径与角速度刻意不等 → 内圈快、外圈慢，产生错落的公转节奏
    // 倾角限制在 ±5° 内：再大会让低轨点的鱼穿进地板（已验算）
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

        // ---- 玩家 = 第一人称相机（相机即玩家）----
        var cam = GameObject.Find("Main Camera");
        cam.transform.position = new Vector3(0, 1.6f, -10f);
        cam.transform.rotation = Quaternion.identity;
        cam.GetComponent<Camera>().nearClipPlane = 0.05f;
        var swim = cam.AddComponent<PlayerSwim>();   // 相机位置 = 玩家位置

        // ---- 注视核心挂在相机上 ----
        var spawner = cam.AddComponent<FishSpawner>();

        // ---- 空房间：地面 + 四面墙 ----
        BuildRoom();

        // ---- 鱼模板 prefab（球体灰盒）----
        var fishPrefab = BuildFishPrefab();

        // ---- 出生点 ×5：中心 1 个静止 + 4 个绕中心公转（行星轨道）----
        var center = new GameObject("OrbitCenter");
        center.transform.position = Vector3.zero;

        var points = new Transform[SpawnPointCount];

        // 中心点（静止，"恒星"）
        var hubGo = new GameObject("SpawnPoint_Center");
        hubGo.transform.position = new Vector3(0f, SpawnPointHeight, 0f);
        points[0] = hubGo.transform;

        // 4 条公转轨道
        for (int i = 0; i < OrbitSpecs.Length && i + 1 < points.Length; i++)
        {
            var spec = OrbitSpecs[i];
            var go = new GameObject("SpawnPoint_Orbit_" + i);
            var orbit = go.AddComponent<OrbitSpawnPoint>();
            orbit.center = center.transform;
            orbit.radius = spec.radius;
            orbit.angularSpeed = spec.speed;
            orbit.phase = spec.phase;
            orbit.inclination = spec.inclination;
            orbit.planeY = SpawnPointHeight;
            points[i + 1] = go.transform;
        }
        spawner.spawnPoints = points;

        // ---- 参数 ----
        spawner.fishPrefab = fishPrefab;
        spawner.fishScale = 0.5f;      // 固定大小
        spawner.appearTime = 1f;       // 注视 1 秒显形
        spawner.graceTime = 0.5f;      // 脱离注视 0.5 秒后消散
        spawner.selfDissolveChance = 0.35f;
        spawner.selfDissolveDuration = 6f;

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorUtility.DisplayDialog("大鱼吃小鱼 · 第 2 步（分段消散）",
            "场景已重建：" + ScenePath + "\n\n" +
            "▶ 播放 → 第一人称视角：WASD 移动，左键按住朝视线深处前进。\n\n" +
            "【规则】\n" +
            "· 注视出生点 → 鱼渐渐显形\n" +
            "· 移开视线 → 0.5 秒宽限后消散\n" +
            "· 部分鱼看够 6 秒会自己散掉\n" +
            "· 消散后新鱼在随机出生点出现\n" +
            "· 所有鱼同样大小，不成长\n\n" +
            "【第 2 步】鱼分头/身/尾三段，消散时逐段崩解\n" +
            "（头先散 → 身 → 尾），不是整条鱼一起缩没。", "好");
    }

    static void BuildRoom()
    {
        var roomRoot = new GameObject("Room");

        // 地面
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor";
        floor.transform.SetParent(roomRoot.transform);
        floor.transform.position = new Vector3(0, -0.5f, 0);
        floor.transform.localScale = new Vector3(24f, 1f, 24f);
        SetColor(floor, new Color(0.22f, 0.24f, 0.28f));

        // 四面墙
        MakeWall(roomRoot.transform, "Wall_N", new Vector3(0, 3f, 12f), new Vector3(24f, 6f, 1f));
        MakeWall(roomRoot.transform, "Wall_S", new Vector3(0, 3f, -12f), new Vector3(24f, 6f, 1f));
        MakeWall(roomRoot.transform, "Wall_E", new Vector3(12f, 3f, 0), new Vector3(1f, 6f, 24f));
        MakeWall(roomRoot.transform, "Wall_W", new Vector3(-12f, 3f, 0), new Vector3(1f, 6f, 24f));
    }

    static void MakeWall(Transform parent, string name, Vector3 pos, Vector3 scale)
    {
        var w = GameObject.CreatePrimitive(PrimitiveType.Cube);
        w.name = name;
        w.transform.SetParent(parent);
        w.transform.position = pos;
        w.transform.localScale = scale;
        SetColor(w, new Color(0.16f, 0.17f, 0.2f));
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
        actor.fullScale = 1f;               // 实际尺寸由 FishSpawner 的 fishScale 决定
        actor.baseColor = new Color(0.4f, 0.7f, 1f);
        actor.highlightColor = new Color(1f, 0.95f, 0.6f);
        actor.shrinkDuration = 0.9f;
        actor.segmentStagger = 0.18f;
        // 数组头部 = 最先消散 → 头先散，然后身，最后尾
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
