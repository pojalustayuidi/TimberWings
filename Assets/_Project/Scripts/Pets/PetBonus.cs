// Assets/_Project/Scripts/Pets/PetBonus.cs (НЕ в Editor)
// Суммарный бонус экипированных питомцев. Чистая логика без сцены (проверяется меню «Проверить бонусы»).
//
// Правила (GDD 4.10): бонус одного питомца зависит от редкости (GameBalance.PetBonusByRarity).
// Бонусы одной характеристики складываются, сумма не больше GameBalance.PetBonusCap (+25%).
// Как это ложится на значения игрока (через TalentBonus, то есть через те же формулы PlayerUpgrades.Calc):
//   ChopPower   время рубки      × (1 − доля)
//   SawSpeed    время доски      × (1 − доля)
//   MoveSpeed   скорость бега    + доля
//   DoubleYield шанс двойной     + доля (в долях шанса: +0.05 = +5 п.п.)
//   Haggle      цена продажи     + доля
//   Capacity    рюкзак           + max(1, вверх(доля × PetCapacityBase)) бревен, если есть хоть один питомец с этой характеристикой
//   WorkerIncome доход рабочих   × (1 + доля), считается в WorkerManager.IncomeOf
using System.Collections.Generic;
using UnityEngine;

public struct PetBonusSet
{
    public float chop, move, doubleYield, haggle, saw, worker;   // доли 0..PetBonusCap
    public int capacityLogs;                                      // бревна к рюкзаку

    public static PetBonusSet None { get { return new PetBonusSet(); } }

    public float Get(PetStat s)
    {
        switch (s)
        {
            case PetStat.ChopPower: return chop;
            case PetStat.MoveSpeed: return move;
            case PetStat.DoubleYield: return doubleYield;
            case PetStat.Haggle: return haggle;
            case PetStat.SawSpeed: return saw;
            case PetStat.WorkerIncome: return worker;
            default: return 0f;
        }
    }
}

public static class PetBonus
{
    // считает суммарный бонус по списку питомцев (null и пустые места пропускаются)
    // stars (необязательно): звёзды питомца с тем же индексом. bookAll: бонус «Книги питомцев» ко всем процентным характеристикам
    public static PetBonusSet Compute(IList<PetData> pets, IList<int> stars = null, float bookAll = 0f)
    {
        float chop = 0f, move = 0f, dbl = 0f, haggle = 0f, saw = 0f, worker = 0f, cap = 0f;
        bool hasCapacity = false;

        if (pets != null)
        {
            for (int i = 0; i < pets.Count; i++)
            {
                PetData p = pets[i];
                if (p == null) continue;
                int st = stars != null && i < stars.Count ? stars[i] : 0;
                float b = GameBalance.PetBonus(p.rarity, st);
                switch (p.stat)
                {
                    case PetStat.ChopPower: chop += b; break;
                    case PetStat.MoveSpeed: move += b; break;
                    case PetStat.DoubleYield: dbl += b; break;
                    case PetStat.Haggle: haggle += b; break;
                    case PetStat.SawSpeed: saw += b; break;
                    case PetStat.WorkerIncome: worker += b; break;
                    case PetStat.Capacity: cap += b; hasCapacity = true; break;
                }
            }
        }

        chop += bookAll; move += bookAll; dbl += bookAll; haggle += bookAll; saw += bookAll; worker += bookAll;

        float max = GameBalance.PetBonusCap;
        var set = new PetBonusSet
        {
            chop = Mathf.Min(chop, max),
            move = Mathf.Min(move, max),
            doubleYield = Mathf.Min(dbl, max),
            haggle = Mathf.Min(haggle, max),
            saw = Mathf.Min(saw, max),
            worker = Mathf.Min(worker, max),
            capacityLogs = 0
        };

        if (hasCapacity)
        {
            float f = Mathf.Min(cap, max);
            // 1e-4 защищает от шума float (0.25 × 16 не должно стать 5)
            set.capacityLogs = Mathf.Max(1, Mathf.CeilToInt(f * GameBalance.PetCapacityBase - 1e-4f));
        }
        return set;
    }

    // «ценность» набора для автоподбора: сумма процентов по характеристикам (уже с потолком) + рюкзак в долях от PetCapacityBase
    public static float Score(PetBonusSet b)
    {
        return b.chop + b.move + b.doubleYield + b.haggle + b.saw + b.worker + b.capacityLogs / (float)GameBalance.PetCapacityBase;
    }

    // Заменяет в уже посчитанных бонусах игрока вклад питомцев «from» на «to» (для живых значений в окне коллекции).
    // Обратимо: множители времени делятся на (1 − доля), прибавки вычитаются.
    public static TalentBonus Rebase(TalentBonus b, PetBonusSet from, PetBonusSet to)
    {
        b.chopMul = b.chopMul / Mathf.Max(0.01f, 1f - from.chop) * (1f - to.chop);
        b.sawMul = b.sawMul / Mathf.Max(0.01f, 1f - from.saw) * (1f - to.saw);
        b.speedAdd += to.move - from.move;
        b.doubleAdd += to.doubleYield - from.doubleYield;
        b.sellAdd += to.haggle - from.haggle;
        b.capAdd += to.capacityLogs - from.capacityLogs;
        return b;
    }

    // добавляет бонусы питомцев к бонусам талантов (вызывается из PlayerUpgrades.RecalcBonus)
    public static void ApplyTo(ref TalentBonus b, PetBonusSet p)
    {
        b.chopMul *= 1f - p.chop;
        b.sawMul *= 1f - p.saw;
        b.speedAdd += p.move;
        b.doubleAdd += p.doubleYield;
        b.sellAdd += p.haggle;
        b.capAdd += p.capacityLogs;
    }
}
