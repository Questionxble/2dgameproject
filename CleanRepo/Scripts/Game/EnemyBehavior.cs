using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class EnemyBehavior : MonoBehaviour
{
    [Header("Enemy Settings")]
    [SerializeField] private int maxHealth = 50;
    [SerializeField] private float respawnDelay = 3f;
    
    [Header("Health Bar")]
    [SerializeField] private Vector3 healthBarOffset = new Vector3(0, 1.5f, 0); // Offset above enemy
    [SerializeField] private Vector2 healthBarSize = new Vector2(1.5f, 0.2f); // Width and height of health bar
    
    [Header("Death Settings")]
    [SerializeField] private Color deathColor = new Color(0.5f, 0.5f, 0.5f, 0.7f); // Gray and semi-transparent
    [SerializeField] private bool showRespawnTimer = true;
    
    [Header("Aggression Settings")]
    [SerializeField] private bool isAggressive = false; // Enable enemy aggression
    [SerializeField] private float detectionRadius = 5f; // Radius to detect and follow player
    [SerializeField] private float followSpeed = 2f; // Speed when following player
    [SerializeField] private float attackRange = 2f; // Range to attack player
    [SerializeField] private float attackCooldown = 2f; // Time between attacks
    [SerializeField] private float attackDamage = 15f; // Damage dealt by enemy attacks
    [SerializeField] private float attackDuration = 0.8f; // How long attack damage object lasts (increased for longer animations)
    [SerializeField] private Vector2 attackSize = new Vector2(1f, 1f); // Size of attack damage object
    [SerializeField] private float jumpForce = 8f; // Force applied when enemy jumps
    [SerializeField] private float jumpCooldown = 1f; // Time between jumps
    [SerializeField] private float minHeightDifferenceToJump = 1.5f; // Minimum height difference to trigger jump
    
    [Header("Animation Settings")]
    [SerializeField] private bool alternateAttacks = true; // Whether to alternate between attack animations
    
    [Header("Collision")]
    [SerializeField] private LayerMask playerLayerMask = 1; // What layers count as player
    
    // Private variables
    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private Collider2D enemyCollider;
    private Rigidbody2D rb;
    private int currentHealth;
    private bool isDead = false;
    private Vector3 spawnPosition;
    private Color originalColor;
    
    // Aggression System
    private Transform playerTransform;
    private float lastAttackTime = 0f;
    private bool isFacingRight = true;
    private float lastJumpTime = 0f;
    
    // Animation System
    private bool useNextAttackAnimation = false; // For alternating attacks
    private bool isAttacking = false; // To prevent animation conflicts during attacks
    
    // Damage Object Integration
    private bool inDamageZone = false;
    private float lastDamageTime = 0f;
    private DamageObject currentDamageObject;
    
    // Public Properties
    public bool IsDead => isDead;
    
        // Health Bar UI
    private Canvas healthBarCanvas;
    private Image healthBarBackground;
    private Image healthBarFill;
    private GameObject healthBarObject;
    private float targetFillAmount = 1f; // Target fill amount for smooth animation
    private float currentFillAmount = 1f; // Current animated fill amount
    private float healthBarAnimationSpeed = 3f; // Speed of health bar animation
    private Text respawnTimerText; // Timer text for respawn countdown
    
    void Start()
    {
        // Get components
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            
        animator = GetComponent<Animator>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
            
        enemyCollider = GetComponent<Collider2D>();
        rb = GetComponent<Rigidbody2D>();
        
        // Add rigidbody if none exists (needed for movement)
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
            rb.gravityScale = 1f; // Normal gravity
            rb.freezeRotation = true; // Prevent rotation
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous; // Better collision detection
            
            // Create physics material for better ground interaction
            PhysicsMaterial2D enemyMaterial = new PhysicsMaterial2D("EnemyMaterial");
            enemyMaterial.friction = 0.4f; // Some friction to prevent sliding
            enemyMaterial.bounciness = 0f; // No bouncing
            
            // Apply material to collider
            if (enemyCollider != null)
            {
                enemyCollider.sharedMaterial = enemyMaterial;
            }
        }
        
        // Ensure rotation is frozen even if rigidbody already exists
        if (rb != null)
        {
            rb.freezeRotation = true;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }
        
        // Add collider if none exists
        if (enemyCollider == null)
        {
            enemyCollider = gameObject.AddComponent<BoxCollider2D>();
        }
        
        // Find player for aggression system
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
        
        // Set up collision layer - make sure entity doesn't collide with player or other entities
        SetupCollisionLayers();
        
        // Ensure collision ignoring works with any entities that might have spawned after this one
        StartCoroutine(SetupCollisionIgnoringDelayed());
        
        // Initialize health and position
        currentHealth = maxHealth;
        spawnPosition = transform.position;
        
        // Initialize health bar animation values
        targetFillAmount = 1f;
        currentFillAmount = 1f;
        
        // Store original color
        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }
        
        // Create health bar
        CreateHealthBar();
    }
    
    void Update()
    {
        // Early exit if dead to prevent any processing
        if (isDead) 
        {
            // Clear any remaining damage state when dead
            if (inDamageZone)
            {
                inDamageZone = false;
                currentDamageObject = null;
            }
            return;
        }
        
        // Validate essential components before processing
        if (!ValidateComponents()) return;
        
        UpdateHealthBarPosition();
        UpdateHealthBar(); // Animate health bar continuously
        
        // Ensure enemy rotation stays at zero (additional safety)
        if (transform.rotation != Quaternion.identity)
        {
            transform.rotation = Quaternion.identity;
        }
        
        // Handle continuous damage while in damage zone
        if (inDamageZone && currentDamageObject != null)
        {
            try
            {
                // Additional null check for the damage object and its gameObject
                if (currentDamageObject != null && currentDamageObject.gameObject != null && Time.time - lastDamageTime >= currentDamageObject.damageRate)
                {
                    TakeDamage(currentDamageObject.damageAmount);
                    
                    // Exit immediately if we died from this damage
                    if (isDead) return;
                    
                    lastDamageTime = Time.time;
                }
                else if (currentDamageObject == null || currentDamageObject.gameObject == null)
                {
                    // Clean up null damage object reference
                    inDamageZone = false;
                    currentDamageObject = null;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error processing damage for {gameObject.name}: {e.Message}");
                // Clean up on error
                inDamageZone = false;
                currentDamageObject = null;
            }
        }
        
        // Handle aggression behavior
        if (isAggressive && playerTransform != null)
        {
            try
            {
                HandleAggression();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error in HandleAggression for {gameObject.name}: {e.Message}");
            }
        }
        
        // Update animations based on current state
        UpdateAnimations();
    }
    
    private bool ValidateComponents()
    {
        // Re-get essential components if they're null
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }
        
        if (enemyCollider == null)
        {
            enemyCollider = GetComponent<Collider2D>();
        }
        
        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
        }
        
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
        }
        
        return true; // Continue processing even if some components are missing
    }
    
    private void UpdateAnimations()
    {
        if (animator == null) return;
        
        // Update animator parameters based on current state
        animator.SetBool("isDead", isDead);
        animator.SetBool("isAttacking", isAttacking);
        
        if (!isDead && !isAttacking)
        {
            // Check if moving (has horizontal velocity)
            bool isMoving = rb != null && Mathf.Abs(rb.linearVelocity.x) > 0.1f;
            animator.SetBool("isWalking", isMoving);
        }
        else
        {
            animator.SetBool("isWalking", false);
        }
    }
    
    private void PlayAttackAnimation()
    {
        if (animator == null) return;
        
        // Set attacking flag to prevent animation interruption
        isAttacking = true;
        
        if (alternateAttacks)
        {
            // Set attack type parameter for alternating attacks
            int attackType = useNextAttackAnimation ? 1 : 0;
            animator.SetInteger("attackType", attackType);
            useNextAttackAnimation = !useNextAttackAnimation;
        }
        else
        {
            // Always use attack type 0 (Attack1)
            animator.SetInteger("attackType", 0);
        }
        
        // The isAttacking parameter will trigger the appropriate attack transition
        animator.SetBool("isAttacking", true);
        
        // Clear attacking flag after attack duration
        StartCoroutine(ClearAttackingFlag());
    }
    
    private IEnumerator ClearAttackingFlag()
    {
        // Wait for attack duration plus a small buffer
        yield return new WaitForSeconds(attackDuration + 0.1f);
        isAttacking = false;
        
        // Clear the attacking parameter in the animator
        if (animator != null)
        {
            animator.SetBool("isAttacking", false);
        }
    }
    
    // Damage Object Integration - Trigger Events
    void OnTriggerEnter2D(Collider2D other)
    {
        if (isDead) return; // Don't process triggers when dead
        
        DamageObject damageObj = other.GetComponent<DamageObject>();
        if (damageObj != null)
        {
            // Check if this damage object can actually damage enemies
            if (!damageObj.canDamageEnemies)
            {
                return; // Don't enter damage zone for objects that can't damage enemies
            }
            
            // Additional check: don't damage ourselves if we're tagged as Enemy
            if (gameObject.CompareTag("Enemy") && !damageObj.canDamageEnemies)
            {
                return;
            }
            inDamageZone = true;
            currentDamageObject = damageObj;
            lastDamageTime = 0f; // Reset timer to cause immediate damage
        }
    }
    
    void OnTriggerExit2D(Collider2D other)
    {
        if (isDead) return; // Don't process triggers when dead
        
        DamageObject damageObj = other.GetComponent<DamageObject>();
        if (damageObj != null && currentDamageObject == damageObj)
        {
            inDamageZone = false;
            currentDamageObject = null;
        }
    }
    
    // Damage Object Integration - Collision Events
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (isDead) return; // Don't process collisions when dead
        
        DamageObject damageObj = collision.gameObject.GetComponent<DamageObject>();
        if (damageObj != null)
        {
            // Check if this damage object can actually damage enemies
            if (!damageObj.canDamageEnemies)
            {
                Debug.Log($"Enemy {gameObject.name} ignoring collision damage object that can't damage enemies");
                return; // Don't enter damage zone for objects that can't damage enemies
            }
            
            // Additional check: don't damage ourselves if we're tagged as Enemy
            if (gameObject.CompareTag("Enemy") && !damageObj.canDamageEnemies)
            {
                Debug.Log($"Enemy {gameObject.name} ignoring collision damage object due to Enemy tag");
                return;
            }
            
            Debug.Log($"Enemy {gameObject.name} entering damage zone from collision with {collision.gameObject.name}");
            inDamageZone = true;
            currentDamageObject = damageObj;
            lastDamageTime = 0f; // Reset timer to cause immediate damage
        }
    }
    
    void OnCollisionExit2D(Collision2D collision)
    {
        if (isDead) return; // Don't process collisions when dead
        
        DamageObject damageObj = collision.gameObject.GetComponent<DamageObject>();
        if (damageObj != null && currentDamageObject == damageObj)
        {
            inDamageZone = false;
            currentDamageObject = null;
        }
    }
    
    private void HandleAggression()
    {
        if (rb == null) return;
        
        // Find the nearest target (player or dummy)
        Transform nearestTarget = FindNearestTarget();
        if (nearestTarget == null) return;
        
        float distanceToTarget = Vector3.Distance(transform.position, nearestTarget.position);
        
        // Check if target is within detection radius
        bool targetInRange = distanceToTarget <= detectionRadius;
        
        if (targetInRange)
        {
            // Follow target if not in attack range
            if (distanceToTarget > attackRange)
            {
                FollowTarget(nearestTarget);
            }
            else
            {
                // Stop moving when in attack range
                if (rb != null)
                {
                    rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
                }
                
                // Try to attack if cooldown is ready
                if (Time.time - lastAttackTime >= attackCooldown)
                {
                    AttackTarget(nearestTarget);
                }
            }
            
            // Face the target
            FaceTarget(nearestTarget);
        }
        else
        {
            // Stop moving when player is out of range
            if (rb != null)
            {
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            }
        }
    }

    private Transform FindNearestTarget()
    {
        Transform nearestTarget = null;
        float nearestDistance = float.MaxValue;
        
        // Check player first
        if (playerTransform != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
            if (distanceToPlayer < nearestDistance)
            {
                nearestDistance = distanceToPlayer;
                nearestTarget = playerTransform;
            }
        }
        
        // Check for nearby attack dummies
        GameObject[] dummies = GameObject.FindGameObjectsWithTag("PlayerSummon");
        foreach (GameObject dummy in dummies)
        {
            if (dummy != null)
            {
                float distanceToDummy = Vector3.Distance(transform.position, dummy.transform.position);
                if (distanceToDummy < nearestDistance)
                {
                    nearestDistance = distanceToDummy;
                    nearestTarget = dummy.transform;
                }
            }
        }
        
        return nearestTarget;
    }

    private void FollowTarget(Transform target)
    {
        if (rb == null || target == null) return;
        
        Vector3 direction = (target.position - transform.position).normalized;
        
        // Check if target is significantly above enemy and we should jump
        float heightDifference = target.position.y - transform.position.y;
        bool shouldJump = heightDifference > minHeightDifferenceToJump && 
                         Mathf.Abs(direction.x) > 0.1f &&
                         rb.linearVelocity.y > -0.1f; // Only jump if not already falling
        
        // Apply horizontal movement
        rb.linearVelocity = new Vector2(direction.x * followSpeed, rb.linearVelocity.y);
        
        // Apply jump if needed
        if (shouldJump && Time.time - lastJumpTime >= jumpCooldown)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            lastJumpTime = Time.time;
        }
    }

    private void AttackTarget(Transform target)
    {
        if (target == null) return;
        
        // Face the target before attacking
        FaceTarget(target);
        
        // Play attack animation
        PlayAttackAnimation();
        
        // Check if target is a dummy
        AttackDummy dummy = target.GetComponent<AttackDummy>();
        if (dummy != null)
        {
            dummy.TakeDamage(attackDamage);
            lastAttackTime = Time.time;
            Debug.Log($"Enemy attacked dummy for {attackDamage} damage");
            return;
        }
        
        // Check if target is the player
        PlayerMovement playerMovement = target.GetComponent<PlayerMovement>();
        if (playerMovement != null)
        {
            playerMovement.TakeDamageFromObject((int)attackDamage);
            lastAttackTime = Time.time;
            Debug.Log($"Enemy attacked player for {attackDamage} damage");
            return;
        }
        
        lastAttackTime = Time.time; // Prevent spam even if target wasn't valid
    }
    
    private void FollowPlayer()
    {
        if (rb == null || playerTransform == null) return;
        
        Vector3 direction = (playerTransform.position - transform.position).normalized;
        
        // Check if player is significantly above enemy and we should jump
        float heightDifference = playerTransform.position.y - transform.position.y;
        bool shouldJump = heightDifference > minHeightDifferenceToJump && 
                         Time.time - lastJumpTime >= jumpCooldown &&
                         Mathf.Abs(rb.linearVelocity.y) < 0.1f; // Only jump when grounded (not already moving vertically)
        
        if (shouldJump)
        {
            // Apply jump force
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            lastJumpTime = Time.time;
        }
        
        // Move towards player (horizontal movement)
        float horizontalMovement = direction.x * followSpeed;
        rb.linearVelocity = new Vector2(horizontalMovement, rb.linearVelocity.y);
    }
    
    private void FaceTarget(Transform target)
    {
        if (spriteRenderer == null || target == null) return;
        
        bool shouldFaceRight = target.position.x > transform.position.x;
        
        if (shouldFaceRight != isFacingRight)
        {
            isFacingRight = shouldFaceRight;
            spriteRenderer.flipX = !isFacingRight; // Flip sprite based on direction
        }
    }
    
    private void AttackPlayer()
    {
        if (playerTransform == null) return;
        
        lastAttackTime = Time.time;
        
        // Create attack damage object in direction of player
        Vector3 attackDirection = (playerTransform.position - transform.position).normalized;
        Vector3 attackPosition = transform.position + attackDirection * (attackRange * 0.7f); // Slightly in front of enemy
        
        StartCoroutine(CreateAttackDamageObject(attackPosition));
    }
    
    private IEnumerator CreateAttackDamageObject(Vector3 position)
    {
        // Create temporary attack damage object
        GameObject attack = new GameObject($"{gameObject.name}_Attack");
        attack.transform.position = position;
        
        // Add collider for damage detection
        BoxCollider2D attackCollider = attack.AddComponent<BoxCollider2D>();
        attackCollider.size = attackSize;
        attackCollider.isTrigger = true;
        
        // Add damage object component
        DamageObject damageComponent = attack.AddComponent<DamageObject>();
        damageComponent.damageAmount = (int)attackDamage;
        damageComponent.damageRate = 0.1f; // Fast damage rate
        
        // Set that this should NOT damage enemies (only player)
        damageComponent.canDamageEnemies = false;
        
        // Also exclude Enemy layer and NPC layer to be extra safe
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        int npcLayer = LayerMask.NameToLayer("NPC");
        LayerMask excludeMask = 0;
        
        if (enemyLayer != -1) excludeMask |= (1 << enemyLayer);
        if (npcLayer != -1) excludeMask |= (1 << npcLayer);
        
        damageComponent.excludeLayers = excludeMask;
        
        // Visual indicator (temporary - will be replaced with graphics later)
        SpriteRenderer attackRenderer = attack.AddComponent<SpriteRenderer>();
        
        // Create red attack sprite
        Texture2D attackTexture = new Texture2D(32, 32);
        Color[] pixels = new Color[32 * 32];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = new Color(1f, 0.3f, 0f, 0.7f); // Orange-red color
        }
        attackTexture.SetPixels(pixels);
        attackTexture.Apply();
        
        attackRenderer.sprite = Sprite.Create(attackTexture, new Rect(0, 0, 32, 32), Vector2.one * 0.5f);
        attackRenderer.sortingOrder = 8;
        
        // Wait for attack duration
        yield return new WaitForSeconds(attackDuration);
        
        // Destroy attack object
        Destroy(attack);
    }
    
    private void SetupCollisionLayers()
    {
        // Set this GameObject to Entities layer
        gameObject.layer = LayerMask.NameToLayer("Entities");
        
        // Find player and ignore collision
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            Collider2D playerCollider = player.GetComponent<Collider2D>();
            if (playerCollider != null && enemyCollider != null)
            {
                Physics2D.IgnoreCollision(enemyCollider, playerCollider, true);
            }
        }
        
        // Make entities ignore collisions with each other
        SetupEntityToEntityCollisionIgnoring();
    }
    
    private void SetupEntityToEntityCollisionIgnoring()
    {
        // Find all other entities (enemies/NPCs/dummies) and ignore collisions with them
        EnemyBehavior[] allEntities = FindObjectsByType<EnemyBehavior>(FindObjectsSortMode.None);
        
        foreach (EnemyBehavior otherEntity in allEntities)
        {
            if (otherEntity != this && otherEntity.enemyCollider != null && enemyCollider != null)
            {
                Physics2D.IgnoreCollision(enemyCollider, otherEntity.enemyCollider, true);
            }
        }
    }
    
    public void TakeDamage(int damage)
    {
        if (isDead) return;
        
        currentHealth -= damage;
        currentHealth = Mathf.Max(0, currentHealth);
        
        // Update health bar display
        UpdateHealthBar();
        
        if (currentHealth <= 0)
        {
            // Immediately clear damage zone state before dying to prevent Update conflicts
            inDamageZone = false;
            currentDamageObject = null;
            Die();
        }
    }
    
    private void Die()
    {
        if (isDead) return;
        
        isDead = true;
        Debug.Log($"Enemy {gameObject.name} died");
        
        // Stop all movement when dead
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
        
        // Clear any attack state and set death parameter
        isAttacking = false;
        if (animator != null)
        {
            animator.SetBool("isAttacking", false);
            animator.SetBool("isDead", true);
        }
        
        // Change sprite color to death color and keep it visible
        if (spriteRenderer != null)
        {
            spriteRenderer.color = deathColor;
        }
        
        // Keep collider enabled for ground collision - damage is prevented by isDead checks
        // No need to disable collider as all damage methods check isDead status
        
        // Clean up all damage-related state
        inDamageZone = false;
        currentDamageObject = null;
        lastDamageTime = 0f;
        
        // Reset aggression state
        lastAttackTime = 0f;
        
        // Update health bar to show death state
        UpdateHealthBar();
        
        // Start respawn countdown
        StartCoroutine(RespawnCountdown());
    }
    
    private IEnumerator RespawnCountdown()
    {
        float timeRemaining = respawnDelay;
        
        while (timeRemaining > 0)
        {
            if (showRespawnTimer && respawnTimerText != null)
            {
                respawnTimerText.text = $"Respawning in: {timeRemaining:F1}s";
                respawnTimerText.gameObject.SetActive(true);
            }
            
            timeRemaining -= Time.deltaTime;
            yield return null;
        }
        
        Respawn();
    }
    
    private void Respawn()
    {
        Debug.Log($"Enemy {gameObject.name} respawning");
        
        // Reset position to spawn point
        transform.position = spawnPosition;
        
        // Reset health
        currentHealth = maxHealth;
        isDead = false;
        
        // Reset animation state
        isAttacking = false;
        useNextAttackAnimation = false;
        if (animator != null)
        {
            animator.SetBool("isDead", false);
            animator.SetBool("isAttacking", false);
            animator.SetBool("isWalking", false);
            animator.SetInteger("attackType", 0);
        }
        
        // Reset health bar animation values
        targetFillAmount = 1f;
        currentFillAmount = 1f;
        
        // Ensure rigidbody is still valid and reset its velocity
        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
        }
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
        
        // Restore original sprite color
        if (spriteRenderer != null)
        {
            spriteRenderer.color = originalColor;
        }
        
        // Collider stays enabled throughout, no need to re-enable
        
        // Hide respawn timer
        if (respawnTimerText != null)
        {
            respawnTimerText.gameObject.SetActive(false);
        }
        
        // Reset aggression state
        lastAttackTime = 0f;
        
        // Update health bar
        UpdateHealthBar();
        
        // Re-setup collision layers (in case something changed)
        SetupCollisionLayers();
        
        // Re-find player if reference was lost
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
        }
    }
    
    private void CreateHealthBar()
    {
        // Create a world space canvas for the health bar
        GameObject canvasGO = new GameObject($"{gameObject.name}_HealthBarCanvas");
        healthBarCanvas = canvasGO.AddComponent<Canvas>();
        healthBarCanvas.renderMode = RenderMode.WorldSpace;
        healthBarCanvas.sortingOrder = 10; // Ensure it renders on top
        
        // Set canvas size and position
        RectTransform canvasRect = healthBarCanvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(healthBarSize.x * 100, healthBarSize.y * 100); // Scale up for world space
        canvasRect.localScale = Vector3.one * 0.01f; // Scale down to appropriate size
        
        // Background
        GameObject backgroundGO = new GameObject("HealthBarBackground");
        backgroundGO.transform.SetParent(canvasGO.transform, false);
        
        healthBarBackground = backgroundGO.AddComponent<Image>();
        healthBarBackground.color = new Color(0.2f, 0.2f, 0.2f, 0.8f); // Dark background
        
        RectTransform backgroundRect = backgroundGO.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.sizeDelta = Vector2.zero;
        
        // Health fill
        GameObject fillGO = new GameObject("HealthBarFill");
        fillGO.transform.SetParent(canvasGO.transform, false);
        
        healthBarFill = fillGO.AddComponent<Image>();
        
        // Create a white sprite for the fill (same as player)
        Texture2D fillTexture = new Texture2D(1, 1);
        fillTexture.SetPixel(0, 0, Color.white);
        fillTexture.Apply();
        healthBarFill.sprite = Sprite.Create(fillTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f);
        
        healthBarFill.color = new Color(0f, 1f, 0f, 0.9f); // Green fill
        healthBarFill.type = Image.Type.Filled;
        healthBarFill.fillMethod = Image.FillMethod.Horizontal;
        healthBarFill.fillOrigin = (int)Image.OriginHorizontal.Left; // Fill from left to right, empty from right to left
        
        RectTransform fillRect = fillGO.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.sizeDelta = Vector2.zero;
        fillRect.anchoredPosition = Vector2.zero;
        
        // Respawn timer text
        GameObject timerGO = new GameObject("RespawnTimer");
        timerGO.transform.SetParent(canvasGO.transform, false);
        
        respawnTimerText = timerGO.AddComponent<Text>();
        respawnTimerText.text = "";
        respawnTimerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        respawnTimerText.fontSize = 14;
        respawnTimerText.color = Color.white;
        respawnTimerText.alignment = TextAnchor.MiddleCenter;
        
        RectTransform timerRect = timerGO.GetComponent<RectTransform>();
        timerRect.anchorMin = Vector2.zero;
        timerRect.anchorMax = Vector2.one;
        timerRect.sizeDelta = new Vector2(0, 30); // Taller for text
        timerRect.anchoredPosition = new Vector2(0, -20); // Below health bar
        
        respawnTimerText.gameObject.SetActive(false); // Hidden by default
        
        // Update initial health display
        UpdateHealthBar();
    }
    
    private void UpdateHealthBarPosition()
    {
        if (healthBarCanvas != null)
        {
            Vector3 healthBarPosition = transform.position + healthBarOffset;
            healthBarCanvas.transform.position = healthBarPosition;
            
            // Make health bar face the camera
            if (Camera.main != null)
            {
                healthBarCanvas.transform.LookAt(Camera.main.transform);
                healthBarCanvas.transform.Rotate(0, 180, 0); // Flip to face camera correctly
            }
        }
    }
    
    private void UpdateHealthBar()
    {
        if (healthBarFill != null)
        {
            float healthPercentage = isDead ? 0f : (float)currentHealth / maxHealth;
            
            // Set target fill amount for smooth animation
            targetFillAmount = healthPercentage;
            
            // Animate the fill amount smoothly using Lerp
            currentFillAmount = Mathf.Lerp(currentFillAmount, targetFillAmount, healthBarAnimationSpeed * Time.deltaTime);
            healthBarFill.fillAmount = currentFillAmount;
            
            // Update fill color based on health percentage (use target for immediate color response)
            if (isDead)
            {
                healthBarFill.color = new Color(0.3f, 0.3f, 0.3f, 0.9f); // Dark gray when dead
            }
            else if (targetFillAmount > 0.6f)
            {
                healthBarFill.color = new Color(0f, 1f, 0f, 0.9f); // Green
            }
            else if (targetFillAmount > 0.3f)
            {
                healthBarFill.color = new Color(1f, 1f, 0f, 0.9f); // Yellow
            }
            else
            {
                healthBarFill.color = new Color(1f, 0f, 0f, 0.9f); // Red
            }
        }
    }
    
    private IEnumerator SetupCollisionIgnoringDelayed()
    {
        // Wait a short time for other entities to potentially spawn
        yield return new WaitForSeconds(0.1f);
        
        // Re-run collision ignoring setup to catch any newly spawned entities
        SetupEntityToEntityCollisionIgnoring();
    }
    
    // Public method to update collision ignoring when new entities are spawned
    public static void UpdateAllEntityCollisions()
    {
        EnemyBehavior[] allEntities = FindObjectsByType<EnemyBehavior>(FindObjectsSortMode.None);
        foreach (EnemyBehavior entity in allEntities)
        {
            entity.SetupEntityToEntityCollisionIgnoring();
        }
    }
    
    void OnDestroy()
    {
        // Clean up health bar when enemy is destroyed
        if (healthBarCanvas != null)
        {
            Destroy(healthBarCanvas.gameObject);
        }
    }
}