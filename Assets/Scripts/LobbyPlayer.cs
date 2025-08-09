using FishNet.Object;
using UnityEngine;

namespace Game.Lobby
{
    /// <summary>
    /// Per-connection lobby object. Holds ready state and name. Visibility is scoped to room members.
    /// </summary>
    public class LobbyPlayer : NetworkBehaviour
    {
        public string DisplayName { get; private set; }
        public bool IsReady { get; private set; }

        [HideInInspector]
        public LobbyManager.LobbyRoom Room;

        // Default lifecycle is sufficient here; server controls visibility via observer settings if configured.

        [Server]
        public void ServerSetName(string name)
        {
            DisplayName = string.IsNullOrWhiteSpace(name) ? $"Player {Owner.ClientId}" : name;
            RpcUpdateState(DisplayName, IsReady);
        }

        [ServerRpc]
        public void ToggleReadyServerRpc()
        {
            IsReady = !IsReady;
            RpcUpdateState(DisplayName, IsReady);
        }

        [ObserversRpc]
        private void RpcUpdateState(string displayName, bool isReady)
        {
            DisplayName = displayName;
            IsReady = isReady;
            // TODO: notify local UI if needed.
        }
    }
}


