// Assets/_Project/Scripts/Player/PlayerStats.cs (НЕ в Editor)
// Уровень и опыт. Теперь это ISaveable: секция «stats», LoadOrder 10.
using UnityEngine;

public class PlayerStats : MonoBehaviour, ISaveable
{
    [Header("Характеристики")]
    public int currentLevel = 1;
    public int currentXP = 0;
    public int coins = 0;   // копия для просмотра в Inspector, настоящие монеты лежат в PlayerInventory

    private PlayerInventory inventory;

    // Для статических систем (PlayerSkills): текущий уровень без FindObjectOfType
    public static PlayerStats Instance { get; private set; }
    public static int Level { get { return Instance != null ? Instance.currentLevel : 1; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { Instance = null; }

    // ------------------------------------------------------------ сохранение
    [System.Serializable]
    class SaveData
    {
        public int level = 1;
        public int xp = 0;
    }

    void Awake() { Instance = this; SaveSystem.Register(this); }
    void OnDestroy() { SaveSystem.Unregister(this); if (Instance == this) Instance = null; }

    public string SaveKey { get { return "stats"; } }
    public int LoadOrder { get { return 10; } }

    public string Save()
    {
        return JsonUtility.ToJson(new SaveData { level = currentLevel, xp = currentXP });
    }

    // Без наград (монеты за уровень уже лежат в секции inventory), без GameEvents и RequestSave
    public void Load(string json, int formatVersion)
    {
        if (string.IsNullOrEmpty(json)) { ResetToDefault(); return; }
        var d = JsonUtility.FromJson<SaveData>(json);
        if (d == null) { ResetToDefault(); return; }

        currentLevel = Mathf.Clamp(d.level, 1, GameBalance.MaxLevel);
        currentXP = Mathf.Max(0, d.xp);
        RefreshUI();
    }

    public void ResetToDefault()
    {
        currentLevel = 1;
        currentXP = 0;
        RefreshUI();
    }

    // ------------------------------------------------------------ как было
    void Start()
    {
        inventory = GetComponent<PlayerInventory>();
        RefreshUI();
        PlayerSkills.NotifyStatsReady();   // уровень известен: можно проверить сохранённые навыки
    }

    // Формулы GDD v0.2 живут в GameBalance (50 × L опыта на уровень, 15 × L монет за уровень, максимум 10)
    public static int XpForLevel(int n) { return GameBalance.XpForLevel(n); }
    public static int CoinsForLevel(int n) { return GameBalance.CoinsForLevel(n); }

    // Вызываем, когда получаем опыт (срубили дерево или построили здание)
    public void AddXP(int amount)
    {
        if (amount <= 0) return;

        currentXP += amount;
        if (UIManager.Instance != null) UIManager.Instance.ShowXpGain(amount);

        bool coinsChanged = false;

        // while вместо рекурсии: опыта может хватить сразу на несколько уровней
        while (currentLevel < GameBalance.MaxLevel && currentXP >= XpForLevel(currentLevel + 1))
        {
            currentLevel++;
            int reward = CoinsForLevel(currentLevel);

            if (inventory != null) inventory.coinsCount += reward;
            coins = inventory != null ? inventory.coinsCount : coins + reward;
            coinsChanged = true;

            Debug.Log($"НОВЫЙ УРОВЕНЬ {currentLevel}. Монет получено: {reward}. Всего: {coins}");
            GameEvents.RaiseLevelUp(currentLevel);
        }

        RefreshUI();
        SaveSystem.RequestSave("stats");
        if (coinsChanged) SaveSystem.RequestSave("inventory");   // монеты за уровень тоже должны уйти в файл
    }

    // Обновляет HUD: полоса показывает опыт внутри текущего уровня
    public void RefreshUI()
    {
        if (UIManager.Instance == null) return;

        if (currentLevel >= GameBalance.MaxLevel)
        {
            UIManager.Instance.UpdateXP(currentLevel, 1, 1);   // максимальный уровень: полоса полная
        }
        else
        {
            int start = XpForLevel(currentLevel);
            int next = XpForLevel(currentLevel + 1);
            UIManager.Instance.UpdateXP(currentLevel, currentXP - start, next - start);
        }

        if (inventory == null) inventory = GetComponent<PlayerInventory>();
        if (inventory != null)
            UIManager.Instance.UpdateResources(inventory.woodCount, inventory.coinsCount);
    }
}
