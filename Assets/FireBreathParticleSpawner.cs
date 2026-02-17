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
    
    void Start()
    {
        // Get the DamageObject component
        damageObject = GetComponent<DamageObject>();
        
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
        
        Vector3 position = transform.position;
        LayerMask surfaceLayerMask = dragonBoss.GetSurfaceLayerMask();
        
        Debug.Log($"FireBreathParticleSpawner: Checking surface collision from {position} with layerMask {surfaceLayerMask.value}");
        
        // Get all colliders on this GameObject to exclude them from raycast
        Collider2D[] selfColliders = GetComponents<Collider2D>();
        
        // Raycast down to find surfaces (longer distance for flying attacks)
        RaycastHit2D[] allGroundHits = Physics2D.RaycastAll(position, Vector2.down, 10f, surfaceLayerMask);
        foreach (var hit in allGroundHits)
        {
            if (hit.collider != null && !IsSelfCollider(hit.collider, selfColliders))
            {
                Debug.Log($"FireBreathParticleSpawner: Ground raycast hit {hit.collider.name} on layer {hit.collider.gameObject.layer}");
                
                // Additional check: make sure we're not hitting the dragon or other damage zones
                if (!IsInvalidCollider(hit.collider))
                {
                    dragonBoss.HandleSurfaceFireHit(hit.point);
                    Debug.Log($"FireBreathParticleSpawner: Valid ground hit at {hit.point} on {hit.collider.name}");
                    break; // Only spawn one particle per direction
                }
            }
        }
        
        // Check sides for wall collisions (shorter distance)
        RaycastHit2D[] leftHits = Physics2D.RaycastAll(position, Vector2.left, 3f, surfaceLayerMask);
        foreach (var hit in leftHits)
        {
            if (hit.collider != null && !IsSelfCollider(hit.collider, selfColliders) && !IsInvalidCollider(hit.collider))
            {
                dragonBoss.HandleSurfaceFireHit(hit.point);
                Debug.Log($"FireBreathParticleSpawner: Valid left wall hit at {hit.point} on {hit.collider.name}");
                break;
            }
        }
        
        RaycastHit2D[] rightHits = Physics2D.RaycastAll(position, Vector2.right, 3f, surfaceLayerMask);
        foreach (var hit in rightHits)
        {
            if (hit.collider != null && !IsSelfCollider(hit.collider, selfColliders) && !IsInvalidCollider(hit.collider))
            {
                dragonBoss.HandleSurfaceFireHit(hit.point);
                Debug.Log($"FireBreathParticleSpawner: Valid right wall hit at {hit.point} on {hit.collider.name}");
                break;
            }
        }
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