using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 提示页面：显示操作提示，按任意键后进入游戏关卡
/// </summary>
public class HintPageUI : MonoBehaviour
{
    [Header("游戏管理器")]
    [SerializeField] private GameManager gameManager;
    [Header("音频")]
    [SerializeField] private AudioManager audioManager;

    private InputAction anyKeyAction;

    void OnEnable()
    {
        anyKeyAction = new InputAction("AnyKey", binding: "/*/<button>");
        anyKeyAction.performed += ctx => OnAnyInput();
        anyKeyAction.Enable();
    }

    void OnAnyInput()
    {
        if (audioManager != null)
            audioManager.PlayClickSFX();

        gameObject.SetActive(false);

        if (gameManager != null)
        {
            gameManager.NewGame();
            gameManager.ShowFirstGamePage();
            gameManager.EnableViewportMask();
        }

        anyKeyAction.Disable();
    }

    void OnDisable()
    {
        anyKeyAction?.Disable();
    }
}
