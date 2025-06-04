using Unity.Netcode;
using UnityEngine;
using Steamworks;
using System.Collections.Generic;
using System;
using Unity.VisualScripting;



namespace HelloWorld
{
    public class HelloWorldManager : MonoBehaviour
    {
        [SerializeField] GameObject lobby_canvas;
        [SerializeField] GameObject connect_screen;

        [SerializeField] GameObject hud_canvas;

        private NetworkManager m_NetworkManager;

        public List<Tuple<CSteamID, string>> friendsList = new List<Tuple<CSteamID, string>>();

        public GameObject friend_panel_prefab;

        CSteamID chosen_host;
        public static string chosen_host_name = "none"; // this is the name of the host that the player has chosen to connect to

        void Awake()
        {
            m_NetworkManager = GetComponent<NetworkManager>();
            lobby_canvas.SetActive(true);
            connect_screen.SetActive(false);
            hud_canvas.SetActive(false);
            chosen_host_name = "none"; // reset the chosen host name
            chosen_host = new CSteamID(0);
            // this could probably break if player resets lobby
        }

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, 300));
            if (!m_NetworkManager.IsClient && !m_NetworkManager.IsServer)
            {
                //StartButtons();
            }
            else
            {
                //StatusLabels();
            }
            GUILayout.EndArea();
        }

        bool IsSteamReady()
        {
            return SteamAPI.IsSteamRunning();
        }

        public void StartHost()
        {
            if (IsSteamReady())
            {
                m_NetworkManager.StartHost();
                SteamFriends.SetRichPresence("status", "Hosting");
                lobby_canvas.SetActive(false);
                hud_canvas.SetActive(true);
            }
            else
            {
                Debug.LogWarning("Steam is not running.");
            }

        }

        void Update()
        {
            if (chosen_host_name != "none")
            {
                Debug.Log("Connecting to host: " + chosen_host_name);
                chosen_host = friendsList.Find(friend => friend.Item2 == chosen_host_name).Item1;
                ConnectToHost(chosen_host);
                connect_screen.SetActive(false);
                hud_canvas.SetActive(true);
                chosen_host_name = "none"; // reset the chosen host name after connecting
            }
           
        }

        public void StartClient()
        {
            if (IsSteamReady())
            {

                int friendCount = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);
                for (int i = 0; i < friendCount; i++)
                {
                    CSteamID friendSteamID = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate);
                    string friendName = SteamFriends.GetFriendPersonaName(friendSteamID);
                    //EPersonaState friendState = SteamFriends.GetFriendPersonaState(friendSteamID);

                    FriendGameInfo_t gameInfo;
                    if (SteamFriends.GetFriendGamePlayed(friendSteamID, out gameInfo))
                    {
                        if (gameInfo.m_gameID.AppID() == SteamUtils.GetAppID())
                        {
                            Debug.Log($"{friendName} ({friendSteamID}) is in-game.");
                            friendsList.Add(new Tuple<CSteamID, string>(friendSteamID, friendName));
                        }
                    }
                }

                lobby_canvas.SetActive(false);
                connect_screen.SetActive(true);

                if (friendsList.Count > 0)
                {
                    for (int i = 0; i < friendsList.Count; i++)
                    {
                        GameObject friend_panel = Instantiate(friend_panel_prefab, connect_screen.transform);
                        friend_panel.transform.Find("name").GetComponent<TMPro.TextMeshProUGUI>().text = friendsList[i].Item2;
                    }
                        
                }
                else
                {
                    connect_screen.SetActive(false);
                    lobby_canvas.SetActive(true);
                    Debug.Log("No friends are currently in-game.");
                }


            }
            else
            {
                Debug.LogError("Steam is not running.");
            }
        }

        public void SelectFriend(GameObject friendPanel)
        { // this is a button on the friend panel that will connect to the host
            var text = friendPanel.transform.Find("name").GetComponent<TMPro.TextMeshProUGUI>().text;
            Debug.Log("Selected friend: " + text);
            chosen_host_name = text;
        }

        public void ConnectToHost(CSteamID hostID)
        {
            if (IsSteamReady())
            {
                chosen_host = hostID;
                Debug.Log("Connecting to host: " + hostID);
                // Set the chosen host's Steam ID in the transport component
                var steamTransport = m_NetworkManager.NetworkConfig.NetworkTransport;
                var steamIDProperty = steamTransport.GetType().GetField("ConnectToSteamID");
                if (steamIDProperty != null)
                {
                    steamIDProperty.SetValue(steamTransport, hostID.m_SteamID);
                    Debug.Log("Set ConnectToSteamID to: " + hostID.m_SteamID);
                }
                m_NetworkManager.StartClient();
            }
            else
            {
                Debug.LogError("Steam is not running.");
            }
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
