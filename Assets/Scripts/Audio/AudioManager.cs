using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// 音频管理器 —— 实现 IAudioManager，桥接 SettingsManager ↔ AudioMixer。
/// DontDestroyOnLoad 单例。
/// </summary>
public class AudioManager : MonoBehaviour, IAudioManager
{
    // ==================== 单例 ====================

    public static AudioManager Instance { get; private set; }

    // ==================== Inspector ====================

    [Header("AudioMixer")]
    [SerializeField] private AudioMixer _mixer;

    [Header("组件引用")]
    [SerializeField] private SFXPool _sfxPool;
    [SerializeField] private BGMController _bgmController;

    [Header("音效库")]
    [SerializeField] private SFXLibrary _sfxLibrary;

    [Header("AudioMixer 参数名")]
    [SerializeField] private string _masterVolumeParam = "MasterVolume";
    [SerializeField] private string _bgmVolumeParam = "BGMVolume";
    [SerializeField] private string _sfxVolumeParam = "SFXVolume";

    // ==================== 运行时数据 ====================

    private float _currentMasterVolume = 1f;
    private float _currentBGMVolume = 1f;
    private float _currentSFXVolume = 1f;

    // ==================== 生命周期 ====================

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (_mixer == null)
            Debug.LogWarning("[AudioManager] AudioMixer 未赋值，音量控制将无效");

        // 初始化 SFXPool
        if (_sfxPool != null)
        {
            _sfxPool.Library = _sfxLibrary;
            _sfxPool.Init();
        }

        // 初始化 BGMController
        if (_bgmController != null)
            _bgmController.Init();
    }

    private IEnumerator Start()
    {
        // 等待 SettingsManager 初始化完毕再订阅（解决 Awake 执行顺序问题）
        yield return null;

        var sm = SettingsManager.Instance;
        if (sm != null && _mixer != null)
        {
            _currentMasterVolume = sm.MasterVolume;
            _currentBGMVolume = sm.BGMVolume;
            _currentSFXVolume = sm.SFXVolume;

            ApplyMasterVolume(_currentMasterVolume);
            ApplyBGMVolume(_currentBGMVolume);
            ApplySFXVolume(_currentSFXVolume);

            Debug.Log($"[AudioManager] 已同步音量: Master={_currentMasterVolume}, BGM={_currentBGMVolume}, SFX={_currentSFXVolume}");

            // 订阅后续变更
            sm.OnMasterVolumeChanged += OnMasterVolumeChanged;
            sm.OnBGMVolumeChanged += OnBGMVolumeChanged;
            sm.OnSFXVolumeChanged += OnSFXVolumeChanged;
        }
        else
        {
            if (sm == null) Debug.LogWarning("[AudioManager] SettingsManager.Instance 为 null，音量同步失败");
            if (_mixer == null) Debug.LogWarning("[AudioManager] AudioMixer 未赋值");
        }
    }

    private void OnDestroy()
    {
        var sm = SettingsManager.Instance;
        if (sm != null)
        {
            sm.OnMasterVolumeChanged -= OnMasterVolumeChanged;
            sm.OnBGMVolumeChanged -= OnBGMVolumeChanged;
            sm.OnSFXVolumeChanged -= OnSFXVolumeChanged;
        }
    }

    // ==================== SettingsManager 事件 ====================

    private void OnMasterVolumeChanged(float volume)
    {
        _currentMasterVolume = volume;
        ApplyMasterVolume(volume);
    }

    private void OnBGMVolumeChanged(float volume)
    {
        _currentBGMVolume = volume;
        ApplyBGMVolume(volume);
    }

    private void OnSFXVolumeChanged(float volume)
    {
        _currentSFXVolume = volume;
        ApplySFXVolume(volume);
    }

    private void ApplyMasterVolume(float volume) => SetMixerFloat(_masterVolumeParam, volume);
    private void ApplyBGMVolume(float volume) => SetMixerFloat(_bgmVolumeParam, volume);
    private void ApplySFXVolume(float volume) => SetMixerFloat(_sfxVolumeParam, volume);

    private void SetMixerFloat(string param, float linearValue)
    {
        if (_mixer == null) return;
        float db = LinearToDb(linearValue);
        _mixer.SetFloat(param, db);
    }

    // ==================== dB 转换 ====================

    /// <summary>线性 0~1 → 对数 dB (-80 ~ 0)</summary>
    public static float LinearToDb(float linear)
    {
        return linear > 0.0001f ? Mathf.Log10(linear) * 20f : -80f;
    }

    // ==================== IAudioManager 实现 ====================

    public void SetMasterVolume(float volume) => OnMasterVolumeChanged(volume);
    public void SetBGMVolume(float volume) => OnBGMVolumeChanged(volume);
    public void SetSFXVolume(float volume) => OnSFXVolumeChanged(volume);

    public float GetMasterVolume() => _currentMasterVolume;
    public float GetBGMVolume() => _currentBGMVolume;
    public float GetSFXVolume() => _currentSFXVolume;

    public void PlaySFX(string sfxName, Vector3? position = null)
    {
        _sfxPool?.PlaySFX(sfxName, position);
    }

    public void PlayBGM(string bgmName, float fadeDuration = 0.5f)
    {
        _bgmController?.PlayBGM(bgmName, fadeDuration);
    }

    public void StopBGM(float fadeDuration = 0.5f)
    {
        _bgmController?.StopBGM(fadeDuration);
    }

    // ==================== BGM 状态控制 ====================

    public void SetBGMState(BGMController.BGMState state)
    {
        _bgmController?.RequestState(state);
    }

    public BGMController.BGMState GetBGMState()
    {
        return _bgmController != null ? _bgmController.CurrentState : BGMController.BGMState.Explore;
    }

    // ==================== SFX 便捷方法 ====================

    /// <summary>设置 SFXLibrary（运行时注入）</summary>
    public void SetSFXLibrary(SFXLibrary library)
    {
        if (_sfxPool != null)
            _sfxPool.Library = library;
    }
}
