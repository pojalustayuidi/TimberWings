#if UNITY_EDITOR
// =====================================================================================
//  WorldBuilder 1.0        Меню: Tools > BuilderSim > World Builder
//  Положи в:  Assets/_Project/Editor/WorldBuilder.cs
//
//  Что делает: лес, тропы, кусты, цветы, траву, грибы, камни, брёвна, светлячков, листопад,
//  туман. Всё лежит отдельными слоями внутри объекта «Location».
//  Работает сразу (рисует свои мультяшные модели), а если в группу положить префабы,
//  то ставит их вместо своих.
//  Сначала построй реку (RiverBuilder): локация использует её землю («Ground») и обходит воду.
// =====================================================================================
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static EditorAssets;

public class WorldBuilder : EditorWindow
{
    public enum Kind { None, RoundTree, Pine, Bush, Flower, Mushroom, Rock, Log, Stump, Grass }

    // ------------------------------------------------------------------ ДАННЫЕ
    [System.Serializable]
    public class Group
    {
        public string name = "Группа";
        public bool enabled = true;
        public Kind fallback = Kind.RoundTree;          // что рисовать, если префабов нет
        public GameObject[] prefabs = new GameObject[0];
        public int count = 100;
        public float scaleMin = 0.8f;
        public float scaleMax = 1.3f;
        public float radius = 1f;                       // сколько места занимает объект, м
        public float pathMargin = 2f;                   // отступ от тропы, м
        public bool keepClearing = true;                // true: на поляне не ставить
        public bool allowNearWater = false;            // true: можно у самой воды
        [Range(0f, 1f)] public float edgeBias = 0f;     // 1 = гуще к краю локации
        public int cluster = 1;                         // группами по N штук
        public float clusterRadius = 3f;
        public bool alignToNormal = false;
        public bool keepColliders = false;
        public float yOffset = 0f;
        [HideInInspector] public string[] prefabPaths = new string[0];
    }

    [System.Serializable]
    public class PathData
    {
        public List<Vector2> pts = new List<Vector2>();   // мировые X, Z
        public float width = 2.4f;
    }

    [System.Serializable]
    class Params
    {
        public int seed = 1;
        public float groundHalf = 60f;
        public string groundName = "Ground";
        public Vector2 clearCenter = Vector2.zero;
        public float clearRadius = 14f;
        public float areaRadius = 45f;
        public bool avoidColliders = true;
        public float avoidRadius = 3f;
        public List<PathData> paths = new List<PathData>();
        public int selPath = 0;
        public float pathStones = 4f;                     // камушков на 10 м тропы
        public Color dirt = new Color(0.76f, 0.60f, 0.38f);
        public Color dirtEdge = new Color(0.52f, 0.38f, 0.22f);
        public bool fog = true;
        public Color fogColor = new Color(0.72f, 0.88f, 0.85f);
        public float fogDensity = 0.006f;
        public bool tintLight = true;
        public Color lightColor = new Color(1f, 0.95f, 0.82f);
        public bool fireflies = true;
        public int fireflyCount = 70;
        public bool leaves = true;
        public int leafCount = 40;
    }

    [System.Serializable]
    class SaveData
    {
        public Params p = new Params();
        public List<Group> g = new List<Group>();
    }

    [SerializeField] Params P = new Params();
    [SerializeField] List<Group> groups = new List<Group>();
    [SerializeField] GameObject[] avoid = new GameObject[0];

    const string Dir = "Assets/_Game/Location";
    const string PrefsKey = "BuilderSim.WorldBuilder.v1";

    Vector2 scroll;
    bool fGen = true, fPath = true, fGroups = true, fAtm = true;
    float previewY = 0f;

    // состояние сборки
    bool flat;
    float flatY;
    float baseH;
    Occ occ = new Occ();
    readonly List<Vector3> excl = new List<Vector3>();   // x, z, радиус
    readonly List<Vector2> pathPts = new List<Vector2>();
    readonly List<float> pathHW = new List<float>();
    readonly Dictionary<string, Material> matCache = new Dictionary<string, Material>();
    readonly Dictionary<Kind, List<Variant>> vcache = new Dictionary<Kind, List<Variant>>();

    class Variant { public Mesh mesh; public Material[] mats; }

    [MenuItem("Tools/BuilderSim/World Builder")]
    static void Open()
    {
        var w = GetWindow<WorldBuilder>("World Builder");
        w.minSize = new Vector2(400, 560);
    }

    void OnEnable()
    {
        if (groups == null || groups.Count == 0) LoadAll();
        if (groups == null || groups.Count == 0) ResetGroups();
        if (P.paths.Count == 0) AddDefaultPath();
        SceneView.duringSceneGui += OnScene;
        RefreshBase();
    }

    void OnDisable() { SceneView.duringSceneGui -= OnScene; }

    // ------------------------------------------------------------------ СОХРАНЕНИЕ
    void SaveAll()
    {
        foreach (var g in groups)
        {
            var list = new List<string>();
            if (g.prefabs != null)
                foreach (var pf in g.prefabs)
                    if (pf != null) list.Add(AssetDatabase.GetAssetPath(pf));
            g.prefabPaths = list.ToArray();
        }
        var sd = new SaveData { p = P, g = groups };
        EditorPrefs.SetString(PrefsKey, JsonUtility.ToJson(sd));
    }

    void LoadAll()
    {
        if (!EditorPrefs.HasKey(PrefsKey)) return;
        try
        {
            var sd = JsonUtility.FromJson<SaveData>(EditorPrefs.GetString(PrefsKey));
            if (sd == null || sd.p == null || sd.g == null) return;
            P = sd.p;
            groups = sd.g;
            foreach (var g in groups)
            {
                if (g.prefabPaths == null || g.prefabPaths.Length == 0) continue;
                var list = new List<GameObject>();
                foreach (var path in g.prefabPaths)
                {
                    var o = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (o != null) list.Add(o);
                }
                g.prefabs = list.ToArray();
            }
        }
        catch { }
    }

    static Group MakeGroup(string name, Kind k, int count, float smin, float smax, float radius,
        float pathMargin, bool keepClearing, bool nearWater, float edge, int cluster, float clusterR, bool colliders, float yOff)
    {
        return new Group
        {
            name = name, fallback = k, count = count, scaleMin = smin, scaleMax = smax, radius = radius,
            pathMargin = pathMargin, keepClearing = keepClearing, allowNearWater = nearWater, edgeBias = edge,
            cluster = cluster, clusterRadius = clusterR, keepColliders = colliders, yOffset = yOff
        };
    }

    void ResetGroups()
    {
        groups = new List<Group>
        {
            MakeGroup("Деревья",        Kind.RoundTree, 240, 0.9f, 1.5f, 1.3f, 3.0f, true,  false, 0.6f, 1, 3f,   true,  0f),
            MakeGroup("Ёлки",           Kind.Pine,      220, 1.0f, 1.7f, 1.1f, 3.0f, true,  false, 0.8f, 1, 3f,   true,  0f),
            MakeGroup("Кусты",          Kind.Bush,      120, 0.8f, 1.3f, 0.8f, 1.4f, false, false, 0.3f, 3, 2.5f, false, 0f),
            MakeGroup("Цветы",          Kind.Flower,    260, 0.8f, 1.2f, 0.2f, 0.5f, false, true,  0.0f, 6, 1.6f, false, 0f),
            MakeGroup("Трава",          Kind.Grass,     500, 0.8f, 1.4f, 0.12f, 0.3f, false, true,  0.0f, 5, 1.2f, false, 0f),
            MakeGroup("Грибы",          Kind.Mushroom,  40,  0.8f, 1.4f, 0.2f, 1.0f, false, false, 0.4f, 3, 0.8f, false, 0f),
            MakeGroup("Камни",          Kind.Rock,      60,  0.6f, 1.8f, 0.7f, 1.3f, false, true,  0.4f, 2, 1.8f, false, -0.1f),
            MakeGroup("Брёвна",         Kind.Log,       14,  0.9f, 1.4f, 1.2f, 1.6f, false, false, 0.3f, 1, 3f,   false, 0f),
            MakeGroup("Пни",            Kind.Stump,     12,  0.8f, 1.3f, 0.5f, 1.5f, false, false, 0.3f, 1, 3f,   false, 0f)
        };
    }

    void AddDefaultPath()
    {
        var pd = new PathData();
        Vector2 c = P.clearCenter;
        float r = P.clearRadius;
        pd.pts.Add(c + new Vector2(0f, -r * 0.6f));
        pd.pts.Add(c + new Vector2(-3f, -r - 8f));
        pd.pts.Add(c + new Vector2(4f, -r - 18f));
        pd.pts.Add(c + new Vector2(-2f, -r - 30f));
        P.paths.Add(pd);
        P.selPath = P.paths.Count - 1;
    }

    // ------------------------------------------------------------------ ОКНО
    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        var so = new SerializedObject(this);
        so.Update();

        GUILayout.Space(4);
        EditorGUILayout.LabelField("World Builder 1.0", new GUIStyle(EditorStyles.boldLabel) { fontSize = 15 });
        EditorGUILayout.HelpBox(
            "1) Сначала построй реку (Tools > BuilderSim > River Builder).\n" +
            "2) Выдели палатку и нажми «Поляна = выделенный объект». Зелёный круг в Scene = поляна без деревьев, оранжевый = граница локации.\n" +
            "3) Тропы: двигай белые шарики в Scene или Shift+клик по земле, чтобы добавить точку.\n" +
            "4) В «Группах декора» можно закинуть свои префабы: они заменят мои модели.\n" +
            "5) Жми «Построить локацию». Можно перестраивать сколько угодно.", MessageType.Info);

        var old = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.55f, 0.95f, 0.55f);
        if (GUILayout.Button("Построить локацию", GUILayout.Height(36))) Build();
        GUI.backgroundColor = old;

        EditorGUI.BeginChangeCheck();

        // --- общее
        fGen = EditorGUILayout.Foldout(fGen, "Общее", true);
        if (fGen)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.BeginHorizontal();
            P.seed = EditorGUILayout.IntField("Seed (случайность)", P.seed);
            if (GUILayout.Button("Случайный", GUILayout.Width(80))) P.seed = Random.Range(1, 99999);
            EditorGUILayout.EndHorizontal();
            P.clearCenter = EditorGUILayout.Vector2Field("Центр поляны (X, Z)", P.clearCenter);
            P.clearRadius = EditorGUILayout.FloatField("Радиус поляны", P.clearRadius);
            if (GUILayout.Button("Поляна = выделенный объект (или Level1Tent)"))
            {
                var t = Selection.activeTransform;
                if (t == null) { var g = FindAny("Level1Tent"); if (g != null) t = g.transform; }
                if (t != null) { P.clearCenter = new Vector2(t.position.x, t.position.z); RefreshBase(); }
            }
            P.areaRadius = EditorGUILayout.FloatField("Радиус локации (от центра поляны)", P.areaRadius);
            P.groundName = EditorGUILayout.TextField("Имя земли (по нему ищу высоту)", P.groundName);
            P.avoidColliders = EditorGUILayout.Toggle("Обходить готовые объекты с коллайдером", P.avoidColliders);
            EditorGUILayout.PropertyField(so.FindProperty("avoid"), new GUIContent("Ещё обходить (палатка, заборы...)"), true);
            P.avoidRadius = EditorGUILayout.FloatField("   Радиус обхода для них", P.avoidRadius);
            EditorGUI.indentLevel--;
        }

        // --- тропы
        fPath = EditorGUILayout.Foldout(fPath, "Тропы", true);
        if (fPath)
        {
            EditorGUI.indentLevel++;
            if (GUILayout.Button("＋ Новая тропа")) AddDefaultPath();
            if (P.paths.Count > 0)
            {
                P.selPath = Mathf.Clamp(P.selPath, 0, P.paths.Count - 1);
                var names = new string[P.paths.Count];
                for (int i = 0; i < names.Length; i++) names[i] = "Тропа " + (i + 1) + " (" + P.paths[i].pts.Count + " точек)";
                P.selPath = EditorGUILayout.Popup("Выбрана", P.selPath, names);
                var pd = P.paths[P.selPath];
                pd.width = EditorGUILayout.Slider("Ширина", pd.width, 1f, 6f);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("＋ точка в центре вида"))
                {
                    Vector3 pv = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
                    pd.pts.Add(new Vector2(pv.x, pv.z));
                }
                if (GUILayout.Button("− последняя") && pd.pts.Count > 0) pd.pts.RemoveAt(pd.pts.Count - 1);
                if (GUILayout.Button("Удалить тропу"))
                {
                    P.paths.RemoveAt(P.selPath);
                    P.selPath = Mathf.Clamp(P.selPath, 0, Mathf.Max(0, P.paths.Count - 1));
                }
                EditorGUILayout.EndHorizontal();
                if (P.paths.Count > 0 && GUILayout.Button("Добавить в конец тропы точки моста (BridgePoint A и B)"))
                    AddBridgePoints(P.paths[P.selPath]);
            }
            P.pathStones = EditorGUILayout.Slider("Камушки по краям (на 10 м)", P.pathStones, 0f, 12f);
            P.dirt = EditorGUILayout.ColorField("Цвет тропы", P.dirt);
            P.dirtEdge = EditorGUILayout.ColorField("Цвет края тропы", P.dirtEdge);
            EditorGUI.indentLevel--;
        }

        // --- группы
        fGroups = EditorGUILayout.Foldout(fGroups, "Группы декора (лес, кусты, цветы, камни...)", true);
        if (fGroups)
        {
            EditorGUILayout.LabelField("В каждой группе: «Prefabs» = твои модели (пусто = мои), «Count» = сколько, «Edge Bias» = гуще к краю.", EditorStyles.miniLabel);
            EditorGUILayout.PropertyField(so.FindProperty("groups"), new GUIContent("Группы"), true);
            if (GUILayout.Button("Сбросить группы на стандартные")) { ResetGroups(); GUIUtility.ExitGUI(); }
        }

        // --- атмосфера
        fAtm = EditorGUILayout.Foldout(fAtm, "Атмосфера", true);
        if (fAtm)
        {
            EditorGUI.indentLevel++;
            P.fog = EditorGUILayout.Toggle("Туман", P.fog);
            P.fogColor = EditorGUILayout.ColorField("   Цвет тумана", P.fogColor);
            P.fogDensity = EditorGUILayout.Slider("   Густота", P.fogDensity, 0f, 0.03f);
            P.tintLight = EditorGUILayout.Toggle("Тёплый свет солнца", P.tintLight);
            P.lightColor = EditorGUILayout.ColorField("   Цвет света", P.lightColor);
            P.fireflies = EditorGUILayout.Toggle("Светлячки", P.fireflies);
            P.fireflyCount = EditorGUILayout.IntSlider("   Сколько", P.fireflyCount, 10, 300);
            P.leaves = EditorGUILayout.Toggle("Падающие листья", P.leaves);
            P.leafCount = EditorGUILayout.IntSlider("   Сколько", P.leafCount, 5, 200);
            EditorGUI.indentLevel--;
        }

        so.ApplyModifiedProperties();
        if (EditorGUI.EndChangeCheck())
        {
            SaveAll();
            SceneView.RepaintAll();
        }

        GUILayout.Space(6);
        GUI.backgroundColor = new Color(0.55f, 0.95f, 0.55f);
        if (GUILayout.Button("Построить локацию", GUILayout.Height(36))) Build();
        GUI.backgroundColor = new Color(1f, 0.65f, 0.6f);
        if (GUILayout.Button("Удалить локацию", GUILayout.Height(22))) DeleteLocation();
        GUI.backgroundColor = old;
        GUILayout.Space(6);
        EditorGUILayout.EndScrollView();
    }

    void AddBridgePoints(PathData pd)
    {
        var a = FindAny("BridgePoint_A");
        var b = FindAny("BridgePoint_B");
        if (a == null || b == null) { Debug.LogWarning("World: BridgePoint_A / BridgePoint_B не найдены. Сначала построй реку с мостом."); return; }
        Vector2 pa = new Vector2(a.transform.position.x, a.transform.position.z);
        Vector2 pb = new Vector2(b.transform.position.x, b.transform.position.z);
        if (pd.pts.Count > 0)
        {
            Vector2 last = pd.pts[pd.pts.Count - 1];
            if (Vector2.Distance(last, pb) < Vector2.Distance(last, pa)) { var t = pa; pa = pb; pb = t; }
        }
        pd.pts.Add(pa);
        pd.pts.Add(pb);
    }

    // ------------------------------------------------------------------ СЦЕНА
    void OnScene(SceneView sv)
    {
        Event e = Event.current;
        bool changed = false;

        Vector3 cc = new Vector3(P.clearCenter.x, previewY + 0.1f, P.clearCenter.y);
        Handles.color = new Color(0.3f, 1f, 0.3f, 1f);
        Handles.DrawWireDisc(cc, Vector3.up, P.clearRadius);
        Handles.Label(cc + Vector3.up * 1.5f, "Поляна (без деревьев)");
        Handles.color = new Color(1f, 0.6f, 0.2f, 0.9f);
        Handles.DrawWireDisc(cc, Vector3.up, P.areaRadius);
        Handles.Label(cc + new Vector3(P.areaRadius, 1.5f, 0f), "Граница локации");

        EditorGUI.BeginChangeCheck();
        float csz = HandleUtility.GetHandleSize(cc) * 0.15f;
        Handles.color = new Color(0.3f, 1f, 0.3f, 1f);
        Vector3 ncc = Handles.Slider2D(cc, Vector3.up, Vector3.right, Vector3.forward, csz, Handles.CubeHandleCap, Vector2.zero);
        if (EditorGUI.EndChangeCheck())
        {
            P.clearCenter = new Vector2(ncc.x, ncc.z);
            changed = true;
        }

        for (int pi = 0; pi < P.paths.Count; pi++)
        {
            var pd = P.paths[pi];
            if (pd.pts.Count >= 2)
            {
                var S = SamplePath(pd);
                var arr = new Vector3[S.Count];
                for (int i = 0; i < arr.Length; i++) arr[i] = new Vector3(S[i].x, previewY + 0.2f, S[i].y);
                Handles.color = pi == P.selPath ? new Color(1f, 0.9f, 0.2f) : new Color(1f, 1f, 1f, 0.7f);
                Handles.DrawAAPolyLine(pi == P.selPath ? 5f : 3f, arr);
            }
            for (int j = 0; j < pd.pts.Count; j++)
            {
                Vector3 w = new Vector3(pd.pts[j].x, previewY + 0.25f, pd.pts[j].y);
                float sz = HandleUtility.GetHandleSize(w) * 0.12f;
                Handles.color = pi == P.selPath ? new Color(1f, 0.9f, 0.2f) : Color.white;
                EditorGUI.BeginChangeCheck();
                Vector3 nw = Handles.Slider2D(w, Vector3.up, Vector3.right, Vector3.forward, sz, Handles.SphereHandleCap, Vector2.zero);
                if (EditorGUI.EndChangeCheck())
                {
                    pd.pts[j] = new Vector2(nw.x, nw.z);
                    P.selPath = pi;
                    changed = true;
                }
            }
        }

        // Shift + клик = новая точка выбранной тропы
        if (e.shift && e.type == EventType.Layout) HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        if (e.shift && !e.alt && e.type == EventType.MouseDown && e.button == 0 && P.paths.Count > 0)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            var plane = new Plane(Vector3.up, new Vector3(0f, previewY, 0f));
            float en;
            if (plane.Raycast(ray, out en))
            {
                Vector3 hp = ray.GetPoint(en);
                P.selPath = Mathf.Clamp(P.selPath, 0, P.paths.Count - 1);
                P.paths[P.selPath].pts.Add(new Vector2(hp.x, hp.z));
                changed = true;
                e.Use();
            }
        }

        Handles.BeginGUI();
        if (GUI.Button(new Rect(10, 10, 190, 32), "Построить локацию"))
            EditorApplication.delayCall += Build;
        GUI.Label(new Rect(10, 46, 360, 20), "Shift+клик по земле = новая точка тропы", EditorStyles.whiteMiniLabel);
        Handles.EndGUI();

        if (changed)
        {
            SaveAll();
            Repaint();
        }
    }

    void RefreshBase()
    {
        float h; Vector3 n;
        bool oldFlat = flat;
        flat = false;
        if (GroundAt(P.clearCenter.x, P.clearCenter.y, out h, out n)) previewY = h;
        flat = oldFlat;
    }

    // ------------------------------------------------------------------ СБОРКА
    void DeleteLocation()
    {
        var old = FindAny("Location");
        if (old != null && old.transform.parent == null) Undo.DestroyObjectImmediate(old);
    }

    void Build()
    {
        try
        {
            DeleteLocation();
            EnsureFolder("Assets/_Game");
            EnsureFolder(Dir);
            matCache.Clear();
            vcache.Clear();
            Physics.SyncTransforms();

            flat = false; flatY = 0f;
            float h0; Vector3 n0;
            if (!GroundAt(P.clearCenter.x, P.clearCenter.y, out h0, out n0))
            {
                Debug.LogWarning("World: не нашёл землю с коллайдером по имени «" + P.groundName + "». Строю на плоскости Y = 0. Построй реку или поправь имя земли.");
                flat = true; h0 = 0f;
            }
            baseH = h0;
            previewY = h0;
            occ = new Occ();
            BuildExcl();

            var rng = new System.Random(P.seed * 7919 + 11);
            var root = new GameObject("Location");
            Undo.RegisterCreatedObjectUndo(root, "Build location");

            EditorUtility.DisplayProgressBar("Location", "Тропы...", 0.05f);
            BuildPaths(root.transform, rng);

            int total = Mathf.Max(1, groups.Count);
            for (int i = 0; i < groups.Count; i++)
            {
                var g = groups[i];
                if (g == null || !g.enabled) continue;
                EditorUtility.DisplayProgressBar("Location", g.name + "...", 0.1f + 0.75f * i / total);
                var holder = BuildGroup(g, root.transform, rng);
                if (holder != null) MarkStatic(holder.gameObject);
            }

            EditorUtility.DisplayProgressBar("Location", "Атмосфера...", 0.92f);
            BuildAtmosphere(root.transform);

            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("World: локация готова.");
        }
        catch (System.Exception e)
        {
            Debug.LogError("World: ошибка сборки: " + e);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    void BuildExcl()
    {
        excl.Clear();
        if (avoid != null)
            foreach (var o in avoid)
                if (o != null) excl.Add(new Vector3(o.transform.position.x, o.transform.position.z, P.avoidRadius));
        string[] names = { "BridgePoint_A", "BridgePoint_B", "BridgeGap" };
        foreach (var n in names)
        {
            var g = FindAny(n);
            if (g != null) excl.Add(new Vector3(g.transform.position.x, g.transform.position.z, 3.5f));
        }
    }

    // ------------------------------------------------------------------ ЗЕМЛЯ / ПРОВЕРКИ
    bool GroundAt(float x, float z, out float h, out Vector3 n)
    {
        h = 0f; n = Vector3.up;
        if (flat) { h = flatY; return true; }
        var hits = Physics.RaycastAll(new Vector3(x, 300f, z), Vector3.down, 600f, ~0, QueryTriggerInteraction.Ignore);
        bool ok = false;
        float best = float.MinValue;
        for (int i = 0; i < hits.Length; i++)
        {
            if (!hits[i].collider.gameObject.name.Contains(P.groundName)) continue;
            if (hits[i].point.y > best) { best = hits[i].point.y; n = hits[i].normal; ok = true; }
        }
        h = best;
        return ok;
    }

    bool NearWater(float x, float z)
    {
        for (int i = 0; i < 8; i++)
        {
            float a = i / 8f * Mathf.PI * 2f;
            float h; Vector3 n;
            if (GroundAt(x + Mathf.Cos(a) * 2.2f, z + Mathf.Sin(a) * 2.2f, out h, out n))
                if (baseH - h > 0.3f) return true;
        }
        return false;
    }

    float NearPath(float x, float z)
    {
        float best = 999f;
        for (int i = 0; i < pathPts.Count; i++)
        {
            float d = Vector2.Distance(pathPts[i], new Vector2(x, z)) - pathHW[i];
            if (d < best) best = d;
        }
        return best;
    }

    bool Candidate(Group g, float x, float z, float r, out float h, out Vector3 n)
    {
        h = 0f; n = Vector3.up;
        Vector2 p = new Vector2(x, z);
        if (Vector2.Distance(p, P.clearCenter) > P.areaRadius) return false;
        float lim = P.groundHalf - 1.5f;
        if (Mathf.Abs(x) > lim || Mathf.Abs(z) > lim) return false;
        if (g.keepClearing && Vector2.Distance(p, P.clearCenter) < P.clearRadius) return false;
        if (NearPath(x, z) < g.pathMargin) return false;
        for (int i = 0; i < excl.Count; i++)
            if (Vector2.Distance(p, new Vector2(excl[i].x, excl[i].y)) < excl[i].z + r) return false;
        if (!occ.Free(x, z, r)) return false;
        if (!GroundAt(x, z, out h, out n)) return false;
        if (baseH - h > (g.allowNearWater ? 0.3f : 0.1f)) return false;
        if (g.fallback != Kind.Rock && n.y < 0.8f) return false;
        if (!g.allowNearWater && NearWater(x, z)) return false;
        if (P.avoidColliders)
        {
            float rr = Mathf.Clamp(r, 0.15f, 1.2f);
            if (Physics.CheckSphere(new Vector3(x, h + rr + 0.2f, z), rr, ~0, QueryTriggerInteraction.Ignore)) return false;
        }
        return true;
    }

    // ------------------------------------------------------------------ ТРОПЫ
    static Vector2 CR(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    List<Vector2> SamplePath(PathData pd)
    {
        var res = new List<Vector2>();
        int n = pd.pts.Count;
        if (n < 2) return res;
        for (int i = 0; i < n - 1; i++)
        {
            Vector2 p0 = pd.pts[Mathf.Max(i - 1, 0)], p1 = pd.pts[i], p2 = pd.pts[i + 1], p3 = pd.pts[Mathf.Min(i + 2, n - 1)];
            float len = Vector2.Distance(p1, p2);
            int steps = Mathf.Max(2, Mathf.CeilToInt(len / 0.5f));
            for (int s = 0; s < steps; s++) res.Add(CR(p0, p1, p2, p3, s / (float)steps));
        }
        res.Add(pd.pts[n - 1]);
        return res;
    }

    void BuildPaths(Transform root, System.Random rng)
    {
        pathPts.Clear(); pathHW.Clear();
        foreach (var pd in P.paths)
        {
            var S = SamplePath(pd);
            foreach (var s in S) { pathPts.Add(s); pathHW.Add(pd.width * 0.55f); }
        }

        var holder = new GameObject("Paths").transform;
        holder.SetParent(root, false);
        Material mat = MakePathMaterial();
        for (int i = 0; i < P.paths.Count; i++) BuildPathMesh(holder, P.paths[i], i, mat);

        // камушки по краям
        if (P.pathStones > 0.01f)
        {
            var vars = GetVariants(Kind.Rock);
            var stones = new GameObject("PathStones").transform;
            stones.SetParent(holder, false);
            foreach (var pd in P.paths)
            {
                var S = SamplePath(pd);
                float acc = 0f, next = 0f;
                for (int i = 1; i < S.Count - 1; i++)
                {
                    acc += Vector2.Distance(S[i], S[i - 1]);
                    if (acc < next) continue;
                    next = acc + 10f / P.pathStones * (0.5f + (float)rng.NextDouble());
                    Vector2 t = (S[i + 1] - S[i - 1]).normalized;
                    Vector2 perp = new Vector2(t.y, -t.x);
                    float side = rng.Next(2) == 0 ? -1f : 1f;
                    Vector2 q = S[i] + perp * side * (pd.width * 0.5f + 0.15f + (float)rng.NextDouble() * 0.5f);
                    float sc = 0.25f + (float)rng.NextDouble() * 0.35f;
                    float h; Vector3 nn;
                    if (!GroundAt(q.x, q.y, out h, out nn)) continue;
                    if (baseH - h > 0.1f) continue;
                    if (!occ.Free(q.x, q.y, sc * 0.5f)) continue;
                    var v = vars[rng.Next(vars.Count)];
                    var go = new GameObject("Stone");
                    go.transform.SetParent(stones, false);
                    go.AddComponent<MeshFilter>().sharedMesh = v.mesh;
                    go.AddComponent<MeshRenderer>().sharedMaterials = v.mats;
                    go.transform.position = new Vector3(q.x, h - 0.03f, q.y);
                    go.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                    go.transform.localScale = new Vector3(sc * 1.2f, sc * 0.8f, sc);
                    occ.Add(q.x, q.y, sc * 0.5f);
                }
            }
            MarkStatic(stones.gameObject);
        }
    }

    void BuildPathMesh(Transform parent, PathData pd, int idx, Material mat)
    {
        var S = SamplePath(pd);
        if (S.Count < 2) return;
        const int cols = 5;
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var valid = new List<bool>();
        float acc = 0f;
        for (int i = 0; i < S.Count; i++)
        {
            Vector2 a = S[Mathf.Max(i - 1, 0)], b = S[Mathf.Min(i + 1, S.Count - 1)];
            Vector2 t = (b - a).normalized;
            Vector2 perp = new Vector2(t.y, -t.x);
            if (i > 0) acc += Vector2.Distance(S[i], S[i - 1]);
            float hw = pd.width * 0.5f * (1f + 0.1f * Mathf.Sin(acc * 0.45f + idx)) * 1.1f;
            bool ok = true;
            for (int j = 0; j < cols; j++)
            {
                float lat = (j / (cols - 1f) * 2f - 1f) * hw;
                Vector2 q = S[i] + perp * lat;
                float h; Vector3 n;
                if (!GroundAt(q.x, q.y, out h, out n) || baseH - h > 0.12f) { ok = false; h = baseH; }
                verts.Add(new Vector3(q.x, h + 0.035f, q.y));
                uvs.Add(new Vector2(j / (cols - 1f), acc / 4f));
            }
            valid.Add(ok);
        }
        var tris = new List<int>();
        for (int i = 0; i < S.Count - 1; i++)
        {
            if (!valid[i] || !valid[i + 1]) continue;
            for (int j = 0; j < cols - 1; j++)
            {
                int a = i * cols + j, b = a + 1, c = a + cols, d = c + 1;
                AddUp(tris, verts, a, b, c);
                AddUp(tris, verts, b, d, c);
            }
        }
        if (tris.Count == 0) return;
        var mesh = new Mesh { name = "Path_" + idx, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh = SaveAsset(mesh, Dir + "/Path_" + idx + ".asset");

        var go = new GameObject("Path_" + (idx + 1));
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    static void AddUp(List<int> tris, List<Vector3> v, int a, int b, int c)
    {
        if (Vector3.Cross(v[b] - v[a], v[c] - v[a]).y < 0f) { int t = b; b = c; c = t; }
        tris.Add(a); tris.Add(b); tris.Add(c);
    }

    Material MakePathMaterial()
    {
        const int res = 128;
        var px = new Color32[res * res];
        for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float u = x / (float)(res - 1), v = y / (float)res;
                float e = Mathf.Abs(u - 0.5f) * 2f;
                float a2 = v * Mathf.PI * 2f;
                float wob = 0.06f * Mathf.Sin(a2 * 2f) + 0.045f * Mathf.Sin(a2 * 5f + 1f) + 0.03f * Mathf.Sin(a2 * 9f + 2f);
                float limit = 0.86f + wob;
                float n = Mathf.PerlinNoise(u * 5f + Mathf.Cos(a2) * 2f + 10f, Mathf.Sin(a2) * 2f + 10f);
                float n2 = Mathf.PerlinNoise(u * 18f + Mathf.Cos(a2) * 6f + 40f, Mathf.Sin(a2) * 6f + 40f);
                Color c = P.dirt * (0.9f + n * 0.2f);
                if (n2 > 0.72f) c *= 0.85f;
                float rim = Smooth((e - (limit - 0.16f)) / 0.16f);
                c = Color.Lerp(c, P.dirtEdge, rim * 0.85f);
                c.a = e < limit && e < 0.97f ? 1f : 0f;
                px[y * res + x] = (Color32)c;
            }
        var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        tex.SetPixels32(px);
        tex.Apply();
        Texture2D saved = SaveTex(tex, "Path", true, TextureWrapMode.Repeat, true);

        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        var m = new Material(sh) { name = "Loc_Path" };
        m.mainTexture = saved;
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", saved);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0f);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0f);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        if (m.HasProperty("_AlphaClip")) m.SetFloat("_AlphaClip", 1f);
        if (m.HasProperty("_Cutoff")) m.SetFloat("_Cutoff", 0.5f);
        m.EnableKeyword("_ALPHATEST_ON");
        m.SetOverrideTag("RenderType", "TransparentCutout");
        m.renderQueue = 2450;
        return SaveAsset(m, Dir + "/Loc_Path.mat");
    }

    // ------------------------------------------------------------------ ГРУППЫ
    GameObject[] ValidPrefabs(Group g)
    {
        var l = new List<GameObject>();
        if (g.prefabs != null)
            foreach (var p in g.prefabs) if (p != null) l.Add(p);
        return l.ToArray();
    }

    Transform BuildGroup(Group g, Transform root, System.Random rng)
    {
        GameObject[] pf = ValidPrefabs(g);
        List<Variant> vars = null;
        if (pf.Length == 0)
        {
            if (g.fallback == Kind.None) return null;
            vars = GetVariants(g.fallback);
        }
        var holder = new GameObject(g.name).transform;
        holder.SetParent(root, false);

        int placed = 0, tries = 0, maxTries = Mathf.Max(200, g.count * 80), left = 0;
        Vector2 cc = Vector2.zero;
        while (placed < g.count && tries++ < maxTries)
        {
            float x, z;
            bool isCenter = false;
            if (left > 0)
            {
                x = cc.x + Gauss(rng) * g.clusterRadius * 0.5f;
                z = cc.y + Gauss(rng) * g.clusterRadius * 0.5f;
                left--;
            }
            else
            {
                float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                float rad = P.areaRadius * Mathf.Sqrt((float)rng.NextDouble());
                x = P.clearCenter.x + Mathf.Cos(ang) * rad;
                z = P.clearCenter.y + Mathf.Sin(ang) * rad;
                isCenter = true;
                float d = rad / Mathf.Max(1f, P.areaRadius);
                float e = Smooth((d - 0.3f) / 0.7f);
                float prob = Mathf.Lerp(1f, Mathf.Max(0.04f, e), g.edgeBias);
                if (rng.NextDouble() > prob) continue;
            }

            float s = Mathf.Lerp(g.scaleMin, g.scaleMax, (float)rng.NextDouble());
            float r = g.radius * s;
            float h; Vector3 n;
            if (!Candidate(g, x, z, r, out h, out n)) continue;
            if (isCenter) { cc = new Vector2(x, z); left = Mathf.Max(0, g.cluster - 1); }

            GameObject go;
            Quaternion baseRot = Quaternion.identity;
            if (pf.Length > 0)
            {
                var prefab = pf[rng.Next(pf.Length)];
                go = PrefabUtility.InstantiatePrefab(prefab, holder) as GameObject;
                if (go == null) continue;
                if (!g.keepColliders)
                    foreach (var c in go.GetComponentsInChildren<Collider>()) DestroyImmediate(c);
                go.transform.localScale = prefab.transform.localScale * s;
                baseRot = prefab.transform.rotation;
            }
            else
            {
                var v = vars[rng.Next(vars.Count)];
                go = new GameObject(g.name + "_" + placed);
                go.transform.SetParent(holder, false);
                go.AddComponent<MeshFilter>().sharedMesh = v.mesh;
                go.AddComponent<MeshRenderer>().sharedMaterials = v.mats;
                go.transform.localScale = new Vector3(s, s * (0.9f + (float)rng.NextDouble() * 0.25f), s);
                if (g.keepColliders) AddFallbackCollider(go, g.fallback, v.mesh);
            }
            float yaw = (float)rng.NextDouble() * 360f;
            Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
            if (g.alignToNormal) rot = Quaternion.FromToRotation(Vector3.up, n) * rot;
            go.transform.SetPositionAndRotation(new Vector3(x, h + g.yOffset, z), rot * baseRot);
            occ.Add(x, z, r);
            placed++;
        }
        if (placed < g.count) Debug.Log("World: «" + g.name + "» поставлено " + placed + " из " + g.count + " (не хватило места, увеличь радиус локации или уменьши радиус/отступы).");
        return holder;
    }

    void AddFallbackCollider(GameObject go, Kind k, Mesh mesh)
    {
        if (k == Kind.RoundTree || k == Kind.Pine)
        {
            var c = go.AddComponent<CapsuleCollider>();
            c.radius = 0.3f; c.height = 2.5f; c.center = new Vector3(0f, 1.25f, 0f);
        }
        else
        {
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;
            mc.convex = true;
        }
    }

    static float Gauss(System.Random r)
    {
        double u1 = 1.0 - r.NextDouble(), u2 = r.NextDouble();
        return (float)(System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Cos(2.0 * System.Math.PI * u2));
    }

    static float Smooth(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }

    // сетка занятых мест
    class Occ
    {
        const float C = 2f;
        readonly Dictionary<long, List<Vector3>> g = new Dictionary<long, List<Vector3>>();
        static long Key(int cx, int cz) { return ((long)cx << 32) ^ (long)(uint)cz; }
        public bool Free(float x, float z, float r)
        {
            int range = Mathf.CeilToInt((r + 3f) / C);
            int cx = Mathf.FloorToInt(x / C), cz = Mathf.FloorToInt(z / C);
            for (int dx = -range; dx <= range; dx++)
                for (int dz = -range; dz <= range; dz++)
                {
                    List<Vector3> l;
                    if (!g.TryGetValue(Key(cx + dx, cz + dz), out l)) continue;
                    for (int i = 0; i < l.Count; i++)
                    {
                        float ddx = l[i].x - x, ddz = l[i].y - z, rr = l[i].z + r;
                        if (ddx * ddx + ddz * ddz < rr * rr) return false;
                    }
                }
            return true;
        }
        public void Add(float x, float z, float r)
        {
            long k = Key(Mathf.FloorToInt(x / C), Mathf.FloorToInt(z / C));
            List<Vector3> l;
            if (!g.TryGetValue(k, out l)) { l = new List<Vector3>(); g[k] = l; }
            l.Add(new Vector3(x, z, r));
        }
    }

    // ------------------------------------------------------------------ СВОИ МОДЕЛИ (мультяшные, плоские грани)
    class MB2
    {
        public List<Vector3> v = new List<Vector3>();
        public List<List<int>> subs = new List<List<int>>();
        static Vector3[] icoV;
        static int[] icoF;

        public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 outward, int sub)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f) { Vector3 t = b; b = c; c = t; }
            while (subs.Count <= sub) subs.Add(new List<int>());
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c);
            subs[sub].Add(i); subs[sub].Add(i + 1); subs[sub].Add(i + 2);
        }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name };
            m.indexFormat = v.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            m.SetVertices(v);
            m.subMeshCount = subs.Count;
            for (int i = 0; i < subs.Count; i++) m.SetTriangles(subs[i], i);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        static void InitIco()
        {
            if (icoV != null) return;
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            icoV = new[]
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
            };
            for (int i = 0; i < icoV.Length; i++) icoV[i] = icoV[i].normalized;
            icoF = new[]
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
            };
        }

        static Vector3 BP(Vector3 u, Vector3 center, Vector3 radii, int seed)
        {
            float n = Mathf.PerlinNoise(u.x * 1.9f + seed * 7.3f + 5f, u.z * 1.9f + u.y * 1.4f + 5f);
            Vector3 q = u * (0.8f + n * 0.45f);
            return center + Vector3.Scale(q, radii);
        }

        void BlobTri(Vector3 a, Vector3 b, Vector3 c, Vector3 center, Vector3 radii, int seed, int sub)
        {
            Vector3 da = BP(a, center, radii, seed), db = BP(b, center, radii, seed), dc = BP(c, center, radii, seed);
            Tri(da, db, dc, (da + db + dc) / 3f - center, sub);
        }

        public void Blob(Vector3 center, Vector3 radii, int seed, int sub)
        {
            InitIco();
            for (int i = 0; i < icoF.Length; i += 3)
            {
                Vector3 a = icoV[icoF[i]], b = icoV[icoF[i + 1]], c = icoV[icoF[i + 2]];
                Vector3 ab = ((a + b) * 0.5f).normalized, bc = ((b + c) * 0.5f).normalized, ca = ((c + a) * 0.5f).normalized;
                BlobTri(a, ab, ca, center, radii, seed, sub);
                BlobTri(b, bc, ab, center, radii, seed, sub);
                BlobTri(c, ca, bc, center, radii, seed, sub);
                BlobTri(ab, bc, ca, center, radii, seed, sub);
            }
        }

        public void Cone(Vector3 baseC, float r, float h, int sides, int sub, int seed)
        {
            var rng = new System.Random(seed);
            Vector3 tip = baseC + Vector3.up * h;
            var ring = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = i / (float)sides * Mathf.PI * 2f;
                float rr = r * (0.88f + (float)rng.NextDouble() * 0.24f);
                ring[i] = baseC + new Vector3(Mathf.Cos(a) * rr, 0f, Mathf.Sin(a) * rr);
            }
            for (int i = 0; i < sides; i++)
            {
                Vector3 p0 = ring[i], p1 = ring[(i + 1) % sides];
                Tri(p0, p1, tip, (p0 + p1) * 0.5f - baseC + Vector3.up * 0.4f, sub);
                Tri(baseC, p0, p1, Vector3.down, sub);
            }
        }

        public void Cyl(Vector3 c0, float r0, Vector3 c1, float r1, int sides, int sub, int capSub, bool capTop, bool capBot)
        {
            Vector3 axis = (c1 - c0).normalized;
            Vector3 u = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 w = Vector3.Cross(axis, u);
            var a = new Vector3[sides];
            var b = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float t = i / (float)sides * Mathf.PI * 2f;
                Vector3 d = u * Mathf.Cos(t) + w * Mathf.Sin(t);
                a[i] = c0 + d * r0;
                b[i] = c1 + d * r1;
            }
            int cs = capSub < 0 ? sub : capSub;
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                Vector3 mid = (a[i] + a[j] + b[i] + b[j]) * 0.25f;
                Vector3 outw = mid - (c0 + axis * Vector3.Dot(mid - c0, axis));
                Tri(a[i], a[j], b[j], outw, sub);
                Tri(a[i], b[j], b[i], outw, sub);
                if (capTop) Tri(c1, b[i], b[j], axis, cs);
                if (capBot) Tri(c0, a[i], a[j], -axis, cs);
            }
        }

        public void Blade(Vector3 basePos, float width, Vector3 tip)
        {
            Vector3 side = new Vector3(0.0f, 0f, width);
            Vector3 p0 = basePos - side, p1 = basePos + side;
            Vector3 nrm = Vector3.Cross(p1 - p0, tip - p0);
            Tri(p0, p1, tip, nrm, 0);
            Tri(p0, p1, tip, -nrm, 0);
        }
    }

    Material M(string name, Color c, float smooth)
    {
        Material m;
        if (matCache.TryGetValue(name, out m) && m != null) return m;
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        m = new Material(sh) { name = "Loc_" + name };
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        m.enableInstancing = true;
        m = SaveAsset(m, Dir + "/Loc_" + name + ".mat");
        matCache[name] = m;
        return m;
    }

    Variant Make(MB2 mb, string name, Material[] mats)
    {
        var mesh = SaveAsset(mb.ToMesh("Loc_" + name), Dir + "/Loc_" + name + ".asset");
        return new Variant { mesh = mesh, mats = mats };
    }

    List<Variant> GetVariants(Kind k)
    {
        List<Variant> l;
        if (vcache.TryGetValue(k, out l)) return l;
        l = new List<Variant>();
        switch (k)
        {
            case Kind.RoundTree:
            {
                Color[] lc = { new Color(.34f, .70f, .26f), new Color(.43f, .78f, .30f), new Color(.27f, .62f, .30f) };
                Material bark = M("Bark", new Color(.45f, .30f, .18f), 0.05f);
                for (int i = 0; i < 3; i++)
                {
                    var rr = new System.Random(40 + i);
                    var mb = new MB2();
                    float h = 1.6f + (float)rr.NextDouble() * 0.5f;
                    mb.Cyl(Vector3.zero, 0.26f, new Vector3(0.05f, h, 0.03f), 0.15f, 6, 0, 0, false, false);
                    mb.Blob(new Vector3(0f, h + 0.9f, 0f), new Vector3(1.35f, 1.15f, 1.35f), i * 3 + 1, 1);
                    for (int q = 0; q < 3; q++)
                    {
                        float a = (float)rr.NextDouble() * 6.28f;
                        float d = 0.7f + (float)rr.NextDouble() * 0.3f;
                        mb.Blob(new Vector3(Mathf.Cos(a) * d, h + 0.35f + (float)rr.NextDouble() * 0.9f, Mathf.Sin(a) * d),
                            new Vector3(0.8f, 0.7f, 0.8f), i * 5 + q + 2, 1);
                    }
                    l.Add(Make(mb, "Tree" + i, new[] { bark, M("Leaf" + i, lc[i], 0.1f) }));
                }
                break;
            }
            case Kind.Pine:
            {
                Color[] lc = { new Color(.16f, .45f, .30f), new Color(.20f, .52f, .32f), new Color(.14f, .40f, .34f) };
                Material bark = M("Bark", new Color(.45f, .30f, .18f), 0.05f);
                for (int i = 0; i < 3; i++)
                {
                    var mb = new MB2();
                    mb.Cyl(Vector3.zero, 0.22f, new Vector3(0f, 1.1f, 0f), 0.14f, 6, 0, 0, false, false);
                    mb.Cone(new Vector3(0f, 0.7f, 0f), 1.25f, 1.6f, 8, 1, i * 11 + 1);
                    mb.Cone(new Vector3(0f, 1.5f, 0f), 1.0f, 1.5f, 8, 1, i * 11 + 2);
                    mb.Cone(new Vector3(0f, 2.3f, 0f), 0.78f, 1.4f, 8, 1, i * 11 + 3);
                    mb.Cone(new Vector3(0f, 3.0f, 0f), 0.52f, 1.3f, 8, 1, i * 11 + 4);
                    l.Add(Make(mb, "Pine" + i, new[] { bark, M("PineLeaf" + i, lc[i], 0.1f) }));
                }
                break;
            }
            case Kind.Bush:
            {
                Color[] lc = { new Color(.32f, .66f, .25f), new Color(.40f, .74f, .28f), new Color(.26f, .58f, .28f) };
                for (int i = 0; i < 3; i++)
                {
                    var mb = new MB2();
                    mb.Blob(new Vector3(0f, 0.4f, 0f), new Vector3(0.8f, 0.55f, 0.8f), i + 1, 0);
                    mb.Blob(new Vector3(0.5f, 0.3f, 0.2f), new Vector3(0.5f, 0.4f, 0.5f), i + 4, 0);
                    mb.Blob(new Vector3(-0.45f, 0.3f, -0.15f), new Vector3(0.5f, 0.38f, 0.5f), i + 7, 0);
                    l.Add(Make(mb, "Bush" + i, new[] { M("BushLeaf" + i, lc[i], 0.1f) }));
                }
                break;
            }
            case Kind.Flower:
            {
                var mb = new MB2();
                mb.Cyl(Vector3.zero, 0.018f, new Vector3(0f, 0.4f, 0f), 0.012f, 4, 0, 0, false, false);
                mb.Blob(new Vector3(0f, 0.42f, 0f), new Vector3(0.13f, 0.045f, 0.13f), 3, 1);
                mb.Blob(new Vector3(0f, 0.455f, 0f), new Vector3(0.05f, 0.04f, 0.05f), 5, 2);
                Mesh mesh = SaveAsset(mb.ToMesh("Loc_Flower"), Dir + "/Loc_Flower.asset");
                Material stem = M("FlowerStem", new Color(.30f, .62f, .24f), 0.05f);
                Material mid = M("FlowerMid", new Color(1f, .82f, .2f), 0.05f);
                Color[] pc = { new Color(1f, .55f, .75f), new Color(1f, .92f, .35f), new Color(1f, 1f, 1f), new Color(.55f, .75f, 1f), new Color(1f, .5f, .35f) };
                for (int i = 0; i < pc.Length; i++)
                    l.Add(new Variant { mesh = mesh, mats = new[] { stem, M("Petal" + i, pc[i], 0.1f), mid } });
                break;
            }
            case Kind.Mushroom:
            {
                var mb = new MB2();
                mb.Cyl(Vector3.zero, 0.07f, new Vector3(0f, 0.22f, 0f), 0.05f, 6, 0, 0, false, false);
                mb.Blob(new Vector3(0f, 0.27f, 0f), new Vector3(0.21f, 0.12f, 0.21f), 9, 1);
                Mesh mesh = SaveAsset(mb.ToMesh("Loc_Mushroom"), Dir + "/Loc_Mushroom.asset");
                Material stalk = M("MushStalk", new Color(.95f, .9f, .78f), 0.05f);
                Color[] cc = { new Color(.9f, .22f, .2f), new Color(.95f, .6f, .2f), new Color(.7f, .35f, .85f) };
                for (int i = 0; i < cc.Length; i++)
                    l.Add(new Variant { mesh = mesh, mats = new[] { stalk, M("MushCap" + i, cc[i], 0.2f) } });
                break;
            }
            case Kind.Rock:
            {
                Color[] rc = { new Color(.62f, .60f, .68f), new Color(.55f, .55f, .62f), new Color(.70f, .66f, .64f) };
                for (int i = 0; i < 5; i++)
                {
                    var mb = new MB2();
                    mb.Blob(new Vector3(0f, 0.18f, 0f), new Vector3(0.55f + 0.05f * i, 0.4f, 0.5f), i + 20, 0);
                    l.Add(Make(mb, "Rock" + i, new[] { M("RockC" + (i % 3), rc[i % 3], 0.05f) }));
                }
                break;
            }
            case Kind.Log:
            {
                Material bark = M("Bark", new Color(.45f, .30f, .18f), 0.05f);
                Material ring = M("WoodRing", new Color(.85f, .68f, .42f), 0.05f);
                for (int i = 0; i < 2; i++)
                {
                    var mb = new MB2();
                    float len = 0.9f + 0.25f * i;
                    mb.Cyl(new Vector3(-len, 0.22f, 0f), 0.22f, new Vector3(len, 0.22f, 0f), 0.2f, 8, 0, 1, true, true);
                    l.Add(Make(mb, "Log" + i, new[] { bark, ring }));
                }
                break;
            }
            case Kind.Stump:
            {
                Material bark = M("Bark", new Color(.45f, .30f, .18f), 0.05f);
                Material ring = M("WoodRing", new Color(.85f, .68f, .42f), 0.05f);
                var mb = new MB2();
                mb.Cyl(Vector3.zero, 0.34f, new Vector3(0f, 0.45f, 0f), 0.27f, 8, 0, 1, true, false);
                l.Add(Make(mb, "Stump", new[] { bark, ring }));
                break;
            }
            case Kind.Grass:
            {
                Color[] gc = { new Color(.42f, .78f, .28f), new Color(.34f, .68f, .24f) };
                for (int i = 0; i < 2; i++)
                {
                    var rr = new System.Random(70 + i);
                    var mb = new MB2();
                    for (int q = 0; q < 8; q++)
                    {
                        float a = (float)rr.NextDouble() * 6.28f;
                        float d = (float)rr.NextDouble() * 0.12f;
                        Vector3 b = new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                        float h = 0.3f + (float)rr.NextDouble() * 0.35f;
                        float la = (float)rr.NextDouble() * 6.28f;
                        Vector3 tip = b + new Vector3(Mathf.Cos(la) * 0.12f, h, Mathf.Sin(la) * 0.12f);
                        mb.Blade(b, 0.035f, tip);
                    }
                    l.Add(Make(mb, "Grass" + i, new[] { M("GrassC" + i, gc[i], 0.0f) }));
                }
                break;
            }
        }
        vcache[k] = l;
        return l;
    }

    // ------------------------------------------------------------------ АТМОСФЕРА
    void BuildAtmosphere(Transform root)
    {
        if (P.fog)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = P.fogColor;
            RenderSettings.fogDensity = P.fogDensity;
        }
        if (P.tintLight)
        {
            Light sun = RenderSettings.sun;
            if (sun == null)
            {
                foreach (var li in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (li.type == LightType.Directional) { sun = li; break; }
            }
            if (sun != null)
            {
                Undo.RecordObject(sun, "Tint sun");
                sun.color = P.lightColor;
                sun.shadows = LightShadows.Soft;
            }
        }

        var atm = new GameObject("Atmosphere").transform;
        atm.SetParent(root, false);
        Vector3 c = new Vector3(P.clearCenter.x, baseH, P.clearCenter.y);

        if (P.fireflies)
        {
            Texture2D glow = MakeGlowTexture();
            Shader sh = Shader.Find("Mobile/Particles/Additive");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            var mat = new Material(sh) { name = "Loc_Firefly" };
            mat.mainTexture = glow;
            mat = SaveAsset(mat, Dir + "/Loc_Firefly.mat");

            var go = new GameObject("Fireflies");
            go.transform.SetParent(atm, false);
            go.transform.position = c + Vector3.up * 1.8f;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 8f;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.26f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.4f), new Color(0.7f, 1f, 0.5f));
            main.maxParticles = P.fireflyCount;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission;
            em.rateOverTime = P.fireflyCount / 8f;
            var sp = ps.shape;
            sp.enabled = true;
            sp.shapeType = ParticleSystemShapeType.Box;
            sp.scale = new Vector3(P.areaRadius * 1.5f, 3f, P.areaRadius * 1.5f);
            var nz = ps.noise;
            nz.enabled = true;
            nz.strength = 0.8f;
            nz.frequency = 0.3f;
            nz.scrollSpeed = 0.2f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;
            ps.Play();
        }

        if (P.leaves)
        {
            Texture2D leaf = MakeLeafTexture();
            Shader sh = Shader.Find("Sprites/Default");
            var mat = new Material(sh) { name = "Loc_Leaf" };
            mat.mainTexture = leaf;
            mat = SaveAsset(mat, Dir + "/Loc_Leaf.mat");

            var go = new GameObject("FallingLeaves");
            go.transform.SetParent(atm, false);
            go.transform.position = c + Vector3.up * 14f;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 10f;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(14f, 18f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 0.85f, 0.3f), new Color(0.95f, 0.8f, 0.25f));
            main.maxParticles = P.leafCount;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission;
            em.rateOverTime = P.leafCount / 16f;
            var sp = ps.shape;
            sp.enabled = true;
            sp.shapeType = ParticleSystemShapeType.Box;
            sp.scale = new Vector3(P.areaRadius * 1.6f, 1f, P.areaRadius * 1.6f);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0.3f);
            vel.y = new ParticleSystem.MinMaxCurve(-0.7f);
            vel.z = new ParticleSystem.MinMaxCurve(0f);
            var nz = ps.noise;
            nz.enabled = true;
            nz.strength = 0.5f;
            nz.frequency = 0.25f;
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;
            ps.Play();
        }
    }

    Texture2D MakeGlowTexture()
    {
        const int r = 64;
        var px = new Color32[r * r];
        for (int y = 0; y < r; y++)
            for (int x = 0; x < r; x++)
            {
                float dx = (x + 0.5f) / r * 2f - 1f, dy = (y + 0.5f) / r * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float f = Mathf.Clamp01(1f - d);
                f = f * f * f;
                byte b = (byte)(f * 255f);
                px[y * r + x] = new Color32(b, b, b, b);
            }
        var tex = new Texture2D(r, r, TextureFormat.RGBA32, false);
        tex.SetPixels32(px);
        tex.Apply();
        return SaveTex(tex, "Glow", true, TextureWrapMode.Clamp, false);
    }

    Texture2D MakeLeafTexture()
    {
        const int r = 32;
        var px = new Color32[r * r];
        for (int y = 0; y < r; y++)
            for (int x = 0; x < r; x++)
            {
                float u = (x + 0.5f) / r - 0.5f, v = (y + 0.5f) / r - 0.5f;
                float e = (u * u) / (0.22f * 0.22f) + (v * v) / (0.46f * 0.46f);
                float a = Mathf.Clamp01((1f - e) * 4f);
                px[y * r + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        var tex = new Texture2D(r, r, TextureFormat.RGBA32, false);
        tex.SetPixels32(px);
        tex.Apply();
        return SaveTex(tex, "Leaf", true, TextureWrapMode.Clamp, false);
    }

    // ------------------------------------------------------------------ УТИЛИТЫ
    Texture2D SaveTex(Texture2D tex, string name, bool alpha, TextureWrapMode wrap, bool mips)
    {
        string rel = Dir + "/Loc_" + name + ".png";
        File.WriteAllBytes(ProjectPath(rel), tex.EncodeToPNG());
        DestroyImmediate(tex);
        AssetDatabase.ImportAsset(rel, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(rel);
        imp.alphaIsTransparency = alpha;
        imp.wrapMode = wrap;
        imp.mipmapEnabled = mips;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(rel);
    }

    static void MarkStatic(GameObject root)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>())
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
    }

    static GameObject FindAny(string name)
    {
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (t == null || t.name != name) continue;
            if (EditorUtility.IsPersistent(t)) continue;
            if (!t.gameObject.scene.IsValid()) continue;
            return t.gameObject;
        }
        return null;
    }

    static string ProjectPath(string assetRelative)
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetRelative));
    }

    static T SaveAsset<T>(T obj, string path) where T : Object
    {
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(obj, path);
        return obj;
    }

}
#endif
