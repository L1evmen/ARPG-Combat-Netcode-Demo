using UnityEngine;

public class ObserveState : BossState
{
    const float DirChangeCooldown = 2f;

    float _timeout;
    float _timer;
    bool _walkingRight;
    float _lastDirChangeTime;

    public ObserveState(BossController boss, AliveState alive) : base(boss, alive) { }

    public override void Enter()
    {
        _timeout = Boss.BlockStamina > 0 ? Boss.ObserveTimeoutNormal : Boss.ObserveTimeoutDepleted;
        _timer = 0f;
        _lastDirChangeTime = -DirChangeCooldown; // 首次立即选择
        _walkingRight = IsPlayerOnRight();
        Play(_walkingRight ? "WalkRight" : "WalkLeft");
    }

    public override void Update()
    {
        if (Boss.DistanceToPlayer > Boss.MaxAttackRange)
            Boss.MoveTowardPlayer(Boss.WalkSpeed);
        else
            Boss.StopMovement();
        Boss.FacePlayer();

        if (Time.time - _lastDirChangeTime >= DirChangeCooldown)
        {
            bool onRight = IsPlayerOnRight();
            if (onRight != _walkingRight)
            {
                _walkingRight = onRight;
                _lastDirChangeTime = Time.time;
                Play(_walkingRight ? "WalkRight" : "WalkLeft");
            }
        }

        _timer += Time.deltaTime;
        if (_timer >= _timeout)
        {
            Go(Boss.DistanceToPlayer > Boss.MaxAttackRange
                ? new ChaseState(Boss, Alive)
                : new ComboState(Boss, Alive));
        }
    }

    public override void Exit() => Boss.StopMovement();

    bool IsPlayerOnRight()
    {
        Vector3 toPlayer = (Boss.PlayerPosition - Boss.transform.position).normalized;
        return Vector3.Dot(Boss.transform.right, toPlayer) >= 0f;
    }
}
