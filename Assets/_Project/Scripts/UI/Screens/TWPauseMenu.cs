// TimberWings Settings 2.0 — compact pause screen using existing UiKit.Candy assets.
// The pause screen is separate from SettingsMenu; no prefabs or scene setup needed.
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-1500)]
public sealed class TWPauseMenu : MonoBehaviour
{
    static TWPauseMenu instance;
    public static bool IsOpen => instance != null && instance.pauseOpen && !instance.settingsOpen;

    GameObject canvasRoot;
    GameObject pausePanel;
    GameObject confirmation;
    TMP_Text confirmationLabel;
    TMP_Text statusLabel;
    Button yesButton;
    Button continueButton, settingsButton, menuButton, quitButton;
    TMP_Text pauseTitle;
    bool pauseOpen;
    bool settingsOpen;
    bool exiting;
    float previousTimeScale = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { instance = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void RegisterModal() { UiModal.Register(typeof(TWPauseMenu), () => IsOpen); }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (instance != null) return;
        var host = new GameObject("TWPauseMenu");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<TWPauseMenu>();
    }

    void Awake() { SceneManager.sceneLoaded += OnSceneLoaded; }
    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (pauseOpen || settingsOpen) Time.timeScale = previousTimeScale;
        if (instance == this) instance = null;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        pauseOpen = false;
        settingsOpen = false;
        exiting = false;
        if (canvasRoot != null) canvasRoot.SetActive(false);
    }

    void Update()
    {
        // SettingsMenu handles Esc/Back/X. Its unmodified version does not call us
        // when closing, so detect that transition on the following frame.
        if (settingsOpen)
        {
            if (!SettingsMenu.IsOpen) ReturnFromSettings();
            return;
        }
        if (!Input.GetKeyDown(KeyCode.Escape)) return;
        if (pauseOpen)
        {
            if (confirmation != null && confirmation.activeSelf) HideConfirmation();
            else ResumeGame();
            return;
        }
        if (!CanPause()) return;
        OpenPause();
    }

    void LateUpdate()
    {
        // Keep the old SettingsMenu from processing the very same Esc press
        // that just resumed the game; release suppression only after its Update.
        if (!pauseOpen && !settingsOpen && !MenuFlow.Busy &&
            SceneManager.GetActiveScene().buildIndex == MenuFlow.GameScene &&
            SettingsMenu.Suppressed)
            SettingsMenu.Suppressed = false;
    }

    static bool CanPause()
    {
        if (MenuFlow.Busy || SettingsMenu.Suppressed || SettingsMenu.IsOpen || EndingSequence.Active) return false;
        if (SceneManager.GetActiveScene().buildIndex != MenuFlow.GameScene) return false;
        if (Time.timeScale < 0.5f || MobileInput.ModalOpen) return false;
        return !UiModal.AnyOpen(typeof(TWPauseMenu));
    }

    void OpenPause()
    {
        if (canvasRoot == null) Build();
        UiInputGuard.EnsureEventSystem();
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        pauseOpen = true;
        settingsOpen = false;
        exiting = false;
        SettingsMenu.Suppressed = true; // Esc must not reopen legacy settings.
        if (statusLabel != null) statusLabel.text = "";
        RefreshLanguage();
        if (confirmation != null) confirmation.SetActive(false);
        canvasRoot.SetActive(true);
        pausePanel.SetActive(true);
    }

    void ResumeGame()
    {
        if (!pauseOpen || settingsOpen) return;
        pauseOpen = false;
        if (canvasRoot != null) canvasRoot.SetActive(false);
        Time.timeScale = previousTimeScale;
    }

    void OpenSettings()
    {
        if (!pauseOpen || settingsOpen || exiting) return;
        settingsOpen = true;
        canvasRoot.SetActive(false);
        // SettingsMenu.Toggle() exists in both original and Settings V2 sources.
        // Temporarily remove the pause modal/suppression before requesting it.
        SettingsMenu.Suppressed = false;
        SettingsMenu.Toggle();
        if (!SettingsMenu.IsOpen)
        {
            settingsOpen = false;
            SettingsMenu.Suppressed = true;
            canvasRoot.SetActive(true);
        }
    }

    // Called by SettingsMenu.Close(), including Esc / Back / X.
    public static void ReturnFromSettings()
    {
        if (instance == null || !instance.pauseOpen || !instance.settingsOpen) return;
        if (SceneManager.GetActiveScene().buildIndex != MenuFlow.GameScene || MenuFlow.Busy)
        {
            instance.pauseOpen = false;
            instance.settingsOpen = false;
            if (instance.canvasRoot != null) instance.canvasRoot.SetActive(false);
            return;
        }
        instance.settingsOpen = false;
        SettingsMenu.Suppressed = true;
        instance.pausePanel.SetActive(true);
        instance.canvasRoot.SetActive(true);
        instance.RefreshLanguage();
        Time.timeScale = 0f;
    }

    void ShowConfirmation(bool quit)
    {
        if (!pauseOpen || settingsOpen || confirmation == null) return;
        exiting = quit;
        confirmationLabel.text = GameLanguage.T(quit
            ? "Сохранить игру и выйти на рабочий стол?"
            : "Сохранить игру и вернуться в главное меню?");
        var label = yesButton.GetComponentInChildren<TMP_Text>();
        if (label != null) label.text = GameLanguage.T(quit ? "Выйти" : "В меню");
        confirmation.SetActive(true);
    }

    void HideConfirmation() { if (confirmation != null) confirmation.SetActive(false); }

    void ConfirmExit()
    {
        if (settingsOpen) return;
        if (!SaveSystem.SaveNow())
        {
            HideConfirmation();
            if (statusLabel != null) statusLabel.text = GameLanguage.T("Не удалось сохранить игру. Выход отменён.");
            return;
        }
        HideConfirmation();
        pauseOpen = false;
        if (canvasRoot != null) canvasRoot.SetActive(false);
        Time.timeScale = 1f;
        if (exiting)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        else MenuFlow.ReturnToMenu();
    }

    void RefreshLanguage()
    {
        if (pauseTitle != null) pauseTitle.text = GameLanguage.T("Пауза");
        SetButtonText(continueButton, "Продолжить");
        SetButtonText(settingsButton, "Настройки");
        SetButtonText(menuButton, "В главное меню");
        SetButtonText(quitButton, "Выйти из игры");
    }
    static void SetButtonText(Button button, string text)
    {
        if (button == null) return;
        var label = button.GetComponentInChildren<TMP_Text>();
        if (label != null) label.text = GameLanguage.T(text);
    }

    static void TopLeft(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
    }
    static void Fill(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
    Button MakeButton(Transform parent, string label, UiBtn style, float x, float y, float w, float h, UnityEngine.Events.UnityAction handler)
    {
        var button = UiKit.MakeButton(parent, label, style, new Vector2(w, h), 31f);
        TopLeft((RectTransform)button.transform, x, y, w, h);
        button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
        button.onClick.AddListener(handler);
        return button;
    }

    void Build()
    {
        using (UiKit.UseSkin(UiSkin.Candy))
        {
            canvasRoot = new GameObject("TWPauseCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasRoot.layer = 5;
            canvasRoot.transform.SetParent(transform, false);
            var canvas = canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = UiLayers.Settings - 1;
            var scaler = canvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;

            var backdrop = UiKit.MakeImage(canvasRoot.transform, "Dim", null, false);
            backdrop.color = new Color(.10f, .12f, .27f, .68f);
            backdrop.raycastTarget = true;
            Fill(backdrop.rectTransform);

            var panel = UiKit.MakePanel(backdrop.transform, new Vector2(610f, 705f), false, 1.5f, "CandyPausePanel");
            pausePanel = panel.gameObject;
            panel.raycastTarget = true;
            var rt = panel.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
            rt.pivot = new Vector2(.5f, .5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(610f, 705f);

            var plaque = UiKit.MakePlaque(rt, new Vector2(425f, 94f), 1.5f, "PauseTitle");
            TopLeft(plaque.rectTransform, 92f, 18f, 425f, 94f);
            pauseTitle = UiKit.MakeLabel(plaque.transform, "Пауза", 48f, UiKit.CandyInk);
            Fill(pauseTitle.rectTransform);
            var subtitle = UiKit.MakeLabel(rt, "TimberWings", 26f, UiKit.CandyMuted);
            TopLeft(subtitle.rectTransform, 118f, 117f, 375f, 42f);

            continueButton = MakeButton(rt, "Продолжить", UiBtn.Yellow, 72f, 188f, 466f, 87f, ResumeGame);
            settingsButton = MakeButton(rt, "Настройки", UiBtn.Gray, 72f, 293f, 466f, 87f, OpenSettings);
            menuButton = MakeButton(rt, "В главное меню", UiBtn.Gray, 72f, 398f, 466f, 87f, () => ShowConfirmation(false));
            quitButton = MakeButton(rt, "Выйти из игры", UiBtn.Gray, 72f, 503f, 466f, 87f, () => ShowConfirmation(true));
            statusLabel = UiKit.MakeLabel(rt, "", 20f, UiKit.CandyMuted);
            TopLeft(statusLabel.rectTransform, 45f, 614f, 520f, 58f);

            // A second, smaller Candy panel for destructive actions; no separate scene.
            var shade = UiKit.MakeImage(backdrop.transform, "PauseConfirmDim", null, false);
            shade.color = new Color(.03f, .05f, .15f, .65f);
            shade.raycastTarget = true;
            Fill(shade.rectTransform);
            confirmation = shade.gameObject;
            var dialog = UiKit.MakePanel(shade.transform, new Vector2(800f, 375f), false, 1.4f, "ConfirmPauseAction");
            dialog.raycastTarget = true;
            var dr = dialog.rectTransform;
            dr.anchorMin = dr.anchorMax = new Vector2(.5f, .5f);
            dr.pivot = new Vector2(.5f, .5f);
            dr.anchoredPosition = Vector2.zero;
            dr.sizeDelta = new Vector2(800f, 375f);
            confirmationLabel = UiKit.MakeLabel(dr, "", 34f, UiKit.CandyInk);
            TopLeft(confirmationLabel.rectTransform, 35f, 65f, 730f, 130f);
            MakeButton(dr, "Отмена", UiBtn.Gray, 75f, 247f, 285f, 78f, HideConfirmation);
            yesButton = MakeButton(dr, "В меню", UiBtn.Yellow, 440f, 247f, 285f, 78f, ConfirmExit);
            confirmation.SetActive(false);
        }
        canvasRoot.SetActive(false);
    }
}
