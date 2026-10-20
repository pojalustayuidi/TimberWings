// Assets/_Project/Editor/Tests/PetBonusSelfTest.cs (Editor)
// Проверка расчёта бонусов питомцев: Tools > BuilderSim > Питомцы > Проверить бонусы. Сцена не нужна.
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class PetBonusSelfTest
{
    static int errors;
    static StringBuilder log;

    [MenuItem("Tools/BuilderSim/Питомцы/Проверить бонусы")]
    public static void Run()
    {
        errors = 0;
        log = new StringBuilder();
        var temp = new List<Object>();

        // один Обычный на рубку: +3%
        PetBonusSet a = PetBonus.Compute(Pets(temp, P(PetStat.ChopPower, PetRarity.Common)));
        Check("Обычный на рубку даёт 0.03", Near(a.chop, 0.03f) && a.capacityLogs == 0 && Near(a.saw, 0f));

        // три Легендарных на цену продажи: 36% режется до потолка 25%
        PetBonusSet b = PetBonus.Compute(Pets(temp,
            P(PetStat.Haggle, PetRarity.Legendary), P(PetStat.Haggle, PetRarity.Legendary), P(PetStat.Haggle, PetRarity.Legendary)));
        Check("Потолок +25% на характеристику", Near(b.haggle, GameBalance.PetBonusCap));

        // рюкзак: один Обычный = +1, один Легендарный = +2, три Легендарных (потолок) = +4
        Check("Рюкзак: Обычный +1", PetBonus.Compute(Pets(temp, P(PetStat.Capacity, PetRarity.Common))).capacityLogs == 1);
        Check("Рюкзак: Легендарный +2", PetBonus.Compute(Pets(temp, P(PetStat.Capacity, PetRarity.Legendary))).capacityLogs == 2);
        Check("Рюкзак: три Легендарных (потолок) +4", PetBonus.Compute(Pets(temp,
            P(PetStat.Capacity, PetRarity.Legendary), P(PetStat.Capacity, PetRarity.Legendary), P(PetStat.Capacity, PetRarity.Legendary))).capacityLogs == 4);

        // пусто и null
        Check("Пустой список даёт нули", PetBonus.Compute(new List<PetData>()).capacityLogs == 0 && Near(PetBonus.Compute(null).chop, 0f));

        // разные характеристики не смешиваются
        PetBonusSet c = PetBonus.Compute(Pets(temp, P(PetStat.MoveSpeed, PetRarity.Rare), P(PetStat.WorkerIncome, PetRarity.Uncommon)));
        Check("Разные характеристики независимы", Near(c.move, 0.08f) && Near(c.worker, 0.05f) && Near(c.chop, 0f));

        // применение к TalentBonus
        TalentBonus t = TalentBonus.None;
        PetBonus.ApplyTo(ref t, PetBonus.Compute(Pets(temp, P(PetStat.ChopPower, PetRarity.Rare), P(PetStat.SawSpeed, PetRarity.Uncommon),
            P(PetStat.MoveSpeed, PetRarity.Common), P(PetStat.DoubleYield, PetRarity.Legendary), P(PetStat.Capacity, PetRarity.Rare))));
        Check("ApplyTo: время рубки ×0.92, пилы ×0.95", Near(t.chopMul, 0.92f) && Near(t.sawMul, 0.95f));
        Check("ApplyTo: бег +0.03, двойная +0.12, рюкзак +2", Near(t.speedAdd, 0.03f) && Near(t.doubleAdd, 0.12f) && t.capAdd == 2);

        // итог в формуле игрока: время рубки 3 с × 0.92 = 2.76 с
        Check("PlayerUpgrades.Calc видит бонус: рубка 2.76 с", Near(PlayerUpgrades.Calc(UpgradeType.ChopPower, 0, t), 3f * 0.92f));

        for (int i = 0; i < temp.Count; i++) Object.DestroyImmediate(temp[i]);

        string result = errors == 0 ? "ВСЕ ПРОВЕРКИ ПРОЙДЕНЫ" : "ОШИБОК: " + errors;
        Debug.Log("[Питомцы] Бонусы: " + result + "\n" + log);
        EditorUtility.DisplayDialog("Проверка бонусов питомцев", result + "\n\nПодробности в Console.", "OK");
    }

    // временный PetData только для проверки (на диск не сохраняется)
    static PetData P(PetStat stat, PetRarity rarity)
    {
        PetData d = ScriptableObject.CreateInstance<PetData>();
        d.stat = stat;
        d.rarity = rarity;
        return d;
    }

    static List<PetData> Pets(List<Object> temp, params PetData[] pets)
    {
        for (int i = 0; i < pets.Length; i++) temp.Add(pets[i]);
        return new List<PetData>(pets);
    }

    static bool Near(float x, float y) { return Mathf.Abs(x - y) < 0.0005f; }

    static void Check(string name, bool ok)
    {
        if (!ok) errors++;
        log.AppendLine((ok ? "OK      " : "ОШИБКА  ") + name);
    }
}
