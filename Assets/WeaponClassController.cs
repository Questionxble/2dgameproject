using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using System.Collections;
using System.Collections.Generic;

public class WeaponClassController : MonoBehaviour
{
    [Header("GUI Settings")]
    [SerializeField] private Vector2 guiPosition = new Vector2(-50, 100); // Further right from bottom right corner
    [SerializeField] private Vector2 slotSize = new Vector2(80, 80);
    [SerializeField] private float slotSpacing = 10f;
    
    [Header("Interaction Settings")]
    [SerializeField] private float interactionRange = 2f;
    [SerializeField] private Vector3 promptOffset = new Vector3(0, 1.2f, 0); // Lower above shard
    
    [Header("Player Animation Controllers")]
    [SerializeField] private RuntimeAnimatorController defaultPlayerAnimController = null; // Default animation when no shards equipped
    [SerializeField] private RuntimeAnimatorController valorShardPlayerAnimController = null; // Animation when Valor Shard equipped
    [SerializeField] private RuntimeAnimatorController whisperShardPlayerAnimController = null; // Animation when Whisper Shard equipped  
    [SerializeField] private RuntimeAnimatorController stormShardPlayerAnimController = null; // Animation when Storm Shard equipped
    
    // ========== VALOR SHARD CONFIGURATION ==========
    [Header("Valor Shard - Melee Attack Settings")]
    [SerializeField] private float swordRange = 0.8f; // Closer to player
    [SerializeField] private float swordWidth = 1.2f; // Wider to match player
    [SerializeField] private float swordHeight = 2.0f; // Taller to ensure coverage
    [SerializeField] private float swordDuration = 0.25f; // Halved from 0.5f
    [SerializeField] private int swordDamage = 25;
    [SerializeField] private float swordCooldown = 0.5f; // Cooldown between sword attacks
    [SerializeField] private float waveCooldown = 1.0f; // Cooldown between wave attacks
    [SerializeField] private float swordAnimationDelay = 0.1f; // Delay before sword damage is applied after animation starts
    
    [Header("Valor Shard - Wave Attack Settings")]
    [SerializeField] private float waveBlockSize = 1f; // Size of each wave block
    [SerializeField] private float waveBlockSpacing = 1.2f; // Spacing between blocks (no overlap)
    [SerializeField] private float waveBounceHeight = 2f; // How high blocks bounce
    [SerializeField] private int waveDamage = 30; // Damage per wave block
    [SerializeField] private GameObject valorWavePrefab = null; // Prefab with animation controller for valor wave damage blocks
    [SerializeField] private float waveBlockCleanupDelay = 0.3f; // Delay before wave blocks are destroyed after animation
    
    [Header("Valor Shard - Special Attack Settings")]
    [SerializeField] private float dashForce = 6f; // Forward propulsion force (reduced)
    [SerializeField] private float thrustDashForce = 8f; // Dash force for sword thrust attack
    [SerializeField] private int thrustDamage = 50; // Damage dealt during sword thrust
    [SerializeField] private float thrustDamageWidth = 2f; // Width of thrust damage zone
    [SerializeField] private float thrustDamageHeight = 2f; // Height of thrust damage zone
    [SerializeField] private float thrustPierceDuration = 1.5f; // How long enemies are anchored to thrust
    [SerializeField] private float dashDelayBeforeAttack = 0.1f; // Delay before dash attack animation starts
    [SerializeField] private float dashMovementDisableDuration = 0.2f; // How long movement is disabled during dash
    
    [Header("Valor Shard - Passive Buff System")]
    [SerializeField] private float doubleClickAegisPercent = 5f; // 5% aegis shield
    [SerializeField] private float doubleClickDurabilityAmount = 5f; // 5 durability points
    [SerializeField] private float doubleClickBuffDuration = 10f; // 10 seconds
    [SerializeField] private float tripleClickAttackPercent = 15f; // 15% attack buff
    [SerializeField] private float tripleClickBuffDuration = 5f; // 5 seconds
    [SerializeField] private float killAttackPercent = 10f; // 10% attack buff per kill
    [SerializeField] private float killBuffDuration = 10f; // 10 seconds per stack
    [SerializeField] private int maxKillBuffStacks = 5; // Maximum stackable kill buffs
    [SerializeField] private float waveChargeAegisPercent = 5f; // 5% aegis shield
    [SerializeField] private int minWaveChargesForBuff = 3; // 3+ charges required
    [SerializeField] private float waveChargeBuffDuration = 15f; // 15 seconds for wave buffs
    [SerializeField] private float valorAegisCapPercent = 33f; // 33% of max health cap for attack-generated aegis
    
    [Header("Valor Shard - Ultimate Settings")]
    [SerializeField] private GameObject attackDummyPrefab = null; // Prefab for summoned attack dummies
    [SerializeField] private int dummiesPerUltimate = 3; // Number of dummies summoned per ultimate use
    [SerializeField] private int maxActiveDummies = 5; // Maximum dummies active at once
    [SerializeField] private float dummyLifespan = 120f; // 2 minutes in seconds
    [SerializeField] private float summonRadius = 3f; // Radius around player to summon dummies
    
    // ========== WHISPER SHARD CONFIGURATION ==========
    [Header("Whisper Shard - Melee Attack Settings")]
    [SerializeField] private float daggerRange = 0.6f; // Closer than sword
    [SerializeField] private float daggerWidth = 0.8f; // Smaller than sword
    [SerializeField] private float daggerHeight = 1.5f; // Smaller than sword
    [SerializeField] private float daggerDuration = 0.2f; // Quick attack
    [SerializeField] private int daggerDamage = 20;
    [SerializeField] private float daggerCooldown = 0.3f; // Cooldown between dagger melee attacks
    [SerializeField] private float daggerAnimationDelay = 0.1f; // Delay before dagger damage is applied after animation starts
    
    [Header("Whisper Shard - Projectile Attack Settings")]
    [SerializeField] private float projectileSpeed = 15f; // Increased speed for longer horizontal flight
    [SerializeField] private int projectileDamage = 15;
    [SerializeField] private float projectileLifetime = 3f; // How long projectile exists
    [SerializeField] private float projectileCooldown = 0.8f; // Cooldown between dagger throws
    [SerializeField] private float recallSpeed = 20f; // Speed for dagger recall
    [SerializeField] private float enemyDetectionRange = 13f; // Range to detect enemies for redirects
    [SerializeField] private int maxDaggerRedirects = 5; // Maximum redirects before dagger expires
    [SerializeField] private float redirectCooldownExtension = 0.5f; // Additional cooldown per redirect
    [SerializeField] private float projectileWidth = 0.4f; // Projectile collider width
    [SerializeField] private float projectileHeight = 0.6f; // Projectile collider height
    [SerializeField] private Sprite daggerSprite = null; // Sprite for dagger projectile (faces flight direction)
    [SerializeField] private float projectileThrowAnimationDelay = 0.3f; // Delay after throw animation before projectile spawns
    [SerializeField] private float projectileRedirectDelay = 0.1f; // Delay between projectile redirects
    [SerializeField] private float projectileCleanupDelay = 0.5f; // Delay before cleaning up redirected projectiles
    
    [Header("Whisper Shard - Multi-Click System")]
    [SerializeField] private int multiClickThreshold = 3; // Number of clicks needed for triple dagger (reduced for easier activation)
    [SerializeField] private float multiClickTimeWindow = 5f; // Time window for resetting click sequence (increased for more forgiving timing)  
    [SerializeField] private float tripleDaggerSpread = 15f; // Angle spread between daggers in degrees
    [SerializeField] private float tripleDaggerDelay = 0.05f; // Delay between each dagger in triple throw
    
    [Header("Whisper Shard - Passive System")]
    [SerializeField] private float whisperAttackBuffPercent = 3f; // 3% attack buff per enemy hit
    [SerializeField] private float whisperAttackBuffDuration = 8f; // 8 seconds per buff
    [SerializeField] private int maxWhisperAttackStacks = 15; // Maximum attack buff stacks
    
    // ========== STORM SHARD CONFIGURATION ==========
    [Header("Storm Shard - Lightning Arc Settings")]
    [SerializeField] private float staffRange = 1f; // Particle emission point distance
    [SerializeField] private float lightningRange = 10f; // Max range for targeted lightning
    [SerializeField] private int lightningDamage = 30;
    [SerializeField] private float lightningCooldown = 0.6f; // Cooldown between lightning arcs
    [SerializeField] private float lightningDuration = 0.3f; // How long lightning arc lasts
    [SerializeField] private Material lightningArcMaterial = null; // Custom glowing material for lightning arcs and bolt strikes
    [SerializeField] private float lightningAnimationDelay = 0.1f; // Delay before lightning arc is created after animation starts
    [SerializeField] private GameObject lightningSparkPrefab = null; // Particle effect for lightning sparks at SSParticle points
    [SerializeField] private GameObject lightningHitPrefab = null; // Particle effect for lightning hits on enemies
    
    [Header("Storm Shard - Lightning Bolt Settings")]
    [SerializeField] private int boltDamage = 40; // Sky bolt damage
    [SerializeField] private float boltDuration = 0.5f; // How long lightning bolt impact lasts
    [SerializeField] private float boltCooldown = 1.2f; // Cooldown between lightning bolts
    [SerializeField] private float boltHeight = 100f; // Height above player for sky bolt
    [SerializeField] private float boltRange = 15f; // Range to find nearest enemy for sky bolt
    [SerializeField] private GameObject lightningBlastPrefab = null; // Prefab with animation controller for lightning blast
    [SerializeField] private Sprite lightningBlastSprite = null; // Fallback sprite if no blast prefab assigned
    [SerializeField] private float boltAnimationDelay = 0.1f; // Delay before lightning bolt is created after animation starts
    
    [Header("Storm Shard - Chain Lightning Settings")]
    [SerializeField] private float chainRange = 8f; // Range to find nearby enemies for chaining
    [SerializeField] private int maxChainArcs = 2; // Maximum number of chain arcs per attack (1-2)
    [SerializeField] private float chainDamageMultiplier = 0.7f; // Damage multiplier for chain arcs (70% of original)
    [SerializeField] private float chainDelay = 0.1f; // Delay between each chain arc
    [SerializeField] private float chainArcDuration = 0.25f; // How long chain arcs last
    
    [Header("Storm Shard - Passive System")]
    [SerializeField] private float stormMovementSwiftnessPercent = 5f; // 5% swiftness per stack
    [SerializeField] private float stormMovementBuffDuration = 2f; // Duration per stack when moving
    [SerializeField] private int maxStormMovementStacks = 10; // Maximum swiftness stacks
    [SerializeField] private float stormMovementCheckInterval = 0.1f; // How often to check movement (10 times per second)
    
    [Header("Ultimate Charge Settings")]
    [SerializeField] private float valorLeftClickCharge = 5f; // Charge generated per valor left click attack
    [SerializeField] private float valorTripleClickCharge = 15f; // Charge generated per valor triple click combo
    [SerializeField] private float valorRightClickCharge = 10f; // Charge generated per valor wave attack
    [SerializeField] private float whisperLeftClickCharge = 3f; // Charge generated per whisper melee attack
    [SerializeField] private float whisperRightClickCharge = 8f; // Charge generated per whisper dagger throw
    [SerializeField] private float stormConstantLeftClickCharge = 2f; // Charge generated per storm arc (auto-fire)
    [SerializeField] private float stormLeftClickCharge = 4f; // Charge generated per storm arc (manual)
    [SerializeField] private float stormRightClickCharge = 12f; // Charge generated per storm bolt attack
    [SerializeField] private float maxUltimateCharge = 100f; // Maximum ultimate charge required to fill bar completely
    private PlayerMovement playerMovement; // Reference to player movement for charge updates
    
    // Weapon System
    private enum ShardType { None, ValorShard, WhisperShard, StormShard }
    private ShardType[] equippedShards = new ShardType[2]; // Two slots
    private int activeSlotIndex = 0;
    private bool isWeaponMenuOpen = false;
    
    // ValorShard Charging System
    private bool isChargingValorAttack = false;
    private float chargeStartTime = 0f;
    private float currentChargeTime = 0f;
    
    // ValorShard Multi-Click System
    private int clickCount = 0;
    private float firstClickTime = 0f;
    private float multiClickWindow = 1.5f; // Extended window for easier multi-click detection (increased for thrust attack)
    private bool isPerformingSpecialAttack = false;
    private bool isPerformingBasicAttack = false; // Track if basic sword attack is in progress
    private Queue<int> attackQueue = new Queue<int>(); // Queue for sequential attacks (1=basic, 2=dash, 3=thrust)
    private Coroutine attackQueueProcessor = null; // Track the queue processing coroutine
    
    // ========== PRIVATE TRACKING VARIABLES ==========
    // Whisper Shard Tracking
    private GameObject currentThrownDagger = null;
    private int currentRedirectCount = 0;
    private bool daggerExpired = false;
    private int currentClickCount = 0;
    private Coroutine resetClickCoroutine = null; // Track reset coroutine to prevent overlaps
    private List<GameObject> activeDaggers = new List<GameObject>();
    
    // Storm Shard Tracking
    private int stormClickCount = 0; // Alternates between 1 and 2 for attack types
    
    // Valor Shard Tracking
    private bool isPerformingThrust = false;
    private float thrustStartTime = 0f;
    private float thrustDuration = 1f; // Duration of thrust animation
    private GameObject thrustDamageZone = null;
    private List<GameObject> piercedEnemies = new List<GameObject>(); // Track enemies anchored to thrust
    
    // Ultimate System Tracking
    private List<GameObject> activeDummies = new List<GameObject>(); // Track active summoned dummies
    
    // Passive System Tracking
    private float lastMovementCheckTime = 0f; // Track movement check timing
    private bool wasMovingLastCheck = false; // Track previous movement state
    private Dictionary<string, int> passiveBuffStacks = new Dictionary<string, int>(); // Track passive buff stacks
    
    // Cooldown System
    private float lastSwordAttackTime = 0f;
    private float lastWaveAttackTime = 0f;
    private float lastDaggerAttackTime = 0f;
    private float lastProjectileAttackTime = 0f;
    private float lastLightningArcTime = 0f;
    private float lastLightningBoltTime = 0f;
    
    // Auto-fire System for Storm Shard
    private bool isAutoFiring = false;
    private float nextAutoFireTime = 0f;
    private int autoFireAttackType = 0; // Cycles between 0 and 1
    private const float attackType1Interval = 0.583f; // 0.583 seconds for attack type 1
    private const float attackType2Interval = 0.75f; // 0.75 seconds for attack type 2
    
    // Shard Swap System
    private bool isInSwapMode = false;
    private int swapTargetSlot = 0; // Which slot will be replaced during swap
    
    // GUI Components
    private Canvas weaponCanvas; // Automatically created
    private Image[] slotImages = new Image[2];
    private Image[] slotBackgrounds = new Image[2];
    private Image activeSlotIndicator;
    private Image swapTargetIndicator;
    
    // Interaction System
    private GameObject nearbyShardObject;
    private ShardType nearbyShardType;
    private Canvas promptCanvas;
    private Text promptText;
    
    // Player References (this script should be attached to the player)
    private Transform playerTransform;
    
    // Storm Shard Components
    private GameObject stormParticlePoint1; // Invisible emission point for storm attack type 1
    private GameObject stormParticlePoint2; // Invisible emission point for storm attack type 2
    
    // Shard Sprites (will be loaded from the GameObjects)
    private Dictionary<ShardType, Sprite> shardSprites = new Dictionary<ShardType, Sprite>();
    
    // Public Properties
    public bool IsChargingValorAttack => isChargingValorAttack;
    public bool IsWhisperShardActive => equippedShards[activeSlotIndex] == ShardType.WhisperShard;
    
    void Start()
    {
        // Get player components (this script is attached to the player)
        playerMovement = GetComponent<PlayerMovement>();
        playerTransform = transform;
        
        if (playerMovement == null)
        {
            Debug.LogError("WeaponClassController must be attached to the same GameObject as PlayerMovement!");
            enabled = false;
            return;
        }
        
        // Initialize weapon system
        attackQueue = new Queue<int>(); // Initialize attack queue
        InitializeGUI();
        LoadShardSprites();
        FindStormParticlePoint();
        
        // Initialize animation controller based on current equipped shards
        Debug.Log("WeaponClassController: Initializing animation controller...");
        UpdatePlayerAnimationController();
        
        // Sync ultimate charge configuration with PlayerMovement
        SyncUltimateChargeSettings();
    }
    
    void Update()
    {
        CheckForNearbyShards();
        HandleInput();
        UpdatePromptPosition();
        
        // Continuously update facing direction when using WhisperShard
        if (IsWhisperShardActive)
        {
            UpdatePlayerFacingForMouse();
        }
        
        // Handle passive abilities
        UpdatePassiveAbilities();
    }
    
    private void InitializeGUI()
    {
        // Create weapon GUI canvas
        GameObject canvasGO = new GameObject("WeaponGUI");
        weaponCanvas = canvasGO.AddComponent<Canvas>();
        weaponCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        weaponCanvas.sortingOrder = 100;
        
        // Add CanvasScaler for responsive design
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        
        // Add GraphicRaycaster for UI interactions
        canvasGO.AddComponent<GraphicRaycaster>();
        
        // Create slot container
        GameObject slotContainer = new GameObject("SlotContainer");
        slotContainer.transform.SetParent(weaponCanvas.transform, false);
        
        RectTransform containerRect = slotContainer.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(1, 0); // Bottom right
        containerRect.anchorMax = new Vector2(1, 0);
        containerRect.pivot = new Vector2(1, 0);
        containerRect.anchoredPosition = guiPosition;
        containerRect.sizeDelta = new Vector2(slotSize.x * 2 + slotSpacing, slotSize.y);
        
        // Create slots
        for (int i = 0; i < 2; i++)
        {
            CreateWeaponSlot(slotContainer, i);
        }
        
        // Create active slot indicator
        CreateActiveSlotIndicator(slotContainer);
    }
    
    private void CreateWeaponSlot(GameObject parent, int slotIndex)
    {
        // Slot background
        GameObject slotBG = new GameObject($"Slot{slotIndex}_Background");
        slotBG.transform.SetParent(parent.transform, false);
        
        RectTransform bgRect = slotBG.AddComponent<RectTransform>();
        bgRect.anchoredPosition = new Vector2(-(slotIndex * (slotSize.x + slotSpacing)), 0);
        bgRect.sizeDelta = slotSize;
        
        Image bgImage = slotBG.AddComponent<Image>();
        bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f); // Dark background
        slotBackgrounds[slotIndex] = bgImage;
        
        // Slot content (shard image)
        GameObject slotContent = new GameObject($"Slot{slotIndex}_Content");
        slotContent.transform.SetParent(slotBG.transform, false);
        
        RectTransform contentRect = slotContent.AddComponent<RectTransform>();
        contentRect.anchorMin = Vector2.zero;
        contentRect.anchorMax = Vector2.one;
        contentRect.sizeDelta = Vector2.zero;
        contentRect.anchoredPosition = Vector2.zero;
        
        Image contentImage = slotContent.AddComponent<Image>();
        contentImage.color = Color.clear; // Transparent by default
        slotImages[slotIndex] = contentImage;
    }
    
    private void CreateActiveSlotIndicator(GameObject parent)
    {
        GameObject indicator = new GameObject("ActiveSlotIndicator");
        indicator.transform.SetParent(parent.transform, false);
        
        RectTransform indicatorRect = indicator.AddComponent<RectTransform>();
        indicatorRect.sizeDelta = slotSize + Vector2.one * 5f; // Slightly larger than slot
        
        Image indicatorImage = indicator.AddComponent<Image>();
        indicatorImage.color = new Color(1f, 1f, 0f, 0.8f); // Yellow outline
        indicatorImage.type = Image.Type.Sliced;
        
        // Create simple border sprite
        Texture2D borderTexture = new Texture2D(32, 32);
        Color[] pixels = new Color[32 * 32];
        for (int i = 0; i < pixels.Length; i++)
        {
            int x = i % 32;
            int y = i / 32;
            if (x < 3 || x > 28 || y < 3 || y > 28)
                pixels[i] = Color.white;
            else
                pixels[i] = Color.clear;
        }
        borderTexture.SetPixels(pixels);
        borderTexture.Apply();
        
        indicatorImage.sprite = Sprite.Create(borderTexture, new Rect(0, 0, 32, 32), Vector2.one * 0.5f, 32f, 0, SpriteMeshType.FullRect, new Vector4(3, 3, 3, 3));
        activeSlotIndicator = indicatorImage;
        
        UpdateActiveSlotIndicator();
        CreateSwapTargetIndicator(parent);
    }
    
    private void CreateSwapTargetIndicator(GameObject parent)
    {
        GameObject indicator = new GameObject("SwapTargetIndicator");
        indicator.transform.SetParent(parent.transform, false);
        
        RectTransform indicatorRect = indicator.AddComponent<RectTransform>();
        indicatorRect.sizeDelta = slotSize + Vector2.one * 8f; // Slightly larger than active slot indicator
        
        Image indicatorImage = indicator.AddComponent<Image>();
        indicatorImage.color = new Color(1f, 0.3f, 0.3f, 0.9f); // Red outline for swap target
        indicatorImage.type = Image.Type.Sliced;
        
        // Create border sprite with thicker border for distinction
        Texture2D borderTexture = new Texture2D(32, 32);
        Color[] pixels = new Color[32 * 32];
        for (int i = 0; i < pixels.Length; i++)
        {
            int x = i % 32;
            int y = i / 32;
            if (x < 4 || x > 27 || y < 4 || y > 27) // Thicker border
                pixels[i] = Color.white;
            else
                pixels[i] = Color.clear;
        }
        borderTexture.SetPixels(pixels);
        borderTexture.Apply();
        
        indicatorImage.sprite = Sprite.Create(borderTexture, new Rect(0, 0, 32, 32), Vector2.one * 0.5f, 32f, 0, SpriteMeshType.FullRect, new Vector4(4, 4, 4, 4));
        swapTargetIndicator = indicatorImage;
        
        // Initially hidden
        swapTargetIndicator.gameObject.SetActive(false);
    }
    
    private void LoadShardSprites()
    {
        // Find shard GameObjects and extract their sprites
        string[] shardTags = { "ValorShard", "WhisperShard", "StormShard" };
        ShardType[] shardTypes = { ShardType.ValorShard, ShardType.WhisperShard, ShardType.StormShard };
        
        for (int i = 0; i < shardTags.Length; i++)
        {
            GameObject[] shardObjects = GameObject.FindGameObjectsWithTag(shardTags[i]);
            if (shardObjects.Length > 0)
            {
                SpriteRenderer spriteRenderer = shardObjects[0].GetComponent<SpriteRenderer>();
                if (spriteRenderer != null && spriteRenderer.sprite != null)
                {
                    shardSprites[shardTypes[i]] = spriteRenderer.sprite;
                }
            }
        }
    }
    
    private void CheckForNearbyShards()
    {
        if (playerTransform == null) return;
        
        // Check for each shard type
        string[] shardTags = { "ValorShard", "WhisperShard", "StormShard" };
        ShardType[] shardTypes = { ShardType.ValorShard, ShardType.WhisperShard, ShardType.StormShard };
        
        GameObject closestShard = null;
        ShardType closestShardType = ShardType.None;
        float closestDistance = float.MaxValue;
        
        for (int i = 0; i < shardTags.Length; i++)
        {
            GameObject[] shards = GameObject.FindGameObjectsWithTag(shardTags[i]);
            foreach (GameObject shard in shards)
            {
                float distance = Vector3.Distance(playerTransform.position, shard.transform.position);
                if (distance <= interactionRange && distance < closestDistance)
                {
                    closestDistance = distance;
                    closestShard = shard;
                    closestShardType = shardTypes[i];
                }
            }
        }
        
        // Update nearby shard
        if (closestShard != nearbyShardObject)
        {
            nearbyShardObject = closestShard;
            nearbyShardType = closestShardType;
            UpdateInteractionPrompt();
        }
    }
    
    private void UpdateInteractionPrompt()
    {
        if (nearbyShardObject != null && !HasShardEquipped(nearbyShardType))
        {
            int emptySlot = GetEmptySlotIndex();
            if (emptySlot != -1)
            {
                // Normal equip when there's an empty slot
                ShowInteractionPrompt($"Press E to equip\n{nearbyShardType}");
            }
            else if (isInSwapMode)
            {
                // In swap mode - show which shard will be replaced
                ShardType targetShard = equippedShards[swapTargetSlot];
                ShowInteractionPrompt($"Hold E + Left/Right to choose slot\nPress E to replace {targetShard} with {nearbyShardType}");
            }
            else
            {
                // Swap when both slots are full - initial prompt
                ShowInteractionPrompt($"Hold E to swap for\n{nearbyShardType}");
            }
        }
        else
        {
            HideInteractionPrompt();
        }
    }
    
    private void ShowInteractionPrompt(string message)
    {
        if (promptCanvas == null)
        {
            CreateInteractionPrompt();
        }
        
        promptText.text = message;
        promptCanvas.gameObject.SetActive(true);
    }
    
    private void HideInteractionPrompt()
    {
        if (promptCanvas != null)
        {
            promptCanvas.gameObject.SetActive(false);
        }
    }
    
    private void CreateInteractionPrompt()
    {
        GameObject promptGO = new GameObject("InteractionPrompt");
        promptCanvas = promptGO.AddComponent<Canvas>();
        promptCanvas.renderMode = RenderMode.WorldSpace;
        promptCanvas.sortingOrder = 15; // Above health bar
        
        RectTransform canvasRect = promptCanvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(350, 80); // Even larger size
        canvasRect.localScale = Vector3.one * 0.008f; // Slightly smaller scale to fit better
        
        // Background
        GameObject bgGO = new GameObject("Background");
        bgGO.transform.SetParent(promptGO.transform, false);
        
        Image bgImage = bgGO.AddComponent<Image>();
        bgImage.color = new Color(0, 0, 0, 0.85f); // Even darker background
        
        RectTransform bgRect = bgGO.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        
        // Text
        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(promptGO.transform, false);
        
        promptText = textGO.AddComponent<Text>();
        promptText.text = "Press E to equip";
        promptText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        promptText.fontSize = 24; // Even larger font
        promptText.color = Color.white;
        promptText.alignment = TextAnchor.MiddleCenter;
        
        // Enable text wrapping for multi-line text
        promptText.horizontalOverflow = HorizontalWrapMode.Wrap;
        promptText.verticalOverflow = VerticalWrapMode.Overflow;
        
        RectTransform textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.offsetMin = new Vector2(5, 5); // Smaller padding so text fills more
        textRect.offsetMax = new Vector2(-5, -5); // Smaller padding
        
        promptCanvas.gameObject.SetActive(false);
    }
    
    private void UpdatePromptPosition()
    {
        if (promptCanvas != null && promptCanvas.gameObject.activeSelf && nearbyShardObject != null)
        {
            // Position the prompt above the shard, not the player
            Vector3 promptPosition = nearbyShardObject.transform.position + promptOffset;
            promptCanvas.transform.position = promptPosition;
            
            // Face camera
            if (Camera.main != null)
            {
                promptCanvas.transform.LookAt(Camera.main.transform);
                promptCanvas.transform.Rotate(0, 180, 0);
            }
        }
    }
    
    private void HandleInput()
    {
        if (playerMovement == null) return;
        
        // Check for shard pickup/swap using new Input System
        bool eKeyPressed = Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
        bool eKeyHeld = Keyboard.current != null && Keyboard.current.eKey.isPressed;
        bool eKeyReleased = Keyboard.current != null && Keyboard.current.eKey.wasReleasedThisFrame;
        
        if (nearbyShardObject != null && !HasShardEquipped(nearbyShardType))
        {
            int emptySlot = GetEmptySlotIndex();
            if (emptySlot != -1)
            {
                // Normal equip when there's an empty slot
                if (eKeyPressed)
                {
                    EquipShard(nearbyShardType);
                }
            }
            else
            {
                // Both slots are full - handle swap mode
                if (eKeyPressed)
                {
                    // Enter swap mode and set initial target slot to active slot
                    isInSwapMode = true;
                    swapTargetSlot = activeSlotIndex;
                    ShowSwapTargetIndicator();
                    UpdateInteractionPrompt();
                }
                else if (isInSwapMode && eKeyHeld)
                {
                    // Handle slot selection while in swap mode
                    bool leftPressed = Keyboard.current.leftArrowKey.wasPressedThisFrame || Keyboard.current.aKey.wasPressedThisFrame;
                    bool rightPressed = Keyboard.current.rightArrowKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame;
                    
                    if (leftPressed)
                    {
                        swapTargetSlot = 1; // Left slot
                        UpdateSwapTargetIndicator();
                        UpdateInteractionPrompt();
                    }
                    else if (rightPressed)
                    {
                        swapTargetSlot = 0; // Right slot
                        UpdateSwapTargetIndicator();
                        UpdateInteractionPrompt();
                    }
                }
                else if (isInSwapMode && eKeyReleased)
                {
                    // Confirm swap and exit swap mode
                    SwapShard(nearbyShardType, swapTargetSlot);
                    isInSwapMode = false;
                    HideSwapTargetIndicator();
                }
            }
        }
        
        // Exit swap mode if player moves away from shard
        if (nearbyShardObject == null && isInSwapMode)
        {
            isInSwapMode = false;
            HideSwapTargetIndicator();
        }
        
        // Check for weapon switching using new Input System
        bool qKeyHeld = Keyboard.current != null && Keyboard.current.qKey.isPressed;
        if (qKeyHeld)
        {
            // Disable player movement while in weapon menu
            isWeaponMenuOpen = true;
            
            bool leftPressed = Keyboard.current.leftArrowKey.wasPressedThisFrame || Keyboard.current.aKey.wasPressedThisFrame;
            bool rightPressed = Keyboard.current.rightArrowKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame;
            
            if (leftPressed)
            {
                SwitchToSlot(1); // Left arrow switches to left slot (slot 1)
            }
            else if (rightPressed)
            {
                SwitchToSlot(0); // Right arrow switches to right slot (slot 0)
            }
        }
        else
        {
            isWeaponMenuOpen = false;
        }
        
        // Check for attack using new Input System
        bool leftClickPressed = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        bool leftClickHeld = Mouse.current != null && Mouse.current.leftButton.isPressed;
        bool leftClickReleased = Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame;
        bool rightClickPressed = Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
        bool rightClickHeld = Mouse.current != null && Mouse.current.rightButton.isPressed;
        bool rightClickReleased = Mouse.current != null && Mouse.current.rightButton.wasReleasedThisFrame;
        
        if (!isWeaponMenuOpen)
        {
            // Handle ValorShard charging attack
            ShardType activeWeapon = equippedShards[activeSlotIndex];
            if (activeWeapon == ShardType.ValorShard && rightClickPressed)
            {
                // Start charging
                isChargingValorAttack = true;
                chargeStartTime = Time.time;
            }
            
            if (isChargingValorAttack)
            {
                currentChargeTime = Time.time - chargeStartTime;
                
                if (rightClickReleased)
                {
                    // Release charge attack
                    UseActiveWeapon(true); // Right-click attack
                    isChargingValorAttack = false;
                    currentChargeTime = 0f;
                }
            }
            else if (leftClickPressed)
            {
                HandleLeftClickInput(activeWeapon);
            }
            else if (leftClickReleased)
            {
                // Stop auto-fire when left click is released
                isAutoFiring = false;
            }
            else if (rightClickPressed && activeWeapon != ShardType.ValorShard)
            {
                UseActiveWeapon(true); // Right-click attack for other weapons
            }
            
            // Handle auto-fire for Storm Shard
            if (isAutoFiring && activeWeapon == ShardType.StormShard && leftClickHeld)
            {
                if (Time.time >= nextAutoFireTime)
                {
                    // Fire with current attack type
                    CreateElectricArc(autoFireAttackType);
                    
                    // Set next fire time based on current attack type
                    float interval = (autoFireAttackType == 0) ? attackType1Interval : attackType2Interval;
                    nextAutoFireTime = Time.time + interval;
                    
                    // Cycle to next attack type
                    autoFireAttackType = (autoFireAttackType == 0) ? 1 : 0;
                    
                    // Generate ultimate charge for storm constant left click (auto-fire)
                    GenerateUltimateCharge(stormConstantLeftClickCharge);
                }
            }
            else if (!leftClickHeld)
            {
                // Stop auto-fire if left click is no longer held
                isAutoFiring = false;
            }
            
            // Handle Ultimate Activation (R key)
            bool rKeyPressed = Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
            if (rKeyPressed && playerMovement.HasFullUltimate())
            {
                ActivateUltimate();
            }
        }
    }
    
    private void EquipShard(ShardType shardType)
    {
        int emptySlot = GetEmptySlotIndex();
        if (emptySlot == -1) return;
        
        equippedShards[emptySlot] = shardType;
        UpdateSlotDisplay(emptySlot);
        
        // Update animation controller if this is the active slot
        if (emptySlot == activeSlotIndex)
        {
            UpdatePlayerAnimationController();
        }
        
        // Remove shard from world
        if (nearbyShardObject != null)
        {
            Destroy(nearbyShardObject);
            nearbyShardObject = null;
        }
        
        HideInteractionPrompt();
    }
    
    private void SwapShard(ShardType newShardType, int slotToReplace)
    {
        if (slotToReplace < 0 || slotToReplace >= equippedShards.Length) return;
        
        ShardType oldShardType = equippedShards[slotToReplace];
        
        // Replace the shard in the specified slot
        equippedShards[slotToReplace] = newShardType;
        UpdateSlotDisplay(slotToReplace);
        
        // Update animation controller if this is the active slot
        if (slotToReplace == activeSlotIndex)
        {
            UpdatePlayerAnimationController();
        }
        
        // Transform the current shard object to become the old shard type
        if (nearbyShardObject != null)
        {
            // Change the shard's tag and sprite to represent the old shard
            ConvertShardObject(nearbyShardObject, oldShardType);
            
            // Clear nearby reference since we're no longer interacting with it
            nearbyShardObject = null;
        }
        
        HideInteractionPrompt();
    }
    
    private void ConvertShardObject(GameObject shardObject, ShardType newShardType)
    {
        if (shardObject == null) return;
        
        // Store original transform properties
        Vector3 originalPosition = shardObject.transform.position;
        Vector3 originalScale = shardObject.transform.localScale;
        Quaternion originalRotation = shardObject.transform.rotation;
        
        // Get the sprite renderer to change the visual
        SpriteRenderer spriteRenderer = shardObject.GetComponent<SpriteRenderer>();
        
        // Map shard types to their corresponding tags
        string[] shardTags = { "ValorShard", "WhisperShard", "StormShard" };
        ShardType[] shardTypes = { ShardType.ValorShard, ShardType.WhisperShard, ShardType.StormShard };
        
        for (int i = 0; i < shardTypes.Length; i++)
        {
            if (shardTypes[i] == newShardType)
            {
                // Change the tag
                shardObject.tag = shardTags[i];
                
                // Change the sprite if we have it cached
                if (spriteRenderer != null && shardSprites.ContainsKey(newShardType))
                {
                    spriteRenderer.sprite = shardSprites[newShardType];
                    
                    // Preserve original transform properties
                    shardObject.transform.position = originalPosition;
                    shardObject.transform.localScale = originalScale;
                    shardObject.transform.rotation = originalRotation;
                }
                
                return;
            }
        }
        
        Debug.LogWarning($"Could not convert shard to {newShardType} - unknown type");
    }
    
    private void SwitchToSlot(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < 2 && equippedShards[slotIndex] != ShardType.None)
        {
            activeSlotIndex = slotIndex;
            UpdateActiveSlotIndicator();
            
            // Update animation controller for new active shard
            UpdatePlayerAnimationController();
        }
    }
    
    private void UseActiveWeapon(bool isRightClick = false, bool bypassCooldown = false)
    {
        ShardType activeShard = equippedShards[activeSlotIndex];
        
        switch (activeShard)
        {
            case ShardType.ValorShard:
                UseValorShard(isRightClick, bypassCooldown);
                break;
            case ShardType.WhisperShard:
                UseWhisperShard(isRightClick);
                break;
            case ShardType.StormShard:
                UseStormShard(isRightClick);
                break;
            default:
                break;
        }
    }
    
    private void UseValorShard(bool isRightClick, bool bypassCooldown = false)
    {
        if (playerTransform == null) return;
        
        if (isRightClick)
        {
            // Right-click: Wave attack with cooldown
            float timeSinceLastWave = Time.time - lastWaveAttackTime;
            if (timeSinceLastWave < waveCooldown)
            {
                Debug.Log($"Wave attack blocked by cooldown - timeSince: {timeSinceLastWave:F3}, cooldown: {waveCooldown}");
                return;
            }
            
            CreateWaveAttack();
            lastWaveAttackTime = Time.time;
        }
        else
        {
            // Left-click: Regular sword attack with cooldown
            float timeSinceLastAttack = Time.time - lastSwordAttackTime;
            if (!bypassCooldown && timeSinceLastAttack < swordCooldown)
            {
                Debug.Log($"Sword attack blocked by cooldown - timeSince: {timeSinceLastAttack:F3}, cooldown: {swordCooldown}");
                return;
            }
            
            CreateSwordAttack();
            
            // Only update cooldown timer for regular attacks, not bypassed ones
            if (!bypassCooldown)
            {
                lastSwordAttackTime = Time.time;
            }
            
            if (bypassCooldown)
            {
                Debug.Log("Dash sword swing bypassed cooldown - timer not updated!");
            }
        }
    }
    
    private void CreateSwordAttack(int attackAnimationType = 0)
    {
        // Trigger melee attack animation
        TriggerAttackAnimation(attackAnimationType, swordDuration);
        
        // Get player's sprite renderer to check facing direction
        SpriteRenderer playerSprite = GetComponent<SpriteRenderer>();
        if (playerSprite == null)
            playerSprite = GetComponentInChildren<SpriteRenderer>();
        
        // Create sword attack damage object in front of player
        bool facingLeft = playerSprite != null ? playerSprite.flipX : false;
        Vector3 attackPosition = playerTransform.position + (Vector3.right * (facingLeft ? -swordRange : swordRange));
        
        // Generate ultimate charge for valor left click attack
        GenerateUltimateCharge(valorLeftClickCharge);
        
        // Add small delay to sync with animation start
        StartCoroutine(DelayedSwordAttack(attackPosition));
    }
    
    private System.Collections.IEnumerator DelayedSwordAttack(Vector3 attackPosition)
    {
        // Wait for animation to start (adjust this value based on your animation controller transition time)
        yield return new WaitForSeconds(swordAnimationDelay);
        
        // Now create the actual damage object
        StartCoroutine(CreateSwordAttack(attackPosition));
    }
    
    private void UseWhisperShard(bool isRightClick)
    {
        if (playerTransform == null) return;
        
        if (isRightClick)
        {
            // Check if there's an active dagger that can be redirected
            if (currentThrownDagger != null && !daggerExpired)
            {
                // Check if we haven't reached the redirect limit
                if (currentRedirectCount < maxDaggerRedirects)
                {
                    RedirectDagger();
                    return;
                }
                else
                {
                    Debug.Log("WhisperShard: Dagger has reached maximum redirects and is now expired!");
                    return;
                }
            }
            
            // Check if dagger throw is on cooldown
            if (Time.time - lastProjectileAttackTime < projectileCooldown)
            {
                Debug.Log("WhisperShard: Dagger throw on cooldown!");
                return;
            }
            
            // Right-click: Throw new projectile dagger
            ThrowDaggerProjectile();
            lastProjectileAttackTime = Time.time;
            currentRedirectCount = 0; // Reset redirect count for new dagger
            daggerExpired = false; // Reset expired flag
            
            // Generate ultimate charge for whisper right click attack
            GenerateUltimateCharge(whisperRightClickCharge);
        }
        else
        {
            // Left-click: Quick dagger strike with cooldown and multi-click tracking
            
            // Track consecutive clicks BEFORE cooldown check so clicks are always counted
            TrackMultiClick();
            
            if (Time.time - lastDaggerAttackTime < daggerCooldown) {
                Debug.Log($"WhisperShard: Left-click on cooldown, but click was tracked");
                return;
            }
            
            // Determine attack type based on current click count (0 = first click, 1 = second click)
            int attackType = Mathf.Min(currentClickCount - 1, 1);
            CreateDaggerStrike(attackType);
            lastDaggerAttackTime = Time.time;
            
            // Generate ultimate charge for whisper left click attack
            GenerateUltimateCharge(whisperLeftClickCharge);
        }
    }

    private void TrackMultiClick()
    {
        Debug.Log($"WhisperShard: Click detected! Current count: {currentClickCount}");
        
        // Stop any existing reset coroutine since we got a new click
        if (resetClickCoroutine != null)
        {
            StopCoroutine(resetClickCoroutine);
            resetClickCoroutine = null;
        }
        
        // Increment click count (always increment for sequential detection)
        currentClickCount++;
        
        Debug.Log($"WhisperShard: Multi-click count: {currentClickCount}/{multiClickThreshold}");
        
        // Check if we've reached the threshold for triple dagger attack
        if (currentClickCount >= multiClickThreshold)
        {
            Debug.Log("WhisperShard: TRIGGERING Triple Dagger Attack!");
            
            // Trigger triple dagger attack
            StartCoroutine(TripleDaggerAttack());
            
            // Reset counter
            currentClickCount = 0;
            
            Debug.Log("WhisperShard: Triple Dagger Attack activated!");
        }
        else
        {
            // Start countdown to reset counter if no more clicks come
            resetClickCoroutine = StartCoroutine(ResetClickCountAfterDelay());
        }
    }
    
    private IEnumerator ResetClickCountAfterDelay()
    {
        float resetTime = multiClickTimeWindow; // Use existing time window for reset
        yield return new WaitForSeconds(resetTime);
        
        // Only reset if we haven't reached the threshold yet
        if (currentClickCount > 0 && currentClickCount < multiClickThreshold)
        {
            Debug.Log($"WhisperShard: Click sequence timed out, resetting from {currentClickCount} to 0");
            currentClickCount = 0;
        }
        
        // Clear the coroutine reference
        resetClickCoroutine = null;
    }

    private IEnumerator TripleDaggerAttack()
    {
        Debug.Log("WhisperShard: TripleDaggerAttack coroutine started!");
        
        // Trigger ultimate attack animation (Type 2) 
        TriggerAttackAnimation(2, 1.0f);
        
        if (playerTransform == null) 
        {
            Debug.Log("WhisperShard: playerTransform is null, aborting triple dagger attack");
            yield break;
        }
        
        // Get mouse position for targeting using Input System
        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, Camera.main.nearClipPlane));
        mouseWorldPos.z = 0f; // Ensure Z is 0 for 2D
        
        Debug.Log($"WhisperShard: Mouse position: {mouseWorldPos}, Player position: {playerTransform.position}");
        
        // Calculate base direction from player to mouse
        Vector3 baseDirection = (mouseWorldPos - playerTransform.position).normalized;
        
        // Calculate starting position (in front of player)
        Vector3 startPosition = playerTransform.position + baseDirection * 0.5f;
        
        // Create three daggers with spread angles
        for (int i = 0; i < 3; i++)
        {
            // Calculate angle offset: center dagger (0°), left (-spread), right (+spread)
            float angleOffset = (i - 1) * tripleDaggerSpread; // -15°, 0°, +15° for default spread
            
            // Apply rotation to base direction
            Vector3 daggerDirection = RotateVector2D(baseDirection, angleOffset);
            
            // Create the dagger projectile
            GameObject dagger = CreateTripleDagger(startPosition, daggerDirection, i);
            
            if (dagger != null)
            {
                activeDaggers.Add(dagger);
                
                // Add slight delay between dagger spawns for visual effect
                if (i < 2) yield return new WaitForSeconds(tripleDaggerDelay);
            }
        }
        
        Debug.Log($"WhisperShard: Triple dagger attack launched! {activeDaggers.Count} active daggers");
    }

    private GameObject CreateTripleDagger(Vector3 startPosition, Vector3 direction, int index)
    {
        // Create projectile dagger similar to regular dagger but with special properties
        GameObject projectile = new GameObject($"TripleDagger_{index}");
        projectile.transform.position = startPosition;
        
        // Add rigidbody for physics
        Rigidbody2D rb = projectile.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0.3f; // Same as regular dagger
        rb.linearVelocity = direction * projectileSpeed;
        
        // Add collider
        BoxCollider2D projectileCollider = projectile.AddComponent<BoxCollider2D>();
        projectileCollider.size = new Vector2(projectileWidth, projectileHeight);
        projectileCollider.isTrigger = true;
        
        // Add damage component
        DamageObject damageComponent = projectile.AddComponent<DamageObject>();
        damageComponent.damageAmount = playerMovement.GetModifiedMeleeDamage(projectileDamage);
        damageComponent.damageRate = 0.1f;
        
        // Add Whisper Shard passive callback
        damageComponent.onEnemyHit = () => ApplyWhisperAttackPassive();
        
        // Configure damage object
        var excludeField = typeof(DamageObject).GetField("excludePlayerLayer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (excludeField != null)
        {
            excludeField.SetValue(damageComponent, true);
        }
        
        var enemyDamageField = typeof(DamageObject).GetField("canDamageEnemies", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (enemyDamageField != null)
        {
            enemyDamageField.SetValue(damageComponent, true);
        }
        
        // Exclude NPC and PlayerSummon layers to prevent damaging player summons/allies
        damageComponent.excludeLayers = LayerMask.GetMask("NPC", "PlayerSummon");
        
        // Visual indicator - white color for triple daggers
        SpriteRenderer daggerRenderer = projectile.AddComponent<SpriteRenderer>();
        Color tripleDaggerColor = new Color(1f, 1f, 1f, 1f); // White color
        
        if (daggerSprite != null)
        {
            daggerRenderer.sprite = daggerSprite;
            daggerRenderer.color = tripleDaggerColor;
            
            // Add rotation controller for natural flight
            projectile.AddComponent<DaggerRotationController>();
            
            // Add ground collision component to make dagger stick in ground
            DaggerGroundCollision groundCollision = projectile.AddComponent<DaggerGroundCollision>();
            groundCollision.weaponController = this;
        }
        else
        {
            // Fallback visual (purple rectangle)
            int textureWidth = Mathf.RoundToInt(projectileWidth * 64);
            int textureHeight = Mathf.RoundToInt(projectileHeight * 64);
            
            Texture2D daggerTexture = new Texture2D(textureWidth, textureHeight);
            
            for (int x = 0; x < textureWidth; x++)
            {
                for (int y = 0; y < textureHeight; y++)
                {
                    daggerTexture.SetPixel(x, y, tripleDaggerColor);
                }
            }
            
            daggerTexture.Apply();
            Sprite daggerSprite = Sprite.Create(daggerTexture, new Rect(0, 0, textureWidth, textureHeight), new Vector2(0.5f, 0.5f));
            daggerRenderer.sprite = daggerSprite;
            
            // Add rotation controller and ground collision for fallback daggers too
            projectile.AddComponent<DaggerRotationController>();
            DaggerGroundCollision groundCollision = projectile.AddComponent<DaggerGroundCollision>();
            groundCollision.weaponController = this;
        }
        
        // Set lifetime and cleanup
        StartCoroutine(DestroyProjectileAfterTime(projectile, projectileLifetime));
        
        return projectile;
    }
    
    private Vector3 RotateVector2D(Vector3 vector, float angleInDegrees)
    {
        float angleInRadians = angleInDegrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(angleInRadians);
        float sin = Mathf.Sin(angleInRadians);
        
        return new Vector3(
            vector.x * cos - vector.y * sin,
            vector.x * sin + vector.y * cos,
            vector.z
        );
    }
    
    private void UseStormShard(bool isRightClick)
    {
        if (playerTransform == null) return;
        
        if (isRightClick)
        {
            // Right-click: Lightning bolt from sky with cooldown
            if (Time.time - lastLightningBoltTime < boltCooldown) return;
            
            CreateLightningBolt();
            lastLightningBoltTime = Time.time;
            
            // Generate ultimate charge for storm right click attack
            GenerateUltimateCharge(stormRightClickCharge);
        }
        else
        {
            // Left-click: Electric arc with cooldown (only for manual clicks, auto-fire handled separately)
            if (!isAutoFiring && Time.time - lastLightningArcTime < lightningCooldown) return;
            
            // For manual clicks, cycle between attack types 1 and 2
            if (!isAutoFiring) {
                stormClickCount = (stormClickCount + 1) % 2; // Cycles 0 -> 1 -> 0 -> 1...
                CreateElectricArc(stormClickCount);
                lastLightningArcTime = Time.time;
            }
            // Note: Auto-fire is handled in Update() method with its own timing
            
            // Generate ultimate charge for storm left click attack (manual click)
            GenerateUltimateCharge(stormLeftClickCharge);
        }
    }
    
    private IEnumerator CreateSwordAttack(Vector3 startPosition)
    {
        // Create temporary damage object
        GameObject swordAttack = new GameObject("SwordAttack");
        swordAttack.transform.position = startPosition;
        
        // Add collider for damage detection - larger size spanning player height
        BoxCollider2D attackCollider = swordAttack.AddComponent<BoxCollider2D>();
        attackCollider.size = new Vector2(swordWidth, swordHeight);
        attackCollider.isTrigger = true;
        
        // Add damage object component with player layer exclusion
        DamageObject damageComponent = swordAttack.AddComponent<DamageObject>();
        damageComponent.damageAmount = playerMovement.GetModifiedMeleeDamage(swordDamage);
        damageComponent.damageRate = 0.1f; // Fast damage rate for sword
        
        // Use reflection to set the private excludePlayerLayer field
        var excludeField = typeof(DamageObject).GetField("excludePlayerLayer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (excludeField != null)
        {
            excludeField.SetValue(damageComponent, true);
        }
        
        // Use reflection to set the private canDamageEnemies field
        var enemyDamageField = typeof(DamageObject).GetField("canDamageEnemies", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (enemyDamageField != null)
        {
            enemyDamageField.SetValue(damageComponent, true);
        }
        
        // Exclude NPC and PlayerSummon layers to prevent damaging player summons/allies
        damageComponent.excludeLayers = LayerMask.GetMask("NPC", "PlayerSummon");
        
        // Visual indicator (temporary - will be replaced with graphics later)
        SpriteRenderer swordRenderer = swordAttack.AddComponent<SpriteRenderer>();
        
        // Create larger red rectangle sprite for sword attack (scaled to match collider)
        int textureWidth = Mathf.RoundToInt(swordWidth * 64); // Scale texture based on collider width
        int textureHeight = Mathf.RoundToInt(swordHeight * 32); // Scale texture based on collider height
        Texture2D swordTexture = new Texture2D(textureWidth, textureHeight);
        Color[] pixels = new Color[textureWidth * textureHeight];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = new Color(1f, 0f, 0f, 0.7f); // Semi-transparent red, more visible
        }
        swordTexture.SetPixels(pixels);
        swordTexture.Apply();
        
        swordRenderer.sprite = Sprite.Create(swordTexture, new Rect(0, 0, textureWidth, textureHeight), Vector2.one * 0.5f);
        swordRenderer.sortingLayerName = "Player";
        swordRenderer.sortingOrder = 0;
        
        // Store initial player position and facing direction
        bool facingLeft = false;
        SpriteRenderer playerSprite = GetComponent<SpriteRenderer>();
        if (playerSprite == null)
            playerSprite = GetComponentInChildren<SpriteRenderer>();
        if (playerSprite != null)
            facingLeft = playerSprite.flipX;
        
        // Track sword attack duration
        float elapsed = 0f;
        
        while (elapsed < swordDuration)
        {
            // Make sword follow player
            if (playerTransform != null)
            {
                Vector3 newPosition = playerTransform.position + (Vector3.right * (facingLeft ? -swordRange : swordRange));
                swordAttack.transform.position = newPosition;
            }
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        // Destroy attack object
        Destroy(swordAttack);
    }
    
    private void CreateWaveAttack()
    {
        // Determine number of blocks based on charge time
        int waveBlocks = GetWaveBlockCount(currentChargeTime);
        
        // Apply Valor Shard passive buffs for wave charges 3+
        ApplyWaveChargeBuffs(waveBlocks);
        
        // Get player's facing direction
        SpriteRenderer playerSprite = GetComponent<SpriteRenderer>();
        if (playerSprite == null)
            playerSprite = GetComponentInChildren<SpriteRenderer>();
        
        bool facingLeft = playerSprite != null ? playerSprite.flipX : false;
        Vector3 waveDirection = facingLeft ? Vector3.left : Vector3.right;
        
        // Generate ultimate charge for valor right click wave attack
        GenerateUltimateCharge(valorRightClickCharge);
        
        StartCoroutine(CreateWaveBlocks(waveBlocks, waveDirection));
    }
    
    private int GetWaveBlockCount(float chargeTime)
    {
        if (chargeTime < 0.5f) return 1;        // Less than 0.5 second = 1 block
        else if (chargeTime < 0.8f) return 2;   // 0.5-0.8 seconds = 2 blocks
        else if (chargeTime < 1.2f) return 3;   // 0.8-1.2 seconds = 3 blocks
        else return 5;                          // 1.2+ seconds = 5 blocks
    }
    
    private IEnumerator CreateWaveBlocks(int blockCount, Vector3 direction)
    {
        List<GameObject> waveBlocks = new List<GameObject>();
        
        // Create all wave blocks
        for (int i = 0; i < blockCount; i++)
        {
            // Calculate position for this block (no overlap)
            float distance = (i + 1) * waveBlockSpacing;
            Vector3 blockPosition = playerTransform.position + (direction * distance);
            
            // Start blocks underground
            blockPosition.y -= 1f;
            
            GameObject waveBlock = CreateWaveBlock(blockPosition, i);
            waveBlocks.Add(waveBlock);
        }
        
        // Animate blocks rising sequentially with wave delay
        for (int i = 0; i < waveBlocks.Count; i++)
        {
            if (waveBlocks[i] != null)
            {
                StartCoroutine(AnimateWaveBlock(waveBlocks[i], i * 0.1f)); // 0.1s delay between blocks
            }
        }
        
        yield return null;
    }
    
    private GameObject CreateWaveBlock(Vector3 position, int blockIndex)
    {
        GameObject waveBlock = new GameObject($"WaveBlock_{blockIndex}");
        waveBlock.transform.position = position;
        
        // Add collider for damage detection
        BoxCollider2D blockCollider = waveBlock.AddComponent<BoxCollider2D>();
        blockCollider.size = new Vector2(waveBlockSize, waveBlockSize);
        blockCollider.isTrigger = true;
        
        // Add damage object component
        DamageObject damageComponent = waveBlock.AddComponent<DamageObject>();
        damageComponent.damageAmount = playerMovement.GetModifiedMeleeDamage(waveDamage);
        damageComponent.damageRate = 0.1f;
        
        // Configure damage object
        var excludeField = typeof(DamageObject).GetField("excludePlayerLayer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (excludeField != null)
        {
            excludeField.SetValue(damageComponent, true);
        }
        
        var enemyDamageField = typeof(DamageObject).GetField("canDamageEnemies", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (enemyDamageField != null)
        {
            enemyDamageField.SetValue(damageComponent, true);
        }
        
        // Exclude NPC and PlayerSummon layers to prevent damaging player summons/allies
        damageComponent.excludeLayers = LayerMask.GetMask("NPC", "PlayerSummon");
        
        // Visual indicator (valor wave prefab or fallback sprite)
        if (valorWavePrefab != null)
        {
            Debug.Log("Using valor wave prefab: " + valorWavePrefab.name);
            // Use animated prefab - instantiate it as child of the wave block
            GameObject animatedWave = Instantiate(valorWavePrefab, waveBlock.transform);
            animatedWave.transform.localPosition = Vector3.zero;
            animatedWave.transform.localScale = Vector3.one;
            
            // Store the animated wave reference in the wave block for later triggering
            WaveBlockComponent waveComponent = waveBlock.GetComponent<WaveBlockComponent>();
            if (waveComponent == null)
            {
                waveComponent = waveBlock.AddComponent<WaveBlockComponent>();
            }
            waveComponent.animatedWave = animatedWave;
        }
        else
        {
            Debug.Log("Valor wave prefab is null, using fallback sprite");
            // Fallback: Use sprite renderer for static sprite
            SpriteRenderer blockRenderer = waveBlock.AddComponent<SpriteRenderer>();
            
            // Fallback: Create texture scaled to match collider size
            int textureSize = Mathf.RoundToInt(waveBlockSize * 80);
            Texture2D blockTexture = new Texture2D(textureSize, textureSize);
            Color[] pixels = new Color[textureSize * textureSize];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color(1f, 0.8f, 0f, 0.9f); // Golden yellow
            }
            blockTexture.SetPixels(pixels);
            blockTexture.Apply();
            blockRenderer.sprite = Sprite.Create(blockTexture, new Rect(0, 0, textureSize, textureSize), Vector2.one * 0.5f);
            
            blockRenderer.sortingLayerName = "Player";
            blockRenderer.sortingOrder = 0;
        }
        
        return waveBlock;
    }
    
    private IEnumerator AnimateWaveBlock(GameObject waveBlock, float delay)
    {
        // Wait for wave delay
        yield return new WaitForSeconds(delay);
        
        if (waveBlock == null) yield break;
        
        // Trigger the wave animation on the prefab if it exists
        WaveBlockComponent waveComponent = waveBlock.GetComponent<WaveBlockComponent>();
        if (waveComponent != null)
        {
            waveComponent.TriggerWaveAnimation();
        }
        
        Vector3 startPos = waveBlock.transform.position;
        Vector3 groundPos = new Vector3(startPos.x, startPos.y + 1f, startPos.z); // Rise to ground level
        Vector3 peakPos = new Vector3(startPos.x, startPos.y + 1f + waveBounceHeight, startPos.z); // Bounce up
        
        float riseTime = 0.2f; // Time to rise from underground
        float bounceTime = 0.3f; // Time to bounce up and down
        float fallTime = 0.2f; // Time to fall back to ground
        
        // Rise from underground to ground level
        float elapsed = 0f;
        while (elapsed < riseTime && waveBlock != null)
        {
            float progress = elapsed / riseTime;
            waveBlock.transform.position = Vector3.Lerp(startPos, groundPos, progress);
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        if (waveBlock != null)
            waveBlock.transform.position = groundPos;
        
        // Bounce up
        elapsed = 0f;
        while (elapsed < bounceTime && waveBlock != null)
        {
            float progress = elapsed / bounceTime;
            float bounceProgress = Mathf.Sin(progress * Mathf.PI); // Smooth arc
            Vector3 currentPos = Vector3.Lerp(groundPos, peakPos, bounceProgress);
            waveBlock.transform.position = currentPos;
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        // Fall back down
        elapsed = 0f;
        Vector3 fallStart = waveBlock != null ? waveBlock.transform.position : peakPos;
        while (elapsed < fallTime && waveBlock != null)
        {
            float progress = elapsed / fallTime;
            waveBlock.transform.position = Vector3.Lerp(fallStart, groundPos, progress);
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        // Wait a moment then destroy
        yield return new WaitForSeconds(waveBlockCleanupDelay);
        
        if (waveBlock != null)
            Destroy(waveBlock);
    }
    
    private void FindStormParticlePoint()
    {
        // Find existing SSParticlePoint1 and SSParticlePoint2 in player hierarchy
        Transform particlePoint1 = transform.Find("SSParticlePoint1");
        Transform particlePoint2 = transform.Find("SSParticlePoint2");
        
        if (particlePoint1 == null)
        {
            // Search in children recursively
            particlePoint1 = GetComponentInChildren<Transform>().Find("SSParticlePoint1");
        }
        
        if (particlePoint2 == null)
        {
            // Search in children recursively
            particlePoint2 = GetComponentInChildren<Transform>().Find("SSParticlePoint2");
        }
        
        if (particlePoint1 != null)
        {
            stormParticlePoint1 = particlePoint1.gameObject;
        }
        else
        {
            Debug.LogError("SSParticlePoint1 not found! Please create an empty GameObject named 'SSParticlePoint1' as a child of the player.");
            // Create a fallback point
            stormParticlePoint1 = new GameObject("SSParticlePoint1_Fallback");
            stormParticlePoint1.transform.SetParent(transform);
            stormParticlePoint1.transform.localPosition = new Vector3(1.5f, 1f, 0);
        }
        
        if (particlePoint2 != null)
        {
            stormParticlePoint2 = particlePoint2.gameObject;
        }
        else
        {
            Debug.LogError("SSParticlePoint2 not found! Please create an empty GameObject named 'SSParticlePoint2' as a child of the player.");
            // Create a fallback point
            stormParticlePoint2 = new GameObject("SSParticlePoint2_Fallback");
            stormParticlePoint2.transform.SetParent(transform);
            stormParticlePoint2.transform.localPosition = new Vector3(1.5f, 0.5f, 0);
        }
    }
    
    private void UpdateStormParticlePosition(int particlePointNumber)
    {
        GameObject targetParticlePoint = particlePointNumber == 0 ? stormParticlePoint1 : stormParticlePoint2;
        if (targetParticlePoint == null) return;
        
        // Get player's facing direction
        SpriteRenderer playerSprite = GetComponent<SpriteRenderer>();
        if (playerSprite == null)
            playerSprite = GetComponentInChildren<SpriteRenderer>();
        
        bool facingLeft = playerSprite != null ? playerSprite.flipX : false;
        
        // Position the particle point based on facing direction
        // Adjust these values based on your desired positioning
        float xOffset = facingLeft ? -staffRange : staffRange;
        float yOffset = particlePointNumber == 0 ? 1f : 0.5f; // Different heights for different points
        
        targetParticlePoint.transform.localPosition = new Vector3(xOffset, yOffset, 0);
    }
    
    private void UpdatePlayerFacingForMouse()
    {
        // Get mouse position in world space
        Vector3 mousePosition = Vector3.zero;
        if (Mouse.current != null && Camera.main != null)
        {
            Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
            mousePosition = Camera.main.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, Camera.main.nearClipPlane));
            mousePosition.z = 0; // Ensure z is 0 for 2D
        }
        
        // Get player sprite for flipping
        SpriteRenderer playerSprite = GetComponent<SpriteRenderer>();
        if (playerSprite == null)
            playerSprite = GetComponentInChildren<SpriteRenderer>();
        
        if (playerSprite != null && playerTransform != null)
        {
            // Determine if mouse is to the left or right of player
            bool mouseIsLeft = mousePosition.x < playerTransform.position.x;
            bool previousFlipX = playerSprite.flipX;
            
            // Update player facing direction
            playerSprite.flipX = mouseIsLeft;
            
            // If facing direction changed, flip particle points
            if (previousFlipX != playerSprite.flipX)
            {
                FlipParticlePoints(playerSprite.flipX);
            }
        }
    }
    
    /// <summary>
    /// Flip SS particle points to match player's facing direction while preserving relative position
    /// </summary>
    public void FlipParticlePoints(bool facingLeft)
    {
        if (stormParticlePoint1 != null)
        {
            Vector3 currentPos = stormParticlePoint1.transform.localPosition;
            // Flip X position while preserving Y and Z
            stormParticlePoint1.transform.localPosition = new Vector3(-currentPos.x, currentPos.y, currentPos.z);
        }
        
        if (stormParticlePoint2 != null)
        {
            Vector3 currentPos = stormParticlePoint2.transform.localPosition;
            // Flip X position while preserving Y and Z
            stormParticlePoint2.transform.localPosition = new Vector3(-currentPos.x, currentPos.y, currentPos.z);
        }
    }
    
    private void CreateDaggerStrike(int attackAnimationType = 0)
    {
        if (playerTransform == null) return;
        
        // Trigger melee attack animation
        TriggerAttackAnimation(attackAnimationType, daggerDuration);
        
        // Get player's facing direction
        SpriteRenderer playerSprite = GetComponent<SpriteRenderer>();
        if (playerSprite == null)
            playerSprite = GetComponentInChildren<SpriteRenderer>();
        
        bool facingLeft = playerSprite != null ? playerSprite.flipX : false;
        Vector3 attackPosition = playerTransform.position + (Vector3.right * (facingLeft ? -daggerRange : daggerRange));
        
        // Add small delay to sync with animation start
        StartCoroutine(DelayedDaggerAttack(attackPosition));
    }
    
    private System.Collections.IEnumerator DelayedDaggerAttack(Vector3 attackPosition)
    {
        // Wait for animation to start
        yield return new WaitForSeconds(daggerAnimationDelay);
        
        // Now create the actual damage object
        StartCoroutine(CreateDaggerAttack(attackPosition));
    }
    
    private IEnumerator CreateDaggerAttack(Vector3 startPosition)
    {
        // Create temporary dagger damage object (smaller than sword)
        GameObject daggerAttack = new GameObject("DaggerAttack");
        daggerAttack.transform.position = startPosition;
        
        // Add collider for damage detection
        BoxCollider2D attackCollider = daggerAttack.AddComponent<BoxCollider2D>();
        attackCollider.size = new Vector2(daggerWidth, daggerHeight);
        attackCollider.isTrigger = true;
        
        // Add damage object component
        DamageObject damageComponent = daggerAttack.AddComponent<DamageObject>();
        damageComponent.damageAmount = playerMovement.GetModifiedMeleeDamage(daggerDamage);
        damageComponent.damageRate = 0.1f;
        
        // Add Whisper Shard passive callback
        damageComponent.onEnemyHit = () => ApplyWhisperAttackPassive();
        
        // Configure damage object
        var excludeField = typeof(DamageObject).GetField("excludePlayerLayer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (excludeField != null)
        {
            excludeField.SetValue(damageComponent, true);
        }
        
        var enemyDamageField = typeof(DamageObject).GetField("canDamageEnemies", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (enemyDamageField != null)
        {
            enemyDamageField.SetValue(damageComponent, true);
        }
        
        // Exclude NPC and PlayerSummon layers to prevent damaging player summons/allies
        damageComponent.excludeLayers = LayerMask.GetMask("NPC", "PlayerSummon");
        
        // Visual indicator (blue for dagger)
        SpriteRenderer daggerRenderer = daggerAttack.AddComponent<SpriteRenderer>();
        
        // Create larger texture scaled to match collider size
        int textureWidth = Mathf.RoundToInt(daggerWidth * 64);
        int textureHeight = Mathf.RoundToInt(daggerHeight * 64);
        Texture2D daggerTexture = new Texture2D(textureWidth, textureHeight);
        Color[] pixels = new Color[textureWidth * textureHeight];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = new Color(0f, 0.5f, 1f, 0.8f); // Blue color for dagger, more visible
        }
        daggerTexture.SetPixels(pixels);
        daggerTexture.Apply();
        
        daggerRenderer.sprite = Sprite.Create(daggerTexture, new Rect(0, 0, textureWidth, textureHeight), Vector2.one * 0.5f);
        daggerRenderer.sortingLayerName = "Player";
        daggerRenderer.sortingOrder = 0;
        
        // Store facing direction
        SpriteRenderer playerSprite = GetComponent<SpriteRenderer>();
        if (playerSprite == null)
            playerSprite = GetComponentInChildren<SpriteRenderer>();
        bool facingLeft = playerSprite != null ? playerSprite.flipX : false;
        
        // Track attack duration and follow player
        float elapsed = 0f;
        while (elapsed < daggerDuration)
        {
            if (playerTransform != null)
            {
                Vector3 newPosition = playerTransform.position + (Vector3.right * (facingLeft ? -daggerRange : daggerRange));
                daggerAttack.transform.position = newPosition;
            }
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        Destroy(daggerAttack);
    }
    
    private void ThrowDaggerProjectile()
    {
        if (playerTransform == null) return;
        
        // Trigger projectile attack animation (Type 2)
        TriggerAttackAnimation(2, 0.4f);
        
        // Get mouse position in world space
        Vector3 mousePosition = Vector3.zero;
        if (Mouse.current != null && Camera.main != null)
        {
            Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
            mousePosition = Camera.main.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, Camera.main.nearClipPlane));
            mousePosition.z = 0; // Ensure z is 0 for 2D
        }
        
        // Calculate throw direction from player to mouse
        Vector3 throwDirection = (mousePosition - playerTransform.position).normalized;
        
        // If mouse position is invalid or too close, use facing direction as fallback
        if (throwDirection.magnitude < 0.1f)
        {
            SpriteRenderer playerSprite = GetComponent<SpriteRenderer>();
            if (playerSprite == null)
                playerSprite = GetComponentInChildren<SpriteRenderer>();
            
            bool facingLeft = playerSprite != null ? playerSprite.flipX : false;
            throwDirection = facingLeft ? Vector3.left : Vector3.right;
        }
        
        // Start position is slightly in front of player in throw direction
        Vector3 startPosition = playerTransform.position + (throwDirection * 0.5f);
        
        StartCoroutine(DelayedProjectileThrow(startPosition, throwDirection));
    }
    
    private System.Collections.IEnumerator DelayedProjectileThrow(Vector3 startPosition, Vector3 throwDirection)
    {
        // Wait for throw animation to progress before spawning projectile
        yield return new WaitForSeconds(projectileThrowAnimationDelay);
        
        // Now create the projectile
        StartCoroutine(CreateProjectileDagger(startPosition, throwDirection));
    }
    
    private void RedirectDagger()
    {
        // Check if we have active triple daggers
        if (activeDaggers.Count > 0)
        {
            RedirectAllActiveDaggers();
            return;
        }
        
        // Handle single dagger redirect (original behavior)
        if (currentThrownDagger == null || daggerExpired) return;
        
        // Find nearest enemy within detection range
        GameObject nearestEnemy = FindNearestEnemy();
        
        if (nearestEnemy != null)
        {
            currentRedirectCount++;
            Debug.Log($"WhisperShard: Dagger redirect #{currentRedirectCount} - targeting enemy! ({maxDaggerRedirects - currentRedirectCount} redirects remaining)");
            
            // Extend cooldown with each redirect to increase dagger duration
            lastProjectileAttackTime += redirectCooldownExtension;
            Debug.Log($"WhisperShard: Cooldown extended by {redirectCooldownExtension}s - new remaining cooldown: {(lastProjectileAttackTime + projectileCooldown - Time.time):F1}s");
            
            StartCoroutine(CreateRedirectAttack(currentThrownDagger, nearestEnemy));
        }
        else
        {
            Debug.Log("WhisperShard: No enemies in range for redirect!");
        }
        
        // Check if dagger should expire after this redirect
        if (currentRedirectCount >= maxDaggerRedirects)
        {
            daggerExpired = true;
            Debug.Log("WhisperShard: Dagger has reached maximum redirects and will expire!");
        }
    }
    
    private void RedirectAllActiveDaggers()
    {
        // Clean up null references first
        activeDaggers.RemoveAll(dagger => dagger == null);
        
        if (activeDaggers.Count == 0) return;
        
        // Find multiple nearby enemies for each dagger
        List<GameObject> nearbyEnemies = FindMultipleNearbyEnemies(activeDaggers.Count);
        
        if (nearbyEnemies.Count == 0)
        {
            Debug.Log("WhisperShard: No enemies in range for triple dagger redirect!");
            return;
        }
        
        currentRedirectCount++;
        Debug.Log($"WhisperShard: Triple dagger redirect #{currentRedirectCount} - targeting {nearbyEnemies.Count} enemies! ({maxDaggerRedirects - currentRedirectCount} redirects remaining)");
        
        // Redirect each dagger to a different enemy (or same if fewer enemies)
        for (int i = 0; i < activeDaggers.Count; i++)
        {
            GameObject dagger = activeDaggers[i];
            if (dagger == null) continue;
            
            // Assign enemy target (cycle through available enemies)
            GameObject targetEnemy = nearbyEnemies[i % nearbyEnemies.Count];
            
            StartCoroutine(CreateRedirectAttack(dagger, targetEnemy));
        }
        
        // Check if daggers should expire after this redirect
        if (currentRedirectCount >= maxDaggerRedirects)
        {
            Debug.Log("WhisperShard: Triple daggers have reached maximum redirects and will expire!");
            // Clean up expired daggers
            StartCoroutine(CleanupExpiredTripleDaggers());
        }
    }
    
    private List<GameObject> FindMultipleNearbyEnemies(int maxCount)
    {
        List<GameObject> enemies = new List<GameObject>();
        
        // Filter and sort by distance
        var validEnemies = new List<(GameObject enemy, float distance)>();
        
        // Find all enemies with EnemyBehavior component within range
        EnemyBehavior[] enemyBehaviors = FindObjectsByType<EnemyBehavior>(FindObjectsSortMode.None);
        foreach (EnemyBehavior enemyBehavior in enemyBehaviors)
        {
            if (enemyBehavior == null || enemyBehavior.gameObject == null) continue;
            if (enemyBehavior.IsDead) continue; // Skip dead enemies
            
            float distance = Vector3.Distance(playerTransform.position, enemyBehavior.transform.position);
            if (distance <= enemyDetectionRange)
            {
                validEnemies.Add((enemyBehavior.gameObject, distance));
            }
        }
        
        // Also find DragonBoss enemies
        DragonBoss[] dragonBosses = FindObjectsByType<DragonBoss>(FindObjectsSortMode.None);
        foreach (DragonBoss dragonBoss in dragonBosses)
        {
            if (dragonBoss == null || dragonBoss.gameObject == null) continue;
            if (dragonBoss.IsDead) continue; // Skip dead dragons
            
            float distance = Vector3.Distance(playerTransform.position, dragonBoss.transform.position);
            if (distance <= enemyDetectionRange)
            {
                validEnemies.Add((dragonBoss.gameObject, distance));
            }
        }
        
        // Sort by distance and take up to maxCount
        validEnemies.Sort((a, b) => a.distance.CompareTo(b.distance));
        
        int count = Mathf.Min(maxCount, validEnemies.Count);
        for (int i = 0; i < count; i++)
        {
            enemies.Add(validEnemies[i].enemy);
        }
        
        return enemies;
    }
    
    private IEnumerator CleanupExpiredTripleDaggers()
    {
        yield return new WaitForSeconds(projectileCleanupDelay); // Small delay to let redirects complete
        
        // Destroy all active daggers
        foreach (GameObject dagger in activeDaggers)
        {
            if (dagger != null)
            {
                Destroy(dagger);
            }
        }
        
        activeDaggers.Clear();
        currentRedirectCount = 0; // Reset for future attacks
        
        Debug.Log("WhisperShard: Expired triple daggers cleaned up!");
    }
    
    private GameObject FindNearestEnemy()
    {
        if (playerTransform == null) return null;
        
        // Use the range version with enemyDetectionRange for consistency
        return FindNearestEnemy(enemyDetectionRange);
    }
    
    private bool IsEnemy(GameObject obj)
    {
        // First check if it has EnemyBehavior component and verify it's alive
        EnemyBehavior enemyBehavior = obj.GetComponent<EnemyBehavior>();
        if (enemyBehavior != null)
        {
            // Only count as valid target if enemy is not dead
            return !enemyBehavior.IsDead;
        }
        
        // Check for DragonBoss component and verify it's alive
        DragonBoss dragonBoss = obj.GetComponent<DragonBoss>();
        if (dragonBoss != null)
        {
            // Only count as valid target if dragon is not dead
            return !dragonBoss.IsDead;
        }
        
        // Check for enemy indicators - you can expand this based on your enemy system
        if (obj.CompareTag("Enemy")) return true;
        if (obj.name.ToLower().Contains("enemy")) return true;
        
        // Check layer (if enemies are on a specific layer)
        if (obj.layer == LayerMask.NameToLayer("Enemy")) return true;
        
        return false;
    }
    


    private IEnumerator CreateRedirectAttack(GameObject dagger, GameObject target)
    {
        if (dagger == null || target == null) yield break;
        
        // Wait for projectile redirect delay to create smooth redirects
        yield return new WaitForSeconds(projectileRedirectDelay);
        
        if (dagger == null || target == null) yield break; // Check again after delay
        
        Rigidbody2D daggerRb = dagger.GetComponent<Rigidbody2D>();
        if (daggerRb == null) yield break;
        
        // Change dagger color to indicate wind redirect (purple-ish wind effect)
        SpriteRenderer daggerRenderer = dagger.GetComponent<SpriteRenderer>();
        if (daggerRenderer != null)
        {
            daggerRenderer.color = new Color(0.8f, 0.3f, 1f, 1f); // Purple wind effect
        }
        
        // Disable gravity for redirect but keep collision detection active
        daggerRb.gravityScale = 0f;
        // Ensure collider is still active for ground detection
        Collider2D daggerCollider = dagger.GetComponent<Collider2D>();
        if (daggerCollider != null)
        {
            daggerCollider.isTrigger = true; // Ensure trigger detection works
        }
        
        // Calculate direction to target with random body part targeting
        Vector3 targetPosition = GetRandomBodyPartPosition(target);
        Vector3 directionToTarget = (targetPosition - dagger.transform.position).normalized;
        
        // Update dagger rotation to face redirect direction
        UpdateDaggerRotation(dagger, directionToTarget);
        
        // Apply redirect velocity (faster than normal projectile)
        daggerRb.linearVelocity = directionToTarget * (recallSpeed * 1.2f);
        
        // Track the redirect for up to 1.5 seconds or until it hits the target
        float redirectTimer = 0f;
        float maxRedirectTime = 1.5f;
        
        while (dagger != null && redirectTimer < maxRedirectTime)
        {
            redirectTimer += Time.deltaTime;
            
            if (target != null)
            {
                // Update direction to target (homing effect)
                Vector3 newDirection = (target.transform.position - dagger.transform.position).normalized;
                daggerRb.linearVelocity = newDirection * (recallSpeed * 1.2f);
                
                // Update rotation to match redirect direction
                UpdateDaggerRotation(dagger, newDirection);
                
                // Check if dagger passed through target
                float distanceToTarget = Vector3.Distance(dagger.transform.position, target.transform.position);
                if (distanceToTarget < 1.5f)
                {
                    // Dagger hit target, continue moving through and ready for next redirect
                    Debug.Log("WhisperShard: Dagger passed through target, ready for next redirect!");
                    
                    // Reset color and restore normal projectile behavior
                    if (daggerRenderer != null)
                    {
                        daggerRenderer.color = Color.white;
                    }
                    daggerRb.gravityScale = 1f; // Restore gravity
                    
                    // Continue moving in the same direction instead of stopping
                    Vector3 throughDirection = newDirection;
                    daggerRb.linearVelocity = throughDirection * projectileSpeed;
                    
                    // Re-enable rotation controller for natural flight
                    DaggerRotationController rotationController = dagger.GetComponent<DaggerRotationController>();
                    if (rotationController != null)
                    {
                        rotationController.enabled = true;
                    }
                    
                    break; // Exit redirect tracking, dagger continues flying
                }
            }
            
            yield return null;
        }
        
        // Redirect timed out - restore normal dagger behavior
        if (dagger != null)
        {
            // Reset color and restore normal projectile behavior
            if (daggerRenderer != null)
            {
                daggerRenderer.color = Color.white;
            }
            
            if (daggerRb != null)
            {
                daggerRb.gravityScale = 1f; // Restore gravity for ground collision
            }
            
            // Re-enable rotation controller for natural flight
            DaggerRotationController rotationController = dagger.GetComponent<DaggerRotationController>();
            if (rotationController != null)
            {
                rotationController.enabled = true;
            }
            
            Debug.Log("WhisperShard: Redirect timed out, dagger returning to normal flight");
        }
        
        // Handle cleanup for single dagger vs triple daggers
        if (currentRedirectCount >= maxDaggerRedirects)
        {
            Debug.Log("WhisperShard: Dagger has expired after maximum redirects!");
            
            // If this is the currentThrownDagger (single dagger system)
            if (dagger == currentThrownDagger)
            {
                Destroy(currentThrownDagger);
                currentThrownDagger = null;
                daggerExpired = false; // Reset for next dagger
                currentRedirectCount = 0;
            }
            // Triple daggers are handled by CleanupExpiredTripleDaggers
        }
    }

    private Vector3 GetRandomBodyPartPosition(GameObject target)
    {
        Vector3 basePosition = target.transform.position;
        
        // Get enemy sprite bounds to calculate relative body part positions
        SpriteRenderer enemyRenderer = target.GetComponent<SpriteRenderer>();
        float spriteHeight = 1f; // Default height
        
        if (enemyRenderer != null && enemyRenderer.sprite != null)
        {
            spriteHeight = enemyRenderer.bounds.size.y;
        }
        
        // Define body part offsets as percentages of sprite height
        float[] bodyPartOffsets = 
        {
            0.7f,   // Head (70% up from center)
            0.0f,   // Torso (center)
            -0.4f,  // Lower torso (40% down from center)
            -0.7f   // Legs (70% down from center)
        };
        
        // Pick random body part
        int randomPart = Random.Range(0, bodyPartOffsets.Length);
        Vector3 offset = new Vector3(0, bodyPartOffsets[randomPart] * spriteHeight, 0);
        
        // Add slight horizontal randomness for more natural targeting
        offset.x += Random.Range(-0.2f, 0.2f);
        
        return basePosition + offset;
    }
    
    private IEnumerator DestroyProjectileAfterTime(GameObject projectile, float lifetime)
    {
        yield return new WaitForSeconds(lifetime);
        
        if (projectile != null)
        {
            // Remove from activeDaggers list if it's a triple dagger
            if (activeDaggers.Contains(projectile))
            {
                activeDaggers.Remove(projectile);
                Debug.Log($"WhisperShard: Triple dagger expired naturally. {activeDaggers.Count} daggers remaining.");
            }
            
            Destroy(projectile);
        }
    }

    private IEnumerator CreateProjectileDagger(Vector3 startPosition, Vector3 direction)
    {
        // Create projectile dagger (1/3 player size)
        GameObject projectile = new GameObject("DaggerProjectile");
        projectile.transform.position = startPosition;
        
        // Add rigidbody for physics
        Rigidbody2D rb = projectile.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0.3f; // Reduced gravity for longer horizontal flight
        rb.linearVelocity = direction * projectileSpeed;
        
        // Add collider (smaller size - 1/3 player size)
        BoxCollider2D projectileCollider = projectile.AddComponent<BoxCollider2D>();
        projectileCollider.size = new Vector2(projectileWidth, projectileHeight);
        projectileCollider.isTrigger = true;
        
        // Add damage object component
        DamageObject damageComponent = projectile.AddComponent<DamageObject>();
        damageComponent.damageAmount = playerMovement.GetModifiedMeleeDamage(projectileDamage);
        damageComponent.damageRate = 0.1f;
        
        // Add Whisper Shard passive callback
        damageComponent.onEnemyHit = () => ApplyWhisperAttackPassive();
        
        // Configure damage object
        var excludeField = typeof(DamageObject).GetField("excludePlayerLayer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (excludeField != null)
        {
            excludeField.SetValue(damageComponent, true);
        }
        
        var enemyDamageField = typeof(DamageObject).GetField("canDamageEnemies", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (enemyDamageField != null)
        {
            enemyDamageField.SetValue(damageComponent, true);
        }
        
        // Exclude NPC and PlayerSummon layers to prevent damaging player summons/allies
        damageComponent.excludeLayers = LayerMask.GetMask("NPC", "PlayerSummon");
        
        // Visual indicator (dagger sprite)
        SpriteRenderer projectileRenderer = projectile.AddComponent<SpriteRenderer>();
        
        // Use custom dagger sprite if assigned, otherwise create fallback blue texture
        if (daggerSprite != null)
        {
            projectileRenderer.sprite = daggerSprite;
        }
        else
        {
            // Fallback: Create texture scaled to match collider size
            int textureWidth = Mathf.RoundToInt(projectileWidth * 80);
            int textureHeight = Mathf.RoundToInt(projectileHeight * 80);
            Texture2D projectileTexture = new Texture2D(textureWidth, textureHeight);
            Color[] pixels = new Color[textureWidth * textureHeight];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color(0f, 0.7f, 1f, 0.9f); // Bright blue for projectile
            }
            projectileTexture.SetPixels(pixels);
            projectileTexture.Apply();
            projectileRenderer.sprite = Sprite.Create(projectileTexture, new Rect(0, 0, textureWidth, textureHeight), Vector2.one * 0.5f);
        }
        
        projectileRenderer.sortingLayerName = "Player";
        projectileRenderer.sortingOrder = 0;
        
        // Add rotation component to make dagger face travel direction
        projectile.AddComponent<DaggerRotationController>();
        
        // Add ground collision component to make dagger stick in ground
        DaggerGroundCollision groundCollision = projectile.AddComponent<DaggerGroundCollision>();
        groundCollision.weaponController = this;
        
        // Add cleanup component to handle reference clearing
        DaggerCleanup cleanup = projectile.AddComponent<DaggerCleanup>();
        cleanup.weaponController = this;
        
        // Track this projectile for potential recall
        currentThrownDagger = projectile;
        
        // Wait for projectile lifetime
        yield return new WaitForSeconds(projectileLifetime);
        
        // Clear reference when projectile expires naturally
        if (currentThrownDagger == projectile)
        {
            currentThrownDagger = null;
            currentRedirectCount = 0;
            daggerExpired = false;
        }
            
        // Destroy projectile
        Destroy(projectile);
    }
    
    private void CreateElectricArc(int attackAnimationType = 0)
    {
        GameObject currentParticlePoint = attackAnimationType == 0 ? stormParticlePoint1 : stormParticlePoint2;
        if (playerTransform == null || currentParticlePoint == null) return;
        
        // Trigger magic attack animation
        TriggerAttackAnimation(attackAnimationType, lightningDuration);
        
        // Add small delay to sync with animation start
        StartCoroutine(DelayedElectricArc(attackAnimationType, currentParticlePoint));
    }
    
    private System.Collections.IEnumerator DelayedElectricArc(int attackAnimationType, GameObject currentParticlePoint)
    {
        // Wait for animation to start
        yield return new WaitForSeconds(lightningAnimationDelay);
        
        // Trigger lightning spark effect at the SSParticle point
        if (lightningSparkPrefab != null && currentParticlePoint != null)
        {
            GameObject sparkEffect = Instantiate(lightningSparkPrefab, currentParticlePoint.transform.position, Quaternion.identity);
            Debug.Log($"Lightning spark effect triggered at {currentParticlePoint.name}");
            
            // Auto-destroy the particle effect after a reasonable time
            Destroy(sparkEffect, 3f);
        }
        
        // Find nearest enemy within range
        GameObject nearestEnemy = FindNearestEnemy(lightningRange);
        if (nearestEnemy == null)
        {
            yield break;
        }
        
        Vector3 startPos = currentParticlePoint.transform.position;
        Vector3 endPos = nearestEnemy.transform.position;
        
        // Check for obstacles between player and enemy
        if (IsPathBlocked(startPos, endPos))
        {
            yield break;
        }
        
        StartCoroutine(CreateLightningArc(startPos, endPos, nearestEnemy));
    }
    
    private void CreateLightningBolt()
    {
        if (playerTransform == null) return;
        
        // Trigger ultimate attack animation (Type 2)
        TriggerAttackAnimation(2, boltDuration);
        
        // Add small delay to sync with animation start
        StartCoroutine(DelayedLightningBolt());
    }
    
    private System.Collections.IEnumerator DelayedLightningBolt()
    {
        // Wait for animation to start
        yield return new WaitForSeconds(boltAnimationDelay);
        
        // Find nearest enemy within bolt range
        GameObject nearestEnemy = FindNearestEnemy(boltRange);
        if (nearestEnemy == null)
        {
            yield break;
        }
        
        Vector3 skyPosition = new Vector3(playerTransform.position.x, playerTransform.position.y + boltHeight, 0);
        Vector3 groundPosition = new Vector3(nearestEnemy.transform.position.x, nearestEnemy.transform.position.y - 0.5f, 0); // Slightly below enemy to touch ground
        
        // Check if ground position is blocked by terrain
        if (IsGroundBlocked(groundPosition))
        {
            yield break;
        }
        
        StartCoroutine(CreateSkyBolt(skyPosition, groundPosition, nearestEnemy));
    }
    
    private GameObject FindNearestEnemy(float maxRange)
    {
        GameObject nearest = null;
        float nearestDistance = float.MaxValue;
        
        // Find all objects with EnemyBehavior component
        EnemyBehavior[] enemyBehaviors = FindObjectsByType<EnemyBehavior>(FindObjectsSortMode.None);
        
        foreach (EnemyBehavior enemyBehavior in enemyBehaviors)
        {
            if (enemyBehavior == null || enemyBehavior.gameObject == null) continue;
            
            // Skip if enemy is dead
            if (enemyBehavior.IsDead) continue;
            
            float distance = Vector3.Distance(playerTransform.position, enemyBehavior.transform.position);
            if (distance <= maxRange && distance < nearestDistance)
            {
                nearest = enemyBehavior.gameObject;
                nearestDistance = distance;
            }
        }
        
        // Also check DragonBoss components
        DragonBoss[] dragonBosses = FindObjectsByType<DragonBoss>(FindObjectsSortMode.None);
        
        foreach (DragonBoss dragonBoss in dragonBosses)
        {
            if (dragonBoss == null || dragonBoss.gameObject == null) continue;
            
            // Skip if dragon is dead
            if (dragonBoss.IsDead) continue;
            
            float distance = Vector3.Distance(playerTransform.position, dragonBoss.transform.position);
            if (distance <= maxRange && distance < nearestDistance)
            {
                nearest = dragonBoss.gameObject;
                nearestDistance = distance;
            }
        }
        
        return nearest;
    }
    
    private List<GameObject> FindNearbyEnemiesForChaining(GameObject attackedEnemy, float maxRange, int maxCount)
    {
        List<GameObject> nearbyEnemies = new List<GameObject>();
        if (attackedEnemy == null) return nearbyEnemies;
        
        // Find all objects with EnemyBehavior component
        EnemyBehavior[] enemyBehaviors = FindObjectsByType<EnemyBehavior>(FindObjectsSortMode.None);
        
        List<GameObject> candidates = new List<GameObject>();
        
        foreach (EnemyBehavior enemyBehavior in enemyBehaviors)
        {
            if (enemyBehavior == null || enemyBehavior.gameObject == null) continue;
            
            // Skip if enemy is dead
            if (enemyBehavior.IsDead) continue;
            
            // Skip the already attacked enemy
            if (enemyBehavior.gameObject == attackedEnemy) continue;
            
            float distance = Vector3.Distance(attackedEnemy.transform.position, enemyBehavior.transform.position);
            if (distance <= maxRange)
            {
                candidates.Add(enemyBehavior.gameObject);
            }
        }
        
        // Also check DragonBoss components for chaining
        DragonBoss[] dragonBosses = FindObjectsByType<DragonBoss>(FindObjectsSortMode.None);
        
        foreach (DragonBoss dragonBoss in dragonBosses)
        {
            if (dragonBoss == null || dragonBoss.gameObject == null) continue;
            
            // Skip if dragon is dead
            if (dragonBoss.IsDead) continue;
            
            // Skip the already attacked dragon
            if (dragonBoss.gameObject == attackedEnemy) continue;
            
            float distance = Vector3.Distance(attackedEnemy.transform.position, dragonBoss.transform.position);
            if (distance <= maxRange)
            {
                candidates.Add(dragonBoss.gameObject);
            }
        }
        
        // Sort by distance and take closest ones
        candidates.Sort((a, b) => {
            float distA = Vector3.Distance(attackedEnemy.transform.position, a.transform.position);
            float distB = Vector3.Distance(attackedEnemy.transform.position, b.transform.position);
            return distA.CompareTo(distB);
        });
        
        // Take up to maxCount enemies
        int count = Mathf.Min(maxCount, candidates.Count);
        for (int i = 0; i < count; i++)
        {
            nearbyEnemies.Add(candidates[i]);
        }
        
        return nearbyEnemies;
    }
    
    private IEnumerator CreateLightningArc(Vector3 startPos, Vector3 endPos, GameObject target)
    {
        // Create lightning arc visual with outer glow
        GameObject lightning = new GameObject("ElectricArc");
        lightning.transform.position = startPos;
        
        // Create glow (background) LineRenderer first
        LineRenderer glowRenderer = lightning.AddComponent<LineRenderer>();
        Material glowMaterial = CreateLightningGlowMaterial();
        glowRenderer.material = glowMaterial;
        glowRenderer.startWidth = 0.3f; // Much wider for glow effect
        glowRenderer.endWidth = 0.2f;
        glowRenderer.positionCount = 10;
        glowRenderer.sortingLayerName = "Player";
        glowRenderer.sortingOrder = 0;
        
        // Create main (foreground) LineRenderer
        GameObject mainLightning = new GameObject("MainElectricArc");
        mainLightning.transform.SetParent(lightning.transform);
        mainLightning.transform.localPosition = Vector3.zero;
        
        LineRenderer lineRenderer = mainLightning.AddComponent<LineRenderer>();
        lineRenderer.material = CreateLightningMaterial();
        lineRenderer.startWidth = 0.1f;
        lineRenderer.endWidth = 0.05f;
        lineRenderer.positionCount = 10; // More points for bending effect
        lineRenderer.sortingLayerName = "Player";
        lineRenderer.sortingOrder = 0;
        
        // Create bending arc points
        Vector3[] arcPoints = CreateBendingArc(startPos, endPos, 10);
        lineRenderer.SetPositions(arcPoints);
        glowRenderer.SetPositions(arcPoints); // Use same path for glow
        
        // Deal damage to target
        EnemyBehavior enemyBehavior = target.GetComponent<EnemyBehavior>();
        if (enemyBehavior != null)
        {
            enemyBehavior.TakeDamage(lightningDamage);
            
            // Trigger lightning hit effect at enemy position
            if (lightningHitPrefab != null)
            {
                GameObject hitEffect = Instantiate(lightningHitPrefab, target.transform.position, Quaternion.identity);
                Debug.Log($"Lightning hit effect triggered at enemy: {target.name}");
                
                // Auto-destroy the particle effect after a reasonable time
                Destroy(hitEffect, 3f);
            }
            
            // Trigger chain lightning from this enemy
            StartCoroutine(TriggerChainLightning(target, lightningDamage));
        }
        
        // Lightning visual effect duration
        yield return new WaitForSeconds(lightningDuration);
        
        Destroy(lightning);
    }
    
    private IEnumerator CreateChainArc(Vector3 startPos, Vector3 endPos, GameObject target, int chainDamage)
    {
        // Create chain lightning arc visual with outer glow
        GameObject chainLightning = new GameObject("ChainElectricArc");
        chainLightning.transform.position = startPos;
        
        // Create glow (background) LineRenderer for chain arc
        LineRenderer chainGlowRenderer = chainLightning.AddComponent<LineRenderer>();
        Material chainGlowMaterial = CreateLightningGlowMaterial();
        
        // Modify glow for purple chain lightning
        if (chainGlowMaterial.HasProperty("_Color"))
        {
            chainGlowMaterial.SetColor("_Color", new Color(0.2f, 0.1f, 0.4f, 0.25f)); // Purple glow
            chainGlowMaterial.SetColor("_EmissionColor", new Color(0.4f, 0.2f, 0.8f, 0.3f)); // Purple emission
        }
        else
        {
            chainGlowMaterial.color = new Color(0.3f, 0.2f, 0.8f, 0.2f); // Fallback purple glow
        }
        
        chainGlowRenderer.material = chainGlowMaterial;
        chainGlowRenderer.startWidth = 0.24f; // Wider glow for chain
        chainGlowRenderer.endWidth = 0.16f;
        chainGlowRenderer.positionCount = 8;
        chainGlowRenderer.sortingLayerName = "Player";
        chainGlowRenderer.sortingOrder = 0;
        
        // Create main chain LineRenderer
        GameObject mainChain = new GameObject("MainChainArc");
        mainChain.transform.SetParent(chainLightning.transform);
        mainChain.transform.localPosition = Vector3.zero;
        
        LineRenderer lineRenderer = mainChain.AddComponent<LineRenderer>();
        Material chainMaterial = CreateLightningMaterial();
        
        // Make chain lightning slightly more purple-blue to distinguish from main lightning
        if (chainMaterial.HasProperty("_Color"))
        {
            chainMaterial.SetColor("_Color", new Color(0.4f, 0.5f, 1f, 1f)); // More purple-blue
        }
        else
        {
            chainMaterial.color = new Color(0.4f, 0.7f, 1.2f, 0.9f); // Fallback purple-blue
        }
        
        lineRenderer.material = chainMaterial;
        lineRenderer.startWidth = 0.08f; // Slightly thinner than main lightning
        lineRenderer.endWidth = 0.04f;
        lineRenderer.positionCount = 8; // Fewer points for quicker creation
        lineRenderer.sortingLayerName = "Player";
        lineRenderer.sortingOrder = 0;
        
        // Create bending arc points for chain lightning
        Vector3[] arcPoints = CreateBendingArc(startPos, endPos, 8);
        lineRenderer.SetPositions(arcPoints);
        chainGlowRenderer.SetPositions(arcPoints); // Use same path for glow
        
        // Deal chain damage to target
        EnemyBehavior enemyBehavior = target.GetComponent<EnemyBehavior>();
        if (enemyBehavior != null)
        {
            enemyBehavior.TakeDamage(chainDamage);
            
            // Trigger lightning hit effect at enemy position for chain lightning
            if (lightningHitPrefab != null)
            {
                GameObject hitEffect = Instantiate(lightningHitPrefab, target.transform.position, Quaternion.identity);
                Debug.Log($"Chain lightning hit effect triggered at enemy: {target.name}");
                
                // Auto-destroy the particle effect after a reasonable time
                Destroy(hitEffect, 3f);
            }
            
            Debug.Log($"Chain lightning hit {target.name} for {chainDamage} damage");
        }
        
        // Chain arc visual effect duration
        yield return new WaitForSeconds(chainArcDuration);
        
        Destroy(chainLightning);
    }
    
    private IEnumerator TriggerChainLightning(GameObject attackedEnemy, int baseDamage)
    {
        if (attackedEnemy == null) yield break;
        
        // Find nearby enemies within chain range
        List<GameObject> nearbyEnemies = FindNearbyEnemiesForChaining(attackedEnemy, chainRange, maxChainArcs);
        
        if (nearbyEnemies.Count == 0)
        {
            Debug.Log("No nearby enemies for chain lightning");
            yield break;
        }
        
        Debug.Log($"Chain lightning: Found {nearbyEnemies.Count} nearby enemies");
        
        // Calculate chain damage
        int chainDamage = Mathf.RoundToInt(baseDamage * chainDamageMultiplier);
        
        // Create chain arcs with delays
        for (int i = 0; i < nearbyEnemies.Count; i++)
        {
            GameObject targetEnemy = nearbyEnemies[i];
            if (targetEnemy != null)
            {
                Vector3 startPos = attackedEnemy.transform.position;
                Vector3 endPos = targetEnemy.transform.position;
                
                // Start chain arc creation
                StartCoroutine(CreateChainArc(startPos, endPos, targetEnemy, chainDamage));
                
                // Generate ultimate charge for chain arc
                GenerateUltimateCharge(stormConstantLeftClickCharge * 0.5f); // Half charge for chain arcs
                
                // Wait before next chain arc
                if (i < nearbyEnemies.Count - 1) // Don't wait after the last arc
                {
                    yield return new WaitForSeconds(chainDelay);
                }
            }
        }
    }
    
    private IEnumerator CreateSkyBolt(Vector3 startPos, Vector3 endPos, GameObject target)
    {
        // Create lightning bolt from sky with outer glow
        GameObject bolt = new GameObject("LightningBolt");
        bolt.transform.position = startPos;
        
        // Create glow (background) LineRenderer for sky bolt
        LineRenderer boltGlowRenderer = bolt.AddComponent<LineRenderer>();
        Material boltGlowMaterial = CreateLightningGlowMaterial();
        boltGlowRenderer.material = boltGlowMaterial;
        boltGlowRenderer.startWidth = 0.5f; // Much wider glow for dramatic effect
        boltGlowRenderer.endWidth = 0.3f;
        boltGlowRenderer.positionCount = 6;
        boltGlowRenderer.sortingLayerName = "Player";
        boltGlowRenderer.sortingOrder = 0;
        
        // Create main lightning bolt LineRenderer
        GameObject mainBolt = new GameObject("MainLightningBolt");
        mainBolt.transform.SetParent(bolt.transform);
        mainBolt.transform.localPosition = Vector3.zero;
        
        LineRenderer lineRenderer = mainBolt.AddComponent<LineRenderer>();
        lineRenderer.material = CreateLightningMaterial();
        lineRenderer.startWidth = 0.2f;
        lineRenderer.endWidth = 0.1f;
        lineRenderer.positionCount = 6; // More points for subtle bending
        lineRenderer.sortingLayerName = "Player";
        lineRenderer.sortingOrder = 0;
        
        // Create slightly squiggly lightning bolt path
        Vector3[] boltPoints = CreateBendingBolt(startPos, endPos, 6);
        lineRenderer.SetPositions(boltPoints);
        boltGlowRenderer.SetPositions(boltPoints); // Use same path for glow
        
        // Deal damage to target
        EnemyBehavior enemyBehavior = target.GetComponent<EnemyBehavior>();
        if (enemyBehavior != null)
        {
            enemyBehavior.TakeDamage(boltDamage);
            
            // Trigger lightning hit effect at enemy position for lightning bolt
            if (lightningHitPrefab != null)
            {
                GameObject hitEffect = Instantiate(lightningHitPrefab, target.transform.position, Quaternion.identity);
                Debug.Log($"Lightning bolt hit effect triggered at enemy: {target.name}");
                
                // Auto-destroy the particle effect after a reasonable time
                Destroy(hitEffect, 3f);
            }
            
            // Trigger chain lightning from this enemy
            StartCoroutine(TriggerChainLightning(target, boltDamage));
        }
        
        // Create ground impact effect
        GameObject impact = new GameObject("BoltImpact");
        impact.transform.position = endPos;
        
        // Add impact damage area
        BoxCollider2D impactCollider = impact.AddComponent<BoxCollider2D>();
        impactCollider.size = Vector2.one * 2f; // 2x2 impact area
        impactCollider.isTrigger = true;
        
        DamageObject impactDamage = impact.AddComponent<DamageObject>();
        impactDamage.damageAmount = playerMovement.GetModifiedMagicDamage(boltDamage / 2); // Half damage for impact area
        impactDamage.damageRate = 0.1f;
        
        // Configure impact damage
        var excludeField = typeof(DamageObject).GetField("excludePlayerLayer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (excludeField != null)
        {
            excludeField.SetValue(impactDamage, true);
        }
        
        var enemyDamageField = typeof(DamageObject).GetField("canDamageEnemies", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (enemyDamageField != null)
        {
            enemyDamageField.SetValue(impactDamage, true);
        }
        
        // Visual impact effect (animated lightning blast or sprite)
        if (lightningBlastPrefab != null)
        {
            // Use animated prefab - instantiate it as child of the impact object
            GameObject animatedBlast = Instantiate(lightningBlastPrefab, impact.transform);
            animatedBlast.transform.localPosition = Vector3.zero;
            animatedBlast.transform.localScale = Vector3.one;
            
            // Trigger the lightning blast animation
            Animator blastAnimator = animatedBlast.GetComponent<Animator>();
            if (blastAnimator != null)
            {
                blastAnimator.SetTrigger("StartBlast");
            }
        }
        else
        {
            // Fallback: Use sprite renderer for static sprite
            SpriteRenderer impactRenderer = impact.AddComponent<SpriteRenderer>();
            
            if (lightningBlastSprite != null)
            {
                impactRenderer.sprite = lightningBlastSprite;
            }
            else
        {
            // Fallback: Create texture scaled to impact area
            int impactTextureSize = Mathf.RoundToInt(2f * 80);
            Texture2D impactTexture = new Texture2D(impactTextureSize, impactTextureSize);
            Color[] pixels = new Color[impactTextureSize * impactTextureSize];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color(1f, 1f, 0f, 0.8f); // Yellow impact
            }
            impactTexture.SetPixels(pixels);
            impactTexture.Apply();
            impactRenderer.sprite = Sprite.Create(impactTexture, new Rect(0, 0, impactTextureSize, impactTextureSize), Vector2.one * 0.5f);
            }
            impactRenderer.sortingLayerName = "Player";
            impactRenderer.sortingOrder = 0;
        }
        
        // Lightning and impact duration (using serialized parameters)
        yield return new WaitForSeconds(lightningDuration);
        
        Destroy(bolt);
        
        // Impact lasts for bolt duration
        yield return new WaitForSeconds(boltDuration);
        
        Destroy(impact);
    }
    
    private Vector3[] CreateBendingArc(Vector3 start, Vector3 end, int pointCount)
    {
        Vector3[] points = new Vector3[pointCount];
        
        for (int i = 0; i < pointCount; i++)
        {
            float t = (float)i / (pointCount - 1);
            
            // Linear interpolation between start and end
            Vector3 basePoint = Vector3.Lerp(start, end, t);
            
            // Create much more dramatic bending effect
            float bendAmount = Mathf.Sin(t * Mathf.PI) * 2f; // Increased from 0.5f to 2f
            
            // Add both perpendicular and random offsets for more chaotic lightning
            Vector3 direction = (end - start).normalized;
            Vector3 perpendicular = new Vector3(-direction.y, direction.x, 0);
            
            // Create multiple bend points for more chaotic effect
            float chaos1 = Mathf.Sin(t * Mathf.PI * 3f) * bendAmount * 0.8f;
            float chaos2 = Mathf.Sin(t * Mathf.PI * 5f) * bendAmount * 0.4f;
            float randomBend = Random.Range(-0.5f, 0.5f) * bendAmount;
            
            Vector3 bendOffset = perpendicular * (chaos1 + chaos2 + randomBend);
            
            // Add some vertical chaos too
            Vector3 verticalOffset = Vector3.up * Random.Range(-0.3f, 0.3f) * bendAmount;
            
            points[i] = basePoint + bendOffset + verticalOffset;
        }
        
        return points;
    }
    
    private Vector3[] CreateBendingBolt(Vector3 start, Vector3 end, int pointCount)
    {
        Vector3[] points = new Vector3[pointCount];
        
        for (int i = 0; i < pointCount; i++)
        {
            float t = (float)i / (pointCount - 1);
            
            // Linear interpolation between start and end
            Vector3 basePoint = Vector3.Lerp(start, end, t);
            
            // Add subtle bending effect (much less than arc)
            float bendAmount = Mathf.Sin(t * Mathf.PI) * 0.8f; // Moderate bending
            
            // Add perpendicular offset for slight zigzag
            Vector3 direction = (end - start).normalized;
            Vector3 perpendicular = new Vector3(-direction.y, direction.x, 0);
            
            // Create subtle zigzag pattern
            float zigzag = Mathf.Sin(t * Mathf.PI * 4f) * bendAmount * 0.3f; // Gentle zigzag
            float randomOffset = Random.Range(-0.1f, 0.1f) * bendAmount; // Small random variation
            
            Vector3 bendOffset = perpendicular * (zigzag + randomOffset);
            
            points[i] = basePoint + bendOffset;
        }
        
        return points;
    }
    
    private Material CreateLightningMaterial()
    {
        // Use custom lightning arc material if provided, otherwise create glowing shader
        if (lightningArcMaterial != null)
        {
            return lightningArcMaterial;
        }
        
        // Create a custom glowing lightning shader programmatically
        return CreateGlowingLightningShader();
    }
    
    private Material CreateGlowingLightningShader()
    {
        // Try to find our custom glowing lightning shader
        Shader glowShader = Shader.Find("Custom/GlowingLightning");
        if (glowShader != null)
        {
            Material glowMat = new Material(glowShader);
            
            // Set up the glowing blue lightning properties
            glowMat.SetColor("_Color", new Color(0.2f, 0.6f, 1f, 1f)); // Base blue color
            glowMat.SetColor("_EmissionColor", new Color(0.3f, 0.8f, 2f, 1f)); // Bright blue emission
            glowMat.SetFloat("_GlowIntensity", 4f); // Strong glow
            glowMat.SetFloat("_PulseSpeed", 3f); // Fast pulsing
            glowMat.SetFloat("_Width", 1.2f); // Line width
            
            Debug.Log("Using Custom/GlowingLightning shader for lightning arcs");
            return glowMat;
        }
        else
        {
            Debug.LogWarning("Custom/GlowingLightning shader not found, using fallback glow material");
            return CreateSimpleGlowMaterial();
        }
    }
    
    private Material CreateSimpleGlowMaterial()
    {
        // Try different shaders for better glow effect
        Shader shader = Shader.Find("UI/Default") ?? Shader.Find("Unlit/Transparent") ?? Shader.Find("Sprites/Default");
        Material glowMat = new Material(shader);
        
        // Set bright electric blue color with higher intensity
        glowMat.color = new Color(0.3f, 0.8f, 2.5f, 0.9f); // Very bright blue
        
        // Configure for additive blending to create glow effect
        if (glowMat.HasProperty("_SrcBlend"))
            glowMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (glowMat.HasProperty("_DstBlend"))
            glowMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One); // Additive blending
        if (glowMat.HasProperty("_ZWrite"))
            glowMat.SetInt("_ZWrite", 0);
        
        glowMat.renderQueue = 3000; // Transparent queue for proper sorting
        
        Debug.Log("Using fallback glow material for lightning arcs");
        return glowMat;
    }
    
    private Material CreateLightningGlowMaterial()
    {
        // Try to find our custom glowing lightning shader for the outer glow
        Shader glowShader = Shader.Find("Custom/GlowingLightning");
        if (glowShader != null)
        {
            Material glowMat = new Material(glowShader);
            
            // Set up outer glow properties - softer, more transparent
            glowMat.SetColor("_Color", new Color(0.1f, 0.3f, 0.6f, 0.3f)); // Dim blue base
            glowMat.SetColor("_EmissionColor", new Color(0.2f, 0.5f, 1.5f, 0.4f)); // Soft blue glow
            glowMat.SetFloat("_GlowIntensity", 2f); // Moderate glow for outer effect
            glowMat.SetFloat("_PulseSpeed", 2f); // Sync with main lightning
            glowMat.SetFloat("_Width", 2f); // Wider falloff for glow
            
            return glowMat;
        }
        else
        {
            // Fallback glow material
            return CreateSimpleOuterGlowMaterial();
        }
    }
    
    private Material CreateSimpleOuterGlowMaterial()
    {
        // Create a soft glow material using built-in shaders
        Shader shader = Shader.Find("UI/Default") ?? Shader.Find("Unlit/Transparent") ?? Shader.Find("Sprites/Default");
        Material glowMat = new Material(shader);
        
        // Set soft, transparent blue glow
        glowMat.color = new Color(0.2f, 0.4f, 1.2f, 0.25f); // Very transparent bright blue
        
        // Configure for additive blending with transparency
        if (glowMat.HasProperty("_SrcBlend"))
            glowMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (glowMat.HasProperty("_DstBlend"))
            glowMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One); // Additive
        if (glowMat.HasProperty("_ZWrite"))
            glowMat.SetInt("_ZWrite", 0);
        
        glowMat.renderQueue = 2999; // Just before main lightning
        
        return glowMat;
    }
    
    private bool IsPathBlocked(Vector3 startPos, Vector3 endPos)
    {
        // Check for colliders between start and end positions
        // Using LayerMask to check for ground/terrain layers
        int groundLayerMask = LayerMask.GetMask("Ground", "Default"); // Adjust layer names as needed
        
        RaycastHit2D hit = Physics2D.Linecast(startPos, endPos, groundLayerMask);
        if (hit.collider != null)
        {
            // Check if it's a CompositeCollider2D (common for tilemaps)
            CompositeCollider2D compositeCollider = hit.collider.GetComponent<CompositeCollider2D>();
            if (compositeCollider != null)
            {
                return true;
            }
            
            // Also check for any other ground colliders
            if (hit.collider.gameObject.name.ToLower().Contains("ground") || 
                hit.collider.gameObject.name.ToLower().Contains("terrain") ||
                hit.collider.gameObject.name.ToLower().Contains("tilemap"))
            {
                return true;
            }
        }
        
        return false;
    }
    
    private bool IsGroundBlocked(Vector3 groundPos)
    {
        // Check if the ground position is inside a collider
        int groundLayerMask = LayerMask.GetMask("Ground", "Default");
        
        // Check point collision
        Collider2D groundCollider = Physics2D.OverlapPoint(groundPos, groundLayerMask);
        if (groundCollider != null)
        {
            // Check if it's a CompositeCollider2D
            CompositeCollider2D compositeCollider = groundCollider.GetComponent<CompositeCollider2D>();
            if (compositeCollider != null)
            {
                return true;
            }
            
            // Check for ground/terrain objects
            if (groundCollider.gameObject.name.ToLower().Contains("ground") || 
                groundCollider.gameObject.name.ToLower().Contains("terrain") ||
                groundCollider.gameObject.name.ToLower().Contains("tilemap"))
            {
                return true;
            }
        }
        
        return false;
    }
    
    private void UpdateSlotDisplay(int slotIndex)
    {
        ShardType shardType = equippedShards[slotIndex];
        
        if (shardType != ShardType.None && shardSprites.ContainsKey(shardType))
        {
            slotImages[slotIndex].sprite = shardSprites[shardType];
            slotImages[slotIndex].color = Color.white;
        }
        else
        {
            slotImages[slotIndex].sprite = null;
            slotImages[slotIndex].color = Color.clear;
        }
    }
    
    private void UpdateActiveSlotIndicator()
    {
        if (activeSlotIndicator != null)
        {
            RectTransform indicatorRect = activeSlotIndicator.GetComponent<RectTransform>();
            indicatorRect.anchoredPosition = new Vector2(-(activeSlotIndex * (slotSize.x + slotSpacing)), 0);
        }
    }
    
    private void UpdateSwapTargetIndicator()
    {
        if (swapTargetIndicator != null)
        {
            RectTransform indicatorRect = swapTargetIndicator.GetComponent<RectTransform>();
            indicatorRect.anchoredPosition = new Vector2(-(swapTargetSlot * (slotSize.x + slotSpacing)), 0);
        }
    }
    
    private void ShowSwapTargetIndicator()
    {
        if (swapTargetIndicator != null)
        {
            swapTargetIndicator.gameObject.SetActive(true);
            UpdateSwapTargetIndicator();
        }
    }
    
    private void HideSwapTargetIndicator()
    {
        if (swapTargetIndicator != null)
        {
            swapTargetIndicator.gameObject.SetActive(false);
        }
    }
    
    private bool HasShardEquipped(ShardType shardType)
    {
        return equippedShards[0] == shardType || equippedShards[1] == shardType;
    }
    
    private int GetEmptySlotIndex()
    {
        for (int i = 0; i < equippedShards.Length; i++)
        {
            if (equippedShards[i] == ShardType.None)
                return i;
        }
        return -1; // No empty slots
    }
    
    // Public method to check if weapon menu is open (for PlayerMovement to disable movement)
    public bool IsWeaponMenuOpen()
    {
        return isWeaponMenuOpen;
    }
    
    // Public method to get active weapon type
    public string GetActiveWeaponName()
    {
        ShardType activeShard = equippedShards[activeSlotIndex];
        return activeShard != ShardType.None ? activeShard.ToString() : "None";
    }
    
    private void HandleLeftClickInput(ShardType activeWeapon)
    {
        // For ValorShard, allow additional clicks during basic attack execution to enable multi-click combos
        // Only block clicks during special attacks that shouldn't be interrupted
        if (activeWeapon == ShardType.ValorShard)
        {
            // Allow clicks during basic attacks for multi-click system, but block during special attacks
            if (isPerformingSpecialAttack || isPerformingThrust)
                return;
        }
        else
        {
            // For other shards, don't process clicks if already performing any attack
            if (isPerformingSpecialAttack || isPerformingThrust || isPerformingBasicAttack)
                return;
        }
            
        // Handle multi-click detection for ValorShard
        if (activeWeapon == ShardType.ValorShard)
        {
            float currentTime = Time.time;
            
            // Check if we should start a new sequence or continue existing one
            bool shouldStartNewSequence = (clickCount == 0 || currentTime - firstClickTime > multiClickWindow);
            
            Debug.Log($"Click timing debug - clickCount: {clickCount}, timeSinceFirst: {currentTime - firstClickTime:F3}, multiClickWindow: {multiClickWindow}, shouldStartNew: {shouldStartNewSequence}");
            
            if (shouldStartNewSequence)
            {
                // Start new sequence
                clickCount = 1;
                firstClickTime = currentTime;
                attackQueue.Clear(); // Clear any previous queue
                
                Debug.Log($"First click registered - clickCount: {clickCount}");
                
                // Queue the first attack
                attackQueue.Enqueue(1); // 1 = basic attack
                
                // Start the sequential attack system
                StartAttackQueueProcessing();
                StartCoroutine(CheckForAdditionalClicks());
            }
            else
            {
                // Continue existing sequence
                clickCount++;
                Debug.Log($"Additional click registered - clickCount: {clickCount}, timeDiff: {currentTime - firstClickTime:F3}");
                
                if (clickCount == 2)
                {
                    // Double click detected - queue dash attack
                    attackQueue.Enqueue(2); // 2 = dash attack
                    Debug.Log("Double click detected - queued dash attack");
                    
                    // Apply Valor Shard passive buffs for double-click
                    ApplyDoubleClickBuffs();
                    
                    // Ensure queue processing is active
                    EnsureAttackQueueProcessing();
                }
                else if (clickCount == 3)
                {
                    // Triple click detected - queue thrust attack
                    attackQueue.Enqueue(3); // 3 = thrust attack
                    Debug.Log("Triple click detected - queued thrust attack");
                    
                    // Apply Valor Shard passive buffs for triple-click
                    ApplyTripleClickBuffs();
                    
                    // Ensure queue processing is active
                    EnsureAttackQueueProcessing();
                }
            }
        }
        else
        {
            // Standard attack for other weapons
            UseActiveWeapon(false);
            
            // Start auto-fire for Storm Shard
            if (activeWeapon == ShardType.StormShard)
            {
                isAutoFiring = true;
                autoFireAttackType = 0; // Start with attack type 0
                nextAutoFireTime = Time.time + attackType1Interval; // Use type 1 interval for first shot
            }
        }
    }
    
    private System.Collections.IEnumerator ProcessAttackQueue()
    {
        while (attackQueue.Count > 0)
        {
            int attackType = attackQueue.Dequeue();
            Debug.Log($"Processing queued attack type: {attackType}");
            
            switch (attackType)
            {
                case 1: // Basic attack
                    yield return StartCoroutine(PerformBasicValorAttack());
                    break;
                case 2: // Dash attack
                    yield return StartCoroutine(PerformValorDashAttackSequential());
                    break;
                case 3: // Thrust attack
                    yield return StartCoroutine(PerformValorThrustAttackSequential());
                    break;
            }
            
            Debug.Log($"Completed attack type: {attackType}. Remaining in queue: {attackQueue.Count}");
        }
        
        Debug.Log("Attack queue processing complete");
        attackQueueProcessor = null; // Clear the reference when processing is complete
    }
    
    private void StartAttackQueueProcessing()
    {
        if (attackQueueProcessor == null)
        {
            Debug.Log("Starting attack queue processing");
            attackQueueProcessor = StartCoroutine(ProcessAttackQueue());
        }
    }
    
    private void EnsureAttackQueueProcessing()
    {
        if (attackQueueProcessor == null && attackQueue.Count > 0)
        {
            Debug.Log("Restarting attack queue processing for additional attacks");
            attackQueueProcessor = StartCoroutine(ProcessAttackQueue());
        }
    }
    
    private void ClearAttackQueue()
    {
        if (attackQueueProcessor != null)
        {
            StopCoroutine(attackQueueProcessor);
            attackQueueProcessor = null;
        }
        attackQueue.Clear();
        clickCount = 0;
        isPerformingBasicAttack = false;
        Debug.Log("Attack queue cleared");
    }

    private System.Collections.IEnumerator PerformBasicValorAttack()
    {
        isPerformingBasicAttack = true;
        Debug.Log("Starting basic valor attack");
        
        // Trigger basic sword attack
        CreateSwordAttack(0); // Attack type 0 for basic attack
        
        // Wait for the attack duration plus a small buffer
        yield return new WaitForSeconds(swordDuration + 0.1f);
        
        isPerformingBasicAttack = false;
        Debug.Log("Basic valor attack completed");
    }
    
    private System.Collections.IEnumerator PerformValorDashAttackSequential()
    {
        Debug.Log("Starting sequential dash attack");
        
        // Use the existing dash attack but wait for completion
        PerformValorDashAttack();
        
        // Wait for dash movement and sword attack to complete
        yield return new WaitForSeconds(dashMovementDisableDuration + swordDuration + 0.2f);
        
        Debug.Log("Sequential dash attack completed");
    }
    
    private System.Collections.IEnumerator PerformValorThrustAttackSequential()
    {
        Debug.Log("Starting sequential thrust attack");
        
        // Use the existing thrust attack but wait for completion
        PerformValorThrustAttack();
        
        // Wait for the full thrust attack to complete
        yield return new WaitForSeconds(thrustDuration + 0.1f);
        
        // Wait until thrust attack flags are cleared
        while (isPerformingThrust || isPerformingSpecialAttack)
        {
            yield return new WaitForSeconds(0.1f);
        }
        
        Debug.Log("Sequential thrust attack completed");
    }

    private System.Collections.IEnumerator DelayedDashAttack()
    {
        // Wait a tiny bit to ensure first attack is visually distinct
        yield return new WaitForSeconds(0.1f);
        PerformValorDashAttack();
    }

    private System.Collections.IEnumerator CheckForAdditionalClicks()
    {
        yield return new WaitForSeconds(multiClickWindow);
        
        // Only reset click count if we're still at 1 (no additional clicks were registered)
        if (clickCount == 1)
        {
            Debug.Log("Resetting clickCount from CheckForAdditionalClicks - no additional clicks detected");
            clickCount = 0;
        }
        else
        {
            Debug.Log($"Not resetting clickCount from CheckForAdditionalClicks - clickCount is {clickCount}");
        }
    }
    
    private System.Collections.IEnumerator DelayedClickReset()
    {
        // Wait a bit longer than multiClickWindow to allow for third click
        yield return new WaitForSeconds(multiClickWindow + 0.1f);
        
        // Reset click count if no third click was registered
        if (clickCount < 3)
        {
            clickCount = 0;
        }
    }
    

    
    private void PerformValorDashAttack()
    {
        // Don't set isPerformingSpecialAttack immediately - allow third click detection
        // Don't reset clickCount here - let normal timing handle it
        
        // Get facing direction - use multiple methods to determine direction
        bool facingRight = GetActualFacingDirection();
        Vector2 dashDirection = facingRight ? Vector2.right : Vector2.left;
        Debug.Log("=== NEW DIRECTION DETECTION SYSTEM ACTIVE ===");
        Debug.Log($"Facing Debug - ActualFacing: {facingRight}, IsFacingRight: {playerMovement.IsFacingRight()}, Direction Vector: {dashDirection}");
        
        // Temporarily disable player movement to let physics take over
        StartCoroutine(TemporarilyDisableMovement());
        
        // Apply forward dash with direct velocity
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            // Calculate dash velocity (reduced force)
            Vector2 dashVelocity = new Vector2(dashDirection.x * dashForce * 1.2f, dashForce * 0.2f);
            
            // Set velocity directly instead of adding force
            rb.linearVelocity = dashVelocity;
            
            Debug.Log($"Dash Debug - Direction: {dashDirection}, DashForce: {dashForce}, SetVelocity: {dashVelocity}, RB Mass: {rb.mass}");
            Debug.Log($"Dash Debug - RB Constraints: {rb.constraints}, Gravity Scale: {rb.gravityScale}, Drag: {rb.linearDamping}");
            
            // Check velocity after setting it
            StartCoroutine(CheckDashVelocity(rb));
        }
        else
        {
            Debug.LogError("No Rigidbody2D found for dash attack!");
        }
        
        Debug.Log("ValorShard: Dash attack performed!");
        
        // Generate ultimate charge for valor double-click dash attack (should be the triple click charge since dash leads to triple)
        // Actually this is the second click, so no charge here - charge comes from the sword attacks
        
        // Perform sword attack after brief delay
        StartCoroutine(PerformDashSwordAttack());
    }
    
    private System.Collections.IEnumerator PerformDashSwordAttack()
    {
        // Set performing special attack after a tiny delay to allow third click detection
        yield return new WaitForSeconds(dashDelayBeforeAttack);
        isPerformingSpecialAttack = true;
        
        // Wait for a shorter dash duration for quicker sword swing
        yield return new WaitForSeconds(dashMovementDisableDuration); // Reduced delay for quicker response
        
        // Ensure player movement is re-enabled before sword attack
        if (!playerMovement.enabled)
        {
            yield return new WaitForSeconds(0.1f); // Extra wait if still disabled
        }
        
        // Perform sword attack with cooldown bypass and attack type 2
        Debug.Log("About to execute dash sword swing - player movement enabled: " + playerMovement.enabled);
        CreateSwordAttack(1); // Second click animation
        Debug.Log("Dash sword swing executed!");
        
        // End special attack
        isPerformingSpecialAttack = false;
    }
    
    private System.Collections.IEnumerator CheckDashVelocity(Rigidbody2D rb)
    {
        yield return new WaitForFixedUpdate(); // Wait one physics frame
        Debug.Log($"Dash Velocity Check - Immediately after force: {rb.linearVelocity}");
        
        yield return new WaitForSeconds(0.1f); // Wait a bit more
        Debug.Log($"Dash Velocity Check - After 0.1s: {rb.linearVelocity}");
        
        yield return new WaitForSeconds(0.1f); // Wait a bit more
        Debug.Log($"Dash Velocity Check - After 0.2s: {rb.linearVelocity}");
    }
    
    private System.Collections.IEnumerator TemporarilyDisableMovement()
    {
        // Temporarily disable player movement component to let physics take over
        bool wasEnabled = playerMovement.enabled;
        playerMovement.enabled = false;
        
        yield return new WaitForSeconds(0.3f); // Disable for dash duration
        
        // Re-enable movement
        playerMovement.enabled = wasEnabled;
        Debug.Log("Player movement re-enabled after dash");
    }
    
    private bool GetActualFacingDirection()
    {
        // Method 1: Check current input direction using New Input System
        if (UnityEngine.InputSystem.Keyboard.current != null)
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            {
                Debug.Log("Direction detected from input: RIGHT");
                return true;
            }
            else if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            {
                Debug.Log("Direction detected from input: LEFT");
                return false;
            }
        }
        
        // Method 2: Check sprite flip as backup
        SpriteRenderer playerSprite = GetComponent<SpriteRenderer>();
        if (playerSprite == null)
            playerSprite = GetComponentInChildren<SpriteRenderer>();
        
        if (playerSprite != null)
        {
            bool facingRight = !playerSprite.flipX;
            Debug.Log($"Direction detected from sprite flip: {(facingRight ? "RIGHT" : "LEFT")} (flipX: {playerSprite.flipX})");
            return facingRight;
        }
        
        // Method 3: Fallback to PlayerMovement method
        bool fallbackDirection = playerMovement.IsFacingRight();
        Debug.Log($"Direction detected from PlayerMovement fallback: {(fallbackDirection ? "RIGHT" : "LEFT")}");
        return fallbackDirection;
    }
    
    private void PerformValorThrustAttack()
    {
        isPerformingSpecialAttack = true;
        isPerformingThrust = true;
        clickCount = 0; // Reset click count (thrust is final attack, so safe to reset)
        thrustStartTime = Time.time;
        
        // Trigger thrust attack animation (Type 2)
        TriggerAttackAnimation(2, thrustDuration);
        
        Debug.Log("ValorShard: Sword thrust attack initiated!");
        
        // Generate ultimate charge for valor triple click attack (higher than basic attacks)
        GenerateUltimateCharge(valorTripleClickCharge);
        
        // Get facing direction - use multiple methods to determine direction
        bool facingRight = GetActualFacingDirection();
        Vector2 thrustDirection = facingRight ? Vector2.right : Vector2.left;
        Debug.Log("=== NEW DIRECTION DETECTION SYSTEM ACTIVE (THRUST) ===");
        Debug.Log($"Thrust Facing Debug - ActualFacing: {facingRight}, IsFacingRight: {playerMovement.IsFacingRight()}, Direction Vector: {thrustDirection}");
        
        // Apply horizontal dash force (no vertical component for thrust)
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            // Clear existing velocity and apply horizontal dash force
            rb.linearVelocity = Vector2.zero;
            Vector2 thrustVelocity = new Vector2(thrustDirection.x * thrustDashForce, 0f); // Only horizontal movement
            rb.linearVelocity = thrustVelocity;
            
            Debug.Log($"Thrust Debug - Direction: {thrustDirection}, ThrustVelocity: {thrustVelocity}, ThrustDashForce: {thrustDashForce}");
        }
        
        // Disable player movement during thrust (will be re-enabled in PerformThrustAnimation)
        if (playerMovement != null)
        {
            playerMovement.enabled = false;
            Debug.Log("Player movement disabled for thrust attack");
        }
        
        // Create thrust damage zone that will anchor enemies
        CreateThrustDamageZone();
        
        // Start thrust coroutine
        StartCoroutine(PerformThrustAnimation());
    }
    
    private System.Collections.IEnumerator PerformThrustAnimation()
    {
        float elapsedTime = 0f;
        Transform playerTransform = transform;
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        
        while (elapsedTime < thrustDuration)
        {
            elapsedTime += Time.deltaTime;
            
            // Update position of anchored enemies to follow the thrust
            UpdatePiercedEnemyPositions();
            
            yield return null;
        }
        
        // Stop thrust movement and re-enable player movement
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
        
        // IMPORTANT: Re-enable player movement after thrust dash ends
        if (playerMovement != null)
        {
            playerMovement.enabled = true;
            Debug.Log("Player movement re-enabled after thrust dash");
        }
        
        // Release pierced enemies after pierce duration
        yield return new WaitForSeconds(thrustPierceDuration - thrustDuration);
        
        // Clean up thrust attack
        ReleasePiercedEnemies();
        
        if (thrustDamageZone != null)
        {
            Destroy(thrustDamageZone);
            thrustDamageZone = null;
        }
        
        isPerformingThrust = false;
        isPerformingSpecialAttack = false;
        
        // Final safety check to ensure movement is enabled
        if (playerMovement != null && !playerMovement.enabled)
        {
            playerMovement.enabled = true;
            Debug.Log("Safety re-enabled player movement at end of thrust");
        }
        
        Debug.Log("ValorShard: Sword thrust attack completed!");
    }
    
    private void CreateThrustDamageZone()
    {
        // Create damage zone in front of the player for sword thrust
        GameObject damageZone = new GameObject("ValorThrustDamage");
        damageZone.transform.SetParent(transform);
        
        // Position damage zone in front of player based on facing direction
        bool facingRight = GetActualFacingDirection();
        Vector3 frontOffset = new Vector3(facingRight ? thrustDamageWidth * 0.5f : -thrustDamageWidth * 0.5f, 0, 0);
        damageZone.transform.localPosition = frontOffset;
        
        // Add collider
        BoxCollider2D collider = damageZone.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = new Vector2(thrustDamageWidth, thrustDamageHeight); // Use serialized thrust damage size
        
        // Add special thrust damage component that anchors enemies
        ThrustDamageObject thrustDamageComponent = damageZone.AddComponent<ThrustDamageObject>();
        thrustDamageComponent.damageAmount = playerMovement.GetModifiedMeleeDamage(thrustDamage); // Use serialized thrust damage
        thrustDamageComponent.damageRate = 0.1f; // Fast damage rate for thrust attack
        thrustDamageComponent.weaponController = this; // Reference to this controller for enemy anchoring
        
        // Use reflection to set the private excludePlayerLayer field
        var excludeField = typeof(DamageObject).GetField("excludePlayerLayer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (excludeField != null)
        {
            excludeField.SetValue(thrustDamageComponent, true);
        }
        
        // Use reflection to set the private canDamageEnemies field
        var enemyDamageField = typeof(DamageObject).GetField("canDamageEnemies", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (enemyDamageField != null)
        {
            enemyDamageField.SetValue(thrustDamageComponent, true);
        }
        
        // Exclude NPC and PlayerSummon layers to prevent damaging player summons/allies
        thrustDamageComponent.excludeLayers = LayerMask.GetMask("NPC", "PlayerSummon");
        
        // Visual indicator (semi-transparent gold for thrust attack)
        SpriteRenderer thrustRenderer = damageZone.AddComponent<SpriteRenderer>();
        
        // Create gold rectangle sprite for thrust attack (scaled to match collider)
        int textureWidth = Mathf.RoundToInt(thrustDamageWidth * 64); 
        int textureHeight = Mathf.RoundToInt(thrustDamageHeight * 64); 
        Texture2D thrustTexture = new Texture2D(textureWidth, textureHeight);
        Color[] pixels = new Color[textureWidth * textureHeight];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = new Color(1f, 0.8f, 0f, 0.7f); // Golden color for thrust attack
        }
        thrustTexture.SetPixels(pixels);
        thrustTexture.Apply();
        
        thrustRenderer.sprite = Sprite.Create(thrustTexture, new Rect(0, 0, textureWidth, textureHeight), Vector2.one * 0.5f);
        thrustRenderer.sortingLayerName = "Player";
        thrustRenderer.sortingOrder = 0;
        
        thrustDamageZone = damageZone;
        
        Debug.Log("Thrust damage zone created with damage: " + thrustDamageComponent.damageAmount);
    }
    
    // ===== THRUST ATTACK ENEMY ANCHORING SYSTEM =====
    
    /// <summary>
    /// Add an enemy to the pierced enemies list and disable their attacks
    /// </summary>
    public void AnchorEnemyToThrust(GameObject enemy)
    {
        if (!piercedEnemies.Contains(enemy))
        {
            piercedEnemies.Add(enemy);
            
            // Disable enemy attacks by finding and disabling enemy attack components
            DisableEnemyAttacks(enemy);
            
            // Store original position offset relative to player for carrying
            Vector3 offset = enemy.transform.position - transform.position;
            EnemyThrustAnchor anchor = enemy.GetComponent<EnemyThrustAnchor>();
            if (anchor == null)
            {
                anchor = enemy.AddComponent<EnemyThrustAnchor>();
            }
            anchor.originalOffset = offset;
            anchor.isAnchored = true;
            
            Debug.Log($"Enemy {enemy.name} anchored to thrust attack");
        }
    }
    
    /// <summary>
    /// Update positions of all pierced enemies to follow the player's thrust
    /// </summary>
    private void UpdatePiercedEnemyPositions()
    {
        for (int i = piercedEnemies.Count - 1; i >= 0; i--)
        {
            if (piercedEnemies[i] == null)
            {
                piercedEnemies.RemoveAt(i);
                continue;
            }
            
            EnemyThrustAnchor anchor = piercedEnemies[i].GetComponent<EnemyThrustAnchor>();
            if (anchor != null && anchor.isAnchored)
            {
                // Keep enemy at the same relative position to the player
                piercedEnemies[i].transform.position = transform.position + anchor.originalOffset;
            }
        }
    }
    
    /// <summary>
    /// Release all pierced enemies and re-enable their attacks
    /// </summary>
    private void ReleasePiercedEnemies()
    {
        foreach (GameObject enemy in piercedEnemies)
        {
            if (enemy != null)
            {
                // Re-enable enemy attacks
                EnableEnemyAttacks(enemy);
                
                // Remove anchor component
                EnemyThrustAnchor anchor = enemy.GetComponent<EnemyThrustAnchor>();
                if (anchor != null)
                {
                    anchor.isAnchored = false;
                    Destroy(anchor);
                }
                
                Debug.Log($"Enemy {enemy.name} released from thrust attack");
            }
        }
        
        piercedEnemies.Clear();
    }
    
    /// <summary>
    /// Disable enemy attack capabilities while anchored
    /// </summary>
    private void DisableEnemyAttacks(GameObject enemy)
    {
        // Find common enemy attack components and disable them
        MonoBehaviour[] components = enemy.GetComponents<MonoBehaviour>();
        foreach (var component in components)
        {
            // Look for components that likely handle enemy attacks
            if (component.GetType().Name.Contains("Attack") || 
                component.GetType().Name.Contains("Combat") ||
                component.GetType().Name.Contains("Weapon"))
            {
                component.enabled = false;
            }
        }
        
        // Also try to find and disable any DamageObject components on the enemy
        DamageObject[] damageObjects = enemy.GetComponentsInChildren<DamageObject>();
        foreach (var damageObj in damageObjects)
        {
            damageObj.enabled = false;
        }
    }
    
    /// <summary>
    /// Re-enable enemy attack capabilities when released
    /// </summary>
    private void EnableEnemyAttacks(GameObject enemy)
    {
        // Re-enable previously disabled components
        MonoBehaviour[] components = enemy.GetComponents<MonoBehaviour>();
        foreach (var component in components)
        {
            if (component.GetType().Name.Contains("Attack") || 
                component.GetType().Name.Contains("Combat") ||
                component.GetType().Name.Contains("Weapon"))
            {
                component.enabled = true;
            }
        }
        
        // Re-enable DamageObject components
        DamageObject[] damageObjects = enemy.GetComponentsInChildren<DamageObject>();
        foreach (var damageObj in damageObjects)
        {
            damageObj.enabled = true;
        }
    }
    
    // ===== PLAYER ANIMATION SYSTEM =====
    // Manages animation controller switching based on equipped shards
    
    /// <summary>
    /// Update player animation controller based on currently active shard
    /// </summary>
    private void UpdatePlayerAnimationController()
    {
        if (playerMovement == null) 
        {
            Debug.LogError("WeaponClassController: playerMovement is null in UpdatePlayerAnimationController");
            return;
        }
        
        ShardType activeShard = equippedShards[activeSlotIndex];
        RuntimeAnimatorController targetController = defaultPlayerAnimController;
        
        Debug.Log($"WeaponClassController: Updating animation controller for shard: {activeShard}");
        
        // Select the appropriate animation controller based on active shard
        switch (activeShard)
        {
            case ShardType.ValorShard:
                targetController = valorShardPlayerAnimController ?? defaultPlayerAnimController;
                break;
            case ShardType.WhisperShard:
                targetController = whisperShardPlayerAnimController ?? defaultPlayerAnimController;
                break;
            case ShardType.StormShard:
                targetController = stormShardPlayerAnimController ?? defaultPlayerAnimController;
                break;
            case ShardType.None:
            default:
                targetController = defaultPlayerAnimController;
                break;
        }
        
        Debug.Log($"WeaponClassController: Selected controller: {(targetController != null ? targetController.name : "null")}");
        
        // Pass the controller reference to PlayerMovement
        playerMovement.SetAnimationController(targetController);
    }
    
    /// <summary>
    /// Trigger attack animation with specific type and duration
    /// </summary>
    private void TriggerAttackAnimation(int attackType, float duration = 0.5f)
    {
        if (playerMovement == null) return;
        
        playerMovement.TriggerAttackAnimation(attackType, duration);
        
        Debug.Log($"Triggered {GetActiveWeaponName()} attack animation: Type {attackType}");
    }
    
    // ===== WEAPON SHARD PASSIVE ABILITIES =====
    // Storm, Whisper, and Valor Shard passive abilities
    
    private void UpdatePassiveAbilities()
    {
        // Storm Shard Movement Passive - 5% swiftness while moving
        UpdateStormMovementPassive();
    }
    
    private void UpdateStormMovementPassive()
    {
        // Only apply if Storm Shard is equipped in either slot
        bool hasStormShard = equippedShards[0] == ShardType.StormShard || equippedShards[1] == ShardType.StormShard;
        if (!hasStormShard || playerMovement == null) return;
        
        // Check movement at specified intervals
        if (Time.time >= lastMovementCheckTime + stormMovementCheckInterval)
        {
            lastMovementCheckTime = Time.time;
            
            // Check if player is moving horizontally
            bool isMovingNow = playerMovement.IsMovingHorizontally();
            
            if (isMovingNow && !wasMovingLastCheck)
            {
                // Player started moving - add a swiftness stack
                ApplyStormMovementBuff();
            }
            else if (!isMovingNow && wasMovingLastCheck)
            {
                // Player stopped moving - no new stacks but existing ones will naturally expire
                Debug.Log("Storm Shard: Player stopped moving, swiftness stacks will expire naturally");
            }
            
            wasMovingLastCheck = isMovingNow;
        }
    }
    
    private void ApplyStormMovementBuff()
    {
        string buffKey = "StormMovementSwiftness";
        
        // Track current stacks
        if (!passiveBuffStacks.ContainsKey(buffKey))
        {
            passiveBuffStacks[buffKey] = 0;
        }
        
        // Only add if under stack limit
        if (passiveBuffStacks[buffKey] < maxStormMovementStacks)
        {
            passiveBuffStacks[buffKey]++;
            
            // Apply swiftness buff through PlayerMovement system
            playerMovement.ApplyBuff(PlayerMovement.BuffType.Swiftness, stormMovementSwiftnessPercent, stormMovementBuffDuration);
            
            Debug.Log($"Storm Movement Passive: Applied {stormMovementSwiftnessPercent}% Swiftness (Stack {passiveBuffStacks[buffKey]}/{maxStormMovementStacks})");
        }
        else
        {
            // At max stacks - just refresh duration by applying another buff
            playerMovement.ApplyBuff(PlayerMovement.BuffType.Swiftness, stormMovementSwiftnessPercent, stormMovementBuffDuration);
            Debug.Log($"Storm Movement Passive: Refreshed swiftness buff (Max stacks: {maxStormMovementStacks})");
        }
    }
    
    // Whisper Shard Attack Passive - buffs on enemy hits
    public void ApplyWhisperAttackPassive()
    {
        // Only apply if Whisper Shard is equipped in either slot
        bool hasWhisperShard = equippedShards[0] == ShardType.WhisperShard || equippedShards[1] == ShardType.WhisperShard;
        if (!hasWhisperShard || playerMovement == null) return;
        
        string buffKey = "WhisperAttackBuff";
        
        // Track current stacks
        if (!passiveBuffStacks.ContainsKey(buffKey))
        {
            passiveBuffStacks[buffKey] = 0;
        }
        
        // Only add if under stack limit
        if (passiveBuffStacks[buffKey] < maxWhisperAttackStacks)
        {
            passiveBuffStacks[buffKey]++;
            
            // Apply attack buff through PlayerMovement system
            playerMovement.ApplyBuff(PlayerMovement.BuffType.Strength, whisperAttackBuffPercent, whisperAttackBuffDuration);
            
            Debug.Log($"Whisper Attack Passive: Applied {whisperAttackBuffPercent}% Attack buff (Stack {passiveBuffStacks[buffKey]}/{maxWhisperAttackStacks})");
        }
        else
        {
            // At max stacks - just refresh duration
            playerMovement.ApplyBuff(PlayerMovement.BuffType.Strength, whisperAttackBuffPercent, whisperAttackBuffDuration);
            Debug.Log($"Whisper Attack Passive: Refreshed attack buff (Max stacks: {maxWhisperAttackStacks})");
        }
    }
    
    // ===== VALOR SHARD PASSIVE BUFF SYSTEM =====
    // Comprehensive buff application methods that integrate with PlayerMovement buff system
    
    /// <summary>
    /// Apply double-click buffs: 5% aegis shield + 5 durability for 10 seconds
    /// </summary>
    private void ApplyDoubleClickBuffs()
    {
        if (equippedShards[activeSlotIndex] != ShardType.ValorShard || playerMovement == null) return;
        
        // Apply aegis shield buff with Valor Shard cap (percentage based on configuration)
        playerMovement.ApplyValorAttackAegisBuff(doubleClickAegisPercent, valorAegisCapPercent);
        
        // Apply durability buff (flat health increase)
        playerMovement.ApplyBuff(PlayerMovement.BuffType.Durability, doubleClickDurabilityAmount, doubleClickBuffDuration);
        
        Debug.Log($"Valor Double-Click: Applied {doubleClickAegisPercent}% Aegis Shield + {doubleClickDurabilityAmount} Durability for {doubleClickBuffDuration}s");
    }
    
    /// <summary>
    /// Apply triple-click buffs: 15% attack buff for 5 seconds
    /// </summary>
    private void ApplyTripleClickBuffs()
    {
        if (equippedShards[activeSlotIndex] != ShardType.ValorShard || playerMovement == null) return;
        
        // Apply strength buff (attack damage increase)
        playerMovement.ApplyBuff(PlayerMovement.BuffType.Strength, tripleClickAttackPercent, tripleClickBuffDuration);
        
        Debug.Log($"Valor Triple-Click: Applied {tripleClickAttackPercent}% Attack buff for {tripleClickBuffDuration}s");
    }
    
    /// <summary>
    /// Apply kill-based stackable buffs: 10% attack buff for 10 seconds (stackable up to 5 times)
    /// </summary>
    private Dictionary<string, int> killBuffStacks = new Dictionary<string, int>();
    
    private void ApplyKillBasedBuffs()
    {
        if (equippedShards[activeSlotIndex] != ShardType.ValorShard || playerMovement == null) return;
        
        string killBuffKey = "ValorKillAttack";
        
        // Track current stacks
        if (!killBuffStacks.ContainsKey(killBuffKey))
        {
            killBuffStacks[killBuffKey] = 0;
        }
        
        // Only add if under stack limit
        if (killBuffStacks[killBuffKey] < maxKillBuffStacks)
        {
            killBuffStacks[killBuffKey]++;
            
            // Apply strength buff for each stack
            playerMovement.ApplyBuff(PlayerMovement.BuffType.Strength, killAttackPercent, killBuffDuration);
            
            Debug.Log($"Valor Kill: Applied {killAttackPercent}% Attack buff (Stack {killBuffStacks[killBuffKey]}/{maxKillBuffStacks}) for {killBuffDuration}s");
            
            // Start decay coroutine for this specific stack
            StartCoroutine(DecayKillBuffStack(killBuffKey));
        }
        else
        {
            Debug.Log($"Valor Kill: Max stacks ({maxKillBuffStacks}) reached for kill attack buffs!");
        }
    }
    
    /// <summary>
    /// Decay individual kill buff stacks after their duration expires
    /// </summary>
    private System.Collections.IEnumerator DecayKillBuffStack(string buffKey)
    {
        yield return new WaitForSeconds(killBuffDuration);
        
        if (killBuffStacks.ContainsKey(buffKey) && killBuffStacks[buffKey] > 0)
        {
            killBuffStacks[buffKey]--;
            Debug.Log($"Valor Kill Stack decayed. Remaining: {killBuffStacks[buffKey]}/{maxKillBuffStacks}");
        }
    }
    
    /// <summary>
    /// Apply wave charge buffs: 5% aegis shield for 3+ charge waves
    /// </summary>
    private void ApplyWaveChargeBuffs(int chargeLevel)
    {
        if (equippedShards[activeSlotIndex] != ShardType.ValorShard || playerMovement == null) return;
        
        // Only apply if charge level meets minimum requirement
        if (chargeLevel >= minWaveChargesForBuff)
        {
            // Apply aegis shield buff with Valor Shard cap (percentage based on configuration)
            playerMovement.ApplyValorAttackAegisBuff(waveChargeAegisPercent, valorAegisCapPercent);
            
            Debug.Log($"Valor Wave Charge: Applied {waveChargeAegisPercent}% Aegis Shield for {chargeLevel}-charge wave (duration: {waveChargeBuffDuration}s)");
        }
    }
    
    /// <summary>
    /// Public method called when enemy is killed by Valor Shard attacks
    /// </summary>
    public void OnEnemyKilledByValor()
    {
        ApplyKillBasedBuffs();
    }
    
    // ===== ULTIMATE SYSTEM =====
    
    /// <summary>
    /// Activates the ultimate ability for the currently equipped shard
    /// </summary>
    private void ActivateUltimate()
    {
        ShardType activeWeapon = equippedShards[activeSlotIndex];
        
        switch (activeWeapon)
        {
            case ShardType.ValorShard:
                ActivateValorUltimate();
                break;
            case ShardType.WhisperShard:
                Debug.Log("WhisperShard ultimate not yet implemented");
                break;
            case ShardType.StormShard:
                Debug.Log("StormShard ultimate not yet implemented");
                break;
            default:
                Debug.Log("No shard equipped - cannot use ultimate");
                return;
        }
        
        // Consume ultimate charge
        playerMovement.ConsumeUltimateCharge(100f);
        Debug.Log($"Ultimate activated for {activeWeapon}! Ultimate charge consumed.");
    }
    
    /// <summary>
    /// Valor Shard Ultimate: Summons 3 attack dummies around the player
    /// </summary>
    private void ActivateValorUltimate()
    {
        if (attackDummyPrefab == null)
        {
            Debug.LogError("Attack Dummy Prefab not assigned! Cannot summon dummies.");
            return;
        }
        
        // Clean up any destroyed dummies from the list
        CleanUpDestroyedDummies();
        
        // Check if we can summon the full amount
        int dummiesToSummon = Mathf.Min(dummiesPerUltimate, maxActiveDummies - activeDummies.Count);
        
        if (dummiesToSummon <= 0)
        {
            Debug.Log($"Cannot summon dummies - already at maximum ({maxActiveDummies})");
            return;
        }
        
        Vector3 playerPosition = transform.position;
        
        // Summon dummies in assorted formation with follow distances 1-5
        for (int i = 0; i < dummiesToSummon; i++)
        {
            // Left/right alternating pattern for ground rising animation only
            bool isLeft = (i % 2 == 0); // Even indices go left, odd go right
            float horizontalOffset = summonRadius; // Use base summon radius for spawn positioning
            if (isLeft) horizontalOffset = -horizontalOffset; // Negative for left side
            
            // Follow distance is simply 1, 2, 3, 4, 5 (not tied to spawn position)
            int followDistance = Mathf.Min(i + 1, 5); // Direct follow distance: 1-5
            
            Vector3 spawnPosition = playerPosition + new Vector3(horizontalOffset, 0f, 0f);
            
            // Ensure spawn position is well above ground
            spawnPosition.y = playerPosition.y + 1f; // Start 1 unit above player
            
            // Check for ground below and adjust if needed
            RaycastHit2D groundCheck = Physics2D.Raycast(spawnPosition, Vector2.down, 10f, LayerMask.GetMask("Ground", "Platform"));
            if (groundCheck.collider != null)
            {
                // Place clearly above ground surface
                spawnPosition.y = Mathf.Max(spawnPosition.y, groundCheck.point.y + 1.5f);
            }
            
            // Summon dummy from underground to this position
            StartCoroutine(SummonDummyFromGroundWithDistance(spawnPosition, followDistance));
            
            Debug.Log($"Summoning dummy {i+1}: {(isLeft ? "LEFT" : "RIGHT")} side, follow distance: {followDistance}, spawn position {spawnPosition}");
        }
        
        Debug.Log($"Valor Ultimate: Summoning {dummiesToSummon} attack dummies around player!");
    }
    
    /// <summary>
    /// Coroutine to animate dummy summoning from ground with direct follow distance (1-5)
    /// </summary>
    private System.Collections.IEnumerator SummonDummyFromGroundWithDistance(Vector3 spawnPosition, int followDistance)
    {
        // Ensure spawn position is above player Y level to prevent underground stuck
        Vector3 playerPos = transform.position;
        if (spawnPosition.y < playerPos.y + 1f)
        {
            spawnPosition.y = playerPos.y + 1.5f; // Ensure well above player
        }
        
        // Create dummy well below ground initially for animation
        Vector3 undergroundPos = spawnPosition + Vector3.down * 4f;
        GameObject dummy = Instantiate(attackDummyPrefab, undergroundPos, Quaternion.identity);
        
        // Set up dummy properties
        dummy.tag = "PlayerSummon";
        
        // Set layer to avoid collision with Entities and Player layers
        // Priority: PlayerSummon > NPC > IgnoreRaycast (fallback)
        int playerSummonLayer = LayerMask.NameToLayer("PlayerSummon");
        int npcLayer = LayerMask.NameToLayer("NPC"); 
        int ignoreRaycastLayer = 2; // Built-in layer that typically doesn't collide with much
        
        if (playerSummonLayer != -1)
        {
            dummy.layer = playerSummonLayer;
            Debug.Log("AttackDummy set to PlayerSummon layer - should not collide with Player/Entities");
        }
        else if (npcLayer != -1)
        {
            dummy.layer = npcLayer;
            Debug.Log("AttackDummy set to NPC layer - configure Layer Collision Matrix to prevent Player/Entities collision");
        }
        else
        {
            // Use IgnoreRaycast layer as fallback - this layer typically doesn't collide
            dummy.layer = ignoreRaycastLayer;
            Debug.Log("AttackDummy set to IgnoreRaycast layer (fallback) - should not collide with most layers");
        }
        
        // Set custom follow distance (direct value 1-5)
        AttackDummy dummyScript = dummy.GetComponent<AttackDummy>();
        if (dummyScript != null)
        {
            dummyScript.SetFollowDistance(followDistance);
            Debug.Log($"AttackDummy follow distance set to: {followDistance}");
        }
        
        Debug.Log($"AttackDummy final layer: {LayerMask.LayerToName(dummy.layer)} ({dummy.layer})");
        
        // Animate rising from ground
        float riseTime = 0.8f; // Slightly longer for better visual
        float elapsed = 0f;
        
        while (elapsed < riseTime)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / riseTime;
            dummy.transform.position = Vector3.Lerp(undergroundPos, spawnPosition, progress);
            yield return null;
        }
        
        dummy.transform.position = spawnPosition;
        
        // Add to active dummies list
        activeDummies.Add(dummy);
        
        // Start lifespan countdown
        StartCoroutine(DummyLifespanCountdown(dummy));
        
        Debug.Log($"Attack dummy summoned at {spawnPosition} with follow distance {followDistance} - Active dummies: {activeDummies.Count}/{maxActiveDummies}");
    }
    
    /// <summary>
    /// Coroutine to animate dummy summoning from ground (legacy method)
    /// </summary>
    private System.Collections.IEnumerator SummonDummyFromGround(Vector3 spawnPosition)
    {
        // Ensure spawn position is above player Y level to prevent underground stuck
        Vector3 playerPos = transform.position;
        if (spawnPosition.y < playerPos.y)
        {
            spawnPosition.y = playerPos.y + 0.5f;
        }
        
        // Create dummy below ground initially for animation
        Vector3 undergroundPos = spawnPosition + Vector3.down * 3f;
        GameObject dummy = Instantiate(attackDummyPrefab, undergroundPos, Quaternion.identity);
        
        // Set up dummy properties
        dummy.tag = "PlayerSummon";
        
        // Try multiple layer options for non-colliding summons
        int playerSummonLayer = LayerMask.NameToLayer("PlayerSummon");
        int npcLayer = LayerMask.NameToLayer("NPC"); 
        int ignoreRaycastLayer = 2; // Built-in layer that typically doesn't collide with much
        
        if (playerSummonLayer != -1)
        {
            dummy.layer = playerSummonLayer;
            Debug.Log("AttackDummy set to PlayerSummon layer");
        }
        else if (npcLayer != -1)
        {
            dummy.layer = npcLayer;
            Debug.Log("AttackDummy set to NPC layer");
        }
        else
        {
            // Use IgnoreRaycast layer as fallback - this layer typically doesn't collide
            dummy.layer = ignoreRaycastLayer;
            Debug.Log("AttackDummy set to IgnoreRaycast layer (fallback)");
        }
        
        Debug.Log($"AttackDummy final layer: {LayerMask.LayerToName(dummy.layer)} ({dummy.layer})");
        
        // Animate rising from ground
        float riseTime = 0.5f;
        float elapsed = 0f;
        
        while (elapsed < riseTime)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / riseTime;
            dummy.transform.position = Vector3.Lerp(undergroundPos, spawnPosition, progress);
            yield return null;
        }
        
        dummy.transform.position = spawnPosition;
        
        // Add to active dummies list
        activeDummies.Add(dummy);
        
        // Start lifespan countdown
        StartCoroutine(DummyLifespanCountdown(dummy));
        
        Debug.Log($"Attack dummy summoned at {spawnPosition} - Active dummies: {activeDummies.Count}/{maxActiveDummies}");
    }
    
    /// <summary>
    /// Handles dummy lifespan and destruction
    /// </summary>
    private System.Collections.IEnumerator DummyLifespanCountdown(GameObject dummy)
    {
        yield return new WaitForSeconds(dummyLifespan);
        
        if (dummy != null)
        {
            // Animate sinking back into ground
            Vector3 currentPos = dummy.transform.position;
            Vector3 undergroundPos = currentPos + Vector3.down * 2f;
            
            float sinkTime = 0.5f;
            float elapsed = 0f;
            
            while (elapsed < sinkTime && dummy != null)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / sinkTime;
                dummy.transform.position = Vector3.Lerp(currentPos, undergroundPos, progress);
                yield return null;
            }
            
            // Remove from active list and destroy
            if (activeDummies.Contains(dummy))
            {
                activeDummies.Remove(dummy);
            }
            
            if (dummy != null)
            {
                Destroy(dummy);
                Debug.Log($"Attack dummy lifespan expired - Active dummies: {activeDummies.Count}/{maxActiveDummies}");
            }
        }
    }
    
    /// <summary>
    /// Removes destroyed dummies from the active list
    /// </summary>
    private void CleanUpDestroyedDummies()
    {
        for (int i = activeDummies.Count - 1; i >= 0; i--)
        {
            if (activeDummies[i] == null)
            {
                activeDummies.RemoveAt(i);
            }
        }
    }
    
    // Method for dagger cleanup component to clear reference
    public void ClearDaggerReference(GameObject dagger)
    {
        if (currentThrownDagger == dagger)
        {
            currentThrownDagger = null;
            currentRedirectCount = 0;
            daggerExpired = false;
        }
    }
    
    // Helper method to update dagger rotation to face movement direction
    private void UpdateDaggerRotation(GameObject dagger, Vector3 direction)
    {
        if (dagger == null || direction == Vector3.zero) return;
        
        // Calculate rotation angle from direction vector
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        
        // Apply rotation to make dagger point in movement direction
        dagger.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        
        // Temporarily disable automatic rotation controller during redirect
        DaggerRotationController rotationController = dagger.GetComponent<DaggerRotationController>();
        if (rotationController != null)
        {
            rotationController.enabled = false;
        }
    }
    
    // Ultimate Charge System
    private void SyncUltimateChargeSettings()
    {
        if (playerMovement != null)
        {
            // Sync the max ultimate charge setting with PlayerMovement
            playerMovement.SetMaxUltimateCharge(maxUltimateCharge);
            Debug.Log($"Ultimate charge system initialized - Max charge: {maxUltimateCharge}");
        }
    }
    
    private void GenerateUltimateCharge(float chargeAmount)
    {
        if (playerMovement != null)
        {
            playerMovement.AddUltimateCharge(chargeAmount);
            Debug.Log($"Ultimate charge generated: +{chargeAmount}");
        }
        else
        {
            Debug.LogError("WeaponClassController: PlayerMovement reference is null, cannot add ultimate charge!");
        }
    }
    
    public float GetCurrentUltimateCharge()
    {
        if (playerMovement != null)
        {
            return playerMovement.GetCurrentUltimateCharge();
        }
        return 0f;
    }
    
    public void ConsumeUltimateCharge(float amount)
    {
        if (playerMovement != null)
        {
            playerMovement.ConsumeUltimateCharge(amount);
            Debug.Log($"Ultimate charge consumed: -{amount}");
        }
    }
}

// Component to handle dagger rotation based on velocity
public class DaggerRotationController : MonoBehaviour
{
    private Rigidbody2D rb;
    
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }
    
    void Update()
    {
        if (rb != null && rb.linearVelocity != Vector2.zero)
        {
            // Calculate angle based on velocity direction
            float angle = Mathf.Atan2(rb.linearVelocity.y, rb.linearVelocity.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }
    }
}

// Component to handle dagger cleanup and reference clearing
public class DaggerCleanup : MonoBehaviour
{
    [System.NonSerialized]
    public WeaponClassController weaponController;
    
    void OnDestroy()
    {
        if (weaponController != null)
        {
            weaponController.ClearDaggerReference(gameObject);
        }
    }
}

// Component to handle dagger ground collision
public class DaggerGroundCollision : MonoBehaviour
{
    [System.NonSerialized]
    public WeaponClassController weaponController;
    private bool hasStuck = false;
    
    void OnTriggerEnter2D(Collider2D other)
    {
        // Check if dagger hits ground layer objects and hasn't stuck yet
        if (!hasStuck && IsGroundObject(other))
        {
            StickToGround();
        }
    }
    
    void OnCollisionEnter2D(Collision2D collision)
    {
        // Also handle non-trigger collisions with ground
        if (!hasStuck && IsGroundObject(collision.collider))
        {
            StickToGround();
        }
    }
    
    private bool IsGroundObject(Collider2D collider)
    {
        // Check if object is on the Ground layer
        return collider.gameObject.layer == LayerMask.NameToLayer("Ground");
    }
    
    private void StickToGround()
    {
        hasStuck = true;
        
        // Stop all movement
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.gravityScale = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic; // Make it completely static
        }
        
        // Disable rotation component to maintain current rotation
        DaggerRotationController rotationController = GetComponent<DaggerRotationController>();
        if (rotationController != null)
        {
            rotationController.enabled = false;
        }
        
        Debug.Log("WhisperShard: Dagger stuck in ground!");
    }
}

/// <summary>
/// Component to hold reference to animated wave prefab for valor wave blocks
/// </summary>
public class WaveBlockComponent : MonoBehaviour
{
    public GameObject animatedWave;
    
    public void TriggerWaveAnimation()
    {
        if (animatedWave != null)
        {
            Animator animator = animatedWave.GetComponent<Animator>();
            if (animator != null)
            {
                Debug.Log($"Animator found on {animatedWave.name}");
                Debug.Log($"Controller: {(animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.name : "NULL")}");
                
                // Check all available parameters
                AnimatorControllerParameter[] parameters = animator.parameters;
                Debug.Log($"Available parameters: {parameters.Length}");
                foreach (var param in parameters)
                {
                    Debug.Log($"Parameter: {param.name} (Type: {param.type})");
                }
                
                // Get current state info
                AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
                Debug.Log($"Current state hash: {currentState.fullPathHash}");
                Debug.Log($"Is WaveAnim state: {currentState.IsName("WaveAnim")}");
                Debug.Log($"Is Idle state: {currentState.IsName("Idle")}");
                
                // Check if StartWave parameter exists
                bool hasStartWave = false;
                foreach (var param in parameters)
                {
                    if (param.name == "StartWave" && param.type == AnimatorControllerParameterType.Trigger)
                    {
                        hasStartWave = true;
                        break;
                    }
                }
                
                if (hasStartWave)
                {
                    Debug.Log("StartWave parameter found, triggering animation");
                    animator.SetTrigger("StartWave");
                    Debug.Log("Triggered StartWave on prefab: " + animatedWave.name);
                }
                else
                {
                    Debug.LogError("StartWave trigger parameter not found in animation controller!");
                }
            }
            else
            {
                Debug.LogError("No Animator found on valor wave prefab: " + animatedWave.name);
            }
        }
        else
        {
            Debug.LogError("AnimatedWave is null in TriggerWaveAnimation!");
        }
    }
}

/// <summary>
/// Special damage object for sword thrust that anchors enemies
/// </summary>
public class ThrustDamageObject : DamageObject
{
    public WeaponClassController weaponController;
    
    void OnTriggerEnter2D(Collider2D other)
    {
        // Let DamageObject handle its normal damage logic
        // Since we can't override, we'll just add our anchoring logic
        
        // Anchor enemy if it's an enemy and not already anchored
        if (other.CompareTag("Enemy") && weaponController != null)
        {
            weaponController.AnchorEnemyToThrust(other.gameObject);
        }
    }
}

/// <summary>
/// Component to track enemy anchoring state during thrust attacks
/// </summary>
public class EnemyThrustAnchor : MonoBehaviour
{
    public Vector3 originalOffset;
    public bool isAnchored = false;
}