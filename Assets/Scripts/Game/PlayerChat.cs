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
    [SerializeField] private Vector2 chatHistoryPanelSize = new Vector2(460f, 260f);
    [SerializeField] private Vector2 chatHistoryMargin = new Vector2(32f, 32f);
    [SerializeField] private Vector2 inputBubbleMinSize = new Vector2(240f, 58f);
    [SerializeField] private float worldBubbleMinWidth = 110f;
    [SerializeField] private float worldBubbleMaxWidth = 320f;
    [SerializeField] private float worldBubbleMinHeight = 44f;
    [SerializeField] private Vector2 bubblePadding = new Vector2(16f, 10f);
    [SerializeField] private float compactBubbleSpacing = 10f;
    [SerializeField] private int maxVisiblePlayerBubbles = 3;
    [SerializeField] private float compactBubbleLifetime = 6f;
    [SerializeField] private float compactBubbleFadeDuration = 1.15f;
    [SerializeField] private float compactBubbleSlideDistance = 28f;
    [SerializeField] private float inputFollowSmoothing = 18f;

    private const string BuiltInFontName = "LegacyRuntime.ttf";

    private static readonly List<ChatLine> SharedChatHistory = new List<ChatLine>();
    private static event Action SharedChatHistoryChanged;
    private static int openChatWindowCount;
    private static Sprite roundedBubbleSprite;

    private Canvas chatCanvas;
    private GameObject chatWindow;
    private RectTransform chatWindowRect;
    private Text historyText;
    private InputField inputField;
    private Text inputText;
    private Text placeholderText;
    private Font chatFont;
    private Coroutine focusInputCoroutine;
    private bool isChatOpen;
    private PlayerMovement playerMovement;
    private Canvas bubbleCanvas;
    private RectTransform bubbleCanvasRect;
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

        if (playerMovement == null)
        {
            playerMovement = GetComponent<PlayerMovement>();
        }

        chatFont = GetPreferredFont();
        CreateBubbleUI();

        if (!IsOwner)
        {
            return;
        }

        CreateChatUI();
        SharedChatHistoryChanged += RefreshHistoryText;
        RefreshHistoryText();
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
        {
            SharedChatHistoryChanged -= RefreshHistoryText;
            CloseChatWindow(clearDraft: true);
            CleanupChatUI();
        }

        CleanupBubbleUI();

        base.OnNetworkDespawn();
    }

    private void Update()
    {
        UpdateBubbleCanvasTransform();
        UpdateCompactBubbles();

        if (!IsOwner)
        {
            return;
        }

        UpdateChatWindowPosition();

        if (Keyboard.current == null)
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

        CreateHistoryPanel();
        CreateChatWindow();
        chatWindow.SetActive(false);
    }

    private void CreateBubbleUI()
    {
        if (bubbleCanvas != null)
        {
            return;
        }

        GameObject canvasGO = new GameObject($"{gameObject.name}_ChatBubbleCanvas");
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

    private void CreateHistoryPanel()
    {
        GameObject historyPanelGO = new GameObject("CompactChatHistory");
        historyPanelGO.transform.SetParent(chatCanvas.transform, false);

        RectTransform historyPanelRect = historyPanelGO.AddComponent<RectTransform>();
        historyPanelRect.anchorMin = new Vector2(1f, 1f);
        historyPanelRect.anchorMax = new Vector2(1f, 1f);
        historyPanelRect.pivot = new Vector2(1f, 1f);
        historyPanelRect.sizeDelta = chatHistoryPanelSize;
        historyPanelRect.anchoredPosition = new Vector2(-chatHistoryMargin.x, -chatHistoryMargin.y);

        Image historyBackground = historyPanelGO.AddComponent<Image>();
        historyBackground.sprite = GetRoundedBubbleSprite();
        historyBackground.type = Image.Type.Sliced;
        historyBackground.color = new Color(1f, 1f, 1f, 0.34f);
        historyBackground.raycastTarget = false;

        Shadow historyShadow = historyPanelGO.AddComponent<Shadow>();
        historyShadow.effectColor = new Color(0f, 0f, 0f, 0.12f);
        historyShadow.effectDistance = new Vector2(0f, -2f);
        historyShadow.useGraphicAlpha = true;

        GameObject historyGO = new GameObject("HistoryText");
        historyGO.transform.SetParent(historyPanelGO.transform, false);
        historyText = historyGO.AddComponent<Text>();
        historyText.font = chatFont;
        historyText.fontSize = 18;
        historyText.color = new Color(0f, 0f, 0f, 0.72f);
        historyText.alignment = TextAnchor.UpperLeft;
        historyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        historyText.verticalOverflow = VerticalWrapMode.Truncate;
        historyText.supportRichText = true;
        historyText.raycastTarget = false;

        RectTransform historyRect = historyText.GetComponent<RectTransform>();
        historyRect.anchorMin = Vector2.zero;
        historyRect.anchorMax = Vector2.one;
        historyRect.offsetMin = new Vector2(16f, 16f);
        historyRect.offsetMax = new Vector2(-16f, -16f);
    }

    private void CreateChatWindow()
    {
        GameObject windowGO = new GameObject("ChatWindow");
        windowGO.transform.SetParent(chatCanvas.transform, false);
        chatWindow = windowGO;

        chatWindowRect = windowGO.AddComponent<RectTransform>();
        chatWindowRect.anchorMin = Vector2.zero;
        chatWindowRect.anchorMax = Vector2.zero;
        chatWindowRect.pivot = new Vector2(0.5f, 0f);
        chatWindowRect.sizeDelta = inputBubbleMinSize;
        chatWindowRect.anchoredPosition = Vector2.zero;

        Image panelImage = windowGO.AddComponent<Image>();
        panelImage.sprite = GetRoundedBubbleSprite();
        panelImage.type = Image.Type.Sliced;
        panelImage.color = new Color(1f, 1f, 1f, 0.94f);

        Outline panelOutline = windowGO.AddComponent<Outline>();
        panelOutline.effectColor = new Color(0f, 0f, 0f, 0.95f);
        panelOutline.effectDistance = new Vector2(2.8f, -2.8f);
        panelOutline.useGraphicAlpha = true;

        Shadow panelShadow = windowGO.AddComponent<Shadow>();
        panelShadow.effectColor = new Color(0f, 0f, 0f, 0.18f);
        panelShadow.effectDistance = new Vector2(0f, -3f);
        panelShadow.useGraphicAlpha = true;

        GameObject inputGO = new GameObject("InputField");
        inputGO.transform.SetParent(windowGO.transform, false);

        RectTransform inputRect = inputGO.AddComponent<RectTransform>();
        inputRect.anchorMin = Vector2.zero;
        inputRect.anchorMax = Vector2.one;
        inputRect.sizeDelta = Vector2.zero;
        inputRect.anchoredPosition = Vector2.zero;

        Image inputBackground = inputGO.AddComponent<Image>();
        inputBackground.color = new Color(1f, 1f, 1f, 0.01f);

        inputField = inputGO.AddComponent<InputField>();
        inputField.targetGraphic = inputBackground;
        inputField.characterLimit = maxMessageLength;
        inputField.lineType = InputField.LineType.SingleLine;
        inputField.customCaretColor = true;
        inputField.caretColor = Color.black;
        inputField.selectionColor = new Color(0.74f, 0.84f, 1f, 0.75f);
        inputField.onValueChanged.AddListener(HandleInputValueChanged);

        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(inputGO.transform, false);
        inputText = textGO.AddComponent<Text>();
        inputText.font = chatFont;
        inputText.fontSize = 20;
        inputText.color = new Color(0f, 0f, 0f, 0.98f);
        inputText.alignment = TextAnchor.MiddleLeft;
        inputText.horizontalOverflow = HorizontalWrapMode.Wrap;
        inputText.verticalOverflow = VerticalWrapMode.Overflow;
        inputText.supportRichText = false;

        RectTransform inputTextRect = inputText.GetComponent<RectTransform>();
        inputTextRect.anchorMin = Vector2.zero;
        inputTextRect.anchorMax = Vector2.one;
        inputTextRect.offsetMin = new Vector2(bubblePadding.x, bubblePadding.y);
        inputTextRect.offsetMax = new Vector2(-bubblePadding.x, -bubblePadding.y);

        GameObject placeholderGO = new GameObject("Placeholder");
        placeholderGO.transform.SetParent(inputGO.transform, false);
        placeholderText = placeholderGO.AddComponent<Text>();
        placeholderText.font = chatFont;
        placeholderText.fontSize = 20;
        placeholderText.fontStyle = FontStyle.Italic;
        placeholderText.color = new Color(0f, 0f, 0f, 0.35f);
        placeholderText.text = "Type a message...";
        placeholderText.alignment = TextAnchor.MiddleLeft;
        placeholderText.horizontalOverflow = HorizontalWrapMode.Wrap;
        placeholderText.verticalOverflow = VerticalWrapMode.Overflow;

        RectTransform placeholderRect = placeholderText.GetComponent<RectTransform>();
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = new Vector2(bubblePadding.x, bubblePadding.y);
        placeholderRect.offsetMax = new Vector2(-bubblePadding.x, -bubblePadding.y);

        inputField.textComponent = inputText;
        inputField.placeholder = placeholderText;
        UpdateInputBubbleSize();
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
        UpdateInputBubbleSize();
        UpdateChatWindowPosition(forceSnap: true);
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

        if (inputField != null)
        {
            inputField.DeactivateInputField();
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

    private void HandleInputValueChanged(string _)
    {
        UpdateInputBubbleSize();
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
        ChatLine line = new ChatLine(speakerName.ToString(), message.ToString());
        RegisterSharedChatLine(line, maxStoredMessages);
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
    }

    private void CreateCompactBubble(ChatLine line)
    {
        if (bubbleCanvasRect == null)
        {
            return;
        }

        GameObject bubbleGO = new GameObject("CompactBubble");
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
        bubbleText.font = chatFont;
        bubbleText.fontSize = 16;
        bubbleText.color = Color.black;
        bubbleText.alignment = TextAnchor.MiddleCenter;
        bubbleText.horizontalOverflow = HorizontalWrapMode.Wrap;
        bubbleText.verticalOverflow = VerticalWrapMode.Overflow;
        bubbleText.supportRichText = false;
        bubbleText.raycastTarget = false;
        bubbleText.text = EscapeRichText(line.Message);

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

        while (compactBubbles.Count > maxVisiblePlayerBubbles)
        {
            Destroy(compactBubbles[0].Root);
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

    private float CalculateBubbleWidth(float preferredTextWidth)
    {
        float paddedWidth = preferredTextWidth + (bubblePadding.x * 2f);
        return Mathf.Clamp(paddedWidth, worldBubbleMinWidth, worldBubbleMaxWidth);
    }

    private void UpdateBubbleCanvasTransform()
    {
        if (bubbleCanvas == null)
        {
            return;
        }

        Vector3 anchorPosition = playerMovement != null
            ? playerMovement.ChatBubbleAnchorWorldPosition
            : transform.position + new Vector3(0f, 2f, 0f);

        bubbleCanvas.transform.position = anchorPosition;

        if (Camera.main != null)
        {
            bubbleCanvas.transform.LookAt(Camera.main.transform);
            bubbleCanvas.transform.Rotate(0f, 180f, 0f);
        }
    }

    private void UpdateChatWindowPosition(bool forceSnap = false)
    {
        if (!IsOwner || !isChatOpen || chatWindowRect == null)
        {
            return;
        }

        if (!TryGetChatAnchorScreenPosition(out Vector2 screenPoint))
        {
            return;
        }

        if (forceSnap)
        {
            chatWindowRect.anchoredPosition = screenPoint;
            return;
        }

        float deltaTime = Time.unscaledDeltaTime;
        float smoothing = deltaTime > 0f ? 1f - Mathf.Exp(-inputFollowSmoothing * deltaTime) : 1f;
        chatWindowRect.anchoredPosition = Vector2.Lerp(chatWindowRect.anchoredPosition, screenPoint, smoothing);
    }

    private void UpdateInputBubbleSize()
    {
        if (chatWindowRect == null || inputField == null || inputText == null || placeholderText == null)
        {
            return;
        }

        float preferredTextWidth = string.IsNullOrEmpty(inputField.text)
            ? placeholderText.preferredWidth
            : inputText.preferredWidth;
        float preferredTextHeight = string.IsNullOrEmpty(inputField.text)
            ? placeholderText.preferredHeight
            : inputText.preferredHeight;

        float bubbleWidth = Mathf.Max(inputBubbleMinSize.x, CalculateBubbleWidth(preferredTextWidth));
        float bubbleHeight = Mathf.Max(inputBubbleMinSize.y, preferredTextHeight + (bubblePadding.y * 2f));
        chatWindowRect.sizeDelta = new Vector2(bubbleWidth, bubbleHeight);
    }

    private bool TryGetChatAnchorScreenPosition(out Vector2 screenPoint)
    {
        screenPoint = Vector2.zero;
        if (Camera.main == null)
        {
            return false;
        }

        Vector3 anchorPosition = playerMovement != null
            ? playerMovement.ChatBubbleAnchorWorldPosition
            : transform.position + new Vector3(0f, 2f, 0f);
        Vector3 rawScreenPoint = Camera.main.WorldToScreenPoint(anchorPosition);
        if (rawScreenPoint.z < 0f)
        {
            return false;
        }

        screenPoint = new Vector2(rawScreenPoint.x, rawScreenPoint.y);
        return true;
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
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

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
        if (chatCanvas != null)
        {
            Destroy(chatCanvas.gameObject);
            chatCanvas = null;
        }

        chatWindow = null;
        chatWindowRect = null;
        historyText = null;
        inputField = null;
        inputText = null;
        placeholderText = null;
    }

    private void CleanupBubbleUI()
    {
        compactBubbles.Clear();

        if (bubbleCanvas != null)
        {
            Destroy(bubbleCanvas.gameObject);
            bubbleCanvas = null;
        }

        bubbleCanvasRect = null;
    }
}