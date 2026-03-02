using UnityEngine;

public class ParallaxLayer : MonoBehaviour
{
    public float speed = 0.1f;
    private Vector3 startPos;

    void Start()
    {
        startPos = transform.position;
    }

    void Update()
    {
        float offset = Mathf.Sin(Time.time * speed) * 0.5f;
        transform.position = startPos + new Vector3(offset, 0, 0);
    }
}
