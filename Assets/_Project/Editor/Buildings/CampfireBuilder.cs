// Tools > BuilderSim > Campfire Builder
// Процедурный костёр в 3 этапа. Unity 6 (6000.3), URP.
// Требует в проекте: HutBuilder.cs (класс HutMesh), HutStages.cs, CampfireFlicker.cs,
// BuildingController.cs
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using static EditorAssets;

public class CampfireBuilder : EditorWindow
{
    const string Dir = "Assets/_Game/Campfire";
    const string PrefKey = "BuilderSim.CampfireBuilder.v1";
    const string RootName = "Campfire";

    [System.Serializable]
    public class Settings
    {
        public float radius = 0.9f, firewood = 0.1f;
        public int seatCount = 4;
        public int seed = 777;
        public Color woodLight = new Color(0.66f, 0.44f, 0.24f);
        public Color woodDark = new Color(0.40f, 0.26f, 0.14f);
        public Color stone = new Color(0.60f, 0.58f, 0.64f);
        public Color moss = new Color(0.36f, 0.66f, 0.22f);
        public Color tent = new Color(0.82f, 0.32f, 0.24f);
        public Color flameA = new Color(1.00f, 0.45f, 0.12f);
        public Color flameB = new Color(1.00f, 0.82f, 0.25f);
        public bool decGrass = true, decWoodpile = true;                    // этап 1
        public bool decSeats = true, decTripod = true, decTent = true;       // этап 2
        public bool decPot = true, decSticks = true, decSparks = true, decSmoke = true; // этап 3
        public bool decZone = true;                                          // зона сдачи
        public int costStage2 = 4, costStage3 = 4;                           // цена этапов в брёвнах
        public Vector3 position;
        public float yaw;
    }

    enum MatId { WoodL, WoodD, Stone, Ash, Moss, FlameA, FlameB, Smoke, Tent, Metal }

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

    Settings S = new Settings();
    [SerializeField] GameObject[] seatPrefabs;
    [SerializeField] GameObject[] decorPrefabs;
    Vector2 scroll;

    System.Random rng;
    Transform root;
    readonly Dictionary<MatId, Material> matCache = new Dictionary<MatId, Material>();
    Mesh smokeMesh, sparkMesh;
    float R, fr;

    [MenuItem("Tools/BuilderSim/Здания/Campfire Builder", false, 304)]
    static void Open()
    {
        var w = GetWindow<CampfireBuilder>(false, "Костёр");
        w.minSize = new Vector2(360, 520);
        w.Show();
    }

    // ---------------- сохранение ----------------
    void OnEnable() { Load(); SceneView.duringSceneGui += OnSceneGUI; }
    void OnDisable() { SceneView.duringSceneGui -= OnSceneGUI; Save(); }
    void Save() { EditorPrefs.SetString(PrefKey, JsonUtility.ToJson(S)); }

    void SavePrefabs()
    {
        EditorPrefs.SetString(PrefKey + ".seats", PathsOf(seatPrefabs));
        EditorPrefs.SetString(PrefKey + ".decor", PathsOf(decorPrefabs));
    }

    void Load()
    {
        if (EditorPrefs.HasKey(PrefKey)) JsonUtility.FromJsonOverwrite(EditorPrefs.GetString(PrefKey), S);
        seatPrefabs = FromPaths(EditorPrefs.GetString(PrefKey + ".seats", ""));
        decorPrefabs = FromPaths(EditorPrefs.GetString(PrefKey + ".decor", ""));
    }

    // центр карты: центр активного Terrain, иначе (0,0,0)
    static Vector3 MapCenter()
    {
        var t = Terrain.activeTerrain;
        if (t != null && t.terrainData != null)
        {
            Vector3 size = t.terrainData.size, p = t.transform.position;
            return new Vector3(p.x + size.x * 0.5f, 0f, p.z + size.z * 0.5f);
        }
        return Vector3.zero;
    }

    // ---------------- окно ----------------
    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("Костёр", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();

        EditorGUILayout.LabelField("Параметры", EditorStyles.boldLabel);
        S.radius = EditorGUILayout.Slider("Радиус кольца (м)", S.radius, 0.6f, 1.6f);
        S.firewood = EditorGUILayout.Slider("Толщина поленьев (м)", S.firewood, 0.06f, 0.18f);
        S.seatCount = EditorGUILayout.IntSlider("Лавок вокруг", S.seatCount, 0, 8);
        S.seed = EditorGUILayout.IntField("Seed", S.seed);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Цвета", EditorStyles.boldLabel);
        S.woodLight = EditorGUILayout.ColorField("Светлое дерево", S.woodLight);
        S.woodDark = EditorGUILayout.ColorField("Тёмное дерево", S.woodDark);
        S.stone = EditorGUILayout.ColorField("Камень", S.stone);
        S.moss = EditorGUILayout.ColorField("Трава / мох", S.moss);
        S.tent = EditorGUILayout.ColorField("Палатка", S.tent);
        S.flameA = EditorGUILayout.ColorField("Пламя (внешнее)", S.flameA);
        S.flameB = EditorGUILayout.ColorField("Пламя (внутреннее)", S.flameB);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Декор: этап 1", EditorStyles.boldLabel);
        S.decGrass = EditorGUILayout.Toggle("Трава у кольца", S.decGrass);
        S.decWoodpile = EditorGUILayout.Toggle("Кучка поленьев и веточки", S.decWoodpile);
        EditorGUILayout.LabelField("Декор: этап 2", EditorStyles.boldLabel);
        S.decSeats = EditorGUILayout.Toggle("Лавки-брёвна", S.decSeats);
        S.decTripod = EditorGUILayout.Toggle("Тренога", S.decTripod);
        S.decTent = EditorGUILayout.Toggle("Палатка", S.decTent);
        EditorGUILayout.LabelField("Декор: этап 3", EditorStyles.boldLabel);
        S.decPot = EditorGUILayout.Toggle("Котелок на цепи", S.decPot);
        S.decSticks = EditorGUILayout.Toggle("Палочки с зефиром", S.decSticks);
        S.decSparks = EditorGUILayout.Toggle("Искры", S.decSparks);
        S.decSmoke = EditorGUILayout.Toggle("Дым", S.decSmoke);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Зона сдачи брёвен", EditorStyles.boldLabel);
        S.decZone = EditorGUILayout.Toggle("Невидимая зона вокруг костра", S.decZone);
        using (new EditorGUI.DisabledScope(!S.decZone))
        {
            S.costStage2 = EditorGUILayout.IntField("Цена этапа 2 (брёвен)", S.costStage2);
            S.costStage3 = EditorGUILayout.IntField("Цена этапа 3 (брёвен)", S.costStage3);
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Положение (или ручки в Scene)", EditorStyles.boldLabel);
        S.position = EditorGUILayout.Vector3Field("Позиция", S.position);
        S.yaw = EditorGUILayout.FloatField("Поворот Y (°)", S.yaw);

        if (EditorGUI.EndChangeCheck()) { Save(); MoveExisting(); SceneView.RepaintAll(); }

        if (GUILayout.Button("Поставить в центр карты"))
        {
            S.position = MapCenter();
            Save();
            MoveExisting();
            SceneView.RepaintAll();
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Свои префабы (пусто = процедурные меши)", EditorStyles.boldLabel);
        var so = new SerializedObject(this);
        so.Update();
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(so.FindProperty("seatPrefabs"), new GUIContent("Лавки (по кругу)"), true);
        EditorGUILayout.PropertyField(so.FindProperty("decorPrefabs"), new GUIContent("Декор (вокруг)"), true);
        bool ch = EditorGUI.EndChangeCheck();
        so.ApplyModifiedProperties();
        if (ch) SavePrefabs();

        EditorGUILayout.Space(8);
        if (GUILayout.Button("Построить костёр", GUILayout.Height(32))) EditorApplication.delayCall += DoBuild;
        if (GUILayout.Button("Удалить костёр", GUILayout.Height(24))) EditorApplication.delayCall += DoDelete;

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Предпросмотр этапа", EditorStyles.miniBoldLabel);
        EditorGUILayout.BeginHorizontal();
        for (int i = 1; i <= 3; i++)
            if (GUILayout.Button("Этап " + i)) ShowStage(i);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.HelpBox("Окно должно быть открыто, чтобы в Scene были кнопка и ручки. В игре костёр всегда стартует с этапа 1.", MessageType.None);
        EditorGUILayout.EndScrollView();
    }

    void DoBuild() { if (this != null) Build(); }
    void DoDelete() { if (this != null) Delete(); }

    void ShowStage(int stage)
    {
        var hut = FindRoot();
        if (hut == null) return;
        var hs = hut.GetComponent<HutStages>();
        if (hs != null) hs.SetStage(stage);
    }

    // ---------------- Scene ----------------
    void OnSceneGUI(SceneView sv)
    {
        Handles.BeginGUI();
        GUILayout.BeginArea(new Rect(10, 10, 190, 40));
        if (GUILayout.Button("Построить костёр", GUILayout.Height(28))) EditorApplication.delayCall += DoBuild;
        GUILayout.EndArea();
        Handles.EndGUI();

        Handles.color = new Color(1f, 0.6f, 0.2f, 0.9f);
        Handles.DrawWireDisc(S.position, Vector3.up, S.radius);
        Handles.DrawWireDisc(S.position, Vector3.up, S.radius + 1.25f);

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
        var go = FindRoot();
        if (go == null) return;
        float y;
        if (!GroundY(S.position, out y)) y = go.transform.position.y;
        Undo.RecordObject(go.transform, "Переместить костёр");
        go.transform.SetPositionAndRotation(new Vector3(S.position.x, y, S.position.z), Quaternion.Euler(0, S.yaw, 0));
    }

    static GameObject FindRoot()
    {
        foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
            if (go.name == RootName) return go;
        return null;
    }

    void Delete()
    {
        var go = FindRoot();
        if (go != null) Undo.DestroyObjectImmediate(go);
    }

    // ---------------- земля ----------------
    // Землёй считаем Terrain и объекты Ground / Terrain*. Сам костёр луч не учитывает.
    static bool IsGround(Collider c)
    {
        if (c is TerrainCollider) return true;
        string n = c.gameObject.name;
        return n == "Ground" || n.StartsWith("Terrain");
    }

    static bool GroundY(Vector3 wp, out float y)
    {
        y = 0f;
        var camp = FindRoot();
        var hits = Physics.RaycastAll(new Vector3(wp.x, wp.y + 500f, wp.z), Vector3.down, 1500f, ~0,
                                      QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        bool ok = false;
        foreach (var h in hits)
        {
            if (camp != null && h.collider.transform.IsChildOf(camp.transform)) continue;
            if (!IsGround(h.collider)) continue;
            if (h.distance < best) { best = h.distance; y = h.point.y; ok = true; }
        }
        return ok;
    }

    float GY(float x, float z)
    {
        Vector3 wp = root.TransformPoint(new Vector3(x, 0, z));
        float y;
        if (GroundY(wp, out y)) return root.InverseTransformPoint(new Vector3(wp.x, y, wp.z)).y;
        return 0f;
    }

    // ---------------- утилиты ----------------
    float Rf(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }
    float Jf(float a) { return Rf(-a, a); }
    static float TanYaw(float a) { return -(a * Mathf.Rad2Deg + 90f); }

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
            case MatId.Ash: col = new Color(0.22f, 0.20f, 0.23f); break;
            case MatId.Moss: col = S.moss; break;
            case MatId.FlameA: col = S.flameA; emis = 1.8f; break;
            case MatId.FlameB: col = S.flameB; emis = 2.0f; break;
            case MatId.Smoke: col = new Color(0.88f, 0.88f, 0.9f); break;
            case MatId.Tent: col = S.tent; break;
            default: col = new Color(0.35f, 0.36f, 0.42f); break;
        }
        string path = Dir + "/Camp_" + id + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            m = new Material(sh) { name = "Camp_" + id };
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

    StageCtx NewCtx(Transform stageRoot, string tag) { return new StageCtx { root = stageRoot, tag = tag, rng = rng }; }

    void Flush(StageCtx c)
    {
        foreach (var kv in c.mbs)
        {
            if (kv.Value.Count == 0) continue;
            string nm = "Camp_" + c.tag + "_" + kv.Key;
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
        go.AddComponent<BoxCollider>().size = size;
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

    // ---------------- сборка ----------------
    void Build()
    {
        EnsureFolder(Dir);
        var old = FindRoot();
        if (old != null) Undo.DestroyObjectImmediate(old);

        rng = new System.Random(S.seed);
        matCache.Clear();
        smokeMesh = null; sparkMesh = null;
        R = S.radius; fr = S.firewood;
        Physics.SyncTransforms();

        float gy;
        if (!GroundY(S.position, out gy))
        {
            gy = S.position.y;
            Debug.LogWarning("CampfireBuilder: земля под костром не найдена (нужен Terrain или объект Ground), высота взята из ручки.");
        }

        var go = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(go, "Построить костёр");
        root = go.transform;
        root.SetPositionAndRotation(new Vector3(S.position.x, gy, S.position.z), Quaternion.Euler(0, S.yaw, 0));

        var s1 = NewChild(root, "Stage1");
        var s2 = NewChild(root, "Stage2");
        var s3 = NewChild(root, "Stage3");
        BuildStage1(s1);
        BuildStage2(s2);
        BuildStage3(s3);

        var bp = NewChild(root, "BuildPoint");
        bp.localPosition = new Vector3(0, GY(0, -R - 2.6f), -R - 2.6f);
        bp.localRotation = Quaternion.identity;

        var stages = go.AddComponent<HutStages>();
        stages.Configure(s1.gameObject, s2.gameObject, s3.gameObject, bp, 1);
        stages.SetStage(3);

        if (S.decZone) BuildZone(root, stages);

        ApplyStatic(root);
        Selection.activeGameObject = go;
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(go.scene);
    }

    // Невидимый блок вокруг костра: сюда игрок сдаёт брёвна. Рендера нет, в Scene виден зелёным гизмо.
    void BuildZone(Transform rootT, HutStages stages)
    {
        var zg = new GameObject("BuildZone");
        zg.transform.SetParent(rootT, false);

        float side = R * 2f + 6f;
        var col = zg.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(side, 3f, side);
        col.center = new Vector3(0f, 1.5f, 0f);

        var bc = zg.AddComponent<BuildingController>();
        bc.stages = stages;
        bc.startStage = 1;
        bc.stageCosts = new[] { Mathf.Max(1, S.costStage2), Mathf.Max(1, S.costStage3) };
        bc.woodRequired = bc.stageCosts[0];
        bc.useRing = true;
        bc.ringOffset = new Vector3(0f, 3f, 0f);
        bc.rewardXp = 10;
    }

    static void ApplyStatic(Transform t)
    {
        var flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic;
        foreach (var tr in t.GetComponentsInChildren<Transform>(true))
        {
            string n = tr.name;
            if (n == "Smoke" || n == "Sparks" || n == "CampLight" || n == "Flames") continue;
            if (n == "BuildZone" || n == "CostTag") continue;
            if (tr.IsChildOf(t) && tr.parent != null && tr.parent.name == "Flames") continue;
            GameObjectUtility.SetStaticEditorFlags(tr.gameObject, flags);
        }
    }

    // ===== ЭТАП 1: место под костёр =====
    void BuildStage1(Transform parent)
    {
        var c = NewCtx(parent, "S1");
        c.Get(MatId.Ash).Cyl(new Vector3(0, -0.02f, 0), new Vector3(0, 0.04f, 0), R * 0.85f, R * 0.85f, 9, 0.05f);

        var st = c.Get(MatId.Stone);
        int n = Mathf.Max(8, Mathf.RoundToInt(2f * Mathf.PI * R / 0.42f));
        for (int i = 0; i < n; i++)
        {
            float a = (i + Jf(0.15f)) * Mathf.PI * 2f / n;
            float h = Rf(0.2f, 0.3f);
            st.Box(new Vector3(Mathf.Cos(a) * R, h * 0.5f - 0.04f, Mathf.Sin(a) * R),
                   new Vector3(Rf(0.3f, 0.4f), h, Rf(0.24f, 0.32f)),
                   Quaternion.Euler(0, TanYaw(a) + Jf(10f), 0), 0.08f);
        }
        AddCol(c, new Vector3(0, 0.15f, 0), new Vector3(R * 2f + 0.5f, 0.3f, R * 2f + 0.5f));

        if (S.decGrass)
        {
            var mo = c.Get(MatId.Moss);
            for (int i = 0; i < 14; i++)
            {
                float a = Rf(0f, Mathf.PI * 2f), rr = R + Rf(0.4f, 1.2f);
                float x = Mathf.Cos(a) * rr, z = Mathf.Sin(a) * rr, g = GY(x, z);
                for (int k = 0; k < 3; k++)
                {
                    var b = new Vector3(x + Jf(0.12f), g - 0.02f, z + Jf(0.12f));
                    mo.Cyl(b, b + new Vector3(Jf(0.05f), Rf(0.14f, 0.26f), Jf(0.05f)), 0.07f, 0.008f, 4, 0.1f);
                }
            }
        }

        if (S.decWoodpile)
        {
            float pa = 200f * Mathf.Deg2Rad;
            float px = Mathf.Cos(pa) * (R + 2.0f), pz = Mathf.Sin(pa) * (R + 2.0f);
            float g = GY(px, pz);
            int[] rows = { 4, 3, 2, 1 };
            for (int row = 0; row < rows.Length; row++)
                for (int i = 0; i < rows[row]; i++)
                {
                    float z = pz + (i - (rows[row] - 1) * 0.5f) * fr * 2.1f;
                    float y = g + fr + row * fr * 1.75f;
                    float len = Rf(0.8f, 1.0f);
                    float xs = Jf(0.05f);
                    var mb = c.Get(rng.NextDouble() < 0.6 ? MatId.WoodL : MatId.WoodD);
                    mb.Cyl(new Vector3(px + xs - len * 0.5f, y, z), new Vector3(px + xs + len * 0.5f, y, z), fr, fr, 6, 0.06f);
                }
            var wd = c.Get(MatId.WoodD);
            for (int i = 0; i < 8; i++)
            {
                float a = Rf(0f, Mathf.PI * 2f), rr = R + Rf(0.6f, 1.4f);
                float x = Mathf.Cos(a) * rr, z = Mathf.Sin(a) * rr, gg = GY(x, z) + 0.03f;
                float da = Rf(0f, Mathf.PI * 2f), len = Rf(0.5f, 0.9f);
                wd.Cyl(new Vector3(x, gg, z), new Vector3(x + Mathf.Cos(da) * len, gg, z + Mathf.Sin(da) * len), 0.025f, 0.02f, 4, 0.1f);
            }
        }
        Flush(c);
    }

    // ===== ЭТАП 2: дрова, лавки, палатка =====
    void BuildStage2(Transform parent)
    {
        var c = NewCtx(parent, "S2");

        // шалаш из поленьев над ямой
        int logs = 7;
        for (int i = 0; i < logs; i++)
        {
            float a = i * Mathf.PI * 2f / logs + Jf(0.2f);
            var b = new Vector3(Mathf.Cos(a) * R * 0.6f, 0.06f, Mathf.Sin(a) * R * 0.6f);
            var t = new Vector3(Jf(0.05f), 0.95f + Jf(0.06f), Jf(0.05f));
            c.Get(i % 2 == 0 ? MatId.WoodL : MatId.WoodD).Cyl(b, t, fr * 1.05f, fr * 0.8f, 6, 0.05f);
        }
        AddCol(c, new Vector3(0, 0.45f, 0), new Vector3(R * 1.6f, 0.9f, R * 1.6f));

        if (S.decSeats && S.seatCount > 0) BuildSeats(c, parent);
        if (S.decTripod) BuildTripod(c);
        if (S.decTent) BuildTent(c);
        Flush(c);
    }

    void BuildSeats(StageCtx c, Transform parent)
    {
        var prefabs = Valid(seatPrefabs);
        float sr = R + 1.25f;
        for (int i = 0; i < S.seatCount; i++)
        {
            float a = (i + 0.5f) * Mathf.PI * 2f / S.seatCount;
            float x = Mathf.Cos(a) * sr, z = Mathf.Sin(a) * sr, g = GY(x, z);
            if (prefabs.Count > 0)
            {
                float yaw = Mathf.Atan2(-Mathf.Cos(a), -Mathf.Sin(a)) * Mathf.Rad2Deg;
                PlacePrefab(prefabs[i % prefabs.Count], parent, new Vector3(x, g, z), yaw);
                continue;
            }
            var tg = new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a));
            var ctr = new Vector3(x, g + 0.19f, z);
            c.Get(i % 2 == 0 ? MatId.WoodL : MatId.WoodD).Cyl(ctr - tg * 0.75f, ctr + tg * 0.75f, 0.17f, 0.17f, 7, 0.05f);
            var wd = c.Get(MatId.WoodD);
            for (int s = -1; s <= 1; s += 2)
                wd.Box(new Vector3(x, g + 0.05f, z) + tg * (s * 0.5f), new Vector3(0.18f, 0.1f, 0.18f), Quaternion.identity, 0.05f);
            AddCol(c, new Vector3(x, g + 0.2f, z), new Vector3(1.5f, 0.4f, 0.4f), Quaternion.Euler(0, TanYaw(a) + 90f, 0));
        }
    }

    void BuildTripod(StageCtx c)
    {
        var wd = c.Get(MatId.WoodD);
        var apex = new Vector3(0, 1.5f, 0);
        for (int i = 0; i < 3; i++)
        {
            float a = (30f + i * 120f) * Mathf.Deg2Rad;
            float x = Mathf.Cos(a) * (R * 1.15f + 0.1f), z = Mathf.Sin(a) * (R * 1.15f + 0.1f);
            wd.Beam(new Vector3(x, GY(x, z) - 0.05f, z), apex, 0.06f, 0.06f, 0.03f);
        }
        wd.Box(apex, new Vector3(0.14f, 0.1f, 0.14f), Quaternion.Euler(0, Jf(20f), 0), 0.05f);
    }

    void BuildTent(StageCtx c)
    {
        float dist = R + 4.2f;
        float cx = 0f, cz = dist;
        float g = GY(cx, cz);
        Quaternion yq = Quaternion.Euler(0, Jf(10f), 0);
        Vector3 org = new Vector3(cx, g, cz);
        float hw = 1.0f, H = 1.3f, hl = 1.1f;
        var tm = c.Get(MatId.Tent);

        for (int sg = -1; sg <= 1; sg += 2)
        {
            Vector3 dir = new Vector3(sg * hw, -H, 0).normalized;
            float sl = Mathf.Sqrt(hw * hw + H * H);
            Vector3 nrm = sg * Vector3.Cross(Vector3.forward, dir);
            Quaternion q = Quaternion.LookRotation(Vector3.forward, nrm);
            Vector3 mid = org + yq * new Vector3(sg * hw * 0.5f, H * 0.5f, 0) + yq * nrm * 0.03f;
            tm.Box(mid, new Vector3(sl, 0.06f, hl * 2f), yq * q, 0.01f);
        }
        Vector3 inside = org + yq * new Vector3(0, H * 0.3f, 0);
        for (int e = -1; e <= 1; e += 2)
            tm.TriOut(org + yq * new Vector3(-hw, 0, e * hl), org + yq * new Vector3(hw, 0, e * hl),
                      org + yq * new Vector3(0, H, e * hl), inside);
        // тёмный вход со стороны костра (-Z)
        float zf = -hl - 0.01f;
        c.Get(MatId.Ash).TriOut(org + yq * new Vector3(-0.4f, 0.0f, zf), org + yq * new Vector3(0.4f, 0.0f, zf),
                                org + yq * new Vector3(0, 0.9f, zf), inside);
        AddCol(c, org + yq * new Vector3(0, 0.6f, 0), new Vector3(2f, 1.2f, 2.2f), yq);
    }

    // ===== ЭТАП 3: костёр горит =====
    void BuildStage3(Transform parent)
    {
        // пламя: отдельный не-статичный объект (его масштабирует мерцание)
        var flames = NewChild(parent, "Flames");
        flames.localPosition = new Vector3(0, 0.25f, 0);
        var fc = NewCtx(flames, "S3Flames");
        var fa = fc.Get(MatId.FlameA);
        var fb = fc.Get(MatId.FlameB);
        // мультяшное пламя: «капли» из ступеней (широкое основание, мягкий изгиб, острый кончик), крупные языки как на референсе
        Teardrop(fa, new Vector3(0f, 0f, 0f), 1.25f, 0.36f, new Vector3(Jf(0.08f), 0f, Jf(0.08f)));
        Teardrop(fb, new Vector3(0f, 0.02f, 0f), 0.85f, 0.22f, new Vector3(Jf(0.05f), 0f, Jf(0.05f)));
        for (int i = 0; i < 5; i++)
        {
            float a = i * Mathf.PI * 2f / 5f + Jf(0.3f);
            var b = new Vector3(Mathf.Cos(a) * 0.24f, 0.02f, Mathf.Sin(a) * 0.24f);
            var lean = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.20f;
            Teardrop(fa, b, Rf(0.55f, 0.85f), 0.15f, lean);
        }
        for (int i = 0; i < 3; i++)
        {
            float a = i * Mathf.PI * 2f / 3f + 0.5f + Jf(0.3f);
            var b = new Vector3(Mathf.Cos(a) * 0.14f, 0.04f, Mathf.Sin(a) * 0.14f);
            Teardrop(fb, b, Rf(0.35f, 0.55f), 0.09f, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.10f);
        }
        fb.Cyl(new Vector3(0, -0.2f, 0), new Vector3(0, -0.15f, 0), R * 0.6f, R * 0.6f, 9, 0.05f); // жар на золе
        Flush(fc);

        // свет
        var lg = new GameObject("CampLight");
        lg.transform.SetParent(parent, false);
        lg.transform.localPosition = new Vector3(0, 0.9f, 0);
        var l = lg.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = 8f;
        l.intensity = 3f;
        l.color = new Color(1f, 0.6f, 0.25f);
        l.shadows = LightShadows.None;
        parent.gameObject.AddComponent<CampfireFlicker>().Configure(l, flames);

        if (S.decSparks)
        {
            if (sparkMesh == null)
            {
                var mb = new HutMesh(rng);
                mb.Ico(Vector3.zero, 0.5f, 0.05f);
                sparkMesh = SaveMesh(mb.ToMesh("Camp_SparkMesh"), "Camp_SparkMesh");
            }
            MakeParticles(parent, "Sparks", new Vector3(0, 0.9f, 0), sparkMesh, MatId.FlameB,
                          6f, 1.6f, 1.2f, 0.03f, 0.06f, 20f, 0.1f, -0.08f);
        }
        if (S.decSmoke)
        {
            if (smokeMesh == null)
            {
                var mb = new HutMesh(rng);
                mb.Ico(Vector3.zero, 0.5f, 0.12f);
                smokeMesh = SaveMesh(mb.ToMesh("Camp_SmokePuff"), "Camp_SmokePuff");
            }
            MakeParticles(parent, "Smoke", new Vector3(0, 1.3f, 0), smokeMesh, MatId.Smoke,
                          2f, 3.5f, 0.6f, 0.25f, 0.45f, 8f, 0.12f, -0.02f);
        }

        var c = NewCtx(parent, "S3");
        if (S.decPot) BuildPot(c);
        if (S.decSticks) BuildSticks(c);
        Flush(c);
        PlaceDecor(parent);
    }

    // «капля» пламени: 5 ступеней вверх, радиус плавно сужается до острого кончика, верх уходит в сторону lean (мягкий изгиб языка)
    static readonly float[] DropH = { 0f, 0.22f, 0.48f, 0.72f, 0.90f, 1f };
    static readonly float[] DropR = { 0.92f, 1.00f, 0.80f, 0.50f, 0.20f, 0.02f };
    void Teardrop(HutMesh m, Vector3 basePos, float height, float radius, Vector3 lean)
    {
        for (int i = 0; i + 1 < DropH.Length; i++)
        {
            float t0 = DropH[i], t1 = DropH[i + 1];
            Vector3 p0 = basePos + new Vector3(lean.x * t0 * t0, height * t0, lean.z * t0 * t0);
            Vector3 p1 = basePos + new Vector3(lean.x * t1 * t1, height * t1, lean.z * t1 * t1);
            m.Cyl(p0, p1, radius * DropR[i], radius * DropR[i + 1], 7, 0.03f, i == 0);
        }
    }

    void BuildPot(StageCtx c)
    {
        var mt = c.Get(MatId.Metal);
        mt.Beam(new Vector3(0, 1.5f, 0), new Vector3(0, 1.1f, 0), 0.02f, 0.02f, 0f);
        mt.Cyl(new Vector3(0, 0.85f, 0), new Vector3(0, 1.07f, 0), 0.2f, 0.25f, 8, 0.04f);
        mt.Cyl(new Vector3(0, 1.07f, 0), new Vector3(0, 1.1f, 0), 0.23f, 0.23f, 8, 0.02f);   // крышка
        c.Get(MatId.WoodD).Cyl(new Vector3(0, 1.1f, 0), new Vector3(0, 1.17f, 0), 0.04f, 0.04f, 5, 0.05f); // ручка
    }

    void BuildSticks(StageCtx c)
    {
        float[] angs = { 0.35f, 2.3f };
        var wl = c.Get(MatId.WoodL);
        var mm = c.Get(MatId.Smoke);
        foreach (float a in angs)
        {
            float x0 = Mathf.Cos(a) * (R + 1.6f), z0 = Mathf.Sin(a) * (R + 1.6f);
            var p0 = new Vector3(x0, GY(x0, z0) + 0.2f, z0);
            var p1 = new Vector3(Mathf.Cos(a) * R * 0.55f, 0.55f, Mathf.Sin(a) * R * 0.55f);
            wl.Beam(p0, p1, 0.025f, 0.025f, 0.05f);
            mm.Ico(p1, 0.06f, 0.1f);
        }
    }

    void PlaceDecor(Transform parent)
    {
        var list = Valid(decorPrefabs);
        if (list.Count == 0) return;
        var holder = NewChild(parent, "DecorPrefabs");
        float rr = R + 5f;
        for (int i = 0; i < list.Count; i++)
        {
            float ang = (140f + i * 61f) * Mathf.Deg2Rad;
            float x = Mathf.Cos(ang) * rr, z = Mathf.Sin(ang) * rr;
            PlacePrefab(list[i], holder, new Vector3(x, GY(x, z), z), Rf(0f, 360f));
        }
    }

    void MakeParticles(Transform parent, string name, Vector3 pos, Mesh mesh, MatId mat,
                       float rate, float life, float speed, float sizeMin, float sizeMax,
                       float angle, float radius, float gravity)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);   // эмиссия вдоль +Z -> вверх

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.startLifetime = life;
        main.startSpeed = speed;
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = gravity;
        main.maxParticles = 40;

        var em = ps.emission;
        em.rateOverTime = rate;

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle = angle;
        sh.radius = radius;

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        var curve = new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.35f, 1f), new Keyframe(1f, 0f));
        sol.size = new ParticleSystem.MinMaxCurve(1f, curve);

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.renderMode = ParticleSystemRenderMode.Mesh;
        psr.mesh = mesh;
        psr.sharedMaterial = GetMat(mat);
        psr.shadowCastingMode = ShadowCastingMode.Off;
        psr.receiveShadows = false;
    }
}