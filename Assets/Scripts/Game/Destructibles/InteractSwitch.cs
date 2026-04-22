using UnityEngine;

public class InteractSwitch : Interactable
{
    public GameObject door; // The secret door object

    public override void Interact()
    {
        if (door != null)
            door.SetActive(false); // or play animation, slide open, etc.
    }
}
