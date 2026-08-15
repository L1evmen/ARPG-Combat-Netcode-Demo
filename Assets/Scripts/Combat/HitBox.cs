using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 武器碰撞检测组件 —— 挂载在武器预制体的碰撞器上。
///
/// 核心设计：
/// 1. 由 ComboAction 调用 Enable() / Disable() 控制激活时机。
/// 2. 使用 HashSet 记录每次挥砍已命中的目标，避免同一目标重复受击。
/// 3. 通过 IDamageable 接口检测目标，不依赖具体的 Enemy / Player 类型。
/// 4. 支持多个碰撞器（如剑身 + 剑尖），每个碰撞器挂一个 HitBox。
///
/// 性能考虑：
/// - HashSet.Clear() 在每次攻击开始时调用，O(n) 但 n 通常 < 5，影响可忽略。
/// - 不使用 UnityEvent 回调（避免 GC），直接通过 CombatEvents 静态事件通知。
/// </summary>
[RequireComponent(typeof(Collider))]
public class HitBox : MonoBehaviour
{
    [SerializeField, Tooltip("伤害倍率（可用于不同部位的碰撞器，如剑身 1.0，剑尖 1.5）")]
    private float _damageMultiplier = 1.0f;

    [SerializeField, Tooltip("是否在攻击开始时自动加入已命中列表（如队友碰撞器需要始终忽略）")]
    private bool _ignoreOwner = true;

    private Collider _collider;
    private readonly HashSet<IDamageable> _hitTargets = new HashSet<IDamageable>();
    private int _currentDamage;

    private void Awake()
    {
        _collider = GetComponent<Collider>();
        _collider.isTrigger = true;
        _collider.enabled = false;
    }

    /// <summary>激活 HitBox，开始检测。</summary>
    public void Enable(int baseDamage)
    {
        _currentDamage = Mathf.RoundToInt(baseDamage * _damageMultiplier);
        _hitTargets.Clear();
        _collider.enabled = true;
        Debug.Log($"[HitBox] Enable | obj={gameObject.name} | dmg={_currentDamage} | collider.enabled={_collider.enabled} | collider.isTrigger={_collider.isTrigger}");
    }

    /// <summary>停用 HitBox，停止检测。</summary>
    public void Disable()
    {
        _collider.enabled = false;
    }

    /// <summary>清空命中记录，允许再次命中同一目标。</summary>
    public void ClearHitTargets()
    {
        _hitTargets.Clear();
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[HitBox] OnTriggerEnter | hit={other.name} | root={other.transform.root.name}");

        if (_ignoreOwner && IsOwnerCollider(other))
        {
            Debug.Log($"[HitBox]   → skipped (owner)");
            return;
        }

        if (!other.TryGetComponent<IDamageable>(out var damageable))
            damageable = other.GetComponentInParent<IDamageable>();
        if (damageable == null)
        {
            Debug.Log($"[HitBox]   → skipped (no IDamageable on {other.name})");
            return;
        }

        if (!damageable.CanBeHit())
        {
            Debug.Log($"[HitBox]   → skipped (CanBeHit=false)");
            return;
        }

        if (_hitTargets.Contains(damageable))
        {
            Debug.Log($"[HitBox]   → skipped (already hit)");
            return;
        }

        _hitTargets.Add(damageable);
        bool routed = CombatHitRouter.TryRoute(
            damageable,
            _currentDamage,
            transform.root);
        if (!routed)
            damageable.TakeDamage(_currentDamage, transform.root);

        Debug.Log($"[HitBox]   → DAMAGE {(routed ? "ROUTED" : "DEALT")} | dmg={_currentDamage} | to={other.name}");
        CombatEvents.FireAttackHit(other.transform, _currentDamage);
    }

    private bool IsOwnerCollider(Collider other)
    {
        // 检查碰撞体是否属于武器持有者（玩家自身）
        return other.transform.IsChildOf(transform.root) ||
               other.attachedRigidbody != null &&
               other.attachedRigidbody.transform.IsChildOf(transform.root);
    }
}
