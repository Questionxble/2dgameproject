using UnityEngine;

public class Destructible : MonoBehaviour
{
    [Header("Optional Settings")]
    public int health = 1;
    public GameObject breakEffect;
    public AudioClip breakSound;

    public void TakeDamage(int amount)
    {
        health -= amount;

        if (health <= 0)
            Break();
    }

    public void Break()
    {
        if (breakEffect != null)
            Instantiate(breakEffect, transform.position, Quaternion.identity);

        if (breakSound != null)
            AudioSource.PlayClipAtPoint(breakSound, transform.position);

        Destroy(gameObject);
    }
}
