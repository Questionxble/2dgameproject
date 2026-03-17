using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Simple Dedicated Server Launcher - automatically starts the game as a multiplayer client
/// for dedicated server environments without complex transport configuration
/// </summary>
public class DedicatedServerLauncher : MonoBehaviour
{
    [Header("Server Configuration")]
    [SerializeField] private bool autoConnectOnStart = true;
    [SerializeField] private float connectionDelay = 1f;
    
    private void Awake()
    {
        // Configure for dedicated server environment
        ConfigureForDedicatedServer();
    }
    
    private void Start()
    {
        if (autoConnectOnStart)
        {
            // Wait a moment for other systems to initialize
            Invoke("ConnectToGameServer", connectionDelay);
        }
    }
    
    private void ConfigureForDedicatedServer()
    {
        // Set target framerate for server performance
        Application.targetFrameRate = 60;
        
        // Disable VSync for server
        QualitySettings.vSyncCount = 0;
        
        // Parse command line arguments for auto-connect override
        ParseCommandLineArgs();
        
        Debug.Log($"[DedicatedServerLauncher] Configured for dedicated server mode");
    }
    
    private void ParseCommandLineArgs()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLower())
            {
                case "-noautoconnect":
                    autoConnectOnStart = false;
                    Debug.Log("[DedicatedServerLauncher] Auto-connect disabled via command line");
                    break;
                case "-dedicatedserver":
                case "-server":
                    autoConnectOnStart = true;
                    Debug.Log("[DedicatedServerLauncher] Auto-connect enabled via command line");
                    break;
            }
        }
    }
    
    private void ConnectToGameServer()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[DedicatedServerLauncher] NetworkManager not found!");
            return;
        }
        
        // Start as client (will use whatever connection settings are configured on NetworkManager)
        bool success = NetworkManager.Singleton.StartClient();
        
        if (success)
        {
            Debug.Log("[DedicatedServerLauncher] Successfully started as client");
        }
        else
        {
            Debug.LogError("[DedicatedServerLauncher] Failed to start as client");
        }
    }
    
    private void OnApplicationQuit()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
        {
            NetworkManager.Singleton.Shutdown();
        }
    }
}