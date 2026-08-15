using UnityEngine;

namespace CombatV2.Actions
{
    /// <summary>
    /// 升龙斩 —— 重击+上方向发动，将敌人挑飞至空中，标记为浮空状态。
    /// 发动后自动进入可衔接空中连招的状态。
    /// </summary>
    public class RisingSlash : ComboAction
    {
        public override string AnimName => "RisingSlash";
        public override float AnimLength => 0.9f;
        public override int Damage => 25;

        public override float ComboWindowStart => 0.20f;
        public override float ComboWindowEnd => 1.00f;
        public override float HitboxStart => 1f;
        public override float HitboxEnd => 0f;

        public override bool CanTransitionTo(System.Type nextActionType)
        {
            return nextActionType == typeof(AirCombo) ||
                   nextActionType == typeof(GroundSlam);
        }

        public override bool CanEnter(ComboContext ctx)
        {
            if (SkillManager.Instance == null || ctx.PlayerProperty == null)
                return false;

            const string skillId = "RisingSlash";
            return SkillManager.Instance.IsUnlocked(skillId) &&
                   !SkillManager.Instance.IsOnCooldown(skillId) &&
                   ctx.PlayerProperty.energyValue >= SkillManager.Instance.GetManaCost(skillId);
        }

        public override void OnEnter(ComboContext ctx)
        {
            PlayAnim(ctx, 0.05f);

            // 扣减法力（从 SkillManager 读取配置）
            int manaCost = SkillManager.Instance != null
                ? SkillManager.Instance.GetManaCost("RisingSlash") : 20;
            ctx.PlayerProperty.ConsumeEnergy(manaCost);

            // 触发冷却
            CombatEvents.FireSkillUsed("RisingSlash");
            SkillManager.Instance?.StartCooldown("RisingSlash");

            // 允许动画根运动驱动 Y 轴上升
            if (ctx.PlayerCtrl != null)
                ctx.PlayerCtrl.UseRootMotionY = true;

            // 阻止 Animator AnyState 误触发 Jump
            ctx.Animator.SetBool("IsAirborneAction", true);

            ctx.Manager.IsEnemyAirborne = true;
        }

        public override void OnUpdate(ComboContext ctx)
        {
            UpdateHitboxAuto(ctx);

            // 连招窗口内轻击 → 空中连招
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
            // Y 轴控制权交给后续招式或地面重力
            if (ctx.PlayerCtrl != null)
                ctx.PlayerCtrl.UseRootMotionY = false;
            ctx.Animator.SetBool("IsAirborneAction", false);
        }
    }
}
