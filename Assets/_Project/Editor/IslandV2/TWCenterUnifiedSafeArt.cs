#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

namespace IslandV2
{
    /// <summary>
    /// Timber Wings — one safe, unified decorative center pass.
    /// Adds a calm art layer only: no lights, no particles, no colliders, no runtime scripts.
    /// Uses existing island material with vertex colors so the center stays in the same visual language.
    /// </summary>
    public static class TWCenterUnifiedSafeArt
    {
        private const string Menu = "Tools/BuilderSim/Локация/Остров v2/ЦЕНТР — UNIFIED SAFE/";
        private const string OverlayName = "TW_CenterUnified";
        private const string MeshAssetPath = "Assets/_Project/Generated/TWCenterUnified/TW_CenterUnifiedMesh.asset";
        private const string ReportFile = "TW_CenterUnified_Audit.txt";

        private struct Context
        {
            public Scene scene;
            public Transform campPlaza;
            public Transform campfire;
            public Material sharedMaterial;
            public Vector3 center;
            public float plazaRadius;
            public string notes;
        }

        [MenuItem(Menu + "1. Аудит центра (без изменений)", false, 300)]
        private static void Audit()
        {
            Context c;
            string issue;
            bool ok = TryContext(out c, out issue);

            var sb = new StringBuilder();
            sb.AppendLine("TIMBER WINGS — CENTER UNIFIED SAFE / READ ONLY AUDIT");
            sb.AppendLine("UTC: " + DateTime.UtcNow.ToString("O"));
            sb.AppendLine("Unity: " + Application.unityVersion);
            Scene scene = SceneManager.GetActiveScene();
            sb.AppendLine("Scene: " + scene.path);
            sb.AppendLine("Scene in _Recovery: " + scene.path.Contains("_Recovery"));
            if (!ok)
            {
                sb.AppendLine("PRECONDITIONS FAILED: " + issue);
            }
            else
            {
                sb.AppendLine("PRECONDITIONS: OK");
                sb.AppendLine("CampPlaza: " + PathOf(c.campPlaza));
                sb.AppendLine("Campfire: " + (c.campfire != null ? PathOf(c.campfire) : "not found; using plaza center"));
                sb.AppendLine("Center: " + c.center);
                sb.AppendLine("Approx plaza radius: " + c.plazaRadius.ToString("0.###") + " m");
                sb.AppendLine("Shared material: " + (c.sharedMaterial != null ? c.sharedMaterial.name : "null"));
                sb.AppendLine("Existing overlay child: " + (c.campPlaza.Find(OverlayName) != null));
                sb.AppendLine("Notes: " + c.notes);
                sb.AppendLine("APPLY creates or replaces only CampPlaza/" + OverlayName + " and the mesh asset at:");
                sb.AppendLine(MeshAssetPath);
                sb.AppendLine("No light, particle system, collider, runtime script or campfire mesh replacement will be created.");
            }
            sb.AppendLine("This audit changes nothing in the scene, assets or saves.");

            string reportPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, ReportFile);
            try { File.WriteAllText(reportPath, sb.ToString(), Encoding.UTF8); }
            catch (Exception ex) { Debug.LogWarning("TW Center Unified: could not write report: " + ex.Message); }
            Debug.Log(sb.ToString() + "\nReport: " + reportPath);
        }

        [MenuItem(Menu + "2. Применить красивый цельный центр", false, 301)]
        private static void Apply()
        {
            Context c;
            string issue;
            if (!TryContext(out c, out issue))
            {
                Debug.LogError("TW Center Unified: APPLY ABORTED. " + issue);
                return;
            }

            string msg =
                "Will CREATE or REPLACE exactly one overlay child:\n\n" +
                PathOf(c.campPlaza) + "/" + OverlayName + "\n\n" +
                "Will CREATE or UPDATE exactly one mesh asset:\n" + MeshAssetPath + "\n\n" +
                "This is a calm static art layer only:\n" +
                "- no Light\n- no ParticleSystem\n- no Collider\n- no runtime component\n- no campfire mesh replacement\n- no path / terrain rebuild\n\n" +
                "It uses the existing island material so the center keeps one style.\n" +
                "Recommended: make a scene backup or Git checkpoint first.";

            if (c.scene.path.Contains("_Recovery"))
                msg = "WARNING: active scene is in _Recovery. Continue only if this is the intended working scene.\n\n" + msg;

            if (!EditorUtility.DisplayDialog("CENTER UNIFIED SAFE — confirm scope", msg, "Apply", "Cancel"))
                return;

            string backup = null;
            try { backup = BackupScene(c.scene); }
            catch (Exception ex) { Debug.LogWarning("TW Center Unified: scene backup failed: " + ex.Message); }

            Mesh runtimeMesh = BuildUnifiedMesh(c);
            if (runtimeMesh == null || runtimeMesh.vertexCount < 3)
            {
                if (runtimeMesh != null) UnityEngine.Object.DestroyImmediate(runtimeMesh);
                Debug.LogError("TW Center Unified: mesh generation failed. Scene untouched.");
                return;
            }

            Mesh persisted = null;
            try
            {
                EnsureFolders();
                persisted = PersistMesh(MeshAssetPath, runtimeMesh);
                AssetDatabase.SaveAssetIfDirty(persisted);
            }
            catch (Exception ex)
            {
                UnityEngine.Object.DestroyImmediate(runtimeMesh);
                Debug.LogError("TW Center Unified: mesh asset write failed. Scene untouched. " + ex);
                return;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(runtimeMesh);
            }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("TW Center Unified Safe Art");
            try
            {
                Transform prev = c.campPlaza.Find(OverlayName);
                if (prev != null) Undo.DestroyObjectImmediate(prev.gameObject);

                var go = new GameObject(OverlayName);
                Undo.RegisterCreatedObjectUndo(go, "Create " + OverlayName);
                go.transform.SetParent(c.campPlaza, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                var mf = Undo.AddComponent<MeshFilter>(go);
                mf.sharedMesh = persisted;
                var mr = Undo.AddComponent<MeshRenderer>(go);
                mr.sharedMaterial = c.sharedMaterial;
                mr.shadowCastingMode = ShadowCastingMode.On;
                mr.receiveShadows = true;
                Undo.CollapseUndoOperations(group);
                EditorSceneManager.MarkSceneDirty(c.scene);
                Debug.Log("TW Center Unified: applied successfully." +
                          (string.IsNullOrEmpty(backup) ? "" : " Scene backup: " + backup) +
                          " One overlay only, no lights/particles/colliders/runtime scripts.");
            }
            catch (Exception ex)
            {
                Undo.RevertAllDownToGroup(group);
                Debug.LogError("TW Center Unified: scene edit failed and was undone. " + ex);
            }
        }

        [MenuItem(Menu + "3. Убрать только этот декор", false, 302)]
        private static void Remove()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("TW Center Unified: open a valid scene first.");
                return;
            }
            Transform plaza = FindByName(scene, "CampPlaza");
            if (plaza == null)
            {
                Debug.LogWarning("TW Center Unified: CampPlaza not found.");
                return;
            }
            Transform child = plaza.Find(OverlayName);
            if (child == null)
            {
                Debug.Log("TW Center Unified: no overlay to remove.");
                return;
            }
            if (!EditorUtility.DisplayDialog("CENTER UNIFIED SAFE — remove overlay",
                "Will delete only " + PathOf(child) + ".\nMesh asset remains in project for reapply/undo.\nNo other object is changed.",
                "Remove", "Cancel")) return;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Remove " + OverlayName);
            Undo.DestroyObjectImmediate(child.gameObject);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("TW Center Unified: overlay removed. Mesh asset retained at " + MeshAssetPath);
        }

        private static bool TryContext(out Context c, out string issue)
        {
            c = new Context();
            issue = null;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                issue = "Exit Play Mode before using this tool.";
                return false;
            }

            c.scene = SceneManager.GetActiveScene();
            if (!c.scene.IsValid() || !c.scene.isLoaded || string.IsNullOrEmpty(c.scene.path))
            {
                issue = "Open and save the intended scene first.";
                return false;
            }

            c.campPlaza = FindByName(c.scene, "CampPlaza");
            if (c.campPlaza == null)
            {
                issue = "CampPlaza was not found in the active scene.";
                return false;
            }

            Renderer plazaRenderer = c.campPlaza.GetComponentInChildren<MeshRenderer>(true);
            if (plazaRenderer == null || plazaRenderer.sharedMaterial == null)
            {
                issue = "CampPlaza mesh/material not found.";
                return false;
            }
            c.sharedMaterial = plazaRenderer.sharedMaterial;

            c.campfire = FindUniqueCampfire(c.scene);
            c.center = c.campfire != null ? c.campfire.position : plazaRenderer.bounds.center;
            c.plazaRadius = EstimateRadius(plazaRenderer.bounds, c.center);
            if (c.plazaRadius < 2.5f) c.plazaRadius = 5.0f;
            c.notes = "Radius derived from CampPlaza renderer bounds. Decorative placement is intentionally conservative.";
            return true;
        }

        private static float EstimateRadius(Bounds b, Vector3 center)
        {
            float rx = Mathf.Max(Mathf.Abs(b.max.x - center.x), Mathf.Abs(center.x - b.min.x));
            float rz = Mathf.Max(Mathf.Abs(b.max.z - center.z), Mathf.Abs(center.z - b.min.z));
            return Mathf.Max(rx, rz);
        }

        private static Transform FindUniqueCampfire(Scene scene)
        {
            Transform found = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != "Campfire") continue;
                if (found != null) return found; // use first if duplicates exist; we are not editing campfire.
                found = root.transform;
            }
            return found;
        }

        private static Transform FindByName(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    if (string.Equals(t.name, name, StringComparison.OrdinalIgnoreCase))
                        return t;
            }
            return null;
        }

        private static string PathOf(Transform t)
        {
            if (t == null) return "null";
            string path = t.name;
            for (Transform p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
            return path;
        }

        private static string BackupScene(Scene scene)
        {
            if (string.IsNullOrEmpty(scene.path) || !File.Exists(scene.path)) return null;
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string backupDir = Path.Combine(projectRoot, "TW_SceneBackups");
            Directory.CreateDirectory(backupDir);
            string file = Path.GetFileNameWithoutExtension(scene.path) + "_TWCenterUnified_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity";
            string outPath = Path.Combine(backupDir, file);
            File.Copy(scene.path, outPath, false);
            return outPath;
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Generated"))
                AssetDatabase.CreateFolder("Assets/_Project", "Generated");
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Generated/TWCenterUnified"))
                AssetDatabase.CreateFolder("Assets/_Project/Generated", "TWCenterUnified");
        }

        private static Mesh PersistMesh(string path, Mesh source)
        {
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (old == null)
            {
                var other = AssetDatabase.LoadMainAssetAtPath(path);
                if (other != null) throw new InvalidOperationException("Non-Mesh asset already exists at " + path);
                Mesh copy = UnityEngine.Object.Instantiate(source);
                copy.name = Path.GetFileNameWithoutExtension(path);
                AssetDatabase.CreateAsset(copy, path);
                return copy;
            }
            Undo.RecordObject(old, "Refresh " + old.name);
            EditorUtility.CopySerialized(source, old);
            old.name = Path.GetFileNameWithoutExtension(path);
            EditorUtility.SetDirty(old);
            return old;
        }

        private static float GroundY(Vector3 xz, float fallbackY)
        {
            RaycastHit hit;
            Vector3 start = new Vector3(xz.x, fallbackY + 25f, xz.z);
            if (Physics.Raycast(start, Vector3.down, out hit, 100f, ~0, QueryTriggerInteraction.Ignore))
                return hit.point.y;
            return fallbackY;
        }

        private static Mesh BuildUnifiedMesh(Context c)
        {
            var mb = new MeshBuilder();

            // Palette derived from the existing plaza comments: cream, lavender, sand, muted greens, warm wood.
            Color cream = C("#E7D9B8");
            Color lavender = C("#CFC3DE");
            Color sand = C("#DDBD96");
            Color lavenderDark = C("#B0A4C4");
            Color mint = C("#9DC597");
            Color leaf = C("#7FA76E");
            Color leafDark = C("#668D5F");
            Color flowerPink = C("#E8B6C9");
            Color flowerYellow = C("#E5C86D");
            Color wood = C("#9B6C49");
            Color woodDark = C("#744A33");

            Vector3 center = c.center;
            float yBase = GroundY(center, center.y);
            center.y = yBase;
            float R = Mathf.Clamp(c.plazaRadius, 4.6f, 6.6f);

            // 1) A gentle rhythm of low accent stones on the outer paved ring.
            float[] slabAngles = { 28f, 62f, 118f, 152f, 208f, 242f, 298f, 332f };
            for (int i = 0; i < slabAngles.Length; i++)
            {
                float a = slabAngles[i] * Mathf.Deg2Rad;
                Vector3 p = center + new Vector3(Mathf.Cos(a) * (R - 1.0f), 0f, Mathf.Sin(a) * (R - 1.0f));
                p.y = GroundY(p, yBase) + 0.075f;
                Quaternion rot = Quaternion.Euler(0f, slabAngles[i] + 90f, 0f);
                Vector3 size = (i % 2 == 0) ? new Vector3(0.68f, 0.05f, 0.28f) : new Vector3(0.58f, 0.05f, 0.24f);
                mb.Box(p, size, rot, (i % 3 == 0) ? lavender : (i % 3 == 1 ? cream : sand));
            }

            // 2) A few quiet pebble clusters just outside the rim.
            float[] pebbleAngles = { 20f, 98f, 176f, 260f, 316f };
            for (int i = 0; i < pebbleAngles.Length; i++)
            {
                float a = pebbleAngles[i] * Mathf.Deg2Rad;
                Vector3 p = center + new Vector3(Mathf.Cos(a) * (R + 0.35f), 0f, Mathf.Sin(a) * (R + 0.35f));
                p.y = GroundY(p, yBase);
                AddPebbleCluster(mb, p, i * 47f, cream, lavender, lavenderDark);
            }

            // 3) Sparse low shrub + flower mounds outside the circle to stitch the center into the biome.
            float[] shrubAngles = { 44f, 132f, 228f, 304f };
            for (int i = 0; i < shrubAngles.Length; i++)
            {
                float a = shrubAngles[i] * Mathf.Deg2Rad;
                Vector3 p = center + new Vector3(Mathf.Cos(a) * (R + 0.68f), 0f, Mathf.Sin(a) * (R + 0.68f));
                p.y = GroundY(p, yBase);
                AddShrubCluster(mb, p, i * 51f, mint, leaf, leafDark);
                AddFlowerDots(mb, p + new Vector3(0.18f, 0f, -0.12f), i * 11f, flowerPink, flowerYellow);
            }

            // 4) Two small firewood stacks at the outer edge — enough story, no clutter.
            float[] woodAngles = { 146f, 326f };
            for (int i = 0; i < woodAngles.Length; i++)
            {
                float a = woodAngles[i] * Mathf.Deg2Rad;
                Vector3 p = center + new Vector3(Mathf.Cos(a) * (R - 0.15f), 0f, Mathf.Sin(a) * (R - 0.15f));
                p.y = GroundY(p, yBase) + 0.03f;
                AddWoodStack(mb, p, woodAngles[i] + 90f, wood, woodDark, sand);
            }

            // 5) A very subtle inner companion ring of small stone chips to visually gather the hearth without touching campfire meshes.
            for (int i = 0; i < 10; i++)
            {
                float ang = i * 36f + 14f;
                float a = ang * Mathf.Deg2Rad;
                Vector3 p = center + new Vector3(Mathf.Cos(a) * 1.95f, 0f, Mathf.Sin(a) * 1.95f);
                p.y = GroundY(p, yBase) + 0.015f;
                Quaternion rot = Quaternion.Euler(0f, ang + 13f, 0f);
                Vector3 size = (i % 2 == 0) ? new Vector3(0.18f, 0.04f, 0.11f) : new Vector3(0.15f, 0.035f, 0.10f);
                mb.Box(p, size, rot, (i % 2 == 0) ? cream : lavender);
            }

            return mb.Create("TW_CenterUnifiedMesh");
        }

        private static void AddPebbleCluster(MeshBuilder mb, Vector3 pos, float yaw, Color c1, Color c2, Color c3)
        {
            Quaternion r = Quaternion.Euler(0f, yaw, 0f);
            mb.Box(pos + Rotate(r, new Vector3(-0.10f, 0.06f, -0.05f)), new Vector3(0.24f, 0.10f, 0.18f), r * Quaternion.Euler(6f, 8f, 0f), c1);
            mb.Box(pos + Rotate(r, new Vector3(0.10f, 0.05f, 0.02f)), new Vector3(0.18f, 0.08f, 0.14f), r * Quaternion.Euler(-4f, -12f, 0f), c2);
            mb.Box(pos + Rotate(r, new Vector3(0.02f, 0.04f, 0.13f)), new Vector3(0.14f, 0.07f, 0.12f), r * Quaternion.Euler(0f, 18f, 6f), c3);
        }

        private static void AddShrubCluster(MeshBuilder mb, Vector3 pos, float yaw, Color c1, Color c2, Color c3)
        {
            Quaternion r = Quaternion.Euler(0f, yaw, 0f);
            mb.Box(pos + Rotate(r, new Vector3(0f, 0.11f, 0f)), new Vector3(0.34f, 0.20f, 0.26f), r * Quaternion.Euler(0f, 12f, 6f), c2);
            mb.Box(pos + Rotate(r, new Vector3(-0.14f, 0.08f, 0.02f)), new Vector3(0.22f, 0.16f, 0.18f), r * Quaternion.Euler(8f, -15f, -8f), c1);
            mb.Box(pos + Rotate(r, new Vector3(0.14f, 0.07f, -0.02f)), new Vector3(0.20f, 0.14f, 0.16f), r * Quaternion.Euler(-8f, 14f, 8f), c3);
        }

        private static void AddFlowerDots(MeshBuilder mb, Vector3 pos, float yaw, Color pink, Color yellow)
        {
            Quaternion r = Quaternion.Euler(0f, yaw, 0f);
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f * Mathf.Deg2Rad;
                Vector3 p = pos + Rotate(r, new Vector3(Mathf.Cos(a) * 0.18f, 0.0f, Mathf.Sin(a) * 0.12f));
                p.y += 0.03f;
                mb.Box(p, new Vector3(0.05f, 0.10f, 0.05f), Quaternion.Euler(0f, yaw + i * 17f, 0f), leafStem(i));
                mb.Box(p + new Vector3(0f, 0.07f, 0f), new Vector3(0.08f, 0.04f, 0.08f), Quaternion.Euler(0f, yaw + i * 22f, 45f), i % 2 == 0 ? pink : yellow);
            }
            Color leafStem(int idx) { return idx % 2 == 0 ? C("#6F9867") : C("#7DA673"); }
        }

        private static void AddWoodStack(MeshBuilder mb, Vector3 pos, float yaw, Color wood, Color bark, Color cap)
        {
            Quaternion r = Quaternion.Euler(0f, yaw, 0f);
            // bottom row
            AddLog(mb, pos + Rotate(r, new Vector3(-0.15f, 0.06f, 0.00f)), r, 0.48f, wood, bark, cap);
            AddLog(mb, pos + Rotate(r, new Vector3(0.15f, 0.06f, 0.00f)), r, 0.48f, wood, bark, cap);
            // top row
            AddLog(mb, pos + Rotate(r, new Vector3(0.00f, 0.15f, 0.02f)), r * Quaternion.Euler(0f, 90f, 0f), 0.42f, wood, bark, cap);
        }

        private static void AddLog(MeshBuilder mb, Vector3 center, Quaternion rot, float length, Color wood, Color bark, Color cap)
        {
            mb.Cylinder(center + rot * new Vector3(0f, 0f, -length * 0.5f), center + rot * new Vector3(0f, 0f, length * 0.5f), 0.08f, 0.08f, bark, cap, 7);
            // A subtle lighter mid-band.
            mb.Box(center, new Vector3(0.10f, 0.05f, length * 0.38f), rot, wood);
        }

        private static Vector3 Rotate(Quaternion r, Vector3 v) { return r * v; }

        private static Color C(string hex)
        {
            Color c;
            return ColorUtility.TryParseHtmlString(hex, out c) ? c.linear : Color.white;
        }

        private sealed class MeshBuilder
        {
            private readonly List<Vector3> v = new List<Vector3>();
            private readonly List<Color> c = new List<Color>();
            private readonly List<int> t = new List<int>();

            private int Add(Vector3 p, Color col)
            {
                int i = v.Count;
                v.Add(p);
                c.Add(col);
                return i;
            }

            private void Tri(int a, int b, int cc)
            {
                t.Add(a); t.Add(b); t.Add(cc);
            }

            public void Box(Vector3 center, Vector3 size, Quaternion rot, Color col)
            {
                Vector3 e = size * 0.5f;
                Vector3[] p = new Vector3[8]
                {
                    center + rot * new Vector3(-e.x,-e.y,-e.z),
                    center + rot * new Vector3( e.x,-e.y,-e.z),
                    center + rot * new Vector3( e.x,-e.y, e.z),
                    center + rot * new Vector3(-e.x,-e.y, e.z),
                    center + rot * new Vector3(-e.x, e.y,-e.z),
                    center + rot * new Vector3( e.x, e.y,-e.z),
                    center + rot * new Vector3( e.x, e.y, e.z),
                    center + rot * new Vector3(-e.x, e.y, e.z),
                };

                // bottom, top, front, back, left, right as separate quads for hard edges.
                Quad(p[0], p[1], p[2], p[3], Shade(col, 0.88f));
                Quad(p[7], p[6], p[5], p[4], Shade(col, 1.04f));
                Quad(p[3], p[2], p[6], p[7], Shade(col, 1.00f));
                Quad(p[1], p[0], p[4], p[5], Shade(col, 0.92f));
                Quad(p[0], p[3], p[7], p[4], Shade(col, 0.96f));
                Quad(p[2], p[1], p[5], p[6], Shade(col, 0.98f));
            }

            public void Cylinder(Vector3 a, Vector3 b, float ra, float rb, Color side, Color cap, int sides)
            {
                Vector3 axis = (b - a).normalized;
                Vector3 u = Vector3.Cross(axis, Vector3.up);
                if (u.sqrMagnitude < 0.0001f) u = Vector3.Cross(axis, Vector3.right);
                u.Normalize();
                Vector3 w = Vector3.Cross(axis, u).normalized;

                int[] lo = new int[sides];
                int[] hi = new int[sides];
                for (int i = 0; i < sides; i++)
                {
                    float ang = i * Mathf.PI * 2f / sides;
                    Vector3 r = u * Mathf.Cos(ang) + w * Mathf.Sin(ang);
                    lo[i] = Add(a + r * ra, side);
                    hi[i] = Add(b + r * rb, side);
                }
                for (int i = 0; i < sides; i++)
                {
                    int n = (i + 1) % sides;
                    Tri(lo[i], lo[n], hi[i]);
                    Tri(lo[n], hi[n], hi[i]);
                }

                int ca = Add(a, cap), cb = Add(b, cap);
                for (int i = 0; i < sides; i++)
                {
                    int n = (i + 1) % sides;
                    Tri(ca, lo[n], lo[i]);
                    Tri(cb, hi[i], hi[n]);
                }
            }

            private void Quad(Vector3 a, Vector3 b, Vector3 cc, Vector3 d, Color col)
            {
                int i0 = Add(a, col);
                int i1 = Add(b, col);
                int i2 = Add(cc, col);
                int i3 = Add(d, col);
                Tri(i0, i1, i2);
                Tri(i0, i2, i3);
            }

            private static Color Shade(Color src, float k)
            {
                return new Color(Mathf.Clamp01(src.r * k), Mathf.Clamp01(src.g * k), Mathf.Clamp01(src.b * k), 1f);
            }

            public Mesh Create(string name)
            {
                if (v.Count < 3 || t.Count < 3) return null;
                var m = new Mesh();
                m.name = name;
                m.SetVertices(v);
                m.SetColors(c);
                m.SetTriangles(t, 0, true);
                m.RecalculateNormals();
                m.RecalculateBounds();
                return m;
            }
        }
    }
}
#endif
