// Assets/_Project/Scripts/Pets/PetIconKit.cs   (НЕ в Editor)
// Иконки питомцев, нарисованные кодом: 8 зверей в стиле «стикер» (толстый тёмный контур, большие глаза, мягкий объём).
// Картинок в проекте не нужно: спрайты рисуются при первом обращении, как в UiKit. Тот же подход: SDF-фигуры + заливки.
//
//   PetIconKit.Get("raccoon")     спрайт 432x432 с прозрачным фоном (кэшируется)
//   PetIconKit.Render("raccoon")  свежая Texture2D (для запекания в PNG, см. Editor/PetIconBaker.cs)
//   PetIconKit.Ids                овца, бобёр, кролик, барсук, сорока, енот, дятел, медведь, курица, свинья, собака, лошадь, кошка
//
// Холст: 144x144 дизайн-единиц, рисуем в рамке 0..128 (по 8 запаса с каждой стороны под контур и уши), Y вверх, Q = 3 пикселя на единицу.
// Фигуры: Circle, Ellipse, Rrect, Poly, Union, SmoothUnion, Intersect. Заливки: P (объёмная), F (плоская), G (вертикальный градиент).
// Блок «звери» внизу файла сгенерирован из описания в Python-прототипе: править руками можно, координаты читаются просто.
using System;
using System.Collections.Generic;
using UnityEngine;
using Sdf = System.Func<float, float, float>;
using Shade = System.Func<float, float, float, UnityEngine.Color>;   // x, y, расстояние до края фигуры (внутри меньше нуля)

public static class PetIconKit
{
    public const int Design = 144;
    public const int Q = 3;
    public const int PixelSize = Design * Q;
    const float Origin = -8f;

    public static readonly string[] Ids = { "sheep", "beaver", "rabbit", "badger", "magpie", "raccoon", "woodpecker", "bear", "chicken", "pig", "dog", "horse", "cat" };

    static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
    static readonly Dictionary<string, Color> palette = new Dictionary<string, Color>();
    static readonly Color Navy = new Color32(0x16, 0x24, 0x3D, 255);   // цвет контура

    public static bool Has(string id) { return !string.IsNullOrEmpty(id) && Array.IndexOf(Ids, id.ToLowerInvariant()) >= 0; }

    // спрайт иконки по id вида (sheep, raccoon...). Нет такого вида: null, тогда UI рисует лапку по-старому
    public static Sprite Get(string id)
    {
        if (!Has(id)) return null;
        id = id.ToLowerInvariant();
        Sprite s;
        if (cache.TryGetValue(id, out s) && s != null) return s;
        Texture2D tex = Render(id);
        s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f * Q);
        s.name = "PetIcon_" + id;
        cache[id] = s;
        return s;
    }

    public static Texture2D Render(string id)
    {
        if (!Has(id)) return null;
        id = id.ToLowerInvariant();
        var p = new Painter();
        var k = new Ink(p);
        switch (id)
        {
            case "sheep": Sheep(k); break;
            case "beaver": Beaver(k); break;
            case "rabbit": Rabbit(k); break;
            case "badger": Badger(k); break;
            case "magpie": Magpie(k); break;
            case "raccoon": Raccoon(k); break;
            case "woodpecker": Woodpecker(k); break;
            case "bear": Bear(k); break;
            case "chicken": Chicken(k); break;
            case "pig": Pig(k); break;
            case "dog": Dog(k); break;
            case "horse": Horse(k); break;
            default: Cat(k); break;
        }
        p.Outline(3f, Navy);
        p.Bleed();
        var tex = new Texture2D(PixelSize, PixelSize, TextureFormat.RGBA32, false);
        tex.name = "PetIcon_" + id;
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.SetPixels(p.px);
        tex.Apply(false, false);
        return tex;
    }

    // ============================================================ фигуры

    sealed class Shape
    {
        public readonly Sdf D;
        public readonly float X0, Y0, X1, Y1;   // границы (дизайн-единицы), по ним Painter ограничивает заливку
        public Shape(Sdf d, float x0, float y0, float x1, float y1) { D = d; X0 = x0; Y0 = y0; X1 = x1; Y1 = y1; }
    }

    static Vector2 V(float x, float y) { return new Vector2(x, y); }

    static Shape Circle(float cx, float cy, float r)
    {
        return new Shape((x, y) => Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r, cx - r, cy - r, cx + r, cy + r);
    }

    static Shape Ellipse(float cx, float cy, float rx, float ry, float rot = 0f)
    {
        float a = rot * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a), m = Mathf.Min(rx, ry);
        float w = Mathf.Sqrt(rx * c * rx * c + ry * s * ry * s), h = Mathf.Sqrt(rx * s * rx * s + ry * c * ry * c);
        return new Shape((x, y) =>
        {
            float dx = x - cx, dy = y - cy;
            float u = dx * c + dy * s, v = -dx * s + dy * c;
            return (Mathf.Sqrt((u / rx) * (u / rx) + (v / ry) * (v / ry)) - 1f) * m;
        }, cx - w, cy - h, cx + w, cy + h);
    }

    static Shape Rrect(float cx, float cy, float w, float h, float r, float rot = 0f)
    {
        float a = rot * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
        float hx = w * 0.5f, hy = h * 0.5f;
        float bw = hx * Mathf.Abs(c) + hy * Mathf.Abs(s), bh = hx * Mathf.Abs(s) + hy * Mathf.Abs(c);
        return new Shape((x, y) =>
        {
            float dx = x - cx, dy = y - cy;
            float u = dx * c + dy * s, v = -dx * s + dy * c;
            float qx = Mathf.Abs(u) - (hx - r), qy = Mathf.Abs(v) - (hy - r);
            float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }, cx - bw, cy - bh, cx + bw, cy + bh);
    }

    static Shape Capsule(float ax, float ay, float bx, float by, float r)
    {
        float ex = bx - ax, ey = by - ay, len2 = ex * ex + ey * ey + 1e-9f;
        return new Shape((x, y) =>
        {
            float px = x - ax, py = y - ay;
            float t = Mathf.Clamp01((px * ex + py * ey) / len2);
            float dx = px - ex * t, dy = py - ey * t;
            return Mathf.Sqrt(dx * dx + dy * dy) - r;
        }, Mathf.Min(ax, bx) - r, Mathf.Min(ay, by) - r, Mathf.Max(ax, bx) + r, Mathf.Max(ay, by) + r);
    }

    // многоугольник; round скругляет углы (раздувает контур)
    static Shape Poly(Vector2[] v, float round = 0f)
    {
        int n = v.Length;
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        for (int i = 0; i < n; i++)
        {
            minX = Mathf.Min(minX, v[i].x); maxX = Mathf.Max(maxX, v[i].x);
            minY = Mathf.Min(minY, v[i].y); maxY = Mathf.Max(maxY, v[i].y);
        }
        return new Shape((px, py) =>
        {
            float d = float.MaxValue;
            bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                Vector2 a = v[i], b = v[j];
                float ex = b.x - a.x, ey = b.y - a.y, wx = px - a.x, wy = py - a.y;
                float t = Mathf.Clamp01((wx * ex + wy * ey) / (ex * ex + ey * ey + 1e-9f));
                float dx = wx - ex * t, dy = wy - ey * t;
                d = Mathf.Min(d, dx * dx + dy * dy);
                if (((a.y > py) != (b.y > py)) && (px < (b.x - a.x) * (py - a.y) / (b.y - a.y) + a.x)) inside = !inside;
            }
            d = Mathf.Sqrt(d);
            return (inside ? -d : d) - round;
        }, minX - round, minY - round, maxX + round, maxY + round);
    }

    static Shape Union(params Shape[] fs)
    {
        float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
        for (int i = 0; i < fs.Length; i++)
        {
            x0 = Mathf.Min(x0, fs[i].X0); y0 = Mathf.Min(y0, fs[i].Y0);
            x1 = Mathf.Max(x1, fs[i].X1); y1 = Mathf.Max(y1, fs[i].Y1);
        }
        return new Shape((x, y) =>
        {
            float d = float.MaxValue;
            for (int i = 0; i < fs.Length; i++) d = Mathf.Min(d, fs[i].D(x, y));
            return d;
        }, x0, y0, x1, y1);
    }

    // «плавное» объединение: круги сливаются в облако (шерсть овцы, кудри)
    static Shape SmoothUnion(Shape[] fs, float k)
    {
        float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
        for (int i = 0; i < fs.Length; i++)
        {
            x0 = Mathf.Min(x0, fs[i].X0); y0 = Mathf.Min(y0, fs[i].Y0);
            x1 = Mathf.Max(x1, fs[i].X1); y1 = Mathf.Max(y1, fs[i].Y1);
        }
        return new Shape((x, y) =>
        {
            float acc = fs[0].D(x, y);
            for (int i = 1; i < fs.Length; i++)
            {
                float d = fs[i].D(x, y);
                float h = Mathf.Clamp01(0.5f + 0.5f * (d - acc) / k);
                acc = d * (1f - h) + acc * h - k * h * (1f - h);
            }
            return acc;
        }, x0 - k, y0 - k, x1 + k, y1 + k);
    }

    static Shape Intersect(Shape a, Shape b)
    {
        return new Shape((x, y) => Mathf.Max(a.D(x, y), b.D(x, y)),
                         Mathf.Max(a.X0, b.X0), Mathf.Max(a.Y0, b.Y0), Mathf.Min(a.X1, b.X1), Mathf.Min(a.Y1, b.Y1));
    }

    // квадратичная кривая Безье по трём точкам, n точек
    static Vector2[] Bez(Vector2 p0, Vector2 p1, Vector2 p2, float n = 14f)
    {
        int cnt = Mathf.Max(2, (int)n);
        var r = new Vector2[cnt];
        for (int i = 0; i < cnt; i++)
        {
            float t = i / (float)(cnt - 1), u = 1f - t;
            r[i] = u * u * p0 + 2f * u * t * p1 + t * t * p2;
        }
        return r;
    }

    static Shape Stroke(Vector2[] pts, float r)
    {
        var caps = new Shape[pts.Length - 1];
        for (int i = 0; i < caps.Length; i++) caps[i] = Capsule(pts[i].x, pts[i].y, pts[i + 1].x, pts[i + 1].y, r);
        return Union(caps);
    }

    // локальная система координат вдоль рукояти (u вдоль рукояти вверх, v вбок): удобно рисовать головку топора
    sealed class Frame
    {
        readonly float ox, oy, ux, uy, vx, vy, deg;
        public Frame(float ox, float oy, float dx, float dy)
        {
            float n = Mathf.Sqrt(dx * dx + dy * dy);
            this.ox = ox; this.oy = oy; ux = dx / n; uy = dy / n; vx = uy; vy = -ux;
            deg = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
        }
        public Vector2 O { get { return new Vector2(ox, oy); } }
        public Vector2 L(float u, float v) { return new Vector2(ox + ux * u + vx * v, oy + uy * u + vy * v); }
        public Vector2 W(float dx, float dy) { return new Vector2(ox + dx, oy + dy); }
        public Shape Rrect(float u, float v, float w, float h, float r) { Vector2 p = L(u, v); return PetIconKit.Rrect(p.x, p.y, w, h, r, deg + 90f); }
    }

    // ============================================================ цвет

    static Color Hex(string h, float a = 1f)
    {
        Color c;
        if (!palette.TryGetValue(h, out c))
        {
            c = new Color32(Convert.ToByte(h.Substring(1, 2), 16), Convert.ToByte(h.Substring(3, 2), 16), Convert.ToByte(h.Substring(5, 2), 16), 255);
            palette[h] = c;
        }
        c.a = a;
        return c;
    }

    static Color Lighten(Color c, float k) { return Color.Lerp(c, new Color(1f, 1f, 1f, c.a), k); }
    static Color Shaded(Color c, float k) { return Color.Lerp(c, new Color(0.16f, 0.10f, 0.22f, c.a), k); }   // тень чуть теплее и фиолетовее чёрного

    static Color Over(Color s, Color d)
    {
        float a = s.a + d.a * (1f - s.a);
        if (a <= 0.0001f) return new Color(0f, 0f, 0f, 0f);
        float k = d.a * (1f - s.a);
        return new Color((s.r * s.a + d.r * k) / a, (s.g * s.a + d.g * k) / a, (s.b * s.a + d.b * k) / a, a);
    }

    // «надутая» заливка: свет сверху, тень снизу, тёмная кайма внутри края формы
    static Shade Puffy(Shape s, Color baseC, float rim = 3.2f)
    {
        float y0 = s.Y0, hgt = Mathf.Max(0.001f, s.Y1 - s.Y0);
        Color light = Lighten(baseC, 0.38f), sh = Shaded(baseC, 0.28f), edge = Shaded(baseC, 0.5f);
        return (x, y, d) =>
        {
            float t = Mathf.Clamp01((y - y0) / hgt);
            Color c = t < 0.5f ? Color.Lerp(sh, baseC, t * 2f) : Color.Lerp(baseC, light, (t - 0.5f) * 2f);
            float k = Mathf.Clamp01(1f + d / rim);
            return Color.Lerp(c, edge, k * 0.55f);
        };
    }

    // ============================================================ холст

    sealed class Painter
    {
        public const int N = PixelSize;
        public readonly Color[] px = new Color[N * N];

        public void Fill(Shape s, Shade shade)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt((s.X0 - Origin - 1.5f) * Q));
            int x1 = Mathf.Min(N - 1, Mathf.CeilToInt((s.X1 - Origin + 1.5f) * Q));
            int y0 = Mathf.Max(0, Mathf.FloorToInt((s.Y0 - Origin - 1.5f) * Q));
            int y1 = Mathf.Min(N - 1, Mathf.CeilToInt((s.Y1 - Origin + 1.5f) * Q));
            for (int y = y0; y <= y1; y++)
            {
                float wy = (y + 0.5f) / Q + Origin;
                for (int x = x0; x <= x1; x++)
                {
                    float wx = (x + 0.5f) / Q + Origin;
                    float d = s.D(wx, wy);
                    float cov = Mathf.Clamp01(0.5f - d * Q);
                    if (cov <= 0f) continue;
                    Color c = shade(wx, wy, d);
                    c.a *= cov;
                    int i = y * N + x;
                    px[i] = Over(c, px[i]);
                }
            }
        }

        // тёмный контур вокруг всего нарисованного, ложится ПОД рисунок.
        // Быстро: считаем только в рамке рисунка, внутри закрашенного не ищем, длину строки круга берём из таблицы
        public void Outline(float t, Color c)
        {
            int r = Mathf.CeilToInt(t * Q);
            var a = new float[px.Length];
            int bx0 = N, by0 = N, bx1 = -1, by1 = -1;
            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    float v = px[y * N + x].a;
                    a[y * N + x] = v;
                    if (v > 0.001f)
                    {
                        if (x < bx0) bx0 = x;
                        if (x > bx1) bx1 = x;
                        if (y < by0) by0 = y;
                        if (y > by1) by1 = y;
                    }
                }
            }
            if (bx1 < 0) return;
            var ext = new int[r + 1];
            for (int oy = 0; oy <= r; oy++) ext[oy] = Mathf.FloorToInt(Mathf.Sqrt(r * r - oy * oy));
            for (int y = Mathf.Max(0, by0 - r); y <= Mathf.Min(N - 1, by1 + r); y++)
            {
                for (int x = Mathf.Max(0, bx0 - r); x <= Mathf.Min(N - 1, bx1 + r); x++)
                {
                    float m = a[y * N + x];
                    for (int oy = -r; oy <= r && m < 0.999f; oy++)
                    {
                        int yy = y + oy;
                        if (yy < 0 || yy >= N) continue;
                        int e = ext[Mathf.Abs(oy)];
                        int xa = Mathf.Max(0, x - e), xb = Mathf.Min(N - 1, x + e), row = yy * N;
                        for (int xx = xa; xx <= xb; xx++)
                        {
                            float v = a[row + xx];
                            if (v > m) m = v;
                        }
                    }
                    if (m > 0.001f)
                    {
                        int i = y * N + x;
                        px[i] = Over(px[i], new Color(c.r, c.g, c.b, m));
                    }
                }
            }
        }

        // цвет границы «протекает» в прозрачные пиксели, иначе при билинейной фильтрации по краям лезет тёмная кайма
        public void Bleed()
        {
            var src = (Color[])px.Clone();
            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    int i = y * N + x;
                    if (src[i].a > 0.001f) continue;
                    bool found = false;
                    for (int oy = -2; oy <= 2 && !found; oy++)
                    {
                        int yy = y + oy;
                        if (yy < 0 || yy >= N) continue;
                        for (int ox = -2; ox <= 2; ox++)
                        {
                            int xx = x + ox;
                            if (xx < 0 || xx >= N) continue;
                            Color n = src[yy * N + xx];
                            if (n.a > 0.05f) { px[i] = new Color(n.r, n.g, n.b, 0f); found = true; break; }
                        }
                    }
                }
            }
        }
    }

    // ============================================================ кисть: заливки и мелочи мордочки

    sealed class Ink
    {
        readonly Painter p;
        public Ink(Painter p) { this.p = p; }

        // объёмная заливка (refShape: по чьей высоте считать градиент, если рисуем кусок большей формы)
        public void P(Shape s, string hex, float a = 1f, Shape refShape = null) { p.Fill(s, Puffy(refShape ?? s, Hex(hex, a))); }

        // плоская заливка
        public void F(Shape s, string hex, float a = 1f) { Color c = Hex(hex, a); p.Fill(s, (x, y, d) => c); }

        // вертикальный градиент по высоте фигуры
        public void G(Shape s, string bottom, string top)
        {
            Color cb = Hex(bottom), ct = Hex(top);
            float y0 = s.Y0, hgt = Mathf.Max(0.001f, s.Y1 - s.Y0);
            p.Fill(s, (x, y, d) => Color.Lerp(cb, ct, Mathf.Clamp01((y - y0) / hgt)));
        }

        public void Line(Vector2[] pts, float w, string hex, float a = 1f) { F(Stroke(pts, w * 0.5f), hex, a); }

        public void Smile(float cx, float cy, float w = 7f, float depth = 3.2f, bool open = true)
        {
            if (open)   // открытый рот: тёмный полумесяц и язык
            {
                var pts = new List<Vector2>(Bez(V(cx - w, cy + 1.5f), V(cx, cy - depth * 2.6f), V(cx + w, cy + 1.5f), 16f));
                pts.Add(V(cx + w * 0.2f, cy + 2.4f));
                pts.Add(V(cx - w * 0.2f, cy + 2.4f));
                Shape m = Poly(pts.ToArray());
                F(m, "#4a1d1d");
                F(Intersect(Ellipse(cx, cy - 2.6f, w * 0.5f, 2.4f), m), "#f0707a");
            }
            else Line(Bez(V(cx - w, cy + 0.5f), V(cx, cy - depth * 1.7f), V(cx + w, cy + 0.5f), 14f), 1.5f, "#3a2418");
        }

        // большой блестящий глаз: тёмный низ с янтарным отсветом и два блика
        public void Eye(float cx, float cy, float r = 6.6f, Vector2 look = default(Vector2))
        {
            float ex = cx + look.x, ey = cy + look.y;
            Shape eye = Ellipse(ex, ey, r * 0.95f, r * 1.08f);
            Color dark = Hex("#1a0f0c"), amber = Hex("#a8561a");
            p.Fill(eye, (x, y, d) => Color.Lerp(dark, amber, Mathf.Pow(Mathf.Clamp01((ey - y) / (r * 1.1f) * 0.9f + 0.05f), 1.6f)));
            F(Ellipse(ex, ey - r * 0.05f, r * 0.62f, r * 0.72f), "#6b3410", 0.55f);
            F(Circle(ex - r * 0.32f, ey + r * 0.38f, r * 0.40f), "#ffffff");
            F(Circle(ex + r * 0.34f, ey - r * 0.36f, r * 0.20f), "#ffffff", 0.92f);
        }

        public void Blush(float cx, float cy, float rx = 5f, float ry = 3f) { F(Ellipse(cx, cy, rx, ry), "#ff7f7f", 0.38f); }

        public void Nose(float cx, float cy, float w = 3.4f, string hex = "#2a1a1a")
        {
            F(Ellipse(cx, cy, w, w * 0.72f), hex);
            F(Ellipse(cx - w * 0.35f, cy + w * 0.28f, w * 0.38f, w * 0.2f), "#ffffff", 0.7f);
        }

        // клетка из линий (чешуя хвоста бобра)
        public void Grid(Shape s, string baseHex, string lineHex, string edgeHex, float cx, float cy, float rot, float period = 6f, float lw = 0.7f)
        {
            float a = rot * Mathf.Deg2Rad, c = Mathf.Cos(a), sn = Mathf.Sin(a);
            Color cb = Hex(baseHex), cl = Hex(lineHex), ce = Hex(edgeHex);
            p.Fill(s, (x, y, d) =>
            {
                float u = (x - cx) * c + (y - cy) * sn, v = -(x - cx) * sn + (y - cy) * c;
                bool g = Mathf.Abs(Mathf.Repeat(u + 40f, period) - period * 0.5f) < lw || Mathf.Abs(Mathf.Repeat(v + 40f, period) - period * 0.5f) < lw;
                return Color.Lerp(g ? cl : cb, ce, Mathf.Clamp01(1f + d / 3f) * 0.4f);
            });
        }

        // полосатая заливка (хвост енота)
        public void Bands(Shape s, string c1, string c2, string edge, float cx, float cy, float rot, float period = 8f)
        {
            float a = rot * Mathf.Deg2Rad, c = Mathf.Cos(a), sn = Mathf.Sin(a);
            Color ca = Hex(c1), cb = Hex(c2), ce = Hex(edge);
            p.Fill(s, (x, y, d) =>
            {
                float u = (x - cx) * sn - (y - cy) * c;
                int band = Mathf.FloorToInt((u + 60f) / period);
                return Color.Lerp(((band % 2) + 2) % 2 == 0 ? ca : cb, ce, Mathf.Clamp01(1f + d / 3f) * 0.35f);
            });
        }

        // клетчатая ткань (кепка лесоруба): тёмный, средний и светлый квадраты, к верху чуть светлее
        public void Plaid(Shape s, string dark, string mid, string light, string edge, float cell, float yFade0, float yFade1)
        {
            Color cd = Hex(dark), cm = Hex(mid), cl = Hex(light), ce = Hex(edge), white = new Color(1f, 1f, 1f, 1f);
            p.Fill(s, (x, y, d) =>
            {
                bool a = (((Mathf.FloorToInt(x / cell) % 2) + 2) % 2) == 0;
                bool b = (((Mathf.FloorToInt(y / cell) % 2) + 2) % 2) == 0;
                Color c = (a && b) ? cd : ((a != b) ? cm : cl);
                c = Color.Lerp(c, white, Mathf.Clamp01((y - yFade0) / (yFade1 - yFade0)) * 0.14f);
                return Color.Lerp(c, ce, Mathf.Clamp01(1f + d / 3f) * 0.4f);
            });
        }

        public void Done() { }   // контур и Bleed накладывает Render
    }

    // ============================================================ звери

    // ------------------------------------------------------------ sheep
    static void Sheep(Ink k)
    {
        k.P(Ellipse(50f, 13f, 8f, 5f), "#3b3753");
        k.P(Ellipse(80f, 13f, 8f, 5f), "#3b3753");
        var body = SmoothUnion(new[] { Circle(64f, 34f, 21f), Circle(40f, 38f, 14f), Circle(88f, 38f, 14f), Circle(46f, 24f, 13f), Circle(82f, 24f, 13f), Circle(64f, 24f, 16f), Circle(32f, 26f, 9f), Circle(96f, 26f, 9f), Circle(52f, 14f, 8f), Circle(76f, 14f, 8f), Circle(30f, 44f, 8f), Circle(98f, 44f, 8f), Circle(50f, 50f, 10f), Circle(78f, 50f, 10f) }, 2.2f);
        k.P(body, "#ffecc8");
        foreach (float[] t in new[] { new[] { 34f, 32f, 4f }, new[] { 96f, 34f, 4f }, new[] { 74f, 16f, 4f }, new[] { 50f, 16f, 3.5f }, new[] { 88f, 22f, 3f }, new[] { 40f, 22f, 3f } })
        {
            float x = t[0], y = t[1], r = t[2];
            k.Line(Bez(V((x - r), y), V(x, (y + (r * 1.4f))), V((x + r), y), 8f), 1.3f, "#d2b080", 0.85f);
        }
        k.P(Ellipse(30f, 77f, 14f, 8f, 22f), "#ffdcb5");
        k.F(Ellipse(30f, 77f, 9.5f, 4.8f, 22f), "#f6a89c");
        k.P(Ellipse(98f, 77f, 14f, 8f, -22f), "#ffdcb5");
        k.F(Ellipse(98f, 77f, 9.5f, 4.8f, -22f), "#f6a89c");
        k.P(Ellipse(64f, 74f, 30f, 26f), "#ffe9c4");
        var fluff = SmoothUnion(new[] { Circle(64f, 100f, 12f), Circle(52f, 97f, 10f), Circle(76f, 97f, 10f), Circle(44f, 91f, 8f), Circle(84f, 91f, 8f) }, 3f);
        k.P(fluff, "#fff3d6");
        k.F(Ellipse(64f, 82f, 21f, 10f), "#fff8e8", 0.5f);
        k.Eye(51f, 76f, 7.4f);
        k.Eye(77f, 76f, 7.4f);
        k.Blush(41f, 66f, 5f, 3f);
        k.Blush(87f, 66f, 5f, 3f);
        k.Nose(64f, 68f, 3.0f, "#e98a86");
        k.Line(Bez(V(58f, 65f), V(61f, 61.5f), V(64f, 65f)), 1.4f, "#7a3b2e");
        k.Line(Bez(V(64f, 65f), V(67f, 61.5f), V(70f, 65f)), 1.4f, "#7a3b2e");
        k.Line(Bez(V(40f, 54f), V(64f, 46f), V(88f, 54f), 18f), 4.5f, "#8a4f23");
        k.Line(Bez(V(40f, 55.5f), V(64f, 47.5f), V(88f, 55.5f), 18f), 1.2f, "#b8742f", 0.9f);
        k.P(Circle(66f, 44f, 6.2f), "#ffc93a");
        k.F(Rrect(66f, 41.5f, 7f, 1.6f, 0.8f), "#a56a10");
        k.F(Circle(66f, 38f, 1.3f), "#a56a10");
        k.F(Ellipse(64f, 46.5f, 2f, 1.3f, 30f), "#fff4b0");
        k.Done();
    }

    // ------------------------------------------------------------ beaver
    static void Beaver(Ink k)
    {
        // хвост-лопата
        k.Grid(Rrect(24f, 28f, 24f, 32f, 9f, 18f), "#6a3a1a", "#40220d", "#2a1408", 24f, 28f, 18f);
        k.P(Ellipse(48f, 13f, 11f, 6f), "#6b3513");
        k.P(Ellipse(82f, 13f, 11f, 6f), "#6b3513");
        k.P(Ellipse(64f, 38f, 28f, 28f), "#b5651d");
        k.P(Ellipse(64f, 34f, 16f, 19f), "#e2a964");
        k.P(Circle(38f, 98f, 10f), "#a4561a");
        k.F(Circle(38f, 98f, 5.5f), "#e8b27a");
        k.P(Circle(90f, 98f, 10f), "#a4561a");
        k.F(Circle(90f, 98f, 5.5f), "#e8b27a");
        // чубчик
        k.P(SmoothUnion(new[] { Circle(58f, 106f, 7f), Circle(68f, 108f, 7f), Circle(76f, 104f, 6f) }, 2f), "#a4561a");
        k.P(Ellipse(64f, 80f, 31f, 27f), "#b96a20");
        k.P(Ellipse(64f, 70f, 17f, 12.5f), "#f0c68e");
        k.Eye(49f, 82f, 7.4f);
        k.Eye(79f, 82f, 7.4f);
        k.Blush(41f, 71f, 5f, 3f);
        k.Blush(87f, 71f, 5f, 3f);
        k.Nose(64f, 76f, 5.2f);
        k.Line(new[] { V(64f, 72f), V(64f, 69.5f) }, 1.3f, "#4a2a18");
        k.P(Rrect(64f, 64f, 9f, 10.5f, 2.4f), "#fffaf0");
        k.F(Rrect(64f, 64f, 0.9f, 10f, 0.4f), "#c9b79a");
        k.Line(Bez(V(55f, 66f), V(59f, 61f), V(63f, 65f)), 1.3f, "#4a2a18");
        k.Line(Bez(V(65f, 65f), V(69f, 61f), V(73f, 66f)), 1.3f, "#4a2a18");
        // топор: рукоять и головка в локальных координатах вдоль рукояти
        var fr = new Frame(98f, 64f, 20f, 44f);
        k.Line(new[] { V(78f, 22f), fr.O }, 5.2f, "#b9803f");
        k.Line(new[] { V(79f, 24f), fr.W(-1f, -2f) }, 1.4f, "#e3ad66", 0.9f);
        var poll = Poly(new[] { fr.L(-6f, -4f), fr.L(5f, -4f), fr.L(5f, 3f), fr.L(-6f, 3f) }, 1.2f);
        var blade = Poly(new[] { fr.L(-6f, 3f), fr.L(5f, 3f), fr.L(10f, 17f), fr.L(-11f, 17f) }, 1.5f);
        k.G(Union(poll, blade), "#7c8794", "#d3dbe4");
        k.F(Poly(new[] { fr.L(-9f, 14f), fr.L(8f, 14f), fr.L(10f, 17f), fr.L(-11f, 17f) }, 0.6f), "#f4f8fb", 0.9f);
        k.F(Poly(new[] { fr.L(-5f, 4f), fr.L(0f, 4f), fr.L(-1f, 13f), fr.L(-8f, 13f) }, 0.5f), "#ffffff", 0.35f);
        k.F(fr.Rrect(0f, -0.5f, 11f, 3.4f, 1.2f), "#4d5663", 0.9f);
        // лапы на топоре
        k.P(Ellipse(80f, 44f, 8f, 6.5f, 30f), "#7a3d14");
        k.P(Ellipse(90f, 34f, 7.5f, 6f, 30f), "#7a3d14");
        k.Done();
    }

    // ------------------------------------------------------------ rabbit
    static void Rabbit(Ink k)
    {
        k.P(Circle(30f, 32f, 12f), "#fff4e2");
        k.P(Circle(26f, 36f, 7f), "#fffaf0");
        // уши
        k.P(Ellipse(44f, 108f, 10f, 23f, 14f), "#fff0da");
        k.F(Ellipse(45f, 107f, 5.4f, 18f, 14f), "#f7aaa6");
        k.P(Ellipse(86f, 106f, 10f, 26f, -10f), "#fff0da");
        k.F(Ellipse(85.5f, 105f, 5.4f, 20.5f, -10f), "#f7aaa6");
        k.P(Ellipse(64f, 38f, 25f, 29f), "#fff0da");
        k.P(Ellipse(64f, 72f, 28f, 25f), "#fff3df");
        k.F(Ellipse(64f, 80f, 20f, 12f), "#ffffff", 0.5f);
        // сапожки
        foreach (float x in new float[] { 52f, 78f })
        {
            k.P(Rrect(x, 14f, 17f, 13f, 5f), "#b3702f");
            k.P(Rrect(x, 22f, 18f, 7f, 3f), "#7d4a20");
            k.F(Rrect((x - 3f), 14f, 2f, 8f, 1f), "#e0a35e", 0.7f);
            k.P(Circle((x + 6f), 16f, 2.3f), "#ffd45a");
        }
        k.Eye(52f, 75f, 7f);
        k.Eye(76f, 75f, 7f);
        k.Blush(43f, 65f, 5f, 3f);
        k.Blush(85f, 65f, 5f, 3f);
        k.Nose(64f, 68f, 3.3f, "#f08c8c");
        k.Line(new[] { V(64f, 66f), V(64f, 63f) }, 1.3f, "#9a4a46");
        k.Smile(64f, 60f, 6.5f, 3.0f);
        k.Done();
    }

    // ------------------------------------------------------------ badger
    static void Badger(Ink k)
    {
        // рюкзак
        k.P(Rrect(30f, 46f, 20f, 32f, 7f, 6f), "#a8632a");
        k.P(Rrect(25f, 42f, 11f, 14f, 3f, 6f), "#8a4d1e");
        k.P(Rrect(33f, 36f, 5f, 9f, 1.5f, 6f), "#ffcf4a");
        k.P(Ellipse(48f, 14f, 12f, 6f), "#2c2c36");
        k.P(Ellipse(84f, 14f, 12f, 6f), "#2c2c36");
        k.P(Ellipse(66f, 36f, 30f, 28f), "#50505c");
        k.F(Ellipse(70f, 40f, 12f, 16f), "#6a6a78", 0.45f);
        k.Line(new[] { V(40f, 56f), V(50f, 28f) }, 4.5f, "#a8632a");
        k.Line(new[] { V(92f, 56f), V(86f, 28f) }, 4.5f, "#a8632a");
        k.Line(new[] { V(41f, 54f), V(50f, 30f) }, 1.2f, "#d08a45", 0.8f);
        // уши
        k.P(Circle(38f, 100f, 10f), "#2e2e38");
        k.F(Circle(38f, 100f, 5.4f), "#d7d2c6");
        k.P(Circle(94f, 100f, 10f), "#2e2e38");
        k.F(Circle(94f, 100f, 5.4f), "#d7d2c6");
        var head = Ellipse(66f, 78f, 32f, 26f);
        k.P(head, "#f6f1e6");
        // серые полосы
        k.P(Intersect(head, Ellipse(66f, 108f, 38f, 17f)), "#8e8e9a", 1.0f, head);
        k.P(Intersect(head, Ellipse(45f, 80f, 9f, 20f, 10f)), "#2f2f3a", 1.0f, head);
        k.P(Intersect(head, Ellipse(87f, 80f, 9f, 20f, -10f)), "#2f2f3a", 1.0f, head);
        k.F(Rrect(66f, 94f, 9f, 30f, 4f), "#fbf7ee");
        k.Eye(50f, 78f, 7.2f);
        k.Eye(82f, 78f, 7.2f);
        k.P(Ellipse(66f, 66f, 16f, 11f), "#fbf7ee");
        k.Blush(44f, 68f, 4.5f, 2.6f);
        k.Blush(88f, 68f, 4.5f, 2.6f);
        k.Nose(66f, 70f, 4.6f, "#26262e");
        k.Smile(66f, 61f, 6f, 2.6f, false);
        k.Done();
    }

    // ------------------------------------------------------------ magpie
    static void Magpie(Ink k)
    {
        k.P(Poly(new[] { V(46f, 26f), V(18f, 2f), V(28f, -2f), V(56f, 20f) }, 3f), "#1d3a78");
        k.P(Poly(new[] { V(40f, 32f), V(10f, 12f), V(18f, 4f), V(48f, 24f) }, 2.5f), "#2a4d9a");
        foreach (float x in new float[] { 56f, 70f })
        {
            k.Line(new[] { V(x, 18f), V(x, 8f) }, 3f, "#2c2c3a");
            k.Line(new[] { V((x - 6f), 5f), V(x, 8f), V((x + 6f), 5f) }, 3f, "#2c2c3a");
        }
        k.P(Ellipse(58f, 42f, 25f, 33f, 8f), "#1d2a4d");
        k.P(Ellipse(64f, 36f, 16f, 25f, 8f), "#fbf4e4");
        k.P(Ellipse(44f, 42f, 13f, 27f, 14f), "#1f3f87");
        k.F(Ellipse(42f, 50f, 5f, 15f, 14f), "#5b8ae0", 0.6f);
        k.F(Ellipse(49f, 26f, 4f, 11f, 18f), "#0f1d44", 0.6f);
        k.P(Circle(56f, 82f, 23f), "#1d2a4d");
        k.F(Ellipse(47f, 95f, 9f, 3.6f, 28f), "#7f98d6", 0.55f);
        k.Eye(60f, 87f, 7.2f, V(1f, 0f));
        k.P(Poly(new[] { V(72f, 83f), V(96f, 77f), V(72f, 72f) }, 2.5f), "#7e8794");
        // два бревна в клюве
        k.P(Rrect(84f, 72f, 46f, 11f, 5f, 6f), "#a9743f");
        k.F(Rrect(84f, 75f, 40f, 2f, 1f, 6f), "#d2a063", 0.8f);
        k.P(Rrect(84f, 61f, 46f, 11f, 5f, -4f), "#c28a4e");
        k.F(Rrect(84f, 64f, 40f, 2f, 1f, -4f), "#e0b27a", 0.8f);
        k.P(Circle(106f, 72f, 5.8f), "#ecc995");
        k.F(Circle(106f, 72f, 3.2f), "#d3a566");
        k.F(Circle(106f, 72f, 1.1f), "#ecc995");
        k.P(Circle(106f, 60f, 5.8f), "#ecc995");
        k.F(Circle(106f, 60f, 3.2f), "#d3a566");
        k.F(Circle(106f, 60f, 1.1f), "#ecc995");
        k.Done();
    }

    // ------------------------------------------------------------ raccoon
    static void Raccoon(Ink k)
    {
        // полосатый хвост
        k.Bands(Ellipse(28f, 36f, 13f, 28f, 24f), "#857466", "#3d3634", "#1c1614", 28f, 36f, 24f);
        k.P(Ellipse(48f, 13f, 11f, 6f), "#3d3634");
        k.P(Ellipse(82f, 13f, 11f, 6f), "#3d3634");
        k.P(Ellipse(64f, 36f, 28f, 26f), "#8b7b6d");
        k.P(Ellipse(64f, 32f, 16f, 18f), "#b8a898");
        // уши
        k.P(Ellipse(38f, 100f, 11f, 13f, 28f), "#a39383");
        k.F(Ellipse(38f, 99f, 6f, 8f, 28f), "#f0b4a8");
        k.P(Ellipse(92f, 100f, 11f, 13f, -28f), "#a39383");
        k.F(Ellipse(92f, 99f, 6f, 8f, -28f), "#f0b4a8");
        k.P(Ellipse(64f, 80f, 35f, 25f), "#a99989");
        k.P(Ellipse(64f, 70f, 24f, 14f), "#f6efe4");
        k.F(Poly(new[] { V(64f, 96f), V(57f, 84f), V(64f, 78f), V(71f, 84f) }, 1.5f), "#f6efe4");
        // маска
        k.P(Ellipse(48f, 82f, 14f, 9.5f, 14f), "#4c433e");
        k.P(Ellipse(80f, 82f, 14f, 9.5f, -14f), "#4c433e");
        k.Eye(48f, 82f, 6.6f);
        k.Eye(80f, 82f, 6.6f);
        k.Blush(40f, 70f, 4.5f, 2.6f);
        k.Blush(88f, 70f, 4.5f, 2.6f);
        k.Nose(64f, 73f, 4.6f, "#2a2224");
        k.Smile(64f, 64f, 6.5f, 3.2f);
        // мешок с золотом
        k.P(Ellipse(78f, 27f, 19f, 16f), "#a8632a");
        k.P(Ellipse(78f, 38f, 12f, 5f), "#7d4a20");
        k.Line(new[] { V(74f, 32f), V(74f, 20f) }, 1.6f, "#e8b04a");
        k.P(Circle(74f, 19f, 2.4f), "#ffcf4a");
        k.P(Circle(80f, 22f, 2.2f), "#ffcf4a");
        foreach (float[] t in new[] { new[] { 70f, 44f }, new[] { 79f, 46f }, new[] { 88f, 43f }, new[] { 75f, 40f }, new[] { 84f, 40f } })
        {
            float x = t[0], y = t[1];
            k.P(Circle(x, y, 5.2f), "#ffcb3a");
            k.F(Circle(x, y, 3.2f), "#e8a317");
            k.F(Ellipse((x - 1.2f), (y + 1.2f), 1.5f, 0.9f, 30f), "#fff4b0");
        }
        k.P(Ellipse(61f, 30f, 7f, 6f), "#4c433e");
        k.P(Ellipse(97f, 28f, 6.5f, 6f), "#4c433e");
        k.Done();
    }

    // ------------------------------------------------------------ woodpecker
    static void Woodpecker(Ink k)
    {
        k.P(Poly(new[] { V(50f, 24f), V(26f, 2f), V(42f, -2f), V(66f, 20f) }, 3f), "#252535");
        foreach (float x in new float[] { 60f, 74f })
        {
            k.Line(new[] { V(x, 18f), V(x, 8f) }, 3f, "#2c2c3a");
            k.Line(new[] { V((x - 6f), 5f), V(x, 8f), V((x + 6f), 5f) }, 3f, "#2c2c3a");
        }
        k.P(Ellipse(58f, 40f, 24f, 32f, 10f), "#f8f0e0");
        k.P(Ellipse(54f, 16f, 20f, 10f, 10f), "#e3382d");
        k.P(Ellipse(63f, 38f, 14f, 20f, 10f), "#fffaf0");
        k.P(Ellipse(46f, 42f, 14f, 29f, 16f), "#232333");
        foreach (float[] t in new[] { new[] { 42f, 56f, 3.6f }, new[] { 47f, 46f, 3.4f }, new[] { 40f, 40f, 3.2f }, new[] { 46f, 33f, 3f }, new[] { 43f, 25f, 2.8f } })
        {
            float x = t[0], y = t[1], r = t[2];
            k.F(Ellipse(x, y, (r * 1.3f), r, 40f), "#ffffff", 0.95f);
        }
        k.P(Circle(62f, 78f, 23f), "#fbf4e6");
        k.P(Ellipse(47f, 82f, 7f, 13f, 14f), "#e3382d");
        k.P(Ellipse(63f, 66f, 14f, 4.2f, -8f), "#1e1e2c");
        k.Eye(65f, 79f, 7.2f, V(0.5f, 0f));
        k.P(Poly(new[] { V(78f, 80f), V(116f, 74f), V(78f, 67f) }, 2f), "#8f9aa6");
        k.F(Poly(new[] { V(80f, 77f), V(110f, 74f), V(80f, 72f) }, 1f), "#cfd8e0", 0.9f);
        k.F(Poly(new[] { V(78f, 72f), V(112f, 74f), V(116f, 74f), V(78f, 67f) }, 0.6f), "#5d6773", 0.5f);
        var dome = Intersect(Ellipse(62f, 91f, 24f, 21f), Rrect(62f, 112f, 70f, 42f, 0f));
        k.P(dome, "#ffc61a");
        k.P(Rrect(64f, 91f, 58f, 6.5f, 3.2f, 3f), "#f0a800");
        k.F(Rrect(62f, 106f, 6f, 14f, 2.6f), "#ffe27a", 0.85f);
        k.F(Ellipse(50f, 101f, 6f, 3f, 30f), "#fff3b0", 0.75f);
        k.Done();
    }

    // ------------------------------------------------------------ bear
    static void Bear(Ink k)
    {
        k.P(Ellipse(46f, 12f, 11f, 6f), "#a85f22");
        k.P(Ellipse(82f, 12f, 11f, 6f), "#a85f22");
        k.P(Ellipse(64f, 36f, 34f, 30f), "#b8692a");
        k.P(Ellipse(64f, 32f, 20f, 22f), "#d99a58");
        k.P(Circle(34f, 96f, 11f), "#a85f22");
        k.F(Circle(34f, 96f, 6f), "#e1a569");
        k.P(Circle(94f, 96f, 11f), "#a85f22");
        k.F(Circle(94f, 96f, 6f), "#e1a569");
        k.P(Ellipse(64f, 72f, 35f, 29f), "#bd6e2d");
        k.P(Ellipse(64f, 63f, 17f, 13f), "#efc088");
        k.Eye(49f, 73f, 7.6f);
        k.Eye(79f, 73f, 7.6f);
        k.Blush(40f, 62f, 5f, 3f);
        k.Blush(88f, 62f, 5f, 3f);
        k.Nose(64f, 68f, 5.0f);
        k.Line(new[] { V(64f, 65f), V(64f, 62f) }, 1.3f, "#3a2418");
        k.Smile(64f, 57.5f, 6.5f, 3.2f);
        // кепка в клетку: купол только выше линии лба
        var dome = Intersect(Ellipse(64f, 89f, 30f, 24f), Rrect(64f, 110f, 80f, 42f, 0f));
        k.Plaid(dome, "#7d1620", "#c42a33", "#e9505a", "#2a0a0e", 6f, 90f, 114f);
        k.P(Ellipse(74f, 89.5f, 31f, 5f, -2f), "#8a1a24");
        k.P(Rrect(64f, 101f, 13f, 15f, 3.2f), "#d9c08a");
        k.F(Poly(new[] { V(64f, 109f), V(57.5f, 99f), V(70.5f, 99f) }, 0.6f), "#2f7a3e");
        k.F(Poly(new[] { V(64f, 104f), V(56.5f, 95.5f), V(71.5f, 95.5f) }, 0.6f), "#3b9a4c");
        k.F(Rrect(64f, 94.5f, 2f, 4f, 0.5f), "#6b3d18");
        k.Done();
    }

    // ------------------------------------------------------------ chicken
    static void Chicken(Ink k)
    {
        foreach (float x in new float[] { 56f, 72f })
        {
            k.Line(new[] { V(x, 18f), V(x, 8f) }, 3f, "#ff9a1a");
            k.Line(new[] { V((x - 6f), 6f), V(x, 8f), V((x + 6f), 6f) }, 3f, "#ff9a1a");
        }
        k.P(Ellipse(30f, 48f, 9f, 16f, 28f), "#f1dcb8");
        k.P(Ellipse(34f, 54f, 8f, 14f, 12f), "#fbeccd");
        k.P(Ellipse(64f, 40f, 30f, 30f), "#fff6e6");
        k.P(Ellipse(64f, 34f, 17f, 19f), "#ffe9c4");
        k.P(Ellipse(37f, 42f, 10f, 18f, 14f), "#f4dfba");
        k.F(Ellipse(36f, 36f, 4f, 9f, 14f), "#d9b985", 0.6f);
        k.P(Ellipse(91f, 42f, 10f, 18f, -14f), "#f4dfba");
        k.F(Ellipse(92f, 36f, 4f, 9f, -14f), "#d9b985", 0.6f);
        k.P(Ellipse(64f, 80f, 27f, 25f), "#fff8ea");
        var comb = SmoothUnion(new[] { Circle(54f, 104f, 6.5f), Circle(64f, 109f, 7.5f), Circle(74f, 104f, 6.5f) }, 2.5f);
        k.P(comb, "#e3382d");
        k.F(Ellipse(60f, 110f, 2.5f, 1.6f, 30f), "#ff9a90", 0.8f);
        k.Eye(53f, 83f, 7f);
        k.Eye(75f, 83f, 7f);
        k.Blush(42f, 73f, 5f, 3f);
        k.Blush(86f, 73f, 5f, 3f);
        k.P(Poly(new[] { V(57f, 77f), V(71f, 77f), V(64f, 65f) }, 2.6f), "#ffa31a");
        k.F(Poly(new[] { V(60f, 76f), V(68f, 76f), V(64f, 71f) }, 0.8f), "#ffd27a", 0.8f);
        k.P(Ellipse(64f, 60f, 3.6f, 5.6f), "#e3382d");
        k.P(Ellipse(90f, 20f, 8f, 10f), "#fff6e6");
        k.F(Ellipse(88f, 23f, 3f, 4f, 20f), "#ffffff", 0.7f);
        k.Done();
    }

    // ------------------------------------------------------------ pig
    static void Pig(Ink k)
    {
        k.P(Ellipse(48f, 13f, 10f, 6f), "#e0788c");
        k.P(Ellipse(82f, 13f, 10f, 6f), "#e0788c");
        k.Line(Bez(V(34f, 36f), V(18f, 40f), V(24f, 26f), 12f), 4f, "#f08ca0");
        k.Line(Bez(V(24f, 26f), V(30f, 18f), V(38f, 24f), 10f), 4f, "#f08ca0");
        k.P(Ellipse(64f, 38f, 30f, 28f), "#ffb3bf");
        k.P(Ellipse(64f, 34f, 17f, 19f), "#ffd2d9");
        k.P(Poly(new[] { V(36f, 94f), V(28f, 112f), V(52f, 102f) }, 4f), "#ff9fae");
        k.F(Poly(new[] { V(37f, 97f), V(33f, 106f), V(47f, 102f) }, 2f), "#f27a90");
        k.P(Poly(new[] { V(92f, 94f), V(100f, 112f), V(76f, 102f) }, 4f), "#ff9fae");
        k.F(Poly(new[] { V(91f, 97f), V(95f, 106f), V(81f, 102f) }, 2f), "#f27a90");
        k.P(Ellipse(64f, 76f, 33f, 27f), "#ffb3bf");
        k.Eye(48f, 82f, 7.2f);
        k.Eye(80f, 82f, 7.2f);
        k.Blush(38f, 70f, 5f, 3f);
        k.Blush(90f, 70f, 5f, 3f);
        k.P(Ellipse(64f, 68f, 14f, 10.5f), "#ff8fa3");
        k.F(Ellipse(58.5f, 68f, 2.2f, 3.2f), "#8c2f45");
        k.F(Ellipse(69.5f, 68f, 2.2f, 3.2f), "#8c2f45");
        k.F(Ellipse(60f, 72f, 3f, 1.4f, 20f), "#ffd2d9", 0.7f);
        k.Smile(64f, 57f, 5.5f, 2.4f, false);
        k.P(Circle(92f, 30f, 12f), "#ffc93a");
        k.F(Circle(92f, 30f, 8.5f), "#e8a317");
        k.F(Circle(92f, 30f, 6.5f), "#ffd95a");
        k.F(Rrect(92f, 30f, 3f, 9f, 1f), "#e8a317");
        k.F(Ellipse(88f, 35f, 3f, 1.6f, 40f), "#fff4b0");
        k.P(Ellipse(80f, 26f, 7f, 6.5f, 20f), "#ff9fae");
        k.Done();
    }

    // ------------------------------------------------------------ dog
    static void Dog(Ink k)
    {
        k.P(Ellipse(100f, 30f, 7f, 14f, -28f), "#c78a4e");
        k.P(Ellipse(48f, 13f, 11f, 6f), "#b87a3e");
        k.P(Ellipse(80f, 13f, 11f, 6f), "#b87a3e");
        k.P(Ellipse(64f, 36f, 28f, 27f), "#dba267");
        k.P(Ellipse(64f, 30f, 16f, 18f), "#fff0d8");
        k.P(Ellipse(34f, 76f, 10f, 21f, 12f), "#8a5426");
        k.P(Ellipse(94f, 76f, 10f, 21f, -12f), "#8a5426");
        k.P(Ellipse(64f, 78f, 29f, 25f), "#e2aa70");
        k.P(Ellipse(51f, 84f, 11f, 10f, 20f), "#a5662d");
        k.F(Rrect(64f, 84f, 6f, 20f, 3f), "#fff0d8");
        k.Eye(52f, 82f, 7.2f);
        k.Eye(76f, 82f, 7.2f);
        k.P(Ellipse(64f, 66f, 16f, 12f), "#fff0d8");
        k.Nose(64f, 71f, 5.6f);
        k.Line(new[] { V(64f, 68f), V(64f, 65f) }, 1.3f, "#4a2a18");
        k.Smile(64f, 58.5f, 7f, 3.4f);
        k.P(Ellipse(66f, 52.5f, 3.6f, 5f), "#f26f84");
        k.Blush(43f, 66f, 4.5f, 2.6f);
        k.Blush(85f, 66f, 4.5f, 2.6f);
        // красная бандана
        k.P(Poly(new[] { V(40f, 58f), V(88f, 58f), V(64f, 34f) }, 4f), "#e3382d");
        k.F(Circle(58f, 52f, 1.8f), "#ffffff", 0.9f);
        k.F(Circle(70f, 52f, 1.8f), "#ffffff", 0.9f);
        k.F(Circle(64f, 44f, 1.8f), "#ffffff", 0.9f);
        k.P(Rrect(64f, 58f, 52f, 5f, 2.5f), "#c42a33");
        k.Done();
    }

    // ------------------------------------------------------------ cat
    static void Cat(Ink k)
    {
        k.Line(Bez(V(88f, 22f), V(118f, 22f), V(106f, 56f), 14f), 8f, "#f0a23c");
        k.Line(Bez(V(103f, 40f), V(108f, 46f), V(105f, 52f), 6f), 8f, "#c06a1c");
        k.P(Ellipse(48f, 13f, 11f, 6f), "#d98a2a");
        k.P(Ellipse(80f, 13f, 11f, 6f), "#d98a2a");
        k.P(Ellipse(64f, 36f, 27f, 27f), "#f0a23c");
        k.P(Ellipse(64f, 30f, 15f, 18f), "#fff0d8");
        k.P(Poly(new[] { V(34f, 90f), V(36f, 116f), V(58f, 98f) }, 4f), "#f0a23c");
        k.F(Poly(new[] { V(38f, 95f), V(39f, 108f), V(50f, 99f) }, 2f), "#f6a3a8");
        k.P(Poly(new[] { V(94f, 90f), V(92f, 116f), V(70f, 98f) }, 4f), "#f0a23c");
        k.F(Poly(new[] { V(90f, 95f), V(89f, 108f), V(78f, 99f) }, 2f), "#f6a3a8");
        k.P(Ellipse(64f, 76f, 31f, 26f), "#f4aa44");
        foreach (float x in new float[] { 52f, 64f, 76f })
        {
            k.F(Rrect(x, 96f, 3.4f, 10f, 1.6f), "#c06a1c", 0.85f);
        }
        k.Eye(50f, 78f, 7.4f);
        k.Eye(78f, 78f, 7.4f);
        k.Blush(40f, 67f, 5f, 3f);
        k.Blush(88f, 67f, 5f, 3f);
        k.P(Ellipse(64f, 66f, 12f, 8.5f), "#fff0d8");
        k.Nose(64f, 70f, 3.2f, "#f06c80");
        k.Line(Bez(V(58f, 65f), V(61f, 61.5f), V(64f, 65f)), 1.4f, "#6a3a22");
        k.Line(Bez(V(64f, 65f), V(67f, 61.5f), V(70f, 65f)), 1.4f, "#6a3a22");
        k.Line(new[] { V(30f, 68f), V(14f, 70f) }, 1.1f, "#fff6e6", 0.9f);
        k.Line(new[] { V(30f, 64f), V(15f, 62f) }, 1.1f, "#fff6e6", 0.9f);
        k.Line(new[] { V(98f, 68f), V(114f, 70f) }, 1.1f, "#fff6e6", 0.9f);
        k.Line(new[] { V(98f, 64f), V(113f, 62f) }, 1.1f, "#fff6e6", 0.9f);
        // пила в лапах
        var fr = new Frame(14f, 30f, 10f, 3f);
        k.P(Rrect(10f, 30f, 12f, 7f, 3f, 16f), "#a9743f");
        k.G(Poly(new[] { fr.L(4f, 3f), fr.L(36f, 3f), fr.L(36f, -1f), fr.L(33f, -3f), fr.L(30f, -1f), fr.L(27f, -3f), fr.L(24f, -1f), fr.L(21f, -3f), fr.L(18f, -1f), fr.L(15f, -3f), fr.L(12f, -1f), fr.L(9f, -3f), fr.L(6f, -1f), fr.L(4f, -1f) }, 0.5f), "#8e99a6", "#dfe6ee");
        k.F(Poly(new[] { fr.L(6f, 2.4f), fr.L(34f, 2.4f), fr.L(34f, 1.2f), fr.L(6f, 1.2f) }, 0.3f), "#ffffff", 0.55f);
        k.P(Ellipse(34f, 38f, 7f, 6f, 30f), "#f0a23c");
        k.Done();
    }

    // ------------------------------------------------------------ horse
    static void Horse(Ink k)
    {
        k.P(SmoothUnion(new[] { Ellipse(102f, 34f, 7f, 14f, -12f), Circle(106f, 22f, 7f) }, 3f), "#3b2210");
        k.P(Ellipse(48f, 13f, 11f, 6f), "#2f2a30");
        k.P(Ellipse(80f, 13f, 11f, 6f), "#2f2a30");
        k.P(Ellipse(64f, 34f, 29f, 26f), "#b9753a");
        k.P(Ellipse(64f, 30f, 16f, 17f), "#d79b5c");
        k.P(Poly(new[] { V(42f, 94f), V(40f, 112f), V(54f, 100f) }, 3f), "#b9753a");
        k.F(Poly(new[] { V(43f, 98f), V(43f, 107f), V(50f, 101f) }, 1.5f), "#8a4f22");
        k.P(Poly(new[] { V(86f, 94f), V(88f, 112f), V(74f, 100f) }, 3f), "#b9753a");
        k.F(Poly(new[] { V(85f, 98f), V(85f, 107f), V(78f, 101f) }, 1.5f), "#8a4f22");
        k.P(SmoothUnion(new[] { Circle(40f, 92f, 7f), Circle(35f, 80f, 6.5f), Circle(36f, 68f, 6f), Circle(40f, 58f, 5f) }, 2.5f), "#3b2210");
        k.P(Ellipse(64f, 80f, 27f, 26f), "#c88344");
        k.P(Rrect(64f, 84f, 10f, 30f, 5f), "#fff3e0");
        k.Eye(50f, 82f, 7.2f);
        k.Eye(78f, 82f, 7.2f);
        k.P(Ellipse(64f, 62f, 19f, 15f), "#e7bb86");
        k.F(Ellipse(57.5f, 62f, 2.4f, 3.2f), "#5a2f1c");
        k.F(Ellipse(70.5f, 62f, 2.4f, 3.2f), "#5a2f1c");
        k.Smile(64f, 54f, 5.5f, 2.4f, false);
        k.Blush(41f, 70f, 5f, 3f);
        k.Blush(87f, 70f, 5f, 3f);
        k.P(SmoothUnion(new[] { Circle(56f, 100f, 7f), Circle(66f, 102f, 7.5f), Circle(75f, 99f, 6f) }, 3f), "#3b2210");
        k.F(Ellipse(58f, 104f, 4f, 1.8f, 20f), "#6a4020", 0.8f);
        // подкова на удачу
        k.Line(Bez(V(14f, 46f), V(26f, 8f), V(40f, 46f), 14f), 5.5f, "#ffcf4a");
        k.Line(Bez(V(16f, 44f), V(26f, 14f), V(38f, 44f), 14f), 1.4f, "#fff1a8", 0.8f);
        k.F(Circle(19f, 32f, 1.3f), "#a56a10");
        k.F(Circle(26f, 20f, 1.3f), "#a56a10");
        k.F(Circle(34f, 32f, 1.3f), "#a56a10");
        k.Done();
    }
}
