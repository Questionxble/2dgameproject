using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;

public class MultiplayerUI : MonoBehaviour
{
    [Header("Connection UI")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private Button serverButton;
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private Text statusText;
    [SerializeField] private Button disconnectButton;
    
    [Header("Game UI")]
    [SerializeField] private Text playerCountText;
    [SerializeField] private Text networkStatusText;
    
    private NetworkManager networkManager;
    
    private void Start()
    {
        networkManager = NetworkManager.Singleton;
        
        // Setup button listeners
        if (hostButton != null)
            hostButton.onClick.AddListener(() => StartNetworking("Host"));
        if (joinButton != null)
            joinButton.onClick.AddListener(() => StartNetworking("Client"));
        if (serverButton != null)
            serverButton.onClick.AddListener(() => StartNetworking("Server"));
        if (disconnectButton != null)
            disconnectButton.onClick.AddListener(Disconnect);
        
        // Subscribe to network events
        if (networkManager != null)
        {
            networkManager.OnClientConnectedCallback += OnClientConnected;
            networkManager.OnClientDisconnectCallback += OnClientDisconnected;
        }
        
        UpdateUI();
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from events
        if (networkManager != null)
        {
            networkManager.OnClientConnectedCallback -= OnClientConnected;
            networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }
    
    private void Update()
    {
        UpdateNetworkStatus();
    }
    
    private void StartNetworking(string mode)
    {
        if (networkManager == null)
        {
            Debug.LogError("NetworkManager is null!");
            return;
        }
        
        switch (mode)
        {
            case "Host":
                UpdateStatus("Starting as Host...");
                networkManager.StartHost();
                break;
            case "Client":
                UpdateStatus("Connecting as Client...");
                networkManager.StartClient();
                break;
            case "Server":
                UpdateStatus("Starting as Server...");
                networkManager.StartServer();
                break;
        }
        
        // Hide menu panel when connecting
        if (menuPanel != null)
            menuPanel.SetActive(false);
    }
    
    private void Disconnect()
    {
        if (networkManager != null)
        {
            networkManager.Shutdown();
            UpdateStatus("Disconnected");
            
            // Show menu panel again
            if (menuPanel != null)
                menuPanel.SetActive(true);
        }
    }
    
    private void OnClientConnected(ulong clientId)
    {
        UpdateStatus($"Client {clientId} connected");
        UpdateUI();
    }
    
    private void OnClientDisconnected(ulong clientId)
    {
        UpdateStatus($"Client {clientId} disconnected");
        UpdateUI();
    }
    
    private void UpdateUI()
    {
        if (networkManager == null) return;
        
        // Update player count
        if (playerCountText != null)
        {
            int connectedPlayers = networkManager.IsListening ? networkManager.ConnectedClientsIds.Count : 0;
            playerCountText.text = $"Players: {connectedPlayers}/2";
        }
        
        // Show/hide disconnect button
        if (disconnectButton != null)
        {
            disconnectButton.gameObject.SetActive(networkManager.IsListening);
        }
    }
    
    private void UpdateNetworkStatus()
    {
        if (networkStatusText == null || networkManager == null) return;
        
        string status = "Not Connected";
        if (networkManager.IsHost)
            status = "Host";
        else if (networkManager.IsServer)
            status = "Server";
        else if (networkManager.IsClient)
            status = "Client";
        
        networkStatusText.text = $"Status: {status}";
    }
    
    private void UpdateStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
        
        Debug.Log($"[MultiplayerUI] {message}");
    }
}