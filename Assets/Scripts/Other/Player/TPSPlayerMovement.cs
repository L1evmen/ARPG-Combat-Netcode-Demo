using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class TPSPlayerMovement : MonoBehaviour
{
    [Header("移动")]
    public float walkSpeed = 2f;
    public float runSpeed = 5f;
    public float acceleration = 8f;
    public float turnSpeed = 15f;
    public float aimTurnSpeed = 30f;

    [Header("跳跃")]
    public float maxHeight = 1.5f;
    public float gravity = -9.8f;
    public float fallMultiplier = 1.5f;
    public float coyoteTime = 0.1f;
    public float jumpBufferTime = 0.1f;

    [Header("跌落检测")]
    public float fallHeight = 0.5f;

    [Header("其他")]
    public float interactRange = 3f;

    private CharacterController characterController;
    private Animator animator;
    private TPSCameraController camCtrl;

    private bool isGrounded;
    private float VerticalVelocity;
    private bool isJumping;

    private float currentSpeed;
    private Vector3 currentMoveDir;
    private bool hasMoveInput;
    private bool isMove;

    private float coyoteTimer;
    private float jumpBufferTimer;

    private static readonly int CACHE_SIZE = 3;
    private Vector3[] velCache = new Vector3[CACHE_SIZE];
    private int currentCacheIndex;
    private Vector3 averageVel;

    private bool couldFall;
    private bool justLanded;

    void Start()
    {
        characterController = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        animator.applyRootMotion = false;
        camCtrl = Camera.main?.GetComponent<TPSCameraController>();
    }

    void Update()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        bool runHeld = Input.GetKey(KeyCode.LeftShift);
        bool jumpPressed = Input.GetKeyDown(KeyCode.Space);

        var state = animator.GetCurrentAnimatorStateInfo(0);
        bool locked = state.IsName("Attack") || state.IsName("Throw");

        Camera cam = Camera.main;
        Vector3 forward = cam.transform.forward;
        Vector3 right = cam.transform.right;
        forward.y = 0f; right.y = 0f;
        forward.Normalize(); right.Normalize();

        currentMoveDir = (forward * v + right * h).normalized;
        isMove = v != 0 || h != 0;
        hasMoveInput = currentMoveDir.magnitude > 0.01f;

        isGrounded = characterController.isGrounded;
        couldFall = !Physics.Raycast(transform.position, Vector3.down, fallHeight);

        if (isGrounded)
            coyoteTimer = coyoteTime;
        else if (coyoteTimer > 0f)
            coyoteTimer -= Time.deltaTime;

        if (jumpPressed)
            jumpBufferTimer = jumpBufferTime;
        else if (jumpBufferTimer > 0f)
            jumpBufferTimer -= Time.deltaTime;

        if (locked)
        {
            currentSpeed = 0f;
        }
        else if (!isJumping)
        {
            if (hasMoveInput)
            {
                float targetSpeed = runHeld ? runSpeed : walkSpeed;
                currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * Time.deltaTime);

                Quaternion targetRot;
                if (camCtrl != null && camCtrl.IsAiming)
                    targetRot = Quaternion.LookRotation(forward);
                else
                    targetRot = Quaternion.LookRotation(currentMoveDir);

                float ts = (camCtrl != null && camCtrl.IsAiming) ? aimTurnSpeed : turnSpeed;
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, ts * Time.deltaTime);
            }
            else
            {
                currentSpeed = 0f;

                if (camCtrl != null && camCtrl.IsAiming)
                {
                    Quaternion targetRot = Quaternion.LookRotation(forward);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, aimTurnSpeed * Time.deltaTime);
                }
            }
        }
        else
        {
            if (hasMoveInput)
                currentSpeed = Mathf.MoveTowards(currentSpeed, runHeld ? runSpeed : walkSpeed, acceleration * Time.deltaTime);
        }

        CaculateGravity(locked);
        Jump(locked);

        // 速度缓存：地面时用输入速度，离地后用于空中水平移动
        if (isGrounded)
        {
            Vector3 currentVel = hasMoveInput ? currentMoveDir * currentSpeed : Vector3.zero;
            averageVel = AverageVel(currentVel);
        }

        // 位移：地面用输入方向×速度，空中用缓存速度
        Vector3 horizontalMove = isGrounded
            ? (hasMoveInput ? currentMoveDir * currentSpeed : Vector3.zero)
            : averageVel;
        horizontalMove *= Time.deltaTime;
        horizontalMove.y = 0f;
        characterController.Move(horizontalMove + Vector3.up * VerticalVelocity * Time.deltaTime);

        animator.SetFloat("Speed", currentSpeed);
        animator.SetBool("isJump", !isGrounded && couldFall);
        // 落地后 Jump→Idle transition 期间，锁死 jumpSpeed 在 Landing threshold
        if (justLanded)
        {
            if (animator.GetCurrentAnimatorStateInfo(0).IsName("Jump"))
                animator.SetFloat("jumpSpeed", -5.4f);
            else
                justLanded = false;
        }
        else
        {
            animator.SetFloat("jumpSpeed", VerticalVelocity, 0.1f, Time.deltaTime);
        }
    }

    void CaculateGravity(bool locked)
    {
        if (isGrounded && !isJumping)
        {
            VerticalVelocity = gravity * Time.deltaTime;
            return;
        }

        if (isGrounded && isJumping)
        {
            isJumping = false;
            justLanded = true;
            return;
        }

        if (VerticalVelocity <= 0f || !isJumping)
            VerticalVelocity += fallMultiplier * gravity * Time.deltaTime;
        else
            VerticalVelocity += gravity * Time.deltaTime;
    }

    void Jump(bool locked)
    {
        if (isGrounded && !locked && jumpBufferTimer > 0f)
        {
            VerticalVelocity = Mathf.Sqrt(-2f * gravity * maxHeight);
            isJumping = true;
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;

            float feetTween = animator.GetCurrentAnimatorStateInfo(0).normalizedTime % 1f;
            float jumpPosture = feetTween < 0.5f ? 1f : -1f;

            if (isMove)
                jumpPosture *= 2f;
            else
                jumpPosture *= Random.Range(-1f, 1f);

            animator.SetFloat("jumpPosture", jumpPosture);
        }
    }

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

    void LateUpdate()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            InteractableObject nearest = null;
            float minDist = float.MaxValue;

            foreach (var c in Physics.OverlapSphere(transform.position, interactRange))
            {
                if (!c.CompareTag("Interactable")) continue;

                var obj = c.GetComponent<InteractableObject>();
                if (obj == null) continue;

                float d = Vector3.Distance(transform.position, c.transform.position);
                if (d < minDist)
                {
                    minDist = d;
                    nearest = obj;
                }
            }

            if (nearest != null)
                nearest.TriggerInteract();
        }
    }

    public bool IsMoving() => currentSpeed > 0.1f;
}
