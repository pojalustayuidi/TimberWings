#if UNITY_EDITOR
// Assets/_Project/Editor/Island/Forest/Look/TreeToonSetup.cs (Editor)
// Tools > Forest > Tree Toon:
//   1. Создать материалы        Tree_Leaf_Green / Tree_Leaf_Yellow / Tree_Leaf_Dark / Tree_Bark (существующие не трогает)
//   1b. Сбросить материалы      возвращает стартовые значения
//   2. Тестовая сцена           Assets/_Project/Scenes/TreeToonTest.unity: 3 дерева из примитивов ToonKit + 4 настоящих дерева из префабов (материалы подменены только на инстансах сцены)
//   3. Скриншот 55              <проект>/Screenshots/TreeToon_55.png
// Рантайм-кода нет. Префабы деревьев и ToonKit не меняются (ToonKit используется только как источник мешей в памяти).
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using static EditorAssets;

public static class TreeToonSetup
{
    public const string ShaderPath = "Assets/_Project/Shaders/Forest/TreeToon.shader";
    public const string MatDir = "Assets/_Project/Materials/Forest";
    const string TestDir = MatDir + "/Test";
    const string ScenePath = "Assets/_Project/Scenes/TreeToonTest.unity";
    const string TreePrefabDir = "Assets/_Project/Prefabs/Trees/Generated/";
    const string CamName = "TreeToon_Cam";

    // ------------------------------------------------------------ материалы
    [MenuItem("Tools/BuilderSim/Лес/Tree Toon/1. Создать материалы", false, 250)]
    public static void CreateMaterials() { MakeAll(false); }

    [MenuItem("Tools/BuilderSim/Лес/Tree Toon/1b. Сбросить материалы к стартовым", false, 251)]
    public static void ResetMaterials() { MakeAll(true); }

    public static Shader LoadShader()
    {
        Shader sh = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        if (sh == null)
        {
            AssetDatabase.ImportAsset(ShaderPath, ImportAssetOptions.ForceSynchronousImport);
            sh = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        }
        if (sh == null)
        {
            Debug.LogError("[TreeToon] Нет шейдера по пути " + ShaderPath + ". Положи TreeToon.shader в Assets/_Project/Shaders/Forest/.");
            return null;
        }
        if (!sh.isSupported)
            Debug.LogError("[TreeToon] Шейдер Project/TreeToon не скомпилировался: смотри ошибки в Console (выбери шейдер в Project, там список ошибок). Пришли их мне.");
        return sh;
    }

    public sealed class MatSet { public Material green, yellow, dark, bark; }

    // библиотека материалов TreeToon: имя, верх, низ, высота градиента (м, локальный Y меша), дрожь листвы, рим, сила градиента
    sealed class Look
    {
        public string name; public Color top, bottom; public float gMin, gMax, flutter, rim, gradStrength;
        public Look(string n, Color t, Color b, float g0, float g1, float fl, float r, float gs = 1f)
        { name = n; top = t; bottom = b; gMin = g0; gMax = g1; flutter = fl; rim = r; gradStrength = gs; }
    }

    static Color C(float r, float g, float b) { return new Color(r, g, b); }

    static readonly Look[] Looks =
    {
        // основные четыре (задача 1)
        new Look("Tree_Leaf_Green",     C(0.56f, 0.80f, 0.16f), C(0.05f, 0.25f, 0.12f), 1.6f, 5.4f, 0.025f, 0.12f),
        new Look("Tree_Leaf_Yellow",    C(0.94f, 0.78f, 0.15f), C(0.42f, 0.33f, 0.05f), 1.6f, 5.4f, 0.025f, 0.14f),
        new Look("Tree_Leaf_Dark",      C(0.16f, 0.45f, 0.28f), C(0.03f, 0.16f, 0.16f), 1.2f, 7.0f, 0.012f, 0.08f),
        new Look("Tree_Bark",           C(0.50f, 0.33f, 0.20f), C(0.20f, 0.13f, 0.09f), 0.0f, 3.0f, 0.0f, 0.0f),
        // виды (задача 2)
        new Look("Tree_Leaf_Birch",     C(0.70f, 0.86f, 0.28f), C(0.12f, 0.36f, 0.14f), 1.6f, 5.4f, 0.025f, 0.14f),
        new Look("Tree_Leaf_Orange",    C(0.96f, 0.62f, 0.12f), C(0.50f, 0.22f, 0.05f), 1.6f, 5.4f, 0.025f, 0.14f),
        new Look("Tree_Leaf_DarkUnder", C(0.06f, 0.24f, 0.18f), C(0.02f, 0.09f, 0.10f), 1.2f, 7.0f, 0.012f, 0.0f),
        new Look("Tree_Leaf_Oak",       C(0.50f, 0.74f, 0.14f), C(0.04f, 0.22f, 0.10f), 2.8f, 8.2f, 0.020f, 0.12f),
        new Look("Tree_Leaf_OakAlt",    C(0.68f, 0.80f, 0.16f), C(0.08f, 0.28f, 0.10f), 2.8f, 8.2f, 0.020f, 0.12f),
        new Look("Tree_Bark_Birch",     C(0.92f, 0.90f, 0.82f), C(0.45f, 0.42f, 0.38f), 0.0f, 3.0f, 0.0f, 0.0f),
        new Look("Tree_BarkMark",       C(0.18f, 0.16f, 0.15f), C(0.10f, 0.09f, 0.09f), 0.0f, 3.0f, 0.0f, 0.0f),
        new Look("Tree_Bark_Oak",       C(0.38f, 0.26f, 0.17f), C(0.14f, 0.10f, 0.07f), 0.0f, 4.5f, 0.0f, 0.0f),
        new Look("Tree_Bark_Willow",    C(0.42f, 0.36f, 0.31f), C(0.20f, 0.17f, 0.15f), 0.0f, 3.0f, 0.0f, 0.0f),
        new Look("Tree_Twig_Willow",    C(0.55f, 0.48f, 0.42f), C(0.30f, 0.26f, 0.22f), 0.0f, 5.0f, 0.0f, 0.0f),
        // тёмный низ крон и светлые кончики ёлки (сабмеши 2-3, см. шапку TreeSpeciesMeshes)
        new Look("Tree_Leaf_DarkTip",     C(0.34f, 0.66f, 0.30f), C(0.06f, 0.28f, 0.20f), 1.2f, 7.0f, 0.012f, 0.08f),
        new Look("Tree_Leaf_Under",       C(0.14f, 0.42f, 0.12f), C(0.03f, 0.16f, 0.09f), 1.6f, 5.4f, 0.025f, 0.0f),
        new Look("Tree_Leaf_UnderYellow", C(0.70f, 0.50f, 0.08f), C(0.30f, 0.20f, 0.04f), 1.6f, 5.4f, 0.025f, 0.0f),
        new Look("Tree_Leaf_OakUnder",    C(0.16f, 0.44f, 0.10f), C(0.03f, 0.15f, 0.08f), 2.8f, 8.2f, 0.020f, 0.0f),
        new Look("Tree_Leaf_BirchUnder",  C(0.46f, 0.66f, 0.18f), C(0.10f, 0.28f, 0.12f), 1.6f, 5.4f, 0.025f, 0.0f),
        new Look("Tree_Cut",            C(0.78f, 0.58f, 0.34f), C(0.78f, 0.58f, 0.34f), 0.0f, 1.0f, 0.0f, 0.0f, 0f),
    };

    static Shader cachedShader;

    static MatSet MakeAll(bool force)
    {
        if (LoadShader() == null) return null;
        EnsureFolder(MatDir);
        var s = new MatSet();
        s.green = Get("Tree_Leaf_Green", force);
        s.yellow = Get("Tree_Leaf_Yellow", force);
        s.dark = Get("Tree_Leaf_Dark", force);
        s.bark = Get("Tree_Bark", force);
        if (force) for (int i = 0; i < Looks.Length; i++) Get(Looks[i].name, true);
        AssetDatabase.SaveAssets();
        Debug.Log("[TreeToon] Материалы готовы: " + MatDir + (force ? " (сброшены)" : ""));
        return s;
    }

    // материал библиотеки по имени (создаётся при первом обращении, существующий не трогается). null, если нет шейдера
    public static Material Library(string name) { return Get(name, false); }

    static Material Get(string name, bool force)
    {
        Look look = null;
        for (int i = 0; i < Looks.Length; i++) if (Looks[i].name == name) { look = Looks[i]; break; }
        if (look == null) { Debug.LogError("[TreeToon] Нет материала в библиотеке: " + name); return null; }
        if (cachedShader == null) cachedShader = LoadShader();
        if (cachedShader == null) return null;
        EnsureFolder(MatDir);
        return Ensure(look, cachedShader, force);
    }

    static Material Ensure(Look l, Shader sh, bool force)
    {
        string path = MatDir + "/" + l.name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool created = false;
        if (m == null)
        {
            m = new Material(sh) { name = l.name };
            AssetDatabase.CreateAsset(m, path);
            created = true;
        }
        else if (m.shader != sh)
        {
            m.shader = sh;
            created = true;
        }
        m.enableInstancing = true;
        if (created || force)
        {
            m.SetColor("_BaseColor", l.top);
            m.SetColor("_BottomColor", l.bottom);
            m.SetFloat("_GradMin", l.gMin);
            m.SetFloat("_GradMax", l.gMax);
            m.SetFloat("_GradStrength", l.gradStrength);
            m.SetFloat("_Flutter", l.flutter);
            m.SetFloat("_RimStrength", l.rim);
            m.SetFloat("_Step1", 0.55f);
            m.SetFloat("_Step2", 0.82f);
            m.SetFloat("_StepSoft", 0.05f);
            m.SetFloat("_Level0", 0.42f);
            m.SetFloat("_Level1", 0.72f);
            m.SetFloat("_Level2", 0.98f);
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    // ------------------------------------------------------------ тестовая сцена
    [MenuItem("Tools/BuilderSim/Лес/Tree Toon/2. Тестовая сцена", false, 252)]
    public static void BuildTestScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[TreeToon] Останови Play Mode.");
            return;
        }
        MatSet mats = MakeAll(false);
        if (mats == null) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EnsureFolder(TestDir);
        EnsureFolder("Assets/_Project/Scenes");

        var sc = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // свет
        var lg = new GameObject("Sun");
        var light = lg.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.96f, 0.82f);
        light.intensity = 1.25f;
        light.shadows = LightShadows.Soft;
        lg.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.55f, 0.64f, 0.72f);
        RenderSettings.sun = light;

        // земля
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(3f, 1f, 3f);
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null) lit = Shader.Find("Standard");
        var gm = new Material(lit) { name = "TreeToon_Ground" };
        if (gm.HasProperty("_BaseColor")) gm.SetColor("_BaseColor", new Color(0.36f, 0.56f, 0.16f));
        if (gm.HasProperty("_Color")) gm.SetColor("_Color", new Color(0.36f, 0.56f, 0.16f));
        if (gm.HasProperty("_Smoothness")) gm.SetFloat("_Smoothness", 0f);
        AssetDatabase.DeleteAsset(TestDir + "/TreeToon_Ground.mat");
        AssetDatabase.CreateAsset(gm, TestDir + "/TreeToon_Ground.mat");
        ground.GetComponent<MeshRenderer>().sharedMaterial = gm;

        // деревья из мешей ToonKit (в метрах, пивот у корня)
        var kit = new ToonKit();
        var root = new GameObject("TestTrees").transform;
        BuildBlobTree(root, kit, "Tree_Green", new Vector3(-5f, 0f, 0f), mats.green, mats.bark, false);
        BuildBlobTree(root, kit, "Tree_Yellow", new Vector3(0f, 0f, 0f), mats.yellow, mats.bark, false);
        BuildBlobTree(root, kit, "Tree_Dark", new Vector3(5f, 0f, 0f), mats.dark, mats.bark, true);

        // настоящие деревья из префабов (если собраны Tools > Forest > Trees > Build Species)
        var real = new GameObject("RealTrees").transform;
        AddReal(real, "Tree_Random_A_s1", new Vector3(-7.5f, 0f, 8f), mats.bark, mats.green, mats.green);
        AddReal(real, "Tree_Birch_A_s1", new Vector3(-2.5f, 0f, 8f), mats.bark, mats.green, mats.bark);
        AddReal(real, "Tree_Autumn_A_s1", new Vector3(2.5f, 0f, 8f), mats.bark, mats.yellow, mats.yellow);
        AddReal(real, "Tree_Cedar_A_s1", new Vector3(7.5f, 0f, 8f), mats.bark, mats.dark, mats.dark);

        // камера: наклон 55
        var cg = new GameObject(CamName);
        var cam = cg.AddComponent<Camera>();
        cam.fieldOfView = 40f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.10f, 0.22f, 0.12f);
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 200f;
        cg.tag = "MainCamera";
        Quaternion rot = Quaternion.Euler(55f, 0f, 0f);
        Vector3 center = new Vector3(0f, 2.2f, 4f);
        cg.transform.rotation = rot;
        cg.transform.position = center - rot * Vector3.forward * 24f;

        EditorSceneManager.SaveScene(sc, ScenePath);
        Selection.activeGameObject = cg;
        Debug.Log("[TreeToon] Сцена: " + ScenePath + ". Для движения ветра в Scene view включи Always Refresh; в Game view ветер идёт сам.");
    }

    static void BuildBlobTree(Transform parent, ToonKit kit, string name, Vector3 pos, Material leaf, Material bark, bool spruce)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.position = pos;

        Part(t, name + "_Trunk", Bake(kit.cyl, new Vector3(0f, 1.3f, 0f), new Vector3(0.55f, 2.6f, 0.55f), name + "_trunk"), bark);

        if (!spruce)
        {
            Part(t, name + "_Crown", Bake(kit.blob[1], new Vector3(0f, 3.8f, 0f), new Vector3(3.6f, 3.2f, 3.6f), name + "_crown"), leaf);
            Part(t, name + "_ClumpA", Bake(kit.blob[0], new Vector3(1.2f, 3.0f, 0.6f), new Vector3(2.0f, 1.8f, 2.0f), name + "_clumpA"), leaf);
            Part(t, name + "_ClumpB", Bake(kit.blob[2], new Vector3(-1.1f, 4.6f, -0.5f), new Vector3(1.9f, 1.7f, 1.9f), name + "_clumpB"), leaf);
        }
        else
        {
            Part(t, name + "_Tier1", Bake(kit.cone, new Vector3(0f, 2.6f, 0f), new Vector3(3.2f, 2.4f, 3.2f), name + "_t1"), leaf);
            Part(t, name + "_Tier2", Bake(kit.cone, new Vector3(0f, 4.0f, 0f), new Vector3(2.5f, 2.2f, 2.5f), name + "_t2"), leaf);
            Part(t, name + "_Tier3", Bake(kit.cone, new Vector3(0f, 5.3f, 0f), new Vector3(1.7f, 2.0f, 1.7f), name + "_t3"), leaf);
        }
    }

    static void Part(Transform parent, string name, Mesh mesh, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = ShadowCastingMode.On;
        mr.receiveShadows = true;
    }

    // копия меша в метрах относительно корня дерева, нормали пересчитаны (меши ToonKit с плоскими гранями)
    static Mesh Bake(Mesh src, Vector3 center, Vector3 scale, string name)
    {
        Vector3[] vs = src.vertices;
        int[] ts = src.triangles;
        var nv = new Vector3[vs.Length];
        for (int i = 0; i < vs.Length; i++) nv[i] = Vector3.Scale(vs[i], scale) + center;

        var m = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
        m.vertices = nv;
        m.triangles = ts;
        m.RecalculateNormals();
        m.RecalculateBounds();

        string path = TestDir + "/" + name + ".asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static void AddReal(Transform parent, string file, Vector3 pos, params Material[] map)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TreePrefabDir + file + ".prefab");
        if (prefab == null)
        {
            Debug.LogWarning("[TreeToon] Нет префаба " + file + " (Tools > Forest > Trees > Build Species). Пропускаю.");
            return;
        }
        var go = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (go == null) return;
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var rs = go.GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < rs.Length; i++)
        {
            Material[] cur = rs[i].sharedMaterials;
            for (int j = 0; j < cur.Length; j++) cur[j] = map[Mathf.Min(j, map.Length - 1)];
            rs[i].sharedMaterials = cur;
        }
    }

    // ------------------------------------------------------------ скриншот
    [MenuItem("Tools/BuilderSim/Лес/Tree Toon/3. Скриншот 55", false, 253)]
    public static void Screenshot()
    {
        var cg = GameObject.Find(CamName);
        Camera cam = cg != null ? cg.GetComponent<Camera>() : Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[TreeToon] Нет камеры. Сначала Tools > Forest > Tree Toon > 2. Тестовая сцена.");
            return;
        }

        const int w = 1920, h = 1080;
        var rt = new RenderTexture(w, h, 24);
        RenderTexture prevT = cam.targetTexture;
        RenderTexture prevA = RenderTexture.active;
        try
        {
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();

            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots"));
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, "TreeToon_55.png");
            File.WriteAllBytes(file, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Debug.Log("[TreeToon] Скриншот: " + file);
            EditorUtility.RevealInFinder(file);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[TreeToon] Скриншот не получился (" + e.Message + "). Сделай снимок из Game view, камера TreeToon_Cam.");
        }
        finally
        {
            cam.targetTexture = prevT;
            RenderTexture.active = prevA;
            rt.Release();
            Object.DestroyImmediate(rt);
        }
    }

}
#endif
