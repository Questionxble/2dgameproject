using System;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System.Collections;
using System.Text;
using UnityEngine.Networking;

public class DedicatedServerConfig : MonoBehaviour
{
    [Header("Server Configuration")]
    [SerializeField] private bool isDedicatedServer = false;
    [SerializeField] private string serverListenAddress = "0.0.0.0";
    [SerializeField] private ushort serverPort = 7777;
    [SerializeField] private int maxPlayers = 4;

    [Header("Secure Relay Transport")]
    [SerializeField] private bool useRelaySecureTransport = false;
    [SerializeField] private string relayConnectionType = RelayTransportBootstrap.DefaultConnectionType;
    [SerializeField] private int relayMaxConnections = 4;

    [Header("Runtime Status Reporting")]
    [SerializeField] private bool enableRuntimeStatusReporting = true;
    [SerializeField] private string orchestrationApiBaseUrl = "";
    [SerializeField] private string runtimeStatusEndpoint = "/server/runtime";
    [SerializeField] private string registrationHeaderName = "X-Server-Registration-Token";
    [SerializeField] private string registrationToken = "";
    [SerializeField] private float runtimeStatusTimeoutSeconds = 10f;
    [SerializeField] private float runtimeStatusReportIntervalSeconds = 5f;

    private string configuredTargetId = string.Empty;
    private Coroutine runtimeStatusReporterRoutine;

    private const string OrchestrationUrlEnvironmentVariable = "GAME_SERVER_ORCHESTRATION_URL";
    private const string RuntimeStatusEndpointEnvironmentVariable = "GAME_SERVER_RUNTIME_STATUS_ENDPOINT";
    private const string RegistrationHeaderEnvironmentVariable = "SERVER_REGISTRATION_HEADER_NAME";
    private const string RegistrationTokenEnvironmentVariable = "GAME_SERVER_REGISTRATION_TOKEN";
    private const string TransportModeEnvironmentVariable = "GAME_TRANSPORT_MODE";
    private const string RelayConnectionTypeEnvironmentVariable = "RELAY_CONNECTION_TYPE";
    private const string ServerPortEnvironmentVariable = "SERVER_PORT";
    private const string MaxPlayersEnvironmentVariable = "MAX_PLAYERS";
    private const string TargetIdEnvironmentVariable = "GAME_SERVER_TARGET_ID";
    private const string LegacyTargetIdEnvironmentVariable = "GAME_SERVER_INSTANCE_ID";
    private const string JoinTicketSecretEnvironmentVariable = "JOIN_TICKET_SECRET";
    
    private void Start()
    {
        // Detailed startup logging
        Debug.Log("=== DedicatedServerConfig Starting ===");
        Debug.Log($"Application.isBatchMode: {Application.isBatchMode}");
        Debug.Log($"SystemInfo.graphicsDeviceType: {SystemInfo.graphicsDeviceType}");
        Debug.Log($"Application.platform: {Application.platform}");
        
        // Check if running in dedicated server mode
        if (Application.isBatchMode
            || Application.platform == RuntimePlatform.LinuxServer
            || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
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
        ResolveRuntimeConfiguration();
        
        // Configure network settings if this is a server
        if (isDedicatedServer)
        {
            maxPlayers = Mathf.Max(1, maxPlayers);
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
                        maxPlayers = Mathf.Max(1, maxP);
                        Debug.Log($"Max players set to: {maxPlayers}");
                    }
                    break;
                case "-server":
                    isDedicatedServer = true;
                    Debug.Log("Dedicated server mode enabled via command line");
                    break;
                case "-transportmode":
                    if (i + 1 < args.Length)
                    {
                        useRelaySecureTransport = string.Equals(args[i + 1], RelayTransportBootstrap.TransportModeRelay, StringComparison.OrdinalIgnoreCase);
                        Debug.Log($"Transport mode set to: {(useRelaySecureTransport ? RelayTransportBootstrap.TransportModeRelay : "direct")}");
                    }
                    break;
                case "-relayconnectiontype":
                    if (i + 1 < args.Length)
                    {
                        relayConnectionType = RelayTransportBootstrap.NormalizeConnectionType(args[i + 1]);
                        Debug.Log($"Relay connection type set to: {relayConnectionType}");
                    }
                    break;
                case "-orchestrationurl":
                    if (i + 1 < args.Length)
                    {
                        orchestrationApiBaseUrl = args[i + 1];
                    }
                    break;
                case "-runtimestatusendpoint":
                    if (i + 1 < args.Length)
                    {
                        runtimeStatusEndpoint = args[i + 1];
                    }
                    break;
                case "-serverregistrationtoken":
                    if (i + 1 < args.Length)
                    {
                        registrationToken = args[i + 1];
                    }
                    break;
                case "-serverregistrationheader":
                    if (i + 1 < args.Length)
                    {
                        registrationHeaderName = args[i + 1];
                    }
                    break;
                case "-targetid":
                case "-instanceid":
                    if (i + 1 < args.Length)
                    {
                        configuredTargetId = args[i + 1];
                    }
                    break;
            }
        }
    }

    private void ResolveRuntimeConfiguration()
    {
        relayConnectionType = RelayTransportBootstrap.NormalizeConnectionType(ResolveFirstNonEmpty(
            Environment.GetEnvironmentVariable(RelayConnectionTypeEnvironmentVariable),
            relayConnectionType,
            RelayTransportBootstrap.DefaultConnectionType));

        string resolvedTransportMode = ResolveFirstNonEmpty(Environment.GetEnvironmentVariable(TransportModeEnvironmentVariable));
        if (!string.IsNullOrWhiteSpace(resolvedTransportMode))
        {
            useRelaySecureTransport = string.Equals(resolvedTransportMode, RelayTransportBootstrap.TransportModeRelay, StringComparison.OrdinalIgnoreCase);
        }

        if (ushort.TryParse(Environment.GetEnvironmentVariable(ServerPortEnvironmentVariable), out ushort resolvedServerPort))
        {
            serverPort = resolvedServerPort;
        }

        if (int.TryParse(Environment.GetEnvironmentVariable(MaxPlayersEnvironmentVariable), out int resolvedMaxPlayers))
        {
            maxPlayers = Mathf.Max(1, resolvedMaxPlayers);
        }

        orchestrationApiBaseUrl = ResolveFirstNonEmpty(
            Environment.GetEnvironmentVariable(OrchestrationUrlEnvironmentVariable),
            orchestrationApiBaseUrl);
        runtimeStatusEndpoint = ResolveFirstNonEmpty(
            Environment.GetEnvironmentVariable(RuntimeStatusEndpointEnvironmentVariable),
            runtimeStatusEndpoint,
            "/server/runtime");
        registrationHeaderName = ResolveFirstNonEmpty(
            Environment.GetEnvironmentVariable(RegistrationHeaderEnvironmentVariable),
            registrationHeaderName,
            "X-Server-Registration-Token");
        registrationToken = ResolveFirstNonEmpty(
            Environment.GetEnvironmentVariable(RegistrationTokenEnvironmentVariable),
            registrationToken,
            Environment.GetEnvironmentVariable(JoinTicketSecretEnvironmentVariable));
        configuredTargetId = ResolveFirstNonEmpty(
            configuredTargetId,
            Environment.GetEnvironmentVariable(TargetIdEnvironmentVariable),
            Environment.GetEnvironmentVariable(LegacyTargetIdEnvironmentVariable));

        if (runtimeStatusTimeoutSeconds <= 0f)
        {
            runtimeStatusTimeoutSeconds = 10f;
        }

        if (runtimeStatusReportIntervalSeconds <= 0f)
        {
            runtimeStatusReportIntervalSeconds = 5f;
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

        if (useRelaySecureTransport)
        {
            StartCoroutine(StartServerWithRelayAfterDelay(networkManager, transport));
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

    private IEnumerator StartServerWithRelayAfterDelay(NetworkManager networkManager, UnityTransport transport)
    {
        yield return null;

        RelayTransportBootstrap.RelayHostSession relaySession = null;
        string relayError = null;

        yield return RelayTransportBootstrap.ConfigureServerTransportForRelay(
            transport,
            Mathf.Max(1, relayMaxConnections),
            relayConnectionType,
            session => relaySession = session,
            error => relayError = error);

        if (!string.IsNullOrWhiteSpace(relayError))
        {
            Debug.LogError(relayError);
            yield break;
        }

        if (relaySession != null)
        {
            Debug.Log($"Relay secure transport configured with {relaySession.ConnectionType}. Join code: {relaySession.JoinCode}");
        }

        Debug.Log("Starting dedicated server over Relay secure transport...");
        bool serverStarted = networkManager.StartServer();

        if (serverStarted)
        {
            Debug.Log("✅ Dedicated server started successfully over Relay secure transport");
            Debug.Log($"Waiting for up to {Mathf.Max(1, relayMaxConnections)} players to connect...");
            StartRuntimeStatusReporter(RelayTransportBootstrap.TransportModeRelay, relaySession);
        }
        else
        {
            Debug.LogError("❌ Failed to start dedicated server over Relay secure transport!");
        }
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
                StartRuntimeStatusReporter("direct", null);
            }
            else
            {
                Debug.LogError("❌ Failed to start dedicated server!");
            }
        }
    }

    private void StartRuntimeStatusReporter(string transportMode, RelayTransportBootstrap.RelayHostSession relaySession)
    {
        if (runtimeStatusReporterRoutine != null)
        {
            StopCoroutine(runtimeStatusReporterRoutine);
            runtimeStatusReporterRoutine = null;
        }

        if (!enableRuntimeStatusReporting)
        {
            LogMissingRuntimeStatusConfiguration(transportMode, "Runtime status reporting is disabled.");
            return;
        }

        if (string.IsNullOrWhiteSpace(orchestrationApiBaseUrl)
            || string.IsNullOrWhiteSpace(registrationToken)
            || string.IsNullOrWhiteSpace(configuredTargetId))
        {
            LogMissingRuntimeStatusConfiguration(transportMode, "Runtime status reporting is not fully configured.");
            return;
        }

        runtimeStatusReporterRoutine = StartCoroutine(ReportRuntimeStatusLoop(transportMode, relaySession));
    }

    private void LogMissingRuntimeStatusConfiguration(string transportMode, string message)
    {
        if (string.Equals(transportMode, RelayTransportBootstrap.TransportModeRelay, StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogWarning($"{message} Set {OrchestrationUrlEnvironmentVariable}, {TargetIdEnvironmentVariable}, and either {RegistrationTokenEnvironmentVariable} or {JoinTicketSecretEnvironmentVariable} so the control plane can publish the Relay join code.");
        }
    }

    private IEnumerator ReportRuntimeStatusLoop(string transportMode, RelayTransportBootstrap.RelayHostSession relaySession)
    {
        while (ShouldContinueRuntimeStatusReporting())
        {
            yield return SendRuntimeStatusUpdate(transportMode, relaySession);
            yield return new WaitForSecondsRealtime(runtimeStatusReportIntervalSeconds);
        }

        runtimeStatusReporterRoutine = null;
    }

    private bool ShouldContinueRuntimeStatusReporting()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        return isDedicatedServer && networkManager != null && networkManager.IsServer && networkManager.IsListening;
    }

    private IEnumerator SendRuntimeStatusUpdate(string transportMode, RelayTransportBootstrap.RelayHostSession relaySession)
    {
        string url = BuildOrchestrationUrl(runtimeStatusEndpoint);
        if (string.IsNullOrWhiteSpace(url))
        {
            yield break;
        }

        RuntimeStatusPayload payload = CreateRuntimeStatusPayload(transportMode, relaySession);
        byte[] body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));

        using (UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            request.downloadHandler = new DownloadHandlerBuffer();
            request.uploadHandler = new UploadHandlerRaw(body);
            request.timeout = Mathf.CeilToInt(runtimeStatusTimeoutSeconds);
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader(registrationHeaderName, registrationToken);

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                string statusCode = request.responseCode > 0 ? $"HTTP {request.responseCode}: " : string.Empty;
                Debug.LogWarning($"Dedicated server runtime status update failed: {statusCode}{request.error}");
            }
        }
    }

    private RuntimeStatusPayload CreateRuntimeStatusPayload(string transportMode, RelayTransportBootstrap.RelayHostSession relaySession)
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        int connectedPlayers = networkManager != null && networkManager.IsServer
            ? networkManager.ConnectedClientsIds.Count
            : 0;

        string directConnectionAddress = string.Equals(transportMode, RelayTransportBootstrap.TransportModeRelay, StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : ResolveDirectConnectionAddress();

        return new RuntimeStatusPayload
        {
            targetId = configuredTargetId,
            instanceId = configuredTargetId,
            transportMode = transportMode,
            connectionAddress = directConnectionAddress,
            port = serverPort,
            relayJoinCode = relaySession != null ? relaySession.JoinCode : string.Empty,
            relayRegion = relaySession != null ? relaySession.Region : string.Empty,
            relayConnectionType = relaySession != null ? relaySession.ConnectionType : relayConnectionType,
            maxPlayers = string.Equals(transportMode, RelayTransportBootstrap.TransportModeRelay, StringComparison.OrdinalIgnoreCase)
                ? Mathf.Max(1, relayMaxConnections)
                : Mathf.Max(1, maxPlayers),
            connectedPlayers = connectedPlayers,
            isReady = networkManager != null && networkManager.IsServer && networkManager.IsListening,
        };
    }

    private string ResolveDirectConnectionAddress()
    {
        if (string.Equals(serverListenAddress, "0.0.0.0", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(serverListenAddress))
        {
            return string.Empty;
        }

        return serverListenAddress;
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

    private static string ResolveFirstNonEmpty(params string[] values)
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

    private void OnDestroy()
    {
        if (runtimeStatusReporterRoutine != null)
        {
            StopCoroutine(runtimeStatusReporterRoutine);
            runtimeStatusReporterRoutine = null;
        }
    }

    [Serializable]
    private class RuntimeStatusPayload
    {
        public string targetId;
        public string instanceId;
        public string transportMode;
        public string connectionAddress;
        public ushort port;
        public string relayJoinCode;
        public string relayRegion;
        public string relayConnectionType;
        public int maxPlayers;
        public int connectedPlayers;
        public bool isReady;
    }
}