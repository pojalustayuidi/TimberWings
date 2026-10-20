using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Табличка в стиле HUD, которая «висит» над объектом в мире (как полоска рубки над игроком).
// Рисуется поверх экрана, поэтому деревья её не закрывают и текст всегда чёткий.
// Вешать вручную обычно не нужно: её создают InteractionHint, BackpackTag и TreeRespawn.
public class WorldTag : MonoBehaviour
{
    [Header("Положение")]
    public Vector3 offset = new Vector3(0f, 2.5f, 0f);
    public float bobAmount = 0f;     // покачивание, px (0 = метка стоит спокойно, как метка стройки)
    public float bobSpeed = 2f;

    [Header("Вид")]
    public Sprite icon;              // необязательно: иконка слева от текста (только для прямоугольной)
    public TMP_FontAsset font;       // пусто = шрифт задания из UIManager
    public float fontSize = 34f;
    public bool visible = true;

    static RectTransform root;
    static Sprite panelSprite, ringSprite, circleSprite, circleRing;
    static bool nextCircle;

    bool circle;
    RectTransform rt;
    CanvasGroup cg;
    TextMeshProUGUI label;
    Image fill, frameImg, iconImg;
    string pending;
    float pendingProgress = 1f;
    float shownProgress = 1f;
    float punch, punchAmount;
    Color pendingColor = new Color32(0x6B, 0xCB, 0x5A, 255);
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
        if (label != null) label.text = s;
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
        bg.sprite = circle ? circleSprite : UiKit.Panel;          // прямоугольная: панель UiKit (латунный ободок, заклёпки)
        bg.type = circle ? Image.Type.Simple : Image.Type.Sliced;
        if (!circle) bg.pixelsPerUnitMultiplier = 1f / 0.62f;   // та же толщина ободка, что у метки стройки
        bg.color = circle ? new Color32(0x1F, 0x2A, 0x44, 230) : Color.white;
        bg.raycastTarget = false;

        cg = go.GetComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable = false;
        cg.alpha = 0f;

        var h = go.GetComponent<HorizontalLayoutGroup>();
        h.padding = circle ? new RectOffset(0, 0, 0, 0) : new RectOffset(24, 24, 14, 16);
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

            // тёмная дорожка и зелёное кольцо-таймер
            MakeRing("Track", new Color32(0x14, 0x1C, 0x30, 255), false);
            fill = MakeRing("Fill", new Color32(0x6B, 0xCB, 0x5A, 255), true);
            shownProgress = pendingProgress;
            fill.fillAmount = shownProgress;
            fill.color = pendingColor;
        }
        else
        {
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

        // текст
        var tg = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        tg.layer = 5;
        tg.transform.SetParent(rt, false);
        label = tg.GetComponent<TextMeshProUGUI>();
        label.fontSize = circle ? 34f : fontSize;
        label.color = Color.white;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;

        if (font == null && UIManager.Instance != null && UIManager.Instance.objectiveText != null)
            font = UIManager.Instance.objectiveText.font;
        if (font != null) label.font = font;
        if (!circle) { label.outlineWidth = 0.18f; label.outlineColor = new Color32(0x14, 0x1C, 0x30, 255); }   // обводка после шрифта, иначе сбросится; как у метки стройки
        if (pending != null) label.text = pending;
    }

    Image MakeRing(string n, Color color, bool radial)
    {
        var g = new GameObject(n, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        g.layer = 5;
        g.transform.SetParent(rt, false);
        g.GetComponent<LayoutElement>().ignoreLayout = true;
        var r = (RectTransform)g.transform;
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;

        var img = g.GetComponent<Image>();
        img.sprite = circleRing;
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
        if (panelSprite == null) panelSprite = Make(false, 16f, 3f, true);
        if (ringSprite == null) ringSprite = Make(true, 16f, 3f, true);
        if (circleSprite == null) circleSprite = Make(false, 32f, 0f, false);
        if (circleRing == null) circleRing = Make(true, 32f, 7f, false);
        if (root != null) return;

        var c = new GameObject("WorldTags_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        c.layer = 5;
        var canvas = c.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5; // ниже HUD (10) и попапов (20)
        var scaler = c.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        root = (RectTransform)c.transform;
    }

    // Скруглённая панель или кольцо рисуются кодом, файлы не нужны
    static Sprite Make(bool ring, float radius, float thickness, bool sliced)
    {
        const int S = 64;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                float d = UiDraw.RRect(x + 0.5f, y + 0.5f, S, S, radius);
                float a = ring ? Mathf.Clamp01(Cov(d) - Cov(d + thickness)) : Cov(d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        Vector4 border = sliced ? new Vector4(20, 20, 20, 20) : Vector4.zero;
        return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f, 0,
                             SpriteMeshType.FullRect, border);
    }

    static float Cov(float d) => Mathf.Clamp01(0.5f - d);

    static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float u = x - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }
}