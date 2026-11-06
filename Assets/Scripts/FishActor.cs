using UnityEngine;
using System.Collections;

/// <summary>
/// 鱼身的一段（头 / 身 / 尾 …）。
///
/// 第 2 步：鱼由多段构成，每段独立缩放淡出 → 分段消散。
/// 段的顺序由 FishActor 的 segments 数组决定（数组头部 = 最先消散）。
///
/// 注意：本组件的 target 默认是自己，但可指向别的物体，
/// 这样"单段退化"时可以让 FishActor 自己充当那一段。
/// </summary>
public class FishSegment : MonoBehaviour
{
    [Tooltip("要缩放/渲染的目标。留空 = 自己")]
    public Transform target;

    public Renderer bodyRenderer;

    Vector3 baseScale;        // 初始尺寸（Awake 时缓存）
    Vector3 currentScale;     // 当前尺寸（每次 Apply 记录）

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");

    void Awake()
    {
        if (target == null) target = transform;
        if (bodyRenderer == null) bodyRenderer = GetComponent<Renderer>();
        if (bodyRenderer != null)
        {
            // 实例化材质，避免改到 prefab 共享的材质
            bodyMaterial = bodyRenderer.material;
        }
        baseScale = target.localScale;
        currentScale = baseScale;
    }

    Material bodyMaterial;

    /// <summary>当前实际尺寸（供消散时取起点）。</summary>
    public Vector3 CurrentScale => currentScale;

    /// <summary>
    /// 设定本段的尺寸倍率与透明度。
    /// scaleMul 乘在**初始尺寸**上（不是当前尺寸），因此可反复调用而不会累积缩小。
    /// </summary>
    public void Apply(float scaleMul, float alpha, Color color)
    {
        // 下限极小值而非 0：零缩放会产生奇异矩阵，某些平台渲染出 NaN 瑕疵。
        // 真正"消失"靠下面的 alpha==0 时关闭渲染器。
        currentScale = baseScale * Mathf.Max(0.001f, scaleMul);
        target.localScale = currentScale;

        if (bodyMaterial == null) return;
        color.a = alpha;
        if (bodyMaterial.HasProperty(BaseColorId)) bodyMaterial.SetColor(BaseColorId, color);
        if (bodyMaterial.HasProperty(ColorId)) bodyMaterial.SetColor(ColorId, color);

        // 完全透明时直接关掉渲染器 —— 不留残影，也不依赖材质是否开了透明混合
        if (bodyRenderer != null)
        {
            bool visible = alpha > 0.002f;
            if (bodyRenderer.enabled != visible) bodyRenderer.enabled = visible;
        }
    }
}

/// <summary>
/// 鱼本体：锥内缓慢漂移 + 缓缓消散（缩小淡出 + 微尘缓缓上浮）。
///
/// 第 1 步：
///   · SetPresence(0~1) —— 显形程度。0 = 隐形，1 = 完全显形。
///   · SetHighlighted(bool) —— 注视高亮，让玩家知道自己在看哪条。
///
/// 第 2 步：多段鱼身 + 分段消散
///   · 鱼由若干 FishSegment 组成（头 → 身 → 尾），挂在同一个父物体下
///   · 消散时**逐段进行**：前面的段先散完，后面的段才开始
///   · 整条鱼的总消散时长保持不变（各段分摊），演出节奏与单段时一致
/// </summary>
public class FishActor : MonoBehaviour
{
    public float driftSpeed = 0.4f;
    public float shrinkDuration = 0.8f;   // 整条鱼消散的总时长

    [Header("显形")]
    [Tooltip("完全显形时的基础尺寸")]
    public float fullScale = 1f;

    [Header("分段")]
    [Tooltip("鱼的各段。数组头部 = 最先消散的那一段")]
    public FishSegment[] segments;
    [Tooltip("相邻两段开始消散的时间间隔（占总时长的比例）")]
    [Range(0f, 0.5f)] public float segmentStagger = 0.18f;
    [Tooltip("每段散尽时是否额外崩出碎块（体素碎片感）")]
    public bool spawnDebrisOnDissolve = true;

    [Header("高亮")]
    public Color baseColor = new Color(0.4f, 0.7f, 1f);
    public Color highlightColor = new Color(1f, 0.95f, 0.6f);

    Vector3 driftDir;
    float driftTimer;
    bool dissolving;

    float presence = 1f;          // 当前显形程度 0~1
    bool highlighted;
    float overallScale = 1f;      // 整条鱼的尺寸倍率（由 FishSpawner 设置）

    /// <summary>整条鱼的尺寸倍率。与"所有鱼同样大小"的规则对应。</summary>
    public void SetOverallScale(float s)
    {
        overallScale = s;
        ApplyAllSegments();
    }

    /// <summary>段的目标倍率 = 整鱼倍率 × 显形倍率。</summary>
    float SegmentMultiplier(float presenceMul) => overallScale * presenceMul;

    void Awake()
    {
        if (segments == null || segments.Length == 0)
        {
            // 没配段就退化成单段：让 FishActor 自己充当那一段
            // （prefab 配置失误时仍能跑，不会变成隐形物体）
            var seg = gameObject.AddComponent<FishSegment>();
            seg.target = transform;
            seg.bodyRenderer = GetComponent<Renderer>();
            segments = new[] { seg };
        }
    }

    void Update()
    {
        if (dissolving) return;

        driftTimer -= Time.deltaTime;
        if (driftTimer <= 0f)
        {
            driftTimer = Random.Range(1.5f, 3.5f);
            driftDir = Random.onUnitSphere;
            driftDir.y = 0f;                     // 漂移锁定演出平面
        }
        transform.position += driftDir * driftSpeed * Time.deltaTime;
    }

    /// <summary>设定显形程度 0~1。0 = 隐形，1 = 完全显形。</summary>
    public void SetPresence(float p)
    {
        presence = Mathf.Clamp01(p);
        ApplyAllSegments();
    }

    /// <summary>注视高亮。</summary>
    public void SetHighlighted(bool on)
    {
        highlighted = on;
        ApplyAllSegments();
    }

    void ApplyAllSegments()
    {
        // 显形时整条鱼一起长出来（各段按自身初始尺寸等比缩放）
        float s = SegmentMultiplier(Mathf.Lerp(0.02f, 1f, presence));
        Color c = highlighted ? highlightColor : baseColor;
        c.a = presence;

        if (segments == null) return;
        for (int i = 0; i < segments.Length; i++)
        {
            var seg = segments[i];
            if (seg == null) continue;
            seg.Apply(s, presence, c);
        }
    }

    /// <summary>缓缓消散：逐段进行，每段缩小淡出并释放缓缓上浮的微尘。</summary>
    public void Dissolve()
    {
        if (dissolving) return;
        dissolving = true;
        StartCoroutine(DissolveRoutine());
    }

    IEnumerator DissolveRoutine()
    {
        int count = (segments == null) ? 0 : segments.Length;
        if (count == 0) { Destroy(gameObject); yield break; }

        // 各段分摊总时长：第 i 段在自己的时间窗内从当前尺寸缩到 0
        // 时间窗互相错开 segmentStagger，形成"逐段崩解"的观感
        float perSegment = shrinkDuration / count;
        float stagger = Mathf.Min(segmentStagger * shrinkDuration, perSegment * 0.9f);
        Color c = highlighted ? highlightColor : baseColor;

        for (int i = 0; i < count; i++)
        {
            var seg = segments[i];
            if (seg == null) continue;

            Vector3 startScale = seg.CurrentScale;
            float startMul = SegmentMultiplier(Mathf.Lerp(0.02f, 1f, presence));
            float t = 0f;
            float dustTimer = 0f;

            while (t < perSegment)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / perSegment);

                // 本段缩小淡出（相对起始尺寸）
                seg.Apply(startMul * (1f - k), 1f - k, c);

                // 每 0.12 秒放一颗缓缓上浮的微尘
                dustTimer -= Time.deltaTime;
                if (dustTimer <= 0f)
                {
                    dustTimer = 0.12f;
                    SpawnDust(seg.transform.position, startScale.x * 0.4f);
                }
                yield return null;
            }

            // 本段散尽（alpha=0 → 渲染器关闭，不留残影）
            if (spawnDebrisOnDissolve)
            {
                for (int d = 0; d < 6; d++)
                {
                    SpawnDust(seg.transform.position + Random.insideUnitSphere * 0.15f, 0.2f);
                }
            }
            seg.Apply(0f, 0f, c);

            // 等 stagger 再散下一段（最后一段不再等）
            if (i < count - 1)
            {
                float waited = 0f;
                while (waited < stagger)
                {
                    waited += Time.deltaTime;
                    yield return null;          // 等待期间后面的段保持原样
                }
            }
        }

        Destroy(gameObject);
    }

    void SpawnDust(Vector3 pos, float radius)
    {
        var p = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        p.transform.position = pos + Random.insideUnitSphere * radius;
        p.transform.localScale = Vector3.one * 0.05f;
        var d = p.AddComponent<DustParticle>();
        d.velocity = new Vector3(Random.Range(-0.3f, 0.3f),
            Random.Range(0.4f, 0.9f),        // 缓缓上浮
            Random.Range(-0.3f, 0.3f));
    }
}

/// <summary>微尘：缓缓上浮，几秒后消失。</summary>
public class DustParticle : MonoBehaviour
{
    public Vector3 velocity;
    float life = 2.5f;

    void Update()
    {
        transform.position += velocity * Time.deltaTime;
        life -= Time.deltaTime;
        if (life <= 0f) Destroy(gameObject);
    }
}
