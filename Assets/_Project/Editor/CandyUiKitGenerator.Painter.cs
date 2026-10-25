// Assets/_Project/Editor/CandyUiKitGenerator.Painter.cs   (ДОЛЖЕН лежать в папке Editor, рядом с CandyUiKitGenerator.cs)
//
// Движок рисования спрайтов кодом: SDF-фигуры, градиенты, холст Painter с суперсэмплингом (Q = 2).
// Перенесён из старого процедурного генератора БЕЗ изменений. Сейчас им рисуются только Fun-спрайты
// (CandyUiKitGenerator.Fun.cs); основные спрайты набора берутся из готовых PNG v2.
// Нужен, если захотите дорисовать ещё один декоративный спрайт кодом: добавьте Add("Fun/имя", ...) в Fun.cs.
using System;
using UnityEngine;
using Sdf = System.Func<float, float, float>;
using Shade = System.Func<float, float, UnityEngine.Color>;

public static partial class CandyUiKitGenerator
{
    // ======================================================================================
    //  ПРИМИТИВЫ (SDF) И КРАСКИ
    // ======================================================================================
    static Color H(string hex, float a = 1f)
    {
        return new Color(Convert.ToInt32(hex.Substring(1, 2), 16) / 255f, Convert.ToInt32(hex.Substring(3, 2), 16) / 255f,
                         Convert.ToInt32(hex.Substring(5, 2), 16) / 255f, a);
    }
    static Vector2 V(float x, float y) { return new Vector2(x, y); }

    static Sdf RR(float x, float y, float w, float h, float r)
    {
        float cx = x + w * 0.5f, cy = y + h * 0.5f, hx = w * 0.5f - r, hy = h * 0.5f - r;
        return (px, py) =>
        {
            float qx = Mathf.Abs(px - cx) - hx, qy = Mathf.Abs(py - cy) - hy;
            float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        };
    }
    static Sdf Circle(float cx, float cy, float r)
    {
        return (px, py) => Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy)) - r;
    }
    static Sdf Ellipse(float cx, float cy, float rx, float ry)
    {
        float m = Mathf.Min(rx, ry);
        return (px, py) =>
        {
            float dx = (px - cx) / rx, dy = (py - cy) / ry;
            return (Mathf.Sqrt(dx * dx + dy * dy) - 1f) * m;
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
        int n = v.Length;
        return (px, py) =>
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
                if (((a.y > py) != (b.y > py)) && (px < (b.x - a.x) * (py - a.y) / (b.y - a.y + 1e-9f) + a.x)) inside = !inside;
            }
            d = Mathf.Sqrt(d);
            return inside ? -d : d;
        };
    }
    static Sdf Ring(Sdf f, float t) { return (x, y) => { float d = f(x, y); return Mathf.Max(d, -(d + t)); }; }
    static Sdf Union(params Sdf[] fs)
    {
        return (x, y) => { float r = fs[0](x, y); for (int i = 1; i < fs.Length; i++) r = Mathf.Min(r, fs[i](x, y)); return r; };
    }
    static Sdf Inter(Sdf a, Sdf b) { return (x, y) => Mathf.Max(a(x, y), b(x, y)); }
    static Sdf Sub(Sdf a, Sdf b) { return (x, y) => Mathf.Max(a(x, y), -b(x, y)); }
    static Sdf Grow(Sdf f, float t) { return (x, y) => f(x, y) - t; }
    static Sdf ClipY(Sdf f, float minY) { return (x, y) => Mathf.Max(f(x, y), minY - y); }      // оставить то, что выше minY
    static Sdf ClipYMax(Sdf f, float maxY) { return (x, y) => Mathf.Max(f(x, y), y - maxY); }   // оставить то, что ниже maxY
    static Sdf ClipX(Sdf f, float minX) { return (x, y) => Mathf.Max(f(x, y), minX - x); }
    static Sdf Rot(Sdf f, float cx, float cy, float deg)
    {
        float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
        return (x, y) => { float dx = x - cx, dy = y - cy; return f(cx + dx * c + dy * s, cy - dx * s + dy * c); };
    }

    static Shade Solid(string hex, float a = 1f) { Color c = H(hex, a); return (x, y) => c; }
    static Shade VGrad(float y0, float y1, string bottom, string top)
    {
        Color b = H(bottom), t = H(top);
        return (x, y) => Color.LerpUnclamped(b, t, Mathf.Clamp01((y - y0) / (y1 - y0)));
    }
    static Shade HGrad(float x0, float x1, string left, string right)
    {
        Color b = H(left), t = H(right);
        return (x, y) => Color.LerpUnclamped(b, t, Mathf.Clamp01((x - x0) / (x1 - x0)));
    }
    static Shade RGrad(float cx, float cy, float r, string inner, string outer)
    {
        Color b = H(inner), t = H(outer);
        return (x, y) => Color.LerpUnclamped(b, t, Mathf.Clamp01(Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / r));
    }
    // дерево: вертикальный градиент и волокна (вытянутый по горизонтали шум)
    static Shade Wood(float y0, float y1, string bottom, string top, float seed)
    {
        Color b = H(bottom), t = H(top);
        return (x, y) =>
        {
            Color c = Color.LerpUnclamped(b, t, Mathf.Clamp01((y - y0) / (y1 - y0)));
            float n = VNoise(x * 0.05f, y * 0.6f + seed) - 0.5f;
            float n2 = VNoise(x * 0.13f, y * 2.1f + seed * 3f) - 0.5f;
            float k = 1f + n * 0.22f + n2 * 0.12f;
            return new Color(c.r * k, c.g * k, c.b * k, c.a);
        };
    }
    static float Hash(float x, float y) { float v = Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f; return v - Mathf.Floor(v); }
    static float VNoise(float x, float y)
    {
        float xi = Mathf.Floor(x), yi = Mathf.Floor(y), fx = x - xi, fy = y - yi;
        fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
        float a = Hash(xi, yi), b = Hash(xi + 1f, yi), c = Hash(xi, yi + 1f), d = Hash(xi + 1f, yi + 1f);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    static Color Over(Color s, Color d)
    {
        float a = s.a + d.a * (1f - s.a);
        if (a <= 0.0001f) return new Color(0f, 0f, 0f, 0f);
        float k = d.a * (1f - s.a);
        return new Color((s.r * s.a + d.r * k) / a, (s.g * s.a + d.g * k) / a, (s.b * s.a + d.b * k) / a, a);
    }

    // холст: координаты в дизайн-пикселях, начало внизу слева, ось Y вверх
    public sealed class Painter
    {
        public readonly int W, H;
        public readonly Color[] px;

        public Painter(int designW, int designH)
        {
            W = designW * Q; H = designH * Q;
            px = new Color[W * H];
        }

        public void Fill(Sdf f, Shade c)
        {
            float inv = 1f / Q;
            for (int y = 0; y < H; y++)
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
        public void Fill(Sdf f, Color c) { Fill(f, (x, y) => c); }

        // тёмный контур вокруг всего нарисованного (под уже нарисованным)
        public void Outline(float t, Color c)
        {
            int r = Mathf.CeilToInt(t * Q);
            var a = new float[px.Length];
            for (int i = 0; i < a.Length; i++) a[i] = px[i].a;
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    float m = a[y * W + x];
                    if (m < 0.999f)
                    {
                        for (int oy = -r; oy <= r; oy++)
                        {
                            int yy = y + oy; if (yy < 0 || yy >= H) continue;
                            for (int ox = -r; ox <= r; ox++)
                            {
                                if (ox * ox + oy * oy > r * r) continue;
                                int xx = x + ox; if (xx < 0 || xx >= W) continue;
                                float v = a[yy * W + xx]; if (v > m) m = v;
                            }
                        }
                    }
                    if (m > 0.001f)
                    {
                        int i = y * W + x;
                        px[i] = Over(px[i], new Color(c.r, c.g, c.b, m * c.a));
                    }
                }
            }
        }

        // цвет границы «протекает» в прозрачные пиксели: без этого при билинейной фильтрации по краям лезет тёмная кайма
        public void Bleed()
        {
            var src = (Color[])px.Clone();
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    if (src[i].a > 0.001f) continue;
                    bool found = false;
                    for (int oy = -2; oy <= 2 && !found; oy++)
                    {
                        int yy = y + oy; if (yy < 0 || yy >= H) continue;
                        for (int ox = -2; ox <= 2; ox++)
                        {
                            int xx = x + ox; if (xx < 0 || xx >= W) continue;
                            Color n = src[yy * W + xx];
                            if (n.a > 0.05f) { px[i] = new Color(n.r, n.g, n.b, 0f); found = true; break; }
                        }
                    }
                }
            }
        }
    }
}
