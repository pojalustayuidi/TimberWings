// Assets/_Project/Scripts/UI/WorkersWindow.cs (НЕ в Editor)
// Окно «Лагерь» (дизайн «Система рабочих», задача 5). Стиль UiKit, всё строится из кода (на сцене ничего настраивать не нужно,
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

public class WorkersWindow : MonoBehaviour
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

    static readonly Color Soft = new Color(0.72f, 0.78f, 0.88f, 1f);
    static readonly Color Dim = new Color(0.50f, 0.56f, 0.66f, 1f);
    static readonly Color WoodText = new Color32(0x2A, 0x16, 0x08, 255);
    static readonly Color Warn = new Color32(0xF2, 0xD4, 0x5C, 255);

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

    void OnDestroy() { if (Instance == this) Instance = null; }

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
        if (FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
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
        if (shakeT <= 0f) panel.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, UiDraw.EaseOutCubic(openK));

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

    // ------------------------------------------------------------ вкладка «Бригада»
    void RefreshBrigade(WorkerManager m)
    {
        slotsLabel.text = "Слоты " + m.WorkingCount + "/" + m.Slots + "     доход Приказчиков " + WorkerUi.Inv(m.TotalIncome, "0.00") + " монет/с";

        // выбранный рабочий: если пропал, берём первого в слоте, иначе первого из ростера
        if (m.Find(selId) == null)
        {
            selId = null;
            for (int i = 0; i < m.Slots && selId == null; i++) { var w = m.SlotAt(i); if (w != null) selId = w.id; }
            if (selId == null && m.Roster.Count > 0) selId = m.Roster[0].id;
        }

        for (int i = 0; i < m.Slots; i++)
        {
            var w = m.SlotAt(i);
            var card = slotCards[i];
            card.rt.gameObject.SetActive(w != null);
            slotEmpty[i].gameObject.SetActive(w == null);
            if (w != null) card.Set(w.def, w, false, w.id == selId);
            else
            {
                var sel = m.Find(selId);
                bool canPlace = sel != null && m.Sim.SlotOf(sel) < 0;
                slotEmptyText[i].text = canPlace ? "Свободный слот\nнажми, чтобы поставить" : "Свободный слот";
            }
        }

        // запас: рабочие, которых нет в слотах
        reserveIds.Clear();
        for (int i = 0; i < m.Roster.Count; i++) if (m.Sim.SlotOf(m.Roster[i]) < 0) reserveIds.Add(m.Roster[i].id);
        reserveLabel.text = "Запас (" + reserveIds.Count + ")" + (reserveIds.Count == 0 ? "   все рабочие стоят в слотах" : "");
        while (reserveCards.Count < reserveIds.Count) reserveCards.Add(MakeReserveCard(reserveCards.Count));
        for (int i = 0; i < reserveCards.Count; i++)
        {
            bool on = i < reserveIds.Count;
            reserveCards[i].rt.gameObject.SetActive(on);
            if (!on) continue;
            var w = m.Find(reserveIds[i]);
            if (w == null) continue;
            reserveCards[i].Set(w.def, w, false, w.id == selId);
            TL(reserveCards[i].rt, i * 120f, 4f, 112f, 150f);
        }
        reserveContent.sizeDelta = new Vector2(Mathf.Max(800f, reserveIds.Count * 120f), 156f);
        // Wheel navigation for large reserves (100+ workers), while keeping the compact horizontal strip.
        var reserveWheel = reserveContent.GetComponent<WorkerReserveWheel>();
        if (reserveWheel == null) reserveWheel = reserveContent.gameObject.AddComponent<WorkerReserveWheel>();
        reserveWheel.Target = reserveContent;

        RefreshDetail(m);
        RefreshStock(m);
    }

    WorkerCardView MakeReserveCard(int index)
    {
        var c = WorkerCardView.Create(reserveContent, 112f, 150f, "Reserve" + index);
        c.showRole = false;
        int idx = index;
        AddClick(c.frame, () => { if (idx < reserveIds.Count) { selId = reserveIds[idx]; dirty = true; } });
        return c;
    }

    void RefreshDetail(WorkerManager m)
    {
        var w = m.Find(selId);
        detailBody.SetActive(w != null);
        detailEmpty.SetActive(w == null);
        if (w == null) return;

        var d = w.def;
        detailCard.Set(d, w, false);
        dName.text = w.DisplayName;
        dAge.text = d != null ? "Возраст " + d.age : "";
        dGem.color = WorkerUi.RarityAccent(w.rarity);
        dRarity.text = WorkerText.Rarity(w.rarity);
        dRarity.color = WorkerUi.RarityAccent(w.rarity);
        for (int i = 0; i < 5; i++) dStars[i].gameObject.SetActive(i <= (int)w.rarity);
        dRoleIcon.sprite = UiKit.Icon(WorkerUi.RoleIcon(w.role));
        dRole.text = WorkerText.Role(w.role);

        if (w.IsMax)
        {
            dLevel.text = "Уровень " + w.level + " (максимум, «Мастер» ×" + WorkerUi.Inv(WorkerBalance.MasterBonus, "0.0") + ")";
            dXpFill.fillAmount = 1f;
            dXp.text = "МАКС";
        }
        else
        {
            double into = w.totalXp - WorkerBalance.TotalXpForLevel(w.level);
            int need = WorkerBalance.XpToNext(w.level);
            dLevel.text = "Уровень " + w.level + " из " + WorkerBalance.MaxLevel;
            dXpFill.fillAmount = Mathf.Clamp01((float)(into / Mathf.Max(1, need)));
            dXp.text = (int)into + " / " + need + " XP";
        }

        var s = m.Sim;
        string prod;
        switch (w.role)
        {
            case WorkerRole.Clerk: prod = WorkerUi.Inv(s.ClerkRate(w), "0.00") + " монет/с"; break;
            case WorkerRole.Lumberjack: prod = WorkerUi.Inv(s.LumberRate(w), "0.00") + " бревен/с"; break;
            default: prod = "1 доска за " + WorkerUi.Inv(s.SawTime(w), "0.0") + " с"; break;
        }
        dProd.text = "Выработка: <b>" + prod + "</b>   ×" + WorkerUi.Inv(WorkerBalance.RarityMultiplier(w.rarity), "0.0#") + " редкость, ×" + WorkerUi.Inv(WorkerBalance.LevelMult(w.level), "0.00") + " уровень";

        int slot = s.SlotOf(w);
        string status; bool working = false;
        if (slot < 0) status = "В запасе: не работает";
        else if (w.role == WorkerRole.Lumberjack && m.Logs >= m.LogCap) status = "Стоит: склад бревен полон";
        else if (w.role == WorkerRole.Sawyer && m.Planks >= m.PlankCap) status = "Стоит: склад досок полон";
        else if (w.role == WorkerRole.Sawyer && !w.sawActive && m.Logs < WorkerBalance.SawyerLogsPerPlank) status = "Стоит: на складе нет бревен";
        else { status = "Работает в слоте " + (slot + 1) + ", копит опыт"; working = true; }
        dStatus.text = status;
        dStatusIcon.sprite = UiKit.Icon(working ? UiIcon.Check : UiIcon.Clock);
        dStatus.color = working ? UiKit.Good : Warn;

        // четыре перка роли: открытые и закрытые (закрытый = замок и серый текст, не только цвет)
        var perks = WorkerText.PerksOf(w.role);
        for (int i = 0; i < 4; i++)
        {
            bool has = w.Has(perks[i]);
            perkIcon[i].sprite = UiKit.Icon(has ? UiIcon.Check : UiIcon.Lock);
            perkIcon[i].color = has ? Color.white : new Color(1f, 1f, 1f, 0.45f);
            perkName[i].text = WorkerText.Perk(perks[i]);
            perkName[i].color = has ? Color.white : Dim;
            perkDesc[i].text = WorkerText.PerkDesc(perks[i]);
            perkDesc[i].color = has ? UiKit.Good : Dim;
        }
        bool sig = w.rarity == WorkerRarity.Mythic;
        perkIcon[4].gameObject.SetActive(sig);
        perkName[4].gameObject.SetActive(sig);
        perkDesc[4].gameObject.SetActive(sig);
        if (sig)
        {
            perkIcon[4].sprite = UiKit.Icon(UiIcon.Star);
            perkName[4].text = "Фирменный";
            perkName[4].color = WorkerUi.RarityAccent(WorkerRarity.Mythic);
            perkDesc[4].text = WorkerText.Signature(w.role);
            perkDesc[4].color = Soft;
        }

        bool inSlot = slot >= 0;
        SetBtn(dAction, true, inSlot ? "Убрать из слота" : "Поставить в лагерь");
        dAction.b.onClick.RemoveAllListeners();
        string id = w.id;
        dAction.b.onClick.AddListener(() => ToggleSlot(id));
    }

    void ToggleSlot(string id)
    {
        var m = WorkerManager.Instance;
        if (m == null) return;
        var w = m.Find(id);
        if (w == null) return;
        int slot = m.Sim.SlotOf(w);
        if (slot >= 0) { m.Unassign(slot); Say("Убрано в запас."); }
        else Say(AssignText(m.AutoAssign(id), "Поставлен в лагерь."));
        dirty = true;
    }

    void ClickSlot(int i)
    {
        var m = WorkerManager.Instance;
        if (m == null) return;
        var w = m.SlotAt(i);
        if (w != null) { selId = w.id; dirty = true; return; }
        var sel = m.Find(selId);
        if (sel == null || m.Sim.SlotOf(sel) >= 0) return;
        Say(AssignText(m.Assign(selId, i), "Поставлен в слот " + (i + 1) + "."));
        dirty = true;
    }

    static string AssignText(AssignResult r, string ok)
    {
        switch (r)
        {
            case AssignResult.Ok: return ok;
            case AssignResult.RoleFull: return "Не больше " + WorkerBalance.MaxPerRole + " рабочих одной роли.";
            case AssignResult.NoFreeSlot: return "Все слоты заняты: убери кого-нибудь.";
            default: return "Не получилось.";
        }
    }

    void Say(string s) { message = s; messageUntil = Time.unscaledTime + 3.5f; }

    // ------------------------------------------------------------ склад лагеря
    void RefreshStock(WorkerManager m)
    {
        logFill.fillAmount = m.LogCap > 0 ? Mathf.Clamp01(m.Logs / (float)m.LogCap) : 0f;
        plankFill.fillAmount = m.PlankCap > 0 ? Mathf.Clamp01(m.Planks / (float)m.PlankCap) : 0f;
        logText.text = m.Logs + "/" + m.LogCap + (m.Logs >= m.LogCap ? " полон" : "");
        plankText.text = m.Planks + "/" + m.PlankCap + (m.Planks >= m.PlankCap ? " полон" : "");
        logText.color = m.Logs >= m.LogCap ? Warn : Color.white;
        plankText.color = m.Planks >= m.PlankCap ? Warn : Color.white;

        var take = m.PreviewTake();
        var sell = m.PreviewSell();
        string pre = "";
        if (take.Any) pre = "Заберёшь: бревна " + take.logs + ", доски " + take.planks;
        else if (m.Logs > 0) pre = "В рюкзаке нет места для бревен";
        else pre = "На складе пусто";
        if (sell.Any) pre += "   ·   продашь: +" + sell.coins + " монет";
        stockPreview.text = pre;
        SetBtn(takeBtn, take.Any, null);
        SetBtn(sellBtn, sell.Any, null);

        bool showMsg = message.Length > 0 && Time.unscaledTime < messageUntil;
        stockMsg.text = showMsg ? message : "";
    }

    void DoTake()
    {
        var m = WorkerManager.Instance;
        if (m == null) return;
        var r = m.TakeAll();
        if (r.Any)
        {
            string s = "Забрано: бревна " + r.logs + ", доски " + r.planks;
            if (m.Logs > 0) s += " (рюкзак полон)";
            Say(s);
            Tick();
        }
        else Say(m.Logs > 0 ? "В рюкзаке нет места." : "На складе пусто.");
        dirty = true;
    }

    void DoSell()
    {
        var m = WorkerManager.Instance;
        if (m == null) return;
        var r = m.SellAll();
        Say(r.Any ? "Продано за " + r.coins + " монет" : "Продавать нечего.");
        if (r.Any) Tick();
        dirty = true;
    }

    static void Tick() { if (SfxManager.Instance != null) SfxManager.Play("ui"); }

    // ------------------------------------------------------------ вкладка «Паки»
    void RefreshPacks(WorkerManager m)
    {
        for (int t = 0; t < 3; t++)
        {
            var type = (WorkerPackType)t;
            var u = packUi[t];
            var packs = m.Packs;
            u.hl.color = t == packSel ? UiKit.BrassHi : new Color(1f, 1f, 1f, 0f);

            bool free = type == WorkerPackType.Artel && packs.FreePending;
            int price = m.PackPrice(type);
            bool unlocked = m.IsPackUnlocked(type);
            u.ribbon.gameObject.SetActive(free);

            if (!unlocked)
            {
                u.priceIcon.sprite = UiKit.Icon(UiIcon.Lock);
                u.priceText.text = "С " + WorkerBalance.PackLevel(type) + " уровня (сейчас " + PlayerStats.Level + ")";
                u.priceText.color = Warn;
            }
            else
            {
                u.priceIcon.sprite = UiKit.Icon(UiIcon.Coin);
                u.priceText.text = free ? "Бесплатно!" : price.ToString();
                u.priceText.color = free ? UiKit.Good : Color.white;
            }

            // шансы видны всегда, до покупки
            var odds = WorkerBalance.OddsOf(type);
            for (int r = 0; r < 5; r++)
            {
                bool zero = odds[r] <= 0f;
                u.oddName[r].color = zero ? Dim : Color.white;
                u.oddPct[r].text = WorkerUi.OddsText(odds[r]);
                u.oddPct[r].color = zero ? Dim : Color.white;
                u.oddIcon[r].color = zero ? new Color(1f, 1f, 1f, 0.25f) : WorkerUi.RarityAccent((WorkerRarity)r);
                WorkerUi.SetBar(u.oddFill[r], odds[r] <= 0f ? 0f : Mathf.Max(0.03f, odds[r] / 100f));
            }

            // счётчики гарантий
            u.guar[0].text = "Редкий или выше: " + UntilText(packs.OpensUntilRare(type));
            bool leg = WorkerBalance.HasLegendaryGuarantee(type);
            u.guar[1].gameObject.SetActive(leg);
            if (leg) u.guar[1].text = "Легендарный или выше: " + UntilText(packs.OpensUntilLegendary(type));
            bool myth = WorkerBalance.HasMythicGuarantee(type);
            u.guar[2].gameObject.SetActive(myth);
            u.mythBack.gameObject.SetActive(myth);
            if (myth)
            {
                u.guar[2].text = "до мифика: " + packs.MythicProgress(type) + "/" + WorkerBalance.GuaranteeMythicEvery;
                WorkerUi.SetBar(u.mythFill, packs.MythicProgress(type) / (float)WorkerBalance.GuaranteeMythicEvery);
            }

            PackOpenResult why;
            bool can = m.CanOpenPack(type, out why);
            u.reason.text = can ? "" : WhyText(why, type, price);
            SetBtn(u.open, can, free ? "Открыть бесплатно" : "Открыть");
        }
    }

    static string UntilText(int n)
    {
        if (n <= 1) return "гарантия в этом паке";
        return "через " + n + " " + UiDraw.Plural(n, "пак", "пака", "паков");
    }

    static string WhyText(PackOpenResult why, WorkerPackType t, int price)
    {
        switch (why)
        {
            case PackOpenResult.Locked: return "Откроется на " + WorkerBalance.PackLevel(t) + " уровне";
            case PackOpenResult.NoCoins: return "Не хватает монет: нужно " + price;
            case PackOpenResult.NoDatabase: return "Не назначена база рабочих";
            default: return "Нет игрока";
        }
    }

    // ------------------------------------------------------------ вкладка «Коллекция»
    void RefreshCollection(WorkerManager m)
    {
        if (!collBuilt) BuildCollection(m);
        if (!collBuilt) return;

        int have = 0;
        var perRoleHave = new int[3]; var perRoleAll = new int[3];
        for (int i = 0; i < collDefs.Count; i++)
        {
            var d = collDefs[i];
            var st = m.Find(d.id);
            bool owned = st != null;
            if (owned) { have++; perRoleHave[(int)d.role]++; }
            perRoleAll[(int)d.role]++;
            collCards[i].Set(d, st, !owned, d == selDef);
        }
        collCount.text = "Собрано " + have + "/" + collDefs.Count;
        for (int r = 0; r < 3; r++) collRoleCount[r].text = perRoleHave[r] + "/" + perRoleAll[r];

        if (selDef == null && collDefs.Count > 0) selDef = collDefs[0];
        cdBody.SetActive(selDef != null);
        if (selDef == null) return;

        var sd = selDef;
        var ss = m.Find(sd.id);
        bool own = ss != null;
        cdCard.Set(sd, ss, !own);
        Color acc = WorkerUi.RarityAccent(sd.rarity);
        cdGem.color = acc;
        for (int i = 0; i < 5; i++) cdStars[i].gameObject.SetActive(i <= (int)sd.rarity);
        cdRarity.text = WorkerText.Rarity(sd.rarity) + " · " + WorkerText.Role(sd.role);
        cdRarity.color = acc;

        if (own)
        {
            cdName.text = sd.FullName;
            cdLine.text = "Возраст " + sd.age;
            int slot = m.Sim.SlotOf(ss);
            cdState.text = "Уровень " + ss.level + (ss.IsMax ? " (максимум)" : "") + "  ·  " + (slot >= 0 ? "работает в слоте " + (slot + 1) : "в запасе");
            cdState.color = Color.white;

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < sd.perks.Count; i++)
                sb.Append("<b>").Append(WorkerText.Perk(sd.perks[i])).Append("</b>: ").Append(WorkerText.PerkDesc(sd.perks[i])).Append('\n');
            if (sd.HasSignature) sb.Append("<b>Фирменный</b>: ").Append(WorkerText.Signature(sd.role));
            if (sb.Length == 0) sb.Append("Перков нет, зато надёжный работник.");
            cdPerks.text = sb.ToString();
            cdBio.text = string.IsNullOrEmpty(sd.bio) ? "" : sd.bio;
        }
        else
        {
            cdName.text = "???";
            cdLine.text = "Ещё не найден";
            cdState.text = "Выпадает из: " + WorkerUi.PacksWith(sd.rarity);
            cdState.color = Warn;
            cdPerks.text = sd.perks.Count > 0 ? "Перков: " + sd.perks.Count + (sd.HasSignature ? " и фирменный" : "") : "Перков нет";
            cdBio.text = "Открой пак, чтобы узнать этого человека.";
        }
    }

    // ============================================================ открытие пака
    // Кнопка «Открыть»: раздаём три карты рубашкой вверх. Монеты спишутся, только когда игрок выберет карту (WorkerPackTable)
    void StartOpen(WorkerPackType type)
    {
        var m = WorkerManager.Instance;
        if (m == null || phase != Phase.None || table == null) return;
        var why = table.Begin(type, m);
        if (why != PackOpenResult.Ok) { Say(WhyText(why, type, m.PackPrice(type))); Refresh(); return; }
        phase = Phase.Table;
        Refresh();
    }

    void OnTableClosed()
    {
        phase = Phase.None;
        panel.anchoredPosition = Vector2.zero;
        dirty = true;
    }

    static readonly Vector2 CardPos = new Vector2(-300f, 30f);

    void FillResult(WorkerManager m)
    {
        var d = res.def;
        var st = res.worker;
        Color acc = WorkerUi.RarityAccent(res.rarity);
        resCard.Set(d, st, false);

        rTag.gameObject.SetActive(true);
        if (res.isNew) { rTag.color = WorkerUi.Bad; rTagText.text = "НОВЫЙ!"; }
        else { rTag.color = new Color32(0x3A, 0x4A, 0x62, 255); rTagText.text = "ДУБЛИКАТ"; }
        if (res.wasFree) rTagText.text += "  ·  ПОДАРОК";

        rName.text = d.FullName + ", " + d.age;
        rGem.color = acc;
        rRarity.text = WorkerText.Rarity(res.rarity);
        rRarity.color = acc;
        for (int i = 0; i < 5; i++) rStars[i].gameObject.SetActive(i <= (int)res.rarity);
        rRoleIcon.sprite = UiKit.Icon(WorkerUi.RoleIcon(d.role));
        rRole.text = WorkerText.Role(d.role) + " · " + WorkerText.Output(d.role);
        rLevel.text = "Уровень " + st.level + (st.IsMax ? " (максимум)" : "");

        var sb = new System.Text.StringBuilder();
        if (res.isNew)
        {
            int slot = m.Sim.SlotOf(st);
            if (slot >= 0) sb.Append("Встал в слот ").Append(slot + 1).Append(" и уже работает.");
            else if (m.Sim.CountInSlots(d.role) >= WorkerBalance.MaxPerRole) sb.Append("В запасе: в лагере уже ").Append(WorkerBalance.MaxPerRole).Append(" рабочих этой роли.");
            else sb.Append("В запасе: все слоты заняты.");
        }
        else if (res.coinsGained > 0) sb.Append("Уже на максимальном уровне: вместо опыта +").Append(res.coinsGained).Append(" монет.");
        else
        {
            sb.Append("Уже в лагере: +").Append(res.xpGained).Append(" XP.");
            if (res.levelsGained > 0) sb.Append(" Новый уровень: ").Append(st.level).Append("!");
        }
        if (res.GuaranteeTriggered) sb.Append("\nСработала гарантия: ").Append(GuaranteeName(res.guarantee)).Append(".");
        rLine.text = sb.ToString();

        var pk = new System.Text.StringBuilder();
        for (int i = 0; i < d.perks.Count; i++) { if (i > 0) pk.Append("  ·  "); pk.Append(WorkerText.Perk(d.perks[i])); }
        if (d.HasSignature) pk.Append("  ·  Фирменный");
        rPerks.text = pk.Length > 0 ? "Перки: " + pk : "Перков нет";
        rBio.text = string.IsNullOrEmpty(d.bio) ? "" : d.bio;
        RefreshResultButtons(m);
    }

    static string GuaranteeName(PackGuarantee g)
    {
        switch (g)
        {
            case PackGuarantee.Mythic: return "Мифический";
            case PackGuarantee.Legendary: return "Легендарный или выше";
            default: return "Редкий или выше";
        }
    }

    void RefreshResultButtons(WorkerManager m)
    {
        PackOpenResult why;
        bool can = m.CanOpenPack(resType, out why);
        int price = m.PackPrice(resType);
        SetBtn(againBtn, can && phase == Phase.Result, "Ещё: " + (price == 0 ? "бесплатно" : price + " монет"));
        SetBtn(doneBtn, phase == Phase.Result, "Готово");
    }

    void OpenAgain()
    {
        if (phase != Phase.Result) return;
        var m = WorkerManager.Instance;
        PackOpenResult why;
        if (m == null || !m.CanOpenPack(resType, out why)) return;
        phase = Phase.None;
        StartOpen(resType);
    }

    void CloseResult()
    {
        phase = Phase.None;
        openRoot.SetActive(false);
        panel.anchoredPosition = Vector2.zero;
        dirty = true;
    }

    void SkipAnim()
    {
        if (phase != Phase.Anim) return;
        animT = AnimDur;
        FinishAnim();
    }

    void TickOpening(float dt)
    {
        if (phase == Phase.None) { return; }
        if (phase == Phase.Table) { table.Tick(dt); return; }
        if (phase == Phase.Anim)
        {
            animT += dt;
            if (animT >= AnimDur) FinishAnim(); else AnimFrame(animT);
        }
        else
        {
            if (raysRt.gameObject.activeSelf) raysRt.localRotation = Quaternion.Euler(0f, 0f, -Time.unscaledTime * 18f);
        }
        TickSparks(dt);
    }

    // 0–1.8 тряска и искры → 1.8–2.05 вспышка → 2.05–2.75 карточка выезжает → 2.8–3.3 появляется текст → 3.4 конец
    void AnimFrame(float t)
    {
        const float charge = 1.8f, flashEnd = 2.05f, revealEnd = 2.75f;
        Color pc = WorkerUi.PackColor(resType);
        Color acc = WorkerUi.RarityAccent(res.rarity);

        if (t < charge)
        {
            float k = t / charge;
            artRt.anchoredPosition = CardPos + new Vector2(Mathf.Sin(t * 55f) * (2f + 11f * k), Mathf.Sin(t * 41f) * 3f * k);
            artRt.localScale = Vector3.one * (1f + 0.2f * k);
            glowRt.localScale = Vector3.one * (0.7f + 0.6f * k);
            openGlow.color = new Color(pc.r, pc.g, pc.b, 0.15f + 0.45f * k);
            if (Random.value < 0.35f + 0.5f * k)
            {
                float a = Random.value * Mathf.PI * 2f;
                Vector2 p = CardPos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(90f, 150f);
                Vector2 v = (CardPos - p).normalized * Random.Range(60f, 120f);
                SpawnSpark(p, v, Color.Lerp(pc, Color.white, 0.4f), Random.Range(0.5f, 0.9f), Random.Range(10f, 20f));
            }
            return;
        }

        if (t < flashEnd)
        {
            float k = (t - charge) / (flashEnd - charge);
            flash.color = new Color(acc.r, acc.g, acc.b, k);
            artRt.localScale = Vector3.one * (1.2f + 0.3f * k);
            return;
        }

        // показ карточки
        if (artRt.gameObject.activeSelf) { openArt.gameObject.SetActive(false); }
        if (!burstDone) Burst(true);
        float fk = Mathf.Clamp01((t - flashEnd) / 0.45f);
        flash.color = new Color(acc.r, acc.g, acc.b, 1f - fk);

        if (!resCard.rt.gameObject.activeSelf) resCard.rt.gameObject.SetActive(true);
        float rk = Mathf.Clamp01((t - flashEnd) / (revealEnd - flashEnd));
        float s = Mathf.Lerp(0.55f, 1f, UiDraw.EaseOutBack(rk));
        float flip = Mathf.Clamp01(rk / 0.6f);                         // переворот: ширина от 0 до полной
        resCard.rt.localScale = new Vector3(s * UiDraw.EaseOutCubic(flip), s, 1f);

        if (res.rarity >= WorkerRarity.Uncommon)
        {
            raysRt.gameObject.SetActive(true);
            raysRt.localRotation = Quaternion.Euler(0f, 0f, -t * 18f);
            openRays.color = new Color(acc.r, acc.g, acc.b, 0.55f * rk);
        }
        openGlow.color = new Color(acc.r, acc.g, acc.b, 0.35f * rk);

        float ik = Mathf.Clamp01((t - 2.8f) / 0.5f);
        infoCg.alpha = ik;
        if (ik > 0f) waitText.gameObject.SetActive(false);
    }

    // карточка в конечном состоянии (анимация закончилась сама или её пропустили)
    void FinishAnim()
    {
        phase = Phase.Result;
        Color acc = WorkerUi.RarityAccent(res.rarity);
        openArt.gameObject.SetActive(false);
        flash.color = new Color(1f, 1f, 1f, 0f);
        resCard.rt.gameObject.SetActive(true);
        resCard.rt.localScale = Vector3.one;
        openGlow.color = new Color(acc.r, acc.g, acc.b, 0.35f);
        glowRt.localScale = Vector3.one;
        bool rays = res.rarity >= WorkerRarity.Uncommon;
        raysRt.gameObject.SetActive(rays);
        openRays.color = new Color(acc.r, acc.g, acc.b, 0.55f);
        waitText.gameObject.SetActive(false);
        infoCg.alpha = 1f;
        infoCg.interactable = true; infoCg.blocksRaycasts = true;
        openHint.text = "E или Esc: закрыть   ·   Enter: открыть ещё";
        if (!burstDone) Burst(false);
        var m = WorkerManager.Instance;
        if (m != null) RefreshResultButtons(m);
    }

    // взрыв искр в момент показа карточки; тряска окна у Редкого и выше (если анимацию не пропустили)
    void Burst(bool withShake)
    {
        burstDone = true;
        Color acc = WorkerUi.RarityAccent(res.rarity);
        int n = 10 + 6 * (int)res.rarity;
        for (int i = 0; i < n; i++)
        {
            float a = Random.value * Mathf.PI * 2f, sp = Random.Range(120f, 380f);
            SpawnSpark(CardPos, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * sp, Color.Lerp(acc, Color.white, Random.value * 0.5f), Random.Range(0.6f, 1.1f), Random.Range(12f, 26f));
        }
        if (withShake && res.rarity >= WorkerRarity.Rare) shakeT = 0.45f;
        if (SfxManager.Instance != null) SfxManager.Play("ui", 0.8f, 0.7f + 0.15f * (int)res.rarity);
    }

    void SpawnSpark(Vector2 p, Vector2 v, Color c, float life, float size)
    {
        for (int i = 0; i < sparks.Length; i++)
        {
            var s = sparks[i];
            if (s.live) continue;
            s.live = true; s.p = p; s.v = v; s.life = s.max = life; s.size = size;
            s.img.color = c;
            s.img.gameObject.SetActive(true);
            s.rt.anchoredPosition = p;
            s.rt.sizeDelta = new Vector2(size, size);
            return;
        }
    }

    void TickSparks(float dt)
    {
        for (int i = 0; i < sparks.Length; i++)
        {
            var s = sparks[i];
            if (!s.live) continue;
            s.life -= dt;
            if (s.life <= 0f) { s.live = false; s.img.gameObject.SetActive(false); continue; }
            s.p += s.v * dt;
            s.v *= 1f - Mathf.Min(1f, 1.6f * dt);
            s.rt.anchoredPosition = s.p;
            var c = s.img.color; c.a = Mathf.Clamp01(s.life / s.max);
            s.img.color = c;
        }
    }

    // ============================================================ окно шансов
    void OpenOdds(WorkerPackType t)
    {
        oddsType = t;
        oddsOpen = true;
        oddsGo.SetActive(true);
        var m = WorkerManager.Instance;
        if (m != null) RefreshOdds(m);
    }

    void CloseOdds() { oddsOpen = false; oddsGo.SetActive(false); }

    void RefreshOdds(WorkerManager m)
    {
        var t = oddsType;
        var packs = m.Packs;
        oTitle.text = "Шансы: " + WorkerText.Pack(t);
        oTitle.color = WorkerUi.PackColor(t);
        var odds = WorkerBalance.OddsOf(t);
        for (int r = 0; r < 5; r++)
        {
            bool zero = odds[r] <= 0f;
            oGem[r].color = zero ? new Color(1f, 1f, 1f, 0.25f) : WorkerUi.RarityAccent((WorkerRarity)r);
            oName[r].color = zero ? Dim : Color.white;
            oPct[r].text = WorkerUi.OddsText(odds[r]);
            oPct[r].color = zero ? Dim : Color.white;
            WorkerUi.SetBar(oFill[r], zero ? 0f : Mathf.Max(0.02f, odds[r] / 100f));
        }

        oGuarLine[0].text = "Редкий или выше: каждые " + WorkerBalance.GuaranteeRareEvery + " паков (" + UntilText(packs.OpensUntilRare(t)) + ")";
        bool leg = WorkerBalance.HasLegendaryGuarantee(t);
        oGuarLine[1].text = leg ? "Легендарный или выше: каждые " + WorkerBalance.GuaranteeLegendaryEvery + " паков (" + UntilText(packs.OpensUntilLegendary(t)) + ")"
                                : "Легендарный или выше: гарантии нет";
        oGuarLine[1].color = leg ? Soft : Dim;
        bool myth = WorkerBalance.HasMythicGuarantee(t);
        oGuarLine[2].text = myth ? "Мифический: каждые " + WorkerBalance.GuaranteeMythicEvery + " паков (до мифика: " + packs.MythicProgress(t) + "/" + WorkerBalance.GuaranteeMythicEvery + ")"
                                 : "Мифический: гарантии нет";
        oGuarLine[2].color = myth ? Soft : Dim;
        oMythFill.parent.gameObject.SetActive(myth);
        if (myth) WorkerUi.SetBar(oMythFill, packs.MythicProgress(t) / (float)WorkerBalance.GuaranteeMythicEvery);
    }

    // ============================================================ помощники кнопок
    Btn MakeBtn(Transform parent, string label, UiBtn style, float x, float y, float w, float h, float fs, UnityEngine.Events.UnityAction a)
    {
        var b = UiKit.MakeButton(parent, label, style, new Vector2(w, h), fs, 1f);
        TL((RectTransform)b.transform, x, y, w, h);
        b.onClick.AddListener(a);
        var t = b.GetComponentInChildren<TMP_Text>();
        AutoSize(t, fs * 0.55f, fs);
        return new Btn { b = b, t = t, style = style, fg = t.color };
    }

    static void SetBtn(Btn b, bool on, string text)
    {
        if (b == null) return;
        b.b.interactable = on;
        if (text != null) b.t.text = text;
        b.t.color = on ? b.fg : Dim;
    }

    static void Restyle(Btn b, UiBtn style)
    {
        if (b.style == style) return;
        b.style = style;
        var img = (Image)b.b.targetGraphic;
        img.sprite = UiKit.BtnSprite(style);
        var ss = b.b.spriteState;
        ss.highlightedSprite = UiKit.BtnSprite(style, UiBtnState.Hover);
        ss.pressedSprite = UiKit.BtnSprite(style, UiBtnState.Pressed);
        ss.selectedSprite = UiKit.BtnSprite(style, UiBtnState.Normal);
        b.b.spriteState = ss;
        b.fg = (style == UiBtn.Amber || style == UiBtn.Yellow) ? UiKit.Navy : Color.white;
        b.t.color = b.fg;
    }

    // подпись с переносом строк и автоподбором размера
    static TMP_Text Wrap(Transform p, string text, float size, Color c, TextAlignmentOptions al, bool bold, float x, float y, float w, float h)
    {
        var t = UiKit.MakeLabel(p, text, size, c, al, bold);
        TL(t.rectTransform, x, y, w, h);
        t.enableAutoSizing = true;
        t.fontSizeMin = Mathf.Max(10f, size * 0.55f);
        t.fontSizeMax = size;
        t.enableWordWrapping = true;
        return t;
    }

    Image Icon(Transform p, UiIcon icon, float cx, float cy, float size)
    {
        var im = UiKit.MakeIcon(p, icon, size, "Icon");
        TLC(im.rectTransform, cx, cy, size, size);
        im.raycastTarget = false;
        return im;
    }

    // ============================================================ построение окна
    void Build()
    {
        if (built) return;

        var cgo = new GameObject("WorkersCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cgo.layer = 5;
        cgo.transform.SetParent(transform, false);
        var canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 31;
        var sc = cgo.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        sc.matchWidthOrHeight = 0.5f;

        // затемнение гасит клики по миру, но окно НЕ закрывает (промах мимо окна не должен его сбрасывать)
        var ov = UiKit.MakeImage(cgo.transform, "Overlay", null, false);
        ov.color = new Color(0f, 0f, 0f, 0.6f);
        Stretch(ov.rectTransform);
        ov.raycastTarget = true;
        overlay = ov.gameObject.AddComponent<CanvasGroup>();

        var win = UiKit.MakePanel(ov.transform, new Vector2(WinW, WinH), true, BS, "Window");
        win.raycastTarget = true;
        panel = win.rectTransform;
        Anch(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(WinW, WinH));
        BuildHeader();

        var content = NewRT("Content", panel);
        content.anchorMin = Vector2.zero; content.anchorMax = Vector2.one;
        content.offsetMin = new Vector2(26f * BS, 26f * BS);
        content.offsetMax = new Vector2(-26f * BS, -36f * BS);
        var innerImg = UiKit.MakePanel(content, new Vector2(100, 100), false, 0.6f, "Inner");
        Stretch(innerImg.rectTransform);
        inner = innerImg.rectTransform;

        // вкладки
        string[] names = { "Бригада", "Паки", "Коллекция" };
        UiIcon[] icons = { UiIcon.Person, UiIcon.Box, UiIcon.Book };
        for (int i = 0; i < 3; i++)
        {
            int ti = i;
            tabBtns[i] = MakeBtn(inner, "     " + names[i] + " [" + (i + 1) + "]", i == 0 ? UiBtn.Amber : UiBtn.Gray, PageX + i * 262f, 14f, 252f, 52f, 26f, () => { if (phase == Phase.None && !oddsOpen) SetTab((Tab)ti); });
            var ic = UiKit.MakeIcon(tabBtns[i].b.transform, icons[i], 30f, "TabIcon");
            Anch(ic.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(34f, 2f), new Vector2(30f, 30f));
            ic.raycastTarget = false;
        }

        for (int i = 0; i < 3; i++)
        {
            pages[i] = NewRT("Page" + i, inner);
            TL(pages[i], PageX, PageY, PageW, PageH);
        }
        BuildBrigade(pages[0]);
        BuildPacks(pages[1]);
        BuildCollectionShell(pages[2]);
        BuildOpenOverlay();
        table = new WorkerPackTable(inner);
        table.onClosed = OnTableClosed;
        table.onPaid = MarkDirty;
        table.onShake = () => { shakeT = 0.45f; };
        BuildOddsOverlay();

        ov.gameObject.SetActive(false);
        built = true;
        open = false;
        SetTab(Tab.Brigade);
    }

    void BuildHeader()
    {
        var coin = UiKit.MakeIcon(panel, UiIcon.Coin, 36f, "CoinIcon");
        TLC(coin.rectTransform, 64, 27, 36, 36);
        coinsText = UiKit.MakeLabel(panel, "0", 30f, WoodText, TextAlignmentOptions.Left, true);
        TL(coinsText.rectTransform, 90, 8, 200, 38);

        titleText = UiKit.MakeLabel(panel, "ЛАГЕРЬ", 34f, WoodText, TextAlignmentOptions.Center, true);
        Anch(titleText.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -8), new Vector2(520, 40));

        var close = UiKit.MakeButton(panel, "", UiBtn.Amber, new Vector2(64, 48), 20f, 1f);
        Anch(close.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-34, -5), new Vector2(64, 48));
        close.onClick.AddListener(OnCloseButton);
        var x = UiKit.MakeIcon(close.transform, UiIcon.Cross, 30f, "X");
        x.rectTransform.anchoredPosition = new Vector2(0, 2);
    }

    void BuildPackCaseArt(Transform parent, Color accent, float x, float y)
    {
        var glow = UiKit.MakeImage(parent, "CaseHalo", PetUi.GlowSprite, false);
        glow.color = new Color(accent.r, accent.g, accent.b, 0.22f);
        glow.raycastTarget = false;
        TLC(glow.rectTransform, x, y, 228, 220);

        var shadow = UiKit.MakeImage(parent, "CaseShadow", UiKit.Slot, false);
        shadow.color = new Color(0.015f, 0.012f, 0.01f, 0.85f);
        shadow.raycastTarget = false;
        TLC(shadow.rectTransform, x + 3, y - 13, 124, 96);

        var body = UiKit.MakeImage(parent, "CaseWood", UiKit.Slot, false);
        body.color = new Color(0.24f, 0.13f, 0.065f, 1f);
        body.raycastTarget = false;
        TLC(body.rectTransform, x, y, 118, 94);

        var lid = UiKit.MakeImage(parent, "CaseLid", UiKit.Slot, false);
        lid.color = new Color(0.38f, 0.23f, 0.10f, 1f);
        lid.raycastTarget = false;
        TLC(lid.rectTransform, x, y + 42, 132, 30);

        for (int i = -1; i <= 1; i++)
        {
            var band = UiKit.MakeImage(parent, "BrassBand" + i, UiKit.Slot, false);
            band.color = Color.Lerp(new Color(0.55f, 0.32f, 0.10f), accent, 0.48f);
            band.raycastTarget = false;
            TLC(band.rectTransform, x + i * 38f, y - 1, 7, 83);
        }

        var clasp = UiKit.MakeImage(parent, "CaseClasp", UiKit.Slot, false);
        clasp.color = accent;
        clasp.raycastTarget = false;
        TLC(clasp.rectTransform, x, y + 7, 25, 30);

        var gem = UiKit.MakeIcon(parent, UiIcon.Gem, 34f, "CaseRarityGem");
        gem.color = accent;
        gem.raycastTarget = false;
        TLC(gem.rectTransform, x, y + 9, 32, 32);

        var shine = UiKit.MakeImage(parent, "CaseGlint", UiKit.Slot, false);
        shine.color = new Color(1f, 0.86f, 0.57f, 0.72f);
        shine.raycastTarget = false;
        TLC(shine.rectTransform, x - 37, y + 24, 3, 47);
    }

    // ------------------------------------------------------------ Бригада
    void BuildBrigade(RectTransform page)
    {
        slotsLabel = Lbl(page, "", 24f, Color.white, TextAlignmentOptions.Left, true, 0, 0, 800, 28);

        // 6 слотов: 3 в ряд
        for (int i = 0; i < CampSimulation.Slots; i++)
        {
            int si = i;
            float x = (i % 3) * 274f, y = 32f + (i / 3) * 250f;

            var card = WorkerCardView.Create(page, 252f, 240f, "Slot" + i);
            TL(card.rt, x, y, 252f, 240f);
            AddClick(card.frame, () => ClickSlot(si));
            slotCards[i] = card;

            var empty = UiKit.MakeImage(page, "EmptySlot" + i, UiKit.Slot, true, 1f);
            empty.color = new Color(0.7f, 0.75f, 0.85f, 0.55f);
            TL(empty.rectTransform, x, y, 252f, 240f);
            AddClick(empty, () => ClickSlot(si));
            var plus = UiKit.MakeIcon(empty.transform, UiIcon.Person, 64f, "Ghost");
            Anch(plus.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(64, 64));
            plus.color = new Color(1f, 1f, 1f, 0.3f);
            plus.raycastTarget = false;
            var tx = UiKit.MakeLabel(empty.transform, "Свободный слот", 22f, Dim, TextAlignmentOptions.Center, false);
            Anch(tx.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 14), new Vector2(230, 80));
            tx.enableWordWrapping = true;
            slotEmpty[i] = empty; slotEmptyText[i] = tx;
        }

        reserveLabel = Lbl(page, "Запас", 24f, Color.white, TextAlignmentOptions.Left, true, 0, 528, 800, 28);
        var vp = NewRT("ReserveView", page);
        TL(vp, 0, 558, 800, 157);
        var vpImg = vp.gameObject.AddComponent<Image>();
        vpImg.color = new Color(0f, 0f, 0f, 0.001f);
        vp.gameObject.AddComponent<RectMask2D>();
        reserveContent = NewRT("Content", vp);
        Anch(reserveContent, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(800, 156));
        var sr = vp.gameObject.AddComponent<ScrollRect>();
        sr.viewport = vp; sr.content = reserveContent;
        sr.horizontal = true; sr.vertical = false;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.scrollSensitivity = 40f;
        // ScrollRect's horizontal mode doesn't consume the mouse wheel by default.
        var wheel = vp.gameObject.AddComponent<WorkerReserveWheel>();
        wheel.Target = reserveContent;

        BuildDetail(page);
        BuildStock(page);
    }

    void BuildDetail(RectTransform page)
    {
        var p = UiKit.MakePanel(page, new Vector2(476, 490), false, 0.6f, "Detail");
        TL(p.rectTransform, 814, 0, 476, 490);
        var root = p.rectTransform;

        detailEmpty = NewRT("Empty", root).gameObject;
        Stretch((RectTransform)detailEmpty.transform);
        Wrap(detailEmpty.transform, "В лагере пока никого.\nОткрой пак на вкладке «Паки»: первый бесплатный подарок за палатку.", 24f, Soft, TextAlignmentOptions.Center, false, 30, 120, 416, 120);
        MakeBtn(detailEmpty.transform, "К пакам", UiBtn.Amber, 110, 270, 256, 56, 28f, () => SetTab(Tab.Packs));

        detailBody = NewRT("Body", root).gameObject;
        Stretch((RectTransform)detailBody.transform);
        var b = detailBody.transform;

        detailCard = WorkerCardView.Create(b, 132f, 176f, "DetailCard");
        detailCard.showRole = false;
        TL(detailCard.rt, 16, 16, 132, 176);

        dName = Lbl(b, "", 30f, Color.white, TextAlignmentOptions.Left, true, 164, 12, 300, 40);
        dAge = Lbl(b, "", 21f, Soft, TextAlignmentOptions.Left, false, 164, 52, 300, 26);
        dGem = Icon(b, UiIcon.Gem, 180, 100, 28);
        dRarity = Lbl(b, "", 24f, Color.white, TextAlignmentOptions.Left, true, 200, 84, 130, 32);
        for (int i = 0; i < 5; i++) dStars[i] = Icon(b, UiIcon.Star, 352 + i * 24, 100, 22);
        dRoleIcon = Icon(b, UiIcon.Coin, 180, 134, 28);
        dRole = Lbl(b, "", 24f, Color.white, TextAlignmentOptions.Left, true, 200, 118, 260, 32);
        dLevel = Lbl(b, "", 20f, Soft, TextAlignmentOptions.Left, false, 164, 154, 300, 26);
        Image xf;
        var bar = UiKit.MakeBar(b, UiBar.Amber, new Vector2(296, 24), out xf, 0.8f);
        TL(bar.rectTransform, 164, 182, 296, 24);
        dXpFill = xf;
        dXp = UiKit.MakeLabel(bar.transform, "", 15f, Color.white, TextAlignmentOptions.Center, true);

        dProd = Wrap(b, "", 20f, Color.white, TextAlignmentOptions.Left, false, 16, 214, 444, 30);
        dStatusIcon = Icon(b, UiIcon.Clock, 30, 262, 26);
        dStatus = Lbl(b, "", 21f, Color.white, TextAlignmentOptions.Left, true, 50, 248, 410, 28);

        for (int i = 0; i < 5; i++)
        {
            float y = 282f + i * 30f;
            perkIcon[i] = Icon(b, UiIcon.Check, 30, y + 15, 24);
            perkName[i] = Lbl(b, "", 20f, Color.white, TextAlignmentOptions.Left, true, 50, y, 150, 30);
            perkDesc[i] = Wrap(b, "", 17f, UiKit.Good, TextAlignmentOptions.Left, false, 204, y, 262, 30);
        }

        dAction = MakeBtn(b, "Убрать из слота", UiBtn.Yellow, 100, 438, 276, 46, 24f, () => { });
    }

    void BuildStock(RectTransform page)
    {
        var p = UiKit.MakePanel(page, new Vector2(476, 217), false, 0.6f, "Stock");
        TL(p.rectTransform, 814, 498, 476, 217);
        var b = p.transform;

        Icon(b, UiIcon.Tent, 34, 26, 32);
        Lbl(b, "Склад лагеря", 24f, Color.white, TextAlignmentOptions.Left, true, 58, 8, 170, 36);
        stockMsg = Lbl(b, "", 17f, Warn, TextAlignmentOptions.Right, true, 230, 8, 230, 36);

        Icon(b, UiIcon.Log, 34, 70, 34);
        Image lf;
        var lb = UiKit.MakeBar(b, UiBar.Amber, new Vector2(270, 26), out lf, 0.8f);
        TL(lb.rectTransform, 58, 56, 270, 26);
        logFill = lf;
        logText = Lbl(b, "", 21f, Color.white, TextAlignmentOptions.Left, true, 338, 54, 130, 30);

        Icon(b, UiIcon.Plank, 34, 108, 34);
        Image pf;
        var pb = UiKit.MakeBar(b, UiBar.Green, new Vector2(270, 26), out pf, 0.8f);
        TL(pb.rectTransform, 58, 94, 270, 26);
        plankFill = pf;
        plankText = Lbl(b, "", 21f, Color.white, TextAlignmentOptions.Left, true, 338, 92, 130, 30);

        stockPreview = Wrap(b, "", 17f, Soft, TextAlignmentOptions.Left, false, 16, 126, 446, 26);
        takeBtn = MakeBtn(b, "Забрать  [Пробел]", UiBtn.Green, 14, 156, 222, 48, 21f, DoTake);
        sellBtn = MakeBtn(b, "Продать −" + Mathf.RoundToInt((1f - WorkerBalance.SellRate) * 100f) + "%", UiBtn.Yellow, 240, 156, 222, 48, 22f, DoSell);
    }

    // ------------------------------------------------------------ Паки
    void BuildPacks(RectTransform page)
    {
        for (int t = 0; t < 3; t++)
        {
            int ti = t;
            var type = (WorkerPackType)t;
            var u = new PackUi();
            packUi[t] = u;
            Color pc = WorkerUi.PackColor(type);

            var holder = NewRT("Pack" + t, page);
            TL(holder, t * 437f, 0, 416, 715);

            u.hl = UiKit.MakeImage(holder, "Highlight", UiKit.Slot, true, 1f);
            u.hl.rectTransform.anchorMin = Vector2.zero; u.hl.rectTransform.anchorMax = Vector2.one;
            u.hl.rectTransform.offsetMin = new Vector2(-7, -7); u.hl.rectTransform.offsetMax = new Vector2(7, 7);
            u.hl.raycastTarget = false;

            var pn = UiKit.MakePanel(holder, new Vector2(416, 715), false, 0.6f, "Panel");
            Stretch(pn.rectTransform);
            AddClick(pn, () => { packSel = ti; dirty = true; });
            var b = pn.transform;

            Lbl(b, WorkerText.Pack(type), 32f, pc, TextAlignmentOptions.Center, true, 16, 12, 384, 42);

            u.artGlow = UiKit.MakeImage(b, "Glow", PetUi.GlowSprite, false);
            u.artGlow.color = new Color(pc.r, pc.g, pc.b, 0.5f);
            u.artGlow.raycastTarget = false;
            TLC(u.artGlow.rectTransform, 208, 140, 280, 280);
            // Custom camp-style pack case: no legacy Box icon; layered wood, brass bands and a rarity gem.
            BuildPackCaseArt(b, pc, 208f, 140f);

            u.ribbon = UiKit.MakeImage(b, "Ribbon", null, false);
            u.ribbon.color = UiKit.Good;
            TL(u.ribbon.rectTransform, 268, 66, 130, 34);
            u.ribbon.raycastTarget = false;
            u.ribbonText = UiKit.MakeLabel(u.ribbon.transform, "ПОДАРОК", 20f, UiKit.Navy, TextAlignmentOptions.Center, true);

            u.priceIcon = Icon(b, UiIcon.Coin, 78, 252, 34);
            u.priceText = Lbl(b, "", 26f, Color.white, TextAlignmentOptions.Left, true, 102, 234, 300, 36);

            Lbl(b, "Шансы выпадения", 19f, WorkerUi.Dim, TextAlignmentOptions.Left, false, 16, 284, 384, 24);
            for (int r = 0; r < 5; r++)
            {
                float y = 310f + r * 34f;
                u.oddIcon[r] = Icon(b, UiIcon.Gem, 36, y + 17, 26);
                u.oddName[r] = Lbl(b, WorkerText.Rarity((WorkerRarity)r), 21f, Color.white, TextAlignmentOptions.Left, true, 56, y, 132, 34);
                RectTransform f;
                var bar = WorkerUi.MiniBar(b, new Color(0f, 0f, 0f, 0.4f), WorkerUi.RarityAccent((WorkerRarity)r), out f);
                TL(bar.rectTransform, 192, y + 11, 130, 13);
                u.oddFill[r] = f;
                u.oddPct[r] = Lbl(b, "", 21f, Color.white, TextAlignmentOptions.Right, true, 326, y, 76, 34);
            }

            Lbl(b, "Гарантии", 19f, WorkerUi.Dim, TextAlignmentOptions.Left, false, 16, 486, 384, 24);
            u.guar[0] = Lbl(b, "", 19f, Soft, TextAlignmentOptions.Left, false, 16, 510, 384, 26);
            u.guar[1] = Lbl(b, "", 19f, Soft, TextAlignmentOptions.Left, false, 16, 536, 384, 26);
            u.guar[2] = Lbl(b, "", 19f, Soft, TextAlignmentOptions.Left, true, 16, 562, 384, 26);
            u.mythBack = WorkerUi.MiniBar(b, new Color(0f, 0f, 0f, 0.4f), WorkerUi.RarityAccent(WorkerRarity.Mythic), out u.mythFill);
            TL(u.mythBack.rectTransform, 16, 592, 384, 12);

            u.reason = Lbl(b, "", 19f, Warn, TextAlignmentOptions.Center, true, 16, 610, 384, 26);
            u.open = MakeBtn(b, "Открыть", UiBtn.Amber, 16, 644, 236, 58, 27f, () => { packSel = ti; StartOpen(type); });
            u.odds = MakeBtn(b, "Шансы", UiBtn.Gray, 262, 644, 138, 58, 24f, () => OpenOdds(type));
        }
    }

    // ------------------------------------------------------------ Коллекция
    void BuildCollectionShell(RectTransform page)
    {
        collCount = Lbl(page, "Собрано 0/27", 26f, Color.white, TextAlignmentOptions.Left, true, 0, 0, 500, 36);
        Lbl(page, "Нажми на карточку, чтобы посмотреть подробности", 19f, Dim, TextAlignmentOptions.Right, false, 600, 6, 690, 28);

        // подписи рядов и подробности строятся сразу, карточки по базе: при первом показе
        for (int r = 0; r < 3; r++)
        {
            float y = 44f + r * 164f;
            Icon(page, WorkerUi.RoleIcon((WorkerRole)r), 35, y + 44, 44);
            Lbl(page, WorkerUi.RolePlural((WorkerRole)r), 15f, Color.white, TextAlignmentOptions.Center, true, 0, y + 70, 72, 22);
            collRoleCount[r] = Lbl(page, "0/9", 18f, Soft, TextAlignmentOptions.Center, true, 0, y + 94, 72, 24);
        }

        var dp = UiKit.MakePanel(page, new Vector2(PageW, 179), false, 0.6f, "CollDetail");
        TL(dp.rectTransform, 0, 536, PageW, 179);
        cdBody = NewRT("Body", dp.rectTransform).gameObject;
        Stretch((RectTransform)cdBody.transform);
        var b = cdBody.transform;

        cdCard = WorkerCardView.Create(b, 112f, 150f, "CollDetailCard");
        cdCard.showRole = false;
        TL(cdCard.rt, 16, 14, 112, 150);
        cdName = Lbl(b, "", 32f, Color.white, TextAlignmentOptions.Left, true, 146, 8, 500, 42);
        cdLine = Lbl(b, "", 21f, Soft, TextAlignmentOptions.Left, false, 146, 50, 500, 26);
        cdGem = Icon(b, UiIcon.Gem, 162, 98, 28);
        cdRarity = Lbl(b, "", 23f, Color.white, TextAlignmentOptions.Left, true, 184, 82, 340, 32);
        for (int i = 0; i < 5; i++) cdStars[i] = Icon(b, UiIcon.Star, 540 + i * 22, 98, 20);
        cdState = Wrap(b, "", 21f, Color.white, TextAlignmentOptions.Left, false, 146, 118, 520, 52);
        cdPerks = Wrap(b, "", 19f, Color.white, TextAlignmentOptions.TopLeft, false, 690, 10, 580, 100);
        cdBio = Wrap(b, "", 19f, Soft, TextAlignmentOptions.TopLeft, false, 690, 114, 580, 56);
        cdBio.fontStyle = FontStyles.Italic;
    }

    void BuildCollection(WorkerManager m)
    {
        if (m.database == null || m.database.workers == null || m.database.workers.Count == 0)
        {
            collCount.text = "База рабочих не назначена (Tools > BuilderSim > Настроить рабочих)";
            cdBody.SetActive(false);
            return;
        }

        // ряд на роль, внутри по редкости (порядок в базе сохраняется)
        var page = pages[2];
        for (int r = 0; r < 3; r++)
        {
            var list = new List<WorkerDefinition>();
            for (int k = 0; k < 5; k++)
                for (int i = 0; i < m.database.workers.Count; i++)
                {
                    var d = m.database.workers[i];
                    if (d != null && d.role == (WorkerRole)r && d.rarity == (WorkerRarity)k) list.Add(d);
                }
            for (int i = 0; i < list.Count && i < 9; i++)
            {
                var d = list[i];
                var c = WorkerCardView.Create(page, 118f, 150f, "Coll_" + d.id);
                c.showRole = false;
                TL(c.rt, 78f + i * 126f, 44f + r * 164f + 4f, 118f, 150f);
                var dd = d;
                AddClick(c.frame, () => { selDef = dd; dirty = true; });
                collDefs.Add(d);
                collCards.Add(c);
            }
        }
        collBuilt = true;
    }

    // ------------------------------------------------------------ экран открытия пака
    void BuildOpenOverlay()
    {
        openRoot = NewRT("Opening", inner).gameObject;
        var rt = (RectTransform)openRoot.transform;
        Stretch(rt);
        var dimImg = UiKit.MakeImage(rt, "Dim", null, false);
        dimImg.color = new Color(0.03f, 0.05f, 0.09f, 0.93f);
        Stretch(dimImg.rectTransform);
        dimImg.raycastTarget = true;

        var mid = new Vector2(0.5f, 0.5f);
        openRays = UiKit.MakeImage(rt, "Rays", PetUi.RaysSprite, false);
        raysRt = openRays.rectTransform;
        Anch(raysRt, mid, mid, CardPos, new Vector2(860, 860));
        openRays.raycastTarget = false;

        openGlow = UiKit.MakeImage(rt, "Glow", PetUi.GlowSprite, false);
        glowRt = openGlow.rectTransform;
        Anch(glowRt, mid, mid, CardPos, new Vector2(620, 620));
        openGlow.raycastTarget = false;

        openArt = UiKit.MakeIcon(rt, UiIcon.Box, 230f, "PackArt");
        artRt = openArt.rectTransform;
        Anch(artRt, mid, mid, CardPos, new Vector2(230, 230));
        openArt.raycastTarget = false;

        resCard = WorkerCardView.Create(rt, 300f, 400f, "ResultCard");
        Anch(resCard.rt, mid, mid, CardPos, new Vector2(300, 400));

        fxRt = NewRT("Fx", rt);
        Stretch(fxRt);
        for (int i = 0; i < sparks.Length; i++)
        {
            var im = UiKit.MakeImage(fxRt, "Spark", PetUi.SparkSprite, false);
            im.raycastTarget = false;
            Anch(im.rectTransform, mid, mid, Vector2.zero, new Vector2(16, 16));
            im.gameObject.SetActive(false);
            sparks[i] = new Spark { img = im, rt = im.rectTransform };
        }

        waitText = UiKit.MakeLabel(rt, "", 38f, Soft, TextAlignmentOptions.Center, true);
        Anch(waitText.rectTransform, mid, mid, new Vector2(260, 20), new Vector2(640, 60));

        // информация о рабочем (справа), координаты TL внутри блока 640x520
        var info = NewRT("Info", rt);
        Anch(info, mid, mid, new Vector2(260, 24), new Vector2(640, 520));
        infoCg = info.gameObject.AddComponent<CanvasGroup>();
        var b = info.transform;

        rTag = UiKit.MakeImage(b, "Tag", null, false);
        TL(rTag.rectTransform, 0, 0, 330, 44);
        rTag.raycastTarget = false;
        rTagText = UiKit.MakeLabel(rTag.transform, "", 26f, Color.white, TextAlignmentOptions.Center, true);
        AutoSize(rTagText, 14f, 26f);

        rName = Lbl(b, "", 40f, Color.white, TextAlignmentOptions.Left, true, 0, 56, 640, 54);
        rGem = Icon(b, UiIcon.Gem, 20, 138, 34);
        rRarity = Lbl(b, "", 30f, Color.white, TextAlignmentOptions.Left, true, 46, 118, 230, 40);
        for (int i = 0; i < 5; i++) rStars[i] = Icon(b, UiIcon.Star, 300 + i * 32, 138, 28);
        rRoleIcon = Icon(b, UiIcon.Coin, 20, 184, 32);
        rRole = Lbl(b, "", 26f, Color.white, TextAlignmentOptions.Left, true, 46, 166, 590, 36);
        rLevel = Lbl(b, "", 24f, Soft, TextAlignmentOptions.Left, false, 0, 208, 640, 32);
        rLine = Wrap(b, "", 25f, Color.white, TextAlignmentOptions.TopLeft, true, 0, 246, 640, 84);
        rPerks = Wrap(b, "", 21f, Soft, TextAlignmentOptions.TopLeft, false, 0, 336, 640, 56);
        rBio = Wrap(b, "", 20f, Soft, TextAlignmentOptions.TopLeft, false, 0, 394, 640, 60);
        rBio.fontStyle = FontStyles.Italic;
        againBtn = MakeBtn(b, "Ещё", UiBtn.Amber, 0, 464, 320, 56, 26f, OpenAgain);
        doneBtn = MakeBtn(b, "Готово", UiBtn.Gray, 340, 464, 200, 56, 26f, CloseResult);

        flash = UiKit.MakeImage(rt, "Flash", null, false);
        Stretch(flash.rectTransform);
        flash.color = new Color(1f, 1f, 1f, 0f);
        flash.raycastTarget = false;

        openHint = UiKit.MakeLabel(rt, "", 22f, WorkerUi.Dim, TextAlignmentOptions.Center, false);
        Anch(openHint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 16), new Vector2(900, 34));

        openRoot.SetActive(false);
    }

    // ------------------------------------------------------------ окно шансов
    void BuildOddsOverlay()
    {
        oddsGo = NewRT("Odds", inner).gameObject;
        var rt = (RectTransform)oddsGo.transform;
        Stretch(rt);
        var dimImg = UiKit.MakeImage(rt, "Dim", null, false);
        dimImg.color = new Color(0f, 0f, 0f, 0.7f);
        Stretch(dimImg.rectTransform);
        dimImg.raycastTarget = true;

        var mid = new Vector2(0.5f, 0.5f);
        var pn = UiKit.MakePanel(rt, new Vector2(800, 690), true, 1f, "OddsPanel");
        Anch(pn.rectTransform, mid, mid, Vector2.zero, new Vector2(800, 690));
        pn.raycastTarget = true;
        var b = pn.transform;

        oTitle = Lbl(b, "Шансы", 36f, Color.white, TextAlignmentOptions.Center, true, 30, 24, 740, 46);

        Lbl(b, "Редкость", 18f, WorkerUi.Dim, TextAlignmentOptions.Left, false, 70, 76, 200, 24);
        Lbl(b, "Шанс", 18f, WorkerUi.Dim, TextAlignmentOptions.Left, false, 290, 76, 100, 24);
        Lbl(b, "Перки", 18f, WorkerUi.Dim, TextAlignmentOptions.Center, false, 560, 76, 80, 24);
        Lbl(b, "Дубликат", 18f, WorkerUi.Dim, TextAlignmentOptions.Right, false, 640, 76, 130, 24);
        for (int r = 0; r < 5; r++)
        {
            float y = 104f + r * 46f;
            var rar = (WorkerRarity)r;
            oGem[r] = Icon(b, UiIcon.Gem, 50, y + 22, 30);
            oName[r] = Lbl(b, WorkerText.Rarity(rar), 24f, Color.white, TextAlignmentOptions.Left, true, 70, y, 200, 44);
            RectTransform f;
            var bar = WorkerUi.MiniBar(b, new Color(0f, 0f, 0f, 0.4f), WorkerUi.RarityAccent(rar), out f);
            TL(bar.rectTransform, 290, y + 14, 140, 16);
            oFill[r] = f;
            oPct[r] = Lbl(b, "", 24f, Color.white, TextAlignmentOptions.Right, true, 436, y, 100, 44);
            oPerks[r] = Lbl(b, rar == WorkerRarity.Mythic ? "все+" : WorkerBalance.PerkCount(rar).ToString(), 24f, Soft, TextAlignmentOptions.Center, false, 560, y, 80, 44);
            oXp[r] = Lbl(b, "+" + WorkerBalance.DuplicateXpFor(rar) + " XP", 22f, Soft, TextAlignmentOptions.Right, false, 640, y, 130, 44);
        }

        Lbl(b, "Гарантии (считаются отдельно для каждого пака)", 20f, WorkerUi.Dim, TextAlignmentOptions.Left, false, 30, 346, 740, 28);
        for (int i = 0; i < 3; i++) oGuarLine[i] = Lbl(b, "", 21f, Soft, TextAlignmentOptions.Left, false, 30, 376 + i * 32, 740, 30);
        var mb = WorkerUi.MiniBar(b, new Color(0f, 0f, 0f, 0.4f), WorkerUi.RarityAccent(WorkerRarity.Mythic), out oMythFill);
        TL(mb.rectTransform, 30, 474, 740, 12);

        string coins = "";
        for (int r = 0; r < 5; r++) { if (r > 0) coins += " / "; coins += WorkerBalance.DuplicateCoinsFor((WorkerRarity)r); }
        oDup = Wrap(b, "Дубликат даёт уже найденному рабочему опыт (столбец «Дубликат»). Если он на максимальном уровне, вместо опыта выплачиваются монеты: " + coins + ". Подарочный пак гарантирует первого рабочего.",
            19f, Soft, TextAlignmentOptions.TopLeft, false, 30, 504, 740, 90);

        MakeBtn(b, "Закрыть", UiBtn.Amber, 270, 610, 260, 56, 28f, CloseOdds);
        oddsGo.SetActive(false);
    }
}
