using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class PlayerChat : NetworkBehaviour
{
    [Header("Chat Settings")]
    [SerializeField] private int maxVisibleMessages = 7;
    [SerializeField] private int maxStoredMessages = 32;
    [SerializeField] private int maxMessageLength = 120;
    [SerializeField] private Vector2 chatWindowSize = new Vector2(620f, 228f);
    [SerializeField] private Vector2 chatWindowOffset = new Vector2(0f, 210f);
    [SerializeField] private float compactBubbleWidth = 420f;
    [SerializeField] private float compactBubbleSpacing = 10f;
    [SerializeField] private Vector2 compactHistoryOffset = new Vector2(0f, 470f);
    [SerializeField] private float compactBubbleLifetime = 6f;
    [SerializeField] private float compactBubbleFadeDuration = 1.15f;
    [SerializeField] private float compactBubbleSlideDistance = 28f;

    private const string BuiltInFontName = "LegacyRuntime.ttf";

    private static readonly List<ChatLine> SharedChatHistory = new List<ChatLine>();
    private static event Action SharedChatHistoryChanged;
    private static event Action<ChatLine> SharedChatLineAdded;
    private static int openChatWindowCount;
    private static Sprite roundedBubbleSprite;

    private Canvas chatCanvas;
    private GameObject chatWindow;
    private RectTransform compactHistoryRoot;
    private Text historyText;
    private InputField inputField;
    private Font chatFont;
    private Coroutine focusInputCoroutine;
    private bool isChatOpen;
    private PlayerMovement playerMovement;
    private readonly List<CompactBubble> compactBubbles = new List<CompactBubble>();

    public static bool IsTextEntryActive => openChatWindowCount > 0;

    private struct ChatLine
    {
        public string Speaker;
        public string Message;

        public ChatLine(string speaker, string message)
        {
            Speaker = speaker;
            Message = message;
        }
    }

    private sealed class CompactBubble
    {
        public GameObject Root;
        public RectTransform RectTransform;
        public CanvasGroup CanvasGroup;
        public float Height;
        public float CreatedAt;
    }

    private void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsOwner)
        {
            return;
        }

        CreateChatUI();
        SharedChatHistoryChanged += RefreshHistoryText;
        SharedChatLineAdded += HandleSharedChatLineAdded;
        RefreshHistoryText();
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
        {
            SharedChatHistoryChanged -= RefreshHistoryText;
            SharedChatLineAdded -= HandleSharedChatLineAdded;
            CloseChatWindow(clearDraft: true);
            CleanupChatUI();
        }

        base.OnNetworkDespawn();
    }

    private void Update()
    {
        if (IsOwner)
        {
            UpdateCompactBubbles();
        }

        if (!IsOwner || Keyboard.current == null)
        {
            return;
        }

        if (!isChatOpen)
        {
            if (Keyboard.current.tKey.wasPressedThisFrame)
            {
                OpenChatWindow();
            }

            return;
        }

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CloseChatWindow(clearDraft: true);
            return;
        }

        if (Keyboard.current.enterKey.wasPressedThisFrame)
        {
            SubmitCurrentMessage();
            return;
        }

        if (inputField != null && !inputField.isFocused && Keyboard.current.tKey.wasPressedThisFrame)
        {
            FocusChatInput();
        }
    }

    private void CreateChatUI()
    {
        if (chatCanvas != null)
        {
            return;
        }

        EnsureEventSystemExists();
        chatFont = GetPreferredFont();

        GameObject canvasGO = new GameObject("PlayerChatCanvas");
        chatCanvas = canvasGO.AddComponent<Canvas>();
        chatCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        chatCanvas.sortingOrder = 140;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        CreateCompactHistoryRoot();
        CreateChatWindow();
        chatWindow.SetActive(false);
    }

    private void CreateCompactHistoryRoot()
    {
        GameObject historyRootGO = new GameObject("CompactChatHistory");
        historyRootGO.transform.SetParent(chatCanvas.transform, false);
        compactHistoryRoot = historyRootGO.AddComponent<RectTransform>();
        compactHistoryRoot.anchorMin = new Vector2(0.5f, 0f);
        compactHistoryRoot.anchorMax = new Vector2(0.5f, 0f);
        compactHistoryRoot.pivot = new Vector2(0.5f, 0f);
        compactHistoryRoot.sizeDelta = new Vector2(compactBubbleWidth + 24f, Mathf.Max(280f, maxVisibleMessages * 72f));
        compactHistoryRoot.anchoredPosition = compactHistoryOffset;

        CanvasGroup historyGroup = historyRootGO.AddComponent<CanvasGroup>();
        historyGroup.blocksRaycasts = false;
        historyGroup.interactable = false;
    }

    private void CreateChatWindow()
    {
        GameObject windowGO = new GameObject("ChatWindow");
        windowGO.transform.SetParent(chatCanvas.transform, false);
        chatWindow = windowGO;

        RectTransform windowRect = windowGO.AddComponent<RectTransform>();
        windowRect.anchorMin = new Vector2(0.5f, 0f);
        windowRect.anchorMax = new Vector2(0.5f, 0f);
        windowRect.pivot = new Vector2(0.5f, 0f);
        windowRect.sizeDelta = chatWindowSize;
        windowRect.anchoredPosition = chatWindowOffset;

        Image panelImage = windowGO.AddComponent<Image>();
        panelImage.sprite = GetRoundedBubbleSprite();
        panelImage.type = Image.Type.Sliced;
        panelImage.color = new Color(1f, 1f, 1f, 0.76f);

        Outline panelOutline = windowGO.AddComponent<Outline>();
        panelOutline.effectColor = new Color(0f, 0f, 0f, 0.95f);
        panelOutline.effectDistance = new Vector2(3f, -3f);
        panelOutline.useGraphicAlpha = true;

        Shadow panelShadow = windowGO.AddComponent<Shadow>();
        panelShadow.effectColor = new Color(0f, 0f, 0f, 0.24f);
        panelShadow.effectDistance = new Vector2(0f, -4f);
        panelShadow.useGraphicAlpha = true;

        GameObject titleGO = new GameObject("TitleText");
        titleGO.transform.SetParent(windowGO.transform, false);
        Text titleText = titleGO.AddComponent<Text>();
        titleText.font = chatFont;
        titleText.fontSize = 20;
        titleText.fontStyle = FontStyle.Bold;
        titleText.text = "Chat";
        titleText.color = new Color(0f, 0f, 0f, 0.9f);
        titleText.alignment = TextAnchor.MiddleLeft;

        RectTransform titleRect = titleText.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.06f, 0.80f);
        titleRect.anchorMax = new Vector2(0.45f, 0.94f);
        titleRect.sizeDelta = Vector2.zero;
        titleRect.anchoredPosition = Vector2.zero;

        GameObject hintGO = new GameObject("HintText");
        hintGO.transform.SetParent(windowGO.transform, false);
        Text hintText = hintGO.AddComponent<Text>();
        hintText.font = chatFont;
        hintText.fontSize = 14;
        hintText.text = "Enter sends    Esc closes";
        hintText.color = new Color(0f, 0f, 0f, 0.6f);
        hintText.alignment = TextAnchor.MiddleRight;

        RectTransform hintRect = hintText.GetComponent<RectTransform>();
        hintRect.anchorMin = new Vector2(0.50f, 0.80f);
        hintRect.anchorMax = new Vector2(0.94f, 0.94f);
        hintRect.sizeDelta = Vector2.zero;
        hintRect.anchoredPosition = Vector2.zero;

        GameObject historyGO = new GameObject("HistoryText");
        historyGO.transform.SetParent(windowGO.transform, false);
        historyText = historyGO.AddComponent<Text>();
        historyText.font = chatFont;
        historyText.fontSize = 18;
        historyText.color = Color.black;
        historyText.alignment = TextAnchor.LowerLeft;
        historyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        historyText.verticalOverflow = VerticalWrapMode.Truncate;
        historyText.supportRichText = true;

        RectTransform historyRect = historyText.GetComponent<RectTransform>();
        historyRect.anchorMin = new Vector2(0.06f, 0.31f);
        historyRect.anchorMax = new Vector2(0.94f, 0.74f);
        historyRect.sizeDelta = Vector2.zero;
        historyRect.anchoredPosition = Vector2.zero;

        GameObject inputGO = new GameObject("InputField");
        inputGO.transform.SetParent(windowGO.transform, false);

        RectTransform inputRect = inputGO.AddComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0.06f, 0.08f);
        inputRect.anchorMax = new Vector2(0.94f, 0.22f);
        inputRect.sizeDelta = Vector2.zero;
        inputRect.anchoredPosition = Vector2.zero;

        Image inputBackground = inputGO.AddComponent<Image>();
        inputBackground.sprite = GetRoundedBubbleSprite();
        inputBackground.type = Image.Type.Sliced;
        inputBackground.color = new Color(1f, 1f, 1f, 0.96f);

        Outline inputOutline = inputGO.AddComponent<Outline>();
        inputOutline.effectColor = new Color(0f, 0f, 0f, 1f);
        inputOutline.effectDistance = new Vector2(2.4f, -2.4f);
        inputOutline.useGraphicAlpha = true;

        inputField = inputGO.AddComponent<InputField>();
        inputField.targetGraphic = inputBackground;
        inputField.characterLimit = maxMessageLength;
        inputField.lineType = InputField.LineType.SingleLine;
        inputField.customCaretColor = true;
        inputField.caretColor = Color.black;
        inputField.selectionColor = new Color(0.74f, 0.84f, 1f, 0.75f);

        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(inputGO.transform, false);
        Text inputText = textGO.AddComponent<Text>();
        inputText.font = chatFont;
        inputText.fontSize = 20;
        inputText.color = Color.black;
        inputText.alignment = TextAnchor.MiddleLeft;
        inputText.supportRichText = false;

        RectTransform inputTextRect = inputText.GetComponent<RectTransform>();
        inputTextRect.anchorMin = new Vector2(0f, 0f);
        inputTextRect.anchorMax = new Vector2(1f, 1f);
        inputTextRect.offsetMin = new Vector2(16f, 10f);
        inputTextRect.offsetMax = new Vector2(-16f, -10f);

        GameObject placeholderGO = new GameObject("Placeholder");
        placeholderGO.transform.SetParent(inputGO.transform, false);
        Text placeholderText = placeholderGO.AddComponent<Text>();
        placeholderText.font = chatFont;
        placeholderText.fontSize = 20;
        placeholderText.fontStyle = FontStyle.Italic;
        placeholderText.color = new Color(0f, 0f, 0f, 0.35f);
        placeholderText.text = "Type a message...";
        placeholderText.alignment = TextAnchor.MiddleLeft;

        RectTransform placeholderRect = placeholderText.GetComponent<RectTransform>();
        placeholderRect.anchorMin = new Vector2(0f, 0f);
        placeholderRect.anchorMax = new Vector2(1f, 1f);
        placeholderRect.offsetMin = new Vector2(16f, 10f);
        placeholderRect.offsetMax = new Vector2(-16f, -10f);

        inputField.textComponent = inputText;
        inputField.placeholder = placeholderText;
    }

    private void OpenChatWindow()
    {
        if (chatWindow == null || isChatOpen)
        {
            return;
        }

        isChatOpen = true;
        openChatWindowCount++;
        chatWindow.SetActive(true);
        inputField.text = string.Empty;
        RefreshHistoryText();
        FocusChatInput();
    }

    private void CloseChatWindow(bool clearDraft)
    {
        if (!isChatOpen)
        {
            return;
        }

        isChatOpen = false;
        openChatWindowCount = Mathf.Max(0, openChatWindowCount - 1);

        if (focusInputCoroutine != null)
        {
            StopCoroutine(focusInputCoroutine);
            focusInputCoroutine = null;
        }

        if (clearDraft && inputField != null)
        {
            inputField.text = string.Empty;
        }

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        if (chatWindow != null)
        {
            chatWindow.SetActive(false);
        }
    }

    private void FocusChatInput()
    {
        if (inputField == null)
        {
            return;
        }

        if (focusInputCoroutine != null)
        {
            StopCoroutine(focusInputCoroutine);
        }

        focusInputCoroutine = StartCoroutine(FocusChatInputRoutine());
    }

    private IEnumerator FocusChatInputRoutine()
    {
        yield return null;

        if (inputField == null || EventSystem.current == null)
        {
            focusInputCoroutine = null;
            yield break;
        }

        EventSystem.current.SetSelectedGameObject(inputField.gameObject);
        inputField.Select();
        inputField.ActivateInputField();
        inputField.MoveTextEnd(false);
        focusInputCoroutine = null;
    }

    private void SubmitCurrentMessage()
    {
        if (inputField == null)
        {
            return;
        }

        string sanitizedMessage = SanitizeMessage(inputField.text);
        if (string.IsNullOrEmpty(sanitizedMessage))
        {
            CloseChatWindow(clearDraft: true);
            return;
        }

        SubmitChatMessageServerRpc(new FixedString128Bytes(sanitizedMessage));
        CloseChatWindow(clearDraft: true);
    }

    [ServerRpc]
    private void SubmitChatMessageServerRpc(FixedString128Bytes rawMessage)
    {
        string sanitizedMessage = SanitizeMessage(rawMessage.ToString());
        if (string.IsNullOrEmpty(sanitizedMessage))
        {
            return;
        }

        string speakerName = playerMovement != null ? playerMovement.DisplayName : PlayerSessionSettings.LocalPlayerName;
        speakerName = PlayerSessionSettings.SanitizePlayerName(speakerName);

        ReceiveChatMessageClientRpc(new FixedString64Bytes(speakerName), new FixedString128Bytes(sanitizedMessage));
    }

    [ClientRpc]
    private void ReceiveChatMessageClientRpc(FixedString64Bytes speakerName, FixedString128Bytes message)
    {
        RegisterSharedChatLine(new ChatLine(speakerName.ToString(), message.ToString()), maxStoredMessages);
    }

    private void HandleSharedChatLineAdded(ChatLine line)
    {
        if (!IsOwner || compactHistoryRoot == null)
        {
            return;
        }

        CreateCompactBubble(line);
        UpdateCompactBubbleLayout(Time.unscaledTime, Time.unscaledDeltaTime);
    }

    private void RefreshHistoryText()
    {
        if (historyText == null)
        {
            return;
        }

        if (SharedChatHistory.Count == 0)
        {
            historyText.text = "<i>No messages yet.</i>";
            return;
        }

        int startIndex = Mathf.Max(0, SharedChatHistory.Count - maxVisibleMessages);
        StringBuilder builder = new StringBuilder();

        for (int i = startIndex; i < SharedChatHistory.Count; i++)
        {
            if (builder.Length > 0)
            {
                builder.Append('\n');
            }

            ChatLine line = SharedChatHistory[i];
            builder.Append("<b>");
            builder.Append(EscapeRichText(line.Speaker));
            builder.Append("</b>: ");
            builder.Append(EscapeRichText(line.Message));
        }

        historyText.text = builder.ToString();
    }

    private static void RegisterSharedChatLine(ChatLine line, int maxStoredLines)
    {
        SharedChatHistory.Add(line);

        while (SharedChatHistory.Count > maxStoredLines)
        {
            SharedChatHistory.RemoveAt(0);
        }

        SharedChatHistoryChanged?.Invoke();
        SharedChatLineAdded?.Invoke(line);
    }

    private void CreateCompactBubble(ChatLine line)
    {
        GameObject bubbleGO = new GameObject("CompactBubble");
        bubbleGO.transform.SetParent(compactHistoryRoot, false);

        RectTransform bubbleRect = bubbleGO.AddComponent<RectTransform>();
        bubbleRect.anchorMin = new Vector2(0.5f, 0f);
        bubbleRect.anchorMax = new Vector2(0.5f, 0f);
        bubbleRect.pivot = new Vector2(0.5f, 0f);
        bubbleRect.sizeDelta = new Vector2(compactBubbleWidth, 84f);

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
        bubbleText.font = chatFont;
        bubbleText.fontSize = 16;
        bubbleText.color = Color.black;
        bubbleText.alignment = TextAnchor.MiddleLeft;
        bubbleText.horizontalOverflow = HorizontalWrapMode.Wrap;
        bubbleText.verticalOverflow = VerticalWrapMode.Overflow;
        bubbleText.supportRichText = true;
        bubbleText.raycastTarget = false;
        bubbleText.text = $"<b>{EscapeRichText(line.Speaker)}</b>: {EscapeRichText(line.Message)}";

        RectTransform textRect = bubbleText.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = new Vector2(16f, 10f);
        textRect.offsetMax = new Vector2(-16f, -10f);

        LayoutRebuilder.ForceRebuildLayoutImmediate(textRect);
        float bubbleHeight = Mathf.Max(46f, bubbleText.preferredHeight + 22f);
        bubbleRect.sizeDelta = new Vector2(compactBubbleWidth, bubbleHeight);
        bubbleRect.anchoredPosition = new Vector2(0f, -10f);

        compactBubbles.Add(new CompactBubble
        {
            Root = bubbleGO,
            RectTransform = bubbleRect,
            CanvasGroup = bubbleGroup,
            Height = bubbleHeight,
            CreatedAt = Time.unscaledTime
        });
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
            if (currentTime - compactBubbles[i].CreatedAt < compactBubbleLifetime)
            {
                continue;
            }

            Destroy(compactBubbles[i].Root);
            compactBubbles.RemoveAt(i);
        }

        if (compactBubbles.Count == 0)
        {
            return;
        }

        UpdateCompactBubbleLayout(currentTime, Time.unscaledDeltaTime);
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

    private static string SanitizeMessage(string rawMessage)
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

        if (sanitized.Length > 120)
        {
            sanitized = sanitized.Substring(0, 120);
        }

        return sanitized;
    }

    private static string EscapeRichText(string value)
    {
        return value.Replace("<", "‹").Replace(">", "›");
    }

    private Font GetPreferredFont()
    {
        if (chatFont != null)
        {
            return chatFont;
        }

        string[] preferredFonts =
        {
            "Trebuchet MS",
            "Segoe UI",
            "Verdana",
            "Arial"
        };

        foreach (string fontName in preferredFonts)
        {
            try
            {
                Font osFont = Font.CreateDynamicFontFromOSFont(fontName, 18);
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
        texture.name = "RoundedBubbleTexture";
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color32[] pixels = new Color32[textureSize * textureSize];
        Color32 solidWhite = new Color32(255, 255, 255, 255);
        Color32 transparent = new Color32(255, 255, 255, 0);

        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                pixels[(y * textureSize) + x] = IsInsideRoundedRect(x, y, textureSize, cornerRadius) ? solidWhite : transparent;
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        roundedBubbleSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, textureSize, textureSize),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(cornerRadius, cornerRadius, cornerRadius, cornerRadius));

        return roundedBubbleSprite;
    }

    private static bool IsInsideRoundedRect(int x, int y, int size, int radius)
    {
        int maxIndex = size - 1;

        if ((x >= radius && x <= maxIndex - radius) || (y >= radius && y <= maxIndex - radius))
        {
            return true;
        }

        int cornerCenterX = x < radius ? radius : maxIndex - radius;
        int cornerCenterY = y < radius ? radius : maxIndex - radius;
        int deltaX = x - cornerCenterX;
        int deltaY = y - cornerCenterY;

        return (deltaX * deltaX) + (deltaY * deltaY) <= radius * radius;
    }

    private static void EnsureEventSystemExists()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystemGO = new GameObject("EventSystem");
        eventSystemGO.AddComponent<EventSystem>();
        eventSystemGO.AddComponent<InputSystemUIInputModule>();
    }

    private void CleanupChatUI()
    {
        compactBubbles.Clear();

        if (chatCanvas != null)
        {
            Destroy(chatCanvas.gameObject);
            chatCanvas = null;
        }

        chatWindow = null;
        compactHistoryRoot = null;
        historyText = null;
        inputField = null;
    }
}