using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum NpcDialoguePortraitSide
{
    Left,
    Right
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
}

public class NpcDialogueInteractable : MonoBehaviour
{
    private const string DefaultStartNodeId = "start";

    [Header("Conversation")]
    [SerializeField] private string npcDisplayName = "NPC";
    [SerializeField] private float interactionRadius = 2.75f;
    [SerializeField] private string startNodeId = DefaultStartNodeId;
    [SerializeField] private Sprite defaultPortraitSprite;
    [SerializeField] private NpcDialoguePortraitSide defaultPortraitSide = NpcDialoguePortraitSide.Left;
    [SerializeField] private NpcDialogueNode[] dialogueNodes = new NpcDialogueNode[0];

    private readonly Dictionary<string, NpcDialogueNode> nodeLookup = new Dictionary<string, NpcDialogueNode>(StringComparer.OrdinalIgnoreCase);

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
        RebuildNodeLookup();
    }

    private void OnValidate()
    {
        RebuildNodeLookup();
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
}