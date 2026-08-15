using UnityEngine;

public class StaggerState : BossState
{
    private float _timer;
    private bool _animFinished;

    public StaggerState(BossController boss, AliveState alive) : base(boss, alive) { }

    public override void Enter()
    {
        Play("Stagger");
        Boss.StopMovement();
        _timer = 0f;
        _animFinished = false;
    }

    public override void Update()
    {
        if (!Boss.IsActivated) return;

        if (!_animFinished && AnimDone())
            _animFinished = true;

        if (_animFinished)
        {
            _timer += Time.deltaTime;
            if (_timer >= 0.5f)
                Go(new RecoverState(Boss, Alive));
        }
    }
}
