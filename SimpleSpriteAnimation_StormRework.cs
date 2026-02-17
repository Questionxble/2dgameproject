using UnityEngine;

public class SimpleSpriteAnimation : MonoBehaviour
{
    [Header("Sprite Animation Settings")]
    [SerializeField] private Sprite[] animationFrames;
    [SerializeField] private float frameRate = 12f; // Frames per second
    [SerializeField] private bool loop = true;
    [SerializeField] private bool playOnStart = true;
    
    private SpriteRenderer spriteRenderer;
    private int currentFrame = 0;
    private float timer = 0f;
    private bool isPlaying = false;
    
    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        
        if (spriteRenderer == null)
        {
            Debug.LogError("SimpleSpriteAnimation requires a SpriteRenderer component!");
            return;
        }
        
        if (animationFrames.Length == 0)
        {
            Debug.LogError("No animation frames assigned to SimpleSpriteAnimation!");
            return;
        }
        
        // Verify all frames are assigned
        for (int i = 0; i < animationFrames.Length; i++)
        {
            if (animationFrames[i] == null)
            {
                Debug.LogError($"Animation frame {i} is null! Please assign all frames.");
                return;
            }
        }
        
        // Set first frame only
        spriteRenderer.sprite = animationFrames[0];
        
        if (playOnStart)
        {
            Play();
        }
        
        Debug.Log($"SimpleSpriteAnimation initialized with {animationFrames.Length} frames at {frameRate} FPS. First frame: {animationFrames[0].name}");
    }
    
    void Update()
    {
        if (!isPlaying || animationFrames.Length == 0) return;
        
        timer += Time.deltaTime;
        
        if (timer >= (1f / frameRate))
        {
            timer = 0f;
            currentFrame++;
            
            if (currentFrame >= animationFrames.Length)
            {
                if (loop)
                {
                    currentFrame = 0;
                }
                else
                {
                    isPlaying = false;
                    currentFrame = animationFrames.Length - 1;
                }
            }
            
            spriteRenderer.sprite = animationFrames[currentFrame];
            Debug.Log($"SimpleSpriteAnimation: Frame {currentFrame} - {animationFrames[currentFrame].name}");
        }
    }
    
    public void Play()
    {
        isPlaying = true;
        currentFrame = 0;
        timer = 0f;
        if (spriteRenderer != null && animationFrames.Length > 0)
        {
            spriteRenderer.sprite = animationFrames[0];
        }
    }
    
    public void Stop()
    {
        isPlaying = false;
    }
    
    public void Pause()
    {
        isPlaying = false;
    }
    
    public void Resume()
    {
        isPlaying = true;
    }
}