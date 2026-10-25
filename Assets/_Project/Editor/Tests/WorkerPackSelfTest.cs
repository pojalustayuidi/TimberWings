#if UNITY_EDITOR
// Assets/_Project/Editor/Tests/WorkerPackSelfTest.cs (Editor)
// Проверка паков рабочих: Tools > BuilderSim > Рабочие > Проверить паки. Сцена не нужна, база строится в памяти (ассеты не трогаются).
// Покрывает: суммы шансов = 100; доли редкостей на 100 000 паков каждого типа (без гарантий строго по таблице, с гарантиями не ниже таблицы);
// предельные промежутки между Редким+/Легендарным+/Мификом; Мифик ровно на 100-м Гильдейском (генератор, который сам Мифика не даёт);
// раздельные счётчики; подарочный пак; дубликаты (опыт и монеты на максимуме); событие OnPackOpened.
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class WorkerPackSelfTest
{
    const int N = 100000;

    [MenuItem("Tools/BuilderSim/Отладка и тесты/Проверки/Рабочие: паки", false, 1010)]
    public static void RunMenu()
    {
        var log = new StringBuilder();
        int errors = Run(log);
        string result = errors == 0 ? "ВСЕ ПРОВЕРКИ ПРОЙДЕНЫ" : "ОШИБОК: " + errors;
        if (errors > 0) Debug.LogError("[Паки рабочих] " + result + "\n" + log); else Debug.Log("[Паки рабочих] " + result + "\n" + log);
        EditorUtility.DisplayDialog("Проверка паков рабочих", result + "\n\nПодробности в Console.", "OK");
    }

    static int errors;
    static StringBuilder sb;

    static void Check(bool ok, string name, string detail = "")
    {
        if (!ok) errors++;
        sb.AppendLine((ok ? "OK      " : "ОШИБКА  ") + name + (detail.Length > 0 ? ": " + detail : ""));
    }

    // Генератор, который всегда возвращает одно число: так видно, что именно делает гарантия, а не удача
    class FixedRandom : System.Random
    {
        readonly double v;
        public FixedRandom(double v) { this.v = v; }
        public override double NextDouble() { return v; }
        protected override double Sample() { return v; }
    }

    static string[] Types = { "Артельный", "Мастерской", "Гильдейский" };

    // База в памяти: 3 роли × (3 обычных, 2 необычных, 2 редких, 1 легендарный, 1 мифический) = 27
    static WorkerDatabase MakeDb(List<UnityEngine.Object> junk)
    {
        var db = ScriptableObject.CreateInstance<WorkerDatabase>();
        junk.Add(db);
        for (int role = 0; role < 3; role++)
            for (int rar = 0; rar < 5; rar++)
                for (int k = 0; k < WorkerBalance.PoolPerRarity[rar]; k++)
                {
                    var d = ScriptableObject.CreateInstance<WorkerDefinition>();
                    junk.Add(d);
                    d.id = "t_" + role + "_" + rar + "_" + k;
                    d.firstName = "T" + role + rar + k; d.lastName = "Test";
                    d.role = (WorkerRole)role; d.rarity = (WorkerRarity)rar;
                    db.workers.Add(d);
                }
        return db;
    }

    public static int Run(StringBuilder log)
    {
        errors = 0; sb = log;
        var junk = new List<UnityEngine.Object>();
        try { RunAll(junk); }
        finally { for (int i = 0; i < junk.Count; i++) if (junk[i] != null) UnityEngine.Object.DestroyImmediate(junk[i]); }
        return errors;
    }

    static void RunAll(List<UnityEngine.Object> junk)
    {
        var db = MakeDb(junk);

        // 1. Таблицы: суммы шансов 100, число персонажей
        for (int t = 0; t < 3; t++)
            Check(Math.Abs(WorkerBalance.OddsSum((WorkerPackType)t) - 100f) < 1e-4f, "Сумма шансов «" + Types[t] + "» пака = 100", WorkerBalance.OddsSum((WorkerPackType)t).ToString());
        Check(db.workers.Count == WorkerBalance.PoolTotal, "Тестовая база 27 персонажей", db.workers.Count.ToString());

        // 2. Чистые шансы (без гарантий): доли на 100 000 паков в допуске 4σ
        for (int t = 0; t < 3; t++)
        {
            var type = (WorkerPackType)t;
            var rng = new System.Random(1000 + t);
            var st = new WorkerPackState();
            int[] got = new int[5];
            for (int i = 0; i < N; i++)
            {
                PackGuarantee g;
                got[(int)WorkerPackRoller.RollRarity(type, st, rng, false, out g)]++;
            }
            bool ok = true; var det = new StringBuilder();
            float[] odds = WorkerBalance.OddsOf(type);
            for (int r = 0; r < 5; r++)
            {
                double p = odds[r] / 100.0, share = got[r] / (double)N;
                double tol = 4 * Math.Sqrt(p * (1 - p) / N) + 0.0003;
                if (Math.Abs(share - p) > tol) ok = false;
                det.Append(WorkerText.Rarity((WorkerRarity)r)[0]).Append(' ').Append((share * 100).ToString("F2")).Append("% (по таблице ").Append(odds[r]).Append("%)  ");
            }
            Check(ok, "«" + Types[t] + "»: доли редкостей на " + N + " паков без гарантий", det.ToString());
        }

        // 3. С гарантиями: промежутки между нужными редкостями не больше N, доли не ниже таблицы, считаем реальные доли
        for (int t = 0; t < 3; t++)
        {
            var type = (WorkerPackType)t;
            var rng = new System.Random(2000 + t);
            var st = new WorkerPackState();
            int[] got = new int[5];
            int cRare = 0, cLeg = 0, cMyth = 0, maxRare = 0, maxLeg = 0, maxMyth = 0, triggered = 0;
            for (int i = 0; i < N; i++)
            {
                PackGuarantee g;
                var r = WorkerPackRoller.RollRarity(type, st, rng, true, out g);
                if (g != PackGuarantee.None) triggered++;
                WorkerPackRoller.Advance(type, st, r);
                got[(int)r]++;
                cRare++; if (r >= WorkerRarity.Rare) { if (cRare > maxRare) maxRare = cRare; cRare = 0; }
                cLeg++; if (r >= WorkerRarity.Legendary) { if (cLeg > maxLeg) maxLeg = cLeg; cLeg = 0; }
                cMyth++; if (r >= WorkerRarity.Mythic) { if (cMyth > maxMyth) maxMyth = cMyth; cMyth = 0; }
            }
            if (cRare > maxRare) maxRare = cRare;
            if (cLeg > maxLeg) maxLeg = cLeg;
            if (cMyth > maxMyth) maxMyth = cMyth;
            // maxX = самое долгое ожидание в паках, считая и пак с попаданием (при гарантии на N-м паке оно ровно N)
            Check(maxRare <= WorkerBalance.GuaranteeRareEvery, "«" + Types[t] + "»: Редкий+ не реже чем раз в " + WorkerBalance.GuaranteeRareEvery, "макс. ожидание " + maxRare);
            if (WorkerBalance.HasLegendaryGuarantee(type))
                Check(maxLeg <= WorkerBalance.GuaranteeLegendaryEvery, "«" + Types[t] + "»: Легендарный+ не реже чем раз в " + WorkerBalance.GuaranteeLegendaryEvery, "макс. ожидание " + maxLeg);
            if (WorkerBalance.HasMythicGuarantee(type))
                Check(maxMyth <= WorkerBalance.GuaranteeMythicEvery, "«" + Types[t] + "»: Мифик не реже чем раз в " + WorkerBalance.GuaranteeMythicEvery, "макс. ожидание " + maxMyth);

            float[] odds = WorkerBalance.OddsOf(type);
            double rareNominal = (odds[2] + odds[3] + odds[4]) / 100.0, rareReal = (got[2] + got[3] + got[4]) / (double)N;
            Check(rareReal >= rareNominal - 0.005, "«" + Types[t] + "»: реальная доля Редкого+ не ниже таблицы",
                  (rareReal * 100).ToString("F2") + "% при " + (rareNominal * 100).ToString("F2") + "% по таблице (гарантия сработала " + triggered + " раз). Мифик реально " + (got[4] * 100.0 / N).ToString("F2") + "%");
        }

        // 4. Мифик ровно на 100-м Гильдейском: генератор 0.0 сам Мифика не даёт, значит только гарантия
        {
            var sim = new CampSimulation(new System.Random(1));
            var sys = new WorkerPackSystem(sim, new FixedRandom(0.0));
            bool noEarlyMyth = true, rareOk = true, legOk = true, mythOk = false;
            int mythAt = -1;
            for (int i = 1; i <= 100; i++)
            {
                PackResult r;
                sys.Open(WorkerPackType.Guild, db, out r);
                if (r.rarity == WorkerRarity.Mythic) { if (i < 100) noEarlyMyth = false; else { mythAt = i; mythOk = r.guarantee == PackGuarantee.Mythic; } }
                if (i % 10 == 0 && i % 40 != 0 && i != 100 && !(r.rarity == WorkerRarity.Rare && r.guarantee == PackGuarantee.Rare)) rareOk = false;
                if ((i == 40 || i == 80) && !(r.rarity == WorkerRarity.Legendary && r.guarantee == PackGuarantee.Legendary)) legOk = false;
            }
            Check(noEarlyMyth && mythAt == 100 && mythOk, "Гильдейский: Мифик ровно на 100-м паке, на 1-99 его нет", "Мифик на паке " + mythAt);
            Check(rareOk, "Гильдейский: гарантия Редкого на 10, 20, 30, 50... паке");
            Check(legOk, "Гильдейский: гарантия Легендарного на 40-м и 80-м паке");
            Check(sys.MythicProgress(WorkerPackType.Guild) == 0 && sys.OpensUntilMythic(WorkerPackType.Guild) == 100, "После Мифика счётчик «до мифика» сброшен: 0/100");
        }

        // 5. Счётчики раздельные: паки одного типа не двигают гарантии другого
        {
            var sim = new CampSimulation(new System.Random(1));
            var sys = new WorkerPackSystem(sim, new FixedRandom(0.0));
            PackResult r;
            for (int i = 0; i < 5; i++) sys.Open(WorkerPackType.Guild, db, out r);
            Check(sys.OpensUntilRare(WorkerPackType.Guild) == 5 && sys.OpensUntilRare(WorkerPackType.Artel) == 10 && sys.OpensUntilRare(WorkerPackType.Workshop) == 10,
                  "Счётчики гарантий раздельные по типам пака", "Гильдейский до Редкого " + sys.OpensUntilRare(WorkerPackType.Guild) + ", Артельный " + sys.OpensUntilRare(WorkerPackType.Artel));
            Check(sys.OpensUntilMythic(WorkerPackType.Artel) == 0 && sys.OpensUntilLegendary(WorkerPackType.Artel) == 0 && sys.OpensUntilLegendary(WorkerPackType.Workshop) == 40,
                  "У Артельного нет гарантий Легендарного и Мифика, у Мастерского нет Мифика");
        }

        // 6. Подарочный пак: Обычный Приказчик, один раз, счётчики не двигает, цена 0
        {
            var sim = new CampSimulation(new System.Random(1));
            var sys = new WorkerPackSystem(sim, new System.Random(5));
            Check(sys.Price(WorkerPackType.Artel) == 100, "Без подарка Артельный пак стоит 100");
            sys.OfferFree();
            bool secondOffer = sys.OfferFree();
            Check(sys.Price(WorkerPackType.Artel) == 0 && sys.Price(WorkerPackType.Guild) == 1400 && !secondOffer, "Подарок: Артельный пак бесплатный, повторно предложить нельзя");
            PackResult r; sys.Open(WorkerPackType.Artel, db, out r);
            Check(r.wasFree && r.isNew && r.rarity == WorkerRarity.Common && r.def.role == WorkerRole.Clerk && sim.WorkingCount == 1,
                  "Подарочный пак даёт нового Обычного Приказчика, он сразу встаёт в слот", r.def.FullName);
            Check(sys.OpensUntilRare(WorkerPackType.Artel) == 10 && !sys.FreePending && sys.FreeUsed && sys.Price(WorkerPackType.Artel) == 100,
                  "Подарок не двигает гарантию и тратится один раз");
            sys.OfferFree();
            Check(!sys.FreePending, "После использования подарок больше не предлагается");
        }

        // 7. Дубликаты: опыт по редкости; на максимуме монеты; события уровня
        {
            int paid = 0;
            var sim = new CampSimulation(new System.Random(1));
            var sys = new WorkerPackSystem(sim, new System.Random(2), c => paid += c);
            var rare = db.workers.Find(w => w.rarity == WorkerRarity.Rare && w.role == WorkerRole.Sawyer);
            var first = sys.Award(rare, WorkerPackType.Workshop, false);
            Check(first.isNew && !first.isDuplicate, "Первый раз рабочий новый");
            var dup = sys.Award(rare, WorkerPackType.Workshop, false);
            Check(dup.isDuplicate && dup.xpGained == 400 && Math.Abs(dup.worker.totalXp - 400) < 1e-6 && dup.worker.level == 3,
                  "Дубликат Редкого: +400 XP (3 уровень)", "XP " + dup.worker.totalXp + ", ур. " + dup.worker.level + ", уровней +" + dup.levelsGained);
            int[] xp = { 150, 250, 400, 700, 1500 }, coins = { 50, 80, 150, 300, 600 };
            bool tbl = true;
            for (int r = 0; r < 5; r++) if (WorkerBalance.DuplicateXpFor((WorkerRarity)r) != xp[r] || WorkerBalance.DuplicateCoinsFor((WorkerRarity)r) != coins[r]) tbl = false;
            Check(tbl, "Таблицы дубликатов: опыт 150/250/400/700/1500, монеты 50/80/150/300/600");

            sim.AddXp(dup.worker, 1000000);
            var dupMax = sys.Award(rare, WorkerPackType.Workshop, false);
            Check(dupMax.isDuplicate && dupMax.xpGained == 0 && dupMax.coinsGained == 150 && paid == 150 && dup.worker.IsMax,
                  "Дубликат на максимуме: 150 монет вместо опыта", "монет выдано " + paid);
        }

        // 8. Событие OnPackOpened: приходит один раз с готовым результатом, признак гарантии виден
        {
            var sim = new CampSimulation(new System.Random(1));
            var sys = new WorkerPackSystem(sim, new FixedRandom(0.0));
            int events = 0; PackResult last = default(PackResult);
            Action<PackResult> h = r => { events++; last = r; };
            WorkerPackSystem.OnPackOpened += h;
            try
            {
                PackResult r10 = default(PackResult);
                for (int i = 0; i < 10; i++) sys.Open(WorkerPackType.Artel, db, out r10);
                Check(events == 10 && last.GuaranteeTriggered && last.guarantee == PackGuarantee.Rare && last.rarity == WorkerRarity.Rare && sim.Find(last.def.id) != null,
                      "OnPackOpened: 10 событий, на 10-м пакете сработала гарантия Редкого", "событий " + events + ", гарантия " + last.guarantee);
            }
            finally { WorkerPackSystem.OnPackOpened -= h; }
        }

        // 9. Сохранение счётчиков: туда и обратно, мусор приводится к допустимому
        {
            var a = new WorkerPackState();
            a.sinceRare[2] = 7; a.sinceLegendary[1] = 33; a.sinceMythic[2] = 63; a.freePending = true;
            var json = JsonUtility.ToJson(a);
            var b = new WorkerPackState(); b.CopyFrom(JsonUtility.FromJson<WorkerPackState>(json));
            Check(b.sinceRare[2] == 7 && b.sinceLegendary[1] == 33 && b.sinceMythic[2] == 63 && b.freePending, "Счётчики гарантий переживают сохранение: до мифика 63/100");
            var bad = new WorkerPackState(); bad.sinceRare = new int[1]; bad.sinceMythic[2] = 5000; bad.sinceLegendary[0] = -4; bad.freePending = true; bad.freeUsed = true;
            var c = new WorkerPackState(); c.CopyFrom(bad);
            Check(c.sinceMythic[2] == 99 && c.sinceLegendary[0] == 0 && c.sinceRare[2] == 0 && !c.freePending, "Битые счётчики приводятся к допустимым");
        }
    }
}
#endif
