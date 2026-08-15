using UnityEngine;

namespace CombatV2.Actions
{
    /// <summary>
    /// 空中连招 —— 敌人浮空时按轻击发动，可在空中持续连击。
    /// 玩家会自动升空追踪敌人位置。
    /// </summary>
    public class AirCombo : ComboAction
    {
        private string _activeAnimName = "AirCombo1";

        public override string AnimName => _activeAnimName;
        public override float AnimLength => _activeAnimName == "AirCombo3" ? 0.85f : 0.55f;
        public override int Damage => 12;

        public override float ComboWindowStart => 0.20f;
        public override float ComboWindowEnd => 1.00f;
        public override float HitboxStart => 1f;
        public override float HitboxEnd => 0f;

        public override bool CanBeCancelled => true;
        public override float CancelWindowStart => 0.30f;

        /// <summary>空中连招循环计数（用于动画变化）</summary>
        private int _loopCount;

        public override bool CanTransitionTo(System.Type nextActionType)
        {
            return nextActionType == typeof(AirCombo) ||
                   nextActionType == typeof(GroundSlam) ||
                   nextActionType == typeof(DodgeAction);
        }

        public override void OnEnter(ComboContext ctx)
        {
            if (ctx.PlayerCtrl != null)
                ctx.PlayerCtrl.UseRootMotionY = true;

            // 阻止 Animator AnyState 误触发 Jump
            ctx.Animator.SetBool("IsAirborneAction", true);

            // 根据循环次数播放不同动画（最多3种变化）
            _activeAnimName = _loopCount % 3 == 0 ? "AirCombo1"
                            : _loopCount % 3 == 1 ? "AirCombo2"
                            : "AirCombo3";
            _animHash = 0;
            PlayAnim(ctx, 0.05f);

            _loopCount++;
        }

        public override void OnUpdate(ComboContext ctx)
        {
            UpdateHitboxAuto(ctx);

            // 连招窗口内轻击 → 继续空中连招（自身循环）
            if (IsInComboWindow(ctx) && ctx.HasBufferedLight())
            {
                ctx.ConsumeLight();
                RequestTransition<AirCombo>(ctx);
                return;
            }

            // 连招窗口内重击 → 裂地劈
            if (IsInComboWindow(ctx) && ctx.HeavyReleased)
            {
                RequestTransition<GroundSlam>(ctx);
                return;
            }

            CheckAnimEnd(ctx);
        }

        public override void OnExit(ComboContext ctx)
        {
            ctx.DisableHitBoxes();
            if (ctx.PlayerCtrl != null)
                ctx.PlayerCtrl.UseRootMotionY = false;
            ctx.Animator.SetBool("IsAirborneAction", false);
        }

        public override void ResetState(bool isReentry)
        {
            base.ResetState(isReentry);
            if (!isReentry)
                _loopCount = 0;
        }
    }
}
