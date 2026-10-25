// Assets/_Project/Scripts/UI/Workers/WorkersWindow.cs (НЕ в Editor)
// Окно «Лагерь» (дизайн «Система рабочих», задача 5). Стиль Candy (задача 13: UiCandy.Window, пилюли-вкладки, конфетные карточки), всё строится из кода (на сцене ничего настраивать не нужно,
// окно создаётся при первом E у палатки). Публичный API прежний: Instance, IsOpen, IsOpenFor(WorkerCamp), Toggle(WorkerCamp).
//
// Три вкладки (клавиши 1, 2, 3 или Tab):
//   «Бригада»   6 слотов лагеря карточками, запас (кого нет в слотах), подробности выбранного рабочего (уровень, опыт, выработка,
//               4 перка роли: открытые и закрытые), склад лагеря: полосы бревен и досок, «Забрать» (Пробел) и «Продать −20%».
//   «Паки»      три пака: цена, уровень открытия, ШАНСЫ ВИДНЫ ДО ПОКУПКИ, счётчики гарантий («до мифика: 63/100»),
//               кнопка «Открыть» (серая с причиной, если нельзя) и «Шансы» (подробное окно: шансы, гарантии, награда за дубликат).
//   «Коллекция» все 27 персонажей по ролям; не найденные в виде силуэтов с подсказкой, из какого пака выпадают.
//
// Открытие пака: WorkerManager.TryOpenPack вызывается ОДИН раз по кнопке, результат известен сразу, анимация его только показывает.
//   Тряска пака с искрами → вспышка цвета редкости → карточка выезжает (лучи у Необычного и выше, тряска окна у Редкого и выше) →
//   результат: «НОВЫЙ!» или дубликат (опыт/монеты), в какой слот встал, сработала ли гарантия. Вся анимация 3,4 с (меньше 4).
//   E или Esc пропускают анимацию; на результате E, Esc или «Готово» закрывают его; Enter открывает ещё один такой же пак.
//
// Закрывается: Esc, E, крестик, отойти от палатки, начался разговор. Пока окно открыто, MobileInput.ModalOpen = true (персонаж стоит).
// Доступность (GDD 10.4): редкость = слово + число звёзд + камень + цвет; роль = иконка + слово; причины «нельзя» всегда пишутся словами.
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UiLayout;

// ----- КАРТА ФАЙЛОВ (класс разбит на части partial, поведение не менялось) -----
// WorkersWindow.cs: статический API, размеры, цвета, состояние, жизненный цикл (этот файл)
// WorkersWindow.Tabs.cs: вкладки «Бригада», «Паки», «Коллекция» и склад лагеря
// WorkersWindow.PackOpen.cs: экран открытия пака и окно шансов (логика и анимация)
// WorkersWindow.Build.cs: помощники кнопок и построение окна из кода
public partial class WorkersWindow : MonoBehaviour
{
    // ============================================================ статический API (прежний)
    public static WorkersWindow Instance { get; private set; }

    public bool IsOpen { get { return open; } }
    public static bool IsOpenFor(WorkerCamp c) { return Instance != null && Instance.open && Instance.camp == c; }

    public static void Toggle(WorkerCamp c)
    {
        var win = GetOrCreate();
        if (win.open && win.camp == c) win.Close(); else win.Open(c);
    }

    static WorkersWindow GetOrCreate()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("WorkersWindow");
        return go.AddComponent<WorkersWindow>();   // Awake выставит Instance
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { Instance = null; }

    // ============================================================ размеры (единицы Canvas 1920x1080)
    const float WinW = 1400f, WinH = 900f, BS = 1.5f;
    const float PageX = 16f, PageY = 76f, PageW = 1290f, PageH = 715f;
    const float AnimDur = 3.4f;                       // вся анимация пака (меньше 4 секунд)

    // цвета текста на светлых Candy-панелях: белый и бледный текст старого стиля тут пропал бы
    static readonly Color Ink = UiCandy.Ink;            // основной текст
    static readonly Color Soft = UiCandy.Muted;         // второстепенный текст
    static readonly Color Dim = WorkerUi.Dim;           // подсказки и «выключено»
    static readonly Color Warn = UiCandy.GoldText;      // «нельзя», «полон», нехватка (золотисто-коричневый, читается на светлом)
    static readonly Color Good = UiCandy.Done;          // «хорошо»

    enum Tab { Brigade = 0, Packs = 1, Collection = 2 }
    enum Phase { None, Anim, Result, Table }

    class Btn
    {
        public Button b;
        public TMP_Text t;
        public UiBtn style;
        public Color fg;
    }

    // ============================================================ состояние
    WorkerCamp camp;
    bool open, built, dirty, oddsOpen;
    float openK, refreshAt, shakeT;
    int openFrame = -1;
    Tab tab = Tab.Brigade;
    string selId;                       // выбранный рабочий (Бригада)
    WorkerDefinition selDef;            // выбранный персонаж (Коллекция)
    int packSel;                        // подсвеченный пак (стрелки, Enter)
    WorkerPackType oddsType;
    string message = "";
    float messageUntil;

    WorkerPackTable table;              // стол пака: выбор одной из трёх карт
    CanvasGroup overlay;
    RectTransform panel, inner;
    TMP_Text coinsText, titleText;
    readonly RectTransform[] pages = new RectTransform[3];
    readonly Btn[] tabBtns = new Btn[3];

    // ---- Бригада
    TMP_Text slotsLabel, reserveLabel;
    readonly WorkerCardView[] slotCards = new WorkerCardView[CampSimulation.Slots];
    readonly Image[] slotEmpty = new Image[CampSimulation.Slots];
    readonly TMP_Text[] slotEmptyText = new TMP_Text[CampSimulation.Slots];
    readonly List<WorkerCardView> reserveCards = new List<WorkerCardView>();
    readonly List<string> reserveIds = new List<string>();
    RectTransform reserveContent;
    GameObject detailBody, detailEmpty;
    WorkerCardView detailCard;
    TMP_Text dName, dAge, dRarity, dRole, dLevel, dXp, dProd, dStatus;
    Image dGem, dRoleIcon, dStatusIcon, dXpFill;
    Image[] dStars = new Image[5];
    readonly Image[] perkIcon = new Image[5];
    readonly TMP_Text[] perkName = new TMP_Text[5];
    readonly TMP_Text[] perkDesc = new TMP_Text[5];
    Btn dAction;
    TMP_Text stockMsg, stockPreview;
    Image logFill, plankFill;
    TMP_Text logText, plankText;
    Btn takeBtn, sellBtn;

    // ---- Паки
    class PackUi
    {
        public Image hl, artGlow;
        public Image priceIcon, ribbon;
        public TMP_Text priceText, ribbonText, reason;
        public Image[] oddIcon = new Image[5];
        public TMP_Text[] oddName = new TMP_Text[5];
        public TMP_Text[] oddPct = new TMP_Text[5];
        public RectTransform[] oddFill = new RectTransform[5];
        public TMP_Text[] guar = new TMP_Text[3];
        public Image mythBack;
        public RectTransform mythFill;
        public Btn open, odds;
    }
    readonly PackUi[] packUi = new PackUi[3];

    // ---- Коллекция
    bool collBuilt;
    TMP_Text collCount;
    readonly List<WorkerDefinition> collDefs = new List<WorkerDefinition>();
    readonly List<WorkerCardView> collCards = new List<WorkerCardView>();
    readonly TMP_Text[] collRoleCount = new TMP_Text[3];
    WorkerCardView cdCard;
    TMP_Text cdName, cdLine, cdRarity, cdState, cdPerks, cdBio;
    Image cdGem;
    Image[] cdStars = new Image[5];
    GameObject cdBody;

    // ---- Открытие пака
    Phase phase = Phase.None;
    WorkerPackType resType;
    PackResult res;
    float animT;
    bool burstDone;
    GameObject openRoot;
    CanvasGroup infoCg;
    Image openArt, openGlow, openRays, flash;
    RectTransform artRt, glowRt, raysRt, fxRt;
    WorkerCardView resCard;
    TMP_Text waitText, openHint;
    Image rTag, rGem, rRoleIcon;
    TMP_Text rTagText, rName, rRarity, rRole, rLevel, rLine, rPerks, rBio;
    readonly Image[] rStars = new Image[5];
    Btn againBtn, doneBtn;

    class Spark { public Image img; public RectTransform rt; public Vector2 p, v; public float life, max, size; public bool live; }
    readonly Spark[] sparks = new Spark[44];

    // ---- Шансы
    GameObject oddsGo;
    TMP_Text oTitle, oGuar, oDup;
    readonly Image[] oGem = new Image[5];
    readonly TMP_Text[] oName = new TMP_Text[5], oPct = new TMP_Text[5], oPerks = new TMP_Text[5], oXp = new TMP_Text[5];
    readonly RectTransform[] oFill = new RectTransform[5];
    readonly TMP_Text[] oGuarLine = new TMP_Text[3];
    RectTransform oMythFill;

    // ============================================================ жизненный цикл
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        UiModal.Register(this, () => IsOpen, Close);
    }

    void OnEnable()
    {
        WorkerManager.OnRosterChanged += MarkDirty;
        WorkerManager.OnCampStockChanged += MarkDirty;
        WorkerManager.OnWorkerLevelUp += OnLevelUp;
    }

    void OnDisable()
    {
        WorkerManager.OnRosterChanged -= MarkDirty;
        WorkerManager.OnCampStockChanged -= MarkDirty;
        WorkerManager.OnWorkerLevelUp -= OnLevelUp;
    }

    void OnDestroy() { UiModal.Unregister(this); if (Instance == this) Instance = null; }

    void MarkDirty() { dirty = true; }
    void OnLevelUp(WorkerState w) { dirty = true; }

    void Open(WorkerCamp c)
    {
        if (c == null) return;
        EnsureBuilt();
        camp = c;
        open = true;
        openFrame = Time.frameCount;       // E, которой открыли окно, его же не закрывает
        message = "";
        ResetOverlays();
        var m = WorkerManager.Instance;
        if (m != null && m.Packs.FreePending) tab = Tab.Packs;     // ждёт подарочный пак: сразу показываем
        packSel = 0;
        SetTab(tab);
        Refresh();
    }

    void Close()
    {
        open = false;
        camp = null;
        ResetOverlays();
    }

    void EnsureBuilt()
    {
        if (built) return;
        UiInputGuard.EnsureEventSystem();
        Build();
    }

    void ResetOverlays()
    {
        phase = Phase.None;
        oddsOpen = false;
        shakeT = 0f;
        if (!built) return;
        if (table != null) table.Hide();
        openRoot.SetActive(false);
        oddsGo.SetActive(false);
        panel.anchoredPosition = Vector2.zero;
        for (int i = 0; i < sparks.Length; i++) { sparks[i].live = false; sparks[i].img.gameObject.SetActive(false); }
    }

    void Update()
    {
        if (!built) return;

        if (open)
        {
            var m = WorkerManager.Instance;
            if (m == null || camp == null || camp.DistanceToPlayer() > camp.radius * 1.5f
                || DialogueManager.IsBusy || DialogueSequence.AnyPlaying || HudMinimap.BigMapOpen)
                Close();
            else
            {
                // выбранная кнопка реагировала бы ещё и на Enter: снимаем выделение, Enter обрабатывает только Hotkeys
                var es = EventSystem.current;
                if (es != null && es.currentSelectedGameObject != null) es.SetSelectedGameObject(null);
                if (!SettingsMenu.IsOpen) Hotkeys();
            }
        }

        openK = Mathf.MoveTowards(openK, open ? 1f : 0f, Time.unscaledDeltaTime / 0.15f);
        bool show = openK > 0.001f;
        if (overlay.gameObject.activeSelf != show) overlay.gameObject.SetActive(show);
        if (!show) return;

        overlay.alpha = openK;
        float dt = Time.unscaledDeltaTime;
        UpdateShake(dt);
        // появление окна (масштаб и проявление) делает CandyPopIn на панели UiCandy.Window: он играет при каждом включении затемнения

        if (!open) return;
        if (dirty || Time.unscaledTime >= refreshAt) Refresh();
        TickOpening(dt);
    }

    void UpdateShake(float dt)
    {
        if (shakeT > 0f)
        {
            shakeT = Mathf.Max(0f, shakeT - dt);
            float a = 9f * (shakeT / 0.45f);
            panel.anchoredPosition = new Vector2(Random.Range(-a, a), Random.Range(-a, a));
            if (shakeT <= 0f) panel.anchoredPosition = Vector2.zero;
        }
    }

    // ============================================================ клавиши
    void Hotkeys()
    {
        bool esc = Input.GetKeyDown(KeyCode.Escape);
        bool act = Input.GetKeyDown(MobileInput.ActionKey) && Time.frameCount != openFrame;
        if (esc || act) { Back(); return; }

        if (oddsOpen) return;
        if (phase == Phase.Table) { table.HandleKeys(); return; }
        if (phase == Phase.Anim) return;
        if (phase == Phase.Result)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) OpenAgain();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Alpha1)) SetTab(Tab.Brigade);
        else if (Input.GetKeyDown(KeyCode.Alpha2)) SetTab(Tab.Packs);
        else if (Input.GetKeyDown(KeyCode.Alpha3)) SetTab(Tab.Collection);
        else if (Input.GetKeyDown(KeyCode.Tab)) SetTab((Tab)(((int)tab + 1) % 3));

        if (tab == Tab.Brigade && Input.GetKeyDown(KeyCode.Space)) DoTake();
        if (tab == Tab.Packs)
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow)) { packSel = (packSel + 2) % 3; dirty = true; }
            if (Input.GetKeyDown(KeyCode.RightArrow)) { packSel = (packSel + 1) % 3; dirty = true; }
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) StartOpen((WorkerPackType)packSel);
        }
    }

    // Esc и E: шаг назад (шансы → пропуск анимации → закрыть результат → закрыть окно)
    void Back()
    {
        if (oddsOpen) { CloseOdds(); return; }
        if (phase == Phase.Table) { table.Back(); return; }
        if (phase == Phase.Anim) { SkipAnim(); return; }
        if (phase == Phase.Result) { CloseResult(); return; }
        Close();
    }

    void OnCloseButton() { Back(); }

    void SetTab(Tab t)
    {
        tab = t;
        for (int i = 0; i < 3; i++) pages[i].gameObject.SetActive(i == (int)t);
        Refresh();
    }

    // ============================================================ обновление
    void Refresh()
    {
        if (!built) return;
        dirty = false;
        refreshAt = Time.unscaledTime + 0.25f;
        var m = WorkerManager.Instance;
        if (m == null) return;

        var inv = PlayerInventory.Instance;
        coinsText.text = (inv != null ? inv.coinsCount : 0).ToString();

        for (int i = 0; i < 3; i++) Restyle(tabBtns[i], i == (int)tab ? UiBtn.Amber : UiBtn.Gray);

        switch (tab)
        {
            case Tab.Brigade: RefreshBrigade(m); break;
            case Tab.Packs: RefreshPacks(m); break;
            default: RefreshCollection(m); break;
        }
        if (oddsOpen) RefreshOdds(m);
        if (phase == Phase.Anim || phase == Phase.Result) RefreshResultButtons(m);
    }

}
