using UnityEngine;

public class Destructor : MonoBehaviour
{
    public int damage = 1;

    void OnCollisionEnter2D(Collision2D collision)
    {
        Destructible destructible = collision.gameObject.GetComponent<Destructible>();

        if (destructible != null)
            destructible.TakeDamage(damage);
    }
}
