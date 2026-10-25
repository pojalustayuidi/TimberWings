#if UNITY_EDITOR
// Assets/_Project/Editor/IslandV2/SoftMesh.cs (Editor)
// Остров v2, Candy-центр: маленький сборщик «мягких» мешей. Нужен костру (CandyCampfire.cs) и украшениям площадки (IslandBlockout.PlazaCandy.cs).
// Отличие от MeshAcc (там плоские грани): здесь гладкие нормали, поэтому шарики, кочки и капли пламени выглядят пухлыми, «облачными».
//
// Что умеет (все цвета передавай уже в ЛИНЕЙНОМ виде: Soft.C(0xFBE9B7) или new Color(..).linear):
//   Puff(center, radii, color, ...)          пухлый эллипсоид (облачко, камень-пончик, подушка, куст). Сверху светлее, снизу темнее (gradient)
//   Cluster(center, r, count, spread, ...)   кучка облачков (куст, облако)
//   Cyl(a, b, r0, r1, color, capColor)       цилиндр/конус со сглаженными боками (брёвна, столбы, цепь). Торцы своим цветом (кольца бревна)
//   Teardrop(base, h, r, lean, c0, c1)       капля пламени: широкое основание, мягкий изгиб, острый кончик
//   Box(center, size, rotation, color)       коробка с плоскими гранями (доски, таблички)
//   Fan(points, color, up, doubleSided)      плоский многоугольник (звезда, сердце, флажок, лепесток мозаики)
//   Quad(a, b, c, d, color, hint)            один четырёхугольник, лицом в сторону hint
//   Tri(a, b, c, color, hint)                один треугольник, лицом в сторону hint
//   Heart(c, right, up, size, color), Star(c, right, up, rOuter, rInner, points, color), Sparkle(...)  плоские фигурки в две стороны
//   ToMesh(name)                             готовый Mesh (индексы UInt32 при необходимости, нормали пересчитаны)
// Все примитивы со своими вершинами (общих нет), поэтому RecalculateNormals даёт гладкость внутри фигуры и жёсткие рёбра между фигурами.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace IslandV2
{
    public sealed class SoftMesh
    {
        public readonly List<Vector3> v = new List<Vector3>();
        public readonly List<Color> c = new List<Color>();
        public readonly List<int> t = new List<int>();

        public int Tris { get { return t.Count / 3; } }
        public int Count { get { return v.Count; } }

        // ---------------------------------------------------------------- цвет
        // sRGB hex -> линейный цвет (как W() в IslandBlockout)
        public static Color C(int hex)
        {
            float r = ((hex >> 16) & 255) / 255f, g = ((hex >> 8) & 255) / 255f, b = (hex & 255) / 255f;
            return new Color(r, g, b, 1f).linear;
        }

        public static Color Shade(Color col, float k)
        {
            return new Color(Mathf.Clamp01(col.r * k), Mathf.Clamp01(col.g * k), Mathf.Clamp01(col.b * k), 1f);
        }

        public static Color Mix(Color a, Color b, float t) { Color m = Color.Lerp(a, b, t); m.a = 1f; return m; }

        // ---------------------------------------------------------------- низкий уровень
        int Add(Vector3 p, Color col) { v.Add(p); c.Add(new Color(col.r, col.g, col.b, 1f)); return v.Count - 1; }

        // треугольник лицом в сторону hint; вырожденные пропускаются
        void T(int a, int b, int d, Vector3 hint)
        {
            Vector3 g = Vector3.Cross(v[b] - v[a], v[d] - v[a]);
            if (g.sqrMagnitude < 1e-12f) return;
            if (Vector3.Dot(g, hint) < 0f) { int tmp = b; b = d; d = tmp; }
            t.Add(a); t.Add(b); t.Add(d);
        }

        // ---------------------------------------------------------------- пухлый эллипсоид
        public void Puff(Vector3 center, Vector3 radii, Color col, float yawDeg = 0f, int seg = 8, int rings = 5, float gradient = 0.2f)
        {
            Puff(center, radii, col, Quaternion.Euler(0f, yawDeg, 0f), seg, rings, gradient);
        }

        public void Puff(Vector3 center, Vector3 radii, Color col, Quaternion q, int seg = 8, int rings = 5, float gradient = 0.2f)
        {
            seg = Mathf.Max(5, seg); rings = Mathf.Max(3, rings);
            var idx = new int[rings + 1, seg];
            for (int i = 0; i <= rings; i++)
            {
                float phi = Mathf.PI * i / rings, y = Mathf.Cos(phi), s = Mathf.Sin(phi);
                if (i == 0 || i == rings)
                {
                    int id = Add(center + q * new Vector3(0f, y * radii.y, 0f), Shade(col, 1f + gradient * y));
                    for (int j = 0; j < seg; j++) idx[i, j] = id;
                }
                else
                {
                    for (int j = 0; j < seg; j++)
                    {
                        float a = Mathf.PI * 2f * j / seg;
                        Vector3 p = new Vector3(Mathf.Cos(a) * s * radii.x, y * radii.y, Mathf.Sin(a) * s * radii.z);
                        idx[i, j] = Add(center + q * p, Shade(col, 1f + gradient * y));
                    }
                }
            }
            for (int i = 0; i < rings; i++)
            {
                for (int j = 0; j < seg; j++)
                {
                    int j2 = (j + 1) % seg;
                    int a = idx[i, j], b = idx[i, j2], e = idx[i + 1, j2], d = idx[i + 1, j];
                    Vector3 hint = (v[a] + v[b] + v[e] + v[d]) * 0.25f - center;
                    T(a, b, e, hint);
                    T(a, e, d, hint);
                }
            }
        }

        // кучка облачков: центральный крупный и вокруг поменьше
        public void Cluster(Vector3 center, float r, int count, float spread, Color col, System.Random rng, int seg = 7, int rings = 4)
        {
            Puff(center + Vector3.up * (r * 0.55f), new Vector3(r, r * 0.85f, r), col, (float)rng.NextDouble() * 360f, seg, rings);
            for (int i = 1; i < count; i++)
            {
                float a = (float)(rng.NextDouble() * Mathf.PI * 2.0), d = spread * (0.6f + (float)rng.NextDouble() * 0.5f);
                float rr = r * (0.55f + (float)rng.NextDouble() * 0.3f);
                Vector3 p = center + new Vector3(Mathf.Cos(a) * d, rr * 0.55f, Mathf.Sin(a) * d);
                Puff(p, new Vector3(rr, rr * 0.85f, rr), Shade(col, 0.96f + (float)rng.NextDouble() * 0.1f), (float)rng.NextDouble() * 360f, seg, rings);
            }
        }

        // ---------------------------------------------------------------- цилиндр / конус
        public void Cyl(Vector3 a, Vector3 b, float r0, float r1, Color col, Color capCol, bool capA = true, bool capB = true, int seg = 8)
        {
            Vector3 dir = b - a;
            if (dir.sqrMagnitude < 1e-8f) return;
            dir.Normalize();
            Vector3 u = Mathf.Abs(dir.y) < 0.95f ? Vector3.Cross(dir, Vector3.up).normalized : Vector3.Cross(dir, Vector3.right).normalized;
            Vector3 w = Vector3.Cross(dir, u);
            var ia = new int[seg]; var ib = new int[seg];
            for (int j = 0; j < seg; j++)
            {
                float ang = Mathf.PI * 2f * j / seg;
                Vector3 rad = u * Mathf.Cos(ang) + w * Mathf.Sin(ang);
                ia[j] = Add(a + rad * r0, col);
                ib[j] = Add(b + rad * r1, col);
            }
            for (int j = 0; j < seg; j++)
            {
                int j2 = (j + 1) % seg;
                Vector3 hint = (v[ia[j]] + v[ia[j2]] + v[ib[j]] + v[ib[j2]]) * 0.25f - (a + b) * 0.5f;
                hint -= dir * Vector3.Dot(hint, dir);
                T(ia[j], ia[j2], ib[j2], hint);
                T(ia[j], ib[j2], ib[j], hint);
            }
            if (capA) Disc(a, -dir, r0, capCol, u, w, seg);
            if (capB) Disc(b, dir, r1, capCol, u, w, seg);
        }

        public void Cyl(Vector3 a, Vector3 b, float r0, float r1, Color col, int seg = 8)
        {
            Cyl(a, b, r0, r1, col, col, true, true, seg);
        }

        void Disc(Vector3 center, Vector3 normal, float r, Color col, Vector3 u, Vector3 w, int seg)
        {
            int cid = Add(center, col);
            var ring = new int[seg];
            for (int j = 0; j < seg; j++)
            {
                float ang = Mathf.PI * 2f * j / seg;
                ring[j] = Add(center + (u * Mathf.Cos(ang) + w * Mathf.Sin(ang)) * r, col);
            }
            for (int j = 0; j < seg; j++) T(cid, ring[j], ring[(j + 1) % seg], normal);
        }

        // ---------------------------------------------------------------- капля пламени
        public void Teardrop(Vector3 b, float h, float rBase, Vector3 lean, Color c0, Color c1, int seg = 8, int rings = 6)
        {
            var idx = new int[rings + 1, seg];
            var ctr = new Vector3[rings + 1];
            for (int k = 0; k <= rings; k++)
            {
                float tt = (float)k / rings;
                float w = rBase * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Pow(tt, 0.7f))), 0.9f);
                ctr[k] = b + Vector3.up * (h * tt) + lean * (tt * tt);
                Color col = Color.Lerp(c0, c1, tt);
                if (k == 0 || k == rings)
                {
                    int id = Add(ctr[k], col);
                    for (int j = 0; j < seg; j++) idx[k, j] = id;
                }
                else
                {
                    for (int j = 0; j < seg; j++)
                    {
                        float a = Mathf.PI * 2f * j / seg;
                        idx[k, j] = Add(ctr[k] + new Vector3(Mathf.Cos(a) * w, 0f, Mathf.Sin(a) * w), col);
                    }
                }
            }
            for (int k = 0; k < rings; k++)
            {
                for (int j = 0; j < seg; j++)
                {
                    int j2 = (j + 1) % seg;
                    int a = idx[k, j], bb = idx[k, j2], e = idx[k + 1, j2], d = idx[k + 1, j];
                    Vector3 hint = (v[a] + v[bb] + v[e] + v[d]) * 0.25f - (ctr[k] + ctr[k + 1]) * 0.5f;
                    if (k == 0) hint += Vector3.down * 0.05f;
                    if (k == rings - 1) hint += Vector3.up * 0.05f;
                    T(a, bb, e, hint);
                    T(a, e, d, hint);
                }
            }
        }

        // ---------------------------------------------------------------- коробка с плоскими гранями
        public void Box(Vector3 center, Vector3 size, Quaternion q, Color col, float topLight = 1.08f)
        {
            Vector3 h = size * 0.5f;
            Vector3[] n = { Vector3.up, Vector3.down, Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
            for (int f = 0; f < 6; f++)
            {
                Vector3 nn = n[f];
                Vector3 u = f < 2 ? Vector3.right : (f < 4 ? Vector3.forward : Vector3.right);
                Vector3 w = Vector3.Cross(nn, u);
                u = Vector3.Cross(w, nn);
                Vector3 cc = Vector3.Scale(nn, h);
                Vector3 uu = Vector3.Scale(u, h), ww = Vector3.Scale(w, h);
                float k = f == 0 ? topLight : (f == 1 ? 0.8f : 1f);
                Color fc = Shade(col, k);
                int a = Add(center + q * (cc - uu - ww), fc), b = Add(center + q * (cc + uu - ww), fc);
                int e = Add(center + q * (cc + uu + ww), fc), d = Add(center + q * (cc - uu + ww), fc);
                Vector3 hint = q * nn;
                T(a, b, e, hint);
                T(a, e, d, hint);
            }
        }

        // ---------------------------------------------------------------- плоские фигуры
        public void Quad(Vector3 a, Vector3 b, Vector3 d, Vector3 e, Color col, Vector3 hint)
        {
            int ia = Add(a, col), ib = Add(b, col), id = Add(d, col), ie = Add(e, col);
            T(ia, ib, id, hint);
            T(ia, id, ie, hint);
        }

        // один треугольник, лицом в сторону hint (свои вершины: жёсткое ребро)
        public void Tri(Vector3 a, Vector3 b, Vector3 d, Color col, Vector3 hint)
        {
            int ia = Add(a, col), ib = Add(b, col), id = Add(d, col);
            T(ia, ib, id, hint);
        }

        // многоугольник веером от центра (для выпуклых и «звёздных» фигур), лицом вверх по up; doubleSided: вторая сторона отдельными вершинами
        public void Fan(IList<Vector3> pts, Color col, Vector3 up, bool doubleSided)
        {
            if (pts.Count < 3) return;
            Vector3 mid = Vector3.zero;
            for (int i = 0; i < pts.Count; i++) mid += pts[i];
            mid /= pts.Count;
            for (int side = 0; side < (doubleSided ? 2 : 1); side++)
            {
                Vector3 hint = side == 0 ? up : -up;
                int cid = Add(mid, col);
                var ring = new int[pts.Count];
                for (int i = 0; i < pts.Count; i++) ring[i] = Add(pts[i], col);
                for (int i = 0; i < pts.Count; i++) T(cid, ring[i], ring[(i + 1) % pts.Count], hint);
            }
        }

        // звезда в плоскости (right, up), лицом вдоль cross(right, up) и обратно
        public void Star(Vector3 center, Vector3 right, Vector3 up, float rOuter, float rInner, int points, Color col)
        {
            var pts = new Vector3[points * 2];
            for (int i = 0; i < pts.Length; i++)
            {
                float a = Mathf.PI * 0.5f + Mathf.PI * i / points;
                float r = (i & 1) == 0 ? rOuter : rInner;
                pts[i] = center + right * (Mathf.Cos(a) * r) + up * (Mathf.Sin(a) * r);
            }
            Fan(pts, col, Vector3.Cross(right, up), true);
        }

        // четырёхлучевая искорка «блёстка»
        public void Sparkle(Vector3 center, Vector3 right, Vector3 up, float r, Color col)
        {
            Star(center, right, up, r, r * 0.28f, 4, col);
        }

        public void Heart(Vector3 center, Vector3 right, Vector3 up, float size, Color col)
        {
            const int n = 18;
            var pts = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float a = Mathf.PI * 2f * i / n;
                float x = 16f * Mathf.Pow(Mathf.Sin(a), 3f);
                float y = 13f * Mathf.Cos(a) - 5f * Mathf.Cos(2f * a) - 2f * Mathf.Cos(3f * a) - Mathf.Cos(4f * a);
                pts[i] = center + right * (x / 17f * size) + up * ((y + 2f) / 17f * size);
            }
            Fan(pts, col, Vector3.Cross(right, up), true);
        }

        // ---------------------------------------------------------------- результат
        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name };
            if (v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(v);
            m.SetColors(c);
            m.SetTriangles(t, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}
#endif
