using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Tilemaps;

public class DragonBoss : MonoBehaviour
{
    [Header("Dragon Boss Settings")]
    [SerializeField] private int maxHealth = 300;
    [SerializeField] private float respawnDelay = 10f;
    
    [Header("Health Bar")]
    [SerializeField] private Vector3 healthBarOffset = new Vector3(0, 2f, 0); // Offset above dragon
    [SerializeField] private Vector2 healthBarSize = new Vector2(3f, 0.4f); // Larger health bar for boss
    
    [Header("Death Settings")]
    [SerializeField] private Color deathColor = new Color(0.3f, 0.3f, 0.3f, 0.8f); // Darker death color
    [SerializeField] private bool showRespawnTimer = true;
    
    [Header("Basic Movement & Detection")]
    [SerializeField] private float detectionRadius = 8f; // Larger detection radius for boss
    [SerializeField] private float followSpeed = 1.5f; // Slower movement for intimidating boss
    [SerializeField] private LayerMask playerLayerMask = 1; // What layers count as player
    
    [Header("Attack 1: Melee Claw Swipe")]
    [SerializeField] private float meleeAttackRange = 2.5f; // Range for claw swipe
    [SerializeField] private float meleeAttackCooldown = 3f; // Cooldown between melee attacks
    [SerializeField] private float meleeAttackDamage = 25f; // Damage from claw swipe
    [SerializeField] private float meleeAttackDuration = 2.0f; // Duration of claw attack animation (increased for visibility)
    [SerializeField] private Vector2 meleeAttackSize = new Vector2(2f, 1.5f); // Size of claw attack hitbox
    
    [Header("Attack 2: Fire Breath")]
    [SerializeField] private float fireBreathRange = 6f; // Maximum range for fire breath
    [SerializeField] private float fireBreathMinRange = 2f; // Minimum range to use fire breath
    [SerializeField] private float fireBreathCooldown = 5f; // Cooldown for fire breath
    [SerializeField] private float fireBreathDamage = 20f; // Damage per fire block
    [SerializeField] private float fireBreathDuration = 2f; // Duration dragon remains stationary
    [SerializeField] private float fireBlockFrontOffset = 1f; // Distance in front of dragon to start fire blocks
    [SerializeField] private float fireBlockSpawnDelay = 0.3f; // Time between fire block spawns
    [SerializeField] private Vector2 fireBlockSize = new Vector2(1.5f, 1.5f); // Size of each fire damage block
    [SerializeField] private float fireBlockLifetime = 5f; // How long fire blocks last (increased for visibility)
    [SerializeField] private int numberOfFireBlocks = 3; // Number of fire blocks to spawn
    
    [Header("Attack 3: Flying Fire Attack")]
    [SerializeField] private float flyingAttackCooldown = 8f; // Cooldown for flying attack
    [SerializeField] private float flyingAttackDamage = 30f; // Damage from flying attack
    [SerializeField] private float flyHeight = 5f; // How high dragon flies
    [SerializeField] private float flySpeed = 4f; // Speed when flying
    [SerializeField] private float flyOverDistance = 15f; // Distance to fly past player
    [SerializeField] private int flyOverCount = 1; // How many times to fly over player
    [SerializeField] private float flyAttackWidth = 2f; // Width of damage area while flying
    [SerializeField] private Vector2 flyingAttackSize = new Vector2(3f, 2f); // Size of flying attack hitbox
    [SerializeField] private float flyingAttackDepth = 3f; // Distance below dragon to position damage zone
    [SerializeField] private float flyingAttackForwardOffset = 1.5f; // Distance forward in facing direction to position damage zone
    [SerializeField] private float playerDetectionRange = 5f; // Horizontal range for auto-attack detection during flight
    [SerializeField] private float risingDelay = 0.3f; // Delay before starting rising motion
    
    [Header("Fire Particle Effects")]
    [SerializeField] private GameObject fireParticleEmitterPrefab; // Particle emitter to spawn on fire attacks
    [SerializeField] private float fireParticleDuration = 5f; // Duration fire particles last (matches burning effect)
    [SerializeField] private float particleSpawnCooldown = 1f; // Minimum time between particle spawns to prevent spam
    [SerializeField] private LayerMask surfaceLayerMask = -1; // Layers to spawn particles on (All layers by default, will filter out unwanted ones)
    
    [Header("Jump/Landing Settings")]
    [SerializeField] private float jumpForce = 8f; // Force applied when dragon jumps
    [SerializeField] private float jumpCooldown = 2f; // Time between jumps
    [SerializeField] private float minHeightDifferenceToJump = 1.5f; // Minimum height difference to trigger jump
    [SerializeField] private float jumpToLandingDelay = 1.5f; // Time from jump start to landing phase
    
    // Private variables
    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private Collider2D dragonCollider;
    private Rigidbody2D rb;
    private int currentHealth;
    private bool isDead = false;
    private Vector3 spawnPosition;
    private Color originalColor;
    
    // Health-based buff system
    private bool halfHealthBuffActivated = false;
    private bool quarterHealthBuffActivated = false;
    private int baseFlyOverCount; // Store the original flyOverCount
    
    // Player tracking
    private Transform playerTransform;
    private bool isFacingRight = true;
    
    // Attack system
    private float lastMeleeAttackTime = 0f;
    private float lastFireBreathTime = 0f;
    private float lastFlyingAttackTime = 0f;
    private float lastJumpTime = 0f;
    private bool isAttacking = false;
    private bool isGrounded = true;
    private bool isRising = false;
    
    // Fire particle system
    // Fire particle tracking for AOE attacks
    private float lastParticleSpawnTime = 0f;
    private Dictionary<Vector3, float> recentParticlePositions = new Dictionary<Vector3, float>();
    private Dictionary<Transform, float> targetBurnCooldowns = new Dictionary<Transform, float>(); // Per-target cooldown
    private bool isLanding = false;
    
    // Health Bar UI
    private Canvas healthBarCanvas;
    private Image healthBarBackground;
    private Image healthBarFill;
    private GameObject healthBarObject;
    private float targetFillAmount = 1f;
    private float currentFillAmount = 1f;
    private float healthBarAnimationSpeed = 3f;
    private Text respawnTimerText;
    
    // Damage Object Integration
    private bool inDamageZone = false;
    private float lastDamageTime = 0f;
    private DamageObject currentDamageObject;
    
    // Public Properties
    public bool IsDead => isDead;
    
    void Start()
    {
        InitializeComponents();
        SetupPhysics();
        FindPlayer();
        SetupCollisionLayers();
        InitializeHealth();
        CreateHealthBar();
        
        // Store original flyover count for health-based buffs
        baseFlyOverCount = flyOverCount;
        
        // Start periodic cleanup for burn cooldowns
        InvokeRepeating(nameof(CleanupExpiredBurnCooldowns), 5f, 5f);
        
        // Dragon initialization complete
    }
    
    void Update()
    {
        if (isDead)
        {
            HandleDeathState();
            return;
        }
        
        if (!ValidateComponents()) return;
        
        UpdateHealthBarPosition();
        UpdateHealthBar();
        
        // Handle continuous damage while in damage zone
        HandleDamageZone();
        
        // Main AI behavior
        if (!isAttacking)
        {
            HandleDragonAI();
        }
        else
        {
            // Ensure dragon stays completely still during attacks (especially fire breath)
            if (rb != null && isGrounded)
            {
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y); // Keep Y velocity for gravity, stop X movement
            }
        }
        
        // Update animator parameters
        UpdateAnimatorParameters();
    }
    
    private void InitializeComponents()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            
        animator = GetComponent<Animator>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
            
        dragonCollider = GetComponent<Collider2D>();
        rb = GetComponent<Rigidbody2D>();
        
        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }
    }
    
    private void SetupPhysics()
    {
        // Add rigidbody if none exists
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
            rb.gravityScale = 1f;
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }
        
        // Ensure rotation is frozen
        if (rb != null)
        {
            rb.freezeRotation = true;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }
        
        // Add collider if none exists
        if (dragonCollider == null)
        {
            dragonCollider = gameObject.AddComponent<BoxCollider2D>();
        }
    }
    
    private void FindPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
    }
    
    private void SetupCollisionLayers()
    {
        // Set dragon to Entities layer (where all enemies belong)
        gameObject.layer = LayerMask.NameToLayer("Entities");
        gameObject.tag = "Enemy";
        
        // Setup collision rules according to your layer system:
        // - Entities cannot collide with Entities, Player, or NPC
        int entitiesLayer = LayerMask.NameToLayer("Entities");
        int playerLayer = LayerMask.NameToLayer("Player");
        int npcLayer = LayerMask.NameToLayer("NPC");
        
        if (entitiesLayer != -1)
        {
            // Entities don't collide with other Entities
            Physics2D.IgnoreLayerCollision(entitiesLayer, entitiesLayer, true);
            
            // Entities don't collide with Player
            if (playerLayer != -1)
            {
                Physics2D.IgnoreLayerCollision(entitiesLayer, playerLayer, true);
            }
            
            // Entities don't collide with NPCs
            if (npcLayer != -1)
            {
                Physics2D.IgnoreLayerCollision(entitiesLayer, npcLayer, true);
            }
        }
        
        Debug.Log($"DragonBoss setup: Layer={LayerMask.LayerToName(gameObject.layer)} ({gameObject.layer}), Tag={gameObject.tag}");
    }
    
    private void InitializeHealth()
    {
        currentHealth = maxHealth;
        spawnPosition = transform.position;
        targetFillAmount = 1f;
        currentFillAmount = 1f;
    }
    
    private void CreateHealthBar()
    {
        // Create a canvas for the health bar
        GameObject healthBarCanvasObject = new GameObject("DragonBossHealthBarCanvas");
        healthBarCanvas = healthBarCanvasObject.AddComponent<Canvas>();
        healthBarCanvas.renderMode = RenderMode.WorldSpace;
        healthBarCanvas.worldCamera = Camera.main;
        healthBarCanvas.sortingLayerName = "UI";
        healthBarCanvas.sortingOrder = 100;
        
        // Scale the canvas
        RectTransform canvasRect = healthBarCanvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = healthBarSize;
        canvasRect.localScale = Vector3.one * 0.01f; // Scale down for world space
        
        // Create background
        healthBarObject = new GameObject("DragonBossHealthBar");
        healthBarObject.transform.SetParent(healthBarCanvas.transform, false);
        
        healthBarBackground = healthBarObject.AddComponent<Image>();
        healthBarBackground.color = new Color(0.2f, 0.2f, 0.2f, 0.8f); // Dark background
        
        RectTransform backgroundRect = healthBarObject.GetComponent<RectTransform>();
        backgroundRect.sizeDelta = healthBarSize * 100f; // Scale up for canvas
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;
        
        // Create fill
        GameObject fillObject = new GameObject("DragonBossHealthBarFill");
        fillObject.transform.SetParent(healthBarObject.transform, false);
        
        healthBarFill = fillObject.AddComponent<Image>();
        healthBarFill.color = new Color(0.8f, 0.2f, 0.2f, 1f); // Boss red color
        healthBarFill.type = Image.Type.Filled;
        healthBarFill.fillMethod = Image.FillMethod.Horizontal;
        
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.sizeDelta = healthBarSize * 100f;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        
        // Create respawn timer text
        if (showRespawnTimer)
        {
            GameObject timerObject = new GameObject("DragonBossRespawnTimer");
            timerObject.transform.SetParent(healthBarCanvas.transform, false);
            
            respawnTimerText = timerObject.AddComponent<Text>();
            respawnTimerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            respawnTimerText.text = "";
            respawnTimerText.fontSize = 20;
            respawnTimerText.color = Color.white;
            respawnTimerText.alignment = TextAnchor.MiddleCenter;
            
            RectTransform timerRect = timerObject.GetComponent<RectTransform>();
            timerRect.sizeDelta = new Vector2(200f, 30f);
            timerRect.anchoredPosition = new Vector2(0, -40f);
        }
    }
    
    private bool ValidateComponents()
    {
        return spriteRenderer != null && rb != null && dragonCollider != null;
    }
    
    private void HandleDeathState()
    {
        // Ensure dragon stays completely immobile during death
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
        
        if (inDamageZone)
        {
            inDamageZone = false;
            currentDamageObject = null;
        }
    }
    
    private void UpdateHealthBarPosition()
    {
        if (healthBarCanvas != null)
        {
            Vector3 worldPosition = transform.position + healthBarOffset;
            healthBarCanvas.transform.position = worldPosition;
        }
    }
    
    private void UpdateHealthBar()
    {
        if (healthBarFill != null)
        {
            // Smooth animation towards target
            currentFillAmount = Mathf.Lerp(currentFillAmount, targetFillAmount, Time.deltaTime * healthBarAnimationSpeed);
            healthBarFill.fillAmount = currentFillAmount;
        }
    }
    
    private void HandleDamageZone()
    {
        if (inDamageZone && currentDamageObject != null)
        {
            if (currentDamageObject != null && currentDamageObject.gameObject != null && Time.time - lastDamageTime >= currentDamageObject.damageRate)
            {
                TakeDamage(currentDamageObject.damageAmount);
                if (isDead) return;
                lastDamageTime = Time.time;
                
                // Trigger callback for weapon passives
                if (currentDamageObject.onEnemyHit != null)
                {
                    currentDamageObject.onEnemyHit.Invoke();
                }
                
                Debug.Log($"DragonBoss took continuous damage: {currentDamageObject.damageAmount} from {currentDamageObject.gameObject.name}");
            }
            else if (currentDamageObject == null || currentDamageObject.gameObject == null)
            {
                inDamageZone = false;
                currentDamageObject = null;
            }
        }
    }
    
    private void HandleDragonAI()
    {
        // Don't run AI logic during flying attacks or fire breath to prevent direction changes
        if (!isGrounded || isAttacking) return;
        
        // Find the nearest target (player or ally)
        Transform nearestTarget = FindNearestTarget();
        if (nearestTarget == null) return;
        
        float distanceToTarget = Vector3.Distance(transform.position, nearestTarget.position);
        
        // Check if target is within detection radius
        if (distanceToTarget <= detectionRadius)
        {
            // Face the target
            FaceTarget(nearestTarget);
            
            // Choose attack based on distance and cooldowns
            ChooseAttack(distanceToTarget);
        }
        else
        {
            // Stop moving when no targets are in range
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
        
        // Check player
        if (playerTransform != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
            if (distanceToPlayer < nearestDistance)
            {
                nearestDistance = distanceToPlayer;
                nearestTarget = playerTransform;
            }
        }
        
        // Check allies (PlayerSummon tag)
        GameObject[] allies = GameObject.FindGameObjectsWithTag("PlayerSummon");
        foreach (GameObject ally in allies)
        {
            if (ally != null && ally.activeInHierarchy)
            {
                AttackDummy allyComponent = ally.GetComponent<AttackDummy>();
                if (allyComponent != null)
                {
                    float distanceToAlly = Vector3.Distance(transform.position, ally.transform.position);
                    if (distanceToAlly < nearestDistance)
                    {
                        nearestDistance = distanceToAlly;
                        nearestTarget = ally.transform;
                    }
                }
            }
        }
        
        return nearestTarget;
    }
    
    private void FaceTarget(Transform target)
    {
        if (target == null || spriteRenderer == null) return;
        
        // Determine facing direction
        bool shouldFaceRight = target.position.x > transform.position.x;
        
        if (shouldFaceRight != isFacingRight)
        {
            isFacingRight = shouldFaceRight;
            spriteRenderer.flipX = !isFacingRight;
        }
    }
    
    private void ChooseAttack(float distanceToPlayer)
    {
        float currentTime = Time.time;
        
        // Priority 1: Flying attack if cooldown is ready and not recently used
        if (currentTime - lastFlyingAttackTime >= flyingAttackCooldown && isGrounded)
        {
            StartCoroutine(PerformFlyingAttack());
            return;
        }
        
        // Priority 2: Fire breath if in range and cooldown ready
        if (distanceToPlayer >= fireBreathMinRange && distanceToPlayer <= fireBreathRange && 
            currentTime - lastFireBreathTime >= fireBreathCooldown && isGrounded)
        {
            StartCoroutine(PerformFireBreath());
            return;
        }
        
        // Priority 3: Melee attack if close enough
        if (distanceToPlayer <= meleeAttackRange && currentTime - lastMeleeAttackTime >= meleeAttackCooldown)
        {
            StartCoroutine(PerformMeleeAttack());
            return;
        }
        
        // Move towards player if no attacks are available
        if (!isAttacking && distanceToPlayer > meleeAttackRange)
        {
            MoveTowardsPlayer();
        }
        
        // Check for auto-jump if player is significantly higher
        CheckForAutoJump();
    }
    
    private void CheckForAutoJump()
    {
        if (playerTransform == null || !isGrounded || isAttacking) return;
        
        float currentTime = Time.time;
        if (currentTime - lastJumpTime < jumpCooldown) return;
        
        float heightDifference = playerTransform.position.y - transform.position.y;
        bool shouldJump = heightDifference > minHeightDifferenceToJump && 
                         Mathf.Abs(rb.linearVelocity.y) < 0.1f; // Only jump when grounded
        
        if (shouldJump)
        {
            StartCoroutine(PerformAutoJump());
        }
    }
    
    private IEnumerator PerformAutoJump()
    {
        lastJumpTime = Time.time;
        
        // Auto-jump to reach player
        
        // Set rising state for jump
        isRising = true;
        isGrounded = false;
        
        // Apply jump force
        if (rb != null)
        {
            Vector3 direction = (playerTransform.position - transform.position).normalized;
            Vector2 jumpVelocity = new Vector2(direction.x * followSpeed, jumpForce);
            rb.linearVelocity = jumpVelocity;
        }
        
        // Wait for the specified delay before starting landing phase
        yield return new WaitForSeconds(jumpToLandingDelay);
        
        // Start landing phase
        isRising = false;
        isLanding = true;
        
        // Auto-jump landing phase
        
        // Wait until grounded (check for ground collision or timeout)
        float landingTimeout = 3f;
        float landingStartTime = Time.time;
        
        while (!isGrounded && (Time.time - landingStartTime) < landingTimeout)
        {
            // Check if we're close to ground level (simple ground check)
            if (transform.position.y <= spawnPosition.y + 0.5f && rb.linearVelocity.y <= 0.1f)
            {
                isGrounded = true;
                break;
            }
            yield return new WaitForSeconds(0.1f);
        }
        
        // Clear landing state when grounded
        isLanding = false;
        isGrounded = true;
        
        // Auto-jump completed
    }
    
    private void MoveTowardsPlayer()
    {
        if (rb == null || playerTransform == null || !isGrounded) return;
        
        Vector3 direction = (playerTransform.position - transform.position).normalized;
        rb.linearVelocity = new Vector2(direction.x * followSpeed, rb.linearVelocity.y);
        
        // Update animation state - dragon is walking when velocity is significant
        bool isWalking = Mathf.Abs(rb.linearVelocity.x) > 0.1f;
        
        if (animator != null)
        {
            animator.SetBool("isWalking", isWalking);
        }
    }
    
    private void FacePlayer()
    {
        if (playerTransform == null || spriteRenderer == null) return;
        
        bool shouldFaceRight = playerTransform.position.x > transform.position.x;
        
        if (shouldFaceRight != isFacingRight)
        {
            isFacingRight = shouldFaceRight;
            spriteRenderer.flipX = !isFacingRight;
        }
    }
    
    private void UpdateAnimatorParameters()
    {
        if (animator == null) return;
        
        // Check if dragon is walking (based on velocity, but not during attacks or flying)
        bool isWalking = rb != null && Mathf.Abs(rb.linearVelocity.x) > 0.1f && !isAttacking && !isRising && !isLanding;
        
        // Set animation parameters
        animator.SetBool("isAttacking", isAttacking);
        animator.SetBool("isRising", isRising);
        animator.SetBool("isLanding", isLanding);
        animator.SetBool("isWalking", isWalking);
        
        // Set attack type when attacking
        if (isAttacking)
        {
            // attackType should already be set by individual attack methods
            // but we can ensure it's maintained during the attack
        }
    }
    
    // === ATTACK IMPLEMENTATIONS ===
    
    private IEnumerator PerformMeleeAttack()
    {
        isAttacking = true;
        lastMeleeAttackTime = Time.time;
        
        Debug.Log("DragonBoss: Performing melee claw swipe attack!");
        
        // Stop movement during attack
        if (rb != null)
        {
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        }
        
        // Trigger melee attack animation
        if (animator != null)
        {
            animator.SetInteger("attackType", 0); // Melee attack type
            animator.SetBool("isAttacking", true);
        }
        
        // Create melee damage zone
        Vector3 attackPosition = transform.position + (Vector3.right * (isFacingRight ? meleeAttackRange * 0.5f : -meleeAttackRange * 0.5f));
        CreateDamageZone(attackPosition, meleeAttackSize, meleeAttackDamage, meleeAttackDuration, "DragonClawSwipe");
        
        // Wait for attack duration
        yield return new WaitForSeconds(meleeAttackDuration);
        
        // Clear attack state
        isAttacking = false;
        if (animator != null)
        {
            animator.SetBool("isAttacking", false);
        }
        
        // Melee attack completed
    }
    
    private IEnumerator PerformFireBreath()
    {
        isAttacking = true;
        lastFireBreathTime = Time.time;
        
        Debug.Log("DragonBoss: Performing fire breath attack!");
        
        // Stop movement and become stationary
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
        
        // Trigger fire breath animation
        if (animator != null)
        {
            animator.SetInteger("attackType", 1); // Fire breath attack type
            animator.SetBool("isAttacking", true);
        }
        
        // Create fire blocks progressively
        StartCoroutine(CreateFireBlocks());
        
        // Wait for attack duration
        yield return new WaitForSeconds(fireBreathDuration);
        
        // Clear attack state
        isAttacking = false;
        if (animator != null)
        {
            animator.SetBool("isAttacking", false);
        }
        
        // Fire breath attack completed
    }
    
    private IEnumerator CreateFireBlocks()
    {
        Vector3 startPosition = transform.position;
        Vector3 direction = isFacingRight ? Vector3.right : Vector3.left;
        
        // Safety check: ensure offset doesn't exceed the total range
        float safeOffset = Mathf.Min(fireBlockFrontOffset, fireBreathRange * 0.8f); // Use max 80% of range for offset
        
        // Calculate available range for fire blocks (total range minus the front offset)
        float availableRange = fireBreathRange - safeOffset;
        
        // Start fire blocks from the front offset position
        Vector3 fireStartPosition = startPosition + direction * safeOffset;
        
        if (safeOffset != fireBlockFrontOffset)
        {
            Debug.LogWarning($"DragonBoss: fireBlockFrontOffset ({fireBlockFrontOffset}) reduced to {safeOffset} to stay within fireBreathRange ({fireBreathRange})");
        }
        
        for (int i = 0; i < numberOfFireBlocks; i++)
        {
            // Calculate position for this fire block within the available range
            float distance = i * (availableRange / numberOfFireBlocks);
            Vector3 fireBlockPosition = fireStartPosition + direction * distance;
            
            // Calculate total distance from dragon for debugging
            float totalDistanceFromDragon = safeOffset + distance;
            
            // Create fire damage block that follows the dragon
            Vector3 relativeOffset = fireBlockPosition - startPosition; // Calculate offset from dragon
            CreateFollowingFireBlock(relativeOffset, fireBlockSize, fireBreathDamage, fireBlockLifetime, $"DragonFireBlock_{i}");
            
            Debug.Log($"DragonBoss: Fire block {i + 1} created at total distance {totalDistanceFromDragon:F2} from dragon (within range {fireBreathRange})");
            
            
            // Wait before creating next block
            if (i < numberOfFireBlocks - 1)
            {
                yield return new WaitForSeconds(fireBlockSpawnDelay);
            }
        }
    }
    
    private IEnumerator PerformFlyingAttack()
    {
        Debug.Log("DragonBoss: Starting flying attack sequence");
        
        // FLYOVER LANDING BEHAVIOR:
        // - Odd flyover counts (1, 3, 5...): Land at final flyover position
        // - Even flyover counts (2, 4, 6...): Return to original rising position before landing
        
        isAttacking = false; // Not attacking during rising phase
        isGrounded = false;
        lastFlyingAttackTime = Time.time;
        
        // Disable gravity while flying
        float originalGravity = rb.gravityScale;
        rb.gravityScale = 0f;
        // Disabled gravity for flight
        
        // Phase 1: Rising phase
        isRising = true;
        // Phase 1 - Rising phase started
        
        // Update animator for rising
        if (animator != null)
        {
            animator.SetBool("isRising", true);
            animator.SetBool("isAttacking", false);
            // Set animator isRising=true, isAttacking=false
        }
        
        // Wait for rising delay before starting movement
        // Waiting before rising motion
        yield return new WaitForSeconds(risingDelay);
        
        // Perform rising movement
        // About to start rising movement
        yield return StartCoroutine(PerformRisingPhase());
        // Rising movement completed
        
        // Phase 2: Attack phases (alternating attack/flying based on animation exit times)
        isRising = false;
        // Phase 2 - Attack phase started
        
        // Store the rising position for proper landing (only for multi-pass)
        Vector3 originalRisingPosition = Vector3.zero;
        if (flyOverCount > 1)
        {
            originalRisingPosition = new Vector3(transform.position.x, transform.position.y - flyHeight, transform.position.z);
            Debug.Log($"DragonBoss: Stored original rising position for multi-pass landing: {originalRisingPosition}");
        }
        
        // Show landing strategy for this flyover sequence
        bool willReturnToOriginal = (flyOverCount % 2 == 0) && (flyOverCount > 1);
        string landingStrategy = willReturnToOriginal ? "return to original position" : "land at final flyover position";
        Debug.Log($"DragonBoss: Flyover count: {flyOverCount} - Landing strategy: {landingStrategy}");
        
        if (animator != null)
        {
            animator.SetBool("isRising", false);
        }
        
        // Perform fly-over attacks using timer-based completion (clean and reliable)
        for (int flyOver = 0; flyOver < flyOverCount; flyOver++)
        {
            Debug.Log($"DragonBoss: Starting fly-over attack {flyOver + 1}/{flyOverCount}");
            
            // Set attacking state for the duration of the fly-over
            isAttacking = true;
            Debug.Log("DragonBoss: Set isAttacking=true for fly-over duration");
            
            // Start the movement coroutine with auto-attack detection
            Coroutine flyOverMovement = StartCoroutine(PerformSingleFlyOver());
            
            // Wait for the fly-over movement to complete (includes auto-attack detection)
            yield return flyOverMovement;
            Debug.Log($"DragonBoss: Fly-over movement completed");
            
            // Complete the attack state
            isAttacking = false;
            if (animator != null)
            {
                animator.SetBool("isAttacking", false);
            }
            Debug.Log($"DragonBoss: Attack state completed, isAttacking set to false");
            
            // Turn around for the next pass (if there is one)
            if (flyOver < flyOverCount - 1)
            {
                Debug.Log($"DragonBoss: Turning around for next fly-over pass");
                isFacingRight = !isFacingRight;
                
                // Update sprite direction
                if (spriteRenderer != null)
                {
                    spriteRenderer.flipX = !isFacingRight;
                    Debug.Log($"DragonBoss: Flipped sprite, now facing {(isFacingRight ? "right" : "left")}");
                }
                
                // Brief pause for turning around
                yield return new WaitForSeconds(0.5f);
            }
        }
        
        // After all fly-overs, determine landing behavior based on flyover count
        // Even numbers (2, 4, 6...): return to original position
        // Odd numbers (1, 3, 5...): land where dragon currently is
        bool shouldReturnToOriginal = (flyOverCount % 2 == 0);
        
        if (flyOverCount > 1 && shouldReturnToOriginal)
        {
            Vector3 returnPosition = new Vector3(originalRisingPosition.x, originalRisingPosition.y + flyHeight, originalRisingPosition.z);
            Debug.Log($"DragonBoss: Even flyover count ({flyOverCount}) - Returning to original rising position before landing: {returnPosition}");
            yield return StartCoroutine(MoveToPosition(returnPosition, flySpeed));
            Debug.Log($"DragonBoss: Returned to original rising position for landing");
        }
        else if (flyOverCount > 1)
        {
            Debug.Log($"DragonBoss: Odd flyover count ({flyOverCount}) - Landing at current position after final flyover");
        }
        
        // Phase 3: Landing phase
        isAttacking = false; // Ensure attacking is off
        isLanding = true;
        Debug.Log("DragonBoss: Phase 3 - Landing phase started");
        
        // Update animator for landing
        if (animator != null)
        {
            animator.SetBool("isAttacking", false);
            animator.SetBool("isLanding", true);
            Debug.Log("DragonBoss: Set isLanding=true for landing animation");
        }
        
        // Wait a brief moment for landing animation to start
        yield return new WaitForSeconds(0.2f);
        
        // Land back on ground
        if (flyOverCount > 1 && shouldReturnToOriginal)
        {
            // Even flyover count: land at original rising position
            Debug.Log($"DragonBoss: Landing at original rising position (even flyover count: {flyOverCount})");
            yield return StartCoroutine(LandOnGround(originalRisingPosition));
        }
        else
        {
            // Single pass OR odd flyover count: land wherever the dragon currently is
            Vector3 currentLandingPosition = new Vector3(transform.position.x, spawnPosition.y, transform.position.z);
            Debug.Log($"DragonBoss: Landing at current position (flyover count: {flyOverCount})");
            yield return StartCoroutine(LandOnGround(currentLandingPosition));
        }
        
        // Restore gravity
        rb.gravityScale = originalGravity;
        
        // Clear all states when fully landed
        isGrounded = true;
        isLanding = false;
        
        // Fully reset animator for completion - ensure proper ground state
        if (animator != null)
        {
            animator.SetBool("isLanding", false);
            animator.SetBool("isRising", false); 
            animator.SetBool("isAttacking", false);
            Debug.Log("DragonBoss: Reset all animation flags - isLanding, isRising, isAttacking = false");
        }
        
        // Force update animator parameters to ensure correct ground animation state
        UpdateAnimatorParameters();
        Debug.Log("DragonBoss: Updated animator parameters for ground state");
        
        Debug.Log("DragonBoss: Flying attack completed!");
    }
    
    private IEnumerator PerformRisingPhase()
    {
        Vector3 playerPosition = playerTransform.position;
        Vector3 startPosition = transform.position;
        
        Debug.Log($"DragonBoss: Rising phase - Start pos: {startPosition}, Player pos: {playerPosition}");
        
        // Rise into the air
        Vector3 flyStartPosition = new Vector3(startPosition.x, startPosition.y + flyHeight, startPosition.z);
        Debug.Log($"DragonBoss: Rising to fly position: {flyStartPosition} (flyHeight: {flyHeight})");
        yield return StartCoroutine(MoveToPosition(flyStartPosition, flySpeed * 0.8f));
        Debug.Log($"DragonBoss: Reached fly height at: {transform.position}");
        
        // Move to position before player for the attack run
        Vector3 preAttackPosition = new Vector3(playerPosition.x - flyOverDistance * (isFacingRight ? 1 : -1), flyStartPosition.y, flyStartPosition.z);
        Debug.Log($"DragonBoss: Moving to pre-attack position: {preAttackPosition} (flyOverDistance: {flyOverDistance})");
        yield return StartCoroutine(MoveToPosition(preAttackPosition, flySpeed));
        Debug.Log($"DragonBoss: Reached pre-attack position at: {transform.position}");
    }

    private IEnumerator PerformSingleFlyOver()
    {
        Vector3 playerPosition = playerTransform.position;
        
        // Calculate fly-over target based on flyOverCount
        Vector3 targetPosition;
        
        if (flyOverCount == 1)
        {
            // Single pass: fly over player by flyOverDistance (classic behavior)
            targetPosition = new Vector3(playerPosition.x + flyOverDistance * (isFacingRight ? 1 : -1), transform.position.y, transform.position.z);
            Debug.Log($"DragonBoss: Single fly-over to: {targetPosition} (flyOverDistance: {flyOverDistance})");
        }
        else
        {
            // Multiple passes: use flyOverDistance for true fly-over
            targetPosition = new Vector3(playerPosition.x + flyOverDistance * (isFacingRight ? 1 : -1), transform.position.y, transform.position.z);
            Debug.Log($"DragonBoss: Multi-pass fly-over to: {targetPosition} (flyOverDistance: {flyOverDistance})");
        }
        
        // Move to target with player detection for auto-attack
        yield return StartCoroutine(MoveToPositionWithPlayerDetection(targetPosition, flySpeed * 1.2f));
        
        Debug.Log($"DragonBoss: Reached fly-over target: {transform.position}");
    }
    
    private IEnumerator MoveToPositionWithPlayerDetection(Vector3 targetPosition, float speed)
    {
        Vector3 startPos = transform.position;
        Debug.Log($"DragonBoss: MoveToPositionWithPlayerDetection from {startPos} to {targetPosition} at speed {speed}");
        
        bool hasTriggeredAutoAttack = false;
        // Use SerializeField for configurable detection range
        
        while (Vector3.Distance(transform.position, targetPosition) > 0.1f)
        {
            // Move toward target
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);
            
            // Check if player is below dragon and within detection range
            if (!hasTriggeredAutoAttack && playerTransform != null)
            {
                float horizontalDistance = Mathf.Abs(transform.position.x - playerTransform.position.x);
                float verticalDistance = transform.position.y - playerTransform.position.y;
                
                // Player is below and within horizontal range (broader detection)
                if (horizontalDistance <= playerDetectionRange && verticalDistance > 1f && verticalDistance < 10f)
                {
                    Debug.Log($"DragonBoss: Auto-attack triggered! Player detected below at horizontal distance {horizontalDistance:F2} (detection range: {playerDetectionRange})");
                    hasTriggeredAutoAttack = true;
                    
                    // Create damage zone positioned below and forward of the dragon during attack
                    Vector2 flyingDamageSize = new Vector2(flyAttackWidth, flyingAttackSize.y);
                    float forwardOffset = isFacingRight ? flyingAttackForwardOffset : -flyingAttackForwardOffset;
                    Vector3 damageZonePosition = new Vector3(transform.position.x + forwardOffset, transform.position.y - flyingAttackDepth, transform.position.z);
                    GameObject flyingDamage = CreateFlyingDamageZone(damageZonePosition, flyingDamageSize, flyingAttackDamage, flyingAttackDepth, forwardOffset, "DragonFlyingAttack");
                    
                    // Trigger attack animation automatically
                    if (animator != null)
                    {
                        animator.SetBool("isAttacking", true);
                        animator.SetInteger("attackType", 2);
                        Debug.Log("DragonBoss: Auto-triggered attack animation during flight");
                    }
                    
                    // Start non-blocking cleanup after attack duration (dragon continues flying)
                    StartCoroutine(CleanupFlyingDamageAfterDelay(flyingDamage, 0.75f));
                }
            }
            
            yield return null; // Wait one frame
        }
        
        // Ensure exact final position
        transform.position = targetPosition;
        Debug.Log($"DragonBoss: MoveToPositionWithPlayerDetection completed - Final position: {targetPosition}");
    }
    
    private IEnumerator LandOnGround(Vector3 landingPosition)
    {
        // Land at the specified position
        Debug.Log($"DragonBoss: Landing at position: {landingPosition}");
        yield return StartCoroutine(MoveToPosition(landingPosition, flySpeed * 0.8f));
    }
    
    private IEnumerator MoveToPosition(Vector3 targetPosition, float speed)
    {
        Vector3 startPos = transform.position;
        Debug.Log($"DragonBoss: MoveToPosition from {startPos} to {targetPosition} at speed {speed}");
        
        while (Vector3.Distance(transform.position, targetPosition) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);
            yield return null;
        }
        transform.position = targetPosition;
        Debug.Log($"DragonBoss: MoveToPosition completed - Final position: {transform.position}");
    }
    
    private IEnumerator CleanupFlyingDamageAfterDelay(GameObject flyingDamage, float delay)
    {
        yield return new WaitForSeconds(delay);
        
        // Destroy damage zone after attack duration
        if (flyingDamage != null)
        {
            Destroy(flyingDamage);
            Debug.Log("DragonBoss: Destroyed flying damage zone after attack");
        }
        
        // Clear attack state
        if (animator != null)
        {
            animator.SetBool("isAttacking", false);
        }
    }
    
    // === DAMAGE SYSTEM ===
    
    private GameObject CreateFlyingDamageZone(Vector3 position, Vector2 size, float damage, float depth, float forwardOffset, string zoneName)
    {
        Debug.Log($"=== CREATING FLYING DAMAGE ZONE ===");
        Debug.Log($"Position: {position}, Size: {size}, Damage: {damage}, Depth: {depth}, ForwardOffset: {forwardOffset}, Name: {zoneName}");
        
        GameObject damageZone = new GameObject(zoneName);
        damageZone.transform.position = position;
        
        Debug.Log($"Created GameObject '{zoneName}' at position: {damageZone.transform.position}");
        
        // Add collider
        BoxCollider2D collider = damageZone.AddComponent<BoxCollider2D>();
        collider.size = size;
        collider.isTrigger = true;
        
        Debug.Log($"Added BoxCollider2D - Size: {collider.size}, IsTrigger: {collider.isTrigger}");
        
        // Add damage component
        DamageObject damageComponent = damageZone.AddComponent<DamageObject>();
        damageComponent.damageAmount = (int)damage;
        damageComponent.damageRate = 0.3f;
        damageComponent.canDamageEnemies = false;
        damageComponent.excludeLayers = LayerMask.GetMask("Entities");
        
        // Add fire particle spawner component for flying fire breath
        FireBreathParticleSpawner particleSpawner = damageZone.AddComponent<FireBreathParticleSpawner>();
        particleSpawner.dragonBoss = this;
        
        Debug.Log($"Added DamageObject - Damage: {damageComponent.damageAmount}, Rate: {damageComponent.damageRate}, ExcludeLayers: {damageComponent.excludeLayers.value}");
        
        // Add visual indicator - make invisible for production
        SpriteRenderer visual = damageZone.AddComponent<SpriteRenderer>();
        visual.color = new Color(1f, 0f, 0f, 0f); // Fully transparent (invisible)
        
        // Try default sorting layer first, then Entities if that doesn't work
        try 
        {
            visual.sortingLayerName = "Entities"; 
            Debug.Log($"Set sorting layer to 'Entities' successfully");
        }
        catch
        {
            Debug.LogWarning("'Entities' sorting layer not found, using default");
            visual.sortingLayerName = "Default";
        }
        
        visual.sortingOrder = 100; 
        
        Debug.Log($"Added SpriteRenderer - Color: {visual.color}, SortingLayer: {visual.sortingLayerName}, Order: {visual.sortingOrder}");
        
        // Create a properly sized texture for the damage zone
        int textureWidth = Mathf.Max(1, Mathf.RoundToInt(size.x * 100f)); // Scale up for better resolution
        int textureHeight = Mathf.Max(1, Mathf.RoundToInt(size.y * 100f));
        
        Texture2D texture = new Texture2D(textureWidth, textureHeight);
        Color[] pixels = new Color[textureWidth * textureHeight];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.white; // Fill entire texture with white
        }
        texture.SetPixels(pixels);
        texture.Apply();
        
        // Create sprite with proper pixels per unit to match the desired size
        float pixelsPerUnit = 100f; // This will make the sprite the correct world size
        visual.sprite = Sprite.Create(texture, new Rect(0, 0, textureWidth, textureHeight), Vector2.one * 0.5f, pixelsPerUnit);
        
        Debug.Log($"Created damage zone texture: {textureWidth}x{textureHeight} pixels, PPU: {pixelsPerUnit}, Final size should be: {size}");
        
        Debug.Log($"Created sprite texture and set size: {visual.size}");
        
        // Add a yellow outline for better visibility - make invisible for production
        GameObject outline = new GameObject($"{zoneName}_Outline");
        outline.transform.SetParent(damageZone.transform);
        outline.transform.localPosition = Vector3.zero;
        SpriteRenderer outlineRenderer = outline.AddComponent<SpriteRenderer>();
        outlineRenderer.color = new Color(1f, 1f, 0f, 0f); // Fully transparent (invisible)
        
        // Use same sorting layer as main visual
        try 
        {
            outlineRenderer.sortingLayerName = "Entities"; 
        }
        catch
        {
            outlineRenderer.sortingLayerName = "Default";
        }
        
        outlineRenderer.sortingOrder = 99; 
        
        // Create outline texture (slightly larger)
        Vector2 outlineSize = size + Vector2.one * 0.2f;
        int outlineWidth = Mathf.Max(1, Mathf.RoundToInt(outlineSize.x * 100f));
        int outlineHeight = Mathf.Max(1, Mathf.RoundToInt(outlineSize.y * 100f));
        
        Texture2D outlineTexture = new Texture2D(outlineWidth, outlineHeight);
        Color[] outlinePixels = new Color[outlineWidth * outlineHeight];
        for (int i = 0; i < outlinePixels.Length; i++)
        {
            outlinePixels[i] = Color.white; // Fill entire texture with white
        }
        outlineTexture.SetPixels(outlinePixels);
        outlineTexture.Apply();
        
        outlineRenderer.sprite = Sprite.Create(outlineTexture, new Rect(0, 0, outlineWidth, outlineHeight), Vector2.one * 0.5f, 100f);
        
        Debug.Log($"Created outline - Color: {outlineRenderer.color}, Size: {outlineSize}");
        
        // Make the damage zone follow the dragon with offset (below and forward)
        FlyingDamageFollower follower = damageZone.AddComponent<FlyingDamageFollower>();
        follower.target = transform;
        follower.offset = new Vector3(forwardOffset, -depth, 0); // Position below and forward of the dragon
        
        Debug.Log($"Added FlyingDamageFollower targeting: {follower.target.name} with offset: {follower.offset}");
        Debug.Log($"=== FLYING DAMAGE ZONE CREATION COMPLETE ===");
        
        return damageZone;
    }

    // === DAMAGE SYSTEM ===
    
    private void CreateDamageZone(Vector3 position, Vector2 size, float damage, float duration, string zoneName)
    {
        GameObject damageZone = new GameObject(zoneName);
        damageZone.transform.position = position;
        
        // Add collider
        BoxCollider2D collider = damageZone.AddComponent<BoxCollider2D>();
        collider.size = size;
        collider.isTrigger = true;
        
        // Add damage component
        DamageObject damageComponent = damageZone.AddComponent<DamageObject>();
        damageComponent.damageAmount = (int)damage;
        damageComponent.damageRate = 0.5f; // Damage every 0.5 seconds
        damageComponent.canDamageEnemies = false; // Boss attacks don't damage other enemies
        
        // Entities cannot damage other Entities (according to your layer system)
        damageComponent.excludeLayers = LayerMask.GetMask("Entities");
        
        Debug.Log($"DragonBoss: CreateDamageZone - Excluding Entities layer from damage. ExcludeLayers value: {damageComponent.excludeLayers.value}");
        
        // Add visual indicator - make invisible for production  
        SpriteRenderer visual = damageZone.AddComponent<SpriteRenderer>();
        visual.color = new Color(1f, 0f, 0f, 0f); // Fully transparent (invisible)
        visual.sortingLayerName = "Entities"; // Use Entities sorting layer
        visual.sortingOrder = 100; // Very high sorting order to render above everything
        
        // Create a properly sized texture for the damage zone
        int textureWidth = Mathf.Max(1, Mathf.RoundToInt(size.x * 100f)); // Scale up for better resolution
        int textureHeight = Mathf.Max(1, Mathf.RoundToInt(size.y * 100f));
        
        Texture2D texture = new Texture2D(textureWidth, textureHeight);
        Color[] pixels = new Color[textureWidth * textureHeight];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.white; // Fill entire texture with white
        }
        texture.SetPixels(pixels);
        texture.Apply();
        
        // Create sprite with proper pixels per unit to match the desired size
        float pixelsPerUnit = 100f; // This will make the sprite the correct world size
        visual.sprite = Sprite.Create(texture, new Rect(0, 0, textureWidth, textureHeight), Vector2.one * 0.5f, pixelsPerUnit);
        
        // Add a yellow outline to make it even more visible
        GameObject outline = new GameObject($"{zoneName}_Outline");
        outline.transform.SetParent(damageZone.transform);
        outline.transform.localPosition = Vector3.zero;
        SpriteRenderer outlineRenderer = outline.AddComponent<SpriteRenderer>();
        outlineRenderer.color = new Color(1f, 1f, 0f, 0f); // Fully transparent (invisible)
        outlineRenderer.sortingLayerName = "Entities"; // Use Entities sorting layer
        outlineRenderer.sortingOrder = 99; // Just below the main visual
        
        // Create outline texture (slightly larger)
        Vector2 outlineSize = size + Vector2.one * 0.2f;
        int outlineWidth = Mathf.Max(1, Mathf.RoundToInt(outlineSize.x * 100f));
        int outlineHeight = Mathf.Max(1, Mathf.RoundToInt(outlineSize.y * 100f));
        
        Texture2D outlineTexture = new Texture2D(outlineWidth, outlineHeight);
        Color[] outlinePixels = new Color[outlineWidth * outlineHeight];
        for (int i = 0; i < outlinePixels.Length; i++)
        {
            outlinePixels[i] = Color.white; // Fill entire texture with white
        }
        outlineTexture.SetPixels(outlinePixels);
        outlineTexture.Apply();
        
        outlineRenderer.sprite = Sprite.Create(outlineTexture, new Rect(0, 0, outlineWidth, outlineHeight), Vector2.one * 0.5f, 100f);
        
        Debug.Log($"DragonBoss: Created visual for {zoneName} with color {visual.color}, sortingLayer: {visual.sortingLayerName}, order: {visual.sortingOrder}");
        
        // Destroy after duration
        StartCoroutine(DestroyAfterTime(damageZone, duration));
        
        Debug.Log($"DragonBoss: Created {zoneName} at {position} with {damage} damage");
    }
    
    private void CreateFollowingFireBlock(Vector3 offset, Vector2 size, float damage, float duration, string zoneName)
    {
        GameObject damageZone = new GameObject(zoneName);
        
        // Add collider
        BoxCollider2D collider = damageZone.AddComponent<BoxCollider2D>();
        collider.size = size;
        collider.isTrigger = true;
        
        // Add damage component
        DamageObject damageComponent = damageZone.AddComponent<DamageObject>();
        damageComponent.damageAmount = (int)damage;
        damageComponent.damageRate = 0.5f; // Damage every 0.5 seconds
        damageComponent.canDamageEnemies = false; // Boss attacks don't damage other enemies
        
        // Entities cannot damage other Entities
        damageComponent.excludeLayers = LayerMask.GetMask("Entities");
        
        // Add fire particle spawner component for ground fire breath
        FireBreathParticleSpawner particleSpawner = damageZone.AddComponent<FireBreathParticleSpawner>();
        particleSpawner.dragonBoss = this;
        
        // Add visual indicator - make invisible for production
        SpriteRenderer visual = damageZone.AddComponent<SpriteRenderer>();
        visual.color = new Color(1f, 0.5f, 0f, 0f); // Fully transparent (invisible)
        visual.sortingLayerName = "Entities";
        visual.sortingOrder = 100;
        
        // Create a properly sized texture for the damage zone
        int textureWidth = Mathf.Max(1, Mathf.RoundToInt(size.x * 100f));
        int textureHeight = Mathf.Max(1, Mathf.RoundToInt(size.y * 100f));
        
        Texture2D texture = new Texture2D(textureWidth, textureHeight);
        Color[] pixels = new Color[textureWidth * textureHeight];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.white;
        }
        texture.SetPixels(pixels);
        texture.Apply();
        
        float pixelsPerUnit = 100f;
        visual.sprite = Sprite.Create(texture, new Rect(0, 0, textureWidth, textureHeight), Vector2.one * 0.5f, pixelsPerUnit);
        
        // Add yellow outline
        GameObject outline = new GameObject($"{zoneName}_Outline");
        outline.transform.SetParent(damageZone.transform);
        outline.transform.localPosition = Vector3.zero;
        SpriteRenderer outlineRenderer = outline.AddComponent<SpriteRenderer>();
        outlineRenderer.color = new Color(1f, 1f, 0f, 0f); // Fully transparent (invisible)
        outlineRenderer.sortingLayerName = "Entities";
        outlineRenderer.sortingOrder = 99;
        
        Vector2 outlineSize = size + Vector2.one * 0.2f;
        int outlineWidth = Mathf.Max(1, Mathf.RoundToInt(outlineSize.x * 100f));
        int outlineHeight = Mathf.Max(1, Mathf.RoundToInt(outlineSize.y * 100f));
        
        Texture2D outlineTexture = new Texture2D(outlineWidth, outlineHeight);
        Color[] outlinePixels = new Color[outlineWidth * outlineHeight];
        for (int i = 0; i < outlinePixels.Length; i++)
        {
            outlinePixels[i] = Color.white;
        }
        outlineTexture.SetPixels(outlinePixels);
        outlineTexture.Apply();
        
        outlineRenderer.sprite = Sprite.Create(outlineTexture, new Rect(0, 0, outlineWidth, outlineHeight), Vector2.one * 0.5f, 100f);
        
        // Add follower component to track dragon with offset
        FireBlockFollower follower = damageZone.AddComponent<FireBlockFollower>();
        follower.target = transform;
        follower.offset = offset;
        
        // Set initial position
        damageZone.transform.position = transform.position + offset;
        
        // Destroy after duration
        StartCoroutine(DestroyAfterTime(damageZone, duration));
        
        Debug.Log($"DragonBoss: Created following fire block {zoneName} with offset {offset} and duration {duration}");
    }
    
    private GameObject CreateMovingDamageZone(Vector2 size, float damage, string zoneName)
    {
        GameObject damageZone = new GameObject(zoneName);
        damageZone.transform.SetParent(transform); // Follow dragon
        damageZone.transform.localPosition = Vector3.zero;
        
        // Add collider
        BoxCollider2D collider = damageZone.AddComponent<BoxCollider2D>();
        collider.size = size;
        collider.isTrigger = true;
        
        // Add damage component
        DamageObject damageComponent = damageZone.AddComponent<DamageObject>();
        damageComponent.damageAmount = (int)damage;
        damageComponent.damageRate = 0.3f; // Faster damage rate for flying attack
        damageComponent.canDamageEnemies = false;
        
        // Entities cannot damage other Entities, but can damage Player
        // Only exclude other Entities, not the Player layer
        damageComponent.excludeLayers = LayerMask.GetMask("Entities"); 
        
        Debug.Log($"DragonBoss: CreateMovingDamageZone - Excluding Entities layer from damage. ExcludeLayers value: {damageComponent.excludeLayers.value}");
        
        // Add visual indicator - make invisible for production
        SpriteRenderer visual = damageZone.AddComponent<SpriteRenderer>();
        visual.color = new Color(1f, 0f, 0f, 0f); // Fully transparent (invisible)
        visual.sortingLayerName = "Entities"; // Use Entities sorting layer
        visual.sortingOrder = 100; // Very high sorting order to render above everything
        
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        visual.sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f);
        visual.size = size;
        
        // Add a yellow outline for flying attack too - make invisible for production
        GameObject outline = new GameObject($"{zoneName}_Outline");
        outline.transform.SetParent(damageZone.transform);
        outline.transform.localPosition = Vector3.zero;
        SpriteRenderer outlineRenderer = outline.AddComponent<SpriteRenderer>();
        outlineRenderer.color = new Color(1f, 1f, 0f, 0f); // Fully transparent (invisible)
        outlineRenderer.sortingLayerName = "Entities"; // Use Entities sorting layer
        outlineRenderer.sortingOrder = 99; // Just below the main visual
        outlineRenderer.sprite = visual.sprite;
        outlineRenderer.size = size + Vector2.one * 0.2f; // Slightly larger for outline effect
        
        Debug.Log($"DragonBoss: Created moving visual for {zoneName} with color {visual.color}, sortingLayer: {visual.sortingLayerName}, order: {visual.sortingOrder}, size: {visual.size}");
        
        return damageZone;
    }
    
    private IEnumerator DestroyAfterTime(GameObject obj, float time)
    {
        yield return new WaitForSeconds(time);
        if (obj != null)
        {
            Destroy(obj);
        }
    }
    
    // === FIRE PARTICLE SYSTEM ===
    
    /// <summary>
    /// Spawns fire particle emitter at a surface collision point
    /// </summary>
    private void SpawnFireParticleAtSurface(Vector3 position)
    {
        if (fireParticleEmitterPrefab == null) return;
        
        // Prevent rapid spawning at the same position
        if (Time.time - lastParticleSpawnTime < particleSpawnCooldown) return;
        
        // Check if a particle was recently spawned nearby
        Vector3 roundedPosition = new Vector3(
            Mathf.Round(position.x * 2f) / 2f, // Round to 0.5 units
            Mathf.Round(position.y * 2f) / 2f,
            0f
        );
        
        // Clean up old position entries
        var keysToRemove = new System.Collections.Generic.List<Vector3>();
        foreach (var kvp in recentParticlePositions)
        {
            if (Time.time - kvp.Value > fireParticleDuration)
            {
                keysToRemove.Add(kvp.Key);
            }
        }
        foreach (var key in keysToRemove)
        {
            recentParticlePositions.Remove(key);
        }
        
        // Check if we already have a particle near this position
        if (recentParticlePositions.ContainsKey(roundedPosition)) return;
        
        // Do a more careful raycast to find the actual ground surface
        // Cast from above the position downwards to find solid ground
        Vector3 rayStart = new Vector3(position.x, position.y + 5f, position.z);
        RaycastHit2D hit = Physics2D.Raycast(rayStart, Vector2.down, 15f, surfaceLayerMask);
        
        Vector3 spawnPosition;
        if (hit.collider != null)
        {
            // Verify this isn't an invalid collider
            bool isValid = true;
            
            // Check for dragon or damage objects
            if (hit.collider.GetComponent<DragonBoss>() != null ||
                hit.collider.GetComponent<DamageObject>() != null ||
                hit.collider.name.Contains("Dragon"))
            {
                isValid = false;
            }
            
            if (isValid)
            {
                spawnPosition = hit.point;
                Debug.Log($"DragonBoss: Found valid surface at {spawnPosition} on {hit.collider.name}");
            }
            else
            {
                Debug.Log($"DragonBoss: Skipped invalid surface: {hit.collider.name}");
                return;
            }
        }
        else
        {
            // If no valid surface found, don't spawn
            Debug.Log($"DragonBoss: No valid surface found near {position}");
            return;
        }
        
        GameObject fireParticle = Instantiate(fireParticleEmitterPrefab, spawnPosition, Quaternion.identity);
        
        // Track this position
        recentParticlePositions[roundedPosition] = Time.time;
        lastParticleSpawnTime = Time.time;
        
        // Auto-destroy after duration
        StartCoroutine(DestroyAfterTime(fireParticle, fireParticleDuration));
        
        Debug.Log($"DragonBoss: Spawned fire particle at surface: {spawnPosition}");
    }
    
    /// <summary>
    /// Attaches fire particle emitter to player or ally that follows them
    /// </summary>
    private void AttachFireParticleToPlayer(Transform target)
    {
        if (fireParticleEmitterPrefab == null || target == null) return;
        
        // Check if this specific target was recently burned (per-target cooldown for AOE)
        if (targetBurnCooldowns.ContainsKey(target))
        {
            if (Time.time - targetBurnCooldowns[target] < particleSpawnCooldown)
            {
                Debug.Log($"DragonBoss: Target {target.name} is on burn cooldown, skipping");
                return;
            }
        }
        
        // Check if target already has a fire particle attached
        if (target.GetComponentInChildren<FireParticleFollower>() != null) 
        {
            Debug.Log($"DragonBoss: Target {target.name} already has fire particle, skipping");
            return;
        }
        
        GameObject fireParticle = Instantiate(fireParticleEmitterPrefab, target.position, Quaternion.identity);
        fireParticle.transform.SetParent(target);
        fireParticle.transform.localPosition = Vector3.zero;
        
        // Add a component to identify this as a target-attached fire particle
        fireParticle.AddComponent<FireParticleFollower>();
        
        // Update the per-target cooldown
        targetBurnCooldowns[target] = Time.time;
        
        // Auto-destroy after duration
        StartCoroutine(DestroyAfterTime(fireParticle, fireParticleDuration));
        
        // Apply burning effect to target
        PlayerMovement playerMovement = target.GetComponent<PlayerMovement>();
        AttackDummy allyDummy = target.GetComponent<AttackDummy>();
        
        if (playerMovement != null)
        {
            // Target is the player
            playerMovement.ApplyBurningEffect(fireParticleDuration);
            Debug.Log($"DragonBoss: Attached fire particle to player for {fireParticleDuration}s and applied burning effect");
        }
        else if (allyDummy != null)
        {
            // Target is a player summon (ally)
            allyDummy.ApplyBurningEffect(fireParticleDuration);
            Debug.Log($"DragonBoss: Attached fire particle to ally {target.name} for {fireParticleDuration}s and applied burning effect");
        }
        else
        {
            Debug.LogWarning($"DragonBoss: Target {target.name} is neither player nor ally - no burning effect applied");
        }
    }
    
    /// <summary>
    /// Cleanup expired burn cooldowns to prevent memory leaks
    /// </summary>
    private void CleanupExpiredBurnCooldowns()
    {
        var expiredTargets = new List<Transform>();
        float currentTime = Time.time;
        
        foreach (var kvp in targetBurnCooldowns)
        {
            // Remove cooldowns that are older than the particle duration + some buffer
            if (currentTime - kvp.Value > fireParticleDuration + 1f)
            {
                expiredTargets.Add(kvp.Key);
            }
        }
        
        foreach (var target in expiredTargets)
        {
            targetBurnCooldowns.Remove(target);
        }
        
        if (expiredTargets.Count > 0)
        {
            Debug.Log($"DragonBoss: Cleaned up {expiredTargets.Count} expired burn cooldowns");
        }
    }
    
    /// <summary>
    /// Public method called by FireBreathParticleSpawner when hitting surfaces
    /// </summary>
    public void HandleSurfaceFireHit(Vector3 position)
    {
        SpawnFireParticleAtSurface(position);
    }
    
    /// <summary>
    /// Public method called by FireBreathParticleSpawner when hitting player or allies
    /// </summary>
    public void HandlePlayerFireHit(Transform target)
    {
        AttachFireParticleToPlayer(target);
    }
    
    /// <summary>
    /// Public method to get the surface layer mask for FireBreathParticleSpawner
    /// </summary>
    public LayerMask GetSurfaceLayerMask()
    {
        return surfaceLayerMask;
    }
    
    /// <summary>
    /// Debug method to help identify what layers are being used
    /// </summary>
    public void LogLayerInfo()
    {
        Debug.Log($"DragonBoss: Surface layer mask = {surfaceLayerMask.value}");
        Debug.Log($"Default layer = {LayerMask.NameToLayer("Default")} (bit: {1 << LayerMask.NameToLayer("Default")})");
        Debug.Log($"Entities layer = {LayerMask.NameToLayer("Entities")} (bit: {1 << LayerMask.NameToLayer("Entities")})");
        Debug.Log($"Player layer = {LayerMask.NameToLayer("Player")} (bit: {1 << LayerMask.NameToLayer("Player")})");
        Debug.Log($"Ground layer = {LayerMask.NameToLayer("Ground")} (bit: {1 << LayerMask.NameToLayer("Ground")})");
        
        // Check what's around us
        Collider2D[] nearbyColliders = Physics2D.OverlapCircleAll(transform.position, 15f);
        Debug.Log($"Found {nearbyColliders.Length} colliders nearby:");
        foreach (var col in nearbyColliders)
        {
            string layerName = LayerMask.LayerToName(col.gameObject.layer);
            bool hasComposite = col.GetComponent<CompositeCollider2D>() != null;
            bool hasTilemap = col.GetComponent<TilemapCollider2D>() != null;
            Debug.Log($"  - {col.name} on layer {col.gameObject.layer} ({layerName}) - Composite: {hasComposite}, Tilemap: {hasTilemap}");
        }
    }
    
    /// <summary>
    /// Test raycast from dragon position to find ground
    /// </summary>
    public void TestGroundRaycast()
    {
        Vector3 startPos = transform.position;
        Debug.Log($"DragonBoss: Testing raycast from {startPos}");
        
        // Test with all layers
        RaycastHit2D hit = Physics2D.Raycast(startPos, Vector2.down, 20f, -1);
        if (hit.collider != null)
        {
            string layerName = LayerMask.LayerToName(hit.collider.gameObject.layer);
            Debug.Log($"DragonBoss: Hit {hit.collider.name} on layer {hit.collider.gameObject.layer} ({layerName}) at {hit.point}");
        }
        else
        {
            Debug.Log("DragonBoss: No ground found with raycast");
        }
        
        // Test with surface layer mask
        RaycastHit2D hit2 = Physics2D.Raycast(startPos, Vector2.down, 20f, surfaceLayerMask);
        if (hit2.collider != null)
        {
            string layerName = LayerMask.LayerToName(hit2.collider.gameObject.layer);
            Debug.Log($"DragonBoss: Surface mask hit {hit2.collider.name} on layer {hit2.collider.gameObject.layer} ({layerName}) at {hit2.point}");
        }
        else
        {
            Debug.Log($"DragonBoss: No surface found with layer mask {surfaceLayerMask.value}");
        }
    }
    
    // === ANIMATION EVENT HANDLERS ===
    
    /// <summary>
    /// Called by Animation Event when attack animation completes
    /// Note: We use timer-based completion but keep this to avoid Unity warnings
    /// </summary>
    public void OnAttackAnimationComplete()
    {
        Debug.Log("Animation Event: OnAttackAnimationComplete called (timer-based system handles actual completion)");
    }
    
    /// <summary>
    /// Test method to verify Animation Events work at all
    /// </summary>
    public void TestAnimationEvent()
    {
        // Debug.Log("Animation Event: TestAnimationEvent fired"); // Commented out to reduce console spam
    }
    
    /// <summary>
    /// Called by Animation Event when rising animation completes
    /// Note: Disabled to prevent interference with timer-based flying system
    /// </summary>
    public void OnRisingAnimationComplete()
    {
        Debug.Log("Animation Event: OnRisingAnimationComplete called (DISABLED - using timer-based system)");
        // Disabled - the timer-based system handles rising completion
    }
    
    /// <summary>
    /// Called by Animation Event when landing animation completes  
    /// Note: Disabled to prevent interference with timer-based flying system
    /// </summary>
    public void OnLandingAnimationComplete()
    {
        Debug.Log("Animation Event: OnLandingAnimationComplete called (DISABLED - using timer-based system)");
        // Disabled - the timer-based system handles landing completion
    }

    // === HEALTH SYSTEM ===
    
    public void TakeDamage(int damage)
    {
        if (isDead) return;
        
        currentHealth -= damage;
        currentHealth = Mathf.Max(0, currentHealth);
        
        // Create damage number
        DamageObject.CreateDamageNumber(damage, transform.position + Vector3.up * 2f);
        
        // Update health bar target
        targetFillAmount = (float)currentHealth / maxHealth;
        
        Debug.Log($"DragonBoss took {damage} damage! Health: {currentHealth}/{maxHealth}");
        
        // Check for health-based buffs
        CheckHealthThresholds();
        
        // Flash effect - only if not dying
        if (spriteRenderer != null && currentHealth > 0)
        {
            StartCoroutine(FlashRed());
        }
        
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// Check health thresholds and apply passive buffs
    /// </summary>
    private void CheckHealthThresholds()
    {
        float healthPercentage = (float)currentHealth / maxHealth;
        
        // Half health buff: +1 flyover
        if (healthPercentage <= 0.5f && !halfHealthBuffActivated)
        {
            halfHealthBuffActivated = true;
            flyOverCount = baseFlyOverCount + 1;
            Debug.Log($"DragonBoss: Half health reached! Flyover count increased to {flyOverCount} (was {baseFlyOverCount})");
        }
        
        // Quarter health buff: +1 additional flyover (total +2)
        if (healthPercentage <= 0.25f && !quarterHealthBuffActivated)
        {
            quarterHealthBuffActivated = true;
            flyOverCount = baseFlyOverCount + 2;
            Debug.Log($"DragonBoss: Quarter health reached! Flyover count increased to {flyOverCount} (was {baseFlyOverCount})");
        }
    }

    /// <summary>
    /// Get current flyover count for debugging
    /// </summary>
    public int GetCurrentFlyoverCount()
    {
        return flyOverCount;
    }

    /// <summary>
    /// Get health percentage for debugging
    /// </summary>
    public float GetHealthPercentage()
    {
        return (float)currentHealth / maxHealth;
    }

    private IEnumerator FlashRed()
    {
        if (spriteRenderer != null && !isDead)
        {
            // Use the stored original color to ensure consistency
            spriteRenderer.color = Color.red;
            yield return new WaitForSeconds(0.1f);
            
            // Only restore if still alive (not dead)
            if (!isDead && spriteRenderer != null)
            {
                spriteRenderer.color = originalColor;
            }
        }
    }    public void Die()
    {
        if (isDead) return;
        
        isDead = true;
        Debug.Log("DragonBoss died");
        
        // CRITICAL: Stop all coroutines to prevent jump/landing mechanics from continuing
        StopAllCoroutines();
        
        // CRITICAL: Clean up any remaining damage objects created by this dragon
        CleanupDamageObjects();
        
        // Change sprite color to death color
        if (spriteRenderer != null)
        {
            spriteRenderer.color = deathColor;
        }
        
        // CRITICAL: Immediately reset physics and stop all movement
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.gravityScale = 1f; // Restore normal gravity in case it was disabled during flying
            rb.bodyType = RigidbodyType2D.Kinematic; // Make kinematic to prevent physics interactions
        }
        
        // CRITICAL: Reset all movement/attack states immediately
        isAttacking = false;
        isGrounded = true;
        isRising = false;
        isLanding = false;
        
        // Update animator with all states cleared
        if (animator != null)
        {
            animator.SetBool("isDead", true);
            animator.SetBool("isAttacking", false);
            animator.SetBool("isRising", false);
            animator.SetBool("isLanding", false);
            animator.SetBool("isWalking", false); // Ensure walking animation stops
        }
        
        // Start respawn countdown
        StartCoroutine(RespawnCountdown());
    }

    private void CleanupDamageObjects()
    {
        // Find and destroy all damage objects created by this dragon
        // They typically have names starting with "Dragon"
        GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        
        foreach (GameObject obj in allObjects)
        {
            if (obj != null && obj.name.StartsWith("Dragon") && obj != gameObject)
            {
                // Check if it has DamageObject component (our damage zones)
                DamageObject damageComp = obj.GetComponent<DamageObject>();
                if (damageComp != null)
                {
                    Debug.Log($"DragonBoss: Cleaning up damage object: {obj.name}");
                    Destroy(obj);
                }
            }
        }
        
        // Also clean up any current damage zone references
        if (inDamageZone)
        {
            inDamageZone = false;
            currentDamageObject = null;
        }
    }
    
    public void Respawn()
    {
        isDead = false;
        currentHealth = maxHealth;
        targetFillAmount = 1f;
        currentFillAmount = 1f;
        
        // Reset position
        transform.position = spawnPosition;
        
        // CRITICAL: Reset all states properly
        isAttacking = false;
        isGrounded = true;
        isRising = false;
        isLanding = false;
        
        // Reset timers
        lastMeleeAttackTime = 0f;
        lastFireBreathTime = 0f;
        lastFlyingAttackTime = 0f;
        lastJumpTime = 0f;
        
        // Reset flyover count to original value
        flyOverCount = baseFlyOverCount;
        Debug.Log($"DragonBoss: Respawned - flyover count reset to original value {baseFlyOverCount}");
        
        // Restore original appearance
        if (spriteRenderer != null)
        {
            spriteRenderer.color = originalColor;
        }
        
        // CRITICAL: Restore physics properly
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero; // Clear any residual velocity
            rb.gravityScale = 1f; // Restore normal gravity
            rb.bodyType = RigidbodyType2D.Dynamic; // Re-enable physics interactions
        }
        
        // Update animator with all states properly reset
        if (animator != null)
        {
            animator.SetBool("isDead", false);
            animator.SetBool("isAttacking", false);
            animator.SetBool("isRising", false);
            animator.SetBool("isLanding", false);
        }
        
        Debug.Log("DragonBoss respawned");
    }
    
    private IEnumerator RespawnCountdown()
    {
        float timeRemaining = respawnDelay;
        
        while (timeRemaining > 0)
        {
            if (showRespawnTimer && respawnTimerText != null)
            {
                respawnTimerText.text = $"Respawning in: {timeRemaining:F1}s";
            }
            
            timeRemaining -= Time.deltaTime;
            yield return null;
        }
        
        if (respawnTimerText != null)
        {
            respawnTimerText.text = "";
        }
        
        Respawn();
    }
    
    // === DAMAGE OBJECT INTEGRATION ===
    
    void OnTriggerEnter2D(Collider2D other)
    {
        if (isDead) return; // Don't process triggers when dead
        
        DamageObject damageObject = other.GetComponent<DamageObject>();
        if (damageObject != null)
        {
            Debug.Log($"DragonBoss: Triggered with damage object {damageObject.gameObject.name}. Dragon layer: {gameObject.layer} ({LayerMask.LayerToName(gameObject.layer)}), Exclude layers: {damageObject.excludeLayers.value}");
            
            // FIRST: Check if this damage object excludes our layer
            int dragonLayerBit = (1 << gameObject.layer);
            bool isExcluded = (damageObject.excludeLayers.value & dragonLayerBit) != 0;
            Debug.Log($"DragonBoss: Layer check - dragonLayerBit: {dragonLayerBit}, isExcluded: {isExcluded}");
            
            if (isExcluded)
            {
                Debug.Log($"DragonBoss: Excluded from damage object {damageObject.gameObject.name} due to layer exclusion");
                return; // Exit early - don't process this damage object at all
            }
            
            // SECOND: Check if this damage object can actually damage enemies
            if (!damageObject.canDamageEnemies)
            {
                Debug.Log($"DragonBoss: Ignoring damage object {damageObject.gameObject.name} - canDamageEnemies is false");
                return; // Don't enter damage zone for objects that can't damage enemies
            }
            
            // If we get here, the damage object can affect the dragon
            inDamageZone = true;
            currentDamageObject = damageObject;
            lastDamageTime = Time.time - damageObject.damageRate; // Allow immediate damage
            
            Debug.Log($"DragonBoss entered damage zone: {damageObject.gameObject.name} with {damageObject.damageAmount} damage");
        }
    }
    
    void OnTriggerExit2D(Collider2D other)
    {
        DamageObject damageObject = other.GetComponent<DamageObject>();
        if (damageObject != null && damageObject == currentDamageObject)
        {
            inDamageZone = false;
            currentDamageObject = null;
            Debug.Log($"DragonBoss exited damage zone: {damageObject.gameObject.name}");
        }
    }
    
    // Damage Object Integration - Collision Events (like regular enemies)
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (isDead) return; // Don't process collisions when dead
        
        DamageObject damageObj = collision.collider.GetComponent<DamageObject>();
        if (damageObj != null)
        {
            // Check if this damage object can actually damage enemies
            if (!damageObj.canDamageEnemies)
            {
                Debug.Log($"DragonBoss: Ignoring collision damage object {damageObj.gameObject.name} - canDamageEnemies is false");
                return;
            }
            
            // Additional check: don't damage ourselves if we're tagged as Enemy
            if (gameObject.CompareTag("Enemy") && !damageObj.canDamageEnemies)
            {
                Debug.Log($"DragonBoss: Ignoring collision damage object due to Enemy tag");
                return;
            }
            
            // Check layer exclusions
            bool canTakeDamage = ((damageObj.excludeLayers.value & (1 << gameObject.layer)) == 0);
            
            if (canTakeDamage)
            {
                // Take damage immediately on collision
                TakeDamage(damageObj.damageAmount);
                
                // Trigger callback for weapon passives
                if (damageObj.onEnemyHit != null)
                {
                    damageObj.onEnemyHit.Invoke();
                }
                
                Debug.Log($"DragonBoss took collision damage: {damageObj.damageAmount} from {damageObj.gameObject.name}");
            }
            else
            {
                Debug.Log($"DragonBoss: Excluded from collision damage object {damageObj.gameObject.name} due to layer exclusion");
            }
        }
    }
    
    // === DEBUG GIZMOS ===
    
    void OnDrawGizmosSelected()
    {
        // Detection radius
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        
        // Melee attack range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, meleeAttackRange);
        
        // Fire breath range
        Gizmos.color = Color.orange;
        Gizmos.DrawWireSphere(transform.position, fireBreathRange);
        Gizmos.DrawWireSphere(transform.position, fireBreathMinRange);
        
        // Fly height indicator
        Gizmos.color = Color.cyan;
        Vector3 flyPosition = transform.position + Vector3.up * flyHeight;
        Gizmos.DrawWireCube(flyPosition, Vector3.one * 0.5f);
        
        // Attack hitbox previews
        if (Application.isPlaying)
        {
            // Melee attack preview
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Vector3 meleePos = transform.position + (Vector3.right * (isFacingRight ? meleeAttackRange * 0.5f : -meleeAttackRange * 0.5f));
            Gizmos.DrawCube(meleePos, meleeAttackSize);
            
            // Flying attack preview - show damage zone position below and forward of dragon
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            Vector2 flyingPreviewSize = new Vector2(flyAttackWidth, flyingAttackSize.y);
            float previewForwardOffset = isFacingRight ? flyingAttackForwardOffset : -flyingAttackForwardOffset;
            Vector3 flyingPreviewPos = transform.position + new Vector3(previewForwardOffset, -flyingAttackDepth, 0);
            Gizmos.DrawCube(flyingPreviewPos, flyingPreviewSize);
        }
    }
}

// Helper component to make damage zones follow the dragon during flying attacks
public class FlyingDamageFollower : MonoBehaviour
{
    public Transform target;
    public Vector3 offset; // Offset from target position
    private bool debugLogged = false;
    
    void Start()
    {
        Debug.Log($"FlyingDamageFollower: Started following {(target ? target.name : "NULL")} from position {transform.position} with offset {offset}");
    }
    
    void Update()
    {
        if (target != null)
        {
            Vector3 oldPos = transform.position;
            transform.position = target.position + offset;
            
            if (!debugLogged)
            {
                Debug.Log($"FlyingDamageFollower: Moving from {oldPos} to {transform.position} (following {target.name} with offset {offset})");
                debugLogged = true; // Only log once to avoid spam
            }
        }
        else if (!debugLogged)
        {
            Debug.LogError("FlyingDamageFollower: Target is NULL!");
            debugLogged = true;
        }
    }
}

// Helper component to make fire blocks follow the dragon during fire breath attacks
public class FireBlockFollower : MonoBehaviour
{
    public Transform target;
    public Vector3 offset; // Offset from target position
    private bool debugLogged = false;
    
    void Start()
    {
        Debug.Log($"FireBlockFollower: Started following {(target ? target.name : "NULL")} from position {transform.position} with offset {offset}");
    }
    
    void Update()
    {
        if (target != null)
        {
            Vector3 oldPos = transform.position;
            transform.position = target.position + offset;
            
            if (!debugLogged)
            {
                Debug.Log($"FireBlockFollower: Moving from {oldPos} to {transform.position} (following {target.name} with offset {offset})");
                debugLogged = true; // Only log once to avoid spam
            }
        }
        else if (!debugLogged)
        {
            Debug.LogError("FireBlockFollower: Target is NULL!");
            debugLogged = true;
        }
    }
}