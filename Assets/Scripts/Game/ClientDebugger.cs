using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.IO;
using System.Text;

public class ClientDebugger : MonoBehaviour
{
    [Header("Debug Settings")]
    [SerializeField] private bool enableDebugUI = true;
    [SerializeField] private bool verboseLogging = true;
    [SerializeField] private KeyCode toggleDebugKey = KeyCode.F11;
    
    [Header("UI State")]
    [SerializeField] private bool isDebugVisible = false;
    [SerializeField] private bool isConnected = false;

    [Header("Remote Server Orchestration")]
    [SerializeField] private bool enableRemoteServerOrchestration = false;
    [SerializeField] private bool startServerWhenConnectRequested = true;
    [SerializeField] private bool autoConnectOnLaunch = false;
    [SerializeField] private string orchestrationApiBaseUrl = "";
    [SerializeField] private string startServerEndpoint = "/server/start";
    [SerializeField] private string serverStatusEndpoint = "/server/status";
    [SerializeField] private string apiKeyHeaderName = "x-api-key";
    [SerializeField] private string apiKeyValue = "";
    [SerializeField] private string authorizationBearerToken = "";
    [SerializeField] private string authorizationBearerTokenEnvironmentVariable = "OIDC_BEARER_TOKEN";
    [SerializeField] private string authorizationBearerTokenCommandLineArgument = "-oidcBearerToken";
    [SerializeField] private string authorizationBearerTokenFileEnvironmentVariable = "OIDC_BEARER_TOKEN_FILE";
    [SerializeField] private string authorizationBearerTokenFileCommandLineArgument = "-oidcBearerTokenFile";
    [SerializeField] private string authorizationBearerTokenFilePath = "";
    [SerializeField] private float requestTimeoutSeconds = 10f;
    [SerializeField] private float serverStartupTimeoutSeconds = 180f;
    [SerializeField] private float serverStatusPollIntervalSeconds = 3f;
    [SerializeField] private float postReadyConnectDelaySeconds = 8f;
    [SerializeField] private float directClientConnectTimeoutSeconds = 25f;
    [SerializeField] private int orchestratedReconnectRetries = 2;
    [SerializeField] private float orchestratedReconnectDelaySeconds = 3f;

    [Header("Join Ticket Auth")]
    [SerializeField] private bool enableJoinTicketAuth = true;
    [SerializeField] private string joinTicketEndpoint = "/server/join-token";
    [SerializeField] private float joinTicketTimeoutSeconds = 10f;

    [Header("Secure Transport")]
    [SerializeField] private bool enableSecureRelayTransport = true;
    [SerializeField] private string preferredRelayConnectionType = RelayTransportBootstrap.DefaultConnectionType;

    [Header("Firebase Anonymous Auth")]
    [SerializeField] private bool enableFirebaseAnonymousAuth = false;
    [SerializeField] private string firebaseWebApiKey = "";
    [SerializeField] private string firebaseWebApiKeyEnvironmentVariable = "FIREBASE_WEB_API_KEY";
    [SerializeField] private string firebaseWebApiKeyCommandLineArgument = "-firebaseWebApiKey";
    [SerializeField] private float firebaseAuthTimeoutSeconds = 10f;
    [SerializeField] private int firebaseTokenRefreshBufferSeconds = 60;
    
    private NetworkManager networkManager;
    private UnityTransport transport;
    private string debugInfo = "";
    private bool isConnecting = false;
    private bool isWaitingForServerAvailability = false;
    private float connectionStartTime;
    private string playerNameInput = PlayerSessionSettings.DefaultPlayerName;
    private string serverStartupStatus = "";
    private bool launchConnectTriggered = false;
    private Coroutine orchestratedConnectRoutine;
    private Coroutine joinTicketConnectRoutine;
    private Coroutine orchestratedRetryRoutine;
    private string targetServerId = "";
    private string lastLoggedOrchestrationMessage = string.Empty;
    private string joinLobbyCodeInput = "";
    private string createLobbyCodeInput = "";
    private bool showCreateLobbyDialog = false;
    private LobbyRequestAction pendingLobbyRequestAction = LobbyRequestAction.None;
    private string pendingLobbyCode = "";
    private string lastResolvedBearerTokenSource = "Unavailable";
    private string cachedFirebaseIdToken = string.Empty;
    private string cachedFirebaseRefreshToken = string.Empty;
    private long cachedFirebaseIdTokenExpiresAtUnix;
    private string cachedFirebaseUserId = string.Empty;
    private int remainingOrchestratedReconnectRetries;
    private bool lastConnectionUsedJoinTicket;

    private const int MaxLobbyCodeLength = 12;
    private const string FirebaseAnonymousSignInEndpoint = "https://identitytoolkit.googleapis.com/v1/accounts:signUp";
    private const string FirebaseRefreshTokenEndpoint = "https://securetoken.googleapis.com/v1/token";

    public bool IsBusy => isConnecting || isWaitingForServerAvailability;
    public bool IsClientConnected => isConnected;
    public bool UsesLobbyWorkflow => ShouldUseLobbyWorkflow();
    public bool UsesRemoteServerOrchestration => ShouldUseRemoteOrchestration();
    public string StatusMessage => serverStartupStatus;
    public string PlayerNameInput
    {
        get => playerNameInput;
        set => playerNameInput = NormalizePlayerNameInput(value);
    }

    public string JoinLobbyCodeInput
    {
        get => joinLobbyCodeInput;
        set => joinLobbyCodeInput = SanitizeLobbyCode(value);
    }

    public string CreateLobbyCodeInput
    {
        get => createLobbyCodeInput;
        set => createLobbyCodeInput = SanitizeLobbyCode(value);
    }

    public void ConnectToConfiguredServer()
    {
        AttemptConnection();
    }

    public void JoinConfiguredLobby()
    {
        AttemptJoinLobby();
    }

    public void CreateConfiguredLobby()
    {
        AttemptCreateLobby();
    }

    public void CreateConfiguredLobby(string lobbyCode)
    {
        createLobbyCodeInput = SanitizeLobbyCode(lobbyCode);
        AttemptCreateLobby();
    }

    public void ShowCreateLobbyPrompt()
    {
        OpenCreateLobbyDialog();
    }

    public void DisconnectClient()
    {
        AttemptDisconnection();
    }
    
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
    private Vector2 debugInfoScrollPosition;
    
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

        ApplyConfigurationDefaults();
        playerNameInput = GetInitialPlayerNameInput();
        
        InitializeUI();
        TryAutoConnectOnLaunch();
    }

    private void ApplyConfigurationDefaults()
    {
        if (string.IsNullOrWhiteSpace(startServerEndpoint))
        {
            startServerEndpoint = "/server/start";
        }

        if (string.IsNullOrWhiteSpace(serverStatusEndpoint))
        {
            serverStatusEndpoint = "/server/status";
        }

        if (string.IsNullOrWhiteSpace(apiKeyHeaderName))
        {
            apiKeyHeaderName = "x-api-key";
        }

        if (string.IsNullOrWhiteSpace(authorizationBearerTokenEnvironmentVariable))
        {
            authorizationBearerTokenEnvironmentVariable = "OIDC_BEARER_TOKEN";
        }

        if (string.IsNullOrWhiteSpace(authorizationBearerTokenCommandLineArgument))
        {
            authorizationBearerTokenCommandLineArgument = "-oidcBearerToken";
        }

        if (string.IsNullOrWhiteSpace(authorizationBearerTokenFileEnvironmentVariable))
        {
            authorizationBearerTokenFileEnvironmentVariable = "OIDC_BEARER_TOKEN_FILE";
        }

        if (string.IsNullOrWhiteSpace(authorizationBearerTokenFileCommandLineArgument))
        {
            authorizationBearerTokenFileCommandLineArgument = "-oidcBearerTokenFile";
        }

        if (string.IsNullOrWhiteSpace(joinTicketEndpoint))
        {
            joinTicketEndpoint = "/server/join-token";
        }

        if (string.IsNullOrWhiteSpace(firebaseWebApiKeyEnvironmentVariable))
        {
            firebaseWebApiKeyEnvironmentVariable = "FIREBASE_WEB_API_KEY";
        }

        if (string.IsNullOrWhiteSpace(firebaseWebApiKeyCommandLineArgument))
        {
            firebaseWebApiKeyCommandLineArgument = "-firebaseWebApiKey";
        }

        if (requestTimeoutSeconds <= 0f)
        {
            requestTimeoutSeconds = 10f;
        }

        if (joinTicketTimeoutSeconds <= 0f)
        {
            joinTicketTimeoutSeconds = 10f;
        }

        if (firebaseAuthTimeoutSeconds <= 0f)
        {
            firebaseAuthTimeoutSeconds = 10f;
        }

        if (firebaseTokenRefreshBufferSeconds < 0)
        {
            firebaseTokenRefreshBufferSeconds = 60;
        }

        if (serverStartupTimeoutSeconds <= 0f)
        {
            serverStartupTimeoutSeconds = 180f;
        }

        if (serverStatusPollIntervalSeconds <= 0f)
        {
            serverStatusPollIntervalSeconds = 3f;
        }

        if (postReadyConnectDelaySeconds < 0f)
        {
            postReadyConnectDelaySeconds = 0f;
        }

        if (directClientConnectTimeoutSeconds <= 0f)
        {
            directClientConnectTimeoutSeconds = 25f;
        }

        if (orchestratedReconnectRetries < 0)
        {
            orchestratedReconnectRetries = 0;
        }

        if (orchestratedReconnectDelaySeconds < 0f)
        {
            orchestratedReconnectDelaySeconds = 0f;
        }

        remainingOrchestratedReconnectRetries = orchestratedReconnectRetries;
    }

    private void TryAutoConnectOnLaunch()
    {
        if (launchConnectTriggered || !autoConnectOnLaunch || networkManager == null)
        {
            return;
        }

        launchConnectTriggered = true;

        if (ShouldUseLobbyWorkflow())
        {
            string sanitizedLobbyCode = SanitizeLobbyCode(joinLobbyCodeInput);
            if (!string.IsNullOrWhiteSpace(sanitizedLobbyCode))
            {
                joinLobbyCodeInput = sanitizedLobbyCode;
                StartLobbyConnection(LobbyRequestAction.Join, sanitizedLobbyCode);
            }
            else
            {
                serverStartupStatus = "Auto-connect skipped. Enter a lobby code or create a lobby.";
                LogDebug(serverStartupStatus);
            }

            return;
        }

        if (enableRemoteServerOrchestration && HasOrchestrationConfiguration())
        {
            StartOrchestratedConnection();
            return;
        }

        AttemptConnection();
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
        if (isConnecting && !isWaitingForServerAvailability && Time.time - connectionStartTime > directClientConnectTimeoutSeconds)
        {
            LogDebug("Connection timeout - stopping connection attempt");

            if (TryResumeOrchestratedConnectionAfterFailedConnect($"Connection attempt timed out after {directClientConnectTimeoutSeconds:F0}s."))
            {
                return;
            }

            isConnecting = false;
            if (networkManager.IsClient)
            {
                networkManager.Shutdown();
            }

            if (string.IsNullOrWhiteSpace(serverStartupStatus))
            {
                serverStartupStatus = $"Connection attempt timed out after {directClientConnectTimeoutSeconds:F0}s.";
            }
        }
    }
    
    void HandleInput()
    {
        // Toggle debug window with the configured key.
        if (WasKeyPressedThisFrame(toggleDebugKey))
        {
            isDebugVisible = !isDebugVisible;
            LogDebug($"Debug window toggled: {(isDebugVisible ? "visible" : "hidden")}");
        }
        
        // Handle ESC key for stopping player movement only (not pause)
        if (WasKeyPressedThisFrame(KeyCode.Escape))
        {
            HandleEscapeKey();
        }
        
        // Quick connect with 'C' key (for debugging)
        if (WasKeyPressedThisFrame(KeyCode.C) && !isConnected && !isConnecting)
        {
            AttemptConnection();
        }
    }

    private bool WasKeyPressedThisFrame(KeyCode keyCode)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !TryMapToInputSystemKey(keyCode, out Key mappedKey))
        {
            return false;
        }

        var keyControl = keyboard[mappedKey];
        return keyControl != null && keyControl.wasPressedThisFrame;
    }

    private bool TryMapToInputSystemKey(KeyCode keyCode, out Key mappedKey)
    {
        switch (keyCode)
        {
            case KeyCode.Alpha0:
                mappedKey = Key.Digit0;
                return true;
            case KeyCode.Alpha1:
                mappedKey = Key.Digit1;
                return true;
            case KeyCode.Alpha2:
                mappedKey = Key.Digit2;
                return true;
            case KeyCode.Alpha3:
                mappedKey = Key.Digit3;
                return true;
            case KeyCode.Alpha4:
                mappedKey = Key.Digit4;
                return true;
            case KeyCode.Alpha5:
                mappedKey = Key.Digit5;
                return true;
            case KeyCode.Alpha6:
                mappedKey = Key.Digit6;
                return true;
            case KeyCode.Alpha7:
                mappedKey = Key.Digit7;
                return true;
            case KeyCode.Alpha8:
                mappedKey = Key.Digit8;
                return true;
            case KeyCode.Alpha9:
                mappedKey = Key.Digit9;
                return true;
            default:
                return Enum.TryParse(keyCode.ToString(), true, out mappedKey);
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

        if (orchestratedConnectRoutine != null)
        {
            StopCoroutine(orchestratedConnectRoutine);
            orchestratedConnectRoutine = null;
        }

        if (joinTicketConnectRoutine != null)
        {
            StopCoroutine(joinTicketConnectRoutine);
            joinTicketConnectRoutine = null;
        }

        if (playerNameFieldBackground != null)
        {
            Destroy(playerNameFieldBackground);
        }

        if (playerNameFieldFocusedBackground != null)
        {
            Destroy(playerNameFieldFocusedBackground);
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
        if (networkManager == null || transport == null)
        {
            LogDebug("Cannot connect because NetworkManager or UnityTransport is missing.");
            serverStartupStatus = "Network transport is not configured.";
            return;
        }

        if (isConnecting || isConnected)
        {
            return;
        }

        ResetOrchestratedReconnectBudget();

        if (ShouldUseLobbyWorkflow())
        {
            string sanitizedLobbyCode = SanitizeLobbyCode(joinLobbyCodeInput);
            if (!string.IsNullOrWhiteSpace(sanitizedLobbyCode))
            {
                joinLobbyCodeInput = sanitizedLobbyCode;
                StartLobbyConnection(LobbyRequestAction.Join, sanitizedLobbyCode);
            }
            else
            {
                showCreateLobbyDialog = true;
                serverStartupStatus = "Enter a lobby code to join, or create a new lobby.";
            }

            return;
        }

        if (ShouldUseRemoteOrchestration())
        {
            StartOrchestratedConnection();
            return;
        }

        BeginDirectConnection();
    }

    private bool ShouldUseRemoteOrchestration()
    {
        return enableRemoteServerOrchestration
            && startServerWhenConnectRequested
            && HasOrchestrationConfiguration();
    }

    private bool HasOrchestrationConfiguration()
    {
        return !string.IsNullOrWhiteSpace(orchestrationApiBaseUrl);
    }

    private bool ShouldUseLobbyWorkflow()
    {
        return enableJoinTicketAuth && HasOrchestrationConfiguration();
    }

    private void AttemptJoinLobby()
    {
        string sanitizedLobbyCode = SanitizeLobbyCode(joinLobbyCodeInput);
        if (string.IsNullOrWhiteSpace(sanitizedLobbyCode))
        {
            serverStartupStatus = "Enter a lobby code to join.";
            LogDebug(serverStartupStatus);
            return;
        }

        joinLobbyCodeInput = sanitizedLobbyCode;
        StartLobbyConnection(LobbyRequestAction.Join, sanitizedLobbyCode);
    }

    private void OpenCreateLobbyDialog()
    {
        if (isConnecting || isConnected)
        {
            return;
        }

        createLobbyCodeInput = string.IsNullOrWhiteSpace(createLobbyCodeInput)
            ? SanitizeLobbyCode(joinLobbyCodeInput)
            : SanitizeLobbyCode(createLobbyCodeInput);
        showCreateLobbyDialog = true;
    }

    private void AttemptCreateLobby()
    {
        string sanitizedLobbyCode = SanitizeLobbyCode(createLobbyCodeInput);
        if (string.IsNullOrWhiteSpace(sanitizedLobbyCode))
        {
            serverStartupStatus = "Enter a lobby code to create.";
            LogDebug(serverStartupStatus);
            return;
        }

        createLobbyCodeInput = sanitizedLobbyCode;
        joinLobbyCodeInput = sanitizedLobbyCode;
        showCreateLobbyDialog = false;
        StartLobbyConnection(LobbyRequestAction.Create, sanitizedLobbyCode);
    }

    private void StartLobbyConnection(LobbyRequestAction action, string lobbyCode)
    {
        if (networkManager == null || transport == null)
        {
            LogDebug("Cannot connect because NetworkManager or UnityTransport is missing.");
            serverStartupStatus = "Network transport is not configured.";
            return;
        }

        if (isConnecting || isConnected)
        {
            return;
        }

        ResetOrchestratedReconnectBudget();

        pendingLobbyRequestAction = action;
        pendingLobbyCode = SanitizeLobbyCode(lobbyCode);
        PlayerSessionSettings.LocalPlayerName = playerNameInput;
        serverStartupStatus = action == LobbyRequestAction.Create
            ? $"Creating lobby {pendingLobbyCode}..."
            : $"Joining lobby {pendingLobbyCode}...";

        if (enableRemoteServerOrchestration && HasOrchestrationConfiguration())
        {
            StartOrchestratedConnection(action == LobbyRequestAction.Create && startServerWhenConnectRequested);
            return;
        }

        BeginDirectConnection();
    }

    private void StartOrchestratedConnection()
    {
        StartOrchestratedConnection(true);
    }

    private void StartOrchestratedConnection(bool requestServerStart)
    {
        if (orchestratedConnectRoutine != null)
        {
            StopCoroutine(orchestratedConnectRoutine);
        }

        orchestratedConnectRoutine = StartCoroutine(OrchestrateServerStartupAndConnect(requestServerStart));
    }

    private void ResetOrchestratedReconnectBudget()
    {
        remainingOrchestratedReconnectRetries = Mathf.Max(0, orchestratedReconnectRetries);
        lastConnectionUsedJoinTicket = false;

        if (orchestratedRetryRoutine != null)
        {
            StopCoroutine(orchestratedRetryRoutine);
            orchestratedRetryRoutine = null;
        }
    }

    private bool TryResumeOrchestratedConnectionAfterFailedConnect(string failureReason)
    {
        if (!lastConnectionUsedJoinTicket || !HasOrchestrationConfiguration() || remainingOrchestratedReconnectRetries <= 0)
        {
            return false;
        }

        remainingOrchestratedReconnectRetries--;

        if (networkManager != null && networkManager.IsClient)
        {
            networkManager.Shutdown();
        }

        if (networkManager != null)
        {
            networkManager.NetworkConfig.ConnectionData = Array.Empty<byte>();
        }

        isConnecting = false;
        isWaitingForServerAvailability = false;
        isConnected = false;
        serverStartupStatus = $"{failureReason} Retrying dedicated server availability check...";
        LogDebug($"{failureReason} Retrying orchestration readiness check. Remaining retries: {remainingOrchestratedReconnectRetries}");

        if (orchestratedRetryRoutine != null)
        {
            StopCoroutine(orchestratedRetryRoutine);
        }

        orchestratedRetryRoutine = StartCoroutine(RetryOrchestratedConnectionAfterDelay());
        return true;
    }

    private IEnumerator RetryOrchestratedConnectionAfterDelay()
    {
        if (orchestratedReconnectDelaySeconds > 0f)
        {
            yield return new WaitForSeconds(orchestratedReconnectDelaySeconds);
        }

        orchestratedRetryRoutine = null;
        StartOrchestratedConnection(false);
    }

    private IEnumerator OrchestrateServerStartupAndConnect(bool requestServerStart)
    {
        isConnecting = true;
        isWaitingForServerAvailability = true;
        connectionStartTime = Time.time;
        serverStartupStatus = requestServerStart
            ? "Requesting dedicated server startup..."
            : "Checking dedicated lobby availability...";
        LogOrchestrationStatusChanged(serverStartupStatus);

        string requestError = null;
        OrchestrationApiResponse latestResponse = null;

        string initialMethod = requestServerStart ? UnityWebRequest.kHttpVerbPOST : UnityWebRequest.kHttpVerbGET;
        string initialEndpoint = requestServerStart ? startServerEndpoint : BuildStatusEndpoint(targetServerId);
    string initialBody = requestServerStart ? BuildStartServerRequestPayloadJson() : null;

        yield return SendOrchestrationRequest(
            initialMethod,
            initialEndpoint,
            initialBody,
            response => latestResponse = response,
            error => requestError = error);

        if (!string.IsNullOrWhiteSpace(requestError))
        {
            if (!requestServerStart)
            {
                HandleOrchestrationFailure(requestError);
                yield break;
            }

            LogDebug($"Dedicated server start request failed: {requestError}");
            serverStartupStatus = "Start request failed. Checking current dedicated server status...";
            LogOrchestrationStatusChanged(serverStartupStatus);

            requestError = null;
            latestResponse = null;

            yield return SendOrchestrationRequest(
                UnityWebRequest.kHttpVerbGET,
                BuildStatusEndpoint(targetServerId),
                null,
                response => latestResponse = response,
                error => requestError = error);

            if (!string.IsNullOrWhiteSpace(requestError))
            {
                HandleOrchestrationFailure(requestError);
                yield break;
            }
        }

        if (!requestServerStart && !CanWaitForLobbyAvailability(latestResponse))
        {
            HandleOrchestrationFailure(BuildLobbyUnavailableMessage(latestResponse));
            yield break;
        }

        float deadline = Time.time + serverStartupTimeoutSeconds;
        float readySince = -1f;
        string orchestrationTargetId = ResolveTargetId(latestResponse);

        while (Time.time < deadline)
        {
            if (latestResponse != null)
            {
                string latestTargetId = ResolveTargetId(latestResponse);
                if (!string.IsNullOrWhiteSpace(latestTargetId))
                {
                    orchestrationTargetId = latestTargetId;
                }

                ApplyConnectionDataFromResponse(latestResponse);
                serverStartupStatus = BuildServerStartupStatus(latestResponse);
                LogOrchestrationStatusChanged(serverStartupStatus);

                if (latestResponse.isReady)
                {
                    if (readySince < 0f)
                    {
                        readySince = Time.time;
                    }

                    float warmupSeconds = Mathf.Max(postReadyConnectDelaySeconds, latestResponse.serverWarmupSeconds);
                    float elapsedSinceReady = Time.time - readySince;
                    float remainingWarmup = Mathf.Max(0f, warmupSeconds - elapsedSinceReady);

                    if (remainingWarmup > 0f)
                    {
                        serverStartupStatus = $"Server ready. Connecting in {Mathf.CeilToInt(remainingWarmup)}s...";
                        yield return null;
                        continue;
                    }

                    isWaitingForServerAvailability = false;
                    orchestratedConnectRoutine = null;
                    BeginDirectConnection();
                    yield break;
                }
            }

            if (!requestServerStart && latestResponse != null && !CanWaitForLobbyAvailability(latestResponse))
            {
                HandleOrchestrationFailure(BuildLobbyUnavailableMessage(latestResponse));
                yield break;
            }

            readySince = -1f;
            yield return new WaitForSeconds(serverStatusPollIntervalSeconds);

            requestError = null;
            latestResponse = null;

            string statusEndpointWithQuery = BuildStatusEndpoint(orchestrationTargetId);
            yield return SendOrchestrationRequest(
                UnityWebRequest.kHttpVerbGET,
                statusEndpointWithQuery,
                null,
                response => latestResponse = response,
                error => requestError = error);

            if (!string.IsNullOrWhiteSpace(requestError))
            {
                HandleOrchestrationFailure(requestError);
                yield break;
            }
        }

        HandleOrchestrationFailure(requestServerStart
            ? "Timed out while waiting for the dedicated server to become ready."
            : "Timed out while waiting for the dedicated lobby to become ready.");
    }

    private void HandleOrchestrationFailure(string errorMessage)
    {
        LogDebug($"Server orchestration failed: {errorMessage}");
        serverStartupStatus = errorMessage;
        isConnecting = false;
        isWaitingForServerAvailability = false;
        orchestratedConnectRoutine = null;
    }

    private string BuildStatusEndpoint(string targetId)
    {
        string requestedLobbyCode = ResolveRequestedLobbyCode();
        bool hasTargetId = !string.IsNullOrWhiteSpace(targetId);
        bool hasLobbyCode = ShouldUseLobbyWorkflow() && !string.IsNullOrWhiteSpace(requestedLobbyCode);

        if (!hasTargetId && !hasLobbyCode)
        {
            return serverStatusEndpoint;
        }

        StringBuilder builder = new StringBuilder(serverStatusEndpoint);
        string delimiter = serverStatusEndpoint.Contains("?") ? "&" : "?";

        if (hasTargetId)
        {
            string escapedTargetId = UnityWebRequest.EscapeURL(targetId);
            builder.Append(delimiter);
            builder.Append($"targetId={escapedTargetId}&instanceId={escapedTargetId}");
            delimiter = "&";
        }

        if (hasLobbyCode)
        {
            string escapedLobbyCode = UnityWebRequest.EscapeURL(requestedLobbyCode);
            builder.Append(delimiter);
            builder.Append($"lobbyCode={escapedLobbyCode}");
        }

        return builder.ToString();
    }

    private string BuildStartServerRequestPayloadJson()
    {
        string requestedLobbyCode = ResolveRequestedLobbyCode();
        string requestedLobbyAction = ResolveRequestedLobbyAction();
        StartServerRequestPayload requestPayload = new StartServerRequestPayload
        {
            targetId = targetServerId,
            instanceId = targetServerId,
            lobbyCode = requestedLobbyCode,
            lobbyAction = requestedLobbyAction,
        };

        return JsonUtility.ToJson(requestPayload);
    }

    private string BuildServerStartupStatus(OrchestrationApiResponse response)
    {
        if (response == null)
        {
            return "Waiting for dedicated server status...";
        }

        if (!string.IsNullOrWhiteSpace(response.message))
        {
            return response.message;
        }

        if (response.isReady)
        {
            return "Dedicated server is ready.";
        }

        string state = string.IsNullOrWhiteSpace(response.instanceState) ? "starting" : response.instanceState;
        return $"Dedicated server state: {state}";
    }

    private bool CanWaitForLobbyAvailability(OrchestrationApiResponse response)
    {
        if (response == null)
        {
            return true;
        }

        string instanceState = string.IsNullOrWhiteSpace(response.instanceState)
            ? string.Empty
            : response.instanceState.Trim().ToLowerInvariant();

        return instanceState == "running"
            || instanceState == "provisioning"
            || instanceState == "staging";
    }

    private string BuildLobbyUnavailableMessage(OrchestrationApiResponse response)
    {
        if (response == null)
        {
            return "No active dedicated lobby is running. Create the lobby first.";
        }

        string instanceState = string.IsNullOrWhiteSpace(response.instanceState)
            ? string.Empty
            : response.instanceState.Trim().ToLowerInvariant();

        switch (instanceState)
        {
            case "terminated":
            case "stopped":
            case "":
                return "No active dedicated lobby is running. Create the lobby first.";
            case "stopping":
            case "suspending":
                return "The dedicated lobby is shutting down. Ask the host to create it again.";
            default:
                return string.IsNullOrWhiteSpace(response.message)
                    ? "The dedicated lobby is not ready yet."
                    : response.message;
        }
    }

    private void LogOrchestrationStatusChanged(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        if (string.Equals(lastLoggedOrchestrationMessage, message, StringComparison.Ordinal))
        {
            return;
        }

        lastLoggedOrchestrationMessage = message;
        LogDebug(message);
    }

    private void ApplyConnectionDataFromResponse(OrchestrationApiResponse response)
    {
        if (transport == null || response == null)
        {
            return;
        }

        if (string.Equals(response.transportMode, RelayTransportBootstrap.TransportModeRelay, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string responseTargetId = ResolveTargetId(response);
        if (!string.IsNullOrWhiteSpace(responseTargetId))
        {
            targetServerId = responseTargetId;
        }

        string address = response.connectionAddress;
        if (string.IsNullOrWhiteSpace(address))
        {
            address = !string.IsNullOrWhiteSpace(response.publicDnsName)
                ? response.publicDnsName
                : response.publicIpAddress;
        }

        if (string.IsNullOrWhiteSpace(address))
        {
            return;
        }

        ushort port = response.port > 0 ? response.port : transport.ConnectionData.Port;
        transport.SetConnectionData(address, port, "0.0.0.0");
    }

    private void BeginDirectConnection()
    {
        if (ShouldUseJoinTicketAuth())
        {
            StartJoinTicketConnection();
            return;
        }

        StartClientWithJoinTicket(string.Empty);
    }

    private bool ShouldUseJoinTicketAuth()
    {
        return enableJoinTicketAuth && HasOrchestrationConfiguration();
    }

    private void StartJoinTicketConnection()
    {
        if (joinTicketConnectRoutine != null)
        {
            StopCoroutine(joinTicketConnectRoutine);
        }

        joinTicketConnectRoutine = StartCoroutine(RequestJoinTicketAndConnect());
    }

    private IEnumerator RequestJoinTicketAndConnect()
    {
        serverStartupStatus = "Requesting join ticket...";
        PlayerSessionSettings.LocalPlayerName = playerNameInput;
        string requestedLobbyCode = ResolveRequestedLobbyCode();
        string requestedLobbyAction = ResolveRequestedLobbyAction();

        if (ShouldUseLobbyWorkflow() && (string.IsNullOrWhiteSpace(requestedLobbyCode) || string.IsNullOrWhiteSpace(requestedLobbyAction)))
        {
            joinTicketConnectRoutine = null;
            HandleOrchestrationFailure("Select Join Lobby or Create Lobby before connecting.");
            yield break;
        }

        JoinTicketRequestPayload requestPayload = new JoinTicketRequestPayload
        {
            playerName = PlayerSessionSettings.LocalPlayerName,
            targetId = targetServerId,
            instanceId = targetServerId,
            lobbyCode = requestedLobbyCode,
            lobbyAction = requestedLobbyAction,
        };

        string requestError = null;
        JoinTicketApiResponse response = null;

        yield return SendJoinTicketRequest(
            JsonUtility.ToJson(requestPayload),
            joinTicketResponse => response = joinTicketResponse,
            error => requestError = error);

        joinTicketConnectRoutine = null;

        if (!string.IsNullOrWhiteSpace(requestError))
        {
            HandleOrchestrationFailure(requestError);
            yield break;
        }

        if (response == null || string.IsNullOrWhiteSpace(response.joinToken))
        {
            HandleOrchestrationFailure("Join ticket response did not contain a token.");
            yield break;
        }

        string responseTargetId = ResolveTargetId(response);
        if (!string.IsNullOrWhiteSpace(responseTargetId))
        {
            targetServerId = responseTargetId;
        }

        if (ShouldUseRelayTransport(response))
        {
            string relayError = null;
            string configuredConnectionType = null;
            serverStartupStatus = "Configuring secure relay transport...";

            yield return RelayTransportBootstrap.ConfigureClientTransportForRelay(
                transport,
                response.relayJoinCode,
                ResolveRelayConnectionType(response.relayConnectionType),
                connectionType => configuredConnectionType = connectionType,
                error => relayError = error);

            if (!string.IsNullOrWhiteSpace(relayError))
            {
                HandleOrchestrationFailure(relayError);
                yield break;
            }

            LogDebug($"Configured secure Relay transport using {configuredConnectionType}. Region: {response.relayRegion}");
        }
        else if (transport != null && !string.IsNullOrWhiteSpace(response.connectionAddress))
        {
            ushort port = response.port > 0 ? response.port : transport.ConnectionData.Port;
            transport.SetConnectionData(response.connectionAddress, port, "0.0.0.0");
        }

        if (!string.IsNullOrWhiteSpace(response.playerName))
        {
            playerNameInput = response.playerName;
            PlayerSessionSettings.LocalPlayerName = response.playerName;
        }

        if (!string.IsNullOrWhiteSpace(response.lobbyCode))
        {
            pendingLobbyCode = SanitizeLobbyCode(response.lobbyCode);
            joinLobbyCodeInput = pendingLobbyCode;
        }

        LobbyRequestAction responseAction = ParseLobbyRequestAction(response.lobbyAction);
        if (responseAction != LobbyRequestAction.None)
        {
            pendingLobbyRequestAction = responseAction;
        }

        serverStartupStatus = string.IsNullOrWhiteSpace(response.message)
            ? "Join ticket issued. Connecting..."
            : response.message;

        StartClientWithJoinTicket(response.joinToken);
    }

    private void StartClientWithJoinTicket(string joinTicket)
    {
        LogDebug("=== Attempting Connection ===");
        LogCurrentNetworkSettings();

        PlayerSessionSettings.LocalPlayerName = playerNameInput;
        LogDebug($"Using player name: {PlayerSessionSettings.LocalPlayerName}");

        if (networkManager.IsListening)
        {
            networkManager.Shutdown();
        }

        networkManager.NetworkConfig.ConnectionData = string.IsNullOrWhiteSpace(joinTicket)
            ? Array.Empty<byte>()
            : Encoding.UTF8.GetBytes(joinTicket);
        lastConnectionUsedJoinTicket = !string.IsNullOrWhiteSpace(joinTicket);
        
        isConnecting = true;
        isWaitingForServerAvailability = false;
        connectionStartTime = Time.time;
        serverStartupStatus = string.IsNullOrWhiteSpace(serverStartupStatus) ? "Starting client connection..." : serverStartupStatus;
        
        bool started = networkManager.StartClient();
        LogDebug($"StartClient() returned: {started}");

        if (!started)
        {
            isConnecting = false;
            serverStartupStatus = "NetworkManager failed to enter client mode.";
        }
    }

    private bool ShouldUseRelayTransport(JoinTicketApiResponse response)
    {
        if (!enableSecureRelayTransport || response == null || string.IsNullOrWhiteSpace(response.relayJoinCode))
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(response.transportMode)
            || string.Equals(response.transportMode, RelayTransportBootstrap.TransportModeRelay, StringComparison.OrdinalIgnoreCase);
    }

    private string ResolveRelayConnectionType(string responseConnectionType)
    {
        string requestedConnectionType = string.IsNullOrWhiteSpace(responseConnectionType)
            ? preferredRelayConnectionType
            : responseConnectionType;
        return RelayTransportBootstrap.NormalizeConnectionType(requestedConnectionType);
    }
    
    private void AttemptDisconnection()
    {
        LogDebug("=== Disconnecting ===");
        ResetOrchestratedReconnectBudget();

        if (orchestratedConnectRoutine != null)
        {
            StopCoroutine(orchestratedConnectRoutine);
            orchestratedConnectRoutine = null;
        }

        if (joinTicketConnectRoutine != null)
        {
            StopCoroutine(joinTicketConnectRoutine);
            joinTicketConnectRoutine = null;
        }

        if (orchestratedRetryRoutine != null)
        {
            StopCoroutine(orchestratedRetryRoutine);
            orchestratedRetryRoutine = null;
        }

        if (networkManager != null && networkManager.IsListening)
        {
            networkManager.Shutdown();
            isConnecting = false;
            isConnected = false;
        }

        if (networkManager != null)
        {
            networkManager.NetworkConfig.ConnectionData = Array.Empty<byte>();
        }

        isWaitingForServerAvailability = false;
        serverStartupStatus = "Disconnected";
        pendingLobbyRequestAction = LobbyRequestAction.None;
        pendingLobbyCode = string.Empty;
    }
    
    private void OnClientConnected(ulong clientId)
    {
        LogDebug($"✅ Client connected! Client ID: {clientId}");
        isConnecting = false;
        isWaitingForServerAvailability = false;
        isConnected = true;

        if (orchestratedRetryRoutine != null)
        {
            StopCoroutine(orchestratedRetryRoutine);
            orchestratedRetryRoutine = null;
        }

        showCreateLobbyDialog = false;
        serverStartupStatus = "Connected to dedicated server.";
    }
    
    private void OnClientDisconnected(ulong clientId)
    {
        LogDebug($"❌ Client disconnected. Client ID: {clientId}");
        bool retryableInitialDisconnect = isConnecting && !isWaitingForServerAvailability && !isConnected;
        isConnecting = false;
        isWaitingForServerAvailability = false;
        isConnected = false;
        if (string.IsNullOrWhiteSpace(serverStartupStatus) || serverStartupStatus == "Connected to dedicated server.")
        {
            serverStartupStatus = "Disconnected from server.";
        }

        if (retryableInitialDisconnect && TryResumeOrchestratedConnectionAfterFailedConnect("The server rejected or dropped the initial connection attempt."))
        {
            return;
        }
        
        // Clean up connection state to allow fresh reconnect
        if (networkManager != null && !networkManager.IsListening)
        {
            // Reset any stuck connection states
            networkManager.Shutdown();
        }

        if (networkManager != null)
        {
            networkManager.NetworkConfig.ConnectionData = Array.Empty<byte>();
        }

        pendingLobbyRequestAction = LobbyRequestAction.None;
        pendingLobbyCode = string.Empty;
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

        if (enableRemoteServerOrchestration)
        {
            debugInfo += $"=== SERVER ORCHESTRATION ===\n";
            debugInfo += $"API Configured: {HasOrchestrationConfiguration()}\n";
            debugInfo += $"Start On Connect: {startServerWhenConnectRequested}\n";
            debugInfo += $"Auto Connect On Launch: {autoConnectOnLaunch}\n";
            if (!string.IsNullOrWhiteSpace(serverStartupStatus))
            {
                debugInfo += $"Status: {serverStartupStatus}\n";
            }
            debugInfo += "\n";
        }

        if (enableJoinTicketAuth)
        {
            debugInfo += $"=== JOIN TICKET AUTH ===\n";
            debugInfo += $"Enabled: {ShouldUseJoinTicketAuth()}\n";
            debugInfo += $"Endpoint: {joinTicketEndpoint}\n";
            debugInfo += $"Target Server: {targetServerId}\n\n";
        }

        if (ShouldUseLobbyWorkflow())
        {
            string currentLobbyCode = ResolveRequestedLobbyCode();
            string currentLobbyAction = ResolveRequestedLobbyAction();
            debugInfo += $"=== LOBBY FLOW ===\n";
            debugInfo += $"Lobby Code: {(string.IsNullOrWhiteSpace(currentLobbyCode) ? "(not set)" : currentLobbyCode)}\n";
            debugInfo += $"Lobby Action: {(string.IsNullOrWhiteSpace(currentLobbyAction) ? "(not selected)" : currentLobbyAction)}\n";
            debugInfo += $"Bearer Token Source: {lastResolvedBearerTokenSource}\n\n";
        }
        
        debugInfo += $"=== CONTROLS ===\n";
        debugInfo += $"F11: Toggle this debug window\n";
        debugInfo += $"C: Quick connect (debug only)\n";
        debugInfo += $"ESC: Stop player movement only\n\n";
        
        if (isConnecting)
        {
            string connectingLabel = isWaitingForServerAvailability ? "STARTING SERVER" : "CONNECTING";
            debugInfo += $"⏳ {connectingLabel}... ({Time.time - connectionStartTime:F1}s)\n";
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

        if (showCreateLobbyDialog)
        {
            DrawCreateLobbyDialog();
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
        if (ShouldUseLobbyWorkflow())
        {
            DrawCenterLobbyControls();
            return;
        }

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
        string buttonText = GetPrimaryConnectButtonLabel();
        
        GUI.enabled = !isConnecting;
        if (GUI.Button(centerButtonRect, buttonText, centerButtonStyle))
        {
            AttemptConnection();
        }
        GUI.enabled = true;

        DrawCenterConnectionInfo(centerButtonRect.y + centerButtonRect.height + 20f);
    }

    private void DrawCenterLobbyControls()
    {
        const float buttonHeight = 48f;

        Rect nameLabelRect = new Rect(centerButtonRect.x, centerButtonRect.y - 128f, centerButtonRect.width, 24f);
        Rect nameFieldRect = new Rect(centerButtonRect.x, centerButtonRect.y - 100f, centerButtonRect.width, 32f);
        Rect lobbyLabelRect = new Rect(centerButtonRect.x, centerButtonRect.y - 60f, centerButtonRect.width, 24f);
        Rect lobbyFieldRect = new Rect(centerButtonRect.x, centerButtonRect.y - 32f, centerButtonRect.width, 32f);
        Rect joinButtonRect = new Rect(centerButtonRect.x, centerButtonRect.y + 16f, centerButtonRect.width, buttonHeight);
        Rect createButtonRect = new Rect(centerButtonRect.x, centerButtonRect.y + 72f, centerButtonRect.width, buttonHeight);

        GUI.Label(nameLabelRect, "Player Name", playerNameLabelStyle);
        playerNameInput = DrawPlayerNameField(nameFieldRect, "CenterPlayerNameField");

        GUI.Label(lobbyLabelRect, "Join Lobby | Enter Lobby Code:", playerNameLabelStyle);
        joinLobbyCodeInput = DrawLobbyCodeField(lobbyFieldRect, "CenterLobbyCodeField", joinLobbyCodeInput);

        bool previousEnabled = GUI.enabled;
        GUI.enabled = !isConnecting && !showCreateLobbyDialog;

        if (GUI.Button(joinButtonRect, pendingLobbyRequestAction == LobbyRequestAction.Join && isConnecting ? "JOINING LOBBY..." : "JOIN LOBBY", centerButtonStyle))
        {
            AttemptJoinLobby();
        }

        if (GUI.Button(createButtonRect, pendingLobbyRequestAction == LobbyRequestAction.Create && isConnecting ? "CREATING LOBBY..." : "CREATE LOBBY", centerButtonStyle))
        {
            OpenCreateLobbyDialog();
        }

        GUI.enabled = previousEnabled;
        DrawCenterConnectionInfo(createButtonRect.y + createButtonRect.height + 20f);
    }

    private void DrawCenterConnectionInfo(float topY)
    {
        float infoWidth = centerButtonRect.width + 200f;

        string connectionInfo = "";
        if (transport != null)
        {
            connectionInfo = $"Connecting to: {transport.ConnectionData.Address}:{transport.ConnectionData.Port}\n";
        }

        if (!string.IsNullOrWhiteSpace(serverStartupStatus))
        {
            connectionInfo += $"{serverStartupStatus}\n";
        }

        if (ShouldUseLobbyWorkflow())
        {
            string lobbyCode = ResolveRequestedLobbyCode();
            if (!string.IsNullOrWhiteSpace(lobbyCode))
            {
                connectionInfo += $"Lobby code: {lobbyCode}\n";
            }
        }

        connectionInfo += $"Player name: {PlayerSessionSettings.SanitizePlayerName(playerNameInput)}\n";
        connectionInfo += "Press F11 for debug info";

        GUIStyle infoStyle = new GUIStyle(GUI.skin.label);
        infoStyle.alignment = TextAnchor.MiddleCenter;
        infoStyle.fontSize = 14;
        infoStyle.normal.textColor = Color.gray;
        infoStyle.wordWrap = true;

        float infoHeight = Mathf.Max(80f, infoStyle.CalcHeight(new GUIContent(connectionInfo), infoWidth));
        Rect infoRect = new Rect(
            centerButtonRect.x - 100f,
            topY,
            infoWidth,
            infoHeight
        );

        GUI.Label(infoRect, connectionInfo, infoStyle);
    }

    private void DrawCreateLobbyDialog()
    {
        Rect dialogRect = new Rect((Screen.width - 360f) / 2f, (Screen.height - 180f) / 2f, 360f, 180f);
        GUI.Box(dialogRect, "CREATE LOBBY");

        Rect labelRect = new Rect(dialogRect.x + 20f, dialogRect.y + 38f, dialogRect.width - 40f, 24f);
        Rect fieldRect = new Rect(dialogRect.x + 20f, dialogRect.y + 68f, dialogRect.width - 40f, 32f);
        Rect createRect = new Rect(dialogRect.x + 20f, dialogRect.y + 118f, dialogRect.width - 120f, 36f);
        Rect cancelRect = new Rect(dialogRect.x + dialogRect.width - 90f, dialogRect.y + 118f, 70f, 36f);

        GUI.Label(labelRect, "Enter Custom Lobby Code:", playerNameLabelStyle);
        createLobbyCodeInput = DrawLobbyCodeField(fieldRect, "CreateLobbyCodeField", createLobbyCodeInput);

        bool previousEnabled = GUI.enabled;
        GUI.enabled = !isConnecting;

        if (GUI.Button(createRect, "CREATE"))
        {
            AttemptCreateLobby();
        }

        if (GUI.Button(cancelRect, "CANCEL"))
        {
            showCreateLobbyDialog = false;
        }

        GUI.enabled = previousEnabled;
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

        if (ShouldUseLobbyWorkflow())
        {
            GUILayout.Label("Join Lobby | Enter Lobby Code", GUILayout.Height(22));
            GUI.SetNextControlName("DebugLobbyCodeField");
            joinLobbyCodeInput = SanitizeLobbyCode(GUILayout.TextField(joinLobbyCodeInput ?? string.Empty, MaxLobbyCodeLength, playerNameFieldStyle, GUILayout.Height(28)));
            GUILayout.Space(8);
        }
        
        GUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandHeight(true));
        debugInfoScrollPosition = GUILayout.BeginScrollView(debugInfoScrollPosition, false, true, GUILayout.ExpandHeight(true));

        float debugInfoContentWidth = Mathf.Max(100f, debugWindowRect.width - 72f);
        float debugInfoContentHeight = Mathf.Max(0f, debugTextStyle.CalcHeight(new GUIContent(debugInfo), debugInfoContentWidth));
        Rect debugInfoRect = GUILayoutUtility.GetRect(debugInfoContentWidth, debugInfoContentHeight, GUILayout.ExpandWidth(true));
        GUI.Label(debugInfoRect, debugInfo, debugTextStyle);

        GUILayout.EndScrollView();
        GUILayout.EndVertical();
        
        GUILayout.Space(10);
        
        // Connection controls
        GUILayout.BeginHorizontal();

        if (ShouldUseLobbyWorkflow())
        {
            if (!isConnected && !isConnecting)
            {
                if (GUILayout.Button("JOIN LOBBY", GUILayout.Height(30)))
                {
                    AttemptJoinLobby();
                }

                if (GUILayout.Button("CREATE LOBBY", GUILayout.Height(30)))
                {
                    OpenCreateLobbyDialog();
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
        }
        else
        {
            if (!isConnected && !isConnecting)
            {
                if (GUILayout.Button(GetPrimaryConnectButtonLabel(), GUILayout.Height(30)))
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

    private string GetPrimaryConnectButtonLabel()
    {
        if (isConnecting)
        {
            return isWaitingForServerAvailability ? "STARTING SERVER..." : "CONNECTING...";
        }

        if (enableRemoteServerOrchestration && startServerWhenConnectRequested && HasOrchestrationConfiguration())
        {
            return "START SERVER + CONNECT";
        }

        return "CONNECT TO SERVER";
    }

    private string DrawPlayerNameField(Rect fieldRect, string controlName)
    {
        GUI.SetNextControlName(controlName);
        return GUI.TextField(fieldRect, playerNameInput ?? string.Empty, 24, playerNameFieldStyle);
    }

    private string DrawLobbyCodeField(Rect fieldRect, string controlName, string currentValue)
    {
        GUI.SetNextControlName(controlName);
        return SanitizeLobbyCode(GUI.TextField(fieldRect, currentValue ?? string.Empty, MaxLobbyCodeLength, playerNameFieldStyle));
    }

    private string GetInitialPlayerNameInput()
    {
        string storedName = PlayerSessionSettings.LocalPlayerName;
        return storedName == PlayerSessionSettings.DefaultPlayerName ? string.Empty : storedName;
    }

    private string NormalizePlayerNameInput(string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return string.Empty;
        }

        return PlayerSessionSettings.SanitizePlayerName(candidate);
    }

    private string SanitizeLobbyCode(string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return string.Empty;
        }

        string trimmedCandidate = candidate.Trim();
        StringBuilder builder = new StringBuilder(MaxLobbyCodeLength);

        for (int index = 0; index < trimmedCandidate.Length && builder.Length < MaxLobbyCodeLength; index++)
        {
            char character = trimmedCandidate[index];
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToUpperInvariant(character));
            }
            else if (character == '-' || character == '_')
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    private string ResolveRequestedLobbyCode()
    {
        string candidate = !string.IsNullOrWhiteSpace(pendingLobbyCode) ? pendingLobbyCode : joinLobbyCodeInput;
        return SanitizeLobbyCode(candidate);
    }

    private string ResolveRequestedLobbyAction()
    {
        switch (pendingLobbyRequestAction)
        {
            case LobbyRequestAction.Create:
                return "create";
            case LobbyRequestAction.Join:
                return "join";
            default:
                return string.Empty;
        }
    }

    private LobbyRequestAction ParseLobbyRequestAction(string rawValue)
    {
        if (string.Equals(rawValue, "create", StringComparison.OrdinalIgnoreCase))
        {
            return LobbyRequestAction.Create;
        }

        if (string.Equals(rawValue, "join", StringComparison.OrdinalIgnoreCase))
        {
            return LobbyRequestAction.Join;
        }

        return LobbyRequestAction.None;
    }

    private IEnumerator SendOrchestrationRequest(string method, string endpoint, string bodyJson, Action<OrchestrationApiResponse> onSuccess, Action<string> onError)
    {
        string url = BuildOrchestrationUrl(endpoint);
        if (string.IsNullOrWhiteSpace(url))
        {
            onError?.Invoke("No orchestration API URL is configured.");
            yield break;
        }

        string resolvedBearerToken = string.Empty;
        string authorizationError = null;

        yield return EnsureAuthorizationBearerToken(
            token => resolvedBearerToken = token,
            error => authorizationError = error);

        if (!string.IsNullOrWhiteSpace(authorizationError))
        {
            onError?.Invoke(authorizationError);
            yield break;
        }

        using (UnityWebRequest request = new UnityWebRequest(url, method))
        {
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = Mathf.CeilToInt(requestTimeoutSeconds);

            if (method != UnityWebRequest.kHttpVerbGET)
            {
                byte[] bodyBytes = Encoding.UTF8.GetBytes(string.IsNullOrWhiteSpace(bodyJson) ? "{}" : bodyJson);
                request.uploadHandler = new UploadHandlerRaw(bodyBytes);
                request.SetRequestHeader("Content-Type", "application/json");
            }

            ApplyOrchestrationHeaders(request, resolvedBearerToken);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                string statusCode = request.responseCode > 0 ? $"HTTP {request.responseCode}: " : string.Empty;
                onError?.Invoke($"{statusCode}{request.error}");
                yield break;
            }

            string json = request.downloadHandler.text;
            if (string.IsNullOrWhiteSpace(json))
            {
                onSuccess?.Invoke(new OrchestrationApiResponse());
                yield break;
            }

            OrchestrationApiResponse response;

            try
            {
                response = JsonUtility.FromJson<OrchestrationApiResponse>(json);
            }
            catch (Exception ex)
            {
                onError?.Invoke($"Invalid orchestration API response: {ex.Message}");
                yield break;
            }

            if (response == null)
            {
                onError?.Invoke("Orchestration API returned an empty payload.");
                yield break;
            }

            onSuccess?.Invoke(response);
        }
    }

    private IEnumerator SendJoinTicketRequest(string bodyJson, Action<JoinTicketApiResponse> onSuccess, Action<string> onError)
    {
        string url = BuildOrchestrationUrl(joinTicketEndpoint);
        if (string.IsNullOrWhiteSpace(url))
        {
            onError?.Invoke("No join-ticket API URL is configured.");
            yield break;
        }

        string resolvedBearerToken = string.Empty;
        string authorizationError = null;

        yield return EnsureAuthorizationBearerToken(
            token => resolvedBearerToken = token,
            error => authorizationError = error);

        if (!string.IsNullOrWhiteSpace(authorizationError))
        {
            onError?.Invoke(authorizationError);
            yield break;
        }

        using (UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = Mathf.CeilToInt(joinTicketTimeoutSeconds);
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(string.IsNullOrWhiteSpace(bodyJson) ? "{}" : bodyJson));
            request.SetRequestHeader("Content-Type", "application/json");

            ApplyOrchestrationHeaders(request, resolvedBearerToken);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                string statusCode = request.responseCode > 0 ? $"HTTP {request.responseCode}: " : string.Empty;
                onError?.Invoke($"{statusCode}{request.error}");
                yield break;
            }

            string json = request.downloadHandler.text;
            if (string.IsNullOrWhiteSpace(json))
            {
                onError?.Invoke("Join-ticket API returned an empty payload.");
                yield break;
            }

            JoinTicketApiResponse response;

            try
            {
                response = JsonUtility.FromJson<JoinTicketApiResponse>(json);
            }
            catch (Exception ex)
            {
                onError?.Invoke($"Invalid join-ticket API response: {ex.Message}");
                yield break;
            }

            if (response == null)
            {
                onError?.Invoke("Join-ticket API returned an empty object.");
                yield break;
            }

            onSuccess?.Invoke(response);
        }
    }

    private string BuildOrchestrationUrl(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(orchestrationApiBaseUrl))
        {
            return string.Empty;
        }

        string trimmedBaseUrl = orchestrationApiBaseUrl.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return trimmedBaseUrl;
        }

        if (endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return endpoint;
        }

        return endpoint.StartsWith("/") ? $"{trimmedBaseUrl}{endpoint}" : $"{trimmedBaseUrl}/{endpoint}";
    }

    private void ApplyOrchestrationHeaders(UnityWebRequest request, string resolvedBearerToken)
    {
        if (!string.IsNullOrWhiteSpace(apiKeyValue))
        {
            request.SetRequestHeader(apiKeyHeaderName, apiKeyValue);
        }

        if (!string.IsNullOrWhiteSpace(resolvedBearerToken))
        {
            request.SetRequestHeader("Authorization", $"Bearer {resolvedBearerToken}");
        }
    }

    private string ResolveAuthorizationBearerToken()
    {
        string resolvedToken = ResolveConfiguredAuthorizationBearerToken();
        if (!string.IsNullOrWhiteSpace(resolvedToken))
        {
            return resolvedToken;
        }

        if (HasValidCachedFirebaseIdToken())
        {
            lastResolvedBearerTokenSource = "Firebase anonymous auth";
            return cachedFirebaseIdToken;
        }

        lastResolvedBearerTokenSource = enableFirebaseAnonymousAuth ? "Firebase anonymous auth unavailable" : "Unavailable";
        return string.Empty;
    }

    private string ResolveConfiguredAuthorizationBearerToken()
    {
        string resolvedToken = ResolveRuntimeConfigurationValue(
            authorizationBearerTokenCommandLineArgument,
            authorizationBearerTokenEnvironmentVariable,
            out string tokenSourceLabel);

        if (!string.IsNullOrWhiteSpace(resolvedToken))
        {
            lastResolvedBearerTokenSource = tokenSourceLabel;
            return resolvedToken.Trim();
        }

        string resolvedTokenFilePath = ResolveAuthorizationBearerTokenFilePath(out string fileSourceLabel);
        if (!string.IsNullOrWhiteSpace(resolvedTokenFilePath) && TryReadBearerTokenFromFile(resolvedTokenFilePath, out string fileToken))
        {
            lastResolvedBearerTokenSource = fileSourceLabel;
            return fileToken;
        }

        if (!string.IsNullOrWhiteSpace(authorizationBearerToken))
        {
            lastResolvedBearerTokenSource = "Inspector fallback";
            return authorizationBearerToken.Trim();
        }

        lastResolvedBearerTokenSource = string.IsNullOrWhiteSpace(fileSourceLabel) ? "Unavailable" : $"{fileSourceLabel} unavailable";
        return string.Empty;
    }

    private string ResolveAuthorizationBearerTokenFilePath(out string sourceLabel)
    {
        string resolvedPath = ResolveRuntimeConfigurationValue(
            authorizationBearerTokenFileCommandLineArgument,
            authorizationBearerTokenFileEnvironmentVariable,
            out sourceLabel);

        if (!string.IsNullOrWhiteSpace(resolvedPath))
        {
            return NormalizeRuntimeFilePath(resolvedPath);
        }

        if (!string.IsNullOrWhiteSpace(authorizationBearerTokenFilePath))
        {
            sourceLabel = "Configured token file";
            return NormalizeRuntimeFilePath(authorizationBearerTokenFilePath);
        }

        sourceLabel = string.Empty;
        return string.Empty;
    }

    private IEnumerator EnsureAuthorizationBearerToken(Action<string> onSuccess, Action<string> onError)
    {
        string resolvedToken = ResolveAuthorizationBearerToken();
        if (!string.IsNullOrWhiteSpace(resolvedToken))
        {
            onSuccess?.Invoke(resolvedToken);
            yield break;
        }

        if (!enableFirebaseAnonymousAuth)
        {
            onSuccess?.Invoke(string.Empty);
            yield break;
        }

        string resolvedFirebaseApiKey = ResolveFirebaseWebApiKey();
        if (string.IsNullOrWhiteSpace(resolvedFirebaseApiKey))
        {
            onError?.Invoke("Firebase anonymous auth is enabled, but no Firebase Web API key is configured.");
            yield break;
        }

        if (!string.IsNullOrWhiteSpace(cachedFirebaseRefreshToken))
        {
            FirebaseRefreshTokenResponse refreshResponse = null;
            string refreshError = null;

            yield return RefreshFirebaseAnonymousIdToken(
                resolvedFirebaseApiKey,
                response => refreshResponse = response,
                error => refreshError = error);

            if (refreshResponse != null && !string.IsNullOrWhiteSpace(refreshResponse.id_token))
            {
                CacheFirebaseAnonymousSession(
                    refreshResponse.id_token,
                    refreshResponse.refresh_token,
                    refreshResponse.expires_in,
                    refreshResponse.user_id,
                    "Firebase anonymous auth (refreshed)");
                onSuccess?.Invoke(cachedFirebaseIdToken);
                yield break;
            }

            if (!string.IsNullOrWhiteSpace(refreshError))
            {
                LogDebug($"Firebase anonymous auth refresh failed: {refreshError}");
            }

            ClearCachedFirebaseAnonymousSession();
        }

        FirebaseAnonymousAuthResponse signInResponse = null;
        string signInError = null;

        yield return SignInAnonymouslyWithFirebase(
            resolvedFirebaseApiKey,
            response => signInResponse = response,
            error => signInError = error);

        if (signInResponse == null || string.IsNullOrWhiteSpace(signInResponse.idToken))
        {
            onError?.Invoke(string.IsNullOrWhiteSpace(signInError)
                ? "Firebase anonymous auth did not return an ID token."
                : signInError);
            yield break;
        }

        CacheFirebaseAnonymousSession(
            signInResponse.idToken,
            signInResponse.refreshToken,
            signInResponse.expiresIn,
            signInResponse.localId,
            "Firebase anonymous auth");
        onSuccess?.Invoke(cachedFirebaseIdToken);
    }

    private string ResolveFirebaseWebApiKey()
    {
        string resolvedApiKey = ResolveRuntimeConfigurationValue(
            firebaseWebApiKeyCommandLineArgument,
            firebaseWebApiKeyEnvironmentVariable,
            out string sourceLabel);

        if (!string.IsNullOrWhiteSpace(resolvedApiKey))
        {
            return resolvedApiKey.Trim();
        }

        if (!string.IsNullOrWhiteSpace(firebaseWebApiKey))
        {
            return firebaseWebApiKey.Trim();
        }

        return string.Empty;
    }

    private bool HasValidCachedFirebaseIdToken()
    {
        if (string.IsNullOrWhiteSpace(cachedFirebaseIdToken))
        {
            return false;
        }

        long nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long refreshThresholdUnix = nowUnix + Math.Max(0, firebaseTokenRefreshBufferSeconds);
        return cachedFirebaseIdTokenExpiresAtUnix > refreshThresholdUnix;
    }

    private void CacheFirebaseAnonymousSession(string idToken, string refreshToken, string expiresInText, string userId, string sourceLabel)
    {
        cachedFirebaseIdToken = idToken ?? string.Empty;
        cachedFirebaseRefreshToken = refreshToken ?? string.Empty;
        cachedFirebaseUserId = userId ?? string.Empty;
        cachedFirebaseIdTokenExpiresAtUnix = CalculateFirebaseIdTokenExpiryUnix(expiresInText);
        lastResolvedBearerTokenSource = sourceLabel;
    }

    private void ClearCachedFirebaseAnonymousSession()
    {
        cachedFirebaseIdToken = string.Empty;
        cachedFirebaseRefreshToken = string.Empty;
        cachedFirebaseUserId = string.Empty;
        cachedFirebaseIdTokenExpiresAtUnix = 0;
    }

    private long CalculateFirebaseIdTokenExpiryUnix(string expiresInText)
    {
        long expiresInSeconds = 3600;
        if (!long.TryParse(expiresInText, out expiresInSeconds) || expiresInSeconds <= 0)
        {
            expiresInSeconds = 3600;
        }

        return DateTimeOffset.UtcNow.ToUnixTimeSeconds() + expiresInSeconds;
    }

    private IEnumerator SignInAnonymouslyWithFirebase(string apiKey, Action<FirebaseAnonymousAuthResponse> onSuccess, Action<string> onError)
    {
        string requestUrl = $"{FirebaseAnonymousSignInEndpoint}?key={UnityWebRequest.EscapeURL(apiKey)}";
        string requestBody = JsonUtility.ToJson(new FirebaseAnonymousAuthRequestPayload { returnSecureToken = true });

        using (UnityWebRequest request = new UnityWebRequest(requestUrl, UnityWebRequest.kHttpVerbPOST))
        {
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = Mathf.CeilToInt(firebaseAuthTimeoutSeconds);
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(requestBody));
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke(BuildFirebaseAuthRequestError(request, "Firebase anonymous auth failed."));
                yield break;
            }

            FirebaseAnonymousAuthResponse response;

            try
            {
                response = JsonUtility.FromJson<FirebaseAnonymousAuthResponse>(request.downloadHandler.text);
            }
            catch (Exception ex)
            {
                onError?.Invoke($"Invalid Firebase anonymous auth response: {ex.Message}");
                yield break;
            }

            if (response == null)
            {
                onError?.Invoke("Firebase anonymous auth returned an empty response.");
                yield break;
            }

            onSuccess?.Invoke(response);
        }
    }

    private IEnumerator RefreshFirebaseAnonymousIdToken(string apiKey, Action<FirebaseRefreshTokenResponse> onSuccess, Action<string> onError)
    {
        string requestUrl = $"{FirebaseRefreshTokenEndpoint}?key={UnityWebRequest.EscapeURL(apiKey)}";
        string requestBody = $"grant_type=refresh_token&refresh_token={UnityWebRequest.EscapeURL(cachedFirebaseRefreshToken)}";

        using (UnityWebRequest request = new UnityWebRequest(requestUrl, UnityWebRequest.kHttpVerbPOST))
        {
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = Mathf.CeilToInt(firebaseAuthTimeoutSeconds);
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(requestBody));
            request.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke(BuildFirebaseAuthRequestError(request, "Firebase anonymous auth token refresh failed."));
                yield break;
            }

            FirebaseRefreshTokenResponse response;

            try
            {
                response = JsonUtility.FromJson<FirebaseRefreshTokenResponse>(request.downloadHandler.text);
            }
            catch (Exception ex)
            {
                onError?.Invoke($"Invalid Firebase token refresh response: {ex.Message}");
                yield break;
            }

            if (response == null)
            {
                onError?.Invoke("Firebase token refresh returned an empty response.");
                yield break;
            }

            onSuccess?.Invoke(response);
        }
    }

    private string BuildFirebaseAuthRequestError(UnityWebRequest request, string fallbackMessage)
    {
        string parsedFirebaseError = ParseFirebaseAuthErrorMessage(request.downloadHandler != null ? request.downloadHandler.text : string.Empty);
        string statusCode = request.responseCode > 0 ? $"HTTP {request.responseCode}: " : string.Empty;

        if (!string.IsNullOrWhiteSpace(parsedFirebaseError))
        {
            return $"{statusCode}{parsedFirebaseError}";
        }

        if (!string.IsNullOrWhiteSpace(request.error))
        {
            return $"{statusCode}{request.error}";
        }

        return fallbackMessage;
    }

    private string ParseFirebaseAuthErrorMessage(string rawResponse)
    {
        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            return string.Empty;
        }

        try
        {
            FirebaseErrorResponse errorResponse = JsonUtility.FromJson<FirebaseErrorResponse>(rawResponse);
            string rawMessage = errorResponse?.error?.message;
            if (string.IsNullOrWhiteSpace(rawMessage))
            {
                return string.Empty;
            }

            switch (rawMessage)
            {
                case "OPERATION_NOT_ALLOWED":
                    return "Firebase anonymous auth is disabled for this project.";
                case "PROJECT_NUMBER_MISMATCH":
                    return "Firebase Web API key does not match this Firebase project.";
                case "INVALID_REFRESH_TOKEN":
                    return "Cached Firebase anonymous session is invalid.";
                case "TOKEN_EXPIRED":
                    return "Cached Firebase anonymous session has expired.";
                default:
                    return $"Firebase auth request failed: {rawMessage}";
            }
        }
        catch
        {
            return string.Empty;
        }
    }

    private string ResolveRuntimeConfigurationValue(string commandLineArgument, string environmentVariableName, out string sourceLabel)
    {
        string commandLineValue = ResolveCommandLineValue(commandLineArgument);
        if (!string.IsNullOrWhiteSpace(commandLineValue))
        {
            sourceLabel = $"Command line ({commandLineArgument})";
            return commandLineValue;
        }

        if (!string.IsNullOrWhiteSpace(environmentVariableName))
        {
            string environmentValue = Environment.GetEnvironmentVariable(environmentVariableName);
            if (!string.IsNullOrWhiteSpace(environmentValue))
            {
                sourceLabel = $"Environment ({environmentVariableName})";
                return environmentValue;
            }
        }

        sourceLabel = string.Empty;
        return string.Empty;
    }

    private string ResolveCommandLineValue(string commandLineArgument)
    {
        if (string.IsNullOrWhiteSpace(commandLineArgument))
        {
            return string.Empty;
        }

        string[] commandLineArgs = Environment.GetCommandLineArgs();
        for (int index = 0; index < commandLineArgs.Length - 1; index++)
        {
            if (string.Equals(commandLineArgs[index], commandLineArgument, StringComparison.OrdinalIgnoreCase))
            {
                return commandLineArgs[index + 1];
            }
        }

        return string.Empty;
    }

    private string NormalizeRuntimeFilePath(string candidatePath)
    {
        if (string.IsNullOrWhiteSpace(candidatePath))
        {
            return string.Empty;
        }

        string trimmedPath = candidatePath.Trim();
        if (Path.IsPathRooted(trimmedPath))
        {
            return trimmedPath;
        }

        return Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, trimmedPath));
    }

    private bool TryReadBearerTokenFromFile(string tokenFilePath, out string token)
    {
        token = string.Empty;

        try
        {
            if (!File.Exists(tokenFilePath))
            {
                return false;
            }

            string rawContents = File.ReadAllText(tokenFilePath).Trim();
            if (string.IsNullOrWhiteSpace(rawContents))
            {
                return false;
            }

            if (!rawContents.StartsWith("{", StringComparison.Ordinal))
            {
                token = rawContents;
                return true;
            }

            RuntimeBearerTokenFilePayload payload = JsonUtility.FromJson<RuntimeBearerTokenFilePayload>(rawContents);
            if (payload == null)
            {
                return false;
            }

            token = FirstNonEmpty(payload.accessToken, payload.bearerToken, payload.token, payload.idToken);
            return !string.IsNullOrWhiteSpace(token);
        }
        catch
        {
            return false;
        }
    }

    private string FirstNonEmpty(params string[] values)
    {
        if (values == null)
        {
            return string.Empty;
        }

        for (int index = 0; index < values.Length; index++)
        {
            if (!string.IsNullOrWhiteSpace(values[index]))
            {
                return values[index].Trim();
            }
        }

        return string.Empty;
    }

    private string ResolveTargetId(OrchestrationApiResponse response)
    {
        if (response == null)
        {
            return string.Empty;
        }

        return !string.IsNullOrWhiteSpace(response.targetId) ? response.targetId : response.instanceId;
    }

    private string ResolveTargetId(JoinTicketApiResponse response)
    {
        if (response == null)
        {
            return string.Empty;
        }

        return !string.IsNullOrWhiteSpace(response.targetId) ? response.targetId : response.instanceId;
    }

    private Texture2D CreateSolidTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }

    [Serializable]
    private class OrchestrationApiResponse
    {
        public string targetId;
        public string instanceId;
        public string instanceState;
        public string publicIpAddress;
        public string publicDnsName;
        public string connectionAddress;
        public ushort port;
        public string transportMode;
        public string relayJoinCode;
        public string relayRegion;
        public string relayConnectionType;
        public int maxPlayers;
        public bool isReady;
        public float serverWarmupSeconds;
        public string message;
    }

    [Serializable]
    private class StartServerRequestPayload
    {
        public string targetId;
        public string instanceId;
        public string lobbyCode;
        public string lobbyAction;
    }

    [Serializable]
    private class JoinTicketRequestPayload
    {
        public string playerName;
        public string targetId;
        public string instanceId;
        public string lobbyCode;
        public string lobbyAction;
    }

    [Serializable]
    private class JoinTicketApiResponse
    {
        public string targetId;
        public string instanceId;
        public string connectionAddress;
        public ushort port;
        public string transportMode;
        public string relayJoinCode;
        public string relayRegion;
        public string relayConnectionType;
        public string playerName;
        public string lobbyCode;
        public string lobbyAction;
        public string joinToken;
        public long expiresAtUnix;
        public string message;
    }

    [Serializable]
    private class RuntimeBearerTokenFilePayload
    {
        public string accessToken;
        public string bearerToken;
        public string token;
        public string idToken;
    }

    [Serializable]
    private class FirebaseAnonymousAuthRequestPayload
    {
        public bool returnSecureToken;
    }

    [Serializable]
    private class FirebaseAnonymousAuthResponse
    {
        public string localId;
        public string idToken;
        public string refreshToken;
        public string expiresIn;
        public string email;
    }

    [Serializable]
    private class FirebaseRefreshTokenResponse
    {
        public string id_token;
        public string refresh_token;
        public string expires_in;
        public string user_id;
        public string project_id;
        public string token_type;
    }

    [Serializable]
    private class FirebaseErrorResponse
    {
        public FirebaseErrorPayload error;
    }

    [Serializable]
    private class FirebaseErrorPayload
    {
        public int code;
        public string message;
    }

    private enum LobbyRequestAction
    {
        None,
        Join,
        Create,
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