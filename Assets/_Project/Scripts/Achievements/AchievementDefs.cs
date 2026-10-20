// Assets/_Project/Scripts/Achievements/AchievementDefs.cs (НЕ в Editor)
// Таблица достижений («Дневник Лео»). Все цифры наград и порогов живут ЗДЕСЬ, в одном месте (как GameBalance).
// Новое достижение = одна строка в Build(). Условие задаётся метрикой (Metric) и порогом (goal).
//
// ДЕРЕВО (как в Minecraft): каждое достижение лежит на вкладке (cat), в клетке сетки (gx вправо, gy вниз)
// и может иметь родителя (parent, только из той же вкладки). Пока родитель не открыт, узел в окне тёмный с «???».
// Выполненное достижение видно всегда, даже если родителя ещё нет.
//
// БАЛАНС (см. GDD, раздел 5.8 и 16.2):
//   * Награды маленькие: первые 30 минут (вертикальный срез) дают достижениями ~13% монет и ~10% XP от темпа
//     из раздела 16.2 (≈4600 монет, ≈1650 XP). Достижения украшают прогресс, а не заменяют его.
//   * Бронза 10–35 монет, Серебро 40–120, Золото 100–500. Крупные суммы стоят за дальними целями (часы игры, 2000 деревьев).
//   * Награда выдаётся кнопкой «Забрать», а не автоматически: игрок сам выбирает момент и видит сумму.
//   * Монеты за достижения НЕ засчитываются в счётчик «заработано монет» (иначе петля: награда → новое достижение).
using System.Collections.Generic;
using UnityEngine;

public enum AchMetric
{
    PlaySeconds,        // время в игре, секунды
    TreesChopped,       // срублено деревьев игроком вручную
    PlanksCollected,    // досок забрано с пилорамы за всю игру
    CoinsEarned,        // заработано монет за всю игру (без наград достижений)
    WorkersHired,       // рабочих в лагере сейчас
    PlayerLevel,        // уровень игрока
    UpgradeLevels,      // сумма уровней всех улучшений инструментов
    SkillsOwned,        // узлов навыков куплено
    PetsDiscovered,     // видов питомцев в Книге
    Chapter             // закрыта глава StoryFlow с id = chapterId (0 или 1)
}

public enum AchTier { Bronze, Silver, Gold }

// вкладки окна. ПОРЯДОК важен: от него зависит узор фона в AchUi.Pattern (0 искры, 1 ёлочки, 2 монетки, 3 ромбики, 4 точки)
public enum AchCat { Camp, Forest, Trade, Friends, Path }

public static class AchCats
{
    public const int Count = 5;

    public static readonly string[] Names = { "Лагерь", "Лес", "Торговля", "Друзья", "Путь" };

    public static readonly string[] Blurbs =
    {
        "Костёр, хижина и всё, с чего начинался остров",
        "Топор, пилорама и шум кедров",
        "Монеты, кошелёк и мастерская",
        "Рабочие и питомцы: с ними веселее",
        "Уровни, навыки и время на острове",
    };

    // цвет вкладки: тонирует фон, линии и подпись раздела
    public static readonly Color[] Colors =
    {
        new Color32(0xF0, 0x8A, 0x3C, 255),   // Лагерь: огонь
        new Color32(0x5F, 0xBC, 0x5A, 255),   // Лес: хвоя
        new Color32(0xF2, 0xC2, 0x3C, 255),   // Торговля: золото
        new Color32(0xE8, 0x7C, 0xA6, 255),   // Друзья: тёплый розовый
        new Color32(0x5B, 0xA8, 0xEC, 255),   // Путь: небо
    };

    public static readonly UiIcon[] Icons = { UiIcon.Flame, UiIcon.Tree, UiIcon.Coin, UiIcon.Paw, UiIcon.Boot };
}

public class AchievementDef
{
    public string id;
    public string title;
    public string desc;
    public AchMetric metric;
    public int goal;
    public string chapterId;     // только для Metric.Chapter
    public AchTier tier;
    public int coins;
    public int xp;
    public bool early;           // достижимо в первые ~30 минут (нужно только для проверки бюджета наград)

    // положение в дереве
    public AchCat cat;
    public string parent;        // id родителя из той же вкладки или null (корень)
    public int gx, gy;           // клетка сетки: gx вправо, gy вниз
    public UiIcon icon;
}

public static class AchievementDefs
{
    static List<AchievementDef> all;
    static Dictionary<string, AchievementDef> byId;
    static List<AchievementDef>[] byCat;

    public static IReadOnlyList<AchievementDef> All { get { EnsureBuilt(); return all; } }

    public static AchievementDef Find(string id)
    {
        EnsureBuilt();
        AchievementDef d;
        return id != null && byId.TryGetValue(id, out d) ? d : null;
    }

    // достижения одной вкладки в порядке добавления
    public static IReadOnlyList<AchievementDef> OfCat(AchCat c)
    {
        EnsureBuilt();
        return byCat[(int)c];
    }

    static void EnsureBuilt() { if (all == null) Build(); }

    static void Add(AchCat cat, int gx, int gy, string parent, UiIcon icon,
                    string id, string title, string desc, AchMetric m, int goal, AchTier tier, int coins, int xp, bool early, string chapterId = null)
    {
        var d = new AchievementDef
        {
            id = id, title = title, desc = desc, metric = m, goal = goal, tier = tier, coins = coins, xp = xp, early = early, chapterId = chapterId,
            cat = cat, parent = parent, gx = gx, gy = gy, icon = icon
        };
        all.Add(d);
        byId[id] = d;
        byCat[(int)cat].Add(d);
    }

    static void Build()
    {
        all = new List<AchievementDef>(48);
        byId = new Dictionary<string, AchievementDef>(48);
        byCat = new List<AchievementDef>[AchCats.Count];
        for (int i = 0; i < byCat.Length; i++) byCat[i] = new List<AchievementDef>(12);

        const AchTier B = AchTier.Bronze, S = AchTier.Silver, G = AchTier.Gold;
        const AchMetric C = AchMetric.Chapter;
        const AchCat Camp = AchCat.Camp, Forest = AchCat.Forest, Trade = AchCat.Trade, Friends = AchCat.Friends, Path = AchCat.Path;

        // =============================== ЛАГЕРЬ: сюжет (id глав из StoryFlow.BuildChapters + «intro» из IntroFlow)
        //   костёр → хижина → ┬ рынок
        //                      └ палатка → мост → записка Гуннара
        Add(Camp, 0, 1, null,       UiIcon.Flame,  "ch_intro",  "Огонь горит",          "Пройди главу 1: разожги костёр на каменном круге", C, 1, B, 15, 5,  true, "intro");
        Add(Camp, 1, 1, "ch_intro", UiIcon.Hut,    "ch_hut",    "Крыша над головой",    "Дострой хижину лесоруба",                          C, 1, B, 15, 5,  true, "hut");
        Add(Camp, 2, 0, "ch_hut",   UiIcon.Stall,  "ch_market", "Открыто для торговли", "Построй рынок Миры",                               C, 1, B, 15, 5,  true, "market");
        Add(Camp, 2, 2, "ch_hut",   UiIcon.Tent,   "ch_tent",   "Лагерь разбит",        "Поставь палатку для рабочих",                      C, 1, B, 20, 10, true, "tent");
        Add(Camp, 3, 2, "ch_tent",  UiIcon.Bridge, "ch_bridge", "Мост над рекой",       "Почини подвесной мост",                            C, 1, S, 40, 15, true, "bridge");
        Add(Camp, 4, 2, "ch_bridge",UiIcon.Scroll, "ch_gunnar", "Записка Гуннара",      "Дойди до мастерской и найди первую записку",       C, 1, G, 100, 30, true, "gunnar");

        // =============================== ЛЕС: рубка (верхний ряд) и пилорама (нижний ряд, ответвление от «Десятка»)
        Add(Forest, 0, 0, null,        UiIcon.Axe,    "tree_1",    "Первые щепки",   "Срубить 1 дерево",     AchMetric.TreesChopped, 1,    B, 10, 5,  true);
        Add(Forest, 1, 0, "tree_1",    UiIcon.Log,    "tree_10",   "Десяток",        "Срубить 10 деревьев",  AchMetric.TreesChopped, 10,   B, 15, 5,  true);
        Add(Forest, 2, 0, "tree_10",   UiIcon.Tree,   "tree_100",  "Лесоруб",        "Срубить 100 деревьев", AchMetric.TreesChopped, 100,  S, 60, 25, false);
        Add(Forest, 3, 0, "tree_100",  UiIcon.Tree,   "tree_500",  "Лесной гигант",  "Срубить 500 деревьев", AchMetric.TreesChopped, 500,  S, 120, 40, false);
        Add(Forest, 4, 0, "tree_500",  UiIcon.Star,   "tree_2000", "Хозяин кедров",  "Срубить 2000 деревьев",AchMetric.TreesChopped, 2000, G, 300, 80, false);

        Add(Forest, 2, 2, "tree_10",   UiIcon.Plank,   "plank_10",   "Первая партия",             "Забрать 10 досок",   AchMetric.PlanksCollected, 10,   B, 15, 5,  true);
        Add(Forest, 3, 2, "plank_10",  UiIcon.Planks,  "plank_50",   "Пильщик",                   "Забрать 50 досок",   AchMetric.PlanksCollected, 50,   B, 30, 10, true);
        Add(Forest, 4, 2, "plank_50",  UiIcon.Saw,     "plank_250",  "Мастер-пильщик",            "Забрать 250 досок",  AchMetric.PlanksCollected, 250,  S, 100, 35, false);
        Add(Forest, 5, 2, "plank_250", UiIcon.Sawmill, "plank_1000", "Лесопилка Кедрового Мыса",  "Забрать 1000 досок", AchMetric.PlanksCollected, 1000, G, 250, 70, false);

        // =============================== ТОРГОВЛЯ: монеты (верхний ряд) и мастерская (нижний ряд)
        Add(Trade, 0, 0, null,        UiIcon.Coin,     "coin_100",   "Первые медяки",          "Заработать 100 монет",    AchMetric.CoinsEarned, 100,   B, 10, 0,  true);
        Add(Trade, 1, 0, "coin_100",  UiIcon.Backpack, "coin_1000",  "Кошелёк потяжелел",      "Заработать 1000 монет",   AchMetric.CoinsEarned, 1000,  B, 25, 5,  true);
        Add(Trade, 2, 0, "coin_1000", UiIcon.Book,     "coin_5000",  "Запись в красной книге", "Заработать 5000 монет",   AchMetric.CoinsEarned, 5000,  S, 70, 20, true);
        Add(Trade, 3, 0, "coin_5000", UiIcon.Gem,      "coin_25000", "Купец",                  "Заработать 25 000 монет", AchMetric.CoinsEarned, 25000, G, 300, 70, false);

        Add(Trade, 1, 2, "coin_100",  UiIcon.Hammer,   "upg_5",  "Заточка",             "Улучшения инструментов: суммарно 5 уровней",  AchMetric.UpgradeLevels, 5,  B, 20, 5,  true);
        Add(Trade, 2, 2, "upg_5",     UiIcon.Saw,      "upg_15", "Мастерская на ходу",  "Улучшения инструментов: суммарно 15 уровней", AchMetric.UpgradeLevels, 15, S, 80, 30, false);
        Add(Trade, 3, 2, "upg_15",    UiIcon.Star,     "upg_34", "Всё по максимуму",    "Прокачать все инструменты до предела",        AchMetric.UpgradeLevels, 34, G, 250, 60, false);

        // =============================== ДРУЗЬЯ: питомцы (верхний ряд) и рабочие (нижний ряд)
        Add(Friends, 0, 0, null,     UiIcon.Paw,    "pet_1",  "Новый друг",      "Открыть первого питомца",    AchMetric.PetsDiscovered, 1,  B, 15, 5,  true);
        Add(Friends, 1, 0, "pet_1",  UiIcon.Paw,    "pet_5",  "Зверинец",        "Открыть 5 видов питомцев",   AchMetric.PetsDiscovered, 5,  S, 60, 25, false);
        Add(Friends, 2, 0, "pet_5",  UiIcon.Book,   "pet_15", "Хранитель Книги", "Открыть 15 видов питомцев",  AchMetric.PetsDiscovered, 15, G, 200, 60, false);

        Add(Friends, 0, 2, null,     UiIcon.Person, "work_1", "Первый помощник", "Нанять 1 рабочего",         AchMetric.WorkersHired, 1, B, 20, 10, true);
        Add(Friends, 1, 2, "work_1", UiIcon.Person, "work_3", "Своя артель",     "Нанять 3 рабочих",          AchMetric.WorkersHired, 3, S, 35, 15, true);
        Add(Friends, 2, 2, "work_3", UiIcon.Tent,   "work_6", "Полный лагерь",   "Нанять всех 6 рабочих",     AchMetric.WorkersHired, 6, S, 100, 30, false);

        // =============================== ПУТЬ: уровень (верхний ряд, XP не даём: уровень и так награда), навыки, время в игре
        //   время в игре на 6–8 часов полной игры: «10 часов» требует вернуться после финала
        Add(Path, 0, 0, null,      UiIcon.Star,   "lvl_3",  "Освоился",       "Достичь 3 уровня",                  AchMetric.PlayerLevel, 3,  B, 15, 0, true);
        Add(Path, 1, 0, "lvl_3",   UiIcon.Star,   "lvl_5",  "Бывалый",        "Достичь 5 уровня",                  AchMetric.PlayerLevel, 5,  B, 30, 0, true);
        Add(Path, 2, 0, "lvl_5",   UiIcon.Target, "lvl_8",  "Мастер острова", "Достичь 8 уровня",                  AchMetric.PlayerLevel, 8,  S, 60, 0, true);
        Add(Path, 3, 0, "lvl_8",   UiIcon.Trophy, "lvl_10", "Золотой топор",  "Достичь максимального 10 уровня",   AchMetric.PlayerLevel, GameBalance.MaxLevel, G, 200, 0, false);

        Add(Path, 1, 1, "lvl_3",   UiIcon.Bolt,   "skill_1", "Свой путь",     "Выбрать первый навык",              AchMetric.SkillsOwned, 1, B, 10, 5,  true);
        Add(Path, 2, 1, "skill_1", UiIcon.Bolt,   "skill_5", "Своя сборка",   "Выбрать 5 навыков",                 AchMetric.SkillsOwned, 5, S, 60, 20, false);

        Add(Path, 0, 2, null,      UiIcon.Clock,  "time_1",  "Освоился на острове", "Сыграть 1 час",    AchMetric.PlaySeconds, 3600,  B, 30, 10, false);
        Add(Path, 1, 2, "time_1",  UiIcon.Clock,  "time_3",  "Свой человек",        "Сыграть 3 часа",   AchMetric.PlaySeconds, 10800, S, 80, 25, false);
        Add(Path, 2, 2, "time_3",  UiIcon.Clock,  "time_10", "Старожил",            "Сыграть 10 часов", AchMetric.PlaySeconds, 36000, G, 250, 60, false);
        Add(Path, 3, 2, "time_10", UiIcon.Trophy, "time_25", "Душа острова",        "Сыграть 25 часов", AchMetric.PlaySeconds, 90000, G, 500, 100, false);
    }

    // ---------------------------------------------------------------- проверка бюджета наград (вызывает Editor/AchievementSelfTest)
    // ориентиры из GDD 16.2: за 30 минут ≈4600 монет и ≈1650 XP; достижения первых 30 минут не должны давать больше 15% монет и 12% XP
    public const int Gdd30MinCoins = 4600, Gdd30MinXp = 1650;
    public const float MaxEarlyCoinShare = 0.15f, MaxEarlyXpShare = 0.12f;

    public static string BudgetReport(out bool ok)
    {
        EnsureBuilt();
        int coins = 0, xp = 0, ec = 0, ex = 0;
        foreach (var d in all)
        {
            coins += d.coins; xp += d.xp;
            if (d.early) { ec += d.coins; ex += d.xp; }
        }
        float cs = (float)ec / Gdd30MinCoins, xs = (float)ex / Gdd30MinXp;
        ok = cs <= MaxEarlyCoinShare && xs <= MaxEarlyXpShare;
        return "Достижений: " + all.Count + ". Всего наград: " + coins + " монет, " + xp + " XP.\n" +
               "Первые 30 минут: " + ec + " монет (" + (cs * 100f).ToString("0.#") + "% от темпа, лимит " + (MaxEarlyCoinShare * 100f) + "%), " +
               ex + " XP (" + (xs * 100f).ToString("0.#") + "%, лимит " + (MaxEarlyXpShare * 100f) + "%). " + (ok ? "OK" : "ПРЕВЫШЕНИЕ");
    }

    // проверка дерева: родитель существует, лежит на той же вкладке и левее ребёнка, клетки не пересекаются (вызывать из Editor-теста)
    public static string TreeReport(out bool ok)
    {
        EnsureBuilt();
        ok = true;
        var sb = new System.Text.StringBuilder();
        var cells = new HashSet<string>();
        foreach (var d in all)
        {
            if (!cells.Add((int)d.cat + ":" + d.gx + ":" + d.gy)) { ok = false; sb.AppendLine("Две клетки совпали: " + d.id); }
            if (string.IsNullOrEmpty(d.parent)) continue;
            AchievementDef p;
            if (!byId.TryGetValue(d.parent, out p)) { ok = false; sb.AppendLine(d.id + ": нет родителя " + d.parent); continue; }
            if (p.cat != d.cat) { ok = false; sb.AppendLine(d.id + ": родитель " + p.id + " на другой вкладке"); }
            if (p.gx >= d.gx) { ok = false; sb.AppendLine(d.id + ": родитель " + p.id + " не левее"); }
        }
        return ok ? "Дерево достижений в порядке (" + all.Count + " узлов)" : sb.ToString();
    }
}
