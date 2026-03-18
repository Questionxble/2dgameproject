using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target Settings")]
    [SerializeField] private Transform target; // The target to follow
    
    [Header("Screen Bounds")]
    [SerializeField] private float leftBound = 2f;    // Distance from left edge before camera moves
    [SerializeField] private float rightBound = 2f;   // Distance from right edge before camera moves
    [SerializeField] private float topBound = 1.5f;   // Distance from top edge before camera moves
    [SerializeField] private float bottomBound = 1.5f; // Distance from bottom edge before camera moves
    
    [Header("Follow Settings")]
    [SerializeField] private bool followX = true;     // Follow target on X axis
    [SerializeField] private bool followY = true;     // Follow target on Y axis
    [SerializeField] private bool smoothFollow = false;     // Toggle for camera to smoothly follow target
    [SerializeField] private float followSpeed = 2f;  // How fast camera follows target

    [Header("Offset")]
    [SerializeField] private Vector3 offset = Vector3.zero; // Offset from target position

    private Camera cam;
    private Vector3 targetPosition;
    private bool isFollowing = false;
    private bool updateCameraX = false; // Out of left and right bounds
    private bool updateCameraY = false; // Out of top and bottom bounds

    void Start()
    {
        cam = GetComponent<Camera>();
        
        // If no target assigned, try to find player
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                target = player.transform;
        }
        
        // Initialize target position to current camera position
        targetPosition = transform.position;
    }
    
    void LateUpdate()
    {
        if (target == null) return;
        
        CheckScreenBounds();
        
        if (isFollowing)
        {
            UpdateCameraPosition();
        }
    }
    
    private void CheckScreenBounds()
    {
        // Convert target world position to viewport position (0-1 range)
        Vector3 viewportPos = cam.WorldToViewportPoint(target.position);
        
        bool shouldFollow = false;
        updateCameraX = false;
        updateCameraY = false;

        // Check if target is outside the defined bounds
        if (followX)
        {
            float leftViewport = leftBound / (cam.orthographicSize * 2f * cam.aspect);
            float rightViewport = 1f - (rightBound / (cam.orthographicSize * 2f * cam.aspect));
            
            if (viewportPos.x < leftViewport || viewportPos.x > rightViewport)
            {
                shouldFollow = true;
                updateCameraX = true;
            }
        }
        
        if (followY)
        {
            float bottomViewport = bottomBound / (cam.orthographicSize * 2f);
            float topViewport = 1f - (topBound / (cam.orthographicSize * 2f));
            
            if (viewportPos.y < bottomViewport || viewportPos.y > topViewport)
            {
                shouldFollow = true;
                updateCameraY = true;
            }
        }
        
        isFollowing = shouldFollow;
    }

    private void UpdateCameraPosition()
    {
        // Calculate desired camera position

        Vector3 desiredPosition = transform.position;

        // Follow X and Y axes if needed
        if (!updateCameraX)
            desiredPosition.x = transform.position.x;
        else{
            // Target is further left than camera, so move camera left 
            if (target.position.x < transform.position.x)
                desiredPosition.x = target.position.x + (cam.orthographicSize * cam.aspect) - leftBound;
            else // Further right
                desiredPosition.x = target.position.x - (cam.orthographicSize * cam.aspect) + rightBound;
        }
        if (!updateCameraY)
            desiredPosition.y = transform.position.y;
        else
        {
            // Target is further down than camera, so move camera down 
            if (target.position.y < transform.position.y)
                desiredPosition.y = target.position.y + (cam.orthographicSize) - bottomBound;
            else // Further up
                desiredPosition.y = target.position.y - (cam.orthographicSize) + topBound;
        }

        // Always keep the same Z position for 2D camera

        if (smoothFollow)
        {
            // Smoothly move camera towards target
            targetPosition = Vector3.Lerp(targetPosition, desiredPosition, followSpeed * Time.deltaTime);
            transform.position = targetPosition;
        }else{
            // Snap camera to keep target in frame
            transform.position = desiredPosition;
        }
    }

    // Method to manually center camera on target
    [ContextMenu("Center on Target")]
    public void CenterOnTarget()
    {
        if (target != null)
        {
            Vector3 newPosition = target.position + offset;
            newPosition.z = transform.position.z;
            transform.position = newPosition;
            targetPosition = newPosition;
        }
    }
    
    // Method to set new target at runtime
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
    
    // Gizmos to visualize bounds in Scene view
    void OnDrawGizmosSelected()
    {
        if (cam == null) cam = GetComponent<Camera>();
        
        // Calculate screen bounds in world space
        float height = cam.orthographicSize * 2f;
        float width = height * cam.aspect;
        
        Vector3 center = transform.position;
        
        // Draw outer bounds (screen edges)
        Gizmos.color = Color.white;
        Gizmos.DrawWireCube(center, new Vector3(width, height, 0));
        
        // Draw inner bounds (follow trigger area)
        float innerWidth = width - (leftBound + rightBound);
        float innerHeight = height - (topBound + bottomBound);
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(center, new Vector3(innerWidth, innerHeight, 0));
        
        // Draw center point
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(center, 0.1f);
    }
}
