// Assets/_Project/Scripts/Pets/PetManager.cs (НЕ в Editor)
// Питомцы игрока: владения, слоты, клети, гарантия, бонусы, сохранение. Ставится на сцену один раз (как WorkerManager),
// в поле database перетащи ассет PetDatabase.
//
// Экономика: ТОЛЬКО GameBalance (цены, шансы, гарантия, слоты). Монеты живут в PlayerInventory.coinsCount.
// События (UI подписывается, менеджер про UI не знает): Opened, Changed, GrowthStageChanged.
//   Changed вызывается при любом изменении: открыта клеть, взят/снят/отпущен питомец, новый уровень игрока.
//   PlayerUpgrades и WorkerManager слушают Changed и сами пересчитывают бонусы.
//
// Сохранение: ISaveable, секция «pets» (SaveKey pets, LoadOrder 50) в общем файле SaveSystem. Запись через RequestSave("pets").
// Для тестов выключи persistAcrossRuns (питомцы не пишутся и не читаются) или используй «Тест: сбросить питомцев».
using System;
using System.Collections.Generic;
using UnityEngine;

public enum PetOpenResult { Ok, NoDatabase, Locked, NoCoins, NoPlayer }

// один экземпляр питомца у игрока (дубликаты разрешены, у каждого свой uid)
public class PetInstance
{
    public int uid;
    public PetData data;
    public int stars;        // звёзды слияния, 0..GameBalance.PetStarMax
    public bool isNew;       // только что получен и ещё не просмотрен в окне коллекции

    // бонус этого питомца как доля, со звёздами (для Capacity это доля до перевода в бревна)
    public float Bonus { get { return data != null ? GameBalance.PetBonus(data.rarity, stars) : 0f; } }
}

public class PetManager : MonoBehaviour, ISaveable
{
    public static PetManager Instance { get; private set; }

    // 3 слота по уровням (GameBalance.PetSlotLevels) + 4-й слот (индекс 3) по навыку «Верный друг» (carry_friend)
    public const int MaxSlots = 4;
    public const int SkillSlotIndex = 3;

    [Header("Данные")]
    [Tooltip("Ассет PetDatabase со всеми питомцами")]
    public PetDatabase database;

    [Header("Сохранение")]
    [Tooltip("Хранить питомцев в файле сохранения (SaveSystem)")]
    public bool persistAcrossRuns = true;

    [Header("Отладка")]
    [Tooltip("Открывать клети независимо от уровня игрока (для тестов)")]
    public bool debugIgnoreLevelLock = false;

    // ------------------------------------------------------------ события
    public static event Action<PetInstance> Opened;        // клеть открыта: выпал питомец (после списания монет и сохранения)
    public static event Action Changed;                    // изменились владения, экипировка, слоты или стадия
    public static event Action<int> GrowthStageChanged;    // стадия роста питомцев стала 0, 1 или 2 (малыш, подросток, взрослый)

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic()
    {
        Instance = null;
        Opened = null;
        Changed = null;
        GrowthStageChanged = null;
    }

    // ------------------------------------------------------------ состояние
    readonly List<PetInstance> owned = new List<PetInstance>();
    readonly int[] equippedUid = new int[] { -1, -1, -1, -1 };    // uid питомца в каждом слоте, -1 пусто
    readonly int[] pity = new int[3];                         // клетей без Редкого подряд, по типам клети
    readonly List<PetData> equippedBuf = new List<PetData>(MaxSlots);
    readonly List<int> starsBuf = new List<int>(MaxSlots);
    readonly HashSet<string> discovered = new HashSet<string>();   // id всех видов, которые игрок хоть раз получал (Книга питомцев)
    float bookAll;                                                  // бонус Книги ко всем процентным характеристикам
    static readonly List<PetData> tmpByRarity = new List<PetData>();
    static readonly List<PetInstance> tmpFodder = new List<PetInstance>();
    readonly System.Random rng = new System.Random();
    int nextUid = 1;
    bool skyUnlockedByStory;                                  // Небесная клеть открыта сюжетом (мост построен)
    bool freeCratePending, freeCrateUsed;                     // первая бесплатная Лесная клеть: предложена сюжетом / уже открыта (один раз за всё время)
    PetBonusSet bonus = PetBonusSet.None;
    int lastStage;

    PlayerInventory inv;
    PlayerStats stats;
    float findAt;

    // ------------------------------------------------------------ чтение состояния
    public IReadOnlyList<PetInstance> Owned { get { return owned; } }
    public PetBonusSet Bonus { get { return bonus; } }

    public int PlayerLevel
    {
        get { return FindPlayer() && stats != null ? stats.currentLevel : 1; }
    }

    // 4-й слот открыт навыком «Верный друг»
    public static bool SkillSlotUnlocked { get { return PlayerSkills.Has(SkillIds.CarryFriend); } }

    // слот i открыт: первые три по уровню игрока, четвёртый по навыку
    public bool IsSlotOpen(int i)
    {
        if (i < 0 || i >= MaxSlots) return false;
        if (i == SkillSlotIndex) return SkillSlotUnlocked;
        return i < Mathf.Clamp(GameBalance.PetSlotsAt(PlayerLevel), 1, SkillSlotIndex);
    }

    // сколько слотов экипировки доступно сейчас (1..4)
    public int SlotCount
    {
        get { int n = 0; for (int i = 0; i < MaxSlots; i++) if (IsSlotOpen(i)) n++; return Mathf.Max(1, n); }
    }

    // стадия роста питомцев: 0 малыш, 1 подросток, 2 взрослый
    public int GrowthStage { get { return GameBalance.PetGrowthStage(PlayerLevel); } }

    public int EquippedCount
    {
        get { int n = 0; for (int i = 0; i < MaxSlots; i++) if (equippedUid[i] >= 0) n++; return n; }
    }

    public PetInstance GetEquipped(int slot)
    {
        if (slot < 0 || slot >= MaxSlots) return null;
        return FindOwned(equippedUid[slot]);
    }

    public bool IsEquipped(int uid)
    {
        if (uid < 0) return false;
        for (int i = 0; i < MaxSlots; i++) if (equippedUid[i] == uid) return true;
        return false;
    }

    // сколько клетей этого типа осталось открыть до гарантированного Редкого (1 = следующая даст Редкого)
    public int OpensUntilGuarantee(CrateType crate)
    {
        return Mathf.Max(1, GameBalance.PetGuaranteeEvery - pity[(int)crate]);
    }

    // бесплатная клеть (глава «Звери-помощники»): пока она ждёт, Лесная стоит 0 монет
    public int Price(CrateType crate)
    {
        if (crate == CrateType.Forest && freeCratePending) return 0;
        return GameBalance.PetCratePrice[(int)crate];
    }

    public bool FreeCratePending { get { return freeCratePending; } }
    public bool FreeCrateUsed { get { return freeCrateUsed; } }

    public bool IsCrateUnlocked(CrateType crate)
    {
        if (debugIgnoreLevelLock) return true;
        if (crate == CrateType.Forest && freeCratePending) return true;
        if (crate == CrateType.Sky && skyUnlockedByStory) return true;
        return PlayerLevel >= GameBalance.PetCrateLevel[(int)crate];
    }

    // сюжет (мост построен) открывает Небесную клеть независимо от уровня
    public void UnlockSkyCrate()
    {
        if (skyUnlockedByStory) return;
        skyUnlockedByStory = true;
        SaveSystem.RequestSave("pets");
        RaiseChanged();
    }

    // сюжет предлагает первую бесплатную Лесную клеть. Один раз: если уже открывали, ничего не происходит
    public void OfferFreeCrate()
    {
        if (freeCrateUsed || freeCratePending) return;
        freeCratePending = true;
        SaveSystem.RequestSave("pets");
        RaiseChanged();
    }

    // первая бесплатная клеть всегда даёт Обычного (если Обычных в базе нет, любого)
    PetData PickFreePet()
    {
        if (database == null || database.pets == null) return null;
        var list = new List<PetData>();
        database.CollectByRarity(PetRarity.Common, list);
        if (list.Count == 0)
            for (int i = 0; i < database.pets.Count; i++) if (database.pets[i] != null) list.Add(database.pets[i]);
        return list.Count > 0 ? list[rng.Next(list.Count)] : null;
    }

    // ------------------------------------------------------------ клети
    // Можно ли открыть сейчас (причина в why). Проверка без побочных эффектов
    public bool CanOpen(CrateType crate, out PetOpenResult why)
    {
        why = PetOpenResult.Ok;
        if (database == null || database.pets == null || database.pets.Count == 0) { why = PetOpenResult.NoDatabase; return false; }
        if (!FindPlayer()) { why = PetOpenResult.NoPlayer; return false; }
        if (!IsCrateUnlocked(crate)) { why = PetOpenResult.Locked; return false; }
        if (inv.coinsCount < Price(crate)) { why = PetOpenResult.NoCoins; return false; }
        return true;
    }

    // Открыть клеть: списывает монеты, бросает питомца (с гарантией), добавляет во владения, сохраняет.
    // Результат известен сразу, UI только красиво его показывает. Питомца в слот не ставит: это делает игрок (Equip).
    public PetOpenResult TryOpenCrate(CrateType crate, out PetInstance result)
    {
        result = null;
        PetOpenResult why;
        if (!CanOpen(crate, out why)) return why;

        bool free = crate == CrateType.Forest && freeCratePending;
        int price = Price(crate);
        int before = inv.coinsCount;
        inv.AddCoins(-price);
        if (before - inv.coinsCount != price)      // списание проверяем по факту
        {
            if (before - inv.coinsCount > 0) inv.AddCoins(before - inv.coinsCount);
            return PetOpenResult.NoCoins;
        }

        int pityNew = pity[(int)crate];            // бесплатная клеть счётчик гарантии не трогает
        PetData data = free ? PickFreePet() : PetRoller.Roll(database, crate, pity[(int)crate], rng, out pityNew);
        if (data == null)
        {
            inv.AddCoins(price);                   // база пустая: возвращаем монеты
            return PetOpenResult.NoDatabase;
        }

        pity[(int)crate] = pityNew;
        if (free) { freeCratePending = false; freeCrateUsed = true; }
        result = AddInstance(data);
        SaveSystem.RequestSave("pets");
        if (Opened != null) Opened(result);
        RaiseChanged();
        return PetOpenResult.Ok;
    }

    // Выдать конкретного питомца без клети (для сюжета: первая бесплатная клеть, награды)
    public PetInstance Grant(PetData data)
    {
        if (data == null) return null;
        PetInstance p = AddInstance(data);
        SaveSystem.RequestSave("pets");
        RaiseChanged();
        return p;
    }

    // ------------------------------------------------------------ экипировка и коллекция
    // Взять питомца с собой. slot = -1: в первый свободный. false, если слотов нет, слот занят или питомец уже взят
    public bool Equip(int uid, int slot = -1)
    {
        if (FindOwned(uid) == null || IsEquipped(uid)) return false;
        if (slot < 0)
        {
            for (int i = 0; i < MaxSlots; i++) if (IsSlotOpen(i) && equippedUid[i] < 0) { slot = i; break; }
            if (slot < 0) return false;
        }
        else if (!IsSlotOpen(slot) || equippedUid[slot] >= 0) return false;

        equippedUid[slot] = uid;
        Commit();
        return true;
    }

    // Снять питомца из слота (остаётся в коллекции)
    public bool Unequip(int slot)
    {
        if (slot < 0 || slot >= MaxSlots || equippedUid[slot] < 0) return false;
        equippedUid[slot] = -1;
        Commit();
        return true;
    }

    // Отпустить питомца: удаляется из коллекции навсегда (бонус пропадает, если он был в слоте)
    public bool Release(int uid)
    {
        PetInstance p = FindOwned(uid);
        if (p == null) return false;
        for (int i = 0; i < MaxSlots; i++) if (equippedUid[i] == uid) equippedUid[i] = -1;
        owned.Remove(p);
        Commit();
        return true;
    }

    // сколько штук такого вида у игрока (для пометки дубликатов в UI)
    public int CountOf(PetData data)
    {
        int n = 0;
        for (int i = 0; i < owned.Count; i++) if (owned[i].data == data) n++;
        return n;
    }

    // ------------------------------------------------------------ NEW, Книга питомцев
    public int NewCount
    {
        get { int n = 0; for (int i = 0; i < owned.Count; i++) if (owned[i].isNew) n++; return n; }
    }

    // игрок посмотрел питомца: бейдж NEW гаснет (событие Changed не шлём: окно само обновит карточку)
    public void MarkSeen(int uid)
    {
        PetInstance p = FindOwned(uid);
        if (p == null || !p.isNew) return;
        p.isNew = false;
        SaveSystem.RequestSave("pets");
    }

    public bool IsDiscovered(PetData d) { return d != null && !string.IsNullOrEmpty(d.id) && discovered.Contains(d.id); }

    public int DiscoveredCount
    {
        get
        {
            if (database == null) return discovered.Count;
            int n = 0;
            for (int i = 0; i < database.pets.Count; i++) if (IsDiscovered(database.pets[i])) n++;
            return n;
        }
    }

    public int TotalSpecies { get { int n = 0; if (database != null) for (int i = 0; i < database.pets.Count; i++) if (database.pets[i] != null) n++; return n; } }

    // открыто и всего видов данной редкости
    public void RarityProgress(PetRarity r, out int found, out int total)
    {
        found = 0; total = 0;
        if (database == null) return;
        database.CollectByRarity(r, tmpByRarity);
        total = tmpByRarity.Count;
        for (int i = 0; i < tmpByRarity.Count; i++) if (IsDiscovered(tmpByRarity[i])) found++;
        tmpByRarity.Clear();
    }

    public bool IsRarityComplete(PetRarity r)
    {
        int f, t; RarityProgress(r, out f, out t);
        return t > 0 && f >= t;
    }

    public float BookBonusAll { get { return bookAll; } }

    float ComputeBookBonus()
    {
        float sum = 0f;
        for (int r = 0; r < GameBalance.PetBookBonusByRarity.Length; r++)
            if (IsRarityComplete((PetRarity)r)) sum += GameBalance.PetBookBonusByRarity[r];
        return sum;
    }

    // ------------------------------------------------------------ слияние дубликатов
    // Выбирает, кто получит звезду (keeper) и кого «съедят» (fodder). Экипированного питомца не съедаем.
    PetInstance PlanMerge(PetData data, List<PetInstance> fodder)
    {
        fodder.Clear();
        if (data == null) return null;

        PetInstance keeper = null;
        for (int i = 0; i < owned.Count; i++)
        {
            PetInstance o = owned[i];
            if (o.data != data || o.stars >= GameBalance.PetStarMax) continue;
            if (keeper == null) { keeper = o; continue; }
            bool oe = IsEquipped(o.uid), ke = IsEquipped(keeper.uid);
            if ((oe && !ke) || (oe == ke && o.stars > keeper.stars)) keeper = o;
        }
        if (keeper == null) return null;

        int need = GameBalance.PetMergeCount - 1;
        for (int i = 0; i < owned.Count; i++)
            if (owned[i].data == data && owned[i] != keeper && !IsEquipped(owned[i].uid) && owned[i].stars <= keeper.stars) fodder.Add(owned[i]);   // копии с большим числом звёзд не съедаем
        if (fodder.Count < need) { fodder.Clear(); return null; }

        fodder.Sort((a, b) => a.stars != b.stars ? a.stars.CompareTo(b.stars) : b.uid.CompareTo(a.uid));
        if (fodder.Count > need) fodder.RemoveRange(need, fodder.Count - need);
        return keeper;
    }

    // можно ли слить этого вида: хватает копий и у кого-то ещё нет максимума звёзд
    public bool CanMerge(PetData data)
    {
        bool ok = PlanMerge(data, tmpFodder) != null;
        tmpFodder.Clear();
        return ok;
    }

    // Сливает PetMergeCount копий в одну с +1 звездой. Возвращает uid получившего звезду или -1
    public int Merge(PetData data)
    {
        PetInstance keeper = PlanMerge(data, tmpFodder);
        if (keeper == null) { tmpFodder.Clear(); return -1; }
        for (int i = 0; i < tmpFodder.Count; i++) owned.Remove(tmpFodder[i]);
        tmpFodder.Clear();
        keeper.stars = Mathf.Min(GameBalance.PetStarMax, keeper.stars + 1);
        Commit();
        return keeper.uid;
    }

    // ------------------------------------------------------------ замена в слоте, превью, автоподбор
    readonly List<PetData> pvData = new List<PetData>(MaxSlots + 1);
    readonly List<int> pvStars = new List<int>(MaxSlots + 1);

    bool HasFreeOpenSlot()
    {
        for (int i = 0; i < MaxSlots; i++) if (IsSlotOpen(i) && equippedUid[i] < 0) return true;
        return false;
    }

    // бонусы, если взять в слоты cand, сняв replace (replace = null: никого не снимать). Текущие слоты не меняются
    public PetBonusSet PreviewBonus(PetInstance cand, PetInstance replace)
    {
        pvData.Clear(); pvStars.Clear();
        for (int i = 0; i < MaxSlots; i++)
        {
            PetInstance e = FindOwned(equippedUid[i]);
            if (e == null || e == replace || e == cand) continue;
            pvData.Add(e.data); pvStars.Add(e.stars);
        }
        if (cand != null) { pvData.Add(cand.data); pvStars.Add(cand.stars); }
        return PetBonus.Compute(pvData, pvStars, bookAll);
    }

    // Кого вытеснит cand при нажатии «Взять с собой». null: есть свободный слот, питомец уже в слоте или слотов нет совсем.
    // Если слоты полны, выбираем того, чья замена даёт наибольший суммарный эффект.
    public PetInstance WouldReplace(PetInstance cand)
    {
        if (cand == null || IsEquipped(cand.uid) || HasFreeOpenSlot()) return null;
        PetInstance best = null; float bestScore = float.MinValue;
        for (int i = 0; i < MaxSlots; i++)
        {
            PetInstance e = FindOwned(equippedUid[i]);
            if (e == null) continue;
            float sc = PetBonus.Score(PreviewBonus(cand, e));
            if (sc > bestScore + 1e-5f || (Mathf.Abs(sc - bestScore) <= 1e-5f && best != null && e.Bonus < best.Bonus)) { bestScore = sc; best = e; }
        }
        return best;
    }

    // Взять cand в слот того, кого вытесняем. false, если replace не в слоте или cand уже взят
    public bool EquipReplacing(int uid, int replaceUid)
    {
        if (FindOwned(uid) == null || IsEquipped(uid)) return false;
        for (int i = 0; i < MaxSlots; i++)
        {
            if (equippedUid[i] != replaceUid || replaceUid < 0) continue;
            equippedUid[i] = uid;
            Commit();
            return true;
        }
        return false;
    }

    // «Подобрать лучших»: набор питомцев с максимальным суммарным эффектом с учётом PetBonusCap, звёзд и Книги.
    // Перебор: на каждую характеристику хватает лучших N (N = число открытых слотов), всего до ~28 кандидатов, до ~20 тыс. сочетаний.
    // true, если набор в слотах изменился.
    public bool AutoEquipBest()
    {
        var slotIdx = new List<int>(MaxSlots);
        for (int i = 0; i < MaxSlots; i++) if (IsSlotOpen(i)) slotIdx.Add(i);
        int k = slotIdx.Count;
        if (k == 0 || owned.Count == 0) return false;

        // кандидаты: по каждой характеристике лучшие k (экипированные при равенстве выше)
        var sortedAll = new List<PetInstance>(owned);
        sortedAll.Sort((a, b) =>
        {
            int r = b.Bonus.CompareTo(a.Bonus);
            if (r != 0) return r;
            bool ae = IsEquipped(a.uid), be = IsEquipped(b.uid);
            if (ae != be) return ae ? -1 : 1;
            return a.uid.CompareTo(b.uid);
        });
        var perStat = new int[7];
        var cands = new List<PetInstance>();
        for (int i = 0; i < sortedAll.Count; i++)
        {
            int st = (int)sortedAll[i].data.stat;
            if (st < 0 || st >= 7 || perStat[st] >= k) continue;
            perStat[st]++;
            cands.Add(sortedAll[i]);
        }

        int take = Mathf.Min(k, cands.Count);
        var cur = new int[take];
        var best = new int[take];
        float bestScore = -1f;
        var d = new List<PetData>(take);
        var sr = new List<int>(take);

        // рекурсия без замыканий лишних аллокаций: локальная функция
        void Rec(int start, int depth)
        {
            if (depth == take)
            {
                d.Clear(); sr.Clear();
                for (int i = 0; i < take; i++) { d.Add(cands[cur[i]].data); sr.Add(cands[cur[i]].stars); }
                float sc = PetBonus.Score(PetBonus.Compute(d, sr, bookAll));
                if (sc > bestScore + 1e-6f) { bestScore = sc; System.Array.Copy(cur, best, take); }
                return;
            }
            for (int i = start; i <= cands.Count - (take - depth); i++) { cur[depth] = i; Rec(i + 1, depth + 1); }
        }
        Rec(0, 0);
        if (bestScore < 0f) return false;

        var chosen = new List<PetInstance>(take);
        for (int i = 0; i < take; i++) chosen.Add(cands[best[i]]);

        // уже стоящие остаются на своих местах, остальные идут в свободные открытые слоты
        var newSlots = new int[MaxSlots];
        for (int i = 0; i < MaxSlots; i++) newSlots[i] = -1;
        var rest = new List<PetInstance>();
        for (int i = 0; i < chosen.Count; i++)
        {
            int at = -1;
            for (int sIdx = 0; sIdx < MaxSlots; sIdx++) if (equippedUid[sIdx] == chosen[i].uid && IsSlotOpen(sIdx)) { at = sIdx; break; }
            if (at >= 0) newSlots[at] = chosen[i].uid; else rest.Add(chosen[i]);
        }
        int ri = 0;
        for (int i = 0; i < slotIdx.Count && ri < rest.Count; i++)
            if (newSlots[slotIdx[i]] < 0) newSlots[slotIdx[i]] = rest[ri++].uid;

        bool changed = false;
        for (int i = 0; i < MaxSlots; i++) if (equippedUid[i] != newSlots[i]) changed = true;
        if (!changed) return false;
        RememberLoadout();
        for (int i = 0; i < MaxSlots; i++) equippedUid[i] = newSlots[i];
        Commit();
        return true;
    }

    // ------------------------------------------------------------ состав одним действием и наборы (пресеты)
    // Любая смена состава через ApplyLoadout: один Commit (одно сохранение, один пересчёт бонусов, одно событие Changed).
    // Предыдущий состав запоминается: UndoLoadout() возвращает его (повторный вызов возвращает обратно).
    public const int PresetCount = 3;

    // Набор хранит uid И id вида: если uid пропал (слияние, «Отпустить», новая игра), ищем копию того же вида
    [Serializable] class SavedPreset
    {
        public int[] uids = new int[] { -1, -1, -1, -1 };
        public string[] ids = new string[] { "", "", "", "" };
        public int cap;            // 0 = подпись по главной характеристике, n = n-я готовая подпись (список в окне)
    }

    readonly SavedPreset[] presets = CreatePresets();
    int[] undoLoadout;
    static readonly int[] presetTmp = new int[MaxSlots];

    static SavedPreset[] CreatePresets()
    {
        var a = new SavedPreset[PresetCount];
        for (int i = 0; i < a.Length; i++) a[i] = new SavedPreset();
        return a;
    }

    void ClearAllPresets()
    {
        for (int i = 0; i < PresetCount; i++)
        {
            for (int k = 0; k < MaxSlots; k++) { presets[i].uids[k] = -1; presets[i].ids[k] = ""; }
            presets[i].cap = 0;
        }
        undoLoadout = null;
    }

    void RememberLoadout()
    {
        if (undoLoadout == null) undoLoadout = new int[MaxSlots];
        for (int i = 0; i < MaxSlots; i++) undoLoadout[i] = equippedUid[i];
    }

    public bool CanUndoLoadout { get { return undoLoadout != null; } }

    // Ставит uids[i] в слот i (-1 = пусто). Недоступные слоты, чужие и повторяющиеся uid пропускаются.
    // Возвращает, сколько запрошенных питомцев поставить не удалось. Если состав не изменился, ничего не сохраняет.
    int ApplyLoadout(int[] uids, bool remember)
    {
        var next = new int[MaxSlots];
        var used = new HashSet<int>();
        int missing = 0;
        for (int i = 0; i < MaxSlots; i++)
        {
            int u = uids != null && i < uids.Length ? uids[i] : -1;
            if (u >= 0 && (!IsSlotOpen(i) || FindOwned(u) == null || !used.Add(u))) { missing++; u = -1; }
            next[i] = u;
        }
        bool changed = false;
        for (int i = 0; i < MaxSlots; i++) if (next[i] != equippedUid[i]) { changed = true; break; }
        if (!changed) return missing;
        if (remember) RememberLoadout();
        for (int i = 0; i < MaxSlots; i++) equippedUid[i] = next[i];
        Commit();
        return missing;
    }

    // Весь состав одним действием. Возвращает, сколько питомцев не удалось поставить
    public int SetLoadout(int[] uids) { return ApplyLoadout(uids, true); }

    // Снять всех (остаются в коллекции). false, если и так никого не было
    public bool UnequipAll()
    {
        if (EquippedCount == 0) return false;
        ApplyLoadout(new int[] { -1, -1, -1, -1 }, true);
        return true;
    }

    // Вернуть состав до последней массовой смены (набор, «Снять всех», «Подобрать лучших»). Повтор возвращает обратно
    public bool UndoLoadout()
    {
        if (undoLoadout == null) return false;
        ApplyLoadout((int[])undoLoadout.Clone(), true);
        return true;
    }

    public bool PresetFilled(int i)
    {
        if (i < 0 || i >= PresetCount) return false;
        for (int k = 0; k < MaxSlots; k++) if (PresetWants(i, k)) return true;
        return false;
    }

    // в слоте k набора i записан питомец
    public bool PresetWants(int i, int k)
    {
        if (i < 0 || i >= PresetCount || k < 0 || k >= MaxSlots) return false;
        return !string.IsNullOrEmpty(presets[i].ids[k]);
    }

    // Кто реально встанет в слоты набора i: тот же uid, если он жив и того же вида, иначе лучшая свободная копия вида. -1: нет
    public void ResolvePreset(int i, int[] result)
    {
        for (int k = 0; k < MaxSlots; k++) result[k] = -1;
        if (i < 0 || i >= PresetCount) return;
        SavedPreset p = presets[i];
        var used = new HashSet<int>();

        for (int k = 0; k < MaxSlots; k++)                       // 1: тот же питомец
        {
            if (!PresetWants(i, k)) continue;
            PetInstance o = FindOwned(p.uids[k]);
            if (o != null && o.data != null && o.data.id == p.ids[k] && used.Add(o.uid)) result[k] = o.uid;
        }
        for (int k = 0; k < MaxSlots; k++)                       // 2: другая копия того же вида (после слияния или «Отпустить»)
        {
            if (!PresetWants(i, k) || result[k] >= 0) continue;
            PetInstance best = null;
            for (int j = 0; j < owned.Count; j++)
            {
                PetInstance o = owned[j];
                if (o.data == null || o.data.id != p.ids[k] || used.Contains(o.uid)) continue;
                if (best == null || o.stars > best.stars) best = o;
            }
            if (best != null) { result[k] = best.uid; used.Add(best.uid); }
        }
    }

    // Сохранить текущий состав слотов в набор i. false, если в слотах пусто
    public bool SavePreset(int i)
    {
        if (i < 0 || i >= PresetCount || EquippedCount == 0) return false;
        for (int k = 0; k < MaxSlots; k++)
        {
            PetInstance e = GetEquipped(k);
            presets[i].uids[k] = e != null ? e.uid : -1;
            presets[i].ids[k] = e != null && e.data != null && e.data.id != null ? e.data.id : "";
        }
        SaveSystem.RequestSave("pets");
        return true;
    }

    public void ClearPreset(int i)
    {
        if (i < 0 || i >= PresetCount) return;
        for (int k = 0; k < MaxSlots; k++) { presets[i].uids[k] = -1; presets[i].ids[k] = ""; }
        presets[i].cap = 0;
        SaveSystem.RequestSave("pets");
    }

    // Надеть набор i одним действием. Возвращает, для скольких записанных мест питомца нет (-1: набор пуст).
    // substituted: сколько мест заняли другие копии того же вида
    public int ApplyPreset(int i, out int substituted)
    {
        substituted = 0;
        if (!PresetFilled(i)) return -1;
        var res = new int[MaxSlots];
        ResolvePreset(i, res);
        int missing = 0;
        for (int k = 0; k < MaxSlots; k++)
        {
            if (!PresetWants(i, k)) continue;
            if (res[k] < 0 || !IsSlotOpen(k)) { missing++; continue; }
            if (res[k] != presets[i].uids[k]) substituted++;
        }
        ApplyLoadout(res, true);
        return missing;
    }

    static readonly int[] presetTmp2 = new int[MaxSlots];
    readonly List<PetData> prData = new List<PetData>(MaxSlots);
    readonly List<int> prStars = new List<int>(MaxSlots);

    // Какой бонус даст набор i, если надеть его сейчас (с потолком, звёздами и Книгой). Только для открытых слотов
    public PetBonusSet PresetBonus(int i)
    {
        if (!PresetFilled(i)) return PetBonusSet.None;
        ResolvePreset(i, presetTmp2);
        prData.Clear(); prStars.Clear();
        for (int k = 0; k < MaxSlots; k++)
        {
            if (presetTmp2[k] < 0 || !IsSlotOpen(k)) continue;
            PetInstance o = FindOwned(presetTmp2[k]);
            if (o != null) { prData.Add(o.data); prStars.Add(o.stars); }
        }
        return PetBonus.Compute(prData, prStars, bookAll);
    }

    // Подпись набора: -1 = авто (по главной характеристике), 0.. = номер готовой подписи
    public int PresetCaption(int i)
    {
        return i >= 0 && i < PresetCount ? presets[i].cap - 1 : -1;
    }

    // Следующая подпись по кругу: авто, 0, 1, ... count-1, авто
    public void CyclePresetCaption(int i, int count)
    {
        if (i < 0 || i >= PresetCount || count <= 0) return;
        int n = presets[i].cap + 1;
        if (n > count) n = 0;
        presets[i].cap = n;
        SaveSystem.RequestSave("pets");
    }

    // набор i надет прямо сейчас (все его места заняты теми, кого он выбирает)
    public bool PresetActive(int i)
    {
        if (!PresetFilled(i)) return false;
        ResolvePreset(i, presetTmp);
        for (int k = 0; k < MaxSlots; k++) if (presetTmp[k] != equippedUid[k]) return false;
        return true;
    }

    // ------------------------------------------------------------ внутреннее
    PetInstance AddInstance(PetData data)
    {
        var p = new PetInstance { uid = nextUid++, data = data, isNew = true };
        owned.Add(p);
        if (!string.IsNullOrEmpty(data.id)) discovered.Add(data.id);
        return p;
    }

    PetInstance FindOwned(int uid)
    {
        if (uid < 0) return null;
        for (int i = 0; i < owned.Count; i++) if (owned[i].uid == uid) return owned[i];
        return null;
    }

    void Commit()
    {
        SaveSystem.RequestSave("pets");
        RaiseChanged();
    }

    void RecomputeBonus()
    {
        bookAll = ComputeBookBonus();
        equippedBuf.Clear();
        starsBuf.Clear();
        for (int i = 0; i < MaxSlots; i++)
        {
            PetInstance p = FindOwned(equippedUid[i]);
            if (p != null) { equippedBuf.Add(p.data); starsBuf.Add(p.stars); }
        }
        bonus = PetBonus.Compute(equippedBuf, starsBuf, bookAll);
    }

    // Слот закрылся (навык «Верный друг» сброшен или не прошёл проверку при загрузке): питомец из него возвращается
    // в клеть, то есть остаётся в коллекции (owned не трогаем), просто снимается с экипировки. Потерять питомца нельзя.
    bool EnforceSlots()
    {
        bool moved = false;
        for (int i = 0; i < MaxSlots; i++)
        {
            if (equippedUid[i] < 0 || IsSlotOpen(i)) continue;
            equippedUid[i] = -1;
            moved = true;
        }
        return moved;
    }

    void RaiseChanged()
    {
        if (EnforceSlots() && !SaveSystem.IsLoading)
        {
            SaveSystem.RequestSave("pets");
            SaveIndicator.ShowToast("Слот питомца закрылся: питомец вернулся в клеть");
        }
        RecomputeBonus();
        if (Changed != null) Changed();
    }

    bool FindPlayer()
    {
        if (inv != null) return true;
        if (Time.unscaledTime < findAt) return false;
        findAt = Time.unscaledTime + 0.5f;

        inv = PlayerInventory.Instance;
        if (inv == null) return false;
        stats = inv.GetComponent<PlayerStats>();
        return true;
    }

    // ------------------------------------------------------------ жизненный цикл
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("PetManager: на сцене уже есть менеджер («" + Instance.name + "»), лишний («" + name + "») удалён.");
            if (GetComponents<Component>().Length <= 2) Destroy(gameObject); else Destroy(this);
            return;
        }
        Instance = this;
        SaveSystem.Register(this);   // если файл уже прочитан, секция «pets» применится прямо здесь (Load)
        RecomputeBonus();
    }

    void Start()
    {
        if (Instance != this) return;
        lastStage = GrowthStage;
        RaiseChanged();            // слушатели (PlayerUpgrades, WorkerManager) пересчитывают бонусы по загруженным питомцам
    }

    void OnEnable()
    {
        GameEvents.LevelUp += OnLevelUp; SaveSystem.Loaded += OnSaveLoaded;
        PlayerSkills.Changed -= OnSkillsChanged; PlayerSkills.Changed += OnSkillsChanged;
    }
    void OnDisable()
    {
        GameEvents.LevelUp -= OnLevelUp; SaveSystem.Loaded -= OnSaveLoaded;
        PlayerSkills.Changed -= OnSkillsChanged;
    }

    // купили или сбросили навык: 4-й слот открывается или закрывается, питомец из него возвращается в клеть
    void OnSkillsChanged()
    {
        if (Instance != this) return;
        RaiseChanged();
    }

    // «Продолжить»: Load() применил секцию без событий, сообщаем слушателям (PlayerUpgrades, WorkerManager, окна) один раз
    void OnSaveLoaded()
    {
        if (Instance != this) return;
        lastStage = GrowthStage;
        RaiseChanged();
    }

    void OnDestroy()
    {
        SaveSystem.Unregister(this);
        if (Instance == this) Instance = null;
    }

    void OnLevelUp(int level)
    {
        if (Instance != this) return;
        int stage = GameBalance.PetGrowthStage(level);
        if (stage != lastStage)
        {
            lastStage = stage;
            if (GrowthStageChanged != null) GrowthStageChanged(stage);
        }
        RaiseChanged();            // могли появиться новые слоты и открыться клети
    }

    // ------------------------------------------------------------ сохранение (ISaveable, секция «pets»)
    public string SaveKey { get { return "pets"; } }
    public int LoadOrder { get { return 50; } }

    // stars и isNew добавлены в версии 2: в старых файлах их нет, JsonUtility подставляет 0 и false
    [Serializable] class SavedPet { public int uid; public string id; public int stars; public bool isNew; }

    [Serializable]
    class SaveData
    {
        public int version = 3;                                   // 3: наборы питомцев (presets)
        public List<string> discovered = new List<string>();    // id видов для Книги питомцев (в старых файлах пусто: берём из owned)
        public List<SavedPet> owned = new List<SavedPet>();
        public int[] equipped = new int[] { -1, -1, -1, -1 };   // старые файлы с тремя значениями читаются: недостающие считаются пустыми
        public int[] pity = new int[3];
        public int nextUid = 1;
        public bool skyByStory;
        public bool freePending, freeUsed;
        public List<SavedPreset> presets = new List<SavedPreset>();   // в старых файлах пусто
    }

    public string Save()
    {
        if (!persistAcrossRuns) return null;   // SaveSystem оставит прошлую версию секции (или пропустит)
        var d = new SaveData { nextUid = nextUid, skyByStory = skyUnlockedByStory, freePending = freeCratePending, freeUsed = freeCrateUsed };
        for (int i = 0; i < owned.Count; i++)
            d.owned.Add(new SavedPet { uid = owned[i].uid, id = owned[i].data.id, stars = owned[i].stars, isNew = owned[i].isNew });
        foreach (string id in discovered) d.discovered.Add(id);
        for (int i = 0; i < MaxSlots; i++) d.equipped[i] = equippedUid[i];
        for (int i = 0; i < 3; i++) d.pity[i] = pity[i];
        for (int i = 0; i < PresetCount; i++)
        {
            var sp = new SavedPreset();
            for (int k = 0; k < MaxSlots; k++) { sp.uids[k] = presets[i].uids[k]; sp.ids[k] = presets[i].ids[k] ?? ""; }
            sp.cap = presets[i].cap;
            d.presets.Add(sp);
        }
        return JsonUtility.ToJson(d);
    }

    // без событий, наград и RequestSave (правило ISaveable); слушателей уведомит OnSaveLoaded
    public void Load(string json, int formatVersion)
    {
        ResetToDefault();
        if (!persistAcrossRuns || string.IsNullOrEmpty(json)) return;

        SaveData d = null;
        try { d = JsonUtility.FromJson<SaveData>(json); }
        catch (Exception e) { Debug.LogWarning("PetManager: не удалось прочитать сохранение питомцев: " + e.Message); }
        if (d == null) return;

        nextUid = Mathf.Max(1, d.nextUid);
        skyUnlockedByStory = d.skyByStory;
        freeCrateUsed = d.freeUsed;
        freeCratePending = d.freePending && !d.freeUsed;
        for (int i = 0; i < 3; i++)
            pity[i] = d.pity != null && i < d.pity.Length ? Mathf.Max(0, d.pity[i]) : 0;

        if (d.owned != null)
        {
            for (int i = 0; i < d.owned.Count; i++)
            {
                PetData data = database != null ? database.Find(d.owned[i].id) : null;
                if (data == null)
                {
                    Debug.LogWarning("PetManager: питомец «" + d.owned[i].id + "» из сохранения не найден в базе, пропущен.");
                    continue;
                }
                owned.Add(new PetInstance { uid = d.owned[i].uid, data = data, stars = Mathf.Clamp(d.owned[i].stars, 0, GameBalance.PetStarMax), isNew = d.owned[i].isNew });
                if (!string.IsNullOrEmpty(data.id)) discovered.Add(data.id);   // старое сохранение: всё, что есть, уже «открыто»
                nextUid = Mathf.Max(nextUid, d.owned[i].uid + 1);
            }
        }

        if (d.discovered != null)
            for (int i = 0; i < d.discovered.Count; i++)
                if (!string.IsNullOrEmpty(d.discovered[i])) discovered.Add(d.discovered[i]);

        for (int i = 0; i < MaxSlots; i++)
        {
            int u = d.equipped != null && i < d.equipped.Length ? d.equipped[i] : -1;
            equippedUid[i] = FindOwned(u) != null ? u : -1;
        }
        if (d.presets != null)
            for (int i = 0; i < PresetCount && i < d.presets.Count; i++)
            {
                SavedPreset sp = d.presets[i];
                if (sp == null) continue;
                presets[i].cap = Mathf.Max(0, sp.cap);
                for (int k = 0; k < MaxSlots; k++)
                {
                    bool has = sp.ids != null && k < sp.ids.Length && !string.IsNullOrEmpty(sp.ids[k]);
                    presets[i].ids[k] = has ? sp.ids[k] : "";
                    presets[i].uids[k] = has && sp.uids != null && k < sp.uids.Length ? sp.uids[k] : -1;
                }
            }
        RecomputeBonus();
    }

    // чистая игра: «Новая игра», битое сохранение, секция без данных
    public void ResetToDefault()
    {
        owned.Clear();
        discovered.Clear();
        for (int i = 0; i < MaxSlots; i++) equippedUid[i] = -1;
        for (int i = 0; i < 3; i++) pity[i] = 0;
        nextUid = 1;
        skyUnlockedByStory = false;
        freeCratePending = freeCrateUsed = false;
        ClearAllPresets();
        RecomputeBonus();
    }

    // ------------------------------------------------------------ отладка: правый клик по компоненту в Inspector
    void DebugOpen(CrateType crate)
    {
        PetInstance r;
        PetOpenResult res = TryOpenCrate(crate, out r);
        if (res == PetOpenResult.Ok)
            Debug.Log("[Питомцы] " + PetText.Crate(crate) + ": выпал «" + r.data.displayName + "» (" + PetText.Rarity(r.data.rarity)
                + ", " + PetText.Stat(r.data.stat) + " +" + Mathf.RoundToInt(r.data.Bonus * 100f) + "%). До гарантии: " + OpensUntilGuarantee(crate));
        else
            Debug.Log("[Питомцы] " + PetText.Crate(crate) + " не открыта: " + res);
    }

    [ContextMenu("Тест: открыть Лесную клеть")] void DebugForest() { DebugOpen(CrateType.Forest); }
    [ContextMenu("Тест: открыть Речную клеть")] void DebugRiver() { DebugOpen(CrateType.River); }
    [ContextMenu("Тест: открыть Небесную клеть")] void DebugSky() { DebugOpen(CrateType.Sky); }

    [ContextMenu("Тест: +2000 монет")]
    void DebugCoins()
    {
        if (FindPlayer()) inv.AddCoins(2000);
        else Debug.Log("[Питомцы] Игрок не найден: запусти игру (Play).");
    }

    [ContextMenu("Тест: взять в слоты всех подряд")]
    void DebugEquipAll()
    {
        for (int i = 0; i < owned.Count; i++) Equip(owned[i].uid);
        Debug.Log("[Питомцы] В слотах: " + EquippedCount + " из " + SlotCount + ". Бонус: рубка " + bonus.chop + ", бег " + bonus.move
            + ", рюкзак +" + bonus.capacityLogs + ", двойная " + bonus.doubleYield + ", торг " + bonus.haggle + ", пила " + bonus.saw + ", рабочие " + bonus.worker);
    }

    [ContextMenu("Тест: сбросить питомцев и сохранение")]
    void DebugReset()
    {
        owned.Clear();
        discovered.Clear();
        for (int i = 0; i < MaxSlots; i++) equippedUid[i] = -1;
        for (int i = 0; i < 3; i++) pity[i] = 0;
        nextUid = 1;
        skyUnlockedByStory = false;
        freeCratePending = freeCrateUsed = false;
        ClearAllPresets();
        SaveSystem.RequestSave("pets");
        RaiseChanged();
    }
}
