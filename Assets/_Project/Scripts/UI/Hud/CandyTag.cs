// Assets/_Project/Scripts/UI/Hud/CandyTag.cs (НЕ в Editor)
// Общие мелочи для меток в мире и подсказок Candy (WorldTag, SmartWorldTag, ActionPrompt, BuildSiteTagView, PlayerHeadHud,
// QuestEdgeArrow, QuestGuide, QuestCelebration): спрайты Candy независимо от текущего скина, круглый шрифт, текст индиго,
// «перекраска» старых цветных тегов <color=#...> в цвета, читаемые на светлом поле, и несколько спрайтов, нарисованных кодом
// (значок-стрелка, булавка, шеврон дорожки). Ничего не знает про логику меток.
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class CandyTag
{
    // ---------------------------------------------------------------- спрайты UiKit в скине Candy
    // Любой сборщик может звать их, не думая про UiKit.Skin: на время вызова включается Candy и потом возвращается прежний скин.
    static Sprite With(Func<Sprite> f)
    {
        using (UiKit.UseSkin(UiSkin.Candy)) return f();
    }

    public static Sprite Panel { get { return With(() => UiKit.Panel); } }
    public static Sprite Plaque { get { return With(() => UiKit.Plaque); } }
    public static Sprite Slot { get { return With(() => UiKit.Slot); } }
    public static Sprite Medallion { get { return With(() => UiKit.Medallion); } }
    public static Sprite Keycap { get { return With(() => UiKit.Keycap); } }
    public static Sprite Tail { get { return With(() => UiKit.Tail); } }
    public static Sprite Icon(UiIcon icon) { return With(() => UiKit.Icon(icon)); }

    // ---------------------------------------------------------------- цвета на светлом поле
    public static readonly Color Ink = UiCandy.Ink;
    public static readonly Color Muted = UiCandy.Muted;
    public static readonly Color Good = UiCandy.Done;
    public static readonly Color Danger = UiCandy.Danger;
    public static readonly Color GoldText = UiCandy.GoldText;
    public static readonly Color CoralFill = new Color32(0xFF, 0x7A, 0x86, 255);   // заливка «полон» и «мимо»: светлее, чем текст Danger
    public static readonly Color Track = new Color32(0xC9, 0xCF, 0xEA, 255);       // дорожка полос: облачно-лавандовая

    // Старые подсказки и чужие скрипты красят слова светлыми цветами под тёмную панель (<color=#6BCB5A>E</color> и т. п.).
    // На светлой облачной метке они пропадают, поэтому при выводе заменяем их на тот же оттенок, но тёмнее.
    static readonly string[] OldHex = { "#6BCB5A", "#F5C542", "#F2A93B", "#F2B134", "#FFD66B", "#FF8A7A", "#AEB9D6", "#B8D8FF" };
    static readonly string[] NewHex = { "#2A9573", "#9A5F00", "#9A5F00", "#9A5F00", "#9A5F00", "#C8365C", "#5E6A96", "#4A7FC8" };

    public static string Readable(string s)
    {
        if (string.IsNullOrEmpty(s) || s.IndexOf("<color=#", StringComparison.Ordinal) < 0) return s;
        for (int i = 0; i < OldHex.Length; i++)
            s = s.Replace(OldHex[i], NewHex[i], StringComparison.OrdinalIgnoreCase);
        return s;
    }

    // ---------------------------------------------------------------- текст
    // круглый шрифт Candy, если он есть; иначе запасной (обычно шрифт задания из UIManager)
    public static void Font(TMP_Text t, TMP_FontAsset fallback = null)
    {
        if (t == null) return;
        var f = UiCandy.RoundFont != null ? UiCandy.RoundFont : fallback;
        if (f != null) t.font = f;
    }

    public static TMP_FontAsset BestFont(TMP_FontAsset fallback)
    {
        return UiCandy.RoundFont != null ? UiCandy.RoundFont : fallback;
    }

    // текст без обводки (метка светлая, обводка тёмная больше не нужна)
    public static TextMeshProUGUI Text(Transform parent, string name, float size, Color color,
                                       TextAlignmentOptions align = TextAlignmentOptions.Center,
                                       bool bold = true, TMP_FontAsset fallback = null)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        Font(t, fallback);
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        t.richText = true;
        t.raycastTarget = false;
        return t;
    }

    // ---------------------------------------------------------------- Image-мелочи
    // облачная панель на самом Image (borderScale меньше 1 = тоньше ободок для мелких плашек)
    public static void StylePanel(Image img, float borderScale)
    {
        img.sprite = Panel;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 1f / Mathf.Max(0.1f, borderScale);
        img.color = Color.white;
        img.raycastTarget = false;
    }

    // хвостик-указатель под плашкой: растянут по низу родителя и свешивается вниз, кончик в нижней точке
    public static RectTransform AddTail(RectTransform parent, float width = 34f, float overlap = 3f)
    {
        var go = new GameObject("Tail", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().ignoreLayout = true;
        var r = (RectTransform)go.transform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0f);
        r.pivot = new Vector2(0.5f, 1f);
        r.sizeDelta = new Vector2(width, width * 22f / 36f);
        r.anchoredPosition = new Vector2(0f, overlap);
        var im = go.GetComponent<Image>();
        im.sprite = Tail;
        im.raycastTarget = false;
        return r;
    }

    // высота хвостика AddTail (для подъёма метки, чтобы кончик стоял в нужной точке)
    public static float TailHeight(float width = 34f, float overlap = 3f) { return width * 22f / 36f - overlap; }

    // ---------------------------------------------------------------- спрайты, нарисованные кодом
    static Sprite badge, pin, chevron;
    static readonly Color Lemon = new Color32(0xFF, 0xDD, 0x48, 255);
    static readonly Color LemonHi = new Color32(0xFF, 0xF4, 0xA8, 255);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { badge = pin = chevron = null; }

    // круглый значок со стрелкой вверх (его поворачивают к цели): облачный диск, индиго-контур, лимонная стрелка
    public static Sprite Badge
    {
        get
        {
            if (badge == null)
                badge = MakeSprite(64, (x, y) =>
                {
                    var p = new Vector2(x, y);
                    float dc = Vector2.Distance(p, new Vector2(32f, 32f)) - 30f;
                    float a = Cov(dc);
                    if (a <= 0f) return Color.clear;
                    Color c = dc > -3.5f ? Ink : Color.Lerp(new Color32(0xE6, 0xEE, 0xFF, 255), Color.white, y / 64f);
                    float ad = TriSd(p, new Vector2(32f, 53f), new Vector2(15f, 19f), new Vector2(49f, 19f)) - 2.5f;
                    float aa = Cov(ad);
                    if (aa > 0f)
                    {
                        Color ac = ad > -2.4f ? Ink : Color.Lerp(Lemon, LemonHi, Mathf.InverseLerp(19f, 50f, y));
                        c = Color.Lerp(c, ac, aa);
                    }
                    c.a = a;
                    return c;
                }, new Vector2(0.5f, 0.5f), 100f);
            return badge;
        }
    }

    // булавка-капля остриём вниз (pivot у острия): лимонная, индиго-контур, светлая точка
    public static Sprite Pin
    {
        get
        {
            if (pin == null)
                pin = MakeSprite(64, (x, y) =>
                {
                    var p = new Vector2(x, y);
                    float dCirc = Vector2.Distance(p, new Vector2(32f, 42f)) - 20f;
                    float dTri = TriSd(p, new Vector2(32f, 4f), new Vector2(13f, 34f), new Vector2(51f, 34f)) - 1f;
                    float d = Mathf.Min(dCirc, dTri);
                    float a = Cov(d);
                    if (a <= 0f) return Color.clear;
                    Color c = d > -3.5f ? Ink : Color.Lerp(Lemon, LemonHi, Mathf.InverseLerp(10f, 60f, y));
                    float dot = Vector2.Distance(p, new Vector2(32f, 42f)) - 7.5f;
                    c = Color.Lerp(c, Color.white, Cov(dot));
                    c.a = a;
                    return c;
                }, new Vector2(0.5f, 4f / 64f), 100f);
            return pin;
        }
    }

    // шеврон «^» лежит на земле и показывает путь: лимонный с индиго-контуром
    public static Sprite Chevron
    {
        get
        {
            if (chevron == null)
                chevron = MakeSprite(64, (x, y) =>
                {
                    var p = new Vector2(x, y);
                    float outer = TriSd(p, new Vector2(32f, 60f), new Vector2(4f, 14f), new Vector2(60f, 14f)) - 1.5f;
                    float inner = TriSd(p, new Vector2(32f, 42f), new Vector2(16f, 12f), new Vector2(48f, 12f)) + 1.5f;
                    float d = Mathf.Max(outer, -inner);
                    float a = Cov(d);
                    if (a <= 0f) return Color.clear;
                    Color c = d > -3f ? Ink : Lemon;
                    c.a = a;
                    return c;
                }, new Vector2(0.5f, 0.5f), 64f);
            return chevron;
        }
    }

    // ---------------------------------------------------------------- математика
    static float Cov(float d) { return Mathf.Clamp01(0.5f - d); }

    static Sprite MakeSprite(int size, Func<float, float, Color> px, Vector2 pivot, float ppu)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var cols = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                cols[y * size + x] = px(x + 0.5f, y + 0.5f);
        tex.SetPixels(cols);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.hideFlags = HideFlags.HideAndDontSave;
        tex.Apply();
        var sp = Sprite.Create(tex, new Rect(0, 0, size, size), pivot, ppu);
        sp.hideFlags = HideFlags.HideAndDontSave;
        return sp;
    }

    // расстояние со знаком до треугольника (минус внутри)
    static float TriSd(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float s1 = SegD(p, a, b), s2 = SegD(p, b, c), s3 = SegD(p, c, a);
        float d = Mathf.Min(s1, Mathf.Min(s2, s3));
        float c1 = Cross(b - a, p - a), c2 = Cross(c - b, p - b), c3 = Cross(a - c, p - c);
        bool inside = (c1 >= 0f && c2 >= 0f && c3 >= 0f) || (c1 <= 0f && c2 <= 0f && c3 <= 0f);
        return inside ? -d : d;
    }

    static float Cross(Vector2 u, Vector2 v) { return u.x * v.y - u.y * v.x; }

    static float SegD(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
        return Vector2.Distance(p, a + ab * t);
    }
}
