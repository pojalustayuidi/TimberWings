// Assets/_Project/Scripts/Workshop/PlankWorkshop.cs (НЕ в Editor)
// Пилорама в лесорубской хижине: бревна -> доски. Игрок открывает окно (E у хижины, PlankWorkshopWindow), выбирает,
// сколько досок сделать, и жмёт «Начать». Бревна списываются из рюкзака сразу, доски делаются В ФОНЕ (окно можно закрыть,
// уйти рубить дальше), готовые доски забираются кнопкой в окне.
//   рецепт:   GameBalance.PlankRecipe бревна -> 1 доска (потолок дизайна GameBalance.MaxLogsPerPlank = 6)
//   время:    GameBalance.PlankTime на доску, с прокачкой «Скорость пилы» (PlayerUpgrades.SawTime)
//   очередь:  не больше QueueCap досок в работе; склад готовых: OutputCap (GameBalance + «Запасливый» boss_hoard)
//   «Мастер-пильщик» (boss_sawyer): каждая доска заказа с шансом SkillSawyerChance не тратит бревна (они возвращаются в рюкзак)
// Вешается на корень хижины (меню Tools > BuilderSim > Настроить цепочку StoryFlow делает это само).
// Пока хижина не достроена, пила закрыта (StoryFlow включает её после главы про хижину).
// Для квестов: unlocked, totalCollected (сколько досок забрано за всю игру).
using System;
using UnityEngine;

public class PlankWorkshop : MonoBehaviour, IInteractable, IPriorityInteractable, ISaveable
{
    [Header("Доступность")]
    [Tooltip("StoryFlow включает пилу, когда хижина достроена. Для теста можно включить руками")]
    public bool unlocked = false;
    public float radius = 3.5f;
    public Vector3 promptOffset = new Vector3(0f, 3f, 0f);

    [Header("Табличка над пилой (пока идёт работа или ждут готовые доски)")]
    public Vector3 statusOffset = new Vector3(0f, 4.2f, 0f);
    public float statusShowDistance = 20f;

    [Header("Только для просмотра")]
    public int queuedPlanks;     // доски в работе (бревна за них уже списаны)
    public int readyPlanks;      // готовые доски, ждут игрока
    public int totalCollected;   // сколько досок забрано за всю игру (для квестов)
    [Range(0f, 1f)] public float progress;   // прогресс текущей доски

    public Color plankColor = new Color32(0xE8, 0xC1, 0x7A, 255);

    // окно и квесты подписываются, чтобы не опрашивать каждый кадр
    public event Action Changed;

    PlayerInventory inv;
    PlayerUpgrades up;
    PlayerStats stats;
    float findAt;

    WorldTag statusTag;
    GameObject statusGo;
    string statusShown;
    float statusAt;

    // ------------------------------------------------------------ данные для окна
    public int LogsPerPlank { get { return GameBalance.PlankRecipe; } }

    public float SecondsPerPlank
    {
        get
        {
            var u = up != null ? up : PlayerUpgrades.Instance;
            return u != null ? u.SawTime : GameBalance.PlankTime;
        }
    }

    public int LogsInBackpack { get { return FindPlayer() ? inv.woodCount : 0; } }

    // вместимость очереди и склада: базовая + «Запасливый» (boss_hoard)
    public static int QueueCap { get { return GameBalance.PlankQueueCap + (PlayerSkills.Has(SkillIds.BossHoard) ? GameBalance.SkillHoardQueue : 0); } }
    public static int OutputCap { get { return GameBalance.PlankOutputCap + (PlayerSkills.Has(SkillIds.BossHoard) ? GameBalance.SkillHoardOutput : 0); } }

    // самый большой возможный предел (с навыком): по нему режем сохранение, чтобы не потерять доски, пока навык ещё не сверен с уровнем
    static int QueueCapMax { get { return GameBalance.PlankQueueCap + GameBalance.SkillHoardQueue; } }
    static int OutputCapMax { get { return GameBalance.PlankOutputCap + GameBalance.SkillHoardOutput; } }

    // сколько досок ещё можно поставить в работу: очередь и склад готовых не должны переполниться.
    // После сброса «Запасливого» очередь и склад могут стоять выше предела: ничего не пропадает, новые заказы ждут, пока место освободится
    public int FreeSlots
    {
        get
        {
            int byQueue = QueueCap - queuedPlanks;
            int byOutput = OutputCap - queuedPlanks - readyPlanks;
            return Mathf.Max(0, Mathf.Min(byQueue, byOutput));
        }
    }

    // сколько досок можно заказать прямо сейчас (бревна в рюкзаке и свободные места)
    public int MaxOrder { get { return Mathf.Max(0, Mathf.Min(FreeSlots, LogsInBackpack / LogsPerPlank)); } }

    public float SecondsLeft { get { return queuedPlanks <= 0 ? 0f : (queuedPlanks - progress) * SecondsPerPlank; } }

    // почему нельзя начать (null = можно)
    public string BlockReason
    {
        get
        {
            if (!unlocked) return "Пила пока закрыта";
            if (FreeSlots <= 0)
                return queuedPlanks >= QueueCap ? "Очередь полна: подожди, пока пила освободится" : "Склад досок полон: забери готовые";
            int logs = LogsInBackpack;
            if (logs < LogsPerPlank)
            {
                int need = LogsPerPlank - logs;
                return "Не хватает бревен: нужно ещё " + need + ". На 1 доску идёт " + LogsPerPlank;
            }
            return null;
        }
    }

    public float DistanceToPlayer()
    {
        if (!FindPlayer()) return float.MaxValue;
        Vector3 d = inv.transform.position - transform.position;
        d.y = 0f;
        return d.magnitude;
    }

    public static string FormatTime(float seconds)
    {
        int t = Mathf.Max(0, Mathf.CeilToInt(seconds));
        return t >= 60 ? (t / 60) + ":" + (t % 60).ToString("00") : t + " с";
    }

    // ------------------------------------------------------------ действия (их вызывает окно)
    public bool TryOrder(int planks)
    {
        if (!unlocked || !FindPlayer()) return false;
        planks = Mathf.Min(planks, MaxOrder);
        if (planks <= 0) return false;

        if (!inv.RemoveWood(planks * LogsPerPlank)) return false;   // не хватило брёвен: доски не выдаём

        // «Мастер-пильщик»: часть досок без трат. Сначала списываем всё, потом возвращаем бревна за счастливые доски:
        // так не нужно ничего предсказывать заранее, а рюкзак после возврата не больше, чем был до заказа
        int free = RollSawyer(planks);
        if (free > 0)
        {
            inv.AddWood(free * LogsPerPlank);
            var popups = RewardPopups.Instance;
            if (popups != null)
                popups.ShowText(PromptPoint, "Мастер-пильщик: " + free + " " + UiDraw.Plural(free, "доска", "доски", "досок") + " без бревен", plankColor, 40f);
        }

        queuedPlanks += planks;
        SaveSystem.RequestSave("workshop");
        RaiseChanged();
        return true;
    }

    // сколько из заказанных досок повезло (без навыка всегда 0)
    static int RollSawyer(int planks)
    {
        if (!PlayerSkills.Has(SkillIds.BossSawyer)) return 0;
        int n = 0;
        for (int i = 0; i < planks; i++)
            if (UnityEngine.Random.value < GameBalance.SkillSawyerChance) n++;
        return n;
    }

    public int Collect()
    {
        if (!unlocked || !FindPlayer() || readyPlanks <= 0) return 0;

        int n = readyPlanks;
        readyPlanks = 0;
        totalCollected += n;
        inv.AddPlanks(n);
        SaveSystem.RequestSave("workshop");
        if (stats != null) stats.AddXP(n * GameBalance.XpPerPlank);

        var popups = RewardPopups.Instance;
        if (popups != null)
            popups.ShowText(PromptPoint, "+" + n + " " + UiDraw.Plural(n, "доска", "доски", "досок"), plankColor, 48f);

        RaiseChanged();
        return n;
    }

    void RaiseChanged()
    {
        if (Changed != null) Changed();
    }

    // ------------------------------------------------------------ IInteractable
    public Transform Anchor { get { return transform; } }
    public Vector3 PromptPoint { get { return transform.position + promptOffset; } }
    public float Radius { get { return radius; } }
    public bool Priority { get { return true; } }
    public string Prompt { get { return "открыть пилораму"; } }
    public bool CanInteract { get { return unlocked && FindPlayer(); } }

    public void Interact()
    {
        if (!CanInteract) return;
        PlankWorkshopWindow.Toggle(this);   // E: открыть окно, а если оно открыто, закрыть
    }

    // ------------------------------------------------------------ сохранение (ISaveable, секция «workshop», LoadOrder 55)
    // Бревна за доски в очереди уже списаны из рюкзака, поэтому очередь и готовые доски обязаны жить в файле.
    // «unlocked» НЕ пишем: его ведёт StoryFlow (секция story)
    [Serializable]
    class SaveData
    {
        public int version = 1;
        public int queued;
        public int ready;
        public int collected;
        public float progress;
    }

    public string SaveKey { get { return "workshop"; } }
    public int LoadOrder { get { return 55; } }

    public string Save()
    {
        return JsonUtility.ToJson(new SaveData { queued = queuedPlanks, ready = readyPlanks, collected = totalCollected, progress = progress });
    }

    // Без наград, XP, всплывашек и RequestSave (правило ISaveable). Окно уведомит OnSaveLoaded
    public void Load(string json, int formatVersion)
    {
        SaveData d = null;
        if (!string.IsNullOrEmpty(json))
        {
            try { d = JsonUtility.FromJson<SaveData>(json); }
            catch (Exception e) { Debug.LogWarning("PlankWorkshop: не удалось прочитать сохранение пилы: " + e.Message); }
        }
        if (d == null) { ResetToDefault(); return; }

        queuedPlanks = Mathf.Clamp(d.queued, 0, QueueCapMax);     // по максимуму с навыком: доски за уже списанные бревна не теряем
        readyPlanks = Mathf.Clamp(d.ready, 0, OutputCapMax);
        totalCollected = Mathf.Max(0, d.collected);
        progress = queuedPlanks > 0 ? Mathf.Clamp01(d.progress) : 0f;
    }

    public void ResetToDefault()
    {
        queuedPlanks = 0;
        readyPlanks = 0;
        totalCollected = 0;
        progress = 0f;
    }

    void OnSaveLoaded() { RaiseChanged(); }

    // ------------------------------------------------------------ жизненный цикл
    void Awake() { SaveSystem.Register(this); }   // если файл уже прочитан, секция «workshop» применится прямо здесь (Load)
    void OnDestroy() { SaveSystem.Unregister(this); }

    void OnEnable()
    {
        Interactables.Register(this); SaveSystem.Loaded += OnSaveLoaded;
        PlayerSkills.Changed -= RaiseChanged; PlayerSkills.Changed += RaiseChanged;   // пределы очереди и склада поменялись: окно обновится
    }

    void OnDisable()
    {
        PlayerSkills.Changed -= RaiseChanged;
        SaveSystem.Loaded -= OnSaveLoaded;
        Interactables.Unregister(this);
        if (statusTag != null) statusTag.visible = false;
    }

    bool FindPlayer()
    {
        if (inv != null) return true;
        if (Time.unscaledTime < findAt) return false;
        findAt = Time.unscaledTime + 0.5f;
        var pm = PlayerMovement.Instance;
        if (pm == null) return false;
        inv = pm.GetComponent<PlayerInventory>();
        up = pm.GetComponent<PlayerUpgrades>();
        stats = pm.GetComponent<PlayerStats>();
        return inv != null;
    }

    void Update()
    {
        if (!unlocked) { if (statusTag != null) statusTag.visible = false; return; }
        FindPlayer();

        // доски делаются всегда: окно закрыто, игрок рубит или ушёл далеко
        if (queuedPlanks > 0)
        {
            progress += Time.deltaTime / Mathf.Max(0.1f, SecondsPerPlank);
            bool done = false;
            while (progress >= 1f && queuedPlanks > 0)
            {
                progress -= 1f;
                queuedPlanks--;
                readyPlanks++;
                done = true;
            }
            if (queuedPlanks == 0) progress = 0f;

            if (done)
            {
                SaveSystem.RequestSave("workshop");
                RaiseChanged();
                if (queuedPlanks == 0) AnnounceBatchDone();
            }
        }
        else progress = 0f;

        UpdateStatus();
    }

    // очередь закончилась: короткое «Доски готовы» над пилой (если игрок рядом, увидит)
    void AnnounceBatchDone()
    {
        if (PlankWorkshopWindow.IsOpenFor(this)) return;
        var popups = RewardPopups.Instance;
        if (popups != null && readyPlanks > 0)
            popups.ShowText(PromptPoint, "Доски готовы: " + readyPlanks, plankColor, 44f);
    }

    // ------------------------------------------------------------ табличка над пилой
    void UpdateStatus()
    {
        bool want = (queuedPlanks > 0 || readyPlanks > 0)
                    && !PlankWorkshopWindow.IsOpenFor(this)
                    && DistanceToPlayer() <= statusShowDistance;

        if (!want)
        {
            if (statusTag != null) statusTag.visible = false;
            return;
        }

        if (statusTag == null)
        {
            statusGo = new GameObject("SawStatus");
            statusGo.transform.SetParent(transform, false);
            statusTag = WorldTag.Add(statusGo, false);
            statusTag.offset = statusOffset;
            statusTag.SetIcon(UiKit.Icon(UiIcon.Plank));
            statusTag.SetFontSize(30f);
        }
        statusTag.visible = true;

        if (Time.unscaledTime < statusAt) return;
        statusAt = Time.unscaledTime + 0.25f;

        string s;
        if (queuedPlanks > 0)
        {
            s = "Пила: " + queuedPlanks + " в работе, " + FormatTime(SecondsLeft);
            if (readyPlanks > 0) s += " · готово " + readyPlanks;
        }
        else s = "Готово: " + readyPlanks + " " + UiDraw.Plural(readyPlanks, "доска", "доски", "досок");

        if (s != statusShown)
        {
            statusShown = s;
            statusTag.SetText(s);
        }
    }

    // ------------------------------------------------------------ проверка в Play (ПКМ по компоненту)
    [ContextMenu("Тест: открыть пилу и положить 10 бревен в рюкзак")]
    void DebugGive()
    {
        unlocked = true;
        if (FindPlayer()) inv.AddWood(10);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.91f, 0.76f, 0.48f, 0.9f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.05f, radius);
    }
}
