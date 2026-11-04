#if UNITY_EDITOR
// Timber Wings / ONE-TIME CENTER EMERGENCY ROLLBACK
// Based on Scripts(7).zip and ISL023/024/B/C, CenterComplete v1, HOTFIX, SafeV2 bundles.
// Absolutely NO Island builder/terrain/collider/save-game work.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IslandV2
{
    public static class TWCenterEmergencyRollback
    {
        private const string Menu = "Tools/BuilderSim/ЦЕНТР — ВОССТАНОВЛЕНИЕ/";
        private const string BackupParent = "TW_CENTER_ROLLBACK_BACKUPS";
        private const string Stage2Asset = "Assets/_Game/Campfire/CandyCamp_S2.asset";
        private const string Stage3Asset = "Assets/_Game/Campfire/CandyCamp_S3.asset";
        private const string FlameAsset = "Assets/_Game/Campfire/CandyCamp_S3Flame.asset";

        // Exact objects created by the previous ISL-023/024 and Complete packages.
        private static readonly string[] OverlayPaths = {
            "IslandV2/_Generated/CampPlaza/ISL023_StoneRhythm",
            "IslandV2/_Generated/CampPlaza/TW_CenterComplete_Art",
            "IslandV2/_Generated/CampPlaza/TW_CenterSafeV2_Art",
            "Campfire/Stage2/ISL024_TripodAccents",
            "Campfire/Stage3/ISL024_LogsAccents",
            "Campfire/Stage3/Flames/ISL024_FlameAccents",
            "Campfire/Stage2/ISL024B_TripodHero",
            "Campfire/Stage2/ISL024B_Kettle",
            "Campfire/Stage3/ISL024B_CoreLogs",
            "Campfire/Stage3/Flames/ISL024B_FlameHero",
            "Campfire/Stage3/TW_CenterComplete_Atmosphere",
            "Campfire/Stage3/TW_CenterSafeV2_Audio"
        };
        private static readonly string[] GeneratedFolders = {
            "Assets/_Project/Generated/ISL024",
            "Assets/_Project/Generated/ISL024B",
            "Assets/_Project/Generated/ISL024C",
            "Assets/_Project/Generated/TWCenterComplete",
            "Assets/_Project/Generated/TWCenterSafeV2"
        };
        private static readonly string[] OldCodeFiles = {
            "ISL023StoneRhythm.cs",
            "ISL024CampfireSilhouette.cs",
            "ISL024CampfireSilhouetteV2.cs",
            "ISL024CCompactCampfire.cs",
            "ISL_CampCenterComplete.cs",
            "TW_CenterCompleteHotfix.cs",
            "TWCenterCompleteSafeV2.cs"
        };
        private static readonly string[] RuntimeCodePaths = {
            "Assets/_Project/Scripts/Environment/TWCampCenterAtmosphere.cs",
            "Assets/_Project/Scripts/Environment/TWCampCenterRestoreState.cs"
        };
        private const string SoundAsset = "Assets/_Project/Audio/TW_WarmCampfireLoop.wav";
        private static string lastBackupPath;

        [MenuItem(Menu + "1. Проверить и показать план отката (без изменений)", false, 500)]
        private static void Audit()
        {
            Scene scene;
            Transform root, stage2, stage3;
            string problem;
            bool ok = Inspect(out scene, out root, out stage2, out stage3, out problem);
            var sb = new StringBuilder();
            sb.AppendLine("TW EMERGENCY CENTER ROLLBACK / AUDIT (READ ONLY)");
            sb.AppendLine("Unity: " + Application.unityVersion + "; UTC: " + DateTime.UtcNow.ToString("O"));
            sb.AppendLine("Scene: " + SceneManager.GetActiveScene().path);
            sb.AppendLine("PRECONDITIONS: " + (ok ? "OK" : "FAILED: " + problem));
            if (ok)
            {
                foreach (string p in OverlayPaths)
                {
                    Transform tr = Find(scene, p);
                    sb.AppendLine("OVERLAY " + p + " = " + (tr == null ? "ABSENT" : tr.gameObject.activeSelf ? "ACTIVE" : "INACTIVE"));
                }
                DescribeMesh(sb, stage2, "Mesh_Solid", Stage2Asset);
                DescribeMesh(sb, stage3, "Mesh_Solid", Stage3Asset);
                Transform flames = stage3.Find("Flames");
                if (flames != null) DescribeMesh(sb, flames, "Mesh_Flame", FlameAsset);
                Transform fx = stage3.Find("TW_CenterComplete_Atmosphere");
                if (fx != null)
                    sb.AppendLine("Restore snapshot: " +
                        fx.GetComponents<MonoBehaviour>().Any(component => component != null &&
                            component.GetType().Name == "TWCampCenterRestoreState"));
                var light = stage3.Find("CampLight");
                if (light != null && light.GetComponent<Light>() != null)
                {
                    Light l = light.GetComponent<Light>();
                    sb.AppendLine("CampLight: enabled=" + l.enabled + " intensity=" + l.intensity + " range=" + l.range);
                }
                sb.AppendLine("KNOWN GENERATED FILES: " + string.Join(", ", GeneratedFolders));
            }
            sb.AppendLine("Audit changes NO SCENE, MESHES, LIGHTS, VOLUMES, SAVE FILES OR MATERIALS.");
            string path = Path.Combine(ProjectRoot(), "TW_CENTER_ROLLBACK_AUDIT.txt");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            Debug.Log(sb.ToString() + "\nAudit: " + path);
        }

        [MenuItem(Menu + "2. ОТКАТИТЬ ВСЁ оформление центра (с копией сцены)", false, 501)]
        private static void Rollback()
        {
            Scene scene;
            Transform root, stage2, stage3;
            string issue;
            if (!Inspect(out scene, out root, out stage2, out stage3, out issue))
            {
                Debug.LogError("TW ROLLBACK ABORTED: " + issue);
                return;
            }
            Mesh original2 = AssetDatabase.LoadAssetAtPath<Mesh>(Stage2Asset);
            Mesh original3 = AssetDatabase.LoadAssetAtPath<Mesh>(Stage3Asset);
            Mesh originalFlame = AssetDatabase.LoadAssetAtPath<Mesh>(FlameAsset);
            if (original2 == null || original3 == null || originalFlame == null)
            {
                Debug.LogError("TW ROLLBACK ABORTED: one of THREE CandyCamp original meshes is absent. Nothing changed. " +
                    Stage2Asset + " / " + Stage3Asset + " / " + FlameAsset);
                return;
            }
            MeshFilter mf2 = GetMesh(stage2, "Mesh_Solid");
            MeshFilter mf3 = GetMesh(stage3, "Mesh_Solid");
            Transform flames = stage3.Find("Flames");
            MeshFilter mff = flames == null ? null : GetMesh(flames, "Mesh_Flame");
            if (mf2 == null || mf3 == null || mff == null ||
                !KnownSource(mf2, Stage2Asset) || !KnownSource(mf3, Stage3Asset) || !KnownSource(mff, FlameAsset))
            {
                Debug.LogError("TW ROLLBACK ABORTED: a base MeshFilter is missing or points to an UNKNOWN asset. " +
                    "Will not overwrite a potential later edit. Run Audit and send TXT. Nothing changed.");
                return;
            }
            string scope =
                "SCENE: " + scene.path + "\n\n" +
                "Make backup copy in PROJECT ROOT /" + BackupParent + ".\n" +
                "DELETE ONLY 12 KNOWN overlay objects (ISL-023/024/B + Complete v1/v2), when found.\n" +
                "RESTORE THREE ORIGINAL BASE MESH REFERENCES:\n" + Stage2Asset + "\n" + Stage3Asset + "\n" + FlameAsset + "\n\n" +
                "DO NOT TOUCH: Terrain, plaza slabs, campfire root and stages, collisions, NPCs, " +
                "save IDs, game save files, lighting, post-processing, shaders, original .meta or materials.\n" +
                "This will SAVE the currently opened scene AFTER making a copy.\n\n" +
                "No island/center generation will run.";
            if (scene.path.Contains("_Recovery"))
                scope = "WARNING: ACTIVE SCENE IS UNDER _Recovery. This operation changes ONLY THAT SCENE.\n\n" + scope;
            if (!EditorUtility.DisplayDialog("TW CENTER — FULL EXPERIMENTAL ROLLBACK", scope,
                "Backup + rollback", "Cancel")) return;
            string backup;
            try { backup = Backup(scene); }
            catch (Exception ex)
            {
                Debug.LogError("TW ROLLBACK ABORTED: could not safely back up the scene. " + ex);
                return;
            }
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("TW: remove ALL experimental center changes");
            int count = 0;
            try
            {
                // Restore even when v1 FX root is inactive. It stored source mesh references.
                Swap(mf2, original2);
                Swap(mf3, original3);
                Swap(mff, originalFlame);

                foreach (string path in OverlayPaths)
                {
                    Transform overlay = Find(scene, path);
                    if (overlay == null) continue;
                    Undo.DestroyObjectImmediate(overlay.gameObject);
                    count++;
                }

                Undo.CollapseUndoOperations(group);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                    throw new IOException("Unity did not save active scene.");
                lastBackupPath = backup;
                Debug.Log("TW CENTER ROLLBACK: DONE. Removed " + count +
                    " known overlay roots, restored 3 original mesh refs and saved SCENE. " +
                    "BACKUP: " + backup +
                    "\nENTER PLAY MODE NOW. DO NOT RUN any old Center Complete builder. " +
                    "If blown-out image persists, run menu 4 Play diagnostic; underlying light/postprocessing is not proven fixed.");
            }
            catch (Exception ex)
            {
                Undo.RevertAllDownToGroup(group);
                Debug.LogError("TW ROLLBACK: scene operation failed. Original scene copy remains: " + backup + "\n" + ex);
            }
        }

        [MenuItem(Menu + "3. УБРАТЬ файлы старых патчей после отката (с архивом)", false, 502)]
        private static void RemovePatchFiles()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("TW: exit Play Mode before removing old patch files."); return;
            }
            // Refuse until all loaded scenes have had their patch components removed.
            foreach (Scene sc in Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt))
            {
                if (!sc.IsValid() || !sc.isLoaded) continue;
                foreach (string path in OverlayPaths)
                {
                    if (Find(sc, path) != null)
                    {
                        Debug.LogError("TW: first use menu 2 rollback; overlay still exists: " + path + " in " + sc.path);
                        return;
                    }
                }
            }
            var files = new List<string>();
            foreach (string folder in GeneratedFolders)
                if (AssetDatabase.IsValidFolder(folder)) files.Add(folder);
            foreach (string name in OldCodeFiles)
            {
                string[] locations = {
                    "Assets/_Project/Editor/" + name,
                    "Assets/_Project/Editor/IslandV2/" + name
                };
                foreach (string location in locations)
                    if (AssetDatabase.LoadMainAssetAtPath(location) != null) files.Add(location);
            }
            foreach (string path in RuntimeCodePaths)
                if (AssetDatabase.LoadMainAssetAtPath(path) != null) files.Add(path);
            if (AssetDatabase.LoadMainAssetAtPath(SoundAsset) != null) files.Add(SoundAsset);
            if (files.Count == 0) { Debug.Log("TW CLEANUP: no old patch files left."); return; }
            string message = "BACKUP each file + .meta in project root /" + BackupParent + ".\n" +
                "DELETE only the following known PATCH files/folders from Assets (NOT original CandyCampfire or original camp materials):\n\n" +
                string.Join("\n", files.ToArray()) + "\n\nThis triggers Unity reimport/recompilation. " +
                "Only run after scene rollback. Other unopened scenes could still reference these files.";
            if (!EditorUtility.DisplayDialog("TW CLEANUP — old center scripts & generated assets", message,
                "Archive + delete only listed", "Cancel")) return;
            string destination = Path.Combine(ProjectRoot(), BackupParent,
                DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + "_CODE_AND_ASSETS");
            try
            {
                foreach (string path in files) CopyAssetToBackup(path, destination);
                foreach (string path in files)
                {
                    string error = AssetDatabase.DeleteAsset(path) ? "" : "Unity refused deletion: " + path;
                    if (error.Length > 0) Debug.LogError("TW CLEANUP: " + error);
                }
                AssetDatabase.Refresh();
                Debug.Log("TW CLEANUP: archived and deleted known patch files only. BACKUP: " + destination +
                    ". Original scene, Unity engine files and save games untouched. " +
                    "TWCenterEmergencyRollback.cs deliberately retained for diagnostics; remove it manually later if desired.");
            }
            catch (Exception ex)
            {
                Debug.LogError("TW CLEANUP failed; backup folder: " + destination + ". " + ex);
            }
        }

        [MenuItem(Menu + "4. Play Mode: временно погасить свет и FX костра", false, 503)]
        private static void PlayTest()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("TW diagnostic: ENTER Play Mode first. This is only an A/B test, not saved to scene.");
                return;
            }
            Scene scene = SceneManager.GetActiveScene();
            Transform camp = Find(scene, "Campfire");
            if (camp == null) { Debug.LogError("TW diagnostic: Campfire missing."); return; }
            var stage3 = camp.Find("Stage3");
            if (stage3 == null) { Debug.LogError("TW diagnostic: Stage3 missing."); return; }
            // Do not change original serialized assets; Play Mode values revert when exiting.
            int disabledLights = 0;
            Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (Light l in lights)
            {
                if (l.type == LightType.Directional || l.gameObject.scene != scene || !l.enabled) continue;
                if (Vector3.Distance(l.transform.position, camp.position) > 12f) continue;
                l.enabled = false;
                disabledLights++;
            }
            Transform flame = stage3.Find("Flames");
            if (flame != null) flame.gameObject.SetActive(false);
            foreach (string n in new [] { "Sparks", "Smoke", "Hearts" })
            {
                var t = stage3.Find(n);
                if (t != null) t.gameObject.SetActive(false);
            }
            Debug.Log("TW DIAGNOSTIC: temporarily disabled " + disabledLights +
                " nearby point/spot lights and original Campfire flame/particles IN PLAY MODE ONLY. " +
                "IF BLEACHING PERSISTS: likely another source (post-processing / scene lighting / material). " +
                "IF IT DISAPPEARS: investigate campfire emission, nearby Light or FireLightPool. " +
                "EXIT Play Mode to restore values. DO NOT SAVE Play Mode scene edits.");
        }

        private static bool Inspect(out Scene scene, out Transform camp, out Transform stage2, out Transform stage3, out string problem)
        {
            scene = SceneManager.GetActiveScene();
            camp = stage2 = stage3 = null;
            problem = null;
            if (EditorApplication.isPlayingOrWillChangePlaymode) { problem = "Exit Play Mode."; return false; }
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(scene.path))
            { problem = "Open and save an intended game scene."; return false; }
            camp = Find(scene, "Campfire");
            if (camp == null || camp.GetComponent<HutStages>() == null)
            { problem = "Expected unique root Campfire with HutStages. No modifications made."; return false; }
            stage2 = camp.Find("Stage2");
            stage3 = camp.Find("Stage3");
            if (stage2 == null || stage3 == null || stage3.Find("Flames") == null)
            { problem = "Expected Stage2, Stage3 and Stage3/Flames."; return false; }
            if (Find(scene, "IslandV2/_Generated/CampPlaza") == null)
            { problem = "Expected IslandV2/_Generated/CampPlaza; no other scene object will be edited."; return false; }
            return true;
        }

        private static void DescribeMesh(StringBuilder sb, Transform parent, string child, string target)
        {
            MeshFilter m = GetMesh(parent, child);
            sb.AppendLine(parent.name + "/" + child + ": current=" +
                (m == null ? "MISSING" : AssetDatabase.GetAssetPath(m.sharedMesh)) + " target=" + target);
        }
        private static bool KnownSource(MeshFilter mf, string original)
        {
            if (mf == null || mf.sharedMesh == null) return false;
            string path = AssetDatabase.GetAssetPath(mf.sharedMesh).Replace('\\', '/');
            if (path == original) return true;
            return path.StartsWith("Assets/_Project/Generated/ISL024C/", StringComparison.Ordinal) ||
                   path.StartsWith("Assets/_Project/Generated/TWCenterComplete/", StringComparison.Ordinal);
        }
        private static void Swap(MeshFilter f, Mesh asset)
        {
            if (f.sharedMesh == asset) return;
            Undo.RecordObject(f, "TW rollback to original campfire mesh asset");
            f.sharedMesh = asset;
            EditorUtility.SetDirty(f);
        }
        private static MeshFilter GetMesh(Transform parent, string child)
        {
            Transform tr = parent == null ? null : parent.Find(child);
            return tr == null ? null : tr.GetComponent<MeshFilter>();
        }
        private static Transform Find(Scene scene, string path)
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;
            string[] segs = path.Split('/');
            Transform result = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != segs[0]) continue;
                if (result != null) return null; // ambiguous duplicate root; refuse
                result = root.transform;
            }
            for (int i=1; i<segs.Length && result != null; i++) result = result.Find(segs[i]);
            return result;
        }
        private static string ProjectRoot() { return Directory.GetParent(Application.dataPath).FullName; }
        private static string Backup(Scene scene)
        {
            // Save edits to active scene so the backup includes even currently unsaved modifications.
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save current scene for backup.");
            string sceneFile = Path.Combine(ProjectRoot(), scene.path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(sceneFile)) throw new FileNotFoundException("Scene file missing on disk", sceneFile);
            string folder = Path.Combine(ProjectRoot(), BackupParent,
                DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + "_SCENE");
            Directory.CreateDirectory(folder);
            File.Copy(sceneFile, Path.Combine(folder, Path.GetFileName(sceneFile)), false);
            string manifest = "TW Center rollback backup (BEFORE)\nScene: " + scene.path +
                "\nUTC: " + DateTime.UtcNow.ToString("O") +
                "\nKeep this copy if you need to restore the whole scene.\n";
            File.WriteAllText(Path.Combine(folder, "README.txt"), manifest);
            return folder;
        }
        private static void CopyAssetToBackup(string path, string baseDir)
        {
            string project = ProjectRoot();
            string full = Path.Combine(project, path.Replace('/', Path.DirectorySeparatorChar));
            if (Directory.Exists(full))
            {
                foreach (string f in Directory.GetFiles(full, "*", SearchOption.AllDirectories))
                {
                    string relative = f.Substring(project.Length).TrimStart(Path.DirectorySeparatorChar);
                    string target = Path.Combine(baseDir, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    File.Copy(f, target, true);
                }
            }
            else if (File.Exists(full))
            {
                string target = Path.Combine(baseDir, path.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(full, target, true);
            }
            string meta = full + ".meta";
            if (File.Exists(meta))
            {
                string target = Path.Combine(baseDir, (path + ".meta").Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(meta, target, true);
            }
        }
    }
}
#endif
