using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

public class MenuController : MonoBehaviour
{
    [Header("Scene Management")]
    [SerializeField] private string gameSceneName = "StartingScene";
    
    [Header("Menu State")]
    [SerializeField] private bool isInMainMenu = true;
    
    private ClientDebugger clientDebugger;
    
    void Start()
    {
        // Find the ClientDebugger in the scene
        clientDebugger = FindFirstObjectByType<ClientDebugger>();
        
        // Make sure we're not connected to any network when starting menu
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }
        
        Debug.Log("Menu Scene loaded. Use ClientDebugger to connect to server.");
    }
    
    void Update()
    {
        // Check if we've connected to the server
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient)
        {
            // Once connected, automatically transition to game scene
            if (isInMainMenu)
            {
                Debug.Log("Connected to server! Transitioning to game scene...");
                LoadGameScene();
            }
        }
        
        // Allow manual scene transition for testing
        if (Input.GetKeyDown(KeyCode.G))
        {
            Debug.Log("Manual game scene transition (for testing)");
            LoadGameScene();
        }
    }
    
    public void LoadGameScene()
    {
        if (!isInMainMenu) return;
        
        isInMainMenu = false;
        
        // Load the game scene
        Debug.Log($"Loading game scene: {gameSceneName}");
        SceneManager.LoadScene(gameSceneName);
    }
    
    public void QuitGame()
    {
        Debug.Log("Quitting game...");
        Application.Quit();
        
        // For editor testing
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }
    
    public void DisconnectAndReturnToMenu()
    {
        // Disconnect from network
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }
        
        // Return to menu scene
        SceneManager.LoadScene("MenuScene");
    }
    
    void OnGUI()
    {
        // Simple menu overlay (can be enhanced by your collaborator's UI)
        GUILayout.BeginArea(new Rect(50, Screen.height - 150, 300, 100));
        GUILayout.BeginVertical("box");
        
        GUILayout.Label("=== GAME MENU ===");
        
        if (GUILayout.Button("Load Game Scene (G)"))
        {
            LoadGameScene();
        }
        
        if (GUILayout.Button("Quit Game"))
        {
            QuitGame();
        }
        
        GUILayout.EndVertical();
        GUILayout.EndArea();
        
        // Instructions
        GUILayout.BeginArea(new Rect(Screen.width - 350, 50, 300, 200));
        GUILayout.BeginVertical("box");
        
        GUILayout.Label("=== INSTRUCTIONS ===");
        GUILayout.Label("1. Click 'CONNECT TO SERVER' button");
        GUILayout.Label("2. Game will auto-load when connected");
        GUILayout.Label("3. Or press 'G' to load manually");
        GUILayout.Label("");
        GUILayout.Label("F11: Toggle debug window");
        
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient)
        {
            GUILayout.Label("✅ CONNECTED TO SERVER");
        }
        else
        {
            GUILayout.Label("❌ Not connected");
        }
        
        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
}