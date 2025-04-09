using Unity.Netcode;
using UnityEngine;

public class BootstrapTransport : MonoBehaviour
{
    void Awake()
    {
        var transport = GetComponent<NetworkTransport>();
        NetworkManager.Singleton.NetworkConfig.NetworkTransport = transport;
    }
}
