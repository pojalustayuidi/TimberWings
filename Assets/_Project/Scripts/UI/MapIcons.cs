// Assets/_Project/Scripts/UI/MapIcons.cs (НЕ в Editor)
// Спрайты карты, нарисованные кодом (картинок в проекте не нужно), в палитре UiKit:
//   MapIcons.Get(MapIcons.Id.Campfire)   значки меток: костёр, хижина, рынок, палатка, мост, лес, доска заданий, точка
//   MapIcons.Guess("PAD_Market")         какой значок подходит ключу типа метки
//   MapIcons.Frame                       латунная рамка большой карты (9-slice, центр прозрачный)
//   MapIcons.Compass                     диск компаса со стрелкой (север вверх, крутится через RectTransform)
//   MapIcons.SoftCircle / MapIcons.Ring  мягкое пятно и кольцо (мигающая метка цели, зоны)
// Значки 64x64 с тёмной мягкой обводкой: читаются и на 24 px (720p), и на светлой траве, и на тёмном лесу.
using System;
using System.Collections.Generic;
using UnityEngine;

public static class MapIcons
{
    public enum Id { Campfire, Hut, Market, Tent, Bridge, Forest, Board, Dot }

    static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    // ---------------------------------------------------------------- публичное
    public static Sprite Get(Id id) { return Cached("icon_" + id, () => BuildIcon(id)); }
    public static Sprite Frame { get { return Cached("frame", BuildFrame); } }
    public static Sprite Compass { get { return Cached("compass", BuildCompass); } }
    public static Sprite SoftCircle { get { return Cached("soft", BuildSoft); } }
    public static Sprite Ring { get { return Cached("ring", BuildRing); } }

    // Подбор значка по ключу типа метки (ключи из HudMinimap.kinds: PAD_Market, CAMPFIRE_Point, Bridge, Forest ...)
    public static Id Guess(string key)
    {
        if (string.IsNullOrEmpty(key)) return Id.Dot;
        string k = key.ToLowerInvariant();
        if (Has(k, "camp", "fire", "костёр", "костер")) return Id.Campfire;
        if (Has(k, "hut", "house", "home", "хиж", "дом")) return Id.Hut;
        if (Has(k, "market", "shop", "stall", "trade", "рын", "лавк")) return Id.Market;
        if (Has(k, "tent", "палат")) return Id.Tent;
        if (Has(k, "bridge", "мост")) return Id.Bridge;
        if (Has(k, "forest", "wood", "grove", "лес", "бор")) return Id.Forest;
        if (Has(k, "board", "quest", "order", "доск", "заказ")) return Id.Board;
        return Id.Dot;
    }

    static bool Has(string s, params string[] keys)
    {
        for (int i = 0; i < keys.Length; i++) if (s.IndexOf(keys[i], StringComparison.Ordinal) >= 0) return true;
        return false;
    }

    static Sprite Cached(string key, Func<Sprite> make)
    {
        Sprite s;
        if (cache.TryGetValue(key, out s) && s != null) return s;   // s != null: спрайт мог быть уничтожен при выходе из Play
        s = make();
        s.name = "MapIcons_" + key;
        cache[key] = s;
        return s;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { cache.Clear(); }

    // ---------------------------------------------------------------- цвета
    static Color C(int r, int g, int b, int a = 255) { return new Color(r / 255f, g / 255f, b / 255f, a / 255f); }
    static readonly Color Outline = C(0x12, 0x1B, 0x28, 235);

    // ---------------------------------------------------------------- рисовалка
    delegate float Sdf(float x, float y);

    struct Shape { public Sdf sdf; public Color col; }

    sealed class Painter
    {
        public readonly int W, H;
        public readonly Color[] px;
        public Painter(int w, int h) { W = w; H = h; px = new Color[w * h]; }

        public void Fill(Sdf f, Color c)
        {
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float cov = Mathf.Clamp01(0.5f - f(x + 0.5f, y + 0.5f));
                    if (cov <= 0f) continue;
                    int i = y * W + x;
                    px[i] = Over(px[i], new Color(c.r, c.g, c.b, c.a * cov));
                }
        }

        // «поверх» в непремультиплицированных цветах
        static Color Over(Color d, Color s)
        {
            float a = s.a + d.a * (1f - s.a);
            if (a <= 0.0001f) return new Color(0, 0, 0, 0);
            float k = d.a * (1f - s.a);
            return new Color((s.r * s.a + d.r * k) / a, (s.g * s.a + d.g * k) / a, (s.b * s.a + d.b * k) / a, a);
        }

        public Sprite ToSprite(Vector4 border, FilterMode fm = FilterMode.Bilinear)
        {
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = fm };
            tex.SetPixels(px);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }
    }

    // ---------------------------------------------------------------- примитивы (расстояние со знаком, <0 внутри)
    static Sdf Circle(float cx, float cy, float r)
    {
        return (x, y) => Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r;
    }

    static Sdf Box(float cx, float cy, float hw, float hh, float r)
    {
        return (x, y) =>
        {
            float qx = Mathf.Abs(x - cx) - (hw - r), qy = Mathf.Abs(y - cy) - (hh - r);
            float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        };
    }

    static Sdf Capsule(float ax, float ay, float bx, float by, float r)
    {
        return (x, y) =>
        {
            float pax = x - ax, pay = y - ay, bax = bx - ax, bay = by - ay;
            float h = Mathf.Clamp01((pax * bax + pay * bay) / Mathf.Max(0.0001f, bax * bax + bay * bay));
            float dx = pax - bax * h, dy = pay - bay * h;
            return Mathf.Sqrt(dx * dx + dy * dy) - r;
        };
    }

    static Sdf Tri(float ax, float ay, float bx, float by, float cx, float cy)
    {
        return (x, y) =>
        {
            float e0x = bx - ax, e0y = by - ay, e1x = cx - bx, e1y = cy - by, e2x = ax - cx, e2y = ay - cy;
            float v0x = x - ax, v0y = y - ay, v1x = x - bx, v1y = y - by, v2x = x - cx, v2y = y - cy;
            float t0 = Mathf.Clamp01((v0x * e0x + v0y * e0y) / (e0x * e0x + e0y * e0y));
            float t1 = Mathf.Clamp01((v1x * e1x + v1y * e1y) / (e1x * e1x + e1y * e1y));
            float t2 = Mathf.Clamp01((v2x * e2x + v2y * e2y) / (e2x * e2x + e2y * e2y));
            float p0x = v0x - e0x * t0, p0y = v0y - e0y * t0;
            float p1x = v1x - e1x * t1, p1y = v1y - e1y * t1;
            float p2x = v2x - e2x * t2, p2y = v2y - e2y * t2;
            float s = Mathf.Sign(e0x * e2y - e0y * e2x);
            float d0 = p0x * p0x + p0y * p0y, c0 = s * (v0x * e0y - v0y * e0x);
            float d1 = p1x * p1x + p1y * p1y, c1 = s * (v1x * e1y - v1y * e1x);
            float d2 = p2x * p2x + p2y * p2y, c2 = s * (v2x * e2y - v2y * e2x);
            float dd = Mathf.Min(d0, Mathf.Min(d1, d2));
            float cc = Mathf.Min(c0, Mathf.Min(c1, c2));
            return -Mathf.Sqrt(dd) * Mathf.Sign(cc);
        };
    }

    static Sdf Union(params Sdf[] fs) { return (x, y) => { float d = 1e9f; for (int i = 0; i < fs.Length; i++) d = Mathf.Min(d, fs[i](x, y)); return d; }; }
    static Sdf Intersect(Sdf a, Sdf b) { return (x, y) => Mathf.Max(a(x, y), b(x, y)); }
    static Sdf Grow(Sdf a, float d) { return (x, y) => a(x, y) - d; }
    static Sdf ArcRing(float cx, float cy, float R, float t) { return (x, y) => Mathf.Abs(Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - R) - t; }
    static Sdf Above(float yMin) { return (x, y) => yMin - y; }          // внутри при y >= yMin

    static Shape S(Sdf f, Color c) { return new Shape { sdf = f, col = c }; }

    // обводка вокруг объединения всех фигур, потом сами фигуры по порядку
    static void Draw(Painter p, float outline, params Shape[] shapes)
    {
        var all = new Sdf[shapes.Length];
        for (int i = 0; i < shapes.Length; i++) all[i] = shapes[i].sdf;
        var u = Union(all);
        p.Fill(Grow(u, outline + 1.4f), new Color(Outline.r, Outline.g, Outline.b, 0.22f));   // мягкий внешний ореол
        p.Fill(Grow(u, outline), Outline);
        for (int i = 0; i < shapes.Length; i++) p.Fill(shapes[i].sdf, shapes[i].col);
    }

    // ---------------------------------------------------------------- значки
    static Sprite BuildIcon(Id id)
    {
        var p = new Painter(64, 64);
        Color woodHi = C(0xC9, 0x8A, 0x4B), woodLo = C(0x8F, 0x5A, 0x2E), brass = C(0xF2, 0xB1, 0x34), brassHi = C(0xFF, 0xD6, 0x6B);
        switch (id)
        {
            case Id.Campfire:
                Draw(p, 2.2f,
                    S(Capsule(13, 13, 51, 23, 4.4f), woodLo),
                    S(Capsule(13, 23, 51, 13, 4.4f), woodHi),
                    S(Union(Circle(32, 28, 10.5f), Tri(22.2f, 31, 41.8f, 31, 32, 58)), C(0xF2, 0x83, 0x2F)),
                    S(Union(Circle(32, 26, 5.6f), Tri(27.4f, 28, 36.6f, 28, 32, 45)), brassHi));
                break;
            case Id.Hut:
                Draw(p, 2.2f,
                    S(Box(45, 47, 3.6f, 6.5f, 0.6f), C(0x8E, 0x96, 0xA0)),
                    S(Box(32, 21, 17, 12, 2), woodHi),
                    S(Tri(8, 31, 56, 31, 32, 55), C(0xC8, 0x55, 0x3D)),
                    S(Tri(32, 55, 56, 31, 32, 31), C(0xA8, 0x40, 0x2E)),
                    S(Box(32, 16.5f, 4.6f, 7, 1), C(0x5B, 0x35, 0x1C)),
                    S(Box(21.5f, 22, 3, 3, 0.5f), brassHi));
                break;
            case Id.Market:
            {
                var shapes = new List<Shape>();
                shapes.Add(S(Box(32, 15, 19, 8, 1.5f), woodLo));
                shapes.Add(S(Capsule(14, 20, 14, 44, 1.8f), woodHi));
                shapes.Add(S(Capsule(50, 20, 50, 44, 1.8f), woodHi));
                shapes.Add(S(Box(32, 46, 22, 8, 1.5f), C(0xF4, 0xEA, 0xD0)));
                for (int i = 0; i < 7; i += 2) shapes.Add(S(Box(13 + i * 6f, 46, 3.2f, 8, 0f), C(0xD6, 0x45, 0x3A)));
                for (int i = 0; i < 4; i++) shapes.Add(S(Circle(16 + i * 10.7f, 38.2f, 3.4f), i % 2 == 0 ? C(0xD6, 0x45, 0x3A) : C(0xF4, 0xEA, 0xD0)));
                shapes.Add(S(Circle(26, 23, 3.2f), brass));
                shapes.Add(S(Circle(38, 23, 3.2f), C(0x6D, 0xBE, 0x6A)));
                Draw(p, 2.2f, shapes.ToArray());
                break;
            }
            case Id.Tent:
                Draw(p, 2.2f,
                    S(Tri(7, 11, 57, 11, 32, 56), C(0xE8, 0xDC, 0xC0)),
                    S(Tri(32, 56, 57, 11, 32, 11), C(0xCD, 0xBE, 0x9B)),
                    S(Tri(24.5f, 11, 39.5f, 11, 32, 34), C(0x3A, 0x2A, 0x1C)),
                    S(Capsule(32, 55, 32, 61, 1.2f), woodLo));
                break;
            case Id.Bridge:
            {
                var shapes = new List<Shape>();
                shapes.Add(S(Intersect(ArcRing(32, -4, 30, 3.6f), Above(11)), woodHi));
                shapes.Add(S(Intersect(ArcRing(32, -4, 39, 1.3f), Above(17)), woodLo));
                for (int i = 0; i < 5; i++)
                {
                    float px = 13f + i * 9.5f;
                    float yTop = -4f + Mathf.Sqrt(Mathf.Max(0f, 900f - (px - 32f) * (px - 32f)));
                    shapes.Add(S(Capsule(px, yTop, px, yTop + 8.5f, 1.4f), woodLo));
                }
                Draw(p, 2.2f, shapes.ToArray());
                break;
            }
            case Id.Forest:
                Draw(p, 2.2f,
                    S(Box(32, 10, 3, 4.5f, 0.5f), woodLo),
                    S(Tri(11, 15, 53, 15, 32, 36), C(0x2F, 0x78, 0x45)),
                    S(Tri(15, 27, 49, 27, 32, 48), C(0x3F, 0x8E, 0x52)),
                    S(Tri(20, 39, 44, 39, 32, 60), C(0x57, 0xA8, 0x62)));
                break;
            case Id.Board:
                Draw(p, 2.2f,
                    S(Capsule(21, 8, 21, 22, 2f), woodLo),
                    S(Capsule(43, 8, 43, 22, 2f), woodLo),
                    S(Box(32, 34, 21, 16, 2.5f), woodHi),
                    S(Box(32, 34, 17.5f, 12.5f, 1.2f), C(0xF4, 0xEA, 0xD0)),
                    S(Capsule(24, 38, 40, 38, 1.1f), C(0x8A, 0x7A, 0x5A)),
                    S(Capsule(24, 33, 40, 33, 1.1f), C(0x8A, 0x7A, 0x5A)),
                    S(Capsule(24, 28, 35, 28, 1.1f), C(0x8A, 0x7A, 0x5A)),
                    S(Circle(32, 44, 3.4f), C(0xD6, 0x45, 0x3A)));
                break;
            default:
                Draw(p, 2.4f, S(Circle(32, 32, 15), brassHi), S(Circle(32, 32, 7), C(0xB8, 0x74, 0x1A)));
                break;
        }
        return p.ToSprite(Vector4.zero);
    }

    // ---------------------------------------------------------------- рамка большой карты
    // 96x96, рамка 9-slice с границей 32: тёмный контур, латунная полоса с градиентом, канавка, внутренняя тень, заклёпки в углах. Центр прозрачный.
    static Sprite BuildFrame()
    {
        const int S = 96;
        var p = new Painter(S, S);
        float brassW = 9f;
        Color hi = C(0xFF, 0xD6, 0x6B), mid = C(0xF2, 0xB1, 0x34), lo = C(0xB8, 0x74, 0x1A);
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float fx = x + 0.5f, fy = y + 0.5f;
                float d = UiDraw.RRect(fx, fy, S, S, 16f);   // <0 внутри внешнего контура
                float cov = Mathf.Clamp01(0.5f - d);
                if (cov <= 0f) continue;
                float depth = -d;                            // сколько px внутрь от внешнего края
                Color c;
                float a = 1f;
                if (depth < 2f) c = Outline;                                                      // тёмный внешний контур
                else if (depth < 2f + brassW)
                {
                    float t = (fx / S) * 0.5f + (1f - fy / S) * 0.5f;                             // 0 слева-сверху .. 1 справа-снизу
                    c = t < 0.5f ? Color.Lerp(hi, mid, t * 2f) : Color.Lerp(mid, lo, (t - 0.5f) * 2f);
                    float edge = Mathf.InverseLerp(2f, 3.6f, depth);                              // блик по верхней кромке
                    c = Color.Lerp(c, Color.white, (1f - edge) * 0.18f);
                }
                else if (depth < 2f + brassW + 2f) c = new Color(0.16f, 0.09f, 0.03f, 1f);       // канавка между латунью и картой
                else
                {
                    float t = Mathf.InverseLerp(2f + brassW + 2f + 12f, 2f + brassW + 2f, depth);  // внутренняя тень сходит на нет
                    c = new Color(0.04f, 0.07f, 0.12f, 1f);
                    a = Mathf.Clamp01(t) * 0.55f;
                }
                int i = y * S + x;
                p.px[i] = new Color(c.r, c.g, c.b, a * cov);
            }
        // заклёпки
        float rv = 17f;
        Sdf[] rivets = { Circle(rv, rv, 3.2f), Circle(S - rv, rv, 3.2f), Circle(rv, S - rv, 3.2f), Circle(S - rv, S - rv, 3.2f) };
        for (int i = 0; i < rivets.Length; i++)
        {
            p.Fill(Grow(rivets[i], 1f), new Color(0.16f, 0.09f, 0.03f, 0.9f));
            p.Fill(rivets[i], C(0xB8, 0x74, 0x1A));
        }
        // блик на заклёпках
        p.Fill(Circle(rv - 1f, rv + 1f, 1.1f), new Color(1f, 1f, 1f, 0.7f));
        p.Fill(Circle(S - rv - 1f, rv + 1f, 1.1f), new Color(1f, 1f, 1f, 0.7f));
        p.Fill(Circle(rv - 1f, S - rv + 1f, 1.1f), new Color(1f, 1f, 1f, 0.7f));
        p.Fill(Circle(S - rv - 1f, S - rv + 1f, 1.1f), new Color(1f, 1f, 1f, 0.7f));
        return p.ToSprite(new Vector4(32, 32, 32, 32));
    }

    // ---------------------------------------------------------------- компас
    static Sprite BuildCompass()
    {
        const int S = 128;
        var p = new Painter(S, S);
        float c = S * 0.5f;
        p.Fill(Circle(c, c, 62), Outline);
        p.Fill(Circle(c, c, 59.5f), C(0xF2, 0xB1, 0x34));
        p.Fill(Circle(c, c, 54), C(0xB8, 0x74, 0x1A));
        p.Fill(Circle(c, c, 52), C(0x1C, 0x2A, 0x3A, 240));
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI * 0.25f;
            float r0 = (i % 2 == 0) ? 40f : 45f, r1 = 50f;
            p.Fill(Capsule(c + Mathf.Sin(a) * r0, c + Mathf.Cos(a) * r0, c + Mathf.Sin(a) * r1, c + Mathf.Cos(a) * r1, (i % 2 == 0) ? 1.8f : 1.1f), C(0xFF, 0xD6, 0x6B));
        }
        p.Fill(Tri(c - 11, c, c + 11, c, c, c + 40), C(0xE2, 0x4A, 0x3C));    // север
        p.Fill(Tri(c - 11, c, c + 11, c, c, c - 40), C(0xEE, 0xE6, 0xD2));    // юг
        p.Fill(Circle(c, c, 4.6f), C(0xFF, 0xD6, 0x6B));
        p.Fill(Circle(c, c, 2.2f), C(0x1C, 0x2A, 0x3A));
        return p.ToSprite(Vector4.zero);
    }

    // ---------------------------------------------------------------- мягкие
    static Sprite BuildSoft()
    {
        const int S = 128;
        var p = new Painter(S, S);
        float c = S * 0.5f;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - c) * (x + 0.5f - c) + (y + 0.5f - c) * (y + 0.5f - c)) / c;
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a);
                p.px[y * S + x] = new Color(1f, 1f, 1f, a);
            }
        return p.ToSprite(Vector4.zero);
    }

    static Sprite BuildRing()
    {
        const int S = 128;
        var p = new Painter(S, S);
        float c = S * 0.5f;
        p.Fill(ArcRing(c, c, 52f, 5f), new Color(1f, 1f, 1f, 1f));
        p.Fill(ArcRing(c, c, 52f, 9f), new Color(1f, 1f, 1f, 0.25f));
        return p.ToSprite(Vector4.zero);
    }
}
