using System;
using System.Collections.Generic;
using UnityEngine;

public enum SkillBranch { Lumberjack, Workshop, Camp }

public sealed class SkillNodeDef
{
    public string Id;
    public SkillBranch Branch;
    public int Tier, Cost, RequiredLevel;
    public string ExclusiveGroup, Title, Description;
}

public static class SkillIds
{
    // Retained IDs preserve compatible choices and existing effects.
    public const string CarryDash = "carry_dash", LumberFastGrow = "lumber_fastgrow",
        CutLayout = "lumber_layout", LumberEcho = "lumber_echo", LumberGolden = "lumber_golden",
        WideZone = "lumber_wide", BossHoard = "boss_hoard", BossTongue = "boss_tongue",
        BossSawyer = "boss_sawyer", ForgeDiscount = "workshop_forge", BossBulk = "boss_bulk",
        Builder = "workshop_builder", Mentor = "camp_mentor", Lucky = "camp_lucky",
        CarryFriend = "carry_friend", ClerkIncome = "camp_clerk", Crew = "camp_crew", Storage = "camp_storage";

    // IDs understood by legacy gameplay paths. They are no longer offered in the tree.
    public const string LumberChain = "lumber_chain", LumberSturdy = "lumber_sturdy",
        CarryLight = "carry_light", CarryBasket = "carry_basket", CarryLegs = "carry_legs", BossChief = "boss_chief";
}

public static class SkillTree
{
    static readonly string[] branchNames = { "Лесоруб", "Мастерская и рынок", "Лагерь и питомцы" };
    static readonly List<SkillNodeDef> all = new List<SkillNodeDef>();
    static readonly Dictionary<string, SkillNodeDef> byId = new Dictionary<string, SkillNodeDef>();

    static SkillTree()
    {
        Add(SkillIds.CarryDash, SkillBranch.Lumberjack, 1, "Ускорение", "Shift: рывок ×2 на 0,4 с. Перезарядка 8 с.");
        Add(SkillIds.LumberFastGrow, SkillBranch.Lumberjack, 1, "Быстрый рост", "Обычное дерево отрастает на 25% быстрее после полного сбора.");
        Add(SkillIds.CutLayout, SkillBranch.Lumberjack, 2, "Разметка ствола", "Точный завершающий удар на первом разрезе старого дерева уменьшает второй разрез с 8 до 6 единиц.");
        Add(SkillIds.LumberEcho, SkillBranch.Lumberjack, 2, "Эхо удара", "При валке зрелого или старого дерева с шансом 15% падает молодой сосед. Из него можно собрать 1 бревно.");
        Add(SkillIds.LumberGolden, SkillBranch.Lumberjack, 3, "Золотое дерево", "Даёт 8 брёвен после валки. Следующее появляется через 3 минуты после полного сбора.");
        Add(SkillIds.WideZone, SkillBranch.Lumberjack, 3, "Широкая зона", "На валке и разделке зелёная зона расширяется с 34% до 48% шкалы.");
        Add(SkillIds.BossHoard, SkillBranch.Workshop, 1, "Запасливый", "Личная пила: +3 места в очереди, +5 мест под готовые доски.");
        Add(SkillIds.BossTongue, SkillBranch.Workshop, 1, "Базарный день", "Следующая продажа ×1,5. Повторный бонус доступен через 5 минут.");
        Add(SkillIds.BossSawyer, SkillBranch.Workshop, 2, "Мастер-пильщик", "Каждая шестая доска с личной пилы экономит одно бревно.");
        Add(SkillIds.ForgeDiscount, SkillBranch.Workshop, 2, "Подмастерье кузнеца", "Зачарование в рунной кузнице требует на 20% меньше монет.");
        Add(SkillIds.BossBulk, SkillBranch.Workshop, 3, "Оптовик", "Продажа от 50 единиц даёт +10%. У Рико можно отложить партию за несколько заходов.");
        Add(SkillIds.Builder, SkillBranch.Workshop, 3, "Мастер стройки", "На строительный этап требуется на 10% меньше досок с округлением вверх.");
        Add(SkillIds.Mentor, SkillBranch.Camp, 1, "Наставник", "Рабочие получают на 20% больше опыта за время работы.");
        Add(SkillIds.Lucky, SkillBranch.Camp, 1, "Удачливый", "Шанс редкого и выше в клетях и паках растёт на 10% от исходного. 10% превращаются в 11%.");
        Add(SkillIds.CarryFriend, SkillBranch.Camp, 2, "Хозяин", "Открывает дополнительное активное место для питомца.");
        Add(SkillIds.ClerkIncome, SkillBranch.Camp, 2, "Доход приказчика", "Назначенные приказчики приносят на 15% больше пассивного дохода.");
        Add(SkillIds.Crew, SkillBranch.Camp, 3, "Слаженная бригада", "Когда все шесть мест заняты тремя дровосеками и тремя пильщиками, пильщики работают на 15% быстрее.");
        Add(SkillIds.Storage, SkillBranch.Camp, 3, "Большой склад", "Базовый склад лагеря растёт с 30 до 50 брёвен и с 10 до 20 досок.");
    }

    public static IReadOnlyList<SkillNodeDef> All => all;

    public static SkillNodeDef Get(string id) =>
        !string.IsNullOrEmpty(id) && byId.TryGetValue(id, out var def) ? def : null;

    public static string BranchName(SkillBranch branch) => branchNames[(int)branch];

    internal static bool HasRoot(SkillBranch branch, HashSet<string> owned)
    {
        foreach (var node in all)
            if (node.Branch == branch && node.Tier == 1 && owned.Contains(node.Id)) return true;
        return false;
    }

    static void Add(string id, SkillBranch branch, int tier, string title, string description)
    {
        var node = new SkillNodeDef
        {
            Id = id, Branch = branch, Tier = tier, Cost = GameBalance.SkillTierCost[tier - 1],
            RequiredLevel = 3, ExclusiveGroup = branch + "/" + tier, Title = title, Description = description
        };
        all.Add(node);
        byId.Add(id, node);
    }
}

internal static class TalentEffects
{
    internal static int BuildPlanks(int count) => PlayerSkills.Has(SkillIds.Builder)
        ? (int)((count * 9L + 9) / 10) : count;

    internal static int EnchantCoins(int baseCoins) => PlayerSkills.Has(SkillIds.ForgeDiscount)
        ? (int)((baseCoins * 8L + 9) / 10) : baseCoins;

    internal static float[] LuckyOdds(float[] baseOdds, int rareIndex)
    {
        if (!PlayerSkills.Has(SkillIds.Lucky)) return baseOdds;
        var odds = (float[])baseOdds.Clone();
        double common = 0, rare = 0;
        for (int i = 0; i < odds.Length; i++)
        {
            if (i < rareIndex) common += Math.Max(0, odds[i]); else rare += Math.Max(0, odds[i]);
        }
        if (common <= 0 || rare <= 0) return odds;
        double moved = Math.Min(common, rare * .10);
        for (int i = 0; i < odds.Length; i++)
            odds[i] = (float)(odds[i] * (i < rareIndex ? (common - moved) / common : (rare + moved) / rare));
        return odds;
    }
}
