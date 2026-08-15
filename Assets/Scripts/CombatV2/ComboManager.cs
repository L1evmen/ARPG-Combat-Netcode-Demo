using System;
using System.Collections.Generic;
using CombatV2.Actions;
using CombatV2.LockOn;
using UnityEngine;

namespace CombatV2
{
    /// <summary>
    /// 连招管理器 —— 整个连招系统的中枢。
    ///
    /// 职责：
    /// 1. 检测玩家输入（轻击 / 重击 / 方向）与蓄力状态。
    /// 2. 管理输入缓冲队列，支持宽松的连招输入窗口。
    /// 3. 维护当前招式状态，在不同 ComboAction 子类之间切换。
    /// 4. 提供统一的 Transition / Idle 入口，保证 OCP（招式间互不修改）。
    ///
    /// 使用方式：挂载到玩家 GameObject 上，拖入 Animator、PlayerController、WeaponBase 引用。
    /// </summary>
    public class ComboManager : MonoBehaviour
    {
        [Header("组件引用")]
        [SerializeField] private Animator _animator;
        [SerializeField] private PlayerController _playerController;
        [SerializeField] private WeaponBase _weapon;

        [Header("蓄力等级时间（秒）")]
        [Tooltip("达到 Lv1 所需最短蓄力时间")]
        [SerializeField] private float _chargeL1Time = 0.4f;
        [Tooltip("达到 Lv2 所需蓄力时间")]
        [SerializeField] private float _chargeL2Time = 1.0f;
        [Tooltip("达到 Lv3 所需蓄力时间（同时也是满蓄力上限）")]
        [SerializeField] private float _chargeL3Time = 2.0f;

        [Header("动画")]
        [Tooltip("招式结束后回到的移动状态名（Animator 中的 Locomotion / StandMove 等）")]
        [SerializeField] private string _locomotionStateName = "Locomotion";

        [Header("输入缓冲")]
        [Tooltip("缓冲窗口（秒），在此时间内输入都有效")]
        [SerializeField] private float _bufferWindow = 0.25f;

        // ==================== 内部状态 ====================

        // 动作缓存 —— 每种 ComboAction 只创建一个实例，复用避免 GC
        private readonly Dictionary<Type, ComboAction> _actionCache = new Dictionary<Type, ComboAction>();

        private ComboAction _currentAction;
        private ComboContext _context;
        private InputController _inputCtrl;
        private ICombatInputProvider _inputProvider;

        // 待处理的切换请求（帧末执行）
        private Type _pendingTransition;
        private bool _pendingIdle;

        // 闪避中断连招时保存当前招式，用于闪避后接续连招
        private Type _interruptedComboType;

        // ==================== 输入状态 ====================

        private bool _lightPressedThisFrame;
        private bool _heavyPressedThisFrame;
        private bool _heavyHeld;
        private bool _heavyReleasedThisFrame;
        private bool _upPressedThisFrame;
        private bool _specialPressedThisFrame;

        // 蓄力追踪
        private float _heavyHoldStartTime = -1f;
        private float _chargeDuration;
        private int _chargeLevel;

        // 动画 Hash
        private int _chargeAnimHash;
        private int _locomotionAnimHash;
        private bool _chargeAnimPlaying;

        // 延迟蓄力释放：当前动作不允许转入 ChargeHeavy 时暂存
        private bool _pendingChargeRelease;
        private float _pendingChargeReleaseTime;

        // 输入缓冲队列
        private readonly Queue<BufferedInput> _inputBuffer = new Queue<BufferedInput>();

        private struct BufferedInput
        {
            public ComboInputType Type;
            public float Time;
        }

        // ==================== 公开属性 ====================

        public bool LightPressed => _lightPressedThisFrame;
        public bool HeavyHeld => _heavyHeld;
        public bool HeavyReleased => _heavyReleasedThisFrame;
        public bool UpPressed => _upPressedThisFrame;
        public bool SpecialPressed => _specialPressedThisFrame;
        public bool DodgePressed { get; private set; }

        public bool IsEnemyAirborne { get; set; }
        public int ChargeLevel => _chargeLevel;
        public ComboAction CurrentAction => _currentAction;
        public bool IsInAction => _currentAction != null;

        // ==================== Unity 生命周期 ====================

        private TargetLockManager _lockManager;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _playerController = GetComponent<PlayerController>();
            _inputCtrl = GetComponent<InputController>();
            _inputProvider = GetComponent<ICombatInputProvider>();
            _chargeAnimHash = Animator.StringToHash("ChargeHold");
            _locomotionAnimHash = Animator.StringToHash(_locomotionStateName);

            // 若未手动挂载输入提供者，自动添加默认实现
            if (_inputProvider == null)
            {
                _inputProvider = gameObject.AddComponent<DefaultCombatInputProvider>();
                if (_inputProvider is DefaultCombatInputProvider defaultProvider)
                    defaultProvider.SetInputController(_inputCtrl);
            }

            // 锁定管理器（若 PlayerController 已有 [RequireComponent] 一般不会缺，防御性补加）
            _lockManager = GetComponent<TargetLockManager>();
            if (_lockManager == null)
                _lockManager = gameObject.AddComponent<TargetLockManager>();

            var animEventHandler = GetComponent<PlayerAnimEventHandler>();
            if (animEventHandler == null)
                animEventHandler = gameObject.AddComponent<PlayerAnimEventHandler>();
            animEventHandler.Init(this);

            _context = new ComboContext
            {
                Animator = _animator,
                Controller = GetComponent<CharacterController>(),
                PlayerCtrl = _playerController,
                Manager = this,
                Transform = transform,
                PlayerProperty = GetComponent<PlayerProperty>(),
                Weapon = _weapon,
                AnimEventHandler = animEventHandler
            };

            CollectHitBoxes();
        }

        private void OnEnable()
        {
            if (_lockManager != null)
            {
                _lockManager.OnTargetLocked += OnLocked;
                _lockManager.OnTargetUnlocked += OnUnlocked;
            }
        }

        private void OnDisable()
        {
            if (_lockManager != null)
            {
                _lockManager.OnTargetLocked -= OnLocked;
                _lockManager.OnTargetUnlocked -= OnUnlocked;
            }
        }

        private void OnLocked(Transform target) => _context.LockedEnemy = target;
        private void OnUnlocked() => _context.LockedEnemy = null;

        private void CollectHitBoxes()
        {
            if (_weapon != null)
                _context.HitBoxes = _weapon.GetComponentsInChildren<HitBox>();
        }

        private void Update()
        {
            DetectInputs();
            UpdateInputBuffer();

            if (_currentAction != null)
            {
                // 闪避可取消当前招式（若当前招式可被取消）
                if (DodgePressed && _currentAction.CanDodgeCancel(_context)
                    && _currentAction.CanTransitionTo(typeof(DodgeAction)))
                {
                    _interruptedComboType = _currentAction.GetType();
                    ForceTransition<DodgeAction>();
                    return;
                }

                // 清除上帧的请求
                _pendingTransition = null;
                _pendingIdle = false;

                // 处理延迟蓄力释放（在 OnUpdate 之前，当前动作可在 OnUpdate 中覆盖）
                TryPendingChargeRelease();

                // 动作中转向：锁定时间朝敌人，否则跟随移动方向
                if (_context.LockedEnemy != null)
                {
                    Vector3 toEnemy = _context.LockedEnemy.position - _context.Transform.position;
                    toEnemy.y = 0f;
                    if (toEnemy.magnitude > 0.01f)
                        _context.Transform.rotation = Quaternion.Slerp(
                            _context.Transform.rotation, Quaternion.LookRotation(toEnemy), 12f * Time.deltaTime);
                }
                else if (_context.MoveWorldDir != Vector3.zero)
                {
                    var targetRot = Quaternion.LookRotation(_context.MoveWorldDir);
                    _context.Transform.rotation = Quaternion.Slerp(_context.Transform.rotation, targetRot, 12f * Time.deltaTime);
                }

                // 运行当前招式逻辑
                _currentAction.OnUpdate(_context);

                // 执行切换
                if (_pendingIdle)
                    DoTransitionToIdle();
                else if (_pendingTransition != null)
                    DoTransition(_pendingTransition);
            }
            else
            {
                // 空闲状态：持续追踪蓄力
                ProcessChargeOnIdle();

                // 空闲状态：检测输入启动新动作
                _pendingTransition = null;
                HandleIdleInput();

                if (_pendingTransition != null)
                    DoTransition(_pendingTransition);
            }
        }

        // ==================== 输入检测 ====================

        private void DetectInputs()
        {
            // 通过输入提供者获取（默认 DefaultCombatInputProvider，可替换为重映射实现）
            if (_inputProvider == null)
            {
                _inputProvider = GetComponent<ICombatInputProvider>();
                if (_inputProvider == null)
                    _inputProvider = gameObject.AddComponent<DefaultCombatInputProvider>();
            }
            _inputProvider.UpdateInput();

            _lightPressedThisFrame = _inputProvider.LightPressed;
            _heavyPressedThisFrame = _inputProvider.HeavyPressed;
            _heavyHeld = _inputProvider.HeavyHeld;
            _heavyReleasedThisFrame = _inputProvider.HeavyReleased;
            _upPressedThisFrame = _inputProvider.UpPressed;
            _specialPressedThisFrame = _inputProvider.SpecialPressed;
            DodgePressed = _inputProvider.DodgePressed;

            UpdateMoveDirection();

            // 蓄力开始
            if (_heavyPressedThisFrame)
            {
                _heavyHoldStartTime = Time.time;
                _chargeLevel = 0;
                CombatEvents.FireChargeStarted(3);
            }

            // 蓄力进行中
            if (_heavyHeld && _heavyHoldStartTime > 0)
            {
                _chargeDuration = Time.time - _heavyHoldStartTime;
                int newLevel = CalculateChargeLevel(_chargeDuration);
                if (newLevel != _chargeLevel)
                    _chargeLevel = newLevel;

                float progress = Mathf.Clamp01(_chargeDuration / _chargeL3Time);
                CombatEvents.FireChargeProgress(_chargeLevel, 3, progress);
            }

            // 蓄力释放
            if (_heavyReleasedThisFrame && _heavyHoldStartTime > 0)
            {
                _chargeDuration = Time.time - _heavyHoldStartTime;
                _chargeLevel = CalculateChargeLevel(_chargeDuration);
                CombatEvents.FireChargeEnded();
                StopChargeAnim();

                if (_chargeDuration >= _chargeL1Time)
                {
                    _context.ChargeLevel = _chargeLevel;
                    _context.ChargeDuration = _chargeDuration;
                    _pendingChargeRelease = true;
                    _pendingChargeReleaseTime = Time.time;
                }
                else
                {
                    // 蓄力不足，回到移动状态
                    if (_animator != null)
                        _animator.CrossFadeInFixedTime(_locomotionAnimHash, 0.15f, 0);
                }

                _heavyHoldStartTime = -1f;
            }
        }

        private void UpdateMoveDirection()
        {
            if (_inputProvider == null || _context == null) return;
            Vector2 input = _inputProvider.MoveInput;
            _context.MoveInput = input;

            if (input.magnitude > 0.1f)
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    var forward = cam.transform.forward;
                    var right = cam.transform.right;
                    forward.y = 0f; right.y = 0f;
                    forward.Normalize(); right.Normalize();
                    _context.MoveWorldDir = (forward * input.y + right * input.x).normalized;
                }
            }
            else
            {
                _context.MoveWorldDir = Vector3.zero;
            }
        }

        private int CalculateChargeLevel(float duration)
        {
            if (duration >= _chargeL3Time) return 2;
            if (duration >= _chargeL2Time) return 1;
            if (duration >= _chargeL1Time) return 0;
            return -1; // 未达到最低蓄力门槛
        }

        // ==================== 蓄力释放处理 ====================

        /// <summary>每帧检查延迟蓄力释放是否可以执行</summary>
        private void TryPendingChargeRelease()
        {
            if (!_pendingChargeRelease) return;

            if (Time.time - _pendingChargeReleaseTime > _bufferWindow * 2f)
            {
                _pendingChargeRelease = false;
                return;
            }

            if (_currentAction != null && _currentAction.CanTransitionTo(typeof(ChargeHeavy)))
            {
                _pendingChargeRelease = false;
                _pendingTransition = typeof(ChargeHeavy);
            }
        }

        // ==================== 空闲蓄力 ====================

        private void ProcessChargeOnIdle()
        {
            if (!_heavyHeld) return;
            if (_heavyHoldStartTime < 0) return;

            _chargeDuration = Time.time - _heavyHoldStartTime;
            _chargeLevel = CalculateChargeLevel(_chargeDuration);
            float progress = Mathf.Clamp01(_chargeDuration / _chargeL3Time);
            CombatEvents.FireChargeProgress(_chargeLevel, 3, progress);

            // 播放蓄力待机动画（只播一次，不重复 CrossFade）
            if (!_chargeAnimPlaying && _animator != null)
            {
                _animator.CrossFadeInFixedTime(_chargeAnimHash, 0.1f, 0);
                _chargeAnimPlaying = true;
            }
        }

        private void StopChargeAnim()
        {
            _chargeAnimPlaying = false;
        }

        // ==================== 输入缓冲 ====================

        private void UpdateInputBuffer()
        {
            if (_lightPressedThisFrame)
                _inputBuffer.Enqueue(new BufferedInput { Type = ComboInputType.Light, Time = Time.time });
            if (_heavyReleasedThisFrame)
                _inputBuffer.Enqueue(new BufferedInput { Type = ComboInputType.Heavy, Time = Time.time });
            if (_upPressedThisFrame)
                _inputBuffer.Enqueue(new BufferedInput { Type = ComboInputType.Up, Time = Time.time });

            while (_inputBuffer.Count > 0 &&
                   Time.time - _inputBuffer.Peek().Time > _bufferWindow)
                _inputBuffer.Dequeue();
        }

        public bool HasBufferedInput(ComboInputType type, float window = 0.2f)
        {
            foreach (var item in _inputBuffer)
                if (item.Type == type && Time.time - item.Time <= window)
                    return true;
            return false;
        }

        /// <summary>保留最后一个匹配输入，丢弃其余重复输入，返回是否在窗口内</summary>
        public bool DrainExtraAndHasInput(ComboInputType type, float window = 0.2f)
        {
            BufferedInput? last = null;
            int count = _inputBuffer.Count;
            for (int i = 0; i < count; i++)
            {
                var item = _inputBuffer.Dequeue();
                if (item.Type == type)
                    last = item;
                else
                    _inputBuffer.Enqueue(item);
            }
            if (last != null && Time.time - last.Value.Time <= window)
            {
                _inputBuffer.Enqueue(last.Value);
                return true;
            }
            return false;
        }

        public void ConsumeInput(ComboInputType type)
        {
            var remaining = new Queue<BufferedInput>();
            bool consumed = false;
            while (_inputBuffer.Count > 0)
            {
                var item = _inputBuffer.Dequeue();
                if (!consumed && item.Type == type)
                    consumed = true;
                else
                    remaining.Enqueue(item);
            }
            while (remaining.Count > 0)
                _inputBuffer.Enqueue(remaining.Dequeue());
        }

        // ==================== 空闲状态输入处理 ====================

        private void HandleIdleInput()
        {
            // 闪避
            if (DodgePressed)
            {
                _pendingTransition = typeof(DodgeAction);
                return;
            }

            // 延迟蓄力释放
            if (_pendingChargeRelease)
            {
                _pendingChargeRelease = false;
                _pendingTransition = typeof(ChargeHeavy);
                return;
            }

            // F 键 → 升龙斩（需已解锁且不在冷却中）
            if (_specialPressedThisFrame
                && SkillManager.Instance != null
                && SkillManager.Instance.IsUnlocked("RisingSlash")
                && !SkillManager.Instance.IsOnCooldown("RisingSlash"))
            {
                _pendingTransition = typeof(RisingSlash);
                return;
            }

            // 轻击 → 地面轻击1 或 空中连招
            if (_lightPressedThisFrame)
            {
                if (_context.PlayerProperty == null || _context.PlayerProperty.mentalValue < 5)
                {
                    ConsumeInput(ComboInputType.Light);
                    return;
                }

                _pendingTransition = IsEnemyAirborne
                    ? typeof(AirCombo)
                    : typeof(LightAttack1);
                return;
            }
        }

        // ==================== 招式切换 API ====================

        /// <summary>请求切换到指定招式（由 ComboAction.OnUpdate 调用）</summary>
        public void RequestTransition<T>() where T : ComboAction, new()
        {
            _pendingTransition = typeof(T);
        }

        /// <summary>请求切换到指定招式（非泛型版本，供运行时动态决定目标类型）</summary>
        public void RequestTransition(System.Type type)
        {
            _pendingTransition = type;
        }

        /// <summary>是否有待处理的切换请求（过渡/空闲）</summary>
        public bool HasPendingRequest => _pendingTransition != null || _pendingIdle;

        /// <summary>请求返回空闲（由 ComboAction.CheckAnimEnd 调用）</summary>
        public void RequestIdle()
        {
            _pendingIdle = true;
        }

        /// <summary>强制切换（无视 CanTransitionTo 限制）</summary>
        public void ForceTransition<T>() where T : ComboAction, new()
        {
            _pendingTransition = null;
            _pendingIdle = false;
            DoForceTransition(typeof(T));
        }

        /// <summary>
        /// 强制清除当前 action 并回到空闲状态。
        /// 供 PlayerStateMachine 在 HitStun 结束后调用，清除闪避/攻击中断后的残留状态。
        /// </summary>
        public void ForceIdle()
        {
            bool wasCharging = _heavyHoldStartTime >= 0f;
            ExitCurrentAction();
            _inputBuffer.Clear();
            _pendingTransition = null;
            _pendingIdle = false;
            _interruptedComboType = null;
            _pendingChargeRelease = false;
            _heavyHoldStartTime = -1f;
            _chargeDuration = 0f;
            _chargeLevel = -1;
            StopChargeAnim();
            IsEnemyAirborne = false;
            if (_context != null)
            {
                _context.HitboxControlledByAnimEvent = false;
                _context.HitboxWindowOpened = false;
                _context.HitboxActive = false;
            }
            if (wasCharging)
                CombatEvents.FireChargeEnded();
            BlockMovement(false);
        }

        // ==================== 内部切换实现 ====================

        private void DoTransition(Type nextType)
        {
            if (_currentAction != null && !_currentAction.CanTransitionTo(nextType))
                return;

            Type previousType = _currentAction?.GetType();
            var action = GetOrCreateAction(nextType);
            if (!action.CanEnter(_context))
                return;

            ExitCurrentAction();
            EnsureHitBoxes();

            action.ResetState(previousType == nextType);
            _context.ActionEnterTime = Time.time;
            _context.HitboxControlledByAnimEvent = false;
            _context.HitboxWindowOpened = false;
            _context.HitboxActive = false;
            _context.SfxControlledByAnimEvent = false;

            BlockMovement(true);
            _currentAction = action;
            _currentAction.OnEnter(_context);

            ConsumeInput(ComboInputType.Light);
        }

        private void DoForceTransition(Type nextType)
        {
            Type previousType = _currentAction?.GetType();
            ExitCurrentAction();
            EnsureHitBoxes();

            var action = GetOrCreateAction(nextType);
            action.ResetState(previousType == nextType);
            _context.ActionEnterTime = Time.time;
            _context.HitboxControlledByAnimEvent = false;
            _context.HitboxWindowOpened = false;
            _context.HitboxActive = false;
            _context.SfxControlledByAnimEvent = false;

            BlockMovement(true);
            _currentAction = action;
            _currentAction.OnEnter(_context);
        }

        private void EnsureHitBoxes()
        {
            if (_context.HitBoxes != null && _context.HitBoxes.Length > 0) return;
            _context.HitBoxes = GetComponentsInChildren<HitBox>(includeInactive: true);
        }

        private void DoTransitionToIdle()
        {
            ExitCurrentAction();
            IsEnemyAirborne = false;
            BlockMovement(false);
            _pendingChargeRelease = false;

            // 落地才切 StandMove；空中由 Animator AnyState 自行过渡到 Jump
            if (_animator != null && _context.IsGrounded)
                _animator.CrossFadeInFixedTime(_locomotionAnimHash, 0.25f, 0);

            // 闪避中断连招后续接：有缓冲轻击 → 直接进下一段
            if (_interruptedComboType != null)
            {
                Type next = GetNextComboStep(_interruptedComboType);
                _interruptedComboType = null;
                if (next != null && HasBufferedInput(ComboInputType.Light, _bufferWindow * 3f))
                {
                    ConsumeInput(ComboInputType.Light);
                    DoTransition(next);
                }
            }
        }

        Type GetNextComboStep(Type current)
        {
            if (current == typeof(LightAttack1)) return typeof(LightAttack2);
            if (current == typeof(LightAttack2)) return typeof(LightAttack3);
            if (current == typeof(LightAttack3)) return typeof(LightAttack4);
            if (current == typeof(LightAttack4)) return typeof(LightAttack5);
            if (current == typeof(LightAttack5)) return typeof(LightAttack1);
            return null;
        }

        private void ExitCurrentAction()
        {
            if (_currentAction == null) return;
            _currentAction.OnExit(_context);
            DisableAllHitBoxes();
            _currentAction = null;
        }

        private void BlockMovement(bool blocked)
        {
            if (_inputCtrl != null)
                _inputCtrl.MovementBlocked = blocked;
        }

        // ==================== 动作缓存 ====================

        private ComboAction GetOrCreateAction(Type type)
        {
            if (!_actionCache.TryGetValue(type, out var action))
            {
                action = (ComboAction)Activator.CreateInstance(type);
                _actionCache[type] = action;
            }
            return action;
        }

        // ==================== HitBox ====================

        private void DisableAllHitBoxes()
        {
            if (_context.HitBoxes != null)
                foreach (var hb in _context.HitBoxes)
                    hb.Disable();

            if (_weapon != null)
                _weapon.DisableHitBoxes();
        }

        // ==================== 公开工具方法 ====================

        public void SetLockedEnemy(Transform enemy) => _context.LockedEnemy = enemy;
        public ComboContext GetContext() => _context;

        /// <summary>动态更新武器引用（由 PlayerAttack 装备武器后调用）</summary>
        public void RefreshWeapon(GameObject weaponObject)
        {
            if (weaponObject == null) return;
            _context.HitBoxes = weaponObject.GetComponentsInChildren<HitBox>(includeInactive: true);
            var wb = weaponObject.GetComponent<WeaponBase>();
            if (wb != null) _context.Weapon = wb;
        }
    }
}
