public class AliveState : CompositeState
{
    readonly BossController _boss;
    readonly BossHFSM _hfsm;

    public AliveState(BossController boss, BossHFSM hfsm)
    {
        _boss = boss;
        _hfsm = hfsm;
    }

    public override void Enter() => Change(new ObserveState(_boss, this));

    public override void Update()
    {
        if (_boss.CurrentHP <= 0) { _hfsm.Change(new DeathState(_boss, this)); return; }
        base.Update();
    }
}
