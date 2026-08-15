using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 敌人AI控制器 —— 基于有限状态机的敌人行为系统。
///
/// 状态流转：
///   Patrol(巡逻) → Chase(追逐) → Attack(攻击) → Patrol/Chase
///   Any State  → Hited(受击) → Idle(强制2秒) → Patrol/Chase
///   Any State  → Death(死亡) → 销毁
///
/// 攻击判定使用 Animation Event 驱动 Hitbox 开关，而非协程计时。
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class EnemyController : MonoBehaviour, IDamageable, ILockable
{
    // ================================================================
    // 状态枚举
    // ================================================================
    private enum State
    {
        Patrol,   // 巡逻：idle / walk 交替
        Chase,    // 追逐玩家
        Attack,   // 攻击（播放攻击动画，由 Animation Event 开启判定窗口）
        Hited,    // 受击硬直
        Death     // 死亡（不可逆）
    }

    // ================================================================
    // Animator 参数 Hash（避免每帧字符串查找）
    // ================================================================
    private static readonly int ParamSpeed       = Animator.StringToHash("Speed");
    private static readonly int ParamIsAttack    = Animator.StringToHash("IsAttack");
    private static readonly int ParamIsHit       = Animator.StringToHash("IsHit");
    private static readonly int ParamIsDeath     = Animator.StringToHash("IsDeath");
    private static readonly int ParamDeathId     = Animator.StringToHash("DeathId");

    // ================================================================
    // 公开属性（在 Inspector 中配置）
    // ================================================================
    [Header("基础属性")]
    public int maxHP = 100;
    public int currentHP;
    public int atkValue = 15;
    public int exp = 20;

    [Header("检测范围")]
    public float aggroRange = 8f;       // 索敌范围
    public float attackRange = 2f;      // 攻击距离
    public float deaggroRange = 12f;    // 脱离仇恨范围（通常是索敌范围的1.5倍）

    [Header("攻击配置")]
    public float attackCooldown = 1.5f; // 攻击冷却时间

    [Header("巡逻配置")]
    public float patrolRadius = 10f;     // 巡逻半径（以初始位置为中心）
    public float idleTimeMin = 1f;       // 最短 idle 时间
    public float idleTimeMax = 3f;       // 最长 idle 时间
    public float walkTimeMin = 1.5f;     // 最短 walk 时间
    public float walkTimeMax = 4f;       // 最长 walk 时间
    public float walkSpeedMin = 0.5f;    // 巡逻最慢速度
    public float walkSpeedMax = 1.2f;    // 巡逻最快速度

    [Header("攻击判定窗口（normalizedTime：0=动画起点, 1=动画终点）")]
    public float hitboxEnableTime  = 0.3f;  // 命中帧（例如动画 30% 处开启窗口）
    public float hitboxDisableTime = 0.55f; // 收招帧（例如动画 55% 处关闭窗口）

    [Header("锁定配置")]
    public float lockPointHeight = 1.5f; // 锁定点高度（胸口位置）

    [Header("攻击碰撞盒（仅用于 Gizmo 预览，实际由子物体 Hitbox Trigger 决定）")]
    public Vector3 hitboxCenter = new Vector3(0, 1f, 1f);
    public Vector3 hitboxSize   = new Vector3(1.5f, 1.5f, 1.5f);

    // ================================================================
    // 内部引用
    // ================================================================
    private NavMeshAgent _agent;
    private Animator _anim;
    private Transform _player;
    private Vector3 _spawnPosition;  // 初始位置（巡逻中心点）
    private Rigidbody _rb;

    [Header("坠落检测")]
    [SerializeField] private float _groundCheckDist = 0.3f;
    [SerializeField] private LayerMask _groundLayer = ~0;
    private bool _isFalling;

    // ================================================================
    // 状态机变量
    // ================================================================
    private State _currentState = State.Patrol;
    private float _attackTimer;       // 攻击冷却倒计时
    private bool _isDead;
    private float _lastHitTime;       // 最近一次受击时间

    // ================================================================
    // 攻击判定窗口变量
    // ================================================================
    private bool _hitboxActive;       // 当前是否处于攻击判定窗口
    private Collider _hitboxCollider; // 攻击碰撞体引用（旧系统兼容）
    private HitBox[] _hitBoxes;       // HitBox 组件数组（新系统）
    private AnimationEventSystem _animEventSys;
    private AnimEvent[] _currentEvents;
    private int _nextEvent;

    // ================================================================
    // 巡逻变量
    // ================================================================
    private float _patrolTimer;       // 当前巡逻状态计时器
    private float _patrolDuration;    // 当前巡逻状态持续时长
    private bool _isPatrolIdle;       // 当前是否处于巡逻 idle（true=idle, false=walk）

    // ================================================================
    // Unity 生命周期
    // ================================================================
    void Start()
    {
        _agent = GetComponent<NavMeshAgent>();
        _anim  = GetComponent<Animator>();
        _rb = GetComponent<Rigidbody>();
        _player = GameObject.FindGameObjectWithTag("Player")?.transform;
        _spawnPosition = transform.position;
        currentHP = maxHP;

        // 不使用 Root Motion，由 NavMeshAgent 驱动位移
        _anim.applyRootMotion = false;
        _agent.updatePosition = true;
        _agent.updateRotation = false;

        // 确保有 Rigidbody（NavMeshAgent 需要）
        if (GetComponent<Rigidbody>() == null)
        {
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
        }

        // 查找子物体上的攻击 Hitbox（旧系统兼容）
        _hitboxCollider = transform.Find("AttackHitbox")?.GetComponent<Collider>();
        if (_hitboxCollider != null)
            _hitboxCollider.enabled = false;

        // 查找所有 HitBox 组件（新系统）
        _hitBoxes = GetComponentsInChildren<HitBox>(includeInactive: true);
        if (_hitBoxes != null)
            foreach (var hb in _hitBoxes)
                hb.Disable();

        // AnimationEventSystem（新系统优先）
        _animEventSys = GetComponent<AnimationEventSystem>();

        // 运行时动态绑定 Animation Event（解决 FBX 内嵌动画只读问题）
        // 如果 hitboxEnableTime > 0，说明需要使用代码方式绑定判定窗口
        if (hitboxEnableTime > 0f)
            SetupAttackAnimationEvents();

        // 进入初始巡逻状态
        EnterPatrolState();
    }

    void Update()
    {
        if (_isDead) return;
        if (_player == null) return;

        // 坠落检测：脚下无地面时切换为物理下落
        bool grounded = Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, _groundCheckDist + 0.1f, _groundLayer);

        if (_isFalling)
        {
            if (grounded && _rb.velocity.y <= 0.1f)
            {
                // 落地：恢复 NavMeshAgent
                _isFalling = false;
                _rb.isKinematic = true;
                _agent.enabled = true;
                _agent.Warp(transform.position);
                _anim.applyRootMotion = true;
                EnterPatrolState();
            }
            return; // 坠落中不执行 AI 逻辑
        }

        if (!grounded)
        {
            // 开始坠落：禁用 NavMeshAgent，启用物理
            _isFalling = true;
            _agent.enabled = false;
            _rb.isKinematic = false;
            _anim.applyRootMotion = false;
            return;
        }

        // 更新计时器
        _attackTimer -= Time.deltaTime;

        float distToPlayer = Vector3.Distance(transform.position, _player.position);

        // 分派状态更新
        switch (_currentState)
        {
            case State.Patrol: UpdatePatrol(distToPlayer); break;
            case State.Chase:  UpdateChase(distToPlayer);  break;
            case State.Attack: UpdateAttack(distToPlayer); break;
            case State.Hited:  UpdateHited();              break;
            case State.Death:  break;
        }

        // 更新 Animator 速度参数
        UpdateAnimSpeed();
    }

    // 不使用 Root Motion，NavMeshAgent 直接控制位移，无需 OnAnimatorMove

    // ================================================================
    // 状态：Patrol（巡逻）
    // —— 在 idle 和 walk 之间交替，在 spawn 点附近移动
    // ================================================================
    void EnterPatrolState()
    {
        _currentState = State.Patrol;
        _agent.isStopped = true;
        _agent.speed = 0;
        StartNewPatrolCycle();
    }

    void UpdatePatrol(float distToPlayer)
    {
        // 玩家进入索敌范围 → 切换追逐
        if (distToPlayer <= aggroRange)
        {
            ChangeState(State.Chase);
            return;
        }

        // 巡逻计时
        _patrolTimer -= Time.deltaTime;
        if (_patrolTimer <= 0f)
        {
            StartNewPatrolCycle();
        }

        if (_isPatrolIdle)
        {
            // Idle：停止移动
            _agent.isStopped = true;
        }
        else
        {
            // Walk：向随机巡逻点移动
            if (_agent.isStopped)
                _agent.isStopped = false;

            // 面向移动方向
            if (_agent.velocity.magnitude > 0.1f)
            {
                FaceTarget(transform.position + _agent.velocity.normalized);
            }

            // 到达目标点附近时提前切换到 idle
            if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.1f)
            {
                StartNewIdleCycle();
            }
        }
    }

    /// <summary>开始新的巡逻周期（随机选择 idle 或 walk）</summary>
    void StartNewPatrolCycle()
    {
        if (Random.value < 0.4f)
        {
            StartNewIdleCycle();
        }
        else
        {
            StartNewWalkCycle();
        }
    }

    void StartNewIdleCycle()
    {
        _isPatrolIdle = true;
        _patrolDuration = Random.Range(idleTimeMin, idleTimeMax);
        _patrolTimer = _patrolDuration;
        _agent.isStopped = true;
    }

    void StartNewWalkCycle()
    {
        _isPatrolIdle = false;
        _patrolDuration = Random.Range(walkTimeMin, walkTimeMax);
        _patrolTimer = _patrolDuration;
        _agent.speed = Random.Range(walkSpeedMin, walkSpeedMax);
        _agent.isStopped = false;
        _agent.SetDestination(GetRandomPatrolPoint());
    }

    /// <summary>在巡逻半径内随机取一个 NavMesh 上的点</summary>
    Vector3 GetRandomPatrolPoint()
    {
        Vector3 randomDir = Random.insideUnitSphere * patrolRadius;
        randomDir.y = 0;
        Vector3 target = _spawnPosition + randomDir;

        if (NavMesh.SamplePosition(target, out NavMeshHit hit, patrolRadius, NavMesh.AllAreas))
            return hit.position;

        return _spawnPosition;
    }

    // ================================================================
    // 状态：Chase（追逐玩家）
    // ================================================================
    void UpdateChase(float distToPlayer)
    {
        // 超出脱战范围 → 返回巡逻
        if (distToPlayer > deaggroRange)
        {
            ChangeState(State.Patrol);
            return;
        }

        // 进入攻击距离且冷却就绪，且受击后超过 1 秒 → 攻击
        if (distToPlayer <= attackRange && _attackTimer <= 0f && Time.time - _lastHitTime >= 1f)
        {
            ChangeState(State.Attack);
            return;
        }

        // 持续追击
        if (_agent.isActiveAndEnabled && !_agent.isStopped)
        {
            _agent.SetDestination(_player.position);
        }

        FaceTarget(_player.position);
    }

    // ================================================================
    // 状态：Attack（攻击）
    // —— 播放攻击动画，由 Animation Event 调用 EnableHitbox/DisableHitbox
    // ================================================================
    private float _attackLockTimer;

    void UpdateAttack(float distToPlayer)
    {
        // 攻击过程中面朝玩家，清零速度防止推挤
        FaceTarget(_player.position);
        _agent.velocity = Vector3.zero;

        // 2.1秒后恢复 Agent 位置同步
        _attackLockTimer -= Time.deltaTime;
        if (_attackLockTimer <= 0f)
        {
            _agent.updatePosition = true;
            _agent.updateRotation = false; // 保持手动控制旋转
        }

        // AnimationEventSystem 驱动 HitBox 开关
        if (_currentEvents != null && _currentEvents.Length > 0)
        {
            float t = _anim.GetCurrentAnimatorStateInfo(0).normalizedTime % 1f;
            while (_nextEvent < _currentEvents.Length && t >= _currentEvents[_nextEvent].normalizedTime)
            {
                HandleAnimEvent(_currentEvents[_nextEvent]);
                _nextEvent++;
            }
        }

        // 检测攻击动画是否已播放完毕（normalizedTime >= 1 表示播放完毕）
        var stateInfo = _anim.GetCurrentAnimatorStateInfo(0);
        if (stateInfo.IsName("Attack") && stateInfo.normalizedTime >= 0.95f)
        {
            DisableHitbox();
            _attackTimer = attackCooldown;
            _currentEvents = null;

            if (distToPlayer <= aggroRange)
                ChangeState(State.Chase);
            else
                ChangeState(State.Patrol);
        }
    }

    void HandleAnimEvent(AnimEvent evt)
    {
        switch (evt.eventName)
        {
            case "HitBoxOn":  EnableHitbox(); break;
            case "HitBoxOff": DisableHitbox(); break;
        }
    }

    // ================================================================
    // 运行时动态绑定 Animation Event（FBX 只读动画的替代方案）
    // ================================================================
    /// <summary>
    /// 在运行时为 Attack 动画动态添加 EnableHitbox / DisableHitbox 事件。
    /// 无需 Animation Event 手动配置，适合 FBX 内嵌只读动画。
    /// </summary>
    void SetupAttackAnimationEvents()
    {
        if (_anim == null || _anim.runtimeAnimatorController == null) return;

        foreach (var clip in _anim.runtimeAnimatorController.animationClips)
        {
            // 按名称匹配 Attack 动画（支持 "Attack", "attack", "Attack01" 等变体）
            if (!clip.name.ToLower().Contains("attack")) continue;

            // 检查是否已手动添加过事件，避免重复
            bool hasEnable = false, hasDisable = false;
            foreach (var evt in clip.events)
            {
                if (evt.functionName == "EnableHitbox")  hasEnable  = true;
                if (evt.functionName == "DisableHitbox") hasDisable = true;
            }

            if (!hasEnable)
            {
                var evt = new AnimationEvent();
                evt.functionName = "EnableHitbox";
                evt.time = clip.length * hitboxEnableTime;
                clip.AddEvent(evt);
                Debug.Log($"[EnemyController] 运行时添加 EnableHitbox 到 {clip.name} (time={evt.time:F2}s)");
            }

            if (!hasDisable)
            {
                var evt = new AnimationEvent();
                evt.functionName = "DisableHitbox";
                evt.time = clip.length * hitboxDisableTime;
                clip.AddEvent(evt);
                Debug.Log($"[EnemyController] 运行时添加 DisableHitbox 到 {clip.name} (time={evt.time:F2}s)");
            }

            break; // 只处理第一个匹配的 Attack 动画
        }
    }

    // ================================================================
    // 攻击判定窗口（由 Animation Event 调用）
    // ================================================================
    /// <summary>
    /// 开启攻击判定窗口。
    /// 在 Attack 动画中通过 Animation Event 调用此方法。
    /// 建议在动画中设置两个事件：一个在挥砍命中帧调用 EnableHitbox，
    /// 一个在收招帧调用 DisableHitbox。
    /// </summary>
    public void EnableHitbox()
    {
        _hitboxActive = true;
        if (_hitboxCollider != null)
            _hitboxCollider.enabled = true;
        if (_hitBoxes != null)
            foreach (var hb in _hitBoxes)
                hb.Enable(atkValue);
    }

    /// <summary>
    /// 关闭攻击判定窗口。
    /// 在 Attack 动画中通过 Animation Event 调用此方法。
    /// </summary>
    public void DisableHitbox()
    {
        _hitboxActive = false;
        if (_hitboxCollider != null)
            _hitboxCollider.enabled = false;
        if (_hitBoxes != null)
            foreach (var hb in _hitBoxes)
            {
                hb.Disable();
                hb.ClearHitTargets();
            }
    }

    /// <summary>
    /// 当玩家进入攻击 Hitbox 触发区域时调用。
    /// 挂载在 AttackHitbox 子物体的 Collider(IsTrigger=true) 上。
    /// </summary>
    public void OnAttackHitboxTrigger(Collider other)
    {
        if (!_hitboxActive || _isDead) return;

        if (other.CompareTag("Player"))
        {
            // 通过 IDamageable 接口造成伤害（低耦合：不依赖具体 Player 类）
            var damageable = other.GetComponent<IDamageable>();
            if (damageable != null && damageable.CanBeHit())
            {
                damageable.TakeDamage(atkValue, transform);
            }

            // 命中后立即关闭窗口，防止一帧多次判定
            DisableHitbox();
        }
    }

    // ================================================================
    // 状态：Hited（受击硬直）
    // —— 通过 Animator 的 Any State → Hited 过渡
    // ================================================================
    void UpdateHited()
    {
        // 等待 Hited 动画播放完毕
        var stateInfo = _anim.GetCurrentAnimatorStateInfo(0);
        if (stateInfo.IsName("Hited") && stateInfo.normalizedTime >= 0.95f)
        {
            float distToPlayer = Vector3.Distance(transform.position, _player.position);
            if (distToPlayer <= aggroRange)
                ChangeState(State.Chase);
            else
                ChangeState(State.Patrol);
        }
    }

    // ================================================================
    // 状态切换
    // ================================================================
    void ChangeState(State newState)
    {
        if (_isDead) return;

        Debug.Log($"[EnemyController] {_currentState} → {newState}");
        _currentState = newState;

        // 确保攻击窗口关闭
        if (_hitboxActive) DisableHitbox();

        switch (newState)
        {
            case State.Patrol:
                _agent.isStopped = true;
                _agent.speed = 0;
                _agent.updatePosition = true;
                _agent.updateRotation = false; // 统一手动控制旋转
                _anim.SetFloat(ParamSpeed, 0f);
                StartNewPatrolCycle();
                break;

            case State.Chase:
                _agent.isStopped = false;
                _agent.speed = 3.5f; // 追逐速度比巡逻快
                _agent.updatePosition = true;
                _agent.updateRotation = false; // 统一手动控制旋转
                _anim.SetFloat(ParamSpeed, 1f);
                break;

            case State.Attack:
                _agent.isStopped = true;
                _agent.speed = 0;
                _agent.velocity = Vector3.zero;
                _agent.updatePosition = false; // 完全禁止 Agent 控制位置
                _agent.updateRotation = false;
                _attackLockTimer = 2.1f;
                _anim.SetFloat(ParamSpeed, 0f);
                _anim.SetTrigger(ParamIsAttack);
                // 从 AnimationEventSystem 读取事件
                _currentEvents = _animEventSys != null ? _animEventSys.GetEvents("Attack") : null;
                _nextEvent = 0;
                break;

            case State.Hited:
                _agent.isStopped = true;
                _agent.speed = 0;
                _agent.updateRotation = false; // 统一手动控制旋转
                _anim.SetFloat(ParamSpeed, 0f);
                _anim.SetTrigger(ParamIsHit);
                break;

            case State.Death:
                _agent.isStopped = true;
                _agent.speed = 0;
                _agent.updateRotation = false; // 统一手动控制旋转
                _anim.SetFloat(ParamSpeed, 0f);
                break;
        }
    }

    // ================================================================
    // IDamageable 接口实现
    // ================================================================
    /// <summary>受到伤害（IDamageable 接口）。</summary>
    public void TakeDamage(int damage, Transform source)
    {
        if (_isDead) return;

        currentHP -= damage;
        Debug.Log($"[EnemyController] 受到 {damage} 点伤害，剩余 HP={currentHP}");

        if (currentHP <= 0)
        {
            Die();
            return;
        }

        // 进入受击状态（Any State → Hited 过渡由 Animator 处理）
        _lastHitTime = Time.time;
        ChangeState(State.Hited);
    }

    /// <summary>是否可被命中。</summary>
    public bool CanBeHit()
    {
        return !_isDead && enabled && gameObject.activeInHierarchy;
    }

    // ================================================================
    // ILockable 接口实现（锁定系统通过此接口发现可锁定目标）
    // ================================================================
    /// <summary>锁定点的世界坐标（胸口位置）。</summary>
    public Vector3 LockPoint => transform.position + Vector3.up * lockPointHeight;

    /// <summary>是否仍可被锁定（死亡后返回 false）。</summary>
    public bool CanBeLocked => !_isDead;

    // 向下兼容：无 source 参数的伤害入口
    public void TakeDamage(int damage)
    {
        TakeDamage(damage, null);
    }

    // ================================================================
    // 死亡
    // ================================================================
    void Die()
    {
        Debug.Log("[EnemyController] Die() 被调用");
        if (_isDead) return;
        _isDead = true;

        // 关闭碰撞（不再受击），但保持 isKinematic 防止穿模
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
        var rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        // 随机选择死亡动画（1~3）
        int deathId = Random.Range(1, 4);
        Debug.Log($"[EnemyController] 死亡 → Death{deathId}");

        // 直接设置状态，跳过 ChangeState 的 _isDead 守卫
        _currentState = State.Death;
        _agent.isStopped = true;
        _agent.speed = 0;
        _anim.SetFloat(ParamSpeed, 0f);
        _anim.SetInteger(ParamDeathId, deathId);
        _anim.ResetTrigger(ParamIsAttack);
        _anim.ResetTrigger(ParamIsHit);
        _anim.Play($"Death{deathId}", 0, 0f);

        // 通知事件中心（任务系统等依赖此事件）
        EventCenter.EnemyDied(exp);

        // 死亡动画开始时立即掉落
        if (ItemDBManager.Instance != null)
            ItemDBManager.Instance.DropItems(transform.position, 3);

        // 延迟销毁（给死亡动画留出播放时间）
        float destroyDelay = 3f;
        StartCoroutine(DestroyAfterDelay(destroyDelay));
    }

    IEnumerator DestroyAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        Destroy(gameObject);
    }

    // ================================================================
    // 辅助方法
    // ================================================================
    /// <summary>面朝目标位置（仅绕 Y 轴旋转）。</summary>
    void FaceTarget(Vector3 targetPos)
    {
        Vector3 dir = (targetPos - transform.position).normalized;
        dir.y = 0;
        if (dir.magnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, targetRot, 360f * Time.deltaTime);
        }
    }

    /// <summary>根据当前状态更新 Animator 的 Speed 参数。</summary>
    void UpdateAnimSpeed()
    {
        if (_anim == null) return;

        float speed = 0f;
        switch (_currentState)
        {
            case State.Chase:
                speed = 1f;
                break;
            case State.Patrol:
                speed = _isPatrolIdle ? 0f : 0.6f;
                break;
            // Attack / Hited / Death 保持 0
        }
        _anim.SetFloat(ParamSpeed, speed);
    }

    // ================================================================
    // 编辑器可视化
    // ================================================================
#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        // 索敌范围（黄色）
        Gizmos.color = new Color(1f, 1f, 0, 0.2f);
        Gizmos.DrawWireSphere(transform.position, aggroRange);

        // 攻击范围（红色）
        Gizmos.color = new Color(1f, 0, 0, 0.3f);
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // 攻击 Hitbox 预览
        Gizmos.color = new Color(1f, 0, 0, 0.5f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(hitboxCenter, hitboxSize);
    }
#endif
}
