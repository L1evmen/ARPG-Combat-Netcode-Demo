namespace CombatV2.Actions
{
    /// <summary>
    /// 轻击第二段 —— 比第一段稍重，可衔接第三段或蓄力重击。
    /// </summary>
    public class LightAttack2 : ChainComboAction
    {
        public override string AnimName => "LightAttack2";
        public override float AnimLength => 0.65f;
        public override int Damage => 20;

        public override float HitboxStart => 1f;
        public override float HitboxEnd => 0f;

        public override System.Type NextActionType => typeof(LightAttack3);
        protected override int ChainIndex => 1;

        public override bool CanTransitionTo(System.Type t) =>
            t == typeof(LightAttack3) || t == typeof(ChargeHeavy) ||
            t == typeof(DodgeAction);
    }
}
