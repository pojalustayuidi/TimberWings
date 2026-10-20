// Assets/_Project/Scripts/Pets/PetRoller.cs (НЕ в Editor)
// Бросок клети: чистая логика без сцены, поэтому её легко проверять (Tools > BuilderSim > Питомцы > Проверить шансы).
//
// Гарантия (GDD 4.10): pityCounter = сколько клетей этого типа подряд открыто БЕЗ Редкого и выше.
// Когда счётчик дошёл до PetGuaranteeEvery - 1, текущая (10-я) клеть даёт Редкого или выше.
// Любой Редкий и выше (случайный или гарантированный) обнуляет счётчик. Счётчик свой на каждый тип клети (хранит PetManager).
//
// Результат выбирается ДО анимации: UI только красиво его показывает.
using System;
using System.Collections.Generic;

public static class PetRoller
{
    static readonly List<PetData> pool = new List<PetData>();

    // Выбирает редкость. Чистая функция: зависит только от таблицы шансов, счётчика и rng.
    public static PetRarity RollRarity(CrateType crate, int pityCounter, Random rng, out int newPity)
    {
        float[] odds = GameBalance.PetCrateOdds[(int)crate];
        bool forced = pityCounter >= GameBalance.PetGuaranteeEvery - 1;
        int first = forced ? (int)PetRarity.Rare : 0;

        double total = 0;
        for (int i = first; i < odds.Length; i++) if (odds[i] > 0) total += odds[i];

        PetRarity result;
        if (total <= 0)
        {
            // защита от кривой таблицы: безопасный запасной результат
            result = forced ? PetRarity.Rare : PetRarity.Common;
        }
        else
        {
            double roll = rng.NextDouble() * total;
            double acc = 0;
            int pick = first;
            for (int i = first; i < odds.Length; i++)
            {
                if (odds[i] <= 0) continue;
                acc += odds[i];
                pick = i;
                if (roll < acc) break;
            }
            result = (PetRarity)pick;
        }

        newPity = result >= PetRarity.Rare ? 0 : pityCounter + 1;
        return result;
    }

    // Выбирает питомца: сначала редкость, потом случайный вид этой редкости из базы.
    // Если питомцев нужной редкости в базе нет, берём ближайшую более низкую, потом более высокую (чтобы игра не ломалась).
    // Возвращает null только если база пустая. newPity считается по реально выданной редкости.
    public static PetData Roll(PetDatabase db, CrateType crate, int pityCounter, Random rng, out int newPity)
    {
        int pityAfterRarity;
        PetRarity wanted = RollRarity(crate, pityCounter, rng, out pityAfterRarity);
        newPity = pityAfterRarity;
        if (db == null) return null;

        int r = (int)wanted;
        for (int step = 0; step < 4; step++)
        {
            // порядок поиска: wanted, ниже, ещё ниже..., потом выше
            int idx = r - step;
            if (idx < 0) break;
            db.CollectByRarity((PetRarity)idx, pool);
            if (pool.Count > 0) return Finish(pool[rng.Next(pool.Count)], pityCounter, out newPity);
        }
        for (int idx = r + 1; idx <= (int)PetRarity.Legendary; idx++)
        {
            db.CollectByRarity((PetRarity)idx, pool);
            if (pool.Count > 0) return Finish(pool[rng.Next(pool.Count)], pityCounter, out newPity);
        }
        return null;
    }

    static PetData Finish(PetData pet, int pityBefore, out int newPity)
    {
        newPity = pet.rarity >= PetRarity.Rare ? 0 : pityBefore + 1;
        return pet;
    }
}
