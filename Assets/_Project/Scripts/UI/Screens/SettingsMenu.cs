// Облачное меню паузы и настроек TimberWings. Создаётся автоматически.
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static UiLayout;

[DefaultExecutionOrder(-1000)]   // раньше окон: видим, было ли открыто окно в момент нажатия Esc
public class SettingsMenu : MonoBehaviour
{
    public static SettingsMenu Instance { get; private set; }
    public static bool IsOpen { get { return Instance != null && Instance.open; } }

    // true в сцене главного меню: Esc не открывает паузу (там своё меню). Ставит MenuFlow и MainMenuStub.
    public static bool Suppressed;

    // Окно из главного меню скрывает действия с текущим сохранением.
    public static void OpenFromMenu()
    {
        if (Instance == null || Instance.open) return;
        Instance.menuMode = true;
        Instance.Open();
    }
    bool menuMode;

    // громкость музыки 0..1 (оставлено для старого кода, настоящее значение хранит GameAudio)
    public static float MusicVolume
    {
        get { return GameAudio.Music; }
        set { GameAudio.Music = value; }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { Instance = null; Suppressed = false; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Instance != null) return;
        var go = new GameObject("SettingsMenu");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<SettingsMenu>();
    }

    public static void Toggle()
    {
        if (Instance == null) return;
        if (Instance.open) Instance.Close(); else if (Instance.CanOpen()) Instance.Open();
    }

    const float WinW = 1760f, WinH = 893f, RowH = 60f;
    readonly Button[] tabs = new Button[6];
    readonly GameObject[] pages = new GameObject[6];
    readonly List<GameObject> gameRows = new List<GameObject>();
    readonly List<Action> loadDraft = new List<Action>();
    readonly List<Action> resetDraft = new List<Action>();
    readonly List<Action> applyDraft = new List<Action>();
    Texture2D referenceArt;
    TMP_Text footerStatus, fpsLabel, mainMenuNote;
    float fpsTime;
    int fpsFrames;

    bool open, built;
    float prevScale = 1f;
    float anim;                                   // 0..1, плавное открытие
    GameObject confirmGo;                         // окно подтверждения (одно на «Новая игра» и «В главное меню»)
    TMP_Text confirmTitle, confirmBody, confirmOkLabel;
    Action confirmAction;                         // что делает кнопка подтверждения
    Button menuBtn;
    bool confirmOpen;
    RectTransform panel;
    CanvasGroup overlay;
    GameObject canvasGo;
    Button continueBtn;
    readonly List<Action> refreshers = new List<Action>();
    float fit = 1f;
    // Нажатия обрабатываем сами, по координатам курсора, а не через EventSystem: меню работает, даже если EventSystem в сцене сломан или занят
    class HitBtn { public RectTransform rt; public Button button; public Action act; public bool modal; }   // modal: кнопка окна подтверждения
    readonly List<HitBtn> hitBtns = new List<HitBtn>();
    readonly List<SettingsSliderDrag> hitSliders = new List<SettingsSliderDrag>();
    HitBtn pressedBtn;
    SettingsSliderDrag draggedSlider;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void RegisterModal() { UiModal.Register(typeof(SettingsMenu), () => IsOpen); }

    // ============================================================ жизненный цикл
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (open) RestoreTime();
        if (Instance == this) Instance = null;
    }

    void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        Close();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (confirmOpen) HideConfirm();          // сначала закрываем диалог, меню остаётся
            else if (open) Close();
            else if (CanOpen()) Open();
        }

        if (!open) return;

        PollPointer();
        if (!open) return;
        FitToScreen();
        fpsLabel.gameObject.SetActive(PerformanceBoot.ShowSettingsFps);
        fpsTime += Time.unscaledDeltaTime;
        fpsFrames++;
        if (fpsTime >= 0.35f)
        {
            fpsLabel.text = "FPS  " + Mathf.RoundToInt(fpsFrames / fpsTime);
            fpsTime = 0f;
            fpsFrames = 0;
        }

        // плавное открытие, по реальному времени (игра на паузе)
        anim = Mathf.MoveTowards(anim, 1f, Time.unscaledDeltaTime / 0.18f);
        float e = UiDraw.EaseOutCubic(anim);
        if (overlay != null) overlay.alpha = e;
        if (panel != null) panel.localScale = Vector3.one * (Mathf.Lerp(0.92f, 1f, e) * fit);
    }

    // меню нельзя открыть поверх заставки, другого окна или большой карты (им Esc нужен, чтобы закрыться)
    bool CanOpen()
    {
        if (Suppressed) return false;
        if (MenuFlow.Busy) return false;          // идёт переход между сценами
        if (EndingSequence.Active) return false;
        if (MobileInput.ModalOpen) return false;
        return true;
    }

    // ============================================================ открыть / закрыть
    void Open()
    {
        if (!built) Build();
        if (!built) return;
        EnsureEventSystem();        // после перезагрузки сцены EventSystem, который создали мы, умирает: без него в меню ничего не нажимается

        prevScale = Time.timeScale;
        Time.timeScale = 0f;
        open = true;
        anim = 0f;
        FitToScreen();
        HideConfirm();
        foreach (var load in loadDraft) load();
        RefreshAll();
        ApplyMenuMode();
        SelectTab(0);
        fpsTime = 0f;
        fpsFrames = 0;
        fpsLabel.text = "";
        footerStatus.text = "Изменения вступают в силу после применения.";
        pressedBtn = null; draggedSlider = null;
        canvasGo.SetActive(true);
        if (overlay != null) overlay.alpha = 0f;
        if (panel != null) panel.localScale = Vector3.one * (0.92f * fit);
    }

    void Close()
    {
        if (!open) return;
        open = false;
        menuMode = false;
        HideConfirm();
        PlayerPrefs.Save();
        if (canvasGo != null) canvasGo.SetActive(false);
        RestoreTime();
    }

    void RestoreTime()
    {
        // хит-стоп (timeScale 0.06) на момент паузы не должен «залипнуть»
        Time.timeScale = prevScale < 0.5f ? 1f : prevScale;
    }

    // ============================================================ действия
    void Resume() { Close(); }

    // «Продолжить»: читаем файл и возвращаемся в игру. Без файла кнопка неактивна.
    void ContinueGame()
    {
        if (menuMode) return;
        if (!SaveSystem.HasSave) return;
        SaveSystem.Load();      // повреждённый файл: тост покажет SaveIndicator (LoadedFromBackup / LoadFailed)
        Close();
    }

    void ShowNewGameConfirm()
    {
        ShowConfirm("НОВАЯ ИГРА", "Весь прогресс будет удалён.\nЭто нельзя отменить.", "Удалить", StartNewGame);
    }

    void ShowConfirm(string title, string body, string okText, Action onOk)
    {
        if (confirmGo == null) return;
        if (confirmTitle != null) confirmTitle.text = title;
        if (confirmBody != null) confirmBody.text = body;
        if (confirmOkLabel != null) confirmOkLabel.text = okText;
        confirmAction = onOk;
        confirmOpen = true;
        pressedBtn = null; draggedSlider = null;
        confirmGo.SetActive(true);
    }

    void ConfirmOk()
    {
        var a = confirmAction;
        HideConfirm();
        if (a != null) a();
    }

    void HideConfirm()
    {
        confirmOpen = false;
        confirmAction = null;
        pressedBtn = null;
        if (confirmGo != null) confirmGo.SetActive(false);
    }

    void StartNewGame()
    {
        HideConfirm();
        SaveSystem.NewGame();                  // все системы в дефолт, файл и копия удалены
        SaveSystem.RequestSave("settings");    // настройки (свет, сутки) не прогресс: сразу пишем их в чистый файл
        open = false;
        if (canvasGo != null) canvasGo.SetActive(false);
        Time.timeScale = 1f;
        var sc = SceneManager.GetActiveScene();
        if (sc.buildIndex >= 0) SceneManager.LoadScene(sc.buildIndex); else SceneManager.LoadScene(sc.name);
    }

    // в главное меню нельзя посреди диалога, сна, показа стройки и финальной заставки: у них есть статические флаги
    static bool CanLeaveToMenu()
    {
        if (EndingSequence.Active) return false;
        if (DialogueManager.IsBusy || DialogueSequence.AnyPlaying) return false;
        if (CampSleep.Sleeping || DayNightCycle.Warping) return false;
        if (BuildingController.AnyRevealing) return false;
        return !MenuFlow.Busy;
    }

    void AskMainMenu()
    {
        if (!CanLeaveToMenu()) return;
        ShowConfirm("В ГЛАВНОЕ МЕНЮ", "Игра будет сохранена.\nВернуться в главное меню?", "В меню", GoToMainMenu);
    }

    void GoToMainMenu()
    {
        HideConfirm();
        if (!CanLeaveToMenu()) return;
        SaveSystem.SaveNow();                  // до загрузки сцены: потом сейверы сцены снимутся и не успеют записать
        PlayerPrefs.Save();
        open = false;
        if (canvasGo != null) canvasGo.SetActive(false);
        Time.timeScale = 1f;                   // пауза не должна «залипнуть» в меню
        MenuFlow.ReturnToMenu();
    }

    void ApplyMenuMode()
    {
        foreach (var row in gameRows) row.SetActive(!menuMode);
        mainMenuNote.gameObject.SetActive(menuMode);
        continueBtn.interactable = SaveSystem.HasSave;
        menuBtn.interactable = CanLeaveToMenu();
        TintButton(continueBtn, UiBtn.Gray);
        TintButton(menuBtn, UiBtn.Gray);
    }

    void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // окно рассчитано на экран 1920x1080; на низких экранах (телефон вбок) уменьшаем, чтобы все строки были видны
    void FitToScreen()
    {
        float lw = Mathf.Log(Mathf.Max(1, Screen.width) / 1920f, 2f);
        float lh = Mathf.Log(Mathf.Max(1, Screen.height) / 1080f, 2f);
        float sf = Mathf.Pow(2f, Mathf.Lerp(lw, lh, 0.5f));         // тот же расчёт, что у CanvasScaler (match 0.5)
        float canvasH = Screen.height / sf, canvasW = Screen.width / sf;
        fit = Mathf.Min(1f, canvasH * 0.9f / (WinH + 60f), canvasW * 0.96f / WinW);
    }

    void RefreshAll()
    {
        for (int i = 0; i < refreshers.Count; i++) refreshers[i]();
    }

    // ============================================================ построение окна
    void EnsureEventSystem() { UiInputGuard.EnsureEventSystem(); }

    void Bind(Button b, Action act)
    {
        hitBtns.Add(new HitBtn { rt = (RectTransform)b.transform, button = b, act = act });
    }

    void BindModal(Button b, Action act)
    {
        hitBtns.Add(new HitBtn { rt = (RectTransform)b.transform, button = b, act = act, modal = true });
    }

    void PollPointer()
    {
        Vector2 pos = Input.mousePosition;
        if (Input.GetMouseButtonDown(0))
        {
            pressedBtn = null; draggedSlider = null;
            if (!confirmOpen)
                for (int i = 0; i < hitSliders.Count; i++)
                    if (hitSliders[i].Contains(pos)) { draggedSlider = hitSliders[i]; draggedSlider.PickAt(pos); break; }
            if (draggedSlider == null)
                for (int i = 0; i < hitBtns.Count; i++)
                    if (hitBtns[i].modal == confirmOpen && hitBtns[i].button.interactable && hitBtns[i].rt.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(hitBtns[i].rt, pos, null)) { pressedBtn = hitBtns[i]; break; }
        }
        else if (Input.GetMouseButton(0) && draggedSlider != null)
        {
            draggedSlider.PickAt(pos);
        }

        if (Input.GetMouseButtonUp(0))
        {
            if (draggedSlider != null) draggedSlider = null;
            else if (pressedBtn != null)
            {
                var b = pressedBtn; pressedBtn = null;
                if (b.button.interactable && b.rt.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(b.rt, pos, null)) b.act();   // отпустил на той же кнопке: нажатие
            }
        }
    }

    void Build()
    {
        EnsureEventSystem();
        using (UiKit.UseSkin(UiSkin.Candy))
        {
            canvasGo = new GameObject("SettingsCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.layer = 5;
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = UiLayers.Settings;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var dim = UiKit.MakeImage(canvasGo.transform, "Overlay", null, false);
            dim.color = new Color(0.12f, 0.10f, 0.27f, 0.52f);
            dim.raycastTarget = true;
            Stretch(dim.rectTransform);
            overlay = dim.gameObject.AddComponent<CanvasGroup>();

            var window = UiKit.MakePanel(dim.transform, new Vector2(WinW, WinH), false, 2f, "CloudSettings");
            panel = window.rectTransform;
            Anch(panel, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(WinW, WinH));

            var title = UiKit.MakeImage(panel, "TitlePlaque", UiKit.Plaque, true, 1.5f);
            TL(title.rectTransform, 615f, -34f, 530f, 105f);
            Label(title.transform, "Настройки", 49f, 25f, 3f, 480f, 91f);
            Label(panel, "TimberWings", 30f, 32f, 7f, 220f, 53f, true);
            fpsLabel = Label(panel, "", 24f, 1528f, 11f, 185f, 44f);
            referenceArt = Resources.Load<Texture2D>("TimberWings/SettingsLandscape");

            BuildTabs();
            BuildPreview();
            BuildFooter();
            BuildConfirm(dim.transform);
        }

        canvasGo.SetActive(false);
        built = true;
    }

    void BuildTabs()
    {
        string[] labels = { "Графика", "Камера", "Звук", "Управление", "Интерфейс", "Игра" };
        UiIcon[] icons = { UiIcon.Tree, UiIcon.Target, UiIcon.Bolt, UiIcon.Boot, UiIcon.Book, UiIcon.Axe };
        for (int i = 0; i < labels.Length; i++)
        {
            int index = i;
            float x = 34f + 284f * i;
            var tab = MakeButton(panel, labels[i], x, 79f, 270f, 65f, () => SelectTab(index));
            var tabIcon = UiKit.MakeIcon(tab.transform, icons[i], 33f);
            TL(tabIcon.rectTransform, 15f, 13f, 33f, 33f);
            var tabLabel = tab.GetComponentInChildren<TMP_Text>();
            tabLabel.rectTransform.offsetMin = new Vector2(30f, tabLabel.rectTransform.offsetMin.y);
            tabs[i] = tab;

            var page = UiKit.MakePanel(panel, new Vector2(800f, 610f), false, 1f, "Page_" + labels[i]);
            TL(page.rectTransform, 34f, 157f, 800f, 610f);
            pages[i] = page.gameObject;
        }

        BuildGraphics(pages[0].transform);
        var c = pages[1].transform;
        Choice(c, 0, "Режим камеры", new[] { "Свободная", "Изометрия" }, () => OrbitCamera.Enabled ? 0 : 1, v => OrbitCamera.Enabled = v == 0, 0);
        ToggleRow(c, 1, "Поворот мышью без ПКМ", () => OrbitCamera.MouseLook, v => OrbitCamera.MouseLook = v, true);
        Slider(c, 2, "Чувствительность мыши", () => Mathf.InverseLerp(0.25f, 2f, PerformanceBoot.MouseMultiplier),
            v => PerformanceBoot.MouseMultiplier = Mathf.Lerp(0.25f, 2f, v), 3f / 7f,
            v => Mathf.RoundToInt(Mathf.Lerp(25f, 200f, v)) + " %");
        ToggleRow(c, 3, "Меньше тряски и эффектов", () => VibeSettings.ReduceEffects, v => VibeSettings.ReduceEffects = v, false);
        Info(c, 4, "Чувствительность и поворот мышью действуют в свободной камере.");
        Placeholder(c, 5, "Инверсия оси Y");
        Placeholder(c, 6, "Настройка FOV");

        c = pages[2].transform;
        ToggleRow(c, 0, "Звук в игре", () => !GameAudio.Muted, v => GameAudio.Muted = !v, true);
        Slider(c, 1, "Общая громкость", () => GameAudio.Master, v => GameAudio.Master = v, 1f);
        Slider(c, 2, "Музыка", () => GameAudio.Music, v => GameAudio.Music = v, 1f);
        Slider(c, 3, "Звуковые эффекты", () => GameAudio.Sfx, v => GameAudio.Sfx = v, 0.8f);
        Slider(c, 4, "Голоса", () => GameAudio.Voice, v => GameAudio.Voice = v, 1f);
        Slider(c, 5, "Звуки природы", () => GameAudio.Ambient, v => GameAudio.Ambient = v, 0.5f);
        Choice(c, 6, "Речь персонажей", new[] { "Озвучка", "Бормотание", "Выкл" },
            () => (int)GameAudio.Speech, v => GameAudio.Speech = (DialogueSpeechMode)v, 0);
        ActionRow(c, 7, "Проверка текущего голоса", "Прослушать", () => SfxManager.PreviewVoice());

        c = pages[3].transform;
        Info(c, 0, "WASD — движение   ·   Esc — пауза");
        Info(c, 1, "E / ЛКМ — взаимодействие и рубка");
        Info(c, 2, "Колесо — зум   ·   Alt — свободный курсор");
        Placeholder(c, 3, "Переназначение клавиш");
        Placeholder(c, 4, "Настройки геймпада");
        if (MobileInput.Enabled)
            ToggleRow(c, 5, "Экранные кнопки (после перезапуска)", () => MobileInput.OnScreenControls,
                v => MobileInput.OnScreenControls = v, Application.isMobilePlatform);
        else
            Placeholder(c, 5, "Экранные кнопки");

        c = pages[4].transform;
        ToggleRow(c, 0, "Подсказки к цели", () => VibeSettings.GuideEnabled, v => VibeSettings.GuideEnabled = v, true);
        ToggleRow(c, 1, "Счётчик FPS в настройках", () => PerformanceBoot.ShowSettingsFps, v => PerformanceBoot.ShowSettingsFps = v, true);
        ToggleRow(c, 2, "Автопереход реплик", () => DialoguePreferences.AutoAdvance, v => DialoguePreferences.AutoAdvance = v, true);
        Placeholder(c, 3, "Масштаб интерфейса");
        Placeholder(c, 4, "Язык интерфейса");

        c = pages[5].transform;
        Choice(c, 0, "Скорость суток", new[] { "Обычная", "Медленная", "Фиксированный день" },
            () => (int)GameSettings.DaySpeed, v => GameSettings.DaySpeed = (DaySpeedMode)v, 0);
        ToggleRow(c, 1, "Редкие реплики персонажей", () => DialoguePreferences.PlayerComments, v => DialoguePreferences.PlayerComments = v, true);
        ActionRow(c, 2, "Сохранение прогресса", "Сохранить", () => SaveSystem.SaveNow());
        continueBtn = ActionRow(c, 3, "Загрузить сохранение", "Продолжить", ContinueGame);
        ActionRow(c, 4, "Сброс прогресса", "Новая игра", ShowNewGameConfirm);
        menuBtn = ActionRow(c, 5, "Вернуться с сохранением", "В главное меню", AskMainMenu);
        if (!Application.isMobilePlatform && Application.platform != RuntimePlatform.WebGLPlayer)
            ActionRow(c, 6, "Закрыть игру", "Выйти", () => ShowConfirm("Выход", "Сохранить прогресс и выйти из игры?", "Выйти", () =>
            {
                if (SaveSystem.SaveNow()) QuitGame();
            }));
        mainMenuNote = Label(c, "Управление сохранением доступно из паузы.", 23f, 29f, RowY(2), 740f, RowH, true);
    }

    void BuildGraphics(Transform c)
    {
        Label(c, "Качество света", 29f, 28f, 22f, 250f, 112f, true);
        var quality = Draft(() => (int)GameSettings.LightQuality, v => GameSettings.LightQuality = (LightQualityLevel)v, 1);
        string[] names = { "Низкое", "Среднее", "Высокое" };
        int[] cropX = { 314, 469, 624 };
        for (int i = 0; i < names.Length; i++)
        {
            int index = i;
            float x = 277f + 165f * i;
            var tile = MakeButton(c, "", x, 22f, 150f, 131f, () => Change(quality, index));
            AddArt(tile.transform, new Rect(cropX[i], 272, 126, 67), 8f, 8f, 134f, 72f);
            Label(tile.transform, names[i], 22f, 5f, 84f, 140f, 36f);
            refreshers.Add(() => Style(tile, quality.Value == index ? UiBtn.Yellow : UiBtn.Gray));
        }

        var rows = NewRT("GraphicsRows", c);
        TL(rows, 0f, 146f, 800f, 446f);
        Choice(rows, 0, "Дальность прорисовки", new[] { "Ближняя", "Средняя", "Дальняя" },
            () => (int)VibeSettings.DrawDistance, v => VibeSettings.DrawDistance = (DrawDistanceLevel)v, 1);
        ToggleRow(rows, 1, "Тени", () => PerformanceBoot.Shadows, v => PerformanceBoot.Shadows = v, true);
        ToggleRow(rows, 2, "Дымка вдали", () => PerformanceBoot.DistanceFog, v => PerformanceBoot.DistanceFog = v, true);
        Choice(rows, 3, "Лимит FPS", new[] { "30", "60", "90", "120" },
            () => PerformanceBoot.FpsIndex, v => PerformanceBoot.FpsIndex = v, 1);
        if (PerformanceBoot.SupportsRenderScale)
            Slider(rows, 4, "Масштаб рендера", () => Mathf.InverseLerp(0.5f, 1f, PerformanceBoot.RenderScale),
                v => PerformanceBoot.RenderScale = Mathf.Lerp(0.5f, 1f, v), 1f,
                v => Mathf.RoundToInt(Mathf.Lerp(50f, 100f, v)) + " %");
        else
            Placeholder(rows, 4, "Масштаб рендера");
        if (!Application.isMobilePlatform && Application.platform != RuntimePlatform.WebGLPlayer)
            ToggleRow(rows, 5, "Полноэкранный режим", () => PerformanceBoot.Fullscreen, v => PerformanceBoot.Fullscreen = v, PerformanceBoot.DefaultFullscreen);
        Placeholder(rows, 6, "Сглаживание");
    }

    void BuildPreview()
    {
        var frame = UiKit.MakePanel(panel, new Vector2(876f, 610f), false, 1f, "LandscapeFrame");
        TL(frame.rectTransform, 850f, 157f, 876f, 610f);
        AddArt(frame.transform, new Rect(803, 234, 780, 550), 14f, 12f, 848f, 570f);
        var caption = UiKit.MakeImage(frame.transform, "Caption", UiKit.Plaque, true);
        TL(caption.rectTransform, 22f, 520f, 312f, 46f);
        Label(caption.transform, "Виды TimberWings", 21f, 5f, 0f, 302f, 43f);
    }

    void AddArt(Transform parent, Rect crop, float x, float y, float width, float height)
    {
        if (referenceArt == null) return;
        var go = new GameObject("Illustration", typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<RawImage>();
        image.texture = referenceArt;
        image.raycastTarget = false;
        // Координаты областей заданы на исходном макете 1672 × 941, сверху вниз.
        image.uvRect = new Rect(crop.x / 1672f, 1f - crop.yMax / 941f, crop.width / 1672f, crop.height / 941f);
        TL(image.rectTransform, x, y, width, height);
    }

    void BuildFooter()
    {
        MakeButton(panel, "По умолчанию", 305f, 784f, 345f, 68f, ResetDraft);
        MakeButton(panel, "Применить", 708f, 784f, 345f, 68f, ApplyDraft, UiBtn.Yellow);
        MakeButton(panel, "Назад", 1111f, 784f, 345f, 68f, Resume);
        footerStatus = Label(panel, "", 19f, 375f, 853f, 1010f, 30f);
    }

    void SelectTab(int index)
    {
        for (int i = 0; i < pages.Length; i++)
        {
            pages[i].SetActive(i == index);
            Style(tabs[i], i == index ? UiBtn.Yellow : UiBtn.Gray);
        }
    }

    static float RowY(int index) => 14f + index * RowH;

    void Row(Transform c, int index, string title, float labelWidth = 355f)
    {
        float y = RowY(index);
        var strip = UiKit.MakeImage(c, "SettingRow", UiKit.Slot, true, 0.7f);
        strip.color = new Color(0.93f, 0.93f, 1f, 0.68f);
        TL(strip.rectTransform, 13f, y, 774f, RowH - 5f);
        Label(c, title, 25f, 29f, y, labelWidth, RowH - 6f, true);
    }

    void Info(Transform c, int index, string line) => Row(c, index, line, 740f);

    void Placeholder(Transform c, int index, string title)
    {
        Row(c, index, title);
        var tag = Label(c, "В разработке", 21f, 542f, RowY(index), 220f, RowH - 6f);
        tag.color = UiKit.CandyMuted;
    }

    void ToggleRow(Transform c, int index, string title, Func<bool> read, Action<bool> write, bool fallback)
    {
        Row(c, index, title);
        var setting = Draft(read, write, fallback);
        var button = MakeButton(c, "", 590f, RowY(index) + 6f, 172f, 43f, () => Change(setting, !setting.Value));
        var label = button.GetComponentInChildren<TMP_Text>();
        refreshers.Add(() =>
        {
            label.text = setting.Value ? "Вкл" : "Выкл";
            Style(button, setting.Value ? UiBtn.Green : UiBtn.Gray);
        });
    }

    void Choice(Transform c, int index, string title, string[] names, Func<int> read, Action<int> write, int fallback)
    {
        Row(c, index, title);
        var setting = Draft(read, write, fallback);
        float y = RowY(index) + 6f;
        MakeButton(c, "‹", 411f, y, 49f, 43f, () => Change(setting, (setting.Value + names.Length - 1) % names.Length));
        var text = Label(c, "", 24f, 467f, y, 239f, 43f);
        MakeButton(c, "›", 713f, y, 49f, 43f, () => Change(setting, (setting.Value + 1) % names.Length));
        refreshers.Add(() => text.text = names[Mathf.Clamp(setting.Value, 0, names.Length - 1)]);
    }

    void Slider(Transform c, int index, string title, Func<float> read, Action<float> write, float fallback, Func<float, string> format = null)
    {
        Row(c, index, title);
        var setting = Draft(read, write, fallback);
        float y = RowY(index);
        var frame = UiKit.MakeBar(c, UiBar.Blue, new Vector2(250f, 24f), out var fill);
        TLC(frame.rectTransform, 544f, y + 26f, 250f, 24f);
        var knob = UiKit.MakeImage(frame.transform, "Knob", UiKit.Medallion, false);
        knob.rectTransform.sizeDelta = new Vector2(38f, 38f);
        var label = Label(c, "", 22f, 680f, y, 89f, 52f);
        var drag = frame.gameObject.AddComponent<SettingsSliderDrag>();
        drag.Init(frame.rectTransform, fill, knob.rectTransform, label, v => Change(setting, v, false), format);
        hitSliders.Add(drag);
        refreshers.Add(() => drag.SetValue(setting.Value));
    }

    Button ActionRow(Transform c, int index, string title, string label, Action action)
    {
        var group = NewRT(title, c);
        TL(group, 0f, RowY(index) - RowY(0), 800f, RowH);
        Row(group, 0, title);
        var button = MakeButton(group, label, 477f, RowY(0) + 6f, 285f, 43f, action);
        if (c.gameObject == pages[5]) gameRows.Add(group.gameObject);
        return button;
    }

    static TMP_Text Label(Transform parent, string text, float size, float x, float y, float w, float h, bool left = false)
    {
        return Lbl(parent, text, size, UiKit.CandyInk, left ? TextAlignmentOptions.Left : TextAlignmentOptions.Center, true, x, y, w, h);
    }

    Button MakeButton(Transform c, string text, float x, float y, float w, float h, Action action, UiBtn style = UiBtn.Gray)
    {
        var button = UiKit.MakeButton(c, text, style, new Vector2(w, h), h >= 60f ? 30f : 23f);
        TL((RectTransform)button.transform, x, y, w, h);
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        AutoSize(button.GetComponentInChildren<TMP_Text>(), 15f, h >= 60f ? 30f : 23f);
        TintButton(button, style);
        Bind(button, action);
        return button;
    }

    static void TintButton(Button button, UiBtn style)
    {
        button.image.color = style == UiBtn.Gray && button.interactable
            ? new Color(0.80f, 0.90f, 1f) : Color.white;
    }

    static void Style(Button button, UiBtn style)
    {
        using (UiKit.UseSkin(UiSkin.Candy))
        {
            var normal = UiKit.BtnSprite(style);
            button.image.sprite = normal;
            TintButton(button, style);
            button.spriteState = new SpriteState
            {
                highlightedSprite = UiKit.BtnSprite(style, UiBtnState.Hover),
                pressedSprite = UiKit.BtnSprite(style, UiBtnState.Pressed),
                selectedSprite = normal,
                disabledSprite = UiKit.BtnSprite(UiBtn.Gray, UiBtnState.Disabled)
            };
        }
    }

    sealed class Setting<T>
    {
        public T Value;
    }

    Setting<T> Draft<T>(Func<T> read, Action<T> write, T fallback)
    {
        var setting = new Setting<T>();
        loadDraft.Add(() => setting.Value = read());
        resetDraft.Add(() => setting.Value = fallback);
        applyDraft.Add(() =>
        {
            if (!EqualityComparer<T>.Default.Equals(setting.Value, read()))
                write(setting.Value);
        });
        return setting;
    }

    void Change<T>(Setting<T> setting, T value, bool refresh = true)
    {
        if (EqualityComparer<T>.Default.Equals(setting.Value, value)) return;
        setting.Value = value;
        footerStatus.text = "Есть изменения. Нажми «Применить», чтобы их сохранить.";
        if (refresh) RefreshAll();
    }

    void ResetDraft()
    {
        foreach (var reset in resetDraft) reset();
        RefreshAll();
        footerStatus.text = "Значения по умолчанию готовы. Нажми «Применить».";
    }

    void ApplyDraft()
    {
        foreach (var apply in applyDraft) apply();
        PlayerPrefs.Save();
        footerStatus.text = "Настройки применены";
        RefreshAll();
    }

    void BuildConfirm(Transform parent)
    {
        var dim = UiKit.MakeImage(parent, "Confirm", null, false);
        dim.color = new Color(0.02f, 0.04f, 0.08f, 0.6f);
        dim.raycastTarget = true;      // клики мимо диалога никуда не проходят
        Stretch(dim.rectTransform);
        confirmGo = dim.gameObject;

        var center = new Vector2(0.5f, 0.5f);
        var win = UiKit.MakePanel(dim.transform, new Vector2(760f, 360f), true, 1f, "ConfirmWindow");
        win.raycastTarget = true;
        Anch(win.rectTransform, center, center, Vector2.zero, new Vector2(760f, 360f));

        var title = UiKit.MakeLabel(win.transform, "НОВАЯ ИГРА", 34f, UiKit.CandyInk, TextAlignmentOptions.Center, true);
        confirmTitle = title;
        Anch(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(420f, 42f));

        var body = UiKit.MakeLabel(win.transform, "Весь прогресс будет удалён.\nЭто нельзя отменить.", 32f, UiKit.CandyInk, TextAlignmentOptions.Center, true);
        Anch(body.rectTransform, center, center, new Vector2(0f, 24f), new Vector2(680f, 130f));
        confirmBody = body;

        var cancel = UiKit.MakeButton(win.transform, "Отмена", UiBtn.Green, new Vector2(270f, 72f), 30f, 1f);
        cancel.navigation = new Navigation { mode = Navigation.Mode.None };
        Anch(cancel.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-150f, 40f), new Vector2(270f, 72f));
        var ct = cancel.GetComponentInChildren<TMP_Text>();
        ct.color = UiKit.CandyInk; ct.enableAutoSizing = true; ct.fontSizeMin = 18f; ct.fontSizeMax = 30f;
        BindModal(cancel, HideConfirm);

        var ok = UiKit.MakeButton(win.transform, "Удалить", UiBtn.Gray, new Vector2(270f, 72f), 30f, 1f);
        ok.navigation = new Navigation { mode = Navigation.Mode.None };
        Anch(ok.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(150f, 40f), new Vector2(270f, 72f));
        var ot = ok.GetComponentInChildren<TMP_Text>();
        ot.color = UiKit.CandyInk; ot.enableAutoSizing = true; ot.fontSizeMin = 18f; ot.fontSizeMax = 30f;
        confirmOkLabel = ot;
        BindModal(ok, ConfirmOk);

        confirmGo.SetActive(false);
    }


}

// Ползунок: тянешь мышью или пальцем по полоске, заливка и медальон-ручка следуют за курсором.
// Ввод приходит от SettingsMenu (PollPointer), а не от EventSystem.
public class SettingsSliderDrag : MonoBehaviour
{
    RectTransform bar, knob;
    Image fill;
    TMP_Text pct;
    Action<float> onChange;
    Func<float, string> format;
    float value;

    const float Inset = 8f;

    public void Init(RectTransform bar, Image fill, RectTransform knob, TMP_Text pct, Action<float> onChange, Func<float, string> format)
    {
        this.bar = bar; this.fill = fill; this.knob = knob; this.pct = pct; this.onChange = onChange; this.format = format;
    }

    public void SetValue(float v)
    {
        value = Mathf.Clamp01(v);
        if (fill != null) fill.fillAmount = value;
        if (pct != null) pct.text = format != null ? format(value) : Mathf.RoundToInt(value * 100f) + " %";
        if (knob != null && bar != null)
        {
            float half = bar.rect.width * 0.5f - Inset;
            knob.anchoredPosition = new Vector2(Mathf.Lerp(-half, half, value), 0f);
        }
    }

    // попал ли курсор в полоску (с запасом: ручка выше самой полоски)
    public bool Contains(Vector2 screenPos)
    {
        if (bar == null || !gameObject.activeInHierarchy) return false;
        Vector2 lp;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(bar, screenPos, null, out lp)) return false;
        Rect r = bar.rect;
        r.xMin -= 24f; r.xMax += 24f; r.yMin -= 20f; r.yMax += 20f;
        return r.Contains(lp);
    }

    public void PickAt(Vector2 screenPos)
    {
        if (bar == null || !gameObject.activeInHierarchy) return;
        Vector2 lp;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(bar, screenPos, null, out lp)) return;
        float half = bar.rect.width * 0.5f - Inset;
        float v = Mathf.Clamp01(Mathf.InverseLerp(-half, half, lp.x));
        SetValue(v);
        if (onChange != null) onChange(value);
    }

}

