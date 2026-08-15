using CombatV2;
using UnityEngine;

/// <summary>
/// 战斗音效播放器 —— 挂载到玩家或 Boss 上，拖入音效 key 即可。
/// 通过 CombatEvents 自动响应攻击和命中事件，音量由 AudioMixer 控制。
///
/// 当 Animation Event 通过 SFX:key 控制音效时，OnSwing 自动跳过（避免重复播放）。
/// </summary>
public class CombatAudio : MonoBehaviour
{
    [Header("SFX Key（对应 SFXLibrary 中的 key）")]
    [SerializeField] private string _swingKey = "combat_swing";
    [SerializeField] private string _hitKey = "combat_hit";

    private ComboManager _comboManager;

    void Awake()
    {
        _comboManager = GetComponent<ComboManager>();
    }

    void OnEnable()
    {
        CombatEvents.OnAttackStarted += OnSwing;
        CombatEvents.OnAttackHit += OnHit;
    }

    void OnDisable()
    {
        CombatEvents.OnAttackStarted -= OnSwing;
        CombatEvents.OnAttackHit -= OnHit;
    }

    void OnSwing(int comboIndex)
    {
        // Animation Event 已接管音效控制，跳过
        if (_comboManager != null && _comboManager.GetContext().SfxControlledByAnimEvent)
            return;

        if (!string.IsNullOrEmpty(_swingKey))
            AudioManager.Instance?.PlaySFX(_swingKey, transform.position);
    }

    void OnHit(Transform target, int damage)
    {
        if (!string.IsNullOrEmpty(_hitKey))
            AudioManager.Instance?.PlaySFX(_hitKey, target != null ? target.position : transform.position);
    }
}
