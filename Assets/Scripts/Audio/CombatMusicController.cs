using UnityEngine;

/// <summary>
/// 战斗音乐控制器 —— Boss 激活时播放 Boss BGM，击败后播放 Victory BGM。
/// 挂载到 AudioManager GameObject 上。
/// </summary>
public class CombatMusicController : MonoBehaviour
{
    [Header("BGM 控制器")]
    [SerializeField] private BGMController _bgmController;

    [Header("Boss")]
    [SerializeField] private BossController _bossController;

    private bool _wasActivated;
    private bool _wasDead;
    private bool _partyDefeated;

    private void Start()
    {
        if (_bgmController == null)
            _bgmController = GetComponent<BGMController>();
    }

    private void Update()
    {
        if (_bossController == null) return;

        // Boss 激活 → Boss BGM
        if (!_partyDefeated && !_wasActivated && _bossController.IsActivated)
        {
            _bgmController?.RequestState(BGMController.BGMState.Boss);
        }

        // Boss 死亡 → Victory BGM
        if (!_wasDead && _bossController.IsDead)
        {
            _bgmController?.RequestState(BGMController.BGMState.Victory);
        }

        _wasActivated = _bossController.IsActivated;
        _wasDead = _bossController.IsDead;
    }

    public void SetPartyDefeated(bool defeated)
    {
        if (_partyDefeated == defeated)
            return;

        _partyDefeated = defeated;
        if (defeated)
        {
            PlayDefeatBGM();
            return;
        }

        BGMController.BGMState nextState = _bossController != null
            && _bossController.IsActivated
            && !_bossController.IsDead
                ? BGMController.BGMState.Boss
                : BGMController.BGMState.Explore;
        _bgmController?.RequestState(nextState);
    }

    /// <summary>外部调用：播放战败 BGM</summary>
    public void PlayDefeatBGM()
    {
        _bgmController?.RequestState(BGMController.BGMState.Defeat);
    }
}
