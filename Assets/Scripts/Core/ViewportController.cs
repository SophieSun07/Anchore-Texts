using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class ViewportController : MonoBehaviour
{
    [Header("组件")]
    [SerializeField] private RectTransform maskRect;
    [SerializeField] private Image maskImage;

    [Header("洞口尺寸 (归一化 0~1)")]
    [SerializeField] private Vector2 holeSize = new Vector2(0.25f, 0.25f);

    [Header("羽化宽度 (归一化)")]
    [SerializeField, Range(0.001f, 0.1f)] private float feather = 0.02f;
    [Header("圆角半径 (归一化)")]
    [SerializeField, Range(0f, 0.2f)] private float radius = 0.05f;
    private Material maskMaterial;
    private bool isRevealing = false;
    public bool IsRevealing => isRevealing;
    private bool isRevealed = false;
    public bool IsRevealed => isRevealed;
    private Coroutine revealCoroutine;

    void Start()
    {
        if (maskRect == null) maskRect = GetComponent<RectTransform>();
        if (maskImage == null) maskImage = GetComponent<Image>();

        maskMaterial = new Material(maskImage.material);
        maskImage.material = maskMaterial;

        // 设置所有初始属性
        maskMaterial.SetVector("_HoleSize", holeSize);
        maskMaterial.SetFloat("_Radius", radius);
        maskMaterial.SetFloat("_Feather", feather);
        maskMaterial.SetColor("_MaskColor", Color.black);
    }

    void Update()
    {
        if (isRevealing || isRevealed)
        {
            // 揭示中或已揭示：洞口中置，不跟随鼠标
            maskMaterial.SetVector("_HoleCenter", new Vector2(0.5f, 0.5f));
            return;
        }

        if (Mouse.current == null) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            maskRect,
            mousePos,
            null,
            out Vector2 localPoint
        );

        // 转换为归一化 UV
        Vector2 uv = Rect.PointToNormalized(maskRect.rect, localPoint);
        maskMaterial.SetVector("_HoleCenter", uv);
    }

    /// <summary>
    /// 判断屏幕坐标是否在圆角洞口内部
    /// </summary>
    public bool IsPointInsideHole(Vector2 screenPoint)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            maskRect, screenPoint, null, out Vector2 localPoint);
        Vector2 uv = Rect.PointToNormalized(maskRect.rect, localPoint);
        Vector2 p = uv - (Vector2)maskMaterial.GetVector("_HoleCenter");

        Vector2 halfSize = holeSize * 0.5f;
        // 使用本地 radius 变量
        Vector2 d = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - halfSize + Vector2.one * radius;
        float dist = Vector2.Max(d, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f) - radius;
        return dist < 0f;
    }

    // 供外部动态调整洞口大小（可选）
    public void SetHoleSize(Vector2 size)
    {
        holeSize = size;
        maskMaterial.SetVector("_HoleSize", holeSize);
    }

    /// <summary>
    /// 洞口逐渐扩大至目标尺寸（可用于页面揭示）
    /// </summary>
    /// <param name="targetSize">目标洞口尺寸（归一化）</param>
    /// <param name="duration">动画时长（秒）</param>
    /// <param name="curve">缓动曲线</param>
    public void AnimateHoleReveal(Vector2 targetSize, float duration, AnimationCurve curve)
    {
        if (revealCoroutine != null)
            StopCoroutine(revealCoroutine);
        revealCoroutine = StartCoroutine(HoleRevealCoroutine(targetSize, duration, curve));
    }

    /// <summary>停止正在进行的揭示动画</summary>
    public void StopReveal()
    {
        if (revealCoroutine != null)
        {
            StopCoroutine(revealCoroutine);
            revealCoroutine = null;
        }
        isRevealing = false;
    }

    private System.Collections.IEnumerator HoleRevealCoroutine(Vector2 targetSize, float duration, AnimationCurve curve)
    {
        isRevealing = true;
        Vector2 startSize = holeSize;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float curvedT = curve.Evaluate(t);
            holeSize = Vector2.Lerp(startSize, targetSize, curvedT);
            maskMaterial.SetVector("_HoleSize", holeSize);
            yield return null;
        }

        holeSize = targetSize;
        maskMaterial.SetVector("_HoleSize", holeSize);
        isRevealing = false;
        isRevealed = true;
        revealCoroutine = null;
    }

    void OnValidate()
    {
        if (maskImage != null && maskImage.material != null)
        {
            maskImage.material.SetFloat("_Radius", radius);
        }
    }

    
}