// Assets/_Project/Scripts/Workers/CampSimulation.cs (НЕ в Editor)
// Чистая симуляция лагеря рабочих: ростер, 6 слотов, склад, тики производства, перки, опыт. Без сцены, без MonoBehaviour,
// без UI и без PlayerInventory, поэтому полностью проверяется тестом (Tools > BuilderSim > Рабочие > Проверить производство).
// Игровая обвязка (монеты игроку, «Забрать»/«Продать», сохранение, события для UI) живёт в WorkerManager.
//
// Правила (дизайн «Система рабочих»):
//   Приказчик: монеты тиками раз в секунду; монеты копятся в pendingCoins, менеджер выдаёт их игроку.
//   Дровосек: бревна на склад лагеря. Склад полон = стоит, опыта нет.
//   Пильщик: берёт 2 бревна со склада, через время делает доску. Нет бревен или склад досок полон = стоит, опыта нет.
//   Опыт: 0.5 XP/с, только пока рабочий реально производит. Работают только рабочие в слотах.
//   «Наставник»: +5% выработки всем рабочим, суммарно не больше +15%.
using System;
using System.Collections.Generic;

public enum CampResource { Coins = 0, Logs = 1, Planks = 2 }
public enum AssignResult { Ok, NotOwned, BadSlot, NoFreeSlot, RoleFull }

// Что только что произвёл рабочий (для всплывающих «+N» и тестов). special = сработал перк (удача, двойной, золотое дерево, премия)
public struct WorkerProduced
{
    public WorkerState worker;
    public CampResource resource;
    public int amount;
    public bool special;
}

// Состояние одного рабочего. Роль, редкость и перки скопированы из WorkerDefinition, поэтому логика не зависит от ассетов.
public class WorkerState
{
    public string id;
    public WorkerRole role;
    public WorkerRarity rarity;
    public WorkerPerk[] perks;
    public WorkerDefinition def;       // для UI (имя, портрет); в тестах может быть null

    public double totalXp;             // суммарный опыт от начала
    public int level = 1;

    // таймеры и накопления (сохраняются)
    public double coinAcc, logAcc, clerkTick, bonusTimer, goldenTimer, sawProgress;
    public bool sawActive;             // пила запущена: бревна уже взяты
    public bool sawPaid;               // бревна реально списаны («Экономный» мог не списать)

    public WorkerState(string id, WorkerRole role, WorkerRarity rarity, WorkerPerk[] perks)
    {
        this.id = id; this.role = role; this.rarity = rarity;
        this.perks = perks ?? new WorkerPerk[0];
    }

    public static WorkerState From(WorkerDefinition d)
    {
        var s = new WorkerState(d.id, d.role, d.rarity, d.perks != null ? d.perks.ToArray() : new WorkerPerk[0]);
        s.def = d;
        return s;
    }

    public bool Has(WorkerPerk p)
    {
        for (int i = 0; i < perks.Length; i++) if (perks[i] == p) return true;
        return false;
    }

    public bool IsMax { get { return level >= WorkerBalance.MaxLevel; } }
    public string DisplayName { get { return def != null ? def.FullName : id; } }
}

public sealed class CampSimulation
{
    public const int Slots = WorkerBalance.CampSlots;
    const double Eps = 1e-9;

    readonly WorkerState[] slots = new WorkerState[Slots];
    readonly List<WorkerState> roster = new List<WorkerState>();
    readonly Random rng;

    public int logs, planks;                       // склад лагеря
    public int pendingCoins;                       // монеты Приказчиков, ещё не выданные игроку (забирает менеджер)
    public float coinMult = 1f;                    // внешний множитель дохода Приказчиков (питомцы, навыки): считает менеджер
    public bool stackAcrossRole = WorkerBalance.PerksStackAcrossRole;

    public event Action<WorkerProduced> Produced;
    public event Action<WorkerState> LevelUp;
    public event Action StockChanged;
    public event Action RosterChanged;             // состав ростера или слотов изменился

    bool stockDirty;

    public CampSimulation(Random rng = null) { this.rng = rng ?? new Random(); }

    // ====================================================== ростер и слоты
    public IReadOnlyList<WorkerState> Roster { get { return roster; } }
    public WorkerState SlotAt(int i) { return (i >= 0 && i < Slots) ? slots[i] : null; }

    public WorkerState Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        for (int i = 0; i < roster.Count; i++) if (roster[i].id == id) return roster[i];
        return null;
    }

    public int SlotOf(WorkerState w)
    {
        for (int i = 0; i < Slots; i++) if (slots[i] == w) return i;
        return -1;
    }

    public int CountInSlots(WorkerRole role)
    {
        int n = 0;
        for (int i = 0; i < Slots; i++) if (slots[i] != null && slots[i].role == role) n++;
        return n;
    }

    public int WorkingCount { get { int n = 0; for (int i = 0; i < Slots; i++) if (slots[i] != null) n++; return n; } }

    // Добавить в ростер (коллекцию). Повторный id не добавляется: возвращает уже существующего
    public WorkerState AddToRoster(WorkerState w)
    {
        var ex = Find(w.id);
        if (ex != null) return ex;
        roster.Add(w);
        if (RosterChanged != null) RosterChanged();
        return w;
    }

    public AssignResult CanAssign(WorkerState w, int slot)
    {
        if (w == null || !roster.Contains(w)) return AssignResult.NotOwned;
        if (slot < 0 || slot >= Slots) return AssignResult.BadSlot;
        int same = 0;
        for (int i = 0; i < Slots; i++)
        {
            var o = slots[i];
            if (o == null || o == w || i == slot) continue;   // сам рабочий и вытесняемый из этого слота не считаются
            if (o.role == w.role) same++;
        }
        return same >= WorkerBalance.MaxPerRole ? AssignResult.RoleFull : AssignResult.Ok;
    }

    public AssignResult Assign(WorkerState w, int slot)
    {
        var r = CanAssign(w, slot);
        if (r != AssignResult.Ok) return r;

        int old = SlotOf(w);
        if (old == slot) return AssignResult.Ok;
        if (old >= 0) slots[old] = null;
        if (slots[slot] != null) Release(slots[slot]);      // вытесненный уходит в запас
        slots[slot] = w;
        Raise();
        return AssignResult.Ok;
    }

    // В первый свободный слот (если уже в слоте: Ok)
    public AssignResult AutoAssign(WorkerState w)
    {
        if (w == null || !roster.Contains(w)) return AssignResult.NotOwned;
        if (SlotOf(w) >= 0) return AssignResult.Ok;
        for (int i = 0; i < Slots; i++)
            if (slots[i] == null) return Assign(w, i);
        return AssignResult.NoFreeSlot;
    }

    public void Unassign(int slot)
    {
        if (slot < 0 || slot >= Slots || slots[slot] == null) return;
        var w = slots[slot];
        slots[slot] = null;
        Release(w);
        Raise();
    }

    // Рабочий ушёл со слота посреди распила: списанные бревна возвращаются на склад, ничего не теряется
    void Release(WorkerState w)
    {
        if (w.sawActive)
        {
            if (w.sawPaid) { logs += WorkerBalance.SawyerLogsPerPlank; stockDirty = true; }
            w.sawActive = false; w.sawPaid = false; w.sawProgress = 0;
        }
        FlushStock();
    }

    void Raise() { if (RosterChanged != null) RosterChanged(); }

    // Полный сброс (новая игра, загрузка): события не вызываются, их сообщит менеджер
    public void ResetAll()
    {
        for (int i = 0; i < Slots; i++) slots[i] = null;
        roster.Clear();
        logs = 0; planks = 0; pendingCoins = 0; stockDirty = false;
    }

    // ====================================================== склад
    public int LogCap
    {
        get
        {
            int cap = WorkerBalance.BaseLogStock;
            for (int i = 0; i < Slots; i++) if (slots[i] != null && slots[i].Has(WorkerPerk.Roomy)) cap += WorkerBalance.RoomyLogs;
            return cap;
        }
    }

    public int PlankCap
    {
        get
        {
            int cap = WorkerBalance.BasePlankStock;
            for (int i = 0; i < Slots; i++) if (slots[i] != null && slots[i].Has(WorkerPerk.Stocker)) cap += WorkerBalance.StockerPlanks;
            return cap;
        }
    }

    public int TakeLogs(int max)
    {
        int n = Math.Max(0, Math.Min(max, logs));
        if (n > 0) { logs -= n; stockDirty = true; FlushStock(); }
        return n;
    }

    public int TakePlanks(int max)
    {
        int n = Math.Max(0, Math.Min(max, planks));
        if (n > 0) { planks -= n; stockDirty = true; FlushStock(); }
        return n;
    }

    void FlushStock()
    {
        if (!stockDirty) return;
        stockDirty = false;
        if (StockChanged != null) StockChanged();
    }

    // ====================================================== эффективные значения (для симуляции, UI и тестов)
    // Сумма значения перка: у своего владельца или по всем рабочим роли в лагере (stackAcrossRole)
    float PerkSum(WorkerState w, WorkerPerk perk, float value)
    {
        if (!stackAcrossRole) return w.Has(perk) ? value : 0f;
        float sum = 0f;
        for (int i = 0; i < Slots; i++)
            if (slots[i] != null && slots[i].role == w.role && slots[i].Has(perk)) sum += value;
        return sum;
    }

    // «Наставник»: +5% каждый, суммарно не больше +15%, действует на всех
    public float MentorBonus
    {
        get
        {
            float sum = 0f;
            for (int i = 0; i < Slots; i++) if (slots[i] != null && slots[i].Has(WorkerPerk.Mentor)) sum += WorkerBalance.MentorIncome;
            return WorkerBalance.CapMentor(sum);
        }
    }

    public double ClerkRate(WorkerState w)    // монет/с
    {
        double r = WorkerBalance.ClerkIncome(w.rarity, w.level)
                 * (1.0 + PerkSum(w, WorkerPerk.GoodDeal, WorkerBalance.GoodDealIncome) + MentorBonus) * coinMult;
        return Math.Round(r, 6);               // 0.45f даёт 0.4499999..., округляем шум float
    }

    public double LumberRate(WorkerState w)   // бревен/с
    {
        float heavy = WorkerBalance.CapSpeed(PerkSum(w, WorkerPerk.HeavyHand, WorkerBalance.HeavyHandSpeed));
        return Math.Round(WorkerBalance.LumberjackRate(w.rarity, w.level, heavy) * (1.0 + MentorBonus), 6);
    }

    public double SawTime(WorkerState w)      // секунд на доску
    {
        float cut = WorkerBalance.CapSpeed(PerkSum(w, WorkerPerk.FastSaw, WorkerBalance.FastSawTime));
        return Math.Round(WorkerBalance.SawyerTime(w.rarity, w.level, cut) / (1.0 + MentorBonus), 6);
    }

    public float LuckyChance(WorkerState w) { return WorkerBalance.CapChance(PerkSum(w, WorkerPerk.LuckyDeal, WorkerBalance.LuckyDealChance)); }
    public float DoubleStrikeChance(WorkerState w) { return WorkerBalance.CapChance(PerkSum(w, WorkerPerk.DoubleStrike, WorkerBalance.DoubleStrikeChance)); }
    public float DoublePlankChance(WorkerState w) { return WorkerBalance.CapChance(PerkSum(w, WorkerPerk.DoublePlank, WorkerBalance.DoublePlankChance)); }
    public float ThriftyChance(WorkerState w) { return WorkerBalance.CapChance(PerkSum(w, WorkerPerk.Thrifty, WorkerBalance.ThriftyChance)); }

    // Суммарный «паспортный» доход Приказчиков в слотах, монет/с (без удачи и премий): для строки в окне
    public double TotalClerkRate
    {
        get
        {
            double sum = 0;
            for (int i = 0; i < Slots; i++) if (slots[i] != null && slots[i].role == WorkerRole.Clerk) sum += ClerkRate(slots[i]);
            return sum;
        }
    }

    bool Roll(float chance) { return chance > 0f && rng.NextDouble() < chance; }

    void Emit(WorkerState w, CampResource res, int amount, bool special)
    {
        if (Produced != null) Produced(new WorkerProduced { worker = w, resource = res, amount = amount, special = special });
    }

    // ====================================================== опыт
    // Добавляет опыт (с потолком на максимальном уровне). Возвращает, сколько уровней получено. Событие LevelUp один раз за вызов
    public int AddXp(WorkerState w, double amount)
    {
        if (w == null || amount <= 0 || w.IsMax) return 0;
        double max = WorkerBalance.TotalXpForLevel(WorkerBalance.MaxLevel);
        w.totalXp = Math.Min(max, w.totalXp + amount);
        int before = w.level;
        w.level = WorkerBalance.LevelFromTotalXp(w.totalXp);
        int gained = w.level - before;
        if (gained > 0 && LevelUp != null) LevelUp(w);
        return gained;
    }

    // ====================================================== шаг времени
    public void Step(float dtF)
    {
        double dt = dtF;
        if (dt <= 0) return;

        // порядок: дровосеки, пильщики, приказчики (свежие бревна доступны пильщику уже в этом шаге)
        for (int i = 0; i < Slots; i++) if (slots[i] != null && slots[i].role == WorkerRole.Lumberjack) StepLumberjack(slots[i], dt);
        for (int i = 0; i < Slots; i++) if (slots[i] != null && slots[i].role == WorkerRole.Sawyer) StepSawyer(slots[i], dt);
        for (int i = 0; i < Slots; i++) if (slots[i] != null && slots[i].role == WorkerRole.Clerk) StepClerk(slots[i], dt);
        FlushStock();
    }

    void StepClerk(WorkerState w, double dt)
    {
        double rate = ClerkRate(w);
        float luckyChance = LuckyChance(w);
        int mult = (w.rarity == WorkerRarity.Mythic && w.Has(WorkerPerk.LuckyDeal)) ? WorkerBalance.SignatureLuckyMult : WorkerBalance.LuckyDealMult;
        double tick = WorkerBalance.ClerkTickSeconds;
        bool lucky = false;

        w.clerkTick += dt;
        while (w.clerkTick + Eps >= tick)
        {
            w.clerkTick -= tick;
            double amount = rate * tick;
            if (Roll(luckyChance)) { amount *= mult; lucky = true; }
            w.coinAcc += amount;
        }

        int bonus = 0;
        if (w.Has(WorkerPerk.Bonus))
        {
            w.bonusTimer += dt;
            while (w.bonusTimer + Eps >= WorkerBalance.BonusInterval) { w.bonusTimer -= WorkerBalance.BonusInterval; bonus += WorkerBalance.BonusCoins; }
        }

        int whole = (int)Math.Floor(w.coinAcc + Eps);
        if (whole > 0) w.coinAcc = Math.Max(0.0, w.coinAcc - whole);
        int total = whole + bonus;
        if (total > 0)
        {
            pendingCoins += total;
            Emit(w, CampResource.Coins, total, lucky || bonus > 0);
        }
        AddXp(w, dt * WorkerBalance.XpPerSecond);          // Приказчик производит всегда
    }

    void StepLumberjack(WorkerState w, double dt)
    {
        if (logs >= LogCap) { w.logAcc = Math.Min(w.logAcc, 1.0 - 2 * Eps); return; }   // склад полон: стоит, опыта нет

        w.logAcc += LumberRate(w) * dt;
        float dbl = DoubleStrikeChance(w);
        while (w.logAcc + Eps >= 1.0)
        {
            int space = LogCap - logs;
            if (space <= 0) { w.logAcc = Math.Min(w.logAcc, 1.0 - 2 * Eps); break; }
            w.logAcc -= 1.0;
            int n = 1; bool special = false;
            if (Roll(dbl)) { n = 2; special = true; }
            n = Math.Min(n, space);
            logs += n; stockDirty = true;
            Emit(w, CampResource.Logs, n, special);
        }

        if (w.Has(WorkerPerk.GoldenTree))
        {
            double interval = (w.rarity == WorkerRarity.Mythic) ? WorkerBalance.SignatureGoldenTreeInterval : WorkerBalance.GoldenTreeInterval;
            w.goldenTimer += dt;
            while (w.goldenTimer + Eps >= interval)
            {
                int space = LogCap - logs;
                if (space <= 0) { w.goldenTimer = interval; break; }       // пачка ждёт места на складе
                w.goldenTimer -= interval;
                int n = Math.Min(WorkerBalance.GoldenTreeLogs, space);
                logs += n; stockDirty = true;
                Emit(w, CampResource.Logs, n, true);
            }
        }
        AddXp(w, dt * WorkerBalance.XpPerSecond);
    }

    void StepSawyer(WorkerState w, double dt)
    {
        double left = dt, worked = 0;
        for (int guard = 0; left > Eps && guard < 1000; guard++)
        {
            if (!w.sawActive)
            {
                // старт: нужны бревна и место под доску; иначе стоим без опыта
                if (logs < WorkerBalance.SawyerLogsPerPlank || planks >= PlankCap) break;
                bool free = Roll(ThriftyChance(w));          // «Экономный»: бревна не тратятся
                if (!free) { logs -= WorkerBalance.SawyerLogsPerPlank; stockDirty = true; }
                w.sawActive = true; w.sawPaid = !free; w.sawProgress = 0;
            }

            double time = SawTime(w);
            double need = Math.Max(0.0, time - w.sawProgress);
            if (left + Eps < need)
            {
                w.sawProgress += left; worked += left; left = 0;
            }
            else
            {
                worked += need; left -= need;
                w.sawProgress = time;
                if (planks >= PlankCap) break;               // доска готова, но склад полон: ждёт места

                int n = 1; bool special = false;
                if (w.rarity == WorkerRarity.Mythic && Roll(WorkerBalance.SignatureEvenCutChance))
                { n = WorkerBalance.SignatureEvenCutMult; special = true; }                 // «Ровный распил»: отборная доска
                else if (Roll(DoublePlankChance(w))) { n = 2; special = true; }
                n = Math.Min(n, PlankCap - planks);
                planks += n; stockDirty = true;
                Emit(w, CampResource.Planks, n, special);
                w.sawActive = false; w.sawPaid = false; w.sawProgress = 0;
            }
        }
        if (worked > 0) AddXp(w, worked * WorkerBalance.XpPerSecond);   // опыт только за реальную работу
    }
}
