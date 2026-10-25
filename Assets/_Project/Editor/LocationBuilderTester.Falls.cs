#if UNITY_EDITOR
// Assets/_Project/Editor/LocationBuilderTester.Falls.cs
// Водопады Location Builder TESTER (partial-часть LocationBuilderTester): с края острова и с горы.
// Меши лент плотные (40-44 ряда, 15 колонок), лист с края острова падает плавной дугой (парабола, у кромки касательная горизонтальна), края без зубцов, а рисунок падающей воды рисует шейдер LocationToonWater
// (свойства _Streak, _FallLen, _FallFade, _Pulse): струи-колонки, бегущие вниз, светлая кромка у верха, пена в основании,
// ленты воды распадаются в туман внизу, а края прозрачные и неровные.
// Ленты кладут в uv0 (x = плотность воды 0.2 по краям .. 1 в центре, y = путь вниз, u) и в uv1 (x = положение поперёк ленты, y = полуширина).
// Вызывается из сборки в сцене: BuildLipFallV2 / BuildMountainFallV2 / BuildFootMist / ConfigureFallWater.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public partial class LocationBuilderTester
{
    // сглаженный шум 0..1 для рваных краёв
    static float FallN(float a, float b) { return Mathf.PerlinNoise(a + 31.7f, b + 12.3f); }

    // материал падающей воды: включает рисунок струй. len = длина ленты по пути воды (u), fade = лента растворяется внизу в туман
    static void ConfigureFallWater(Material m, float len, bool fade, float streak)
    {
        if (m == null || !m.HasProperty("_Streak")) return;
        m.SetFloat("_Streak", streak);
        m.SetFloat("_FallLen", len);
        m.SetFloat("_FallFade", fade ? 1f : 0f);
        UnityEditor.EditorUtility.SetDirty(m);
    }

    // наибольшее значение uv.y сетки = длина пути воды по ленте
    static float FallMeshLen(Mesh m)
    {
        var uv = new List<Vector2>();
        m.GetUVs(0, uv);
        float mx = 0f;
        for (int i = 0; i < uv.Count; i++) mx = Mathf.Max(mx, uv[i].y);
        return mx;
    }

    // водопад с края острова: лист воды, который плавно перегибается через кромку и падает дугой.
    // Форма: x = reach * u, y = fallLength * u^2: у кромки касательная горизонтальна (лист продолжает поверхность реки без излома),
    // внизу почти отвесно. Плотная сетка (40 рядов), края без зубцов (шум плавный по ширине и длине), uv.y = путь по воде в u (для струй шейдера).
    static Mesh BuildLipFallV2(Layout L)
    {
        var r = L.river; var c = L.c; float ws = c.worldScale;
        int i = r.p.Count - 2;
        float t = r.s[i];
        Vector2 d = r.lipDir;
        Vector2 nr = new Vector2(-d.y, d.x);
        float hw = r.HW(t) + 0.3f;
        float y0 = r.WaterY(t);
        float len = Mathf.Max(4f, c.fallLength);
        float reach = Mathf.Clamp(0.14f * len, 1.8f, 4.2f);      // на сколько струя вылетает за кромку по горизонтали
        const int rows = 40, COLS = 15;
        var vs = new List<Vector3>(); var uvs = new List<Vector2>(); var uv2 = new List<Vector2>(); var tris = new List<int>();
        float acc = 0f; Vector3 prevC = Vector3.zero;
        for (int k = 0; k <= rows; k++)
        {
            float u = k / (float)rows;
            float outD = -0.25f + (reach + 0.25f) * u;           // чуть заходим на реку: лист и русло перекрываются без щели
            float drop = len * u * u;
            float wk = hw * (1f + 0.28f * Mathf.SmoothStep(0f, 1f, u));
            Vector2 pc = r.lip + d * outD;
            Vector3 cen = new Vector3(pc.x * ws, (y0 - drop) * ws, pc.y * ws);
            if (k > 0) acc += (cen - prevC).magnitude / ws;
            prevC = cen;
            for (int q = 0; q < COLS; q++)
            {
                float f = q / (float)(COLS - 1);
                float lt = 2f * f - 1f;
                float edge = Mathf.Abs(lt);
                float jag = (FallN(lt * 1.6f, u * 2.2f) - 0.5f) * 2f;                    // плавно по ширине и длине
                float lat = wk * lt * (1f + 0.07f * jag * edge * Mathf.Sqrt(u));
                float push = (FallN(lt * 0.7f + 5f, u * 2f) - 0.5f) * 0.25f * u;          // лист слегка «дышит»
                Vector2 pp = pc + nr * lat + d * push;
                vs.Add(new Vector3(pp.x * ws, (y0 - drop) * ws, pp.y * ws));
                uvs.Add(new Vector2(0.18f + 0.82f * (1f - edge * edge), acc));
                uv2.Add(new Vector2(lat, wk));
            }
        }
        for (int k = 0; k < rows; k++)
            for (int q = 0; q < COLS - 1; q++)
            {
                int a0 = k * COLS + q, b0 = a0 + 1, c0 = a0 + COLS + 1, d0 = a0 + COLS;
                tris.Add(a0); tris.Add(b0); tris.Add(c0);
                tris.Add(a0); tris.Add(c0); tris.Add(d0);
            }
        var m = new Mesh { name = "LocationTest_FallLip", indexFormat = IndexFormat.UInt32 };
        m.SetVertices(vs);
        m.SetUVs(0, uvs);
        m.SetUVs(1, uv2);
        m.SetTriangles(tris, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    // водопад горы: лента вдоль жёлоба в передней грани, сужена вверху и шире у чаши, края рваные
    static Mesh BuildMountainFallV2(Layout L)
    {
        var c = L.c; var m = L.mtn; float ws = c.worldScale;
        const int rows = 44, COLS = 15;
        float hw = c.fallWidth * 0.5f - 0.15f;
        Vector2 inD = -m.front, perp = new Vector2(-m.front.y, m.front.x);
        float wBase = L.river.WaterY(0f);
        var vs = new List<Vector3>(); var uvs = new List<Vector2>(); var uv2 = new List<Vector2>(); var tris = new List<int>();
        float acc = 0f; Vector3 prevC = Vector3.zero;
        for (int k = 0; k <= rows; k++)
        {
            float u = k / (float)rows;
            float a = Mathf.Lerp(m.lipA, -0.9f, u);
            float y = Mathf.Max(ChuteY(m, a) + 0.25f, wBase + 0.03f);
            Vector2 pc = m.foot + inD * a + m.front * 0.1f;
            float wk = hw * (0.9f + 0.35f * Mathf.SmoothStep(0f, 1f, u));
            Vector3 cen = new Vector3(pc.x * ws, y * ws, pc.y * ws);
            if (k > 0) acc += (cen - prevC).magnitude / ws;
            prevC = cen;
            for (int q = 0; q < COLS; q++)
            {
                float f = q / (float)(COLS - 1);
                float lt = 2f * f - 1f;
                float edge = Mathf.Abs(lt);
                float jag = (FallN(lt * 1.8f, u * 2.5f) - 0.5f) * 2f;      // плавно, без зубцов
                float lat = wk * lt * (1f + 0.06f * jag * edge);
                Vector2 pp = pc + perp * lat;
                vs.Add(new Vector3(pp.x * ws, y * ws, pp.y * ws));
                uvs.Add(new Vector2(0.18f + 0.82f * (1f - edge * edge), acc));
                uv2.Add(new Vector2(lat, wk));
            }
        }
        for (int k = 0; k < rows; k++)
            for (int q = 0; q < COLS - 1; q++)
            {
                int a0 = k * COLS + q, b0 = a0 + 1, c0 = a0 + COLS + 1, d0 = a0 + COLS;
                tris.Add(a0); tris.Add(b0); tris.Add(c0);
                tris.Add(a0); tris.Add(c0); tris.Add(d0);
            }
        var mesh = new Mesh { name = "LocationTest_MountainFall", indexFormat = IndexFormat.UInt32 };
        mesh.SetVertices(vs);
        mesh.SetUVs(0, uvs);
        mesh.SetUVs(1, uv2);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // светлые мягкие диски тумана. Шейдер воды: альфа идёт от uv.x (центр 0.1 = полная, кромка -0.03 = ноль)
    static Mesh BuildMistDiscs(string name, Vector2 baseP, Vector2 d, float yLow, float yHigh, int discs, float rMin, float rMax,
                               float spreadAlong, float spreadSide, float ws, System.Random rnd)
    {
        Vector2 nr = new Vector2(-d.y, d.x);
        var vs = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
        const int seg = 18;
        for (int k = 0; k < discs; k++)
        {
            float f = discs > 1 ? k / (float)(discs - 1) : 0f;
            float rx = (rMin + Rf(rnd) * (rMax - rMin)) * (1f + 0.25f * f);
            float rz = rx * (0.7f + 0.3f * Rf(rnd));
            Vector2 cen = baseP + d * ((Rf(rnd) - 0.3f) * spreadAlong) + nr * ((Rf(rnd) * 2f - 1f) * spreadSide);
            float y = Mathf.Lerp(yLow, yHigh, f) + Rf(rnd) * 0.4f;
            int c0 = vs.Count;
            vs.Add(new Vector3(cen.x * ws, y * ws, cen.y * ws));
            uvs.Add(new Vector2(0.10f, 0f));
            for (int q = 0; q < seg; q++)
            {
                float a = q * 6.2832f / seg;
                float jit = 0.9f + 0.2f * Rf(rnd);
                Vector2 off = d * (Mathf.Cos(a) * rz * jit) + nr * (Mathf.Sin(a) * rx * jit);
                vs.Add(new Vector3((cen.x + off.x) * ws, y * ws, (cen.y + off.y) * ws));
                uvs.Add(new Vector2(-0.03f, 0f));
            }
            for (int q = 0; q < seg; q++)
            {
                tris.Add(c0); tris.Add(c0 + 1 + q); tris.Add(c0 + 1 + (q + 1) % seg);
            }
        }
        var m = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
        m.SetVertices(vs);
        m.SetUVs(0, uvs);
        m.SetTriangles(tris, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    // туман в чаше у подножия горы: мягкие облака над пенным кругом, ниже у воды и выше у стены
    static Mesh BuildFootMist(Layout L)
    {
        var c = L.c; var m = L.mtn; float ws = c.worldScale;
        var rnd = new System.Random(c.seed * 53 + 9);
        float wy = L.river.WaterY(0f);
        return BuildMistDiscs("LocationTest_FootMist", L.foamC, m.front, wy + 0.2f, wy + 2.4f, 9, 1.0f, 2.0f,
                              c.sourceFoamR * 0.9f, c.sourceFoamR * 0.9f, ws, rnd);
    }
}
#endif
