using Unity.Netcode;
using UnityEngine;
using Steamworks;

namespace HelloWorld
{
    public class HelloWorldManager : MonoBehaviour
    {
        private NetworkManager m_NetworkManager;

        void Awake()
        {
            m_NetworkManager = GetComponent<NetworkManager>();
        }

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, 300));
            if (!m_NetworkManager.IsClient && !m_NetworkManager.IsServer)
            {
                StartButtons();
            }
            else
            {
                StatusLabels();
            }
            GUILayout.EndArea();
        }

        bool IsSteamReady()
        {
            return SteamAPI.IsSteamRunning();
        }

        void StartButtons()
        {
            if (GUILayout.Button("Host"))
            {
                if (IsSteamReady())
                {
                    m_NetworkManager.StartHost();
                }
                else
                {
                    Debug.LogWarning("Steam is not running.");
                }
            }

            if (GUILayout.Button("Client"))
            {
                Debug.Log("client start");
                if (IsSteamReady())
                {
                    var transport = m_NetworkManager.NetworkConfig.NetworkTransport;
                    // Use reflection to set ConnectToSteamID if the type has it
                    var field = transport.GetType().GetField("ConnectToSteamID");
                    if (field != null)
                    {
                        field.SetValue(transport, 76561198153860112);
                        Debug.Log("ConnectToSteamID set via reflection.");
                    }
                    else
                    {
                        Debug.LogWarning("Could not set ConnectToSteamID: transport doesn't expose the field.");
                    }

                    m_NetworkManager.StartClient();
                } else {
                    Debug.Log("Steam is not running.");
                }
            }

            if (GUILayout.Button("Server"))
            {
                if (IsSteamReady()) m_NetworkManager.StartServer();
            }
        }

        void StatusLabels()
        {
            var mode = m_NetworkManager.IsHost ?
                "Host" : m_NetworkManager.IsServer ? "Server" : "Client";

            GUILayout.Label("Transport: " +
                m_NetworkManager.NetworkConfig.NetworkTransport.GetType().Name);
            GUILayout.Label("Mode: " + mode);
        }
    }
}
