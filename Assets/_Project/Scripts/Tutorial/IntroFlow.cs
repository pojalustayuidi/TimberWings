// Assets/_Project/Scripts/Tutorial/IntroFlow.cs (НЕ в Editor)
// Первые минуты игры, по шагам:
//   Move      Лео сказал реплику, внизу экрана клавиши W A S D, цель «иди WASD». Шаг кончается, когда прошёл moveDistance м.
//   GoToMira  цель «Подойди к Мире», стрелка у края экрана ведёт к ней.
//   Talk      рядом с Мирой над ней «Нажми E, чтобы поговорить» (InteractionHub). Ушёл, шаг откатывается.
//   Chop      после разговора цель «Нарубить деревьев: 0/4», стрелка ведёт к ближайшему дереву.
//   Campfire  цель «Сдай дрова на костёр: N/M», стрелка к CAMPFIRE_Point. Завершает CompleteCampfire() (зовёт BuildingController, когда костёр достроен).
//   ReturnToMira  цель «Вернись к Мире», стрелка к ней.
//   Talk2     «Нажми E», второй разговор (Мира хвалит, Лео отвечает, сюжет идёт дальше).
//   Done      цель скрыта, вызывается onFinished.
// Мира закрыта (нельзя поговорить, нет «!»), пока игрок не прошёл Move и GoToMira: пропустить WASD через разговор нельзя.
// Завершение костра не зависит от флага completesTutorial в сцене: зона костра ищется по CAMPFIRE_Point и флаг включается сам.
// Реплики Миры и ответы Лео пишутся прямо в этом скрипте (scriptedDialogue), в Inspector ничего заполнять не надо.
//
// Ничего настраивать не надо: Мира ищется по имени (Speaker «Мира»), костёр по объекту CAMPFIRE_Point.
// Меню Tools > BuilderSim > Настроить старт игры создаёт этот объект на сцене.
// Инструкции живут в панели цели (UIManager.UpdateObjective), реплики дают сюжет и эмоции.
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[DefaultExecutionOrder(-20)]
public class IntroFlow : MonoBehaviour, ISaveable
{
    public enum Step { Move = 0, GoToMira = 1, Talk = 2, Chop = 3, Campfire = 4, Done = 5, ReturnToMira = 6, Talk2 = 7 }

    [System.Serializable] public class IntEvent : UnityEvent<int> { }

    public static IntroFlow Instance { get; private set; }

    [Header("Ссылки (пусто = найдутся сами)")]
    public DialogueSequence miraTalk;
    public Transform campfirePoint;

    [Header("Шаги")]
    public Step startStep = Step.Move;
    [Tooltip("Через сколько секунд после старта игры появляется первая цель. Больше 0.8 с не работает: цель должна появляться сразу")]
    public float firstDelay = 0.6f;
    const float MaxFirstDelay = 0.8f;
    public float moveDistance = 3f;
    [Tooltip("Больше не используется: цель теперь в брёвнах и не больше вместимости рюкзака")]
    public int treesToChop = GameBalance.CampfireTrees;

    [Header("Тексты цели (можно <b> и <color>)")]
    public string moveText = "Осмотрись: иди с помощью <color=#6BCB5A>WASD</color>";
    public string goText = "Подойди к Мире";
    public string talkText = "Поговори с Мирой: <color=#6BCB5A>E</color>";
    public string chopLogsText = "Нарубить дров: {0}/{1}";
    public string chopMoreText = "Нужно ещё дров: {0}/{1}";
    [Tooltip("true = цель «нарубить дров» показывается виджетом (иконка бревна, 7/10, прогрессбар). false = прежняя строка")]
    public bool useObjectiveWidget = true;
    public string chopTitle = "Нарубить дров";
    public string chopMoreTitle = "Нужно ещё дров";
    public string logsName = "Бревна";
    public string campfireProgressTitle = "Сдай дрова на костёр";
    public string campfireText = "Отнеси дрова на каменный круг и разожги костёр";
    public string campfireProgressText = "Сдай дрова на костёр: {0}/{1}";
    public string returnText = "Вернись к Мире: костёр горит";

    [Header("Диалоги")]
    [Tooltip("true = реплики Миры и Лео берутся из кода ниже (цикл: взять задание, построить костёр, вернуться за наградой). false = из Inspector")]
    public bool scriptedDialogue = true;
    public string playerName = "Лео";

    [Header("События")]
    public IntEvent onStepChanged;
    public UnityEvent onFinished;

    public Step Current { get { return step; } }

    Step step = (Step)(-1);
    bool started;
    float beginAt, treeScanAt;
    Transform player;
    Vector3 startPos;
        readonly KeyHints keys = new KeyHints();
    BuildingController campfireBuild;
    float campScanAt;
    int lastCampKey = -1;
    bool hasRestore, restoring, miraSilenced, campfireDone;   // сохранение: шаг, который надо поставить при Begin, и флаги
    Step restoreStep = Step.Move;

    // ------------------------------------------------------------ жизненный цикл
    void Awake()
    {
        Instance = this;
        beginAt = Time.unscaledTime + Mathf.Clamp(firstDelay, 0f, MaxFirstDelay);
        SaveSystem.Register(this);   // если Load() уже был, секция придёт сюда, а применится при Begin
    }

    // Мира настраивается сразу, а не через 2 секунды: иначе, пока ждали первую цель, она могла заговорить сама, как только игрок подошёл
    void Start()
    {
        Resolve();
        EnsureStoryFlow();
    }

    // Без StoryFlow после вступления нет заданий (8 брёвен Мире, прокачка, хижина...). Если его нет на сцене
    // (не запускали меню настройки или у объекта слетел скрипт), создаём сами, чтобы цепочка не обрывалась.
    void EnsureStoryFlow()
    {
        if (FindFirstObjectByType<StoryFlow>() != null) return;
        Debug.LogWarning("IntroFlow: на сцене нет рабочего StoryFlow, создал его сам. Запусти Tools > BuilderSim > Настроить цепочку StoryFlow " +
                         "и сохрани сцену (если у старого объекта StoryFlow слетел скрипт, удали его).");
        new GameObject("StoryFlow").AddComponent<StoryFlow>();
    }

    void OnEnable()
    {
        GameEvents.TreeChopped += OnTree;
        GameEvents.DialogueFinished += OnDialogue;
    }

    void OnDisable()
    {
        GameEvents.TreeChopped -= OnTree;
        GameEvents.DialogueFinished -= OnDialogue;
        GateOff();   // статический флаг не должен пережить обучение
    }

    void OnDestroy()
    {
        if (Instance == this) { SaveSystem.Unregister(this); Instance = null; }
        QuestEdgeArrow.Target = null;
    }

    void Update()
    {
        keys.Tick(Time.unscaledDeltaTime);

        // «!» над закрытой Мирой не показываем (её собственный Start мог включить кружок обратно)
        if (!miraOpen && miraTalk != null && miraTalk.marker != null) miraTalk.marker.SetVisible(false);

        if (!started)
        {
            if (Time.unscaledTime >= beginAt) Begin();
            return;
        }
        if (step == Step.Done) return;
        if (player == null && !FindPlayer()) return;

        switch (step)
        {
            case Step.Move:
                if (Flat(player.position - startPos) >= moveDistance) SetStep(miraTalk != null ? Step.GoToMira : Step.Chop);
                break;

            case Step.GoToMira:
                if (Flat(player.position - miraTalk.transform.position) <= miraTalk.radius) SetStep(Step.Talk);
                break;

            case Step.Talk:
                // отошёл, не дослушав: возвращаем шаг «подойди»
                if (!miraTalk.IsPlaying && Flat(player.position - miraTalk.transform.position) > miraTalk.radius * 1.6f)
                    SetStep(Step.GoToMira);
                break;

            case Step.Chop:
                // костёр уже достроен (сигнал мог не дойти): не ждём дрова, идём к Мире
                if (CampfireBuilt()) { SetStep(Step.ReturnToMira); break; }
                if (Time.unscaledTime >= treeScanAt)
                {
                    treeScanAt = Time.unscaledTime + 1f;
                    QuestEdgeArrow.Target = NearestTree();
                }
                UpdateChopObjective();
                break;

            case Step.Campfire:
                if (Time.unscaledTime >= campScanAt)
                {
                    campScanAt = Time.unscaledTime + 0.25f;
                    if (CampfireBuilt()) { SetStep(Step.ReturnToMira); break; }
                    UpdateCampfireObjective();
                }
                break;

            case Step.ReturnToMira:
                if (Flat(player.position - miraTalk.transform.position) <= miraTalk.radius) SetStep(Step.Talk2);
                break;

            case Step.Talk2:
                if (!miraTalk.IsPlaying && Flat(player.position - miraTalk.transform.position) > miraTalk.radius * 1.6f)
                    SetStep(Step.ReturnToMira);
                break;
        }
    }

    // ------------------------------------------------------------ запуск
    void Begin()
    {
        started = true;
        FindPlayer();
        Resolve();
        keys.Build(transform, UIManager.Instance != null && UIManager.Instance.objectiveText != null
            ? UIManager.Instance.objectiveText.font : null);
        if (hasRestore) ApplyRestore();   // загрузка: сразу на сохранённый шаг
        else SetStep(startStep);
    }

    bool FindPlayer()
    {
        var pm = PlayerMovement.Instance;
        if (pm == null) return false;
        player = pm.transform;
        return true;
    }

    bool resolved;

    void Resolve()
    {
        if (resolved) return;
        if (miraTalk == null)
        {
            var sp = Speaker.Find("Мира");
            if (sp != null) miraTalk = sp.GetComponent<DialogueSequence>();
            if (miraTalk == null)
                Debug.LogWarning("IntroFlow: не найдена Мира (Speaker «Мира» с DialogueSequence). Шаги с разговором пропущены.");
        }
        // шаг Talk: «Нажми E, чтобы поговорить», а не автозапуск при подходе
        if (miraTalk != null)
        {
            miraTalk.once = true;
            if (scriptedDialogue) miraTalk.SetLines(TaskLines(), true);
            // до шага «Подойди к Мире» разговор закрыт, потом откроется сам (SetStep)
            SetMiraOpen(!(startStep == Step.Move || startStep == Step.GoToMira));
        }
        // стрелка у края экрана создаётся сама
        if (FindFirstObjectByType<QuestEdgeArrow>() == null) gameObject.AddComponent<QuestEdgeArrow>();

        if (campfirePoint == null)
        {
            var go = GameObject.Find("CAMPFIRE_Point");
            if (go != null) campfirePoint = go.transform;
        }
        resolved = miraTalk != null;   // Миры нет: попробуем ещё раз при старте шагов
        // после загрузки (обучение уже пройдено дальше) закрывать рубку и стройки не нужно
        if (resolved && !hasRestore && (startStep == Step.Move || startStep == Step.GoToMira || startStep == Step.Talk)) GateOn();
    }

    // ------------------------------------------------------------ Мира открыта для разговора или нет
    bool miraOpen = true;

    void SetMiraOpen(bool open)
    {
        if (miraSilenced) open = false;   // после загрузки Мира первый разговор уже отговорила: «!» и повтор не нужны
        miraOpen = open;
        if (miraTalk == null) return;
        // Manual = Мира не регистрируется в InteractionHub и сама не заговорит
        miraTalk.SetTrigger(open ? DialogueSequence.Trigger.OnInteract : DialogueSequence.Trigger.Manual);
        if (miraTalk.marker != null) miraTalk.marker.SetVisible(open && !miraTalk.Played);
    }

    bool CampfireBuilt()
    {
        ResolveCampfire();
        return campfireBuild != null && campfireBuild.IsFinished;
    }

    // ------------------------------------------------------------ «сначала поговори с Мирой»
    // До первого разговора нельзя рубить и строить. Закрываем рубку и все открытые стройки,
    // после разговора открываем ровно то, что закрыли сами (хижину, которую закрыл другой код, не трогаем).
    readonly List<BuildingController> gated = new List<BuildingController>();
    bool gateOn;

    void GateOn()
    {
        if (gateOn) return;
        gateOn = true;
        PlayerInteraction.ChoppingEnabled = false;

        ResolveCampfire();
        foreach (var b in FindObjectsByType<BuildingController>(FindObjectsSortMode.None))
        {
            if (b == null || b.locked || b.IsFinished || b.storyManaged) continue;   // стройки StoryFlow (хижина, рынок, палатка) закрыты и без нас
            b.SetLocked(true);
            gated.Add(b);
            if (b == campfireBuild)
            {
                var view = b.GetComponent<BuildSiteTagView>();
                if (view != null) view.lockedAfter = "разговор с Мирой";
            }
        }
    }

    void GateOff()
    {
        if (!gateOn) return;
        gateOn = false;
        PlayerInteraction.ChoppingEnabled = true;
        foreach (var b in gated) if (b != null) b.SetLocked(false);
        gated.Clear();
    }

    // ------------------------------------------------------------ шаги
    public void SetStep(Step s)
    {
        // если Мира не найдена, разговорные шаги пропускаем
        if ((s == Step.GoToMira || s == Step.Talk) && miraTalk == null) s = Step.Chop;
        if ((s == Step.ReturnToMira || s == Step.Talk2) && miraTalk == null) s = Step.Done;

        step = s;
        if (s == Step.ReturnToMira || s == Step.Talk2) miraSilenced = false;
        if (s == Step.ReturnToMira || s == Step.Talk2 || s == Step.Done) campfireDone = true;
        if (s == Step.Move || s == Step.GoToMira || s == Step.Talk) GateOn(); else GateOff();
        SetMiraOpen(!(s == Step.Move || s == Step.GoToMira));
        keys.Show(s == Step.Move);
        if (player != null && s == Step.Move) startPos = player.position;

        switch (s)
        {
            case Step.Move:
                Objective(moveText);
                QuestEdgeArrow.Target = null;
                break;
            case Step.GoToMira:
                Objective(goText);
                QuestEdgeArrow.Target = miraTalk.transform;
                break;
            case Step.Talk:
                Objective(talkText);
                QuestEdgeArrow.Target = miraTalk.transform;
                break;
            case Step.Chop:
                chopTarget = ChopGoal();
                lastChopKey = -1;
                UpdateChopObjective();
                QuestEdgeArrow.Target = player != null ? NearestTree() : null;
                treeScanAt = Time.unscaledTime + 1f;
                break;
            case Step.Campfire:
                Objective(campfireText);
                QuestEdgeArrow.Target = campfirePoint;
                lastCampKey = -1;
                campScanAt = 0f;
                break;
            case Step.ReturnToMira:
                Objective(returnText);
                QuestEdgeArrow.Target = miraTalk.transform;
                // Мира снова доступна по E, но теперь говорит про костёр
                if (scriptedDialogue) miraTalk.SetLines(RewardLines(), true); else miraTalk.Rearm();
                break;
            case Step.Talk2:
                Objective(talkText);
                QuestEdgeArrow.Target = miraTalk.transform;
                break;
            case Step.Done:
                Objective("");
                QuestEdgeArrow.Target = null;
                if (!restoring)   // при загрузке праздника и onFinished не повторяем
                {
                    GameEvents.RaiseQuestDone("intro");   // праздник: костёр горит
                    if (onFinished != null) onFinished.Invoke();
                }
                break;
        }

        if (onStepChanged != null) onStepChanged.Invoke((int)s);

        // шаги до первого разговора не пишем: вся игра с нуля и так стартует с них
        if (!restoring && s != Step.Move && s != Step.GoToMira && s != Step.Talk) SaveSystem.RequestSave("intro");
    }

    // костёр построен: вызови из своего скрипта стройки (или повесь на UnityEvent)
    public void CompleteCampfire() { CompleteCampfire(null); }

    // src = стройка, которая закончилась. Чужие зоны сдачи (заглушки без костра) обучение не завершают
    public void CompleteCampfire(BuildingController src)
    {
        if (src != null)
        {
            ResolveCampfire();
            if (campfireBuild != null && src != campfireBuild) return;
        }
        if (step == Step.Campfire || step == Step.Chop) SetStep(Step.ReturnToMira);   // без Миры ReturnToMira сам станет Done
    }

    public void Skip() { SetStep(Step.Done); }

    // ------------------------------------------------------------ события игры
    void OnDialogue(DialogueSequence seq)
    {
        if (!started || seq == null || seq != miraTalk) return;
        // первый разговор засчитывается только на шаге Talk: сюда игрок попадает, лишь пройдя WASD и дойдя до Миры
        if (step == Step.Talk) { chopTrips = 0; SetStep(Step.Chop); }
        else if (step == Step.ReturnToMira || step == Step.Talk2) SetStep(Step.Done);
    }

    void OnTree()
    {
        if (step != Step.Chop) return;
        QuestEdgeArrow.Target = null;                 // следующее дерево выберется при ближайшем скане
        treeScanAt = Time.unscaledTime + 0.4f;
    }

    // ------------------------------------------------------------ цель «дрова» (в брёвнах, а не в деревьях)
    // Рюкзак на старте вмещает 4 бревна, а дерево даёт 2. Цель «4 дерева» была недостижима: на втором дереве рюкзак полон.
    // Теперь цель = сколько бревен нужно костру, но не больше вместимости. Не хватило за раз, идём второй рейс.
    int chopTarget = 4, lastChopKey = -1, chopTrips;
    PlayerInventory inv;
    BackpackTag pack;

    bool FindInventory()
    {
        if (inv == null) inv = PlayerInventory.Instance;
        if (pack == null) pack = FindFirstObjectByType<BackpackTag>();
        return inv != null;
    }

    int ChopGoal()
    {
        FindInventory();
        ResolveCampfire();
        int cap = pack != null ? pack.Capacity : GameBalance.StartBackpack;
        int need = campfireBuild != null && !campfireBuild.IsFinished ? campfireBuild.Cost : 0;
        if (need <= 0) need = cap;                    // цена костра неизвестна: просто полный рюкзак
        return Mathf.Max(1, Mathf.Min(cap, need));
    }

    void UpdateChopObjective()
    {
        if (!FindInventory()) return;
        int carried = inv.woodCount;
        bool full = pack != null && pack.IsFull;

        // первый заход: под целью короткая инструкция про бонус (Пробел в зелёной зоне), пока игрок его не освоил
        bool teach = chopTrips == 0 && PlayerInteraction.BonusTeaching;
        int key = Mathf.Min(carried, chopTarget) * 100 + chopTarget + (teach ? 100000 : 0);
        if (key != lastChopKey)
        {
            lastChopKey = key;
            const string TeachHint = "Зажми <color=#6BCB5A>E</color>, а когда маркер в зелёной зоне, жми <color=#F2A93B>ПРОБЕЛ</color>: дерево упадёт быстрее";
            if (useObjectiveWidget && UIManager.Instance != null)
            {
                // живой счётчик: число берётся из PlayerInventory каждый кадр, цель пересобирается только при смене формы
                int target = chopTarget;
                var d = new ObjectiveData(chopTrips > 0 ? chopMoreTitle : chopTitle);
                d.Req(UiIcon.Log, logsName, target, () => inv != null ? inv.woodCount : 0);
                if (teach) d.footer = TeachHint;   // размер задаёт виджет (20, не меньше 18)
                UIManager.Instance.SetObjective(d);
            }
            else
            {
                string fmt = chopTrips > 0 ? chopMoreText : chopLogsText;
                string msg = string.Format(fmt, Mathf.Min(carried, chopTarget), chopTarget);
                if (teach) msg += "\n<size=70%>" + TeachHint + "</size>";
                Objective(msg);
            }
        }

        if (carried >= chopTarget || full) SetStep(Step.Campfire);
    }

    // ------------------------------------------------------------ сюжетные разговоры
    // Реплика с speaker "" говорит сама Мира (её Speaker), остальные берут имя героя.
    DialogueSequence.Line L(bool hero, string text)
    {
        return new DialogueSequence.Line { speaker = hero ? playerName : "", text = text, pauseAfter = 0.3f };
    }

    // Разговор 1: знакомство и задание «построить костёр» (каждая реплика до 120 символов)
    DialogueSequence.Line[] TaskLines()
    {
        return new[]
        {
            L(false, "О, новенький! Ты ведь племянник Гуннара? Лео, да?"),
            L(true,  "Да. Он писал, что нужна помощь. Где он?"),
            L(false, "Пропал три дня назад. А мост к его мастерской шторм разнёс в щепки."),
            L(true,  "Значит, будем строить новый. С чего начать?"),
            L(false, "С огня. Нарубай дров и разожги костёр на каменном круге. Там сердце лагеря."),
            L(true,  "Дрова так дрова. Топор при мне, берусь."),
            L(false, "Вот это разговор! Принесёшь дров, поговорим дальше.")
        };
    }

    // Разговор 2: костёр достроен (обе стадии). Мира даёт следующее задание (8 брёвен, глава «Припасы» в StoryFlow)
    DialogueSequence.Line[] RewardLines()
    {
        return new[]
        {
            L(false, "Горит! Впервые за три дня в лагере тепло."),
            L(true,  "Дядя бы гордился... Что дальше?"),
            L(false, "Теперь про запас. Принеси мне восемь брёвен, пригодятся на холода."),
            L(true,  "Восемь? Рюкзак вмещает только четыре."),
            L(false, "Значит, два рейса. За труд подкину опыта на новый уровень, а там покажу прокачку."),
            L(true,  "Договорились. Две ходки.")
        };
    }

    // ------------------------------------------------------------ прогресс костра в панели цели
    void UpdateCampfireObjective()
    {
        ResolveCampfire();
        if (campfireBuild == null) return;
        UpdateCampfireObjectiveCore();
    }

    float resolveAt;
    static readonly string[] CampKeys = { "campfire", "костёр", "костер" };

    void ResolveCampfire()
    {
        if (campfireBuild != null) return;
        if (Time.unscaledTime < resolveAt) return;   // не чаще раза в секунду: поиск по сцене не бесплатный
        resolveAt = Time.unscaledTime + 1f;

        if (campfirePoint == null)
        {
            var go = GameObject.Find("CAMPFIRE_Point");
            if (go != null) campfirePoint = go.transform;
        }
        campfireBuild = FindCampfireBuild(campfirePoint);
        if (campfireBuild == null) return;

        // зона, созданная через «Создать зону сдачи», приходит с completesTutorial = false: костёр достроился бы, а обучение не узнало
        if (!campfireBuild.completesTutorial)
        {
            Debug.LogWarning("IntroFlow: у зоны костра «" + campfireBuild.name + "» был выключен completesTutorial, включил сам. " +
                             "Чтобы убрать предупреждение, запусти Tools > BuilderSim > Проверить подключение (Setup Doctor) и сохрани сцену.", campfireBuild);
            campfireBuild.completesTutorial = true;
        }
    }

    // Зона костра: в пути есть «campfire»/«костёр», рядом с CAMPFIRE_Point. Хижину, рынок и палатку не берём никогда.
    // public static: этим же пользуется Setup Doctor
    public static BuildingController FindCampfireBuild(Transform point)
    {
        var all = FindObjectsByType<BuildingController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        BuildingController best = null;
        int bestRank = -1;
        float bestD = float.MaxValue;

        foreach (var b in all)
        {
            if (b == null) continue;
            string path = StoryFlow.PathOf(b);
            if (StoryFlow.MatchesSite(StoryFlow.Site.Hut, path) || StoryFlow.MatchesSite(StoryFlow.Site.Market, path) ||
                StoryFlow.MatchesSite(StoryFlow.Site.Tent, path)) continue;

            int rank = StoryFlow.HasAny(path, CampKeys) ? 2 : (b.completesTutorial ? 1 : 0);
            rank = rank * 100 + StoryFlow.SiteScore(b);   // среди дублей рабочая зона (триггер, цена) выигрывает у пустой
            float d = point != null ? Flat(b.transform.position - point.position) : 0f;
            if (rank > bestRank || (rank == bestRank && d < bestD)) { best = b; bestRank = rank; bestD = d; }
        }

        // зона без единого признака костра: берём только если стоит почти на самой точке
        if (best != null && bestRank < 100 && (point == null || bestD > 15f)) return null;
        return best;
    }

    void UpdateCampfireObjectiveCore()
    {
        if (campfireBuild.IsFinished) return;

        // рюкзак пуст, а костру ещё не хватает: идём рубить вторую партию
        if (FindInventory() && inv.woodCount == 0 && campfireBuild.Cost > 0 && !campfireBuild.IsBuilding)
        {
            chopTrips++;
            SetStep(Step.Chop);
            return;
        }

        int key = campfireBuild.Deposited * 1000 + campfireBuild.CostTotal;
        if (key == lastCampKey) return;
        lastCampKey = key;
        if (useObjectiveWidget && UIManager.Instance != null)
        {
            var b = campfireBuild;
            int total = b.CostTotal;
            var d = new ObjectiveData(campfireProgressTitle);
            d.Req(UiIcon.Log, logsName, total, () => b.Deposited,
                  () => b.Deposited >= total ? null : "в рюкзаке " + (inv != null ? inv.woodCount : 0) + " · сдано " + b.Deposited + "/" + total);
            UIManager.Instance.SetObjective(d);
        }
        else Objective(string.Format(campfireProgressText, campfireBuild.Deposited, campfireBuild.CostTotal));
    }

    // ------------------------------------------------------------ сохранение (ISaveable, ключ «intro»)
    public string SaveKey { get { return "intro"; } }
    public int LoadOrder { get { return 80; } }

    // ДОБАВЛЯТЬ ПОЛЯ ТОЛЬКО С ДЕФОЛТАМИ: старые файлы без них читаются нормально
    [System.Serializable]
    class SaveData
    {
        public int version = 1;
        public int step = -1;             // шаг вступления (-1 = ещё не начиналось)
        public bool campfireDone;         // CompleteCampfire уже сработал (костёр достроен)
    }

    public string Save()
    {
        var d = new SaveData { campfireDone = campfireDone };
        if (hasRestore && !started) d.step = (int)restoreStep;   // ещё не применили: не затираем
        else d.step = started ? (int)step : -1;
        return JsonUtility.ToJson(d);
    }

    public void Load(string json, int formatVersion)
    {
        SaveData d = null;
        if (!string.IsNullOrEmpty(json))
        {
            try { d = JsonUtility.FromJson<SaveData>(json); }
            catch (System.Exception e) { Debug.LogWarning("IntroFlow: не удалось прочитать сохранение вступления: " + e.Message); }
        }
        if (d == null) { ResetToDefault(); return; }

        campfireDone = d.campfireDone;
        Step s = (Step)d.step;
        bool valid = d.step >= (int)Step.Move && d.step <= (int)Step.Talk2;

        // до первого разговора с Мирой вступление просто начинается заново (это секунды), дальше возвращаем на сохранённый шаг
        if (!valid || s == Step.Move || s == Step.GoToMira || s == Step.Talk)
        {
            hasRestore = false;
            if (started) ResetToDefault();
            return;
        }

        if (s == Step.Talk2) s = Step.ReturnToMira;
        if ((s == Step.Chop || s == Step.Campfire) && campfireDone) s = Step.ReturnToMira;
        restoreStep = s;
        hasRestore = true;
        if (started) ApplyRestore();
    }

    public void ResetToDefault()
    {
        hasRestore = false;
        campfireDone = false;
        miraSilenced = false;
        restoreStep = Step.Move;
        if (!started) return;

        // идёт игра: вступление начинается сначала (Begin заново настроит Миру и закроет рубку)
        started = false;
        resolved = false;
        step = (Step)(-1);
        beginAt = Time.unscaledTime + 0.1f;
    }

    // Ставит вступление на сохранённый шаг: без праздника, onFinished и повторной болтовни Миры
    void ApplyRestore()
    {
        hasRestore = false;
        restoring = true;
        Resolve();
        if (player == null) FindPlayer();

        Step s = restoreStep;
        if (s == Step.Chop || s == Step.Campfire) miraSilenced = true;
        SetStep(s);

        // Мира всё сказала: задание Припасов ведёт StoryFlow (и сам включит ей разговор), SupplyPoint работает отдельно
        if (step == Step.Done && miraTalk != null)
        {
            miraTalk.SetTrigger(DialogueSequence.Trigger.Manual);
            if (miraTalk.marker != null) miraTalk.marker.SetVisible(false);
            miraOpen = true;
        }
        restoring = false;
    }

    // StoryFlow нашёл в своём сохранении главу дальше вступления, а секция intro пропала (старый файл): считаем вступление пройденным
    public void RestoreDoneFromStory()
    {
        if (started)
        {
            if (step == Step.Done) return;
            restoreStep = Step.Done;
            ApplyRestore();
            return;
        }
        hasRestore = true;
        restoreStep = Step.Done;
    }

    // ------------------------------------------------------------ утилиты
    static float Flat(Vector3 d)
    {
        d.y = 0f;
        return d.magnitude;
    }

    void Objective(string text)
    {
        if (UIManager.Instance != null) UIManager.Instance.UpdateObjective(text);
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
            float s = d.sqrMagnitude;
            if (s < bestSqr) { bestSqr = s; best = r.transform; }
        }
        return best;
    }

    // ================================================================ клавиши WASD на экране
    // Четыре «клавиши» внизу экрана: нажатая загорается зелёным, остальные тихо пульсируют.
    sealed class KeyHints
    {
        static readonly KeyCode[] Keys = { KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D };
        static readonly string[] Names = { "W", "A", "S", "D" };
        static readonly Vector2[] Pos =
        {
            new Vector2(0f, 48f), new Vector2(-92f, -44f), new Vector2(0f, -44f), new Vector2(92f, -44f)
        };

        static readonly Color Dark = new Color(0.06f, 0.09f, 0.17f, 0.92f);
        static readonly Color Green = new Color(0.42f, 0.80f, 0.35f, 1f);
        static readonly Color Ring = new Color(0.62f, 0.72f, 0.95f, 1f);

        CanvasGroup group;
        readonly RectTransform[] caps = new RectTransform[4];
        readonly Image[] back = new Image[4];
        readonly Image[] ring = new Image[4];
        readonly TMP_Text[] label = new TMP_Text[4];
        readonly float[] press = new float[4];
        float vis;
        bool want;

        public void Show(bool v) { want = v; }

        public void Build(Transform parent, TMP_FontAsset font)
        {
            if (group != null) return;

            var cgo = new GameObject("IntroKeysCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            cgo.layer = 5;
            cgo.transform.SetParent(parent, false);
            var canvas = cgo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var sc = cgo.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            sc.matchWidthOrHeight = 0.5f;
            group = cgo.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            var panelGo = new GameObject("Panel", typeof(RectTransform));
            panelGo.layer = 5;
            panelGo.transform.SetParent(cgo.transform, false);
            var panel = (RectTransform)panelGo.transform;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.2f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(320f, 200f);

            for (int i = 0; i < 4; i++)
            {
                var kg = new GameObject("Key_" + Names[i], typeof(RectTransform), typeof(Image));
                kg.layer = 5;
                kg.transform.SetParent(panel, false);
                var rt = (RectTransform)kg.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Pos[i];
                rt.sizeDelta = new Vector2(80f, 80f);
                caps[i] = rt;

                back[i] = kg.GetComponent<Image>();
                back[i].sprite = UiKit.Keycap;                       // клавиша UiKit: латунный медальон с янтарной «кнопкой»
                back[i].type = Image.Type.Simple;
                back[i].color = Color.white;
                back[i].raycastTarget = false;

                var rg = new GameObject("Ring", typeof(RectTransform), typeof(Image));
                rg.layer = 5;
                rg.transform.SetParent(rt, false);
                var rr = (RectTransform)rg.transform;
                rr.anchorMin = Vector2.zero; rr.anchorMax = Vector2.one;
                rr.offsetMin = rr.offsetMax = Vector2.zero;
                ring[i] = rg.GetComponent<Image>();
                ring[i].sprite = UiDraw.PillRing;
                ring[i].type = Image.Type.Sliced;
                ring[i].color = Ring;
                ring[i].raycastTarget = false;
                ring[i].enabled = false;

                var tg = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                tg.layer = 5;
                tg.transform.SetParent(rt, false);
                var tr = (RectTransform)tg.transform;
                tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
                tr.offsetMin = tr.offsetMax = Vector2.zero;
                var tx = tg.GetComponent<TextMeshProUGUI>();
                if (font != null) tx.font = font;
                tx.text = Names[i];
                tx.fontSize = 34f;
                tx.fontStyle = FontStyles.Bold;
                tx.alignment = TextAlignmentOptions.Center;
                tx.color = UiKit.Navy;
                tx.raycastTarget = false;
                label[i] = tx;
            }

            cgo.SetActive(false);
        }

        public void Tick(float dt)
        {
            if (group == null) return;

            vis = Mathf.MoveTowards(vis, want ? 1f : 0f, dt / 0.3f);
            bool on = vis > 0.01f;
            if (group.gameObject.activeSelf != on) group.gameObject.SetActive(on);
            if (!on) return;
            group.alpha = vis;

            float t = Time.unscaledTime;
            for (int i = 0; i < 4; i++)
            {
                bool down = Input.GetKey(Keys[i]);
                press[i] = Mathf.MoveTowards(press[i], down ? 1f : 0f, dt * 10f);
                float k = press[i];

                back[i].color = Color.Lerp(Color.white, new Color(0.62f, 1f, 0.55f, 1f), k);      // нажатая загорается зелёным
                label[i].color = UiKit.Navy;
                float idle = 1f + 0.04f * Mathf.Sin(t * 3f + i * 0.9f);                           // остальные тихо «дышат»
                caps[i].localScale = Vector3.one * Mathf.Lerp(idle, 0.9f, k);

                float pulse = 0.65f + 0.3f * Mathf.Sin(t * 3f + i * 0.9f);
                var c = Ring;
                c.a = Mathf.Lerp(pulse, 1f, k);
                ring[i].color = c;
            }
        }
    }
}