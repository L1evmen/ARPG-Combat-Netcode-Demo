using UnityEngine;

namespace CombatV2.Actions
{
    /// <summary>
    /// 裂地劈 —— 将浮空敌人重重劈向地面，造成高额伤害并解除浮空状态。
    /// 发动后返回地面连招节奏。
    /// </summary>
    public class GroundSlam : ComboAction
    {
        public override string AnimName => "GroundSlam";
        public override float AnimLength => 1.0f;
        public override int Damage => 35;

        public override float ComboWindowStart => 0.20f;
        public override float ComboWindowEnd => 1.00f;
        public override float HitboxStart => 1f;
        public override float HitboxEnd => 0f;

        public override bool CanBeCancelled => true;
        public override float CancelWindowStart => 0.65f;

        public override bool CanTransitionTo(System.Type nextActionType)
        {
            return nextActionType == typeof(LightAttack1) ||
                   nextActionType == typeof(ChargeHeavy) ||
                   nextActionType == typeof(DodgeAction);
        }

        public override void OnEnter(ComboContext ctx)
        {
            PlayAnim(ctx, 0.05f);

            // 裂地劈期间保留动画根运动（包括下落 Y 轴）
            if (ctx.PlayerCtrl != null)
                ctx.PlayerCtrl.UseRootMotionY = true;

            // 阻止 Animator AnyState 误触发 Jump
            ctx.Animator.SetBool("IsAirborneAction", true);

            ctx.Manager.IsEnemyAirborne = false;
        }

        public override void OnUpdate(ComboContext ctx)
        {
            UpdateHitboxAuto(ctx);

            // 取消窗口内轻击 → 重新开始地面连段
            if (IsInCancelWindow(ctx) && ctx.HasBufferedLight())
            {
                ctx.ConsumeLight();
                RequestTransition<LightAttack1>(ctx);
                return;
            }

            // 取消窗口内重击（蓄力释放）→ 蓄力重击
            if (IsInCancelWindow(ctx) && ctx.HeavyReleased)
            {
                RequestTransition<ChargeHeavy>(ctx);
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
    }
}
