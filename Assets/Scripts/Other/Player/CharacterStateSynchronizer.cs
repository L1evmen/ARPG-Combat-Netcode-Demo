using System;
using System.Collections.Generic;
using CombatV2;
using UnityEngine;

namespace ARPG.StateSync
{
    /// <summary>
    /// Captures authoritative snapshots or renders a buffered remote replica.
    /// A real network adapter only needs to forward SnapshotProduced to PushSnapshot.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterStateSynchronizer : MonoBehaviour
    {
        public enum SyncRole
        {
            Authority,
            Replica
        }

        private const int MaxBufferedSnapshots = 32;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int InputXHash = Animator.StringToHash("inputX");
        private static readonly int InputYHash = Animator.StringToHash("inputY");
        private static readonly int JumpingHash = Animator.StringToHash("isJump");
        private static readonly int JumpSpeedHash = Animator.StringToHash("jumpSpeed");
        private static readonly int HoldItemIdHash = Animator.StringToHash("HoldItemId");
        private static readonly int AttackingHash = Animator.StringToHash("IsAttacking");
        private static readonly int DodgingHash = Animator.StringToHash("IsDodging");
        private static readonly int AirborneActionHash = Animator.StringToHash("IsAirborneAction");

        [SerializeField] private SyncRole _role = SyncRole.Authority;
        [SerializeField] private Animator _animator;

        [Header("Authority")]
        [SerializeField, Min(1f)] private float _sendRate = 20f;

        [Header("Replica")]
        [SerializeField, Min(0f)] private float _interpolationDelay = 0.1f;
        [SerializeField, Min(0f)] private float _maxExtrapolation = 0.12f;
        [SerializeField, Min(0.01f)] private float _teleportThreshold = 3f;
        [SerializeField, Min(0f)] private float _positionSharpness = 24f;
        [SerializeField, Min(0f)] private float _rotationSharpness = 24f;
        [SerializeField] private Vector3 _worldOffset;

        private readonly List<CharacterStateSnapshot> _snapshots = new List<CharacterStateSnapshot>(MaxBufferedSnapshots);
        private readonly HashSet<int> _animatorParameters = new HashSet<int>();
        private PlayerController _playerController;
        private ComboManager _comboManager;
        private Func<double> _clock;
        private uint _sequence;
        private double _nextSendTime;
        private double _lastCaptureTime;
        private Vector3 _lastCapturePosition;
        private bool _hasCapture;
        private bool _hasRenderedSnapshot;
        private int _lastAnimatorStateHash;

        public event Action<CharacterStateSnapshot> SnapshotProduced;

        public SyncRole Role => _role;
        public int BufferedSnapshotCount => _snapshots.Count;
        public float LastPositionError { get; private set; }

        private void Awake()
        {
            CacheReferences();
        }

        private void OnEnable()
        {
            _nextSendTime = Now;
        }

        private void Update()
        {
            if (_role == SyncRole.Authority)
                UpdateAuthority();
            else
                UpdateReplica();
        }

        public void ConfigureAuthority(Animator animator, float sendRate)
        {
            _role = SyncRole.Authority;
            _animator = animator;
            _sendRate = Mathf.Max(1f, sendRate);
            ResetRuntimeState();
            CacheReferences();
        }

        public void ConfigureClock(Func<double> clock)
        {
            _clock = clock;
            _nextSendTime = Now;
        }

        public void ConfigureReplica(
            Animator animator,
            Vector3 worldOffset,
            float interpolationDelay,
            float maxExtrapolation)
        {
            _role = SyncRole.Replica;
            _animator = animator;
            _worldOffset = worldOffset;
            _interpolationDelay = Mathf.Max(0f, interpolationDelay);
            _maxExtrapolation = Mathf.Max(0f, maxExtrapolation);
            ResetRuntimeState();
            CacheReferences();
        }

        /// <summary>Call on Unity's main thread after a packet is decoded.</summary>
        public void PushSnapshot(CharacterStateSnapshot snapshot)
        {
            if (_role != SyncRole.Replica) return;

            int insertIndex = _snapshots.Count;
            for (int i = _snapshots.Count - 1; i >= 0; i--)
            {
                if (_snapshots[i].Sequence == snapshot.Sequence)
                    return;

                if (_snapshots[i].Timestamp <= snapshot.Timestamp)
                    break;

                insertIndex = i;
            }

            _snapshots.Insert(insertIndex, snapshot);
            if (_snapshots.Count > MaxBufferedSnapshots)
                _snapshots.RemoveAt(0);
        }

        public void ClearSnapshots()
        {
            _snapshots.Clear();
            _hasRenderedSnapshot = false;
            _lastAnimatorStateHash = 0;
        }

        private void UpdateAuthority()
        {
            double now = Now;
            if (now < _nextSendTime) return;

            _nextSendTime = now + 1d / Mathf.Max(1f, _sendRate);
            CharacterStateSnapshot snapshot = CaptureSnapshot(now);
            SnapshotProduced?.Invoke(snapshot);
        }

        private CharacterStateSnapshot CaptureSnapshot(double now)
        {
            Vector3 velocity = Vector3.zero;
            if (_hasCapture)
            {
                double elapsed = now - _lastCaptureTime;
                if (elapsed > double.Epsilon)
                    velocity = (transform.position - _lastCapturePosition) / (float)elapsed;
            }

            _lastCapturePosition = transform.position;
            _lastCaptureTime = now;
            _hasCapture = true;

            AnimatorStateInfo state = default;
            if (_animator != null)
            {
                state = _animator.IsInTransition(0)
                    ? _animator.GetNextAnimatorStateInfo(0)
                    : _animator.GetCurrentAnimatorStateInfo(0);
            }

            CharacterStateSnapshot.StateFlags flags = CharacterStateSnapshot.StateFlags.None;
            if (_playerController != null && _playerController.isGrounded)
                flags |= CharacterStateSnapshot.StateFlags.Grounded;
            if (GetBool(JumpingHash))
                flags |= CharacterStateSnapshot.StateFlags.Jumping;
            if (GetBool(AttackingHash) || _comboManager != null && _comboManager.IsInAction)
                flags |= CharacterStateSnapshot.StateFlags.Attacking;
            if (GetBool(DodgingHash))
                flags |= CharacterStateSnapshot.StateFlags.Dodging;
            if (GetBool(AirborneActionHash))
                flags |= CharacterStateSnapshot.StateFlags.AirborneAction;

            return new CharacterStateSnapshot
            {
                Sequence = ++_sequence,
                Timestamp = now,
                Position = transform.position,
                Rotation = transform.rotation,
                Velocity = velocity,
                AnimatorStateHash = state.fullPathHash,
                AnimatorNormalizedTime = state.normalizedTime,
                Speed = GetFloat(SpeedHash),
                InputX = GetFloat(InputXHash),
                InputY = GetFloat(InputYHash),
                JumpSpeed = GetFloat(JumpSpeedHash),
                HoldItemId = GetInt(HoldItemIdHash),
                EquipmentId = GetEquipmentId(),
                Flags = flags
            };
        }

        private void UpdateReplica()
        {
            if (_snapshots.Count == 0) return;

            double renderTime = Now - _interpolationDelay;
            while (_snapshots.Count >= 3 && _snapshots[1].Timestamp <= renderTime)
                _snapshots.RemoveAt(0);

            CharacterStateSnapshot rendered;
            if (renderTime <= _snapshots[0].Timestamp || _snapshots.Count == 1)
            {
                rendered = _snapshots[0];
            }
            else if (renderTime >= _snapshots[_snapshots.Count - 1].Timestamp)
            {
                rendered = CharacterStateSnapshot.Extrapolate(
                    _snapshots[_snapshots.Count - 1], renderTime, _maxExtrapolation);
            }
            else
            {
                rendered = CharacterStateSnapshot.Interpolate(_snapshots[0], _snapshots[1], renderTime);
            }

            ApplySnapshot(rendered);
        }

        private void ApplySnapshot(CharacterStateSnapshot snapshot)
        {
            Vector3 targetPosition = snapshot.Position + _worldOffset;
            LastPositionError = Vector3.Distance(transform.position, targetPosition);

            if (!_hasRenderedSnapshot || LastPositionError >= _teleportThreshold)
            {
                transform.SetPositionAndRotation(targetPosition, snapshot.Rotation);
                _hasRenderedSnapshot = true;
            }
            else
            {
                float positionT = DampFactor(_positionSharpness);
                float rotationT = DampFactor(_rotationSharpness);
                transform.position = Vector3.Lerp(transform.position, targetPosition, positionT);
                transform.rotation = Quaternion.Slerp(transform.rotation, snapshot.Rotation, rotationT);
            }

            ApplyAnimatorSnapshot(snapshot);
        }

        private void ApplyAnimatorSnapshot(CharacterStateSnapshot snapshot)
        {
            if (_animator == null) return;

            if (snapshot.AnimatorStateHash != 0 && snapshot.AnimatorStateHash != _lastAnimatorStateHash)
            {
                _animator.Play(snapshot.AnimatorStateHash, 0, Mathf.Max(0f, snapshot.AnimatorNormalizedTime));
                _lastAnimatorStateHash = snapshot.AnimatorStateHash;
            }

            SetFloat(SpeedHash, snapshot.Speed);
            SetFloat(InputXHash, snapshot.InputX);
            SetFloat(InputYHash, snapshot.InputY);
            SetFloat(JumpSpeedHash, snapshot.JumpSpeed);
            SetInt(HoldItemIdHash, snapshot.HoldItemId);
            SetBool(JumpingHash, snapshot.HasFlag(CharacterStateSnapshot.StateFlags.Jumping));
            SetBool(AttackingHash, snapshot.HasFlag(CharacterStateSnapshot.StateFlags.Attacking));
            SetBool(DodgingHash, snapshot.HasFlag(CharacterStateSnapshot.StateFlags.Dodging));
            SetBool(AirborneActionHash, snapshot.HasFlag(CharacterStateSnapshot.StateFlags.AirborneAction));
        }

        private void CacheReferences()
        {
            if (_animator == null)
                _animator = GetComponent<Animator>();

            _playerController = GetComponent<PlayerController>();
            _comboManager = GetComponent<ComboManager>();
            _animatorParameters.Clear();
            if (_animator == null) return;

            AnimatorControllerParameter[] parameters = _animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
                _animatorParameters.Add(parameters[i].nameHash);
        }

        private void ResetRuntimeState()
        {
            _snapshots.Clear();
            _sequence = 0;
            _nextSendTime = Now;
            _hasCapture = false;
            _hasRenderedSnapshot = false;
            _lastAnimatorStateHash = 0;
        }

        private float DampFactor(float sharpness)
        {
            return sharpness <= 0f ? 1f : 1f - Mathf.Exp(-sharpness * Time.unscaledDeltaTime);
        }

        private float GetFloat(int hash) =>
            _animator != null && _animatorParameters.Contains(hash) ? _animator.GetFloat(hash) : 0f;

        private int GetInt(int hash) =>
            _animator != null && _animatorParameters.Contains(hash) ? _animator.GetInteger(hash) : 0;

        private bool GetBool(int hash) =>
            _animator != null && _animatorParameters.Contains(hash) && _animator.GetBool(hash);

        private double Now => _clock != null ? _clock() : Time.unscaledTimeAsDouble;

        private byte GetEquipmentId()
        {
            if (_playerController == null || WeaponManager.Instance == null)
                return 0;

            ItemSO item = WeaponManager.Instance.GetCurrent();
            if (item == null || item.prefab == null)
                return 0;

            Weapon weapon = item.prefab.GetComponent<Weapon>();
            if (weapon is ScytheWeapon) return 2;
            if (weapon is JavelinWeapon) return 3;
            return weapon != null || item.prefab.GetComponent<WeaponBase>() != null ? (byte)1 : (byte)0;
        }

        private void SetFloat(int hash, float value)
        {
            if (_animatorParameters.Contains(hash)) _animator.SetFloat(hash, value);
        }

        private void SetInt(int hash, int value)
        {
            if (_animatorParameters.Contains(hash)) _animator.SetInteger(hash, value);
        }

        private void SetBool(int hash, bool value)
        {
            if (_animatorParameters.Contains(hash)) _animator.SetBool(hash, value);
        }
    }
}
