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
        Interactable interactable = other.GetComponent<Interactable>();

        if (interactable != null)
            currentInteractable = interactable;
    }

    void OnTriggerExit2D(Collider2D other)
    {
        Interactable interactable = other.GetComponent<Interactable>();

        if (interactable != null && interactable == currentInteractable)
            currentInteractable = null;
    }

}
