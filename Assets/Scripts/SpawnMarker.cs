using UnityEngine;

/// <summary>
/// 出生点标记：一颗缓慢脉动的小光点。
///
/// 轨道本身不可见（用户设定），但玩家必须知道该往哪看 ——
/// 否则在球形空间里盲目转头，注视机制根本无法触发。
/// 所有出生点用**同一个** prefab，造型完全一致（用户设定）。
/// </summary>
public class SpawnMarker : MonoBehaviour
{
    [Header("外观")]
    [Tooltip("光点颜色")]
    public Color color = new Color(0.45f, 0.75f, 1f);

    [Tooltip("光点大小")]
    public float size = 0.12f;

    [Header("脉动")]
    [Tooltip("脉动周期（秒）")]
    public float pulsePeriod = 2.4f;

    [Tooltip("脉动时的最大放大倍数")]
    public float pulseScale = 1.6f;

    Renderer bodyRenderer;
    Material bodyMaterial;
    float baseTime;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    void Awake()
    {
        bodyRenderer = GetComponent<Renderer>();
        if (bodyRenderer != null) bodyMaterial = bodyRenderer.material;

        baseTime = Random.Range(0f, pulsePeriod);   // 各点错开，避免同步闪烁
        Apply(1f);
    }

    void Update()
    {
        float phase = Mathf.Repeat(Time.time + baseTime, pulsePeriod) / pulsePeriod;
        // 一个来回的呼吸曲线：0→1→0
        float k = Mathf.Sin(phase * Mathf.PI * 2f) * 0.5f + 0.5f;
        float s = Mathf.Lerp(1f, pulseScale, k);
        Apply(s);
    }

    void Apply(float scale)
    {
        transform.localScale = Vector3.one * (size * scale);

        if (bodyMaterial == null) return;
        // 自发光，让光点在暗房间里也能看见
        bodyMaterial.EnableKeyword("_EMISSION");
        if (bodyMaterial.HasProperty(EmissionColorId))
        {
            Color e = color * (1.6f * scale / pulseScale);
            bodyMaterial.SetColor(EmissionColorId, e);
        }
        if (bodyMaterial.HasProperty(BaseColorId)) bodyMaterial.SetColor(BaseColorId, color);
        if (bodyMaterial.HasProperty(ColorId)) bodyMaterial.SetColor(ColorId, color);
    }
}
