// Assets/_Project/Editor/Buildings/TentBuilder.cs
// Tools > BuilderSim > Tent Builder
// Процедурная палатка в 2 состояниях (1 = стройплощадка, 2 = готова). Unity 6 (6000.3), URP.
// Использует класс HutMesh из HutBuilder.cs (плоское затенение, low-poly).
// Дверь палатки смотрит в локальную +Z. Если на сцене есть PAD_Tent (Location Builder),
// палатка встаёт на него и дверью смотрит в сторону центра/тропы.
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using static EditorAssets;

public class TentBuilder : EditorWindow
{
    const string Dir = "Assets/_Game/Tent";
    const string PrefKey = "BuilderSim.TentBuilder.v1";
    const string RootName = "CampTent";

    [System.Serializable]
    public class Settings
    {
        public float width = 2.4f, length = 2.9f, height = 2.2f, wallHeight = 0.5f, doorWidth = 1.0f;
        public int seed = 777;
        public Color canvasA = new Color(0.94f, 0.88f, 0.72f);
        public Color canvasB = new Color(0.86f, 0.36f, 0.28f);
        public Color accentA = new Color(0.22f, 0.55f, 0.78f);
        public Color accentB = new Color(0.98f, 0.78f, 0.25f);
        public Color woodLight = new Color(0.66f, 0.44f, 0.24f);
        public Color woodDark = new Color(0.40f, 0.26f, 0.14f);
        // состояние 1
        public bool decSite = true;
        // состояние 2
        public bool decRopes = true, decBunting = true, decValance = true, decFlag = true,
                    decInterior = true, decLanternOut = true, decLanternIn = true,
                    decProps = true, decFireflies = true, decLights = true;
        public bool followPad = true;
        public Vector3 position;
        public float yaw;
    }

    enum M { CanvasA, CanvasB, WoodL, WoodD, Rope, Stone, Metal, Lamp, AccentA, AccentB, Rug, Fabric, Floor, Dust, Leather }

    class Ctx
    {
        public Transform root;
        public string tag;
        public Transform colRoot;
        public System.Random rng;
        public Dictionary<M, HutMesh> mbs = new Dictionary<M, HutMesh>();
        public HutMesh Get(M id)
        {
            HutMesh m;
            if (!mbs.TryGetValue(id, out m)) { m = new HutMesh(rng); mbs[id] = m; }
            return m;
        }
    }

    Settings S = new Settings();
    Vector2 scroll;

    System.Random rng;
    Transform root;
    readonly Dictionary<M, Material> matCache = new Dictionary<M, Material>();
    Mesh puffMesh;
    readonly List<Light> lights = new List<Light>();
    readonly List<Transform> swingers = new List<Transform>();
    readonly List<Transform> curtains = new List<Transform>();
    readonly List<Transform> flagSegs = new List<Transform>();
    ParticleSystem puffPS;

    // размеры
    float W, L, H, wh, hw, slopeLen, slopeAng, dw, strL;
    int nStr;

    [MenuItem("Tools/BuilderSim/Tent Builder")]
    static void Open()
    {
        var w = GetWindow<TentBuilder>(false, "Палатка");
        w.minSize = new Vector2(380, 520);
        w.Show();
    }

    // ===================================================================== prefs
    void OnEnable()
    {
        if (EditorPrefs.HasKey(PrefKey)) JsonUtility.FromJsonOverwrite(EditorPrefs.GetString(PrefKey), S);
        SceneView.duringSceneGui += OnSceneGUI;
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        Save();
    }

    void Save() { EditorPrefs.SetString(PrefKey, JsonUtility.ToJson(S)); }

    // ===================================================================== UI
    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("Палатка", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();

        EditorGUILayout.LabelField("Размеры", EditorStyles.boldLabel);
        S.width = EditorGUILayout.Slider("Ширина (м)", S.width, 1.6f, 5f);
        S.length = EditorGUILayout.Slider("Длина (м)", S.length, 2f, 6f);
        S.height = EditorGUILayout.Slider("Высота конька (м)", S.height, 1.6f, 3.5f);
        S.wallHeight = EditorGUILayout.Slider("Высота стенки (м)", S.wallHeight, 0.2f, 1.2f);
        S.doorWidth = EditorGUILayout.Slider("Ширина входа (м)", S.doorWidth, 0.7f, 1.6f);
        S.seed = EditorGUILayout.IntField("Seed", S.seed);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Цвета", EditorStyles.boldLabel);
        S.canvasA = EditorGUILayout.ColorField("Полотно A", S.canvasA);
        S.canvasB = EditorGUILayout.ColorField("Полотно B (полосы)", S.canvasB);
        S.accentA = EditorGUILayout.ColorField("Акцент A", S.accentA);
        S.accentB = EditorGUILayout.ColorField("Акцент B", S.accentB);
        S.woodLight = EditorGUILayout.ColorField("Светлое дерево", S.woodLight);
        S.woodDark = EditorGUILayout.ColorField("Тёмное дерево", S.woodDark);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Состояние 1: стройплощадка", EditorStyles.boldLabel);
        S.decSite = EditorGUILayout.Toggle("Каркас, колышки, рулон ткани", S.decSite);
        EditorGUILayout.LabelField("Состояние 2: готово", EditorStyles.boldLabel);
        S.decValance = EditorGUILayout.Toggle("Зубчатая кромка", S.decValance);
        S.decRopes = EditorGUILayout.Toggle("Растяжки и колышки", S.decRopes);
        S.decBunting = EditorGUILayout.Toggle("Гирлянда флажков", S.decBunting);
        S.decFlag = EditorGUILayout.Toggle("Флаг на мачте (анимация)", S.decFlag);
        S.decInterior = EditorGUILayout.Toggle("Интерьер (спальник, ящик, рюкзак)", S.decInterior);
        S.decLanternIn = EditorGUILayout.Toggle("Фонарь внутри (свет, качается)", S.decLanternIn);
        S.decLanternOut = EditorGUILayout.Toggle("Фонарь у входа (свет, качается)", S.decLanternOut);
        S.decProps = EditorGUILayout.Toggle("Бочка, ящики, пенёк снаружи", S.decProps);
        S.decFireflies = EditorGUILayout.Toggle("Светлячки у фонаря", S.decFireflies);
        S.decLights = EditorGUILayout.Toggle("Реальные источники света", S.decLights);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Положение", EditorStyles.boldLabel);
        S.followPad = EditorGUILayout.Toggle("Вставать на PAD_Tent при сборке", S.followPad);
        S.position = EditorGUILayout.Vector3Field("Позиция", S.position);
        S.yaw = EditorGUILayout.FloatField("Поворот Y (°)", S.yaw);
        if (GUILayout.Button("Взять позицию с PAD_Tent"))
        {
            if (SnapToPad()) MoveExisting();
            else Debug.LogWarning("TentBuilder: объект PAD_Tent не найден на сцене (сначала собери локацию).");
        }

        if (EditorGUI.EndChangeCheck())
        {
            Save();
            MoveExisting();
            SceneView.RepaintAll();
        }

        EditorGUILayout.Space(8);
        if (GUILayout.Button("Построить палатку", GUILayout.Height(32))) EditorApplication.delayCall += DoBuild;
        if (GUILayout.Button("Удалить палатку", GUILayout.Height(24))) EditorApplication.delayCall += DoDelete;

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Предпросмотр состояния", EditorStyles.miniBoldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("1 Стройплощадка")) ShowStage(1);
        if (GUILayout.Button("2 Готова")) ShowStage(2);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndScrollView();
    }

    void DoBuild() { if (this != null) Build(); }
    void DoDelete() { if (this != null) { var t = FindTent(); if (t != null) Undo.DestroyObjectImmediate(t); } }

    void ShowStage(int stage)
    {
        var t = FindTent();
        if (t == null) return;
        var st = t.GetComponent<TentStages>();
        if (st != null) st.SetStage(stage, false);
    }

    bool SnapToPad()
    {
        var pad = GameObject.Find("PAD_Tent");
        if (pad == null) return false;
        S.position = pad.transform.position;
        S.yaw = pad.transform.eulerAngles.y;
        Save();
        return true;
    }

    void OnSceneGUI(SceneView sv)
    {
        Handles.BeginGUI();
        GUILayout.BeginArea(new Rect(10, 54, 190, 40));
        if (GUILayout.Button("Построить палатку", GUILayout.Height(28))) EditorApplication.delayCall += DoBuild;
        GUILayout.EndArea();
        Handles.EndGUI();

        var prev = Handles.matrix;
        Handles.matrix = Matrix4x4.TRS(S.position, Quaternion.Euler(0, S.yaw, 0), Vector3.one);
        Handles.color = new Color(0.4f, 0.9f, 1f, 0.9f);
        Handles.DrawWireCube(new Vector3(0, S.height * 0.5f, 0), new Vector3(S.width, S.height, S.length));
        Handles.DrawLine(new Vector3(0, 0.05f, S.length * 0.5f), new Vector3(0, 0.05f, S.length * 0.5f + 1.2f));
        Handles.matrix = prev;

        EditorGUI.BeginChangeCheck();
        Vector3 np = Handles.PositionHandle(S.position, Quaternion.identity);
        float size = HandleUtility.GetHandleSize(S.position);
        Quaternion nr = Handles.Disc(Quaternion.Euler(0, S.yaw, 0), S.position, Vector3.up, size * 1.2f, false, 0f);
        if (EditorGUI.EndChangeCheck())
        {
            S.position = np;
            S.yaw = nr.eulerAngles.y;
            Save();
            MoveExisting();
            Repaint();
        }
    }

    void MoveExisting()
    {
        var t = FindTent();
        if (t == null) return;
        float y;
        if (!GroundY(S.position, out y, t.transform)) y = t.transform.position.y;
        Undo.RecordObject(t.transform, "Переместить палатку");
        t.transform.SetPositionAndRotation(new Vector3(S.position.x, y, S.position.z), Quaternion.Euler(0, S.yaw, 0));
    }

    static GameObject FindTent()
    {
        foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
            if (go.name == RootName) return go;
        return null;
    }

    // ===================================================================== ground
    static bool GroundY(Vector3 wp, out float y, Transform ignore)
    {
        y = 0f;
        var hits = Physics.RaycastAll(new Vector3(wp.x, wp.y + 500f, wp.z), Vector3.down, 1500f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        bool ok = false;
        foreach (var h in hits)
        {
            if (ignore != null && h.collider.transform.IsChildOf(ignore)) continue;
            bool ground = h.collider is TerrainCollider || h.collider.gameObject.name == "Ground";
            if (!ground) continue;
            if (h.distance < best) { best = h.distance; y = h.point.y; ok = true; }
        }
        return ok;
    }

    bool AverageGround(out float y)
    {
        var q = Quaternion.Euler(0, S.yaw, 0);
        Vector3[] offs = { Vector3.zero, new Vector3(W / 2, 0, L / 2), new Vector3(-W / 2, 0, L / 2), new Vector3(W / 2, 0, -L / 2), new Vector3(-W / 2, 0, -L / 2) };
        float sum = 0f; int cnt = 0;
        foreach (var o in offs)
        {
            float gy;
            if (GroundY(S.position + q * o, out gy, null)) { sum += gy; cnt++; }
        }
        y = cnt > 0 ? sum / cnt : 0f;
        return cnt > 0;
    }

    float GY(float x, float z)
    {
        Vector3 wp = root.TransformPoint(new Vector3(x, 0, z));
        float y;
        if (GroundY(wp, out y, root)) return root.InverseTransformPoint(new Vector3(wp.x, y, wp.z)).y;
        return 0f;
    }

    // ===================================================================== utils
    float Rf(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }
    float Jf(float a) { return Rf(-a, a); }
    float RoofY(float ax) { return H - (H - wh) * Mathf.Clamp01(ax / hw); }

    Material GetMat(M id)
    {
        Material cached;
        if (matCache.TryGetValue(id, out cached)) return cached;
        Color col; float emis = 0f;
        switch (id)
        {
            case M.CanvasA: col = S.canvasA; break;
            case M.CanvasB: col = S.canvasB; break;
            case M.WoodL: col = S.woodLight; break;
            case M.WoodD: col = S.woodDark; break;
            case M.Rope: col = new Color(0.78f, 0.66f, 0.42f); break;
            case M.Stone: col = new Color(0.60f, 0.58f, 0.64f); break;
            case M.Metal: col = new Color(0.28f, 0.28f, 0.32f); break;
            case M.Lamp: col = new Color(1f, 0.85f, 0.4f); emis = 1.6f; break;
            case M.AccentA: col = S.accentA; break;
            case M.AccentB: col = S.accentB; break;
            case M.Rug: col = S.canvasB * 0.8f; col.a = 1f; break;
            case M.Fabric: col = new Color(0.38f, 0.62f, 0.42f); break;
            case M.Floor: col = new Color(0.45f, 0.36f, 0.26f); break;
            case M.Dust: col = new Color(0.92f, 0.88f, 0.80f); break;
            default: col = new Color(0.55f, 0.36f, 0.20f); break; // Leather
        }
        string path = Dir + "/Tent_" + id + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            m = new Material(sh) { name = "Tent_" + id };
            AssetDatabase.CreateAsset(m, path);
        }
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
        if (m.HasProperty("_Color")) m.SetColor("_Color", col);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.05f);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        if (emis > 0f)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", col * emis);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        matCache[id] = m;
        return m;
    }

    Mesh SaveMesh(Mesh m, string name)
    {
        string path = Dir + "/" + name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
        {
            existing.Clear();
            EditorUtility.CopySerialized(m, existing);
            DestroyImmediate(m);
            EditorUtility.SetDirty(existing);
            return existing;
        }
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    Ctx NewCtx(Transform r, string tag) { return new Ctx { root = r, tag = tag, rng = rng }; }

    void Flush(Ctx c)
    {
        foreach (var kv in c.mbs)
        {
            if (kv.Value.Count == 0) continue;
            string nm = "Tent_" + c.tag + "_" + kv.Key;
            var go = new GameObject("Mesh_" + kv.Key);
            go.transform.SetParent(c.root, false);
            go.AddComponent<MeshFilter>().sharedMesh = SaveMesh(kv.Value.ToMesh(nm), nm);
            go.AddComponent<MeshRenderer>().sharedMaterial = GetMat(kv.Key);
        }
    }

    void AddCol(Ctx c, Vector3 center, Vector3 size, Quaternion? rot = null)
    {
        if (c.colRoot == null)
        {
            var g = new GameObject("Colliders");
            g.transform.SetParent(c.root, false);
            c.colRoot = g.transform;
        }
        var go = new GameObject("Col");
        go.transform.SetParent(c.colRoot, false);
        go.transform.localPosition = center;
        go.transform.localRotation = rot ?? Quaternion.identity;
        go.AddComponent<BoxCollider>().size = size;
    }

    static Transform NewChild(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    static void Tri2(HutMesh m, Vector3 a, Vector3 b, Vector3 c) { m.Tri(a, b, c); m.Tri(a, c, b); }

    // тонкая плита по плоскому четырёхугольнику в плоскости z = const
    static void Slab(HutMesh m, Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
    {
        Vector3 o = new Vector3(0, 0, t * 0.5f);
        Vector3 ctr = (a + b + c + d) * 0.25f;
        m.Quad(a + o, b + o, c + o, d + o, ctr);
        m.Quad(a - o, b - o, c - o, d - o, ctr);
        m.Quad(a + o, b + o, b - o, a - o, ctr);
        m.Quad(b + o, c + o, c - o, b - o, ctr);
        m.Quad(c + o, d + o, d - o, c - o, ctr);
        m.Quad(d + o, a + o, a - o, d - o, ctr);
    }

    void EnsurePuffMesh()
    {
        if (puffMesh != null) return;
        var mb = new HutMesh(rng);
        mb.Ico(Vector3.zero, 0.5f, 0.12f);
        puffMesh = SaveMesh(mb.ToMesh("Tent_Puff"), "Tent_Puff");
    }

    // ===================================================================== BUILD
    void Prepare()
    {
        W = S.width; L = S.length; H = S.height;
        wh = Mathf.Min(S.wallHeight, H - 0.5f);
        hw = W * 0.5f;
        slopeLen = Mathf.Sqrt(hw * hw + (H - wh) * (H - wh));
        slopeAng = Mathf.Atan2(H - wh, hw) * Mathf.Rad2Deg;
        dw = Mathf.Min(S.doorWidth, W * 0.55f);
        nStr = Mathf.Max(4, Mathf.RoundToInt(L / 0.45f));
        strL = L / nStr;
    }

    void Build()
    {
        EnsureFolder(Dir);
        var old = FindTent();
        if (old != null) Undo.DestroyObjectImmediate(old);
        if (S.followPad) SnapToPad();

        rng = new System.Random(S.seed);
        matCache.Clear();
        puffMesh = null;
        lights.Clear(); swingers.Clear(); curtains.Clear(); flagSegs.Clear();
        puffPS = null;
        Prepare();
        Physics.SyncTransforms();

        float gy;
        if (!AverageGround(out gy))
        {
            gy = S.position.y;
            Debug.LogWarning("TentBuilder: под палаткой не найден Terrain/«Ground», высота взята из позиции ручки.");
        }

        var go = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(go, "Построить палатку");
        root = go.transform;
        root.SetPositionAndRotation(new Vector3(S.position.x, gy, S.position.z), Quaternion.Euler(0, S.yaw, 0));

        var s1 = NewChild(root, "Stage1");
        var s2 = NewChild(root, "Stage2");
        BuildStage1(s1);
        BuildStage2(s2);

        var bp = NewChild(root, "BuildPoint");
        float bz = L * 0.5f + 2.2f;
        bp.localPosition = new Vector3(0, GY(0, bz), bz);

        var st = go.AddComponent<TentStages>();
        st.stage1 = s1.gameObject;
        st.stage2 = s2.gameObject;
        st.buildPoint = bp;
        st.startStage = 1;
        st.curtains = curtains.ToArray();
        st.flagSegs = flagSegs.ToArray();
        st.swingers = swingers.ToArray();
        st.lights = lights.ToArray();
        st.buildPuff = puffPS;
        st.SetStage(2, false);

        Selection.activeGameObject = go;
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(go.scene);
    }

    // ===================================================================== STAGE 1: стройплощадка
    void BuildStage1(Transform parent)
    {
        var c = NewCtx(parent, "S1");
        if (!S.decSite) { Flush(c); return; }

        var wl = c.Get(M.WoodL);
        var wd = c.Get(M.WoodD);
        var rope = c.Get(M.Rope);

        // колышки по углам + верёвка, размечающая контур
        float ax = hw + 0.45f, az = L * 0.5f + 0.45f;
        var corners = new[] { new Vector2(-ax, -az), new Vector2(ax, -az), new Vector2(ax, az), new Vector2(-ax, az) };
        var tops = new List<Vector3>();
        foreach (var p in corners)
        {
            float g = GY(p.x, p.y);
            var b = new Vector3(p.x, g - 0.1f, p.y);
            var t = b + new Vector3(Jf(0.03f), 0.62f, Jf(0.03f));
            wl.Cyl(b, t, 0.04f, 0.03f, 5, 0.1f);
            tops.Add(t - new Vector3(0, 0.06f, 0));
        }
        for (int i = 0; i < 4; i++)
        {
            Vector3 a = tops[i], b = tops[(i + 1) % 4], prev = a;
            for (int s = 1; s <= 6; s++)
            {
                float t = s / 6f;
                Vector3 q = Vector3.Lerp(a, b, t) + Vector3.down * 0.12f * 4f * t * (1f - t);
                rope.Beam(prev, q, 0.025f, 0.025f, 0f);
                prev = q;
            }
        }

        // деревянный каркас: ридж + два основных и два промежуточных двускатных треугольника
        float ih = hw - 0.06f, iH = H - 0.07f, iwh = wh - 0.02f;
        wd.Cyl(new Vector3(0, iH, -L * 0.5f - 0.15f), new Vector3(0, iH, L * 0.5f + 0.15f), 0.05f, 0.05f, 6, 0.03f);
        float[] zs = { -L * 0.5f + 0.05f, -L * 0.17f, L * 0.17f, L * 0.5f - 0.05f };
        foreach (float z in zs)
        {
            for (int sg = -1; sg <= 1; sg += 2)
            {
                wl.Beam(new Vector3(sg * ih, 0, z), new Vector3(sg * ih, iwh, z), 0.07f, 0.07f, 0.01f);
                wl.Beam(new Vector3(sg * ih, iwh, z), new Vector3(0, iH, z), 0.07f, 0.07f, 0.01f);
            }
            wl.Beam(new Vector3(-ih, iwh, z), new Vector3(ih, iwh, z), 0.06f, 0.06f, 0.01f);
        }

        // уже натянутые полосы полотна у задней стороны — «процесс идёт»
        BuildCanvas(c, 0, Mathf.Max(1, nStr / 3), false);

        // рулон ткани (с полосами), перевязанный верёвкой
        float rx = hw + 1.0f, rz = L * 0.1f, rg = GY(rx, rz);
        var ca = c.Get(M.CanvasA);
        var cb = c.Get(M.CanvasB);
        Vector3 r0 = new Vector3(rx, rg + 0.2f, rz - 0.85f), r1 = new Vector3(rx, rg + 0.2f, rz + 0.85f);
        ca.Cyl(r0, r1, 0.2f, 0.2f, 8, 0.02f);
        cb.Cyl(r0, r0 + new Vector3(0, 0, 0.12f), 0.205f, 0.205f, 8, 0.01f);
        cb.Cyl(r1 - new Vector3(0, 0, 0.12f), r1, 0.205f, 0.205f, 8, 0.01f);
        for (int k = -1; k <= 1; k += 2)
            rope.Cyl(new Vector3(rx, rg + 0.2f, rz + k * 0.4f), new Vector3(rx, rg + 0.2f, rz + k * 0.4f + 0.05f), 0.215f, 0.215f, 8, 0f);
        AddCol(c, new Vector3(rx, rg + 0.2f, rz), new Vector3(0.42f, 0.42f, 1.75f));

        // стопка сложенной ткани
        float px = -(hw + 1.0f), pz = L * 0.2f, pg = GY(px, pz);
        for (int i = 0; i < 6; i++)
            c.Get(i % 2 == 0 ? M.CanvasA : M.CanvasB).Box(new Vector3(px + Jf(0.03f), pg + 0.05f + i * 0.09f, pz + Jf(0.03f)),
                new Vector3(0.9f, 0.08f, 0.7f), Quaternion.Euler(0, Jf(6f), 0), 0.04f);
        AddCol(c, new Vector3(px, pg + 0.28f, pz), new Vector3(0.9f, 0.56f, 0.7f));

        // ящик с инструментом
        float cx = -(hw + 0.9f), cz = -L * 0.25f, cg = GY(cx, cz);
        wl.Box(new Vector3(cx, cg + 0.22f, cz), new Vector3(0.6f, 0.44f, 0.45f), Quaternion.Euler(0, 12f, 0), 0.02f);
        wd.Box(new Vector3(cx, cg + 0.455f, cz), new Vector3(0.64f, 0.05f, 0.49f), Quaternion.Euler(0, 12f, 0), 0.02f);
        AddCol(c, new Vector3(cx, cg + 0.25f, cz), new Vector3(0.62f, 0.5f, 0.5f), Quaternion.Euler(0, 12f, 0));

        // молоток-киянка и колышки (запасные)
        float mx = hw + 0.7f, mz = -L * 0.3f, mg = GY(mx, mz);
        wd.Beam(new Vector3(mx, mg + 0.04f, mz), new Vector3(mx + 0.5f, mg + 0.04f, mz + 0.18f), 0.05f, 0.05f, 0.01f);
        wl.Box(new Vector3(mx - 0.05f, mg + 0.07f, mz - 0.02f), new Vector3(0.12f, 0.12f, 0.2f), Quaternion.Euler(0, 20f, 0), 0.04f);
        for (int i = 0; i < 7; i++)
        {
            float ox = Jf(0.12f);
            wl.Cyl(new Vector3(mx - 0.5f + ox, mg + 0.03f, mz + 0.3f + i * 0.045f),
                   new Vector3(mx - 0.5f + ox + 0.4f, mg + 0.03f, mz + 0.3f + i * 0.045f + Jf(0.02f)), 0.02f, 0.012f, 5, 0.1f);
        }

        // камни, прижимающие край полотна
        var st = c.Get(M.Stone);
        for (int i = 0; i < 4; i++)
        {
            float sx = -hw - 0.05f + Jf(0.1f), sz = Rf(-L * 0.45f, -L * 0.1f), sg = GY(sx, sz);
            st.Box(new Vector3(sx, sg + 0.09f, sz), new Vector3(Rf(0.25f, 0.4f), 0.18f, Rf(0.22f, 0.34f)), Quaternion.Euler(0, Jf(30f), 0), 0.08f);
        }

        Flush(c);
    }

    // ===================================================================== STAGE 2: готовая палатка
    void BuildStage2(Transform parent)
    {
        var c = NewCtx(parent, "S2");

        BuildCanvas(c, 0, nStr, S.decValance);
        BuildGables(c);
        BuildDoorFrame(c);
        BuildPoles(c);
        if (S.decRopes) BuildRopes(c);
        if (S.decInterior) BuildInterior(c);
        if (S.decProps) BuildProps(c);
        AddCol(c, new Vector3(0, H * 0.36f, 0), new Vector3(W, H * 0.72f, L));
        Flush(c);

        // --- анимируемые элементы ---
        BuildCurtains(parent);
        if (S.decFlag) BuildFlag(parent);
        if (S.decLanternIn)
            MakeLantern(parent, "InnerLantern", new Vector3(0, H - 0.04f, 0.05f), 4.2f, 1.7f);
        Transform outer = null;
        if (S.decLanternOut) outer = BuildOuterLantern(parent);
        if (S.decFireflies && outer != null) MakeFireflies(parent, outer.localPosition + new Vector3(0, -0.3f, 0));
        MakePuff(parent);
    }

    // полосатое полотно: скаты + стенки + (опц.) зубчатая кромка
    void BuildCanvas(Ctx c, int i0, int i1, bool valance)
    {
        float a = slopeAng * Mathf.Deg2Rad;
        for (int i = i0; i < i1; i++)
        {
            float z0 = -L * 0.5f + i * strL, zc = z0 + strL * 0.5f;
            var mb = c.Get(i % 2 == 0 ? M.CanvasA : M.CanvasB);
            for (int sg = -1; sg <= 1; sg += 2)
            {
                Vector3 mid = new Vector3(sg * hw * 0.5f, (wh + H) * 0.5f, zc) + new Vector3(sg * Mathf.Sin(a), Mathf.Cos(a), 0) * 0.0225f;
                mb.Box(mid, new Vector3(slopeLen * 1.03f, 0.045f, strL * 0.995f),
                       Quaternion.Euler(Jf(0.3f), 0, sg < 0 ? slopeAng : -slopeAng), 0f);
                mb.Box(new Vector3(sg * (hw + 0.0225f), wh * 0.5f, zc), new Vector3(0.045f, wh, strL * 0.995f), Quaternion.identity, 0f);
                if (valance)
                {
                    float x = sg * (hw + 0.055f);
                    Tri2(c.Get(i % 2 == 0 ? M.AccentA : M.AccentB),
                         new Vector3(x, wh + 0.03f, z0 + 0.01f), new Vector3(x, wh + 0.03f, z0 + strL - 0.01f), new Vector3(x, wh - 0.2f, zc));
                }
            }
        }
    }

    // торцы: передний с вырезом под вход, задний глухой
    void BuildGables(Ctx c)
    {
        var m = c.Get(M.CanvasA);
        float tp = RoofY(dw * 0.5f), zf = L * 0.5f, zb = -L * 0.5f, t = 0.045f;
        for (int sg = -1; sg <= 1; sg += 2)
        {
            Slab(m, new Vector3(sg * hw, 0, zf), new Vector3(sg * dw * 0.5f, 0, zf), new Vector3(sg * dw * 0.5f, tp, zf), new Vector3(sg * hw, wh, zf), t);
            if (sg < 0)
                Slab(m, new Vector3(-hw, 0, zb), new Vector3(0, 0, zb), new Vector3(0, H, zb), new Vector3(-hw, wh, zb), t);
            else
                Slab(m, new Vector3(0, 0, zb), new Vector3(hw, 0, zb), new Vector3(hw, wh, zb), new Vector3(0, H, zb), t);
        }
        // эмблема на заднем торце
        var e = c.Get(M.AccentB);
        Tri2(e, new Vector3(-0.3f, 0.5f, zb - 0.03f), new Vector3(0.3f, 0.5f, zb - 0.03f), new Vector3(0, 1.2f, zb - 0.03f));
    }

    void BuildDoorFrame(Ctx c)
    {
        float z = L * 0.5f + 0.035f, tp = RoofY(dw * 0.5f);
        var wd = c.Get(M.WoodD);
        for (int sg = -1; sg <= 1; sg += 2)
        {
            wd.Beam(new Vector3(sg * dw * 0.5f, 0, z), new Vector3(sg * dw * 0.5f, tp, z), 0.08f, 0.07f, 0f);
            wd.Beam(new Vector3(sg * dw * 0.5f, tp, z), new Vector3(0, H, z), 0.07f, 0.06f, 0f);
        }
        c.Get(M.Stone).Box(new Vector3(0, 0.03f, L * 0.5f + 0.12f), new Vector3(dw + 0.2f, 0.06f, 0.26f), Quaternion.identity, 0.03f);
    }

    void BuildPoles(Ctx c)
    {
        float zF = L * 0.5f + 0.4f, zB = -L * 0.5f - 0.4f;
        c.Get(M.WoodL).Cyl(new Vector3(0, H + 0.035f, zB), new Vector3(0, H + 0.035f, zF), 0.05f, 0.05f, 6, 0.03f);
        Vector3 m0 = new Vector3(0, H + 0.03f, zF);
        c.Get(M.WoodD).Cyl(m0, m0 + new Vector3(0, 0.75f, 0), 0.028f, 0.02f, 5, 0.02f);
        c.Get(M.AccentB).Ico(m0 + new Vector3(0, 0.79f, 0), 0.045f, 0.1f);
        // задняя А-образная опора
        float zA = -L * 0.5f - 0.32f;
        for (int sg = -1; sg <= 1; sg += 2)
            c.Get(M.WoodD).Beam(new Vector3(sg * (hw + 0.15f), GY(sg * (hw + 0.15f), zA), zA), new Vector3(0, H + 0.02f, zA), 0.07f, 0.07f, 0.01f);
    }

    void BuildRopes(Ctx c)
    {
        var rope = c.Get(M.Rope);
        var wl = c.Get(M.WoodL);
        M[] bm = { M.AccentA, M.AccentB, M.CanvasB };
        for (int sg = -1; sg <= 1; sg += 2)
        {
            foreach (float zs in new[] { L * 0.5f, 0f, -L * 0.5f })
            {
                Vector3 e = new Vector3(sg * hw, wh + 0.08f, zs);
                float sx = sg * (hw + 0.95f);
                float sz = zs + (zs > 0 ? 0.9f : (zs < 0 ? -0.9f : 0f));
                float gy = GY(sx, sz);
                Vector3 b = new Vector3(sx, gy - 0.08f, sz);
                Vector3 top = new Vector3(sx + sg * 0.05f, gy + 0.28f, sz);
                wl.Cyl(b, top, 0.03f, 0.03f, 5, 0.05f);
                rope.Beam(e, top - new Vector3(0, 0.03f, 0), 0.022f, 0.022f, 0f);
                if (S.decBunting && zs > 0)
                {
                    Vector3 dir = (top - e).normalized;
                    for (int k = 1; k <= 5; k++)
                    {
                        Vector3 p = Vector3.Lerp(e, top, k / 6f);
                        Tri2(c.Get(bm[k % 3]), p - dir * 0.07f, p + dir * 0.07f, p + new Vector3(0, -0.17f, 0));
                    }
                }
            }
        }
    }

    void BuildInterior(Ctx c)
    {
        float ft = 0.04f;
        c.Get(M.Floor).Box(new Vector3(0, ft * 0.5f, 0), new Vector3(W - 0.06f, ft, L - 0.06f), Quaternion.identity, 0f);
        // коврик с каймой
        float rw = hw * 0.8f, rl = L * 0.42f, rz = L * 0.12f;
        c.Get(M.Rug).Box(new Vector3(0, ft + 0.01f, rz), new Vector3(rw, 0.02f, rl), Quaternion.identity, 0.01f);
        c.Get(M.AccentB).Box(new Vector3(0, ft + 0.02f, rz + rl * 0.5f - 0.04f), new Vector3(rw, 0.02f, 0.06f), Quaternion.identity, 0f);
        c.Get(M.AccentB).Box(new Vector3(0, ft + 0.02f, rz - rl * 0.5f + 0.04f), new Vector3(rw, 0.02f, 0.06f), Quaternion.identity, 0f);

        // спальник, подушка, скатка
        float bx = -hw * 0.5f, bz = -L * 0.15f;
        c.Get(M.Fabric).Box(new Vector3(bx, ft + 0.07f, bz), new Vector3(0.72f, 0.12f, 1.7f), Quaternion.Euler(0, 2f, 0), 0.05f);
        c.Get(M.CanvasA).Box(new Vector3(bx, ft + 0.15f, bz - 0.7f), new Vector3(0.5f, 0.1f, 0.32f), Quaternion.Euler(0, 2f, 0), 0.08f);
        c.Get(M.AccentA).Box(new Vector3(bx, ft + 0.14f, bz + 0.25f), new Vector3(0.74f, 0.04f, 0.7f), Quaternion.Euler(0, 2f, 0), 0.03f);
        c.Get(M.Leather).Cyl(new Vector3(bx - 0.3f, ft + 0.1f, bz + 0.95f), new Vector3(bx + 0.3f, ft + 0.1f, bz + 0.95f), 0.1f, 0.1f, 7, 0.03f);

        // ящик
        float cx = hw * 0.58f, cz = -L * 0.3f;
        c.Get(M.WoodL).Box(new Vector3(cx, ft + 0.23f, cz), new Vector3(0.5f, 0.46f, 0.5f), Quaternion.Euler(0, -8f, 0), 0.02f);
        c.Get(M.WoodD).Box(new Vector3(cx, ft + 0.475f, cz), new Vector3(0.54f, 0.05f, 0.54f), Quaternion.Euler(0, -8f, 0), 0.02f);
        // рюкзак
        float kx = hw * 0.6f, kz = L * 0.02f;
        c.Get(M.Leather).Box(new Vector3(kx, ft + 0.24f, kz), new Vector3(0.38f, 0.48f, 0.26f), Quaternion.Euler(0, 20f, 0), 0.06f);
        c.Get(M.AccentB).Box(new Vector3(kx, ft + 0.12f, kz + 0.02f), new Vector3(0.3f, 0.16f, 0.3f), Quaternion.Euler(0, 20f, 0), 0.04f);
        // табурет со свечой
        float tx = 0f, tz = -L * 0.38f;
        c.Get(M.WoodD).Cyl(new Vector3(tx, ft, tz), new Vector3(tx, ft + 0.32f, tz), 0.17f, 0.17f, 7, 0.04f);
        c.Get(M.CanvasA).Cyl(new Vector3(tx, ft + 0.32f, tz), new Vector3(tx, ft + 0.43f, tz), 0.025f, 0.025f, 6, 0f);
        c.Get(M.Lamp).Ico(new Vector3(tx, ft + 0.46f, tz), 0.03f, 0.1f);
    }

    void BuildProps(Ctx c)
    {
        // бочка
        float bx = -(hw + 0.55f), bz = L * 0.5f - 0.2f, bg = GY(bx, bz);
        c.Get(M.WoodL).Cyl(new Vector3(bx, bg, bz), new Vector3(bx, bg + 0.6f, bz), 0.26f, 0.26f, 8, 0.03f);
        c.Get(M.Metal).Cyl(new Vector3(bx, bg + 0.13f, bz), new Vector3(bx, bg + 0.17f, bz), 0.27f, 0.27f, 8, 0f);
        c.Get(M.Metal).Cyl(new Vector3(bx, bg + 0.43f, bz), new Vector3(bx, bg + 0.47f, bz), 0.27f, 0.27f, 8, 0f);
        c.Get(M.WoodD).Cyl(new Vector3(bx, bg + 0.6f, bz), new Vector3(bx, bg + 0.63f, bz), 0.24f, 0.24f, 8, 0f);
        AddCol(c, new Vector3(bx, bg + 0.3f, bz), new Vector3(0.52f, 0.6f, 0.52f));

        // два ящика у боковой стены
        float cx = hw + 0.5f, cz = -L * 0.2f, cg = GY(cx, cz);
        c.Get(M.WoodL).Box(new Vector3(cx, cg + 0.22f, cz), new Vector3(0.55f, 0.44f, 0.5f), Quaternion.Euler(0, 8f, 0), 0.02f);
        c.Get(M.WoodD).Box(new Vector3(cx + 0.02f, cg + 0.62f, cz + 0.02f), new Vector3(0.42f, 0.36f, 0.4f), Quaternion.Euler(0, -14f, 0), 0.02f);
        AddCol(c, new Vector3(cx, cg + 0.4f, cz), new Vector3(0.58f, 0.8f, 0.52f));

        // пенёк-табурет
        float sx = -(dw * 0.5f + 0.95f), sz = L * 0.5f + 0.8f, sg = GY(sx, sz);
        c.Get(M.WoodD).Cyl(new Vector3(sx, sg - 0.02f, sz), new Vector3(sx, sg + 0.32f, sz), 0.22f, 0.2f, 8, 0.04f);
        c.Get(M.WoodL).Cyl(new Vector3(sx, sg + 0.31f, sz), new Vector3(sx, sg + 0.34f, sz), 0.19f, 0.19f, 8, 0.02f);
        AddCol(c, new Vector3(sx, sg + 0.17f, sz), new Vector3(0.42f, 0.34f, 0.42f));
    }

    // ===================================================================== анимируемые части
    void BuildCurtains(Transform parent)
    {
        float cw = Mathf.Min(0.32f, dw * 0.3f);
        float tp = RoofY(dw * 0.5f);
        float topIn = RoofY(dw * 0.5f - cw);
        for (int sg = -1; sg <= 1; sg += 2)
        {
            var piv = NewChild(parent, sg < 0 ? "CurtainL" : "CurtainR");
            piv.localPosition = new Vector3(sg * dw * 0.5f, 0f, L * 0.5f + 0.05f);
            var c = NewCtx(piv, sg < 0 ? "CurL" : "CurR");
            float d = -sg;   // в сторону центра
            Slab(c.Get(M.AccentA), new Vector3(0, 0.03f, 0), new Vector3(d * cw, 0.03f, 0), new Vector3(d * cw, topIn - 0.02f, 0), new Vector3(0, tp - 0.02f, 0), 0.025f);
            c.Get(M.AccentB).Box(new Vector3(d * cw * 0.5f, tp * 0.42f, 0), new Vector3(cw + 0.06f, 0.05f, 0.06f), Quaternion.identity, 0.02f);
            Flush(c);
            curtains.Add(piv);
        }
    }

    void BuildFlag(Transform parent)
    {
        float zF = L * 0.5f + 0.4f;
        Transform prev = NewChild(parent, "Flag");
        prev.localPosition = new Vector3(0.02f, H + 0.7f, zF);
        int segs = 5;
        float len = 0.15f, full = 0.34f;
        for (int i = 0; i < segs; i++)
        {
            var seg = NewChild(prev, "FlagSeg" + i);
            seg.localPosition = i == 0 ? Vector3.zero : new Vector3(len, 0, 0);
            var c = NewCtx(seg, "Flag" + i);
            float h0 = full * (1f - i / (float)segs), h1 = full * (1f - (i + 1) / (float)segs);
            var mb = c.Get(i % 2 == 0 ? M.CanvasB : M.AccentB);
            Vector3 a = new Vector3(0, h0 * 0.5f, 0), b = new Vector3(len, h1 * 0.5f, 0);
            Vector3 cc = new Vector3(len, -h1 * 0.5f, 0), dd = new Vector3(0, -h0 * 0.5f, 0);
            mb.Quad(a, b, cc, dd, new Vector3(len * 0.5f, 0, -1f));
            mb.Quad(a, b, cc, dd, new Vector3(len * 0.5f, 0, 1f));
            Flush(c);
            flagSegs.Add(seg);
            prev = seg;
        }
    }

    Transform BuildOuterLantern(Transform parent)
    {
        float px = dw * 0.5f + 0.6f, pz = L * 0.5f + 0.55f, pg = GY(px, pz);
        var c = NewCtx(parent, "LampPost");
        var wd = c.Get(M.WoodD);
        float ph = 1.5f;
        wd.Cyl(new Vector3(px, pg - 0.05f, pz), new Vector3(px, pg + ph, pz), 0.045f, 0.035f, 6, 0.03f);
        wd.Beam(new Vector3(px, pg + ph - 0.05f, pz), new Vector3(px - 0.38f, pg + ph - 0.05f, pz), 0.05f, 0.05f, 0.01f);
        c.Get(M.WoodL).Beam(new Vector3(px, pg + ph - 0.45f, pz), new Vector3(px - 0.3f, pg + ph - 0.07f, pz), 0.035f, 0.035f, 0.01f);
        c.Get(M.Stone).Box(new Vector3(px, pg + 0.04f, pz), new Vector3(0.3f, 0.1f, 0.3f), Quaternion.Euler(0, 20f, 0), 0.05f);
        AddCol(c, new Vector3(px, pg + ph * 0.5f, pz), new Vector3(0.14f, ph, 0.14f));
        Flush(c);
        return MakeLantern(parent, "OuterLantern", new Vector3(px - 0.38f, pg + ph - 0.07f, pz), 5.5f, 2.2f);
    }

    Transform MakeLantern(Transform parent, string name, Vector3 pos, float range, float intensity)
    {
        var piv = NewChild(parent, name);
        piv.localPosition = pos;
        var c = NewCtx(piv, name);
        var mt = c.Get(M.Metal);
        c.Get(M.Rope).Beam(Vector3.zero, new Vector3(0, -0.1f, 0), 0.015f, 0.015f, 0f);
        mt.Cyl(new Vector3(0, -0.1f, 0), new Vector3(0, -0.16f, 0), 0.07f, 0.07f, 6, 0f);
        mt.Cyl(new Vector3(0, -0.16f, 0), new Vector3(0, -0.2f, 0), 0.02f, 0.085f, 6, 0f);
        c.Get(M.Lamp).Box(new Vector3(0, -0.29f, 0), new Vector3(0.13f, 0.17f, 0.13f), Quaternion.identity, 0.02f);
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                mt.Box(new Vector3(sx * 0.07f, -0.29f, sz * 0.07f), new Vector3(0.02f, 0.2f, 0.02f), Quaternion.identity, 0f);
        mt.Box(new Vector3(0, -0.4f, 0), new Vector3(0.17f, 0.03f, 0.17f), Quaternion.identity, 0f);
        Flush(c);

        if (S.decLights)
        {
            var lg = new GameObject(name + "Light");
            lg.transform.SetParent(piv, false);
            lg.transform.localPosition = new Vector3(0, -0.29f, 0);
            var l = lg.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = range;
            l.intensity = intensity;
            l.color = new Color(1f, 0.74f, 0.4f);
            l.shadows = LightShadows.None;
            lights.Add(l);
        }
        swingers.Add(piv);
        return piv;
    }

    void MakeFireflies(Transform parent, Vector3 pos)
    {
        EnsurePuffMesh();
        var go = new GameObject("Fireflies");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 6f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.07f);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 8;

        var em = ps.emission;
        em.rateOverTime = 1.6f;

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius = 0.5f;

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.3f;
        noise.frequency = 1.1f;
        noise.scrollSpeed = 0.6f;

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.2f, 1f), new Keyframe(0.8f, 1f), new Keyframe(1f, 0f)));

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Mesh;
        r.mesh = puffMesh;
        r.sharedMaterial = GetMat(M.Lamp);
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        ps.Play();
    }

    // облачко пыли при "раскладывании" палатки
    void MakePuff(Transform parent)
    {
        EnsurePuffMesh();
        var go = new GameObject("BuildPuff");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0, 0.12f, 0);
        go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);   // круг в плоскости земли
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.05f;
        main.maxParticles = 40;

        var em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, 26) });

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Circle;
        sh.radius = Mathf.Max(W, L) * 0.4f;
        sh.radiusThickness = 0.3f;

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0f)));

        var vel = ps.limitVelocityOverLifetime;
        vel.enabled = true;
        vel.dampen = 0.15f;
        vel.limit = 1.5f;

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Mesh;
        r.mesh = puffMesh;
        r.sharedMaterial = GetMat(M.Dust);
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        puffPS = ps;
    }
}
