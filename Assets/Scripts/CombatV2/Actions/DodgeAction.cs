using UnityEngine;

namespace CombatV2.Actions
{
    /// <summary>
    /// 闪避 —— 按 Shift 触发。
    /// 方向直接读取 Animator 当前的 inputX/inputY（由 PlayerController 在移动状态中持续写入），
    /// 无需自己做坐标转换或死区处理。
    /// 闪避期间 MovementBlocked=true，PlayerController 停止覆写 inputX/Y，方向值保持稳定。
    /// </summary>
    public class DodgeAction : ComboAction
    {
        public override string AnimName => "Dodge";
        public override float AnimLength => 0.5f;
        public override int Damage => 0;

        public override float HitboxStart => 1f;
        public override float HitboxEnd => 0f;

        public override bool CanBeCancelled => false;

        private static readonly int ParamInputX = Animator.StringToHash("inputX");
        private static readonly int ParamInputY = Animator.StringToHash("inputY");
        private static readonly int ParamIsDodging = Animator.StringToHash("IsDodging");

        public override bool CanTransitionTo(System.Type nextActionType) => false;

        public override void OnEnter(ComboContext ctx)
        {
            // 直接读 Animator 当前的 inputX/Y ——
            // 这是 PlayerController 在移动状态中写入的本地空间方向值，已正确反映角色朝向与输入
            float dx = ctx.Animator.GetFloat(ParamInputX);
            float dy = ctx.Animator.GetFloat(ParamInputY);

            // 无输入时给默认方向
            if (Mathf.Abs(dx) < 0.1f && Mathf.Abs(dy) < 0.1f)
            {
                dx = 0f;
                dy = -1f; // 默认后闪
            }

            // 瞬时写入，不用 damping（闪避需要立刻切换到目标姿态）
            ctx.Animator.SetFloat(ParamInputX, dx);
            ctx.Animator.SetFloat(ParamInputY, dy);
            ctx.Animator.SetBool(ParamIsDodging, true);
            PlayAnim(ctx, 0f);
        }

        public override void OnUpdate(ComboContext ctx)
        {
            CheckAnimEnd(ctx);
        }

        public override void OnExit(ComboContext ctx)
        {
            ctx.Animator.SetBool(ParamIsDodging, false);
        }
    }
}
