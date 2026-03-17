using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;

public class PlayerMovement : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 10f;

    [Header("Ground Detection")]
    public LayerMask groundLayerMask = 1;
    public LayerMask solidObjectLayerMask = 1; // For detecting solid objects (not one-way platforms)
    public float groundCheckDistance = 0.1f;
    public float ceilingCheckDistance = 0.2f; // Distance to check for ceiling/objects above
    
    [Header("Health System")]
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private float minFallDamageHeight = 5f; // Minimum height to start taking damage
    [SerializeField] private float maxFallDamageHeight = 15f; // Height that causes instant death
    [SerializeField] private int maxFallDamage = 80; // Maximum damage before instant death
    
    [Header("Health Regeneration")]
    [SerializeField] private bool enableHealthRegeneration = true;
    [SerializeField] private float healthRegenDelay = 3f; // Time to wait after taking damage before regenerating
    [SerializeField] private float healthRegenRate = 2f; // Health points per second
    [SerializeField] private float healthRegenInterval = 0.5f; // How often to regenerate (in seconds)
    
    [Header("Burning Status Effect")]
    [SerializeField] private float burnDamageRate = 2f; // Damage per second while burning
    [SerializeField] private float burnDamageInterval = 1f; // How often to apply burn damage (in seconds)
    [SerializeField] private float baseBurnDuration = 5f; // Base duration of burning effect
    [SerializeField] private Sprite burnEffectSprite; // Sprite for burning visual effect
    
    [Header("Health Bar")]
    [SerializeField] private Vector3 healthBarOffset = new Vector3(0, 1.5f, 0); // Offset above player
    [SerializeField] private Vector2 healthBarSize = new Vector2(2f, 0.3f); // Width and height of health bar
    
    [Header("Animation System")]
    [SerializeField] private RuntimeAnimatorController defaultPlayerAnimController = null; // Default animation when no shards equipped
    
    [Header("Buff Icon Fire Animation")]
    [SerializeField] private Sprite[] fireAnimationSprites; // Array of fire animation frames
    [SerializeField] private float fireAnimationSpeed = 0.1f; // Time between frames (seconds)
    
    [Header("Legacy Sprite Animation (Fallback)")]
    [SerializeField] private Sprite idleSprite;
    [SerializeField] private Sprite walkingSprite1;
    [SerializeField] private Sprite walkingSprite2;
    [SerializeField] private Sprite fallingSprite;
    [SerializeField] private float animationSpeed = 0.2f; // Time between frame changes

    [Header("Particle Effects")]
    [SerializeField] private ParticleSystem waterParticles; // Particles when the player lands in water
    ParticleSystem waterParticlesInstance;
    [SerializeField] private float minFallSpeedForSplash = 4f; // Minimum downwards speed needed to make a splash

    [Header("Storm Shard System")]
    [SerializeField] private ParticleSystem lightningArcParticles; // Lightning particles for Storm Shard attacks
    [SerializeField] private float lightningParticleDuration = 2f; // How long lightning particles should play

    [Header("Ultimate System")]
    [SerializeField] private float maxUltimateCharge = 100f;
    private float currentUltimateCharge = 0f;
    
    [Header("Aegis Shield System")]
    private float currentAegisShield = 0f;
    private float maxAegisShield = 0f; // Will be set to player's max health
    private float previousMaxHealth = 0f; // Track previous max health for proportional scaling
    
    [Header("Screen UI System")]
    [SerializeField] private bool useScreenUI = true;
    [SerializeField] private Vector2 healthBarScreenSize = new Vector2(200f, 20f);
    [SerializeField] private Vector2 ultimateBarScreenSize = new Vector2(150f, 15f);
    [SerializeField] private Vector2 buffListSize = new Vector2(300f, 100f);
    
    [Header("Damage Modifiers")]
    // Damage modifiers applied through GetModifiedMeleeDamage/GetModifiedMagicDamage methods
    
    // Ladder System
    private GameObject nearbyLadder = null;
    
    // Buff System
    public enum BuffType { Attack, Aegis, Durability, Strength, Vitality, Flux, Swiftness }
    
    [System.Serializable]
    public class ActiveBuff
    {
        public BuffType type;
        public float value;
        public float duration;
        public float startTime;
        public string description;
        
        public ActiveBuff(BuffType buffType, float buffValue, float buffDuration, string desc = "")
        {
            type = buffType;
            value = buffValue;
            duration = buffDuration;
            startTime = Time.time;
            description = desc;
        }
        
        public bool IsExpired => Time.time >= startTime + duration;
        public float TimeRemaining => Mathf.Max(0f, (startTime + duration) - Time.time);
    }
    
    private List<ActiveBuff> activeBuffs = new List<ActiveBuff>();
    
    // Components
    private Rigidbody2D rb;
    [Header("Collider")]
    [SerializeField] private CapsuleCollider2D playerCollider;
    private SpriteRenderer spriteRenderer;
    public Animator playerAnimator; // Made public for WeaponClassController access

    // Input
    private float horizontalInput;
    private bool jumpInput;
    private bool leftInput;
    private bool rightInput;
    private bool downInput;

    // State
    private bool isGrounded;
    private bool hasCeilingClearance = true; // Check if player has space above to jump
    private float animationTimer = 0f;
    private bool useFirstWalkSprite = true;
    
    // Animation State
    private bool isPlayerAttacking = false;
    private bool isPlayerJumping = false; 
    private bool isPlayerWalking = false;
    private bool isPlayerDead = false;
    private int currentAttackType = 0; // 0=None, 1=Melee, 2=Projectile, 3=Ultimate
    public RuntimeAnimatorController currentAnimController = null; // Made public for WeaponClassController access
    private float lastAnimationWarningTime = 0f; // To prevent log spam

    private GameObject currentPlatform;
    
    // Health System
    private int currentHealth;
    private Vector3 spawnPosition;
    
    // Network Synchronized Variables
    private NetworkVariable<int> networkHealth = new NetworkVariable<int>(100, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<bool> networkIsBurning = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<bool> networkIsPlayerDead = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> networkEquippedShardType = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<float> networkCurrentShield = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<ulong> networkPlayerID = new NetworkVariable<ulong>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> networkFacingLeft = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<bool> networkIsWalking = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<bool> networkIsAttacking = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    
    // Health Regeneration System
    private float lastDamageTime;
    private float lastRegenTime;
    private bool isRegenerating = false;
    
    // Burning Status Effect System
    private bool isBurning = false;
    private float burnEndTime = 0f;
    private float nextBurnDamageTime = 0f;
    private GameObject burnEffectObject;
    
    // Fall Damage System
    private bool isFalling = false;
    private float fallStartHeight;
    private bool wasGroundedLastFrame = false;
    
    // Water Physics System
    private bool inWater = false;
    private WaterProperties currentWaterProperties;
    private float originalMoveSpeed;
    private float originalJumpForce;
    private float originalGravityScale;
    private float timeInWater = 0f;
    private int waterObjectCount = 0; // Track how many water objects player is in
    
    // Surface Swimming System
    private bool isAtWaterSurface = false;
    private float waterSurfaceY = 0f; // Y position of the water surface
    private Collider2D currentWaterCollider; // Track the current water collider for surface detection
    private bool wantsToSubmerge = false; // Track if player wants to go underwater
    
    // World-Space Health Bar UI
    private Canvas healthBarCanvas;
    private Image healthBarBackground;
    private Image healthBarFill;
    
    // Screen-Space UI System
    private Canvas screenUICanvas;
    private Image screenHealthBarBackground;
    private Image screenHealthBarFill;
    
    // Aegis Shield Outline UI
    private Image aegisOutlineLeft;
    private Image aegisOutlineTop;
    private Image aegisOutlineBottom;
    private Image aegisOutlineRight;
    private Image ultimateBarBackground;
    private Image ultimateBarFill;
    private Text screenHealthText;
    private Image ultimateFullEffect;
    private bool wasUltimateFull = false;
    private bool isUltimateEffectPlaying = false;
    private Transform buffListPanel;
    private List<GameObject> activeBuffUI = new List<GameObject>();
    private GameObject tooltipPanel;
    private Text tooltipText;
    private float tooltipShowTime;
    private const float tooltipMaxDisplayTime = 10f; // Auto-hide after 10 seconds
    private bool isTooltipVisible = false;
    private string currentTooltipType = "";
    private int ultimateBarUpdateCount = 0;
    private float lastLoggedUltimateCharge = -1f;
    
    // Weapon System
    private WeaponClassController weaponController;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<CapsuleCollider2D>();
        
        // Ensure we have a capsule collider
        if (playerCollider == null)
        {
            playerCollider = gameObject.AddComponent<CapsuleCollider2D>();
            playerCollider.direction = CapsuleDirection2D.Vertical;
        }
        
        // Try to get SpriteRenderer from child object first, then from this object
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
            
        // Try to get Animator component
        playerAnimator = GetComponentInChildren<Animator>();
        if (playerAnimator == null)
            playerAnimator = GetComponent<Animator>();
            
        // Initialize with default animation controller
        if (playerAnimator != null && defaultPlayerAnimController != null)
        {
            playerAnimator.runtimeAnimatorController = defaultPlayerAnimController;
            currentAnimController = defaultPlayerAnimController;
        }

        // Prevent player from rotating when falling off edges
        rb.freezeRotation = true;
        
        // Improve slope handling with physics material
        PhysicsMaterial2D slopeMaterial = new PhysicsMaterial2D("PlayerSlopeMaterial");
        slopeMaterial.friction = 0.4f; // Moderate friction for slopes
        slopeMaterial.bounciness = 0f; // No bouncing
        playerCollider.sharedMaterial = slopeMaterial;        // Initialize health system
        currentHealth = maxHealth;
        spawnPosition = transform.position;
        
        // Initialize aegis shield system
        maxAegisShield = maxHealth;
        currentAegisShield = 0f;
        previousMaxHealth = maxHealth;
        
        // Initialize water physics system
        originalMoveSpeed = moveSpeed;
        originalJumpForce = jumpForce;
        originalGravityScale = rb.gravityScale;
        
        // Create screen UI system (replaces world-space health bar)
        // UI creation will be handled in OnNetworkSpawn() for proper network ownership
        if (!useScreenUI)
        {
            // Only create world-space health bar if screen UI is disabled
            CreateHealthBar();
        }
        
        // Get weapon controller (should be on the same GameObject)
        weaponController = GetComponent<WeaponClassController>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        if (IsOwner)
        {
            // Set spawn position based on player ID
            SetPlayerSpawnPosition();
            
            // Initialize network variables for the owning player
            networkPlayerID.Value = OwnerClientId;
            
            // Create screen UI for local player only
            if (useScreenUI)
            {
                CreateScreenUI();
                Debug.Log("Created UI for local player");
            }
            
            // Set initial network health
            if (IsServer)
            {
                networkHealth.Value = maxHealth;
                networkIsBurning.Value = false;
                networkIsPlayerDead.Value = false;
                networkEquippedShardType.Value = 0; // No shard equipped initially
                networkCurrentShield.Value = 0f;
            }
            
            // Setup camera to follow this local player
            SetupCameraFollow();
        }
        
        // Setup player collision ignoring to prevent players from colliding with each other
        SetupPlayerCollisionIgnoring();
        
        // Subscribe to network variable changes for all clients
        networkHealth.OnValueChanged += OnHealthChanged;
        networkIsBurning.OnValueChanged += OnBurningStatusChanged;
        networkFacingLeft.OnValueChanged += OnFacingDirectionChanged;
        networkIsPlayerDead.OnValueChanged += OnDeathStatusChanged;
        networkEquippedShardType.OnValueChanged += OnShardEquipChanged;
        networkCurrentShield.OnValueChanged += OnShieldChanged;
        networkIsWalking.OnValueChanged += OnWalkingStatusChanged;
        networkIsAttacking.OnValueChanged += OnAttackingStatusChanged;
        
        // Sync current health with network variable
        currentHealth = networkHealth.Value;
        isBurning = networkIsBurning.Value;
        isPlayerDead = networkIsPlayerDead.Value;
    }
    
    public override void OnNetworkDespawn()
    {
        // Unsubscribe from network variable changes
        if (networkHealth != null)
        {
            networkHealth.OnValueChanged -= OnHealthChanged;
            networkIsBurning.OnValueChanged -= OnBurningStatusChanged;
            networkFacingLeft.OnValueChanged -= OnFacingDirectionChanged;
            networkIsPlayerDead.OnValueChanged -= OnDeathStatusChanged;
            networkEquippedShardType.OnValueChanged -= OnShardEquipChanged;
            networkCurrentShield.OnValueChanged -= OnShieldChanged;
            networkIsWalking.OnValueChanged -= OnWalkingStatusChanged;
            networkIsAttacking.OnValueChanged -= OnAttackingStatusChanged;
        }
        
        base.OnNetworkDespawn();
    }
    
    private void UpdateHealthUI()
    {
        // Update health displays based on current network health value
        UpdateHealthBar();
        UpdateScreenUI();
    }
    
    private void SetPlayerSpawnPosition()
    {
        // Set different spawn positions for different players
        Vector3 baseSpawnPosition = spawnPosition;
        
        // Offset spawn position based on client ID
        switch (OwnerClientId)
        {
            case 0: // Player 1 - original position
                transform.position = baseSpawnPosition;
                break;
            case 1: // Player 2 - offset to the right
                transform.position = baseSpawnPosition + new Vector3(3f, 0f, 0f);
                break;
            default: // Additional players - spread them out
                transform.position = baseSpawnPosition + new Vector3(1.5f * OwnerClientId, 0f, 0f);
                break;
        }
    }
    
    // Network variable change callbacks
    private void OnHealthChanged(int previousValue, int newValue)
    {
        currentHealth = newValue;
        UpdateHealthUI();
    }
    
    private void OnBurningStatusChanged(bool previousValue, bool newValue)
    {
        isBurning = newValue;
        UpdateBurningVisuals();
    }
    
    private void OnFacingDirectionChanged(bool previousValue, bool newValue)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = newValue;
            
            // Update particle points for remote players
            if (!IsOwner && weaponController != null)
            {
                weaponController.FlipParticlePoints(newValue);
            }
        }
    }
    
    private void OnDeathStatusChanged(bool previousValue, bool newValue)
    {
        isPlayerDead = newValue;
        if (newValue)
        {
            HandlePlayerDeath();
        }
    }
    
    private void OnShardEquipChanged(int previousValue, int newValue)
    {
        // Update animation controller based on equipped shard
        UpdateAnimationController(newValue);
    }
    
    private void OnShieldChanged(float previousValue, float newValue)
    {
        currentAegisShield = newValue;
        UpdateShieldVisuals();
    }
    
    private void OnWalkingStatusChanged(bool previousValue, bool newValue)
    {
        isPlayerWalking = newValue;
        if (playerAnimator != null)
        {
            playerAnimator.SetBool("isWalking", newValue);
        }
    }
    
    private void OnAttackingStatusChanged(bool previousValue, bool newValue)
    {
        isPlayerAttacking = newValue;
        if (playerAnimator != null)
        {
            playerAnimator.SetBool("isAttacking", newValue);
        }
    }
    
    private void UpdateShieldVisuals()
    {
        // Update aegis shield visual effects
        UpdateAegisOutline();
    }
    
    private void SetupPlayerCollisionIgnoring()
    {
        // Find all other players and ignore collisions between them
        PlayerMovement[] allPlayers = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        
        foreach (PlayerMovement otherPlayer in allPlayers)
        {
            if (otherPlayer != this && otherPlayer.playerCollider != null && playerCollider != null)
            {
                Physics2D.IgnoreCollision(playerCollider, otherPlayer.playerCollider, true);
                Debug.Log($"PlayerCollision: Ignoring collision between {gameObject.name} and {otherPlayer.gameObject.name}");
            }
        }
    }
    
    private void SetupCameraFollow()
    {
        // Find the main camera and set this player as the target
        CameraFollow cameraFollow = Camera.main?.GetComponent<CameraFollow>();
        if (cameraFollow != null)
        {
            cameraFollow.SetTarget(transform);
            cameraFollow.CenterOnTarget();
            Debug.Log($"CameraFollow: Set target to local player {gameObject.name} (Owner: {OwnerClientId})");
        }
        else
        {
            Debug.LogWarning("CameraFollow: Could not find CameraFollow component on main camera");
        }
    }

    void Update()
    {
        // Only process input and certain logic for the owning player
        // Handle networked vs non-networked movement
        if (IsOwner || !IsSpawned)
        {
            // IsSpawned false means we're testing without networking
            GetInput();
            HandleMovement();
        }
        
        // These run for all clients to keep visuals synchronized
        CheckGrounded();
        CheckCeilingClearance();
        UpdateSprite();
        UpdateAnimationParameters();
        
        // Owner-only systems that affect game state (or non-networked testing)
        if (IsOwner || !IsSpawned)
        {
            HandleFallDamage();
            HandleWaterPhysics();
            HandleHealthRegeneration();
            HandleBurningEffect();
            UpdateBuffs();
        }
        
        // UI updates for all players (local UI shows based on network variables)
        UpdateHealthBarPosition();
        UpdateScreenUI();
        
        // Handle max health changes from durability buffs (owner only)
        if (IsOwner)
        {
            float newMaxHealth = GetModifiedMaxHealth();
            if (newMaxHealth != previousMaxHealth)
            {
                // Calculate the ratio to maintain proportional health and aegis
                float healthRatio = previousMaxHealth > 0 ? (float)currentHealth / previousMaxHealth : 1f;
                float aegisRatio = maxAegisShield > 0 ? currentAegisShield / maxAegisShield : 0f;
                
                // Update max health and maintain ratio
                currentHealth = Mathf.RoundToInt(newMaxHealth * healthRatio);
                currentHealth = Mathf.Min(currentHealth, (int)newMaxHealth); // Ensure health doesn't exceed new max
                
                // Update aegis shield proportionally
                maxAegisShield = newMaxHealth;
                if (currentAegisShield > 0f)
                {
                    currentAegisShield = maxAegisShield * aegisRatio;
                    currentAegisShield = Mathf.Min(currentAegisShield, maxAegisShield); // Ensure aegis doesn't exceed new max
                }
                
                previousMaxHealth = newMaxHealth;
                UpdateAegisOutline();
                UpdateHealthBar(); // Update health bar to reflect new values
                
                // Debug.Log($"Max health changed: {previousMaxHealth} -> {newMaxHealth}. Health: {currentHealth}/{newMaxHealth}, Aegis: {currentAegisShield:F1}/{maxAegisShield:F1}");
            }
        }
    }
    
    private void GetInput()
    {
        // Use new Input System
        horizontalInput = 0f;
        jumpInput = false;
        leftInput = false;
        rightInput = false;

        // Check keyboard directly
        if (Keyboard.current != null)
        {
            // Left/right - WASD and arrow keys
            leftInput = Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed;
            rightInput = Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed;
            downInput = Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed;
            
            // Track if player wants to submerge (for surface swimming)
            wantsToSubmerge = downInput;
            
            // Jump - spacebar, W key, and up arrow (use wasPressedThisFrame for better jump responsiveness)
            jumpInput = Keyboard.current.spaceKey.wasPressedThisFrame ||
                       Keyboard.current.wKey.wasPressedThisFrame ||
                       Keyboard.current.upArrowKey.wasPressedThisFrame;
            
            // Set horizontal input
            if (leftInput)
                horizontalInput = -1f;
            else if (rightInput)
                horizontalInput = 1f;
        }
    }
    
    private void HandleMovement()
    {
        // Check if weapon menu is open - disable movement if it is
        if (weaponController != null && weaponController.IsWeaponMenuOpen())
        {
            // Stop horizontal movement but maintain vertical velocity
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            return;
        }
        
        // Horizontal movement (modified by water if in water)
        float currentMoveSpeed = inWater && currentWaterProperties != null ? 
            originalMoveSpeed * currentWaterProperties.speedModifier : moveSpeed;
        
        // Apply Swiftness buffs to movement speed
        foreach (ActiveBuff buff in activeBuffs)
        {
            if (buff.type == BuffType.Swiftness && !buff.IsExpired)
            {
                currentMoveSpeed += moveSpeed * (buff.value / 100f);
            }
        }
        
        // Reduce movement speed while charging ValorShard attack
        bool isChargingValor = weaponController != null && weaponController.IsChargingValorAttack;
        if (isChargingValor)
        {
            currentMoveSpeed *= 0.3f; // Reduce speed to 30% while charging
        }
        
        float targetVelocityX = horizontalInput * currentMoveSpeed;
        
        // Apply water current if in water
        if (inWater && currentWaterProperties != null)
        {
            targetVelocityX += currentWaterProperties.currentForceX;
        }
        
        rb.linearVelocity = new Vector2(targetVelocityX, rb.linearVelocity.y);
        
        // Sprite flipping (don't flip during weapon menu or when using WhisperShard)
        bool weaponMenuOpen = weaponController != null && weaponController.IsWeaponMenuOpen();
        bool isUsingWhisperShard = weaponController != null && weaponController.IsWhisperShardActive;
        
        if (spriteRenderer != null && !weaponMenuOpen && !isUsingWhisperShard && IsOwner)
        {
            bool previousFlipX = spriteRenderer.flipX;
            bool shouldFaceLeft = false;
            
            if (horizontalInput < -0.1f)
            {
                shouldFaceLeft = true;
            }
            else if (horizontalInput > 0.1f)
            {
                shouldFaceLeft = false;
            }
            else
            {
                // No input, keep current direction
                return;
            }
            
            // Only update if direction changed and sync to network
            if (shouldFaceLeft != networkFacingLeft.Value)
            {
                networkFacingLeft.Value = shouldFaceLeft;
                spriteRenderer.flipX = shouldFaceLeft;
                
                // If facing direction changed, flip particle points
                if (weaponController != null)
                {
                    weaponController.FlipParticlePoints(shouldFaceLeft);
                }
            }
        }
        
        // Jumping/Swimming
        if (inWater)
        {
            HandleSurfaceSwimming();
        }
        else if (jumpInput && isGrounded && hasCeilingClearance)
        {
            // Normal jumping - only if we have ceiling clearance
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        }
        
        // Drop down through one-way platform
        if (downInput && currentPlatform != null && isGrounded)
        {
            StartCoroutine(DisableCollision());
        }
    }
    
    private void HandleSurfaceSwimming()
    {
        // Check if W or Up is being held for continuous swimming
        bool swimUpInput = false;
        if (Keyboard.current != null)
        {
            swimUpInput = Keyboard.current.wKey.isPressed || 
                         Keyboard.current.upArrowKey.isPressed ||
                         Keyboard.current.spaceKey.isPressed;
        }
        
        // Calculate player's half height for surface detection
        float playerHalfHeight = (playerCollider.size.y * transform.localScale.y) * 0.5f;
        float playerTopY = transform.position.y + playerHalfHeight;
        float targetSurfaceY = waterSurfaceY - playerHalfHeight; // Player center should be at surface minus half height
        
        // Check if player is near the water surface
        float distanceFromSurface = Mathf.Abs(transform.position.y - targetSurfaceY);
        bool nearSurface = distanceFromSurface < 0.3f; // Tolerance for being "at surface"
        
        // Update surface state
        if (nearSurface && playerTopY >= waterSurfaceY - 0.1f)
        {
            isAtWaterSurface = true;
        }
        else if (wantsToSubmerge || transform.position.y < targetSurfaceY - 1f)
        {
            isAtWaterSurface = false;
        }
        
        // Handle swimming behavior
        if (isAtWaterSurface && !wantsToSubmerge)
        {
            // Surface swimming - keep player at surface level
            float currentY = transform.position.y;
            
            // If above target surface level, gently pull down to surface
            if (currentY > targetSurfaceY)
            {
                float pullForce = (currentY - targetSurfaceY) * 10f; // Proportional force
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, -pullForce);
            }
            // If at or below surface, maintain surface level
            else
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
                transform.position = new Vector3(transform.position.x, targetSurfaceY, transform.position.z);
            }
            
            // Allow swimming up only if player wants to (to get out of water)
            if (swimUpInput)
            {
                float surfaceSwimForce = currentWaterProperties != null ? 
                    originalJumpForce * currentWaterProperties.jumpForceModifier * 0.8f : originalJumpForce * 0.6f;
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, surfaceSwimForce);
                isAtWaterSurface = false; // Allow leaving surface
            }
        }
        else
        {
            // Underwater swimming - normal behavior
            if (swimUpInput)
            {
                // Continuous swimming upward when holding swim keys - increased speed
                float swimForce = currentWaterProperties != null ? 
                    originalJumpForce * currentWaterProperties.jumpForceModifier * 1.2f : originalJumpForce * 0.95f;
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, swimForce);
            }
        }
    }

    private void CheckGrounded()
    {
        Vector2 colliderCenter = (Vector2)transform.position + (Vector2)(playerCollider.offset * transform.localScale.y);
        float checkDistance = groundCheckDistance;
        
        // Account for scaling when calculating the effective collider dimensions
        float scaledPlayerWidth = playerCollider.size.x * transform.localScale.x;
        float scaledPlayerHeight = playerCollider.size.y * transform.localScale.y;
        
        // For capsule collider, calculate the actual bottom position
        // The capsule extends from center by half its height, then we need to account for the rounded ends
        float halfHeight = scaledPlayerHeight * 0.5f;
        float bottomY = colliderCenter.y - halfHeight;
        
        // Create multiple ground check positions across the player's width
        Vector2 leftCheckPos = new Vector2(colliderCenter.x - scaledPlayerWidth * 0.3f, bottomY - 0.05f);
        Vector2 centerCheckPos = new Vector2(colliderCenter.x, bottomY - 0.05f);
        Vector2 rightCheckPos = new Vector2(colliderCenter.x + scaledPlayerWidth * 0.3f, bottomY - 0.05f);
        
        // Check ground at multiple points
        bool leftGrounded = CheckGroundAtPosition(leftCheckPos, checkDistance);
        bool centerGrounded = CheckGroundAtPosition(centerCheckPos, checkDistance);
        bool rightGrounded = CheckGroundAtPosition(rightCheckPos, checkDistance);
        
        // Additional check using OverlapBox for better tilemap detection
        Vector2 overlapBoxCenter = new Vector2(colliderCenter.x, bottomY - checkDistance * 0.5f);
        Vector2 overlapBoxSize = new Vector2(scaledPlayerWidth * 0.8f, checkDistance);
        Collider2D groundCollider = Physics2D.OverlapBox(overlapBoxCenter, overlapBoxSize, 0f, groundLayerMask);
        bool overlapGrounded = groundCollider != null && groundCollider != playerCollider && groundCollider.gameObject != gameObject;
        
        // Player is grounded if ANY of the check points hit ground OR the overlap box detects ground
        isGrounded = leftGrounded || centerGrounded || rightGrounded || overlapGrounded;
    }
    
    private void CheckCeilingClearance()
    {
        Vector2 colliderCenter = (Vector2)transform.position + (Vector2)(playerCollider.offset * transform.localScale.y);
        
        // Account for scaling when calculating the effective collider dimensions
        float scaledPlayerWidth = playerCollider.size.x * transform.localScale.x;
        float scaledPlayerHeight = playerCollider.size.y * transform.localScale.y;
        
        // Check if player is inside a solid object by detecting overlaps
        Bounds playerBounds = new Bounds(colliderCenter, new Vector3(scaledPlayerWidth * 0.9f, scaledPlayerHeight * 0.9f, 0));
        
        // Get all colliders that overlap with the player
        Collider2D[] overlappingColliders = Physics2D.OverlapAreaAll(
            (Vector2)playerBounds.min, 
            (Vector2)playerBounds.max, 
            solidObjectLayerMask
        );
        
        bool insideSolidObject = false;
        
        foreach (Collider2D col in overlappingColliders)
        {
            // Skip our own collider
            if (col == playerCollider || col.gameObject == gameObject)
                continue;
                
            // Skip one-way platforms - they don't count as being "inside"
            if (col.CompareTag("OneWayPlatform"))
                continue;
                
            // Skip water objects - they don't block jumping
            if (col.CompareTag("Water"))
                continue;
            
            insideSolidObject = true;
            break;
        }
        
        // Player has ceiling clearance if they're not inside a solid object
        hasCeilingClearance = !insideSolidObject;
    }
    
    private void HandleFallDamage()
    {
        // Don't track fall damage while in water
        if (inWater)
        {
            isFalling = false;
            wasGroundedLastFrame = isGrounded;
            return;
        }
        
        // Check if we just became airborne (includes jumping off ledges)
        if (wasGroundedLastFrame && !isGrounded)
        {
            // Start tracking fall from any airborne state
            if (!isFalling)
            {
                isFalling = true;
                fallStartHeight = transform.position.y;
            }
        }
        
        // Update fall start height if we're still going up (for jump tracking)
        if (isFalling && rb.linearVelocity.y > 0 && transform.position.y > fallStartHeight)
        {
            fallStartHeight = transform.position.y; // Track highest point reached
        }
        
        // Check if we just landed
        if (!wasGroundedLastFrame && isGrounded && isFalling)
        {
            float fallDistance = fallStartHeight - transform.position.y;
            
            // Only apply damage if fall was significant
            if (fallDistance > minFallDamageHeight)
            {
                ApplyFallDamage(fallDistance);
                Debug.Log($"Fall damage applied! Distance: {fallDistance:F1}m, Damage calculated from height {fallStartHeight:F1} to {transform.position.y:F1}");
            }
            
            isFalling = false;
        }
        
        // Update the previous frame ground state
        wasGroundedLastFrame = isGrounded;
    }
    
    private void ApplyFallDamage(float fallDistance)
    {
        int damage = 0;
        
        // Calculate damage based on fall distance
        if (fallDistance >= maxFallDamageHeight)
        {
            // Instant death for extreme falls
            damage = currentHealth;
        }
        else
        {
            // Scale damage between 0 and maxFallDamage
            float damageRatio = (fallDistance - minFallDamageHeight) / (maxFallDamageHeight - minFallDamageHeight);
            damage = Mathf.RoundToInt(damageRatio * maxFallDamage);
        }
        
        TakeDamage(damage);
    }
    
    private void TakeDamage(int damage)
    {
        // For networked gameplay, damage must be processed on the server
        if (IsOwner && IsClient)
        {
            TakeDamageServerRpc(damage);
        }
        else if (IsServer && !IsClient)
        {
            // Direct server processing
            ProcessDamage(damage);
        }
    }
    
    [ServerRpc]
    private void TakeDamageServerRpc(int damage)
    {
        ProcessDamage(damage);
    }
    
    private void ProcessDamage(int damage)
    {
        // This method processes damage with server authority
        int remainingDamage = damage;
        
        // Check aegis shield first
        if (networkCurrentShield.Value > 0f)
        {
            float shieldDamage = Mathf.Min(networkCurrentShield.Value, remainingDamage);
            networkCurrentShield.Value -= shieldDamage;
            remainingDamage -= (int)shieldDamage;
            
            Debug.Log($"Aegis Shield absorbed {shieldDamage:F1} damage. Shield remaining: {networkCurrentShield.Value:F1}/{maxAegisShield:F1}");
        }
        
        // Apply remaining damage to health
        if (remainingDamage > 0)
        {
            networkHealth.Value = Mathf.Max(0, networkHealth.Value - remainingDamage);
            
            // Record damage time for regeneration system (local only)
            if (IsOwner)
            {
                lastDamageTime = Time.time;
                isRegenerating = false;
            }
            
            Debug.Log($"Health damage: -{remainingDamage}, Health remaining: {networkHealth.Value}/{GetModifiedMaxHealth()}");
        }
        
        if (networkHealth.Value <= 0)
        {
            ProcessPlayerDeath();
        }
    }
    
    private void ProcessPlayerDeath()
    {
        if (IsServer)
        {
            networkIsPlayerDead.Value = true;
            // Additional death processing will be handled by the network callback
        }
    }
    
    private void HandlePlayerDeath()
    {
        // This is called when the death status changes via network
        if (isPlayerDead)
        {
            Debug.Log("Player died!");
            // Handle death visuals and logic for all clients
            if (playerAnimator != null)
            {
                playerAnimator.SetBool("IsDead", true);
            }
            
            // Disable player control for the owner
            if (IsOwner)
            {
                enabled = false; // Disable this script's Update method
            }
            
            // Trigger respawn after delay (only on server)
            if (IsServer)
            {
                StartCoroutine(RespawnAfterDelay());
            }
        }
    }
    
    // Coroutine to handle respawn delay
    private System.Collections.IEnumerator RespawnAfterDelay()
    {
        yield return new WaitForSeconds(3f); // Wait 3 seconds before respawning
        
        // Get the MultiplayerGameManager and trigger respawn
        MultiplayerGameManager gameManager = FindObjectOfType<MultiplayerGameManager>();
        if (gameManager != null)
        {
            gameManager.RespawnPlayerServerRpc(OwnerClientId);
        }
        else
        {
            Debug.LogError("MultiplayerGameManager not found! Cannot respawn player.");
        }
    }
    
    // Apply burning status effect to the player
    public void ApplyBurningEffect(float duration = -1f)
    {
        if (IsOwner)
        {
            ApplyBurningEffectServerRpc(duration);
        }
    }
    
    [ServerRpc]
    private void ApplyBurningEffectServerRpc(float duration = -1f)
    {
        float burnDuration = duration > 0 ? duration : baseBurnDuration;
        
        if (networkIsBurning.Value)
        {
            // If already burning, extend the duration (duration stacking)
            float timeRemaining = burnEndTime - Time.time;
            float newDuration = Mathf.Max(timeRemaining, 0f) + burnDuration;
            burnEndTime = Time.time + newDuration;
            Debug.Log($"Burning effect extended. New duration: {newDuration:F1}s");
        }
        else
        {
            // Start new burning effect
            networkIsBurning.Value = true;
            burnEndTime = Time.time + burnDuration;
            nextBurnDamageTime = Time.time + burnDamageInterval;
            Debug.Log($"Burning effect applied. Duration: {burnDuration:F1}s");
        }
    }
    
    private void UpdateBurningVisuals()
    {
        if (networkIsBurning.Value && !isBurning)
        {
            // Start burning visual effect
            CreateBurnVisualEffect();
        }
        else if (!networkIsBurning.Value && isBurning)
        {
            // Stop burning visual effect
            StopBurningVisualEffect();
        }
    }
    
    private void StopBurningVisualEffect()
    {
        // Stop the visual burning effect only (not the damage over time)
        isBurning = false;
        
        if (burnEffectObject != null)
        {
            Destroy(burnEffectObject);
            burnEffectObject = null;
        }
        
        Debug.Log("Burning visual effect stopped");
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
            // Use TakeDamage method but bypass aegis shield for DoT effects
            int burnDamage = Mathf.RoundToInt(burnDamageRate);
            currentHealth -= burnDamage;
            currentHealth = Mathf.Max(0, currentHealth);
            
            // Reset damage timer for health regeneration
            lastDamageTime = Time.time;
            
            UpdateHealthBar();
            Debug.Log($"Burn damage applied: -{burnDamage}, Health remaining: {currentHealth}/{GetModifiedMaxHealth()}");
            
            nextBurnDamageTime = Time.time + burnDamageInterval;
            
            // Check if player died from burn damage
            if (currentHealth <= 0)
            {
                Die();
            }
        }
    }
    
    private void StopBurningEffect()
    {
        if (!isBurning) return;
        
        isBurning = false;
        burnEndTime = 0f;
        nextBurnDamageTime = 0f;
        
        // Remove visual effect
        if (burnEffectObject != null)
        {
            Destroy(burnEffectObject);
            burnEffectObject = null;
        }
        
        Debug.Log("Burning effect ended");
    }
    
    private void CreateBurnVisualEffect()
    {
        if (burnEffectSprite == null) return;
        
        // Create GameObject for burn visual effect
        burnEffectObject = new GameObject("BurnEffect");
        burnEffectObject.transform.SetParent(transform);
        burnEffectObject.transform.localPosition = Vector3.zero;
        
        // Add SpriteRenderer component
        SpriteRenderer spriteRenderer = burnEffectObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = burnEffectSprite;
        spriteRenderer.sortingLayerName = "Player";
        spriteRenderer.sortingOrder = 1;
        
        Debug.Log("Burn visual effect created");
    }
    
    // Public method to check if player is currently burning (for UI or other systems)
    public bool IsBurning()
    {
        return isBurning;
    }
    
    // Public method to get remaining burn time (for UI)
    public float GetBurnTimeRemaining()
    {
        if (!isBurning) return 0f;
        return Mathf.Max(0f, burnEndTime - Time.time);
    }
    
    private void HandleHealthRegeneration()
    {
        int modifiedMaxHealth = GetModifiedMaxHealth();
        
        // Don't regenerate if system is disabled, health is full, or player is dead
        if (!enableHealthRegeneration || currentHealth >= modifiedMaxHealth || currentHealth <= 0)
        {
            return;
        }
        
        // Check if enough time has passed since last damage
        if (Time.time - lastDamageTime >= healthRegenDelay)
        {
            // Check if enough time has passed since last regeneration tick
            if (!isRegenerating || Time.time - lastRegenTime >= healthRegenInterval)
            {
                if (!isRegenerating)
                {
                    isRegenerating = true;
                    lastRegenTime = Time.time;
                }
                
                // Calculate health regeneration with Vitality buffs
                float modifiedRegenRate = healthRegenRate;
                foreach (ActiveBuff buff in activeBuffs)
                {
                    if (buff.type == BuffType.Vitality && !buff.IsExpired)
                    {
                        modifiedRegenRate += healthRegenRate * (buff.value / 100f);
                    }
                }
                
                int healthToRegenerate = Mathf.RoundToInt(modifiedRegenRate * healthRegenInterval);
                currentHealth += healthToRegenerate;
                currentHealth = Mathf.Min(currentHealth, modifiedMaxHealth);
                
                // Update health bar
                UpdateHealthBar();
                
                // Update last regeneration time
                lastRegenTime = Time.time;
                
                // Stop regenerating if health is full
                if (currentHealth >= modifiedMaxHealth)
                {
                    isRegenerating = false;
                }
            }
        }
    }
    
    // Public method for external damage sources
    public void TakeDamageFromObject(int damage)
    {
        TakeDamage(damage);
    }
    
    // Public method for water state management
    public void SetWaterState(bool enteringWater, WaterProperties properties = null, Collider2D waterCollider = null)
    {
        if (enteringWater)
        {
            // Spawn Water Particles if player is moving down fast enough
            if (rb.linearVelocity.y < -minFallSpeedForSplash)
            {
                waterParticlesInstance = Instantiate(waterParticles, new Vector3(transform.position.x, transform.position.y - (playerCollider.size.y * transform.localScale.y) * 0.5f, transform.position.z), Quaternion.Euler(0, 0, 60));
            }

            // Increment water object counter
            waterObjectCount++;
            
            // Store the water collider for surface detection
            if (waterCollider != null)
            {
                currentWaterCollider = waterCollider;
                // Calculate water surface Y position (top of the water collider)
                waterSurfaceY = waterCollider.bounds.max.y;
            }
            
            // Only set up water physics if this is the first water object
            if (waterObjectCount == 1)
            {
                inWater = true;
                timeInWater = 0f;
                
                // Reset fall damage tracking when entering water
                isFalling = false;
                fallStartHeight = 0f;
                
                // Reset surface swimming state
                isAtWaterSurface = false;
            }
            
            // Update water properties (use the most recent properties)
            if (properties != null)
            {
                currentWaterProperties = properties;
                // Apply water physics modifications
                rb.gravityScale = originalGravityScale * properties.gravityModifier;
            }
        }
        else
        {
            // Decrement water object counter
            waterObjectCount = Mathf.Max(0, waterObjectCount - 1);
            
            // Only disable water physics when exiting ALL water objects
            if (waterObjectCount == 0)
            {
                inWater = false;
                
                // Reset surface swimming state
                isAtWaterSurface = false;
                currentWaterCollider = null;
                
                // Restore normal physics
                currentWaterProperties = null;
                rb.gravityScale = originalGravityScale;
                timeInWater = 0f;
                
                // Reset fall tracking when exiting water to prevent false fall damage
                isFalling = false;
                wasGroundedLastFrame = false;
            }
        }
    }
    
    // Public method to get current water object count (for debugging)
    public int GetWaterObjectCount()
    {
        return waterObjectCount;
    }
    
    private void HandleWaterPhysics()
    {
        if (inWater && currentWaterProperties != null)
        {
            timeInWater += Time.deltaTime;
            
            // Apply gentle buoyancy only when sinking too fast
            if (rb.linearVelocity.y < -3f)
            {
                // Gentle upward force to prevent endless sinking
                rb.AddForce(Vector2.up * currentWaterProperties.buoyancyForce * 0.3f, ForceMode2D.Force);
            }
            
            // Apply gentle drag force
            Vector2 dragForce = -rb.linearVelocity * currentWaterProperties.dragForce * 0.3f;
            rb.AddForce(dragForce, ForceMode2D.Force);
            
            // Apply water current (vertical)
            if (currentWaterProperties.currentForceY != 0)
            {
                rb.AddForce(Vector2.up * currentWaterProperties.currentForceY, ForceMode2D.Force);
            }
            
            // Handle player rotation for tilting effect
            HandleWaterTilt();
            
            // Handle breathing (if water doesn't allow breathing)
            if (!currentWaterProperties.allowBreathing && timeInWater > 10f) // 10 seconds before drowning starts
            {
                // Start drowning damage
                if (timeInWater > 10f && ((int)timeInWater) % 2 == 0) // Damage every 2 seconds after 10 seconds
                {
                    TakeDamage(5); // Drowning damage
                }
            }
        }
        else if (!inWater)
        {
            // Reset rotation when not in water (only when completely out of all water)
            transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.identity, Time.deltaTime * 5f);
        }
    }
    
    private void HandleWaterTilt()
    {
        // Use inWater state instead of currentWaterProperties to maintain tilt across water transitions
        if (inWater)
        {
            // Tilt player based on horizontal movement
            float tiltAngle = 0f;
            
            if (Mathf.Abs(horizontalInput) > 0.1f)
            {
                // Tilt in direction of movement
                tiltAngle = horizontalInput * 15f; // 15 degrees max tilt
            }
            
            // Apply tilt rotation smoothly
            Quaternion targetRotation = Quaternion.Euler(0, 0, -tiltAngle);
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * 3f);
        }
    }
    
    private void Die()
    {
        // Stop any status effects
        StopBurningEffect();
        
        // Reset player to spawn position
        transform.position = spawnPosition;
        
        // Reset health
        currentHealth = maxHealth;
        
        // Reset aegis shield
        currentAegisShield = 0f;
        UpdateAegisOutline();
        
        // Update health bar display
        UpdateHealthBar();
        
        // Reset physics
        rb.linearVelocity = Vector2.zero;
        
        // Reset fall tracking
        isFalling = false;
        wasGroundedLastFrame = false;
        
        // Reposition camera directly on player after respawn
        CameraFollow cameraFollow = Camera.main?.GetComponent<CameraFollow>();
        if (cameraFollow != null)
        {
            cameraFollow.CenterOnTarget();
        }
    }
    
    private void CreateHealthBar()
    {
        // Create a world space canvas for the health bar
        GameObject canvasGO = new GameObject("HealthBarCanvas");
        healthBarCanvas = canvasGO.AddComponent<Canvas>();
        healthBarCanvas.renderMode = RenderMode.WorldSpace;
        healthBarCanvas.sortingOrder = 10; // Ensure it renders on top
        
        // Set canvas size and position
        RectTransform canvasRect = healthBarCanvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(healthBarSize.x * 100, healthBarSize.y * 100); // Scale up for world space
        canvasRect.localScale = Vector3.one * 0.01f; // Scale down for proper world size
        
        // Create background (grey bar that shows full health bar area)
        GameObject backgroundGO = new GameObject("HealthBarBackground");
        backgroundGO.transform.SetParent(canvasGO.transform, false);
        healthBarBackground = backgroundGO.AddComponent<Image>();
        
        // Set sprite for background
        // Create a simple white texture for the background
        Texture2D backgroundTexture = new Texture2D(1, 1);
        backgroundTexture.SetPixel(0, 0, Color.white);
        backgroundTexture.Apply();
        healthBarBackground.sprite = Sprite.Create(backgroundTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f);
        
        healthBarBackground.color = new Color(1f, 1f, 1f, 0.9f); // White background
        
        RectTransform bgRect = backgroundGO.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        bgRect.anchoredPosition = Vector2.zero;
        
        // Create fill (colored bar that shows current health)
        GameObject fillGO = new GameObject("HealthBarFill");
        fillGO.transform.SetParent(backgroundGO.transform, false); // Child of background
        healthBarFill = fillGO.AddComponent<Image>();
        
        // Create a white sprite for the fill
        Texture2D fillTexture = new Texture2D(1, 1);
        fillTexture.SetPixel(0, 0, Color.white);
        fillTexture.Apply();
        healthBarFill.sprite = Sprite.Create(fillTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f);
        
        healthBarFill.color = Color.green;
        healthBarFill.type = Image.Type.Filled;
        healthBarFill.fillMethod = Image.FillMethod.Horizontal;
        healthBarFill.fillOrigin = (int)Image.OriginHorizontal.Left; // Fill from left to right, empty from right to left
        
        RectTransform fillRect = fillGO.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.sizeDelta = Vector2.zero;
        fillRect.anchoredPosition = Vector2.zero;
        
        // Update initial health display
        UpdateHealthBar();
    }
    
    private void UpdateHealthBarPosition()
    {
        if (healthBarCanvas != null)
        {
            // Position the health bar above the player
            Vector3 healthBarPosition = transform.position + healthBarOffset;
            healthBarCanvas.transform.position = healthBarPosition;
            
            // Make health bar face the camera
            if (Camera.main != null)
            {
                healthBarCanvas.transform.LookAt(Camera.main.transform);
                healthBarCanvas.transform.Rotate(0, 180, 0); // Flip to face properly
            }
        }
    }
    
    private void UpdateHealthBar()
    {
        if (healthBarFill != null)
        {
            float healthPercent = (float)currentHealth / maxHealth;
            healthBarFill.fillAmount = healthPercent;
            
            // Change color based on health percentage
            if (healthPercent > 0.6f)
                healthBarFill.color = Color.green;
            else if (healthPercent > 0.3f)
                healthBarFill.color = Color.yellow;
            else
                healthBarFill.color = Color.red;
        }
    }
    
    private bool CheckGroundAtPosition(Vector2 position, float distance)
    {
        // Use multiple detection methods for better compatibility with tilemaps
        
        // Method 1: Raycast detection
        RaycastHit2D[] hits = Physics2D.RaycastAll(position, Vector2.down, distance, groundLayerMask);
        
        foreach (RaycastHit2D hit in hits)
        {
            // Skip our own colliders
            if (hit.collider != playerCollider && hit.collider.gameObject != gameObject)
            {
                return true;
            }
        }
        
        // Method 2: Point overlap detection (good for composite colliders)
        Vector2 pointCheckPos = position + Vector2.down * (distance * 0.5f);
        Collider2D pointHit = Physics2D.OverlapPoint(pointCheckPos, groundLayerMask);
        if (pointHit != null && pointHit != playerCollider && pointHit.gameObject != gameObject)
        {
            return true;
        }
        
        // Method 3: Small circle overlap detection - better for CapsuleCollider2D
        Collider2D circleHit = Physics2D.OverlapCircle(position + Vector2.down * (distance * 0.5f), 0.1f, groundLayerMask);
        if (circleHit != null && circleHit != playerCollider && circleHit.gameObject != gameObject)
        {
            return true;
        }
        
        return false;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("OneWayPlatform"))
        {
            currentPlatform = collision.gameObject;
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("OneWayPlatform"))
        {
            currentPlatform = null;
        }
    }

    private IEnumerator DisableCollision()
    {
        BoxCollider2D platformCollider = currentPlatform.GetComponent<BoxCollider2D>();
        Physics2D.IgnoreCollision(playerCollider, platformCollider, true);
        yield return new WaitForSeconds(0.5f);
        Physics2D.IgnoreCollision(playerCollider, platformCollider, false);
    }
    
    private void UpdateSprite()
    {
        if (spriteRenderer == null) return;
        
        // Check if player is moving horizontally
        bool isMoving = Mathf.Abs(horizontalInput) > 0.1f;
        
        // Change sprite based on movement and ground state
        if (!isGrounded)
        {
            // Use falling sprite when in the air
            if (fallingSprite != null)
                spriteRenderer.sprite = fallingSprite;
            else if (idleSprite != null)
                spriteRenderer.sprite = idleSprite; // Fallback to idle if no falling sprite
            
            // Reset animation when in air
            animationTimer = 0f;
            useFirstWalkSprite = true;
        }
        else if (isMoving && isGrounded)
        {
            // Animate between two walking sprites when moving on ground
            animationTimer += Time.deltaTime;
            
            if (animationTimer >= animationSpeed)
            {
                // Switch between walking sprites
                useFirstWalkSprite = !useFirstWalkSprite;
                animationTimer = 0f;
            }
            
            // Set the appropriate walking sprite
            if (useFirstWalkSprite && walkingSprite1 != null)
                spriteRenderer.sprite = walkingSprite1;
            else if (!useFirstWalkSprite && walkingSprite2 != null)
                spriteRenderer.sprite = walkingSprite2;
        }
        else
        {
            // Use idle sprite when not moving and grounded
            if (idleSprite != null)
                spriteRenderer.sprite = idleSprite;
            
            // Reset animation when not moving
            animationTimer = 0f;
            useFirstWalkSprite = true;
        }
    }
    
    // ===== MISSING METHODS FOR WEAPON SYSTEM =====
    
    // Movement Detection Methods
    public bool IsMovingHorizontally()
    {
        // For owner: check input for immediate feedback
        // For non-owners: check actual velocity since they don't have input
        if (IsOwner)
        {
            return Mathf.Abs(horizontalInput) > 0.1f;
        }
        else
        {
            return rb != null && Mathf.Abs(rb.linearVelocity.x) > 0.1f;
        }
    }
    
    // ===== ANIMATION CONTROLLER SYSTEM =====
    
    /// <summary>
    /// Set the animation controller directly (networked)
    /// </summary>
    public void SetAnimationController(RuntimeAnimatorController controller)
    {
        if (IsOwner)
        {
            // Determine shard type based on controller
            int shardType = GetShardTypeFromController(controller);
            SetAnimationControllerServerRpc(shardType);
        }
    }
    
    [ServerRpc]
    private void SetAnimationControllerServerRpc(int shardType)
    {
        networkEquippedShardType.Value = shardType;
    }
    
    private void UpdateAnimationController(int shardType)
    {
        // This method is called when networkEquippedShardType changes
        if (playerAnimator == null) return;
        
        RuntimeAnimatorController targetController = GetControllerFromShardType(shardType);
        
        // Only switch if controller is different to avoid unnecessary changes
        if (currentAnimController != targetController)
        {
            playerAnimator.runtimeAnimatorController = targetController;
            currentAnimController = targetController;
            Debug.Log($"Switched animation controller to: {(targetController != null ? targetController.name : "null")} for shard type {shardType}");
        }
    }
    
    private int GetShardTypeFromController(RuntimeAnimatorController controller)
    {
        // Convert controller to shard type (you'll need to implement this based on your shard system)
        if (controller == null || controller == defaultPlayerAnimController)
            return 0; // No shard
            
        // Add logic to determine shard type from controller name or reference
        // This is a placeholder - you'll need to implement based on your actual shard system
        if (controller.name.Contains("Valor")) return 1;
        if (controller.name.Contains("Whisper")) return 2;
        if (controller.name.Contains("Storm")) return 3;
        
        return 0; // Default to no shard
    }
    
    private RuntimeAnimatorController GetControllerFromShardType(int shardType)
    {
        // Convert shard type back to controller
        // Get the WeaponClassController to access the proper controllers
        WeaponClassController weaponController = GetComponent<WeaponClassController>();
        if (weaponController == null)
        {
            Debug.LogError("WeaponClassController not found on player!");
            return defaultPlayerAnimController;
        }

        switch (shardType)
        {
            case 0: return defaultPlayerAnimController; // No shard
            case 1: // Valor shard
                return weaponController.valorShardPlayerAnimController ?? defaultPlayerAnimController;
            case 2: // Whisper shard
                return weaponController.whisperShardPlayerAnimController ?? defaultPlayerAnimController;
            case 3: // Storm shard
                return weaponController.stormShardPlayerAnimController ?? defaultPlayerAnimController;
            default: 
                return defaultPlayerAnimController;
        }
    }
    
    /// <summary>
    /// Update animation parameters based on current player state
    /// </summary>
    public void UpdateAnimationParameters()
    {
        if (playerAnimator == null) return;
        
        // Fallback to default controller if no controller is set
        if (currentAnimController == null)
        {
            if (defaultPlayerAnimController != null)
            {
                SetAnimationController(defaultPlayerAnimController);
                Debug.Log("PlayerMovement: Fallback to default animation controller");
            }
            else
            {
                // Only log this warning once every 5 seconds to prevent spam
                if (Time.time - lastAnimationWarningTime > 5f)
                {
                    Debug.LogWarning("PlayerMovement: No animation controller available (default is null)");
                    lastAnimationWarningTime = Time.time;
                }
                return;
            }
        }
        
        // Update movement state
        bool isMoving = IsMovingHorizontally();
        if (isPlayerWalking != isMoving)
        {
            isPlayerWalking = isMoving;
            playerAnimator.SetBool("isWalking", isPlayerWalking);
            
            // Sync to network if this is the owner
            if (IsOwner)
            {
                networkIsWalking.Value = isPlayerWalking;
            }
        }

        // Update jumping state - should be true until player lands
        bool isJumping = !isGrounded;
        if (isPlayerJumping != isJumping)
        {
            isPlayerJumping = isJumping;
            playerAnimator.SetBool("isJumping", isPlayerJumping);
        }

        // Update attacking state and sync to network
        if (IsOwner && networkIsAttacking.Value != isPlayerAttacking)
        {
            networkIsAttacking.Value = isPlayerAttacking;
        }

        // Update other parameters
        playerAnimator.SetBool("isDead", isPlayerDead);
        playerAnimator.SetBool("isAttacking", isPlayerAttacking);
        playerAnimator.SetInteger("attackType", currentAttackType);
    }
    
    /// <summary>
    /// Trigger attack animation with specific attack type
    /// </summary>
    public void TriggerAttackAnimation(int attackType, float duration = 0.5f)
    {
        if (playerAnimator == null) return;
        
        isPlayerAttacking = true;
        currentAttackType = attackType;
        playerAnimator.SetBool("isAttacking", true);
        playerAnimator.SetInteger("attackType", attackType);
        
        // Sync attack state to network if this is the owner
        if (IsOwner)
        {
            networkIsAttacking.Value = true;
        }
        
        // Reset attack state after duration
        StartCoroutine(ResetAttackState(duration));
        
        Debug.Log($"Triggered attack animation: Type {attackType}");
    }
    
    /// <summary>
    /// Trigger attack animation with specific attack type (animation events control duration)
    /// </summary>
    public void TriggerAttackAnimation(int attackType)
    {
        if (playerAnimator == null) return;
        
        isPlayerAttacking = true;
        currentAttackType = attackType;
        playerAnimator.SetBool("isAttacking", true);
        playerAnimator.SetInteger("attackType", attackType);
        
        // Sync attack state to network if this is the owner
        if (IsOwner)
        {
            networkIsAttacking.Value = true;
        }
        
        Debug.Log($"Triggered attack animation (event-based): Type {attackType}");
    }
    
    /// <summary>
    /// Animation Event: End attack animation (called by animation events)
    /// </summary>
    public void OnAttackAnimationEnd()
    {
        isPlayerAttacking = false;
        currentAttackType = 0;
        
        if (playerAnimator != null)
        {
            playerAnimator.SetBool("isAttacking", false);
        }
        
        // Sync attack state to network if this is the owner
        if (IsOwner)
        {
            networkIsAttacking.Value = false;
        }
        
        // Notify weapon controller that animation ended
        WeaponClassController weaponController = GetComponent<WeaponClassController>();
        if (weaponController != null)
        {
            // Use reflection to call the private EndAttackAnimation method
            var method = typeof(WeaponClassController).GetMethod("EndAttackAnimation", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (method != null)
            {
                method.Invoke(weaponController, null);
            }
        }
        
        Debug.Log("Attack animation ended via animation event");
    }
    
    /// <summary>
    /// Animation Event: Trigger lightning particles at arc point 1 (Storm Shard Attack 1)
    /// </summary>
    public void OnStormArcPoint1()
    {
        // Only trigger for the owner, but show effect for all clients
        if (IsOwner)
        {
            TriggerLightningParticlesServerRpc(1);
        }
        Debug.Log("Storm Shard lightning particles triggered at arc point 1");
    }
    
    /// <summary>
    /// Animation Event: Trigger lightning particles at arc point 2 (Storm Shard Attack 2)
    /// </summary>
    public void OnStormArcPoint2()
    {
        // Only trigger for the owner, but show effect for all clients
        if (IsOwner)
        {
            TriggerLightningParticlesServerRpc(2);
        }
        Debug.Log("Storm Shard lightning particles triggered at arc point 2");
    }
    
    /// <summary>
    /// Server RPC to trigger lightning particles for all clients
    /// </summary>
    [ServerRpc]
    private void TriggerLightningParticlesServerRpc(int arcPoint)
    {
        TriggerLightningParticlesClientRpc(arcPoint);
    }
    
    /// <summary>
    /// Client RPC to spawn lightning particles on all clients
    /// </summary>
    [ClientRpc]
    private void TriggerLightningParticlesClientRpc(int arcPoint)
    {
        TriggerLightningParticles(arcPoint);
    }
    
    /// <summary>
    /// Spawn lightning particles at the specified arc point
    /// </summary>
    private void TriggerLightningParticles(int arcPoint)
    {
        if (lightningArcParticles == null)
        {
            Debug.LogWarning("Lightning arc particles not assigned!");
            return;
        }
        
        // Get the weapon controller to access particle point positions
        WeaponClassController weaponController = GetComponent<WeaponClassController>();
        if (weaponController == null)
        {
            Debug.LogWarning("WeaponClassController not found for lightning particles!");
            return;
        }
        
        // Use reflection to get the particle points (assuming they exist in WeaponClassController)
        var particlePointsField = typeof(WeaponClassController).GetField("particlePoints", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (particlePointsField != null)
        {
            Transform[] particlePoints = particlePointsField.GetValue(weaponController) as Transform[];
            if (particlePoints != null && arcPoint > 0 && arcPoint <= particlePoints.Length)
            {
                Vector3 spawnPosition = particlePoints[arcPoint - 1].position; // arcPoint is 1-based, array is 0-based
                
                // Instantiate lightning particles at the arc point
                ParticleSystem lightningInstance = Instantiate(lightningArcParticles, spawnPosition, particlePoints[arcPoint - 1].rotation);
                
                // Force the particle system settings to ensure visibility
                var main = lightningInstance.main;
                main.startLifetime = lightningParticleDuration; // Use configurable duration
                main.playOnAwake = true;
                
                // Start playing immediately
                lightningInstance.Play();
                
                // Force emission burst if particles aren't visible
                var emission = lightningInstance.emission;
                emission.enabled = true;
                if (emission.burstCount == 0)
                {
                    // Add a burst if there isn't one configured
                    emission.SetBursts(new ParticleSystem.Burst[] {
                        new ParticleSystem.Burst(0f, 50) // Emit 50 particles immediately
                    });
                }
                
                // Auto-destroy the particle system after the configured duration
                StartCoroutine(DestroyParticleSystemAfterDuration(lightningInstance, lightningParticleDuration + 1f));
                
                Debug.Log($"Lightning particles spawned at arc point {arcPoint} - Position: {spawnPosition}, Duration: {lightningParticleDuration}s");
            }
            else
            {
                Debug.LogWarning($"Invalid arc point {arcPoint} or particle points not found! Available points: {particlePoints?.Length ?? 0}");
            }
        }
        else
        {
            Debug.LogWarning("Particle points not found in WeaponClassController!");
            
            // Fallback: spawn at player position if particle points aren't found
            Vector3 fallbackPosition = transform.position + Vector3.up * 0.5f;
            ParticleSystem lightningInstance = Instantiate(lightningArcParticles, fallbackPosition, Quaternion.identity);
            
            var main = lightningInstance.main;
            main.startLifetime = lightningParticleDuration;
            
            lightningInstance.Play();
            StartCoroutine(DestroyParticleSystemAfterDuration(lightningInstance, lightningParticleDuration + 1f));
            
            Debug.Log($"Lightning particles spawned at fallback position (player) - Position: {fallbackPosition}");
        }
    }
    
    /// <summary>
    /// Coroutine to destroy particle system after a specified duration
    /// </summary>
    private System.Collections.IEnumerator DestroyParticleSystemAfterDuration(ParticleSystem particles, float duration)
    {
        // Wait for the specified duration
        yield return new WaitForSeconds(duration);
        
        // Destroy the particle system
        if (particles != null)
        {
            Destroy(particles.gameObject);
        }
    }
    
    /// <summary>
    /// Coroutine to destroy particle system after it finishes playing (backup method)
    /// </summary>
    private System.Collections.IEnumerator DestroyParticleSystemWhenDone(ParticleSystem particles)
    {
        // Wait until the particle system stops playing
        while (particles != null && particles.isPlaying)
        {
            yield return null;
        }
        
        // Destroy the particle system
        if (particles != null)
        {
            Destroy(particles.gameObject);
        }
    }
    
    /// <summary>
    /// Reset attack animation state
    /// </summary>
    private System.Collections.IEnumerator ResetAttackState(float duration)
    {
        yield return new WaitForSeconds(duration);
        
        isPlayerAttacking = false;
        currentAttackType = 0;
        if (playerAnimator != null)
        {
            playerAnimator.SetBool("isAttacking", false);
            playerAnimator.SetInteger("attackType", 0);
        }
        
        // Sync attack state to network if this is the owner
        if (IsOwner)
        {
            networkIsAttacking.Value = false;
        }
    }
    
    /// <summary>
    /// Set death animation state
    /// </summary>
    public void SetDeathAnimation(bool isDead)
    {
        if (isPlayerDead != isDead)
        {
            isPlayerDead = isDead;
            if (playerAnimator != null)
            {
                playerAnimator.SetBool("isDead", isDead);
            }
        }
    }
    
    // Ultimate System Methods
    public bool HasFullUltimate()
    {
        return currentUltimateCharge >= maxUltimateCharge;
    }
    
    public void SetMaxUltimateCharge(float maxCharge)
    {
        maxUltimateCharge = maxCharge;
    }
    
    public void AddUltimateCharge(float charge)
    {
        float oldCharge = currentUltimateCharge;
        bool wasAlreadyFull = currentUltimateCharge >= maxUltimateCharge;
        currentUltimateCharge = Mathf.Min(currentUltimateCharge + charge, maxUltimateCharge);
        
        // If we tried to add charge to a full bar, trigger the effect (but not if already playing)
        if (wasAlreadyFull && charge > 0 && !isUltimateEffectPlaying)
        {
            StartCoroutine(PlayUltimateFullEffect());
            Debug.Log("Ultimate already full! Triggering full effect from overflow charge.");
        }
        
        // Debug.Log($"Ultimate charge: {oldCharge:F1} + {charge:F1} = {currentUltimateCharge:F1} / {maxUltimateCharge:F1} ({(currentUltimateCharge/maxUltimateCharge)*100:F1}%)");
    }
    
    public float GetCurrentUltimateCharge()
    {
        return currentUltimateCharge;
    }
    
    public void ConsumeUltimateCharge(float charge)
    {
        currentUltimateCharge = Mathf.Max(0f, currentUltimateCharge - charge);
    }
    
    // Aegis Shield System Methods
    public float GetCurrentAegisShield()
    {
        return currentAegisShield;
    }
    
    public float GetMaxAegisShield()
    {
        return maxAegisShield;
    }
    
    public void SetAegisShield(float shieldValue)
    {
        if (IsOwner)
        {
            SetAegisShieldServerRpc(shieldValue);
        }
    }
    
    [ServerRpc]
    private void SetAegisShieldServerRpc(float shieldValue)
    {
        networkCurrentShield.Value = Mathf.Clamp(shieldValue, 0f, maxAegisShield);
    }
    
    // Damage Modifier Methods
    public int GetModifiedMeleeDamage(float baseDamage)
    {
        float modifiedDamage = baseDamage;
        
        // Apply Attack buffs (general melee damage)
        foreach (ActiveBuff buff in activeBuffs)
        {
            if (buff.type == BuffType.Attack && !buff.IsExpired)
            {
                modifiedDamage += baseDamage * (buff.value / 100f);
            }
        }
        
        // Apply Strength buffs (melee-specific damage)
        foreach (ActiveBuff buff in activeBuffs)
        {
            if (buff.type == BuffType.Strength && !buff.IsExpired)
            {
                modifiedDamage += baseDamage * (buff.value / 100f);
            }
        }
        
        return Mathf.RoundToInt(modifiedDamage);
    }
    
    public int GetModifiedMagicDamage(float baseDamage)
    {
        float modifiedDamage = baseDamage;
        
        // Apply Flux buffs (magic damage)
        foreach (ActiveBuff buff in activeBuffs)
        {
            if (buff.type == BuffType.Flux && !buff.IsExpired)
            {
                modifiedDamage += baseDamage * (buff.value / 100f);
            }
        }
        
        return Mathf.RoundToInt(modifiedDamage);
    }
    
    public int GetModifiedMaxHealth()
    {
        float modifiedMaxHealth = maxHealth;
        
        // Apply Durability buffs (max health increase)
        foreach (ActiveBuff buff in activeBuffs)
        {
            if (buff.type == BuffType.Durability && !buff.IsExpired)
            {
                modifiedMaxHealth += maxHealth * (buff.value / 100f);
            }
        }
        
        return Mathf.RoundToInt(modifiedMaxHealth);
    }
    
    // Facing Direction Method
    public bool IsFacingRight()
    {
        return !spriteRenderer.flipX; // Assuming flipX = true means facing left
    }
    
    // Ladder System Methods
    public void SetNearbyLadder(GameObject ladder)
    {
        nearbyLadder = ladder;
    }
    
    // Buff System Methods
    public void ApplyAegisBuff(float shieldPercentage = 100f)
    {
        // Aegis is now a durationless shield system, not a timed buff
        float currentMaxHealth = GetModifiedMaxHealth();
        maxAegisShield = currentMaxHealth; // Set max shield to current max health
        float shieldValue = maxAegisShield * (shieldPercentage / 100f);
        currentAegisShield = Mathf.Min(currentAegisShield + shieldValue, maxAegisShield);
        
        // Update tracking variable
        previousMaxHealth = currentMaxHealth;
        
        Debug.Log($"Aegis Shield Applied: +{shieldValue:F1} shield ({currentAegisShield:F1}/{maxAegisShield:F1})");
        UpdateAegisOutline();
    }
    
    // Overload for Valor Shard attack-based aegis with configurable cap
    public void ApplyValorAttackAegisBuff(float shieldPercentage = 5f, float capPercentage = 33f)
    {
        float currentMaxHealth = GetModifiedMaxHealth();
        float valorAegisCap = currentMaxHealth * (capPercentage / 100f); // Configurable cap percentage
        maxAegisShield = currentMaxHealth; // Still set max to full health for non-attack sources
        
        float shieldValue = currentMaxHealth * (shieldPercentage / 100f);
        
        // Check if adding this would exceed the Valor cap (but only for attack-generated aegis)
        float potentialNewShield = currentAegisShield + shieldValue;
        
        if (potentialNewShield > valorAegisCap)
        {
            // Cap the shield value to not exceed the specified percentage of max health
            shieldValue = Mathf.Max(0, valorAegisCap - currentAegisShield);
            Debug.Log($"Valor Aegis Cap: Limited to {valorAegisCap:F1} shield ({capPercentage}% of {currentMaxHealth:F1} max health)");
        }
        
        currentAegisShield = Mathf.Min(currentAegisShield + shieldValue, maxAegisShield);
        
        // Update tracking variable
        previousMaxHealth = currentMaxHealth;
        
        Debug.Log($"Valor Attack Aegis Applied: +{shieldValue:F1} shield ({currentAegisShield:F1}/{maxAegisShield:F1}) [Cap: {valorAegisCap:F1} ({capPercentage}%)]");
        UpdateAegisOutline();
    }
    
    public void ApplyStrengthBuff(float percentage = 10f, float duration = 15f)
    {
        ApplyBuff(BuffType.Strength, percentage, duration, "Increases melee attack damage");
    }
    
    public void ApplyVitalityBuff(float percentage = 5f, float duration = 20f)
    {
        ApplyBuff(BuffType.Vitality, percentage, duration, "Increases health regeneration rate");
    }
    
    public void ApplyFluxBuff(float percentage = 15f, float duration = 12f)
    {
        ApplyBuff(BuffType.Flux, percentage, duration, "Increases magic damage output");
    }
    
    public void ApplyDurabilityBuff(float percentage = 20f, float duration = 30f)
    {
        ApplyBuff(BuffType.Durability, percentage, duration, "Increases maximum health points");
    }
    
    public void ApplySwiftnessBuff(float percentage = 25f, float duration = 10f)
    {
        ApplyBuff(BuffType.Swiftness, percentage, duration, "Increases player movement speed");
    }
    
    public void ApplyBuff(BuffType buffType, float value, float duration, string description = "")
    {
        // For networked gameplay, buffs are applied locally but critical ones are synced
        // Check if buff type allows stacking - most buffs are now stackable
        bool allowStacking = (buffType == BuffType.Durability || buffType == BuffType.Flux || 
                             buffType == BuffType.Swiftness || buffType == BuffType.Strength || 
                             buffType == BuffType.Vitality);
        
        if (!allowStacking)
        {
            // Remove existing buff of same type for non-stackable buffs (Attack and Aegis)
            activeBuffs.RemoveAll(b => b.type == buffType);
        }
        
        // Add new buff
        ActiveBuff newBuff = new ActiveBuff(buffType, value, duration, description);
        activeBuffs.Add(newBuff);
        
        // For critical buffs that affect other players (like Aegis shield), sync them
        if (buffType == BuffType.Aegis && IsOwner)
        {
            SyncAegisBuffServerRpc(value, duration);
        }
        
        Debug.Log($"Buff applied: {buffType}, value: {value}, duration: {duration}, stacking: {allowStacking}");
        UpdateBuffUI();
    }
    
    [ServerRpc]
    private void SyncAegisBuffServerRpc(float value, float duration)
    {
        // This syncs Aegis shield values across all clients
        float shieldAmount = (value / 100f) * maxAegisShield;
        networkCurrentShield.Value = Mathf.Min(networkCurrentShield.Value + shieldAmount, maxAegisShield);
    }
    
    private void UpdateBuffs()
    {
        bool buffsChanged = false;
        
        // Remove expired buffs
        for (int i = activeBuffs.Count - 1; i >= 0; i--)
        {
            if (activeBuffs[i].IsExpired)
            {
                activeBuffs.RemoveAt(i);
                buffsChanged = true;
            }
        }
        
        // Hide tooltip if no buffs remain
        if (activeBuffs.Count == 0 && tooltipPanel != null && tooltipPanel.activeInHierarchy)
        {
            HideBuffTooltip();
        }
        
        // Update UI if buffs changed
        if (buffsChanged)
        {
            UpdateBuffUI();
        }
    }
    
    // ===== SCREEN UI SYSTEM =====
    
    private void CreateScreenUI()
    {
        // Prevent duplicate UI creation
        if (screenUICanvas != null)
        {
            Debug.LogWarning("Screen UI already exists! Skipping creation to prevent duplicates.");
            return;
        }
        
        // Create main screen UI canvas
        GameObject canvasGO = new GameObject("PlayerScreenUICanvas");
        screenUICanvas = canvasGO.AddComponent<Canvas>();
        screenUICanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        screenUICanvas.sortingOrder = 100; // High priority
        
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        
        canvasGO.AddComponent<GraphicRaycaster>();
        
        // Ensure EventSystem exists for UI interactions
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject eventSystemGO = new GameObject("EventSystem");
            eventSystemGO.AddComponent<EventSystem>();
            // Use InputSystemUIInputModule for new Input System instead of StandaloneInputModule
            eventSystemGO.AddComponent<InputSystemUIInputModule>();
        }
        
        // Create health bar at bottom of screen
        CreateScreenHealthBar();
        
        // Create ultimate bar
        CreateUltimateBar();
        
        // Create buff list panel
        CreateBuffIconArray();
    }
    
    private void CreateAegisOutline(GameObject healthBarParent)
    {
        // Create a yellow texture for the aegis outline
        Texture2D yellowTexture = new Texture2D(1, 1);
        yellowTexture.SetPixel(0, 0, Color.yellow);
        yellowTexture.Apply();
        Sprite yellowSprite = Sprite.Create(yellowTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
        
        float outlineThickness = 4f; // Increased thickness of outline bars in pixels
        float outlineExtension = 6f; // How much wider/taller the outline extends beyond healthbar
        
        // Left outline bar (extends slightly beyond health bar height to overlap with top/bottom)
        GameObject leftOutlineGO = new GameObject("AegisOutlineLeft");
        leftOutlineGO.transform.SetParent(healthBarParent.transform, false);
        
        RectTransform leftRect = leftOutlineGO.AddComponent<RectTransform>();
        leftRect.anchorMin = new Vector2(0f, 0f);
        leftRect.anchorMax = new Vector2(0f, 1f);
        leftRect.sizeDelta = new Vector2(outlineThickness, outlineExtension * 2f); // Extended height to overlap corners
        leftRect.anchoredPosition = new Vector2(-(outlineThickness/2f + outlineExtension/2f), 0f); // Move further left
        
        aegisOutlineLeft = leftOutlineGO.AddComponent<Image>();
        aegisOutlineLeft.sprite = yellowSprite;
        aegisOutlineLeft.color = Color.yellow;
        aegisOutlineLeft.gameObject.SetActive(false); // Hidden by default
        
        // Top outline bar (extends beyond health bar width to overlap with left/right outlines)
        GameObject topOutlineGO = new GameObject("AegisOutlineTop");
        topOutlineGO.transform.SetParent(healthBarParent.transform, false);
        
        RectTransform topRect = topOutlineGO.AddComponent<RectTransform>();
        topRect.anchorMin = new Vector2(0f, 1f);
        topRect.anchorMax = new Vector2(0f, 1f); // Keep anchor at left edge for consistent positioning
        topRect.pivot = new Vector2(0f, 0.5f); // Set pivot to left-center
        topRect.sizeDelta = new Vector2(0f, outlineThickness); // Initial width (will be updated by UpdateAegisOutline)
        topRect.anchoredPosition = new Vector2(-7f, 4f); // Adjusted positioning
        
        aegisOutlineTop = topOutlineGO.AddComponent<Image>();
        aegisOutlineTop.sprite = yellowSprite;
        aegisOutlineTop.color = Color.yellow;
        aegisOutlineTop.gameObject.SetActive(false); // Hidden by default
        
        // Bottom outline bar (extends beyond health bar width to overlap with left/right outlines)
        GameObject bottomOutlineGO = new GameObject("AegisOutlineBottom");
        bottomOutlineGO.transform.SetParent(healthBarParent.transform, false);
        
        RectTransform bottomRect = bottomOutlineGO.AddComponent<RectTransform>();
        bottomRect.anchorMin = new Vector2(0f, 0f);
        bottomRect.anchorMax = new Vector2(0f, 0f); // Keep anchor at left edge for consistent positioning
        bottomRect.pivot = new Vector2(0f, 0.5f); // Set pivot to left-center
        bottomRect.sizeDelta = new Vector2(0f, outlineThickness); // Initial width (will be updated by UpdateAegisOutline)
        bottomRect.anchoredPosition = new Vector2(-7f, -5f); // Set to -5 as per your working values
        
        aegisOutlineBottom = bottomOutlineGO.AddComponent<Image>();
        aegisOutlineBottom.sprite = yellowSprite;
        aegisOutlineBottom.color = Color.yellow;
        aegisOutlineBottom.gameObject.SetActive(false); // Hidden by default
        
        // Right outline bar (extends slightly beyond health bar height to overlap with top/bottom)
        GameObject rightOutlineGO = new GameObject("AegisOutlineRight");
        rightOutlineGO.transform.SetParent(healthBarParent.transform, false);
        
        RectTransform rightRect = rightOutlineGO.AddComponent<RectTransform>();
        rightRect.anchorMin = new Vector2(1f, 0f);
        rightRect.anchorMax = new Vector2(1f, 1f);
        rightRect.sizeDelta = new Vector2(outlineThickness, outlineExtension * 2f); // Extended height to overlap corners
        rightRect.anchoredPosition = new Vector2(outlineThickness/2f + outlineExtension/2f, 0f); // Move further right
        
        aegisOutlineRight = rightOutlineGO.AddComponent<Image>();
        aegisOutlineRight.sprite = yellowSprite;
        aegisOutlineRight.color = Color.yellow;
        aegisOutlineRight.gameObject.SetActive(false); // Hidden by default
    }
    
    private void UpdateAegisOutline()
    {
        if (aegisOutlineLeft == null || aegisOutlineTop == null || 
            aegisOutlineBottom == null || aegisOutlineRight == null) return;
        
        bool hasAnyShield = currentAegisShield > 0f;
        bool hasFullShield = currentAegisShield >= maxAegisShield;
        
        // Show left, top, bottom outlines when any shield is present
        aegisOutlineLeft.gameObject.SetActive(hasAnyShield);
        aegisOutlineTop.gameObject.SetActive(hasAnyShield);
        aegisOutlineBottom.gameObject.SetActive(hasAnyShield);
        
        // Show right outline only when shield is at maximum (full shield)
        aegisOutlineRight.gameObject.SetActive(hasFullShield);
        
        // Adjust top and bottom outline widths based on shield percentage
        if (hasAnyShield)
        {
            float shieldPercentage = currentAegisShield / maxAegisShield;
            float outlineThickness = 4f; // Match the thickness used in creation
            
            // Full width is 968 to represent 100% shield coverage
            float fullWidth = 968f;
            float dynamicWidth = shieldPercentage * fullWidth; // Scale width based on shield percentage
            
            // Update top outline width (extends from left edge to shield percentage with full overlap)
            RectTransform topRect = aegisOutlineTop.GetComponent<RectTransform>();
            topRect.anchorMin = new Vector2(0f, 1f);
            topRect.anchorMax = new Vector2(0f, 1f); // Keep anchor at left edge, control width with sizeDelta
            topRect.pivot = new Vector2(0f, 0.5f); // Set pivot to left-center
            topRect.sizeDelta = new Vector2(dynamicWidth, outlineThickness);
            topRect.anchoredPosition = new Vector2(-7f, 4f); // Use exact working values
            
            // Update bottom outline width (extends from left edge to shield percentage with full overlap)
            RectTransform bottomRect = aegisOutlineBottom.GetComponent<RectTransform>();
            bottomRect.anchorMin = new Vector2(0f, 0f);
            bottomRect.anchorMax = new Vector2(0f, 0f); // Keep anchor at left edge, control width with sizeDelta
            bottomRect.pivot = new Vector2(0f, 0.5f); // Set pivot to left-center
            bottomRect.sizeDelta = new Vector2(dynamicWidth, outlineThickness);
            bottomRect.anchoredPosition = new Vector2(-7f, -5f); // Use exact working values
        }
    }
    
    private void CreateScreenHealthBar()
    {
        // Health bar container
        GameObject healthBarGO = new GameObject("ScreenHealthBar");
        healthBarGO.transform.SetParent(screenUICanvas.transform, false);
        
        RectTransform healthBarRect = healthBarGO.AddComponent<RectTransform>();
        healthBarRect.anchorMin = new Vector2(0.25f, 0.08f); // Centered horizontally, bottom area
        healthBarRect.anchorMax = new Vector2(0.75f, 0.11f); // 50% width, 3% height
        healthBarRect.anchoredPosition = Vector2.zero;
        healthBarRect.sizeDelta = Vector2.zero; // Use anchor-based sizing
        
        // Background
        screenHealthBarBackground = healthBarGO.AddComponent<Image>();
        screenHealthBarBackground.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        
        // Fill
        GameObject fillGO = new GameObject("HealthFill");
        fillGO.transform.SetParent(healthBarGO.transform, false);
        
        RectTransform fillRect = fillGO.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.sizeDelta = Vector2.zero;
        fillRect.anchoredPosition = Vector2.zero;
        
        screenHealthBarFill = fillGO.AddComponent<Image>();
        
        // Create a white texture for the health bar fill
        Texture2D healthFillTexture = new Texture2D(1, 1);
        healthFillTexture.SetPixel(0, 0, Color.white);
        healthFillTexture.Apply();
        screenHealthBarFill.sprite = Sprite.Create(healthFillTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f);
        
        screenHealthBarFill.color = Color.green;
        screenHealthBarFill.type = Image.Type.Filled;
        screenHealthBarFill.fillMethod = Image.FillMethod.Horizontal;
        screenHealthBarFill.fillOrigin = (int)Image.OriginHorizontal.Left; // Fill from left to right, empty from right to left
        screenHealthBarFill.fillAmount = 1f; // Start full
        
        // Health text
        GameObject healthTextGO = new GameObject("HealthText");
        healthTextGO.transform.SetParent(healthBarGO.transform, false);
        
        screenHealthText = healthTextGO.AddComponent<Text>();
        screenHealthText.text = $"{currentHealth}/{GetModifiedMaxHealth()}";
        screenHealthText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        screenHealthText.fontSize = 14;
        screenHealthText.color = Color.white;
        screenHealthText.alignment = TextAnchor.MiddleCenter;
        
        RectTransform textRect = screenHealthText.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = Vector2.zero;
        
        // Create aegis shield outline elements
        CreateAegisOutline(healthBarGO);
    }
    
    private void CreateUltimateBar()
    {
        // Check for existing ultimate bar
        if (ultimateBarFill != null)
        {
            Debug.LogWarning("Ultimate bar already exists! Skipping creation.");
            return;
        }
        
        // Ultimate bar container
        GameObject ultimateBarGO = new GameObject("UltimateBar");
        ultimateBarGO.transform.SetParent(screenUICanvas.transform, false);
        
        RectTransform ultimateBarRect = ultimateBarGO.AddComponent<RectTransform>();
        ultimateBarRect.anchorMin = new Vector2(0.35f, 0.046f); // Below health bar, centered, narrower
        ultimateBarRect.anchorMax = new Vector2(0.65f, 0.060f); // 30% width (narrower), 1.4% height (taller)
        ultimateBarRect.anchoredPosition = Vector2.zero;
        ultimateBarRect.sizeDelta = Vector2.zero; // Use anchor-based sizing
        
        // Create a simple white texture for UI sprites
        Texture2D whiteTexture = new Texture2D(1, 1);
        whiteTexture.SetPixel(0, 0, Color.white);
        whiteTexture.Apply();
        Sprite whiteSprite = Sprite.Create(whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
        
        // Background (simple background image, not using fillAmount)
        ultimateBarBackground = ultimateBarGO.AddComponent<Image>();
        ultimateBarBackground.sprite = whiteSprite;
        ultimateBarBackground.color = new Color(0.4f, 0.4f, 0.4f, 0.8f); // Grey background
        ultimateBarBackground.type = Image.Type.Simple; // Simple background, no fill behavior
        
        // Fill
        GameObject fillGO = new GameObject("UltimateFill");
        fillGO.transform.SetParent(ultimateBarGO.transform, false);
        
        RectTransform fillRect = fillGO.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.sizeDelta = Vector2.zero;
        fillRect.anchoredPosition = Vector2.zero;
        
        ultimateBarFill = fillGO.AddComponent<Image>();
        ultimateBarFill.sprite = whiteSprite; // Use the same white sprite
        ultimateBarFill.color = new Color(1f, 0.5f, 0f, 1f); // Orange color
        ultimateBarFill.type = Image.Type.Filled;
        ultimateBarFill.fillMethod = Image.FillMethod.Horizontal;
        ultimateBarFill.fillAmount = 0f; // Start empty
        
        // Debug: Verify Image component settings immediately after creation
        Debug.Log($"UltimateFill Image created: type={ultimateBarFill.type}, fillMethod={ultimateBarFill.fillMethod}, fillAmount={ultimateBarFill.fillAmount}, sprite={ultimateBarFill.sprite?.name ?? "null"}");
        
        // Ultimate text
        GameObject ultimateTextGO = new GameObject("UltimateText");
        ultimateTextGO.transform.SetParent(ultimateBarGO.transform, false);
        
        Text ultimateText = ultimateTextGO.AddComponent<Text>();
        ultimateText.text = "ULTIMATE";
        ultimateText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        ultimateText.fontSize = 12;
        ultimateText.color = Color.white;
        ultimateText.alignment = TextAnchor.MiddleCenter;
        
        RectTransform textRect = ultimateText.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = Vector2.zero;
        
        // Full ultimate effect (red outline)
        GameObject effectGO = new GameObject("UltimateFullEffect");
        effectGO.transform.SetParent(ultimateBarGO.transform, false);
        
        RectTransform effectRect = effectGO.AddComponent<RectTransform>();
        effectRect.anchorMin = new Vector2(-0.1f, -0.2f); // Slightly larger than bar
        effectRect.anchorMax = new Vector2(1.1f, 1.2f);
        effectRect.sizeDelta = Vector2.zero;
        effectRect.anchoredPosition = Vector2.zero;
        
        ultimateFullEffect = effectGO.AddComponent<Image>();
        ultimateFullEffect.color = new Color(1f, 0f, 0f, 0.8f); // Red color
        ultimateFullEffect.gameObject.SetActive(false); // Hidden by default
    }
    
    private void CreateBuffIconArray()
    {
        // Buff icon container - positioned above health bar
        GameObject buffIconGO = new GameObject("BuffIconArray");
        buffIconGO.transform.SetParent(screenUICanvas.transform, false);
        
        RectTransform buffIconRect = buffIconGO.AddComponent<RectTransform>();
        buffIconRect.anchorMin = new Vector2(0.25f, 0.12f); // Above health bar, same width
        buffIconRect.anchorMax = new Vector2(0.75f, 0.16f); // 4% height for icons
        buffIconRect.anchoredPosition = Vector2.zero;
        buffIconRect.sizeDelta = Vector2.zero;
        
        buffListPanel = buffIconRect;
        
        // Add horizontal layout group for side-by-side buff icons
        HorizontalLayoutGroup layoutGroup = buffIconGO.AddComponent<HorizontalLayoutGroup>();
        layoutGroup.spacing = 5f; // Tighter spacing for closer buff icons
        layoutGroup.childAlignment = TextAnchor.MiddleCenter;
        layoutGroup.childControlHeight = true;
        layoutGroup.childControlWidth = false;
        
        ContentSizeFitter sizeFitter = buffIconGO.AddComponent<ContentSizeFitter>();
        sizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
    }
    
    private void UpdateScreenUI()
    {
        if (!useScreenUI || screenUICanvas == null) return;
        
        // Update health bar
        if (screenHealthBarFill != null)
        {
            int modifiedMaxHealth = GetModifiedMaxHealth();
            float healthPercent = (float)currentHealth / modifiedMaxHealth;
            screenHealthBarFill.fillAmount = healthPercent;
            
            // Update color based on health - smooth color transition from green to yellow to red
            if (healthPercent > 0.6f)
            {
                // Green to yellow transition (100% to 60%)
                float t = (healthPercent - 0.6f) / 0.4f; // Normalize to 0-1
                screenHealthBarFill.color = Color.Lerp(Color.yellow, Color.green, t);
            }
            else if (healthPercent > 0.3f)
            {
                // Yellow to red transition (60% to 30%)
                float t = (healthPercent - 0.3f) / 0.3f; // Normalize to 0-1
                screenHealthBarFill.color = Color.Lerp(Color.red, Color.yellow, t);
            }
            else
            {
                // Pure red (30% to 0%)
                screenHealthBarFill.color = Color.red;
            }
        }
        
        // Update health text
        if (screenHealthText != null)
        {
            int modifiedMaxHealth = GetModifiedMaxHealth();
            screenHealthText.text = $"{currentHealth}/{modifiedMaxHealth}";
        }
        
        // Update ultimate bar
        if (ultimateBarFill != null)
        {
            float ultimatePercent = currentUltimateCharge / maxUltimateCharge;
            
            // Store the previous fillAmount for comparison
            float previousFillAmount = ultimateBarFill.fillAmount;
            
            // Set the new fillAmount
            ultimateBarFill.fillAmount = ultimatePercent;
            
            // Verify the fillAmount was actually set correctly
            float actualFillAmount = ultimateBarFill.fillAmount;
            
            // Debug: Only log when ultimate charge actually changes
            if (currentUltimateCharge != lastLoggedUltimateCharge)
            {
                ultimateBarUpdateCount++;
                // Debug.Log($"Ultimate bar charge changed #{ultimateBarUpdateCount}: {currentUltimateCharge:F1}/{maxUltimateCharge:F1} = {ultimatePercent:F3} ({ultimatePercent*100:F1}%)");
                // Debug.Log($"  Fill amounts: Previous={previousFillAmount:F3} -> Expected={ultimatePercent:F3} -> Actual={actualFillAmount:F3}");
                // Debug.Log($"  Ultimate bar properties: Type={ultimateBarFill.type}, FillMethod={ultimateBarFill.fillMethod}, Active={ultimateBarFill.gameObject.activeInHierarchy}");
                
                // Check for multiple ultimate fill objects and all ultimate-related objects
                GameObject[] allUltimateFills = FindObjectsByType<GameObject>(FindObjectsSortMode.None).Where(go => go.name == "UltimateFill").ToArray();
                GameObject[] allUltimateBars = FindObjectsByType<GameObject>(FindObjectsSortMode.None).Where(go => go.name == "UltimateBar").ToArray();
                GameObject[] allUltimateObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None).Where(go => go.name.ToLower().Contains("ultimate")).ToArray();
                
                // Debug.Log($"ULTIMATE OBJECT AUDIT: UltimateFill={allUltimateFills.Length}, UltimateBar={allUltimateBars.Length}, Any Ultimate={allUltimateObjects.Length}");
                
                if (allUltimateFills.Length > 1)
                {
                    Debug.LogWarning($"MULTIPLE ULTIMATE BARS DETECTED: Found {allUltimateFills.Length} UltimateFill objects!");
                }
                
                // Log all ultimate-related objects with detailed info
                /* for (int i = 0; i < allUltimateObjects.Length; i++)
                {
                    Image img = allUltimateObjects[i].GetComponent<Image>();
                    string fillInfo = "no Image component";
                    if (img != null)
                    {
                        string spriteInfo = img.sprite != null ? $"sprite={img.sprite.name}" : "sprite=null";
                        fillInfo = $"fillAmount={img.fillAmount:F3}, type={img.type}, fillMethod={img.fillMethod}, {spriteInfo}, color=({img.color.r:F2},{img.color.g:F2},{img.color.b:F2},{img.color.a:F2})";
                    }
                    Debug.Log($"  Ultimate Object #{i}: {allUltimateObjects[i].name} - {fillInfo}, active={allUltimateObjects[i].activeInHierarchy}, parent={allUltimateObjects[i].transform.parent?.name ?? "null"}");
                } */
                
                // Extra debug - check if the fill amount is actually being set correctly
                if (Mathf.Abs(ultimateBarFill.fillAmount - ultimatePercent) > 0.001f)
                {
                    Debug.LogWarning($"MISMATCH: Expected fill {ultimatePercent:F3} but got {ultimateBarFill.fillAmount:F3}!");
                }
                
                // Color changes based on charge: grey when empty, orange when charging
                if (ultimatePercent <= 0.01f)
                {
                    ultimateBarFill.color = new Color(0.4f, 0.4f, 0.4f, 1f); // Grey when empty
                    // Debug.Log($"  Color set to GREY: ultimatePercent={ultimatePercent:F3} <= 0.01f");
                }
                else
                {
                    ultimateBarFill.color = new Color(1f, 0.5f, 0f, 1f); // Orange when charging
                    // Debug.Log($"  Color set to ORANGE: ultimatePercent={ultimatePercent:F3} > 0.01f");
                }
                
                lastLoggedUltimateCharge = currentUltimateCharge;
            }
            
            // Additional color update outside the logging block (in case of timing issues)
            if (ultimatePercent <= 0.01f)
            {
                ultimateBarFill.color = new Color(0.4f, 0.4f, 0.4f, 1f); // Grey when empty
            }
            else
            {
                ultimateBarFill.color = new Color(1f, 0.5f, 0f, 1f); // Orange when charging
            }
            
            // Check if ultimate is full and trigger effect
            bool isUltimateFull = ultimatePercent >= 1.0f;
            if (isUltimateFull && !wasUltimateFull && !isUltimateEffectPlaying)
            {
                // Ultimate just became full - trigger effect
                StartCoroutine(PlayUltimateFullEffect());
                Debug.Log("Ultimate is full! Triggering full effect.");
            }
            wasUltimateFull = isUltimateFull;
        }
        
        // Auto-hide tooltip if it's been shown too long
        if (tooltipPanel != null && tooltipPanel.activeInHierarchy)
        {
            if (Time.time - tooltipShowTime > tooltipMaxDisplayTime)
            {
                HideBuffTooltip();
                Debug.Log("Tooltip auto-hidden after max display time");
            }
        }
    }
    
    private void UpdateBuffUI()
    {
        if (!useScreenUI || buffListPanel == null) return;
        
        // Clear existing buff UI
        foreach (GameObject buffUI in activeBuffUI)
        {
            if (buffUI != null) Destroy(buffUI);
        }
        activeBuffUI.Clear();
        
        // Add aegis shield as a "buff" if active - insert at beginning so it appears first
        List<ActiveBuff> allBuffsIncludingAegis = new List<ActiveBuff>();
        if (currentAegisShield > 0f)
        {
            // Create a virtual aegis buff for display purposes
            ActiveBuff aegisBuff = new ActiveBuff(BuffType.Aegis, (currentAegisShield/maxAegisShield) * 100f, 999f, "Aegis Shield Protection");
            allBuffsIncludingAegis.Add(aegisBuff);
        }
        allBuffsIncludingAegis.AddRange(activeBuffs);
        
        // Show individual buff instances instead of grouping by type
        foreach (ActiveBuff buff in allBuffsIncludingAegis)
        {
            CreateIndividualBuffIcon(buff);
        }
    }
    
    private void CreateIndividualBuffIcon(ActiveBuff buff)
    {
        // Create fire-animated buff icon
        GameObject buffIconGO = new GameObject($"FireBuffIcon_{buff.type}_{Time.time}");
        buffIconGO.transform.SetParent(buffListPanel, false);
        
        RectTransform iconRect = buffIconGO.AddComponent<RectTransform>();
        iconRect.sizeDelta = new Vector2(120f, 120f); // 3x larger icons (40f * 3 = 120f)
        iconRect.localScale = Vector3.one * 3.0f; // Much larger scale multiplier for big fire sprites (1.5 * 2 = 3.0)
        
        // Anchor to bottom of icon area so it scales upward from the healthbar top
        iconRect.anchorMin = new Vector2(0f, 0f);
        iconRect.anchorMax = new Vector2(0f, 0f);
        iconRect.pivot = new Vector2(0.5f, 0f); // Pivot at bottom center so it grows upward
        
        // Debug.Log($"Creating fire buff icon for {buff.type}. Fire sprites available: {fireAnimationSprites != null && fireAnimationSprites.Length > 0}");
        
        // Fire image with colored tint for buff type
        Image fireImage = buffIconGO.AddComponent<Image>();
        fireImage.raycastTarget = false; // Disable raycast - using invisible hover area instead
        fireImage.type = Image.Type.Simple;
        fireImage.preserveAspect = true;
        fireImage.color = GetFireColorForBuff(buff.type);
        
        // Set up fire animation or fallback
        if (fireAnimationSprites != null && fireAnimationSprites.Length > 0)
        {
            // Set initial sprite
            fireImage.sprite = fireAnimationSprites[0];
            
            // Add fire animation component
            FireSpriteAnimator fireAnimator = buffIconGO.AddComponent<FireSpriteAnimator>();
            fireAnimator.Initialize(fireImage, fireAnimationSprites, fireAnimationSpeed);
            
            // Debug.Log($"Fire sprite animation added for {buff.type} with {fireAnimationSprites.Length} frames");
        }
        else
        {
            // Fallback: colored box (original system)
            fireImage.color = GetBuffColor(buff.type);
            Debug.LogWarning($"No fire animation sprites - using colored box for {buff.type}");
        }
        
        // Add small duration text overlay - positioned ON TOP of the fire icon
        GameObject textGO = new GameObject("BuffDurationText");
        textGO.transform.SetParent(buffIconGO.transform, false);
        
        Text durationText = textGO.AddComponent<Text>();
        float remainingTime = buff.duration - (Time.time - buff.startTime);
        
        // Handle special display for aegis (show percentage instead of duration)
        if (buff.type == BuffType.Aegis)
        {
            durationText.text = Mathf.RoundToInt(buff.value).ToString(); // Show percentage without % sign
        }
        else
        {
            durationText.text = Mathf.Ceil(remainingTime).ToString(); // Show duration
        }
        
        durationText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        durationText.fontSize = 16; // Slightly smaller text for better proportions
        durationText.color = Color.white;
        durationText.alignment = TextAnchor.MiddleCenter; // Center the text
        durationText.fontStyle = FontStyle.Bold;
        
        // Add black outline for better visibility on fire background
        Outline textOutline = textGO.AddComponent<Outline>();
        textOutline.effectColor = Color.black;
        textOutline.effectDistance = new Vector2(1, -1);
        textOutline.useGraphicAlpha = true;
        
        // Position duration text to be centered on top of the fire icon
        RectTransform textRect = durationText.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = Vector2.zero;
        
        // Add auto-updating component for duration text
        BuffDurationTextUpdater textUpdater = textGO.AddComponent<BuffDurationTextUpdater>();
        textUpdater.Initialize(durationText, buff);
        
        // Add hover tooltip functionality with individual buff data and debug
        // Only add to the invisible hover area to prevent duplicate events
        // IndividualBuffTooltipHandler tooltipHandler = buffIconGO.AddComponent<IndividualBuffTooltipHandler>();
        // tooltipHandler.Initialize(this, buff);
        
        // Create larger invisible hover area to improve mouse detection
        GameObject hoverArea = new GameObject("HoverArea");
        hoverArea.transform.SetParent(buffIconGO.transform, false);
        
        // Add Canvas to ensure proper layering and event handling
        Canvas hoverCanvas = hoverArea.AddComponent<Canvas>();
        hoverCanvas.overrideSorting = true;
        hoverCanvas.sortingOrder = 300 + activeBuffUI.Count; // Ensure each hover area has unique sorting order
        
        Image hoverImage = hoverArea.AddComponent<Image>();
        hoverImage.color = new Color(1f, 0f, 0f, 0f); // Fully transparent (invisible) hover area
        hoverImage.raycastTarget = true; // Detects mouse events
        
        RectTransform hoverRect = hoverArea.GetComponent<RectTransform>();
        hoverRect.anchorMin = Vector2.zero;
        hoverRect.anchorMax = Vector2.one;
        hoverRect.sizeDelta = Vector2.zero;
        hoverRect.anchoredPosition = Vector2.zero;
        hoverRect.localScale = new Vector3(0.2f, 0.6f, 1f); // Your working values that fit perfectly around icons
        
        // Add the tooltip handler only to the hover area (prevents duplicate events)
        IndividualBuffTooltipHandler hoverTooltipHandler = hoverArea.AddComponent<IndividualBuffTooltipHandler>();
        hoverTooltipHandler.Initialize(this, buff);
        
        // Debug.Log($"Added tooltip handler for {buff.type} to hover area. Hover area scale: {hoverRect.localScale}");
        // Debug.Log($"Buff icon {buff.type} position: {buffIconGO.transform.position}, localPosition: {buffIconGO.transform.localPosition}");
        
        // Debug.Log($"Created fire buff icon for {buff.type} at scale {iconRect.localScale} with size {iconRect.sizeDelta}. Raycast target: {fireImage.raycastTarget}");
        
        activeBuffUI.Add(buffIconGO);
    }
    
    private Color GetFireColorForBuff(BuffType buffType)
    {
        switch (buffType)
        {
            case BuffType.Attack: return new Color(1f, 0.3f, 0.3f, 1f); // Red fire
            case BuffType.Strength: return new Color(1f, 0.6f, 0f, 1f); // Orange fire  
            case BuffType.Vitality: return new Color(0.3f, 1f, 0.3f, 1f); // Green fire
            case BuffType.Flux: return new Color(0.8f, 0.3f, 1f, 1f); // Purple fire
            case BuffType.Durability: return new Color(0.5f, 0.8f, 0.5f, 1f); // Forest Green fire
            case BuffType.Swiftness: return new Color(1f, 1f, 0.3f, 1f); // Yellow fire
            case BuffType.Aegis: return new Color(0.4f, 0.7f, 1f, 1f); // Blue fire
            default: return Color.white; // White fire
        }
    }
    
    private Color GetBuffColor(BuffType buffType)
    {
        switch (buffType)
        {
            case BuffType.Attack: return new Color(1f, 0.3f, 0.3f, 0.9f); // Red
            case BuffType.Strength: return new Color(1f, 0.5f, 0.2f, 0.9f); // Orange  
            case BuffType.Vitality: return new Color(0.2f, 1f, 0.2f, 0.9f); // Bright Green
            case BuffType.Flux: return new Color(0.5f, 0.2f, 1f, 0.9f); // Purple
            case BuffType.Durability: return new Color(0.3f, 0.8f, 0.3f, 0.9f); // Green
            case BuffType.Swiftness: return new Color(1f, 1f, 0.2f, 0.9f); // Yellow
            case BuffType.Aegis: return new Color(0.3f, 0.6f, 1f, 0.9f); // Blue
            default: return new Color(0.5f, 0.5f, 0.5f, 0.9f); // Gray
        }
    }
    
    private string GetBuffIcon(BuffType buffType)
    {
        switch (buffType)
        {
            case BuffType.Attack: return "⚔"; // Sword
            case BuffType.Strength: return "💪"; // Muscle
            case BuffType.Vitality: return "❤"; // Heart
            case BuffType.Flux: return "✨"; // Sparkles
            case BuffType.Durability: return "🛡"; // Shield
            case BuffType.Swiftness: return "⚡"; // Lightning
            case BuffType.Aegis: return "🔰"; // Shield symbol
            default: return "?";
        }
    }
    
    private string GetBuffStatText(BuffType buffType, float value)
    {
        switch (buffType)
        {
            case BuffType.Attack: return $"+{value:F1}% attack damage";
            case BuffType.Strength: return $"+{value:F1}% melee damage"; 
            case BuffType.Vitality: return $"+{value:F1}% health regen";
            case BuffType.Flux: return $"+{value:F1}% magic damage";
            case BuffType.Durability: return $"+{value:F1}% max health";
            case BuffType.Swiftness: return $"+{value:F1}% movement speed";
            case BuffType.Aegis: return $"{value:F1}% shield protection";
            default: return $"+{value:F1}% unknown effect";
        }
    }
    
    private string GetBuffDescription(BuffType buffType)
    {
        switch (buffType)
        {
            case BuffType.Attack: return "Increases attack damage";
            case BuffType.Strength: return "Increases melee attack damage"; 
            case BuffType.Vitality: return "Increases health regeneration rate";
            case BuffType.Flux: return "Increases magic damage output";
            case BuffType.Durability: return "Increases maximum health points";
            case BuffType.Swiftness: return "Increases player movement speed";
            case BuffType.Aegis: return "Provides health shield protection";
            default: return "Unknown buff effect";
        }
    }
    
    public void ShowBuffTooltip(BuffType buffType, float totalValue, float totalDuration, int stacks, Vector2 position)
    {
        string tooltipKey = buffType.ToString();
        
        // If this tooltip is already showing for this buff type, don't recreate it
        if (isTooltipVisible && currentTooltipType == tooltipKey)
        {
            return;
        }
        
        if (tooltipPanel == null) 
        {
            CreateTooltipPanel();
        }
        
        string title = buffType.ToString();
        string statBonus = GetBuffStatText(buffType, totalValue);
        string duration = $"{Mathf.CeilToInt(totalDuration)}s";
        string description = GetBuffDescription(buffType);
        
        string stackText = stacks > 1 ? $" (x{stacks} stacks)" : "";
        
        tooltipText.text = $"<b>{title}</b>{stackText}\n" +
                          $"{statBonus}\n" +
                          $"Duration: {duration}\n" +
                          $"{description}";
        
        tooltipPanel.SetActive(true);
        isTooltipVisible = true;
        currentTooltipType = tooltipKey;
        tooltipShowTime = Time.time; // Track when tooltip was shown
        
        // Position tooltip higher up than before
        RectTransform tooltipRect = tooltipPanel.GetComponent<RectTransform>();
        
        // Convert screen position to canvas normalized coordinates
        Vector2 screenSize = new Vector2(Screen.width, Screen.height);
        Vector2 normalizedPosition = position / screenSize;
        
        // Position tooltip higher above the buff area
        float tooltipX = Mathf.Clamp(normalizedPosition.x, 0.1f, 0.9f); // Keep within screen bounds
        float tooltipY = 0.22f; // Much higher than buff icons area (they're around 0.12f-0.16f)
        
        tooltipRect.anchorMin = new Vector2(tooltipX, tooltipY);
        tooltipRect.anchorMax = new Vector2(tooltipX, tooltipY);
        tooltipRect.anchoredPosition = Vector2.zero;
    }
    
    public void ShowIndividualBuffTooltip(ActiveBuff buff, Vector2 position)
    {
        Debug.Log($"ShowIndividualBuffTooltip called for {buff.type} at position {position}");
        
        string tooltipKey = $"{buff.type}_{buff.startTime}";
        
        // If this tooltip is already showing for this specific buff instance, update it
        if (isTooltipVisible && currentTooltipType == tooltipKey)
        {
            Debug.Log($"Updating existing tooltip for {buff.type}");
            UpdateTooltipContent(buff);
            return;
        }
        
        if (tooltipPanel == null) 
        {
            Debug.Log($"Creating new tooltip panel for {buff.type}");
            CreateTooltipPanel();
        }
        else
        {
            Debug.Log($"Using existing tooltip panel for {buff.type}");
        }
        
        UpdateTooltipContent(buff);
        
        Debug.Log($"Setting tooltip panel active for {buff.type}. Panel exists: {tooltipPanel != null}");
        tooltipPanel.SetActive(true);
        
        // Check if panel is actually active and visible
        Debug.Log($"Panel state after SetActive(true): active={tooltipPanel.activeSelf}, activeInHierarchy={tooltipPanel.activeInHierarchy}");
        
        // Check canvas and sorting
        Canvas panelCanvas = tooltipPanel.GetComponent<Canvas>();
        if (panelCanvas != null)
        {
            Debug.Log($"Panel has Canvas component: enabled={panelCanvas.enabled}, sortingOrder={panelCanvas.sortingOrder}, renderMode={panelCanvas.renderMode}");
        }
        else
        {
            Debug.Log("Panel does not have Canvas component - checking parent canvas");
            Canvas parentCanvas = tooltipPanel.GetComponentInParent<Canvas>();
            if (parentCanvas != null)
            {
                Debug.Log($"Parent canvas found: {parentCanvas.name}, enabled={parentCanvas.enabled}, sortingOrder={parentCanvas.sortingOrder}");
                
                // Ensure tooltip renders on top, especially when there's only one buff
                if (activeBuffs.Count == 1)
                {
                    parentCanvas.sortingOrder = 200; // Higher than normal UI
                    Debug.Log($"Single buff detected - increased canvas sorting order to 200");
                }
            }
        }
        
        isTooltipVisible = true;
        currentTooltipType = tooltipKey;
        tooltipShowTime = Time.time;
        
        Debug.Log($"Tooltip visibility set: isTooltipVisible={isTooltipVisible}, currentTooltipType={currentTooltipType}");
        
        // Position tooltip higher up than before
        RectTransform tooltipRect = tooltipPanel.GetComponent<RectTransform>();
        
        // Convert screen position to canvas normalized coordinates
        Vector2 screenSize = new Vector2(Screen.width, Screen.height);
        Vector2 normalizedPosition = position / screenSize;
        
        // Position tooltip higher above the buff area
        float tooltipX = Mathf.Clamp(normalizedPosition.x, 0.1f, 0.9f);
        float tooltipY = activeBuffs.Count == 1 ? 0.4f : 0.22f; // Much higher for single buffs
        
        Debug.Log($"Positioning tooltip for {buff.type}: screen={screenSize}, mousePos={position}, normalizedPos={normalizedPosition}, finalPos=({tooltipX}, {tooltipY}), buffCount={activeBuffs.Count}");
        
        tooltipRect.anchorMin = new Vector2(tooltipX, tooltipY);
        tooltipRect.anchorMax = new Vector2(tooltipX, tooltipY);
        tooltipRect.anchoredPosition = Vector2.zero;
        
        // Check final tooltip state
        Debug.Log($"Tooltip final state: position=({tooltipRect.anchoredPosition.x}, {tooltipRect.anchoredPosition.y}), size=({tooltipRect.sizeDelta.x}, {tooltipRect.sizeDelta.y}), scale=({tooltipRect.localScale.x}, {tooltipRect.localScale.y}, {tooltipRect.localScale.z})");
        Debug.Log($"Tooltip anchors: min=({tooltipRect.anchorMin.x}, {tooltipRect.anchorMin.y}), max=({tooltipRect.anchorMax.x}, {tooltipRect.anchorMax.y})");
        Debug.Log($"Canvas sorting and transform: parent={tooltipRect.parent?.name}, activeInHierarchy={tooltipPanel.activeInHierarchy}");
        
        // Check if tooltip text component exists and has content
        Text tooltipTextComponent = tooltipText;
        if (tooltipTextComponent != null)
        {
            Debug.Log($"Tooltip text component found: enabled={tooltipTextComponent.enabled}, text length={tooltipTextComponent.text.Length}, color={tooltipTextComponent.color}, fontSize={tooltipTextComponent.fontSize}");
        }
        else
        {
            Debug.Log("No tooltip text component found!");
        }
        
        Debug.Log($"Tooltip positioned. Starting countdown coroutine for {buff.type}");
        
        // Start updating the tooltip content every frame for live countdown
        StartCoroutine(UpdateTooltipCountdown(buff));
    }
    
    private void UpdateTooltipContent(ActiveBuff buff)
    {
        Debug.Log($"UpdateTooltipContent called for {buff.type}. TooltipText exists: {tooltipText != null}");
        
        if (tooltipText == null) return;
        
        string title = buff.type.ToString();
        string statBonus = buff.type == BuffType.Aegis ? 
            $"{buff.value:F1}% shield ({currentAegisShield:F0}/{maxAegisShield:F0})" : 
            GetBuffStatText(buff.type, buff.value);
        
        string duration = buff.type == BuffType.Aegis ? 
            "Permanent" : 
            $"{Mathf.CeilToInt(buff.TimeRemaining)}s";
        
        string description = GetBuffDescription(buff.type);
        
        string tooltipContent = $"<b>{title}</b>\n" +
                          $"{statBonus}\n" +
                          $"Duration: {duration}\n" +
                          $"{description}";
        
        Debug.Log($"Setting tooltip text for {buff.type}: '{tooltipContent}'");
        tooltipText.text = tooltipContent;
    }
    
    private System.Collections.IEnumerator UpdateTooltipCountdown(ActiveBuff buff)
    {
        while (isTooltipVisible && currentTooltipType.StartsWith(buff.type.ToString()))
        {
            UpdateTooltipContent(buff);
            yield return new WaitForSeconds(0.1f); // Update every 0.1 seconds
        }
    }
    
    public void HideBuffTooltip()
    {
        if (tooltipPanel != null && isTooltipVisible)
        {
            tooltipPanel.SetActive(false);
            isTooltipVisible = false;
            currentTooltipType = "";
        }
    }
    
    private void CreateTooltipPanel()
    {
        // Create tooltip panel
        GameObject tooltipGO = new GameObject("BuffTooltipPanel");
        tooltipGO.transform.SetParent(screenUICanvas.transform, false);
        
        RectTransform tooltipRect = tooltipGO.AddComponent<RectTransform>();
        tooltipRect.sizeDelta = new Vector2(480f, 170f); // 2x larger tooltip (240f * 2 = 480f, 85f * 2 = 170f)
        tooltipRect.anchorMin = new Vector2(0, 0);
        tooltipRect.anchorMax = new Vector2(0, 0);
        tooltipRect.pivot = new Vector2(0.5f, 0.5f); // Center pivot for easier positioning
        
        // Create white sprite for tooltip background
        Texture2D whiteTexture = new Texture2D(1, 1);
        whiteTexture.SetPixel(0, 0, Color.white);
        whiteTexture.Apply();
        Sprite whiteSprite = Sprite.Create(whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
        
        // Background - make it more visible for testing
        Image tooltipBG = tooltipGO.AddComponent<Image>();
        tooltipBG.sprite = whiteSprite;
        tooltipBG.color = new Color(0.1f, 0.1f, 0.1f, 0.95f); // Dark background with slight transparency
        tooltipBG.raycastTarget = false; // Make tooltip non-interactive
        
        // Add Canvas Group to ensure tooltip doesn't block raycasts
        CanvasGroup tooltipCanvasGroup = tooltipGO.AddComponent<CanvasGroup>();
        tooltipCanvasGroup.blocksRaycasts = false; // Critical: don't block mouse events
        
        // Text
        GameObject textGO = new GameObject("TooltipText");
        textGO.transform.SetParent(tooltipGO.transform, false);
        
        tooltipText = textGO.AddComponent<Text>();
        tooltipText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        tooltipText.fontSize = 28; // 2x larger text (14 * 2 = 28)
        tooltipText.color = Color.white;
        tooltipText.alignment = TextAnchor.UpperLeft;
        
        RectTransform textRect = tooltipText.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.05f, 0.05f);
        textRect.anchorMax = new Vector2(0.95f, 0.95f);
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = Vector2.zero;
        
        tooltipPanel = tooltipGO;
        tooltipPanel.SetActive(false);
        
        Debug.Log($"Tooltip panel created: {tooltipPanel.name}, canvas: {screenUICanvas.name}");
    }
    
    private System.Collections.IEnumerator PlayUltimateFullEffect()
    {
        if (ultimateFullEffect == null || isUltimateEffectPlaying) yield break;
        
        // Set the animation flag to prevent overlapping animations
        isUltimateEffectPlaying = true;
        
        // Show the red outline effect
        ultimateFullEffect.gameObject.SetActive(true);
        
        float expandTime = 0.3f; // Time to expand
        float contractTime = 0.3f; // Time to contract
        
        // Expand phase
        float elapsedTime = 0f;
        Vector3 startScale = Vector3.one;
        Vector3 maxScale = Vector3.one * 1.1f; // 10% larger (smaller expansion)
        
        while (elapsedTime < expandTime)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / expandTime;
            ultimateFullEffect.transform.localScale = Vector3.Lerp(startScale, maxScale, t);
            yield return null;
        }
        
        // Contract phase
        elapsedTime = 0f;
        while (elapsedTime < contractTime)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / contractTime;
            ultimateFullEffect.transform.localScale = Vector3.Lerp(maxScale, startScale, t);
            yield return null;
        }
        
        // Hide the effect
        ultimateFullEffect.transform.localScale = Vector3.one;
        ultimateFullEffect.gameObject.SetActive(false);
        
        // Clear the animation flag
        isUltimateEffectPlaying = false;
    }
    
    // Method to handle respawning the player after death
    public void RespawnPlayer()
    {
        if (!IsServer) return;
        
        // Reset health and death status
        networkHealth.Value = maxHealth;
        networkIsPlayerDead.Value = false;
        
        // Re-enable player control
        enabled = true;
        
        // Reset animation state
        if (playerAnimator != null)
        {
            playerAnimator.SetBool("IsDead", false);
        }
        
        // Reset any ongoing effects
        if (isBurning)
        {
            StopCoroutine("BurningEffect");
            isBurning = false;
        }
        
        // Clear any active buffs
        ClearAllBuffs();
        
        Debug.Log($"Player respawned with full health at position {transform.position}");
    }
    
    // Helper method to clear all active buffs
    private void ClearAllBuffs()
    {
        if (IsServer)
        {
            activeBuffs.Clear();
        }
    }
}

// Separate component for handling buff icon hover events
public class BuffTooltipHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private PlayerMovement playerMovement;
    private PlayerMovement.BuffType buffType;
    private float totalValue;
    private float totalDuration;
    private int stacks;
    
    public void Initialize(PlayerMovement player, PlayerMovement.BuffType type, float value, float duration, int stackCount)
    {
        playerMovement = player;
        buffType = type;
        totalValue = value;
        totalDuration = duration;
        stacks = stackCount;
    }
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (playerMovement != null)
        {
            playerMovement.ShowBuffTooltip(buffType, totalValue, totalDuration, stacks, eventData.position);
        }
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        if (playerMovement != null)
        {
            playerMovement.HideBuffTooltip();
        }
    }
}

// Individual buff tooltip handler for new buff display system
public class IndividualBuffTooltipHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private PlayerMovement playerMovement;
    private PlayerMovement.ActiveBuff buff;
    
    public void Initialize(PlayerMovement player, PlayerMovement.ActiveBuff buffInstance)
    {
        playerMovement = player;
        buff = buffInstance;
        // Debug.Log($"IndividualBuffTooltipHandler initialized for {buff.type} with player: {player != null}");
    }
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        Debug.Log($"IndividualBuffTooltipHandler: Mouse entered buff icon for {buff.type} at position {eventData.position}");
        if (playerMovement != null)
        {
            playerMovement.ShowIndividualBuffTooltip(buff, eventData.position);
        }
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        Debug.Log($"IndividualBuffTooltipHandler: Mouse exited buff icon for {buff.type}");
        if (playerMovement != null)
        {
            playerMovement.HideBuffTooltip();
        }
    }
}

/// <summary>
/// Component that automatically animates UI Image sprites by cycling through frames
/// </summary>
public class FireSpriteAnimator : MonoBehaviour
{
    private Image targetImage;
    private Sprite[] animationFrames;
    private float frameTime;
    private int currentFrame = 0;
    private float lastFrameTime;
    
    public void Initialize(Image image, Sprite[] sprites, float animationSpeed)
    {
        targetImage = image;
        animationFrames = sprites;
        frameTime = animationSpeed;
        lastFrameTime = Time.time;
        
        // Start with random frame for visual variety
        currentFrame = UnityEngine.Random.Range(0, animationFrames.Length);
        UpdateSprite();
    }
    
    private void Update()
    {
        if (targetImage == null || animationFrames == null || animationFrames.Length == 0)
            return;
            
        // Check if it's time to advance to next frame
        if (Time.time - lastFrameTime >= frameTime)
        {
            currentFrame = (currentFrame + 1) % animationFrames.Length;
            UpdateSprite();
            lastFrameTime = Time.time;
        }
    }
    
    private void UpdateSprite()
    {
        if (targetImage != null && animationFrames != null && currentFrame < animationFrames.Length)
        {
            targetImage.sprite = animationFrames[currentFrame];
        }
    }
}

/// <summary>
/// Component that automatically updates buff duration text display
/// </summary>
public class BuffDurationTextUpdater : MonoBehaviour
{
    private Text durationText;
    private PlayerMovement.ActiveBuff buff;
    
    public void Initialize(Text text, PlayerMovement.ActiveBuff buffInstance)
    {
        durationText = text;
        buff = buffInstance;
    }
    
    void Update()
    {
        if (durationText == null || buff == null) return;
        
        // Handle special display for aegis (show percentage instead of duration)
        if (buff.type == PlayerMovement.BuffType.Aegis)
        {
            durationText.text = Mathf.RoundToInt(buff.value).ToString(); // Show percentage without % sign
        }
        else
        {
            float remainingTime = buff.duration - (Time.time - buff.startTime);
            if (remainingTime <= 0)
            {
                durationText.text = "0";
            }
            else
            {
                durationText.text = Mathf.Ceil(remainingTime).ToString();
            }
        }
    }
}