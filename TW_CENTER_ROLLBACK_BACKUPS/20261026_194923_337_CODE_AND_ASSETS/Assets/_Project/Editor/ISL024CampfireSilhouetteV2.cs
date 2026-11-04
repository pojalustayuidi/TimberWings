#if UNITY_EDITOR
// Timber Wings / ISL-024B — stronger campfire hero silhouette, additive and reversible.
// Coexists with ISL024CampfireSilhouette.cs. Removes prior ISL-024 overlay objects on apply
// to avoid doubled decoration, but does not touch base Campfire stages / materials / lights.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace IslandV2
{
    public static class ISL024CampfireSilhouetteV2
    {
        private const string Menu = "Tools/BuilderSim/Локация/Остров v2/ISL-024B/";
        private const string ReportFile = "ISL-024B_CampfireHero_Audit.txt";
        private const string AssetFolder = "Assets/_Project/Generated/ISL024B";

        // New overlay names.
        private const string TripodName = "ISL024B_TripodHero";
        private const string LogsName = "ISL024B_CoreLogs";
        private const string FlameName = "ISL024B_FlameHero";
        private const string KettleName = "ISL024B_Kettle";
        // Old v1 overlay names, removed automatically during v2 apply.
        private const string OldTripod = "ISL024_TripodAccents";
        private const string OldLogs = "ISL024_LogsAccents";
        private const string OldFlame = "ISL024_FlameAccents";

        private struct Context
        {
            public Scene scene;
            public Transform campfire, stage2, stage3, flames;
            public Material campMaterial, flameMaterial;
        }

        [MenuItem(Menu + "1. Аудит костра v2 (без изменений)", false, 244)]
        private static void Audit()
        {
            var sb = new StringBuilder();
            sb.AppendLine("TIMBER WINGS — ISL-024B / CAMPFIRE HERO SILHOUETTE AUDIT (READ ONLY)");
            sb.AppendLine("UTC: " + DateTime.UtcNow.ToString("O"));
            sb.AppendLine("Unity: " + Application.unityVersion);
            Scene scene = SceneManager.GetActiveScene();
            sb.AppendLine("Active scene: " + scene.path);
            sb.AppendLine("Recovery scene: " + (scene.path.Contains("_Recovery") ? "YES — verify main scene and backup" : "no"));
            sb.AppendLine("No scene, mesh, collider, material, save or light will be modified by this audit.");
            Context c;
            string problem;
            if (!TryContext(out c, out problem))
            {
                sb.AppendLine("PRECONDITIONS FAILED: " + problem);
            }
            else
            {
                sb.AppendLine("PRECONDITIONS: OK");
                sb.AppendLine("Campfire: " + PathOf(c.campfire));
                sb.AppendLine("Stage2: " + PathOf(c.stage2));
                sb.AppendLine("Stage3: " + PathOf(c.stage3));
                sb.AppendLine("Flames: " + PathOf(c.flames));
                sb.AppendLine("Camp material: " + c.campMaterial.name + " / " + c.campMaterial.shader.name);
                sb.AppendLine("Flame material: " + c.flameMaterial.name + " / " + c.flameMaterial.shader.name);
                sb.AppendLine("Existing old v1 overlay: tripod=" + (c.stage2.Find(OldTripod) != null) +
                              ", logs=" + (c.stage3.Find(OldLogs) != null) +
                              ", flame=" + (c.flames.Find(OldFlame) != null));
                sb.AppendLine("Existing v2 overlay: tripod=" + (c.stage2.Find(TripodName) != null) +
                              ", kettle=" + (c.stage2.Find(KettleName) != null) +
                              ", logs=" + (c.stage3.Find(LogsName) != null) +
                              ", flame=" + (c.flames.Find(FlameName) != null));
                sb.AppendLine("APPLY will CREATE/REPLACE ONLY:");
                sb.AppendLine(" - Campfire/Stage2/" + TripodName);
                sb.AppendLine(" - Campfire/Stage2/" + KettleName);
                sb.AppendLine(" - Campfire/Stage3/" + LogsName);
                sb.AppendLine(" - Campfire/Stage3/Flames/" + FlameName);
                sb.AppendLine("APPLY will ALSO REMOVE only previous overlay children if found: " + OldTripod + ", " + OldLogs + ", " + OldFlame + ".");
                sb.AppendLine("No collider, runtime script, extra light, texture or material asset will be created.");
                sb.AppendLine("No full Campfire / island rebuild is part of ISL-024B.");
            }
            string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, ReportFile);
            try { File.WriteAllText(path, sb.ToString(), Encoding.UTF8); }
            catch (Exception ex) { Debug.LogWarning("ISL-024B: could not write report: " + ex.Message); }
            Debug.Log(sb.ToString() + "\nReport: " + path);
            if (!string.IsNullOrEmpty(problem)) Debug.LogWarning("ISL-024B: fix preconditions before Apply.");
        }

        [MenuItem(Menu + "2. Применить усиленный силуэт v2", false, 245)]
        private static void Apply()
        {
            Context c;
            string error;
            if (!TryContext(out c, out error))
            {
                Debug.LogError("ISL-024B: APPLY ABORTED. " + error);
                return;
            }
            string msg =
                "SCENE: " + c.scene.path + "\n\n" +
                "Will CREATE or REPLACE EXACTLY these children:\n" +
                " Campfire/Stage2/" + TripodName + "\n" +
                " Campfire/Stage2/" + KettleName + "\n" +
                " Campfire/Stage3/" + LogsName + "\n" +
                " Campfire/Stage3/Flames/" + FlameName + "\n\n" +
                "Will DELETE ONLY prior overlay children if present:\n" +
                " Campfire/Stage2/" + OldTripod + "\n" +
                " Campfire/Stage3/" + OldLogs + "\n" +
                " Campfire/Stage3/Flames/" + OldFlame + "\n\n" +
                "Creates/updates ONLY four named mesh assets in " + AssetFolder + ".\n" +
                "Will NOT alter base Campfire meshes, HutStages, CampfireFlicker, colliders, lights, materials, save IDs, scene geography, NPCs or paths.\n\n" +
                "This will MODIFY the open scene. Save backup / Git checkpoint first.";
            if (c.scene.path.Contains("_Recovery"))
                msg = "WARNING: ACTIVE SCENE IS IN _RECOVERY. Continue only on an intentional backed-up scene.\n\n" + msg;
            if (!EditorUtility.DisplayDialog("ISL-024B — confirm exact scope", msg, "Apply to THIS scene", "Cancel"))
                return;

            Mesh tri = MakeTripodMesh();
            Mesh logs = MakeLogMesh();
            Mesh flame = MakeFlameMesh();
            Mesh kettle = MakeKettleMesh();
            if (!ValidMesh(tri) || !ValidMesh(logs) || !ValidMesh(flame) || !ValidMesh(kettle))
            {
                DestroyTemporary(tri, logs, flame, kettle);
                Debug.LogError("ISL-024B: mesh generation failed. Scene untouched.");
                return;
            }

            Mesh triAsset = null, logsAsset = null, flameAsset = null, kettleAsset = null;
            try
            {
                EnsureFolders();
                triAsset = PersistMesh(AssetFolder + "/ISL024B_TripodHeroMesh.asset", tri);
                logsAsset = PersistMesh(AssetFolder + "/ISL024B_CoreLogsMesh.asset", logs);
                flameAsset = PersistMesh(AssetFolder + "/ISL024B_FlameHeroMesh.asset", flame);
                kettleAsset = PersistMesh(AssetFolder + "/ISL024B_KettleMesh.asset", kettle);
                AssetDatabase.SaveAssetIfDirty(triAsset);
                AssetDatabase.SaveAssetIfDirty(logsAsset);
                AssetDatabase.SaveAssetIfDirty(flameAsset);
                AssetDatabase.SaveAssetIfDirty(kettleAsset);
            }
            catch (Exception ex)
            {
                Debug.LogError("ISL-024B: mesh asset write failed; scene unchanged. " + ex);
                return;
            }
            finally { DestroyTemporary(tri, logs, flame, kettle); }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("ISL-024B: stronger campfire silhouette");
            try
            {
                RemoveChildIfExists(c.stage2, OldTripod);
                RemoveChildIfExists(c.stage3, OldLogs);
                RemoveChildIfExists(c.flames, OldFlame);
                ReplaceChild(c.stage2, TripodName, triAsset, c.campMaterial, true);
                ReplaceChild(c.stage2, KettleName, kettleAsset, c.campMaterial, true);
                ReplaceChild(c.stage3, LogsName, logsAsset, c.campMaterial, true);
                ReplaceChild(c.flames, FlameName, flameAsset, c.flameMaterial, false);
                Undo.CollapseUndoOperations(group);
                EditorSceneManager.MarkSceneDirty(c.scene);
                Debug.Log("ISL-024B: applied stronger hero silhouette. Created/replaced 4 overlay objects; removed prior v1 overlay if present. Base Campfire/lights/colliders unchanged.");
            }
            catch (Exception ex)
            {
                Undo.RevertAllDownToGroup(group);
                Debug.LogError("ISL-024B: scene edit failed and was undone. " + ex);
            }
        }

        [MenuItem(Menu + "3. Убрать только v2-декор", false, 246)]
        private static void Remove()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("ISL-024B: exit Play Mode before removing decorations.");
                return;
            }
            Scene scene = SceneManager.GetActiveScene();
            Transform root = FindUniqueCampfire(scene);
            if (root == null) { Debug.LogWarning("ISL-024B: unique Campfire root not found."); return; }
            Transform stage2 = root.Find("Stage2");
            Transform stage3 = root.Find("Stage3");
            Transform flames = stage3 != null ? stage3.Find("Flames") : null;
            Transform a = stage2 != null ? stage2.Find(TripodName) : null;
            Transform b = stage2 != null ? stage2.Find(KettleName) : null;
            Transform c = stage3 != null ? stage3.Find(LogsName) : null;
            Transform d = flames != null ? flames.Find(FlameName) : null;
            if (a == null && b == null && c == null && d == null) { Debug.Log("ISL-024B: no v2 overlays to remove."); return; }
            if (!EditorUtility.DisplayDialog("ISL-024B — remove only v2 overlays",
                "Will DELETE ONLY the existing ISL-024B overlay children under Campfire/Stage2, Stage3 and Flames.\n" +
                "Generated mesh assets in " + AssetFolder + " remain for reapply/undo.\nNo other object or asset is touched.", "Remove overlays", "Cancel")) return;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("ISL-024B: remove overlays");
            if (a != null) Undo.DestroyObjectImmediate(a.gameObject);
            if (b != null) Undo.DestroyObjectImmediate(b.gameObject);
            if (c != null) Undo.DestroyObjectImmediate(c.gameObject);
            if (d != null) Undo.DestroyObjectImmediate(d.gameObject);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("ISL-024B: only v2 overlay objects removed. Generated mesh assets retained at " + AssetFolder);
        }

        private static bool TryContext(out Context c, out string issue)
        {
            c = new Context();
            issue = null;
            if (EditorApplication.isPlayingOrWillChangePlaymode) { issue = "Stop Play Mode."; return false; }
            c.scene = SceneManager.GetActiveScene();
            if (!c.scene.IsValid() || !c.scene.isLoaded || string.IsNullOrEmpty(c.scene.path))
            { issue = "Open and save the intended game scene."; return false; }
            c.campfire = FindUniqueCampfire(c.scene);
            if (c.campfire == null) { issue = "Expected exactly one Campfire ROOT (CandyCampfire-generated)."; return false; }
            if (c.campfire.GetComponent<HutStages>() == null)
            { issue = "Campfire has no HutStages."; return false; }
            c.stage2 = c.campfire.Find("Stage2");
            c.stage3 = c.campfire.Find("Stage3");
            c.flames = c.stage3 != null ? c.stage3.Find("Flames") : null;
            if (c.stage2 == null || c.stage3 == null || c.flames == null)
            { issue = "Campfire/Stage2, Stage3 or Stage3/Flames is missing."; return false; }
            Transform tripodMesh = c.stage2.Find("Mesh_Solid");
            Transform fireMesh = c.flames.Find("Mesh_Flame");
            if (tripodMesh == null || fireMesh == null ||
                tripodMesh.GetComponent<MeshFilter>() == null ||
                fireMesh.GetComponent<MeshFilter>() == null ||
                tripodMesh.GetComponent<MeshFilter>().sharedMesh == null ||
                fireMesh.GetComponent<MeshFilter>().sharedMesh == null)
            { issue = "CandyCampfire source meshes unavailable."; return false; }
            MeshRenderer tripodRenderer = tripodMesh.GetComponent<MeshRenderer>();
            MeshRenderer flameRenderer = fireMesh.GetComponent<MeshRenderer>();
            if (tripodRenderer == null || flameRenderer == null ||
                tripodRenderer.sharedMaterial == null || flameRenderer.sharedMaterial == null)
            { issue = "Original CandyCampfire materials unavailable."; return false; }
            if (c.stage3.GetComponent<CampfireFlicker>() == null)
            { issue = "CampfireFlicker missing from Stage3."; return false; }
            if (!NearlyIdentity(c.stage2) || !NearlyIdentity(c.stage3) ||
                Mathf.Abs(c.flames.localPosition.x) > 0.02f ||
                Mathf.Abs(c.flames.localPosition.y - 0.22f) > 0.04f ||
                Mathf.Abs(c.flames.localPosition.z) > 0.02f ||
                Quaternion.Angle(c.flames.localRotation, Quaternion.identity) > 0.1f ||
                (c.flames.localScale - Vector3.one).sqrMagnitude > 0.0001f)
            { issue = "Original stage/flame transforms differ from the audited generator. No patch applied."; return false; }
            c.campMaterial = tripodRenderer.sharedMaterial;
            c.flameMaterial = flameRenderer.sharedMaterial;
            if (!AssetDatabase.IsValidFolder("Assets/_Project"))
            { issue = "Assets/_Project missing."; return false; }
            return true;
        }

        private static bool NearlyIdentity(Transform t)
        {
            return t.localPosition.sqrMagnitude < 0.0001f &&
                   (t.localScale - Vector3.one).sqrMagnitude < 0.0001f &&
                   Quaternion.Angle(t.localRotation, Quaternion.identity) < 0.1f;
        }

        private static Transform FindUniqueCampfire(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;
            Transform found = null;
            foreach (GameObject obj in scene.GetRootGameObjects())
            {
                if (obj.name != "Campfire") continue;
                if (found != null) return null;
                found = obj.transform;
            }
            return found;
        }

        private static string PathOf(Transform t)
        {
            string path = t.name;
            for (Transform p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
            return path;
        }

        private static void RemoveChildIfExists(Transform parent, string name)
        {
            Transform prev = parent.Find(name);
            if (prev != null) Undo.DestroyObjectImmediate(prev.gameObject);
        }

        private static void ReplaceChild(Transform parent, string name, Mesh mesh, Material mat, bool castShadows)
        {
            RemoveChildIfExists(parent, name);
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "ISL-024B: create " + name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            var mf = Undo.AddComponent<MeshFilter>(go);
            mf.sharedMesh = mesh;
            var mr = Undo.AddComponent<MeshRenderer>(go);
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            mr.receiveShadows = castShadows;
        }

        private static bool ValidMesh(Mesh m) => m != null && m.vertexCount >= 3;

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Generated"))
                AssetDatabase.CreateFolder("Assets/_Project", "Generated");
            if (!AssetDatabase.IsValidFolder(AssetFolder))
                AssetDatabase.CreateFolder("Assets/_Project/Generated", "ISL024B");
        }

        private static Mesh PersistMesh(string path, Mesh source)
        {
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (old == null)
            {
                var other = AssetDatabase.LoadMainAssetAtPath(path);
                if (other != null) throw new InvalidOperationException("Non-Mesh asset already occupies " + path);
                Mesh copy = UnityEngine.Object.Instantiate(source);
                copy.name = Path.GetFileNameWithoutExtension(path);
                AssetDatabase.CreateAsset(copy, path);
                return copy;
            }
            Undo.RecordObject(old, "ISL-024B: refresh generated mesh");
            EditorUtility.CopySerialized(source, old);
            old.name = Path.GetFileNameWithoutExtension(path);
            EditorUtility.SetDirty(old);
            return old;
        }

        private static void DestroyTemporary(params Mesh[] meshes)
        {
            foreach (var m in meshes) if (m != null) UnityEngine.Object.DestroyImmediate(m);
        }

        private static Color C(string hex)
        {
            Color col;
            return ColorUtility.TryParseHtmlString(hex, out col) ? col.linear : Color.white;
        }

        private static Mesh MakeTripodMesh()
        {
            var b = new MeshBuilder();
            Color darkWood = C("#774630"), warmWood = C("#B57B52"), wrap = C("#F2D4A4");
            Vector3 apex = new Vector3(0f, 2.82f, 0f);
            for (int i = 0; i < 3; i++)
            {
                float a = (90f + i * 120f) * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 foot = dir * 1.41f;
                Vector3 end = apex + dir * 0.06f;
                // Stronger visible wood mass along each leg.
                b.Rod(Vector3.Lerp(foot, end, 0.03f), Vector3.Lerp(foot, end, 0.80f), 0.105f, 0.075f, darkWood, warmWood, 8);
                // Top binding bundle.
                b.Rod(Vector3.Lerp(foot, end, 0.86f), Vector3.Lerp(foot, end, 0.93f), 0.10f, 0.092f, wrap, wrap, 8);
                // Mid wrap for rhythm.
                b.Rod(Vector3.Lerp(foot, end, 0.56f), Vector3.Lerp(foot, end, 0.62f), 0.078f, 0.074f, wrap, wrap, 8);
            }
            return b.Create("ISL024B_TripodHero");
        }

        private static Mesh MakeLogMesh()
        {
            var b = new MeshBuilder();
            Color logA = C("#8D563C"), logB = C("#714435"), cap = C("#F5DAB8");
            // Four inward-leaning support logs for clearer fire core.
            for (int i = 0; i < 4; i++)
            {
                float a = (i * 90f + 20f) * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 p0 = dir * 0.56f + Vector3.up * 0.10f;
                Vector3 p1 = dir * 0.16f + Vector3.up * 0.34f;
                b.Rod(p0, p1, 0.085f, 0.060f, (i % 2 == 0) ? logA : logB, cap, 8);
            }
            // One short cross piece near center.
            b.Rod(new Vector3(-0.24f, 0.18f, 0.02f), new Vector3(0.24f, 0.24f, -0.02f), 0.05f, 0.045f, logB, cap, 7);
            return b.Create("ISL024B_CoreLogs");
        }

        private static Mesh MakeFlameMesh()
        {
            var b = new MeshBuilder();
            Color baseColor = C("#FF7A33"), middle = C("#FFC15C"), tip = C("#FFF4BF");
            // Taller central tongue.
            b.Flame(Vector3.up * (-0.10f), new Vector3(0.00f, 0f, 0.02f), 1.08f, 0.15f, baseColor, middle, tip, 8);
            // Three surrounding tongues.
            for (int i = 0; i < 3; i++)
            {
                float a = (i * 120f + 35f) * Mathf.Deg2Rad;
                Vector3 outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float h = i == 0 ? 0.82f : (i == 1 ? 0.72f : 0.78f);
                b.Flame(outward * 0.22f + Vector3.up * (-0.10f), outward * 0.06f, h, 0.10f, baseColor, middle, tip, 7);
            }
            return b.Create("ISL024B_FlameHero");
        }

        private static Mesh MakeKettleMesh()
        {
            var b = new MeshBuilder();
            Color body = C("#8E8A7A"), rim = C("#DCCFB5"), dark = C("#6C655C");
            Vector3 center = new Vector3(0f, 2.00f, 0f);
            // Body: squat pot by stacked rings.
            b.Lathe(center + Vector3.up * -0.10f, 0.28f, 0.18f, 9, body, body);
            b.Lathe(center + Vector3.up *  0.05f, 0.33f, 0.08f, 9, body, body);
            b.Lathe(center + Vector3.up *  0.13f, 0.26f, 0.08f, 9, body, body);
            // Lid / knob.
            b.Lathe(center + Vector3.up * 0.22f, 0.18f, 0.03f, 9, rim, rim);
            b.Lathe(center + Vector3.up * 0.27f, 0.05f, 0.05f, 7, dark, dark);
            // Handle arch.
            b.ArcRod(center + new Vector3(0f, 0.10f, 0f), 0.29f, 0.34f, 0.03f, dark, 8, 10);
            // Hanging line.
            b.Rod(new Vector3(0f, 2.68f, 0f), new Vector3(0f, 2.38f, 0f), 0.018f, 0.018f, rim, rim, 6);
            return b.Create("ISL024B_Kettle");
        }

        private sealed class MeshBuilder
        {
            private readonly List<Vector3> v = new List<Vector3>();
            private readonly List<Color> colors = new List<Color>();
            private readonly List<int> t = new List<int>();

            private int Add(Vector3 p, Color c) { int i = v.Count; v.Add(p); colors.Add(c); return i; }
            private void Tri(int a, int b, int c) { t.Add(a); t.Add(b); t.Add(c); }

            public void Rod(Vector3 bottom, Vector3 top, float radiusBottom, float radiusTop, Color side, Color end, int n)
            {
                Vector3 axis = (top - bottom).normalized;
                Vector3 u = Vector3.Cross(axis, Vector3.forward);
                if (u.sqrMagnitude < 0.001f) u = Vector3.Cross(axis, Vector3.right);
                u.Normalize();
                Vector3 w = Vector3.Cross(axis, u).normalized;
                int[] lower = new int[n], upper = new int[n];
                for (int j = 0; j < n; j++)
                {
                    float a = 2f * Mathf.PI * j / n;
                    Vector3 radial = u * Mathf.Cos(a) + w * Mathf.Sin(a);
                    lower[j] = Add(bottom + radial * radiusBottom, side);
                    upper[j] = Add(top + radial * radiusTop, side);
                }
                for (int j = 0; j < n; j++)
                {
                    int q = (j + 1) % n;
                    Tri(lower[j], lower[q], upper[j]);
                    Tri(lower[q], upper[q], upper[j]);
                }
                int lo = Add(bottom, end), hi = Add(top, end);
                int[] capLo = new int[n], capHi = new int[n];
                for (int j = 0; j < n; j++)
                {
                    float a = 2f * Mathf.PI * j / n;
                    Vector3 radial = u * Mathf.Cos(a) + w * Mathf.Sin(a);
                    capLo[j] = Add(bottom + radial * radiusBottom, end);
                    capHi[j] = Add(top + radial * radiusTop, end);
                }
                for (int j = 0; j < n; j++)
                {
                    int q = (j + 1) % n;
                    Tri(lo, capLo[q], capLo[j]);
                    Tri(hi, capHi[j], capHi[q]);
                }
            }

            public void Flame(Vector3 basePoint, Vector3 lean, float height, float r, Color baseCol, Color midCol, Color tipCol, int n)
            {
                int[][] ring = new int[3][];
                float[] yy = { 0f, height * 0.36f, height * 0.74f };
                float[] rr = { r * 0.72f, r * 1.05f, r * 0.42f };
                Color[] cc = { baseCol, midCol, tipCol };
                for (int level = 0; level < 3; level++)
                {
                    ring[level] = new int[n];
                    Vector3 c = basePoint + Vector3.up * yy[level] + lean * (yy[level] / Mathf.Max(0.001f, height));
                    for (int j = 0; j < n; j++)
                    {
                        float a = 2f * Mathf.PI * j / n;
                        ring[level][j] = Add(c + new Vector3(Mathf.Cos(a) * rr[level], 0f, Mathf.Sin(a) * rr[level]), cc[level]);
                    }
                }
                for (int level = 0; level < 2; level++)
                {
                    for (int j = 0; j < n; j++)
                    {
                        int q = (j + 1) % n;
                        Tri(ring[level][j], ring[level + 1][j], ring[level][q]);
                        Tri(ring[level][q], ring[level + 1][j], ring[level + 1][q]);
                    }
                }
                int tip = Add(basePoint + Vector3.up * height + lean, tipCol);
                for (int j = 0; j < n; j++)
                {
                    int q = (j + 1) % n;
                    Tri(ring[2][j], tip, ring[2][q]);
                }
            }

            public void Lathe(Vector3 center, float radius, float height, int n, Color side, Color end)
            {
                Rod(center + Vector3.up * (-height * 0.5f), center + Vector3.up * (height * 0.5f), radius, radius, side, end, n);
            }

            public void ArcRod(Vector3 center, float radius, float archHeight, float rodRadius, Color col, int radialSegments, int arcSegments)
            {
                Vector3 prev = center + new Vector3(-radius, 0f, 0f);
                for (int i = 1; i <= arcSegments; i++)
                {
                    float t01 = i / (float)arcSegments;
                    float a = Mathf.Lerp(Mathf.PI, 0f, t01);
                    Vector3 p = center + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(t01 * Mathf.PI) * archHeight, 0f);
                    Rod(prev, p, rodRadius, rodRadius, col, col, radialSegments);
                    prev = p;
                }
            }

            public Mesh Create(string name)
            {
                if (v.Count < 3 || t.Count == 0) return null;
                Mesh m = new Mesh { name = name };
                m.SetVertices(v);
                m.SetColors(colors);
                m.SetTriangles(t, 0, true);
                m.RecalculateNormals();
                m.RecalculateBounds();
                return m;
            }
        }
    }
}
#endif
