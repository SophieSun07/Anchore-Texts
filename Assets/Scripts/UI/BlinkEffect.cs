using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class BlinkEffect : MonoBehaviour
{
    [Header("闪烁设置")]
    [SerializeField] private float minAlpha = 0.2f;        // 最低透明度
    [SerializeField] private float maxAlpha = 1.0f;        // 最高透明度
    [SerializeField] private float blinkSpeed = 2.0f;      // 闪烁速度（每秒几个周期）

    private Image image;
    private Coroutine blinkCoroutine;

    void Start()
    {
        image = GetComponent<Image>();
        if (image != null)
            blinkCoroutine = StartCoroutine(BlinkRoutine());
    }

    void OnDisable()
    {
        if (blinkCoroutine != null)
            StopCoroutine(blinkCoroutine);
    }

    private IEnumerator BlinkRoutine()
    {
        while (true)
        {
            // 淡出（透明度下降）
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * blinkSpeed;
                float alpha = Mathf.Lerp(maxAlpha, minAlpha, t);
                SetAlpha(alpha);
                yield return null;
            }

            // 淡入（透明度上升）
            t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * blinkSpeed;
                float alpha = Mathf.Lerp(minAlpha, maxAlpha, t);
                SetAlpha(alpha);
                yield return null;
            }
        }
    }

    private void SetAlpha(float alpha)
    {
        Color c = image.color;
        c.a = alpha;
        image.color = c;
    }
}