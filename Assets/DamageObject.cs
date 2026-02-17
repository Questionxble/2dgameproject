using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[System.Serializable]
public class DamageObject : MonoBehaviour
{
    [Header("Damage Settings")]
    [Tooltip("Amount of damage dealt to the player")]
    public int damageAmount = 20;
    
    [Tooltip("Time interval between damage applications (in seconds)")]
    public float damageRate = 1f;
    
    [Tooltip("Apply damage when player enters trigger")]
    [SerializeField] private bool damageOnTrigger = true;
    
    [Tooltip("Apply damage when player collides")]
    [SerializeField] private bool damageOnCollision = true;
    
    [Tooltip("If true, this damage object will not damage objects on the Player layer")]
    [SerializeField] private bool excludePlayerLayer = false;
    
    [Tooltip("If true, this damage object can damage enemies")]
    public bool canDamageEnemies = true;
    
    [Tooltip("If true, this damage object can damage player summons (allies)")]
    public bool canDamagePlayerSummons = true;
    
    [Tooltip("LayerMask of objects that should NOT be damaged by this object")]
    public LayerMask excludeLayers = 0;
    
    // Callback system for weapon passives
    public System.Action onEnemyHit;
    
    // Callback system for fire particle effects
    public System.Action<Transform> onPlayerHit;
    

    
    private bool playerInside = false;
    private bool enemyInside = false;
    private bool playerSummonInside = false;
    private float lastDamageTime = -1f;
    private float lastEnemyDamageTime = -1f;
    private float lastPlayerSummonDamageTime = -1f;
    private PlayerMovement playerMovement;
    private Transform playerTransform;
    private EnemyBehavior currentEnemy;
    private AttackDummy currentPlayerSummon;
    
    void Start()
    {
        // Ensure we have a collider
        Collider2D col = GetComponent<Collider2D>();
        if (col == null)
        {
            BoxCollider2D boxCol = gameObject.AddComponent<BoxCollider2D>();
            boxCol.isTrigger = true;
        }
        
        // Find player reference
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            playerMovement = player.GetComponent<PlayerMovement>();
            playerTransform = player.transform;
        }
    }
    
    void Update()
    {
        // Apply continuous damage while player is inside
        if (playerInside && Time.time - lastDamageTime >= damageRate)
        {
            DealDamageToPlayer();
        }
        
        // Apply continuous damage while enemy is inside
        if (enemyInside && canDamageEnemies && Time.time - lastEnemyDamageTime >= damageRate)
        {
            // Double-check that we should still be damaging enemies
            if (currentEnemy != null && currentEnemy.CompareTag("Enemy") && !canDamageEnemies)
            {
                enemyInside = false;
                currentEnemy = null;
                return;
            }
            
            // Apply damage to enemy
            DealDamageToEnemy();
        }
        
        // Apply continuous damage while player summon is inside
        if (playerSummonInside && canDamagePlayerSummons && Time.time - lastPlayerSummonDamageTime >= damageRate)
        {
            // Apply damage to player summon
            DealDamageToPlayerSummon();
        }
    }
    
    void OnTriggerEnter2D(Collider2D other)
    {
        // Check if object is in excluded layers
        if (excludeLayers != 0 && ((1 << other.gameObject.layer) & excludeLayers) != 0)
        {
            return; // Don't damage objects in excluded layers
        }
        
        if (damageOnTrigger && other.CompareTag("Player"))
        {
            // Check if we should exclude player layer
            if (excludePlayerLayer && other.gameObject.layer == LayerMask.NameToLayer("Player"))
            {
                return; // Don't damage player layer objects
            }
            
            playerInside = true;
            DealDamageToPlayer();
        }
        else if (damageOnTrigger && other.GetComponent<EnemyBehavior>() != null)
        {
            // Additional safety check: don't damage objects tagged as "Enemy" unless specifically allowed
            if (other.CompareTag("Enemy") && !canDamageEnemies)
            {
                return;
            }
            
            if (canDamageEnemies)
            {
                enemyInside = true;
                currentEnemy = other.GetComponent<EnemyBehavior>();
                DealDamageToEnemy();
            }
        }
        else if (damageOnTrigger && other.CompareTag("PlayerSummon"))
        {
            if (canDamagePlayerSummons)
            {
                playerSummonInside = true;
                currentPlayerSummon = other.GetComponent<AttackDummy>();
                DealDamageToPlayerSummon();
            }
        }
    }
    
    void OnTriggerExit2D(Collider2D other)
    {
        if (damageOnTrigger && other.CompareTag("Player"))
        {
            playerInside = false;
        }
        else if (damageOnTrigger && other.GetComponent<EnemyBehavior>() != null)
        {
            enemyInside = false;
            currentEnemy = null;
        }
        else if (damageOnTrigger && other.CompareTag("PlayerSummon"))
        {
            playerSummonInside = false;
            currentPlayerSummon = null;
        }
    }
    
    void OnCollisionEnter2D(Collision2D collision)
    {
        // Check if object is in excluded layers
        if (excludeLayers != 0 && ((1 << collision.gameObject.layer) & excludeLayers) != 0)
        {
            return; // Don't damage objects in excluded layers
        }
        
        if (damageOnCollision && collision.gameObject.CompareTag("Player"))
        {
            // Check if we should exclude player layer
            if (excludePlayerLayer && collision.gameObject.layer == LayerMask.NameToLayer("Player"))
            {
                return; // Don't damage player layer objects
            }
            
            playerInside = true; // Set playerInside so continuous damage works
            DealDamageToPlayer();
        }
        else if (damageOnCollision && canDamageEnemies && collision.gameObject.GetComponent<EnemyBehavior>() != null)
        {
            // Additional safety check: don't damage objects tagged as "Enemy" unless specifically allowed
            if (collision.gameObject.CompareTag("Enemy") && !canDamageEnemies)
            {
                return;
            }
            
            enemyInside = true;
            currentEnemy = collision.gameObject.GetComponent<EnemyBehavior>();
            DealDamageToEnemy();
        }
        else if (damageOnCollision && canDamagePlayerSummons && collision.gameObject.CompareTag("PlayerSummon"))
        {
            playerSummonInside = true;
            currentPlayerSummon = collision.gameObject.GetComponent<AttackDummy>();
            DealDamageToPlayerSummon();
        }
    }
    
    void OnCollisionExit2D(Collision2D collision)
    {
        if (damageOnCollision && collision.gameObject.CompareTag("Player"))
        {
            playerInside = false; // Stop continuous damage
        }
        else if (damageOnCollision && collision.gameObject.GetComponent<EnemyBehavior>() != null)
        {
            enemyInside = false;
            currentEnemy = null;
        }
        else if (damageOnCollision && collision.gameObject.CompareTag("PlayerSummon"))
        {
            playerSummonInside = false;
            currentPlayerSummon = null;
        }
    }
    
    private void DealDamageToPlayer()
    {
        if (playerMovement != null && Time.time - lastDamageTime >= damageRate)
        {
            playerMovement.TakeDamageFromObject(damageAmount);
            lastDamageTime = Time.time;
            
            // Create damage number
            CreateDamageNumber(damageAmount, playerTransform.position + Vector3.up * 1.5f);
            
            // Trigger callback for fire particle effects
            if (onPlayerHit != null)
            {
                Debug.Log($"DamageObject: Triggering onPlayerHit callback for {name}");
                onPlayerHit.Invoke(playerTransform);
            }
        }
    }
    
    private void DealDamageToEnemy()
    {
        if (currentEnemy != null && Time.time - lastEnemyDamageTime >= damageRate)
        {
            currentEnemy.TakeDamage(damageAmount);
            lastEnemyDamageTime = Time.time;
            
            // Create damage number
            CreateDamageNumber(damageAmount, currentEnemy.transform.position + Vector3.up * 1.5f);
            
            // Trigger callback for weapon passives
            onEnemyHit?.Invoke();
        }
    }
    
    private void DealDamageToPlayerSummon()
    {
        if (currentPlayerSummon != null && Time.time - lastPlayerSummonDamageTime >= damageRate)
        {
            currentPlayerSummon.TakeDamage(damageAmount);
            lastPlayerSummonDamageTime = Time.time;
            
            // Create damage number
            CreateDamageNumber(damageAmount, currentPlayerSummon.transform.position + Vector3.up * 1.5f);
            
            Debug.Log($"DamageObject: Dealt {damageAmount} damage to PlayerSummon {currentPlayerSummon.name}");
            
            // Trigger callback for fire particle effects (same as player)
            if (onPlayerHit != null)
            {
                Debug.Log($"DamageObject: Triggering onPlayerHit callback for PlayerSummon {currentPlayerSummon.name}");
                onPlayerHit.Invoke(currentPlayerSummon.transform);
            }
        }
    }
    
    /// <summary>
    /// Public method to deal damage externally (useful for scripted events)
    /// </summary>
    public void TriggerDamage()
    {
        DealDamageToPlayer();
        if (canDamageEnemies)
        {
            DealDamageToEnemy();
        }
    }
    
    /// <summary>
    /// Change damage amount at runtime
    /// </summary>
    public void SetDamageAmount(int newAmount)
    {
        damageAmount = newAmount;
    }
    
    /// <summary>
    /// Change damage rate at runtime
    /// </summary>
    public void SetDamageRate(float newRate)
    {
        damageRate = newRate;
    }
    
    /// <summary>
    /// Create floating damage number at the target position
    /// </summary>
    public static void CreateDamageNumber(int damage, Vector3 worldPosition)
    {
        // Create canvas for the damage number
        GameObject damageNumberObj = new GameObject("DamageNumber");
        damageNumberObj.transform.position = worldPosition;
        
        // Add Canvas component
        Canvas canvas = damageNumberObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        canvas.sortingLayerName = "UI";
        canvas.sortingOrder = 1000; // Very high to appear above everything
        
        // Scale the canvas appropriately for world space
        RectTransform canvasRect = damageNumberObj.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(200, 100);
        canvasRect.localScale = Vector3.one * 0.01f; // Scale down for world space
        
        // Create the text object
        GameObject textObj = new GameObject("DamageText");
        textObj.transform.SetParent(damageNumberObj.transform, false);
        
        // Add Text component
        Text damageText = textObj.AddComponent<Text>();
        damageText.text = damage.ToString();
        damageText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        damageText.fontSize = 60; // Large size for visibility
        damageText.color = Color.red; // Red damage numbers
        damageText.alignment = TextAnchor.MiddleCenter;
        damageText.fontStyle = FontStyle.Bold;
        
        // Add white outline using Outline component
        Outline outline = textObj.AddComponent<Outline>();
        outline.effectColor = Color.white;
        outline.effectDistance = new Vector2(3, 3); // Thick white stroke
        
        // Set the text RectTransform
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.sizeDelta = new Vector2(200, 100);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        
        // Start the animation coroutine
        MonoBehaviour coroutineRunner = Camera.main?.GetComponent<MonoBehaviour>();
        if (coroutineRunner == null)
        {
            // Fallback: find any MonoBehaviour in scene
            coroutineRunner = Object.FindObjectOfType<MonoBehaviour>();
        }
        
        if (coroutineRunner != null)
        {
            coroutineRunner.StartCoroutine(AnimateDamageNumber(damageNumberObj, damageText));
        }
        else
        {
            // Fallback: just destroy after delay
            Object.Destroy(damageNumberObj, 0.8f);
        }
    }
    
    /// <summary>
    /// Animate the damage number with fade out and upward movement
    /// </summary>
    private static System.Collections.IEnumerator AnimateDamageNumber(GameObject damageObj, Text damageText)
    {
        float duration = 0.8f;
        float elapsedTime = 0f;
        
        Vector3 startPos = damageObj.transform.position;
        Vector3 endPos = startPos + Vector3.up * 1.5f; // Move up 1.5 units
        
        Color startColor = damageText.color;
        Outline outline = damageText.GetComponent<Outline>();
        Color startOutlineColor = outline.effectColor;
        
        while (elapsedTime < duration)
        {
            float t = elapsedTime / duration;
            
            // Move upward
            damageObj.transform.position = Vector3.Lerp(startPos, endPos, t);
            
            // Fade out
            float alpha = 1f - t;
            damageText.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
            outline.effectColor = new Color(startOutlineColor.r, startOutlineColor.g, startOutlineColor.b, alpha);
            
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        // Destroy the damage number
        Object.Destroy(damageObj);
    }
}