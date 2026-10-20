// Assets/_Project/Scripts/Workers/WorkerPackSystem.cs (НЕ в Editor)
// Система паков рабочих (дизайн «Система рабочих», задача 4). Обычный C#-класс без сцены: его создаёт WorkerManager,
// тесты создают сами. Делает: бросок пака (WorkerPackRoller) → выдача рабочего в ростер → обработка дубликата
// (опыт существующему рабочему, а на максимальном уровне монеты) → событие OnPackOpened.
// НЕ делает: не списывает монеты, не проверяет уровень игрока, не сохраняет файл: это обвязка WorkerManager.TryOpenPack.
// Монеты за дубликат на максимальном уровне отдаются через coinSink (в игре это инвентарь; в тесте null: сумма только в результате).
using System;

// Что вышло из пака (для окна открытия: UI только красиво это показывает)
public struct PackResult
{
    public WorkerPackType type;
    public WorkerDefinition def;
    public WorkerState worker;          // рабочий в ростере (новый или уже был)
    public WorkerRarity rarity;
    public bool wasFree;                // подарочный пак
    public bool isNew;                  // новый рабочий в коллекции
    public bool isDuplicate;            // уже был: опыт или монеты
    public int xpGained;                // дубликат: выданный опыт (0 если рабочий на максимуме)
    public int levelsGained;            // дубликат: сколько уровней дал опыт
    public int coinsGained;             // дубликат на максимальном уровне: монеты вместо опыта
    public PackGuarantee guarantee;     // какая гарантия реально подняла результат (None = выпало само)
    public bool GuaranteeTriggered { get { return guarantee != PackGuarantee.None; } }
}

// Набор из трёх карт для режима «выбери одну из трёх» (рубашкой вверх). Все три карты НАСТОЯЩИЕ: каждая брошена отдельно
// по шансам пака, поэтому выбранная карта имеет те же шансы, что и обычный бросок. Ничего не применено, пока игрок не выбрал.
public sealed class PackDraft
{
    public const int Size = 3;
    public WorkerPackType type;
    public bool free;                                                        // подарочный пак (карты: разные Обычные Приказчики)
    public readonly WorkerDefinition[] defs = new WorkerDefinition[Size];
    public readonly PackGuarantee[] guarantee = new PackGuarantee[Size];     // какая гарантия подняла именно эту карту
}

public sealed class WorkerPackSystem
{
    // Пак открыт и всё уже применено (рабочий в ростере, опыт и монеты выданы). UI подписывается, система про UI не знает
    public static event Action<PackResult> OnPackOpened;

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { OnPackOpened = null; }

    readonly CampSimulation sim;
    readonly System.Random rng;
    readonly Action<int> coinSink;

    public readonly WorkerPackState state = new WorkerPackState();
    public bool usePity = true;         // false только в тестах «чистых» шансов

    public WorkerPackSystem(CampSimulation sim, System.Random rng = null, Action<int> coinSink = null)
    {
        this.sim = sim;
        this.rng = rng ?? new System.Random();
        this.coinSink = coinSink;
    }

    // ------------------------------------------------------------ счётчики для окна пака (показываются до покупки)
    public int OpensUntilRare(WorkerPackType t)
    {
        return Math.Max(1, WorkerBalance.GuaranteeRareEvery - state.sinceRare[(int)t]);
    }

    // Паков до гарантированного Легендарного+ (0 = у этого пака такой гарантии нет)
    public int OpensUntilLegendary(WorkerPackType t)
    {
        return WorkerBalance.HasLegendaryGuarantee(t) ? Math.Max(1, WorkerBalance.GuaranteeLegendaryEvery - state.sinceLegendary[(int)t]) : 0;
    }

    // Паков до гарантированного Мифика (0 = нет гарантии)
    public int OpensUntilMythic(WorkerPackType t)
    {
        return WorkerBalance.HasMythicGuarantee(t) ? Math.Max(1, WorkerBalance.GuaranteeMythicEvery - state.sinceMythic[(int)t]) : 0;
    }

    // «до мифика: 63/100»: сколько паков уже открыто без Мифика (0 = нет гарантии)
    public int MythicProgress(WorkerPackType t)
    {
        return WorkerBalance.HasMythicGuarantee(t) ? state.sinceMythic[(int)t] : 0;
    }

    // ------------------------------------------------------------ подарочный пак
    public bool FreePending { get { return state.freePending; } }
    public bool FreeUsed { get { return state.freeUsed; } }

    // Сюжет (глава 7 «Палатка») предлагает подарочный Артельный пак. Один раз за всё время
    public bool OfferFree()
    {
        if (state.freeUsed || state.freePending) return false;
        state.freePending = true;
        return true;
    }

    // Цена с учётом подарка (подарок только на Артельный пак)
    public int Price(WorkerPackType t)
    {
        if (t == WorkerPackType.Artel && state.freePending) return 0;
        return WorkerBalance.PackCost(t);
    }

    // ------------------------------------------------------------ открытие
    // Бросает пак и применяет результат. false только если база пустая (ничего не изменено).
    // Подарочный пак: Обычный Приказчик, счётчики гарантий не трогает (как бесплатная клеть питомцев).
    public bool Open(WorkerPackType type, WorkerDatabase db, out PackResult result)
    {
        result = default(PackResult);
        bool free = type == WorkerPackType.Artel && state.freePending;

        WorkerDefinition def;
        PackGuarantee guarantee = PackGuarantee.None;
        if (free) def = WorkerPackRoller.PickFree(db, rng);
        else
        {
            WorkerRarity wanted = WorkerPackRoller.RollRarity(type, state, rng, usePity, out guarantee);
            def = WorkerPackRoller.PickDefinition(db, wanted, rng);
        }
        if (def == null) return false;

        if (free) { state.freePending = false; state.freeUsed = true; }
        else WorkerPackRoller.Advance(type, state, def.rarity);   // считаем по реально выданной редкости

        result = Award(def, type, free);
        // гарантия засчитывается, только если выдана редкость, которую она требовала (на неполной базе её может не быть)
        result.guarantee = (!free && guarantee != PackGuarantee.None && Satisfied(guarantee, def.rarity)) ? guarantee : PackGuarantee.None;

        if (OnPackOpened != null) OnPackOpened(result);
        return true;
    }

    // ------------------------------------------------------------ выбор одной из трёх карт
    // Draft: бросает три карты и НИЧЕГО не меняет (ни счётчики, ни подарок, ни ростер). Брошено до выбора, поэтому закрыть окно
    // и открыть заново безопасно: карты рубашкой вверх, узнать из них ничего нельзя.
    // Если действует гарантия, она касается КАЖДОЙ карты набора: выбор не может «промахнуться» мимо гарантированной редкости.
    // Одинаковых персонажей в наборе стараемся не давать (редкость при этом не пересчитывается, шансы не меняются).
    public bool Draft(WorkerPackType type, WorkerDatabase db, out PackDraft draft)
    {
        draft = null;
        if (db == null) return false;
        bool free = type == WorkerPackType.Artel && state.freePending;
        var d = new PackDraft { type = type, free = free };

        for (int i = 0; i < PackDraft.Size; i++)
        {
            WorkerDefinition def = null;
            PackGuarantee g = PackGuarantee.None;
            WorkerRarity wanted = WorkerRarity.Common;
            if (!free) wanted = WorkerPackRoller.RollRarity(type, state, rng, usePity, out g);

            for (int attempt = 0; attempt < 8; attempt++)
            {
                def = free ? WorkerPackRoller.PickFree(db, rng) : WorkerPackRoller.PickDefinition(db, wanted, rng);
                if (def == null) return false;
                if (!InDraft(d, i, def)) break;
            }
            d.defs[i] = def;
            d.guarantee[i] = g;
        }
        draft = d;
        return true;
    }

    static bool InDraft(PackDraft d, int count, WorkerDefinition def)
    {
        for (int i = 0; i < count; i++) if (d.defs[i] == def) return true;
        return false;
    }

    // Pick: игрок выбрал карту. Применяет ТОЛЬКО её (рабочий в ростер или опыт/монеты за дубликат), двигает счётчики гарантий
    // по выбранной редкости и вызывает OnPackOpened. Остальные две карты пропадают (UI показывает их как «могло выпасть»).
    public bool Pick(PackDraft draft, int index, out PackResult result)
    {
        result = default(PackResult);
        if (draft == null || index < 0 || index >= PackDraft.Size || draft.defs[index] == null) return false;

        var def = draft.defs[index];
        bool free = draft.free && draft.type == WorkerPackType.Artel && state.freePending;
        if (free) { state.freePending = false; state.freeUsed = true; }
        else WorkerPackRoller.Advance(draft.type, state, def.rarity);

        result = Award(def, draft.type, free);
        PackGuarantee g = draft.guarantee[index];
        result.guarantee = (!free && g != PackGuarantee.None && Satisfied(g, def.rarity)) ? g : PackGuarantee.None;

        if (OnPackOpened != null) OnPackOpened(result);
        return true;
    }

    static bool Satisfied(PackGuarantee g, WorkerRarity r)
    {
        switch (g)
        {
            case PackGuarantee.Mythic: return r >= WorkerRarity.Mythic;
            case PackGuarantee.Legendary: return r >= WorkerRarity.Legendary;
            default: return r >= WorkerRarity.Rare;
        }
    }

    // Выдать результат: новый рабочий в ростер (встаёт в свободный слот, если роль не заполнена), дубликат даёт опыт или монеты.
    // Публичный, чтобы награды сюжета и тесты могли выдать конкретного персонажа тем же путём. Событие НЕ вызывает.
    public PackResult Award(WorkerDefinition def, WorkerPackType type, bool free)
    {
        var r = new PackResult { type = type, def = def, rarity = def.rarity, wasFree = free };

        var ex = sim.Find(def.id);
        if (ex == null)
        {
            r.worker = sim.AddToRoster(WorkerState.From(def));
            sim.AutoAssign(r.worker);
            r.isNew = true;
            return r;
        }

        r.worker = ex;
        r.isDuplicate = true;
        if (ex.IsMax)
        {
            r.coinsGained = WorkerBalance.DuplicateCoinsFor(def.rarity);
            if (coinSink != null) coinSink(r.coinsGained);
        }
        else
        {
            int before = ex.level;
            r.xpGained = WorkerBalance.DuplicateXpFor(def.rarity);
            sim.AddXp(ex, r.xpGained);
            r.levelsGained = ex.level - before;
        }
        return r;
    }

    public void ResetAll() { state.Clear(); }
}
