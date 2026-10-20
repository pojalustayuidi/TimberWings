// Assets/_Project/Scripts/Player/PlayerInventory.cs (НЕ в Editor)
// Ресурсы игрока. Теперь это ISaveable: секция «inventory», LoadOrder 20.
using UnityEngine;
using System.Collections.Generic;

public class PlayerInventory : MonoBehaviour, ISaveable
{
    [Header("Ресурсы")]
    public int woodCount = 0;
    public int coinsCount = 0;
    public int planksCount = 0;   // доски (из мастерской), нужны для заказов и моста

    [Header("Визуальный рюкзак")]
    public Transform backpackPoint;
    public GameObject miniLogPrefab;
    public float spacingX = 0.35f;
    public float spacingY = 0.25f;
    public Vector3 logRotation = new Vector3(90, 0, 0);

    private List<GameObject> stackedLogs = new List<GameObject>();

    // ------------------------------------------------------------ сохранение
    [System.Serializable]
    class SaveData
    {
        public int wood;
        public int coins;
        public int planks;
    }

    static PlayerInventory instance;
    // если Awake ещё не сработал или объект пересоздан: безопасный поиск с кэшем (SceneRef)
    public static PlayerInventory Instance
    {
        get { if (instance == null) instance = SceneRef<PlayerInventory>.Get(); return instance; }
        private set { instance = value; SceneRef<PlayerInventory>.Set(value); }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { Instance = null; }

    void Awake() { Instance = this; SaveSystem.Register(this); }       // если Load() уже был, секция применится прямо здесь
    void OnDestroy() { SaveSystem.Unregister(this); if (Instance == this) Instance = null; }

    public string SaveKey { get { return "inventory"; } }
    public int LoadOrder { get { return 20; } }

    public string Save()
    {
        return JsonUtility.ToJson(new SaveData { wood = woodCount, coins = coinsCount, planks = planksCount });
    }

    // Без наград, событий, тостов и RequestSave (правило ISaveable)
    public void Load(string json, int formatVersion)
    {
        if (string.IsNullOrEmpty(json)) { ResetToDefault(); return; }
        var d = JsonUtility.FromJson<SaveData>(json);
        if (d == null) { ResetToDefault(); return; }

        // woodCount НЕ зажимается до Capacity: на момент Load (LoadOrder 20) PlayerUpgrades и питомцы могли ещё не загрузиться,
        // и вместимость временно меньше настоящей. Зажим здесь молча съел бы брёвна. Если вместимость и правда упала,
        // игрок просто несёт «лишнее» (рубить нельзя, пока не сдаст), а FreeSlots не уходит в минус
        woodCount = Mathf.Max(0, d.wood);
        coinsCount = Mathf.Max(0, d.coins);
        planksCount = Mathf.Max(0, d.planks);
        pendingLogs = 0;   // летевшие в HUD брёвна старой игры не вернутся
        RebuildBackpack();
        UpdateUI();
    }

    public void ResetToDefault()
    {
        woodCount = 0; coinsCount = 0; planksCount = 0;
        pendingLogs = 0;
        RebuildBackpack();
        UpdateUI();
    }

    // ------------------------------------------------------------ рюкзак
    void Start()
    {
        UpdateUI();
    }

    void SpawnLog()
    {
        if (miniLogPrefab == null || backpackPoint == null) return;

        GameObject newLog = Instantiate(miniLogPrefab, backpackPoint);
        int index = stackedLogs.Count;
        int row = index / 2;
        int col = index % 2;

        float xPos = (col == 0) ? -spacingX / 2f : spacingX / 2f;
        float yPos = row * spacingY;

        newLog.transform.localPosition = new Vector3(xPos, yPos, 0);
        newLog.transform.localRotation = Quaternion.Euler(logRotation);
        stackedLogs.Add(newLog);
    }

    // после загрузки визуальные брёвна должны совпадать с woodCount
    void RebuildBackpack()
    {
        for (int i = 0; i < stackedLogs.Count; i++)
            if (stackedLogs[i] != null) Destroy(stackedLogs[i]);
        stackedLogs.Clear();

        for (int i = 0; i < woodCount; i++) SpawnLog();
    }

    // ------------------------------------------------------------ вместимость и «летящие» брёвна
    // Единственный источник вместимости: BackpackTag, HUD и подсказки спрашивают её здесь.
    // Вместимость = 4 + 2·уровень «Рюкзака» + таланты + питомцы (PlayerUpgrades), максимум 16.
    public int Capacity { get { return PlayerUpgrades.Instance != null ? PlayerUpgrades.Instance.Capacity : GameBalance.StartBackpack; } }

    // брёвна, которым место уже ОБЕЩАНО, но они ещё летят в HUD (ChopRewards, ~1 с). Без них второе дерево подряд обошло бы проверку
    int pendingLogs;
    public int PendingLogs { get { return pendingLogs; } }

    // сколько брёвен можно взять прямо сейчас: Capacity − несём − летят. Не бывает меньше 0 (после загрузки woodCount может быть больше Capacity)
    public int FreeSlots { get { return Mathf.Max(0, Capacity - woodCount - pendingLogs); } }

    // ЕДИНАЯ точка выдачи брёвен с дерева: «сколько влезло». Возвращает, сколько брёвен резервируется (0..want).
    // Дальше вызывающий обязан довести каждое зарезервированное бревно до LandReserved (по прилёту в HUD) или вернуть его через CancelReserved.
    public int ReserveLogs(int want)
    {
        if (want <= 0) return 0;
        int n = Mathf.Min(want, FreeSlots);
        pendingLogs += n;
        return n;
    }

    // зарезервированное бревно прилетело: кладём в рюкзак (резерв снимается)
    public void LandReserved(int n = 1)
    {
        if (n <= 0) return;
        pendingLogs = Mathf.Max(0, pendingLogs - n);
        AddRaw(n, true);
    }

    // отказались от резерва (например, дерево не упало)
    public void CancelReserved(int n)
    {
        pendingLogs = Mathf.Max(0, pendingLogs - Mathf.Max(0, n));
    }

    // ------------------------------------------------------------ ресурсы
    // Любой источник брёвен (мастерская, отладка, будущие квесты): зажим до свободных слотов, летящие брёвна учитываются.
    // Возвращает, сколько реально добавлено. Рубка идёт через ReserveLogs, а не сюда.
    public int AddWood(int amount)
    {
        if (amount <= 0) return 0;
        int room = FreeSlots;
        if (amount > room)
            Debug.LogWarning("[PlayerInventory] AddWood(" + amount + "): свободно " + room + " (рюкзак " + woodCount + "/" + Capacity + ", летит " + pendingLogs + "). Лишнее не добавлено.", this);
        return AddRaw(Mathf.Min(amount, room), false);
    }

    // общий конец: жёсткий зажим по самой вместимости (даже для зарезервированных: вместимость могла упасть, пока бревно летело)
    int AddRaw(int amount, bool reserved)
    {
        int room = Mathf.Max(0, Capacity - woodCount);
        int n = Mathf.Min(amount, room);
        if (n < amount)
            Debug.LogWarning("[PlayerInventory] Не влезло " + (amount - n) + " бр. (" + (reserved ? "летевшие" : "прямая выдача") + "): вместимость упала или лимит нарушен.", this);
        if (n <= 0) return 0;

        woodCount += n;
        for (int i = 0; i < n; i++) SpawnLog();
        UpdateUI();
        SaveSystem.RequestSave("inventory");
        return n;
    }

    // true = брёвна списаны. Раньше метод молча ничего не делал, если брёвен не хватало, и вызывающий код мог выдать товар бесплатно
    public bool RemoveWood(int amount)
    {
        if (amount <= 0 || woodCount < amount) return false;

        woodCount -= amount;
        for (int i = 0; i < amount; i++)
        {
            if (stackedLogs.Count > 0)
            {
                GameObject topLog = stackedLogs[stackedLogs.Count - 1];
                stackedLogs.RemoveAt(stackedLogs.Count - 1);
                Destroy(topLog);
            }
        }
        UpdateUI();
        SaveSystem.RequestSave("inventory");
        return true;
    }

    public void AddPlanks(int amount)
    {
        planksCount = Mathf.Max(0, planksCount + amount);
        UpdateUI();
        SaveSystem.RequestSave("inventory");
    }

    public bool RemovePlanks(int amount)
    {
        if (planksCount < amount) return false;
        planksCount -= amount;
        UpdateUI();
        SaveSystem.RequestSave("inventory");
        return true;
    }

    public void AddCoins(int amount)
    {
        coinsCount = Mathf.Max(0, coinsCount + amount);   // в минус уйти нельзя
        UpdateUI();
        SaveSystem.RequestSave("inventory");
    }

    // Любое изменение брёвен, досок или монет (в том числе загрузка сохранения). Подписчики: ObjectiveView (живые счётчики цели).
    // static: переживает пересоздание инвентаря; подписчик отписывается в OnDisable.
    public static event System.Action Changed;

    private void UpdateUI()
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateResources(woodCount, coinsCount);
            UIManager.Instance.UpdatePlanks(planksCount);
        }
        if (Changed != null) Changed();
    }
}
