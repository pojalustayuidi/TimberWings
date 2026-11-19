using System;
using UnityEngine;

// Keep serialized indices stable. DoubleYield and Haggle are legacy ranks, with pet effects still supported.
public enum UpgradeType { ChopPower, MoveSpeed, Capacity, DoubleYield, Haggle, SawSpeed }

public struct TalentBonus
{
    public float chopMul, sawMul;
    public float speedAdd, doubleAdd, sellAdd;
    public int capAdd;

    public static TalentBonus None => new TalentBonus { chopMul = 1f, sawMul = 1f };
}

[DisallowMultipleComponent]
public class PlayerUpgrades : MonoBehaviour, ISaveable
{
    public static PlayerUpgrades Instance { get; private set; }

    internal static readonly UpgradeType[] Tracks =
        { UpgradeType.ChopPower, UpgradeType.Capacity, UpgradeType.MoveSpeed, UpgradeType.SawSpeed };

    const int RankCount = 6;
    [SerializeField] int[] levels = new int[RankCount];
    int[] paid = new int[RankCount];
    [SerializeField] int freeSpent;
    int debugPoints;
    PlayerInventory inventory;
    PlayerStats stats;
    TalentBonus bonus = TalentBonus.None;

    public TalentBonus Bonus => bonus;
    public int PlayerLevel => stats != null ? stats.currentLevel : PlayerStats.Level;
    public int freePoints => Mathf.Max(0, PlayerLevel - 1 + debugPoints - freeSpent);
    public event Action Changed;

    public static float Calc(UpgradeType type, int rank, TalentBonus b)
    {
        rank = Mathf.Clamp(rank, 0, GameBalance.UpgradeMaxLevel[(int)type]);
        switch (type)
        {
            case UpgradeType.ChopPower: return GameBalance.ChopTime / (1f + GameBalance.ChopPowerBonus[rank]) * b.chopMul;
            case UpgradeType.MoveSpeed: return GameBalance.RunSpeed[rank] / GameBalance.PlayerSpeed + b.speedAdd;
            case UpgradeType.Capacity: return GameBalance.BackpackCapacity[rank] + b.capAdd;
            case UpgradeType.DoubleYield: return Mathf.Clamp01(.05f * rank + b.doubleAdd);
            case UpgradeType.Haggle: return 1f + .05f * rank + b.sellAdd;
            default: return GameBalance.SawSeconds[rank] * b.sawMul;
        }
    }

    public float ValueAt(UpgradeType type, int rank) => Calc(type, rank, bonus);
    float Current(UpgradeType type) => ValueAt(type, Level(type));
    public float ChopTime => Current(UpgradeType.ChopPower);
    public float SpeedMultiplier => Current(UpgradeType.MoveSpeed);
    public int Capacity => Mathf.RoundToInt(Current(UpgradeType.Capacity));
    public float DoubleChance => Current(UpgradeType.DoubleYield);
    public float SellMultiplier => Current(UpgradeType.Haggle);
    public float SawTime => Current(UpgradeType.SawSpeed);

    public int Level(UpgradeType type) => levels[(int)type];
    public int MaxLevel(UpgradeType type) => GameBalance.UpgradeMaxLevel[(int)type];
    public bool IsMax(UpgradeType type) => Level(type) >= MaxLevel(type);
    public int NextRequiredLevel(UpgradeType type) =>
        GameBalance.UpgradeUnlockLevels[Mathf.Min(Level(type), GameBalance.UpgradeUnlockLevels.Length - 1)];

    public bool IsUnlocked(UpgradeType type)
    {
        if (Array.IndexOf(Tracks, type) < 0 || PlayerLevel < NextRequiredLevel(type)) return false;
        if (type != UpgradeType.SawSpeed) return true;
        var story = StoryFlow.Instance;
        var workshop = story != null ? story.workshop : PlankWorkshop.ActiveWorkshop;
        return workshop != null && workshop.unlocked;
    }

    public int NextCost(UpgradeType type) => IsMax(type) || Array.IndexOf(Tracks, type) < 0
        ? -1 : GameBalance.UpgradeCost((int)type, paid[(int)type]);

    public bool CanBuy(UpgradeType type)
    {
        int cost = NextCost(type);
        return cost >= 0 && !SaveSystem.IsLoading && IsUnlocked(type) &&
            inventory != null && inventory.coinsCount >= cost;
    }

    public bool TryBuy(UpgradeType type)
    {
        if (!CanBuy(type)) return false;
        int cost = NextCost(type);
        paid[(int)type]++;
        levels[(int)type]++;
        inventory.TrySpend(cost);
        SaveSystem.RequestSave(SaveKey);
        Notify();
        return true;
    }

    public bool TryUseFreePoint(UpgradeType type)
    {
        if (SaveSystem.IsLoading || freePoints <= 0 || IsMax(type) || !IsUnlocked(type)) return false;
        freeSpent++;
        levels[(int)type]++;
        SaveSystem.RequestSave(SaveKey);
        Notify();
        return true;
    }

    void Notify()
    {
        if (stats != null && inventory != null) stats.coins = inventory.coinsCount;
        Changed?.Invoke();
    }

    void RefreshBonuses()
    {
        bonus = TalentBonus.None;
        if (PetManager.Instance != null) PetBonus.ApplyTo(ref bonus, PetManager.Instance.Bonus);
        Notify();
    }

    public void DebugReset()
    {
        ResetToDefault();
        SaveSystem.RequestSave(SaveKey);
        Notify();
    }

    public void DebugMaxAll()
    {
        foreach (UpgradeType type in Tracks) levels[(int)type] = MaxLevel(type);
        SaveSystem.RequestSave(SaveKey);
        Notify();
    }

    public void DebugAddFreePoint() { debugPoints++; Notify(); }

    [Serializable]
    class SaveData
    {
        public int[] levels, paid;
        public int freeSpent, freePoints;
    }

    public string SaveKey => "upgrades";
    public int LoadOrder => 30;

    public string Save() => JsonUtility.ToJson(new SaveData { levels = levels, paid = paid, freeSpent = freeSpent });

    public void Load(string json, int formatVersion)
    {
        ResetToDefault();
        if (string.IsNullOrEmpty(json)) return;
        var data = JsonUtility.FromJson<SaveData>(json);
        if (data == null) return;
        for (int i = 0; i < RankCount; i++)
        {
            levels[i] = Mathf.Clamp(ReadRank(data.levels, i), 0, GameBalance.UpgradeMaxLevel[i]);
            paid[i] = data.paid != null ? Mathf.Clamp(ReadRank(data.paid, i), 0, levels[i]) : levels[i];
        }
        freeSpent = Mathf.Max(0, data.freeSpent);
        if (data.paid != null) return;

        // Legacy saves recorded available points but skipped the purchase method in each track.
        int oldLevel = stats != null ? stats.SavedLevel : PlayerLevel;
        int oldSpent = Mathf.Max(0, Mathf.Clamp((oldLevel - 1) / 2, 0, 4) - data.freePoints);
        foreach (UpgradeType type in Tracks)
        {
            int discount = Mathf.Min(oldSpent, paid[(int)type]);
            paid[(int)type] -= discount;
            freeSpent += discount;
            oldSpent -= discount;
        }
    }

    static int ReadRank(int[] data, int i) => data != null && i < data.Length ? data[i] : 0;

    public void ResetToDefault()
    {
        levels = new int[RankCount];
        paid = new int[RankCount];
        debugPoints = freeSpent = 0;
    }

    void Awake()
    {
        Instance = this;
        inventory = GetComponent<PlayerInventory>();
        stats = GetComponent<PlayerStats>();
        SaveSystem.Register(this);
    }

    void OnEnable()
    {
        GameEvents.LevelUp += OnLevelUp;
        PetManager.Changed += RefreshBonuses;
        SaveSystem.Loaded += RefreshBonuses;
        SaveSystem.NewGameStarted += RefreshBonuses;
    }

    void Start() => RefreshBonuses();

    void OnDisable()
    {
        GameEvents.LevelUp -= OnLevelUp;
        PetManager.Changed -= RefreshBonuses;
        SaveSystem.Loaded -= RefreshBonuses;
        SaveSystem.NewGameStarted -= RefreshBonuses;
    }

    void OnDestroy()
    {
        SaveSystem.Unregister(this);
        if (Instance == this) Instance = null;
    }

    void OnLevelUp(int level) => Notify();
}
