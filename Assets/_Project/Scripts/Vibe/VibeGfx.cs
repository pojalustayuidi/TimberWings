// Assets/_Project/Scripts/Vibe/VibeGfx.cs (НЕ в Editor)
// Спрайты, нарисованные кодом (как UiDraw): значок стрелки, булавка, шевроны для дорожки, мягкие точки и листья для частиц.
// Картинок в проект добавлять не нужно.
using System;
using UnityEngine;

public static class VibeGfx
{
    static Sprite soft, leaf, square, chevron, badge, pin;
    static readonly Color Navy = new Color32(0x1C, 0x2A, 0x3A, 255);
    static readonly Color Gold = new Color32(0xF5, 0xC5, 0x42, 255);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { soft = leaf = square = chevron = badge = pin = null; }

    // ---------- готовые спрайты ----------
    public static Sprite Soft
    {
        get
        {
            if (soft == null)
                soft = Make(32, (x, y) =>
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(16f, 16f)) / 16f;
                    float a = Mathf.Clamp01(1f - d);
                    return new Color(1f, 1f, 1f, a * a * (3f - 2f * a));
                }, new Vector2(0.5f, 0.5f), 100f);
            return soft;
        }
    }

    public static Sprite Leaf
    {
        get
        {
            if (leaf == null)
                leaf = Make(32, (x, y) =>
                {
                    float dx = (x - 16f) / 14f, dy = (y - 16f) / 7f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01((1f - r) * 7f);
                    float vein = Mathf.Abs(y - 16f) < 0.7f ? 0.78f : 1f;   // прожилка чуть темнее
                    return new Color(vein, vein, vein, a);
                }, new Vector2(0.5f, 0.5f), 100f);
            return leaf;
        }
    }

    public static Sprite Square
    {
        get
        {
            if (square == null) square = Make(8, (x, y) => Color.white, new Vector2(0.5f, 0.5f), 100f);
            return square;
        }
    }

    // шеврон «^» лежит на земле и показывает путь (смотрит вверх по спрайту)
    public static Sprite Chevron
    {
        get
        {
            if (chevron == null)
                chevron = Make(64, (x, y) =>
                {
                    float outer = TriD(x, y, new Vector2(32f, 60f), new Vector2(4f, 14f), new Vector2(60f, 14f));
                    float inner = TriD(x, y, new Vector2(32f, 42f), new Vector2(16f, 12f), new Vector2(48f, 12f));
                    float d = Mathf.Max(outer, -inner);
                    float a = Mathf.Clamp01(0.5f - d);
                    if (a <= 0f) return Color.clear;
                    Color c = (outer > -3f) ? Navy : Gold;
                    c.a = a;
                    return c;
                }, new Vector2(0.5f, 0.5f), 64f);
            return chevron;
        }
    }

    // круглый значок со стрелкой вверх (его поворачивают к цели)
    public static Sprite Badge
    {
        get
        {
            if (badge == null)
                badge = Make(64, (x, y) =>
                {
                    float dc = Vector2.Distance(new Vector2(x, y), new Vector2(32f, 32f)) - 30f;
                    float a = Mathf.Clamp01(0.5f - dc);
                    if (a <= 0f) return Color.clear;
                    Color c = (dc > -4f) ? Navy : Gold;
                    float ad = TriD(x, y, new Vector2(32f, 52f), new Vector2(17f, 20f), new Vector2(47f, 20f));
                    c = Color.Lerp(c, Navy, Mathf.Clamp01(0.5f - ad));
                    c.a = a;
                    return c;
                }, new Vector2(0.5f, 0.5f), 100f);
            return badge;
        }
    }

    // булавка-капля остриём вниз; pivot у острия
    public static Sprite Pin
    {
        get
        {
            if (pin == null)
                pin = Make(64, (x, y) =>
                {
                    float dCirc = Vector2.Distance(new Vector2(x, y), new Vector2(32f, 42f)) - 20f;
                    float dTri = TriD(x, y, new Vector2(32f, 4f), new Vector2(13f, 34f), new Vector2(51f, 34f));
                    float d = Mathf.Min(dCirc, dTri);
                    float a = Mathf.Clamp01(0.5f - d);
                    if (a <= 0f) return Color.clear;
                    Color c = (d > -3.5f) ? Navy : Gold;
                    float dot = Vector2.Distance(new Vector2(x, y), new Vector2(32f, 42f)) - 7f;
                    c = Color.Lerp(c, Color.white, Mathf.Clamp01(0.5f - dot));
                    c.a = a;
                    return c;
                }, new Vector2(0.5f, 4f / 64f), 100f);
            return pin;
        }
    }

    // материал для частиц: Sprites/Default работает в URP без настроек (цвет частицы = тон)
    public static Material ParticleMat(Sprite s)
    {
        Shader sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) sh = Shader.Find("Particles/Standard Unlit");
        if (sh == null)
        {
            // new Material(null) бросает исключение и роняет весь VibeBootstrap. Лечится так: Project Settings > Graphics > Always Included Shaders > добавить Sprites/Default
            Debug.LogWarning("[VibeGfx] Шейдер для частиц не найден (вырезан из билда?). Добавь Sprites/Default в Always Included Shaders.");
            return null;
        }
        var m = new Material(sh);
        if (s != null)
        {
            m.mainTexture = s.texture;
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", s.texture);
        }
        return m;
    }

    // ---------- математика ----------
    // расстояние до треугольника (положительное снаружи, отрицательное внутри), как у выпуклого многоугольника
    static float TriD(float px, float py, Vector2 a, Vector2 b, Vector2 c)
    {
        Vector2 cen = (a + b + c) / 3f;
        float d = Edge(px, py, a, b, cen);
        d = Mathf.Max(d, Edge(px, py, b, c, cen));
        d = Mathf.Max(d, Edge(px, py, c, a, cen));
        return d;
    }

    static float Edge(float px, float py, Vector2 a, Vector2 b, Vector2 cen)
    {
        Vector2 e = b - a;
        Vector2 n = new Vector2(e.y, -e.x).normalized;
        if (Vector2.Dot(n, cen - a) > 0f) n = -n;            // нормаль наружу
        return Vector2.Dot(new Vector2(px - a.x, py - a.y), n);
    }

    static Sprite Make(int size, Func<float, float, Color> px, Vector2 pivot, float ppu)
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
        return Sprite.Create(tex, new Rect(0, 0, size, size), pivot, ppu);
    }
}
