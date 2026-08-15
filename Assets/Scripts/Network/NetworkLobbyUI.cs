using UnityEngine;
using UnityEngine.UI;

namespace ARPG.Networking
{
    [DisallowMultipleComponent]
    public sealed class NetworkLobbyUI : MonoBehaviour
    {
        [SerializeField] private NetworkSessionController _session;
        [SerializeField] private InputField _addressInput;
        [SerializeField] private Button _hostButton;
        [SerializeField] private Button _clientButton;
        [SerializeField] private Button _disconnectButton;
        [SerializeField] private Text _statusText;

        public void Configure(
            NetworkSessionController session,
            InputField addressInput,
            Button hostButton,
            Button clientButton,
            Button disconnectButton,
            Text statusText)
        {
            _session = session;
            _addressInput = addressInput;
            _hostButton = hostButton;
            _clientButton = clientButton;
            _disconnectButton = disconnectButton;
            _statusText = statusText;
        }

        private void Start()
        {
            _addressInput.text = "127.0.0.1";
            _hostButton.onClick.AddListener(StartHost);
            _clientButton.onClick.AddListener(StartClient);
            _disconnectButton.onClick.AddListener(Shutdown);
            _session.StatusChanged += SetStatus;
            SetStatus("输入 Host 的局域网 IP，或在本机使用 127.0.0.1");
        }

        private void OnDestroy()
        {
            if (_session != null)
                _session.StatusChanged -= SetStatus;
        }

        private void Update()
        {
            bool listening = _session.Manager.IsListening;
            _hostButton.interactable = !listening;
            _clientButton.interactable = !listening;
            _addressInput.interactable = !listening;
            _disconnectButton.interactable = listening;
        }

        private void StartHost()
        {
            if (!_session.StartHost())
                SetStatus("Host 启动失败");
        }

        private void StartClient()
        {
            if (!_session.StartClient(_addressInput.text))
                SetStatus("Client 启动失败");
        }

        private void Shutdown()
        {
            _session.Shutdown();
        }

        private void SetStatus(string status)
        {
            _statusText.text = status;
        }
    }
}
