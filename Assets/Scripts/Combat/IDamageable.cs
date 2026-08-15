using UnityEngine;

/// <summary>
/// 可受击接口 —— 任何需要接收伤害的实体（玩家 / 敌人 / 可破坏物）都应实现此接口。
///
/// 为什么不放基类而用接口：
/// 1. 玩家和敌人可能继承自不同的基类（Player 和 Enemy 已有各自的继承链）。
/// 2. 可破坏物（木箱 / 路障）不是角色，但需要受击逻辑。
/// 3. 接口允许 HitBox 通过 TryGetComponent 检测，无需依赖具体类型。
/// 4. 方便网络同步时 RPC 调用统一接口。
/// </summary>
public interface IDamageable
{
    /// <summary>受到伤害。</summary>
    /// <param name="damage">伤害数值</param>
    /// <param name="source">伤害来源的 Transform（用于击退方向 / 击杀统计等）</param>
    void TakeDamage(int damage, Transform source);

    /// <summary>是否可以被命中（已死亡 / 无敌帧 / 闪避中返回 false）</summary>
    bool CanBeHit();
}
