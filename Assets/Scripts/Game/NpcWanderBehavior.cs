using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class NpcWanderBehavior : MonoBehaviour
{
    private static Sprite invisibleAttackSprite;

    private enum NpcState
    {
        Idle,
        Wandering,
        ChasingEnemy,
        Fleeing
    }

    [Header("Wandering")]
    [SerializeField] private float wanderRadius = 6f;
    [SerializeField] private float moveSpeed = 2.5f;
    [SerializeField] private Vector2 idleDurationRange = new Vector2(1f, 3f);
    [SerializeField] private float waypointReachedDistance = 0.45f;

    [Header("Enemy Response")]
    [SerializeField] private float enemyDetectionRadius = 6f;
    [SerializeField] [Range(0f, 1f)] private float fleeChance = 0.55f;
    [SerializeField] private float chaseSpeedMultiplier = 1.2f;
    [SerializeField] private float fleeSpeedMultiplier = 1.65f;
    [SerializeField] private float fleeDuration = 2.5f;
    [SerializeField] private float disengageDistance = 8f;
    [SerializeField] private float responseDecisionCooldown = 2f;

    [Header("Jumping")]
    [SerializeField] private float jumpForce = 8f;
    [SerializeField] private float jumpCooldown = 1f;
    [SerializeField] private float obstacleCheckDistance = 1.2f;
    [SerializeField] private float gapCheckDistance = 1.25f;
    [SerializeField] private LayerMask groundLayerMask = 1;

    [Header("Combat")]
    [SerializeField] private float attackRange = 1.6f;
    [SerializeField] private float attackCooldown = 1.25f;
    [SerializeField] private float attackDamage = 12f;
    [SerializeField] private float attackHitboxLifetime = 0.3f;
    [SerializeField] private Vector2 attackHitboxSize = new Vector2(1.5f, 1.5f);

    [Header("References")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Animator animator;

    private readonly HashSet<string> animatorParameters = new HashSet<string>();

    private Rigidbody2D rb;
    private Vector3 homePosition;
    private Vector3 wanderTarget;
    private EnemyBehavior currentEnemy;
    private NpcState currentState;
    private bool isAttacking;
    private float lastJumpTime;
    private float lastAttackTime;
    private float idleUntilTime;
    private float fleeUntilTime;
    private float nextDecisionTime;

    private bool UsesDynamicPhysicsMovement
    {
        get { return rb != null && rb.bodyType == RigidbodyType2D.Dynamic; }
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        CacheAnimatorParameters();
        homePosition = transform.position;
        BeginIdle();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        Collider2D ownCollider = GetComponent<Collider2D>();
        if (ownCollider == null || collision.collider == null)
        {
            return;
        }

        GameObject other = collision.gameObject;
        if (other.CompareTag("Player") || other.CompareTag("Enemy") || other.CompareTag("PlayerSummon"))
        {
            Physics2D.IgnoreCollision(ownCollider, collision.collider);
        }
    }

    private void Update()
    {
        UpdateEnemyDecision();
        UpdateStateMachine();
        UpdateAnimationState();
    }

    private void UpdateEnemyDecision()
    {
        if (Time.time < nextDecisionTime)
        {
            return;
        }

        if (currentState == NpcState.Fleeing && IsEnemyValid(currentEnemy))
        {
            return;
        }

        EnemyBehavior nearestEnemy = FindNearestEnemy();
        if (!IsEnemyValid(nearestEnemy))
        {
            if (currentState == NpcState.ChasingEnemy)
            {
                currentEnemy = null;
                BeginIdle();
            }

            return;
        }

        currentEnemy = nearestEnemy;
        nextDecisionTime = Time.time + responseDecisionCooldown;

        if (Random.value <= fleeChance)
        {
            currentState = NpcState.Fleeing;
            fleeUntilTime = Time.time + fleeDuration;
            return;
        }

        currentState = NpcState.ChasingEnemy;
    }

    private void UpdateStateMachine()
    {
        switch (currentState)
        {
            case NpcState.Idle:
                if (Time.time >= idleUntilTime)
                {
                    PickWanderTarget();
                    currentState = NpcState.Wandering;
                }
                else
                {
                    DampHorizontalVelocity();
                }
                break;

            case NpcState.Wandering:
                if (Mathf.Abs(wanderTarget.x - transform.position.x) <= waypointReachedDistance)
                {
                    BeginIdle();
                }
                else
                {
                    MoveTowards(wanderTarget.x, 1f);
                }
                break;

            case NpcState.ChasingEnemy:
                if (!IsEnemyValid(currentEnemy) || Vector3.Distance(transform.position, currentEnemy.transform.position) > disengageDistance)
                {
                    currentEnemy = null;
                    BeginIdle();
                    break;
                }

                float attackDistance = Vector3.Distance(transform.position, currentEnemy.transform.position);
                if (attackDistance <= attackRange)
                {
                    DampHorizontalVelocity();

                    if (Time.time >= lastAttackTime + attackCooldown)
                    {
                        AttackEnemy(currentEnemy.gameObject);
                    }
                }
                else
                {
                    MoveTowards(currentEnemy.transform.position.x, chaseSpeedMultiplier);
                }
                break;

            case NpcState.Fleeing:
                if (!IsEnemyValid(currentEnemy) || Time.time >= fleeUntilTime || Vector3.Distance(transform.position, currentEnemy.transform.position) > disengageDistance)
                {
                    currentEnemy = null;
                    BeginIdle();
                    break;
                }

                float directionAwayFromEnemy = transform.position.x >= currentEnemy.transform.position.x ? 1f : -1f;
                MoveInDirection(directionAwayFromEnemy, fleeSpeedMultiplier);
                break;
        }
    }

    private void BeginIdle()
    {
        currentState = NpcState.Idle;
        idleUntilTime = Time.time + Random.Range(idleDurationRange.x, idleDurationRange.y);
    }

    private void PickWanderTarget()
    {
        float randomOffset = Random.Range(-wanderRadius, wanderRadius);
        wanderTarget = new Vector3(homePosition.x + randomOffset, transform.position.y, transform.position.z);
    }

    private void MoveTowards(float targetX, float speedMultiplier)
    {
        float deltaX = targetX - transform.position.x;
        if (Mathf.Abs(deltaX) <= 0.05f)
        {
            DampHorizontalVelocity();
            return;
        }

        float direction = Mathf.Sign(deltaX);
        MoveInDirection(direction, speedMultiplier);
    }

    private void MoveInDirection(float direction, float speedMultiplier)
    {
        if (Mathf.Abs(direction) <= 0.01f)
        {
            DampHorizontalVelocity();
            return;
        }

        if (ShouldJump(direction))
        {
            PerformJump();
        }

        float targetSpeed = moveSpeed * speedMultiplier;
        if (UsesDynamicPhysicsMovement)
        {
            Vector2 moveForce = new Vector2(direction * targetSpeed * 15f, 0f);
            rb.AddForce(moveForce, ForceMode2D.Force);

            if (Mathf.Abs(rb.linearVelocity.x) > targetSpeed * 1.15f)
            {
                rb.linearVelocity = new Vector2(Mathf.Sign(rb.linearVelocity.x) * targetSpeed * 1.15f, rb.linearVelocity.y);
            }
        }
        else
        {
            transform.position += new Vector3(direction * targetSpeed * Time.deltaTime, 0f, 0f);
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = direction < 0f;
        }
    }

    private void DampHorizontalVelocity()
    {
        if (!UsesDynamicPhysicsMovement)
        {
            return;
        }

        rb.linearVelocity = new Vector2(Mathf.Lerp(rb.linearVelocity.x, 0f, Time.deltaTime * 10f), rb.linearVelocity.y);
    }

    private bool ShouldJump(float direction)
    {
        if (!UsesDynamicPhysicsMovement)
        {
            return false;
        }

        if (Time.time < lastJumpTime + jumpCooldown || !IsGrounded())
        {
            return false;
        }

        Vector3 obstacleRayStart = transform.position + Vector3.up * 0.5f;
        Vector2 obstacleDirection = new Vector2(Mathf.Sign(direction), 0f);
        RaycastHit2D obstacleHit = Physics2D.Raycast(obstacleRayStart, obstacleDirection, obstacleCheckDistance, groundLayerMask);
        if (obstacleHit.collider != null)
        {
            return true;
        }

        Vector3 gapRayStart = transform.position + new Vector3(Mathf.Sign(direction) * 0.7f, 0.1f, 0f);
        RaycastHit2D groundHit = Physics2D.Raycast(gapRayStart, Vector2.down, gapCheckDistance, groundLayerMask);
        return groundHit.collider == null;
    }

    private bool IsGrounded()
    {
        if (!UsesDynamicPhysicsMovement)
        {
            return true;
        }

        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, 1.1f, groundLayerMask);
        return hit.collider != null && Mathf.Abs(rb.linearVelocity.y) < 0.15f;
    }

    private void PerformJump()
    {
        if (!UsesDynamicPhysicsMovement)
        {
            return;
        }

        lastJumpTime = Time.time;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
    }

    private void AttackEnemy(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        lastAttackTime = Time.time;
        isAttacking = true;

        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = target.transform.position.x < transform.position.x;
        }

        StartCoroutine(AttackRoutine(target));
    }

    private IEnumerator AttackRoutine(GameObject target)
    {
        Vector3 direction = target.transform.position.x >= transform.position.x ? Vector3.right : Vector3.left;
        Vector3 attackPosition = transform.position + direction * (attackRange * 0.65f);

        GameObject attackObject = new GameObject(gameObject.name + "_NpcAttack");
        attackObject.transform.position = attackPosition;

        BoxCollider2D attackCollider = attackObject.AddComponent<BoxCollider2D>();
        attackCollider.size = attackHitboxSize;
        attackCollider.isTrigger = true;

        DamageObject damageObject = attackObject.AddComponent<DamageObject>();
        damageObject.damageAmount = Mathf.RoundToInt(attackDamage);
        damageObject.damageRate = 0.1f;
        damageObject.canDamageEnemies = true;

        int playerLayer = LayerMask.NameToLayer("Player");
        int playerSummonLayer = LayerMask.NameToLayer("PlayerSummon");
        LayerMask excludeMask = 0;
        if (playerLayer != -1)
        {
            excludeMask |= 1 << playerLayer;
        }

        if (playerSummonLayer != -1)
        {
            excludeMask |= 1 << playerSummonLayer;
        }

        damageObject.excludeLayers = excludeMask;

        SpriteRenderer attackRenderer = attackObject.AddComponent<SpriteRenderer>();
        attackRenderer.sprite = CreateInvisibleAttackSprite();
        attackRenderer.sortingOrder = 8;

        yield return new WaitForSeconds(attackHitboxLifetime);

        if (attackObject != null)
        {
            Destroy(attackObject);
        }

        isAttacking = false;
    }

    private EnemyBehavior FindNearestEnemy()
    {
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, enemyDetectionRadius);
        EnemyBehavior nearestEnemy = null;
        float nearestDistance = float.MaxValue;

        for (int i = 0; i < colliders.Length; i++)
        {
            EnemyBehavior enemy = colliders[i].GetComponent<EnemyBehavior>();
            if (!IsEnemyValid(enemy))
            {
                continue;
            }

            float distance = Vector3.Distance(transform.position, enemy.transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestEnemy = enemy;
            }
        }

        return nearestEnemy;
    }

    private bool IsEnemyValid(EnemyBehavior enemy)
    {
        return enemy != null && !enemy.IsDead;
    }

    private void UpdateAnimationState()
    {
        bool isWalking = UsesDynamicPhysicsMovement ? Mathf.Abs(rb.linearVelocity.x) > 0.1f : currentState != NpcState.Idle;
        TrySetAnimatorBool("isWalking", isWalking);
        TrySetAnimatorBool("isAttacking", isAttacking);
    }

    private void CacheAnimatorParameters()
    {
        animatorParameters.Clear();
        if (animator == null)
        {
            return;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            animatorParameters.Add(parameters[i].name);
        }
    }

    private void TrySetAnimatorBool(string parameterName, bool value)
    {
        if (animator == null || !animatorParameters.Contains(parameterName))
        {
            return;
        }

        animator.SetBool(parameterName, value);
    }

    private Sprite CreateInvisibleAttackSprite()
    {
        if (invisibleAttackSprite != null)
        {
            return invisibleAttackSprite;
        }

        Texture2D texture = new Texture2D(32, 32);
        Color[] pixels = new Color[32 * 32];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = new Color(0f, 0.8f, 1f, 0f);
        }

        texture.SetPixels(pixels);
        texture.Apply();
        invisibleAttackSprite = Sprite.Create(texture, new Rect(0f, 0f, 32f, 32f), Vector2.one * 0.5f);
        return invisibleAttackSprite;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.9f, 0.5f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, wanderRadius);

        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, enemyDetectionRadius);

        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}