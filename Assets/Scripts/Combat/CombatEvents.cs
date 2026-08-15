using System;
using UnityEngine;

/// <summary>
/// 战斗事件中心 —— 所有战斗相关事件在此定义。
///
/// 为什么用 static event 而非 UnityEvent：
/// 1. 零 GC 分配 —— UnityEvent 会在 Inspector 序列化时产生装箱。
/// 2. 编译期类型安全 —— 每个事件的参数在编译期确定，不会被 Inspector 拖拽破坏。
/// 3. 解耦 —— 发射方和接收方不需要互相引用。
/// 4. 便于在代码中全局搜索引用 —— "Find All References" 可以定位所有订阅者。
///
/// 没有做成单例 —— static class 本身就是全局状态，不需要 MonoBehaviour 承载。
/// 如果未来需要在多个场景中使用不同的事件总线，再引入实例化的 EventBus。
/// </summary>
public static class CombatEvents
{
    /// <summary>攻击动作开始播放（参数：连招段数索引或蓄力等级）</summary>
    public static event Action<int> OnAttackStarted;

    /// <summary>连招输入窗口开启</summary>
    public static event Action OnComboWindowOpen;

    /// <summary>连招输入窗口关闭</summary>
    public static event Action OnComboWindowClose;

    /// <summary>连招成功衔接下一段</summary>
    public static event Action<int> OnComboChained;

    /// <summary>连招中断（超时 / 被打断）</summary>
    public static event Action OnComboReset;

    /// <summary>武器命中有效目标（参数：被命中者 Transform，伤害值）</summary>
    public static event Action<Transform, int> OnAttackHit;

    /// <summary>角色进入战斗状态</summary>
    public static event Action OnEnterCombat;

    /// <summary>角色退出战斗状态</summary>
    public static event Action OnExitCombat;

    // --- 蓄力事件 ---

    /// <summary>蓄力开始（参数：总等级数）</summary>
    public static event Action<int> OnChargeStarted;

    /// <summary>蓄力进度更新（参数：当前等级索引，总等级数，归一化进度 0~1）</summary>
    public static event Action<int, int, float> OnChargeProgress;

    /// <summary>蓄力结束（释放或取消）</summary>
    public static event Action OnChargeEnded;

    // --- 技能解锁事件 ---

    /// <summary>技能解锁（参数：技能 ID）</summary>
    public static event Action<string> OnSkillUnlocked;

    /// <summary>技能使用（参数：技能 ID，用于触发冷却）</summary>
    public static event Action<string> OnSkillUsed;

    // --- 静态调用方法 ---

    public static void FireAttackStarted(int comboIndex)
        => OnAttackStarted?.Invoke(comboIndex);

    public static void FireComboWindowOpen()
        => OnComboWindowOpen?.Invoke();

    public static void FireComboWindowClose()
        => OnComboWindowClose?.Invoke();

    public static void FireComboChained(int comboIndex)
        => OnComboChained?.Invoke(comboIndex);

    public static void FireComboReset()
        => OnComboReset?.Invoke();

    public static void FireAttackHit(Transform target, int damage)
        => OnAttackHit?.Invoke(target, damage);

    public static void FireEnterCombat()
        => OnEnterCombat?.Invoke();

    public static void FireExitCombat()
        => OnExitCombat?.Invoke();

    public static void FireChargeStarted(int totalLevels)
        => OnChargeStarted?.Invoke(totalLevels);

    public static void FireChargeProgress(int levelIndex, int totalLevels, float normalizedProgress)
        => OnChargeProgress?.Invoke(levelIndex, totalLevels, normalizedProgress);

    public static void FireChargeEnded()
        => OnChargeEnded?.Invoke();

    public static void FireSkillUnlocked(string skillId)
        => OnSkillUnlocked?.Invoke(skillId);

    public static void FireSkillUsed(string skillId)
        => OnSkillUsed?.Invoke(skillId);
}
