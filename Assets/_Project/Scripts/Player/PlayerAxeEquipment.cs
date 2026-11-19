using System;
using UnityEngine;

[DisallowMultipleComponent]
internal sealed class PlayerAxeEquipment : MonoBehaviour, ISaveable
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
    }

    int owned = OldAxe;
    AxeId equipped;
    PlayerInventory inventory;
    PlayerInteraction interaction;

    internal AxeDefinition Equipped => AxeCatalog.Get(equipped);
    internal bool ForgeRestored { get; private set; }
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

    internal bool TryBuy(AxeId id, TradingStall stall, StoryFlow story)
    {
        if (!CanBuy(id)) return false;
        if (id == AxeId.Iron && (stall == null || !stall.isActiveAndEnabled ||
            !stall.Unlocked || stall.Inventory != inventory || stall.DistanceToPlayer() > stall.Radius)) return false;
        if (id == AxeId.Steel && (story == null || !story.CanUseAxeForge(this))) return false;

        // Commit ownership before inventory listeners refresh the shop or request a save.
        Obtain(id);
        inventory.TrySpend(AxeCatalog.Get(id).Price);
        return true;
    }

    internal bool CanRestoreForge => CanEquip && !ForgeRestored && inventory != null &&
        inventory.planksCount >= ForgePlanks && inventory.coinsCount >= ForgeCoins;

    internal bool TryRestoreForge(StoryFlow story)
    {
        if (!CanRestoreForge || story == null || !story.CanUseAxeForge(this)) return false;
        ForgeRestored = true;
        inventory.TrySpend(ForgeCoins, ForgePlanks);
        SaveSystem.RequestSave(Key);
        return true;
    }

    internal bool TryRestoreGunnar(StoryFlow story)
    {
        if (!CanEquip || !ForgeRestored || Has(AxeId.Gunnar) || story == null ||
            !story.GunnarBladeAvailable || !story.CanUseAxeForge(this)) return false;
        Obtain(AxeId.Gunnar);
        return true;
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
        forgeRestored = ForgeRestored
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
        ForgeRestored = data.forgeRestored || Has(AxeId.Steel) || Has(AxeId.Gunnar);
    }

    public void ResetToDefault()
    {
        owned = OldAxe;
        equipped = AxeId.Old;
        ForgeRestored = false;
    }
}
