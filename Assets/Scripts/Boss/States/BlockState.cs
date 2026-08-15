using UnityEngine;

public class BlockState : BossState
{
    float _quietTimer;

    public BlockState(BossController boss, AliveState alive) : base(boss, alive) { }

    public override void Enter()
    {
        _quietTimer = Boss.BlockQuietTimeout;
        Boss.Animator.SetBool("IsBlocking", true);
        Play("Block");
        Boss.StopMovement();
    }

    public override void Update()
    {
        if (!Boss.IsActivated) return;
        Boss.FacePlayer();
        _quietTimer -= Time.deltaTime;

        if (_quietTimer <= 0f)
            Go(new ComboState(Boss, Alive));
    }

    public void ResetQuietTimer() => _quietTimer = Boss.BlockQuietTimeout;

    public override void Exit()
    {
        Boss.Animator.SetBool("IsBlocking", false);
    }
}
