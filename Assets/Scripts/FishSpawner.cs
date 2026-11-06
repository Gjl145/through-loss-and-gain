using UnityEngine;
using System.Collections;

/// <summary>
/// 大鱼吃小鱼 · 注视核心（第 1 步）
///
/// 规则（用户定，不可动摇）：
///   · 注视会显性   —— 被注视的鱼显形
///   · 不注视会消散 —— 失去注视的鱼消散
///   · 部分鱼累积注视时间后会消散 —— 有些鱼看够时长就自己散掉
///   · 消散后在随机出生点出生下一条
///   · 所有鱼同样大小，不做尺寸成长
///
/// 玩家 = 第一人称相机（无实体）。相机的位置与朝向就是"注视"来源。
/// </summary>
public class FishSpawner : MonoBehaviour
{
    [Header("注视判定")]
    [Tooltip("视锥半角（度）。超过这个角度就不算注视")]
    public float viewAngle = 35f;
    [Tooltip("注视距离上限（单位）")]
    public float viewDistance = 25f;

    [Header("显形 / 消散")]
    [Tooltip("注视累积到多少才显形（秒）")]
    public float appearTime = 1f;
    [Tooltip("失去注视后，宽限多久才消散（秒）")]
    public float graceTime = 0.5f;
    [Tooltip("鱼消散动画时长（秒）")]
    public float dissolveDuration = 0.8f;

    [Header("鱼的规格（全部同样造型，只有大小不同；不成长）")]
    public GameObject fishPrefab;
    [Tooltip("鱼的最小尺寸")]
    public float fishScaleMin = 0.35f;
    [Tooltip("鱼的最大尺寸")]
    public float fishScaleMax = 0.85f;
    [Tooltip("漂移速度")]
    public float driftSpeed = 0.4f;

    [Header("球形房间")]
    [Tooltip("鱼与玩家都要待在球壳内")]
    public SphericalRoom room;

    [Header("随机出生点")]
    [Tooltip("空物体数组，作为鱼的出生点。随机取一个。")]
    public Transform[] spawnPoints;

    [Header("普通鱼 / 会看够就散的鱼")]
    [Tooltip("这条鱼看够指定时长后自己消散（<=0 表示普通鱼，永不因注视时长消散）")]
    public float selfDissolveAfter = 0f;
    [Tooltip("随机决定这条鱼是不是'看够就散'型")]
    public bool randomizeSelfDissolve = true;
    [Tooltip("'看够就散'型鱼的概率（0~1）")]
    [Range(0f, 1f)] public float selfDissolveChance = 0.35f;
    [Tooltip("'看够就散'型鱼的注视时长上限（秒）")]
    public float selfDissolveDuration = 6f;

    // ---- 运行时状态 ----
    FishActor current;              // 当前这条鱼
    bool watching;                  // 本帧是否注视中
    float watchTime;                // 本条鱼累计被注视时长
    float unwatchTime;              // 连续不被注视时长
    bool selfDissolveType;          // 这条鱼是否"看够就散"型
    float selfDissolveLimit;        // 这条鱼的自消散阈值

    /// <summary>注视进度 0~1，用于 UI 显示。</summary>
    public float WatchProgress => appearTime <= 0f ? 1f : Mathf.Clamp01(watchTime / appearTime);

    /// <summary>当前鱼的自消散进度 0~1；非该类型返回 0。</summary>
    public float SelfDissolveProgress =>
        (selfDissolveType && selfDissolveLimit > 0f)
            ? Mathf.Clamp01(watchTime / selfDissolveLimit)
            : 0f;

    void Update()
    {
        watching = IsWatchingCurrent();
        UpdateCurrent();
        UpdateWatchIndicators();
    }

    // ------------------------------------------------------------------
    // 注视判定
    // ------------------------------------------------------------------

    /// <summary>相机是否正注视当前这条鱼。</summary>
    bool IsWatchingCurrent()
    {
        if (current == null) return false;
        var cam = Camera.main;
        if (cam == null) return false;

        Vector3 to = current.transform.position - cam.transform.position;
        if (to.magnitude > viewDistance) return false;
        return Vector3.Angle(cam.transform.forward, to) < viewAngle;
    }

    /// <summary>相机是否正注视某个出生点（尚无鱼时，用于决定是否显形）。</summary>
    bool IsWatchingPoint(Transform p)
    {
        if (p == null) return false;
        var cam = Camera.main;
        if (cam == null) return false;

        Vector3 to = p.position - cam.transform.position;
        if (to.magnitude > viewDistance) return false;
        return Vector3.Angle(cam.transform.forward, to) < viewAngle;
    }

    // ------------------------------------------------------------------
    // 鱼的生死
    // ------------------------------------------------------------------

    void UpdateCurrent()
    {
        // ---- 无鱼状态：注视某个出生点 → 显形 ----
        if (current == null)
        {
            if (TryFindWatchedSpawnPoint(out var spot)) SpawnFish(spot);
            return;
        }

        // ---- 有鱼状态 ----
        if (watching)
        {
            watchTime += Time.deltaTime;
            unwatchTime = 0f;

            // 规则：部分鱼累积注视时间后会自己消散
            if (selfDissolveType && watchTime >= selfDissolveLimit)
            {
                DissolveCurrent("看够时长，自行消散");
                return;
            }
        }
        else
        {
            // 规则：不注视会消散（先给宽限）
            unwatchTime += Time.deltaTime;
            if (unwatchTime >= graceTime)
            {
                DissolveCurrent("失去注视，消散");
                return;
            }
        }
    }

    /// <summary>在所有出生点里找一个正被注视的。</summary>
    bool TryFindWatchedSpawnPoint(out Transform spot)
    {
        spot = null;
        if (spawnPoints == null || spawnPoints.Length == 0) return false;

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (spawnPoints[i] == null) continue;
            if (IsWatchingPoint(spawnPoints[i])) { spot = spawnPoints[i]; return true; }
        }
        return false;
    }

    void SpawnFish(Transform spot)
    {
        var fish = Instantiate(fishPrefab, spot.position, Quaternion.identity);
        fish.name = "Fish";

        var actor = fish.GetComponent<FishActor>();
        if (actor == null) actor = fish.AddComponent<FishActor>();
        actor.driftSpeed = driftSpeed;
        actor.shrinkDuration = dissolveDuration;
        actor.room = room;
        // 同一套 prefab（造型完全相同），只有大小随机不同
        actor.SetOverallScale(Random.Range(fishScaleMin, fishScaleMax));

        current = actor;
        watchTime = 0f;
        unwatchTime = 0f;

        // 决定这条鱼是不是"看够就散"型
        selfDissolveType = randomizeSelfDissolve
            ? Random.value < selfDissolveChance
            : selfDissolveAfter > 0f;
        selfDissolveLimit = selfDissolveType
            ? (randomizeSelfDissolve ? selfDissolveDuration : selfDissolveAfter)
            : 0f;

        // 初始状态：未显形（缩到 0），注视累积到 appearTime 才长出来
        actor.SetPresence(0f);
        Debug.Log($"[NoiseFish] 显形中 @ {spot.name} · {(selfDissolveType ? "看够就散型" : "普通型")}");
    }

    void DissolveCurrent(string reason)
    {
        if (current != null)
        {
            current.Dissolve();
            Debug.Log($"[NoiseFish] {reason}");
        }
        current = null;
        watchTime = 0f;
        unwatchTime = 0f;
        selfDissolveType = false;
        selfDissolveLimit = 0f;
        // 下一条鱼在随机出生点出生（由 UpdateCurrent 的无鱼分支处理）
    }

    // ------------------------------------------------------------------
    // 注视反馈：让玩家知道自己在注视哪条鱼
    // ------------------------------------------------------------------

    void UpdateWatchIndicators()
    {
        if (current == null) return;

        // 显形进度：0→1 驱动缩放与亮度
        float appear = WatchProgress;
        current.SetPresence(appear);

        // 注视中 = 高亮；脱离注视 = 灰暗
        current.SetHighlighted(watching);
    }

    void OnDrawGizmosSelected()
    {
        var cam = Camera.main;
        if (cam == null) return;

        // 视锥
        Gizmos.color = new Color(1f, 0.9f, 0.4f, 0.25f);
        Vector3 left = Quaternion.Euler(0f, -viewAngle, 0f) * cam.transform.forward;
        Vector3 right = Quaternion.Euler(0f, viewAngle, 0f) * cam.transform.forward;
        Gizmos.DrawRay(cam.transform.position, left * viewDistance);
        Gizmos.DrawRay(cam.transform.position, right * viewDistance);
        Gizmos.DrawRay(cam.transform.position, cam.transform.forward * viewDistance);

        // 出生点
        if (spawnPoints != null)
        {
            foreach (var p in spawnPoints)
            {
                if (p == null) continue;
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(p.position, 0.3f);
            }
        }
    }
}
