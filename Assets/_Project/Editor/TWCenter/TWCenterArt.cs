using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IslandV2
{
    public static class TWCenterArt
    {
        internal const int Revision = 2;
        internal const string RootName = "TW_CENTER_ART";
        const string Menu = "Tools/BuilderSim/Локация/Остров v2/CENTER ART/";
        const string AssetFolder = "Assets/_Project/IslandV2/CenterArt";

        [MenuItem(Menu + "1. Audit — центр и существующий костёр", false, 191)]
        public static void Audit()
        {
            try
            {
                var x = Inspect(false);
                Debug.Log("CENTER ART: костёр " + x.fire.name + ", центр " + x.centre + ", радиус поляны " + x.radius.ToString("F1") +
                    " м. Старых слоёв окружения: " + x.oldDecor.Count + ". Стадии костра, зона стройки и точки подхода зарезервированы.");
            }
            catch (Exception e) { Debug.LogError("CENTER ART: " + e.Message); }
        }

        [MenuItem(Menu + "2. Собрать лесную стоянку — костёр не менять", false, 192)]
        public static void Apply() { ApplyCore(true); }

        static bool ApplyCore(bool requireSaved)
        {
            TWCenterContext x;
            try { x = Inspect(requireSaved); }
            catch (Exception e) { Debug.LogError("CENTER ART: " + e.Message); return false; }
            int group = -1; string folder = null;
            try
            {
                string before = TWCenterValidation.FireHash(x.fire);
                string backup = Backup(x.island.gameObject.scene);
                Undo.IncrementCurrentGroup(); group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("TimberWings — woodland centre scenery");
                string sceneID = AssetDatabase.AssetPathToGUID(x.island.gameObject.scene.path);
                folder = AssetFolder + "/Scenes/" + sceneID + "/Build_" + Guid.NewGuid().ToString("N").Substring(0, 12);
                IslandLayout.EnsureFolder(folder);
                var root = new GameObject(RootName + "__building");
                Undo.RegisterCreatedObjectUndo(root, "Create centre surroundings"); root.transform.SetParent(x.manual, false); x.newRoot = root.transform;
                var state = root.AddComponent<CenterArtState>(); state.revision = Revision; state.generatedFolder = folder;
                state.campfire = x.fire; state.campfireHash = before; state.marker = x.marker; state.markerPosition = x.marker.position;
                EditorUtility.DisplayProgressBar("TimberWings", "Лавочки, дровник и фонари вокруг существующего здания", 0.18f);
                var assets = new TWCenterAssets(folder);
                TWCenterProps.Build(x, assets, root.transform, state);
                EditorUtility.DisplayProgressBar("TimberWings", "Земляная поляна на поверхности существующего рельефа", 0.65f);
                TWCenterGround.Build(x, assets, root.transform);
                foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true)) TWCenterValidation.Mesh(mf.sharedMesh);
                if (TWCenterValidation.FireHash(x.fire) != before) throw new InvalidOperationException("Костёр изменился во время сборки. Окружение отменено.");
                // Only these exact, separate environment roots are disabled.
                // Campfire and its entire hierarchy are never recorded/edited.
                var hidden = x.previous != null ? x.previous.hidden.Where(h => h.target != null).ToList() : new System.Collections.Generic.List<CenterArtState.HiddenObject>();
                foreach (var old in x.oldDecor)
                    if (!hidden.Any(h => h.target == old)) hidden.Add(new CenterArtState.HiddenObject { target = old, active = old.activeSelf });
                state.hidden = hidden.ToArray();
                foreach (var item in state.hidden)
                    if (item.target != null) { Undo.RecordObject(item.target, "Hide original plaza scenery"); item.target.SetActive(false); }
                if (x.previous != null) Undo.DestroyObjectImmediate(x.previous.gameObject);
                root.name = RootName;
                Physics.SyncTransforms();
                if (TWCenterValidation.FireHash(x.fire) != before) throw new InvalidOperationException("Существующий костёр был затронут. Сборка отменена.");
                EditorUtility.SetDirty(state); AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(x.island.gameObject.scene); Undo.CollapseUndoOperations(group);
                Selection.activeGameObject = root;
                Debug.Log("CENTER ART: готово. Лесная стоянка, лавочек " + state.benches + ", фонарей " + state.lanterns +
                    ". Проверка снимка костра: без изменений. Ctrl+S, затем пункт 4 QA. Резервная копия сцены: " + backup);
                return true;
            }
            catch (Exception e)
            {
                if (group >= 0) Undo.RevertAllDownToGroup(group);
                if (!string.IsNullOrEmpty(folder) && AssetDatabase.IsValidFolder(folder)) AssetDatabase.DeleteAsset(folder);
                Debug.LogError("CENTER ART: сборка отменена, прежнее окружение восстановлено. " + e);
                return false;
            }
            finally { EditorUtility.ClearProgressBar(); }
        }

        [MenuItem(Menu + "3. Откатить только окружение центра", false, 193)]
        public static void Rollback()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("Сначала останови Play Mode."); return; }
            var scene = SceneManager.GetActiveScene();
            var states = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<CenterArtState>(true)).ToArray();
            if (states.Length != 1) { Debug.LogError("CENTER ART: нужен один установленный комплект окружения."); return; }
            var state = states[0];
            string before = state.campfire != null ? TWCenterValidation.FireHash(state.campfire) : null;
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Restore original centre surroundings");
            foreach (var item in state.hidden)
                if (item.target != null) { Undo.RecordObject(item.target, "Restore plaza scenery"); item.target.SetActive(item.active); }
            Transform fire = state.campfire; Undo.DestroyObjectImmediate(state.gameObject);
            Undo.CollapseUndoOperations(group); EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("CENTER ART: прежнее окружение возвращено. Костёр " + (fire != null && TWCenterValidation.FireHash(fire) == before ? "без изменений." : "нужно проверить.") + " Ctrl+S.");
        }

        [MenuItem(Menu + "4. QA — окружение, проходы, неизменность костра", false, 194)]
        public static void QA()
        {
            try
            {
                var x = Inspect(false);
                if (x.previous == null) throw new InvalidOperationException("Сначала выполни пункт 2.");
                TWCenterValidation.Installed(x, x.previous);
                if (x.hearth != null) TWHearthBuilder.Check(x.hearth);
                Debug.Log("CENTER ART QA: PASS. Снимок костра, его настройки и иерархия совпадают. Новое окружение, материалы, коллайдеры и подходы проверены. " +
                    "В Play Mode проверь все стадии стройки, управление временем, проходы и день/ночь.");
            }
            catch (Exception e) { Debug.LogError("CENTER ART QA: " + e.Message); }
        }

        [MenuItem(Menu + "5. Показать центр", false, 195)]
        static void Frame()
        {
            try
            {
                var x = Inspect(false);
                SceneView.lastActiveSceneView?.LookAt(new Vector3(x.centre.x, x.GroundY(x.centre) + 0.6f, x.centre.y), Quaternion.Euler(38f, 0f, 0f), 17f);
            }
            catch (Exception e) { Debug.LogError(e.Message); }
        }

        [MenuItem(Menu + "6. Обновить центр и костёр — новая композиция", false, 196)]
        public static void PolishAll()
        {
            TWCenterContext x;
            try { x = Inspect(true); }
            catch (Exception e) { Debug.LogError("CENTER ART: " + e.Message); return; }
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("TimberWings — complete centre polish");
            if (!TWHearthBuilder.ApplyExisting(x.fire.gameObject, true) || !ApplyCore(false))
            {
                Undo.RevertAllDownToGroup(group);
                Debug.LogError("CENTER ART: общая операция отменена, прежнее состояние возвращено."); return;
            }
            Undo.CollapseUndoOperations(group);
            Debug.Log("CENTER ART v2: центр и три визуальные стадии костра обновлены. Строительство, цены и управление временем сохранены. Ctrl+S, затем пункт 4 QA.");
        }

        static TWCenterContext Inspect(bool saved)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) throw new InvalidOperationException("Останови Play Mode и дождись компиляции.");
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path)) throw new InvalidOperationException("Открой сохранённую игровую сцену острова.");
            if (saved && scene.isDirty) throw new InvalidOperationException("Сначала сохрани сцену Ctrl+S.");
            var roots = scene.GetRootGameObjects();
            var island = roots.SingleOrDefault(g => g.name == IslandBlockout.RootName);
            if (island == null) throw new InvalidOperationException("Нет корня IslandV2.");
            var x = new TWCenterContext { island = island.transform };
            x.generated = x.island.Find(IslandBlockout.GeneratedName); x.manual = x.island.Find(IslandBlockout.ManualName);
            x.marker = x.island.Find(IslandBlockout.MarkersName + "/CAMPFIRE_Point");
            if (x.generated == null || x.manual == null || x.marker == null) throw new InvalidOperationException("Не найдены _Generated, _Manual или Markers/CAMPFIRE_Point.");
            if (x.manual.position.sqrMagnitude > 0.00001f || Quaternion.Angle(x.manual.rotation, Quaternion.identity) > 0.01f || Vector3.Distance(x.manual.lossyScale, Vector3.one) > 0.001f)
                throw new InvalidOperationException("_Manual должен иметь исходный мировой Transform: позиция 0, поворот 0, масштаб 1.");
            var fire = roots.SelectMany(g => g.GetComponentsInChildren<HutStages>(true)).Where(h => h.name == "Campfire").ToArray();
            if (fire.Length != 1) throw new InvalidOperationException("В сцене должен быть один существующий Campfire с HutStages. Его генератор запускать не нужно.");
            x.fire = fire[0].transform;
            x.hearth = x.manual.GetComponentsInChildren<HearthArtState>(true).SingleOrDefault(h => h.campfire == x.fire);
            if (Vector2.Distance(new Vector2(x.fire.position.x, x.fire.position.z), new Vector2(x.marker.position.x, x.marker.position.z)) > 0.5f)
                throw new InvalidOperationException("Костёр не совпадает с CAMPFIRE_Point. Открой актуальную сцену; инструмент не переставляет здание.");
            if (x.fire.GetComponentsInChildren<BuildingController>(true).Length == 0) throw new InvalidOperationException("У костра не найдена существующая зона строительства BuildingController.");
            var layout = AssetDatabase.LoadAssetAtPath<IslandLayout>(IslandLayout.AssetPath);
            if (layout == null) throw new InvalidOperationException("Не загружен IslandLayout.");
            x.field = new IslandField(layout); x.centre = new Vector2(x.fire.position.x, x.fire.position.z);
            if (Vector2.Distance(x.field.PlazaCenter, x.centre) > 0.5f) throw new InvalidOperationException("IslandLayout не совпадает с центром сохранённой сцены.");
            if (layout.plazaRadius < 2f || layout.plazaRadius > 7.5f) throw new InvalidOperationException("Неожиданный размер старой площадки. Этот комплект рассчитан на радиус 2–7,5 м.");
            x.radius = Mathf.Max(6.3f, layout.plazaRadius + 1.1f);
            var states = x.manual.GetComponentsInChildren<CenterArtState>(true);
            if (states.Length > 1) throw new InvalidOperationException("Найдены дубли CenterArtState.");
            x.previous = states.FirstOrDefault();
            foreach (string name in new[] { "CampPlaza", "CampPlazaCandy" })
            {
                Transform old = x.generated.Find(name);
                if (old != null)
                {
                    if (x.fire.IsChildOf(old) || old.IsChildOf(x.fire)) throw new InvalidOperationException("Старое окружение вложено в здание костра. Автоматическая замена остановлена.");
                    if (old.GetComponentsInChildren<BuildingController>(true).Length > 0) throw new InvalidOperationException("Внутри старой площадки есть игровая постройка. Её нельзя отключать вместе с окружением.");
                    x.oldDecor.Add(old.gameObject);
                }
            }
            if (x.oldDecor.Count == 0 && x.previous == null) throw new InvalidOperationException("Не найдена исходная CampPlaza. Проверь игровую сцену.");
            if (Shader.Find("Universal Render Pipeline/Simple Lit") == null) throw new InvalidOperationException("Не импортирован URP Simple Lit.");
            var ground = AssetDatabase.LoadAssetAtPath<Material>(IslandBlockout.MaterialPath);
            if (ground == null || !ground.HasProperty("_GroundTex") || ground.GetTexture("_GroundTex") == null)
                throw new InvalidOperationException("Не загружен исходный материал земли.");
            x.ReadGround(); x.ReserveBuilding(); return x;
        }

        internal static string Backup(Scene scene)
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            string directory = Path.Combine(project, "UserSettings/TW_CenterBackups", AssetDatabase.AssetPathToGUID(scene.path));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + ".unity");
            File.Copy(Path.Combine(project, scene.path), path, false); return path;
        }
    }
}
