using UnityEngine;

public class EnemyRangedProjectile : MonoBehaviour
{
    private int damage;
    private float petrificationDuration;
    private float lifetime;
    private bool initialized;
    private GameObject owner;

    public void Initialize(int projectileDamage, float petrifyDuration, float projectileLifetime, GameObject projectileOwner)
    {
        damage = Mathf.Max(0, projectileDamage);
        petrificationDuration = Mathf.Max(0f, petrifyDuration);
        lifetime = Mathf.Max(0.1f, projectileLifetime);
        owner = projectileOwner;
        initialized = true;

        Collider2D ownerCollider = owner != null ? owner.GetComponent<Collider2D>() : null;
        Collider2D projectileCollider = GetComponent<Collider2D>();
        if (ownerCollider != null && projectileCollider != null)
        {
            Physics2D.IgnoreCollision(projectileCollider, ownerCollider, true);
        }

        Destroy(gameObject, lifetime);
    }

    private void Awake()
    {
        // Safety cleanup for prefabs dropped in scene without Initialize calls.
        Destroy(gameObject, 10f);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!initialized || other == null)
        {
            return;
        }

        if (owner != null && (other.gameObject == owner || other.transform.IsChildOf(owner.transform)))
        {
            return;
        }

        PlayerMovement player = other.GetComponent<PlayerMovement>();
        if (player != null)
        {
            if (damage > 0)
            {
                player.TakeDamageFromObject(damage);
            }

            if (petrificationDuration > 0f)
            {
                player.ApplyPetrification(petrificationDuration);
            }

            Destroy(gameObject);
            return;
        }

        AttackDummy dummy = other.GetComponent<AttackDummy>();
        if (dummy != null)
        {
            if (damage > 0)
            {
                dummy.TakeDamage(damage);
            }

            Destroy(gameObject);
            return;
        }

        if (IsTerrain(other))
        {
            Destroy(gameObject);
        }
    }

    private bool IsTerrain(Collider2D other)
    {
        if (other.CompareTag("Ground") || other.CompareTag("Surface") || other.CompareTag("Terrain"))
        {
            return true;
        }

        int ground = LayerMask.NameToLayer("Ground");
        int surface = LayerMask.NameToLayer("Surface");
        int terrain = LayerMask.NameToLayer("Terrain");
        int hitLayer = other.gameObject.layer;
        return hitLayer == ground || hitLayer == surface || hitLayer == terrain;
    }
}
