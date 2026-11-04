// Timber Wings / ISL-021 — READ-ONLY scene and edge-safety audit.
// Place at Assets/_Project/Editor/IslandV2/ISL021EdgeSafetyAudit.cs
// Tested by source inspection against Scripts(5).zip (2026-10-26); NOT tested in Unity.
// No scene objects, components, colliders, transforms, materials or assets are changed.
// Output is a text report in the project root, outside Assets.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IslandV2
{
    public static class ISL021EdgeSafetyAudit
    {
        private const int EdgeSamples = 32;
        private const float EdgeOffset = 1.5f;
        private const string ReportName = "ISL-021_EdgeSafetyAudit.txt";
        private static readonly StringBuilder Report = new StringBuilder(18000);
        private static int warnings;

        [MenuItem("Tools/BuilderSim/Локация/Остров v2/ISL-021/1. Аудит границ и Respawn (без изменений)", false, 189)]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("ISL-021: остановите Play Mode. Это аудит сохранённой сцены в Edit Mode.");
                return;
            }

            Report.Length = 0;
            warnings = 0;
            Scene scene = SceneManager.GetActiveScene();
            Report.AppendLine("TIMBER WINGS — ISL-021 / EDGE SAFETY AUDIT (READ ONLY)");
            Report.AppendLine("Date: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            Report.AppendLine("Unity: " + Application.unityVersion);
            Report.AppendLine("Active scene: " + scene.path + " (" + scene.name + ")");
            Report.AppendLine("This is a static Editor audit, NOT a Play Mode / save migration / FPS test.");
            Report.AppendLine("No scene, .asset, .meta, GameObject, component, collider or mesh will be modified.");
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Warn("Active scene is not loaded/valid. Open the real playable scene first.");
                Finish();
                return;
            }
            if (string.IsNullOrEmpty(scene.path)) Warn("Scene has no saved path: check before doing any other work.");
            if (scene.path.IndexOf("_Recovery", StringComparison.OrdinalIgnoreCase) >= 0)
                Warn("Active scene lies in _Recovery. Confirm the real game scene before testing.");

            var dataList = FindSceneComponents<IslandV2Data>(scene);
            var floatingList = FindSceneComponents<FloatingIsland>(scene);
            var guards = FindSceneComponents<IslandEdgeGuard>(scene);
            var terrains = FindSceneComponents<TerrainCollider>(scene);
            var allColliders = FindSceneComponents<Collider>(scene);

            Section("1. Island sources and edge guards");
            DescribeComponents("IslandV2Data", dataList);
            DescribeComponents("FloatingIsland", floatingList);
            DescribeComponents("IslandEdgeGuard", guards);
            int activeSources = CountEnabled(dataList) + CountEnabled(floatingList);
            if (activeSources == 0) Warn("No enabled island source in this scene.");
            if (activeSources > 1) Warn("More than one active island source: verify IslandSource selection at runtime.");
            if (guards.Count > 1) Warn("Multiple IslandEdgeGuard components in the scene; inspect runtime singleton ownership.");
            if (guards.Count == 0 && CountEnabled(dataList) == 1)
                Report.AppendLine("NOTE: IslandV2Data.Awake() adds IslandEdgeGuard at runtime; absence in Edit Mode may be normal.");
            foreach (IslandEdgeGuard guard in guards)
            {
                Report.AppendLine(" Guard " + HierarchyPath(guard.transform)
                    + " enabled=" + guard.enabled
                    + " useFixedRespawn=" + guard.useFixedRespawn
                    + " safeEdgeMargin=" + F(guard.safeEdgeMargin)
                    + " dropDepth=" + F(guard.dropDepth)
                    + " respawnHeight=" + F(guard.respawnHeight));
            }

            Section("2. Layout and contour");
            IslandLayout layout = AssetDatabase.LoadAssetAtPath<IslandLayout>(IslandLayout.AssetPath);
            if (layout == null) Warn("IslandLayout asset not found: " + IslandLayout.AssetPath);
            else
            {
                Report.AppendLine("Layout: " + IslandLayout.AssetPath);
                Report.AppendLine(" maxWalkSlopeDeg=" + F(layout.maxWalkSlopeDeg)
                    + " hillAmp=" + F(layout.hillAmp) + " hillScale=" + F(layout.hillScale)
                    + " pathFlatten=" + F(layout.pathFlatten) + " flatBlend=" + F(layout.flatBlend));
            }
            IslandV2Data island = FirstEnabled(dataList);
            List<Vector2> contour = null;
            float groundY = 0f;
            if (island == null) Warn("No enabled IslandV2Data: cannot validate scene contour / fixed respawn with v2 data.");
            else
            {
                contour = island.GetOutline();
                groundY = island.groundY;
                Report.AppendLine("IslandV2Data: " + HierarchyPath(island.transform));
                Report.AppendLine("Contour points: " + (contour == null ? 0 : contour.Count) + "; groundY=" + F(groundY));
                if (contour == null || contour.Count < 3) Warn("Scene contour missing/too short: runtime IslandEdgeGuard.Init() cannot use it.");
            }

            Section("3. Terrain colliders and suspicious invisible obstacles");
            Report.AppendLine("All colliders in active scene, including inactive: " + allColliders.Count);
            Report.AppendLine("TerrainColliders (including inactive): " + terrains.Count);
            int enabledTerrains = 0;
            foreach (TerrainCollider t in terrains)
            {
                bool enabled = t.enabled && t.gameObject.activeInHierarchy;
                if (enabled) enabledTerrains++;
                Report.AppendLine(" TerrainCollider " + HierarchyPath(t.transform)
                    + " enabledAndActive=" + enabled + " terrainData=" + (t.terrainData != null));
            }
            if (enabledTerrains == 0) Warn("No enabled TerrainCollider in scene: normal land physics may be missing.");
            if (enabledTerrains > 1) Warn("More than one enabled TerrainCollider: inspect for duplicate/legacy terrain.");

            int suspicious = 0;
            int cliff = 0;
            foreach (Collider c in allColliders)
            {
                if (c == null) continue;
                string n = c.gameObject.name;
                bool isCliff = Contains(n, "cliff") || Contains(n, "обрыв");
                bool isOld = Contains(n, "boundary") || Contains(n, "gatewalls")
                    || Contains(n, "ground_auto") || Contains(n, "borderwall")
                    || Contains(n, "wall_");
                if (!isOld && !isCliff) continue;
                bool enabled = c.enabled && c.gameObject.activeInHierarchy;
                if (isCliff) cliff++;
                if (isOld) suspicious++;
                Report.AppendLine(" " + (isOld ? "CANDIDATE" : "CLIFF") + ": " + HierarchyPath(c.transform)
                    + " / " + c.GetType().Name + " enabledAndActive=" + enabled
                    + " trigger=" + c.isTrigger);
                if (isCliff && enabled && !c.isTrigger)
                    Warn("Active physical collider on Cliff candidate: " + HierarchyPath(c.transform)
                        + ". Verify that the visual cliff is not blocking intended falls.");
                else if (isOld && enabled && !c.isTrigger)
                    Warn("Potential legacy barrier (do NOT automatically disable): " + HierarchyPath(c.transform));
            }
            Report.AppendLine("Name-matched legacy barrier candidates: " + suspicious + "; cliff candidates: " + cliff);
            Report.AppendLine("NOTE: this is a name-based inventory, not proof of a collision defect.");

            Section("4. Respawn markers: references, scene position, ground hit");
            var markers = FindSceneTransformsByName(scene, "IslandRespawn", "SPAWN_Player");
            foreach (Transform m in markers)
                Report.AppendLine(" Marker: " + HierarchyPath(m) + " active=" + m.gameObject.activeInHierarchy
                    + " pos=" + V(m.position));
            if (markers.Count == 0) Warn("No IslandRespawn or SPAWN_Player object found (fixed respawn can fall back to safe/center).");
            Transform selected = null;
            if (guards.Count > 0)
            {
                foreach (IslandEdgeGuard guard in guards)
                    if (guard != null && guard.enabled && guard.gameObject.activeInHierarchy && guard.respawnPoint != null)
                    { selected = guard.respawnPoint; Report.AppendLine("Explicit Respawn Point from " + HierarchyPath(guard.transform) + ": " + HierarchyPath(selected)); break; }
            }
            if (selected == null) selected = PreferredActiveMarker(markers, "IslandRespawn");
            if (selected == null) selected = PreferredActiveMarker(markers, "SPAWN_Player");
            if (selected == null) Report.AppendLine("Selected fixed marker: none verified (check runtime fallback).");
            else
            {
                Report.AppendLine("Selected candidate marker: " + HierarchyPath(selected));
                if (contour != null && contour.Count >= 3)
                {
                    Vector2 q = new Vector2(selected.position.x, selected.position.z);
                    float distance = DistanceToEdge(contour, q);
                    bool inside = Inside(contour, q);
                    Report.AppendLine(" Inside actual scene contour: " + inside + "; edge distance=" + F(distance) + " m");
                    if (!inside) Warn("Preferred respawn marker is outside the island contour.");
                    else if (distance < 3f) Warn("Preferred respawn marker is closer than 3 m to the island edge.");
                }
                float rayY = Mathf.Max(selected.position.y, groundY) + 6f;
                RaycastHit[] hits = Physics.RaycastAll(new Vector3(selected.position.x, rayY, selected.position.z), Vector3.down,
                    60f, ~0, QueryTriggerInteraction.Ignore);
                Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                if (hits.Length == 0) Warn("No collider under selected marker in 60 m Raycast (runtime may use marker position unvalidated).");
                else
                {
                    Report.AppendLine(" Nearest static ray hit: " + HierarchyPath(hits[0].collider.transform)
                        + " / " + hits[0].collider.GetType().Name + " hitY=" + F(hits[0].point.y));
                    if (!(hits[0].collider is TerrainCollider))
                        Warn("First hit under respawn marker is not TerrainCollider; runtime may use a roof/prop as the target.");
                }
            }

            Section("5. Terrain edge samples (32 around actual v2 contour)");
            if (contour != null && contour.Count >= 3)
            {
                int insideMissing = 0, outsideSolid = 0;
                Vector2 center = Vector2.zero;
                foreach (Vector2 p in contour) center += p;
                center /= contour.Count;
                for (int i = 0; i < EdgeSamples; i++)
                {
                    Vector2 edge = contour[i * contour.Count / EdgeSamples];
                    Vector2 dir = edge - center;
                    if (dir.sqrMagnitude < 0.001f) continue;
                    dir.Normalize();
                    Vector2 inner = edge - dir * EdgeOffset;
                    Vector2 outer = edge + dir * EdgeOffset;
                    bool insideHit = TerrainAt(scene, inner);
                    bool outsideHit = TerrainAt(scene, outer);
                    if (!insideHit || outsideHit)
                    {
                        if (!insideHit) insideMissing++;
                        if (outsideHit) outsideSolid++;
                        Report.AppendLine(" Edge sample " + i.ToString("00") + " " + V2(edge)
                            + ": interiorTerrain=" + insideHit + " exteriorTerrain=" + outsideHit);
                    }
                }
                Report.AppendLine(" Interior missing terrain: " + insideMissing + "/" + EdgeSamples);
                Report.AppendLine(" Exterior unexpectedly solid: " + outsideSolid + "/" + EdgeSamples);
                if (insideMissing > 0) Warn("Some interior edge samples lack TerrainCollider; inspect holes / contour alignment.");
                if (outsideSolid > 0) Warn("Some exterior edge samples hit TerrainCollider; inspect unwanted ground beyond contour.");
            }
            else Report.AppendLine("SKIPPED: missing v2 contour.");

            Section("6. Runtime checks that MUST be completed manually");
            Report.AppendLine("[ ] Confirm actual playable scene, Git status, backups and real Inspector values.");
            Report.AppendLine("[ ] Run the existing IslandCheck contract / walkability test.");
            Report.AppendLine("[ ] In a disposable save, test falls from 8 outer-edge sections and final bridge.");
            Report.AppendLine("[ ] Verify respawn position, camera, movement, UI prompts and console errors.");
            Report.AppendLine("[ ] Test water/fall interactions, dash and auto-walk at edge.");
            Report.AppendLine("[ ] Save, restart and confirm progress/buildings/resources unchanged.");
            Report.AppendLine("[ ] Test previously reported steep areas (35.83 / 48.30 deg) in Play Mode.");
            Report.AppendLine("No runtime fall or saved-game integrity can be established by this Editor report.");
            Finish();
        }

        private static void Finish()
        {
            Report.AppendLine();
            Report.AppendLine("Warnings: " + warnings + ". Warning != confirmed gameplay defect.");
            string result = Report.ToString();
            try
            {
                string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, ReportName);
                File.WriteAllText(path, result, new UTF8Encoding(false));
                Debug.Log("ISL-021 read-only audit complete. Warnings: " + warnings + ". Report: " + path + "\n" + result);
            }
            catch (Exception ex)
            {
                Debug.LogError("ISL-021: could not write report: " + ex.Message + "\n" + result);
            }
        }

        private static void Section(string s) { Report.AppendLine("\n[" + s + "]"); }
        private static void Warn(string s) { warnings++; Report.AppendLine("WARNING: " + s); }
        private static string F(float n) { return n.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture); }
        private static string V(Vector3 v) { return "(" + F(v.x) + ", " + F(v.y) + ", " + F(v.z) + ")"; }
        private static string V2(Vector2 v) { return "(" + F(v.x) + ", " + F(v.y) + ")"; }
        private static bool Contains(string a, string b) { return a.IndexOf(b, StringComparison.OrdinalIgnoreCase) >= 0; }

        private static List<T> FindSceneComponents<T>(Scene scene) where T : Component
        {
            var list = new List<T>();
            foreach (GameObject root in scene.GetRootGameObjects())
                list.AddRange(root.GetComponentsInChildren<T>(true));
            return list;
        }

        private static List<Transform> FindSceneTransformsByName(Scene scene, params string[] names)
        {
            var result = new List<Transform>();
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    foreach (string n in names)
                        if (t.name == n) { result.Add(t); break; }
            return result;
        }

        private static Transform PreferredActiveMarker(List<Transform> markers, string wanted)
        {
            foreach (Transform t in markers)
                if (t.name == wanted && t.gameObject.activeInHierarchy) return t;
            return null;
        }

        private static void DescribeComponents<T>(string label, List<T> list) where T : Component
        {
            Report.AppendLine(label + " count (incl. inactive): " + list.Count);
            foreach (T x in list)
            {
                Behaviour b = x as Behaviour;
                Report.AppendLine(" " + HierarchyPath(x.transform) + " active=" + x.gameObject.activeInHierarchy
                    + (b == null ? "" : " enabled=" + b.enabled));
            }
        }

        private static int CountEnabled<T>(List<T> list) where T : Behaviour
        {
            int n = 0;
            foreach (T b in list) if (b != null && b.enabled && b.gameObject.activeInHierarchy) n++;
            return n;
        }

        private static T FirstEnabled<T>(List<T> list) where T : Behaviour
        {
            foreach (T b in list) if (b != null && b.enabled && b.gameObject.activeInHierarchy) return b;
            return null;
        }

        private static string HierarchyPath(Transform t)
        {
            if (t == null) return "(none)";
            var parts = new List<string>();
            for (Transform p = t; p != null; p = p.parent) parts.Add(p.name);
            parts.Reverse();
            return string.Join("/", parts.ToArray());
        }

        private static bool TerrainAt(Scene scene, Vector2 p)
        {
            var hits = Physics.RaycastAll(new Vector3(p.x, 60f, p.y), Vector3.down,
                140f, ~0, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
                if (hit.collider is TerrainCollider && hit.collider.gameObject.scene == scene) return true;
            return false;
        }

        private static bool Inside(List<Vector2> polygon, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                Vector2 a = polygon[i], b = polygon[j];
                if ((a.y > p.y) != (b.y > p.y) &&
                    p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }
            return inside;
        }

        private static float DistanceToEdge(List<Vector2> polygon, Vector2 p)
        {
            float minSquared = float.MaxValue;
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 a = polygon[i], b = polygon[(i + 1) % polygon.Count];
                Vector2 ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
                minSquared = Mathf.Min(minSquared, (p - (a + t * ab)).sqrMagnitude);
            }
            return Mathf.Sqrt(minSquared);
        }
    }
}
#endif
