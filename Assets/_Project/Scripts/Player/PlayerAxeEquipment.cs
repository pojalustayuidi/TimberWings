using System;
using UnityEngine;

[DisallowMultipleComponent]
internal sealed partial class PlayerAxeEquipment : MonoBehaviour, ISaveable
{
    const string Key = "axes";
    const int OldAxe = 1 << (int)AxeId.Old;
    const int ValidAxes = (1 << 4) - 1;
    internal const int ForgePlanks = 8;
    internal const int ForgeCoins = 60;

    [Serializable]
    sealed class Data
    {
        public int owned, equipped;
        public bool forgeRestored;
        public PlayerRuneLoadout runes;
    }

    int owned = OldAxe;
    AxeId equipped;
    bool legacyForgeRestored;
    PlayerInventory inventory;
    PlayerInteraction interaction;

    internal AxeDefinition Equipped => AxeCatalog.Get(equipped);
    internal bool ForgeRestored => RuneForge.Active != null ? RuneForge.Active.IsRestored : legacyForgeRestored;
    internal bool HasLegacyForgeRestoration => legacyForgeRestored;
    internal bool Has(AxeId id) => AxeCatalog.IsValid(id) && (owned & (1 << (int)id)) != 0;
    internal bool CanEquip => interaction != null && !interaction.IsChopping && !WaterSwim.Swimming &&
        !SaveSystem.IsLoading && !StoryInspection.Active && !BuildWorkPose.MovementLocked &&
        !DialogueManager.IsBusy && !DialogueSequence.AnyPlaying && !LeoCelebration.IsPlaying;

    void Awake()
    {
        inventory = GetComponent<PlayerInventory>();
        interaction = GetComponent<PlayerInteraction>();
        SaveSystem.Register(this);
    }

    void OnDestroy() => SaveSystem.Unregister(this);

    internal bool TryEquip(AxeId id)
    {
        if (!CanEquip || !Has(id)) return false;
        if (id != equipped)
        {
            equipped = id;
            SaveSystem.RequestSave(Key);
        }
        return true;
    }

    internal bool CanBuy(AxeId id) => CanEquip && !Has(id) && inventory != null &&
        (id == AxeId.Iron || id == AxeId.Steel && ForgeRestored) &&
        inventory.coinsCount >= AxeCatalog.Get(id).Price;

    internal bool TryBuy(AxeId id, TradingStall stall, StoryFlow story, RuneForgeInspection table = null)
    {
        if (!CanBuy(id)) return false;
        if (id == AxeId.Iron && (stall == null || !stall.isActiveAndEnabled ||
            !stall.Unlocked || stall.Inventory != inventory || stall.DistanceToPlayer() > stall.Radius)) return false;
        if (id == AxeId.Steel && !CanUseForge(story, table)) return false;

        // Commit ownership before inventory listeners refresh the shop or request a save.
        return inventory.TrySpend(AxeCatalog.Get(id).Price, commit: () => Obtain(id));
    }

    internal bool CanRestoreForge => CanEquip && RuneForge.Active == null && !legacyForgeRestored && inventory != null &&
        inventory.planksCount >= ForgePlanks && inventory.coinsCount >= ForgeCoins;

    internal bool TryRestoreForge(StoryFlow story)
    {
        if (!CanRestoreForge || story == null || !story.CanUseAxeForge(this)) return false;
        return inventory.TrySpend(ForgeCoins, ForgePlanks, () =>
        {
            legacyForgeRestored = true;
            SaveSystem.RequestSave(Key);
        });
    }

    internal bool TryRestoreGunnar(StoryFlow story, RuneForgeInspection table = null)
    {
        if (!CanEquip || !ForgeRestored || Has(AxeId.Gunnar) || story == null ||
            !story.GunnarBladeAvailable || !CanUseForge(story, table)) return false;
        Obtain(AxeId.Gunnar);
        return true;
    }

    bool CanUseForge(StoryFlow story, RuneForgeInspection table) => table != null ? table.CanUse(this) :
        RuneForge.Active == null && story != null && story.CanUseAxeForge(this);

    internal void FinishLegacyForgeMigration()
    {
        legacyForgeRestored = false;
        SaveSystem.RequestSave(Key);
    }

    void Obtain(AxeId id)
    {
        owned |= 1 << (int)id;
        equipped = id;
        SaveSystem.RequestSave(Key);
    }

    public string SaveKey => Key;
    public int LoadOrder => 35;

    public string Save() => JsonUtility.ToJson(new Data
    {
        owned = owned,
        equipped = (int)equipped,
        forgeRestored = legacyForgeRestored,
        runes = runes
    });

    public void Load(string json, int formatVersion)
    {
        if (string.IsNullOrEmpty(json))
        {
            ResetToDefault();
            return;
        }
        var data = JsonUtility.FromJson<Data>(json);
        if (data == null)
        {
            ResetToDefault();
            return;
        }
        owned = (data.owned & ValidAxes) | OldAxe;
        equipped = Has((AxeId)data.equipped) ? (AxeId)data.equipped : AxeId.Old;
        legacyForgeRestored = data.forgeRestored || data.runes == null && (Has(AxeId.Steel) || Has(AxeId.Gunnar));
        runes = data.runes ?? new PlayerRuneLoadout();
        runes.Normalize(Has);
        if (runes.HasDrawHistory && inventory != null) inventory.RestoreRuneEssenceDiscovery();
    }

    public void ResetToDefault()
    {
        owned = OldAxe;
        equipped = AxeId.Old;
        legacyForgeRestored = false;
        runes = new PlayerRuneLoadout();
    }
}
