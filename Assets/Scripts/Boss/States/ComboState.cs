using UnityEngine;

public class ComboState : BossState
{
    AnimEvent[] _events;
    int _nextEvent;

    public ComboState(BossController boss, AliveState alive) : base(boss, alive) { }

    public override void Enter()
    {
        float ratio = (float)Boss.CurrentHP / Boss.MaxHP;
        string anim = ratio >= 0.7f ? "Combo1" : ratio >= 0.4f ? "Combo2" : "Combo3";
        Play(anim);
        Boss.StopMovement();
        CombatEvents.FireAttackStarted(0);

        var eventSys = Boss.GetComponent<AnimationEventSystem>();
        _events = eventSys != null ? eventSys.GetEvents(anim) : System.Array.Empty<AnimEvent>();
        _nextEvent = 0;
    }

    public override void Update()
    {
        if (!Boss.IsActivated) return;
        Boss.FacePlayer();

        float t = AnimNormalizedTime();

        // 检测是否到达下一个事件时间点
        while (_nextEvent < _events.Length && t >= _events[_nextEvent].normalizedTime)
        {
            HandleEvent(_events[_nextEvent]);
            _nextEvent++;
        }

        if (AnimDone()) Go(new ObserveState(Boss, Alive));
    }

    public override void Exit()
    {
        Boss.DisableWeaponHitBoxes();
        Boss.ClearHitTargets();
    }

    void HandleEvent(AnimEvent evt)
    {
        switch (evt.eventName)
        {
            case "HitBoxOn":
                Boss.EnableWeaponHitBoxes();
                break;
            case "HitBoxOff":
                Boss.DisableWeaponHitBoxes();
                Boss.ClearHitTargets();
                break;
        }
    }
}
