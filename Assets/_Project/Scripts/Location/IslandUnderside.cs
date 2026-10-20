// Assets/_Project/Scripts/Location/IslandUnderside.cs (НЕ в Editor)
// Подбрюшье парящего острова. Вызывается из FloatingIsland.Build() и цепляется к его bottomRing:
//   Underside : перевёрнутый рваный конус из скалы (сужается к острию, сталактитные выступы)
//   Roots     : корни, свисающие из-под травяной кромки
//   Crystals  : светящиеся кристаллы (свечение берётся из прозрачности цвета вершины, шейдер Project/IslandRock)
//   Debris    : мелкие камни, парящие вокруг (покачиваются через RiverBob)
// Всё из низкополигональных мешей с цветами в вершинах, один материал на шейдере IslandRock.
// Прозрачность цвета вершины: 1 = обычный камень, 0 = полное свечение.
using System.Collections.Generic;
using UnityEngine;

public static class IslandUnderside
{
    static float N(float x, float y, int s) { return Mathf.PerlinNoise(x + s * 3.17f, y + s * 1.93f); }

    // Грань с автоматическим поворотом лица от внутренней точки (для выпуклых форм и конуса)
    public static void Face(List<Vector3> v, List<Color> c, List<int> t, Vector3 a, Vector3 b, Vector3 d,
                     Color ca, Color cb, Color cd, Vector3 inside)
    {
        Vector3 n = Vector3.Cross(b - a, d - a);
        Vector3 mid = (a + b + d) / 3f;
        if (Vector3.Dot(n, mid - inside) < 0f)
        {
            Vector3 tp = b; b = d; d = tp;
            Color tc = cb; cb = cd; cd = tc;
        }
        int i = v.Count;
        v.Add(a); v.Add(b); v.Add(d);
        c.Add(ca); c.Add(cb); c.Add(cd);
        t.Add(i); t.Add(i + 1); t.Add(i + 2);
    }

    static Color WithA(Color col, float a) { return new Color(col.r, col.g, col.b, a); }

    public static Mesh ToMesh(string name, List<Vector3> v, List<Color> c, List<int> t)
    {
        var m = new Mesh { name = name };
        if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.SetVertices(v);
        m.SetColors(c);
        m.SetTriangles(t, 0);
        m.RecalculateNormals();   // вершины не общие: грани плоские
        m.RecalculateBounds();
        return m;
    }

    static GameObject Part(Transform root, string name, Mesh mesh, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        go.isStatic = true;
        return go;
    }

    public static void Build(FloatingIsland fi, Transform root, Material mat)
    {
        Vector3[] ring0 = fi.bottomRing;
        int m = ring0 != null ? ring0.Length : 0;
        if (m < 3) return;

        float H = Mathf.Max(3f, fi.depth - fi.cliffDepth);
        int seed = fi.seed;
        var rnd = new System.Random(seed * 7 + 3);
        System.Func<float> R01 = () => (float)rnd.NextDouble();

        Vector3 c = Vector3.zero;
        for (int j = 0; j < m; j++) c += ring0[j];
        c /= m;
        float R0 = 0f;
        for (int j = 0; j < m; j++) R0 += new Vector2(ring0[j].x - c.x, ring0[j].z - c.z).magnitude;
        R0 /= m;

        // острие чуть смещено от центра, чтобы низ не был идеальным конусом
        Vector3 tip = new Vector3(c.x + (R01() - 0.5f) * R0 * 0.25f, c.y - H, c.z + (R01() - 0.5f) * R0 * 0.25f);
        Vector3 inside = new Vector3(c.x, c.y - H * 0.5f, c.z);

        // ---------- скала ----------
        const int K = 8;
        var P = new Vector3[K + 1, m];
        var C = new Color[K + 1, m];
        Color deep = Color.Lerp(fi.rockDark, new Color(0.22f, 0.22f, 0.34f), 0.6f);

        for (int i = 0; i <= K; i++)
        {
            float t = i / (float)K;
            float s = 1f - Mathf.Pow(1f - t, 0.72f);        // выпуклый профиль: широко сверху, круто к острию
            float amp = Mathf.Sin(t * Mathf.PI);
            for (int j = 0; j < m; j++)
            {
                Vector3 b = ring0[j];
                Vector3 p = new Vector3(Mathf.Lerp(b.x, tip.x, s), Mathf.Lerp(b.y, tip.y, Mathf.Pow(t, 0.9f)), Mathf.Lerp(b.z, tip.z, s));
                if (i > 0 && i < K)
                {
                    Vector2 dir = new Vector2(p.x - tip.x, p.z - tip.z);
                    if (dir.sqrMagnitude > 1e-4f) dir.Normalize();
                    float rj = (N(b.x * 0.18f + i * 5.1f, b.z * 0.18f + i * 3.3f, seed + 31) - 0.5f) * 2f * fi.cliffRoughness * 1.8f * amp;
                    p.x += dir.x * rj; p.z += dir.y * rj;
                    p.y += (N(b.x * 0.25f + i * 2.7f, b.z * 0.25f, seed + 41) - 0.5f) * 1.2f * amp;
                    // сталактитные выступы: некоторые «колонки» свисают ниже соседних
                    float sp = Mathf.Clamp01((N(b.x * 0.16f, b.z * 0.16f, seed + 21) - 0.5f) * 2.2f);
                    p.y -= sp * sp * fi.undersideSpikes * Mathf.Sin(t * Mathf.PI * 0.9f);
                }
                P[i, j] = p;
                float strata = 1f + (N(b.x * 0.5f + i * 3f, b.z * 0.5f, seed + 9) - 0.5f) * 0.18f;
                Color col = Color.Lerp(fi.rockDark, deep, t) * strata; col.a = 1f;
                C[i, j] = col;
            }
        }

        var v = new List<Vector3>(m * K * 3);
        var cl = new List<Color>(m * K * 3);
        var tr = new List<int>(m * K * 3);
        for (int j = 0; j < m; j++)
        {
            int j2 = (j + 1) % m;
            for (int i = 0; i < K; i++)
            {
                Face(v, cl, tr, P[i, j], P[i, j2], P[i + 1, j2], C[i, j], C[i, j2], C[i + 1, j2], inside);
                if (i < K - 1)   // у острия четырёхугольник схлопывается в треугольник
                    Face(v, cl, tr, P[i, j], P[i + 1, j2], P[i + 1, j], C[i, j], C[i + 1, j2], C[i + 1, j], inside);
            }
        }
        Part(root, "Underside", ToMesh("IslandUnderside", v, cl, tr), mat);

        // ---------- корни, свисающие из-под кромки ----------
        var loop = fi.GetEdgeLoop();
        int lm = loop.Count;
        Vector2? gate = fi.Border.GetGatePosition();
        v = new List<Vector3>(); cl = new List<Color>(); tr = new List<int>();
        Color rootA = new Color(0.36f, 0.25f, 0.16f), rootB = new Color(0.24f, 0.17f, 0.12f);
        int rootCount = Mathf.Max(0, fi.roots);
        for (int n = 0; n < rootCount && lm > 4; n++)
        {
            int j = rnd.Next(lm);
            Vector2 lp = loop[j];
            if (gate.HasValue && Vector2.Distance(lp, gate.Value) < fi.gateFlatten) continue;   // у моста чисто
            Vector2 dir = (loop[(j + 1) % lm] - loop[(j - 1 + lm) % lm]).normalized;
            Vector2 outN = new Vector2(dir.y, -dir.x);
            Vector3 start = new Vector3(lp.x + outN.x * 0.15f, fi.GroundY - 0.55f, lp.y + outN.y * 0.15f);
            float len = 1.1f + R01() * 2.6f;     // короткие и толстые корни, а не чёрные иглы
            float r0 = 0.20f + R01() * 0.18f;
            Vector3 sway = new Vector3((R01() - 0.5f) * 0.9f, 0f, (R01() - 0.5f) * 0.9f);
            AddRoot(v, cl, tr, start, len, r0, sway, outN, rootA, rootB);
        }
        if (v.Count > 0) Part(root, "Roots", ToMesh("IslandRoots", v, cl, tr), mat);

        // ---------- кристаллы ----------
        v = new List<Vector3>(); cl = new List<Color>(); tr = new List<int>();
        Color[] palette =
        {
            new Color(0.35f, 0.85f, 1.00f), new Color(0.62f, 0.45f, 1.00f), new Color(0.40f, 1.00f, 0.80f)
        };
        int crystalCount = Mathf.Max(0, fi.crystals);
        for (int n = 0; n < crystalCount; n++)
        {
            int i = 2 + rnd.Next(K - 4);
            int j = 1 + rnd.Next(m - 2);
            Vector3 pos = P[i, j];
            Vector3 nrm = Vector3.Cross(P[i, j + 1] - P[i, j - 1], P[i + 1, j] - P[i - 1, j]).normalized;
            if (Vector3.Dot(nrm, pos - inside) < 0f) nrm = -nrm;   // наружу от скалы
            Color col = palette[rnd.Next(palette.Length)];
            int cluster = 1 + rnd.Next(3);
            for (int k = 0; k < cluster; k++)
            {
                Vector3 tilt = new Vector3(R01() - 0.5f, R01() - 0.5f, R01() - 0.5f) * (k == 0 ? 0.25f : 0.9f);
                float size = (k == 0 ? 1f : 0.55f) * (0.7f + R01() * 0.9f);
                Crystal(v, cl, tr, pos - nrm * 0.25f + Vector3.Cross(nrm, Vector3.up) * (k * 0.35f), (nrm + tilt).normalized,
                        0.22f * size, 1.4f * size, col, R01() * 6.28f);
            }
        }
        if (v.Count > 0) Part(root, "Crystals", ToMesh("IslandCrystals", v, cl, tr), mat);

        // ---------- парящие камни вокруг ----------
        int debrisCount = Mathf.Max(0, fi.debris);
        if (debrisCount > 0)
        {
            var dRoot = new GameObject("Debris").transform;
            dRoot.SetParent(root, false);
            for (int n = 0; n < debrisCount; n++)
            {
                float t = 0.15f + R01() * 0.7f;
                float rAt = R0 * Mathf.Pow(1f - t, 0.72f);       // радиус скалы на этой глубине
                float ang = R01() * Mathf.PI * 2f;
                float dist = rAt + 3f + R01() * 7f;
                Vector3 pos = new Vector3(c.x + Mathf.Cos(ang) * dist, Mathf.Lerp(c.y, tip.y, t) + (R01() - 0.5f) * 3f, c.z + Mathf.Sin(ang) * dist);
                float size = 0.7f + R01() * 1.9f;

                var go = new GameObject("Rock" + n);
                go.transform.SetParent(dRoot, false);
                go.transform.position = pos;
                go.transform.rotation = Quaternion.Euler(R01() * 360f, R01() * 360f, R01() * 360f);
                var rv = new List<Vector3>(); var rc = new List<Color>(); var rt = new List<int>();
                Rock(rv, rc, rt, size, fi.rockMid, rnd);
                go.AddComponent<MeshFilter>().sharedMesh = ToMesh("IslandDebris", rv, rc, rt);
                var rr = go.AddComponent<MeshRenderer>();
                rr.sharedMaterial = mat;
                rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var bob = go.AddComponent<RiverBob>();
                bob.amplitude = 0.2f + R01() * 0.2f;
                bob.speed = 0.5f + R01() * 0.5f;
                bob.tilt = 3f;
            }
        }
    }

    // Корень: пятигранная трубка, сужается к кончику, чуть качается по пути
    static void AddRoot(List<Vector3> v, List<Color> c, List<int> t, Vector3 start, float len, float r0,
                        Vector3 sway, Vector2 outN, Color colA, Color colB)
    {
        const int S = 5, SIDES = 5;
        var centers = new Vector3[S + 1];
        for (int k = 0; k <= S; k++)
        {
            float f = k / (float)S;
            centers[k] = start + new Vector3(sway.x * f * f + outN.x * 0.1f * f, -len * f, sway.z * f * f + outN.y * 0.1f * f);
        }
        var ring = new Vector3[S + 1, SIDES];
        var rcol = new Color[S + 1];
        for (int k = 0; k <= S; k++)
        {
            float f = k / (float)S;
            float rad = Mathf.Lerp(r0, r0 * 0.28f, f);
            Vector3 axis = (k < S ? centers[k + 1] - centers[k] : centers[k] - centers[k - 1]).normalized;
            Vector3 u = Vector3.Cross(axis, Vector3.forward);
            if (u.sqrMagnitude < 1e-4f) u = Vector3.Cross(axis, Vector3.right);
            u.Normalize();
            Vector3 w = Vector3.Cross(axis, u);
            for (int q = 0; q < SIDES; q++)
            {
                float a = q * Mathf.PI * 2f / SIDES;
                ring[k, q] = centers[k] + (u * Mathf.Cos(a) + w * Mathf.Sin(a)) * rad;
            }
            rcol[k] = Color.Lerp(colA, colB, f);
        }
        for (int k = 0; k < S; k++)
        {
            Vector3 inside = (centers[k] + centers[k + 1]) * 0.5f;
            for (int q = 0; q < SIDES; q++)
            {
                int q2 = (q + 1) % SIDES;
                Face(v, c, t, ring[k, q], ring[k, q2], ring[k + 1, q2], rcol[k], rcol[k], rcol[k + 1], inside);
                Face(v, c, t, ring[k, q], ring[k + 1, q2], ring[k + 1, q], rcol[k], rcol[k + 1], rcol[k + 1], inside);
            }
        }
    }

    // Кристалл: шестигранная призма с острым концом. Прозрачность вершины = свечение (кончик светится сильнее всего)
    static void Crystal(List<Vector3> v, List<Color> c, List<int> t, Vector3 basePos, Vector3 dir, float r, float h, Color col, float rot)
    {
        const int S = 6;
        Vector3 u = Vector3.Cross(dir, Mathf.Abs(dir.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
        Vector3 w = Vector3.Cross(dir, u);
        var b = new Vector3[S];
        var tp = new Vector3[S];
        for (int k = 0; k < S; k++)
        {
            float a = rot + k * Mathf.PI * 2f / S;
            Vector3 rad = u * Mathf.Cos(a) + w * Mathf.Sin(a);
            b[k] = basePos + rad * r;
            tp[k] = basePos + dir * h * 0.62f + rad * r * 0.72f;
        }
        Vector3 tipP = basePos + dir * h;
        Vector3 inside = basePos + dir * h * 0.3f;
        Color cb = WithA(col, 0.65f), cm = WithA(col, 0.30f), ct = WithA(col * 1.15f, 0f);
        for (int k = 0; k < S; k++)
        {
            int k2 = (k + 1) % S;
            Face(v, c, t, b[k], b[k2], tp[k2], cb, cb, cm, inside);
            Face(v, c, t, b[k], tp[k2], tp[k], cb, cm, cm, inside);
            Face(v, c, t, tp[k], tp[k2], tipP, cm, cm, ct, inside);
        }
    }

    // Низкополигональный камень: икосаэдр со случайной длиной вершин
    static readonly int[] IcoF =
    {
        0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11,
        1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
        3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9,
        4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1
    };

    static void Rock(List<Vector3> v, List<Color> c, List<int> t, float size, Color baseCol, System.Random rnd)
    {
        float g = (1f + Mathf.Sqrt(5f)) * 0.5f;
        Vector3[] p =
        {
            new Vector3(-1, g, 0), new Vector3(1, g, 0), new Vector3(-1, -g, 0), new Vector3(1, -g, 0),
            new Vector3(0, -1, g), new Vector3(0, 1, g), new Vector3(0, -1, -g), new Vector3(0, 1, -g),
            new Vector3(g, 0, -1), new Vector3(g, 0, 1), new Vector3(-g, 0, -1), new Vector3(-g, 0, 1)
        };
        for (int i = 0; i < p.Length; i++)
            p[i] = p[i].normalized * size * (0.7f + (float)rnd.NextDouble() * 0.6f);
        p[0].y *= 0.8f;   // чуть приплюснутый
        for (int f = 0; f < IcoF.Length; f += 3)
        {
            float shade = 0.9f + (float)rnd.NextDouble() * 0.2f;
            Color col = baseCol * shade; col.a = 1f;
            Face(v, c, t, p[IcoF[f]], p[IcoF[f + 1]], p[IcoF[f + 2]], col, col, col, Vector3.zero);
        }
    }
}
