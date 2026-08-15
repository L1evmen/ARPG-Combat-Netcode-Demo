using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CombatV2.LockOn
{
    /// <summary>
    /// 目标锁定管理器 —— 处理索敌、锁定/解锁、目标筛选。
    /// 挂载在玩家 GameObject 上，与 PlayerController、ComboManager 松耦合。
    ///
    /// 触发：鼠标中键按下 → 锁定屏幕中心最近的合法目标。
    ///        再次按下 → 解锁。
    /// 自动解锁：目标死亡、超出最大脱战距离、被障碍物持续遮挡。
    /// </summary>
    public class TargetLockManager : MonoBehaviour
    {
        // ==================== Inspector 配置 ====================

        [Header("索敌参数")]
        [Tooltip("最大锁定距离（米）")]
        [SerializeField] private float _maxLockDistance = 15f;

        [Tooltip("索敌半角（度），从摄像机前方算起")]
        [SerializeField] private float _maxLockAngle = 55f;

        [Tooltip("超出此距离自动解锁")]
        [SerializeField] private float _unlockDistance = 22f;

        [Header("障碍物检测")]
        [Tooltip("障碍物 LayerMask（阻挡视线的物体）")]
        [SerializeField] private LayerMask _obstacleMask = -1;

        [Tooltip("被连续遮挡多久后自动解锁（秒）")]
        [SerializeField] private float _obstacleUnlockDelay = 1.2f;

        [Header("可锁定目标层")]
        [Tooltip("只搜索这些 Layer 上的 ILockable，留空则搜索所有层")]
        [SerializeField] private LayerMask _targetLayers = -1;

        // ==================== 公开状态 ====================

        /// <summary>当前锁定的目标 Transform（只读）</summary>
        public Transform LockedTarget { get; private set; }

        /// <summary>是否处于锁定状态</summary>
        public bool IsLocked => LockedTarget != null;

        /// <summary>当前锁定目标的 ILockable 接口引用</summary>
        public ILockable CurrentLockable { get; private set; }

        // ==================== 事件 ====================

        /// <summary>锁定目标时触发，参数为锁定目标的 Transform</summary>
        public event Action<Transform> OnTargetLocked;

        /// <summary>解锁时触发</summary>
        public event Action OnTargetUnlocked;

        // ==================== 内部状态 ====================

        private Camera _cam;
        private float _obstructedTime;
        private readonly Collider[] _overlapResults = new Collider[64];

        // ==================== Unity 生命周期 ====================

        private void Awake()
        {
            _cam = Camera.main;
        }

        private void Update()
        {
            if (_cam == null)
            {
                _cam = Camera.main;
                if (_cam == null) return;
            }

            // 检测鼠标中键输入（使用新 Input System，与 DefaultCombatInputProvider 风格一致）
            if (Mouse.current?.middleButton.wasPressedThisFrame == true)
            {
                if (IsLocked)
                    Unlock();
                else
                    TryLock();
            }

            // 锁定状态下持续检查解锁条件
            if (IsLocked)
                CheckUnlockConditions();
        }

        // ==================== 公开 API ====================

        /// <summary>尝试锁定：从摄像机前方锥形区域内选取最靠近屏幕中心的敌人</summary>
        public void TryLock()
        {
            FindBestTarget(out Transform best, out ILockable lockable);
            if (best != null)
                LockOn(best, lockable);
        }

        /// <summary>强制锁定到指定 Transform</summary>
        public void LockOn(Transform target, ILockable lockable = null)
        {
            if (target == null) return;

            if (LockedTarget != null)
                UnlockSilent();

            LockedTarget = target;
            CurrentLockable = lockable ?? target.GetComponentInParent<ILockable>();
            _obstructedTime = 0f;

            Debug.Log($"[TargetLockManager] 锁定目标: {target.name} | type={CurrentLockable?.GetType().Name} CanBeLocked={CurrentLockable?.CanBeLocked}");
            OnTargetLocked?.Invoke(target);
        }

        /// <summary>解锁当前目标（触发 OnTargetUnlocked 事件）</summary>
        public void Unlock()
        {
            if (LockedTarget == null) return;

            Debug.Log($"[TargetLockManager] 解锁目标: {LockedTarget.name}");
            UnlockSilent();
            OnTargetUnlocked?.Invoke();
        }

        // ==================== 索敌逻辑 ====================

        /// <summary>
        /// 在玩家周围球形区域内搜索所有 ILockable，
        /// 过滤出摄像机前方锥形范围内的目标，
        /// 返回屏幕空间中最靠近屏幕中心的那个。
        /// </summary>
        private void FindBestTarget(out Transform bestTransform, out ILockable bestLockable)
        {
            int count = Physics.OverlapSphereNonAlloc(
                transform.position, _maxLockDistance, _overlapResults, _targetLayers);

            bestTransform = null;
            bestLockable = null;
            float bestScore = float.MaxValue;
            Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            for (int i = 0; i < count; i++)
            {
                Collider col = _overlapResults[i];
                if (col == null) continue;

                // ILockable 可能在 Collider 所在物体或其父级（如 Boss 的 Collider 在子物体，BossController 在根物体）
                var lockable = col.GetComponent<ILockable>()
                    ?? col.GetComponentInParent<ILockable>();
                if (lockable == null) continue;

                if (!lockable.CanBeLocked) continue;

                Vector3 lockPoint = lockable.LockPoint;

                Vector3 toTarget = (lockPoint - _cam.transform.position).normalized;
                float angle = Vector3.Angle(_cam.transform.forward, toTarget);
                if (angle > _maxLockAngle) continue;

                Vector3 screenPos = _cam.WorldToScreenPoint(lockPoint);
                if (screenPos.z <= 0f) continue;

                float score = Vector2.Distance(
                    new Vector2(screenPos.x, screenPos.y), screenCenter);

                if (score < bestScore)
                {
                    bestScore = score;
                    bestTransform = (lockable as MonoBehaviour)?.transform ?? col.transform;
                    bestLockable = lockable;
                }
            }
        }

        // ==================== 解锁条件检测 ====================

        private void CheckUnlockConditions()
        {
            if (LockedTarget == null) return;

            // 条件1：目标不再可锁定（死亡等）
            if (CurrentLockable != null && !CurrentLockable.CanBeLocked)
            {
                Debug.Log($"[TargetLockManager] 自动解锁: CanBeLocked={CurrentLockable.CanBeLocked} | target={LockedTarget.name}");
                Unlock();
                return;
            }

            // 条件1b：目标 GameObject 被销毁
            if (LockedTarget == null)
            {
                Debug.Log("[TargetLockManager] 自动解锁: LockedTarget 已销毁");
                Unlock();
                return;
            }

            // 条件2：距离超出最大脱战距离
            float dist = Vector3.Distance(transform.position, LockedTarget.position);
            if (dist > _unlockDistance)
            {
                Debug.Log($"[TargetLockManager] 自动解锁: 距离过远 dist={dist:F1} > max={_unlockDistance}");
                Unlock();
                return;
            }

            // 条件3：视线被障碍物持续遮挡
            // 从摄像机向锁定点发射射线，排除目标自身的碰撞体，只检测真正的障碍物（墙壁、柱子等）
            Vector3 toTarget = CurrentLockable != null
                ? CurrentLockable.LockPoint - _cam.transform.position
                : LockedTarget.position - _cam.transform.position;

            float camDist = toTarget.magnitude;
            if (Physics.Raycast(_cam.transform.position, toTarget.normalized,
                out RaycastHit hit, camDist, _obstacleMask, QueryTriggerInteraction.Ignore))
            {
                // 命中了锁定目标自身 → 不是障碍物，重置计时
                if (hit.collider.transform == LockedTarget
                    || hit.collider.transform.IsChildOf(LockedTarget))
                {
                    _obstructedTime = 0f;
                }
                else
                {
                    _obstructedTime += Time.deltaTime;
                    if (_obstructedTime >= _obstacleUnlockDelay)
                    {
                        Unlock();
                        return;
                    }
                }
            }
            else
            {
                _obstructedTime = 0f;
            }
        }

        private void UnlockSilent()
        {
            LockedTarget = null;
            CurrentLockable = null;
            _obstructedTime = 0f;
        }

        private void OnDisable()
        {
            if (IsLocked) Unlock();
        }

        // ==================== Editor Gizmo ====================

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.8f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, _maxLockDistance);

            if (_cam != null)
            {
                Gizmos.color = new Color(1f, 0.8f, 0f, 0.15f);
                Vector3 forward = _cam.transform.forward * _maxLockDistance;
                Vector3 right = _cam.transform.right * Mathf.Tan(_maxLockAngle * Mathf.Deg2Rad) * _maxLockDistance;
                Vector3 up = _cam.transform.up * Mathf.Tan(_maxLockAngle * Mathf.Deg2Rad) * _maxLockDistance;

                Vector3 origin = _cam.transform.position;
                Vector3 center = origin + forward;
                Gizmos.DrawLine(origin, center + right + up);
                Gizmos.DrawLine(origin, center - right + up);
                Gizmos.DrawLine(origin, center + right - up);
                Gizmos.DrawLine(origin, center - right - up);
            }
        }
#endif
    }
}
