using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider2D))]
public class ChestInteractable : NetworkBehaviour
{
    [System.Serializable]
    private class ChestLootEntry
    {
        public CollectibleItem collectiblePrefab;
        public int stackAmount = 1;
        public float horizontalJitter = 0.1f;
        public float verticalOffset = 0f;
    }

    [Header("Interaction")]
    [SerializeField] private Key interactionKey = Key.E;
    [SerializeField] private Key lockpickKey = Key.X;
    [SerializeField] private bool requiresLockpickRelicToOpen;
    [SerializeField] private float interactionDistanceTolerance = 1f;
    [SerializeField] private Vector3 promptWorldOffset = new Vector3(0f, 1.8f, 0f);

    [Header("Lockpick Mini Game")]
    [SerializeField] private Vector2 lockpickBarSize = new Vector2(220f, 18f);
    [SerializeField] private float lockpickIndicatorHeight = 44f;
    [SerializeField] private float lockpickIndicatorWidth = 5f;
    [SerializeField] private float lockpickGreenZoneWidthNormalized = 0.12f;
    [SerializeField] private float lockpickGreenZoneMinCenterNormalized = 0.24f;
    [SerializeField] private float lockpickGreenZoneMaxCenterNormalized = 0.76f;
    [SerializeField] private float lockpickSweepSpeedNormalized = 0.9f;
    [SerializeField] private float lockpickFailureShakeDuration = 0.16f;
    [SerializeField] private float lockpickFailureShakeDistance = 7f;
    [SerializeField] private float lockpickFailureShakeFrequency = 38f;
    [SerializeField] private Vector3 lockpickUiOffset = new Vector3(0f, 2.45f, 0f);

    [Header("Animation")]
    [SerializeField] private Animator chestAnimator;
    [SerializeField] private string openTriggerParameter = "Open";
    [SerializeField] private string openBoolParameter = "IsOpen";
    [SerializeField] private bool setOpenTrigger = true;
    [SerializeField] private bool setOpenBool = true;

    [Header("Loot Release")]
    [SerializeField] private ChestLootEntry[] lootEntries = new ChestLootEntry[0];
    [SerializeField] private Transform lootLaunchOrigin;
    [SerializeField] private Transform lootLandingCenter;
    [SerializeField] private Vector3 lootLaunchOriginOffset = new Vector3(0f, 1.25f, 0f);
    [SerializeField] private Vector3 lootLandingCenterOffset = new Vector3(0f, 0.15f, 0f);
    [SerializeField] private float lootSpreadWidth = 1.4f;
    [SerializeField] private float lootLaunchDuration = 0.45f;
    [SerializeField] private float lootLaunchArcHeight = 0.85f;
    [SerializeField] private float lootReleaseStaggerSeconds = 0.05f;

    private readonly NetworkVariable<bool> networkIsOpened = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<ulong> networkActiveLockpickerClientId = new NetworkVariable<ulong>(ulong.MaxValue, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private Collider2D chestCollider;
    private PlayerMovement nearbyLocalPlayer;
    private bool localIsOpened;
    private bool localLockpickReservationActive;
    private bool localLockpickMiniGameActive;
    private bool isWaitingForLockpickReservation;
    private bool openPresentationApplied;
    private float lockpickFailureShakeEndTime;
    private float localLockpickGreenZoneCenterNormalized;
    private float localLockpickSweepNormalized;
    private float localLockpickSweepDirection = 1f;
    private Coroutine lootReleaseRoutine;
    private GUIStyle promptLabelStyle;
    private GUIStyle promptBoxStyle;
    private GUIStyle lockpickInstructionStyle;

    private bool IsLocked => requiresLockpickRelicToOpen;
    private bool IsChestInUse => NetworkObject != null && NetworkObject.IsSpawned ? networkActiveLockpickerClientId.Value != ulong.MaxValue : localLockpickReservationActive;
    private bool IsOpened => NetworkObject != null && NetworkObject.IsSpawned ? networkIsOpened.Value : localIsOpened;

    private void Awake()
    {
        chestCollider = GetComponent<Collider2D>();
        if (chestAnimator == null)
        {
            chestAnimator = GetComponentInChildren<Animator>();
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        networkIsOpened.OnValueChanged += OnOpenedStateChanged;
        networkActiveLockpickerClientId.OnValueChanged += OnActiveLockpickerChanged;

        if (NetworkManager != null)
        {
            NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
        }

        if (networkIsOpened.Value)
        {
            ApplyOpenedPresentation();
        }
    }

    public override void OnNetworkDespawn()
    {
        networkIsOpened.OnValueChanged -= OnOpenedStateChanged;
        networkActiveLockpickerClientId.OnValueChanged -= OnActiveLockpickerChanged;
        if (NetworkManager != null)
        {
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }
        base.OnNetworkDespawn();
    }

    private void OnDisable()
    {
        nearbyLocalPlayer = null;
        if (localLockpickMiniGameActive)
        {
            EndLocalLockpickSession(releaseReservation: false);
        }
    }

    public override void OnDestroy()
    {
        if (lootReleaseRoutine != null)
        {
            StopCoroutine(lootReleaseRoutine);
            lootReleaseRoutine = null;
        }

        base.OnDestroy();
    }

    private void Update()
    {
        if (IsOpened)
        {
            return;
        }

        if (localLockpickMiniGameActive)
        {
            UpdateLockpickSweepAnimation();

            if (Keyboard.current != null && Keyboard.current[lockpickKey] != null && Keyboard.current[lockpickKey].wasPressedThisFrame)
            {
                ResolveLocalLockpickAttempt();
            }

            return;
        }

        if (nearbyLocalPlayer == null || Keyboard.current == null)
        {
            return;
        }

        if (IsLocked)
        {
            if (Keyboard.current[lockpickKey] != null && Keyboard.current[lockpickKey].wasPressedThisFrame)
            {
                TryStartLockpick(nearbyLocalPlayer);
            }

            return;
        }

        if (Keyboard.current[interactionKey] == null || !Keyboard.current[interactionKey].wasPressedThisFrame)
        {
            return;
        }

        TryOpenChest(nearbyLocalPlayer);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerMovement player = ResolvePlayer(other);
        if (player == null || !player.IsOwner || IsOpened)
        {
            return;
        }

        nearbyLocalPlayer = player;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        PlayerMovement player = ResolvePlayer(other);
        if (player != null && player == nearbyLocalPlayer)
        {
            if (localLockpickMiniGameActive)
            {
                CancelLockpickAttempt();
            }

            nearbyLocalPlayer = null;
        }
    }

    private void OnGUI()
    {
        if (!ShouldShowPrompt() && !localLockpickMiniGameActive)
        {
            return;
        }

        Camera worldCamera = Camera.main;
        if (worldCamera == null)
        {
            return;
        }

        Vector3 screenPosition = worldCamera.WorldToScreenPoint(transform.position + promptWorldOffset);
        if (screenPosition.z <= 0f)
        {
            return;
        }

        EnsurePromptStyles();

        if (localLockpickMiniGameActive)
        {
            Vector3 lockpickScreenPosition = worldCamera.WorldToScreenPoint(transform.position + lockpickUiOffset);
            if (lockpickScreenPosition.z > 0f)
            {
                DrawLockpickMiniGame(lockpickScreenPosition);
            }
            return;
        }

        string promptText = BuildPromptText();
        Vector2 textSize = promptLabelStyle.CalcSize(new GUIContent(promptText));
        Rect boxRect = new Rect(
            screenPosition.x - (textSize.x * 0.5f) - 12f,
            Screen.height - screenPosition.y - textSize.y - 16f,
            textSize.x + 24f,
            textSize.y + 12f);
        GUI.Box(boxRect, GUIContent.none, promptBoxStyle);
        GUI.Label(boxRect, promptText, promptLabelStyle);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestBeginLockpickServerRpc(NetworkObjectReference playerReference, RpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;
        bool granted = false;

        if (!IsOpened
            && !IsChestInUse
            && playerReference.TryGet(out NetworkObject playerObject))
        {
            PlayerMovement player = playerObject.GetComponent<PlayerMovement>();
            if (player != null
                && player.OwnerClientId == senderClientId
                && CanPlayerStartLockpick(player)
                && IsPlayerWithinInteractionDistance(player))
            {
                networkActiveLockpickerClientId.Value = senderClientId;
                granted = true;
            }
        }

        ConfirmBeginLockpickClientRpc(granted, new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new[] { senderClientId },
            },
        });
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestOpenChestServerRpc(NetworkObjectReference playerReference, RpcParams rpcParams = default)
    {
        if (IsOpened || !playerReference.TryGet(out NetworkObject playerObject))
        {
            return;
        }

        ulong senderClientId = rpcParams.Receive.SenderClientId;
        PlayerMovement player = playerObject.GetComponent<PlayerMovement>();
        if (player == null || player.OwnerClientId != senderClientId)
        {
            return;
        }

        if (!CanPlayerOpen(player) || !IsPlayerWithinInteractionDistance(player))
        {
            return;
        }

        OpenChest();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestCancelLockpickServerRpc(NetworkObjectReference playerReference, RpcParams rpcParams = default)
    {
        if (networkActiveLockpickerClientId.Value != rpcParams.Receive.SenderClientId)
        {
            return;
        }

        if (!playerReference.TryGet(out _))
        {
            return;
        }

        ReleaseActiveLockpicker();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestOpenChestAfterLockpickSuccessServerRpc(NetworkObjectReference playerReference, RpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;
        if (IsOpened || networkActiveLockpickerClientId.Value != senderClientId || !playerReference.TryGet(out NetworkObject playerObject))
        {
            return;
        }

        PlayerMovement player = playerObject.GetComponent<PlayerMovement>();
        if (player == null || !CanPlayerStartLockpick(player) || !IsPlayerWithinInteractionDistance(player))
        {
            ReleaseActiveLockpicker();
            return;
        }

        OpenChest();
    }

    [ClientRpc]
    private void ConfirmBeginLockpickClientRpc(bool granted, ClientRpcParams clientRpcParams = default)
    {
        isWaitingForLockpickReservation = false;
        if (!granted)
        {
            return;
        }

        StartLocalLockpickSession();
    }

    private void TryOpenChest(PlayerMovement player)
    {
        if (!CanPlayerOpen(player))
        {
            return;
        }

        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            if (IsServer)
            {
                if (IsPlayerWithinInteractionDistance(player))
                {
                    OpenChest();
                }
            }
            else if (player.NetworkObject != null && player.NetworkObject.IsSpawned)
            {
                RequestOpenChestServerRpc(player.NetworkObject);
            }

            return;
        }

        OpenChest();
    }

    private void TryStartLockpick(PlayerMovement player)
    {
        if (!CanPlayerStartLockpick(player) || isWaitingForLockpickReservation || localLockpickMiniGameActive)
        {
            return;
        }

        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            if (IsChestInUse)
            {
                return;
            }

            if (IsServer)
            {
                networkActiveLockpickerClientId.Value = player.OwnerClientId;
                StartLocalLockpickSession();
            }
            else if (player.NetworkObject != null && player.NetworkObject.IsSpawned)
            {
                isWaitingForLockpickReservation = true;
                RequestBeginLockpickServerRpc(player.NetworkObject);
            }

            return;
        }

        localLockpickReservationActive = true;
        StartLocalLockpickSession();
    }

    private void OpenChest()
    {
        if (IsOpened)
        {
            return;
        }

        ReleaseActiveLockpicker();
        EndLocalLockpickSession(releaseReservation: false);
        nearbyLocalPlayer = null;

        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            networkIsOpened.Value = true;
        }
        else
        {
            localIsOpened = true;
            ApplyOpenedPresentation();
        }

        if (lootReleaseRoutine != null)
        {
            StopCoroutine(lootReleaseRoutine);
        }

        lootReleaseRoutine = StartCoroutine(ReleaseLootRoutine());
    }

    private void OnOpenedStateChanged(bool previousValue, bool newValue)
    {
        if (newValue)
        {
            ApplyOpenedPresentation();
            nearbyLocalPlayer = null;
            EndLocalLockpickSession(releaseReservation: false);
        }
    }

    private void OnActiveLockpickerChanged(ulong previousValue, ulong newValue)
    {
        if (newValue == ulong.MaxValue)
        {
            isWaitingForLockpickReservation = false;
            if (localLockpickMiniGameActive && !IsOpened)
            {
                EndLocalLockpickSession(releaseReservation: false);
            }
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (IsServer && networkActiveLockpickerClientId.Value == clientId)
        {
            ReleaseActiveLockpicker();
        }
    }

    private void ApplyOpenedPresentation()
    {
        if (openPresentationApplied)
        {
            return;
        }

        openPresentationApplied = true;
        PlayOpenAnimation();
    }

    private IEnumerator ReleaseLootRoutine()
    {
        int validEntryCount = CountValidLootEntries();
        int emittedEntryIndex = 0;

        for (int entryIndex = 0; entryIndex < lootEntries.Length; entryIndex++)
        {
            ChestLootEntry lootEntry = lootEntries[entryIndex];
            if (lootEntry == null || lootEntry.collectiblePrefab == null || lootEntry.stackAmount <= 0)
            {
                continue;
            }

            Vector3 launchPosition = ResolveLootLaunchOrigin();
            Vector3 landingPosition = ResolveLootLandingCenter() + CalculateLootSpreadOffset(emittedEntryIndex, validEntryCount, lootEntry.horizontalJitter, lootEntry.verticalOffset);
            emittedEntryIndex++;

            CollectibleItem spawnedCollectible = Instantiate(lootEntry.collectiblePrefab, launchPosition, lootEntry.collectiblePrefab.transform.rotation);
            spawnedCollectible.SetStackAmount(lootEntry.stackAmount);

            if (NetworkObject != null && NetworkObject.IsSpawned && IsServer)
            {
                NetworkObject collectibleNetworkObject = spawnedCollectible.GetComponent<NetworkObject>();
                if (collectibleNetworkObject != null && !collectibleNetworkObject.IsSpawned)
                {
                    collectibleNetworkObject.Spawn();
                }
                else if (collectibleNetworkObject == null)
                {
                    Debug.LogWarning($"Chest '{name}' spawned loot '{spawnedCollectible.name}' without a NetworkObject. Remote clients will not see this loot in multiplayer.");
                }
            }

            spawnedCollectible.LaunchFromChest(launchPosition, landingPosition, lootLaunchDuration, lootLaunchArcHeight);

            if (lootReleaseStaggerSeconds > 0f)
            {
                yield return new WaitForSeconds(lootReleaseStaggerSeconds);
            }
        }

        lootReleaseRoutine = null;
    }

    private void PlayOpenAnimation()
    {
        if (chestAnimator == null)
        {
            return;
        }

        if (setOpenBool && HasAnimatorParameter(openBoolParameter, AnimatorControllerParameterType.Bool))
        {
            chestAnimator.SetBool(openBoolParameter, true);
        }

        if (setOpenTrigger && HasAnimatorParameter(openTriggerParameter, AnimatorControllerParameterType.Trigger))
        {
            chestAnimator.SetTrigger(openTriggerParameter);
        }
    }

    private bool HasAnimatorParameter(string parameterName, AnimatorControllerParameterType parameterType)
    {
        if (chestAnimator == null || string.IsNullOrWhiteSpace(parameterName))
        {
            return false;
        }

        foreach (AnimatorControllerParameter parameter in chestAnimator.parameters)
        {
            if (parameter.type == parameterType && string.Equals(parameter.name, parameterName, System.StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private bool CanPlayerOpen(PlayerMovement player)
    {
        if (player == null || player.IsDead || IsOpened || IsLocked || IsChestInUse)
        {
            return false;
        }

        return true;
    }

    private bool CanPlayerStartLockpick(PlayerMovement player)
    {
        if (player == null || player.IsDead || IsOpened || !IsLocked || !player.HasLockpickRelic)
        {
            return false;
        }

        if (IsChestInUse && !IsLockpickReservedBy(player))
        {
            return false;
        }

        return true;
    }

    private bool IsLockpickReservedBy(PlayerMovement player)
    {
        if (player == null)
        {
            return false;
        }

        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            return networkActiveLockpickerClientId.Value == player.OwnerClientId;
        }

        return localLockpickReservationActive;
    }

    private bool IsPlayerWithinInteractionDistance(PlayerMovement player)
    {
        if (player == null)
        {
            return false;
        }

        Collider2D playerCollider = player.GetComponent<Collider2D>();
        if (chestCollider != null && playerCollider != null)
        {
            if (chestCollider.IsTouching(playerCollider))
            {
                return true;
            }

            Vector2 playerPoint = playerCollider.bounds.ClosestPoint(chestCollider.bounds.center);
            Vector2 chestPoint = chestCollider.bounds.ClosestPoint(playerCollider.bounds.center);
            return (playerPoint - chestPoint).sqrMagnitude <= interactionDistanceTolerance * interactionDistanceTolerance;
        }

        return (player.transform.position - transform.position).sqrMagnitude <= interactionDistanceTolerance * interactionDistanceTolerance;
    }

    private int CountValidLootEntries()
    {
        int count = 0;
        if (lootEntries == null)
        {
            return 0;
        }

        for (int index = 0; index < lootEntries.Length; index++)
        {
            ChestLootEntry lootEntry = lootEntries[index];
            if (lootEntry != null && lootEntry.collectiblePrefab != null && lootEntry.stackAmount > 0)
            {
                count++;
            }
        }

        return count;
    }

    private Vector3 ResolveLootLaunchOrigin()
    {
        return lootLaunchOrigin != null ? lootLaunchOrigin.position : transform.position + lootLaunchOriginOffset;
    }

    private Vector3 ResolveLootLandingCenter()
    {
        return lootLandingCenter != null ? lootLandingCenter.position : transform.position + lootLandingCenterOffset;
    }

    private Vector3 CalculateLootSpreadOffset(int emittedEntryIndex, int validEntryCount, float horizontalJitter, float verticalOffset)
    {
        float normalizedIndex = validEntryCount <= 1 ? 0.5f : (float)emittedEntryIndex / (validEntryCount - 1);
        float centeredOffset = Mathf.Lerp(-lootSpreadWidth * 0.5f, lootSpreadWidth * 0.5f, normalizedIndex);
        float jitter = horizontalJitter > 0f ? Random.Range(-horizontalJitter, horizontalJitter) : 0f;
        return new Vector3(centeredOffset + jitter, verticalOffset, 0f);
    }

    private PlayerMovement ResolvePlayer(Collider2D other)
    {
        if (other == null)
        {
            return null;
        }

        PlayerMovement player = other.GetComponent<PlayerMovement>();
        if (player != null)
        {
            return player;
        }

        return other.GetComponentInParent<PlayerMovement>();
    }

    private bool ShouldShowPrompt()
    {
        return nearbyLocalPlayer != null && !IsOpened;
    }

    private string BuildPromptText()
    {
        if (localLockpickMiniGameActive)
        {
            return $"Press {lockpickKey} when the black marker is inside the green zone";
        }

        if (IsLocked)
        {
            if (IsChestInUse && !IsLockpickReservedBy(nearbyLocalPlayer))
            {
                return "Chest is being lockpicked";
            }

            return nearbyLocalPlayer != null && nearbyLocalPlayer.HasLockpickRelic
                ? $"Press {lockpickKey} to lockpick chest"
                : "Locked chest requires Lockpick Relic";
        }

        return $"Press {interactionKey} to open chest";
    }

    private void StartLocalLockpickSession()
    {
        localLockpickMiniGameActive = true;
        localLockpickGreenZoneCenterNormalized = Random.Range(lockpickGreenZoneMinCenterNormalized, lockpickGreenZoneMaxCenterNormalized);
        localLockpickSweepNormalized = 0f;
        localLockpickSweepDirection = 1f;
    }

    private void EndLocalLockpickSession(bool releaseReservation)
    {
        localLockpickMiniGameActive = false;
        isWaitingForLockpickReservation = false;

        if (!releaseReservation)
        {
            return;
        }

        ReleaseActiveLockpicker();
        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            if (nearbyLocalPlayer != null && nearbyLocalPlayer.NetworkObject != null && nearbyLocalPlayer.NetworkObject.IsSpawned)
            {
                RequestCancelLockpickServerRpc(nearbyLocalPlayer.NetworkObject);
            }
        }
        else
        {
            localLockpickReservationActive = false;
        }
    }

    private void CancelLockpickAttempt()
    {
        EndLocalLockpickSession(releaseReservation: true);
    }

    private void ResolveLocalLockpickAttempt()
    {
        float halfWidth = Mathf.Clamp01(lockpickGreenZoneWidthNormalized) * 0.5f;
        bool isSuccess = Mathf.Abs(localLockpickSweepNormalized - localLockpickGreenZoneCenterNormalized) <= halfWidth;
        if (!isSuccess)
        {
            lockpickFailureShakeEndTime = Time.unscaledTime + Mathf.Max(0.01f, lockpickFailureShakeDuration);
        }

        EndLocalLockpickSession(releaseReservation: !isSuccess);

        if (!isSuccess)
        {
            return;
        }

        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            if (nearbyLocalPlayer != null && nearbyLocalPlayer.NetworkObject != null && nearbyLocalPlayer.NetworkObject.IsSpawned)
            {
                RequestOpenChestAfterLockpickSuccessServerRpc(nearbyLocalPlayer.NetworkObject);
            }

            return;
        }

        OpenChest();
    }

    private void UpdateLockpickSweepAnimation()
    {
        float speed = Mathf.Max(0.15f, lockpickSweepSpeedNormalized);
        localLockpickSweepNormalized += localLockpickSweepDirection * speed * Time.unscaledDeltaTime;

        if (localLockpickSweepNormalized >= 1f)
        {
            localLockpickSweepNormalized = 1f;
            localLockpickSweepDirection = -1f;
        }
        else if (localLockpickSweepNormalized <= 0f)
        {
            localLockpickSweepNormalized = 0f;
            localLockpickSweepDirection = 1f;
        }
    }

    private void DrawLockpickMiniGame(Vector3 promptScreenPosition)
    {
        if (Time.unscaledTime < lockpickFailureShakeEndTime)
        {
            float shakeTimeRemaining = lockpickFailureShakeEndTime - Time.unscaledTime;
            float normalizedShake = Mathf.Clamp01(shakeTimeRemaining / Mathf.Max(0.01f, lockpickFailureShakeDuration));
            float shakeOffsetX = Mathf.Sin(Time.unscaledTime * Mathf.Max(1f, lockpickFailureShakeFrequency)) * lockpickFailureShakeDistance * normalizedShake;
            promptScreenPosition.x += shakeOffsetX;
        }

        Rect barRect = new Rect(
            promptScreenPosition.x - (lockpickBarSize.x * 0.5f),
            Screen.height - promptScreenPosition.y - lockpickBarSize.y - 12f,
            lockpickBarSize.x,
            lockpickBarSize.y);

        Color previousColor = GUI.color;
        GUI.color = new Color(0.74f, 0.16f, 0.16f, 0.95f);
        GUI.DrawTexture(barRect, Texture2D.whiteTexture);

        float greenWidth = Mathf.Clamp01(lockpickGreenZoneWidthNormalized) * barRect.width;
        float greenCenter = Mathf.Lerp(barRect.xMin, barRect.xMax, localLockpickGreenZoneCenterNormalized);
        Rect greenRect = new Rect(greenCenter - (greenWidth * 0.5f), barRect.y, greenWidth, barRect.height);
        GUI.color = new Color(0.18f, 0.74f, 0.24f, 0.98f);
        GUI.DrawTexture(greenRect, Texture2D.whiteTexture);

        float indicatorCenter = Mathf.Lerp(barRect.xMin, barRect.xMax, localLockpickSweepNormalized);
        Rect indicatorRect = new Rect(
            indicatorCenter - (lockpickIndicatorWidth * 0.5f),
            barRect.center.y - (lockpickIndicatorHeight * 0.5f),
            lockpickIndicatorWidth,
            lockpickIndicatorHeight);
        GUI.color = Color.black;
        GUI.DrawTexture(indicatorRect, Texture2D.whiteTexture);

        GUI.color = previousColor;

        string instructionText = $"Press {lockpickKey} when the marker overlaps green";
        Rect instructionRect = new Rect(barRect.x - 30f, barRect.y - 30f, barRect.width + 60f, 22f);
        GUI.Label(instructionRect, instructionText, lockpickInstructionStyle);
    }

    private void ReleaseActiveLockpicker()
    {
        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            if (IsServer)
            {
                networkActiveLockpickerClientId.Value = ulong.MaxValue;
            }

            return;
        }

        localLockpickReservationActive = false;
    }

    private void EnsurePromptStyles()
    {
        if (promptLabelStyle == null)
        {
            promptLabelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                normal =
                {
                    textColor = Color.white,
                },
            };
        }

        if (lockpickInstructionStyle == null)
        {
            lockpickInstructionStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal =
                {
                    textColor = new Color(0.97f, 0.97f, 0.97f, 1f),
                },
            };
        }

        if (promptBoxStyle == null)
        {
            Texture2D background = new Texture2D(1, 1);
            background.SetPixel(0, 0, new Color(0.08f, 0.1f, 0.12f, 0.84f));
            background.Apply();

            promptBoxStyle = new GUIStyle(GUI.skin.box)
            {
                normal =
                {
                    background = background,
                },
                border = new RectOffset(8, 8, 8, 8),
            };
        }
    }
}
