using UnityEngine;

/// <summary>
/// 攻击判定碰撞体 —— 挂载在敌人子物体 AttackHitbox 上。
/// 当 EnemyController 通过 Animation Event 开启判定窗口后，
/// 此碰撞体被激活；玩家进入 Trigger 时回调父级 EnemyController 造成伤害。
///
/// 使用方式：
///   1. 在敌人 Prefab 下创建子 GameObject "AttackHitbox"
///   2. 添加 Collider（IsTrigger = true）
///   3. 挂载此脚本
///   4. 父级挂载 EnemyController
/// </summary>
[RequireComponent(typeof(Collider))]
public class AttackHitbox : MonoBehaviour
{
    private EnemyController _controller;
    private Collider _collider;

    void Awake()
    {
        _collider = GetComponent<Collider>();
        _collider.isTrigger = true;
        _collider.enabled = false; // 默认关闭，由 Animation Event 开启

        // 向上查找父级 EnemyController
        _controller = GetComponentInParent<EnemyController>();
        if (_controller == null)
        {
            Debug.LogError($"[AttackHitbox] 未在父级找到 EnemyController！请确保 {name} 是敌人的子物体。", this);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (_controller != null)
        {
            _controller.OnAttackHitboxTrigger(other);
        }
    }
}
