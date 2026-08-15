using System;
using CombatV2.Actions;
using UnityEngine;

namespace CombatV2
{
    /// <summary>
    /// 可连段的招式中间基类 —— 提供模板化的 OnEnter/OnUpdate/OnExit。
    ///
    /// 子类只需提供配置属性（动画名、时长、伤害、下一段类型等），无需重复编写连招逻辑。
    ///
    /// 核心改进：连招输入在到达 MinChainExitTime 后立即触发转场，
    /// 不再等待 NormalizedTime >= 0.98，消除了动作僵硬/停顿问题。
    ///
    /// OnUpdate 优先级顺序：
    /// 1. OnChainCustomUpdate（子类自定义取消，如 RisingSlash）
    /// 2. 连招输入（在 Combo 窗口内接受，>= MinChainExitTime 立即转场，否则排队）
    /// 3. 排队连招（到达 MinChainExitTime 时触发）
    /// 4. 兜底 CheckAnimEnd（无排队时动画结束回 Idle）
    /// </summary>
    public abstract class ChainComboAction : ComboAction
    {
        // ==================== 子类必须实现 ====================

        /// <summary>连招链中的下一段招式类型</summary>
        public abstract Type NextActionType { get; }

        /// <summary>在连招链中的索引（0 起始），用于 FireAttackStarted 事件</summary>
        protected abstract int ChainIndex { get; }

        // ==================== 子类可重写的默认值 ====================

        /// <summary>连招可最早退出的归一化时间。到达此时间后，若已有输入则立即转场。</summary>
        public override float MinChainExitTime => 0.9f;

        /// <summary>连招输入窗口起始（归一化时间）。比基类更宽，支持早期输入缓冲。</summary>
        public override float ComboWindowStart => 0.20f;

        /// <summary>连招输入窗口结束（归一化时间）。比基类更宽，覆盖到动画末尾。</summary>
        public override float ComboWindowEnd => 1.00f;

        /// <summary>进入招式时的 CrossFade 时间（秒）</summary>
        protected virtual float EnterCrossFade => 0.15f;

        /// <summary>是否接受连招输入（默认 true）</summary>
        protected virtual bool CanChainFromInput => true;

        public override bool CanBeCancelled => true;

        public override bool CanEnter(ComboContext ctx) =>
            ctx.PlayerProperty != null && ctx.PlayerProperty.mentalValue >= 5;

        public override bool CanDodgeCancel(ComboContext ctx) =>
            ctx.HitboxWindowOpened && !ctx.HitboxActive;

        // ==================== 自定义钩子 ====================

        /// <summary>
        /// 在 OnUpdate 开头调用，优先级最高。
        /// 重写此方法以添加取消逻辑（如 RisingSlash 取消等）。
        /// 调用 RequestTransition 或 RequestTransition&lt;T&gt; 即可触发转场。
        /// </summary>
        protected virtual void OnChainCustomUpdate(ComboContext ctx) { }

        // ==================== 模板方法（sealed 防止子类破坏） ====================

        private bool _chainQueued;

        public sealed override void OnEnter(ComboContext ctx)
        {
            PlayAnim(ctx, EnterCrossFade);

            // 每段轻击消耗耐力
            ctx.PlayerProperty.ConsumeStamina(5);
        }

        public sealed override void OnUpdate(ComboContext ctx)
        {
            UpdateHitboxAuto(ctx);
            ApplyForwardMovement(ctx);

            // 1. 自定义逻辑（RisingSlash 取消等）—— 最高优先级
            OnChainCustomUpdate(ctx);
            if (ctx.Manager.HasPendingRequest) return;

            // 2. 连招输入：在 Combo 窗口内接受，根据 NormalizedTime 决定立即转场或排队
            if (CanChainFromInput && IsInComboWindow(ctx) && ctx.HasBufferedLight())
            {
                ctx.ConsumeLight();
                if (ctx.NormalizedTime >= MinChainExitTime)
                {
                    // 已过最早退出点 —— 立即转场
                    RequestTransition(ctx, NextActionType);
                    return;
                }
                // 未到退出点 —— 排队，到达 MinChainExitTime 时触发
                _chainQueued = true;
            }

            // 3. 排队连招：到达退出点时触发
            if (_chainQueued && ctx.NormalizedTime >= MinChainExitTime)
            {
                _chainQueued = false;
                RequestTransition(ctx, NextActionType);
                return;
            }

            // 4. 兜底：无排队，动画结束 → 回 Idle
            if (!_chainQueued)
                CheckAnimEnd(ctx);
        }

        public sealed override void OnExit(ComboContext ctx)
        {
            ctx.DisableHitBoxes();
        }

        public override void ResetState(bool isReentry)
        {
            base.ResetState(isReentry);
            _chainQueued = false;
        }
    }
}
