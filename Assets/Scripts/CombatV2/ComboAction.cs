using UnityEngine;

namespace CombatV2
{
    /// <summary>
    /// 招式抽象基类 —— 所有攻击动作（轻击 / 重击 / 升龙 / 空中 / 裂地）均继承此类。
    ///
    /// 设计原则：
    /// 1. 每个子类 = 一个独立的招式脚本，放在 Actions/ 目录下。
    /// 2. 添加新招式 = 新建一个 ComboAction 子类，不改任何已有文件（OCP）。
    /// 3. 动画通过 Animator.CrossFade / Play 直接控制，不依赖 Animator 状态机连线。
    /// 4. 招式间切换通过 RequestTransition&lt;T&gt;() 请求，由 ComboManager 统一执行。
    /// </summary>
    public abstract class ComboAction
    {
        // ==================== 子类必须重写的配置属性 ====================

        /// <summary>Animator 中对应的动画状态名称（例如 "LightAttack1"）</summary>
        public abstract string AnimName { get; }

        /// <summary>动画总时长（秒），用于超时检测</summary>
        public abstract float AnimLength { get; }

        /// <summary>基础伤害值</summary>
        public abstract int Damage { get; }

        // ==================== 可选重写的窗口配置 ====================

        /// <summary>连招输入窗口起始（归一化时间 0~1）</summary>
        public virtual float ComboWindowStart => 0.45f;

        /// <summary>连招输入窗口结束（归一化时间 0~1）</summary>
        public virtual float ComboWindowEnd => 0.80f;

        /// <summary>伤害判定窗口起始（归一化时间）</summary>
        public virtual float HitboxStart => 0.2f;

        /// <summary>伤害判定窗口结束（归一化时间）</summary>
        public virtual float HitboxEnd => 0.55f;

        /// <summary>是否可被其他招式取消</summary>
        public virtual bool CanBeCancelled => false;

        /// <summary>取消窗口起始（归一化时间），仅在 CanBeCancelled=true 时有效</summary>
        public virtual float CancelWindowStart => 0.6f;

        /// <summary>招式期间的前方位移速度（0 = 无位移）</summary>
        public virtual float ForwardSpeed => 0f;

        // ==================== Animator Hash（懒加载） ====================

        protected int _animHash;
        public virtual int AnimHash
        {
            get
            {
                if (_animHash == 0)
                    _animHash = Animator.StringToHash(AnimName);
                return _animHash;
            }
        }

        // ==================== 窗口状态查询 ====================

        /// <summary>当前是否处于连招输入窗口内</summary>
        public bool IsInComboWindow(ComboContext ctx)
        {
            float t = ctx.NormalizedTime;
            return t >= ComboWindowStart && t <= ComboWindowEnd;
        }

        /// <summary>当前是否处于取消窗口内</summary>
        public bool IsInCancelWindow(ComboContext ctx)
        {
            return CanBeCancelled && ctx.NormalizedTime >= CancelWindowStart;
        }

        // ==================== 生命周期（子类重写） ====================

        /// <summary>进入招式。播放动画、初始化伤害判定等。</summary>
        public abstract void OnEnter(ComboContext ctx);

        /// <summary>每帧更新。检查输入、请求切换、管理 HitBox 时机。</summary>
        public abstract void OnUpdate(ComboContext ctx);

        /// <summary>退出招式。清理 HitBox、重置状态。</summary>
        public abstract void OnExit(ComboContext ctx);

        // ==================== 切换控制 ====================

        /// <summary>定义允许转入的招式类型。默认允许全部。</summary>
        public virtual bool CanTransitionTo(System.Type nextActionType) => true;

        /// <summary>当前资源和规则是否允许进入该招式。</summary>
        public virtual bool CanEnter(ComboContext ctx) => true;

        /// <summary>当前是否允许闪避取消。</summary>
        public virtual bool CanDodgeCancel(ComboContext ctx) =>
            CanBeCancelled && IsInCancelWindow(ctx);

        /// <summary>
        /// 请求切换到另一个招式。
        /// 在子类的 OnUpdate 中调用，ComboManager 在当帧末尾执行切换。
        /// </summary>
        protected void RequestTransition<T>(ComboContext ctx) where T : ComboAction, new()
        {
            ctx.Manager.RequestTransition<T>();
        }

        /// <summary>请求返回空闲状态</summary>
        protected void RequestIdle(ComboContext ctx)
        {
            ctx.Manager.RequestIdle();
        }

        // ==================== HitBox 辅助 ====================

        private bool _hitboxActive;
        private bool _loggedEnter;

        /// <summary>根据归一化时间自动管理 HitBox 开关。当 Animation Event 已接管时跳过。</summary>
        protected void UpdateHitboxAuto(ComboContext ctx)
        {
            if (ctx.HitboxControlledByAnimEvent) return;

            float t = ctx.NormalizedTime;

            if (!_loggedEnter)
            {
                _loggedEnter = true;
                Debug.Log($"[{GetType().Name}] Entered | AnimState={ctx.Animator.GetCurrentAnimatorStateInfo(0).IsName(AnimName)} | NormalizedTime={t:F3} | Window=[{HitboxStart}, {HitboxEnd}]");
            }

            if (!_hitboxActive && t >= HitboxStart && t <= HitboxEnd)
            {
                _hitboxActive = true;
                ctx.EnableHitBoxes(Damage);
                Debug.Log($"[{GetType().Name}] HitBox ON  | t={t:F3} | Damage={Damage}");
            }
            else if (_hitboxActive && (t > HitboxEnd || t < HitboxStart))
            {
                _hitboxActive = false;
                ctx.DisableHitBoxes();
                Debug.Log($"[{GetType().Name}] HitBox OFF | t={t:F3}");
            }
        }

        // ==================== 动画与位移辅助 ====================

        /// <summary>播放动画</summary>
        protected void PlayAnim(ComboContext ctx, float crossFadeTime = 0.05f)
        {
            ctx.Animator.CrossFadeInFixedTime(AnimHash, crossFadeTime, 0);
        }

        /// <summary>检查动画是否播放完毕，是则自动返回空闲</summary>
        protected bool CheckAnimEnd(ComboContext ctx)
        {
            if (ctx.NormalizedTime >= 0.98f || ctx.ActionElapsed >= AnimLength * 1.2f)
            {
                RequestIdle(ctx);
                return true;
            }
            return false;
        }

        /// <summary>当前 Animator 正在播放本招式动画</summary>
        protected bool IsCurrentAnim(ComboContext ctx)
        {
            return ctx.Animator.GetCurrentAnimatorStateInfo(0).IsName(AnimName);
        }

        /// <summary>应用前方位移（在 OnUpdate 中调用）</summary>
        protected void ApplyForwardMovement(ComboContext ctx)
        {
            if (ForwardSpeed <= 0f || ctx.Controller == null) return;
            Vector3 move = ctx.Transform.forward * (ForwardSpeed * Time.deltaTime);
            ctx.Controller.Move(move);
        }

        /// <summary>重置内部状态（由 ComboManager 在 OnEnter 之前调用）</summary>
        public virtual void ResetState(bool isReentry)
        {
            _hitboxActive = false;
            _loggedEnter = false;
        }

        /// <summary>连招可最早退出的归一化时间（0~1），子类可重写。默认 0 = 不限制。</summary>
        public virtual float MinChainExitTime => 0f;

        /// <summary>请求切换到指定招式（非泛型版本，供子类在运行时决定目标类型）</summary>
        protected void RequestTransition(ComboContext ctx, System.Type nextType)
        {
            ctx.Manager.RequestTransition(nextType);
        }
    }
}
