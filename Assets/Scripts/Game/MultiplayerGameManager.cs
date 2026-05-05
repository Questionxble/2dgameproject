using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

public class MultiplayerGameManager : NetworkBehaviour
{
    public static event Action<string, Vector3> SessionCheckpointActivated;

    [Header("Network Settings")]
    [SerializeField] private Transform player1SpawnPoint;
    [SerializeField] private Transform player2SpawnPoint;
    [SerializeField] private Transform player3SpawnPoint;
    [SerializeField] private Transform player4SpawnPoint;
    [SerializeField] private GameObject playerPrefab;
    
    [Header("UI References (Optional - can be null)")]
    [SerializeField] private UnityEngine.UI.Button hostButton;
    [SerializeField] private UnityEngine.UI.Button clientButton;
    [SerializeField] private UnityEngine.UI.Button serverButton;
    [SerializeField] private GameObject networkUI;
    [SerializeField] private UnityEngine.UI.Text statusText;
    
    [Header("Game Settings")]
    [SerializeField] private int maxPlayers = 4; // Maximum players per session
    [SerializeField] private float fallbackSpawnSpacing = 3f;

    [Header("Join Ticket Security")]
    [SerializeField] private bool requireSignedJoinTickets = true;
    [SerializeField] private bool allowUnsignedConnectionsOutsideDedicatedServer = true;
    [SerializeField] private string joinTicketSecretEnvironmentVariable = "JOIN_TICKET_SECRET";
    [SerializeField] private string joinTicketSecretCommandLineArgument = "-jointokensecret";
    [FormerlySerializedAs("instanceIdEnvironmentVariable")]
    [SerializeField] private string targetIdEnvironmentVariable = "GAME_SERVER_TARGET_ID";
    [FormerlySerializedAs("instanceIdCommandLineArgument")]
    [SerializeField] private string targetIdCommandLineArgument = "-targetid";
    [SerializeField] private string legacyTargetIdEnvironmentVariable = "GAME_SERVER_INSTANCE_ID";
    [SerializeField] private string legacyTargetIdCommandLineArgument = "-instanceid";
    [SerializeField] private string expectedJoinTicketScope = "join";
    [SerializeField] private int joinTicketClockSkewSeconds = 30;

    [Header("Idle Shutdown")]
    [SerializeField] private bool enableIdleShutdownOnDedicatedServer = true;
    [SerializeField] private float idleShutdownTimeoutSeconds = 300f;
    [SerializeField] private string idleShutdownTimeoutEnvironmentVariable = "SERVER_IDLE_TIMEOUT_SECONDS";
    [SerializeField] private string idleShutdownTimeoutCommandLineArgument = "-idletimeout";
    [SerializeField] private string idleShutdownMarkerEnvironmentVariable = "IDLE_STOP_MARKER_FILE";

    private readonly Dictionary<ulong, int> clientSpawnSlots = new Dictionary<ulong, int>();
    private readonly Dictionary<ulong, JoinTicketClaims> approvedJoinTicketClaims = new Dictionary<ulong, JoinTicketClaims>();
    private string joinTicketSecret;
    private string configuredTargetId;
    private float configuredIdleShutdownTimeoutSeconds;
    private float idleSinceRealtime = -1f;
    private bool idleShutdownRequested;
    private string activeLobbyCode = string.Empty;
    private string activeLobbyOwnerSubject = string.Empty;
    private bool hasActiveCheckpoint;
    private Vector3 activeCheckpointPosition;
    private string activeCheckpointId = string.Empty;
    
    private void Start()
    {
        ResolveJoinTicketConfiguration();
        ResolveIdleShutdownConfiguration();

        // Disable NetworkManager auto-spawn to prevent duplicates
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.NetworkConfig.PlayerPrefab = null;
            NetworkManager.Singleton.NetworkConfig.ConnectionApproval = true;
            NetworkManager.Singleton.ConnectionApprovalCallback = ApproveConnection;
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
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
        
        UpdateStatusText("Ready to start networking...");
    }
    
    public override void OnDestroy()
    {
        // Unsubscribe from network events
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            NetworkManager.Singleton.ConnectionApprovalCallback = null;
        }

        clientSpawnSlots.Clear();
        approvedJoinTicketClaims.Clear();
        activeLobbyCode = string.Empty;
        activeLobbyOwnerSubject = string.Empty;
        ClearActiveCheckpointProgress();
        
        base.OnDestroy();
    }

    private void Update()
    {
        if (!ShouldUseIdleShutdown())
        {
            return;
        }

        NetworkManager singleton = NetworkManager.Singleton;
        if (singleton == null || !singleton.IsServer || !singleton.IsListening)
        {
            return;
        }

        if (GetConnectedPlayerCount() > 0)
        {
            ResetIdleShutdownCountdown();
            return;
        }

        if (idleSinceRealtime < 0f)
        {
            idleSinceRealtime = Time.realtimeSinceStartup;
            Debug.Log($"[MultiplayerGameManager] Dedicated server is idle. Shutting down in {configuredIdleShutdownTimeoutSeconds:F0}s if no players connect.");
            return;
        }

        if (!idleShutdownRequested && Time.realtimeSinceStartup - idleSinceRealtime >= configuredIdleShutdownTimeoutSeconds)
        {
            BeginIdleShutdown();
        }
    }

    private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        if (ShouldEnforceSignedJoinTickets())
        {
            if (string.IsNullOrWhiteSpace(joinTicketSecret))
            {
                response.Approved = false;
                response.CreatePlayerObject = false;
                response.Pending = false;
                response.Reason = "Server join-ticket validation is misconfigured.";
                return;
            }

            string joinTicket = request.Payload == null || request.Payload.Length == 0
                ? string.Empty
                : Encoding.UTF8.GetString(request.Payload);

            if (!JoinTicketUtility.TryValidateJoinTicket(
                    joinTicket,
                    joinTicketSecret,
                    configuredTargetId,
                    expectedJoinTicketScope,
                    joinTicketClockSkewSeconds,
                    out JoinTicketClaims claims,
                    out string tokenError))
            {
                response.Approved = false;
                response.CreatePlayerObject = false;
                response.Pending = false;
                response.Reason = tokenError;
                Debug.LogWarning($"[MultiplayerGameManager] Rejected client {request.ClientNetworkId}: {tokenError}");
                return;
            }

            if (!TryApproveLobbyRequest(claims, out string lobbyError))
            {
                response.Approved = false;
                response.CreatePlayerObject = false;
                response.Pending = false;
                response.Reason = lobbyError;
                Debug.LogWarning($"[MultiplayerGameManager] Rejected client {request.ClientNetworkId}: {lobbyError}");
                return;
            }

            approvedJoinTicketClaims[request.ClientNetworkId] = claims;
        }

        bool hasCapacity = clientSpawnSlots.Count < maxPlayers;

        response.Approved = hasCapacity;
        response.CreatePlayerObject = false;
        response.Pending = false;
        response.Reason = hasCapacity ? string.Empty : $"Session is full ({maxPlayers} players).";
    }

    private void ResolveJoinTicketConfiguration()
    {
        joinTicketSecret = ResolveConfigurationValue(joinTicketSecretEnvironmentVariable, joinTicketSecretCommandLineArgument);
        configuredTargetId = ResolveConfigurationValue(
            targetIdEnvironmentVariable,
            targetIdCommandLineArgument,
            legacyTargetIdEnvironmentVariable,
            legacyTargetIdCommandLineArgument);

        if (ShouldEnforceSignedJoinTickets())
        {
            if (string.IsNullOrWhiteSpace(joinTicketSecret))
            {
                Debug.LogWarning("[MultiplayerGameManager] Signed join tickets are required, but no join-ticket secret was provided.");
            }

            if (string.IsNullOrWhiteSpace(configuredTargetId))
            {
                Debug.Log("[MultiplayerGameManager] No server target id was configured. Join tickets will skip target id matching.");
            }
        }
    }

    private void ResolveIdleShutdownConfiguration()
    {
        configuredIdleShutdownTimeoutSeconds = Mathf.Max(0f, idleShutdownTimeoutSeconds);

        string configuredTimeout = ResolveConfigurationValue(idleShutdownTimeoutEnvironmentVariable, idleShutdownTimeoutCommandLineArgument);
        if (string.IsNullOrWhiteSpace(configuredTimeout))
        {
            return;
        }

        if (float.TryParse(configuredTimeout, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedTimeout) && parsedTimeout >= 0f)
        {
            configuredIdleShutdownTimeoutSeconds = parsedTimeout;
            return;
        }

        Debug.LogWarning($"[MultiplayerGameManager] Could not parse idle shutdown timeout '{configuredTimeout}'. Using fallback value {configuredIdleShutdownTimeoutSeconds:F0}s.");
    }

    private bool ShouldEnforceSignedJoinTickets()
    {
        if (!requireSignedJoinTickets)
        {
            return false;
        }

        if (allowUnsignedConnectionsOutsideDedicatedServer && !IsDedicatedServerRuntime())
        {
            return false;
        }

        return true;
    }

    private bool ShouldUseIdleShutdown()
    {
        return enableIdleShutdownOnDedicatedServer
            && IsDedicatedServerRuntime()
            && configuredIdleShutdownTimeoutSeconds > 0f;
    }

    private bool TryApproveLobbyRequest(JoinTicketClaims claims, out string error)
    {
        error = string.Empty;

        if (!ShouldEnforceSignedJoinTickets() || !IsDedicatedServerRuntime())
        {
            return true;
        }

        string lobbyCode = claims == null || claims.LobbyCode == null ? string.Empty : claims.LobbyCode.Trim();
        string lobbyAction = claims == null || claims.LobbyAction == null ? string.Empty : claims.LobbyAction.Trim();
        bool isCreateRequest = string.Equals(lobbyAction, "create", StringComparison.Ordinal);
        bool isJoinRequest = string.Equals(lobbyAction, "join", StringComparison.Ordinal);

        if (string.IsNullOrWhiteSpace(lobbyCode))
        {
            error = "Join ticket is missing a lobby code.";
            return false;
        }

        if (!isCreateRequest && !isJoinRequest)
        {
            error = "Join ticket lobby action is invalid.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(activeLobbyCode))
        {
            if (!isCreateRequest)
            {
                error = "No active lobby exists on this dedicated server. Create one first.";
                return false;
            }

            activeLobbyCode = lobbyCode;
            activeLobbyOwnerSubject = claims.Subject;
            Debug.Log($"[MultiplayerGameManager] Reserved lobby '{activeLobbyCode}' for session owner '{activeLobbyOwnerSubject}'.");
            return true;
        }

        if (!string.Equals(activeLobbyCode, lobbyCode, StringComparison.Ordinal))
        {
            error = "Lobby code does not match the active dedicated session.";
            return false;
        }

        if (isCreateRequest)
        {
            error = "This dedicated session already has an active lobby. Use Join Lobby instead.";
            return false;
        }

        return true;
    }

    private void ResetIdleShutdownCountdown()
    {
        if (idleSinceRealtime < 0f)
        {
            return;
        }

        idleSinceRealtime = -1f;

        if (!idleShutdownRequested)
        {
            Debug.Log("[MultiplayerGameManager] Dedicated server activity detected. Idle shutdown cancelled.");
        }
    }

    private void BeginIdleShutdown()
    {
        if (idleShutdownRequested)
        {
            return;
        }

        idleShutdownRequested = true;
        Debug.Log($"[MultiplayerGameManager] No players remained connected for {configuredIdleShutdownTimeoutSeconds:F0}s. Stopping dedicated server.");
        WriteIdleShutdownMarker();

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }

        Application.Quit();
    }

    private void WriteIdleShutdownMarker()
    {
        string markerPath = Environment.GetEnvironmentVariable(idleShutdownMarkerEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(markerPath))
        {
            return;
        }

        try
        {
            string directoryPath = Path.GetDirectoryName(markerPath);
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            File.WriteAllText(markerPath, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[MultiplayerGameManager] Failed to write idle shutdown marker '{markerPath}': {ex.Message}");
        }
    }

    private void ClearActiveLobbyReservation(string reason)
    {
        if (string.IsNullOrWhiteSpace(activeLobbyCode))
        {
            return;
        }

        Debug.Log($"[MultiplayerGameManager] Clearing lobby '{activeLobbyCode}'. {reason}");
        activeLobbyCode = string.Empty;
        activeLobbyOwnerSubject = string.Empty;
    }

    private bool IsDedicatedServerRuntime()
    {
        return Application.isBatchMode
            || Application.platform == RuntimePlatform.LinuxServer
            || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null;
    }

    private string ResolveConfigurationValue(
        string environmentVariableName,
        string commandLineArgument,
        string legacyEnvironmentVariableName = "",
        string legacyCommandLineArgument = "")
    {
        string environmentValue = Environment.GetEnvironmentVariable(environmentVariableName);
        if (!string.IsNullOrWhiteSpace(environmentValue))
        {
            return environmentValue;
        }

        if (!string.IsNullOrWhiteSpace(legacyEnvironmentVariableName))
        {
            environmentValue = Environment.GetEnvironmentVariable(legacyEnvironmentVariableName);
            if (!string.IsNullOrWhiteSpace(environmentValue))
            {
                return environmentValue;
            }
        }

        string[] args = Environment.GetCommandLineArgs();
        for (int index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], commandLineArgument, StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }

            if (!string.IsNullOrWhiteSpace(legacyCommandLineArgument)
                && string.Equals(args[index], legacyCommandLineArgument, StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }

        return string.Empty;
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
        ResetIdleShutdownCountdown();
        
        // If we're the server, spawn the player
        if (IsServer)
        {
            if (!TryReserveSpawnSlot(clientId, out int slotIndex))
            {
                Debug.LogWarning($"Max players ({maxPlayers}) exceeded. Disconnecting client {clientId}");
                NetworkManager.Singleton.DisconnectClient(clientId);
                return;
            }

            SpawnPlayerForClient(clientId, slotIndex);
        }

        UpdateStatusText($"Client {clientId} connected ({GetConnectedPlayerCount()}/{maxPlayers} players)");
    }
    
    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"Client {clientId} disconnected");
        if (IsServer)
        {
            clientSpawnSlots.Remove(clientId);
            approvedJoinTicketClaims.Remove(clientId);

            if (GetConnectedPlayerCount() == 0)
            {
                ClearActiveLobbyReservation("The last connected player left the session.");
                ClearActiveCheckpointProgress("Checkpoint progress reset because the session is empty.");
            }
        }

        UpdateStatusText($"Client {clientId} disconnected ({GetConnectedPlayerCount()}/{maxPlayers} players)");
    }

    private bool TryReserveSpawnSlot(ulong clientId, out int slotIndex)
    {
        if (clientSpawnSlots.TryGetValue(clientId, out slotIndex))
        {
            return true;
        }

        for (int candidate = 0; candidate < maxPlayers; candidate++)
        {
            if (IsSpawnSlotAvailable(candidate))
            {
                clientSpawnSlots[clientId] = candidate;
                slotIndex = candidate;
                return true;
            }
        }

        slotIndex = -1;
        return false;
    }

    private bool IsSpawnSlotAvailable(int slotIndex)
    {
        foreach (int reservedSlot in clientSpawnSlots.Values)
        {
            if (reservedSlot == slotIndex)
            {
                return false;
            }
        }

        return true;
    }

    private int GetConnectedPlayerCount()
    {
        if (IsServer)
        {
            return clientSpawnSlots.Count;
        }

        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening
            ? NetworkManager.Singleton.ConnectedClientsIds.Count
            : 0;
    }

    private void SpawnPlayerForClient(ulong clientId, int slotIndex)
    {
        if (!IsServer) return;

        Vector3 spawnPosition = GetSpawnPositionForSlot(slotIndex);
        
        // Spawn player
        if (playerPrefab != null)
        {
            GameObject playerInstance = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
            NetworkObject networkObject = playerInstance.GetComponent<NetworkObject>();
            
            if (networkObject != null)
            {
                networkObject.SpawnAsPlayerObject(clientId);
                Debug.Log($"Spawned player for client {clientId} in slot {slotIndex} at position {spawnPosition}");
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
            if (TryReserveSpawnSlot(client, out int slotIndex))
            {
                SpawnPlayerForClient(client, slotIndex);
            }
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
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RespawnPlayerServerRpc(ulong clientId)
    {
        RespawnPlayerForClient(clientId);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void ActivateCheckpointServerRpc(Vector3 checkpointPosition, FixedString128Bytes checkpointId)
    {
        SetActiveCheckpoint(checkpointPosition, checkpointId.ToString());
    }

    public void SetActiveCheckpoint(Vector3 checkpointPosition, string checkpointId)
    {
        if (!IsServer)
        {
            return;
        }

        string resolvedCheckpointId = string.IsNullOrWhiteSpace(checkpointId)
            ? "checkpoint"
            : checkpointId.Trim();

        bool checkpointChanged = !hasActiveCheckpoint
            || !string.Equals(activeCheckpointId, resolvedCheckpointId, StringComparison.Ordinal)
            || (activeCheckpointPosition - checkpointPosition).sqrMagnitude > 0.001f;

        hasActiveCheckpoint = true;
        activeCheckpointPosition = checkpointPosition;
        activeCheckpointId = resolvedCheckpointId;

        if (checkpointChanged)
        {
            Debug.Log($"[MultiplayerGameManager] Active checkpoint set to '{activeCheckpointId}' at {activeCheckpointPosition}.");
            NotifyCheckpointActivatedClientRpc(new FixedString128Bytes(activeCheckpointId), activeCheckpointPosition);
        }
    }

    [ClientRpc]
    private void NotifyCheckpointActivatedClientRpc(FixedString128Bytes checkpointId, Vector3 checkpointPosition)
    {
        SessionCheckpointActivated?.Invoke(checkpointId.ToString(), checkpointPosition);
    }

    private void ClearActiveCheckpointProgress(string reason = null)
    {
        bool hadCheckpoint = hasActiveCheckpoint;
        hasActiveCheckpoint = false;
        activeCheckpointPosition = Vector3.zero;
        activeCheckpointId = string.Empty;

        if (hadCheckpoint && !string.IsNullOrWhiteSpace(reason))
        {
            Debug.Log($"[MultiplayerGameManager] {reason}");
        }
    }

    public void RespawnPlayerForClient(ulong clientId)
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

        if (!TryReserveSpawnSlot(clientId, out int slotIndex))
        {
            slotIndex = 0;
        }

        Vector3 spawnPosition = GetSpawnPositionForSlot(slotIndex);
        
        // Reset player at the correct spawn position
        PlayerMovement playerMovement = playerObject.GetComponent<PlayerMovement>();
        if (playerMovement != null)
        {
            playerMovement.RespawnPlayer(spawnPosition);
        }
        
        Debug.Log($"Respawned player for client {clientId} in slot {slotIndex} at position {spawnPosition}");
    }

    private Vector3 GetSpawnPositionForSlot(int slotIndex)
    {
        if (hasActiveCheckpoint)
        {
            return activeCheckpointPosition + GetFallbackSpawnOffsetForSlot(slotIndex);
        }

        Vector3 origin = player1SpawnPoint != null ? player1SpawnPoint.position : Vector3.zero;

        switch (slotIndex)
        {
            case 0:
                return player1SpawnPoint != null ? player1SpawnPoint.position : origin;
            case 1:
                return player2SpawnPoint != null ? player2SpawnPoint.position : origin + GetFallbackSpawnOffsetForSlot(slotIndex);
            case 2:
                return player3SpawnPoint != null ? player3SpawnPoint.position : origin + GetFallbackSpawnOffsetForSlot(slotIndex);
            case 3:
                return player4SpawnPoint != null ? player4SpawnPoint.position : origin + GetFallbackSpawnOffsetForSlot(slotIndex);
            default:
                return origin + GetFallbackSpawnOffsetForSlot(slotIndex);
        }
    }

    private Vector3 GetFallbackSpawnOffsetForSlot(int slotIndex)
    {
        switch (slotIndex)
        {
            case 0:
                return Vector3.zero;
            case 1:
                return Vector3.right * fallbackSpawnSpacing;
            case 2:
                return Vector3.left * fallbackSpawnSpacing;
            case 3:
                return Vector3.right * fallbackSpawnSpacing * 2f;
            default:
                return Vector3.right * fallbackSpawnSpacing * slotIndex;
        }
    }
}