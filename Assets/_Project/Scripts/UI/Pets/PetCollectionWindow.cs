// Assets/_Project/Scripts/UI/Pets/PetCollectionWindow.cs (НЕ в Editor)
// Окно «Питомцы»: вся коллекция игрока, слоты экипировки и суммарные бонусы. Стиль UiKit, как у PetCrateWindow и WorkersWindow.
// Всё строится из кода, в сцене ничего настраивать не нужно: окно и кнопка в HUD создаются сами после загрузки сцены.
//   PetCollectionWindow.Open() / Close() / Toggle() / IsOpen   управление из кода
//   Открыть игроку:  кнопка с лапкой справа снизу в HUD  или клавиша P.  Закрыть: P, E, Esc или крестик.
//
// Что показывает:
//   Слоты       до 3 штук (сколько открыто, решает уровень: GameBalance.PetSlotLevels). Закрытый слот подписан уровнем.
//               Нажал на занятый слот: питомец выбран. Нажал на пустой открытый слот: в него встаёт выбранный питомец.
//   Бонусы      суммарный бонус всех питомцев в слотах по характеристикам (PetManager.Bonus), без нулевых.
//   Коллекция   все питомцы игрока карточками (PetCardView), по редкости от Легендарных к Обычным, внутри по имени.
//               Прокрутка, зелёная галочка на тех, кто сейчас в слоте, рамка на выбранном. Дубликаты показаны отдельными карточками.
//   Выбранный   крупная карточка, имя, редкость, что усиливает и на сколько, кнопки «Взять с собой» / «Снять» и «Отпустить»
//               («Отпустить» с подтверждением вторым нажатием: питомец удаляется навсегда).
// Окно только читает PetManager (Owned, GetEquipped, Equip, Unequip, Release, Bonus) и слушает PetManager.Changed,
// поэтому всё обновляется само: открыли клеть, сменился уровень, поменяли слоты из другого места.
// Выбранный питомец показан живой вращающейся 3D-моделью (PetLiveView) в стадии роста PetManager.GrowthStage. Остальные карточки: 3D-снимок,
// иначе иконка PetData.icon, иначе лапка на цветной плашке (см. PetCardView).
//
// Дополнительно (задача «UI питомцев»):
//   Дельта      у выбранного питомца: «Скорость рубки +3% -> +5%» (относительно того, кого он заменит или если снять) и живое значение
//               «Рубка: 3.00 с -> 2.85 с» через PlayerUpgrades.Calc и PetBonus.Rebase. Кнопка «Заменить» ставит питомца на место вытесняемого.
//   Сортировка  по редкости / характеристике / имени / новым и фильтр по характеристике (PetStat). Бейдж NEW гаснет, когда питомца выбрали.
//   Подобрать   «Подобрать лучших»: PetManager.AutoEquipBest ставит набор с максимальным суммарным эффектом с учётом PetBonusCap.
//   Слияние     GameBalance.PetMergeCount одинаковых -> одна с +1 звездой (PetManager.Merge). Дубликаты предлагают слияние раньше «Отпустить».
//   Книга       вкладка со всеми видами из PetDatabase: неоткрытые тёмными силуэтами, прогресс «12/30», бонус за полную редкость.
//   Карточки    пул (PoolRows × Cols штук): рисуются только видимые, при 100+ питомцах окно не тормозит.
//
// Кнопка в HUD появляется, когда питомцы уже «открыты» сюжетом (первая клеть открыта или питомцы уже есть), и прячется
// на время диалогов, окон, большой карты и заставок. Красная «!» на кнопке: есть питомец вне слота и свободный слот.
// Пока окно открыто, MobileInput.ModalOpen = true: персонаж стоит, рубка и E «сквозь» окно не работают.
// Для теста: в PetDebugPanel (F10) кнопка «Окно коллекции» открывает окно, даже если питомцы ещё не открыты сюжетом.
//
// Доработка «Удобство и анимации»:
//   Слоты       крестик «снять» на занятом слоте, ПКМ по слоту снимает, ПКМ по карточке берёт/снимает, подсказки у каждого слота.
//               Цифра 1-4 или клик по пустому слоту при выбранном питомце, который уже в команде: переносит его (или меняет местами). Ctrl+Z отменяет.
//   Сообщения   зелёные (получилось), жёлтые (нельзя), белые (подсказка); всплывают с «пружинкой».
//   Анимации    карточки появляются волной при открытии, смене вкладки и фильтра; плавная рамка выбора; переход описания при смене питомца;
//               вспышка панели бонусов при их изменении; полоса таймера на «Отпустить»; индикатор прокрутки в коллекции.
//   Понятность  подсказка при наведении на каждую кнопку, «Показано N из M» при фильтре, кнопка «Сбросить фильтры» в пустом списке,
//               предупреждение при отпуске редкого или питомца в команде, число новых питомцев на кнопке в HUD.
//   Ошибки      выбор больше не сбрасывается при смене вкладки; после «Отпустить» и «Слить» выбирается соседний питомец и список прокручивается к нему;
//               подсказка внизу не вылезает за окно; PetHover больше не оставляет «залипшую» подсказку.
//
// Доработка «Наборы и живой питомец»:
//   Наборы      пресеты переехали из нижней части левой колонки (карточки 112x112, портреты по 23 px) в отдельную колонку «Наборы» справа:
//               три карточки 168x216, портреты 58 px по схеме слотов 2x2, два главных бонуса крупно, кнопки 49x28. Окно стало шире (1600).
//               Освободившееся место в левой колонке отдано слотам (выше) и бонусам (строки 24 px вместо 20 px). Рядом с «Подобрать» появилась кнопка «Отмена».
//   Интерактив  питомца справа можно погладить: клик (или тап) по нему: подпрыгивает, летят сердечки, реплика, растёт «привязанность»
//               (Присматривается, Доверяет, Любит тебя, Лучший друг). Перетаскивание мышью вращает модель. Счётчик живёт до перезапуска сцены.
//   Анимации    слоты и наборы появляются волной при открытии, у надетого набора пульсирует рамка, при сохранении или надевании набора портреты
//               «подпрыгивают», у пустого набора мигает плюс, лапка в пустом слоте покачивается, полоски бонусов плавно доезжают,
//               кнопка «Взять с собой» дышит, пока есть свободный слот, карточка питомца «приседает» при поглаживании.
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UiLayout;

// ----- КАРТА ФАЙЛОВ (класс разбит на части partial, поведение не менялось) -----
// PetCollectionWindow.cs: статический API, размеры, цвета, состояние, жизненный цикл (этот файл)
// PetCollectionWindow.Actions.cs: действия игрока: открыть/закрыть, вкладки, экипировка, сортировка
// PetCollectionWindow.Fill.cs: заполнение окна данными, пул карточек питомцев
// PetCollectionWindow.DeltaBook.cs: дельта статов «что изменится, если взять питомца» и «Книга питомцев»
// PetCollectionWindow.Build.cs: сборка окна из кода (все RectTransform, кнопки, панели)
// PetCollectionWindow.Presets.cs: наборы (пресеты) питомцев
// PetCollectionWindow.Interact.cs: мелкие анимации и интерактив «погладить питомца»
// PetCollectionWindow.HudHelpers.cs: кнопка коллекции в HUD и мелкие помощники
public partial class PetCollectionWindow : MonoBehaviour
{
    // ============================================================ статический API
    static PetCollectionWindow instance;

    public static PetCollectionWindow Instance { get { return instance; } }
    public static bool IsOpen { get { return instance != null && instance.open; } }

    // питомцы уже доступны игроку (иначе кнопка в HUD скрыта и P ничего не открывает)
    public static bool Available
    {
        get
        {
            var pm = PetManager.Instance;
            return pm != null && (pm.Owned.Count > 0 || pm.FreeCrateUsed || pm.debugIgnoreLevelLock);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { instance = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() { Ensure(); }

    static PetCollectionWindow Ensure()
    {
        if (instance == null)
        {
            var go = new GameObject("PetCollectionWindow");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<PetCollectionWindow>();
        }
        return instance;
    }

    public static void Open() { Ensure().DoOpen(false); }
    public static void OpenAnyway() { Ensure().DoOpen(true); }          // для тестов: без проверки «питомцы открыты сюжетом»
    public static void Close() { if (instance != null) instance.DoClose(); }
    public static void Toggle() { if (IsOpen) Close(); else Open(); }

    // ============================================================ размеры (единицы Canvas 1920x1080)
    const float WinW = 1600f, WinH = 900f, BS = 1.5f;             // шире прежних 1400: справа новая колонка «Наборы»
    const float InW = WinW - 78f;                                  // внутренняя область 1522 x 807
    const float InH = WinH - 39f - 54f;
    const float TopH = 64f;                                        // верхняя полоса: вкладки, сортировка, «Подобрать»

    // левая колонка «Команда»: слоты 2x2, бонусы, пресеты (координаты внутри панели)
    const float TeamX = 20f, TeamY = TopH + 6f, TeamW = 372f, TeamH = InH - TeamY - 16f;
    const float SlotW = 170f, SlotH = 184f, SlotGapX = 12f, SlotGapY = 10f, SlotY = 46f;      // слоты выше прежних 164: место освободили наборы
    const float BonusY = SlotY + 2f * SlotH + SlotGapY + 8f, BonusH = TeamH - BonusY - 12f;    // панель бонусов занимает всё остальное
    const float BonusRowY = 44f, BonusPitch = 32f;                                              // строки бонусов: шрифт 24 вместо 20
    const int BonusRows = 7;

    // середина: фильтр по редкости и коллекция
    const float ChipY = TopH + 6f, ChipH = 40f;
    const float GridX = 404f, GridY = ChipY + ChipH + 8f, GridW = 520f, GridH = InH - GridY - 16f;
    const float CardW = 152f, CardH = 180f, PitchX = 168f, PitchY = 196f;
    const int Cols = 3;
    const int PoolRows = 5;                                        // видимых строк не больше 4: пять с запасом
    const int PoolSize = Cols * PoolRows;
    const float CellLeft = (GridW - 16f - (Cols * PitchX - 16f)) * 0.5f;
    const string Arrow = " -> ";

    // справа: выбранный питомец
    const float DetX = GridX + GridW + 12f, DetY = TeamY, DetW = 366f, DetH = TeamH;

    // колонка «Наборы» справа от описания питомца
    const float PrX = DetX + DetW + 12f, PrY = TeamY, PrW = InW - PrX - 20f, PrH = TeamH;

    const float HudBtn = UpgradeWindow.HudBtnH;      // кнопка в HUD: той же высоты, что «Прокачка», стоит левее неё
    const float ArmTime = 3f;                        // сколько живёт подтверждение «Отпустить»

    // ============================================================ цвета
    static readonly Color Soft = PetUi.Soft;
    static readonly Color Dim = PetUi.Dim;
    static readonly Color WoodText = new Color32(0x2A, 0x16, 0x08, 255);
    static readonly Color OffText = new Color(0.86f, 0.88f, 0.92f, 1f);
    static readonly Color Clear = PetUi.Clear;
    static readonly Color DotColor = new Color32(0xE8, 0x4B, 0x3C, 255);

    // ============================================================ состояние
    class SlotView
    {
        public Image bg, icon;
        public GameObject xBtn;                      // крестик «снять»
        public PetHover hover;
        public PetCardView card;
        public TMP_Text label;
        public Color baseCol = Color.white;
        public float pop, baseScale = 1f;
    }

    enum SortMode { Rarity, Stat, Name, Newest }
    enum Tab { Collection, Book }
    static readonly string[] SortNames = { "Редкость", "Характеристика", "Имя", "Новые" };

    // строка списка: питомец игрока (коллекция) или вид из базы (книга; inst == null)
    class Entry
    {
        public PetInstance inst;
        public PetData data;
        public bool locked;
    }

    // карточка из пула: привязана к индексу строки списка, пока он в видимой области
    class PoolCard
    {
        public PetCardView view;
        public GameObject mark;                      // зелёная галочка «в слоте»
        public CanvasGroup cg;                       // плавное появление
        public int index = -1;
        public float appear = 1f, delay, sc = 1f;    // появление (0..1), задержка волны, сглаженный масштаб
    }

    enum ToastKind { Info, Good, Warn }

    bool open, built, dirty;
    int openFrame = -1, closeFrame = -1;
    float openK;
    int selectedUid = -1;
    float armedUntil, toastUntil, toastStart;
    string toastText = "";
    ToastKind toastKind = ToastKind.Warn;
    bool animateNext, revealPending;                 // волна появления карточек / прокрутить к выбранному после перестройки списка
    int lastDetUid = -2;
    float detT = 1f;                                 // переход описания при смене питомца
    CanvasGroup detCg;
    Image armBar, scrollTrack, scrollHandle, bonusPanel;
    Vector2 selTarget;
    float bonusFlash;
    int bonusSig;
    bool bonusSigInit;
    Button resetBtn;
    TMP_Text hudBang;
    bool hudRegistered;
    float dotT;

    // новое: анимации и интерактив
    readonly float[] slotIn = new float[PetManager.MaxSlots];            // появление слотов при открытии (отрицательное значение = задержка)
    readonly float[] bonusBarCur = new float[7], bonusBarTgt = new float[7];   // полоски бонусов доезжают плавно
    Button undoBtn;
    TMP_Text undoLabel;
    PetHeartFx hearts;
    PetPetZone petZone;
    Image petBubble, patHeart;
    TMP_Text petBubbleText, patText;
    float bubbleT = -1f, patCool, patComboT, patPulse;
    int patCombo;
    readonly Dictionary<int, int> pats = new Dictionary<int, int>();     // uid -> сколько раз погладили (до перезапуска сцены)

    // окно
    CanvasGroup overlay;
    RectTransform panel, contentsRt;
    ScrollRect scroll;
    TMP_Text titleText, countText, slotsLabel, hintText, emptyGrid;
    readonly SlotView[] slots = new SlotView[PetManager.MaxSlots];
    readonly Image[] bonusIcon = new Image[7];
    readonly Image[] bonusBarTrack = new Image[7], bonusBarFill = new Image[7];
    readonly TMP_Text[] bonusText = new TMP_Text[7];
    TMP_Text bonusEmpty;
    Image selFrame;
    readonly List<PoolCard> pool = new List<PoolCard>();
    readonly List<Entry> items = new List<Entry>();
    readonly List<PetInstance> sorted = new List<PetInstance>();     // питомцы коллекции после фильтра и сортировки

    // вид списка
    Tab tab = Tab.Collection;
    SortMode sortMode = SortMode.Rarity;
    int filterStat = -1;                             // -1 все, иначе номер PetStat
    bool layoutDirty;
    float lastScrollY, portraitCheckAt;
    Image tabColImg, tabBookImg;
    TMP_Text tabColLabel, tabBookLabel, sortLabel, filterLabel, autoLabel;
    Button sortBtn, filterBtn, autoBtn, unequipAllBtn;
    TMP_Text unequipAllLabel;

    // выбранный питомец
    RectTransform detRoot, bookRoot;
    PetCardView detCard;
    TMP_Text detName, detRarity, detExtra, detStat, detHead, detD1, detD2, detLive, detNote, detEmpty, equipLabel, unequipLabel, mergeLabel, releaseLabel;
    Button equipBtn, unequipBtn, mergeBtn, releaseBtn;
    Color releaseColor;

    // Книга питомцев
    TMP_Text bookTotal;
    Image bookBarFill;
    readonly TMP_Text[] bookName = new TMP_Text[4], bookCount = new TMP_Text[4], bookBonus = new TMP_Text[4];
    readonly Image[] bookCheck = new Image[4];

    // кнопка в HUD
    CanvasGroup hudGroup;
    RectTransform hudRt, hudDot;

    // ============================================================ жизненный цикл
    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance == this) instance = null;
    }

    void OnEnable() { PetManager.Changed += OnPetsChanged; }
    void OnDisable() { PetManager.Changed -= OnPetsChanged; }

    void OnPetsChanged() { if (open) dirty = true; }

    void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m)
    {
        // сцену перезагрузили («Начать заново»): окно закрыто, выбор сброшен
        open = false;
        selectedUid = -1;
        openK = 0f;
        pats.Clear();
        for (int i = 0; i < lastUid.Length; i++) lastUid[i] = -2;
        if (overlay != null) overlay.gameObject.SetActive(false);
    }

    void DoOpen(bool force)
    {
        if (open || Time.frameCount == closeFrame) return;          // клавиша, которой закрыли окно, его же не откроет заново
        if (MobileInput.ModalOpen || DialogueManager.IsBusy || DialogueSequence.AnyPlaying) return;   // два окна сразу не нужны
        if (PetManager.Instance == null) return;
        if (!force && !Available) return;

        EnsureBuilt();
        if (!built) return;
        open = true;
        openFrame = Time.frameCount;                                // клавиша, которой открыли окно, его же не закроет
        armedUntil = 0f;
        toastUntil = 0f;
        hoverPc = null;
        animateNext = true;                                         // карточки выезжают волной
        bonusSigInit = false;                                       // первая отрисовка бонусов без вспышки
        for (int i = 0; i < slotIn.Length; i++) slotIn[i] = -0.08f * i;           // слоты и наборы выезжают по очереди
        for (int i = 0; i < PresetCount; i++) pIn[i] = -0.10f * i - 0.1f;
        bubbleT = -1f;
        patPulse = 0f;
        for (int i = 0; i < lastUid.Length; i++) lastUid[i] = -2;   // слоты, изменившиеся пока окно было закрыто, не «хлопают» искрами
        Populate();
        SfxManager.Play("hint", 0.5f, 1.2f);
    }

    void DoClose()
    {
        if (!open) return;
        open = false;
        closeFrame = Time.frameCount;
        SaveSystem.RequestSave("pets");   // окно закрыли: питомцы и слоты уходят в файл
        armedUntil = 0f;
        SfxManager.Play("ui", 0.3f, 0.9f);
    }

    void EnsureBuilt()
    {
        if (built) return;
        UiInputGuard.EnsureEventSystem();
        Build();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void RegisterModal() { UiModal.Register(typeof(PetCollectionWindow), () => IsOpen, Close); }

    static bool OtherWindowOpen()
    {
        return UiModal.AnyOpen(typeof(PetCollectionWindow));   // реестр окон: UiModal
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        // кнопка в HUD строится лениво: когда питомцы впервые стали доступны
        if (hudGroup == null && Available) BuildHud();
        UpdateHud(dt);

        // P открывает и закрывает окно
        if (Input.GetKeyDown(KeyCode.P) && !open) DoOpen(false);

        if (built && open)
        {
            // начался разговор или открылось другое окно: закрываемся
            if (DialogueManager.IsBusy || DialogueSequence.AnyPlaying || OtherWindowOpen() || PetManager.Instance == null)
                DoClose();
            else
            {
                // выделенная кнопка реагировала бы ещё и на Enter и Пробел: снимаем выделение
                var es = EventSystem.current;
                if (es != null && es.currentSelectedGameObject != null) es.SetSelectedGameObject(null);
                Hotkeys();
            }
        }

        if (!built) return;

        openK = Mathf.MoveTowards(openK, open ? 1f : 0f, dt / 0.15f);
        bool show = openK > 0.001f;
        if (overlay.gameObject.activeSelf != show) overlay.gameObject.SetActive(show);
        if (!show) return;

        overlay.alpha = openK;
        panel.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, UiDraw.EaseOutCubic(openK));
        if (!open) return;

        if (dirty) Populate();
        AnimateFx(dt);
        AnimateCards(dt);
        AnimatePresets(dt);
        AnimateMisc(dt);
        AnimatePat(dt);

        // прокрутка: пул карточек перезаполняется только когда список сдвинулся
        if (layoutDirty || Mathf.Abs(contentsRt.anchoredPosition.y - lastScrollY) > 0.5f) LayoutCells(false);
        if (Time.unscaledTime >= portraitCheckAt) { portraitCheckAt = Time.unscaledTime + 0.25f; RecheckPortraits(); }
        if (armedUntil > 0f && Time.unscaledTime > armedUntil) { armedUntil = 0f; RefreshDetail(); }

        bool toastOn = Time.unscaledTime < toastUntil;
        string hint;
        if (toastOn)
        {
            Color tc = toastKind == ToastKind.Good ? UiKit.Good : (toastKind == ToastKind.Info ? Color.white : UiKit.Warn);
            hint = "<color=#" + PetUi.Hex(tc) + ">" + toastText + "</color>";
        }
        else hint = HoverInfo() ?? (PetHover.Tip ?? DefaultHint);
        Set(hintText, hint);
        float tk = Mathf.Clamp01((Time.unscaledTime - toastStart) / 0.25f);
        hintText.rectTransform.localScale = Vector3.one * (toastOn ? Mathf.LerpUnclamped(0.85f, 1f, UiDraw.EaseOutBack(tk)) : 1f);

        // полоса таймера на «Отпустить»: сколько осталось на подтверждение
        if (armBar != null)
        {
            bool a = armedUntil > 0f;
            if (armBar.gameObject.activeSelf != a) armBar.gameObject.SetActive(a);
            if (a) armBar.rectTransform.localScale = new Vector3(Mathf.Clamp01((armedUntil - Time.unscaledTime) / ArmTime), 1f, 1f);
        }
    }

    void Hotkeys()
    {
        if (Time.frameCount == openFrame) return;
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(MobileInput.ActionKey))
        {
            DoClose();
            return;
        }
        if (Input.GetKeyDown(KeyCode.Tab)) { SetTab(tab == Tab.Collection ? Tab.Book : Tab.Collection); return; }
        if (tab != Tab.Collection) return;

        if (Input.GetKeyDown(KeyCode.LeftArrow)) MoveSel(-1);
        else if (Input.GetKeyDown(KeyCode.RightArrow)) MoveSel(1);
        else if (Input.GetKeyDown(KeyCode.UpArrow)) MoveSel(-Cols);
        else if (Input.GetKeyDown(KeyCode.DownArrow)) MoveSel(Cols);

        bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        if (ctrl && Input.GetKeyDown(KeyCode.Z)) { OnUndoClick(); return; }
        if (!ctrl && Input.GetKeyDown(KeyCode.U)) { OnUnequipAllClick(); return; }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) ToggleSelectedEquip();
        for (int k = 0; k < PetManager.MaxSlots; k++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + k)) QuickSlot(k);
    }

    // стрелки: выбор соседнего питомца в сетке
    void MoveSel(int delta)
    {
        if (items.Count == 0) return;
        int idx = -1;
        for (int i = 0; i < items.Count; i++) if (items[i].inst != null && items[i].inst.uid == selectedUid) { idx = i; break; }
        int t = idx < 0 ? 0 : Mathf.Clamp(idx + delta, 0, items.Count - 1);
        if (items[t].inst != null) SelectUid(items[t].inst.uid, true, true);
    }

    // цифры 1-4: выбранного питомца в этот слот (если слот занят, он меняется местами с прежним)
    void QuickSlot(int k)
    {
        var pm = PetManager.Instance;
        if (pm == null || selectedUid < 0) return;
        if (!pm.IsSlotOpen(k)) { OnSlotClick(k); return; }

        // питомец уже в команде: переносим в этот слот (если там кто-то стоит, меняются местами)
        int fromSlot = -1;
        for (int i = 0; i < PetManager.MaxSlots; i++)
        {
            var cur = pm.GetEquipped(i);
            if (cur != null && cur.uid == selectedUid) { fromSlot = i; break; }
        }
        if (fromSlot >= 0)
        {
            if (fromSlot == k) { Toast("Питомец уже в этом слоте", ToastKind.Info); return; }
            var order = new int[PetManager.MaxSlots];
            for (int i = 0; i < order.Length; i++) { var cur = pm.GetEquipped(i); order[i] = cur != null ? cur.uid : -1; }
            int other = order[k];
            order[k] = selectedUid;
            order[fromSlot] = other;
            pm.SetLoadout(order);
            SfxManager.Play("ui", 0.4f, 1.1f);
            Toast(other >= 0 ? "Питомцы поменялись местами. Ctrl+Z: отменить" : "Питомец перенесён в слот " + (k + 1), ToastKind.Good);
            return;
        }

        var occ = pm.GetEquipped(k);
        bool ok = occ == null ? pm.Equip(selectedUid, k) : pm.EquipReplacing(selectedUid, occ.uid);
        if (ok) SfxManager.Play("coin", 0.7f, 1.1f);
    }

    // строка о питомце под курсором
    string HoverInfo()
    {
        if (hoverPc == null || hoverPc.index < 0 || hoverPc.index >= items.Count) return null;
        var e = items[hoverPc.index];
        if (e.data == null) return null;
        if (e.locked) return "<b>???</b>  ·  " + PetText.Rarity(e.data.rarity) + "  ·  ещё не найден: открывай клети";
        return "<b>" + PetUi.NameOf(e.data) + "</b>  ·  " + PetText.Rarity(e.data.rarity) + "  ·  " + PetUi.StatLine(e.data, e.inst != null ? e.inst.stars : 0);
    }

    static string DefaultHint
    {
        get
        {
            return Application.isMobilePlatform ? "Нажми на питомца, чтобы выбрать  ·  двойное нажатие: взять или снять"
                : "Стрелки: выбор  ·  Enter, двойной клик или ПКМ: взять/снять  ·  1-4: в слот  ·  U: снять всех  ·  Ctrl+Z: отмена  ·  Tab: Книга  ·  Esc: закрыть";
        }
    }

}


// Наведение курсора: лёгкое увеличение (grow), подсказка в строке под окном (tip) и колбэк (cb). Мышь и перо; на телефоне просто не срабатывает.
// tip можно менять на лету: пока курсор над элементом, показывается актуальный текст, и после ухода он не «залипает».
public class PetHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public float grow;
    public string tip;
    public System.Action<bool> cb;
    static string cur;
    public static string Tip { get { return cur; } }

    bool over;
    float k;
    string shown;                 // что именно мы выставили в cur

    public void OnPointerEnter(PointerEventData e)
    {
        over = true;
        shown = tip;
        if (tip != null) cur = tip;
        if (cb != null) cb(true);
    }

    public void OnPointerExit(PointerEventData e) { Leave(); }
    void OnDisable() { k = 0f; if (over) Leave(); if (grow > 0f) transform.localScale = Vector3.one; }

    void Leave()
    {
        over = false;
        if (shown != null && cur == shown) cur = null;
        shown = null;
        if (cb != null) cb(false);
    }

    void Update()
    {
        if (over && tip != shown)                       // текст подсказки сменился, пока курсор на месте
        {
            if (shown != null && cur == shown) cur = tip;
            else if (tip != null && cur == null) cur = tip;
            shown = tip;
        }
        if (grow <= 0f) return;
        k = Mathf.MoveTowards(k, over ? 1f : 0f, Time.unscaledDeltaTime / 0.1f);
        transform.localScale = Vector3.one * (1f + grow * UiDraw.EaseOutCubic(k));
    }
}

// Правая кнопка мыши: быстрое действие (снять питомца со слота, взять или снять с карточки). Левый клик обрабатывает Button.
public class PetRightClick : MonoBehaviour, IPointerClickHandler
{
    public System.Action cb;

    public void OnPointerClick(PointerEventData e)
    {
        if (e.button == PointerEventData.InputButton.Right && cb != null) cb();
    }
}
