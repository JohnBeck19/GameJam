using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FishNet;
using FishNet.Connection;
using FishNet.Managing.Client;
using FishNet.Transporting;
using FishNet.Managing.Scened;
using FishNet.Object;
using UnityEngine;

namespace Game.Lobby
{
    /// <summary>
    /// Authoritative lobby manager. Hosts create rooms, clients join by code.
    /// Server spawns a LobbyPlayer for each connection and keeps room membership.
    /// </summary>
    public class LobbyManager : NetworkBehaviour
    {
        public static event Action<string> RoomJoined; // code
        public static event Action<string> JoinFailed; // reason
        public static event Action<int, int> PlayerCountChanged; // currentCount, maxCount
        public static string LastCreatedCode { get; private set; }
        [SerializeField] private GameObject lobbyPlayerPrefab;
        [SerializeField] private GameObject gameManagerPrefab;
        [SerializeField] private int defaultMaxPlayers = 8;

        private readonly Dictionary<string, LobbyRoom> roomCodeToRoom = new();
        private void Awake()
        {
            Debug.Log("[LobbyManager] Awake called");
            if (InstanceFinder.ServerManager != null)
            {
                InstanceFinder.ServerManager.OnRemoteConnectionState += HandleRemoteConnectionState;
                Debug.Log("[LobbyManager] Subscribed to ServerManager events");
            }
        }

        private void OnDestroy()
        {
            if (InstanceFinder.ServerManager != null)
                InstanceFinder.ServerManager.OnRemoteConnectionState -= HandleRemoteConnectionState;
        }

        private void HandleRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState == RemoteConnectionState.Stopped)
                RemoveFromAnyRoom(conn);
        }

        [ServerRpc(RequireOwnership = false)]
        public void CreateRoomServerRpc(string displayName, int maxPlayers, NetworkConnection caller = null)
        {
            CreateRoomInternal(caller, displayName, maxPlayers);
        }

        [Server]
        public void ServerCreateRoom(string displayName, int maxPlayers, NetworkConnection caller)
        {
            CreateRoomInternal(caller, displayName, maxPlayers);
        }

        [Server]
        private void CreateRoomInternal(NetworkConnection caller, string displayName, int maxPlayers)
        {
            if (caller == null)
                return;

            int capacity = Mathf.Clamp(maxPlayers <= 0 ? defaultMaxPlayers : maxPlayers, 1, 64);
            string code = GenerateRoomCode();
            Debug.Log($"[LobbyManager] Creating room {code} for {caller.ClientId} (capacity {capacity})");
            var room = new LobbyRoom(code, capacity);
            roomCodeToRoom[code] = room;

            AddToRoom(room, caller, displayName);
            Debug.Log($"[LobbyManager] Room {code} created; notifying caller.");
            TargetRoomJoined(caller, code);
            RpcRoomJoined(code); // also notify all observers (host will get this immediately)
            LastCreatedCode = code;
            
            // Update player count
            RpcPlayerCountChanged(room.Connections.Count, room.MaxPlayers);
        }

        [ServerRpc(RequireOwnership = false)]
        public void JoinRoomServerRpc(string roomCode, string displayName, NetworkConnection caller = null)
        {
            if (caller == null)
                return;

            if (string.IsNullOrWhiteSpace(roomCode))
            {
                TargetJoinFailed(caller, "Invalid room code");
                return;
            }

            roomCode = roomCode.Trim().ToUpperInvariant();
            if (!roomCodeToRoom.TryGetValue(roomCode, out LobbyRoom room))
            {
                TargetJoinFailed(caller, "Room not found");
                return;
            }

            if (room.IsFull)
            {
                TargetJoinFailed(caller, "Room is full");
                return;
            }

            AddToRoom(room, caller, displayName);
            TargetRoomJoined(caller, roomCode);
            
            // Update player count
            RpcPlayerCountChanged(room.Connections.Count, room.MaxPlayers);
        }

        [ServerRpc(RequireOwnership = false)]
        public void StartMatchServerRpc(string roomCode, NetworkConnection caller = null)
        {
            if (caller == null)
            {
                Debug.LogWarning("[LobbyManager] StartMatchServerRpc: caller is null");
                return;
            }

            if (!roomCodeToRoom.TryGetValue(roomCode.Trim().ToUpperInvariant(), out LobbyRoom room))
            {
                Debug.LogWarning($"[LobbyManager] StartMatchServerRpc: room {roomCode} not found");
                return;
            }

            // Skip member check for now - just verify the room has connections
            if (room.Connections.Count == 0)
            {
                Debug.LogWarning($"[LobbyManager] StartMatchServerRpc: room {roomCode} has no connections");
                return;
            }

            // Skip ready check for now - start match with any number of players
            Debug.Log($"[LobbyManager] Starting match for room {roomCode} with {room.Connections.Count} players");

            // Debug: Print all connections in the room
            foreach (var conn in room.Connections)
            {
                Debug.Log($"[LobbyManager] Room connection: ClientId={conn.ClientId}, IsHost={conn.IsHost}, IsValid={conn.IsValid}");
            }

            var conns = room.Connections.ToList();
            var load = new SceneLoadData("Game");
            load.ReplaceScenes = ReplaceOption.All;
            
            // Spawn a global GameManager to handle player spawning in the Game scene
            if (gameManagerPrefab == null)
            {
                Debug.LogError("[LobbyManager] GameManager prefab is not assigned!");
                return;
            }
            
            GameObject gmGo = Instantiate(gameManagerPrefab);
            var gameManager = gmGo.GetComponent<Game.GameManager>();
            if (gameManager == null)
            {
                Debug.LogError("[LobbyManager] GameManager prefab does not contain GameManager component!");
                Destroy(gmGo);
                return;
            }
            
            // Spawn the GameManager as a global NetworkObject
            ServerManager.Spawn(gmGo);
            gameManager.SetConnectionsToSpawn(conns);
            Debug.Log($"[LobbyManager] Spawned GameManager to handle {conns.Count} player spawns");
            
            foreach (var c in conns)
            {
                Debug.Log($"[LobbyManager] Loading Game scene for client {c.ClientId}");
                InstanceFinder.SceneManager.LoadConnectionScenes(c, load);
            }
        }

        [TargetRpc]
        private void TargetRoomJoined(NetworkConnection conn, string code)
        {
            RoomJoined?.Invoke(code);
        }

        [TargetRpc]
        private void TargetJoinFailed(NetworkConnection conn, string reason)
        {
            JoinFailed?.Invoke(reason);
        }

        [ObserversRpc]
        private void RpcRoomJoined(string code)
        {
            RoomJoined?.Invoke(code);
        }

        [ObserversRpc]
        private void RpcPlayerCountChanged(int currentCount, int maxCount)
        {
            PlayerCountChanged?.Invoke(currentCount, maxCount);
        }

        private void AddToRoom(LobbyRoom room, NetworkConnection conn, string displayName)
        {
            bool wasAdded = room.Connections.Add(conn);
            Debug.Log($"[LobbyManager] AddToRoom: ClientId={conn.ClientId}, WasAdded={wasAdded}, Total={room.Connections.Count}");
            if (!wasAdded)
                return;

            if (lobbyPlayerPrefab == null)
            {
                Debug.LogError("LobbyManager: LobbyPlayer prefab is not assigned.");
                return;
            }

            GameObject lpGo = Instantiate(lobbyPlayerPrefab);
            var lobbyPlayer = lpGo.GetComponent<LobbyPlayer>();
            if (lobbyPlayer == null)
            {
                Debug.LogError("LobbyManager: LobbyPlayer prefab does not contain LobbyPlayer component.");
                Destroy(lpGo);
                return;
            }

            lobbyPlayer.Room = room;
            ServerManager.Spawn(lpGo, conn);
            room.Members.Add(lobbyPlayer);

            // Initialize display name server-side
            lobbyPlayer.ServerSetName(string.IsNullOrWhiteSpace(displayName) ? $"Player {conn.ClientId}" : displayName);
        }

        private void RemoveFromAnyRoom(NetworkConnection conn)
        {
            foreach (var kvp in roomCodeToRoom.ToList())
            {
                LobbyRoom room = kvp.Value;
                if (!room.Connections.Remove(conn))
                    continue;

                foreach (var lp in room.Members.Where(p => p != null && p.Owner == conn).ToList())
                {
                    room.Members.Remove(lp);
                    if (lp != null && lp.gameObject != null)
                        ServerManager.Despawn(lp.gameObject);
                }

                if (room.Connections.Count == 0)
                    roomCodeToRoom.Remove(kvp.Key);
                else
                    // Update player count for remaining players
                    RpcPlayerCountChanged(room.Connections.Count, room.MaxPlayers);

                break;
            }
        }



        private static string GenerateRoomCode()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            System.Random rand = new System.Random();
            return new string(Enumerable.Range(0, 6).Select(_ => chars[rand.Next(chars.Length)]).ToArray());
        }

        [Serializable]
        public sealed class LobbyRoom
        {
            public readonly string Code;
            public readonly int MaxPlayers;
            public readonly HashSet<NetworkConnection> Connections = new();
            public readonly HashSet<LobbyPlayer> Members = new();

            public LobbyRoom(string code, int maxPlayers)
            {
                Code = code;
                MaxPlayers = maxPlayers;
            }

            public bool IsFull => Connections.Count >= MaxPlayers;
            public bool AllReady => Members.Count > 0 && Members.All(m => m != null && m.IsReady);
        }
    }
}


