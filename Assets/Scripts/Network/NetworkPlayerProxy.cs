using System.Collections.Generic;
using ARPG.StateSync;
using CombatV2;
using Unity.Netcode;
using UnityEngine;

namespace ARPG.Networking
{
    [DisallowMultipleComponent]
    public sealed class NetworkPlayerProxy : NetworkBehaviour, IDamageable, ICombatHitRouter, ICombatTarget
    {
        private const float SendRate = 20f;
        private const float MaxMoveSpeed = 18f;
        private const float MoveTolerance = 1.5f;
        private const float MaxBossHitDistance = 6f;
        private const float HitRequestCooldown = 0.08f;
        private const float AttackStateGrace = 0.45f;
        private const float RespawnDelay = 3f;

        [Header("生命")]
        [SerializeField] private int _maxHealth = 100;

        [Header("远端装备外观")]
        [SerializeField] private GameObject _bladePrefab;
        [SerializeField] private GameObject _scythePrefab;
        [SerializeField] private GameObject _javelinPrefab;

        private NetworkVariable<int> _health = new NetworkVariable<int>(
            100,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private NetworkVariable<bool> _alive = new NetworkVariable<bool>(
            true,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private Collider _serverCollider;
        private Rigidbody _serverRigidbody;
        private GameObject _localPlayer;
        private Animator _localAnimator;
        private PlayerProperty _localProperty;
        private PlayerController _localController;
        private InputController _localInput;
        private ComboManager _localCombo;
        private CharacterStateSynchronizer _sourceSynchronizer;
        private CharacterStateSynchronizer _replicaSynchronizer;
        private GameObject _replica;
        private GameObject _replicaWeapon;
        private byte _replicaEquipmentId = byte.MaxValue;
        private uint _lastSequence;
        private double _lastSnapshotServerTime;
        private double _lastAttackStateTime;
        private double _lastBossHitTime;
        private double _lastDamageTime;
        private double _respawnTime;
        private bool _hasAcceptedSnapshot;

        public Transform TargetTransform => transform;
        public bool CanBeTargeted => IsServer && _alive.Value;

        public void ConfigureEquipment(
            GameObject bladePrefab,
            GameObject scythePrefab,
            GameObject javelinPrefab)
        {
            _bladePrefab = bladePrefab;
            _scythePrefab = scythePrefab;
            _javelinPrefab = javelinPrefab;
        }

        private void Awake()
        {
            _serverCollider = GetComponent<Collider>();
            _serverRigidbody = GetComponent<Rigidbody>();
        }

        public override void OnNetworkSpawn()
        {
            Debug.Log($"[NetworkPlayer] Spawn owner={OwnerClientId} local={IsOwner} server={IsServer}");
            _health.OnValueChanged += OnHealthChanged;
            _alive.OnValueChanged += OnAliveChanged;

            if (IsServer)
            {
                transform.position = NetworkSessionController.Instance.GetSpawnPosition(OwnerClientId);
                _health.Value = _maxHealth;
                _alive.Value = true;
                _serverCollider.enabled = true;
                _serverRigidbody.detectCollisions = true;
                CombatTargetRegistry.Register(this);
            }
            else
            {
                _serverCollider.enabled = false;
                _serverRigidbody.detectCollisions = false;
            }

            if (IsOwner) SetupOwner();
            else CreateReplica();

            OnHealthChanged(_health.Value, _health.Value);
            OnAliveChanged(_alive.Value, _alive.Value);
        }

        public override void OnNetworkDespawn()
        {
            _health.OnValueChanged -= OnHealthChanged;
            _alive.OnValueChanged -= OnAliveChanged;

            if (IsServer)
                CombatTargetRegistry.Unregister(this);
            if (IsOwner)
                CombatHitRouter.Unregister(this);
            if (_sourceSynchronizer != null)
                _sourceSynchronizer.SnapshotProduced -= SendSnapshot;
            if (_replica != null)
                Destroy(_replica);
        }

        private void Update()
        {
            if (!IsServer || _alive.Value || NetworkManager.ServerTime.Time < _respawnTime)
                return;

            _hasAcceptedSnapshot = false;
            _health.Value = _maxHealth;
            _alive.Value = true;
            NetworkBossSynchronizer.Instance?.ServerRefreshPartyState();
        }

        private void SetupOwner()
        {
            _localPlayer = GameObject.FindGameObjectWithTag("Player");
            _localAnimator = _localPlayer.GetComponent<Animator>();
            _localProperty = _localPlayer.GetComponent<PlayerProperty>();
            _localController = _localPlayer.GetComponent<PlayerController>();
            _localInput = _localPlayer.GetComponent<InputController>();
            _localCombo = _localPlayer.GetComponent<ComboManager>();

            StateSyncShowcase showcase = _localPlayer.GetComponent<StateSyncShowcase>();
            if (showcase != null)
                showcase.enabled = false;

            _sourceSynchronizer = _localPlayer.GetComponent<CharacterStateSynchronizer>();
            if (_sourceSynchronizer == null)
                _sourceSynchronizer = _localPlayer.AddComponent<CharacterStateSynchronizer>();
            _sourceSynchronizer.ConfigureAuthority(_localAnimator, SendRate);
            _sourceSynchronizer.ConfigureClock(NetworkTime);
            _sourceSynchronizer.SnapshotProduced += SendSnapshot;

            SetLocalPlayerPosition(NetworkSessionController.Instance.GetSpawnPosition(OwnerClientId));
            CombatHitRouter.Register(this);
        }

        private void CreateReplica()
        {
            GameObject source = GameObject.FindGameObjectWithTag("Player");
            Animator sourceAnimator = source.GetComponent<Animator>();
            _replica = PlayerReplicaFactory.Create(
                source.transform,
                sourceAnimator,
                $"Remote Player {OwnerClientId}",
                new Color(0.45f, 0.75f, 1f, 1f));

            RemoveCopiedWeapons();

            Animator replicaAnimator = _replica.GetComponent<Animator>();
            _replicaSynchronizer = _replica.AddComponent<CharacterStateSynchronizer>();
            _replicaSynchronizer.ConfigureReplica(replicaAnimator, Vector3.zero, 0.1f, 0.12f);
            _replicaSynchronizer.ConfigureClock(NetworkTime);
            _replica.AddComponent<PlayerProceduralIK>();
        }

        private void SendSnapshot(CharacterStateSnapshot snapshot)
        {
            snapshot.Timestamp = NetworkTime();
            SubmitSnapshotServerRpc(snapshot);
        }

        [ServerRpc(Delivery = RpcDelivery.Unreliable)]
        private void SubmitSnapshotServerRpc(CharacterStateSnapshot snapshot)
        {
            if (!_alive.Value || !ValidateSnapshot(snapshot)) return;

            double now = NetworkManager.ServerTime.Time;
            snapshot.Timestamp = now;
            transform.SetPositionAndRotation(snapshot.Position, snapshot.Rotation);
            _lastSequence = snapshot.Sequence;
            _lastSnapshotServerTime = now;
            _hasAcceptedSnapshot = true;

            if (snapshot.HasFlag(CharacterStateSnapshot.StateFlags.Attacking))
                _lastAttackStateTime = now;

            ReceiveSnapshotClientRpc(snapshot);
        }

        [ClientRpc(Delivery = RpcDelivery.Unreliable)]
        private void ReceiveSnapshotClientRpc(CharacterStateSnapshot snapshot)
        {
            if (IsOwner || _replicaSynchronizer == null) return;
            _replicaSynchronizer.PushSnapshot(snapshot);
            ApplyReplicaEquipment(snapshot.EquipmentId);
        }

        [ServerRpc]
        private void RequestBossHitServerRpc(int damage)
        {
            NetworkBossSynchronizer boss = NetworkBossSynchronizer.Instance;
            double now = NetworkManager.ServerTime.Time;
            if (!_alive.Value || boss == null || !boss.CanReceiveDamage) return;
            if (now - _lastAttackStateTime > AttackStateGrace) return;
            if (now - _lastBossHitTime < HitRequestCooldown) return;
            if (Vector3.Distance(transform.position, boss.transform.position) > MaxBossHitDistance) return;

            _lastBossHitTime = now;
            boss.ServerApplyDamage(Mathf.Clamp(damage, 1, 80), transform);
        }

        public bool TryRouteHit(
            IDamageable target,
            int damage,
            Transform source)
        {
            if (!IsOwner || _localPlayer == null || source == null || source.root != _localPlayer.transform)
                return false;

            if (target is BossController)
            {
                if (_alive.Value)
                    RequestBossHitServerRpc(damage);
                return true;
            }

            return target is NetworkPlayerProxy;
        }

        public bool CanBeHit()
        {
            return IsServer && _alive.Value;
        }

        public void TakeDamage(int damage, Transform source)
        {
            if (!IsServer || !_alive.Value || source == null || source.GetComponent<BossController>() == null)
                return;

            double now = NetworkManager.ServerTime.Time;
            if (now - _lastDamageTime < 0.1f) return;
            _lastDamageTime = now;

            _health.Value = Mathf.Max(0, _health.Value - damage);
            if (_health.Value > 0) return;

            _alive.Value = false;
            _respawnTime = now + RespawnDelay;
            NetworkBossSynchronizer.Instance?.ServerRefreshPartyState();
        }

        private bool ValidateSnapshot(CharacterStateSnapshot snapshot)
        {
            if (_hasAcceptedSnapshot && snapshot.Sequence <= _lastSequence)
                return false;
            if (!_hasAcceptedSnapshot)
                return true;

            float elapsed = Mathf.Max(0.05f, (float)(NetworkManager.ServerTime.Time - _lastSnapshotServerTime));
            float maxDistance = MaxMoveSpeed * elapsed + MoveTolerance;
            return (snapshot.Position - transform.position).sqrMagnitude <= maxDistance * maxDistance;
        }

        private void OnHealthChanged(int previous, int current)
        {
            if (IsOwner && _localProperty != null)
                _localProperty.ApplyNetworkHealth(current, current < previous);
        }

        private void OnAliveChanged(bool previous, bool current)
        {
            if (IsOwner && _localPlayer != null)
            {
                _localController.enabled = current;
                _localInput.enabled = current;
                _localCombo.enabled = current;
                SetRenderersEnabled(_localPlayer, current);

            }
            else if (_replica != null)
            {
                SetRenderersEnabled(_replica, current);
            }
        }

        private void ApplyReplicaEquipment(byte equipmentId)
        {
            if (_replica == null || equipmentId == _replicaEquipmentId) return;
            _replicaEquipmentId = equipmentId;

            if (_replicaWeapon != null)
                Destroy(_replicaWeapon);

            GameObject prefab = equipmentId switch
            {
                1 => _bladePrefab,
                2 => _scythePrefab,
                3 => _javelinPrefab,
                _ => null
            };
            if (prefab == null) return;

            Animator animator = _replica.GetComponent<Animator>();
            Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            _replicaWeapon = Instantiate(prefab, hand);

            Weapon weapon = _replicaWeapon.GetComponent<Weapon>();
            WeaponBase weaponBase = _replicaWeapon.GetComponent<WeaponBase>();
            if (weapon != null)
            {
                _replicaWeapon.transform.localPosition = weapon.gripPosition;
                _replicaWeapon.transform.localRotation = Quaternion.Euler(weapon.gripRotation);
                _replicaWeapon.transform.localScale = weapon.gripScale;
            }
            else if (weaponBase != null)
            {
                _replicaWeapon.transform.localPosition = weaponBase.GripPosition;
                _replicaWeapon.transform.localRotation = Quaternion.Euler(weaponBase.GripRotation);
                _replicaWeapon.transform.localScale = weaponBase.GripScale;
            }

            PlayerReplicaFactory.DisableGameplayComponents(_replicaWeapon);
        }

        private void RemoveCopiedWeapons()
        {
            HashSet<GameObject> weapons = new HashSet<GameObject>();
            foreach (Weapon weapon in _replica.GetComponentsInChildren<Weapon>(true))
                weapons.Add(weapon.gameObject);
            foreach (WeaponBase weapon in _replica.GetComponentsInChildren<WeaponBase>(true))
                weapons.Add(weapon.gameObject);

            foreach (GameObject weapon in weapons)
            {
                weapon.SetActive(false);
                Destroy(weapon);
            }
        }

        private void SetLocalPlayerPosition(Vector3 position)
        {
            CharacterController controller = _localPlayer.GetComponent<CharacterController>();
            controller.enabled = false;
            _localPlayer.transform.position = position;
            controller.enabled = true;
        }

        private static void SetRenderersEnabled(GameObject root, bool value)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].enabled = value;
        }

        private double NetworkTime()
        {
            return NetworkManager.ServerTime.Time;
        }
    }
}
