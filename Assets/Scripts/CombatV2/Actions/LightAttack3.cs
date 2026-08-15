namespace CombatV2.Actions
{
    /// <summary>
    /// 轻击第三段 —— 伤害较高，可取消发动升龙斩。
    /// </summary>
    public class LightAttack3 : ChainComboAction
    {
        public override string AnimName => "LightAttack3";
        public override float AnimLength => 0.85f;
        public override int Damage => 30;

        public override float HitboxStart => 1f;
        public override float HitboxEnd => 0f;

        public override System.Type NextActionType => typeof(LightAttack4);
        protected override int ChainIndex => 2;

        public override bool CanBeCancelled => true;
        public override float CancelWindowStart => 0.55f;

        public override bool CanTransitionTo(System.Type t) =>
            t == typeof(LightAttack4) || t == typeof(ChargeHeavy) ||
            t == typeof(RisingSlash) || t == typeof(DodgeAction);

        protected override void OnChainCustomUpdate(ComboContext ctx)
        {
            if (IsInCancelWindow(ctx) && ctx.SpecialPressed
                && (SkillManager.Instance == null || !SkillManager.Instance.IsOnCooldown("RisingSlash")))
                RequestTransition<RisingSlash>(ctx);
        }
    }
}
