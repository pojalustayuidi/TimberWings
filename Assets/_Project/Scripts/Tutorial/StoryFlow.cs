// Assets/_Project/Scripts/Tutorial/StoryFlow.cs (НЕ в Editor)
// Сюжетные главы после вступления. IntroFlow остаётся главой 1 (ходьба, Мира, рубка, костёр из 2 стадий, возврат к Мире).
// Дальше здания открывает НЕ уровень, а задание NPC. Уровень только условие начала главы.
//
//   2. Припасы     Мира                   принести ей 8 брёвен, по 4 за рейс (E у Миры). За бревна идёт опыт: к концу игрок на 2-м уровне
//   3. Прокачка    Мира          (ур. 2)  открывает окно прокачки (U), в нём можно купить только «Вместимость рюкзака», потом отправляет к Оскару
//   4. Хижина      Оскар         (ур. 2)  починить хижину, потом пила: 2 бревна -> 1 доска
//   5. Доски       без разговора          сделать 6 досок на пиле
//   6. Рынок       Мира                   построить прилавок (бревна + доски); после главы включается NPC-торговец
//   7. Первая продажа Торговец            познакомиться (E), потом продать брёвна или доски через окно продажи (E)
//   8. Палатка     Оскар         (ур. 3)  построить палатку (бревна, доски, монеты), лагерь для рабочих
//  8б. Звери-помощники Оскар      (ур. 4)  после палатки: у клети в лагере первая бесплатная Лесная клеть, один раз даёт Обычного питомца
//   9. Мост        Оскар         (ур. 5)  три этапа: опоры (брёвна + монеты), настил, тросы и перила (доски + монеты); после третьего открывается проход, а Небесная клеть питомцев открывается сюжетом
//  10. Записка     без разговора          у моста Лео читает записку Гуннара: конец вертикального среза
//
// Оскар стоит у хижины с самого начала. Пока его глава не началась, он отсылает игрока («рано»), в ходе главы напоминает задание.
// Если Оскара в сцене нет, при старте появляется копия Миры рядом с хижиной (или в точке Oskar Spot), чтобы поменять модель на свою.
//
// Глава = собеседник + условие + реплики + награда. Каждая идёт по циклу:
//   Enter (ждём уровень) -> Offer (поговори, E) -> Do (задание в панели цели, стрелка к цели) -> Report (вернись, E) -> Complete.
// Offer и Report необязательны: без реплик глава сразу идёт дальше. Нет NPC или здания в сцене: глава тихо пропускается,
// а торговля при отсутствии рынка остаётся открытой, поэтому игра не ломается.
// Исключение: глава «Мост» обязательна. Нет зоны стройки моста: ошибка в Console (и в Setup Doctor), глава не пропускается молча.
// Добавить главу: новый блок в BuildChapters().
using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-10)]
public class StoryFlow : MonoBehaviour, ISaveable
{
    public enum Site { Hut = 0, Market = 1, Tent = 2, Bridge = 3 }

    public static StoryFlow Instance { get; private set; }

    [Header("Здания (пусто = найдутся по названию, см. FindSite)")]
    public BuildingController hut;
    public BuildingController market;
    public BuildingController tent;
    public BuildingController bridge;
    public PlankWorkshop workshop;
    public UpgradeWindow upgradeWindow;

    [Header("Оскар")]
    [Tooltip("Где стоит Оскар, если его нет в сцене. Пусто = рядом с хижиной. Лучше поставь свою модель NPC с Speaker «Оскар» и DialogueSequence")]
    public Transform oskarSpot;

    [Header("Мост и записка Гуннара")]
    [Tooltip("Модель моста (RiverBridge). Пусто = найдётся в сцене. Её стадии и проход ведёт BuildStageLink")]
    public RiverBridge riverBridge;
    [Tooltip("Где Лео находит записку Гуннара. Пусто = у моста")]
    public Transform noteSpot;
    [Tooltip("Когда мост достроен: вместо чтения записки вслух показать финальную заставку (EndingSequence). Выключи, чтобы вернуть старый диалог")]
    public bool useEnding = true;

    [Header("Звери-помощники")]
    [Tooltip("Кто даёт главу про питомцев. Сейчас Оскар. Будет свой NPC: впиши его Speaker.displayName (и добавь имя в VoiceId.SpeakerKey)")]
    public string petsGiverName = "Оскар";
    [Tooltip("Куда ведёт стрелка в главе питомцев. Пусто = найдётся PetCrateSite с Лесной клетью")]
    public Transform petCrateSpot;

    [Header("Торговец")]
    [Tooltip("NPC-торговец у прилавка (Speaker + DialogueSequence + NpcMarker + TradingStall). Пусто = найдётся по merchantName, в том числе среди выключенных. До постройки рынка объект выключается, после главы «Рынок» включается")]
    public GameObject merchant;

    [Header("Имена говорящих (как в Speaker.displayName)")]
    public string miraName = "Мира";
    public string oskarName = "Оскар";
    public string playerName = "Лео";
    [Tooltip("Имя NPC-торговца (Speaker.displayName). Если переименуете, добавьте новое имя в VoiceId.SpeakerKey")]
    public string merchantName = "Торговец";

    [Header("Тексты")]
    public string levelText = "Нужен {0}-й уровень: руби дрова и строй";
    public string talkToText = "Поговори с {0}: <color=#6BCB5A>E</color>";
    public string returnToText = "Вернись к {0}: <color=#6BCB5A>E</color>";

    // торговля (у NPC-торговца) открыта, когда прилавок построен (или рынка в сцене нет)
    public bool MarketOpen { get; private set; }
    public bool AllDone { get; private set; }
    // клеть питомцев в лагере доступна (глава «Звери-помощники» принята или пропущена). Без StoryFlow клеть открыта по уровню
    public bool PetsOpen { get; private set; }
    public int ChapterIndex { get { return ci; } }
    public event Action ChaptersFinished;

    // ------------------------------------------------------------ главы
    class Chapter
    {
        public string id;
        public string giver;                       // у кого разговоры (null = без NPC)
        public int minLevel = 1;
        public DialogueSequence.Line[] offer;      // разговор перед заданием (null = сразу задание)
        public DialogueSequence.Line[] report;     // разговор после задания (null = сразу следующая глава)
        public Func<string> objective;             // текст цели, пока идёт задание (запасной путь, если objectiveData нет или пуст)
        public Func<ObjectiveData> objectiveData;  // цель-виджет: иконки, счётчики, прогресс (необязательно)
        public Func<bool> done;                    // задание выполнено?
        public Func<Transform> target;             // куда ведёт стрелка
        public Func<bool> skip;                    // true = главу пропустить (нет здания или NPC)
        public string talkText;                    // цель на время разговора «до» (пусто = talkToText «Поговори с {0}»); для имён, которые не склоняются
        public string reminder;                    // что NPC отвечает, пока идёт задание (E на нём)
        public string idleAfter;                   // что NPC отвечает после главы
        public bool dumpWhenFull;                  // пока ждём уровень, полный рюкзак можно сложить у костра (иначе нечем добирать опыт)
        public Action onAccept;                    // после разговора offer
        public Action onComplete;                  // награда и открытия
        public Action restoreDone;                 // Load: тихо повторить открытия уже закрытой главы (без наград, диалогов, событий)
        public Action restoreActive;               // Load: тихо повторить принятие задания, если глава в процессе (onAccept без наград)
    }

    enum Phase { Enter, Offer, Do, Report }

    readonly List<Chapter> chapters = new List<Chapter>();
    int ci;
    Phase phase = Phase.Enter;
    DialogueSequence talk;
    bool introDone, firstSale, warnedNoGiver;
    bool bridgeWarned, noteStarted, noteRead, petsCrateWarned;
    PetCrateSite forestCrate;
    float endingAt = -1f;
    float pollAt;
    PlayerInventory inv;
    PlayerStats stats;
    GameObject oskarGo;
    PlayerMovement playerMove;
    Transform player;
    BackpackTag pack;
    Transform treeCache;
    float treeAt;
    DialogueSequence idleSeq;
    string idleText;
    SupplyPoint supply;

    // ------------------------------------------------------------ жизненный цикл
    void Awake()
    {
        Instance = this;
        ResolveSites();
        BuildChapters();
        SaveSystem.Register(this);   // если Load() уже был, секция придёт сюда, а применится в конце Start
    }

    void Start()
    {
        EnsureWindow();
        SetupOskar();
        SetupMerchant();

        started = true;
        if (restorePending) ApplyRestore();   // после SetupOskar/SetupMerchant: они ставят «стартовые» реплики, которые мы перекрываем
    }

    void OnEnable()
    {
        GameEvents.DialogueFinished += OnDialogue;
        GameEvents.BuildRevealFinished += OnBuildRevealFinished;
        GameEvents.BuildingRestored += OnBuildingRestored;
        TradingStall.Sold += OnSold;
    }

    void OnDisable()
    {
        GameEvents.DialogueFinished -= OnDialogue;
        GameEvents.BuildRevealFinished -= OnBuildRevealFinished;
        GameEvents.BuildingRestored -= OnBuildingRestored;
        TradingStall.Sold -= OnSold;
    }

    // показ стадии здания закончился: сразу проверяем цель, а не ждём следующего опроса (0.25 с)
    void OnBuildRevealFinished(BuildingController b) { pollAt = 0f; }

    void OnDestroy()
    {
        if (Instance == this) { SaveSystem.Unregister(this); Instance = null; }
    }

    // вступление (WASD, Мира, костёр) закончено? Пока нет, ни одна глава не стартует и торговля закрыта.
    // Раньше при IntroFlow.Instance == null (объект выключен или ещё не проснулся) главы стартовали сразу
    IntroFlow introRef;
    bool introSearched;

    bool IntroFinished()
    {
        if (introDone) return true;
        if (!introSearched)
        {
            introSearched = true;
            introRef = IntroFlow.Instance != null ? IntroFlow.Instance : FindFirstObjectByType<IntroFlow>(FindObjectsInactive.Include);
        }
        if (introRef == null) return true;   // вступления в сцене нет вообще
        return introRef.Current == IntroFlow.Step.Done;
    }

    void Update()
    {
        bool introOver = IntroFinished();
        // рынок без вступления закрыт, даже если прилавок в сцене не найден
        MarketOpen = introOver && (market == null || market.IsFinished);
        // рынка в сцене нет: глава «Рынок» пропущена и её onComplete не сработает, торговец выходит сразу после вступления
        if (market == null && MarketOpen) ShowMerchant();

        if (AllDone) return;

        if (!introDone)
        {
            if (!introOver) return;
            introDone = true;
            if (FindFirstObjectByType<QuestEdgeArrow>() == null) gameObject.AddComponent<QuestEdgeArrow>();
        }

        if (Time.unscaledTime < pollAt) return;
        pollAt = Time.unscaledTime + 0.25f;
        Tick();
    }

    // ------------------------------------------------------------ отладка (Editor-меню Tools/BuilderSim/Тест квестов)
    // Закрывает текущую главу как выполненную: принимает задание (если ещё не принято), выдаёт награду и открытия.
    public void DebugSkipChapter()
    {
        if (AllDone) return;
        if (ci >= chapters.Count) { Finish(); return; }
        var ch = chapters[ci];
        if ((phase == Phase.Enter || phase == Phase.Offer) && ch.onAccept != null) ch.onAccept();
        Complete(ch);
        if (ci >= chapters.Count) Finish();
    }

    public void DebugSkipAll()
    {
        int guard = 0;
        while (!AllDone && guard++ < 64) DebugSkipChapter();
    }

    // ------------------------------------------------------------ автомат
    void Tick()
    {
        // пока игроку показывают готовую стадию здания (BuildReveal), ни разговоры, ни новые задания не стартуют:
        // иначе реплика Оскара перебила бы показ. Конец показа придёт событием BuildRevealFinished (оно сбрасывает pollAt)
        if (BuildingController.AnyRevealing) return;
        // после загрузки здания встают в сохранённое состояние в LateUpdate первого кадра: пока не встали, цели не проверяем
        if (BuildingController.AnyRestorePending) return;
        if (needHudRefresh) { needHudRefresh = false; RefreshHud(); }
        if (ci >= chapters.Count) { Finish(); return; }
        var ch = chapters[ci];

        switch (phase)
        {
            case Phase.Enter:
                if (ch.skip != null && ch.skip()) { ci++; return; }
                if (Level() < ch.minLevel)
                {
                    WaitForLevel(ch);
                    return;
                }
                if (ch.offer == null || !BeginTalk(ch, ch.offer, Phase.Offer, string.IsNullOrEmpty(ch.talkText) ? talkToText : ch.talkText))
                {
                    // нет разговора (или NPC не найден): принимаем задание сразу, иначе здание осталось бы закрытым
                    if (ch.onAccept != null) ch.onAccept();
                    BeginDo(ch);
                }
                break;

            case Phase.Do:
                ChapterObjective(ch);
                if (ch.done == null || ch.done())
                {
                    if (ch.report == null || !BeginTalk(ch, ch.report, Phase.Report, returnToText)) Complete(ch);
                }
                break;
        }
    }

    void OnDialogue(DialogueSequence seq)
    {
        if (seq == null) return;
        if (seq == idleSeq) { ApplyIdle(); return; }   // болтовня «пока не время»: NPC снова доступен по E
        if (seq != talk || ci >= chapters.Count) return;
        var ch = chapters[ci];

        if (phase == Phase.Offer)
        {
            if (ch.onAccept != null) ch.onAccept();
            if (!string.IsNullOrEmpty(ch.reminder)) SetIdle(ch.giver, ch.reminder);
            BeginDo(ch);
        }
        else if (phase == Phase.Report) Complete(ch);
    }

    void OnSold() { firstSale = true; }

    // разговор: NPC находится, ему подставляются реплики, цель «Поговори с ...»
    bool BeginTalk(Chapter ch, DialogueSequence.Line[] lines, Phase next, string objectiveFormat)
    {
        talk = null;
        if (string.IsNullOrEmpty(ch.giver)) return false;

        if (ch.giver == oskarName && oskarGo != null && !oskarGo.activeSelf) oskarGo.SetActive(true);

        var sp = Speaker.Find(ch.giver);
        var seq = sp != null ? sp.GetComponent<DialogueSequence>() : null;
        if (seq == null)
        {
            if (!warnedNoGiver)
                Debug.LogWarning("StoryFlow: не найден NPC «" + ch.giver + "» (Speaker + DialogueSequence). Глава «" + ch.id + "» идёт без разговора.");
            warnedNoGiver = true;
            return false;
        }

        if (idleSeq == seq) idleSeq = null;   // теперь у NPC настоящий разговор главы
        talk = seq;
        seq.SetTrigger(DialogueSequence.Trigger.OnInteract);
        seq.once = true;
        seq.SetLines(lines, true);

        phase = next;
        Objective(string.Format(objectiveFormat, ch.giver));
        Arrow(sp.transform);
        return true;
    }

    void BeginDo(Chapter ch)
    {
        phase = Phase.Do;
        talk = null;
        Arrow(ch.target != null ? ch.target() : null);
        pollAt = 0f;
        SaveSystem.RequestSave("story");   // задание принято
    }

    void Complete(Chapter ch)
    {
        if (ch.onComplete != null) ch.onComplete();
        GameEvents.RaiseQuestDone(ch.id);   // звон, плашка и конфетти (Vibe/QuestCelebration, Vibe/SfxManager)
        if (!string.IsNullOrEmpty(ch.idleAfter)) SetIdle(ch.giver, ch.idleAfter);
        ci++;
        phase = Phase.Enter;
        talk = null;
        Arrow(null);
        pollAt = 0f;
        SaveSystem.RequestSave("story");   // глава закрыта
    }

    void Finish()
    {
        AllDone = true;
        Objective("");
        Arrow(null);
        SaveSystem.RequestSave("story");
        if (ChaptersFinished != null) ChaptersFinished();
    }

    // ------------------------------------------------------------ главы и реплики
    void BuildChapters()
    {
        // 2. Припасы (Мира): задание уже прозвучало в конце вступления, поэтому без разговора «до»; сдача по E у Миры
        chapters.Add(new Chapter
        {
            id = "supply", giver = miraName,
            skip = () => Speaker.Find(miraName) == null,
            onAccept = BeginSupply,
            objective = SupplyObjective,
            objectiveData = SupplyData,
            done = () => supply == null || supply.delivered >= supply.need,
            onComplete = EndSupply,
            restoreActive = RestoreSupply
        });

        // 3. Прокачка (Мира, ур. 2): окно открывается, можно купить только рюкзак
        chapters.Add(new Chapter
        {
            id = "upgrade", giver = miraName, minLevel = GameBalance.UpgradeLevel,
            offer = UpgradeOffer(), report = UpgradeReport(),
            dumpWhenFull = true,
            skip = () => PlayerUpgrades.Instance == null,
            onAccept = BeginUpgradeQuest,
            objective = UpgradeObjective,
            done = () => PlayerUpgrades.Instance == null || PlayerUpgrades.Instance.Level(UpgradeType.Capacity) >= 1,
            onComplete = EndUpgradeQuest,
            restoreDone = RestoreUpgradeDone,
            restoreActive = RestoreUpgradeActive
        });

        // 4. Хижина (Оскар, ур. 2)
        chapters.Add(new Chapter
        {
            id = "hut", giver = oskarName, minLevel = GameBalance.HutLevel,
            offer = HutOffer(), report = HutReport(),
            reminder = "Десять брёвен на стены, десять на крышу. Не стой, неси.",
            idleAfter = "Бревна на верстак: из двух выйдет доска. Шесть досок отнеси Мире.",
            skip = () => hut == null,
            onAccept = () => Unlock(hut),
            objective = () => SiteObjective("Почини хижину", hut),
            objectiveData = () => SiteData("Почини хижину", hut),
            done = () => hut.IsFinished,
            target = () => hut.transform,
            onComplete = () => { if (workshop != null) workshop.unlocked = true; },
            restoreDone = () => { Unlock(hut); if (workshop != null) workshop.unlocked = true; },
            restoreActive = () => Unlock(hut)
        });

        // 5. Доски (без разговора: Оскар уже всё объяснил)
        chapters.Add(new Chapter
        {
            id = "planks",
            skip = () => workshop == null,
            objective = () => "Сделай досок для Миры: <color=#6BCB5A>" + Mathf.Min(workshop.totalCollected, GameBalance.MarketPlanks) + "/" + GameBalance.MarketPlanks
                              + "</color>. Пилорама: E у хижины",
            objectiveData = PlanksData,
            done = () => workshop.totalCollected >= GameBalance.MarketPlanks,
            target = () => workshop.transform
        });

        // 6. Рынок (Мира)
        chapters.Add(new Chapter
        {
            id = "market", giver = miraName,
            offer = MarketOffer(), report = MarketReport(),
            skip = () => market == null,
            onAccept = () => Unlock(market),
            onComplete = ShowMerchant,
            restoreDone = () => { Unlock(market); ShowMerchant(); },
            restoreActive = () => Unlock(market),
            objective = () => SiteObjective("Построй прилавок", market),
            objectiveData = () => SiteData("Построй прилавок", market),
            done = () => market.IsFinished,
            target = () => market.transform
        });

        // 7. Первая продажа
        // Торговец включается в onComplete главы «Рынок» (ShowMerchant). Знакомство: E на нём (пока оно не прозвучало,
        // TradingStall окно не открывает), потом цель «продай» и стрелка к нему. Сам NPC после главы молчит: E у него только торговля
        chapters.Add(new Chapter
        {
            id = "firstSale", giver = merchantName,
            offer = MerchantIntro(),
            talkText = "Подойди к торговцу: <color=#6BCB5A>E</color>",
            skip = () => market == null || firstSale || merchant == null,   // без торговца продавать некому, главу не зависаем
            objective = () => "Продай брёвна или доски торговцу: <color=#6BCB5A>E</color> у прилавка",
            done = () => firstSale,
            target = () => merchant != null ? merchant.transform : null
        });

        // 8. Палатка (Оскар, ур. 3)
        chapters.Add(new Chapter
        {
            id = "tent", giver = oskarName, minLevel = GameBalance.TentLevel,
            offer = TentOffer(), report = TentReport(),
            reminder = "Брёвна, доски и монеты. Лагерь задаром не строят.",
            idleAfter = "Лагерь стоит. Копи монеты, дальше мост.",
            skip = () => tent == null,
            onAccept = () => Unlock(tent),
            objective = () => SiteObjective("Поставь палатку", tent),
            objectiveData = () => SiteData("Поставь палатку", tent),
            done = () => tent.IsFinished,
            target = () => tent.transform,
            restoreDone = () => Unlock(tent),
            restoreActive = () => Unlock(tent)
        });

        // 8б. Звери-помощники (ур. 4, по умолчанию Оскар): первая бесплатная Лесная клеть, один раз даёт Обычного питомца.
        // Клеть стоит в лагере (PetCrateSite). Нет PetManager, базы или клети в сцене: глава тихо пропускается.
        // Дальше клети покупаются за монеты, Небесная открывается после моста (onComplete главы «Мост»).
        chapters.Add(new Chapter
        {
            id = "pets", giver = petsGiverName, minLevel = GameBalance.PetCrateLevel[0],
            offer = PetsOffer(), report = PetsReport(),
            reminder = "Клеть в лагере, у палатки. Первая бесплатная, жми E.",
            idleAfter = "Зверя корми по расписанию, он ворчать не будет. Теперь к мосту.",
            skip = PetsSkip,
            onAccept = PetsAccept,
            objective = () => "Открой клеть у палатки: <color=#6BCB5A>E</color>",
            done = () => PetManager.Instance == null || PetManager.Instance.FreeCrateUsed,
            target = PetCrateTarget,
            restoreDone = () => PetsOpen = true,     // сама клеть и бесплатная попытка живут в PetManager (ключ pets)
            restoreActive = () => PetsOpen = true
        });

        // 9. Мост (Оскар, ур. 5): три этапа, цены из GameBalance.Bridge* (через Build Site Data).
        // Обязательная глава: skip всегда false. Нет зоны моста: ошибка в Console, цепочка стоит здесь, а не открывает финал вхолостую.
        // Разговора «после» нет: сразу записка у моста. Этапы идут строго по порядку (BuildingController не берёт следующий, пока не сдан текущий)
        chapters.Add(new Chapter
        {
            id = "bridge", giver = oskarName, minLevel = GameBalance.BridgeLevel,
            offer = BridgeOffer(),
            reminder = "Сперва опоры, потом настил, потом тросы. Брёвна, доски и монеты по списку над мостом.",
            idleAfter = "Мост стоит. Ступай к Гуннару, малец. Он ждал.",
            skip = BridgeMissing,
            onAccept = () => Unlock(bridge),
            objective = BridgeObjective,
            objectiveData = BridgeData,
            done = () => bridge != null && bridge.IsFinished,
            target = () => bridge != null ? bridge.transform : null,
            onComplete = () => { if (PetManager.Instance != null) PetManager.Instance.UnlockSkyCrate(); },   // мост стоит: открывается Небесная клеть
            restoreDone = () => Unlock(bridge),       // Небесную клеть открывает PetManager из своего сохранения (skyByStory)
            restoreActive = () => Unlock(bridge)
        });

        // 10. Записка Гуннара: без NPC. Когда мост достроен и игрок рядом, Лео читает записку вслух. Конец среза.
        chapters.Add(new Chapter
        {
            id = "gunnar",
            objective = NoteObjective,
            done = NoteDone,
            target = NoteTarget,
            onComplete = EndOfSlice
        });

        // Дальше (следующая работа): наём рабочих у палатки, доска заданий.
        // Каждая глава: новый блок chapters.Add(...) здесь, реплики пишутся прямо в коде.
    }

    // реплика: hero = true говорит Лео, иначе NPC (его Speaker). Каждая до 120 символов
    DialogueSequence.Line L(bool hero, string text)
    {
        return new DialogueSequence.Line { speaker = hero ? playerName : "", text = text, pauseAfter = 0.3f };
    }

    // ------------------------------------------------------------ глава «Звери-помощники»
    DialogueSequence.Line[] PetsOffer()
    {
        return new[]
        {
            L(false, "Лагерь стоит, а рук всё мало. Помощники нужны не только людям."),
            L(true,  "Помощники? Это кто?"),
            L(false, "Звери. Гуннар прислал клеть, она у палатки. Первая задаром, дальше за монеты."),
            L(true,  "И что они умеют?"),
            L(false, "Ворчат и помогают. Кто выпадет, того и бери. Иди, открывай.")
        };
    }

    DialogueSequence.Line[] PetsReport()
    {
        return new[]
        {
            L(false, "Вот и зверь у тебя. В слот его, пусть бежит следом."),
            L(true,  "Кормить по расписанию, как в записке?"),
            L(false, "Именно. А теперь мост, малец. Монеты копи.")
        };
    }

    // пропуск: нет PetManager или базы питомцев, нет клети в сцене, либо бесплатную клеть уже открывали (сохранение)
    bool PetsSkip()
    {
        var pm = PetManager.Instance;
        if (pm == null || pm.database == null || pm.database.pets == null || pm.database.pets.Count == 0) return true;
        if (FindForestCrate() == null)
        {
            if (!petsCrateWarned)
            {
                petsCrateWarned = true;
                Debug.LogWarning("StoryFlow: в сцене нет PetCrateSite с Лесной клетью. Глава «Звери-помощники» пропущена. Tools > BuilderSim > Питомцы > Поставить клети в локации.");
            }
            return true;
        }
        if (pm.FreeCrateUsed) { PetsOpen = true; return true; }
        return false;
    }

    void PetsAccept()
    {
        PetsOpen = true;
        if (PetManager.Instance != null) PetManager.Instance.OfferFreeCrate();
    }

    Transform PetCrateTarget()
    {
        if (petCrateSpot != null) return petCrateSpot;
        var c = FindForestCrate();
        return c != null ? c.transform : null;
    }

    PetCrateSite FindForestCrate()
    {
        if (forestCrate != null) return forestCrate;
        var all = FindObjectsByType<PetCrateSite>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++) if (all[i].crate == CrateType.Forest) { forestCrate = all[i]; break; }
        return forestCrate;
    }

    DialogueSequence.Line[] UpgradeOffer()
    {
        return new[]
        {
            L(false, "Второй уровень! Вот теперь можно говорить о настоящем деле."),
            L(true,  "О каком?"),
            L(false, "О прокачке. Я открыла тебе окно, монеты за уровень уже в кармане."),
            L(true,  "И что улучшать первым?"),
            L(false, "Рюкзак. Четыре бревна за рейс, курам на смех. Бегаешь туда-сюда."),
            L(true,  "Окно на U. Берусь.")
        };
    }

    DialogueSequence.Line[] UpgradeReport()
    {
        return new[]
        {
            L(false, "Шесть брёвен за рейс, совсем другое дело! Топор и ноги подкачаешь позже."),
            L(true,  "А теперь куда?"),
            L(false, "К Оскару, он у хижины, где брёвна лежат. Друг Гуннара, доски делать умеет."),
            L(true,  "Иду к Оскару.")
        };
    }

    DialogueSequence.Line[] HutOffer()
    {
        return new[]
        {
            L(false, "Гуннаров племянник? Вылитый дядя, даже хмуришься так же."),
            L(true,  "Вы Оскар? Мира сказала, вы научите делать доски."),
            L(false, "Научу. Но пила стоит в хижине, а крыша там как решето. Сначала чиним."),
            L(true,  "Сколько нужно брёвен?"),
            L(false, "Десять на стены, десять на крышу. Топор не обманет, как говорил Гуннар."),
            L(true,  "Понял. Несу брёвна.")
        };
    }

    DialogueSequence.Line[] HutReport()
    {
        return new[]
        {
            L(false, "Стоит и не скрипит! Гуннар бы кивнул."),
            L(true,  "А пила?"),
            L(false, "Работает. Клади брёвна на верстак, потом забирай доски. Два бревна дают одну."),
            L(true,  "Куда мне доски?"),
            L(false, "Мира без них лавку не поставит. Шести ей хватит для начала."),
            L(true,  "Шесть так шесть. Займусь.")
        };
    }

    DialogueSequence.Line[] MarketOffer()
    {
        return new[]
        {
            L(false, "Доски! Настоящие! Ну-ка, дай посмотреть."),
            L(true,  "Оскар сказал, они нужны для прилавка."),
            L(false, "Ещё как. Торговец до сих пор с телеги торгует, а ему нужна нормальная лавка."),
            L(true,  "Что строим?"),
            L(false, "Помост, потом стойки с прилавком. Брёвна и твои доски."),
            L(true,  "А кто будет стоять за прилавком?"),
            L(false, "Торговец. Он берёт брёвна и доски по честной цене.")
        };
    }

    // знакомство с торговцем: первая реплика его (NPC), Лео отвечает
    DialogueSequence.Line[] MerchantIntro()
    {
        return new[]
        {
            L(false, "Новенький? Ты, видать, племянник Гуннара. Мира про тебя говорила."),
            L(true,  "Лео. Так это вы теперь за прилавком?"),
            L(false, "Я. Беру брёвна и доски, плачу сразу и по-честному."),
            L(true,  "Доски лучше оставить на стройку, да?"),
            L(false, "Умно. Лишнее продавай, нужное придержи. Окно подскажет, сколько оставить."),
            L(true,  "Понял. Сейчас продам, что есть.")
        };
    }

    DialogueSequence.Line[] MarketReport()
    {
        return new[]
        {
            L(false, "Красота! Теперь у торговца настоящая лавка."),
            L(true,  "Дядя бы оценил."),
            L(false, "Иди к торговцу, он платит сразу. Бревно за 2 монеты, доска за 6."),
            L(true,  "Доска втрое дороже?"),
            L(false, "Труд стоит денег. Монеты пригодятся: на палатку, на людей, на мост."),
            L(true,  "Мост... Да. Гуннар ждёт.")
        };
    }

    DialogueSequence.Line[] TentOffer()
    {
        return new[]
        {
            L(false, "Монеты звенят, доски лежат. Пора звать людей."),
            L(true,  "Каких людей?"),
            L(false, "Лесорубов. После шторма многие разбрелись. Будет крыша, вернутся."),
            L(true,  "Палатка у костра?"),
            L(false, "Она самая. Брёвна, доски и немного монет. Лагерь задаром не строят."),
            L(true,  "Принято. Соберу всё.")
        };
    }

    DialogueSequence.Line[] TentReport()
    {
        return new[]
        {
            L(false, "Вот и лагерь. Костёр, хижина, лавка, палатка."),
            L(true,  "А где люди?"),
            L(false, "Придут, когда будет чем платить. Копи монеты, мост ждёт."),
            L(true,  "Значит, дальше мост.")
        };
    }

    DialogueSequence.Line[] BridgeOffer()
    {
        return new[]
        {
            L(false, "Лагерь стоит, монеты звенят. Теперь мост, малец. Гуннар по ту сторону реки."),
            L(true,  "Он правда там? Три дня ни слова."),
            L(false, "Мастерская за рекой. Шторм разнёс мост, а у меня одного рук не хватит."),
            L(true,  "Что нужно?"),
            L(false, "Брёвен сорок, досок семьдесят и монет восемьсот. Три этапа, по порядку."),
            L(true,  "Опоры, настил, тросы. Понял. Берусь.")
        };
    }

    // записка Гуннара: читает Лео вслух. ТЕКСТ ЧЕРНОВОЙ, замени на авторский из GDD (до 120 символов в реплике)
    string[] NoteLines()
    {
        return new[]
        {
            "Под перилами привязана записка. Почерк дяди Гуннара.",
            "«Лео. Раз читаешь это, мост снова стоит, а ты не побоялся топора.»",
            "«Я ушёл за кедром для большой работы. Вернусь, когда лес позволит.»",
            "«Лагерь не бросай. Ты справишься лучше, чем думаешь. Гуннар.»",
            "Он жив. И он в меня верит. Значит, рубим дальше."
        };
    }

    // ------------------------------------------------------------ здания
    // По каким словам в названии (цепочка имён от объекта до корня + название в Build Site Data) узнаём стройку
    static readonly string[][] Keys =
    {
        new[] { "hut", "lumberjack", "хиж", "лесоруб" },
        new[] { "tradingstall", "market", "shop", "рын", "лавк", "прилав" },
        new[] { "tent", "палат" },
        new[] { "bridge", "мост", "подвесн" }
    };
    static readonly string[] CampfireKeys = { "campfire", "костёр", "костер" };

    public static string PathOf(Transform tr)
    {
        var sb = new System.Text.StringBuilder();
        for (var t = tr; t != null; t = t.parent) sb.Append(t.name).Append('/');
        return sb.ToString().ToLowerInvariant();
    }

    public static string PathOf(BuildingController b)
    {
        string p = PathOf(b.transform);
        if (b.data != null) p += (b.data.buildingName + "/" + b.data.name).ToLowerInvariant();
        return p;
    }

    public static bool HasAny(string path, string[] keys)
    {
        foreach (var k in keys) if (path.Contains(k)) return true;
        return false;
    }

    // нужен и редактору: ищет объект стадий (HutStages / ShopStages / TentStages) нужного здания
    public static bool MatchesSite(Site s, string path)
    {
        return !HasAny(path, CampfireKeys) && HasAny(path, Keys[(int)s]);
    }

    // используется и рантаймом, и меню настройки в редакторе
    // Если подходящих зон несколько (дубли, старые DropZone), берётся лучшая по SiteScore, а не первая попавшаяся
    public static BuildingController FindSite(Site s, BuildingController[] all)
    {
        BuildingController best = null;
        int bestScore = int.MinValue;
        foreach (var b in all)
        {
            if (b == null) continue;
            if (!MatchesSite(s, PathOf(b))) continue;
            int sc = SiteScore(b);
            if (sc > bestScore) { best = b; bestScore = sc; }
        }
        return best;
    }

    // чем выше, тем лучше зона: рабочая зона с триггером, ценой в брёвнах и обычным именем BuildZone
    public static int SiteScore(BuildingController b)
    {
        int sc = 0;
        if (b.GetComponent<Collider>() != null) sc += 4;     // без триггера стройка не работает
        if (b.TotalWoodAllSteps > 0) sc += 2;                // цена не нулевая
        if (b.data != null && b.data.StepCount > 0) sc += 1;
        if (b.name.StartsWith("BuildZone")) sc += 1;         // не старая DropZone
        return sc;
    }

    void ResolveSites()
    {
        var all = FindObjectsByType<BuildingController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (hut == null) hut = FindSite(Site.Hut, all);
        if (market == null) market = FindSite(Site.Market, all);
        if (tent == null) tent = FindSite(Site.Tent, all);
        if (bridge == null) bridge = FindSite(Site.Bridge, all);
        if (riverBridge == null) riverBridge = FindFirstObjectByType<RiverBridge>(FindObjectsInactive.Include);

        if (workshop == null && hut != null)
        {
            workshop = hut.GetComponentInParent<PlankWorkshop>();
            if (workshop == null) workshop = FindFirstObjectByType<PlankWorkshop>(FindObjectsInactive.Include);
        }
        if (workshop != null) workshop.unlocked = false;   // включится после главы про хижину

        // вид рынка и палатки переключает мостик, HutStages у них нет
        if (market != null) EnsureLink(market, true);
        if (tent != null) EnsureLink(tent, false);
        if (bridge != null) EnsureBridgeLink(bridge, riverBridge);

        Lock(hut, "задание Оскара");
        Lock(market, "задание Миры");
        Lock(tent, "задание Оскара");
        Lock(bridge, "задание Оскара, " + GameBalance.BridgeLevel + "-й уровень");

        foreach (var sp in FindObjectsByType<Speaker>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (sp != null && string.Equals(sp.displayName, oskarName, StringComparison.OrdinalIgnoreCase)) { oskarGo = sp.gameObject; break; }
    }

    static void EnsureLink(BuildingController b, bool isMarket)
    {
        ShopStages shop = null;
        TentStages tentSt = null;
        if (isMarket)
        {
            shop = b.GetComponentInParent<ShopStages>();
            if (shop == null) shop = FindFirstObjectByType<ShopStages>(FindObjectsInactive.Include);
        }
        else
        {
            tentSt = b.GetComponentInParent<TentStages>();
            if (tentSt == null) tentSt = FindFirstObjectByType<TentStages>(FindObjectsInactive.Include);
        }
        if (shop == null && tentSt == null) return;
        if (b.GetComponent<BuildStageLink>() != null) return;

        var link = b.gameObject.AddComponent<BuildStageLink>();
        link.build = b;
        link.shop = shop;
        link.tent = tentSt;
    }

    // мост: стройка ведёт RiverBridge и открывает проход. Ссылку проставляем и тогда, когда мостик уже висит на зоне
    static void EnsureBridgeLink(BuildingController b, RiverBridge rb)
    {
        if (b == null || rb == null) return;
        var link = b.GetComponent<BuildStageLink>();
        if (link == null)
        {
            link = b.gameObject.AddComponent<BuildStageLink>();
            link.build = b;
        }
        if (link.bridge == null) link.bridge = rb;
        if (link.build == null) link.build = b;
    }

    // storyManaged: вступление (IntroFlow) не трогает эти стройки, их открывают только главы
    static void Lock(BuildingController b, string after)
    {
        if (b == null) return;
        b.storyManaged = true;
        b.completesTutorial = false;
        b.SetLocked(true);
        var view = b.GetComponent<BuildSiteTagView>();
        if (view != null) view.lockedAfter = after;
    }

    static void Unlock(BuildingController b)
    {
        if (b != null) b.SetLocked(false);
    }

    // «Почини хижину: Бревна 3/10»
    string SiteObjective(string title, BuildingController b)
    {
        if (b == null) return title;
        if (inv == null) inv = PlayerInventory.Instance;
        if (b.AllDeposited && !b.IsFinished) return title + ": строим…";
        return title + ": " + b.NeedSummary(inv);
    }

    // ------------------------------------------------------------ цель-виджет (ObjectiveView)
    // Единая точка для главы: виджет, если у неё есть objectiveData с требованиями, иначе прежняя строка
    void ChapterObjective(Chapter ch)
    {
        ObjectiveData d = ch.objectiveData != null ? ch.objectiveData() : null;
        if (d != null && d.HasReqs && UIManager.Instance != null) { UIManager.Instance.SetObjective(d); return; }
        Objective(ch.objective != null ? ch.objective() : "");
    }

    // «Почини хижину»: бревна, доски, монеты этапа. Число «сдано» живое (BuildingController), «в рюкзаке» живое (PlayerInventory)
    ObjectiveData SiteData(string title, BuildingController b)
    {
        if (b == null) return new ObjectiveData(title);
        if (inv == null) inv = PlayerInventory.Instance;
        if (workshop == null) workshop = FindFirstObjectByType<PlankWorkshop>();

        bool building = b.AllDeposited && !b.IsFinished;
        var d = new ObjectiveData(building ? title + ": строим…" : title);
        // «Стадия 1 из 3 · Опоры»: у многоэтапных построек (хижина, мост) видно, на каком этапе игрок
        if (b.StepsTotal > 1) d.Stage(Mathf.Min(b.StepsDone + 1, b.StepsTotal), b.StepsTotal, b.StepName);

        int logs = b.CostTotal, planks = b.PlankTotal, coins = b.CoinTotal;
        if (logs > 0)
            d.Req(UiIcon.Log, "Бревна", logs, () => b.Deposited,
                () => b.Deposited >= logs ? null : "в рюкзаке " + (inv != null ? inv.woodCount : 0) + " · сдано " + b.Deposited + "/" + logs);
        if (planks > 0)
            d.Req(UiIcon.Plank, "Доски", planks, () => b.DepositedPlanks, () =>
            {
                if (b.DepositedPlanks >= planks) return null;
                string s = "в рюкзаке " + (inv != null ? inv.planksCount : 0) + " · сдано " + b.DepositedPlanks + "/" + planks;
                int ready = workshop != null ? workshop.readyPlanks : 0;
                return ready > 0 ? s + " · на складе пилы " + ready + " готово" : s;
            });
        if (coins > 0)
            d.Req(UiIcon.Coin, "Монеты", coins, () => b.DepositedCoins >= coins ? coins : Mathf.Min(inv != null ? inv.coinsCount : 0, coins),
                  () => b.DepositedCoins >= coins ? null : "в кошельке " + (inv != null ? inv.coinsCount : 0));
        return d;
    }

    ObjectiveData BridgeData()
    {
        if (bridge == null) return null;   // нет зоны: BridgeObjective уже пишет причину строкой
        return SiteData("Построй мост", bridge);
    }

    // «Сделай досок для Миры»: заметка «на складе пилы N готово» (их ещё надо забрать)
    ObjectiveData PlanksData()
    {
        if (workshop == null) return null;
        int need = GameBalance.MarketPlanks;
        var d = new ObjectiveData("Сделай досок для Миры");
        d.Req(UiIcon.Plank, "Доски", need, () => workshop.totalCollected, () => workshop.readyPlanks > 0 ? "на складе пилы " + workshop.readyPlanks + " готово: забери" : null);
        d.footer = "Пилорама: <color=#6BCB5A>E</color> у хижины";
        return d;
    }

    // ------------------------------------------------------------ глава «Мост» и записка
    // обязательная глава: пропускать нельзя ни при каких условиях, поэтому всегда false, но об отсутствии зоны кричим один раз
    bool BridgeMissing()
    {
        if (bridge == null && !bridgeWarned)
        {
            bridgeWarned = true;
            Debug.LogError("StoryFlow: нет зоны стройки моста (BuildingController с «bridge»/«мост» в пути). Глава «Мост» обязательна и не пропускается: " +
                           "цепочка остановится здесь. Tools > BuilderSim > Настроить цепочку StoryFlow, потом Setup Doctor.");
        }
        return false;
    }

    string BridgeObjective()
    {
        if (bridge == null) return "Мост: в сцене нет зоны стройки (см. Setup Doctor)";
        int n = Mathf.Min(bridge.StepsDone + 1, bridge.StepsTotal);
        string name = bridge.StepName;
        return SiteObjective("Мост, этап " + n + "/" + bridge.StepsTotal + (string.IsNullOrEmpty(name) ? "" : " «" + name + "»"), bridge);
    }

    Transform NoteTarget()
    {
        if (noteSpot != null) return noteSpot;
        return bridge != null ? bridge.transform : null;
    }

    string NoteObjective()
    {
        return noteStarted ? "Записка Гуннара…" : "Подойди к мосту: под перилами записка";
    }

    // вызывается из опроса главы. Записка читается один раз, только когда мост достроен и Лео рядом
    bool NoteDone()
    {
        if (noteRead) return true;
        if (noteStarted) return false;
        if (bridge == null || !bridge.IsFinished) return false;

        FindRefs();
        var at = NoteTarget();
        if (player != null && at != null)
        {
            Vector3 d = player.position - at.position;
            d.y = 0f;
            if (d.magnitude > 8f) return false;
        }

        // финал: небольшая пауза, чтобы игрок увидел достроенный мост, потом экран уходит в чёрное
        if (useEnding)
        {
            if (endingAt < 0f) endingAt = Time.unscaledTime + 1.8f;
            if (Time.unscaledTime < endingAt) return false;
            noteStarted = true;
            EndingSequence.Play(() => noteRead = true);
            return false;
        }

        noteStarted = true;
        if (Speaker.Find(playerName) == null)
            Debug.LogError("StoryFlow: у игрока нет Speaker «" + playerName + "», записку Гуннара читать некому. Финал засчитан без реплик. Tools > BuilderSim > Добавить реплики Лео игроку.");
        SayNote(NoteLines(), 0);
        return noteRead;
    }

    // реплики по одной; пропуск разговора (удержание E) обрывает цепочку и засчитывает записку прочитанной
    void SayNote(string[] lines, int i)
    {
        if (i >= lines.Length || DialogueManager.SkipAllRequested) { noteRead = true; return; }
        DialogueManager.SayAs(playerName, lines[i], null, () => SayNote(lines, i + 1));
    }

    void EndOfSlice()
    {
        if (useEnding) return;   // «Продолжение следует» уже показала заставка
        FindRefs();
        if (RewardPopups.Instance != null && player != null)
            RewardPopups.Instance.ShowText(player.position + Vector3.up * 3f, "Конец прототипа. Спасибо за игру!", Color.white, 48f);
    }

    // ------------------------------------------------------------ глава «Припасы»
    void BeginSupply()
    {
        var sp = Speaker.Find(miraName);
        if (sp == null) return;
        supply = sp.GetComponent<SupplyPoint>();
        if (supply == null) supply = sp.gameObject.AddComponent<SupplyPoint>();

        Level();
        int need = GameBalance.SupplyLogs;
        int gap = Mathf.Max(0, GameBalance.XpForLevel(GameBalance.UpgradeLevel) - (stats != null ? stats.currentXP : 0));
        supply.Begin(need, Mathf.Max(GameBalance.SupplyMinXp, Mathf.CeilToInt(gap / (float)need)));   // 8 брёвен гарантированно дают нужный уровень
    }

    void EndSupply()
    {
        if (supply != null) supply.End();
    }

    // стрелка главы «Припасы»: к Мире, пока несём брёвна, иначе к ближайшему дереву
    void SupplyArrow(bool carrying)
    {
        if (carrying)
        {
            var mira = Speaker.Find(miraName);
            Arrow(mira != null ? mira.transform : null);
        }
        else
        {
            if (Time.unscaledTime >= treeAt) { treeAt = Time.unscaledTime + 1f; treeCache = NearestTree(); }
            Arrow(treeCache);
        }
    }

    string SupplyObjective()
    {
        FindRefs();
        int done = supply != null ? supply.delivered : 0;
        int need = GameBalance.SupplyLogs;
        bool carrying = inv != null && inv.woodCount > 0;
        SupplyArrow(carrying);

        string head = "Принеси Мире бревна: <color=#6BCB5A>" + done + "/" + need + "</color>";
        return head + (carrying ? ". Отдай: <color=#6BCB5A>E</color> у Миры" : ". Руби деревья");
    }

    ObjectiveData SupplyData()
    {
        FindRefs();
        int need = GameBalance.SupplyLogs;
        bool carrying = inv != null && inv.woodCount > 0;
        SupplyArrow(carrying);

        var d = new ObjectiveData("Принеси Мире бревна");
        d.Req(UiIcon.Log, "Бревна", need, () => supply != null ? supply.delivered : 0,
              () => "в рюкзаке " + (inv != null ? inv.woodCount : 0) + " · сдано " + (supply != null ? supply.delivered : 0) + "/" + need);
        d.footer = carrying ? "Отдай: <color=#6BCB5A>E</color> у Миры" : "Руби деревья";
        return d;
    }

    // ------------------------------------------------------------ глава «Прокачка»
    void BeginUpgradeQuest()
    {
        EnsureWindow();
        var w = upgradeWindow;
        if (w != null)
        {
            w.Unlock();
            w.SetOnly((int)UpgradeType.Capacity);        // пока идёт задание, покупается только рюкзак
            w.SetHighlight((int)UpgradeType.Capacity);
        }

        // страховка: монет за 2-й уровень (30) впритык хватает на рюкзак (30). Если что-то потрачено, Мира добавит недостающее
        FindRefs();
        int lvl = PlayerUpgrades.Instance != null ? PlayerUpgrades.Instance.Level(UpgradeType.Capacity) : 0;
        int cost = GameBalance.UpgradeCost((int)UpgradeType.Capacity, lvl);
        if (inv != null && inv.coinsCount < cost) inv.AddCoins(cost - inv.coinsCount);
    }

    void EndUpgradeQuest()
    {
        if (upgradeWindow == null) return;
        upgradeWindow.SetOnly(-1);                        // дальше можно покупать всё
        upgradeWindow.SetHighlight(-1);
    }

    string UpgradeObjective()
    {
        int cost = GameBalance.UpgradeCost((int)UpgradeType.Capacity, 0);
        if (upgradeWindow != null && upgradeWindow.IsOpen)
            return "Улучши рюкзак: <color=#6BCB5A>3</color> или кнопка за " + cost + " монет";
        return "Открой прокачку: <color=#6BCB5A>U</color> или кнопка справа, и улучши рюкзак";
    }

    void EnsureWindow()
    {
        if (upgradeWindow == null) upgradeWindow = FindFirstObjectByType<UpgradeWindow>();
        if (upgradeWindow == null) upgradeWindow = gameObject.AddComponent<UpgradeWindow>();   // кнопка «Прокачка» (закрыта, пока не откроет сюжет)
    }

    // ------------------------------------------------------------ ожидание уровня
    // Показываем, сколько опыта не хватает, ведём к дереву. Опыт добирается рубкой, а рюкзак при этом наполняется:
    // если он полон, а сдавать брёвна некуда, добрать опыт нечем. Тогда просим сложить брёвна у костра (глава «Прокачка»).
    void WaitForLevel(Chapter ch)
    {
        FindRefs();
        if (ch.dumpWhenFull && HandleFullPack()) return;

        Objective(XpWaitText(ch.minLevel));
        if (Time.unscaledTime >= treeAt)
        {
            treeAt = Time.unscaledTime + 1f;
            treeCache = NearestTree();
        }
        Arrow(treeCache);
    }

    string XpWaitText(int lvl)
    {
        Level();
        int need = GameBalance.XpForLevel(lvl);
        int have = stats != null ? stats.currentXP : 0;
        int trees = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(0, need - have) / (float)GameBalance.XpPerTree));
        return "До " + lvl + "-го уровня: опыт <color=#6BCB5A>" + Mathf.Min(have, need) + "/" + need + "</color>. Руби деревья, ещё "
               + trees + " " + UiDraw.Plural(trees, "дерево", "дерева", "деревьев");
    }

    // true = рюкзак полон и игрок ещё идёт к костру (цель уже выставлена)
    bool HandleFullPack()
    {
        if (pack == null || inv == null || !pack.IsFull) return false;

        var intro = IntroFlow.Instance;
        Transform fire = intro != null ? intro.campfirePoint : null;
        if (fire != null && player != null)
        {
            Vector3 d = player.position - fire.position;
            d.y = 0f;
            if (d.magnitude > 4f)
            {
                Objective("Рюкзак полон: сложи брёвна у костра");
                Arrow(fire);
                return true;
            }
        }

        int n = inv.woodCount;
        if (n > 0)
        {
            inv.RemoveWood(n);
            if (RewardPopups.Instance != null && player != null)
                RewardPopups.Instance.ShowText(player.position + Vector3.up * 3f, "Брёвна сложены", Color.white, 44f);
        }
        return false;
    }

    void FindRefs()
    {
        if (inv == null) inv = PlayerInventory.Instance;
        if (pack == null) pack = FindFirstObjectByType<BackpackTag>();
        if (player == null)
        {
            if (playerMove == null) playerMove = PlayerMovement.Instance;
            if (playerMove != null) player = playerMove.transform;
        }
    }

    Transform NearestTree()
    {
        if (player == null) return null;
        Transform best = null;
        float bestSqr = float.MaxValue;
        foreach (var r in FindObjectsByType<ResourceItem>(FindObjectsSortMode.None))
        {
            if (r == null || !r.gameObject.activeInHierarchy) continue;
            Vector3 d = r.transform.position - player.position;
            d.y = 0f;
            float sq = d.sqrMagnitude;
            if (sq < bestSqr) { bestSqr = sq; best = r.transform; }
        }
        return best;
    }

    // ------------------------------------------------------------ Оскар
    // ------------------------------------------------------------ торговец
    bool merchantWarned;

    // находит торговца и прячет его до конца постройки рынка
    void SetupMerchant()
    {
        if (merchant == null)
        {
            foreach (var sp in FindObjectsByType<Speaker>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (string.Equals(sp.displayName, merchantName, StringComparison.OrdinalIgnoreCase)) { merchant = sp.gameObject; break; }
        }

        if (merchant == null)
        {
            if (market != null && !merchantWarned)
                Debug.LogWarning("StoryFlow: торговец «" + merchantName + "» не найден (Speaker + DialogueSequence + TradingStall). Глава «Первая продажа» будет пропущена.");
            merchantWarned = true;
            return;
        }

        var msp = merchant.GetComponentInChildren<Speaker>(true);
        if (msp != null && !string.Equals(msp.displayName, merchantName, StringComparison.OrdinalIgnoreCase))
            Debug.LogWarning("StoryFlow: у торговца Speaker «" + msp.displayName + "», а в StoryFlow.merchantName «" + merchantName + "». Разговор главы «Первая продажа» не найдёт NPC: приведите имена к одному.", merchant);

        // уже построенный рынок (старт посреди игры) торговца не прячет
        bool built = market != null && market.IsFinished;
        if (!built && merchant.activeSelf) merchant.SetActive(false);
    }

    // включает торговца (onComplete главы «Рынок»); повторный вызов ничего не делает
    void ShowMerchant()
    {
        if (merchant == null && !merchantWarned) SetupMerchant();
        if (merchant != null && !merchant.activeSelf) merchant.SetActive(true);
    }

    void SetupOskar()
    {
        if (oskarGo == null) oskarGo = SpawnOskar();
        if (oskarGo == null) return;
        if (!oskarGo.activeSelf) oskarGo.SetActive(true);   // он стоит у хижины с самого начала
        SetIdle(oskarName, "Погоди, малец. Сначала костёр и разговор с Мирой. Тогда приходи.");
    }

    // Оскара нет в сцене: ставим копию Миры рядом с хижиной (потом замени на свою модель)
    GameObject SpawnOskar()
    {
        var mira = Speaker.Find(miraName);
        if (mira == null || (hut == null && oskarSpot == null)) return null;

        Vector3 pos;
        Quaternion rot;
        if (oskarSpot != null) { pos = oskarSpot.position; rot = oskarSpot.rotation; }
        else
        {
            pos = hut.transform.position + hut.transform.right * 3.5f;
            RaycastHit hit;
            if (Physics.Raycast(pos + Vector3.up * 10f, Vector3.down, out hit, 40f, ~0, QueryTriggerInteraction.Ignore)) pos = hit.point;
            else pos.y = mira.transform.position.y;
            Vector3 look = hut.transform.position - pos;
            look.y = 0f;
            rot = look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : mira.transform.rotation;
        }

        var go = Instantiate(mira.gameObject, pos, rot);
        go.name = "NPC_Oskar";
        foreach (var st in go.GetComponentsInChildren<TradingStall>(true)) Destroy(st);   // торговля только у торговца (если копируем NPC с прилавком)
        var sp = go.GetComponent<Speaker>();
        if (sp != null)
        {
            sp.displayName = oskarName;
            sp.nameColor = new Color32(0xE8, 0x9A, 0x5A, 255);
        }
        Debug.Log("StoryFlow: Оскара не было в сцене, поставил копию Миры рядом с хижиной. Для своей модели создай NPC со Speaker «" +
                  oskarName + "» и DialogueSequence или задай Oskar Spot.", go);
        return go;
    }

    // NPC отвечает одной репликой по E, пока у него нет настоящего разговора главы. Кружок «!» скрыт
    void SetIdle(string giver, string text)
    {
        var sp = Speaker.Find(giver);
        var seq = sp != null ? sp.GetComponent<DialogueSequence>() : null;
        if (seq == null || seq.IsPlaying) return;
        idleSeq = seq;
        idleText = text;
        ApplyIdle();
    }

    void ApplyIdle()
    {
        if (idleSeq == null || idleSeq.IsPlaying) return;
        idleSeq.SetTrigger(DialogueSequence.Trigger.OnInteract);
        idleSeq.once = true;
        idleSeq.SetLines(new[] { L(false, idleText) }, true);
        if (idleSeq.marker != null) idleSeq.marker.SetVisible(false);
    }

    // ------------------------------------------------------------ сохранение (ISaveable, ключ «story»)
    public string SaveKey { get { return "story"; } }
    public int LoadOrder { get { return 70; } }

    // ДОБАВЛЯТЬ ПОЛЯ ТОЛЬКО С ДЕФОЛТАМИ: старые файлы без них читаются нормально
    [Serializable]
    class SaveData
    {
        public int version = 1;
        public string chapterId = "";     // id главы (надёжнее индекса: главы можно переставить)
        public int chapterIndex;          // запасной вариант, если id не нашёлся
        public int phase;                 // 0 = ждём/разговор «до» (его покажем заново), 1 = задание принято
        public bool allDone;
        public bool firstSale;
        public bool noteRead;
        public int supplyDelivered;       // сколько брёвен уже отдано Мире в главе «Припасы»
        public int planksCollected;       // сколько досок сделано на пиле (глава «Доски»)
    }

    SaveData restoreData;
    bool restorePending, started, needHudRefresh;
    int restoreSupply;

    public string Save()
    {
        if (restorePending && restoreData != null) return JsonUtility.ToJson(restoreData);   // ещё не применили: не затираем дефолтами

        var d = new SaveData { allDone = AllDone, firstSale = firstSale, noteRead = noteRead, chapterIndex = ci };
        if (ci < chapters.Count) d.chapterId = chapters[ci].id;
        // Offer пишем как «ещё не взяли» (разговор покажется снова), Report как «задание идёт» (отчёт ещё не звучал)
        d.phase = (phase == Phase.Do || phase == Phase.Report) ? 1 : 0;
        if (supply != null) d.supplyDelivered = supply.delivered;
        if (workshop != null) d.planksCollected = workshop.totalCollected;
        return JsonUtility.ToJson(d);
    }

    public void Load(string json, int formatVersion)
    {
        SaveData d = null;
        if (!string.IsNullOrEmpty(json))
        {
            try { d = JsonUtility.FromJson<SaveData>(json); }
            catch (Exception e) { Debug.LogWarning("StoryFlow: не удалось прочитать сохранение сюжета: " + e.Message); }
        }
        if (d == null) { ResetToDefault(); return; }

        restoreData = d;
        restorePending = true;
        if (started) ApplyRestore();   // Load в середине игры: сцена уже стартовала
    }

    public void ResetToDefault()
    {
        restoreData = null;
        restorePending = false;
        if (!started) return;   // до Start поля и так по умолчанию

        ci = 0;
        phase = Phase.Enter;
        talk = null;
        idleSeq = null;
        supply = null;
        AllDone = false;
        PetsOpen = false;
        firstSale = noteRead = noteStarted = false;
        endingAt = -1f;
        if (hut != null) hut.SetLocked(true);
        if (market != null) market.SetLocked(true);
        if (tent != null) tent.SetLocked(true);
        if (bridge != null) bridge.SetLocked(true);
        if (workshop != null) workshop.unlocked = false;
        Arrow(null);
        Objective("");
        pollAt = 0f;
    }

    // Ставит главу туда, где остановились, БЕЗ диалогов, наград и QuestDone. Выданное раньше (XP, монеты, питомцы) лежит в своих секциях сохранения
    void ApplyRestore()
    {
        var d = restoreData;
        restoreData = null;
        restorePending = false;
        if (d == null) return;

        int idx = -1;
        if (!string.IsNullOrEmpty(d.chapterId))
            for (int i = 0; i < chapters.Count; i++)
                if (chapters[i].id == d.chapterId) { idx = i; break; }
        if (idx < 0) idx = Mathf.Clamp(d.chapterIndex, 0, chapters.Count);
        if (d.allDone) idx = chapters.Count;

        ci = idx;
        phase = (d.phase == 1 && ci < chapters.Count) ? Phase.Do : Phase.Enter;
        talk = null;
        idleSeq = null;
        endingAt = -1f;
        firstSale = d.firstSale;
        noteRead = d.noteRead;
        noteStarted = false;
        AllDone = d.allDone;
        restoreSupply = d.supplyDelivered;

        // ВОЗМОЖНА ОШИБКА КОМПИЛЯТОРА: если totalCollected у PlankWorkshop не public int (например, только get), закомментируй строку
        if (workshop != null && d.planksCollected > workshop.totalCollected) workshop.totalCollected = d.planksCollected;

        // глава идёт, значит вступление пройдено (старый файл мог не успеть записать секцию intro)
        if ((ci > 0 || AllDone) && IntroFlow.Instance != null) IntroFlow.Instance.RestoreDoneFromStory();

        // закрытые главы: тихо повторяем открытия и реплики «после главы» (кроме Оскара «Погоди», его уже перекрыли)
        for (int i = 0; i < ci && i < chapters.Count; i++)
        {
            var c = chapters[i];
            if (c.restoreDone != null) c.restoreDone();
            if (!string.IsNullOrEmpty(c.idleAfter)) SetIdle(c.giver, c.idleAfter);
        }

        // текущая глава в процессе: принятие задания без наград, напоминание у NPC, цель и стрелка
        if (phase == Phase.Do)
        {
            var ch = chapters[ci];
            if (ch.restoreActive != null) ch.restoreActive();
            if (!string.IsNullOrEmpty(ch.reminder)) SetIdle(ch.giver, ch.reminder);
        }

        if (AllDone) { Objective(""); Arrow(null); }
        else needHudRefresh = true;   // вступление после загрузки ещё закроет цель (SetStep Done): выставим цель заново, когда оно кончится
        RefreshHud();
        pollAt = 0f;
    }

    // цель в панели и стрелка для главы в процессе (в Enter их ведёт Tick: ожидание уровня, разговор)
    void RefreshHud()
    {
        if (AllDone || ci >= chapters.Count || phase != Phase.Do) return;
        var ch = chapters[ci];
        ChapterObjective(ch);
        Arrow(ch.target != null ? ch.target() : null);
    }

    // стройка встала в сохранённое состояние: торговец у готового рынка должен быть на месте, цели пересчитываются сразу
    void OnBuildingRestored(string id, int stage)
    {
        var b = BuildingController.FindById(id);
        if (b != null && b == market && b.IsFinished) ShowMerchant();
        pollAt = 0f;
    }

    // Load: Мира (не показываем «Припасы» заново, но продолжаем с тем же счётом)
    void RestoreSupply()
    {
        BeginSupply();
        // ВОЗМОЖНА ОШИБКА КОМПИЛЯТОРА: если delivered у SupplyPoint не public int с set, закомментируй строку (тогда счёт начнётся с нуля)
        if (supply != null && restoreSupply > 0) supply.delivered = restoreSupply;
    }

    // Load: окно прокачки после закрытой главы «Прокачка» (всё доступно) и в её процессе (только рюкзак). Монеты-страховку НЕ добавляем
    void RestoreUpgradeDone()
    {
        EnsureWindow();
        if (upgradeWindow == null) return;
        upgradeWindow.Unlock();
        upgradeWindow.SetOnly(-1);
        upgradeWindow.SetHighlight(-1);
    }

    void RestoreUpgradeActive()
    {
        EnsureWindow();
        if (upgradeWindow == null) return;
        upgradeWindow.Unlock();
        upgradeWindow.SetOnly((int)UpgradeType.Capacity);
        upgradeWindow.SetHighlight((int)UpgradeType.Capacity);
    }

    // ------------------------------------------------------------ утилиты
    int Level()
    {
        if (stats == null) stats = FindFirstObjectByType<PlayerStats>();
        return stats != null ? stats.currentLevel : 1;
    }

    static void Objective(string text)
    {
        if (UIManager.Instance != null) UIManager.Instance.UpdateObjective(text);
    }

    static void Arrow(Transform t) { QuestEdgeArrow.Target = t; }
}
