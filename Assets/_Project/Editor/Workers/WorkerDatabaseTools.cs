#if UNITY_EDITOR
// Assets/_Project/Editor/Workers/WorkerDatabaseTools.cs (Editor)
// Меню: Tools > BuilderSim > Рабочие > Создать базу рабочих   (27 ассетов WorkerDefinition + WorkerDatabase, портреты подцепляются сами)
//       Tools > BuilderSim > Рабочие > Проверить базу         (по 9 на роль, составы редкостей, перки, портреты, id)
// Setup Doctor вызывает WorkerDatabaseTools.CheckForDoctor. Портреты рисует «Сгенерировать портреты» (id файла = id персонажа).
// Безопасно запускать повторно: существующие ассеты обновляются на месте (ссылки на них не ломаются).
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class WorkerDatabaseTools
{
    const string DataFolder = "Assets/_Project/Data/Workers";
    const string DatabasePath = "Assets/_Project/Data/WorkerDatabase.asset";
    const string PortraitFolder = "Assets/Project/Art/Workers/Portraits";   // так пишет WorkerPortraitGenerator

    // ------------------------------------------------------------------ данные (из дизайна)
    struct Row
    {
        public string id, first, last, bio;
        public int age;
        public WorkerRole role;
        public WorkerRarity rarity;
        public WorkerPerk[] perks;
    }

    static Row R(string id, string first, string last, int age, WorkerRole role, WorkerRarity rarity, string bio, params WorkerPerk[] perks)
    {
        return new Row { id = id, first = first, last = last, age = age, role = role, rarity = rarity, bio = bio, perks = perks };
    }

    const WorkerRole CL = WorkerRole.Clerk, LJ = WorkerRole.Lumberjack, SW = WorkerRole.Sawyer;
    const WorkerRarity C = WorkerRarity.Common, U = WorkerRarity.Uncommon, Rr = WorkerRarity.Rare, L = WorkerRarity.Legendary, M = WorkerRarity.Mythic;

    static List<Row> Rows()
    {
        var all = new List<Row>();
        // ---- Приказчики
        all.Add(R("clerk_nils", "Нильс", "Кошелёк", 24, CL, C, "Худой веснушчатый парень в жёлтой жилетке. «Считаю даже ступеньки»."));
        all.Add(R("clerk_agata", "Агата", "Медяк", 31, CL, C, "Носит монетку на шнурке и круглые очки. «Копейка рубль бережёт»."));
        all.Add(R("clerk_rurik", "Рюрик", "Грош", 45, CL, C, "Усатый коротышка, вечно дремлет за прилавком. «Я не сплю, я прикидываю»."));
        all.Add(R("clerk_sigrid", "Сигрид", "Ларсен", 38, CL, U, "Высокая, в синем плаще, с весами на поясе. «Каждому по весу».", WorkerPerk.GoodDeal));
        all.Add(R("clerk_bjorn", "Бьорн", "Тарн", 52, CL, U, "Лысеющий, всё записывает мелом в блокнот. «Записано, значит, было».", WorkerPerk.Bonus));
        all.Add(R("clerk_elsa", "Эльза", "Звонкая", 29, CL, Rr, "Рыжие кудри, красный шарф, в кармане звенят монеты. «Слышишь? Это к удаче».", WorkerPerk.GoodDeal, WorkerPerk.LuckyDeal));
        all.Add(R("clerk_gustav", "Густав", "Пенс", 57, CL, Rr, "Седой, в потёртом цилиндре, с тростью-монетой. «В молодости я был богат. Дважды».", WorkerPerk.Bonus, WorkerPerk.Mentor));
        all.Add(R("clerk_frida", "Фрида", "Счётная", 48, CL, L, "Золотое пенсне и огромные счёты за спиной. «Ошибка в счёте? Не у меня».", WorkerPerk.GoodDeal, WorkerPerk.Bonus, WorkerPerk.Mentor));
        all.Add(R("clerk_brita", "Брита", "Золотые Весы", 61, CL, M, "Основательница артели, когда-то вела её кассу; весы-амулет чуть светятся. «Сначала честность, потом выгода. Выгода всё равно придёт».",
            WorkerPerk.GoodDeal, WorkerPerk.LuckyDeal, WorkerPerk.Bonus, WorkerPerk.Mentor));
        // ---- Дровосеки
        all.Add(R("lumber_pekka", "Пекка", "Топорик", 20, LJ, C, "Мальчишка в большой шапке, топор больше него. «Он не тяжёлый, он просто длинный»."));
        all.Add(R("lumber_martta", "Мартта", "Щепка", 27, LJ, C, "Крепкая, в косынке в клетку, щепки в волосах. «Если не щепки, значит, не работа»."));
        all.Add(R("lumber_olle", "Олле", "Пенёк", 41, LJ, C, "Широкий, с бородой лопатой, любит посидеть на пне. «Отдыхать тоже надо уметь»."));
        all.Add(R("lumber_knut", "Кнут", "Бук", 36, LJ, U, "Румяный, в красной клетчатой рубахе с закатанными рукавами. «Топор сам знает, куда лететь».", WorkerPerk.HeavyHand));
        all.Add(R("lumber_aune", "Ауне", "Ветка", 33, LJ, U, "Ловкая, в зелёной куртке, топор на двух ремнях. «Два удара, два дерева. Ну почти».", WorkerPerk.DoubleStrike));
        all.Add(R("lumber_johanna", "Йоханна", "Корень", 39, LJ, Rr, "Таскает большой плетёный короб за спиной. «Что не влезло, то я поднимусь ещё раз».", WorkerPerk.DoubleStrike, WorkerPerk.Roomy));
        all.Add(R("lumber_einar", "Эйнар", "Жила", 50, LJ, Rr, "Сутулый и жилистый, золотистая щепка за ухом. «Золото в дереве есть, надо знать, где рубить».", WorkerPerk.HeavyHand, WorkerPerk.GoldenTree));
        all.Add(R("lumber_tor", "Тор", "Глыба", 44, LJ, L, "Великан с плечами как бочки и топором размером с весло. «Не торопи дерево, оно само упадёт».", WorkerPerk.DoubleStrike, WorkerPerk.HeavyHand, WorkerPerk.Roomy));
        all.Add(R("lumber_holger", "Хольгер", "Кедровый", 70, LJ, M, "Основатель артели и друг Гуннара, с кедровой шишкой на шнурке. «Кедры поют. Ты слушай, а не руби».",
            WorkerPerk.DoubleStrike, WorkerPerk.HeavyHand, WorkerPerk.Roomy, WorkerPerk.GoldenTree));
        // ---- Пильщики
        all.Add(R("saw_til", "Тиль", "Опилка", 22, SW, C, "Растрёпанный, очки-консервы на лбу, опилки на ресницах. «Это не мусор, это стружка»."));
        all.Add(R("saw_helga", "Хельга", "Брусок", 35, SW, C, "Аккуратная, в синем комбинезоне, карандаш за ухом. «Ровно, или никак»."));
        all.Add(R("saw_ivar", "Ивар", "Рейка", 47, SW, C, "Длинный и худой, усы клином, пила на плече. «Пилу не торопят»."));
        all.Add(R("saw_solveig", "Сольвейг", "Рубанок", 30, SW, U, "Жёлтая шапка, фартук с инструментами. «Чем быстрее, тем ровнее, если не дрожит рука».", WorkerPerk.FastSaw));
        all.Add(R("saw_vilhelm", "Вильхельм", "Стамеска", 55, SW, U, "Полный, в жилете с кучей карманов. «Лишних досок не бывает».", WorkerPerk.Stocker));
        all.Add(R("saw_ingeborg", "Ингеборг", "Зубец", 42, SW, Rr, "Суровая, коса короной, толстые перчатки. «Один распил, две доски».", WorkerPerk.DoublePlank, WorkerPerk.FastSaw));
        all.Add(R("saw_dagni", "Дагни", "Шлиф", 34, SW, Rr, "Сиреневый платок, чистый фартук, всё меряет рулеткой. «Бревно любит бережного».", WorkerPerk.Thrifty, WorkerPerk.Stocker));
        all.Add(R("saw_lars", "Ларс", "Пила", 52, SW, L, "Бородач с седыми висками, пила-«смычок» с гравировкой. «Пила поёт, если её слушать».", WorkerPerk.DoublePlank, WorkerPerk.Thrifty, WorkerPerk.FastSaw));
        all.Add(R("saw_ingrid", "Ингрид", "Ровная", 55, SW, M, "Основательница артели, делала лучшие доски на острове; линейка-амулет на шее. «Хорошая доска не нуждается в объяснениях».",
            WorkerPerk.DoublePlank, WorkerPerk.Thrifty, WorkerPerk.FastSaw, WorkerPerk.Stocker));
        return all;
    }

    // ------------------------------------------------------------------ создание
    [MenuItem("Tools/BuilderSim/Рабочие/Создать базу рабочих", false, 500)]
    public static void CreateAll()
    {
        EnsureFolder("Assets/_Project");
        EnsureFolder("Assets/_Project/Data");
        EnsureFolder(DataFolder);

        var rows = Rows();
        var defs = new List<WorkerDefinition>();
        int created = 0, updated = 0, noPortrait = 0;

        for (int i = 0; i < rows.Count; i++)
        {
            Row r = rows[i];
            string path = DataFolder + "/" + r.id + ".asset";
            var def = AssetDatabase.LoadAssetAtPath<WorkerDefinition>(path);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<WorkerDefinition>();
                AssetDatabase.CreateAsset(def, path);
                created++;
            }
            else updated++;

            def.id = r.id; def.firstName = r.first; def.lastName = r.last; def.age = r.age;
            def.role = r.role; def.rarity = r.rarity; def.bio = r.bio;
            def.perks = new List<WorkerPerk>(r.perks);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PortraitFolder + "/" + r.id + ".png");
            if (sprite != null) def.portrait = sprite; else noPortrait++;
            EditorUtility.SetDirty(def);
            defs.Add(def);
        }

        var db = AssetDatabase.LoadAssetAtPath<WorkerDatabase>(DatabasePath);
        if (db == null)
        {
            db = ScriptableObject.CreateInstance<WorkerDatabase>();
            AssetDatabase.CreateAsset(db, DatabasePath);
        }
        db.workers = defs;
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string msg = "Создано " + created + ", обновлено " + updated + " персонажей. База: " + DatabasePath
                   + (noPortrait > 0 ? "\nПортретов не найдено: " + noPortrait + ". Запусти «Сгенерировать портреты» и затем «Создать базу рабочих» ещё раз." : "\nПортреты подцеплены.");
        Debug.Log("[Рабочие] " + msg);
        EditorUtility.DisplayDialog("База рабочих", msg, "OK");
        Selection.activeObject = db;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        string parent = path.Substring(0, slash);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
    }

    public static WorkerDatabase FindDatabase()
    {
        var db = AssetDatabase.LoadAssetAtPath<WorkerDatabase>(DatabasePath);
        if (db != null) return db;
        string[] guids = AssetDatabase.FindAssets("t:WorkerDatabase");
        return guids.Length > 0 ? AssetDatabase.LoadAssetAtPath<WorkerDatabase>(AssetDatabase.GUIDToAssetPath(guids[0])) : null;
    }

    // ------------------------------------------------------------------ проверка
    [MenuItem("Tools/BuilderSim/Рабочие/Проверить базу", false, 520)]
    public static void CheckMenu()
    {
        var sb = new StringBuilder();
        int errors = 0, warns = 0;
        Validate(FindDatabase(), sb, ref errors, ref warns);
        string result = "Ошибок: " + errors + ", предупреждений: " + warns;
        if (errors > 0) Debug.LogError("[Рабочие] " + result + "\n" + sb);
        else if (warns > 0) Debug.LogWarning("[Рабочие] " + result + "\n" + sb);
        else Debug.Log("[Рабочие] " + result + "\n" + sb);
        EditorUtility.DisplayDialog("Проверка базы рабочих", result + "\n\nПодробности в Console.", "OK");
    }

    // Для Setup Doctor: те же строки OK/ERR/WARN
    public static void CheckForDoctor(StringBuilder sb, ref int errors, ref int warns)
    {
        Validate(FindDatabase(), sb, ref errors, ref warns);
    }

    public static void Validate(WorkerDatabase db, StringBuilder sb, ref int errors, ref int warns)
    {
        if (db == null)
        {
            errors++;
            sb.AppendLine("ERR  Нет ассета WorkerDatabase. Tools > BuilderSim > Рабочие > Создать базу рабочих.");
            return;
        }

        int before = errors;
        int[,] counts = new int[3, 5];
        var ids = new HashSet<string>();
        int mythics = 0, noPortrait = 0, total = 0;

        for (int i = 0; i < db.workers.Count; i++)
        {
            var w = db.workers[i];
            if (w == null) { errors++; sb.AppendLine("ERR  В базе пустая ссылка на позиции " + i + "."); continue; }
            total++;

            string why;
            if (!w.Validate(out why)) { errors++; sb.AppendLine("ERR  «" + w.name + "»: " + why + "."); }
            if (!string.IsNullOrEmpty(w.id) && !ids.Add(w.id)) { errors++; sb.AppendLine("ERR  Повторяется id «" + w.id + "»."); }
            if (string.IsNullOrWhiteSpace(w.bio)) { warns++; sb.AppendLine("WARN «" + w.name + "»: нет био."); }
            else if (w.bio.Contains("\n")) { warns++; sb.AppendLine("WARN «" + w.name + "»: био должно быть в одну строку."); }
            if (w.portrait == null) noPortrait++;

            counts[(int)w.role, (int)w.rarity]++;
            if (w.rarity == WorkerRarity.Mythic) mythics++;
        }

        if (total != WorkerBalance.PoolTotal) { errors++; sb.AppendLine("ERR  В базе " + total + " персонажей, нужно " + WorkerBalance.PoolTotal + "."); }

        for (int r = 0; r < 3; r++)
        {
            int n = 0; for (int q = 0; q < 5; q++) n += counts[r, q];
            if (n != WorkerBalance.PoolPerRole) { errors++; sb.AppendLine("ERR  Роль «" + WorkerText.Role((WorkerRole)r) + "»: " + n + " персонажей, нужно " + WorkerBalance.PoolPerRole + "."); }
            for (int q = 0; q < 5; q++)
                if (counts[r, q] != WorkerBalance.PoolPerRarity[q])
                { errors++; sb.AppendLine("ERR  Роль «" + WorkerText.Role((WorkerRole)r) + "», «" + WorkerText.Rarity((WorkerRarity)q) + "»: " + counts[r, q] + ", нужно " + WorkerBalance.PoolPerRarity[q] + "."); }
        }
        if (mythics != 3) { errors++; sb.AppendLine("ERR  Мификов " + mythics + ", нужно 3 (по одному на роль)."); }
        if (noPortrait > 0) { warns++; sb.AppendLine("WARN У " + noPortrait + " персонажей нет портрета. «Сгенерировать портреты», потом «Создать базу рабочих»."); }

        if (errors == before) sb.AppendLine("OK   База рабочих: " + total + " персонажей, по 9 на роль (3/2/2/1/1), мификов 3, перки по правилам");
    }
}
#endif
