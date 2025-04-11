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
                    // Get the transport component
                    var steamTransport = m_NetworkManager.NetworkConfig.NetworkTransport;

                    // SteamID MUST be a CSteamID object and set in this way
                    var steamIDProperty = steamTransport.GetType().GetField("ConnectToSteamID");
                    if (steamIDProperty != null)
                    {
                        CSteamID steamID = new CSteamID(76561198153860112);
                        // Extract the uint64 value from the CSteamID object
                        ulong steamIDValue = steamID.m_SteamID;
                        steamIDProperty.SetValue(steamTransport, steamIDValue);
                        Debug.Log("Set Steam ID: " + steamIDValue);
                    }

                    // Start client with additional logging
                    Debug.Log("Starting client...");
                    m_NetworkManager.StartClient();
                    Debug.Log("StartClient method called");
                } else {
                    Debug.LogError("Steam is not running.");
                }
                Debug.Log("client start 2");
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
