using System;
using System.Collections.Generic;
using UnityEngine;

public class NpcAmbientChat : MonoBehaviour
{
    [Serializable]
    private class AmbientResponseProfile
    {
        public string profileId = "default";

        public string[] walkByResponses = new string[0];
        public string[] jumpResponses = new string[0];
        public string[] attackResponses = new string[0];
    }

    [Serializable]
    private class AmbientResponseLibrary
    {
        public string defaultProfileId = "default";
        public string[] walkByResponses = new string[0];
        public string[] jumpResponses = new string[0];
        public string[] attackResponses = new string[0];
        public AmbientResponseProfile[] profiles = new AmbientResponseProfile[0];
    }

    private sealed class ObservedPlayerState
    {
        public bool WasNearby;
        public bool WasMoving;
        public bool WasAttacking;
        public float PreviousVerticalVelocity;
        public float LastReactionTime;
    }

    [Header("Response Source")]
    [SerializeField] private TextAsset ambientResponseJson;
    [SerializeField] private string fallbackResourcesPath = "NpcAmbientResponses";
    [SerializeField] private string responseProfileId = "default";

    [Header("Reaction Settings")]
    [SerializeField] private float reactionRadius = 5f;
    [SerializeField] private float attackReactionRadius = 4f;
    [SerializeField] private float walkByChance = 0.18f;
    [SerializeField] private float jumpChance = 0.3f;
    [SerializeField] private float attackChance = 0.35f;
    [SerializeField] private float walkByMovementThreshold = 0.2f;
    [SerializeField] private float jumpVelocityThreshold = 2.5f;
    [SerializeField] private float minSecondsBetweenReactions = 4f;
    [SerializeField] private float perPlayerReactionCooldown = 2f;

    [Header("References")]
    [SerializeField] private NpcChatBubbleController bubbleController;

    private readonly Dictionary<PlayerMovement, ObservedPlayerState> observedPlayers = new Dictionary<PlayerMovement, ObservedPlayerState>();
    private AmbientResponseProfile activeResponses = new AmbientResponseProfile();
    private float nextAllowedReactionTime;

    private void Awake()
    {
        if (bubbleController == null)
        {
            bubbleController = GetComponent<NpcChatBubbleController>();
        }

        if (bubbleController == null)
        {
            bubbleController = gameObject.AddComponent<NpcChatBubbleController>();
        }

        LoadResponses();
    }

    private void Update()
    {
        PlayerMovement[] players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        HashSet<PlayerMovement> seenPlayers = new HashSet<PlayerMovement>();

        for (int i = 0; i < players.Length; i++)
        {
            PlayerMovement player = players[i];
            if (player == null || player.IsDead)
            {
                continue;
            }

            seenPlayers.Add(player);

            ObservedPlayerState state;
            if (!observedPlayers.TryGetValue(player, out state))
            {
                state = new ObservedPlayerState();
                observedPlayers[player] = state;
            }

            Rigidbody2D playerBody = player.GetComponent<Rigidbody2D>();
            WeaponClassController weaponController = player.GetComponent<WeaponClassController>();
            float distance = Vector3.Distance(transform.position, player.transform.position);
            bool isNearby = distance <= reactionRadius;
            float horizontalSpeed = playerBody != null ? Mathf.Abs(playerBody.linearVelocity.x) : 0f;
            bool isMoving = player.IsMovingHorizontally() && horizontalSpeed >= walkByMovementThreshold;
            float verticalVelocity = playerBody != null ? playerBody.linearVelocity.y : 0f;
            bool jumpTriggered = isNearby && state.WasNearby && state.PreviousVerticalVelocity <= 0.1f && verticalVelocity > jumpVelocityThreshold;
            bool isAttacking = weaponController != null && weaponController.IsPlayingAttackAnimation() && distance <= attackReactionRadius;
            bool attackTriggered = isAttacking && !state.WasAttacking;
            bool walkByTriggered = isNearby && isMoving && !state.WasNearby;

            if (CanReact(state))
            {
                if (attackTriggered && TryReact(activeResponses.attackResponses, attackChance, state))
                {
                    UpdateObservedState(state, isNearby, isMoving, isAttacking, verticalVelocity);
                    continue;
                }

                if (jumpTriggered && TryReact(activeResponses.jumpResponses, jumpChance, state))
                {
                    UpdateObservedState(state, isNearby, isMoving, isAttacking, verticalVelocity);
                    continue;
                }

                if (walkByTriggered && TryReact(activeResponses.walkByResponses, walkByChance, state))
                {
                    UpdateObservedState(state, isNearby, isMoving, isAttacking, verticalVelocity);
                    continue;
                }
            }

            UpdateObservedState(state, isNearby, isMoving, isAttacking, verticalVelocity);
        }

        RemoveMissingPlayers(seenPlayers);
    }

    public void SetResponseProfile(string profileId, TextAsset source = null)
    {
        responseProfileId = string.IsNullOrWhiteSpace(profileId) ? "default" : profileId.Trim();
        if (source != null)
        {
            ambientResponseJson = source;
        }

        LoadResponses();
    }

    private void LoadResponses()
    {
        TextAsset source = ambientResponseJson;
        if (source == null && !string.IsNullOrWhiteSpace(fallbackResourcesPath))
        {
            source = Resources.Load<TextAsset>(fallbackResourcesPath);
        }

        if (source == null || string.IsNullOrWhiteSpace(source.text))
        {
            activeResponses = CreateFallbackProfile(new AmbientResponseLibrary());
            return;
        }

        try
        {
            AmbientResponseLibrary parsedLibrary = JsonUtility.FromJson<AmbientResponseLibrary>(source.text);
            activeResponses = ResolveProfile(parsedLibrary ?? new AmbientResponseLibrary());
        }
        catch (Exception exception)
        {
            Debug.LogWarning("NpcAmbientChat failed to parse response JSON: " + exception.Message);
            activeResponses = CreateFallbackProfile(new AmbientResponseLibrary());
        }
    }

    private AmbientResponseProfile ResolveProfile(AmbientResponseLibrary library)
    {
        AmbientResponseProfile fallbackProfile = CreateFallbackProfile(library);
        string targetProfileId = string.IsNullOrWhiteSpace(responseProfileId) ? fallbackProfile.profileId : responseProfileId;

        AmbientResponseProfile matchedProfile = FindProfile(library, targetProfileId);
        if (matchedProfile != null)
        {
            return matchedProfile;
        }

        AmbientResponseProfile defaultProfile = FindProfile(library, library.defaultProfileId);
        return defaultProfile ?? fallbackProfile;
    }

    private AmbientResponseProfile FindProfile(AmbientResponseLibrary library, string profileId)
    {
        if (library == null || library.profiles == null || string.IsNullOrWhiteSpace(profileId))
        {
            return null;
        }

        for (int i = 0; i < library.profiles.Length; i++)
        {
            AmbientResponseProfile profile = library.profiles[i];
            if (profile == null || string.IsNullOrWhiteSpace(profile.profileId))
            {
                continue;
            }

            if (string.Equals(profile.profileId.Trim(), profileId.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return profile;
            }
        }

        return null;
    }

    private AmbientResponseProfile CreateFallbackProfile(AmbientResponseLibrary library)
    {
        AmbientResponseProfile profile = new AmbientResponseProfile();
        profile.profileId = string.IsNullOrWhiteSpace(library.defaultProfileId) ? "default" : library.defaultProfileId.Trim();
        profile.walkByResponses = library.walkByResponses ?? new string[0];
        profile.jumpResponses = library.jumpResponses ?? new string[0];
        profile.attackResponses = library.attackResponses ?? new string[0];
        return profile;
    }

    private bool CanReact(ObservedPlayerState state)
    {
        return Time.time >= nextAllowedReactionTime && Time.time >= state.LastReactionTime + perPlayerReactionCooldown;
    }

    private bool TryReact(string[] responses, float chance, ObservedPlayerState state)
    {
        if (responses == null || responses.Length == 0)
        {
            return false;
        }

        if (UnityEngine.Random.value > Mathf.Clamp01(chance))
        {
            return false;
        }

        string selectedResponse = responses[UnityEngine.Random.Range(0, responses.Length)];
        if (string.IsNullOrWhiteSpace(selectedResponse))
        {
            return false;
        }

        bubbleController.ShowMessage(selectedResponse);
        state.LastReactionTime = Time.time;
        nextAllowedReactionTime = Time.time + minSecondsBetweenReactions;
        return true;
    }

    private void UpdateObservedState(ObservedPlayerState state, bool isNearby, bool isMoving, bool isAttacking, float verticalVelocity)
    {
        state.WasNearby = isNearby;
        state.WasMoving = isMoving;
        state.WasAttacking = isAttacking;
        state.PreviousVerticalVelocity = verticalVelocity;
    }

    private void RemoveMissingPlayers(HashSet<PlayerMovement> seenPlayers)
    {
        List<PlayerMovement> playersToRemove = null;

        foreach (KeyValuePair<PlayerMovement, ObservedPlayerState> entry in observedPlayers)
        {
            if (entry.Key == null || !seenPlayers.Contains(entry.Key))
            {
                if (playersToRemove == null)
                {
                    playersToRemove = new List<PlayerMovement>();
                }

                playersToRemove.Add(entry.Key);
            }
        }

        if (playersToRemove == null)
        {
            return;
        }

        for (int i = 0; i < playersToRemove.Count; i++)
        {
            observedPlayers.Remove(playersToRemove[i]);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, reactionRadius);

        Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, attackReactionRadius);
    }
}