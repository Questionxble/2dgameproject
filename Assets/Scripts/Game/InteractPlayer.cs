using UnityEngine;

public class PlayerInteract : MonoBehaviour
{
    public KeyCode interactKey = KeyCode.E;

    private Interactable currentInteractable;

    void Update()
    {
        if (currentInteractable != null && Input.GetKeyDown(interactKey))
        {
            currentInteractable.Interact();
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        currentInteractable = other.GetComponent<Interactable>();
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponent<Interactable>() == currentInteractable)
            currentInteractable = null;
    }
}
