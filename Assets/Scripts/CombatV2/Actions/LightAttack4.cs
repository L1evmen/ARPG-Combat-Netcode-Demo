namespace CombatV2.Actions
{
    /// <summary>
    /// 轻击第四段 —— 可衔接第五段（终结段）。
    /// </summary>
    public class LightAttack4 : ChainComboAction
    {
        public override string AnimName => "LightAttack4";
        public override float AnimLength => 0.75f;
        public override int Damage => 25;

        public override float HitboxStart => 1f;
        public override float HitboxEnd => 0f;

        public override System.Type NextActionType => typeof(LightAttack5);
        protected override int ChainIndex => 3;

        public override bool CanTransitionTo(System.Type t) =>
            t == typeof(LightAttack5) || t == typeof(ChargeHeavy) ||
            t == typeof(DodgeAction);
    }
}
