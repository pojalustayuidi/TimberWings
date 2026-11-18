// TimberWings Settings A — full, direct-replacement Unity component.
// Replaces Assets/_Project/Scripts/UI/Screens/SettingsMenu.cs (no installer required).
// Keeps the original public SettingsMenu API and routes settings to existing game systems.
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static UiLayout;

[DefaultExecutionOrder(-1000)]
public class SettingsMenu : MonoBehaviour
{
    public static SettingsMenu Instance { get; private set; }
    public static bool IsOpen { get { return Instance != null && Instance.open; } }
    public static bool Suppressed;
    public static float MusicVolume { get { return GameAudio.Music; } set { GameAudio.Music = value; } }

    public static void OpenFromMenu()
    {
        if (Instance == null || Instance.open) return;
        Instance.menuMode = true;
        Instance.Open();
    }

    public static void Toggle()
    {
        if (Instance == null) return;
        if (Instance.open) Instance.Resume();
        else if (Instance.CanOpen()) Instance.Open();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { Instance = null; Suppressed = false; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void RegisterModal() { UiModal.Register(typeof(SettingsMenu), () => IsOpen); }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Instance != null) return;
        var go = new GameObject("SettingsMenu");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<SettingsMenu>();
    }

    const float WinW = 1760f, WinH = 893f, RowH = 60f;
    readonly Button[] tabs = new Button[6];
    readonly GameObject[] pages = new GameObject[6];
    readonly List<GameObject> gameRows = new List<GameObject>();
    readonly List<Action> loadDraft = new List<Action>();
    readonly List<Action> resetDraft = new List<Action>();
    readonly List<Action> applyDraft = new List<Action>();
    readonly List<Action> refreshers = new List<Action>();
    readonly List<HitBtn> hitBtns = new List<HitBtn>();
    readonly List<SettingsSliderDrag> hitSliders = new List<SettingsSliderDrag>();

    TMP_Text footerStatus, fpsLabel, mainMenuNote, previewTitle, previewDescription, previewTip1, previewTip2;
    Image dimImage, windowImage;
    RectTransform panel;
    CanvasGroup overlay;
    GameObject canvasGo, confirmGo;
    TMP_Text confirmTitle, confirmBody, confirmOkLabel;
    Action confirmAction;
    Button menuBtn, continueBtn;
    bool open, built, pending, confirmOpen, menuMode;
    int selectedTab;
    float fit=1f, anim, prevScale=1f, fpsTime;
    int fpsFrames;

    sealed class HitBtn
    {
        public RectTransform rt;
        public Button button;
        public Action act;
        public bool modal;
    }
    HitBtn pressedBtn;
    SettingsSliderDrag draggedSlider;

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
    void OnSceneLoaded(Scene scene, LoadSceneMode mode) { Close(); }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (confirmOpen) HideConfirm();
            else if (open) Resume();
            else if (CanOpen()) Open();
        }
        if (!open) return;
        if (!confirmOpen)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) SelectTab(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SelectTab(1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SelectTab(2);
            if (Input.GetKeyDown(KeyCode.Alpha4)) SelectTab(3);
            if (Input.GetKeyDown(KeyCode.Alpha5)) SelectTab(4);
            if (Input.GetKeyDown(KeyCode.Alpha6)) SelectTab(5);
        }
        PollPointer();
        if (!open) return;
        FitToScreen();
        if (fpsLabel != null)
        {
            fpsLabel.gameObject.SetActive(PerformanceBoot.ShowSettingsFps);
            fpsTime += Time.unscaledDeltaTime;
            fpsFrames++;
            if (fpsTime >= .45f)
            {
                fpsLabel.text = "FPS  " + Mathf.RoundToInt(fpsFrames / fpsTime);
                fpsTime = 0f; fpsFrames = 0;
            }
        }
        anim = SettingsAppearance.AnimateOpen ? Mathf.MoveTowards(anim,1f,Time.unscaledDeltaTime/.18f) : 1f;
        float e = UiDraw.EaseOutCubic(anim);
        if (overlay != null) overlay.alpha = e;
        if (panel != null) panel.localScale = Vector3.one * (Mathf.Lerp(.92f,1f,e)*fit);
    }

    bool CanOpen()
    {
        return !Suppressed && !MenuFlow.Busy && !EndingSequence.Active && !MobileInput.ModalOpen;
    }

    void Open()
    {
        if (!built) Build();
        if (!built) return;
        UiInputGuard.EnsureEventSystem();
        prevScale = Time.timeScale;
        Time.timeScale = 0f;
        open = true; pending = false; anim = 0f;
        HideConfirm();
        foreach (var load in loadDraft) load();
        RefreshAll();
        ApplyMenuMode();
        SelectTab(0);
        fpsFrames=0; fpsTime=0f;
        if (fpsLabel != null) fpsLabel.text="";
        footerStatus.text = GameLanguage.T("Изменения вступают в силу после применения.");
        pressedBtn=null; draggedSlider=null;
        FitToScreen();
        canvasGo.SetActive(true);
        if (overlay != null) overlay.alpha=0f;
        if (panel != null) panel.localScale=Vector3.one*(.92f*fit);
        UpdateAppearance();
    }

    void Close()
    {
        if (!open) return;
        open=false; menuMode=false; pending=false;
        HideConfirm();
        if (canvasGo != null) canvasGo.SetActive(false);
        PlayerPrefs.Save();
        RestoreTime();
    }

    void RestoreTime() { Time.timeScale = prevScale < .5f ? 1f : prevScale; }

    void Resume()
    {
        if (pending)
            ShowConfirm("НЕСОХРАНЁННЫЕ ИЗМЕНЕНИЯ",
                "Закрыть настройки без применения изменений?", "Не сохранять",
                () => { pending=false; Close(); });
        else Close();
    }

    void ContinueGame()
    {
        if (menuMode || !SaveSystem.HasSave) return;
        SaveSystem.Load();
        Close();
    }

    void StartNewGame()
    {
        HideConfirm();
        SaveSystem.NewGame();
        SaveSystem.RequestSave("settings");
        open=false;
        if (canvasGo != null) canvasGo.SetActive(false);
        Time.timeScale=1f;
        var sc=SceneManager.GetActiveScene();
        if (sc.buildIndex >= 0) SceneManager.LoadScene(sc.buildIndex);
        else SceneManager.LoadScene(sc.name);
    }

    void ShowNewGameConfirm() => ShowConfirm("НОВАЯ ИГРА", "Весь прогресс будет удалён.\nЭто нельзя отменить.", "Удалить", StartNewGame);

    static bool CanLeaveToMenu()
    {
        if (EndingSequence.Active || DialogueManager.IsBusy || DialogueSequence.AnyPlaying) return false;
        if (CampSleep.Sleeping || DayNightCycle.Warping || BuildingController.AnyRevealing) return false;
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
        SaveSystem.SaveNow();
        PlayerPrefs.Save();
        open=false;
        if (canvasGo != null) canvasGo.SetActive(false);
        Time.timeScale=1f;
        MenuFlow.ReturnToMenu();
    }
    void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying=false;
#else
        Application.Quit();
#endif
    }
    void ApplyMenuMode()
    {
        foreach (var row in gameRows) if (row != null) row.SetActive(!menuMode);
        if (mainMenuNote != null) mainMenuNote.gameObject.SetActive(menuMode);
        if (continueBtn != null) { continueBtn.interactable = !menuMode && SaveSystem.HasSave; TintButton(continueBtn,UiBtn.Gray); }
        if (menuBtn != null) { menuBtn.interactable = !menuMode && CanLeaveToMenu(); TintButton(menuBtn,UiBtn.Gray); }
    }

    void FitToScreen()
    {
        float lw=Mathf.Log(Mathf.Max(1,Screen.width)/1920f,2f);
        float lh=Mathf.Log(Mathf.Max(1,Screen.height)/1080f,2f);
        float sf=Mathf.Pow(2f,Mathf.Lerp(lw,lh,.5f));
        float canvasH=Screen.height/sf,canvasW=Screen.width/sf;
        fit=Mathf.Min(SettingsAppearance.MenuScale,canvasH*.9f/(WinH+60f),canvasW*.96f/WinW);
    }

    void RefreshAll() { foreach (var r in refreshers) r(); }

    void Bind(Button button, Action action)
    {
        hitBtns.Add(new HitBtn { rt=(RectTransform)button.transform, button=button, act=action });
    }
    void BindModal(Button button, Action action)
    {
        hitBtns.Add(new HitBtn { rt=(RectTransform)button.transform, button=button, act=action, modal=true });
    }
    void PollPointer()
    {
        Vector2 pos=Input.mousePosition;
        if (Input.GetMouseButtonDown(0))
        {
            pressedBtn=null; draggedSlider=null;
            if (!confirmOpen)
                for (int i=0;i<hitSliders.Count;i++)
                    if (hitSliders[i].Contains(pos)) { draggedSlider=hitSliders[i]; draggedSlider.PickAt(pos); break; }
            if (draggedSlider == null)
                for (int i=hitBtns.Count-1;i>=0;i--)
                    if (hitBtns[i].modal == confirmOpen && hitBtns[i].button.interactable &&
                        hitBtns[i].rt.gameObject.activeInHierarchy &&
                        RectTransformUtility.RectangleContainsScreenPoint(hitBtns[i].rt,pos,null))
                    { pressedBtn=hitBtns[i]; break; }
        }
        else if (Input.GetMouseButton(0) && draggedSlider != null)
        {
            draggedSlider.PickAt(pos);
        }
        if (Input.GetMouseButtonUp(0))
        {
            if (draggedSlider != null) draggedSlider=null;
            else if (pressedBtn != null)
            {
                var b=pressedBtn; pressedBtn=null;
                if (b.button.interactable && b.rt.gameObject.activeInHierarchy &&
                    RectTransformUtility.RectangleContainsScreenPoint(b.rt,pos,null))
                    b.act();
            }
        }
    }

    void Build()
    {
        UiInputGuard.EnsureEventSystem();
        using (UiKit.UseSkin(UiSkin.Candy))
        {
            canvasGo=new GameObject("SettingsCanvas", typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            canvasGo.layer=5;
            canvasGo.transform.SetParent(transform,false);
            var canvas=canvasGo.GetComponent<Canvas>();
            canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder=UiLayers.Settings;
            var scaler=canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920f,1080f);
            scaler.matchWidthOrHeight=.5f;

            var dim=UiKit.MakeImage(canvasGo.transform,"Overlay",null,false);
            dimImage=dim;
            dim.color=new Color(.12f,.10f,.27f,SettingsAppearance.Backdrop);
            dim.raycastTarget=true;
            Stretch(dim.rectTransform);
            overlay=dim.gameObject.AddComponent<CanvasGroup>();
            var window=UiKit.MakePanel(dim.transform,new Vector2(WinW,WinH),false,2f,"CloudSettings");
            panel=window.rectTransform;
            windowImage=window;
            Anch(panel,Vector2.one*.5f,Vector2.one*.5f,Vector2.zero,new Vector2(WinW,WinH));
            var title=UiKit.MakeImage(panel,"TitlePlaque",UiKit.Plaque,true,1.5f);
            TL(title.rectTransform,615f,-34f,530f,105f);
            Label(title.transform,"Настройки",49f,25f,3f,480f,91f);
            Label(panel,"TimberWings",28f,42f,16f,270f,50f,true);
            fpsLabel=Label(panel,"",24f,1515f,11f,130f,44f);
            MakeButton(panel,"×",1671f,6f,68f,62f,Resume);
            BuildTabs();
            BuildPreview();
            BuildFooter();
            BuildConfirm(dim.transform);
            UpdateAppearance();
        }
        canvasGo.SetActive(false);
        built=true;
    }

    void BuildTabs()
    {
        string[] labels = { "Общие", "Графика", "Звук", "Управление", "Интерфейс", "Игра" };
        UiIcon[] icons = { UiIcon.Book, UiIcon.Tree, UiIcon.Bolt, UiIcon.Target, UiIcon.Book, UiIcon.Axe };
        for (int i = 0; i < labels.Length; i++)
        {
            int index = i;
            float x = 42f;
            float y = 166f + i * 96f;
            var tab = MakeButton(panel, labels[i], x, y, 292f, 78f, () => SelectTab(index));
            var tabIcon = UiKit.MakeIcon(tab.transform, icons[i], 42f);
            TL(tabIcon.rectTransform, 14f, 17f, 42f, 42f);
            var tabLabel = tab.GetComponentInChildren<TMP_Text>();
            tabLabel.rectTransform.offsetMin = new Vector2(64f, tabLabel.rectTransform.offsetMin.y);
            tabs[i] = tab;

            var page = UiKit.MakePanel(panel, new Vector2(800f, 610f), false, 1f, "Page_" + labels[i]);
            TL(page.rectTransform, 354f, 157f, 800f, 610f);
            pages[i] = page.gameObject;
        }

        var c = pages[0].transform;
        Choice(c, 0, "Язык", new[] { "Русский", "English" },
            () => GameLanguage.Index, v => GameLanguage.Index = v, 0);
        Choice(c, 1, "Оформление настроек", new[] { "Классическое", "Лесное" },
            () => SettingsAppearance.Theme, v => SettingsAppearance.Theme = v, 0);
        ToggleRow(c, 2, "Сохранение по таймеру", () => SettingsAppearance.PeriodicAutosave,
            v => SettingsAppearance.PeriodicAutosave = v, true);
        ToggleRow(c, 3, "Автопереход реплик", () => DialoguePreferences.AutoAdvance,
            v => DialoguePreferences.AutoAdvance = v, true);
        ToggleRow(c, 4, "Редкие реплики героя", () => DialoguePreferences.PlayerComments,
            v => DialoguePreferences.PlayerComments = v, true);
        Info(c, 5, "Язык переводит настройки и главное меню.");
        Info(c, 6, "Прогресс сохраняется и при важных событиях.");

        BuildGraphics(pages[1].transform);

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
        Choice(c, 0, "Режим камеры", new[] { "Свободная", "Изометрия" },
            () => OrbitCamera.Enabled ? 0 : 1, v => OrbitCamera.Enabled = v == 0, 0);
        ToggleRow(c, 1, "Поворот мышью без ПКМ", () => OrbitCamera.MouseLook,
            v => OrbitCamera.MouseLook = v, true);
        Slider(c, 2, "Чувствительность мыши",
            () => Mathf.InverseLerp(0.25f, 2f, PerformanceBoot.MouseMultiplier),
            v => PerformanceBoot.MouseMultiplier = Mathf.Lerp(0.25f, 2f, v), 3f / 7f,
            v => Mathf.RoundToInt(Mathf.Lerp(25f, 200f, v)) + " %");
        Info(c, 3, "WASD — движение   ·   Esc — пауза");
        Info(c, 4, "E / ЛКМ — взаимодействие и рубка");
        Info(c, 5, "Колесо — зум   ·   Alt — свободный курсор");
        if (!Application.isMobilePlatform)
            ToggleRow(c, 6, "Покачивание камеры от первого лица", () => OrbitCamera.FirstPersonHeadBob,
                v => OrbitCamera.FirstPersonHeadBob = v, true);
        if (MobileInput.Enabled)
            ToggleRow(c, Application.isMobilePlatform ? 6 : 7, "Экранные кнопки (после перезапуска)",
                () => MobileInput.OnScreenControls,
                v => MobileInput.OnScreenControls = v, Application.isMobilePlatform);

        c = pages[4].transform;
        ToggleRow(c, 0, "Подсказки к цели", () => VibeSettings.GuideEnabled,
            v => VibeSettings.GuideEnabled = v, true);
        ToggleRow(c, 1, "FPS в настройках", () => PerformanceBoot.ShowSettingsFps,
            v => PerformanceBoot.ShowSettingsFps = v, true);
        ToggleRow(c, 2, "Меньше тряски и эффектов", () => VibeSettings.ReduceEffects,
            v => VibeSettings.ReduceEffects = v, false);
        ToggleRow(c, 3, "Анимация открытия окна", () => SettingsAppearance.AnimateOpen,
            v => SettingsAppearance.AnimateOpen = v, true);
        Slider(c, 4, "Размер окна настроек", () => Mathf.InverseLerp(.85f, 1.05f, SettingsAppearance.MenuScale),
            v => SettingsAppearance.MenuScale = Mathf.Lerp(.85f, 1.05f, v), .75f,
            v => Mathf.RoundToInt(Mathf.Lerp(85f, 105f, v)) + " %");
        Slider(c, 5, "Затемнение за меню", () => Mathf.InverseLerp(.30f, .80f, SettingsAppearance.Backdrop),
            v => SettingsAppearance.Backdrop = Mathf.Lerp(.30f, .80f, v), .44f,
            v => Mathf.RoundToInt(Mathf.Lerp(30f, 80f, v)) + " %");
        Info(c, 6, "Масштаб влияет только на окно настроек.");

        c = pages[5].transform;
        Choice(c, 0, "Скорость суток", new[] { "Обычная", "Медленная", "Фиксированный день" },
            () => (int)GameSettings.DaySpeed, v => GameSettings.DaySpeed = (DaySpeedMode)v, 0);
        ActionRow(c, 1, "Сохранение прогресса", "Сохранить", () => SaveSystem.SaveNow());
        continueBtn = ActionRow(c, 2, "Загрузить сохранение", "Продолжить", ContinueGame);
        ActionRow(c, 3, "Сброс прогресса", "Новая игра", ShowNewGameConfirm);
        menuBtn = ActionRow(c, 4, "Вернуться с сохранением", "В главное меню", AskMainMenu);
        if (!Application.isMobilePlatform && Application.platform != RuntimePlatform.WebGLPlayer)
            ActionRow(c, 5, "Закрыть игру", "Выйти", () =>
                ShowConfirm("Выход", "Сохранить прогресс и выйти из игры?", "Выйти", () =>
                { if (SaveSystem.SaveNow()) QuitGame(); }));
        mainMenuNote = Label(c, "Управление сохранением доступно из паузы.",
            23f, 29f, RowY(1), 740f, RowH, true);
    }

    void BuildGraphics(Transform c)
    {
        Choice(c, 0, "Качество света", new[] { "Низкое", "Среднее", "Высокое" },
            () => (int)GameSettings.LightQuality,
            v => GameSettings.LightQuality = (LightQualityLevel)v, 1);
        Choice(c, 1, "Дальность прорисовки", new[] { "Ближняя", "Средняя", "Дальняя" },
            () => (int)VibeSettings.DrawDistance, v => VibeSettings.DrawDistance = (DrawDistanceLevel)v, 1);
        ToggleRow(c, 2, "Тени", () => PerformanceBoot.Shadows, v => PerformanceBoot.Shadows = v, true);
        ToggleRow(c, 3, "Дымка вдали", () => PerformanceBoot.DistanceFog,
            v => PerformanceBoot.DistanceFog = v, true);
        Choice(c, 4, "Лимит FPS", new[] { "30", "60", "90", "120" },
            () => PerformanceBoot.FpsIndex, v => PerformanceBoot.FpsIndex = v, 1);
        if (PerformanceBoot.SupportsRenderScale)
            Slider(c, 5, "Масштаб рендера", () => Mathf.InverseLerp(.5f, 1f, PerformanceBoot.RenderScale),
                v => PerformanceBoot.RenderScale = Mathf.Lerp(.5f, 1f, v), 1f,
                v => Mathf.RoundToInt(Mathf.Lerp(50f, 100f, v)) + " %");
        if (!Application.isMobilePlatform && Application.platform != RuntimePlatform.WebGLPlayer)
            ToggleRow(c, 6, "Полноэкранный режим", () => PerformanceBoot.Fullscreen,
                v => PerformanceBoot.Fullscreen = v, PerformanceBoot.DefaultFullscreen);
    }

    void BuildPreview()
    {
        var frame = UiKit.MakePanel(panel, new Vector2(545f, 610f), false, 1f, "AdviceFrame");
        TL(frame.rectTransform, 1178f, 157f, 545f, 610f);
        var photoFrame = UiKit.MakePanel(frame.transform, new Vector2(516f, 214f), false, 0.8f, "ArtworkBorder");
        TL(photoFrame.rectTransform, 14f, 12f, 516f, 214f);
        var tex = Resources.Load<Texture2D>("TimberWings/SettingsPreview");
        if (tex != null)
        {
            var imageGo = new GameObject("ForestPreview", typeof(RectTransform), typeof(RawImage));
            imageGo.transform.SetParent(frame.transform, false);
            var raw = imageGo.GetComponent<RawImage>();
            raw.texture = tex;
            raw.raycastTarget = false;
            TL(raw.rectTransform, 22f, 19f, 500f, 198f);
        }
        previewTitle = Label(frame.transform, "", 31f, 29f, 239f, 480f, 46f, true);
        previewDescription = Label(frame.transform, "", 22f, 29f, 291f, 480f, 124f, true);
        var tipPlaque = UiKit.MakeImage(frame.transform, "TipHeader", UiKit.Plaque, true, .65f);
        TL(tipPlaque.rectTransform, 21f, 425f, 502f, 54f);
        Label(tipPlaque.transform, "Быстрые советы", 27f, 20f, 2f, 457f, 50f, true);
        previewTip1 = Label(frame.transform, "", 22f, 30f, 489f, 480f, 47f, true);
        previewTip2 = Label(frame.transform, "", 22f, 30f, 544f, 480f, 48f, true);
    }

    void UpdatePreview(int index)
    {
        if (previewTitle == null) return;
        string[] titleRu = { "Общие настройки", "Графика и качество", "Звуки TimberWings",
            "Камера и управление", "Удобство интерфейса", "Игровой прогресс" };
        string[] titleEn = { "General settings", "Graphics and quality", "TimberWings audio",
            "Camera and controls", "Interface comfort", "Game progress" };
        string[] descRu = {
            "Основные параметры игры, язык и\nнастройки поведения персонажей.",
            "Подберите баланс красоты и\nплавности под свой компьютер.",
            "Отдельная громкость для музыки,\nприроды, эффектов и речи.",
            "Выберите удобную камеру и\nчувствительность мыши.",
            "Настройте подсказки, масштаб окна\nи спокойные анимации.",
            "Управляйте скоростью суток,\nсохранением и новой игрой."
        };
        string[] descEn = {
            "Choose language, timed saves and\ncharacter dialogue behaviour.",
            "Balance visual quality and\nsmooth performance on your PC.",
            "Separate levels for music,\nambience, effects and voices.",
            "Find a comfortable camera mode\nand mouse sensitivity.",
            "Adjust hints, window size and\ncalmer animations.",
            "Manage day speed, progress,\nsaves and new games."
        };
        string[] tip1Ru = { "• Изменения сохраняются после «Применить».", "• Начните со среднего качества света.",
            "• Проверьте голоса кнопкой «Прослушать».", "• Камеру можно поменять без перезапуска.",
            "• Уменьшение эффектов помогает от тряски.", "• Перед выходом сохраните игру." };
        string[] tip1En = { "• Press Apply to keep changes.", "• Try medium lighting quality first.",
            "• Use Preview to test the current voice.", "• Camera mode changes without restart.",
            "• Reduce effects to lessen camera shake.", "• Save your game before quitting." };
        string[] tip2Ru = { "• Переключение языка не переводит сюжет.", "• Лимит FPS влияет на нагрузку.",
            "• Музыка и звуки регулируются отдельно.", "• F5 — первое лицо, R — достать топор.",
            "• Размер меняет только это окно.", "• Удаление прогресса требует подтверждения." };
        string[] tip2En = { "• Story dialogue is not translated yet.", "• Frame limit helps manage performance.",
            "• Music and ambience have separate controls.", "• F5 — first person, R — ready axe.",
            "• Size setting affects this window only.", "• Resetting progress requires confirmation." };
        int i = Mathf.Clamp(index, 0, 5);
        bool en = GameLanguage.English;
        previewTitle.text = en ? titleEn[i] : titleRu[i];
        previewDescription.text = en ? descEn[i] : descRu[i];
        previewTip1.text = en ? tip1En[i] : tip1Ru[i];
        previewTip2.text = en ? tip2En[i] : tip2Ru[i];
    }

    void BuildFooter()
    {
        MakeButton(panel,"По умолчанию",305f,784f,345f,68f,ResetDraft);
        MakeButton(panel,"Применить",708f,784f,345f,68f,ApplyDraft,UiBtn.Yellow);
        MakeButton(panel,"Назад",1111f,784f,345f,68f,Resume);
        footerStatus=Label(panel,"",19f,375f,853f,1010f,30f);
    }
    void SelectTab(int index)
    {
        selectedTab=Mathf.Clamp(index,0,pages.Length-1);
        for (int i=0;i<pages.Length;i++)
        {
            pages[i].SetActive(i==selectedTab);
            Style(tabs[i],i==selectedTab?UiBtn.Yellow:UiBtn.Gray);
        }
        UpdatePreview(selectedTab);
    }

    static float RowY(int index) { return 14f+index*RowH; }
    void Row(Transform c,int index,string title,float labelWidth=355f)
    {
        float y=RowY(index);
        var strip=UiKit.MakeImage(c,"SettingRow",UiKit.Slot,true,.7f);
        strip.color=new Color(.93f,.93f,1f,.68f);
        TL(strip.rectTransform,13f,y,774f,RowH-5f);
        Label(c,title,25f,29f,y,labelWidth,RowH-6f,true);
    }
    void Info(Transform c,int index,string line) { Row(c,index,line,740f); }
    void ToggleRow(Transform c,int index,string title,Func<bool> read,Action<bool> write,bool fallback)
    {
        Row(c,index,title);
        var setting=Draft(read,write,fallback);
        var b=MakeButton(c,"",590f,RowY(index)+6f,172f,43f,()=>Change(setting,!setting.Value));
        var lab=b.GetComponentInChildren<TMP_Text>();
        refreshers.Add(()=> { lab.text=GameLanguage.T(setting.Value?"Вкл":"Выкл"); Style(b,setting.Value?UiBtn.Green:UiBtn.Gray); });
    }
    void Choice(Transform c,int index,string title,string[] names,Func<int> read,Action<int> write,int fallback)
    {
        Row(c,index,title);
        var setting=Draft(read,write,fallback);
        float y=RowY(index)+6f;
        MakeButton(c,"‹",411f,y,49f,43f,()=>Change(setting,(setting.Value+names.Length-1)%names.Length));
        var lab=Label(c,"",23f,467f,y,239f,43f);
        MakeButton(c,"›",713f,y,49f,43f,()=>Change(setting,(setting.Value+1)%names.Length));
        refreshers.Add(()=>lab.text=GameLanguage.T(names[Mathf.Clamp(setting.Value,0,names.Length-1)]));
    }
    void Slider(Transform c,int index,string title,Func<float> read,Action<float> write,float fallback,Func<float,string> format=null)
    {
        Row(c,index,title);
        var setting=Draft(read,write,fallback);
        float y=RowY(index);
        var frame=UiKit.MakeBar(c,UiBar.Blue,new Vector2(250f,24f),out var fill);
        TLC(frame.rectTransform,544f,y+26f,250f,24f);
        var knob=UiKit.MakeImage(frame.transform,"Knob",UiKit.Medallion,false);
        knob.rectTransform.sizeDelta=new Vector2(38f,38f);
        var lab=Label(c,"",22f,680f,y,89f,52f);
        var drag=frame.gameObject.AddComponent<SettingsSliderDrag>();
        drag.Init(frame.rectTransform,fill,knob.rectTransform,lab,v=>Change(setting,v,false),format);
        hitSliders.Add(drag);
        refreshers.Add(()=>drag.SetValue(setting.Value));
    }
    Button ActionRow(Transform c,int index,string title,string label,Action action)
    {
        var group=NewRT(title,c);
        TL(group,0f,RowY(index)-RowY(0),800f,RowH);
        Row(group,0,title);
        var b=MakeButton(group,label,477f,RowY(0)+6f,285f,43f,action);
        if (c.gameObject==pages[5]) gameRows.Add(group.gameObject);
        return b;
    }
    static TMP_Text Label(Transform parent,string text,float size,float x,float y,float w,float h,bool left=false)
    {
        return Lbl(parent,GameLanguage.T(text),size,UiKit.CandyInk,left?TextAlignmentOptions.Left:TextAlignmentOptions.Center,true,x,y,w,h);
    }
    Button MakeButton(Transform c,string text,float x,float y,float w,float h,Action action,UiBtn style=UiBtn.Gray)
    {
        var button=UiKit.MakeButton(c,GameLanguage.T(text),style,new Vector2(w,h),h>=60f?30f:23f);
        TL((RectTransform)button.transform,x,y,w,h);
        button.navigation=new Navigation { mode=Navigation.Mode.None };
        AutoSize(button.GetComponentInChildren<TMP_Text>(),15f,h>=60f?30f:23f);
        TintButton(button,style);
        Bind(button,action);
        return button;
    }
    static void TintButton(Button button,UiBtn style)
    {
        button.image.color=style==UiBtn.Gray && button.interactable ? new Color(.80f,.90f,1f):Color.white;
    }
    static void Style(Button b,UiBtn style)
    {
        using (UiKit.UseSkin(UiSkin.Candy))
        {
            var normal=UiKit.BtnSprite(style);
            b.image.sprite=normal;
            TintButton(b,style);
            b.spriteState=new SpriteState
            {
                highlightedSprite=UiKit.BtnSprite(style,UiBtnState.Hover),
                pressedSprite=UiKit.BtnSprite(style,UiBtnState.Pressed),
                selectedSprite=normal,
                disabledSprite=UiKit.BtnSprite(UiBtn.Gray,UiBtnState.Disabled)
            };
        }
    }
    sealed class Setting<T> { public T Value; }
    Setting<T> Draft<T>(Func<T> read,Action<T> write,T fallback)
    {
        var st=new Setting<T>();
        loadDraft.Add(()=>st.Value=read());
        resetDraft.Add(()=>st.Value=fallback);
        applyDraft.Add(()=>{ if(!EqualityComparer<T>.Default.Equals(st.Value,read())) write(st.Value); });
        return st;
    }
    void Change<T>(Setting<T> st,T value,bool refresh=true)
    {
        if (EqualityComparer<T>.Default.Equals(st.Value,value)) return;
        st.Value=value;
        pending=true;
        footerStatus.text=GameLanguage.T("Есть изменения. Нажми «Применить», чтобы их сохранить.");
        if (refresh) RefreshAll();
    }
    void ResetDraft()
    {
        foreach(var reset in resetDraft) reset();
        RefreshAll();
        pending=true;
        footerStatus.text=GameLanguage.T("Значения по умолчанию готовы. Нажми «Применить».");
    }
    void ApplyDraft()
    {
        int oldLang=GameLanguage.Index;
        foreach(var apply in applyDraft) apply();
        PlayerPrefs.Save(); pending=false;
        if(oldLang!=GameLanguage.Index)
        {
            GameObject previous=canvasGo;
            previous.SetActive(false);
            hitBtns.Clear(); hitSliders.Clear(); pressedBtn=null; draggedSlider=null;
            refreshers.Clear(); loadDraft.Clear(); resetDraft.Clear(); applyDraft.Clear(); gameRows.Clear();
            built=false;
            Build();
            if(previous!=null) Destroy(previous);
            canvasGo.SetActive(true);
            foreach(var load in loadDraft) load();
            RefreshAll(); ApplyMenuMode(); SelectTab(selectedTab);
        }
        else RefreshAll();
        UpdateAppearance(); FitToScreen();
        footerStatus.text=GameLanguage.T("Настройки применены");
    }
    void UpdateAppearance()
    {
        if (windowImage!=null) windowImage.color=SettingsAppearance.Theme==0?Color.white:new Color(.92f,1f,.94f,1f);
        if (dimImage!=null) dimImage.color=new Color(.12f,.10f,.27f,SettingsAppearance.Backdrop);
    }

    void ShowConfirm(string title,string body,string okText,Action action)
    {
        if(confirmGo==null) return;
        if(confirmTitle!=null) confirmTitle.text=GameLanguage.T(title);
        if(confirmBody!=null) confirmBody.text=GameLanguage.T(body);
        if(confirmOkLabel!=null) confirmOkLabel.text=GameLanguage.T(okText);
        confirmAction=action;
        confirmOpen=true; pressedBtn=null; draggedSlider=null;
        confirmGo.SetActive(true);
    }
    void HideConfirm()
    {
        confirmOpen=false; confirmAction=null; pressedBtn=null;
        if(confirmGo!=null) confirmGo.SetActive(false);
    }
    void ConfirmOk()
    {
        var act=confirmAction;
        HideConfirm();
        if(act!=null) act();
    }
    void BuildConfirm(Transform parent)
    {
        var dim=UiKit.MakeImage(parent,"Confirm",null,false);
        dim.color=new Color(.02f,.04f,.08f,.6f);
        dim.raycastTarget=true;
        Stretch(dim.rectTransform);
        confirmGo=dim.gameObject;
        var center=new Vector2(.5f,.5f);
        var win=UiKit.MakePanel(dim.transform,new Vector2(760f,360f),true,1f,"ConfirmWindow");
        win.raycastTarget=true;
        Anch(win.rectTransform,center,center,Vector2.zero,new Vector2(760f,360f));
        confirmTitle=UiKit.MakeLabel(win.transform,GameLanguage.T("НОВАЯ ИГРА"),34f,UiKit.CandyInk,TextAlignmentOptions.Center,true);
        Anch(confirmTitle.rectTransform,new Vector2(.5f,1f),new Vector2(.5f,1f),new Vector2(0f,-4f),new Vector2(420f,42f));
        confirmBody=UiKit.MakeLabel(win.transform,GameLanguage.T("Весь прогресс будет удалён.\nЭто нельзя отменить."),32f,UiKit.CandyInk,TextAlignmentOptions.Center,true);
        Anch(confirmBody.rectTransform,center,center,new Vector2(0f,24f),new Vector2(680f,130f));
        var cancel=UiKit.MakeButton(win.transform,GameLanguage.T("Отмена"),UiBtn.Green,new Vector2(270f,72f),30f,1f);
        cancel.navigation=new Navigation{mode=Navigation.Mode.None};
        Anch(cancel.GetComponent<RectTransform>(),new Vector2(.5f,0f),new Vector2(.5f,0f),new Vector2(-150f,40f),new Vector2(270f,72f));
        var ct=cancel.GetComponentInChildren<TMP_Text>();
        ct.color=UiKit.CandyInk; ct.enableAutoSizing=true; ct.fontSizeMin=18f; ct.fontSizeMax=30f;
        BindModal(cancel,HideConfirm);
        var ok=UiKit.MakeButton(win.transform,GameLanguage.T("Удалить"),UiBtn.Gray,new Vector2(270f,72f),30f,1f);
        ok.navigation=new Navigation {mode=Navigation.Mode.None};
        Anch(ok.GetComponent<RectTransform>(),new Vector2(.5f,0f),new Vector2(.5f,0f),new Vector2(150f,40f),new Vector2(270f,72f));
        var ot=ok.GetComponentInChildren<TMP_Text>();
        ot.color=UiKit.CandyInk; ot.enableAutoSizing=true; ot.fontSizeMin=18f; ot.fontSizeMax=30f;
        confirmOkLabel=ot;
        BindModal(ok,ConfirmOk);
        confirmGo.SetActive(false);
    }
}

public class SettingsSliderDrag : MonoBehaviour
{
    RectTransform bar,knob;
    Image fill;
    TMP_Text pct;
    Action<float> onChange;
    Func<float,string> format;
    const float Inset=8f;
    public void Init(RectTransform b,Image f,RectTransform k,TMP_Text t,Action<float> callback,Func<float,string> fmt)
    { bar=b;fill=f;knob=k;pct=t;onChange=callback;format=fmt; }
    public void SetValue(float v)
    {
        v=Mathf.Clamp01(v);
        if(fill!=null) fill.fillAmount=v;
        if(pct!=null) pct.text=format!=null?format(v):Mathf.RoundToInt(v*100f)+" %";
        if(knob!=null&&bar!=null)
        {
            float half=bar.rect.width*.5f-Inset;
            knob.anchoredPosition=new Vector2(Mathf.Lerp(-half,half,v),0f);
        }
    }
    public bool Contains(Vector2 pos)
    {
        if(bar==null||!gameObject.activeInHierarchy) return false;
        Vector2 lp;
        if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(bar,pos,null,out lp))return false;
        Rect r=bar.rect;
        r.xMin-=24f; r.xMax+=24f; r.yMin-=20f; r.yMax+=20f;
        return r.Contains(lp);
    }
    public void PickAt(Vector2 pos)
    {
        if(bar==null||!gameObject.activeInHierarchy)return;
        Vector2 lp;
        if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(bar,pos,null,out lp))return;
        float half=bar.rect.width*.5f-Inset;
        float v=Mathf.Clamp01(Mathf.InverseLerp(-half,half,lp.x));
        SetValue(v);
        if(onChange!=null)onChange(v);
    }
}
