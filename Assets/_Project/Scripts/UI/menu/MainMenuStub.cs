// Assets/_Project/Scripts/UI/menu/MainMenuStub.cs (НЕ в Editor)
// ШАГ 4 (имя класса оставлено MainMenuStub, чтобы не пересобирать сцену; переименуем позже).
// Главное меню: деревянная доска под вывеской и четыре кнопки-плашки (Продолжить / Новая игра / Настройки / Выйти),
// как на референсе. Всё рисуется кодом через UiKit и лежит ребёнком Art: доска стоит на одном месте картинки на любом экране.
// Управление: мышь, стрелки / W S, Enter / E / Space / Submit (геймпад), Esc закрывает диалог. Нажатия считаем сами по координатам курсора
// (как в SettingsMenu), поэтому меню не зависит от EventSystem.
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UiLayout;

[System.Serializable]
public class MenuSkin
{
    [Tooltip("Доска, 9-slice (граница 40 px). Пусто: рисуется из UiKit")]
    public Sprite board;
    [Header("Плашки (Normal / Hover / Pressed)")]
    public Sprite amberNormal, amberHover, amberPressed;
    public Sprite greenNormal, greenHover, greenPressed;
    public Sprite grayDisabled;
    [Header("Иконки")]
    public Sprite iconPlayNavy, iconLeaf, iconGear, iconExit;
    [Header("Размеры спрайтов (дизайн-пиксели, ставит генератор)")]
    [Tooltip("Поля доски вокруг лицевой части: лево, низ, право, верх")]
    public Vector4 boardPad = new Vector4(30f, 52f, 38f, 36f);
    public Vector2 plateSpriteSize = new Vector2(508f, 153f);
    [Tooltip("Где в спрайте плашки центр лицевой части (доли)")]
    public Vector2 plateFacePivot = new Vector2(0.5f, 0.5719f);
    public bool Ready { get { return amberNormal != null && greenNormal != null && grayDisabled != null; } }
}

public class MainMenuStub : MonoBehaviour
{
    [Header("Скин (ставит Tools > Timber Wings > Шаг 4: скин меню)")]
    public MenuSkin skin = new MenuSkin();

    [Tooltip("TMP-шрифт с кириллицей. Пусто: ищется любой загруженный с буквой «Ж»")]
    public TMP_FontAsset menuFont;

    [Header("Доска (доли картинки, y от верха)")]
    [Tooltip("Доска уже нарисована на фоне: код её не рисует, только ставит плашки на её место")]
    public bool boardInPicture = true;
    [Tooltip("x = центр доски, y = её верхний край")]
    public Vector2 boardAnchor = new Vector2(0.232f, 0.412f);
    [Tooltip("Ширина доски в пикселях при 1920x1080")]
    public float boardWidth = 514f;
    [Tooltip("Размер кнопки-плашки при 1920x1080")]
    public Vector2 plateSize = new Vector2(448f, 87f);
    public float plateStep = 117f;
    public float firstPlateY = 75f;               // от верха доски до центра первой плашки
    [Tooltip("Лишний зазор под «Продолжить» для строки «Уровень, день» (остальные плашки сдвигаются вниз)")]
    public float subLineGap = 16f;
    [Tooltip("Сдвиг выбранной плашки вправо")]
    public float hoverShift = 10f;

    [Header("Появление")]
    public float fadeFromBlack = 0.7f;
    public float itemStagger = 0.09f;

    // ------------------------------------------------------------ элементы
    class Item
    {
        public RectTransform root;      // невидимая зона нажатия (она же родитель плашки)
        public RectTransform plate, face;
        public float k = 1f;
        public Image plateImg, icon;
        public bool iconSkinned;
        public TMP_Text label;
        public CanvasGroup cg;
        public UiBtn style;
        public System.Action act;
        public bool enabled = true;
        public float shift, appearAt;
        public Vector2 basePos;
    }

    class Group
    {
        public readonly List<Item> items = new List<Item>();
        public bool horizontal;
        public int sel = -1;
    }

    Transform canvasRoot;                       // канвас меню (для окон на весь экран)
    RectTransform board;
    CanvasGroup boardGroup;
    MainMenuBackground bg;
    readonly Group main = new Group();
    Group modal;                                // открытый диалог или null
    GameObject modalGo;
    Item pressed;
    Vector2 lastMouse;
    CanvasGroup blackGroup;
    TMP_Text contSub;                           // «Уровень N, день M» под «Продолжить»
    float t0, swallowUntil;
    int newGameIndex, settingsIndex;
    AudioSource sfx;
    static AudioClip blip, tick;

    static readonly Color Cream = new Color32(0xFF, 0xF1, 0xD0, 255);
    static readonly Color Dim = new Color(0.62f, 0.64f, 0.66f, 1f);
    static readonly Color WoodText = new Color32(0x2A, 0x16, 0x08, 255);

    // ------------------------------------------------------------ жизненный цикл
    void Awake()
    {
        Time.timeScale = 1f;
        SettingsMenu.Suppressed = true;
        if (menuFont == null) menuFont = FindCyrillicFont();
        if (menuFont != null) UiKit.DefaultFont = menuFont;
        else Debug.LogWarning("MainMenu: не найден TMP-шрифт с кириллицей. Перетащи его в поле Menu Font.");

        EnsureEventSystem();
        bg = FindFirstObjectByType<MainMenuBackground>();
        if (bg == null) { Debug.LogError("MainMenu: на сцене нет MainMenuBackground (шаг 3)."); enabled = false; return; }
        bg.EnsureBuilt();

        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false; sfx.spatialBlend = 0f;

        Build();
        t0 = Time.unscaledTime;
        lastMouse = Input.mousePosition;
    }

    void OnDestroy() { SettingsMenu.Suppressed = false; }

    static TMP_FontAsset FindCyrillicFont()
    {
        var all = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        for (int i = 0; i < all.Length; i++)
            if (all[i] != null && all[i].HasCharacter('Ж')) return all[i];
        return null;
    }

    static void EnsureEventSystem() { UiInputGuard.EnsureEventSystem(); }

    // ------------------------------------------------------------ построение
    void Build()
    {
        canvasRoot = bg.Art.parent;
        bool quit = Application.platform != RuntimePlatform.WebGLPlayer && !Application.isMobilePlatform;
        int n = quit ? 4 : 3;
        float h = firstPlateY + subLineGap + plateStep * (n - 1) + plateSize.y * 0.5f + 49f;

        // доска: ребёнок Art, привязана к доле картинки; масштаб следует за размером картинки
        var go = new GameObject("MenuBoard", typeof(RectTransform), typeof(CanvasGroup));
        go.transform.SetParent(bg.Art, false);
        board = (RectTransform)go.transform;
        board.anchorMin = board.anchorMax = new Vector2(boardAnchor.x, 1f - boardAnchor.y);
        board.pivot = new Vector2(0.5f, 1f);
        board.anchoredPosition = Vector2.zero;
        board.sizeDelta = new Vector2(boardWidth, h);
        boardGroup = go.GetComponent<CanvasGroup>();

        if (skin != null && skin.Ready && skin.board != null)
        {
            var wood = UiKit.MakeImage(board, "Wood", skin.board, true, 1f);
            var wr = wood.rectTransform;
            wr.anchorMin = Vector2.zero; wr.anchorMax = Vector2.one;
            wr.offsetMin = new Vector2(-skin.boardPad.x, -skin.boardPad.y);     // тень, толщина и цепочки выходят за лицевую часть
            wr.offsetMax = new Vector2(skin.boardPad.z, skin.boardPad.w);
        }
        else if (!boardInPicture)
        {
            var wood = UiKit.MakePanel(board, new Vector2(boardWidth, h), true, 1.3f, "Wood");
            Stretch(wood.rectTransform);
        }

        int i = 0;
        AddMain("Продолжить", UiBtn.Amber, Icon.Play, OnContinue, i++);
        newGameIndex = i;
        AddMain("Новая игра", UiBtn.Green, Icon.Leaf, OnNewGame, i++);
        settingsIndex = i;
        AddMain("Настройки", UiBtn.Green, Icon.Gear, OnSettings, i++);
        if (quit) AddMain("Выйти", UiBtn.Green, Icon.Exit, OnQuit, i++);

        // мелкая строка под «Продолжить»: лежит в зоне нажатия плашки как ребёнок, поэтому появляется вместе с ней
        contSub = UiKit.MakeLabel(main.items[0].root, "", 26f, Cream, TextAlignmentOptions.Center, true);
        var sr = contSub.rectTransform;
        sr.anchorMin = sr.anchorMax = new Vector2(0.5f, 0.5f); sr.pivot = new Vector2(0.5f, 0.5f);
        sr.sizeDelta = new Vector2(plateSize.x, 30f);
        sr.anchoredPosition = new Vector2(0f, -(main.items[0].root.sizeDelta.y * 0.5f + 28f));
        contSub.outlineWidth = 0.25f; contSub.outlineColor = new Color32(0x2A, 0x16, 0x08, 255);
        contSub.raycastTarget = false;
        contSub.enableAutoSizing = true; contSub.fontSizeMin = 18f; contSub.fontSizeMax = 26f;
        RefreshContinueInfo();

        main.items[0].enabled = SaveSystem.HasSave;
        main.sel = main.items[0].enabled ? 0 : 1;

        // версия: правый нижний угол канваса, янтарная
        var ver = UiKit.MakeLabel(canvasRoot, "v" + Application.version, 28f, UiKit.Brass, TextAlignmentOptions.BottomRight, true);
        var vr = ver.rectTransform;
        vr.anchorMin = vr.anchorMax = new Vector2(1f, 0f); vr.pivot = new Vector2(1f, 0f);
        vr.anchoredPosition = new Vector2(-28f, 20f); vr.sizeDelta = new Vector2(400f, 44f);
        ver.outlineWidth = 0.25f; ver.outlineColor = new Color32(0x3A, 0x20, 0x0C, 255);

        // чёрная шторка для проявления поверх всего меню
        var bl = UiKit.MakeImage(canvasRoot, "FadeIn", null, false);
        bl.color = Color.black; bl.raycastTarget = false;
        Stretch(bl.rectTransform);
        blackGroup = bl.gameObject.AddComponent<CanvasGroup>();
        blackGroup.alpha = VibeSettings.ReduceEffects ? 0f : 1f;
        blackGroup.blocksRaycasts = false;

        for (int k = 0; k < main.items.Count; k++)
        {
            main.items[k].appearAt = fadeFromBlack * 0.6f + itemStagger * k;
            if (!VibeSettings.ReduceEffects) main.items[k].cg.alpha = 0f;
        }
        boardGroup.alpha = VibeSettings.ReduceEffects ? 1f : 0f;
        ApplyVisuals(true);
    }

    void RefreshContinueInfo()
    {
        if (contSub == null) return;
        string t = SaveSystem.HasSave ? SaveSummary.Text() : null;
        contSub.text = t ?? "";
        contSub.gameObject.SetActive(!string.IsNullOrEmpty(t));
    }

    void AddMain(string text, UiBtn style, Icon ic, System.Action act, int index)
    {
        var it = MakeItem(board, text, style, plateSize, 40f, ic, act);
        it.root.anchorMin = it.root.anchorMax = new Vector2(0.5f, 1f);
        it.basePos = new Vector2(0f, -(firstPlateY + plateStep * index + (index > 0 ? subLineGap : 0f)));
        it.root.anchoredPosition = it.basePos;
        main.items.Add(it);
    }

    Item MakeItem(Transform parent, string text, UiBtn style, Vector2 size, float fontSize, Icon ic, System.Action act)
    {
        var it = new Item { style = style, act = act };
        var go = new GameObject("Item_" + text, typeof(RectTransform), typeof(CanvasGroup));
        go.transform.SetParent(parent, false);
        it.root = (RectTransform)go.transform;
        it.root.pivot = new Vector2(0.5f, 0.5f);
        it.root.sizeDelta = new Vector2(size.x, Mathf.Max(size.y, 76f));     // зона нажатия не ниже 76
        it.cg = go.GetComponent<CanvasGroup>();

        bool skinned = skin != null && skin.Ready;
        it.plateImg = UiKit.MakeImage(it.root, "Plate", PlateSprite(style, UiBtnState.Normal), !skinned, 1f);
        it.plate = it.plateImg.rectTransform;
        it.plate.anchorMin = it.plate.anchorMax = new Vector2(0.5f, 0.5f);
        it.k = skinned ? size.x / plateSize.x : 1f;
        if (skinned) { it.plate.pivot = skin.plateFacePivot; it.plate.sizeDelta = skin.plateSpriteSize * it.k; }
        else it.plate.sizeDelta = size;

        // лицевая часть: на ней текст и иконка (спрайт шире её на тень и толщину)
        var fgo = new GameObject("Face", typeof(RectTransform));
        fgo.transform.SetParent(it.plate, false);
        it.face = (RectTransform)fgo.transform;
        it.face.anchorMin = it.face.anchorMax = skinned ? skin.plateFacePivot : new Vector2(0.5f, 0.5f);
        it.face.pivot = new Vector2(0.5f, 0.5f);
        it.face.anchoredPosition = Vector2.zero;
        it.face.sizeDelta = size;

        float iconSize = size.y * 0.52f;
        if (ic != Icon.None)
        {
            Sprite skinIcon = SkinIcon(ic);
            it.iconSkinned = skinIcon != null;
            it.icon = UiKit.MakeImage(it.face, "Icon", skinIcon != null ? skinIcon : IconSprite(ic), false);
            it.icon.preserveAspect = true;
            var r = it.icon.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0f, 0.5f); r.pivot = new Vector2(0.5f, 0.5f);
            if (skinned) { iconSize = size.y * 0.58f; r.anchoredPosition = new Vector2(size.y * 0.98f, 2f); }
            else r.anchoredPosition = new Vector2(size.y * 0.62f, 3f);
            r.sizeDelta = new Vector2(iconSize, iconSize);
        }

        it.label = UiKit.MakeLabel(it.face, text, fontSize, Color.white, TextAlignmentOptions.Center, true);
        it.label.rectTransform.offsetMin = new Vector2(ic != Icon.None ? size.y * (skinned ? 1.55f : 1.0f) : 12f, 5f);
        it.label.rectTransform.offsetMax = new Vector2(-24f, -1f);
        it.label.enableAutoSizing = true; it.label.fontSizeMin = 20f; it.label.fontSizeMax = fontSize;
        it.label.fontStyle = FontStyles.Bold;
        return it;
    }

    Sprite PlateSprite(UiBtn style, UiBtnState st)
    {
        if (skin == null || !skin.Ready) return UiKit.BtnSprite(style, st);
        if (st == UiBtnState.Disabled) return skin.grayDisabled;
        bool amber = style == UiBtn.Amber || style == UiBtn.Yellow;
        Sprite n = amber ? skin.amberNormal : skin.greenNormal;
        Sprite h = (amber ? skin.amberHover : skin.greenHover) ?? n;
        Sprite p = (amber ? skin.amberPressed : skin.greenPressed) ?? n;
        return st == UiBtnState.Hover ? h : st == UiBtnState.Pressed ? p : n;
    }

    Sprite SkinIcon(Icon ic)
    {
        if (skin == null || !skin.Ready) return null;
        return ic == Icon.Play ? skin.iconPlayNavy : ic == Icon.Leaf ? skin.iconLeaf : ic == Icon.Gear ? skin.iconGear : ic == Icon.Exit ? skin.iconExit : null;
    }

    // ------------------------------------------------------------ диалог
    void ShowConfirm(string title, string body, string okText, System.Action onOk)
    {
        if (modal != null) return;
        var dim = UiKit.MakeImage(canvasRoot, "Confirm", null, false);
        dim.color = new Color(0.02f, 0.04f, 0.08f, 0.66f);
        dim.raycastTarget = true;
        Stretch(dim.rectTransform);
        modalGo = dim.gameObject;
        var cg = modalGo.AddComponent<CanvasGroup>();
        if (!VibeSettings.ReduceEffects) cg.alpha = 0f;

        var win = UiKit.MakePanel(dim.transform, new Vector2(760f, 360f), true, 1f, "Window");
        win.rectTransform.anchorMin = win.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);

        var tt = UiKit.MakeLabel(win.transform, title, 36f, WoodText, TextAlignmentOptions.Center, true);
        Place(tt.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -52f), new Vector2(620f, 48f));
        var bd = UiKit.MakeLabel(win.transform, body, 32f, Cream, TextAlignmentOptions.Center, true);
        bd.outlineWidth = 0.2f; bd.outlineColor = new Color32(0x2A, 0x16, 0x08, 255);
        Place(bd.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(680f, 130f));

        modal = new Group { horizontal = true };
        var cancel = MakeItem(win.transform, "Отмена", UiBtn.Green, new Vector2(330f, 64f), 30f, Icon.None, CloseModal);
        cancel.root.anchorMin = cancel.root.anchorMax = new Vector2(0.5f, 0f);
        cancel.basePos = new Vector2(-185f, 78f); cancel.root.anchoredPosition = cancel.basePos;
        var ok = MakeItem(win.transform, okText, UiBtn.Gray, new Vector2(330f, 64f), 30f, Icon.None, () => { var a = onOk; CloseModal(); if (a != null) a(); });
        ok.root.anchorMin = ok.root.anchorMax = new Vector2(0.5f, 0f);
        ok.basePos = new Vector2(185f, 78f); ok.root.anchoredPosition = ok.basePos;
        modal.items.Add(cancel); modal.items.Add(ok);
        modal.sel = 0;                                  // по умолчанию безопасная кнопка
        pressed = null;
        ApplyVisuals(true);
        Play(true, 0.6f);
    }

    static void Place(RectTransform r, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        r.anchorMin = r.anchorMax = anchor; r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos; r.sizeDelta = size;
    }

    void CloseModal()
    {
        if (modalGo != null) Destroy(modalGo);
        modalGo = null; modal = null; pressed = null;
        swallowUntil = Time.unscaledTime + 0.1f;
    }

    // ------------------------------------------------------------ действия
    void OnContinue()
    {
        if (!SaveSystem.HasSave) return;
        MenuFlow.StartGame(MenuRequest.Continue);
    }

    void OnNewGame()
    {
        if (SaveSystem.HasSave)
            ShowConfirm("НОВАЯ ИГРА", "Весь прогресс будет удалён.\nЭто нельзя отменить.", "Удалить", () => MenuFlow.StartGame(MenuRequest.NewGame));
        else
            MenuFlow.StartGame(MenuRequest.NewGame);
    }

    void OnSettings() { SettingsMenu.OpenFromMenu(); }

    void OnQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ------------------------------------------------------------ ввод
    void Update()
    {
        // масштаб доски по размеру картинки: на любой пропорции экрана она совпадает с рисунком
        if (board != null) board.localScale = Vector3.one * (bg.Art.rect.width / 1920f);

        float e = Time.unscaledTime - t0;
        if (blackGroup != null && blackGroup.alpha > 0f)
            blackGroup.alpha = Mathf.Clamp01(1f - e / Mathf.Max(0.01f, fadeFromBlack));
        if (!VibeSettings.ReduceEffects)
        {
            float be = UiDraw.EaseOutCubic(Mathf.Clamp01((e - fadeFromBlack * 0.4f) / 0.5f));
            boardGroup.alpha = be;
            for (int i = 0; i < main.items.Count; i++)
                main.items[i].cg.alpha = Mathf.Clamp01((e - main.items[i].appearAt) / 0.35f);
        }

        if (modalGo != null)
        {
            var cg = modalGo.GetComponent<CanvasGroup>();
            if (cg != null && cg.alpha < 1f) cg.alpha = Mathf.MoveTowards(cg.alpha, 1f, Time.unscaledDeltaTime / 0.15f);
        }

        // обновляем «Продолжить» (сохранение могло появиться/исчезнуть)
        bool has = SaveSystem.HasSave;
        if (main.items.Count > 0 && main.items[0].enabled != has)
        {
            main.items[0].enabled = has;
            if (!has && main.sel == 0) main.sel = 1;
            RefreshContinueInfo();
        }

        if (SettingsMenu.IsOpen) { swallowUntil = Time.unscaledTime + 0.15f; pressed = null; ApplyVisuals(false); return; }
        if (MenuFlow.Busy || Time.unscaledTime < swallowUntil) { ApplyVisuals(false); return; }

        var g = modal ?? main;
        HandlePointer(g);
        HandleKeys(g);
        ApplyVisuals(false);
    }

    void HandlePointer(Group g)
    {
        Vector2 mp = Input.mousePosition;
        bool moved = (mp - lastMouse).sqrMagnitude > 1f;
        lastMouse = mp;

        int over = -1;
        for (int i = 0; i < g.items.Count; i++)
            if (g.items[i].cg.alpha > 0.3f && RectTransformUtility.RectangleContainsScreenPoint(g.items[i].root, mp, null)) { over = i; break; }

        if (moved && over >= 0 && g.items[over].enabled && g.sel != over) { g.sel = over; Play(true, 0.5f); }

        if (Input.GetMouseButtonDown(0))
            pressed = (over >= 0 && g.items[over].enabled) ? g.items[over] : null;
        if (Input.GetMouseButtonUp(0))
        {
            var p = pressed; pressed = null;
            if (p != null && over >= 0 && g.items[over] == p) Activate(p);
        }
    }

    void HandleKeys(Group g)
    {
        int dir = 0;
        if (g.horizontal)
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) dir = -1;
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) dir = 1;
        }
        else
        {
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) dir = -1;
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) dir = 1;
        }
        // геймпад: стик / крестовина по оси Vertical (Horizontal для диалога), по одному шагу на отклонение
        float axis = Input.GetAxisRaw(g.horizontal ? "Horizontal" : "Vertical");
        if (dir == 0)
        {
            if (axis > 0.6f && !axisHeld) { dir = g.horizontal ? 1 : -1; axisHeld = true; }
            else if (axis < -0.6f && !axisHeld) { dir = g.horizontal ? -1 : 1; axisHeld = true; }
            else if (Mathf.Abs(axis) < 0.3f) axisHeld = false;
        }
        if (dir != 0) Move(g, dir);

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.E) ||
            Input.GetKeyDown(KeyCode.Space) || Input.GetButtonDown("Submit"))
        {
            if (g.sel >= 0 && g.sel < g.items.Count && g.items[g.sel].enabled) Activate(g.items[g.sel]);
        }

        if (Input.GetKeyDown(KeyCode.Escape) && modal != null) CloseModal();
    }
    bool axisHeld;

    void Move(Group g, int dir)
    {
        int n = g.items.Count;
        int s = g.sel;
        for (int k = 0; k < n; k++)
        {
            s = (s + dir + n) % n;
            if (g.items[s].enabled) { if (s != g.sel) Play(true, 0.5f); g.sel = s; return; }
        }
    }

    void Activate(Item it)
    {
        if (it == null || !it.enabled || it.act == null) return;
        Play(false, 0.8f);
        it.act();
    }

    // ------------------------------------------------------------ вид плашек
    void ApplyVisuals(bool snap)
    {
        Visual(main, modal == null && !SettingsMenu.IsOpen, snap);
        if (modal != null) Visual(modal, true, snap);
    }

    void Visual(Group g, bool live, bool snap)
    {
        for (int i = 0; i < g.items.Count; i++)
        {
            var it = g.items[i];
            bool selected = live && i == g.sel && it.enabled;
            bool down = selected && pressed == it;

            Sprite s;
            Color txt;
            if (!it.enabled)
            {
                s = PlateSprite(UiBtn.Gray, UiBtnState.Disabled);
                txt = Dim;
            }
            else
            {
                s = PlateSprite(it.style, down ? UiBtnState.Pressed : selected ? UiBtnState.Hover : UiBtnState.Normal);
                txt = (it.style == UiBtn.Amber || it.style == UiBtn.Yellow) ? UiKit.Navy : Cream;
                if (selected && it.style != UiBtn.Amber && it.style != UiBtn.Yellow) txt = Color.white;
            }
            if (it.plateImg.sprite != s) it.plateImg.sprite = s;
            it.label.color = txt;
            bool amberPlate = it.style == UiBtn.Amber || it.style == UiBtn.Yellow;
            if (it.label.outlineWidth != (amberPlate ? 0f : 0.22f))
            {
                it.label.outlineWidth = amberPlate ? 0f : 0.22f;
                it.label.outlineColor = new Color32(0x12, 0x24, 0x14, 255);
            }
            if (it.icon != null) it.icon.color = it.iconSkinned ? (it.enabled ? Color.white : new Color(1f, 1f, 1f, 0.45f)) : txt;

            // сдвиг вправо и лёгкое увеличение выбранной плашки
            float target = (selected && !g.horizontal) ? hoverShift : 0f;
            it.shift = snap || VibeSettings.ReduceEffects ? target : Mathf.MoveTowards(it.shift, target, Time.unscaledDeltaTime * hoverShift * 8f);
            it.plate.anchoredPosition = new Vector2(it.shift, 0f);
            if (it.face != null)
            {
                float lift = skin != null && skin.Ready ? (down ? -7f : selected ? 3f : 0f) * it.k : 0f;
                it.face.anchoredPosition = new Vector2(0f, lift);
            }
            float sc = selected ? 1.02f : 1f;
            it.plate.localScale = Vector3.Lerp(it.plate.localScale, Vector3.one * sc, snap ? 1f : Time.unscaledDeltaTime * 14f);
        }
    }

    // ------------------------------------------------------------ звуки (синтез: файлов нет)
    void Play(bool soft, float vol)
    {
        if (sfx == null) return;
        if (tick == null) tick = Synth(1250f, 0.05f);
        if (blip == null) blip = Synth(720f, 0.11f);
        var c = soft ? tick : blip;
        sfx.PlayOneShot(c, Mathf.Clamp01(vol) * GameAudio.SfxLevel);
    }

    static AudioClip Synth(float freq, float len)
    {
        int rate = 44100, n = (int)(rate * len);
        var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate, env = Mathf.Exp(-t * (6f / len)) * Mathf.Clamp01(t * 400f);
            d[i] = (Mathf.Sin(6.2832f * freq * t) + 0.35f * Mathf.Sin(6.2832f * freq * 2f * t)) * 0.3f * env;
        }
        var c = AudioClip.Create("menu_sfx", n, 1, rate, false);
        c.SetData(d, 0);
        return c;
    }

    // ------------------------------------------------------------ иконки (рисуем кодом, белые, красятся через color)
    enum Icon { None, Play, Leaf, Gear, Exit }
    static readonly Dictionary<Icon, Sprite> iconCache = new Dictionary<Icon, Sprite>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { iconCache.Clear(); blip = tick = null; }

    static Sprite IconSprite(Icon ic)
    {
        Sprite s;
        if (iconCache.TryGetValue(ic, out s) && s != null) return s;
        if (ic == Icon.Play) s = UiKit.Icon(UiIcon.Play);
        else s = DrawIcon(ic);
        iconCache[ic] = s;
        return s;
    }

    // u, v в -1..1 (v вверх). true = закрашено
    static bool Shape(Icon ic, float u, float v)
    {
        switch (ic)
        {
            case Icon.Gear:
            {
                float r = Mathf.Sqrt(u * u + v * v), a = Mathf.Atan2(v, u);
                float outer = 0.66f + 0.2f * Mathf.Clamp01(Mathf.Cos(a * 8f) * 3f + 0.5f);
                return r < outer && r > 0.27f;
            }
            case Icon.Leaf:
            {
                float x = (u + v) * 0.7071f, y = (v - u) * 0.7071f;       // повернули на 45°: лист смотрит вверх-вправо
                const float R = 1.15f, d = 0.68f;
                bool lens = (x * x + (y + d) * (y + d) < R * R) && (x * x + (y - d) * (y - d) < R * R);
                bool rib = Mathf.Abs(y) < 0.045f && x > -0.95f && x < 0.45f;
                bool stem = Mathf.Abs(y) < 0.07f && x <= -0.55f && x > -0.98f;
                return (lens && !rib) || stem;
            }
            case Icon.Exit:
            {
                bool frame = u > -0.85f && u < -0.12f && v > -0.85f && v < 0.85f &&
                             !(u > -0.7f && u < -0.27f && v > -0.7f && v < 0.7f);
                bool shaft = u > -0.2f && u < 0.55f && Mathf.Abs(v) < 0.11f;
                bool head = u >= 0.35f && u < 0.9f && Mathf.Abs(v) < (0.9f - u) * 0.62f;
                return frame || shaft || head;
            }
        }
        return false;
    }

    static Sprite DrawIcon(Icon ic)
    {
        const int N = 64, SS = 3;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                int hit = 0;
                for (int sy = 0; sy < SS; sy++)
                    for (int sx = 0; sx < SS; sx++)
                    {
                        float u = ((x + (sx + 0.5f) / SS) / N) * 2f - 1f;
                        float v = ((y + (sy + 0.5f) / SS) / N) * 2f - 1f;
                        if (Shape(ic, u, v)) hit++;
                    }
                px[y * N + x] = new Color(1f, 1f, 1f, hit / (float)(SS * SS));
            }
        tex.SetPixels(px); tex.Apply();
        var sp = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
        sp.name = "MenuIcon_" + ic;
        return sp;
    }
}
