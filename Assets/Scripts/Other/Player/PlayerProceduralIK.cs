using CombatV2.LockOn;
using UnityEngine;

/// <summary>
/// Humanoid animation post-process: grounded foot placement, pelvis correction,
/// and a weighted look-at target while locked on.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public sealed class PlayerProceduralIK : MonoBehaviour
{
    private static readonly int JumpingHash = Animator.StringToHash("isJump");

    [SerializeField] private Animator _animator;
    [SerializeField] private PlayerController _playerController;
    [SerializeField] private InputController _inputController;
    [SerializeField] private TargetLockManager _lockManager;

    [Header("Foot IK")]
    [SerializeField] private LayerMask _groundLayers = -1;
    [SerializeField, Min(0f)] private float _rayStartHeight = 0.45f;
    [SerializeField, Min(0f)] private float _rayDistance = 0.9f;
    [SerializeField, Min(0f)] private float _soleOffset = 0.035f;
    [SerializeField, Range(0f, 60f)] private float _maxFootAngle = 45f;
    [SerializeField, Min(0f)] private float _weightSpeed = 8f;
    [SerializeField, Min(0f)] private float _footSharpness = 20f;

    [Header("Pelvis")]
    [SerializeField, Min(0f)] private float _maxPelvisDrop = 0.35f;
    [SerializeField, Min(0f)] private float _maxPelvisRise = 0.1f;
    [SerializeField, Min(0f)] private float _pelvisSharpness = 12f;

    [Header("Lock-on Look IK")]
    [SerializeField, Range(0f, 1f)] private float _lookBodyWeight = 0.25f;
    [SerializeField, Range(0f, 1f)] private float _lookHeadWeight = 0.75f;
    [SerializeField, Range(0f, 1f)] private float _lookEyesWeight = 0.6f;
    [SerializeField, Range(0f, 1f)] private float _lookClampWeight = 0.55f;
    [SerializeField, Min(0f)] private float _lookSharpness = 12f;

    private Transform _leftFoot;
    private Transform _rightFoot;
    private bool _hasJumpingParameter;
    private float _footWeight;
    private float _pelvisOffset;
    private float _lookWeight;
    private Vector3 _leftPosition;
    private Vector3 _rightPosition;
    private Quaternion _leftRotation;
    private Quaternion _rightRotation;
    private Vector3 _lookPosition;
    private bool _leftInitialized;
    private bool _rightInitialized;
    private bool _lookInitialized;

    private void Awake()
    {
        CacheReferences();
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (layerIndex != 0 || _animator == null || !_animator.isHuman) return;

        bool footIKAllowed = IsFootIKAllowed();
        float targetWeight = footIKAllowed ? 1f : 0f;
        _footWeight = Mathf.MoveTowards(_footWeight, targetWeight, _weightSpeed * Time.deltaTime);

        ApplyFootIK();
        ApplyLookIK();
    }

    private void ApplyFootIK()
    {
        bool leftHit = TrySolveFoot(
            AvatarIKGoal.LeftFoot, _leftFoot, ref _leftPosition, ref _leftRotation, ref _leftInitialized);
        bool rightHit = TrySolveFoot(
            AvatarIKGoal.RightFoot, _rightFoot, ref _rightPosition, ref _rightRotation, ref _rightInitialized);

        float targetPelvisOffset = CalculatePelvisOffset(leftHit, rightHit);
        _pelvisOffset = Mathf.Lerp(_pelvisOffset, targetPelvisOffset, DampFactor(_pelvisSharpness));
        _animator.bodyPosition += Vector3.up * (_pelvisOffset * _footWeight);

        ApplyFootGoal(AvatarIKGoal.LeftFoot, leftHit, _leftPosition, _leftRotation);
        ApplyFootGoal(AvatarIKGoal.RightFoot, rightHit, _rightPosition, _rightRotation);
    }

    private bool TrySolveFoot(
        AvatarIKGoal goal,
        Transform foot,
        ref Vector3 smoothedPosition,
        ref Quaternion smoothedRotation,
        ref bool initialized)
    {
        if (foot == null) return false;

        Vector3 origin = foot.position + Vector3.up * _rayStartHeight;
        float distance = _rayStartHeight + _rayDistance;
        if (!Physics.Raycast(
                origin,
                Vector3.down,
                out RaycastHit hit,
                distance,
                _groundLayers,
                QueryTriggerInteraction.Ignore))
            return false;

        Vector3 normal = hit.normal;
        float slopeAngle = Vector3.Angle(Vector3.up, normal);
        if (slopeAngle > _maxFootAngle && slopeAngle > 0.001f)
            normal = Vector3.Slerp(Vector3.up, normal, _maxFootAngle / slopeAngle);

        Vector3 targetPosition = hit.point + normal * _soleOffset;
        Quaternion animationRotation = _animator.GetIKRotation(goal);
        Quaternion targetRotation = Quaternion.FromToRotation(Vector3.up, normal) * animationRotation;

        if (!initialized)
        {
            smoothedPosition = targetPosition;
            smoothedRotation = targetRotation;
            initialized = true;
        }
        else
        {
            float t = DampFactor(_footSharpness);
            smoothedPosition = Vector3.Lerp(smoothedPosition, targetPosition, t);
            smoothedRotation = Quaternion.Slerp(smoothedRotation, targetRotation, t);
        }

        return true;
    }

    private float CalculatePelvisOffset(bool leftHit, bool rightHit)
    {
        if (!leftHit && !rightHit) return 0f;

        float leftOffset = leftHit && _leftFoot != null ? _leftPosition.y - _leftFoot.position.y : float.MaxValue;
        float rightOffset = rightHit && _rightFoot != null ? _rightPosition.y - _rightFoot.position.y : float.MaxValue;
        float offset = Mathf.Min(leftOffset, rightOffset);
        return Mathf.Clamp(offset, -_maxPelvisDrop, _maxPelvisRise);
    }

    private void ApplyFootGoal(AvatarIKGoal goal, bool hasGround, Vector3 position, Quaternion rotation)
    {
        float weight = hasGround ? _footWeight : 0f;
        _animator.SetIKPositionWeight(goal, weight);
        _animator.SetIKRotationWeight(goal, weight);
        if (!hasGround) return;

        _animator.SetIKPosition(goal, position);
        _animator.SetIKRotation(goal, rotation);
    }

    private void ApplyLookIK()
    {
        Transform target = _lockManager != null ? _lockManager.LockedTarget : null;
        float targetWeight = target != null ? 1f : 0f;
        _lookWeight = Mathf.Lerp(_lookWeight, targetWeight, DampFactor(_lookSharpness));

        if (target != null)
        {
            Vector3 targetPosition = _lockManager.CurrentLockable != null
                ? _lockManager.CurrentLockable.LockPoint
                : target.position + Vector3.up;

            if (!_lookInitialized)
            {
                _lookPosition = targetPosition;
                _lookInitialized = true;
            }
            else
            {
                _lookPosition = Vector3.Lerp(_lookPosition, targetPosition, DampFactor(_lookSharpness));
            }
        }

        _animator.SetLookAtWeight(
            _lookWeight,
            _lookBodyWeight,
            _lookHeadWeight,
            _lookEyesWeight,
            _lookClampWeight);
        if (_lookInitialized)
            _animator.SetLookAtPosition(_lookPosition);
    }

    private bool IsFootIKAllowed()
    {
        if (_playerController != null && !_playerController.isGrounded)
            return false;
        if (_inputController != null && _inputController.MovementBlocked)
            return false;
        if (_playerController == null && _hasJumpingParameter && _animator.GetBool(JumpingHash))
            return false;
        return true;
    }

    private void CacheReferences()
    {
        if (_animator == null) _animator = GetComponent<Animator>();
        if (_playerController == null) _playerController = GetComponent<PlayerController>();
        if (_inputController == null) _inputController = GetComponent<InputController>();
        if (_lockManager == null) _lockManager = GetComponent<TargetLockManager>();

        if (_animator == null || !_animator.isHuman) return;
        _leftFoot = _animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        _rightFoot = _animator.GetBoneTransform(HumanBodyBones.RightFoot);

        AnimatorControllerParameter[] parameters = _animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].nameHash == JumpingHash)
            {
                _hasJumpingParameter = true;
                break;
            }
        }
    }

    private static float DampFactor(float sharpness)
    {
        return sharpness <= 0f ? 1f : 1f - Mathf.Exp(-sharpness * Time.deltaTime);
    }
}
