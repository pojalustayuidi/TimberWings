using System;

internal enum AxeRuneId
{
    None = 0, LightGrip = 1, CarefulCollect = 2, SharpEdge = 3, TravelReserve = 4, SturdyWood = 5,
    ForestSpark = 6, SummerSpark = 7, RunicSaw = 8, CleanStock = 9, GunnarStrike = 10
}

internal enum RuneRarity { Common, Rare, Legendary, Mythic, Gunnar }
internal enum ForgeTier { Simple, Enhanced, Master }

internal sealed class AxeRune
{
    internal readonly AxeRuneId Id;
    internal readonly RuneRarity Rarity;
    readonly string name, nameEnglish, effect, effectEnglish;

    internal AxeRune(AxeRuneId id, RuneRarity rarity, string name, string nameEnglish, string effect, string effectEnglish)
    {
        Id = id;
        Rarity = rarity;
        this.name = name;
        this.nameEnglish = nameEnglish;
        this.effect = effect;
        this.effectEnglish = effectEnglish;
    }

    internal string Name => GameLanguage.English ? nameEnglish : name;
    internal string Effect => GameLanguage.English ? effectEnglish : effect;
}

internal static class AxeRunes
{
    internal const float GripInterval = .04f;
    internal const float EdgePower = .16f;
    internal const int ReserveSlots = 2;
    internal const int SturdyInterval = 5;
    internal const int SparkInterval = 3;
    internal const int MasterLimit = 3;
    internal const int OddsScale = 1000;

    static readonly AxeRune[] Catalog =
    {
        new AxeRune(AxeRuneId.None, RuneRarity.Common, "Пустое гнездо", "Empty socket", "Можно установить одну руну.", "Holds one rune."),
        new AxeRune(AxeRuneId.LightGrip, RuneRarity.Common, "Лёгкая рукоять", "Light Grip",
            "На 0,04 с меньше между ударами.", "0.04 s less between swings."),
        new AxeRune(AxeRuneId.CarefulCollect, RuneRarity.Common, "Бережный сбор", "Careful Harvest",
            "Одно взятие собирает готовые секции одного ствола, сколько влезет.", "Pick up ready sections of one trunk with a single action, up to capacity."),
        new AxeRune(AxeRuneId.SharpEdge, RuneRarity.Rare, "Острая кромка", "Sharp Edge",
            "+0,16 к силе удара. Общий предел ×2,5.", "+0.16 chopping power, within the ×2.5 cap."),
        new AxeRune(AxeRuneId.TravelReserve, RuneRarity.Rare, "Запас в дороге", "Trail Reserve",
            "+2 места для брёвен, пока топор в руках.", "+2 log slots while this axe is equipped."),
        new AxeRune(AxeRuneId.SturdyWood, RuneRarity.Rare, "Крепкая древесина", "Sturdy Wood",
            "+1 бревно за каждое пятое полностью собранное обычное зрелое или старое дерево.",
            "+1 log for every fifth fully collected ordinary mature or old tree."),
        new AxeRune(AxeRuneId.ForestSpark, RuneRarity.Rare, "Лесная искра", "Forest Spark",
            "+1 эссенция за каждое третье полностью собранное рунное дерево.", "+1 essence for every third fully collected rune tree."),
        new AxeRune(AxeRuneId.SummerSpark, RuneRarity.Legendary, "Летняя искра", "Summer Spark",
            "Каждый третий точный удар по одному дереву добавляет силу обычного удара.",
            "Every third precise hit on the same tree adds one normal hit's progress."),
        new AxeRune(AxeRuneId.RunicSaw, RuneRarity.Mythic, "Рунная пила", "Runic Saw",
            "Режим досок превращает 2 брёвна зрелого или старого дерева в доску после разделки.",
            "In board mode, convert two logs from a mature or old tree into one board after processing."),
        new AxeRune(AxeRuneId.CleanStock, RuneRarity.Mythic, "Чистая заготовка", "Clean Stock",
            "+1 доска за каждое третье полностью разделанное зрелое или старое дерево.",
            "+1 board for every third fully processed mature or old tree."),
        new AxeRune(AxeRuneId.GunnarStrike, RuneRarity.Gunnar, "Удар Гуннара", "Gunnar's Strike",
            "Валит обычное дерево первым ударом. Молодое даёт 1 бревно и 1 доску, зрелое 2 и 1, старое 4 и 2.",
            "Fell ordinary trees in one hit. Young: 1 log and 1 board, mature: 2 and 1, old: 4 and 2.")
    };

    static readonly AxeRuneId[][] Pools =
    {
        new[] { AxeRuneId.LightGrip, AxeRuneId.CarefulCollect },
        new[] { AxeRuneId.SharpEdge, AxeRuneId.TravelReserve, AxeRuneId.SturdyWood, AxeRuneId.ForestSpark },
        new[] { AxeRuneId.SummerSpark },
        new[] { AxeRuneId.RunicSaw, AxeRuneId.CleanStock },
        new[] { AxeRuneId.GunnarStrike }
    };
    static readonly int[,] Odds = { { 900, 90, 9, 1, 0 }, { 450, 480, 65, 5, 0 }, { 0, 450, 450, 99, 1 } };
    static readonly int[] Coins = { 0, 40, 100 };

    internal static bool IsValid(AxeRuneId id) => (uint)id < (uint)Catalog.Length;
    internal static AxeRune Get(AxeRuneId id) => Catalog[IsValid(id) ? (int)id : 0];
    internal static bool ValidTier(ForgeTier tier) => (uint)tier < (uint)Coins.Length;
    internal static int EssenceCost(ForgeTier tier) => (int)tier + 1;
    internal static int BaseCoins(ForgeTier tier) => Coins[(int)tier];

    internal static int Weight(ForgeTier tier, RuneRarity rarity, bool guaranteed = false)
    {
        if (tier == ForgeTier.Master && guaranteed)
            return rarity == RuneRarity.Mythic ? 999 : rarity == RuneRarity.Gunnar ? 1 : 0;
        return Odds[(int)tier, (int)rarity];
    }

    internal static RuneRarity PickRarity(ForgeTier tier, bool guaranteed, int roll)
    {
        if (tier == ForgeTier.Master)
        {
            if (roll == 0) return RuneRarity.Gunnar;
            roll--;
        }
        for (int i = 0; i < (int)RuneRarity.Gunnar; i++)
        {
            roll -= Weight(tier, (RuneRarity)i, guaranteed);
            if (roll < 0) return (RuneRarity)i;
        }
        throw new ArgumentOutOfRangeException(nameof(roll));
    }

    internal static ForgeRuneOffer Roll(ForgeTier tier, AxeId axe, int coins, bool guaranteed, Func<int, int> random)
    {
        var rarity = PickRarity(tier, guaranteed, random(OddsScale));
        var pool = Pools[(int)rarity];
        int first = random(pool.Length);
        int second = pool.Length > 1 ? random(pool.Length - 1) : -1;
        if (second >= first) second++;
        return new ForgeRuneOffer
        {
            tier = tier, axe = axe, coins = coins, guaranteed = guaranteed,
            first = pool[first], second = second < 0 ? AxeRuneId.None : pool[second]
        };
    }

    internal static bool Supports(AxeId axe) => AxeCatalog.IsValid(axe) && axe != AxeId.Old;
    internal static bool Qualifies(TreeSize size, bool ordinary) => ordinary && size != TreeSize.Young;
    internal static int GunnarLogs(TreeSize size) => size == TreeSize.Old ? 4 : size == TreeSize.Mature ? 2 : 1;
    internal static int GunnarBoards(TreeSize size) => size == TreeSize.Old ? 2 : 1;

    internal static int ApplyRecipe(AxeRuneId rune, TreeSize size, bool ordinary, bool boardsMode, ref int wood, ref int cuts)
    {
        if (rune == AxeRuneId.GunnarStrike && ordinary)
        {
            wood = GunnarLogs(size);
            cuts = 0;
            return GunnarBoards(size);
        }
        if (rune == AxeRuneId.RunicSaw && boardsMode && Qualifies(size, ordinary) && wood >= 2)
        {
            wood -= 2;
            return 1;
        }
        return 0;
    }

    internal static bool AdvanceCycle(ref int count, int interval)
    {
        count = (count + 1) % interval;
        return count == 0;
    }
}

[Serializable]
internal sealed class ForgeRuneOffer
{
    public ForgeTier tier;
    public AxeId axe;
    public AxeRuneId first, second;
    public int coins;
    public bool guaranteed;

    internal RuneRarity Rarity => AxeRunes.Get(first).Rarity;
    internal bool Accepts(AxeRuneId rune) => rune == AxeRuneId.None || rune == first || rune == second;

    internal bool Valid() => AxeRunes.ValidTier(tier) && AxeRunes.Supports(axe) &&
        first != AxeRuneId.None && AxeRunes.IsValid(first) && AxeRunes.IsValid(second) &&
        first != second && (second == AxeRuneId.None || AxeRunes.Get(second).Rarity == Rarity) &&
        AxeRunes.Weight(tier, Rarity, guaranteed) > 0 &&
        (coins == AxeRunes.BaseCoins(tier) || coins == (AxeRunes.BaseCoins(tier) * 8 + 9) / 10);
}

[Serializable]
internal sealed class PlayerRuneLoadout
{
    public AxeRuneId[] sockets = new AxeRuneId[AxeCatalog.All.Length];
    public ForgeRuneOffer pending;
    public bool sawBoards;
    public int masterMisses, sturdyWood, forestSpark, cleanStock;

    internal bool MasterGuaranteed => masterMisses >= AxeRunes.MasterLimit;

    internal void SetOffer(ForgeRuneOffer offer)
    {
        pending = offer;
        if (offer.tier == ForgeTier.Master)
            masterMisses = offer.Rarity >= RuneRarity.Mythic ? 0 : masterMisses + 1;
    }

    internal void Normalize(Func<AxeId, bool> owns)
    {
        var valid = new AxeRuneId[AxeCatalog.All.Length];
        if (sockets != null)
            for (int i = 1; i < Math.Min(valid.Length, sockets.Length); i++)
                if (owns((AxeId)i) && AxeRunes.IsValid(sockets[i])) valid[i] = sockets[i];
        sockets = valid;
        if (pending != null && (!pending.Valid() || !owns(pending.axe))) pending = null;
        masterMisses = Math.Max(0, Math.Min(AxeRunes.MasterLimit, masterMisses));
        sturdyWood = Math.Max(0, Math.Min(AxeRunes.SturdyInterval - 1, sturdyWood));
        forestSpark = Math.Max(0, Math.Min(AxeRunes.SparkInterval - 1, forestSpark));
        cleanStock = Math.Max(0, Math.Min(AxeRunes.SparkInterval - 1, cleanStock));
    }
}
