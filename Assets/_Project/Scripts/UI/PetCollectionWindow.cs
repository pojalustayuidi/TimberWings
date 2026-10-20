// Assets/_Project/Scripts/UI/PetCollectionWindow.cs (НЕ в Editor)
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

public class PetCollectionWindow : MonoBehaviour
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
        if (FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        Build();
    }

    static bool OtherWindowOpen()
    {
        var u = UpgradeWindow.Instance;
        if (u != null && u.IsOpen) return true;
        var p = PlankWorkshopWindow.Instance;
        if (p != null && p.IsOpen) return true;
        var sw = SellWindow.Instance;
        if (sw != null && sw.IsOpen) return true;
        var ww = WorkersWindow.Instance;
        if (ww != null && ww.IsOpen) return true;
        var bw = BuildWindow.Instance;
        if (bw != null && bw.IsOpen) return true;
        if (PetCrateWindow.IsOpen || AchievementsWindow.IsOpen) return true;
        return HudMinimap.BigMapOpen || EndingSequence.Active || SettingsMenu.IsOpen;
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

    // ============================================================ действия
    void OnSlotClick(int i)
    {
        var pm = PetManager.Instance;
        if (pm == null) return;

        PetInstance e = pm.GetEquipped(i);
        if (e != null) { SelectUid(e.uid, true, true); return; }

        if (!pm.IsSlotOpen(i))
        {
            if (i == PetManager.SkillSlotIndex) { Toast("Этот слот открывает навык «Верный друг»"); return; }
            int lvl = i < GameBalance.PetSlotLevels.Length ? GameBalance.PetSlotLevels[i] : 0;
            Toast("Этот слот откроется на " + lvl + " уровне");
            return;
        }

        // пустой открытый слот: ставим (или переносим) туда выбранного питомца
        if (tab != Tab.Collection) { Toast("Открой вкладку «Коллекция», выбери питомца, и он встанет в слот", ToastKind.Info); return; }
        if (selectedUid >= 0 && FindInstance(selectedUid) != null) { QuickSlot(i); return; }
        Toast("Выбери питомца в коллекции, и он встанет в слот", ToastKind.Info);
    }

    // крестик на слоте и ПКМ по слоту: снять питомца (остаётся в коллекции)
    void OnSlotUnequip(int i)
    {
        var pm = PetManager.Instance;
        if (pm == null) return;
        if (pm.Unequip(i)) { SfxManager.Play("ui", 0.4f, 0.9f); Toast("Питомец снят: он в коллекции", ToastKind.Info); }
    }

    // ПКМ по карточке: выбрать и сразу взять или снять
    void OnCardRightClick(PoolCard pc)
    {
        if (tab != Tab.Collection || pc.index < 0 || pc.index >= items.Count) return;
        Entry e = items[pc.index];
        if (e.inst == null) return;
        SelectUid(e.inst.uid, true, false);
        ToggleSelectedEquip();
    }

    void OnEquipClick()
    {
        var pm = PetManager.Instance;
        if (pm == null || selectedUid < 0) return;
        if (pm.Equip(selectedUid)) { SfxManager.Play("coin", 0.7f, 1.1f); return; }

        // слоты заняты: ставим на место того, кого вытесняет питомец (его показывает дельта)
        PetInstance sel = FindInstance(selectedUid);
        PetInstance rep = pm.WouldReplace(sel);
        if (rep != null && pm.EquipReplacing(selectedUid, rep.uid)) SfxManager.Play("coin", 0.7f, 1.1f);
        else Toast("Все слоты заняты");
    }

    void OnMergeClick()
    {
        var pm = PetManager.Instance;
        PetInstance sel = FindInstance(selectedUid);
        if (pm == null || sel == null) return;
        int u = pm.Merge(sel.data);
        if (u < 0) { Toast("Слить нельзя"); return; }
        selectedUid = u;
        revealPending = true;                                       // покажем получившегося питомца, даже если список перестроится
        SfxManager.Play("coin", 0.8f, 1.3f);
        if (fx != null) fx.Burst(InnerToFx(DetX + DetW * 0.5f, DetY + 130f), new Color32(0xFF, 0xD2, 0x4A, 255), 22, 320f, 26f, 300f, 0.9f);
        Toast("Слияние: +1 звезда", ToastKind.Good);
    }

    void OnAutoClick()
    {
        var pm = PetManager.Instance;
        if (pm == null) return;
        if (pm.AutoEquipBest()) { SfxManager.Play("coin", 0.7f, 1.1f); Toast("Подобраны лучшие питомцы. Ctrl+Z: отменить", ToastKind.Good); }
        else Toast(pm.Owned.Count == 0 ? "Пока нет питомцев" : "Уже стоят лучшие", pm.Owned.Count == 0 ? ToastKind.Warn : ToastKind.Info);
    }

    void SetTab(Tab t)
    {
        if (tab == t) return;
        tab = t;
        contentsRt.anchoredPosition = Vector2.zero;
        armedUntil = 0f;
        animateNext = true;
        revealPending = t == Tab.Collection;                        // вернулись в коллекцию: показать выбранного
        SfxManager.Play("ui", 0.3f, 1.1f);
        Populate();
    }

    void OnSortClick()
    {
        sortMode = (SortMode)(((int)sortMode + 1) % SortNames.Length);
        contentsRt.anchoredPosition = Vector2.zero;
        animateNext = true;
        revealPending = true;
        SfxManager.Play("ui", 0.3f, 1.1f);
        Populate();
    }

    // следующая характеристика, которая есть у кого-то из питомцев; после последней снова «Все»
    void OnFilterClick()
    {
        var pm = PetManager.Instance;
        int next = filterStat;
        for (int guard = 0; guard < 9; guard++)
        {
            next++;
            if (next > 6) { next = -1; break; }
            bool any = false;
            if (pm != null) for (int i = 0; i < pm.Owned.Count; i++) if ((int)pm.Owned[i].data.stat == next) { any = true; break; }
            if (any) break;
        }
        filterStat = next;
        contentsRt.anchoredPosition = Vector2.zero;
        animateNext = true;
        revealPending = true;
        SfxManager.Play("ui", 0.3f, 1.1f);
        Populate();
    }

    void OnUnequipClick()
    {
        var pm = PetManager.Instance;
        if (pm == null || selectedUid < 0) return;
        for (int i = 0; i < PetManager.MaxSlots; i++)
        {
            var e = pm.GetEquipped(i);
            if (e != null && e.uid == selectedUid)
            {
                if (pm.Unequip(i)) SfxManager.Play("ui", 0.4f, 0.9f);
                return;
            }
        }
    }

    void OnReleaseClick()
    {
        var pm = PetManager.Instance;
        if (pm == null || selectedUid < 0) return;

        if (armedUntil <= 0f)                                       // первое нажатие: просим подтвердить
        {
            PetInstance cur = FindInstance(selectedUid);
            if (cur != null && pm.CanMerge(cur.data)) Toast("Дубликаты лучше слить в звезду: кнопка выше", ToastKind.Info);
            armedUntil = Time.unscaledTime + ArmTime;
            RefreshDetail();
            SfxManager.Play("hint", 0.4f, 0.9f);
            return;
        }

        armedUntil = 0f;
        int uid = selectedUid;
        int next = NeighborUid(uid);                                // выбор переходит к соседней карточке, а не прыгает в начало списка
        selectedUid = -1;
        if (pm.Release(uid))
        {
            selectedUid = next;
            revealPending = true;
            Toast("Питомец отпущен", ToastKind.Info);
            SfxManager.Play("ui", 0.5f, 0.7f);
        }
        else selectedUid = uid;
    }

    // питомец рядом с uid в текущем списке (следующий, иначе предыдущий); -1, если больше никого нет
    int NeighborUid(int uid)
    {
        int idx = -1;
        for (int i = 0; i < sorted.Count; i++) if (sorted[i].uid == uid) { idx = i; break; }
        if (idx < 0) return -1;
        if (idx + 1 < sorted.Count) return sorted[idx + 1].uid;
        if (idx > 0) return sorted[idx - 1].uid;
        return -1;
    }

    void Toast(string s, ToastKind kind = ToastKind.Warn)
    {
        toastText = s;
        toastKind = kind;
        toastStart = Time.unscaledTime;
        toastUntil = Time.unscaledTime + (kind == ToastKind.Warn ? 3f : 2.5f);
        if (kind != ToastKind.Good) SfxManager.Play("ui", 0.3f, 0.8f);   // у «получилось» уже есть свой звук
    }

    // ============================================================ заполнение
    int Compare(PetInstance a, PetInstance b)
    {
        int r;
        switch (sortMode)
        {
            case SortMode.Stat:
                r = ((int)a.data.stat).CompareTo((int)b.data.stat);
                if (r != 0) return r;
                r = b.Bonus.CompareTo(a.Bonus);
                if (r != 0) return r;
                break;
            case SortMode.Name:
                r = string.CompareOrdinal(PetUi.NameOf(a.data), PetUi.NameOf(b.data));
                if (r != 0) return r;
                break;
            case SortMode.Newest:
                if (a.isNew != b.isNew) return a.isNew ? -1 : 1;
                return b.uid.CompareTo(a.uid);
        }
        // остальное одинаково для всех режимов: редкость от Легендарных, звёзды, имя, номер
        r = ((int)b.data.rarity).CompareTo((int)a.data.rarity);
        if (r != 0) return r;
        r = b.stars.CompareTo(a.stars);
        if (r != 0) return r;
        r = string.CompareOrdinal(PetUi.NameOf(a.data), PetUi.NameOf(b.data));
        if (r != 0) return r;
        return a.uid.CompareTo(b.uid);
    }

    static int CompareSpecies(PetData a, PetData b)
    {
        int r = ((int)b.rarity).CompareTo((int)a.rarity);
        if (r != 0) return r;
        return string.CompareOrdinal(PetUi.NameOf(a), PetUi.NameOf(b));
    }

    PetInstance FindInstance(int uid)
    {
        if (uid < 0) return null;
        for (int i = 0; i < sorted.Count; i++) if (sorted[i].uid == uid) return sorted[i];
        return null;
    }

    static string StatShort(PetStat s)
    {
        switch (s)
        {
            case PetStat.ChopPower: return "Рубка";
            case PetStat.MoveSpeed: return "Бег";
            case PetStat.Capacity: return "Рюкзак";
            case PetStat.DoubleYield: return "Двойная добыча";
            case PetStat.Haggle: return "Цена";
            case PetStat.SawSpeed: return "Пила";
            default: return "Рабочие";
        }
    }

    // пересобирает список (данные, не объекты), слоты, бонусы и описание. Зовётся при открытии, смене вида и при PetManager.Changed
    void Populate()
    {
        dirty = false;
        var pm = PetManager.Instance;
        float keepY = contentsRt.anchoredPosition.y;

        sorted.Clear();
        if (pm != null)
            for (int i = 0; i < pm.Owned.Count; i++)
            {
                var o = pm.Owned[i];
                if ((filterStat < 0 || (int)o.data.stat == filterStat) && (rarFilter < 0 || (int)o.data.rarity == rarFilter)) sorted.Add(o);
            }
        sorted.Sort(Compare);

        items.Clear();
        if (tab == Tab.Collection)
        {
            for (int i = 0; i < sorted.Count; i++) items.Add(new Entry { inst = sorted[i], data = sorted[i].data });
        }
        else if (pm != null && pm.database != null)
        {
            var species = new List<PetData>();
            for (int i = 0; i < pm.database.pets.Count; i++) if (pm.database.pets[i] != null) species.Add(pm.database.pets[i]);
            species.Sort(CompareSpecies);
            for (int i = 0; i < species.Count; i++) items.Add(new Entry { data = species[i], locked = !pm.IsDiscovered(species[i]) });
        }

        // выбранный пропал или ещё не выбран: берём первого из слотов (если он прошёл фильтр), иначе первого в списке
        if (FindInstance(selectedUid) == null)
        {
            selectedUid = -1;
            if (pm != null)
                for (int s = 0; s < PetManager.MaxSlots && selectedUid < 0; s++)
                {
                    var e = pm.GetEquipped(s);
                    if (e != null && FindInstance(e.uid) != null) selectedUid = e.uid;
                }
            if (selectedUid < 0 && sorted.Count > 0) selectedUid = sorted[0].uid;
        }

        int rows = (items.Count + Cols - 1) / Cols;
        float h = 8f + rows * PitchY + 8f;
        contentsRt.sizeDelta = new Vector2(0f, h);
        float maxY = Mathf.Max(0f, h - (GridH - 16f));
        contentsRt.anchoredPosition = new Vector2(0f, Mathf.Clamp(keepY, 0f, maxY));

        // выбранного показываем сразу: после «Отпустить», слияния, смены сортировки или вкладки
        if (revealPending && tab == Tab.Collection)
        {
            revealPending = false;
            for (int i = 0; i < items.Count; i++)
                if (items[i].inst != null && items[i].inst.uid == selectedUid) { EnsureVisible(8f + (i / Cols) * PitchY); break; }
        }

        bool none = items.Count == 0;
        bool filtered = tab == Tab.Collection && pm != null && pm.Owned.Count > 0;
        emptyGrid.gameObject.SetActive(none);
        if (resetBtn != null) resetBtn.gameObject.SetActive(none && filtered);
        if (none)
            Set(emptyGrid, tab == Tab.Book ? "База питомцев пуста"
                : (filtered ? "Нет питомцев с такими фильтрами"
                    : "Пока нет ни одного питомца.\nОткрывай клети: Лесная, Речная и Небесная ждут в лагере."));

        detRoot.gameObject.SetActive(tab == Tab.Collection);
        bookRoot.gameObject.SetActive(tab == Tab.Book);

        RefreshHeader();
        RefreshBonus();
        LayoutCells(true);
        SelectUid(selectedUid, false, false);            // подсветка, слоты и описание (выбор переживает смену вкладки)
        if (tab == Tab.Book) RefreshBook();
    }

    // ------------------------------------------------------------ пул карточек
    // Видны максимум 4 строки, поэтому живут PoolSize карточек: индекс i всегда достаётся pool[i % PoolSize].
    // При прокрутке привязываются только вновь появившиеся индексы.
    void LayoutCells(bool rebind)
    {
        layoutDirty = false;
        float y = contentsRt.anchoredPosition.y;
        lastScrollY = y;

        int firstRow = Mathf.Max(0, Mathf.FloorToInt((y - 8f) / PitchY));
        int first = firstRow * Cols;
        int last = Mathf.Min(items.Count, first + PoolSize);

        for (int k = 0; k < pool.Count; k++)
        {
            var pc = pool[k];
            if (pc.index >= 0 && (pc.index < first || pc.index >= last || pc.index >= items.Count))
            {
                pc.index = -1;
                pc.view.rt.gameObject.SetActive(false);
            }
        }
        bool wave = animateNext && rebind;
        for (int i = first; i < last; i++)
        {
            var pc = pool[i % PoolSize];
            bool fresh = pc.index != i;
            if (!fresh && !rebind) continue;
            Bind(pc, i);
            if (wave) StartAppear(pc, (i - first) * 0.03f);              // открытие, смена вкладки или фильтра: волной
            else if (fresh) StartAppear(pc, 0f);                          // новая строка при прокрутке: быстро
        }
        if (wave) animateNext = false;
    }

    static void StartAppear(PoolCard pc, float delay)
    {
        pc.appear = 0f;
        pc.delay = delay;
        if (pc.cg != null) pc.cg.alpha = 0f;
    }

    void Bind(PoolCard pc, int i)
    {
        var pm = PetManager.Instance;
        Entry e = items[i];
        pc.index = i;
        pc.view.rt.gameObject.SetActive(true);

        float x = CellLeft + (i % Cols) * PitchX;
        float cy = 8f + (i / Cols) * PitchY;
        Anch(pc.view.rt, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(x + CardW * 0.5f, -(cy + CardH * 0.5f)), new Vector2(CardW, CardH));

        if (e.locked)
        {
            pc.view.SetLocked(e.data);
            pc.mark.SetActive(false);
        }
        else
        {
            pc.view.Set(e.data, e.inst != null ? e.inst.stars : 0, e.inst != null && e.inst.isNew);
            pc.mark.SetActive(e.inst != null && pm != null && pm.IsEquipped(e.inst.uid));
        }
        pc.sc = IsSelectedEntry(e) ? 1.06f : 1f;
        pc.view.rt.localScale = new Vector3(pc.sc, pc.sc, 1f);
    }

    bool IsSelectedEntry(Entry e)
    {
        return tab == Tab.Collection && e.inst != null && selectedUid >= 0 && e.inst.uid == selectedUid;
    }

    void OnCardClick(PoolCard pc)
    {
        bool dbl = pc.index >= 0 && pc.index == lastClickIdx && Time.unscaledTime - lastClickT < 0.35f && tab == Tab.Collection;
        lastClickIdx = pc.index;
        lastClickT = Time.unscaledTime;
        OnCardClickCore(pc);
        if (dbl) ToggleSelectedEquip();
    }

    // Enter и двойной клик: питомец в слот, а если уже там, то снять
    void ToggleSelectedEquip()
    {
        var pm = PetManager.Instance;
        if (pm == null || selectedUid < 0) return;
        if (pm.IsEquipped(selectedUid)) OnUnequipClick(); else OnEquipClick();
    }

    void OnCardClickCore(PoolCard pc)
    {
        if (pc.index < 0 || pc.index >= items.Count) return;
        Entry e = items[pc.index];
        if (e.locked) { Toast("Ещё не найден: " + PetText.Rarity(e.data.rarity) + " питомец"); return; }
        if (e.inst == null)    // книга: открытый вид
        {
            Toast(PetUi.NameOf(e.data) + ": " + PetText.Rarity(e.data.rarity) + ", " + PetText.Stat(e.data.stat));
            return;
        }
        SelectUid(e.inst.uid, true, false);
    }

    // 3D-снимки рисуются в фоне: карточки, которым снимка не хватило, перерисовываем, когда он появится
    void RecheckPortraits()
    {
        for (int k = 0; k < pool.Count; k++)
            if (pool[k].index >= 0 && pool[k].view.pendingPortrait) Bind(pool[k], pool[k].index);
        if (detCard != null && detCard.pendingPortrait && tab == Tab.Collection) RefreshDetail();
        if (presetPortraitPending) RefreshPresets();
    }

    // зелёная галочка в правом нижнем углу карточки: питомец сейчас в слоте
    static GameObject AddEquippedMark(PetCardView cv)
    {
        var mid = new Vector2(0.5f, 0.5f);
        var m = UiKit.MakeImage(cv.rt, "Equipped", UiKit.Medallion, false);
        m.color = UiKit.Good;
        Anch(m.rectTransform, new Vector2(1f, 0f), mid, new Vector2(-26f, 62f), new Vector2(38f, 38f));
        var ic = UiKit.MakeIcon(m.transform, UiIcon.Check, 24f, "Check");
        ic.rectTransform.anchoredPosition = Vector2.zero;
        m.gameObject.SetActive(false);
        return m.gameObject;
    }

    void SelectUid(int uid, bool user, bool reveal)
    {
        selectedUid = uid;
        armedUntil = 0f;
        var pm = PetManager.Instance;
        if (uid != lastDetUid) { lastDetUid = uid; detT = 0f; bubbleT = -1f; }       // описание «переезжает» на нового питомца

        // игрок сам выбрал питомца: бейдж NEW гаснет
        if (user && pm != null && uid >= 0)
        {
            var si = FindInstance(uid);
            if (si != null && si.isNew) pm.MarkSeen(uid);
        }

        int idx = -1;
        if (tab == Tab.Collection && uid >= 0)
            for (int i = 0; i < items.Count; i++) if (items[i].inst != null && items[i].inst.uid == uid) { idx = i; break; }

        for (int k = 0; k < pool.Count; k++)
        {
            var pc = pool[k];
            if (pc.index < 0) continue;
            bool on = pc.index == idx;
            if (on && user) Bind(pc, pc.index);           // обновить NEW и звёзды (масштаб сглаживает AnimateCards)
        }

        bool frameWas = selFrame.gameObject.activeSelf;
        selFrame.gameObject.SetActive(idx >= 0);
        if (idx >= 0)
        {
            float x = CellLeft + (idx % Cols) * PitchX;
            float cy = 8f + (idx / Cols) * PitchY;
            selTarget = new Vector2(x + CardW * 0.5f, -(cy + CardH * 0.5f));
            if (!frameWas) selFrame.rectTransform.anchoredPosition = selTarget;   // появилась: сразу на месте, дальше плавно
            if (reveal) { EnsureVisible(cy); layoutDirty = true; }
        }

        RefreshSlots();
        RefreshDetail();
        if (user) SfxManager.Play("ui", 0.3f, 1.2f);
        if (user && uid >= 0 && tab == Tab.Collection && Random.value < 0.35f) ShowBubble(GreetPhrases[Random.Range(0, GreetPhrases.Length)]);
    }

    // прокручивает коллекцию так, чтобы карточка (верх = top) была в окне
    void EnsureVisible(float top)
    {
        float vh = GridH - 16f;
        float y = contentsRt.anchoredPosition.y;
        if (top < y) y = top - 8f;
        else if (top + CardH > y + vh) y = top + CardH - vh + 8f;
        float maxY = Mathf.Max(0f, contentsRt.sizeDelta.y - vh);
        contentsRt.anchoredPosition = new Vector2(0f, Mathf.Clamp(y, 0f, maxY));
        if (scroll != null) scroll.velocity = Vector2.zero;
    }

    void RefreshHeader()
    {
        var pm = PetManager.Instance;
        int n = pm != null ? pm.Owned.Count : 0;
        Set(countText, n.ToString());

        int found = pm != null ? pm.DiscoveredCount : 0, total = pm != null ? pm.TotalSpecies : 0;
        Set(tabColLabel, "Коллекция  " + (sorted.Count != n ? sorted.Count + "/" + n : n.ToString()));
        Set(tabBookLabel, "Книга  " + found + "/" + total);
        tabColImg.color = tab == Tab.Collection ? Color.white : new Color(0.62f, 0.62f, 0.62f, 1f);
        tabBookImg.color = tab == Tab.Book ? Color.white : new Color(0.62f, 0.62f, 0.62f, 1f);

        bool col = tab == Tab.Collection;
        Set(sortLabel, "Сорт.: " + SortNames[(int)sortMode]);
        Set(filterLabel, "Фильтр: " + (filterStat < 0 ? "все" : StatShort((PetStat)filterStat)));
        SetBtn(sortBtn, sortLabel, col);
        SetBtn(filterBtn, filterLabel, col);
        for (int j = 0; j < 5; j++)
        {
            rarImg[j].color = (rarFilter == j - 1 && col) ? Color.white : new Color(0.62f, 0.62f, 0.62f, 1f);
            if (rarBtn[j] != null) rarBtn[j].interactable = col;
        }
    }

    void RefreshSlots()
    {
        var pm = PetManager.Instance;
        int unlocked = pm != null ? pm.SlotCount : 1;
        int used = pm != null ? pm.EquippedCount : 0;
        Set(slotsLabel, "Команда  <size=65%><color=#" + PetUi.Hex(Dim) + ">" + used + " / " + unlocked + "</color></size>");

        for (int i = 0; i < slots.Length; i++)
        {
            var s = slots[i];
            bool isOpen = pm != null ? pm.IsSlotOpen(i) : i == 0;
            PetInstance e = pm != null ? pm.GetEquipped(i) : null;
            bool has = e != null;

            s.card.rt.gameObject.SetActive(has);
            if (s.xBtn != null) s.xBtn.SetActive(has);
            if (s.hover != null)
            {
                int lv = i < GameBalance.PetSlotLevels.Length ? GameBalance.PetSlotLevels[i] : 0;
                s.hover.tip = has ? "<b>" + PetUi.NameOf(e.data) + "</b>: клик выбирает  ·  крестик или ПКМ снимает  ·  цифра " + (i + 1) + " ставит выбранного сюда"
                    : isOpen ? "Свободный слот " + (i + 1) + ": выбери питомца и нажми сюда (или цифру " + (i + 1) + ")"
                    : i == PetManager.SkillSlotIndex ? "Закрыт: этот слот открывает навык «Верный друг»"
                    : "Закрыт: откроется на " + lv + " уровне";
            }
            s.baseCol = has ? Color.Lerp(Color.white, PetUi.RarityAccent(e.data.rarity), 0.45f) : Color.white;
            s.baseScale = has && e.uid == selectedUid ? 1.06f : 1f;
            int curUid = has ? e.uid : -1;
            if (lastUid[i] != -2 && curUid >= 0 && curUid != lastUid[i])
            {
                s.pop = 1f;
                if (fx != null)
                    fx.Burst(InnerToFx(TeamX + 10f + (i % 2) * (SlotW + SlotGapX) + SlotW * 0.5f, TeamY + SlotY + (i / 2) * (SlotH + SlotGapY) + SlotH * 0.5f),
                             PetUi.RarityAccent(e.data.rarity), 16, 280f, 22f, 320f, 0.75f);
            }
            lastUid[i] = curUid;
            if (has)
            {
                s.card.Set(e.data, e.stars, false);
                s.card.rt.localScale = e.uid == selectedUid ? new Vector3(1.06f, 1.06f, 1f) : Vector3.one;
            }

            s.icon.gameObject.SetActive(!has);
            s.icon.sprite = UiKit.Icon(isOpen ? UiIcon.Paw : UiIcon.Lock);
            s.icon.color = new Color(1f, 1f, 1f, isOpen ? 0.28f : 0.5f);

            s.label.gameObject.SetActive(!has);
            if (!has)
            {
                int lvl = i < GameBalance.PetSlotLevels.Length ? GameBalance.PetSlotLevels[i] : 0;
                Set(s.label, isOpen ? "Пусто" : (i == PetManager.SkillSlotIndex ? "Навык" : "Ур. " + lvl));
            }
        }
        if (autoBtn != null) SetBtn(autoBtn, autoLabel, pm != null && pm.Owned.Count > 0);
        if (unequipAllBtn != null) SetBtn(unequipAllBtn, unequipAllLabel, pm != null && pm.EquippedCount > 0);
        if (undoBtn != null) SetBtn(undoBtn, undoLabel, pm != null && pm.CanUndoLoadout);
        RefreshPresets();
    }

    void RefreshBonus()
    {
        var pm = PetManager.Instance;
        PetBonusSet b = pm != null ? pm.Bonus : PetBonusSet.None;
        int n = 0;
        for (int k = 0; k < 7; k++)
        {
            PetStat st = (PetStat)k;
            string val = null;
            float frac = -1f;
            if (st == PetStat.Capacity)
            {
                if (b.capacityLogs > 0)
                    val = "+" + b.capacityLogs + " " + UiDraw.Plural(b.capacityLogs, "бревно", "бревна", "бревен");
            }
            else
            {
                float v = b.Get(st);
                if (v > 0.0001f)
                {
                    val = "+" + PetUi.Pct(v) + "%";
                    frac = Mathf.Clamp01(v / GameBalance.PetBonusCap);
                    if (v >= GameBalance.PetBonusCap - 1e-4f) val += " <size=70%>макс.</size>";
                }
            }
            if (val == null) continue;

            bonusIcon[n].gameObject.SetActive(true);
            bonusIcon[n].sprite = UiKit.Icon(PetUi.StatIcon(st));
            bonusText[n].gameObject.SetActive(true);
            bonusBarTrack[n].gameObject.SetActive(frac >= 0f);
            if (frac >= 0f)
            {
                bonusBarTgt[n] = Mathf.Max(0.02f, frac);                       // дальше доезжает AnimateMisc
                if (!bonusSigInit) bonusBarCur[n] = bonusBarTgt[n];            // первая отрисовка после открытия: сразу на месте
                bonusBarFill[n].color = frac >= 0.999f ? UiKit.BrassHi : UiKit.Good;
            }
            Set(bonusText[n], "<b>" + PetText.Stat(st) + "</b>  <color=#" + PetUi.Hex(UiKit.Good) + ">" + val + "</color>");
            n++;
        }
        // бонусы изменились (взяли, сняли, слили): панель коротко вспыхивает
        int sig = b.capacityLogs;
        for (int q = 0; q < 7; q++) sig = unchecked(sig * 31 + Mathf.RoundToInt(b.Get((PetStat)q) * 10000f));
        if (bonusSigInit && sig != bonusSig) bonusFlash = 1f;
        bonusSig = sig;
        bonusSigInit = true;

        for (int i = n; i < bonusIcon.Length; i++)
        {
            bonusIcon[i].gameObject.SetActive(false);
            bonusText[i].gameObject.SetActive(false);
            bonusBarTrack[i].gameObject.SetActive(false);
        }
        bonusEmpty.gameObject.SetActive(n == 0);
    }

    // ------------------------------------------------------------ дельта: что изменится, если взять питомца
    static float StatValue(PetBonusSet b, PetStat st) { return st == PetStat.Capacity ? b.capacityLogs : b.Get(st); }

    static string StatValueText(PetStat st, PetBonusSet b)
    {
        return st == PetStat.Capacity ? "+" + b.capacityLogs : "+" + PetUi.Pct(b.Get(st)) + "%";
    }

    // «Скорость рубки +3% -> +5%»: «после» зелёное, если стало лучше, красноватое, если хуже
    static string DeltaLine(PetStat st, PetBonusSet before, PetBonusSet after)
    {
        float a = StatValue(before, st), b = StatValue(after, st);
        Color col = b > a + 1e-4f ? UiKit.Good : (b < a - 1e-4f ? UiKit.Warn : Dim);
        string unit = st == PetStat.Capacity ? " " + UiDraw.Plural(after.capacityLogs, "бревно", "бревна", "бревен") : "";
        return "<b>" + PetText.Stat(st) + "</b>  " + StatValueText(st, before) + Arrow
            + "<color=#" + PetUi.Hex(col) + ">" + StatValueText(st, after) + "</color>" + unit;
    }

    static string F2(float v) { return v.ToString("0.00", CultureInfo.InvariantCulture); }

    // живое значение у игрока (через PlayerUpgrades.Calc): «Рубка: 3.00 с -> 2.85 с». Пусто, если игрока ещё нет или стат не считается тут
    static string LiveLine(PetStat st, PetBonusSet before, PetBonusSet after)
    {
        var pu = PlayerUpgrades.Instance;
        if (pu == null) return "";
        TalentBonus b0 = pu.Bonus;
        TalentBonus b1 = PetBonus.Rebase(b0, before, after);

        UpgradeType ut;
        switch (st)
        {
            case PetStat.ChopPower: ut = UpgradeType.ChopPower; break;
            case PetStat.MoveSpeed: ut = UpgradeType.MoveSpeed; break;
            case PetStat.Capacity: ut = UpgradeType.Capacity; break;
            case PetStat.DoubleYield: ut = UpgradeType.DoubleYield; break;
            case PetStat.Haggle: ut = UpgradeType.Haggle; break;
            case PetStat.SawSpeed: ut = UpgradeType.SawSpeed; break;
            default: return "";                  // доход рабочих считает WorkerManager
        }
        int lvl = pu.Level(ut);
        float v0 = PlayerUpgrades.Calc(ut, lvl, b0), v1 = PlayerUpgrades.Calc(ut, lvl, b1);

        string t0, t1, name;
        switch (st)
        {
            case PetStat.ChopPower: name = "Рубка"; t0 = F2(v0) + " с"; t1 = F2(v1) + " с"; break;
            case PetStat.SawSpeed: name = "Пила"; t0 = F2(v0) + " с"; t1 = F2(v1) + " с"; break;
            case PetStat.MoveSpeed: name = "Бег"; t0 = "x" + F2(v0); t1 = "x" + F2(v1); break;
            case PetStat.Haggle: name = "Цена продажи"; t0 = "x" + F2(v0); t1 = "x" + F2(v1); break;
            case PetStat.DoubleYield: name = "Двойная добыча"; t0 = Mathf.RoundToInt(v0 * 100f) + "%"; t1 = Mathf.RoundToInt(v1 * 100f) + "%"; break;
            default:
                int c0 = Mathf.RoundToInt(v0), c1 = Mathf.RoundToInt(v1);
                name = "Рюкзак"; t0 = c0.ToString(); t1 = c1 + " " + UiDraw.Plural(c1, "бревно", "бревна", "бревен"); break;
        }
        // для времени «лучше» = меньше
        bool better = (st == PetStat.ChopPower || st == PetStat.SawSpeed) ? v1 < v0 - 1e-4f : v1 > v0 + 1e-4f;
        bool worse = (st == PetStat.ChopPower || st == PetStat.SawSpeed) ? v1 > v0 + 1e-4f : v1 < v0 - 1e-4f;
        Color col = better ? UiKit.Good : (worse ? UiKit.Warn : Dim);
        return "<color=#" + PetUi.Hex(Soft) + ">" + name + ":</color> " + t0 + Arrow + "<color=#" + PetUi.Hex(col) + ">" + t1 + "</color>";
    }

    static readonly string[] StageNames = { "малыш", "подросток", "взрослый" };

    void RefreshDetail()
    {
        var pm = PetManager.Instance;
        PetInstance sel = FindInstance(selectedUid);
        bool has = sel != null && pm != null && tab == Tab.Collection;
        RefreshPat();

        detCard.rt.gameObject.SetActive(has);
        detName.gameObject.SetActive(has);
        detRarity.gameObject.SetActive(has);
        detExtra.gameObject.SetActive(has);
        detStat.gameObject.SetActive(has);
        detHead.gameObject.SetActive(has);
        detD1.gameObject.SetActive(has);
        detD2.gameObject.SetActive(has);
        detLive.gameObject.SetActive(has);
        detNote.gameObject.SetActive(has);
        releaseBtn.gameObject.SetActive(has);
        detEmpty.gameObject.SetActive(!has);
        if (!has)
        {
            equipBtn.gameObject.SetActive(false);
            unequipBtn.gameObject.SetActive(false);
            mergeBtn.gameObject.SetActive(false);
            Set(detEmpty, sorted.Count == 0 ? "" : "Выбери питомца");
            return;
        }

        PetData d = sel.data;
        Color acc = PetUi.RarityAccent(d.rarity);
        int stage = Mathf.Clamp(pm.GrowthStage, 0, 2);
        detCard.LiveStage = stage;                       // стадия роста: малыш, подросток, взрослый
        detCard.Set(d, sel.stars, false);
        detName.color = acc;
        Set(detName, PetUi.NameOf(d));
        detRarity.color = acc;
        Set(detRarity, PetText.Rarity(d.rarity) + "  ·  " + StageNames[stage]);
        Set(detStat, PetUi.StatLine(d, sel.stars));

        // звёзды и копии
        int copies = pm.CountOf(d);
        bool canMerge = pm.CanMerge(d);
        string extra = "";
        if (sel.stars > 0)
            extra = "<color=#FFD24A>Звёзды: " + sel.stars + " из " + GameBalance.PetStarMax + "</color>  <size=80%>(бонус +"
                + Mathf.RoundToInt((GameBalance.PetStarMult(sel.stars) - 1f) * 100f) + "%)</size>";
        else if (copies > 1)
            extra = "Копий: <b>x" + copies + "</b>  <size=80%>(слияние от " + GameBalance.PetMergeCount + ")</size>";
        Set(detExtra, extra);

        bool eq = pm.IsEquipped(sel.uid);
        bool free = pm.EquippedCount < pm.SlotCount;

        // дельта относительно текущих бонусов
        PetBonusSet before = pm.Bonus, after;
        PetInstance rep = null;
        string head;
        if (eq)
        {
            after = pm.PreviewBonus(null, sel);
            head = "Если снять:";
        }
        else
        {
            rep = pm.WouldReplace(sel);
            after = pm.PreviewBonus(sel, rep);
            head = rep != null ? "Заменит «" + PetUi.NameOf(rep.data) + "»:" : "Если взять:";
        }
        Set(detHead, head);
        Set(detD1, DeltaLine(d.stat, before, after));
        bool second = rep != null && rep.data.stat != d.stat;
        Set(detD2, second ? DeltaLine(rep.data.stat, before, after) : "");
        Set(detLive, LiveLine(d.stat, before, after));

        equipBtn.gameObject.SetActive(!eq);
        unequipBtn.gameObject.SetActive(eq);
        SetBtn(equipBtn, equipLabel, free || rep != null);
        Set(equipLabel, free ? "Взять с собой" : "Заменить");
        if (equipIco != null) equipIco.sprite = PetIcons.Get(free ? PetIco.Plus : PetIco.Swap);

        if (eq) Set(detNote, "<color=#" + PetUi.Hex(UiKit.Good) + ">Сейчас с тобой</color>");
        else if (free) Set(detNote, "");
        else if (pm.SlotCount < PetManager.SkillSlotIndex && pm.SlotCount < GameBalance.PetSlotLevels.Length)
            Set(detNote, "<color=#" + PetUi.Hex(UiKit.Warn) + ">Слоты заняты. Новый слот: ур. " + GameBalance.PetSlotLevels[pm.SlotCount] + "</color>");
        else if (!PetManager.SkillSlotUnlocked)
            Set(detNote, "<color=#" + PetUi.Hex(UiKit.Warn) + ">Слоты заняты. 4-й слот: навык «Верный друг»</color>");
        else Set(detNote, "");

        // слияние предлагается раньше «Отпустить»
        mergeBtn.gameObject.SetActive(canMerge);
        if (canMerge) Set(mergeLabel, "Слить " + GameBalance.PetMergeCount + " копии: +1 звезда");

        bool armed = armedUntil > 0f;
        bool precious = d.rarity >= PetRarity.Rare || sel.stars > 0;
        Set(releaseLabel, armed ? (precious ? "Отпустить навсегда?" : "Точно отпустить?") : "Отпустить");
        releaseLabel.color = armed ? UiKit.Warn : releaseColor;
        if (armed)
            Set(detNote, "<color=#" + PetUi.Hex(UiKit.Warn) + ">Питомец исчезнет навсегда"
                + (eq ? " и уйдёт из команды" : precious ? ": он ценный" : "") + "</color>");
    }

    // ------------------------------------------------------------ Книга питомцев
    void RefreshBook()
    {
        var pm = PetManager.Instance;
        if (pm == null) return;
        int found = pm.DiscoveredCount, total = pm.TotalSpecies;
        Set(bookTotal, "Открыто  <b>" + found + "/" + total + "</b>");
        bookBarFill.rectTransform.anchorMax = new Vector2(total > 0 ? Mathf.Clamp01(found / (float)total) : 0f, 1f);

        for (int r = 3; r >= 0; r--)
        {
            PetRarity rar = (PetRarity)r;
            int f, t;
            pm.RarityProgress(rar, out f, out t);
            bool done = t > 0 && f >= t;
            Color acc = PetUi.RarityAccent(rar);
            bookName[r].color = acc;
            Set(bookName[r], PetText.Rarity(rar));
            Set(bookCount[r], f + "/" + t);
            bookCount[r].color = done ? UiKit.Good : Color.white;
            bookCheck[r].gameObject.SetActive(done);
            float bonus = r < GameBalance.PetBookBonusByRarity.Length ? GameBalance.PetBookBonusByRarity[r] : 0f;
            bookBonus[r].color = done ? UiKit.Good : Dim;
            Set(bookBonus[r], (done ? "Получено: " : "За все виды: ") + "+" + PetUi.Pct(bonus) + "% ко всем");
        }
    }

    static void Set(TMP_Text t, string s) { if (t != null && t.text != s) t.text = s; }

    static void SetBtn(Button b, TMP_Text label, bool on)
    {
        if (b == null) return;
        b.interactable = on;
        if (label != null) label.color = on ? UiKit.Ink : OffText;
    }

    // ============================================================ сборка окна
    void Build()
    {
        if (built) return;

        var cgo = new GameObject("PetCollectionCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cgo.layer = 5;
        cgo.transform.SetParent(transform, false);
        var canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 31;                                   // как у окна клети: они никогда не открыты вместе
        var sc = cgo.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        sc.matchWidthOrHeight = 0.5f;

        // затемнение: только гасит клики по миру
        var ov = UiKit.MakeImage(cgo.transform, "Overlay", null, false);
        ov.color = new Color(0f, 0f, 0f, 0.6f);
        Stretch(ov.rectTransform);
        ov.raycastTarget = true;   // затемнение гасит клики по миру, но окно НЕ закрывает (промах мимо окна не должен его сбрасывать)
        overlay = ov.gameObject.AddComponent<CanvasGroup>();

        // окно: деревянная планка сверху. Пустая кнопка на самом окне не даёт клику «всплыть» до затемнения и закрыть окно
        var win = UiKit.MakePanel(ov.transform, new Vector2(WinW, WinH), true, BS, "Window");
        panel = win.rectTransform;
        Anch(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(WinW, WinH));
        AddBtn(win, () => { });
        BuildHeader();

        var content = NewRT("Content", panel);
        content.anchorMin = Vector2.zero; content.anchorMax = Vector2.one;
        content.offsetMin = new Vector2(26f * BS, 26f * BS);
        content.offsetMax = new Vector2(-26f * BS, -36f * BS);

        var inner = UiKit.MakePanel(content, new Vector2(InW, InH), false, 0.6f, "Inner");
        Stretch(inner.rectTransform);

        BuildTop(inner.transform);
        BuildGrid(inner.transform);
        BuildDetail(inner.transform);

        hintText = UiKit.MakeLabel(ov.transform, "", 22f, Dim, TextAlignmentOptions.Center, false);
        Anch(hintText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0, -(WinH * 0.5f + 12f)), new Vector2(WinW - 40f, 34f));
        PetUi.AutoSize(hintText, 14f, 22f);                          // длинная подсказка уменьшается, а не вылезает за экран

        fx = AchSparks.Create(panel, new Vector2(0.5f, 0.5f));
        hearts = PetHeartFx.Create(panel, new Vector2(0.5f, 0.5f));      // сердечки над питомцем (поверх всего окна)

        ov.gameObject.SetActive(false);
        built = true;
    }

    void BuildHeader()
    {
        // сколько питомцев всего: слева на деревянной планке
        var paw = UiKit.MakeIcon(panel, UiIcon.Paw, 36f, "PawIcon");
        TLC(paw.rectTransform, 64, 27, 36, 36);
        countText = UiKit.MakeLabel(panel, "0", 30f, WoodText, TextAlignmentOptions.Left, true);
        TL(countText.rectTransform, 90, 8, 200, 38);

        titleText = UiKit.MakeLabel(panel, "ПИТОМЦЫ", 34f, WoodText, TextAlignmentOptions.Center, true);
        Anch(titleText.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -8), new Vector2(520, 40));

        var close = UiKit.MakeButton(panel, "", UiBtn.Amber, new Vector2(64, 48), 20f, 1f);
        Anch(close.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-34, -5), new Vector2(64, 48));
        close.onClick.AddListener(DoClose);
        var x = UiKit.MakeIcon(close.transform, UiIcon.Cross, 30f, "X");
        x.rectTransform.anchoredPosition = new Vector2(0, 2);
    }

    // левая колонка «Команда»: слоты 2x2, бонусы, пресеты
    void BuildTop(Transform p)
    {
        var team = UiKit.MakeImage(p, "Team", UiKit.Slot, true, 1f);
        TL(team.rectTransform, TeamX, TeamY, TeamW, TeamH);
        team.raycastTarget = false;
        var tr = team.transform;

        var paw = UiKit.MakeIcon(tr, UiIcon.Paw, 30f, "TeamIcon");
        TLC(paw.rectTransform, 28f, 24f, 30f, 30f);
        slotsLabel = UiKit.MakeLabel(tr, "Команда", 28f, Color.white, TextAlignmentOptions.Left, true);
        TL(slotsLabel.rectTransform, 50f, 8f, 180f, 34f);
        PetUi.AutoSize(slotsLabel, 16f, 28f);

        // «Снять всех»: один клик убирает команду (U с клавиатуры, Ctrl+Z вернёт)
        unequipAllBtn = MakeBtn(tr, "Снять всех", UiBtn.Gray, TeamW - 10f - 124f, 6f, 124f, 34f, 18f, OnUnequipAllClick, out unequipAllLabel);
        BtnIco(unequipAllBtn, PetIcons.Get(PetIco.Minus), 22f, 8f);
        unequipAllBtn.gameObject.AddComponent<PetHover>().tip = "Снять всех питомцев (U). Ctrl+Z вернёт состав назад";

        for (int i = 0; i < slots.Length; i++)
        {
            var s = new SlotView();
            float x = 10f + (i % 2) * (SlotW + SlotGapX);
            float y = SlotY + (i / 2) * (SlotH + SlotGapY);

            s.bg = UiKit.MakeImage(tr, "Slot" + i, UiKit.Slot, true, 1f);
            TL(s.bg.rectTransform, x, y, SlotW, SlotH);
            int capI = i;
            AddBtn(s.bg, () => OnSlotClick(capI));
            s.hover = s.bg.gameObject.AddComponent<PetHover>();
            s.hover.grow = 0.04f;
            s.bg.gameObject.AddComponent<PetRightClick>().cb = () => OnSlotUnequip(capI);

            s.card = PetCardView.Create(s.bg.transform, SlotW, SlotH, "SlotCard", false);
            Anch(s.card.rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(SlotW, SlotH));

            s.icon = UiKit.MakeIcon(s.bg.transform, UiIcon.Paw, 56f, "Empty");
            s.icon.rectTransform.anchoredPosition = new Vector2(0f, 14f);

            s.label = UiKit.MakeLabel(s.bg.transform, "", 22f, Dim, TextAlignmentOptions.Center, true);
            Anch(s.label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(SlotW - 12f, 30f));
            PetUi.AutoSize(s.label, 14f, 22f);

            // крестик «снять»: быстрее, чем выбирать питомца и жать «Снять»
            var xb = UiKit.MakeButton(s.bg.transform, "", UiBtn.Gray, new Vector2(30f, 28f), 14f, 0.6f);
            Anch(xb.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-19f, -6f), new Vector2(30f, 28f));
            xb.onClick.AddListener(() => OnSlotUnequip(capI));
            var xi = UiKit.MakeIcon(xb.transform, UiIcon.Cross, 16f, "X");
            xi.raycastTarget = false;
            var xh = xb.gameObject.AddComponent<PetHover>();
            xh.grow = 0.1f;
            xh.tip = "Снять питомца из этого слота (он останется в коллекции)";
            s.xBtn = xb.gameObject;
            s.xBtn.SetActive(false);

            // номер слота = горячая клавиша (на телефоне не показываем)
            if (!Application.isMobilePlatform)
            {
                var kc = UiKit.MakeKeycap(s.bg.transform, (i + 1).ToString(), 30f);
                Anch(kc.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(18f, -18f), new Vector2(30f, 30f));
                kc.raycastTarget = false;
            }
            slots[i] = s;
        }

        // бонусы: до семи строк в одну колонку (крупнее прежних: место освободили наборы, переехавшие в свою колонку)
        var bp = UiKit.MakeImage(tr, "Bonus", UiKit.Slot, true, 0.6f);
        TL(bp.rectTransform, 10f, BonusY, TeamW - 20f, BonusH);
        bp.raycastTarget = false;
        bonusPanel = bp;

        var title = UiKit.MakeLabel(bp.transform, "Бонусы питомцев", 26f, UiKit.BrassHi, TextAlignmentOptions.Left, true);
        TL(title.rectTransform, 16f, 6f, 320f, 34f);

        for (int i = 0; i < 7; i++)
        {
            float y = BonusRowY + i * BonusPitch;
            bonusIcon[i] = UiKit.MakeIcon(bp.transform, UiIcon.Axe, 26f, "BIcon");
            TLC(bonusIcon[i].rectTransform, 30f, y + 14f, 26f, 26f);
            bonusText[i] = UiKit.MakeLabel(bp.transform, "", 24f, Color.white, TextAlignmentOptions.Left, false);
            TL(bonusText[i].rectTransform, 54f, y, TeamW - 20f - 66f, 28f);
            PetUi.AutoSize(bonusText[i], 13f, 24f);

            // тонкая полоска: сколько от потолка бонуса уже набрано
            var tk = UiKit.MakeImage(bp.transform, "BarTrack", null, false);
            TL(tk.rectTransform, 54f, y + 27f, TeamW - 20f - 82f, 4f);
            tk.color = new Color(0f, 0f, 0f, 0.4f);
            tk.raycastTarget = false;
            var bf = UiKit.MakeImage(tk.transform, "BarFill", null, false);
            bf.raycastTarget = false;
            bf.color = UiKit.Good;
            Stretch(bf.rectTransform);
            bonusBarTrack[i] = tk;
            bonusBarFill[i] = bf;
        }

        bonusEmpty = UiKit.MakeLabel(bp.transform, "Поставь питомца в слот, и он начнёт помогать", 24f, Dim, TextAlignmentOptions.Center, false);
        TL(bonusEmpty.rectTransform, 16f, 56f, TeamW - 52f, 150f);
        bonusEmpty.enableWordWrapping = true;

        BuildPresets(p);
    }

    // ============================================================ наборы (пресеты)
    // 3 набора питомцев: список uid по слотам. Нажал на пустой набор: сохранился текущий состав. Нажал на заполненный: питомцы встали в слоты.
    // Раньше это были три мелкие карточки 112x112 внизу левой колонки (портреты по 23 px, текст 10-16 px: «еле видно»).
    // Теперь отдельная колонка «Наборы»: три карточки 168x216, портреты 58 px по схеме слотов 2x2 (рамка цвета редкости), два главных бонуса, три кнопки
    // (подпись, перезаписать текущим составом, очистить). Галочка: этот набор сейчас надет, его рамка пульсирует.
    // Наборы хранит PetManager (секция «pets» общего сейва): они привязаны к сохранению, переживают слияние и «Отпустить».
    const int PresetCount = PetManager.PresetCount;
    const string OldPresetKey = "pet_presets_v1";      // старое хранилище в PlayerPrefs: один раз удаляется
    readonly int[] presetRes = new int[PetManager.MaxSlots];
    const float PCardX = 10f, PCardW = PrW - 20f, PCardH = 216f, PCardGap = 8f, PCardY0 = 46f;
    const float PMini = 58f, PMiniGap = 4f, PMiniY = 34f;
    static readonly string[] PresetCaptions = { "Основной", "Рубка", "Торговля", "Бригада", "Дорога", "Пила" };
    static readonly string[] AutoNames = { "Рубка", "Бег", "Рюкзак", "Добыча", "Торговля", "Пила", "Бригада" };   // по PetStat
    readonly RectTransform[] pWrap = new RectTransform[PresetCount];      // обёртка: на ней въезд при открытии (PetHover шевелит сам фон)
    readonly CanvasGroup[] pCg = new CanvasGroup[PresetCount];
    readonly Image[] pBg = new Image[PresetCount];
    readonly PetHover[] pHover = new PetHover[PresetCount];
    readonly Image[][] pFrame = new Image[PresetCount][];                 // рамка цвета редкости
    readonly Image[][] pBack = new Image[PresetCount][];                  // плашка под портретом
    readonly Image[][] pPic = new Image[PresetCount][];
    readonly Image[][] pChipIco = new Image[PresetCount][];
    readonly TMP_Text[][] pChipTxt = new TMP_Text[PresetCount][];
    readonly Image[] pCheck = new Image[PresetCount], pPlus = new Image[PresetCount];
    readonly TMP_Text[] pLabel = new TMP_Text[PresetCount], pEmpty = new TMP_Text[PresetCount];
    readonly Button[] pCap = new Button[PresetCount], pSave = new Button[PresetCount], pClear = new Button[PresetCount];
    readonly bool[] pActive = new bool[PresetCount];
    readonly float[] pIn = new float[PresetCount], pPop = new float[PresetCount];
    readonly int[] topI = new int[2];
    readonly float[] topV = new float[2];
    bool presetPortraitPending;                  // у мини-портрета ещё нет 3D-снимка: обновим позже
    Image hoverFrame;
    PoolCard hoverPc;
    float hoverK;

    // питомец по uid среди всех (а не только прошедших фильтр коллекции: иначе набор «теряет» отфильтрованных)
    static PetInstance FindOwned(int uid)
    {
        var pm = PetManager.Instance;
        if (pm == null || uid < 0) return null;
        for (int i = 0; i < pm.Owned.Count; i++) if (pm.Owned[i].uid == uid) return pm.Owned[i];
        return null;
    }

    void PresetBurst(int i)
    {
        if (fx == null) return;
        float cy = PrY + PCardY0 + i * (PCardH + PCardGap) + PMiniY + PMini + PMiniGap * 0.5f;
        fx.Burst(InnerToFx(PrX + PCardX + PCardW * 0.5f, cy), UiKit.BrassHi, 14, 240f, 20f, 260f, 0.7f);
    }

    void LoadPresets()
    {
        if (PlayerPrefs.HasKey(OldPresetKey)) { PlayerPrefs.DeleteKey(OldPresetKey); PlayerPrefs.Save(); }
    }

    bool PresetFilled(int i)
    {
        var pm = PetManager.Instance;
        return pm != null && pm.PresetFilled(i);
    }

    void OnPresetMain(int i)
    {
        if (PresetFilled(i)) ApplyPreset(i); else SavePreset(i);
    }

    void SavePreset(int i)
    {
        var pm = PetManager.Instance;
        if (pm == null) return;
        if (!pm.SavePreset(i)) { Toast("Сначала поставь питомцев в слоты, потом сохрани набор"); return; }
        SfxManager.Play("coin", 0.6f, 1.2f);
        pPop[i] = 0.0001f;                                      // портреты «подпрыгивают» волной
        PresetBurst(i);
        Toast("Набор " + (i + 1) + " сохранён");
        RefreshPresets();
    }

    void ClearPreset(int i)
    {
        var pm = PetManager.Instance;
        if (pm == null) return;
        pm.ClearPreset(i);
        SfxManager.Play("ui", 0.3f, 0.8f);
        Toast("Набор " + (i + 1) + " очищен");
        RefreshPresets();
    }

    void ApplyPreset(int i)
    {
        var pm = PetManager.Instance;
        if (pm == null) return;
        int subst;
        int missing = pm.ApplyPreset(i, out subst);
        if (missing < 0) return;
        SfxManager.Play("coin", 0.7f, 1.1f);
        pPop[i] = 0.0001f;
        PresetBurst(i);
        if (fx != null) fx.FloatText(InnerToFx(TeamX + TeamW * 0.5f, TeamY + SlotY + SlotH), "Набор " + (i + 1), UiKit.BrassHi, 40f);
        string msg = "Набор " + (i + 1) + " надет";
        if (subst > 0) msg += ", другие копии: " + subst;
        if (missing > 0) msg += ", не хватило питомцев или слотов: " + missing;
        Toast(msg + ". Ctrl+Z: отменить");
    }

    void OnUnequipAllClick()
    {
        var pm = PetManager.Instance;
        if (pm == null) return;
        if (pm.UnequipAll()) { SfxManager.Play("ui", 0.4f, 0.9f); Toast("Все питомцы сняты. Ctrl+Z: отменить"); }
        else Toast("Слоты и так пусты");
    }

    void OnUndoClick()
    {
        var pm = PetManager.Instance;
        if (pm == null) return;
        if (pm.UndoLoadout()) { SfxManager.Play("ui", 0.4f, 1.0f); Toast("Состав возвращён"); }
        else Toast("Отменять нечего");
    }

    bool PresetActive(int i)
    {
        var pm = PetManager.Instance;
        return pm != null && pm.PresetActive(i);
    }

    static float StatNorm(PetBonusSet b, PetStat st)
    {
        return st == PetStat.Capacity ? b.capacityLogs / (float)GameBalance.PetCapacityBase : b.Get(st);
    }

    // две самые сильные характеристики набора (topI = номера PetStat или -1, topV = нормированные значения)
    void TopTwo(PetBonusSet b)
    {
        topI[0] = topI[1] = -1;
        topV[0] = topV[1] = 0f;
        for (int s = 0; s < 7; s++)
        {
            float x = StatNorm(b, (PetStat)s);
            if (x <= 1e-4f) continue;
            if (x > topV[0]) { topV[1] = topV[0]; topI[1] = topI[0]; topV[0] = x; topI[0] = s; }
            else if (x > topV[1]) { topV[1] = x; topI[1] = s; }
        }
    }

    static Sprite MiniPic(PetData d, out bool pending)
    {
        Sprite s = PetPortrait.Get(d);
        pending = s == null && PetPortrait.Enabled && d.prefab != null;
        if (s != null) return s;
        return d.icon != null ? d.icon : UiKit.Icon(UiIcon.Paw);
    }

    void OnPresetCaption(int i)
    {
        var pm = PetManager.Instance;
        if (pm == null) return;
        if (!pm.PresetFilled(i)) { Toast("Сначала сохрани состав в набор"); return; }
        pm.CyclePresetCaption(i, PresetCaptions.Length);
        SfxManager.Play("ui", 0.3f, 1.2f);
        RefreshPresets();
    }

    void RefreshPresets()
    {
        if (pBg[0] == null) return;
        var pm = PetManager.Instance;
        presetPortraitPending = false;
        bool canSave = pm != null && pm.EquippedCount > 0;
        for (int i = 0; i < PresetCount; i++)
        {
            bool filled = PresetFilled(i);
            bool active = filled && PresetActive(i);
            pActive[i] = active;
            PetBonusSet pb = filled && pm != null ? pm.PresetBonus(i) : PetBonusSet.None;
            if (pm != null) pm.ResolvePreset(i, presetRes);

            for (int k = 0; k < PetManager.MaxSlots; k++)
            {
                bool wants = pm != null && pm.PresetWants(i, k);
                PetInstance inst = wants && presetRes[k] >= 0 ? FindOwned(presetRes[k]) : null;
                Image fr = pFrame[i][k], bk = pBack[i][k], pic = pPic[i][k];
                if (!wants)
                {
                    fr.color = new Color(1f, 1f, 1f, 0.14f);
                    bk.color = new Color(0f, 0f, 0f, 0.35f);
                    pic.enabled = false;
                }
                else if (inst == null)                                    // питомцев этого вида больше нет
                {
                    fr.color = new Color(DotColor.r, DotColor.g, DotColor.b, 0.85f);
                    bk.color = new Color(0.25f, 0.08f, 0.08f, 0.85f);
                    pic.enabled = false;
                }
                else
                {
                    PetRarity r = inst.data.rarity;
                    fr.color = PetUi.RarityAccent(r);
                    bk.color = Color.Lerp(PetUi.RarityFill(r), Color.black, 0.2f);
                    bool pend;
                    pic.sprite = MiniPic(inst.data, out pend);
                    pic.enabled = true;
                    presetPortraitPending |= pend;
                }
            }

            TopTwo(pb);
            for (int c = 0; c < 2; c++)
            {
                bool on = filled && topI[c] >= 0;
                pChipIco[i][c].gameObject.SetActive(on);
                pChipTxt[i][c].gameObject.SetActive(on);
                if (!on) continue;
                PetStat st = (PetStat)topI[c];
                pChipIco[i][c].sprite = UiKit.Icon(PetUi.StatIcon(st));
                Set(pChipTxt[i][c], StatValueText(st, pb));
            }

            int cap = pm != null ? pm.PresetCaption(i) : -1;
            string name = cap >= 0 && cap < PresetCaptions.Length ? PresetCaptions[cap]
                        : filled && topI[0] >= 0 ? AutoNames[topI[0]]
                        : "Набор " + (i + 1);
            pLabel[i].color = active ? UiKit.BrassHi : Color.white;
            Set(pLabel[i], name);

            pEmpty[i].gameObject.SetActive(!filled);
            pEmpty[i].color = canSave ? UiKit.BrassHi : Dim;
            Set(pEmpty[i], canSave ? "Сохранить состав" : "Пока пусто");
            pPlus[i].gameObject.SetActive(!filled);
            pCheck[i].gameObject.SetActive(active);
            SetBtn(pCap[i], null, filled);
            SetBtn(pSave[i], null, canSave);
            SetBtn(pClear[i], null, filled);
            pHover[i].tip = filled
                ? "<b>Набор " + (i + 1) + " «" + name + "»</b>: клик надевает всех сразу  ·  кнопки внизу: сменить подпись, перезаписать текущим составом, очистить"
                : "Набор " + (i + 1) + " пуст: клик сохранит текущий состав слотов";
        }
    }

    // анимации наборов: въезд при открытии, пульс рамки надетого набора, волна на портретах, мигающий плюс у пустого
    void AnimatePresets(float dt)
    {
        if (pBg[0] == null) return;
        float tm = Time.unscaledTime;
        var pm = PetManager.Instance;
        bool canSave = pm != null && pm.EquippedCount > 0;
        for (int i = 0; i < PresetCount; i++)
        {
            if (pIn[i] < 1f) pIn[i] = Mathf.Min(1f, pIn[i] + dt);
            float k = Mathf.Clamp01(pIn[i] / 0.35f);
            pWrap[i].localScale = Vector3.one * Mathf.LerpUnclamped(0.82f, 1f, UiDraw.EaseOutBack(k));
            pWrap[i].anchoredPosition = new Vector2(PCardX + PCardW * 0.5f,
                -(PCardY0 + i * (PCardH + PCardGap) + PCardH * 0.5f) - (1f - UiDraw.EaseOutCubic(k)) * 24f);
            pCg[i].alpha = Mathf.Clamp01(k * 2.5f);

            // надетый набор: рамка тёплым светом пульсирует
            Color tgt = pActive[i] ? Color.Lerp(Color.white, UiKit.BrassHi, 0.45f + 0.2f * Mathf.Sin(tm * 3.2f)) : Color.white;
            pBg[i].color = Color.Lerp(pBg[i].color, tgt, Mathf.Clamp01(dt * 12f));
            if (pCheck[i].gameObject.activeSelf) pCheck[i].rectTransform.localScale = Vector3.one * (1f + 0.1f * Mathf.Sin(tm * 4f));

            // пустой набор: плюс мигает, если есть что сохранить
            if (pPlus[i].gameObject.activeSelf)
            {
                float pl = 0.5f + 0.5f * Mathf.Sin(tm * 3f + i);
                pPlus[i].rectTransform.localScale = Vector3.one * (canSave ? 1f + 0.14f * pl : 1f);
                pPlus[i].color = new Color(1f, 1f, 1f, canSave ? 0.5f + 0.5f * pl : 0.22f);
            }

            // сохранили или надели: портреты подпрыгивают по очереди
            if (pPop[i] > 0f)
            {
                pPop[i] += dt;
                if (pPop[i] > 1.2f) pPop[i] = 0f;
                for (int m = 0; m < PetManager.MaxSlots; m++)
                {
                    float t = (pPop[i] - m * 0.07f) / 0.4f;
                    float sc = 1f + (t > 0f && t < 1f ? 0.35f * Mathf.Sin(t * Mathf.PI) : 0f);
                    pFrame[i][m].rectTransform.localScale = Vector3.one * sc;
                }
            }
        }
    }

    void BuildPresets(Transform p)
    {
        LoadPresets();

        // колонка в той же рамке, что и «Команда»
        var col = UiKit.MakeImage(p, "PresetsCol", UiKit.Slot, true, 1f);
        TL(col.rectTransform, PrX, PrY, PrW, PrH);
        col.raycastTarget = false;
        var tr = col.transform;

        var hico = UiKit.MakeImage(tr, "TitleIco", PetIcons.Get(PetIco.Save), false);
        hico.preserveAspect = true;
        hico.raycastTarget = false;
        TLC(hico.rectTransform, 28f, 24f, 28f, 28f);
        var ttl = UiKit.MakeLabel(tr, "Наборы", 28f, Color.white, TextAlignmentOptions.Left, true);
        TL(ttl.rectTransform, 50f, 8f, PrW - 60f, 34f);
        PetUi.AutoSize(ttl, 16f, 28f);

        Vector2 mid = new Vector2(0.5f, 0.5f);
        for (int i = 0; i < PresetCount; i++)
        {
            int n = i;

            // обёртка по центру карточки: на ней масштаб и прозрачность въезда
            var wrap = NewRT("PresetWrap" + (i + 1), tr);
            Anch(wrap, new Vector2(0f, 1f), mid, new Vector2(PCardX + PCardW * 0.5f, -(PCardY0 + i * (PCardH + PCardGap) + PCardH * 0.5f)), new Vector2(PCardW, PCardH));
            pCg[i] = wrap.gameObject.AddComponent<CanvasGroup>();
            pWrap[i] = wrap;
            pIn[i] = 1f;

            var bg = UiKit.MakeImage(wrap, "Preset" + (i + 1), UiKit.Slot, true, 0.9f);
            Stretch(bg.rectTransform);
            AddBtn(bg, () => OnPresetMain(n));
            var hv = bg.gameObject.AddComponent<PetHover>();
            hv.grow = 0.03f;
            pBg[i] = bg;
            pHover[i] = hv;

            // имя набора и галочка «надет сейчас»
            pLabel[i] = UiKit.MakeLabel(bg.transform, "", 22f, Color.white, TextAlignmentOptions.Left, true);
            TL(pLabel[i].rectTransform, 10f, 5f, PCardW - 44f, 26f);
            PetUi.AutoSize(pLabel[i], 13f, 22f);
            pLabel[i].raycastTarget = false;
            pCheck[i] = UiKit.MakeIcon(bg.transform, UiIcon.Check, 22f, "Active");
            pCheck[i].raycastTarget = false;
            pCheck[i].color = UiKit.Good;
            Anch(pCheck[i].rectTransform, new Vector2(1f, 1f), mid, new Vector2(-20f, -18f), new Vector2(24f, 24f));
            pCheck[i].gameObject.SetActive(false);

            // 4 портрета по схеме слотов 2x2: рамка цвета редкости, плашка, сам питомец
            pFrame[i] = new Image[PetManager.MaxSlots];
            pBack[i] = new Image[PetManager.MaxSlots];
            pPic[i] = new Image[PetManager.MaxSlots];
            float mx0 = (PCardW - (2f * PMini + PMiniGap)) * 0.5f;
            for (int k = 0; k < PetManager.MaxSlots; k++)
            {
                float mx = mx0 + (k % 2) * (PMini + PMiniGap), my = PMiniY + (k / 2) * (PMini + PMiniGap);
                var fr = UiKit.MakeImage(bg.transform, "Mini" + k, null, false);
                TLC(fr.rectTransform, mx + PMini * 0.5f, my + PMini * 0.5f, PMini, PMini);   // от центра: подпрыгивает без смещения
                fr.raycastTarget = false;
                var bk = UiKit.MakeImage(fr.transform, "Back", null, false);
                Stretch(bk.rectTransform);
                bk.rectTransform.offsetMin = new Vector2(2f, 2f);
                bk.rectTransform.offsetMax = new Vector2(-2f, -2f);
                bk.raycastTarget = false;
                var pic = UiKit.MakeImage(fr.transform, "Pic", null, false);
                pic.preserveAspect = true;
                pic.raycastTarget = false;
                Stretch(pic.rectTransform);
                pic.rectTransform.offsetMin = new Vector2(3f, 3f);
                pic.rectTransform.offsetMax = new Vector2(-3f, -3f);
                pFrame[i][k] = fr;
                pBack[i][k] = bk;
                pPic[i][k] = pic;
            }

            // плюс по центру блока портретов: виден, только пока набор пуст
            pPlus[i] = UiKit.MakeImage(bg.transform, "Plus", PetIcons.Get(PetIco.Plus), false);
            pPlus[i].preserveAspect = true;
            pPlus[i].raycastTarget = false;
            TLC(pPlus[i].rectTransform, PCardW * 0.5f, PMiniY + PMini + PMiniGap * 0.5f, 56f, 56f);

            // два главных бонуса набора: значок характеристики и значение
            pChipIco[i] = new Image[2];
            pChipTxt[i] = new TMP_Text[2];
            for (int c = 0; c < 2; c++)
            {
                float cx = 8f + c * (PCardW * 0.5f - 4f);
                var ci = UiKit.MakeImage(bg.transform, "ChipIco" + c, null, false);
                ci.preserveAspect = true;
                ci.raycastTarget = false;
                TL(ci.rectTransform, cx, 159f, 22f, 22f);
                var ct = UiKit.MakeLabel(bg.transform, "", 20f, UiKit.Good, TextAlignmentOptions.Left, true);
                TL(ct.rectTransform, cx + 24f, 157f, PCardW * 0.5f - 34f, 26f);
                PetUi.AutoSize(ct, 11f, 20f);
                ct.raycastTarget = false;
                pChipIco[i][c] = ci;
                pChipTxt[i][c] = ct;
            }

            pEmpty[i] = UiKit.MakeLabel(bg.transform, "Сохранить состав", 17f, Dim, TextAlignmentOptions.Center, true);
            TL(pEmpty[i].rectTransform, 4f, 157f, PCardW - 8f, 26f);
            PetUi.AutoSize(pEmpty[i], 11f, 17f);
            pEmpty[i].raycastTarget = false;

            // кнопки: подпись, перезаписать, очистить
            const float BW = 49f, BH = 28f, BY = 184f;
            pCap[i] = UiKit.MakeButton(bg.transform, "", UiBtn.Amber, new Vector2(BW, BH), 14f, 0.6f);
            TL(pCap[i].GetComponent<RectTransform>(), 6f, BY, BW, BH);
            pCap[i].onClick.AddListener(() => OnPresetCaption(n));
            Ico(pCap[i].transform, PetIco.Swap, 18f);
            pCap[i].gameObject.AddComponent<PetHover>().tip = "Сменить подпись набора " + (i + 1) + " (по кругу, первая: по главному бонусу)";

            pSave[i] = UiKit.MakeButton(bg.transform, "", UiBtn.Green, new Vector2(BW, BH), 14f, 0.6f);
            TL(pSave[i].GetComponent<RectTransform>(), 6f + BW + 4f, BY, BW, BH);
            pSave[i].onClick.AddListener(() => SavePreset(n));
            Ico(pSave[i].transform, PetIco.Save, 19f);
            var hs = pSave[i].gameObject.AddComponent<PetHover>(); hs.grow = 0.08f; hs.tip = "Перезаписать набор " + (i + 1) + " текущим составом слотов";

            pClear[i] = UiKit.MakeButton(bg.transform, "", UiBtn.Gray, new Vector2(BW, BH), 14f, 0.6f);
            TL(pClear[i].GetComponent<RectTransform>(), 6f + 2f * (BW + 4f), BY, BW, BH);
            pClear[i].onClick.AddListener(() => ClearPreset(n));
            var ci2 = UiKit.MakeIcon(pClear[i].transform, UiIcon.Cross, 16f, "C");
            ci2.raycastTarget = false;
            var hc = pClear[i].gameObject.AddComponent<PetHover>(); hc.grow = 0.08f; hc.tip = "Очистить набор " + (i + 1);
        }
        RefreshPresets();
    }

    // ------------------------------------------------------------ мелкие анимации
    // полоски бонусов плавно доезжают, кнопка «Взять с собой» дышит, пока есть свободный слот
    void AnimateMisc(float dt)
    {
        float k = 1f - Mathf.Exp(-12f * dt);
        for (int i = 0; i < bonusBarFill.Length; i++)
        {
            if (bonusBarTrack[i] == null || !bonusBarTrack[i].gameObject.activeSelf) continue;
            bonusBarCur[i] = Mathf.Lerp(bonusBarCur[i], bonusBarTgt[i], k);
            bonusBarFill[i].rectTransform.anchorMax = new Vector2(bonusBarCur[i], 1f);
        }

        var pm = PetManager.Instance;
        if (equipBtn != null && equipBtn.gameObject.activeSelf)
        {
            bool ready = tab == Tab.Collection && pm != null && equipBtn.interactable && pm.EquippedCount < pm.SlotCount;
            float s = ready ? 1f + 0.025f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f)) : 1f;
            equipBtn.transform.localScale = Vector3.one * Mathf.Lerp(equipBtn.transform.localScale.x, s, Mathf.Clamp01(dt * 10f));
        }
    }

    // ------------------------------------------------------------ интерактив: погладить питомца
    static readonly string[] PetTiers = { "Присматривается", "Доверяет", "Любит тебя", "Лучший друг" };
    static readonly int[] TierAt = { 0, 5, 15, 30 };
    static readonly string[] PatPhrases = { "Мур!", "Хи-хи!", "Ещё!", "Ура!", "Тепло~", "Я рад!", "Ого!", "Люблю!" };
    static readonly string[] GreetPhrases = { "Привет!", "Я тут!", "Идём?", "Готов!" };

    int PatCount(int uid)
    {
        int n;
        return pats.TryGetValue(uid, out n) ? n : 0;
    }

    static int TierOf(int n)
    {
        int t = 0;
        for (int i = 1; i < TierAt.Length; i++) if (n >= TierAt[i]) t = i;
        return t;
    }

    void ShowBubble(string text)
    {
        if (petBubble == null || tab != Tab.Collection) return;
        petBubbleText.text = text;
        bubbleT = 0f;
    }

    // строка под кнопками: уровень привязанности выбранного питомца
    void RefreshPat()
    {
        if (patText == null) return;
        PetInstance sel = tab == Tab.Collection ? FindInstance(selectedUid) : null;
        bool on = sel != null;
        patHeart.gameObject.SetActive(on);
        patText.gameObject.SetActive(on);
        if (!on) return;
        int n = PatCount(sel.uid);
        int t = TierOf(n);
        if (n == 0) { Set(patText, "<color=#" + PetUi.Hex(Dim) + ">Нажми на питомца: погладить</color>"); return; }
        string next = t + 1 < TierAt.Length ? "  <color=#" + PetUi.Hex(Dim) + ">" + n + "/" + TierAt[t + 1] + "</color>" : "  <color=#" + PetUi.Hex(Dim) + ">" + n + "</color>";
        Set(patText, "<b>" + PetTiers[t] + "</b>" + next);
    }

    // клик (тап) по питомцу
    void OnPetPat()
    {
        PetInstance sel = tab == Tab.Collection ? FindInstance(selectedUid) : null;
        if (sel == null || hearts == null || detCard == null) return;
        if (Time.unscaledTime < patCool) return;
        patCool = Time.unscaledTime + 0.12f;

        int before = PatCount(sel.uid), after = before + 1;
        pats[sel.uid] = after;
        patCombo = Time.unscaledTime - patComboT < 0.8f ? patCombo + 1 : 1;      // серия быстрых поглаживаний
        patComboT = Time.unscaledTime;
        patPulse = 1f;

        PetLiveView live = detCard.Live;
        if (live != null) live.Hop();

        Vector2 c = InnerToFx(DetX + DetW * 0.5f, DetY + 10f + 110f);
        hearts.Burst(c, patCombo >= 5 ? 7 : 3, 60f, patCombo >= 5 ? 38f : 30f);
        SfxManager.Play("ui", 0.35f, Mathf.Min(1.9f, 1.2f + 0.07f * patCombo));  // с серией звук становится выше
        ShowBubble(patCombo >= 5 ? "Мурррр!" : PatPhrases[Random.Range(0, PatPhrases.Length)]);

        PetRarity r = sel.data.rarity;
        if (fx != null && r >= PetRarity.Rare)
            fx.Burst(c, PetUi.RarityAccent(r), 6, 220f, 18f, 300f, 0.6f);

        int t0 = TierOf(before), t1 = TierOf(after);
        if (t1 > t0)                                                              // новый уровень привязанности
        {
            if (fx != null)
            {
                fx.Burst(c, UiKit.BrassHi, 22, 340f, 26f, 280f, 0.9f);
                fx.FloatText(c + new Vector2(0f, 90f), PetTiers[t1], UiKit.BrassHi, 36f);
            }
            hearts.Burst(c, 10, 90f, 40f);
            SfxManager.Play("coin", 0.7f, 1.3f);
            Toast(PetUi.NameOf(sel.data) + ": " + PetTiers[t1].ToLowerInvariant(), ToastKind.Good);
        }
        RefreshPat();
    }

    // перетаскивание по питомцу: покрутить рукой
    void OnPetDrag(float dx)
    {
        if (detCard == null) return;
        PetLiveView live = detCard.Live;
        if (live != null) live.Drag(dx * 0.6f);
    }

    // пузырь с репликой: выезжает пружинкой, висит около полутора секунд, гаснет; сердечко в строке привязанности бьётся
    void AnimatePat(float dt)
    {
        if (petBubble == null) return;
        patPulse = Mathf.MoveTowards(patPulse, 0f, dt / 0.35f);

        bool vis = bubbleT >= 0f && tab == Tab.Collection;
        if (!vis)
        {
            if (petBubble.gameObject.activeSelf) petBubble.gameObject.SetActive(false);
            bubbleT = -1f;
        }
        else
        {
            const float Life = 1.7f;
            bubbleT += dt;
            if (bubbleT >= Life)
            {
                bubbleT = -1f;
                petBubble.gameObject.SetActive(false);
            }
            else
            {
                if (!petBubble.gameObject.activeSelf) petBubble.gameObject.SetActive(true);
                float inK = UiDraw.EaseOutBack(Mathf.Clamp01(bubbleT / 0.2f));
                float a = Mathf.Clamp01((Life - bubbleT) / 0.3f);
                petBubble.rectTransform.localScale = Vector3.one * Mathf.LerpUnclamped(0.5f, 1f, inK);
                petBubble.rectTransform.anchoredPosition = new Vector2(0f, -34f + 2f * Mathf.Sin(bubbleT * 6f));
                petBubble.color = new Color(1f, 1f, 1f, a);
                petBubbleText.alpha = a;
            }
        }

        if (patHeart != null && patHeart.gameObject.activeSelf)
        {
            float beat = Mathf.Max(0f, Mathf.Sin(Time.unscaledTime * 4f));
            patHeart.rectTransform.localScale = Vector3.one * (1f + 0.10f * beat + 0.35f * patPulse);
        }
    }

    // фон и живые эффекты: цвет сцены по редкости, дыхание свечения, лучи легендарных, подсветка под курсором, пульс пустых слотов
    Image gridBase, gridGlow, detGlow, detRays, equipIco;
    AchSparks fx;
    readonly int[] lastUid = { -2, -2, -2, -2 };
    int lastClickIdx = -1;
    float lastClickT;
    AchMotes gridMotes;
    Color tintCur = new Color(1f, 0.84f, 0.42f, 1f), detTintCur = new Color(1f, 0.84f, 0.42f, 1f);
    float raysAng;
    int rarFilter = -1;
    readonly Image[] rarImg = new Image[5];
    readonly Button[] rarBtn = new Button[5];

    void AnimateFx(float dt)
    {
        float tm = Time.unscaledTime;
        var si = tab == Tab.Collection ? FindInstance(selectedUid) : null;
        Color target = rarFilter >= 0 ? PetUi.RarityAccent((PetRarity)rarFilter) : (si != null ? PetUi.RarityAccent(si.data.rarity) : UiKit.Brass);
        tintCur = Color.Lerp(tintCur, target, 1f - Mathf.Exp(-8f * dt));
        float breathe = 0.5f + 0.5f * Mathf.Sin(tm * 0.8f);
        if (gridBase != null) gridBase.color = Color.Lerp(UiKit.Ink, tintCur, 0.16f);
        if (gridGlow != null)
        {
            gridGlow.color = new Color(tintCur.r, tintCur.g, tintCur.b, 0.14f + 0.07f * breathe);
            gridGlow.rectTransform.localScale = Vector3.one * (1f + 0.04f * breathe);
        }
        if (gridMotes != null) gridMotes.SetColor(tintCur);

        // свечение и лучи за большой карточкой выбранного
        Color dt2 = si != null ? PetUi.RarityAccent(si.data.rarity) : UiKit.Brass;
        detTintCur = Color.Lerp(detTintCur, dt2, 1f - Mathf.Exp(-10f * dt));
        if (detGlow != null)
        {
            detGlow.gameObject.SetActive(si != null);
            detGlow.color = new Color(detTintCur.r, detTintCur.g, detTintCur.b, 0.30f + 0.16f * Mathf.Sin(tm * 2.2f));
        }
        if (detRays != null)
        {
            bool legend = si != null && si.data.rarity == PetRarity.Legendary;
            detRays.gameObject.SetActive(legend);
            raysAng += dt * 16f;
            detRays.rectTransform.localEulerAngles = new Vector3(0f, 0f, -raysAng);
            detRays.color = new Color(detTintCur.r, detTintCur.g, detTintCur.b, 0.38f);
        }

        if (hoverFrame != null)
        {
            bool on = hoverPc != null && hoverPc.index >= 0 && hoverPc.view.rt.gameObject.activeSelf && tab == Tab.Collection;
            hoverK = Mathf.MoveTowards(hoverK, on ? 1f : 0f, dt / 0.12f);
            hoverFrame.gameObject.SetActive(hoverK > 0.01f);
            if (on)
            {
                int idx = hoverPc.index;
                float x = CellLeft + (idx % Cols) * PitchX;
                float cy = 8f + (idx / Cols) * PitchY;
                hoverFrame.rectTransform.anchoredPosition = new Vector2(x + CardW * 0.5f, -(cy + CardH * 0.5f));
            }
            var hc = UiKit.Brass; hc.a = 0.55f * UiDraw.EaseOutCubic(hoverK);
            hoverFrame.color = hc;
        }

        // рамка выбора плавно перелетает к новой карточке и мягко «дышит»
        if (selFrame != null && selFrame.gameObject.activeSelf)
        {
            selFrame.rectTransform.anchoredPosition = Vector2.Lerp(selFrame.rectTransform.anchoredPosition, selTarget, 1f - Mathf.Exp(-18f * dt));
            selFrame.color = Color.Lerp(UiKit.Brass, UiKit.BrassHi, 0.5f + 0.5f * Mathf.Sin(tm * 3f));
        }

        // описание выбранного: короткий «выезд» при смене питомца
        detT = Mathf.MoveTowards(detT, 1f, dt / 0.22f);
        if (detCg != null) detCg.alpha = Mathf.Lerp(0.3f, 1f, Mathf.Clamp01(detT * 1.6f));
        if (detCard != null && detCard.rt.gameObject.activeSelf)
            detCard.rt.localScale = Vector3.one * (Mathf.LerpUnclamped(0.9f, 1f, UiDraw.EaseOutBack(detT)) * (1f + 0.08f * patPulse));

        // панель бонусов вспыхивает зелёным, когда бонусы изменились
        if (bonusPanel != null)
        {
            bonusFlash = Mathf.MoveTowards(bonusFlash, 0f, dt / 0.6f);
            bonusPanel.color = Color.Lerp(Color.white, UiKit.Good, 0.6f * bonusFlash);
        }

        var pm = PetManager.Instance;
        bool canPlace = pm != null && tab == Tab.Collection && selectedUid >= 0 && !pm.IsEquipped(selectedUid);
        float pulse = 0.5f + 0.5f * Mathf.Sin(tm * 4f);
        for (int i = 0; i < slots.Length; i++)
        {
            var sl = slots[i];
            if (sl == null) continue;
            Color c = sl.baseCol;
            if (canPlace && pm.IsSlotOpen(i) && pm.GetEquipped(i) == null) c = Color.Lerp(Color.white, UiKit.Good, 0.25f + 0.35f * pulse);
            sl.bg.color = Color.Lerp(sl.bg.color, c, Mathf.Clamp01(dt * 14f));
            if (slotIn[i] < 1f) slotIn[i] = Mathf.Min(1f, slotIn[i] + dt);
            float inS = Mathf.LerpUnclamped(0.55f, 1f, UiDraw.EaseOutBack(Mathf.Clamp01(slotIn[i] / 0.32f)));   // выезд при открытии
            if (sl.card != null && sl.card.rt.gameObject.activeSelf)
            {
                sl.pop = Mathf.MoveTowards(sl.pop, 0f, dt / 0.4f);
                sl.card.rt.localScale = Vector3.one * (sl.baseScale * (1f + 0.22f * sl.pop * sl.pop) * inS);
            }
            if (sl.icon != null && sl.icon.gameObject.activeSelf)        // пустой слот: лапка тихо покачивается
            {
                sl.icon.rectTransform.anchoredPosition = new Vector2(0f, 16f + 3f * Mathf.Sin(tm * 2f + i * 1.3f));
                sl.icon.rectTransform.localScale = Vector3.one * inS;
            }
        }
    }

    // карточки: появление волной, плавное увеличение выбранной и карточки под курсором; индикатор прокрутки
    void AnimateCards(float dt)
    {
        for (int k = 0; k < pool.Count; k++)
        {
            var pc = pool[k];
            if (pc.index < 0 || pc.index >= items.Count) continue;
            if (pc.delay > 0f)
            {
                pc.delay -= dt;
                if (pc.cg != null) pc.cg.alpha = 0f;
                pc.view.rt.localScale = Vector3.one * 0.7f;
                continue;
            }
            pc.appear = Mathf.MoveTowards(pc.appear, 1f, dt / 0.24f);
            bool sel = IsSelectedEntry(items[pc.index]);
            bool hov = pc == hoverPc && tab == Tab.Collection;
            float target = sel ? 1.06f : (hov ? 1.03f : 1f);
            pc.sc = Mathf.Lerp(pc.sc, target, 1f - Mathf.Exp(-16f * dt));
            if (pc.cg != null) pc.cg.alpha = Mathf.Clamp01(pc.appear * 2.4f);
            pc.view.rt.localScale = Vector3.one * (pc.sc * Mathf.LerpUnclamped(0.7f, 1f, UiDraw.EaseOutBack(pc.appear)));
        }

        // тонкая полоса справа: видно, что список длиннее окна, и где мы в нём
        if (scrollTrack != null)
        {
            float vh = GridH - 16f, ch = contentsRt.sizeDelta.y;
            bool need = ch > vh + 1f && items.Count > 0;
            if (scrollTrack.gameObject.activeSelf != need) scrollTrack.gameObject.SetActive(need);
            if (need)
            {
                float trackH = scrollTrack.rectTransform.sizeDelta.y;
                float handleH = Mathf.Clamp(trackH * vh / ch, 28f, trackH);
                float t01 = Mathf.Clamp01(contentsRt.anchoredPosition.y / Mathf.Max(1f, ch - vh));
                scrollHandle.rectTransform.sizeDelta = new Vector2(5f, handleH);
                scrollHandle.rectTransform.anchoredPosition = new Vector2(0f, -t01 * (trackH - handleH));
            }
        }
    }

    // координаты внутри внутренней панели (от её левого верхнего угла) -> координаты искр (от центра окна)
    static Vector2 InnerToFx(float x, float y) { return new Vector2(39f + x - WinW * 0.5f, WinH * 0.5f - (54f + y)); }

    void OnRarClick(int j)
    {
        int f = j - 1;
        if (rarFilter == f || tab != Tab.Collection) return;
        rarFilter = f;
        contentsRt.anchoredPosition = Vector2.zero;
        armedUntil = 0f;
        animateNext = true;
        revealPending = true;
        SfxManager.Play("ui", 0.3f, 1.1f);
        Populate();
    }

    // кнопка в пустом списке: убрать все фильтры
    void OnResetFilters()
    {
        filterStat = -1;
        rarFilter = -1;
        contentsRt.anchoredPosition = Vector2.zero;
        animateNext = true;
        SfxManager.Play("ui", 0.3f, 1.1f);
        Populate();
    }

    // коллекция: вкладки, сортировка, фильтр, окно прокрутки с пулом карточек (карточки привязывает LayoutCells)
    void BuildGrid(Transform p)
    {
        // верхняя полоса
        var colBtn = MakeBtn(p, "Коллекция", UiBtn.Green, 20f, 8f, 210f, 48f, 22f, () => SetTab(Tab.Collection), out tabColLabel);
        tabColImg = colBtn.GetComponent<Image>();
        var bookBtn = MakeBtn(p, "Книга", UiBtn.Green, 238f, 8f, 210f, 48f, 22f, () => SetTab(Tab.Book), out tabBookLabel);
        tabBookImg = bookBtn.GetComponent<Image>();
        sortBtn = MakeBtn(p, "Сорт.", UiBtn.Amber, 456f, 8f, 230f, 48f, 20f, OnSortClick, out sortLabel);
        filterBtn = MakeBtn(p, "Фильтр", UiBtn.Amber, 694f, 8f, 230f, 48f, 20f, OnFilterClick, out filterLabel);
        autoBtn = MakeBtn(p, "Подобрать лучших", UiBtn.Yellow, DetX, 8f, 290f, 48f, 22f, OnAutoClick, out autoLabel);
        BtnIco(colBtn, UiKit.Icon(UiIcon.Paw), 30f, 18f);
        BtnIco(bookBtn, UiKit.Icon(UiIcon.Book), 30f, 18f);
        BtnIco(sortBtn, PetIcons.Get(PetIco.Sort), 28f, 16f);
        BtnIco(filterBtn, PetIcons.Get(PetIco.Filter), 28f, 16f);
        BtnIco(autoBtn, PetIcons.Get(PetIco.Wand), 34f, 16f);
        Tip(colBtn, "Твои питомцы: выбирай, бери с собой, сливай дубликаты (Tab)");
        Tip(bookBtn, "Книга: все виды питомцев и награда за полный набор редкости (Tab)");
        Tip(sortBtn, "Порядок карточек: клик переключает (редкость, характеристика, имя, новые)");
        Tip(filterBtn, "Показать только питомцев одной характеристики: клик переключает");
        Tip(autoBtn, "Сам подберёт команду с наибольшим суммарным бонусом. Ctrl+Z вернёт прежний состав");

        // «Отмена»: то же, что Ctrl+Z, но видно глазами (над колонкой «Наборы»)
        undoBtn = MakeBtn(p, "Отмена", UiBtn.Gray, PrX, 8f, PrW, 48f, 22f, OnUndoClick, out undoLabel);
        BtnIco(undoBtn, PetIcons.Get(PetIco.Swap), 26f, 12f);
        Tip(undoBtn, "Вернуть прежний состав команды (Ctrl+Z). Повторное нажатие вернёт обратно");

        // фильтр по редкости (над коллекцией)
        string[] rn = { "Все", "Обычн.", "Необыч.", "Редкие", "Легенд." };
        for (int j = 0; j < 5; j++)
        {
            int jj = j;
            TMP_Text rl;
            var cb = MakeBtn(p, rn[j], UiBtn.Amber, GridX + j * 104f, ChipY, 100f, ChipH, 17f, () => OnRarClick(jj), out rl);
            rarBtn[j] = cb;
            rarImg[j] = cb.GetComponent<Image>();
            var gem = UiKit.MakeIcon(cb.transform, j == 0 ? UiIcon.Paw : UiIcon.Gem, 20f, "G");
            gem.color = j == 0 ? Color.white : PetUi.RarityAccent((PetRarity)(j - 1));
            gem.raycastTarget = false;
            Anch(gem.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(15f, 0f), new Vector2(20f, 20f));
            if (rl != null)
            {
                rl.rectTransform.offsetMin = new Vector2(28f, rl.rectTransform.offsetMin.y);
                rl.rectTransform.offsetMax = new Vector2(-4f, rl.rectTransform.offsetMax.y);
            }
        }

        var view = UiKit.MakeImage(p, "View", UiKit.Slot, true, 1f);
        TL(view.rectTransform, GridX, GridY, GridW, GridH);

        // сцена как у «Достижений»: тёмная подложка цвета редкости, дышащее свечение, пылинки
        gridBase = UiKit.MakeImage(view.transform, "Base", null, false);
        Stretch(gridBase.rectTransform);
        gridBase.rectTransform.offsetMin = new Vector2(8f, 8f);
        gridBase.rectTransform.offsetMax = new Vector2(-8f, -8f);
        gridBase.raycastTarget = false;
        gridGlow = UiKit.MakeImage(view.transform, "Glow", PetUi.GlowSprite, false);
        gridGlow.raycastTarget = false;
        Anch(gridGlow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(GridW * 1.1f, GridH * 0.8f));
        gridMotes = AchMotes.Create(view.transform, GridW, GridH);

        var vp = NewRT("Viewport", view.transform);
        Stretch(vp);
        vp.offsetMin = new Vector2(8f, 8f);
        vp.offsetMax = new Vector2(-8f, -8f);
        vp.gameObject.AddComponent<RectMask2D>();
        var vpImg = vp.gameObject.AddComponent<Image>();
        vpImg.color = Clear;
        vpImg.raycastTarget = true;                                  // ловит перетаскивание

        contentsRt = NewRT("Content", vp);
        contentsRt.anchorMin = new Vector2(0f, 1f);
        contentsRt.anchorMax = new Vector2(1f, 1f);
        contentsRt.pivot = new Vector2(0.5f, 1f);
        contentsRt.anchoredPosition = Vector2.zero;
        contentsRt.sizeDelta = new Vector2(0f, 100f);

        scroll = view.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = vp;
        scroll.content = contentsRt;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;
        scroll.inertia = true;

        // рамка выбранной карточки: лежит под всеми карточками, выглядывает по краям
        selFrame = UiKit.MakeImage(contentsRt, "Select", UiKit.Slot, true, 1f);
        selFrame.color = UiKit.BrassHi;
        selFrame.raycastTarget = false;
        Anch(selFrame.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CardW + 14f, CardH + 14f));
        selFrame.gameObject.SetActive(false);

        // подсветка под курсором (под карточками, как рамка выбранного)
        hoverFrame = UiKit.MakeImage(contentsRt, "Hover", UiKit.Slot, true, 1f);
        hoverFrame.raycastTarget = false;
        Anch(hoverFrame.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CardW + 10f, CardH + 10f));
        hoverFrame.gameObject.SetActive(false);

        // пул карточек: создаётся один раз
        for (int k = 0; k < PoolSize; k++)
        {
            var cv = PetCardView.Create(contentsRt, CardW, CardH, "Pet", false);
            var pc = new PoolCard { view = cv };
            pc.cg = cv.rt.gameObject.AddComponent<CanvasGroup>();
            pc.mark = AddEquippedMark(cv);
            cv.frame.gameObject.AddComponent<PetRightClick>().cb = () => OnCardRightClick(pc);
            cv.frame.raycastTarget = true;
            var btn = cv.frame.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = cv.frame;
            btn.onClick.AddListener(() => OnCardClick(pc));
            var hov = cv.frame.gameObject.AddComponent<PetHover>();
            hov.cb = on => { if (on) hoverPc = pc; else if (hoverPc == pc) hoverPc = null; };
            cv.rt.gameObject.SetActive(false);
            pool.Add(pc);
        }

        var vig = UiKit.MakeImage(view.transform, "Vignette", AchUi.Vignette, false);
        vig.raycastTarget = false;
        Stretch(vig.rectTransform);

        // пустая коллекция: подсказка поверх окна прокрутки
        emptyGrid = UiKit.MakeLabel(view.transform, "", 26f, Dim, TextAlignmentOptions.Center, false);
        Anch(emptyGrid.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(GridW - 80f, 140f));
        emptyGrid.enableWordWrapping = true;
        emptyGrid.gameObject.SetActive(false);

        TMP_Text rl2;
        resetBtn = MakeBtn(view.transform, "Сбросить фильтры", UiBtn.Amber, (GridW - 260f) * 0.5f, GridH * 0.5f + 56f, 260f, 50f, 22f, OnResetFilters, out rl2);
        resetBtn.gameObject.SetActive(false);

        // индикатор прокрутки
        scrollTrack = UiKit.MakeImage(view.transform, "ScrollTrack", null, false);
        scrollTrack.raycastTarget = false;
        scrollTrack.color = new Color(0f, 0f, 0f, 0.35f);
        Anch(scrollTrack.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-3f, -14f), new Vector2(5f, GridH - 28f));
        scrollHandle = UiKit.MakeImage(scrollTrack.transform, "Handle", null, false);
        scrollHandle.raycastTarget = false;
        scrollHandle.color = new Color(UiKit.BrassHi.r, UiKit.BrassHi.g, UiKit.BrassHi.b, 0.85f);
        Anch(scrollHandle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(5f, 40f));
        scrollTrack.gameObject.SetActive(false);
    }

    // правая панель: выбранный питомец крупно, что он даёт, кнопки
    void BuildDetail(Transform p)
    {
        var det = UiKit.MakeImage(p, "Detail", UiKit.Slot, true, 1f);
        TL(det.rectTransform, DetX, DetY, DetW, DetH);

        detRoot = NewRT("DetailRoot", det.transform);
        Stretch(detRoot);
        detCg = detRoot.gameObject.AddComponent<CanvasGroup>();      // плавное появление при смене питомца
        var t = detRoot;

        // свечение и лучи за карточкой (цвет редкости, лучи только у легендарных)
        const float CW = 200f, CH = 236f;
        float cx = DetW * 0.5f, cyc = 10f + CH * 0.5f;
        detRays = UiKit.MakeImage(t, "Rays", PetUi.RaysSprite, false);
        detRays.raycastTarget = false;
        Anch(detRays.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(cx, -cyc), new Vector2(520f, 520f));
        detRays.gameObject.SetActive(false);
        detGlow = UiKit.MakeImage(t, "Glow", PetUi.GlowSprite, false);
        detGlow.raycastTarget = false;
        Anch(detGlow.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(cx, -cyc), new Vector2(380f, 380f));

        detCard = PetCardView.Create(t, CW, CH, "DetailCard", false);
        detCard.EnableLive();        // питомец вращается живой 3D-моделью (стадия роста по PetManager.GrowthStage)
        Anch(detCard.rt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(CW, CH));

        float w = DetW - 32f;
        detName = UiKit.MakeLabel(t, "", 32f, Color.white, TextAlignmentOptions.Center, true);
        TL(detName.rectTransform, 16f, 254f, w, 40f);
        PetUi.AutoSize(detName, 16f, 32f);

        detRarity = UiKit.MakeLabel(t, "", 24f, Color.white, TextAlignmentOptions.Center, true);
        TL(detRarity.rectTransform, 16f, 294f, w, 28f);
        PetUi.AutoSize(detRarity, 14f, 24f);

        detExtra = UiKit.MakeLabel(t, "", 20f, Soft, TextAlignmentOptions.Top, false);
        TL(detExtra.rectTransform, 16f, 322f, w, 26f);
        detExtra.enableWordWrapping = true;

        // блок «что даёт»
        var box = UiKit.MakeImage(t, "StatBox", UiKit.Slot, true, 0.6f);
        TL(box.rectTransform, 10f, 352f, DetW - 20f, 162f);
        box.raycastTarget = false;

        detStat = UiKit.MakeLabel(t, "", 26f, Color.white, TextAlignmentOptions.Center, true);
        TL(detStat.rectTransform, 16f, 358f, w, 30f);
        PetUi.AutoSize(detStat, 14f, 26f);

        detHead = UiKit.MakeLabel(t, "", 20f, Dim, TextAlignmentOptions.Center, false);
        TL(detHead.rectTransform, 16f, 390f, w, 22f);
        PetUi.AutoSize(detHead, 12f, 20f);

        detD1 = UiKit.MakeLabel(t, "", 22f, Color.white, TextAlignmentOptions.Center, false);
        TL(detD1.rectTransform, 16f, 413f, w, 24f);
        PetUi.AutoSize(detD1, 12f, 22f);

        detD2 = UiKit.MakeLabel(t, "", 22f, Color.white, TextAlignmentOptions.Center, false);
        TL(detD2.rectTransform, 16f, 437f, w, 24f);
        PetUi.AutoSize(detD2, 12f, 22f);

        detLive = UiKit.MakeLabel(t, "", 22f, Color.white, TextAlignmentOptions.Center, false);
        TL(detLive.rectTransform, 16f, 461f, w, 24f);
        PetUi.AutoSize(detLive, 12f, 22f);

        detNote = UiKit.MakeLabel(t, "", 20f, Color.white, TextAlignmentOptions.Center, false);
        TL(detNote.rectTransform, 16f, 485f, w, 24f);
        PetUi.AutoSize(detNote, 12f, 20f);

        detEmpty = UiKit.MakeLabel(t, "Выбери питомца", 28f, Dim, TextAlignmentOptions.Center, true);
        Anch(detEmpty.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(DetW - 30f, 60f));

        // «Взять с собой» («Заменить») и «Снять» делят одно место: показывается одна из двух
        float bw = DetW - 40f;
        equipBtn = MakeBtn(t, "Взять с собой", UiBtn.Green, 20f, 528f, bw, 58f, 28f, OnEquipClick, out equipLabel);
        unequipBtn = MakeBtn(t, "Снять", UiBtn.Amber, 20f, 528f, bw, 58f, 28f, OnUnequipClick, out unequipLabel);
        mergeBtn = MakeBtn(t, "Слить", UiBtn.Amber, 20f, 594f, bw, 44f, 22f, OnMergeClick, out mergeLabel);
        releaseBtn = MakeBtn(t, "Отпустить", UiBtn.Gray, 20f, 646f, bw, 42f, 22f, OnReleaseClick, out releaseLabel);
        releaseColor = releaseLabel.color;
        equipIco = BtnIco(equipBtn, PetIcons.Get(PetIco.Plus), 38f, 22f);
        BtnIco(unequipBtn, PetIcons.Get(PetIco.Minus), 38f, 22f);
        BtnIco(mergeBtn, PetIcons.Get(PetIco.Merge), 30f, 16f);
        BtnIco(releaseBtn, PetIcons.Get(PetIco.Release), 28f, 16f);
        Tip(equipBtn, "Взять питомца в команду: он начнёт усиливать тебя (Enter, двойной клик или ПКМ)");
        Tip(unequipBtn, "Убрать питомца из команды: он останется в коллекции (Enter, двойной клик или ПКМ)");
        Tip(mergeBtn, GameBalance.PetMergeCount + " одинаковых питомца сливаются в одного с +1 звездой: его бонус растёт");
        Tip(releaseBtn, "Отпустить навсегда: питомец исчезнет. Нужно подтвердить вторым нажатием");

        // полоса таймера подтверждения «Отпустить» (сжимается за ArmTime секунд)
        armBar = UiKit.MakeImage(releaseBtn.transform, "ArmBar", null, false);
        armBar.raycastTarget = false;
        armBar.color = UiKit.Warn;
        var arb = armBar.rectTransform;
        arb.anchorMin = new Vector2(0f, 0f);
        arb.anchorMax = new Vector2(1f, 0f);
        arb.pivot = new Vector2(0f, 0f);
        arb.offsetMin = new Vector2(12f, 7f);
        arb.offsetMax = new Vector2(-12f, 11f);
        armBar.gameObject.SetActive(false);

        // ---- интерактив: питомца можно погладить (клик) и покрутить (перетаскивание)
        var zone = UiKit.MakeImage(detCard.rt, "PetZone", null, false);
        zone.color = new Color(1f, 1f, 1f, 0f);
        zone.raycastTarget = true;
        Stretch(zone.rectTransform);
        petZone = zone.gameObject.AddComponent<PetPetZone>();
        petZone.onPat = OnPetPat;
        petZone.onDrag = OnPetDrag;
        zone.gameObject.AddComponent<PetHover>().tip = "Клик: погладить питомца  ·  потяни мышью: покрутить";

        // реплика питомца над карточкой (пузырь появляется и гаснет сам)
        petBubble = UiKit.MakeImage(t, "PetBubble", UiKit.Slot, true, 0.7f);
        Anch(petBubble.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -34f), new Vector2(150f, 38f));
        petBubble.raycastTarget = false;
        petBubbleText = UiKit.MakeLabel(petBubble.transform, "", 20f, UiKit.BrassHi, TextAlignmentOptions.Center, true);
        PetUi.AutoSize(petBubbleText, 12f, 20f);
        petBubble.gameObject.SetActive(false);

        // строка внизу: сердечко и уровень привязанности
        patHeart = UiKit.MakeImage(t, "PatHeart", PetHeartFx.HeartSprite, false);
        patHeart.color = PetHeartFx.Pink;
        patHeart.preserveAspect = true;
        patHeart.raycastTarget = false;
        TLC(patHeart.rectTransform, 34f, 705f, 24f, 24f);
        patText = UiKit.MakeLabel(t, "", 20f, Soft, TextAlignmentOptions.Left, false);
        TL(patText.rectTransform, 52f, 692f, DetW - 70f, 26f);
        PetUi.AutoSize(patText, 12f, 20f);

        BuildBook(det.transform);
    }

    // Книга питомцев: общий прогресс, по строке на каждую редкость
    void BuildBook(Transform p)
    {
        bookRoot = NewRT("BookRoot", p);
        Stretch(bookRoot);
        var t = bookRoot;

        var title = UiKit.MakeLabel(t, "Книга питомцев", 28f, Color.white, TextAlignmentOptions.Center, true);
        TL(title.rectTransform, 10f, 10f, DetW - 20f, 34f);

        bookTotal = UiKit.MakeLabel(t, "", 24f, Soft, TextAlignmentOptions.Center, false);
        TL(bookTotal.rectTransform, 10f, 46f, DetW - 20f, 30f);

        var barBg = UiKit.MakeImage(t, "BarBg", UiKit.Slot, true, 0.5f);
        TL(barBg.rectTransform, 20f, 82f, DetW - 40f, 20f);
        barBg.raycastTarget = false;
        var inner = NewRT("BarInner", barBg.transform);
        Stretch(inner);
        inner.offsetMin = new Vector2(5f, 5f);
        inner.offsetMax = new Vector2(-5f, -5f);
        bookBarFill = UiKit.MakeImage(inner, "BarFill", null, false);
        bookBarFill.color = UiKit.Good;
        bookBarFill.raycastTarget = false;
        bookBarFill.rectTransform.anchorMin = Vector2.zero;
        bookBarFill.rectTransform.anchorMax = new Vector2(0f, 1f);
        bookBarFill.rectTransform.offsetMin = bookBarFill.rectTransform.offsetMax = Vector2.zero;

        for (int r = 3; r >= 0; r--)
        {
            float y0 = 130f + (3 - r) * 100f;
            PetRarity rar = (PetRarity)r;
            var gem = UiKit.MakeIcon(t, UiIcon.Gem, 28f, "Gem" + r);
            gem.color = PetUi.RarityAccent(rar);
            TLC(gem.rectTransform, 32f, y0 + 15f, 28f, 28f);

            bookName[r] = UiKit.MakeLabel(t, "", 24f, Color.white, TextAlignmentOptions.Left, true);
            TL(bookName[r].rectTransform, 54f, y0, 140f, 30f);
            PetUi.AutoSize(bookName[r], 13f, 24f);

            bookCount[r] = UiKit.MakeLabel(t, "", 24f, Color.white, TextAlignmentOptions.Right, true);
            TL(bookCount[r].rectTransform, DetW - 100f, y0, 80f, 30f);

            bookCheck[r] = UiKit.MakeIcon(t, UiIcon.Check, 24f, "Done" + r);
            bookCheck[r].color = UiKit.Good;
            TLC(bookCheck[r].rectTransform, DetW - 118f, y0 + 15f, 24f, 24f);

            bookBonus[r] = UiKit.MakeLabel(t, "", 18f, Dim, TextAlignmentOptions.Left, false);
            TL(bookBonus[r].rectTransform, 20f, y0 + 38f, DetW - 40f, 44f);
            bookBonus[r].enableWordWrapping = true;
            PetUi.AutoSize(bookBonus[r], 12f, 20f);
        }

        var note = UiKit.MakeLabel(t, "Рюкзак Книга не усиливает. Сумма с питомцами не выше +" + PetUi.Pct(GameBalance.PetBonusCap) + "%.", 17f, Dim, TextAlignmentOptions.Center, false);
        TL(note.rectTransform, 14f, 560f, DetW - 28f, 60f);
        note.enableWordWrapping = true;
    }

    // ============================================================ кнопка в HUD
    void BuildHud()
    {
        var cgo = new GameObject("PetHudCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        cgo.layer = 5;
        cgo.transform.SetParent(transform, false);
        var canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 12;                                   // выше HUD (10), ниже окон (30+)
        var sc = cgo.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        sc.matchWidthOrHeight = 0.5f;
        hudGroup = cgo.GetComponent<CanvasGroup>();
        hudGroup.alpha = 0f;
        hudGroup.blocksRaycasts = false;

        var btn = UiKit.MakeButton(cgo.transform, "", UiBtn.Amber, new Vector2(HudBtn, HudBtn), 20f, 1f);
        btn.name = "PetsButton";
        hudRt = btn.GetComponent<RectTransform>();
        // раньше обе кнопки стояли в одной точке справа внизу, и «Прокачка» закрывала лапку: теперь питомцы левее неё через зазор
        Anch(hudRt, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-(UpgradeWindow.HudRight + UpgradeWindow.HudBtnW + UpgradeWindow.HudGap), UpgradeWindow.HudBottom), new Vector2(HudBtn, HudBtn));
        btn.onClick.AddListener(Toggle);

        var paw = UiKit.MakeIcon(btn.transform, UiIcon.Paw, 54f, "Paw");
        paw.rectTransform.anchoredPosition = new Vector2(0f, 2f);

        // подсказка клавиши (на телефоне клавиатуры нет)
        if (!Application.isMobilePlatform)
        {
            var key = UiKit.MakeKeycap(btn.transform, "P", 38f);
            Anch(key.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(8f, -8f), new Vector2(38f, 38f));
        }

        // красная «!»: есть питомец вне слота и есть свободный слот
        var dot = UiKit.MakeImage(btn.transform, "Dot", UiKit.Medallion, false);
        dot.color = DotColor;
        hudDot = dot.rectTransform;
        Anch(hudDot, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-8f, -8f), new Vector2(34f, 34f));
        var bang = UiKit.MakeLabel(dot.transform, "!", 24f, Color.white, TextAlignmentOptions.Center, true);
        Stretch(bang.rectTransform);
        hudBang = bang;
        hudDot.gameObject.SetActive(false);
    }

    void UpdateHud(float dt)
    {
        if (hudGroup == null) return;

        // пока не зарегистрировали в реестре HUD, метки над миром могут оказаться под кнопкой
        if (!hudRegistered && HudSafeAreaRegistry.Instance != null)
        {
            HudSafeAreaRegistry.Instance.Register(hudRt);
            hudRegistered = true;
        }

        bool visible = Available && !MobileInput.ModalOpen && !DialogueManager.IsBusy && !DialogueSequence.AnyPlaying;
        hudGroup.alpha = Mathf.MoveTowards(hudGroup.alpha, visible ? 1f : 0f, dt / 0.2f);
        bool on = visible && hudGroup.alpha > 0.5f;
        hudGroup.blocksRaycasts = on;
        hudGroup.interactable = on;

        var pm = PetManager.Instance;
        bool hint = visible && pm != null && pm.Owned.Count > pm.EquippedCount && pm.EquippedCount < pm.SlotCount;
        int fresh = visible && pm != null ? pm.NewCount : 0;          // новые питомцы, которых ещё не смотрели: число на значке
        bool showDot = hint || fresh > 0;
        if (hudDot.gameObject.activeSelf != showDot) hudDot.gameObject.SetActive(showDot);
        if (showDot)
        {
            Set(hudBang, fresh > 0 ? (fresh > 9 ? "9+" : fresh.ToString()) : "!");
            dotT += dt;
            hudDot.localScale = Vector3.one * (1f + 0.12f * Mathf.Sin(dotT * 5f));
        }
    }

    // ============================================================ мелкие помощники
    // иконка поверх кнопки слева, подпись сдвигается правее
    static Image BtnIco(Button b, Sprite sp, float size, float padLeft = 14f)
    {
        var img = UiKit.MakeImage(b.transform, "Ico", sp, false);
        img.preserveAspect = true;
        img.raycastTarget = false;
        Anch(img.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(padLeft + size * 0.5f, 1f), new Vector2(size, size));
        var lbl = b.GetComponentInChildren<TMP_Text>();
        if (lbl != null)
        {
            lbl.rectTransform.offsetMin = new Vector2(padLeft + size + 4f, lbl.rectTransform.offsetMin.y);
            lbl.rectTransform.offsetMax = new Vector2(-8f, lbl.rectTransform.offsetMax.y);
        }
        return img;
    }

    static Image Ico(Transform parent, PetIco ico, float size)
    {
        var img = UiKit.MakeImage(parent, "Ico", PetIcons.Get(ico), false);
        img.preserveAspect = true;
        img.raycastTarget = false;
        Anch(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
        return img;
    }

    // подсказка в строке под окном при наведении на кнопку
    static void Tip(Button b, string text)
    {
        if (b == null) return;
        var h = b.gameObject.GetComponent<PetHover>();
        if (h == null) h = b.gameObject.AddComponent<PetHover>();
        h.tip = text;
    }

    static Button MakeBtn(Transform parent, string label, UiBtn style, float x, float y, float w, float h, float font,
                          UnityAction onClick, out TMP_Text labelText)
    {
        var b = UiKit.MakeButton(parent, label, style, new Vector2(w, h), font, 1f);
        TL(b.GetComponent<RectTransform>(), x, y, w, h);
        b.onClick.AddListener(onClick);
        labelText = b.GetComponentInChildren<TMP_Text>();
        if (labelText != null) PetUi.AutoSize(labelText, 14f, font);
        return b;
    }

    static void AddBtn(Image img, UnityAction a)
    {
        img.raycastTarget = true;
        var b = img.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        b.targetGraphic = img;
        b.onClick.AddListener(a);
    }

    static void Anch(RectTransform r, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        PetUi.Anchor(r, anchor, pivot, pos, size);
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
