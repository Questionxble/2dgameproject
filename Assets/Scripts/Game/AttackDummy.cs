using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class AttackDummy : MonoBehaviour
{
    [Header("Dummy Configuration")]
    [SerializeField] private float maxHealth = 50f;
    [SerializeField] private float attackDamage = 15f;
    [SerializeField] private float attackRange = 5f; // Increased attack range
    [SerializeField] private float attackCooldown = 1.5f;
    [SerializeField] private float moveSpeed = 4f; // Increased base speed
    [SerializeField] private float followDistance = 12f; // Increased follow distance
    
    [Header("Health Bar")]
    [SerializeField] private Vector2 healthBarSize = new Vector2(1.5f, 0.2f); // Match enemy health bar size
    [SerializeField] private Vector3 healthBarOffset = new Vector3(0f, 1.5f, 0f); // Match enemy health bar offset
    
    [Header("Animation Settings")]
    [SerializeField] private bool alternateAttacks = true; // Whether to alternate between attack animations
    
    [Header("Jump Settings")]
    [SerializeField] private float jumpForce = 8f; // Force applied when dummy jumps
    [SerializeField] private float jumpCooldown = 1f; // Time between jumps
    [SerializeField] private float minHeightDifferenceToJump = 1.5f; // Minimum height difference to trigger jump
    [SerializeField] private float jumpDetectionDistance = 2f; // How far ahead to check for surfaces
    [SerializeField] private LayerMask groundLayerMask = 1; // What layers count as ground for jump detection
    
    [Header("Death Settings")]
    [SerializeField] private Color deathColor = new Color(0.5f, 0.5f, 0.5f, 0.7f); // Gray and semi-transparent
    [SerializeField] private float deathAnimationDuration = 2f; // How long death animation plays before destruction
    
    [Header("Burning Status Effect")]
    [SerializeField] private float burnDamageRate = 2f; // Damage per second while burning
    [SerializeField] private float burnDamageInterval = 1f; // Time between burn damage applications
    [SerializeField] private float baseBurnDuration = 5f; // Base duration of burning effect
    [SerializeField] private Sprite burnEffectSprite; // Sprite for burning visual effect
    
    // Components and references
    private float currentHealth;
    private GameObject player;
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Animator animator;
    
    // Combat system
    private float lastAttackTime = 0f;
    private GameObject currentTarget = null;
    
    // Health bar UI
    private Canvas healthBarCanvas;
    private Image healthBarBackground;
    private Image healthBarFill;
    
    // State machine
    private enum DummyState { Following, Attacking, Returning }
    private DummyState currentState = DummyState.Following;
    
    // Animation System
    private bool useNextAttackAnimation = false; // For alternating attacks
    private bool isAttacking = false; // To prevent animation conflicts during attacks
    private bool isDead = false; // Death state for animations
    
    // Jump System
    private float lastJumpTime = 0f;
    
    // Death System
    private Color originalColor;
    
    // Burning Status Effect System
    private bool isBurning = false;
    private float burnEndTime = 0f;
    private float nextBurnDamageTime = 0f;
    private GameObject burnVisualEffect;
    
    // Track active attacks for cleanup
    private System.Collections.Generic.List<GameObject> activeAttackObjects = new System.Collections.Generic.List<GameObject>();
    private System.Collections.Generic.List<Coroutine> activeAttackCoroutines = new System.Collections.Generic.List<Coroutine>();
    private bool hasBeenCleaned = false;
    
    void Start()
    {
        // Initialize health
        currentHealth = maxHealth;
        
        // Find player
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj;
        }
        else
        {
            Debug.LogError("AttackDummy: Could not find player with 'Player' tag!");
        }
        
        // Get components
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        
        // Store original color for death animation
        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }
        
        // Get animator component
        animator = GetComponent<Animator>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        
        // Ensure proper setup - will be configured as dynamic in SetupColliders
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
        }
        // Note: Full rb configuration will be done in SetupColliders method
        
        // Set up colliders properly
        SetupColliders();
        
        // Create health bar
        CreateHealthBar();
        
        Debug.Log($"Attack Dummy spawned with {maxHealth} HP");
    }
    
    private void SetupColliders()
    {
        // Main physics collider for ground collision
        Collider2D mainCollider = GetComponent<Collider2D>();
        if (mainCollider == null)
        {
            mainCollider = gameObject.AddComponent<BoxCollider2D>();
        }
        
        // Make it a physical collider for ground interaction
        mainCollider.isTrigger = false; // Physical collider for ground collision
        
        // Create physics material for ground interaction
        PhysicsMaterial2D dummyMaterial = new PhysicsMaterial2D("DummyMaterial");
        dummyMaterial.friction = 0.4f; // Good friction for ground traction
        dummyMaterial.bounciness = 0f; // No bounce
        mainCollider.sharedMaterial = dummyMaterial;
        
        // Set up rigidbody for proper physics
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic; // Dynamic for ground collision
            rb.gravityScale = 1f; // Normal gravity
            rb.freezeRotation = true; // Prevent rotation
            rb.linearDamping = 2f; // Some drag to prevent sliding
        }
        
        // Log layer info for debugging collision issues
        Debug.Log($"AttackDummy layer: {LayerMask.LayerToName(gameObject.layer)} ({gameObject.layer})");
        Debug.Log($"Player layer: {LayerMask.NameToLayer("Player")}");
        Debug.Log($"Entities layer: {LayerMask.NameToLayer("Entities")}");
        Debug.Log($"Enemy layer: {LayerMask.NameToLayer("Enemy")}");
        
        // Create a separate attack trigger collider for enemy detection only
        GameObject attackTrigger = new GameObject("AttackTrigger");
        attackTrigger.transform.SetParent(transform);
        attackTrigger.transform.localPosition = Vector3.zero;
        attackTrigger.layer = gameObject.layer; // Same layer as dummy
        
        CircleCollider2D attackCollider = attackTrigger.AddComponent<CircleCollider2D>();
        attackCollider.radius = attackRange;
        attackCollider.isTrigger = true;
        
        // Add the AttackTrigger component to handle enemy detection only
        AttackTrigger triggerComponent = attackTrigger.AddComponent<AttackTrigger>();
        triggerComponent.parentDummy = this;
        
        Debug.Log("AttackDummy: Colliders set up - dynamic rigidbody with ground collision, separate enemy detection trigger");
    }
    
    // Handle collision detection to prevent unwanted physical interactions
    void OnCollisionEnter2D(Collision2D collision)
    {
        // Check if we're colliding with player or enemies and ignore those collisions
        GameObject other = collision.gameObject;
        
        if (other.CompareTag("Player") || other.CompareTag("Enemy") || other.CompareTag("PlayerSummon"))
        {
            // Ignore physics collision with these objects
            Physics2D.IgnoreCollision(GetComponent<Collider2D>(), collision.collider);
            Debug.Log($"AttackDummy: Ignoring collision with {other.name} (tag: {other.tag})");
        }
    }
    
    void Update()
    {
        if (player == null) return;
        
        // Clean up null coroutine references periodically
        CleanupNullCoroutines();
        
        // Handle burning status effect
        HandleBurningEffect();
        
        // Update state machine
        UpdateStateMachine();
        
        // Update animations based on current state
        UpdateAnimations();
        
        // Update health bar
        UpdateHealthBar();
    }
    
    private void CleanupNullCoroutines()
    {
        // Remove null coroutine references (completed coroutines become null)
        for (int i = activeAttackCoroutines.Count - 1; i >= 0; i--)
        {
            if (activeAttackCoroutines[i] == null)
            {
                activeAttackCoroutines.RemoveAt(i);
            }
        }
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
    
    private System.Collections.IEnumerator ClearAttackingFlag()
    {
        // Wait for attack duration plus a small buffer
        yield return new WaitForSeconds(0.3f + 0.1f); // attackDuration + buffer
        isAttacking = false;
        
        // Clear the attacking parameter in the animator
        if (animator != null)
        {
            animator.SetBool("isAttacking", false);
        }
    }
    
    private void UpdateStateMachine()
    {
        float distanceToPlayer = Vector3.Distance(transform.position, player.transform.position);
        
        switch (currentState)
        {
            case DummyState.Following:
                // Look for enemies to attack
                GameObject nearestEnemy = FindNearestEnemy();
                if (nearestEnemy != null)
                {
                    currentTarget = nearestEnemy;
                    currentState = DummyState.Attacking;
                }
                else if (distanceToPlayer > followDistance)
                {
                    // Follow player if too far away
                    MoveTowards(player.transform.position);
                }
                break;
                
            case DummyState.Attacking:
                // Check if target is null, too far, or dead
                EnemyBehavior targetEnemy = currentTarget?.GetComponent<EnemyBehavior>();
                if (currentTarget == null || Vector3.Distance(transform.position, currentTarget.transform.position) > attackRange * 2f || 
                    (targetEnemy != null && targetEnemy.IsDead))
                {
                    // Target lost, too far, or dead - return to following
                    currentTarget = null;
                    currentState = DummyState.Following;
                }
                else
                {
                    // Move towards target and attack
                    float distanceToTarget = Vector3.Distance(transform.position, currentTarget.transform.position);
                    
                    if (distanceToTarget <= attackRange)
                    {
                        // Face the target while in combat range
                        if (spriteRenderer != null && currentTarget != null)
                        {
                            bool shouldFaceRight = currentTarget.transform.position.x > transform.position.x;
                            spriteRenderer.flipX = !shouldFaceRight;
                        }
                        
                        // In attack range - attack if cooldown is ready
                        if (Time.time - lastAttackTime >= attackCooldown)
                        {
                            AttackTarget(currentTarget);
                        }
                    }
                    else
                    {
                        // Move towards target
                        MoveTowards(currentTarget.transform.position);
                    }
                }
                break;
                
            case DummyState.Returning:
                if (distanceToPlayer <= followDistance * 0.8f)
                {
                    currentState = DummyState.Following;
                }
                else
                {
                    MoveTowards(player.transform.position);
                }
                break;
        }
    }
    
    private void MoveTowards(Vector3 targetPosition)
    {
        Vector3 direction = (targetPosition - transform.position).normalized;
        float distance = Vector3.Distance(transform.position, targetPosition);
        
        // Only move if there's significant distance to avoid jittering
        if (distance < 0.5f) return;
        
        // Increase speed when far from target
        float currentSpeed = moveSpeed;
        if (distance > followDistance * 1.5f)
        {
            currentSpeed *= 3f; // Triple speed when far away
        }
        else if (distance > followDistance)
        {
            currentSpeed *= 1.5f; // 1.5x speed when moderately far
        }
        
        // Check for higher surfaces and jump if needed
        if (ShouldJumpToReachTarget(direction))
        {
            PerformJump();
        }
        
        // Apply horizontal force for dynamic rigidbody movement
        if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic)
        {
            // Use AddForce for natural physics movement
            Vector2 moveForce = new Vector2(direction.x * currentSpeed * 15f, 0f);
            rb.AddForce(moveForce, ForceMode2D.Force);
            
            // Limit maximum horizontal speed to prevent overshooting
            if (Mathf.Abs(rb.linearVelocity.x) > currentSpeed * 1.2f)
            {
                rb.linearVelocity = new Vector2(Mathf.Sign(rb.linearVelocity.x) * currentSpeed * 1.2f, rb.linearVelocity.y);
            }
        }
        else
        {
            // Fallback to direct transform movement if not using dynamic rigidbody
            Vector3 movement = new Vector3(direction.x, 0f, 0f) * currentSpeed * Time.deltaTime;
            transform.position += movement;
        }
        
        // Flip sprite based on movement direction
        if (spriteRenderer != null && Mathf.Abs(direction.x) > 0.1f)
        {
            spriteRenderer.flipX = direction.x < 0;
        }
    }
    
    private bool ShouldJumpToReachTarget(Vector3 direction)
    {
        // Don't jump if on cooldown or not grounded
        if (Time.time - lastJumpTime < jumpCooldown || !IsGrounded())
        {
            return false;
        }
        
        // Only check for jumps when moving horizontally
        if (Mathf.Abs(direction.x) < 0.1f)
        {
            return false;
        }
        
        // Cast a ray forward to detect surfaces
        Vector3 rayStart = transform.position + Vector3.up * 0.5f; // Start slightly above ground
        Vector3 rayDirection = new Vector3(Mathf.Sign(direction.x), 0f, 0f);
        
        RaycastHit2D hit = Physics2D.Raycast(rayStart, rayDirection, jumpDetectionDistance, groundLayerMask);
        
        if (hit.collider != null)
        {
            // Check if the surface is significantly higher than current position
            float heightDifference = hit.point.y - transform.position.y;
            
            // If there's a wall or surface that's higher than our jump threshold, we should jump
            if (heightDifference > minHeightDifferenceToJump)
            {
                Debug.Log($"AttackDummy: Detected surface {heightDifference:F2} units above, preparing to jump");
                return true;
            }
        }
        
        // Also check if there's a gap ahead that requires jumping over
        Vector3 groundCheckStart = transform.position + new Vector3(Mathf.Sign(direction.x) * 1f, -0.5f, 0f);
        RaycastHit2D groundHit = Physics2D.Raycast(groundCheckStart, Vector2.down, 2f, groundLayerMask);
        
        if (groundHit.collider == null)
        {
            // There's a gap ahead - we might need to jump over it
            Debug.Log("AttackDummy: Detected gap ahead, preparing to jump");
            return true;
        }
        
        return false;
    }
    
    private bool IsGrounded()
    {
        // Check if dummy is on the ground by casting a short ray downward
        Vector3 rayStart = transform.position;
        RaycastHit2D hit = Physics2D.Raycast(rayStart, Vector2.down, 1.1f, groundLayerMask);
        
        // Also check if vertical velocity is near zero (not already jumping/falling fast)
        bool velocityGrounded = rb == null || Mathf.Abs(rb.linearVelocity.y) < 0.1f;
        
        return hit.collider != null && velocityGrounded;
    }
    
    private void PerformJump()
    {
        if (rb == null) return;
        
        lastJumpTime = Time.time;
        
        // Apply upward force for jump
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        
        Debug.Log($"AttackDummy: Performed jump with force {jumpForce}");
    }
    
    private GameObject FindNearestEnemy()
    {
        // Find all potential targets within extended range
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, attackRange * 1.5f);
        GameObject nearestEnemy = null;
        float nearestDistance = float.MaxValue;
        
        foreach (Collider2D collider in colliders)
        {
            // Check for regular enemies with EnemyBehavior
            EnemyBehavior enemyBehavior = collider.GetComponent<EnemyBehavior>();
            DragonBoss dragonBoss = collider.GetComponent<DragonBoss>();
            
            // Target regular enemies OR the dragon boss
            bool isValidTarget = false;
            bool isDead = false;
            
            if (enemyBehavior != null)
            {
                isValidTarget = true;
                isDead = enemyBehavior.IsDead;
            }
            else if (dragonBoss != null)
            {
                isValidTarget = true;
                isDead = dragonBoss.IsDead; // Assuming DragonBoss has an IsDead property
            }
            
            if (isValidTarget && !isDead)
            {
                // Extra safety check - don't attack player or other summons
                if (collider.gameObject.tag != "Player" && collider.gameObject.tag != "PlayerSummon")
                {
                    float distance = Vector3.Distance(transform.position, collider.transform.position);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearestEnemy = collider.gameObject;
                    }
                }
            }
        }
        
        return nearestEnemy;
    }
    
    private void AttackTarget(GameObject target)
    {
        if (target == null) return;
        
        // Don't attack dead enemies
        EnemyBehavior enemyBehavior = target.GetComponent<EnemyBehavior>();
        if (enemyBehavior != null && enemyBehavior.IsDead)
        {
            Debug.Log($"AttackDummy: Skipping attack on dead enemy {target.name}");
            return;
        }
        
        lastAttackTime = Time.time;
        
        // Face the target before attacking
        if (spriteRenderer != null)
        {
            bool shouldFaceRight = target.transform.position.x > transform.position.x;
            spriteRenderer.flipX = !shouldFaceRight; // Flip sprite to face target
        }
        
        // Play attack animation
        PlayAttackAnimation();
        
        // Create attack damage object in direction of target (similar to enemy behavior)
        Vector3 directionToTarget = (target.transform.position - transform.position).normalized;
        Vector3 attackPosition = transform.position + directionToTarget * (attackRange * 0.7f);
        
        // Track the coroutine for cleanup
        Coroutine attackCoroutine = StartCoroutine(CreateAttackDamageObject(attackPosition));
        if (attackCoroutine != null)
        {
            activeAttackCoroutines.Add(attackCoroutine);
        }
        
        Debug.Log($"Attack Dummy attacked {target.name} - creating damage object at {attackPosition}");
    }
    
    private System.Collections.IEnumerator CreateAttackDamageObject(Vector3 position)
    {
        // Create temporary attack damage object (similar to EnemyBehavior)
        GameObject attack = new GameObject($"{gameObject.name}_Attack");
        attack.transform.position = position;
        
        // Track the attack object for cleanup
        activeAttackObjects.Add(attack);
        
        // Add collider for damage detection
        BoxCollider2D attackCollider = attack.AddComponent<BoxCollider2D>();
        attackCollider.size = new Vector2(1.5f, 1.5f); // Size of attack damage object
        attackCollider.isTrigger = true;
        
        // Add damage object component
        DamageObject damageComponent = attack.AddComponent<DamageObject>();
        damageComponent.damageAmount = (int)attackDamage;
        damageComponent.damageRate = 0.1f; // Fast damage rate
        
        // Set that this should ONLY damage enemies (not player or other dummies)
        damageComponent.canDamageEnemies = true;
        
        // Exclude Player and PlayerSummon layers, but allow Entities layer for dragon
        int playerLayer = LayerMask.NameToLayer("Player");
        int playerSummonLayer = LayerMask.NameToLayer("PlayerSummon");
        LayerMask excludeMask = 0;
        
        if (playerLayer != -1) excludeMask |= (1 << playerLayer);
        if (playerSummonLayer != -1) excludeMask |= (1 << playerSummonLayer);
        
        damageComponent.excludeLayers = excludeMask;
        
        Debug.Log($"AttackDummy: Attack damage setup - can hit enemies on Entities layer, excludes Player/PlayerSummon layers");
        
        // Visual indicator (red damage box like enemies) - COMMENTED OUT FOR INVISIBILITY
        SpriteRenderer attackRenderer = attack.AddComponent<SpriteRenderer>();
        
        // Create red attack sprite - COMMENTED OUT FOR INVISIBILITY
        Texture2D attackTexture = new Texture2D(32, 32);
        Color[] pixels = new Color[32 * 32];
        for (int i = 0; i < pixels.Length; i++)
        {
            // pixels[i] = new Color(0f, 0.8f, 1f, 0.7f); // Blue color to distinguish from enemy attacks - VISIBLE
            pixels[i] = new Color(0f, 0.8f, 1f, 0f); // Fully transparent (invisible)
        }
        attackTexture.SetPixels(pixels);
        attackTexture.Apply();
        
        attackRenderer.sprite = Sprite.Create(attackTexture, new Rect(0, 0, 32, 32), Vector2.one * 0.5f);
        attackRenderer.sortingOrder = 8;
        
        // Wait for attack duration
        yield return new WaitForSeconds(0.3f);
        
        // Clean up tracking and destroy attack object
        activeAttackObjects.Remove(attack);
        if (attack != null)
        {
            Destroy(attack);
        }
    }
    
    public void TakeDamage(float damage)
    {
        if (isDead) return; // Prevent damage after death
        
        currentHealth -= damage;
        currentHealth = Mathf.Max(0f, currentHealth);
        
        Debug.Log($"Attack Dummy took {damage} damage. Health: {currentHealth}/{maxHealth}");
        
        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    // Apply burning status effect to the ally
    public void ApplyBurningEffect(float duration = -1f)
    {
        if (isDead) return; // Don't apply burning to dead allies
        
        float burnDuration = duration > 0 ? duration : baseBurnDuration;
        
        if (isBurning)
        {
            // If already burning, extend the duration (duration stacking)
            float timeRemaining = burnEndTime - Time.time;
            float newDuration = Mathf.Max(timeRemaining, 0f) + burnDuration;
            burnEndTime = Time.time + newDuration;
            Debug.Log($"AttackDummy burning effect extended. New duration: {newDuration:F1}s");
        }
        else
        {
            // Start new burning effect
            isBurning = true;
            burnEndTime = Time.time + burnDuration;
            nextBurnDamageTime = Time.time + burnDamageInterval;
            CreateBurnVisualEffect();
            Debug.Log($"AttackDummy burning effect applied. Duration: {burnDuration:F1}s");
        }
    }
    
    private void HandleBurningEffect()
    {
        if (!isBurning) return;
        
        // Check if burning effect should end
        if (Time.time >= burnEndTime)
        {
            StopBurningEffect();
            return;
        }
        
        // Apply burn damage at intervals
        if (Time.time >= nextBurnDamageTime)
        {
            TakeDamage(burnDamageRate);
            nextBurnDamageTime = Time.time + burnDamageInterval;
            Debug.Log($"AttackDummy burn damage applied: -{burnDamageRate}, Health remaining: {currentHealth}/{maxHealth}");
        }
    }
    
    private void StopBurningEffect()
    {
        if (!isBurning) return;
        
        isBurning = false;
        if (burnVisualEffect != null)
        {
            Destroy(burnVisualEffect);
            burnVisualEffect = null;
        }
        Debug.Log("AttackDummy burning effect ended");
    }
    
    private void CreateBurnVisualEffect()
    {
        if (burnEffectSprite != null && !isDead)
        {
            // Create visual effect similar to player
            GameObject burnEffect = new GameObject("BurnEffect");
            burnEffect.transform.SetParent(transform);
            burnEffect.transform.localPosition = Vector3.up * 0.5f; // Above the dummy
            
            SpriteRenderer burnRenderer = burnEffect.AddComponent<SpriteRenderer>();
            burnRenderer.sprite = burnEffectSprite;
            burnRenderer.sortingLayerName = "Player"; // Same as player
            burnRenderer.sortingOrder = 1;
            
            burnVisualEffect = burnEffect;
        }
    }
    
    public void OnEnemyEntered(GameObject enemy)
    {
        // Enemy entered attack range - this will be detected in FindNearestEnemy
        Debug.Log($"AttackDummy: Enemy {enemy.name} entered attack range");
    }
    
    public void OnEnemyExited(GameObject enemy)
    {
        // Enemy left attack range
        if (currentTarget == enemy)
        {
            currentTarget = null;
            Debug.Log($"AttackDummy: Target {enemy.name} left attack range");
        }
    }
    
    /// <summary>
    /// Set custom follow distance for this dummy
    /// </summary>
    public void SetFollowDistance(float distance)
    {
        followDistance = distance;
        Debug.Log($"AttackDummy follow distance set to: {followDistance}");
    }
    
    private void Die()
    {
        if (isDead) return; // Prevent multiple death calls
        
        isDead = true;
        Debug.Log("Attack Dummy dying - playing death animation");
        
        // Stop burning effect when dead
        StopBurningEffect();
        
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
        
        // Change sprite color to death color
        if (spriteRenderer != null)
        {
            spriteRenderer.color = deathColor;
        }
        
        // Keep collider enabled for ground collision (like enemies)
        // No need to disable collider as all damage methods check isDead status
        
        // Clean up active attacks but keep health bar visible during death animation
        CleanupActiveAttacks();
        
        // Start death animation countdown
        StartCoroutine(DeathAnimationCountdown());
    }
    
    private System.Collections.IEnumerator DeathAnimationCountdown()
    {
        // Wait for death animation to complete
        yield return new WaitForSeconds(deathAnimationDuration);
        
        Debug.Log("Attack Dummy death animation complete - destroying");
        
        // Clean up health bar and destroy
        CleanupAttacks();
        Destroy(gameObject);
    }
    
    private void CleanupActiveAttacks()
    {
        // Stop all active attack coroutines
        foreach (Coroutine coroutine in activeAttackCoroutines)
        {
            if (coroutine != null)
            {
                StopCoroutine(coroutine);
            }
        }
        activeAttackCoroutines.Clear();
        
        // Destroy all active attack objects
        foreach (GameObject attackObj in activeAttackObjects)
        {
            if (attackObj != null)
            {
                Destroy(attackObj);
            }
        }
        activeAttackObjects.Clear();
    }
    
    void OnDestroy()
    {
        // Ensure cleanup happens even if Die() isn't called directly
        CleanupAttacks();
    }
    
    private void CleanupAttacks()
    {
        // Prevent double cleanup
        if (hasBeenCleaned) return;
        hasBeenCleaned = true;
        
        Debug.Log($"Cleaning up AttackDummy: {gameObject.name}");
        
        // Stop all active attack coroutines
        foreach (Coroutine coroutine in activeAttackCoroutines)
        {
            if (coroutine != null)
            {
                StopCoroutine(coroutine);
            }
        }
        activeAttackCoroutines.Clear();
        
        // Destroy all active attack objects
        foreach (GameObject attackObj in activeAttackObjects)
        {
            if (attackObj != null)
            {
                Debug.Log($"Cleaning up orphaned attack object: {attackObj.name}");
                Destroy(attackObj);
            }
        }
        activeAttackObjects.Clear();
        
        // Clean up health bar (moved here so it's called from both Die() and OnDestroy())
        if (healthBarCanvas != null)
        {
            Debug.Log($"Cleaning up health bar for {gameObject.name}");
            Destroy(healthBarCanvas.gameObject);
            healthBarCanvas = null;
        }
    }
    
    private void CreateHealthBar()
    {
        // Force correct health bar size (override any Inspector values)
        healthBarSize = new Vector2(1.5f, 0.2f);
        
        // Create a world space canvas for the health bar (match enemy exactly)
        GameObject canvasGO = new GameObject($"{gameObject.name}_HealthBarCanvas");
        healthBarCanvas = canvasGO.AddComponent<Canvas>();
        healthBarCanvas.renderMode = RenderMode.WorldSpace;
        healthBarCanvas.sortingOrder = 10; // Ensure it renders on top
        
        // Use the exact same method as enemies to ensure consistent sizing
        RectTransform canvasRect = healthBarCanvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(healthBarSize.x * 100, healthBarSize.y * 100); // 150 x 20 (same as enemy)
        canvasRect.localScale = Vector3.one * 0.01f; // Scale down to 1.5 x 0.2 world units
        
        Debug.Log($"AttackDummy HealthBar Values: healthBarSize=({healthBarSize.x}, {healthBarSize.y})");
        Debug.Log($"AttackDummy HealthBar Result: sizeDelta=({canvasRect.sizeDelta.x}, {canvasRect.sizeDelta.y}), scale={canvasRect.localScale}");
        
        // Background (exactly like enemy)
        GameObject backgroundGO = new GameObject("HealthBarBackground");
        backgroundGO.transform.SetParent(canvasGO.transform, false);
        
        healthBarBackground = backgroundGO.AddComponent<Image>();
        healthBarBackground.color = new Color(0.2f, 0.2f, 0.2f, 0.8f); // Dark background
        
        RectTransform backgroundRect = backgroundGO.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.sizeDelta = Vector2.zero;
        
        // Health fill (exactly like enemy)
        GameObject fillGO = new GameObject("HealthBarFill");
        fillGO.transform.SetParent(canvasGO.transform, false);
        
        healthBarFill = fillGO.AddComponent<Image>();
        
        // Create a white sprite for the fill (same as enemy)
        Texture2D fillTexture = new Texture2D(1, 1);
        fillTexture.SetPixel(0, 0, Color.white);
        fillTexture.Apply();
        healthBarFill.sprite = Sprite.Create(fillTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f);
        
        healthBarFill.color = new Color(0f, 0.5f, 1f, 0.9f); // Blue fill (full health)
        healthBarFill.type = Image.Type.Filled;
        healthBarFill.fillMethod = Image.FillMethod.Horizontal;
        healthBarFill.fillOrigin = (int)Image.OriginHorizontal.Left; // Fill from left to right, empty from right to left
        
        RectTransform fillRect = fillGO.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.sizeDelta = Vector2.zero;
        fillRect.anchoredPosition = Vector2.zero;
    }
    
    private void UpdateHealthBar()
    {
        if (healthBarCanvas != null)
        {
            // Position health bar above dummy (exactly like enemy)
            Vector3 healthBarPosition = transform.position + healthBarOffset;
            healthBarCanvas.transform.position = healthBarPosition;
            
            // Keep health bar facing camera (exactly like enemy)
            if (Camera.main != null)
            {
                healthBarCanvas.transform.LookAt(Camera.main.transform);
                healthBarCanvas.transform.Rotate(0, 180, 0); // Face camera properly
            }
        }
        
        if (healthBarFill != null)
        {
            float healthPercent = currentHealth / maxHealth;
            healthBarFill.fillAmount = healthPercent; // This makes it shrink from right to left
            
            // Change color based on health (ally blue scheme)
            if (healthPercent > 0.6f)
                healthBarFill.color = new Color(0f, 0.5f, 1f, 0.9f); // Blue (full health)
            else if (healthPercent > 0.3f)
                healthBarFill.color = new Color(0.5f, 0.8f, 1f, 0.9f); // Light blue (intermediate health)
            else
                healthBarFill.color = new Color(0.7f, 0.7f, 0.7f, 0.9f); // Light grey (almost dead)
        }
    }
    
    void OnDrawGizmosSelected()
    {
        // Draw attack range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        
        // Draw follow distance
        if (player != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(player.transform.position, followDistance);
        }
        
        // Draw jump detection rays
        Gizmos.color = Color.yellow;
        Vector3 rayStart = transform.position + Vector3.up * 0.5f;
        
        // Forward jump detection ray (both directions)
        Gizmos.DrawRay(rayStart, Vector3.right * jumpDetectionDistance);
        Gizmos.DrawRay(rayStart, Vector3.left * jumpDetectionDistance);
        
        // Ground detection ray
        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, Vector2.down * 1.1f);
        
        // Gap detection rays
        Gizmos.color = Color.cyan;
        Vector3 gapCheckRight = transform.position + new Vector3(1f, -0.5f, 0f);
        Vector3 gapCheckLeft = transform.position + new Vector3(-1f, -0.5f, 0f);
        Gizmos.DrawRay(gapCheckRight, Vector2.down * 2f);
        Gizmos.DrawRay(gapCheckLeft, Vector2.down * 2f);
    }
}

// Separate component to handle attack trigger detection - ONLY for enemies
public class AttackTrigger : MonoBehaviour
{
    [System.NonSerialized]
    public AttackDummy parentDummy;
    
    void OnTriggerEnter2D(Collider2D other)
    {
        if (parentDummy == null) return;
        
        // ONLY target enemies - never player or other dummies
        EnemyBehavior enemy = other.GetComponent<EnemyBehavior>();
        if (enemy != null && !enemy.IsDead)
        {
            // Double check it's not a player summon
            if (other.gameObject.tag != "PlayerSummon" && other.gameObject.tag != "Player")
            {
                parentDummy.OnEnemyEntered(other.gameObject);
                Debug.Log($"AttackDummy trigger: Enemy {other.name} entered range");
            }
        }
    }
    
    void OnTriggerExit2D(Collider2D other)
    {
        if (parentDummy == null) return;
        
        // Check if it's an enemy leaving
        EnemyBehavior enemy = other.GetComponent<EnemyBehavior>();
        if (enemy != null && other.gameObject.tag != "PlayerSummon" && other.gameObject.tag != "Player")
        {
            parentDummy.OnEnemyExited(other.gameObject);
            Debug.Log($"AttackDummy trigger: Enemy {other.name} left range");
        }
    }
}