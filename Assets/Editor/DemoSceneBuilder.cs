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

        // ---- 出生点：房间里随机分布 6 个，全部同一演出平面 ----
        var points = new Transform[6];
        var rnd = new System.Random(20261106);      // 固定种子，便于复现
        for (int i = 0; i < points.Length; i++)
        {
            float ang = (float)(rnd.NextDouble() * Math.PI * 2);
            float rad = 4f + (float)(rnd.NextDouble() * 6f);   // 4~10 单位
            var go = new GameObject("SpawnPoint_" + i);
            go.transform.position = new Vector3(
                Mathf.Cos(ang) * rad, 1.2f, Mathf.Sin(ang) * rad);
            points[i] = go.transform;
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
        EditorUtility.DisplayDialog("大鱼吃小鱼 · 第 1 步",
            "场景已重建：" + ScenePath + "\n\n" +
            "▶ 播放 → 第一人称视角：WASD 移动，左键按住朝视线深处前进。\n\n" +
            "【规则】\n" +
            "· 注视出生点 → 鱼渐渐显形\n" +
            "· 移开视线 → 0.5 秒宽限后消散\n" +
            "· 部分鱼看够 6 秒会自己散掉\n" +
            "· 消散后新鱼在随机出生点出现\n" +
            "· 所有鱼同样大小，不成长", "好");
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
        var template = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        template.name = "FishTemplate";
        template.transform.position = Vector3.zero;
        template.transform.localScale = Vector3.one * 0.5f;
        template.GetComponent<Collider>().enabled = false;   // 鱼不参与碰撞
        SetColor(template, new Color(0.4f, 0.7f, 1f));

        // 把 FishActor 挂进 prefab（漂移 + 消散 + 显形/高亮）
        var actor = template.AddComponent<FishActor>();
        actor.fullScale = 1f;               // 实际尺寸由 FishSpawner 的 fishScale 决定
        actor.baseColor = new Color(0.4f, 0.7f, 1f);
        actor.highlightColor = new Color(1f, 0.95f, 0.6f);

        PrefabUtility.SaveAsPrefabAsset(template, PrefabPath);
        Object.DestroyImmediate(template);
        return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
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
