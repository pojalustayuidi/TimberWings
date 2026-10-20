#if UNITY_EDITOR
// Assets/_Project/Editor/Tests/WorkerProductionSelfTest.cs (Editor)
// Проверка производства лагеря: Tools > BuilderSim > Рабочие > Проверить производство.
// Гоняет чистую CampSimulation (без сцены) с фиксированным seed, результат воспроизводим. Отчёт в Console и в диалоге.
// Покрывает: пильщик без бревен (стоит, опыта нет), переполнение склада бревен и досок (производство останавливается),
// потолки перков, лимиты слотов и ролей, опыт и уровни, шансы перков, возврат бревен при снятии пильщика.
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class WorkerProductionSelfTest
{
    [MenuItem("Tools/BuilderSim/Рабочие/Проверить производство")]
    public static void RunMenu()
    {
        var log = new StringBuilder();
        int errors = Run(log);
        string result = errors == 0 ? "ВСЕ ПРОВЕРКИ ПРОЙДЕНЫ" : "ОШИБОК: " + errors;
        if (errors > 0) Debug.LogError("[Рабочие] " + result + "\n" + log); else Debug.Log("[Рабочие] " + result + "\n" + log);
        EditorUtility.DisplayDialog("Проверка производства рабочих", result + "\n\nПодробности в Console.", "OK");
    }

    static int errors;
    static StringBuilder sb;

    static void Check(bool ok, string name, string detail = "")
    {
        if (!ok) errors++;
        sb.AppendLine((ok ? "OK      " : "ОШИБКА  ") + name + (detail.Length > 0 ? ": " + detail : ""));
    }

    static bool Near(double a, double b, double tol) { return Math.Abs(a - b) <= tol; }

    // Рабочий сразу на максимальном уровне (множитель выработки постоянный)
    static void MaxLevel(WorkerState w)
    {
        w.totalXp = WorkerBalance.TotalXpForLevel(WorkerBalance.MaxLevel);
        w.level = WorkerBalance.MaxLevel;
    }

    static CampSimulation NewSim() { return new CampSimulation(new System.Random(12345)); }

    static WorkerState Add(CampSimulation sim, string id, WorkerRole role, WorkerRarity rarity, params WorkerPerk[] perks)
    {
        var w = new WorkerState(id, role, rarity, perks);
        sim.AddToRoster(w);
        sim.AutoAssign(w);
        return w;
    }

    // Гоняет симуляцию seconds секунд шагами по 0.25 с. drainLogs/drainPlanks: игрок «забирает» склад каждый шаг
    static void Run(CampSimulation sim, double seconds, bool drainLogs = false, bool drainPlanks = false)
    {
        int steps = (int)Math.Round(seconds / 0.25);
        for (int i = 0; i < steps; i++)
        {
            sim.Step(0.25f);
            if (drainLogs) sim.TakeLogs(100000);
            if (drainPlanks) sim.TakePlanks(100000);
        }
    }

    public static int Run(StringBuilder log)
    {
        errors = 0; sb = log;

        // 1. Пильщик без бревен: стоит, опыта нет
        {
            var sim = NewSim();
            var saw = Add(sim, "s", WorkerRole.Sawyer, WorkerRarity.Common);
            Run(sim, 120);
            Check(sim.planks == 0 && saw.totalXp == 0 && saw.level == 1 && !saw.sawActive, "Пильщик без бревен стоит, опыта нет",
                  "досок " + sim.planks + ", XP " + saw.totalXp);
        }

        // 2. Пильщик с бревнами: 4 бревна = 2 доски за 20 с, опыт за 20 с работы
        {
            var sim = NewSim();
            var saw = Add(sim, "s", WorkerRole.Sawyer, WorkerRarity.Common);
            sim.logs = 4;
            Run(sim, 20);
            Check(sim.planks == 2 && sim.logs == 0 && Near(saw.totalXp, 10.0, 1e-6), "Пильщик: 4 бревна → 2 доски за 20 с, XP 10",
                  "досок " + sim.planks + ", бревен " + sim.logs + ", XP " + saw.totalXp);
            Run(sim, 30);
            Check(sim.planks == 2 && Near(saw.totalXp, 10.0, 1e-6), "Бревна кончились: пильщик снова стоит, XP не растёт");
        }

        // 3. Переполнение склада бревен: производство останавливается, опыт не идёт
        {
            var sim = NewSim();
            var cap = new List<int>();
            sim.StockChanged += () => cap.Add(sim.logs);
            var a = Add(sim, "a", WorkerRole.Lumberjack, WorkerRarity.Common);
            Add(sim, "b", WorkerRole.Lumberjack, WorkerRarity.Common);
            Add(sim, "c", WorkerRole.Lumberjack, WorkerRarity.Common);
            Run(sim, 100);                       // 0.6 бревна/с: за 50 с склад полон
            bool never = true; for (int i = 0; i < cap.Count; i++) if (cap[i] > WorkerBalance.BaseLogStock) never = false;
            double xpFull = a.totalXp;
            Run(sim, 60);
            Check(sim.logs == WorkerBalance.BaseLogStock && never, "Склад бревен 30/30, выше не растёт", "бревен " + sim.logs);
            Check(Near(a.totalXp, xpFull, 1e-9), "Склад полон: дровосек стоит, опыта нет", "XP " + xpFull + " → " + a.totalXp);
            sim.TakeLogs(10);
            Run(sim, 5);
            Check(a.totalXp > xpFull && sim.logs > 20, "После «Забрать» дровосеки снова работают");
        }

        // 4. Переполнение склада досок: пильщик не стартует, бревна не тратятся, опыта нет
        {
            var sim = NewSim();
            var saw = Add(sim, "s", WorkerRole.Sawyer, WorkerRarity.Common);
            sim.logs = 30; sim.planks = WorkerBalance.BasePlankStock;
            Run(sim, 60);
            Check(sim.planks == 10 && sim.logs == 30 && saw.totalXp == 0, "Склад досок 10/10: пильщик стоит, бревна целы, опыта нет");

            var sim2 = NewSim();
            var saw2 = Add(sim2, "s", WorkerRole.Sawyer, WorkerRarity.Common);
            sim2.logs = 4; sim2.planks = 9;
            Run(sim2, 100);
            Check(sim2.planks == 10 && sim2.logs == 2 && Near(saw2.totalXp, 5.0, 1e-6), "Последняя доска влезла, следующая не началась",
                  "досок " + sim2.planks + ", бревен " + sim2.logs + ", XP " + saw2.totalXp);
        }

        // 5. Вместимость склада растёт от «Вместительный» и «Запасливый» (только у рабочих в слотах)
        {
            var sim = NewSim();
            var j = Add(sim, "j", WorkerRole.Lumberjack, WorkerRarity.Rare, WorkerPerk.Roomy);
            var s = Add(sim, "s", WorkerRole.Sawyer, WorkerRarity.Rare, WorkerPerk.Stocker);
            Check(sim.LogCap == 50 && sim.PlankCap == 20, "«Вместительный» +20 бревен, «Запасливый» +10 досок", sim.LogCap + " / " + sim.PlankCap);
            sim.Unassign(sim.SlotOf(j));
            Check(sim.LogCap == 30 && sim.PlankCap == 20, "Рабочий убран из слота: его перк склада не действует");
        }

        // 6. Потолки перков. Режим «перки действуют на всю роль» (там потолки срабатывают)
        {
            var sim = NewSim(); sim.stackAcrossRole = true;
            for (int i = 0; i < 3; i++) Add(sim, "saw" + i, WorkerRole.Sawyer, WorkerRarity.Common, WorkerPerk.FastSaw);
            var w = sim.SlotAt(0);
            Check(Near(sim.SawTime(w), 6.0, 1e-3), "3 × «Быстрая пила» (−45%) режется до −40%", sim.SawTime(w).ToString("F3") + " с на доску (нужно 6.000)");

            var sim2 = NewSim(); sim2.stackAcrossRole = true;
            for (int i = 0; i < 3; i++) Add(sim2, "j" + i, WorkerRole.Lumberjack, WorkerRarity.Common, WorkerPerk.HeavyHand);
            Check(Near(sim2.LumberRate(sim2.SlotAt(0)), 0.28, 1e-4), "3 × «Тяжёлая рука» (+45%) режется до +40%", sim2.LumberRate(sim2.SlotAt(0)).ToString("F3") + " бр/с (нужно 0.280)");

            var sim3 = NewSim();   // обычный режим: перк только у владельца
            Add(sim3, "a", WorkerRole.Sawyer, WorkerRarity.Common, WorkerPerk.FastSaw);
            Add(sim3, "b", WorkerRole.Sawyer, WorkerRarity.Common);
            Check(Near(sim3.SawTime(sim3.SlotAt(0)), 8.5, 1e-3) && Near(sim3.SawTime(sim3.SlotAt(1)), 10.0, 1e-3),
                  "Обычный режим: «Быстрая пила» только у владельца", sim3.SawTime(sim3.SlotAt(0)).ToString("F2") + " и " + sim3.SawTime(sim3.SlotAt(1)).ToString("F2"));
        }
        // «Наставник»: +5% каждый, всего не больше +15%, действует на всех рабочих
        {
            var sim = NewSim();
            for (int i = 0; i < 3; i++) Add(sim, "m" + i, WorkerRole.Clerk, WorkerRarity.Common, WorkerPerk.Mentor);
            var lj = Add(sim, "lj", WorkerRole.Lumberjack, WorkerRarity.Common);
            Check(Near(sim.MentorBonus, 0.15, 1e-6) && Near(sim.ClerkRate(sim.SlotAt(0)), 0.45 * 1.15, 1e-5) && Near(sim.LumberRate(lj), 0.23, 1e-5),
                  "3 «Наставника» = +15% всем рабочим", "приказчик " + sim.ClerkRate(sim.SlotAt(0)).ToString("F4") + ", дровосек " + sim.LumberRate(lj).ToString("F3"));
        }

        // 7. Лимиты: 3 на роль, 6 слотов, замена слота
        {
            var sim = NewSim();
            var clerks = new List<WorkerState>();
            for (int i = 0; i < 4; i++)
            {
                var w = new WorkerState("c" + i, WorkerRole.Clerk, WorkerRarity.Common, null);
                sim.AddToRoster(w); clerks.Add(w); sim.AutoAssign(w);
            }
            Check(sim.CountInSlots(WorkerRole.Clerk) == 3 && sim.SlotOf(clerks[3]) == -1 && sim.CanAssign(clerks[3], 5) == AssignResult.RoleFull,
                  "Не больше 3 рабочих одной роли в слотах");
            for (int i = 0; i < 3; i++) Add(sim, "l" + i, WorkerRole.Lumberjack, WorkerRarity.Common);
            var saw = Add(sim, "s", WorkerRole.Sawyer, WorkerRarity.Common);
            Check(sim.WorkingCount == 6 && sim.SlotOf(saw) == -1 && sim.AutoAssign(saw) == AssignResult.NoFreeSlot, "Всего 6 слотов");
            Check(sim.Assign(saw, 0) == AssignResult.Ok && sim.SlotOf(saw) == 0 && sim.SlotOf(clerks[0]) == -1 && sim.CountInSlots(WorkerRole.Clerk) == 2,
                  "Рабочий встаёт в занятый слот, прежний уходит в запас");
        }

        // 8. Опыт и уровни
        {
            var sim = NewSim();
            var w = Add(sim, "w", WorkerRole.Clerk, WorkerRarity.Common);
            int ups = 0; sim.LevelUp += x => ups++;
            sim.AddXp(w, 119); bool still1 = w.level == 1 && ups == 0;
            sim.AddXp(w, 1);
            Check(still1 && w.level == 2 && ups == 1, "120 XP = 2 уровень, событие уровня один раз");
            sim.AddXp(w, 1000000);
            Check(w.level == 10 && w.IsMax && Near(w.totalXp, 4764, 1e-6) && sim.AddXp(w, 100) == 0, "Максимум 10 уровень, опыт выше не копится");

            var sim2 = NewSim();
            var c = Add(sim2, "c", WorkerRole.Clerk, WorkerRarity.Common);
            Run(sim2, 240);
            Check(c.level == 2 && Near(c.totalXp, 120, 1e-6), "Приказчик: 240 с работы = 120 XP (0.5 XP/с)", "уровень " + c.level + ", XP " + c.totalXp);
        }

        // 9. Доход Приказчика: 100 с = 45 монет
        {
            var sim = NewSim();
            Add(sim, "c", WorkerRole.Clerk, WorkerRarity.Common);
            Run(sim, 100);
            Check(sim.pendingCoins == 45, "Обычный Приказчик: 100 с = 45 монет", sim.pendingCoins + " монет");
        }

        // 10. Снятие пильщика посреди доски возвращает бревна
        {
            var sim = NewSim();
            var saw = Add(sim, "s", WorkerRole.Sawyer, WorkerRarity.Common);
            sim.logs = 2;
            Run(sim, 5);
            bool taken = sim.logs == 0 && saw.sawActive;
            sim.Unassign(sim.SlotOf(saw));
            Check(taken && sim.logs == 2 && !saw.sawActive && saw.sawProgress == 0, "Пильщика сняли посреди доски: 2 бревна вернулись на склад");
        }

        // 11. Золотое дерево: раз в 120 с (у мифика раз в 60 с), пачка из 6
        {
            int golden = 0;
            var sim = NewSim(); sim.Produced += p => { if (p.special && p.amount == WorkerBalance.GoldenTreeLogs) golden++; };
            Add(sim, "j", WorkerRole.Lumberjack, WorkerRarity.Common, WorkerPerk.GoldenTree);
            Run(sim, 119, true);
            bool none = golden == 0;
            Run(sim, 2, true);
            Check(none && golden == 1, "«Золотое дерево»: пачка в 6 бревен на 120-й секунде");

            int gm = 0;
            var sim2 = NewSim(); sim2.Produced += p => { if (p.special && p.amount == WorkerBalance.GoldenTreeLogs) gm++; };
            Add(sim2, "m", WorkerRole.Lumberjack, WorkerRarity.Mythic, WorkerPerk.GoldenTree);
            Run(sim2, 121, true);
            Check(gm == 2, "«Поющий кедр» (мифик): пачка каждые 60 с", "пачек за 121 с: " + gm);
        }

        // 12. Шансы перков (seed фиксирован, допуск 6-8%). Рабочие сразу на максимальном уровне, чтобы выработка не менялась по ходу теста
        {
            var sim = NewSim(); long logsMade = 0; sim.Produced += p => { if (p.resource == CampResource.Logs) logsMade += p.amount; };
            var j = Add(sim, "j", WorkerRole.Lumberjack, WorkerRarity.Common, WorkerPerk.DoubleStrike); MaxLevel(j);
            double expectLogs = sim.LumberRate(j) * 2000 * (1 + WorkerBalance.DoubleStrikeChance);
            Run(sim, 2000, true);
            Check(Near(logsMade, expectLogs, expectLogs * 0.06), "«Двойной удар» 10%: за 2000 с около " + Mathf.RoundToInt((float)expectLogs) + " бревен", logsMade.ToString());

            var sim2 = NewSim();
            var c = Add(sim2, "c", WorkerRole.Clerk, WorkerRarity.Common, WorkerPerk.LuckyDeal); MaxLevel(c);
            double expect = sim2.ClerkRate(c) * 3000 * (1 + WorkerBalance.LuckyDealChance * (WorkerBalance.LuckyDealMult - 1));
            Run(sim2, 3000);
            Check(Near(sim2.pendingCoins, expect, expect * 0.06), "«Счастливая сделка» 8% ×3: за 3000 с около " + Mathf.RoundToInt((float)expect) + " монет", sim2.pendingCoins.ToString());

            var sim3 = NewSim(); long used = 0, made = 0;
            var s3 = Add(sim3, "s", WorkerRole.Sawyer, WorkerRarity.Common, WorkerPerk.Thrifty); MaxLevel(s3);
            sim3.Produced += p => { if (p.resource == CampResource.Planks) made += p.amount; };
            double cycles = 3000 / sim3.SawTime(s3);
            double expectUsed = cycles * WorkerBalance.SawyerLogsPerPlank * (1 - WorkerBalance.ThriftyChance);
            for (int i = 0; i < 12000; i++) { sim3.logs = 100; sim3.Step(0.25f); used += 100 - sim3.logs; sim3.TakePlanks(1000); }
            Check(Near(used, expectUsed, expectUsed * 0.08), "«Экономный» 15%: на ~" + Mathf.RoundToInt((float)cycles) + " досок уходит около " + Mathf.RoundToInt((float)expectUsed) + " бревен",
                  "потрачено " + used + ", досок " + made);
        }

        return errors;
    }
}
#endif
