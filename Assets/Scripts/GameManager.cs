using System.Collections;
using System.Collections.Generic;
using FishNet;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Handles player spawning in the Game scene.
    /// This is spawned as a global NetworkObject by the LobbyManager.
    /// </summary>
    public class GameManager : NetworkBehaviour
    {
        [SerializeField] private float spawnCheckDelay = 1.0f;
        
        private List<NetworkConnection> connectionsToSpawn = new();
        
        public override void OnStartServer()
        {
            base.OnStartServer();
            Debug.Log("[GameManager] Started on server - checking for players to spawn");
            
            // Start checking for players to spawn
            StartCoroutine(CheckAndSpawnPlayers());
        }
        
        [Server]
        public void SetConnectionsToSpawn(List<NetworkConnection> connections)
        {
            connectionsToSpawn = new List<NetworkConnection>(connections);
            Debug.Log($"[GameManager] Set {connections.Count} connections to spawn");
        }
        
        private IEnumerator CheckAndSpawnPlayers()
        {
            yield return new WaitForSeconds(spawnCheckDelay);
            
            Debug.Log($"[GameManager] Checking for PlayerSpawner to spawn {connectionsToSpawn.Count} players");
            
            // Get all currently active connections instead of relying on stored ones
            var activeConnections = new List<NetworkConnection>();
            foreach (var kvp in InstanceFinder.ServerManager.Clients)
            {
                var conn = kvp.Value;
                if (conn != null && conn.IsActive && conn.IsValid)
                {
                    activeConnections.Add(conn);
                    Debug.Log($"[GameManager] Found active connection: ClientId={conn.ClientId}, IsHost={conn.IsHost}");
                }
            }
            
            Debug.Log($"[GameManager] Found {activeConnections.Count} active connections to spawn");
            
            // Find the PlayerSpawner in the Game scene
            var playerSpawner = FindObjectOfType<FishNet.Component.Spawning.PlayerSpawner>();
            if (playerSpawner == null)
            {
                Debug.LogError("[GameManager] No PlayerSpawner found in Game scene");
                yield break;
            }
            
            foreach (var conn in activeConnections)
            {
                Debug.Log($"[GameManager] Spawning player for client {conn.ClientId} (IsHost: {conn.IsHost})");
                SpawnPlayerForConnection(playerSpawner, conn);
            }
            
            // Clear the original list
            connectionsToSpawn.Clear();
        }
        
        private void SpawnPlayerForConnection(FishNet.Component.Spawning.PlayerSpawner spawner, NetworkConnection conn)
        {
            // Use reflection to access the private fields of PlayerSpawner
            var prefabField = typeof(FishNet.Component.Spawning.PlayerSpawner).GetField("_playerPrefab", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var addToSceneField = typeof(FishNet.Component.Spawning.PlayerSpawner).GetField("_addToDefaultScene", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (prefabField != null && addToSceneField != null)
            {
                var playerPrefab = (NetworkObject)prefabField.GetValue(spawner);
                var addToScene = (bool)addToSceneField.GetValue(spawner);

                if (playerPrefab != null)
                {
                    Vector3 position = playerPrefab.transform.position;
                    Quaternion rotation = playerPrefab.transform.rotation;

                    NetworkObject nob = InstanceFinder.NetworkManager.GetPooledInstantiated(playerPrefab, position, rotation, true);
                    InstanceFinder.ServerManager.Spawn(nob, conn);

                    if (addToScene)
                        InstanceFinder.SceneManager.AddOwnerToDefaultScene(nob);

                    Debug.Log($"[GameManager] Successfully spawned player for client {conn.ClientId}");
                }
                else
                {
                    Debug.LogError("[GameManager] PlayerSpawner has no player prefab assigned");
                }
            }
            else
            {
                Debug.LogError("[GameManager] Could not access PlayerSpawner private fields via reflection");
            }
        }
    }
}
