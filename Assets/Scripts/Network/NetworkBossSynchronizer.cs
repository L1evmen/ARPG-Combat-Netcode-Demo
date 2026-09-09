using ARPG.StateSync;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace ARPG.Networking
{
    [DisallowMultipleComponent]
    public sealed class NetworkBossSynchronizer : NetworkBehaviour
    {
        private const float SendRate = 20f;

        private NetworkVariable<int> _health = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private NetworkVariable<int> _blockStamina = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private NetworkVariable<bool> _active = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private NetworkVariable<bool> _dead = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private BossController _boss;
        private Animator _animator;
        private NavMeshAgent _agent;
        private CharacterStateSynchronizer _synchronizer;
        private HitBox[] _hitBoxes;
        private bool _partyDefeated;

        public static NetworkBossSynchronizer Instance { get; private set; }
        public bool CanReceiveDamage => IsServer && _boss.IsActivated && !_boss.IsDead;

        private void Awake()
        {
            Instance = this;
            _boss = GetComponent<BossController>();
            _animator = GetComponent<Animator>();
            _agent = GetComponent<NavMeshAgent>();
            _hitBoxes = GetComponentsInChildren<HitBox>(true);
            _synchronizer = GetComponent<CharacterStateSynchronizer>();
            if (_synchronizer == null)
                _synchronizer = gameObject.AddComponent<CharacterStateSynchronizer>();

            _synchronizer.enabled = false;
            _boss.enabled = false;
            _agent.enabled = false;
            DisableHitBoxes();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public override void OnNetworkSpawn()
        {
            Debug.Log($"[NetworkBoss] Spawn server={IsServer}");
            _health.OnValueChanged += OnStateChanged;
            _blockStamina.OnValueChanged += OnStateChanged;
            _active.OnValueChanged += OnStateChanged;
            _dead.OnValueChanged += OnStateChanged;

            if (IsServer)
            {
                _agent.enabled = true;
                _boss.enabled = true;
                _health.Value = _boss.CurrentHP;
                _blockStamina.Value = _boss.BlockStamina;
                _active.Value = _boss.IsActivated;
                _dead.Value = _boss.IsDead;
                _synchronizer.ConfigureAuthority(_animator, SendRate);
                _synchronizer.ConfigureClock(NetworkTime);
                _synchronizer.SnapshotProduced += SendSnapshot;
            }
            else
            {
                _boss.enabled = false;
                _agent.enabled = false;
                DisableHitBoxes();
                _synchronizer.ConfigureReplica(_animator, Vector3.zero, 0.1f, 0.12f);
                _synchronizer.ConfigureClock(NetworkTime);
            }

            _synchronizer.enabled = true;
            ApplyReplicaState();
        }

        public override void OnNetworkDespawn()
        {
            _health.OnValueChanged -= OnStateChanged;
            _blockStamina.OnValueChanged -= OnStateChanged;
            _active.OnValueChanged -= OnStateChanged;
            _dead.OnValueChanged -= OnStateChanged;
            _synchronizer.SnapshotProduced -= SendSnapshot;
            _synchronizer.enabled = false;
            _boss.enabled = false;
            _agent.enabled = false;
            DisableHitBoxes();
        }

        private void LateUpdate()
        {
            if (!IsServer || !IsSpawned) return;

            if (_health.Value != _boss.CurrentHP)
                _health.Value = _boss.CurrentHP;
            if (_blockStamina.Value != _boss.BlockStamina)
                _blockStamina.Value = _boss.BlockStamina;
            if (_active.Value != _boss.IsActivated)
                _active.Value = _boss.IsActivated;
            if (_dead.Value != _boss.IsDead)
                _dead.Value = _boss.IsDead;
        }

        public void ServerApplyDamage(int damage, Transform source)
        {
            if (!CanReceiveDamage) return;
            _boss.TakeDamage(damage, source);
        }

        public void ServerRefreshPartyState()
        {
            if (!IsServer || !IsSpawned || _boss.IsDead) return;

            bool defeated = !CombatTargetRegistry.TryGetClosest(transform.position, out _);
            if (defeated == _partyDefeated) return;

            _partyDefeated = defeated;
            ReceivePartyStateClientRpc(defeated);
        }

        [ClientRpc]
        private void ReceivePartyStateClientRpc(bool defeated)
        {
            AudioManager.Instance?.GetComponent<CombatMusicController>()?.SetPartyDefeated(defeated);
        }

        private void SendSnapshot(CharacterStateSnapshot snapshot)
        {
            snapshot.Timestamp = NetworkTime();
            ReceiveSnapshotClientRpc(snapshot);
        }

        [ClientRpc(Delivery = RpcDelivery.Unreliable)]
        private void ReceiveSnapshotClientRpc(CharacterStateSnapshot snapshot)
        {
            if (IsServer) return;
            _synchronizer.PushSnapshot(snapshot);
        }

        private void OnStateChanged(int previous, int current)
        {
            ApplyReplicaState();
        }

        private void OnStateChanged(bool previous, bool current)
        {
            ApplyReplicaState();
        }

        private void ApplyReplicaState()
        {
            if (IsServer) return;
            _boss.ApplyReplicaState(_health.Value, _blockStamina.Value, _active.Value, _dead.Value);
        }

        private void DisableHitBoxes()
        {
            for (int i = 0; i < _hitBoxes.Length; i++)
                _hitBoxes[i].GetComponent<Collider>().enabled = false;
        }

        private double NetworkTime()
        {
            return NetworkManager.ServerTime.Time;
        }
    }
}
