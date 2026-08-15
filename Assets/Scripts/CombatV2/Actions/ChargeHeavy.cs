using UnityEngine;

namespace CombatV2.Actions
{
    /// <summary>
    /// 蓄力重击 —— 按住重击键蓄力后松开发动。
    /// 蓄力时长决定等级（L1/L2/L3），不同等级对应不同的动画与伤害倍率。
    /// </summary>
    public class ChargeHeavy : ComboAction
    {
        private int _activeChargeLevel;

        public override string AnimName => _activeChargeLevel >= 2
            ? "ChargeHeavy_L3"
            : _activeChargeLevel >= 1
                ? "ChargeHeavy_L2"
                : "ChargeHeavy_L1";

        public override float AnimLength => 1.1f;

        public override int Damage
        {
            get
            {
                int[] dmg = { 40, 60, 80 };
                return dmg[Mathf.Clamp(_activeChargeLevel, 0, dmg.Length - 1)];
            }
        }

        public override float ComboWindowStart => 0.20f;
        public override float ComboWindowEnd => 1.00f;
        public override float HitboxStart => 1f;
        public override float HitboxEnd => 0f;

        public override bool CanBeCancelled => true;
        public override float CancelWindowStart => 0.60f;

        public override bool CanTransitionTo(System.Type nextActionType)
        {
            return nextActionType == typeof(LightAttack1) ||
                   nextActionType == typeof(RisingSlash) ||
                   nextActionType == typeof(DodgeAction);
        }

        public override void OnEnter(ComboContext ctx)
        {
            _activeChargeLevel = ctx.ChargeLevel;

            PlayAnim(ctx, 0.05f);
            float speed = 1f + _activeChargeLevel * 0.15f;
            ctx.Animator.SetFloat("AttackSpeed", speed);
        }

        public override void OnUpdate(ComboContext ctx)
        {
            UpdateHitboxAuto(ctx);

            if (IsInCancelWindow(ctx) && ctx.SpecialPressed
                && (SkillManager.Instance == null || !SkillManager.Instance.IsOnCooldown("RisingSlash")))
            {
                RequestTransition<RisingSlash>(ctx);
                return;
            }

            if (IsInCancelWindow(ctx) && ctx.HasBufferedLight())
            {
                ctx.ConsumeLight();
                RequestTransition<LightAttack1>(ctx);
                return;
            }

            CheckAnimEnd(ctx);
        }

        public override void OnExit(ComboContext ctx)
        {
            ctx.DisableHitBoxes();
            ctx.Animator.SetFloat("AttackSpeed", 1f);
        }

        public override void ResetState(bool isReentry)
        {
            base.ResetState(isReentry);
            _animHash = 0;
        }

        public override int AnimHash
        {
            get
            {
                if (_animHash == 0)
                    _animHash = Animator.StringToHash(AnimName);
                return _animHash;
            }
        }
    }
}
