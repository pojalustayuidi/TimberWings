#if UNITY_EDITOR
// Assets/_Project/Editor/GateBridge/GBMesh.cs (Editor)
// Накопитель плоско-затенённого меша из коробок, балок, камней и лент (цвета в вершинах, шейдер Project/IslandRock).
// Прозрачность цвета вершины: 1 = обычный материал, 0 = свечение (как у кристалла арки).
using System.Collections.Generic;
using UnityEngine;

sealed class GBMesh
{
    public readonly List<Vector3> V = new List<Vector3>();
    public readonly List<Color> C = new List<Color>();
    public readonly List<int> T = new List<int>();

    public void Tri(Vector3 a, Vector3 b, Vector3 d, Color col, Vector3 inside)
    {
        IslandUnderside.Face(V, C, T, a, b, d, col, col, col, inside);
    }

    public void Tri(Vector3 a, Vector3 b, Vector3 d, Color ca, Color cb, Color cd, Vector3 inside)
    {
        IslandUnderside.Face(V, C, T, a, b, d, ca, cb, cd, inside);
    }

    public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col, Vector3 inside)
    {
        Tri(a, b, c, col, inside);
        Tri(a, c, d, col, inside);
    }

    // Коробка: центр, оси (right, up, fwd), половинные размеры
    public void Box(Vector3 c0, Vector3 r, Vector3 u, Vector3 f, float hx, float hy, float hz, Color col)
    {
        System.Func<int, int, int, Vector3> P = (sx, sy, sz) => c0 + r * (sx * hx) + u * (sy * hy) + f * (sz * hz);
        Quad(P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1), col, c0);
        Quad(P(-1, -1, -1), P(-1, 1, -1), P(-1, 1, 1), P(-1, -1, 1), col, c0);
        Quad(P(-1, 1, -1), P(1, 1, -1), P(1, 1, 1), P(-1, 1, 1), col, c0);
        Quad(P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1), col, c0);
        Quad(P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), col, c0);
        Quad(P(-1, -1, -1), P(1, -1, -1), P(1, 1, -1), P(-1, 1, -1), col, c0);
    }

    // Коробка по осям мира
    public void BoxAxis(Vector3 c0, float hx, float hy, float hz, Color col)
    {
        Box(c0, Vector3.right, Vector3.up, Vector3.forward, hx, hy, hz, col);
    }

    // Балка квадратного сечения между двумя точками (трос, стойка)
    public void Beam(Vector3 a, Vector3 b, float hw, Color col)
    {
        Vector3 f = b - a;
        float len = f.magnitude;
        if (len < 1e-4f) return;
        f /= len;
        Vector3 r = Vector3.Cross(Vector3.up, f);
        if (r.sqrMagnitude < 1e-4f) r = Vector3.right;
        r.Normalize();
        Vector3 u = Vector3.Cross(f, r).normalized;
        Box((a + b) * 0.5f, r, u, f, hw, hw, len * 0.5f, col);
    }

    public void Cone(Vector3 bp, float radius, float h, Color col, int seg)
    {
        Vector3 tip = bp + Vector3.up * h, inside = bp + Vector3.up * (h * 0.4f);
        for (int j = 0; j < seg; j++)
        {
            float a0 = j * Mathf.PI * 2f / seg, a1 = (j + 1) * Mathf.PI * 2f / seg;
            Vector3 p0 = bp + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius;
            Vector3 p1 = bp + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius;
            Tri(p0, p1, tip, col, inside);
            Tri(p0, p1, bp, Sh(col, 0.8f), inside);
        }
    }

    // Плоский диск (плита, мощение): верх своим цветом, бока темнее
    public void Disc(Vector3 center, float radius, float thick, int seg, Color top)
    {
        Vector3 inside = center - Vector3.up * (thick * 0.5f);
        Vector3 topC = center, botC = center - Vector3.up * thick;
        Color side = Sh(top, 0.72f);
        for (int j = 0; j < seg; j++)
        {
            float a0 = j * Mathf.PI * 2f / seg, a1 = (j + 1) * Mathf.PI * 2f / seg;
            Vector3 o0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius;
            Vector3 o1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius;
            Tri(topC, center + o0, center + o1, top, inside);
            Quad(center + o0, center + o1, center + o1 - Vector3.up * thick, center + o0 - Vector3.up * thick, side, inside);
            Tri(botC, botC + o0, botC + o1, side, inside);
        }
    }

    // Лента вдоль пути: сечение прямоугольник [x0..x1] x [y0..y1] в системе кадра пути. Для коллайдеров и подложки настила
    public void Sweep(System.Func<float, Vector3> P, float s0, float s1, float step, float x0, float x1, float y0, float y1, Color col)
    {
        int K = Mathf.Max(1, Mathf.CeilToInt((s1 - s0) / step));
        var a = new Vector3[K + 1]; var b = new Vector3[K + 1]; var c = new Vector3[K + 1]; var d = new Vector3[K + 1];
        var mid = new Vector3[K + 1];
        for (int k = 0; k <= K; k++)
        {
            float s = Mathf.Lerp(s0, s1, k / (float)K);
            Vector3 f = (P(s + 0.05f) - P(s - 0.05f)).normalized;
            Vector3 r = Vector3.Cross(Vector3.up, f).normalized;
            Vector3 u = Vector3.Cross(f, r).normalized;
            Vector3 p = P(s);
            a[k] = p + r * x0 + u * y1; b[k] = p + r * x1 + u * y1;   // верх: левый, правый
            c[k] = p + r * x1 + u * y0; d[k] = p + r * x0 + u * y0;   // низ: правый, левый
            mid[k] = p + r * ((x0 + x1) * 0.5f) + u * ((y0 + y1) * 0.5f);
        }
        for (int k = 0; k < K; k++)
        {
            Vector3 ins = (mid[k] + mid[k + 1]) * 0.5f;
            Quad(a[k], b[k], b[k + 1], a[k + 1], col, ins);   // верх
            Quad(d[k], c[k], c[k + 1], d[k + 1], col, ins);   // низ
            Quad(a[k], d[k], d[k + 1], a[k + 1], col, ins);   // левый бок
            Quad(b[k], c[k], c[k + 1], b[k + 1], col, ins);   // правый бок
        }
        Quad(a[0], b[0], c[0], d[0], col, mid[0] + (mid[1] - mid[0]));
        Quad(a[K], b[K], c[K], d[K], col, mid[K] - (mid[K] - mid[K - 1]));
    }

    static float Noise(Vector3 v, int seed)
    {
        return Mathf.PerlinNoise(v.x * 2.1f + v.y * 1.7f + seed * 3.1f + 40f, v.z * 2.3f + v.y * 1.3f + seed * 1.7f + 40f);
    }

    // Камень или облачко: октаэдр, поделённый один раз (32 грани), вершины качаются по шуму. bottomFlat < 1 приплющивает низ
    public void Rock(Vector3 c, float rx, float ry, float rz, int seed, float jitter, float bottomFlat, Color col)
    {
        Vector3[] bv = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        int[,] bf = { { 0, 2, 4 }, { 4, 2, 1 }, { 1, 2, 5 }, { 5, 2, 0 }, { 0, 4, 3 }, { 4, 1, 3 }, { 1, 5, 3 }, { 5, 0, 3 } };
        System.Func<Vector3, Vector3> W = v =>
        {
            float k = 1f - jitter + jitter * 2f * Noise(v, seed);
            float y = v.y < 0f ? v.y * bottomFlat : v.y;
            return c + new Vector3(v.x * rx * k, y * ry * k, v.z * rz * k);
        };
        for (int i = 0; i < 8; i++)
        {
            Vector3 A = bv[bf[i, 0]], B = bv[bf[i, 1]], D = bv[bf[i, 2]];
            Vector3 ab = ((A + B) * 0.5f).normalized, bd = ((B + D) * 0.5f).normalized, da = ((D + A) * 0.5f).normalized;
            float shade = 0.9f + 0.2f * Noise(A + B + D, seed + 5);
            Color cc = Sh(col, shade);
            Tri(W(A), W(ab), W(da), cc, c);
            Tri(W(ab), W(B), W(bd), cc, c);
            Tri(W(da), W(bd), W(D), cc, c);
            Tri(W(ab), W(bd), W(da), cc, c);
        }
    }

    public static Color Sh(Color c, float k) { return new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), c.a); }

    public Mesh ToMesh(string name)
    {
        return IslandUnderside.ToMesh(name, V, C, T);
    }
}
#endif
