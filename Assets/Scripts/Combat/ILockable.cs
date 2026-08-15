using UnityEngine;

/// <summary>
/// 可被锁定的实体需实现此接口。
/// 挂载在敌人/Boss的碰撞体所在GameObject上。
/// </summary>
public interface ILockable
{
    /// <summary>锁定点的世界坐标（通常为胸口/头部位置）</summary>
    Vector3 LockPoint { get; }
    /// <summary>是否仍可被锁定（死亡/脱战返回false）</summary>
    bool CanBeLocked { get; }
}
