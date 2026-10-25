// Assets/_Project/Editor/IslandV2/IslandBlockout.cs
// Остров v2, этап 1: сборка серого блокаута в сцене. Меню: Tools/BuilderSim/Локация/Остров v2/2. Собрать блокаут (приоритет 121).
// Части этапа 1:
//   IslandLayout.cs          ассет со всеми числами
//   IslandField.cs           математика поля (контур, высота, вода, тропы)
//   IslandBlockout.cs        (этот файл) меню, корень IslandV2, порядок сборки, предупреждения про старую локацию
//   IslandBlockout.GroundTex.cs запекание цвета земли в текстуру (G)
//   IslandBlockout.Ground.cs невидимый Terrain-коллайдер и видимые меш-чанки с цветом вершин (BuildGround, BuildTerrain, BuildChunks)
//   IslandBlockout.Props.cs  здания-коробки BLK_*, лес-заглушки, маркеры (BuildBuildings, BuildTrees, BuildMarkers), мелкий сборщик мешей
//   IslandCheck.cs           проверка контракта и проходимости
//   IslandCliff.cs           обрыв и подбрюшье (этап 4, D: вырез под реку)
//   IslandRiverWater.cs      вода реки, водопад и объём воды для плавания (этап 6, E)
//   IslandBlockout.Plaza.cs  площадка лагеря у костра: мощение, бордюр, флагшток, фонари с гирляндой, цветы
//   IslandBlockout.Bridge.cs мост через реку на броде: настил, опоры, перила, фонари и гирлянда (FireSource)
//
// Структура в сцене (контракт):
//   IslandV2                 корень, в нуле
//     _Generated             ПЕРЕСОБИРАЕТСЯ ЦЕЛИКОМ каждый раз: Ground_Terrain, Ground_Chunk_*, BLK_*, BLK_Trees, BLK_TreeColliders
//     _Manual                генератор не трогает: сюда кладёшь руками всё своё
//     Markers                метки (CAMPFIRE_Point, SPAWN_Player, PAD_*, ForestGate, DEFERRED_Bridge): создаются, если нет; если есть, только переставляются по раскладке
// Что делает сборка: удаляет старый _Generated, строит новый, расставляет метки. Одна группа отмены (Ctrl+Z откатывает всё).
// Что НЕ делает: не отключает старую локацию (только предупреждает в Console), не трогает UI, сохранения, StoryFlow, мост (только метка DEFERRED_Bridge).
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IslandV2
{
    public static partial class IslandBlockout
    {
        public const string RootName = "IslandV2";
        public const string GeneratedName = "_Generated";
        public const string ManualName = "_Manual";
        public const string MarkersName = "Markers";
        public const string ShaderName = "Project/IslandV2/VertexColor";
        public const string MaterialPath = IslandLayout.AssetFolder + "/Mat_IslandV2.mat";
        public const string TerrainDataPath = IslandLayout.AssetFolder + "/Generated/IslandV2_Terrain.asset";

        [MenuItem("Tools/BuilderSim/Локация/Остров v2/2. Собрать блокаут", false, 121)]
        static void MenuBuild()
        {
            Build(IslandLayout.GetOrCreate());
        }

        public static GameObject FindRoot()
        {
            var go = GameObject.Find(RootName);
            return go;
        }

        public static void Build(IslandLayout layout)
        {
            if (Application.isPlaying) { Debug.LogError("IslandV2: блокаут собирается вне Play Mode."); return; }
            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError("IslandV2: не найден шейдер " + ShaderName + " (файл Shaders/IslandV2/IslandVertexColor.shader). Положи его в проект и дождись компиляции.");
                return;
            }
            string fixNote;
            if (layout.EnforceContract(out fixNote))
            {
                EditorUtility.SetDirty(layout); AssetDatabase.SaveAssets();
                Debug.LogWarning("IslandV2: раскладка нарушала контракт, исправлено. " + fixNote);
            }
            var field = new IslandField(layout);
            if (field.Contour.Count < 3) { Debug.LogError("IslandV2: контур пустой, проверь раскладку."); return; }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Остров v2: собрать блокаут");
            int group = Undo.GetCurrentGroup();

            Material mat = GetMaterial(shader);

            // корень и дети
            var rootGo = FindRoot();
            if (rootGo == null)
            {
                rootGo = new GameObject(RootName);
                Undo.RegisterCreatedObjectUndo(rootGo, "Create IslandV2");
            }
            Transform root = rootGo.transform;
            root.position = Vector3.zero; root.rotation = Quaternion.identity; root.localScale = Vector3.one;

            // данные острова для рантайма (контур, GroundY, seed): их читают IslandLook, CloudSea, IslandAbyss, IslandEdgeGuard, LightingRig, миникарта
            var data = rootGo.GetComponent<IslandV2Data>();
            if (data == null) data = Undo.AddComponent<IslandV2Data>(rootGo);
            data.SetOutline(field.Contour, 0f, layout.seed);
            EditorUtility.SetDirty(data);

            var old = root.Find(GeneratedName);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var gen = new GameObject(GeneratedName).transform;
            Undo.RegisterCreatedObjectUndo(gen.gameObject, "Create _Generated");
            gen.SetParent(root, false);

            if (root.Find(ManualName) == null)
            {
                var man = new GameObject(ManualName);
                Undo.RegisterCreatedObjectUndo(man, "Create _Manual");
                man.transform.SetParent(root, false);
            }
            var markers = root.Find(MarkersName);
            if (markers == null)
            {
                var mk = new GameObject(MarkersName);
                Undo.RegisterCreatedObjectUndo(mk, "Create Markers");
                mk.transform.SetParent(root, false);
                markers = mk.transform;
            }

            int chunks, groundTris;
            BakeGroundTexture(field, mat);   // этап 2/G: цвет земли в текстуру 0.5 м на пиксель, шейдер читает её по мировым XZ
            BuildGround(field, gen, mat, out chunks, out groundTris);
            int propTris = BuildBuildings(field, gen, mat);
            int trees;
            int treeTris = BuildTrees(field, gen, mat, out trees);
            BuildMarkers(field, markers);
            int cliffTris = 0;
            if (layout.buildCliff)
            {
                Material rockMat;
                cliffTris = IslandCliff.Build(field, gen, data, out rockMat);
            }
            int waterTris = 0;
            if (layout.buildRiverWater) waterTris = IslandRiverWater.Build(field, gen, data);   // E: вода, водопад, объём воды (после обрыва: нужна его глубина)
            int bridgeTris = BuildRiverBridge(field, gen, mat);   // мост через реку с фонарями (IslandBlockout.Bridge.cs), после воды: нужен RiverBedY для свай
            int plazaTris = BuildCampPlaza(field, gen, mat);      // площадка лагеря у костра: мощение, бордюр, флаг, фонари (IslandBlockout.Plaza.cs)

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(rootGo.scene);
            Selection.activeGameObject = rootGo;

            Debug.Log("IslandV2: блокаут собран. Площадь " + field.Area.ToString("0") + " м² (цель " + layout.targetArea.ToString("0") + "), чанков земли " + chunks
+ ", Tris: земля " + groundTris + ", здания " + propTris + ", лес " + treeTris + " (деревьев " + trees + "), обрыв " + cliffTris + ", вода " + waterTris + ", мост " + bridgeTris + ", площадка " + plazaTris + ". Дальше: пункт «3. Проверить блокаут»."); WarnAboutOldLocation(root);
            if (rootGo.scene.name != "Island_v2")
                Debug.LogWarning("IslandV2: открыта сцена «" + rootGo.scene.name + "», а не Island_v2. Если это не нарочно, открой Island_v2 и собери блокаут там.");
        }

        // общий материал блокаута: один на землю и заглушки
        static Material GetMaterial(Shader shader)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (m == null)
            {
                IslandLayout.EnsureFolder(IslandLayout.AssetFolder);
                m = new Material(shader) { name = "Mat_IslandV2" };
                AssetDatabase.CreateAsset(m, MaterialPath);
            }
            else if (m.shader != shader) m.shader = shader;
            m.enableInstancing = false;   // SRP Batcher сам батчит; instancing для чанков не нужен
            EditorUtility.SetDirty(m);
            return m;
        }

        // старая локация (Terrain, второй CAMPFIRE_Point вне IslandV2): без отключения будут дубли и два коллайдера земли
        public static List<string> FindForeign(Transform root)
        {
            var res = new List<string>();
            foreach (var t in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.gameObject.activeInHierarchy && !t.transform.IsChildOf(root)) res.Add("Terrain «" + t.name + "»");
            foreach (var tr in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (tr.name != "CAMPFIRE_Point" || tr.IsChildOf(root)) continue;
                if (tr.gameObject.activeInHierarchy) res.Add("CAMPFIRE_Point вне IslandV2 (путь: " + PathOf(tr) + ")");
            }
            foreach (var fi in Object.FindObjectsByType<FloatingIsland>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (fi.gameObject.activeInHierarchy && !fi.transform.IsChildOf(root)) res.Add("FloatingIsland «" + fi.name + "»");
            return res;
        }

        static string PathOf(Transform t)
        {
            string p = t.name;
            while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
            return p;
        }

        static void WarnAboutOldLocation(Transform root)
        {
            var foreign = FindForeign(root);
            if (foreign.Count == 0) return;
            Debug.LogWarning("IslandV2: в сцене остались объекты старой локации, отключи их (иначе дубли меток и второй коллайдер земли): " + string.Join("; ", foreign) + ".");
        }
    }
}
