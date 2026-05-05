using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class NpcDialogueUI : MonoBehaviour
{
    private const string BuiltInFontName = "LegacyRuntime.ttf";
    private const float DoubleClickThreshold = 0.3f;

    private static NpcDialogueUI instance;

    private Canvas canvas;
    private RectTransform panelRect;
    private Image leftPortrait;
    private Image rightPortrait;
    private Text speakerText;
    private Text dialogueText;
    private Text hintText;
    private RectTransform choiceContainer;
    private Font uiFont;

    private readonly List<Button> choiceButtons = new List<Button>();

    private NpcDialogueInteractable activeConversation;
    private PlayerMovement activePlayer;
    private NpcDialogueNode currentNode;
    private string[] currentLines = new string[0];
    private int currentLineIndex;
    private Coroutine typewriterRoutine;
    private bool isTyping;
    private bool showingChoices;
    private float lastClickTime;
    private bool currentNodeCompletionHandled;

    public static NpcDialogueUI Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<NpcDialogueUI>();
                if (instance == null)
                {
                    GameObject uiObject = new GameObject("NpcDialogueUI");
                    instance = uiObject.AddComponent<NpcDialogueUI>();
                }
            }

            instance.EnsureUI();
            return instance;
        }
    }

    public static NpcDialogueUI ExistingInstance
    {
        get { return instance; }
    }

    public static bool HasActiveConversation
    {
        get { return instance != null && instance.IsConversationActive; }
    }

    public bool IsConversationActive
    {
        get { return activeConversation != null; }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        EnsureUI();
    }

    public bool IsConversationOwnedBy(NpcDialogueInteractable interactable)
    {
        return activeConversation == interactable && activeConversation != null;
    }

    public void BeginConversation(NpcDialogueInteractable interactable, PlayerMovement player)
    {
        if (interactable == null || player == null)
        {
            return;
        }

        EnsureUI();

        activeConversation = interactable;
        activePlayer = player;
        panelRect.gameObject.SetActive(true);
        SetCurrentNode(interactable.GetStartingNode());
    }

    public void HandlePanelClick()
    {
        if (!IsConversationActive)
        {
            return;
        }

        float clickTime = Time.unscaledTime;
        bool isDoubleClick = clickTime - lastClickTime <= DoubleClickThreshold;
        lastClickTime = clickTime;

        if (isDoubleClick)
        {
            SkipCurrentNodePortion();
            return;
        }

        if (showingChoices)
        {
            return;
        }

        if (isTyping)
        {
            CompleteCurrentLineImmediately();
            return;
        }

        AdvanceConversation();
    }

    private void EnsureUI()
    {
        if (canvas != null)
        {
            return;
        }

        EnsureEventSystemExists();
        uiFont = GetPreferredFont();

        canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        CanvasScaler scaler = gameObject.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = gameObject.AddComponent<CanvasScaler>();
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (gameObject.GetComponent<GraphicRaycaster>() == null)
        {
            gameObject.AddComponent<GraphicRaycaster>();
        }

        CreatePanel();
        panelRect.gameObject.SetActive(false);
    }

    private void CreatePanel()
    {
        GameObject panelObject = new GameObject("DialoguePanel");
        panelObject.transform.SetParent(transform, false);

        panelRect = panelObject.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, 22f);
        panelRect.sizeDelta = new Vector2(1240f, 260f);

        Image background = panelObject.AddComponent<Image>();
        background.color = new Color(0.07f, 0.08f, 0.1f, 0.95f);

        Outline outline = panelObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.82f, 0.72f, 0.42f, 0.95f);
        outline.effectDistance = new Vector2(2f, -2f);

        Shadow shadow = panelObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
        shadow.effectDistance = new Vector2(0f, -6f);

        NpcDialoguePanelClickHandler clickHandler = panelObject.AddComponent<NpcDialoguePanelClickHandler>();
        clickHandler.Owner = this;

        leftPortrait = CreatePortrait("LeftPortrait", panelRect, new Vector2(22f, 22f), new Vector2(190f, 216f), TextAnchor.LowerLeft);
        rightPortrait = CreatePortrait("RightPortrait", panelRect, new Vector2(-22f, 22f), new Vector2(190f, 216f), TextAnchor.LowerRight);

        speakerText = CreateText("SpeakerText", panelRect, 22, FontStyle.Bold, TextAnchor.UpperLeft);
        RectTransform speakerRect = speakerText.rectTransform;
        speakerRect.anchorMin = new Vector2(0f, 1f);
        speakerRect.anchorMax = new Vector2(1f, 1f);
        speakerRect.offsetMin = new Vector2(236f, -48f);
        speakerRect.offsetMax = new Vector2(-236f, -12f);

        dialogueText = CreateText("DialogueText", panelRect, 28, FontStyle.Normal, TextAnchor.UpperLeft);
        dialogueText.lineSpacing = 1.1f;
        RectTransform dialogueRect = dialogueText.rectTransform;
        dialogueRect.anchorMin = new Vector2(0f, 0f);
        dialogueRect.anchorMax = new Vector2(1f, 1f);
        dialogueRect.offsetMin = new Vector2(236f, 56f);
        dialogueRect.offsetMax = new Vector2(-236f, -56f);

        hintText = CreateText("HintText", panelRect, 18, FontStyle.Italic, TextAnchor.LowerLeft);
        hintText.color = new Color(0.85f, 0.83f, 0.76f, 0.9f);
        RectTransform hintRect = hintText.rectTransform;
        hintRect.anchorMin = new Vector2(0f, 0f);
        hintRect.anchorMax = new Vector2(1f, 0f);
        hintRect.offsetMin = new Vector2(236f, 18f);
        hintRect.offsetMax = new Vector2(-236f, 42f);

        GameObject choiceContainerObject = new GameObject("ChoiceContainer");
        choiceContainerObject.transform.SetParent(panelRect, false);
        choiceContainer = choiceContainerObject.AddComponent<RectTransform>();
        choiceContainer.anchorMin = new Vector2(0.5f, 0f);
        choiceContainer.anchorMax = new Vector2(0.5f, 0f);
        choiceContainer.pivot = new Vector2(0.5f, 0f);
        choiceContainer.anchoredPosition = new Vector2(0f, 54f);
        choiceContainer.sizeDelta = new Vector2(640f, 120f);

        VerticalLayoutGroup choiceLayout = choiceContainerObject.AddComponent<VerticalLayoutGroup>();
        choiceLayout.spacing = 10f;
        choiceLayout.childAlignment = TextAnchor.LowerCenter;
        choiceLayout.childControlWidth = true;
        choiceLayout.childControlHeight = false;
        choiceLayout.childForceExpandHeight = false;
        choiceLayout.childForceExpandWidth = true;

        ContentSizeFitter choiceSizeFitter = choiceContainerObject.AddComponent<ContentSizeFitter>();
        choiceSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        choiceContainer.gameObject.SetActive(false);
    }

    private Image CreatePortrait(string name, RectTransform parent, Vector2 offset, Vector2 size, TextAnchor anchor)
    {
        GameObject frameObject = new GameObject(name + "Frame");
        frameObject.transform.SetParent(parent, false);

        Image frameImage = frameObject.AddComponent<Image>();
        frameImage.color = new Color(0.13f, 0.14f, 0.18f, 0.88f);

        RectTransform frameRect = frameImage.rectTransform;
        if (anchor == TextAnchor.LowerLeft)
        {
            frameRect.anchorMin = new Vector2(0f, 0f);
            frameRect.anchorMax = new Vector2(0f, 0f);
            frameRect.pivot = new Vector2(0f, 0f);
        }
        else
        {
            frameRect.anchorMin = new Vector2(1f, 0f);
            frameRect.anchorMax = new Vector2(1f, 0f);
            frameRect.pivot = new Vector2(1f, 0f);
        }

        frameRect.anchoredPosition = offset;
        frameRect.sizeDelta = size;

        Outline frameOutline = frameObject.AddComponent<Outline>();
        frameOutline.effectColor = new Color(1f, 1f, 1f, 0.06f);
        frameOutline.effectDistance = new Vector2(1f, -1f);

        GameObject portraitObject = new GameObject(name);
        portraitObject.transform.SetParent(frameObject.transform, false);
        Image portraitImage = portraitObject.AddComponent<Image>();
        portraitImage.color = Color.white;
        portraitImage.preserveAspect = true;
        portraitImage.enabled = false;

        RectTransform portraitRect = portraitImage.rectTransform;
        portraitRect.anchorMin = Vector2.zero;
        portraitRect.anchorMax = Vector2.one;
        portraitRect.offsetMin = new Vector2(10f, 10f);
        portraitRect.offsetMax = new Vector2(-10f, -10f);

        return portraitImage;
    }

    private Text CreateText(string name, RectTransform parent, int fontSize, FontStyle fontStyle, TextAnchor alignment)
    {
        GameObject textObject = new GameObject(name);
        textObject.transform.SetParent(parent, false);

        Text text = textObject.AddComponent<Text>();
        text.font = uiFont;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.color = new Color(0.97f, 0.95f, 0.9f, 1f);
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.supportRichText = false;

        return text;
    }

    private void SetCurrentNode(NpcDialogueNode node)
    {
        if (node == null)
        {
            EndConversation();
            return;
        }

        currentNode = node;
        currentLines = BuildNodeLines(node);
        currentLineIndex = 0;
        showingChoices = false;
        currentNodeCompletionHandled = false;
        choiceContainer.gameObject.SetActive(false);
        ClearChoiceButtons();
        RefreshPortraits();
        speakerText.text = activeConversation.ResolveSpeakerName(node);
        ShowCurrentLine();
    }

    private string[] BuildNodeLines(NpcDialogueNode node)
    {
        if (node.lines == null || node.lines.Length == 0)
        {
            return new[] { string.Empty };
        }

        List<string> lines = new List<string>();
        for (int i = 0; i < node.lines.Length; i++)
        {
            if (node.lines[i] == null)
            {
                continue;
            }

            lines.Add(node.lines[i]);
        }

        return lines.Count > 0 ? lines.ToArray() : new[] { string.Empty };
    }

    private void ShowCurrentLine()
    {
        if (currentLines == null || currentLineIndex < 0 || currentLineIndex >= currentLines.Length)
        {
            dialogueText.text = string.Empty;
            UpdateHintText();
            return;
        }

        if (typewriterRoutine != null)
        {
            StopCoroutine(typewriterRoutine);
        }

        typewriterRoutine = StartCoroutine(TypeLine(currentLines[currentLineIndex]));
    }

    private IEnumerator TypeLine(string line)
    {
        isTyping = true;
        dialogueText.text = string.Empty;

        float delay = 1f / 48f;
        if (string.IsNullOrEmpty(line))
        {
            isTyping = false;
            typewriterRoutine = null;
            UpdateHintText();
            yield break;
        }

        for (int i = 0; i < line.Length; i++)
        {
            dialogueText.text = line.Substring(0, i + 1);
            yield return new WaitForSecondsRealtime(delay);
        }

        isTyping = false;
        typewriterRoutine = null;
        UpdateHintText();
    }

    private void CompleteCurrentLineImmediately()
    {
        if (!isTyping)
        {
            return;
        }

        if (typewriterRoutine != null)
        {
            StopCoroutine(typewriterRoutine);
            typewriterRoutine = null;
        }

        dialogueText.text = currentLines != null && currentLineIndex < currentLines.Length ? currentLines[currentLineIndex] : string.Empty;
        isTyping = false;
        UpdateHintText();
    }

    private void AdvanceConversation()
    {
        if (currentLines != null && currentLineIndex < currentLines.Length - 1)
        {
            currentLineIndex++;
            ShowCurrentLine();
            return;
        }

        CompleteCurrentNodeIfNeeded();

        if (currentNode != null && currentNode.choices != null && currentNode.choices.Length > 0)
        {
            ShowChoices(currentNode.choices);
            return;
        }

        if (currentNode != null && currentNode.endConversationAfterNode)
        {
            EndConversation();
            return;
        }

        if (currentNode != null && !string.IsNullOrWhiteSpace(currentNode.nextNodeId))
        {
            SetCurrentNode(activeConversation.GetNodeById(currentNode.nextNodeId));
            return;
        }

        EndConversation();
    }

    private void SkipCurrentNodePortion()
    {
        if (!IsConversationActive)
        {
            return;
        }

        if (showingChoices)
        {
            return;
        }

        if (currentLines == null || currentLines.Length == 0)
        {
            AdvanceConversation();
            return;
        }

        currentLineIndex = currentLines.Length - 1;
        if (typewriterRoutine != null)
        {
            StopCoroutine(typewriterRoutine);
            typewriterRoutine = null;
        }

        dialogueText.text = currentLines[currentLineIndex];
        isTyping = false;
        UpdateHintText();
    }

    private void ShowChoices(NpcDialogueChoice[] choices)
    {
        showingChoices = true;
        choiceContainer.gameObject.SetActive(true);
        ClearChoiceButtons();

        for (int i = 0; i < choices.Length; i++)
        {
            NpcDialogueChoice choice = choices[i];
            if (choice == null || string.IsNullOrWhiteSpace(choice.choiceText))
            {
                continue;
            }

            GameObject buttonObject = new GameObject("ChoiceButton_" + i);
            buttonObject.transform.SetParent(choiceContainer, false);

            Image buttonImage = buttonObject.AddComponent<Image>();
            buttonImage.color = new Color(0.18f, 0.2f, 0.26f, 0.98f);

            Button button = buttonObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = buttonImage.color;
            colors.highlightedColor = new Color(0.3f, 0.34f, 0.42f, 1f);
            colors.pressedColor = new Color(0.52f, 0.44f, 0.22f, 1f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            RectTransform buttonRect = button.GetComponent<RectTransform>();
            buttonRect.sizeDelta = new Vector2(640f, 42f);

            Outline buttonOutline = buttonObject.AddComponent<Outline>();
            buttonOutline.effectColor = new Color(0f, 0f, 0f, 0.35f);
            buttonOutline.effectDistance = new Vector2(1f, -1f);

            Text buttonText = CreateText("ChoiceText", buttonRect, 20, FontStyle.Normal, TextAnchor.MiddleCenter);
            buttonText.text = choice.choiceText;
            buttonText.rectTransform.anchorMin = Vector2.zero;
            buttonText.rectTransform.anchorMax = Vector2.one;
            buttonText.rectTransform.offsetMin = new Vector2(14f, 6f);
            buttonText.rectTransform.offsetMax = new Vector2(-14f, -6f);

            string nextNodeId = choice.nextNodeId;
            button.onClick.AddListener(delegate { SelectChoice(nextNodeId); });

            choiceButtons.Add(button);
        }

        UpdateHintText();
    }

    private void SelectChoice(string nextNodeId)
    {
        showingChoices = false;
        choiceContainer.gameObject.SetActive(false);
        ClearChoiceButtons();
        CompleteCurrentNodeIfNeeded();

        if (string.IsNullOrWhiteSpace(nextNodeId))
        {
            EndConversation();
            return;
        }

        SetCurrentNode(activeConversation.GetNodeById(nextNodeId));
    }

    private void ClearChoiceButtons()
    {
        for (int i = 0; i < choiceButtons.Count; i++)
        {
            if (choiceButtons[i] != null)
            {
                Destroy(choiceButtons[i].gameObject);
            }
        }

        choiceButtons.Clear();
    }

    private void RefreshPortraits()
    {
        Sprite portrait = activeConversation.ResolvePortrait(currentNode);
        NpcDialoguePortraitSide side = activeConversation.ResolvePortraitSide(currentNode);

        leftPortrait.enabled = portrait != null && side == NpcDialoguePortraitSide.Left;
        leftPortrait.sprite = leftPortrait.enabled ? portrait : null;

        rightPortrait.enabled = portrait != null && side == NpcDialoguePortraitSide.Right;
        rightPortrait.sprite = rightPortrait.enabled ? portrait : null;
    }

    private void EndConversation()
    {
        if (typewriterRoutine != null)
        {
            StopCoroutine(typewriterRoutine);
            typewriterRoutine = null;
        }

        activeConversation = null;
        activePlayer = null;
        currentNode = null;
        currentLines = new string[0];
        currentLineIndex = 0;
        currentNodeCompletionHandled = false;
        isTyping = false;
        showingChoices = false;
        speakerText.text = string.Empty;
        dialogueText.text = string.Empty;
        hintText.text = string.Empty;
        leftPortrait.enabled = false;
        rightPortrait.enabled = false;
        choiceContainer.gameObject.SetActive(false);
        ClearChoiceButtons();
        panelRect.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!IsConversationActive)
        {
            return;
        }

        if (activePlayer == null || activeConversation == null || !activeConversation.IsPlayerInRange(activePlayer))
        {
            EndConversation();
        }
    }

    private void UpdateHintText()
    {
        if (showingChoices)
        {
            hintText.text = "Choose a response.";
            return;
        }

        if (isTyping)
        {
            hintText.text = "Click to finish this line. Double-click to skip to the end of this dialogue portion.";
            return;
        }

        hintText.text = "Click to continue. Double-click to skip to the end of this dialogue portion.";
    }

    private void EnsureEventSystemExists()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
    }

    private Font GetPreferredFont()
    {
        if (uiFont != null)
        {
            return uiFont;
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

    private void CompleteCurrentNodeIfNeeded()
    {
        if (currentNodeCompletionHandled || activeConversation == null || activePlayer == null || currentNode == null)
        {
            return;
        }

        activeConversation.CompleteNodeForPlayer(currentNode, activePlayer);
        currentNodeCompletionHandled = true;
    }
}

public class NpcDialoguePanelClickHandler : MonoBehaviour, IPointerClickHandler
{
    public NpcDialogueUI Owner { get; set; }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (Owner != null)
        {
            Owner.HandlePanelClick();
        }
    }
}