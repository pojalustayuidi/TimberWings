// Assets/_Project/Scripts/Editor/HutV2/HutMeshLib.cs   (ТОЛЬКО в папке Editor!)
// Общие низкополигональные меши для хижины и пилорамы v2: куб с фаской, цилиндры, конусы, «кляксы» (кусты, камни, дым),
// фронтон-призма, диск пилы с зубьями, водяное колесо. Все меши плоско-затенённые (вершины не общие).
// Лежат ассетами в Assets/_Project/Art/HutV2/Meshes и грузятся при повторной сборке (второй запуск быстрый).
// Единицы: куб 1x1x1; цилиндр радиус 0.5, высота 1 (ось Y); конус радиус 0.5, высота 1; клякса радиус 0.5.
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class HutMeshLib
{
    public const string Root = "Assets/_Project/Art/HutV2";
    public const string MeshDir = Root + "/Meshes";
    public const string MatDir = Root + "/Materials";

    static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();

    // ---------------------------------------------------------------- папки
    public static void EnsureFolders()
    {
        Folder("Assets", "_Project");
        Folder("Assets/_Project", "Art");
        Folder("Assets/_Project/Art", "HutV2");
        Folder(Root, "Meshes");
        Folder(Root, "Materials");
    }

    static void Folder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
    }

    // ---------------------------------------------------------------- построитель плоских мешей
    class MB
    {
        public readonly List<Vector3> v = new List<Vector3>();
        public readonly List<int> t = new List<int>();

        public void Tri(Vector3 a, Vector3 b, Vector3 c)
        {
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c);
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d) { Tri(a, b, c); Tri(a, c, d); }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name };
            m.SetVertices(v);
            m.SetTriangles(t, 0);
            m.RecalculateNormals();   // вершины не общие: нормали получаются плоскими
            m.RecalculateBounds();
            return m;
        }
    }

    static Mesh GetOrMake(string name, System.Func<MB> make)
    {
        Mesh m;
        if (cache.TryGetValue(name, out m) && m != null) return m;
        EnsureFolders();
        string path = MeshDir + "/" + name + ".asset";
        m = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (m == null)
        {
            m = make().ToMesh(name);
            AssetDatabase.CreateAsset(m, path);
        }
        cache[name] = m;
        return m;
    }

    /// <summary>Сбросить кэш в памяти (после ручного удаления папки Meshes).</summary>
    public static void ClearCache() { cache.Clear(); }

    // ---------------------------------------------------------------- куб с фаской
    /// <summary>Куб с лёгкой фаской по кромкам: деревянные детали выглядят «мультяшно мягче», чем у простого куба.</summary>
    public static Mesh SoftCube
    {
        get
        {
            return GetOrMake("HutV2_SoftCube", () =>
            {
                var b = new MB();
                const float h = 0.5f, c = 0.06f, i = h - c;
                // 6 больших граней (уменьшены на фаску)
                b.Quad(new Vector3(-i, -i, h), new Vector3(i, -i, h), new Vector3(i, i, h), new Vector3(-i, i, h));
                b.Quad(new Vector3(i, -i, -h), new Vector3(-i, -i, -h), new Vector3(-i, i, -h), new Vector3(i, i, -h));
                b.Quad(new Vector3(h, -i, i), new Vector3(h, -i, -i), new Vector3(h, i, -i), new Vector3(h, i, i));
                b.Quad(new Vector3(-h, -i, -i), new Vector3(-h, -i, i), new Vector3(-h, i, i), new Vector3(-h, i, -i));
                b.Quad(new Vector3(-i, h, i), new Vector3(i, h, i), new Vector3(i, h, -i), new Vector3(-i, h, -i));
                b.Quad(new Vector3(-i, -h, -i), new Vector3(i, -h, -i), new Vector3(i, -h, i), new Vector3(-i, -h, i));
                // вертикальные фаски
                b.Quad(new Vector3(i, -i, h), new Vector3(h, -i, i), new Vector3(h, i, i), new Vector3(i, i, h));
                b.Quad(new Vector3(h, -i, -i), new Vector3(i, -i, -h), new Vector3(i, i, -h), new Vector3(h, i, -i));
                b.Quad(new Vector3(-i, -i, -h), new Vector3(-h, -i, -i), new Vector3(-h, i, -i), new Vector3(-i, i, -h));
                b.Quad(new Vector3(-h, -i, i), new Vector3(-i, -i, h), new Vector3(-i, i, h), new Vector3(-h, i, i));
                // верхние фаски
                b.Quad(new Vector3(-i, i, h), new Vector3(i, i, h), new Vector3(i, h, i), new Vector3(-i, h, i));
                b.Quad(new Vector3(i, i, -h), new Vector3(-i, i, -h), new Vector3(-i, h, -i), new Vector3(i, h, -i));
                b.Quad(new Vector3(h, i, i), new Vector3(h, i, -i), new Vector3(i, h, -i), new Vector3(i, h, i));
                b.Quad(new Vector3(-h, i, -i), new Vector3(-h, i, i), new Vector3(-i, h, i), new Vector3(-i, h, -i));
                // нижние фаски
                b.Quad(new Vector3(i, -i, h), new Vector3(-i, -i, h), new Vector3(-i, -h, i), new Vector3(i, -h, i));
                b.Quad(new Vector3(-i, -i, -h), new Vector3(i, -i, -h), new Vector3(i, -h, -i), new Vector3(-i, -h, -i));
                b.Quad(new Vector3(h, -i, -i), new Vector3(h, -i, i), new Vector3(i, -h, i), new Vector3(i, -h, -i));
                b.Quad(new Vector3(-h, -i, i), new Vector3(-h, -i, -i), new Vector3(-i, -h, -i), new Vector3(-i, -h, i));
                // углы
                b.Tri(new Vector3(i, i, h), new Vector3(h, i, i), new Vector3(i, h, i));
                b.Tri(new Vector3(h, i, -i), new Vector3(i, i, -h), new Vector3(i, h, -i));
                b.Tri(new Vector3(-i, i, -h), new Vector3(-h, i, -i), new Vector3(-i, h, -i));
                b.Tri(new Vector3(-h, i, i), new Vector3(-i, i, h), new Vector3(-i, h, i));
                b.Tri(new Vector3(h, -i, i), new Vector3(i, -i, h), new Vector3(i, -h, i));
                b.Tri(new Vector3(i, -i, -h), new Vector3(h, -i, -i), new Vector3(i, -h, -i));
                b.Tri(new Vector3(-h, -i, -i), new Vector3(-i, -i, -h), new Vector3(-i, -h, -i));
                b.Tri(new Vector3(-i, -i, h), new Vector3(-h, -i, i), new Vector3(-i, -h, i));
                return b;
            });
        }
    }

    // ---------------------------------------------------------------- цилиндр и конус
    public static Mesh Cyl(int sides)
    {
        sides = Mathf.Clamp(sides, 5, 24);
        return GetOrMake("HutV2_Cyl" + sides, () =>
        {
            var b = new MB();
            for (int k = 0; k < sides; k++)
            {
                float a0 = k * Mathf.PI * 2f / sides, a1 = (k + 1) * Mathf.PI * 2f / sides;
                Vector3 p0 = new Vector3(Mathf.Cos(a0) * 0.5f, 0, Mathf.Sin(a0) * 0.5f);
                Vector3 p1 = new Vector3(Mathf.Cos(a1) * 0.5f, 0, Mathf.Sin(a1) * 0.5f);
                Vector3 lo0 = p0 + Vector3.down * 0.5f, lo1 = p1 + Vector3.down * 0.5f;
                Vector3 hi0 = p0 + Vector3.up * 0.5f, hi1 = p1 + Vector3.up * 0.5f;
                b.Quad(lo1, lo0, hi0, hi1);                               // стенка
                b.Tri(new Vector3(0, 0.5f, 0), hi1, hi0);                 // верх
                b.Tri(new Vector3(0, -0.5f, 0), lo0, lo1);                // низ
            }
            return b;
        });
    }

    /// <summary>Бревно: цилиндр с ДВУМЯ сабмешами (0 = ствол, 1 = торцы). Один Renderer, поэтому BuildFx роняет бревно целиком, а не по частям.</summary>
    public static Mesh LogCyl(int sides)
    {
        sides = Mathf.Clamp(sides, 5, 24);
        string name = "HutV2_LogCyl" + sides;
        Mesh m;
        if (cache.TryGetValue(name, out m) && m != null) return m;
        EnsureFolders();
        string path = MeshDir + "/" + name + ".asset";
        m = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (m == null)
        {
            var side = new MB();
            var caps = new MB();
            const float capOut = 0.015f;   // торец чуть выступает и чуть шире: виден светлый «срез» с кольцом
            for (int k = 0; k < sides; k++)
            {
                float a0 = k * Mathf.PI * 2f / sides, a1 = (k + 1) * Mathf.PI * 2f / sides;
                Vector3 p0 = new Vector3(Mathf.Cos(a0) * 0.5f, 0, Mathf.Sin(a0) * 0.5f);
                Vector3 p1 = new Vector3(Mathf.Cos(a1) * 0.5f, 0, Mathf.Sin(a1) * 0.5f);
                side.Quad(p1 + Vector3.down * 0.5f, p0 + Vector3.down * 0.5f, p0 + Vector3.up * 0.5f, p1 + Vector3.up * 0.5f);
                Vector3 c0 = p0 * 0.93f, c1 = p1 * 0.93f;
                caps.Tri(new Vector3(0, 0.5f + capOut, 0), c1 + Vector3.up * (0.5f + capOut), c0 + Vector3.up * (0.5f + capOut));
                caps.Tri(new Vector3(0, -0.5f - capOut, 0), c0 + Vector3.down * (0.5f + capOut), c1 + Vector3.down * (0.5f + capOut));
                // узкий «поясок» вокруг торца, чтобы между стволом и торцом не было щели
                caps.Quad(p1 * 0.93f + Vector3.up * (0.5f + capOut), p0 * 0.93f + Vector3.up * (0.5f + capOut), p0 + Vector3.up * 0.5f, p1 + Vector3.up * 0.5f);
                caps.Quad(p0 * 0.93f + Vector3.down * (0.5f + capOut), p1 * 0.93f + Vector3.down * (0.5f + capOut), p1 + Vector3.down * 0.5f, p0 + Vector3.down * 0.5f);
            }
            m = new Mesh { name = name };
            var verts = new List<Vector3>(side.v);
            int off = verts.Count;
            verts.AddRange(caps.v);
            var t0 = new List<int>(side.t);
            var t1 = new List<int>();
            for (int i = 0; i < caps.t.Count; i++) t1.Add(caps.t[i] + off);
            m.SetVertices(verts);
            m.subMeshCount = 2;
            m.SetTriangles(t0, 0);
            m.SetTriangles(t1, 1);
            m.RecalculateNormals();
            m.RecalculateBounds();
            AssetDatabase.CreateAsset(m, path);
        }
        cache[name] = m;
        return m;
    }

    public static Mesh Cone(int sides)
    {
        sides = Mathf.Clamp(sides, 5, 24);
        return GetOrMake("HutV2_Cone" + sides, () =>
        {
            var b = new MB();
            Vector3 apex = new Vector3(0, 0.5f, 0);
            for (int k = 0; k < sides; k++)
            {
                float a0 = k * Mathf.PI * 2f / sides, a1 = (k + 1) * Mathf.PI * 2f / sides;
                Vector3 p0 = new Vector3(Mathf.Cos(a0) * 0.5f, -0.5f, Mathf.Sin(a0) * 0.5f);
                Vector3 p1 = new Vector3(Mathf.Cos(a1) * 0.5f, -0.5f, Mathf.Sin(a1) * 0.5f);
                b.Tri(p1, p0, apex);
                b.Tri(new Vector3(0, -0.5f, 0), p0, p1);
            }
            return b;
        });
    }

    /// <summary>Трёхгранная призма (фронтон): ширина по X = 1, глубина по Z = 1, высота по Y = 1, вершина вверху по центру.</summary>
    public static Mesh Prism
    {
        get
        {
            return GetOrMake("HutV2_Prism", () =>
            {
                var b = new MB();
                Vector3 fl = new Vector3(-0.5f, -0.5f, 0.5f), fr = new Vector3(0.5f, -0.5f, 0.5f), ft = new Vector3(0, 0.5f, 0.5f);
                Vector3 bl = new Vector3(-0.5f, -0.5f, -0.5f), br = new Vector3(0.5f, -0.5f, -0.5f), bt = new Vector3(0, 0.5f, -0.5f);
                b.Tri(fl, fr, ft);                 // перед
                b.Tri(br, bl, bt);                 // зад
                b.Quad(fl, ft, bt, bl);            // левый скат
                b.Quad(fr, br, bt, ft);            // правый скат
                b.Quad(bl, br, fr, fl);            // низ
                return b;
            });
        }
    }

    /// <summary>Клякса: низкополигональный «шар» с неровностями (кусты, камни, мох, облачка дыма). seed 0..3 даёт разные формы.</summary>
    public static Mesh Blob(int seed)
    {
        seed = Mathf.Clamp(seed, 0, 3);
        return GetOrMake("HutV2_Blob" + seed, () =>
        {
            var b = new MB();
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            Vector3[] v =
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
            };
            int[] f =
            {
                0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1
            };
            var rnd = new System.Random(1000 + seed * 77);
            var jit = new Vector3[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                float k = 0.5f * (0.86f + (float)rnd.NextDouble() * 0.26f);
                jit[i] = v[i].normalized * k;
                jit[i].y *= 0.9f;
            }
            for (int i = 0; i < f.Length; i += 3) b.Tri(jit[f[i]], jit[f[i + 1]], jit[f[i + 2]]);
            return b;
        });
    }

    // ---------------------------------------------------------------- пила и колесо
    static Vector3 P(float a, float r) { return new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f); }

    /// <summary>Диск круглой пилы. Лежит в плоскости XY, ось вращения Z. Радиус 0.5 по краю зубьев, толщина 1 по Z (масштабируй тоньше), втулка торчит.</summary>
    public static Mesh SawBlade(int teeth)
    {
        teeth = Mathf.Clamp(teeth, 8, 40);
        return GetOrMake("HutV2_SawBlade" + teeth, () =>
        {
            var b = new MB();
            float zf = 0.5f, zb = -0.5f;
            float rOut = 0.5f, rIn = 0.43f, rHub = 0.09f;
            for (int k = 0; k < teeth; k++)
            {
                // зуб «пилой»: пологий подъём по окружности и резкий спад
                float a0 = k * Mathf.PI * 2f / teeth;
                float a1 = (k + 0.85f) * Mathf.PI * 2f / teeth;
                float a2 = (k + 1f) * Mathf.PI * 2f / teeth;
                Vector3 in0 = P(a0, rIn), tip = P(a1, rOut), in2 = P(a2, rIn);
                b.Tri(new Vector3(0, 0, zf), new Vector3(in0.x, in0.y, zf), new Vector3(tip.x, tip.y, zf));
                b.Tri(new Vector3(0, 0, zf), new Vector3(tip.x, tip.y, zf), new Vector3(in2.x, in2.y, zf));
                b.Tri(new Vector3(0, 0, zb), new Vector3(tip.x, tip.y, zb), new Vector3(in0.x, in0.y, zb));
                b.Tri(new Vector3(0, 0, zb), new Vector3(in2.x, in2.y, zb), new Vector3(tip.x, tip.y, zb));
                b.Quad(new Vector3(in0.x, in0.y, zb), new Vector3(tip.x, tip.y, zb), new Vector3(tip.x, tip.y, zf), new Vector3(in0.x, in0.y, zf));
                b.Quad(new Vector3(tip.x, tip.y, zb), new Vector3(in2.x, in2.y, zb), new Vector3(in2.x, in2.y, zf), new Vector3(tip.x, tip.y, zf));
            }
            // втулка торчит с обеих сторон
            const int hs = 8;
            const float zz = 0.9f;
            for (int k = 0; k < hs; k++)
            {
                float a0 = k * Mathf.PI * 2f / hs, a1 = (k + 1) * Mathf.PI * 2f / hs;
                Vector3 p0 = P(a0, rHub), p1 = P(a1, rHub);
                b.Quad(new Vector3(p1.x, p1.y, -zz), new Vector3(p0.x, p0.y, -zz), new Vector3(p0.x, p0.y, zz), new Vector3(p1.x, p1.y, zz));
                b.Tri(new Vector3(0, 0, zz), new Vector3(p1.x, p1.y, zz), new Vector3(p0.x, p0.y, zz));
                b.Tri(new Vector3(0, 0, -zz), new Vector3(p0.x, p0.y, -zz), new Vector3(p1.x, p1.y, -zz));
            }
            return b;
        });
    }

    /// <summary>Водяное колесо: ось Z, радиус 0.5, толщина 1 по Z, лопасти торчат за обод.</summary>
    public static Mesh Paddle(int spokes)
    {
        spokes = Mathf.Clamp(spokes, 6, 16);
        return GetOrMake("HutV2_Paddle" + spokes, () =>
        {
            var b = new MB();
            float rOut = 0.5f, rRim = 0.40f, rHub = 0.08f;
            for (int k = 0; k < spokes; k++)
            {
                float a = k * Mathf.PI * 2f / spokes;
                Vector3 dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                Vector3 side = new Vector3(-dir.y, dir.x, 0f);
                AddBox(b, dir * (rHub + rRim) * 0.5f, dir, side, (rRim - rHub) * 0.5f, 0.03f, 0.28f);   // спица
                AddBox(b, dir * (rRim + rOut) * 0.5f, dir, side, (rOut - rRim) * 0.5f, 0.10f, 0.5f);    // лопасть
            }
            const int hs = 8;
            for (int k = 0; k < hs; k++)
            {
                float a0 = k * Mathf.PI * 2f / hs, a1 = (k + 1) * Mathf.PI * 2f / hs;
                Vector3 p0 = P(a0, 0.11f), p1 = P(a1, 0.11f);
                b.Quad(new Vector3(p1.x, p1.y, -0.5f), new Vector3(p0.x, p0.y, -0.5f), new Vector3(p0.x, p0.y, 0.5f), new Vector3(p1.x, p1.y, 0.5f));
                b.Tri(new Vector3(0, 0, 0.5f), new Vector3(p1.x, p1.y, 0.5f), new Vector3(p0.x, p0.y, 0.5f));
                b.Tri(new Vector3(0, 0, -0.5f), new Vector3(p0.x, p0.y, -0.5f), new Vector3(p1.x, p1.y, -0.5f));
            }
            int rs = spokes * 2;
            for (int k = 0; k < rs; k++)
            {
                float a0 = k * Mathf.PI * 2f / rs, a1 = (k + 1) * Mathf.PI * 2f / rs;
                float ri = rRim - 0.04f, ro = rRim + 0.01f;
                Vector3 i0 = P(a0, ri), i1 = P(a1, ri), o0 = P(a0, ro), o1 = P(a1, ro);
                float z1 = 0.35f, z0 = -z1;
                b.Quad(new Vector3(o0.x, o0.y, z0), new Vector3(o1.x, o1.y, z0), new Vector3(o1.x, o1.y, z1), new Vector3(o0.x, o0.y, z1));
                b.Quad(new Vector3(i1.x, i1.y, z0), new Vector3(i0.x, i0.y, z0), new Vector3(i0.x, i0.y, z1), new Vector3(i1.x, i1.y, z1));
                b.Quad(new Vector3(i0.x, i0.y, z1), new Vector3(o0.x, o0.y, z1), new Vector3(o1.x, o1.y, z1), new Vector3(i1.x, i1.y, z1));
                b.Quad(new Vector3(o0.x, o0.y, z0), new Vector3(i0.x, i0.y, z0), new Vector3(i1.x, i1.y, z0), new Vector3(o1.x, o1.y, z0));
            }
            return b;
        });
    }

    // коробка, ориентированная по (dir, side, Z): половины размеров hd вдоль dir, hs вдоль side, hz вдоль Z
    static void AddBox(MB b, Vector3 c, Vector3 dir, Vector3 side, float hd, float hs, float hz)
    {
        Vector3 z = Vector3.forward * hz;
        Vector3 d = dir * hd, s = side * hs;
        Vector3 p000 = c - d - s - z, p100 = c + d - s - z, p110 = c + d + s - z, p010 = c - d + s - z;
        Vector3 p001 = c - d - s + z, p101 = c + d - s + z, p111 = c + d + s + z, p011 = c - d + s + z;
        b.Quad(p001, p101, p111, p011);
        b.Quad(p100, p000, p010, p110);
        b.Quad(p000, p001, p011, p010);
        b.Quad(p101, p100, p110, p111);
        b.Quad(p010, p011, p111, p110);
        b.Quad(p000, p100, p101, p001);
    }
}
#endif
