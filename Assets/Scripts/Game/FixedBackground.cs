using UnityEngine;

public class FixedBackground : MonoBehaviour
{
    [Header("Background Sprite")]
    [SerializeField] private Transform backgroundSprite;
    
    [Header("Background Settings")]
    [SerializeField] private bool followCameraX = true;
    [SerializeField] private bool followCameraY = true;
    [SerializeField] private Vector3 offset = Vector3.zero;
    [SerializeField] private float parallaxFactorX = 0f;
    [SerializeField] private float parallaxFactorY = 0f;
    
    [Header("Background Scaling")]
    [SerializeField] private bool autoScale = true;
    [SerializeField] private float scaleMultiplier = 1.2f;
    
    private Camera thisCamera;
    private SpriteRenderer backgroundSpriteRenderer;
    private Vector3 initialCameraPosition;
    private Vector3 initialBackgroundPosition;
    
    void Start()
    {
        thisCamera = GetComponent<Camera>();
        if (thisCamera == null)
        {
            Debug.LogError("FixedBackground: This script must be attached to a Camera!");
            return;
        }
        
        if (backgroundSprite == null)
        {
            backgroundSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (backgroundSpriteRenderer != null)
            {
                backgroundSprite = backgroundSpriteRenderer.transform;
            }
        }
        else
        {
            backgroundSpriteRenderer = backgroundSprite.GetComponent<SpriteRenderer>();
        }
        
        if (backgroundSprite == null || backgroundSpriteRenderer == null)
        {
            Debug.LogError("FixedBackground: No background sprite found as child of camera!");
            return;
        }
        
        // Ensure the background sprite is enabled and visible
        backgroundSprite.gameObject.SetActive(true);
        backgroundSpriteRenderer.enabled = true;
        
        initialCameraPosition = transform.position;
        initialBackgroundPosition = backgroundSprite.position;
        
        SetupBackground();
    }
    
    void Update()
    {
        if (backgroundSprite == null || thisCamera == null) return;
        
        UpdateBackgroundPosition();
    }
    
    private void SetupBackground()
    {
        backgroundSpriteRenderer.sortingLayerName = "Background";
        backgroundSpriteRenderer.sortingOrder = -1; // Use -1 as you specified
        backgroundSpriteRenderer.color = Color.white;
        
        if (autoScale && backgroundSpriteRenderer.sprite != null)
        {
            ScaleBackgroundToCamera();
        }
        
        UpdateBackgroundPosition();
    }
    
    private void ScaleBackgroundToCamera()
    {
        if (thisCamera == null || backgroundSpriteRenderer.sprite == null) return;
        
        float cameraHeight = thisCamera.orthographicSize * 2f;
        float cameraWidth = cameraHeight * thisCamera.aspect;
        
        Vector2 spriteSize = backgroundSpriteRenderer.sprite.bounds.size;
        
        float scaleX = (cameraWidth / spriteSize.x) * scaleMultiplier;
        float scaleY = (cameraHeight / spriteSize.y) * scaleMultiplier;
        
        float scale = Mathf.Max(scaleX, scaleY);
        
        backgroundSprite.localScale = Vector3.one * scale;
    }
    
    private void UpdateBackgroundPosition()
    {
        Vector3 cameraPos = transform.position;
        Vector3 newPosition = new Vector3();
        
        if (followCameraX)
        {
            if (parallaxFactorX == 0)
            {
                newPosition.x = cameraPos.x;
            }
            else
            {
                Vector3 cameraDelta = cameraPos - initialCameraPosition;
                newPosition.x = initialBackgroundPosition.x + (cameraDelta.x * parallaxFactorX);
            }
        }
        else
        {
            newPosition.x = initialBackgroundPosition.x;
        }
        
        if (followCameraY)
        {
            if (parallaxFactorY == 0)
            {
                newPosition.y = cameraPos.y;
            }
            else
            {
                Vector3 cameraDelta = cameraPos - initialCameraPosition;
                newPosition.y = initialBackgroundPosition.y + (cameraDelta.y * parallaxFactorY);
            }
        }
        else
        {
            newPosition.y = initialBackgroundPosition.y;
        }
        
        newPosition.z = cameraPos.z - 1f; // Keep behind camera but not too far
        newPosition += offset;
        
        // Ensure Z position is appropriate for 2D (not too far behind)
        if (newPosition.z < -10f)
        {
            newPosition.z = -1f;
        }
        
        backgroundSprite.position = newPosition;
    }
    
    public void SetBackgroundSprite(Sprite newSprite)
    {
        if (backgroundSpriteRenderer != null)
        {
            backgroundSpriteRenderer.sprite = newSprite;
            if (autoScale)
            {
                ScaleBackgroundToCamera();
            }
        }
    }
    
    public void SetBackgroundSpriteTransform(Transform spriteTransform)
    {
        backgroundSprite = spriteTransform;
        if (backgroundSprite != null)
        {
            backgroundSpriteRenderer = backgroundSprite.GetComponent<SpriteRenderer>();
            initialBackgroundPosition = backgroundSprite.position;
            
            if (autoScale && backgroundSpriteRenderer != null && backgroundSpriteRenderer.sprite != null)
            {
                ScaleBackgroundToCamera();
            }
        }
    }
}
