using UnityEngine;
using System.Collections;

/// <summary>
/// 鱼本体：锥内缓慢漂移 + 缓缓消散（缩小淡出 + 微尘缓缓上浮）。
///
/// 第 1 步新增：
///   · SetPresence(0~1) —— 显形程度。0 = 隐形，1 = 完全显形。
///     注视累积时用它把鱼"长出来"，取代原来瞬间出现的做法。
///   · SetHighlighted(bool) —— 注视高亮，让玩家知道自己在看哪条。
/// </summary>
public class FishActor : MonoBehaviour
{
    public float driftSpeed = 0.4f;
    public float shrinkDuration = 0.8f;   // 缓缓消散的时长

    [Header("显形")]
    [Tooltip("完全显形时的基础尺寸")]
    public float fullScale = 1f;

    [Header("高亮")]
    public Color baseColor = new Color(0.4f, 0.7f, 1f);
    public Color highlightColor = new Color(1f, 0.95f, 0.6f);

    Vector3 driftDir;
    float driftTimer;
    bool dissolving;

    float presence = 1f;          // 当前显形程度 0~1
    bool highlighted;
    Renderer bodyRenderer;
    Material bodyMaterial;
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");

    void Awake()
    {
        bodyRenderer = GetComponent<Renderer>();
        if (bodyRenderer != null)
        {
            // 实例化材质，避免改到 prefab 共享的材质
            bodyMaterial = bodyRenderer.material;
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
        // 未显形时缩到极小但不完全为 0（避免完全不可见导致的判定困惑）
        float s = Mathf.Lerp(0.02f, 1f, presence);
        transform.localScale = Vector3.one * (fullScale * s);
        ApplyColor();
    }

    /// <summary>注视高亮。</summary>
    public void SetHighlighted(bool on)
    {
        highlighted = on;
        ApplyColor();
    }

    void ApplyColor()
    {
        if (bodyMaterial == null) return;
        Color c = highlighted ? highlightColor : baseColor;
        c.a = presence;
        if (bodyMaterial.HasProperty(BaseColorId)) bodyMaterial.SetColor(BaseColorId, c);
        if (bodyMaterial.HasProperty(ColorId)) bodyMaterial.SetColor(ColorId, c);
    }

    /// <summary>缓缓消散：鱼身缩小淡出，同时释放缓缓上浮的微尘。</summary>
    public void Dissolve()
    {
        if (dissolving) return;
        dissolving = true;
        StartCoroutine(DissolveRoutine());
    }

    IEnumerator DissolveRoutine()
    {
        Vector3 startScale = transform.localScale;
        float t = 0f;
        float dustTimer = 0f;

        while (t < shrinkDuration)
        {
            t += Time.deltaTime;
            // 鱼身缓缓缩小
            transform.localScale = Vector3.Lerp(startScale,
                Vector3.one * 0.01f, t / shrinkDuration);

            // 淡出
            if (bodyMaterial != null)
            {
                Color c = highlighted ? highlightColor : baseColor;
                c.a = Mathf.Clamp01(1f - t / shrinkDuration);
                if (bodyMaterial.HasProperty(BaseColorId)) bodyMaterial.SetColor(BaseColorId, c);
                if (bodyMaterial.HasProperty(ColorId)) bodyMaterial.SetColor(ColorId, c);
            }

            // 每 0.12 秒放一颗缓缓上浮的微尘
            dustTimer -= Time.deltaTime;
            if (dustTimer <= 0f)
            {
                dustTimer = 0.12f;
                var p = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                p.transform.position = transform.position +
                    Random.insideUnitSphere * transform.localScale.x * 0.4f;
                p.transform.localScale = Vector3.one * 0.05f;
                var d = p.AddComponent<DustParticle>();
                d.velocity = new Vector3(Random.Range(-0.3f, 0.3f),
                    Random.Range(0.4f, 0.9f),        // 缓缓上浮
                    Random.Range(-0.3f, 0.3f));
            }
            yield return null;
        }
        Destroy(gameObject);
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
