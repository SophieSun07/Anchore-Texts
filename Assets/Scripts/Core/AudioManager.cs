using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 音频管理器：控制 BGM 和音效的播放、停止与音量
/// </summary>
public class AudioManager : MonoBehaviour
{
    [Header("BGM")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioClip defaultBGM;
    [Range(0f, 1f)]
    [SerializeField] private float bgmVolume = 0.5f;

    [Header("音效")]
    [SerializeField] private AudioSource sfxSource;          // 主音效源（用于长音效）
    [Range(0f, 1f)]
    [SerializeField] private float sfxVolume = 0.7f;

    [Header("音效资源")]
    [SerializeField] private AudioClip clickClip;            // 通用点击音效
    [SerializeField] private AudioClip pageSwitchClip;       // 页面切换音效
    [SerializeField] private AudioClip longPressActivateClip; // 长按激活音效
    [SerializeField] private AudioClip linkClickClip;        // 链接点击跳转音效
    [SerializeField] private AudioClip cooldownClip;         // 错误冷却音效
    [SerializeField] private AudioClip backClip;             // 返回上一页音效

    // 可扩展的 AudioSource 池，用于同时播放多个短音效
    private Queue<AudioSource> sfxPool = new Queue<AudioSource>();
    private const int POOL_SIZE = 5;

    private static AudioManager _instance;
    public static AudioManager Instance => _instance;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        // 如果未手动指定 AudioSource，自动创建
        if (bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.playOnAwake = false;
        }

        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.loop = false;
            sfxSource.playOnAwake = false;
        }

        // 初始化音效池
        for (int i = 0; i < POOL_SIZE; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.loop = false;
            source.playOnAwake = false;
            sfxPool.Enqueue(source);
        }

        ApplyVolumes();
    }

    void Start()
    {
        // 自动播放默认 BGM（可在 GameManager 中改为手动触发）
        if (defaultBGM != null && !bgmSource.isPlaying)
        {
            PlayBGM(defaultBGM);
        }
    }

    // ==================== BGM ====================

    /// <summary>播放 BGM（循环）</summary>
    public void PlayBGM(AudioClip clip)
    {
        if (clip == null || bgmSource == null) return;
        if (bgmSource.clip == clip && bgmSource.isPlaying) return;

        bgmSource.clip = clip;
        bgmSource.volume = bgmVolume;
        bgmSource.Play();
    }

    /// <summary>停止 BGM</summary>
    public void StopBGM()
    {
        if (bgmSource != null)
            bgmSource.Stop();
    }

    /// <summary>暂停 BGM</summary>
    public void PauseBGM()
    {
        if (bgmSource != null)
            bgmSource.Pause();
    }

    /// <summary>恢复 BGM</summary>
    public void ResumeBGM()
    {
        if (bgmSource != null && bgmSource.clip != null)
            bgmSource.UnPause();
    }

    /// <summary>设置 BGM 音量</summary>
    public void SetBGMVolume(float volume)
    {
        bgmVolume = Mathf.Clamp01(volume);
        if (bgmSource != null)
            bgmSource.volume = bgmVolume;
    }

    public float GetBGMVolume() => bgmVolume;

    // ==================== SFX ====================

    /// <summary>播放音效（使用池化，支持多音效同时播放）</summary>
    public void PlaySFX(AudioClip clip)
    {
        if (clip == null) return;

        AudioSource freeSource = null;
        int poolCount = sfxPool.Count;
        for (int i = 0; i < poolCount; i++)
        {
            AudioSource src = sfxPool.Dequeue();
            sfxPool.Enqueue(src);
            if (!src.isPlaying)
            {
                freeSource = src;
                break;
            }
        }

        // 如果池中全部繁忙，取一个最早的覆盖播放
        if (freeSource == null)
        {
            freeSource = sfxPool.Dequeue();
            sfxPool.Enqueue(freeSource);
        }

        freeSource.volume = sfxVolume;
        freeSource.PlayOneShot(clip);
    }

    /// <summary>设置音效音量</summary>
    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        if (sfxSource != null)
            sfxSource.volume = sfxVolume;
    }

    public float GetSFXVolume() => sfxVolume;

    // ==================== 便捷方法 ====================

    public void PlayClickSFX() => PlaySFX(clickClip);
    public void PlayPageSwitchSFX() => PlaySFX(pageSwitchClip);
    public void PlayLongPressActivateSFX() => PlaySFX(longPressActivateClip);
    public void PlayLinkClickSFX() => PlaySFX(linkClickClip);
    public void PlayCooldownSFX() => PlaySFX(cooldownClip);
    public void PlayBackSFX() => PlaySFX(backClip);

    // ==================== 内部 ====================

    private void ApplyVolumes()
    {
        if (bgmSource != null) bgmSource.volume = bgmVolume;
        if (sfxSource != null) sfxSource.volume = sfxVolume;
    }
}
