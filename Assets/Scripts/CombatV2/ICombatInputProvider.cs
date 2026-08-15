using UnityEngine;

namespace CombatV2
{
    /// <summary>
    /// 战斗输入提供者接口 —— 解耦 ComboManager 与具体硬件输入。
    ///
    /// 设计目的：
    /// 1. ComboManager 不直接依赖 Mouse.current / Keyboard.current。
    /// 2. 后续接入键位修改系统时，只需替换实现类，不改 ComboManager。
    /// 3. 方便编写单元测试（可注入模拟输入）。
    ///
    /// 使用方式：
    /// - 默认使用 DefaultCombatInputProvider（直接读取 Unity Input System）。
    /// - 键位重映射系统实现此接口，从玩家配置读取绑定。
    /// - ComboManager 在 Awake 中通过 GetComponent 获取实现。
    /// </summary>
    public interface ICombatInputProvider
    {
        /// <summary>轻击键是否在本帧按下</summary>
        bool LightPressed { get; }

        /// <summary>重击键是否在本帧按下</summary>
        bool HeavyPressed { get; }

        /// <summary>重击键是否持续按住</summary>
        bool HeavyHeld { get; }

        /// <summary>重击键是否在本帧松开</summary>
        bool HeavyReleased { get; }

        /// <summary>上方向键是否在本帧按下（W / ↑）</summary>
        bool UpPressed { get; }

        /// <summary>闪避键是否在本帧按下</summary>
        bool DodgePressed { get; }

        /// <summary>特殊攻击键（F）是否在本帧按下 —— 用于升龙斩等特殊招式</summary>
        bool SpecialPressed { get; }

        /// <summary>移动输入向量（WASD 归一化）</summary>
        Vector2 MoveInput { get; }

        /// <summary>每帧调用一次，刷新输入状态</summary>
        void UpdateInput();
    }
}
