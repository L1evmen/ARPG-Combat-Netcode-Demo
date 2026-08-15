public class RecoverState : BossState
{
    public RecoverState(BossController boss, AliveState alive) : base(boss, alive) { }

    public override void Enter() => Play("Recover");

    public override void Update()
    {
        if (!Boss.IsActivated) return;
        if (AnimDone()) Go(new ObserveState(Boss, Alive));
    }
}
