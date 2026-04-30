using UnityEngine;
using UnityEngine.UI;

public class NpcInteractionPrompt : MonoBehaviour
{
    private const string BuiltInFontName = "LegacyRuntime.ttf";

    [SerializeField] private NpcDialogueInteractable dialogueInteractable;
    [SerializeField] private Vector3 promptOffset = new Vector3(0f, 2.15f, 0f);
    [SerializeField] private string actionLabel = "Talk";
    [SerializeField] private string interactionKeyLabel = "F";
    [SerializeField] private Vector2 promptSize = new Vector2(360f, 92f);

    private Canvas promptCanvas;
    private Text titleText;
    private Text detailText;
    private Font promptFont;

    private void Awake()
    {
        if (dialogueInteractable == null)
        {
            dialogueInteractable = GetComponent<NpcDialogueInteractable>();
        }
    }

    private void LateUpdate()
    {
        if (dialogueInteractable == null)
        {
            HidePrompt();
            return;
        }

        if (!dialogueInteractable.HasConversationNodes || NpcDialogueUI.HasActiveConversation)
        {
            HidePrompt();
            return;
        }

        PlayerMovement player = dialogueInteractable.FindNearestEligiblePlayer();
        if (player == null)
        {
            HidePrompt();
            return;
        }

        EnsurePrompt();
        titleText.text = actionLabel.ToUpperInvariant();
        detailText.text = "Press " + interactionKeyLabel + " to talk to " + dialogueInteractable.NpcDisplayName;
        promptCanvas.gameObject.SetActive(true);
        UpdatePromptPosition();
    }

    private void OnDisable()
    {
        HidePrompt();
    }

    private void OnDestroy()
    {
        if (promptCanvas != null)
        {
            Destroy(promptCanvas.gameObject);
            promptCanvas = null;
        }
    }

    private void EnsurePrompt()
    {
        if (promptCanvas != null)
        {
            return;
        }

        promptFont = GetPreferredFont();

        GameObject promptGO = new GameObject(gameObject.name + "_TalkPrompt");
        promptCanvas = promptGO.AddComponent<Canvas>();
        promptCanvas.renderMode = RenderMode.WorldSpace;
        promptCanvas.sortingOrder = 15;

        RectTransform canvasRect = promptCanvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = promptSize;
        canvasRect.localScale = Vector3.one * 0.008f;

        CanvasGroup canvasGroup = promptGO.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        GameObject backgroundGO = new GameObject("Background");
        backgroundGO.transform.SetParent(promptGO.transform, false);

        Image backgroundImage = backgroundGO.AddComponent<Image>();
        backgroundImage.color = new Color(0.05f, 0.06f, 0.08f, 0.9f);

        Outline backgroundOutline = backgroundGO.AddComponent<Outline>();
        backgroundOutline.effectColor = new Color(0.88f, 0.76f, 0.45f, 0.95f);
        backgroundOutline.effectDistance = new Vector2(2f, -2f);

        RectTransform backgroundRect = backgroundGO.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        GameObject accentGO = new GameObject("AccentBar");
        accentGO.transform.SetParent(backgroundGO.transform, false);

        Image accentImage = accentGO.AddComponent<Image>();
        accentImage.color = new Color(0.9f, 0.69f, 0.28f, 0.95f);

        RectTransform accentRect = accentGO.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 1f);
        accentRect.anchorMax = new Vector2(1f, 1f);
        accentRect.pivot = new Vector2(0.5f, 1f);
        accentRect.sizeDelta = new Vector2(0f, 6f);
        accentRect.anchoredPosition = Vector2.zero;

        titleText = CreateText(promptGO.transform, "Title", 16, FontStyle.Bold, TextAnchor.UpperCenter);
        RectTransform titleRect = titleText.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.offsetMin = new Vector2(16f, -34f);
        titleRect.offsetMax = new Vector2(-16f, -8f);

        detailText = CreateText(promptGO.transform, "Detail", 22, FontStyle.Normal, TextAnchor.MiddleCenter);
        RectTransform detailRect = detailText.rectTransform;
        detailRect.anchorMin = new Vector2(0f, 0f);
        detailRect.anchorMax = new Vector2(1f, 1f);
        detailRect.offsetMin = new Vector2(16f, 12f);
        detailRect.offsetMax = new Vector2(-16f, -24f);

        promptCanvas.gameObject.SetActive(false);
    }

    private Text CreateText(Transform parent, string name, int fontSize, FontStyle fontStyle, TextAnchor alignment)
    {
        GameObject textGO = new GameObject(name);
        textGO.transform.SetParent(parent, false);

        Text text = textGO.AddComponent<Text>();
        text.font = promptFont;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = new Color(0.98f, 0.97f, 0.93f, 1f);
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.supportRichText = false;
        text.raycastTarget = false;
        return text;
    }

    private void UpdatePromptPosition()
    {
        if (promptCanvas == null)
        {
            return;
        }

        promptCanvas.transform.position = transform.position + promptOffset;
        if (Camera.main != null)
        {
            promptCanvas.transform.LookAt(Camera.main.transform);
            promptCanvas.transform.Rotate(0f, 180f, 0f);
        }
    }

    private void HidePrompt()
    {
        if (promptCanvas != null)
        {
            promptCanvas.gameObject.SetActive(false);
        }
    }

    private Font GetPreferredFont()
    {
        if (promptFont != null)
        {
            return promptFont;
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
}