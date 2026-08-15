using UnityEngine;

/// <summary>
/// 自动存档触发器 —— 玩家进入 Trigger 区域时自动存档。
///
/// 挂载到带有 Collider(isTrigger=true) 的 GameObject 上。
/// 典型放置位置：Boss 房入口、关卡传送点、安全区域。
/// </summary>
[RequireComponent(typeof(Collider))]
public class AutoSaveTrigger : MonoBehaviour
{
    private bool _triggered;

    private void OnTriggerEnter(Collider other)
    {
        if (_triggered) return;
        if (!other.CompareTag("Player")) return;

        _triggered = true;
        SaveManager.Instance?.Save();
        Debug.Log($"[AutoSave] 触发点 {gameObject.name} 已自动存档");
    }

    /// <summary>重置触发状态（允许再次触发）</summary>
    public void ResetTrigger() => _triggered = false;
}
