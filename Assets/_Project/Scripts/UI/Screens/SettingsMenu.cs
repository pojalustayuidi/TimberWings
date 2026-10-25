// Assets/_Project/Scripts/UI/Screens/SettingsMenu.cs (НЕ в Editor)
// Меню настроек и паузы на Escape. Создаётся само после загрузки сцены, ничего вешать в сцену не нужно.
// Стиль окон игры: деревянная планка, карточка UiKit, латунные кнопки, ползунки из полосок UiKit.
//
// Что умеет:
//   Esc                 открыть / закрыть (игра на паузе, Time.timeScale = 0). Если открыто другое окно или большая карта,
//                       Esc закрывает их, как раньше; меню открывается следующим нажатием.
//   Звук в игре         ВКЛ / ВЫКЛ: выключает вообще весь звук (GameAudio.Muted)
//   Общая громкость     множитель на всё остальное (GameAudio.Master)
//   Музыка              громкость плейлиста (MusicManager)
//   Звуки               рубка, шаги, монеты, стройка
//   Голоса              озвучка реплик и бормотание
//   Природа             ветер, птицы
//   Бормотание          ВКЛ / ВЫКЛ: короткие «бла-бла» на репликах, у которых нет озвучки
//   Все громкости хранятся в GameAudio. Двигаешь любой ползунок при выключенном звуке: звук включается сам.
//   Подсказки к цели    VibeSettings.GuideEnabled
//   Меньше эффектов     VibeSettings.ReduceEffects
//   На весь экран       Screen.fullScreen (на телефонах скрыто)
//   Экранные кнопки     строка показывается только если MobileInput.Enabled = true (сейчас выключены совсем)
//   Качество света      Низкое / Среднее / Высокое (GameSettings.LightQuality, хранится в файле сохранения)
//   Скорость суток      Обычная / Медленная / Фиксированный день (GameSettings.DaySpeed)
//   Сохранить игру      SaveSystem.SaveNow()
//   Продолжить          активна только при SaveSystem.HasSave: загружает сохранение и закрывает меню (вернуться в игру без загрузки: Esc или крестик)
//   Новая игра          диалог «Весь прогресс будет удалён»; по подтверждению SaveSystem.NewGame() и перезапуск сцены
//   В главное меню      диалог подтверждения, SaveNow, возврат в сцену MainMenu через MenuFlow (недоступна во время диалога, сна, показа стройки)
//   Выйти               не показывается в браузере
//
// Пока меню открыто, MobileInput.ModalOpen = true: персонаж стоит, E и Пробел «сквозь» меню не работают.
// Открыть из кода (например, кнопкой-шестерёнкой на телефоне): SettingsMenu.Toggle().
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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

    // Окно настроек из главного меню: те же настройки, внизу одна кнопка «Назад». Esc тоже закрывает.
    public static void OpenFromMenu()
    {
        if (Instance == null || Instance.open) return;
        Instance.menuMode = true;
        Instance.Open();
    }
    bool menuMode;
    float continueX;
    Button quitBtn;

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

    // ============================================================ вид
    static readonly Color Soft = new Color(0.90f, 0.93f, 0.98f, 1f);
    static readonly Color Dim = new Color(0.55f, 0.62f, 0.72f, 1f);
    static readonly Color WoodText = new Color32(0x2A, 0x16, 0x08, 255);
    static readonly Color Sep = new Color(0.30f, 0.38f, 0.50f, 0.45f);

    const float WinW = 980f, BS = 1.5f;           // окно и масштаб его рамки
    const float RowH = 58f, CardPad = 12f;
    const float InnerW = WinW - 2f * 26f * BS;    // ширина карточки

    // ============================================================ состояние
    bool open, built;
    float prevScale = 1f;
    float anim;                                   // 0..1, плавное открытие
    GameObject confirmGo;                         // окно подтверждения (одно на «Новая игра» и «В главное меню»)
    TMP_Text confirmTitle, confirmBody, confirmOkLabel;
    Action confirmAction;                         // что делает кнопка подтверждения
    Button menuBtn;
    TMP_Text menuLabel;
    bool confirmOpen;
    RectTransform panel;
    CanvasGroup overlay;
    GameObject canvasGo;
    TMP_Text restartLabel, continueLabel;
    Button restartBtn, continueBtn;
    readonly List<Action> refreshers = new List<Action>();
    RectTransform hintRt;
    float winHeight, fit = 1f;                    // fit < 1: окно не влезает в экран (телефон), сжимаем
    // Нажатия обрабатываем сами, по координатам курсора, а не через EventSystem: меню работает, даже если EventSystem в сцене сломан или занят
    class HitBtn { public RectTransform rt; public Action act; public bool modal; }   // modal: кнопка окна подтверждения
    readonly List<HitBtn> hitBtns = new List<HitBtn>();
    readonly List<SettingsSliderDrag> hitSliders = new List<SettingsSliderDrag>();
    HitBtn pressedBtn;
    SettingsSliderDrag draggedSlider;
    readonly List<GraphicRaycaster> pausedRaycasters = new List<GraphicRaycaster>();   // чужие рейкастеры, выключенные на время меню

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
        if (open) { ResumeOtherRaycasters(); RestoreTime(); }
        if (Instance == this) Instance = null;
    }

    void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        // сцену перезагрузили (например, «Начать заново»): меню закрыто, музыка по настройке
        open = false;
        menuMode = false;
        ResumeOtherRaycasters();    // уничтоженные вместе со сценой пропускаются, уцелевшие включаем обратно
        if (canvasGo != null) canvasGo.SetActive(false);
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
        for (int i = 0; i < refreshers.Count; i++) refreshers[i]();
        ApplyMenuMode();
        pressedBtn = null; draggedSlider = null;
        canvasGo.SetActive(true);   // чужие GraphicRaycaster больше не трогаем: клики меню обрабатывает PollPointer
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
        ResumeOtherRaycasters();
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
        if (menuMode) { Close(); return; }      // из главного меню эта кнопка «Назад»
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
        ResumeOtherRaycasters();
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
        ResumeOtherRaycasters();
        Time.timeScale = 1f;                   // пауза не должна «залипнуть» в меню
        MenuFlow.ReturnToMenu();
    }

    void RefreshMenuBtn()
    {
        if (menuBtn == null) return;
        bool ok = CanLeaveToMenu();
        var style = UiBtn.Gray;
        menuBtn.GetComponent<Image>().sprite = UiKit.BtnSprite(style, ok ? UiBtnState.Normal : UiBtnState.Disabled);
        var ss = new SpriteState();
        ss.highlightedSprite = UiKit.BtnSprite(style, UiBtnState.Hover);
        ss.pressedSprite = UiKit.BtnSprite(style, UiBtnState.Pressed);
        ss.selectedSprite = UiKit.BtnSprite(style, UiBtnState.Normal);
        ss.disabledSprite = UiKit.BtnSprite(UiBtn.Gray, UiBtnState.Disabled);
        menuBtn.spriteState = ss;
        menuBtn.interactable = ok;
        if (menuLabel != null) menuLabel.color = ok ? Color.white : Dim;
    }

    // в режиме «из меню» остаётся одна кнопка «Назад» по центру
    void ApplyMenuMode()
    {
        if (restartBtn != null) restartBtn.gameObject.SetActive(!menuMode);
        if (menuBtn != null) menuBtn.gameObject.SetActive(!menuMode);
        if (quitBtn != null) quitBtn.gameObject.SetActive(!menuMode);
        if (continueBtn == null) return;
        var rt = (RectTransform)continueBtn.transform;
        rt.anchoredPosition = new Vector2(menuMode ? (InnerW - rt.sizeDelta.x) * 0.5f : continueX, rt.anchoredPosition.y);
        if (continueLabel != null) continueLabel.text = menuMode ? "Назад" : "Продолжить";
    }

    // кнопка «Продолжить» серая, пока нечего загружать
    void RefreshContinue()
    {
        if (continueBtn == null) return;
        bool has = menuMode || SaveSystem.HasSave;
        var style = has ? UiBtn.Green : UiBtn.Gray;
        continueBtn.GetComponent<Image>().sprite = UiKit.BtnSprite(style, has ? UiBtnState.Normal : UiBtnState.Disabled);
        var ss = new SpriteState();
        ss.highlightedSprite = UiKit.BtnSprite(style, UiBtnState.Hover);
        ss.pressedSprite = UiKit.BtnSprite(style, UiBtnState.Pressed);
        ss.selectedSprite = UiKit.BtnSprite(style, UiBtnState.Normal);
        ss.disabledSprite = UiKit.BtnSprite(UiBtn.Gray, UiBtnState.Disabled);
        continueBtn.spriteState = ss;
        continueBtn.interactable = has;
        if (continueLabel != null) continueLabel.color = has ? UiKit.Navy : Dim;
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
        fit = Mathf.Min(1f, canvasH * 0.9f / (winHeight + 70f), canvasW * 0.96f / WinW);
        if (hintRt != null) hintRt.anchoredPosition = new Vector2(0f, -(winHeight * fit * 0.5f + 14f));
    }

    void RefreshAll()
    {
        for (int i = 0; i < refreshers.Count; i++) refreshers[i]();
    }

    // ============================================================ построение окна
    void EnsureEventSystem() { UiInputGuard.EnsureEventSystem(); }

    void ResumeOtherRaycasters()
    {
        for (int i = 0; i < pausedRaycasters.Count; i++)
            if (pausedRaycasters[i] != null) pausedRaycasters[i].enabled = true;
        pausedRaycasters.Clear();
    }

    void Bind(Button b, Action act)
    {
        hitBtns.Add(new HitBtn { rt = (RectTransform)b.transform, act = act });
    }

    void BindModal(Button b, Action act)
    {
        hitBtns.Add(new HitBtn { rt = (RectTransform)b.transform, act = act, modal = true });
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
                    if (hitBtns[i].modal == confirmOpen && hitBtns[i].rt.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(hitBtns[i].rt, pos, null)) { pressedBtn = hitBtns[i]; break; }
        }
        else if (Input.GetMouseButton(0) && draggedSlider != null)
        {
            draggedSlider.PickAt(pos);
        }

        if (Input.GetMouseButtonUp(0))
        {
            if (draggedSlider != null) { var d = draggedSlider; draggedSlider = null; d.Release(); }
            else if (pressedBtn != null)
            {
                var b = pressedBtn; pressedBtn = null;
                if (RectTransformUtility.RectangleContainsScreenPoint(b.rt, pos, null)) b.act();   // отпустил на той же кнопке: нажатие
            }
        }
    }

    void Build()
    {
        EnsureEventSystem();

        bool showFullscreen = !Application.isMobilePlatform;
        bool showQuit = Application.platform != RuntimePlatform.WebGLPlayer && !Application.isMobilePlatform;
        bool showOnScreen = MobileInput.Enabled;   // экранные кнопки выключены глобально: строки в настройках нет
        int n = 13 + (showFullscreen ? 1 : 0) + (showOnScreen ? 1 : 0);   // 9 прежних + 2 выбора (свет, сутки) + «Сохранить игру»

        float cardH = n * RowH + 2f * CardPad;
        const float headH = 36f * BS;             // деревянная планка сверху
        const float footH = 96f;
        float winH = headH + cardH + 16f + footH + 26f * BS;
        winHeight = winH;

        canvasGo = new GameObject("SettingsCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.layer = 5;
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = UiLayers.Settings;
        var sc = canvasGo.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        sc.matchWidthOrHeight = 0.5f;

        // затемнение, клики по нему никуда не проходят
        var ov = UiKit.MakeImage(canvasGo.transform, "Overlay", null, false);
        ov.color = new Color(0.02f, 0.04f, 0.08f, 0.72f);
        ov.raycastTarget = true;
        Stretch(ov.rectTransform);
        overlay = ov.gameObject.AddComponent<CanvasGroup>();

        // окно: деревянная панель, как у торговца
        var win = UiKit.MakePanel(ov.transform, new Vector2(WinW, winH), true, BS, "Window");
        win.raycastTarget = true;
        panel = win.rectTransform;
        Anch(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(WinW, winH));

        BuildHeader(winH);

        float pad = 26f * BS;
        var content = NewRT("Content", panel);
        content.anchorMin = Vector2.zero; content.anchorMax = Vector2.one;
        content.offsetMin = new Vector2(pad, pad);
        content.offsetMax = new Vector2(-pad, -headH);

        BuildRows(content, cardH, showFullscreen, showOnScreen);
        BuildFooter(content, cardH + 16f, showQuit);

        var hint = UiKit.MakeLabel(ov.transform, "Esc - вернуться в игру", 24f, Dim, TextAlignmentOptions.Center, false);
        Anch(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, -(winH * 0.5f + 14f)), new Vector2(800f, 36f));
        hintRt = hint.rectTransform;
        if (Application.isMobilePlatform) hint.gameObject.SetActive(false);   // на телефоне Esc нет

        BuildConfirm(ov.transform);   // последним: рисуется поверх окна

        canvasGo.SetActive(false);
        built = true;
    }

    void BuildHeader(float winH)
    {
        var icon = UiKit.MakeIcon(panel, UiIcon.Axe, 46f, "TitleIcon");
        Anch(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-124f, -28f), new Vector2(46f, 46f));

        var title = UiKit.MakeLabel(panel, "НАСТРОЙКИ", 36f, WoodText, TextAlignmentOptions.Center, true);
        Anch(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(14f, -8f), new Vector2(320f, 42f));

        var close = UiKit.MakeButton(panel, "", UiBtn.Amber, new Vector2(64f, 48f), 20f, 1f);
        Anch(close.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-34f, -5f), new Vector2(64f, 48f));
        close.navigation = new Navigation { mode = Navigation.Mode.None };
        Bind(close, Resume);
        var x = UiKit.MakeIcon(close.transform, UiIcon.Cross, 30f, "X");
        x.rectTransform.anchoredPosition = new Vector2(0f, 2f);
    }

    void BuildRows(Transform parent, float cardH, bool showFullscreen, bool showOnScreen)
    {
        var card = UiKit.MakePanel(parent, new Vector2(InnerW, cardH), false, 0.6f, "Rows");
        TL(card.rectTransform, 0f, 0f, InnerW, cardH);
        var c = card.transform;

        int row = 0;
        AddToggle(c, row++, "Звук в игре", () => !GameAudio.Muted, v => GameAudio.Muted = !v);
        AddSlider(c, row++, "Общая громкость", () => GameAudio.Master, v => GameAudio.Master = v, () => SfxManager.Play("ui", 1f, 1f));
        AddSlider(c, row++, "Музыка", () => GameAudio.Music, v => GameAudio.Music = v);
        AddSlider(c, row++, "Звуки", () => GameAudio.Sfx, v => GameAudio.Sfx = v, () => SfxManager.Play("coin", 0.8f, 1f));
        AddSlider(c, row++, "Голоса", () => GameAudio.Voice, v => GameAudio.Voice = v, () => SfxManager.PreviewVoice());
        AddSlider(c, row++, "Природа", () => GameAudio.Ambient, v => GameAudio.Ambient = v);
        AddToggle(c, row++, "Бормотание вместо озвучки", () => GameAudio.Babble, v => GameAudio.Babble = v);
        AddToggle(c, row++, "Подсказки к цели", () => VibeSettings.GuideEnabled, v => VibeSettings.GuideEnabled = v);
        AddToggle(c, row++, "Меньше эффектов", () => VibeSettings.ReduceEffects, v => VibeSettings.ReduceEffects = v);
        AddChoice(c, row++, "Камера", new[] { "Свободная", "Изометрия" },
            () => OrbitCamera.Enabled ? 0 : 1, v => OrbitCamera.Enabled = (v == 0));
        AddChoice(c, row++, "Качество света", new[] { "Низкое", "Среднее", "Высокое" },
            () => (int)GameSettings.LightQuality, v => GameSettings.LightQuality = (LightQualityLevel)v);
        AddChoice(c, row++, "Скорость суток", new[] { "Обычная", "Медленная", "Фиксированный день" },
            () => (int)GameSettings.DaySpeed, v => GameSettings.DaySpeed = (DaySpeedMode)v);
        if (showFullscreen)
            AddToggle(c, row++, "На весь экран", () => Screen.fullScreen, v => Screen.fullScreen = v);
        if (showOnScreen)
            AddToggle(c, row++, "Экранные кнопки (после перезапуска)", () => MobileInput.OnScreenControls, v => MobileInput.OnScreenControls = v);
        AddAction(c, row++, "Сохранить игру", "Сохранить", () => SaveSystem.SaveNow());   // «Сохранено» покажет SaveIndicator
    }

    float RowY(int i) { return CardPad + i * RowH; }

    void RowFrame(Transform c, int i, string label)
    {
        float y = RowY(i);
        var lbl = UiKit.MakeLabel(c, label, 28f, Soft, TextAlignmentOptions.Left, true);
        TL(lbl.rectTransform, 34f, y, 470f, RowH);
        lbl.enableAutoSizing = true; lbl.fontSizeMin = 16f; lbl.fontSizeMax = 28f;

        if (i > 0)
        {
            var line = UiKit.MakeImage(c, "Line" + i, null, false);
            line.color = Sep;
            TL(line.rectTransform, 24f, y, InnerW - 48f, 2f);
        }
    }

    // ---------- ползунок: полоска UiKit с заливкой и медальоном-ручкой
    void AddSlider(Transform c, int i, string label, Func<float> get, Action<float> set, Action onRelease = null)
    {
        RowFrame(c, i, label);
        float y = RowY(i);
        const float barW = 250f, barH = 24f;

        Image fill;
        var frame = UiKit.MakeBar(c, UiBar.Amber, new Vector2(barW, barH), out fill, 1f);
        frame.raycastTarget = true;
        TLC(frame.rectTransform, 520f + barW * 0.5f, y + RowH * 0.5f, barW, barH);

        var knob = UiKit.MakeImage(frame.transform, "Knob", UiKit.Medallion, false);
        knob.rectTransform.sizeDelta = new Vector2(46f, 46f);

        var pct = UiKit.MakeLabel(c, "100%", 26f, UiKit.BrassHi, TextAlignmentOptions.Right, true);
        TL(pct.rectTransform, InnerW - 30f - 96f, y, 96f, RowH);

        // двигаешь ползунок при выключенном звуке: звук включается, иначе непонятно, почему ничего не слышно
        Action<float> setAndUnmute = v =>
        {
            set(v);
            if (v > 0.001f && GameAudio.Muted) { GameAudio.Muted = false; RefreshAll(); }
        };
        Action released = () =>
        {
            PlayerPrefs.Save();
            if (onRelease != null) onRelease();       // короткий звук-образец новой громкости
        };

        var drag = frame.gameObject.AddComponent<SettingsSliderDrag>();
        drag.Init(frame.rectTransform, fill, knob.rectTransform, pct, setAndUnmute, released);

        hitSliders.Add(drag);
        refreshers.Add(() => drag.SetValue(get()));
    }

    // ---------- переключатель ВКЛ / ВЫКЛ
    void AddToggle(Transform c, int i, string label, Func<bool> get, Action<bool> set)
    {
        RowFrame(c, i, label);
        float y = RowY(i);
        var btn = UiKit.MakeButton(c, "ВЫКЛ", UiBtn.Gray, new Vector2(170f, 48f), 26f, 1f);
        Anch(btn.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 0.5f),
             new Vector2(InnerW - 30f, -(y + RowH * 0.5f)), new Vector2(170f, 48f));
        btn.navigation = new Navigation { mode = Navigation.Mode.None };

        bool state = false;
        Action refresh = () => { state = get(); SetToggleLook(btn, state); };
        Bind(btn, () => { state = !state; set(state); SetToggleLook(btn, state); });
        refreshers.Add(refresh);
    }

    static void SetToggleLook(Button btn, bool on)
    {
        var style = on ? UiBtn.Green : UiBtn.Gray;
        var img = btn.GetComponent<Image>();
        img.sprite = UiKit.BtnSprite(style, UiBtnState.Normal);
        var ss = new SpriteState();
        ss.highlightedSprite = UiKit.BtnSprite(style, UiBtnState.Hover);
        ss.pressedSprite = UiKit.BtnSprite(style, UiBtnState.Pressed);
        ss.selectedSprite = UiKit.BtnSprite(style, UiBtnState.Normal);
        ss.disabledSprite = UiKit.BtnSprite(UiBtn.Gray, UiBtnState.Disabled);
        btn.spriteState = ss;
        var t = btn.GetComponentInChildren<TMP_Text>();
        if (t != null)
        {
            t.text = on ? "ВКЛ" : "ВЫКЛ";
            t.color = on ? UiKit.Navy : Color.white;
        }
    }

    // ---------- выбор из нескольких значений: нажатие переключает по кругу
    void AddChoice(Transform c, int i, string label, string[] names, Func<int> get, Action<int> set)
    {
        RowFrame(c, i, label);
        float y = RowY(i);
        const float bw = 290f;
        var btn = UiKit.MakeButton(c, names[0], UiBtn.Gray, new Vector2(bw, 48f), 26f, 1f);
        Anch(btn.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 0.5f),
             new Vector2(InnerW - 30f, -(y + RowH * 0.5f)), new Vector2(bw, 48f));
        btn.navigation = new Navigation { mode = Navigation.Mode.None };
        var t = btn.GetComponentInChildren<TMP_Text>();
        t.color = Color.white; t.enableAutoSizing = true; t.fontSizeMin = 16f; t.fontSizeMax = 26f;

        int state = 0;
        Action refresh = () => { state = Mathf.Clamp(get(), 0, names.Length - 1); t.text = names[state]; };
        Bind(btn, () => { state = (state + 1) % names.Length; set(state); t.text = names[state]; });
        refreshers.Add(refresh);
    }

    // ---------- строка с кнопкой-действием
    void AddAction(Transform c, int i, string label, string btnText, Action act)
    {
        RowFrame(c, i, label);
        float y = RowY(i);
        const float bw = 230f;
        var btn = UiKit.MakeButton(c, btnText, UiBtn.Green, new Vector2(bw, 48f), 26f, 1f);
        Anch(btn.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 0.5f),
             new Vector2(InnerW - 30f, -(y + RowH * 0.5f)), new Vector2(bw, 48f));
        btn.navigation = new Navigation { mode = Navigation.Mode.None };
        var t = btn.GetComponentInChildren<TMP_Text>();
        t.color = UiKit.Navy; t.enableAutoSizing = true; t.fontSizeMin = 16f; t.fontSizeMax = 26f;
        Bind(btn, act);
    }

    // ---------- диалог «Новая игра»
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

        var title = UiKit.MakeLabel(win.transform, "НОВАЯ ИГРА", 34f, WoodText, TextAlignmentOptions.Center, true);
        confirmTitle = title;
        Anch(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(420f, 42f));

        var body = UiKit.MakeLabel(win.transform, "Весь прогресс будет удалён.\nЭто нельзя отменить.", 32f, Soft, TextAlignmentOptions.Center, true);
        Anch(body.rectTransform, center, center, new Vector2(0f, 24f), new Vector2(680f, 130f));
        confirmBody = body;

        var cancel = UiKit.MakeButton(win.transform, "Отмена", UiBtn.Green, new Vector2(270f, 72f), 30f, 1f);
        cancel.navigation = new Navigation { mode = Navigation.Mode.None };
        Anch(cancel.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-150f, 40f), new Vector2(270f, 72f));
        var ct = cancel.GetComponentInChildren<TMP_Text>();
        ct.color = UiKit.Navy; ct.enableAutoSizing = true; ct.fontSizeMin = 18f; ct.fontSizeMax = 30f;
        BindModal(cancel, HideConfirm);

        var ok = UiKit.MakeButton(win.transform, "Удалить", UiBtn.Gray, new Vector2(270f, 72f), 30f, 1f);
        ok.navigation = new Navigation { mode = Navigation.Mode.None };
        Anch(ok.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(150f, 40f), new Vector2(270f, 72f));
        var ot = ok.GetComponentInChildren<TMP_Text>();
        ot.color = Color.white; ot.enableAutoSizing = true; ot.fontSizeMin = 18f; ot.fontSizeMax = 30f;
        confirmOkLabel = ot;
        BindModal(ok, ConfirmOk);

        confirmGo.SetActive(false);
    }

    // ---------- нижние кнопки
    void BuildFooter(Transform content, float y, bool showQuit)
    {
        var row = NewRT("Footer", content);
        TL(row, 0f, y, InnerW, 80f);
        var buttons = new List<Button>();

        continueBtn = UiKit.MakeButton(row, "Продолжить", UiBtn.Green, new Vector2(300f, 76f), 32f, 1f);
        continueBtn.navigation = new Navigation { mode = Navigation.Mode.None };
        Bind(continueBtn, ContinueGame);
        continueLabel = continueBtn.GetComponentInChildren<TMP_Text>();
        continueLabel.color = UiKit.Navy; continueLabel.enableAutoSizing = true; continueLabel.fontSizeMin = 18f; continueLabel.fontSizeMax = 32f;
        refreshers.Add(RefreshContinue);
        buttons.Add(continueBtn);

        restartBtn = UiKit.MakeButton(row, "Новая игра", UiBtn.Gray, new Vector2(300f, 76f), 30f, 1f);
        restartBtn.navigation = new Navigation { mode = Navigation.Mode.None };
        Bind(restartBtn, ShowNewGameConfirm);
        restartLabel = restartBtn.GetComponentInChildren<TMP_Text>();
        restartLabel.color = Color.white; restartLabel.enableAutoSizing = true; restartLabel.fontSizeMin = 18f; restartLabel.fontSizeMax = 30f;
        buttons.Add(restartBtn);

        menuBtn = UiKit.MakeButton(row, "В главное меню", UiBtn.Gray, new Vector2(300f, 76f), 28f, 1f);
        menuBtn.navigation = new Navigation { mode = Navigation.Mode.None };
        Bind(menuBtn, AskMainMenu);
        menuLabel = menuBtn.GetComponentInChildren<TMP_Text>();
        menuLabel.color = Color.white; menuLabel.enableAutoSizing = true; menuLabel.fontSizeMin = 16f; menuLabel.fontSizeMax = 28f;
        refreshers.Add(RefreshMenuBtn);
        buttons.Add(menuBtn);

        if (showQuit)
        {
            var quit = quitBtn = UiKit.MakeButton(row, "Выйти", UiBtn.Gray, new Vector2(200f, 76f), 30f, 1f);
            quit.navigation = new Navigation { mode = Navigation.Mode.None };
            Bind(quit, QuitGame);
            var qt = quit.GetComponentInChildren<TMP_Text>();
            qt.color = Color.white; qt.enableAutoSizing = true; qt.fontSizeMin = 18f; qt.fontSizeMax = 30f;
            buttons.Add(quit);
        }

        // раскладка по центру: кнопки одной ширины, не шире 300
        const float gap = 14f;
        float w = Mathf.Min(300f, (InnerW - gap * (buttons.Count - 1)) / buttons.Count);
        float total = w * buttons.Count + gap * (buttons.Count - 1);
        float x = (InnerW - total) * 0.5f;
        continueX = x;
        for (int i = 0; i < buttons.Count; i++) { PlaceBtn(buttons[i], x, w); x += w + gap; }
    }

    static void PlaceBtn(Button b, float x, float w)
    {
        Anch(b.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(w, 76f));
    }

    // ============================================================ помощники разметки

    // от левого верхнего угла родителя: x вправо, y вниз
    static void TL(RectTransform r, float x, float y, float w, float h)
    {
        Anch(r, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(w, h));
    }

    // то же, но по центру элемента
    static void TLC(RectTransform r, float cx, float cy, float w, float h)
    {
        Anch(r, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(cx, -cy), new Vector2(w, h));
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
    Action onRelease;
    float value;

    const float Inset = 8f;

    public void Init(RectTransform bar, Image fill, RectTransform knob, TMP_Text pct, Action<float> onChange, Action onRelease = null)
    {
        this.bar = bar; this.fill = fill; this.knob = knob; this.pct = pct; this.onChange = onChange; this.onRelease = onRelease;
    }

    public void SetValue(float v)
    {
        value = Mathf.Clamp01(v);
        if (fill != null) fill.fillAmount = value;
        if (pct != null) pct.text = Mathf.RoundToInt(value * 100f) + "%";
        if (knob != null && bar != null)
        {
            float half = bar.rect.width * 0.5f - Inset;
            knob.anchoredPosition = new Vector2(Mathf.Lerp(-half, half, value), 0f);
        }
    }

    // попал ли курсор в полоску (с запасом: ручка выше самой полоски)
    public bool Contains(Vector2 screenPos)
    {
        if (bar == null) return false;
        Vector2 lp;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(bar, screenPos, null, out lp)) return false;
        Rect r = bar.rect;
        r.xMin -= 24f; r.xMax += 24f; r.yMin -= 20f; r.yMax += 20f;
        return r.Contains(lp);
    }

    public void PickAt(Vector2 screenPos)
    {
        if (bar == null) return;
        Vector2 lp;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(bar, screenPos, null, out lp)) return;
        float half = bar.rect.width * 0.5f - Inset;
        float v = Mathf.Clamp01(Mathf.InverseLerp(-half, half, lp.x));
        if (v < 0.03f) v = 0f;          // у левого края проще выключить звук совсем
        SetValue(v);
        if (onChange != null) onChange(value);
    }

    public void Release() { if (onRelease != null) onRelease(); }
}
