// Assets/_Project/Scripts/UI/Pets/PetUiIcons.cs (НЕ в Editor)
// Иконки окна питомцев, нарисованные кодом в стиле UiKit: цветные фигуры с градиентом и тёмным контуром. Картинок в проекте не нужно.
//   PetIcons.Get(PetIco.Wand)   спрайт 192x192 с прозрачным фоном (рисуется при первом обращении и кэшируется)
// Иконки: Sort (сортировка), Filter (воронка), Wand (подобрать лучших), Merge (слияние), Release (отпустить),
//         Save (сохранить набор), Swap (заменить), Plus (взять с собой), Minus (снять).
// Заменить любую своей картинкой: PetIcons.SetOverride(PetIco.Wand, мойСпрайт).
using System;
using System.Collections.Generic;
using UnityEngine;
using Sdf = System.Func<float, float, float>;
using Shade = System.Func<float, float, UnityEngine.Color>;

public enum PetIco { Sort, Filter, Wand, Merge, Release, Save, Swap, Plus, Minus }

public static class PetIcons
{
    const int D = 64;                  // дизайн-единиц на сторону
    const int Q = 3;                   // пикселей на единицу
    const int W = D * Q;

    static readonly Dictionary<PetIco, Sprite> cache = new Dictionary<PetIco, Sprite>();
    static readonly Dictionary<PetIco, Sprite> overrides = new Dictionary<PetIco, Sprite>();
    static readonly Color Ink = new Color32(0x0B, 0x13, 0x20, 255);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { cache.Clear(); overrides.Clear(); }

    public static void SetOverride(PetIco ico, Sprite s) { if (s == null) overrides.Remove(ico); else overrides[ico] = s; }

    public static Sprite Get(PetIco ico)
    {
        Sprite s;
        if (overrides.TryGetValue(ico, out s) && s != null) return s;
        if (cache.TryGetValue(ico, out s) && s != null) return s;
        s = Build(ico);
        cache[ico] = s;
        return s;
    }

    // ------------------------------------------------------------ цвета и заливки
    static Color C(int r, int g, int b, int a = 255) { return new Color32((byte)r, (byte)g, (byte)b, (byte)a); }
    static Shade Solid(Color c) { return (x, y) => c; }
    static Shade VGrad(float y0, float y1, Color a, Color b) { return (x, y) => Color.Lerp(a, b, Mathf.InverseLerp(y0, y1, y)); }

    static readonly Color BrassLo = new Color32(0xE0, 0x9A, 0x14, 255), BrassHi = new Color32(0xFF, 0xDC, 0x55, 255);
    static readonly Color SteelLo = new Color32(0x9C, 0xB4, 0xD0, 255);

    // ------------------------------------------------------------ фигуры (расстояние до края: внутри меньше нуля)
    static Sdf Circle(float cx, float cy, float r) { return (x, y) => Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r; }

    static Sdf RR(float x0, float y0, float w, float h, float r)
    {
        float cx = x0 + w * 0.5f, cy = y0 + h * 0.5f, hx = w * 0.5f - r, hy = h * 0.5f - r;
        return (px, py) =>
        {
            float dx = Mathf.Abs(px - cx) - hx, dy = Mathf.Abs(py - cy) - hy;
            float ox = Mathf.Max(dx, 0f), oy = Mathf.Max(dy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(dx, dy), 0f) - r;
        };
    }

    static Sdf Capsule(float ax, float ay, float bx, float by, float r)
    {
        return (px, py) =>
        {
            float pax = px - ax, pay = py - ay, bax = bx - ax, bay = by - ay;
            float h = Mathf.Clamp01((pax * bax + pay * bay) / (bax * bax + bay * bay));
            float dx = pax - bax * h, dy = pay - bay * h;
            return Mathf.Sqrt(dx * dx + dy * dy) - r;
        };
    }

    static Sdf Poly(params Vector2[] v)
    {
        return (px, py) =>
        {
            float d = float.MaxValue;
            bool inside = false;
            for (int i = 0, j = v.Length - 1; i < v.Length; j = i++)
            {
                Vector2 a = v[i], b = v[j];
                float ex = b.x - a.x, ey = b.y - a.y, wx = px - a.x, wy = py - a.y;
                float t = Mathf.Clamp01((wx * ex + wy * ey) / (ex * ex + ey * ey));
                float dx = wx - ex * t, dy = wy - ey * t;
                d = Mathf.Min(d, dx * dx + dy * dy);
                if (((a.y > py) != (b.y > py)) && (px < (b.x - a.x) * (py - a.y) / (b.y - a.y) + a.x)) inside = !inside;
            }
            d = Mathf.Sqrt(d);
            return inside ? -d : d;
        };
    }

    static Vector2 V(float x, float y) { return new Vector2(x, y); }

    static Sdf Star(float cx, float cy, float ro, float ri, int n)
    {
        var pts = new Vector2[n * 2];
        for (int i = 0; i < pts.Length; i++)
        {
            float a = Mathf.PI * 0.5f + i * Mathf.PI / n;
            float r = (i % 2 == 0) ? ro : ri;
            pts[i] = V(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r);
        }
        return Poly(pts);
    }

    static Sdf Sub(Sdf a, Sdf b) { return (x, y) => Mathf.Max(a(x, y), -b(x, y)); }
    static Sdf Isect(Sdf a, Sdf b) { return (x, y) => Mathf.Max(a(x, y), b(x, y)); }

    // ------------------------------------------------------------ холст
    sealed class Painter
    {
        public readonly Color[] px = new Color[W * W];

        public void Fill(Sdf f, Shade c)
        {
            float inv = 1f / Q;
            for (int y = 0; y < W; y++)
            {
                float dy = (y + 0.5f) * inv;
                for (int x = 0; x < W; x++)
                {
                    float dx = (x + 0.5f) * inv;
                    float cov = Mathf.Clamp01(0.5f - f(dx, dy) * Q);
                    if (cov <= 0f) continue;
                    Color s = c(dx, dy);
                    s.a *= cov;
                    int i = y * W + x;
                    px[i] = Over(s, px[i]);
                }
            }
        }

        public void Fill(Sdf f, Color c) { Fill(f, Solid(c)); }

        // тёмный контур вокруг нарисованного, лежит под рисунком
        public void Outline(float t, Color c)
        {
            int r = Mathf.CeilToInt(t * Q);
            var a = new float[px.Length];
            for (int i = 0; i < a.Length; i++) a[i] = px[i].a;
            for (int y = 0; y < W; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    if (a[i] >= 0.999f) continue;
                    float m = a[i];
                    for (int oy = -r; oy <= r && m < 0.999f; oy++)
                    {
                        int yy = y + oy;
                        if (yy < 0 || yy >= W) continue;
                        for (int ox = -r; ox <= r; ox++)
                        {
                            int xx = x + ox;
                            if (xx < 0 || xx >= W) continue;
                            float d = Mathf.Sqrt(ox * ox + oy * oy);
                            if (d > r + 0.5f) continue;
                            float v = a[yy * W + xx] * Mathf.Clamp01(r + 0.5f - d);
                            if (v > m) m = v;
                        }
                    }
                    if (m <= 0f) continue;
                    px[i] = Over(px[i], new Color(c.r, c.g, c.b, m));
                }
            }
        }
    }

    // s поверх d (обычная прозрачность)
    static Color Over(Color s, Color d)
    {
        float a = s.a + d.a * (1f - s.a);
        if (a <= 0f) return new Color(0f, 0f, 0f, 0f);
        float k = d.a * (1f - s.a);
        return new Color((s.r * s.a + d.r * k) / a, (s.g * s.a + d.g * k) / a, (s.b * s.a + d.b * k) / a, a);
    }

    static Sprite Build(PetIco ico)
    {
        var p = new Painter();
        Draw(p, ico);
        p.Outline(2f, Ink);
        var tex = new Texture2D(W, W, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.SetPixels(p.px);
        tex.Apply(false, false);
        var sp = Sprite.Create(tex, new Rect(0, 0, W, W), new Vector2(0.5f, 0.5f), 100f);
        sp.name = "PetIco_" + ico;
        return sp;
    }

    static void Draw(Painter p, PetIco ico)
    {
        Color white = Color.white;
        Color brassLo = BrassLo, brassHi = BrassHi;
        switch (ico)
        {
            case PetIco.Sort:
            {
                Color lo = C(0xCF, 0xD8, 0xE6), hi = white;
                p.Fill(RR(7, 44, 34, 9, 4.5f), VGrad(44, 53, lo, hi));
                p.Fill(RR(7, 29, 26, 9, 4.5f), VGrad(29, 38, lo, hi));
                p.Fill(RR(7, 14, 18, 9, 4.5f), VGrad(14, 23, lo, hi));
                p.Fill(Capsule(50, 50, 50, 24, 3.6f), VGrad(14, 54, brassLo, brassHi));
                p.Fill(Poly(V(39, 29), V(61, 29), V(50, 10)), VGrad(10, 29, brassLo, brassHi));
                break;
            }
            case PetIco.Filter:
            {
                p.Fill(Poly(V(5, 55), V(59, 55), V(38, 30), V(38, 8), V(26, 14), V(26, 30)), VGrad(8, 55, SteelLo, white));
                p.Fill(RR(9, 50, 46, 3.4f, 1.7f), new Color(1f, 1f, 1f, 0.65f));
                p.Fill(Poly(V(38, 30), V(38, 8), V(26, 14), V(26, 30)), new Color(0f, 0f, 0f, 0.10f));
                break;
            }
            case PetIco.Wand:
            {
                p.Fill(Capsule(11, 10, 37, 36, 4.8f), VGrad(10, 36, C(0x8A, 0x5A, 0x34), C(0xC9, 0x8A, 0x4B)));
                p.Fill(Capsule(30, 29, 38, 37, 5.2f), VGrad(29, 37, brassLo, brassHi));
                p.Fill(Star(46, 46, 15, 5.5f, 4), VGrad(31, 61, brassLo, brassHi));
                p.Fill(Star(46, 46, 8, 3f, 4), new Color(1f, 1f, 1f, 0.55f));
                p.Fill(Star(15, 50, 7, 2.6f, 4), VGrad(43, 57, C(0xCF, 0xE8, 0xFF), white));
                p.Fill(Star(54, 17, 6, 2.2f, 4), VGrad(11, 23, C(0xCF, 0xE8, 0xFF), white));
                break;
            }
            case PetIco.Merge:
            {
                p.Fill(Circle(23, 32, 19), VGrad(13, 51, C(0x2F, 0x78, 0xB8), C(0x6C, 0xC0, 0xFF)));
                p.Fill(Circle(41, 32, 19), VGrad(13, 51, C(0x3F, 0x8F, 0x47), C(0x8F, 0xE0, 0x8A)));
                p.Fill(Isect(Circle(23, 32, 19), Circle(41, 32, 19)), new Color(1f, 1f, 1f, 0.45f));
                p.Fill(Star(32, 32, 12, 5, 5), VGrad(20, 44, brassLo, brassHi));
                break;
            }
            case PetIco.Release:
            {
                p.Fill(Sub(RR(6, 6, 42, 34, 7), RR(12, 12, 30, 34, 3)), VGrad(6, 40, C(0xB8, 0xC4, 0xD6), white));
                Color o0 = C(0xE8, 0x7A, 0x2E), o1 = C(0xFF, 0xC0, 0x6B);
                p.Fill(Capsule(26, 26, 46, 46, 3.8f), VGrad(26, 46, o0, o1));
                p.Fill(Poly(V(36, 58), V(60, 58), V(60, 34)), VGrad(34, 58, o0, o1));
                break;
            }
            case PetIco.Save:
            {
                p.Fill(RR(7, 7, 50, 50, 7), VGrad(7, 57, C(0x2F, 0x78, 0xB8), C(0x4D, 0xA9, 0xF0)));
                p.Fill(RR(17, 36, 30, 21, 2.5f), VGrad(36, 57, C(0xDD, 0xE6, 0xF2), white));
                p.Fill(RR(36, 41, 6, 12, 1.5f), C(0x2F, 0x78, 0xB8, 190));
                p.Fill(RR(15, 10, 34, 19, 3.5f), VGrad(10, 29, C(0xB8, 0xC8, 0xDC), white));
                p.Fill(RR(20, 15, 24, 2.4f, 1.2f), C(0x2F, 0x78, 0xB8, 150));
                p.Fill(RR(20, 21, 18, 2.4f, 1.2f), C(0x2F, 0x78, 0xB8, 150));
                break;
            }
            case PetIco.Swap:
            {
                p.Fill(Capsule(9, 45, 40, 45, 3.6f), VGrad(35, 55, brassLo, brassHi));
                p.Fill(Poly(V(38, 57), V(38, 33), V(60, 45)), VGrad(33, 57, brassLo, brassHi));
                Color s0 = C(0xB8, 0xC4, 0xD6);
                p.Fill(Capsule(24, 19, 55, 19, 3.6f), VGrad(9, 29, s0, white));
                p.Fill(Poly(V(26, 31), V(26, 7), V(4, 19)), VGrad(7, 31, s0, white));
                break;
            }
            case PetIco.Plus:
            {
                p.Fill(Circle(32, 32, 26), VGrad(6, 58, C(0x3F, 0x8F, 0x47), C(0x8F, 0xE0, 0x8A)));
                p.Fill(RR(15, 28, 34, 8.5f, 4f), white);
                p.Fill(RR(27.75f, 15, 8.5f, 34, 4f), white);
                break;
            }
            default:   // Minus
            {
                p.Fill(Circle(32, 32, 26), VGrad(6, 58, C(0xC9, 0x6A, 0x14), C(0xFF, 0xB8, 0x4A)));
                p.Fill(RR(15, 28, 34, 8.5f, 4f), white);
                break;
            }
        }
    }
}
