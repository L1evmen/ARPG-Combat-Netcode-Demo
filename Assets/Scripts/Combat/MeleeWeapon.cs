using UnityEngine;

/// <summary>
/// 近战武器 —— WeaponBase 的具体实现。
/// 在 Awake 中自动收集子物体上的所有 HitBox，由 ComboAction 统一控制开关。
/// </summary>
public class MeleeWeapon : WeaponBase
{
    [Header("刀鞘（可选）")]
    [Tooltip("刀鞘 Prefab，拥有者装备此武器时自动生成并绑到副手")]
    [SerializeField] private GameObject _sheathPrefab;

    [Tooltip("刀鞘在副手的本地坐标")]
    [SerializeField] private Vector3 _sheathGripPos = Vector3.zero;
    [Tooltip("刀鞘在副手的本地旋转")]
    [SerializeField] private Vector3 _sheathGripRot = Vector3.zero;
    [Tooltip("刀鞘在副手的本地缩放")]
    [SerializeField] private Vector3 _sheathGripScl = Vector3.one;

    private HitBox[] _hitBoxes;

    public GameObject SheathPrefab => _sheathPrefab;
    public Vector3 SheathGripPos => _sheathGripPos;
    public Vector3 SheathGripRot => _sheathGripRot;
    public Vector3 SheathGripScl => _sheathGripScl;

    private void Awake()
    {
        _hitBoxes = GetComponentsInChildren<HitBox>(includeInactive: true);
        if (_hitBoxes.Length == 0)
        {
            Debug.LogWarning($"[MeleeWeapon] No HitBox found on {gameObject.name}");
        }
    }

    public override void EnableHitBoxes(int damage)
    {
        for (int i = 0; i < _hitBoxes.Length; i++)
        {
            _hitBoxes[i].Enable(damage);
        }
    }

    public override void DisableHitBoxes()
    {
        for (int i = 0; i < _hitBoxes.Length; i++)
        {
            _hitBoxes[i].Disable();
        }
    }
}
