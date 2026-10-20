using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Всплывающие награды на экране: «+2 [бревно]» и «+2 XP» вылетают из места, где срубили дерево,
// а бревно потом летит в счётчик дерева в HUD. Настраивать ничего не нужно: объект создаётся сам.
public class RewardPopups : MonoBehaviour
{
    static RewardPopups instance;
    static bool quitting;

    // Создаётся сам при первом обращении и переживает перезагрузку сцены
    public static RewardPopups Instance
    {
        get
        {
            if (instance == null && !quitting)
            {
                instance = FindFirstObjectByType<RewardPopups>();
                if (instance == null) instance = new GameObject("RewardPopups").AddComponent<RewardPopups>();
            }
            return instance;
        }
    }

    [Header("Внешний вид (необязательно)")]
    public TMP_FontAsset font;     // пусто = шрифт текста задания из UIManager
    public Sprite woodIcon;        // пусто = нарисую бревно сам
    public Color woodColor = Color.white;
    public Color xpColor = new Color32(0x6B, 0xCB, 0x5A, 255);
    public Color outlineColor = new Color32(0x14, 0x1C, 0x30, 255);

    [Header("Анимация")]
    public float riseHeight = 120f;   // на сколько пикселей (из 1080) всплывает
    public float lifetime = 1.3f;     // сколько живёт, с
    public float woodSize = 56f;      // размер «+2»
    public float xpSize = 38f;        // размер «+2 XP»
    public float xpDelay = 0.14f;     // на сколько позже появляется «+XP»

    [Header("Полёт бревна в HUD")]
    public bool flyToHud = true;
    public float flyTime = 0.7f;
    public int maxFlyIcons = 4;

    class Popup
    {
        public RectTransform rt;
        public CanvasGroup cg;
        public TextMeshProUGUI txt;
        public Image icon;
        public float age, delay;
        public Vector2 start;
    }

    class Fly
    {
        public RectTransform rt;
        public Image img;
        public float age, delay, dur, spin;
        public Vector2 a, b, c;
    }

    RectTransform canvasRect;
    readonly Stack<Popup> pool = new Stack<Popup>();
    readonly List<Popup> active = new List<Popup>();
    readonly Stack<Fly> flyPool = new Stack<Fly>();
    readonly List<Fly> flies = new List<Fly>();

    Transform flyTarget;   // HUD_Auto/Wood/Badge
    HudPop flyPop;         // HUD_Auto/Wood/Value

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var _ = Instance;
    }

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);

        var c = new GameObject("RewardPopups_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        c.layer = 5;
        c.transform.SetParent(transform, false);
        var canvas = c.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        var scaler = c.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasRect = (RectTransform)c.transform;

        if (woodIcon == null) woodIcon = BuildLogSprite();
    }

    void OnDestroy() { if (instance == this) instance = null; }
    void OnApplicationQuit() { quitting = true; }

    // worldPos: где срубили дерево. wood и xp: сколько показать (0 = не показывать)
    public void Show(Vector3 worldPos, int wood, int xp)
    {
        var cam = Camera.main;
        if (cam == null || canvasRect == null) return;

        Vector3 sp = cam.WorldToScreenPoint(worldPos);
        if (sp.z <= 0f) sp = new Vector3(Screen.width * 0.5f, Screen.height * 0.45f, 1f); // точка за камерой

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, sp, null, out Vector2 p)) return;

        // не даём уехать за край экрана
        Vector2 half = canvasRect.rect.size * 0.5f;
        p.x = Mathf.Clamp(p.x, -half.x + 140f, half.x - 140f);
        p.y = Mathf.Clamp(p.y, -half.y + 120f, half.y - riseHeight - 100f);
        p.x += Random.Range(-24f, 24f); // чтобы подряд срубленные не слипались

        if (wood > 0)
        {
            Spawn("+" + wood, woodColor, woodSize, woodIcon, p, 0f);
            if (flyToHud) SpawnFlies(p + new Vector2(0f, riseHeight * 0.45f), Mathf.Min(wood, maxFlyIcons));
        }
        if (xp > 0) Spawn("+" + xp + " XP", xpColor, xpSize, null, p + new Vector2(0f, -56f), xpDelay);
    }

    // Любой короткий текст над миром, например "Рюкзак полон"
    public void ShowText(Vector3 worldPos, string text, Color color, float size = 44f)
    {
        var cam = Camera.main;
        if (cam == null || canvasRect == null) return;
        Vector3 sp = cam.WorldToScreenPoint(worldPos);
        if (sp.z <= 0f) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, sp, null, out Vector2 p)) return;
        Spawn(text, color, size, null, p, 0f);
    }

    void Spawn(string text, Color color, float size, Sprite icon, Vector2 pos, float delay)
    {
        Popup p = pool.Count > 0 ? pool.Pop() : Create();

        p.rt.gameObject.SetActive(true);
        p.txt.text = text;
        p.txt.color = color;
        p.txt.fontSize = size;
        p.icon.sprite = icon;
        p.icon.gameObject.SetActive(icon != null);
        p.start = pos;
        p.age = 0f;
        p.delay = delay;
        p.cg.alpha = 0f;
        p.rt.anchoredPosition = pos;
        p.rt.localScale = Vector3.zero;
        p.rt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-5f, 5f));
        active.Add(p);
    }

    TMP_FontAsset ResolveFont()
    {
        if (font != null) return font;
        if (UIManager.Instance != null && UIManager.Instance.objectiveText != null)
            return UIManager.Instance.objectiveText.font;
        return null;
    }

    Popup Create()
    {
        var go = new GameObject("Popup", typeof(RectTransform), typeof(CanvasGroup),
                                typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        go.layer = 5;
        go.transform.SetParent(canvasRect, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);

        var cg = go.GetComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable = false;

        var h = go.GetComponent<HorizontalLayoutGroup>();
        h.childAlignment = TextAnchor.MiddleCenter;
        h.spacing = 8f;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = false;

        var fit = go.GetComponent<ContentSizeFitter>();
        fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var ig = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        ig.layer = 5;
        ig.transform.SetParent(rt, false);
        var le = ig.GetComponent<LayoutElement>();
        le.preferredWidth = le.preferredHeight = 52f;
        var img = ig.GetComponent<Image>();
        img.preserveAspect = true;
        img.raycastTarget = false;

        var tg = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        tg.layer = 5;
        tg.transform.SetParent(rt, false);
        var txt = tg.GetComponent<TextMeshProUGUI>();

        // ВАЖНО: сначала шрифт, потом обводка. Смена шрифта сбрасывает материал вместе с обводкой.
        var f = ResolveFont();
        if (f != null) txt.font = f;
        txt.alignment = TextAlignmentOptions.Center;
        txt.fontStyle = FontStyles.Bold;
        txt.raycastTarget = false;
        txt.outlineWidth = 0.22f;
        txt.outlineColor = (Color32)outlineColor;

        return new Popup { rt = rt, cg = cg, txt = txt, icon = img };
    }

    // ---------- бревно летит в счётчик HUD ----------
    void ResolveHud()
    {
        if (flyTarget != null) return;
        var hud = GameObject.Find("HUD_Auto");
        if (hud == null) return;
        flyTarget = hud.transform.Find("Wood/Badge");
        var v = hud.transform.Find("Wood/Value");
        flyPop = v != null ? v.GetComponent<HudPop>() : null;
    }

    void SpawnFlies(Vector2 from, int count)
    {
        ResolveHud();
        if (flyTarget == null || woodIcon == null || count <= 0) return;

        Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, flyTarget.position);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 to)) return;

        for (int i = 0; i < count; i++)
        {
            Fly f = flyPool.Count > 0 ? flyPool.Pop() : CreateFly();
            f.rt.gameObject.SetActive(true);
            f.img.sprite = woodIcon;
            f.a = from + new Vector2(Random.Range(-18f, 18f), Random.Range(-10f, 10f));
            f.b = to;
            Vector2 mid = (f.a + f.b) * 0.5f;
            f.c = mid + new Vector2(Random.Range(-160f, 160f), 140f + Random.Range(0f, 80f));
            f.age = 0f;
            f.delay = 0.45f + i * 0.09f;
            f.dur = flyTime;
            f.spin = Random.value < 0.5f ? -1f : 1f;
            f.rt.anchoredPosition = f.a;
            f.rt.localScale = Vector3.zero;
            flies.Add(f);
        }
    }

    Fly CreateFly()
    {
        var go = new GameObject("Fly", typeof(RectTransform), typeof(Image));
        go.layer = 5;
        go.transform.SetParent(canvasRect, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(46f, 46f);
        var img = go.GetComponent<Image>();
        img.raycastTarget = false;
        img.preserveAspect = true;
        return new Fly { rt = rt, img = img };
    }

    static Vector2 Bezier(Vector2 a, Vector2 c, Vector2 b, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * c + t * t * b;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        for (int i = active.Count - 1; i >= 0; i--)
        {
            Popup p = active[i];
            p.age += dt;
            float t = (p.age - p.delay) / lifetime;

            if (t < 0f) continue;            // ещё ждёт своей очереди

            if (t >= 1f)
            {
                p.rt.gameObject.SetActive(false);
                active.RemoveAt(i);
                pool.Push(p);
                continue;
            }

            // выпрыгивает с отскоком, потом плавно уходит вверх и тает
            float pop = t < 0.16f ? EaseOutBack(t / 0.16f) : 1f;
            float alpha = t < 0.65f ? 1f : 1f - (t - 0.65f) / 0.35f;
            float rise = riseHeight * EaseOutCubic(t);

            p.rt.localScale = Vector3.one * pop;
            p.rt.anchoredPosition = p.start + new Vector2(0f, rise);
            p.cg.alpha = alpha;
        }

        for (int i = flies.Count - 1; i >= 0; i--)
        {
            Fly f = flies[i];
            f.age += dt;
            float t = (f.age - f.delay) / f.dur;
            if (t < 0f) continue;

            if (t >= 1f)
            {
                f.rt.gameObject.SetActive(false);
                flies.RemoveAt(i);
                flyPool.Push(f);
                if (flyPop != null) flyPop.Pop();   // счётчик "чавкает" в момент прилёта
                continue;
            }

            float u = t * t * (3f - 2f * t);
            f.rt.anchoredPosition = Bezier(f.a, f.c, f.b, u);
            f.rt.localScale = Vector3.one * (Mathf.Min(1f, t * 8f) * Mathf.Lerp(1f, 0.55f, u));
            f.rt.localRotation = Quaternion.Euler(0f, 0f, u * 220f * f.spin);
        }
    }

    static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);

    static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float u = x - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }

    // ---------- бревно рисуется в коде, иконка не нужна ----------
    static Sprite BuildLogSprite()
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                px[y * size + x] = LogPixel(x + 0.5f, y + 0.5f);
        tex.SetPixels(px);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    static float Cov(float d) { return Mathf.Clamp01(0.5f - d); }

    static Color LogPixel(float x, float y)
    {
        Color bark = new Color32(0xA9, 0x74, 0x4A, 255);
        Color light = new Color32(0xD9, 0xB0, 0x7A, 255);
        Color ring = new Color32(0xB9, 0x84, 0x55, 255);

        Color c = Color.clear;
        float cb = Cov(UiDraw.RRect(x - 6f, y - 16f, 42f, 32f, 8f));
        if (cb > 0f) c = new Color(bark.r, bark.g, bark.b, cb);

        float dd = Vector2.Distance(new Vector2(x, y), new Vector2(46, 32));
        float cc = Cov(dd - 16f);
        if (cc > 0f)
        {
            Color cap = ((dd > 12.5f && dd < 14.5f) || (dd > 6f && dd < 8f)) ? ring : light;
            c = new Color(Mathf.Lerp(c.r, cap.r, cc), Mathf.Lerp(c.g, cap.g, cc),
                          Mathf.Lerp(c.b, cap.b, cc), Mathf.Max(c.a, cc));
        }
        return c;
    }
}