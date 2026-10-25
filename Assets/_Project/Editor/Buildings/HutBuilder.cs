// Tools > BuilderSim > Hut Builder
// Процедурная «Хижина лесоруба» в 3 этапа. Unity 6 (6000.3), URP.
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using static EditorAssets;

// ============================================================================
//  Построитель меша: у каждой грани СВОИ вершины -> плоское затенение
// ============================================================================
sealed class HutMesh
{
    readonly List<Vector3> v = new List<Vector3>();
    readonly List<int> t = new List<int>();
    readonly System.Random rng;

    public HutMesh(System.Random r) { rng = r; }
    public int Count => t.Count;

    float J(float a) { return ((float)rng.NextDouble() * 2f - 1f) * a; }

    public void Tri(Vector3 a, Vector3 b, Vector3 c)
    {
        int i = v.Count;
        v.Add(a); v.Add(b); v.Add(c);
        t.Add(i); t.Add(i + 1); t.Add(i + 2);
    }

    // Треугольник, нормаль которого гарантированно смотрит от точки inside.
    public void TriOut(Vector3 a, Vector3 b, Vector3 c, Vector3 inside)
    {
        Vector3 n = Vector3.Cross(b - a, c - a);
        Vector3 ctr = (a + b + c) / 3f;
        if (Vector3.Dot(n, ctr - inside) >= 0f) Tri(a, b, c); else Tri(a, c, b);
    }

    public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 inside)
    {
        TriOut(a, b, c, inside);
        TriOut(a, c, d, inside);
    }

    static readonly int[][] BoxFaces =
    {
        new[]{0,2,6,4}, new[]{1,3,7,5}, new[]{0,1,5,4},
        new[]{2,3,7,6}, new[]{0,1,3,2}, new[]{4,5,7,6}
    };

    // Коробка с вращением и «кривизной» углов (jit — доля от минимального размера).
    public void Box(Vector3 c, Vector3 size, Quaternion rot, float jit = 0f)
    {
        var p = new Vector3[8];
        float jm = jit * Mathf.Min(size.x, Mathf.Min(size.y, size.z));
        for (int i = 0; i < 8; i++)
        {
            var l = new Vector3(((i & 1) != 0 ? 0.5f : -0.5f) * size.x,
                                ((i & 2) != 0 ? 0.5f : -0.5f) * size.y,
                                ((i & 4) != 0 ? 0.5f : -0.5f) * size.z);
            if (jm > 0f) l += new Vector3(J(jm), J(jm), J(jm));
            p[i] = c + rot * l;
        }
        foreach (var f in BoxFaces) Quad(p[f[0]], p[f[1]], p[f[2]], p[f[3]], c);
    }

    // Балка от p0 до p1; w — ширина, h — высота сечения.
    public void Beam(Vector3 p0, Vector3 p1, float w, float h, float jit = 0f)
    {
        Vector3 d = p1 - p0;
        float len = d.magnitude;
        if (len < 1e-4f) return;
        Vector3 dn = d / len;
        Vector3 up = Mathf.Abs(Vector3.Dot(dn, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up;
        Box((p0 + p1) * 0.5f, new Vector3(w, h, len), Quaternion.LookRotation(dn, up), jit);
    }

    // Бревно / усечённый конус с малым числом граней.
    public void Cyl(Vector3 p0, Vector3 p1, float r0, float r1, int sides, float jit = 0f, bool caps = true)
    {
        Vector3 axis = p1 - p0;
        if (axis.sqrMagnitude < 1e-8f) return;
        var q = Quaternion.FromToRotation(Vector3.up, axis.normalized);
        float spin = J(1f) * Mathf.PI;
        var a = new Vector3[sides];
        var b = new Vector3[sides];
        for (int i = 0; i < sides; i++)
        {
            float ang = spin + i * Mathf.PI * 2f / sides;
            var dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
            a[i] = p0 + q * (dir * r0 * (1f + J(jit)));
            b[i] = p1 + q * (dir * r1 * (1f + J(jit)));
        }
        Vector3 mid = (p0 + p1) * 0.5f;
        for (int i = 0; i < sides; i++)
        {
            int j = (i + 1) % sides;
            Quad(a[i], a[j], b[j], b[i], mid);
            if (caps)
            {
                TriOut(p0, a[i], a[j], mid);
                TriOut(p1, b[i], b[j], mid);
            }
        }
    }

    static readonly Vector3[] IcoV =
    {
        new Vector3(-1,1.618f,0), new Vector3(1,1.618f,0), new Vector3(-1,-1.618f,0), new Vector3(1,-1.618f,0),
        new Vector3(0,-1,1.618f), new Vector3(0,1,1.618f), new Vector3(0,-1,-1.618f), new Vector3(0,1,-1.618f),
        new Vector3(1.618f,0,-1), new Vector3(1.618f,0,1), new Vector3(-1.618f,0,-1), new Vector3(-1.618f,0,1)
    };
    static readonly int[] IcoF =
    {
        0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11,
        1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
        3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9,
        4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1
    };

    // Низкополигональный «шарик» (клуб дыма).
    public void Ico(Vector3 c, float radius, float jit)
    {
        var p = new Vector3[12];
        for (int i = 0; i < 12; i++) p[i] = c + IcoV[i].normalized * radius * (1f + J(jit));
        for (int i = 0; i < IcoF.Length; i += 3)
            TriOut(p[IcoF[i]], p[IcoF[i + 1]], p[IcoF[i + 2]], c);
    }

    public Mesh ToMesh(string name)
    {
        var m = new Mesh { name = name };
        if (v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
        m.SetVertices(v);
        m.SetTriangles(t, 0);
        m.RecalculateNormals();   // вершины не общие -> плоские грани
        m.RecalculateBounds();
        return m;
    }
}

// ============================================================================
//  Окно инструмента
// ============================================================================
public class HutBuilder : EditorWindow
{
    const string Dir = "Assets/_Game/Hut";
    const string PrefKey = "BuilderSim.HutBuilder.v1";
    const string RootName = "LumberjackHut";
    const float Rise = 0.25f;   // насколько фундамент торчит над землёй

    [System.Serializable]
    public class Settings
    {
        public float width = 4.2f, length = 5f, wallHeight = 2.5f, roofAngle = 35f, logThickness = 0.36f;
        public int seed = 12345;
        public Color woodLight = new Color(0.66f, 0.44f, 0.24f);
        public Color woodDark = new Color(0.40f, 0.26f, 0.14f);
        public Color stone = new Color(0.60f, 0.58f, 0.64f);
        public Color roof = new Color(0.85f, 0.70f, 0.35f);
        public Color moss = new Color(0.36f, 0.66f, 0.22f);
        public bool decStakes = true, decPile = true, decHorse = true;                 // этап 1
        public bool decScaffold = true;                                                 // этап 2
        public bool decPorch = true, decChimney = true, decSmoke = true, decWoodpile = true,
                    decStump = true, decLantern = true, decMoss = true;                 // этап 3
        public Vector3 position;
        public float yaw;
    }

    enum MatId { WoodL, WoodD, Stone, Roof, RoofB, Moss, Glass, Lamp, Smoke, Rope }

    class StageCtx
    {
        public Transform root;
        public string tag;
        public Transform colRoot;
        public System.Random rng;
        public Dictionary<MatId, HutMesh> mbs = new Dictionary<MatId, HutMesh>();
        public HutMesh Get(MatId id)
        {
            HutMesh m;
            if (!mbs.TryGetValue(id, out m)) { m = new HutMesh(rng); mbs[id] = m; }
            return m;
        }
    }

    // ---- состояние окна ----
    Settings S = new Settings();
    [SerializeField] GameObject[] wallPrefabs;
    [SerializeField] GameObject[] roofPrefabs;
    [SerializeField] GameObject[] doorPrefabs;
    [SerializeField] GameObject[] decorPrefabs;
    Vector2 scroll;

    // ---- состояние сборки ----
    System.Random rng;
    Transform root;
    readonly Dictionary<MatId, Material> matCache = new Dictionary<MatId, Material>();
    Mesh smokeMesh;

    // ---- производные размеры ----
    float W, L, d, r, step, off, topY, tanA, hs, ridgeY, eaveY, doorH, winBottom, winTop;
    int n, doorLayers, winK0, winK1;
    bool hasWin;
    const float oh = 0.55f, ohZ = 0.45f, doorW = 1.0f, winW = 0.9f, winZ = 0f, roofOff = 0.115f;

    [MenuItem("Tools/BuilderSim/Здания/Hut Builder", false, 302)]
    static void Open()
    {
        var w = GetWindow<HutBuilder>(false, "Хижина лесоруба");
        w.minSize = new Vector2(380, 560);
        w.Show();
    }

    // ========================================================================
    //  Сохранение настроек
    // ========================================================================
    void OnEnable()
    {
        Load();
        SceneView.duringSceneGui += OnSceneGUI;
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        Save();
    }

    void Save()
    {
        EditorPrefs.SetString(PrefKey, JsonUtility.ToJson(S));
    }

    void SavePrefabs()
    {
        EditorPrefs.SetString(PrefKey + ".walls", PathsOf(wallPrefabs));
        EditorPrefs.SetString(PrefKey + ".roofs", PathsOf(roofPrefabs));
        EditorPrefs.SetString(PrefKey + ".doors", PathsOf(doorPrefabs));
        EditorPrefs.SetString(PrefKey + ".decor", PathsOf(decorPrefabs));
    }

    void Load()
    {
        if (EditorPrefs.HasKey(PrefKey))
            JsonUtility.FromJsonOverwrite(EditorPrefs.GetString(PrefKey), S);
        wallPrefabs = FromPaths(EditorPrefs.GetString(PrefKey + ".walls", ""));
        roofPrefabs = FromPaths(EditorPrefs.GetString(PrefKey + ".roofs", ""));
        doorPrefabs = FromPaths(EditorPrefs.GetString(PrefKey + ".doors", ""));
        decorPrefabs = FromPaths(EditorPrefs.GetString(PrefKey + ".decor", ""));
    }

    // ========================================================================
    //  UI окна
    // ========================================================================
    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("Хижина лесоруба", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Размеры", EditorStyles.boldLabel);
        S.width = EditorGUILayout.Slider("Ширина (м)", S.width, 2.5f, 8f);
        S.length = EditorGUILayout.Slider("Длина (м)", S.length, 3f, 10f);
        S.wallHeight = EditorGUILayout.Slider("Высота стен (м)", S.wallHeight, 1.8f, 4f);
        S.roofAngle = EditorGUILayout.Slider("Наклон крыши (°)", S.roofAngle, 20f, 55f);
        S.logThickness = EditorGUILayout.Slider("Толщина брёвен (м)", S.logThickness, 0.2f, 0.6f);
        S.seed = EditorGUILayout.IntField("Seed", S.seed);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Цвета", EditorStyles.boldLabel);
        S.woodLight = EditorGUILayout.ColorField("Светлое дерево", S.woodLight);
        S.woodDark = EditorGUILayout.ColorField("Тёмное дерево", S.woodDark);
        S.stone = EditorGUILayout.ColorField("Камень", S.stone);
        S.roof = EditorGUILayout.ColorField("Крыша (солома/дранка)", S.roof);
        S.moss = EditorGUILayout.ColorField("Мох / трава", S.moss);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Декор: этап 1", EditorStyles.boldLabel);
        S.decStakes = EditorGUILayout.Toggle("Колышки с верёвкой", S.decStakes);
        S.decPile = EditorGUILayout.Toggle("Стопка досок и брёвен", S.decPile);
        S.decHorse = EditorGUILayout.Toggle("Козлы для распиловки", S.decHorse);
        EditorGUILayout.LabelField("Декор: этап 2", EditorStyles.boldLabel);
        S.decScaffold = EditorGUILayout.Toggle("Подмостки и лестница", S.decScaffold);
        EditorGUILayout.LabelField("Декор: этап 3", EditorStyles.boldLabel);
        S.decPorch = EditorGUILayout.Toggle("Крыльцо", S.decPorch);
        S.decChimney = EditorGUILayout.Toggle("Каменная труба", S.decChimney);
        S.decSmoke = EditorGUILayout.Toggle("Дым из трубы", S.decSmoke);
        S.decWoodpile = EditorGUILayout.Toggle("Дровница", S.decWoodpile);
        S.decStump = EditorGUILayout.Toggle("Пень с топором", S.decStump);
        S.decLantern = EditorGUILayout.Toggle("Фонарь у двери", S.decLantern);
        S.decMoss = EditorGUILayout.Toggle("Мох на крыше и траву у фундамента", S.decMoss);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Положение (или двигай ручки в Scene)", EditorStyles.boldLabel);
        S.position = EditorGUILayout.Vector3Field("Позиция", S.position);
        S.yaw = EditorGUILayout.FloatField("Поворот Y (°)", S.yaw);

        if (EditorGUI.EndChangeCheck())
        {
            Save();
            MoveExisting();
            SceneView.RepaintAll();
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Свои префабы (пусто = процедурные меши)", EditorStyles.boldLabel);
        var so = new SerializedObject(this);
        so.Update();
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(so.FindProperty("wallPrefabs"), new GUIContent("Стены (4 шт. по кругу)"), true);
        EditorGUILayout.PropertyField(so.FindProperty("roofPrefabs"), new GUIContent("Крыша"), true);
        EditorGUILayout.PropertyField(so.FindProperty("doorPrefabs"), new GUIContent("Дверь"), true);
        EditorGUILayout.PropertyField(so.FindProperty("decorPrefabs"), new GUIContent("Декор (вокруг хижины)"), true);
        bool prefabsChanged = EditorGUI.EndChangeCheck();
        so.ApplyModifiedProperties();
        if (prefabsChanged) SavePrefabs();

        EditorGUILayout.Space(8);
        if (GUILayout.Button("Построить хижину", GUILayout.Height(32)))
            EditorApplication.delayCall += DoBuild;
        if (GUILayout.Button("Удалить хижину", GUILayout.Height(24)))
            EditorApplication.delayCall += DoDelete;

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Предпросмотр этапа", EditorStyles.miniBoldLabel);
        EditorGUILayout.BeginHorizontal();
        for (int i = 1; i <= 3; i++)
            if (GUILayout.Button("Этап " + i)) ShowStage(i);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox("Окно должно быть открыто, чтобы в Scene были кнопка и ручки.", MessageType.None);
        EditorGUILayout.EndScrollView();
    }

    void DoBuild() { if (this != null) Build(); }
    void DoDelete() { if (this != null) Delete(); }

    void ShowStage(int stage)
    {
        var hut = FindHut();
        if (hut == null) return;
        var hsComp = hut.GetComponent<HutStages>();
        if (hsComp != null) hsComp.SetStage(stage);
    }

    // ========================================================================
    //  Scene: кнопка в левом верхнем углу + ручки
    // ========================================================================
    void OnSceneGUI(SceneView sv)
    {
        Handles.BeginGUI();
        GUILayout.BeginArea(new Rect(10, 10, 190, 40));
        if (GUILayout.Button("Построить хижину", GUILayout.Height(28)))
            EditorApplication.delayCall += DoBuild;
        GUILayout.EndArea();
        Handles.EndGUI();

        // габариты-призрак
        var prev = Handles.matrix;
        Handles.matrix = Matrix4x4.TRS(S.position, Quaternion.Euler(0, S.yaw, 0), Vector3.one);
        Handles.color = new Color(1f, 0.8f, 0.2f, 0.9f);
        Handles.DrawWireCube(new Vector3(0, S.wallHeight * 0.5f, 0), new Vector3(S.width, S.wallHeight, S.length));
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

    // Переставляет уже построенную хижину вслед за ручками (без пересборки).
    void MoveExisting()
    {
        var hut = FindHut();
        if (hut == null) return;
        float y;
        if (!GroundY(S.position, out y)) y = hut.transform.position.y - Rise;
        Undo.RecordObject(hut.transform, "Переместить хижину");
        hut.transform.SetPositionAndRotation(new Vector3(S.position.x, y + Rise, S.position.z),
                                             Quaternion.Euler(0, S.yaw, 0));
    }

    static GameObject FindHut()
    {
        foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
            if (go.name == RootName) return go;
        return null;
    }

    void Delete()
    {
        var hut = FindHut();
        if (hut != null) Undo.DestroyObjectImmediate(hut);
    }

    // ========================================================================
    //  Земля
    // ========================================================================
    static bool GroundY(Vector3 wp, out float y)
    {
        y = 0f;
        var hits = Physics.RaycastAll(new Vector3(wp.x, wp.y + 500f, wp.z), Vector3.down, 1500f, ~0,
                                      QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        bool ok = false;
        foreach (var h in hits)
        {
            if (h.collider.gameObject.name != "Ground") continue;
            if (h.distance < best) { best = h.distance; y = h.point.y; ok = true; }
        }
        return ok;
    }

    bool AverageGround(out float y)
    {
        var q = Quaternion.Euler(0, S.yaw, 0);
        Vector3[] offs =
        {
            Vector3.zero, new Vector3(W / 2, 0, L / 2), new Vector3(-W / 2, 0, L / 2),
            new Vector3(W / 2, 0, -L / 2), new Vector3(-W / 2, 0, -L / 2)
        };
        float sum = 0f; int cnt = 0;
        foreach (var o in offs)
        {
            float gy;
            if (GroundY(S.position + q * o, out gy)) { sum += gy; cnt++; }
        }
        y = cnt > 0 ? sum / cnt : 0f;
        return cnt > 0;
    }

    // Высота земли в ЛОКАЛЬНЫХ координатах хижины.
    float GY(float x, float z)
    {
        Vector3 wp = root.TransformPoint(new Vector3(x, 0, z));
        float y;
        if (GroundY(wp, out y)) return root.InverseTransformPoint(new Vector3(wp.x, y, wp.z)).y;
        return -Rise;
    }

    // ========================================================================
    //  Утилиты: случайность, ассеты
    // ========================================================================
    float Rf(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }
    float Jf(float a) { return Rf(-a, a); }

    Material GetMat(MatId id)
    {
        Material cached;
        if (matCache.TryGetValue(id, out cached)) return cached;

        Color col; float emis = 0f;
        switch (id)
        {
            case MatId.WoodL: col = S.woodLight; break;
            case MatId.WoodD: col = S.woodDark; break;
            case MatId.Stone: col = S.stone; break;
            case MatId.Roof: col = S.roof; break;
            case MatId.RoofB: col = S.roof * 0.86f; col.a = 1f; break;
            case MatId.Moss: col = S.moss; break;
            case MatId.Glass: col = new Color(0.62f, 0.85f, 0.95f); break;
            case MatId.Lamp: col = new Color(1f, 0.85f, 0.4f); emis = 1.6f; break;
            case MatId.Smoke: col = new Color(0.88f, 0.88f, 0.9f); break;
            default: col = new Color(0.78f, 0.66f, 0.42f); break; // Rope
        }

        string path = Dir + "/Hut_" + id + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            m = new Material(sh) { name = "Hut_" + id };
            AssetDatabase.CreateAsset(m, path);
        }
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
        if (m.HasProperty("_Color")) m.SetColor("_Color", col);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.05f);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.05f);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        if (emis > 0f)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", col * emis);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
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

    StageCtx NewCtx(Transform stageRoot, string tag)
    {
        return new StageCtx { root = stageRoot, tag = tag, rng = rng };
    }

    void Flush(StageCtx c)
    {
        foreach (var kv in c.mbs)
        {
            if (kv.Value.Count == 0) continue;
            string nm = "Hut_" + c.tag + "_" + kv.Key;
            var go = new GameObject("Mesh_" + kv.Key);
            go.transform.SetParent(c.root, false);
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mf.sharedMesh = SaveMesh(kv.Value.ToMesh(nm), nm);
            mr.sharedMaterial = GetMat(kv.Key);
        }
    }

    void AddCol(StageCtx c, Vector3 center, Vector3 size, Quaternion? rot = null)
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
        var bc = go.AddComponent<BoxCollider>();
        bc.size = size;
    }

    static Transform NewChild(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    static List<GameObject> Valid(GameObject[] a)
    {
        var l = new List<GameObject>();
        if (a != null) foreach (var g in a) if (g != null) l.Add(g);
        return l;
    }

    GameObject PlacePrefab(GameObject prefab, Transform parent, Vector3 lp, float yaw)
    {
        var inst = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
        if (inst == null) inst = Instantiate(prefab, parent);
        inst.transform.localPosition = lp;
        inst.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        return inst;
    }

    // ========================================================================
    //  СБОРКА
    // ========================================================================
    void Prepare()
    {
        W = S.width; L = S.length;
        d = S.logThickness; r = d * 0.5f; step = d * 0.86f; off = step * 0.5f;
        n = Mathf.Max(4, Mathf.RoundToInt((S.wallHeight - d - off) / step) + 1);
        topY = (n - 1) * step + d + off;
        tanA = Mathf.Tan(S.roofAngle * Mathf.Deg2Rad);
        hs = W * 0.5f + oh;
        ridgeY = topY + W * 0.5f * tanA;
        eaveY = topY - oh * tanA;
        doorLayers = Mathf.Clamp(Mathf.FloorToInt(1.9f / step), 1, n - 2);
        doorH = doorLayers * step - 0.03f;

        hasWin = n >= 6;
        winK0 = 0; winK1 = 0;
        if (hasWin)
        {
            winK0 = Mathf.Clamp(Mathf.RoundToInt(0.95f / step), 1, n - 4);
            int layers = n >= 9 ? 3 : 2;
            winK1 = winK0 + layers - 1;
            if (winK1 > n - 3) hasWin = false;
        }
        winBottom = winK0 * step + off + (d - step);
        winTop = (winK1 + 1) * step + off;
    }

    void Build()
    {
        EnsureFolder(Dir);
        var old = FindHut();
        if (old != null) Undo.DestroyObjectImmediate(old);

        rng = new System.Random(S.seed);
        matCache.Clear();
        smokeMesh = null;
        Prepare();
        Physics.SyncTransforms();

        float gy;
        if (!AverageGround(out gy))
        {
            gy = S.position.y - Rise;
            Debug.LogWarning("HutBuilder: коллайдер объекта «Ground» под хижиной не найден, " +
                             "высота взята из позиции ручки.");
        }

        var go = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(go, "Построить хижину");
        root = go.transform;
        root.SetPositionAndRotation(new Vector3(S.position.x, gy + Rise, S.position.z),
                                    Quaternion.Euler(0, S.yaw, 0));

        var s1 = NewChild(root, "Stage1");
        var s2 = NewChild(root, "Stage2");
        var s3 = NewChild(root, "Stage3");

        BuildStage1(s1);
        BuildStage2(s2);
        BuildStage3(s3);

        var bp = NewChild(root, "BuildPoint");
        bp.localPosition = new Vector3(0, GY(0, -L * 0.5f - 3f), -L * 0.5f - 3f);
        bp.localRotation = Quaternion.identity;

        var stages = go.AddComponent<HutStages>();
        stages.Configure(s1.gameObject, s2.gameObject, s3.gameObject, bp, 1);
        stages.SetStage(3);   // в редакторе показываем готовый результат

        ApplyStatic(root);
        Selection.activeGameObject = go;
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(go.scene);
    }

    static void ApplyStatic(Transform t)
    {
        var flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic;
        foreach (var tr in t.GetComponentsInChildren<Transform>(true))
        {
            if (tr.name == "Smoke" || tr.name == "LanternLight") continue;
            GameObjectUtility.SetStaticEditorFlags(tr.gameObject, flags);
        }
    }

    // ========================================================================
    //  ЭТАП 1: фундамент
    // ========================================================================
    void BuildStage1(Transform parent)
    {
        var c = NewCtx(parent, "S1");
        var st = c.Get(MatId.Stone);

        // плита фундамента
        st.Box(new Vector3(0, -0.45f, 0), new Vector3(W + 0.4f, 0.9f, L + 0.4f), Quaternion.identity, 0.01f);
        AddCol(c, new Vector3(0, -0.45f, 0), new Vector3(W + 0.4f, 0.9f, L + 0.4f));

        // валуны по периметру (у крыльца пропускаем)
        float sx = W * 0.5f + 0.4f, sz = L * 0.5f + 0.4f;
        float porchHalf = S.decPorch ? doorW * 0.5f + 0.6f : 0f;
        for (float x = -sx; x <= sx + 0.01f; x += 0.55f)
            for (int sg = -1; sg <= 1; sg += 2)
            {
                if (sg < 0 && Mathf.Abs(x) < porchHalf) continue;
                RimStone(st, x + Jf(0.05f), sg * sz + Jf(0.05f));
            }
        for (float z = -sz + 0.55f; z <= sz - 0.55f + 0.01f; z += 0.55f)
            for (int sg = -1; sg <= 1; sg += 2)
                RimStone(st, sg * sx + Jf(0.05f), z + Jf(0.05f));

        // два лежня под боковыми стенами
        var wd = c.Get(MatId.WoodD);
        for (int sg = -1; sg <= 1; sg += 2)
            wd.Cyl(new Vector3(sg * W * 0.5f, 0.12f, -L * 0.5f - 0.05f),
                   new Vector3(sg * W * 0.5f, 0.12f, L * 0.5f + 0.05f), 0.14f, 0.14f, 7, 0.05f);

        if (S.decPile) { BuildPile(c); BuildLogStack(c); }
        if (S.decHorse) BuildHorse(c);
        if (S.decStakes) BuildStakes(c);
        if (S.decMoss) BuildGrassTufts(c);

        Flush(c);
    }

    void RimStone(HutMesh st, float x, float z)
    {
        float h = Rf(0.30f, 0.42f);
        st.Box(new Vector3(x, 0.08f - h * 0.5f, z),
               new Vector3(Rf(0.42f, 0.62f), h, Rf(0.42f, 0.55f)),
               Quaternion.Euler(0, Jf(12f), 0), 0.06f);
    }

    void BuildGrassTufts(StageCtx c)
    {
        var mo = c.Get(MatId.Moss);
        float sx = W * 0.5f + 0.7f, sz = L * 0.5f + 0.7f;
        for (int i = 0; i < 12; i++)
        {
            float a = Rf(0f, Mathf.PI * 2f);
            float x = Mathf.Clamp(Mathf.Cos(a) * 100f, -sx, sx);
            float z = Mathf.Clamp(Mathf.Sin(a) * 100f, -sz, sz);
            if (z < 0 && Mathf.Abs(x) < doorW * 0.5f + 0.9f) continue;
            float g = GY(x, z);
            for (int k = 0; k < 3; k++)
            {
                var b = new Vector3(x + Jf(0.12f), g - 0.02f, z + Jf(0.12f));
                mo.Cyl(b, b + new Vector3(Jf(0.05f), Rf(0.14f, 0.26f), Jf(0.05f)), 0.07f, 0.008f, 4, 0.1f);
            }
        }
    }

    void BuildPile(StageCtx c)
    {
        float px = W * 0.5f + 1.9f, pz = L * 0.25f;
        float g = GY(px, pz);
        var wd = c.Get(MatId.WoodD);
        for (int b = -1; b <= 1; b += 2)
            wd.Box(new Vector3(px, g + 0.04f, pz + b * 0.7f), new Vector3(1.0f, 0.08f, 0.1f), Quaternion.identity, 0.03f);
        for (int layer = 0; layer < 7; layer++)
            for (int k = 0; k < 3; k++)
            {
                var mb = c.Get(rng.NextDouble() < 0.7 ? MatId.WoodL : MatId.WoodD);
                float len = Rf(1.6f, 2.0f);
                mb.Box(new Vector3(px + (k - 1) * 0.26f + Jf(0.02f), g + 0.105f + layer * 0.055f, pz + Jf(0.08f)),
                       new Vector3(0.24f, 0.05f, len), Quaternion.Euler(0, Jf(2.5f), 0), 0.03f);
            }
    }

    void BuildLogStack(StageCtx c)
    {
        float lx = -(W * 0.5f + 2.2f), lz = -L * 0.15f;
        float g = GY(lx, lz);
        int[] rows = { 3, 2, 1 };
        for (int row = 0; row < rows.Length; row++)
            for (int i = 0; i < rows[row]; i++)
            {
                float x = lx + (i - (rows[row] - 1) * 0.5f) * 0.36f;
                float y = g + 0.17f + row * 0.31f;
                float len = Rf(1.9f, 2.4f);
                float zs = Jf(0.15f);
                var mb = c.Get(rng.NextDouble() < 0.6 ? MatId.WoodL : MatId.WoodD);
                mb.Cyl(new Vector3(x, y, lz + zs - len * 0.5f), new Vector3(x, y, lz + zs + len * 0.5f),
                       0.17f, 0.17f, 7, 0.05f);
            }
    }

    void BuildHorse(StageCtx c)
    {
        float hx = W * 0.5f + 1.8f, hz = -L * 0.28f;
        float g = GY(hx, hz);
        float hh = 0.8f;
        var wd = c.Get(MatId.WoodD);
        for (int e = -1; e <= 1; e += 2)
        {
            float z = hz + e * 0.7f;
            var top = new Vector3(hx, g + hh, z);
            for (int s = -1; s <= 1; s += 2)
                wd.Beam(top, new Vector3(hx + s * 0.38f, g, z), 0.07f, 0.07f, 0.02f);
        }
        wd.Beam(new Vector3(hx, g + hh, hz - 0.85f), new Vector3(hx, g + hh, hz + 0.85f), 0.1f, 0.08f, 0.02f);
        c.Get(MatId.WoodL).Cyl(new Vector3(hx, g + hh + 0.15f, hz - 1.0f),
                               new Vector3(hx, g + hh + 0.15f, hz + 0.9f), 0.1f, 0.1f, 7, 0.04f);
    }

    void BuildStakes(StageCtx c)
    {
        float ax = W * 0.5f + 1.4f, az = L * 0.5f + 1.4f;
        var corners = new[] { new Vector2(-ax, -az), new Vector2(ax, -az), new Vector2(ax, az), new Vector2(-ax, az) };
        var pts = new List<Vector3>();
        var wl = c.Get(MatId.WoodL);
        for (int i = 0; i < 4; i++)
        {
            Vector2 a = corners[i], b = corners[(i + 1) % 4];
            int seg = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / 2.1f));
            for (int j = 0; j < seg; j++)
            {
                Vector2 p = Vector2.Lerp(a, b, (float)j / seg);
                float g = GY(p.x, p.y);
                var bot = new Vector3(p.x, g - 0.1f, p.y);
                var top = bot + new Vector3(Jf(0.05f), 1.0f + Jf(0.1f), Jf(0.05f));
                wl.Cyl(bot, top, 0.05f, 0.03f, 5, 0.1f);
                pts.Add(top - new Vector3(0, 0.12f, 0));
            }
        }
        var rope = c.Get(MatId.Rope);
        for (int i = 0; i < pts.Count; i++)
        {
            Vector3 a = pts[i], b = pts[(i + 1) % pts.Count];
            Vector3 prev = a;
            for (int s = 1; s <= 5; s++)
            {
                float t = s / 5f;
                Vector3 q = Vector3.Lerp(a, b, t) + Vector3.down * 0.14f * 4f * t * (1f - t);
                rope.Beam(prev, q, 0.03f, 0.03f, 0f);
                prev = q;
            }
        }
    }

    // ========================================================================
    //  ЭТАП 2: каркас и стены
    // ========================================================================
    void BuildStage2(Transform parent)
    {
        var c = NewCtx(parent, "S2");
        var walls = Valid(wallPrefabs);

        if (walls.Count == 0)
        {
            BuildWalls(c);
            BuildGables(c);
            BuildWallColliders(c);
        }
        else
        {
            // 0: передняя(-Z), 1: правая(+X), 2: задняя(+Z), 3: левая(-X); ось Z префаба смотрит наружу
            Vector3[] pos = { new Vector3(0, 0, -L * 0.5f), new Vector3(W * 0.5f, 0, 0),
                              new Vector3(0, 0, L * 0.5f), new Vector3(-W * 0.5f, 0, 0) };
            float[] yaw = { 180f, 90f, 0f, 270f };
            for (int i = 0; i < 4; i++) PlacePrefab(walls[i % walls.Count], parent, pos[i], yaw[i]);
        }

        BuildRafters(c);
        if (S.decScaffold) BuildScaffold(c);
        Flush(c);
    }

    // бревно с лёгкой неровностью
    void Log(StageCtx c, MatId m, Vector3 a, Vector3 b, float radius)
    {
        a += new Vector3(Jf(0.01f), Jf(0.012f), Jf(0.01f));
        b += new Vector3(Jf(0.01f), Jf(0.012f), Jf(0.01f));
        c.Get(m).Cyl(a, b, radius * Rf(0.96f, 1.04f), radius * Rf(0.96f, 1.04f), 7, 0.04f);
    }

    // бревно стены с возможным проёмом (hole — диапазон по оси бревна)
    void WallLog(StageCtx c, MatId m, Vector3 a, Vector3 b, bool hole, float h0, float h1)
    {
        if (!hole) { Log(c, m, a, b, r); return; }
        bool ax = Mathf.Abs(b.x - a.x) > Mathf.Abs(b.z - a.z);
        float lo = ax ? a.x : a.z, hi = ax ? b.x : b.z;
        Log(c, m, WallPt(a, ax, lo), WallPt(a, ax, h0), r);
        Log(c, m, WallPt(a, ax, h1), WallPt(a, ax, hi), r);
    }

    static Vector3 WallPt(Vector3 a, bool alongX, float t)
    {
        return alongX ? new Vector3(t, a.y, a.z) : new Vector3(a.x, a.y, t);
    }

    void BuildWalls(StageCtx c)
    {
        float hw = W * 0.5f, hl = L * 0.5f;
        for (int k = 0; k < n; k++)
        {
            MatId m = (k % 2 == 0) ? MatId.WoodL : MatId.WoodD;
            MatId m2 = (k % 2 == 0) ? MatId.WoodD : MatId.WoodL;
            float yF = k * step + r;
            float yS = yF + off;
            bool dCut = k < doorLayers;
            bool wCut = hasWin && k >= winK0 && k <= winK1;

            // фронт (с дверью) и тыл
            WallLog(c, m, new Vector3(-hw - d * Rf(0.2f, 0.45f), yF, -hl),
                          new Vector3(hw + d * Rf(0.2f, 0.45f), yF, -hl), dCut, -doorW * 0.5f, doorW * 0.5f);
            WallLog(c, m, new Vector3(-hw - d * Rf(0.2f, 0.45f), yF, hl),
                          new Vector3(hw + d * Rf(0.2f, 0.45f), yF, hl), false, 0, 0);
            // левая и правая (с окном)
            WallLog(c, m2, new Vector3(-hw, yS, -hl - d * Rf(0.2f, 0.45f)),
                           new Vector3(-hw, yS, hl + d * Rf(0.2f, 0.45f)), false, 0, 0);
            WallLog(c, m2, new Vector3(hw, yS, -hl - d * Rf(0.2f, 0.45f)),
                           new Vector3(hw, yS, hl + d * Rf(0.2f, 0.45f)), wCut, winZ - winW * 0.5f, winZ + winW * 0.5f);
        }
    }

    void BuildGables(StageCtx c)
    {
        float hl = L * 0.5f;
        for (int j = 0; j < 30; j++)
        {
            float yj = (n - 1) * step + r + step * (j + 1);
            float hw = Mathf.Min(W * 0.5f, W * 0.5f - (yj + r * 0.5f - topY) / tanA);
            if (hw < 0.15f || yj - r > ridgeY) break;
            MatId m = (j % 2 == 0) ? MatId.WoodL : MatId.WoodD;
            Log(c, m, new Vector3(-hw, yj, -hl), new Vector3(hw, yj, -hl), r);
            Log(c, m, new Vector3(-hw, yj, hl), new Vector3(hw, yj, hl), r);
        }
    }

    void BuildWallColliders(StageCtx c)
    {
        float t = d, hw = W * 0.5f, hl = L * 0.5f;
        // фронт: слева и справа от двери + перемычка над дверью. На самом проёме коллайдера нет.
        float lx0 = -hw - r, lx1 = -doorW * 0.5f;
        AddCol(c, new Vector3((lx0 + lx1) * 0.5f, topY * 0.5f, -hl), new Vector3(lx1 - lx0, topY, t));
        AddCol(c, new Vector3(-(lx0 + lx1) * 0.5f, topY * 0.5f, -hl), new Vector3(lx1 - lx0, topY, t));
        AddCol(c, new Vector3(0, (doorH + topY) * 0.5f, -hl), new Vector3(doorW, topY - doorH, t));
        // тыл и левая стена
        AddCol(c, new Vector3(0, topY * 0.5f, hl), new Vector3(W + t, topY, t));
        AddCol(c, new Vector3(-hw, topY * 0.5f, 0), new Vector3(t, topY, L + t));
        // правая стена (окно)
        if (hasWin)
        {
            float z0 = -hl - r, z1 = winZ - winW * 0.5f, z2 = winZ + winW * 0.5f, z3 = hl + r;
            AddCol(c, new Vector3(hw, topY * 0.5f, (z0 + z1) * 0.5f), new Vector3(t, topY, z1 - z0));
            AddCol(c, new Vector3(hw, topY * 0.5f, (z2 + z3) * 0.5f), new Vector3(t, topY, z3 - z2));
            AddCol(c, new Vector3(hw, winBottom * 0.5f, winZ), new Vector3(t, winBottom, winW));
            AddCol(c, new Vector3(hw, (winTop + topY) * 0.5f, winZ), new Vector3(t, topY - winTop, winW));
        }
        else
        {
            AddCol(c, new Vector3(hw, topY * 0.5f, 0), new Vector3(t, topY, L + t));
        }
    }

    void BuildRafters(StageCtx c)
    {
        var wd = c.Get(MatId.WoodD);
        int m = Mathf.Max(3, Mathf.RoundToInt(L / 0.95f) + 1);
        float zE = L * 0.5f + ohZ;
        for (int i = 0; i < m; i++)
        {
            float z = Mathf.Lerp(-L * 0.5f, L * 0.5f, i / (float)(m - 1));
            var ridge = new Vector3(0, ridgeY - 0.02f, z);
            for (int sg = -1; sg <= 1; sg += 2)
                wd.Beam(new Vector3(sg * hs, eaveY, z), ridge, 0.12f, 0.16f, 0.02f);
            if (i % 2 == 0)
                wd.Beam(new Vector3(-W * 0.5f, topY - 0.04f, z), new Vector3(W * 0.5f, topY - 0.04f, z), 0.12f, 0.12f, 0.02f);
        }
        wd.Beam(new Vector3(0, ridgeY + 0.02f, -zE), new Vector3(0, ridgeY + 0.02f, zE), 0.16f, 0.16f, 0.02f);
        float[] ts = { 0.35f, 0.7f };
        for (int sg = -1; sg <= 1; sg += 2)
            foreach (float tt in ts)
            {
                Vector3 a = Vector3.Lerp(new Vector3(sg * hs, eaveY, -zE), new Vector3(0, ridgeY - 0.02f, -zE), tt);
                Vector3 b = Vector3.Lerp(new Vector3(sg * hs, eaveY, zE), new Vector3(0, ridgeY - 0.02f, zE), tt);
                wd.Beam(a, b, 0.1f, 0.1f, 0.02f);
            }
    }

    void BuildScaffold(StageCtx c)
    {
        float cx = -W * 0.15f, hw = Mathf.Min(1.1f, W * 0.3f);
        float z0 = L * 0.5f + 0.55f, z1 = L * 0.5f + 1.3f;
        float py = Mathf.Min(1.25f, topY * 0.5f);
        float g = GY(cx, (z0 + z1) * 0.5f);
        var wd = c.Get(MatId.WoodD);
        var wl = c.Get(MatId.WoodL);

        float postTop = py + 0.9f;
        for (int sx = -1; sx <= 1; sx += 2)
            for (int k = 0; k < 2; k++)
            {
                float z = k == 0 ? z0 : z1;
                wd.Box(new Vector3(cx + sx * hw, (g + postTop) * 0.5f, z), new Vector3(0.1f, postTop - g, 0.1f), Quaternion.identity, 0.04f);
            }
        int boards = 4;
        float bd = (z1 - z0) / boards;
        for (int i = 0; i < boards; i++)
            wl.Box(new Vector3(cx, py, z0 + (i + 0.5f) * bd), new Vector3(hw * 2f + 0.3f, 0.05f, bd - 0.02f),
                   Quaternion.Euler(0, Jf(2f), 0), 0.03f);
        wd.Beam(new Vector3(cx - hw, py + 0.85f, z1), new Vector3(cx + hw, py + 0.85f, z1), 0.06f, 0.06f, 0.02f);
        wd.Beam(new Vector3(cx - hw, g + 0.1f, z1), new Vector3(cx + hw, py - 0.05f, z1), 0.05f, 0.05f, 0.02f);
        wd.Beam(new Vector3(cx + hw, g + 0.1f, z1), new Vector3(cx - hw, py - 0.05f, z1), 0.05f, 0.05f, 0.02f);

        // лестница, прислонённая к задней стене
        float lx = W * 0.32f;
        float lg = GY(lx, L * 0.5f + 1.1f);
        float topLy = Mathf.Min(topY * 0.85f, 2.0f);
        Vector3 b0 = new Vector3(lx - 0.22f, lg, L * 0.5f + 1.1f), t0 = new Vector3(lx - 0.22f, topLy, L * 0.5f + 0.28f);
        Vector3 b1 = new Vector3(lx + 0.22f, lg, L * 0.5f + 1.1f), t1 = new Vector3(lx + 0.22f, topLy, L * 0.5f + 0.28f);
        wl.Beam(b0, t0, 0.06f, 0.06f, 0.02f);
        wl.Beam(b1, t1, 0.06f, 0.06f, 0.02f);
        for (int i = 1; i <= 6; i++)
        {
            float t = i / 7f;
            wl.Beam(Vector3.Lerp(b0, t0, t), Vector3.Lerp(b1, t1, t), 0.04f, 0.04f, 0.02f);
        }
    }

    // ========================================================================
    //  ЭТАП 3: готово
    // ========================================================================
    void BuildStage3(Transform parent)
    {
        var c = NewCtx(parent, "S3");
        bool procWalls = Valid(wallPrefabs).Count == 0;

        var roofs = Valid(roofPrefabs);
        if (roofs.Count > 0) PlacePrefab(roofs[0], parent, new Vector3(0, topY, 0), 0f);
        else
        {
            BuildRoof(c);
            if (S.decMoss) BuildRoofMoss(c);
        }

        var doors = Valid(doorPrefabs);
        if (doors.Count > 0) PlacePrefab(doors[0], parent, new Vector3(0, 0, -L * 0.5f), 180f);
        else if (procWalls) BuildDoor(c);

        if (procWalls && hasWin) BuildWindow(c);
        if (S.decPorch) BuildPorch(c);
        if (S.decChimney) BuildChimney(c);
        if (S.decWoodpile) BuildWoodpile(c);
        if (S.decStump) BuildStump(c);
        if (S.decLantern) BuildLantern(c);

        Flush(c);
        PlaceDecor(parent);
    }

    void BuildRoof(StageCtx c)
    {
        float zE = L * 0.5f + ohZ;
        var ridgeP = new Vector3(0, ridgeY, 0);
        for (int sg = -1; sg <= 1; sg += 2)
        {
            var eaveP = new Vector3(sg * hs, eaveY, 0);
            Vector3 sd = eaveP - ridgeP;
            float sl = sd.magnitude;
            Vector3 dir = sd / sl;
            Vector3 nrm = sg * Vector3.Cross(Vector3.forward, dir);
            Quaternion q = Quaternion.LookRotation(Vector3.forward, nrm);

            int rows = Mathf.CeilToInt(sl / 0.34f);
            float rs = sl / rows;
            for (int i = 0; i < rows; i++)
            {
                var mb = c.Get((i % 2 == 0) ? MatId.Roof : MatId.RoofB);
                float tt = (i + 0.5f) * rs;
                float lift = roofOff + ((i % 2 == 0) ? 0f : 0.025f);
                float z = -zE;
                while (z < zE - 0.05f)
                {
                    float len = Mathf.Min(Rf(0.5f, 0.85f), zE - z);
                    float zc = z + len * 0.5f;
                    Vector3 pos = ridgeP + dir * tt + nrm * lift + new Vector3(0, 0, zc);
                    mb.Box(pos, new Vector3(rs * 1.4f, 0.07f, len - 0.01f),
                           q * Quaternion.Euler(Jf(1.5f), 0, Jf(2f)), 0.03f);
                    z += len;
                }
            }
            AddCol(c, ridgeP + dir * (sl * 0.5f) + nrm * roofOff, new Vector3(sl, 0.12f, L + 2f * ohZ), q);
        }
        // коньковый брус, повёрнутый «ромбом»
        c.Get(MatId.WoodD).Box(new Vector3(0, ridgeY + 0.1f, 0), new Vector3(0.2f, 0.2f, L + 2f * ohZ + 0.1f),
                               Quaternion.Euler(0, 0, 45f), 0.02f);
    }

    void BuildRoofMoss(StageCtx c)
    {
        var mo = c.Get(MatId.Moss);
        var ridgeP = new Vector3(0, ridgeY, 0);
        for (int i = 0; i < 8; i++)
        {
            int sg = rng.NextDouble() < 0.5 ? -1 : 1;
            var eaveP = new Vector3(sg * hs, eaveY, 0);
            Vector3 sd = eaveP - ridgeP;
            float sl = sd.magnitude;
            Vector3 dir = sd / sl;
            Vector3 nrm = sg * Vector3.Cross(Vector3.forward, dir);
            float tt = Rf(0.2f, 0.9f) * sl;
            float z = Rf(-L * 0.5f, L * 0.5f);
            Vector3 p = ridgeP + dir * tt + new Vector3(0, 0, z) + nrm * (roofOff + 0.06f);
            mo.Cyl(p, p + nrm * 0.05f, Rf(0.18f, 0.4f), Rf(0.12f, 0.3f), 6, 0.2f);
        }
    }

    void BuildDoor(StageCtx c)
    {
        var wl = c.Get(MatId.WoodL);
        var wd = c.Get(MatId.WoodD);
        float z = -L * 0.5f, fd = d * 1.05f;
        // рама
        for (int sg = -1; sg <= 1; sg += 2)
            wl.Box(new Vector3(sg * (doorW * 0.5f - 0.05f), doorH * 0.5f, z), new Vector3(0.1f, doorH, fd), Quaternion.identity, 0.02f);
        wl.Box(new Vector3(0, doorH + 0.05f, z), new Vector3(doorW + 0.3f, 0.14f, fd), Quaternion.identity, 0.02f);
        // полотно из досок (без коллайдера)
        float dw = doorW - 0.2f, dh = doorH - 0.06f;
        int nb = 4;
        float bw = dw / nb;
        for (int i = 0; i < nb; i++)
            wd.Box(new Vector3(-dw * 0.5f + bw * (i + 0.5f), dh * 0.5f + 0.02f, z),
                   new Vector3(bw - 0.012f, dh + Jf(0.03f), 0.07f), Quaternion.Euler(0, 0, Jf(0.8f)), 0.02f);
        wl.Box(new Vector3(0, dh * 0.25f, z - 0.06f), new Vector3(dw, 0.09f, 0.04f), Quaternion.identity, 0.03f);
        wl.Box(new Vector3(0, dh * 0.75f, z - 0.06f), new Vector3(dw, 0.09f, 0.04f), Quaternion.identity, 0.03f);
        c.Get(MatId.Stone).Box(new Vector3(dw * 0.5f - 0.1f, dh * 0.5f, z - 0.08f), new Vector3(0.05f, 0.12f, 0.05f), Quaternion.identity, 0.03f);
    }

    void BuildWindow(StageCtx c)
    {
        var wl = c.Get(MatId.WoodL);
        var wd = c.Get(MatId.WoodD);
        float x = W * 0.5f;
        float h = winTop - winBottom;
        float yc = (winTop + winBottom) * 0.5f;
        float fd = d * 1.05f;

        wl.Box(new Vector3(x, yc, winZ - winW * 0.5f + 0.04f), new Vector3(fd, h, 0.08f), Quaternion.identity, 0.02f);
        wl.Box(new Vector3(x, yc, winZ + winW * 0.5f - 0.04f), new Vector3(fd, h, 0.08f), Quaternion.identity, 0.02f);
        wl.Box(new Vector3(x, winBottom + 0.04f, winZ), new Vector3(fd, 0.08f, winW), Quaternion.identity, 0.02f);
        wl.Box(new Vector3(x, winTop - 0.04f, winZ), new Vector3(fd, 0.08f, winW), Quaternion.identity, 0.02f);
        wd.Box(new Vector3(x, yc, winZ), new Vector3(0.07f, h - 0.1f, 0.05f), Quaternion.identity, 0.02f);
        wd.Box(new Vector3(x, yc, winZ), new Vector3(0.07f, 0.05f, winW - 0.1f), Quaternion.identity, 0.02f);
        c.Get(MatId.Glass).Box(new Vector3(x, yc, winZ), new Vector3(0.03f, h - 0.12f, winW - 0.12f), Quaternion.identity, 0f);
        wd.Box(new Vector3(x + r + 0.04f, winBottom - 0.02f, winZ), new Vector3(0.2f, 0.06f, winW + 0.24f), Quaternion.identity, 0.02f);

        // ставни, распахнутые наружу
        float sw = winW * 0.5f + 0.02f;
        float phi = 70f * Mathf.Deg2Rad;
        for (int sg = -1; sg <= 1; sg += 2)
        {
            Vector3 hinge = new Vector3(x + r + 0.06f, yc, winZ + sg * (winW * 0.5f + 0.02f));
            Vector3 dir = new Vector3(Mathf.Sin(phi), 0, -sg * Mathf.Cos(phi));
            Vector3 ctr = hinge + dir * (sw * 0.5f);
            Quaternion q = Quaternion.LookRotation(dir, Vector3.up);
            wd.Box(ctr, new Vector3(0.05f, h, sw), q, 0.03f);
            wl.Box(ctr + new Vector3(0.035f, h * 0.28f, 0), new Vector3(0.07f, 0.07f, sw * 0.9f), q, 0.03f);
            wl.Box(ctr + new Vector3(0.035f, -h * 0.28f, 0), new Vector3(0.07f, 0.07f, sw * 0.9f), q, 0.03f);
        }
    }

    void BuildPorch(StageCtx c)
    {
        float pw = doorW + 0.8f;
        float zA = -L * 0.5f - 0.2f, zB = -L * 0.5f - 1.0f;
        int cnt = 4;
        float bd = (zA - zB) / cnt;
        var wl = c.Get(MatId.WoodL);
        var wd = c.Get(MatId.WoodD);
        for (int i = 0; i < cnt; i++)
        {
            var mb = (i % 2 == 0) ? wl : wd;
            mb.Box(new Vector3(Jf(0.01f), -0.11f, zA - (i + 0.5f) * bd), new Vector3(pw, 0.18f, bd - 0.015f),
                   Quaternion.Euler(0, Jf(1f), 0), 0.02f);
        }
        wd.Box(new Vector3(0, -0.2f, zB - 0.17f), new Vector3(pw * 0.85f, 0.12f, 0.3f), Quaternion.identity, 0.03f);
        AddCol(c, new Vector3(0, -0.11f, (zA + zB) * 0.5f), new Vector3(pw, 0.18f, zA - zB));
        AddCol(c, new Vector3(0, -0.2f, zB - 0.17f), new Vector3(pw * 0.85f, 0.12f, 0.3f));
    }

    void BuildChimney(StageCtx c)
    {
        float cx = -(W * 0.5f + 0.6f), cz = 0f;
        float baseY = GY(cx, cz) - 0.15f;
        float topChim = ridgeY + 0.55f;
        int layers = Mathf.CeilToInt((topChim - baseY) / 0.32f);
        var st = c.Get(MatId.Stone);
        for (int i = 0; i < layers; i++)
        {
            float y = baseY + 0.16f + i * 0.32f;
            float w = (i < 3) ? 1.15f : 0.95f;
            st.Box(new Vector3(cx + Jf(0.02f), y, cz + Jf(0.02f)),
                   new Vector3(w * Rf(0.97f, 1.03f), 0.32f, w * Rf(0.97f, 1.03f)),
                   Quaternion.Euler(0, Jf(5f), 0), 0.04f);
        }
        float capY = baseY + 0.32f * layers + 0.06f;
        st.Box(new Vector3(cx, capY, cz), new Vector3(1.1f, 0.14f, 1.1f), Quaternion.Euler(0, Jf(4f), 0), 0.03f);
        float top = capY + 0.07f;
        AddCol(c, new Vector3(cx, (baseY + top) * 0.5f, cz), new Vector3(1.0f, top - baseY, 1.0f));
        if (S.decSmoke) MakeSmoke(c.root, new Vector3(cx, top + 0.05f, cz));
    }

    void MakeSmoke(Transform parent, Vector3 pos)
    {
        if (smokeMesh == null)
        {
            var mb = new HutMesh(rng);
            mb.Ico(Vector3.zero, 0.5f, 0.12f);
            smokeMesh = SaveMesh(mb.ToMesh("Hut_SmokePuff"), "Hut_SmokePuff");
        }
        var go = new GameObject("Smoke");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);   // эмиссия вдоль +Z -> вверх

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.startLifetime = 3.2f;
        main.startSpeed = 0.7f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 30;

        var em = ps.emission;
        em.rateOverTime = 3f;

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle = 6f;
        sh.radius = 0.08f;

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        var curve = new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(0.35f, 1f), new Keyframe(1f, 0f));
        sol.size = new ParticleSystem.MinMaxCurve(1f, curve);

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = 0.2f;
        vel.y = 0f;
        vel.z = 0f;

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.renderMode = ParticleSystemRenderMode.Mesh;
        psr.mesh = smokeMesh;
        psr.sharedMaterial = GetMat(MatId.Smoke);
        psr.shadowCastingMode = ShadowCastingMode.Off;
        psr.receiveShadows = false;
    }

    void BuildWoodpile(StageCtx c)
    {
        float cx = -(W * 0.5f + 0.65f);
        float wz = Mathf.Max(L * 0.3f, 1.5f);
        float g = GY(cx, wz);
        var wd = c.Get(MatId.WoodD);
        for (int sz = -1; sz <= 1; sz += 2)
            for (int sx = -1; sx <= 1; sx += 2)
                wd.Box(new Vector3(cx + sx * 0.32f, g + 0.5f, wz + sz * 0.85f), new Vector3(0.09f, 1.0f, 0.09f), Quaternion.identity, 0.04f);
        for (int sx = -1; sx <= 1; sx += 2)
            wd.Box(new Vector3(cx + sx * 0.2f, g + 0.04f, wz), new Vector3(0.09f, 0.08f, 1.8f), Quaternion.identity, 0.03f);
        for (int row = 0; row < 4; row++)
        {
            int cols = (row % 2 == 0) ? 8 : 7;
            for (int col = 0; col < cols; col++)
            {
                float z = wz + (col - (cols - 1) * 0.5f) * 0.2f;
                float y = g + 0.13f + row * 0.17f;
                var mb = c.Get(rng.NextDouble() < 0.5 ? MatId.WoodL : MatId.WoodD);
                mb.Cyl(new Vector3(cx - 0.27f + Jf(0.03f), y + Jf(0.01f), z),
                       new Vector3(cx + 0.27f + Jf(0.03f), y, z), 0.085f, 0.085f, 6, 0.06f);
            }
        }
    }

    void BuildStump(StageCtx c)
    {
        float sx = W * 0.5f + 2.4f, sz = -L * 0.5f - 0.6f;
        float g = GY(sx, sz);
        c.Get(MatId.WoodD).Cyl(new Vector3(sx, g - 0.05f, sz), new Vector3(sx, g + 0.45f, sz), 0.36f, 0.3f, 8, 0.05f);
        c.Get(MatId.WoodL).Cyl(new Vector3(sx, g + 0.44f, sz), new Vector3(sx, g + 0.47f, sz), 0.29f, 0.29f, 8, 0.03f);
        // топор: лезвие воткнуто в пень, рукоять торчит вверх
        c.Get(MatId.WoodL).Beam(new Vector3(sx + 0.02f, g + 0.5f, sz), new Vector3(sx + 0.45f, g + 1.05f, sz), 0.05f, 0.05f, 0.01f);
        c.Get(MatId.Stone).Box(new Vector3(sx, g + 0.42f, sz), new Vector3(0.05f, 0.2f, 0.22f), Quaternion.Euler(0, 0, -10f), 0.03f);
    }

    void BuildLantern(StageCtx c)
    {
        float x = doorW * 0.5f + 0.55f;
        float z0 = -L * 0.5f - r;
        float by = Mathf.Min(doorH * 0.9f, topY - 0.5f);
        var wd = c.Get(MatId.WoodD);
        wd.Beam(new Vector3(x, by + 0.3f, z0 + 0.05f), new Vector3(x, by + 0.3f, z0 - 0.45f), 0.06f, 0.06f, 0.01f);
        wd.Beam(new Vector3(x, by - 0.05f, z0 + 0.05f), new Vector3(x, by + 0.28f, z0 - 0.4f), 0.04f, 0.04f, 0.01f);
        float ly = by + 0.3f - 0.14f;
        var lp = new Vector3(x, ly - 0.12f, z0 - 0.45f);
        c.Get(MatId.Stone).Beam(new Vector3(x, by + 0.27f, z0 - 0.45f), new Vector3(x, ly, z0 - 0.45f), 0.02f, 0.02f, 0f);
        c.Get(MatId.Lamp).Box(lp, new Vector3(0.16f, 0.2f, 0.16f), Quaternion.identity, 0.02f);
        wd.Box(lp + Vector3.up * 0.12f, new Vector3(0.22f, 0.04f, 0.22f), Quaternion.identity, 0.02f);
        wd.Box(lp - Vector3.up * 0.12f, new Vector3(0.22f, 0.04f, 0.22f), Quaternion.identity, 0.02f);

        var lg = new GameObject("LanternLight");
        lg.transform.SetParent(c.root, false);
        lg.transform.localPosition = lp;
        var l = lg.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = 5f;
        l.intensity = 2.2f;
        l.color = new Color(1f, 0.78f, 0.45f);
        l.shadows = LightShadows.None;
    }

    void PlaceDecor(Transform parent)
    {
        var list = Valid(decorPrefabs);
        if (list.Count == 0) return;
        var holder = NewChild(parent, "DecorPrefabs");
        float R = Mathf.Max(W, L) * 0.5f + 3.2f;
        for (int i = 0; i < list.Count; i++)
        {
            float ang = (100f + i * 53f) * Mathf.Deg2Rad;
            float x = Mathf.Cos(ang) * R * 0.9f;
            float z = Mathf.Sin(ang) * R;
            PlacePrefab(list[i], holder, new Vector3(x, GY(x, z), z), Rf(0f, 360f));
        }
    }
}