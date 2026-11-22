using UnityEngine;
using UnityEngine.UI;

public class LinkArea : MonoBehaviour
{
    public string targetPageName;
    public bool isActivated = false;
    public bool isVisited = false;               // 新增：是否已访问

    [Header("高亮图片（激活时显示）")]
    public Image highlightImage;
    [Header("已访问图片（返回后显示）")]
    public Image visitedImage;

    void Awake()
    {
        if (highlightImage == null)
            highlightImage = GetComponentInChildren<Image>(true);
        // 初始状态：高亮图透明但可交互，已访问图隐藏
        if (highlightImage != null)
        {
            Color c = highlightImage.color;
            c.a = 0f;
            highlightImage.color = c;
            highlightImage.raycastTarget = true;
            highlightImage.alphaHitTestMinimumThreshold = 0f;
        }
        if (visitedImage != null)
            visitedImage.gameObject.SetActive(false);
    }

    // 长按激活：显示高亮图，清除已访问状态（如果需要重新激活）
    public void Activate()
    {
        isActivated = true;
        // 如果之前标记为已访问，恢复为未访问（或者保留，取决于设计，这里我们选择重新激活时清除已访问）
        if (highlightImage != null)
        {
            Color c = highlightImage.color;
            c.a = 1f;
            highlightImage.color = c;
        }
        if (visitedImage != null)
            visitedImage.gameObject.SetActive(false);
    }

    // 取消激活：隐藏高亮图，保留已访问状态不受影响
    public void Deactivate()
    {
        isActivated = false;
        if (highlightImage != null)
        {
            Color c = highlightImage.color;
            c.a = 0f;
            highlightImage.color = c;
        }
        // 注意：不清除 isVisited，不清除 visitedImage
    }

    // 标记为已访问：显示已访问图片，隐藏高亮图，但仍保持激活状态（可点击跳转）
    public void MarkVisited()
    {
        isVisited = true;
        // 隐藏高亮图
        if (highlightImage != null)
        {
            Color c = highlightImage.color;
            c.a = 0f;
            highlightImage.color = c;
        }
        // 显示已访问图片
        if (visitedImage != null)
            visitedImage.gameObject.SetActive(true);
        // 保持 isActivated = true，这样仍可被单击跳转
        isActivated = true;
    }
}