using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

public class NetworkBootstrap : MonoBehaviour
{
    [Header("Autostart")] 
    [SerializeField] private bool autoStartAsHost = false;

    [Header("Transport")] 
    [SerializeField] private UnityTransport transport;

    void Awake()
    {
        if (transport == null)
        {
            transport = FindObjectOfType<UnityTransport>();
        }
    }

    void Start()
    {
        if (autoStartAsHost)
        {
            if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
            {
                NetworkManager.Singleton.StartHost();
            }
        }
    }
}


