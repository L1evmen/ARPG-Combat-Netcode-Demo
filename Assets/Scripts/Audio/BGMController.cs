using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// BGM 控制器 —— 双 AudioSource 交叉淡入淡出，根据战斗状态动态切换音乐。
/// </summary>
public class BGMController : MonoBehaviour
{
    [Header("AudioMixer BGM 组")]
    [SerializeField] private AudioMixerGroup _bgmMixerGroup;

    [Header("BGM 曲目")]
    [SerializeField] private AudioClip _bgmExplore;
    [SerializeField] private AudioClip _bgmCombat;
    [SerializeField] private AudioClip _bgmBoss;
    [SerializeField] private AudioClip _bgmVictory;
    [SerializeField] private AudioClip _bgmDefeat;

    [Header("淡入淡出时长")]
    [SerializeField] private float _combatFadeDuration = 1.5f;
    [SerializeField] private float _bossFadeDuration = 2.0f;
    [SerializeField] private float _exploreFadeDuration = 2.0f;
    [SerializeField] private float _victoryFadeDuration = 1.0f;
    [SerializeField] private float _defeatFadeDuration = 1.0f;

    [Header("战斗状态去抖")]
    [SerializeField] private float _debounceTime = 0.3f;

    public enum BGMState { Explore, Combat, Boss, Victory, Defeat }
    public BGMState CurrentState { get; private set; } = BGMState.Explore;

    private AudioSource _sourceA;
    private AudioSource _sourceB;
    private bool _usingSourceA = true;
    private Coroutine _fadeRoutine;
    private Coroutine _debounceRoutine;
    private BGMState _pendingState;
    private Coroutine _waitForEndRoutine;
    private float _lastStateChangeRequest;

    // ==================== 初始化 ====================

    public void Init()
    {
        _sourceA = CreateSource("BGM_Source_A");
        _sourceB = CreateSource("BGM_Source_B");
        _sourceA.volume = 1f;
        _sourceB.volume = 0f;

        if (_bgmExplore != null)
            PlayBGM(_bgmExplore, 0f);
    }

    private AudioSource CreateSource(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform);
        go.hideFlags = HideFlags.HideInHierarchy;

        var src = go.AddComponent<AudioSource>();
        src.outputAudioMixerGroup = _bgmMixerGroup;
        src.loop = true;
        src.playOnAwake = false;
        return src;
    }

    // ==================== 公开接口 ====================

    public void RequestState(BGMState newState)
    {
        if (newState != CurrentState && _waitForEndRoutine != null)
        {
            StopCoroutine(_waitForEndRoutine);
            _waitForEndRoutine = null;
        }

        _pendingState = newState;
        _lastStateChangeRequest = Time.unscaledTime;

        if (_debounceRoutine != null)
            StopCoroutine(_debounceRoutine);
        _debounceRoutine = StartCoroutine(DebouncedApply());
    }

    public void PlayBGM(string clipName, float fadeDuration = 1f)
    {
        AudioClip clip = GetClipByName(clipName);
        if (clip != null) PlayBGM(clip, fadeDuration);
    }

    public void StopBGM(float fadeDuration = 1f)
    {
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeOut(fadeDuration));
    }

    // ==================== 内部 ====================

    private void PlayBGM(AudioClip clip, float fadeDuration, bool loop = true)
    {
        AudioSource from, to;
        if (_usingSourceA)
        {
            from = _sourceA;
            to = _sourceB;
        }
        else
        {
            from = _sourceB;
            to = _sourceA;
        }

        to.clip = clip;
        to.loop = loop;
        to.Play();

        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(CrossFade(from, to, fadeDuration));
        _usingSourceA = !_usingSourceA;
    }

    private IEnumerator CrossFade(AudioSource from, AudioSource to, float duration)
    {
        float startFromVol = from.volume;
        float startToVol = to.volume;

        if (duration <= 0f)
        {
            from.volume = 0f;
            to.volume = 1f;
            from.Stop();
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            from.volume = Mathf.Lerp(startFromVol, 0f, t);
            to.volume = Mathf.Lerp(startToVol, 1f, t);
            yield return null;
        }

        from.volume = 0f;
        to.volume = 1f;
        from.Stop();
    }

    private IEnumerator FadeOut(float duration)
    {
        AudioSource from = _usingSourceA ? _sourceA : _sourceB;
        float startVol = from.volume;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            from.volume = Mathf.Lerp(startVol, 0f, elapsed / duration);
            yield return null;
        }

        from.Stop();
    }

    private IEnumerator DebouncedApply()
    {
        yield return new WaitForSecondsRealtime(_debounceTime);

        if (_pendingState == CurrentState)
            yield break;

        float fadeDuration = _pendingState switch
        {
            BGMState.Boss => _bossFadeDuration,
            BGMState.Combat => _combatFadeDuration,
            BGMState.Victory => _victoryFadeDuration,
            BGMState.Defeat => _defeatFadeDuration,
            _ => _exploreFadeDuration,
        };

        bool isOneShot = _pendingState == BGMState.Victory || _pendingState == BGMState.Defeat;

        AudioClip clip = _pendingState switch
        {
            BGMState.Boss => _bgmBoss ?? _bgmCombat,
            BGMState.Combat => _bgmCombat,
            BGMState.Victory => _bgmVictory,
            BGMState.Defeat => _bgmDefeat,
            _ => _bgmExplore,
        };

        if (clip != null)
        {
            PlayBGM(clip, fadeDuration, loop: !isOneShot);

            // Victory/Defeat 播放完毕后自动切回 Explore
            if (isOneShot)
            {
                if (_waitForEndRoutine != null) StopCoroutine(_waitForEndRoutine);
                _waitForEndRoutine = StartCoroutine(WaitForClipEndAndSwitch(clip.length));
            }
        }

        CurrentState = _pendingState;
    }

    private IEnumerator WaitForClipEndAndSwitch(float clipLength)
    {
        yield return new WaitForSecondsRealtime(clipLength + 0.5f);
        _waitForEndRoutine = null;
        RequestState(BGMState.Explore);
    }

    private AudioClip GetClipByName(string name)
    {
        return name.ToLowerInvariant() switch
        {
            "explore" => _bgmExplore,
            "combat" => _bgmCombat,
            "boss" => _bgmBoss,
            "victory" => _bgmVictory,
            "defeat" => _bgmDefeat,
            _ => null,
        };
    }

    private void OnDestroy()
    {
        if (_sourceA != null) Destroy(_sourceA.gameObject);
        if (_sourceB != null) Destroy(_sourceB.gameObject);
    }
}
