// Assets/_Project/Scripts/UI/HudGain.cs
// «+2 бревна» выезжает из-под панели дерева, «+2 XP» из-под панели уровня, «+8» из-под монет.
// Подряд идущие награды складываются в одну плашку («+1» -> «+2» -> «+4»), плашка подпрыгивает.
// Панели находятся по компоненту HudGainAnchor. Сам объект HudGain создаётся при первом Show.
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HudGain : MonoBehaviour
{
    public enum Kind { Wood = 0, Xp = 1, Coin = 2 }

    [Header("Время, с")]
    public float popTime = 0.22f;     // выезд из-под панели
    public float hold = 1.0f;         // сколько висит после последней награды
    public float fadeTime = 0.35f;    // таяние

    [Header("Вид")]
    public float fontSize = 30f;
    public float gap = 6f;            // зазор под панелью (в единицах канваса)
    public Sprite woodIcon;           // пусто = нарисую сам
    public Sprite xpIcon;
    public Sprite coinIcon;
    public Color woodColor = Color.white;
    public Color xpColor = new Color32(0x6B, 0xCB, 0x5A, 255);
    public Color coinColor = new Color32(0xF5, 0xC5, 0x42, 255);
    public Color panelColor = new Color32(0x1F, 0x2A, 0x44, 235);
    public Color outlineColor = new Color32(0x14, 0x1C, 0x30, 255);

    static HudGain instance;
    static bool quitting;

    // amount > 0. delay: через сколько секунд показать
    public static void Show(Kind kind, int amount, float delay = 0f)
    {
        if (amount <= 0 || quitting) return;
        if (instance == null) instance = FindFirstObjectByType<HudGain>();
        if (instance == null)
        {
            var go = new GameObject("HudGain");
            instance = go.AddComponent<HudGain>();
            DontDestroyOnLoad(go);
        }
        instance.Push(kind, amount, delay);
    }

    class Pending { public Kind kind; public int amount; public float time; }

    class Entry
    {
        public Kind kind;
        public RectTransform rt, anchor;
        public RectTransform behind;      // перед чем встаёт плашка в иерархии (пусто = перед anchor)
        public CanvasGroup cg;
        public TextMeshProUGUI txt;
        public int total;
        public float age, holdUntil, punch;
        public bool active;
    }

    readonly List<Pending> pending = new List<Pending>();
    readonly Entry[] entries = new Entry[3];
    readonly Vector3[] corners = new Vector3[4];

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(this); return; }
        instance = this;
    }

    void OnApplicationQuit() { quitting = true; }
    void OnDestroy() { if (instance == this) instance = null; }

    void Push(Kind kind, int amount, float delay)
    {
        if (delay <= 0.001f) { Apply(kind, amount); return; }
        pending.Add(new Pending { kind = kind, amount = amount, time = Time.unscaledTime + delay });
    }

    void Update()
    {
        float now = Time.unscaledTime;
        for (int i = pending.Count - 1; i >= 0; i--)
        {
            if (now < pending[i].time) continue;
            Pending p = pending[i];
            pending.RemoveAt(i);
            Apply(p.kind, p.amount);
        }
    }

    // двигаем плашки в LateUpdate: панели уже «подпрыгнули» в этом кадре
    void LateUpdate()
    {
        float dt = Time.unscaledDeltaTime;
        float now = Time.unscaledTime;
        for (int i = 0; i < entries.Length; i++) Tick(entries[i], dt, now);
    }

    // ---------- показ и слияние ----------
    void Apply(Kind kind, int amount)
    {
        Entry e = GetEntry(kind);
        if (e == null) return;

        float now = Time.unscaledTime;
        bool merge = e.active && now < e.holdUntil;
        if (merge)
        {
            e.total += amount;
            e.punch = 1f;
        }
        else
        {
            e.total = amount;
            e.age = 0f;
            e.punch = 0f;
            e.active = true;
            e.cg.alpha = 0f;
            e.rt.gameObject.SetActive(true);
            // сразу ПЕРЕД панелью в иерархии: плашка рисуется под панелью и «выезжает» из-под неё
            PlaceBehind(e);
        }
        e.holdUntil = now + hold;
        SetText(e);
    }

    // Плашка встаёт прямо перед целью (фон общей панели или сама панель). Если плашка уже стоит перед целью,
    // после её «вынимания» индекс цели сдвигается на 1: без этой поправки плашка при втором показе оказывалась ПОВЕРХ панели
    static void PlaceBehind(Entry e)
    {
        RectTransform target = e.behind != null ? e.behind : e.anchor;
        int i = target.GetSiblingIndex();
        if (e.rt.GetSiblingIndex() < i) i--;
        e.rt.SetSiblingIndex(Mathf.Max(0, i));
    }

    void SetText(Entry e)
    {
        string s;
        switch (e.kind)
        {
            case Kind.Wood: s = "+" + e.total + " " + UiDraw.Plural(e.total, "бревно", "бревна", "бревен"); break;
            case Kind.Xp: s = "+" + e.total + " XP"; break;
            default: s = "+" + e.total; break;
        }
        e.txt.text = s;
        LayoutRebuilder.ForceRebuildLayoutImmediate(e.rt);
    }

    void Tick(Entry e, float dt, float now)
    {
        if (e == null || !e.active) return;
        if (e.rt == null) { e.active = false; return; }
        if (e.anchor == null) { e.rt.gameObject.SetActive(false); e.active = false; return; }

        e.age += dt;
        float a = Mathf.Clamp01(e.age / Mathf.Max(0.01f, popTime));
        float scale = UiDraw.EaseOutBack(a);
        if (e.punch > 0f)
        {
            e.punch = Mathf.Max(0f, e.punch - dt / 0.22f);
            scale *= 1f + 0.16f * Mathf.Sin((1f - e.punch) * Mathf.PI);
        }

        float alpha = 1f, drop = 0f;
        if (now > e.holdUntil)
        {
            float f = (now - e.holdUntil) / Mathf.Max(0.01f, fadeTime);
            if (f >= 1f) { e.rt.gameObject.SetActive(false); e.active = false; return; }
            alpha = 1f - f;
            drop = 24f * UiDraw.EaseOutCubic(f);
        }

        var parent = e.rt.parent as RectTransform;
        float k = parent != null ? Mathf.Max(0.0001f, parent.lossyScale.x) : 1f;

        e.anchor.GetWorldCorners(corners);
        Vector3 bottom = (corners[0] + corners[3]) * 0.5f;          // середина нижнего края панели
        float h = e.rt.rect.height;
        float slide = (1f - UiDraw.EaseOutCubic(a)) * (h + gap);     // стартует спрятанной за панелью
        float bob = Mathf.Sin(now * 3f) * 2f;

        e.rt.position = bottom + new Vector3(0f, (-gap + slide - drop + bob) * k, 0f);
        e.rt.localScale = Vector3.one * Mathf.Max(0.0001f, scale);
        e.cg.alpha = alpha * Mathf.Clamp01(a * 3f);
    }

    // ---------- создание плашек ----------
    Entry GetEntry(Kind kind)
    {
        HudGainAnchor a = HudGainAnchor.Find(kind);
        if (a == null) return null;
        RectTransform anchor = a.Rect;
        RectTransform parent = anchor.parent as RectTransform;
        if (parent == null) return null;

        RectTransform behind = a.gainBehind != null && a.gainBehind.parent == parent ? a.gainBehind : null;
        Entry e = entries[(int)kind];
        if (e != null && (e.rt == null || e.anchor != anchor || e.behind != behind))
        {
            if (e.rt != null) Destroy(e.rt.gameObject);   // HUD пересобрали: делаем плашку заново
            e = null;
        }
        if (e == null)
        {
            e = Build(kind, anchor, behind, parent, a.GetFont());
            entries[(int)kind] = e;
        }
        return e;
    }

    Entry Build(Kind kind, RectTransform anchor, RectTransform behind, RectTransform parent, TMP_FontAsset font)
    {
        Color text, ring;
        Sprite icon;
        switch (kind)
        {
            case Kind.Wood:
                text = woodColor; ring = new Color32(0xB9, 0x84, 0x55, 255);
                icon = woodIcon != null ? woodIcon : UiKit.Icon(UiIcon.Log); break;
            case Kind.Xp:
                text = xpColor; ring = xpColor;
                icon = xpIcon != null ? xpIcon : UiKit.Icon(UiIcon.Star); break;
            default:
                text = coinColor; ring = coinColor;
                icon = coinIcon != null ? coinIcon : UiKit.Icon(UiIcon.Coin); break;
        }

        var go = new GameObject("Gain_" + kind, typeof(RectTransform), typeof(CanvasGroup),
                                typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter), typeof(LayoutElement));
        go.layer = anchor.gameObject.layer;
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().ignoreLayout = true;   // не ломаем layout-группы HUD

        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 1f);

        var cg = go.GetComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable = false;
        cg.alpha = 0f;

        var hg = go.GetComponent<HorizontalLayoutGroup>();
        hg.padding = new RectOffset(20, 24, 12, 12);
        hg.spacing = 8f;
        hg.childAlignment = TextAnchor.MiddleCenter;
        hg.childControlWidth = hg.childControlHeight = true;
        hg.childForceExpandWidth = hg.childForceExpandHeight = false;

        var fit = go.GetComponent<ContentSizeFitter>();
        fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        MakeLayer("Fill", rt, UiKit.Panel, Color.white, go.layer, 0.4f, 0f);
        MakeLayer("Ring", rt, UiDraw.PillRing, ring, go.layer, 1f, 4f);   // цветной акцент внутри латунного ободка

        var ig = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        ig.layer = go.layer;
        ig.transform.SetParent(rt, false);
        var le = ig.GetComponent<LayoutElement>();
        le.preferredWidth = le.preferredHeight = 34f;
        var img = ig.GetComponent<Image>();
        img.sprite = icon;
        img.preserveAspect = true;
        img.raycastTarget = false;

        var tg = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        tg.layer = go.layer;
        tg.transform.SetParent(rt, false);
        var t = tg.GetComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;                 // сначала шрифт, потом обводка
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        t.fontSize = fontSize;
        t.color = text;
        t.raycastTarget = false;
        t.outlineWidth = 0.22f;
        t.outlineColor = (Color32)outlineColor;

        go.SetActive(false);
        return new Entry { kind = kind, rt = rt, anchor = anchor, behind = behind, cg = cg, txt = t };
    }

    static void MakeLayer(string name, RectTransform parent, Sprite sprite, Color color, int layer, float borderScale = 1f, float inset = 0f)
    {
        var g = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        g.layer = layer;
        g.transform.SetParent(parent, false);
        g.GetComponent<LayoutElement>().ignoreLayout = true;
        var r = (RectTransform)g.transform;
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(inset, inset);
        r.offsetMax = new Vector2(-inset, -inset);
        var img = g.GetComponent<Image>();
        img.sprite = sprite;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 1f / Mathf.Max(0.1f, borderScale);
        img.color = color;
        img.raycastTarget = false;
    }
}
