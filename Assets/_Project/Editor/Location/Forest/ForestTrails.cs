#if UNITY_EDITOR
// Assets/_Project/Editor/Location/Forest/ForestTrails.cs (Editor)
// Тропы леса, часть 2: сборка в сцене. Кнопки: Tools → Forest → Лес → «Тропы».
// Видимые тропы и поляны красятся в Terrain тем же способом, что тропы острова (LocationGen.ForestPaint.cs): единый цвет, кромка, колеи.
// Объекты ground_pathOpen_* остаются в сцене НЕВИДИМЫМИ (renderer выключен): по ним ForestPlacementFilter вырезает лес.
// Если Terrain перекрасить не вышло, ленты включаются и рисуются поверх земли прозрачным материалом, как раньше.
//   Build()          план (ForestTrailPlanner) -> меши троп, поляны, обочины, маски, маркеры
//   BuildAndForest() то же + пересборка деревьев под новые маски
//   Remove()         убирает корень ForestTrails (маркеры ForestSawmill / ForestFindSpot остаются)
//   Shots()          скриншоты: сверху + игровая камера от арки + игровая камера у лесопилки
//
// Что создаётся в сцене:
//   ForestTrails/                       корень
//     ground_pathOpen_Main, _Branch_*   ленты троп (ForestPlacementFilter ищет по этому имени). ЛЕНТЫ БЕЗ ДЕТЕЙ
//     ground_pathOpen_Glade_*           диски поляны лесопилки и тупиковой поляны
//     Decor/                            камни, цветы, корни (НЕ под ground_pathOpen*, иначе фильтр сочтёт их тропой)
//     Masks/ForestMask_*                Clearing вокруг находки, Thicket позади неё и позади лесопилки
//   ForestSawmill, ForestFindSpot       маркеры в корне сцены, создаются один раз; их можно двигать руками
// Меши лежат в сцене (не ассеты). Материалы и текстуры: Assets/_Game/ForestTrails (папку Location Builder не трогает).
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using static EditorAssets;

public static class ForestTrails
{
    public const string RootName = "ForestTrails";
    const string AssetDir = "Assets/_Game/ForestTrails";
    const string PathPrefix = "ground_pathOpen_";

    // ------------------------------------------------------------ вход
    public static string BuildAndForest(ForestSettings fs)
    {
        string r = Build(fs);
        if (!r.StartsWith("Тропы собраны")) return r;
        if (!fs.useDensityMap) r += "\nuseDensityMap выключен: лес пересобран старым режимом рощиц, маски не учтены.";
        return r + "\n" + fs.Generate(null);
    }

    public static string Remove()
    {
        if (Application.isPlaying) return "Во время Play тропы не меняются: выйди из Play.";
        bool had = RemoveRoot();
        string pm;
        bool unpainted = LocationBuilder.PaintForestTrails(null, out pm);
        string tail = unpainted ? " С Terrain тропы леса тоже убраны." : " Terrain не тронут (" + pm + ").";
        return (had ? "Тропы убраны (маркеры ForestSawmill и ForestFindSpot остались)." : "Тропы в сцене не найдены.") + tail;
    }

    static bool RemoveRoot()
    {
        bool had = false;
        GameObject r;
        while ((r = GameObject.Find(RootName)) != null) { Undo.DestroyObjectImmediate(r); had = true; }
        return had;
    }

    public static string Build(ForestSettings fs)
    {
        if (Application.isPlaying) return "Во время Play тропы не меняются: выйди из Play.";
        string err;
        var plan = ForestTrailPlanner.Plan(fs, out err);
        if (plan == null) return "Тропы: " + err;

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Build forest trails");
        int group = Undo.GetCurrentGroup();

        var master = ForestZones.Find(ForestBiome.Forest);
        float y0 = master != null ? master.transform.position.y : 0f;
        RemoveRoot();

        // высота земли: ForestPlacementFilter (Ground_Auto / Terrain), иначе высота зоны леса
        var f0 = ForestPlacementFilter.Get();
        bool f0ok = f0.Rebuild();
        System.Func<float, float, float> gy = (x, z) =>
        {
            Vector3 g;
            if (f0ok && f0.GroundPoint(x, z, out g)) return g.y;
            return y0;
        };

        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Forest trails");

        // тропы и поляны красятся в Terrain (как тропы острова); ленты тогда только для фильтра деревьев и не рисуются
        string paintMsg;
        bool painted = LocationBuilder.PaintForestTrails(plan, out paintMsg);

        // материалы и текстуры нужны только если Terrain не перекрасился
        EnsureFolder(AssetDir + "/Materials");
        Material trailMat = null, gladeMat = null;
        if (!painted)
        {
            EnsureFolder(AssetDir + "/Textures");
            trailMat = TrailMat("Mat_ForestTrail", DirtTex("Tex_ForestTrail", false, fs.forestTrailSoftEdge), true);
            gladeMat = TrailMat("Mat_ForestGlade", DirtTex("Tex_ForestGlade", true, fs.forestTrailSoftEdge), false);
        }

        // ленты троп и диски полян
        float total = 0f;
        for (int k = 0; k < plan.paths.Count; k++)
        {
            var t = plan.paths[k];
            total += t.len;
            var go = new GameObject(PathPrefix + t.name);
            go.transform.SetParent(root.transform, false);
            var mesh = Ribbon(t, gy, 0.05f);
            mesh.name = go.name + "_mesh";
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            Setup(go.AddComponent<MeshRenderer>(), trailMat, !painted);

            if (t.glade)
            {
                var dg = new GameObject(PathPrefix + "Glade_" + (t.main ? "Sawmill" : t.name));
                dg.transform.SetParent(root.transform, false);
                var dm = Disc(t.end, t.endRadius * 1.25f, gy, 0.03f);
                dm.name = dg.name + "_mesh";
                dg.AddComponent<MeshFilter>().sharedMesh = dm;
                Setup(dg.AddComponent<MeshRenderer>(), gladeMat, !painted);
            }
        }

        // маркеры лесопилки и находки: создаются один раз, дальше их позиция принадлежит пользователю
        var rep = new StringBuilder();
        if (!plan.sawmillExisting)
        {
            var sg = new GameObject(ForestTrailPlanner.SawmillName);
            Undo.RegisterCreatedObjectUndo(sg, "Forest sawmill marker");
            sg.transform.position = new Vector3(plan.sawmill.x, gy(plan.sawmill.x, plan.sawmill.y), plan.sawmill.y);
            sg.transform.localScale = new Vector3(plan.sawmillRadius * 2f, 1f, plan.sawmillRadius * 2f);
            rep.Append("\nСоздан маркер ForestSawmill (двигай его, затем «Тропы» ещё раз: тропа придёт к новому месту).");
        }
        if (plan.hasFind && !plan.findExisting)
        {
            var fg = new GameObject(ForestTrailPlanner.FindSpotName);
            Undo.RegisterCreatedObjectUndo(fg, "Forest find spot marker");
            fg.transform.position = new Vector3(plan.findSpot.x, gy(plan.findSpot.x, plan.findSpot.y), plan.findSpot.y);
            fg.transform.localScale = new Vector3(plan.findRadius * 2f, 1f, plan.findRadius * 2f);
            rep.Append("\nСоздан маркер ForestFindSpot (место для находки / NPC).");
        }

        // маска растеризуется заново: теперь в ней новые тропы (нужна для проверки обочин)
        var f1 = ForestPlacementFilter.Get();
        f1.Rebuild();

        var decorRoot = new GameObject("Decor");
        decorRoot.transform.SetParent(root.transform, false);
        string decorRep = BuildDecor(plan, fs, f1, gy, decorRoot.transform);

        var masksRoot = new GameObject("Masks");
        masksRoot.transform.SetParent(root.transform, false);
        int masks = BuildMasks(plan, master, y0, gy, masksRoot.transform);

        EditorSceneManager.MarkSceneDirty(root.scene);
        Undo.CollapseUndoOperations(group);
        Selection.activeGameObject = root;

        var sb = new StringBuilder();
        sb.Append("Тропы собраны: ").Append(plan.paths.Count).Append(" шт., всего ").Append(total.ToString("0")).Append(" м.");
        for (int k = 0; k < plan.paths.Count; k++)
            sb.Append("\n  ").Append(plan.paths[k].name).Append(": ").Append(plan.paths[k].len.ToString("0.#")).Append(" м");
        sb.Append("\nПоляна лесопилки R=").Append(plan.sawmillRadius.ToString("0.#")).Append(plan.sawmillExisting ? " (по объекту)" : "");
        if (plan.hasFind) sb.Append(", тупиковая поляна R=").Append(plan.findRadius.ToString("0.#")).Append(plan.findExisting ? " (по объекту)" : "");
        sb.Append("\n").Append(decorRep);
        sb.Append(painted ? "\nЗемля: " + paintMsg + "." : "\n! Terrain не перекрашен (" + paintMsg + "): тропы нарисованы лентами поверх земли.");
        sb.Append("\nМасок леса: ").Append(masks);
        if (!fs.useSceneMasks) sb.Append(" (useSceneMasks выключен: включи, иначе маски не сработают)");
        if (!f0ok) sb.Append("\nЗемля (Ground_Auto / Terrain) не найдена: тропы лежат на высоте зоны леса.");
        sb.Append(rep);
        for (int i = 0; i < plan.warnings.Count; i++) sb.Append("\n! ").Append(plan.warnings[i]);
        sb.Append("\nДальше: «Тропы + пересобрать лес».");
        return sb.ToString();
    }

    // visible = false: рендерер выключен, но остаётся в сцене (фильтр леса требует Renderer у объекта тропы)
    static void Setup(MeshRenderer mr, Material m, bool visible)
    {
        if (m != null) mr.sharedMaterial = m;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = true;
        mr.enabled = visible;
    }

    // ------------------------------------------------------------ скриншоты
    public static string Shots(ForestSettings fs)
    {
        var gate = GameObject.Find("ForestGate");
        var saw = GameObject.Find(ForestTrailPlanner.SawmillName);
        var sb = new StringBuilder(ForestShots.Top(fs));
        if (gate == null || saw == null) return sb.Append("\nИгровые кадры пропущены: нужны ForestGate и ForestSawmill.").ToString();

        Vector3 dir = saw.transform.position - gate.transform.position;
        dir.y = 0f;
        var oldTarget = fs.shotTarget; float oldYaw = fs.shotYaw;
        try
        {
            fs.shotYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;   // камера смотрит вдоль главной тропы
            fs.shotTarget = ForestShotTarget.Gate;
            sb.Append("\n").Append(ForestShots.Game(fs));
            fs.shotTarget = ForestShotTarget.Sawmill;
            sb.Append("\n").Append(ForestShots.Game(fs));
        }
        finally { fs.shotTarget = oldTarget; fs.shotYaw = oldYaw; }
        return sb.ToString();
    }

    // ------------------------------------------------------------ меши троп
    // лента вдоль пути: две вершины на сечение, мягкий край даёт альфа текстуры по U
    static Mesh Ribbon(ForestTrailPath t, System.Func<float, float, float> gy, float lift)
    {
        int n = t.p.Count;
        var vs = new Vector3[n * 2]; var ns = new Vector3[n * 2]; var uv = new Vector2[n * 2];
        float acc = 0f;
        for (int i = 0; i < n; i++)
        {
            Vector2 tan = (t.p[Mathf.Min(i + 1, n - 1)] - t.p[Mathf.Max(i - 1, 0)]).normalized;
            Vector2 nr = new Vector2(-tan.y, tan.x);
            Vector2 l = t.p[i] + nr * t.half[i], r = t.p[i] - nr * t.half[i];
            vs[i * 2] = new Vector3(l.x, gy(l.x, l.y) + lift, l.y);
            vs[i * 2 + 1] = new Vector3(r.x, gy(r.x, r.y) + lift, r.y);
            ns[i * 2] = ns[i * 2 + 1] = Vector3.up;
            uv[i * 2] = new Vector2(0f, acc / 4f); uv[i * 2 + 1] = new Vector2(1f, acc / 4f);
            if (i < n - 1) acc += (t.p[i + 1] - t.p[i]).magnitude;
        }
        var tris = new int[(n - 1) * 6];
        for (int i = 0; i < n - 1; i++)
        {
            int L = i * 2, R = i * 2 + 1, L2 = L + 2, R2 = R + 2;
            tris[i * 6] = L; tris[i * 6 + 1] = L2; tris[i * 6 + 2] = R;
            tris[i * 6 + 3] = R; tris[i * 6 + 4] = L2; tris[i * 6 + 5] = R2;
        }
        var m = new Mesh { indexFormat = IndexFormat.UInt32 };
        m.vertices = vs; m.normals = ns; m.uv = uv; m.triangles = tris;
        m.RecalculateBounds();
        return m;
    }

    // диск поляны: центр + 3 кольца, радиальная альфа текстуры даёт мягкий край
    static Mesh Disc(Vector2 c, float radius, System.Func<float, float, float> gy, float lift)
    {
        const int Seg = 28;
        float[] fr = { 0f, 0.45f, 0.8f, 1f };
        int rings = fr.Length;
        var vs = new Vector3[rings * (Seg + 1)]; var ns = new Vector3[vs.Length]; var uv = new Vector2[vs.Length];
        for (int j = 0; j < rings; j++)
            for (int k = 0; k <= Seg; k++)
            {
                float a = k / (float)Seg * Mathf.PI * 2f;
                float cx = Mathf.Cos(a) * fr[j], cz = Mathf.Sin(a) * fr[j];
                float x = c.x + cx * radius, z = c.y + cz * radius;
                int idx = j * (Seg + 1) + k;
                vs[idx] = new Vector3(x, gy(x, z) + lift, z);
                ns[idx] = Vector3.up;
                uv[idx] = new Vector2(0.5f + 0.5f * cx, 0.5f + 0.5f * cz);
            }
        var tris = new List<int>();
        for (int j = 0; j < rings - 1; j++)
            for (int k = 0; k < Seg; k++)
            {
                int a0 = j * (Seg + 1) + k, a1 = a0 + 1, b0 = (j + 1) * (Seg + 1) + k, b1 = b0 + 1;
                tris.Add(a0); tris.Add(a1); tris.Add(b0);
                tris.Add(b0); tris.Add(a1); tris.Add(b1);
            }
        var m = new Mesh { indexFormat = IndexFormat.UInt32 };
        m.vertices = vs; m.normals = ns; m.uv = uv; m.triangles = tris.ToArray();
        m.RecalculateBounds();
        return m;
    }

    // ------------------------------------------------------------ материалы и текстуры

    static float Sm(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / Mathf.Max(1e-4f, b - a));
        return t * t * (3f - 2f * t);
    }

    static Texture2D DirtTex(string name, bool radial, float soft)
    {
        string path = AssetDir + "/Textures/" + name + ".asset";
        int w = 64, h = radial ? 64 : 128;
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex == null || tex.width != w || tex.height != h)
        {
            if (tex != null) AssetDatabase.DeleteAsset(path);
            tex = new Texture2D(w, h, TextureFormat.RGBA32, true, false) { name = name };
            AssetDatabase.CreateAsset(tex, path);
        }
        Color center = new Color(0.66f, 0.54f, 0.37f), edge = new Color(0.43f, 0.40f, 0.24f);
        float fade = radial ? 0.38f : Mathf.Clamp(soft * 1.4f, 0.08f, 0.9f);
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                float e = radial ? 1f - Mathf.Sqrt((u * 2f - 1f) * (u * 2f - 1f) + (v * 2f - 1f) * (v * 2f - 1f)) : Mathf.Min(u, 1f - u) * 2f;
                float a = Sm(0f, fade, e);
                Color c = Color.Lerp(edge, center, Sm(0f, 0.85f, e));
                float nz = Mathf.PerlinNoise(x * 0.35f + 7.3f, y * 0.35f + 2.1f) - 0.5f;   // пятна и крошка
                float sp = Mathf.PerlinNoise(x * 1.7f + 31f, y * 1.7f + 5f) > 0.78f ? -0.07f : 0f;
                float k = 1f + nz * 0.16f + sp;
                px[y * w + x] = new Color(c.r * k, c.g * k, c.b * k, a);
            }
        tex.SetPixels(px);
        tex.wrapModeU = TextureWrapMode.Clamp;
        tex.wrapModeV = radial ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;
        tex.Apply(true);
        EditorUtility.SetDirty(tex);
        return tex;
    }

    static Shader LitShader()
    {
        var sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) throw new System.Exception("Не найден шейдер 'Universal Render Pipeline/Lit'. Проект должен быть на URP.");
        return sh;
    }

    static Material LoadOrCreate(string name)
    {
        string path = AssetDir + "/Materials/" + name + ".mat";
        var sh = LitShader();
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(sh) { name = name }; AssetDatabase.CreateAsset(m, path); }
        else m.shader = sh;
        return m;
    }

    static Material TrailMat(string name, Texture2D tex, bool ribbon)
    {
        var m = LoadOrCreate(name);
        m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Smoothness", 0f);
        m.SetFloat("_Metallic", 0f);
        // прозрачность URP Lit: мягкие края через альфу текстуры
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetShaderPassEnabled("ShadowCaster", false);
        m.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material SolidMat(string name, Color c)
    {
        var m = LoadOrCreate(name);
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", 0.1f);
        m.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(m);
        return m;
    }

    // ------------------------------------------------------------ обочины: сборка мешей
    sealed class MB
    {
        public readonly List<Vector3> v = new List<Vector3>();
        public readonly List<Vector3> n = new List<Vector3>();
        public readonly List<int> t = new List<int>();

        public void Tri(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 nn = Vector3.Cross(b - a, c - a).normalized;
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c); n.Add(nn); n.Add(nn); n.Add(nn);
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
        }
        // тонкая деталь видна с обеих сторон
        public void Tri2(Vector3 a, Vector3 b, Vector3 c) { Tri(a, b, c); Tri(a, c, b); }
        public void TriOut(Vector3 a, Vector3 b, Vector3 c, Vector3 center)
        {
            Vector3 nn = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(nn, (a + b + c) / 3f - center) < 0f) Tri(a, c, b); else Tri(a, b, c);
        }
        public bool Empty { get { return v.Count == 0; } }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            m.SetVertices(v); m.SetNormals(n); m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }
    }

    static readonly Vector3[] IcoV =
    {
        new Vector3(-1, 1.618f, 0), new Vector3(1, 1.618f, 0), new Vector3(-1, -1.618f, 0), new Vector3(1, -1.618f, 0),
        new Vector3(0, -1, 1.618f), new Vector3(0, 1, 1.618f), new Vector3(0, -1, -1.618f), new Vector3(0, 1, -1.618f),
        new Vector3(1.618f, 0, -1), new Vector3(1.618f, 0, 1), new Vector3(-1.618f, 0, -1), new Vector3(-1.618f, 0, 1)
    };
    static readonly int[] IcoF =
    {
        0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
        3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
    };

    // камень: искажённый икосаэдр, на треть утоплен в землю
    static void AddRock(MB mb, Vector3 pos, float size, System.Random rng)
    {
        var pv = new Vector3[12];
        float sx = 1f + (float)rng.NextDouble() * 0.5f, sz = 1f + (float)rng.NextDouble() * 0.5f, sy = 0.5f + (float)rng.NextDouble() * 0.3f;
        for (int i = 0; i < 12; i++)
        {
            float j = 0.78f + (float)rng.NextDouble() * 0.44f;
            Vector3 d = IcoV[i].normalized * j;
            pv[i] = new Vector3(d.x * sx, d.y * sy, d.z * sz) * size;
        }
        float yaw = (float)rng.NextDouble() * 360f;
        Quaternion q = Quaternion.Euler(0f, yaw, 0f);
        Vector3 c = pos + Vector3.up * size * sy * 0.15f;
        for (int i = 0; i < 12; i++) pv[i] = c + q * pv[i];
        for (int f = 0; f < IcoF.Length; f += 3) mb.TriOut(pv[IcoF[f]], pv[IcoF[f + 1]], pv[IcoF[f + 2]], c);
    }

    static void AddFlower(MB stem, MB head, MB centre, Vector3 b, float h, float hr, System.Random rng)
    {
        Vector3 top = b + new Vector3(((float)rng.NextDouble() - 0.5f) * 0.05f, h, ((float)rng.NextDouble() - 0.5f) * 0.05f);
        const float w = 0.012f;
        Vector3 s1 = new Vector3(w, 0f, 0f), s2 = new Vector3(0f, 0f, w);
        stem.Tri2(b - s1, b + s1, top + s1); stem.Tri2(b - s1, top + s1, top - s1);
        stem.Tri2(b - s2, b + s2, top + s2); stem.Tri2(b - s2, top + s2, top - s2);
        // два листика
        float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
        for (int k = 0; k < 2; k++)
        {
            Vector3 o = new Vector3(Mathf.Cos(ang + k * Mathf.PI), 0f, Mathf.Sin(ang + k * Mathf.PI));
            Vector3 sd = new Vector3(-o.z, 0f, o.x) * 0.02f;
            Vector3 m = b + (top - b) * (0.3f + 0.15f * k);
            Vector3 tip = m + o * 0.09f + Vector3.up * 0.035f;
            stem.Tri2(m, tip + sd, tip - sd);
        }
        Vector3 c = top + Vector3.up * 0.012f;
        for (int k = 0; k < 6; k++)
        {
            float a0 = k / 6f * Mathf.PI * 2f, a1 = (k + 1) / 6f * Mathf.PI * 2f;
            Vector3 p0 = c + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * hr, p1 = c + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * hr;
            head.Tri2(c, p1, p0);
        }
        if (centre != null)
        {
            Vector3 cc = c + Vector3.up * 0.006f;
            for (int k = 0; k < 4; k++)
            {
                float a0 = k / 4f * Mathf.PI * 2f, a1 = (k + 1) / 4f * Mathf.PI * 2f;
                centre.Tri2(cc, cc + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * hr * 0.4f, cc + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * hr * 0.4f);
            }
        }
    }

    // корень: трубка вдоль точек, сужается к концу, концы в земле
    static void AddTube(MB mb, List<Vector3> pts, float r0, float r1, int sides)
    {
        int m = pts.Count;
        var ring = new Vector3[m, sides];
        var cen = new Vector3[m];
        for (int i = 0; i < m; i++)
        {
            Vector3 tan = (pts[Mathf.Min(i + 1, m - 1)] - pts[Mathf.Max(i - 1, 0)]).normalized;
            Vector3 side = Vector3.Cross(tan, Vector3.up);
            if (side.sqrMagnitude < 1e-4f) side = Vector3.right;
            side.Normalize();
            Vector3 up = Vector3.Cross(side, tan).normalized;
            float r = Mathf.Lerp(r0, r1, i / (float)Mathf.Max(1, m - 1));
            cen[i] = pts[i];
            for (int k = 0; k < sides; k++)
            {
                float a = k / (float)sides * Mathf.PI * 2f;
                ring[i, k] = pts[i] + (side * Mathf.Cos(a) + up * Mathf.Sin(a)) * r;
            }
        }
        for (int i = 0; i < m - 1; i++)
            for (int k = 0; k < sides; k++)
            {
                int k2 = (k + 1) % sides;
                Vector3 a = ring[i, k], b = ring[i + 1, k], c = ring[i, k2], d = ring[i + 1, k2];
                mb.TriOut(a, b, c, cen[i]);
                mb.TriOut(c, b, d, cen[i]);
            }
    }

    // ------------------------------------------------------------ обочины: расстановка
    sealed class PS { public ForestTrailPath t; public float[] cum; }

    static void At(PS ps, float s, out Vector2 pos, out Vector2 nrm, out float half)
    {
        var t = ps.t; int n = t.p.Count; int i = 0;
        while (i < n - 2 && ps.cum[i + 1] < s) i++;
        float seg = Mathf.Max(1e-4f, ps.cum[i + 1] - ps.cum[i]);
        float u = Mathf.Clamp01((s - ps.cum[i]) / seg);
        pos = Vector2.Lerp(t.p[i], t.p[i + 1], u);
        Vector2 tan = (t.p[i + 1] - t.p[i]).normalized;
        nrm = new Vector2(-tan.y, tan.x);
        half = Mathf.Lerp(t.half[i], t.half[i + 1], u);
    }

    static string BuildDecor(ForestTrailPlan plan, ForestSettings fs, ForestPlacementFilter f, System.Func<float, float, float> gy, Transform parent)
    {
        float k = fs.forestTrailDecor;
        if (k <= 0.001f) return "Обочины: выключены (forestTrailDecor = 0).";
        var rng = new System.Random(unchecked(fs.seed * 17 + fs.forestTrailVariant * 31 + 5));
        var rockA = new MB(); var rockB = new MB(); var stem = new MB(); var fW = new MB(); var fY = new MB(); var fP = new MB(); var root = new MB();
        int rocks = 0, flowers = 0, roots = 0;
        System.Func<double> R = () => rng.NextDouble();

        System.Func<Vector2, bool> blocked = p =>
        {
            if (Vector2.Distance(p, plan.sawmill) < plan.sawmillRadius * 1.15f) return true;
            if (plan.hasFind && Vector2.Distance(p, plan.findSpot) < plan.findRadius * 1.1f) return true;
            return f.PathDistance(new Vector3(p.x, 0f, p.y)) < 0.08f;   // не на другой тропе
        };
        System.Func<Vector2, Vector3> g3 = p => new Vector3(p.x, gy(p.x, p.y), p.y);

        System.Action<Vector2, float> PutRock = (p, size) =>
        {
            AddRock(R() < 0.65 ? rockA : rockB, g3(p), size, rng);
            rocks++;
        };
        System.Action<Vector2, int> PutFlowers = (p, count) =>
        {
            int col = (int)(R() * 3.0);
            for (int i = 0; i < count; i++)
            {
                float a = (float)R() * Mathf.PI * 2f, d = (float)R() * 0.45f;
                Vector2 q = p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
                if (blocked(q)) continue;
                MB head = col == 0 ? fW : (col == 1 ? fY : fP);
                AddFlower(stem, head, col == 1 ? null : fY, g3(q), 0.16f + (float)R() * 0.18f, 0.05f + (float)R() * 0.03f, rng);
                flowers++;
            }
        };

        for (int pi = 0; pi < plan.paths.Count; pi++)
        {
            var path = plan.paths[pi];
            int n = path.p.Count;
            if (n < 4) continue;
            var ps = new PS { t = path, cum = new float[n] };
            for (int i = 1; i < n; i++) ps.cum[i] = ps.cum[i - 1] + (path.p[i] - path.p[i - 1]).magnitude;
            float len = ps.cum[n - 1];

            if (fs.forestTrailRocks)
            {
                float s = 2.5f;
                while (s < len - 1f)
                {
                    Vector2 pos, nr; float half;
                    At(ps, s, out pos, out nr, out half);
                    float side = R() < 0.5 ? 1f : -1f;
                    Vector2 c = pos + nr * side * (half + 0.1f + (float)R() * 0.8f);
                    if (!blocked(c))
                    {
                        int cnt = R() < 0.35 ? 3 : (R() < 0.5 ? 2 : 1);
                        for (int q = 0; q < cnt; q++)
                        {
                            Vector2 cq = c + new Vector2((float)R() - 0.5f, (float)R() - 0.5f) * 0.5f * (cnt - 1);
                            if (blocked(cq)) continue;
                            float size = q == 0 ? 0.2f + (float)R() * 0.28f : 0.1f + (float)R() * 0.14f;
                            PutRock(cq, size);
                        }
                    }
                    s += (2.4f + (float)R() * 2.4f) / k;
                }
            }

            if (fs.forestTrailFlowers)
            {
                float s = 3f;
                while (s < len - 1f)
                {
                    Vector2 pos, nr; float half;
                    At(ps, s, out pos, out nr, out half);
                    float side = R() < 0.5 ? 1f : -1f;
                    Vector2 c = pos + nr * side * (half + 0.4f + (float)R() * 1.2f);
                    if (!blocked(c)) PutFlowers(c, 3 + (int)(R() * 5.0));
                    s += (3.5f + (float)R() * 3.5f) / k;
                }
            }

            if (fs.forestTrailRoots)
            {
                float s = 5.5f;
                while (s < len - 3f)
                {
                    Vector2 pos, nr; float half;
                    At(ps, s, out pos, out nr, out half);
                    if (!blocked(pos))
                    {
                        float side = R() < 0.5 ? 1f : -1f;
                        float skew = ((float)R() - 0.5f) * 0.9f;                      // корень идёт не строго поперёк
                        Vector2 tan = new Vector2(nr.y, -nr.x);
                        Vector2 a = pos + nr * side * (half + 1.5f + (float)R() * 0.6f) + tan * skew * 1.0f;
                        Vector2 b = pos - nr * side * (half * 0.35f + 0.1f + (float)R() * 0.4f) - tan * skew * 0.6f;
                        const int Steps = 9;
                        var pts = new List<Vector3>();
                        for (int i = 0; i < Steps; i++)
                        {
                            float u = i / (float)(Steps - 1);
                            Vector2 p2 = Vector2.Lerp(a, b, u);
                            float arch = Mathf.Pow(Mathf.Sin(Mathf.PI * u), 0.8f) * (0.13f + (float)R() * 0.03f) - 0.07f;
                            pts.Add(new Vector3(p2.x, gy(p2.x, p2.y) + arch, p2.y));
                        }
                        float rr = 0.07f + (float)R() * 0.06f;
                        AddTube(root, pts, rr * 1.5f, rr * 0.7f, 6);
                        roots++;
                    }
                    s += (6.5f + (float)R() * 4.5f) / k;
                }
            }
        }

        // тупиковая поляна: кольцо камней и цветов с просветом к тропе
        if (plan.hasFind)
        {
            var fp = plan.paths.Find(x => x.name == "Branch_Find");
            Vector2 toFork = fp != null && fp.p.Count > 3 ? (fp.p[fp.p.Count - 4] - plan.findSpot).normalized : Vector2.up;
            float baseA = Mathf.Atan2(toFork.y, toFork.x);
            int ringN = 9;
            for (int i = 0; i < ringN; i++)
            {
                float a = baseA + (0.5f + i) / ringN * Mathf.PI * 2f;
                if (Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, baseA * Mathf.Rad2Deg)) < 32f) continue;   // вход на поляну
                Vector2 c = plan.findSpot + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * plan.findRadius * (1.0f + (float)R() * 0.2f);
                if (f.PathDistance(new Vector3(c.x, 0f, c.y)) < 0.08f && false) continue;
                if (fs.forestTrailRocks) PutRock(c, i % 4 == 0 ? 0.55f + (float)R() * 0.2f : 0.2f + (float)R() * 0.2f);
                if (fs.forestTrailFlowers && i % 2 == 1) PutFlowers(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.6f, 4 + (int)(R() * 3.0));
            }
        }

        EnsureFolder(AssetDir + "/Materials");
        Emit(parent, "Rocks_A", rockA, SolidMat("Mat_TrailRock", new Color(0.55f, 0.55f, 0.52f)));
        Emit(parent, "Rocks_B", rockB, SolidMat("Mat_TrailRockMoss", new Color(0.42f, 0.52f, 0.36f)));
        Emit(parent, "Flower_Stems", stem, SolidMat("Mat_TrailStem", new Color(0.30f, 0.55f, 0.22f)));
        Emit(parent, "Flowers_White", fW, SolidMat("Mat_TrailFlowerWhite", new Color(0.96f, 0.95f, 0.90f)));
        Emit(parent, "Flowers_Yellow", fY, SolidMat("Mat_TrailFlowerYellow", new Color(0.98f, 0.82f, 0.25f)));
        Emit(parent, "Flowers_Pink", fP, SolidMat("Mat_TrailFlowerPink", new Color(0.92f, 0.45f, 0.60f)));
        Emit(parent, "Roots", root, SolidMat("Mat_TrailRoot", new Color(0.33f, 0.23f, 0.14f)));
        return "Обочины: камней " + rocks + ", цветов " + flowers + ", корней " + roots + ".";
    }

    static void Emit(Transform parent, string name, MB mb, Material mat)
    {
        if (mb.Empty) return;
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var mesh = mb.ToMesh(name + "_mesh");
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = name.StartsWith("Rocks") || name == "Roots" ? ShadowCastingMode.On : ShadowCastingMode.Off;
    }

    // ------------------------------------------------------------ маски леса и маркеры
    static GameObject Mask(Transform parent, string name, Vector2 c, float r, System.Func<float, float, float> gy)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(c.x, gy(c.x, c.y), c.y);
        go.transform.localScale = new Vector3(r * 2f, 1f, r * 2f);
        return go;
    }

    static int BuildMasks(ForestTrailPlan plan, ForestZone master, float y0, System.Func<float, float, float> gy, Transform parent)
    {
        int n = 0;
        System.Func<Vector2, float, bool> inside = (p, margin) => master == null || master.Depth(new Vector3(p.x, y0, p.y)) > margin;

        // дальше всех от арки: направление «вглубь леса»
        Vector2 deep = plan.center - plan.gate;
        deep = deep.sqrMagnitude < 1e-4f ? Vector2.up : deep.normalized;
        Vector2 left = new Vector2(-deep.y, deep.x);

        // тупиковая поляна: открытое место, а за ней и по бокам чаща (поляна прячется в лесу)
        if (plan.hasFind)
        {
            Mask(parent, "ForestMask_Clearing_Find", plan.findSpot, plan.findRadius + 2f, gy); n++;
            var fp = plan.paths.Find(x => x.name == "Branch_Find");
            Vector2 away = fp != null && fp.p.Count > 3 ? (plan.findSpot - fp.p[fp.p.Count - 4]).normalized : deep;
            Vector2 side = new Vector2(-away.y, away.x);
            float tr = plan.findRadius + 3f;
            Vector2[] pos = { plan.findSpot + away * (plan.findRadius + 4f), plan.findSpot + away * (plan.findRadius * 0.6f) + side * (plan.findRadius * 1.9f), plan.findSpot + away * (plan.findRadius * 0.6f) - side * (plan.findRadius * 1.9f) };
            for (int i = 0; i < pos.Length; i++)
                if (inside(pos[i], 3f)) { Mask(parent, "ForestMask_Thicket_Find" + i, pos[i], tr, gy); n++; }
        }

        // лесопилка: чаща позади и по бокам (сам вырез делает объект ForestSawmill)
        float sr = plan.sawmillRadius;
        Vector2[] sp = { plan.sawmill + deep * (sr + 4f), plan.sawmill + deep * (sr * 0.3f) + left * (sr * 1.5f), plan.sawmill + deep * (sr * 0.3f) - left * (sr * 1.5f) };
        for (int i = 0; i < sp.Length; i++)
            if (inside(sp[i], 3f)) { Mask(parent, "ForestMask_Thicket_Saw" + i, sp[i], sr * 0.75f + 1f, gy); n++; }
        return n;
    }
}

// ------------------------------------------------------------ подсказки в Scene View: лесопилка и находка
[InitializeOnLoad]
static class ForestTrailHints
{
    static Transform saw, find;
    static ForestSettings settings;
    static bool resolved;

    static ForestTrailHints()
    {
        SceneView.duringSceneGui += Draw;
        EditorApplication.hierarchyChanged += () => resolved = false;
    }

    static void Resolve()
    {
        var s = GameObject.Find(ForestTrailPlanner.SawmillName);
        var f = GameObject.Find(ForestTrailPlanner.FindSpotName);
        saw = s != null ? s.transform : null;
        find = f != null ? f.transform : null;
        settings = AssetDatabase.LoadAssetAtPath<ForestSettings>(ForestSettings.AssetPath);
        resolved = true;
    }

    static void Draw(SceneView sv)
    {
        if (Application.isPlaying) return;
        if (!resolved) Resolve();
        if (settings != null && !settings.drawGizmos) return;
        if (saw != null)
        {
            Handles.color = new Color(1f, 0.8f, 0.2f);
            Handles.DrawWireDisc(saw.position, Vector3.up, Mathf.Max(saw.lossyScale.x, saw.lossyScale.z) * 0.5f);
            Handles.Label(saw.position + Vector3.up * 1.5f, "Лесопилка: двигай и жми «Тропы»");
        }
        if (find != null)
        {
            Handles.color = new Color(0.4f, 1f, 0.5f);
            Handles.DrawWireDisc(find.position, Vector3.up, Mathf.Max(find.lossyScale.x, find.lossyScale.z) * 0.5f);
            Handles.Label(find.position + Vector3.up * 1.5f, "Находка / NPC (тупиковая поляна)");
        }
    }
}
#endif
