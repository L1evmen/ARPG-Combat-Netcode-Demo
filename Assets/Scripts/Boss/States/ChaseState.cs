using UnityEngine;

/// <summary>
/// 追击状态 —— Boss 决定攻击但距离玩家太远时，播放 Run 动画快速接近。
/// 进入攻击范围后自动转入 ComboState。
/// </summary>
public class ChaseState : BossState
{
    public ChaseState(BossController boss, AliveState alive) : base(boss, alive) { }

    public override void Enter()
    {
        Play("Run");
    }

    public override void Update()
    {
        Boss.FacePlayer();

        if (Boss.DistanceToPlayer <= Boss.MaxAttackRange)
        {
            Boss.StopMovement();
            Go(new ComboState(Boss, Alive));
            return;
        }

        Boss.MoveTowardPlayer(Boss.RunSpeed);
    }

    public override void Exit()
    {
        Boss.StopMovement();
    }
}
