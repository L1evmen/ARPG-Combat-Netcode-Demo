using UnityEngine;

public class DeathState : BossState
{
    public DeathState(BossController boss, AliveState alive) : base(boss, alive) { }

    public override void Enter()
    {
        Play("Die");
        Boss.StopMovement();
    }

    public override void Update()
    {
        if (AnimDone()) Object.Destroy(Boss.gameObject);
    }
}
