using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StartPageUI : MonoBehaviour
{
    [Header("按钮")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button continueButton;

    [Header("游戏管理器")]
    [SerializeField] private GameManager gameManager; // 拖入 Canvas 上的 GameManager
    [Header("提示页面")]
    [SerializeField] private GameObject hintPage;     // 拖入提示页面物体

    [Header("音频")]
    [SerializeField] private AudioManager audioManager;

    void Start()
    {
        // 绑定按钮事件
        startButton.onClick.AddListener(OnStartGame);
        continueButton.onClick.AddListener(OnContinueGame);

        // 如果没有存档，继续游戏按钮不可交互
        if (gameManager != null && !gameManager.HasSaveFile())
        {
            continueButton.interactable = false;
            // 可选：把文字颜色变灰
            var tmp = continueButton.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null) tmp.color = Color.gray;
        }
    }

    private void PlayClickSound()
    {
        if (audioManager != null)
            audioManager.PlayClickSFX();
    }

    void OnStartGame()
    {
        PlayClickSound();
        // 隐藏开始页，显示提示页
        HideStartPage();
        if (hintPage != null)
            hintPage.SetActive(true);
    }

    void OnContinueGame()
    {
        PlayClickSound();
        gameManager.ContinueGame();
        HideStartPage();
        gameManager.EnableViewportMask();
    }

    void HideStartPage()
    {
        gameObject.SetActive(false);
    }
}