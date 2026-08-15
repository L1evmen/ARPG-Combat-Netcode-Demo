public class HitStunState : BossState
{
    public HitStunState(BossController boss, AliveState alive) : base(boss, alive) { }

    public override void Enter()
    {
        Play("HitStun");
        Boss.StopMovement();
    }

    public override void Update()
    {
        if (!Boss.IsActivated) return;
        if (AnimDone()) Go(new ObserveState(Boss, Alive));
    }
}
