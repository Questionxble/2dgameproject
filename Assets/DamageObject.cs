using UnityEngine;

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
}