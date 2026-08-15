using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// AudioSource 对象池 —— 预创建 N 个 Source，按 key 播放 SFX。
/// 所有 Source 输出到 AudioMixer 的 SFX 总线。
/// </summary>
public class SFXPool : MonoBehaviour
{
    [Header("池大小")]
    [SerializeField] private int _poolSize = 16;

    [Header("AudioMixer SFX 组")]
    [SerializeField] private AudioMixerGroup _sfxMixerGroup;

    private AudioSource[] _sources;
    private int _nextIndex;

    /// <summary>关联的 SFXLibrary</summary>
    public SFXLibrary Library { get; set; }

    public void Init()
    {
        _sources = new AudioSource[_poolSize];
        for (int i = 0; i < _poolSize; i++)
        {
            var go = new GameObject($"SFX_Source_{i}");
            go.transform.SetParent(transform);
            go.hideFlags = HideFlags.HideInHierarchy;

            _sources[i] = go.AddComponent<AudioSource>();
            _sources[i].outputAudioMixerGroup = _sfxMixerGroup;
            _sources[i].playOnAwake = false;
        }
    }

    /// <summary>按 key 播放音效（使用库中默认音量）</summary>
    public void PlaySFX(string key, Vector3? position = null)
    {
        if (Library == null) return;
        var clip = Library.FindClip(key);
        if (clip == null) return;

        float volume = Library.FindVolume(key);
        PlaySFX(clip, volume, position);
    }

    /// <summary>直接播放 AudioClip</summary>
    public void PlaySFX(AudioClip clip, float volume = 1f, Vector3? position = null)
    {
        if (clip == null || _sources == null) return;

        var src = GetNextSource();
        if (position.HasValue)
        {
            src.transform.position = position.Value;
            src.spatialBlend = 1f;
        }
        else
        {
            src.spatialBlend = 0f;
        }
        src.PlayOneShot(clip, volume);
    }

    /// <summary>轮转获取下一个 Source（PlayOneShot 不会互斥，故简单轮转即可</summary>
    private AudioSource GetNextSource()
    {
        var src = _sources[_nextIndex];
        _nextIndex = (_nextIndex + 1) % _poolSize;
        return src;
    }

    /// <summary>停止所有 SFX</summary>
    public void StopAll()
    {
        if (_sources == null) return;
        foreach (var src in _sources)
        {
            if (src != null)
                src.Stop();
        }
    }
}
