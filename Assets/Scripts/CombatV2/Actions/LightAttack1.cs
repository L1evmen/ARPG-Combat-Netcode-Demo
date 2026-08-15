namespace CombatV2.Actions
{
    /// <summary>
    /// 轻击第一段 —— 快速起手，可衔接第二段或蓄力重击。
    /// </summary>
    public class LightAttack1 : ChainComboAction
    {
        public override string AnimName => "LightAttack1";
        public override float AnimLength => 0.6f;
        public override int Damage => 15;

        public override float HitboxStart => 1f;
        public override float HitboxEnd => 0f;

        public override System.Type NextActionType => typeof(LightAttack2);
        protected override int ChainIndex => 0;

        public override bool CanTransitionTo(System.Type t) =>
            t == typeof(LightAttack2) || t == typeof(ChargeHeavy) ||
            t == typeof(DodgeAction);
    }
}
