using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider2D))]
public class CollectibleItem : NetworkBehaviour
{
    private enum CollectionMode
    {
        Auto,
        RequireInteract,
    }

    private enum CollectibleType
    {
        Health,
        SilverPennies,
        Buff,
        LockpickRelic,
        BottledSoul,
    }

    private enum BuffCollectibleApplicationMode
    {
        UseBuffPayload,
        UseAegisShieldPercent,
    }

    [Header("Collection")]
    [SerializeField] private CollectionMode collectionMode = CollectionMode.Auto;
    [SerializeField] private CollectibleType collectibleType = CollectibleType.SilverPennies;
    [SerializeField] private int amount = 1;
    [SerializeField] private bool allowStacking = true;
    [SerializeField] private float collectionDistanceTolerance = 0.75f;
    [SerializeField] private Key interactionKey = Key.E;
    [SerializeField] private Vector3 promptWorldOffset = new Vector3(0f, 1.15f, 0f);

    [Header("Stacking")]
    [SerializeField] private float stackMergeCheckInterval = 0.15f;
    [SerializeField] private float stackMergeSearchPadding = 0.05f;
    [SerializeField] private bool showStackAmountLabel = true;
    [SerializeField] private Vector3 stackAmountWorldOffset = new Vector3(0f, 0.6f, 0f);

    [Header("Chest Launch")]
    [SerializeField] private float defaultChestLaunchDuration = 0.45f;
    [SerializeField] private float defaultChestLaunchArcHeight = 0.85f;

    [Header("Buff Payload")]
    [SerializeField] private PlayerMovement.BuffType buffType = PlayerMovement.BuffType.Strength;
    [SerializeField] private BuffCollectibleApplicationMode buffApplicationMode = BuffCollectibleApplicationMode.UseBuffPayload;
    [SerializeField] private float buffValue = 10f;
    [SerializeField] private float buffDuration = 15f;
    [SerializeField] private string buffDescription = string.Empty;
    [SerializeField] private float aegisShieldPercent = 10f;

    [Header("Bobbing")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private float bobHeight = 0.18f;
    [SerializeField] private float bobSpeed = 2.4f;
    [SerializeField] private bool randomizeInitialPhase = true;
    [SerializeField] private float bobPhaseOffset;

    private Collider2D triggerCollider;
    private PlayerMovement nearbyLocalPlayer;
    private Vector3 visualRootBaseLocalPosition;
    private bool hasVisualRootBaseLocalPosition;
    private bool isCollected;
    private GUIStyle promptLabelStyle;
    private GUIStyle promptBoxStyle;
    private GUIStyle stackLabelStyle;
    private float nextStackMergeCheckTime;
    private bool isLaunchingFromChest;
    private float chestLaunchStartedAt;
    private float chestLaunchDuration;
    private float chestLaunchArcHeight;
    private Vector3 chestLaunchStartPosition;
    private Vector3 chestLaunchTargetPosition;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider2D>();
        ResolveVisualRoot();

        if (randomizeInitialPhase)
        {
            bobPhaseOffset = Random.Range(0f, Mathf.PI * 2f);
        }
    }

    private void OnValidate()
    {
        if (collectibleType != CollectibleType.Buff)
        {
            return;
        }

        if (buffType == PlayerMovement.BuffType.Attack)
        {
            buffType = PlayerMovement.BuffType.Strength;
            Debug.LogWarning($"CollectibleItem '{name}' converted legacy Attack buff selection to Strength so only one melee damage buff remains.", this);
        }

        if (buffType == PlayerMovement.BuffType.Aegis)
        {
            buffApplicationMode = BuffCollectibleApplicationMode.UseAegisShieldPercent;
        }
        else if (buffApplicationMode == BuffCollectibleApplicationMode.UseAegisShieldPercent && buffType != PlayerMovement.BuffType.Aegis)
        {
            buffApplicationMode = BuffCollectibleApplicationMode.UseBuffPayload;
        }

        if (buffApplicationMode == BuffCollectibleApplicationMode.UseBuffPayload)
        {
            buffValue = Mathf.Max(0f, buffValue);
            buffDuration = Mathf.Max(0f, buffDuration);
        }
        else
        {
            aegisShieldPercent = Mathf.Max(0f, aegisShieldPercent);
        }
    }

    private void OnDisable()
    {
        ResetVisualOffset();
        nearbyLocalPlayer = null;
        isLaunchingFromChest = false;
    }

    private void Update()
    {
        UpdateChestLaunchAnimation();
        UpdateBobbingAnimation();

        if (ShouldRunStackMerging())
        {
            TryMergeWithTouchingCollectibles();
        }

        if (isCollected || nearbyLocalPlayer == null)
        {
            return;
        }

        if (collectionMode == CollectionMode.Auto)
        {
            TryCollect(nearbyLocalPlayer);
            return;
        }

        if (Keyboard.current != null && Keyboard.current[interactionKey] != null && Keyboard.current[interactionKey].wasPressedThisFrame)
        {
            TryCollect(nearbyLocalPlayer);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerMovement player = ResolvePlayer(other);
        if (player == null || !player.IsOwner)
        {
            return;
        }

        nearbyLocalPlayer = player;
        if (collectionMode == CollectionMode.Auto)
        {
            TryCollect(player);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        PlayerMovement player = ResolvePlayer(other);
        if (player != null && player == nearbyLocalPlayer)
        {
            nearbyLocalPlayer = null;
        }
    }

    private void OnGUI()
    {
        if (isCollected)
        {
            return;
        }

        Camera worldCamera = Camera.main;
        if (worldCamera == null)
        {
            return;
        }

        Vector3 worldPosition = (visualRoot != null ? visualRoot.position : transform.position) + promptWorldOffset;
        Vector3 screenPosition = worldCamera.WorldToScreenPoint(worldPosition);
        if (screenPosition.z <= 0f)
        {
            return;
        }

        EnsurePromptStyles();

        if (showStackAmountLabel && amount > 1)
        {
            DrawStackAmountLabel(worldCamera);
        }

        if (!ShouldShowInteractionPrompt())
        {
            return;
        }

        string promptText = $"Press {interactionKey} to collect";
        Vector2 textSize = promptLabelStyle.CalcSize(new GUIContent(promptText));
        Rect boxRect = new Rect(
            screenPosition.x - (textSize.x * 0.5f) - 10f,
            Screen.height - screenPosition.y - textSize.y - 16f,
            textSize.x + 20f,
            textSize.y + 10f);
        GUI.Box(boxRect, GUIContent.none, promptBoxStyle);
        GUI.Label(boxRect, promptText, promptLabelStyle);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestCollectServerRpc(NetworkObjectReference playerReference, RpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;

        if (isCollected || !playerReference.TryGet(out NetworkObject playerObject))
        {
            return;
        }

        PlayerMovement player = playerObject.GetComponent<PlayerMovement>();
        if (player == null || player.OwnerClientId != senderClientId)
        {
            return;
        }

        if (!CanCollect(player) || !IsPlayerWithinCollectionDistance(player))
        {
            return;
        }

        ApplyCollection(player);
    }

    private void TryCollect(PlayerMovement player)
    {
        if (!CanCollect(player))
        {
            return;
        }

        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            if (IsServer)
            {
                if (IsPlayerWithinCollectionDistance(player))
                {
                    ApplyCollection(player);
                }
            }
            else if (player.NetworkObject != null && player.NetworkObject.IsSpawned)
            {
                RequestCollectServerRpc(player.NetworkObject);
            }

            return;
        }

        ApplyCollection(player);
    }

    private bool CanCollect(PlayerMovement player)
    {
        if (isCollected || isLaunchingFromChest || player == null || player.IsDead || amount <= 0)
        {
            return false;
        }

        switch (collectibleType)
        {
            case CollectibleType.Health:
                return player.CanCollectHealth(amount);
            case CollectibleType.SilverPennies:
                return true;
            case CollectibleType.Buff:
                return CanCollectBuff(player);
            case CollectibleType.LockpickRelic:
                return !player.HasLockpickRelic;
            case CollectibleType.BottledSoul:
                return !player.HasBottledSoul;
            default:
                return false;
        }
    }

    private void ApplyCollection(PlayerMovement player)
    {
        if (isCollected)
        {
            return;
        }

        switch (collectibleType)
        {
            case CollectibleType.Health:
                player.HealFromCollectible(amount);
                break;
            case CollectibleType.SilverPennies:
                player.CollectSilverPennies(amount);
                break;
            case CollectibleType.Buff:
                ApplyBuffCollection(player);
                break;
            case CollectibleType.LockpickRelic:
                player.AcquireLockpickRelic();
                break;
            case CollectibleType.BottledSoul:
                player.AcquireBottledSoul();
                break;
        }

        isCollected = true;
        nearbyLocalPlayer = null;

        if (NetworkObject != null && NetworkObject.IsSpawned && IsServer)
        {
            NetworkObject.Despawn(true);
            return;
        }

        Destroy(gameObject);
    }

    private bool IsPlayerWithinCollectionDistance(PlayerMovement player)
    {
        if (player == null)
        {
            return false;
        }

        Collider2D playerCollider = player.GetComponent<Collider2D>();
        if (triggerCollider != null && playerCollider != null)
        {
            if (triggerCollider.IsTouching(playerCollider))
            {
                return true;
            }

            Vector2 playerPoint = playerCollider.bounds.ClosestPoint(triggerCollider.bounds.center);
            Vector2 itemPoint = triggerCollider.bounds.ClosestPoint(playerCollider.bounds.center);
            return (playerPoint - itemPoint).sqrMagnitude <= collectionDistanceTolerance * collectionDistanceTolerance;
        }

        return (player.transform.position - transform.position).sqrMagnitude <= collectionDistanceTolerance * collectionDistanceTolerance;
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

    private bool CanCollectBuff(PlayerMovement player)
    {
        if (player == null)
        {
            return false;
        }

        if (buffApplicationMode == BuffCollectibleApplicationMode.UseAegisShieldPercent)
        {
            return aegisShieldPercent > 0f;
        }

        return buffValue > 0f && buffDuration > 0f;
    }

    private void ApplyBuffCollection(PlayerMovement player)
    {
        if (player == null)
        {
            return;
        }

        if (buffApplicationMode == BuffCollectibleApplicationMode.UseAegisShieldPercent || buffType == PlayerMovement.BuffType.Aegis)
        {
            player.ApplyAegisShieldBuff(aegisShieldPercent);
            return;
        }

        player.ApplyBuff(buffType, buffValue, buffDuration, buffDescription);
    }

    private bool ShouldShowInteractionPrompt()
    {
        return collectionMode == CollectionMode.RequireInteract
            && nearbyLocalPlayer != null
            && !isCollected
            && !isLaunchingFromChest
            && CanCollect(nearbyLocalPlayer);
    }

    private void UpdateBobbingAnimation()
    {
        if (isLaunchingFromChest)
        {
            return;
        }

        Transform targetVisualRoot = ResolveVisualRoot();
        if (targetVisualRoot == null)
        {
            return;
        }

        if (!hasVisualRootBaseLocalPosition)
        {
            visualRootBaseLocalPosition = targetVisualRoot.localPosition;
            hasVisualRootBaseLocalPosition = true;
        }

        // Avoid shifting the authoritative network root on the server. Assign a child visual root on networked pickups when possible.
        if (targetVisualRoot == transform && NetworkObject != null && NetworkObject.IsSpawned && IsServer)
        {
            targetVisualRoot.localPosition = visualRootBaseLocalPosition;
            return;
        }

        float bobOffset = Mathf.Sin(Time.unscaledTime * bobSpeed + bobPhaseOffset) * bobHeight;
        targetVisualRoot.localPosition = visualRootBaseLocalPosition + Vector3.up * bobOffset;
    }

    private Transform ResolveVisualRoot()
    {
        if (visualRoot != null)
        {
            return visualRoot;
        }

        if (transform.childCount > 0)
        {
            visualRoot = transform.GetChild(0);
        }
        else
        {
            visualRoot = transform;
        }

        return visualRoot;
    }

    private void ResetVisualOffset()
    {
        if (visualRoot != null && hasVisualRootBaseLocalPosition)
        {
            visualRoot.localPosition = visualRootBaseLocalPosition;
        }
    }

    public void SetStackAmount(int stackAmount)
    {
        amount = Mathf.Max(1, stackAmount);
    }

    public void LaunchFromChest(Vector3 startPosition, Vector3 targetPosition, float duration = -1f, float arcHeight = -1f)
    {
        float resolvedDuration = duration > 0f ? duration : defaultChestLaunchDuration;
        float resolvedArcHeight = arcHeight >= 0f ? arcHeight : defaultChestLaunchArcHeight;

        ApplyChestLaunchLocally(startPosition, targetPosition, resolvedDuration, resolvedArcHeight);

        if (NetworkObject != null && NetworkObject.IsSpawned && IsServer)
        {
            LaunchFromChestClientRpc(startPosition, targetPosition, resolvedDuration, resolvedArcHeight);
        }
    }

    [ClientRpc]
    private void LaunchFromChestClientRpc(Vector3 startPosition, Vector3 targetPosition, float duration, float arcHeight)
    {
        if (IsServer)
        {
            return;
        }

        ApplyChestLaunchLocally(startPosition, targetPosition, duration, arcHeight);
    }

    private void ApplyChestLaunchLocally(Vector3 startPosition, Vector3 targetPosition, float duration, float arcHeight)
    {
        isLaunchingFromChest = true;
        chestLaunchStartedAt = Time.unscaledTime;
        chestLaunchDuration = Mathf.Max(0.05f, duration);
        chestLaunchArcHeight = Mathf.Max(0f, arcHeight);
        chestLaunchStartPosition = startPosition;
        chestLaunchTargetPosition = targetPosition;
        transform.position = startPosition;
        nearbyLocalPlayer = null;
    }

    private void UpdateChestLaunchAnimation()
    {
        if (!isLaunchingFromChest)
        {
            return;
        }

        float elapsed = Time.unscaledTime - chestLaunchStartedAt;
        float progress = Mathf.Clamp01(elapsed / chestLaunchDuration);
        float smoothedProgress = Mathf.SmoothStep(0f, 1f, progress);
        Vector3 basePosition = Vector3.Lerp(chestLaunchStartPosition, chestLaunchTargetPosition, smoothedProgress);
        float arcOffset = Mathf.Sin(progress * Mathf.PI) * chestLaunchArcHeight;
        transform.position = basePosition + Vector3.up * arcOffset;

        if (progress >= 1f)
        {
            transform.position = chestLaunchTargetPosition;
            isLaunchingFromChest = false;
            nextStackMergeCheckTime = Time.unscaledTime + 0.05f;
        }
    }

    private bool ShouldRunStackMerging()
    {
        if (!allowStacking || amount <= 0 || isCollected || isLaunchingFromChest)
        {
            return false;
        }

        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            if (!IsServer)
            {
                return false;
            }
        }

        return Time.unscaledTime >= nextStackMergeCheckTime;
    }

    private void TryMergeWithTouchingCollectibles()
    {
        nextStackMergeCheckTime = Time.unscaledTime + stackMergeCheckInterval;
        if (triggerCollider == null)
        {
            return;
        }

        Bounds bounds = triggerCollider.bounds;
        Vector2 size = bounds.size + Vector3.one * stackMergeSearchPadding;
        Collider2D[] overlaps = Physics2D.OverlapBoxAll(bounds.center, size, 0f);
        foreach (Collider2D overlap in overlaps)
        {
            CollectibleItem other = overlap != null ? overlap.GetComponent<CollectibleItem>() : null;
            if (!CanMergeWith(other))
            {
                continue;
            }

            if (!HasAbsorbPriorityOver(other))
            {
                continue;
            }

            AbsorbCollectibleStack(other);
        }
    }

    private bool CanMergeWith(CollectibleItem other)
    {
        return other != null
            && other != this
            && !other.isCollected
            && !other.isLaunchingFromChest
            && allowStacking
            && other.allowStacking
            && collectibleType == other.collectibleType
            && collectionMode == other.collectionMode;
    }

    private bool HasAbsorbPriorityOver(CollectibleItem other)
    {
        if (NetworkObject != null && NetworkObject.IsSpawned && other.NetworkObject != null && other.NetworkObject.IsSpawned)
        {
            return NetworkObjectId < other.NetworkObjectId;
        }

        return GetInstanceID() < other.GetInstanceID();
    }

    private void AbsorbCollectibleStack(CollectibleItem other)
    {
        amount = Mathf.Max(1, amount + Mathf.Max(1, other.amount));
        other.isCollected = true;
        other.nearbyLocalPlayer = null;

        if (other.NetworkObject != null && other.NetworkObject.IsSpawned && IsServer)
        {
            other.NetworkObject.Despawn(true);
            return;
        }

        Destroy(other.gameObject);
    }

    private void DrawStackAmountLabel(Camera worldCamera)
    {
        Vector3 labelWorldPosition = (visualRoot != null ? visualRoot.position : transform.position) + stackAmountWorldOffset;
        Vector3 labelScreenPosition = worldCamera.WorldToScreenPoint(labelWorldPosition);
        if (labelScreenPosition.z <= 0f)
        {
            return;
        }

        string amountLabel = $"x{amount}";
        Vector2 labelSize = stackLabelStyle.CalcSize(new GUIContent(amountLabel));
        Rect labelRect = new Rect(
            labelScreenPosition.x - (labelSize.x * 0.5f) - 8f,
            Screen.height - labelScreenPosition.y - labelSize.y - 6f,
            labelSize.x + 16f,
            labelSize.y + 6f);
        GUI.Box(labelRect, GUIContent.none, promptBoxStyle);
        GUI.Label(labelRect, amountLabel, stackLabelStyle);
    }

    private void EnsurePromptStyles()
    {
        if (promptLabelStyle == null)
        {
            promptLabelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 15,
                normal =
                {
                    textColor = Color.white,
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

        if (stackLabelStyle == null)
        {
            stackLabelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal =
                {
                    textColor = new Color(0.98f, 0.94f, 0.72f, 1f),
                },
            };
        }
    }
}