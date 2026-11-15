// TimberWings — Leo's Journal Main Menu (v1.9 final polish)
// Runtime only. Uses existing MenuFlow, SaveSystem, SaveSummary, SettingsMenu and GameAudio; duplicates none of them.
// Do not attach this to the gameplay scene; install with Tools > BuilderSim > UI > Главное меню > Дневник.
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class TWJournalMenuController : MonoBehaviour
{
    [Header("Generated UI references")]
    public RectTransform canvasRect;
    public RectTransform layoutRoot;
    public RectTransform bookRoot;
    public RectTransform backgroundRect;
    public CanvasGroup layoutGroup;
    public Button continueButton;
    public Button newGameButton;
    public Button settingsButton;
    public Button exitButton;
    public TMP_Text continueSubtitle;
    public TMP_Text versionLabel;
    public TMP_Text hintLabel;
    public CanvasGroup hintGroup;
    public GameObject confirmation;
    public TMP_Text confirmationTitle;
    public TMP_Text confirmationText;
    public Button confirmYes;
    public Button confirmNo;

    [Header("Page roots (v1.9 final)")]
    public RectTransform leftPageRoot;     // column with the four paper buttons
    public RectTransform rightPageRoot;    // container of the right-page illustrations
    public RectTransform versionBadge;     // screen-anchored badge (bottom-right corner of the SCREEN)

    [Header("Menu UI sound and version")]
    [Tooltip("Shown in the corner badge. Empty = 'v' + Application.version. The text is always set from code.")]
    public string menuVersion = "v1.9";
    [Range(0f, 1f)] public float uiSoundVolume = 0.7f;
    public AudioClip hoverClip;
    public AudioClip clickClip;
    public AudioClip loreClip;
    public AudioClip pageClip;      // paper rustle: Continue pressed / sketch changes on the loading screen
    public AudioClip readyClip;     // soft chime when the game scene has really been loaded

    [Header("Journal loading screen")]
    [Tooltip("Sketches from Leo's journey that rotate on the right page while loading.")]
    public Sprite[] loadingSketches;
    [Tooltip("One short caption per sketch. Missing captions are simply not shown.")]
    public string[] loadingSketchCaptions;
    [Tooltip("Short atmospheric messages. Empty = built-in list.")]
    public string[] loadingMessages;
    public Sprite loadingLeaf;
    // Legacy v1.9.x fields are kept so existing scenes keep their references (used only by the fallback screen).
    public Sprite loadingBackground;
    public Sprite loadingBook;
    public Sprite loadingLogo;
    public Sprite loadingCedar;
    public Sprite loadingBridge;
    public Sprite loadingPortal;

    [Header("Subtle motion")]
    [Range(0.1f, 2f)] public float revealSeconds = 0.6f;
    [Range(0f, 15f)] public float backgroundParallaxPixels = 8f;

    /// <summary>Cursor position relative to the screen centre, -1..1 on both axes (smoothed).</summary>
    public Vector2 CursorNormalized { get; private set; }

    AudioSource uiAudio;
    float lastHoverSound = -10f;
    float revealTime;
    float hintUntil;
    bool confirming;
    bool loadingStarted;
    bool continueRoutineRunning;
    Vector2 bgPos;
    Vector3 originalBookScale;
    CanvasGroup badgeGroup;
    Vector2 badgeBase;
    Rect lastSafe;
    Vector2 lastCanvasSize;

    /// <summary>Menu layout scale. Shared with the loading screen so both screens match pixel for pixel.</summary>
    public static float ComputeFit(RectTransform canvas)
    {
        if (canvas == null) return 1f;
        return Mathf.Min(1f,
            Mathf.Max(0.01f, (canvas.rect.width - 32f) / 1920f),
            Mathf.Max(0.01f, (canvas.rect.height - 24f) / 1080f));
    }

    void Awake()
    {
        Time.timeScale = 1f;
        SettingsMenu.Suppressed = true;
        UiInputGuard.EnsureEventSystem();
        uiAudio = GetComponent<AudioSource>();
        if (uiAudio == null) uiAudio = gameObject.AddComponent<AudioSource>();
        uiAudio.playOnAwake = false;
        uiAudio.spatialBlend = 0f;
        uiAudio.loop = false;

        // Canvas conflict fix: the Settings window (UiLayers.Settings) must be drawn ABOVE the journal menu.
        // MenuFlow's black fade (5000) and the loading screen (32000) stay above both.
        if (canvasRect != null)
        {
            var cv = canvasRect.GetComponent<Canvas>();
            if (cv != null && cv.sortingOrder >= UiLayers.Settings) cv.sortingOrder = UiLayers.Settings - 1;
        }

        if (bookRoot != null) originalBookScale = bookRoot.localScale;
        if (backgroundRect != null) bgPos = backgroundRect.anchoredPosition;
        if (continueButton != null) continueButton.onClick.AddListener(ContinueGame);
        if (newGameButton != null) newGameButton.onClick.AddListener(NewGame);
        if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettings);
        if (exitButton != null) exitButton.onClick.AddListener(QuitGame);
        if (confirmYes != null) confirmYes.onClick.AddListener(ConfirmNewGame);
        if (confirmNo != null) confirmNo.onClick.AddListener(CloseConfirmation);

        if (confirmation != null) confirmation.SetActive(false);
        if (hintGroup != null) { hintGroup.alpha = 0f; hintGroup.blocksRaycasts = false; hintGroup.interactable = false; }
        RefreshSave();
        RefreshVersion();

        if (versionBadge != null)
        {
            badgeBase = versionBadge.anchoredPosition;
            badgeGroup = versionBadge.GetComponent<CanvasGroup>();
            if (badgeGroup == null) badgeGroup = versionBadge.gameObject.AddComponent<CanvasGroup>();
            badgeGroup.interactable = false;
            badgeGroup.blocksRaycasts = false;
        }

        if (exitButton != null)
        {
            // The quit button is hidden on mobile / WebGL. The VerticalLayoutGroup re-centres the others.
            bool showQuit = !Application.isMobilePlatform && Application.platform != RuntimePlatform.WebGLPlayer;
            exitButton.gameObject.SetActive(showQuit);
            if (!showQuit && settingsButton != null && continueButton != null)
            {
                var settingsNav = settingsButton.navigation;
                settingsNav.selectOnDown = continueButton;
                settingsButton.navigation = settingsNav;
                var continueNav = continueButton.navigation;
                continueNav.selectOnUp = settingsButton;
                continueButton.navigation = continueNav;
            }
        }

        revealTime = 0f;
        if (layoutGroup != null)
        {
            layoutGroup.alpha = VibeSettings.ReduceEffects ? 1f : 0f;
            layoutGroup.interactable = VibeSettings.ReduceEffects;
            layoutGroup.blocksRaycasts = VibeSettings.ReduceEffects;
        }
    }

    void Start()
    {
        // EventSystem is bootstrapped by UiInputGuard; selection also supports keyboard/gamepad.
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(
                continueButton != null && continueButton.interactable
                    ? continueButton.gameObject : (newGameButton != null ? newGameButton.gameObject : null));
    }

    void OnDestroy() { SettingsMenu.Suppressed = false; }

    // The version text is driven by code (the old installer baked "v1.9" into the PNG and kept the TMP label disabled).
    public void RefreshVersion()
    {
        if (versionLabel == null) return;
        versionLabel.text = string.IsNullOrEmpty(menuVersion) ? "v" + Application.version : menuVersion;
        versionLabel.enabled = true;
        versionLabel.raycastTarget = false;
        if (!versionLabel.gameObject.activeSelf) versionLabel.gameObject.SetActive(true);
    }

    /// <summary>"Уровень 8 · День 8" from the live save. Used by the menu and by the loading screen.</summary>
    public static string BuildSaveLine(out bool hasLevelOrDay)
    {
        hasLevelOrDay = false;
        int level, day;
        if (!SaveSystem.HasSave || !SaveSummary.TryGet(out level, out day)) return string.Empty;
        hasLevelOrDay = level > 0 || day > 0;
        if (level > 0 && day > 0) return "Уровень " + level + " · День " + day;
        if (level > 0) return "Уровень " + level;
        if (day > 0) return "День " + day;
        return string.Empty;
    }

    public void RefreshSave()
    {
        bool hasSave = SaveSystem.HasSave;
        if (continueButton != null) continueButton.interactable = hasSave;
        if (continueSubtitle == null) return;
        bool any;
        string line = BuildSaveLine(out any);
        if (hasSave && any) continueSubtitle.text = line;
        else continueSubtitle.text = hasSave ? "Продолжить приключение" : "Нет сохранения";
    }

    void ContinueGame()
    {
        if (loadingStarted || MenuFlow.Busy || !SaveSystem.HasSave) return;
        loadingStarted = true;
        StartCoroutine(ContinueRoutine());
    }

    // The player sees the reaction in the very same frame: button spring + click sound (TWJournalButtonHover),
    // page rustle, and both pages start to fade. After ~0.26 s the journal loading screen takes over
    // with an identical book, so the transition has no cut. The real async scene load stays in MenuFlow.
    IEnumerator ContinueRoutine()
    {
        continueRoutineRunning = true;
        PlayUi(pageClip, .85f);
        if (continueButton != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);

        CanvasGroup left = EnsureGroup(leftPageRoot);
        CanvasGroup right = EnsureGroup(rightPageRoot);
        Vector2 rightBase = rightPageRoot != null ? rightPageRoot.anchoredPosition : Vector2.zero;
        float duration = VibeSettings.ReduceEffects ? 0f : .26f;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
            if (left != null) left.alpha = 1f - k;
            if (right != null) right.alpha = 1f - k;
            if (rightPageRoot != null) rightPageRoot.anchoredPosition = rightBase + Vector2.down * (8f * k);
            yield return null;
        }
        if (left != null) left.alpha = 0f;
        if (right != null) right.alpha = 0f;
        if (rightPageRoot != null) rightPageRoot.anchoredPosition = rightBase;

        TWJournalLoadingScreen.Show(this);
        MenuFlow.StartGame(MenuRequest.Continue);
        continueRoutineRunning = false;
    }

    static CanvasGroup EnsureGroup(RectTransform rt)
    {
        if (rt == null) return null;
        var g = rt.GetComponent<CanvasGroup>();
        if (g == null) g = rt.gameObject.AddComponent<CanvasGroup>();
        return g;
    }

    void RestoreAfterFailedLoad()
    {
        loadingStarted = false;
        var left = leftPageRoot != null ? leftPageRoot.GetComponent<CanvasGroup>() : null;
        var right = rightPageRoot != null ? rightPageRoot.GetComponent<CanvasGroup>() : null;
        if (left != null) left.alpha = 1f;
        if (right != null) right.alpha = 1f;
        RefreshSave();
        ShowLoreHint("Не получилось открыть игру. Попробуй ещё раз.");
        if (EventSystem.current != null && continueButton != null && continueButton.interactable)
            EventSystem.current.SetSelectedGameObject(continueButton.gameObject);
    }

    void NewGame()
    {
        if (MenuFlow.Busy || loadingStarted) return;
        if (!SaveSystem.HasSave) { MenuFlow.StartGame(MenuRequest.NewGame); return; }
        if (confirmation == null) return;
        confirming = true;
        if (confirmationTitle != null) confirmationTitle.text = "Новая игра";
        if (confirmationText != null) confirmationText.text = "Сохранённый прогресс будет удалён.\nНачать заново?";
        confirmation.SetActive(true);
        if (EventSystem.current != null && confirmNo != null)
            EventSystem.current.SetSelectedGameObject(confirmNo.gameObject);
    }

    void ConfirmNewGame()
    {
        if (!confirming || MenuFlow.Busy) return;
        confirming = false;
        if (confirmation != null) confirmation.SetActive(false);
        // Existing MenuFlow executes SaveSystem.NewGame on game-scene load.
        MenuFlow.StartGame(MenuRequest.NewGame);
    }

    void CloseConfirmation()
    {
        confirming = false;
        if (confirmation != null) confirmation.SetActive(false);
        if (EventSystem.current != null && newGameButton != null)
            EventSystem.current.SetSelectedGameObject(newGameButton.gameObject);
    }

    void OpenSettings() { if (!MenuFlow.Busy && !loadingStarted) SettingsMenu.OpenFromMenu(); }

    void QuitGame()
    {
        if (MenuFlow.Busy || loadingStarted) return;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // Always read the project's live audio settings on each interaction.
    // No independent volume state, no changes to GameAudio or saved settings.
    void PlayUi(AudioClip clip, float gain)
    {
        if (clip == null || uiAudio == null) return;
        float vol = GameAudio.SfxLevel * uiSoundVolume * gain;
        if (vol > .001f) uiAudio.PlayOneShot(clip, Mathf.Clamp01(vol));
    }

    public void PlayHoverSound()
    {
        if (Time.unscaledTime - lastHoverSound < .085f) return;
        lastHoverSound = Time.unscaledTime;
        PlayUi(hoverClip, .6f);
    }
    public void PlayClickSound() { PlayUi(clickClip, 1f); }
    public void PlayLoreSound() { PlayUi(loreClip, .68f); }

    public void ShowLoreHint(string message)
    {
        if (string.IsNullOrEmpty(message) || hintLabel == null) return;
        hintLabel.text = message;
        hintUntil = Time.unscaledTime + 3.2f;
    }

    void Update()
    {
        // If the game scene could not be opened (e.g. missing from Build Settings), unlock the menu and allow a retry.
        if (loadingStarted && !continueRoutineRunning && !MenuFlow.Busy && TWJournalLoadingScreen.Active == null &&
            (SceneManager.GetActiveScene().buildIndex == MenuFlow.MainMenuScene ||
             SceneManager.GetActiveScene().name == "MainMenu"))
            RestoreAfterFailedLoad();
        if (Input.GetKeyDown(KeyCode.Escape) && confirming) CloseConfirmation();

        // No Update-time save polling: saving is read once on scene entry or when explicitly refreshed.
        bool reduced = VibeSettings.ReduceEffects;
        revealTime += Time.unscaledDeltaTime;
        float reveal = reduced ? 1f : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(revealTime / Mathf.Max(0.1f, revealSeconds)));
        if (layoutGroup != null)
        {
            layoutGroup.alpha = reveal;
            layoutGroup.interactable = reveal > 0.95f && !loadingStarted;
            layoutGroup.blocksRaycasts = reveal > 0.95f;
        }
        if (bookRoot != null) bookRoot.localScale = originalBookScale * (reduced ? 1f : Mathf.Lerp(0.965f, 1f, reveal));
        if (layoutRoot != null) layoutRoot.localScale = Vector3.one * ComputeFit(canvasRect);

        Vector2 mouse = new Vector2(Screen.width > 0 ? Input.mousePosition.x / Screen.width : 0.5f,
            Screen.height > 0 ? Input.mousePosition.y / Screen.height : 0.5f);
        Vector2 centred = new Vector2(Mathf.Clamp(mouse.x * 2f - 1f, -1f, 1f), Mathf.Clamp(mouse.y * 2f - 1f, -1f, 1f));
        if (reduced || Application.isMobilePlatform) centred = Vector2.zero;
        CursorNormalized = Vector2.Lerp(CursorNormalized, centred, 1f - Mathf.Exp(-4f * Time.unscaledDeltaTime));

        if (backgroundRect != null)
        {
            Vector2 offset = reduced || Application.isMobilePlatform ? Vector2.zero :
                (mouse - Vector2.one * 0.5f) * backgroundParallaxPixels;
            backgroundRect.anchoredPosition = Vector2.Lerp(backgroundRect.anchoredPosition,
                bgPos - offset, 1f - Mathf.Exp(-2f * Time.unscaledDeltaTime));
        }

        UpdateVersionBadge(reveal);

        if (hintGroup != null)
        {
            float a = Time.unscaledTime < hintUntil ? 1f : 0f;
            hintGroup.alpha = Mathf.MoveTowards(hintGroup.alpha, a, Time.unscaledDeltaTime * 4f);
        }
    }

    // The badge lives on the Canvas (anchored to the bottom-right corner of the SCREEN, outside the scaled book layout)
    // and keeps clear of notches / rounded corners via Screen.safeArea.
    void UpdateVersionBadge(float reveal)
    {
        if (versionBadge == null || canvasRect == null) return;
        if (badgeGroup != null) badgeGroup.alpha = reveal;
        Rect safe = Screen.safeArea;
        Vector2 size = canvasRect.rect.size;
        if (safe == lastSafe && size == lastCanvasSize) return;
        lastSafe = safe;
        lastCanvasSize = size;
        if (Screen.width <= 0 || Screen.height <= 0) return;
        float sx = size.x / Screen.width, sy = size.y / Screen.height;
        float right = Mathf.Max(0f, Screen.width - safe.xMax) * sx;
        float bottom = Mathf.Max(0f, safe.yMin) * sy;
        versionBadge.anchoredPosition = badgeBase + new Vector2(-right, bottom);
    }
}
