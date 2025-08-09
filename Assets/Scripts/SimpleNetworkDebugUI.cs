using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

// Minimal on-screen UI for starting a simple Host/Client session using Unity Transport (no UGS).
// Add this to any GameObject in your scene. Use in Editor as Host and in Build as Client.
public class SimpleNetworkDebugUI : MonoBehaviour
{
    [Header("Transport")]
    [SerializeField] private UnityTransport transport;

    [Header("Connection Settings")] 
    [SerializeField] private string address = "127.0.0.1";
    [SerializeField] private ushort port = 7777;

    void Awake()
    {
        if (transport == null)
        {
            transport = FindObjectOfType<UnityTransport>();
            if (transport == null && NetworkManager.Singleton != null)
            {
                transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                if (transport == null)
                {
                    transport = NetworkManager.Singleton.gameObject.AddComponent<UnityTransport>();
                }
            }
        }
    }

    void OnGUI()
    {
        const int panelWidth = 300;
        const int panelHeight = 140;
        var rect = new Rect(10, 10, panelWidth, panelHeight);
        GUI.Box(rect, "Simple Network");

        GUILayout.BeginArea(new Rect(20, 35, panelWidth - 20, panelHeight - 40));

        GUILayout.BeginHorizontal();
        GUILayout.Label("Address", GUILayout.Width(60));
        address = GUILayout.TextField(address, GUILayout.Width(140));
        GUILayout.Label("Port", GUILayout.Width(40));
        ushort.TryParse(GUILayout.TextField(port.ToString(), GUILayout.Width(60)), out port);
        GUILayout.EndHorizontal();

        GUILayout.Space(6);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Start Host", GUILayout.Height(28)))
        {
            EnsureTransport();
            // For host, bind to all interfaces for incoming; address parameter is used by client side, safe to keep as current.
            transport.SetConnectionData(address, port, "0.0.0.0");
            NetworkManager.Singleton.StartHost();
        }
        if (GUILayout.Button("Start Client", GUILayout.Height(28)))
        {
            EnsureTransport();
            transport.SetConnectionData(address, port);
            NetworkManager.Singleton.StartClient();
        }
        if (GUILayout.Button("Shutdown", GUILayout.Height(28)))
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.Shutdown();
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.EndArea();
    }

    void EnsureTransport()
    {
        if (transport == null)
        {
            if (NetworkManager.Singleton == null)
            {
                Debug.LogError("SimpleNetworkDebugUI: No NetworkManager found in scene.");
                return;
            }
            transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport == null)
            {
                transport = NetworkManager.Singleton.gameObject.AddComponent<UnityTransport>();
            }
        }
    }
}


