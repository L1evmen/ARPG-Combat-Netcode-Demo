namespace CombatV2.Actions
{
    /// <summary>
    /// 轻击第五段（终结段）—— 最高伤害，可取消发动升龙斩，循环回第一段。
    /// </summary>
    public class LightAttack5 : ChainComboAction
    {
        public override string AnimName => "LightAttack5";
        public override float AnimLength => 0.9f;
        public override int Damage => 35;

        public override float HitboxStart => 1f;
        public override float HitboxEnd => 0f;

        public override System.Type NextActionType => typeof(LightAttack1); // 循环连段
        protected override int ChainIndex => 4;

        public override bool CanBeCancelled => true;
        public override float CancelWindowStart => 0.55f;

        public override bool CanTransitionTo(System.Type t) =>
            t == typeof(LightAttack1) || t == typeof(ChargeHeavy) ||
            t == typeof(RisingSlash) || t == typeof(DodgeAction);

        protected override void OnChainCustomUpdate(ComboContext ctx)
        {
            if (IsInCancelWindow(ctx) && ctx.SpecialPressed
                && (SkillManager.Instance == null || !SkillManager.Instance.IsOnCooldown("RisingSlash")))
                RequestTransition<RisingSlash>(ctx);
        }
    }
}
