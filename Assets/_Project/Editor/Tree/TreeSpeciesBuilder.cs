#if UNITY_EDITOR
// Assets/_Project/Editor/Tree/TreeSpeciesBuilder.cs (Editor)
// Tools → Forest → Trees → Build Species: строит 5 видов деревьев × 3 вариации формы (A/B/C = seed 1/2/3) и сохраняет префабами.
// Tools → Forest → Trees → Open TreeShowcase: открывает сцену-превью (ряд на вид: эталон Tree_Random и все вариации × размеры).
//
// Что создаётся (всё в Assets/_Project/Prefabs/Trees/Generated/):
//   Tree_Cedar_A_s1.prefab ... Tree_Random_C_s3.prefab, Willow_Dead_1_s1 ... (по 3-4 вариации на вид), Stump_<Вид>.prefab (5 пней)
//   *_LOD0/1/2.asset  меши (рядом с префабами, чтобы не было Missing после перезагрузки)
//   Materials/        общие URP Lit с GPU Instancing (на вид: кора; на вариацию: цвет листвы). В рантайме копий не создаётся
//   TriReport.txt     таблица треугольников по LOD
//
// Структура префаба (как у дерева из TreeAndPlayerFix / TreeCollision): корень с тегом Resource, ResourceItem (useGeneratedModel = false,
// stumpPrefab = пень вида), ToonSway, TreeSpeciesInfo, триггер-капсула «зона выбора»; дети LOD0 / LOD1 / LOD2 (по одному мешу) и LODGroup.
// TreeHits добавит PlayerInteraction, ствол TrunkSolid и пятно земли добавит рантайм (TreeCollision.Setup, TreeGround).
//
// Размеры (молодое/зрелое/старое) в префабы НЕ запекаются: их даёт рантайм (TreeSizing + GameBalance.TreeScaleMul 0.7 / 1 / 1.3).
// Идемпотентность: повторный запуск перезаписывает те же файлы по тем же путям (GUID префабов, мешей и материалов сохраняется).
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using static EditorAssets;

public static class TreeSpeciesBuilder
{
    const string Dir = "Assets/_Project/Prefabs/Trees/Generated";
    const string MatDir = Dir + "/Materials";
    const string ShowcaseDir = "Assets/_Project/Scenes";
    const string ShowcasePath = ShowcaseDir + "/TreeShowcase.unity";

    // пороги LODGroup (доля высоты экрана): LOD0 → LOD1 → LOD2 → culled
    static readonly float[] LodScreen = { 0.30f, 0.10f, 0.025f };
    static readonly int[] Budget = { 1800, 500, 80 };

    sealed class Def
    {
        public TreeSpecies sp;
        public string file;      // начало имени префаба
        public string key;       // имя пня и материалов
        public string[] ids;     // буквы или номера вариаций
    }

    static readonly Def[] Defs =
    {
        new Def { sp = TreeSpecies.Cedar,  file = "Tree_Cedar",   key = "Cedar",       ids = new[] { "A", "B", "C", "D" } },
        new Def { sp = TreeSpecies.Birch,  file = "Tree_Birch",   key = "Birch",       ids = new[] { "A", "B", "C", "D" } },
        new Def { sp = TreeSpecies.Autumn, file = "Tree_Autumn",  key = "Autumn",      ids = new[] { "A", "B", "C" } },
        new Def { sp = TreeSpecies.Willow, file = "Willow_Dead",  key = "Willow_Dead", ids = new[] { "1", "2", "3" } },
        new Def { sp = TreeSpecies.Plain,  file = "Tree_Random",  key = "Random",      ids = new[] { "A", "B", "C", "D" } },
        new Def { sp = TreeSpecies.Oak,    file = "Tree_Oak",     key = "Oak",         ids = new[] { "A", "B", "C" } },
    };

    // материалы TreeToon по сабмешам (порядок = порядок сабмешей меша, см. шапку TreeSpeciesMeshes), берутся из библиотеки TreeToonSetup.
    // Имена важны для TreeSizing.IsFoliage: «bark» = не листва, «leaf» = листва
    //   Cedar: кора, хвоя, тёмный низ ярусов, светлые кончики лап   Birch: кора, листва, тёмные отметины, тёмный низ кроны
    //   Autumn: кора, жёлтая, жёлто-оранжевая, тёмный низ кроны     Plain: кора, листва, тёмный низ кроны
    //   Oak: кора, листва, листва (жёлтее), тёмный низ кроны        Willow: кора, ветки
    static string[] MatNames(TreeSpecies sp)
    {
        switch (sp)
        {
            case TreeSpecies.Cedar:  return new[] { "Tree_Bark", "Tree_Leaf_Dark", "Tree_Leaf_DarkUnder", "Tree_Leaf_DarkTip" };
            case TreeSpecies.Birch:  return new[] { "Tree_Bark_Birch", "Tree_Leaf_Birch", "Tree_BarkMark", "Tree_Leaf_BirchUnder" };
            case TreeSpecies.Autumn: return new[] { "Tree_Bark", "Tree_Leaf_Yellow", "Tree_Leaf_Orange", "Tree_Leaf_UnderYellow" };
            case TreeSpecies.Willow: return new[] { "Tree_Bark_Willow", "Tree_Twig_Willow" };
            case TreeSpecies.Oak:    return new[] { "Tree_Bark_Oak", "Tree_Leaf_Oak", "Tree_Leaf_OakAlt", "Tree_Leaf_OakUnder" };
            default:                 return new[] { "Tree_Bark", "Tree_Leaf_Green", "Tree_Leaf_Under" };
        }
    }

    // ------------------------------------------------------------ меню
    [MenuItem("Tools/Forest/Trees/Build Species")]
    public static void BuildAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Деревья: останови Play Mode перед сборкой видов.");
            return;
        }

        EnsureFolder(Dir);
        EnsureFolder(MatDir);

        var report = new StringBuilder(2048);
        report.AppendLine("Префаб".PadRight(26) + "LOD0".PadLeft(7) + "LOD1".PadLeft(7) + "LOD2".PadLeft(7) + "   Бюджет (1800 / 500 / 80)");
        report.AppendLine(new string('-', 70));
        bool over = false;
        int built = 0;
        var paths = new List<string>();   // по порядку: вид × вариация

        try
        {
            bool hasTag = System.Array.IndexOf(UnityEditorInternal.InternalEditorUtility.tags, "Resource") >= 0;
            if (!hasTag) Debug.LogWarning("Деревья: в проекте нет тега Resource: PlayerInteraction не найдёт деревья. Добавь тег и запусти сборку снова.");

            if (TreeToonSetup.Library("Tree_Bark") == null)
            {
                Debug.LogError("Деревья: нет шейдера TreeToon (Assets/_Project/Shaders/Forest/TreeToon.shader). Сборка остановлена.");
                return;
            }
            Material cutMat = TreeToonSetup.Library("Tree_Cut");
            int totalVars = 0, doneVars = 0;
            for (int q = 0; q < Defs.Length; q++) totalVars += Defs[q].ids.Length;

            for (int d = 0; d < Defs.Length; d++)
            {
                var def = Defs[d];
                GameObject stumpPrefab = null;

                for (int v = 0; v < def.ids.Length; v++)
                {
                    EditorUtility.DisplayProgressBar("Деревья", def.key + " " + def.ids[v], doneVars++ / (float)totalVars);

                    string id = def.ids[v];
                    int seed = v + 1;
                    string name = def.file + "_" + id + "_s" + seed;
                    TreeParams prm = TreeSpeciesMeshes.GetParams(def.sp, v);

                    // материалы
                    string[] mn = MatNames(def.sp);
                    var mats = new Material[mn.Length];
                    for (int i = 0; i < mn.Length; i++) mats[i] = TreeToonSetup.Library(mn[i]);

                    // пень вида: один на все вариации, строится вместе с первой
                    if (v == 0) stumpPrefab = BuildStump(def, prm, mats[0], cutMat);

                    // меши трёх LOD
                    var meshes = new Mesh[3];
                    var tris = new int[3];
                    for (int lod = 0; lod < 3; lod++)
                    {
                        Mesh m = TreeSpeciesMeshes.BuildTree(def.sp, v, seed, lod, name + "_LOD" + lod);
                        tris[lod] = TriCount(m);
                        meshes[lod] = SaveMesh(m, Dir + "/" + name + "_LOD" + lod + ".asset");
                    }

                    string prefabPath = Dir + "/" + name + ".prefab";
                    BuildTreePrefab(name, def, prm, meshes, mats, stumpPrefab, prefabPath, hasTag);
                    paths.Add(prefabPath);
                    built++;

                    bool ok = tris[0] <= Budget[0] && tris[1] <= Budget[1] && tris[2] <= Budget[2];
                    if (!ok) over = true;
                    report.AppendLine(name.PadRight(26) + tris[0].ToString().PadLeft(7) + tris[1].ToString().PadLeft(7) + tris[2].ToString().PadLeft(7) + (ok ? "   ок" : "   ПРЕВЫШЕН БЮДЖЕТ"));
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            File.WriteAllText(Dir + "/TriReport.txt", report.ToString(), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(Dir + "/TriReport.txt");

            BuildShowcase();
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        string text = report.ToString();
        if (over) Debug.LogError("Деревья: бюджет треугольников превышен.\n" + text);
        else Debug.Log("Деревья: собрано префабов " + built + ", бюджет соблюдён.\n" + text);
    }

    [MenuItem("Tools/Forest/Trees/Open TreeShowcase")]
    public static void OpenShowcase()
    {
        if (!File.Exists(ShowcasePath))
        {
            Debug.LogWarning("Деревья: сцена " + ShowcasePath + " ещё не создана. Сначала Tools → Forest → Trees → Build Species.");
            return;
        }
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene(ShowcasePath, OpenSceneMode.Single);
    }

    // ------------------------------------------------------------ префаб дерева
    static void BuildTreePrefab(string name, Def def, TreeParams prm, Mesh[] meshes, Material[] mats, GameObject stumpPrefab, string path, bool hasTag)
    {
        var root = new GameObject(name);
        try
        {
            if (hasTag) root.tag = "Resource";

            // дети LOD0..2: порядок важен, ResourceHighlighter берёт первый MeshFilter (контур) = LOD0
            var rends = new Renderer[3];
            for (int lod = 0; lod < 3; lod++)
            {
                var go = new GameObject("LOD" + lod);
                go.transform.SetParent(root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = meshes[lod];
                var mr = go.AddComponent<MeshRenderer>();
                int subs = Mathf.Max(1, meshes[lod].subMeshCount);
                var arr = new Material[subs];
                for (int i = 0; i < subs; i++) arr[i] = mats[Mathf.Min(i, mats.Length - 1)];
                mr.sharedMaterials = arr;
                mr.shadowCastingMode = lod == 2 ? ShadowCastingMode.Off : ShadowCastingMode.On;
                mr.receiveShadows = true;
                rends[lod] = mr;
            }

            var lg = root.AddComponent<LODGroup>();
            lg.fadeMode = LODFadeMode.None;
            var lods = new LOD[3];
            for (int i = 0; i < 3; i++) lods[i] = new LOD(LodScreen[i], new[] { rends[i] });
            lg.SetLODs(lods);
            lg.RecalculateBounds();

            // зона выбора: триггер на корне (TreeCollision.Setup подгонит её по TreeSpeciesInfo; ствол TrunkSolid добавит сам)
            var cap = root.AddComponent<CapsuleCollider>();
            cap.isTrigger = true;
            cap.direction = 1;
            cap.radius = prm.pickR;
            cap.height = Mathf.Max(prm.pickH, prm.pickR * 2f + 0.01f);
            cap.center = new Vector3(0f, prm.pickH * 0.5f - 0.1f, 0f);

            var info = root.AddComponent<TreeSpeciesInfo>();
            info.species = def.key;
            info.pickRadius = prm.pickR;
            info.pickHeight = prm.pickH;
            info.trunkRadius = prm.trunkR;
            info.trunkHeight = prm.trunkH;

            var ri = root.AddComponent<ResourceItem>();
            ri.useGeneratedModel = false;    // своя модель: TreeMeshFactory её не затрёт
            ri.stumpPrefab = stumpPrefab;    // свой пень вида (сгенерированный пень был бы обычного дерева)
            root.AddComponent<ToonSway>();

            PrefabUtility.SaveAsPrefabAsset(root, path);   // тот же путь = тот же GUID
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // ------------------------------------------------------------ пень вида
    static GameObject BuildStump(Def def, TreeParams prm, Material bark, Material cut)
    {
        Mesh m = TreeSpeciesMeshes.BuildStump(prm.stumpR, "Stump_" + def.key);
        m = SaveMesh(m, Dir + "/Stump_" + def.key + ".asset");

        var go = new GameObject("Stump_" + def.key);
        try
        {
            go.AddComponent<MeshFilter>().sharedMesh = m;
            go.AddComponent<MeshRenderer>().sharedMaterials = new[] { bark, cut };

            // пень сплошной (как TreeCollision.AddStumpCollider): сквозь него не пройти
            var cap = go.AddComponent<CapsuleCollider>();
            cap.direction = 1;
            cap.radius = prm.stumpR;
            cap.height = Mathf.Max(0.5f, prm.stumpR * 2f);
            cap.center = new Vector3(0f, -0.02f, 0f);
            SetSlip(cap);

            string path = Dir + "/Stump_" + def.key + ".prefab";
            return PrefabUtility.SaveAsPrefabAsset(go, path);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    // скользкий материал ствола и пня: игрок огибает, а не залипает (как TreeCollision.SlipMat в рантайме)
    static void SetSlip(Collider c)
    {
        string path = MatDir + "/TreeTrunk_Slip.asset";
#if UNITY_6000_0_OR_NEWER
        var m = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
        if (m == null)
        {
            m = new PhysicsMaterial("TreeTrunk_Slip");
            m.frictionCombine = PhysicsMaterialCombine.Minimum;
            m.dynamicFriction = 0f; m.staticFriction = 0f; m.bounciness = 0f;
            AssetDatabase.CreateAsset(m, path);
        }
#else
        var m = AssetDatabase.LoadAssetAtPath<PhysicMaterial>(path);
        if (m == null)
        {
            m = new PhysicMaterial("TreeTrunk_Slip");
            m.frictionCombine = PhysicMaterialCombine.Minimum;
            m.dynamicFriction = 0f; m.staticFriction = 0f; m.bounciness = 0f;
            AssetDatabase.CreateAsset(m, path);
        }
#endif
        c.sharedMaterial = m;
    }

    // ------------------------------------------------------------ сцена-превью
    // Ряд на вид (по Z): эталон Tree_Random (меш из TreeMeshFactory, зрелое дерево) с мачтой 4.3 м, правее 3 вариации × 3 размера
    // (масштаб = GameBalance.TreeScaleMul, как в игре). Камера под углом сверху: подгони под камеру игры, если нужно.
    static void BuildShowcase()
    {
        EnsureFolder(ShowcaseDir);
        Scene prev = SceneManager.GetActiveScene();
        Scene sc = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(sc);
        try
        {
            const float colStep = 6f, rowStep = 9f, x0 = 8f;

            // свет и воздух
            var sun = new GameObject("Sun");
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.62f);

            // земля
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = new Vector3(28f, 0f, 18f);
            ground.transform.localScale = new Vector3(9f, 1f, 6.5f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = GetMat("TreeShowcase_Ground", new Color(0.40f, 0.58f, 0.28f));

            // эталон: меш зрелого Tree_Random из TreeMeshFactory, копия сохранена как ассет
            Mesh refMesh = Object.Instantiate(TreeMeshFactory.TreeMesh(TreeSize.Mature));
            refMesh.name = "Tree_Random_Reference";
            refMesh = SaveMesh(refMesh, Dir + "/Tree_Random_Reference.asset");
            Material refBark = GetMat("Tree_Random_Bark", new Color(0.42f, 0.28f, 0.16f));
            Material refLeaf = GetMat("Tree_Random_Leaf_A", new Color(0.30f, 0.68f, 0.24f));
            Material marker = GetMat("TreeShowcase_Marker", new Color(0.9f, 0.15f, 0.15f));

            for (int d = 0; d < Defs.Length; d++)
            {
                var row = new GameObject("Row_" + Defs[d].key);
                row.transform.position = new Vector3(0f, 0f, d * rowStep);

                var refGo = new GameObject("Reference_Tree_Random");
                refGo.transform.SetParent(row.transform, false);
                refGo.AddComponent<MeshFilter>().sharedMesh = refMesh;
                refGo.AddComponent<MeshRenderer>().sharedMaterials = new[] { refBark, refLeaf };

                var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pole.name = "HeightMarker_4.3m";
                Object.DestroyImmediate(pole.GetComponent<Collider>());
                pole.transform.SetParent(row.transform, false);
                pole.transform.localPosition = new Vector3(-2.2f, 2.15f, 0f);
                pole.transform.localScale = new Vector3(0.06f, 2.15f, 0.06f);
                pole.GetComponent<MeshRenderer>().sharedMaterial = marker;

                string[] sizeNames = { "Young", "Mature", "Old" };
                for (int v = 0; v < Defs[d].ids.Length; v++)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Dir + "/" + Defs[d].file + "_" + Defs[d].ids[v] + "_s" + (v + 1) + ".prefab");
                    if (prefab == null) continue;
                    for (int s = 0; s < 3; s++)
                    {
                        var inst = PrefabUtility.InstantiatePrefab(prefab, sc) as GameObject;
                        if (inst == null) continue;
                        inst.name = Defs[d].key + "_" + Defs[d].ids[v] + "_" + sizeNames[s];
                        inst.transform.SetParent(row.transform, false);
                        inst.transform.localPosition = new Vector3(x0 + (v * 3 + s) * colStep, 0f, 0f);
                        inst.transform.localRotation = Quaternion.Euler(0f, v * 40f + s * 70f, 0f);
                        inst.transform.localScale = Vector3.one * GameBalance.TreeScaleMul[s];
                    }
                }
            }

            // камера
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 40f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.62f, 0.78f, 0.90f);
            camGo.AddComponent<AudioListener>();
            Quaternion rot = Quaternion.Euler(40f, 0f, 0f);
            Vector3 center = new Vector3(28f, 2f, 18f);
            camGo.transform.rotation = rot;
            camGo.transform.position = center - rot * Vector3.forward * 72f;

            EditorSceneManager.SaveScene(sc, ShowcasePath);
        }
        finally
        {
            if (prev.IsValid() && prev != sc) SceneManager.SetActiveScene(prev);
            EditorSceneManager.CloseScene(sc, true);
        }
    }

    // ------------------------------------------------------------ ассеты
    static int TriCount(Mesh m)
    {
        int n = 0;
        for (int i = 0; i < m.subMeshCount; i++) n += (int)m.GetIndexCount(i) / 3;
        return n;
    }

    // меш: если ассет уже есть, его содержимое заменяется на месте (GUID и ссылки префабов живы)
    static Mesh SaveMesh(Mesh m, string path)
    {
        var ex = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (ex != null)
        {
            ex.Clear();
            EditorUtility.CopySerialized(m, ex);
            Object.DestroyImmediate(m);
            EditorUtility.SetDirty(ex);
            return ex;
        }
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    // материал: URP Lit, GPU Instancing, матовый. Существующий обновляется на месте
    static Material GetMat(string name, Color c)
    {
        string path = MatDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null)
            {
                Debug.LogError("Деревья: шейдер Universal Render Pipeline/Lit не найден (нет URP?). Материал " + name + " создан на Standard.");
                sh = Shader.Find("Standard");
            }
            m = new Material(sh) { name = name };
            AssetDatabase.CreateAsset(m, path);
        }
        m.enableInstancing = true;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.05f);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.05f);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(m);
        return m;
    }

}
#endif
