// Assets/_Project/Scripts/UI/Map/MapIcons.cs (НЕ в Editor)
// Спрайты карты, нарисованные кодом (картинок в проекте не нужно), в стиле Candy (индиго-контур, пастель, блики):
//   MapIcons.Get(MapIcons.Id.Campfire)   значки меток: костёр, хижина, рынок, палатка, мост, лес, доска заданий, точка
//   MapIcons.Pin                         круглая конфетная «подложка» метки (белая, красится через Image.color)
//   MapIcons.PinTint(id)                 пастельный цвет подложки для каждого вида метки (по цвету и значку метки не спутать)
//   MapIcons.Guess("PAD_Market")         какой значок подходит ключу типа метки
//   MapIcons.Frame                       облачная рамка большой карты (9-slice, центр прозрачный)
//   MapIcons.Compass                     круглый компас со стрелкой (север вверх, крутится через RectTransform)
//   MapIcons.PlayerArrow                 стрелка игрока (смотрит вверх, крутится через RectTransform)
//   MapIcons.SoftCircle / MapIcons.Ring  мягкое пятно и кольцо цели (кольцо уже лимонное с индиго-контуром)
// Значки 64x64 с индиго-контуром: читаются и на 24 px (720p), и на светлой траве, и на лавандовой дымке тумана.
using System;
using System.Collections.Generic;
using UnityEngine;

public static class MapIcons
{
    public enum Id { Campfire, Hut, Market, Tent, Bridge, Forest, Board, Dot }

    static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    // ---------------------------------------------------------------- публичное
    public static Sprite Get(Id id) { return Cached("icon_" + id, () => BuildIcon(id)); }
    public static Sprite Pin { get { return Cached("pin", BuildPin); } }
    public static Sprite PlayerArrow { get { return Cached("arrow", BuildArrow); } }
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
    static readonly Color Outline = C(0x2B, 0x2F, 0x5C, 245);          // индиго Candy (как в CandyTaskView)

    // пастельная подложка метки: каждому виду свой цвет
    public static Color PinTint(Id id)
    {
        switch (id)
        {
            case Id.Campfire: return C(0xFF, 0xC9, 0xA8);   // персик
            case Id.Hut: return C(0xFF, 0xB8, 0xC6);        // розовый
            case Id.Market: return C(0xB8, 0xF0, 0xD4);     // мята
            case Id.Tent: return C(0xD8, 0xD6, 0xFF);       // лаванда
            case Id.Bridge: return C(0xB8, 0xDE, 0xFF);     // небо
            case Id.Forest: return C(0x9F, 0xE8, 0xC0);     // зелёная мята
            case Id.Board: return C(0xFF, 0xEE, 0x9E);      // лимон
            default: return C(0xE9, 0xEC, 0xFF);            // облако
        }
    }

    // ---------------------------------------------------------------- рисовалка
    delegate float Sdf(float x, float y);

    struct Shape { public Sdf sdf; public Color col; }

    sealed class Painter
    {
        public readonly int W, H;
        public readonly Color[] px;
        public Painter(int w, int h) { W = w; H = h; px = new Color[w * h]; }

        // Zoom меньше 1: рисунок уменьшается к центру холста (значок помещается в круглую подложку), контур уменьшается вместе с ним
        public float Zoom = 1f;

        public void Fill(Sdf f, Color c)
        {
            float cx = W * 0.5f, cy = H * 0.5f;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float gx = x + 0.5f, gy = y + 0.5f;
                    if (Zoom != 1f) { gx = cx + (gx - cx) / Zoom; gy = cy + (gy - cy) / Zoom; }
                    float cov = Mathf.Clamp01(0.5f - f(gx, gy) * Zoom);
                    if (cov <= 0f) continue;
                    int i = y * W + x;
                    px[i] = Over(px[i], new Color(c.r, c.g, c.b, c.a * cov));
                }
        }

        // заливка с вертикальным градиентом: y0 (низ) .. y1 (верх) в пикселях спрайта
        public void FillGrad(Sdf f, float y0, float y1, Color c0, Color c1)
        {
            for (int y = 0; y < H; y++)
            {
                float t = Mathf.Clamp01((y + 0.5f - y0) / Mathf.Max(0.001f, y1 - y0));
                Color c = Color.Lerp(c0, c1, t);
                for (int x = 0; x < W; x++)
                {
                    float cov = Mathf.Clamp01(0.5f - f(x + 0.5f, y + 0.5f));
                    if (cov <= 0f) continue;
                    int i = y * W + x;
                    px[i] = Over(px[i], new Color(c.r, c.g, c.b, c.a * cov));
                }
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
    // Значок = только рисунок (без фона): круглую пастельную подложку даёт MapIcons.Pin. Рисунки крупные, с толстым индиго-контуром.
    static Sprite BuildIcon(Id id)
    {
        var p = new Painter(64, 64) { Zoom = 0.72f };                      // рисунок меньше холста: помещается в круг подложки
        const float ol = 3.0f;                                              // контур в единицах рисунка (после Zoom около 2.2 px)
        Color peachHi = C(0xFF, 0xC4, 0x7E), peachLo = C(0xF0, 0x93, 0x4E);
        Color cream = C(0xFF, 0xF6, 0xDD), creamLo = C(0xFF, 0xE2, 0xB4);
        Color coral = C(0xFF, 0x6F, 0x8E), coralHi = C(0xFF, 0x9D, 0xB0), lemon = C(0xFF, 0xDD, 0x48);
        Color mint = C(0x4F, 0xD1, 0x98), mintHi = C(0x8C, 0xEB, 0xC0), mintLo = C(0x2F, 0xB0, 0x7C);
        Color lav = C(0x8E, 0x86, 0xEE), lavHi = C(0xC9, 0xC5, 0xFF);
        switch (id)
        {
            case Id.Campfire:
                Draw(p, ol,
                    S(Capsule(13, 13, 51, 23, 4.6f), peachLo),
                    S(Capsule(13, 23, 51, 13, 4.6f), peachHi),
                    S(Union(Circle(32, 28, 10.5f), Tri(22.2f, 31, 41.8f, 31, 32, 58)), C(0xFF, 0x7A, 0x59)),
                    S(Union(Circle(32, 26, 5.6f), Tri(27.4f, 28, 36.6f, 28, 32, 45)), lemon));
                break;
            case Id.Hut:
                Draw(p, ol,
                    S(Box(45, 47, 3.8f, 6.5f, 1.2f), lavHi),
                    S(Box(32, 21, 17, 12, 3), creamLo),
                    S(Tri(8, 31, 56, 31, 32, 55), coral),
                    S(Tri(32, 55, 56, 31, 32, 31), C(0xE5, 0x58, 0x74)),
                    S(Box(32, 16.5f, 4.8f, 7, 2.2f), lav),
                    S(Box(21.5f, 22, 3.2f, 3.2f, 1.2f), lemon));
                break;
            case Id.Market:
            {
                var shapes = new List<Shape>();
                shapes.Add(S(Box(32, 15, 19, 8, 3f), peachLo));
                shapes.Add(S(Capsule(14, 20, 14, 44, 2.2f), peachHi));
                shapes.Add(S(Capsule(50, 20, 50, 44, 2.2f), peachHi));
                shapes.Add(S(Box(32, 46, 22, 8, 2.5f), cream));
                for (int i = 0; i < 7; i += 2) shapes.Add(S(Box(13 + i * 6f, 46, 3.2f, 8, 0f), coral));
                for (int i = 0; i < 4; i++) shapes.Add(S(Circle(16 + i * 10.7f, 38.2f, 3.6f), i % 2 == 0 ? coral : cream));
                shapes.Add(S(Circle(26, 23, 3.4f), lemon));
                shapes.Add(S(Circle(38, 23, 3.4f), mint));
                Draw(p, ol, shapes.ToArray());
                break;
            }
            case Id.Tent:
                Draw(p, ol,
                    S(Tri(7, 11, 57, 11, 32, 56), cream),
                    S(Tri(32, 56, 57, 11, 32, 11), creamLo),
                    S(Tri(24.5f, 11, 39.5f, 11, 32, 34), lav),
                    S(Capsule(32, 55, 32, 61, 1.6f), coral));
                break;
            case Id.Bridge:
            {
                var shapes = new List<Shape>();
                shapes.Add(S(Intersect(ArcRing(32, -4, 30, 3.8f), Above(11)), peachHi));
                shapes.Add(S(Intersect(ArcRing(32, -4, 39, 1.6f), Above(17)), peachLo));
                for (int i = 0; i < 5; i++)
                {
                    float px = 13f + i * 9.5f;
                    float yTop = -4f + Mathf.Sqrt(Mathf.Max(0f, 900f - (px - 32f) * (px - 32f)));
                    shapes.Add(S(Capsule(px, yTop, px, yTop + 8.5f, 1.6f), peachLo));
                }
                Draw(p, ol, shapes.ToArray());
                break;
            }
            case Id.Forest:
                Draw(p, ol,
                    S(Box(32, 10, 3.2f, 4.5f, 1f), peachLo),
                    S(Tri(11, 15, 53, 15, 32, 36), mintLo),
                    S(Tri(15, 27, 49, 27, 32, 48), mint),
                    S(Tri(20, 39, 44, 39, 32, 60), mintHi));
                break;
            case Id.Board:
                Draw(p, ol,
                    S(Capsule(21, 8, 21, 22, 2.2f), peachLo),
                    S(Capsule(43, 8, 43, 22, 2.2f), peachLo),
                    S(Box(32, 34, 21, 16, 4f), peachHi),
                    S(Box(32, 34, 17.5f, 12.5f, 2.6f), cream),
                    S(Capsule(24, 38, 40, 38, 1.3f), C(0x8F, 0x98, 0xC8)),
                    S(Capsule(24, 33, 40, 33, 1.3f), C(0x8F, 0x98, 0xC8)),
                    S(Capsule(24, 28, 35, 28, 1.3f), C(0x8F, 0x98, 0xC8)),
                    S(Circle(32, 44, 3.6f), coral));
                break;
            default:
                Draw(p, 3.4f, S(Circle(32, 32, 15), lemon), S(Circle(32, 32, 7), coral));
                break;
        }
        return p.ToSprite(Vector4.zero);
    }

    // ---------------------------------------------------------------- подложка метки
    // 64x64: индиго-контур, светлая «конфета» с градиентом (сверху белая, снизу холодная), блик. Красится через Image.color (PinTint).
    static Sprite BuildPin()
    {
        var p = new Painter(64, 64);
        p.Fill(Circle(32, 31, 31.5f), new Color(Outline.r, Outline.g, Outline.b, 0.22f));          // мягкая тень по низу
        p.Fill(Circle(32, 32, 30.5f), Outline);
        p.FillGrad(Circle(32, 32, 27.8f), 4f, 60f, C(0xC9, 0xD2, 0xF2), Color.white);
        p.Fill(Capsule(19, 49, 34, 54, 2.4f), new Color(1f, 1f, 1f, 0.85f));                        // блик
        p.Fill(Circle(42, 52, 1.8f), new Color(1f, 1f, 1f, 0.8f));
        return p.ToSprite(Vector4.zero);
    }

    // ---------------------------------------------------------------- стрелка игрока (смотрит вверх)
    static Sprite BuildArrow()
    {
        var p = new Painter(64, 64);
        Draw(p, 2.6f,
            S(Tri(32, 60, 10, 8, 32, 22), C(0xFF, 0x6F, 0x8E)),
            S(Tri(32, 60, 54, 8, 32, 22), C(0xFF, 0x9D, 0xB0)));
        p.Fill(Capsule(24, 30, 30, 46, 1.6f), new Color(1f, 1f, 1f, 0.55f));
        return p.ToSprite(Vector4.zero);
    }

    // ---------------------------------------------------------------- рамка большой карты
    // 96x96, рамка 9-slice с границей 32: индиго-контур, лавандовая полоса с градиентом и бликом, тонкий внутренний контур,
    // мягкая внутренняя тень, в углах белые «конфетки». Центр прозрачный.
    static Sprite BuildFrame()
    {
        const int S = 96;
        var p = new Painter(S, S);
        float bandW = 10f;
        Color hi = C(0xF1, 0xEF, 0xFF), lo = C(0xA2, 0x9B, 0xF2);
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float fx = x + 0.5f, fy = y + 0.5f;
                float d = UiDraw.RRect(fx, fy, S, S, 18f);   // <0 внутри внешнего контура
                float cov = Mathf.Clamp01(0.5f - d);
                if (cov <= 0f) continue;
                float depth = -d;                            // сколько px внутрь от внешнего края
                Color c;
                float a = 1f;
                if (depth < 2.2f) c = Outline;                                                    // внешний контур
                else if (depth < 2.2f + bandW)
                {
                    float t = Mathf.Clamp01(1f - fy / S);                                         // сверху светлее, снизу насыщеннее
                    c = Color.Lerp(hi, lo, t * 0.85f);
                    float edge = Mathf.InverseLerp(2.2f, 4.2f, depth);                            // блик по внешней кромке
                    c = Color.Lerp(c, Color.white, (1f - edge) * 0.45f);
                }
                else if (depth < 2.2f + bandW + 1.8f) c = new Color(Outline.r, Outline.g, Outline.b, 0.9f);   // тонкий внутренний контур
                else
                {
                    float t = Mathf.InverseLerp(2.2f + bandW + 1.8f + 11f, 2.2f + bandW + 1.8f, depth);        // внутренняя тень сходит на нет
                    c = Outline;
                    a = Mathf.Clamp01(t) * 0.28f;
                }
                int i = y * S + x;
                p.px[i] = new Color(c.r, c.g, c.b, a * cov);
            }
        // «конфетки» в углах: белые круглые заклёпки-пузыри с индиго-контуром, сидят на лавандовой полосе
        float rv = 10.4f, rd = 2.6f;
        Sdf[] dots = { Circle(rv, rv, rd), Circle(S - rv, rv, rd), Circle(rv, S - rv, rd), Circle(S - rv, S - rv, rd) };
        for (int i = 0; i < dots.Length; i++)
        {
            p.Fill(Grow(dots[i], 1.1f), Outline);
            p.Fill(dots[i], C(0xFF, 0xF3, 0xC4));
        }
        p.Fill(Circle(rv - 0.8f, S - rv + 0.8f, 0.9f), new Color(1f, 1f, 1f, 0.9f));
        p.Fill(Circle(S - rv - 0.8f, S - rv + 0.8f, 0.9f), new Color(1f, 1f, 1f, 0.9f));
        p.Fill(Circle(rv - 0.8f, rv + 0.8f, 0.9f), new Color(1f, 1f, 1f, 0.9f));
        p.Fill(Circle(S - rv - 0.8f, rv + 0.8f, 0.9f), new Color(1f, 1f, 1f, 0.9f));
        return p.ToSprite(new Vector4(32, 32, 32, 32));
    }

    // ---------------------------------------------------------------- компас
    static Sprite BuildCompass()
    {
        const int S = 128;
        var p = new Painter(S, S);
        float c = S * 0.5f;
        p.Fill(Circle(c, c, 62), Outline);
        p.FillGrad(Circle(c, c, 59.5f), 4f, 124f, C(0xA2, 0x9B, 0xF2), C(0xE3, 0xE0, 0xFF));        // лавандовое кольцо
        p.Fill(Circle(c, c, 52.5f), Outline);
        p.FillGrad(Circle(c, c, 50.5f), 12f, 116f, C(0xD7, 0xDE, 0xF8), Color.white);                  // светлая середина
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI * 0.25f;
            float r0 = (i % 2 == 0) ? 38f : 43f, r1 = 48f;
            p.Fill(Capsule(c + Mathf.Sin(a) * r0, c + Mathf.Cos(a) * r0, c + Mathf.Sin(a) * r1, c + Mathf.Cos(a) * r1, (i % 2 == 0) ? 2.2f : 1.5f), C(0x8E, 0x86, 0xEE));
        }
        Sdf north = Tri(c - 12, c, c + 12, c, c, c + 40);
        Sdf south = Tri(c - 12, c, c + 12, c, c, c - 40);
        p.Fill(Grow(Union(north, south), 2f), Outline);
        p.Fill(north, C(0xFF, 0x5C, 0x7A));                                                            // север: ягодный
        p.Fill(south, C(0xC9, 0xCF, 0xEA));                                                            // юг: облачный
        p.Fill(Circle(c, c, 7f), Outline);
        p.Fill(Circle(c, c, 5f), C(0xFF, 0xDD, 0x48));
        p.Fill(Capsule(c - 30, c + 52, c + 8, c + 56, 2.2f), new Color(1f, 1f, 1f, 0.55f));            // блик
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

    // кольцо метки цели: лимонное, с индиго-контуром и мягким ореолом (красится не нужно: цвет уже в спрайте)
    static Sprite BuildRing()
    {
        const int S = 128;
        var p = new Painter(S, S);
        float c = S * 0.5f;
        p.Fill(ArcRing(c, c, 52f, 11f), new Color(Outline.r, Outline.g, Outline.b, 0.2f));
        p.Fill(ArcRing(c, c, 52f, 8f), Outline);
        p.Fill(ArcRing(c, c, 52f, 5f), C(0xFF, 0xDD, 0x48));
        p.Fill(Intersect(ArcRing(c, c, 52f, 2f), Above(c + 8f)), new Color(1f, 1f, 1f, 0.7f));         // блик по верхней дуге
        return p.ToSprite(Vector4.zero);
    }
}
