using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using GameHUD;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(NavMeshAgent))]
public class BossController : MonoBehaviour, IDamageable, ILockable
{
    [Header("── 基本 ──")]
    [SerializeField] string _bossName = "Boss";
    [SerializeField] int _maxHP = 500;
    [SerializeField] int _expReward = 100;

    [Header("── 锁定 ──")]
    [SerializeField] float _lockPointHeight = 1.8f;

    [Header("── 移动 ──")]
    [SerializeField] float _walkSpeed = 3.5f;
    [SerializeField] float _runSpeed = 7f;
    [SerializeField] float _maxAttackRange = 3f;
    [SerializeField] float _idealDistance = 2.5f;

    [Header("── 攻击 ──")]
    [SerializeField] int _attackDamage = 30;
    [Tooltip("招式间动画过渡时间（秒）")]
    [SerializeField] float _animTransitionTime = 0.15f;

    [Header("── 战斗区域 ──")]
    [SerializeField] Collider _arenaBoundary;
    [SerializeField] BossHealthBar _bossHealthBar;

    [Header("── 观察超时 ──")]
    [SerializeField] float _observeTimeoutNormal = 3f;
    [SerializeField] float _observeTimeoutDepleted = 0.5f;

    [Header("── 格挡 ──")]
    [SerializeField] int _maxBlockStamina = 80;
    [SerializeField] float _staminaRegenPerSecond = 10f;
    [SerializeField] int _lightStaminaCost = 10;
    [SerializeField] int _heavyStaminaCost = 30;
    [SerializeField] float _blockQuietTimeout = 3f;

    [Header("── 轻重击阈值 ──")]
    [SerializeField] int _heavyAttackThreshold = 30;

    [Header("── 阶段切换 ──")]
    [SerializeField] float _phase2HPPercent = 0.7f;
    [SerializeField] float _phase3HPPercent = 0.4f;

    [Header("── 事件 ──")]
    public UnityEvent OnBattleStart;
    public UnityEvent OnBossDefeated;

    // ==================== 组件 ====================

    public Animator Animator { get; private set; }
    public NavMeshAgent NavAgent { get; private set; }

    // ==================== 运行时状态 ====================

    public int CurrentHP { get; set; }
    public int BlockStamina { get; private set; }
    public bool IsDead { get; private set; }
    public bool IsActivated { get; private set; }
    public bool PlayerInBoundary { get; private set; }
    private bool _phase2Triggered;
    private bool _phase3Triggered;

    // ==================== 配置访问器 ====================

    public string BossName => _bossName;
    public int MaxHP => _maxHP;
    public float WalkSpeed => _walkSpeed;
    public float RunSpeed => _runSpeed;
    public float MaxAttackRange => _maxAttackRange;
    public float ObserveTimeoutNormal => _observeTimeoutNormal;
    public float ObserveTimeoutDepleted => _observeTimeoutDepleted;
    public float BlockQuietTimeout => _blockQuietTimeout;
    public float AnimTransitionTime => _animTransitionTime;
    public bool HitboxesActive { get; private set; }

    // ==================== 玩家引用 ====================

    Transform _player;
    public Vector3 PlayerPosition => _player != null ? _player.position : Vector3.zero;
    public float DistanceToPlayer => _player != null
        ? Vector3.Distance(transform.position, _player.position)
        : float.MaxValue;

    // ==================== HFSM ====================

    BossHFSM _hfsm;
    AliveState _aliveState;
    CompositeState _rootState;

    // ==================== 内部 ====================

    Vector3 _spawnPosition;
    HitBox[] _hitBoxes;

    // ==================== 初始化 ====================

    void Awake()
    {
        Animator = GetComponent<Animator>();
        NavAgent = GetComponent<NavMeshAgent>();

        _spawnPosition = transform.position;
        CurrentHP = _maxHP;
        BlockStamina = _maxBlockStamina;

        if (NavAgent != null)
        {
            NavAgent.speed = _walkSpeed;
            NavAgent.stoppingDistance = _idealDistance;
            NavAgent.autoBraking = true;
            NavAgent.updateRotation = false;
        }

        var capsule = GetComponentInChildren<CapsuleCollider>();
        if (capsule != null)
        {
            capsule.isTrigger = false;
            capsule.radius = 0.5f;
            capsule.height = 2f;
            capsule.center = new Vector3(0, 1f, 0);
        }

        if (!CompareTag("Enemy"))
            tag = "Enemy";

        _hitBoxes = GetComponentsInChildren<HitBox>(includeInactive: true);

        _rootState = new RootState();
        _hfsm = new BossHFSM(_rootState);
        _aliveState = new AliveState(this, _hfsm);
        _rootState.Change(new DormantState(this, _aliveState));
        _rootState.Enter();
    }

    void Start()
    {
        _player = GameObject.FindGameObjectWithTag("Player")?.transform;
    }

    // ==================== 主循环 ====================

    void Update()
    {
        UpdatePlayerRef();
        CheckBoundary();
        RegenerateStamina();
        UpdateAnimatorSpeed();

        if (!IsActivated)
        {
            ReturnToSpawn();
            return;
        }

        _hfsm.Update();
    }

    void LateUpdate()
    {
        ClampPositionToBoundary();
    }

    void UpdateAnimatorSpeed()
    {
        if (NavAgent == null || !NavAgent.enabled || NavAgent.velocity.magnitude < 0.1f)
            Animator.SetFloat("Speed", 0f);
        else
            Animator.SetFloat("Speed", NavAgent.velocity.magnitude / _walkSpeed);
    }

    // ==================== 边界检测 ====================

    void CheckBoundary()
    {
        if (_arenaBoundary == null || _player == null) return;

        bool inBounds = _arenaBoundary.bounds.Contains(_player.position);

        if (inBounds && !PlayerInBoundary)
        {
            PlayerInBoundary = true;
            if (!IsActivated) Activate();
        }
        else if (!inBounds && PlayerInBoundary)
        {
            PlayerInBoundary = false;
            if (IsActivated) Deactivate();
        }
    }

    // ==================== 激活 / 休眠 ====================

    public void Activate()
    {
        if (IsDead) return;
        IsActivated = true;
        _phase2Triggered = false;
        _phase3Triggered = false;

        if (NavAgent != null) NavAgent.enabled = true;
        if (_bossHealthBar != null) _bossHealthBar.Show(_bossName);

        _rootState.Change(_aliveState);
        OnBattleStart?.Invoke();
    }

    public void Deactivate()
    {
        IsActivated = false;

        if (NavAgent != null)
        {
            NavAgent.ResetPath();
            NavAgent.velocity = Vector3.zero;
        }

        CurrentHP = _maxHP;
        IsDead = false;
        BlockStamina = _maxBlockStamina;
        _phase2Triggered = false;
        _phase3Triggered = false;

        if (_bossHealthBar != null) _bossHealthBar.Hide();

        Animator.SetFloat("Speed", 0f);
        Animator.SetBool("IsBlocking", false);

        _rootState.Change(new DormantState(this, _aliveState));
    }

    void ReturnToSpawn()
    {
        if (NavAgent == null || !NavAgent.enabled) return;

        float dist = Vector3.Distance(transform.position, _spawnPosition);
        if (dist < 0.5f)
        {
            NavAgent.ResetPath();
            NavAgent.velocity = Vector3.zero;
            return;
        }

        NavAgent.SetDestination(_spawnPosition);
        NavAgent.speed = _walkSpeed;
    }

    // ==================== 耐力 ====================

    void RegenerateStamina()
    {
        if (_hfsm == null) return;
        var leaf = _hfsm.CurrentLeaf;
        if (leaf is BlockState) return;
        if (BlockStamina >= _maxBlockStamina) return;

        BlockStamina = Mathf.Min(_maxBlockStamina,
            Mathf.RoundToInt(BlockStamina + _staminaRegenPerSecond * Time.deltaTime));
        UpdateHPUI();
    }

    /// <summary>格挡耐力回满（阶段切换时调用）</summary>
    public void RefillBlockStamina()
    {
        BlockStamina = _maxBlockStamina;
        UpdateHPUI();
    }

    // ==================== IDamageable ====================

    public bool CanBeHit() => !IsDead;

    // ==================== ILockable ====================

    public Vector3 LockPoint => transform.position + Vector3.up * _lockPointHeight;
    public bool CanBeLocked => !IsDead;

    public void TakeDamage(int damage, Transform source)
    {
        if (IsDead) return;
        if (!IsActivated) return;

        bool isHeavy = damage >= _heavyAttackThreshold;
        var leaf = _hfsm.CurrentLeaf;

        // --- Dormant / Death / Retreat: 不可打断，忽略 ---
        if (leaf is DormantState || leaf is DeathState || leaf is RetreatState)
            return;

        // --- HitStun / Stagger / Recover: 惩罚状态，不可格挡，直接扣血 ---
        if (leaf is HitStunState || leaf is StaggerState || leaf is RecoverState)
        {
            CurrentHP -= damage;
            CheckPhaseTransition();
            if (CurrentHP <= 0) Die();
            UpdateHPUI();
            return;
        }

        // --- 所有活跃战斗状态：有耐力就格挡，无耐力就扣血+硬直 ---
        if (BlockStamina > 0)
        {
            int cost = isHeavy ? _heavyStaminaCost : _lightStaminaCost;
            BlockStamina -= cost;

            if (leaf is BlockState block)
            {
                block.ResetQuietTimer();
            }
            else
            {
                _aliveState.Change(new BlockState(this, _aliveState));
            }

            if (BlockStamina <= 0)
            {
                BlockStamina = 0;
                _aliveState.Change(new StaggerState(this, _aliveState));
            }
            UpdateHPUI();
            return;
        }

        // --- 耐力耗尽：直接扣血 + 硬直 / 重硬直 ---
        CurrentHP -= damage;
        CheckPhaseTransition();
        _aliveState.Change(isHeavy ? new StaggerState(this, _aliveState) : new HitStunState(this, _aliveState));
        if (CurrentHP <= 0) Die();
        UpdateHPUI();
    }

    void CheckPhaseTransition()
    {
        float hpPercent = (float)CurrentHP / _maxHP;

        if (!_phase2Triggered && hpPercent <= _phase2HPPercent)
        {
            _phase2Triggered = true;
            _aliveState.Change(new RetreatState(this, _aliveState));
        }
        else if (!_phase3Triggered && hpPercent <= _phase3HPPercent)
        {
            _phase3Triggered = true;
            _aliveState.Change(new RetreatState(this, _aliveState));
        }
    }

    void UpdateHPUI()
    {
        if (_bossHealthBar != null)
        {
            _bossHealthBar.UpdateHP((float)CurrentHP / _maxHP);
            _bossHealthBar.UpdateBlockStamina((float)BlockStamina / _maxBlockStamina);
        }
    }

    // ==================== 死亡 ====================

    void Die()
    {
        IsDead = true;
        IsActivated = false;
        CurrentHP = 0;

        if (NavAgent != null) NavAgent.enabled = false;

        if (_bossHealthBar != null) _bossHealthBar.Hide();
        OnBossDefeated?.Invoke();

        _hfsm.Change(new DeathState(this, _aliveState));
    }

    // ==================== 兼容接口（BossArena 使用） ====================

    public void ResetToFull()
    {
        CurrentHP = _maxHP;
        IsDead = false;
        BlockStamina = _maxBlockStamina;
        if (_bossHealthBar != null) _bossHealthBar.UpdateHP(1f);
    }

    public void SetActivated(bool active)
    {
        if (active) Activate(); else Deactivate();
    }

    public void ApplyReplicaState(int health, int blockStamina, bool active, bool dead)
    {
        CurrentHP = Mathf.Clamp(health, 0, _maxHP);
        BlockStamina = Mathf.Clamp(blockStamina, 0, _maxBlockStamina);
        IsActivated = active;
        IsDead = dead;

        if (_bossHealthBar != null)
        {
            if (active && !dead) _bossHealthBar.Show(_bossName);
            else _bossHealthBar.Hide();
        }
        UpdateHPUI();
    }

    // ==================== 移动辅助 ====================

    public void FacePlayer()
    {
        if (_player == null) return;
        Vector3 dir = (PlayerPosition - transform.position).normalized;
        dir.y = 0f;
        if (dir.magnitude > 0.01f)
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, Quaternion.LookRotation(dir), 360f * Time.deltaTime);
    }

    public void MoveTowardPlayer(float speed)
    {
        if (NavAgent == null || !NavAgent.enabled || _player == null) return;
        NavAgent.SetDestination(ClampPointInBoundary(PlayerPosition));
        NavAgent.speed = speed;
    }

    public void EnableWeaponHitBoxes()
    {
        if (_hitBoxes == null) return;
        foreach (var hb in _hitBoxes) hb.Enable(_attackDamage);
        HitboxesActive = true;
    }

    public void DisableWeaponHitBoxes()
    {
        if (_hitBoxes == null) return;
        foreach (var hb in _hitBoxes) hb.Disable();
        HitboxesActive = false;
    }

    public void ClearHitTargets()
    {
        if (_hitBoxes == null) return;
        foreach (var hb in _hitBoxes) hb.ClearHitTargets();
    }

    public void StopMovement()
    {
        if (NavAgent != null && NavAgent.enabled)
        {
            NavAgent.ResetPath();
            NavAgent.velocity = Vector3.zero;
        }
    }

    // ==================== 边界钳制 ====================

    Vector3 ClampPointInBoundary(Vector3 point)
    {
        if (_arenaBoundary == null) return point;
        if (_arenaBoundary.bounds.Contains(point)) return point;

        Vector3 clamped = _arenaBoundary.ClosestPoint(point);
        if (NavMesh.SamplePosition(clamped, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            return hit.position;
        return clamped;
    }

    void ClampPositionToBoundary()
    {
        if (_arenaBoundary == null) return;
        if (_arenaBoundary.bounds.Contains(transform.position)) return;

        Vector3 clamped = _arenaBoundary.ClosestPoint(transform.position);
        if (NavMesh.SamplePosition(clamped, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            transform.position = hit.position;
        else
            transform.position = clamped;

        if (NavAgent != null && NavAgent.enabled)
        {
            NavAgent.velocity = Vector3.zero;
            NavAgent.ResetPath();
        }
    }

    // ==================== 玩家引用 ====================

    void UpdatePlayerRef()
    {
        if (CombatTargetRegistry.TryGetClosest(transform.position, out Transform target))
        {
            _player = target;
            return;
        }

        if (_player == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) _player = go.transform;
        }
    }
}

sealed class RootState : CompositeState { }
