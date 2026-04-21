using System.Collections;
using UnityEngine;

public class GorgonEnemyBehavior : EnemyBehavior
{
    [Header("Medusa Stare Melee Stun")]
    [SerializeField] private Vector2 stareBoxSize = new Vector2(3f, 2f);
    [SerializeField] private Vector3 stareBoxOffset = new Vector3(1f, 0f, 0f);
    [SerializeField] private float petrificationDuration = 5f;

    private bool firedStareThisAttack = false;
    private Coroutine stareFallbackCoroutine = null;

    protected override void Start()
    {
        isAggressive = true;
        base.Start();
    }

    protected override int GetAttackAnimationType()
    {
        return 5;
    }

    protected override void OnAttackAnimationStarted(Transform target)
    {
        firedStareThisAttack = false;
    }

    protected override void StartAttackEventFallback()
    {
        if (stareFallbackCoroutine != null)
        {
            StopCoroutine(stareFallbackCoroutine);
        }

        stareFallbackCoroutine = StartCoroutine(StareAttackEventFallback());
    }

    protected override void ExecuteAttack(Transform target)
    {
        // Gorgon attacks resolve from the animation event or its fallback.
    }

    private IEnumerator StareAttackEventFallback()
    {
        float fallbackDelay = Mathf.Max(0.05f, attackDuration * 0.45f);
        yield return new WaitForSeconds(fallbackDelay);

        if (!isDead && isAttacking && !firedStareThisAttack)
        {
            MedusaStareAnimationEvent();
        }

        stareFallbackCoroutine = null;
    }

    public void MedusaStareAnimationEvent()
    {
        if (isDead || firedStareThisAttack)
        {
            return;
        }

        Vector3 boxOffset = stareBoxOffset;
        if (!isFacingRight)
        {
            boxOffset.x = -boxOffset.x;
        }

        Vector3 boxCenter = transform.position + boxOffset;
        Collider2D[] hitColliders = Physics2D.OverlapBoxAll(boxCenter, stareBoxSize, 0f);

        foreach (Collider2D hit in hitColliders)
        {
            if (hit == null || hit.gameObject == gameObject)
            {
                continue;
            }

            PlayerMovement targetPlayer = hit.GetComponent<PlayerMovement>();
            if (targetPlayer != null)
            {
                targetPlayer.ApplyPetrification(petrificationDuration);
                Debug.Log($"Gorgon stare petrified player for {petrificationDuration}s");
                continue;
            }

            AttackDummy dummy = hit.GetComponent<AttackDummy>();
            if (dummy != null)
            {
                dummy.ApplyPetrification(petrificationDuration);
                Debug.Log($"Gorgon stare petrified attack dummy for {petrificationDuration}s");
            }
        }

        firedStareThisAttack = true;

        if (stareFallbackCoroutine != null)
        {
            StopCoroutine(stareFallbackCoroutine);
            stareFallbackCoroutine = null;
        }
    }

    protected override void ResetVariantState()
    {
        firedStareThisAttack = false;

        if (stareFallbackCoroutine != null)
        {
            StopCoroutine(stareFallbackCoroutine);
            stareFallbackCoroutine = null;
        }
    }
}