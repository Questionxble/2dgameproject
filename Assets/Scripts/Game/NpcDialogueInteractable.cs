using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public enum NpcDialoguePortraitSide
{
    Left,
    Right
}

[Serializable]
public class NpcDialogueCollectibleDrop
{
    public CollectibleItem collectiblePrefab;
    public int stackAmount = 1;
    public Vector3 localOffset;
}

[Serializable]
public class NpcDialogueChoice
{
    public string choiceText;
    public string nextNodeId;
}

[Serializable]
public class NpcDialogueNode
{
    public string nodeId = "start";
    public string speakerName;
    public Sprite portraitSprite;
    public NpcDialoguePortraitSide portraitSide = NpcDialoguePortraitSide.Left;
    public string[] lines = new string[0];
    public string nextNodeId;
    public bool endConversationAfterNode;
    public NpcDialogueChoice[] choices = new NpcDialogueChoice[0];
    public int silverPenniesReward;
    public NpcDialogueCollectibleDrop[] collectibleDrops = new NpcDialogueCollectibleDrop[0];
    public bool rewardOnlyOnce = true;
    public string futureStartNodeId;
}

[Serializable]
public class NpcDialogueProgressSaveEntry
{
    public string npcId;
    public string startNodeIdOverride;
    public string[] completedRewardNodeIds = new string[0];
}

public class NpcDialogueInteractable : MonoBehaviour
{
    private const string DefaultStartNodeId = "start";
    private static readonly List<NpcDialogueInteractable> registeredInteractables = new List<NpcDialogueInteractable>();

    public static event Action LocalProgressionChanged;

    [Header("Conversation")]
    [SerializeField] private string npcDisplayName = "NPC";
    [SerializeField] private float interactionRadius = 2.75f;
    [SerializeField] private string startNodeId = DefaultStartNodeId;
    [SerializeField] private string progressionSaveKey;
    [SerializeField] private Sprite defaultPortraitSprite;
    [SerializeField] private NpcDialoguePortraitSide defaultPortraitSide = NpcDialoguePortraitSide.Left;
    [SerializeField] private NpcDialogueNode[] dialogueNodes = new NpcDialogueNode[0];

    [Header("Dialogue Rewards")]
    [SerializeField] private Transform rewardDropOrigin;
    [SerializeField] private Vector3 rewardDropOffset = new Vector3(0f, 0.6f, 0f);

    private readonly Dictionary<string, NpcDialogueNode> nodeLookup = new Dictionary<string, NpcDialogueNode>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> completedRewardNodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private string runtimeStartNodeIdOverride;

    public string ProgressionSaveKey
    {
        get { return ResolveProgressionSaveKey(); }
    }

    public string NpcDisplayName
    {
        get { return string.IsNullOrWhiteSpace(npcDisplayName) ? gameObject.name : npcDisplayName; }
    }

    public float InteractionRadius
    {
        get { return interactionRadius; }
    }

    public bool HasConversationNodes
    {
        get { return dialogueNodes != null && dialogueNodes.Length > 0; }
    }

    private void Awake()
    {
        RegisterInteractable();
        RebuildNodeLookup();
    }

    private void OnEnable()
    {
        RegisterInteractable();
    }

    private void OnDisable()
    {
        registeredInteractables.Remove(this);
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(progressionSaveKey))
        {
            progressionSaveKey = BuildFallbackProgressionSaveKey();
        }

        RebuildNodeLookup();
        ValidateDialogueDropPrefabs();
    }

    private void Update()
    {
        if (!HasConversationNodes)
        {
            return;
        }

        PlayerMovement localPlayer = FindNearestEligiblePlayer();
        if (localPlayer == null)
        {
            return;
        }

        NpcDialogueUI dialogueUI = NpcDialogueUI.ExistingInstance;
        if (dialogueUI != null && dialogueUI.IsConversationActive && !dialogueUI.IsConversationOwnedBy(this))
        {
            return;
        }

        if (dialogueUI != null && dialogueUI.IsConversationOwnedBy(this))
        {
            return;
        }

        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
        {
            NpcDialogueUI.Instance.BeginConversation(this, localPlayer);
        }
    }

    public void ConfigureConversation(string displayName, NpcDialogueNode[] nodes, string firstNodeId = DefaultStartNodeId)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            npcDisplayName = displayName;
        }

        dialogueNodes = nodes ?? new NpcDialogueNode[0];
        startNodeId = string.IsNullOrWhiteSpace(firstNodeId) ? DefaultStartNodeId : firstNodeId;
        RebuildNodeLookup();
    }

    public NpcDialogueNode GetStartingNode()
    {
        RebuildNodeLookup();

        NpcDialogueNode node;
        if (!string.IsNullOrWhiteSpace(runtimeStartNodeIdOverride) && nodeLookup.TryGetValue(runtimeStartNodeIdOverride, out node))
        {
            return node;
        }

        if (!string.IsNullOrWhiteSpace(startNodeId) && nodeLookup.TryGetValue(startNodeId, out node))
        {
            return node;
        }

        for (int i = 0; i < dialogueNodes.Length; i++)
        {
            if (dialogueNodes[i] != null)
            {
                return dialogueNodes[i];
            }
        }

        return null;
    }

    public NpcDialogueNode GetNodeById(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return null;
        }

        RebuildNodeLookup();
        NpcDialogueNode node;
        nodeLookup.TryGetValue(nodeId, out node);
        return node;
    }

    public void CompleteNodeForPlayer(NpcDialogueNode node, PlayerMovement player)
    {
        if (node == null || player == null)
        {
            return;
        }

        bool shouldGrantRewards = ApplyLocalNodeProgression(node);

        if (!shouldGrantRewards)
        {
            return;
        }

        if (!player.IsSpawned)
        {
            GrantNodeRewardsLocally(node, player);
            return;
        }

        if (player.IsServer)
        {
            GrantNodeRewardsAuthoritatively(node, player);
            return;
        }

        player.RequestNpcDialogueNodeCompletion(ProgressionSaveKey, node.nodeId);
    }

    public void ApplySavedProgression(NpcDialogueProgressSaveEntry progressEntry, bool suppressEvents = false)
    {
        completedRewardNodeIds.Clear();
        runtimeStartNodeIdOverride = null;

        if (progressEntry != null)
        {
            if (!string.IsNullOrWhiteSpace(progressEntry.startNodeIdOverride) && GetNodeById(progressEntry.startNodeIdOverride) != null)
            {
                runtimeStartNodeIdOverride = progressEntry.startNodeIdOverride;
            }

            if (progressEntry.completedRewardNodeIds != null)
            {
                for (int index = 0; index < progressEntry.completedRewardNodeIds.Length; index++)
                {
                    string completedNodeId = progressEntry.completedRewardNodeIds[index];
                    if (!string.IsNullOrWhiteSpace(completedNodeId))
                    {
                        completedRewardNodeIds.Add(completedNodeId);
                    }
                }
            }
        }

        if (!suppressEvents)
        {
            NotifyLocalProgressionChanged();
        }
    }

    public NpcDialogueProgressSaveEntry ExportProgression()
    {
        if (string.IsNullOrWhiteSpace(runtimeStartNodeIdOverride) && completedRewardNodeIds.Count == 0)
        {
            return null;
        }

        string[] completedNodeIds = new string[completedRewardNodeIds.Count];
        completedRewardNodeIds.CopyTo(completedNodeIds);
        Array.Sort(completedNodeIds, StringComparer.OrdinalIgnoreCase);

        return new NpcDialogueProgressSaveEntry
        {
            npcId = ProgressionSaveKey,
            startNodeIdOverride = runtimeStartNodeIdOverride ?? string.Empty,
            completedRewardNodeIds = completedNodeIds,
        };
    }

    public bool TryGrantNodeRewardsAuthoritatively(string nodeId, PlayerMovement player)
    {
        if (player == null || string.IsNullOrWhiteSpace(nodeId))
        {
            return false;
        }

        NpcDialogueNode node = GetNodeById(nodeId);
        if (node == null)
        {
            return false;
        }

        GrantNodeRewardsAuthoritatively(node, player);
        return true;
    }

    public Sprite ResolvePortrait(NpcDialogueNode node)
    {
        if (node != null && node.portraitSprite != null)
        {
            return node.portraitSprite;
        }

        return defaultPortraitSprite;
    }

    public NpcDialoguePortraitSide ResolvePortraitSide(NpcDialogueNode node)
    {
        if (node == null)
        {
            return defaultPortraitSide;
        }

        return node.portraitSprite != null ? node.portraitSide : defaultPortraitSide;
    }

    public string ResolveSpeakerName(NpcDialogueNode node)
    {
        if (node != null && !string.IsNullOrWhiteSpace(node.speakerName))
        {
            return node.speakerName;
        }

        return NpcDisplayName;
    }

    public bool IsPlayerInRange(PlayerMovement player)
    {
        if (player == null)
        {
            return false;
        }

        return Vector3.Distance(transform.position, player.transform.position) <= interactionRadius;
    }

    public PlayerMovement FindNearestEligiblePlayer()
    {
        PlayerMovement[] players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        PlayerMovement nearestPlayer = null;
        float nearestDistance = float.MaxValue;
        bool foundOwnerPlayer = false;

        for (int i = 0; i < players.Length; i++)
        {
            PlayerMovement player = players[i];
            if (player == null || player.IsDead)
            {
                continue;
            }

            if (!player.IsOwner && foundOwnerPlayer)
            {
                continue;
            }

            if (!player.IsOwner && !foundOwnerPlayer)
            {
                float distanceWithoutOwner = Vector3.Distance(transform.position, player.transform.position);
                if (distanceWithoutOwner <= interactionRadius && distanceWithoutOwner < nearestDistance)
                {
                    nearestPlayer = player;
                    nearestDistance = distanceWithoutOwner;
                }

                continue;
            }

            if (player.IsOwner && !foundOwnerPlayer)
            {
                nearestPlayer = null;
                nearestDistance = float.MaxValue;
                foundOwnerPlayer = true;
            }

            float distance = Vector3.Distance(transform.position, player.transform.position);
            if (distance > interactionRadius || distance >= nearestDistance)
            {
                continue;
            }

            nearestPlayer = player;
            nearestDistance = distance;
        }

        return nearestPlayer;
    }

    private void RebuildNodeLookup()
    {
        nodeLookup.Clear();

        if (dialogueNodes == null)
        {
            return;
        }

        for (int i = 0; i < dialogueNodes.Length; i++)
        {
            NpcDialogueNode node = dialogueNodes[i];
            if (node == null || string.IsNullOrWhiteSpace(node.nodeId) || nodeLookup.ContainsKey(node.nodeId))
            {
                continue;
            }

            nodeLookup.Add(node.nodeId, node);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.95f, 0.35f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, interactionRadius);
    }

    private bool ApplyLocalNodeProgression(NpcDialogueNode node)
    {
        bool shouldGrantRewards = !node.rewardOnlyOnce || string.IsNullOrWhiteSpace(node.nodeId) || !completedRewardNodeIds.Contains(node.nodeId);
        bool progressionChanged = false;

        if (shouldGrantRewards && node.rewardOnlyOnce && !string.IsNullOrWhiteSpace(node.nodeId))
        {
            completedRewardNodeIds.Add(node.nodeId);
            progressionChanged = true;
        }

        if (!string.IsNullOrWhiteSpace(node.futureStartNodeId) && GetNodeById(node.futureStartNodeId) != null && !string.Equals(runtimeStartNodeIdOverride, node.futureStartNodeId, StringComparison.OrdinalIgnoreCase))
        {
            runtimeStartNodeIdOverride = node.futureStartNodeId;
            progressionChanged = true;
        }

        if (progressionChanged)
        {
            NotifyLocalProgressionChanged();
        }

        return shouldGrantRewards;
    }

    private void GrantNodeRewardsLocally(NpcDialogueNode node, PlayerMovement player)
    {
        if (node.silverPenniesReward > 0)
        {
            player.CollectSilverPennies(node.silverPenniesReward);
        }

        SpawnDialogueCollectibleDrops(node, authoritativeSpawn: false);
    }

    private void GrantNodeRewardsAuthoritatively(NpcDialogueNode node, PlayerMovement player)
    {
        if (node.silverPenniesReward > 0)
        {
            player.CollectSilverPennies(node.silverPenniesReward);
        }

        SpawnDialogueCollectibleDrops(node, authoritativeSpawn: true);
    }

    private void SpawnDialogueCollectibleDrops(NpcDialogueNode node, bool authoritativeSpawn)
    {
        if (node == null || node.collectibleDrops == null || node.collectibleDrops.Length == 0)
        {
            return;
        }

        Vector3 basePosition = rewardDropOrigin != null ? rewardDropOrigin.position : transform.position + rewardDropOffset;

        for (int i = 0; i < node.collectibleDrops.Length; i++)
        {
            NpcDialogueCollectibleDrop drop = node.collectibleDrops[i];
            if (drop == null || drop.collectiblePrefab == null)
            {
                continue;
            }

            Vector3 spawnPosition = basePosition + drop.localOffset;
            CollectibleItem spawnedCollectible = Instantiate(drop.collectiblePrefab, spawnPosition, drop.collectiblePrefab.transform.rotation);
            spawnedCollectible.SetStackAmount(drop.stackAmount);

            if (authoritativeSpawn)
            {
                NetworkObject collectibleNetworkObject = spawnedCollectible.GetComponent<NetworkObject>();
                if (collectibleNetworkObject != null && !collectibleNetworkObject.IsSpawned)
                {
                    collectibleNetworkObject.Spawn();
                }
                else if (collectibleNetworkObject == null)
                {
                    Debug.LogWarning($"Dialogue NPC '{name}' spawned loot '{spawnedCollectible.name}' without a NetworkObject. Remote clients will not see this loot in multiplayer.");
                }
            }
        }
    }

    private void RegisterInteractable()
    {
        if (!registeredInteractables.Contains(this))
        {
            registeredInteractables.Add(this);
        }
    }

    private string ResolveProgressionSaveKey()
    {
        if (string.IsNullOrWhiteSpace(progressionSaveKey))
        {
            progressionSaveKey = BuildFallbackProgressionSaveKey();
        }

        return progressionSaveKey;
    }

    private string BuildFallbackProgressionSaveKey()
    {
        string sceneName = gameObject.scene.IsValid() ? gameObject.scene.name : "Scene";
        return $"{sceneName}/{BuildHierarchyPath(transform)}";
    }

    private string BuildHierarchyPath(Transform target)
    {
        if (target == null)
        {
            return gameObject.name;
        }

        List<string> segments = new List<string>();
        Transform current = target;
        while (current != null)
        {
            segments.Add(current.name);
            current = current.parent;
        }

        segments.Reverse();
        return string.Join("/", segments);
    }

    private void NotifyLocalProgressionChanged()
    {
        LocalProgressionChanged?.Invoke();
    }

    public static NpcDialogueInteractable FindByProgressionSaveKey(string progressionKey)
    {
        if (string.IsNullOrWhiteSpace(progressionKey))
        {
            return null;
        }

        for (int index = 0; index < registeredInteractables.Count; index++)
        {
            NpcDialogueInteractable interactable = registeredInteractables[index];
            if (interactable != null && string.Equals(interactable.ProgressionSaveKey, progressionKey, StringComparison.OrdinalIgnoreCase))
            {
                return interactable;
            }
        }

        return null;
    }

    public static NpcDialogueProgressSaveEntry[] CaptureAllProgressionState()
    {
        List<NpcDialogueProgressSaveEntry> entries = new List<NpcDialogueProgressSaveEntry>();
        for (int index = 0; index < registeredInteractables.Count; index++)
        {
            NpcDialogueInteractable interactable = registeredInteractables[index];
            if (interactable == null)
            {
                continue;
            }

            NpcDialogueProgressSaveEntry entry = interactable.ExportProgression();
            if (entry != null)
            {
                entries.Add(entry);
            }
        }

        return entries.ToArray();
    }

    public static void ApplyAllProgressionState(NpcDialogueProgressSaveEntry[] entries, bool suppressEvents = false)
    {
        Dictionary<string, NpcDialogueProgressSaveEntry> entryLookup = new Dictionary<string, NpcDialogueProgressSaveEntry>(StringComparer.OrdinalIgnoreCase);
        if (entries != null)
        {
            for (int index = 0; index < entries.Length; index++)
            {
                NpcDialogueProgressSaveEntry entry = entries[index];
                if (entry != null && !string.IsNullOrWhiteSpace(entry.npcId) && !entryLookup.ContainsKey(entry.npcId))
                {
                    entryLookup.Add(entry.npcId, entry);
                }
            }
        }

        for (int index = 0; index < registeredInteractables.Count; index++)
        {
            NpcDialogueInteractable interactable = registeredInteractables[index];
            if (interactable == null)
            {
                continue;
            }

            entryLookup.TryGetValue(interactable.ProgressionSaveKey, out NpcDialogueProgressSaveEntry entry);
            interactable.ApplySavedProgression(entry, suppressEvents);
        }
    }

    private void ValidateDialogueDropPrefabs()
    {
        if (dialogueNodes == null)
        {
            return;
        }

        for (int nodeIndex = 0; nodeIndex < dialogueNodes.Length; nodeIndex++)
        {
            NpcDialogueNode node = dialogueNodes[nodeIndex];
            if (node == null || node.collectibleDrops == null)
            {
                continue;
            }

            for (int dropIndex = 0; dropIndex < node.collectibleDrops.Length; dropIndex++)
            {
                NpcDialogueCollectibleDrop drop = node.collectibleDrops[dropIndex];
                if (drop == null || drop.collectiblePrefab == null)
                {
                    continue;
                }

                if (drop.collectiblePrefab.GetComponent<NetworkObject>() == null)
                {
                    Debug.LogWarning($"NpcDialogueInteractable '{name}' node '{node.nodeId}' has a collectible drop prefab '{drop.collectiblePrefab.name}' without a NetworkObject. Multiplayer dialogue drops will not replicate until the prefab is network-registered.", this);
                }
            }
        }
    }
}