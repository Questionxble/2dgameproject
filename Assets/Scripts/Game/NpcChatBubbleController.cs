using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class NpcChatBubbleController : MonoBehaviour
{
    [Header("Bubble Anchor")]
    [SerializeField] private Transform bubbleAnchor;
    [SerializeField] private Vector3 bubbleAnchorOffset = new Vector3(0f, 2f, 0f);

    [Header("Bubble Appearance")]
    [SerializeField] private int maxMessageLength = 120;
    [SerializeField] private float worldBubbleMinWidth = 110f;
    [SerializeField] private float worldBubbleMaxWidth = 320f;
    [SerializeField] private float worldBubbleMinHeight = 44f;
    [SerializeField] private Vector2 bubblePadding = new Vector2(16f, 10f);
    [SerializeField] private int maxVisibleBubbles = 3;
    [SerializeField] private float compactBubbleSpacing = 10f;
    [SerializeField] private float compactBubbleLifetime = 6f;
    [SerializeField] private float compactBubbleFadeDuration = 1.15f;
    [SerializeField] private float compactBubbleSlideDistance = 28f;

    private const string BuiltInFontName = "LegacyRuntime.ttf";

    private static Sprite roundedBubbleSprite;

    private Canvas bubbleCanvas;
    private RectTransform bubbleCanvasRect;
    private Font bubbleFont;
    private readonly List<CompactBubble> compactBubbles = new List<CompactBubble>();

    private sealed class CompactBubble
    {
        public GameObject Root;
        public RectTransform RectTransform;
        public CanvasGroup CanvasGroup;
        public float Height;
        public float CreatedAt;
    }

    private void OnEnable()
    {
        EnsureBubbleUI();
    }

    private void LateUpdate()
    {
        if (bubbleCanvas == null)
        {
            return;
        }

        UpdateBubbleCanvasTransform();
        UpdateCompactBubbles();
    }

    private void OnDisable()
    {
        CleanupBubbleUI();
    }

    public void ShowMessage(string rawMessage)
    {
        string sanitizedMessage = SanitizeMessage(rawMessage);
        if (string.IsNullOrEmpty(sanitizedMessage))
        {
            return;
        }

        EnsureBubbleUI();
        CreateCompactBubble(sanitizedMessage);
        UpdateCompactBubbleLayout(Time.unscaledTime, Time.unscaledDeltaTime);
    }

    private void EnsureBubbleUI()
    {
        if (bubbleCanvas != null)
        {
            return;
        }

        bubbleFont = GetPreferredFont();

        GameObject canvasGO = new GameObject(gameObject.name + "_NpcBubbleCanvas");
        bubbleCanvas = canvasGO.AddComponent<Canvas>();
        bubbleCanvas.renderMode = RenderMode.WorldSpace;
        bubbleCanvas.sortingOrder = 14;

        bubbleCanvasRect = bubbleCanvas.GetComponent<RectTransform>();
        bubbleCanvasRect.anchorMin = new Vector2(0.5f, 0f);
        bubbleCanvasRect.anchorMax = new Vector2(0.5f, 0f);
        bubbleCanvasRect.pivot = new Vector2(0.5f, 0f);
        bubbleCanvasRect.sizeDelta = new Vector2(worldBubbleMaxWidth + 32f, 240f);
        bubbleCanvasRect.localScale = Vector3.one * 0.01f;

        CanvasGroup bubbleCanvasGroup = canvasGO.AddComponent<CanvasGroup>();
        bubbleCanvasGroup.blocksRaycasts = false;
        bubbleCanvasGroup.interactable = false;

        UpdateBubbleCanvasTransform();
    }

    private void CleanupBubbleUI()
    {
        compactBubbles.Clear();

        if (bubbleCanvas != null)
        {
            Destroy(bubbleCanvas.gameObject);
            bubbleCanvas = null;
            bubbleCanvasRect = null;
        }
    }

    private void CreateCompactBubble(string message)
    {
        if (bubbleCanvasRect == null)
        {
            return;
        }

        GameObject bubbleGO = new GameObject("NpcBubble");
        bubbleGO.transform.SetParent(bubbleCanvasRect, false);

        RectTransform bubbleRect = bubbleGO.AddComponent<RectTransform>();
        bubbleRect.anchorMin = new Vector2(0.5f, 0f);
        bubbleRect.anchorMax = new Vector2(0.5f, 0f);
        bubbleRect.pivot = new Vector2(0.5f, 0f);
        bubbleRect.sizeDelta = new Vector2(worldBubbleMaxWidth, worldBubbleMinHeight);

        CanvasGroup bubbleGroup = bubbleGO.AddComponent<CanvasGroup>();
        bubbleGroup.alpha = 1f;
        bubbleGroup.blocksRaycasts = false;
        bubbleGroup.interactable = false;

        Image bubbleBackground = bubbleGO.AddComponent<Image>();
        bubbleBackground.sprite = GetRoundedBubbleSprite();
        bubbleBackground.type = Image.Type.Sliced;
        bubbleBackground.color = new Color(1f, 1f, 1f, 0.82f);
        bubbleBackground.raycastTarget = false;

        Outline bubbleOutline = bubbleGO.AddComponent<Outline>();
        bubbleOutline.effectColor = new Color(0f, 0f, 0f, 0.94f);
        bubbleOutline.effectDistance = new Vector2(2.2f, -2.2f);
        bubbleOutline.useGraphicAlpha = true;

        Shadow bubbleShadow = bubbleGO.AddComponent<Shadow>();
        bubbleShadow.effectColor = new Color(0f, 0f, 0f, 0.15f);
        bubbleShadow.effectDistance = new Vector2(0f, -2f);
        bubbleShadow.useGraphicAlpha = true;

        GameObject textGO = new GameObject("BubbleText");
        textGO.transform.SetParent(bubbleGO.transform, false);

        Text bubbleText = textGO.AddComponent<Text>();
        bubbleText.font = bubbleFont;
        bubbleText.fontSize = 16;
        bubbleText.color = Color.black;
        bubbleText.alignment = TextAnchor.MiddleCenter;
        bubbleText.horizontalOverflow = HorizontalWrapMode.Wrap;
        bubbleText.verticalOverflow = VerticalWrapMode.Overflow;
        bubbleText.supportRichText = false;
        bubbleText.raycastTarget = false;
        bubbleText.text = EscapeRichText(message);

        RectTransform textRect = bubbleText.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(bubblePadding.x, bubblePadding.y);
        textRect.offsetMax = new Vector2(-bubblePadding.x, -bubblePadding.y);

        float bubbleWidth = CalculateBubbleWidth(bubbleText.preferredWidth);
        bubbleRect.sizeDelta = new Vector2(bubbleWidth, worldBubbleMinHeight);
        LayoutRebuilder.ForceRebuildLayoutImmediate(textRect);
        float bubbleHeight = Mathf.Max(worldBubbleMinHeight, bubbleText.preferredHeight + (bubblePadding.y * 2f));
        bubbleRect.sizeDelta = new Vector2(bubbleWidth, bubbleHeight);

        compactBubbles.Add(new CompactBubble
        {
            Root = bubbleGO,
            RectTransform = bubbleRect,
            CanvasGroup = bubbleGroup,
            Height = bubbleHeight,
            CreatedAt = Time.unscaledTime
        });

        while (compactBubbles.Count > maxVisibleBubbles)
        {
            if (compactBubbles[0].Root != null)
            {
                Destroy(compactBubbles[0].Root);
            }

            compactBubbles.RemoveAt(0);
        }
    }

    private void UpdateCompactBubbles()
    {
        if (compactBubbles.Count == 0)
        {
            return;
        }

        float currentTime = Time.unscaledTime;
        for (int i = compactBubbles.Count - 1; i >= 0; i--)
        {
            CompactBubble bubble = compactBubbles[i];
            if (bubble == null || bubble.Root == null)
            {
                compactBubbles.RemoveAt(i);
                continue;
            }

            if (currentTime - bubble.CreatedAt < compactBubbleLifetime)
            {
                continue;
            }

            Destroy(bubble.Root);
            compactBubbles.RemoveAt(i);
        }

        if (compactBubbles.Count > 0)
        {
            UpdateCompactBubbleLayout(currentTime, Time.unscaledDeltaTime);
        }
    }

    private void UpdateCompactBubbleLayout(float currentTime, float deltaTime)
    {
        float fadeDuration = Mathf.Max(0.01f, Mathf.Min(compactBubbleFadeDuration, compactBubbleLifetime));
        float fadeStartTime = compactBubbleLifetime - fadeDuration;
        float smoothing = deltaTime > 0f ? 1f - Mathf.Exp(-18f * deltaTime) : 1f;
        float stackedHeight = 0f;

        for (int i = compactBubbles.Count - 1; i >= 0; i--)
        {
            CompactBubble bubble = compactBubbles[i];
            float age = currentTime - bubble.CreatedAt;
            float fadeProgress = age <= fadeStartTime ? 0f : Mathf.Clamp01((age - fadeStartTime) / fadeDuration);
            float targetY = stackedHeight + (fadeProgress * compactBubbleSlideDistance);
            Vector2 targetPosition = new Vector2(0f, targetY);

            bubble.CanvasGroup.alpha = 1f - fadeProgress;
            bubble.RectTransform.anchoredPosition = Vector2.Lerp(bubble.RectTransform.anchoredPosition, targetPosition, smoothing);

            stackedHeight += bubble.Height + compactBubbleSpacing;
        }
    }

    private void UpdateBubbleCanvasTransform()
    {
        if (bubbleCanvas == null)
        {
            return;
        }

        bubbleCanvas.transform.position = GetAnchorWorldPosition();

        if (Camera.main != null)
        {
            bubbleCanvas.transform.LookAt(Camera.main.transform);
            bubbleCanvas.transform.Rotate(0f, 180f, 0f);
        }
    }

    private Vector3 GetAnchorWorldPosition()
    {
        Transform anchor = bubbleAnchor != null ? bubbleAnchor : transform;
        return anchor.position + bubbleAnchorOffset;
    }

    private float CalculateBubbleWidth(float preferredTextWidth)
    {
        float paddedWidth = preferredTextWidth + (bubblePadding.x * 2f);
        return Mathf.Clamp(paddedWidth, worldBubbleMinWidth, worldBubbleMaxWidth);
    }

    private string SanitizeMessage(string rawMessage)
    {
        if (string.IsNullOrWhiteSpace(rawMessage))
        {
            return string.Empty;
        }

        string sanitized = rawMessage.Trim();
        sanitized = sanitized.Replace('\n', ' ');
        sanitized = sanitized.Replace('\r', ' ');

        while (sanitized.Contains("  "))
        {
            sanitized = sanitized.Replace("  ", " ");
        }

        if (sanitized.Length > maxMessageLength)
        {
            sanitized = sanitized.Substring(0, maxMessageLength);
        }

        return sanitized;
    }

    private static string EscapeRichText(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Replace("<", "‹").Replace(">", "›");
    }

    private Font GetPreferredFont()
    {
        if (bubbleFont != null)
        {
            return bubbleFont;
        }

        string[] preferredFonts =
        {
            "Trebuchet MS",
            "Segoe UI",
            "Verdana",
            "Arial"
        };

        for (int i = 0; i < preferredFonts.Length; i++)
        {
            try
            {
                Font osFont = Font.CreateDynamicFontFromOSFont(preferredFonts[i], 18);
                if (osFont != null)
                {
                    return osFont;
                }
            }
            catch
            {
            }
        }

        return Resources.GetBuiltinResource<Font>(BuiltInFontName);
    }

    private static Sprite GetRoundedBubbleSprite()
    {
        if (roundedBubbleSprite != null)
        {
            return roundedBubbleSprite;
        }

        const int textureSize = 32;
        const int cornerRadius = 10;

        Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.ARGB32, false);
        texture.name = "RoundedNpcBubbleTexture";
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color clear = new Color(1f, 1f, 1f, 0f);
        Color fill = Color.white;

        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                bool inside = IsInsideRoundedRect(x, y, textureSize, cornerRadius);
                texture.SetPixel(x, y, inside ? fill : clear);
            }
        }

        texture.Apply();
        roundedBubbleSprite = Sprite.Create(texture, new Rect(0f, 0f, textureSize, textureSize), new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect, new Vector4(cornerRadius, cornerRadius, cornerRadius, cornerRadius));
        return roundedBubbleSprite;
    }

    private static bool IsInsideRoundedRect(int x, int y, int size, int radius)
    {
        int max = size - 1;
        bool insideHorizontal = x >= radius && x <= max - radius;
        bool insideVertical = y >= radius && y <= max - radius;
        if (insideHorizontal || insideVertical)
        {
            return true;
        }

        int cornerX = x < radius ? radius : max - radius;
        int cornerY = y < radius ? radius : max - radius;
        int deltaX = x - cornerX;
        int deltaY = y - cornerY;
        return (deltaX * deltaX) + (deltaY * deltaY) <= radius * radius;
    }
}