// Tools > BuilderSim > Shop Builder
// Процедурная торговая лавка в 3 этапа, мультяшный стиль. Unity 6 (6000.3), URP.
// Положить в папку Editor. Самодостаточный (свой генератор мешей, HutMesh не нужен).
// Требует рантайм-компонент ShopStages.cs (в НЕ-Editor папке).
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ShopBuilder : EditorWindow
{
    const string Dir = "Assets/_Game/Shop";
    const string PrefKey = "BuilderSim.ShopBuilder.v1";
    const string RootName = "TradingStall";

    const float PlatH = 0.28f;   // высота помоста
    const float PostH = 2.2f;    // высота столбов над помостом
    const float Slope = 0.42f;   // tan угла крыши (~23°)

    [System.Serializable]
    public class Settings
    {
        public float width = 3.6f, depth = 2.6f;
        public int seed = 321;
        public int previewStage = 3;

        public Color woodLight = new Color(0.78f, 0.55f, 0.32f);
        public Color woodDark = new Color(0.42f, 0.27f, 0.15f);
        public Color roof = new Color32(0x1C, 0x2A, 0x3A, 255);   // под цвет UI
        public Color cream = new Color(0.96f, 0.90f, 0.74f);
        public Color accent = new Color(0.86f, 0.30f, 0.25f);
        public Color gold = new Color(1.00f, 0.78f, 0.20f);
        public Color metal = new Color(0.56f, 0.61f, 0.67f);
        public Color grass = new Color(0.38f, 0.70f, 0.25f);

        public bool decGrass = true, decTools = true;                 // этап 1
        public bool decRack = true;                                   // этап 2
        public bool decSign = true, decLogs = true, decScales = true, // этап 3
                    decBook = true, decChest = true;
        public Vector3 position;
        public float yaw;
    }

    enum MatId { WoodL, WoodM, WoodD, Roof, Cream, Gold, GoldD, Metal, Grass, GrassL, Red }

    static readonly Quaternion AlongX = Quaternion.Euler(0, 0, 90);
    static readonly Quaternion AlongZ = Quaternion.Euler(90, 0, 0);

    // ====================== мини-генератор мешей ======================
    class MB
    {
        public List<Vector3> V = new List<Vector3>();
        public List<Vector3> N = new List<Vector3>();
        public List<int> T = new List<int>();

        int Add(Vector3 p, Vector3 n) { V.Add(p); N.Add(n); return V.Count - 1; }

        // порядок вершин подбирается автоматически под нормаль
        void Tri(int a, int b, int c)
        {
            Vector3 fn = Vector3.Cross(V[b] - V[a], V[c] - V[a]);
            if (Vector3.Dot(fn, N[a] + N[b] + N[c]) < 0f) { int t = b; b = c; c = t; }
            T.Add(a); T.Add(b); T.Add(c);
        }

        void Quad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 n)
        {
            int a = Add(p0, n), b = Add(p1, n), c = Add(p2, n), d = Add(p3, n);
            Tri(a, b, c); Tri(a, c, d);
        }

        public void Triangle(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 n)
        {
            int a = Add(p0, n), b = Add(p1, n), c = Add(p2, n);
            Tri(a, b, c);
        }

        static readonly Vector3[] Ax = { Vector3.right, Vector3.up, Vector3.forward };

        public void Box(Vector3 c, Vector3 s, Quaternion q)
        {
            Vector3 h = s * 0.5f;
            for (int ai = 0; ai < 3; ai++)
            {
                Vector3 d = Ax[ai], u = Ax[(ai + 1) % 3], v = Ax[(ai + 2) % 3];
                float hd = Mathf.Abs(Vector3.Dot(d, h));
                float hu = Mathf.Abs(Vector3.Dot(u, h));
                float hv = Mathf.Abs(Vector3.Dot(v, h));
                for (int sg = -1; sg <= 1; sg += 2)
                {
                    Vector3 o = d * (hd * sg);
                    Quad(c + q * (o - u * hu - v * hv),
                         c + q * (o + u * hu - v * hv),
                         c + q * (o + u * hu + v * hv),
                         c + q * (o - u * hu + v * hv),
                         q * (d * sg));
                }
            }
        }

        // цилиндр, ось = локальная Y
        public void Cyl(Vector3 c, float rx, float rz, float h, Quaternion q, int seg)
        {
            float hh = h * 0.5f;
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
                Vector3 n0 = new Vector3(Mathf.Cos(a0) / rx, 0, Mathf.Sin(a0) / rz).normalized;
                Vector3 n1 = new Vector3(Mathf.Cos(a1) / rx, 0, Mathf.Sin(a1) / rz).normalized;
                Vector3 r0 = new Vector3(Mathf.Cos(a0) * rx, 0, Mathf.Sin(a0) * rz);
                Vector3 r1 = new Vector3(Mathf.Cos(a1) * rx, 0, Mathf.Sin(a1) * rz);
                int a = Add(c + q * (r0 + Vector3.down * hh), q * n0);
                int b = Add(c + q * (r1 + Vector3.down * hh), q * n1);
                int cc = Add(c + q * (r1 + Vector3.up * hh), q * n1);
                int d = Add(c + q * (r0 + Vector3.up * hh), q * n0);
                Tri(a, b, cc); Tri(a, cc, d);
            }
            for (int sg = -1; sg <= 1; sg += 2)
            {
                Vector3 n = q * new Vector3(0, sg, 0);
                int ctr = Add(c + q * new Vector3(0, sg * hh, 0), n);
                for (int i = 0; i < seg; i++)
                {
                    float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
                    int p0 = Add(c + q * new Vector3(Mathf.Cos(a0) * rx, sg * hh, Mathf.Sin(a0) * rz), n);
                    int p1 = Add(c + q * new Vector3(Mathf.Cos(a1) * rx, sg * hh, Mathf.Sin(a1) * rz), n);
                    Tri(ctr, p0, p1);
                }
            }
        }

        // конус (травинка), основание в c, вершина вверх по локальной Y
        public void Cone(Vector3 c, float r, float h, Quaternion q, int seg)
        {
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg, am = (a0 + a1) * 0.5f;
                Vector3 n0 = new Vector3(Mathf.Cos(a0) * h, r, Mathf.Sin(a0) * h).normalized;
                Vector3 n1 = new Vector3(Mathf.Cos(a1) * h, r, Mathf.Sin(a1) * h).normalized;
                Vector3 nm = new Vector3(Mathf.Cos(am) * h, r, Mathf.Sin(am) * h).normalized;
                int a = Add(c + q * new Vector3(Mathf.Cos(a0) * r, 0, Mathf.Sin(a0) * r), q * n0);
                int b = Add(c + q * new Vector3(Mathf.Cos(a1) * r, 0, Mathf.Sin(a1) * r), q * n1);
                int t = Add(c + q * new Vector3(0, h, 0), q * nm);
                Tri(a, b, t);
            }
        }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name };
            m.indexFormat = V.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            m.SetVertices(V);
            m.SetNormals(N);
            m.SetTriangles(T, 0);
            m.RecalculateBounds();
            return m;
        }
    }

    // часть здания = GameObject + по одному мешу на материал
    class Part
    {
        public GameObject go;
        public Dictionary<MatId, MB> mbs = new Dictionary<MatId, MB>();
        Vector3 o = Vector3.zero;
        Quaternion r = Quaternion.identity;

        public Part(Transform parent, string name)
        {
            go = new GameObject(name);
            go.transform.SetParent(parent, false);
        }

        public MB Get(MatId id)
        {
            MB m;
            if (!mbs.TryGetValue(id, out m)) { m = new MB(); mbs[id] = m; }
            return m;
        }

        // локальная система для следующих примитивов
        public void At(Vector3 origin, float yaw = 0f) { o = origin; r = Quaternion.Euler(0, yaw, 0); }

        public void Box(MatId id, Vector3 lp, Vector3 size, Quaternion? lr = null)
        { Get(id).Box(o + r * lp, size, r * (lr ?? Quaternion.identity)); }

        public void Cyl(MatId id, Vector3 lp, float rad, float h, Quaternion? lr = null, int seg = 12)
        { Get(id).Cyl(o + r * lp, rad, rad, h, r * (lr ?? Quaternion.identity), seg); }

        public void Cone(MatId id, Vector3 lp, float rad, float h, Quaternion? lr = null, int seg = 5)
        { Get(id).Cone(o + r * lp, rad, h, r * (lr ?? Quaternion.identity), seg); }

        public void Tri2(MatId id, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 pa = o + r * a, pb = o + r * b, pc = o + r * c;
            Vector3 n = Vector3.Cross(pb - pa, pc - pa).normalized;
            Get(id).Triangle(pa, pb, pc, n);
            Get(id).Triangle(pa, pb, pc, -n);
        }
    }

    // ====================== состояние окна ======================
    Settings S = new Settings();
    Vector2 scroll;
    System.Random rng;
    Transform root;
    readonly Dictionary<MatId, Material> matCache = new Dictionary<MatId, Material>();

    // общие точки между этапами
    Vector3[] pallets;
    float counterL, counterR, counterZ;

    float Rnd() { return (float)rng.NextDouble(); }

    [MenuItem("Tools/BuilderSim/Здания/Shop Builder", false, 301)]
    static void Open()
    {
        var w = GetWindow<ShopBuilder>(false, "Лавка");
        w.minSize = new Vector2(360, 560);
        w.Show();
    }

    void OnEnable() { Load(); SceneView.duringSceneGui += OnSceneGUI; }
    void OnDisable() { SceneView.duringSceneGui -= OnSceneGUI; Save(); }
    void Save() { EditorPrefs.SetString(PrefKey, JsonUtility.ToJson(S)); }
    void Load() { if (EditorPrefs.HasKey(PrefKey)) JsonUtility.FromJsonOverwrite(EditorPrefs.GetString(PrefKey), S); }

    // ====================== окно ======================
    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("Торговая лавка", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();

        EditorGUILayout.LabelField("Параметры", EditorStyles.boldLabel);
        S.width = EditorGUILayout.Slider("Ширина помоста (м)", S.width, 3.0f, 5.0f);
        S.depth = EditorGUILayout.Slider("Глубина помоста (м)", S.depth, 2.2f, 3.6f);
        S.seed = EditorGUILayout.IntField("Seed", S.seed);
        S.previewStage = EditorGUILayout.IntSlider("Этап (предпросмотр)", S.previewStage, 1, 3);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Цвета", EditorStyles.boldLabel);
        S.woodLight = EditorGUILayout.ColorField("Светлое дерево", S.woodLight);
        S.woodDark = EditorGUILayout.ColorField("Тёмное дерево", S.woodDark);
        S.roof = EditorGUILayout.ColorField("Крыша (#1C2A3A)", S.roof);
        S.cream = EditorGUILayout.ColorField("Полосы навеса / бумага", S.cream);
        S.accent = EditorGUILayout.ColorField("Акцент (ящик, книга)", S.accent);
        S.gold = EditorGUILayout.ColorField("Золото / монеты", S.gold);
        S.metal = EditorGUILayout.ColorField("Металл", S.metal);
        S.grass = EditorGUILayout.ColorField("Трава", S.grass);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Декор: этап 1", EditorStyles.boldLabel);
        S.decGrass = EditorGUILayout.Toggle("Трава и цветы", S.decGrass);
        S.decTools = EditorGUILayout.Toggle("Козелки, ящик, доски", S.decTools);
        EditorGUILayout.LabelField("Декор: этап 2", EditorStyles.boldLabel);
        S.decRack = EditorGUILayout.Toggle("Стеллаж и поддоны", S.decRack);
        EditorGUILayout.LabelField("Декор: этап 3", EditorStyles.boldLabel);
        S.decSign = EditorGUILayout.Toggle("Вывеска", S.decSign);
        S.decLogs = EditorGUILayout.Toggle("Стопки бревен", S.decLogs);
        S.decScales = EditorGUILayout.Toggle("Весы", S.decScales);
        S.decBook = EditorGUILayout.Toggle("Счётная книга", S.decBook);
        S.decChest = EditorGUILayout.Toggle("Сундучок и монеты", S.decChest);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Положение (или ручки в Scene)", EditorStyles.boldLabel);
        S.position = EditorGUILayout.Vector3Field("Позиция", S.position);
        S.yaw = EditorGUILayout.FloatField("Поворот Y (°)", S.yaw);

        if (EditorGUI.EndChangeCheck()) { Save(); MoveExisting(); ApplyPreview(); SceneView.RepaintAll(); }

        EditorGUILayout.Space(8);
        if (GUILayout.Button("Построить / перестроить", GUILayout.Height(30))) Build();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Сохранить префаб")) SavePrefab();
            if (GUILayout.Button("Удалить из сцены")) DeleteExisting();
        }
        EditorGUILayout.HelpBox("Лавка смотрит фасадом (прилавок, вывеска) в сторону +Z; вход справа, со ступенькой.", MessageType.Info);
        EditorGUILayout.EndScrollView();
    }

    void OnSceneGUI(SceneView sv)
    {
        var r = FindRoot();
        if (!r) return;
        Handles.color = new Color(1f, 0.8f, 0.2f, 0.9f);
        Handles.matrix = Matrix4x4.TRS(r.position, r.rotation, Vector3.one);
        Handles.DrawWireCube(new Vector3(0, 0.5f, 0), new Vector3(S.width, 1f, S.depth));
        Handles.matrix = Matrix4x4.identity;

        EditorGUI.BeginChangeCheck();
        Vector3 pos = Handles.PositionHandle(r.position, r.rotation);
        Quaternion rot = Handles.Disc(r.rotation, r.position, Vector3.up, 1.2f, false, 0f);
        if (EditorGUI.EndChangeCheck())
        {
            S.position = pos;
            S.yaw = rot.eulerAngles.y;
            Save(); MoveExisting(); Repaint();
        }
    }

    Transform FindRoot()
    {
        var sc = SceneManager.GetActiveScene();
        if (!sc.IsValid()) return null;
        foreach (var g in sc.GetRootGameObjects()) if (g.name == RootName) return g.transform;
        return null;
    }

    void MoveExisting()
    {
        var r = FindRoot();
        if (!r) return;
        r.position = S.position;
        r.rotation = Quaternion.Euler(0, S.yaw, 0);
    }

    void ApplyPreview()
    {
        var r = FindRoot();
        if (!r) return;
        var st = r.GetComponent<ShopStages>();
        if (st) st.SetStage(S.previewStage);
    }

    void DeleteExisting()
    {
        var r = FindRoot();
        if (r) Undo.DestroyObjectImmediate(r.gameObject);
    }

    void SavePrefab()
    {
        var r = FindRoot();
        if (!r) { EditorUtility.DisplayDialog("Лавка", "Сначала постройте лавку.", "OK"); return; }
        EnsureDirs();
        PrefabUtility.SaveAsPrefabAssetAndConnect(r.gameObject, Dir + "/TradingStall.prefab", InteractionMode.UserAction);
        AssetDatabase.SaveAssets();
    }

    // ====================== ассеты ======================
    static void EnsureDirs()
    {
        foreach (var d in new[] { Dir, Dir + "/Meshes", Dir + "/Materials" })
            if (!Directory.Exists(d)) Directory.CreateDirectory(d);
        AssetDatabase.Refresh();
    }

    Color ColorOf(MatId id)
    {
        switch (id)
        {
            case MatId.WoodL: return S.woodLight;
            case MatId.WoodM: return Color.Lerp(S.woodLight, S.woodDark, 0.45f);
            case MatId.WoodD: return S.woodDark;
            case MatId.Roof: return S.roof;
            case MatId.Cream: return S.cream;
            case MatId.Gold: return S.gold;
            case MatId.GoldD: return new Color(S.gold.r * 0.78f, S.gold.g * 0.72f, S.gold.b * 0.6f, 1f);
            case MatId.Metal: return S.metal;
            case MatId.Grass: return S.grass;
            case MatId.GrassL: return Color.Lerp(S.grass, Color.white, 0.25f);
            default: return S.accent;
        }
    }

    Material GetMat(MatId id)
    {
        Material m;
        if (!matCache.TryGetValue(id, out m) || m == null)
        {
            string path = Dir + "/Materials/Shop_" + id + ".mat";
            m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit");
                if (sh == null) sh = Shader.Find("Standard");
                m = new Material(sh);
                AssetDatabase.CreateAsset(m, path);
            }
            matCache[id] = m;
        }
        Color c = ColorOf(id);
        m.SetColor("_BaseColor", c);
        m.SetColor("_Color", c);
        bool metal = id == MatId.Metal || id == MatId.Gold || id == MatId.GoldD;
        m.SetFloat("_Metallic", metal ? 0.5f : 0f);
        m.SetFloat("_Smoothness", metal ? 0.55f : 0.1f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Mesh SaveMesh(Mesh m, string path)
    {
        var ex = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (ex != null)
        {
            ex.Clear();
            EditorUtility.CopySerialized(m, ex);
            DestroyImmediate(m);
            return ex;
        }
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    void Finish(Part p)
    {
        foreach (var kv in p.mbs)
        {
            if (kv.Value.V.Count == 0) continue;
            var child = new GameObject(kv.Key.ToString());
            child.transform.SetParent(p.go.transform, false);
            var mesh = SaveMesh(kv.Value.ToMesh(p.go.name + "_" + kv.Key), Dir + "/Meshes/" + p.go.name + "_" + kv.Key + ".asset");
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            child.AddComponent<MeshRenderer>().sharedMaterial = GetMat(kv.Key);
        }
    }

    static void AddCollider(Transform parent, string name, Vector3 center, Vector3 size)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        var bc = g.AddComponent<BoxCollider>();
        bc.center = center; bc.size = size;
    }

    // ====================== сборка ======================
    void Build()
    {
        EnsureDirs();
        matCache.Clear();
        DeleteExisting();
        rng = new System.Random(S.seed);

        float W = S.width, D = S.depth;
        counterL = -W / 2 + 0.5f;
        counterR = W / 2 - 1.0f;
        counterZ = D / 2 - 0.45f;
        pallets = new[] { new Vector3(-W / 2 + 0.9f, 0, -0.2f), new Vector3(-W / 2 + 2.1f, 0, -0.2f) };

        var go = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(go, "Build Shop");
        root = go.transform;
        root.position = S.position;
        root.rotation = Quaternion.Euler(0, S.yaw, 0);

        // Этап 1 (всегда виден): помост
        var plat = new Part(root, "Platform");
        BuildPlatform(plat);
        if (S.decGrass) BuildGrass(plat);
        Finish(plat);
        AddCollider(plat.go.transform, "Collider", new Vector3(0, PlatH / 2, 0), new Vector3(W + 0.1f, PlatH, D + 0.1f));

        // Этап 1–2: стройинвентарь
        var props = new Part(root, "Stage1_Props");
        if (S.decTools) BuildTools(props);
        Finish(props);

        // Этап 2: каркас, прилавок, стеллаж
        var s2 = new Part(root, "Stage2");
        BuildFrame(s2);
        BuildCounter(s2);
        if (S.decRack) BuildRack(s2);
        Finish(s2);
        float cw = counterR - counterL;
        AddCollider(s2.go.transform, "CounterCollider",
            new Vector3((counterL + counterR) / 2, PlatH + 0.5f, counterZ), new Vector3(cw, 1.0f, 0.6f));

        // Этап 3: крыша, вывеска, товар
        var s3 = new Part(root, "Stage3");
        BuildRoof(s3);
        if (S.decSign) BuildSign(s3);
        if (S.decLogs) BuildLogs(s3);
        BuildCounterProps(s3);
        Finish(s3);

        var st = go.AddComponent<ShopStages>();
        st.constructionProps = props.go;
        st.stage2 = s2.go;
        st.stage3 = s3.go;
        st.stage = S.previewStage;
        st.Apply();

        AssetDatabase.SaveAssets();
        Selection.activeGameObject = go;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    // -------- этап 1: помост --------
    void BuildPlatform(Part p)
    {
        float W = S.width, D = S.depth;
        p.At(Vector3.zero);

        // травяная лужайка
        p.Get(MatId.Grass).Cyl(new Vector3(0, 0.01f, 0), W * 0.5f + 1.2f, D * 0.5f + 1.0f, 0.06f, Quaternion.identity, 24);

        // основание и обвязка
        p.Box(MatId.WoodD, new Vector3(0, 0.115f, 0), new Vector3(W, 0.17f, D));
        p.Box(MatId.WoodD, new Vector3(0, 0.2f, D / 2 + 0.02f), new Vector3(W + 0.12f, 0.12f, 0.12f));
        p.Box(MatId.WoodD, new Vector3(0, 0.2f, -D / 2 - 0.02f), new Vector3(W + 0.12f, 0.12f, 0.12f));
        p.Box(MatId.WoodD, new Vector3(W / 2 + 0.02f, 0.2f, 0), new Vector3(0.12f, 0.12f, D + 0.12f));
        p.Box(MatId.WoodD, new Vector3(-W / 2 - 0.02f, 0.2f, 0), new Vector3(0.12f, 0.12f, D + 0.12f));

        // доски настила
        int n = Mathf.Max(3, Mathf.RoundToInt(D / 0.26f));
        float pd = D / n;
        for (int i = 0; i < n; i++)
        {
            float z = -D / 2 + pd * (i + 0.5f);
            float len = W - 0.02f - Rnd() * 0.04f;
            float dx = (Rnd() - 0.5f) * 0.04f;
            float dy = (Rnd() - 0.5f) * 0.014f;
            p.Box(Rnd() < 0.5f ? MatId.WoodL : MatId.WoodM, new Vector3(dx, 0.23f + dy, z), new Vector3(len, 0.1f, pd - 0.025f));
        }

        // ступенька у входа (справа, спереди)
        p.Box(MatId.WoodM, new Vector3(W / 2 - 0.55f, 0.09f, D / 2 + 0.30f), new Vector3(0.9f, 0.14f, 0.36f));
        p.Box(MatId.WoodD, new Vector3(W / 2 - 0.55f, 0.05f, D / 2 + 0.31f), new Vector3(0.94f, 0.06f, 0.4f));
    }

    void BuildGrass(Part p)
    {
        float W = S.width, D = S.depth;
        p.At(Vector3.zero);
        for (int i = 0; i < 60; i++)
        {
            float a = Rnd() * Mathf.PI * 2f;
            float k = 0.15f + Rnd() * 0.8f;
            float x = Mathf.Cos(a) * (W / 2 + k), z = Mathf.Sin(a) * (D / 2 + k);
            if (z > 0 && Mathf.Abs(x - (W / 2 - 0.55f)) < 0.8f && z < D / 2 + 0.8f) continue;
            Tuft(p, new Vector3(x, 0.03f, z));
        }
        for (int i = 0; i < 14; i++)
        {
            float a = Rnd() * Mathf.PI * 2f;
            float k = 0.3f + Rnd() * 0.8f;
            float x = Mathf.Cos(a) * (W / 2 + k), z = Mathf.Sin(a) * (D / 2 + k);
            if (z > 0 && Mathf.Abs(x - (W / 2 - 0.55f)) < 0.8f && z < D / 2 + 0.8f) continue;
            Flower(p, new Vector3(x, 0.03f, z));
        }
    }

    void Tuft(Part p, Vector3 pos)
    {
        int blades = 3 + rng.Next(3);
        for (int b = 0; b < blades; b++)
        {
            float h = 0.18f + Rnd() * 0.16f, r = 0.035f + Rnd() * 0.02f;
            var lean = Quaternion.Euler((Rnd() - 0.5f) * 50f, 0, (Rnd() - 0.5f) * 50f);
            p.Cone(Rnd() < 0.5f ? MatId.Grass : MatId.GrassL,
                   pos + new Vector3((Rnd() - 0.5f) * 0.1f, 0, (Rnd() - 0.5f) * 0.1f), r, h, lean);
        }
    }

    void Flower(Part p, Vector3 pos)
    {
        p.Cyl(MatId.Grass, pos + new Vector3(0, 0.11f, 0), 0.01f, 0.22f, null, 5);
        var tilt = Quaternion.Euler(35f, Rnd() * 360f, 0);
        p.Cyl(Rnd() < 0.5f ? MatId.Cream : MatId.Red, pos + new Vector3(0, 0.23f, 0), 0.045f, 0.03f, tilt, 8);
        p.Cyl(MatId.Gold, pos + new Vector3(0, 0.23f, 0), 0.02f, 0.036f, tilt, 6);
    }

    // -------- этап 1: стройинвентарь на траве --------
    void BuildTools(Part p)
    {
        float W = S.width, D = S.depth;

        // два козелка с доской
        float sx = -W / 2 - 0.95f;
        Sawhorse(p, new Vector3(sx, 0.03f, -0.35f));
        Sawhorse(p, new Vector3(sx, 0.03f, 0.55f));
        p.At(Vector3.zero);
        p.Box(MatId.WoodL, new Vector3(sx + 0.05f, 0.715f, 0.1f), new Vector3(0.22f, 0.04f, 1.6f), Quaternion.Euler(0, 4f, 0));
        p.Box(MatId.WoodM, new Vector3(sx - 0.1f, 0.755f, 0.2f), new Vector3(0.18f, 0.04f, 0.9f), Quaternion.Euler(0, -6f, 0));

        // ящик с инструментами
        p.At(new Vector3(W / 2 + 0.8f, 0.03f, -0.2f), 25f);
        p.Box(MatId.Red, new Vector3(0, 0.12f, 0), new Vector3(0.58f, 0.24f, 0.3f));
        p.Box(MatId.Metal, new Vector3(0, 0.245f, 0), new Vector3(0.6f, 0.03f, 0.32f));
        p.Box(MatId.Metal, new Vector3(-0.2f, 0.12f, 0), new Vector3(0.05f, 0.25f, 0.31f));
        p.Box(MatId.Metal, new Vector3(0.2f, 0.12f, 0), new Vector3(0.05f, 0.25f, 0.31f));
        p.Box(MatId.Metal, new Vector3(-0.1f, 0.3f, 0), new Vector3(0.03f, 0.09f, 0.03f));
        p.Box(MatId.Metal, new Vector3(0.1f, 0.3f, 0), new Vector3(0.03f, 0.09f, 0.03f));
        p.Box(MatId.WoodD, new Vector3(0, 0.35f, 0), new Vector3(0.26f, 0.035f, 0.035f));

        // молоток
        p.At(new Vector3(W / 2 + 0.75f, 0.05f, 0.4f), 70f);
        p.Box(MatId.WoodL, new Vector3(0, 0, 0), new Vector3(0.34f, 0.03f, 0.03f));
        p.Box(MatId.Metal, new Vector3(0.15f, 0.02f, 0), new Vector3(0.05f, 0.07f, 0.1f));

        // стопка досок
        p.At(new Vector3(W / 2 + 0.85f, 0.03f, -D / 2 - 0.1f), -12f);
        for (int i = 0; i < 4; i++)
        {
            p.Box(MatId.WoodL, new Vector3((Rnd() - 0.5f) * 0.05f, 0.03f + i * 0.055f, 0), new Vector3(1.25f, 0.05f, 0.24f), Quaternion.Euler(0, (Rnd() - 0.5f) * 6f, 0));
        }
        p.Box(MatId.WoodD, new Vector3(-0.4f, 0.01f, 0), new Vector3(0.1f, 0.03f, 0.3f));
        p.Box(MatId.WoodD, new Vector3(0.4f, 0.01f, 0), new Vector3(0.1f, 0.03f, 0.3f));
    }

    void Sawhorse(Part p, Vector3 pos)
    {
        p.At(pos, 0);
        p.Box(MatId.WoodL, new Vector3(0, 0.62f, 0), new Vector3(0.95f, 0.09f, 0.12f));
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                p.Box(MatId.WoodD, new Vector3(sx * 0.36f, 0.31f, sz * 0.09f), new Vector3(0.07f, 0.66f, 0.07f), Quaternion.Euler(-sz * 18f, 0, 0));
        p.Box(MatId.WoodM, new Vector3(0, 0.28f, 0), new Vector3(0.72f, 0.04f, 0.04f));
    }

    // -------- этап 2: каркас --------
    void BuildFrame(Part p)
    {
        float W = S.width, D = S.depth;
        p.At(Vector3.zero);
        float px = W / 2 - 0.18f, pz = D / 2 - 0.18f;
        float top = PlatH + PostH;

        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                p.Box(MatId.WoodL, new Vector3(sx * px, PlatH + PostH / 2, sz * pz), new Vector3(0.2f, PostH, 0.2f));
                p.Box(MatId.WoodD, new Vector3(sx * px, PlatH + 0.07f, sz * pz), new Vector3(0.3f, 0.14f, 0.3f));
                p.Box(MatId.WoodD, new Vector3(sx * px, top - 0.05f, sz * pz), new Vector3(0.28f, 0.1f, 0.28f));
                // раскос
                p.Box(MatId.WoodM, new Vector3(sx * (px - 0.22f), top - 0.3f, sz * pz), new Vector3(0.07f, 0.5f, 0.07f), Quaternion.Euler(0, 0, sx * 45f));
            }

        // обвязка поверху
        p.Box(MatId.WoodM, new Vector3(0, top - 0.1f, pz), new Vector3(W - 0.2f, 0.16f, 0.16f));
        p.Box(MatId.WoodM, new Vector3(0, top - 0.1f, -pz), new Vector3(W - 0.2f, 0.16f, 0.16f));
        p.Box(MatId.WoodM, new Vector3(px, top - 0.1f, 0), new Vector3(0.16f, 0.16f, D - 0.2f));
        p.Box(MatId.WoodM, new Vector3(-px, top - 0.1f, 0), new Vector3(0.16f, 0.16f, D - 0.2f));
    }

    // -------- этап 2: прилавок --------
    void BuildCounter(Part p)
    {
        float cw = counterR - counterL;
        float cx = (counterL + counterR) / 2;
        p.At(Vector3.zero);

        p.Box(MatId.WoodD, new Vector3(cx, PlatH + 0.425f, counterZ), new Vector3(cw, 0.85f, 0.5f));
        // вертикальные доски фасада
        int nb = Mathf.RoundToInt(cw / 0.2f);
        float bw = cw / nb;
        for (int i = 0; i < nb; i++)
            p.Box(i % 2 == 0 ? MatId.WoodL : MatId.WoodM,
                  new Vector3(counterL + bw * (i + 0.5f), PlatH + 0.41f, counterZ + 0.27f), new Vector3(bw - 0.02f, 0.78f, 0.05f));
        // столешница с козырьком
        p.Box(MatId.WoodL, new Vector3(cx, PlatH + 0.9f, counterZ + 0.06f), new Vector3(cw + 0.16f, 0.1f, 0.78f));
        p.Box(MatId.WoodD, new Vector3(cx, PlatH + 0.84f, counterZ + 0.06f), new Vector3(cw + 0.1f, 0.04f, 0.72f));
        // боковые стойки
        p.Box(MatId.WoodD, new Vector3(counterL, PlatH + 0.45f, counterZ + 0.26f), new Vector3(0.1f, 0.9f, 0.1f));
        p.Box(MatId.WoodD, new Vector3(counterR, PlatH + 0.45f, counterZ + 0.26f), new Vector3(0.1f, 0.9f, 0.1f));
    }

    // -------- этап 2: пустые стеллажи и поддоны --------
    void BuildRack(Part p)
    {
        float W = S.width, D = S.depth;
        p.At(Vector3.zero);
        float rz = -D / 2 + 0.3f;
        float ux = W / 2 - 0.4f;
        foreach (float x in new[] { -ux, 0f, ux })
            p.Box(MatId.WoodD, new Vector3(x, PlatH + 0.95f, rz), new Vector3(0.1f, 1.9f, 0.1f));
        foreach (float y in new[] { 0.45f, 1.05f, 1.65f })
        {
            p.Box(MatId.WoodL, new Vector3(0, PlatH + y, rz), new Vector3(2 * ux + 0.1f, 0.06f, 0.38f));
            p.Box(MatId.WoodM, new Vector3(0, PlatH + y - 0.06f, rz + 0.17f), new Vector3(2 * ux + 0.1f, 0.06f, 0.04f));
        }
        foreach (var pc in pallets) Pallet(p, pc);
    }

    void Pallet(Part p, Vector3 c)
    {
        p.At(c, 0);
        for (int i = -1; i <= 1; i++)
            p.Box(MatId.WoodD, new Vector3(i * 0.4f, PlatH + 0.04f, 0), new Vector3(0.1f, 0.08f, 0.8f));
        for (int i = -2; i <= 2; i++)
            p.Box(MatId.WoodM, new Vector3(0, PlatH + 0.095f, i * 0.17f), new Vector3(1.0f, 0.03f, 0.13f));
    }

    // -------- этап 3: крыша --------
    void BuildRoof(Part p)
    {
        float W = S.width, D = S.depth;
        const float ov = 0.4f, ovx = 0.35f, t = 0.14f;
        float halfD = D * 0.5f, run = halfD + ov;
        float ang = Mathf.Atan(Slope);
        float th = ang * Mathf.Rad2Deg;
        float L = run / Mathf.Cos(ang);
        float yc0 = PlatH + PostH + 0.10f - ov * Slope;
        float rise = run * Slope;
        float totW = W + 2 * ovx;
        int ns = Mathf.RoundToInt(totW / 0.3f);
        if (ns % 2 == 1) ns++;
        float sw = totW / ns;
        p.At(Vector3.zero);

        for (int side = -1; side <= 1; side += 2)
        {
            Quaternion q = Quaternion.Euler(side * th, 0, 0);
            Vector3 c = new Vector3(0, yc0 + rise * 0.5f, side * run * 0.5f);
            p.Box(MatId.Roof, c, new Vector3(totW, t, L), q);

            for (int i = 0; i < ns; i++)
            {
                float x = -totW / 2 + sw * (i + 0.5f);
                bool cream = i % 2 == 0;
                if (cream)
                {
                    p.Box(MatId.Cream, c + q * new Vector3(x, t / 2 + 0.008f, 0), new Vector3(sw, 0.016f, L), q);
                    p.Cyl(MatId.Cream, c + q * new Vector3(x, 0.008f, side * L / 2), sw / 2, t + 0.016f, q, 14);
                }
                else
                {
                    p.Cyl(MatId.Roof, c + q * new Vector3(x, 0, side * L / 2), sw / 2, t, q, 14);
                }
            }
            // торцевые доски
            for (int sx = -1; sx <= 1; sx += 2)
                p.Box(MatId.WoodL, c + q * new Vector3(sx * totW / 2, -0.01f, 0), new Vector3(0.1f, t + 0.06f, L + 0.1f), q);
        }

        // коньковая доска
        p.Box(MatId.WoodL, new Vector3(0, yc0 + rise + 0.06f, 0), new Vector3(totW + 0.1f, 0.12f, 0.24f));

        // фронтоны
        float yBase = PlatH + PostH - 0.02f;
        float yApex = yc0 + rise - 0.076f;
        for (int sx = -1; sx <= 1; sx += 2)
        {
            float x = sx * (totW / 2 - 0.06f);
            p.Tri2(MatId.WoodM, new Vector3(x, yBase, -halfD), new Vector3(x, yBase, halfD), new Vector3(x, yApex, 0));
        }
    }

    // -------- этап 3: вывеска --------
    void BuildSign(Part p)
    {
        float D = S.depth;
        float cx = (counterL + counterR) / 2;
        float beamFrontZ = D / 2 - 0.18f + 0.08f;
        float zs = beamFrontZ + 0.07f;
        float ys = PlatH + PostH - 0.74f;
        p.At(Vector3.zero);

        // верёвки
        for (int sx = -1; sx <= 1; sx += 2)
            p.Box(MatId.Metal, new Vector3(cx + sx * 0.55f, ys + 0.43f, zs - 0.02f), new Vector3(0.025f, 0.34f, 0.025f));

        // щит и рамка
        p.Box(MatId.WoodL, new Vector3(cx, ys, zs), new Vector3(1.5f, 0.62f, 0.07f));
        p.Box(MatId.WoodD, new Vector3(cx, ys + 0.31f, zs + 0.01f), new Vector3(1.56f, 0.06f, 0.09f));
        p.Box(MatId.WoodD, new Vector3(cx, ys - 0.31f, zs + 0.01f), new Vector3(1.56f, 0.06f, 0.09f));
        p.Box(MatId.WoodD, new Vector3(cx - 0.75f, ys, zs + 0.01f), new Vector3(0.06f, 0.68f, 0.09f));
        p.Box(MatId.WoodD, new Vector3(cx + 0.75f, ys, zs + 0.01f), new Vector3(0.06f, 0.68f, 0.09f));
        // синее поле
        float zf = zs + 0.045f;
        p.Box(MatId.Roof, new Vector3(cx, ys, zf), new Vector3(1.32f, 0.46f, 0.02f));

        // бревно (торцом к зрителю)
        float zl = zf + 0.01f;
        p.Cyl(MatId.WoodD, new Vector3(cx - 0.36f, ys, zl + 0.025f), 0.15f, 0.05f, AlongZ, 14);
        p.Cyl(MatId.WoodL, new Vector3(cx - 0.36f, ys, zl + 0.029f), 0.115f, 0.058f, AlongZ, 14);
        p.Cyl(MatId.WoodM, new Vector3(cx - 0.36f, ys, zl + 0.033f), 0.06f, 0.066f, AlongZ, 12);

        // «=»
        p.Box(MatId.Cream, new Vector3(cx, ys + 0.045f, zl + 0.015f), new Vector3(0.16f, 0.04f, 0.02f));
        p.Box(MatId.Cream, new Vector3(cx, ys - 0.045f, zl + 0.015f), new Vector3(0.16f, 0.04f, 0.02f));

        // монета
        p.Cyl(MatId.GoldD, new Vector3(cx + 0.36f, ys, zl + 0.025f), 0.16f, 0.05f, AlongZ, 16);
        p.Cyl(MatId.Gold, new Vector3(cx + 0.36f, ys, zl + 0.029f), 0.13f, 0.058f, AlongZ, 16);
        p.Cyl(MatId.GoldD, new Vector3(cx + 0.36f, ys, zl + 0.033f), 0.05f, 0.066f, AlongZ, 12);
    }

    // -------- этап 3: бревна на поддонах и полке --------
    void BuildLogs(Part p)
    {
        float D = S.depth;
        p.At(Vector3.zero);
        float top = PlatH + 0.11f;
        LogStack(p, new Vector3(pallets[0].x, top, pallets[0].z), 0.88f, 0.095f, new[] { 4, 3, 2 });
        LogStack(p, new Vector3(pallets[1].x, top, pallets[1].z), 0.88f, 0.095f, new[] { 4, 3 });
        // пара брёвен на средней полке
        float rz = -D / 2 + 0.3f;
        LogStack(p, new Vector3(-S.width / 2 + 1.3f, PlatH + 1.05f + 0.03f, rz), 0.7f, 0.085f, new[] { 2, 1 });
    }

    void LogStack(Part p, Vector3 b, float len, float r, int[] layers)
    {
        for (int l = 0; l < layers.Length; l++)
        {
            int n = layers[l];
            float y = b.y + r + l * r * 1.75f;
            for (int j = 0; j < n; j++)
            {
                float z = b.z + (j - (n - 1) / 2f) * 2f * r * 1.04f;
                float jl = len + (Rnd() - 0.5f) * 0.08f;
                float jx = (Rnd() - 0.5f) * 0.06f;
                Log(p, new Vector3(b.x + jx, y, z), jl, r);
            }
        }
    }

    void Log(Part p, Vector3 c, float len, float r)
    {
        p.Cyl(MatId.WoodD, c, r, len, AlongX, 12);
        p.Cyl(MatId.WoodL, c, r * 0.8f, len + 0.02f, AlongX, 12);
        p.Cyl(MatId.WoodM, c, r * 0.4f, len + 0.03f, AlongX, 8);
    }

    // -------- этап 3: реквизит на прилавке --------
    void BuildCounterProps(Part p)
    {
        float topY = PlatH + 0.95f;
        float zc = counterZ + 0.06f;
        float xl = counterL, ccx = (counterL + counterR) / 2;

        if (S.decChest)
        {
            p.At(new Vector3(xl + 0.5f, topY, zc - 0.12f), 8f);
            p.Box(MatId.WoodD, new Vector3(0, 0.10f, 0), new Vector3(0.5f, 0.2f, 0.3f));
            p.Cyl(MatId.WoodM, new Vector3(0, 0.20f, 0), 0.15f, 0.5f, AlongX, 12);
            foreach (float x in new[] { -0.15f, 0.15f })
            {
                p.Box(MatId.Metal, new Vector3(x, 0.10f, 0), new Vector3(0.05f, 0.205f, 0.31f));
                p.Cyl(MatId.Metal, new Vector3(x, 0.20f, 0), 0.155f, 0.05f, AlongX, 12);
            }
            p.Box(MatId.Gold, new Vector3(0, 0.18f, 0.16f), new Vector3(0.07f, 0.08f, 0.03f));

            // стопки монет
            p.At(new Vector3(xl + 0.95f, topY, zc + 0.18f), 0f);
            CoinStack(p, new Vector3(0, 0, 0), 5);
            CoinStack(p, new Vector3(0.11f, 0, 0.04f), 3);
            p.Cyl(MatId.Gold, new Vector3(-0.12f, 0.011f, 0.05f), 0.05f, 0.02f, null, 12);
        }

        if (S.decScales)
        {
            float sx = ccx + 0.15f, sz = zc - 0.05f;
            p.At(new Vector3(sx, topY, sz), 0f);
            p.Cyl(MatId.Metal, new Vector3(0, 0.02f, 0), 0.1f, 0.04f, null, 14);
            p.Cyl(MatId.Metal, new Vector3(0, 0.265f, 0), 0.018f, 0.45f, null, 8);
            p.Cyl(MatId.Gold, new Vector3(0, 0.50f, 0), 0.03f, 0.04f, AlongZ, 10);
            var q = Quaternion.Euler(0, 0, 4f);          // левая чаша ниже (бревно тяжелее)
            p.Box(MatId.Metal, new Vector3(0, 0.50f, 0), new Vector3(0.52f, 0.025f, 0.03f), q);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 end = new Vector3(0, 0.50f, 0) + q * new Vector3(s * 0.26f, 0, 0);
                float panY = end.y - 0.27f;
                float len = end.y - panY - 0.01f;
                p.Box(MatId.Metal, new Vector3(end.x, panY + 0.01f + len / 2, 0), new Vector3(0.01f, len, 0.01f));
                p.Cyl(MatId.Gold, new Vector3(end.x, panY, 0), 0.11f, 0.02f, null, 14);
                if (s < 0) p.Cyl(MatId.WoodD, new Vector3(end.x, panY + 0.05f, 0), 0.04f, 0.12f, AlongZ, 8);
                else p.Cyl(MatId.Gold, new Vector3(end.x, panY + 0.02f, 0), 0.05f, 0.015f, null, 12);
            }
        }

        if (S.decBook)
        {
            p.At(new Vector3(counterR - 0.4f, topY, zc + 0.05f), 14f);
            p.Box(MatId.Red, new Vector3(0, 0.01f, 0), new Vector3(0.3f, 0.02f, 0.22f));
            p.Box(MatId.Cream, new Vector3(0.005f, 0.035f, 0), new Vector3(0.28f, 0.03f, 0.2f));
            p.Box(MatId.Red, new Vector3(0, 0.06f, 0), new Vector3(0.3f, 0.02f, 0.22f));
            p.Box(MatId.Red, new Vector3(-0.14f, 0.035f, 0), new Vector3(0.025f, 0.07f, 0.22f));
            p.Box(MatId.Gold, new Vector3(0.05f, 0.075f, 0.02f), new Vector3(0.1f, 0.005f, 0.04f));
        }
    }

    void CoinStack(Part p, Vector3 lp, int count)
    {
        for (int i = 0; i < count; i++)
            p.Cyl(i % 2 == 0 ? MatId.Gold : MatId.GoldD,
                  lp + new Vector3((Rnd() - 0.5f) * 0.008f, 0.011f + i * 0.021f, (Rnd() - 0.5f) * 0.008f), 0.05f, 0.02f, null, 12);
    }
}
