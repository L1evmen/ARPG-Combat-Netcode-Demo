public class DormantState : BossState
{
    public DormantState(BossController boss, AliveState alive) : base(boss, alive) { }

    public override void Enter() => Play("Idle");
}
