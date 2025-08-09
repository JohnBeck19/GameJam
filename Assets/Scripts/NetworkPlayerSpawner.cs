using System.Linq;
using UnityEngine;
using Unity.Netcode;

// Server-authoritative fallback spawner that ensures a player object exists for each client.
// Use this if NetworkManager Default Player Prefab is not assigned.
public class NetworkPlayerSpawner : MonoBehaviour
{
    [Header("Player Prefab (NetworkObject)")]
    [SerializeField] private NetworkObject playerPrefab;

    void OnEnable()
    {
        if (NetworkManager.Singleton == null)
            return;
        NetworkManager.Singleton.OnServerStarted += OnServerStarted;
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    void OnDisable()
    {
        if (NetworkManager.Singleton == null)
            return;
        NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }

    void OnServerStarted()
    {
        if (!NetworkManager.Singleton.IsServer)
            return;
        // Host may already be connected when server starts
        foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds.ToList())
        {
            EnsurePlayerSpawned(clientId);
        }
    }

    void OnClientConnected(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsServer)
            return;
        EnsurePlayerSpawned(clientId);
    }

    void EnsurePlayerSpawned(ulong clientId)
    {
        if (playerPrefab == null)
        {
            Debug.LogError("NetworkPlayerSpawner: Player Prefab is not assigned.");
            return;
        }

        var client = NetworkManager.Singleton.ConnectedClients.ContainsKey(clientId)
            ? NetworkManager.Singleton.ConnectedClients[clientId]
            : null;
        if (client == null)
            return;

        if (client.PlayerObject != null)
            return; // Already spawned

        var instance = Instantiate(playerPrefab);
        instance.SpawnAsPlayerObject(clientId, true);
    }
}


