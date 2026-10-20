// Assets/_Project/Scripts/Skills/SkillTree.cs (НЕ в Editor)
// Статические данные дерева навыков: 3 ветки × 5 узлов (ярус 1: один узел, ярусы 2 и 3: два взаимоисключающих).
// Здесь только описание узлов. Очки, покупка, сброс и сохранение: PlayerSkills. Эффекты: задачи 3 и 4 (читают PlayerSkills.Has(id)).
// Цены, уровни и числа в описаниях берутся из GameBalance: поменял число там, и текст в UI обновится сам.
//
// Как добавить узел: новая строка в Build(), новая константа ID в SkillIds, при необходимости числа в GameBalance.
// Старые сохранения читаются без миграции (неизвестные ID просто игнорируются).
using System.Collections.Generic;
using UnityEngine;

public enum SkillBranch { Lumberjack = 0, Carrier = 1, Master = 2 }

public sealed class SkillNodeDef
{
    public string Id;
    public SkillBranch Branch;
    public int Tier;               // 1..3
    public int Cost;               // очки = GameBalance.SkillTierCost[Tier - 1] (1/2/3)
    public int RequiredLevel;      // GameBalance.SkillTierLevel[Tier - 1] (2/4/7)
    public string ExclusiveGroup;  // null или "lumber_t2": из группы можно взять один узел
    public string Title, Description;
}

// Строковые ID узлов (чтобы не опечататься в эффектах): PlayerSkills.Has(SkillIds.LumberChain)
public static class SkillIds
{
    public const string LumberChain = "lumber_chain", LumberSturdy = "lumber_sturdy", LumberFastGrow = "lumber_fastgrow",
                        LumberEcho = "lumber_echo", LumberGolden = "lumber_golden";
    public const string CarryLight = "carry_light", CarryDash = "carry_dash", CarryBasket = "carry_basket",
                        CarryFriend = "carry_friend", CarryLegs = "carry_legs";
    public const string BossTongue = "boss_tongue", BossHoard = "boss_hoard", BossChief = "boss_chief",
                        BossBulk = "boss_bulk", BossSawyer = "boss_sawyer";
}

public static class SkillTree
{
    static readonly List<SkillNodeDef> all = new List<SkillNodeDef>();
    static readonly Dictionary<string, SkillNodeDef> byId = new Dictionary<string, SkillNodeDef>();
    static readonly SkillNodeDef[] roots = new SkillNodeDef[3];   // узлы 1-го яруса по веткам

    static SkillTree() { Build(); }

    // все 15 узлов в порядке: ветка, ярус, левый/правый
    public static IReadOnlyList<SkillNodeDef> All { get { return all; } }

    public static SkillNodeDef Get(string id)
    {
        SkillNodeDef d;
        return !string.IsNullOrEmpty(id) && byId.TryGetValue(id, out d) ? d : null;
    }

    // узел 1-го яруса ветки: его нужно изучить перед ярусами 2 и 3
    public static SkillNodeDef Tier1Of(SkillBranch b) { return roots[(int)b]; }

    public static string BranchName(SkillBranch b)
    {
        switch (b)
        {
            case SkillBranch.Lumberjack: return "Лесоруб";
            case SkillBranch.Carrier: return "Носильщик";
            default: return "Хозяин";
        }
    }

    // ------------------------------------------------------------ данные
    static string Pct(float v) { return Mathf.RoundToInt(v * 100f) + "%"; }
    static string Sec(float v) { return v.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture); }

    static void Add(string id, SkillBranch branch, int tier, string group, string title, string desc)
    {
        var d = new SkillNodeDef
        {
            Id = id, Branch = branch, Tier = tier,
            Cost = GameBalance.SkillTierCost[tier - 1],
            RequiredLevel = GameBalance.SkillTierLevel[tier - 1],
            ExclusiveGroup = group, Title = title, Description = desc
        };
        all.Add(d);
        byId[id] = d;
        if (tier == 1) roots[(int)branch] = d;
    }

    static void Build()
    {
        // ---------- Лесоруб (деревья) ----------
        Add(SkillIds.LumberChain, SkillBranch.Lumberjack, 1, null, "Серия рубки",
            "Каждое дерево подряд (пауза не больше " + Sec(GameBalance.SkillChainPause) + " с) даёт +" + Pct(GameBalance.SkillChainBonus)
            + " к скорости рубки, максимум +" + Pct(GameBalance.SkillChainMax) + ".");
        Add(SkillIds.LumberSturdy, SkillBranch.Lumberjack, 2, "lumber_t2", "Крепкая древесина",
            "Дерево даёт " + GameBalance.SkillSturdyWood + " бревна, но растёт на " + Pct(GameBalance.SkillSturdyGrowth - 1f) + " дольше.");
        Add(SkillIds.LumberFastGrow, SkillBranch.Lumberjack, 2, "lumber_t2", "Быстрый рост",
            "Деревья растут на " + Pct(1f - GameBalance.SkillFastGrowth) + " быстрее, ближайшее спелое подсвечивается.");
        Add(SkillIds.LumberEcho, SkillBranch.Lumberjack, 3, "lumber_t3", "Эхо удара",
            Pct(GameBalance.SkillEchoChance) + " шанс, что соседнее дерево тоже упадёт, +" + GameBalance.SkillEchoBonusWood + " бревно.");
        Add(SkillIds.LumberGolden, SkillBranch.Lumberjack, 3, "lumber_t3", "Золотое дерево",
            "Раз в " + Mathf.RoundToInt(GameBalance.SkillGoldenInterval) + " с появляется дерево на " + GameBalance.SkillGoldenWood + " брёвен.");

        // ---------- Носильщик (ноги и груз) ----------
        Add(SkillIds.CarryLight, SkillBranch.Carrier, 1, null, "Лёгкая поступь",
            "Груз замедляет бег на " + Pct(GameBalance.SkillCarrySlowdown) + " вместо " + Pct(GameBalance.FullBackpackSlowdown) + ".");
        Add(SkillIds.CarryDash, SkillBranch.Carrier, 2, "carry_t2", "Рывок",
            "Shift (на телефоне кнопка): скорость ×" + Sec(GameBalance.SkillDashMul) + " на " + Sec(GameBalance.SkillDashTime) + " с, перезарядка "
            + Mathf.RoundToInt(GameBalance.SkillDashCooldown) + " с.");
        Add(SkillIds.CarryBasket, SkillBranch.Carrier, 2, "carry_t2", "Большой короб",
            "Рюкзак вмещает на " + GameBalance.SkillBasketBonus + " бревна больше.");
        Add(SkillIds.CarryFriend, SkillBranch.Carrier, 3, "carry_t3", "Верный друг",
            "Открывает 4-й слот для питомца.");
        Add(SkillIds.CarryLegs, SkillBranch.Carrier, 3, "carry_t3", "Быстрые ноги",
            "+" + Pct(GameBalance.SkillLegsBonus) + " к скорости бега.");

        // ---------- Хозяин (деньги, пила, рабочие) ----------
        Add(SkillIds.BossTongue, SkillBranch.Master, 1, null, "Бойкий язык",
            "Раз в " + Mathf.RoundToInt(GameBalance.SkillBazaarCooldown / 60f) + " минут «Базарный день»: следующая продажа ×" + Sec(GameBalance.SkillBazaarMul) + ".");
        Add(SkillIds.BossHoard, SkillBranch.Master, 2, "boss_t2", "Запасливый",
            "Очередь пилы +" + GameBalance.SkillHoardQueue + ", склад досок +" + GameBalance.SkillHoardOutput + ".");
        Add(SkillIds.BossChief, SkillBranch.Master, 2, "boss_t2", "Хороший начальник",
            "Рабочие приносят на " + Pct(GameBalance.SkillChiefBonus) + " больше.");
        Add(SkillIds.BossBulk, SkillBranch.Master, 3, "boss_t3", "Оптовик",
            "Продажа от " + GameBalance.SkillBulkMinItems + " предметов за раз даёт +" + Pct(GameBalance.SkillBulkBonus) + ".");
        Add(SkillIds.BossSawyer, SkillBranch.Master, 3, "boss_t3", "Мастер-пильщик",
            Pct(GameBalance.SkillSawyerChance) + " шанс получить доску без траты брёвен.");
    }
}
