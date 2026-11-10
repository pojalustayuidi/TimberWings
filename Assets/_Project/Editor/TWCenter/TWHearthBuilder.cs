using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IslandV2
{
    public static class TWHearthBuilder
    {
        const string RootName = "TW_HEARTH_ART";
        const string Menu = "Tools/BuilderSim/Здания/CAMPFIRE ART/";

        public static GameObject Existing()
        {
            var all = SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<HutStages>(true)).Where(s => s.name == "Campfire").ToArray();
            return all.Length == 1 ? all[0].gameObject : null;
        }

        public static bool HasExisting()
        {
            return SceneManager.GetActiveScene().GetRootGameObjects().Any(g => g.GetComponentsInChildren<HutStages>(true).Any(s => s.name == "Campfire"));
        }

        [MenuItem(Menu + "1. Обновить внешний вид существующего костра", false, 308)]
        public static void Apply() { ApplyExisting(Existing(), true); }

        public static void AfterLegacyBuild(GameObject fire) { ApplyExisting(fire, false); }

        internal static bool ApplyExisting(GameObject fireGo, bool requireSaved)
        {
            string folder = null; int group = -1;
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) throw new InvalidOperationException("Останови Play Mode и дождись компиляции.");
                if (fireGo == null) throw new InvalidOperationException("Нужен один существующий Campfire с HutStages.");
                var scene = fireGo.scene;
                if (string.IsNullOrEmpty(scene.path) || (requireSaved && scene.isDirty)) throw new InvalidOperationException("Сначала сохрани игровую сцену Ctrl+S.");
                Transform fire = fireGo.transform;
                var stages = new[] { fire.Find("Stage1"), fire.Find("Stage2"), fire.Find("Stage3") };
                if (stages.Any(t => t == null) || fireGo.GetComponent<HutStages>() == null || fireGo.GetComponentsInChildren<BuildingController>(true).Length != 1)
                    throw new InvalidOperationException("Нужны существующие Stage1/2/3, HutStages и одна зона строительства.");
                var controller = fireGo.GetComponentInChildren<BuildingController>(true);
                var stageController = fireGo.GetComponent<HutStages>();
                if (controller.stages != null && controller.stages != stageController) throw new InvalidOperationException("Зона строительства привязана к другому зданию.");
                var serializedStages = new SerializedObject(stageController);
                for (int i = 0; i < 3; i++)
                {
                    var property = serializedStages.FindProperty("stage" + (i + 1));
                    if (property != null && property.objectReferenceValue != null && property.objectReferenceValue != stages[i].gameObject)
                        throw new InvalidOperationException("HutStages использует нестандартные объекты стадий.");
                }
                var flames = stages[2].Find("Flames");
                if (flames == null) throw new InvalidOperationException("Нет существующего Stage3/Flames для мерцания. Костёр не изменён.");
                var island = scene.GetRootGameObjects().Single(g => g.name == IslandBlockout.RootName).transform;
                var manual = island.Find(IslandBlockout.ManualName);
                if (manual == null) throw new InvalidOperationException("Нет IslandV2/_Manual.");
                var installed = manual.GetComponentsInChildren<HearthArtState>(true).Where(s => s.campfire == fire).ToArray();
                if (installed.Length > 1) throw new InvalidOperationException("Найдены дубли HearthArtState.");
                var previous = installed.FirstOrDefault();
                string logicBefore = TWCenterValidation.LogicHash(fire);
                string backup = TWCenterArt.Backup(scene);
                Undo.IncrementCurrentGroup(); group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("TimberWings — campfire art, preserve construction");
                folder = "Assets/_Project/IslandV2/HearthArt/Scenes/" + AssetDatabase.AssetPathToGUID(scene.path) + "/Build_" + Guid.NewGuid().ToString("N").Substring(0, 12);
                IslandLayout.EnsureFolder(folder);
                var owner = new GameObject(RootName + "__building"); owner.transform.SetParent(manual, false);
                Undo.RegisterCreatedObjectUndo(owner, "Create hearth art ownership");
                var state = owner.AddComponent<HearthArtState>(); state.campfire = fire; state.generatedFolder = folder; state.logicHash = logicBefore;
                var hidden = previous != null ? previous.hidden.Where(h => h.target != null).ToList() : new List<HearthArtState.HiddenObject>();
                var colliders = previous != null ? previous.colliders.Where(h => h.target != null).ToList() : new List<HearthArtState.HiddenCollider>();
                foreach (var stage in stages)
                {
                    foreach (var renderer in stage.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        if (TWCenterValidation.IsHearthVisual(renderer.transform) || renderer.GetComponentInParent<Canvas>() != null) continue;
                        var go = renderer.gameObject;
                        if (go.GetComponents<MonoBehaviour>().Length > 0 || go.transform == stage || go.transform == flames)
                            throw new InvalidOperationException("Модель объединена с игровыми компонентами: " + go.name + ". Обновление остановлено.");
                        if (!hidden.Any(h => h.target == go)) hidden.Add(new HearthArtState.HiddenObject { target = go, active = go.activeSelf });
                    }
                    foreach (var collider in stage.GetComponentsInChildren<Collider>(true))
                        if (!collider.isTrigger && !TWCenterValidation.IsHearthVisual(collider.transform) && !colliders.Any(c => c.target == collider))
                            colliders.Add(new HearthArtState.HiddenCollider { target = collider, enabled = collider.enabled });
                }
                var hearts = stages[2].Find("Hearts");
                if (hearts != null && !hidden.Any(h => h.target == hearts.gameObject)) hidden.Add(new HearthArtState.HiddenObject { target = hearts.gameObject, active = hearts.gameObject.activeSelf });
                state.hidden = hidden.ToArray(); state.colliders = colliders.ToArray();
                var roots = new List<GameObject>();
                for (int i = 0; i < 4; i++)
                {
                    var art = new GameObject("TW_HEARTH_VISUAL_" + i);
                    art.transform.SetParent(i == 3 ? flames : stages[i], false);
                    Undo.RegisterCreatedObjectUndo(art, "Create staged hearth artwork"); roots.Add(art);
                }
                state.visuals = roots.ToArray();
                var assets = new TWCenterAssets(folder); assets.AddHearthPalette();
                int preview = stages[2].gameObject.activeSelf ? 3 : stages[1].gameObject.activeSelf ? 2 : 1;
                TWHearthVisual.Build(assets, roots[0].transform, roots[1].transform, roots[2].transform, roots[3].transform, preview);
                foreach (var art in roots)
                    foreach (var filter in art.GetComponentsInChildren<MeshFilter>(true))
                        if (AssetDatabase.GetAssetPath(filter.sharedMesh).StartsWith(folder + "/", StringComparison.Ordinal)) TWCenterValidation.Mesh(filter.sharedMesh);
                foreach (var item in hidden) { Undo.RecordObject(item.target, "Hide previous hearth geometry"); item.target.SetActive(false); }
                foreach (var item in colliders) { Undo.RecordObject(item.target, "Disable previous visual collision"); item.target.enabled = false; }
                if (previous != null)
                {
                    foreach (var old in previous.visuals) if (old != null) Undo.DestroyObjectImmediate(old);
                    Undo.DestroyObjectImmediate(previous.gameObject);
                }
                owner.name = RootName;
                if (TWCenterValidation.LogicHash(fire) != logicBefore) throw new InvalidOperationException("Параметры строительства или логика изменились. Облик отменён.");
                foreach (var center in manual.GetComponentsInChildren<CenterArtState>(true))
                    if (center.campfire == fire) { Undo.RecordObject(center, "Update authorized campfire art snapshot"); center.campfireHash = TWCenterValidation.FireHash(fire); EditorUtility.SetDirty(center); }
                EditorUtility.SetDirty(state); AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); Undo.CollapseUndoOperations(group);
                Selection.activeGameObject = fireGo;
                Debug.Log("CAMPFIRE ART: внешний вид трёх стадий обновлён. Существующие Campfire, HutStages, BuildZone, BuildPoint, цены и логика сохранены. " +
                    "Ctrl+S. Для центра целиком — CENTER ART, пункт 6. Резервная сцена: " + backup);
                return true;
            }
            catch (Exception e)
            {
                if (group >= 0) Undo.RevertAllDownToGroup(group);
                if (!string.IsNullOrEmpty(folder) && AssetDatabase.IsValidFolder(folder)) AssetDatabase.DeleteAsset(folder);
                Debug.LogError("CAMPFIRE ART: обновление отменено. " + e); return false;
            }
        }

        [MenuItem(Menu + "2. Проверить стадии и сохранность логики", false, 309)]
        public static void QA()
        {
            try
            {
                var fire = Existing();
                if (fire == null) throw new InvalidOperationException("Нет одного Campfire.");
                var state = fire.scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<HearthArtState>(true)).Single(s => s.campfire == fire.transform);
                Check(state); Debug.Log("CAMPFIRE ART QA: PASS. Стадии, артефакты, исходные игровые компоненты и цены сохранены.");
            }
            catch (Exception e) { Debug.LogError("CAMPFIRE ART QA: " + e.Message); }
        }

        internal static void Check(HearthArtState state)
        {
            if (state.campfire == null || TWCenterValidation.LogicHash(state.campfire) != state.logicHash) throw new InvalidOperationException("Игровые параметры изменились после применения.");
            if (state.visuals.Length != 4 || state.visuals.Any(g => g == null)) throw new InvalidOperationException("Потеряны новые визуальные стадии.");
            for (int i = 0; i < 3; i++)
                if (state.visuals[i].transform.parent != state.campfire.Find("Stage" + (i + 1))) throw new InvalidOperationException("Нарушена привязка арта к стадиям.");
            if (state.visuals[3].transform.parent != state.campfire.Find("Stage3/Flames")) throw new InvalidOperationException("Пламя отсоединено от существующего мерцания.");
            foreach (var item in state.hidden) if (item.target != null && item.target.activeSelf) throw new InvalidOperationException("Старый арт включён: " + item.target.name);
            foreach (var item in state.colliders) if (item.target != null && item.target.enabled) throw new InvalidOperationException("Старый визуальный коллайдер снова включён.");
            foreach (var art in state.visuals)
                foreach (var filter in art.GetComponentsInChildren<MeshFilter>(true))
                    if (AssetDatabase.GetAssetPath(filter.sharedMesh).StartsWith(state.generatedFolder + "/", StringComparison.Ordinal)) TWCenterValidation.Mesh(filter.sharedMesh);
        }

        [MenuItem(Menu + "3. Вернуть прежний внешний вид костра", false, 310)]
        public static void Rollback()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("Сначала останови Play Mode."); return; }
            var scene = SceneManager.GetActiveScene();
            var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<HearthArtState>(true)).ToArray();
            if (all.Length != 1) { Debug.LogError("Нужен один установленный комплект костра."); return; }
            var state = all[0]; var fire = state.campfire;
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Restore old campfire art only");
            foreach (var item in state.hidden) if (item.target != null) { Undo.RecordObject(item.target, "Restore original hearth visuals"); item.target.SetActive(item.active); }
            foreach (var item in state.colliders) if (item.target != null) { Undo.RecordObject(item.target, "Restore original collision"); item.target.enabled = item.enabled; }
            foreach (var art in state.visuals) if (art != null) Undo.DestroyObjectImmediate(art);
            Undo.DestroyObjectImmediate(state.gameObject);
            foreach (var center in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<CenterArtState>(true)))
                if (center.campfire == fire) { Undo.RecordObject(center, "Update restored hearth snapshot"); center.campfireHash = TWCenterValidation.FireHash(fire); EditorUtility.SetDirty(center); }
            Undo.CollapseUndoOperations(group); EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("CAMPFIRE ART: прежний облик возвращён, игровое здание сохранено. Ctrl+S.");
        }
    }
}
