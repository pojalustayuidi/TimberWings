#if UNITY_EDITOR
// =====================================================================================
//  LocationRiver.cs  ->  Assets/_Project/Editor/Island/Features/LocationRiver.cs
//  Вода (мультяшный шейдер, каустики, пена), мост с 3 стадиями, камыш, кувшинки, камни.
//  Русло уже прорезано в Terrain самим LocationBuilder, здесь только вода/мост/декор.
//  Всё строится в "u" внутри узла Location/River (localScale = worldScale).
//  Нужен RiverBridge.cs в игровых скриптах (не в Editor). RiverBob.cs - по желанию.
// =====================================================================================
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class LocationRiver
{
    const string ShaderPath = LocKit.ShaderDir + "/LocWaterToon.shader";
    public const string RingShaderPath = LocKit.ShaderDir + "/LocFoamRing.shader";

    readonly LocationCtx c;
    readonly System.Random rng;
    Transform node;
    Mesh mBox, mCyl6, mCyl8, mCone6, mOcta, mRing, mFlag, mPad, mLilyFlower;
    Mesh[] mRocks, mReeds, mCatStem, mCatHead;
    Material ringMat;
    float bridgeS;

    LocationRiver(LocationCtx ctx)
    {
        c = ctx;
        rng = new System.Random(ctx.seed * 17 + 3);
    }

    public static void Build(LocationCtx ctx)
    {
        new LocationRiver(ctx).Run();
    }

    // переключение стадии моста из окна (без пересборки)
    public static void SetStage(int st)
    {
        st = Mathf.Clamp(st, 1, 3);
        GameObject loc = null;
        foreach (var g in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            if (g.name == "Location") { loc = g; break; }
        if (loc == null) return;
        var br = loc.transform.Find("River/BrokenBridge");
        if (br == null) return;
        for (int i = 1; i <= 3; i++)
        {
            var t = br.Find("Stage" + i);
            if (t != null) t.gameObject.SetActive(i == st);
        }
        var bl = br.Find("Blockers");
        if (bl != null) bl.gameObject.SetActive(st == 1);
        foreach (var m in br.GetComponents<MonoBehaviour>())
        {
            if (m == null || m.GetType().Name != "RiverBridge") continue;
            var f = m.GetType().GetField("stage");
            if (f != null && f.FieldType == typeof(int)) { f.SetValue(m, st); EditorUtility.SetDirty(m); }
        }
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        SceneView.RepaintAll();
    }

    // ------------------------------------------------------------------ run
    void Run()
    {
        var go = new GameObject("River");
        go.transform.SetParent(c.root, false);
        go.transform.localScale = Vector3.one * c.ws;
        node = go.transform;
        bridgeS = c.hasBridge ? c.bridgePos.y : 0f;

        PrepareMeshes();
        ringMat = MakeRingMat();
        BuildWater();

        var decor = new GameObject("Decor");
        decor.transform.SetParent(node, false);
        ScatterShoreRocks(decor.transform);
        ScatterMidRocks(decor.transform);
        ScatterPebbles(decor.transform);
        ScatterPlants(decor.transform);
        ScatterLilies(node);
        if (!c.springBuilt)                      // запасной каскад (LocationSpring) строит свои камни и ручей сам
        {
            // есть гора (LocationHeadMountain): подкова валунов и каменная рама родника ей не нужны, у неё свой грот и валуны
            bool mountain = c.headFace != null && c.headFace.Count >= 2;
            ScatterSource(decor.transform, mountain);
            BuildSpringFall();
            if (!mountain) BuildSourceCliff(decor.transform); // layered rock lip frames the spring instead of a bare ribbon
        }

        if (c.hasBridge) BuildBridge();
        if (c.s.waterBarrier) BuildBarrier();
    }

    // ------------------------------------------------------------------ координаты
    // s = вдоль реки (это Z), v = поперёк (это X)
    float WaterY { get { return c.waterLevel; } }
    float Height(float x, float z) { return c.heightU(x, z); }
    float Center(float s) { return c.riverX(s); }
    Vector3 ToWorld(float s, float v, float y) { return new Vector3(v, y, s); }
    bool HasWater(float s) { return Height(Center(s), s) < WaterY - 0.05f; }
    float SignedDepth(float s, float v) { return WaterY - Height(v, s); }
    bool InGround(Vector3 p, float margin)
    {
        float m = c.half - margin;
        return Mathf.Abs(p.x) <= m && Mathf.Abs(p.z) <= m;
    }

    float ShoreDist(float s, int side)
    {
        float cc = Center(s);
        float limit = c.riverWidth * 0.5f + c.riverBank + c.s.riverSlope + 3f;
        if (c.s.newRiver) limit = c.riverHalfTop(s) + c.s.riverBrowWidth + 3f;
        for (float d = 0f; d < limit; d += 0.1f)
            if (Height(cc + side * d, s) > WaterY) return d;
        return c.riverWidth * 0.5f;
    }

    float ShoreAlong(Vector3 center, Vector3 dir)
    {
        // новая река (Р3): «берег» для моста = бровка (ровная земля на уровне настила), а не урез воды на дне ущелья
        float thr = c.s.newRiver ? c.s.riverBrowLevel - 0.12f : WaterY;
        for (float d = 0f; d < 40f; d += 0.1f)
        {
            Vector3 p = center + dir * d;
            if (Height(p.x, p.z) > thr) return d;
        }
        return 10f;
    }

    float Rf(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }

    static void SetLocal(Transform t, Vector3 p, Quaternion r)
    {
        t.localPosition = p;
        t.localRotation = r;
    }

    // ------------------------------------------------------------------ меши
    void PrepareMeshes()
    {
        mBox = LocKit.SaveAsset(LocKit.Box(), LocKit.Batches + "/RBox.asset");
        mCyl6 = LocKit.Prism("Cyl6", 6, 0.5f, 0.5f);
        mCyl8 = LocKit.Prism("Cyl8", 8, 0.5f, 0.5f);
        mCone6 = LocKit.Prism("Cone6", 6, 0.5f, 0f);
        mOcta = LocKit.Octa();
        mRing = LocKit.Ring();
        mFlag = LocKit.Flag();
        mPad = LocKit.SaveAsset(MakePadMesh(), LocKit.Batches + "/RLilyPad.asset");
        mLilyFlower = LocKit.SaveAsset(MakeFlowerMesh(), LocKit.Batches + "/RLilyFlower.asset");
        mRocks = new Mesh[5];
        for (int i = 0; i < mRocks.Length; i++) mRocks[i] = LocKit.Ico("RRock_" + i, i + 1, LocKit.RockDeform);
        mReeds = new Mesh[4];
        for (int i = 0; i < 4; i++) mReeds[i] = MakeReedMesh(i + 1);
        mCatStem = new Mesh[3];
        mCatHead = new Mesh[3];
        for (int i = 0; i < 3; i++) MakeCattail(i + 1, out mCatStem[i], out mCatHead[i]);
    }

    Mesh MakeReedMesh(int seed)
    {
        var r = new System.Random(seed * 101);
        var mb = new LocMB();
        for (int i = 0; i < 7; i++)
        {
            float ang = (float)r.NextDouble() * Mathf.PI * 2f;
            float rad = (float)r.NextDouble() * 0.18f;
            Vector3 b0 = new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);
            float h = 0.6f + (float)r.NextDouble() * 0.7f;
            float lean = 0.10f + (float)r.NextDouble() * 0.25f;
            float la = (float)r.NextDouble() * Mathf.PI * 2f;
            Vector3 tip = b0 + new Vector3(Mathf.Cos(la) * lean, h, Mathf.Sin(la) * lean);
            float rr = 0.035f;
            Vector3 p0 = b0 + new Vector3(rr, 0f, 0f);
            Vector3 p1 = b0 + new Vector3(-rr * 0.5f, 0f, rr * 0.87f);
            Vector3 p2 = b0 + new Vector3(-rr * 0.5f, 0f, -rr * 0.87f);
            mb.Tri(p0, p1, tip, (p0 + p1) * 0.5f - b0);
            mb.Tri(p1, p2, tip, (p1 + p2) * 0.5f - b0);
            mb.Tri(p2, p0, tip, (p2 + p0) * 0.5f - b0);
        }
        return mb.ToMesh("Reed_" + seed);
    }

    void MakeCattail(int seed, out Mesh stem, out Mesh head)
    {
        var r = new System.Random(seed * 71 + 5);
        var ms = new LocMB();
        var mh = new LocMB();
        for (int i = 0; i < 3; i++)
        {
            float ang = (float)r.NextDouble() * Mathf.PI * 2f;
            float rad = (float)r.NextDouble() * 0.12f;
            Vector3 b0 = new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);
            float h = 1.0f + (float)r.NextDouble() * 0.7f;
            float lean = 0.08f + (float)r.NextDouble() * 0.2f;
            float la = (float)r.NextDouble() * Mathf.PI * 2f;
            Vector3 tip = b0 + new Vector3(Mathf.Cos(la) * lean, h, Mathf.Sin(la) * lean);
            LocKit.AddTube(ms, b0, tip, 0.02f, 0.008f, 4);
            Vector3 dir = (tip - b0).normalized;
            LocKit.AddTube(mh, tip - dir * 0.34f, tip - dir * 0.08f, 0.04f, 0.035f, 6);
        }
        stem = ms.ToMesh("CatStem_" + seed);
        head = mh.ToMesh("CatHead_" + seed);
    }

    Mesh MakePadMesh()
    {
        var mb = new LocMB();
        const int n = 18;
        Vector3 cc = Vector3.zero;
        for (int i = 1; i < n; i++)
        {
            float a0 = i / (float)n * Mathf.PI * 2f, a1 = (i + 1) / (float)n * Mathf.PI * 2f;
            Vector3 p0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * 0.5f;
            Vector3 p1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * 0.5f;
            mb.Tri(cc, p0, p1, Vector3.up);
            mb.Tri(cc, p0, p1, Vector3.down);
        }
        return mb.ToMesh("LilyPad");
    }

    Mesh MakeFlowerMesh()
    {
        var mb = new LocMB();
        const int n = 6;
        Vector3 tip = new Vector3(0f, 0.16f, 0f);
        for (int i = 0; i < n; i++)
        {
            float a0 = i / (float)n * Mathf.PI * 2f, a1 = (i + 1) / (float)n * Mathf.PI * 2f;
            Vector3 p0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * 0.11f;
            Vector3 p1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * 0.11f;
            mb.Tri(p0, p1, tip, (p0 + p1) * 0.5f);
        }
        return mb.ToMesh("LilyFlower");
    }

    // ------------------------------------------------------------------ вода
    void BuildWater()
    {
        const float step = 0.5f;
        const int cols = 56;
        float lo = -c.half, hi = c.half;
        int rows = Mathf.CeilToInt((hi - lo) / step) + 1;
        var verts = new Vector3[rows * cols];
        var uv0 = new Vector2[rows * cols];
        var uv1 = new Vector2[rows * cols];
        var sd = new float[rows * cols];
        var inside = new bool[rows * cols];

        for (int r = 0; r < rows; r++)
        {
            float s = lo + r * step;
            float cc = Center(s);
            float hw = c.riverWidth * 0.5f + c.riverBank + c.s.riverSlope + 1.2f;
            if (c.s.newRiver) hw = c.riverHalfTop(s) + 0.6f;   // лента воды не шире русла с бровкой
            for (int j = 0; j < cols; j++)
            {
                float f = j / (cols - 1f) * 2f - 1f;
                float v = cc + f * hw;
                int idx = r * cols + j;
                verts[idx] = ToWorld(s, v, WaterY);
                inside[idx] = InGround(verts[idx], 0f) && (!c.s.newRiver || c.edgeIn(v, s) > -0.25f);   // Р3: вода не висит за кромкой острова (дальше вода падает водопадом)
                float d = SignedDepth(s, v);
                sd[idx] = d;
                uv0[idx] = new Vector2(f * hw, s);
                uv1[idx] = new Vector2(d, 0f);
            }
        }

        var tris = new List<int>();
        for (int r = 0; r < rows - 1; r++)
            for (int j = 0; j < cols - 1; j++)
            {
                int a = r * cols + j, b = a + 1, c2 = a + cols, d2 = c2 + 1;
                if (!(inside[a] && inside[b] && inside[c2] && inside[d2])) continue;
                float mx = Mathf.Max(Mathf.Max(sd[a], sd[b]), Mathf.Max(sd[c2], sd[d2]));
                if (mx <= 0.02f) continue;
                AddUpTri(tris, verts, a, b, c2);
                AddUpTri(tris, verts, b, d2, c2);
            }
        if (tris.Count == 0)
        {
            Debug.LogWarning("[LocationRiver] Воды нет: проверь глубину реки.");
            return;
        }

        var mesh = new Mesh { name = "LocWater", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.vertices = verts;
        mesh.SetUVs(0, new List<Vector2>(uv0));
        mesh.SetUVs(1, new List<Vector2>(uv1));
        mesh.SetTriangles(tris, 0);
        var normals = new Vector3[verts.Length];
        for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.up;
        mesh.normals = normals;
        mesh.RecalculateBounds();
        mesh = LocKit.SaveAsset(mesh, LocKit.Batches + "/LocWater.asset");

        var go = new GameObject("Water");
        go.transform.SetParent(node, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = MakeWaterMaterial();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    static void AddUpTri(List<int> tris, Vector3[] v, int a, int b, int cI)
    {
        if (Vector3.Cross(v[b] - v[a], v[cI] - v[a]).y < 0f) { int t = b; b = cI; cI = t; }
        tris.Add(a); tris.Add(b); tris.Add(cI);
    }

    Material MakeWaterMaterial() { return GetWaterMaterial(c); }

    // Материал воды реки одним ассетом на сборку: его берёт и скала-исток (LocationWaterfallCliff, она строится ДО реки),
    // и сама река. SaveAsset удаляет старый файл, поэтому второй вызов обязан вернуть уже созданный материал, иначе у скалы будет Missing.
    public static Material GetWaterMaterial(LocationCtx c)
    {
        var cached = LocKit.Cached("LocWater");
        if (cached != null) return cached;
        Shader sh = LocKit.EnsureShader(ShaderPath, WaterShaderSource);
        if (sh != null && sh.isSupported && !ShaderUtil.ShaderHasError(sh))
        {
            var m = new Material(sh) { name = "LocWater" };
            m.SetColor("_ShallowColor", new Color(0.45f, 0.92f, 0.84f, 0.55f));
            m.SetColor("_DeepColor", new Color(0.06f, 0.42f, 0.64f, 0.93f));
            m.SetColor("_FoamColor", Color.white);
            m.SetFloat("_FoamWidth", 0.22f);
            m.SetFloat("_DepthRange", Mathf.Max(0.5f, c.riverDepth + c.waterLevel));
            m.SetFloat("_Tiling", 1.1f);
            m.SetFloat("_Speed", 0.55f);
            m.SetFloat("_RippleStrength", 0.5f);
            m.SetFloat("_Sparkle", 0.6f);
            m.SetFloat("_Caustics", 0.6f);
            m.renderQueue = 3000;
            m = LocKit.SaveAsset(m, LocKit.MatDir + "/LocWater.mat");
            LocKit.Cache("LocWater", m);
            return m;
        }
        Debug.LogWarning("[LocationRiver] Шейдер воды не скомпилировался, использую простую прозрачную воду.");
        return LocKit.MatTransparent("LocWaterFallback", c.s.waterColor, 0.8f);
    }

    Material MakeRingMat()
    {
        Shader sh = LocKit.EnsureShader(RingShaderPath, RingShaderSource);
        if (sh == null || !sh.isSupported || ShaderUtil.ShaderHasError(sh))
        {
            Debug.LogWarning("[LocationRiver] Шейдер колец пены не скомпилировался, кольца пропущены.");
            return null;
        }
        var m = new Material(sh) { name = "LocFoamRing" };
        m.SetColor("_Color", Color.white);
        m.renderQueue = 3001;
        return LocKit.SaveAsset(m, LocKit.MatDir + "/LocFoamRing.mat");
    }

    // ------------------------------------------------------------------ декор
    Material[] RockMats()
    {
        return new[]
        {
            LocKit.Mat("RRockA", new Color(0.62f, 0.60f, 0.68f), 0.05f),
            LocKit.Mat("RRockB", new Color(0.55f, 0.55f, 0.62f), 0.05f),
            LocKit.Mat("RRockC", new Color(0.70f, 0.66f, 0.64f), 0.05f),
            LocKit.Mat("RRockMoss", new Color(0.50f, 0.58f, 0.46f), 0.05f)
        };
    }

    Material[] FlowerMats()
    {
        return new[]
        {
            LocKit.Mat("FlowerPink", new Color(1f, 0.62f, 0.78f), 0.2f),
            LocKit.Mat("FlowerYellow", new Color(1f, 0.86f, 0.30f), 0.2f),
            LocKit.Mat("FlowerWhite", new Color(0.98f, 0.97f, 0.92f), 0.2f),
            LocKit.Mat("FlowerBlue", new Color(0.55f, 0.65f, 1f), 0.2f)
        };
    }

    // крутизна рельефа в точке (для посадки камней на склон без зазора)
    float Slope(float x, float z)
    {
        const float d = 0.5f;
        float gx = (Height(x + d, z) - Height(x - d, z)) / (2f * d);
        float gz = (Height(x, z + d) - Height(x, z - d)) / (2f * d);
        return Mathf.Sqrt(gx * gx + gz * gz);
    }

    void PlaceRock(LocBatcher bt, Vector3 pos, float scale, Material[] mats)
    {
        pos.y -= scale * 0.35f * Mathf.Min(Slope(pos.x, pos.z), 1.2f);   // на склоне вкапываем глубже, чтобы низ не висел
        Quaternion rot = Quaternion.Euler(Rf(-6f, 6f), Rf(0f, 360f), Rf(-6f, 6f));
        float sx = scale * Rf(0.85f, 1.25f);
        float sz = scale * Rf(0.85f, 1.25f);
        float sy = scale * 0.75f * Rf(0.8f, 1.3f);
        bt.Add(mRocks[rng.Next(mRocks.Length)], mats[rng.Next(mats.Length)], pos, rot, new Vector3(sx, sy, sz));
    }

    bool NearBridge(float s, float pad)
    {
        return c.hasBridge && Mathf.Abs(s - bridgeS) < c.s.bridgeWidth * 0.5f + pad;
    }

    void ScatterShoreRocks(Transform parent)
    {
        var holder = new GameObject("ShoreRocks").transform;
        holder.SetParent(parent, false);
        var bt = new LocBatcher(24f);
        var mats = RockMats();
        const float rockStep = 2.4f;
        for (float s = -c.half + 2f; s < c.half - 2f; s += rockStep)
            for (int side = -1; side <= 1; side += 2)
            {
                if (rng.NextDouble() < 0.25) continue;
                float ss = s + (float)(rng.NextDouble() - 0.5) * rockStep * 0.8f;
                if (!HasWater(ss)) continue;
                if (NearBridge(ss, 1.6f)) continue;
                float d = ShoreDist(ss, side) + (float)(rng.NextDouble() * 1.6 - 0.5);
                Vector3 p = ToWorld(ss, Center(ss) + side * d, 0f);
                if (!InGround(p, 1f)) continue;
                p.y = Height(p.x, p.z) - 0.08f;
                float sc = 0.7f * Rf(0.5f, 1.7f);
                PlaceRock(bt, p, sc, mats);
                if (rng.NextDouble() < 0.22)
                {
                    int cnt = 1 + rng.Next(2);
                    for (int k = 0; k < cnt; k++)
                    {
                        float ang = Rf(0f, Mathf.PI * 2f);
                        float rr = sc * Rf(0.8f, 1.3f);
                        Vector3 q = p + new Vector3(Mathf.Cos(ang) * rr, 0f, Mathf.Sin(ang) * rr);
                        if (!InGround(q, 1f)) continue;
                        q.y = Height(q.x, q.z) - 0.06f;
                        PlaceRock(bt, q, sc * Rf(0.35f, 0.6f), mats);
                    }
                }
            }
        bt.Flush(holder, "ShoreRocks", true);
    }

    void ScatterMidRocks(Transform parent)
    {
        var holder = new GameObject("WaterRocks").transform;
        holder.SetParent(parent, false);
        var bt = new LocBatcher(24f);
        var btRing = new LocBatcher(24f);
        var mats = RockMats();
        int placed = 0, tries = 0;
        int midCount = c.s.riverFullLength ? 18 : 7;   // v9: камни на дне по всей длине
        while (placed < midCount && tries++ < 900)
        {
            float s = Rf(-c.half, c.half);
            if (!HasWater(s)) continue;
            if (c.hasBridge && Mathf.Abs(s - bridgeS) < 5f) continue;
            float f = Rf(-0.55f, 0.55f);
            float v = Center(s) + f * c.riverWidth * 0.5f;
            if (SignedDepth(s, v) < 0.35f) continue;
            float bed = Height(v, s);                          // v9: русло глубже, ставим камень на дно, а не на уровень воды
            Vector3 p = ToWorld(s, v, bed - 0.05f);
            if (!InGround(p, 2f)) continue;
            float sc = 0.7f * Rf(0.8f, 1.7f);
            PlaceRock(bt, p, sc, mats);
            bool pierces = (WaterY - bed) < sc * 0.75f;       // пена-кольцо только если камень торчит из воды
            if (ringMat != null && pierces)
                btRing.Add(mRing, ringMat, new Vector3(p.x, WaterY + 0.02f, p.z), Quaternion.identity, new Vector3(sc * 2.1f, 1f, sc * 2.1f));
            placed++;
        }
        bt.Flush(holder, "WaterRocks", true);
        btRing.Flush(holder, "Rings", false);
    }

    void ScatterPebbles(Transform parent)
    {
        var holder = new GameObject("Pebbles").transform;
        holder.SetParent(parent, false);
        var bt = new LocBatcher(24f);
        var mat = LocKit.Mat("RPebble", new Color(0.72f, 0.68f, 0.62f), 0.05f);
        int placed = 0, tries = 0;
        int pebCount = c.s.riverFullLength ? 260 : 140;
        while (placed < pebCount && tries++ < 1800)
        {
            float s = Rf(-c.half, c.half);
            if (!HasWater(s)) continue;
            int side = rng.Next(2) == 0 ? -1 : 1;
            float d = ShoreDist(s, side) + Rf(-0.3f, 2.2f);
            Vector3 p = ToWorld(s, Center(s) + side * d, 0f);
            if (!InGround(p, 1f)) continue;
            p.y = Height(p.x, p.z) + 0.01f;
            float sc = Rf(0.07f, 0.21f);
            bt.Add(mRocks[rng.Next(mRocks.Length)], mat, p, Quaternion.Euler(0f, Rf(0f, 360f), 0f), new Vector3(sc * 1.2f, sc * 0.7f, sc));
            placed++;
        }
        bt.Flush(holder, "Pebbles", true);
    }

    void ScatterPlants(Transform parent)
    {
        var holder = new GameObject("Plants").transform;
        holder.SetParent(parent, false);
        var bt = new LocBatcher(24f);
        Material[] reedMats =
        {
            LocKit.Mat("ReedA", new Color(0.36f, 0.66f, 0.24f), 0.1f),
            LocKit.Mat("ReedB", new Color(0.46f, 0.74f, 0.28f), 0.1f)
        };
        Material catHead = LocKit.Mat("CattailHead", new Color(0.42f, 0.26f, 0.14f), 0.1f);
        const float step = 1.1f;
        for (float u = -c.half + 2f; u < c.half - 2f; u += step)
            for (int side = -1; side <= 1; side += 2)
            {
                float s = u + (float)(rng.NextDouble() - 0.5) * step;
                if (!HasWater(s)) continue;
                if (NearBridge(s, 1.6f)) continue;
                float k = (float)rng.NextDouble();
                if (rng.NextDouble() > 1.0 - k * 0.55) continue;
                float range = c.riverBank * 0.8f + c.s.riverSlope * 0.5f + 2.5f;
                float d = ShoreDist(s, side) - 0.15f + k * range;
                float v = Center(s) + side * d;
                float hv = Height(v, s);
                if (hv < WaterY - 0.2f) continue;
                Vector3 p = ToWorld(s, v, hv);
                if (!InGround(p, 1f)) continue;
                float sc = Rf(0.8f, 1.5f);
                Quaternion rot = Quaternion.Euler(0f, Rf(0f, 360f), 0f);
                bool cat = hv < WaterY + 0.35f && rng.NextDouble() < 0.45;
                if (cat)
                {
                    int ci = rng.Next(mCatStem.Length);
                    bt.Add(mCatStem[ci], reedMats[1], p, rot, Vector3.one * sc);
                    bt.Add(mCatHead[ci], catHead, p, rot, Vector3.one * sc);
                }
                else
                    bt.Add(mReeds[rng.Next(mReeds.Length)], reedMats[rng.Next(reedMats.Length)], p, rot, Vector3.one * sc);
            }
        bt.Flush(holder, "Plants", false);
    }

    Material MakeFallMat()
    {
        Shader sh = Shader.Find("Project/Waterfall");
        Material mat;
        if (sh != null)
        {
            mat = new Material(sh) { name = "LocSpringFall" };
            mat.SetColor("_Deep", new Color(0.26f, 0.60f, 0.86f, 1f));
            mat.SetColor("_Light", new Color(0.86f, 0.96f, 1f, 1f));
            mat.SetColor("_Foam", Color.white);
            mat.SetFloat("_Speed", 2.6f);
            mat.SetFloat("_Streaks", 6f);
            mat.SetFloat("_FoamLen", 1.2f);
            mat = LocKit.SaveAsset(mat, LocKit.MatDir + "/LocSpringFall.mat");
        }
        else
        {
            Debug.LogWarning("[LocationRiver] Не найден шейдер Project/Waterfall (Scripts/Location/Waterfall.shader): водопадик у истока нарисован запасным материалом воды.");
            mat = LocKit.MatTransparent("LocSpringFallFallback", c.s.waterColor, 0.85f);
        }
        return mat;
    }

    // Ручей на горе: лента идёт по профилю передней стены (ctx.headFace: (s, y) снизу вверх), на полках стелется, на стенах падает.
    // Профиль строит LocationHeadMountain, поэтому вода всегда лежит на камне в 14 см от грани.
    void BuildMountainFall()
    {
        var face = c.headFace;
        int m = face.Count;
        float x0 = c.headSpringX;
        float fw = c.s.headFallWidth;
        const float off = 0.14f;

        var pos = new Vector2[m];
        for (int i = 0; i < m; i++)
        {
            Vector2 d = face[Mathf.Min(i + 1, m - 1)] - face[Mathf.Max(i - 1, 0)];
            if (d.sqrMagnitude < 1e-6f) d = Vector2.up;
            d.Normalize();
            Vector2 nrm = new Vector2(-d.y, d.x);              // (s, y): наружу от камня (вертикаль: на юг, полка: вверх)
            pos[i] = face[i] + nrm * off;
        }

        // сверху вниз, шаг ~0.3 u
        var dense = new List<Vector2>();
        dense.Add(new Vector2(pos[m - 1].x + 0.6f, pos[m - 1].y));      // старт чуть глубже арки: вода выходит из тёмного проёма, а не висит в воздухе
        for (int i = m - 1; i > 0; i--)
        {
            Vector2 a = pos[i], b = pos[i - 1];
            int steps = Mathf.Max(1, Mathf.CeilToInt((b - a).magnitude / 0.3f));
            for (int k = 0; k < steps; k++) dense.Add(Vector2.Lerp(a, b, k / (float)steps));
        }
        dense.Add(pos[0]);
        int rows = dense.Count;
        if (rows < 3) return;

        Material mat = MakeFallMat();
        var verts = new Vector3[rows * 2];
        var uvs = new Vector2[rows * 2];
        var cols = new Color[rows * 2];
        var nor = new Vector3[rows * 2];
        float pathM = 0f;
        Vector3 prev = Vector3.zero;
        for (int i = 0; i < rows; i++)
        {
            float t = i / (float)(rows - 1);
            Vector3 p = ToWorld(dense[i].x, x0, dense[i].y);
            if (i > 0) pathM += Vector3.Distance(p, prev) * c.ws;
            prev = p;
            float hw = fw * 0.5f * Mathf.Lerp(0.75f, 1.15f, t);
            verts[i * 2] = p + new Vector3(-hw, 0f, 0f);
            verts[i * 2 + 1] = p + new Vector3(hw, 0f, 0f);
            uvs[i * 2] = new Vector2(0f, pathM);
            uvs[i * 2 + 1] = new Vector2(1f, pathM);
            cols[i * 2] = cols[i * 2 + 1] = Color.white;
            nor[i * 2] = nor[i * 2 + 1] = Vector3.up;
        }
        var tris = new List<int>();
        for (int i = 0; i < rows - 1; i++)
        {
            int a = i * 2, b = a + 1, c2 = a + 2, d = a + 3;
            tris.Add(a); tris.Add(c2); tris.Add(b);
            tris.Add(b); tris.Add(c2); tris.Add(d);
        }
        var mesh = new Mesh { name = "LocSpringFall" };
        mesh.vertices = verts;
        mesh.SetUVs(0, new List<Vector2>(uvs));
        mesh.colors = cols;
        mesh.normals = nor;
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        mesh = LocKit.SaveAsset(mesh, LocKit.Batches + "/LocSpringFall.asset");

        var go = new GameObject("SpringFall");
        go.transform.SetParent(node, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        if (mRing != null && ringMat != null)       // пена в бассейне под струйкой и на полках, где вода падает
        {
            AddFoam(ToWorld(face[0].x - 0.15f, x0, WaterY + 0.03f), fw * 2.4f);
            for (int i = 1; i + 1 < m - 1; i++)
            {
                // сегмент-полка (горизонталь, идёт вглубь): вода с вышестоящей стены падает в её внутренний край face[i+1]
                bool ledge = Mathf.Abs(face[i].y - face[i + 1].y) < 0.15f && face[i + 1].x > face[i].x + 0.05f;
                if (ledge && face[i + 1].y > WaterY + 1.0f)
                    AddFoam(ToWorld(face[i + 1].x - 0.3f, x0, face[i + 1].y + 0.08f), fw * 1.3f);
            }
        }
        BuildMountainPools(face, x0, fw, off, mat);
    }

    // Чаши воды: родниковая заводь в гроте, по небольшой запруде на каждой полке (вода копится и переливается дальше вниз)
    // и широкая заводь у подножия, из которой начинается русло реки. Плоские эллипсы с тем же материалом, что и струя: края пенятся.
    void BuildMountainPools(List<Vector2> face, float x0, float fw, float off, Material mat)
    {
        int m = face.Count;
        var pv = new List<Vector3>();
        var pu = new List<Vector2>();
        var pc = new List<Color>();
        var pt = new List<int>();
        const int R = 8;

        System.Action<float, float, float, float> pool = (sc, yy, rs, rv) =>
        {
            int b0 = pv.Count;
            for (int i = 0; i <= R; i++)
            {
                float t = -1f + 2f * i / R;
                float hw = Mathf.Max(0.03f, rv * Mathf.Sqrt(Mathf.Max(0f, 1f - t * t)));
                float s = sc + rs * t;
                float pm = (rs - rs * t) * c.ws;                    // метры вдоль потока: течение идёт на юг
                pv.Add(ToWorld(s, x0 - hw, yy)); pv.Add(ToWorld(s, x0 + hw, yy));
                pu.Add(new Vector2(0f, pm)); pu.Add(new Vector2(1f, pm));
                pc.Add(Color.white); pc.Add(Color.white);
            }
            for (int i = 0; i < R; i++)
            {
                int a = b0 + i * 2, b = a + 1, c2 = a + 2, d = a + 3;
                pt.Add(a); pt.Add(c2); pt.Add(b);
                pt.Add(b); pt.Add(c2); pt.Add(d);
            }
        };

        // заводь в гроте: вода выходит из-под арки
        pool(face[m - 1].x - 0.05f, face[m - 1].y + off + 0.03f, 0.5f, fw * 0.9f);
        // запруды на полках
        for (int i = 1; i + 1 < m - 1; i++)
        {
            bool ledge = Mathf.Abs(face[i].y - face[i + 1].y) < 0.15f && face[i + 1].x > face[i].x + 0.25f;
            if (!ledge || face[i + 1].y <= WaterY + 1.0f) continue;
            float depth = face[i + 1].x - face[i].x;
            pool((face[i].x + face[i + 1].x) * 0.5f, face[i].y + off + 0.03f, Mathf.Max(0.18f, depth * 0.45f), fw * 0.85f);
        }
        // заводь у подножия: отсюда начинается река
        pool(face[0].x - 0.9f, WaterY + 0.04f, fw * 1.5f, fw * 1.9f);

        var mesh = new Mesh { name = "LocSpringPools" };
        mesh.SetVertices(pv);
        mesh.SetUVs(0, pu);
        mesh.SetColors(pc);
        mesh.SetTriangles(pt, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh = LocKit.SaveAsset(mesh, LocKit.Batches + "/LocSpringPools.asset");
        var go = new GameObject("SpringPools");
        go.transform.SetParent(node, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    void AddFoam(Vector3 localPos, float size)
    {
        var foam = new GameObject("SpringFoam");
        foam.transform.SetParent(node, false);
        foam.transform.localPosition = localPos;
        foam.transform.localScale = new Vector3(size, 1f, size);
        foam.AddComponent<MeshFilter>().sharedMesh = mRing;
        var fr = foam.AddComponent<MeshRenderer>();
        fr.sharedMaterial = ringMat;
        fr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        fr.receiveShadows = false;
    }

    // Исток: дуга валунов вокруг родника и камни по ручью ниже него (v9, часть 2)
    // Водопадик из скального лба у истока: узкая струйка стекает по скале в верхний бассейн реки.
    // Форма лба строится в LocationGen.Core.cs (HeadRidge); здесь только лента воды по готовому рельефу + пена у подножия.
    void BuildSpringFall()
    {
        if (!c.s.riverFullLength || !c.s.riverGorge || !c.s.riverHead) return;
        if (c.headFace != null && c.headFace.Count >= 2) { BuildMountainFall(); return; }   // есть слоистая гора: ручей течёт по её уступам
        float sz = Mathf.Min(c.s.riverSourceZ, c.half - 8f);
        float sBot = sz - 6f;
        if (!HasWater(sBot)) return;
        while (sBot < sz + 8f && HasWater(sBot + 0.25f)) sBot += 0.25f;     // последняя точка с водой при движении на север

        float maxH = float.MinValue;
        for (float s = sBot; s < sBot + 14f; s += 0.25f) maxH = Mathf.Max(maxH, Height(Center(s), s));
        if (maxH - WaterY < 1.5f) { Debug.LogWarning("[LocationRiver] Скального лба у истока нет (хребет ниже 1.5 u), водопадик пропущен."); return; }
        float sTop = sBot;
        for (float s = sBot; s < sBot + 14f; s += 0.25f)
            if (Height(Center(s), s) >= maxH - 0.6f) { sTop = s; break; }
        if (sTop - sBot < 1f) return;

        Material mat = MakeFallMat();

        int rows = Mathf.Max(3, Mathf.CeilToInt((sTop - sBot) / 0.4f) + 1);
        var verts = new Vector3[rows * 2];
        var uvs = new Vector2[rows * 2];
        var cols = new Color[rows * 2];
        var nor = new Vector3[rows * 2];
        float pathM = 0f;
        Vector3 prev = Vector3.zero;
        float fw = c.s.headFallWidth;
        for (int i = 0; i < rows; i++)
        {
            float t = i / (float)(rows - 1);
            float s = Mathf.Lerp(sTop + 0.5f, sBot - 0.2f, t);              // сверху вниз: так бежит UV.y
            float cx = Center(s);
            // Follow the cliff until the lip, then transition to a near-vertical sheet.
            float groundY = Height(cx, s);
            float lipBlend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(sBot - 0.2f, sTop + 0.5f, s));
            float y = Mathf.Max(groundY, WaterY) + 0.10f;
            // The final rows are deliberately held near the receiving pool level to avoid a floating ribbon.
            y = Mathf.Lerp(WaterY + 0.08f, y, lipBlend);
            Vector3 p = ToWorld(s, cx, y);
            if (i > 0) pathM += Vector3.Distance(p, prev) * c.ws;
            prev = p;
            float hw = fw * 0.5f * Mathf.Lerp(0.75f, 1.15f, t);
            verts[i * 2] = p + new Vector3(-hw, 0f, 0f);
            verts[i * 2 + 1] = p + new Vector3(hw, 0f, 0f);
            uvs[i * 2] = new Vector2(0f, pathM);
            uvs[i * 2 + 1] = new Vector2(1f, pathM);
            cols[i * 2] = cols[i * 2 + 1] = new Color(1f, 1f, 1f, 1f);
            nor[i * 2] = nor[i * 2 + 1] = Vector3.up;
        }
        var tris = new List<int>();
        for (int i = 0; i < rows - 1; i++)
        {
            int a = i * 2, b = a + 1, c2 = a + 2, d = a + 3;
            tris.Add(a); tris.Add(c2); tris.Add(b);
            tris.Add(b); tris.Add(c2); tris.Add(d);
        }
        var mesh = new Mesh { name = "LocSpringFall" };
        mesh.vertices = verts;
        mesh.SetUVs(0, new List<Vector2>(uvs));
        mesh.colors = cols;
        mesh.normals = nor;
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        mesh = LocKit.SaveAsset(mesh, LocKit.Batches + "/LocSpringFall.asset");

        var go = new GameObject("SpringFall");
        go.transform.SetParent(node, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        if (mRing != null && ringMat != null)       // пена в бассейне под струйкой
        {
            var foam = new GameObject("SpringFoam");
            foam.transform.SetParent(node, false);
            foam.transform.localPosition = ToWorld(sBot - 0.1f, Center(sBot), WaterY + 0.03f);
            foam.transform.localScale = new Vector3(fw * 2.2f, 1f, fw * 2.2f);
            foam.AddComponent<MeshFilter>().sharedMesh = mRing;
            var fr = foam.AddComponent<MeshRenderer>();
            fr.sharedMaterial = ringMat;
            fr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            fr.receiveShadows = false;
        }
    }

    // A substantial low-poly headwall around the spring.
    // IMPORTANT: this is decoration only. It does not edit Terrain, the river axis,
    // water mesh, bridge placement, or the island outline. The stream stays open in the middle.
    void BuildSourceCliff(Transform parent)
    {
        if (!c.s.riverFullLength || !c.s.riverGorge || !c.s.riverHead) return;

        float sz = Mathf.Min(c.s.riverSourceZ, c.half - 8f);
        float sourceX = Center(sz);
        float halfWater = Mathf.Max(1.35f, c.s.riverWidth * 0.20f);
        var holder = new GameObject("SourceCliffFrame").transform;
        holder.SetParent(parent, false);
        var bt = new LocBatcher(24f);
        var mats = RockMats();

        // Large, overlapping rock masses make two uneven canyon shoulders.
        // The inner faces are kept outside the water ribbon so the waterfall remains visible.
        float[] widths = { 4.0f, 5.2f, 6.1f, 6.8f };
        float[] heights = { 5.0f, 6.8f, 8.6f, 9.2f };
        float[] depths = { 3.0f, 3.6f, 4.1f, 4.4f };
        for (int side = -1; side <= 1; side += 2)
        {
            for (int row = 0; row < 4; row++)
            {
                float z = sz + 2.0f + row * 2.85f + Rf(-0.35f, 0.35f);
                float width = widths[row] * Rf(0.9f, 1.08f);
                float height = heights[row] * Rf(0.88f, 1.12f);
                float depth = depths[row] * Rf(0.9f, 1.12f);
                float lateral = halfWater + width * 0.5f + 0.72f + row * 0.18f;
                float x = Center(z) + side * lateral + Rf(-0.25f, 0.25f);

                AddSourceCliffMass(bt, mats, x, z, width, height, depth,
                    Rf(0f, 360f), row >= 2 ? 0.34f : 0.28f);

                // A lower broken buttress ties the high wall into the river bank.
                float zFoot = z - 1.1f + Rf(-0.3f, 0.3f);
                float footWidth = width * Rf(0.62f, 0.82f);
                float footX = Center(zFoot) + side * (halfWater + footWidth * 0.5f + 0.48f);
                AddSourceCliffMass(bt, mats, footX, zFoot,
                    footWidth, height * Rf(0.38f, 0.52f), depth * 0.78f,
                    Rf(0f, 360f), 0.24f);

                // Thin shelves break up the silhouette and catch light along the cliff face.
                if (row == 1 || row == 3)
                {
                    float shelfZ = z + Rf(-0.45f, 0.45f);
                    float shelfX = Center(shelfZ) + side * (halfWater + width * 0.5f + 0.5f);
                    float shelfY = Height(shelfX, shelfZ) + height * Rf(0.48f, 0.62f);
                    if (SourceRockAllowed(shelfX, shelfZ))
                    {
                        bt.Add(mRocks[rng.Next(mRocks.Length)], mats[side < 0 ? 0 : 1],
                            new Vector3(shelfX, shelfY, shelfZ),
                            Quaternion.Euler(Rf(-8f, 8f), Rf(-18f, 18f), Rf(-8f, 8f)),
                            new Vector3(width * Rf(0.72f, 0.9f), Rf(0.65f, 1.05f), depth * 0.72f));
                    }
                }
            }
        }

        // Rounded stones at the foot of the cliff, deliberately sparse beside the water.
        for (int i = 0; i < 12; i++)
        {
            float z = sz + 1.0f + i * 0.82f + Rf(-0.3f, 0.3f);
            int side = (i % 2 == 0) ? -1 : 1;
            float x = Center(z) + side * (halfWater + Rf(1.45f, 2.35f));
            if (!SourceRockAllowed(x, z)) continue;
            float sc = Rf(0.75f, 1.55f);
            PlaceRock(bt, new Vector3(x, Height(x, z) - 0.04f, z), sc, mats);
        }

        bt.Flush(holder, "SourceCliffFrame", true);
    }

    bool SourceRockAllowed(float x, float z)
    {
        if (!InGround(new Vector3(x, 0f, z), 1.0f)) return false;
        // edgeIn is the actual FloatingIsland outline when one is present.
        // A small negative tolerance avoids visible gaps on a curved shoreline.
        return c.edgeIn == null || c.edgeIn(x, z) >= -0.5f;
    }

    void AddSourceCliffMass(LocBatcher bt, Material[] mats, float x, float z,
        float width, float height, float depth, float yaw, float embed)
    {
        if (!SourceRockAllowed(x, z)) return;
        float ground = Height(x, z);
        float y = ground + height * (0.5f - embed);
        Material mat = mats[rng.Next(0, 3)]; // keep the main cliff in cool stone tones; moss is reserved for accents
        bt.Add(mRocks[rng.Next(mRocks.Length)], mat,
            new Vector3(x, y, z),
            Quaternion.Euler(Rf(-5f, 5f), yaw + Rf(-10f, 10f), Rf(-6f, 6f)),
            new Vector3(width, height, depth));
    }

    void ScatterSource(Transform parent, bool mountain)
    {
        if (!c.s.riverFullLength) return;
        float sz = Mathf.Min(c.s.riverSourceZ, c.half - 8f);
        var holder = new GameObject("SourceRocks").transform;
        holder.SetParent(parent, false);
        var bt = new LocBatcher(24f);
        var mats = RockMats();
        float cx = Center(sz);
        for (int i = 0; i < (mountain ? 0 : 15); i++)
        {
            float ang = Mathf.PI * (0.12f + 0.76f * i / 14f) + Rf(-0.10f, 0.10f);   // broken horseshoe around the spring lip
            float rr = Rf(2.0f, 3.8f);
            Vector3 p = ToWorld(sz + 1f + Mathf.Sin(ang) * rr, cx + Mathf.Cos(ang) * rr * 1.1f, 0f);
            if (!InGround(p, 1.5f)) continue;
            p.y = Height(p.x, p.z) - 0.05f;
            PlaceRock(bt, p, Rf(0.9f, 1.9f), mats);
        }
        for (int i = 0; i < 8; i++)
        {
            float z = sz - 2f - i * 2.5f + Rf(-0.8f, 0.8f);
            if (!HasWater(z)) continue;
            Vector3 p = ToWorld(z, Center(z) + Rf(-1.4f, 1.4f), 0f);
            if (!InGround(p, 1.5f)) continue;
            p.y = Height(p.x, p.z) - 0.05f;
            PlaceRock(bt, p, Rf(0.4f, 0.9f), mats);
        }
        bt.Flush(holder, "SourceRocks", true);
    }

    void ScatterLilies(Transform parent)
    {
        var holder = new GameObject("LilyPads").transform;
        holder.SetParent(parent, false);
        Material m1 = LocKit.Mat("PadA", new Color(0.30f, 0.70f, 0.28f), 0.3f);
        Material m2 = LocKit.Mat("PadB", new Color(0.22f, 0.58f, 0.26f), 0.3f);
        Material mf = LocKit.Mat("PadFlower", new Color(1f, 0.62f, 0.78f), 0.2f);
        var bobType = LocKit.FindType("RiverBob");

        int placed = 0, tries = 0;
        float cs = 0f, cv = 0f;
        while (placed < 22 && tries++ < 900)
        {
            float s, v;
            if (placed % 4 == 0 || tries % 17 == 0)
            {
                s = Rf(-c.half, c.half);
                if (!HasWater(s)) continue;
                v = Center(s) + Rf(-1f, 1f) * c.riverWidth * 0.3f;
                cs = s; cv = v;
            }
            else
            {
                s = cs + Rf(-1.6f, 1.6f);
                v = cv + Rf(-1.6f, 1.6f);
            }
            float sdp = SignedDepth(s, v);
            if (sdp < 0.2f || sdp > 0.75f) continue;
            if (NearBridge(s, 2.5f)) continue;
            if (!InGround(ToWorld(s, v, 0f), 2f)) continue;

            var go = new GameObject("Pad");
            go.transform.SetParent(holder, false);
            SetLocal(go.transform, ToWorld(s, v, WaterY + 0.03f), Quaternion.Euler(0f, Rf(0f, 360f), 0f));
            float sc = Rf(0.7f, 1.5f);
            go.transform.localScale = new Vector3(sc, 1f, sc);
            go.AddComponent<MeshFilter>().sharedMesh = mPad;
            go.AddComponent<MeshRenderer>().sharedMaterial = rng.Next(2) == 0 ? m1 : m2;
            if (rng.NextDouble() < 0.35)
            {
                var fl = new GameObject("Flower");
                fl.transform.SetParent(go.transform, false);
                fl.transform.localPosition = new Vector3(0.05f, 0.01f, 0.04f);
                fl.AddComponent<MeshFilter>().sharedMesh = mLilyFlower;
                fl.AddComponent<MeshRenderer>().sharedMaterial = mf;
            }
            if (bobType != null) go.AddComponent(bobType);
            placed++;
        }
    }

    // ------------------------------------------------------------------ мост (3 стадии)
    sealed class BR
    {
        public Vector3 cw, tan, across;
        public float a, span, deckY, w, leftEnd, rightStart, hump;
        public Quaternion rot;
        // off = расстояние вдоль моста от начала, lat = в сторону (вдоль реки)
        public Vector3 At(float off, float lat, float y)
        {
            Vector3 p = cw + across * (off - a) + tan * lat;
            p.y = y;
            return p;
        }
    }

    float ArchY(BR b, float off)
    {
        float tt = Mathf.Clamp01(off / b.span);
        float uq = 2f * tt - 1f;
        return b.deckY + b.hump * (1f - uq * uq);
    }

    float ArchAng(BR b, float off)
    {
        float tt = Mathf.Clamp01(off / b.span);
        float slope = b.hump * (-4f * (2f * tt - 1f)) / b.span;
        return Mathf.Atan(slope) * Mathf.Rad2Deg;
    }

    static void SetField(object o, string name, object val)
    {
        var f = o.GetType().GetField(name);
        if (f != null) f.SetValue(o, val);
    }

    void BuildBridge()
    {
        float s0 = bridgeS;
        if (!HasWater(s0))
        {
            Debug.LogWarning("[LocationRiver] В точке моста нет воды, мост не построен.");
            return;
        }
        var brGo = new GameObject("BrokenBridge");   // имя оставлено для твоих скриптов
        brGo.transform.SetParent(node, false);

        var b = new BR();
        b.cw = ToWorld(s0, Center(s0), 0f);
        b.across = new Vector3(c.bridgeSide, 0f, 0f);       // от площадки к реке
        b.tan = Vector3.Cross(b.across, Vector3.up).normalized;
        float eA = ShoreAlong(b.cw, -b.across), eB = ShoreAlong(b.cw, b.across);
        b.a = eA + 1.6f;
        b.span = b.a + eB + 1.6f;
        b.rot = Quaternion.LookRotation(b.across, Vector3.up);
        b.deckY = 0.14f;
        b.w = c.s.bridgeWidth;
        b.leftEnd = b.span * 0.42f;
        b.rightStart = b.span * 0.66f;
        b.hump = 0f;

        var r = new System.Random(c.seed * 31 + 7);
        var s1 = BuildStage1(brGo.transform, b, r);
        var s2 = BuildStage2(brGo.transform, b, r);
        b.hump = Mathf.Min(0.42f, b.span * 0.04f);
        var s3 = BuildStage3(brGo.transform, b, r);

        // стенки на провале (только стадия 1)
        var bl = new GameObject("Blockers");
        bl.transform.SetParent(brGo.transform, false);
        float[] at = { b.leftEnd + 0.1f, b.rightStart - 0.1f };
        for (int i = 0; i < at.Length; i++)
        {
            var g = new GameObject("Blocker_" + i);
            g.transform.SetParent(bl.transform, false);
            SetLocal(g.transform, b.At(at[i], 0f, b.deckY + 1.5f), b.rot);
            g.AddComponent<BoxCollider>().size = new Vector3(b.w + 0.5f, 3.2f, 0.3f);
        }

        int st = Mathf.Clamp(c.s.bridgeStage, 1, 3);
        var compType = LocKit.FindType("RiverBridge");
        if (compType != null)
        {
            var comp = brGo.AddComponent(compType);
            SetField(comp, "stages", new GameObject[] { s1, s2, s3 });
            SetField(comp, "blockers", bl);
            SetField(comp, "stage", st);
        }
        else Debug.LogWarning("[LocationRiver] RiverBridge.cs не найден (положи в Assets/_Project/Scripts). В редакторе стадии работают, из кода игры нет.");
        s1.SetActive(st == 1);
        s2.SetActive(st == 2);
        s3.SetActive(st == 3);
        bl.SetActive(st == 1);

        // маркеры для логики стройки моста
        Transform mp = c.root.Find("Markers");
        if (mp == null) mp = c.root;
        Vector3 va = b.At(-0.7f, 0f, 0f); va.y = Height(va.x, va.z);
        Vector3 vb = b.At(b.span + 0.7f, 0f, 0f); vb.y = Height(vb.x, vb.z);
        Vector3 vg = b.At((b.leftEnd + b.rightStart) * 0.5f, 0f, b.deckY);
        Marker("BridgePoint_A", mp, node.TransformPoint(va), b.rot);
        Marker("BridgePoint_B", mp, node.TransformPoint(vb), b.rot);
        Marker("BridgeGap", mp, node.TransformPoint(vg), b.rot);
        Marker("BridgeBroken", mp, node.TransformPoint(vg), b.rot);
    }

    static void Marker(string name, Transform parent, Vector3 worldPos, Quaternion rot)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.position = worldPos;
        g.transform.rotation = rot;
    }

    // ---------- общие кирпичики
    void AddBeam(LocBatcher bt, Material m, Vector3 p0, Vector3 p1, float tx, float ty)
    {
        Vector3 d = p1 - p0;
        float len = d.magnitude;
        if (len < 0.001f) return;
        Vector3 dn = d / len;
        Vector3 up = Mathf.Abs(Vector3.Dot(dn, Vector3.up)) > 0.98f ? Vector3.right : Vector3.up;
        bt.Add(mBox, m, (p0 + p1) * 0.5f, Quaternion.LookRotation(dn, up), new Vector3(tx, ty, len));
    }

    void AddRope(LocBatcher bt, Material m, Vector3 p0, Vector3 p1, float sag, float th, int seg)
    {
        Vector3 prev = p0;
        for (int i = 1; i <= seg; i++)
        {
            float t = i / (float)seg;
            Vector3 q = Vector3.Lerp(p0, p1, t);
            q.y -= sag * 4f * t * (1f - t);
            AddBeam(bt, m, prev, q, th, th);
            prev = q;
        }
    }

    void AddPile(LocBatcher bt, Material m, BR b, float off, float lat, float topY, float diam, float tilt)
    {
        Vector3 p = b.At(off, lat, 0f);
        float bed = Height(p.x, p.z) - 0.25f;
        float h = Mathf.Max(0.3f, topY - bed);
        bt.Add(mCyl6, m, new Vector3(p.x, bed + h * 0.5f, p.z), b.rot * Quaternion.Euler(tilt, 0f, tilt * 0.5f), new Vector3(diam, h, diam));
    }

    void AddAbutment(LocBatcher bt, Material m1, Material m2, BR b, bool endB, bool fancy)
    {
        const float thick = 1.2f;
        float off = endB ? b.span - thick * 0.5f : thick * 0.5f;
        Vector3 g = b.At(off, 0f, 0f);
        float ground = Height(g.x, g.z);
        float top = b.deckY - 0.04f;
        float bottom = Mathf.Min(ground, WaterY) - 0.35f;
        float h = top - bottom;
        bt.Add(mBox, m1, new Vector3(g.x, bottom + h * 0.5f, g.z), b.rot, new Vector3(b.w + 0.9f, h, thick));
        if (!fancy) return;
        float wingLen = thick + 1.4f;
        float woff = endB ? b.span - wingLen * 0.5f : wingLen * 0.5f;
        for (int sgn = -1; sgn <= 1; sgn += 2)
        {
            Vector3 wp = b.At(woff, sgn * (b.w * 0.5f + 0.35f), 0f);
            float wg = Height(wp.x, wp.z);
            float wb = Mathf.Min(wg, WaterY) - 0.35f;
            float wt = b.deckY + 0.35f;
            bt.Add(mBox, m1, new Vector3(wp.x, wb + (wt - wb) * 0.5f, wp.z), b.rot, new Vector3(0.5f, wt - wb, wingLen));
            bt.Add(mBox, m2, new Vector3(wp.x, wt + 0.05f, wp.z), b.rot, new Vector3(0.62f, 0.1f, wingLen + 0.1f));
        }
    }

    void AddLantern(LocBatcher bt, Vector3 cc, Material lamp, Material iron)
    {
        bt.Add(mCyl6, lamp, cc, Quaternion.identity, new Vector3(0.18f, 0.24f, 0.18f));
        bt.Add(mCone6, iron, cc + Vector3.up * 0.17f, Quaternion.identity, new Vector3(0.28f, 0.12f, 0.28f));
        bt.Add(mCyl6, iron, cc - Vector3.up * 0.14f, Quaternion.identity, new Vector3(0.22f, 0.04f, 0.22f));
        bt.Add(mCyl6, iron, cc + Vector3.up * 0.25f, Quaternion.identity, new Vector3(0.04f, 0.08f, 0.04f));
    }

    void AddBarrel(LocBatcher bt, Vector3 ground, Material wood, Material iron)
    {
        bt.Add(mCyl8, wood, ground + Vector3.up * 0.35f, Quaternion.identity, new Vector3(0.55f, 0.7f, 0.55f));
        bt.Add(mCyl8, iron, ground + Vector3.up * 0.18f, Quaternion.identity, new Vector3(0.58f, 0.05f, 0.58f));
        bt.Add(mCyl8, iron, ground + Vector3.up * 0.52f, Quaternion.identity, new Vector3(0.58f, 0.05f, 0.58f));
    }

    void AddCrate(LocBatcher bt, Vector3 ground, float yaw, float size, Material wood, Material dark)
    {
        Quaternion q = Quaternion.Euler(0f, yaw, 0f);
        bt.Add(mBox, wood, ground + Vector3.up * (size * 0.5f), q, Vector3.one * size);
        bt.Add(mBox, dark, ground + Vector3.up * (size + 0.03f), q, new Vector3(size * 1.06f, 0.06f, size * 1.06f));
        bt.Add(mBox, dark, ground + Vector3.up * (size * 0.5f), q, new Vector3(size * 1.03f, 0.08f, size * 1.03f));
    }

    void AddDeckColliders(Transform parent, BR b, float o0, float o1, int seg, System.Func<float, float> hy, bool sides)
    {
        for (int k = 0; k < seg; k++)
        {
            float t0 = o0 + (o1 - o0) * k / seg, t1 = o0 + (o1 - o0) * (k + 1) / seg;
            Vector3 p0 = b.At(t0, 0f, hy(t0)), p1 = b.At(t1, 0f, hy(t1));
            Vector3 d = p1 - p0;
            float len = d.magnitude + 0.05f;
            Quaternion q = Quaternion.LookRotation(d.normalized, Vector3.up);
            Vector3 mid = (p0 + p1) * 0.5f;

            var go = new GameObject("DeckCol");
            go.transform.SetParent(parent, false);
            SetLocal(go.transform, mid - Vector3.up * 0.04f, q);
            go.AddComponent<BoxCollider>().size = new Vector3(b.w + 0.1f, 0.2f, len);
            if (!sides) continue;
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                var sg = new GameObject("SideCol");
                sg.transform.SetParent(parent, false);
                SetLocal(sg.transform, mid + b.tan * (sgn * (b.w * 0.5f + 0.05f)) + Vector3.up * 0.6f, q);
                sg.AddComponent<BoxCollider>().size = new Vector3(0.12f, 1.4f, len);
            }
        }
    }

    void MakeFloater(Transform parent, Material m, Vector3 pos, Quaternion rot, Vector3 size, System.Type bob)
    {
        var go = new GameObject("FallenPlank");
        go.transform.SetParent(parent, false);
        SetLocal(go.transform, pos, rot);
        go.transform.localScale = size;
        go.AddComponent<MeshFilter>().sharedMesh = mBox;
        go.AddComponent<MeshRenderer>().sharedMaterial = m;
        if (bob != null) go.AddComponent(bob);
    }

    // ---------- СТАДИЯ 1: сломан полностью
    GameObject BuildStage1(Transform parent, BR b, System.Random r)
    {
        var st = new GameObject("Stage1");
        st.transform.SetParent(parent, false);
        var bt = new LocBatcher(1e6f);
        Material wood = LocKit.Mat("BridgeWood", new Color(0.66f, 0.44f, 0.24f), 0.1f);
        Material dark = LocKit.Mat("BridgeDark", new Color(0.40f, 0.26f, 0.14f), 0.1f);
        Material old = LocKit.Mat("BridgeOld", new Color(0.52f, 0.40f, 0.30f), 0.05f);
        Material stone = LocKit.Mat("BridgeStone", new Color(0.60f, 0.58f, 0.64f), 0.05f);
        Material rope = LocKit.Mat("BridgeRope", new Color(0.78f, 0.68f, 0.45f), 0.05f);
        var bob = LocKit.FindType("RiverBob");
        float w = b.w;

        int count = Mathf.Max(4, Mathf.RoundToInt(b.span / 0.58f));
        float pitch = b.span / count, plank = pitch * 0.86f;
        for (int i = 0; i < count; i++)
        {
            float off = (i + 0.5f) * pitch;
            bool left = off < b.leftEnd, right = off > b.rightStart;
            if (left || right)
            {
                bool near = (left && off > b.leftEnd - pitch * 2.2f) || (right && off < b.rightStart + pitch * 2.2f);
                if (!near && r.NextDouble() < 0.07) continue;
                float tilt = 0f, drop = 0f;
                if (near && r.NextDouble() < 0.6)
                {
                    tilt = (left ? 1f : -1f) * (float)(15 + r.NextDouble() * 30);
                    drop = -0.12f;
                }
                float jy = (float)(r.NextDouble() - 0.5) * 0.03f;
                float jr = (float)(r.NextDouble() - 0.5) * 3f;
                Material m = (i % 2 == 0) ? wood : (r.NextDouble() < 0.3 ? old : dark);
                bt.Add(mBox, m, b.At(off, 0f, b.deckY + drop + jy), b.rot * Quaternion.Euler(tilt, jr, 0f),
                    new Vector3(w * (0.96f + (float)r.NextDouble() * 0.06f), 0.1f, plank));
            }
            else if (r.NextDouble() < 0.22)
            {
                float lat = (float)(r.NextDouble() - 0.5) * w * 1.1f;
                MakeFloater(st.transform, wood, b.At(off, lat, WaterY + 0.04f),
                    b.rot * Quaternion.Euler((float)r.NextDouble() * 8f, (float)r.NextDouble() * 360f, (float)r.NextDouble() * 8f),
                    new Vector3(w * 0.8f, 0.1f, plank), bob);
            }
        }

        float midOff = (b.leftEnd + b.rightStart) * 0.5f;
        float[] sides = { -(w * 0.5f - 0.2f), (w * 0.5f - 0.2f) };
        foreach (float ox in sides)
        {
            float lLen = b.leftEnd + 0.25f;
            bt.Add(mBox, dark, b.At(lLen * 0.5f, ox, b.deckY - 0.14f), b.rot * Quaternion.Euler(4f, 0f, 0f), new Vector3(0.2f, 0.2f, lLen));
            float r0 = b.rightStart - 0.25f, rLen = b.span - r0;
            bt.Add(mBox, dark, b.At(r0 + rLen * 0.5f, ox, b.deckY - 0.14f), b.rot * Quaternion.Euler(-4f, 0f, 0f), new Vector3(0.2f, 0.2f, rLen));
            bt.Add(mBox, old, b.At(b.leftEnd + 0.15f, ox, b.deckY - 0.34f), b.rot * Quaternion.Euler(38f, 0f, 0f), new Vector3(0.18f, 0.18f, 0.6f));
            bt.Add(mBox, old, b.At(b.rightStart - 0.15f, ox, b.deckY - 0.34f), b.rot * Quaternion.Euler(-38f, 0f, 0f), new Vector3(0.18f, 0.18f, 0.6f));

            for (float o = 0.3f; o < b.leftEnd - 0.5f; o += 2.2f)
                bt.Add(mBox, dark, b.At(o, ox, b.deckY + 0.5f), b.rot, new Vector3(0.16f, 1f, 0.16f));
            for (float o = b.rightStart + 0.9f; o < b.span - 0.2f; o += 2.2f)
                bt.Add(mBox, dark, b.At(o, ox, b.deckY + 0.5f), b.rot, new Vector3(0.16f, 1f, 0.16f));
            float rl0 = 0.3f, rl1 = b.leftEnd - 0.8f;
            bt.Add(mBox, wood, b.At((rl0 + rl1) * 0.5f, ox, b.deckY + 0.9f), b.rot, new Vector3(0.1f, 0.1f, rl1 - rl0));
            float rr0 = b.rightStart + 0.9f, rr1 = b.span - 0.3f;
            bt.Add(mBox, wood, b.At((rr0 + rr1) * 0.5f, ox, b.deckY + 0.9f), b.rot, new Vector3(0.1f, 0.1f, rr1 - rr0));

            bt.Add(mBox, dark, b.At(b.leftEnd - 0.2f, ox, b.deckY + 0.45f), b.rot * Quaternion.Euler(28f, 0f, 0f), new Vector3(0.16f, 1f, 0.16f));
            bt.Add(mBox, dark, b.At(b.rightStart + 0.2f, ox, b.deckY + 0.45f), b.rot * Quaternion.Euler(-28f, 0f, 0f), new Vector3(0.16f, 1f, 0.16f));
            AddBeam(bt, rope, b.At(b.leftEnd + 0.3f, ox, b.deckY + 0.85f), b.At(b.leftEnd + 0.55f, ox, b.deckY - 0.5f), 0.035f, 0.035f);
            AddBeam(bt, rope, b.At(b.rightStart - 0.3f, ox, b.deckY + 0.85f), b.At(b.rightStart - 0.5f, ox, b.deckY - 0.6f), 0.035f, 0.035f);

            AddPile(bt, dark, b, b.leftEnd * 0.5f, ox, b.deckY - 0.24f, 0.28f, 0f);
            AddPile(bt, dark, b, (b.rightStart + b.span) * 0.5f, ox, b.deckY - 0.24f, 0.28f, 0f);
            AddPile(bt, old, b, midOff - 0.9f, ox, WaterY + 0.35f, 0.26f, ox < 0f ? 10f : -12f);
            AddPile(bt, old, b, midOff + 0.9f, ox, WaterY + 0.15f, 0.24f, ox < 0f ? -14f : 9f);
        }

        AddAbutment(bt, stone, stone, b, false, false);
        AddAbutment(bt, stone, stone, b, true, false);
        bt.Flush(st.transform, "S1", true);

        var col = new GameObject("Colliders");
        col.transform.SetParent(st.transform, false);
        System.Func<float, float> flatY = uu => b.deckY;
        AddDeckColliders(col.transform, b, 0f, b.leftEnd, 2, flatY, true);
        AddDeckColliders(col.transform, b, b.rightStart, b.span, 2, flatY, true);
        return st;
    }

    // ---------- СТАДИЯ 2: починен кое-как
    GameObject BuildStage2(Transform parent, BR b, System.Random r)
    {
        var st = new GameObject("Stage2");
        st.transform.SetParent(parent, false);
        var bt = new LocBatcher(1e6f);
        Material wood = LocKit.Mat("BridgeWood", new Color(0.66f, 0.44f, 0.24f), 0.1f);
        Material dark = LocKit.Mat("BridgeDark", new Color(0.40f, 0.26f, 0.14f), 0.1f);
        Material old = LocKit.Mat("BridgeOld", new Color(0.52f, 0.40f, 0.30f), 0.05f);
        Material fresh = LocKit.Mat("BridgeNew", new Color(0.86f, 0.64f, 0.36f), 0.1f);
        Material fresh2 = LocKit.Mat("BridgeNew2", new Color(0.92f, 0.72f, 0.42f), 0.1f);
        Material stone = LocKit.Mat("BridgeStone", new Color(0.60f, 0.58f, 0.64f), 0.05f);
        Material rope = LocKit.Mat("BridgeRope", new Color(0.78f, 0.68f, 0.45f), 0.05f);
        Material iron = LocKit.Mat("BridgeIron", new Color(0.20f, 0.20f, 0.24f), 0.2f);
        Material lamp = LocKit.Mat("BridgeLamp", new Color(1f, 0.85f, 0.45f), 0.2f, 2.2f);
        float w = b.w;

        int count = Mathf.Max(4, Mathf.RoundToInt(b.span / 0.56f));
        float pitch = b.span / count, plank = pitch * 0.9f;
        for (int i = 0; i < count; i++)
        {
            float off = (i + 0.5f) * pitch;
            bool patch = off > b.leftEnd - 1.0f && off < b.rightStart + 1.0f;
            Material m = patch ? (r.NextDouble() < 0.3 ? fresh2 : fresh) : ((i % 2 == 0) ? wood : old);
            float jr = ((float)r.NextDouble() - 0.5f) * (patch ? 3f : 1.5f);
            float jy = ((float)r.NextDouble() - 0.5f) * 0.02f;
            bt.Add(mBox, m, b.At(off, 0f, b.deckY + jy), b.rot * Quaternion.Euler(0f, jr, 0f),
                new Vector3(w * (0.97f + (float)r.NextDouble() * 0.05f), 0.1f, plank));
        }

        float[] sides = { -(w * 0.5f - 0.2f), (w * 0.5f - 0.2f) };
        foreach (float ox in sides)
        {
            bt.Add(mBox, dark, b.At(b.span * 0.5f, ox, b.deckY - 0.14f), b.rot, new Vector3(0.2f, 0.2f, b.span));
            AddPile(bt, dark, b, b.leftEnd - 0.2f, ox, b.deckY - 0.25f, 0.3f, 0f);
            AddPile(bt, dark, b, b.rightStart + 0.2f, ox, b.deckY - 0.25f, 0.3f, 0f);
        }
        bt.Add(mBox, dark, b.At(b.leftEnd - 0.2f, 0f, b.deckY - 0.3f), b.rot, new Vector3(w + 0.3f, 0.14f, 0.24f));
        bt.Add(mBox, dark, b.At(b.rightStart + 0.2f, 0f, b.deckY - 0.3f), b.rot, new Vector3(w + 0.3f, 0.14f, 0.24f));

        int np = Mathf.Max(3, Mathf.RoundToInt((b.span - 1f) / 2f) + 1);
        float o0 = 0.5f, o1 = b.span - 0.5f;
        for (int sd = 0; sd < 2; sd++)
        {
            float ox = sd == 0 ? -(w * 0.5f - 0.05f) : (w * 0.5f - 0.05f);
            Vector3 prevTop = Vector3.zero, prevLow = Vector3.zero;
            for (int k = 0; k < np; k++)
            {
                float off = Mathf.Lerp(o0, o1, k / (float)(np - 1));
                float tl = ((float)r.NextDouble() - 0.5f) * 5f;
                bt.Add(mBox, dark, b.At(off, ox, b.deckY + 0.55f), b.rot * Quaternion.Euler(tl, 0f, tl * 0.6f), new Vector3(0.15f, 1.1f, 0.15f));
                Vector3 top = b.At(off, ox, b.deckY + 1.0f), low = b.At(off, ox, b.deckY + 0.55f);
                if (k > 0)
                {
                    AddRope(bt, rope, prevTop, top, 0.08f, 0.035f, 4);
                    AddRope(bt, rope, prevLow, low, 0.06f, 0.035f, 4);
                }
                prevTop = top; prevLow = low;
                if (k == 0 || k == np - 1)
                    AddLantern(bt, b.At(off, ox, b.deckY + 1.32f), lamp, iron);
            }
        }

        AddAbutment(bt, stone, stone, b, false, false);
        AddAbutment(bt, stone, stone, b, true, false);

        Vector3 gA = b.At(-1.1f, w * 0.5f + 0.9f, 0f);
        gA.y = Height(gA.x, gA.z);
        AddBarrel(bt, gA, wood, iron);
        Vector3 gB = b.At(b.span + 1.1f, -(w * 0.5f + 0.8f), 0f);
        gB.y = Height(gB.x, gB.z);
        AddCrate(bt, gB, 20f, 0.55f, fresh, dark);
        Vector3 gC = b.At(b.span + 1.0f, -(w * 0.5f + 1.6f), 0f);
        gC.y = Height(gC.x, gC.z);
        AddCrate(bt, gC, -15f, 0.45f, wood, dark);
        Vector3 gD = gB + Vector3.up * 0.55f;
        AddCrate(bt, gD, 40f, 0.38f, fresh2, dark);
        bt.Flush(st.transform, "S2", true);

        var col = new GameObject("Colliders");
        col.transform.SetParent(st.transform, false);
        System.Func<float, float> flatY = uu => b.deckY;
        AddDeckColliders(col.transform, b, 0f, b.span, 3, flatY, true);
        return st;
    }

    // ---------- СТАДИЯ 3: красивый арочный мост
    GameObject BuildStage3(Transform parent, BR b, System.Random r)
    {
        var st = new GameObject("Stage3");
        st.transform.SetParent(parent, false);
        var bt = new LocBatcher(1e6f);
        Material honeyA = LocKit.Mat("BridgePlankA", new Color(0.88f, 0.64f, 0.36f), 0.1f);
        Material honeyB = LocKit.Mat("BridgePlankB", new Color(0.80f, 0.55f, 0.30f), 0.1f);
        Material under = LocKit.Mat("BridgeUnder", new Color(0.58f, 0.38f, 0.22f), 0.1f);
        Material cream = LocKit.Mat("BridgeCream", new Color(0.95f, 0.90f, 0.78f), 0.1f);
        Material cap = LocKit.Mat("BridgeCap", new Color(0.82f, 0.44f, 0.30f), 0.1f);
        Material stone = LocKit.Mat("BridgeStone", new Color(0.60f, 0.58f, 0.64f), 0.05f);
        Material stoneB = LocKit.Mat("BridgeStone2", new Color(0.52f, 0.52f, 0.60f), 0.05f);
        Material moss = LocKit.Mat("BridgeMoss", new Color(0.36f, 0.58f, 0.26f), 0.05f);
        Material iron = LocKit.Mat("BridgeIron", new Color(0.20f, 0.20f, 0.24f), 0.2f);
        Material lamp = LocKit.Mat("BridgeLamp", new Color(1f, 0.85f, 0.45f), 0.2f, 2.2f);
        Material pot = LocKit.Mat("BridgePot", new Color(0.78f, 0.42f, 0.28f), 0.1f);
        Material leaf = LocKit.Mat("BridgeLeaf", new Color(0.30f, 0.62f, 0.26f), 0.05f);
        Material ropeM = LocKit.Mat("BridgeRope", new Color(0.78f, 0.68f, 0.45f), 0.05f);
        Material[] petals = FlowerMats();
        Material[] flags =
        {
            LocKit.Mat("FlagRed", new Color(0.90f, 0.32f, 0.30f), 0.05f),
            LocKit.Mat("FlagYellow", new Color(1.00f, 0.82f, 0.28f), 0.05f),
            LocKit.Mat("FlagTeal", new Color(0.30f, 0.78f, 0.72f), 0.05f),
            LocKit.Mat("FlagPink", new Color(1.00f, 0.55f, 0.72f), 0.05f),
            LocKit.Mat("FlagBlue", new Color(0.38f, 0.55f, 0.95f), 0.05f)
        };
        float w = b.w, span = b.span;

        int count = Mathf.Max(6, Mathf.RoundToInt(span / 0.5f));
        float pitch = span / count, plank = pitch * 0.92f;
        for (int i = 0; i < count; i++)
        {
            float off = (i + 0.5f) * pitch;
            bt.Add(mBox, (i % 2 == 0) ? honeyA : honeyB, b.At(off, 0f, ArchY(b, off)),
                b.rot * Quaternion.Euler(-ArchAng(b, off), 0f, 0f), new Vector3(w, 0.1f, plank));
        }

        int segs = Mathf.Max(6, Mathf.RoundToInt(span / 0.8f));
        for (int sgn = -1; sgn <= 1; sgn += 2)
        {
            float latE = sgn * (w * 0.5f + 0.04f);
            float latG = sgn * (w * 0.3f);
            for (int k = 0; k < segs; k++)
            {
                float q0 = span * k / segs, q1 = span * (k + 1) / segs;
                AddBeam(bt, cream, b.At(q0, latE, ArchY(b, q0) - 0.02f), b.At(q1, latE, ArchY(b, q1) - 0.02f), 0.14f, 0.2f);
                AddBeam(bt, under, b.At(q0, latG, ArchY(b, q0) - 0.2f), b.At(q1, latG, ArchY(b, q1) - 0.2f), 0.16f, 0.2f);
            }
        }

        float[] piers = { span * 0.3f, span * 0.7f };
        foreach (float po in piers)
        {
            Vector3 g = b.At(po, 0f, 0f);
            float bed = Height(g.x, g.z) - 0.3f;
            float top = ArchY(b, po) - 0.22f;
            float h = top - bed;
            float pierLat = w + 0.3f;
            float core = pierLat - 0.8f;
            Vector3 center = new Vector3(g.x, bed + h * 0.5f, g.z);
            bt.Add(mBox, stone, center, b.rot, new Vector3(core, h, 0.8f));
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                Vector3 ep = b.At(po, sgn * core * 0.5f, center.y);
                bt.Add(mCyl8, stoneB, ep, Quaternion.identity, new Vector3(0.8f, h, 0.8f));
            }
            bt.Add(mBox, stoneB, b.At(po, 0f, top + 0.06f), b.rot, new Vector3(pierLat + 0.2f, 0.14f, 1.0f));
            bt.Add(mBox, moss, b.At(po, 0f, WaterY), b.rot, new Vector3(core + 0.02f, 0.3f, 0.86f));
        }

        AddAbutment(bt, stone, stoneB, b, false, true);
        AddAbutment(bt, stone, stoneB, b, true, true);

        int np = Mathf.Max(4, Mathf.RoundToInt((span - 1.4f) / 1.7f) + 1);
        float po0 = 0.6f, po1 = span - 0.6f;
        float latP = w * 0.5f - 0.02f;
        var offs = new float[np];
        for (int k = 0; k < np; k++) offs[k] = Mathf.Lerp(po0, po1, k / (float)(np - 1));

        for (int sd = 0; sd < 2; sd++)
        {
            float lat = sd == 0 ? -latP : latP;
            float outSgn = sd == 0 ? -1f : 1f;
            Vector3 poleTopA = Vector3.zero, poleTopB = Vector3.zero;

            for (int k = 0; k < np; k++)
            {
                float off = offs[k];
                float y = ArchY(b, off);
                bool end = (k == 0 || k == np - 1);
                float ph = end ? 1.15f : 1.0f;
                bt.Add(mBox, cream, b.At(off, lat, y + ph * 0.5f + 0.05f), b.rot, new Vector3(0.17f, ph, 0.17f));
                bt.Add(mBox, cap, b.At(off, lat, y + ph + 0.09f), b.rot, new Vector3(0.26f, 0.09f, 0.26f));
                if (end)
                {
                    Vector3 pt = b.At(off, lat, y + ph + 0.14f);
                    bt.Add(mCyl6, iron, pt + Vector3.up * 0.4f, Quaternion.identity, new Vector3(0.07f, 0.8f, 0.07f));
                    Vector3 tip = pt + Vector3.up * 0.8f;
                    AddLantern(bt, tip + Vector3.up * 0.14f, lamp, iron);
                    if (k == 0) poleTopA = tip; else poleTopB = tip;
                    Vector3 pp = b.At(off + (k == 0 ? -0.55f : 0.55f), lat + outSgn * 0.25f, 0f);
                    pp.y = Mathf.Max(Height(pp.x, pp.z), y - 0.1f);
                    bt.Add(mCyl8, pot, pp + Vector3.up * 0.18f, Quaternion.identity, new Vector3(0.38f, 0.36f, 0.38f));
                    bt.Add(mOcta, leaf, pp + Vector3.up * 0.42f, Quaternion.identity, new Vector3(0.5f, 0.3f, 0.5f));
                    for (int f = 0; f < 3; f++)
                        bt.Add(mOcta, petals[(k + f + sd) % petals.Length], pp + new Vector3((f - 1) * 0.1f, 0.58f + (f % 2) * 0.06f, ((f * 7) % 3 - 1) * 0.08f), Quaternion.identity, new Vector3(0.16f, 0.12f, 0.16f));
                }
            }

            for (int k = 0; k < np - 1; k++)
            {
                float a0 = offs[k], a1 = offs[k + 1];
                AddBeam(bt, cream, b.At(a0, lat, ArchY(b, a0) + 0.98f), b.At(a1, lat, ArchY(b, a1) + 0.98f), 0.1f, 0.08f);
                AddBeam(bt, cream, b.At(a0, lat, ArchY(b, a0) + 0.5f), b.At(a1, lat, ArchY(b, a1) + 0.5f), 0.08f, 0.07f);
                for (int j = 1; j <= 3; j++)
                {
                    float bo = Mathf.Lerp(a0, a1, j / 4f);
                    bt.Add(mBox, cream, b.At(bo, lat, ArchY(b, bo) + 0.76f), b.rot, new Vector3(0.05f, 0.46f, 0.05f));
                }
                if (k % 2 == 0)
                {
                    float mo = (a0 + a1) * 0.5f;
                    float my = ArchY(b, mo);
                    Quaternion rr = b.rot * Quaternion.Euler(-ArchAng(b, mo), 0f, 0f);
                    Vector3 bp = b.At(mo, lat + outSgn * 0.16f, my + 0.58f);
                    bt.Add(mBox, pot, bp, rr, new Vector3(0.22f, 0.16f, 0.9f));
                    bt.Add(mOcta, leaf, bp + Vector3.up * 0.12f, rr, new Vector3(0.24f, 0.18f, 0.9f));
                    for (int f = 0; f < 3; f++)
                    {
                        float fo = mo + (f - 1) * 0.3f;
                        bt.Add(mOcta, petals[(k + f + sd) % petals.Length], b.At(fo, lat + outSgn * 0.16f, ArchY(b, fo) + 0.78f),
                            Quaternion.identity, new Vector3(0.15f, 0.12f, 0.15f));
                    }
                }
            }

            // гирлянда флажков между фонарями
            Vector3 ga = poleTopA + Vector3.up * 0.02f, gb = poleTopB + Vector3.up * 0.02f;
            const float sag = 0.35f;
            AddRope(bt, ropeM, ga, gb, sag, 0.03f, 14);
            float dist = Vector3.Distance(ga, gb);
            int nf = Mathf.Max(4, Mathf.FloorToInt(dist / 0.45f));
            for (int j = 0; j < nf; j++)
            {
                float t = (j + 0.5f) / nf;
                Vector3 fp = Vector3.Lerp(ga, gb, t);
                fp.y -= sag * 4f * t * (1f - t);
                bt.Add(mFlag, flags[(j + sd * 2) % flags.Length], fp, b.rot, Vector3.one);
            }
        }
        bt.Flush(st.transform, "S3", true);

        var col = new GameObject("Colliders");
        col.transform.SetParent(st.transform, false);
        System.Func<float, float> archFn = uu => ArchY(b, uu);
        AddDeckColliders(col.transform, b, 0f, span, 14, archFn, true);
        return st;
    }

    // невидимая стенка над водой (на месте моста проход оставлен).
    // У водопада стены нет: она кончается за barrierEdgeGap до того места, где река выходит на кромку острова. Раньше коробка
    // высотой 3.6 тянулась до края террейна и висела невидимой стеной за обрывом, а вода там должна просто падать вниз.
    void BuildBarrier()
    {
        var parent = new GameObject("WaterBarrier");
        parent.transform.SetParent(node, false);
        const float seg = 2f;
        float endS = -c.half;                     // s, где русло впервые оказывается внутри острова (течёт на юг, к -z)
        if (c.edgeIn != null)
            for (float q = -c.half; q < c.half; q += 0.5f)
                if (c.edgeIn(Center(q), q) >= 1f) { endS = q; break; }
        float cutS = endS + Mathf.Max(0f, c.s.barrierEdgeGap);
        for (float s = -c.half + seg * 0.5f; s < c.half; s += seg)
        {
            if (s < cutS) continue;
            if (!HasWater(s)) continue;
            if (c.hasBridge && Mathf.Abs(s - bridgeS) <= c.s.bridgeWidth * 0.5f + seg * 0.5f) continue;
            float cc = Center(s);
            float dl = ShoreDist(s, -1), dr = ShoreDist(s, 1);
            float mid = cc + (dr - dl) * 0.5f;
            Vector3 dir = ToWorld(s + 0.5f, Center(s + 0.5f), 0f) - ToWorld(s - 0.5f, Center(s - 0.5f), 0f);
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) continue;
            Vector3 pos = ToWorld(s, mid, WaterY + 1.2f);
            if (!InGround(pos, 0f)) continue;
            var go = new GameObject("Seg");
            go.transform.SetParent(parent.transform, false);
            SetLocal(go.transform, pos, Quaternion.LookRotation(dir.normalized, Vector3.up));
            go.AddComponent<BoxCollider>().size = new Vector3(dl + dr + 0.8f, 3.6f, seg + 0.4f);
        }
    }

    // ------------------------------------------------------------------ шейдеры (обратные кавычки -> обычные)
    const string WaterShaderSource = @"
Shader `BuilderSim/LocCartoonWater`
{
    Properties
    {
        _ShallowColor (`Shallow`, Color) = (0.45, 0.92, 0.84, 0.55)
        _DeepColor (`Deep`, Color) = (0.06, 0.42, 0.64, 0.93)
        _FoamColor (`Foam`, Color) = (1, 1, 1, 1)
        _FoamWidth (`Foam width`, Range(0.02, 0.6)) = 0.14
        _DepthRange (`Depth range`, Float) = 2.0
        _Tiling (`Ripple tiling`, Float) = 0.9
        _Speed (`Flow speed`, Float) = 0.55
        _RippleStrength (`Ripple strength`, Range(0, 1)) = 0.5
        _Sparkle (`Sparkle`, Range(0, 1)) = 0.6
        _Caustics (`Caustics`, Range(0, 1)) = 0.6
    }
    SubShader
    {
        Tags { `RenderType`=`Transparent` `Queue`=`Transparent` `RenderPipeline`=`UniversalPipeline` `IgnoreProjector`=`True` }
        Pass
        {
            Name `Forward`
            Tags { `LightMode`=`UniversalForward` }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include `Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl`

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half4 _FoamColor;
                float _FoamWidth;
                float _DepthRange;
                float _Tiling;
                float _Speed;
                float _RippleStrength;
                float _Sparkle;
                float _Caustics;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 dep : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.uv = IN.uv;
                OUT.dep = IN.uv1;
                return OUT;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float2 hash22(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            float vnoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1.0, 0.0));
                float c = hash21(i + float2(0.0, 1.0));
                float d = hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float2 voro2(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float d1 = 8.0;
                float d2 = 8.0;
                [unroll] for (int y = -1; y <= 1; y++)
                {
                    [unroll] for (int x = -1; x <= 1; x++)
                    {
                        float2 g = float2(x, y);
                        float2 o = hash22(i + g);
                        o = 0.5 + 0.45 * sin(_Time.y * 0.7 + 6.2831 * o);
                        float2 r = g + o - f;
                        float d = dot(r, r);
                        if (d < d1) { d2 = d1; d1 = d; }
                        else if (d < d2) { d2 = d; }
                    }
                }
                return float2(sqrt(d1), sqrt(d2));
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float t = _Time.y * _Speed;
                float dep = IN.dep.x;
                float d01 = saturate(dep / max(0.01, _DepthRange));
                float2 p = IN.uv * _Tiling;

                float n1 = vnoise(float2(p.x * 2.2, p.y * 0.9 - t * 1.6) * 2.0);
                float n2 = vnoise(float2(p.x * 3.4 + 7.3, p.y * 1.3 - t * 2.3) * 2.0 + n1);
                float streak = 1.0 - smoothstep(0.0, 0.07, abs(n1 - n2));

                float2 vc = voro2(p * 1.35 + float2(0.0, -t * 0.6));
                float caustic = 1.0 - smoothstep(0.0, 0.12, vc.y - vc.x);

                float depthMix = smoothstep(0.0, 0.9, d01);
                depthMix = lerp(depthMix, floor(depthMix * 4.0 + 0.5) / 4.0, 0.45);
                half3 col = lerp(_ShallowColor.rgb, _DeepColor.rgb, depthMix);

                col += caustic * _Caustics * (1.0 - depthMix) * 0.22;
                col += streak * _RippleStrength * 0.20;

                float3 L = normalize(_MainLightPosition.xyz);
                float3 V = normalize(GetWorldSpaceViewDir(IN.positionWS));
                float3 N = normalize(float3((n1 - 0.5) * 0.45, 1.0, (n2 - 0.5) * 0.45));
                float3 R = reflect(-L, N);
                float spec = step(0.8, pow(saturate(dot(R, V)), 28.0));
                col += spec * _Sparkle * 0.6;
                float fres = pow(1.0 - saturate(dot(N, V)), 3.0);
                col = lerp(col, half3(0.75, 0.93, 1.0), fres * 0.18);

                float sp = vnoise(p * 9.0 + float2(t * 0.8, -t * 2.0));
                col += step(0.95, sp) * _Sparkle * 0.35;

                float wob = (vnoise(p * 6.0 + float2(0.0, -t)) - 0.5) * 0.10 + sin(_Time.y * 1.7 + p.y * 5.0) * 0.012;
                float foamEdge = dep + wob;
                float foam = 1.0 - smoothstep(_FoamWidth * 0.55, _FoamWidth, foamEdge);
                float foam2 = 1.0 - smoothstep(0.0, 0.03, abs(foamEdge - _FoamWidth * 1.9));
                float dash = step(0.5, vnoise(p * 7.0 + float2(0.0, -t)));
                col = lerp(col, _FoamColor.rgb, saturate(foam + foam2 * 0.55 * dash));

                float a = lerp(_ShallowColor.a, _DeepColor.a, depthMix);
                a = saturate(max(a, foam * 0.95) + streak * 0.05);
                a *= smoothstep(-0.02, 0.02, dep);
                return half4(col, a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
";

    public const string RingShaderSource = @"
Shader `BuilderSim/LocFoamRing`
{
    Properties
    {
        _Color (`Color`, Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { `RenderType`=`Transparent` `Queue`=`Transparent+1` `RenderPipeline`=`UniversalPipeline` `IgnoreProjector`=`True` }
        Pass
        {
            Name `Forward`
            Tags { `LightMode`=`UniversalForward` }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include `Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl`

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float r = IN.uv.x;
                float ang = IN.uv.y;
                float ph = frac(sin(dot(floor(IN.positionWS.xz * 0.5), float2(12.9898, 78.233))) * 43758.5453) * 6.2831;
                float pulse = 0.5 + 0.5 * sin(_Time.y * 1.6 + ph);
                float edge = smoothstep(0.0, 0.3, r) * (1.0 - smoothstep(0.35 + 0.4 * pulse, 1.0, r));
                float dash = 0.65 + 0.35 * sin(ang * 6.2831 * 9.0 + _Time.y * 0.8 + ph);
                float a = edge * dash * 0.75 * _Color.a;
                return half4(_Color.rgb, a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
";
}
#endif
