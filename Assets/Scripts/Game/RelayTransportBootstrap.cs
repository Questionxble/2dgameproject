using System;
using System.Collections;
using System.Threading.Tasks;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

public static class RelayTransportBootstrap
{
    public const string TransportModeRelay = "relay";
    public const string DefaultConnectionType = "dtls";

    public sealed class RelayHostSession
    {
        public RelayHostSession(string joinCode, string connectionType, string region, string allocationId)
        {
            JoinCode = joinCode;
            ConnectionType = connectionType;
            Region = region;
            AllocationId = allocationId;
        }

        public string JoinCode { get; }
        public string ConnectionType { get; }
        public string Region { get; }
        public string AllocationId { get; }
    }

    private sealed class RelayHostAllocationResult
    {
        public RelayHostAllocationResult(Allocation allocation, string joinCode)
        {
            Allocation = allocation;
            JoinCode = joinCode;
        }

        public Allocation Allocation { get; }
        public string JoinCode { get; }
    }

    public static string NormalizeConnectionType(string rawConnectionType)
    {
        if (string.IsNullOrWhiteSpace(rawConnectionType))
        {
            return DefaultConnectionType;
        }

        switch (rawConnectionType.Trim().ToLowerInvariant())
        {
            case "udp":
            case "dtls":
            case "ws":
            case "wss":
                return rawConnectionType.Trim().ToLowerInvariant();
            default:
                return DefaultConnectionType;
        }
    }

    public static IEnumerator ConfigureClientTransportForRelay(
        UnityTransport transport,
        string relayJoinCode,
        string connectionType,
        Action<string> onSuccess,
        Action<string> onError)
    {
        if (transport == null)
        {
            onError?.Invoke("Unity Transport is missing, so Relay transport cannot be configured.");
            yield break;
        }

        if (string.IsNullOrWhiteSpace(relayJoinCode))
        {
            onError?.Invoke("Relay join code is missing from the server response.");
            yield break;
        }

        string normalizedConnectionType = NormalizeConnectionType(connectionType);
        string setupError = null;
        Task<JoinAllocation> joinTask = JoinRelayAllocationAsync(relayJoinCode);

        yield return AwaitTask(joinTask, exception => setupError = exception.Message);

        if (!string.IsNullOrWhiteSpace(setupError))
        {
            onError?.Invoke($"Relay client setup failed: {setupError}");
            yield break;
        }

        transport.UseWebSockets = normalizedConnectionType.StartsWith("ws", StringComparison.OrdinalIgnoreCase);
        transport.SetRelayServerData(new RelayServerData(joinTask.Result, normalizedConnectionType));
        onSuccess?.Invoke(normalizedConnectionType);
    }

    public static IEnumerator ConfigureServerTransportForRelay(
        UnityTransport transport,
        int maxConnections,
        string connectionType,
        Action<RelayHostSession> onSuccess,
        Action<string> onError)
    {
        if (transport == null)
        {
            onError?.Invoke("Unity Transport is missing, so Relay transport cannot be configured.");
            yield break;
        }

        string normalizedConnectionType = NormalizeConnectionType(connectionType);
        string setupError = null;
        Task<RelayHostAllocationResult> allocationTask = CreateRelayAllocationAsync(maxConnections);

        yield return AwaitTask(allocationTask, exception => setupError = exception.Message);

        if (!string.IsNullOrWhiteSpace(setupError))
        {
            onError?.Invoke($"Relay server setup failed: {setupError}");
            yield break;
        }

        RelayHostAllocationResult allocationResult = allocationTask.Result;
        transport.UseWebSockets = normalizedConnectionType.StartsWith("ws", StringComparison.OrdinalIgnoreCase);
        transport.SetRelayServerData(new RelayServerData(allocationResult.Allocation, normalizedConnectionType));
        onSuccess?.Invoke(new RelayHostSession(
            allocationResult.JoinCode,
            normalizedConnectionType,
            allocationResult.Allocation.Region,
            allocationResult.Allocation.AllocationId.ToString()));
    }

    private static async Task EnsureServicesReadyAsync()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
        {
            await UnityServices.InitializeAsync();
        }

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
    }

    private static async Task<JoinAllocation> JoinRelayAllocationAsync(string relayJoinCode)
    {
        await EnsureServicesReadyAsync();
        return await RelayService.Instance.JoinAllocationAsync(relayJoinCode);
    }

    private static async Task<RelayHostAllocationResult> CreateRelayAllocationAsync(int maxConnections)
    {
        await EnsureServicesReadyAsync();
        Allocation allocation = await RelayService.Instance.CreateAllocationAsync(Mathf.Max(1, maxConnections));
        string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
        return new RelayHostAllocationResult(allocation, joinCode);
    }

    private static IEnumerator AwaitTask(Task task, Action<Exception> onError)
    {
        while (!task.IsCompleted)
        {
            yield return null;
        }

        if (task.IsFaulted)
        {
            onError?.Invoke(task.Exception?.GetBaseException() ?? new Exception("Task failed."));
            yield break;
        }

        if (task.IsCanceled)
        {
            onError?.Invoke(new Exception("Task was canceled."));
        }
    }
}