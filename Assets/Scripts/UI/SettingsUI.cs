using UnityEngine;
using UnityEngine.UI;

public class SettingsUI : MonoBehaviour
{
    [Header("音频")]
    [SerializeField] private AudioManager audioManager;

    [Header("UI 控件")]
    public Slider bgmVolumeSlider;
    public Slider sfxVolumeSlider;
    public Button closeButton;
    public Button returnButton;
    public Button quitButton;

    private GameManager gameManager;

    void Start()
    {
        gameManager = FindAnyObjectByType<GameManager>();
        if (gameManager == null)
        {
            Debug.LogError("SettingsUI 找不到 GameManager！");
            return;
        }

        if (audioManager != null)
        {
            bgmVolumeSlider.value = audioManager.GetBGMVolume();
            sfxVolumeSlider.value = audioManager.GetSFXVolume();
        }

        bgmVolumeSlider.onValueChanged.AddListener(OnBGMVolumeChanged);
        sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
        closeButton.onClick.AddListener(ClosePanel);
        returnButton.onClick.AddListener(OnReturnToMenu);
        quitButton.onClick.AddListener(OnQuit);
    }

    public void OnBGMVolumeChanged(float value)
    {
        if (audioManager != null)
            audioManager.SetBGMVolume(value);
    }

    public void OnSFXVolumeChanged(float value)
    {
        if (audioManager != null)
            audioManager.SetSFXVolume(value);
    }

    public void ClosePanel()
    {
        gameObject.SetActive(false);
    }

    public void OnReturnToMenu()
    {
        if (gameManager != null)
            gameManager.ReturnToMainMenu();
    }

    public void OnQuit()
    {
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
}