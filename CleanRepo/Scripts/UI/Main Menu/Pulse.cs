using UnityEngine;

public class Pulse : MonoBehaviour
{
    public float speed = 2f;
    public float minAlpha = 0.3f;
    public float maxAlpha = 0.8f;

    private SpriteRenderer sr;

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        float alpha = Mathf.Lerp(minAlpha, maxAlpha, (Mathf.Sin(Time.time * speed) + 1) / 2);
        Color c = sr.color;
        c.a = alpha;
        sr.color = c;
    }
}
