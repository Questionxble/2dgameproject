using UnityEngine;

/// <summary>
/// Ladder system that works with a GameObject containing a BoxCollider2D set as a trigger.
/// Player can climb up/down the ladder by pressing movement keys when near the ladder.
/// </summary>
public class Ladder : MonoBehaviour
{
    [Header("Ladder Settings")]
    [SerializeField] private bool isRightSideLadder = true; // True for right side, false for left side
    [SerializeField] private float attachDistance = 0.5f; // How close player needs to be to attach
    [SerializeField] private float sideOffset = 0.4f; // How far from the ladder center to position the player
    
    [Header("Debug")]
    [SerializeField] private bool showGizmos = true;
    
    private BoxCollider2D ladderCollider;
    private PlayerMovement playerMovement;
    
    void Start()
    {
        // Check if GameObject is active
        if (!gameObject.activeInHierarchy)
        {
            Debug.LogError("Ladder GameObject is not active!");
            return;
        }
        
        // Try to find BoxCollider2D
        ladderCollider = GetComponent<BoxCollider2D>();
        if (ladderCollider == null)
        {
            Debug.LogError("Ladder requires a BoxCollider2D component on: " + gameObject.name);
            return;
        }
        
        if (!ladderCollider.isTrigger)
        {
            Debug.LogWarning("BoxCollider2D is NOT set as trigger! Ladder won't work properly.");
        }
        
        // Find the player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerMovement = player.GetComponent<PlayerMovement>();
            
            if (playerMovement == null)
            {
                Debug.LogError("Player found but has no PlayerMovement component!");
            }
        }
        else
        {
            Debug.LogError("No GameObject with 'Player' tag found!");
        }
    }
    
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && playerMovement != null)
        {
            playerMovement.SetNearbyLadder(this.gameObject);
        }
    }
    
    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player") && playerMovement != null)
        {
            // Only stop climbing if player is actually far from the ladder
            // This prevents stopping when player is just positioned at side offset
            if (!CanAttach(other.transform.position))
            {
                playerMovement.SetNearbyLadder(null);
            }
        }
    }
    
    // Remove the collision detection fallback methods since we're using trigger-only approach
    // with BoxCollider2D set to isTrigger = true
    
    /// <summary>
    /// Gets the attachment position on the ladder surface for the player
    /// </summary>
    /// <param name="playerPosition">Current player position</param>
    /// <returns>The position where the player should attach to the ladder</returns>
    public Vector2 GetAttachmentPosition(Vector2 playerPosition)
    {
        if (ladderCollider == null) return playerPosition;
        
        Bounds bounds = ladderCollider.bounds;
        
        // Position player slightly to the side of the ladder center based on ladder type
        float attachX;
        if (isRightSideLadder)
        {
            attachX = bounds.center.x + sideOffset; // Position slightly to the right of center
        }
        else
        {
            attachX = bounds.center.x - sideOffset; // Position slightly to the left of center
        }
        
        // Clamp the Y position to be within the ladder bounds
        float clampedY = Mathf.Clamp(playerPosition.y, bounds.min.y, bounds.max.y);
        
        return new Vector2(attachX, clampedY);
    }
    
    /// <summary>
    /// Checks if the player can attach to this ladder from their current position
    /// </summary>
    /// <param name="playerPosition">Current player position</param>
    /// <returns>True if the player can attach</returns>
    public bool CanAttach(Vector2 playerPosition)
    {
        if (ladderCollider == null) return false;
        
        Bounds bounds = ladderCollider.bounds;
        
        // Allow attachment if player is near the ladder horizontally
        float horizontalDistance = Mathf.Abs(playerPosition.x - bounds.center.x);
        
        // Expand horizontal range to account for side offset positioning
        float expandedHorizontalRange = (bounds.size.x / 2) + attachDistance + sideOffset;
        
        // Allow attachment if:
        // 1. Player is within expanded horizontal range of ladder (accounts for side offset)
        // 2. Player is within or slightly above/below the ladder vertically
        bool withinHorizontalRange = horizontalDistance <= expandedHorizontalRange;
        bool withinVerticalRange = playerPosition.y >= bounds.min.y - attachDistance && 
                                  playerPosition.y <= bounds.max.y + attachDistance;
        
        return withinHorizontalRange && withinVerticalRange;
    }
    
    /// <summary>
    /// Checks if a Y position is within the ladder's bounds
    /// </summary>
    /// <param name="yPosition">Y position to check</param>
    /// <returns>True if the position is within ladder bounds</returns>
    public bool IsWithinVerticalBounds(float yPosition)
    {
        if (ladderCollider == null) return false;
        
        Bounds bounds = ladderCollider.bounds;
        return yPosition >= bounds.min.y && yPosition <= bounds.max.y;
    }
    
    /// <summary>
    /// Gets the top Y position of the ladder
    /// </summary>
    public float GetTopY()
    {
        return ladderCollider != null ? ladderCollider.bounds.max.y : transform.position.y;
    }
    
    /// <summary>
    /// Gets the bottom Y position of the ladder
    /// </summary>
    public float GetBottomY()
    {
        return ladderCollider != null ? ladderCollider.bounds.min.y : transform.position.y;
    }
    
    /// <summary>
    /// Returns whether this is a right-side ladder
    /// </summary>
    public bool IsRightSide()
    {
        return isRightSideLadder;
    }
    
    /// <summary>
    /// Checks if the player is above the ladder (for falling into ladder functionality)
    /// </summary>
    /// <param name="playerPosition">Current player position</param>
    /// <returns>True if player is above the ladder</returns>
    public bool IsPlayerAboveLadder(Vector2 playerPosition)
    {
        if (ladderCollider == null) return false;
        
        Bounds bounds = ladderCollider.bounds;
        bool withinHorizontalRange = Mathf.Abs(playerPosition.x - bounds.center.x) <= bounds.size.x / 2;
        bool isAbove = playerPosition.y > bounds.max.y - 0.5f; // Small buffer zone
        
        return withinHorizontalRange && isAbove;
    }
    
    // Debug method - call this manually in inspector to test
    [ContextMenu("Test Ladder Detection")]
    public void TestLadderDetection()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            // Debug information available if needed
        }
        else
        {
            Debug.LogError("No player found!");
        }
    }
    
    void OnDrawGizmos()
    {
        if (!showGizmos || ladderCollider == null) return;
        
        Bounds bounds = ladderCollider.bounds;
        
        // Draw the attachment position in green
        Gizmos.color = Color.green;
        float attachX = isRightSideLadder ? bounds.center.x + sideOffset : bounds.center.x - sideOffset;
        Vector3 attachmentPosition = new Vector3(attachX, bounds.center.y, 0);
        Gizmos.DrawWireCube(attachmentPosition, new Vector3(0.1f, bounds.size.y, 0.1f));
        
        // Draw attachment detection area (where player can trigger climbing)
        Gizmos.color = Color.yellow;
        float expandedWidth = bounds.size.x + (attachDistance * 2) + (sideOffset * 2);
        Gizmos.DrawWireCube(bounds.center, new Vector3(expandedWidth, bounds.size.y, 0.1f));
    }
}