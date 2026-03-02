
using UnityEngine;
using UnityEngine.UIElements;
namespace PistonMedia {
// Sample Camera Movement to pan around the scenery. 
public class FantasticCameraMovement: MonoBehaviour {
    public float speed = 15.0f;
    public float speedMultiplier = 2.5f;
    // Camera bounds, so the camera wont go outside of the level bounds.
    public Vector2 minBounds = new Vector2(-10f, -5f);
    public Vector2 maxBounds = new Vector2(10f, 5f);
    void Update()
    {
        float moveSpeed = speed;

        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            moveSpeed *= speedMultiplier;
        }

        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        Vector3 movement = new Vector3(horizontal, vertical, 0) * moveSpeed * Time.deltaTime;
        // clamp the movement by the set bounds. 
        transform.position += movement;
        Vector3 clampedPos = transform.position;
        clampedPos.x = Mathf.Clamp(clampedPos.x, minBounds.x, maxBounds.x);
        clampedPos.y = Mathf.Clamp(clampedPos.y, minBounds.y, maxBounds.y);
        transform.position = clampedPos;
    }
}
}