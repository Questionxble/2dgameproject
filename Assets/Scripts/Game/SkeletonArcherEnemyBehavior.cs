using System.Collections;
using UnityEngine;

public class SkeletonArcherEnemyBehavior : EnemyBehavior
{
    [Header("Ranged Attack")]
    [SerializeField] private GameObject arrowPrefab = null;
    [SerializeField] private float projectileSpeed = 18f;
    [SerializeField] private float projectileLifetime = 4f;
    [SerializeField] private Vector3 projectileSpawnOffset = new Vector3(0f, 0.35f, 0f);
    [SerializeField] private float rangedAttackRange = 8f;
    [SerializeField] private int meleeComboLength = 3;

    private Transform pendingRangedTarget = null;
    private bool firedProjectileThisAttack = false;
    private Coroutine rangedAttackFallbackCoroutine = null;
    private bool useRangedAttack = true;
    private int nextMeleeAttackType = 0;

    protected override void Start()
    {
        isAggressive = true;
        detectionRadius = Mathf.Max(detectionRadius, rangedAttackRange);
        base.Start();
    }

    protected override int GetAttackAnimationType()
    {
        if (useRangedAttack)
        {
            return 5;
        }

        if (!alternateAttacks)
        {
            return 0;
        }

        int attackType = nextMeleeAttackType;
        nextMeleeAttackType = (nextMeleeAttackType + 1) % Mathf.Max(1, meleeComboLength);
        return attackType;
    }

    protected override void HandleAggression()
    {
        if (rb == null)
        {
            return;
        }

        Transform nearestTarget = FindNearestTarget();
        if (nearestTarget == null)
        {
            useRangedAttack = false;
            ResetMeleeCombo();
            return;
        }

        float distanceToTarget = Vector3.Distance(transform.position, nearestTarget.position);
        if (distanceToTarget > detectionRadius)
        {
            StopHorizontalMovement();
            useRangedAttack = false;
            ResetMeleeCombo();
            return;
        }

        FaceTarget(nearestTarget);

        if (distanceToTarget <= attackRange)
        {
            useRangedAttack = false;
            StopHorizontalMovement();

            if (Time.time - lastAttackTime >= attackCooldown)
            {
                AttackTarget(nearestTarget);
            }

            return;
        }

        ResetMeleeCombo();

        if (distanceToTarget <= Mathf.Max(rangedAttackRange, attackRange + 0.1f))
        {
            useRangedAttack = true;
            StopHorizontalMovement();

            if (Time.time - lastAttackTime >= attackCooldown)
            {
                AttackTarget(nearestTarget);
            }

            return;
        }

        useRangedAttack = true;
        FollowTarget(nearestTarget);
    }

    protected override void OnAttackAnimationStarted(Transform target)
    {
        if (!useRangedAttack)
        {
            pendingRangedTarget = null;
            firedProjectileThisAttack = false;
            return;
        }

        pendingRangedTarget = target;
        firedProjectileThisAttack = false;
    }

    protected override void StartAttackEventFallback()
    {
        if (!useRangedAttack)
        {
            if (rangedAttackFallbackCoroutine != null)
            {
                StopCoroutine(rangedAttackFallbackCoroutine);
                rangedAttackFallbackCoroutine = null;
            }

            return;
        }

        if (rangedAttackFallbackCoroutine != null)
        {
            StopCoroutine(rangedAttackFallbackCoroutine);
        }

        rangedAttackFallbackCoroutine = StartCoroutine(RangedAttackEventFallback());
    }

    protected override void ExecuteAttack(Transform target)
    {
        if (useRangedAttack)
        {
            return;
        }

        base.ExecuteAttack(target);
    }

    private IEnumerator RangedAttackEventFallback()
    {
        float fallbackDelay = Mathf.Max(0.05f, attackDuration * 0.45f);
        yield return new WaitForSeconds(fallbackDelay);

        if (!isDead && isAttacking && !firedProjectileThisAttack)
        {
            ShootArrowAnimationEvent();
        }

        rangedAttackFallbackCoroutine = null;
    }

    public void ShootArrowAnimationEvent()
    {
        if (isDead || firedProjectileThisAttack)
        {
            return;
        }

        Transform target = pendingRangedTarget != null ? pendingRangedTarget : FindNearestTarget();
        if (target == null)
        {
            return;
        }

        Vector3 spawnOffset = projectileSpawnOffset;
        if (!isFacingRight)
        {
            spawnOffset.x = -spawnOffset.x;
        }

        Vector3 spawnPosition = transform.position + spawnOffset;
        Vector3 travelDirection = (target.position - spawnPosition).normalized;
        if (travelDirection.sqrMagnitude <= 0.001f)
        {
            travelDirection = isFacingRight ? Vector3.right : Vector3.left;
        }

        GameObject projectile = arrowPrefab != null
            ? Instantiate(arrowPrefab, spawnPosition, Quaternion.identity)
            : new GameObject("SkeletonArcherArrow");

        projectile.transform.position = spawnPosition;

        Rigidbody2D projectileRb = projectile.GetComponent<Rigidbody2D>();
        if (projectileRb == null)
        {
            projectileRb = projectile.AddComponent<Rigidbody2D>();
        }

        projectileRb.gravityScale = 0f;
        projectileRb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        projectileRb.linearVelocity = travelDirection * projectileSpeed;

        Collider2D projectileCollider = projectile.GetComponent<Collider2D>();
        if (projectileCollider == null)
        {
            projectileCollider = projectile.AddComponent<BoxCollider2D>();
        }

        projectileCollider.isTrigger = true;

        EnemyRangedProjectile enemyProjectile = projectile.GetComponent<EnemyRangedProjectile>();
        if (enemyProjectile == null)
        {
            enemyProjectile = projectile.AddComponent<EnemyRangedProjectile>();
        }

        enemyProjectile.Initialize((int)attackDamage, 0f, projectileLifetime, gameObject);

        if (projectile.GetComponent<DaggerRotationController>() == null)
        {
            projectile.AddComponent<DaggerRotationController>();
        }

        firedProjectileThisAttack = true;
        pendingRangedTarget = null;

        if (rangedAttackFallbackCoroutine != null)
        {
            StopCoroutine(rangedAttackFallbackCoroutine);
            rangedAttackFallbackCoroutine = null;
        }
    }

    protected override void ResetVariantState()
    {
        pendingRangedTarget = null;
        firedProjectileThisAttack = false;
        useRangedAttack = true;
        ResetMeleeCombo();

        if (rangedAttackFallbackCoroutine != null)
        {
            StopCoroutine(rangedAttackFallbackCoroutine);
            rangedAttackFallbackCoroutine = null;
        }
    }

    private void StopHorizontalMovement()
    {
        if (rb != null)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }
    }

    private void ResetMeleeCombo()
    {
        nextMeleeAttackType = 0;
    }
}