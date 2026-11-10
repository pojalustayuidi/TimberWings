// Assets/_Project/Editor/TWRiver/TWRiverKit.cs
// Timber Wings: ось реки, гранёные меши, растительность, шум и контекст сборки.
// Ничего не меняет в сцене сам по себе. Только геометрия и вспомогательные расчёты.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace IslandV2
{
    // ------------------------------------------------------------------ ось реки
    internal sealed class TWRiverAxis
    {
        public readonly List<Vector2> pts = new List<Vector2>();
        readonly List<float> cum = new List<float>();
        public float Length { get { return cum.Count > 0 ? cum[cum.Count - 1] : 0f; } }

        public TWRiverAxis(List<Vector2> piece, float step)
        {
            if (piece == null || piece.Count == 0) return;
            pts.Add(piece[0]); cum.Add(0f);
            for (int i = 1; i < piece.Count; i++)
            {
                Vector2 a = piece[i - 1], b = piece[i];
                int n = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / Mathf.Max(0.3f, step)));
                for (int k = 1; k <= n; k++)
                {
                    Vector2 p = Vector2.Lerp(a, b, (float)k / n);
                    cum.Add(cum[cum.Count - 1] + Vector2.Distance(pts[pts.Count - 1], p));
                    pts.Add(p);
                }
            }
        }

        Vector2 TangentRaw(int i)
        {
            if (pts.Count < 2) return Vector2.up;
            int j = Mathf.Clamp(i, 0, pts.Count - 2);
            Vector2 d = pts[j + 1] - pts[j];
            return d.sqrMagnitude < 1e-8f ? Vector2.up : d.normalized;
        }

        // точка на оси на расстоянии s от начала; до начала и после конца продолжает по касательной
        public Vector2 At(float s)
        {
            if (pts.Count < 2) return pts.Count == 1 ? pts[0] : Vector2.zero;
            if (s <= 0f) return pts[0] + TangentRaw(0) * s;
            if (s >= Length) return pts[pts.Count - 1] + TangentRaw(pts.Count - 2) * (s - Length);
            int lo = 0, hi = cum.Count - 1;
            while (hi - lo > 1) { int mid = (lo + hi) / 2; if (cum[mid] <= s) lo = mid; else hi = mid; }
            float seg = Mathf.Max(1e-5f, cum[hi] - cum[lo]);
            return Vector2.Lerp(pts[lo], pts[hi], (s - cum[lo]) / seg);
        }

        public Vector2 Tangent(float s)
        {
            float a = Mathf.Clamp(s - 1.0f, 0f, Length), b = Mathf.Clamp(s + 1.0f, 0f, Length);
            if (b - a < 0.2f) return TangentRaw(s <= 0f ? 0 : pts.Count - 2);
            Vector2 d = At(b) - At(a);
            return d.sqrMagnitude < 1e-8f ? TangentRaw(0) : d.normalized;
        }

        // влево по течению (если смотреть сверху, X вправо, Z вверх)
        public Vector2 Side(float s) { Vector2 t = Tangent(s); return new Vector2(-t.y, t.x); }
    }

    // ------------------------------------------------------------------ шум и хеш
    internal static class TWNoise
    {
        public static float Hash(int a, int b, int seed)
        {
            unchecked
            {
                uint x = (uint)(a * 73856093) ^ (uint)(b * 19349663) ^ (uint)(seed * 83492791);
                x ^= x >> 16; x *= 2246822519u; x ^= x >> 13; x *= 3266489917u; x ^= x >> 16;
                return (x & 0xFFFFFF) / 16777215f;
            }
        }

        // гладкий 1D-шум 0..1
        public static float Val1(float x, int seed)
        {
            int i = Mathf.FloorToInt(x);
            float f = x - i;
            f = f * f * (3f - 2f * f);
            return Mathf.Lerp(Hash(i, 0, seed), Hash(i + 1, 0, seed), f);
        }
    }

    // Shared foliage colours; rock shapes/materials come from authored project assets.
    internal sealed class TWRiverPalette
    {
        readonly Color light, dark;
        public TWRiverPalette(IslandLayout layout)
        {
            light = Color.Lerp(layout.lipTop, new Color(0.63f, 0.76f, 0.46f), 0.65f);
            dark = Color.Lerp(layout.lipDark, new Color(0.29f, 0.51f, 0.37f), 0.65f);
        }
        public Color Green(float hash, float tip)
        {
            Color colour = Color.Lerp(dark, light, Mathf.Clamp01(hash * 0.75f + tip * 0.25f));
            colour.a = 1f;
            return colour;
        }
    }

    // ------------------------------------------------------------------ граненый меш (у каждой грани своя нормаль и цвет)
    internal sealed class TWFaceted
    {
        readonly List<Vector3> v = new List<Vector3>();
        readonly List<Vector3> n = new List<Vector3>();
        readonly List<Color> c = new List<Color>();
        readonly List<int> t = new List<int>();
        public int Tris { get { return t.Count / 3; } }

        void Add(Vector3 a, Vector3 b, Vector3 cc, Vector3 nn, Color col)
        {
            int k = v.Count;
            v.Add(a); v.Add(b); v.Add(cc);
            n.Add(nn); n.Add(nn); n.Add(nn);
            c.Add(col); c.Add(col); c.Add(col);
            t.Add(k); t.Add(k + 1); t.Add(k + 2);
        }

        // нормаль смотрит от точки inside (грань наружу из тела)
        public void TriAway(Vector3 a, Vector3 b, Vector3 cc, Color col, Vector3 inside)
        {
            Vector3 nn = Vector3.Cross(b - a, cc - a);
            if (nn.sqrMagnitude < 1e-10f) return;
            nn.Normalize();
            Vector3 cen = (a + b + cc) / 3f;
            if (Vector3.Dot(nn, cen - inside) < 0f) { Vector3 tmp = b; b = cc; cc = tmp; nn = -nn; }
            Add(a, b, cc, nn, col);
        }

        // нормаль смотрит в сторону точки toward
        public void TriToward(Vector3 a, Vector3 b, Vector3 cc, Color col, Vector3 toward)
        {
            Vector3 nn = Vector3.Cross(b - a, cc - a);
            if (nn.sqrMagnitude < 1e-10f) return;
            nn.Normalize();
            Vector3 cen = (a + b + cc) / 3f;
            if (Vector3.Dot(nn, toward - cen) < 0f) { Vector3 tmp = b; b = cc; cc = tmp; nn = -nn; }
            Add(a, b, cc, nn, col);
        }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name };
            if (v.Count >= 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetColors(c);
            m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }
    }

    // ------------------------------------------------------------------ пена: полосы вдоль течения, альфа из цвета вершин
    internal sealed class TWFoam
    {
        readonly List<Vector3> v = new List<Vector3>();
        readonly List<Color> c = new List<Color>();
        readonly List<int> t = new List<int>();
        public int Tris { get { return t.Count / 3; } }

        // p: центральная линия (с высотой), hw: полуширина, a: непрозрачность в центре (по краям 0)
        public void Strip(IList<Vector3> p, IList<float> hw, IList<float> a)
        {
            int cnt = p.Count;
            if (cnt < 2) return;
            int first = v.Count;
            Vector3 lastDir = Vector3.forward;
            for (int i = 0; i < cnt; i++)
            {
                Vector3 d = p[Mathf.Min(cnt - 1, i + 1)] - p[Mathf.Max(0, i - 1)];
                d.y = 0f;
                if (d.sqrMagnitude < 1e-8f) d = lastDir;
                d.Normalize(); lastDir = d;
                Vector3 side = new Vector3(-d.z, 0f, d.x);
                v.Add(p[i] - side * hw[i]); c.Add(new Color(1f, 1f, 1f, 0f));
                v.Add(p[i]); c.Add(new Color(1f, 1f, 1f, Mathf.Clamp01(a[i])));
                v.Add(p[i] + side * hw[i]); c.Add(new Color(1f, 1f, 1f, 0f));
            }
            for (int i = 0; i + 1 < cnt; i++)
            {
                int b = first + i * 3;
                t.Add(b); t.Add(b + 3); t.Add(b + 1);
                t.Add(b + 1); t.Add(b + 3); t.Add(b + 4);
                t.Add(b + 1); t.Add(b + 4); t.Add(b + 2);
                t.Add(b + 2); t.Add(b + 4); t.Add(b + 5);
            }
        }

        // мягкое кольцо пены вокруг камня на уровне воды: эллипс, вытянутый вдоль течения
        public void Ring(Vector3 centre, Vector3 flowDir, float rAlong, float rAcross, float width, float alpha)
        {
            flowDir.y = 0f;
            if (flowDir.sqrMagnitude < 1e-6f) flowDir = Vector3.forward;
            flowDir.Normalize();
            Vector3 side = new Vector3(-flowDir.z, 0f, flowDir.x);
            const int S = 14;
            int first = v.Count;
            for (int i = 0; i < S; i++)
            {
                float th = i * Mathf.PI * 2f / S;
                Vector3 dir = flowDir * Mathf.Cos(th) + side * Mathf.Sin(th);
                Vector3 pIn = centre + flowDir * (Mathf.Cos(th) * rAlong) + side * (Mathf.Sin(th) * rAcross);
                // ниже по течению пена гуще (след), выше по течению чуть слабее
                float k = 0.65f + 0.35f * Mathf.Clamp01(0.5f + 0.5f * Mathf.Cos(th));
                v.Add(pIn - dir * (width * 0.25f)); c.Add(new Color(1f, 1f, 1f, alpha * k));
                v.Add(pIn + dir * width); c.Add(new Color(1f, 1f, 1f, 0f));
            }
            for (int i = 0; i < S; i++)
            {
                int a0 = first + i * 2, b0 = first + ((i + 1) % S) * 2;
                t.Add(a0); t.Add(b0); t.Add(a0 + 1);
                t.Add(a0 + 1); t.Add(b0); t.Add(b0 + 1);
            }
        }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name };
            if (v.Count >= 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(v);
            m.SetColors(c);
            var nr = new List<Vector3>(v.Count);
            for (int i = 0; i < v.Count; i++) nr.Add(Vector3.up);
            m.SetNormals(nr);
            m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }
    }

    // ------------------------------------------------------------------ камень: низкополигональный неровный эллипсоид
    internal static class TWStone
    {
        public delegate Color ColorFn(float worldY, Vector3 normal, float hash);

        public static void Build(TWFaceted m, Vector3 centre, Vector3 radii, float yaw, int seed, ColorFn color, float sinkBelow)
        {
            const int S = 8;
            float[] ryF = { -0.62f, -0.08f, 0.44f, 0.80f };
            float[] rsF = { 0.60f, 1.00f, 0.90f, 0.52f };
            const int R = 4;
            var P = new Vector3[R, S];
            float cy = Mathf.Cos(yaw), sy = Mathf.Sin(yaw);
            for (int r = 0; r < R; r++)
            {
                for (int j = 0; j < S; j++)
                {
                    float th = (j + (TWNoise.Hash(j, r, seed + 3) - 0.5f) * 0.45f) * Mathf.PI * 2f / S;
                    float jit = Mathf.Lerp(0.80f, 1.18f, TWNoise.Hash(j, r, seed));
                    float x = Mathf.Cos(th) * radii.x * rsF[r] * jit;
                    float z = Mathf.Sin(th) * radii.z * rsF[r] * jit;
                    float yy = ryF[r] * radii.y * Mathf.Lerp(0.88f, 1.12f, TWNoise.Hash(j, r + 7, seed));
                    P[r, j] = centre + new Vector3(x * cy - z * sy, yy, x * sy + z * cy);
                }
            }
            Vector3 topPole = centre + new Vector3((TWNoise.Hash(1, 2, seed) - 0.5f) * 0.3f * radii.x, radii.y * Mathf.Lerp(0.92f, 1.08f, TWNoise.Hash(3, 4, seed)), (TWNoise.Hash(5, 6, seed) - 0.5f) * 0.3f * radii.z);
            Vector3 botPole = centre + new Vector3(0f, -radii.y * 0.9f - sinkBelow, 0f);
            for (int j = 0; j < S; j++)
            {
                int k = (j + 1) % S;
                for (int r = 0; r + 1 < R; r++)
                {
                    float h = TWNoise.Hash(j, r + 20, seed);
                    Vector3 a = P[r, j], b = P[r, k], c = P[r + 1, k], d = P[r + 1, j];
                    Vector3 cen = (a + b + c + d) * 0.25f;
                    Vector3 nn = Vector3.Cross(b - a, d - a).normalized;
                    if (Vector3.Dot(nn, cen - centre) < 0f) nn = -nn;
                    Color col = color(cen.y, nn, h);
                    m.TriAway(a, b, c, col, centre);
                    m.TriAway(a, c, d, col, centre);
                }
                {
                    Vector3 a = P[R - 1, j], b = P[R - 1, k];
                    Vector3 cen = (a + b + topPole) / 3f;
                    Vector3 nn = Vector3.Cross(b - a, topPole - a).normalized;
                    if (Vector3.Dot(nn, cen - centre) < 0f) nn = -nn;
                    m.TriAway(a, b, topPole, color(cen.y, nn, TWNoise.Hash(j, 40, seed)), centre);
                }
                {
                    Vector3 a = P[0, j], b = P[0, k];
                    m.TriAway(a, b, botPole, color(a.y, Vector3.down, 0.2f), centre);
                }
            }
        }
    }

    // ------------------------------------------------------------------ общий контекст сборки
    internal sealed class TWRiverCtx
    {
        public IslandField f;
        public IslandLayout L;
        public TWRiverAxis ax;
        public TWRiverPalette pal;
        public TWFaceted rocks = new TWFaceted();   // камень и растения: один меш и один материал
        public TWFoam foam = new TWFoam();
        public float half;
        public int rockSide = 1;                    // +1: скалистый берег слева по течению, -1: справа
        public readonly List<Vector3> exclusions = new List<Vector3>();   // x,y = центр (XZ), z = радиус
        public readonly List<Vector4> stones = new List<Vector4>();       // камни в воде: x = s вдоль оси, y = смещение поперёк, z = высота воды, w = радиус (для пены-следа)
        public int seed;
        public bool hasSource;

        public float Yw(float s)
        {
            Vector2 p = ax.At(Mathf.Max(0f, s));
            return f.RiverSurfaceAt(p.x, p.y);
        }

        public Vector3 P(float s, float off, float y)
        {
            Vector2 a = ax.At(s), c = ax.Side(Mathf.Clamp(s, 0f, ax.Length));
            return new Vector3(a.x + c.x * off, y, a.y + c.y * off);
        }

        public Vector3 OnGround(float s, float off, float lift)
        {
            Vector3 p = P(s, off, 0f);
            p.y = f.Height(p.x, p.z) + lift;
            return p;
        }

        public float ExclusionWeight(Vector2 p, float pad)
        {
            float w = 0f;
            for (int i = 0; i < exclusions.Count; i++)
            {
                Vector3 e = exclusions[i];
                float d = Vector2.Distance(p, new Vector2(e.x, e.y));
                float k = 1f - TWRiverSpec.Smooth(e.z, e.z + pad, d);
                if (k > w) w = k;
            }
            return w;
        }

        public readonly List<Bounds> protectedBounds = new List<Bounds>();

        public bool Protected(Vector3 p, float padding = 0.6f)
        {
            if (ExclusionWeight(new Vector2(p.x, p.z), padding) > 0.01f) return true;
            foreach (var b in protectedBounds)
                if (p.x > b.min.x - padding && p.x < b.max.x + padding &&
                    p.z > b.min.z - padding && p.z < b.max.z + padding) return true;
            return false;
        }
    }
}

