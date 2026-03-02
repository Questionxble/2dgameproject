using UnityEngine;
namespace PistonMedia {
public class ParallaxBackground : MonoBehaviour
{
    private float length;
    public Camera mainCamera;
    private Vector2 startPosition;
    public float parallaxEffect = 1f; // 0 = near, 1 = far
    public float verticalParallaxMultiplier = 0.5f; // vertical parallax, default half of the speed. 1 equals the speed of the horizontal 0 Stopped.

    private void Start() {
        startPosition = transform.position;
        if (mainCamera == null) {
            mainCamera = Camera.main;
        }
    }

    private void LateUpdate() {
        if (mainCamera != null) {
            float distanceX = mainCamera.transform.position.x * parallaxEffect;
            float distanceY = (mainCamera.transform.position.y * parallaxEffect) *  verticalParallaxMultiplier;
            transform.position = new Vector3(startPosition.x + distanceX, startPosition.y + distanceY, transform.position.z);
        }
    }
}
}