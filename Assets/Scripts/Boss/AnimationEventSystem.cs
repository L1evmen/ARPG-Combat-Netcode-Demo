using System;
using UnityEngine;

/// <summary>
/// 动画事件系统 —— 基于归一化时间触发事件，解决只读动画 clip 无法添加 AnimationEvent 的问题。
///
/// 在 Inspector 中配置事件时间点，运行时由 ComboState 每帧检测并触发。
/// 每个事件只触发一次（动画循环时重置）。
/// </summary>
[Serializable]
public struct AnimEvent
{
    [Tooltip("触发时间（归一化 0~1）")]
    public float normalizedTime;
    [Tooltip("事件名称（用于区分不同事件）")]
    public string eventName;
}

/// <summary>
/// 附加在 Boss 上，存储每个动画的事件配置。
/// ComboState 在 Enter 时读取对应动画的事件列表，Update 中检测触发。
/// </summary>
public class AnimationEventSystem : MonoBehaviour
{
    [Serializable]
    public class AnimEventGroup
    {
        [Tooltip("动画状态名（如 Combo1、Combo2、Combo3）")]
        public string animName;
        [Tooltip("该动画的事件列表")]
        public AnimEvent[] events;
    }

    [Tooltip("每个动画的事件配置")]
    [SerializeField] AnimEventGroup[] _eventGroups;

    /// <summary>获取指定动画的事件列表，未配置则返回空数组</summary>
    public AnimEvent[] GetEvents(string animName)
    {
        if (_eventGroups == null) return Array.Empty<AnimEvent>();
        foreach (var group in _eventGroups)
            if (group.animName == animName)
                return group.events ?? Array.Empty<AnimEvent>();
        return Array.Empty<AnimEvent>();
    }
}
