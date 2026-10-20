using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("Текстовые поля ресурсов")]
    public TMP_Text woodText;
    public TMP_Text coinText;

    [Header("Панель уровня")]
    public TMP_Text levelText;
    public Image xpFillBar;

    [Header("Текущее задание (Онбординг)")]
    public TMP_Text objectiveText;

    [Header("Анимация счётчиков")]
    public float fillSpeed = 6f;        // скорость наполнения полосы опыта
    public float pulseAmount = 1.25f;   // во сколько раз «подпрыгивает» число при изменении
    public float pulseTime = 0.25f;     // длительность подпрыгивания, с

    [Header("«+N» под счётчиками")]
    [Tooltip("true = плашки «+2 бревна / +2 XP / +8» рисует HudGain (выезжают из-под панели, складываются). false = старые надписи ниже")]
    public bool useHudGain = true;
    public Color gainWoodColor = Color.white;
    public Color gainXpColor = new Color32(0x6B, 0xCB, 0x5A, 255);
    public Color gainCoinColor = new Color32(0xF5, 0xC5, 0x42, 255);
    public Color gainOutline = new Color32(0x14, 0x1C, 0x30, 255);
    public float gainSize = 32f;        // размер текста (старый режим)
    public float gainLife = 1.1f;       // сколько живёт, с (старый режим)
    public float gainDrop = 40f;        // на сколько px уезжает вниз (старый режим)

    // Маленький помощник: «подпрыгивание» масштаба элемента
    class Pulse
    {
        readonly Transform t;
        readonly Vector3 baseScale;
        float p = 1f;

        public Pulse(Transform t)
        {
            this.t = t;
            if (t != null) baseScale = t.localScale;
        }

        public void Kick() { p = 0f; }

        public void Tick(float dt, float duration, float amount)
        {
            if (t == null || p >= 1f) return;
            p = Mathf.Min(1f, p + dt / Mathf.Max(0.01f, duration));
            float k = 1f + (amount - 1f) * Mathf.Sin(p * Mathf.PI);
            t.localScale = p >= 1f ? baseScale : baseScale * k;
        }
    }

    // «+2» под панелью счётчика (старый режим)
    class Gain
    {
        public RectTransform rt;
        public TextMeshProUGUI txt;
        public Color color;
        public float age;
    }

    Pulse woodPulse, coinPulse, levelPulse, objectivePulse;
    int lastWood = int.MinValue, lastCoins = int.MinValue, lastLevel = int.MinValue, lastPlanks;
    float targetFill;
    string lastObjective;

    readonly List<Gain> gains = new List<Gain>();
    readonly Stack<Gain> gainPool = new Stack<Gain>();

    void Awake()
    {
        // Защита: оставляем только один UIManager (удаляем только лишний компонент, а не весь объект)
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        woodPulse = new Pulse(woodText != null ? woodText.transform : null);
        coinPulse = new Pulse(coinText != null ? coinText.transform : null);
        levelPulse = new Pulse(levelText != null ? levelText.transform : null);
        objectivePulse = new Pulse(objectiveText != null ? objectiveText.transform : null);

        MarkGainAnchors();
    }

    // Панели HUD помечаются сами: панель = родитель текста. Руками ничего вешать не нужно.
    void MarkGainAnchors()
    {
        MarkAnchor(woodText, HudGain.Kind.Wood);
        MarkAnchor(levelText, HudGain.Kind.Xp);
        MarkAnchor(coinText, HudGain.Kind.Coin);
    }

    static void MarkAnchor(TMP_Text label, HudGain.Kind kind)
    {
        if (label == null) return;
        Transform panel = label.transform.parent != null ? label.transform.parent : label.transform;
        var a = panel.GetComponent<HudGainAnchor>();
        if (a == null) a = panel.gameObject.AddComponent<HudGainAnchor>();
        a.kind = kind;
        if (a.flyTarget == null)
        {
            // иконка в круглом бейдже (BuilderSimHud: Badge/Icon): сюда долетают бревна
            Transform icon = panel.Find("Badge/Icon");
            if (icon != null) a.flyTarget = icon as RectTransform;
        }
    }

    void Start()
    {
        // счётчик досок: копия панели «дерево» (HudPlanks), строится сам
        HudPlanks.Create(woodText);
        if (HudPlanks.Instance != null) HudPlanks.Instance.Set(lastPlanks);
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        if (xpFillBar != null)
        {
            // при новом уровне полоса не едет назад, а сразу начинается заново
            if (targetFill < xpFillBar.fillAmount - 0.5f) xpFillBar.fillAmount = 0f;
            xpFillBar.fillAmount = Mathf.Lerp(xpFillBar.fillAmount, targetFill, 1f - Mathf.Exp(-fillSpeed * dt));
        }

        woodPulse?.Tick(dt, pulseTime, pulseAmount);
        coinPulse?.Tick(dt, pulseTime, pulseAmount);
        levelPulse?.Tick(dt, pulseTime * 2f, pulseAmount * 1.3f);
        objectivePulse?.Tick(dt, pulseTime * 1.5f, 1.06f);

        TickGains(dt);
    }

    // Компактные значения HUD: маленькие суммы остаются точными, крупные не ломают панель.
    // 700 -> 700, 12 500 -> 12.5K, 1 250 000 -> 1.25M.
    static string FormatResourceCount(int value)
    {
        if (value < 0) return "−" + FormatResourceCount(-value);
        if (value < 10000) return value.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("ru-RU"));
        if (value < 1000000) return FormatCompact(value / 1000d, "K");
        if (value < 1000000000) return FormatCompact(value / 1000000d, "M");
        return FormatCompact(value / 1000000000d, "B");
    }

    static string FormatGainCount(int value)
    {
        // Приросты до 999 показываем точно: игрок видит и +1, и +40, и +700.
        if (value < 1000) return value.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("ru-RU"));
        return FormatResourceCount(value);
    }

    static string FormatCompact(double value, string suffix)
    {
        string format = value >= 100 ? "0" : value >= 10 ? "0.#" : "0.##";
        return value.ToString(format, System.Globalization.CultureInfo.GetCultureInfo("ru-RU")) + suffix;
    }

    public void UpdateResources(int wood, int coins)
    {
        if (woodText != null && wood != lastWood)
        {
            woodText.text = FormatResourceCount(wood);
            if (lastWood != int.MinValue)
            {
                woodPulse?.Kick();
                if (wood > lastWood) ShowGain(HudGain.Kind.Wood, woodText, wood - lastWood, "+" + FormatGainCount(wood - lastWood), gainWoodColor);
            }
            lastWood = wood;
        }

        if (coinText != null && coins != lastCoins)
        {
            coinText.text = FormatResourceCount(coins);
            if (lastCoins != int.MinValue)
            {
                coinPulse?.Kick();
                if (coins > lastCoins) ShowGain(HudGain.Kind.Coin, coinText, coins - lastCoins, "+" + FormatGainCount(coins - lastCoins), gainCoinColor);
            }
            lastCoins = coins;
        }
    }

    // доски (вызывает PlayerInventory)
    public void UpdatePlanks(int planks)
    {
        lastPlanks = planks;
        if (HudPlanks.Instance != null) HudPlanks.Instance.Set(planks);
    }

    // currentXP и nextLevelXP — опыт ВНУТРИ уровня (см. PlayerStats.RefreshUI)
    public void UpdateXP(int level, int currentXP, int nextLevelXP)
    {
        if (levelText != null)
        {
            levelText.text = "LVL " + level;
            if (lastLevel != int.MinValue && level != lastLevel) levelPulse?.Kick();
        }
        lastLevel = level;

        targetFill = nextLevelXP > 0 ? Mathf.Clamp01((float)currentXP / nextLevelXP) : 0f;
    }

    // «+2 XP» под панелью уровня (вызывает PlayerStats)
    public void ShowXpGain(int amount)
    {
        if (amount > 0) ShowGain(HudGain.Kind.Xp, levelText, amount, "+" + amount + " XP", gainXpColor);
    }

    // Запасной путь для простых целей («Поговори с Мирой»): одна строка TMP. Если до этого показывался виджет, он прячется.
    public void UpdateObjective(string text)
    {
        if (objectiveText == null) return;
        var view = ObjectiveView.Instance;
        bool widgetWasShown = view != null && view.IsShown;
        if (widgetWasShown) { view.Hide(); lastObjective = null; }   // текст мог совпасть с прежним: показать заново обязательно
        if (text == lastObjective) return;
        objectiveText.text = text;
        lastObjective = text;
        objectivePulse?.Kick();
    }

    // Цель-виджет: заголовок + требования с иконками, счётчиками и прогрессбаром (см. ObjectiveView).
    // Без требований (или без панели задания) уходит в запасной путь UpdateObjective(title).
    public void SetObjective(ObjectiveData data)
    {
        if (data == null || !data.HasReqs || objectiveText == null)
        {
            UpdateObjective(data != null ? data.title : "");
            return;
        }

        var view = ObjectiveView.Ensure(this);
        if (view == null) { UpdateObjective(data.ToPlain()); return; }

        lastObjective = null;
        view.Show(data);
    }

    // Единая точка: новая плашка HudGain, а если панель не размечена — старая надпись
    void ShowGain(HudGain.Kind kind, TMP_Text anchor, int amount, string oldText, Color oldColor)
    {
        if (useHudGain && HudGainAnchor.Find(kind) != null) HudGain.Show(kind, amount);
        else SpawnGain(anchor, oldText, oldColor);
    }

    // ---------------- «+N» под счётчиками (старый режим) ----------------
    void SpawnGain(TMP_Text anchor, string text, Color color)
    {
        if (anchor == null) return;

        // Привязываем к панели счётчика: надпись выезжает из-под неё вниз
        Transform panel = anchor.transform.parent != null ? anchor.transform.parent : anchor.transform;

        Gain g = gainPool.Count > 0 ? gainPool.Pop() : CreateGain();
        g.rt.SetParent(panel, false);
        g.rt.gameObject.SetActive(true);
        g.rt.anchorMin = g.rt.anchorMax = new Vector2(0.5f, 0f);
        g.rt.pivot = new Vector2(0.5f, 1f);
        g.rt.sizeDelta = new Vector2(260f, 52f);
        g.rt.anchoredPosition = new Vector2(0f, -6f);
        g.rt.localScale = Vector3.zero;

        g.txt.font = anchor.font;
        g.txt.fontSize = gainSize;
        g.txt.text = text;
        g.color = color;
        g.txt.color = new Color(color.r, color.g, color.b, 0f);
        g.age = 0f;
        gains.Add(g);
    }

    Gain CreateGain()
    {
        var go = new GameObject("Gain", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        go.layer = 5;
        go.GetComponent<LayoutElement>().ignoreLayout = true; // не ломает layout-группы панели

        var t = go.GetComponent<TextMeshProUGUI>();
        t.alignment = TextAlignmentOptions.Center;
        t.fontStyle = FontStyles.Bold;
        t.raycastTarget = false;
        t.outlineWidth = 0.22f;
        t.outlineColor = (Color32)gainOutline;

        return new Gain { rt = (RectTransform)go.transform, txt = t };
    }

    void TickGains(float dt)
    {
        for (int i = gains.Count - 1; i >= 0; i--)
        {
            Gain g = gains[i];
            if (g.rt == null) { gains.RemoveAt(i); continue; } // панель удалили

            g.age += dt;
            float t = g.age / Mathf.Max(0.05f, gainLife);

            if (t >= 1f)
            {
                g.rt.gameObject.SetActive(false);
                gains.RemoveAt(i);
                gainPool.Push(g);
                continue;
            }

            float pop = t < 0.15f ? EaseOutBack(t / 0.15f) : 1f;
            float alpha = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;

            g.rt.localScale = Vector3.one * pop;
            g.rt.anchoredPosition = new Vector2(0f, -6f - gainDrop * EaseOutCubic(t));
            g.txt.color = new Color(g.color.r, g.color.g, g.color.b, alpha);
        }
    }

    static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);

    static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float u = x - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }
}