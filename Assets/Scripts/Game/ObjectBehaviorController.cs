using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;
using System.Collections;

// Water properties definition (shared between PlayerMovement and ObjectBehaviorController)
[System.Serializable]
public class WaterProperties
{
    [Header("Water Physics")]
    public float gravityModifier = 0.1f; // Much reduced gravity in water
    public float speedModifier = 0.6f; // Slower movement in water
    public float jumpForceModifier = 0.8f; // Reduced jump force (swimming upward)
    public float buoyancyForce = 8f; // Strong upward force when not moving
    public float dragForce = 2f; // Strong resistance to movement
    
    [Header("Water Type")]
    public string waterType = "Normal"; // Normal, Current, Toxic, etc.
    public bool allowBreathing = true; // Can player breathe in this water?
    public float currentForceX = 0f; // Horizontal current force
    public float currentForceY = 0f; // Vertical current force
}

public class ObjectBehaviorController : MonoBehaviour
{
    [Header("Platform Settings")]
    [SerializeField] private float dropThroughTime = 0.5f;
    
    [Header("Water Settings")]
    [SerializeField] private float waterFloatForce = 3f;
    [SerializeField] private float waterDragForce = 2f;
    [SerializeField] private float waterRotationSpeed = 1.5f;
    [SerializeField] private float maxWaterRotationAngle = 25f;
    
    private PlayerMovement playerMovement;
    
    void Start()
    {
        // Find the player in the scene
        playerMovement = FindFirstObjectByType<PlayerMovement>();
        
        if (playerMovement == null)
        {
            Debug.LogError("ObjectBehaviorController: No PlayerMovement found in scene!");
        }
        
        // Set up all objects in the scene
        SetupAllObjects();
    }

    void Update()
    {
        
    }
    
    private void SetupAllObjects()
    {
        // Platform objects
        SetupObjectsByTag("OneWayPlatform", typeof(OneWayPlatform));
        SetupObjectsByTag("FallingPlatform", typeof(EnvironmentFallingPlatform));
        
        // Water objects
        SetupObjectsByTag("Water", typeof(Water));
        
        // Environment objects
        SetupObjectsByTag("Door", typeof(EnvironmentDoor));
        SetupObjectsByTag("Switch", typeof(EnvironmentSwitch));
        SetupObjectsByTag("Trigger", typeof(EnvironmentTrigger));
        SetupObjectsByTag("Teleporter", typeof(EnvironmentTeleporter));
        SetupObjectsByTag("Checkpoint", typeof(EnvironmentCheckpoint));
    }
    
    private void SetupObjectsByTag(string tag, System.Type componentType)
    {
        // Check if tag exists first to avoid exceptions
        try
        {
            GameObject[] objects = GameObject.FindGameObjectsWithTag(tag);
        
        foreach (GameObject obj in objects)
        {
            // Add appropriate component if it doesn't exist
            Component component = obj.GetComponent(componentType);
            
            if (component == null)
            {
                component = obj.AddComponent(componentType);
            }
            
            // Initialize the component
            if (component is IObjectBehavior behaviorComponent)
            {
                behaviorComponent.Initialize(this, tag);
            }
            

        }
        }
        catch (UnityException e)
        {
            Debug.LogWarning($"Tag '{tag}' is not defined in Unity's Tag Manager. Skipping setup for {componentType.Name}. Error: {e.Message}");
        }
    }
    
    // PLATFORM SYSTEM
    public void HandleDropThrough(GameObject platform)
    {
        if (playerMovement != null && platform != null)
        {
            StartCoroutine(DisableCollisionTemporarily(platform));
        }
    }
    
    private IEnumerator DisableCollisionTemporarily(GameObject platform)
    {
        BoxCollider2D platformCollider = platform.GetComponent<BoxCollider2D>();
        BoxCollider2D playerCollider = playerMovement.GetComponent<BoxCollider2D>();
        
        if (platformCollider != null && playerCollider != null)
        {
            Physics2D.IgnoreCollision(playerCollider, platformCollider, true);
            yield return new WaitForSeconds(dropThroughTime);
            Physics2D.IgnoreCollision(playerCollider, platformCollider, false);
        }
    }
    
    // WATER SYSTEM
    public void HandleWaterEntry(GameObject player, Collider2D waterCollider = null)
    {
        if (playerMovement != null)
        {
            playerMovement.SetWaterState(true, GetWaterProperties(), waterCollider);
        }
    }
    
    public void HandleWaterExit(GameObject player)
    {
        if (playerMovement != null)
        {
            playerMovement.SetWaterState(false);
        }
    }
    
    public void ApplyWaterPhysics(Rigidbody2D playerRb, bool isMovingHorizontally, bool sinkInput = false)
    {
        if (playerRb == null) return;
        
        // Let PlayerMovement handle all water physics
        // This method is kept for compatibility but does nothing
        // All water physics are now handled in PlayerMovement.cs
    }
    
    public void ApplyWaterRotation(Transform playerTransform, float horizontalInput)
    {
        if (playerTransform == null) return;
        
        // Calculate target rotation based on movement direction with gradual buildup
        float targetRotation = 0f;
        if (Mathf.Abs(horizontalInput) > 0.1f)
        {
            // Gradual tilt that builds up over time
            float inputStrength = Mathf.Clamp01(Mathf.Abs(horizontalInput));
            targetRotation = -horizontalInput * maxWaterRotationAngle * inputStrength;
        }
        
        // Very smooth, gradual rotation
        float currentRotation = playerTransform.eulerAngles.z;
        if (currentRotation > 180f) currentRotation -= 360f;
        
        // Slower, more gradual interpolation
        float rotationSpeed = waterRotationSpeed * Time.deltaTime;
        float newRotation = Mathf.LerpAngle(currentRotation, targetRotation, rotationSpeed);
        playerTransform.rotation = Quaternion.Euler(0, 0, newRotation);
    }
    
    private WaterProperties GetWaterProperties()
    {
        WaterProperties props = new WaterProperties();
        props.gravityModifier = 0.8f; // Allow more sinking
        props.speedModifier = 0.7f;
        props.jumpForceModifier = 0.8f;
        props.buoyancyForce = waterFloatForce;
        props.dragForce = waterDragForce;
        props.waterType = "Swimming";
        props.allowBreathing = true;
        return props;
    }
    
    // ENVIRONMENT SYSTEM
    public void HandleDoorInteraction(GameObject door)
    {
    }
    
    public void HandleSwitchActivation(GameObject switchObj)
    {
        Debug.Log($"Switch activated: {switchObj.name}");
    }
    
    public void HandleTriggerActivation(GameObject trigger)
    {
        Debug.Log($"Trigger activated: {trigger.name}");
    }
    
    public void HandleTeleporter(GameObject teleporter, GameObject destination)
    {
        if (playerMovement != null)
        {
            Debug.Log($"Teleporter used: {teleporter.name} -> {destination?.name ?? "Unknown"}");
        }
    }
    
    public void HandleCheckpoint(GameObject checkpoint)
    {
        if (playerMovement != null)
        {
            Debug.Log($"Checkpoint reached: {checkpoint.name}");
        }
    }
}

// Interface for all object behaviors
public interface IObjectBehavior
{
    void Initialize(ObjectBehaviorController controller, string objectType);
}

// Interface for objects that use default controller values

// WATER OBJECTS
public class Water : MonoBehaviour, IObjectBehavior
{
    private ObjectBehaviorController controller;
    private bool playerInWater = false;
    private GameObject player;
    private Rigidbody2D playerRb;
    private Transform playerTransform;
    private PlayerMovement playerMovement;
    private Collider2D waterCollider;
    
    public void Initialize(ObjectBehaviorController behaviorController, string objectType)
    {
        controller = behaviorController;
        
        // Ensure we have a trigger collider
        waterCollider = GetComponent<Collider2D>();
        if (waterCollider == null)
        {
            BoxCollider2D boxCol = gameObject.AddComponent<BoxCollider2D>();
            boxCol.isTrigger = true;
            waterCollider = boxCol;
        }
        else
        {
            waterCollider.isTrigger = true;
        }
        
        // Find player reference
        player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            playerRb = player.GetComponent<Rigidbody2D>();
            playerTransform = player.transform;
            playerMovement = player.GetComponent<PlayerMovement>();
        }
    }
    
    void Update()
    {
        if (playerInWater && controller != null && playerRb != null && playerMovement != null)
        {
            // Get input for movement and sinking
            float horizontalInput = GetPlayerHorizontalInput();
            bool downInput = GetPlayerDownInput();
            bool isMovingHorizontally = Mathf.Abs(horizontalInput) > 0.1f;
            
            // Apply simple water physics
            controller.ApplyWaterPhysics(playerRb, isMovingHorizontally, downInput);
            
            // Apply rotation when moving
            controller.ApplyWaterRotation(playerTransform, horizontalInput);
        }
    }
    
    private float GetPlayerHorizontalInput()
    {
        // Use Input System directly since we can't access PlayerMovement's private fields
        if (Keyboard.current != null)
        {
            bool leftPressed = Keyboard.current.aKey.isPressed || 
                              Keyboard.current.leftArrowKey.isPressed;
            bool rightPressed = Keyboard.current.dKey.isPressed || 
                               Keyboard.current.rightArrowKey.isPressed;
            
            if (leftPressed) return -1f;
            if (rightPressed) return 1f;
        }
        return 0f;
    }
    
    private bool GetPlayerDownInput()
    {
        if (Keyboard.current != null)
        {
            return Keyboard.current.sKey.isPressed || 
                   Keyboard.current.downArrowKey.isPressed;
        }
        return false;
    }
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInWater = true;
            if (controller != null)
            {
                controller.HandleWaterEntry(other.gameObject, waterCollider);
            }
        }
    }
    
    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInWater = false;
            if (controller != null)
            {
                controller.HandleWaterExit(other.gameObject);
            }
            
            // Reset player rotation when exiting water
            if (playerTransform != null)
            {
                playerTransform.rotation = Quaternion.identity;
            }
        }
    }
}

// PLATFORM OBJECTS
public class OneWayPlatform : MonoBehaviour, IObjectBehavior
{
    private ObjectBehaviorController controller;
    
    public void Initialize(ObjectBehaviorController behaviorController, string objectType)
    {
        controller = behaviorController;
        
        Collider2D col = GetComponent<Collider2D>();
        if (col == null)
        {
            gameObject.AddComponent<BoxCollider2D>();
        }
    }
    
    public void RequestDropThrough()
    {
        if (controller != null)
        {
            controller.HandleDropThrough(gameObject);
        }
    }
}

// HAZARD OBJECTS

// ENVIRONMENT OBJECTS
public class EnvironmentDoor : MonoBehaviour, IObjectBehavior
{
    [Header("Door Settings")]
    public bool locked = false;

    [Header("Door Animation")]
    public float doorOpenDuration = 1f;
    
    private ObjectBehaviorController controller;
    private BoxCollider2D doorCollider;
    private bool isOpen = false;
    private GameObject innerDoorSprite;
    private bool playerInsideDoor = false;
    private Coroutine closeDoorCoroutine;
    
    public void Initialize(ObjectBehaviorController behaviorController, string objectType)
    {
        controller = behaviorController;
        
        doorCollider = GetComponent<BoxCollider2D>();
        if (doorCollider == null)
        {
            doorCollider = gameObject.AddComponent<BoxCollider2D>();
        }
        
        // Make sure it's not a trigger initially (solid door)
        doorCollider.isTrigger = false;
    }
    
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player") && !isOpen && !locked)
        {
            OpenDoor(collision.gameObject);
        }
    }
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInsideDoor = true;
        }
    }
    
    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInsideDoor = false;
            
            // Start close sequence when player leaves
            if (isOpen && closeDoorCoroutine == null)
            {
                closeDoorCoroutine = StartCoroutine(CloseDoorAfterDelay());
            }
        }
    }
    
    private void OpenDoor(GameObject player)
    {
        if (isOpen) return;
        
        isOpen = true;
        
        // Disable collision
        doorCollider.enabled = false;
        
        // Enable trigger detection for player inside door
        doorCollider.isTrigger = true;
        doorCollider.enabled = true;
        
        // Determine which side the player is on
        bool playerOnLeft = player.transform.position.x < transform.position.x;
        
        // Create inner door sprite
        CreateInnerDoorSprite(playerOnLeft);
        
        // Only start closing sequence if player is not inside
        if (!playerInsideDoor)
        {
            closeDoorCoroutine = StartCoroutine(CloseDoorAfterDelay());
        }
        
        // Notify controller
        if (controller != null)
        {
            controller.HandleDoorInteraction(gameObject);
        }
    }
    
    private void CreateInnerDoorSprite(bool playerOnLeft)
    {
        // Create inner door object
        innerDoorSprite = new GameObject("InnerDoorSprite");
        innerDoorSprite.transform.SetParent(transform);
        
        // Add sprite renderer
        SpriteRenderer spriteRenderer = innerDoorSprite.AddComponent<SpriteRenderer>();
        
        // Create a brown rectangle texture
        Texture2D doorTexture = new Texture2D(32, 32); // Make it larger for visibility
        Color brownColor = new Color(0.6f, 0.4f, 0.2f, 1f);
        
        // Fill the entire texture with brown
        for (int x = 0; x < 32; x++)
        {
            for (int y = 0; y < 32; y++)
            {
                doorTexture.SetPixel(x, y, brownColor);
            }
        }
        doorTexture.Apply();
        
        // Create sprite from texture
        Sprite doorSprite = Sprite.Create(doorTexture, new Rect(0, 0, 32, 32), Vector2.one * 0.5f, 32f);
        spriteRenderer.sprite = doorSprite;
        
        // Set sorting layer and order
        spriteRenderer.sortingLayerName = "Background";
        spriteRenderer.sortingOrder = 10;
        
        // Calculate door dimensions - make it much wider (2.5x original size)
        Vector2 doorSize = doorCollider.size;
        float doorWidth = doorSize.x * 2.5f; // Make it 2.5x wider than original
        float doorHeight = doorSize.y;
        
        // Set sorting order based on player approach direction
        int sortingOrder = playerOnLeft ? 1 : 3; // Left = 1, Right = 3
        spriteRenderer.sortingOrder = sortingOrder;
        
        // Position the inner door to start at the player's side (thin, closed door)
        // Use actual door size, not inflated doorWidth
        Vector3 startPosition = playerOnLeft ? 
            new Vector3(-doorSize.x * 0.4f, 0, 0) :   // Player on left -> door starts on left side (near player)
            new Vector3(doorSize.x * 0.4f, 0, 0);     // Player on right -> door starts on right side (near player)
            
        innerDoorSprite.transform.localPosition = startPosition;
        innerDoorSprite.transform.localScale = new Vector3(0.1f, doorHeight, 1f); // Start very thin (closed)
        
        // Animate the door opening by shrinking and positioning based on player side
        // Store initial scale and player direction for animation
        StartCoroutine(AnimateDoorOpen(playerOnLeft, doorWidth, doorHeight));
    }
    
    private System.Collections.IEnumerator AnimateDoorOpen(bool playerOnLeft, float doorWidth, float doorHeight)
    {
        if (innerDoorSprite == null) 
        {
            Debug.LogError("Inner door sprite is null!");
            yield break;
        }
        
        float elapsedTime = 0f;
        float animationDuration = doorOpenDuration * 0.8f; // 80% of total duration for opening
        
        // Start thin (closed) and expand to full width (open)
        Vector3 startScale = new Vector3(0.1f, doorHeight, 1f); // Start very thin (closed door)
        Vector3 targetScale = new Vector3(doorWidth, doorHeight, 1f); // Expand to full width (open)
        
        // Start positioned near the player, expand away from player
        // Use actual door size for positioning within door bounds
        Vector2 actualDoorSize = doorCollider.size;
        Vector3 startPosition = playerOnLeft ? 
            new Vector3(-actualDoorSize.x * 0.4f, 0, 0) :   // Player on left -> door starts on left side (near player)
            new Vector3(actualDoorSize.x * 0.4f, 0, 0);     // Player on right -> door starts on right side (near player)
        Vector3 targetPosition = playerOnLeft ?
            new Vector3(actualDoorSize.x * 0.4f, 0, 0) :    // Player on left -> door expands to right side (away from player)
            new Vector3(-actualDoorSize.x * 0.4f, 0, 0);    // Player on right -> door expands to left side (away from player)
        
        while (elapsedTime < animationDuration && innerDoorSprite != null)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / animationDuration;
            
            // Smooth easing
            float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
            
            // Animate both scale and position
            Vector3 currentScale = Vector3.Lerp(startScale, targetScale, easedProgress);
            Vector3 currentPos = Vector3.Lerp(startPosition, targetPosition, easedProgress);
            
            innerDoorSprite.transform.localScale = currentScale;
            innerDoorSprite.transform.localPosition = currentPos;
            
            yield return null;
        }
        
        if (innerDoorSprite != null)
        {
            innerDoorSprite.transform.localScale = targetScale;
            innerDoorSprite.transform.localPosition = targetPosition;
        }
    }
    
    private System.Collections.IEnumerator CloseDoorAfterDelay()
    {
        yield return new WaitForSeconds(doorOpenDuration);
        
        // Check if player is still inside before closing
        if (playerInsideDoor)
        {
            closeDoorCoroutine = null; // Reset coroutine reference
            yield break; // Exit without closing
        }
        
        // Animate door sliding back before destroying
        if (innerDoorSprite != null)
        {
            yield return StartCoroutine(AnimateDoorReturn());
        }
        
        // Remove inner door sprite
        if (innerDoorSprite != null)
        {
            Destroy(innerDoorSprite);
            innerDoorSprite = null;
        }
        
        // Re-enable solid collision (disable trigger mode)
        doorCollider.isTrigger = false;
        doorCollider.enabled = true;
        isOpen = false;
        closeDoorCoroutine = null;
    }
    
    private System.Collections.IEnumerator AnimateDoorReturn()
    {
        if (innerDoorSprite == null) yield break;
        
        Vector3 currentPosition = innerDoorSprite.transform.localPosition;
        Vector3 currentScale = innerDoorSprite.transform.localScale;
        
        // Return to hinge side and thin scale (door closing)
        Vector2 doorSize = doorCollider.size;
        float doorWidth = doorSize.x * 2.5f;
        float doorHeight = doorSize.y;
        
        // Determine which side to close to based on current position
        // If door is currently on positive X, player was on left (door expanded right)
        // If door is currently on negative X, player was on right (door expanded left)
        bool wasPlayerOnLeft = currentPosition.x > 0; // If positive X, player was on left
        Vector3 targetPosition = wasPlayerOnLeft ? 
            new Vector3(-doorSize.x * 0.4f, 0, 0) :   // Player was on left -> close back to left side (near where player was)
            new Vector3(doorSize.x * 0.4f, 0, 0);     // Player was on right -> close back to right side (near where player was)
        Vector3 targetScale = new Vector3(0.1f, doorHeight, 1f); // Shrink to thin (closed door)
        
        float animationDuration = doorOpenDuration * 0.5f; // Half duration for return
        float elapsedTime = 0f;
        
        while (elapsedTime < animationDuration && innerDoorSprite != null)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / animationDuration;
            
            // Smooth easing for return
            float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
            
            Vector3 newPosition = Vector3.Lerp(currentPosition, targetPosition, easedProgress);
            Vector3 newScale = Vector3.Lerp(currentScale, targetScale, easedProgress);
            
            innerDoorSprite.transform.localPosition = newPosition;
            innerDoorSprite.transform.localScale = newScale;
            
            yield return null;
        }
        
        if (innerDoorSprite != null)
        {
            innerDoorSprite.transform.localPosition = targetPosition;
            innerDoorSprite.transform.localScale = targetScale;
        }
    }
}

public class EnvironmentSwitch : MonoBehaviour, IObjectBehavior
{
    [Header("Switch Settings")]
    public bool isToggle = true;
    public bool isActivated = false;
    
    private ObjectBehaviorController controller;
    
    public void Initialize(ObjectBehaviorController behaviorController, string objectType)
    {
        controller = behaviorController;
        
        Collider2D col = GetComponent<Collider2D>();
        if (col == null)
        {
            BoxCollider2D boxCol = gameObject.AddComponent<BoxCollider2D>();
            boxCol.isTrigger = true;
        }
    }
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && controller != null)
        {
            isActivated = isToggle ? !isActivated : true;
            controller.HandleSwitchActivation(gameObject);
        }
    }
}

public class EnvironmentTrigger : MonoBehaviour, IObjectBehavior
{
    [Header("Trigger Settings")]
    public bool triggerOnce = true;
    private bool hasTriggered = false;
    
    private ObjectBehaviorController controller;
    
    public void Initialize(ObjectBehaviorController behaviorController, string objectType)
    {
        controller = behaviorController;
        
        Collider2D col = GetComponent<Collider2D>();
        if (col == null)
        {
            BoxCollider2D boxCol = gameObject.AddComponent<BoxCollider2D>();
            boxCol.isTrigger = true;
        }
    }
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && controller != null && (!triggerOnce || !hasTriggered))
        {
            hasTriggered = true;
            controller.HandleTriggerActivation(gameObject);
        }
    }
}

public class EnvironmentTeleporter : MonoBehaviour, IObjectBehavior
{
    [Header("Teleporter Settings")]
    public Transform destinationPoint;
    private float lastTeleportTime = -1f;
    
    private ObjectBehaviorController controller;
    
    public void Initialize(ObjectBehaviorController behaviorController, string objectType)
    {
        controller = behaviorController;
        
        Collider2D col = GetComponent<Collider2D>();
        if (col == null)
        {
            BoxCollider2D boxCol = gameObject.AddComponent<BoxCollider2D>();
            boxCol.isTrigger = true;
        }
    }
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && controller != null && Time.time - lastTeleportTime > 1f)
        {
            lastTeleportTime = Time.time;
            controller.HandleTeleporter(gameObject, destinationPoint?.gameObject);
        }
    }
}

public class EnvironmentCheckpoint : MonoBehaviour, IObjectBehavior
{
    [Header("Checkpoint Settings")]
    public bool isActivated = false;
    
    private ObjectBehaviorController controller;
    
    public void Initialize(ObjectBehaviorController behaviorController, string objectType)
    {
        controller = behaviorController;
        
        Collider2D col = GetComponent<Collider2D>();
        if (col == null)
        {
            BoxCollider2D boxCol = gameObject.AddComponent<BoxCollider2D>();
            boxCol.isTrigger = true;
        }
    }
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && controller != null && !isActivated)
        {
            isActivated = true;
            controller.HandleCheckpoint(gameObject);
        }
    }
}

public class EnvironmentFallingPlatform : MonoBehaviour, IObjectBehavior
{
    [Header("Falling Platform Settings")]
    public float fallDelay = 0.1f;           // Time before platform starts falling after player lands (reduced from 0.5f)
    public float fallSpeed = 10f;            // Speed at which platform falls
    public float respawnTime = 5f;           // Time before platform respawns
    public LayerMask groundLayerMask = -1;   // What layers count as ground for the platform to land on
    
    private ObjectBehaviorController controller;
    private BoxCollider2D platformCollider;
    private CompositeCollider2D compositeCollider;
    private SpriteRenderer platformRenderer;
    private Vector3 originalPosition;
    private bool isFalling = false;
    private bool hasPlayerOnTop = false;
    private GameObject fallingCopy;
    private Rigidbody2D platformRigidbody;
    
    public void Initialize(ObjectBehaviorController behaviorController, string objectType)
    {
        if (gameObject == null || transform == null)
        {
            Debug.LogError("FallingPlatform: GameObject or transform is null during initialization!");
            return;
        }
        
        controller = behaviorController;
        originalPosition = transform.position;
        
        // Get or add required components - check for CompositeCollider2D first
        compositeCollider = GetComponent<CompositeCollider2D>();
        platformCollider = GetComponent<BoxCollider2D>();
        
        if (compositeCollider != null)
        {
            // Use CompositeCollider2D if available (for tilemaps)
            compositeCollider.isTrigger = false;
            Debug.Log("FallingPlatform: Using CompositeCollider2D for tilemap");
        }
        else if (platformCollider == null)
        {
            // Add BoxCollider2D only if no composite collider exists
            platformCollider = gameObject.AddComponent<BoxCollider2D>();
            platformCollider.isTrigger = false;
        }
        else
        {
            // Ensure existing BoxCollider2D is NOT a trigger
            platformCollider.isTrigger = false;
        }
        
        // Add a trigger detector on top for backup detection
        GameObject triggerDetector = new GameObject("FallingPlatformTrigger");
        if (triggerDetector != null && transform != null)
        {
            triggerDetector.transform.SetParent(transform);
        }
        else
        {
            Debug.LogError("FallingPlatform: Failed to create trigger detector or transform is null!");
            return;
        }
        
        // Get bounds from composite collider or box collider
        Bounds colliderBounds;
        if (compositeCollider != null)
        {
            colliderBounds = compositeCollider.bounds;
        }
        else if (platformCollider != null)
        {
            colliderBounds = platformCollider.bounds;
        }
        else
        {
            Debug.LogError("FallingPlatform: No valid collider found for bounds calculation!");
            colliderBounds = new Bounds(transform.position, Vector3.one); // Fallback
        }
        
        triggerDetector.transform.localPosition = new Vector3(0, colliderBounds.size.y / 2 + 0.1f, 0);
        
        BoxCollider2D triggerCollider = triggerDetector.AddComponent<BoxCollider2D>();
        triggerCollider.isTrigger = true;
        triggerCollider.size = new Vector2(colliderBounds.size.x, 0.2f);
        
        FallingPlatformTrigger triggerScript = triggerDetector.AddComponent<FallingPlatformTrigger>();
        triggerScript.parentPlatform = this;
        
        // Configure Rigidbody2D for CompositeCollider2D compatibility
        Rigidbody2D existingRigidbody = GetComponent<Rigidbody2D>();
        if (existingRigidbody != null)
        {
            // Keep the rigidbody but make it kinematic (no physics until falling)
            existingRigidbody.bodyType = RigidbodyType2D.Kinematic;
            existingRigidbody.gravityScale = 0f;
            existingRigidbody.freezeRotation = true;
            platformRigidbody = existingRigidbody;
        }
        else
        {
            // Add rigidbody if none exists (required for CompositeCollider2D)
            platformRigidbody = gameObject.AddComponent<Rigidbody2D>();
            platformRigidbody.bodyType = RigidbodyType2D.Kinematic;
            platformRigidbody.gravityScale = 0f;
            platformRigidbody.freezeRotation = true;
        }
        
        platformRenderer = GetComponent<SpriteRenderer>();
        
        // For tilemap platforms, we don't need to add a SpriteRenderer
        // The TilemapRenderer handles the visuals
        if (platformRenderer == null && compositeCollider == null)
        {
            // Only add SpriteRenderer for non-tilemap platforms
            platformRenderer = gameObject.AddComponent<SpriteRenderer>();
            if (platformRenderer != null)
            {
                // Create a simple white square sprite for the platform if none exists
                platformRenderer.sprite = CreateSimpleSprite();
                platformRenderer.color = new Color(0.8f, 0.6f, 0.4f); // Brown platform color
            }
            else
            {
                Debug.LogError("FallingPlatform: Failed to add SpriteRenderer component!");
            }
        }
        else if (compositeCollider != null)
        {
            Debug.Log("FallingPlatform: Tilemap platform detected - skipping SpriteRenderer setup");
        }
    }
    
    private void OnCollisionEnter2D(Collision2D collision)
    {
        Debug.Log($"FallingPlatform collision detected with: {collision.gameObject.name}, Tag: {collision.gameObject.tag}");
        Debug.Log($"FallingPlatform state: isFalling={isFalling}, hasPlayerOnTop={hasPlayerOnTop}");
        
        if (collision.gameObject.CompareTag("Player") && !isFalling && !hasPlayerOnTop)
        {
            // Check if player is landing on top of the platform
            Vector2 contactPoint = collision.contacts[0].point;
            Vector2 playerPosition = collision.gameObject.transform.position;
            
            // Get bounds from the appropriate collider
            Bounds currentBounds;
            if (compositeCollider != null)
            {
                currentBounds = compositeCollider.bounds;
            }
            else if (platformCollider != null)
            {
                currentBounds = platformCollider.bounds;
            }
            else
            {
                Debug.LogError("FallingPlatform: No valid collider found!");
                return;
            }
            
            Vector2 platformTop = new Vector2(transform.position.x, transform.position.y + currentBounds.size.y / 2);
            
            Debug.Log($"Contact point: {contactPoint}, Platform top: {platformTop}, Player pos: {playerPosition}");
            Debug.Log($"Player Y vs Platform Top Y: {playerPosition.y} vs {platformTop.y} (diff: {playerPosition.y - platformTop.y})");
            
            // Use player position instead of contact point for more reliable detection
            if (playerPosition.y >= platformTop.y - 0.5f) // Increased tolerance
            {
                hasPlayerOnTop = true;
                Debug.Log("Player detected on top of falling platform - starting fall sequence");
                StartCoroutine(StartFallingSequence());
            }
            else
            {
                Debug.Log($"Player not on top - contact too low. Required: {platformTop.y - 0.5f}, Got: {playerPosition.y}");
            }
        }
    }
    
    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            Debug.Log("OnCollisionExit2D: Player left platform - setting hasPlayerOnTop to false");
            hasPlayerOnTop = false;
        }
    }
    
    // Public methods for trigger-based detection
    public void TriggerFall()
    {
        if (!isFalling && !hasPlayerOnTop)
        {
            Debug.Log("TriggerFall called - starting fall sequence");
            hasPlayerOnTop = true;
            StartCoroutine(StartFallingSequence());
        }
        else
        {
            Debug.Log($"TriggerFall called but conditions not met: isFalling={isFalling}, hasPlayerOnTop={hasPlayerOnTop}");
        }
    }
    
    public void PlayerLeft()
    {
        hasPlayerOnTop = false;
        Debug.Log("Player left falling platform trigger area");
    }
    
    private IEnumerator StartFallingSequence()
    {
        Debug.Log($"StartFallingSequence: Beginning fall sequence with delay {fallDelay}s");
        
        // Wait for the fall delay
        yield return new WaitForSeconds(fallDelay);
        
        Debug.Log($"StartFallingSequence: After delay - hasPlayerOnTop={hasPlayerOnTop}, isFalling={isFalling}");
        
        // Only proceed if player is still on the platform
        if (hasPlayerOnTop && !isFalling)
        {
            Debug.Log("StartFallingSequence: Conditions met - proceeding with fall");
            isFalling = true;
            Debug.Log("StartFallingSequence: Set isFalling = true");
            
            try
            {
                // Create a copy of the platform that will fall
                Debug.Log("StartFallingSequence: About to call CreateFallingCopy()");
                CreateFallingCopy();
                Debug.Log("StartFallingSequence: CreateFallingCopy() completed");
                
                // Start the respawn timer BEFORE hiding the platform (so coroutine can start)
                Debug.Log("StartFallingSequence: About to start RespawnPlatform coroutine");
                StartCoroutine(RespawnPlatform());
                Debug.Log("StartFallingSequence: RespawnPlatform coroutine started");
                
                // Hide the original platform
                Debug.Log("StartFallingSequence: About to call SetPlatformVisibility(false)");
                SetPlatformVisibility(false);
                Debug.Log("StartFallingSequence: SetPlatformVisibility(false) completed");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"StartFallingSequence: Exception occurred: {e.Message}\n{e.StackTrace}");
            }
        }
        else
        {
            Debug.Log($"StartFallingSequence: Conditions not met - hasPlayerOnTop={hasPlayerOnTop}, isFalling={isFalling}");
        }
    }
    
    private void CreateFallingCopy()
    {
        Debug.Log($"CreateFallingCopy: Creating falling copy at position {transform.position}");
        
        // For tilemap platforms, use the actual tilemap bounds instead of transform position
        Vector3 copyPosition;
        if (compositeCollider != null)
        {
            // Use the center of the tilemap bounds
            Bounds tilemapBounds = compositeCollider.bounds;
            copyPosition = new Vector3(tilemapBounds.center.x, tilemapBounds.center.y - 0.1f, transform.position.z);
            Debug.Log($"CreateFallingCopy: Using tilemap bounds center {tilemapBounds.center} for copy position");
        }
        else
        {
            // Use transform position for sprite-based platforms
            copyPosition = new Vector3(transform.position.x, transform.position.y - 0.1f, transform.position.z);
        }
        
        Debug.Log($"CreateFallingCopy: Final copy position will be {copyPosition}");
        fallingCopy = Instantiate(gameObject, copyPosition, transform.rotation);
        
        Debug.Log($"CreateFallingCopy: Falling copy created at {copyPosition}, GameObject name: {fallingCopy.name}");
        
        // Check what components the copy has
        Component[] components = fallingCopy.GetComponents<Component>();
        Debug.Log($"CreateFallingCopy: Copy has {components.Length} components:");
        foreach (Component comp in components)
        {
            Debug.Log($"  - {comp.GetType().Name}");
        }
        
        // Put the copy on a different layer to avoid raycast issues
        fallingCopy.layer = 0; // Default layer
        Debug.Log($"CreateFallingCopy: Set copy layer to {fallingCopy.layer}");
        
        // Remove the falling platform script from the copy to prevent recursion
        EnvironmentFallingPlatform copyScript = fallingCopy.GetComponent<EnvironmentFallingPlatform>();
        if (copyScript != null)
        {
            Destroy(copyScript);
        }
        
        // Remove the trigger detector from the copy
        FallingPlatformTrigger triggerScript = fallingCopy.GetComponentInChildren<FallingPlatformTrigger>();
        if (triggerScript != null)
        {
            Destroy(triggerScript.gameObject);
        }
        
        // Ensure the copy's colliders are NOT triggers for proper physics
        CompositeCollider2D copyCompositeCollider = fallingCopy.GetComponent<CompositeCollider2D>();
        BoxCollider2D copyCollider = fallingCopy.GetComponent<BoxCollider2D>();
        
        if (copyCompositeCollider != null)
        {
            copyCompositeCollider.isTrigger = false;
        }
        if (copyCollider != null)
        {
            copyCollider.isTrigger = false;
        }
        
        // Configure the copy's Rigidbody2D for physics-based falling
        Rigidbody2D copyRigidbody = fallingCopy.GetComponent<Rigidbody2D>();
        if (copyRigidbody != null)
        {
            Debug.Log($"CreateFallingCopy: Found Rigidbody2D on copy, changing to Dynamic");
            // Change from Kinematic to Dynamic for physics
            copyRigidbody.bodyType = RigidbodyType2D.Dynamic;
            copyRigidbody.gravityScale = 3f; // Increased gravity for faster falling
            copyRigidbody.freezeRotation = true;
            copyRigidbody.linearDamping = 0f; // No air resistance
            copyRigidbody.angularDamping = 0f;
            Debug.Log($"CreateFallingCopy: Rigidbody2D configured - bodyType=Dynamic, gravityScale=3f");
        }
        else
        {
            // Add rigidbody if somehow missing
            copyRigidbody = fallingCopy.AddComponent<Rigidbody2D>();
            copyRigidbody.bodyType = RigidbodyType2D.Dynamic;
            copyRigidbody.gravityScale = 3f;
            copyRigidbody.freezeRotation = true;
            copyRigidbody.linearDamping = 0f;
            copyRigidbody.angularDamping = 0f;
        }
        
        // Ensure the copy is active and visible
        fallingCopy.SetActive(true);
        
        // For tilemap copies, ensure the TilemapRenderer is enabled
        TilemapRenderer copyTilemapRenderer = fallingCopy.GetComponent<TilemapRenderer>();
        if (copyTilemapRenderer != null)
        {
            copyTilemapRenderer.enabled = true;
            Debug.Log("CreateFallingCopy: Enabled TilemapRenderer on copy");
        }
        
        Debug.Log($"CreateFallingCopy: Final copy state - Active: {fallingCopy.activeInHierarchy}, Position: {fallingCopy.transform.position}");
        
        // Start the falling coroutine for the copy
        StartCoroutine(HandleFallingCopy(copyRigidbody));
    }
    
    private IEnumerator HandleFallingCopy(Rigidbody2D copyRigidbody)
    {
        // Wait a brief moment for physics to kick in
        yield return new WaitForSeconds(0.1f);
        
        float fallStartTime = Time.time;
        bool hasLanded = false;
        
        while (!hasLanded && Time.time - fallStartTime < 10f) // Max 10 seconds of falling
        {
            if (fallingCopy == null)
            {
                yield break;
            }
            
            // Only check for landing after the copy has had time to start falling
            if (Time.time - fallStartTime > 0.5f)
            {
                // Check if the copy has hit something below it (exclude the copy itself)
                RaycastHit2D hit = Physics2D.Raycast(
                    fallingCopy.transform.position, 
                    Vector2.down, 
                    0.6f, 
                    groundLayerMask
                );
                
                // Make sure we don't detect the copy itself or the original platform
                if (hit.collider != null && hit.collider.gameObject != fallingCopy && 
                    hit.collider.gameObject != gameObject && copyRigidbody.linearVelocity.y <= 0)
                {
                    hasLanded = true;
                    copyRigidbody.linearVelocity = Vector2.zero;
                    copyRigidbody.bodyType = RigidbodyType2D.Kinematic;
                }
            }
            
            yield return new WaitForFixedUpdate();
        }
        
        // Destroy the copy after 5 seconds
        yield return new WaitForSeconds(5f);
        
        if (fallingCopy != null)
        {
            Destroy(fallingCopy);
        }
    }
    
    private IEnumerator RespawnPlatform()
    {
        yield return new WaitForSeconds(respawnTime);
        
        // Reset platform state
        isFalling = false;
        hasPlayerOnTop = false;
        
        // Show the original platform again
        SetPlatformVisibility(true);
        
        // Reset position in case it somehow moved
        transform.position = originalPosition;
    }
    
    private void SetPlatformVisibility(bool visible)
    {
        Debug.Log($"SetPlatformVisibility: Setting visibility to {visible}");
        
        // Handle sprite-based platforms
        if (platformRenderer != null)
        {
            platformRenderer.enabled = visible;
            Debug.Log($"SetPlatformVisibility: Set SpriteRenderer.enabled = {visible}");
        }
        
        if (platformCollider != null)
        {
            platformCollider.enabled = visible;
            Debug.Log($"SetPlatformVisibility: Set BoxCollider2D.enabled = {visible}");
        }
        
        // Handle tilemap-based platforms
        if (compositeCollider != null)
        {
            compositeCollider.enabled = visible;
            Debug.Log($"SetPlatformVisibility: Set CompositeCollider2D.enabled = {visible}");
        }
        
        // Disable the tilemap renderer for visual hiding
        TilemapRenderer tilemapRenderer = GetComponent<TilemapRenderer>();
        if (tilemapRenderer != null)
        {
            tilemapRenderer.enabled = visible;
            Debug.Log($"SetPlatformVisibility: Set TilemapRenderer.enabled = {visible}");
        }
        
        // Don't deactivate the GameObject completely as that kills coroutines
        // Just disable the visual and collision components
        Debug.Log($"SetPlatformVisibility: Completed visibility change to {visible} without deactivating GameObject");
    }
    
    private Sprite CreateSimpleSprite()
    {
        // Create a simple 32x8 pixel sprite for the platform
        Texture2D texture = new Texture2D(32, 8);
        Color[] pixels = new Color[32 * 8];
        
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.white;
        }
        
        texture.SetPixels(pixels);
        texture.Apply();
        
        return Sprite.Create(texture, new Rect(0, 0, 32, 8), new Vector2(0.5f, 0.5f), 32);
    }
}

// Helper class for falling platform trigger detection
public class FallingPlatformTrigger : MonoBehaviour
{
    public EnvironmentFallingPlatform parentPlatform;
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && parentPlatform != null)
        {
            parentPlatform.TriggerFall();
        }
    }
    
    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player") && parentPlatform != null)
        {
            parentPlatform.PlayerLeft();
        }
    }
}