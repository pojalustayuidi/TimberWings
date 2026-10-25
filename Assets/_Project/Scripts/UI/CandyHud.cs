// Assets/_Project/Scripts/UI/Candy/CandyHud.cs   (НЕ в Editor)
// Новый мультяшный HUD «Candy» (ресурсы, задание, уровень). Панели собирает Editor-меню
// Tools > BuilderSim > UI > Candy Kit > Собрать HUD (ресурсы, задание, уровень), в сцене они лежат как обычные объекты.
//
// Здесь два компонента:
//   CandyHud       маркер и хранилище: словарь спрайтов-иконок, ссылки на старые элементы (чтобы вернуть старый HUD из меню).
//                  Пока он включён, CandyHud.Active = true: HudKitSkin не перекрашивает панели уровня и задания под старый стиль.
//   CandyTaskView  панель задания: табличка «ЗАДАНИЕ», заголовок, строки-требования (иконка, имя, счётчик «3/6», полоса), подсказка,
//                  кнопка-шеврон для сворачивания, клавиша J по кругу (полное → свёрнутое → скрыто). Вместо ObjectiveView,
//                  но с теми же данными: UIManager.SetObjective(ObjectiveData) и UIManager.UpdateObjective(string).
//
// Ресурсы и уровень отдельных скриптов не требуют: UIManager по-прежнему пишет в woodText / coinText / levelText / xpFillBar,
// сборщик просто указывает эти поля на новые тексты и полосу. Доски ведёт старый HudPlanks (слот скрыт, пока досок нет и пила закрыта).
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CandyHud : MonoBehaviour
{
    public static CandyHud Instance { get; private set; }
    public static bool Active { get { return Instance != null && Instance.isActiveAndEnabled; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { Instance = null; }

    [Serializable]
    public class Entry { public string key; public Sprite sprite; }

    [Header("Спрайты Candy Kit (заполняет сборщик)")]
    public List<Entry> sprites = new List<Entry>();

    [Header("Старый HUD (для возврата из меню; руками не трогать)")]
    public TMP_Text oldWoodText, oldCoinText, oldLevelText, oldObjectiveText;
    public Image oldXpFill;
    public List<GameObject> hiddenOld = new List<GameObject>();

    Dictionary<string, Sprite> map;

    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }
    void Start() { EnsureLevelBack(); }

    // Под звездой, плашкой «LVL N» и полосой опыта нужна облачная подложка (как у плашек ресурсов): без неё они висят прямо на сцене.
    // Сборщик подложку не рисовал, поэтому она создаётся здесь, один раз. Нашли объект «Back» в Level: ничего не делаем.
    void EnsureLevelBack()
    {
        var level = transform.Find("Level") as RectTransform;
        if (level == null || level.Find("Back") != null) return;
        using (UiKit.UseSkin(UiSkin.Candy))
        {
            var back = UiKit.MakeImage(level, "Back", UiKit.Panel, true, 0.62f);
            back.raycastTarget = false;
            back.gameObject.layer = level.gameObject.layer;
            var r = back.rectTransform;
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(30f, -10f);    // слева заходит под звезду, снизу уходит «юбка» панели
            r.offsetMax = new Vector2(12f, 6f);      // справа и сверху запас вокруг полосы и плашки
            r.SetAsFirstSibling();                   // под звездой, плашкой и полосой
        }
    }

    public Sprite Get(string key)
    {
        if (map == null)
        {
            map = new Dictionary<string, Sprite>();
            for (int i = 0; i < sprites.Count; i++)
                if (sprites[i] != null && !string.IsNullOrEmpty(sprites[i].key) && sprites[i].sprite != null) map[sprites[i].key] = sprites[i].sprite;
        }
        Sprite s;
        return map.TryGetValue(key, out s) ? s : null;
    }

    // иконка требования: свой спрайт > иконка Candy Kit > прежняя иконка UiKit
    public Sprite IconFor(ObjectiveReq q)
    {
        if (q == null) return null;
        if (q.sprite != null) return q.sprite;
        string key = null;
        switch (q.icon)
        {
            case UiIcon.Coin: key = "icon_coin"; break;
            case UiIcon.Log: key = "icon_log"; break;
            case UiIcon.Plank:
            case UiIcon.Planks: key = "icon_planks"; break;
            case UiIcon.Axe: key = "icon_axe"; break;
            case UiIcon.Boot: key = "icon_boot"; break;
            case UiIcon.Backpack: key = "icon_backpack"; break;
            case UiIcon.Saw: key = "icon_saw"; break;
            case UiIcon.Star: key = "icon_star"; break;
            case UiIcon.Trophy: key = "icon_trophy"; break;
            case UiIcon.Paw: key = "icon_paw"; break;
            case UiIcon.Lock: key = "icon_lock"; break;
            case UiIcon.Box: key = "icon_gift"; break;
        }
        Sprite s = key != null ? Get(key) : null;
        return s != null ? s : UiKit.CandyIcon(q.icon);   // запасной вариант: иконка Candy из UiKit, не латунная
    }
}

public class CandyTaskView : MonoBehaviour
{
    public static CandyTaskView Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { Instance = null; }

    [Header("Ссылки (расставляет сборщик HUD)")]
    public RectTransform panel;          // сама панель (Image panel_quest); этот компонент лежит на родителе, который всегда включён
    public TMP_Text title, footer;
    public Button chevron;
    public RectTransform chevronRt;
    public Sprite barFrame, barFillBlue, barFillMint;

    [Header("Размеры (единицы канваса)")]
    public float width = 560f;
    public float sidePad = 34f;
    public float topPad = 64f;           // под табличкой «ЗАДАНИЕ»
    public float bottomPad = 46f;        // голубая «юбка» облака
    public float rowHeight = 60f;
    public float rowGap = 6f;
    public float barHeight = 18f;

    public static readonly Color Ink = new Color32(0x2B, 0x2F, 0x5C, 255);
    public static readonly Color Muted = new Color32(0x5E, 0x6A, 0x96, 255);
    public static readonly Color Done = new Color32(0x2A, 0x95, 0x73, 255);
    const string CollapsePref = "objective_collapsed";
    const float SlideDist = 40f;

    class Row
    {
        public ObjectiveReq req;
        public RectTransform rt, fillRt, countRt;
        public Image icon, check, barBack, barFill;
        public TMP_Text name, count;
        public int have = -1;
        public bool done;
        public float punch, punchAmp, fill, fillTarget;
    }

    readonly List<Row> rows = new List<Row>(4);
    ObjectiveData cur;
    string sig;
    bool shown, collapsed, hiddenAll;
    float appear = 1f;
    CanvasGroup cg;
    Vector2 panelPos;

    public bool IsShown { get { return shown; } }

    void Awake()
    {
        Instance = this;
        if (panel != null)
        {
            cg = panel.GetComponent<CanvasGroup>();
            if (cg == null) cg = panel.gameObject.AddComponent<CanvasGroup>();
            panelPos = panel.anchoredPosition;
            panel.gameObject.SetActive(false);
        }
        try { collapsed = PlayerPrefs.GetInt(CollapsePref, 0) == 1; } catch { collapsed = false; }
        if (chevron != null)
        {
            chevron.onClick.AddListener(ToggleCollapsed);
            UpdateChevron();
        }
    }

    void OnEnable()
    {
        PlayerInventory.Changed += OnInventoryChanged;
    }

    void OnDisable()
    {
        PlayerInventory.Changed -= OnInventoryChanged;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void OnInventoryChanged() { if (shown) Poll(); }

    // ---------------------------------------------------------------- вход
    public void ShowText(string text)
    {
        if (string.IsNullOrEmpty(text)) { Hide(); return; }
        Show(new ObjectiveData(text));
    }

    public void Show(ObjectiveData d)
    {
        if (d == null || string.IsNullOrEmpty(d.title)) { Hide(); return; }

        // QuestGuide и прочие читают простой текст цели из UIManager.objectiveText
        var ui = UIManager.Instance;
        if (ui != null && ui.objectiveText != null) ui.objectiveText.text = d.ToPlain();

        string s = d.Signature;
        bool same = shown && s == sig;
        cur = d;
        if (same)
        {
            for (int i = 0; i < rows.Count && i < d.reqs.Count; i++) rows[i].req = d.reqs[i];
            if (footer != null && !string.IsNullOrEmpty(d.footer) && footer.text != d.footer) { footer.text = d.footer; Layout(); }
            Poll();
            return;
        }

        sig = s;
        Rebuild(d);
        if (!shown)
        {
            shown = true;
            if (panel != null) panel.gameObject.SetActive(true);
        }
        appear = 0f;
        Poll();
    }

    public void Hide()
    {
        shown = false;
        sig = null;
        cur = null;
        if (panel != null) panel.gameObject.SetActive(false);
    }

    // ---------------------------------------------------------------- сворачивание
    public void ToggleCollapsed() { if (hiddenAll) hiddenAll = false; else SetCollapsed(!collapsed); }

    // клавиша J: полное → свёрнутое → скрыто → полное
    public void CycleMode()
    {
        if (hiddenAll) { hiddenAll = false; SetCollapsed(false); }
        else if (!collapsed) SetCollapsed(true);
        else hiddenAll = true;
    }

    public void SetCollapsed(bool v)
    {
        if (collapsed == v) return;
        collapsed = v;
        try { PlayerPrefs.SetInt(CollapsePref, v ? 1 : 0); } catch { }
        UpdateChevron();
        Layout();
    }

    // Спрайт icon_chevron из Candy Kit v2 нарисован «>» (смотрит ВПРАВО). Разворачиваем его так, чтобы:
    // список развёрнут = «^» (можно свернуть), свёрнут = «v» (можно развернуть).
    const float ChevronOpenAngle = 90f;       // «>» повёрнутая на +90° = «^»
    const float ChevronCollapsedAngle = -90f; // «>» повёрнутая на -90° = «v»

    void UpdateChevron()
    {
        if (chevronRt != null) chevronRt.localRotation = Quaternion.Euler(0f, 0f, collapsed ? ChevronCollapsedAngle : ChevronOpenAngle);
    }

    // ---------------------------------------------------------------- построение строк
    void Rebuild(ObjectiveData d)
    {
        for (int i = 0; i < rows.Count; i++) if (rows[i].rt != null) Destroy(rows[i].rt.gameObject);
        rows.Clear();

        title.text = d.title;
        if (footer != null) footer.text = d.footer ?? "";

        for (int i = 0; i < d.reqs.Count; i++) rows.Add(MakeRow(d.reqs[i]));
        Layout();
    }

    Row MakeRow(ObjectiveReq q)
    {
        var font = title != null ? title.font : null;
        var r = new Row { req = q };
        var go = new GameObject("Row", typeof(RectTransform));
        go.layer = panel.gameObject.layer;
        r.rt = (RectTransform)go.transform;
        r.rt.SetParent(panel, false);
        r.rt.anchorMin = r.rt.anchorMax = new Vector2(0f, 1f);
        r.rt.pivot = new Vector2(0f, 1f);

        // иконка
        r.icon = MakeImage("Icon", r.rt, CandyHud.Instance != null ? CandyHud.Instance.IconFor(q) : UiKit.Icon(q.icon));
        r.icon.preserveAspect = true;
        SetRect(r.icon.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -2f), new Vector2(36f, 36f));

        // имя
        r.name = MakeText("Name", r.rt, q.name, 24f, Ink, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, font);
        SetRect(r.name.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(46f, 0f), new Vector2(-46f - 130f, 38f));

        // галочка и счётчик
        r.check = MakeImage("Check", r.rt, UiKit.Icon(UiIcon.Check));
        r.check.preserveAspect = true;
        SetRect(r.check.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-92f, -6f), new Vector2(26f, 26f));
        r.check.gameObject.SetActive(false);

        r.count = MakeText("Count", r.rt, "", 28f, Ink, TextAlignmentOptions.MidlineRight, FontStyles.Bold, font);
        r.countRt = r.count.rectTransform;
        SetRect(r.countRt, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(86f, 38f));

        // полоса
        r.barBack = MakeImage("Bar", r.rt, barFrame);
        r.barBack.type = Image.Type.Sliced;
        r.barBack.pixelsPerUnitMultiplier = 26f / Mathf.Max(8f, barHeight);
        SetRect(r.barBack.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, -40f), new Vector2(0f, barHeight));

        r.barFill = MakeImage("Fill", r.barBack.rectTransform, barFillBlue);
        r.barFill.type = Image.Type.Sliced;
        r.barFill.pixelsPerUnitMultiplier = r.barBack.pixelsPerUnitMultiplier;
        r.fillRt = r.barFill.rectTransform;
        r.fillRt.anchorMin = Vector2.zero;
        r.fillRt.anchorMax = new Vector2(0f, 1f);
        r.fillRt.offsetMin = r.fillRt.offsetMax = Vector2.zero;
        r.barFill.gameObject.SetActive(false);
        return r;
    }

    static Image MakeImage(string name, Transform parent, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        var im = go.GetComponent<Image>();
        im.sprite = sprite;
        im.raycastTarget = false;
        return im;
    }

    static TMP_Text MakeText(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align, FontStyles style, TMP_FontAsset font)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.fontStyle = style;
        t.raycastTarget = false;
        t.enableWordWrapping = false;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    static void SetRect(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
    }

    // ---------------------------------------------------------------- раскладка
    void Layout()
    {
        if (panel == null || title == null) return;
        float w = width, inner = w - sidePad * 2f;
        float y = topPad;

        float th = Mathf.Max(32f, title.GetPreferredValues(title.text, inner, 0f).y);
        var tr = title.rectTransform;
        SetRect(tr, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(sidePad, -y), new Vector2(inner, th));
        title.enableWordWrapping = true;
        y += th + 10f;

        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            bool on = !collapsed;
            if (r.rt != null) r.rt.gameObject.SetActive(on);
            if (!on) continue;
            SetRect(r.rt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(sidePad, -y), new Vector2(inner, rowHeight));
            y += rowHeight + rowGap;
        }

        bool showFooter = footer != null && !collapsed && !string.IsNullOrEmpty(footer.text);
        if (footer != null)
        {
            footer.gameObject.SetActive(showFooter);
            if (showFooter)
            {
                footer.enableWordWrapping = true;
                float fh = Mathf.Max(24f, footer.GetPreferredValues(footer.text, inner, 0f).y);
                SetRect(footer.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(sidePad, -y), new Vector2(inner, fh));
                y += fh + 4f;
            }
        }

        panel.sizeDelta = new Vector2(w, y + bottomPad);
    }

    // ---------------------------------------------------------------- живые счётчики
    static float Frac(ObjectiveReq q) { return q.need > 0 ? Mathf.Clamp01((float)q.Shown / q.need) : 0f; }

    void Poll()
    {
        for (int i = 0; i < rows.Count; i++)
        {
            Row r = rows[i];
            var q = r.req;
            if (q == null) continue;
            q.Refresh();
            int shownN = q.Shown;
            if (shownN == r.have) continue;

            bool up = shownN > r.have;
            bool wasDone = r.done;
            bool first = r.have < 0;
            r.have = shownN;
            r.done = q.Done;
            r.fillTarget = Frac(q);
            if (first) r.fill = r.fillTarget;
            else
            {
                r.punch = 1f;
                r.punchAmp = up ? 0.22f : 0.1f;
                if (r.done && !wasDone) { r.punchAmp = 0.4f; SfxManager.Play("hint", 0.8f, 1.35f); }
            }
            ApplyRow(r);
        }
    }

    void ApplyRow(Row r)
    {
        var q = r.req;
        r.count.text = q.need > 0 ? (r.have + "/" + q.need) : r.have.ToString();
        r.count.color = r.done ? Done : Ink;
        r.name.color = r.done ? Done : Ink;
        r.check.gameObject.SetActive(r.done);
        r.barFill.sprite = r.done && barFillMint != null ? barFillMint : barFillBlue;
        ApplyFill(r);
    }

    static void ApplyFill(Row r)
    {
        bool vis = r.fill > 0.015f;
        if (r.barFill.gameObject.activeSelf != vis) r.barFill.gameObject.SetActive(vis);
        if (vis) r.fillRt.anchorMax = new Vector2(Mathf.Clamp01(r.fill), 1f);
    }

    // ---------------------------------------------------------------- кадр
    void Update()
    {
        if (!shown || panel == null) return;
        float dt = Time.unscaledDeltaTime;

        if (Input.GetKeyDown(KeyCode.J) && !MobileInput.ModalOpen && !DialogueManager.IsBusy) CycleMode();

        float a = hiddenAll ? 0f : 1f;
        if (appear < 1f)
        {
            appear = Mathf.Min(1f, appear + dt / 0.35f);
            float e = UiDraw.EaseOutBack(appear);
            panel.localScale = Vector3.one * Mathf.LerpUnclamped(0.88f, 1f, e);
            panel.anchoredPosition = panelPos + new Vector2(0f, SlideDist * (1f - e));
            a *= Mathf.Clamp01(appear * 2.5f);
        }
        else
        {
            if (panel.localScale != Vector3.one) panel.localScale = Vector3.one;
            if (panel.anchoredPosition != panelPos) panel.anchoredPosition = panelPos;
        }
        if (cg != null)
        {
            cg.alpha = Mathf.MoveTowards(cg.alpha, a, dt / 0.15f);
            cg.blocksRaycasts = !hiddenAll;
            cg.interactable = !hiddenAll;
        }

        if (!collapsed) Poll();

        for (int i = 0; i < rows.Count; i++)
        {
            Row r = rows[i];
            if (r.rt == null || collapsed) continue;
            if (!Mathf.Approximately(r.fill, r.fillTarget))
            {
                r.fill = Mathf.Lerp(r.fill, r.fillTarget, 1f - Mathf.Exp(-9f * dt));
                if (Mathf.Abs(r.fill - r.fillTarget) < 0.002f) r.fill = r.fillTarget;
                ApplyFill(r);
            }
            if (r.punch > 0f)
            {
                r.punch = Mathf.Max(0f, r.punch - dt / 0.3f);
                float k = 1f + r.punchAmp * Mathf.Sin(r.punch * Mathf.PI);
                r.countRt.localScale = new Vector3(k, k, 1f);
                r.countRt.pivot = new Vector2(1f, 0.5f);
            }
            else if (r.countRt.localScale != Vector3.one) r.countRt.localScale = Vector3.one;
        }
    }
}
