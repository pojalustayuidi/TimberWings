using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IslandV2
{
    public static class TWGroveArt
    {
        const string Menu = "Tools/BuilderSim/Лес/SOUTH GROVE ART/";
        internal const string RootName = "TW_GROVE_SOUTH";
        const string AssetFolder = "Assets/_Project/IslandV2/GroveArt";

        [MenuItem(Menu + "1. Проверить южную рощу", false, 270)]
        public static void Audit()
        {
            try
            {
                var x = Inspect();
                Debug.Log("GROVE ART: PAD_GroveSouth, деревьев для рубки " + x.resources.Length + ", старых слоёв декора " + x.old.Count +
                    ". Деревья, зоны карты, цены и стадии строек сохраняются. Арка и реквизит будут отдельными объектами.");
            }
            catch (Exception e) { Debug.LogError("GROVE ART: " + e.Message); }
        }

        [MenuItem(Menu + "2. Переделать поляну — отдельные объекты", false, 271)]
        public static void Apply() { ApplyCore(false); }

        [MenuItem(Menu + "6. Добавить детали — сохранить всю расстановку", false, 275)]
        public static void DetailExisting() { ApplyCore(true); }

        static void ApplyCore(bool preserveAll)
        {
            TWGroveContext x;
            try { x = Inspect(); }
            catch (Exception e) { Debug.LogError("GROVE ART: " + e.Message); return; }
            int group = -1; string folder = null;
            try
            {
                Scene scene = x.marker.gameObject.scene;
                if (scene.isDirty && !EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Не удалось сохранить исходную сцену для резервной копии.");
                string backup = Backup(scene); string before = TWGroveValidation.ProtectedHash(scene);
                KeepEditedObjects(x, preserveAll);
                Transform oldRoot = x.previous != null ? x.previous.artRoot : null;
                Undo.IncrementCurrentGroup(); group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("TimberWings — южная лесная делянка");
                folder = AssetFolder + "/Scenes/" + AssetDatabase.AssetPathToGUID(scene.path) + "/Build_" + Guid.NewGuid().ToString("N").Substring(0, 12);
                IslandLayout.EnsureFolder(folder);
                var root = new GameObject(RootName + "__building"); Undo.RegisterCreatedObjectUndo(root, "Create authored grove");
                root.transform.SetParent(x.manual, false); root.transform.SetPositionAndRotation(x.origin, x.rotation); x.root = root.transform;
                var assets = new TWGroveAssets(folder);
                EditorUtility.DisplayProgressBar("TimberWings", "Арка, рабочие уголки и сюжетные детали", 0.15f);
                GameObject gate = TWGroveProps.Gate(x, assets);
                TWGroveProps.Build(x, assets);
                ReadPlacedFootprints(x);
                EditorUtility.DisplayProgressBar("TimberWings", "Подходы к деревьям и тропы на существующей земле", 0.5f);
                TWGrovePaths.Build(x, gate.transform);
                TWGroveDetails.Build(x, assets);
                foreach (var h in x.keep.Values)
                    if (h != null && h.transform.parent != x.root) Undo.SetTransformParent(h.transform,x.root,"Keep existing grove object");
                foreach (var pair in x.keep)
                {
                    if (pair.Value == null) throw new InvalidOperationException("Потерян сохранённый объект: "+pair.Key);
                    Matrix4x4 beforePose=x.keptMatrices[pair.Key], afterPose=pair.Value.transform.localToWorldMatrix;
                    for(int i=0;i<16;i++)
                        if(Mathf.Abs(beforePose[i]-afterPose[i])>0.002f) throw new InvalidOperationException("Изменилась ручная расстановка: "+pair.Key);
                }
                TWGroveGround.Build(x, assets);
                Atmosphere(x);
                TWGroveValidation.Geometry(root.transform);
                if (TWGroveValidation.ProtectedHash(scene) != before) throw new InvalidOperationException("Изменились деревья или игровая логика. Операция отменена.");

                GroveArtState state = x.previous != null ? x.previous : Undo.AddComponent<GroveArtState>(x.marker.gameObject);
                Undo.RecordObject(state, "Authored grove ownership");
                var hidden = state.hidden.Where(h => h.target != null).ToList();
                foreach (var old in x.old)
                    if (!hidden.Any(h => h.target == old)) hidden.Add(new GroveArtState.HiddenObject { target = old, active = old.activeSelf });
                state.hidden = hidden.ToArray(); state.artRoot = root.transform; state.generatedFolder = folder; state.protectedHash = before; state.resourceTrees = x.resources.Length; state.revision = 2;
                foreach (var item in state.hidden)
                    if (item.target != null) { Undo.RecordObject(item.target, "Retire legacy grove scenery"); item.target.SetActive(false); }
                var builder = x.marker.GetComponent<ChopGroveBuilder>();
                if (builder != null)
                {
                    if (!state.hadBuilder) { state.hadBuilder = true; state.builderJson = EditorJsonUtility.ToJson(builder); }
                    Undo.DestroyObjectImmediate(builder);
                }
                if (oldRoot != null) Undo.DestroyObjectImmediate(oldRoot.gameObject);
                root.name = RootName;
                if (TWGroveValidation.ProtectedHash(scene) != before) throw new InvalidOperationException("Затронуты деревья, зоны или стройки. Операция отменена.");
                x.previous = state; TWGroveValidation.Installed(x);
                EditorUtility.SetDirty(state); AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); Undo.CollapseUndoOperations(group);
                Selection.activeGameObject = root;
                Debug.Log("GROVE ART v2: готово. Знаки рощи, рабочие детали и растительность в IslandV2/_Manual/" + RootName +
                    ". Старый генератор убран с метки, Play Mode не перестраивает поляну. Ручных объектов сохранено: " + x.keep.Count +
                    ". Ctrl+S; пункт 4 — проверка. Копия сцены: " + backup);
            }
            catch (Exception e)
            {
                if (group >= 0) Undo.RevertAllDownToGroup(group);
                if (folder != null && AssetDatabase.IsValidFolder(folder)) AssetDatabase.DeleteAsset(folder);
                Debug.LogError("GROVE ART: сборка отменена, исходное состояние восстановлено. " + e);
            }
            finally { EditorUtility.ClearProgressBar(); }
        }

        static void KeepEditedObjects(TWGroveContext x, bool preserveAll)
        {
            var previous = x.previous;
            if (previous == null || previous.artRoot == null) return;
            foreach (var h in previous.artRoot.GetComponentsInChildren<GroveArtHandle>(true))
            {
                Transform t = h.transform;
                if (preserveAll || Vector3.Distance(t.position, h.defaultPosition) > 0.001f || Quaternion.Angle(t.rotation, h.defaultRotation) > 0.05f ||
                    Vector3.Distance(t.lossyScale, h.defaultScale) > 0.001f || !h.gameObject.activeSelf)
                {
                    x.keep[h.key] = h;
                    x.keptMatrices[h.key] = t.localToWorldMatrix;
                }
            }
        }

        static void ReadPlacedFootprints(TWGroveContext x)
        {
            x.occupied.Clear(); x.earthPatches.Clear();
            foreach (var h in x.root.GetComponentsInChildren<GroveArtHandle>(true))
            {
                if (!h.gameObject.activeSelf) continue;
                Vector2 p = x.Local(h.transform.position); Vector3 scale = h.transform.lossyScale;
                float yaw = (Quaternion.Inverse(x.rotation) * h.transform.rotation).eulerAngles.y;
                Vector2 half = Vector2.Scale(h.halfSize, new Vector2(Mathf.Abs(scale.x), Mathf.Abs(scale.z)));
                if (h.key == "Gate")
                {
                    foreach (int s in new[] { -1, 1 })
                        x.occupied.Add(new TWGroveContext.Footprint { point = x.Local(h.transform.TransformPoint(new Vector3(s * 2.45f, 0, 0))), half = new Vector2(0.34f * Mathf.Abs(scale.x), 0.34f * Mathf.Abs(scale.z)), yaw = yaw });
                }
                else
                {
                    if (h.blocksWalking) x.occupied.Add(new TWGroveContext.Footprint { point = p, half = half, yaw = yaw });
                    if (!h.key.StartsWith("Border_", StringComparison.Ordinal))
                        x.earthPatches.Add(new TWGroveContext.Circle { point = p, radius = Mathf.Min(2.3f, half.magnitude + 0.4f) });
                }
            }
        }

        static void Atmosphere(TWGroveContext x)
        {
            var existing = x.marker.gameObject.scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<GladeVibe>(true));
            if (existing.Any(v => v.gameObject.activeInHierarchy && Vector3.Distance(v.transform.position, x.origin) < 16f &&
                !x.old.Any(g => v.transform.IsChildOf(g.transform)) && (x.previous == null || x.previous.artRoot == null || !v.transform.IsChildOf(x.previous.artRoot)))) return;
            var air = new GameObject("30_Лесной_воздух"); air.transform.SetParent(x.root, false);
            var vibe = air.AddComponent<GladeVibe>(); vibe.radius = 11.5f; vibe.haze = false;
            vibe.dustRate = 1.5f; vibe.flyMaxRate = 2.1f; vibe.leafRate = 0.25f;
        }

        [MenuItem(Menu + "3. Откатить оформление южной рощи", false, 272)]
        public static void Rollback()
        {
            int group = -1;
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Останови Play Mode.");
                Scene scene = SceneManager.GetActiveScene();
                var states = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<GroveArtState>(true)).ToArray();
                if (states.Length != 1) throw new InvalidOperationException("Нужен один установленный GROVE ART в активной сцене.");
                var state = states[0]; var marker = state.transform;
                if (scene.isDirty && !EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Не удалось сохранить сцену для резервной копии.");
                Backup(scene);
                Undo.IncrementCurrentGroup(); group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Restore legacy grove scenery");
                foreach (var item in state.hidden)
                    if (item.target != null) { Undo.RecordObject(item.target, "Restore grove scenery"); item.target.SetActive(item.active); }
                if (state.hadBuilder && marker.GetComponent<ChopGroveBuilder>() == null)
                {
                    var builder = Undo.AddComponent<ChopGroveBuilder>(marker.gameObject);
                    if (!string.IsNullOrEmpty(state.builderJson)) EditorJsonUtility.FromJsonOverwrite(state.builderJson, builder);
                }
                if (state.artRoot != null) Undo.DestroyObjectImmediate(state.artRoot.gameObject);
                Undo.DestroyObjectImmediate(state); Undo.CollapseUndoOperations(group); EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log("GROVE ART: исходный декор и настройки генератора возвращены. Ctrl+S.");
            }
            catch (Exception e)
            {
                if (group >= 0) Undo.RevertAllDownToGroup(group);
                Debug.LogError("GROVE ART: " + e.Message);
            }
        }

        [MenuItem(Menu + "4. Проверить установленную поляну", false, 273)]
        public static void QA()
        {
            try { TWGroveValidation.Installed(Inspect()); }
            catch (Exception e) { Debug.LogError("GROVE ART QA: " + e.Message); }
        }

        [MenuItem(Menu + "5. Выделить отдельные объекты поляны", false, 274)]
        static void Frame()
        {
            try
            {
                var x = Inspect();
                if (x.previous == null || x.previous.artRoot == null) throw new InvalidOperationException("Сначала пункт 2.");
                Selection.activeGameObject = x.previous.artRoot.gameObject;
                SceneView.lastActiveSceneView?.LookAt(x.origin + Vector3.up, x.rotation * Quaternion.Euler(46, 0, 0), 22f);
            }
            catch (Exception e) { Debug.LogError("GROVE ART: " + e.Message); }
        }

        static TWGroveContext Inspect()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) throw new InvalidOperationException("Останови Play Mode и дождись компиляции.");
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path)) throw new InvalidOperationException("Открой сохранённую сцену острова.");
            var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
            var markers = all.Where(t => t.name == "PAD_GroveSouth").ToArray();
            if (markers.Length != 1) throw new InvalidOperationException("В активной сцене должна быть одна метка PAD_GroveSouth.");
            var marker = markers[0]; Transform island = marker;
            while (island.parent != null && island.name != IslandBlockout.RootName) island = island.parent;
            var manual = island.Find(IslandBlockout.ManualName); var generated = island.Find(IslandBlockout.GeneratedName);
            if (manual == null || generated == null) throw new InvalidOperationException("Нет IslandV2/_Manual и _Generated.");
            if (Vector3.Distance(manual.lossyScale, Vector3.one) > 0.001f) throw new InvalidOperationException("Масштаб _Manual должен быть 1.");
            var layout = AssetDatabase.LoadAssetAtPath<IslandLayout>(IslandLayout.AssetPath);
            if (layout == null) throw new InvalidOperationException("Нет исходной раскладки IslandLayout.");
            var x = new TWGroveContext { marker = marker, manual = manual, generated = generated, origin = marker.position,
                previous = marker.GetComponent<GroveArtState>(), field = new IslandField(layout) };
            x.candy = AssetDatabase.LoadAssetAtPath<IslandCandy>(IslandCandy.AssetPath);
            var authored = manual.Find(RootName);
            if (authored != null && (x.previous == null || x.previous.artRoot != authored))
                throw new InvalidOperationException("Уже есть TW_GROVE_SOUTH без связанного состояния на PAD_GroveSouth. Восстанови GroveArtState или переименуй лишнюю копию.");
            var camp = all.FirstOrDefault(t => t.name == "CAMPFIRE_Point");
            Vector3 outward = camp != null ? camp.position - x.origin : Vector3.back; outward.y = 0;
            if (outward.sqrMagnitude < 0.01f) outward = Vector3.back;
            x.rotation = Quaternion.LookRotation(-outward.normalized, Vector3.up);
            foreach (string name in new[] { "ChopZone", "ChopCamp", "ChopGladeDecor" })
            {
                Transform old = manual.Find(name);
                if (old != null && Vector2.Distance(new Vector2(old.position.x, old.position.z), new Vector2(x.origin.x, x.origin.z)) < 5f) AddOld(x, old);
            }
            var runtime = marker.Find(ChopGroveBuilder.RootName); if (runtime != null) AddOld(x, runtime);
            x.ReadGround(); x.ReadObstacles(all); Physics.SyncTransforms();
            if (x.resources.Length < 5) throw new InvalidOperationException("У метки найдено меньше пяти ResourceItem. Проверь сцену южной рощи.");
            if (x.buildings.Any(b => b.point.magnitude < b.radius + 0.5f))
            {
                float r = x.buildings.Where(b => b.point.magnitude < b.radius + 0.5f).Max(b => b.radius + 0.7f - b.point.y);
                x.hub = new Vector2(0, -r);
            }
            var material = AssetDatabase.LoadAssetAtPath<Material>(IslandBlockout.MaterialPath);
            if (material == null || !material.HasProperty("_GroundTex") || material.GetTexture("_GroundTex") == null) throw new InvalidOperationException("Нет исходной текстуры земли острова.");
            if (Shader.Find("Project/CandyToon") == null) throw new InvalidOperationException("Не импортирован родной Project/CandyToon.");
            return x;
        }

        static void AddOld(TWGroveContext x, Transform old)
        {
            if (old.GetComponentsInChildren<ResourceItem>(true).Length != 0 || old.GetComponentsInChildren<BuildingController>(true).Length != 0 ||
                old.GetComponentsInChildren<HutStages>(true).Length != 0 || old.GetComponentsInChildren<MapZone>(true).Length != 0 || old.GetComponentsInChildren<LandmarkTree>(true).Length != 0)
                throw new InvalidOperationException("В старом слое «" + old.name + "» есть игровые компоненты. Его нельзя выключать целиком.");
            x.old.Add(old.gameObject);
        }

        static string Backup(Scene scene)
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            string dir = Path.Combine(project, "UserSettings/TW_GroveBackups", AssetDatabase.AssetPathToGUID(scene.path)); Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + ".unity");
            File.Copy(Path.Combine(project, scene.path), path, false); return path;
        }
    }
}
