using UnityEngine;

/// <summary>
/// 武器抽象基类 —— 所有武器（近战 / 远程 / 特殊）继承此类。
///
/// CombatV2 系统依赖：
///   - ComboManager 持有 WeaponBase 引用，用于获取 HitBox 和基础攻击力。
///   - ComboAction 的 UpdateHitboxAuto 通过 ctx.EnableHitBoxes(Damage) 间接调用武器 HitBox。
///   - MeleeWeapon 是近战武器的具体实现。
/// </summary>
public abstract class WeaponBase : MonoBehaviour
{
    [Header("武器基础属性")]
    [SerializeField] protected int _baseAttackValue = 10;

    [Header("握持")]
    public Vector3 GripPosition = Vector3.zero;
    public Vector3 GripRotation = Vector3.zero;
    public Vector3 GripScale = Vector3.one;

    [Header("动画")]
    [SerializeField] protected int _animLayerIndex;

    public int BaseAttackValue => _baseAttackValue;
    public int AnimLayerIndex => _animLayerIndex;

    /// <summary>当前武器是否正在攻击中。</summary>
    public bool IsAttacking { get; protected set; }

    /// <summary>启用武器上所有 HitBox。</summary>
    public abstract void EnableHitBoxes(int damage);

    /// <summary>停用武器上所有 HitBox。</summary>
    public abstract void DisableHitBoxes();
}
