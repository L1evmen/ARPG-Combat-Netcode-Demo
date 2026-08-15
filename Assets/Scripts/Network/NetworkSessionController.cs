using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace ARPG.Networking
{
    [DisallowMultipleComponent]
    public sealed class NetworkSessionController : MonoBehaviour
    {
        private const int MaxPlayers = 2;

        [SerializeField] private NetworkManager _networkManager;
        [SerializeField] private UnityTransport _transport;
        [SerializeField] private ushort _port = 7777;
        [SerializeField] private Vector3[] _spawnPositions = new Vector3[MaxPlayers];

        public static NetworkSessionController Instance { get; private set; }
        public event Action<string> StatusChanged;

        public NetworkManager Manager => _networkManager;

        public void Configure(
            NetworkManager networkManager,
            UnityTransport transport,
            Vector3[] spawnPositions)
        {
            _networkManager = networkManager;
            _transport = transport;
            _spawnPositions = spawnPositions;
        }

        private void Awake()
        {
            Instance = this;
            Application.runInBackground = true;

            if (_networkManager == null)
                _networkManager = GetComponent<NetworkManager>();
            if (_transport == null)
                _transport = GetComponent<UnityTransport>();

            _networkManager.ConnectionApprovalCallback = ApproveConnection;
            _networkManager.OnServerStarted += OnServerStarted;
            _networkManager.OnClientConnectedCallback += OnClientConnected;
            _networkManager.OnClientDisconnectCallback += OnClientDisconnected;
        }

        private void Start()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i < arguments.Length; i++)
            {
                if (arguments[i] == "-host")
                {
                    StartHost();
                    return;
                }

                if (arguments[i] == "-client")
                {
                    string address = i + 1 < arguments.Length ? arguments[i + 1] : "127.0.0.1";
                    StartClient(address);
                    return;
                }
            }
        }

        private void OnDestroy()
        {
            if (_networkManager != null)
            {
                _networkManager.OnServerStarted -= OnServerStarted;
                _networkManager.OnClientConnectedCallback -= OnClientConnected;
                _networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            }

            if (Instance == this)
                Instance = null;
        }

        public bool StartHost()
        {
            if (_networkManager.IsListening) return false;
            _transport.SetConnectionData("127.0.0.1", _port, "0.0.0.0");
            SetStatus("正在启动 Host…");
            return _networkManager.StartHost();
        }

        public bool StartClient(string address)
        {
            if (_networkManager.IsListening) return false;
            _transport.SetConnectionData(string.IsNullOrWhiteSpace(address) ? "127.0.0.1" : address.Trim(), _port);
            SetStatus($"正在连接 {address}:{_port}…");
            return _networkManager.StartClient();
        }

        public void Shutdown()
        {
            if (!_networkManager.IsListening) return;
            _networkManager.Shutdown();
            SetStatus("已断开");
        }

        public Vector3 GetSpawnPosition(ulong clientId)
        {
            if (_spawnPositions == null || _spawnPositions.Length == 0)
                return Vector3.zero;

            int index = Mathf.Min((int)clientId, _spawnPositions.Length - 1);
            return _spawnPositions[index];
        }

        private void ApproveConnection(
            NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            response.Approved = _networkManager.ConnectedClients.Count < MaxPlayers;
            response.CreatePlayerObject = response.Approved;
            response.Pending = false;
            response.Reason = response.Approved ? string.Empty : "房间已满";
        }

        private void OnServerStarted()
        {
            SetStatus("Host 已启动，等待 Client");
        }

        private void OnClientConnected(ulong clientId)
        {
            if (clientId != _networkManager.LocalClientId) return;
            SetStatus(_networkManager.IsHost ? "Host 已连接" : "Client 已连接");
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (clientId == _networkManager.LocalClientId)
                SetStatus("连接已断开");
        }

        private void SetStatus(string status)
        {
            Debug.Log($"[Network] {status}");
            StatusChanged?.Invoke(status);
        }
    }
}
