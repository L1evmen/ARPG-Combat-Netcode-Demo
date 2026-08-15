using UnityEngine;
using CombatV2.LockOn;

public class TPSCameraController : MonoBehaviour
{
    public Transform target;
    public float distance = 5f;
    public float minDistance = 2f;
    public float maxDistance = 10f;
    public float sensitivity = 3f;
    public float minPitch = -20f;
    public float maxPitch = 60f;
    public float heightOffset = 1.4f;
    public float aimOffsetX = 0.5f;
    public float aimDistance = 2.5f;
    public float aimSmoothSpeed = 8f;

    private float yaw;
    private float pitch;
    private float currentAimOffset;
    private float currentAimDistance;

    public bool IsAiming { get; private set; }

    private Animator targetAnimator;
    private static readonly int ParamHoldItemId = Animator.StringToHash("HoldItemId");
    private static readonly int ParamThrowReady = Animator.StringToHash("ThrowReady");

    // ==================== 锁定摄像机 ====================
    [Header("锁定摄像机")]
    [SerializeField] private TargetLockManager _lockManager;

    [Tooltip("锁定时视线在玩家与敌人之间的混合比例（0=玩家, 1=敌人）")]
    [Range(0f, 1f)]
    [SerializeField] private float _lockLookBlend = 0.5f;

    [Tooltip("锁定摄像机距离")]
    [SerializeField] private float _lockDistance = 4.5f;

    [Tooltip("锁定过渡阻尼")]
    [SerializeField] private float _lockDamping = 6f;

    private Vector3 _smoothCamPos;
    private Vector3 _smoothLookAt;
    private float _lockedHeight;
    private bool _lockInit;
    private bool _wasLocked;

    // ==================== 对话摄像机 ====================
    private bool _dialogueCamActive;
    private Transform _dialogueCamPoint;

    public float defaultPitch = 10f;
    public float defaultDistance = 4f;

    /// <summary>启用对话摄像机，摄像机移到 camPoint 的位置和朝向。</summary>
    public void SetDialogueCamera(Transform camPoint)
    {
        _dialogueCamActive = true;
        _dialogueCamPoint = camPoint;
    }

    /// <summary>关闭对话摄像机，回归正常模式。</summary>
    public void ClearDialogueCamera()
    {
        _dialogueCamActive = false;
        _dialogueCamPoint = null;
    }

    void Start()
    {
        yaw = target.eulerAngles.y;
        pitch = defaultPitch;
        distance = defaultDistance;
        Cursor.lockState = CursorLockMode.Locked;
        targetAnimator = target.GetComponentInChildren<Animator>();
        if (targetAnimator == null)
            targetAnimator = target.GetComponent<Animator>();

        if (_lockManager == null)
            _lockManager = target.GetComponent<TargetLockManager>();

        _smoothCamPos = transform.position;
        _smoothLookAt = target.position + Vector3.up * heightOffset;
    }

    void LateUpdate()
    {
        int holdItemId = targetAnimator != null ? targetAnimator.GetInteger(ParamHoldItemId) : -1;
        bool holdingJavelin = holdItemId == 2;
        IsAiming = holdingJavelin && Input.GetMouseButton(1);

        if (targetAnimator != null)
        {
            targetAnimator.SetBool(ParamThrowReady, IsAiming);
        }

        bool dialogueOpen = DialogueUI.Instance != null && DialogueUI.Instance.IsVisible;
        bool inventoryOpen = GameInventory.InventoryController.Instance != null && GameInventory.InventoryController.Instance.IsOpen;
        bool isLocked = _lockManager != null && _lockManager.IsLocked && _lockManager.LockedTarget != null;

        // ---- 自由摄像机：鼠标输入（有 UI 打开时不转动视角） ----
        if (!dialogueOpen && !inventoryOpen && !isLocked && !Cursor.visible)
        {
            yaw += Input.GetAxis("Mouse X") * sensitivity;
            pitch -= Input.GetAxis("Mouse Y") * sensitivity;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            distance -= Input.GetAxis("Mouse ScrollWheel") * 2f;
            distance = Mathf.Clamp(distance, minDistance, maxDistance);
        }

        float targetAimOffset = (IsAiming && holdingJavelin) ? aimOffsetX : 0f;
        float targetAimDistance = (IsAiming && holdingJavelin) ? aimDistance : distance;
        float t = Time.deltaTime * aimSmoothSpeed;
        currentAimOffset = Mathf.Lerp(currentAimOffset, targetAimOffset, t);
        currentAimDistance = Mathf.Lerp(currentAimDistance, targetAimDistance, t);

        Vector3 playerPoint = target.position + Vector3.up * heightOffset + target.right * currentAimOffset;

        // ---- 锁定摄像机：位置 + 视线都由锁定目标决定 ----
        if (isLocked)
        {
            _wasLocked = true;
            Transform enemy = _lockManager.LockedTarget;
            Vector3 enemyPoint = _lockManager.CurrentLockable != null
                ? _lockManager.CurrentLockable.LockPoint
                : enemy.position + Vector3.up * 1.5f;

            // 敌人水平方向（忽略高度差）
            Vector3 toEnemy = enemyPoint - target.position;
            toEnemy.y = 0f;

            if (toEnemy.magnitude > 0.1f)
            {
                Vector3 enemyDir = toEnemy.normalized;

                // 首次进入锁定：捕获当前摄像机高度，后续保持
                if (!_lockInit)
                {
                    _lockedHeight = transform.position.y - target.position.y;
                    _lockInit = true;
                }

                // 摄像机目标位置：玩家身后，高度保持锁定前的高度
                Vector3 targetCamPos = target.position
                    + Vector3.up * _lockedHeight
                    - enemyDir * _lockDistance;

                // 视线目标：玩家与敌人锁定点之间
                Vector3 targetLookAt = Vector3.Lerp(
                    target.position + Vector3.up * heightOffset,
                    enemyPoint,
                    _lockLookBlend);

                // 锁定时有阻尼
                float d = Time.deltaTime * _lockDamping;
                _smoothCamPos = Vector3.Lerp(_smoothCamPos, targetCamPos, d);
                _smoothLookAt = Vector3.Lerp(_smoothLookAt, targetLookAt, d);
            }

            transform.position = _smoothCamPos;
            transform.LookAt(_smoothLookAt);
        }
        else if (_dialogueCamActive && _dialogueCamPoint != null)
        {
            // ---- 对话摄像机：移到指定点位，朝向由点位旋转决定 ----
            float d = Time.deltaTime * _lockDamping;
            _smoothCamPos = Vector3.Lerp(_smoothCamPos, _dialogueCamPoint.position, d);
            transform.position = _smoothCamPos;
            transform.rotation = Quaternion.Slerp(transform.rotation, _dialogueCamPoint.rotation, d);
            _lockInit = false;
        }
        else
        {
            float activeDistance = (IsAiming && holdingJavelin) ? currentAimDistance : distance;
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0);
            Vector3 offset = rot * new Vector3(0, 0, -activeDistance);

            Vector3 freeCamPos = playerPoint + offset;
            Vector3 freeLookAt = playerPoint;

            // 刚从锁定退出：平滑过渡回自由摄像机
            if (_wasLocked)
            {
                float d = Time.deltaTime * _lockDamping;
                _smoothCamPos = Vector3.Lerp(_smoothCamPos, freeCamPos, d);
                _smoothLookAt = Vector3.Lerp(_smoothLookAt, freeLookAt, d);
                transform.position = _smoothCamPos;
                transform.LookAt(_smoothLookAt);

                // 接近到位后切回即时模式
                if (Vector3.Distance(_smoothCamPos, freeCamPos) < 0.05f)
                    _wasLocked = false;
            }
            else
            {
                // 自由模式：无阻尼，即时跟随
                transform.position = freeCamPos;
                transform.LookAt(freeLookAt);
                _smoothCamPos = freeCamPos;
                _smoothLookAt = freeLookAt;
            }

            _lockInit = false;
        }
    }
}
