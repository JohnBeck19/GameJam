using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Networking.Transport.Relay;

public class UgsLobbyManager : MonoBehaviour
{
    [Header("Transport")]
    [SerializeField] private UnityTransport transport;

    [Header("Defaults")]
    [SerializeField] private string defaultLobbyName = "MyLobby";
    [SerializeField] private int defaultMaxPlayers = 2;
    [Header("UGS Environment")]
    [SerializeField] private string environmentName = "production";

    public Lobby CurrentLobby { get; private set; }
    public bool IsHost { get; private set; }
    public string PlayerId => AuthenticationService.Instance?.PlayerId;

    Coroutine heartbeatCoroutine;
    bool servicesInitialized;
    bool transportFailureSubscribed;

    async void Awake()
    {
        if (transport == null)
        {
            transport = FindObjectOfType<UnityTransport>();
        }
        await EnsureServicesAsync();
    }

    async Task EnsureServicesAsync()
    {
        if (servicesInitialized)
            return;

        var initOptions = new InitializationOptions();
        if (!string.IsNullOrWhiteSpace(environmentName))
        {
            initOptions.SetEnvironmentName(environmentName);
        }
        await UnityServices.InitializeAsync(initOptions);
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
        Debug.Log($"UGS initialized. ProjectId={Application.cloudProjectId} Env={environmentName} PlayerId={AuthenticationService.Instance.PlayerId}");
        servicesInitialized = true;
    }

    // Host flow: create lobby -> allocate relay -> publish join code -> start host
    public async Task CreateLobbyAndStartHostAsync(string lobbyName = null, int maxPlayers = -1)
    {
        await EnsureServicesAsync();
        lobbyName ??= defaultLobbyName;
        if (maxPlayers <= 1) maxPlayers = Mathf.Max(2, defaultMaxPlayers);

        // Allocate Relay FIRST so we can embed the join code into the lobby creation
        var allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers - 1);
        string relayJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

        // Create Lobby with relayCode baked in to avoid propagation gaps
        var options = new CreateLobbyOptions
        {
            IsPrivate = false,
            Data = new Dictionary<string, DataObject>
            {
                { "relayCode", new DataObject(DataObject.VisibilityOptions.Public, relayJoinCode) }
            }
        };
        CurrentLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, options);
        IsHost = true;
        Debug.Log($"Lobby created. LobbyCode={CurrentLobby?.LobbyCode} RelayJoinCode={relayJoinCode}");

        // Heartbeat so lobby stays alive
        if (heartbeatCoroutine != null) StopCoroutine(heartbeatCoroutine);
        heartbeatCoroutine = StartCoroutine(HeartbeatLobbyCoroutine());

        // Configure transport for host and start
        EnsureTransport();
        var hostRelayData = new RelayServerData(
            allocation.RelayServer.IpV4,
            (ushort)allocation.RelayServer.Port,
            allocation.AllocationIdBytes,
            allocation.ConnectionData,
            allocation.ConnectionData,
            allocation.Key,
            true,
            false
        );
        transport.SetRelayServerData(hostRelayData);
        NetworkManager.Singleton.StartHost();

        // Subscribe to transport failure for auto-recovery on host
        TrySubscribeTransportFailure();
    }

        // Client flow: quick join lobby -> read relay code -> join relay -> start client
    public async Task QuickJoinAndStartClientAsync()
    {
        await EnsureServicesAsync();
        var joined = await LobbyService.Instance.QuickJoinLobbyAsync();
        await StartClientFromLobbyAsync(joined);
    }

    public async Task JoinByCodeAndStartClientAsync(string lobbyCode)
    {
        await EnsureServicesAsync();

        // Normalize user input to avoid common typos (e.g., 'O' -> '0')
        string originalCode = lobbyCode ?? string.Empty;
        string normalizedCode = NormalizeLobbyCode(originalCode);
        if (!string.Equals(originalCode, normalizedCode, StringComparison.Ordinal))
        {
            Debug.Log($"Normalized lobby code '{originalCode}' -> '{normalizedCode}'");
        }

        Lobby joined = null;

        // If already tracking a lobby, reuse if codes match; otherwise leave local reference only
        if (CurrentLobby != null && string.Equals(CurrentLobby.LobbyCode, normalizedCode, StringComparison.OrdinalIgnoreCase))
        {
            joined = CurrentLobby;
        }

        if (joined == null)
        {
            try
            {
                joined = await LobbyService.Instance.JoinLobbyByCodeAsync(normalizedCode);
            }
            catch (Exception e)
            {
                // If invalid code, don't spam fallback calls
                var emsg = e.Message?.ToLowerInvariant() ?? string.Empty;
                if (emsg.Contains("invalid character") || (emsg.Contains("invalid") && emsg.Contains("lobby code")))
                {
                    throw;
                }
                if (emsg.Contains("rate limit"))
                {
                    await Task.Delay(1200);
                    throw;
                }

                // Otherwise, try to fetch joined lobbies
                Debug.LogWarning($"Join by code failed: {e.Message}. Attempting to fetch joined lobbies...");
                try
                {
                    var joinedLobbyIds = await LobbyService.Instance.GetJoinedLobbiesAsync();
                    int inspected = 0;
                    foreach (var lobbyId in joinedLobbyIds)
                    {
                        Lobby lobDetail = null;
                        try { lobDetail = await LobbyService.Instance.GetLobbyAsync(lobbyId); }
                        catch (Exception e3) { Debug.LogWarning($"GetLobbyAsync failed for {lobbyId}: {e3.Message}"); }
                        if (lobDetail != null && string.Equals(lobDetail.LobbyCode, normalizedCode, StringComparison.OrdinalIgnoreCase))
                        {
                            joined = lobDetail;
                            break;
                        }
                        inspected++;
                        if (inspected >= 3) break; // Avoid rate limits by scanning only a few
                    }
                }
                catch (Exception e2)
                {
                    Debug.LogWarning($"GetJoinedLobbies after join failure also failed: {e2.Message}");
                }
                if (joined == null) throw;
            }
        }

        await StartClientFromLobbyAsync(joined);
    }

    async Task StartClientFromLobbyAsync(Lobby lobby)
    {
        CurrentLobby = lobby;
        IsHost = false;

        EnsureTransport();

        int delay = 500; // ms
        const int maxAttempts = 8;
        JoinAllocation allocation = null;
        string usedCode = null;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            string joinCode = null;
            try
            {
                var snapshot = lobby;
                if (snapshot == null || snapshot.Data == null || !snapshot.Data.ContainsKey("relayCode") || string.IsNullOrEmpty(snapshot.Data["relayCode"].Value))
                {
                    snapshot = await LobbyService.Instance.GetLobbyAsync(CurrentLobby.Id);
                }
                if (snapshot != null && snapshot.Data != null && snapshot.Data.ContainsKey("relayCode"))
                {
                    joinCode = snapshot.Data["relayCode"].Value;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"GetLobbyAsync failed (attempt {attempt}): {e.Message}");
            }

            joinCode = (joinCode ?? string.Empty).Trim();
            if (joinCode.Length < 6)
            {
                Debug.LogWarning($"Relay code not ready yet (attempt {attempt}). Retrying...");
                await Task.Delay(delay);
                delay = Mathf.Min(delay * 2, 7000);
                continue;
            }

            try
            {
                Debug.Log($"Attempting Relay join (attempt {attempt}). LobbyId={CurrentLobby.Id} LobbyCode={CurrentLobby.LobbyCode} RelayJoinCode={joinCode}");
                allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
                usedCode = joinCode;
                break;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Relay join attempt {attempt} failed for code '{joinCode}': {ex.Message}");
                await Task.Delay(delay);
                delay = Mathf.Min(delay * 2, 7000);
                lobby = null; // force a refresh next loop
            }
        }

        if (allocation == null)
        {
            Debug.LogError("Failed to join Relay after multiple attempts.");
            return;
        }

        var clientRelayData = new RelayServerData(
            allocation.RelayServer.IpV4,
            (ushort)allocation.RelayServer.Port,
            allocation.AllocationIdBytes,
            allocation.ConnectionData,
            allocation.HostConnectionData,
            allocation.Key,
            true,
            false
        );
        transport.SetRelayServerData(clientRelayData);

        Debug.Log($"Relay joined with code {usedCode}. Starting client...");
        NetworkManager.Singleton.StartClient();
    }

    async Task<string> FetchRelayJoinCodeWithRetries(string lobbyId, int maxAttempts, int delayMs)
    {
        for (int i = 0; i < maxAttempts; i++)
        {
            try
            {
                var refreshed = await LobbyService.Instance.GetLobbyAsync(lobbyId);
                if (refreshed != null && refreshed.Data != null && refreshed.Data.ContainsKey("relayCode"))
                {
                    var code = refreshed.Data["relayCode"].Value;
                    if (!string.IsNullOrEmpty(code))
                        return code;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Fetch relay code attempt {i+1} failed: {e.Message}");
            }
            await Task.Delay(delayMs);
        }
        return null;
    }

    async Task<string> FetchRelayJoinCodeWithBackoff(string lobbyId, int initialDelayMs, int maxAttempts)
    {
        int delay = initialDelayMs;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var refreshed = await LobbyService.Instance.GetLobbyAsync(lobbyId);
                if (refreshed != null && refreshed.Data != null && refreshed.Data.ContainsKey("relayCode"))
                {
                    var code = refreshed.Data["relayCode"].Value;
                    if (!string.IsNullOrEmpty(code))
                        return code;
                }
            }
            catch (Exception e)
            {
                var emsg = e.Message?.ToLowerInvariant() ?? string.Empty;
                if (emsg.Contains("rate limit"))
                {
                    Debug.LogWarning($"Fetch relay code (backoff) attempt {attempt} hit rate limit. Backing off...");
                }
                else
                {
                    Debug.LogWarning($"Fetch relay code (backoff) attempt {attempt} failed: {e.Message}");
                }
            }
            await Task.Delay(delay);
            // Exponential backoff with cap
            delay = Mathf.Min(delay * 2, 7000);
        }
        return null;
    }

    static string NormalizeLobbyCode(string input)
    {
        // Replace visually ambiguous characters commonly mis-typed in codes
        // O->0, I->1, L->1, S->5, Z->2 (only when those letters appear)
        string s = (input ?? string.Empty).Trim();
        if (s.Length == 0) return s;
        var sb = new System.Text.StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == 'O') c = '0';
            else if (c == 'I' || c == 'L') c = '1';
            else if (c == 'S') c = '5';
            else if (c == 'Z') c = '2';
            sb.Append(c);
        }
        return sb.ToString();
    }

    public async Task LeaveAsync()
    {
        if (heartbeatCoroutine != null)
        {
            StopCoroutine(heartbeatCoroutine);
            heartbeatCoroutine = null;
        }

        if (CurrentLobby != null)
        {
            try
            {
                if (IsHost)
                {
                    await LobbyService.Instance.DeleteLobbyAsync(CurrentLobby.Id);
                }
                else
                {
                    await LobbyService.Instance.RemovePlayerAsync(CurrentLobby.Id, PlayerId);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Leave lobby error: {e}");
            }
        }

        CurrentLobby = null;
        NetworkManager.Singleton.Shutdown();
    }

    void TrySubscribeTransportFailure()
    {
        if (NetworkManager.Singleton == null || transportFailureSubscribed)
            return;
        try
        {
            NetworkManager.Singleton.OnTransportFailure += OnTransportFailure;
            transportFailureSubscribed = true;
        }
        catch (Exception)
        {
            // Older NGO versions may not expose this event
        }
    }

    async void OnTransportFailure()
    {
        Debug.LogError("Transport failure detected. Attempting host recovery...");
        if (!IsHost || CurrentLobby == null)
        {
            return;
        }
        try
        {
            // Shutdown current network session
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.Shutdown();
            }

            // Recreate Relay allocation and update lobby relayCode
            int playerCount = Mathf.Max(2, CurrentLobby.MaxPlayers);
            var allocation = await RelayService.Instance.CreateAllocationAsync(playerCount - 1);
            string relayJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            await LobbyService.Instance.UpdateLobbyAsync(CurrentLobby.Id, new UpdateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>
                {
                    { "relayCode", new DataObject(DataObject.VisibilityOptions.Public, relayJoinCode.ToUpperInvariant()) }
                }
            });

            // Reconfigure transport and restart host
            EnsureTransport();
            var hostRelayData = new RelayServerData(
                allocation.RelayServer.IpV4,
                (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes,
                allocation.ConnectionData,
                allocation.ConnectionData,
                allocation.Key,
                true,
                false
            );
            transport.SetRelayServerData(hostRelayData);
            NetworkManager.Singleton.StartHost();
            Debug.Log($"Host recovered with new Relay code {relayJoinCode}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Host recovery failed: {e.Message}");
        }
    }

    IEnumerator HeartbeatLobbyCoroutine()
    {
        var wait = new WaitForSeconds(15f);
        while (CurrentLobby != null && IsHost)
        {
            yield return wait;
            var task = LobbyService.Instance.SendHeartbeatPingAsync(CurrentLobby.Id);
            while (!task.IsCompleted)
            {
                yield return null;
            }
            if (task.IsFaulted)
            {
                Debug.LogWarning($"Lobby heartbeat failed: {task.Exception?.GetBaseException().Message}");
            }
        }
    }

    void EnsureTransport()
    {
        if (transport == null)
        {
            transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport == null)
            {
                transport = NetworkManager.Singleton.gameObject.AddComponent<UnityTransport>();
            }
        }
    }

    void OnDestroy()
    {
        // Best-effort cleanup when object is destroyed (e.g., scene unload)
        if (CurrentLobby != null)
        {
            var _ = LeaveAsync();
        }
    }

    void OnApplicationQuit()
    {
        // Best-effort cleanup on app quit
        if (CurrentLobby != null)
        {
            var _ = LeaveAsync();
        }
    }

    // Utility to call async from coroutine
    static IEnumerator awaitable(Task task)
    {
        while (!task.IsCompleted)
            yield return null;
        if (task.IsFaulted)
            throw task.Exception;
    }
}


