using UnityEngine;
using Unity.Netcode;

public class MultiplayerGameManager : NetworkBehaviour
{
    [Header("Network Settings")]
    [SerializeField] private Transform player1SpawnPoint;
    [SerializeField] private Transform player2SpawnPoint;
    [SerializeField] private GameObject playerPrefab;
    
    [Header("UI References (Optional - can be null)")]
    [SerializeField] private UnityEngine.UI.Button hostButton;
    [SerializeField] private UnityEngine.UI.Button clientButton;
    [SerializeField] private UnityEngine.UI.Button serverButton;
    [SerializeField] private GameObject networkUI;
    [SerializeField] private UnityEngine.UI.Text statusText;
    
    [Header("Game Settings")]
    [SerializeField] private int maxPlayers = 2; // Maximum players per session
    
    private void Start()
    {
        // Disable NetworkManager auto-spawn to prevent duplicates
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.NetworkConfig.PlayerPrefab = null;
            Debug.Log("[MultiplayerGameManager] Disabled NetworkManager auto-spawn to prevent duplicates");
        }
        
        // Setup button listeners (optional UI)
        if (hostButton != null)
            hostButton.onClick.AddListener(StartHost);
        if (clientButton != null)
            clientButton.onClick.AddListener(StartClient);
        if (serverButton != null)
            serverButton.onClick.AddListener(StartServer);
            
        // Subscribe to network events
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        
        UpdateStatusText("Ready to start networking...");
    }
    
    public override void OnDestroy()
    {
        // Unsubscribe from network events
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
        
        base.OnDestroy();
    }
    
    private void StartHost()
    {
        UpdateStatusText("Starting as Host...");
        NetworkManager.Singleton.StartHost();
        
        if (networkUI != null)
            networkUI.SetActive(false);
    }
    
    private void StartClient()
    {
        UpdateStatusText("Starting as Client...");
        NetworkManager.Singleton.StartClient();
        
        if (networkUI != null)
            networkUI.SetActive(false);
    }
    
    private void StartServer()
    {
        UpdateStatusText("Starting as Server...");
        NetworkManager.Singleton.StartServer();
        
        if (networkUI != null)
            networkUI.SetActive(false);
    }
    
    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"Client {clientId} connected");
        UpdateStatusText($"Client {clientId} connected ({NetworkManager.Singleton.ConnectedClientsIds.Count}/{maxPlayers} players)");
        
        // Check if we've reached max players
        if (NetworkManager.Singleton.ConnectedClientsIds.Count > maxPlayers)
        {
            Debug.LogWarning($"Max players ({maxPlayers}) exceeded. Disconnecting client {clientId}");
            NetworkManager.Singleton.DisconnectClient(clientId);
            return;
        }
        
        // If we're the server, spawn the player
        if (IsServer)
        {
            SpawnPlayerForClient(clientId);
        }
    }
    
    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"Client {clientId} disconnected");
        UpdateStatusText($"Client {clientId} disconnected");
    }
    
    [ServerRpc]
    private void SpawnPlayerServerRpc(ulong clientId)
    {
        SpawnPlayerForClient(clientId);
    }
    
    private void SpawnPlayerForClient(ulong clientId)
    {
        if (!IsServer) return;
        
        // Determine spawn position based on client ID
        Vector3 spawnPosition = Vector3.zero;
        if (clientId == 0) // Host/Server
        {
            spawnPosition = player1SpawnPoint != null ? player1SpawnPoint.position : Vector3.zero;
        }
        else // First client
        {
            spawnPosition = player2SpawnPoint != null ? player2SpawnPoint.position : new Vector3(3, 0, 0);
        }
        
        // Spawn player
        if (playerPrefab != null)
        {
            GameObject playerInstance = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
            NetworkObject networkObject = playerInstance.GetComponent<NetworkObject>();
            
            if (networkObject != null)
            {
                networkObject.SpawnAsPlayerObject(clientId);
                Debug.Log($"Spawned player for client {clientId} at position {spawnPosition}");
            }
            else
            {
                Debug.LogError("Player prefab doesn't have a NetworkObject component!");
                Destroy(playerInstance);
            }
        }
        else
        {
            Debug.LogError("Player prefab is not assigned!");
        }
    }
    
    private void UpdateStatusText(string message)
    {
        // Update UI text if available (optional)
        if (statusText != null)
            statusText.text = message;
        
        // Always log to console (works with or without UI)
        Debug.Log($"[MultiplayerGameManager] {message}");
    }
    
    // Public method to manually spawn players if needed
    public void SpawnAllPlayers()
    {
        if (!IsServer) return;
        
        foreach (var client in NetworkManager.Singleton.ConnectedClientsIds)
        {
            SpawnPlayerForClient(client);
        }
    }
    
    // UI callback for leaving the network
    public void LeaveGame()
    {
        if (NetworkManager.Singleton.IsHost)
        {
            NetworkManager.Singleton.Shutdown();
            UpdateStatusText("Stopped hosting");
        }
        else if (NetworkManager.Singleton.IsClient)
        {
            NetworkManager.Singleton.Shutdown();
            UpdateStatusText("Disconnected from server");
        }
        
        if (networkUI != null)
            networkUI.SetActive(true);
    }
    
    // Respawn functionality for when players die
    [ServerRpc(RequireOwnership = false)]
    public void RespawnPlayerServerRpc(ulong clientId)
    {
        if (!IsServer) return;
        
        // Find the existing player to respawn
        foreach (var player in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (player.ClientId == clientId && player.PlayerObject != null)
            {
                RespawnAtPosition(player.PlayerObject.gameObject, clientId);
                break;
            }
        }
    }
    
    private void RespawnAtPosition(GameObject playerObject, ulong clientId)
    {
        if (playerObject == null) return;
        
        // Determine spawn position based on client ID
        Vector3 spawnPosition = Vector3.zero;
        if (clientId == 0) // Host/Server
        {
            spawnPosition = player1SpawnPoint != null ? player1SpawnPoint.position : Vector3.zero;
        }
        else // First client
        {
            spawnPosition = player2SpawnPoint != null ? player2SpawnPoint.position : new Vector3(3, 0, 0);
        }
        
        // Move player to spawn position and reset health
        playerObject.transform.position = spawnPosition;
        
        // Reset player health and enable movement
        PlayerMovement playerMovement = playerObject.GetComponent<PlayerMovement>();
        if (playerMovement != null)
        {
            playerMovement.RespawnPlayer(); // We'll need to add this method to PlayerMovement
        }
        
        Debug.Log($"Respawned player for client {clientId} at position {spawnPosition}");
    }
}