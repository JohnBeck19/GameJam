using FishNet;
using FishNet.Managing.Client;
using FishNet.Transporting;
using FishNet.Transporting.UTP;
using TMPro;
using UnityEngine;
using System.Collections;
using FishNet.Object;

namespace Game.Lobby
{
    /// <summary>
    /// Simple UI driver for hosting and joining by room code. No Relay specifics here.
    /// Assumes transport is configured in the scene.
    /// </summary>
    public class LobbyUI : MonoBehaviour
    {
        [SerializeField] private LobbyManager lobbyManager;
        [SerializeField] private TMP_InputField nameInput;
        [SerializeField] private TMP_InputField codeInput;
        [SerializeField] private TextMeshProUGUI statusLabel;

        [SerializeField] private int defaultMaxPlayers = 8;
        
        private string currentRoomCode = "";

        [Header("Panels")]
        [SerializeField] private GameObject mainPanel;   // Panel
        [SerializeField] private GameObject hostPanel;   // Panel (1)
        [SerializeField] private GameObject joinPanel;   // Panel (2)

        [Header("Host Widgets")]
        [SerializeField] private TextMeshProUGUI hostLobbyCodeLabel;
        [SerializeField] private TextMeshProUGUI playerCountLabel;
        [SerializeField] private UnityEngine.UI.Button playButton;

        private void Awake()
        {
            if (lobbyManager == null)
                lobbyManager = FindObjectOfType<LobbyManager>(true);

            LobbyManager.RoomJoined += OnRoomJoined;
            LobbyManager.JoinFailed += OnJoinFailed;
            LobbyManager.PlayerCountChanged += OnPlayerCountChanged;
            ShowMain();
        }

        private void OnDestroy()
        {
            LobbyManager.RoomJoined -= OnRoomJoined;
            LobbyManager.JoinFailed -= OnJoinFailed;
            LobbyManager.PlayerCountChanged -= OnPlayerCountChanged;
        }

        private bool _serverStarted;
        private bool _clientStarted;

        public void OnClickHost()
        {
            statusLabel?.SetText("Starting host...");
            // Show host panel immediately
            ShowHost();
            // Set initial state
            if (hostLobbyCodeLabel != null)
                hostLobbyCodeLabel.text = "Generating...";
            if (playerCountLabel != null)
                playerCountLabel.text = "0/2 Players";
            if (playButton != null)
                playButton.interactable = false;
                
            // Ensure LobbyManager object is active so the server can spawn it as a scene object.
            if (lobbyManager == null)
                lobbyManager = FindObjectOfType<LobbyManager>(true);
            if (lobbyManager != null && !lobbyManager.gameObject.activeSelf)
                lobbyManager.gameObject.SetActive(true);
            // Reset flags
            _serverStarted = false;
            _clientStarted = false;
            // Subscribe BEFORE starting connections
            InstanceFinder.ServerManager.OnServerConnectionState += OnServerConnectionState;
            InstanceFinder.ClientManager.OnClientConnectionState += OnClientStartedForCreate;
            // Start server then client (host)
            InstanceFinder.ServerManager.StartConnection();
            ConfigureLocalLoopback();
            InstanceFinder.ClientManager.StartConnection();
        }

        public void OnClickJoin()
        {
            statusLabel?.SetText("Starting client...");
            InstanceFinder.ClientManager.OnClientConnectionState += OnClientStartedForJoin;
            ConfigureLocalLoopback();
            InstanceFinder.ClientManager.StartConnection();
        }

        public void OnClickGoToJoin()
        {
            ShowJoin();
        }

        public void OnClickHostBack()
        {
            InstanceFinder.ClientManager.StopConnection();
            InstanceFinder.ServerManager.StopConnection(true);
            statusLabel?.SetText("Stopped host.");
            ShowMain();
        }

        public void OnClickLeave()
        {
            InstanceFinder.ClientManager.StopConnection();
            statusLabel?.SetText("Left lobby.");
            ShowMain();
        }

        public void OnClickJoinBack()
        {
            InstanceFinder.ClientManager.StopConnection();
            statusLabel?.SetText("Back to menu.");
            ShowMain();
        }

        private void OnClientStartedForCreate(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState != LocalConnectionState.Started)
                return;
            InstanceFinder.ClientManager.OnClientConnectionState -= OnClientStartedForCreate;
            _clientStarted = true;
            TryCreateAsHost();
        }

        private void OnServerConnectionState(ServerConnectionStateArgs args)
        {
            if (args.ConnectionState != LocalConnectionState.Started)
                return;
            InstanceFinder.ServerManager.OnServerConnectionState -= OnServerConnectionState;
            _serverStarted = true;
            
            // Create room immediately when server starts (don't wait for client)
            string display = string.IsNullOrWhiteSpace(nameInput?.text) ? "Player" : nameInput.text;
            Debug.Log("[LobbyUI] Host: server started; creating room immediately.");
            StartCoroutine(Co_CreateRoomWhenReady(display));
        }

        private void TryCreateAsHost()
        {
            // This method is now only used to add the host as a client after both server and client are ready
            if (!(_serverStarted && _clientStarted))
                return;

            Debug.Log("[LobbyUI] Host: client connected; manually adding host to room.");
            
            // Manually add the host to the room since they created it
            if (lobbyManager != null && !string.IsNullOrEmpty(LobbyManager.LastCreatedCode))
            {
                string display = string.IsNullOrWhiteSpace(nameInput?.text) ? "Player" : nameInput.text;
                lobbyManager.JoinRoomServerRpc(LobbyManager.LastCreatedCode, display);
                
                // Also manually update UI since the TargetRpc might not reach us
                currentRoomCode = LobbyManager.LastCreatedCode;
                if (hostLobbyCodeLabel != null)
                    hostLobbyCodeLabel.text = LobbyManager.LastCreatedCode;
                statusLabel?.SetText($"Room created. Code: {LobbyManager.LastCreatedCode}");
            }
        }

        private void OnClientStartedForJoin(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState != LocalConnectionState.Started)
                return;
            InstanceFinder.ClientManager.OnClientConnectionState -= OnClientStartedForJoin;

            string display = string.IsNullOrWhiteSpace(nameInput?.text) ? "Player" : nameInput.text;
            string code = codeInput?.text?.Trim().ToUpperInvariant();
            InvokeOrEnqueueJoinRpc(code, display);
        }

        private void OnRoomJoined(string code)
        {
            Debug.Log($"[LobbyUI] Room code: {code}");
            currentRoomCode = code; // Store the actual room code
            statusLabel?.SetText($"Room created. Code: {code}");
            if (codeInput != null)
                codeInput.text = code;
            if (hostLobbyCodeLabel != null)
                hostLobbyCodeLabel.text = code;
            // Don't call ShowHost() here since it's already shown when Create is clicked
        }

        private void OnJoinFailed(string reason)
        {
            Debug.LogWarning($"[LobbyUI] Join failed: {reason}");
            statusLabel?.SetText($"Join failed: {reason}");
            ShowMain();
        }

        private void OnPlayerCountChanged(int currentCount, int maxCount)
        {
            if (playerCountLabel != null)
                playerCountLabel.text = $"{currentCount}/{maxCount} Players";
            
            if (playButton != null)
                playButton.interactable = currentCount >= 1; // Allow starting with 1 player for testing
        }

        public void OnClickPlay()
        {
            if (lobbyManager != null)
            {
                // Use stored code, or fallback to LobbyManager.LastCreatedCode if event didn't fire
                string codeToUse = !string.IsNullOrEmpty(currentRoomCode) ? currentRoomCode : LobbyManager.LastCreatedCode;
                Debug.Log($"[LobbyUI] Play clicked. Using room code: {codeToUse}");
                lobbyManager.StartMatchServerRpc(codeToUse);
                statusLabel?.SetText("Starting match...");
            }
        }

        private void ConfigureLocalLoopback()
        {
            var ut = InstanceFinder.NetworkManager.TransportManager.GetTransport<UnityTransport>();
            if (ut != null)
            {
                try { ut.SetClientAddress("127.0.0.1"); } catch { }
            }
        }

        private IEnumerator Co_CreateRoomWhenReady(string display)
        {
            if (lobbyManager == null)
                lobbyManager = FindObjectOfType<LobbyManager>(true);
            if (lobbyManager == null)
            {
                statusLabel?.SetText("No LobbyManager found.");
                yield break;
            }

            // For host: bypass RPC and call server method directly to avoid client-side NetworkObject issues
            if (lobbyManager.IsServer && InstanceFinder.ClientManager.Connection != null)
            {
                Debug.Log("[LobbyUI] Host: calling ServerCreateRoom directly.");
                lobbyManager.ServerCreateRoom(display, defaultMaxPlayers, InstanceFinder.ClientManager.Connection);
                statusLabel?.SetText("Creating room...");
                yield break;
            }

            // For regular clients: wait for NetworkObject to be spawned then use RPC
            float timeout = 5f;
            float elapsed = 0f;
            while (!lobbyManager.IsSpawned && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (lobbyManager.IsSpawned)
            {
                Debug.Log("[LobbyUI] Client: LobbyManager is spawned; calling CreateRoomServerRpc.");
                lobbyManager.CreateRoomServerRpc(display, defaultMaxPlayers);
                statusLabel?.SetText("Creating room...");
            }
            else
            {
                statusLabel?.SetText("Lobby not ready. Try again.");
            }
        }

        private void InvokeOrEnqueueJoinRpc(string code, string display)
        {
            if (lobbyManager == null)
                lobbyManager = FindObjectOfType<LobbyManager>(true);
            if (lobbyManager == null)
            {
                Debug.LogWarning("[LobbyUI] No LobbyManager found to join room.");
                return;
            }
            if (lobbyManager.IsSpawned)
            {
                lobbyManager.JoinRoomServerRpc(code, display);
                statusLabel?.SetText("Joining room...");
            }
            else
            {
                // Wait for LobbyManager to be spawned via polling (simpler than event hooking)
                StartCoroutine(WaitForSpawnThenJoin(code, display));
            }
        }

        private IEnumerator WaitForSpawnThenJoin(string code, string display)
        {
            float timeout = 2f;
            float elapsed = 0f;
            while ((lobbyManager == null || !lobbyManager.IsSpawned) && elapsed < timeout)
            {
                if (lobbyManager == null)
                    lobbyManager = FindObjectOfType<LobbyManager>(true);
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (lobbyManager != null && lobbyManager.IsSpawned)
            {
                lobbyManager.JoinRoomServerRpc(code, display);
                statusLabel?.SetText("Joining room...");
            }
            else
            {
                statusLabel?.SetText("Lobby not ready. Try again.");
            }
        }

        private void ShowMain()
        {
            if (mainPanel != null) mainPanel.SetActive(true);
            if (hostPanel != null) hostPanel.SetActive(false);
            if (joinPanel != null) joinPanel.SetActive(false);
        }

        private void ShowHost()
        {
            if (mainPanel != null) mainPanel.SetActive(false);
            if (hostPanel != null) hostPanel.SetActive(true);
            if (joinPanel != null) joinPanel.SetActive(false);
        }

        private void ShowJoin()
        {
            if (mainPanel != null) mainPanel.SetActive(false);
            if (hostPanel != null) hostPanel.SetActive(false);
            if (joinPanel != null) joinPanel.SetActive(true);
        }
    }
}


