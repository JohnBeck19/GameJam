using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UgsLobbyUi : MonoBehaviour
{
    [SerializeField] private UgsLobbyManager lobbyManager;

    [Header("Panels")]
    [SerializeField] private GameObject landingPanel;
    [SerializeField] private GameObject hostPanel;
    [SerializeField] private GameObject joinPanel;

    [Header("Landing Buttons")]
    [SerializeField] private Button createLobbyButton;
    [SerializeField] private Button goToJoinButton;

    [Header("Host (Create) UI")] 
    // removed: lobby name and max players, always 2 players
    [SerializeField] private TMP_Text hostLobbyCodeLabel; // displays the LobbyCode after creation
    [SerializeField] private Button leaveButton;
    [SerializeField] private Button hostBackButton;

    [Header("Join UI")] 
    [SerializeField] private TMP_InputField joinCodeInput; // lobby code
    [SerializeField] private Button quickJoinButton; // repurposed: join by code
    [SerializeField] private Button joinBackButton;

    [Header("Status")] 
    [SerializeField] private TMP_Text statusLabel;

    void Awake()
    {
        if (lobbyManager == null)
            lobbyManager = FindObjectOfType<UgsLobbyManager>();

        // Landing
        if (createLobbyButton != null)
        {
            createLobbyButton.onClick.AddListener(async () =>
            {
                await lobbyManager.CreateLobbyAndStartHostAsync(null, 2);
                UpdateStatus();
                UpdateHostCodeLabel();
                ShowHostPanel();
            });
        }
        if (goToJoinButton != null)
        {
            goToJoinButton.onClick.AddListener(ShowJoinPanel);
        }

        // Host panel
        if (leaveButton != null)
        {
            leaveButton.onClick.AddListener(async () =>
            {
                await lobbyManager.LeaveAsync();
                UpdateStatus();
                ShowLandingPanel();
            });
        }
        if (hostBackButton != null)
        {
            hostBackButton.onClick.AddListener(ShowLandingPanel);
        }

        // Join panel
        if (quickJoinButton != null)
        {
            quickJoinButton.onClick.AddListener(async () =>
            {
                string code = joinCodeInput != null ? (joinCodeInput.text ?? string.Empty).Trim() : string.Empty;
                if (string.IsNullOrEmpty(code))
                {
                    Debug.LogWarning("Join code is empty.");
                    UpdateStatus();
                    return;
                }
                try
                {
                    await lobbyManager.JoinByCodeAndStartClientAsync(code);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Join failed: {ex.Message}");
                }
                UpdateStatus();
                if (lobbyManager != null && lobbyManager.CurrentLobby != null)
                {
                    HideAllPanels(); // close tabs when successfully joined
                }
                else
                {
                    ShowJoinPanel();
                }
            });
        }
        if (joinBackButton != null)
        {
            joinBackButton.onClick.AddListener(ShowLandingPanel);
        }

        ShowLandingPanel();
        UpdateStatus();
    }

    void UpdateHostCodeLabel()
    {
        if (hostLobbyCodeLabel == null || lobbyManager == null)
            return;
        var lobby = lobbyManager.CurrentLobby;
        hostLobbyCodeLabel.text = lobby == null ? "--" : (lobby.LobbyCode ?? "--");
    }

    void ShowLandingPanel()
    {
        if (landingPanel != null) landingPanel.SetActive(true);
        if (hostPanel != null) hostPanel.SetActive(false);
        if (joinPanel != null) joinPanel.SetActive(false);
    }

    void ShowHostPanel()
    {
        if (landingPanel != null) landingPanel.SetActive(false);
        if (hostPanel != null) hostPanel.SetActive(true);
        if (joinPanel != null) joinPanel.SetActive(false);
    }

    void ShowJoinPanel()
    {
        if (landingPanel != null) landingPanel.SetActive(false);
        if (hostPanel != null) hostPanel.SetActive(false);
        if (joinPanel != null) joinPanel.SetActive(true);
    }

    void HideAllPanels()
    {
        if (landingPanel != null) landingPanel.SetActive(false);
        if (hostPanel != null) hostPanel.SetActive(false);
        if (joinPanel != null) joinPanel.SetActive(false);
    }

    void UpdateStatus()
    {
        if (statusLabel == null || lobbyManager == null)
            return;
        if (lobbyManager.CurrentLobby == null)
        {
            statusLabel.text = "Not in lobby";
        }
        else
        {
            string code = lobbyManager.CurrentLobby?.LobbyCode ?? "";
            statusLabel.text = lobbyManager.IsHost ? $"Host | {code}" : $"Client | {code}";
        }
    }
}

