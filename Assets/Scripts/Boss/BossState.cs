using UnityEngine;

public abstract class BossState : IState, IEventReceiver
{
    protected readonly BossController Boss;
    protected readonly AliveState Alive;
    string _animName;

    protected BossState(BossController boss, AliveState alive)
    {
        Boss  = boss;
        Alive = alive;
    }

    public virtual void Enter() { }
    public virtual void Update() { }
    public virtual void Exit()  { }
    public virtual void OnEvent(object evt) { }

    protected void Go(BossState state) => Alive.Change(state);

    protected void Play(string name)
    {
        _animName = name;
        Boss.Animator.CrossFadeInFixedTime(name, Boss.AnimTransitionTime);
    }

    protected float AnimNormalizedTime()
    {
        var info = Boss.Animator.GetCurrentAnimatorStateInfo(0);
        return info.IsName(_animName) ? info.normalizedTime % 1f : 0f;
    }

    protected bool AnimDone()
    {
        var info = Boss.Animator.GetCurrentAnimatorStateInfo(0);
        return info.IsName(_animName) && info.normalizedTime >= 1f;
    }
}
