// Timber Wings — FINAL ART / ISL-004. Cleanup is deliberate, reversible, and limited to known legacy overlays.
// Menu order: Audit -> Scene Cleanup -> Archive Legacy Sources -> Audit -> RIVER ART Audit/Apply -> Island Art v0.2 Audit/Apply.
// Never invokes IslandBlockout.Build/IslandAll or changes IslandLayout, gameplay colliders, paths, Nav or Save IDs.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace IslandV2
{
    public static class TWFinalCleanup
    {
        const string Menu = "Tools/BuilderSim/Локация/Остров v2/FINAL CLEANUP/";
        const string CurrentRiver = "TW_RIVER_ART";
        const string CurrentIsland = "TW_ISLAND_RELEASE_ART_V2";
        static readonly string[] OldSceneRoots =
        {
            "TW_RIVER_OVERHAUL_V1", "TW_RIVER_OVERHAUL_V2", "TW_RIVER_OVERHAUL_V3",
            "TW_ISLAND_RELEASE_ART_V1", "TW_IslandArtOverhaul_V1"
        };
        // ONLY superseded Editor files. These are not gameplay classes or scene scripts.
        static readonly string[] OldFiles =
        {
            "Assets/_Project/Editor/RiverOverhaul/TWRiverOverhaul.cs",
            "Assets/_Project/Editor/RiverOverhaul/TWRiverOverhaulGeometry.cs",
            "Assets/_Project/Editor/RiverOverhaul/TWRiverOverhaulQA.cs",
            "Assets/_Project/Editor/RiverOverhaulV3/TWRiverOverhaulV3.cs",
            "Assets/_Project/Editor/RiverOverhaulV3/TWRiverOverhaulGeometryV3.cs",
            "Assets/_Project/Editor/RiverOverhaulV3/TWRiverOverhaulQAV3.cs",
            "Assets/_Project/Editor/TWIslandRelease/TWIslandReleaseV1.cs",
            "Assets/_Project/Editor/TWIslandRelease/TWIslandReleaseGeometry.cs",
            "Assets/_Project/Editor/TWIslandRelease/TWIslandReleaseQA.cs",
            "Assets/_Project/Editor/TWIslandArtOverhaulV1.cs"
        };
        // The v0.2 waterfall veil shader is NOT used by River v0.3.
        const string OldVeil = "Assets/_Project/Shaders/IslandV2/TW_RiverVeil.shader";
        static readonly string[] OldGeneratedFolders =
        {
            "Assets/_Project/IslandV2/RiverOverhaul",
            "Assets/_Project/IslandV2/RiverOverhaul_v0_2",
            "Assets/_Project/IslandV2/RiverOverhaul_v0_3",
            "Assets/_Project/IslandV2/ReleaseArt_V1",
            "Assets/_Project/Generated/TWIslandArtOverhaulV1"
        };

        static string ProjectRoot { get { return Directory.GetParent(Application.dataPath).FullName; } }
        static string Physical(string assetPath) { return Path.Combine(ProjectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)); }
        static string SafeStamp() { return DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff"); }
        static string BackupRoot(string suffix)
        {
            return Path.Combine(ProjectRoot, "Library", "TW_FinalCleanup", SafeStamp() + "_" + suffix);
        }
        static bool SceneRoot(out Scene scene, out Transform manual, out string why)
        {
            scene = SceneManager.GetActiveScene(); manual = null; why = null;
            if (EditorApplication.isPlayingOrWillChangePlaymode) { why = "Останови Play Mode."; return false; }
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(scene.path))
            { why = "Открой и сохрани сцену острова."; return false; }
            var island = scene.GetRootGameObjects().Where(g => g.name == "IslandV2").ToArray();
            if (island.Length != 1) { why = "Нужен ровно один корневой IslandV2 в активной сцене (сейчас " + island.Length + ")."; return false; }
            manual = island[0].transform.Find("_Manual");
            if (manual == null) { why = "Отсутствует IslandV2/_Manual."; return false; }
            return true;
        }
        static bool SavedScene(out Scene scene, out Transform manual)
        {
            string error;
            if (!SceneRoot(out scene, out manual, out error)) { Debug.LogError("FINAL CLEANUP: " + error); return false; }
            if (scene.isDirty) { Debug.LogError("FINAL CLEANUP: сначала Ctrl+S — в сцене несохранённые изменения."); return false; }
            string file = Physical(scene.path);
            if (!File.Exists(file)) { Debug.LogError("FINAL CLEANUP: нет файла сцены: " + file); return false; }
            return true;
        }

        [MenuItem(Menu + "1. ПРОВЕРИТЬ / старое, актуальное, сцена", false, 245)]
        public static void Audit()
        {
            Scene s; Transform manual; string why;
            var sb = new StringBuilder();
            sb.AppendLine("TIMBER WINGS / FINAL CLEANUP / read-only / " + DateTime.UtcNow.ToString("O"));
            bool sceneReady = SceneRoot(out s, out manual, out why);
            sb.AppendLine("Active scene: " + s.path);
            sb.AppendLine("Scene saved: " + (sceneReady && !s.isDirty));
            if (!sceneReady) sb.AppendLine("BLOCK: " + why);
            if (sceneReady)
            {
                foreach (string n in OldSceneRoots)
                    sb.AppendLine((manual.Find(n) != null ? "OLD SCENE ART: " : "absent: ") + n);
                sb.AppendLine("CURRENT RIVER: " + (manual.Find(CurrentRiver) != null ? "installed" : "not installed"));
                sb.AppendLine("CURRENT ISLAND: " + (manual.Find(CurrentIsland) != null ? "installed" : "not installed"));
            }
            foreach (var n in OldFiles)
                if (File.Exists(Physical(n))) sb.AppendLine("OLD SOURCE: " + n);
            foreach (var n in OldGeneratedFolders)
                if (Directory.Exists(Physical(n))) sb.AppendLine("OLD GENERATED FOLDER (candidate): " + n);
            if (File.Exists(Physical(OldVeil))) sb.AppendLine("OLD SHADER (candidate): " + OldVeil);
            string v3 = "Assets/_Project/Editor/TWRiver/TWRiverArt.cs";
            string v2 = "Assets/_Project/Editor/TWIslandRelease/TWIslandReleaseV2.cs";
            sb.AppendLine("Current scripts: RiverArt=" + File.Exists(Physical(v3)) + ", IslandV2=" + File.Exists(Physical(v2)));
            sb.AppendLine("Protected: _Generated, _Manual itself, IslandLayout, native water, bridge, Terrain, Nav, gameplay scripts, saves.");
            string dir = Path.Combine(ProjectRoot, "Library", "TW_FinalCleanup");
            Directory.CreateDirectory(dir);
            string report = Path.Combine(dir, "FINAL_AUDIT.txt");
            File.WriteAllText(report, sb.ToString(), Encoding.UTF8);
            Debug.Log("FINAL CLEANUP AUDIT: " + report + "\n" + sb);
        }

        [MenuItem(Menu + "2. УБРАТЬ СТАРЫЕ АРТ-СЛОИ ИЗ СЦЕНЫ", false, 246)]
        public static void CleanupScene()
        {
            Scene scene; Transform manual;
            if (!SavedScene(out scene, out manual)) return;
            var old = OldSceneRoots.Select(n => manual.Find(n)).Where(t => t != null).ToArray();
            if (old.Length == 0) { Debug.Log("FINAL CLEANUP: старых арт-слоёв в сцене нет."); return; }
            string names = string.Join(", ", old.Select(t => t.name).ToArray());
            if (!EditorUtility.DisplayDialog("TIMBER WINGS — Cleanup scene",
                "Найдены старые слои: " + names + "\n\n" +
                "Будет создана независимая копия текущей .unity сцены, после чего УДАЛЯТСЯ ТОЛЬКО эти exact-name дочерние объекты внутри IslandV2/_Manual.\n" +
                "_Generated, мост, коллайдеры, River_Water/River_Fall, NPC, сейвы и актуальные V3/V2 не затрагиваются.\n\n" +
                "Убедись, что ты находишься в нужной копии сцены.", "Backup и убрать старый арт", "Отмена")) return;
            string backup = BackupRoot("scene");
            try
            {
                Directory.CreateDirectory(backup);
                File.Copy(Physical(scene.path), Path.Combine(backup, Path.GetFileName(scene.path)), false);
                if (File.Exists(Physical(scene.path) + ".meta"))
                    File.Copy(Physical(scene.path) + ".meta", Path.Combine(backup, Path.GetFileName(scene.path) + ".meta"), false);
            }
            catch (Exception e) { Debug.LogError("FINAL CLEANUP: бэкап сцены не готов; ничего не изменено. " + e); return; }
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("TW Final Cleanup - remove legacy art");
            int group = Undo.GetCurrentGroup();
            foreach (var tr in old) Undo.DestroyObjectImmediate(tr.gameObject);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("FINAL CLEANUP: удалены старые art roots: " + names + ". Backup: " + backup + ". Теперь Ctrl+S, потом пункт 3.");
        }

        [MenuItem(Menu + "3. АРХИВИРОВАТЬ СТАРЫЕ ФАЙЛЫ (не безвозвратно)", false, 247)]
        public static void ArchiveSources()
        {
            Scene scene; Transform manual;
            if (!SavedScene(out scene, out manual)) return;
            string[] present = OldSceneRoots.Where(n => manual.Find(n) != null).ToArray();
            if (present.Length > 0)
            {
                Debug.LogError("FINAL CLEANUP: старые арт-объекты ещё есть: " + string.Join(", ", present) + ". Сначала пункт 2 и Ctrl+S.");
                return;
            }
            // Don't remove code if a different source file actually calls these legacy class names.
            string[] legacyTypes = { "TWRiverOverhaul.", "TWRiverOverhaulQA.", "TWRiverOverhaulGeometry.",
                                     "TWRiverOverhaulV3.", "TWRiverOverhaulQAV3.", "TWRiverOverhaulGeometryV3.",
                                     "TWIslandReleaseV1.", "TWIslandReleaseGeometry.", "TWIslandReleaseQA.",
                                     "TWIslandArtOverhaulV1." };
            var oldFull = new HashSet<string>(OldFiles.Select(Physical), StringComparer.OrdinalIgnoreCase);
            var collisions = new List<string>();
            string ownSource = Physical("Assets/_Project/Editor/TWFinalCleanup/TWFinalCleanup.cs");
            foreach (var file in Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories))
            {
                if (oldFull.Contains(file) || string.Equals(file, ownSource, StringComparison.OrdinalIgnoreCase)) continue;
                // Exclude this cleanup tool's references as it uses exact scene names, not legacy classes.
                var text = File.ReadAllText(file);
                foreach (string type in legacyTypes)
                    if (text.Contains(type)) { collisions.Add(file + " references " + type); break; }
            }
            if (collisions.Count > 0)
            {
                Debug.LogError("FINAL CLEANUP: есть исходники, ссылающиеся на старые классы. Удаление отложено:\n" + string.Join("\n", collisions));
                return;
            }
            if (!EditorUtility.DisplayDialog("TIMBER WINGS — Archive obsolete files",
                "Старые версии Editor-кода переместятся ВНЕ Assets, в Library/TW_FinalCleanup/.\n" +
                "Устаревшие ассеты/шейдер переместятся только если не используются сценами, префабами или материалами.\n" +
                "Исходные файлы, GUID и .meta сохраняются в архиве — при необходимости можно восстановить.\n\n" +
                "Актуальные River Art (TW_RIVER_ART) и Island Art v0.2 останутся.", "Архивировать старое", "Отмена")) return;
            string archive = BackupRoot("files");
            Directory.CreateDirectory(archive);
            var moved = new List<string>();
            var skipped = new List<string>();
            // Editor code has no serialized scene component, but may be referenced by other source.
            foreach (var p in OldFiles) StageFile(p, archive, moved);
            StageEmptyDirectory("Assets/_Project/Editor/RiverOverhaul", archive, moved);
            StageEmptyDirectory("Assets/_Project/Editor/RiverOverhaulV3", archive, moved);
            foreach (var folder in OldGeneratedFolders)
            {
                if (!Directory.Exists(Physical(folder))) continue;
                if (IsUsedByScenesOrPrefabs(folder)) { skipped.Add(folder + " (есть зависимости сцены/префаба/материала)"); continue; }
                StageDirectory(folder, archive, moved);
            }
            if (File.Exists(Physical(OldVeil)))
            {
                if (IsUsedByScenesOrPrefabs(OldVeil)) skipped.Add(OldVeil + " (есть зависимости)");
                else StageFile(OldVeil, archive, moved);
            }
            string report = "Archive: " + archive + "\nMoved:\n" + string.Join("\n", moved) +
                            "\nSkipped:\n" + string.Join("\n", skipped) + "\n";
            File.WriteAllText(Path.Combine(archive, "RESTORE_MANIFEST.txt"), report, Encoding.UTF8);
            Debug.Log("FINAL CLEANUP: устаревшее отправлено в резервную папку. " + report + "\nUnity сейчас обновит импорт и перекомпилирует Editor.");
            AssetDatabase.Refresh();
        }

        [MenuItem(Menu + "4. ПОКАЗАТЬ ПОСЛЕДНИЙ ОТЧЁТ", false, 248)]
        public static void ShowReport()
        {
            string path = Path.Combine(ProjectRoot, "Library", "TW_FinalCleanup", "FINAL_AUDIT.txt");
            if (!File.Exists(path)) Audit();
            else EditorUtility.RevealInFinder(path);
        }

        static void StageFile(string assetPath, string archive, List<string> moved)
        {
            string source = Physical(assetPath);
            if (!File.Exists(source)) return;
            string dest = Path.Combine(archive, assetPath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            File.Move(source, dest);
            if (File.Exists(source + ".meta")) File.Move(source + ".meta", dest + ".meta");
            moved.Add(assetPath);
        }
        static void StageDirectory(string assetPath, string archive, List<string> moved)
        {
            string source = Physical(assetPath);
            if (!Directory.Exists(source)) return;
            string dest = Path.Combine(archive, assetPath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            Directory.Move(source, dest);
            if (File.Exists(source + ".meta")) File.Move(source + ".meta", dest + ".meta");
            moved.Add(assetPath + "/");
        }
        static void StageEmptyDirectory(string assetPath, string archive, List<string> moved)
        {
            string source = Physical(assetPath);
            if (!Directory.Exists(source) || Directory.GetFileSystemEntries(source).Length != 0) return;
            string dest = Path.Combine(archive, assetPath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            Directory.Move(source, dest);
            if (File.Exists(source + ".meta")) File.Move(source + ".meta", dest + ".meta");
            moved.Add(assetPath + "/ (empty)");
        }
        static bool IsUsedByScenesOrPrefabs(string assetPathOrFolder)
        {
            var scenes = AssetDatabase.FindAssets("t:Scene");
            var prefabs = AssetDatabase.FindAssets("t:Prefab");
            var materials = AssetDatabase.FindAssets("t:Material");
            foreach (string guid in scenes.Concat(prefabs).Concat(materials))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path == assetPathOrFolder || path.StartsWith(assetPathOrFolder + "/", StringComparison.Ordinal)) continue;
                // Old-generated asset folder references are only safe to archive when nothing in use points to them.
                var deps = AssetDatabase.GetDependencies(path, true);
                foreach (var dep in deps)
                    if (dep == assetPathOrFolder ||
                        (Directory.Exists(Physical(assetPathOrFolder)) && dep.StartsWith(assetPathOrFolder + "/", StringComparison.Ordinal))) return true;
            }
            return false;
        }
    }
}
