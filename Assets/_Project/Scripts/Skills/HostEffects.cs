// Assets/_Project/Scripts/Skills/HostEffects.cs (НЕ в Editor)
// Живая часть ветки «Хозяин»: «Базарный день» (boss_tongue). Остальные узлы ветки считаются на месте и читают PlayerSkills.Has:
//   boss_hoard   PlankWorkshop.QueueCap / OutputCap;     boss_sawyer  PlankWorkshop.TryOrder;
//   boss_chief   WorkerManager.IncomeOf;                  boss_bulk    Pricing.SaleMultiplier.
// Создаётся сам (BeforeSceneLoad), живёт всю игру, вешать на сцену ничего не нужно.
//
// Базарный день: когда таймер дошёл до нуля, следующая продажа у торговца даёт ×GameBalance.SkillBazaarMul.
// После продажи таймер идёт заново GameBalance.SkillBazaarCooldown (5 минут). Бонус применяет Pricing, расходует SellWindow.
// Готовность видна над прилавком (табличка), а в окне продажи в строке «Выбрано» стоит пометка. Когда день наступил, один раз всплывает тост.
//
// Сохранение: секция «host» (LoadOrder 36), хранит ОСТАВШИЕСЯ секунды таймера. Поэтому «Продолжить» не даёт нового Базарного дня:
// перезапуск игры таймер не обнуляет. Таймер тикает и без навыка (сброс и повторная покупка навыка перезарядку не обходят).
// Наград здесь нет, при Load ничего не выдаётся.
using System;
using UnityEngine;

public sealed class HostEffects : MonoBehaviour, ISaveable
{
    static HostEffects inst;

    float bazaarLeft;      // секунд до готовности, 0 = готов (сохраняется)
    bool announced;        // «Базарный день наступил» уже показан (не сохраняется)

    TradingStall stall;
    float findStallAt;
    WorldTag tag;
    string tagShown;
    float tagAt;

    // ------------------------------------------------------------ чтение для Pricing, SellWindow
    public static bool BazaarReady
    {
        get { return inst != null && PlayerSkills.Has(SkillIds.BossTongue) && inst.bazaarLeft <= 0f; }
    }

    public static float BazaarSecondsLeft { get { return inst != null ? inst.bazaarLeft : 0f; } }

    // продажа состоялась с бонусом: перезарядка
    public static void ConsumeBazaar()
    {
        if (inst == null) return;
        inst.bazaarLeft = GameBalance.SkillBazaarCooldown;
        inst.announced = false;
        SaveSystem.RequestSave("host");
    }

    // ------------------------------------------------------------ жизненный цикл
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { inst = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        if (inst != null) return;
        var go = new GameObject("HostEffects");
        DontDestroyOnLoad(go);
        inst = go.AddComponent<HostEffects>();
    }

    void Awake()
    {
        if (inst != null && inst != this) { Destroy(gameObject); return; }
        inst = this;
        SaveSystem.Register(this);   // если файл уже прочитан, секция «host» применится прямо здесь (Load)
    }

    void OnEnable()
    {
        // сначала отписка: при выключенном Domain Reload подписки не должны задваиваться
        PlayerSkills.Changed -= OnSkillsChanged; PlayerSkills.Changed += OnSkillsChanged;
        SaveSystem.Loaded -= OnLoaded; SaveSystem.Loaded += OnLoaded;
    }

    void OnDisable()
    {
        PlayerSkills.Changed -= OnSkillsChanged;
        SaveSystem.Loaded -= OnLoaded;
    }

    void OnDestroy()
    {
        SaveSystem.Unregister(this);
        if (inst == this) inst = null;
    }

    // навык сбросили: табличка и «уже объявлено» гаснут. Купили, пока день готов: тост покажет Update
    void OnSkillsChanged()
    {
        if (!PlayerSkills.Has(SkillIds.BossTongue)) announced = false;
    }

    // после Load тост «день наступил» не нужен (табличка и так видна)
    void OnLoaded() { announced = bazaarLeft <= 0f; }

    // ------------------------------------------------------------ сохранение (ISaveable, секция «host», LoadOrder 36)
    // ДОБАВЛЯТЬ ПОЛЯ ТОЛЬКО С ДЕФОЛТАМИ: старые файлы без них читаются нормально
    [Serializable]
    class SaveData
    {
        public int version = 1;
        public float bazaarLeft;
    }

    public string SaveKey { get { return "host"; } }
    public int LoadOrder { get { return 36; } }

    public string Save() { return JsonUtility.ToJson(new SaveData { bazaarLeft = bazaarLeft }); }

    // Без событий, тостов и RequestSave (правило ISaveable)
    public void Load(string json, int formatVersion)
    {
        SaveData d = null;
        if (!string.IsNullOrEmpty(json))
        {
            try { d = JsonUtility.FromJson<SaveData>(json); }
            catch (Exception e) { Debug.LogWarning("HostEffects: не удалось прочитать сохранение: " + e.Message); }
        }
        if (d == null) { ResetToDefault(); return; }
        bazaarLeft = Mathf.Clamp(d.bazaarLeft, 0f, GameBalance.SkillBazaarCooldown);
        announced = bazaarLeft <= 0f;
    }

    public void ResetToDefault()
    {
        bazaarLeft = 0f;
        announced = false;
    }

    // ------------------------------------------------------------ каждый кадр
    void Update()
    {
        if (SaveSystem.IsLoading) return;

        // в паузе (timeScale 0) deltaTime равен нулю, таймер стоит
        if (bazaarLeft > 0f) bazaarLeft = Mathf.Max(0f, bazaarLeft - Time.deltaTime);

        bool owned = PlayerSkills.Has(SkillIds.BossTongue);
        if (owned && bazaarLeft <= 0f && !announced)
        {
            announced = true;
            SaveIndicator.ShowToast("Базарный день! Следующая продажа ×" + GameBalance.SkillBazaarMul.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
            SfxManager.Play("hint", 0.9f, 1.15f);
        }

        UpdateTag(owned);
    }

    // табличка над прилавком: готов Базарный день (видно издалека) или сколько ждать (только вблизи)
    void UpdateTag(bool owned)
    {
        if (!owned) { Hide(); return; }

        if (stall == null)
        {
            if (Time.unscaledTime < findStallAt) { Hide(); return; }
            findStallAt = Time.unscaledTime + 1f;
            stall = FindFirstObjectByType<TradingStall>();
            if (stall == null) { Hide(); return; }
        }

        bool ready = bazaarLeft <= 0f;
        float dist = stall.DistanceToPlayer();
        bool want = stall.Unlocked && !SellWindow.IsOpenFor(stall) && dist <= (ready ? 20f : 8f);
        if (!want) { Hide(); return; }

        if (tag == null)
        {
            var go = new GameObject("BazaarStatus");
            go.transform.SetParent(stall.transform, false);
            tag = WorldTag.Add(go, false);
            tag.offset = new Vector3(0f, 4.0f, 0f);
            tag.SetIcon(UiKit.Icon(UiIcon.Coin));
            tag.SetFontSize(30f);
        }
        tag.visible = true;

        if (Time.unscaledTime < tagAt) return;
        tagAt = Time.unscaledTime + 0.25f;

        string s = ready
            ? "Базарный день: ×" + GameBalance.SkillBazaarMul.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)
            : "Базарный день через " + PlankWorkshop.FormatTime(bazaarLeft);
        if (s != tagShown) { tagShown = s; tag.SetText(s); }
    }

    void Hide() { if (tag != null) tag.visible = false; }

    // ------------------------------------------------------------ тест из Inspector (ПКМ по компоненту в Play Mode)
    [ContextMenu("Тест: Базарный день готов прямо сейчас")]
    void DebugReady() { bazaarLeft = 0f; announced = false; }

    [ContextMenu("Тест: Базарный день осталось 5 секунд")]
    void DebugAlmost() { bazaarLeft = 5f; announced = false; }
}
