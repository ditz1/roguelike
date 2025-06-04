using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class GameManager : NetworkBehaviour
{
    [Header("Game Settings")]
    public int MaxRounds = 10;
    
    // Network variables
    private NetworkVariable<int> currentRound = new NetworkVariable<int>(
        1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    
    // Track ready players on server
    private HashSet<ulong> readyPlayers = new HashSet<ulong>();
    
    // Public properties
    public int CurrentRound => currentRound.Value;
    public static GameManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            currentRound.Value = 0;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public override void OnNetworkSpawn()
    {
        currentRound.OnValueChanged += OnRoundChanged;
        
        // Listen for client disconnections to remove them from ready list
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }

    public override void OnNetworkDespawn()
    {
        currentRound.OnValueChanged -= OnRoundChanged;
        
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    private void OnRoundChanged(int oldValue, int newValue)
    {
        Debug.Log($"Round changed from {oldValue} to {newValue}");
        
        // Clear ready players when round advances
        if (IsServer)
        {
            readyPlayers.Clear();
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        readyPlayers.Remove(clientId);
    }

    // Clients call this to ready up
    [ServerRpc(RequireOwnership = false)]
    public void SetPlayerReadyServerRpc(ulong clientId, bool isReady)
    {
        if (isReady)
        {
            readyPlayers.Add(clientId);
            Debug.Log($"Player {clientId} is ready. Ready players: {readyPlayers.Count}/{NetworkManager.Singleton.ConnectedClients.Count}");
        }
        else
        {
            readyPlayers.Remove(clientId);
            Debug.Log($"Player {clientId} is not ready. Ready players: {readyPlayers.Count}/{NetworkManager.Singleton.ConnectedClients.Count}");
        }

        // Check if all players are ready
        CheckAllPlayersReady();
    }

    private void CheckAllPlayersReady()
    {
        int connectedCount = NetworkManager.Singleton.ConnectedClients.Count;
        
        if (readyPlayers.Count >= connectedCount && connectedCount > 0)
        {
            Debug.Log("All players ready! Advancing to next round.");
            NextRound();
        }
    }

    private void NextRound()
    {
        if (currentRound.Value < MaxRounds)
        {
            currentRound.Value++;
        }
        else
        {
            Debug.Log("Max rounds reached!");
        }
    }

    // Helper method for clients to ready up
    public void ReadyUp()
    {
        SetPlayerReadyServerRpc(NetworkManager.Singleton.LocalClientId, true);
    }

    // Helper method for clients to unready
    public void Unready()
    {
        SetPlayerReadyServerRpc(NetworkManager.Singleton.LocalClientId, false);
    }

    public void ResetRounds()
    {
        if (IsServer)
        {
            currentRound.Value = 1;
            readyPlayers.Clear();
        }
    }
}