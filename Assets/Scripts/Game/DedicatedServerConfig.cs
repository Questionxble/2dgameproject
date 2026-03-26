using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

public class DedicatedServerConfig : MonoBehaviour
{
    [Header("Server Configuration")]
    [SerializeField] private bool isDedicatedServer = false;
    [SerializeField] private string serverListenAddress = "0.0.0.0";
    [SerializeField] private ushort serverPort = 7777;
    [SerializeField] private int maxPlayers = 2;
    
    private void Start()
    {
        // Detailed startup logging
        Debug.Log("=== DedicatedServerConfig Starting ===");
        Debug.Log($"Application.isBatchMode: {Application.isBatchMode}");
        Debug.Log($"SystemInfo.graphicsDeviceType: {SystemInfo.graphicsDeviceType}");
        Debug.Log($"Application.platform: {Application.platform}");
        
        // Check if running in dedicated server mode
        if (Application.isBatchMode || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
        {
            isDedicatedServer = true;
            Debug.Log("✅ Running in dedicated server mode");
        }
        else
        {
            Debug.Log("🖥️ Running in client mode");
        }
        
        // Parse command line arguments
        ParseCommandLineArgs();
        
        // Configure network settings if this is a server
        if (isDedicatedServer)
        {
            Debug.Log("🚀 Configuring as dedicated server...");
            ConfigureAsServer();
        }
        else
        {
            Debug.Log("👤 Running as client - no server configuration needed");
        }
    }
    
    private void ParseCommandLineArgs()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-port":
                    if (i + 1 < args.Length && ushort.TryParse(args[i + 1], out ushort port))
                    {
                        serverPort = port;
                        Debug.Log($"Server port set to: {serverPort}");
                    }
                    break;
                case "-maxplayers":
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out int maxP))
                    {
                        maxPlayers = maxP;
                        Debug.Log($"Max players set to: {maxPlayers}");
                    }
                    break;
                case "-server":
                    isDedicatedServer = true;
                    Debug.Log("Dedicated server mode enabled via command line");
                    break;
            }
        }
    }
    
    private void ConfigureAsServer()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null)
        {
            Debug.LogError("NetworkManager not found! Make sure there's a NetworkManager in the scene.");
            return;
        }
        
        // Get the Unity Transport component
        UnityTransport transport = networkManager.GetComponent<UnityTransport>();
        if (transport == null)
        {
            Debug.LogError("UnityTransport component not found on NetworkManager!");
            return;
        }
        
        // Configure transport for server
        transport.SetConnectionData(
            "127.0.0.1",  // This doesn't matter for server
            serverPort,
            serverListenAddress
        );
        
        Debug.Log($"Server configured to listen on {serverListenAddress}:{serverPort}");
        Debug.Log($"Max players: {maxPlayers}");
        
        // Start the server automatically
        StartCoroutine(StartServerAfterDelay());
    }
    
    private System.Collections.IEnumerator StartServerAfterDelay()
    {
        // Wait a frame to ensure everything is initialized
        yield return null;
        
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager != null)
        {
            Debug.Log("Starting dedicated server...");
            bool serverStarted = networkManager.StartServer();
            
            if (serverStarted)
            {
                Debug.Log($"✅ Dedicated server started successfully on port {serverPort}");
                Debug.Log($"Waiting for up to {maxPlayers} players to connect...");
            }
            else
            {
                Debug.LogError("❌ Failed to start dedicated server!");
            }
        }
    }
    
    private void Update()
    {
        // Log server status periodically (only if server and in dedicated mode)
        if (isDedicatedServer && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            if (Time.time % 30f < Time.deltaTime) // Every 30 seconds
            {
                int connectedClients = NetworkManager.Singleton.ConnectedClientsIds.Count;
                Debug.Log($"Server Status - Connected Players: {connectedClients}/{maxPlayers}");
                
                if (connectedClients > 0)
                {
                    string clientIds = string.Join(", ", NetworkManager.Singleton.ConnectedClientsIds);
                    Debug.Log($"Connected Client IDs: {clientIds}");
                }
            }
        }
    }
    
    private void OnApplicationPause(bool pauseStatus)
    {
        // Prevent server from pausing
        if (isDedicatedServer)
        {
            Debug.Log($"Application pause status: {pauseStatus} (ignored for dedicated server)");
        }
    }
    
    private void OnApplicationFocus(bool hasFocus)
    {
        // Log focus changes for debugging
        if (isDedicatedServer)
        {
            Debug.Log($"Application focus: {hasFocus}");
        }
    }
}