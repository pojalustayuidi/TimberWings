// Assets/_Project/Scripts/Editor/Buildings/ShopBuilderV2.cs   (ТОЛЬКО в папке Editor!)
// Генератор «Лавка торговца v2»: три стадии в стиле Cloud Candy, тот же набор систем, что у хижины v2.
//
// ЗАПУСК: Tools > BuilderSim > Здания > Лавка торговца v2. Выбирать ничего не надо: если в сцене уже есть объект «TradingStall»,
//         генератор пересобирает его НА МЕСТЕ (позиция, поворот, BuildStageLink, зоны и всё прочее на корне сохраняются),
//         иначе создаёт новый. Старый ShopBuilder можно не трогать (он делает объект с тем же именем).
//
// Что собирает (лицо лавки смотрит на +Z, вход справа со ступенькой, как у старой лавки: NPC и зоны остаются на местах):
//   Platform      (всегда)  облачное основание, помост, ступенька, лужайка с цветами, дорожка из камней;
//   Stage1_Props  (этапы 1–2, группа StageVisibility, при достройке «улетает в стройку»)  козелки, ящик, молоток, краска, конусы, чертёж;
//   Stage2        каркас, прилавок, стеллаж, поддоны, стропила;
//   Stage3        полосатый навес с облачной бахромой и облаками на коньке, облакодел-труба, гавни, вывеска-облако (бревно = монета),
//                 флажки, 2 фонаря (LanternLight), меловая доска с живыми ценами, ярусы товара и монет (растут с продажами),
//                 весы, колокольчик, сундук, книга, горшки у входа, коврик.
//   Stage1_Crew / Stage2_Crew  бригады строителей с краном и табличкой прогресса (ShopBuilderV2.Crew.cs);
//   Stage3/Rainbow  радуга праздников (ShopBuilderV2.Rainbow.cs), Stage3/Mascot  облачко-талисман «Пух» (ShopBuilderV2.Mascot.cs).
//   Компоненты: ShopStages (v2), ShopLife (интерактив, эффекты, сохранение), HutLife (флажки и покачивание), StageVisibility.
//
// Материалы: Project/CandyToon (как у деревьев и хижины), лежат в Assets/_Project/Art/HutV2/Materials и общие с хижиной.
// Имена важны для света: «HutLamp» (стекло фонаря) и Light «LanternLight_…» подхватывает BuildingLightLink / FireSource.
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public partial class ShopBuilderV2 : EditorWindow
{
    const string PrefKey = "BuilderSim.ShopBuilderV2.v1";
    const string RootName = "TradingStall";
    const string PrefabDir = "Assets/_Game/Shop";

    const float PlatH = 0.28f;    // высота помоста
    const float PostH = 2.2f;     // высота столбов над помостом
    const float Slope = 0.42f;    // tan угла крыши
    const float Ov = 0.45f;       // свес навеса вперёд и назад
    const float OvX = 0.4f;       // свес по бокам

    [System.Serializable]
    public class Settings
    {
        public float width = 3.6f, depth = 2.6f;
        public int seed = 321;
        public int previewStage = 3;
        public int previewTier = 0;

        public bool decLawn = true;        // трава, цветы, грибы, камни
        public bool decPath = true;        // дорожка из камней от ступеньки
        public bool decBunting = true;     // гирлянда флажков по свесу
        public bool decBoard = true;       // меловая доска с ценами
        public bool decPots = true;        // горшки с цветами у входа

        public Vector3 position;
        public float yaw;
    }

    Settings S = new Settings();
    Vector2 scroll;

    // ====================== раскладка (считается в Layout) ======================
    float W, D, top, px, pz, run, rise, eaveY, ridgeY, th, totW, roofL;
    float counterL, counterR, counterZ, cx, stepX;
    Vector3 palA, palB;

    // ====================== реестр «жизни» стадии 3 ======================
    static readonly List<HutLife.ClothChain> cloths = new List<HutLife.ClothChain>();
    static readonly List<Transform> swingers = new List<Transform>();

    class LifeRefs
    {
        public Transform counterPoint, greetPoint, coinProto, cloudPoint, signKick, bellDome;
        public Transform scalesBeam, panL, panR, priceLog, pricePlank, glintPoint, goldStar;
        public float scalesArm = 0.26f, scalesChain = 0.27f, scalesTilt = 4f;
        public GameObject bazaarBadge;
        // радуга праздников (ShopBuilderV2.Rainbow.cs) и облачко-талисман «Пух» (ShopBuilderV2.Mascot.cs)
        public Transform rainbow, rainbowFootL, rainbowFootR;
        public Transform mascot, mascotBody, mascotEyes, mascotArm;
        public readonly GameObject[] logTiers = new GameObject[4];
        public readonly GameObject[] plankTiers = new GameObject[4];
        public readonly GameObject[] coinTiers = new GameObject[4];
    }
    LifeRefs L;

    // ====================== палитра и материалы ======================
    static readonly Dictionary<string, Color> Palette = new Dictionary<string, Color>
    {
        { "WoodDark",  new Color(0.49f, 0.29f, 0.14f) },
        { "WoodMid",   new Color(0.69f, 0.42f, 0.18f) },
        { "WoodLight", new Color(0.88f, 0.60f, 0.29f) },
        { "LogEnd",    new Color(0.96f, 0.80f, 0.52f) },
        { "Plank",     new Color(0.94f, 0.78f, 0.50f) },
        { "PlankDark", new Color(0.80f, 0.60f, 0.34f) },
        { "Roof",      new Color(0.98f, 0.81f, 0.25f) },
        { "RoofShade", new Color(0.88f, 0.66f, 0.18f) },
        { "Stone",     new Color(0.82f, 0.80f, 0.78f) },
        { "StoneDark", new Color(0.63f, 0.61f, 0.62f) },
        { "Moss",      new Color(0.47f, 0.79f, 0.23f) },
        { "Grass",     new Color(0.55f, 0.86f, 0.30f) },
        { "Leaf",      new Color(0.30f, 0.70f, 0.32f) },
        { "Dirt",      new Color(0.90f, 0.78f, 0.53f) },
        { "Red",       new Color(0.90f, 0.32f, 0.25f) },
        { "Cream",     new Color(0.99f, 0.95f, 0.85f) },
        { "Rope",      new Color(0.86f, 0.74f, 0.52f) },
        { "Metal",     new Color(0.68f, 0.71f, 0.76f) },
        { "MetalDark", new Color(0.42f, 0.44f, 0.50f) },
        { "Tarp",      new Color(0.76f, 0.86f, 0.93f) },
        { "Lilac",     new Color(0.74f, 0.55f, 0.92f) },
        { "Pink",      new Color(1.00f, 0.63f, 0.75f) },
        { "Water",     new Color(0.42f, 0.86f, 0.84f) },
        { "HutLamp",   new Color(1.00f, 0.83f, 0.46f) },
        { "HutWindowGlass", new Color(1.00f, 0.87f, 0.55f) },
        { "HutSmoke",  new Color(1.00f, 0.98f, 0.96f) },
        { "HutDust",   new Color(0.93f, 0.82f, 0.60f) },
        // новое для лавки
        { "Sky",       new Color(0.46f, 0.76f, 0.97f) },
        { "SkyDeep",   new Color(0.31f, 0.57f, 0.88f) },
        { "Cloud",     new Color(0.99f, 0.99f, 1.00f) },
        { "Chalk",     new Color(0.19f, 0.27f, 0.31f) },
        { "Gold",      new Color(1.00f, 0.78f, 0.22f) },
        { "GoldDark",  new Color(0.88f, 0.58f, 0.14f) },
        // кожа строителей (как у хижины) и семь цветов радуги в конфетных тонах
        { "Skin",      new Color(0.98f, 0.78f, 0.62f) },
        { "Rb0",       new Color(0.96f, 0.36f, 0.38f) },
        { "Rb1",       new Color(1.00f, 0.62f, 0.26f) },
        { "Rb2",       new Color(1.00f, 0.87f, 0.32f) },
        { "Rb3",       new Color(0.50f, 0.84f, 0.38f) },
        { "Rb4",       new Color(0.38f, 0.82f, 0.92f) },
        { "Rb5",       new Color(0.40f, 0.58f, 0.96f) },
        { "Rb6",       new Color(0.70f, 0.52f, 0.93f) },
    };

    static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
    static Shader toon;
    static bool toonTried;

    static Shader ToonShader()
    {
        if (toonTried) return toon;
        toonTried = true;
        var sh = Shader.Find("Project/CandyToon");
        toon = (sh != null && sh.isSupported) ? sh : null;
        if (toon == null)
        {
            var rp = GraphicsSettings.currentRenderPipeline;
            if (rp != null && rp.defaultMaterial != null) toon = rp.defaultMaterial.shader;
            if (toon == null) toon = Shader.Find("Universal Render Pipeline/Lit");
            if (toon == null) toon = Shader.Find("Standard");
            Debug.LogWarning("[ShopV2] Project/CandyToon не найден: материалы на запасном шейдере. Вид можно потом переназначить в папке " + HutMeshLib.MatDir);
        }
        return toon;
    }

    static Material Mat(string key)
    {
        Material m;
        if (mats.TryGetValue(key, out m) && m != null) return m;

        HutMeshLib.EnsureFolders();
        string path = HutMeshLib.MatDir + "/" + key + ".mat";
        m = AssetDatabase.LoadAssetAtPath<Material>(path);
        Color c;
        if (!Palette.TryGetValue(key, out c)) c = Color.magenta;
        if (m == null)
        {
            m = new Material(ToonShader()) { name = key };
            m.enableInstancing = true;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_GlossStrength")) m.SetFloat("_GlossStrength", 0f);
            if (m.HasProperty("_RimStrength")) m.SetFloat("_RimStrength", key == "HutSmoke" || key == "Cloud" ? 0.2f : 0.05f);
            bool glow = key == "HutLamp" || key == "HutWindowGlass";
            if (glow && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", c * 0.6f);
            }
            AssetDatabase.CreateAsset(m, path);
        }
        mats[key] = m;
        return m;
    }

    // ====================== случайность ======================
    static System.Random rng = new System.Random(321);
    static float R() { return (float)rng.NextDouble(); }
    static float R(float a, float b) { return a + (b - a) * R(); }
    static int RI(int a, int b) { return rng.Next(a, b); }

    // ====================== кирпичики ======================
    static GameObject Obj(Transform p, string name, Vector3 pos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(p, false);
        go.transform.localPosition = pos;
        return go;
    }

    static GameObject Part(Transform p, string name, Mesh mesh, Material m, Vector3 pos, Vector3 scale, Quaternion rot)
    {
        var go = new GameObject(name);
        var t = go.transform;
        t.SetParent(p, false);
        t.localPosition = pos;
        t.localRotation = rot;
        t.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = ShadowCastingMode.On;
        r.receiveShadows = true;
        return go;
    }

    static GameObject Box(Transform p, string name, Vector3 pos, Vector3 size, string mat, Vector3 euler = default(Vector3))
    {
        return Part(p, name, HutMeshLib.SoftCube, Mat(mat), pos, size, Quaternion.Euler(euler));
    }

    static GameObject Cyl(Transform p, string name, Vector3 pos, float radius, float height, string mat, int sides = 8, Vector3 euler = default(Vector3))
    {
        return Part(p, name, HutMeshLib.Cyl(sides), Mat(mat), pos, new Vector3(radius * 2f, height, radius * 2f), Quaternion.Euler(euler));
    }

    static GameObject ConeP(Transform p, string name, Vector3 pos, float radius, float height, string mat, int sides = 7, Vector3 euler = default(Vector3))
    {
        return Part(p, name, HutMeshLib.Cone(sides), Mat(mat), pos, new Vector3(radius * 2f, height, radius * 2f), Quaternion.Euler(euler));
    }

    static GameObject Blob(Transform p, string name, Vector3 pos, Vector3 size, string mat, int seed = -1)
    {
        if (seed < 0) seed = RI(0, 4);
        return Part(p, name, HutMeshLib.Blob(seed), Mat(mat), pos, size, Quaternion.Euler(0f, R(0f, 360f), 0f));
    }

    // клякса с заданным поворотом (бахрома навеса идёт вдоль ската)
    static GameObject BlobQ(Transform p, string name, Vector3 pos, Vector3 size, string mat, Quaternion rot, int seed)
    {
        return Part(p, name, HutMeshLib.Blob(seed), Mat(mat), pos, size, rot);
    }

    static GameObject Beam(Transform p, string name, Vector3 a, Vector3 b, float thick, string mat)
    {
        Vector3 d = b - a;
        float len = Mathf.Max(0.001f, d.magnitude);
        var rot = Quaternion.FromToRotation(Vector3.up, d.normalized);
        return Part(p, name, HutMeshLib.SoftCube, Mat(mat), (a + b) * 0.5f, new Vector3(thick, len, thick), rot);
    }

    static GameObject Rod(Transform p, string name, Vector3 a, Vector3 b, float radius, string mat, int sides = 6)
    {
        Vector3 d = b - a;
        float len = Mathf.Max(0.001f, d.magnitude);
        var rot = Quaternion.FromToRotation(Vector3.up, d.normalized);
        return Part(p, name, HutMeshLib.Cyl(sides), Mat(mat), (a + b) * 0.5f, new Vector3(radius * 2f, len, radius * 2f), rot);
    }

    // бревно со светлыми торцами: один Renderer с двумя материалами
    static GameObject Log(Transform p, string name, Vector3 a, Vector3 b, float radius, string wood = "WoodMid", string end = "LogEnd")
    {
        Vector3 d = b - a;
        float len = Mathf.Max(0.001f, d.magnitude);
        var rot = Quaternion.FromToRotation(Vector3.up, d.normalized);
        var go = Part(p, name, HutMeshLib.LogCyl(8), Mat(wood), (a + b) * 0.5f, new Vector3(radius * 2f, len, radius * 2f), rot);
        go.GetComponent<MeshRenderer>().sharedMaterials = new[] { Mat(wood), Mat(end) };
        return go;
    }

    static GameObject Solid(Transform p, string name, Vector3 center, Vector3 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(p, false);
        go.transform.localPosition = center;
        var bc = go.AddComponent<BoxCollider>();
        bc.size = size;
        return go;
    }

    static void NoShadow(GameObject g)
    {
        var r = g != null ? g.GetComponent<Renderer>() : null;
        if (r != null) r.shadowCastingMode = ShadowCastingMode.Off;
    }

    // «ткань на цепочке» (флаги): HutLife качает шарниры волной
    static HutLife.ClothChain Cloth(Transform p, string name, Vector3 topLocal, float width, float segLen, int segs, string mat, float amp = 10f, float phase = 0f, string tail = null)
    {
        var chain = new HutLife.ClothChain { amp = amp, phase = phase, segs = new Transform[segs] };
        Transform parent = p;
        for (int i = 0; i < segs; i++)
        {
            var hinge = Obj(parent, name + "_h" + i, i == 0 ? topLocal : new Vector3(0f, -segLen, 0f));
            bool last = i == segs - 1;
            Box(hinge.transform, name + "_" + i, new Vector3(0f, -segLen * 0.5f, 0f),
                new Vector3(last && tail != null ? width * 0.8f : width, segLen * 1.03f, 0.025f), (last && tail != null) ? tail : mat);
            chain.segs[i] = hinge.transform;
            parent = hinge.transform;
        }
        return chain;
    }

    // ====================== окно генератора ======================
    [MenuItem("Tools/BuilderSim/Здания/Лавка торговца v2", false, 300)]
    static void Open()
    {
        var w = GetWindow<ShopBuilderV2>(false, "Лавка v2");
        w.minSize = new Vector2(380, 560);
        w.Show();
    }

    void OnEnable() { LoadPrefs(); SceneView.duringSceneGui += OnSceneGUI; }
    void OnDisable() { SceneView.duringSceneGui -= OnSceneGUI; SavePrefs(); }
    void SavePrefs() { EditorPrefs.SetString(PrefKey, JsonUtility.ToJson(S)); }
    void LoadPrefs() { if (EditorPrefs.HasKey(PrefKey)) JsonUtility.FromJsonOverwrite(EditorPrefs.GetString(PrefKey), S); }

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("Лавка торговца v2 (Cloud Candy)", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();

        EditorGUILayout.LabelField("Параметры", EditorStyles.boldLabel);
        S.width = EditorGUILayout.Slider("Ширина помоста (м)", S.width, 3.0f, 5.0f);
        S.depth = EditorGUILayout.Slider("Глубина помоста (м)", S.depth, 2.2f, 3.6f);
        S.seed = EditorGUILayout.IntField("Seed", S.seed);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Украшения", EditorStyles.boldLabel);
        S.decLawn = EditorGUILayout.Toggle("Трава, цветы, грибы, камни", S.decLawn);
        S.decPath = EditorGUILayout.Toggle("Дорожка из камней", S.decPath);
        S.decBunting = EditorGUILayout.Toggle("Гирлянда флажков", S.decBunting);
        S.decBoard = EditorGUILayout.Toggle("Меловая доска с ценами", S.decBoard);
        S.decPots = EditorGUILayout.Toggle("Горшки с цветами у входа", S.decPots);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Предпросмотр", EditorStyles.boldLabel);
        S.previewStage = EditorGUILayout.IntSlider("Этап", S.previewStage, 1, 3);
        S.previewTier = EditorGUILayout.IntSlider("Ярус товара и монет (0..3)", S.previewTier, 0, 3);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Положение нового объекта (у существующего ручки в Scene)", EditorStyles.boldLabel);
        S.position = EditorGUILayout.Vector3Field("Позиция", S.position);
        S.yaw = EditorGUILayout.FloatField("Поворот Y (°)", S.yaw);

        if (EditorGUI.EndChangeCheck()) { SavePrefs(); MoveExisting(); ApplyPreview(); SceneView.RepaintAll(); }

        EditorGUILayout.Space(8);
        if (GUILayout.Button("Собрать / пересобрать на месте", GUILayout.Height(32))) Build();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Сохранить префаб")) SavePrefab();
            if (GUILayout.Button("Удалить из сцены")) DeleteExisting();
        }
        EditorGUILayout.HelpBox(
            "Если в сцене уже есть «TradingStall», он пересобирается НА МЕСТЕ: позиция, поворот и всё, что вы повесили на корень, остаётся. " +
            "Лицо лавки смотрит на +Z, вход справа со ступенькой.\n" +
            "Цены на доске, звон, облачка и рост витрины работают в игре (ShopLife).", MessageType.Info);
        EditorGUILayout.EndScrollView();
    }

    void OnSceneGUI(SceneView sv)
    {
        var r = FindRoot();
        if (!r) return;
        Handles.color = new Color(0.47f, 0.76f, 0.97f, 0.9f);
        Handles.matrix = Matrix4x4.TRS(r.position, r.rotation, Vector3.one);
        Handles.DrawWireCube(new Vector3(0, 0.5f, 0), new Vector3(S.width, 1f, S.depth));
        Handles.matrix = Matrix4x4.identity;

        EditorGUI.BeginChangeCheck();
        Vector3 pos = Handles.PositionHandle(r.position, r.rotation);
        Quaternion rot = Handles.Disc(r.rotation, r.position, Vector3.up, 1.2f, false, 0f);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(r, "Лавка v2: перенос");
            r.position = pos;
            r.rotation = Quaternion.Euler(0f, rot.eulerAngles.y, 0f);
            S.position = pos;
            S.yaw = rot.eulerAngles.y;
            SavePrefs(); Repaint();
        }
    }

    Transform FindRoot()
    {
        var sc = SceneManager.GetActiveScene();
        if (!sc.IsValid()) return null;
        foreach (var g in sc.GetRootGameObjects()) if (g.name == RootName) return g.transform;
        return null;
    }

    // позиция из окна применяется только к объекту, который сделал сам генератор (новый); чужой корень не двигаем из полей окна
    void MoveExisting() { }

    void ApplyPreview()
    {
        var r = FindRoot();
        if (!r) return;
        var st = r.GetComponent<ShopStages>();
        if (st) st.SetStage(S.previewStage);
        var life = r.GetComponent<ShopLife>();
        if (life) life.PreviewTiers(S.previewTier);
    }

    void DeleteExisting()
    {
        var r = FindRoot();
        if (r && EditorUtility.DisplayDialog("Лавка v2", "Удалить «" + RootName + "» из сцены целиком (вместе с вашими компонентами на корне)?", "Удалить", "Отмена"))
            Undo.DestroyObjectImmediate(r.gameObject);
    }

    void SavePrefab()
    {
        var r = FindRoot();
        if (!r) { EditorUtility.DisplayDialog("Лавка v2", "Сначала постройте лавку.", "OK"); return; }
        if (!AssetDatabase.IsValidFolder("Assets/_Game")) AssetDatabase.CreateFolder("Assets", "_Game");
        if (!AssetDatabase.IsValidFolder(PrefabDir)) AssetDatabase.CreateFolder("Assets/_Game", "Shop");
        PrefabUtility.SaveAsPrefabAssetAndConnect(r.gameObject, PrefabDir + "/TradingStall.prefab", InteractionMode.UserAction);
        AssetDatabase.SaveAssets();
    }

    // ====================== сборка ======================
    void Layout()
    {
        W = S.width; D = S.depth;
        top = PlatH + PostH;
        px = W / 2f - 0.18f;
        pz = D / 2f - 0.18f;
        run = D / 2f + Ov;
        th = Mathf.Atan(Slope) * Mathf.Rad2Deg;
        eaveY = top + 0.095f - (Ov + 0.18f) * Slope;
        rise = run * Slope;
        ridgeY = eaveY + rise;
        roofL = Mathf.Sqrt(run * run + rise * rise);
        totW = W + 2f * OvX;
        counterL = -W / 2f + 0.5f;
        counterR = W / 2f - 1.0f;
        counterZ = D / 2f - 0.45f;
        cx = (counterL + counterR) / 2f;
        stepX = W / 2f - 0.55f;
        palA = new Vector3(-W / 2f + 0.9f, 0f, -0.2f);
        palB = new Vector3(-W / 2f + 2.1f, 0f, -0.2f);
    }

    // удаляем только то, что сделал генератор; остальное на корне (зоны стройки, метки, ваши компоненты) остаётся
    static readonly string[] Generated = { "Platform", "Stage1_Props", "Stage2", "Stage3", "Stage1_Crew", "Stage2_Crew" };

    static void ClearGenerated(Transform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            var c = root.GetChild(i);
            if (System.Array.IndexOf(Generated, c.name) >= 0) Undo.DestroyObjectImmediate(c.gameObject);
        }
    }

    void Build()
    {
        HutMeshLib.EnsureFolders();
        HutMeshLib.ClearCache();
        mats.Clear();
        rng = new System.Random(S.seed);
        Layout();
        L = new LifeRefs();
        cloths.Clear();
        swingers.Clear();

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Лавка v2: сборка");
        int ug = Undo.GetCurrentGroup();

        try
        {
            EditorUtility.DisplayProgressBar("Лавка v2", "Корень", 0.05f);
            Transform root = FindRoot();
            if (root == null)
            {
                var go = new GameObject(RootName);
                Undo.RegisterCreatedObjectUndo(go, "Лавка v2");
                go.transform.position = S.position;
                go.transform.rotation = Quaternion.Euler(0f, S.yaw, 0f);
                root = go.transform;
            }
            else ClearGenerated(root);

            var flyT = root.Find("FlyTarget");
            if (flyT == null) flyT = Obj(root, "FlyTarget", new Vector3(0f, 1.4f, 0f)).transform;

            EditorUtility.DisplayProgressBar("Лавка v2", "Этап 1: помост и стройматериалы", 0.2f);
            var plat = Obj(root, "Platform", Vector3.zero).transform;
            Undo.RegisterCreatedObjectUndo(plat.gameObject, "Лавка v2");
            BuildPlatform(plat);
            Solid(plat, "Collider", new Vector3(0f, PlatH / 2f, 0f), new Vector3(W + 0.1f, PlatH, D + 0.1f));

            var props = Obj(root, "Stage1_Props", Vector3.zero).transform;
            Undo.RegisterCreatedObjectUndo(props.gameObject, "Лавка v2");
            BuildProps(props);

            EditorUtility.DisplayProgressBar("Лавка v2", "Этап 2: каркас и прилавок", 0.45f);
            var s2 = Obj(root, "Stage2", Vector3.zero).transform;
            Undo.RegisterCreatedObjectUndo(s2.gameObject, "Лавка v2");
            BuildStage2(s2);

            EditorUtility.DisplayProgressBar("Лавка v2", "Этап 3: навес, вывеска, товар", 0.7f);
            var s3 = Obj(root, "Stage3", Vector3.zero).transform;
            Undo.RegisterCreatedObjectUndo(s3.gameObject, "Лавка v2");
            BuildStage3(s3);

            EditorUtility.DisplayProgressBar("Лавка v2", "Бригады строителей", 0.84f);
            BuildCrew(root, 1);
            BuildCrew(root, 2);

            EditorUtility.DisplayProgressBar("Лавка v2", "Компоненты", 0.92f);
            Wire(root, props, s2, s3, flyT);

            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root.gameObject;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            Undo.CollapseUndoOperations(ug);
        }
    }

    void Wire(Transform root, Transform props, Transform s2, Transform s3, Transform flyT)
    {
        // группа стройматериалов уходит «в стройку», как забор хижины
        var sv = props.gameObject.GetComponent<StageVisibility>();
        if (sv == null) sv = props.gameObject.AddComponent<StageVisibility>();
        sv.minStage = 1;
        sv.maxStage = 2;
        sv.flyTarget = flyT;
        sv.exitStyle = StageVisibility.ExitStyle.Fly;
        sv.exitTime = 1.6f;

        // жизнь стадии 3: флажки и фонари
        var hl = s3.gameObject.GetComponent<HutLife>();
        if (hl == null) hl = s3.gameObject.AddComponent<HutLife>();
        hl.cloths = cloths.ToArray();
        hl.swingers = swingers.ToArray();
        hl.smokePoint = null;
        hl.swingAmp = 3.5f;

        var st = root.GetComponent<ShopStages>();
        if (st == null) st = Undo.AddComponent<ShopStages>(root.gameObject);
        st.constructionProps = props.gameObject;
        st.stage2 = s2.gameObject;
        st.stage3 = s3.gameObject;
        st.stage = S.previewStage;
        EditorUtility.SetDirty(st);

        var life = root.GetComponent<ShopLife>();
        if (life == null) life = Undo.AddComponent<ShopLife>(root.gameObject);
        life.stages = st;
        life.counterPoint = L.counterPoint;
        life.greetPoint = L.greetPoint;
        life.coinProto = L.coinProto;
        life.cloudPoint = L.cloudPoint;
        life.signKick = L.signKick;
        life.bellDome = L.bellDome;
        life.scalesBeam = L.scalesBeam;
        life.panL = L.panL;
        life.panR = L.panR;
        life.scalesArm = L.scalesArm;
        life.scalesChain = L.scalesChain;
        life.scalesBaseTilt = L.scalesTilt;
        life.priceLogAnchor = L.priceLog;
        life.pricePlankAnchor = L.pricePlank;
        life.bazaarBadge = L.bazaarBadge;
        life.glintPoint = L.glintPoint;
        life.goldStar = L.goldStar;
        life.puffMesh = HutMeshLib.Blob(1);
        life.puffMat = Mat("HutSmoke");
        life.hutLife = hl;
        life.rainbow = L.rainbow;
        life.rainbowFootL = L.rainbowFootL;
        life.rainbowFootR = L.rainbowFootR;
        life.mascot = L.mascot;
        life.mascotBody = L.mascotBody;
        life.mascotEyes = L.mascotEyes;
        life.mascotArm = L.mascotArm;
        life.logTiers = new ShopLife.Tiered { tiers = L.logTiers };
        life.plankTiers = new ShopLife.Tiered { tiers = L.plankTiers };
        life.coinTiers = new ShopLife.Tiered { tiers = L.coinTiers };
        EditorUtility.SetDirty(life);

        st.Apply();
        life.PreviewTiers(S.previewTier);
    }

    // ====================== ЭТАП 1: помост ======================
    void BuildPlatform(Transform p)
    {
        // лужайка (эллипс) и пятна травы
        var lawn = Cyl(p, "Lawn", new Vector3(0f, 0.02f, 0f), 1f, 0.08f, "Grass", 28);
        lawn.transform.localScale = new Vector3((W / 2f + 1.4f) * 2f, 0.08f, (D / 2f + 1.2f) * 2f);
        NoShadow(lawn);
        for (int i = 0; i < 7; i++)
        {
            float a = i / 7f * Mathf.PI * 2f + R(-0.3f, 0.3f);
            var pos = new Vector3(Mathf.Cos(a) * (W / 2f + 0.95f), 0.05f, Mathf.Sin(a) * (D / 2f + 0.8f));
            NoShadow(Blob(p, "LawnPatch", pos, new Vector3(R(0.9f, 1.5f), 0.07f, R(0.7f, 1.1f)), "Moss"));
        }

        // облачное основание: пушистая «подушка» вокруг помоста
        CloudBase(p);

        // помост: основание, настил, обвязка
        Box(p, "DeckBase", new Vector3(0f, 0.12f, 0f), new Vector3(W + 0.1f, 0.2f, D + 0.1f), "WoodDark");
        int n = Mathf.Max(3, Mathf.RoundToInt(D / 0.26f));
        float pd = D / n;
        for (int i = 0; i < n; i++)
        {
            float z = -D / 2f + pd * (i + 0.5f);
            float len = W - 0.02f - R() * 0.05f;
            Box(p, "DeckPlank", new Vector3((R() - 0.5f) * 0.04f, 0.235f + (R() - 0.5f) * 0.012f, z),
                new Vector3(len, 0.09f, pd - 0.03f), i % 2 == 0 ? "Plank" : "PlankDark", new Vector3(0f, R(-0.6f, 0.6f), 0f));
        }
        Box(p, "RimFront", new Vector3(0f, 0.2f, D / 2f + 0.03f), new Vector3(W + 0.16f, 0.13f, 0.12f), "WoodMid");
        Box(p, "RimBack", new Vector3(0f, 0.2f, -D / 2f - 0.03f), new Vector3(W + 0.16f, 0.13f, 0.12f), "WoodMid");
        Box(p, "RimRight", new Vector3(W / 2f + 0.03f, 0.2f, 0f), new Vector3(0.12f, 0.13f, D + 0.16f), "WoodMid");
        Box(p, "RimLeft", new Vector3(-W / 2f - 0.03f, 0.2f, 0f), new Vector3(0.12f, 0.13f, D + 0.16f), "WoodMid");

        // ступенька у входа
        Box(p, "Step", new Vector3(stepX, 0.08f, D / 2f + 0.34f), new Vector3(0.95f, 0.15f, 0.44f), "WoodMid");
        Box(p, "StepShadow", new Vector3(stepX, 0.04f, D / 2f + 0.36f), new Vector3(0.99f, 0.07f, 0.48f), "WoodDark");

        // угловые камни
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                Blob(p, "CornerStone", new Vector3(sx * (W / 2f + 0.06f), 0.1f, sz * (D / 2f + 0.06f)), new Vector3(0.38f, 0.28f, 0.38f), "Stone");

        // дорожка из камней от ступеньки
        if (S.decPath)
            for (int i = 0; i < 3; i++)
            {
                float z = D / 2f + 0.95f + i * 0.55f;
                NoShadow(Cyl(p, "PathStone", new Vector3(stepX + R(-0.15f, 0.15f), 0.075f, z), R(0.2f, 0.28f), 0.05f, "Stone", 7, new Vector3(0f, R(0f, 90f), 0f)));
            }

        if (S.decLawn) BuildLawnDecor(p);
    }

    void CloudBase(Transform p)
    {
        const float stepLen = 0.62f;
        for (float x = -W / 2f + 0.2f; x <= W / 2f - 0.2f; x += stepLen)
        {
            CloudPuff(p, new Vector3(x, 0f, D / 2f + 0.1f));
            CloudPuff(p, new Vector3(x, 0f, -D / 2f - 0.1f));
        }
        for (float z = -D / 2f + 0.2f; z <= D / 2f - 0.2f; z += stepLen)
        {
            CloudPuff(p, new Vector3(W / 2f + 0.1f, 0f, z));
            CloudPuff(p, new Vector3(-W / 2f - 0.1f, 0f, z));
        }
    }

    void CloudPuff(Transform p, Vector3 c)
    {
        if (c.z > 0f && Mathf.Abs(c.x - stepX) < 0.7f) return;   // ступенька свободна
        float s = R(0.55f, 0.9f);
        Blob(p, "CloudPuff", new Vector3(c.x + R(-0.06f, 0.06f), 0.03f, c.z + R(-0.06f, 0.06f)), new Vector3(s, s * 0.5f, s), "Cloud");
    }

    void BuildLawnDecor(Transform p)
    {
        for (int i = 0; i < 70; i++)
        {
            Vector3 pos;
            if (!LawnSpot(out pos)) continue;
            GrassTuft(p, pos, R(0.16f, 0.3f));
        }
        string[] petals = { "Pink", "Cream", "Lilac", "Red" };
        for (int i = 0; i < 16; i++)
        {
            Vector3 pos;
            if (!LawnSpot(out pos)) continue;
            Flower(p, pos, petals[RI(0, petals.Length)]);
        }
        for (int i = 0; i < 3; i++)
        {
            Vector3 pos;
            if (LawnSpot(out pos)) Mushroom(p, pos, R(0.2f, 0.32f));
        }
        for (int i = 0; i < 4; i++)
        {
            Vector3 pos;
            if (LawnSpot(out pos)) Rock(p, pos, R(0.25f, 0.42f));
        }
    }

    // случайная точка на лужайке вне помоста и вне дорожки
    bool LawnSpot(out Vector3 pos)
    {
        float a = R() * Mathf.PI * 2f;
        float k = 0.25f + R() * 1.0f;
        float x = Mathf.Cos(a) * (W / 2f + k + 0.1f);
        float z = Mathf.Sin(a) * (D / 2f + k + 0.1f);
        pos = new Vector3(x, 0.06f, z);
        if (Mathf.Abs(x) < W / 2f + 0.3f && Mathf.Abs(z) < D / 2f + 0.3f) return false;
        if (z > 0f && Mathf.Abs(x - stepX) < 0.85f) return false;
        if (x < -W / 2f - 0.15f && z > D / 2f - 0.9f && z < D / 2f + 0.9f && S.decBoard) return false;   // место под меловую доску
        return true;
    }

    static void GrassTuft(Transform p, Vector3 pos, float size)
    {
        var g = Obj(p, "Grass", pos);
        int blades = RI(3, 6);
        for (int i = 0; i < blades; i++)
        {
            float a = i * 360f / blades + R(-20f, 20f);
            float h = size * R(0.7f, 1.2f);
            NoShadow(ConeP(g.transform, "Blade", new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * 0.05f, h * 0.5f, Mathf.Sin(a * Mathf.Deg2Rad) * 0.05f),
                0.045f, h, R() < 0.4f ? "Moss" : "Grass", 4, new Vector3(R(-14f, 14f), a, R(-14f, 14f))));
        }
    }

    static void Flower(Transform p, Vector3 pos, string petal)
    {
        var f = Obj(p, "Flower", pos);
        float h = R(0.14f, 0.26f);
        NoShadow(Cyl(f.transform, "Stem", new Vector3(0f, h * 0.5f, 0f), 0.012f, h, "Leaf", 4));
        NoShadow(Blob(f.transform, "Bloom", new Vector3(0f, h + 0.02f, 0f), new Vector3(0.11f, 0.07f, 0.11f), petal, 1));
        NoShadow(Blob(f.transform, "Core", new Vector3(0f, h + 0.05f, 0f), new Vector3(0.04f, 0.03f, 0.04f), "RoofShade", 2));
    }

    static void Rock(Transform p, Vector3 pos, float size)
    {
        Blob(p, "Rock", pos + Vector3.up * size * 0.22f, new Vector3(size, size * 0.62f, size * R(0.8f, 1.1f)), "StoneDark");
    }

    static void Mushroom(Transform p, Vector3 pos, float s)
    {
        var m = Obj(p, "Mushroom", pos);
        Cyl(m.transform, "Stem", new Vector3(0f, s * 0.35f, 0f), s * 0.18f, s * 0.7f, "Cream", 6);
        Blob(m.transform, "Cap", new Vector3(0f, s * 0.78f, 0f), new Vector3(s * 0.95f, s * 0.55f, s * 0.95f), "Red", 3);
    }

    // ====================== ЭТАП 1: стройматериалы на лужайке ======================
    void BuildProps(Transform p)
    {
        // два козелка с досками (слева)
        float sx = -W / 2f - 0.95f;
        Sawhorse(p, new Vector3(sx, 0.05f, -0.35f));
        Sawhorse(p, new Vector3(sx, 0.05f, 0.55f));
        Box(p, "HorsePlank", new Vector3(sx + 0.05f, 0.77f, 0.1f), new Vector3(0.22f, 0.045f, 1.6f), "Plank", new Vector3(0f, 4f, 0f));
        Box(p, "HorsePlank2", new Vector3(sx - 0.1f, 0.815f, 0.2f), new Vector3(0.18f, 0.045f, 0.95f), "PlankDark", new Vector3(0f, -6f, 0f));

        // ящик с инструментами, молоток (справа от ступеньки)
        var crate = Obj(p, "ToolCrate", new Vector3(W / 2f + 0.8f, 0.06f, -0.2f));
        crate.transform.localRotation = Quaternion.Euler(0f, 25f, 0f);
        var ct = crate.transform;
        Box(ct, "CrateBody", new Vector3(0f, 0.13f, 0f), new Vector3(0.62f, 0.26f, 0.34f), "Red");
        Box(ct, "CrateLid", new Vector3(0f, 0.27f, 0f), new Vector3(0.64f, 0.04f, 0.36f), "WoodMid");
        Box(ct, "CrateBandL", new Vector3(-0.2f, 0.13f, 0f), new Vector3(0.05f, 0.27f, 0.36f), "Metal");
        Box(ct, "CrateBandR", new Vector3(0.2f, 0.13f, 0f), new Vector3(0.05f, 0.27f, 0.36f), "Metal");
        Box(ct, "SawHandle", new Vector3(0.05f, 0.33f, 0f), new Vector3(0.3f, 0.04f, 0.04f), "WoodDark");
        Box(ct, "Hammer", new Vector3(-0.1f, 0.31f, 0.08f), new Vector3(0.28f, 0.03f, 0.03f), "WoodLight", new Vector3(0f, 40f, 0f));
        Box(ct, "HammerHead", new Vector3(-0.2f, 0.32f, 0.14f), new Vector3(0.06f, 0.07f, 0.1f), "Metal", new Vector3(0f, 40f, 0f));

        // стопка досок
        PlankStack(p, "PlankPile", new Vector3(W / 2f + 0.85f, 0.06f, -D / 2f - 0.15f), 1.25f, 0.5f, 4, true, -12f);

        // ведро краски с кистью
        var bucket = Obj(p, "PaintBucket", new Vector3(W / 2f + 0.7f, 0.06f, 0.55f));
        Cyl(bucket.transform, "Bucket", new Vector3(0f, 0.14f, 0f), 0.15f, 0.28f, "Sky", 10);
        Cyl(bucket.transform, "Paint", new Vector3(0f, 0.285f, 0f), 0.125f, 0.02f, "SkyDeep", 10);
        Cyl(bucket.transform, "BucketRim", new Vector3(0f, 0.275f, 0f), 0.16f, 0.03f, "MetalDark", 10);
        Rod(bucket.transform, "BucketHandle", new Vector3(-0.15f, 0.27f, 0f), new Vector3(0.15f, 0.27f, 0f), 0.012f, "MetalDark", 4);
        Box(bucket.transform, "Brush", new Vector3(0.3f, 0.025f, 0.05f), new Vector3(0.22f, 0.03f, 0.03f), "WoodLight", new Vector3(0f, 25f, 0f));
        Box(bucket.transform, "BrushTip", new Vector3(0.39f, 0.025f, 0.09f), new Vector3(0.07f, 0.04f, 0.05f), "Pink", new Vector3(0f, 25f, 0f));

        // два конуса и свёрнутый чертёж
        Cone(p, new Vector3(W / 2f + 0.15f, 0.06f, D / 2f + 0.95f));
        Cone(p, new Vector3(-W / 2f + 0.1f, 0.06f, D / 2f + 1.05f));
        var roll = Obj(p, "Blueprint", new Vector3(-W / 2f - 0.6f, 0.12f, D / 2f + 0.3f));
        roll.transform.localRotation = Quaternion.Euler(0f, 70f, 0f);
        Cyl(roll.transform, "Roll", Vector3.zero, 0.07f, 0.5f, "Cream", 8, new Vector3(0f, 0f, 90f));
        Cyl(roll.transform, "RollBand", new Vector3(0.12f, 0f, 0f), 0.075f, 0.05f, "Red", 8, new Vector3(0f, 0f, 90f));
    }

    static void Cone(Transform p, Vector3 pos)
    {
        var c = Obj(p, "Cone", pos);
        ConeP(c.transform, "ConeBody", new Vector3(0f, 0.2f, 0f), 0.13f, 0.4f, "Red", 8);
        Cyl(c.transform, "ConeBand", new Vector3(0f, 0.17f, 0f), 0.095f, 0.07f, "Cream", 8);
        Box(c.transform, "ConeBase", new Vector3(0f, 0.015f, 0f), new Vector3(0.3f, 0.03f, 0.3f), "Red");
    }

    static void Sawhorse(Transform p, Vector3 pos)
    {
        var h = Obj(p, "Sawhorse", pos);
        var t = h.transform;
        Box(t, "HorseTop", new Vector3(0f, 0.64f, 0f), new Vector3(0.95f, 0.09f, 0.12f), "WoodLight");
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                Beam(t, "HorseLeg", new Vector3(sx * 0.4f, 0f, sz * 0.17f), new Vector3(sx * 0.32f, 0.62f, sz * 0.04f), 0.07f, "WoodDark");
        Box(t, "HorseBrace", new Vector3(0f, 0.28f, 0f), new Vector3(0.72f, 0.04f, 0.04f), "WoodMid");
    }

    // стопка досок: layers слоёв по 3 доски, при tied — две верёвочные стяжки
    static GameObject PlankStack(Transform p, string name, Vector3 pos, float len, float wid, int layers, bool tied, float yaw)
    {
        var s = Obj(p, name, pos);
        s.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        float h = 0.075f;
        for (int i = 0; i < layers; i++)
        {
            for (int k = 0; k < 3; k++)
            {
                float w = wid / 3f - 0.015f;
                Box(s.transform, "Board", new Vector3(R(-0.02f, 0.02f), h * (i + 0.5f), -wid * 0.5f + wid / 3f * (k + 0.5f)),
                    new Vector3(len * R(0.96f, 1.0f), h * 0.92f, w), ((i + k) % 2 == 0) ? "Plank" : "PlankDark", new Vector3(0f, R(-1.2f, 1.2f), 0f));
            }
        }
        if (tied)
        {
            float H = h * layers;
            for (int k = -1; k <= 1; k += 2)
                Box(s.transform, "Strap", new Vector3(k * len * 0.28f, H * 0.5f, 0f), new Vector3(0.05f, H + 0.03f, wid + 0.03f), "Rope");
        }
        return s;
    }

    // ====================== ЭТАП 2: каркас, прилавок, стеллаж ======================
    void BuildStage2(Transform p)
    {
        // столбы с «башмаками» и «шапками», раскосы
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                float x = sx * px, z = sz * pz;
                Box(p, "Post", new Vector3(x, PlatH + PostH / 2f, z), new Vector3(0.2f, PostH, 0.2f), "WoodMid");
                Box(p, "PostFoot", new Vector3(x, PlatH + 0.08f, z), new Vector3(0.3f, 0.16f, 0.3f), "WoodDark");
                Box(p, "PostCap", new Vector3(x, top - 0.04f, z), new Vector3(0.28f, 0.1f, 0.28f), "WoodDark");
                Beam(p, "Brace", new Vector3(x, top - 0.75f, z), new Vector3(x - sx * 0.55f, top - 0.12f, z), 0.07f, "WoodDark");
            }

        // обвязка поверху
        Box(p, "BeamFront", new Vector3(0f, top - 0.1f, pz), new Vector3(2f * px, 0.16f, 0.16f), "WoodDark");
        Box(p, "BeamBack", new Vector3(0f, top - 0.1f, -pz), new Vector3(2f * px, 0.16f, 0.16f), "WoodDark");
        Box(p, "BeamRight", new Vector3(px, top - 0.1f, 0f), new Vector3(0.16f, 0.16f, 2f * pz), "WoodDark");
        Box(p, "BeamLeft", new Vector3(-px, top - 0.1f, 0f), new Vector3(0.16f, 0.16f, 2f * pz), "WoodDark");

        // стропила и конёк (каркас крыши виден на этапе 2, на этапе 3 его накрывает навес)
        int nr = Mathf.Max(3, Mathf.RoundToInt((totW - 0.6f) / 0.62f) + 1);
        for (int i = 0; i < nr; i++)
        {
            float x = -totW / 2f + 0.3f + (totW - 0.6f) * i / (nr - 1);
            for (int side = -1; side <= 1; side += 2)
            {
                Quaternion q = Quaternion.Euler(side * th, 0f, 0f);
                Vector3 n = q * Vector3.up;
                Vector3 a = new Vector3(x, eaveY, side * run) - n * 0.11f;
                Vector3 b = new Vector3(x, ridgeY, 0f) - n * 0.11f;
                Beam(p, "Rafter", a, b, 0.08f, "WoodMid");
            }
        }
        Log(p, "RidgeBeam", new Vector3(-totW / 2f, ridgeY - 0.1f, 0f), new Vector3(totW / 2f, ridgeY - 0.1f, 0f), 0.07f, "WoodDark");

        BuildCounter(p);
        BuildRack(p);
        Pallet(p, palA);
        Pallet(p, palB);

        float cw = counterR - counterL;
        Solid(p, "CounterCollider", new Vector3(cx, PlatH + 0.5f, counterZ), new Vector3(cw, 1.0f, 0.6f));
    }

    void BuildCounter(Transform p)
    {
        float cw = counterR - counterL;
        Box(p, "CounterBody", new Vector3(cx, PlatH + 0.425f, counterZ), new Vector3(cw, 0.85f, 0.5f), "WoodDark");

        // вертикальные доски фасада и голубая «юбка»
        int nb = Mathf.Max(3, Mathf.RoundToInt(cw / 0.2f));
        float bw = cw / nb;
        for (int i = 0; i < nb; i++)
            Box(p, "CounterBoard", new Vector3(counterL + bw * (i + 0.5f), PlatH + 0.43f, counterZ + 0.27f), new Vector3(bw - 0.02f, 0.74f, 0.05f), i % 2 == 0 ? "Plank" : "PlankDark");
        Box(p, "CounterSkirt", new Vector3(cx, PlatH + 0.1f, counterZ + 0.29f), new Vector3(cw + 0.02f, 0.14f, 0.06f), "Sky");

        // столешница с козырьком, кремовая кромка
        Box(p, "CounterTop", new Vector3(cx, PlatH + 0.9f, counterZ + 0.06f), new Vector3(cw + 0.16f, 0.1f, 0.78f), "WoodLight");
        Box(p, "CounterLip", new Vector3(cx, PlatH + 0.86f, counterZ + 0.46f), new Vector3(cw + 0.18f, 0.07f, 0.06f), "Cream");

        // облачная бахрома под столешницей
        int ns = Mathf.Max(3, Mathf.RoundToInt(cw / 0.3f));
        float sw = cw / ns;
        for (int i = 0; i < ns; i++)
            Blob(p, "CounterFringe", new Vector3(counterL + sw * (i + 0.5f), PlatH + 0.8f, counterZ + 0.42f), new Vector3(sw * 1.12f, 0.2f, 0.18f), i % 2 == 0 ? "Cream" : "Sky", 1);

        // боковые стойки
        Box(p, "CounterPostL", new Vector3(counterL - 0.03f, PlatH + 0.5f, counterZ + 0.26f), new Vector3(0.12f, 1.0f, 0.12f), "WoodDark");
        Box(p, "CounterPostR", new Vector3(counterR + 0.03f, PlatH + 0.5f, counterZ + 0.26f), new Vector3(0.12f, 1.0f, 0.12f), "WoodDark");
    }

    void BuildRack(Transform p)
    {
        float rz = -D / 2f + 0.3f;
        float ux = W / 2f - 0.4f;
        foreach (float x in new[] { -ux, 0f, ux })
            Box(p, "RackPost", new Vector3(x, PlatH + 0.95f, rz), new Vector3(0.1f, 1.9f, 0.1f), "WoodDark");
        foreach (float y in new[] { 0.45f, 1.05f, 1.65f })
        {
            Box(p, "Shelf", new Vector3(0f, PlatH + y, rz), new Vector3(2f * ux + 0.12f, 0.06f, 0.4f), "Plank");
            Box(p, "ShelfLip", new Vector3(0f, PlatH + y - 0.06f, rz + 0.18f), new Vector3(2f * ux + 0.12f, 0.06f, 0.04f), "PlankDark");
        }
        // голубой облачный «козырёк» над стеллажом
        for (int i = 0; i < 6; i++)
        {
            float x = -ux + (2f * ux) * (i + 0.5f) / 6f;
            Blob(p, "RackCloud", new Vector3(x, PlatH + 1.93f, rz + 0.1f), new Vector3(0.62f, 0.26f, 0.3f), i % 2 == 0 ? "Cream" : "Sky", 2);
        }
    }

    static void Pallet(Transform p, Vector3 c)
    {
        var pl = Obj(p, "Pallet", new Vector3(c.x, PlatH, c.z));
        for (int i = -1; i <= 1; i++)
            Box(pl.transform, "Runner", new Vector3(i * 0.4f, 0.04f, 0f), new Vector3(0.1f, 0.08f, 0.8f), "WoodDark");
        for (int i = -2; i <= 2; i++)
            Box(pl.transform, "Slat", new Vector3(0f, 0.095f, i * 0.17f), new Vector3(1.0f, 0.03f, 0.13f), "WoodMid");
    }

    // ====================== ЭТАП 3 ======================
    void BuildStage3(Transform p)
    {
        BuildCanopy(p);
        BuildRoofTop(p);
        BuildSign(p);
        Lantern(p, "LanternL", new Vector3(-px + 0.15f, top - 0.2f, pz + 0.16f));
        Lantern(p, "LanternR", new Vector3(px - 0.15f, top - 0.2f, pz + 0.16f));
        if (S.decBunting) BuildBunting(p);
        if (S.decBoard) BuildPriceBoard(p);
        BuildStock(p);
        BuildCounterProps(p);
        BuildEntrance(p);

        BuildRainbow(p);    // радуга праздников: группа выключена, включает ShopLife
        BuildMascot(p);     // облачко-талисман на прилавке

        // точки для ShopLife
        float topY = PlatH + 0.95f;
        L.counterPoint = Obj(p, "CounterPoint", new Vector3(cx, topY + 0.25f, counterZ + 0.1f)).transform;
        L.greetPoint = Obj(p, "GreetPoint", new Vector3(cx, PlatH, D / 2f + 1.0f)).transform;
    }

    // ---------- навес: голубая крыша с кремовыми полосами, облачная бахрома, торцевые доски, фронтоны
    void BuildCanopy(Transform p)
    {
        const float t = 0.14f;
        int ns = Mathf.RoundToInt(totW / 0.32f);
        if (ns % 2 == 1) ns++;
        float sw = totW / ns;
        var roof = Obj(p, "Canopy", Vector3.zero).transform;

        for (int side = -1; side <= 1; side += 2)
        {
            Quaternion q = Quaternion.Euler(side * th, 0f, 0f);
            Vector3 eul = new Vector3(side * th, 0f, 0f);
            Vector3 c = new Vector3(0f, (eaveY + ridgeY) * 0.5f, side * run * 0.5f);
            Box(roof, "RoofSlab", c, new Vector3(totW, t, roofL), "Sky", eul);

            for (int i = 0; i < ns; i++)
            {
                float x = -totW / 2f + sw * (i + 0.5f);
                bool cream = i % 2 == 0;
                if (cream) Box(roof, "RoofStripe", c + q * new Vector3(x, t / 2f + 0.012f, 0f), new Vector3(sw, 0.026f, roofL - 0.02f), "Cream", eul);
                // облачная бахрома на нижней кромке ската: цвет продолжает полосу
                BlobQ(roof, "Valance", c + q * new Vector3(x, 0.0f, side * roofL / 2f), new Vector3(sw * 1.14f, t + 0.14f, 0.3f),
                    cream ? "Cream" : "Sky", q, (i % 4));
            }
            // торцевые доски
            for (int sx = -1; sx <= 1; sx += 2)
                Box(roof, "BargeBoard", c + q * new Vector3(sx * totW / 2f, -0.01f, 0f), new Vector3(0.1f, t + 0.06f, roofL + 0.1f), "WoodLight", eul);
        }

        // фронтоны: треугольник из досок и эмблема-облачко
        float y0 = top - 0.1f;
        float half = run - (y0 + 0.082f - eaveY) / Slope;
        float apex = ridgeY - 0.12f;
        float h = apex - y0;
        for (int sx = -1; sx <= 1; sx += 2)
        {
            float x = sx * (totW / 2f - 0.1f);
            Part(roof, "Gable", HutMeshLib.Prism, Mat("PlankDark"), new Vector3(x, y0 + h / 2f, 0f), new Vector3(2f * half, h, 0.12f), Quaternion.Euler(0f, 90f, 0f));
            float ex = x + sx * 0.08f;
            Cyl(roof, "CrestDisc", new Vector3(ex, y0 + h * 0.4f, 0f), 0.22f, 0.05f, "Sky", 12, new Vector3(0f, 0f, 90f));
            Blob(roof, "CrestCloud", new Vector3(ex + sx * 0.03f, y0 + h * 0.36f, 0f), new Vector3(0.1f, 0.2f, 0.34f), "Cloud", 1);
            Cyl(roof, "CrestSun", new Vector3(ex + sx * 0.03f, y0 + h * 0.52f, 0.1f), 0.05f, 0.04f, "Gold", 8, new Vector3(0f, 0f, 90f));
        }
    }

    // ---------- конёк: пушистые облака, облакодел-труба, флагшток
    void BuildRoofTop(Transform p)
    {
        var top3 = Obj(p, "RoofTop", Vector3.zero).transform;
        Log(top3, "RidgeLog", new Vector3(-totW / 2f - 0.1f, ridgeY + 0.05f, 0f), new Vector3(totW / 2f + 0.1f, ridgeY + 0.05f, 0f), 0.09f, "WoodLight");

        float[] xs = { -totW / 2f + 0.55f, 0.25f, totW / 2f - 0.5f };
        float[] ss = { 0.7f, 0.9f, 0.62f };
        for (int i = 0; i < xs.Length; i++)
        {
            Blob(top3, "RidgeCloud", new Vector3(xs[i], ridgeY + 0.2f, R(-0.05f, 0.05f)), new Vector3(ss[i], ss[i] * 0.62f, ss[i] * 0.8f), "Cloud");
            Blob(top3, "RidgeCloudB", new Vector3(xs[i] + ss[i] * 0.35f, ridgeY + 0.14f, R(-0.1f, 0.1f)), new Vector3(ss[i] * 0.6f, ss[i] * 0.42f, ss[i] * 0.55f), "Cloud");
        }

        // облакодел: медная труба (из неё пыхают облачка)
        float fx = -0.85f;
        Cyl(top3, "FunnelBase", new Vector3(fx, ridgeY + 0.08f, 0f), 0.17f, 0.12f, "GoldDark", 10);
        Cyl(top3, "FunnelPipe", new Vector3(fx, ridgeY + 0.38f, 0f), 0.11f, 0.5f, "Gold", 10);
        Cyl(top3, "FunnelRim", new Vector3(fx, ridgeY + 0.66f, 0f), 0.155f, 0.07f, "GoldDark", 10);
        L.cloudPoint = Obj(top3, "CloudPoint", new Vector3(fx, ridgeY + 0.75f, 0f)).transform;

        // флагшток с вымпелом на левом коньке
        float fpx = -totW / 2f + 0.15f;
        Rod(top3, "FlagPole", new Vector3(fpx, ridgeY, 0f), new Vector3(fpx, ridgeY + 1.0f, 0f), 0.025f, "WoodDark", 5);
        Blob(top3, "FlagKnob", new Vector3(fpx, ridgeY + 1.03f, 0f), new Vector3(0.09f, 0.09f, 0.09f), "Gold", 2);
        cloths.Add(Cloth(top3, "RidgeFlag", new Vector3(fpx, ridgeY + 0.95f, 0.02f), 0.26f, 0.15f, 4, "Pink", 14f, 0.3f, "Cream"));
    }

    // ---------- вывеска-облако: бревно = монета; качается (HutLife) и получает «пинок» (ShopLife)
    void BuildSign(Transform p)
    {
        float zs = pz + 0.14f;
        var pivot = Obj(p, "SignPivot", new Vector3(cx, top - 0.17f, zs));
        swingers.Add(pivot.transform);
        var kick = Obj(pivot.transform, "SignKick", Vector3.zero).transform;
        L.signKick = kick;

        float by = -0.78f;     // центр щита ниже точки подвеса
        // верёвки
        for (int s = -1; s <= 1; s += 2)
            Rod(kick, "SignRope", new Vector3(s * 0.52f, 0f, 0f), new Vector3(s * 0.56f, by + 0.2f, 0f), 0.014f, "Rope", 4);

        // задний «контур» (голубой) и облачная форма щита (кремовая)
        Vector3[] bp = { new Vector3(0f, 0f, 0f), new Vector3(-0.58f, 0.05f, 0f), new Vector3(0.6f, 0.04f, 0f), new Vector3(0.04f, 0.2f, 0f), new Vector3(-0.3f, -0.07f, 0f), new Vector3(0.32f, -0.07f, 0f) };
        Vector3[] bs = { new Vector3(1.35f, 0.58f, 0.1f), new Vector3(0.62f, 0.5f, 0.1f), new Vector3(0.66f, 0.52f, 0.1f), new Vector3(0.7f, 0.4f, 0.1f), new Vector3(0.7f, 0.5f, 0.1f), new Vector3(0.7f, 0.5f, 0.1f) };
        for (int i = 0; i < bp.Length; i++)
        {
            Blob(kick, "SignOutline", new Vector3(bp[i].x, by + bp[i].y, -0.03f), bs[i] * 1.12f, "SkyDeep", i % 4);
            Blob(kick, "SignCloud", new Vector3(bp[i].x, by + bp[i].y, 0f), bs[i], "Cream", i % 4);
        }

        // бревно (торцом к зрителю) = монета
        float zf = 0.07f;
        Cyl(kick, "SignLogOuter", new Vector3(-0.4f, by, zf), 0.15f, 0.05f, "WoodDark", 14, new Vector3(90f, 0f, 0f));
        Cyl(kick, "SignLogMid", new Vector3(-0.4f, by, zf + 0.004f), 0.115f, 0.058f, "WoodLight", 14, new Vector3(90f, 0f, 0f));
        Cyl(kick, "SignLogCore", new Vector3(-0.4f, by, zf + 0.008f), 0.055f, 0.066f, "LogEnd", 10, new Vector3(90f, 0f, 0f));
        Box(kick, "SignEq1", new Vector3(0f, by + 0.045f, zf), new Vector3(0.17f, 0.04f, 0.03f), "WoodDark");
        Box(kick, "SignEq2", new Vector3(0f, by - 0.045f, zf), new Vector3(0.17f, 0.04f, 0.03f), "WoodDark");
        Cyl(kick, "SignCoinEdge", new Vector3(0.4f, by, zf), 0.16f, 0.05f, "GoldDark", 16, new Vector3(90f, 0f, 0f));
        Cyl(kick, "SignCoinFace", new Vector3(0.4f, by, zf + 0.004f), 0.13f, 0.058f, "Gold", 16, new Vector3(90f, 0f, 0f));
        Cyl(kick, "SignCoinCore", new Vector3(0.4f, by, zf + 0.008f), 0.05f, 0.066f, "GoldDark", 10, new Vector3(90f, 0f, 0f));
    }

    // ---------- фонарь на цепочке (LanternLight подхватывает система света)
    void Lantern(Transform p, string name, Vector3 hangPoint)
    {
        var pivot = Obj(p, name + "Pivot", hangPoint);
        var t = pivot.transform;
        Rod(t, "Chain", Vector3.zero, new Vector3(0f, -0.14f, 0f), 0.012f, "MetalDark", 4);
        Box(t, "LampCapTop", new Vector3(0f, -0.2f, 0f), new Vector3(0.22f, 0.05f, 0.22f), "MetalDark");
        Box(t, "HutLamp", new Vector3(0f, -0.33f, 0f), new Vector3(0.16f, 0.2f, 0.16f), "HutLamp");
        Box(t, "LampBase", new Vector3(0f, -0.46f, 0f), new Vector3(0.2f, 0.05f, 0.2f), "MetalDark");
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                Box(t, "LampBar", new Vector3(sx * 0.08f, -0.33f, sz * 0.08f), new Vector3(0.025f, 0.22f, 0.025f), "MetalDark");

        var lgo = new GameObject("LanternLight_" + name);
        lgo.transform.SetParent(t, false);
        lgo.transform.localPosition = new Vector3(0f, -0.33f, 0f);
        var l = lgo.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(1.0f, 0.8f, 0.45f);
        l.range = 5f;
        l.intensity = 1.2f;
        l.shadows = LightShadows.None;
        swingers.Add(t);
    }

    // ---------- гирлянда флажков вдоль переднего свеса
    void BuildBunting(Transform p)
    {
        var b = Obj(p, "Bunting", Vector3.zero).transform;
        float z = run + 0.03f;
        float y = eaveY - 0.12f;
        Pennants(b, "BuntFront", new Vector3(-totW / 2f + 0.3f, y, z), new Vector3(totW / 2f - 0.3f, y, z), 0.14f, Mathf.RoundToInt(totW / 0.3f));
    }

    void Pennants(Transform p, string name, Vector3 a, Vector3 b, float sag, int count)
    {
        string[] cols = { "Red", "Sky", "Cream", "Pink", "Moss", "Roof" };
        int segs = Mathf.Max(6, count);
        Vector3 prev = a;
        for (int i = 1; i <= segs; i++)
        {
            float t = i / (float)segs;
            Vector3 cur = Vector3.Lerp(a, b, t) + Vector3.down * (sag * 4f * t * (1f - t));
            Rod(p, name + "Rope", prev, cur, 0.012f, "Rope", 4);
            prev = cur;
        }
        for (int i = 0; i < count; i++)
        {
            float t = (i + 0.5f) / count;
            Vector3 at = Vector3.Lerp(a, b, t) + Vector3.down * (sag * 4f * t * (1f - t));
            var hinge = Obj(p, name + "Hinge", at);
            NoShadow(Part(hinge.transform, name + "Flag", HutMeshLib.Prism, Mat(cols[i % cols.Length]), new Vector3(0f, -0.1f, 0f), new Vector3(0.17f, 0.2f, 0.025f), Quaternion.Euler(0f, 0f, 180f)));
            // флажок трепещет на ветру ВДОЛЬ гирлянды (поворот вокруг Y с бегущей волной), а не качается вперёд-назад
            // сквозь кромку навеса; в общий swingers не входит, поэтому порыв ветра при продаже его не «бьёт»
            cloths.Add(new HutLife.ClothChain { amp = 24f, phase = i * 0.55f, segs = new[] { hinge.transform } });
        }
    }

    // ---------- меловая доска с ценами (числа рисует ShopLife)
    void BuildPriceBoard(Transform p)
    {
        var board = Obj(p, "PriceBoard", new Vector3(-W / 2f - 0.6f, 0.06f, D / 2f + 0.0f));
        board.transform.localRotation = Quaternion.Euler(0f, 12f, 0f);
        var t = board.transform;
        float by = 0.98f;

        // ножки
        Beam(t, "BoardLegL", new Vector3(-0.38f, 0f, 0.06f), new Vector3(-0.34f, by - 0.3f, 0.0f), 0.07f, "WoodDark");
        Beam(t, "BoardLegR", new Vector3(0.38f, 0f, 0.06f), new Vector3(0.34f, by - 0.3f, 0.0f), 0.07f, "WoodDark");
        Beam(t, "BoardStrut", new Vector3(0f, 0f, -0.45f), new Vector3(0f, by + 0.1f, -0.04f), 0.06f, "WoodMid");

        // рама и грифельная панель
        Box(t, "BoardFrame", new Vector3(0f, by, 0f), new Vector3(1.0f, 0.84f, 0.08f), "WoodMid");
        Box(t, "BoardSlate", new Vector3(0f, by, 0.03f), new Vector3(0.86f, 0.7f, 0.05f), "Chalk");
        Box(t, "BoardTray", new Vector3(0f, by - 0.43f, 0.07f), new Vector3(0.8f, 0.05f, 0.1f), "WoodLight");
        Blob(t, "BoardCloudL", new Vector3(-0.42f, by + 0.46f, 0.0f), new Vector3(0.36f, 0.2f, 0.16f), "Cloud", 1);
        Blob(t, "BoardCloudR", new Vector3(-0.12f, by + 0.5f, 0.0f), new Vector3(0.28f, 0.18f, 0.14f), "Cloud", 2);

        float zf = 0.062f;
        float yLog = by + 0.17f, yPlank = by - 0.14f;

        // строка 1: бревно = цена
        Cyl(t, "IconLogOuter", new Vector3(-0.27f, yLog, zf), 0.1f, 0.03f, "WoodDark", 12, new Vector3(90f, 0f, 0f));
        Cyl(t, "IconLogMid", new Vector3(-0.27f, yLog, zf + 0.004f), 0.078f, 0.036f, "WoodLight", 12, new Vector3(90f, 0f, 0f));
        Cyl(t, "IconLogCore", new Vector3(-0.27f, yLog, zf + 0.008f), 0.035f, 0.04f, "LogEnd", 8, new Vector3(90f, 0f, 0f));
        Cyl(t, "IconCoin1", new Vector3(0.32f, yLog, zf), 0.085f, 0.03f, "GoldDark", 14, new Vector3(90f, 0f, 0f));
        Cyl(t, "IconCoin1b", new Vector3(0.32f, yLog, zf + 0.004f), 0.065f, 0.036f, "Gold", 14, new Vector3(90f, 0f, 0f));
        L.priceLog = Obj(t, "PriceLogAnchor", new Vector3(0.05f, yLog, zf + 0.02f)).transform;

        // строка 2: доски = цена
        Box(t, "IconPlankA", new Vector3(-0.27f, yPlank - 0.035f, zf), new Vector3(0.24f, 0.05f, 0.03f), "Plank");
        Box(t, "IconPlankB", new Vector3(-0.27f, yPlank + 0.025f, zf), new Vector3(0.24f, 0.05f, 0.03f), "PlankDark");
        Box(t, "IconPlankC", new Vector3(-0.27f, yPlank + 0.085f, zf), new Vector3(0.24f, 0.05f, 0.03f), "Plank");
        Cyl(t, "IconCoin2", new Vector3(0.32f, yPlank, zf), 0.085f, 0.03f, "GoldDark", 14, new Vector3(90f, 0f, 0f));
        Cyl(t, "IconCoin2b", new Vector3(0.32f, yPlank, zf + 0.004f), 0.065f, 0.036f, "Gold", 14, new Vector3(90f, 0f, 0f));
        L.pricePlank = Obj(t, "PricePlankAnchor", new Vector3(0.05f, yPlank, zf + 0.02f)).transform;

        // значок «Базарный день» (виден, пока готов навык): золотая звезда на ленте
        var badge = Obj(t, "BazaarBadge", new Vector3(0.46f, by + 0.46f, 0.08f));
        Star(badge.transform, Vector3.zero, 0.2f, "Gold", "GoldDark");
        Box(badge.transform, "BadgeRibbonL", new Vector3(-0.06f, -0.22f, -0.01f), new Vector3(0.07f, 0.2f, 0.02f), "Red", new Vector3(0f, 0f, 14f));
        Box(badge.transform, "BadgeRibbonR", new Vector3(0.06f, -0.22f, -0.01f), new Vector3(0.07f, 0.2f, 0.02f), "Red", new Vector3(0f, 0f, -14f));
        badge.SetActive(false);
        L.bazaarBadge = badge;
    }

    // плоская звезда из двух повёрнутых пятиугольников, лицом к +Z
    static void Star(Transform p, Vector3 pos, float r, string matA, string matB)
    {
        var s = Obj(p, "Star", pos).transform;
        Cyl(s, "StarA", Vector3.zero, r, 0.05f, matA, 5, new Vector3(90f, 0f, 0f));
        Cyl(s, "StarB", new Vector3(0f, 0f, 0.004f), r, 0.05f, matB, 5, new Vector3(90f, 36f, 0f));
        Cyl(s, "StarCore", new Vector3(0f, 0f, 0.012f), r * 0.45f, 0.06f, matA, 5, new Vector3(90f, 18f, 0f));
    }

    // ---------- ярусы товара: растут с продажами (ShopLife включает группы по порогам)
    GameObject Tier(Transform parent, string name, int idx)
    {
        var g = Obj(parent, name, Vector3.zero);
        g.SetActive(idx == 0);
        return g;
    }

    void LogRow(Transform p, Vector3 b, float len, float r, int layer, int n)
    {
        float y = b.y + r + layer * r * 1.75f;
        for (int j = 0; j < n; j++)
        {
            float z = b.z + (j - (n - 1) / 2f) * 2f * r * 1.04f;
            float jl = len + R(-0.04f, 0.04f);
            float jx = R(-0.03f, 0.03f);
            Log(p, "StockLog", new Vector3(b.x + jx - jl / 2f, y, z), new Vector3(b.x + jx + jl / 2f, y, z), r, "WoodMid", "LogEnd");
        }
    }

    void BuildStock(Transform p)
    {
        var stock = Obj(p, "Stock", Vector3.zero).transform;
        float rz = -D / 2f + 0.3f;
        float palTop = PlatH + 0.11f;
        Vector3 a = new Vector3(palA.x, palTop, palA.z);
        Vector3 b = new Vector3(palB.x, palTop, palB.z);

        // бревна
        var l0 = Tier(stock, "StockLogs_0", 0).transform;
        LogRow(l0, a, 0.88f, 0.095f, 0, 3); LogRow(l0, a, 0.88f, 0.095f, 1, 2);
        L.logTiers[0] = l0.gameObject;

        var l1 = Tier(stock, "StockLogs_1", 1).transform;
        LogRow(l1, a, 0.88f, 0.095f, 2, 1);
        LogRow(l1, b, 0.88f, 0.095f, 0, 3); LogRow(l1, b, 0.88f, 0.095f, 1, 2);
        L.logTiers[1] = l1.gameObject;

        var l2 = Tier(stock, "StockLogs_2", 2).transform;
        LogRow(l2, b, 0.88f, 0.095f, 2, 1);
        Vector3 shelf = new Vector3(-W / 2f + 1.3f, PlatH + 1.05f + 0.03f, rz);
        LogRow(l2, shelf, 0.7f, 0.085f, 0, 2); LogRow(l2, shelf, 0.7f, 0.085f, 1, 1);
        L.logTiers[2] = l2.gameObject;

        var l3 = Tier(stock, "StockLogs_3", 3).transform;
        Vector3 bundle = new Vector3(-W / 2f - 0.95f, 0.06f, 0.7f);
        LogRow(l3, bundle, 1.0f, 0.1f, 0, 3); LogRow(l3, bundle, 1.0f, 0.1f, 1, 2);
        for (int k = -1; k <= 1; k += 2)
            Box(l3, "BundleStrap", new Vector3(bundle.x + k * 0.28f, bundle.y + 0.28f, bundle.z), new Vector3(0.05f, 0.5f, 0.7f), "Rope");
        Blob(l3, "BundleBow", new Vector3(bundle.x, bundle.y + 0.56f, bundle.z), new Vector3(0.22f, 0.12f, 0.16f), "Red", 1);
        L.logTiers[3] = l3.gameObject;

        // доски
        var p0 = Tier(stock, "StockPlanks_0", 0).transform;
        PlankStack(p0, "ShelfPlanks", new Vector3(0.75f, PlatH + 1.65f + 0.03f, rz), 0.8f, 0.3f, 2, true, 0f);
        L.plankTiers[0] = p0.gameObject;

        var p1 = Tier(stock, "StockPlanks_1", 1).transform;
        PlankStack(p1, "FloorPlanks", new Vector3(W / 2f - 0.7f, PlatH, -0.6f), 1.0f, 0.5f, 3, true, 0f);
        L.plankTiers[1] = p1.gameObject;

        var p2 = Tier(stock, "StockPlanks_2", 2).transform;
        PlankStack(p2, "FloorPlanksTop", new Vector3(W / 2f - 0.7f, PlatH + 0.225f, -0.6f), 1.0f, 0.5f, 2, true, 3f);
        PlankStack(p2, "ShelfPlanks2", new Vector3(-0.6f, PlatH + 1.05f + 0.03f, rz), 0.7f, 0.3f, 2, true, 0f);
        L.plankTiers[2] = p2.gameObject;

        var p3 = Tier(stock, "StockPlanks_3", 3).transform;
        PlankStack(p3, "LawnPlanks", new Vector3(W / 2f + 0.95f, 0.06f, -0.3f), 1.25f, 0.6f, 4, true, -10f);
        Blob(p3, "PlankBow", new Vector3(W / 2f + 0.95f, 0.06f + 0.35f, -0.3f), new Vector3(0.22f, 0.12f, 0.16f), "Pink", 2);
        L.plankTiers[3] = p3.gameObject;
    }

    // ---------- реквизит на прилавке: сундук с монетами (ярусы), весы, колокольчик, книга
    void CoinStack(Transform p, Vector3 lp, int count)
    {
        for (int i = 0; i < count; i++)
            Cyl(p, "Coin", lp + new Vector3(R(-0.004f, 0.004f), 0.011f + i * 0.021f, R(-0.004f, 0.004f)), 0.05f, 0.02f, i % 2 == 0 ? "Gold" : "GoldDark", 12);
    }

    void BuildCounterProps(Transform p)
    {
        float topY = PlatH + 0.95f;
        float zc = counterZ + 0.06f;
        float xl = counterL, ccx = cx;
        var props = Obj(p, "CounterProps", Vector3.zero).transform;

        // --- открытый сундук
        var chest = Obj(props, "Chest", new Vector3(xl + 0.5f, topY, zc - 0.12f));
        chest.transform.localRotation = Quaternion.Euler(0f, 8f, 0f);
        var ch = chest.transform;
        Box(ch, "ChestBody", new Vector3(0f, 0.09f, 0f), new Vector3(0.52f, 0.18f, 0.32f), "WoodDark");
        Box(ch, "ChestBandL", new Vector3(-0.16f, 0.09f, 0f), new Vector3(0.05f, 0.19f, 0.33f), "Metal");
        Box(ch, "ChestBandR", new Vector3(0.16f, 0.09f, 0f), new Vector3(0.05f, 0.19f, 0.33f), "Metal");
        Box(ch, "ChestLock", new Vector3(0f, 0.13f, 0.165f), new Vector3(0.07f, 0.08f, 0.03f), "Gold");
        var hinge = Obj(ch, "LidHinge", new Vector3(0f, 0.18f, -0.15f));
        hinge.transform.localRotation = Quaternion.Euler(-105f, 0f, 0f);
        Box(hinge.transform, "ChestLid", new Vector3(0f, 0.025f, 0.16f), new Vector3(0.54f, 0.05f, 0.33f), "WoodMid");
        Box(hinge.transform, "ChestLidBandL", new Vector3(-0.16f, 0.03f, 0.16f), new Vector3(0.05f, 0.06f, 0.34f), "Metal");
        Box(hinge.transform, "ChestLidBandR", new Vector3(0.16f, 0.03f, 0.16f), new Vector3(0.05f, 0.06f, 0.34f), "Metal");

        // ярусы монет
        var c0 = Tier(props, "StockCoins_0", 0).transform;
        Blob(c0, "CoinMound", new Vector3(xl + 0.5f, topY + 0.2f, zc - 0.12f), new Vector3(0.4f, 0.12f, 0.24f), "Gold", 1);
        CoinStack(c0, new Vector3(xl + 0.95f, topY, zc + 0.18f), 5);
        CoinStack(c0, new Vector3(xl + 1.06f, topY, zc + 0.22f), 3);
        L.coinTiers[0] = c0.gameObject;

        var c1 = Tier(props, "StockCoins_1", 1).transform;
        Blob(c1, "CoinMoundBig", new Vector3(xl + 0.5f, topY + 0.25f, zc - 0.12f), new Vector3(0.46f, 0.2f, 0.27f), "Gold", 2);
        CoinStack(c1, new Vector3(xl + 0.84f, topY, zc + 0.25f), 6);
        CoinStack(c1, new Vector3(xl + 1.18f, topY, zc + 0.14f), 4);
        Cyl(c1, "LooseCoin", new Vector3(xl + 0.75f, topY + 0.011f, zc + 0.02f), 0.05f, 0.02f, "Gold", 12);
        L.coinTiers[1] = c1.gameObject;

        var c2 = Tier(props, "StockCoins_2", 2).transform;
        Blob(c2, "CoinSack", new Vector3(xl - 0.0f, topY + 0.17f, zc + 0.22f), new Vector3(0.3f, 0.34f, 0.26f), "Cream", 3);
        Blob(c2, "SackNeck", new Vector3(xl - 0.0f, topY + 0.35f, zc + 0.22f), new Vector3(0.14f, 0.1f, 0.12f), "Cream", 1);
        Rod(c2, "SackTie", new Vector3(xl - 0.07f, topY + 0.32f, zc + 0.22f), new Vector3(xl + 0.07f, topY + 0.32f, zc + 0.22f), 0.018f, "Red", 5);
        Cyl(c2, "SackCoin", new Vector3(xl, topY + 0.2f, zc + 0.36f), 0.07f, 0.02f, "Gold", 12, new Vector3(90f, 0f, 0f));
        CoinStack(c2, new Vector3(xl + 0.72f, topY, zc + 0.3f), 8);
        L.coinTiers[2] = c2.gameObject;

        var c3 = Tier(props, "StockCoins_3", 3).transform;
        Blob(c3, "CoinMoundMax", new Vector3(xl + 0.5f, topY + 0.3f, zc - 0.12f), new Vector3(0.52f, 0.26f, 0.3f), "Gold", 0);
        Box(c3, "GoldBarA", new Vector3(xl + 1.35f, topY + 0.04f, zc + 0.18f), new Vector3(0.22f, 0.07f, 0.1f), "Gold", new Vector3(0f, 10f, 0f));
        Box(c3, "GoldBarB", new Vector3(xl + 1.37f, topY + 0.11f, zc + 0.18f), new Vector3(0.2f, 0.07f, 0.09f), "GoldDark", new Vector3(0f, -6f, 0f));
        var star = Obj(c3, "GoldStarFloat", new Vector3(xl + 0.5f, topY + 0.72f, zc - 0.12f)).transform;
        Star(star, Vector3.zero, 0.13f, "Gold", "GoldDark");
        L.goldStar = star;
        L.coinTiers[3] = c3.gameObject;

        L.glintPoint = Obj(props, "GlintPoint", new Vector3(xl + 0.5f, topY + 0.36f, zc - 0.05f)).transform;

        // монета-образец для вылета (выключена, ShopLife клонирует)
        var proto = Obj(props, "CoinProto", new Vector3(xl + 0.5f, topY + 0.3f, zc));
        Cyl(proto.transform, "ProtoEdge", Vector3.zero, 0.07f, 0.025f, "GoldDark", 12);
        Cyl(proto.transform, "ProtoFace", new Vector3(0f, 0.002f, 0f), 0.055f, 0.03f, "Gold", 12);
        proto.SetActive(false);
        L.coinProto = proto.transform;

        // --- весы: балка и чаши двигает ShopLife
        var sc = Obj(props, "Scales", new Vector3(ccx + 0.15f, topY, zc - 0.05f));
        var st = sc.transform;
        Cyl(st, "ScalesBase", new Vector3(0f, 0.02f, 0f), 0.1f, 0.04f, "Metal", 14);
        Cyl(st, "ScalesPole", new Vector3(0f, 0.265f, 0f), 0.018f, 0.45f, "Metal", 8);
        Cyl(st, "ScalesHub", new Vector3(0f, 0.5f, 0f), 0.032f, 0.04f, "Gold", 10, new Vector3(90f, 0f, 0f));
        var beam = Obj(st, "ScalesBeam", new Vector3(0f, 0.5f, 0f));
        Box(beam.transform, "BeamBar", Vector3.zero, new Vector3(0.52f, 0.025f, 0.03f), "Metal");
        Cyl(beam.transform, "BeamEndL", new Vector3(-0.26f, 0f, 0f), 0.02f, 0.04f, "Gold", 8, new Vector3(90f, 0f, 0f));
        Cyl(beam.transform, "BeamEndR", new Vector3(0.26f, 0f, 0f), 0.02f, 0.04f, "Gold", 8, new Vector3(90f, 0f, 0f));
        L.scalesBeam = beam.transform;

        var tilt = Quaternion.Euler(0f, 0f, L.scalesTilt);
        var panLo = Obj(st, "PanL", new Vector3(0f, 0.5f, 0f) + tilt * new Vector3(-L.scalesArm, 0f, 0f) + Vector3.down * L.scalesChain);
        var panRo = Obj(st, "PanR", new Vector3(0f, 0.5f, 0f) + tilt * new Vector3(L.scalesArm, 0f, 0f) + Vector3.down * L.scalesChain);
        foreach (var pan in new[] { panLo.transform, panRo.transform })
        {
            Cyl(pan, "Pan", Vector3.zero, 0.11f, 0.02f, "Gold", 14);
            Rod(pan, "ChainA", new Vector3(-0.09f, 0.01f, 0f), new Vector3(0f, L.scalesChain, 0f), 0.006f, "Metal", 4);
            Rod(pan, "ChainB", new Vector3(0.09f, 0.01f, 0f), new Vector3(0f, L.scalesChain, 0f), 0.006f, "Metal", 4);
            Rod(pan, "ChainC", new Vector3(0f, 0.01f, 0.09f), new Vector3(0f, L.scalesChain, 0f), 0.006f, "Metal", 4);
        }
        Log(panLo.transform, "PanLog", new Vector3(0f, 0.06f, -0.06f), new Vector3(0f, 0.06f, 0.06f), 0.045f, "WoodMid", "LogEnd");
        Cyl(panRo.transform, "PanCoin", new Vector3(0f, 0.03f, 0f), 0.05f, 0.015f, "Gold", 12);
        L.panL = panLo.transform;
        L.panR = panRo.transform;

        // --- колокольчик (дзинь при продаже и приветствии)
        var bell = Obj(props, "CallBell", new Vector3(counterR - 0.22f, topY, zc + 0.22f));
        Cyl(bell.transform, "BellBase", new Vector3(0f, 0.015f, 0f), 0.09f, 0.03f, "GoldDark", 12);
        var dome = Obj(bell.transform, "BellDome", new Vector3(0f, 0.03f, 0f));
        Blob(dome.transform, "BellCap", new Vector3(0f, 0.04f, 0f), new Vector3(0.17f, 0.1f, 0.17f), "Gold", 2);
        Cyl(dome.transform, "BellKnob", new Vector3(0f, 0.095f, 0f), 0.02f, 0.03f, "Red", 8);
        L.bellDome = dome.transform;

        // --- счётная книга
        var book = Obj(props, "Ledger", new Vector3(counterR - 0.55f, topY, zc + 0.02f));
        book.transform.localRotation = Quaternion.Euler(0f, 14f, 0f);
        Box(book.transform, "BookBack", new Vector3(0f, 0.01f, 0f), new Vector3(0.3f, 0.02f, 0.22f), "Red");
        Box(book.transform, "BookPages", new Vector3(0.005f, 0.035f, 0f), new Vector3(0.28f, 0.03f, 0.2f), "Cream");
        Box(book.transform, "BookCover", new Vector3(0f, 0.06f, 0f), new Vector3(0.3f, 0.02f, 0.22f), "Red");
        Box(book.transform, "BookSpine", new Vector3(-0.14f, 0.035f, 0f), new Vector3(0.025f, 0.07f, 0.22f), "Red");
        Box(book.transform, "BookMark", new Vector3(0.05f, 0.075f, 0.02f), new Vector3(0.1f, 0.005f, 0.04f), "Gold");
        Box(book.transform, "Quill", new Vector3(0.1f, 0.09f, -0.04f), new Vector3(0.18f, 0.012f, 0.012f), "Cream", new Vector3(0f, -25f, 6f));
    }

    // ---------- вход: коврик и горшки
    void BuildEntrance(Transform p)
    {
        var e = Obj(p, "Entrance", Vector3.zero).transform;
        var mat = Cyl(e, "WelcomeMat", new Vector3(stepX, PlatH + 0.012f, D / 2f - 0.3f), 0.4f, 0.025f, "Pink", 14);
        mat.transform.localScale = new Vector3(0.9f, 0.025f, 0.6f);
        NoShadow(mat);
        var matIn = Cyl(e, "WelcomeMatIn", new Vector3(stepX, PlatH + 0.016f, D / 2f - 0.3f), 0.4f, 0.025f, "Cream", 14);
        matIn.transform.localScale = new Vector3(0.6f, 0.025f, 0.38f);
        NoShadow(matIn);

        if (!S.decPots) return;
        Pot(e, new Vector3(stepX - 0.62f, PlatH, D / 2f - 0.1f), "Sky", "Pink");
        Pot(e, new Vector3(stepX + 0.62f, PlatH, D / 2f - 0.1f), "Red", "Cream");
    }

    void Pot(Transform p, Vector3 pos, string potMat, string petal)
    {
        var g = Obj(p, "FlowerPot", pos);
        var t = g.transform;
        Cyl(t, "Pot", new Vector3(0f, 0.13f, 0f), 0.13f, 0.26f, potMat, 10);
        Cyl(t, "PotRim", new Vector3(0f, 0.27f, 0f), 0.15f, 0.04f, potMat, 10);
        Blob(t, "Bush", new Vector3(0f, 0.4f, 0f), new Vector3(0.36f, 0.26f, 0.36f), "Leaf", 1);
        for (int i = 0; i < 4; i++)
        {
            float a = i * 90f + R(-20f, 20f);
            var pos2 = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * 0.12f, 0.5f + R(0f, 0.06f), Mathf.Sin(a * Mathf.Deg2Rad) * 0.12f);
            Blob(t, "Bloom", pos2, new Vector3(0.1f, 0.07f, 0.1f), petal, i % 4);
            Blob(t, "BloomCore", pos2 + Vector3.up * 0.03f, new Vector3(0.04f, 0.03f, 0.04f), "RoofShade", 2);
        }
    }
}
#endif
