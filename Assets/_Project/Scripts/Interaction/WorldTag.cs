// Assets/_Project/Scripts/Interaction/WorldTag.cs (НЕ в Editor)
// Табличка Candy, которая «висит» над объектом в мире: светлое облачко с хвостиком, индиго-текст, иконка слева.
// Круглая версия (таймер): светлый диск с индиго-контуром и цветным кольцом прогресса.
// Рисуется поверх экрана, поэтому деревья её не закрывают и текст всегда чёткий.
// Вешать вручную обычно не нужно: её создают InteractionHint, BackpackTag и TreeRespawn.
// Старые подсказки красят слова светлыми цветами под тёмную панель (<color=#6BCB5A>): SetText сам делает их темнее, вызывающий код менять не нужно.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WorldTag : MonoBehaviour
{
    [Header("Положение")]
    public Vector3 offset = new Vector3(0f, 2.5f, 0f);
    public float bobAmount = 0f;     // покачивание, px (0 = метка стоит спокойно, как метка стройки)
    public float bobSpeed = 2f;

    [Header("Вид")]
    public Sprite icon;              // необязательно: иконка слева от текста (только для прямоугольной)
    public TMP_FontAsset font;       // пусто = круглый шрифт Candy, а если его нет, шрифт задания из UIManager
    public float fontSize = 34f;
    public bool visible = true;

    const float TailW = 30f;         // ширина хвостика-указателя (единицы канваса)

    static RectTransform root;
    static Sprite discSprite, ringSprite;
    static bool nextCircle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { root = null; discSprite = ringSprite = null; nextCircle = false; }

    bool circle;
    RectTransform rt;
    CanvasGroup cg;
    TextMeshProUGUI label;
    Image fill, iconImg;
    string pending;
    float pendingProgress = 1f;
    float shownProgress = 1f;
    float punch, punchAmount;
    Color pendingColor = UiCandy.Mint;
    float shown;

    // Создать табличку. circle = true: круглая, с кольцом прогресса (для таймера)
    public static WorldTag Add(GameObject host, bool circle)
    {
        nextCircle = circle;
        var t = host.AddComponent<WorldTag>();
        nextCircle = false;
        return t;
    }

    public void SetText(string s)
    {
        pending = s;
        if (label != null) label.text = CandyTag.Readable(s);
    }

    public void SetFontSize(float size)
    {
        fontSize = size;
        if (label != null) label.fontSize = size;
    }

    // 0..1, только для круглой: заполнение кольца. instant = true: без плавного догоняния
    public void SetProgress(float value, bool instant = false)
    {
        pendingProgress = Mathf.Clamp01(value);
        if (instant) shownProgress = pendingProgress;
        if (instant && fill != null) fill.fillAmount = shownProgress;
    }

    public void SetProgressColor(Color color)
    {
        pendingColor = color;
        if (fill != null) fill.color = color;
    }

    // Иконка слева от текста. Можно менять и после создания (поле icon работало только до Awake)
    public void SetIcon(Sprite s)
    {
        icon = s;
        if (rt == null || circle) return;
        if (iconImg == null)
        {
            if (s == null) return;
            var ig = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            ig.layer = 5;
            ig.transform.SetParent(rt, false);
            ig.transform.SetAsFirstSibling();
            var le = ig.GetComponent<LayoutElement>();
            le.preferredWidth = le.preferredHeight = 40f;
            iconImg = ig.GetComponent<Image>();
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
        }
        iconImg.sprite = s;
        iconImg.gameObject.SetActive(s != null);
    }

    // Короткое «подпрыгивание» таблички (удар, ошибка, награда)
    public void Punch(float amount = 0.25f)
    {
        punchAmount = amount;
        punch = 1f;
    }

    void Awake()
    {
        circle = nextCircle;
        Build();
    }

    void Build()
    {
        EnsureRoot();

        var go = new GameObject("Tag_" + name, typeof(RectTransform), typeof(Image), typeof(CanvasGroup),
                                typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter), typeof(LayoutElement));
        go.layer = 5;
        go.transform.SetParent(root, false);

        rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0f); // табличка стоит над точкой, а не посередине

        var bg = go.GetComponent<Image>();
        if (circle)
        {
            bg.sprite = discSprite;
            bg.type = Image.Type.Simple;
            bg.color = Color.white;
            bg.raycastTarget = false;
        }
        else
        {
            CandyTag.StylePanel(bg, 0.42f);     // облачная панель Candy
        }

        cg = go.GetComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable = false;
        cg.alpha = 0f;

        var h = go.GetComponent<HorizontalLayoutGroup>();
        h.padding = circle ? new RectOffset(0, 0, 0, 0) : new RectOffset(22, 22, 12, 16);
        h.spacing = 10f;
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = false;

        var fit = go.GetComponent<ContentSizeFitter>();
        fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        if (circle)
        {
            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = le.preferredHeight = 84f;

            // светлая дорожка и цветное кольцо-таймер внутри индиго-контура диска
            MakeRing("Track", CandyTag.Track, false);
            fill = MakeRing("Fill", pendingColor, true);
            shownProgress = pendingProgress;
            fill.fillAmount = shownProgress;
            fill.color = pendingColor;
        }
        else
        {
            CandyTag.AddTail(rt, TailW);

            if (icon != null)
            {
                var ig = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                ig.layer = 5;
                ig.transform.SetParent(rt, false);
                var le = ig.GetComponent<LayoutElement>();
                le.preferredWidth = le.preferredHeight = 40f;
                var ii = ig.GetComponent<Image>();
                ii.sprite = icon;
                ii.preserveAspect = true;
                ii.raycastTarget = false;
                iconImg = ii;
            }
        }

        // текст: индиго, круглый шрифт, без обводки
        TMP_FontAsset fallback = font;
        if (fallback == null && UIManager.Instance != null && UIManager.Instance.objectiveText != null)
            fallback = UIManager.Instance.objectiveText.font;
        label = CandyTag.Text(rt, "Text", circle ? 34f : fontSize, CandyTag.Ink, TextAlignmentOptions.Center, true, fallback);
        label.textWrappingMode = TextWrappingModes.NoWrap;
        if (pending != null) label.text = CandyTag.Readable(pending);
    }

    Image MakeRing(string n, Color color, bool radial)
    {
        var g = new GameObject(n, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        g.layer = 5;
        g.transform.SetParent(rt, false);
        g.GetComponent<LayoutElement>().ignoreLayout = true;
        var r = (RectTransform)g.transform;
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(5f, 5f); r.offsetMax = new Vector2(-5f, -5f);      // кольцо внутри индиго-контура диска

        var img = g.GetComponent<Image>();
        img.sprite = ringSprite;
        img.color = color;
        img.raycastTarget = false;
        if (radial)
        {
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Radial360;
            img.fillOrigin = (int)Image.Origin360.Top;
            img.fillClockwise = true;
        }
        return img;
    }

    void LateUpdate()
    {
        if (rt == null) return;

        if (fill != null)
        {
            shownProgress = Mathf.Lerp(shownProgress, pendingProgress, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
            fill.fillAmount = shownProgress;
        }
        if (punch > 0f) punch = Mathf.Max(0f, punch - Time.unscaledDeltaTime / 0.3f);

        shown = Mathf.MoveTowards(shown, visible ? 1f : 0f, Time.unscaledDeltaTime * 6f);

        var cam = Camera.main;
        if (cam == null || shown <= 0.001f)
        {
            cg.alpha = 0f;
            rt.localScale = Vector3.zero;
            return;
        }

        Vector3 sp = cam.WorldToScreenPoint(transform.position + offset);
        if (sp.z <= 0f) { cg.alpha = 0f; return; }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, sp, null, out Vector2 p);
        p.y += Mathf.Sin(Time.time * bobSpeed) * bobAmount;
        if (!circle) p.y += CandyTag.TailHeight(TailW);    // кончик хвостика стоит в самой точке, а не низ панели

        rt.anchoredPosition = p;
        float punchScale = punch > 0f ? 1f + punchAmount * Mathf.Sin((1f - punch) * Mathf.PI) : 1f;
        rt.localScale = Vector3.one * (EaseOutBack(shown) * punchScale);
        cg.alpha = Mathf.Clamp01(shown * 1.6f);
    }

    void OnEnable() { if (rt != null) rt.gameObject.SetActive(true); }
    void OnDisable() { if (rt != null) rt.gameObject.SetActive(false); }
    void OnDestroy() { if (rt != null) Destroy(rt.gameObject); }

    // ---------- общий слой для всех табличек ----------
    static void EnsureRoot()
    {
        if (discSprite == null) discSprite = MakeDisc();
        if (ringSprite == null) ringSprite = MakeRingSprite(32f, 7f);
        if (root != null) return;

        var c = new GameObject("WorldTags_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        c.layer = 5;
        var canvas = c.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = UiLayers.WorldTag;
        var scaler = c.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        root = (RectTransform)c.transform;
    }

    // светлый диск с индиго-контуром (рисуется кодом, файлы не нужны)
    static Sprite MakeDisc()
    {
        const int S = 64;
        var tex = NewTex(S);
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(S * 0.5f, S * 0.5f)) - S * 0.5f + 0.5f;
                float a = Cov(d);
                Color c = d > -3.2f ? CandyTag.Ink : Color.Lerp(new Color32(0xE6, 0xEE, 0xFF, 255), Color.white, (y + 0.5f) / S);
                c.a = a;
                tex.SetPixel(x, y, c);
            }
        }
        return Finish(tex, S);
    }

    // кольцо толщиной thickness (в пикселях спрайта 64)
    static Sprite MakeRingSprite(float radius, float thickness)
    {
        const int S = 64;
        var tex = NewTex(S);
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(S * 0.5f, S * 0.5f)) - radius;
                float a = Mathf.Clamp01(Cov(d) - Cov(d + thickness));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        return Finish(tex, S);
    }

    static Texture2D NewTex(int s)
    {
        return new Texture2D(s, s, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
    }

    static Sprite Finish(Texture2D tex, int s)
    {
        tex.Apply();
        var sp = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, Vector4.zero);
        sp.hideFlags = HideFlags.HideAndDontSave;
        return sp;
    }

    static float Cov(float d) => Mathf.Clamp01(0.5f - d);

    static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float u = x - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }
}
