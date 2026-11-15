// Assets/_Project/Editor/Tests/PetBonusSelfTest.cs (Editor)
// Проверка расчёта бонусов питомцев. Сцена не нужна; временные ScriptableObject удаляются и при ошибке.
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class PetBonusSelfTest
{
    [MenuItem("Tools/BuilderSim/Отладка и тесты/Проверки/Питомцы: бонусы", false, 1012)]
    public static void Run()
    {
        int errors = 0;
        var log = new StringBuilder();
        var temporaryAssets = new List<Object>();

        void Check(string name, bool ok)
        {
            if (!ok) errors++;
            log.AppendLine((ok ? "OK      " : "ОШИБКА  ") + name);
        }

        try
        {
            // Один обычный питомец на рубку: +3%.
            PetBonusSet a = PetBonus.Compute(Pets(temporaryAssets, P(PetStat.ChopPower, PetRarity.Common)));
            Check("Обычный на рубку даёт 0.03", Near(a.chop, 0.03f) && a.capacityLogs == 0 && Near(a.saw, 0f));

            // Три легендарных на цену продажи: 36% режется до 25%.
            PetBonusSet b = PetBonus.Compute(Pets(temporaryAssets,
                P(PetStat.Haggle, PetRarity.Legendary), P(PetStat.Haggle, PetRarity.Legendary),
                P(PetStat.Haggle, PetRarity.Legendary)));
            Check("Потолок +25% на характеристику", Near(b.haggle, GameBalance.PetBonusCap));

            Check("Рюкзак: Обычный +1", PetBonus.Compute(Pets(temporaryAssets,
                P(PetStat.Capacity, PetRarity.Common))).capacityLogs == 1);
            Check("Рюкзак: Легендарный +2", PetBonus.Compute(Pets(temporaryAssets,
                P(PetStat.Capacity, PetRarity.Legendary))).capacityLogs == 2);
            Check("Рюкзак: три Легендарных (потолок) +4", PetBonus.Compute(Pets(temporaryAssets,
                P(PetStat.Capacity, PetRarity.Legendary), P(PetStat.Capacity, PetRarity.Legendary),
                P(PetStat.Capacity, PetRarity.Legendary))).capacityLogs == 4);

            Check("Пустой список даёт нули", PetBonus.Compute(new List<PetData>()).capacityLogs == 0 &&
                Near(PetBonus.Compute(null).chop, 0f));

            PetBonusSet c = PetBonus.Compute(Pets(temporaryAssets,
                P(PetStat.MoveSpeed, PetRarity.Rare), P(PetStat.WorkerIncome, PetRarity.Uncommon)));
            Check("Разные характеристики независимы", Near(c.move, 0.08f) &&
                Near(c.worker, 0.05f) && Near(c.chop, 0f));

            TalentBonus t = TalentBonus.None;
            PetBonus.ApplyTo(ref t, PetBonus.Compute(Pets(temporaryAssets,
                P(PetStat.ChopPower, PetRarity.Rare), P(PetStat.SawSpeed, PetRarity.Uncommon),
                P(PetStat.MoveSpeed, PetRarity.Common), P(PetStat.DoubleYield, PetRarity.Legendary),
                P(PetStat.Capacity, PetRarity.Rare))));
            Check("ApplyTo: время рубки ×0.92, пилы ×0.95",
                Near(t.chopMul, 0.92f) && Near(t.sawMul, 0.95f));
            Check("ApplyTo: бег +0.03, двойная +0.12, рюкзак +2",
                Near(t.speedAdd, 0.03f) && Near(t.doubleAdd, 0.12f) && t.capAdd == 2);

            Check("PlayerUpgrades.Calc видит бонус: рубка 2.76 с",
                Near(PlayerUpgrades.Calc(UpgradeType.ChopPower, 0, t), 3f * 0.92f));
        }
        finally
        {
            // Do not leak temporary ScriptableObjects if one of the checks throws.
            foreach (Object item in temporaryAssets)
                if (item != null) Object.DestroyImmediate(item);
        }

        string result = errors == 0 ? "ВСЕ ПРОВЕРКИ ПРОЙДЕНЫ" : "ОШИБОК: " + errors;
        Debug.Log("[Питомцы] Бонусы: " + result + "\n" + log);
        EditorUtility.DisplayDialog("Проверка бонусов питомцев", result + "\n\nПодробности в Console.", "OK");
    }

    // Temporary PetData objects are kept in memory, never written to disk.
    static PetData P(PetStat stat, PetRarity rarity)
    {
        PetData data = ScriptableObject.CreateInstance<PetData>();
        data.stat = stat;
        data.rarity = rarity;
        return data;
    }

    static List<PetData> Pets(List<Object> temporaryAssets, params PetData[] pets)
    {
        for (int i = 0; i < pets.Length; i++) temporaryAssets.Add(pets[i]);
        return new List<PetData>(pets);
    }

    static bool Near(float x, float y) => Mathf.Abs(x - y) < 0.0005f;
}
