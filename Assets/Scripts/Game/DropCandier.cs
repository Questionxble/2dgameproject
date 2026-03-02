using UnityEngine;

public class ChandelierBreak : MonoBehaviour
{
    public Rigidbody2D rb;
    public GameObject floorToBreak;

    void Start()
    {
        // Optional: start as kinematic so it doesn't fall immediately
        rb.bodyType = RigidbodyType2D.Kinematic;
    }

    public void DropChandelier()
    {
        rb.bodyType = RigidbodyType2D.Dynamic;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        Debug.Log("Hit: " + collision.gameObject.name);

        if (collision.gameObject.CompareTag("BreakableFloor"))
        {
            Destroy(floorToBreak);
        }
    }

}
