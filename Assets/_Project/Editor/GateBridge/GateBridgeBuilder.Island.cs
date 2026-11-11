#if UNITY_EDITOR
// Assets/_Project/Editor/GateBridge/GateBridgeBuilder.Island.cs (Editor)
// Дальний остров, финальная площадка и всё на ней. Локальные оси корня FarIsland: z = вперёд от главного острова (a), x = вбок (b), y = 0 это верх травы.
// Мост приходит к z = -R. Тропа идёт к площадке (центр z = PlatformZ), за площадкой арка с кристаллом (ориентир), перед аркой пюпитр с запиской.
// Материал один: fi.rockMaterial (Project/IslandRock, цвета в вершинах). Деревья и камни: меши и материалы кедров (Prefabs/Trees/Generated)
// и префабы Rock1..3 проекта, если они есть; иначе процедурные замены в том же стиле.
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static partial class GateBridgeBuilder
{
    const string CedarDir = "Assets/_Project/Prefabs/Trees/Generated";
    static readonly string[] RockPrefabs = { "Assets/_Project/Prefabs/Rock1.prefab", "Assets/_Project/Prefabs/Rock2.prefab", "Assets/_Project/Prefabs/Rock3.prefab" };
    static readonly Color Glow = new Color(0.55f, 0.95f, 1f, 0f);
    static readonly Color StoneC = new Color(0.60f, 0.58f, 0.64f);
    static readonly Color StoneL = new Color(0.74f, 0.72f, 0.77f);
    static readonly Color PathC = new Color(0.80f, 0.70f, 0.54f);
    static readonly Color PathEdge = new Color(0.55f, 0.42f, 0.28f);
    static readonly Color PaperC = new Color(0.96f, 0.91f, 0.74f);

    // Платформа и всё вокруг считается от радиуса: числа заданы для R = 11 и масштабируются
    static float PlatformZ(float R) { return R * 0.33f; }
    const float PlatformR = 3.2f;

    static Vector3 L(float b, float a, float y) { return new Vector3(b, y, a); }

    static GameObject MeshObj(Transform parent, string name, Mesh mesh, Material mat, bool shadows)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = GateBridgeAssetVault.Mesh(mesh);
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        if (!shadows) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }

    static GameObject Child(Transform parent, string name, Vector3 localPos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        return go;
    }

    // ------------------------------------------------------------ тело острова
    static Mesh BuildIslandBody(GateBridgePlacement fi, float R, int seed)
    {
        const int SEG = 28;
        float[] rad = { 1f, 1f, 0.95f, 0.68f, 0.38f, 0f };
        float[] yy = { 0f, -0.08f, -0.25f, -0.65f, -1.15f, -1.75f };
        Color[] rc = { fi.grassTop, fi.grassDark, fi.soil, fi.rockWarm, fi.rockMid, fi.rockDark };
        int Rn = rad.Length;
        var ring = new Vector3[Rn, SEG];
        var colr = new Color[Rn, SEG];
        for (int i = 0; i < Rn; i++)
            for (int j = 0; j < SEG; j++)
            {
                float a = j * Mathf.PI * 2f / SEG;
                // верхнее кольцо: плавный силуэт (две низкие гармоники), у въезда моста строго круг
                float dAng = Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, 270f)) * Mathf.Deg2Rad;
                float w = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(dAng / 0.9f));
                float mod = 1f + 0.035f * Mathf.Sin(2f * a + 1.3f) + 0.025f * Mathf.Sin(3f * a + 0.4f);
                float k = Mathf.Lerp(1f, mod, w);
                float jr = i <= 1 ? k : (i == Rn - 1 ? 1f : 0.85f + 0.3f * Mathf.PerlinNoise(j * 0.7f + i * 5.1f + seed, i * 1.3f));
                float jy = (i >= 2 && i < Rn - 1) ? (Mathf.PerlinNoise(j * 0.9f + seed, i * 2.2f) - 0.5f) * 0.04f * R : 0f;
                ring[i, j] = new Vector3(Mathf.Cos(a) * rad[i] * R * jr, yy[i] * R + jy, Mathf.Sin(a) * rad[i] * R * jr);
                float shade = 0.92f + 0.16f * Mathf.PerlinNoise(j * 1.3f + seed * 0.7f, i * 3.1f);
                colr[i, j] = GBMesh.Sh(rc[i], shade);
            }

        var m = new GBMesh();
        Vector3 inside = new Vector3(0f, -0.8f * R, 0f);
        Color centerC = GBMesh.Sh(fi.grassTop, 1.08f);
        Color rimC = Color.Lerp(fi.grassTop, fi.grassDark, 0.35f);
        centerC.a = 1f; rimC.a = 1f;
        for (int j = 0; j < SEG; j++)
        {
            int j2 = (j + 1) % SEG;
            // верх: плоский веер, трава светлее в середине и темнее к кромке
            m.Tri(Vector3.zero, ring[0, j2], ring[0, j], centerC, rimC, rimC, inside);
            for (int i = 0; i < Rn - 1; i++)
            {
                IslandUnderside.Face(m.V, m.C, m.T, ring[i, j], ring[i, j2], ring[i + 1, j2], colr[i, j], colr[i, j2], colr[i + 1, j2], inside);
                if (i < Rn - 2)
                    IslandUnderside.Face(m.V, m.C, m.T, ring[i, j], ring[i + 1, j2], ring[i + 1, j], colr[i, j], colr[i + 1, j2], colr[i + 1, j], inside);
            }
        }
        return m.ToMesh("GateBridgeFarIsland");
    }

    // ------------------------------------------------------------ тропа
    static List<Vector2> PathPoints(float R, float pz)
    {
        // (b, a): от въезда моста к краю площадки, две плавные дуги
        Vector2[] w =
        {
            new Vector2(0f, -R + 1.0f), new Vector2(0f, -R + 2.0f), new Vector2(1.4f * R / 11f, -R * 0.52f),
            new Vector2(-1.5f * R / 11f, -R * 0.2f), new Vector2(0f, pz - PlatformR + 0.3f)
        };
        var pts = new List<Vector2>();
        for (int i = 0; i < w.Length - 1; i++)
        {
            Vector2 p0 = w[Mathf.Max(0, i - 1)], p1 = w[i], p2 = w[i + 1], p3 = w[Mathf.Min(w.Length - 1, i + 2)];
            for (int k = 0; k < 8; k++)
            {
                float t = k / 8f, t2 = t * t, t3 = t2 * t;
                pts.Add(0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
            }
        }
        pts.Add(w[w.Length - 1]);
        return pts;
    }

    static Mesh BuildPathMesh(List<Vector2> pts)
    {
        var m = new GBMesh();
        Vector3 down = Vector3.down;
        for (int layer = 0; layer < 2; layer++)
        {
            float half = layer == 0 ? 1.2f : 0.95f;
            float y = layer == 0 ? 0.016f : 0.026f;
            Color col = layer == 0 ? PathEdge : PathC;
            Vector3 pl = Vector3.zero, pr = Vector3.zero;
            for (int i = 0; i < pts.Count; i++)
            {
                Vector2 t = (pts[Mathf.Min(i + 1, pts.Count - 1)] - pts[Mathf.Max(i - 1, 0)]).normalized;
                Vector2 n = new Vector2(-t.y, t.x);
                Vector3 l = L(pts[i].x + n.x * half, pts[i].y + n.y * half, y);
                Vector3 r = L(pts[i].x - n.x * half, pts[i].y - n.y * half, y);
                if (i > 0) m.Quad(pl, pr, r, l, col, (pl + r) * 0.5f + down);
                pl = l; pr = r;
            }
        }
        // ровные камни по краям: аккуратный маршрут
        float acc = 0f; int side = 1;
        for (int i = 1; i < pts.Count; i++)
        {
            acc += (pts[i] - pts[i - 1]).magnitude;
            if (acc < 1.2f) continue;
            acc = 0f;
            Vector2 t = (pts[i] - pts[i - 1]).normalized;
            Vector2 n = new Vector2(-t.y, t.x) * side;
            m.Disc(L(pts[i].x + n.x * 1.3f, pts[i].y + n.y * 1.3f, 0.06f), 0.17f, 0.07f, 6, GBMesh.Sh(StoneC, 0.95f));
            side = -side;
        }
        return m.ToMesh("GateBridgeFarPath");
    }

    // ------------------------------------------------------------ площадка, арка, пюпитр, фонари
    static Mesh BuildPlatformMesh(float pz)
    {
        var m = new GBMesh();
        m.Disc(L(0f, pz, 0.03f), PlatformR, 0.04f, 28, StoneC);
        m.Disc(L(0f, pz, 0.045f), PlatformR - 0.7f, 0.04f, 28, StoneL);
        m.Disc(L(0f, pz, 0.06f), 1.1f, 0.04f, 20, GBMesh.Sh(StoneC, 0.9f));
        for (int k = 0; k < 10; k++)
        {
            float a = k * Mathf.PI * 2f / 10f;
            if (Mathf.Sin(a) < -0.5f && Mathf.Abs(Mathf.Cos(a)) < 0.45f) continue;   // у входа на площадку рун нет
            m.BoxAxis(L(Mathf.Cos(a) * (PlatformR - 0.28f), pz + Mathf.Sin(a) * (PlatformR - 0.28f), 0.1f), 0.08f, 0.04f, 0.08f, Glow);
        }
        return m.ToMesh("GateBridgePlatform");
    }

    static Mesh BuildArchMesh()
    {
        var m = new GBMesh();
        m.BoxAxis(new Vector3(-2.4f, 1.9f, 0f), 0.3f, 1.9f, 0.3f, StoneC);
        m.BoxAxis(new Vector3(2.4f, 1.9f, 0f), 0.3f, 1.9f, 0.3f, StoneC);
        m.BoxAxis(new Vector3(0f, 3.95f, 0f), 3.0f, 0.25f, 0.35f, GBMesh.Sh(StoneC, 0.92f));
        m.BoxAxis(new Vector3(-2.4f, 0.12f, 0f), 0.42f, 0.12f, 0.42f, GBMesh.Sh(StoneC, 0.85f));
        m.BoxAxis(new Vector3(2.4f, 0.12f, 0f), 0.42f, 0.12f, 0.42f, GBMesh.Sh(StoneC, 0.85f));
        // кристалл-маяк над аркой: крупнее прежнего и выше, виден с ворот ещё до стройки
        Vector3 c = new Vector3(0f, 5.5f, 0f);
        float rr = 0.62f;
        Vector3 top = c + Vector3.up * rr * 1.9f, bot = c - Vector3.up * rr * 1.5f;
        Vector3[] eq = { c + Vector3.right * rr, c + Vector3.forward * rr, c - Vector3.right * rr, c - Vector3.forward * rr };
        for (int i = 0; i < 4; i++)
        {
            m.Tri(top, eq[i], eq[(i + 1) % 4], Glow, c);
            m.Tri(bot, eq[i], eq[(i + 1) % 4], Glow, c);
        }
        return m.ToMesh("GateBridgeArch");
    }

    static Mesh BuildPedestalMesh()
    {
        var m = new GBMesh();
        m.BoxAxis(new Vector3(0f, 0.45f, 0f), 0.32f, 0.45f, 0.32f, StoneC);
        Quaternion q = Quaternion.AngleAxis(-18f, Vector3.right);
        Vector3 r = Vector3.right, u = q * Vector3.up, f = q * Vector3.forward;
        m.Box(new Vector3(0f, 0.96f, 0f), r, u, f, 0.42f, 0.05f, 0.3f, GBMesh.Sh(StoneC, 1.05f));
        m.Box(new Vector3(0f, 1.015f, 0f), r, u, f, 0.2f, 0.008f, 0.14f, PaperC);       // записка
        m.Rock(new Vector3(0f, 1.35f, 0f), 0.08f, 0.1f, 0.08f, 3, 0.05f, 1f, Glow);     // огонёк над запиской
        return m.ToMesh("GateBridgePedestal");
    }

    static Mesh BuildLampsMesh(float R, float pz)
    {
        var m = new GBMesh();
        float[,] at = { { -2.0f, -R + 2.8f }, { 2.0f, -R + 2.8f }, { -2.3f, pz - 2.3f }, { 2.3f, pz - 2.3f } };
        for (int i = 0; i < 4; i++)
        {
            Vector3 b = L(at[i, 0], at[i, 1], 0f);
            m.BoxAxis(b + Vector3.up * 0.7f, 0.05f, 0.7f, 0.05f, PostC);
            m.BoxAxis(b + Vector3.up * 1.5f, 0.12f, 0.14f, 0.12f, Glow);
            m.BoxAxis(b + Vector3.up * 1.7f, 0.16f, 0.04f, 0.16f, PostC);
        }
        return m.ToMesh("GateBridgeLamps");
    }

    // ------------------------------------------------------------ деревья
    sealed class TreeSpec { public float b, a, h; public TreeSpec(float b, float a, float h) { this.b = b; this.a = a; this.h = h; } }

    // Позиции для R = 11 (масштабируются). Ближе 3,5 м к тропе и к площадке нет ничего: место для камеры и разворота Лео,
    // между площадкой и мостом нет деревьев в секторе обзора (вид назад на главный остров)
    static TreeSpec[] TreeSpecs(float R)
    {
        float k = R / 11f;
        var l = new List<TreeSpec>
        {
            new TreeSpec(-4.5f, 8.0f, 7.0f), new TreeSpec(0.6f, 8.8f, 7.6f), new TreeSpec(4.6f, 7.7f, 6.6f),
            new TreeSpec(-7.0f, 3.0f, 5.6f), new TreeSpec(7.2f, 2.0f, 5.2f),
            new TreeSpec(-6.5f, -2.5f, 4.8f), new TreeSpec(6.6f, -3.5f, 5.0f),
            new TreeSpec(-5.4f, 6.2f, 5.8f), new TreeSpec(5.8f, 5.8f, 5.4f),
            new TreeSpec(-4.8f, -7.0f, 4.0f), new TreeSpec(5.0f, -6.4f, 4.2f)
        };
        foreach (var t in l) { t.b *= k; t.a *= k; }
        return l.ToArray();
    }

    sealed class CedarSrc { public Mesh mesh; public Material[] mats; public string name; }

    static List<CedarSrc> LoadCedars()
    {
        var res = new List<CedarSrc>();
        if (!AssetDatabase.IsValidFolder(CedarDir)) return res;
        foreach (var guid in AssetDatabase.FindAssets("Tree_Cedar_ t:Prefab", new[] { CedarDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) continue;
            Transform lod = go.transform.Find("LOD0");
            var mf = lod != null ? lod.GetComponent<MeshFilter>() : go.GetComponentInChildren<MeshFilter>();
            var mr = lod != null ? lod.GetComponent<MeshRenderer>() : go.GetComponentInChildren<MeshRenderer>();
            if (mf == null || mr == null || mf.sharedMesh == null) continue;
            res.Add(new CedarSrc { mesh = mf.sharedMesh, mats = mr.sharedMaterials, name = go.name });
        }
        res.Sort((x, y) => string.CompareOrdinal(x.name, y.name));
        return res;
    }

    // Нерубимые деревья-декор: свой GameObject с мешем и материалами кедра. Без тега Resource, без ResourceItem: срубить нельзя
    static void BuildTrees(Transform parent, GateBridgePlacement fi, Material mat, float R, int seed, StringBuilder log)
    {
        var root = Child(parent, "Trees", Vector3.zero).transform;
        var specs = TreeSpecs(R);
        var cedars = LoadCedars();
        if (cedars.Count > 0)
        {
            for (int i = 0; i < specs.Length; i++)
            {
                var src = cedars[i % cedars.Count];
                float mh = Mathf.Max(0.1f, src.mesh.bounds.size.y);
                float s = specs[i].h / mh;
                var go = new GameObject("Cedar_" + i);
                go.transform.SetParent(root, false);
                go.transform.localPosition = L(specs[i].b, specs[i].a, 0f);
                go.transform.localRotation = Quaternion.Euler(0f, 37f * i + seed, 0f);
                go.transform.localScale = Vector3.one * s;
                go.AddComponent<MeshFilter>().sharedMesh = src.mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterials = src.mats;
                var cc = go.AddComponent<CapsuleCollider>();
                cc.radius = 0.3f / s; cc.height = 2.4f / s; cc.center = new Vector3(0f, 1.2f / s, 0f);
            }
            log.AppendLine("Деревья: " + specs.Length + " кедров из " + CedarDir + " (меши и материалы проекта, рубить нельзя).");
            return;
        }
        // запасной вариант: низкополигональные ёлки в стиле острова одним мешем
        var m = new GBMesh();
        Color tree = new Color(0.18f, 0.42f, 0.24f);
        foreach (var t in specs)
        {
            Vector3 bp = L(t.b, t.a, 0f);
            float h = t.h * 0.55f, rr = 0.9f + t.h * 0.07f;
            m.BoxAxis(bp + Vector3.up * 0.5f, 0.14f, 0.5f, 0.14f, PostC);
            m.Cone(bp + Vector3.up * 0.8f, rr, h, tree, 7);
            m.Cone(bp + Vector3.up * (0.8f + h * 0.45f), rr * 0.75f, h * 0.8f, GBMesh.Sh(tree, 1.1f), 7);
            m.Cone(bp + Vector3.up * (0.8f + h * 0.9f), rr * 0.5f, h * 0.6f, GBMesh.Sh(tree, 1.2f), 7);
        }
        MeshObj(root, "Conifers", m.ToMesh("GateBridgeConifers"), mat, true);
        log.AppendLine("Деревья: НЕ найдены префабы " + CedarDir + "/Tree_Cedar_*.prefab. Поставлены запасные низкополигональные ёлки. Постройте виды деревьев (Tools > Forest > Trees > Build Species) и повторите сборку.");
    }

    // ------------------------------------------------------------ камни
    static void BuildRocks(Transform parent, Material mat, float R, StringBuilder log)
    {
        var root = Child(parent, "Rocks", Vector3.zero).transform;
        float k = R / 11f;
        float[,] spots = { { -3.4f, -6.0f, 0.55f }, { 3.5f, -5.0f, 0.45f }, { -4.4f, 3.4f, 0.9f }, { 4.5f, 4.6f, 0.7f }, { 6.6f, 7.4f, 0.8f }, { -6.2f, 7.0f, 0.65f } };
        int found = 0;
        GameObject[] prefabs = new GameObject[RockPrefabs.Length];
        for (int i = 0; i < RockPrefabs.Length; i++) { prefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(RockPrefabs[i]); if (prefabs[i] != null) found++; }
        var fallback = new GBMesh();
        for (int i = 0; i < spots.GetLength(0); i++)
        {
            float b = spots[i, 0] * k, a = spots[i, 1] * k, rad = spots[i, 2];
            var prefab = prefabs[i % prefabs.Length];
            if (prefab == null)
            {
                fallback.Rock(L(b, a, rad * 0.15f), rad * 1.1f, rad * 0.8f, rad, 11 + i, 0.18f, 0.35f, GBMesh.Sh(new Color(0.55f, 0.54f, 0.60f), 0.9f + 0.05f * i));
                continue;
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
            go.name = "Rock_" + i;
            go.transform.localPosition = L(b, a, 0f);
            go.transform.localRotation = Quaternion.Euler(0f, i * 67f + 19f, 0f);
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) { Object.DestroyImmediate(go); continue; }
            Bounds bb = rs[0].bounds;
            foreach (var r in rs) bb.Encapsulate(r.bounds);
            float sc = rad * 2f / Mathf.Max(0.05f, Mathf.Max(bb.size.x, bb.size.z));
            go.transform.localScale = Vector3.one * sc;
            bb = rs[0].bounds;
            foreach (var r in rs) bb.Encapsulate(r.bounds);
            go.transform.position += Vector3.up * (parent.position.y - bb.min.y - bb.size.y * 0.28f);
            foreach (var c in go.GetComponentsInChildren<Collider>()) c.enabled = false;   // декор: камере и Лео не мешает
        }
        if (fallback.V.Count > 0) MeshObj(root, "RocksFallback", fallback.ToMesh("GateBridgeRocks"), mat, true);
        if (found < RockPrefabs.Length) log.AppendLine("Камни: префабов Rock1..3 найдено " + found + " из " + RockPrefabs.Length + " (" + RockPrefabs[0] + " и далее), недостающие заменены процедурными.");
        else log.AppendLine("Камни: префабы Rock1..3 проекта.");
    }

    // ------------------------------------------------------------ край: камни по кромке, облачка под островом, заграждения
    static Mesh BuildRimStones(float R, int seed)
    {
        var m = new GBMesh();
        for (int k = 0; k < 16; k++)
        {
            float a = (k + 0.5f) * Mathf.PI * 2f / 16f;
            float dAng = Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, 270f));
            if (dAng < 22f) continue;   // у въезда моста чисто
            float rad = 0.35f + 0.2f * Mathf.PerlinNoise(k * 0.9f, seed);
            float rr = R * 0.955f;
            m.Rock(L(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr, rad * 0.1f), rad * 1.2f, rad * 0.8f, rad, seed + k, 0.2f, 0.3f, GBMesh.Sh(new Color(0.58f, 0.57f, 0.63f), 0.9f + 0.2f * Mathf.PerlinNoise(k * 1.7f, seed)));
        }
        return m.ToMesh("GateBridgeRimStones");
    }

    static Mesh BuildCloudPuffs(float R, int seed)
    {
        var m = new GBMesh();
        float k = R / 11f;
        float[,] c = { { -5f, -4.2f, 3f, 3.0f }, { 4.5f, -5f, 1f, 3.4f }, { 0f, -6f, -5f, 3.2f }, { -2f, -5.5f, 7.5f, 2.6f }, { 6f, -3.8f, -4f, 2.8f }, { -6.5f, -3.6f, -3.5f, 2.6f } };
        for (int i = 0; i < c.GetLength(0); i++)
        {
            float r = c[i, 3] * k;
            Color col = Color.Lerp(new Color(0.93f, 0.96f, 1f), new Color(0.78f, 0.85f, 0.97f), 0.25f * (i % 3));
            m.Rock(L(c[i, 0] * k, c[i, 2] * k, c[i, 1] * k), r, r * 0.55f, r * 0.9f, seed + i * 3, 0.12f, 0.6f, col);
        }
        return m.ToMesh("GateBridgeUndersideClouds");
    }

    // Низкие непрерывные заграждения по кромке (1,15 м): Лео не сходит с острова, камера идёт выше и в них не упирается.
    // У въезда моста окно ширины настила, его закрывают бортики моста (они заходят на остров)
    static void BuildRimGuards(Transform parent, float R, float hwDeck)
    {
        var root = Child(parent, "RimGuards", Vector3.zero).transform;
        const int N = 24;
        float ringR = R * 0.93f;
        float gap = Mathf.Asin(Mathf.Clamp01((hwDeck + 0.3f) / ringR));
        float chord = 2f * ringR * Mathf.Sin(Mathf.PI / N);
        for (int k = 0; k < N; k++)
        {
            float a = (k + 0.5f) * Mathf.PI * 2f / N;
            if (Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, 270f)) * Mathf.Deg2Rad < gap) continue;
            var go = Child(root, "Rim" + k, L(Mathf.Cos(a) * ringR, Mathf.Sin(a) * ringR, 0.575f));
            Vector3 tangent = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
            go.transform.localRotation = Quaternion.LookRotation(tangent, Vector3.up);
            go.AddComponent<BoxCollider>().size = new Vector3(0.5f, 1.15f, chord * 1.15f);
        }
    }

    // ------------------------------------------------------------ сборка острова целиком
    static Transform BuildFarIsland(GateBridgePlacement fi, GateBridge gb, GBPlan p, Transform root, Material mat, StringBuilder log, out Transform noteSpot, out Transform arrival)
    {
        float R = p.R;
        float pz = PlatformZ(R);
        var island = new GameObject("FarIsland");
        island.transform.SetParent(root, false);
        island.transform.position = new Vector3(p.C2.x, p.yStubTop, p.C2.y);
        island.transform.rotation = Quaternion.LookRotation(p.N3, Vector3.up);

        Mesh body = BuildIslandBody(fi, R, gb.seed);
        var bodyGo = MeshObj(island.transform, "Body", body, mat, true);
        bodyGo.AddComponent<MeshCollider>().sharedMesh = bodyGo.GetComponent<MeshFilter>().sharedMesh;

        MeshObj(island.transform, "Path", BuildPathMesh(PathPoints(R, pz)), mat, false);
        MeshObj(island.transform, "Platform", BuildPlatformMesh(pz), mat, false);

        var arch = MeshObj(island.transform, "Arch", BuildArchMesh(), mat, true);
        arch.transform.localPosition = L(0f, pz + 2.7f, 0f);
        for (int side = -1; side <= 1; side += 2)
        {
            var bc = arch.AddComponent<BoxCollider>();
            bc.center = new Vector3(side * 2.4f, 1.9f, 0f);
            bc.size = new Vector3(0.7f, 3.8f, 0.7f);
        }

        var ped = MeshObj(island.transform, "NotePedestal", BuildPedestalMesh(), mat, true);
        ped.transform.localPosition = L(0f, pz + 0.9f, 0f);
        var pbc = ped.AddComponent<BoxCollider>();
        pbc.center = new Vector3(0f, 0.5f, 0f); pbc.size = new Vector3(0.7f, 1.0f, 0.7f);
        noteSpot = Child(ped.transform, "NoteSpot", new Vector3(0f, 1.0f, 0f)).transform;

        MeshObj(island.transform, "Lamps", BuildLampsMesh(R, pz), mat, false);
        MeshObj(island.transform, "RimStones", BuildRimStones(R, gb.seed), mat, true);
        MeshObj(island.transform, "UndersideClouds", BuildCloudPuffs(R, gb.seed), mat, false);

        BuildTrees(island.transform, fi, mat, R, gb.seed, log);
        BuildRocks(island.transform, mat, R, log);
        BuildRimGuards(island.transform, R, p.hw);

        arrival = Child(island.transform, "Arrival", L(0f, -R + GateBridge.IslandLandingInset, 0.05f)).transform;
        return island.transform;
    }
}
#endif
