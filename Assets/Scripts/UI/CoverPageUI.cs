using UnityEngine;
using UnityEngine.InputSystem;

public class CoverPageUI : MonoBehaviour
{
    [SerializeField] private GameObject startPage;  // 拖入开始页面物体

    private InputAction anyKeyAction;

    void Start()
    {
        // 监听任意按键或鼠标点击
        anyKeyAction = new InputAction("AnyKey", binding: "/*/<button>"); // 任意设备按键
        anyKeyAction.performed += ctx => OnAnyInput();
        anyKeyAction.Enable();
    }

    void OnAnyInput()
    {
        // 关闭封面，显示开始页面
        gameObject.SetActive(false);
        if (startPage != null)
            startPage.SetActive(true);
        
        anyKeyAction.Disable(); // 不再需要
    }

    void OnDisable()
    {
        anyKeyAction?.Disable();
    }
}