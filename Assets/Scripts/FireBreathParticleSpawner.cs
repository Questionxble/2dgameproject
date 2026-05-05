using UnityEngine;

/// <summary>
/// Component that spawns fire particle emitters when dragon fire breath attacks hit surfaces or players
/// </summary>
public class FireBreathParticleSpawner : MonoBehaviour
{
    [HideInInspector]
    public DragonBoss dragonBoss; // Reference to the dragon boss
    
    private float surfaceCheckInterval = 0.5f; // Check for surface every 0.5 seconds
    private DamageObject damageObject;
    private Collider2D damageCollider;
    
    void Start()
    {
        // Get the DamageObject component
        damageObject = GetComponent<DamageObject>();
        damageCollider = GetComponent<Collider2D>();
        
        // Subscribe to the damage object's player hit callback
        if (damageObject != null)
        {
            damageObject.onPlayerHit += HandlePlayerHit;
            Debug.Log($"FireBreathParticleSpawner: Successfully subscribed to DamageObject callbacks on {name}");
        }
        else
        {
            Debug.LogWarning($"FireBreathParticleSpawner: No DamageObject found on {name}");
        }
        
        Debug.Log($"FireBreathParticleSpawner: Starting on {name}");
        
        // Start checking for surface collisions
        InvokeRepeating(nameof(CheckForSurfaceCollision), 0.5f, surfaceCheckInterval);
    }
    
    /// <summary>
    /// Called when DamageObject hits a player
    /// </summary>
    private void HandlePlayerHit(Transform player)
    {
        if (dragonBoss != null && player != null)
        {
            dragonBoss.HandlePlayerFireHit(player);
            Debug.Log($"FireBreathParticleSpawner: Player hit detected, notifying dragon boss");
        }
    }
    
    /// <summary>
    /// Periodically check for surface collisions to spawn fire particles
    /// </summary>
    void CheckForSurfaceCollision()
    {
        if (dragonBoss == null) return;

        // Get all colliders on this GameObject to exclude them from raycast
        Collider2D[] selfColliders = GetComponents<Collider2D>();

        if (TryFindSurfaceHit(Vector2.down, 10f, selfColliders, out RaycastHit2D groundHit))
        {
            dragonBoss.HandleSurfaceFireHit(groundHit.point, groundHit.collider);
            return;
        }

        if (TryFindSurfaceHit(Vector2.left, 3f, selfColliders, out RaycastHit2D leftHit))
        {
            dragonBoss.HandleSurfaceFireHit(leftHit.point, leftHit.collider);
            return;
        }

        if (TryFindSurfaceHit(Vector2.right, 3f, selfColliders, out RaycastHit2D rightHit))
        {
            dragonBoss.HandleSurfaceFireHit(rightHit.point, rightHit.collider);
        }
    }

    private bool TryFindSurfaceHit(Vector2 direction, float distance, Collider2D[] selfColliders, out RaycastHit2D resolvedHit)
    {
        LayerMask surfaceLayerMask = dragonBoss.GetSurfaceLayerMask();
        Vector2 castOrigin = damageCollider != null ? damageCollider.bounds.center : (Vector2)transform.position;
        Vector2 castSize = damageCollider != null ? damageCollider.bounds.size : Vector2.one * 0.25f;

        if (direction.x != 0f)
        {
            castSize = new Vector2(Mathf.Max(0.1f, castSize.x * 0.3f), Mathf.Max(0.2f, castSize.y * 0.95f));
        }
        else
        {
            castSize = new Vector2(Mathf.Max(0.2f, castSize.x * 0.95f), Mathf.Max(0.1f, castSize.y * 0.3f));
        }

        RaycastHit2D[] hits = Physics2D.BoxCastAll(castOrigin, castSize, 0f, direction, distance, surfaceLayerMask);
        for (int hitIndex = 0; hitIndex < hits.Length; hitIndex++)
        {
            RaycastHit2D hit = hits[hitIndex];
            if (hit.collider == null || IsSelfCollider(hit.collider, selfColliders) || IsInvalidCollider(hit.collider))
            {
                continue;
            }

            resolvedHit = hit;
            return true;
        }

        resolvedHit = default;
        return false;
    }
    
    /// <summary>
    /// Check if the collider belongs to this GameObject (self-collision)
    /// </summary>
    private bool IsSelfCollider(Collider2D collider, Collider2D[] selfColliders)
    {
        foreach (var selfCollider in selfColliders)
        {
            if (collider == selfCollider)
            {
                Debug.Log($"FireBreathParticleSpawner: Filtered out self collider: {collider.name}");
                return true;
            }
        }
        return false;
    }
    
    /// <summary>
    /// Check if a collider should be ignored for particle spawning
    /// </summary>
    private bool IsInvalidCollider(Collider2D collider)
    {
        // Ignore dragon boss itself
        if (collider.GetComponent<DragonBoss>() != null)
        {
            Debug.Log($"FireBreathParticleSpawner: Filtered out DragonBoss: {collider.name}");
            return true;
        }
            
        // Ignore other damage zones (fire blocks, flying damage zones)
        if (collider.GetComponent<DamageObject>() != null)
        {
            Debug.Log($"FireBreathParticleSpawner: Filtered out DamageObject: {collider.name}");
            return true;
        }
            
        // Ignore entities layer
        if (collider.gameObject.layer == LayerMask.NameToLayer("Entities"))
        {
            Debug.Log($"FireBreathParticleSpawner: Filtered out Entities layer: {collider.name}");
            return true;
        }
            
        // Ignore objects with "Dragon" in the name
        if (collider.name.Contains("Dragon"))
        {
            Debug.Log($"FireBreathParticleSpawner: Filtered out Dragon object: {collider.name}");
            return true;
        }
        
        // Ignore player
        if (collider.CompareTag("Player"))
        {
            Debug.Log($"FireBreathParticleSpawner: Filtered out Player: {collider.name}");
            return true;
        }
            
        // Valid surface - log what we found
        string layerName = LayerMask.LayerToName(collider.gameObject.layer);
        Debug.Log($"FireBreathParticleSpawner: VALID SURFACE FOUND: {collider.name} on layer {collider.gameObject.layer} ({layerName})");
        return false;
    }
    
    void OnDestroy()
    {
        // Clean up the repeating invoke
        CancelInvoke();
        
        // Unsubscribe from callback
        if (damageObject != null)
        {
            damageObject.onPlayerHit -= HandlePlayerHit;
        }
    }
}