using UnityEngine;

namespace CombatV2
{
    /// <summary>
    /// 玩家动画事件处理器 —— 接收 Unity Animation Event，统一管理 HitBox / 音效 / 特效。
    ///
    /// 使用方式：
    /// 1. 挂在玩家 GameObject 上（由 ComboManager 自动添加）。
    /// 2. 在 Animation 窗口中对动画 clip 添加 AnimationEvent：
    ///    - Function: OnAnimEvent
    ///    - String Parameter: "HitBoxOn" | "HitBoxOff" | "SFX:key" | "VFX:key"
    ///
    /// 与 UpdateHitboxAuto 的关系：
    /// - 当动画 clip 配置了 HitBoxOn/Off 事件时，本组件接管控制，UpdateHitboxAuto 自动跳过。
    /// - 未配置事件的动画仍由 UpdateHitboxAuto 按 HitboxStart/End 兜底。
    /// </summary>
    public class PlayerAnimEventHandler : MonoBehaviour
    {
        private ComboManager _comboManager;

        public void Init(ComboManager manager)
        {
            _comboManager = manager;
        }

        /// <summary>
        /// Unity Animation Event 回调。
        /// 动画 clip 中配置的事件调用此方法，data 格式见类注释。
        /// </summary>
        public void OnAnimEvent(string data)
        {
            if (string.IsNullOrEmpty(data) || _comboManager == null) return;

            if (data == "HitBoxOn")
                EnableHitBoxes();
            else if (data == "HitBoxOff")
                DisableHitBoxes();
            else if (data.StartsWith("SFX:"))
                PlaySFX(data.Substring(4));
            else if (data.StartsWith("VFX:"))
                PlayVFX(data.Substring(4));
        }

        private void EnableHitBoxes()
        {
            if (_comboManager.CurrentAction == null) return;

            var ctx = _comboManager.GetContext();
            if (ctx == null) return;

            int damage = _comboManager.CurrentAction.Damage;
            ctx.HitboxControlledByAnimEvent = true;
            ctx.EnableHitBoxes(damage);
        }

        private void DisableHitBoxes()
        {
            var ctx = _comboManager.GetContext();
            if (ctx == null) return;
            ctx.DisableHitBoxes();
        }

        private void PlaySFX(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            AudioManager.Instance?.PlaySFX(key, transform.position);
            _comboManager.GetContext().SfxControlledByAnimEvent = true;
        }

        private void PlayVFX(string key)
        {
            // TODO: 特效系统预留
            Debug.Log($"[PlayerAnimEventHandler] VFX: {key}");
        }
    }
}
