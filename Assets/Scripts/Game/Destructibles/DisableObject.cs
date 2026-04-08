using UnityEngine;
using UnityEngine.Tilemaps;

public class DisableTarget : Interactable
{
    public GameObject targetObject;
    public Tilemap targetTilemap;

    public override void Interact()
    {
        if (targetTilemap != null)
            targetTilemap.gameObject.SetActive(false);

        if (targetObject != null)
            targetObject.SetActive(false);
    }
}
