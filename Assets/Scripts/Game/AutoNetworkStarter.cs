using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Auto-startup component that detects NetworkManager and automatically connects to the dedicated server
/// All clients connect to the VM server - only the VM should run as Host
/// </summary>
public class AutoNetworkStarter : MonoBehaviour
{
    [Header("Auto-Start Settings")]
    [SerializeField] private bool enableAutoStart = true;
    [SerializeField] private float startDelay = 2f; // Delay before starting
    [SerializeField] private NetworkMode preferredMode = NetworkMode.Client; // Default to Client for dedicated server architecture
    
    [Header("Dedicated Server Detection")]
    [SerializeField] private bool forceClientMode = true; // Force all builds to be clients unless overridden
    
    public enum NetworkMode
    {
        Host,   // Start as host (only for the VM server)
        Client, // Start as client (for all players connecting to the VM)
        Server  // Start as server only (headless server mode)
    }
    
    private bool hasStarted = false;
    
    private void Start()
    {
        if (enableAutoStart && !hasStarted)
        {
            StartCoroutine(CheckAndStartNetwork());
        }
    }
    
    private System.Collections.IEnumerator CheckAndStartNetwork()
    {
        yield return new WaitForSeconds(startDelay);
        
        // Parse command line arguments to determine role
        ParseCommandLineArguments();
        
        // Unity Editor special handling for local testing
        #if UNITY_EDITOR
        if (forceClientMode && !NetworkManager.Singleton.IsListening)
        {
            // Check if another instance is already running as host
            bool hostExists = NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer;
            if (!hostExists)
            {
                // First instance - start as Host for local testing
                preferredMode = NetworkMode.Host;
                Debug.Log("[AutoNetworkStarter] Unity Editor: First instance detected - starting as Host for local testing");
            }
            else
            {
                // Second instance - stay as Client
                Debug.Log("[AutoNetworkStarter] Unity Editor: Host exists - starting as Client");
            }
        }
        #endif
        
        // Check if we have NetworkManager
        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsListening)
        {
            Debug.Log($"[AutoNetworkStarter] NetworkManager detected - starting as {preferredMode}");
            
            bool success = false;
            
            switch (preferredMode)
            {
                case NetworkMode.Host:
                    success = NetworkManager.Singleton.StartHost();
                    break;
                case NetworkMode.Client:
                    success = NetworkManager.Singleton.StartClient();
                    break;
                case NetworkMode.Server:
                    success = NetworkManager.Singleton.StartServer();
                    break;
            }
            
            hasStarted = true;
            
            if (success)
            {
                Debug.Log($"[AutoNetworkStarter] Successfully started as {preferredMode}");
            }
            else
            {
                Debug.LogError($"[AutoNetworkStarter] Failed to start as {preferredMode}");
                
                // Unity Editor fallback: if Host failed, try Client
                #if UNITY_EDITOR
                if (preferredMode == NetworkMode.Host)
                {
                    Debug.Log("[AutoNetworkStarter] Unity Editor: Host failed, attempting to start as Client...");
                    success = NetworkManager.Singleton.StartClient();
                    if (success)
                    {
                        Debug.Log("[AutoNetworkStarter] Successfully started as fallback Client");
                    }
                    else
                    {
                        Debug.LogError("[AutoNetworkStarter] Failed to start as fallback Client");
                    }
                }
                #endif
                
                // For clients, don't try to fallback to host - that defeats the purpose
                if (preferredMode == NetworkMode.Client)
                {
                    Debug.LogError("[AutoNetworkStarter] Client connection failed - check server IP and port configuration");
                    Debug.LogError("[AutoNetworkStarter] Make sure the dedicated server (VM) is running and accessible");
                }
            }
        }
        else if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[AutoNetworkStarter] No NetworkManager found in scene!");
        }
        else if (NetworkManager.Singleton.IsListening)
        {
            Debug.Log("[AutoNetworkStarter] NetworkManager is already listening");
        }
    }
    
    private void ParseCommandLineArguments()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        bool commandLineOverride = false;
        
        Debug.Log($"[AutoNetworkStarter] Parsing {args.Length} command line arguments");
        for (int i = 0; i < args.Length; i++)
        {
            Debug.Log($"[AutoNetworkStarter] Arg {i}: {args[i]}");
        }
        
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLower())
            {
                case "-server":
                case "-dedicatedserver":
                    preferredMode = NetworkMode.Server;
                    forceClientMode = false;
                    commandLineOverride = true;
                    Debug.Log("[AutoNetworkStarter] Command line: Server mode enabled");
                    break;
                case "-host":
                    preferredMode = NetworkMode.Host;
                    forceClientMode = false;
                    commandLineOverride = true;
                    Debug.Log("[AutoNetworkStarter] Command line: Host mode enabled");
                    break;
                case "-client":
                    preferredMode = NetworkMode.Client;
                    commandLineOverride = true;
                    Debug.Log("[AutoNetworkStarter] Command line: Client mode enabled");
                    break;
            }
        }
        
        // Apply force client mode only if no command line override
        if (forceClientMode && !commandLineOverride)
        {
            Debug.Log("[AutoNetworkStarter] Force client mode enabled - connecting to dedicated server");
            preferredMode = NetworkMode.Client;
        }
    }
}