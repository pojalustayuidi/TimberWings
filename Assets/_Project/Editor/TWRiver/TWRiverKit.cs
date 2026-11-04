// Assets/_Project/Editor/TWRiver/TWRiverKit.cs
// Timber Wings: единая система «исток - русло - водопад». Общий набор: граненый меш, шум, палитра, ear-clipping, зонд по мешу обрыва, контекст сборки.
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

    // ------------------------------------------------------------------ палитра: та же логика, что у обрыва и стен русла
    internal sealed class TWRiverPalette
    {
        public readonly Color wet, mid, top, dark, lipTop, lipDark;

        public TWRiverPalette(IslandLayout L)
        {
            wet = L.riverWallWet; mid = L.rockMid; top = L.rockTop; dark = L.rockDark;
            lipTop = L.lipTop; lipDark = L.lipDark;
        }

        static Color Mul(Color c, float k) { return new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), 1f); }
        static Color Mix(Color a, Color b, float t) { Color r = Color.Lerp(a, b, Mathf.Clamp01(t)); r.a = 1f; return r; }

        // hAbove: высота грани над водой, n: нормаль грани, hash: 0..1 (оттенок), moss: 0..1 (мох на верхних гранях)
        public Color Rock(float hAbove, Vector3 n, float hash, float moss)
        {
            float wetK = 1f - Mathf.SmoothStep(0.12f, 1.35f, hAbove);
            Color c = Mix(mid, top, Mathf.SmoothStep(0.8f, 4.8f, hAbove));
            c = Mix(c, wet, wetK);
            float up = Mathf.Clamp01(n.y);
            c = Mix(Mul(c, 0.88f), Mul(c, 1.07f), up);
            c = Mul(c, 0.93f + 0.14f * hash);
            if (moss > 0.01f) c = Mix(c, Mix(lipDark, lipTop, hash), moss * Mathf.Clamp01(up * 1.3f - 0.2f));
            return c;
        }

        public Color Cave(float depth01)   // 0 у входа, 1 в глубине
        {
            return Mix(Mul(dark, 0.62f), Mul(dark, 0.13f), Mathf.Pow(Mathf.Clamp01(depth01), 0.8f));
        }

        public Color Green(float hash, float tip)
        {
            return Mix(Mix(lipDark, lipTop, hash), Mul(lipTop, 1.12f), tip * 0.6f);
        }
    }

    // ------------------------------------------------------------------ граненый меш (у каждой грани своя нормаль и цвет)
    internal sealed class TWFaceted
    {
        readonly List<Vector3> v = new List<Vector3>();
        readonly List<Vector3> n = new List<Vector3>();
        readonly List<Color> c = new List<Color>();
        readonly List<int> t = new List<int>();
        public readonly List<Vector3> anchors = new List<Vector3>();   // точки, которые обязаны лежать в земле, воде или обрыве
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

    // ------------------------------------------------------------------ геометрия
    internal static class TWGeo
    {
        static float Cross2(Vector2 a, Vector2 b) { return a.x * b.y - a.y * b.x; }

        static bool InTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross2(b - a, p - a), d2 = Cross2(c - b, p - b), d3 = Cross2(a - c, p - c);
            bool neg = d1 < -1e-7f || d2 < -1e-7f || d3 < -1e-7f;
            bool pos = d1 > 1e-7f || d2 > 1e-7f || d3 > 1e-7f;
            return !(neg && pos);
        }

        // триангуляция простого многоугольника (любая ориентация). Индексы относятся к исходному списку
        public static List<int> EarClip(List<Vector2> p)
        {
            var res = new List<int>();
            int n = p.Count;
            if (n < 3) return res;
            float area = 0f;
            for (int i = 0; i < n; i++) area += Cross2(p[i], p[(i + 1) % n]);
            bool ccw = area > 0f;
            var idx = new List<int>(n);
            for (int i = 0; i < n; i++) idx.Add(ccw ? i : n - 1 - i);
            int guard = 0;
            while (idx.Count > 3 && guard++ < 2000)
            {
                bool clipped = false;
                for (int i = 0; i < idx.Count; i++)
                {
                    int ia = idx[(i + idx.Count - 1) % idx.Count], ib = idx[i], ic = idx[(i + 1) % idx.Count];
                    Vector2 a = p[ia], b = p[ib], c = p[ic];
                    if (Cross2(b - a, c - b) <= 1e-6f) continue;   // вогнутая или вырожденная вершина
                    bool inside = false;
                    for (int k = 0; k < idx.Count; k++)
                    {
                        int ik = idx[k];
                        if (ik == ia || ik == ib || ik == ic) continue;
                        if (InTri(p[ik], a, b, c)) { inside = true; break; }
                    }
                    if (inside) continue;
                    res.Add(ia); res.Add(ib); res.Add(ic);
                    idx.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped) break;
            }
            if (idx.Count >= 3)
            {
                // остаток: веер (для вырожденных случаев хуже не будет)
                for (int i = 1; i + 1 < idx.Count; i++) { res.Add(idx[0]); res.Add(idx[i]); res.Add(idx[i + 1]); }
            }
            return res;
        }
    }

    // ------------------------------------------------------------------ зонд по мешу обрыва (чтобы прижимать камни к настоящей скале)
    internal sealed class TWCliffProbe
    {
        readonly Vector3[] verts;
        readonly int[] tris;

        TWCliffProbe(Vector3[] v, int[] t) { verts = v; tris = t; }

        public static TWCliffProbe Find(Transform gen)
        {
            if (gen == null) return null;
            var cl = gen.Find(IslandCliff.ObjectName);
            if (cl == null) return null;
            var mf = cl.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return null;
            var m = mf.sharedMesh;
            var lv = m.vertices;
            var wv = new Vector3[lv.Length];
            for (int i = 0; i < lv.Length; i++) wv[i] = cl.TransformPoint(lv[i]);
            return new TWCliffProbe(wv, m.triangles);
        }

        // Горизонтальный луч из origin в сторону dir (XZ). Возвращает расстояние до ближайшей грани обрыва или -1
        public float Ray(Vector3 origin, Vector2 dir, float maxDist)
        {
            Vector3 d = new Vector3(dir.x, 0f, dir.y).normalized;
            float best = float.MaxValue;
            for (int i = 0; i + 2 < tris.Length; i += 3)
            {
                Vector3 a = verts[tris[i]], b = verts[tris[i + 1]], c = verts[tris[i + 2]];
                Vector3 e1 = b - a, e2 = c - a;
                Vector3 pv = Vector3.Cross(d, e2);
                float det = Vector3.Dot(e1, pv);
                if (Mathf.Abs(det) < 1e-8f) continue;
                float inv = 1f / det;
                Vector3 tv = origin - a;
                float u = Vector3.Dot(tv, pv) * inv;
                if (u < 0f || u > 1f) continue;
                Vector3 qv = Vector3.Cross(tv, e1);
                float w = Vector3.Dot(d, qv) * inv;
                if (w < 0f || u + w > 1f) continue;
                float tt = Vector3.Dot(e2, qv) * inv;
                if (tt > 0.001f && tt < best) best = tt;
            }
            return best < maxDist ? best : -1f;
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
            for (int j = 0; j < S; j++) m.anchors.Add(P[0, j]);
            m.anchors.Add(botPole);
        }
    }

    // ------------------------------------------------------------------ общий контекст сборки
    internal sealed class TWRiverCtx
    {
        public IslandField f;
        public IslandLayout L;
        public TWRiverAxis ax;
        public TWCliffProbe cliff;
        public TWRiverPalette pal;
        public TWFaceted rocks = new TWFaceted();   // камень и растения: один меш и один материал
        public TWFoam foam = new TWFoam();
        public float half;
        public int rockSide = 1;                    // +1: скалистый берег слева по течению, -1: справа
        public float outletEdgeS;                   // длина оси
        public Vector2 outletTail;                  // направление течения в точке порога
        public readonly List<Vector3> exclusions = new List<Vector3>();   // x,y = центр (XZ), z = радиус
        public readonly List<Vector4> mossSpots = new List<Vector4>();    // xyz = позиция, w = радиус
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
                float k = 1f - Mathf.SmoothStep(e.z, e.z + pad, d);
                if (k > w) w = k;
            }
            return w;
        }

        public bool Grounded(Vector3 p)
        {
            if (!f.Inside(p.x, p.z)) return true;   // у кромки держит обрыв (проверяется зондом)
            if (p.y <= f.Height(p.x, p.z) + 0.08f) return true;
            if (f.RiverDistance(p.x, p.z) <= half + 0.2f && p.y <= f.RiverSurfaceAt(p.x, p.z) + 0.01f) return true;
            return false;
        }
    }
}
