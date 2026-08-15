using UnityEngine;

namespace CombatV2
{
    /// <summary>
    /// 连招上下文 —— 在招式脚本间共享的运行时状态。
    /// 每个 OnEnter / OnUpdate / OnExit 均传入同一个 ComboContext 实例。
    ///
    /// 设计原则：
    /// 1. 纯数据 + 便捷查询，不包含业务逻辑。
    /// 2. 招式脚本通过此上下文获取输入、动画、敌人状态等信息。
    /// 3. 招式脚本通过 ctx.Manager.RequestTransition&lt;T&gt;() 请求切换。
    /// </summary>
    public class ComboContext
    {
        // ==================== 组件引用 ====================

        public Animator Animator;
        public CharacterController Controller;
        public PlayerController PlayerCtrl;
        public ComboManager Manager;
        public Transform Transform;
        public PlayerProperty PlayerProperty;
        public WeaponBase Weapon;
        public HitBox[] HitBoxes;
        public PlayerAnimEventHandler AnimEventHandler;

        /// <summary>当 Animation Event 接管 HitBox 控制时为 true，UpdateHitboxAuto 跳过轮询</summary>
        public bool HitboxControlledByAnimEvent;

        /// <summary>当前招式的伤害判定是否已经开启过。</summary>
        public bool HitboxWindowOpened;

        /// <summary>当前招式的伤害判定是否处于开启状态。</summary>
        public bool HitboxActive;

        /// <summary>当 Animation Event 接管音效控制时为 true，CombatAudio 跳过 OnAttackStarted 播放</summary>
        public bool SfxControlledByAnimEvent;

        // ==================== 敌人状态 ====================

        /// <summary>当前锁定敌人是否处于浮空状态</summary>
        public bool IsEnemyAirborne => Manager.IsEnemyAirborne;

        /// <summary>当前锁定的敌人 Transform</summary>
        public Transform LockedEnemy;

        // ==================== 蓄力数据（由 ComboManager 填入） ====================

        /// <summary>蓄力等级（0 ~ ChargeLevels-1）</summary>
        public int ChargeLevel;

        /// <summary>蓄力持续时间（秒）</summary>
        public float ChargeDuration;

        // ==================== 方向输入 ====================

        public Vector2 MoveInput;
        public Vector3 MoveWorldDir;

        // ==================== 地面状态 ====================

        public bool IsGrounded => PlayerCtrl != null && PlayerCtrl.isGrounded;

        // ==================== 时间信息 ====================

        /// <summary>进入当前动作的时间戳</summary>
        public float ActionEnterTime;

        /// <summary>当前动作已持续时间（秒）</summary>
        public float ActionElapsed => Time.time - ActionEnterTime;

        /// <summary>当前动画归一化时间。
        /// CrossFade 期间 Animator 当前状态尚未切换到目标动画，此时用已流逝时间估算。</summary>
        public float NormalizedTime
        {
            get
            {
                float fallback = Manager?.CurrentAction != null
                    ? Mathf.Clamp01(ActionElapsed / Manager.CurrentAction.AnimLength)
                    : 0f;
                if (Animator == null) return fallback;
                var info = Animator.GetCurrentAnimatorStateInfo(0);
                if (Manager?.CurrentAction != null &&
                    info.shortNameHash == Manager.CurrentAction.AnimHash)
                    return info.normalizedTime;
                return fallback;
            }
        }

        // ==================== 输入查询（便捷属性） ====================

        public bool LightPressed => Manager.LightPressed;
        public bool HeavyHeld => Manager.HeavyHeld;
        public bool HeavyReleased => Manager.HeavyReleased;
        public bool UpPressed => Manager.UpPressed;
        public bool SpecialPressed => Manager.SpecialPressed;
        public bool DodgePressed => Manager.DodgePressed;

        public bool HasBufferedLight(float window = 0.2f) =>
            Manager.DrainExtraAndHasInput(ComboInputType.Light, window);

        public bool HasBufferedHeavy(float window = 0.2f) =>
            Manager.DrainExtraAndHasInput(ComboInputType.Heavy, window);

        public void ConsumeLight() => Manager.ConsumeInput(ComboInputType.Light);
        public void ConsumeHeavy() => Manager.ConsumeInput(ComboInputType.Heavy);

        // ==================== HitBox 辅助 ====================

        public void EnableHitBoxes(int damage)
        {
            HitboxWindowOpened = true;
            HitboxActive = true;
            Debug.Log($"[ComboContext] EnableHitBoxes | count={HitBoxes?.Length ?? 0} | damage={damage}");
            if (HitBoxes != null)
                foreach (var hb in HitBoxes) hb.Enable(damage);
        }

        public void DisableHitBoxes()
        {
            HitboxActive = false;
            if (HitBoxes != null)
                foreach (var hb in HitBoxes) hb.Disable();
        }
    }
}
