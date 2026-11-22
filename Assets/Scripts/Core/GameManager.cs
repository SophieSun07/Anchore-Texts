using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

[System.Serializable]
public class PageInfo
{
    public string pageName;
    public GameObject pageObject;
    
    public string nextPageName; // 默认跳转（用于无超链接时的备用，但此版本可以忽略）

    [Header("洞口揭示")]
    public bool enableHoleReveal = false;           // 是否在此页触发洞口逐渐扩大
    public float revealDuration = 2f;               // 揭示动画时长（秒）
    public AnimationCurve revealCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f); // 缓动曲线
    public bool isLastPage = false;                 // 是否为最后一页（揭示后自动进入结局）
    public float endingDelay = 3f;                  // 揭示后等待多久进入结局（秒）
    [Header("历程")]
    public Sprite journeySprite;                    // 历程界面中显示的缩略图
    public Vector2 journeyThumbnailPosition;        // 缩略图在历程面板中的锚点位置
    public Vector2 journeyThumbnailSize = new Vector2(160f, 120f); // 缩略图尺寸
}

public class GameManager : MonoBehaviour
{
    [Header("页面数据")]
    [SerializeField] private PageInfo[] pages;
    [Header("遮罩视野控制")]
    [SerializeField] private ViewportController viewportController;
    [Header("音频管理")]
    [SerializeField] private AudioManager audioManager;
    [Header("光标纹理")]

    [SerializeField] private Texture2D clickCursor;       // 点击非链接区域时的临时光标
    [Header("设置面板")]
    [SerializeField] private GameObject settingsPanel;
    [Header("历程面板")]
    [SerializeField] private GameObject journeyPanel;
    [Header("开始页面")]
    [SerializeField] private GameObject startPage;
    [Header("结束页面")]
    [SerializeField] private GameObject endingPage;

    [Header("长按动画")]
    [SerializeField] private Texture2D[] pressingCursorFrames;   // 长按时播放的帧序列
    [SerializeField] private float pressingCursorFrameRate = 8f; // 帧率

    [Header("错误冷却惩罚")]
    [SerializeField] private float cooldownDuration = 2f;              // 冷却时长（秒）
    [SerializeField] private Texture2D[] cooldownCursorFrames;         // 光标动画帧（按顺序播放）
    [SerializeField] private float cooldownCursorFrameRate = 4f;       // 帧率（张/秒）
    [Header("光标")]
    [SerializeField] private Texture2D defaultCursor;     // 默认光标（遮罩区域）
    [Header("已激活链接悬停光标")]
    [SerializeField] private Texture2D activatedHoverCursor;   // 鼠标悬停在已激活链接上时显示  
    [Header("已激活链接按下光标")]
    [SerializeField] private Texture2D activatedPressCursor;   // 在已激活链接上按下鼠标时显示
    [Header("放大模式")]
    [SerializeField] private bool enableZoomMode = true;   // 是否允许放大模式
    [SerializeField] private Key zoomToggleKey = Key.Tab;   // 切换键（新输入系统用路径）
    [SerializeField] private Vector2 normalHoleSize = new Vector2(0.25f, 0.25f);
    [SerializeField] private Vector2 zoomHoleSize = new Vector2(0.5f, 0.5f);
    [SerializeField] private float zoomScale = 1.8f;        // 背景放大倍数
    [SerializeField] private float panSpeed = 1.0f;         // 镜头移动灵敏度
    [Header("揭示目标")]
    [SerializeField] private Vector2 fullyRevealedHoleSize = new Vector2(1.5f, 1.5f); // 全揭示时的洞口尺寸（>1 确保覆盖全屏）

    private bool isReturningFromBack = false;   // 防止返回时重复压栈
    private Coroutine pressingAnimationCoroutine;                 // 长按动画协程引用
    private bool isZoomMode = false;
    private InputAction zoomToggleAction;
    private RectTransform currentPageRect;   // 当前活动页面的 RectTransform

    private bool isInCooldown = false;
    private Coroutine cooldownCoroutine;
    // 状态
    private PageInfo currentPage;
    private List<string> visitedPages = new List<string>();
    private SaveData currentSave;
    public SaveData CurrentSave => currentSave;   // 只读属性，让外部访问
    private string savePath;
    private InputAction toggleSettingsAction;

    private bool longPressActivated = false;   // 防止松手时激活后又触发单击
    // 输入
    private InputAction mouseClickAction;
    private InputAction mousePositionAction;
    private bool isMouseDown = false;
    private float mouseDownTime = 0f;
    private const float longPressDuration = 0.5f;
    private LinkArea currentHoveredLink = null;
    private LinkArea activatedLink = null;   // 当前已激活的超链接（等待点击）
    // 内部状态
    private LinkArea pressedLink = null;   // 本次按下时所在的链接（用于松手激活判定）
    private bool isLongPressing = false;   // 是否正处于长按状态（用于更新光标）
    private RectTransform canvasRect;          // Canvas 的 RectTransform（用于获取屏幕尺寸）
    private Vector2 originalPageSize;          // 页面原始尺寸（缩放前）

    // 页面历史：记录 (页面名, 跳转时激活的链接)
    private Stack<(string pageName, LinkArea activatedLink)> pageHistory = new Stack<(string, LinkArea)>();

    private string pageBeforeJourney;   // 打开历程面板前所在的页面

    private bool isEndingActive = false;    // 是否在结局页面
    private Coroutine endingCoroutine;      // 结局延迟协程

    //设置页面
    private void ToggleSettingsPanel()
    {
    if (settingsPanel == null) return;
        settingsPanel.SetActive(!settingsPanel.activeSelf);
    }

    private void ToggleJourneyPanel()
    {
        if (journeyPanel == null) return;
        bool open = !journeyPanel.activeSelf;
        journeyPanel.SetActive(open);
        if (open)
        {
            // 记录打开前所在页面
            pageBeforeJourney = currentPage != null ? currentPage.pageName : "";
            var journeyUI = journeyPanel.GetComponent<JourneyPageUI>();
            if (journeyUI != null)
                journeyUI.RefreshJourney();
        }
    }

    /// <summary>关闭历程面板并返回打开前的页面</summary>
    public void CloseJourneyPanel()
    {
        if (journeyPanel != null)
            journeyPanel.SetActive(false);
        if (!string.IsNullOrEmpty(pageBeforeJourney))
        {
            // 如果当前已经在某页面且不是返回目标，则切换回去
            string target = pageBeforeJourney;
            pageBeforeJourney = "";
            if (currentPage == null || currentPage.pageName != target)
                SwitchToPage(target);
        }
    }
    /// <summary>
    /// 触发错误冷却，如果已经在冷却中则忽略
    /// </summary>
    private void TriggerCooldown()
    {
        if (isInCooldown) return;    // 已经在冷却中，不重复触发
        if (cooldownCoroutine != null)
            StopCoroutine(cooldownCoroutine);
        cooldownCoroutine = StartCoroutine(CooldownCoroutine());

        if (audioManager != null)
            audioManager.PlayCooldownSFX();
    }

    /// <summary>
    /// 冷却协程：播放光标帧动画，持续 cooldownDuration 秒后恢复
    /// </summary>
    private System.Collections.IEnumerator CooldownCoroutine()
    {
        isInCooldown = true;
        float elapsed = 0f;
        int frameCount = cooldownCursorFrames.Length;
        float frameDuration = 1f / cooldownCursorFrameRate;

        int currentFrame = 0;
        while (elapsed < cooldownDuration)
        {
            // 如果有动画帧，播放；否则可保持原光标或设置一个错误光标
            if (frameCount > 0)
            {
                Cursor.SetCursor(cooldownCursorFrames[currentFrame], Vector2.zero, CursorMode.Auto);
                currentFrame = (currentFrame + 1) % frameCount;
            }
            yield return new WaitForSeconds(frameDuration);
            elapsed += frameDuration;
        }

        // 冷却结束，恢复默认光标
        Cursor.SetCursor(defaultCursor, Vector2.zero, CursorMode.Auto);
        isInCooldown = false;
        cooldownCoroutine = null;
    }

    /// <summary>
    /// 循环播放长按光标帧动画，直到被 StopCoroutine 打断
    /// </summary>
    private System.Collections.IEnumerator PressingCursorAnimation()
    {
        if (pressingCursorFrames == null || pressingCursorFrames.Length == 0)
            yield break;

        float frameDuration = 1f / pressingCursorFrameRate;
        int frameIndex = 0;

        while (true)
        {
            Cursor.SetCursor(pressingCursorFrames[frameIndex], Vector2.zero, CursorMode.Auto);
            frameIndex = (frameIndex + 1) % pressingCursorFrames.Length;
            yield return new WaitForSeconds(frameDuration);
        }
    }

    private void ToggleZoomMode()
    {
        if (!enableZoomMode) return;
        isZoomMode = !isZoomMode;

        if (isZoomMode)
        {
            // 进入放大模式：洞口变大，背景放大
            viewportController.SetHoleSize(zoomHoleSize);
            if (currentPageRect != null)
            {
                currentPageRect.localScale = Vector3.one * zoomScale;
            }
        }
        else
        {
            // 退出放大模式：恢复
            viewportController.SetHoleSize(normalHoleSize);
            if (currentPageRect != null)
            {
                currentPageRect.localScale = Vector3.one;
                currentPageRect.anchoredPosition = Vector2.zero; // 居中
            }
        }
    }

    void Awake()
    {
        savePath = Path.Combine(Application.persistentDataPath, "save.json");
        LoadGame();

        // 设置输入系统
        mouseClickAction = new InputAction("Click", binding: "<Mouse>/leftButton");
        mousePositionAction = new InputAction("Position", binding: "<Mouse>/position");
        mouseClickAction.Enable();
        mousePositionAction.Enable();

        // 监听 Esc 键（按下时触发）
        toggleSettingsAction = new InputAction("ToggleSettings", binding: "<Keyboard>/escape");
        toggleSettingsAction.performed += ctx => ToggleSettingsPanel();
        toggleSettingsAction.Enable();

        // 监听 J 键（历程面板）
        InputAction toggleJourneyAction = new InputAction("ToggleJourney", binding: "<Keyboard>/h");
        toggleJourneyAction.performed += ctx => ToggleJourneyPanel();
        toggleJourneyAction.Enable();

        // 初始光标
        Cursor.SetCursor(defaultCursor, Vector2.zero, CursorMode.Auto);
        mouseClickAction = new InputAction("Click", binding: "<Mouse>/leftButton");
        mousePositionAction = new InputAction("Position", binding: "<Mouse>/position");
        mouseClickAction.Enable();
        mousePositionAction.Enable();
        // 如果在 Awake 之前已经启用了，确保这里不重复也行，但保留 Enable
        // 放大模式切换键
        zoomToggleAction = new InputAction("ZoomToggle", binding: "<Keyboard>/tab");
        zoomToggleAction.performed += ctx => ToggleZoomMode();
        zoomToggleAction.Enable();
        canvasRect = GetComponent<RectTransform>();
        InputAction backAction = new InputAction("Back", binding: "<Keyboard>/z");
        backAction.performed += ctx => GoBack();
        backAction.Enable();
    }

    void Start()
    {
        // 锁定分辨率（WebGL 不支持 SetResolution）
        #if !UNITY_WEBGL
        Screen.SetResolution(2560, 1440, FullScreenMode.Windowed);
        #endif
        Application.targetFrameRate = 60;

        // 不自动切换到任何页面，等待开始页面指令
        // 但可以加载存档以备“继续游戏”检查
        if (HasSaveFile())
        {
            LoadGame(); // 读取存档数据到 currentSave
        }
        else
        {
            currentSave = new SaveData(); // 确保有一个初始存档
        }
            // 强制设置默认光标，确保无论是否进入游戏页面，光标都能显示
        if (defaultCursor != null)
            Cursor.SetCursor(defaultCursor, Vector2.zero, CursorMode.Auto);
    }

    void Update()
    {
        // ========== 结局页面：长按退出 ==========
        if (isEndingActive)
        {
            HandleEndingInput();
            return;
        }

        // 拦截：无当前页面、冷却中、设置面板打开时，不处理游戏交互
        if (currentPage == null || isInCooldown) return;
        if (settingsPanel != null && settingsPanel.activeSelf) return;
        if (journeyPanel != null && journeyPanel.activeSelf) return;

        Vector2 mousePos = mousePositionAction.ReadValue<Vector2>();
        bool mousePressed = mouseClickAction.ReadValue<float>() > 0.5f;

        // ========== 放大模式镜头移动 ==========
        if (isZoomMode && currentPageRect != null)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, mousePos, null, out Vector2 localPoint);
            Vector2 centeredUV = Rect.PointToNormalized(canvasRect.rect, localPoint) - new Vector2(0.5f, 0.5f);
            float scaledWidth = originalPageSize.x * zoomScale;
            float scaledHeight = originalPageSize.y * zoomScale;
            Vector2 screenSize = canvasRect.rect.size;
            Vector2 maxMove = new Vector2(
                Mathf.Max(0, (scaledWidth - screenSize.x) / 2f),
                Mathf.Max(0, (scaledHeight - screenSize.y) / 2f)
            );
            Vector2 targetPos = new Vector2(centeredUV.x * maxMove.x * 2f, centeredUV.y * maxMove.y * 2f);
            targetPos = -targetPos;
            targetPos.x = Mathf.Clamp(targetPos.x, -maxMove.x, maxMove.x);
            targetPos.y = Mathf.Clamp(targetPos.y, -maxMove.y, maxMove.y);
            currentPageRect.anchoredPosition = targetPos;
        }

        // ========== 光标更新 ==========
        // 冷却中由 CooldownCoroutine 控制，长按动画由 PressingCursorAnimation 控制，其他情况恢复默认
        if (isInCooldown) { /* 冷却动画控制 */ }
        else if (isLongPressing) { /* 长按动画控制 */ }
        else
        {
            LinkArea hoveredLink = GetLinkUnderMouse(mousePos);
            bool hoveringActivatedLink = hoveredLink != null 
                                        && hoveredLink == activatedLink 
                                        && viewportController.IsPointInsideHole(mousePos);
            bool mouseButtonDown = mouseClickAction.ReadValue<float>() > 0.5f;

            if (hoveringActivatedLink)
            {
                if (mouseButtonDown && activatedPressCursor != null)
                    Cursor.SetCursor(activatedPressCursor, Vector2.zero, CursorMode.Auto);
                else if (activatedHoverCursor != null)
                    Cursor.SetCursor(activatedHoverCursor, Vector2.zero, CursorMode.Auto);
                else
                    Cursor.SetCursor(defaultCursor, Vector2.zero, CursorMode.Auto);
            }
            else
            {
                if (defaultCursor != null)
                    Cursor.SetCursor(defaultCursor, Vector2.zero, CursorMode.Auto);
            }
        }
        // ========== 鼠标按下瞬间 ==========
        if (mousePressed && !isMouseDown)
        {
            isMouseDown = true;
            mouseDownTime = Time.time;
            pressedLink = GetLinkUnderMouse(mousePos);
            isLongPressing = false;
            longPressActivated = false;
        }

        // --- 按住过程中检测长按动画 ---
       // --- 按住过程中检测长按动画 ---
        if (mousePressed && isMouseDown)
        {
            if (!isLongPressing && Time.time - mouseDownTime >= longPressDuration)
            {
                // 如果按下的是已访问链接，忽略长按（不进入动画，也不激活）
                if (pressedLink != null && pressedLink.isVisited)
                {
                    // 什么都不做，让这次按压在松手时作为短按处理
                    // 可以设置一个标记防止重复判断，但直接跳过即可
                }
                else if (pressedLink != null && pressedLink == activatedLink)
                {
                    // 已激活但非已访问链接上的长按：保持按下光标，不进入动画
                    longPressActivated = true;
                }
                else
                {
                    // 正常区域长按，播放动画
                    isLongPressing = true;
                    if (pressingAnimationCoroutine != null)
                        StopCoroutine(pressingAnimationCoroutine);
                    pressingAnimationCoroutine = StartCoroutine(PressingCursorAnimation());
                }
            }
        }


        // ========== 鼠标抬起 ==========
        if (!mousePressed && isMouseDown)
        {
            isMouseDown = false;

            // 停止可能的长按动画
            if (pressingAnimationCoroutine != null)
            {
                StopCoroutine(pressingAnimationCoroutine);
                pressingAnimationCoroutine = null;
            }

            // 有效长按激活新链接（未激活的链接）
            bool validLongPress = isLongPressing
                                && pressedLink != null
                                && GetLinkUnderMouse(mousePos) == pressedLink
                                && viewportController.IsPointInsideHole(mousePos)
                                && pressedLink != activatedLink
                                && !pressedLink.isVisited;

            if (validLongPress)
            {
                // 长按激活新链接
                if (activatedLink != null && activatedLink != pressedLink)
                    activatedLink.Deactivate();
                pressedLink.Activate();
                activatedLink = pressedLink;
                longPressActivated = true;   // 标记已激活
                if (audioManager != null)
                    audioManager.PlayLongPressActivateSFX();
            }
            else
            {
                // 非有效长按
                // 优先检测直接跳转按钮（短按）
                DirectJumpButton directButton = GetDirectButtonUnderMouse(mousePos);
                if (directButton != null)
                {
                    if (audioManager != null)
                        audioManager.PlayClickSFX();
                    string target = directButton.targetPageName;
                    if (!string.IsNullOrEmpty(target))
                        SwitchToPage(target);
                    // 跳过后续
                    pressedLink = null;
                    isLongPressing = false;
                    longPressActivated = false;
                    return;
                }

                // 判断是否单击在已激活链接上（包含已访问链接）
                bool clickedOnActivatedLink = (activatedLink != null)
                                            && GetLinkUnderMouse(mousePos) == activatedLink
                                            && viewportController.IsPointInsideHole(mousePos);

                if (clickedOnActivatedLink)
                {
                    // 单击已激活链接 → 跳转
                    string target = activatedLink.targetPageName;
                    if (!string.IsNullOrEmpty(target))
                    {
                        if (audioManager != null)
                            audioManager.PlayLinkClickSFX();
                        SwitchToPage(target);
                    }
                    activatedLink.Deactivate();
                    activatedLink = null;
                    // 不触发任何冷却
                }
                else
                {
                    // 既不是有效长按，也不是单击已激活链接
                    // 只有长按错误（按了足够久但松手时不在正确位置）才触发冷却
                    if (isLongPressing)
                    {
                        TriggerCooldown();
                    }
                    else
                    {
                        // 普通短按：播放点击音效
                        if (audioManager != null)
                            audioManager.PlayClickSFX();
                    }
                    // 短按错误：什么都不做，无惩罚
                }
            }

            // 重置状态
            pressedLink = null;
            isLongPressing = false;
            longPressActivated = false;
        }
    }

    // 获取鼠标下方且在洞口内的链接
    private LinkArea GetLinkUnderMouse(Vector2 screenPoint)
    {
        // 先检查是否在洞口内
        if (!viewportController.IsPointInsideHole(screenPoint))
            return null;

        // 使用 GraphicRaycaster 射线检测（需 Canvas 上有 GraphicRaycaster）
        var raycaster = GetComponent<GraphicRaycaster>();
        if (raycaster == null) return null;

        var eventData = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
        {
            position = screenPoint
        };
        var results = new List<UnityEngine.EventSystems.RaycastResult>();
        raycaster.Raycast(eventData, results);

        foreach (var hit in results)
        {
            // 查找挂有 LinkArea 的物体或其父物体
            LinkArea link = hit.gameObject.GetComponentInParent<LinkArea>();
            if (link != null)
                return link;
        }
        return null;
    }

    /// <summary>
    /// 检测鼠标下方是否有 DirectJumpButton（且鼠标在洞口内）
    /// </summary>
    private DirectJumpButton GetDirectButtonUnderMouse(Vector2 screenPoint)
    {
        if (viewportController != null && !viewportController.IsPointInsideHole(screenPoint))
            return null;

        GraphicRaycaster raycaster = GetComponent<GraphicRaycaster>();
        if (raycaster == null) return null;

        var eventData = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
        {
            position = screenPoint
        };
        var results = new List<UnityEngine.EventSystems.RaycastResult>();
        raycaster.Raycast(eventData, results);

        foreach (var hit in results)
        {
            DirectJumpButton button = hit.gameObject.GetComponentInParent<DirectJumpButton>();
            if (button != null)
                return button;
        }
        return null;
    }

    private IEnumerator RestoreCursorAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        Cursor.SetCursor(defaultCursor, Vector2.zero, CursorMode.Auto);
    }

    // 页面切换
    public void SwitchToPage(string pageName)
    {
         // --- 保存当前页链接状态（切换前） ---
        SaveCurrentPageLinkStates();

         // --- 保存历史（跳转前），回退时不重复压栈 ---
        if (!isReturningFromBack && currentPage != null && !string.IsNullOrEmpty(currentPage.pageName))
        {
            pageHistory.Push((currentPage.pageName, activatedLink));
        }
        // 记录访问（前一页 + 当前页）
        if (currentPage != null && !visitedPages.Contains(currentPage.pageName))
            visitedPages.Add(currentPage.pageName);
        if (!visitedPages.Contains(pageName))
            visitedPages.Add(pageName);

        // 隐藏所有页面
        foreach (var p in pages)
            if (p.pageObject != null) p.pageObject.SetActive(false);

        // 显示目标页面
        foreach (var p in pages)
        {
            if (p.pageName == pageName)
            {
                p.pageObject.SetActive(true);
                currentPage = p;
                break;
            }
        }
        if (currentPage.pageObject != null)
            {
                currentPageRect = currentPage.pageObject.GetComponent<RectTransform>();
                 originalPageSize = currentPageRect.rect.size;   // 缩放为1时的本地尺寸
                // 如果是非放大模式，确保缩放和位置正常
                if (!isZoomMode)
                {
                    currentPageRect.localScale = Vector3.one;
                    currentPageRect.anchoredPosition = Vector2.zero;
                }
                else
                {
                    currentPageRect.localScale = Vector3.one * zoomScale;
                }
            }

        // 洞口揭示：如果该页标记了 enableHoleReveal，触发渐变扩大
        if (currentPage != null && currentPage.enableHoleReveal && viewportController != null)
        {
            viewportController.AnimateHoleReveal(
                fullyRevealedHoleSize,
                currentPage.revealDuration,
                currentPage.revealCurve
            );
        }

        // 最后一页：揭示后延迟进入结局
        if (currentPage != null && currentPage.isLastPage && endingPage != null)
        {
            float waitTime = currentPage.revealDuration + currentPage.endingDelay;
            if (endingCoroutine != null) StopCoroutine(endingCoroutine);
            endingCoroutine = StartCoroutine(EndingCoroutine(waitTime));
        }

        // 恢复目标页的链接状态
        RestorePageLinkStates(pageName);

        // 重置所有链接状态
        ResetAllLinks();

        // 保存进度
        SaveGame();
    }

    /// <summary>
    /// 返回上一页面（Z 键触发）
    /// </summary>
    private void GoBack()
    {
        if (pageHistory.Count == 0) return;

        var (previousPageName, previousLink) = pageHistory.Pop();

        if (previousLink != null)
        {
            previousLink.MarkVisited();
            // 将这个已访问链接设置为当前激活链接，以便可以继续单击跳转
            activatedLink = previousLink;
        }
        else
        {
            activatedLink = null;
        }

        if (audioManager != null)
            audioManager.PlayBackSFX();

        isReturningFromBack = true;
        SwitchToPage(previousPageName);
        isReturningFromBack = false;
    }

    private void ResetAllLinks()
    {
        if (currentPage == null) return;

        var links = currentPage.pageObject.GetComponentsInChildren<LinkArea>();
        foreach (var link in links)
        {
            // 只重置未访问的链接，保留已访问链接的激活状态
            if (!link.isVisited)
            {
                link.Deactivate();
            }
        }

        // 如果当前激活的链接不属于当前页面，则清空激活引用
        if (activatedLink != null && !activatedLink.transform.IsChildOf(currentPage.pageObject.transform))
        {
            activatedLink = null;
        }
    }

    /// <summary>保存当前页面所有已访问链接的目标页名</summary>
    private void SaveCurrentPageLinkStates()
    {
        if (currentPage == null || currentPage.pageObject == null) return;

        var links = currentPage.pageObject.GetComponentsInChildren<LinkArea>();
        var visitedTargets = new List<string>();
        foreach (var link in links)
        {
            if (link.isVisited && !string.IsNullOrEmpty(link.targetPageName))
                visitedTargets.Add(link.targetPageName);
        }

        if (visitedTargets.Count == 0) return;

        // 更新或新增
        var existing = currentSave.pageLinkStates.Find(s => s.pageName == currentPage.pageName);
        if (existing != null)
            existing.visitedLinkTargets = visitedTargets;
        else
            currentSave.pageLinkStates.Add(new PageLinkStates
            {
                pageName = currentPage.pageName,
                visitedLinkTargets = visitedTargets
            });
    }

    /// <summary>恢复指定页面的已访问链接状态</summary>
    private void RestorePageLinkStates(string pageName)
    {
        var state = currentSave.pageLinkStates.Find(s => s.pageName == pageName);
        if (state == null) return;

        var pageInfo = pages.FirstOrDefault(p => p.pageName == pageName);
        if (pageInfo == null || pageInfo.pageObject == null) return;

        var links = pageInfo.pageObject.GetComponentsInChildren<LinkArea>();
        foreach (var link in links)
        {
            if (state.visitedLinkTargets.Contains(link.targetPageName))
            {
                link.MarkVisited();
            }
        }
    }

    /// <summary>从历程界面跳转到指定页面</summary>
    public void JumpToPage(string pageName)
    {
        if (journeyPanel != null)
            journeyPanel.SetActive(false);

        // 不清除历史，允许继续按 Z 返回
        // 不记录当前页到历史（因为是跳转，不是正常导航）
        // 但需要保存当前页链接状态
        SaveCurrentPageLinkStates();

        // 隐藏所有页面
        foreach (var p in pages)
            if (p.pageObject != null) p.pageObject.SetActive(false);

        // 显示目标页面
        PageInfo target = null;
        foreach (var p in pages)
        {
            if (p.pageName == pageName)
            {
                p.pageObject.SetActive(true);
                currentPage = p;
                target = p;
                break;
            }
        }

        if (target == null) return;

        // 还原页面缩放等
        if (currentPage.pageObject != null)
        {
            currentPageRect = currentPage.pageObject.GetComponent<RectTransform>();
            originalPageSize = currentPageRect.rect.size;
            if (!isZoomMode)
            {
                currentPageRect.localScale = Vector3.one;
                currentPageRect.anchoredPosition = Vector2.zero;
            }
            else
            {
                currentPageRect.localScale = Vector3.one * zoomScale;
            }
        }

        // 如果目标页需要揭示且还没揭示过
        if (currentPage.enableHoleReveal && viewportController != null && !viewportController.IsRevealed)
        {
            viewportController.AnimateHoleReveal(
                fullyRevealedHoleSize,
                currentPage.revealDuration,
                currentPage.revealCurve
            );
        }

        // 恢复链接状态
        RestorePageLinkStates(pageName);
        ResetAllLinks();
        activatedLink = null;
        pageHistory.Clear(); // 历程跳转清空返回历史

        SaveGame();
    }


    // 存档系统
    public void SaveGame()
    {
        if (currentSave == null) currentSave = new SaveData();
        currentSave.currentPageName = currentPage != null ? currentPage.pageName : "";
        currentSave.visitedPages = visitedPages;
        string json = JsonUtility.ToJson(currentSave, true);
        File.WriteAllText(savePath, json);
    }

    public void LoadGame()
    {
        if (File.Exists(savePath))
        {
            string json = File.ReadAllText(savePath);
            currentSave = JsonUtility.FromJson<SaveData>(json);
            if (currentSave.visitedPages == null)
                currentSave.visitedPages = new List<string>();
            visitedPages = new List<string>(currentSave.visitedPages);
            Debug.Log("存档已加载");
        }
        else
        {
            Debug.LogWarning("没有存档文件可加载");
        }
    }

    // 记忆页面（供 UI 使用）
    public List<string> GetVisitedPages() => visitedPages;
    
        // 检查是否有存档文件
    public bool HasSaveFile()
    {
        return File.Exists(savePath);
    }

    // 新游戏
    public void NewGame()
    {
        // 强制新建存档（覆盖之前的）
        currentSave = new SaveData();
        currentSave.currentPageName = pages.Length > 0 ? pages[0].pageName : "";
        visitedPages.Clear();
        SaveGame();
        // 可以在这里重置所有链接状态等
        pageHistory.Clear();   // 清除返回历史
    }

    // 继续游戏：加载存档并切换到记录的页面
    public void ContinueGame()
    {
        if (File.Exists(savePath))
        {
            LoadGame(); // 这会设置 currentSave 和 visitedPages
            string savedPage = currentSave.currentPageName;
            if (!string.IsNullOrEmpty(savedPage))
            {
                SwitchToPage(savedPage);
            }
            else
            {
                // 存档中无页面名，显示第一个页面
                SwitchToPage(pages[0].pageName);
            }
        }
        else
        {
            Debug.LogError("没有存档文件，无法继续游戏");
        }
    }

    // 显示第一个游戏页面（用于新游戏）
    public void ShowFirstGamePage()
    {
        if (pages.Length > 0)
        {
            SwitchToPage(pages[0].pageName);
        }
    }

    // 显示遮罩视野
    public void EnableViewportMask()
    {
        if (viewportController != null)
        {
            viewportController.gameObject.SetActive(true);
        }
    }
    /// <summary>返回主菜单（从设置面板调用）</summary>
    public void ReturnToMainMenu()
    {
        // 关闭设置面板和历程面板
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (journeyPanel != null) journeyPanel.SetActive(false);

        // 隐藏所有游戏页面
        foreach (var p in pages)
            if (p.pageObject != null) p.pageObject.SetActive(false);

        // 隐藏遮罩
        DisableViewportMask();

        // 重置状态
        currentPage = null;
        activatedLink = null;
        pageHistory.Clear();

        // 显示开始页面
        if (startPage != null)
            startPage.SetActive(true);
    }

    /// <summary>结局延迟协程</summary>
    private System.Collections.IEnumerator EndingCoroutine(float delay)
    {
        yield return new WaitForSeconds(delay);

        // 隐藏所有游戏页面
        foreach (var p in pages)
            if (p.pageObject != null) p.pageObject.SetActive(false);

        // 显示结局页面
        if (endingPage != null)
            endingPage.SetActive(true);

        isEndingActive = true;
        endingCoroutine = null;
    }

    /// <summary>结局页面输入：长按退出</summary>
    private void HandleEndingInput()
    {
        bool mousePressed = mouseClickAction.ReadValue<float>() > 0.5f;

        if (mousePressed && !isMouseDown)
        {
            isMouseDown = true;
            mouseDownTime = Time.time;
        }

        if (mousePressed && isMouseDown && !isLongPressing)
        {
            if (Time.time - mouseDownTime >= longPressDuration)
            {
                isLongPressing = true;
                if (pressingAnimationCoroutine != null)
                    StopCoroutine(pressingAnimationCoroutine);
                pressingAnimationCoroutine = StartCoroutine(PressingCursorAnimation());
            }
        }

        if (!mousePressed && isMouseDown)
        {
            isMouseDown = false;
            if (pressingAnimationCoroutine != null)
            {
                StopCoroutine(pressingAnimationCoroutine);
                pressingAnimationCoroutine = null;
            }

            if (isLongPressing)
            {
                // 长按退出
                #if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
                #else
                    Application.Quit();
                #endif
            }

            isLongPressing = false;
        }
    }

    // 隐藏遮罩视野（如果需要，比如回到主菜单，暂时不用）
    public void DisableViewportMask()
    {
        if (viewportController != null)
        {
            viewportController.gameObject.SetActive(false);
        }
    }

    /// <summary>获取所有已访问页面的 PageInfo（供历程界面使用）</summary>
    public List<PageInfo> GetVisitedPageInfos()
    {
        var result = new List<PageInfo>();
        foreach (var pageName in visitedPages)
        {
            var info = System.Array.Find(pages, p => p.pageName == pageName);
            if (info != null)
                result.Add(info);
        }
        return result;
    }
}