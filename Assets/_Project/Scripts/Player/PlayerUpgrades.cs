// Assets/_Project/Scripts/Player/PlayerUpgrades.cs (НЕ в Editor)
// Прокачка игрока по GDD v0.2 (раздел 3). Чистая логика, без UI: окно прокачки читает и вызывает этот класс.
// Вешать вручную не нужно: PlayerInteraction добавит компонент сам.
//   TryBuy(type)            купить следующий уровень за монеты
//   TryUseFreePoint(type)   потратить «бесплатное очко» (даются на уровнях 3, 5, 7, 9)
//   TryBuyTalent(index)     изучить талант за монеты (дерево талантов: TalentTree, 3 ветки по 5 узлов)
// Таланты не вводят новых систем: они лишь добавляют бонус к тем же шести значениям (время рубки, скорость, рюкзак,
// двойная добыча, цена продажи, время пилы), поэтому остальные скрипты менять не пришлось.
using System;
using UnityEngine;

public enum UpgradeType { ChopPower = 0, MoveSpeed = 1, Capacity = 2, DoubleYield = 3, Haggle = 4, SawSpeed = 5 }

public enum TalentStat { ChopTime, SawTime, Speed, Capacity, Double, Sell }
public enum TalentState { Bought, Available, NeedParent, NeedLevel }

// суммарный бонус всех изученных талантов
public struct TalentBonus
{
    public float chopMul, sawMul;            // множители времени (меньше 1 = быстрее)
    public float speedAdd, doubleAdd, sellAdd;
    public int capAdd;

    public static TalentBonus None { get { return new TalentBonus { chopMul = 1f, sawMul = 1f }; } }
}

public class TalentNode
{
    public string name;
    public TalentStat stat;
    public float value;      // для времени: множитель (0.95), для остальных: прибавка (0.05, у рюкзака число бревен)
    public int cost;         // монеты
    public int minLevel;     // уровень игрока
    public int parent;       // номер узла внутри ветки (0..4), -1 если корень

    public TalentNode(string name, TalentStat stat, float value, int cost, int minLevel, int parent)
    {
        this.name = name; this.stat = stat; this.value = value; this.cost = cost; this.minLevel = minLevel; this.parent = parent;
    }
}

// Дерево: 3 ветки × 5 узлов. Номер узла в ветке: 0 = ярус I, 1 и 2 = ярус II (левый / правый), 3 и 4 = ярус III.
// Правые узлы II и III закрыты уровнем игрока. Ярус III растёт от левого узла яруса II (как на макете).
public static class TalentTree
{
    public const int Branches = 3, PerBranch = 5, Count = Branches * PerBranch;

    public static readonly string[] BranchNames = { "Добытчик", "Мастер", "Лагерь" };

    public static readonly TalentNode[] Nodes =
    {
        // ветка 0: добыча и рюкзак
        new TalentNode("Богатая вырубка",       TalentStat.Double,   0.04f, 100, 1, -1),
        new TalentNode("Вместительный рюкзак",  TalentStat.Capacity, 2f,    180, 1,  0),
        new TalentNode("Зоркий глаз",           TalentStat.Double,   0.05f, 260, 4,  0),
        new TalentNode("Запасливый",            TalentStat.Capacity, 3f,    380, 1,  1),
        new TalentNode("Лесной дар",            TalentStat.Double,   0.06f, 520, 6,  1),
        // ветка 1: инструменты
        new TalentNode("Острое лезвие",         TalentStat.ChopTime, 0.95f, 100, 1, -1),
        new TalentNode("Точная пила",           TalentStat.SawTime,  0.92f, 180, 1,  0),
        new TalentNode("Крепкая рука",          TalentStat.ChopTime, 0.94f, 260, 4,  0),
        new TalentNode("Быстрое полотно",       TalentStat.SawTime,  0.90f, 380, 1,  1),
        new TalentNode("Меткий удар",           TalentStat.Double,   0.05f, 520, 6,  1),
        // ветка 2: лагерь и торговля
        new TalentNode("Тёплый костёр",         TalentStat.Sell,     0.05f, 100, 1, -1),
        new TalentNode("Привал",                TalentStat.Speed,    0.05f, 180, 1,  0),
        new TalentNode("Хваткий торговец",      TalentStat.Sell,     0.05f, 260, 4,  0),
        new TalentNode("Лёгкая поступь",        TalentStat.Speed,    0.06f, 380, 1,  1),
        new TalentNode("Золотая жила",          TalentStat.Sell,     0.08f, 520, 6,  1),
    };

    public static int Index(int branch, int node) { return branch * PerBranch + node; }

    // индекс родителя в общем массиве, -1 у корня
    public static int ParentIndex(int idx)
    {
        int p = Nodes[idx].parent;
        return p < 0 ? -1 : (idx / PerBranch) * PerBranch + p;
    }

    public static string Describe(TalentNode n)
    {
        switch (n.stat)
        {
            case TalentStat.ChopTime: return "Время рубки −" + Mathf.RoundToInt((1f - n.value) * 100f) + "%";
            case TalentStat.SawTime: return "Время на доску −" + Mathf.RoundToInt((1f - n.value) * 100f) + "%";
            case TalentStat.Speed: return "Скорость бега +" + Mathf.RoundToInt(n.value * 100f) + "%";
            case TalentStat.Capacity: return "Рюкзак вмещает на " + Mathf.RoundToInt(n.value) + " бревна больше";
            case TalentStat.Double: return "Шанс двойной добычи +" + Mathf.RoundToInt(n.value * 100f) + "%";
            default: return "Цена продажи +" + Mathf.RoundToInt(n.value * 100f) + "%";
        }
    }
}

[DisallowMultipleComponent]
public class PlayerUpgrades : MonoBehaviour, ISaveable
{
    public static PlayerUpgrades Instance { get; private set; }

    [Header("Только для просмотра")]
    [SerializeField] int[] levels = new int[6];
    [SerializeField] bool[] talents = new bool[TalentTree.Count];
    public int freePoints;

    public event Action Changed;

    PlayerInventory inventory;
    PlayerStats stats;
    TalentBonus bonus = TalentBonus.None;

    public TalentBonus Bonus { get { return bonus; } }

    // ---------- единая формула значений ----------
    // Возвращает: время (с) для рубки и пилы, МНОЖИТЕЛЬ для скорости и цены продажи, число бревен для рюкзака, шанс 0..1 для двойной добычи.
    public static float Calc(UpgradeType t, int lvl, TalentBonus b)
    {
        switch (t)
        {
            case UpgradeType.ChopPower: return GameBalance.ChopTime * Mathf.Pow(GameBalance.ChopTimeFactor, lvl) * b.chopMul;
            case UpgradeType.MoveSpeed: return 1f + GameBalance.SpeedPerLevel * lvl + b.speedAdd;
            case UpgradeType.Capacity: return GameBalance.StartBackpack + GameBalance.CapacityPerLevel * lvl + b.capAdd;
            case UpgradeType.DoubleYield: return Mathf.Clamp01(GameBalance.DoublePerLevel * lvl + b.doubleAdd);
            case UpgradeType.Haggle: return 1f + GameBalance.HagglePerLevel * lvl + b.sellAdd;
            default: return GameBalance.PlankTime * Mathf.Pow(GameBalance.SawTimeFactor, lvl) * b.sawMul;
        }
    }

    // значение для заданного уровня с учётом изученных талантов (для «до» и «после» в окне)
    public float ValueAt(UpgradeType t, int lvl) { return Calc(t, lvl, bonus); }

    // ---------- значения для остальных скриптов ----------
    public float ChopTime { get { return ValueAt(UpgradeType.ChopPower, Level(UpgradeType.ChopPower)); } }
    public float SpeedMultiplier { get { return ValueAt(UpgradeType.MoveSpeed, Level(UpgradeType.MoveSpeed)); } }
    public int Capacity { get { return Mathf.RoundToInt(ValueAt(UpgradeType.Capacity, Level(UpgradeType.Capacity))); } }
    public float DoubleChance { get { return ValueAt(UpgradeType.DoubleYield, Level(UpgradeType.DoubleYield)); } }
    public float SellMultiplier { get { return ValueAt(UpgradeType.Haggle, Level(UpgradeType.Haggle)); } }
    public float SawTime { get { return ValueAt(UpgradeType.SawSpeed, Level(UpgradeType.SawSpeed)); } }

    // ---------- состояние ----------
    public int Level(UpgradeType t) { return levels[(int)t]; }
    public int MaxLevel(UpgradeType t) { return GameBalance.UpgradeMaxLevel[(int)t]; }
    public bool IsMax(UpgradeType t) { return Level(t) >= MaxLevel(t); }

    public bool IsUnlocked(UpgradeType t)
    {
        if (t == UpgradeType.SawSpeed) return stats != null && stats.currentLevel >= GameBalance.SawUnlockLevel;
        return true;
    }

    // цена следующего уровня, -1 если уже максимум
    public int NextCost(UpgradeType t)
    {
        return IsMax(t) ? -1 : GameBalance.UpgradeCost((int)t, Level(t));
    }

    public bool CanBuy(UpgradeType t)
    {
        int cost = NextCost(t);
        return cost >= 0 && IsUnlocked(t) && inventory != null && inventory.coinsCount >= cost;
    }

    public bool TryBuy(UpgradeType t)
    {
        if (!CanBuy(t)) return false;
        inventory.AddCoins(-NextCost(t));
        levels[(int)t]++;
        SaveSystem.RequestSave("upgrades");
        Notify();
        return true;
    }

    public bool TryUseFreePoint(UpgradeType t)
    {
        if (freePoints <= 0 || IsMax(t) || !IsUnlocked(t)) return false;
        freePoints--;
        levels[(int)t]++;
        SaveSystem.RequestSave("upgrades");
        Notify();
        return true;
    }

    // ---------- таланты ----------
    public bool HasTalent(int idx) { return idx >= 0 && idx < talents.Length && talents[idx]; }

    public int PlayerLevel { get { return stats != null ? stats.currentLevel : 1; } }

    public TalentState GetTalentState(int idx)
    {
        if (HasTalent(idx)) return TalentState.Bought;
        int p = TalentTree.ParentIndex(idx);
        if (p >= 0 && !HasTalent(p)) return TalentState.NeedParent;
        if (PlayerLevel < TalentTree.Nodes[idx].minLevel) return TalentState.NeedLevel;
        return TalentState.Available;
    }

    public bool CanBuyTalent(int idx)
    {
        return GetTalentState(idx) == TalentState.Available && inventory != null && inventory.coinsCount >= TalentTree.Nodes[idx].cost;
    }

    public bool TryBuyTalent(int idx)
    {
        if (!CanBuyTalent(idx)) return false;
        inventory.AddCoins(-TalentTree.Nodes[idx].cost);
        talents[idx] = true;
        RecalcBonus();
        SaveSystem.RequestSave("upgrades");
        Notify();
        return true;
    }

    void RecalcBonus()
    {
        var b = TalentBonus.None;
        for (int i = 0; i < TalentTree.Count && i < talents.Length; i++)
        {
            if (!talents[i]) continue;
            var n = TalentTree.Nodes[i];
            switch (n.stat)
            {
                case TalentStat.ChopTime: b.chopMul *= n.value; break;
                case TalentStat.SawTime: b.sawMul *= n.value; break;
                case TalentStat.Speed: b.speedAdd += n.value; break;
                case TalentStat.Capacity: b.capAdd += Mathf.RoundToInt(n.value); break;
                case TalentStat.Double: b.doubleAdd += n.value; break;
                case TalentStat.Sell: b.sellAdd += n.value; break;
            }
        }
        // навыки носильщика (задача 4): складываются в те же значения, поэтому окно «Улучшения» показывает реальные числа
        if (PlayerSkills.Has(SkillIds.CarryLegs)) b.speedAdd += GameBalance.SkillLegsBonus;
        if (PlayerSkills.Has(SkillIds.CarryBasket)) b.capAdd += GameBalance.SkillBasketBonus;
        // питомцы (GDD 4.10): их бонусы складываются с талантами в те же шесть значений
        if (PetManager.Instance != null) PetBonus.ApplyTo(ref b, PetManager.Instance.Bonus);
        bonus = b;
    }

    void Notify()
    {
        if (stats != null) stats.coins = inventory != null ? inventory.coinsCount : stats.coins;
        if (Changed != null) Changed();
    }

    // ---------- отладка (для Editor-меню Tools/BuilderSim/Тест прокачки) ----------
    public void DebugReset()
    {
        levels = new int[6];
        talents = new bool[TalentTree.Count];
        freePoints = 0;
        RecalcBonus();
        SaveSystem.RequestSave("upgrades");
        Notify();
    }

    public void DebugMaxAll()
    {
        for (int i = 0; i < levels.Length; i++) levels[i] = GameBalance.UpgradeMaxLevel[i];
        for (int i = 0; i < talents.Length; i++) talents[i] = true;
        RecalcBonus();
        SaveSystem.RequestSave("upgrades");
        Notify();
    }

    public void DebugAddFreePoint() { freePoints++; SaveSystem.RequestSave("upgrades"); Notify(); }

    // ---------- сохранение (ISaveable, секция «upgrades», LoadOrder 30) ----------
    // ДОБАВЛЯТЬ ПОЛЯ ТОЛЬКО С ДЕФОЛТАМИ: старые файлы без них читаются нормально
    [Serializable]
    class SaveData
    {
        public int version = 1;
        public int[] levels = new int[6];
        public bool[] talents = new bool[TalentTree.Count];
        public int freePoints;
    }

    public string SaveKey { get { return "upgrades"; } }
    public int LoadOrder { get { return 30; } }

    public string Save()
    {
        return JsonUtility.ToJson(new SaveData
        {
            levels = (int[])levels.Clone(),
            talents = (bool[])talents.Clone(),
            freePoints = freePoints
        });
    }

    // Без наград, событий и RequestSave (правило ISaveable). Слушателей уведомит OnSaveLoaded
    public void Load(string json, int formatVersion)
    {
        SaveData d = null;
        if (!string.IsNullOrEmpty(json))
        {
            try { d = JsonUtility.FromJson<SaveData>(json); }
            catch (Exception e) { Debug.LogWarning("PlayerUpgrades: не удалось прочитать сохранение прокачки: " + e.Message); }
        }
        if (d == null) { ResetToDefault(); return; }

        levels = new int[6];
        for (int i = 0; i < levels.Length; i++)
            levels[i] = d.levels != null && i < d.levels.Length ? Mathf.Clamp(d.levels[i], 0, GameBalance.UpgradeMaxLevel[i]) : 0;

        talents = new bool[TalentTree.Count];
        for (int i = 0; i < talents.Length; i++)
            talents[i] = d.talents != null && i < d.talents.Length && d.talents[i];

        freePoints = Mathf.Max(0, d.freePoints);
        RecalcBonus();
    }

    public void ResetToDefault()
    {
        levels = new int[6];
        talents = new bool[TalentTree.Count];
        freePoints = 0;
        RecalcBonus();
    }

    // «Продолжить» применил секцию без событий: сообщаем окну прокачки один раз
    void OnSaveLoaded()
    {
        RecalcBonus();
        Notify();
    }

    // ---------- жизненный цикл ----------
    void Awake()
    {
        Instance = this;
        inventory = GetComponent<PlayerInventory>();
        stats = GetComponent<PlayerStats>();
        if (levels == null || levels.Length != 6) levels = new int[6];
        if (talents == null || talents.Length != TalentTree.Count) talents = new bool[TalentTree.Count];
        RecalcBonus();
        SaveSystem.Register(this);   // если файл уже прочитан, секция «upgrades» применится прямо здесь (Load)
    }

    void OnEnable()
    {
        GameEvents.LevelUp += OnLevelUp; PetManager.Changed += OnPetsChanged; SaveSystem.Loaded += OnSaveLoaded;
        PlayerSkills.Changed -= OnSkillsChanged; PlayerSkills.Changed += OnSkillsChanged;
    }
    void OnDisable()
    {
        GameEvents.LevelUp -= OnLevelUp; PetManager.Changed -= OnPetsChanged; SaveSystem.Loaded -= OnSaveLoaded;
        PlayerSkills.Changed -= OnSkillsChanged;
    }

    // купили или сбросили навык: бонусы бега и рюкзака пересчитываются сразу
    void OnSkillsChanged()
    {
        RecalcBonus();
        Notify();
    }
    void OnDestroy() { SaveSystem.Unregister(this); if (Instance == this) Instance = null; }

    // питомцев взяли, сняли или отпустили: пересчитать бонусы
    void OnPetsChanged()
    {
        RecalcBonus();
        Notify();
    }

    void OnLevelUp(int level)
    {
        if (Array.IndexOf(GameBalance.FreePointLevels, level) < 0) return;
        freePoints++;
        SaveSystem.RequestSave("upgrades");
        if (Changed != null) Changed();
    }
}
