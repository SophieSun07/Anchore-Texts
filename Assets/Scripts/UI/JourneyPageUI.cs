using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 历程界面：展示已通关卡缩略图，点击可跳转回对应关卡
/// 每个缩略图的位置由 PageInfo.journeyThumbnailPosition 决定，可在 Inspector 中手动调整
/// </summary>
public class JourneyPageUI : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private Button backButton;             // 返回按钮
    [SerializeField] private Transform contentContainer;   // 放置图片按钮的父容器
    [SerializeField] private GameObject imageButtonPrefab; // 图片按钮预制体（需含 Button + Image）
    // 缩略图尺寸由每页的 PageInfo.journeyThumbnailSize 独立控制

    private List<GameObject> spawnedButtons = new List<GameObject>();

    void Start()
    {
        if (backButton != null)
            backButton.onClick.AddListener(OnBackClicked);
    }

    private void OnBackClicked()
    {
        if (gameManager != null)
            gameManager.CloseJourneyPanel();
    }

    /// <summary>刷新历程列表（打开面板或页面变化时调用）</summary>
    public void RefreshJourney()
    {
        ClearButtons();
        if (gameManager == null) return;

        var visitedInfos = gameManager.GetVisitedPageInfos();
        if (visitedInfos.Count == 0) return;

        if (imageButtonPrefab == null || contentContainer == null) return;

        for (int i = 0; i < visitedInfos.Count; i++)
        {
            var pageInfo = visitedInfos[i];
            GameObject btnObj = Instantiate(imageButtonPrefab, contentContainer);
            spawnedButtons.Add(btnObj);

            // 设置位置和尺寸
            var rt = btnObj.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = pageInfo.journeyThumbnailPosition;
                rt.sizeDelta = pageInfo.journeyThumbnailSize;
            }

            // 设置图片
            var img = btnObj.GetComponent<Image>();
            if (img != null && pageInfo.journeySprite != null)
            {
                img.sprite = pageInfo.journeySprite;
                img.preserveAspect = true;
                img.color = Color.white;
                img.raycastTarget = true;
            }

            // 绑定点击事件
            string pageName = pageInfo.pageName;
            var btn = btnObj.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.AddListener(() => gameManager.JumpToPage(pageName));
            }
        }
    }

    private void ClearButtons()
    {
        foreach (var obj in spawnedButtons)
        {
            if (obj != null) Destroy(obj);
        }
        spawnedButtons.Clear();
    }

    void OnDestroy()
    {
        ClearButtons();
    }
}
