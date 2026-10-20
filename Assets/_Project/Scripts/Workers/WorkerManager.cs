// Assets/_Project/Scripts/Workers/WorkerManager.cs (НЕ в Editor)
// Менеджер рабочих (дизайн «Система рабочих», задача 3). Тонкая обвязка вокруг CampSimulation:
//   - гонит симуляцию по игровому времени (Time.deltaTime), выдаёт монеты Приказчиков игроку;
//   - склад лагеря: «Забрать» (в рюкзак, сколько влезет) и «Продать −20%» на месте;
//   - ростер и слоты, выдача рабочего (Grant), опыт (AddXp);
//   - паки (задача 4): TryOpenPack списывает монеты, проверяет уровень и вызывает WorkerPackSystem; событие WorkerPackSystem.OnPackOpened;
//   - события для UI (UI на них подписывается, менеджер про UI не знает);
//   - сохранение ISaveable, секция «workers», версия 2; старые цифровые рабочие (версия 1) превращаются в «Подмастерьев» (WorkerApprentice);
//   - подарочный Артельный пак при достроенной палатке; телеметрия pack_opened и worker_leveled (Telemetry).
//
// Время и пауза: как раньше. Доходит по Time.deltaTime: на паузе (timeScale 0) всё стоит, окна игру не паузят,
//   долгий кадр режется до maxFrameStep, офлайн-дохода нет. Нет игрока (инвентаря): время не копим.
// Экономика: ТОЛЬКО WorkerBalance (своих чисел здесь нет). Старый найм за монеты убран: рабочие приходят из паков.
// Питомцы («Доход рабочих») и навык «Хороший начальник» множат доход Приказчиков (CampSimulation.coinMult).
using System;
using System.Collections.Generic;
using UnityEngine;

public enum GrantResult { Added, Duplicate, Invalid }
public enum PackOpenResult { Ok, NoDatabase, Locked, NoCoins, NoPlayer }

public struct CampTakeResult { public int logs, planks; public bool Any { get { return logs + planks > 0; } } }
public struct CampSellResult { public int logs, planks, coins; public bool Any { get { return logs + planks > 0; } } }

public class WorkerManager : MonoBehaviour, ISaveable
{
    public static WorkerManager Instance { get; private set; }

    [Header("Данные")]
    [Tooltip("Ассет WorkerDatabase (Tools > BuilderSim > Рабочие > Создать базу рабочих). Нужен для загрузки сохранения")]
    public WorkerDatabase database;

    [Header("Отладка")]
    [Tooltip("Открывать паки независимо от уровня игрока (для тестов)")]
    public bool debugIgnoreLevelLock = false;

    [Header("Защита (баланс здесь не хранится, он в WorkerBalance)")]
    [Tooltip("Самый длинный кадр, который учитывается (с). Длиннее режется. Офлайн-дохода нет")]
    public float maxFrameStep = 0.25f;

    // ------------------------------------------------------------ события (UI подписывается)
    public static event Action<WorkerProduced> OnWorkerProduced;     // рабочий произвёл монеты, бревна или доски
    public static event Action<WorkerState> OnWorkerLevelUp;         // рабочий получил уровень
    public static event Action OnCampStockChanged;                   // изменился склад лагеря (или его вместимость)
    public static event Action OnRosterChanged;                      // изменился ростер или слоты

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic()
    {
        Instance = null;
        OnWorkerProduced = null; OnWorkerLevelUp = null; OnCampStockChanged = null; OnRosterChanged = null;
    }

    // ------------------------------------------------------------ состояние
    CampSimulation sim;
    WorkerPackSystem packs;
    int totalEarned;
    int legacyWorkerCount;       // сколько рабочих было в старом сохранении (версия 1): для миграции в задаче 6
    float findAt;
    bool loadingNow;             // идёт Load(): события UI и RequestSave не отправляем
    int pendingRefund;           // компенсация за старых рабочих сверх трёх: выплатим, когда появится игрок (хранится в сохранении)
    bool saveAfterLoad;          // после миграции записать секцию уже новой версии
    bool giftChecked;            // подарочный пак уже предложен или проверен
    float giftAt;

    PlayerInventory inv;

    // ------------------------------------------------------------ чтение состояния
    public CampSimulation Sim { get { return sim; } }
    public WorkerPackSystem Packs { get { return packs; } }      // счётчики гарантий для окна пака: Packs.OpensUntilRare(...) и т.д.
    public IReadOnlyList<WorkerState> Roster { get { return sim.Roster; } }
    public WorkerState SlotAt(int i) { return sim.SlotAt(i); }
    public int Slots { get { return CampSimulation.Slots; } }
    public int Count { get { return sim.Roster.Count; } }                 // всего рабочих у игрока (для достижений)
    public int WorkingCount { get { return sim.WorkingCount; } }          // сколько стоит в слотах
    public int Logs { get { return sim.logs; } }
    public int Planks { get { return sim.planks; } }
    public int LogCap { get { return sim.LogCap; } }
    public int PlankCap { get { return sim.PlankCap; } }
    public float TotalIncome { get { return (float)sim.TotalClerkRate; } }   // монет/с от Приказчиков в слотах (без удачи и премий)
    public int TotalEarned { get { return totalEarned; } }
    public int LegacyWorkerCount { get { return legacyWorkerCount; } }
    public int PendingRefund { get { return pendingRefund; } }
    public WorkerState Find(string id) { return sim.Find(id); }

    // ------------------------------------------------------------ жизненный цикл
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("WorkerManager: на сцене уже есть менеджер («" + Instance.name + "»), лишний («" + name + "») удалён.");
            if (GetComponents<Component>().Length <= 2) Destroy(gameObject); else Destroy(this);
            return;
        }
        Instance = this;

        sim = new CampSimulation();
        packs = new WorkerPackSystem(sim, null, c => { if (c > 0 && FindPlayer()) inv.AddCoins(c); });   // монеты за дубликат макс. рабочего
        sim.Produced += p => { if (OnWorkerProduced != null) OnWorkerProduced(p); };
        sim.LevelUp += w =>
        {
            if (loadingNow) return;
            SaveSystem.RequestSave("workers");
            Telemetry.Track("worker_leveled", "id", w.id, "role", w.role.ToString(), "rarity", w.rarity.ToString(), "level", w.level, "player_level", PlayerStats.Level);
            if (OnWorkerLevelUp != null) OnWorkerLevelUp(w);
        };
        sim.StockChanged += () => { if (!loadingNow && OnCampStockChanged != null) OnCampStockChanged(); };
        sim.RosterChanged += () =>
        {
            if (loadingNow) return;
            SaveSystem.RequestSave("workers");
            if (OnCampStockChanged != null) OnCampStockChanged();       // вместимость склада зависит от слотов
            if (OnRosterChanged != null) OnRosterChanged();
        };
        RefreshExternalMult();

        SaveSystem.Register(this);   // если файл уже прочитан, секция «workers» применится прямо здесь (Load)
    }

    void OnEnable()
    {
        PetManager.Changed += OnExternalChanged; SaveSystem.Loaded += OnSaveLoaded;
        WorkerPackSystem.OnPackOpened -= TrackPack; WorkerPackSystem.OnPackOpened += TrackPack;
        GameEvents.QuestDone -= OnQuestDone; GameEvents.QuestDone += OnQuestDone;
        PlayerSkills.Changed -= OnExternalChanged; PlayerSkills.Changed += OnExternalChanged;   // «Хороший начальник»
    }

    void OnDisable()
    {
        PetManager.Changed -= OnExternalChanged; SaveSystem.Loaded -= OnSaveLoaded;
        WorkerPackSystem.OnPackOpened -= TrackPack;
        GameEvents.QuestDone -= OnQuestDone;
        PlayerSkills.Changed -= OnExternalChanged;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            SaveSystem.Unregister(this);
            Instance = null;
        }
    }

    void OnExternalChanged()
    {
        if (Instance != this) return;
        RefreshExternalMult();
        if (OnRosterChanged != null) OnRosterChanged();
    }

    // «Продолжить» применил секцию без событий: окна обновляем один раз
    void OnSaveLoaded()
    {
        if (Instance != this) return;
        RefreshExternalMult();
        if (OnRosterChanged != null) OnRosterChanged();
        if (OnCampStockChanged != null) OnCampStockChanged();
    }

    // ------------------------------------------------------------ телеметрия и подарочный пак
    void TrackPack(PackResult r)
    {
        if (Instance != this || r.def == null) return;
        Telemetry.Track("pack_opened",
            "pack", r.type.ToString(), "free", r.wasFree,
            "worker", r.def.id, "rarity", r.rarity.ToString(), "role", r.def.role.ToString(),
            "new", r.isNew, "xp", r.xpGained, "coins", r.coinsGained,
            "guarantee", r.guarantee.ToString(), "player_level", PlayerStats.Level);
    }

    // Глава «Палатка» закрыта: дарим первый пак (дубликата не будет: OfferFree срабатывает один раз за всю игру)
    void OnQuestDone(string id)
    {
        if (Instance != this || id != "tent") return;
        giftChecked = true;
        OfferFreePack();
    }

    // Подстраховка: палатка достроена, а подарок ещё не предлагали (старое сохранение, перезагрузка посреди главы)
    void CheckGift()
    {
        if (giftChecked || Time.unscaledTime < giftAt) return;
        giftAt = Time.unscaledTime + 1.5f;
        var story = StoryFlow.Instance;
        if (story == null || story.tent == null || !story.tent.IsFinished) return;
        giftChecked = true;
        OfferFreePack();
    }

    // доход Приказчиков × питомцы («Доход рабочих», GDD 4.10) × навык «Хороший начальник»
    void RefreshExternalMult()
    {
        float petMul = PetManager.Instance != null ? 1f + PetManager.Instance.Bonus.worker : 1f;
        float chiefMul = PlayerSkills.Has(SkillIds.BossChief) ? 1f + GameBalance.SkillChiefBonus : 1f;
        sim.coinMult = petMul * chiefMul;
    }

    void Update()
    {
        if (Instance != this) return;
        float dt = Time.deltaTime;                 // игровое время: на паузе (timeScale 0) равно нулю
        if (dt <= 0f) return;
        if (!FindPlayer()) return;                 // некому платить: время не копим

        CheckGift();
        PayRefund();

        dt = Mathf.Min(dt, Mathf.Max(0.01f, maxFrameStep));
        sim.Step(dt);

        int coins = sim.pendingCoins;
        if (coins > 0)
        {
            sim.pendingCoins = 0;
            inv.AddCoins(coins);                   // HUD сам покажет «+N» и подпрыгнет
            totalEarned += coins;
        }
    }

    // Компенсация за старых рабочих сверх трёх и запись секции после миграции: один раз, когда есть кому платить
    void PayRefund()
    {
        if (pendingRefund > 0)
        {
            int c = pendingRefund;
            pendingRefund = 0;
            inv.AddCoins(c);
            Debug.Log("[Рабочие] Старые рабочие переведены в Подмастерьев, компенсация за лишних: " + c + " монет.");
            saveAfterLoad = true;
        }
        if (saveAfterLoad)
        {
            saveAfterLoad = false;
            SaveSystem.RequestSave("workers");
        }
    }

    // ------------------------------------------------------------ ростер
    // Выдать рабочего (из пака или отладки). Новый попадает в ростер и сам встаёт в свободный слот, если роль не заполнена.
    // Дубликат НЕ добавляется: опыт или монеты за него решает система паков (AddXp / IsMax).
    public GrantResult Grant(WorkerDefinition def, out WorkerState state)
    {
        state = null;
        if (def == null || string.IsNullOrEmpty(def.id)) return GrantResult.Invalid;

        var ex = sim.Find(def.id);
        if (ex != null) { state = ex; return GrantResult.Duplicate; }

        state = sim.AddToRoster(WorkerState.From(def));
        sim.AutoAssign(state);
        SaveSystem.RequestSave("workers");
        return GrantResult.Added;
    }

    public AssignResult Assign(string workerId, int slot) { return sim.Assign(sim.Find(workerId), slot); }
    public AssignResult AutoAssign(string workerId) { return sim.AutoAssign(sim.Find(workerId)); }
    public void Unassign(int slot) { sim.Unassign(slot); }

    // Опыт рабочему не из работы (дубликаты паков). Возвращает, сколько уровней получено
    public int AddXp(string workerId, double xp)
    {
        var w = sim.Find(workerId);
        return w == null ? 0 : sim.AddXp(w, xp);
    }

    // ------------------------------------------------------------ склад лагеря: «Забрать» и «Продать −20%»
    // Сколько заберёт «Забрать» прямо сейчас: бревна «сколько влезет в рюкзак», доски все
    public CampTakeResult PreviewTake()
    {
        var r = new CampTakeResult();
        if (!FindPlayer()) return r;
        r.logs = Mathf.Min(sim.logs, inv.FreeSlots);
        r.planks = sim.planks;
        return r;
    }

    public CampTakeResult TakeAll()
    {
        var r = PreviewTake();
        if (!r.Any) return r;

        if (r.logs > 0)
        {
            int added = inv.AddWood(r.logs);
            sim.TakeLogs(added);
            r.logs = added;
        }
        if (r.planks > 0)
        {
            inv.AddPlanks(r.planks);
            sim.TakePlanks(r.planks);
        }
        SaveSystem.RequestSave("workers");
        return r;
    }

    // Сколько монет даст «Продать»: цена из Pricing (с «Торгом») × (1 − 20%), округление по каждой позиции.
    // Бонусы всей продажи (Оптовик, Базарный день) не применяются: это продажа «на месте» с потерей.
    public CampSellResult PreviewSell()
    {
        var r = new CampSellResult { logs = sim.logs, planks = sim.planks };
        r.coins = Mathf.RoundToInt(Pricing.Price(SellItem.Log, r.logs) * WorkerBalance.SellRate)
                + Mathf.RoundToInt(Pricing.Price(SellItem.Plank, r.planks) * WorkerBalance.SellRate);
        return r;
    }

    public CampSellResult SellAll()
    {
        var r = PreviewSell();
        if (!r.Any || !FindPlayer()) return default(CampSellResult);

        sim.TakeLogs(r.logs);
        sim.TakePlanks(r.planks);
        if (r.coins > 0) inv.AddCoins(r.coins);
        SaveSystem.RequestSave("workers");
        return r;
    }

    // ------------------------------------------------------------ паки (задача 4)
    // Открыт ли пак: по уровню игрока (Артельный 3, Мастерской 5, Гильдейский 7). Подарочный Артельный открыт всегда
    public bool IsPackUnlocked(WorkerPackType t)
    {
        if (debugIgnoreLevelLock) return true;
        if (t == WorkerPackType.Artel && packs.FreePending) return true;
        return PlayerStats.Level >= WorkerBalance.PackLevel(t);
    }

    // Цена с учётом подарочного пака (0 = бесплатно)
    public int PackPrice(WorkerPackType t) { return packs.Price(t); }

    public bool CanOpenPack(WorkerPackType t, out PackOpenResult why)
    {
        why = PackOpenResult.Ok;
        if (database == null || database.workers == null || database.workers.Count == 0) { why = PackOpenResult.NoDatabase; return false; }
        if (!FindPlayer()) { why = PackOpenResult.NoPlayer; return false; }
        if (!IsPackUnlocked(t)) { why = PackOpenResult.Locked; return false; }
        if (inv.coinsCount < packs.Price(t)) { why = PackOpenResult.NoCoins; return false; }
        return true;
    }

    // Открыть пак: списывает монеты, бросает (с гарантиями), выдаёт рабочего или опыт/монеты за дубликат, сохраняет.
    // Результат известен сразу, UI только красиво его показывает. Событие WorkerPackSystem.OnPackOpened приходит уже после применения.
    public PackOpenResult TryOpenPack(WorkerPackType type, out PackResult result)
    {
        result = default(PackResult);
        PackOpenResult why;
        if (!CanOpenPack(type, out why)) return why;

        int price = packs.Price(type);
        if (price > 0)
        {
            int before = inv.coinsCount;
            inv.AddCoins(-price);
            if (before - inv.coinsCount != price)          // списание проверяем по факту
            {
                if (before - inv.coinsCount > 0) inv.AddCoins(before - inv.coinsCount);
                return PackOpenResult.NoCoins;
            }
        }

        if (!packs.Open(type, database, out result))
        {
            if (price > 0) inv.AddCoins(price);            // база пустая: возвращаем монеты
            return PackOpenResult.NoDatabase;
        }
        SaveSystem.RequestSave("workers");
        return PackOpenResult.Ok;
    }

    // Режим «выбери одну из трёх»: карты бросаются сразу, но монеты НЕ списываются и ничего не применяется.
    public PackOpenResult TryDraftPack(WorkerPackType type, out PackDraft draft)
    {
        draft = null;
        PackOpenResult why;
        if (!CanOpenPack(type, out why)) return why;
        if (!packs.Draft(type, database, out draft)) return PackOpenResult.NoDatabase;
        return PackOpenResult.Ok;
    }

    // Игрок выбрал карту: списываем монеты, применяем только её, сохраняем. Не вышло (не хватает монет): ничего не изменено.
    public PackOpenResult TryPickCard(PackDraft draft, int index, out PackResult result)
    {
        result = default(PackResult);
        if (draft == null) return PackOpenResult.NoDatabase;
        PackOpenResult why;
        if (!CanOpenPack(draft.type, out why)) return why;

        int price = packs.Price(draft.type);
        if (price > 0)
        {
            int before = inv.coinsCount;
            inv.AddCoins(-price);
            if (before - inv.coinsCount != price)
            {
                if (before - inv.coinsCount > 0) inv.AddCoins(before - inv.coinsCount);
                return PackOpenResult.NoCoins;
            }
        }

        if (!packs.Pick(draft, index, out result))
        {
            if (price > 0) inv.AddCoins(price);
            return PackOpenResult.NoDatabase;
        }
        SaveSystem.RequestSave("workers");
        return PackOpenResult.Ok;
    }

    // Сюжет (глава 7 «Палатка») дарит первый Артельный пак. Один раз за всё время
    public void OfferFreePack()
    {
        if (!packs.OfferFree()) return;
        SaveSystem.RequestSave("workers");
        if (OnRosterChanged != null) OnRosterChanged();    // окна обновят цену и кнопку
    }

    // ------------------------------------------------------------ отладка (контекстное меню компонента в Play Mode)
    void DebugOpen(WorkerPackType t)
    {
        PackResult r;
        PackOpenResult res = TryOpenPack(t, out r);
        if (res != PackOpenResult.Ok) { Debug.Log("[Рабочие] " + WorkerText.Pack(t) + " не открыт: " + res); return; }
        Debug.Log("[Рабочие] " + WorkerText.Pack(t) + (r.wasFree ? " (подарок)" : "") + ": «" + r.def.FullName + "» (" + WorkerText.Rarity(r.rarity) + ", " + WorkerText.Role(r.def.role) + ")"
            + (r.isNew ? " НОВЫЙ" : r.coinsGained > 0 ? " дубликат, макс. уровень: +" + r.coinsGained + " монет" : " дубликат: +" + r.xpGained + " XP" + (r.levelsGained > 0 ? ", уровней +" + r.levelsGained : ""))
            + (r.GuaranteeTriggered ? ". СРАБОТАЛА ГАРАНТИЯ: " + r.guarantee : "")
            + ". До Редкого: " + packs.OpensUntilRare(t)
            + (WorkerBalance.HasLegendaryGuarantee(t) ? ", до Легендарного: " + packs.OpensUntilLegendary(t) : "")
            + (WorkerBalance.HasMythicGuarantee(t) ? ", до мифика: " + packs.MythicProgress(t) + "/" + WorkerBalance.GuaranteeMythicEvery : ""));
    }

    [ContextMenu("Тест: открыть Артельный пак")] void DebugArtel() { DebugOpen(WorkerPackType.Artel); }
    [ContextMenu("Тест: открыть Мастерской пак")] void DebugWorkshop() { DebugOpen(WorkerPackType.Workshop); }
    [ContextMenu("Тест: открыть Гильдейский пак")] void DebugGuild() { DebugOpen(WorkerPackType.Guild); }
    [ContextMenu("Тест: предложить подарочный пак")] void DebugOfferFree() { OfferFreePack(); Debug.Log("[Рабочие] Подарочный пак: " + (packs.FreePending ? "ждёт открытия" : packs.FreeUsed ? "уже был" : "не предложен")); }
    [ContextMenu("Тест: +5000 монет")] void DebugCoins() { if (FindPlayer()) inv.AddCoins(5000); }

    [ContextMenu("Отладка: выдать Обычного и Мифического каждой роли")]
    void DebugGrantTeam()
    {
        if (database == null) { Debug.LogWarning("WorkerManager: не назначена база рабочих."); return; }
        var list = new List<WorkerDefinition>();
        for (int r = 0; r < 3; r++)
        {
            database.Collect((WorkerRole)r, WorkerRarity.Common, list);
            WorkerState s; if (list.Count > 0) Grant(list[0], out s);
            database.Collect((WorkerRole)r, WorkerRarity.Mythic, list);
            if (list.Count > 0) Grant(list[0], out s);
        }
    }

    // ------------------------------------------------------------ smoke-тест сохранения (только Play Mode, состояние игры возвращается)
    // Проверяет: сохранение и загрузка дают одно и то же состояние, в том числе пока пильщик пилит (бревна уже взяты);
    // миграцию старого сохранения (5 рабочих: 3 Подмастерья и компенсация за двух); повторную загрузку без дублей.
    [ContextMenu("Тест: сохранение и миграция (smoke)")]
    void DebugSmokeSave()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[Рабочие] smoke-тест запускается в Play Mode."); return; }
        if (database == null) { Debug.LogWarning("[Рабочие] smoke-тест: не назначена база рабочих."); return; }

        string backup = Save();
        bool wasLoading = loadingNow;
        int fails = 0;
        var log = new System.Text.StringBuilder("[Рабочие] smoke-тест сохранения:\n");
        Action<bool, string> check = (ok, what) => { log.Append(ok ? "  OK   " : "  ОШИБКА  ").Append(what).Append('\n'); if (!ok) fails++; };

        loadingNow = true;      // пока тест: ни событий, ни RequestSave, ни выплат
        try
        {
            // 1. команда: Обычный и Мифический каждой роли, склад с бревнами, чтобы пильщик начал пилить
            ResetToDefault();
            var list = new List<WorkerDefinition>();
            for (int r = 0; r < 3; r++)
            {
                database.Collect((WorkerRole)r, WorkerRarity.Common, list);
                if (list.Count > 0) sim.AutoAssign(sim.AddToRoster(WorkerState.From(list[0])));
                database.Collect((WorkerRole)r, WorkerRarity.Mythic, list);
                if (list.Count > 0) sim.AutoAssign(sim.AddToRoster(WorkerState.From(list[0])));
            }
            sim.logs = 20;
            for (int i = 0; i < 100; i++) sim.Step(0.25f);       // 25 с игры
            sim.pendingCoins = 0;
            packs.state.sinceRare[0] = 4; packs.state.sinceLegendary[2] = 17; packs.state.sinceMythic[2] = 63; packs.state.freePending = true;

            bool sawing = false;
            for (int i = 0; i < sim.Roster.Count; i++) if (sim.Roster[i].role == WorkerRole.Sawyer && sim.Roster[i].sawActive) sawing = true;
            check(sim.Roster.Count == 6, "в ростере 6 рабочих (" + sim.Roster.Count + ")");
            check(sawing, "пильщик в работе в момент сохранения");

            string j1 = Save();
            LoadInner(j1);
            string j2 = Save();
            check(j1 == j2, "сохранить, загрузить, сохранить: файл тот же");
            check(sim.Roster.Count == 6 && sim.WorkingCount == 6, "после загрузки 6 рабочих, все в слотах");
            check(packs.state.sinceMythic[2] == 63 && packs.state.freePending, "счётчик «до мифика 63/100» и подарочный пак сохранились");

            // 2. миграция старого сохранения: пять цифровых рабочих
            const string legacy = "{\"version\":1,\"workers\":[{\"id\":1,\"baseIncome\":0.4},{\"id\":2,\"baseIncome\":0.4},{\"id\":3,\"baseIncome\":0.4},{\"id\":4,\"baseIncome\":0.4},{\"id\":5,\"baseIncome\":0.4}]}";
            LoadInner(legacy);
            int expectRefund = GameBalance.WorkerCost(4) + GameBalance.WorkerCost(5);
            check(sim.Roster.Count == 3 && sim.CountInSlots(WorkerRole.Clerk) == 3, "миграция: 3 Подмастерья, все в слотах");
            check(pendingRefund == expectRefund, "миграция: компенсация за двух лишних " + pendingRefund + " (ожидали " + expectRefund + ")");
            check(sim.TotalClerkRate >= 3 * GameBalance.WorkerIncome, "миграция: доход Подмастерьев не меньше трёх старых рабочих (" + sim.TotalClerkRate.ToString("0.00") + " монет/с)");

            string j3 = Save();
            LoadInner(j3);
            check(sim.Roster.Count == 3 && pendingRefund == expectRefund, "после записи новой версии и загрузки: без дублей, компенсация на месте");
        }
        catch (Exception e) { fails++; log.Append("  ИСКЛЮЧЕНИЕ  ").Append(e.Message).Append('\n'); }
        finally
        {
            LoadInner(backup);          // вернуть игру как была
            sim.pendingCoins = 0;
            saveAfterLoad = false;
            loadingNow = wasLoading;
            if (OnRosterChanged != null) OnRosterChanged();
            if (OnCampStockChanged != null) OnCampStockChanged();
        }
        log.Append(fails == 0 ? "ИТОГ: всё в порядке." : "ИТОГ: ошибок " + fails + ".");
        if (fails == 0) Debug.Log(log.ToString()); else Debug.LogError(log.ToString());
    }

    // ------------------------------------------------------------ сохранение (ISaveable, секция «workers», версия 2)
    // ДОБАВЛЯТЬ ПОЛЯ ТОЛЬКО С ДЕФОЛТАМИ. Версия 1 (старые цифровые рабочие) переносится в «Подмастерьев» (MigrateLegacy).
    [Serializable]
    class SavedWorker
    {
        public string id;
        public float totalXp, coinAcc, logAcc, clerkTick, bonusTimer, goldenTimer, sawProgress;
        public bool sawActive, sawPaid;
    }

    [Serializable]
    class SaveData
    {
        public int version = 2;
        public List<SavedWorker> workers = new List<SavedWorker>();
        public string[] slots = new string[CampSimulation.Slots];
        public int logs, planks, totalEarned;
        public WorkerPackState packs = new WorkerPackState();   // счётчики гарантий и подарочный пак (поле с дефолтом: старые файлы читаются)
        public int legacyRefund;                                 // невыплаченная компенсация за миграцию (поле с дефолтом)
    }

    [Serializable] class LegacyWorker { public int id; public float baseIncome; }
    [Serializable] class LegacySave { public int version; public List<LegacyWorker> workers = new List<LegacyWorker>(); }
    [Serializable] class VersionProbe { public int version; }

    public string SaveKey { get { return "workers"; } }
    public int LoadOrder { get { return 40; } }

    public string Save()
    {
        var d = new SaveData { logs = sim.logs, planks = sim.planks, totalEarned = totalEarned };
        d.packs = packs.state;
        d.legacyRefund = pendingRefund;
        for (int i = 0; i < sim.Roster.Count; i++)
        {
            var w = sim.Roster[i];
            d.workers.Add(new SavedWorker
            {
                id = w.id, totalXp = (float)w.totalXp, coinAcc = (float)w.coinAcc, logAcc = (float)w.logAcc, clerkTick = (float)w.clerkTick,
                bonusTimer = (float)w.bonusTimer, goldenTimer = (float)w.goldenTimer, sawProgress = (float)w.sawProgress,
                sawActive = w.sawActive, sawPaid = w.sawPaid
            });
        }
        for (int i = 0; i < CampSimulation.Slots; i++)
        {
            var w = sim.SlotAt(i);
            d.slots[i] = w != null ? w.id : "";
        }
        return JsonUtility.ToJson(d);
    }

    // Без событий, наград и RequestSave (правило ISaveable)
    public void Load(string json, int formatVersion)
    {
        loadingNow = true;
        try { LoadInner(json); }
        finally { loadingNow = false; }
    }

    void LoadInner(string json)
    {
        ResetToDefault();
        if (string.IsNullOrEmpty(json)) return;

        VersionProbe probe = null;
        try { probe = JsonUtility.FromJson<VersionProbe>(json); }
        catch (Exception e) { Debug.LogWarning("WorkerManager: не удалось прочитать сохранение рабочих: " + e.Message); }
        if (probe == null) return;

        if (probe.version < 2)
        {
            // старые цифровые рабочие: превращаем в Обычных Приказчиков «Подмастерьев» (доход не падает)
            try { var old = JsonUtility.FromJson<LegacySave>(json); legacyWorkerCount = old != null && old.workers != null ? old.workers.Count : 0; }
            catch { legacyWorkerCount = 0; }
            MigrateLegacy(legacyWorkerCount);
            return;
        }

        SaveData d = null;
        try { d = JsonUtility.FromJson<SaveData>(json); }
        catch (Exception e) { Debug.LogWarning("WorkerManager: не удалось прочитать сохранение рабочих: " + e.Message); }
        if (d == null || d.workers == null) return;

        if (database == null) Debug.LogWarning("WorkerManager: не назначена база рабочих, сохранённые рабочие не восстановлены.");
        else
        {
            for (int i = 0; i < d.workers.Count; i++)
            {
                var sw = d.workers[i];
                if (sw == null) continue;
                var def = database.Find(sw.id) ?? WorkerApprentice.Find(sw.id);
                if (def == null) { Debug.LogWarning("WorkerManager: рабочий «" + sw.id + "» не найден в базе, пропущен."); continue; }

                var s = WorkerState.From(def);
                s.totalXp = Math.Max(0.0, Math.Min(sw.totalXp, WorkerBalance.TotalXpForLevel(WorkerBalance.MaxLevel)));
                s.level = WorkerBalance.LevelFromTotalXp(s.totalXp);
                s.coinAcc = Math.Max(0.0, sw.coinAcc); s.logAcc = Math.Max(0.0, sw.logAcc); s.clerkTick = Math.Max(0.0, sw.clerkTick);
                s.bonusTimer = Math.Max(0.0, sw.bonusTimer); s.goldenTimer = Math.Max(0.0, sw.goldenTimer);
                s.sawActive = sw.sawActive; s.sawPaid = sw.sawPaid && sw.sawActive; s.sawProgress = sw.sawActive ? Math.Max(0.0, sw.sawProgress) : 0.0;
                sim.AddToRoster(s);
            }
            if (d.slots != null)
                for (int i = 0; i < d.slots.Length && i < CampSimulation.Slots; i++)
                    if (!string.IsNullOrEmpty(d.slots[i])) sim.Assign(sim.Find(d.slots[i]), i);
        }

        packs.state.CopyFrom(d.packs);
        pendingRefund = Mathf.Max(0, d.legacyRefund);
        sim.logs = Mathf.Max(0, d.logs);
        sim.planks = Mathf.Max(0, d.planks);
        totalEarned = Mathf.Max(0, d.totalEarned);
        RefreshExternalMult();
    }

    // Было n цифровых рабочих (до 6, по 0.4 монеты/с). Теперь: до трёх Подмастерьев (Приказчик Обычный, 0.45 монеты/с: не меньше прежнего),
    // каждый встаёт в слот. Лишних сверх трёх (роль ограничена тремя) возвращаем монетами: цена найма по старой формуле GameBalance.WorkerCost.
    // Вызывается из Load: событий, наград и RequestSave здесь нет, всё доделает PayRefund в Update.
    void MigrateLegacy(int n)
    {
        n = Mathf.Clamp(n, 0, GameBalance.MaxWorkers);
        int keep = Mathf.Min(n, WorkerApprentice.Count);
        for (int i = 0; i < keep; i++)
        {
            var s = sim.AddToRoster(WorkerState.From(WorkerApprentice.Get(i)));
            sim.AutoAssign(s);
        }
        int refund = 0;
        for (int k = keep + 1; k <= n; k++) refund += GameBalance.WorkerCost(k);
        pendingRefund = refund;
        saveAfterLoad = n > 0;
        Debug.Log("[Рабочие] Миграция старого сохранения: было " + n + ", Подмастерьев " + keep + (refund > 0 ? ", компенсация " + refund + " монет" : "") + ".");
    }

    // чистая игра: «Новая игра», битое сохранение, секция без данных
    public void ResetToDefault()
    {
        sim.ResetAll();
        packs.ResetAll();
        totalEarned = 0;
        legacyWorkerCount = 0;
        pendingRefund = 0;
        saveAfterLoad = false;
        giftChecked = false;
        if (!loadingNow)
        {
            if (OnRosterChanged != null) OnRosterChanged();
            if (OnCampStockChanged != null) OnCampStockChanged();
        }
    }

    // ------------------------------------------------------------ игрок
    bool FindPlayer()
    {
        if (inv != null) return true;
        if (Time.unscaledTime < findAt) return false;
        findAt = Time.unscaledTime + 0.5f;
        inv = PlayerInventory.Instance;
        return inv != null;
    }
}
