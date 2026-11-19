using Random = UnityEngine.Random;

internal sealed partial class PlayerAxeEquipment
{
    PlayerRuneLoadout runes = new PlayerRuneLoadout();

    internal AxeRuneId RuneFor(AxeId axe) => AxeRunes.Supports(axe) ? runes.sockets[(int)axe] : AxeRuneId.None;
    internal AxeRuneId ActiveRune => RuneFor(equipped);
    internal int RuneCapacity => ActiveRune == AxeRuneId.TravelReserve ? AxeRunes.ReserveSlots : 0;
    internal static float SwingInterval(AxeDefinition axe, AxeRuneId rune) =>
        axe.Interval - (rune == AxeRuneId.LightGrip ? AxeRunes.GripInterval : 0f);
    internal float EquippedSwingInterval => SwingInterval(Equipped, ActiveRune);
    internal float RunePower => ActiveRune == AxeRuneId.SharpEdge ? AxeRunes.EdgePower : 0f;
    internal float EquippedPower => Equipped.Power + RunePower;
    internal bool BoardsMode => runes.sawBoards;
    internal ForgeRuneOffer PendingRuneOffer => runes.pending;
    internal int MasterMisses => runes.masterMisses;
    internal bool MasterGuaranteed => runes.MasterGuaranteed;
    internal bool SturdyBonusNext => runes.sturdyWood == AxeRunes.SturdyInterval - 1;

    internal bool TierUnlocked(ForgeTier tier)
    {
        switch (tier)
        {
            case ForgeTier.Simple: return ForgeRestored;
            case ForgeTier.Enhanced: return ForgeRestored && Has(AxeId.Steel);
            case ForgeTier.Master: return ForgeRestored && Has(AxeId.Gunnar);
            default: return false;
        }
    }

    internal static int RuneCoinCost(ForgeTier tier) => AxeCatalog.EnchantmentCoinCost(AxeRunes.BaseCoins(tier));

    internal bool CanRollRune(RuneForgeInspection table, AxeId axe, ForgeTier tier)
    {
        return table != null && table.CanUse(this) && CanEquip && inventory != null &&
            runes.pending == null && AxeRunes.Supports(axe) && Has(axe) && TierUnlocked(tier) &&
            inventory.coinsCount >= RuneCoinCost(tier) && inventory.RuneEssence >= AxeRunes.EssenceCost(tier);
    }

    internal bool TryRollRune(RuneForgeInspection table, AxeId axe, ForgeTier tier)
    {
        if (!CanRollRune(table, axe, tier)) return false;
        int coins = RuneCoinCost(tier);
        bool guaranteed = tier == ForgeTier.Master && runes.MasterGuaranteed;
        var offer = AxeRunes.Roll(tier, axe, coins, guaranteed, max => Random.Range(0, max));
        if (!inventory.TrySpend(coins, commit: () => runes.SetOffer(offer), essence: AxeRunes.EssenceCost(tier)))
            return false;
        SaveRuneSelection();
        return true;
    }

    internal bool TryChooseRune(RuneForgeInspection table, ForgeRuneOffer offer, AxeRuneId rune)
    {
        if (table == null || !table.CanUse(this) || !CanEquip || offer == null || runes.pending != offer ||
            !Has(offer.axe) || !offer.Accepts(rune)) return false;
        if (rune != AxeRuneId.None) runes.sockets[(int)offer.axe] = rune;
        runes.pending = null;
        SaveRuneSelection();
        return true;
    }

    internal bool TrySwitchBoardsMode()
    {
        if (!CanEquip || ActiveRune != AxeRuneId.RunicSaw) return false;
        runes.sawBoards = !runes.sawBoards;
        SaveSystem.RequestSave(Key);
        return true;
    }

    internal bool CountRuneHarvest(AxeRuneId rune)
    {
        bool reward;
        switch (rune)
        {
            case AxeRuneId.SturdyWood:
                reward = AxeRunes.AdvanceCycle(ref runes.sturdyWood, AxeRunes.SturdyInterval);
                break;
            case AxeRuneId.ForestSpark:
                reward = AxeRunes.AdvanceCycle(ref runes.forestSpark, AxeRunes.SparkInterval);
                break;
            case AxeRuneId.CleanStock:
                reward = AxeRunes.AdvanceCycle(ref runes.cleanStock, AxeRunes.SparkInterval);
                break;
            default: return false;
        }
        SaveSystem.RequestSave(Key);
        return reward;
    }

    // The wallet and the paid choice are written in the same save snapshot.
    static void SaveRuneSelection()
    {
        SaveSystem.RequestSave(Key);
        SaveSystem.SaveNow();
    }
}
