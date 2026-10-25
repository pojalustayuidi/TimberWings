// Tools > BuilderSim > Board Builder
// Процедурная «Доска заказов» в 3 этапа. Unity 6 (6000.3), URP.
// Кладётся в Assets/_Project/Editor/ рядом с HutBuilder.cs (использует его класс HutMesh).
// Рантайм-компонент BoardStages.cs кладётся в Assets/_Project/Scripts/ (НЕ в Editor).
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static EditorAssets;

public class BoardBuilder : EditorWindow
{
    const string Dir = "Assets/_Game/Board";
    const string PrefKey = "BuilderSim.BoardBuilder.v1";
    const string RootName = "OrderBoard";
    const float Rise = 0.25f;   // насколько плита торчит над землёй

    [System.Serializable]
    public class Settings
    {
        public float width = 5.2f, depth = 2.8f, postHeight = 3.0f, roofAngle = 30f;
        public int seed = 777;
        public int papers = 14;
        public Color woodLight = new Color(0.66f, 0.44f, 0.24f);
        public Color woodDark = new Color(0.40f, 0.26f, 0.14f);
        public Color stone = new Color(0.60f, 0.58f, 0.64f);
        public Color roof = new Color(0.80f, 0.38f, 0.30f);
        public Color moss = new Color(0.36f, 0.66f, 0.22f);
        public Color banner = new Color(0.90f, 0.30f, 0.28f);
        public Color cork = new Color(0.76f, 0.58f, 0.36f);
        public bool decStakes = true, decPile = true, decHorse = true;                 // этап 1
        public bool decScaffold = true;                                                 // этап 2
        public bool decCounter = true, decBell = true, decLantern = true, decBanner = true,
                    decSign = true, decBarrels = true, decBench = true, decMoss = true; // этап 3
        public Vector3 position;
        public float yaw;
    }

    enum Mt { WoodL, WoodD, Stone, Roof, RoofB, Moss, Lamp, Rope, Cloth, Gold, Cork, Sack, PaperA, PaperB, PaperC, PaperD, Pin, Ink }

    class Ctx
    {
        public Transform root;
        public string tag;
        public Transform colRoot;
        public System.Random rng;
        public Dictionary<Mt, HutMesh> mbs = new Dictionary<Mt, HutMesh>();
        public HutMesh Get(Mt id)
        {
            HutMesh m;
            if (!mbs.TryGetValue(id, out m)) { m = new HutMesh(rng); mbs[id] = m; }
            return m;
        }
    }

    Settings S = new Settings();
    Vector2 scroll;

    // состояние сборки
    System.Random rng;
    Transform root;
    readonly Dictionary<Mt, Material> matCache = new Dictionary<Mt, Material>();
    readonly List<Transform> paperList = new List<Transform>();

    // производные размеры
    float W, Dp, PH, hx, hz, px, pz, tanA, ridgeY, eaveY, hsZ, xE, boardBottom, boardH, boardW;
    const float ohZ = 0.55f, ohX = 0.45f, roofOff = 0.115f, postT = 0.24f;
    const float panelZ = 0.17f;   // панель стоит на этом расстоянии от задней плоскости (внутрь)

    [MenuItem("Tools/BuilderSim/Здания/Board Builder", false, 300)]
    static void Open()
    {
        var w = GetWindow<BoardBuilder>(false, "Доска заказов");
        w.minSize = new Vector2(380, 560);
        w.Show();
    }

    // ========================================================================
    //  Сохранение настроек
    // ========================================================================
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

    // ========================================================================
    //  UI окна
    // ========================================================================
    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("Доска заказов", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Размеры", EditorStyles.boldLabel);
        S.width = EditorGUILayout.Slider("Ширина (м)", S.width, 3.6f, 9f);
        S.depth = EditorGUILayout.Slider("Глубина (м)", S.depth, 2.2f, 5f);
        S.postHeight = EditorGUILayout.Slider("Высота столбов (м)", S.postHeight, 2.6f, 4.5f);
        S.roofAngle = EditorGUILayout.Slider("Наклон крыши (°)", S.roofAngle, 20f, 50f);
        S.papers = EditorGUILayout.IntSlider("Листков-заказов на доске", S.papers, 0, 27);
        S.seed = EditorGUILayout.IntField("Seed", S.seed);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Цвета", EditorStyles.boldLabel);
        S.woodLight = EditorGUILayout.ColorField("Светлое дерево", S.woodLight);
        S.woodDark = EditorGUILayout.ColorField("Тёмное дерево", S.woodDark);
        S.stone = EditorGUILayout.ColorField("Камень", S.stone);
        S.roof = EditorGUILayout.ColorField("Крыша", S.roof);
        S.cork = EditorGUILayout.ColorField("Поле доски (пробка)", S.cork);
        S.banner = EditorGUILayout.ColorField("Флаг", S.banner);
        S.moss = EditorGUILayout.ColorField("Мох / трава", S.moss);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Декор: этап 1", EditorStyles.boldLabel);
        S.decStakes = EditorGUILayout.Toggle("Колышки с верёвкой", S.decStakes);
        S.decPile = EditorGUILayout.Toggle("Стопка досок и брёвен", S.decPile);
        S.decHorse = EditorGUILayout.Toggle("Козлы для распиловки", S.decHorse);
        EditorGUILayout.LabelField("Декор: этап 2", EditorStyles.boldLabel);
        S.decScaffold = EditorGUILayout.Toggle("Лестница и подмостки", S.decScaffold);
        EditorGUILayout.LabelField("Декор: этап 3", EditorStyles.boldLabel);
        S.decCounter = EditorGUILayout.Toggle("Конторка с чернилами и свитками", S.decCounter);
        S.decSign = EditorGUILayout.Toggle("Вывеска с «!»", S.decSign);
        S.decBell = EditorGUILayout.Toggle("Колокол", S.decBell);
        S.decLantern = EditorGUILayout.Toggle("Фонари (со светом)", S.decLantern);
        S.decBanner = EditorGUILayout.Toggle("Флаг на коньке", S.decBanner);
        S.decBarrels = EditorGUILayout.Toggle("Бочка, ящики, мешки", S.decBarrels);
        S.decBench = EditorGUILayout.Toggle("Скамья и табурет", S.decBench);
        S.decMoss = EditorGUILayout.Toggle("Мох на крыше и трава у плиты", S.decMoss);

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

        EditorGUILayout.Space(8);
        if (GUILayout.Button("Построить доску", GUILayout.Height(32))) EditorApplication.delayCall += DoBuild;
        if (GUILayout.Button("Удалить доску", GUILayout.Height(24))) EditorApplication.delayCall += DoDelete;

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
        var b = FindBoard();
        if (b == null) return;
        var comp = b.GetComponent<BoardStages>();
        if (comp != null) comp.SetStage(stage);
    }

    // ========================================================================
    //  Scene: кнопка и ручки
    // ========================================================================
    void OnSceneGUI(SceneView sv)
    {
        Handles.BeginGUI();
        GUILayout.BeginArea(new Rect(10, 52, 190, 40));
        if (GUILayout.Button("Построить доску", GUILayout.Height(28))) EditorApplication.delayCall += DoBuild;
        GUILayout.EndArea();
        Handles.EndGUI();

        var prev = Handles.matrix;
        Handles.matrix = Matrix4x4.TRS(S.position, Quaternion.Euler(0, S.yaw, 0), Vector3.one);
        Handles.color = new Color(0.4f, 0.9f, 1f, 0.9f);
        Handles.DrawWireCube(new Vector3(0, S.postHeight * 0.5f, 0), new Vector3(S.width, S.postHeight, S.depth));
        Handles.Label(new Vector3(0, S.postHeight + 0.3f, -S.depth * 0.5f - 0.5f), "перед (игрок здесь)");
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
        var b = FindBoard();
        if (b == null) return;
        float y;
        if (!GroundY(S.position, out y)) y = b.transform.position.y - Rise;
        Undo.RecordObject(b.transform, "Переместить доску");
        b.transform.SetPositionAndRotation(new Vector3(S.position.x, y + Rise, S.position.z), Quaternion.Euler(0, S.yaw, 0));
    }

    static GameObject FindBoard()
    {
        foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
            if (go.name == RootName) return go;
        return null;
    }

    void Delete()
    {
        var b = FindBoard();
        if (b != null) Undo.DestroyObjectImmediate(b);
    }

    // ========================================================================
    //  Земля (понимает и старый «Ground», и чанки реки Ground/G_x_z)
    // ========================================================================
    static bool IsGround(Collider c)
    {
        var t = c.transform;
        if (t.name == "Ground") return true;
        return t.parent != null && t.parent.name == "Ground";
    }

    static bool GroundY(Vector3 wp, out float y)
    {
        y = 0f;
        var hits = Physics.RaycastAll(new Vector3(wp.x, wp.y + 500f, wp.z), Vector3.down, 1500f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        bool ok = false;
        foreach (var h in hits)
        {
            if (!IsGround(h.collider)) continue;
            if (h.distance < best) { best = h.distance; y = h.point.y; ok = true; }
        }
        return ok;
    }

    bool AverageGround(out float y)
    {
        var q = Quaternion.Euler(0, S.yaw, 0);
        Vector3[] offs =
        {
            Vector3.zero, new Vector3(W / 2, 0, Dp / 2), new Vector3(-W / 2, 0, Dp / 2),
            new Vector3(W / 2, 0, -Dp / 2), new Vector3(-W / 2, 0, -Dp / 2)
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

    float GY(float x, float z)
    {
        Vector3 wp = root.TransformPoint(new Vector3(x, 0, z));
        float y;
        if (GroundY(wp, out y)) return root.InverseTransformPoint(new Vector3(wp.x, y, wp.z)).y;
        return -Rise;
    }

    // ========================================================================
    //  Утилиты
    // ========================================================================
    float Rf(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }
    float Jf(float a) { return Rf(-a, a); }

    Material GetMat(Mt id)
    {
        Material cached;
        if (matCache.TryGetValue(id, out cached) && cached != null) return cached;

        Color col; float emis = 0f; float smooth = 0.05f;
        switch (id)
        {
            case Mt.WoodL: col = S.woodLight; break;
            case Mt.WoodD: col = S.woodDark; break;
            case Mt.Stone: col = S.stone; break;
            case Mt.Roof: col = S.roof; break;
            case Mt.RoofB: col = S.roof * 0.86f; col.a = 1f; break;
            case Mt.Moss: col = S.moss; break;
            case Mt.Lamp: col = new Color(1f, 0.85f, 0.4f); emis = 1.8f; break;
            case Mt.Rope: col = new Color(0.78f, 0.66f, 0.42f); break;
            case Mt.Cloth: col = S.banner; break;
            case Mt.Gold: col = new Color(0.95f, 0.76f, 0.25f); smooth = 0.35f; break;
            case Mt.Cork: col = S.cork; break;
            case Mt.Sack: col = new Color(0.82f, 0.72f, 0.52f); break;
            case Mt.PaperA: col = new Color(0.97f, 0.93f, 0.80f); break;
            case Mt.PaperB: col = new Color(0.93f, 0.86f, 0.66f); break;
            case Mt.PaperC: col = new Color(0.98f, 0.80f, 0.74f); break;
            case Mt.PaperD: col = new Color(0.78f, 0.88f, 0.80f); break;
            case Mt.Pin: col = new Color(0.85f, 0.20f, 0.20f); smooth = 0.3f; break;
            default: col = new Color(0.12f, 0.12f, 0.16f); break;   // Ink
        }

        string path = Dir + "/Board_" + id + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            m = new Material(sh) { name = "Board_" + id };
            AssetDatabase.CreateAsset(m, path);
        }
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
        if (m.HasProperty("_Color")) m.SetColor("_Color", col);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
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

    Ctx NewCtx(Transform stageRoot, string tag) { return new Ctx { root = stageRoot, tag = tag, rng = rng }; }

    void Flush(Ctx c)
    {
        foreach (var kv in c.mbs)
        {
            if (kv.Value.Count == 0) continue;
            string nm = "Board_" + c.tag + "_" + kv.Key;
            var go = new GameObject("Mesh_" + kv.Key);
            go.transform.SetParent(c.root, false);
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mf.sharedMesh = SaveMesh(kv.Value.ToMesh(nm), nm);
            mr.sharedMaterial = GetMat(kv.Key);
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

    // ========================================================================
    //  СБОРКА
    // ========================================================================
    void Prepare()
    {
        W = S.width; Dp = S.depth; PH = S.postHeight;
        hx = W * 0.5f; hz = Dp * 0.5f;
        px = hx - 0.15f; pz = hz - 0.15f;
        tanA = Mathf.Tan(S.roofAngle * Mathf.Deg2Rad);
        hsZ = hz + ohZ; xE = hx + ohX;
        ridgeY = PH + hz * tanA;
        eaveY = PH - ohZ * tanA;
        boardW = W - 1.2f;
        boardBottom = 0.75f;
        boardH = Mathf.Clamp(PH - boardBottom - 0.6f, 1.0f, 1.8f);
    }

    void Build()
    {
        EnsureFolder(Dir);
        var old = FindBoard();
        if (old != null) Undo.DestroyObjectImmediate(old);

        rng = new System.Random(S.seed);
        matCache.Clear();
        paperList.Clear();
        Prepare();
        Physics.SyncTransforms();

        float gy;
        if (!AverageGround(out gy))
        {
            gy = S.position.y - Rise;
            Debug.LogWarning("BoardBuilder: коллайдер земли («Ground» или Ground/G_x_z) под доской не найден, высота взята из позиции ручки.");
        }

        var go = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(go, "Построить доску");
        root = go.transform;
        root.SetPositionAndRotation(new Vector3(S.position.x, gy + Rise, S.position.z), Quaternion.Euler(0, S.yaw, 0));

        var s1 = NewChild(root, "Stage1");
        var s2 = NewChild(root, "Stage2");
        var s3 = NewChild(root, "Stage3");
        BuildStage1(s1);
        BuildStage2(s2);
        BuildStage3(s3);

        // точки и зона для игровой логики
        var bp = NewChild(root, "BuildPoint");
        bp.localPosition = new Vector3(0, GY(0, -hz - 3f), -hz - 3f);
        var ip = NewChild(root, "InteractPoint");
        ip.localPosition = new Vector3(0, GY(0, -hz - 1.6f), -hz - 1.6f);
        var bc = NewChild(root, "BoardCenter");
        bc.localPosition = new Vector3(0, boardBottom + boardH * 0.5f, hz - 0.3f);
        var zone = NewChild(root, "InteractZone");
        zone.localPosition = new Vector3(0, 1f, -hz - 1.2f);
        var col = zone.gameObject.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(W + 0.5f, 2f, 2.6f);

        var stages = go.AddComponent<BoardStages>();
        stages.Configure(s1.gameObject, s2.gameObject, s3.gameObject, bp, ip, bc, zone.gameObject, paperList.ToArray(), 1);
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
            // листки включаются/выключаются игрой, свет живой
            if (tr.name.StartsWith("Paper") || tr.name == "Sheet" || tr.name == "Pin" || tr.name.StartsWith("Lantern")) continue;
            GameObjectUtility.SetStaticEditorFlags(tr.gameObject, flags);
        }
    }

    // ========================================================================
    //  ЭТАП 1: плита и стройплощадка
    // ========================================================================
    void BuildStage1(Transform parent)
    {
        var c = NewCtx(parent, "S1");
        var st = c.Get(Mt.Stone);

        st.Box(new Vector3(0, -0.45f, 0), new Vector3(W + 0.6f, 0.9f, Dp + 0.6f), Quaternion.identity, 0.01f);
        AddCol(c, new Vector3(0, -0.45f, 0), new Vector3(W + 0.6f, 0.9f, Dp + 0.6f));

        // валуны по периметру (спереди оставляем место под ступени)
        float sx = hx + 0.3f, sz = hz + 0.3f;
        for (float x = -sx; x <= sx + 0.01f; x += 0.55f)
            for (int sg = -1; sg <= 1; sg += 2)
            {
                if (sg < 0 && Mathf.Abs(x) < 1.2f) continue;
                RimStone(st, x + Jf(0.05f), sg * sz + Jf(0.05f));
            }
        for (float z = -sz + 0.55f; z <= sz - 0.55f + 0.01f; z += 0.55f)
            for (int sg = -1; sg <= 1; sg += 2)
                RimStone(st, sg * sx + Jf(0.05f), z + Jf(0.05f));

        // каменные ступени перед плитой
        float gs = GY(0, -sz - 0.45f);
        float top1 = -0.12f, bot = gs - 0.05f;
        st.Box(new Vector3(0, (top1 + bot) * 0.5f, -sz - 0.4f), new Vector3(2.2f, top1 - bot, 0.7f), Quaternion.identity, 0.02f);
        AddCol(c, new Vector3(0, (top1 + bot) * 0.5f, -sz - 0.4f), new Vector3(2.2f, top1 - bot, 0.7f));

        if (S.decPile) { BuildPile(c); BuildLogStack(c); }
        if (S.decHorse) BuildHorse(c);
        if (S.decStakes) BuildStakes(c);
        if (S.decMoss) BuildGrassTufts(c);
        Flush(c);
    }

    void RimStone(HutMesh st, float x, float z)
    {
        float h = Rf(0.30f, 0.42f);
        st.Box(new Vector3(x, 0.08f - h * 0.5f, z), new Vector3(Rf(0.42f, 0.62f), h, Rf(0.42f, 0.55f)),
               Quaternion.Euler(0, Jf(12f), 0), 0.06f);
    }

    void BuildGrassTufts(Ctx c)
    {
        var mo = c.Get(Mt.Moss);
        float sx = hx + 0.7f, sz = hz + 0.7f;
        for (int i = 0; i < 14; i++)
        {
            float a = Rf(0f, Mathf.PI * 2f);
            float x = Mathf.Clamp(Mathf.Cos(a) * 100f, -sx, sx);
            float z = Mathf.Clamp(Mathf.Sin(a) * 100f, -sz, sz);
            if (z < 0 && Mathf.Abs(x) < 1.6f) continue;
            float g = GY(x, z);
            for (int k = 0; k < 3; k++)
            {
                var b = new Vector3(x + Jf(0.12f), g - 0.02f, z + Jf(0.12f));
                mo.Cyl(b, b + new Vector3(Jf(0.05f), Rf(0.14f, 0.26f), Jf(0.05f)), 0.07f, 0.008f, 4, 0.1f);
            }
        }
    }

    void BuildPile(Ctx c)
    {
        float cx = hx + 2.0f, cz = -0.2f;
        float g = GY(cx, cz);
        var wd = c.Get(Mt.WoodD);
        for (int b = -1; b <= 1; b += 2)
            wd.Box(new Vector3(cx, g + 0.04f, cz + b * 0.7f), new Vector3(1.0f, 0.08f, 0.1f), Quaternion.identity, 0.03f);
        for (int layer = 0; layer < 7; layer++)
            for (int k = 0; k < 3; k++)
            {
                var mb = c.Get(rng.NextDouble() < 0.7 ? Mt.WoodL : Mt.WoodD);
                mb.Box(new Vector3(cx + (k - 1) * 0.26f + Jf(0.02f), g + 0.105f + layer * 0.055f, cz + Jf(0.08f)),
                       new Vector3(0.24f, 0.05f, Rf(1.6f, 2.0f)), Quaternion.Euler(0, Jf(2.5f), 0), 0.03f);
            }
    }

    void BuildLogStack(Ctx c)
    {
        float lx = -(hx + 2.4f), lz = 0.8f;
        float g = GY(lx, lz);
        int[] rows = { 3, 2, 1 };
        for (int row = 0; row < rows.Length; row++)
            for (int i = 0; i < rows[row]; i++)
            {
                float x = lx + (i - (rows[row] - 1) * 0.5f) * 0.36f;
                float y = g + 0.17f + row * 0.31f;
                float len = Rf(1.9f, 2.4f);
                float zs = Jf(0.15f);
                var mb = c.Get(rng.NextDouble() < 0.6 ? Mt.WoodL : Mt.WoodD);
                mb.Cyl(new Vector3(x, y, lz + zs - len * 0.5f), new Vector3(x, y, lz + zs + len * 0.5f), 0.17f, 0.17f, 7, 0.05f);
            }
    }

    void BuildHorse(Ctx c)
    {
        float hxp = hx + 1.8f, hzp = -hz - 0.4f;
        float g = GY(hxp, hzp);
        float hh = 0.8f;
        var wd = c.Get(Mt.WoodD);
        for (int e = -1; e <= 1; e += 2)
        {
            float z = hzp + e * 0.7f;
            var top = new Vector3(hxp, g + hh, z);
            for (int s = -1; s <= 1; s += 2)
                wd.Beam(top, new Vector3(hxp + s * 0.38f, g, z), 0.07f, 0.07f, 0.02f);
        }
        wd.Beam(new Vector3(hxp, g + hh, hzp - 0.85f), new Vector3(hxp, g + hh, hzp + 0.85f), 0.1f, 0.08f, 0.02f);
        c.Get(Mt.WoodL).Cyl(new Vector3(hxp, g + hh + 0.15f, hzp - 1.0f), new Vector3(hxp, g + hh + 0.15f, hzp + 0.9f), 0.1f, 0.1f, 7, 0.04f);
    }

    void BuildStakes(Ctx c)
    {
        float ax = hx + 1.1f, az = hz + 1.1f;
        var corners = new[] { new Vector2(-ax, -az), new Vector2(ax, -az), new Vector2(ax, az), new Vector2(-ax, az) };
        var pts = new List<Vector3>();
        var wl = c.Get(Mt.WoodL);
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
        var rope = c.Get(Mt.Rope);
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
    //  ЭТАП 2: каркас, задняя стена, пустые рамы доски, стропила
    // ========================================================================
    void BuildStage2(Transform parent)
    {
        var c = NewCtx(parent, "S2");
        var wl = c.Get(Mt.WoodL);
        var wd = c.Get(Mt.WoodD);
        var st = c.Get(Mt.Stone);

        // столбы + каменные пяты
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                wl.Box(new Vector3(sx * px, PH * 0.5f, sz * pz), new Vector3(postT, PH, postT), Quaternion.identity, 0.03f);
                st.Box(new Vector3(sx * px, 0.06f, sz * pz), new Vector3(0.42f, 0.12f, 0.42f), Quaternion.Euler(0, Jf(8f), 0), 0.05f);
                AddCol(c, new Vector3(sx * px, PH * 0.5f, sz * pz), new Vector3(postT + 0.04f, PH, postT + 0.04f));
            }

        // верхняя обвязка
        for (int sg = -1; sg <= 1; sg += 2)
        {
            wd.Beam(new Vector3(-hx, PH - 0.1f, sg * pz), new Vector3(hx, PH - 0.1f, sg * pz), 0.2f, 0.2f, 0.02f);
            wd.Beam(new Vector3(sg * px, PH - 0.1f, -hz), new Vector3(sg * px, PH - 0.1f, hz), 0.2f, 0.2f, 0.02f);
            // боковые перила (внутрь можно зайти только спереди)
            wd.Beam(new Vector3(sg * px, 0.95f, -pz), new Vector3(sg * px, 0.95f, pz), 0.1f, 0.12f, 0.02f);
            wd.Beam(new Vector3(sg * px, 0.5f, -pz), new Vector3(sg * px, 0.5f, pz), 0.09f, 0.1f, 0.02f);
            AddCol(c, new Vector3(sg * px, 0.7f, 0), new Vector3(0.16f, 1.4f, Dp - 0.2f));
            // косынки у передних столбов
            wd.Beam(new Vector3(sg * px, PH - 0.9f, -pz), new Vector3(sg * (px - 0.8f), PH - 0.1f, -pz), 0.1f, 0.1f, 0.02f);
        }

        // задняя стена из горизонтальных досок
        float y0 = 0.05f, bh = 0.26f;
        int rows = Mathf.FloorToInt((PH - 0.3f - y0) / bh);
        for (int i = 0; i < rows; i++)
        {
            var mb = (i % 2 == 0) ? wl : wd;
            float y = y0 + (i + 0.5f) * bh;
            mb.Box(new Vector3(Jf(0.01f), y, hz - 0.1f), new Vector3(2f * (hx - 0.12f) + Jf(0.02f), bh - 0.012f, 0.1f),
                   Quaternion.Euler(0, 0, Jf(0.5f)), 0.03f);
        }
        AddCol(c, new Vector3(0, PH * 0.5f, hz - 0.1f), new Vector3(W - 0.1f, PH, 0.16f));

        // пустая доска: рамка + 3 панели
        BuildBoardFrames(c);

        // дощатый пол
        for (float z = -hz + 0.12f; z <= hz - 0.12f; z += 0.3f)
        {
            var mb = (Mathf.RoundToInt(z * 10f) % 2 == 0) ? wl : wd;
            mb.Box(new Vector3(Jf(0.01f), 0.03f, z), new Vector3(W - 0.5f, 0.06f, 0.28f), Quaternion.Euler(0, Jf(0.6f), 0), 0.02f);
        }

        BuildRafters(c);
        if (S.decScaffold) BuildScaffold(c);
        Flush(c);
    }

    // три панели с рамами; возвращает центры/ширину панелей
    List<Vector2> Panels()
    {
        var l = new List<Vector2>();
        const float fw = 0.12f;
        float panelW = (boardW - 4f * fw) / 3f;
        for (int i = 0; i < 3; i++)
            l.Add(new Vector2(-boardW * 0.5f + fw + panelW * 0.5f + i * (panelW + fw), panelW));
        return l;
    }

    void BuildBoardFrames(Ctx c)
    {
        var wd = c.Get(Mt.WoodD);
        var wl = c.Get(Mt.WoodL);
        var ck = c.Get(Mt.Cork);
        const float fw = 0.12f;
        float zf = hz - panelZ;
        float yMid = boardBottom + boardH * 0.5f;
        // горизонтали
        wd.Box(new Vector3(0, boardBottom + fw * 0.5f, zf), new Vector3(boardW, fw, 0.09f), Quaternion.identity, 0.02f);
        wd.Box(new Vector3(0, boardBottom + boardH - fw * 0.5f, zf), new Vector3(boardW, fw, 0.09f), Quaternion.identity, 0.02f);
        // вертикали (4 шт.)
        float panelW = (boardW - 4f * fw) / 3f;
        for (int i = 0; i < 4; i++)
        {
            float x = -boardW * 0.5f + fw * 0.5f + i * (panelW + fw);
            wd.Box(new Vector3(x, yMid, zf), new Vector3(fw, boardH, 0.09f), Quaternion.identity, 0.02f);
        }
        // поля
        foreach (var p in Panels())
            ck.Box(new Vector3(p.x, yMid, zf + 0.02f), new Vector3(p.y, boardH - 2f * fw, 0.05f), Quaternion.identity, 0.01f);
        // козырёк над доской
        wl.Box(new Vector3(0, boardBottom + boardH + 0.12f, zf - 0.08f), new Vector3(boardW + 0.3f, 0.07f, 0.4f), Quaternion.Euler(-12f, 0, 0), 0.02f);
    }

    void BuildRafters(Ctx c)
    {
        var wd = c.Get(Mt.WoodD);
        int m = Mathf.Max(3, Mathf.RoundToInt(W / 0.9f) + 1);
        for (int i = 0; i < m; i++)
        {
            float x = Mathf.Lerp(-hx, hx, i / (float)(m - 1));
            var ridge = new Vector3(x, ridgeY - 0.02f, 0);
            for (int sg = -1; sg <= 1; sg += 2)
                wd.Beam(new Vector3(x, eaveY, sg * hsZ), ridge, 0.12f, 0.16f, 0.02f);
            if (i % 2 == 0)
                wd.Beam(new Vector3(x, PH - 0.04f, -hz), new Vector3(x, PH - 0.04f, hz), 0.12f, 0.12f, 0.02f);
        }
        wd.Beam(new Vector3(-xE, ridgeY + 0.02f, 0), new Vector3(xE, ridgeY + 0.02f, 0), 0.16f, 0.16f, 0.02f);
        float[] ts = { 0.35f, 0.7f };
        for (int sg = -1; sg <= 1; sg += 2)
            foreach (float tt in ts)
            {
                Vector3 a = Vector3.Lerp(new Vector3(-xE, eaveY, sg * hsZ), new Vector3(-xE, ridgeY - 0.02f, 0), tt);
                Vector3 b = Vector3.Lerp(new Vector3(xE, eaveY, sg * hsZ), new Vector3(xE, ridgeY - 0.02f, 0), tt);
                wd.Beam(a, b, 0.1f, 0.1f, 0.02f);
            }
    }

    void BuildScaffold(Ctx c)
    {
        var wd = c.Get(Mt.WoodD);
        var wl = c.Get(Mt.WoodL);

        // лестница, прислонённая к левому торцу
        float lx = -(hx + 0.9f), lz = -0.4f;
        float lg = GY(lx, lz);
        float topLy = Mathf.Min(PH - 0.4f, 2.6f);
        Vector3 b0 = new Vector3(lx, lg, lz - 0.22f), t0 = new Vector3(-hx - 0.1f, topLy, lz - 0.22f);
        Vector3 b1 = new Vector3(lx, lg, lz + 0.22f), t1 = new Vector3(-hx - 0.1f, topLy, lz + 0.22f);
        wl.Beam(b0, t0, 0.06f, 0.06f, 0.02f);
        wl.Beam(b1, t1, 0.06f, 0.06f, 0.02f);
        for (int i = 1; i <= 7; i++)
        {
            float t = i / 8f;
            wl.Beam(Vector3.Lerp(b0, t0, t), Vector3.Lerp(b1, t1, t), 0.04f, 0.04f, 0.02f);
        }

        // подмостки на двух козлах
        float sx = -(hx + 1.5f), sz = -hz - 0.9f;
        float g = GY(sx, sz);
        for (int e = -1; e <= 1; e += 2)
        {
            float x = sx + e * 0.7f;
            var top = new Vector3(x, g + 0.75f, sz);
            for (int s = -1; s <= 1; s += 2)
                wd.Beam(top, new Vector3(x, g, sz + s * 0.3f), 0.07f, 0.07f, 0.02f);
        }
        for (int i = 0; i < 2; i++)
            wl.Box(new Vector3(sx + Jf(0.03f), g + 0.8f, sz - 0.1f + i * 0.2f), new Vector3(1.9f, 0.05f, 0.19f), Quaternion.Euler(0, Jf(2f), 0), 0.03f);
    }

    // ========================================================================
    //  ЭТАП 3: крыша, листки, конторка, колокол, фонари, декор
    // ========================================================================
    void BuildStage3(Transform parent)
    {
        var c = NewCtx(parent, "S3");

        BuildRoof(c);
        BuildGables(c);
        if (S.decMoss) BuildRoofMoss(c);
        if (S.decSign) BuildSign(c);
        if (S.decBanner) BuildBanner(c);
        if (S.decBell) BuildBell(c);
        if (S.decLantern) { Lantern(c, new Vector3(-px, 0, -pz), true); Lantern(c, new Vector3(px, 0, -pz), false); }
        if (S.decCounter) BuildCounter(c);
        if (S.decBench) BuildBench(c);
        if (S.decBarrels) BuildBarrels(c);
        Flush(c);

        BuildPapers(parent);
    }

    void BuildRoof(Ctx c)
    {
        var ridgeP = new Vector3(0, ridgeY, 0);
        for (int sg = -1; sg <= 1; sg += 2)
        {
            var eaveP = new Vector3(0, eaveY, sg * hsZ);
            Vector3 sd = eaveP - ridgeP;
            float sl = sd.magnitude;
            Vector3 dir = sd / sl;
            Vector3 nrm = sg * Vector3.Cross(dir, Vector3.right);
            Quaternion q = Quaternion.LookRotation(Vector3.right, nrm);   // длинная сторона плитки вдоль X

            int rows = Mathf.CeilToInt(sl / 0.34f);
            float rs = sl / rows;
            for (int i = 0; i < rows; i++)
            {
                var mb = c.Get((i % 2 == 0) ? Mt.Roof : Mt.RoofB);
                float tt = (i + 0.5f) * rs;
                float lift = roofOff + ((i % 2 == 0) ? 0f : 0.025f);
                float x = -xE;
                while (x < xE - 0.05f)
                {
                    float len = Mathf.Min(Rf(0.5f, 0.85f), xE - x);
                    float xc = x + len * 0.5f;
                    Vector3 pos = ridgeP + dir * tt + nrm * lift + new Vector3(xc, 0, 0);
                    mb.Box(pos, new Vector3(rs * 1.4f, 0.07f, len - 0.01f), q * Quaternion.Euler(Jf(1.5f), 0, Jf(2f)), 0.03f);
                    x += len;
                }
            }
            AddCol(c, ridgeP + dir * (sl * 0.5f) + nrm * roofOff, new Vector3(sl, 0.12f, 2f * xE), q);
        }
        c.Get(Mt.WoodD).Box(new Vector3(0, ridgeY + 0.1f, 0), new Vector3(2f * xE + 0.1f, 0.2f, 0.2f), Quaternion.Euler(45f, 0, 0), 0.02f);
    }

    // треугольные заполнения торцов
    void BuildGables(Ctx c)
    {
        const float h = 0.24f;
        for (int j = 0; j < 40; j++)
        {
            float yj = PH + 0.04f + h * (j + 0.5f);
            float zl = hz - (yj - PH) / tanA;
            if (zl < 0.12f) break;
            var mb = c.Get((j % 2 == 0) ? Mt.WoodL : Mt.WoodD);
            for (int sx = -1; sx <= 1; sx += 2)
                mb.Box(new Vector3(sx * px, yj, 0), new Vector3(0.1f, h - 0.01f, 2f * zl + 0.05f), Quaternion.Euler(0, 0, Jf(0.5f)), 0.02f);
        }
    }

    void BuildRoofMoss(Ctx c)
    {
        var mo = c.Get(Mt.Moss);
        var ridgeP = new Vector3(0, ridgeY, 0);
        for (int i = 0; i < 9; i++)
        {
            int sg = rng.NextDouble() < 0.5 ? -1 : 1;
            var eaveP = new Vector3(0, eaveY, sg * hsZ);
            Vector3 sd = eaveP - ridgeP;
            float sl = sd.magnitude;
            Vector3 dir = sd / sl;
            Vector3 nrm = sg * Vector3.Cross(dir, Vector3.right);
            Vector3 p = ridgeP + dir * (Rf(0.2f, 0.9f) * sl) + new Vector3(Rf(-hx, hx), 0, 0) + nrm * (roofOff + 0.06f);
            mo.Cyl(p, p + nrm * 0.05f, Rf(0.18f, 0.4f), Rf(0.12f, 0.3f), 6, 0.2f);
        }
    }

    // подвесная вывеска над входом, с жёлтым «!»
    void BuildSign(Ctx c)
    {
        var wd = c.Get(Mt.WoodD);
        var wl = c.Get(Mt.WoodL);
        var gd = c.Get(Mt.Gold);
        var rp = c.Get(Mt.Rope);
        float z = -pz - 0.16f;
        float y = PH - 0.78f;
        wl.Box(new Vector3(0, y, z), new Vector3(1.75f, 0.58f, 0.07f), Quaternion.identity, 0.02f);
        wd.Box(new Vector3(0, y, z - 0.03f), new Vector3(1.6f, 0.44f, 0.06f), Quaternion.identity, 0.02f);
        gd.Box(new Vector3(0, y + 0.07f, z - 0.07f), new Vector3(0.09f, 0.22f, 0.03f), Quaternion.identity, 0.03f);
        gd.Box(new Vector3(0, y - 0.13f, z - 0.07f), new Vector3(0.09f, 0.09f, 0.03f), Quaternion.identity, 0.03f);
        for (int sg = -1; sg <= 1; sg += 2)
            rp.Beam(new Vector3(sg * 0.65f, PH - 0.2f, z + 0.02f), new Vector3(sg * 0.65f, y + 0.28f, z), 0.03f, 0.03f, 0f);
    }

    // флаг на коньке
    void BuildBanner(Ctx c)
    {
        var wd = c.Get(Mt.WoodD);
        var cl = c.Get(Mt.Cloth);
        float bx = hx * 0.55f;
        float top = ridgeY + 1.35f;
        wd.Beam(new Vector3(bx, ridgeY + 0.1f, 0), new Vector3(bx, top, 0), 0.06f, 0.06f, 0.01f);
        c.Get(Mt.Gold).Ico(new Vector3(bx, top + 0.07f, 0), 0.07f, 0.1f);
        for (int i = 0; i < 4; i++)
        {
            float h = 0.55f - i * 0.1f;
            float w = 0.26f;
            cl.Box(new Vector3(bx + 0.1f + w * (i + 0.5f), top - 0.1f - h * 0.5f + Mathf.Sin(i * 1.3f) * 0.03f, Mathf.Sin(i * 1.6f) * 0.05f),
                   new Vector3(w, h, 0.025f), Quaternion.Euler(0, Jf(10f), 0), 0.02f);
        }
    }

    void BuildBell(Ctx c)
    {
        var wd = c.Get(Mt.WoodD);
        var gd = c.Get(Mt.Gold);
        var rp = c.Get(Mt.Rope);
        float x0 = px + 0.12f, x1 = px + 0.62f, z = -pz;
        float y = PH - 0.55f;
        wd.Beam(new Vector3(x0, y, z), new Vector3(x1, y, z), 0.08f, 0.08f, 0.01f);
        wd.Beam(new Vector3(x0, y - 0.45f, z), new Vector3(x1 - 0.05f, y - 0.04f, z), 0.06f, 0.06f, 0.01f);
        float bxp = x1 - 0.05f;
        rp.Beam(new Vector3(bxp, y - 0.04f, z), new Vector3(bxp, y - 0.14f, z), 0.025f, 0.025f, 0f);
        gd.Cyl(new Vector3(bxp, y - 0.14f, z), new Vector3(bxp, y - 0.42f, z), 0.06f, 0.17f, 8, 0.02f);
        gd.Box(new Vector3(bxp, y - 0.46f, z), new Vector3(0.06f, 0.06f, 0.06f), Quaternion.identity, 0.03f);
        // шнур, чтобы дёрнуть
        rp.Beam(new Vector3(bxp, y - 0.46f, z), new Vector3(bxp, 1.55f, z), 0.025f, 0.025f, 0f);
        gd.Ico(new Vector3(bxp, 1.5f, z), 0.05f, 0.1f);
    }

    void Lantern(Ctx c, Vector3 post, bool withLight)
    {
        var wd = c.Get(Mt.WoodD);
        float z0 = post.z - 0.12f;
        float zl = post.z - 0.6f;
        float by = PH - 0.7f;
        wd.Beam(new Vector3(post.x, by, z0), new Vector3(post.x, by, zl), 0.06f, 0.06f, 0.01f);
        wd.Beam(new Vector3(post.x, by - 0.4f, z0), new Vector3(post.x, by - 0.03f, zl + 0.05f), 0.04f, 0.04f, 0.01f);
        float ly = by - 0.3f;
        var lp = new Vector3(post.x, ly, zl);
        c.Get(Mt.Rope).Beam(new Vector3(post.x, by, zl), new Vector3(post.x, ly + 0.12f, zl), 0.02f, 0.02f, 0f);
        c.Get(Mt.Lamp).Box(lp, new Vector3(0.16f, 0.2f, 0.16f), Quaternion.identity, 0.02f);
        wd.Box(lp + Vector3.up * 0.12f, new Vector3(0.22f, 0.04f, 0.22f), Quaternion.identity, 0.02f);
        wd.Box(lp - Vector3.up * 0.12f, new Vector3(0.22f, 0.04f, 0.22f), Quaternion.identity, 0.02f);

        if (!withLight) return;
        var lg = new GameObject("LanternLight");
        lg.transform.SetParent(c.root, false);
        lg.transform.localPosition = lp;
        var l = lg.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = 6f;
        l.intensity = 2.4f;
        l.color = new Color(1f, 0.78f, 0.45f);
        l.shadows = LightShadows.None;
    }

    void BuildCounter(Ctx c)
    {
        float cx = hx * 0.45f, cz = -0.1f;
        var wl = c.Get(Mt.WoodL);
        var wd = c.Get(Mt.WoodD);
        wl.Box(new Vector3(cx, 0.95f, cz), new Vector3(1.7f, 0.08f, 0.72f), Quaternion.Euler(0, Jf(1f), 0), 0.02f);
        for (int s = -1; s <= 1; s += 2)
            wd.Box(new Vector3(cx + s * 0.78f, 0.5f, cz), new Vector3(0.1f, 0.9f, 0.66f), Quaternion.identity, 0.03f);
        wd.Box(new Vector3(cx, 0.5f, cz + 0.3f), new Vector3(1.5f, 0.85f, 0.05f), Quaternion.identity, 0.02f);
        wd.Box(new Vector3(cx, 0.3f, cz), new Vector3(1.5f, 0.05f, 0.6f), Quaternion.identity, 0.02f);
        AddCol(c, new Vector3(cx, 0.5f, cz), new Vector3(1.8f, 1.0f, 0.8f));

        float ty = 0.99f;
        // чернильница и перо
        var ink = c.Get(Mt.Ink);
        var inkP = new Vector3(cx - 0.55f, ty, cz - 0.1f);
        ink.Cyl(inkP, inkP + Vector3.up * 0.1f, 0.055f, 0.045f, 6, 0.02f);
        wl.Beam(inkP + Vector3.up * 0.09f, inkP + new Vector3(0.1f, 0.3f, 0.06f), 0.012f, 0.012f, 0f);
        // свиток и стопка бумаг
        c.Get(Mt.PaperA).Cyl(new Vector3(cx + 0.05f, ty + 0.05f, cz - 0.15f), new Vector3(cx + 0.5f, ty + 0.05f, cz - 0.08f), 0.05f, 0.05f, 7, 0.03f);
        for (int i = 0; i < 3; i++)
            c.Get(Mt.PaperB).Box(new Vector3(cx - 0.1f + Jf(0.02f), ty + 0.012f + i * 0.016f, cz + 0.12f), new Vector3(0.38f, 0.014f, 0.28f),
                                 Quaternion.Euler(0, Jf(12f), 0), 0.02f);
        // книга учёта
        wd.Box(new Vector3(cx + 0.55f, ty + 0.03f, cz + 0.1f), new Vector3(0.34f, 0.06f, 0.24f), Quaternion.Euler(0, 15f, 0), 0.02f);
        c.Get(Mt.PaperA).Box(new Vector3(cx + 0.55f, ty + 0.035f, cz + 0.1f), new Vector3(0.3f, 0.05f, 0.2f), Quaternion.Euler(0, 15f, 0), 0.02f);
    }

    void BuildBench(Ctx c)
    {
        var wl = c.Get(Mt.WoodL);
        var wd = c.Get(Mt.WoodD);
        float bx = -hx * 0.55f, bz = -hz - 1.3f;
        float g = GY(bx, bz);
        wl.Box(new Vector3(bx, g + 0.46f, bz), new Vector3(1.5f, 0.07f, 0.4f), Quaternion.Euler(0, Jf(3f), 0), 0.02f);
        for (int s = -1; s <= 1; s += 2)
            wd.Box(new Vector3(bx + s * 0.6f, g + 0.22f, bz), new Vector3(0.08f, 0.46f, 0.34f), Quaternion.identity, 0.03f);
        AddCol(c, new Vector3(bx, g + 0.3f, bz), new Vector3(1.5f, 0.6f, 0.4f));

        // табурет у конторки
        float sx = hx * 0.45f - 0.4f, sz = -0.75f;
        c.Get(Mt.WoodL).Cyl(new Vector3(sx, 0.06f, sz), new Vector3(sx, 0.5f, sz), 0.17f, 0.2f, 8, 0.03f);
    }

    void BuildBarrels(Ctx c)
    {
        // у задней правой стороны, снаружи
        float bx = hx + 0.75f, bz = hz - 0.5f;
        float g = GY(bx, bz);
        var wl = c.Get(Mt.WoodL);
        var wd = c.Get(Mt.WoodD);
        wl.Cyl(new Vector3(bx, g, bz), new Vector3(bx, g + 0.8f, bz), 0.3f, 0.3f, 9, 0.03f);
        foreach (float yy in new[] { 0.2f, 0.6f })
            wd.Cyl(new Vector3(bx, g + yy - 0.03f, bz), new Vector3(bx, g + yy + 0.03f, bz), 0.32f, 0.32f, 9, 0.01f);
        AddCol(c, new Vector3(bx, g + 0.4f, bz), new Vector3(0.64f, 0.8f, 0.64f));

        // ящики
        float cx = hx + 0.7f, cz = hz - 1.4f;
        float gc = GY(cx, cz);
        CrateAt(c, new Vector3(cx, gc, cz), 0.62f, 12f);
        CrateAt(c, new Vector3(cx + 0.55f, gc, cz + 0.2f), 0.5f, -18f);
        CrateAt(c, new Vector3(cx + 0.05f, gc + 0.62f, cz + 0.02f), 0.42f, 35f);
        AddCol(c, new Vector3(cx + 0.25f, gc + 0.3f, cz + 0.1f), new Vector3(1.2f, 0.62f, 0.8f));

        // мешки
        var sk = c.Get(Mt.Sack);
        for (int i = 0; i < 3; i++)
        {
            float sx = hx + 1.5f + (i % 2) * 0.35f;
            float sz = hz - 0.9f + i * 0.2f;
            float gs = GY(sx, sz);
            sk.Ico(new Vector3(sx, gs + 0.2f, sz), 0.27f, 0.14f);
            sk.Cyl(new Vector3(sx, gs + 0.38f, sz), new Vector3(sx + Jf(0.03f), gs + 0.5f, sz), 0.08f, 0.04f, 5, 0.1f);
        }
    }

    void CrateAt(Ctx c, Vector3 g, float size, float yaw)
    {
        var q = Quaternion.Euler(0, yaw, 0);
        c.Get(Mt.WoodL).Box(g + Vector3.up * (size * 0.5f), Vector3.one * size, q, 0.02f);
        var wd = c.Get(Mt.WoodD);
        wd.Box(g + Vector3.up * (size + 0.02f), new Vector3(size * 1.06f, 0.05f, size * 1.06f), q, 0.02f);
        wd.Box(g + Vector3.up * 0.03f, new Vector3(size * 1.06f, 0.06f, size * 1.06f), q, 0.02f);
        wd.Box(g + Vector3.up * (size * 0.5f), new Vector3(size * 1.03f, 0.07f, size * 1.03f), q, 0.02f);
    }

    // ========================================================================
    //  Листки-заказы: отдельные объекты, чтобы игра могла прятать/показывать
    // ========================================================================
    void BuildPapers(Transform parent)
    {
        var holder = NewChild(parent, "Papers");
        if (S.papers <= 0) return;

        var pm = new HutMesh(rng);
        pm.Box(Vector3.zero, new Vector3(1f, 1f, 0.012f), Quaternion.identity, 0f);
        Mesh paperMesh = SaveMesh(pm.ToMesh("Board_Paper"), "Board_Paper");
        var pn = new HutMesh(rng);
        pn.Cyl(Vector3.zero, new Vector3(0, 0, -0.03f), 0.022f, 0.022f, 6, 0f);
        Mesh pinMesh = SaveMesh(pn.ToMesh("Board_Pin"), "Board_Pin");

        Mt[] paperMats = { Mt.PaperA, Mt.PaperB, Mt.PaperC, Mt.PaperD };
        float zf = hz - panelZ;
        float zPaper = zf + 0.02f - 0.05f * 0.5f - 0.012f;   // чуть перед лицом пробки
        float yTop = boardBottom + boardH - 0.12f;
        float yBot = boardBottom + 0.12f;

        var slots = new List<Vector3>();
        foreach (var p in Panels())
            for (int r = 0; r < 3; r++)
                for (int k = 0; k < 3; k++)
                {
                    float x = p.x - p.y * 0.5f + p.y * (k + 0.5f) / 3f + Jf(0.03f);
                    float y = yBot + (yTop - yBot) * (r + 0.5f) / 3f + Jf(0.03f);
                    slots.Add(new Vector3(x, y, zPaper));
                }
        for (int i = slots.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            var tmp = slots[i]; slots[i] = slots[j]; slots[j] = tmp;
        }

        int count = Mathf.Min(S.papers, slots.Count);
        float rowH = (yTop - yBot) / 3f;
        for (int i = 0; i < count; i++)
        {
            var g = NewChild(holder, "Paper_" + i);
            g.localPosition = slots[i];
            g.localRotation = Quaternion.Euler(0, 0, Jf(8f));

            var sheet = NewChild(g, "Sheet");
            float w = Rf(0.24f, 0.32f), h = Mathf.Min(Rf(0.30f, 0.42f), rowH * 0.9f);
            sheet.localScale = new Vector3(w, h, 1f);
            sheet.gameObject.AddComponent<MeshFilter>().sharedMesh = paperMesh;
            sheet.gameObject.AddComponent<MeshRenderer>().sharedMaterial = GetMat(paperMats[rng.Next(paperMats.Length)]);

            var pin = NewChild(g, "Pin");
            pin.localPosition = new Vector3(Jf(0.02f), h * 0.38f, -0.006f);
            pin.gameObject.AddComponent<MeshFilter>().sharedMesh = pinMesh;
            pin.gameObject.AddComponent<MeshRenderer>().sharedMaterial = GetMat(Mt.Pin);

            paperList.Add(g);
        }
    }
}
