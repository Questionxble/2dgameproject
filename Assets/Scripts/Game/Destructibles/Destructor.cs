using UnityEngine;

public class Destructor : MonoBehaviour
{
    public int damage = 1;

    void OnCollisionEnter2D(Collision2D collision)
    {
        Destructible destructible = collision.gameObject.GetComponent<Destructible>();
        Debug.Log("Chandelier hit: " + collision.gameObject.name);

        if (destructible != null)
            destructible.TakeDamage(damage);
    }
}
