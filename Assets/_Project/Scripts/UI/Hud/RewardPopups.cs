using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Всплывающие награды на экране (Candy): «+2 [бревно]» и «+2 XP» вылетают облачными плашками из места, где срубили дерево,
// а бревно потом летит в счётчик дерева в HUD. Настраивать ничего не нужно: объект создаётся сам.
// Вид: светлое поле Candy, индиго-текст, иконка слева, пружинное появление, конфетти на бревне и искры на опыте.
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
    public TMP_FontAsset font;     // пусто = круглый шрифт Candy
    public Sprite woodIcon;        // пусто = Candy-бревно из HUD или нарисованное UiKit
    public Color woodColor = Color.white;                          // акцент плашки бревна: белый = тёплая «сливочная» плашка
    public Color xpColor = new Color32(0x6B, 0xCB, 0x5A, 255);     // акцент плашки опыта (мятная)

    [Header("Анимация")]
    public float riseHeight = 120f;   // на сколько пикселей (из 1080) всплывает
    public float lifetime = 1.3f;     // сколько живёт, с
    public float woodSize = 56f;      // размер «+2» (на плашке текст чуть меньше, см. TextScale)
    public float xpSize = 38f;        // размер «+2 XP»
    public float xpDelay = 0.14f;     // на сколько позже появляется «+XP»

    [Header("Полёт бревна в HUD")]
    public bool flyToHud = true;
    public float flyTime = 0.7f;
    public int maxFlyIcons = 4;

    const float TextScale = 0.8f;                       // размер текста на плашке относительно woodSize / xpSize / size
    const float PopStiffness = 240f, PopDamping = 13f;  // пружина появления (как у CandyPopIn)

    enum FxKind { None, Confetti, Sparks }

    class Popup
    {
        public RectTransform rt;
        public CanvasGroup cg;
        public TMP_Text txt;
        public Image icon, fill;
        public LayoutElement iconLe;
        public float age, delay;
        public Vector2 start;
        public CandySpring pop;
        public FxKind fx;
        public Color fxColor;
        public bool fxDone;
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
        canvas.sortingOrder = UiLayers.Popups;
        var scaler = c.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasRect = (RectTransform)c.transform;
    }

    void OnDestroy() { if (instance == this) instance = null; }
    void OnApplicationQuit() { quitting = true; }

    // Candy-бревно берём при показе, а не в Awake: к этому моменту CandyHud уже мог появиться в сцене
    Sprite WoodSprite { get { return woodIcon != null ? woodIcon : UiKit.CandyIcon(UiIcon.Log); } }

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
            Color accent = CandyPlaque.IsNeutral(woodColor) ? UiCandy.Lemon : woodColor;
            Spawn("+" + wood, CandyPlaque.Tint(accent, 0.3f), woodSize, WoodSprite, p, 0f, FxKind.Confetti, UiCandy.Gold);
            if (flyToHud) SpawnFlies(p + new Vector2(0f, riseHeight * 0.45f), Mathf.Min(wood, maxFlyIcons));
        }
        if (xp > 0)
            Spawn("+" + xp + " XP", CandyPlaque.Tint(xpColor, 0.28f), xpSize, UiKit.CandyIcon(UiIcon.Star),
                  p + new Vector2(0f, -56f), xpDelay, FxKind.Sparks, xpColor);
    }

    // Любой короткий текст над миром, например "Рюкзак полон".
    // color теперь акцент плашки (её лёгкая окраска), а не цвет текста: текст всегда индиго. Белый = нейтральная плашка.
    public void ShowText(Vector3 worldPos, string text, Color color, float size = 44f)
    {
        var cam = Camera.main;
        if (cam == null || canvasRect == null) return;
        Vector3 sp = cam.WorldToScreenPoint(worldPos);
        if (sp.z <= 0f) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, sp, null, out Vector2 p)) return;
        Color tint = CandyPlaque.IsNeutral(color) ? Color.white : CandyPlaque.Tint(color, 0.35f);
        Spawn(text, tint, size, null, p, 0f, FxKind.None, color);
    }

    void Spawn(string text, Color tint, float size, Sprite icon, Vector2 pos, float delay, FxKind fx, Color fxColor)
    {
        Popup p = pool.Count > 0 ? pool.Pop() : Create();

        p.rt.gameObject.SetActive(true);
        p.txt.text = text;
        p.txt.color = UiCandy.Ink;
        p.txt.fontSize = Mathf.Round(size * TextScale);
        p.fill.color = tint;
        p.icon.sprite = icon;
        p.icon.gameObject.SetActive(icon != null);
        p.start = pos;
        p.age = 0f;
        p.delay = delay;
        p.pop = new CandySpring(0.45f);
        p.fx = fx;
        p.fxColor = fxColor;
        p.fxDone = false;
        p.cg.alpha = 0f;
        p.rt.anchoredPosition = pos;
        p.rt.localScale = Vector3.zero;
        p.rt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-3f, 3f));
        active.Add(p);
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
        h.padding = new RectOffset(22, 28, 10, 14);   // снизу больше: у панели Candy голубая «юбка»
        h.childAlignment = TextAnchor.MiddleCenter;
        h.spacing = 8f;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = false;

        var fit = go.GetComponent<ContentSizeFitter>();
        fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var fill = CandyPlaque.Fill(rt, Color.white);
        fill.gameObject.layer = 5;

        var ig = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        ig.layer = 5;
        ig.transform.SetParent(rt, false);
        var le = ig.GetComponent<LayoutElement>();
        le.preferredWidth = le.preferredHeight = 44f;
        var img = ig.GetComponent<Image>();
        img.preserveAspect = true;
        img.raycastTarget = false;

        var tg = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        tg.layer = 5;
        tg.transform.SetParent(rt, false);
        var txt = tg.GetComponent<TextMeshProUGUI>();
        var f = font != null ? font : UiCandy.RoundFont;
        if (f != null) txt.font = f;
        txt.alignment = TextAlignmentOptions.Center;
        txt.fontStyle = FontStyles.Bold;
        txt.raycastTarget = false;     // обводки нет: плашка светлая, текст индиго
        txt.textWrappingMode = TextWrappingModes.NoWrap;

        return new Popup { rt = rt, cg = cg, txt = txt, icon = img, fill = fill, iconLe = le };
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
        var sprite = WoodSprite;
        if (flyTarget == null || sprite == null || count <= 0) return;

        Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, flyTarget.position);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 to)) return;

        for (int i = 0; i < count; i++)
        {
            Fly f = flyPool.Count > 0 ? flyPool.Pop() : CreateFly();
            f.rt.gameObject.SetActive(true);
            f.img.sprite = sprite;
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

    void PlayFx(Popup p)
    {
        if (p.fx == FxKind.None || VibeSettings.ReduceEffects) return;
        var kit = UiCandy.Kit;   // null (набор не создан) = без искр, плашки работают как обычно
        if (p.fx == FxKind.Confetti) CandyFx.Confetti(kit, p.rt, 12);
        else CandyFx.Burst(kit, p.rt, p.fxColor, 6, 220f);
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        bool calm = VibeSettings.ReduceEffects;

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

            if (!p.fxDone) { p.fxDone = true; PlayFx(p); }

            // выпрыгивает на пружине (как CandyPopIn), потом плавно уходит вверх и тает
            float scale;
            if (calm) scale = 1f;
            else
            {
                p.pop.Step(1f, PopStiffness, PopDamping, Mathf.Min(dt, 0.05f));
                scale = p.pop.x;
            }
            float alpha = t < 0.65f ? 1f : 1f - (t - 0.65f) / 0.35f;
            alpha = Mathf.Min(alpha, Mathf.Clamp01(t / 0.08f));   // быстрое проявление в начале
            float rise = riseHeight * UiDraw.EaseOutCubic(t);

            p.rt.localScale = Vector3.one * scale;
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
                if (!calm && flyTarget != null)
                    CandyFx.Burst(UiCandy.Kit, flyTarget as RectTransform, UiCandy.Gold, 4, 180f);
                continue;
            }

            float u = t * t * (3f - 2f * t);
            f.rt.anchoredPosition = Bezier(f.a, f.c, f.b, u);
            f.rt.localScale = Vector3.one * (Mathf.Min(1f, t * 8f) * Mathf.Lerp(1f, 0.55f, u));
            f.rt.localRotation = Quaternion.Euler(0f, 0f, u * 220f * f.spin);
        }
    }
}
