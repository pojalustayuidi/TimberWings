#if UNITY_EDITOR
// Timber Wings / CENTER COMPLETE v1.0 HOTFIX
// Safe optional muting of ONLY added glow FX + precise one-expression fix for ChopGrove NaN.
// No world regeneration, no edits to base fire/lighting/terrain/IDs/physics.
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IslandV2
{
    public static class TW_CenterCompleteHotfix
    {
        const string Menu = "Tools/BuilderSim/Локация/Остров v2/ЦЕНТР — HOTFIX/";
        const string GroveAsset = "Assets/_Project/Scripts/Forest/ChopGrove/ChopGroveBuilder.Props.cs";
        const string OldExpr = "Mathf.Pow(Mathf.Sin(t * Mathf.PI), 0.75f)";
        const string NewExpr = "Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * Mathf.PI)), 0.75f)";
        const string FXRootName = "TW_CenterComplete_Atmosphere";
        const string EmberChild = "01_WarmEmbersAtGround";
        const string MotesChild = "03_RareAmberMotes";
        const string ExternalReport = "TW_CENTER_HOTFIX_Audit.txt";
        const string BackupsDir = "TW_CenterHotfix_Backups";

        [MenuItem(Menu + "1. Диагностика (ничего не менять)", false, 350)]
        static void Audit()
        {
            var sb = new StringBuilder();
            sb.AppendLine("TIMBER WINGS — CENTER COMPLETE HOTFIX / READ ONLY");
            sb.AppendLine("UTC: " + DateTime.UtcNow.ToString("O"));
            sb.AppendLine("Unity: " + Application.unityVersion);
            Scene scene = SceneManager.GetActiveScene();
            sb.AppendLine("Scene: " + scene.path);
            sb.AppendLine("Scene in _Recovery: " + scene.path.Contains("_Recovery"));
            Transform fx;
            string reason;
            if (TryFindFX(out fx, out reason))
            {
                sb.AppendLine("Added atmosphere: " + PathOf(fx));
                var ember = fx.Find(EmberChild);
                var motes = fx.Find(MotesChild);
                sb.AppendLine("Added ember glow exists: " + (ember != null) + "; activeSelf=" + (ember != null && ember.gameObject.activeSelf));
                sb.AppendLine("Added motes exists: " + (motes != null) + "; activeSelf=" + (motes != null && motes.gameObject.activeSelf));
                sb.AppendLine("Added 3D audio will be left unchanged.");
            }
            else sb.AppendLine("Added FX not found: " + reason);
            string status = GroovePatchStatus();
            sb.AppendLine("ChopGrove formula: " + status);
            sb.AppendLine("This audit does NOT change scene objects, assets, meshes, colliders, lighting, project scripts or saves.");
            sb.AppendLine("No Play Mode validation is implied.");
            string dst = Path.Combine(Directory.GetParent(Application.dataPath).FullName, ExternalReport);
            try { File.WriteAllText(dst, sb.ToString(), new UTF8Encoding(false)); }
            catch (Exception ex) { Debug.LogWarning("Center HOTFIX: unable to write TXT: " + ex.Message); }
            Debug.Log(sb.ToString() + "\nReport: " + dst);
        }

        [MenuItem(Menu + "2. Убрать пересвет (только новый glow/частицы)", false, 351)]
        static void FixGlow() { MuteFX(true); }

        [MenuItem(Menu + "3. Исправить NaN арки ChopGrove (точечно)", false, 352)]
        static void FixGrove() { PatchGrove(true); }

        [MenuItem(Menu + "4. ОБА исправления (рекомендуется)", false, 353)]
        static void FixBoth()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { Debug.LogError("Center HOTFIX: exit Play Mode before editing."); return; }
            if (!GrovePatchable())
            {
                Debug.LogError("Center HOTFIX: ChopGrove source is missing/changed. Run audit; no action taken.");
                return;
            }
            Transform fx; string reason;
            if (!TryFindFX(out fx, out reason))
            {
                Debug.LogWarning("Center HOTFIX: no Complete v1 atmosphere in scene; fixing source only. " + reason);
            }
            else if ((fx.Find(EmberChild) != null || fx.Find(MotesChild) != null))
            {
                if (!EditorUtility.DisplayDialog("CENTER HOTFIX — precise scope",
                    "Scene: " + SceneManager.GetActiveScene().path + "\n\n" +
                    "DISABLE ONLY (no deletion):\n" +
                    " Campfire/Stage3/" + FXRootName + "/" + EmberChild + "\n" +
                    " Campfire/Stage3/" + FXRootName + "/" + MotesChild + "\n\n" +
                    "PATCH ONLY ONE EXPRESSION in:\n" + GroveAsset + "\n" +
                    "Original script copied outside Assets BEFORE patch. Existing .meta/GUID kept.\n" +
                    "Existing smoke, sparks, fire, audio, lights, gameplay/save IDs, meshes and colliders stay untouched.\n\n" +
                    "Backup the scene and finish Play Mode first.", "Apply hotfix", "Cancel")) return;
                SetFX(fx, false);
            }
            PatchGrove(false);
        }

        [MenuItem(Menu + "5. Вернуть дополнительные эффекты (для A/B)", false, 354)]
        static void RestoreGlow() { MuteFX(false); }

        static bool MuteFX(bool disable)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { Debug.LogError("Center HOTFIX: exit Play Mode first."); return false; }
            Transform fx; string issue;
            if (!TryFindFX(out fx, out issue))
            { Debug.LogWarning("Center HOTFIX: nothing changed. " + issue); return false; }
            if (!EditorUtility.DisplayDialog("CENTER HOTFIX: " + (disable ? "mute glow" : "restore added glow"),
                "Scene: " + SceneManager.GetActiveScene().path + "\n\n" +
                "Only set activeSelf=" + (!disable) + " for:\n" +
                FXRootName + "/" + EmberChild + "\n" + FXRootName + "/" + MotesChild + "\n\n" +
                "No other object/material/light/asset touched. Audio and existing fire/sparks/smoke remain.\n" +
                (disable ? "This is the recommended safe visual test." : "WARNING: overexposure may return."),
                disable ? "Mute only added FX" : "Enable added FX", "Cancel")) return false;
            SetFX(fx, !disable);
            return true;
        }

        static void SetFX(Transform fx, bool enabled)
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("TW Center hotfix: " + (enabled ? "restore added FX" : "mute added FX"));
            int changed = 0;
            foreach (string childName in new[] { EmberChild, MotesChild })
            {
                Transform child = fx.Find(childName);
                if (child == null) continue;
                if (child.gameObject.activeSelf == enabled) continue;
                Undo.RecordObject(child.gameObject, "Center hotfix: toggle " + childName);
                child.gameObject.SetActive(enabled);
                EditorUtility.SetDirty(child.gameObject);
                changed++;
            }
            Undo.CollapseUndoOperations(group);
            if (changed > 0) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("Center HOTFIX: " + changed + " Complete-owned extra FX nodes " +
                      (enabled ? "enabled" : "disabled") + ". Original fire effects / audio untouched.");
        }

        static bool TryFindFX(out Transform fx, out string issue)
        {
            fx = null; issue = null;
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            { issue = "Active scene unavailable."; return false; }
            Transform fire = null;
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                if (go.name != "Campfire") continue;
                if (fire != null)
                { issue = "Multiple Campfire roots; refuse changes."; return false; }
                fire = go.transform;
            }
            if (fire == null) { issue = "Campfire root absent."; return false; }
            fx = fire.Find("Stage3/" + FXRootName);
            if (fx == null) { issue = "No CENTER COMPLETE v1 atmosphere under Campfire/Stage3."; return false; }
            return true;
        }

        static string GroovePatchStatus()
        {
            string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, GroveAsset);
            if (!File.Exists(path)) return "SOURCE MISSING: " + GroveAsset;
            byte[] bytes = File.ReadAllBytes(path);
            int orig = Count(bytes, Encoding.ASCII.GetBytes(OldExpr));
            int fixedCount = Count(bytes, Encoding.ASCII.GetBytes(NewExpr));
            if (orig == 1 && fixedCount == 0) return "OLD vulnerable expression found once; safe to patch";
            if (orig == 0 && fixedCount == 1) return "ALREADY PATCHED";
            return "UNEXPECTED CODE: old=" + orig + ", fixed=" + fixedCount + "; manual inspection needed";
        }

        static bool GrovePatchable()
        {
            string status = GroovePatchStatus();
            return status.StartsWith("OLD vulnerable") || status == "ALREADY PATCHED";
        }

        static void PatchGrove(bool prompt)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { Debug.LogError("Center HOTFIX: exit Play Mode first."); return; }
            string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, GroveAsset);
            if (!File.Exists(path)) { Debug.LogError("Center HOTFIX: source not found: " + GroveAsset); return; }
            byte[] old = File.ReadAllBytes(path);
            byte[] pattern = Encoding.ASCII.GetBytes(OldExpr);
            byte[] replacement = Encoding.ASCII.GetBytes(NewExpr);
            int matchCount = Count(old, pattern);
            int already = Count(old, replacement);
            if (matchCount == 0 && already == 1)
            { Debug.Log("Center HOTFIX: ChopGrove formula already safe; no file changes."); return; }
            if (matchCount != 1 || already != 0)
            {
                Debug.LogError("Center HOTFIX: source changed or ambiguous, old matches=" + matchCount +
                               ", new matches=" + already + ". No file modified."); return;
            }
            if (prompt && !EditorUtility.DisplayDialog("CENTER HOTFIX — fix ChopGrove NaN",
                "Will change ONLY:\n" + GroveAsset + "\n\nFROM:\n" + OldExpr + "\n\nTO:\n" + NewExpr +
                "\n\nOriginal byte-identical .cs backup is saved outside Assets. .meta and GUID unchanged. Unity recompiles afterwards.\n" +
                "No scene or generator command is executed.", "Create backup and patch", "Cancel")) return;
            int index = IndexOf(old, pattern, 0);
            if (index < 0) { Debug.LogError("Center HOTFIX: expression not found."); return; }
            string project = Directory.GetParent(Application.dataPath).FullName;
            string backups = Path.Combine(project, BackupsDir);
            try
            {
                Directory.CreateDirectory(backups);
                string target = Path.Combine(backups,
                    "ChopGroveBuilder.Props_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + ".cs.bak");
                File.Copy(path, target, false);
                byte[] updated = new byte[old.Length - pattern.Length + replacement.Length];
                Buffer.BlockCopy(old, 0, updated, 0, index);
                Buffer.BlockCopy(replacement, 0, updated, index, replacement.Length);
                int end = index + pattern.Length;
                Buffer.BlockCopy(old, end, updated, index + replacement.Length, old.Length - end);
                File.WriteAllBytes(path, updated);
                Debug.Log("Center HOTFIX: fixed ChopGrove Gate arc NaN with one expression change. " +
                          "Byte-identical .cs backup outside Assets: " + target + ". Existing .meta/GUID unchanged. Unity recompiles now.");
                AssetDatabase.ImportAsset(GroveAsset, ImportAssetOptions.ForceUpdate);
            }
            catch (Exception ex)
            { Debug.LogError("Center HOTFIX: patch failed; inspect backup and source before retrying. " + ex); }
        }

        static string PathOf(Transform tr)
        {
            string s = tr.name;
            for (Transform p = tr.parent; p != null; p = p.parent) s = p.name + "/" + s;
            return s;
        }
        static int Count(byte[] haystack, byte[] needle)
        {
            int result = 0;
            for (int from = 0; from <= haystack.Length - needle.Length;)
            {
                int at = IndexOf(haystack, needle, from);
                if (at < 0) break;
                result++;
                from = at + needle.Length;
            }
            return result;
        }
        static int IndexOf(byte[] haystack, byte[] needle, int start)
        {
            for (int i = start; i <= haystack.Length - needle.Length; i++)
            {
                int j = 0;
                while (j < needle.Length && haystack[i + j] == needle[j]) j++;
                if (j == needle.Length) return i;
            }
            return -1;
        }
    }
}
#endif
