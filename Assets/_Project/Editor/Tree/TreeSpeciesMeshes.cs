#if UNITY_EDITOR
// Assets/_Project/Editor/Tree/TreeSpeciesMeshes.cs (Editor)
// Процедурные меши видов деревьев (Oak = старый дуб): Cedar, Birch, Autumn, Willow_Dead, Plain (обычное лиственное = Tree_Random).
// Один меш на LOD, до 4 сабмешей (каждый вид задаёт, что в них лежит, см. TreeSpeciesBuilder.MatNames):
//   Cedar:   0 кора, 1 хвоя (середина яруса), 2 тёмная юбка/днище ярусов, 3 светлые кончики лап
//   Birch:   0 кора, 1 листва, 2 тёмные отметины, 3 тёмный низ кроны
//   Autumn:  0 кора, 1 листва A, 2 листва B, 3 тёмный низ кроны     Willow: 0 кора, 1 висячие ветки
//   Plain:   0 кора, 1 листва, 2 тёмный низ кроны                   Oak: 0 кора, 1 листва, 2 листва B, 3 тёмный низ кроны
// Flat shading: у каждого треугольника свои вершины и своя нормаль (как в TreeMeshFactory).
// Всё детерминировано: форма зависит только от (вид, вариация, seed, lod).
// Масштаб в метрах, пивот у основания ствола, Y вверх. Высота = TreeParams.H при масштабе корня 1 (зрелое дерево).
using System;
using System.Collections.Generic;
using UnityEngine;

public enum TreeSpecies { Cedar = 0, Birch = 1, Autumn = 2, Willow = 3, Plain = 4, Oak = 5 }

// Габариты вида для хитбоксов (читает TreeSpeciesInfo) и для пня. Всё в локальных метрах корня.
public struct TreeParams
{
    public float H;          // высота дерева
    public float pickR, pickH;   // зона выбора (триггер по кроне)
    public float trunkR, trunkH; // сплошной ствол
    public float stumpR;     // радиус пня
}

// ------------------------------------------------------------------ построитель граней
public sealed class TreeMB
{
    readonly List<Vector3> v = new List<Vector3>(1024), n = new List<Vector3>(1024);
    readonly List<int>[] t = { new List<int>(2048), new List<int>(2048), new List<int>(512), new List<int>(512) };

    static readonly List<Vector3>[] icoV = new List<Vector3>[2];
    static readonly List<int>[] icoF = new List<int>[2];

    public int TotalTris { get { return (t[0].Count + t[1].Count + t[2].Count + t[3].Count) / 3; } }

    // Треугольник. inside: точка внутри формы. Если нормаль смотрит к ней, порядок вершин разворачивается:
    // так не надо думать об обходе. Нормаль = cross(b-a, c-a), как в TreeMeshFactory.
    public void Tri(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 inside)
    {
        Vector3 nn = Vector3.Cross(b - a, c - a);
        if (nn.sqrMagnitude < 1e-10f) return;   // вырожденный (радиус 0 на кончике)
        if (Vector3.Dot(nn, (a + b + c) / 3f - inside) < 0f) { Vector3 tmp = b; b = c; c = tmp; nn = -nn; }
        nn.Normalize();
        int i = v.Count;
        v.Add(a); v.Add(b); v.Add(c);
        n.Add(nn); n.Add(nn); n.Add(nn);
        t[sub].Add(i); t[sub].Add(i + 1); t[sub].Add(i + 2);
    }

    // труба по ломаной с радиусами в каждой точке (боковая стенка, без днищ). capTop: верхняя крышка веером
    public void Tube(int sub, Vector3[] p, float[] r, int sides, bool capTop)
    {
        int m = p.Length;
        if (m < 2) return;
        Vector3 d0 = (p[1] - p[0]).normalized;
        Vector3 refv = Mathf.Abs(d0.y) > 0.8f ? Vector3.forward : Vector3.up;
        var ring = new Vector3[m][];
        Vector3 lastAx = d0;
        for (int i = 0; i < m; i++)
        {
            Vector3 ax = i == 0 ? p[1] - p[0] : (i == m - 1 ? p[m - 1] - p[m - 2] : p[i + 1] - p[i - 1]);
            ax.Normalize();
            lastAx = ax;
            Vector3 u = Vector3.Cross(ax, refv);
            if (u.sqrMagnitude < 1e-6f) u = Vector3.Cross(ax, Vector3.right);
            u.Normalize();
            Vector3 w = Vector3.Cross(u, ax).normalized;
            ring[i] = new Vector3[sides];
            for (int j = 0; j < sides; j++)
            {
                float a = j / (float)sides * Mathf.PI * 2f;
                ring[i][j] = p[i] + (u * Mathf.Cos(a) + w * Mathf.Sin(a)) * r[i];
            }
        }
        for (int i = 0; i < m - 1; i++)
        {
            Vector3 inside = (p[i] + p[i + 1]) * 0.5f;
            for (int j = 0; j < sides; j++)
            {
                int j1 = (j + 1) % sides;
                Vector3 a0 = ring[i][j], a1 = ring[i][j1], b0 = ring[i + 1][j], b1 = ring[i + 1][j1];
                Tri(sub, a0, b0, b1, inside);
                Tri(sub, a0, b1, a1, inside);
            }
        }
        if (capTop)
        {
            Vector3 top = p[m - 1] + lastAx * 0.0f;
            for (int j = 0; j < sides; j++)
                Tri(sub, top, ring[m - 1][j], ring[m - 1][(j + 1) % sides], p[m - 2]);
        }
    }

    // труба по функциям положения и радиуса от t (0..1): segs отрезков
    public void TubeF(int sub, Func<float, Vector3> pos, Func<float, float> rad, int segs, int sides, bool capTop)
    {
        var p = new Vector3[segs + 1];
        var r = new float[segs + 1];
        for (int i = 0; i <= segs; i++) { float tt = i / (float)segs; p[i] = pos(tt); r[i] = rad(tt); }
        Tube(sub, p, r, sides, capTop);
    }

    // плоская крышка (нормаль вверх)
    public void Cap(int sub, Vector3 c, float r, int sides)
    {
        for (int i = 0; i < sides; i++)
        {
            float a0 = i / (float)sides * Mathf.PI * 2f, a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
            Vector3 p0 = c + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * r;
            Vector3 p1 = c + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * r;
            Tri(sub, c, p0, p1, c + Vector3.down);
        }
    }

    // «пухлый» шар: level 1 = 80 граней, level 0 = 20 граней. Радиус каждой вершины гуляет, низ приплюснут (как в TreeMeshFactory).
    // subUnder >= 0: грани, смотрящие заметно вниз (нормаль.y < -0.35), уходят в этот сабмеш (тёмный низ кляксы)
    public void Blob(int sub, Vector3 center, Vector3 radii, int seed, int level, float jit = 0.26f, int subUnder = -1)
    {
        level = Mathf.Clamp(level, 0, 1);
        BuildIco(level);
        var iv = icoV[level];
        var iff = icoF[level];
        var p = new Vector3[iv.Count];
        for (int i = 0; i < p.Length; i++)
        {
            Vector3 u = iv[i];
            float j = (1f - jit * 0.5f) + jit * H(seed, i);
            if (u.y < 0f) u.y *= 0.65f;
            p[i] = center + Vector3.Scale(u * j, radii);
        }
        for (int f = 0; f < iff.Count; f += 3)
        {
            Vector3 a = p[iff[f]], b = p[iff[f + 1]], c = p[iff[f + 2]];
            int s = sub;
            if (subUnder >= 0)
            {
                Vector3 nn = Vector3.Cross(b - a, c - a);
                if (Vector3.Dot(nn, (a + b + c) / 3f - center) < 0f) nn = -nn;
                if (nn.normalized.y < -0.35f) s = subUnder;
            }
            Tri(s, a, b, c, center);
        }
    }

    // ярус ели: плоский конус с «лапами» (чётные точки кольца длиннее и ниже). detail: второе кольцо, внешняя полоса граней (кончики лап)
    // уходит в светлый сабмеш subTip, середина яруса в subTop; без detail весь конус в subTip.
    // underside: тёмная «юбка» под краем яруса (видна спереди сверху под 55°, из-за неё ярусы читаются как ступени) и днище в subUnder
    public void Tier(Vector3 c, float r, float h, int sides, float droop, float skirt, int seed, bool underside, bool detail, int subTop, int subTip, int subUnder)
    {
        Vector3 apex = c + Vector3.up * h;
        var outer = new Vector3[sides];
        for (int j = 0; j < sides; j++)
        {
            float a = j / (float)sides * Mathf.PI * 2f;
            bool paw = (j % 2) == 0;
            float rr = r * (paw ? 1.16f : 0.80f) * (0.93f + 0.14f * H(seed, j));
            float dy = -droop * (paw ? 1f : 0.30f) * (0.85f + 0.30f * H(seed, j + 50));
            outer[j] = c + new Vector3(Mathf.Cos(a) * rr, dy, Mathf.Sin(a) * rr);
        }
        Vector3 inside = c + Vector3.up * h * 0.35f;

        if (detail)
        {
            var mid = new Vector3[sides];
            for (int j = 0; j < sides; j++)
            {
                float a = (j + 0.5f) / sides * Mathf.PI * 2f;
                float rm = r * 0.52f * (0.90f + 0.20f * H(seed, j + 100));
                mid[j] = c + new Vector3(Mathf.Cos(a) * rm, h * 0.42f + 0.05f * ((j % 2) == 1 ? 1f : -1f), Mathf.Sin(a) * rm);
            }
            for (int j = 0; j < sides; j++)
                Tri(subTop, apex, mid[(j + sides - 1) % sides], mid[j], inside);
            for (int j = 0; j < sides; j++)
            {
                int j1 = (j + 1) % sides;
                Tri(subTip, mid[j], outer[j], outer[j1], inside);
                Tri(subTip, mid[j], outer[j1], mid[j1], inside);
            }
        }
        else
        {
            for (int j = 0; j < sides; j++)
                Tri(subTip, apex, outer[j], outer[(j + 1) % sides], inside);
        }

        if (underside)
        {
            var low = new Vector3[sides];
            for (int j = 0; j < sides; j++)
            {
                Vector3 p = outer[j];
                Vector3 d = new Vector3(p.x - c.x, 0f, p.z - c.z);
                low[j] = p - Vector3.up * skirt * ((j % 2) == 0 ? 1f : 0.6f) - d * 0.12f;
            }
            Vector3 skIn = c + Vector3.up * h * 0.5f;
            for (int j = 0; j < sides; j++)
            {
                int j1 = (j + 1) % sides;
                Tri(subUnder, outer[j], low[j], low[j1], skIn);
                Tri(subUnder, outer[j], low[j1], outer[j1], skIn);
            }
            Vector3 under = c + Vector3.up * (-skirt * 0.4f);
            Vector3 far = c + Vector3.up * (h * 6f);   // «внутри» высоко над ярусом: нормаль днища точно смотрит вниз
            for (int j = 0; j < sides; j++)
                Tri(subUnder, under, low[(j + 1) % sides], low[j], far);
        }
    }

    public static float H(int a, int b)
    {
        unchecked
        {
            uint h = (uint)(a * 374761393 + b * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
        }
    }

    static void BuildIco(int level)
    {
        if (icoV[level] != null) return;
        float k = (1f + Mathf.Sqrt(5f)) * 0.5f;
        var vs = new List<Vector3>
        {
            new Vector3(-1, k, 0), new Vector3(1, k, 0), new Vector3(-1, -k, 0), new Vector3(1, -k, 0),
            new Vector3(0, -1, k), new Vector3(0, 1, k), new Vector3(0, -1, -k), new Vector3(0, 1, -k),
            new Vector3(k, 0, -1), new Vector3(k, 0, 1), new Vector3(-k, 0, -1), new Vector3(-k, 0, 1)
        };
        for (int i = 0; i < vs.Count; i++) vs[i] = vs[i].normalized;
        int[] f0 =
        {
            0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
            3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1
        };
        if (level == 0)
        {
            icoV[0] = vs;
            icoF[0] = new List<int>(f0);
            return;
        }
        var fs = new List<int>(240);
        var mid = new Dictionary<long, int>();
        Func<int, int, int> Mid = (a, b) =>
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            int m;
            if (mid.TryGetValue(key, out m)) return m;
            vs.Add(((vs[a] + vs[b]) * 0.5f).normalized);
            m = vs.Count - 1;
            mid[key] = m;
            return m;
        };
        for (int f = 0; f < f0.Length; f += 3)
        {
            int a = f0[f], b = f0[f + 1], c = f0[f + 2];
            int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
            fs.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
        }
        icoV[1] = vs;
        icoF[1] = fs;
    }

    // Сабмеши сжимаются до последнего непустого: рендерер не должен получать больше материалов, чем сабмешей
    public Mesh Build(string name)
    {
        int subs = 1;
        for (int s = 0; s < 4; s++) if (t[s].Count > 0) subs = s + 1;
        var m = new Mesh { name = name };
        m.SetVertices(v);
        m.SetNormals(n);
        m.subMeshCount = subs;
        for (int s = 0; s < subs; s++) m.SetTriangles(t[s], s);
        m.RecalculateBounds();
        return m;
    }
}

// ------------------------------------------------------------------ виды
public static class TreeSpeciesMeshes
{
    // габариты по виду и вариации (v = 0..3; у Autumn, Oak, Willow_Dead только 0..2)
    public static TreeParams GetParams(TreeSpecies sp, int v)
    {
        var p = new TreeParams();
        switch (sp)
        {
            case TreeSpecies.Cedar:
                p.H = new[] { 6.2f, 7.0f, 7.8f, 6.6f }[v];
                p.pickR = p.H * 0.11f; p.trunkR = 0.32f; p.trunkH = 1.4f; p.stumpR = 0.38f;
                break;
            case TreeSpecies.Birch:
                p.H = new[] { 4.6f, 5.2f, 5.8f, 5.2f }[v];
                p.pickR = p.H * 0.14f; p.trunkR = v == 1 ? 0.20f : 0.30f; p.trunkH = 1.6f; p.stumpR = v == 1 ? 0.20f : 0.26f;
                break;
            case TreeSpecies.Autumn:      // жёлтое, редкое, акцент
                p.H = new[] { 5.1f, 5.5f, 5.0f }[v];
                p.pickR = p.H * 0.17f; p.trunkR = 0.45f; p.trunkH = 1.8f; p.stumpR = 0.42f;
                break;
            case TreeSpecies.Willow:
                p.H = new[] { 4.2f, 4.6f, 5.0f }[v];
                p.pickR = p.H * 0.20f; p.trunkR = 0.34f; p.trunkH = 1.8f; p.stumpR = 0.32f;
                break;
            case TreeSpecies.Oak:         // старый дуб: крупный, толстый ствол, широкая крона
                p.H = new[] { 7.6f, 8.0f, 8.5f }[v];
                p.pickR = 1.0f; p.trunkR = 0.58f; p.trunkH = 2.4f; p.stumpR = 0.60f;
                break;
            default:                      // Plain: зона выбора как у зрелого Tree_Random (TreeCollision: PickR 0.70, PickH 4.3), ствол толще (0.36 вместо 0.32), как и мешу
                p.H = new[] { 4.9f, 5.3f, 4.7f, 5.6f }[v];
                p.pickR = 0.70f; p.pickH = 4.3f; p.trunkR = 0.36f; p.trunkH = 1.8f; p.stumpR = 0.36f;
                break;
        }
        if (p.pickH <= 0f) p.pickH = p.H;
        return p;
    }

    public static Mesh BuildTree(TreeSpecies sp, int v, int seed, int lod, string name)
    {
        var b = new TreeMB();
        var p = GetParams(sp, v);
        int s = seed * 7919 + (int)sp * 101;
        switch (sp)
        {
            case TreeSpecies.Cedar: Cedar(b, v, s, lod, p); break;
            case TreeSpecies.Birch: Birch(b, v, s, lod, p); break;
            case TreeSpecies.Autumn: Autumn(b, v, s, lod, p); break;
            case TreeSpecies.Willow: Willow(b, v, s, lod, p); break;
            case TreeSpecies.Oak: Oak(b, v, s, lod, p); break;
            default: Plain(b, v, s, lod, p); break;
        }
        return b.Build(name);
    }

    // пень: основание на -0.25, верх на 0.2 (ResourceItem.ChopDown ставит пень на +0.2 над корнем), светлый срез в сабмеше 1
    public static Mesh BuildStump(float r, string name)
    {
        var b = new TreeMB();
        b.TubeF(0, t => new Vector3(0f, Mathf.Lerp(-0.25f, 0.2f, t), 0f), t => Mathf.Lerp(r * 1.12f, r, t), 1, 8, false);
        b.Cap(1, new Vector3(0f, 0.2f, 0f), r, 8);
        return b.Build(name);
    }

    static float Rf(System.Random r, float a, float bb) { return a + (float)r.NextDouble() * (bb - a); }

    // ------------------------------------------------------------ крона из крупных гранёных клякс
    // Раскладка детерминирована по seed и НЕ зависит от LOD (LOD1 берёт крупные кляксы, LOD2 три самые большие), иначе при смене LOD крона «прыгает».
    //   0: главная кляксa (радиус ~R), 1: верхняя (светлая верхушка), дальше по спирали ближе к центру (0.48-0.82 R), чтобы крона читалась круглой, а не рваной: каждая третья крупная, остальные мелкие.
    //   squash = вертикальный множитель радиусов. Низ каждой клякс приплюснут (TreeMB.Blob), градиент TreeToon делает низ тёмным.
    struct Clump { public int idx; public Vector3 c; public float rad; public int sub; }

    static void Crown(TreeMB b, int seed, int lod, Vector3 center, float R, int count, float squash, int subA, int subB, float fracB, int subUnder)
    {
        var rng = new System.Random(seed ^ 0x51ED);
        var list = new List<Clump>(count);
        for (int i = 0; i < count; i++)
        {
            Vector3 c;
            float rad;
            int sub = subA;
            if (i == 0) { c = center + new Vector3(0f, -0.04f * R * squash, 0f); rad = R * 1.02f; }
            else if (i == 1)
            {
                c = center + new Vector3(Rf(rng, -0.20f, 0.20f) * R, 0.52f * R * squash, Rf(rng, -0.20f, 0.20f) * R);
                rad = R * Rf(rng, 0.62f, 0.74f);
            }
            else
            {
                float a = i * 2.39996f + Rf(rng, -0.30f, 0.30f);
                bool big = (i % 3) == 2;
                rad = R * (big ? Rf(rng, 0.60f, 0.74f) : Rf(rng, 0.34f, 0.50f));
                float rho = R * Rf(rng, 0.48f, 0.82f);
                float dy = R * squash * Rf(rng, -0.42f, 0.28f);
                c = center + new Vector3(Mathf.Cos(a) * rho, dy, Mathf.Sin(a) * rho);
                if (Rf(rng, 0f, 1f) < fracB) sub = subB;
            }
            list.Add(new Clump { idx = i, c = c, rad = rad, sub = sub });
        }

        if (lod == 1) list = list.FindAll(x => x.rad >= R * 0.42f);
        else if (lod == 2)
        {
            list.Sort((x, y) => y.rad.CompareTo(x.rad));
            if (list.Count > 3) list.RemoveRange(3, list.Count - 3);
        }

        int level = lod == 0 ? 1 : 0;
        float jit = lod == 0 ? 0.27f : 0.22f;
        for (int i = 0; i < list.Count; i++)
        {
            Clump k = list[i];
            b.Blob(k.sub, k.c, new Vector3(k.rad, k.rad * squash, k.rad), seed + k.idx * 5, level, jit, subUnder);
        }
    }

    // ствол с лёгким наклоном, расширением у корня и ветвями внутрь кроны + крона из клякс. Общий для Plain, Autumn и Oak
    static void Broadleaf(TreeMB b, int seed, int lod, float cy, float R, int count, float squash, float lean,
        float r0, float r1, float flare, int branches, float branchR, int subA, int subB, float fracB, int subUnder)
    {
        var rng = new System.Random(seed ^ 0x2F6B);
        int tSides = lod == 0 ? 8 : (lod == 1 ? 5 : 4);
        int tSegs = lod == 0 ? 3 : (lod == 1 ? 2 : 1);
        float trunkTop = cy - 0.35f * R;

        Func<float, Vector3> trunk = t => new Vector3(lean * t * t, Mathf.Lerp(-0.1f, trunkTop, t), 0f);
        b.TubeF(0, trunk, t => Mathf.Lerp(r0, r1, t) + flare * (1f - t) * (1f - t) * (1f - t), tSegs, tSides, false);

        if (lod == 0)
        {
            for (int k = 0; k < branches; k++)
            {
                float phi = k / (float)branches * Mathf.PI * 2f + Rf(rng, -0.4f, 0.4f);
                Vector3 outd = new Vector3(Mathf.Cos(phi), 0f, Mathf.Sin(phi));
                Vector3 p0 = trunk(Rf(rng, 0.65f, 0.9f));
                Vector3 p2 = new Vector3(lean, cy - 0.1f * R + Rf(rng, -0.2f, 0.2f) * R, 0f) + outd * (R * Rf(rng, 0.45f, 0.7f));
                Vector3 p1 = (p0 + p2) * 0.5f + Vector3.up * 0.1f;
                b.Tube(0, new[] { p0, p1, p2 }, new[] { branchR, branchR * 0.7f, branchR * 0.45f }, 5, false);
            }
        }

        Crown(b, seed, lod, new Vector3(lean, cy, 0f), R, count, squash, subA, subB, fracB, subUnder);
    }

    // ------------------------------------------------------------ Plain (Tree_Random): основное пышное лиственное, 6-9 клякс
    static void Plain(TreeMB b, int v, int seed, int lod, TreeParams p)
    {
        float[] R = { 1.80f, 1.95f, 1.70f, 2.08f };      // крона крупнее (+~16%), ствол короче и толще; верхушка на прежней высоте
        float[] cy = { 2.7f, 2.9f, 2.6f, 3.0f };
        int[] cnt = { 7, 8, 6, 9 };
        float[] sq = { 0.82f, 0.78f, 0.90f, 0.80f };
        float[] lean = { 0.05f, -0.15f, 0.10f, -0.08f };
        Broadleaf(b, seed, lod, cy[v], R[v], cnt[v], sq[v], lean[v], 0.34f, 0.20f, 0.10f, 2, 0.14f, 1, 1, 0f, 2);
    }

    // ------------------------------------------------------------ Autumn: жёлтое дерево-акцент, шире и круглее, два тона жёлтого (сабмеш 1 и 2)
    static void Autumn(TreeMB b, int v, int seed, int lod, TreeParams p)
    {
        float[] R = { 1.70f, 1.90f, 1.60f };
        float[] cy = { 3.0f, 3.2f, 3.0f };
        int[] cnt = { 7, 9, 8 };
        float[] lean = { 0.05f, -0.10f, 0.12f };
        Broadleaf(b, seed, lod, cy[v], R[v], cnt[v], 0.80f, lean[v], 0.42f, 0.22f, 0.12f, 3, 0.13f, 1, 2, 0.35f, 3);
    }

    // ------------------------------------------------------------ Oak: старый дуб, толстый ствол с широким основанием, широкая плоская крона из 11-13 клякс
    static void Oak(TreeMB b, int v, int seed, int lod, TreeParams p)
    {
        float[] R = { 2.70f, 2.90f, 3.10f };
        float[] cy = { 5.0f, 5.3f, 5.6f };
        int[] cnt = { 11, 12, 13 };
        float[] lean = { 0.10f, -0.20f, 0.15f };
        Broadleaf(b, seed, lod, cy[v], R[v], cnt[v], 0.72f, lean[v], 0.62f, 0.34f, 0.25f, 4, 0.22f, 1, 2, 0.30f, 3);
    }

    // ------------------------------------------------------------ Cedar: ёлка из 6-8 плоских ярусов с опущенными лапами и тёмной юбкой, короткий ствол виден внизу
    // Ярусы широкие и пологие (высота конуса ~0.6 радиуса): иначе соседние конусы сливаются в один острый «осколок» и ярусы не читаются.
    static void Cedar(TreeMB b, int v, int seed, int lod, TreeParams p)
    {
        float H = p.H;
        int[] tiers = { 6, 7, 8, 6 };
        float[] lean = { 0f, 0.14f, -0.12f, 0.08f };
        int N = lod == 2 ? 3 : tiers[v];
        int sides = lod == 0 ? 12 : (lod == 1 ? 8 : 6);
        int tSides = lod == 0 ? 8 : (lod == 1 ? 5 : 4);
        int tSegs = lod == 0 ? 3 : (lod == 1 ? 2 : 1);
        float topY = lod == 2 ? 1.2f : H * 0.80f;
        float topR = lod == 2 ? 0.2f : 0.07f;

        b.TubeF(0, t => new Vector3(lean[v] * t * t * 0.9f, Mathf.Lerp(-0.1f, topY, t), 0f), t => Mathf.Lerp(0.34f, topR, t), tSegs, tSides, false);

        float Rb = H * 0.285f, y0 = 0.95f, yTop = H * 0.78f;
        for (int i = 0; i < N; i++)
        {
            float t = N == 1 ? 0f : i / (float)(N - 1);
            float yb = Mathf.Lerp(y0, yTop, t);
            float r = Rb * Mathf.Lerp(1f, 0.26f, Mathf.Pow(t, 1.25f));
            bool last = i == N - 1;
            float h = last ? H - yb : r * 0.62f + 0.55f;
            var c = new Vector3(lean[v] * t * t * 0.9f, yb, 0f);
            float droop = last ? 0.2f : 0.55f * (1f - 0.35f * t);
            b.Tier(c, last ? r * 0.9f : r, h, sides, droop, 0.34f * (1f - 0.3f * t), seed + i * 13, lod <= 1, lod <= 1, 1, 3, 2);
        }
    }

    // ------------------------------------------------------------ Birch: тонкие светлые стволы с отметинами, овальная крона из «облаков»
    static void Birch(TreeMB b, int v, int seed, int lod, TreeParams p)
    {
        var rng = new System.Random(seed);
        float H = p.H;
        int[] stemsBy = { 2, 1, 2, 2 };
        int[] cloudsBy = { 7, 6, 8, 7 };
        int stems = stemsBy[v];
        float Ht = H * 0.80f;
        float r0 = stems == 1 ? 0.17f : 0.14f, r1 = 0.06f;

        int sSides = lod == 0 ? 7 : (lod == 1 ? 5 : 3);
        int sSegs = lod == 0 ? 4 : (lod == 1 ? 2 : 1);
        int marksPer = lod == 0 ? 7 : (lod == 1 ? 3 : 0);

        var bx = new float[stems]; var lx = new float[stems]; var lz = new float[stems]; var bend = new float[stems];
        for (int s = 0; s < stems; s++)
        {
            float side = stems == 1 ? 0f : (s == 0 ? -1f : 1f);
            bx[s] = side * 0.18f;
            lx[s] = side * (0.10f + 0.04f * v) + (stems == 1 ? 0.05f : 0f);
            lz[s] = Rf(rng, -0.05f, 0.05f);
            bend[s] = Rf(rng, -1f, 1f);
        }
        Func<int, float, Vector3> pos = (s, t) =>
            new Vector3(bx[s] + lx[s] * t * t * Ht, Mathf.Lerp(-0.1f, Ht, t), lz[s] * t * Ht + bend[s] * Mathf.Sin(t * Mathf.PI) * 0.12f);

        for (int s = 0; s < stems; s++)
        {
            int si = s;
            b.TubeF(0, t => pos(si, t), t => Mathf.Lerp(r0, r1, t), sSegs, sSides, false);

            // тёмные отметины: короткие полосы вокруг ствола (сабмеш 2)
            for (int m = 0; m < marksPer; m++)
            {
                float t = Rf(rng, 0.10f, 0.85f);
                Vector3 c = pos(si, t);
                Vector3 ax = (pos(si, Mathf.Min(1f, t + 0.02f)) - pos(si, Mathf.Max(0f, t - 0.02f))).normalized;
                Vector3 refv = Mathf.Abs(ax.y) > 0.8f ? Vector3.forward : Vector3.up;
                Vector3 u = Vector3.Cross(ax, refv).normalized;
                Vector3 w = Vector3.Cross(u, ax).normalized;
                float rad = Mathf.Lerp(r0, r1, t) * 1.05f;
                float a0 = Rf(rng, 0f, Mathf.PI * 2f), len = Rf(rng, 1.4f, 2.6f), hh = Rf(rng, 0.025f, 0.055f);
                var lo = new Vector3[3]; var up = new Vector3[3];
                for (int k = 0; k < 3; k++)
                {
                    float a = a0 + len * k / 2f;
                    Vector3 off = (u * Mathf.Cos(a) + w * Mathf.Sin(a)) * rad;
                    lo[k] = c - ax * hh + off;
                    up[k] = c + ax * hh + off;
                }
                for (int k = 0; k < 2; k++)
                {
                    b.Tri(2, lo[k], up[k], up[k + 1], c);
                    b.Tri(2, lo[k], up[k + 1], lo[k + 1], c);
                }
            }
        }

        // крона: овал из крупных клякс, чуть вытянут вверх (раскладка не зависит от LOD)
        float cx = stems == 1 ? 0.05f : 0f;
        Crown(b, seed, lod, new Vector3(cx, H * 0.64f, 0f), H * 0.27f, cloudsBy[v], 1.12f, 1, 1, 0f, 3);
    }

    // ------------------------------------------------------------ Willow_Dead: перекрученный ствол, 3 сука и 8-12 длинных свисающих веток
    static void Willow(TreeMB b, int v, int seed, int lod, TreeParams p)
    {
        var rng = new System.Random(seed);
        float H = p.H;
        float Ht = 0.68f * H;
        float turns = new[] { 1.2f, -1.6f, 1.0f }[v];
        int[] hangBy = { 8, 10, 12 };

        Func<float, Vector3> trunk = t =>
        {
            float ang = t * turns * Mathf.PI * 2f + v;
            float amp = 0.20f * Mathf.Sin(Mathf.PI * t * 0.9f) + 0.05f;
            return new Vector3(Mathf.Cos(ang) * amp, Mathf.Lerp(-0.1f, Ht, t), Mathf.Sin(ang) * amp);
        };
        int tSegs = lod == 0 ? 6 : (lod == 1 ? 3 : 2);
        int tSides = lod == 0 ? 8 : (lod == 1 ? 5 : 3);
        b.TubeF(0, trunk, t => Mathf.Lerp(0.30f, 0.10f, t), tSegs, tSides, false);

        // суки
        Vector3 T = trunk(1f);
        const int nl = 3;
        var mids = new Vector3[nl]; var tips = new Vector3[nl]; var phis = new float[nl];
        for (int k = 0; k < nl; k++)
        {
            float phi = k * 2.094f + Rf(rng, -0.3f, 0.3f) + v * 0.7f;
            phis[k] = phi;
            Vector3 outd = new Vector3(Mathf.Cos(phi), 0f, Mathf.Sin(phi));
            tips[k] = T + outd * Rf(rng, 0.9f, 1.3f) + Vector3.up * Rf(rng, 0.5f, 0.9f);
            mids[k] = T + outd * 0.45f + Vector3.up * Rf(rng, 0.3f, 0.5f);
            if (lod == 0) b.Tube(0, new[] { T, mids[k], tips[k] }, new[] { 0.10f, 0.07f, 0.035f }, 4, false);
            else if (lod == 1) b.Tube(0, new[] { T, tips[k] }, new[] { 0.09f, 0.04f }, 3, false);
        }

        // висячие ветки (сабмеш 1)
        int hc = lod == 2 ? 5 : hangBy[v];
        for (int i = 0; i < hc; i++)
        {
            int k = rng.Next(nl);
            Vector3 S = Vector3.Lerp(mids[k], tips[k], Rf(rng, 0.1f, 1f));
            float phi = phis[k] + Rf(rng, -0.6f, 0.6f);
            Vector3 outd = new Vector3(Mathf.Cos(phi), 0f, Mathf.Sin(phi));
            Vector3 side = new Vector3(-outd.z, 0f, outd.x) * Rf(rng, -0.25f, 0.25f);
            float L = Rf(rng, 1.6f, 2.6f) * (H / 4.6f);
            Vector3 p1 = S + outd * 0.35f + Vector3.up * 0.10f;
            Vector3 p2 = S + outd * 0.60f + Vector3.down * (L * 0.45f);
            Vector3 p3 = S + outd * 0.65f + side + Vector3.down * L;
            p2.y = Mathf.Max(0.25f, p2.y);
            p3.y = Mathf.Max(0.25f, p3.y);
            if (lod == 0) b.Tube(1, new[] { S, p1, p2, p3 }, new[] { 0.05f, 0.04f, 0.025f, 0.012f }, 4, false);
            else if (lod == 1) b.Tube(1, new[] { S, p2, p3 }, new[] { 0.045f, 0.025f, 0.012f }, 3, false);
            else b.Tube(1, new[] { S, p3 }, new[] { 0.045f, 0.015f }, 3, false);
        }
    }
}
#endif
