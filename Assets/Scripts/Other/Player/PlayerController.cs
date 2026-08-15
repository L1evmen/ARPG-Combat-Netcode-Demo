using UnityEngine;
using CombatV2.LockOn;

[RequireComponent(typeof(TargetLockManager))]
public class PlayerController : MonoBehaviour
{
    // ==================== 组件引用 ====================
    private CharacterController characterController;
    private Animator animator;
    private InputController inputController;

    /// <summary>由招式系统设置：true 时保留动画根运动 Y 轴位移（升龙斩等）</summary>
    public bool UseRootMotionY;

    // ==================== 第3节：地面检测 ====================
    public bool isGrounded;

    // ==================== 第3节：重力 ====================
    public float gravity = -9.8f;

    // ==================== 第4节：跳跃 ====================
    public float jumpHeight = 1.5f;
    private float VerticalVelocity;

    [Header("跌落设置")]
    public float fallHeight = 0.5f; // 跌落的最小高度,小于此高度不会触发跌落动画
    // ==================== 第6节：速度缓存 ====================
    private static readonly int CACHE_SIZE = 3;
    private Vector3[] velCache = new Vector3[CACHE_SIZE];
    private int currentCacheIndex;

    private Vector3 averageVel;

    // ==================== 移动 ====================
    [Header("速度设置")]
    public float walkSpeed = 5f;
    public float runSpeed = 5f;
    public float acceleration = 5f;
    public float deceleration = 10f;
    private float currentSpeed;

    private float lastJumpTime;
    private float jumpCooldown = 0.1f;


    [Header("空中设置")]
    public float airControl = 5f; // 空中按WASD时的加速度
    public float maxAirSpeed = 5f; // 空中最大水平速度限制，防止无限加速飞走

    // 缓存一下Update里计算出的移动方向，供OnAnimatorMove使用
    private Vector3 currentMoveDir;

    // ==================== 锁定系统 ====================
    [Header("锁定系统")]
    [SerializeField] private TargetLockManager _lockManager;

    [Tooltip("面向锁定目标的旋转速度")]
    [SerializeField] private float _lockRotationSpeed = 12f;

    /// <summary>当前帧的本地移动输入 X（-1=左, 1=右），供 Animator Blend Tree 使用</summary>
    public float InputX { get; private set; }

    /// <summary>当前帧的本地移动输入 Y（-1=后, 1=前），供 Animator Blend Tree 使用</summary>
    public float InputY { get; private set; }

    // Animator 参数 Hash
    private static readonly int ParamInputX = Animator.StringToHash("inputX");
    private static readonly int ParamInputY = Animator.StringToHash("inputY");
    private static readonly int DancingHash = Animator.StringToHash("Dancing");


    private void Start()
    {
        characterController = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        animator.applyRootMotion = true;
        inputController = GetComponent<InputController>();

        if (_lockManager == null)
            _lockManager = GetComponent<TargetLockManager>();
    }

    void Update()
    {
        // ==================== 第3节：地面检测 ====================
        isGrounded = characterController.isGrounded;

        // ==================== 移动输入 ====================
        Vector2 move = inputController.MovementBlocked ? Vector2.zero : inputController.moveVector2;
        bool runHeld = inputController.isSprint;
        bool hasInput = move.magnitude > 0.1f;

        Camera cam = Camera.main;
        Vector3 forward = cam.transform.forward;
        Vector3 right = cam.transform.right;
        forward.y = 0f; right.y = 0f;
        forward.Normalize(); right.Normalize();
        Vector3 moveDir = (forward * move.y + right * move.x).normalized;
        currentMoveDir = moveDir;

        bool isLocked = _lockManager != null && _lockManager.IsLocked;

        // 战斗动作期间冻结朝向，避免闪避/攻击中 FaceLockedTarget Slerp 导致轨迹偏斜
        bool canRotate = !inputController.MovementBlocked;

        if (hasInput)
        {
            float targetSpeed = runHeld ? runSpeed : walkSpeed;
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * Time.deltaTime);

            if (isLocked && canRotate)
            {
                FaceLockedTarget();
            }
            else if (!isLocked && canRotate)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(moveDir), 15f * Time.deltaTime);
            }
        }
        else
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, deceleration * Time.deltaTime);

            if (isLocked && isGrounded && canRotate)
            {
                FaceLockedTarget();
            }
        }

        // 更新 Animator 参数
        animator.SetFloat("Speed", currentSpeed);
        UpdateLocomotionParams(hasInput, moveDir, isLocked);

        // ==================== 第3节：重力 ====================
        CaculateGravity();

        // ==================== 第4/5节：跳跃 ====================
        Jump();
    }

    /// <summary>平滑转向锁定目标</summary>
    private void FaceLockedTarget()
    {
        if (_lockManager?.LockedTarget == null) return;

        Vector3 dirToTarget = (_lockManager.LockedTarget.position - transform.position).normalized;
        dirToTarget.y = 0f;
        if (dirToTarget.magnitude < 0.01f) return;

        Quaternion targetRot = Quaternion.LookRotation(dirToTarget);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot,
            _lockRotationSpeed * Time.deltaTime);
    }

    /// <summary>
    /// 更新 Animator 的 inputX / inputY 参数，驱动 2D Blend Tree。
    /// 锁定状态：传入本地坐标系移动（四向平移）。
    /// 自由状态：inputX=0，inputY=有输入时1、无输入时0（前进动画）。
    /// 战斗动作期间（MovementBlocked=true）跳过更新，避免覆盖招式/闪避设置的参数。
    /// </summary>
    private void UpdateLocomotionParams(bool hasInput, Vector3 moveDir, bool isLocked)
    {
        // 战斗动作期间（攻击/闪避等），不更新 Locomotion 参数
        // 因为 DodgeAction 等会通过 DodgeDirectionX/Y 控制自己的 Blend Tree
        if (inputController.MovementBlocked) return;

        if (isLocked && hasInput && moveDir.magnitude > 0.01f)
        {
            // 将世界空间移动方向转为本地空间
            Vector3 localMove = transform.InverseTransformDirection(moveDir);
            InputX = Mathf.Clamp(localMove.x, -1f, 1f);
            InputY = Mathf.Clamp(localMove.z, -1f, 1f);
        }
        else if (isLocked)
        {
            InputX = 0f;
            InputY = 0f;
        }
        else
        {
            // 自由移动：inputX=0 消除左右分量，inputY 只区分动/不动
            InputX = 0f;
            InputY = hasInput ? 1f : 0f;
        }

        animator.SetFloat(ParamInputX, InputX, 0.1f, Time.deltaTime);
        animator.SetFloat(ParamInputY, InputY, 0.1f, Time.deltaTime);

        if (InputX != 0f || InputY != 0f)
            Debug.Log($"[Locomotion] SET inputX={InputX:F2} inputY={InputY:F2} | hasInput={hasInput} isLocked={isLocked} Speed={currentSpeed:F2}");
        else if (hasInput)
            Debug.Log($"[Locomotion] hasInput=TRUE but InputX=0 InputY=0 | isLocked={isLocked} moveDir={moveDir}");
    }

    // ==================== 第3节：重力计算 ====================
    // void CaculateGravity()
    // {
    //     if (isGrounded && VerticalVelocity < 0.0f)
    //     {
    //         VerticalVelocity = -2f;
    //         return;
    //     }
    //     VerticalVelocity += gravity * Time.deltaTime;
    // }
    [Header("跳跃设置")]
    public float fallMultiplier = 1.5f; // 下落时的重力倍数
    void CaculateGravity()
    {
        // 战斗招式控制 Y 轴时（升龙斩/空中连招/裂地劈），停止重力累积
        if (UseRootMotionY)
        {
            VerticalVelocity = 0f;
            return;
        }

        // 如果在地面上且没有向上运动，保持一个微小的向下的力让角色贴紧地面
        if (isGrounded && VerticalVelocity < 0.0f)
        {
            VerticalVelocity = -2f;
            return;
        }

        // 判断是上升还是下落：速度小于0表示正在下落
        float currentGravity = VerticalVelocity < 0.0f ? gravity * fallMultiplier : gravity;

        // 应用重力
        VerticalVelocity += currentGravity * Time.deltaTime;
    }

    // ==================== 第4/5节：跳跃 ====================
    void Jump()
    {
        bool isJumpingUp = VerticalVelocity > 0f;

        if (isGrounded && inputController.isJump && Time.time > lastJumpTime + jumpCooldown)
            {
                VerticalVelocity = Mathf.Sqrt(-2f * gravity * jumpHeight);
                lastJumpTime = Time.time;

                animator.SetTrigger("Jumping");
            }

        // 2. 射线检测：判断脚下 fallHeight 距离内是否有碰撞体（地面）
        // 注意：如果你的角色中心点在腰部，请适度增加 fallHeight 的值，或者将起点改为脚底坐标
        bool couldFall = !Physics.Raycast(transform.position, Vector3.down, fallHeight);

        // 战斗招式控制 Y 轴时不推送 isJump / jumpSpeed，避免覆盖动画参数
        if (!UseRootMotionY)
        {
            animator.SetBool("isJump", !isGrounded && (couldFall || isJumpingUp));
            animator.SetFloat("jumpSpeed", VerticalVelocity, 0.1f, Time.deltaTime);
        }
    }

    // ==================== 第5/6节：动画位移 ====================
    private void OnAnimatorMove()
    {
        // 跳舞动画禁止根运动位移
        if (animator.GetCurrentAnimatorStateInfo(0).shortNameHash == DancingHash)
            return;

        if (UseRootMotionY)
        {
            // 招式系统接管 Y 轴：完整保留动画根运动（升龙斩、空中连招等）
            Vector3 deltaPosition = animator.deltaPosition;
            characterController.Move(BlockEnemyPenetration(deltaPosition));
            averageVel = AverageVel(animator.velocity);
            return;
        }

        if (isGrounded)
        {
            Vector3 deltaPosition = animator.deltaPosition;
            float ax = animator.GetFloat(ParamInputX);
            float ay = animator.GetFloat(ParamInputY);
            float aSpd = animator.GetFloat("Speed");
            Debug.Log($"[OnAnimatorMove] delta={deltaPosition} | SET InputX={InputX:F2} InputY={InputY:F2} | ANIM inputX={ax:F2} inputY={ay:F2} Speed={aSpd:F2}");
            deltaPosition.y = VerticalVelocity * Time.deltaTime;
            characterController.Move(BlockEnemyPenetration(deltaPosition));

            averageVel = AverageVel(animator.velocity);
        }
        else
        {
            if (currentMoveDir.magnitude > 0.1f)
            {
                averageVel += currentMoveDir * airControl * Time.deltaTime;

                Vector3 flatVel = new Vector3(averageVel.x, 0, averageVel.z);
                if (flatVel.magnitude > maxAirSpeed)
                {
                    flatVel = flatVel.normalized * maxAirSpeed;
                    averageVel.x = flatVel.x;
                    averageVel.z = flatVel.z;
                }
            }

            Vector3 deltaPosition = averageVel * Time.deltaTime;
            deltaPosition.y = VerticalVelocity * Time.deltaTime;
            characterController.Move(BlockEnemyPenetration(deltaPosition));
        }
    }

    Vector3 BlockEnemyPenetration(Vector3 delta)
    {
        Vector3 horiz = new Vector3(delta.x, 0f, delta.z);
        if (horiz.magnitude < 0.001f) return delta;

        float radius = characterController.radius * 0.9f;
        float dist = horiz.magnitude;
        Vector3 dir = horiz / dist;
        Vector3 basePos = transform.position;

        // CapsuleCast covers full CharacterController height, catches enemies
        // even when airborne (e.g. jump attacks) where SphereCast would sail over.
        float ccHeight = characterController.height;
        Vector3 ccCenter = characterController.center;
        float halfH = (ccHeight * 0.5f) - radius;
        Vector3 p1 = basePos + ccCenter + Vector3.down * halfH;
        Vector3 p2 = basePos + ccCenter + Vector3.up * halfH;

        RaycastHit[] hits = Physics.CapsuleCastAll(p1, p2, radius, dir, dist);
        foreach (var hit in hits)
        {
            if (hit.collider.CompareTag("Enemy"))
            {
                // 1. 判断玩家是不是在试图向敌人方向挤压
                // 计算从玩家到敌人的方向
                Vector3 dirToEnemy = hit.collider.transform.position - transform.position;
                dirToEnemy.y = 0f;
                
                // Dot > 0 表示玩家当前的移动方向是在靠近敌人
                if (Vector3.Dot(dir, dirToEnemy.normalized) > 0)
                {
                    // 2. 核心修复：沿着碰撞法线滑动 (Sliding) 
                    // 如果 hit.normal 因为穿模变为零向量，提供一个保底法线（从敌人推开玩家的方向）
                    Vector3 normal = hit.normal.sqrMagnitude > 0.01f ? hit.normal : -dirToEnemy.normalized;
                    normal.y = 0f;

                    // 将原本的位移投影到法线所在的平面上，去除“顶穿”敌人的分量，保留“滑动”的分量
                    Vector3 slideDelta = Vector3.ProjectOnPlane(horiz, normal.normalized);
                    
                    delta.x = slideDelta.x;
                    delta.z = slideDelta.z;
                    
                    // 返回修改后的滑动向量，不要继续遍历了，避免多重碰撞导致向量异常
                    return delta; 
                }
            }
        }
        return delta;
    }

    // ==================== 第6节：3帧滚动平均速度 ====================
    Vector3 AverageVel(Vector3 newVel)
    {
        velCache[currentCacheIndex] = newVel;
        currentCacheIndex++;
        currentCacheIndex %= CACHE_SIZE;

        Vector3 average = Vector3.zero;
        foreach (var vel in velCache)
            average += vel;

        return average / CACHE_SIZE;
    }
}
