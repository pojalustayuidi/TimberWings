// The campfire tutorial hands off to the supply stockpile, upgrades, hut, saw, stall, crew,
// pet crate, forge, bridge, and portal. Only introductions use required conversations.
// Existing chapter identifiers and save ordering keep older progress and completed sites valid.
using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-10)]
public partial class StoryFlow : MonoBehaviour, ISaveable
{
    const string GunnarAxeChapter = "gunnarAxe";

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
    [Tooltip("Включает финал главы. На дальнем острове заставка запускается через портал, в старых сценах — через записку.")]
    public bool useEnding = true;
    [Tooltip("Радиус около точки записки, в котором Лео «подошёл» (метры). Работает, когда точка записки задана (площадка на дальнем острове). " +
             "Держи его заметно меньше расстояния до главного берега: иначе финал сработает не там")]
    public float noteRadius = 3.5f;

    [Header("Звери-помощники")]
    [Tooltip("Кто даёт главу про питомцев. Сейчас Оскар. Будет свой NPC: впиши его Speaker.displayName (и добавь имя в VoiceId.SpeakerKey)")]
    public string petsGiverName = "Оскар";
    [Tooltip("Куда ведёт стрелка в главе питомцев. Пусто = найдётся PetCrateSite с Лесной клетью")]
    public Transform petCrateSpot;

    [Header("Торговец")]
    [Tooltip("NPC-торговец у прилавка (Speaker + DialogueSequence + NpcMarker + TradingStall). Пусто = найдётся по merchantName, в том числе среди выключенных. До постройки рынка объект выключается, после главы «Рынок» включается")]
    public GameObject merchant;
    [Tooltip("Точка продажи у здания. Рико использует эту точку после знакомства.")]
    public bool tradeAtBuilding;
    public TradingStall marketTrading;
    [Tooltip("Точка у лавки, в которой стоят ноги Рико. Пусто = место за прилавком.")]
    public Transform ricoSpot;

    [Header("Имена говорящих (как в Speaker.displayName)")]
    public string miraName = "Мира";
    public string oskarName = "Оскар";
    public string playerName = "Лео";
    [Tooltip("Имя NPC-торговца (Speaker.displayName). Если переименуете, добавьте новое имя в VoiceId.SpeakerKey")]
    public string merchantName = "Торговец";

    [Header("Тексты")]
    public string levelText = "Нужен {0}-й уровень: руби дрова и строй";
    public string talkToText = "Поговори с {0}: <color=#6BCB5A>E</color>";
    [HideInInspector] public string returnToText = "Вернись к {0}: <color=#6BCB5A>E</color>";

    // торговля открыта, когда прилавок построен (или рынка в сцене нет)
    public bool MarketOpen { get; private set; }
    public bool AllDone { get; private set; }
    // клеть питомцев в лагере доступна (глава «Звери-помощники» принята или пропущена). Без StoryFlow клеть открыта по уровню
    public bool PetsOpen { get; private set; }
    public int ChapterIndex { get { return ci; } }
    public string CurrentChapterId { get { return ci >= 0 && ci < chapters.Count ? chapters[ci].id : ""; } }
    public string CurrentChapterPhase { get { return phase.ToString(); } }
    public int SupplyDelivered { get { return supply != null ? supply.delivered : 0; } }
    public bool TaskAccepted { get { return phase == Phase.Do; } }
    internal bool MapFinaleAvailable => useEnding && bridge != null && bridge.IsFinished
        && (noteSpot != null || (finalBridge != null && finalBridge.Portal != null));
    internal bool MapFinaleInspected => noteRead;
    internal Transform MapFinaleTarget => NoteTarget();

    internal bool MapChapterCompleted(string chapterId)
    {
        for (int i = 0; i < ci && i < chapters.Count; i++)
        {
            var chapter = chapters[i];
            if (chapter.id == chapterId) return true;
        }
        return false;
    }
    internal bool AxeForgeAvailable => RuneForge.Active == null && workshop != null && workshop.unlocked &&
        (tent == null || tent.IsFinished);
    internal bool GunnarBladeAvailable => (CurrentChapterId == GunnarAxeChapter && TaskAccepted) ||
        MapChapterCompleted(GunnarAxeChapter);

    internal bool CanUseAxeForge(PlayerAxeEquipment axes) => AxeForgeAvailable && axes != null &&
        PlayerInteraction.CurrentAxes == axes && workshop.isActiveAndEnabled && workshop.DistanceToPlayer() <= workshop.radius;

    public event Action ChaptersFinished;

    // ------------------------------------------------------------ главы
    class Chapter
    {
        public string id;
        public string giver;                       // у кого разговоры (null = без NPC)
        public int minLevel = 1;
        public DialogueSequence.Line[] offer;      // разговор перед заданием (null = сразу задание)
        public Func<string> objective;             // текст цели, пока идёт задание (запасной путь, если objectiveData нет или пуст)
        public Func<ObjectiveData> objectiveData;  // цель-виджет: иконки, счётчики, прогресс (необязательно)
        public Func<bool> done;                    // задание выполнено?
        public Func<Transform> target;             // куда ведёт стрелка
        public Func<bool> skip;                    // true = главу пропустить (нет здания или NPC)
        public string talkText;                    // цель на время разговора «до» (пусто = talkToText «Поговори с {0}»); для имён, которые не склоняются
        public string reminder;                    // что NPC отвечает, пока идёт задание (E на нём)
        public string idleAfter;                   // что NPC отвечает после главы
        public Action onAccept;                    // после разговора offer
        public Action onComplete;                  // награда и открытия
        public Action restoreDone;                 // Load: тихо повторить открытия уже закрытой главы (без наград, диалогов, событий)
        public Action restoreActive;               // Load: тихо повторить принятие задания, если глава в процессе (onAccept без наград)
    }

    enum Phase { Enter, Offer, Do }

    readonly List<Chapter> chapters = new List<Chapter>();
    int ci;
    Phase phase = Phase.Enter;
    DialogueSequence talk;
    bool introDone, firstSale, warnedNoGiver;
    bool bridgeWarned, noteStarted, noteRead, petsCrateWarned;
    GateBridge finalBridge;
    int finaleAttempt;
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
    RicoMerchant rico;

    // ------------------------------------------------------------ жизненный цикл
    void Awake()
    {
        Instance = this;
        ResolveSites();
        rico = RicoMerchant.Create(this);
        if (rico != null)
        {
            merchant = rico.gameObject;
            merchantName = RicoMerchant.DisplayName;
            marketTrading = rico.Stall;
        }
        BuildChapters();
        SaveSystem.Register(this);   // если Load() уже был, секция придёт сюда, а применится в конце Start
    }

    void Start()
    {
        EnsureWindow();
        if (GetComponent<ChapterOneJournal>() == null) gameObject.AddComponent<ChapterOneJournal>();
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
        EnableChapterOneEvents();
    }

    void OnDisable()
    {
        GameEvents.DialogueFinished -= OnDialogue;
        GameEvents.BuildRevealFinished -= OnBuildRevealFinished;
        GameEvents.BuildingRestored -= OnBuildingRestored;
        TradingStall.Sold -= OnSold;
        DisableChapterOneEvents();
    }

    // показ стадии здания закончился: сразу проверяем цель, а не ждём следующего опроса (0.25 с)
    void OnBuildRevealFinished(BuildingController b)
    {
        pollAt = 0f;
        ChapterOneBuilt(b);
    }

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
        ChapterOneVoice.Tick();
        bool introOver = IntroFinished();
        // рынок без вступления закрыт, даже если прилавок в сцене не найден
        MarketOpen = introOver && (market == null || market.IsFinished);
        if (rico != null && rico.gameObject.activeSelf != MarketOpen) rico.gameObject.SetActive(MarketOpen);
        // рынка в сцене нет: глава «Рынок» пропущена и её onComplete не сработает, торговец выходит сразу после вступления
        if (market == null && MarketOpen) ShowMerchant();

        if (!introDone)
        {
            if (!introOver) return;
            introDone = true;
            if (FindFirstObjectByType<QuestEdgeArrow>() == null) gameObject.AddComponent<QuestEdgeArrow>();
        }

        if (Time.unscaledTime < pollAt) return;
        pollAt = Time.unscaledTime + 0.25f;
        PollChapterOne();
        if (!AllDone) Tick();
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

#if UNITY_EDITOR
    // QA-переход через тот же механизм, что загрузка сохранения, без наград и QuestDone.
    // accepted=true сразу открывает стройку / задание, без разговора с Оскаром.
    public bool EditorTestJumpToChapter(string chapterId, bool accepted = true)
    {
        if (!Application.isPlaying || !started || SaveSystem.IsLoading) return false;
        // EditorTestMode is supplied by the optional Bridge QA package.
        var testMode = typeof(SaveSystem).GetProperty("EditorTestMode",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        if (!(testMode?.GetValue(null) is true)) return false;
        int index = chapters.FindIndex(c => c.id == chapterId);
        if (index < 0) return false;
        Load(JsonUtility.ToJson(new SaveData
        {
            chapterId = chapterId,
            chapterIndex = index,
            phase = accepted ? 1 : 0,
            firstSale = true
        }), SaveSystem.FormatVersion);
        introDone = true;
        return ci == index && phase == (accepted ? Phase.Do : Phase.Enter);
    }
#endif

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
                if (Level() < ch.minLevel && (ch.id != "bridge" || bridge == null || !bridge.IsFinished))
                {
                    WaitForLevel(ch);
                    return;
                }
                if (ch.offer == null || (ch.id == "hut" && hut != null && hut.IsFinished) || !BeginTalk(ch, ch.offer, Phase.Offer, string.IsNullOrEmpty(ch.talkText) ? talkToText : ch.talkText))
                {
                    // нет разговора (или NPC не найден): принимаем задание сразу, иначе здание осталось бы закрытым
                    if (ch.onAccept != null) ch.onAccept();
                    BeginDo(ch);
                }
                break;

            case Phase.Do:
                ChapterObjective(ch);
                if (ch.done == null || ch.done()) Complete(ch);
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
    }

    void OnSold()
    {
        if (!firstSale) ChapterOneVoice.Queue("twv3_s08_04");
        firstSale = true;
    }

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
        seq.isQuestReminder = false;
        seq.SetTrigger(DialogueSequence.Trigger.OnInteract);
        seq.once = true;
        seq.SetLines(lines, true);

        phase = next;
        Objective(string.Format(GameLanguage.T(objectiveFormat), ChapterOneText.SpeakerName(ch.giver)));
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
        ChapterOneCompleted(ch.id);
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
        chapters.Add(new Chapter
        {
            id = "supply",
            onAccept = BeginSupply,
            objective = SupplyObjective,
            objectiveData = SupplyData,
            done = () => supply == null || supply.delivered >= supply.need,
            onComplete = EndSupply,
            restoreActive = RestoreSupply,
            target = () => supply != null ? supply.transform : null
        });
        chapters.Add(new Chapter
        {
            id = "upgrade", minLevel = GameBalance.UpgradeLevel,
            skip = () => PlayerUpgrades.Instance == null,
            onAccept = RestoreUpgradeDone,
            objective = () => ChapterOneText.Text("quest.s05.objective", "Открой улучшения и выбери, что развивать."),
            done = () => upgradeWindow == null || upgradeWindow.HasOpened,
            restoreDone = RestoreUpgradeDone,
            restoreActive = RestoreUpgradeDone
        });
        chapters.Add(new Chapter
        {
            id = "hut", giver = oskarName, minLevel = GameBalance.HutLevel,
            offer = ChapterOneText.Lines("twv3_s06_intro_01", "twv3_s06_intro_02", "twv3_s06_intro_03", "twv3_s06_intro_04"),
            talkText = "Поговори с Оскаром у хижины",
            reminder = "Сначала стены, потом крыша. Материалы указаны на стройке.",
            skip = () => hut == null,
            onAccept = () => Unlock(hut),
            objective = () => SiteObjective(HutTitle(), hut),
            objectiveData = () => SiteData(HutTitle(), hut),
            done = () => hut.IsFinished,
            target = () => hut.transform,
            onComplete = () => { if (workshop != null) workshop.unlocked = true; },
            restoreDone = () => { Unlock(hut); if (workshop != null) workshop.unlocked = true; },
            restoreActive = () => Unlock(hut)
        });
        chapters.Add(new Chapter
        {
            id = "planks",
            skip = () => workshop == null,
            objective = () => ChapterOneText.Format("quest.s07.craft", "Сделай {required} досок на пилораме.", ("required", GameBalance.MarketPlanks)),
            objectiveData = PlanksData,
            done = () => workshop.totalCollected >= GameBalance.MarketPlanks,
            target = () => workshop.transform
        });
        chapters.Add(new Chapter
        {
            id = "market",
            skip = () => market == null,
            onAccept = () => Unlock(market),
            onComplete = ShowMerchant,
            restoreDone = () => { Unlock(market); ShowMerchant(); },
            restoreActive = () => Unlock(market),
            objective = () => SiteObjective(ChapterOneText.Text("quest.s07.build", "Построй прилавок."), market),
            objectiveData = () => SiteData(ChapterOneText.Text("quest.s07.build", "Построй прилавок."), market),
            done = () => market.IsFinished,
            target = () => market.transform
        });
        bool merchantIntro = rico != null || !tradeAtBuilding;
        chapters.Add(new Chapter
        {
            id = "firstSale", giver = merchantIntro ? merchantName : null,
            offer = merchantIntro ? ChapterOneText.Lines("twv3_s08_intro_01", "twv3_s08_intro_02", "twv3_s08_01", "twv3_s08_02") : null,
            talkText = rico != null ? "Познакомься с Рико у прилавка" : "Подойди к прилавку",
            skip = () => market == null || firstSale || (merchantIntro && merchant == null),
            objective = () => ChapterOneText.Text("quest.s08.sell", "Сделай первую продажу."),
            done = () => firstSale,
            target = () => tradeAtBuilding || rico != null
                ? (marketTrading != null ? marketTrading.transform : market != null ? market.transform : null)
                : (merchant != null ? merchant.transform : null)
        });
        chapters.Add(new Chapter
        {
            id = "tent", minLevel = GameBalance.TentLevel,
            skip = () => tent == null,
            onAccept = () => { Unlock(tent); OfferChapterCrew(); },
            objective = () => SiteObjective(ChapterOneText.Text("quest.s10.repair", "Восстанови палатку артели."), tent),
            objectiveData = () => SiteData(ChapterOneText.Text("quest.s10.repair", "Восстанови палатку артели."), tent),
            done = () => tent.IsFinished,
            target = () => tent.transform,
            restoreDone = () => Unlock(tent),
            restoreActive = () => Unlock(tent)
        });
        chapters.Add(new Chapter
        {
            id = "pets", minLevel = GameBalance.PetCrateLevel[0],
            skip = PetsSkip,
            onAccept = PetsAccept,
            objective = () => ChapterOneText.Text("quest.s11.objective", "Открой бесплатную Лесную клеть у палатки."),
            done = () => PetManager.Instance == null || PetManager.Instance.FreeCrateUsed,
            target = PetCrateTarget,
            restoreDone = () => PetsOpen = true,
            restoreActive = PetsAccept
        });
        chapters.Add(new Chapter
        {
            id = GunnarAxeChapter,
            skip = () => RuneForge.Active == null && (workshop == null || hut == null || tent == null),
            objective = GunnarAxeObjective,
            done = () => PlayerInteraction.CurrentAxes != null && PlayerInteraction.CurrentAxes.ForgeRestored,
            target = GunnarAxeTarget
        });
        chapters.Add(new Chapter
        {
            id = "bridge", minLevel = GameBalance.BridgeLevel,
            skip = BridgeMissing,
            onAccept = () => { Unlock(bridge); OfferChapterBridge(); },
            objective = BridgeObjective,
            objectiveData = BridgeData,
            done = () => bridge != null && bridge.IsFinished,
            target = () => bridge != null ? bridge.transform : null,
            restoreDone = () => Unlock(bridge),
            restoreActive = () => Unlock(bridge)
        });
        chapters.Add(new Chapter
        {
            id = "gunnar",
            objective = NoteObjective,
            done = NoteDone,
            target = NoteTarget,
            onComplete = EndOfSlice
        });
    }

    string HutTitle() => ChapterOneText.Text(hut != null && hut.StepsDone > 0 ? "quest.s06.roof" : "quest.s06.walls",
        hut != null && hut.StepsDone > 0 ? "Почини крышу." : "Почини стены.");

    string GunnarAxeObjective()
    {
        var title = ChapterOneText.Text("quest.s12.objective", "Восстанови кузницу Гуннара.");
        var forge = RuneForge.Active;
        if (forge != null && forge.build != null) return SiteObjective(title, forge.build);
        return title + " · " + PlayerAxeEquipment.ForgePlanks + " " + GameLanguage.T("Доски") +
            " · " + PlayerAxeEquipment.ForgeCoins + " " + GameLanguage.T("Монеты");
    }

    Transform GunnarAxeTarget()
    {
        var forge = RuneForge.Active;
        if (forge != null && forge.build != null) return forge.IsRestored ? forge.interior : forge.build.transform;
        return workshop != null ? workshop.Anchor : null;
    }

    // реплика: hero = true говорит Лео, иначе NPC (его Speaker).
    DialogueSequence.Line L(bool hero, string text)
    {
        return new DialogueSequence.Line { speaker = hero ? playerName : "", text = text, pauseAfter = 0.3f };
    }

    // ------------------------------------------------------------ глава «Звери-помощники»
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

    // знакомство с торговцем: первая реплика его (NPC), Лео отвечает
    // записка Гуннара: читает Лео вслух. ТЕКСТ ЧЕРНОВОЙ, замени на авторский из GDD (до 120 символов в реплике)
    string[] NoteLines()
    {
        return new[]
        {
            "На камне под аркой лежит записка. Почерк дяди Гуннара.",
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
        var gate = FindFirstObjectByType<GateBridge>();
        // У финального моста путь BuildZone фиксирован для совместимости SaveId;
        // не выбираем случайную старую стройку через общий поиск по сцене.
        if (gate != null && gate.HasFinalIsland)
        {
            var baked = gate.transform.Find("BuildZone");
            bridge = baked != null ? baked.GetComponent<BuildingController>() : null;
        }
        else if (bridge == null) bridge = FindSite(Site.Bridge, all);
        if (gate != null && gate.HasFinalIsland)
        {
            finalBridge = gate;
            riverBridge = gate.EnsureProgressSource(riverBridge);
            if (bridge == null)
                Debug.LogError("StoryFlow: зона стройки моста не сохранена в сцене. В Edit Mode пересобери GateBridge. Runtime-генерация отключена.", gate);
        }
        else if (riverBridge == null) riverBridge = FindFirstObjectByType<RiverBridge>(FindObjectsInactive.Include);
        if (noteSpot == null && gate != null) noteSpot = gate.noteSpot;

        if (workshop == null && hut != null)
        {
            workshop = hut.GetComponentInParent<PlankWorkshop>();
            if (workshop == null) workshop = FindFirstObjectByType<PlankWorkshop>(FindObjectsInactive.Include);
        }
        if (workshop != null) workshop.unlocked = false;   // включится после главы про хижину

        // вид рынка и палатки переключает мостик, HutStages у них нет
        if (market != null) EnsureLink(market, true);
        if (tent != null) EnsureLink(tent, false);
        if (bridge != null) EnsureBridgeLink(bridge, riverBridge, finalBridge != null);

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
    static void EnsureBridgeLink(BuildingController b, RiverBridge rb, bool finalCrossing)
    {
        if (b == null || rb == null) return;
        var link = b.GetComponent<BuildStageLink>();
        if (link == null)
        {
            link = b.gameObject.AddComponent<BuildStageLink>();
            link.build = b;
        }
        if (link.bridge == null || finalCrossing) link.bridge = rb;
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
        if (b.AllDeposited && !b.IsFinished) return title + ChapterOneText.Pick(" · строим…", " · building…");
        return title + " · " + b.NeedSummary(inv);
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
        var d = new ObjectiveData(building ? title + ChapterOneText.Pick(" · строим…", " · building…") : title);
        // «Стадия 1 из 3 · Опоры»: у многоэтапных построек (хижина, мост) видно, на каком этапе игрок
        if (b.StepsTotal > 1) d.Stage(Mathf.Min(b.StepsDone + 1, b.StepsTotal), b.StepsTotal, GameLanguage.T(b.StepName));

        int logs = b.CostTotal, planks = b.PlankTotal, coins = b.CoinTotal;
        if (logs > 0)
            d.Req(UiIcon.Log, GameLanguage.T("Бревна"), logs, () => b.Deposited,
                () => b.Deposited >= logs ? null : ChapterOneText.Pick("в рюкзаке ", "in pack ") + (inv != null ? inv.woodCount : 0) + ChapterOneText.Pick(" · сдано ", " · stored ") + b.Deposited + "/" + logs);
        if (planks > 0)
            d.Req(UiIcon.Plank, GameLanguage.T("Доски"), planks, () => b.DepositedPlanks, () =>
            {
                if (b.DepositedPlanks >= planks) return null;
                string s = ChapterOneText.Pick("в рюкзаке ", "in pack ") + (inv != null ? inv.planksCount : 0) + ChapterOneText.Pick(" · сдано ", " · stored ") + b.DepositedPlanks + "/" + planks;
                int ready = workshop != null ? workshop.readyPlanks : 0;
                return ready > 0 ? s + ChapterOneText.Pick(" · на складе пилы ", " · at saw ") + ready : s;
            });
        if (coins > 0)
            d.Req(UiIcon.Coin, GameLanguage.T("Монеты"), coins, () => b.DepositedCoins >= coins ? coins : Mathf.Min(inv != null ? inv.coinsCount : 0, coins),
                  () => b.DepositedCoins >= coins ? null : ChapterOneText.Pick("в кошельке ", "in wallet ") + (inv != null ? inv.coinsCount : 0));
        return d;
    }

    ObjectiveData BridgeData()
    {
        if (bridge == null) return null;   // нет зоны: BridgeObjective уже пишет причину строкой
        return SiteData(BridgeStageTitle(), bridge);
    }

    // «Сделай досок для Миры»: заметка «на складе пилы N готово» (их ещё надо забрать)
    ObjectiveData PlanksData()
    {
        if (workshop == null) return null;
        int need = GameBalance.MarketPlanks;
        var d = new ObjectiveData(ChapterOneText.Format("quest.s07.craft", "Сделай {required} досок на пилораме.", ("required", need)));
        d.Req(UiIcon.Plank, GameLanguage.T("Доски"), need, () => workshop.totalCollected, () => workshop.readyPlanks > 0 ? ChapterOneText.Pick("Готово на пилораме: ", "Ready at the saw: ") + workshop.readyPlanks : null);
        d.footer = ChapterOneText.Text("tutorial.saw.output", "Забери готовые доски.");
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

    string BridgeObjective() => bridge == null
        ? ChapterOneText.Pick("Мост: в сцене нет зоны стройки.", "The bridge build site is missing.")
        : SiteObjective(BridgeStageTitle(), bridge);

    string BridgeStageTitle() => bridge.StepsDone switch
    {
        0 => ChapterOneText.Text("quest.s15.supports", "Построй опоры."),
        1 => ChapterOneText.Text("quest.s15.deck", "Уложи настил."),
        _ => ChapterOneText.Text("quest.s15.railings", "Добавь тросы и перила.")
    };

    Transform NoteTarget()
    {
        if (useEnding && finalBridge != null && finalBridge.Portal != null) return finalBridge.Portal.Anchor;
        if (noteSpot != null) return noteSpot;
        return bridge != null ? bridge.transform : null;
    }

    string NoteObjective()
    {
        if (useEnding && finalBridge != null)
        {
            if (noteStarted) return "";
            FindRefs();
            var portal = finalBridge.Portal;
            return player != null && portal != null && portal.OnFarIsland(player.position)
                ? ChapterOneText.Text("quest.s16.activate", "Активируй камень у арки.")
                : ChapterOneText.Text("quest.s16.cross", "Перейди мост на дальний остров.");
        }
        if (noteStarted) return GameLanguage.T("Записка Гуннара…");
        // мост готов: игроку надо перейти на дальний остров к площадке с запиской (стрелка ведёт туда же, см. NoteTarget)
        return noteSpot != null ? GameLanguage.T("Перейди мост: записка Гуннара у арки на дальнем острове") : GameLanguage.T("Подойди к мосту: под перилами записка");
    }

    // вызывается из опроса главы. Записка читается один раз, только когда мост достроен и Лео рядом
    bool NoteDone()
    {
        if (noteRead) return true;
        if (noteStarted) return false;
        if (bridge == null || !bridge.IsFinished) return false;
        if (useEnding && finalBridge != null) return false;   // на новом острове финал запускается взаимодействием

        FindRefs();
        var at = NoteTarget();
        if (player == null || at == null) return false;
        Vector3 offset = player.position - at.position;
        float height = Mathf.Abs(offset.y);
        offset.y = 0f;
        if (noteSpot != null)
        {
            if (offset.magnitude > noteRadius || height > 3f)
            {
                endingAt = -1f;
                return false;
            }
        }
        else if (offset.magnitude > 8f) return false;

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

    internal bool CanActivatePortal(FinalIslandPortal portal)
    {
        return useEnding && introDone && !AllDone && !noteStarted && !noteRead
            && ci < chapters.Count && chapters[ci].id == "gunnar" && phase == Phase.Do
            && finalBridge != null && finalBridge.Portal == portal
            && bridge != null && bridge.IsFinished && finalBridge.WalkwayActive
            && !BuildingController.AnyRevealing && !BuildingController.AnyRestorePending
            && !SaveSystem.IsLoading && !EndingSequence.Active;
    }

    internal bool PreparingChapterPortal { get; private set; }

    internal void ActivatePortal(FinalIslandPortal portal, PlayerMovement actor)
    {
        if (!CanActivatePortal(portal) || actor == null || !portal.OnFarIsland(actor.transform.position)) return;
        int attempt = ++finaleAttempt;
        noteStarted = true;
        PreparingChapterPortal = true;
        actor.CancelWalk();
        Arrow(null);
        Objective("");
        var lines = new List<string>();
        if (!ChapterOneVoice.Heard("twv3_s16_arrive_01")) lines.Add("twv3_s16_arrive_01");
        if (!ChapterOneVoice.Heard("twv3_s16_01a") && !ChapterOneVoice.Heard("twv3_s16_01b"))
            lines.Add(ArchVoice());
        DialogueSeen.Mark(ArchKnown);
        lines.AddRange(new[] { "twv3_s16_activate_01", "twv3_s16_gunnar_01", "twv3_s16_gunnar_02", "twv3_s16_leave_01" });
        ChapterOneVoice.SpeakSequence(() =>
        {
            if (this == null || attempt != finaleAttempt) return;
            PreparingChapterPortal = false;
            if (portal != null && portal.Play(actor, () =>
            {
                if (this == null || attempt != finaleAttempt) return;
                noteRead = true;
                pollAt = 0f;
                SaveSystem.RequestSave("story");
            }, AbortPortal)) return;
            AbortPortal();
        }, lines.ToArray());

        void AbortPortal()
        {
            if (this == null || attempt != finaleAttempt) return;
            noteStarted = false;
            RefreshHud();
        }
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
    void BeginSupply() => PrepareSupply(0);

    void PrepareSupply(int savedLogs)
    {
        var intro = IntroFlow.Instance;
        var fire = intro != null ? intro.campfirePoint : null;
        if (fire == null)
        {
            var site = IntroFlow.FindCampfireBuild(null);
            if (site != null) fire = site.transform;
        }
        if (fire == null) return;

        if (supply == null)
        {
            var point = new GameObject("Campfire Stockpile");
            point.transform.SetParent(transform, false);
            point.transform.position = fire.position;
            supply = point.AddComponent<SupplyPoint>();
        }
        Level();
        int need = GameBalance.SupplyLogs;
        int gap = Mathf.Max(0, GameBalance.XpForLevel(GameBalance.UpgradeLevel) - (stats != null ? stats.currentXP : 0));
        int stored = Mathf.Clamp(savedLogs, 0, need);
        int remaining = Mathf.Max(1, need - stored);
        supply.Begin(need, Mathf.Max(GameBalance.SupplyMinXp, Mathf.CeilToInt(gap / (float)remaining)));
        supply.delivered = stored;
    }

    void EndSupply()
    {
        if (supply != null) supply.End();
    }

    void SupplyArrow(bool carrying)
    {
        if (carrying && supply != null) { Arrow(supply.transform); return; }
        if (Time.unscaledTime >= treeAt) { treeAt = Time.unscaledTime + 1f; treeCache = NearestTree(); }
        Arrow(treeCache);
    }

    string SupplyObjective() => ChapterOneText.Format("quest.s03.stockpile", "Сложи {required} брёвен у костра.", ("required", GameBalance.SupplyLogs));

    ObjectiveData SupplyData()
    {
        FindRefs();
        int need = GameBalance.SupplyLogs;
        bool carrying = inv != null && inv.woodCount > 0;
        SupplyArrow(carrying);
        var data = new ObjectiveData(SupplyObjective());
        data.Req(UiIcon.Log, GameLanguage.T("Бревна"), need, () => SupplyDelivered,
            () => ChapterOneText.Pick("в рюкзаке ", "in pack ") + (inv != null ? inv.woodCount : 0));
        data.footer = carrying ? ChapterOneText.Text("tutorial.stockpile.deposit", "Сложи брёвна у костра.") :
            ChapterOneText.Pick("Наруби дров.", "Chop some wood.");
        return data;
    }

    void EnsureWindow()
    {
        if (upgradeWindow == null) upgradeWindow = FindFirstObjectByType<UpgradeWindow>();
        if (upgradeWindow == null) upgradeWindow = gameObject.AddComponent<UpgradeWindow>();
    }

    // ------------------------------------------------------------ ожидание уровня
    // Показываем, сколько опыта не хватает, ведём к дереву. Опыт добирается рубкой, а рюкзак при этом наполняется:
    // если он полон, а сдавать брёвна некуда, добрать опыт нечем. Тогда просим сложить брёвна у костра (глава «Прокачка»).
    void WaitForLevel(Chapter ch)
    {
        FindRefs();
        if (pack != null && pack.IsFull && market != null && market.IsFinished)
        {
            Objective(ChapterOneText.Pick("Рюкзак полон. Продай лишнее у прилавка.", "Your pack is full. Sell spare wood at the stall."));
            Arrow(market.transform);
            return;
        }

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
        if (GameLanguage.English)
            return $"Level {lvl} · XP <color=#6BCB5A>{Mathf.Min(have, need)}/{need}</color> · keep chopping";
        return "До " + lvl + "-го уровня: опыт <color=#6BCB5A>" + Mathf.Min(have, need) + "/" + need + "</color>. Руби деревья, ещё "
               + trees + " " + UiDraw.Plural(trees, "дерево", "дерева", "деревьев");
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
        if (rico != null) return;
        if (tradeAtBuilding) { SetupMarketTrading(); return; }
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
        if (rico != null) return;
        if (tradeAtBuilding) { SetupMarketTrading(); return; }
        if (merchant == null && !merchantWarned) SetupMerchant();
        if (merchant != null && !merchant.activeSelf) merchant.SetActive(true);
    }

    void SetupMarketTrading()
    {
        if (marketTrading == null && market != null) marketTrading = market.GetComponentInChildren<TradingStall>(true);
        if (marketTrading != null && !marketTrading.enabled) marketTrading.enabled = true;
        // Never hide a parent of the market itself. The builder handles component-only migrations there.
        if (merchant != null && merchant.activeSelf && (market == null || (merchant != market.gameObject && !market.transform.IsChildOf(merchant.transform))))
        {
            var oldSpeaker = merchant.GetComponent<Speaker>();
            if (oldSpeaker == null) oldSpeaker = merchant.GetComponentInChildren<Speaker>(true);
            if (oldSpeaker != null && string.Equals(oldSpeaker.displayName, merchantName, StringComparison.OrdinalIgnoreCase)) merchant.SetActive(false);
        }
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
        seq.isQuestReminder = true;
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
        d.phase = phase == Phase.Do ? 1 : 0;
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

        if (PreparingChapterPortal) { finaleAttempt++; PreparingChapterPortal = false; DialogueManager.RequestSkipAll(); }
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
        EndSupply();
        ChapterOneVoice.Reset();
        introDone = false;
        if (upgradeWindow != null) upgradeWindow.ResetStoryAccess();
        AllDone = false;
        PetsOpen = false;
        firstSale = noteRead = noteStarted = false;
        finaleAttempt++;
        PreparingChapterPortal = false;
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

        EndSupply();
        ChapterOneVoice.Reset();
        int idx = -1;
        if (!string.IsNullOrEmpty(d.chapterId))
            for (int i = 0; i < chapters.Count; i++)
                if (chapters[i].id == d.chapterId) { idx = i; break; }
        if (idx < 0) idx = Mathf.Clamp(d.chapterIndex, 0, chapters.Count);
        if (d.allDone) idx = chapters.Count;

        ci = idx;
        phase = (d.phase >= 1 && ci < chapters.Count) ? Phase.Do : Phase.Enter;
        talk = null;
        idleSeq = null;
        endingAt = -1f;
        firstSale = d.firstSale;
        noteRead = d.noteRead;
        noteStarted = false;
        finaleAttempt++;
        PreparingChapterPortal = false;
        AllDone = d.allDone;
        restoreSupply = d.supplyDelivered;

        // ВОЗМОЖНА ОШИБКА КОМПИЛЯТОРА: если totalCollected у PlankWorkshop не public int (например, только get), закомментируй строку
        if (workshop != null && d.planksCollected > workshop.totalCollected) workshop.totalCollected = d.planksCollected;

        // глава идёт, значит вступление пройдено (старый файл мог не успеть записать секцию intro)
        introDone = ci > 0 || AllDone;
        if (introDone && IntroFlow.Instance != null) IntroFlow.Instance.RestoreDoneFromStory();

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
    void RestoreSupply() => PrepareSupply(restoreSupply);

    // Load: окно прокачки после закрытой главы «Прокачка» (всё доступно) и в её процессе (только рюкзак). Монеты-страховку НЕ добавляем
    void RestoreUpgradeDone()
    {
        EnsureWindow();
        if (upgradeWindow == null) return;
        upgradeWindow.Unlock();
        upgradeWindow.SetOnly(-1);
        upgradeWindow.SetHighlight(-1);
    }

    // ------------------------------------------------------------ утилиты
    int Level()
    {
        if (stats == null) stats = FindFirstObjectByType<PlayerStats>();
        return stats != null ? stats.currentLevel : 1;
    }

    static void Objective(string text)
    {
        if (UIManager.Instance != null) UIManager.Instance.UpdateObjective(GameLanguage.T(text));
    }

    static void Arrow(Transform t) { QuestEdgeArrow.Target = t; }
}

