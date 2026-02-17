using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
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
    
    [Header("ValorShard Attack Settings")]
    [SerializeField] private float swordRange = 0.8f; // Closer to player
    [SerializeField] private float swordWidth = 1.2f; // Wider to match player
    [SerializeField] private float swordHeight = 2.0f; // Taller to ensure coverage
    [SerializeField] private float swordDuration = 0.25f; // Halved from 0.5f
    [SerializeField] private int swordDamage = 25;
    [SerializeField] private float swordCooldown = 0.5f; // Cooldown between sword attacks
    [SerializeField] private float waveCooldown = 1.0f; // Cooldown between wave attacks
    
    [Header("ValorShard Wave Attack Settings")]
    [SerializeField] private float waveBlockSize = 1f; // Size of each wave block
    [SerializeField] private float waveBlockSpacing = 1.2f; // Spacing between blocks (no overlap)
    [SerializeField] private float waveBounceHeight = 2f; // How high blocks bounce
    [SerializeField] private int waveDamage = 30; // Damage per wave block
    [SerializeField] private Sprite valorWaveSprite = null; // Sprite for valor wave damage blocks
    
    [Header("WhisperShard Attack Settings")]
    [SerializeField] private float daggerRange = 0.6f; // Closer than sword
    [SerializeField] private float daggerWidth = 0.8f; // Smaller than sword
    [SerializeField] private float daggerHeight = 1.5f; // Smaller than sword
    [SerializeField] private float daggerDuration = 0.2f; // Quick attack
    [SerializeField] private int daggerDamage = 20;
    [SerializeField] private float daggerCooldown = 0.3f; // Cooldown between dagger melee attacks
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
    
    // Tracking variables for dagger recall
    private GameObject currentThrownDagger = null;
    private int currentRedirectCount = 0;
    private bool daggerExpired = false;
    
    [Header("StormShard Attack Settings")]
    [SerializeField] private float staffRange = 1f; // Particle emission point distance
    [SerializeField] private float lightningRange = 10f; // Max range for targeted lightning
    [SerializeField] private int lightningDamage = 30;
    [SerializeField] private float lightningCooldown = 0.6f; // Cooldown between lightning arcs
    [SerializeField] private int boltDamage = 40; // Sky bolt damage
    [SerializeField] private float lightningDuration = 0.3f; // How long lightning arc lasts
    [SerializeField] private float boltDuration = 0.5f; // How long lightning bolt impact lasts
    [SerializeField] private float boltCooldown = 1.2f; // Cooldown between lightning bolts
    [SerializeField] private float boltHeight = 100f; // Height above player for sky bolt
    [SerializeField] private float boltRange = 15f; // Range to find nearest enemy for sky bolt
    [SerializeField] private Sprite lightningBoltSprite = null; // Sprite for lightning bolt blast damage boxes
    [SerializeField] private Material lightningArcMaterial = null; // Custom glowing material for lightning arcs and bolt strikes
    
    [Header("StormShard Visual Effects")]
    [SerializeField] private GameObject lightningArcGifPrefab = null; // GIF/Animation prefab for lightning arc visual effects
    [SerializeField] private float gifScaleMultiplier = 1f; // Scale multiplier for the GIF effect
    [SerializeField] private bool useGifInsteadOfLineRenderer = true; // Toggle between GIF and LineRenderer visuals
    
    [Header("Chain Lightning Settings")]
    [SerializeField] private float chainRange = 8f; // Range to find nearby enemies for chaining
    [SerializeField] private int maxChainArcs = 2; // Maximum number of chain arcs per attack (1-2)
    [SerializeField] private float chainDamageMultiplier = 0.7f; // Damage multiplier for chain arcs (70% of original)
    [SerializeField] private float chainDelay = 0.1f; // Delay between each chain arc
    [SerializeField] private float chainArcDuration = 0.25f; // How long chain arcs last
    
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
    private float multiClickWindow = 0.8f; // Extended window for easier multi-click detection
    private bool isPerformingSpecialAttack = false;
    
    // ValorShard Special Attack Settings
    [SerializeField] private float dashForce = 8f; // Forward propulsion force
    private bool isPerformingFlip = false;
    private float flipStartTime = 0f;
    private float flipDuration = 1f; // Duration of flip animation
    private GameObject flipDamageZone = null;
    
    // ValorShard Passive Buff System - Comprehensive Configuration
    [Header("Valor Shard - Double Click Buffs")]
    [SerializeField] private float doubleClickAegisPercent = 5f; // 5% aegis shield
    [SerializeField] private float doubleClickDurabilityAmount = 5f; // 5 durability points
    [SerializeField] private float doubleClickBuffDuration = 10f; // 10 seconds
    
    [Header("Valor Shard - Triple Click Buffs")]
    [SerializeField] private float tripleClickAttackPercent = 15f; // 15% attack buff
    [SerializeField] private float tripleClickBuffDuration = 5f; // 5 seconds
    
    [Header("Valor Shard - Kill-Based Stackable Buffs")]
    [SerializeField] private float killAttackPercent = 10f; // 10% attack buff per kill
    [SerializeField] private float killBuffDuration = 10f; // 10 seconds per stack
    [SerializeField] private int maxKillBuffStacks = 5; // Maximum stackable kill buffs
    
    [Header("Valor Shard - Wave Charge Buffs")]
    [SerializeField] private float waveChargeAegisPercent = 5f; // 5% aegis shield
    [SerializeField] private int minWaveChargesForBuff = 3; // 3+ charges required
    [SerializeField] private float waveChargeBuffDuration = 15f; // 15 seconds for wave buffs
    
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
    private GameObject stormParticlePoint; // Invisible emission point for storm attacks
    
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
        InitializeGUI();
        LoadShardSprites();
        FindStormParticlePoint();
        
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
                    CreateElectricArc(); // Fire another arc
                    nextAutoFireTime = Time.time + lightningCooldown;
                    
                    // Generate ultimate charge for storm constant left click (auto-fire)
                    GenerateUltimateCharge(stormConstantLeftClickCharge);
                }
            }
            else if (!leftClickHeld)
            {
                // Stop auto-fire if left click is no longer held
                isAutoFiring = false;
            }
        }
    }
    
    private void EquipShard(ShardType shardType)
    {
        int emptySlot = GetEmptySlotIndex();
        if (emptySlot == -1) return;
        
        equippedShards[emptySlot] = shardType;
        UpdateSlotDisplay(emptySlot);
        
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
            lastSwordAttackTime = Time.time;
            
            if (bypassCooldown)
            {
                Debug.Log("Dash sword swing bypassed cooldown!");
            }
        }
    }
    
    private void CreateSwordAttack()
    {
        // Get player's sprite renderer to check facing direction
        SpriteRenderer playerSprite = GetComponent<SpriteRenderer>();
        if (playerSprite == null)
            playerSprite = GetComponentInChildren<SpriteRenderer>();
        
        // Create sword attack damage object in front of player
        bool facingLeft = playerSprite != null ? playerSprite.flipX : false;
        Vector3 attackPosition = playerTransform.position + (Vector3.right * (facingLeft ? -swordRange : swordRange));
        
        // Generate ultimate charge for valor left click attack
        GenerateUltimateCharge(valorLeftClickCharge);
        
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
            // Left-click: Quick dagger strike with cooldown
            if (Time.time - lastDaggerAttackTime < daggerCooldown) return;
            
            CreateDaggerStrike();
            lastDaggerAttackTime = Time.time;
            
            // Generate ultimate charge for whisper left click attack
            GenerateUltimateCharge(whisperLeftClickCharge);
        }
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
            // Left-click: Electric arc with cooldown (only check for manual clicks, not auto-fire)
            if (!isAutoFiring && Time.time - lastLightningArcTime < lightningCooldown) return;
            
            CreateElectricArc();
            lastLightningArcTime = Time.time;
            
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
        swordRenderer.sortingOrder = 10;
        
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
        
        // Visual indicator (valor wave sprite)
        SpriteRenderer blockRenderer = waveBlock.AddComponent<SpriteRenderer>();
        
        // Use custom valor wave sprite if assigned, otherwise create fallback golden texture
        if (valorWaveSprite != null)
        {
            blockRenderer.sprite = valorWaveSprite;
        }
        else
        {
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
        }
        
        blockRenderer.sortingOrder = 10;
        
        return waveBlock;
    }
    
    private IEnumerator AnimateWaveBlock(GameObject waveBlock, float delay)
    {
        // Wait for wave delay
        yield return new WaitForSeconds(delay);
        
        if (waveBlock == null) yield break;
        
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
        yield return new WaitForSeconds(0.3f);
        
        if (waveBlock != null)
            Destroy(waveBlock);
    }
    
    private void FindStormParticlePoint()
    {
        // Find existing SSParticlePoint in player hierarchy
        Transform particlePoint = transform.Find("SSParticlePoint");
        if (particlePoint == null)
        {
            // Search in children recursively
            particlePoint = GetComponentInChildren<Transform>().Find("SSParticlePoint");
        }
        
        if (particlePoint != null)
        {
            stormParticlePoint = particlePoint.gameObject;
        }
        else
        {
            Debug.LogError("SSParticlePoint not found! Please create an empty GameObject named 'SSParticlePoint' as a child of the player.");
            // Create a fallback point
            stormParticlePoint = new GameObject("SSParticlePoint_Fallback");
            stormParticlePoint.transform.SetParent(transform);
            stormParticlePoint.transform.localPosition = new Vector3(1.5f, 1f, 0);
        }
    }
    
    private void UpdateStormParticlePosition()
    {
        if (stormParticlePoint == null) return;
        
        // Get player's facing direction
        SpriteRenderer playerSprite = GetComponent<SpriteRenderer>();
        if (playerSprite == null)
            playerSprite = GetComponentInChildren<SpriteRenderer>();
        
        bool facingLeft = playerSprite != null ? playerSprite.flipX : false;
        
        // Position the particle point based on facing direction
        // Adjust these values based on your desired positioning
        float xOffset = facingLeft ? -staffRange : staffRange;
        float yOffset = 1f; // Height above player center
        
        stormParticlePoint.transform.localPosition = new Vector3(xOffset, yOffset, 0);
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
            
            // Update player facing direction
            playerSprite.flipX = mouseIsLeft;
        }
    }
    
    private void CreateDaggerStrike()
    {
        if (playerTransform == null) return;
        
        // Get player's facing direction
        SpriteRenderer playerSprite = GetComponent<SpriteRenderer>();
        if (playerSprite == null)
            playerSprite = GetComponentInChildren<SpriteRenderer>();
        
        bool facingLeft = playerSprite != null ? playerSprite.flipX : false;
        Vector3 attackPosition = playerTransform.position + (Vector3.right * (facingLeft ? -daggerRange : daggerRange));
        
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
        daggerRenderer.sortingOrder = 10;
        
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
        
        StartCoroutine(CreateProjectileDagger(startPosition, throwDirection));
    }
    
    private void RedirectDagger()
    {
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
            
            StartCoroutine(CreateRedirectAttack(nearestEnemy));
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
    
    private GameObject FindNearestEnemy()
    {
        if (playerTransform == null) return null;
        
        GameObject nearestEnemy = null;
        float nearestDistance = float.MaxValue;
        
        // Find all game objects with enemy-related components or tags
        GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        
        foreach (GameObject obj in allObjects)
        {
            // Check if object is an enemy (has enemy components or tags)
            if (IsEnemy(obj))
            {
                float distance = Vector3.Distance(playerTransform.position, obj.transform.position);
                if (distance <= enemyDetectionRange && distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestEnemy = obj;
                }
            }
        }
        
        return nearestEnemy;
    }
    
    private bool IsEnemy(GameObject obj)
    {
        // Check for enemy indicators - you can expand this based on your enemy system
        if (obj.CompareTag("Enemy")) return true;
        if (obj.name.ToLower().Contains("enemy")) return true;
        
        // Check layer (if enemies are on a specific layer)
        if (obj.layer == LayerMask.NameToLayer("Enemy")) return true;
        
        // You can add specific enemy component checks here when you know the actual enemy classes:
        // Example: if (obj.GetComponent<YourEnemyClass>() != null) return true;
        
        return false;
    }
    


    private IEnumerator CreateRedirectAttack(GameObject target)
    {
        if (currentThrownDagger == null || target == null) yield break;
        
        Rigidbody2D daggerRb = currentThrownDagger.GetComponent<Rigidbody2D>();
        if (daggerRb == null) yield break;
        
        // Change dagger color to indicate wind redirect (purple-ish wind effect)
        SpriteRenderer daggerRenderer = currentThrownDagger.GetComponent<SpriteRenderer>();
        if (daggerRenderer != null)
        {
            daggerRenderer.color = new Color(0.8f, 0.3f, 1f, 1f); // Purple wind effect
        }
        
        // Disable gravity for redirect but keep collision detection active
        daggerRb.gravityScale = 0f;
        // Ensure collider is still active for ground detection
        Collider2D daggerCollider = currentThrownDagger.GetComponent<Collider2D>();
        if (daggerCollider != null)
        {
            daggerCollider.isTrigger = true; // Ensure trigger detection works
        }
        
        // Calculate direction to target
        Vector3 directionToTarget = (target.transform.position - currentThrownDagger.transform.position).normalized;
        
        // Update dagger rotation to face redirect direction
        UpdateDaggerRotation(currentThrownDagger, directionToTarget);
        
        // Apply redirect velocity (faster than normal projectile)
        daggerRb.linearVelocity = directionToTarget * (recallSpeed * 1.2f);
        
        // Track the redirect for up to 1.5 seconds or until it hits the target
        float redirectTimer = 0f;
        float maxRedirectTime = 1.5f;
        
        while (currentThrownDagger != null && redirectTimer < maxRedirectTime)
        {
            redirectTimer += Time.deltaTime;
            
            if (target != null)
            {
                // Update direction to target (homing effect)
                Vector3 newDirection = (target.transform.position - currentThrownDagger.transform.position).normalized;
                daggerRb.linearVelocity = newDirection * (recallSpeed * 1.2f);
                
                // Update rotation to match redirect direction
                UpdateDaggerRotation(currentThrownDagger, newDirection);
                
                // Check if dagger passed through target
                float distanceToTarget = Vector3.Distance(currentThrownDagger.transform.position, target.transform.position);
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
                    DaggerRotationController rotationController = currentThrownDagger.GetComponent<DaggerRotationController>();
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
        if (currentThrownDagger != null)
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
            DaggerRotationController rotationController = currentThrownDagger.GetComponent<DaggerRotationController>();
            if (rotationController != null)
            {
                rotationController.enabled = true;
            }
            
            Debug.Log("WhisperShard: Redirect timed out, dagger returning to normal flight");
        }
        
        // If redirect expired without hitting target and max redirects reached, destroy dagger
        if (currentRedirectCount >= maxDaggerRedirects)
        {
            Debug.Log("WhisperShard: Dagger has expired after maximum redirects!");
            if (currentThrownDagger != null)
            {
                Destroy(currentThrownDagger);
                currentThrownDagger = null;
                daggerExpired = false; // Reset for next dagger
                currentRedirectCount = 0;
            }
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
        
        projectileRenderer.sortingOrder = 10;
        
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
    
    private void CreateElectricArc()
    {
        if (playerTransform == null || stormParticlePoint == null) return;
        
        // Update particle point position based on facing direction
        UpdateStormParticlePosition();
        
        // Find nearest enemy within range
        GameObject nearestEnemy = FindNearestEnemy(lightningRange);
        if (nearestEnemy == null)
        {
            return;
        }
        
        Vector3 startPos = stormParticlePoint.transform.position;
        Vector3 endPos = nearestEnemy.transform.position;
        
        // Check for obstacles between player and enemy
        if (IsPathBlocked(startPos, endPos))
        {
            return;
        }
        
        StartCoroutine(CreateLightningArc(startPos, endPos, nearestEnemy));
    }
    
    private void CreateLightningBolt()
    {
        if (playerTransform == null) return;
        
        // Find nearest enemy within bolt range
        GameObject nearestEnemy = FindNearestEnemy(boltRange);
        if (nearestEnemy == null)
        {
            return;
        }
        
        Vector3 skyPosition = new Vector3(playerTransform.position.x, playerTransform.position.y + boltHeight, 0);
        Vector3 groundPosition = new Vector3(nearestEnemy.transform.position.x, nearestEnemy.transform.position.y - 0.5f, 0); // Slightly below enemy to touch ground
        
        // Check if ground position is blocked by terrain
        if (IsGroundBlocked(groundPosition))
        {
            return;
        }
        
        StartCoroutine(CreateSkyBolt(skyPosition, groundPosition, nearestEnemy));
    }
    
    private GameObject FindNearestEnemy(float maxRange)
    {
        // Find all objects with EnemyBehavior component
        EnemyBehavior[] enemyBehaviors = FindObjectsByType<EnemyBehavior>(FindObjectsSortMode.None);
        if (enemyBehaviors.Length == 0) return null;
        
        GameObject nearest = null;
        float nearestDistance = float.MaxValue;
        
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
        
        return nearest;
    }
    
    private List<GameObject> FindNearbyEnemiesForChaining(GameObject attackedEnemy, float maxRange, int maxCount)
    {
        List<GameObject> nearbyEnemies = new List<GameObject>();
        if (attackedEnemy == null) return nearbyEnemies;
        
        // Find all objects with EnemyBehavior component
        EnemyBehavior[] enemyBehaviors = FindObjectsByType<EnemyBehavior>(FindObjectsSortMode.None);
        if (enemyBehaviors.Length == 0) return nearbyEnemies;
        
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
        glowRenderer.startWidth = useGifInsteadOfLineRenderer ? 0f : 0.3f; // Hide if using GIF
        glowRenderer.endWidth = useGifInsteadOfLineRenderer ? 0f : 0.2f; // Hide if using GIF
        glowRenderer.positionCount = 10;
        glowRenderer.sortingOrder = 13; // Behind main lightning
        glowRenderer.enabled = !useGifInsteadOfLineRenderer; // Disable entirely if using GIF
        
        // Create main (foreground) LineRenderer
        GameObject mainLightning = new GameObject("MainElectricArc");
        mainLightning.transform.SetParent(lightning.transform);
        mainLightning.transform.localPosition = Vector3.zero;
        
        LineRenderer lineRenderer = mainLightning.AddComponent<LineRenderer>();
        lineRenderer.material = CreateLightningMaterial();
        lineRenderer.startWidth = useGifInsteadOfLineRenderer ? 0f : 0.1f; // Hide if using GIF
        lineRenderer.endWidth = useGifInsteadOfLineRenderer ? 0f : 0.05f; // Hide if using GIF
        lineRenderer.positionCount = 10; // More points for bending effect
        lineRenderer.sortingOrder = 15; // In front of glow
        lineRenderer.enabled = !useGifInsteadOfLineRenderer; // Disable entirely if using GIF
        
        // Create bending arc points
        Vector3[] arcPoints = CreateBendingArc(startPos, endPos, 10);
        lineRenderer.SetPositions(arcPoints);
        glowRenderer.SetPositions(arcPoints); // Use same path for glow
        
        // Create GIF visual effect if enabled
        GameObject gifEffect = null;
        if (useGifInsteadOfLineRenderer && lightningArcGifPrefab != null)
        {
            gifEffect = SpawnLightningArcGif(startPos, endPos, arcPoints, lightning.transform);
        }
        
        // Deal damage to target
        EnemyBehavior enemyBehavior = target.GetComponent<EnemyBehavior>();
        if (enemyBehavior != null)
        {
            enemyBehavior.TakeDamage(lightningDamage);
            
            // Trigger chain lightning from this enemy (TEMPORARILY DISABLED FOR TESTING)
            // StartCoroutine(TriggerChainLightning(target, lightningDamage));
        }
        
        // Lightning visual effect duration
        yield return new WaitForSeconds(lightningDuration);
        
        // Clean up GIF effect if it exists
        if (gifEffect != null)
        {
            Destroy(gifEffect);
        }
        
        Destroy(lightning);
    }
    
    /// <summary>
    /// Spawns a GIF/animation effect along the lightning arc path
    /// </summary>
    private GameObject SpawnLightningArcGif(Vector3 startPos, Vector3 endPos, Vector3[] arcPoints, Transform parent)
    {
        if (lightningArcGifPrefab == null)
        {
            Debug.LogWarning("Lightning arc GIF prefab is not assigned!");
            return null;
        }
        
        // Calculate midpoint of the arc for positioning
        Vector3 midPoint = Vector3.Lerp(startPos, endPos, 0.5f);
        
        // Instantiate the GIF effect
        GameObject gifEffect = Instantiate(lightningArcGifPrefab, midPoint, Quaternion.identity, parent);
        
        // Calculate rotation to face along the arc direction
        Vector3 direction = (endPos - startPos).normalized;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        gifEffect.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        
        // Use uniform scaling to prevent distortion
        float uniformScale = gifScaleMultiplier;
        gifEffect.transform.localScale = new Vector3(uniformScale, uniformScale, 1f);
        
        Debug.Log($"Lightning scaled uniformly to {uniformScale}x{uniformScale} (was trying arc-based scaling)");
        
        // Ensure proper sorting order to appear above other elements
        SpriteRenderer gifRenderer = gifEffect.GetComponent<SpriteRenderer>();
        if (gifRenderer != null)
        {
            gifRenderer.sortingOrder = 16; // Above LineRenderer effects
        }
        
        // If it's an Animator-based GIF, ensure it starts playing
        Animator gifAnimator = gifEffect.GetComponent<Animator>();
        if (gifAnimator != null)
        {
            gifAnimator.enabled = true;
            
            // Try to trigger animation if it has parameters
            if (gifAnimator.runtimeAnimatorController != null)
            {
                // Try common trigger names
                if (gifAnimator.parameters.Length > 0)
                {
                    foreach (var param in gifAnimator.parameters)
                    {
                        if (param.type == AnimatorControllerParameterType.Trigger)
                        {
                            gifAnimator.SetTrigger(param.name);
                            Debug.Log($"Triggered animation parameter: {param.name}");
                            break;
                        }
                    }
                }
                
                // Force play from start
                gifAnimator.Play(0, 0, 0f);
                Debug.Log("Lightning GIF Animator found and started playing");
            }
        }
        else
        {
            // Check if it's using SimpleSpriteAnimation component
            SimpleSpriteAnimation spriteAnimation = gifEffect.GetComponent<SimpleSpriteAnimation>();
            if (spriteAnimation != null)
            {
                spriteAnimation.Play();
                Debug.Log("Lightning SimpleSpriteAnimation found and started playing");
            }
            else
            {
                // Check if it's a legacy animation component
                Animation legacyAnimation = gifEffect.GetComponent<Animation>();
                if (legacyAnimation != null)
                {
                    legacyAnimation.Play();
                    Debug.Log("Lightning GIF Legacy Animation found and started playing");
                }
            }
        }
        
        // Debug component information
        string componentInfo = "";
        if (gifEffect.GetComponent<Animator>() != null) componentInfo += "Animator ";
        if (gifEffect.GetComponent<Animation>() != null) componentInfo += "Animation ";
        if (gifEffect.GetComponent<SimpleSpriteAnimation>() != null) componentInfo += "SimpleSpriteAnimation ";
        if (gifEffect.GetComponent<SpriteRenderer>() != null) componentInfo += "SpriteRenderer ";
        
        Debug.Log($"Lightning GIF spawned at {midPoint} with rotation {angle}° and uniform scale {uniformScale}x{uniformScale}. Components: {componentInfo}");
        
        return gifEffect;
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
        chainGlowRenderer.startWidth = useGifInsteadOfLineRenderer ? 0f : 0.24f; // Hide if using GIF
        chainGlowRenderer.endWidth = useGifInsteadOfLineRenderer ? 0f : 0.16f; // Hide if using GIF
        chainGlowRenderer.positionCount = 8;
        chainGlowRenderer.sortingOrder = 12; // Behind main chain lightning
        chainGlowRenderer.enabled = !useGifInsteadOfLineRenderer; // Disable if using GIF
        
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
        lineRenderer.startWidth = useGifInsteadOfLineRenderer ? 0f : 0.08f; // Hide if using GIF
        lineRenderer.endWidth = useGifInsteadOfLineRenderer ? 0f : 0.04f; // Hide if using GIF
        lineRenderer.positionCount = 8; // Fewer points for quicker creation
        lineRenderer.sortingOrder = 14; // Slightly behind main lightning
        lineRenderer.enabled = !useGifInsteadOfLineRenderer; // Disable if using GIF
        
        // Create bending arc points for chain lightning
        Vector3[] arcPoints = CreateBendingArc(startPos, endPos, 8);
        lineRenderer.SetPositions(arcPoints);
        chainGlowRenderer.SetPositions(arcPoints); // Use same path for glow
        
        // Create GIF visual effect for chain arc if enabled
        GameObject chainGifEffect = null;
        if (useGifInsteadOfLineRenderer && lightningArcGifPrefab != null)
        {
            chainGifEffect = SpawnLightningArcGif(startPos, endPos, arcPoints, chainLightning.transform);
            
            // Make chain lightning GIF slightly smaller and tinted purple
            if (chainGifEffect != null)
            {
                chainGifEffect.transform.localScale *= 0.8f; // Smaller for chain lightning
                
                // Tint purple if possible
                SpriteRenderer chainRenderer = chainGifEffect.GetComponent<SpriteRenderer>();
                if (chainRenderer != null)
                {
                    chainRenderer.color = new Color(0.8f, 0.6f, 1f, 1f); // Purple tint
                    chainRenderer.sortingOrder = 14; // Same as chain LineRenderer
                }
            }
        }
        
        // Deal chain damage to target
        EnemyBehavior enemyBehavior = target.GetComponent<EnemyBehavior>();
        if (enemyBehavior != null)
        {
            enemyBehavior.TakeDamage(chainDamage);
            Debug.Log($"Chain lightning hit {target.name} for {chainDamage} damage");
        }
        
        // Chain arc visual effect duration
        yield return new WaitForSeconds(chainArcDuration);
        
        // Clean up chain GIF effect if it exists
        if (chainGifEffect != null)
        {
            Destroy(chainGifEffect);
        }
        
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
        boltGlowRenderer.sortingOrder = 13; // Behind main bolt
        
        // Create main lightning bolt LineRenderer
        GameObject mainBolt = new GameObject("MainLightningBolt");
        mainBolt.transform.SetParent(bolt.transform);
        mainBolt.transform.localPosition = Vector3.zero;
        
        LineRenderer lineRenderer = mainBolt.AddComponent<LineRenderer>();
        lineRenderer.material = CreateLightningMaterial();
        lineRenderer.startWidth = 0.2f;
        lineRenderer.endWidth = 0.1f;
        lineRenderer.positionCount = 6; // More points for subtle bending
        lineRenderer.sortingOrder = 15;
        
        // Create slightly squiggly lightning bolt path
        Vector3[] boltPoints = CreateBendingBolt(startPos, endPos, 6);
        lineRenderer.SetPositions(boltPoints);
        boltGlowRenderer.SetPositions(boltPoints); // Use same path for glow
        
        // Deal damage to target
        EnemyBehavior enemyBehavior = target.GetComponent<EnemyBehavior>();
        if (enemyBehavior != null)
        {
            enemyBehavior.TakeDamage(boltDamage);
            
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
        
        // Visual impact effect (lightning bolt sprite)
        SpriteRenderer impactRenderer = impact.AddComponent<SpriteRenderer>();
        
        // Use custom lightning bolt sprite if assigned, otherwise create fallback yellow texture
        if (lightningBoltSprite != null)
        {
            impactRenderer.sprite = lightningBoltSprite;
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
        impactRenderer.sortingOrder = 10;
        
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
        // Don't process clicks if already performing a special attack
        if (isPerformingSpecialAttack || isPerformingFlip)
            return;
            
        // Handle multi-click detection for ValorShard
        if (activeWeapon == ShardType.ValorShard)
        {
            float currentTime = Time.time;
            
            // Check if we should start a new sequence or continue existing one
            bool shouldStartNewSequence = (clickCount == 0 || currentTime - firstClickTime > multiClickWindow);
            
            Debug.Log($"Click timing debug - clickCount: {clickCount}, timeSinceFirst: {currentTime - firstClickTime:F3}, multiClickWindow: {multiClickWindow}, shouldStartNew: {shouldStartNewSequence}");
            
            if (shouldStartNewSequence)
            {
                // Don't allow new click sequences during special attacks
                if (isPerformingSpecialAttack)
                {
                    Debug.Log("Ignoring click - special attack in progress");
                    return;
                }
                
                clickCount = 1;
                firstClickTime = currentTime;
                
                Debug.Log($"First click registered - clickCount: {clickCount}");
                
                // Start immediate attack for responsiveness, but prepare for multi-click
                Debug.Log("Executing first attack immediately");
                UseActiveWeapon(false); // Immediate single attack
                Debug.Log("First attack executed");
                
                // Start checking for additional clicks
                StartCoroutine(CheckForAdditionalClicks());
            }
            else
            {
                // Continue existing sequence
                clickCount++;
                Debug.Log($"Additional click registered - clickCount: {clickCount}, timeDiff: {currentTime - firstClickTime:F3}");
                Debug.Log($"Current isPerformingSpecialAttack: {isPerformingSpecialAttack}");
                
                if (clickCount == 2)
                {
                    // Double click detected - perform dash attack
                    StopCoroutine("CheckForAdditionalClicks"); // Only stop the original check
                    
                    // Add a tiny delay to ensure first attack is visually distinct
                    StartCoroutine(DelayedDashAttack());
                    Debug.Log("Double click detected - performing dash attack");
                    
                    // Apply Valor Shard passive buffs for double-click
                    ApplyDoubleClickBuffs();
                    
                    // Don't start any new coroutines - let the normal multiClickWindow handle third click timing
                }
                else if (clickCount == 3)
                {
                    // Triple click detected - perform flip attack
                    PerformValorFlipAttack();
                    Debug.Log("Triple click detected - performing flip attack");
                    
                    // Apply Valor Shard passive buffs for triple-click
                    ApplyTripleClickBuffs();
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
                nextAutoFireTime = Time.time + lightningCooldown;
            }
        }
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
        yield return new WaitForSeconds(0.1f);
        isPerformingSpecialAttack = true;
        
        // Wait for a shorter dash duration for quicker sword swing
        yield return new WaitForSeconds(0.2f); // Reduced delay for quicker response
        
        // Ensure player movement is re-enabled before sword attack
        if (!playerMovement.enabled)
        {
            yield return new WaitForSeconds(0.1f); // Extra wait if still disabled
        }
        
        // Perform sword attack with cooldown bypass
        Debug.Log("About to execute dash sword swing - player movement enabled: " + playerMovement.enabled);
        UseActiveWeapon(false, true); // Bypass cooldown for dash sword swing
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
    
    private void PerformValorFlipAttack()
    {
        isPerformingSpecialAttack = true;
        isPerformingFlip = true;
        clickCount = 0; // Reset click count (flip is final attack, so safe to reset)
        flipStartTime = Time.time;
        
        Debug.Log("ValorShard: Flip attack initiated!");
        
        // Generate ultimate charge for valor triple click attack
        GenerateUltimateCharge(valorTripleClickCharge);
        
        // Get facing direction - use multiple methods to determine direction
        bool facingRight = GetActualFacingDirection();
        Vector2 flipDirection = facingRight ? Vector2.right : Vector2.left;
        Debug.Log("=== NEW DIRECTION DETECTION SYSTEM ACTIVE (FLIP) ===");
        Debug.Log($"Flip Facing Debug - ActualFacing: {facingRight}, IsFacingRight: {playerMovement.IsFacingRight()}, Direction Vector: {flipDirection}");
        
        // Apply upward and forward force (diagonal trajectory)
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            // Unfreeze Z rotation for flip animation
            rb.freezeRotation = false;
            
            // Clear existing velocity and apply diagonal force with more height
            rb.linearVelocity = Vector2.zero;
            Vector2 flipVelocity = new Vector2(flipDirection.x * dashForce * 1f, dashForce * 1.2f); // Increased upward velocity
            rb.linearVelocity = flipVelocity; // Use direct velocity instead of AddForce
            
            Debug.Log($"Flip Debug - Direction: {flipDirection}, FlipVelocity: {flipVelocity}, RB Mass: {rb.mass}");
        }
        
        // Create damage zone during flip
        CreateFlipDamageZone();
        
        // Start flip coroutine
        StartCoroutine(PerformFlipAnimation());
    }
    
    private System.Collections.IEnumerator PerformFlipAnimation()
    {
        float elapsedTime = 0f;
        Transform playerTransform = transform;
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        
        while (elapsedTime < flipDuration)
        {
            elapsedTime += Time.deltaTime;
            
            // Rotate player for flip effect (360 degrees over duration)
            float rotationProgress = elapsedTime / flipDuration;
            float currentRotation = rotationProgress * 360f;
            playerTransform.rotation = Quaternion.Euler(0, 0, currentRotation);
            
            yield return null;
        }
        
        // Ensure player ends upright and freeze rotation again
        playerTransform.rotation = Quaternion.identity;
        if (rb != null)
        {
            rb.freezeRotation = true; // Re-freeze rotation after flip
            rb.angularVelocity = 0f; // Stop any residual rotation
        }
        
        // Clean up flip attack
        if (flipDamageZone != null)
        {
            Destroy(flipDamageZone);
            flipDamageZone = null;
        }
        
        isPerformingFlip = false;
        isPerformingSpecialAttack = false;
        
        Debug.Log("ValorShard: Flip attack completed!");
    }
    
    private void CreateFlipDamageZone()
    {
        // Create damage zone that follows the player during flip
        GameObject damageZone = new GameObject("ValorFlipDamage");
        damageZone.transform.SetParent(transform);
        damageZone.transform.localPosition = Vector3.zero;
        
        // Add collider
        BoxCollider2D collider = damageZone.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = new Vector2(swordRange * 1.5f, swordRange * 1.5f); // Larger damage area
        
        // Add damage component
        DamageObject damageComponent = damageZone.AddComponent<DamageObject>();
        damageComponent.damageAmount = playerMovement.GetModifiedMeleeDamage((int)(swordDamage * 1.5f)); // 50% more damage
        damageComponent.damageRate = 0.1f; // Fast damage rate for flip attack
        
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
        
        // Visual indicator (semi-transparent golden/yellow for flip attack)
        SpriteRenderer flipRenderer = damageZone.AddComponent<SpriteRenderer>();
        
        // Create golden rectangle sprite for flip attack (scaled to match collider)
        float flipSize = swordRange * 1.5f;
        int textureWidth = Mathf.RoundToInt(flipSize * 64); 
        int textureHeight = Mathf.RoundToInt(flipSize * 64); 
        Texture2D flipTexture = new Texture2D(textureWidth, textureHeight);
        Color[] pixels = new Color[textureWidth * textureHeight];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = new Color(1f, 0f, 0f, 0.7f); // Red transparent like other Valor attacks
        }
        flipTexture.SetPixels(pixels);
        flipTexture.Apply();
        
        flipRenderer.sprite = Sprite.Create(flipTexture, new Rect(0, 0, textureWidth, textureHeight), Vector2.one * 0.5f);
        flipRenderer.sortingOrder = 10;
        
        flipDamageZone = damageZone;
        
        Debug.Log("Flip damage zone created with damage: " + damageComponent.damageAmount);
    }
    
    // ===== VALOR SHARD PASSIVE BUFF SYSTEM =====
    // Comprehensive buff application methods that integrate with PlayerMovement buff system
    
    /// <summary>
    /// Apply double-click buffs: 5% aegis shield + 5 durability for 10 seconds
    /// </summary>
    private void ApplyDoubleClickBuffs()
    {
        if (equippedShards[activeSlotIndex] != ShardType.ValorShard || playerMovement == null) return;
        
        // Apply aegis shield buff (fixed amount based on configuration)
        playerMovement.ApplyAegisBuff();
        
        // Apply durability buff (flat health increase)
        playerMovement.ApplyBuff(BuffType.Durability, doubleClickBuffDuration);
        
        Debug.Log($"Valor Double-Click: Applied {doubleClickAegisPercent}% Aegis Shield + {doubleClickDurabilityAmount} Durability for {doubleClickBuffDuration}s");
    }
    
    /// <summary>
    /// Apply triple-click buffs: 15% attack buff for 5 seconds
    /// </summary>
    private void ApplyTripleClickBuffs()
    {
        if (equippedShards[activeSlotIndex] != ShardType.ValorShard || playerMovement == null) return;
        
        // Apply strength buff (attack damage increase)
        playerMovement.ApplyBuff(BuffType.Strength, tripleClickBuffDuration);
        
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
            playerMovement.ApplyBuff(BuffType.Strength, killBuffDuration);
            
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
            // Apply aegis shield buff
            playerMovement.ApplyAegisBuff();
            
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
        
        // Optional: Change dagger color to indicate it's stuck
        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.color = new Color(0.8f, 0.8f, 0.8f, 0.9f); // Slightly grayed out
        }
    }
}