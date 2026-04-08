using UnityEngine;

public class DropChandelier : Interactable
{
    public Rigidbody2D chandelierRb;

    public override void Interact()
    {
        chandelierRb.bodyType = RigidbodyType2D.Dynamic;
    }
}
