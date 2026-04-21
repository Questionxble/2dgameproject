using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine.InputSystem;

public class ClientDebugger : MonoBehaviour
{
    [Header("Debug Settings")]
    [SerializeField] private bool enableDebugUI = true;
    [SerializeField] private bool verboseLogging = true;
    [SerializeField] private KeyCode toggleDebugKey = KeyCode.F11;
    
    [Header("UI State")]
    [SerializeField] private bool isDebugVisible = false;
    [SerializeField] private bool isConnected = false;
    
    private NetworkManager networkManager;
    private UnityTransport transport;
    private string debugInfo = "";
    private bool isConnecting = false;
    private float connectionStartTime;
    private string playerNameInput = PlayerSessionSettings.DefaultPlayerName;
    
    // UI Layout
    private Rect debugWindowRect;
    private Rect centerButtonRect;
    private GUIStyle centerButtonStyle;
    private GUIStyle debugWindowStyle;
    private GUIStyle debugTextStyle;
    private GUIStyle playerNameLabelStyle;
    private GUIStyle playerNameFieldStyle;
    private Texture2D playerNameFieldBackground;
    private Texture2D playerNameFieldFocusedBackground;
    
    void Start()
    {
        // Disable ClientDebugger on dedicated servers
        if (Application.isBatchMode || Application.platform == RuntimePlatform.LinuxServer)
        {
            Debug.Log("[ClientDebug] Disabled ClientDebugger on dedicated server");
            this.enabled = false;
            return;
        }
        
        networkManager = NetworkManager.Singleton;
        if (networkManager != null)
        {
            transport = networkManager.GetComponent<UnityTransport>();
            
            // Subscribe to connection events
            networkManager.OnClientConnectedCallback += OnClientConnected;
            networkManager.OnClientDisconnectCallback += OnClientDisconnected;
            
            LogDebug("ClientDebugger initialized");
            LogCurrentNetworkSettings();
        }
        else
        {
            LogDebug("ERROR: NetworkManager not found!");
        }

        playerNameInput = GetInitialPlayerNameInput();
        
        InitializeUI();
    }
    
    void InitializeUI()
    {
        // Debug window (large, toggleable)
        debugWindowRect = new Rect(50, 50, 500, 400);
        
        // Center connect button (large, screen center)
        float buttonWidth = 320; // Increased from 200 to fit "CONNECT TO SERVER" text
        float buttonHeight = 60;
        centerButtonRect = new Rect(
            (Screen.width - buttonWidth) / 2,
            (Screen.height - buttonHeight) / 2,
            buttonWidth,
            buttonHeight
        );
    }
    
    void Update()
    {
        UpdateDebugInfo();
        
        // Don't handle input on dedicated servers
        if (!Application.isBatchMode && Application.platform != RuntimePlatform.LinuxServer)
        {
            HandleInput();
        }
        
        // Monitor connection timeout
        if (isConnecting && Time.time - connectionStartTime > 10f)
        {
            LogDebug("Connection timeout - stopping connection attempt");
            isConnecting = false;
            if (networkManager.IsClient)
            {
                networkManager.Shutdown();
            }
        }
    }
    
    void HandleInput()
    {
        // Toggle debug window with F11
        if (Input.GetKeyDown(toggleDebugKey))
        {
            isDebugVisible = !isDebugVisible;
            LogDebug($"Debug window toggled: {(isDebugVisible ? "visible" : "hidden")}");
        }
        
        // Handle ESC key for stopping player movement only (not pause)
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            HandleEscapeKey();
        }
        
        // Quick connect with 'C' key (for debugging)
        if (Input.GetKeyDown(KeyCode.C) && !isConnected && !isConnecting)
        {
            AttemptConnection();
        }
    }
    
    void HandleEscapeKey()
    {
        // Stop all player movement/actions without pausing the game
        var playerMovement = FindFirstObjectByType<PlayerMovement>();
        if (playerMovement != null)
        {
            // Only stop movement if this is the local player
            if (playerMovement.IsOwner)
            {
                LogDebug("ESC pressed - stopping player movement/actions");
                // The actual movement stopping will be handled by PlayerMovement script
                // We just send a signal or set a flag
                StopAllPlayerActions();
            }
        }
    }
    
    void StopAllPlayerActions()
    {
        // Find and disable player input temporarily
        var playerInput = FindFirstObjectByType<PlayerInput>();
        if (playerInput != null)
        {
            // Temporarily disable input (can be re-enabled by menu system later)
            playerInput.enabled = false;
            LogDebug("Player input disabled");
            
            // Re-enable after a short delay (or let menu system handle it)
            Invoke("ReEnablePlayerInput", 0.1f);
        }
    }
    
    void ReEnablePlayerInput()
    {
        var playerInput = FindFirstObjectByType<PlayerInput>();
        if (playerInput != null)
        {
            playerInput.enabled = true;
            LogDebug("Player input re-enabled");
        }
    }
    
    void OnDestroy()
    {
        if (networkManager != null)
        {
            networkManager.OnClientConnectedCallback -= OnClientConnected;
            networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }
    
    void OnApplicationFocus(bool hasFocus)
    {
        // Update center button position when window size changes
        if (hasFocus)
        {
            InitializeUI();
        }
    }
    
    private void LogCurrentNetworkSettings()
    {
        if (transport != null)
        {
            LogDebug($"Transport Settings:");
            LogDebug($"  - Address: {transport.ConnectionData.Address}");
            LogDebug($"  - Port: {transport.ConnectionData.Port}");
            LogDebug($"  - Server Listen Address: {transport.ConnectionData.ServerListenAddress}");
        }
    }
    
    private void AttemptConnection()
    {
        LogDebug("=== Attempting Connection ===");
        LogCurrentNetworkSettings();

        PlayerSessionSettings.LocalPlayerName = playerNameInput;
        LogDebug($"Using player name: {PlayerSessionSettings.LocalPlayerName}");
        
        isConnecting = true;
        connectionStartTime = Time.time;
        
        bool started = networkManager.StartClient();
        LogDebug($"StartClient() returned: {started}");
    }
    
    private void AttemptDisconnection()
    {
        LogDebug("=== Disconnecting ===");
        if (networkManager != null && networkManager.IsListening)
        {
            networkManager.Shutdown();
            isConnecting = false;
            isConnected = false;
        }
    }
    
    private void OnClientConnected(ulong clientId)
    {
        LogDebug($"✅ Client connected! Client ID: {clientId}");
        isConnecting = false;
        isConnected = true;
    }
    
    private void OnClientDisconnected(ulong clientId)
    {
        LogDebug($"❌ Client disconnected. Client ID: {clientId}");
        isConnecting = false;
        isConnected = false;
        
        // Clean up connection state to allow fresh reconnect
        if (networkManager != null && !networkManager.IsListening)
        {
            // Reset any stuck connection states
            networkManager.Shutdown();
        }
    }
    
    private void UpdateDebugInfo()
    {
        if (networkManager == null) return;
        
        debugInfo = $"=== CLIENT DEBUG INFO ===\n\n";
        debugInfo += $"Network Status: {GetConnectionStatus()}\n";
        debugInfo += $"Is Client: {networkManager.IsClient}\n";
        debugInfo += $"Is Connected: {networkManager.IsConnectedClient}\n";
        debugInfo += $"Is Listening: {networkManager.IsListening}\n";
        debugInfo += $"Local Client ID: {networkManager.LocalClientId}\n\n";
        
        if (transport != null)
        {
            debugInfo += $"=== CONNECTION SETTINGS ===\n";
            debugInfo += $"Target Server: {transport.ConnectionData.Address}:{transport.ConnectionData.Port}\n";
            debugInfo += $"Protocol: UDP (Unity Netcode)\n\n";
        }
        
        debugInfo += $"=== CONTROLS ===\n";
        debugInfo += $"F11: Toggle this debug window\n";
        debugInfo += $"C: Quick connect (debug only)\n";
        debugInfo += $"ESC: Stop player movement only\n\n";
        
        if (isConnecting)
        {
            debugInfo += $"⏳ CONNECTING... ({Time.time - connectionStartTime:F1}s)\n";
        }
        
        if (isConnected)
        {
            debugInfo += $"✅ CONNECTED TO SERVER\n";
        }
    }
    
    private string GetConnectionStatus()
    {
        if (networkManager.IsHost) return "Host";
        if (networkManager.IsServer) return "Server";
        if (networkManager.IsConnectedClient) return "Connected Client";
        if (networkManager.IsClient) return "Client (Connecting...)";
        return "Disconnected";
    }
    
    private void LogDebug(string message)
    {
        if (verboseLogging)
        {
            Debug.Log($"[ClientDebug] {message}");
        }
    }
    
    void OnGUI()
    {
        // Don't draw GUI on dedicated servers (no display)
        if (Application.isBatchMode || Application.platform == RuntimePlatform.LinuxServer) return;
        
        if (!enableDebugUI) return;
        
        InitializeGUIStyles();
        
        // Draw the large center connect/disconnect button (when not connected or connecting)
        if (!isConnected && !isDebugVisible)
        {
            DrawCenterConnectButton();
        }
        
        // Draw the debug window (when connected or debug mode enabled)
        if (isDebugVisible || isConnected)
        {
            DrawDebugWindow();
        }
    }
    
    private void InitializeGUIStyles()
    {
        if (centerButtonStyle == null)
        {
            centerButtonStyle = new GUIStyle(GUI.skin.button);
            centerButtonStyle.fontSize = 24;
            centerButtonStyle.fontStyle = FontStyle.Bold;
            centerButtonStyle.normal.textColor = Color.white;
            centerButtonStyle.hover.textColor = Color.yellow;
        }
        
        if (debugWindowStyle == null)
        {
            debugWindowStyle = new GUIStyle(GUI.skin.window);
            debugWindowStyle.fontSize = 14;
            debugWindowStyle.fontStyle = FontStyle.Bold;
        }
        
        if (debugTextStyle == null)
        {
            debugTextStyle = new GUIStyle(GUI.skin.label);
            debugTextStyle.fontSize = 12;
            debugTextStyle.fontStyle = FontStyle.Normal;
            debugTextStyle.normal.textColor = Color.white;
            debugTextStyle.wordWrap = true;
        }

        if (playerNameLabelStyle == null)
        {
            playerNameLabelStyle = new GUIStyle(GUI.skin.label);
            playerNameLabelStyle.fontSize = 16;
            playerNameLabelStyle.fontStyle = FontStyle.Bold;
            playerNameLabelStyle.alignment = TextAnchor.MiddleCenter;
            playerNameLabelStyle.normal.textColor = Color.white;
        }

        if (playerNameFieldStyle == null)
        {
            playerNameFieldStyle = new GUIStyle(GUI.skin.textField);
            playerNameFieldStyle.fontSize = 18;
            playerNameFieldStyle.alignment = TextAnchor.MiddleLeft;
            playerNameFieldStyle.padding = new RectOffset(10, 10, 6, 6);

            playerNameFieldBackground = CreateSolidTexture(new Color(0.12f, 0.12f, 0.12f, 0.95f));
            playerNameFieldFocusedBackground = CreateSolidTexture(new Color(0.18f, 0.18f, 0.18f, 1f));

            playerNameFieldStyle.normal.background = playerNameFieldBackground;
            playerNameFieldStyle.hover.background = playerNameFieldFocusedBackground;
            playerNameFieldStyle.focused.background = playerNameFieldFocusedBackground;
            playerNameFieldStyle.active.background = playerNameFieldFocusedBackground;

            playerNameFieldStyle.normal.textColor = Color.white;
            playerNameFieldStyle.hover.textColor = Color.white;
            playerNameFieldStyle.focused.textColor = Color.white;
            playerNameFieldStyle.active.textColor = Color.white;
        }
    }
    
    private void DrawCenterConnectButton()
    {
        Rect nameLabelRect = new Rect(
            centerButtonRect.x,
            centerButtonRect.y - 68,
            centerButtonRect.width,
            24
        );

        Rect nameFieldRect = new Rect(
            centerButtonRect.x,
            centerButtonRect.y - 40,
            centerButtonRect.width,
            32
        );

        GUI.Label(nameLabelRect, "Player Name", playerNameLabelStyle);
        playerNameInput = DrawPlayerNameField(nameFieldRect, "CenterPlayerNameField");

        // Large center connect button
        string buttonText = isConnecting ? "Connecting..." : "CONNECT TO SERVER";
        
        GUI.enabled = !isConnecting;
        if (GUI.Button(centerButtonRect, buttonText, centerButtonStyle))
        {
            AttemptConnection();
        }
        GUI.enabled = true;
        
        // Show connection info below button
        Rect infoRect = new Rect(
            centerButtonRect.x - 100,
            centerButtonRect.y + centerButtonRect.height + 20,
            centerButtonRect.width + 200,
            60
        );
        
        string connectionInfo = "";
        if (transport != null)
        {
            connectionInfo = $"Connecting to: {transport.ConnectionData.Address}:{transport.ConnectionData.Port}\n";
        }
        connectionInfo += $"Player name: {PlayerSessionSettings.SanitizePlayerName(playerNameInput)}\n";
        connectionInfo += $"Press F11 for debug info";
        
        GUIStyle infoStyle = new GUIStyle(GUI.skin.label);
        infoStyle.alignment = TextAnchor.MiddleCenter;
        infoStyle.fontSize = 14;
        infoStyle.normal.textColor = Color.gray;
        
        GUI.Label(infoRect, connectionInfo, infoStyle);
    }
    
    private void DrawDebugWindow()
    {
        // Large debug window
        debugWindowRect = GUI.Window(0, debugWindowRect, DrawDebugWindowContent, "CLIENT DEBUG CONSOLE", debugWindowStyle);
    }
    
    private void DrawDebugWindowContent(int windowID)
    {
        GUILayout.BeginVertical();

        GUILayout.Label("Player Name", GUILayout.Height(22));
        GUI.SetNextControlName("DebugPlayerNameField");
        playerNameInput = GUILayout.TextField(playerNameInput ?? string.Empty, 24, playerNameFieldStyle, GUILayout.Height(28));
        GUILayout.Space(8);
        
        // Debug information
        GUILayout.Label(debugInfo, debugTextStyle, GUILayout.ExpandHeight(true));
        
        GUILayout.Space(10);
        
        // Connection controls
        GUILayout.BeginHorizontal();
        
        if (!isConnected && !isConnecting)
        {
            if (GUILayout.Button("CONNECT", GUILayout.Height(30)))
            {
                AttemptConnection();
            }
        }
        else if (isConnected)
        {
            if (GUILayout.Button("DISCONNECT", GUILayout.Height(30)))
            {
                AttemptDisconnection();
            }
        }
        else if (isConnecting)
        {
            GUI.enabled = false;
            GUILayout.Button("CONNECTING...", GUILayout.Height(30));
            GUI.enabled = true;
        }
        
        if (GUILayout.Button("Hide Debug (F11)", GUILayout.Height(30)))
        {
            isDebugVisible = false;
        }
        
        GUILayout.EndHorizontal();
        
        GUILayout.EndVertical();
        
        // Make window draggable
        GUI.DragWindow();
    }

    private string DrawPlayerNameField(Rect fieldRect, string controlName)
    {
        GUI.SetNextControlName(controlName);
        return GUI.TextField(fieldRect, playerNameInput ?? string.Empty, 24, playerNameFieldStyle);
    }

    private string GetInitialPlayerNameInput()
    {
        string storedName = PlayerSessionSettings.LocalPlayerName;
        return storedName == PlayerSessionSettings.DefaultPlayerName ? string.Empty : storedName;
    }

    private Texture2D CreateSolidTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }
}

// Extension to handle player movement stopping
public static class PlayerMovementExtensions
{
    public static void StopAllMovement(this PlayerMovement playerMovement)
    {
        if (playerMovement == null) return;
        
        // Stop movement by setting velocity to zero
        var rb = playerMovement.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }
}