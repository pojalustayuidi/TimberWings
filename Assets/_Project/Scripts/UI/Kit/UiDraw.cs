// Assets/_Project/Scripts/UI/Kit/UiDraw.cs
// Общие мелочи для UI: математика скруглений, плавности, склонение слов и спрайты, нарисованные кодом.
using UnityEngine;

public static class UiDraw
{
    // ---------- математика ----------
    public static float RRect(float x, float y, float w, float h, float r)
    {
        float cx = x - w * 0.5f, cy = y - h * 0.5f;
        float qx = Mathf.Abs(cx) - (w * 0.5f - r), qy = Mathf.Abs(cy) - (h * 0.5f - r);
        float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
        return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
    }

    public static float Cov(float d) { return Mathf.Clamp01(0.5f - d); }

    public static float EaseOutCubic(float x) { return 1f - Mathf.Pow(1f - x, 3f); }

    public static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float u = x - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }

    public static string Plural(int n, string one, string few, string many)
    {
        int a = Mathf.Abs(n) % 100, b = a % 10;
        if (a > 10 && a < 20) return many;
        if (b > 1 && b < 5) return few;
        if (b == 1) return one;
        return many;
    }

    // ---------- спрайты (создаются один раз) ----------
    static Sprite pill, pillRing, star, coin, log, plank;

    public static Sprite Pill
    {
        get
        {
            if (pill == null)
                pill = Make(64, (x, y) => new Color(1f, 1f, 1f, Cov(RRect(x, y, 64f, 64f, 24f))), 26f);
            return pill;
        }
    }

    public static Sprite PillRing
    {
        get
        {
            if (pillRing == null)
                pillRing = Make(64, (x, y) =>
                {
                    float d = RRect(x, y, 64f, 64f, 24f);
                    return new Color(1f, 1f, 1f, Mathf.Clamp01(Cov(d) - Cov(d + 4f)));
                }, 26f);
            return pillRing;
        }
    }

    public static Sprite Star { get { if (star == null) star = Make(64, StarPixel, 0f); return star; } }
    public static Sprite Coin { get { if (coin == null) coin = Make(64, CoinPixel, 0f); return coin; } }
    public static Sprite Plank { get { if (plank == null) plank = Make(64, PlankPixel, 0f); return plank; } }
    public static Sprite Log { get { if (log == null) log = Make(64, LogPixel, 0f); return log; } }

    static Sprite Make(int size, System.Func<float, float, Color> px, float border)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        var cols = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                cols[y * size + x] = px(x + 0.5f, y + 0.5f);
        tex.SetPixels(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                             SpriteMeshType.FullRect, new Vector4(border, border, border, border));
    }

    static Color StarPixel(float x, float y)
    {
        float dx = x - 32f, dy = y - 31f;
        float d = Mathf.Sqrt(dx * dx + dy * dy);
        float a = Mathf.Atan2(dy, dx) - Mathf.PI * 0.5f;
        float sec = 2f * Mathf.PI / 5f;
        float t = Mathf.Abs(Mathf.Repeat(a, sec) - sec * 0.5f) / (sec * 0.5f);
        float r = Mathf.Lerp(13f, 29f, t);
        float cov = Cov(d - r);
        if (cov <= 0f) return Color.clear;
        Color gold = new Color32(0xFF, 0xD3, 0x4D, 255);
        Color dark = new Color32(0xD9, 0x9A, 0x1A, 255);
        Color c = (d > r - 3.5f) ? dark : gold;
        c.a = cov;
        return c;
    }

    static Color CoinPixel(float x, float y)
    {
        float d = Vector2.Distance(new Vector2(x, y), new Vector2(32f, 32f));
        float a = Cov(d - 30f);
        if (a <= 0f) return Color.clear;
        Color gold = new Color32(0xF5, 0xC5, 0x42, 255);
        Color dark = new Color32(0xC9, 0x9A, 0x1E, 255);
        Color shine = new Color32(0xFF, 0xE9, 0x8A, 255);
        Color c = (d > 25f || (d > 16f && d < 19f)) ? dark : gold;
        if (d < 15f && (x - 32f) + (32f - y) > 10f && d > 5f) c = shine;
        c.a = a;
        return c;
    }

    // доска: светлая планка с волокнами и торцами
    static Color PlankPixel(float x, float y)
    {
        float cov = Cov(RRect(x - 4f, y - 20f, 56f, 24f, 5f));
        if (cov <= 0f) return Color.clear;
        Color body = new Color32(0xE2, 0xBC, 0x82, 255);
        Color grain = new Color32(0xC9, 0x9C, 0x62, 255);
        Color edge = new Color32(0xB5, 0x86, 0x4F, 255);
        Color c = body;
        float ly = y - 32f;                                   // от -12 до 12
        if (Mathf.Abs(ly) > 9f) c = edge;                     // кромки
        else if (Mathf.Abs(ly + 3f) < 0.9f || Mathf.Abs(ly - 4f) < 0.7f) c = grain;
        if (x > 56f || x < 8f) c = edge;                      // торцы
        c.a = cov;
        return c;
    }

    static Color LogPixel(float x, float y)
    {
        Color bark = new Color32(0xA9, 0x74, 0x4A, 255);
        Color light = new Color32(0xD9, 0xB0, 0x7A, 255);
        Color ring = new Color32(0xB9, 0x84, 0x55, 255);

        Color c = Color.clear;
        float cb = Cov(RRect(x - 6f, y - 16f, 42f, 32f, 8f));
        if (cb > 0f) c = new Color(bark.r, bark.g, bark.b, cb);

        float dd = Vector2.Distance(new Vector2(x, y), new Vector2(46f, 32f));
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
