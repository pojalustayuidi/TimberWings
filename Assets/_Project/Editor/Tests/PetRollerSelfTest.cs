// Assets/_Project/Editor/Tests/PetRollerSelfTest.cs (Editor)
// Проверка шансов клетей и гарантии: Tools > BuilderSim > Питомцы > Проверить шансы.
// Отдельного тестового assembly в проекте нет, поэтому проверка сделана меню, результат в Console и в диалоге.
using System;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class PetRollerSelfTest
{
    const int Samples = 10000;
    const double TolerancePercent = 2.0;   // допустимое отклонение доли редкости от таблицы, п.п.

    [MenuItem("Tools/BuilderSim/Питомцы/Проверить шансы")]
    public static void Run()
    {
        var log = new StringBuilder();
        int errors = 0;
        var rng = new System.Random(12345);   // фиксированный seed: результат воспроизводим

        for (int c = 0; c < 3; c++)
        {
            CrateType crate = (CrateType)c;
            float[] odds = GameBalance.PetCrateOdds[c];
            log.AppendLine("== " + PetText.Crate(crate) + " ==");

            // 1. сумма шансов = 100
            double sum = 0;
            for (int i = 0; i < odds.Length; i++) sum += odds[i];
            if (Math.Abs(sum - 100.0) > 0.001) { errors++; log.AppendLine("ОШИБКА: сумма шансов " + sum + ", нужно 100"); }
            else log.AppendLine("Сумма шансов: 100 OK");

            // 2. базовая таблица без гарантии (счётчик всегда 0): доли близки к таблице
            int[] hits = new int[4];
            int dummy;
            for (int n = 0; n < Samples; n++) hits[(int)PetRoller.RollRarity(crate, 0, rng, out dummy)]++;
            for (int i = 0; i < 4; i++)
            {
                double got = hits[i] * 100.0 / Samples;
                bool ok = Math.Abs(got - odds[i]) <= TolerancePercent && (odds[i] > 0 || hits[i] == 0);
                if (!ok) errors++;
                log.AppendLine("  " + PetText.Rarity((PetRarity)i) + ": таблица " + odds[i] + "%, выпало " + got.ToString("F1") + "% " + (ok ? "OK" : "ОШИБКА"));
            }

            // 3. гарантия: при счётчике 9 (это 10-я клеть) всегда Редкий и выше
            bool guaranteeOk = true;
            for (int n = 0; n < 1000; n++)
            {
                PetRarity r = PetRoller.RollRarity(crate, GameBalance.PetGuaranteeEvery - 1, rng, out dummy);
                if (r < PetRarity.Rare) { guaranteeOk = false; break; }
            }
            // и при счётчике 8 гарантия ещё НЕ действует (должна иногда выпадать обычная редкость, если она возможна)
            bool notEarly = true;
            if (odds[0] + odds[1] > 0)
            {
                bool sawLow = false;
                for (int n = 0; n < 1000 && !sawLow; n++)
                    if (PetRoller.RollRarity(crate, GameBalance.PetGuaranteeEvery - 2, rng, out dummy) < PetRarity.Rare) sawLow = true;
                notEarly = sawLow;
            }
            if (!guaranteeOk || !notEarly) errors++;
            log.AppendLine("Гарантия на " + GameBalance.PetGuaranteeEvery + "-й клети: " + (guaranteeOk && notEarly ? "OK" : "ОШИБКА"));

            // 4. длинная серия со счётчиком: подряд без Редкого не больше PetGuaranteeEvery - 1, плюс реальная доля Редких и выше
            int pity = 0, streak = 0, maxStreak = 0, rarePlus = 0;
            for (int n = 0; n < Samples; n++)
            {
                PetRarity r = PetRoller.RollRarity(crate, pity, rng, out pity);
                if (r >= PetRarity.Rare) { rarePlus++; streak = 0; }
                else { streak++; if (streak > maxStreak) maxStreak = streak; }
            }
            bool streakOk = maxStreak <= GameBalance.PetGuaranteeEvery - 1;
            if (!streakOk) errors++;
            log.AppendLine("Самая длинная серия без Редкого: " + maxStreak + " (максимум " + (GameBalance.PetGuaranteeEvery - 1) + ") " + (streakOk ? "OK" : "ОШИБКА"));
            log.AppendLine("С гарантией Редких и выше реально: " + (rarePlus * 100.0 / Samples).ToString("F1") + "% (в таблице " + (odds[2] + odds[3]) + "%)");
        }

        // 5. слоты и стадии роста
        bool lv = GameBalance.PetSlotsAt(1) == 1 && GameBalance.PetSlotsAt(4) == 1 && GameBalance.PetSlotsAt(5) == 2
               && GameBalance.PetSlotsAt(6) == 2 && GameBalance.PetSlotsAt(7) == 3 && GameBalance.PetSlotsAt(10) == 3
               && GameBalance.PetGrowthStage(4) == 0 && GameBalance.PetGrowthStage(5) == 1
               && GameBalance.PetGrowthStage(7) == 1 && GameBalance.PetGrowthStage(8) == 2;
        if (!lv) errors++;
        log.AppendLine("== Слоты (1/2/3 на ур. 1/5/7) и рост (малыш/подросток/взрослый на ур. 1/5/8): " + (lv ? "OK" : "ОШИБКА"));

        string result = errors == 0 ? "ВСЕ ПРОВЕРКИ ПРОЙДЕНЫ" : "ОШИБОК: " + errors;
        Debug.Log("[Питомцы] " + result + "\n" + log);
        EditorUtility.DisplayDialog("Проверка шансов клетей", result + "\n\nПодробности в Console.", "OK");
    }
}
