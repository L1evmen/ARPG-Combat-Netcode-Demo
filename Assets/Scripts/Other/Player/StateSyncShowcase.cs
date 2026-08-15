using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ARPG.StateSync
{
    /// <summary>
    /// F8 portfolio showcase: creates a visual replica and simulates latency,
    /// jitter and packet loss without requiring a networking package.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StateSyncShowcase : MonoBehaviour
    {
        private struct PendingPacket
        {
            public double DeliveryTime;
            public CharacterStateSnapshot Snapshot;
        }

        [SerializeField] private Animator _animator;
        [SerializeField] private bool _startEnabled;
        [SerializeField] private bool _showOverlay = true;
        [SerializeField] private Vector3 _ghostOffset = new Vector3(3f, 0f, 0f);

        [Header("Simulated Network")]
        [SerializeField, Min(1f)] private float _sendRate = 20f;
        [SerializeField, Min(0f)] private float _latencyMs = 100f;
        [SerializeField, Min(0f)] private float _jitterMs = 25f;
        [SerializeField, Range(0f, 0.5f)] private float _packetLoss = 0.03f;
        [SerializeField, Min(0f)] private float _interpolationDelay = 0.1f;
        [SerializeField, Min(0f)] private float _maxExtrapolation = 0.12f;

        private readonly List<PendingPacket> _pendingPackets = new List<PendingPacket>();
        private readonly System.Random _random = new System.Random(2026);
        private CharacterStateSynchronizer _source;
        private CharacterStateSynchronizer _replica;
        private GameObject _ghost;
        private bool _showcaseEnabled;
        private int _sentPackets;
        private int _droppedPackets;
        private int _deliveredPackets;

        private void Awake()
        {
            if (_animator == null)
                _animator = GetComponent<Animator>();
        }

        private void Start()
        {
            if (_startEnabled)
                EnableShowcase();
        }

        private void Update()
        {
            if (Keyboard.current?.f8Key.wasPressedThisFrame == true)
            {
                if (_showcaseEnabled) DisableShowcase();
                else EnableShowcase();
            }

            if (!_showcaseEnabled || _replica == null) return;

            double now = Time.unscaledTimeAsDouble;
            while (_pendingPackets.Count > 0 && _pendingPackets[0].DeliveryTime <= now)
            {
                _replica.PushSnapshot(_pendingPackets[0].Snapshot);
                _pendingPackets.RemoveAt(0);
                _deliveredPackets++;
            }
        }

        private void OnDestroy()
        {
            DisableShowcase();
        }

        private void EnableShowcase()
        {
            if (_showcaseEnabled || _animator == null || !_animator.isHuman) return;

            _source = GetComponent<CharacterStateSynchronizer>();
            if (_source == null)
                _source = gameObject.AddComponent<CharacterStateSynchronizer>();
            _source.ConfigureAuthority(_animator, _sendRate);

            _ghost = CreateGhost();
            Animator ghostAnimator = _ghost.GetComponent<Animator>();
            _replica = _ghost.AddComponent<CharacterStateSynchronizer>();
            _replica.ConfigureReplica(ghostAnimator, _ghostOffset, _interpolationDelay, _maxExtrapolation);
            _ghost.AddComponent<PlayerProceduralIK>();

            _pendingPackets.Clear();
            _sentPackets = 0;
            _droppedPackets = 0;
            _deliveredPackets = 0;
            _source.SnapshotProduced += SimulateSend;
            _showcaseEnabled = true;
        }

        private void DisableShowcase()
        {
            if (_source != null)
                _source.SnapshotProduced -= SimulateSend;

            _pendingPackets.Clear();
            if (_ghost != null)
                Destroy(_ghost);

            _ghost = null;
            _replica = null;
            _showcaseEnabled = false;
        }

        private GameObject CreateGhost()
        {
            GameObject ghost = PlayerReplicaFactory.Create(
                transform,
                _animator,
                "State Sync Ghost (F8)",
                new Color(0.2f, 0.85f, 1f, 1f));
            ghost.transform.position += _ghostOffset;
            return ghost;
        }

        private void SimulateSend(CharacterStateSnapshot snapshot)
        {
            _sentPackets++;
            if (_random.NextDouble() < _packetLoss)
            {
                _droppedPackets++;
                return;
            }

            double jitter = (_random.NextDouble() * 2d - 1d) * _jitterMs;
            PendingPacket packet = new PendingPacket
            {
                DeliveryTime = Time.unscaledTimeAsDouble + Math.Max(0d, (_latencyMs + jitter) / 1000d),
                Snapshot = snapshot
            };

            int insertIndex = _pendingPackets.Count;
            while (insertIndex > 0 && _pendingPackets[insertIndex - 1].DeliveryTime > packet.DeliveryTime)
                insertIndex--;
            _pendingPackets.Insert(insertIndex, packet);
        }

        private void OnGUI()
        {
            if (!_showOverlay) return;

            const float width = 310f;
            float height = _showcaseEnabled ? 118f : 42f;
            Rect rect = new Rect(Screen.width - width - 18f, 18f, width, height);
            string status = _showcaseEnabled
                ? $"F8  State Sync: ON\n20 Hz | {_latencyMs:0} ms ± {_jitterMs:0} ms | loss {_packetLoss:P0}\n" +
                  $"sent {_sentPackets}  dropped {_droppedPackets}  delivered {_deliveredPackets}\n" +
                  $"buffer {_replica?.BufferedSnapshotCount ?? 0}  error {(_replica?.LastPositionError ?? 0f):0.000} m"
                : "F8  State Sync Showcase";
            GUI.Box(rect, status);
        }
    }
}
