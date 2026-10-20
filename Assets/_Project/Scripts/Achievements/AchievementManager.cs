// Assets/_Project/Scripts/Achievements/AchievementManager.cs (НЕ в Editor)
// Считает прогресс достижений, открывает их и выдаёт награды. Сам создаётся после загрузки сцены, в сцене ничего настраивать не нужно.
//   AchievementManager.Instance            доступ
//   Progress(def) / IsUnlocked / IsClaimed  состояние
//   Claim(id) / ClaimAll()                  забрать награду (монеты через PlayerInventory, XP через PlayerStats)
//   Unlocked / Changed                      события для UI
//
// Откуда берутся числа:
//   считаем сами (и сохраняем):  время в игре, срубленные деревья, заработанные монеты, закрытые главы
//   читаем у систем (не дублируем): доски (PlankWorkshop), рабочие, уровень, улучшения, навыки, питомцы (Книга)
//   Для «читаемых» метрик хранится лучшее значение: если игрок распустил питомца, прогресс не откатывается.
//
// Сохранение: ISaveable, секция «achievements» (LoadOrder 95, после «time»). Новые поля только с дефолтами.
// Правило ISaveable: во время Load наград, событий и тостов нет. Достижения, выполненные «задним числом» (старый сейв), открываются тихо.
using System;
using System.Collections.Generic;
using UnityEngine;

public class AchievementManager : MonoBehaviour, ISaveable
{
    public static AchievementManager Instance { get; private set; }

    public static event Action<AchievementDef> Unlocked;   // открыто новое достижение (не при загрузке)
    public static event Action Changed;                    // изменились открытые / забранные награды

    const float PollEvery = 0.5f, SaveEvery = 120f;

    // ------------------------------------------------------------ состояние
    float playSeconds;
    int trees, coinsEarned;
    readonly HashSet<string> chapters = new HashSet<string>();
    readonly HashSet<string> unlocked = new HashSet<string>();
    readonly HashSet<string> claimed = new HashSet<string>();
    readonly int[] best = new int[Enum.GetValues(typeof(AchMetric)).Length];

    int lastCoins = -1;
    bool suppressCoins;           // монеты самой награды не считаем «заработанными»
    float nextPoll, nextSave;
    bool startBackfillDone;
    PlankWorkshop workshop;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { Instance = null; Unlocked = null; Changed = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Instance != null) return;
        var go = new GameObject("AchievementManager");
        DontDestroyOnLoad(go);
        go.AddComponent<AchievementManager>();
    }

    // ------------------------------------------------------------ чтение
    public int Total { get { return AchievementDefs.All.Count; } }
    public int UnlockedCount { get { return unlocked.Count; } }
    public int ClaimableCount
    {
        get
        {
            int n = 0;
            foreach (string id in unlocked) if (!claimed.Contains(id)) n++;
            return n;
        }
    }

    public bool IsUnlocked(string id) { return unlocked.Contains(id); }
    public bool IsClaimed(string id) { return claimed.Contains(id); }
    public bool CanClaim(string id) { return unlocked.Contains(id) && !claimed.Contains(id); }

    // сколько уже набрано по условию (для полоски «7 / 10»); у открытого достижения показываем не меньше порога
    public int Progress(AchievementDef d)
    {
        if (d == null) return 0;
        int v = Live(d);
        if (d.metric != AchMetric.Chapter && d.metric != AchMetric.PlaySeconds) v = Mathf.Max(v, best[(int)d.metric]);
        if (unlocked.Contains(d.id)) v = Mathf.Max(v, d.goal);
        return v;
    }

    int Live(AchievementDef d)
    {
        switch (d.metric)
        {
            case AchMetric.PlaySeconds: return Mathf.FloorToInt(playSeconds);
            case AchMetric.TreesChopped: return trees;
            case AchMetric.CoinsEarned: return coinsEarned;
            case AchMetric.PlanksCollected:
                if (workshop == null) workshop = FindFirstObjectByType<PlankWorkshop>();
                return workshop != null ? workshop.totalCollected : 0;
            case AchMetric.WorkersHired: return WorkerManager.Instance != null ? WorkerManager.Instance.Count : 0;
            case AchMetric.PlayerLevel: return PlayerStats.Level;
            case AchMetric.UpgradeLevels:
                {
                    var u = PlayerUpgrades.Instance;
                    if (u == null) return 0;
                    int sum = 0, n = GameBalance.UpgradeMaxLevel.Length;
                    for (int i = 0; i < n; i++) sum += u.Level((UpgradeType)i);
                    return sum;
                }
            case AchMetric.SkillsOwned: return PlayerSkills.OwnedCount;
            case AchMetric.PetsDiscovered: return PetManager.Instance != null ? PetManager.Instance.DiscoveredCount : 0;
            case AchMetric.Chapter: return chapters.Contains(d.chapterId) ? 1 : 0;
        }
        return 0;
    }

    // ------------------------------------------------------------ жизненный цикл
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        SaveSystem.Register(this);   // если файл уже прочитан, секция применится прямо здесь
    }

    void OnEnable()
    {
        GameEvents.TreeChopped += OnTree;
        GameEvents.QuestDone += OnQuestDone;
        PlayerInventory.Changed += OnInventory;
        SaveSystem.Loaded += OnSaveLoaded;
        SaveSystem.NewGameStarted += OnNewGame;
    }

    void OnDisable()
    {
        GameEvents.TreeChopped -= OnTree;
        GameEvents.QuestDone -= OnQuestDone;
        PlayerInventory.Changed -= OnInventory;
        SaveSystem.Loaded -= OnSaveLoaded;
        SaveSystem.NewGameStarted -= OnNewGame;
    }

    void OnDestroy()
    {
        if (Instance == this) { SaveSystem.Unregister(this); Instance = null; }
    }

    void Update()
    {
        // время в игре: только пока игра идёт (не пауза), окно в фокусе и мы уже на острове (есть игрок)
        if (Time.timeScale > 0.01f && Application.isFocused && PlayerStats.Instance != null)
            playSeconds += Time.unscaledDeltaTime;

        if (Time.unscaledTime < nextPoll) return;
        nextPoll = Time.unscaledTime + PollEvery;

        if (SaveSystem.IsLoading || PlayerStats.Instance == null) return;

        if (!startBackfillDone) { startBackfillDone = true; Backfill(); SyncCoins(); }

        CheckUnlocks(false);

        if (Time.unscaledTime >= nextSave) { nextSave = Time.unscaledTime + SaveEvery; SaveSystem.RequestSave("achievements"); }
    }

    // ------------------------------------------------------------ события игры
    void OnTree()
    {
        if (SaveSystem.IsLoading) return;
        trees++;
    }

    void OnQuestDone(string id)
    {
        if (SaveSystem.IsLoading || string.IsNullOrEmpty(id)) return;
        if (chapters.Add(id)) CheckUnlocks(false);
    }

    void OnInventory()
    {
        var inv = PlayerInventory.Instance;
        if (inv == null) return;
        int c = inv.coinsCount;
        if (lastCoins >= 0 && !SaveSystem.IsLoading && !suppressCoins && c > lastCoins) coinsEarned += c - lastCoins;
        lastCoins = c;
    }

    void SyncCoins() { var inv = PlayerInventory.Instance; if (inv != null) lastCoins = inv.coinsCount; }

    void OnSaveLoaded() { SyncCoins(); startBackfillDone = false; nextPoll = 0f; }

    void OnNewGame() { SyncCoins(); startBackfillDone = false; }

    // старый сейв (сделан до достижений) или игрок прошёл главы раньше: достраиваем «закрытые главы» по состоянию мира, без тостов
    void Backfill()
    {
        var sf = StoryFlow.Instance;
        if (sf == null) return;
        if (sf.hut != null && sf.hut.IsFinished) chapters.Add("hut");
        if (sf.market != null && sf.market.IsFinished) chapters.Add("market");
        if (sf.tent != null && sf.tent.IsFinished) chapters.Add("tent");
        if (sf.bridge != null && sf.bridge.IsFinished) chapters.Add("bridge");
        if (sf.AllDone) chapters.Add("gunnar");
        if (sf.ChapterIndex > 0 || PlayerStats.Level >= 2) chapters.Add("intro");
        CheckUnlocks(true);
    }

    // ------------------------------------------------------------ открытие
    void CheckUnlocks(bool silent)
    {
        var list = AchievementDefs.All;
        bool any = false;
        for (int i = 0; i < list.Count; i++)
        {
            var d = list[i];
            if (d.metric != AchMetric.Chapter && d.metric != AchMetric.PlaySeconds)
            {
                int live = Live(d);
                if (live > best[(int)d.metric]) best[(int)d.metric] = live;
            }
            if (unlocked.Contains(d.id)) continue;
            if (Progress(d) < d.goal) continue;
            unlocked.Add(d.id);
            any = true;
            if (!silent) Announce(d);
        }
        if (any)
        {
            SaveSystem.RequestSave("achievements");
            if (Changed != null) Changed();
        }
    }

    // показ: бронза и серебро выезжают плашкой справа, золото — на весь экран (см. AchievementToast). Звуки играет сам тост
    static void Announce(AchievementDef d)
    {
        AchievementToast.Show(d);
        if (Unlocked != null) Unlocked(d);
    }

    // ------------------------------------------------------------ награды
    public bool Claim(string id)
    {
        if (!CanClaim(id)) return false;
        var d = AchievementDefs.Find(id);
        if (d == null) return false;
        Grant(d);
        claimed.Add(id);
        SaveSystem.RequestSave("achievements");
        if (Changed != null) Changed();
        return true;
    }

    public int ClaimAll()
    {
        int n = 0;
        var list = AchievementDefs.All;
        for (int i = 0; i < list.Count; i++)
        {
            var d = list[i];
            if (!CanClaim(d.id)) continue;
            Grant(d);
            claimed.Add(d.id);
            n++;
        }
        if (n > 0)
        {
            SaveSystem.RequestSave("achievements");
            if (Changed != null) Changed();
        }
        return n;
    }

    void Grant(AchievementDef d)
    {
        var inv = PlayerInventory.Instance;
        if (inv != null && d.coins > 0)
        {
            suppressCoins = true;
            inv.AddCoins(d.coins);      // сработает OnInventory: сумма не пойдёт в «заработано монет»
            suppressCoins = false;
            SyncCoins();
        }
        if (PlayerStats.Instance != null && d.xp > 0) PlayerStats.Instance.AddXP(d.xp);
    }

    // ------------------------------------------------------------ сохранение (секция «achievements»)
    public string SaveKey { get { return "achievements"; } }
    public int LoadOrder { get { return 95; } }

    [Serializable]
    class SaveData
    {
        public int version = 1;
        public float playSeconds;
        public int trees, coinsEarned;
        public List<string> chapters = new List<string>();
        public List<string> unlocked = new List<string>();
        public List<string> claimed = new List<string>();
        public int[] best = new int[0];
    }

    public string Save()
    {
        var d = new SaveData { playSeconds = playSeconds, trees = trees, coinsEarned = coinsEarned };
        d.chapters.AddRange(chapters);
        d.unlocked.AddRange(unlocked);
        d.claimed.AddRange(claimed);
        d.best = (int[])best.Clone();
        return JsonUtility.ToJson(d);
    }

    // без наград, событий, тостов и RequestSave (правило ISaveable)
    public void Load(string json, int formatVersion)
    {
        ResetToDefault();
        if (string.IsNullOrEmpty(json)) return;
        SaveData d = null;
        try { d = JsonUtility.FromJson<SaveData>(json); }
        catch (Exception e) { Debug.LogWarning("AchievementManager: не удалось прочитать сохранение: " + e.Message); }
        if (d == null) return;

        playSeconds = Mathf.Max(0f, d.playSeconds);
        trees = Mathf.Max(0, d.trees);
        coinsEarned = Mathf.Max(0, d.coinsEarned);
        if (d.chapters != null) foreach (string s in d.chapters) if (!string.IsNullOrEmpty(s)) chapters.Add(s);
        // неизвестные id (достижение убрали из таблицы) тихо отбрасываем
        if (d.unlocked != null) foreach (string s in d.unlocked) if (AchievementDefs.Find(s) != null) unlocked.Add(s);
        if (d.claimed != null) foreach (string s in d.claimed) if (unlocked.Contains(s)) claimed.Add(s);
        if (d.best != null) for (int i = 0; i < best.Length && i < d.best.Length; i++) best[i] = Mathf.Max(0, d.best[i]);
    }

    public void ResetToDefault()
    {
        playSeconds = 0f; trees = 0; coinsEarned = 0;
        chapters.Clear(); unlocked.Clear(); claimed.Clear();
        for (int i = 0; i < best.Length; i++) best[i] = 0;
    }
}
