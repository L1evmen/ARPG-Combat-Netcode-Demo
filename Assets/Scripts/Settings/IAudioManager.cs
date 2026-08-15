/// <summary>
/// 音频管理器接口 —— 将音量控制与具体 AudioMixer 实现解耦。
///
/// 后续实现 AudioManager : MonoBehaviour, IAudioManager 时，
/// 挂载到场景中并通过 AudioMixer.SetFloat 控制实际音量。
///
/// 使用方式（在其他系统中）：
///   IAudioManager audio = GetComponent<IAudioManager>();
///   audio?.SetMasterVolume(0.8f);
/// </summary>
public interface IAudioManager
{
    /// <summary>设置主音量（0.0 ~ 1.0）</summary>
    void SetMasterVolume(float volume);

    /// <summary>设置背景音乐音量（0.0 ~ 1.0）</summary>
    void SetBGMVolume(float volume);

    /// <summary>设置音效音量（0.0 ~ 1.0）</summary>
    void SetSFXVolume(float volume);

    /// <summary>获取当前主音量</summary>
    float GetMasterVolume();

    /// <summary>获取当前 BGM 音量</summary>
    float GetBGMVolume();

    /// <summary>获取当前 SFX 音量</summary>
    float GetSFXVolume();

    /// <summary>播放一次性音效（位置可选）</summary>
    void PlaySFX(string sfxName, UnityEngine.Vector3? position = null);

    /// <summary>播放 / 切换背景音乐</summary>
    void PlayBGM(string bgmName, float fadeDuration = 0.5f);

    /// <summary>停止背景音乐</summary>
    void StopBGM(float fadeDuration = 0.5f);
}
