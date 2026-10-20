// Assets/_Project/Scripts/Workers/WorkerPackRoller.cs (НЕ в Editor)
// Розыгрыш пака рабочих: чистая логика без сцены и без инвентаря, поэтому проверяется тестом
// (Tools > BuilderSim > Рабочие > Проверить паки). Все числа берутся из WorkerBalance.
//
// Гарантии (дизайн «Система рабочих», раздел «Паки»), счётчики ОТДЕЛЬНЫЕ для каждого типа пака:
//   Редкий или выше   каждые 10 паков (все типы);
//   Легендарный+      каждые 40 паков (Мастерской и Гильдейский);
//   Мифический        каждые 100 паков (только Гильдейский).
// Счётчик = сколько паков этого типа подряд открыто БЕЗ нужной редкости. Когда он дошёл до N−1, этот (N-й) пак обязан дать нужную
// редкость. Любая такая редкость, выпавшая случайно, обнуляет счётчик. Если требований несколько, действует самое высокое.
// Как работает: сначала обычный бросок по шансам; если результат ниже требования гарантии, делается добор среди редкостей
// «не ниже требования» по тем же шансам (для Мифика результат сразу Мифик). Так «сработала гарантия» значит «гарантия реально
// подняла результат», а распределение при этом такое же, как у «брошенного только среди допустимых».
//
// Результат выбирается ДО анимации: UI только красиво его показывает.
using System;
using System.Collections.Generic;

public enum PackGuarantee { None = 0, Rare = 1, Legendary = 2, Mythic = 3 }

// Счётчики гарантий и подарочный пак. Сериализуется в секцию «workers» (поля только с дефолтами).
[Serializable]
public class WorkerPackState
{
    public int[] sinceRare = new int[3];        // паков без Редкого+ подряд, по типам пака
    public int[] sinceLegendary = new int[3];   // паков без Легендарного+ подряд (Мастерской, Гильдейский)
    public int[] sinceMythic = new int[3];      // паков без Мифического подряд (Гильдейский)
    public bool freePending;                    // сюжет предложил подарочный пак, он ещё не открыт
    public bool freeUsed;                       // подарочный пак уже открыт (один раз за всё время)

    public void Clear()
    {
        for (int i = 0; i < 3; i++) { sinceRare[i] = 0; sinceLegendary[i] = 0; sinceMythic[i] = 0; }
        freePending = false; freeUsed = false;
    }

    // Принять данные из файла: длины массивов и значения приводятся к допустимым
    public void CopyFrom(WorkerPackState o)
    {
        Clear();
        if (o == null) return;
        for (int i = 0; i < 3; i++)
        {
            sinceRare[i] = Clamp(o.sinceRare, i, WorkerBalance.GuaranteeRareEvery - 1);
            sinceLegendary[i] = Clamp(o.sinceLegendary, i, WorkerBalance.GuaranteeLegendaryEvery - 1);
            sinceMythic[i] = Clamp(o.sinceMythic, i, WorkerBalance.GuaranteeMythicEvery - 1);
        }
        freeUsed = o.freeUsed;
        freePending = o.freePending && !o.freeUsed;
    }

    static int Clamp(int[] a, int i, int max)
    {
        if (a == null || i >= a.Length || a[i] < 0) return 0;
        return a[i] > max ? max : a[i];
    }
}

public static class WorkerPackRoller
{
    static readonly List<WorkerDefinition> pool = new List<WorkerDefinition>();

    // Какая гарантия действует на ЭТОТ пак (самая высокая). None = пак обычный
    public static PackGuarantee Required(WorkerPackType t, WorkerPackState s)
    {
        int i = (int)t;
        if (WorkerBalance.HasMythicGuarantee(t) && s.sinceMythic[i] >= WorkerBalance.GuaranteeMythicEvery - 1) return PackGuarantee.Mythic;
        if (WorkerBalance.HasLegendaryGuarantee(t) && s.sinceLegendary[i] >= WorkerBalance.GuaranteeLegendaryEvery - 1) return PackGuarantee.Legendary;
        if (s.sinceRare[i] >= WorkerBalance.GuaranteeRareEvery - 1) return PackGuarantee.Rare;
        return PackGuarantee.None;
    }

    static WorkerRarity MinRarity(PackGuarantee g)
    {
        switch (g)
        {
            case PackGuarantee.Mythic: return WorkerRarity.Mythic;
            case PackGuarantee.Legendary: return WorkerRarity.Legendary;
            case PackGuarantee.Rare: return WorkerRarity.Rare;
            default: return WorkerRarity.Common;
        }
    }

    // Выбор редкости по таблице шансов среди индексов first..конец. Шансы с нулём пропускаются.
    // Только NextDouble (так тест подставляет «фиксированный» генератор). fallback: если подходящих шансов нет (кривая таблица)
    static WorkerRarity Pick(float[] odds, int first, System.Random rng, WorkerRarity fallback)
    {
        double total = 0;
        for (int i = first; i < odds.Length; i++) if (odds[i] > 0f) total += odds[i];
        if (total <= 0) return fallback;

        double roll = rng.NextDouble() * total, acc = 0;
        int pick = -1;
        for (int i = first; i < odds.Length; i++)
        {
            if (odds[i] <= 0f) continue;
            acc += odds[i];
            pick = i;
            if (roll < acc) break;
        }
        return (WorkerRarity)pick;
    }

    // Редкость пака. usePity = false отключает гарантии (тест «чистых» шансов).
    // triggered != None, если гарантия реально подняла результат.
    public static WorkerRarity RollRarity(WorkerPackType type, WorkerPackState state, System.Random rng, bool usePity, out PackGuarantee triggered)
    {
        triggered = PackGuarantee.None;
        float[] odds = WorkerBalance.OddsOf(type);
        WorkerRarity natural = Pick(odds, 0, rng, WorkerRarity.Common);
        if (!usePity) return natural;

        PackGuarantee req = Required(type, state);
        if (req == PackGuarantee.None) return natural;

        WorkerRarity min = MinRarity(req);
        if (natural >= min) return natural;           // выпало само: гарантия не понадобилась

        triggered = req;
        if (min == WorkerRarity.Mythic) return WorkerRarity.Mythic;
        return Pick(odds, (int)min, rng, min);
    }

    // Обновить счётчики после выпавшей (реально выданной) редкости
    public static void Advance(WorkerPackType type, WorkerPackState s, WorkerRarity got)
    {
        int i = (int)type;
        s.sinceRare[i] = got >= WorkerRarity.Rare ? 0 : s.sinceRare[i] + 1;
        if (WorkerBalance.HasLegendaryGuarantee(type)) s.sinceLegendary[i] = got >= WorkerRarity.Legendary ? 0 : s.sinceLegendary[i] + 1;
        if (WorkerBalance.HasMythicGuarantee(type)) s.sinceMythic[i] = got >= WorkerRarity.Mythic ? 0 : s.sinceMythic[i] + 1;
    }

    // Персонаж заданной редкости: равновероятно из всех ролей. Если такой редкости в базе нет, берём ближайшую ниже, потом выше
    // (чтобы игра не ломалась на неполной базе). null только если база пустая.
    public static WorkerDefinition PickDefinition(WorkerDatabase db, WorkerRarity wanted, System.Random rng)
    {
        if (db == null) return null;
        int r = (int)wanted;
        for (int idx = r; idx >= 0; idx--)
        {
            db.CollectByRarity((WorkerRarity)idx, pool);
            if (pool.Count > 0) return pool[Index(rng, pool.Count)];
        }
        for (int idx = r + 1; idx <= (int)WorkerRarity.Mythic; idx++)
        {
            db.CollectByRarity((WorkerRarity)idx, pool);
            if (pool.Count > 0) return pool[Index(rng, pool.Count)];
        }
        return null;
    }

    // Подарочный пак: Обычный Приказчик (он приносит монеты, окупаемость первого пака около 3 минут).
    // Запасной порядок: любой Приказчик по редкости, потом любой персонаж.
    public static WorkerDefinition PickFree(WorkerDatabase db, System.Random rng)
    {
        if (db == null) return null;
        for (int r = 0; r <= (int)WorkerRarity.Mythic; r++)
        {
            db.Collect(WorkerRole.Clerk, (WorkerRarity)r, pool);
            if (pool.Count > 0) return pool[Index(rng, pool.Count)];
        }
        if (db.workers != null)
        {
            pool.Clear();
            for (int i = 0; i < db.workers.Count; i++) if (db.workers[i] != null) pool.Add(db.workers[i]);
            if (pool.Count > 0) return pool[Index(rng, pool.Count)];
        }
        return null;
    }

    static int Index(System.Random rng, int count)
    {
        int i = (int)(rng.NextDouble() * count);
        return i < 0 ? 0 : (i >= count ? count - 1 : i);
    }
}
