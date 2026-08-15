using UnityEngine;

/// <summary>
/// Boss 阶段切换后撤状态 —— 不可打断，播完动画后回到观察状态。
/// </summary>
public class RetreatState : BossState
{
    public RetreatState(BossController boss, AliveState alive) : base(boss, alive) { }

    public override void Enter()
    {
        Play("Retreat");
        Boss.StopMovement();
        Boss.RefillBlockStamina();
    }

    public override void Update()
    {
        if (!Boss.IsActivated) return;
        if (AnimDone()) Go(new ObserveState(Boss, Alive));
    }
}
