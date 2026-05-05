using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerMovement))]
public class BottledSoulTutorialController : MonoBehaviour
{
    [Serializable]
    private class BottledSoulTutorialLibrary
    {
        public BottledSoulTutorialStepData[] steps = new BottledSoulTutorialStepData[0];
    }

    [Serializable]
    private class BottledSoulTutorialStepData
    {
        public string action = string.Empty;
        public string title = string.Empty;
        public string soulText = string.Empty;
        public string instruction = string.Empty;
        public float minDisplaySeconds = 2.5f;
    }

    private enum TutorialAction
    {
        None,
        MoveLeft,
        MoveRight,
        Jump,
        Dash,
        Block,
        Interact,
        EquipAnyShard,
        OpenShardMenu,
        LeftClickAttack,
        RightClickAttack,
        Ultimate,
    }

    private sealed class TutorialStep
    {
        public TutorialAction Action;
        public string Title;
        public string SoulText;
        public string Instruction;
        public float MinDisplaySeconds;
    }

    private const string BottledSoulOwnedPrefsKey = "BottledSoul.HasItem";
    private const string BottledSoulTutorialStepPrefsKey = "BottledSoul.TutorialStepIndex";
    private const string BottledSoulTutorialCompletePrefsKey = "BottledSoul.TutorialComplete";

    [Header("Tutorial Source")]
    [SerializeField] private TextAsset tutorialJson;
    [SerializeField] private string fallbackResourcesPath = "BottledSoulTutorialSteps";

    [Header("Tutorial Layout")]
    [SerializeField] private Vector3 worldAnchorOffset = new Vector3(0f, 1.4f, 0f);
    [SerializeField] private Vector2 panelSize = new Vector2(330f, 160f);
    [SerializeField] private float panelSideOffset = 245f;
    [SerializeField] private float panelVerticalOffset = 80f;
    [SerializeField] private float screenEdgePadding = 24f;
    [SerializeField] private float fadeDuration = 0.24f;

    [Header("Tutorial Look")]
    [SerializeField] private Color panelBackgroundColor = new Color(0.05f, 0.07f, 0.10f, 0.28f);
    [SerializeField] private Color panelAccentColor = new Color(0.74f, 0.87f, 0.98f, 0.18f);
    [SerializeField] private Color titleColor = new Color(0.92f, 0.97f, 1f, 0.96f);
    [SerializeField] private Color bodyColor = new Color(0.90f, 0.95f, 0.98f, 0.92f);

    private readonly List<TutorialStep> tutorialSteps = new List<TutorialStep>();

    private PlayerMovement playerMovement;
    private WeaponClassController weaponController;
    private Canvas tutorialCanvas;
    private RectTransform tutorialCanvasRect;
    private RectTransform leftPanelRect;
    private RectTransform rightPanelRect;
    private CanvasGroup leftPanelGroup;
    private CanvasGroup rightPanelGroup;
    private Text leftPanelTitleText;
    private Text leftPanelBodyText;
    private Text rightPanelTitleText;
    private Text rightPanelBodyText;
    private bool hasBottledSoul;
    private bool tutorialCompleted;
    private bool tutorialActive;
    private bool currentStepSatisfied;
    private bool shardMenuOpenedThisStep;
    private bool ultimateActivatedThisStep;
    private int currentStepIndex;
    private float currentStepShownAt;
    private float pendingAdvanceAt = float.PositiveInfinity;
    private Coroutine transitionCoroutine;
    private WeaponClassController subscribedWeaponController;

    public bool HasBottledSoul => hasBottledSoul;

    private void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>();
        weaponController = GetComponent<WeaponClassController>();
        LoadPersistedState();
        LoadTutorialSteps();
    }

    private void Start()
    {
        SubscribeToWeaponEvents();
        BeginTutorialIfNeeded();
    }

    private void OnDestroy()
    {
        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
            transitionCoroutine = null;
        }

        UnsubscribeFromWeaponEvents();

        if (tutorialCanvas != null)
        {
            Destroy(tutorialCanvas.gameObject);
        }
    }

    private void Update()
    {
        RefreshReferences();

        if (!ShouldRunLocally())
        {
            SetTutorialCanvasEnabled(false);
            return;
        }

        if (!hasBottledSoul || tutorialCompleted)
        {
            return;
        }

        if (!tutorialActive)
        {
            BeginTutorialIfNeeded();
            return;
        }

        UpdateTutorialPanelPositions();

        if (transitionCoroutine != null || currentStepIndex < 0 || currentStepIndex >= tutorialSteps.Count)
        {
            return;
        }

        if (!currentStepSatisfied && IsCurrentStepSatisfied(tutorialSteps[currentStepIndex]))
        {
            currentStepSatisfied = true;
            pendingAdvanceAt = Mathf.Max(Time.time, currentStepShownAt + tutorialSteps[currentStepIndex].MinDisplaySeconds);
        }

        if (currentStepSatisfied && Time.time >= pendingAdvanceAt)
        {
            TransitionToStep(currentStepIndex + 1, immediate: false);
        }
    }

    public void Initialize(PlayerMovement movement, WeaponClassController controller)
    {
        playerMovement = movement != null ? movement : GetComponent<PlayerMovement>();
        weaponController = controller != null ? controller : GetComponent<WeaponClassController>();
        SubscribeToWeaponEvents();
    }

    public void GrantBottledSoul()
    {
        hasBottledSoul = true;
        SaveOwnedState(true);
        BeginTutorialIfNeeded();
    }

    public void BeginTutorialIfNeeded()
    {
        if (!ShouldRunLocally() || !hasBottledSoul || tutorialCompleted)
        {
            return;
        }

        EnsureTutorialCanvas();
        if (tutorialSteps.Count == 0)
        {
            return;
        }

        int clampedStepIndex = Mathf.Clamp(currentStepIndex, 0, tutorialSteps.Count - 1);
        if (!tutorialActive)
        {
            TransitionToStep(clampedStepIndex, immediate: true);
        }
    }

    private void RefreshReferences()
    {
        if (playerMovement == null)
        {
            playerMovement = GetComponent<PlayerMovement>();
        }

        if (weaponController == null)
        {
            weaponController = GetComponent<WeaponClassController>();
        }

        SubscribeToWeaponEvents();
    }

    private bool ShouldRunLocally()
    {
        if (playerMovement == null)
        {
            return false;
        }

        bool hasLiveNetworkSession = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (!hasLiveNetworkSession)
        {
            return true;
        }

        return playerMovement.IsSpawned && playerMovement.IsOwner;
    }

    private void LoadPersistedState()
    {
        hasBottledSoul = PlayerPrefs.GetInt(BottledSoulOwnedPrefsKey, 0) != 0;
        tutorialCompleted = PlayerPrefs.GetInt(BottledSoulTutorialCompletePrefsKey, 0) != 0;
        currentStepIndex = Mathf.Max(0, PlayerPrefs.GetInt(BottledSoulTutorialStepPrefsKey, 0));
    }

    private void SaveOwnedState(bool value)
    {
        PlayerPrefs.SetInt(BottledSoulOwnedPrefsKey, value ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void SaveTutorialStepIndex(int stepIndex)
    {
        PlayerPrefs.SetInt(BottledSoulTutorialStepPrefsKey, Mathf.Max(0, stepIndex));
        PlayerPrefs.Save();
    }

    private void SaveTutorialComplete(bool value)
    {
        PlayerPrefs.SetInt(BottledSoulTutorialCompletePrefsKey, value ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void LoadTutorialSteps()
    {
        tutorialSteps.Clear();

        TextAsset source = tutorialJson;
        if (source == null && !string.IsNullOrWhiteSpace(fallbackResourcesPath))
        {
            source = Resources.Load<TextAsset>(fallbackResourcesPath);
        }

        if (source != null && !string.IsNullOrWhiteSpace(source.text))
        {
            try
            {
                BottledSoulTutorialLibrary library = JsonUtility.FromJson<BottledSoulTutorialLibrary>(source.text);
                if (library != null && library.steps != null)
                {
                    for (int stepIndex = 0; stepIndex < library.steps.Length; stepIndex++)
                    {
                        BottledSoulTutorialStepData rawStep = library.steps[stepIndex];
                        if (rawStep == null)
                        {
                            continue;
                        }

                        TutorialAction action = ParseAction(rawStep.action);
                        if (action == TutorialAction.None)
                        {
                            continue;
                        }

                        tutorialSteps.Add(new TutorialStep
                        {
                            Action = action,
                            Title = string.IsNullOrWhiteSpace(rawStep.title) ? action.ToString() : rawStep.title.Trim(),
                            SoulText = string.IsNullOrWhiteSpace(rawStep.soulText) ? "I suppose I have to explain this part too." : rawStep.soulText.Trim(),
                            Instruction = string.IsNullOrWhiteSpace(rawStep.instruction) ? string.Empty : rawStep.instruction.Trim(),
                            MinDisplaySeconds = Mathf.Max(1.5f, rawStep.minDisplaySeconds),
                        });
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Failed to parse Bottled Soul tutorial JSON: " + exception.Message);
            }
        }

        if (tutorialSteps.Count == 0)
        {
            AddFallbackSteps();
        }
    }

    private void AddFallbackSteps()
    {
        tutorialSteps.Add(CreateFallbackStep(TutorialAction.MoveLeft, "Walk Left", "Let's begin with something even a breathing mortal can manage.", "Hold A or Left Arrow to walk left.", 2.4f));
        tutorialSteps.Add(CreateFallbackStep(TutorialAction.MoveRight, "Walk Right", "Good. Now the other way, before I decide you are hopeless.", "Hold D or Right Arrow to walk right.", 2.4f));
        tutorialSteps.Add(CreateFallbackStep(TutorialAction.Jump, "Jump", "Gravity is rude. Ignore it for a moment.", "Press Space, W, or Up Arrow to jump.", 2.6f));
        tutorialSteps.Add(CreateFallbackStep(TutorialAction.Dash, "Dash", "A quick burst keeps you alive longer than confidence ever will.", "Press Q to dash.", 2.8f));
        tutorialSteps.Add(CreateFallbackStep(TutorialAction.Block, "Block", "Raise your guard before something unpleasant reaches us.", "Hold Left Shift to block.", 2.8f));
        tutorialSteps.Add(CreateFallbackStep(TutorialAction.Interact, "Interact", "Not every problem is solved by striking it.", "Press E to interact with nearby objects or pickups.", 2.8f));
        tutorialSteps.Add(CreateFallbackStep(TutorialAction.EquipAnyShard, "Equip A Shard", "Find a shard and claim it. You are not much of a threat empty-handed.", "Pick up and equip any shard to unlock combat tutorials.", 3.0f));
        tutorialSteps.Add(CreateFallbackStep(TutorialAction.OpenShardMenu, "Shard Switching", "Your shard controls sit on a hold, not a tap. Try to keep up.", "Hold R to open the shard switch menu.", 3.0f));
        tutorialSteps.Add(CreateFallbackStep(TutorialAction.LeftClickAttack, "Primary Attack", "Now show me an attack worth commenting on.", "After equipping a shard, left click to use its primary attack.", 3.0f));
        tutorialSteps.Add(CreateFallbackStep(TutorialAction.RightClickAttack, "Secondary Attack", "Every shard has another trick. Use it.", "Right click to use your shard's secondary attack.", 3.0f));
        tutorialSteps.Add(CreateFallbackStep(TutorialAction.Ultimate, "Ultimate", "When your ultimate bar fills, spend it before the moment passes.", "When your ultimate is ready, press T to unleash it.", 3.2f));
    }

    private TutorialStep CreateFallbackStep(TutorialAction action, string title, string soulText, string instruction, float minDisplaySeconds)
    {
        return new TutorialStep
        {
            Action = action,
            Title = title,
            SoulText = soulText,
            Instruction = instruction,
            MinDisplaySeconds = minDisplaySeconds,
        };
    }

    private TutorialAction ParseAction(string action)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            return TutorialAction.None;
        }

        switch (action.Trim().ToLowerInvariant())
        {
            case "moveleft":
                return TutorialAction.MoveLeft;
            case "moveright":
                return TutorialAction.MoveRight;
            case "jump":
                return TutorialAction.Jump;
            case "dash":
                return TutorialAction.Dash;
            case "block":
                return TutorialAction.Block;
            case "interact":
                return TutorialAction.Interact;
            case "equipanyshard":
                return TutorialAction.EquipAnyShard;
            case "openshardmenu":
                return TutorialAction.OpenShardMenu;
            case "leftclickattack":
                return TutorialAction.LeftClickAttack;
            case "rightclickattack":
                return TutorialAction.RightClickAttack;
            case "ultimate":
                return TutorialAction.Ultimate;
            default:
                return TutorialAction.None;
        }
    }

    private void SubscribeToWeaponEvents()
    {
        if (weaponController == null || subscribedWeaponController == weaponController)
        {
            return;
        }

        UnsubscribeFromWeaponEvents();
        weaponController.LocalShardMenuOpened += OnLocalShardMenuOpened;
        weaponController.LocalUltimateActivated += OnLocalUltimateActivated;
        subscribedWeaponController = weaponController;
    }

    private void UnsubscribeFromWeaponEvents()
    {
        if (subscribedWeaponController == null)
        {
            return;
        }

        subscribedWeaponController.LocalShardMenuOpened -= OnLocalShardMenuOpened;
        subscribedWeaponController.LocalUltimateActivated -= OnLocalUltimateActivated;
        subscribedWeaponController = null;
    }

    private void OnLocalShardMenuOpened()
    {
        shardMenuOpenedThisStep = true;
    }

    private void OnLocalUltimateActivated()
    {
        ultimateActivatedThisStep = true;
    }

    private bool IsCurrentStepSatisfied(TutorialStep step)
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;

        switch (step.Action)
        {
            case TutorialAction.MoveLeft:
                return keyboard != null && (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed);
            case TutorialAction.MoveRight:
                return keyboard != null && (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed);
            case TutorialAction.Jump:
                return keyboard != null
                    && (keyboard.spaceKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame);
            case TutorialAction.Dash:
                return playerMovement != null && playerMovement.IsDashing;
            case TutorialAction.Block:
                return playerMovement != null && playerMovement.IsBlocking;
            case TutorialAction.Interact:
                return keyboard != null && keyboard.eKey.wasPressedThisFrame;
            case TutorialAction.EquipAnyShard:
                return weaponController != null && weaponController.HasAnyEquippedShard();
            case TutorialAction.OpenShardMenu:
                return shardMenuOpenedThisStep;
            case TutorialAction.LeftClickAttack:
                return weaponController != null && weaponController.HasAnyEquippedShard() && mouse != null && mouse.leftButton.wasPressedThisFrame;
            case TutorialAction.RightClickAttack:
                return weaponController != null && weaponController.HasAnyEquippedShard() && mouse != null && mouse.rightButton.wasPressedThisFrame;
            case TutorialAction.Ultimate:
                return ultimateActivatedThisStep;
            default:
                return false;
        }
    }

    private void TransitionToStep(int nextStepIndex, bool immediate)
    {
        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
        }

        transitionCoroutine = StartCoroutine(TransitionToStepRoutine(nextStepIndex, immediate));
    }

    private IEnumerator TransitionToStepRoutine(int nextStepIndex, bool immediate)
    {
        EnsureTutorialCanvas();

        if (!immediate)
        {
            yield return FadePanelsTo(0f);
        }

        if (nextStepIndex >= tutorialSteps.Count)
        {
            tutorialCompleted = true;
            tutorialActive = false;
            SaveTutorialComplete(true);
            SaveTutorialStepIndex(tutorialSteps.Count);
            yield return FadePanelsTo(0f);
            SetTutorialCanvasEnabled(false);
            transitionCoroutine = null;
            yield break;
        }

        currentStepIndex = Mathf.Clamp(nextStepIndex, 0, tutorialSteps.Count - 1);
        SaveTutorialStepIndex(currentStepIndex);
        tutorialActive = true;
        currentStepSatisfied = false;
        shardMenuOpenedThisStep = false;
        ultimateActivatedThisStep = false;
        pendingAdvanceAt = float.PositiveInfinity;
        currentStepShownAt = Time.time;

        ApplyStepContent(tutorialSteps[currentStepIndex]);
        SetTutorialCanvasEnabled(true);

        if (immediate)
        {
            SetPanelAlpha(1f);
        }
        else
        {
            yield return FadePanelsTo(1f);
        }

        transitionCoroutine = null;
    }

    private IEnumerator FadePanelsTo(float targetAlpha)
    {
        EnsureTutorialCanvas();

        float startAlpha = leftPanelGroup != null ? leftPanelGroup.alpha : 0f;
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, fadeDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            SetPanelAlpha(alpha);
            yield return null;
        }

        SetPanelAlpha(targetAlpha);
    }

    private void ApplyStepContent(TutorialStep step)
    {
        if (leftPanelTitleText == null || leftPanelBodyText == null || rightPanelTitleText == null || rightPanelBodyText == null)
        {
            return;
        }

        leftPanelTitleText.text = "Bottled Soul";
        leftPanelBodyText.text = step.SoulText;
        rightPanelTitleText.text = step.Title;
        rightPanelBodyText.text = step.Instruction;
    }

    private void EnsureTutorialCanvas()
    {
        if (tutorialCanvas != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("BottledSoulTutorialCanvas");
        tutorialCanvas = canvasObject.AddComponent<Canvas>();
        tutorialCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        tutorialCanvas.sortingOrder = 140;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        tutorialCanvasRect = tutorialCanvas.GetComponent<RectTransform>();

        CreateTutorialPanel("LeftPanel", out leftPanelRect, out leftPanelGroup, out leftPanelTitleText, out leftPanelBodyText, true);
        CreateTutorialPanel("RightPanel", out rightPanelRect, out rightPanelGroup, out rightPanelTitleText, out rightPanelBodyText, false);
        SetPanelAlpha(0f);
        SetTutorialCanvasEnabled(false);
    }

    private void CreateTutorialPanel(string panelName, out RectTransform panelRect, out CanvasGroup panelGroup, out Text titleText, out Text bodyText, bool italicBody)
    {
        GameObject panelObject = new GameObject(panelName);
        panelObject.transform.SetParent(tutorialCanvas.transform, false);

        panelRect = panelObject.AddComponent<RectTransform>();
        panelRect.sizeDelta = panelSize;
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);

        panelGroup = panelObject.AddComponent<CanvasGroup>();

        Image panelBackground = panelObject.AddComponent<Image>();
        panelBackground.color = panelBackgroundColor;
        panelBackground.raycastTarget = false;

        GameObject accentObject = new GameObject("Accent");
        accentObject.transform.SetParent(panelObject.transform, false);
        RectTransform accentRect = accentObject.AddComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 1f);
        accentRect.anchorMax = new Vector2(1f, 1f);
        accentRect.pivot = new Vector2(0.5f, 1f);
        accentRect.sizeDelta = new Vector2(0f, 6f);
        accentRect.anchoredPosition = Vector2.zero;
        Image accentImage = accentObject.AddComponent<Image>();
        accentImage.color = panelAccentColor;
        accentImage.raycastTarget = false;

        Font builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        titleText = CreateTextElement(panelObject.transform, "Title", builtinFont, 20, titleColor, TextAnchor.UpperLeft, FontStyle.Bold);
        RectTransform titleRect = titleText.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.offsetMin = new Vector2(18f, -44f);
        titleRect.offsetMax = new Vector2(-18f, -12f);

        bodyText = CreateTextElement(panelObject.transform, "Body", builtinFont, 17, bodyColor, TextAnchor.UpperLeft, italicBody ? FontStyle.Italic : FontStyle.Normal);
        RectTransform bodyRect = bodyText.GetComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(0f, 0f);
        bodyRect.anchorMax = new Vector2(1f, 1f);
        bodyRect.offsetMin = new Vector2(18f, 18f);
        bodyRect.offsetMax = new Vector2(-18f, -52f);
    }

    private Text CreateTextElement(Transform parent, string objectName, Font font, int fontSize, Color color, TextAnchor alignment, FontStyle fontStyle)
    {
        GameObject textObject = new GameObject(objectName);
        textObject.transform.SetParent(parent, false);

        Text text = textObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.fontStyle = fontStyle;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private void UpdateTutorialPanelPositions()
    {
        if (tutorialCanvas == null || tutorialCanvasRect == null || leftPanelRect == null || rightPanelRect == null || playerMovement == null)
        {
            return;
        }

        Camera worldCamera = Camera.main;
        if (worldCamera == null)
        {
            SetTutorialCanvasEnabled(false);
            return;
        }

        Vector3 screenPosition = worldCamera.WorldToScreenPoint(playerMovement.transform.position + worldAnchorOffset);
        if (screenPosition.z <= 0f)
        {
            SetTutorialCanvasEnabled(false);
            return;
        }

        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(tutorialCanvasRect, screenPosition, null, out localPoint);

        leftPanelRect.anchoredPosition = ClampPanelPosition(localPoint + new Vector2(-panelSideOffset, panelVerticalOffset));
        rightPanelRect.anchoredPosition = ClampPanelPosition(localPoint + new Vector2(panelSideOffset, panelVerticalOffset));
        SetTutorialCanvasEnabled(true);
    }

    private Vector2 ClampPanelPosition(Vector2 desiredPosition)
    {
        if (tutorialCanvasRect == null)
        {
            return desiredPosition;
        }

        Rect canvasRect = tutorialCanvasRect.rect;
        float halfWidth = panelSize.x * 0.5f;
        float halfHeight = panelSize.y * 0.5f;

        return new Vector2(
            Mathf.Clamp(desiredPosition.x, canvasRect.xMin + halfWidth + screenEdgePadding, canvasRect.xMax - halfWidth - screenEdgePadding),
            Mathf.Clamp(desiredPosition.y, canvasRect.yMin + halfHeight + screenEdgePadding, canvasRect.yMax - halfHeight - screenEdgePadding));
    }

    private void SetPanelAlpha(float alpha)
    {
        if (leftPanelGroup != null)
        {
            leftPanelGroup.alpha = alpha;
        }

        if (rightPanelGroup != null)
        {
            rightPanelGroup.alpha = alpha;
        }
    }

    private void SetTutorialCanvasEnabled(bool isEnabled)
    {
        if (tutorialCanvas != null)
        {
            tutorialCanvas.enabled = isEnabled;
        }
    }
}