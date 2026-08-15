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

    private void Start()
    {
        if (_bgmController == null)
            _bgmController = GetComponent<BGMController>();
    }

    private void Update()
    {
        if (_bossController == null) return;

        // Boss 激活 → Boss BGM
        if (!_wasActivated && _bossController.IsActivated)
        {
            _wasActivated = true;
            _bgmController?.RequestState(BGMController.BGMState.Boss);
        }

        // Boss 死亡 → Victory BGM
        if (_wasActivated && !_wasDead && _bossController.IsDead)
        {
            _wasDead = true;
            _bgmController?.RequestState(BGMController.BGMState.Victory);
        }
    }

    /// <summary>外部调用：播放战败 BGM</summary>
    public void PlayDefeatBGM()
    {
        _bgmController?.RequestState(BGMController.BGMState.Defeat);
    }
}
