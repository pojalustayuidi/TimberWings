// Assets/_Project/Scripts/Build/BuildingController.cs
// Зона сдачи брёвен. Невидимый триггер-блок: игрок заходит, если брёвен хватает, идёт таймер, брёвна списываются.
// Умеет несколько этапов подряд (костёр: 1 -> 2 -> 3).
//
// Цена, время и награда каждого этапа берутся из BuildSiteData (поле Data). Если Data пусто, работают «запасные»
// поля ниже. Метка, подсветка и ценник читают цену отсюда (Cost), поэтому показывают всегда то же, что и логика.
//
// Архитектура: контроллер ничего не знает про UI. Он только хранит состояние и отдаёт его наружу
// (IsBuilding, Cost, StageCurrent, Progress ...) и шлёт события.
//
// Показ стадии (reveal): после КАЖДОГО сданного этапа площадка блокируется на GameBalance.RevealTime (у последней стадии RevealTimeFinal):
// CanPlay, CanDeposit и DepositAll молчат, пока идёт показ. Так игрок с запасом ресурсов не проскакивает визуальные стадии.
// Таймер показа живёт здесь (а не в UI), поэтому блокировка работает, даже если камера или плашка сломались.
// Начало и конец показа: GameEvents.BuildRevealStarted / BuildRevealFinished. Картинку (камера, плашка) рисует Vibe/BuildReveal.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BuildingController : MonoBehaviour
{
    [Header("ДАННЫЕ СТРОЙКИ: единый источник цены и времени")]
    [Tooltip("Цена, время и награда каждого этапа. Если назначено, «Запасные настройки» ниже игнорируются. " +
             "Создать: ПКМ по компоненту → «Создать Build Site Data из текущих полей».")]
    public BuildSiteData data;

    [Header("Этапы (HutStages)")]
    public HutStages stages;                 // если пусто, ищется в родителях; каждая сдача переключает этап
    [Tooltip("НЕ ИСПОЛЬЗУЕТСЯ. Паузу между этапами теперь даёт показ стадии (GameBalance.RevealTime). Поле оставлено, чтобы не слетели сериализованные значения")]
    public float stepDelay = 0.8f;

    [Header("Визуальные стадии")]
    [Tooltip("Готовая модель для ОДНОЭТАПНЫХ построек. Если у стройки есть этапы (HutStages), вид даёт HutStages, " +
             "а этот объект остаётся скрытым (включи finishedWithStages, если он всё же нужен в конце).")]
    public GameObject finishedBuilding;
    [Tooltip("Показывать finishedBuilding в конце, даже если у стройки есть этапы")]
    public bool finishedWithStages = false;

    [Header("Запасные настройки (читаются, только если Data пусто)")]
    public int startStage = 1;               // с какого этапа начинается игра
    public int[] stageCosts = new int[0];    // цена каждого улучшения в брёвнах, например {4, 4}
    public int woodRequired = 4;             // для построек без этапов
    public float buildTime = 2.5f;           // время одного этапа, секунды
    public int rewardXp = 10;                // GDD: +10 опыта за этап

    [Header("Визуальные эффекты (VFX)")]
    public ParticleSystem buildingVFX;
    public ParticleSystem finishVFX;

    [Header("Старое кольцо над площадкой (если нет BuildSiteTagView)")]
    public bool useRing = true;
    public Vector3 ringOffset = new Vector3(0f, 2.2f, 0f);

    [Header("Старый прогресс-бар (необязательно)")]
    public GameObject progressCanvas;
    public Image progressFill;

    [Header("Экономика и Прокачка")]
    public int rewardCoins = 0;
    public PlayerStats playerStats;

    [Header("Связи (находятся сами, если пусто)")]
    public TutorialManager tutorial;
    public bool completesTutorial = true;
    [Tooltip("Больше не используется: обучение считает костёр готовым только после ПОСЛЕДНЕГО этапа (раньше сигнал уходил после первого, и Мира говорила «горит», пока костёр был недостроен)")]
    [Min(1)] public int tutorialStages = 1;

    [Header("Окно стройки")]
    [Tooltip("true = ресурсы сдаются и этап запускается в окне (E на площадке, кнопки «Сдать ресурсы» и «Играть»). " +
             "false = старое поведение: встал на площадку, ресурсы сдались сами, пошёл таймер.")]
    public bool useWindow = true;

    [Header("Доступность")]
    public bool locked = false;     // true = строить пока нельзя (метка показывает «Откроется после...»)
    [Tooltip("true = стройку открывает только сюжетная глава (StoryFlow). Вступление (IntroFlow) такую стройку не закрывает и не открывает")]
    public bool storyManaged = false;

    [Header("Диагностика")]
    [Tooltip("Если этапов в данных меньше, чем нужно до последнего этапа HutStages: достроить (с предупреждением)")]
    public bool autoFixCosts = true;
    [Tooltip("Если этапов в данных больше, чем стадий у HutStages (лишний, например «4-я стадия» костра), лишние отбрасываются")]
    public bool autoTrimSteps = true;
    [Tooltip("Писать в Console каждую сдачу этапа")]
    public bool debugLog = false;

    [Header("Редактор")]
    public bool drawZone = true;    // рисовать невидимую зону в Scene (в игре её не видно)

    // ---------------------------------------------------------------- события
    // Игрок сдал ресурс в стройку. kind: 0 бревно, 1 доска, 2 монеты; amount > 0. Слушает ObjectiveView (иконка «вылетает» к строке квеста)
    public static event Action<BuildingController, int, int> ResourceDeposited;
    void NotifyDeposit(int kind, int amount) { if (amount > 0 && ResourceDeposited != null) ResourceDeposited(this, kind, amount); }

    public event Action StageBuilt;   // сдан очередной этап (после каждого)
    public event Action Finished;     // достроено полностью
    public event Action<int> NotEnoughWood;   // игрок пришёл, но брёвен не хватает (аргумент: сколько ещё нужно)

    // ---------------------------------------------------------------- состояние для UI (только чтение)
    public bool IsBuilding { get { return isBuilding; } }
    public bool IsFinished { get { return isFinished; } }
    bool tutorialFired;
    // обучение уже получило сигнал «готово» от этой стройки (дальше идут необязательные улучшения)
    public bool TutorialFired { get { return tutorialFired; } }
    public bool IsLocked { get { return locked && !isFinished; } }

    // ---------------------------------------------------------------- реестр и резерв досок (для окна продажи)
    static readonly System.Collections.Generic.List<BuildingController> registry = new System.Collections.Generic.List<BuildingController>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetRegistry() { registry.Clear(); revealingCount = 0; }

    void OnEnable()
    {
        if (!registry.Contains(this)) registry.Add(this);
        BuildingSaveHub.Attach(this);   // общий сейф строек: подхватит сохранённое состояние этой площадки
    }

    // сохранённое состояние стройки ещё не применено (применяется в LateUpdate первого кадра, когда отработали все Start)
    public static bool AnyRestorePending
    {
        get
        {
            for (int i = 0; i < registry.Count; i++)
                if (registry[i] != null && registry[i].restorePending) return true;
            return false;
        }
    }

    // стройка по сохранённому ID (null, если её нет в сцене или она выключена)
    public static BuildingController FindById(string id)
    {
        for (int i = 0; i < registry.Count; i++)
            if (registry[i] != null && registry[i].saveId == id) return registry[i];
        return null;
    }

    // сколько досок ещё нужно всем открытым стройкам (ближайший этап, минус уже сданное).
    // Закрытые (locked) и достроенные не считаются. SellWindow не предлагает продать эти доски по умолчанию
    public static int PlanksWanted()
    {
        int sum = 0;
        for (int i = 0; i < registry.Count; i++)
        {
            var b = registry[i];
            if (b == null || b.isFinished || b.IsLocked) continue;
            sum += Mathf.Max(0, b.PlankTotal - b.depositedPlanks);
        }
        return sum;
    }
    public bool PlayerInside { get { return inside != null; } }

    // ---------------------------------------------------------------- для окна стройки (BuildWindow)
    public bool UseWindow { get { return useWindow; } }
    // этап запущен кнопкой «Играть» и идёт (таймер тикает, пока игрок на площадке)
    public bool StageRunning { get { return stageRunning; } }
    // награда в опыте за ближайший этап
    public int StepXp { get { EnsureData(); return xps[StepIndex]; } }
    // подпись ближайшего этапа из данных (пусто, если не задана)
    public string StepDescription
    {
        get
        {
            EnsureData();
            return (data != null && StepIndex < data.StepCount) ? data.steps[StepIndex].description : "";
        }
    }

    // можно ли нажать «Играть»: всё сдано, этап не идёт, пауза между этапами прошла
    public bool CanPlay
    {
        get { return useWindow && !isFinished && !IsLocked && !revealing && !stageRunning && AllDeposited; }
    }

    // есть ли в рюкзаке то, что ещё можно сдать на этап (кнопка «Сдать ресурсы» в окне)
    public bool CanDeposit(PlayerInventory inv)
    {
        if (inv == null || isFinished || IsLocked || revealing || stageRunning) return false;
        EnsureData();
        if (deposited < CostTotal && inv.woodCount > 0) return true;
        if (depositedPlanks < PlankTotal && inv.planksCount > 0) return true;
        return depositedCoins < CoinTotal && deposited >= CostTotal && depositedPlanks >= PlankTotal && inv.coinsCount >= CoinTotal;
    }

    // Сдаёт на этап всё, что можно, ОДНИМ действием (брёвна, доски, монеты по тем же правилам, что и DepositTick).
    // Возвращает, сколько единиц сдано. Повторный вызов без новых ресурсов вернёт 0: двойной клик ничего не списывает.
    public int DepositAll(PlayerInventory inv)
    {
        if (inv == null || isFinished || IsLocked || revealing || stageRunning) return 0;
        EnsureData();
        int moved = 0;

        int w = Mathf.Min(CostTotal - deposited, inv.woodCount);
        if (w > 0 && inv.RemoveWood(w)) { deposited += w; moved += w; NotifyDeposit(0, w); }

        int p = Mathf.Min(PlankTotal - depositedPlanks, inv.planksCount);
        if (p > 0 && inv.RemovePlanks(p)) { depositedPlanks += p; moved += p; NotifyDeposit(1, p); }

        if (depositedCoins < CoinTotal && deposited >= CostTotal && depositedPlanks >= PlankTotal && inv.coinsCount >= CoinTotal)
        {
            inv.AddCoins(-CoinTotal);
            depositedCoins = CoinTotal;
            moved += CoinTotal;
            NotifyDeposit(2, 1);   // монеты уходят одним платежом: одна иконка
        }

        if (moved > 0) { SaveSystem.RequestSave("buildings"); }
        return moved;
    }

    // Кнопка «Играть». Сейчас запускает обычный таймер этапа (как раньше после сдачи ресурсов).
    // ТОЧКА РАСШИРЕНИЯ для мини-игры: здесь вместо таймера запускается мини-игра, а по её результату вызывается CompleteStep.
    // Возвращает false, если запускать рано или этап уже идёт (повторный клик игнорируется).
    public bool StartStage()
    {
        if (!CanPlay) return false;
        stageRunning = true;
        return true;
    }
    public float Progress { get { return GetProgress(); } }

    // сколько брёвен ещё осталось СДАТЬ на ближайший этап (цена минус уже сданное).
    // Игрок сдаёт брёвна частями (рюкзак на старте вмещает 4, а костёр стоит 8): без этого стройка была бы недостижима.
    // Метка, подсветка и ценник сравнивают Cost с бревнами в рюкзаке, поэтому им ничего менять не нужно.
    public int Cost { get { EnsureData(); return Mathf.Max(0, costs[StepIndex] - deposited); } }
    // полная цена ближайшего этапа
    public int CostTotal { get { EnsureData(); return costs[StepIndex]; } }
    // сколько брёвен уже сдано на ближайший этап
    public int Deposited { get { return deposited; } }
    public int PlankTotal { get { EnsureData(); return planks[StepIndex]; } }
    public int CoinTotal { get { EnsureData(); return coins[StepIndex]; } }
    public int DepositedPlanks { get { return depositedPlanks; } }
    public int DepositedCoins { get { return depositedCoins; } }
    // у этапа есть цена не только в брёвнах (доски или монеты)
    public bool HasExtraNeeds { get { return PlankTotal > 0 || CoinTotal > 0; } }
    // всё нужное уже сдано, остаётся время стройки
    public bool AllDeposited { get { return deposited >= CostTotal && depositedPlanks >= PlankTotal && depositedCoins >= CoinTotal; } }
    // сумма брёвен по всем этапам (для проверок и отладки)
    public int TotalWoodAllSteps { get { EnsureData(); int t = 0; for (int i = 0; i < costs.Length; i++) t += costs[i]; return t; } }

    // сколько этапов уже построено / сколько всего / название ближайшего этапа (для цели в HUD и моста)
    public int StepsDone { get { return step; } }
    public int StepsTotal { get { EnsureData(); return costs.Length; } }
    public string StepName { get { EnsureData(); return (data != null && StepIndex < data.StepCount) ? data.steps[StepIndex].name : ""; } }

    // есть ли у игрока что сдать прямо сейчас (подсветка площадки)
    public bool PlayerCanContribute(PlayerInventory inv)
    {
        if (inv == null || isFinished || revealing) return false;   // на время показа стадии площадка заблокирована, подсвечивать нечего
        if (AllDeposited) return true;
        if (deposited < CostTotal && inv.woodCount > 0) return true;
        if (depositedPlanks < PlankTotal && inv.planksCount > 0) return true;
        if (depositedCoins < CoinTotal && deposited >= CostTotal && depositedPlanks >= PlankTotal && inv.coinsCount >= CoinTotal) return true;
        return false;
    }

    // чтобы метка знала, когда перерисовать текст цены
    public int NeedKey(PlayerInventory inv)
    {
        int k = deposited * 31 + depositedPlanks * 1009 + depositedCoins * 100003 + step * 7;
        if (inv != null) k += inv.planksCount * 13 + (inv.coinsCount >= CoinTotal ? 1 : 0);
        return k;
    }

    // «Бревна 2/4 · Доски 0/6 · Монеты 40/150» (зелёным то, что набрано)
    public string NeedSummary(PlayerInventory inv)
    {
        var parts = new System.Collections.Generic.List<string>();
        if (CostTotal > 0) parts.Add(Part("Бревна", deposited, CostTotal));
        if (PlankTotal > 0) parts.Add(Part("Доски", depositedPlanks, PlankTotal));
        if (CoinTotal > 0)
        {
            int have = depositedCoins >= CoinTotal ? CoinTotal : (inv != null ? Mathf.Min(inv.coinsCount, CoinTotal) : 0);
            parts.Add(Part("Монеты", have, CoinTotal));
        }
        return string.Join(" · ", parts.ToArray());
    }

    static string Part(string name, int have, int total)
    {
        string c = have >= total ? "#6BCB5A" : "#FFFFFF";
        return name + " <color=" + c + ">" + have + "/" + total + "</color>";
    }
    // ---------------------------------------------------------------- показ готовой стадии (reveal)
    // идёт ли показ хоть на одной стройке (MobileInput.ModalOpen и StoryFlow читают это)
    public static bool AnyRevealing { get { return revealingCount > 0; } }
    public bool IsRevealing { get { return revealing; } }
    // показывается последняя стадия (дольше и с большим количеством эффектов)
    public bool RevealIsFinal { get { return revealFinal; } }
    // показ пропущен игроком (BuildFx в этом случае сразу достраивает здание, а не ждёт конца анимации)
    public bool RevealSkipped { get { return revealing && revealSkipped; } }
    // сколько всего длится показ (не меняется при пропуске: от него считается облёт камеры)
    public float RevealDuration { get { return revealDuration; } }
    // сколько секунд прошло с начала показа / сколько осталось (0 вне показа)
    public float RevealElapsed { get { return revealing ? Mathf.Max(0f, Time.time - revealStart) : 0f; } }
    public float RevealLeft { get { return revealing ? Mathf.Max(0f, revealEndAt - Time.time) : 0f; } }
    // название только что сданного этапа из данных (пусто, если не задано)
    public string LastStepName { get { return lastStepName; } }

    // название здания: метка стройки, потом данные, потом имя объекта (так же, как в окне стройки)
    public string BuildingTitle
    {
        get
        {
            var v = GetComponent<BuildSiteTagView>();
            if (v != null && !string.IsNullOrEmpty(v.buildingName)) return v.buildingName;
            if (data != null && !string.IsNullOrEmpty(data.buildingName)) return data.buildingName;
            return name;
        }
    }

    // Пропуск показа (E, Пробел, Esc). Раньше RevealSkipGrace с начала не принимается. После пропуска камера ещё RevealSkipTail секунд
    // возвращается, площадка и управление остаются заблокированы на это время (так E, которой пропустили, не откроет окно стройки).
    // Возвращает true, если пропуск принят.
    public bool SkipReveal()
    {
        if (!revealing) return false;
        if (Time.time - revealStart < GameBalance.RevealSkipGrace) return false;
        float tail = Time.time + GameBalance.RevealSkipTail;
        if (revealEndAt <= tail) return false;   // и так заканчивается
        revealEndAt = tail;
        revealSkipped = true;
        return true;
    }

    // хватает ли у игрока ресурсов на ближайший этап (с учётом уже сданного). BuildReveal по этому показывает подсказку «Следующая стадия готова»
    public bool CanAffordNext(PlayerInventory inv)
    {
        if (inv == null || isFinished || IsLocked) return false;
        EnsureData();
        if (inv.woodCount + deposited < CostTotal) return false;
        if (inv.planksCount + depositedPlanks < PlankTotal) return false;
        return depositedCoins >= CoinTotal || inv.coinsCount >= CoinTotal;
    }

    // сколько секунд идёт ближайший этап
    public float BuildTime { get { EnsureData(); return times[StepIndex]; } }
    // какая стадия построена сейчас (у костра 1, 2, 3); у обычной постройки 0 до конца и 1 после
    public int StageCurrent { get { EnsureData(); return staged ? firstStage + step : (isFinished ? 1 : 0); } }
    // сколько всего стадий
    public int StageTotal { get { EnsureData(); return staged ? firstStage + costs.Length : 1; } }

    public void SetLocked(bool value) { locked = value; }

    // ---------------------------------------------------------------- внутреннее
    // рабочие значения: собираются один раз из Data или из запасных полей
    int[] costs;
    int[] planks;
    int[] coins;
    float[] times;
    int[] xps;
    int firstStage = 1;
    bool staged;

    float currentBuildTimer = 0f;
    bool isBuilding = false;
    bool stageRunning = false;    // режим окна: этап запущен кнопкой «Играть»
    bool isFinished = false;
    int step = 0;                 // сколько этапов уже сдано
    string lastStepName = "";     // название этапа, который сдан последним (для плашки показа)

    // показ стадии (reveal): площадка заблокирована, пока revealing
    static int revealingCount;    // сколько показов идёт сейчас (обычно 0 или 1)
    bool revealing, revealFinal, revealSkipped;
    float revealStart, revealEndAt, revealDuration;
    int deposited = 0;            // брёвен уже сдано на текущий этап
    int depositedPlanks = 0;
    int depositedCoins = 0;
    float depositTimer = 0f;
    const float DepositInterval = 0.12f;   // одно бревно за раз, чтобы сдача была видна
    int insideCount = 0;          // у игрока несколько коллайдеров, считаем входы
    PlayerInventory inside;
    WorldTag ring;

    bool Staged { get { EnsureData(); return staged; } }
    int StepIndex { get { return Mathf.Min(step, costs.Length - 1); } }

    // finishedBuilding лежит внутри иерархии стадий (или это сама иерархия): им управляет HutStages, мы не трогаем
    bool FinishedIsPartOfStages
    {
        get { return finishedBuilding != null && stages != null && finishedBuilding.transform.IsChildOf(stages.transform); }
    }
    bool OwnsFinished { get { return finishedBuilding != null && !FinishedIsPartOfStages; } }
    // показывать ли finishedBuilding в конце: только у одноэтапных построек (или если явно разрешено)
    bool ShowFinishedAtEnd { get { return OwnsFinished && (!Staged || finishedWithStages); } }

    void EnsureData() { if (costs == null) ResolveData(); }

    // собираем рабочие значения: Data главнее запасных полей
    void ResolveData()
    {
        if (stages == null) stages = GetComponentInParent<HutStages>();

        if (data != null && data.StepCount > 0)
        {
            int n = data.StepCount;
            costs = new int[n]; times = new float[n]; xps = new int[n]; planks = new int[n]; coins = new int[n];
            for (int i = 0; i < n; i++)
            {
                var s = data.steps[i];
                costs[i] = Mathf.Max(0, s.woodCost);
                planks[i] = Mathf.Max(0, s.plankCost);
                coins[i] = Mathf.Max(0, s.coinCost);
                times[i] = Mathf.Max(0.1f, s.buildTime);
                xps[i] = Mathf.Max(0, s.rewardXp);
            }
            firstStage = Mathf.Max(1, data.startStage);
            staged = stages != null || n > 1;
            if (stages == null && n > 1 && GetComponent<BuildStageLink>() == null && GetComponentInParent<ShopStages>() == null)
                Debug.LogWarning("[BuildingController] «" + name + "»: в Data " + n + " этапа(ов), но HutStages не найден. " +
                                 "Этапы будут считаться, но менять вид нечему.", this);
        }
        else if (stages != null && stageCosts != null && stageCosts.Length > 0)
        {
            int n = stageCosts.Length;
            costs = new int[n]; times = new float[n]; xps = new int[n]; planks = new int[n]; coins = new int[n];
            for (int i = 0; i < n; i++)
            {
                costs[i] = Mathf.Max(1, stageCosts[i]);
                times[i] = Mathf.Max(0.1f, buildTime);
                xps[i] = Mathf.Max(0, rewardXp);
            }
            firstStage = Mathf.Max(1, startStage);
            staged = true;
        }
        else
        {
            costs = new[] { Mathf.Max(1, woodRequired) };
            planks = new[] { 0 };
            coins = new[] { 0 };
            times = new[] { Mathf.Max(0.1f, buildTime) };
            xps = new[] { Mathf.Max(0, rewardXp) };
            firstStage = 1;
            staged = false;
            if (stages == null && stageCosts != null && stageCosts.Length > 0)
                Debug.LogWarning("[BuildingController] «" + name + "»: stageCosts заданы, но HutStages не найден. " +
                                 "Стройка будет ОДНОЭТАПНОЙ. Назначь поле Stages.", this);
        }

        // этапов больше, чем стадий с видом: «лишняя» стадия не показала бы ничего (у костра этапов ровно 2: стадии 2 и 3)
        if (staged && stages != null && autoTrimSteps)
        {
            int max = stages.StageCount - firstStage;
            if (max >= 1 && costs.Length > max)
            {
                Debug.LogWarning("[BuildingController] «" + name + "»: этапов в данных " + costs.Length + ", а видов у HutStages только " +
                                 stages.StageCount + " (стартовая стадия " + firstStage + "). Лишние этапы отброшены, поправь данные стройки.", this);
                Array.Resize(ref costs, max); Array.Resize(ref times, max); Array.Resize(ref xps, max);
                Array.Resize(ref planks, max); Array.Resize(ref coins, max);
            }
        }

        // этапов меньше, чем нужно до последнего этапа HutStages: последний был бы недостижим
        if (staged && stages != null)
        {
            int need = stages.StageCount - firstStage;
            if (need > costs.Length)
            {
                string msg = "[BuildingController] «" + name + "»: этапов в данных " + costs.Length + ", а до последнего этапа нужно " + need +
                             " (стартовая стадия " + firstStage + ", этапов в HutStages " + stages.StageCount + "). Последний этап был бы недостижим.";
                if (autoFixCosts)
                {
                    costs = Extend(costs, need); times = Extend(times, need); xps = Extend(xps, need); planks = Extend(planks, need); coins = Extend(coins, need);
                    Debug.LogWarning(msg + " Достроил недостающие этапы по образцу последнего. Поправь данные стройки.", this);
                }
                else Debug.LogWarning(msg, this);
            }
        }
    }

    static T[] Extend<T>(T[] a, int n)
    {
        var r = new T[n];
        for (int i = 0; i < n; i++) r[i] = i < a.Length ? a[i] : a[a.Length - 1];
        return r;
    }

    // при добавлении компонента сразу делаем невидимый триггер-блок
    void Reset()
    {
        var c = GetComponent<Collider>();
        if (c == null)
        {
            var b = gameObject.AddComponent<BoxCollider>();
            b.isTrigger = true;
            b.size = new Vector3(4f, 3f, 4f);
            b.center = new Vector3(0f, 1.5f, 0f);
        }
        else c.isTrigger = true;
    }

    void Awake()
    {
        // зона никогда не должна блокировать проход
        foreach (var zc in GetComponents<Collider>()) zc.isTrigger = true;

        EnsureData();


        if (tutorial == null) tutorial = FindFirstObjectByType<TutorialManager>();
        if (playerStats == null) playerStats = FindFirstObjectByType<PlayerStats>();

        // E на площадке открывает окно стройки; руками вешать компонент не нужно
        if (useWindow && GetComponent<BuildSiteInteractable>() == null) gameObject.AddComponent<BuildSiteInteractable>();

        if (staged && OwnsFinished && !finishedWithStages)
            Debug.LogWarning("[BuildingController] «" + name + "»: finishedBuilding «" + finishedBuilding.name +
                             "» задан, но у стройки есть этапы, поэтому он останется скрытым. " +
                             "Включи finishedWithStages, если он нужен в конце.", this);
    }

    // метка над площадкой (BuildSiteTagView) нужна всегда: если её не повесили в сцене (например, костёр собран старым
    // CampfireBuilder), добавляем сами. Раньше это делал BuildCostTag, теперь его нет
    void EnsureSiteTag()
    {
        if (GetComponent<BuildSiteTagView>() != null) return;   // метка работает только на одном объекте с контроллером (она берёт bc через GetComponent), метка на родителе не считается
        var view = gameObject.AddComponent<BuildSiteTagView>();
        string raw = (data != null && !string.IsNullOrEmpty(data.buildingName)) ? data.buildingName : gameObject.name;
        view.buildingName = RuTitle(raw);
    }

    // английские имена объектов («Campfire») показываем по-русски
    static string RuTitle(string n)
    {
        string l = (n ?? "").ToLowerInvariant();
        if (l.Contains("campfire") || l.Contains("bonfire")) return "Костёр";
        if (l.Contains("hut") || l.Contains("house")) return "Хижина";
        if (l.Contains("tent") || l.Contains("camp")) return "Палатка";
        if (l.Contains("shop") || l.Contains("stall") || l.Contains("market")) return "Прилавок";
        if (l.Contains("sawmill")) return "Пилорама";
        return n;
    }

    void Start()
    {
        EnsureSiteTag();   // в Start, как раньше BuildCostTag: к этому моменту все Awake (UI, шрифты) уже отработали
        if (OwnsFinished) finishedBuilding.SetActive(false);
        if (buildingVFX != null) buildingVFX.Stop();

        if (progressCanvas != null) progressCanvas.SetActive(false);
        if (progressFill != null) progressFill.fillAmount = 0f;

        if (Staged && stages != null) stages.SetStage(firstStage);   // игра всегда начинается с первого этапа

        // Если есть новая метка (BuildSiteTagView), она сама показывает таймер, старое кольцо не нужно:
        // иначе два индикатора налезают друг на друга.
        bool hasView = GetComponent<BuildSiteTagView>() != null;
        if (useRing && !hasView)
        {
            ring = WorldTag.Add(gameObject, true);
            ring.offset = ringOffset;
            ring.visible = false;
            ring.SetProgress(0f, true);
        }

        started = true;
    }

    // Восстановление из сохранения идёт здесь: к LateUpdate первого кадра отработали Start всех (BuildStageLink, HutStages),
    // и они уже не перебьют вид, который мы ставим
    void LateUpdate()
    {
        if (restorePending && started) ApplyRestore();
    }

    void OnDisable()
    {
        BuildingSaveHub.Detach(this);   // последнее состояние уходит в сейф: смена сцены не потеряет прогресс
        registry.Remove(this);
        if (revealing) EndReveal(false);   // событие не шлём: объект выключается, StoryFlow читает AnyRevealing и не зависнет
        if (isBuilding) StopBuilding();
        insideCount = 0;
        inside = null;
    }

    // ---------- вход / выход игрока ----------
    void OnTriggerEnter(Collider other)
    {
        if (isFinished) return;
        var inv = other.GetComponentInParent<PlayerInventory>();
        if (inv == null) return;

        insideCount++;
        inside = inv;

        // пришёл, но дров не хватает: сообщаем наружу, метка сама дёрнется и напишет «Нужно ещё N»
        if (insideCount == 1 && !locked && inv.woodCount < Cost && NotEnoughWood != null)
            NotEnoughWood(Cost - inv.woodCount);
    }

    void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInParent<PlayerInventory>() == null) return;

        insideCount = Mathf.Max(0, insideCount - 1);
        if (insideCount == 0)
        {
            inside = null;
            if (isBuilding && !isFinished) StopBuilding();   // игрок убежал: прогресс сохраняется
        }
    }

    // OnTriggerStay не вызывается, когда Rigidbody игрока "уснул" (стоит на месте),
    // поэтому стройка идёт в Update, а не в OnTriggerStay
    void Update()
    {
        // показ готовой стадии: площадка стоит, пока он не кончится (даже у достроенного здания: ему нужен конец показа)
        if (revealing)
        {
            if (Time.time >= revealEndAt) EndReveal(true);
            return;
        }

        if (isFinished || inside == null) return;

        if (locked)
        {
            if (isBuilding) StopBuilding();
            return;
        }

        // режим окна: ресурсы сдаёт окно (DepositAll), таймер идёт только после «Играть» (StartStage)
        if (useWindow)
        {
            if (!stageRunning || !AllDeposited)
            {
                if (isBuilding) StopBuilding();
                return;
            }
        }
        // сдаём ресурсы из рюкзака, пока этап не набран: брёвна и доски по одному, монеты разом (когда хватает на всё)
        else if (!AllDeposited)
        {
            depositTimer -= Time.deltaTime;
            if (depositTimer <= 0f)
            {
                depositTimer = DepositInterval;
                DepositTick();
            }
            if (!AllDeposited)
            {
                if (isBuilding) StopBuilding();
                return;   // ждём остальное
            }
        }

        if (!isBuilding) BeginBuilding();

        currentBuildTimer += Time.deltaTime;
        float progress = GetProgress();

        if (progressFill != null) progressFill.fillAmount = progress;

        if (ring != null)
        {
            ring.SetProgress(progress);
            ring.SetProgressColor(Color.Lerp(new Color32(0xF2, 0xA9, 0x3B, 255), new Color32(0x6B, 0xCB, 0x5A, 255), progress));
            ring.SetText(Mathf.CeilToInt(Mathf.Max(0f, BuildTime - currentBuildTimer)).ToString());
        }

        if (currentBuildTimer >= BuildTime) CompleteStep(inside);
    }

    void DepositTick()
    {
        bool changed = false;
        if (deposited < CostTotal && inside.woodCount > 0) { inside.RemoveWood(1); deposited++; changed = true; NotifyDeposit(0, 1); }
        if (depositedPlanks < PlankTotal && inside.planksCount > 0) { inside.RemovePlanks(1); depositedPlanks++; changed = true; NotifyDeposit(1, 1); }
        // монеты уходят одним платежом и только когда брёвна и доски этапа уже сданы: деньги не зависают в стройке,
        // пока игрок ещё носит брёвна (у моста это 150-400 монет)
        if (depositedCoins < CoinTotal && deposited >= CostTotal && depositedPlanks >= PlankTotal && inside.coinsCount >= CoinTotal)
        {
            inside.AddCoins(-CoinTotal);
            depositedCoins = CoinTotal;
            changed = true;
            NotifyDeposit(2, 1);
        }
        if (changed) { SaveSystem.RequestSave("buildings"); }
    }

    void BeginBuilding()
    {
        isBuilding = true;
        if (buildingVFX != null) buildingVFX.Play();

        if (ring != null) ring.visible = true;
        else if (progressCanvas != null) progressCanvas.SetActive(true);
    }

    void StopBuilding()
    {
        isBuilding = false;
        if (buildingVFX != null) buildingVFX.Stop();

        if (ring != null) ring.visible = false;
        if (progressCanvas != null) progressCanvas.SetActive(false);
    }

    // ---------- этап готов ----------
    // inv == null только в тесте из контекстного меню: брёвна не списываются
    void CompleteStep(PlayerInventory inv)
    {
        if (isFinished) return;   // повторный вызов (событие, тест из меню) не выдаёт награду второй раз
        EnsureData();
        int idx = StepIndex;            // какой этап сдаём сейчас
        int cost = costs[idx];
        int xp = xps[idx];

        // брёвна уже списаны по одному в Update (частичная сдача); тут только сбрасываем счётчик
        deposited = 0;
        depositedPlanks = 0;
        depositedCoins = 0;
        depositTimer = 0f;
        stageRunning = false;           // следующий этап снова запускается кнопкой «Играть»

        lastStepName = (data != null && idx < data.StepCount) ? data.steps[idx].name : "";

        step++;
        bool last = step >= costs.Length;

        isBuilding = false;
        currentBuildTimer = 0f;

        if (buildingVFX != null) buildingVFX.Stop();
        if (finishVFX != null) finishVFX.Play();

        if (staged && stages != null) stages.SetStage(firstStage + step);   // 1 -> 2 -> 3

        if (debugLog)
            Debug.Log("[BuildingController] «" + name + "»: сдан этап " + (idx + 1) + " (цена " + cost + ", время " + times[idx] +
                      "), стадия " + StageCurrent + "/" + StageTotal +
                      (stages != null ? ", HutStages.CurrentStage=" + stages.CurrentStage : "") + ", last=" + last, this);

        if (progressCanvas != null) progressCanvas.SetActive(false);
        if (ring != null)
        {
            ring.SetProgress(1f);
            ring.SetText("");
            ring.Punch(0.5f);
            ring.visible = false;
        }

        if (last)
        {
            isFinished = true;
            if (ShowFinishedAtEnd)
            {
                finishedBuilding.SetActive(true);
                StartCoroutine(PopIn(finishedBuilding.transform)); // здание «вырастает» с отскоком
            }
        }
        else
        {
            // готовим следующий этап: метка сама подхватит новую цену
            if (ring != null) ring.SetProgress(0f, true);
        }

        // Награда: «+N XP» вылетает над стройкой
        if (xp > 0)
        {
            if (playerStats != null) playerStats.AddXP(xp);
            if (RewardPopups.Instance != null)
                RewardPopups.Instance.Show(transform.position + Vector3.up * 3f, 0, xp);
        }
        if (rewardCoins > 0 && inv != null) inv.AddCoins(rewardCoins);

        if (completesTutorial && !tutorialFired && last)
        {
            // Сигнал обучению только когда построены ВСЕ этапы (у костра две стадии стройки: 2 и 3).
            // Раньше он уходил после первого этапа (tutorialStages = 1): NPC говорил «костёр построен», а костёр был на стадию позади.
            // IntroFlow (новое обучение) главнее старого TutorialManager.
            tutorialFired = true;
            if (IntroFlow.Instance != null) IntroFlow.Instance.CompleteCampfire(this);
            else if (tutorial != null) tutorial.OnFoundationBuilt();
        }

        // сначала события, потому что UI подпишется и сам покажет «Стадия N готова»
        if (StageBuilt != null) StageBuilt();
        if (last && Finished != null) Finished();

        if (!SaveSystem.IsLoading) SaveSystem.RequestSave("buildings");   // этап сдан: стадия и сюжет уходят в файл

        // и только потом показ: окно уже закрылось (оно слушает StageBuilt), вид здания уже сменился (HutStages, BuildStageLink)
        StartReveal(last);
    }

    // ---------- показ стадии ----------
    void StartReveal(bool last)
    {
        if (revealing) EndReveal(false);   // повторная сдача посреди показа (тест из меню): начинаем показ заново
        revealing = true;
        revealFinal = last;
        revealSkipped = false;
        revealDuration = last ? GameBalance.RevealTimeFinal : GameBalance.RevealTime;
        revealStart = Time.time;
        revealEndAt = revealStart + revealDuration;
        revealingCount++;

        BuildReveal.Ensure();   // камера, плашка и виньетка (компонент создаётся сам)
        GameEvents.RaiseBuildRevealStarted(this);
    }

    // raise = false при выключении объекта: тогда событие конца не шлём
    void EndReveal(bool raise)
    {
        if (!revealing) return;
        revealing = false;
        revealingCount = Mathf.Max(0, revealingCount - 1);
        if (raise) GameEvents.RaiseBuildRevealFinished(this);
    }

    public float GetProgress() { return Mathf.Clamp01(currentBuildTimer / Mathf.Max(0.01f, BuildTime)); }

    IEnumerator PopIn(Transform t)
    {
        Vector3 target = t.localScale;
        const float duration = 0.5f;
        float time = 0f;

        while (time < duration && t != null)
        {
            time += Time.deltaTime;
            float k = EaseOutBack(Mathf.Clamp01(time / duration));
            t.localScale = target * Mathf.LerpUnclamped(0.3f, 1f, k);
            yield return null;
        }

        if (t != null) t.localScale = target;
    }

    static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float u = x - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }

    // ---------------------------------------------------------------- сохранение (общий BuildingSaveHub, ключ «buildings»)
    // ID стройки: путь по иерархии + тип. InstanceID не годится: он другой при каждом запуске.
    // Переименование объекта в сцене = новый ID (прогресс этой стройки начнётся заново).
    internal string saveId;
    BuildSiteKind kindCache = BuildSiteKind.Other;
    bool kindKnown, started, restorePending;
    BuildingSaveEntry restoreEntry;

    static readonly string[] SaveCampfireKeys = { "campfire", "костёр", "костер" };

    public string SaveId { get { return saveId != null ? saveId : MakeBaseId(); } }

    // что это за стройка (по тем же словам в названии, что использует StoryFlow)
    public BuildSiteKind SiteKind
    {
        get
        {
            if (!kindKnown) { kindCache = DetectKind(); kindKnown = true; }
            return kindCache;
        }
    }

    BuildSiteKind DetectKind()
    {
        string path = StoryFlow.PathOf(this);
        if (StoryFlow.HasAny(path, SaveCampfireKeys)) return BuildSiteKind.Campfire;
        if (StoryFlow.MatchesSite(StoryFlow.Site.Hut, path)) return BuildSiteKind.Hut;
        if (StoryFlow.MatchesSite(StoryFlow.Site.Market, path)) return BuildSiteKind.Market;
        if (StoryFlow.MatchesSite(StoryFlow.Site.Tent, path)) return BuildSiteKind.Tent;
        if (StoryFlow.MatchesSite(StoryFlow.Site.Bridge, path)) return BuildSiteKind.Bridge;
        return BuildSiteKind.Other;
    }

    internal string MakeBaseId()
    {
        string path = name;
        for (var p = transform.parent; p != null; p = p.parent) path = p.name + "/" + path;
        return path + "|" + SiteKind;
    }

    // снимок состояния. Пока сохранённое ещё не применено, отдаём его же: автосохранение не затрёт прогресс дефолтами
    internal BuildingSaveEntry CaptureEntry()
    {
        if (restorePending && restoreEntry != null) return restoreEntry;
        EnsureData();
        return new BuildingSaveEntry
        {
            id = SaveId,
            kind = (int)SiteKind,
            step = step,
            stage = StageCurrent,
            deposited = deposited,
            depositedPlanks = depositedPlanks,
            depositedCoins = depositedCoins,
            isFinished = isFinished,
            tutorialFired = tutorialFired
        };
    }

    internal void QueueRestore(BuildingSaveEntry e)
    {
        restoreEntry = e;
        restorePending = e != null;
    }

    // Ставит стройку в сохранённое состояние БЕЗ наград, XP, звуков, BuildFx, показа стадии, StageBuilt/Finished и сигнала обучению.
    // Единственное событие: GameEvents.BuildingRestored (для вида: ворота, NPC, свет). Блокировку (locked) ведёт StoryFlow
    void ApplyRestore()
    {
        var e = restoreEntry;
        restoreEntry = null;
        restorePending = false;
        if (e == null) return;
        EnsureData();

        // Load посреди показа стадии (редко): снимаем показ, иначе камера и площадка зависнут. Это событие нужно BuildReveal, чтобы вернуть камеру
        if (revealing) EndReveal(true);
        if (isBuilding) StopBuilding();
        stageRunning = false;
        currentBuildTimer = 0f;
        depositTimer = 0f;
        lastStepName = "";

        step = Mathf.Clamp(e.step, 0, costs.Length);
        isFinished = e.isFinished || step >= costs.Length;
        if (isFinished) step = costs.Length;

        // сданное считаем по ПОСЛЕ-шаговым ценам (CostTotal зависит от step)
        deposited = isFinished ? 0 : Mathf.Clamp(e.deposited, 0, CostTotal);
        depositedPlanks = isFinished ? 0 : Mathf.Clamp(e.depositedPlanks, 0, PlankTotal);
        depositedCoins = isFinished ? 0 : Mathf.Clamp(e.depositedCoins, 0, CoinTotal);
        tutorialFired = e.tutorialFired;

        // вид: те же номера стадий, что и в CompleteStep, но без событий
        if (staged && stages != null) stages.SetStageSilent(firstStage + step);
        if (OwnsFinished) finishedBuilding.SetActive(ShowFinishedAtEnd && isFinished);

        var link = GetComponent<BuildStageLink>();
        if (link != null)
        {
            if (link.shop != null) link.shop.RestoreStage(staged ? firstStage + step : (isFinished ? 3 : 1));
            if (link.tent != null) link.tent.RestoreStage(isFinished ? 2 : 1);
        }

        if (progressCanvas != null) progressCanvas.SetActive(false);
        if (progressFill != null) progressFill.fillAmount = 0f;
        if (ring != null) { ring.visible = false; ring.SetProgress(0f, true); }

        GameEvents.RaiseBuildingRestored(SaveId, StageCurrent);
    }

    // ---------- диагностика и тесты (ПКМ по заголовку компонента) ----------
    [ContextMenu("Диагностика стройки")]
    void Diagnose()
    {
        EnsureData();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("[BuildingController] «" + name + "»");
        sb.AppendLine("Источник цены: " + (data != null && data.StepCount > 0 ? "Build Site Data «" + data.name + "»" : "запасные поля контроллера"));
        sb.AppendLine("Staged: " + staged + " | stages: " + (stages != null ? stages.name : "НЕТ"));
        if (stages != null)
            sb.AppendLine("HutStages.StageCount: " + stages.StageCount + " | текущий этап: " + stages.CurrentStage +
                          " | объекты Stage1/2/3: " + stages.HasStage(1) + "/" + stages.HasStage(2) + "/" + stages.HasStage(3));
        sb.AppendLine("Стартовая стадия: " + firstStage + " | цены: [" + string.Join(", ", costs) + "] | время: [" + string.Join(", ", times) + "] | XP: [" + string.Join(", ", xps) + "]");
        sb.AppendLine("StageCurrent/StageTotal: " + StageCurrent + "/" + StageTotal + " | сдано этапов: " + step + " | Cost: " + Cost + " | BuildTime: " + BuildTime);
        sb.AppendLine("IsFinished: " + isFinished + " | IsBuilding: " + isBuilding + " | IsLocked: " + IsLocked);
        sb.AppendLine("finishedBuilding: " + (finishedBuilding != null
            ? finishedBuilding.name + " (часть стадий: " + FinishedIsPartOfStages + ", покажется в конце: " + ShowFinishedAtEnd + ")"
            : "нет"));
        Debug.Log(sb.ToString(), this);
    }

    [ContextMenu("Тест: сдать текущий этап (без брёвен)")]
    void DebugCompleteStep()
    {
        if (!Application.isPlaying) { Debug.Log("[BuildingController] Тест работает только в Play Mode.", this); return; }
        if (isFinished) { Debug.Log("[BuildingController] Стройка уже завершена.", this); return; }
        CompleteStep(null);
    }

#if UNITY_EDITOR
    // Создаёт ассет Build Site Data из запасных полей и сразу назначает его в поле Data
    [ContextMenu("Создать Build Site Data из текущих полей")]
    void CreateDataAsset()
    {
        if (Application.isPlaying) { Debug.Log("[BuildingController] Создавать данные нужно вне Play Mode.", this); return; }

        var d = ScriptableObject.CreateInstance<BuildSiteData>();
        d.buildingName = transform.root.name;
        d.startStage = Mathf.Max(1, startStage);
        int n = (stageCosts != null && stageCosts.Length > 0) ? stageCosts.Length : 1;
        d.steps = new BuildSiteData.Step[n];
        for (int i = 0; i < n; i++)
        {
            d.steps[i] = new BuildSiteData.Step
            {
                name = "Стадия " + (d.startStage + i) + " → " + (d.startStage + i + 1),
                woodCost = Mathf.Max(1, (stageCosts != null && stageCosts.Length > 0) ? stageCosts[i] : woodRequired),
                buildTime = Mathf.Max(0.1f, buildTime),
                rewardXp = Mathf.Max(0, rewardXp)
            };
        }

        const string dir = "Assets/_Game/BuildSites";
        if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/_Game")) UnityEditor.AssetDatabase.CreateFolder("Assets", "_Game");
        if (!UnityEditor.AssetDatabase.IsValidFolder(dir)) UnityEditor.AssetDatabase.CreateFolder("Assets/_Game", "BuildSites");
        string path = UnityEditor.AssetDatabase.GenerateUniqueAssetPath(dir + "/BuildSite_" + transform.root.name + ".asset");
        UnityEditor.AssetDatabase.CreateAsset(d, path);

        UnityEditor.Undo.RecordObject(this, "Назначить Build Site Data");
        data = d;
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.AssetDatabase.SaveAssets();
        UnityEditor.EditorGUIUtility.PingObject(d);
        Debug.Log("[BuildingController] Создан и назначен " + path + ". Теперь цену и время меняй только в нём.", d);
    }
#endif

    // Зона невидима в игре, но в Scene видна зелёным блоком
    void OnDrawGizmos()
    {
        if (!drawZone) return;
        var bc = GetComponent<BoxCollider>();
        if (bc == null) return;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.4f, 1f, 0.4f, 0.12f);
        Gizmos.DrawCube(bc.center, bc.size);
        Gizmos.color = new Color(0.4f, 1f, 0.4f, 0.9f);
        Gizmos.DrawWireCube(bc.center, bc.size);
    }
}

// ================================================================ сохранение строек
public enum BuildSiteKind { Other = 0, Campfire = 1, Hut = 2, Market = 3, Tent = 4, Bridge = 5 }

// Запись одной стройки. ДОБАВЛЯТЬ ПОЛЯ ТОЛЬКО С ДЕФОЛТАМИ: старые файлы без них читаются нормально
[Serializable]
public class BuildingSaveEntry
{
    public string id = "";
    public int kind;
    public int step;              // сколько этапов уже построено (0 = ничего)
    public int stage;             // StageCurrent на момент записи (для справки и отладки)
    public int deposited;         // брёвен сдано на ближайший этап
    public int depositedPlanks;
    public int depositedCoins;
    public bool isFinished;
    public bool tutorialFired;
}

[Serializable]
class BuildingSaveData
{
    public int version = 1;
    public List<BuildingSaveEntry> items = new List<BuildingSaveEntry>();
}

// Одна общая ISaveable на все стройки (ключ «buildings», порядок 60). Постройки сами регистрируются в OnEnable и снимаются в OnDisable.
// Записи, чьей стройки сейчас нет в сцене (выключена, другая сцена), в файле сохраняются как есть.
sealed class BuildingSaveHub : ISaveable
{
    static BuildingSaveHub instance;

    readonly Dictionary<string, BuildingSaveEntry> store = new Dictionary<string, BuildingSaveEntry>();   // всё, что знаем из файла и от выгруженных строек
    readonly Dictionary<string, BuildingController> live = new Dictionary<string, BuildingController>();   // стройки в сцене

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { instance = null; }

    public static void Attach(BuildingController b)
    {
        if (instance == null)
        {
            instance = new BuildingSaveHub();
            SaveSystem.Register(instance);   // если Load() уже был, секция применится прямо здесь
        }
        instance.Add(b);
    }

    public static void Detach(BuildingController b)
    {
        if (instance != null) instance.Remove(b);
    }

    void Add(BuildingController b)
    {
        string id = b.MakeBaseId();
        BuildingController other;
        if (live.TryGetValue(id, out other) && other != b && other != null)
        {
            int n = 2;
            while (live.ContainsKey(id + "#" + n)) n++;
            Debug.LogWarning("BuildingSaveHub: две стройки с одним путём и типом «" + id + "». Второй выдан ID «" + id + "#" + n + "»; переименуй объекты, чтобы ID не зависели от порядка загрузки.", b);
            id = id + "#" + n;
        }
        b.saveId = id;
        live[id] = b;

        BuildingSaveEntry e;
        if (store.TryGetValue(id, out e)) b.QueueRestore(e);
    }

    void Remove(BuildingController b)
    {
        if (b.saveId == null) return;
        BuildingController cur;
        if (!live.TryGetValue(b.saveId, out cur) || cur != b) return;
        live.Remove(b.saveId);
        store[b.saveId] = b.CaptureEntry();
    }

    // ------------------------------------------------------------ ISaveable
    public string SaveKey { get { return "buildings"; } }
    public int LoadOrder { get { return 60; } }

    public string Save()
    {
        var d = new BuildingSaveData();
        foreach (var kv in store)
            if (!live.ContainsKey(kv.Key)) d.items.Add(kv.Value);
        foreach (var kv in live)
            if (kv.Value != null) d.items.Add(kv.Value.CaptureEntry());
        return JsonUtility.ToJson(d);
    }

    public void Load(string json, int formatVersion)
    {
        BuildingSaveData d = null;
        if (!string.IsNullOrEmpty(json))
        {
            try { d = JsonUtility.FromJson<BuildingSaveData>(json); }
            catch (Exception e) { Debug.LogWarning("BuildingSaveHub: не удалось прочитать сохранение строек: " + e.Message); }
        }
        if (d == null || d.items == null) { ResetToDefault(); return; }

        store.Clear();
        for (int i = 0; i < d.items.Count; i++)
            if (d.items[i] != null && !string.IsNullOrEmpty(d.items[i].id)) store[d.items[i].id] = d.items[i];

        // стройки, что уже в сцене: сохранённое состояние или чистое (если в файле их нет)
        foreach (var kv in live)
        {
            if (kv.Value == null) continue;
            BuildingSaveEntry e;
            kv.Value.QueueRestore(store.TryGetValue(kv.Key, out e) ? e : new BuildingSaveEntry { id = kv.Key });
        }
    }

    public void ResetToDefault()
    {
        store.Clear();
        foreach (var kv in live)
            if (kv.Value != null) kv.Value.QueueRestore(new BuildingSaveEntry { id = kv.Key });
    }
}
