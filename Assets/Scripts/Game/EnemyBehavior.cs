using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class EnemyBehavior : MonoBehaviour
{
    [Header("Enemy Settings")]
    [SerializeField] protected int maxHealth = 50;
    [SerializeField] protected float respawnDelay = 3f;
    
    [Header("Health Bar")]
    [SerializeField] protected Vector3 healthBarOffset = new Vector3(0, 1.5f, 0); // Offset above enemy
    [SerializeField] protected Vector2 healthBarSize = new Vector2(1.5f, 0.2f); // Width and height of health bar
    
    [Header("Death Settings")]
    [SerializeField] protected Color deathColor = new Color(0.5f, 0.5f, 0.5f, 0.7f); // Gray and semi-transparent
    [SerializeField] protected bool showRespawnTimer = true;
    
    [Header("Aggression Settings")]
    [SerializeField] protected bool isAggressive = false; // Enable enemy aggression
    [SerializeField] protected float detectionRadius = 5f; // Radius to detect and follow player
    [SerializeField] protected float followSpeed = 2f; // Speed when following player
    [SerializeField] protected float attackRange = 2f; // Range to attack player
    [SerializeField] protected float attackCooldown = 2f; // Time between attacks
    [SerializeField] protected float attackDamage = 15f; // Damage dealt by enemy attacks
    [SerializeField] protected float attackDuration = 0.8f; // How long attack damage object lasts (increased for longer animations)
    [SerializeField] protected Vector2 attackSize = new Vector2(1f, 1f); // Size of attack damage object
    [SerializeField] protected float jumpForce = 8f; // Force applied when enemy jumps
    [SerializeField] protected float jumpCooldown = 1f; // Time between jumps
    [SerializeField] protected float minHeightDifferenceToJump = 1.5f; // Minimum height difference to trigger jump
    
    [Header("Animation Settings")]
    [SerializeField] protected bool alternateAttacks = true; // Whether to alternate between attack animations
    
    [Header("Collision")]
    [SerializeField] protected LayerMask playerLayerMask = 1; // What layers count as player
    
    // Private variables
    protected SpriteRenderer spriteRenderer;
    protected Animator animator;
    protected Collider2D enemyCollider;
    protected Rigidbody2D rb;
    protected int currentHealth;
    protected bool isDead = false;
    protected Vector3 spawnPosition;
    protected Color originalColor;
    
    // Aggression System
    protected Transform playerTransform;
    protected float lastAttackTime = 0f;
    protected bool isFacingRight = true;
    protected float lastJumpTime = 0f;
    
    // Animation System
    protected bool useNextAttackAnimation = false; // For alternating attacks
    protected bool isAttacking = false; // To prevent animation conflicts during attacks
    
    // Damage Object Integration
    protected bool inDamageZone = false;
    protected float lastDamageTime = 0f;
    protected DamageObject currentDamageObject;

    // Shock Status Effect System
    protected bool isShocked = false;
    protected float shockEndTime = 0f;
    protected int shockStacks = 0;
    protected const int maxShockStacks = 8;
    protected const float shockBaseDuration = 1f;
    
    // Public Properties
    public bool IsDead => isDead;
    
        // Health Bar UI
    protected Canvas healthBarCanvas;
    protected Image healthBarBackground;
    protected Image healthBarFill;
    protected GameObject healthBarObject;
    protected float targetFillAmount = 1f; // Target fill amount for smooth animation
    protected float currentFillAmount = 1f; // Current animated fill amount
    protected float healthBarAnimationSpeed = 3f; // Speed of health bar animation
    protected Text respawnTimerText; // Timer text for respawn countdown
    
    protected virtual void Start()
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
        
        // Cache the nearest live player for multiplayer targeting.
        playerTransform = FindNearestPlayerTarget();
        
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
    
    protected virtual void Update()
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

        // Handle shock status effect (must be before aggression check)
        HandleShockEffect();
        
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
        if (isAggressive && !isShocked)
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

    protected virtual bool ValidateComponents()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
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
            playerTransform = FindNearestPlayerTarget();
        }

        return true;
    }
    
    protected virtual void UpdateAnimations()
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
    
    protected virtual void PlayAttackAnimation(Transform target)
    {
        isAttacking = true;

        OnAttackAnimationStarted(target);

        if (animator != null)
        {
            animator.SetInteger("attackType", GetAttackAnimationType());
            animator.SetBool("isAttacking", true);
        }

        StartAttackEventFallback();
        StartCoroutine(ClearAttackingFlag());
    }

    protected virtual int GetAttackAnimationType()
    {
        if (!alternateAttacks)
        {
            return 0;
        }

        int attackType = useNextAttackAnimation ? 1 : 0;
        useNextAttackAnimation = !useNextAttackAnimation;
        return attackType;
    }

    protected virtual void OnAttackAnimationStarted(Transform target)
    {
    }

    protected virtual void StartAttackEventFallback()
    {
    }

    protected virtual void ExecuteAttack(Transform target)
    {
        AttackDummy dummy = target.GetComponent<AttackDummy>();
        if (dummy != null)
        {
            dummy.TakeDamage(attackDamage);
            Debug.Log($"Enemy attacked dummy for {attackDamage} damage");
            return;
        }

        PlayerMovement playerMovement = target.GetComponent<PlayerMovement>();
        if (playerMovement != null)
        {
            playerMovement.TakeDamageFromObject((int)attackDamage);
            Debug.Log($"Enemy attacked player for {attackDamage} damage");
        }
    }

    protected virtual void ResetVariantState()
    {
    }
    
    protected virtual IEnumerator ClearAttackingFlag()
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
    protected virtual void OnTriggerEnter2D(Collider2D other)
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
    
    protected virtual void OnTriggerExit2D(Collider2D other)
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
    protected virtual void OnCollisionEnter2D(Collision2D collision)
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
    
    protected virtual void OnCollisionExit2D(Collision2D collision)
    {
        if (isDead) return; // Don't process collisions when dead
        
        DamageObject damageObj = collision.gameObject.GetComponent<DamageObject>();
        if (damageObj != null && currentDamageObject == damageObj)
        {
            inDamageZone = false;
            currentDamageObject = null;
        }
    }
    
    protected virtual void HandleAggression()
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

    protected virtual Transform FindNearestTarget()
    {
        Transform nearestTarget = null;
        float nearestDistance = float.MaxValue;

        Transform nearestPlayer = FindNearestPlayerTarget();
        playerTransform = nearestPlayer;

        if (nearestPlayer != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, nearestPlayer.position);
            if (distanceToPlayer < nearestDistance)
            {
                nearestDistance = distanceToPlayer;
                nearestTarget = nearestPlayer;
            }
        }
        
        // Check for nearby attack dummies without relying on scene tags.
        AttackDummy[] dummies = FindObjectsByType<AttackDummy>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (AttackDummy dummy in dummies)
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

            protected virtual Transform FindNearestPlayerTarget()
            {
                PlayerMovement[] players = FindObjectsByType<PlayerMovement>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                Transform nearestPlayer = null;
                float nearestDistance = float.MaxValue;

                foreach (PlayerMovement player in players)
                {
                    if (!IsValidPlayerTarget(player))
                    {
                        continue;
                    }

                    float distanceToPlayer = Vector3.Distance(transform.position, player.transform.position);
                    if (distanceToPlayer < nearestDistance)
                    {
                        nearestDistance = distanceToPlayer;
                        nearestPlayer = player.transform;
                    }
                }

                return nearestPlayer;
            }

            protected virtual bool IsValidPlayerTarget(PlayerMovement player)
            {
                return player != null && player.isActiveAndEnabled && player.gameObject.activeInHierarchy && !player.IsDead;
            }

    protected virtual void FollowTarget(Transform target)
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

    protected virtual void AttackTarget(Transform target)
    {
        if (target == null) return;
        
        // Face the target before attacking
        FaceTarget(target);
        
        // Play attack animation
        PlayAttackAnimation(target);

        ExecuteAttack(target);
        lastAttackTime = Time.time;
    }
    
    protected virtual void FollowPlayer()
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
    
    protected virtual void FaceTarget(Transform target)
    {
        if (spriteRenderer == null || target == null) return;
        
        bool shouldFaceRight = target.position.x > transform.position.x;
        
        if (shouldFaceRight != isFacingRight)
        {
            isFacingRight = shouldFaceRight;
            spriteRenderer.flipX = !isFacingRight; // Flip sprite based on direction
        }
    }
    
    protected virtual void AttackPlayer()
    {
        if (playerTransform == null) return;
        
        lastAttackTime = Time.time;
        
        // Create attack damage object in direction of player
        Vector3 attackDirection = (playerTransform.position - transform.position).normalized;
        Vector3 attackPosition = transform.position + attackDirection * (attackRange * 0.7f); // Slightly in front of enemy
        
        StartCoroutine(CreateAttackDamageObject(attackPosition));
    }
    
    protected virtual IEnumerator CreateAttackDamageObject(Vector3 position)
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
        
        // Visual indicator (temporary - will be replaced with graphics later) - COMMENTED OUT FOR INVISIBILITY
        SpriteRenderer attackRenderer = attack.AddComponent<SpriteRenderer>();
        
        // Create red attack sprite - COMMENTED OUT FOR INVISIBILITY
        Texture2D attackTexture = new Texture2D(32, 32);
        Color[] pixels = new Color[32 * 32];
        for (int i = 0; i < pixels.Length; i++)
        {
            // pixels[i] = new Color(1f, 0.3f, 0f, 0.7f); // Orange-red color - VISIBLE
            pixels[i] = new Color(1f, 0.3f, 0f, 0f); // Fully transparent (invisible)
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
    
    protected virtual void SetupCollisionLayers()
    {
        // Set this GameObject to Entities layer
        gameObject.layer = LayerMask.NameToLayer("Entities");
        
        // Ignore collisions with all active players in multiplayer.
        PlayerMovement[] players = FindObjectsByType<PlayerMovement>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (PlayerMovement player in players)
        {
            if (player == null)
            {
                continue;
            }

            Collider2D playerCollider = player.GetComponent<Collider2D>();
            if (playerCollider != null && enemyCollider != null)
            {
                Physics2D.IgnoreCollision(enemyCollider, playerCollider, true);
            }
        }
        
        // Make entities ignore collisions with each other
        SetupEntityToEntityCollisionIgnoring();
    }

    // ===== SHOCK STATUS EFFECT =====

    protected virtual void HandleShockEffect()
    {
        if (!isShocked) return;

        if (Time.time >= shockEndTime)
        {
            isShocked = false;
            shockStacks = 0;
            if (spriteRenderer != null)
                spriteRenderer.color = originalColor;
            Debug.Log($"Enemy {gameObject.name}: Shock ended");
        }
        else if (rb != null)
        {
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        }
    }

    public virtual void ApplyShock(float duration = -1f)
    {
        if (isDead) return;

        float shockDuration = duration > 0f ? duration : shockBaseDuration;

        if (isShocked)
        {
            if (shockStacks < maxShockStacks)
            {
                shockStacks++;
                float timeRemaining = Mathf.Max(shockEndTime - Time.time, 0f);
                shockEndTime = Time.time + timeRemaining + shockDuration;
                Debug.Log($"Enemy {gameObject.name}: Shock stacked to {shockStacks}. Duration: {shockEndTime - Time.time:F1}s");
            }
        }
        else
        {
            isShocked = true;
            shockStacks = 1;
            shockEndTime = Time.time + shockDuration;
            if (spriteRenderer != null)
                spriteRenderer.color = new Color(0f, 0.9f, 1f, 1f);
            Debug.Log($"Enemy {gameObject.name}: Shock applied for {shockDuration:F1}s");
        }
    }

    // ===== END SHOCK STATUS EFFECT =====
    
    protected virtual void SetupEntityToEntityCollisionIgnoring()
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
    
    public virtual void TakeDamage(int damage)
    {
        if (isDead) return;
        
        currentHealth -= damage;
        currentHealth = Mathf.Max(0, currentHealth);
        
        // Create damage number
        DamageObject.CreateDamageNumber(damage, transform.position + Vector3.up * 1.5f);
        
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
    
    protected virtual void Die()
    {
        if (isDead) return;
        
        isDead = true;
        ResetVariantState();
        isShocked = false;
        shockStacks = 0;
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
    
    protected virtual IEnumerator RespawnCountdown()
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
    
    protected virtual void Respawn()
    {
        Debug.Log($"Enemy {gameObject.name} respawning");
        
        // Reset position to spawn point
        transform.position = spawnPosition;
        
        // Reset health
        currentHealth = maxHealth;
        isDead = false;
        ResetVariantState();
        
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

        isShocked = false;
        shockStacks = 0;
        
        // Update health bar
        UpdateHealthBar();
        
        // Re-setup collision layers (in case something changed)
        SetupCollisionLayers();
        
        playerTransform = FindNearestPlayerTarget();
    }
    
    protected virtual void CreateHealthBar()
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
    
    protected virtual void UpdateHealthBarPosition()
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
    
    protected virtual void UpdateHealthBar()
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
    
    protected virtual IEnumerator SetupCollisionIgnoringDelayed()
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
    
    protected virtual void OnDestroy()
    {
        ResetVariantState();

        // Clean up health bar when enemy is destroyed
        if (healthBarCanvas != null)
        {
            Destroy(healthBarCanvas.gameObject);
        }
    }
}