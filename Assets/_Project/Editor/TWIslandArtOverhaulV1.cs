#if UNITY_EDITOR
// Timber Wings | ISLAND ART OVERHAUL v1
// One coordinated, reversible art direction pass for the existing IslandV2 geography.
// Does NOT rebuild terrain, water, cliff, vegetation trees, campfire, lighting or quests.
// Editor-only / additive / one renderer per zone / existing vertex-color URP material.
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
    public static class TWIslandArtOverhaulV1
    {
        private const string Menu = "Tools/BuilderSim/ISLAND ART OVERHAUL/";
        private const string RootName = "TW_IslandArtOverhaul_V1";
        private const string AssetsFolder = "Assets/_Project/Generated/TWIslandArtOverhaulV1";
        private const string ReportName = "TW_IslandArtOverhaul_Audit.txt";
        private const string ShaderName = "Project/IslandV2/VertexColor";

        private enum Region { Camp, Pond, River, ForestLight, ForestDark, Mystic, Edge }
        private static readonly Region[] All = {
            Region.Camp, Region.Pond, Region.River, Region.ForestLight,
            Region.ForestDark, Region.Mystic, Region.Edge
        };
        private static readonly string[] OldNames = {
            "TW_CenterComplete_Art", "TW_CenterComplete_Atmosphere",
            "TW_CenterUnified", "TW_CenterSafeV2_Art", "TW_CenterSafeV2_Audio",
            "ISL023_StoneRhythm", "ISL024_TripodAccents", "ISL024_LogsAccents",
            "ISL024_FlameAccents", "ISL024B_TripodHero", "ISL024B_Kettle",
            "ISL024B_CoreLogs", "ISL024B_FlameHero"
        };

        private sealed class Context
        {
            public Scene scene;
            public Transform island, manual, existing;
            public TerrainCollider terrain;
            public IslandLayout layout;
            public IslandField field;
            public Material material;
            public readonly List<string> blockers = new List<string>();
            public readonly List<string> warnings = new List<string>();
        }
        private struct Record
        {
            public Region region;
            public Mesh mesh;
            public int count;
        }

        [MenuItem(Menu + "1. Аудит острова — только чтение", false, 410)]
        private static void Audit()
        {
            Context c; string error;
            bool ok = TryContext(out c, out error);
            var sb = new StringBuilder();
            sb.AppendLine("TIMBER WINGS — ISLAND ART OVERHAUL v1 — READ ONLY");
            sb.AppendLine("UTC: " + DateTime.UtcNow.ToString("O"));
            sb.AppendLine("Unity: " + Application.unityVersion);
            sb.AppendLine("Active scene: " + SceneManager.GetActiveScene().path);
            sb.AppendLine("Scene in _Recovery: " + SceneManager.GetActiveScene().path.Contains("_Recovery"));
            sb.AppendLine("No objects, asset references, mesh assets, colliders or saves are modified by AUDIT.");
            sb.AppendLine("PRECONDITIONS: " + (ok ? "OK" : "FAILED — " + error));
            if (ok)
            {
                sb.AppendLine("Layout: " + IslandLayout.AssetPath);
                sb.AppendLine("Contour vertices: " + c.field.Contour.Count);
                sb.AppendLine("Ground TerrainCollider: " + PathOf(c.terrain.transform));
                sb.AppendLine("Vertex-color material: " + AssetDatabase.GetAssetPath(c.material));
                sb.AppendLine("Existing overhaul root: " + (c.existing == null ? "none" : PathOf(c.existing)));
                sb.AppendLine("No terrain, water, cliff, tree or campfire rebuild is performed by APPLY.");
                sb.AppendLine("Zone meshes: Camp, Pond, River, ForestLight, ForestDark, Mystic, Edge.");
                foreach (string warning in c.warnings) sb.AppendLine("NOTE: " + warning);
                foreach (string blocker in c.blockers) sb.AppendLine("BLOCKER: " + blocker);
                foreach (Region region in All)
                {
                    int count;
                    Mesh m = BuildRegion(c, region, out count);
                    sb.AppendLine("PLAN " + region + ": " + count + " grounded decorative clusters, " +
                                  (m != null ? (m.triangles.Length / 3).ToString() + " tris" : "empty (skip)"));
                    if (m != null) UnityEngine.Object.DestroyImmediate(m);
                }
            }
            sb.AppendLine("APPLY replaces only " + RootName + " under IslandV2/_Manual and seven .mesh assets in " + AssetsFolder + ".");
            sb.AppendLine("Future full island rebuild does not remove _Manual. Remove command only touches our root.");
            string output = Path.Combine(ProjectRoot(), ReportName);
            try { File.WriteAllText(output, sb.ToString(), Encoding.UTF8); }
            catch (Exception ex) { Debug.LogWarning("ISLAND ART OVERHAUL: report write failed: " + ex.Message); }
            Debug.Log(sb.ToString() + "\nReport: " + output);
        }

        [MenuItem(Menu + "2. Применить весь остров (единый стиль)", false, 411)]
        private static void ApplyAll() { Apply(null); }
        [MenuItem(Menu + "3. Применить только ЦЕНТР", false, 412)]
        private static void ApplyCamp() { Apply(Region.Camp); }
        [MenuItem(Menu + "4. Применить только ПРУД", false, 413)]
        private static void ApplyPond() { Apply(Region.Pond); }
        [MenuItem(Menu + "5. Применить только РЕКУ", false, 414)]
        private static void ApplyRiver() { Apply(Region.River); }
        [MenuItem(Menu + "6. Применить только СВЕТЛЫЙ ЛЕС", false, 415)]
        private static void ApplyForestLight() { Apply(Region.ForestLight); }
        [MenuItem(Menu + "7. Применить только ТЁМНЫЙ ЛЕС", false, 416)]
        private static void ApplyForestDark() { Apply(Region.ForestDark); }
        [MenuItem(Menu + "8. Применить только ТАИНСТВЕННУЮ ЗОНУ", false, 417)]
        private static void ApplyMystic() { Apply(Region.Mystic); }
        [MenuItem(Menu + "9. Применить только КРАЯ ОСТРОВА", false, 418)]
        private static void ApplyEdge() { Apply(Region.Edge); }
        [MenuItem(Menu + "10. УДАЛИТЬ весь декор OVERHAUL", false, 419)]
        private static void RemoveAll()
        {
            Context c; string issue;
            if (!TryContext(out c, out issue)) { Debug.LogError("OVERHAUL REMOVE: " + issue); return; }
            if (c.existing == null) { Debug.Log("OVERHAUL: root not present."); return; }
            if (!EditorUtility.DisplayDialog("Remove Island Art Overhaul",
                "DELETE ONLY: " + PathOf(c.existing) + "\n" +
                "Will NOT delete the generated .mesh assets, base island, terrain, woods, lights, campfire or saves.",
                "Delete only Overhaul root", "Cancel")) return;
            Undo.DestroyObjectImmediate(c.existing.gameObject);
            EditorSceneManager.MarkSceneDirty(c.scene);
            Debug.Log("OVERHAUL: only " + RootName + " removed. Original island preserved.");
        }

        private static void Apply(Region? only)
        {
            Context c; string issue;
            if (!TryContext(out c, out issue)) { Debug.LogError("OVERHAUL APPLY ABORTED: " + issue); return; }
            if (c.blockers.Count > 0)
            {
                Debug.LogError("OVERHAUL APPLY ABORTED — first remove old active CENTER patches. " +
                               string.Join("; ", c.blockers));
                return;
            }
            if (c.scene.isDirty)
            {
                Debug.LogError("OVERHAUL: scene contains unsaved changes. Save it (Ctrl+S), then re-run APPLY so the backup is complete.");
                return;
            }
            string[] targets = only.HasValue ? new[] { only.Value.ToString() } : new[] { "Camp", "Pond", "River", "ForestLight", "ForestDark", "Mystic", "Edge" };
            string msg = "ACTIVE SCENE: " + c.scene.path + "\n\n" +
                         "ADD/REPLACE ONLY: IslandV2/_Manual/" + RootName + " / [" + string.Join(", ", targets) + "]\n" +
                         "UPDATE ONLY matching mesh assets in " + AssetsFolder + "\n\n" +
                         "DOES NOT MODIFY: existing CampPlaza/Campfire/Pond/Water/Cliff/Terrain/ForestTrees/NPC/quests/colliders/paths/save IDs.\n" +
                         "NO new Lights, ParticleSystems, runtime scripts, bloom or audio.\n\n" +
                         "A backed-up copy of the current .unity scene is required and created before apply.";
            if (c.scene.path.Contains("_Recovery"))
                msg = "WARNING — ACTIVE SCENE IS IN _RECOVERY. Verify this is the working copy.\n\n" + msg;
            if (!EditorUtility.DisplayDialog("TW Island Art Overhaul v1 — scope", msg, "Back up and Apply", "Cancel")) return;

            // Build meshes before mutating scene.
            var prepared = new List<Record>();
            try
            {
                foreach (Region region in All)
                {
                    if (only.HasValue && only.Value != region) continue;
                    int count;
                    Mesh mesh = BuildRegion(c, region, out count);
                    if (mesh != null && mesh.vertexCount > 0) prepared.Add(new Record { region = region, mesh = mesh, count = count });
                    else if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
                }
                if (prepared.Count == 0)
                {
                    Debug.LogError("OVERHAUL: no safe grounded placements in requested zone. No changes made.");
                    return;
                }
                string backup = BackupScene(c.scene); // must succeed before any writes
                EnsureFolders();
                var assets = new List<Mesh>();
                foreach (Record r in prepared)
                {
                    string asset = AssetPath(r.region);
                    Mesh dest = AssetDatabase.LoadAssetAtPath<Mesh>(asset);
                    if (dest == null)
                    {
                        if (AssetDatabase.LoadMainAssetAtPath(asset) != null)
                            throw new InvalidOperationException("Non-mesh asset occupies " + asset);
                        dest = UnityEngine.Object.Instantiate(r.mesh);
                        dest.name = "TW_AO_" + r.region;
                        AssetDatabase.CreateAsset(dest, asset);
                    }
                    else
                    {
                        Undo.RecordObject(dest, "Update generated art mesh " + r.region);
                        EditorUtility.CopySerialized(r.mesh, dest);
                        dest.name = "TW_AO_" + r.region;
                        EditorUtility.SetDirty(dest);
                    }
                    AssetDatabase.SaveAssetIfDirty(dest);
                    assets.Add(dest);
                }
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("TW Island Art Overhaul " + (only.HasValue ? only.ToString() : "All"));
                try
                {
                    Transform manual = c.manual;
                    Transform root = c.existing;
                    if (root == null)
                    {
                        var go = new GameObject(RootName);
                        Undo.RegisterCreatedObjectUndo(go, "Create Island Art Overhaul root");
                        go.transform.SetParent(manual, false);
                        root = go.transform;
                    }
                    for (int i = 0; i < prepared.Count; i++)
                    {
                        Record r = prepared[i];
                        Transform previous = root.Find(r.region.ToString());
                        if (previous != null) Undo.DestroyObjectImmediate(previous.gameObject);
                        var go = new GameObject(r.region.ToString());
                        Undo.RegisterCreatedObjectUndo(go, "Create zone " + r.region);
                        go.transform.SetParent(root, false);
                        var mf = Undo.AddComponent<MeshFilter>(go);
                        mf.sharedMesh = assets[i];
                        var renderer = Undo.AddComponent<MeshRenderer>(go);
                        renderer.sharedMaterial = c.material;
                        renderer.shadowCastingMode = ShadowCastingMode.On;
                        renderer.receiveShadows = true;
                    }
                    Undo.CollapseUndoOperations(group);
                    EditorSceneManager.MarkSceneDirty(c.scene);
                    Debug.Log("TW OVERHAUL APPLIED: " + prepared.Count + " zone mesh(es), backup: " + backup +
                              ". No original objects/FX/colliders modified. Save scene and inspect Game View.");
                }
                catch
                {
                    Undo.RevertAllDownToGroup(group);
                    throw;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("OVERHAUL FAILED: " + ex + ". Check backup. Scene modifications reverted where possible.");
            }
            finally
            {
                foreach (Record r in prepared) if (r.mesh != null) UnityEngine.Object.DestroyImmediate(r.mesh);
            }
        }

        private static bool TryContext(out Context c, out string issue)
        {
            c = new Context(); issue = null;
            if (EditorApplication.isPlayingOrWillChangePlaymode) { issue = "Exit Play Mode."; return false; }
            c.scene = SceneManager.GetActiveScene();
            if (!c.scene.IsValid() || !c.scene.isLoaded || string.IsNullOrEmpty(c.scene.path))
            { issue = "Open and save a playable scene."; return false; }
            GameObject islandObj = null;
            foreach (var go in c.scene.GetRootGameObjects()) if (go.name == "IslandV2")
            { if (islandObj != null) { issue = "Multiple IslandV2 roots."; return false; } islandObj = go; }
            if (islandObj == null) { issue = "IslandV2 root not found."; return false; }
            c.island = islandObj.transform;
            if ((c.island.position).sqrMagnitude > 0.0001f || Quaternion.Angle(c.island.rotation, Quaternion.identity) > 0.01f ||
                (c.island.localScale - Vector3.one).sqrMagnitude > 0.0001f)
            { issue = "IslandV2 root transform differs from layout origin."; return false; }
            c.manual = c.island.Find("_Manual");
            if (c.manual == null) { issue = "IslandV2/_Manual missing — no overlay will be created."; return false; }
            c.existing = c.manual.Find(RootName);
            Transform generated = c.island.Find("_Generated");
            Transform terrainObject = generated != null ? generated.Find("Ground_Terrain") : null;
            c.terrain = terrainObject != null ? terrainObject.GetComponent<TerrainCollider>() : null;
            if (c.terrain == null || !c.terrain.enabled || !c.terrain.gameObject.activeInHierarchy || c.terrain.terrainData == null)
            { issue = "Active generated Ground_Terrain/TerrainCollider missing."; return false; }
            c.layout = AssetDatabase.LoadAssetAtPath<IslandLayout>(IslandLayout.AssetPath);
            if (c.layout == null) { issue = "IslandLayout.asset missing."; return false; }
            c.field = new IslandField(c.layout);
            if (c.field.Contour.Count < 32) { issue = "Invalid island contour."; return false; }
            c.material = AssetDatabase.LoadAssetAtPath<Material>(IslandBlockout.MaterialPath);
            if (c.material == null || c.material.shader == null || c.material.shader.name != ShaderName)
            { issue = "Mat_IslandV2 is missing or shader is not " + ShaderName + "."; return false; }
            if (c.layout.FindAnchor("CAMPFIRE_Point") == null) { issue = "CAMPFIRE_Point layout anchor missing."; return false; }
            if (c.layout.pondRadius.x <= 0.1f || c.layout.pondRadius.y <= 0.1f) { issue = "Invalid pond radius."; return false; }
            if (c.layout.riverPoints == null || c.layout.riverPoints.Count < 2) { issue = "River path missing."; return false; }
            foreach (Transform t in c.island.GetComponentsInChildren<Transform>(true))
            {
                if (t.IsChildOf(c.existing != null ? c.existing : c.manual) && c.existing != null) continue;
                foreach (string old in OldNames)
                    if (t.name == old && t.gameObject.activeSelf) c.blockers.Add(PathOf(t));
            }
            if (c.scene.path.Contains("_Recovery")) c.warnings.Add("Active scene in _Recovery; verify it is intentionally selected.");
            if (FindForest(c, ForestBiome.Birch) == null) c.warnings.Add("Birch ForestZone missing; forest-light placement uses the PAD_Forest region fallback.");
            if (FindForest(c, ForestBiome.Cedar) == null) c.warnings.Add("Cedar ForestZone missing; forest-dark placement uses the PAD_Forest region fallback.");
            if (c.blockers.Count > 0) c.warnings.Add("Remove old CENTER patches before Apply to avoid visual stacking.");
            return true;
        }

        private static ForestZone FindForest(Context c, ForestBiome biome)
        {
            ForestZone best = null;
            float bestArea = -1f;
            foreach (var root in c.scene.GetRootGameObjects())
            {
                foreach (var zone in root.GetComponentsInChildren<ForestZone>(true))
                {
                    if (zone == null || zone.biome != biome || zone.Map == null) continue;
                    float area = zone.Map.Area;
                    if (area > bestArea) { best = zone; bestArea = area; }
                }
            }
            return best;
        }

        private static bool Ground(Context c, float x, float z, out Vector3 pos, float minEdge = 3f, float pathClear = 2.8f)
        {
            pos = Vector3.zero;
            IslandField f = c.field;
            if (!f.Inside(x, z) || f.EdgeDistance(x, z) < minEdge || f.IsWater(x, z) ||
                f.WaterDistance(x, z) < 1.8f || f.PathAxisDistance(x, z) < pathClear || f.Slope(x, z) > 26f)
                return false;
            foreach (var a in c.layout.anchors)
            {
                Vector2 ap = f.AnchorWorld(a);
                float buffer = a.kind == IslandLayout.AnchorKind.Building ? 3.0f : 2.5f;
                if (a.name == "CAMPFIRE_Point") buffer = 7.4f;
                if (a.name == "SPAWN_Player") buffer = 4.5f;
                if (a.name == "PAD_Forest") buffer = 16.0f; // preserve MysticGlade runtime clearing
                if (a.kind == IslandLayout.AnchorKind.Building)
                { if (f.RectDistance(a, x, z) < buffer) return false; }
                else if (Vector2.Distance(ap, new Vector2(x, z)) < buffer) return false;
            }
            foreach (var meadow in c.layout.meadows)
                if (Vector2.Distance(new Vector2(x, z), meadow.pos) < Mathf.Min(meadow.radius * 0.50f, 3.5f)) return false;
            float expected = f.Height(x, z);
            RaycastHit hit;
            if (!c.terrain.Raycast(new Ray(new Vector3(x, expected + 18f, z), Vector3.down), out hit, 55f)) return false;
            if (Mathf.Abs(expected - hit.point.y) > 0.65f) return false;
            pos = hit.point;
            return true;
        }

        private static Mesh BuildRegion(Context c, Region region, out int placements)
        {
            placements = 0;
            var mb = new Model();
            var rng = new System.Random(c.layout.seed * 2137 + (int)region * 911 + 619);
            Vector2 pc = c.field.AnchorWorld(c.layout.FindAnchor("CAMPFIRE_Point"));
            if (region == Region.Camp)
            {
                // Framed outer-plaza clusters: cohesive stone / warm wood / soft flowers.
                // Never touch the campfire itself, plaza mesh, colliders or four main routes.
                for (int i = 0; i < 50 && placements < 15; i++)
                {
                    float a = (float)(rng.NextDouble() * Mathf.PI * 2);
                    float d = 7.5f + (float)rng.NextDouble() * 2.4f;
                    float x = pc.x + Mathf.Cos(a) * d, z = pc.y + Mathf.Sin(a) * d;
                    Vector3 p;
                    if (!Ground(c, x, z, out p, 3.4f, 3.1f)) continue;
                    if (i % 3 == 0) mb.LogPile(p, 0.72f, rng);
                    else if (i % 3 == 1) mb.Garden(p, 0.9f, rng, Region.Camp);
                    else mb.Rocks(p, 0.85f, rng, Region.Camp);
                    placements++;
                }
            }
            else if (region == Region.Pond)
            {
                Vector2 ctr = c.layout.pondPos;
                for (int i = 0; i < 240 && placements < 38; i++)
                {
                    float a = (float)(rng.NextDouble() * Mathf.PI * 2);
                    float r = 1.23f + (float)rng.NextDouble() * 0.68f;
                    float x = ctr.x + Mathf.Cos(a) * c.layout.pondRadius.x * r;
                    float z = ctr.y + Mathf.Sin(a) * c.layout.pondRadius.y * r;
                    Vector3 p;
                    if (!Ground(c, x, z, out p, 3.6f, 2.8f)) continue;
                    if (i % 4 == 0) mb.Rocks(p, 0.82f, rng, Region.Pond);
                    else if (i % 3 == 0) mb.Garden(p, 0.9f, rng, Region.Pond);
                    else mb.Reeds(p, 0.9f, rng);
                    placements++;
                }
            }
            else if (region == Region.River)
            {
                List<Vector2> rp = c.layout.riverPoints;
                for (int i = 0; i < 320 && placements < 45; i++)
                {
                    int seg = rng.Next(rp.Count - 1);
                    Vector2 dir = rp[seg + 1] - rp[seg];
                    if (dir.sqrMagnitude < 0.01f) continue;
                    Vector2 pos = Vector2.Lerp(rp[seg], rp[seg + 1], (float)rng.NextDouble());
                    Vector2 normal = new Vector2(-dir.y, dir.x).normalized;
                    float side = rng.Next(2) == 0 ? -1 : 1;
                    pos += normal * side * (c.layout.riverWidth * 0.5f + 3.2f + (float)rng.NextDouble() * 3.5f);
                    Vector3 p;
                    if (!Ground(c, pos.x, pos.y, out p, 3.5f, 3f)) continue;
                    if (i % 4 == 0) mb.Rocks(p, 1f, rng, Region.River);
                    else if (i % 3 == 0) mb.Garden(p, 0.75f, rng, Region.River);
                    else mb.Reeds(p, 0.95f, rng);
                    placements++;
                }
            }
            else if (region == Region.ForestLight || region == Region.ForestDark)
            {
                ForestBiome biome = region == Region.ForestLight ? ForestBiome.Birch : ForestBiome.Cedar;
                ForestZone forestZone = FindForest(c, biome);
                Vector2 ctr; float rad;
                if (forestZone != null && forestZone.Map != null)
                {
                    Vector3 p = forestZone.transform.position;
                    ctr = new Vector2(p.x, p.z);
                    rad = forestZone.Map.shape == MapZone.Shape.Circle ? forestZone.Map.radius :
                          Mathf.Min(forestZone.Map.size.x, forestZone.Map.size.y) * 0.5f;
                    rad *= 0.75f;
                }
                else
                {
                    // The explicit ForestGate and PAD_Forest anchors define the north forest.
                    var an = c.layout.FindAnchor("PAD_Forest");
                    if (an == null) return null;
                    Vector2 anchor = c.field.AnchorWorld(an);
                    ctr = anchor + (region == Region.ForestLight ? new Vector2(17f, -12f) : new Vector2(-17f, 12f));
                    rad = 16f;
                }
                for (int i = 0; i < 290 && placements < 45; i++)
                {
                    float a = (float)(rng.NextDouble() * Mathf.PI * 2);
                    float r = Mathf.Sqrt((float)rng.NextDouble()) * rad;
                    float x = ctr.x + Mathf.Cos(a) * r;
                    float z = ctr.y + Mathf.Sin(a) * r;
                    Vector3 p;
                    if (!Ground(c, x, z, out p, 4.0f, 3.0f)) continue;
                    if (i % 5 == 0) mb.Rocks(p, 0.85f, rng, region);
                    else mb.Garden(p, 1.0f, rng, region);
                    placements++;
                }
            }
            else if (region == Region.Mystic)
            {
                var a = c.layout.FindAnchor("PAD_Forest");
                if (a == null) return null;
                Vector2 center = c.field.AnchorWorld(a);
                // MysticGladeBuilder may relocate trees at runtime in radius 15m; stay outside.
                for (int i = 0; i < 250 && placements < 30; i++)
                {
                    float ang = (float)(rng.NextDouble() * Mathf.PI * 2);
                    float radius = 18f + (float)rng.NextDouble() * 8f;
                    float x = center.x + Mathf.Cos(ang) * radius;
                    float z = center.y + Mathf.Sin(ang) * radius;
                    Vector3 p;
                    if (!Ground(c, x, z, out p, 4f, 3.1f)) continue;
                    if (i % 4 == 0) mb.Rocks(p, 0.95f, rng, region);
                    else mb.Garden(p, 0.9f, rng, region);
                    placements++;
                }
            }
            else if (region == Region.Edge)
            {
                var contour = c.field.Contour;
                for (int i = 0; i < contour.Count && placements < 50; i++)
                {
                    if (i % 2 != 0) continue;
                    Vector2 a = contour[i];
                    Vector2 inward = (c.layout.centerOffset - a).normalized;
                    float dist = 4.2f + (float)rng.NextDouble() * 2.8f;
                    Vector2 q = a + inward * dist;
                    Vector3 p;
                    if (!Ground(c, q.x, q.y, out p, 3f, 3.4f)) continue;
                    if (i % 4 == 0) mb.Garden(p, 0.75f, rng, region);
                    else mb.Rocks(p, 1.1f, rng, region);
                    placements++;
                }
            }
            return mb.Create("TW_AO_" + region);
        }

        private static string AssetPath(Region zone) { return AssetsFolder + "/TW_AO_" + zone + ".asset"; }
        private static string PathOf(Transform t)
        {
            string s = t.name;
            while (t.parent != null) { t = t.parent; s = t.name + "/" + s; }
            return s;
        }
        private static string ProjectRoot() { return Directory.GetParent(Application.dataPath).FullName; }
        private static string BackupScene(Scene scene)
        {
            string source = Path.Combine(ProjectRoot(), scene.path);
            if (!File.Exists(source)) throw new FileNotFoundException("Scene does not exist on disk", source);
            string path = Path.Combine(ProjectRoot(), "TW_ArtOverhaul_Backups");
            Directory.CreateDirectory(path);
            string target = Path.Combine(path, Path.GetFileNameWithoutExtension(scene.path) + "_before_AO_" +
                                             DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".unity");
            File.Copy(source, target, false);
            return target;
        }
        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Generated"))
                AssetDatabase.CreateFolder("Assets/_Project", "Generated");
            if (!AssetDatabase.IsValidFolder(AssetsFolder))
                AssetDatabase.CreateFolder("Assets/_Project/Generated", "TWIslandArtOverhaulV1");
        }

        // All geometry is hard-edged low-poly vertex color; one mesh per zone, no materials created.
        private sealed class Model
        {
            private readonly List<Vector3> v = new List<Vector3>(8192);
            private readonly List<Color> col = new List<Color>(8192);
            private readonly List<int> indices = new List<int>(16384);
            private int V(Vector3 p, Color c) { int id = v.Count; v.Add(p); col.Add(c); return id; }
            private void Tri(Vector3 a, Vector3 b, Vector3 c, Color color)
            {
                int i = V(a, color), j = V(b, color), k = V(c, color);
                indices.Add(i); indices.Add(j); indices.Add(k);
            }
            private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color co)
            {
                Tri(a, b, c, co); Tri(a, c, d, co);
            }
            private static Color C(string hex)
            {
                Color c;
                return ColorUtility.TryParseHtmlString(hex, out c) ? c : Color.white;
            }
            // Flat faceted stone / rounded shrub derived from low-poly octagonal rings.
            private void Pebble(Vector3 p, Vector3 size, Color body)
            {
                Vector3 top = p + new Vector3(0f, size.y, 0f);
                Vector3 low = p + new Vector3(0f, 0.025f, 0f);
                Vector3[] a = new Vector3[8];
                for (int i = 0; i < 8; i++)
                {
                    float phi = Mathf.PI * 2f * i / 8f;
                    a[i] = p + new Vector3(Mathf.Cos(phi) * size.x, size.y * 0.33f, Mathf.Sin(phi) * size.z);
                }
                for (int i = 0; i < 8; i++)
                {
                    int next = (i + 1) % 8;
                    Color shade = i % 3 == 0 ? body * 0.90f : (i % 3 == 1 ? body : body * 1.04f);
                    shade.a = 1f;
                    Vector3 outward = (a[i] + a[next]) * 0.5f - p + Vector3.up * .15f;
                    if (Vector3.Dot(Vector3.Cross(top - a[i], a[next] - a[i]), outward) < 0f)
                        Tri(a[next], top, a[i], shade);
                    else Tri(a[i], top, a[next], shade);
                    Tri(low, a[next], a[i], body * 0.76f);
                }
            }
            private void Reed(Vector3 p, float h, Color stem, Color tip)
            {
                Vector3 up = p + Vector3.up * h;
                float d = 0.032f;
                Vector3 side = Vector3.right * d;
                Quad(p - side, p + side, up + side * 0.5f, up - side * 0.5f, stem);
                Quad(p - Vector3.forward * d, up - Vector3.forward * d * 0.5f,
                     up + Vector3.forward * d * 0.5f, p + Vector3.forward * d, stem);
                Pebble(up, new Vector3(0.065f, 0.21f, 0.065f), tip);
            }
            private void QuadFacing(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color co, Vector3 outward)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f)
                    Quad(a, d, c, b, co);
                else Quad(a, b, c, d, co);
            }
            private void Box(Vector3 p, Vector3 sz, Color co, float yaw)
            {
                var q = Quaternion.Euler(0f, yaw, 0f);
                Vector3 e = sz * .5f;
                Vector3[] t = new Vector3[8];
                for (int i = 0; i < 8; i++)
                    t[i] = p + q * new Vector3((i & 1) == 0 ? -e.x : e.x,
                                                (i & 2) == 0 ? -e.y : e.y,
                                                (i & 4) == 0 ? -e.z : e.z);
                QuadFacing(t[0], t[4], t[5], t[1], co * 0.78f, q * Vector3.down);
                QuadFacing(t[2], t[3], t[7], t[6], co * 1.05f, q * Vector3.up);
                QuadFacing(t[0], t[2], t[6], t[4], co * 0.90f, q * Vector3.left);
                QuadFacing(t[1], t[5], t[7], t[3], co, q * Vector3.right);
                QuadFacing(t[0], t[1], t[3], t[2], co * 0.95f, q * Vector3.back);
                QuadFacing(t[4], t[6], t[7], t[5], co * 0.97f, q * Vector3.forward);
            }
            private void Branch(Vector3 a, Vector3 b, float width, Color bark)
            {
                Vector3 d = b - a;
                if (d.sqrMagnitude < 0.00001f) return;
                Quaternion r = Quaternion.FromToRotation(Vector3.up, d.normalized);
                Vector3 e = r * Vector3.right * width;
                Vector3 f = r * Vector3.forward * width;
                Quad(a + e + f, a - e + f, b - e * 0.8f + f * 0.8f, b + e * 0.8f + f * 0.8f, bark);
                Quad(a - e - f, a + e - f, b + e * 0.8f - f * 0.8f, b - e * 0.8f - f * 0.8f, bark * 0.85f);
                Quad(a + e - f, a + e + f, b + e * 0.8f + f * 0.8f, b + e * 0.8f - f * 0.8f, bark * 0.9f);
                Quad(a - e + f, a - e - f, b - e * 0.8f - f * 0.8f, b - e * 0.8f + f * 0.8f, bark * 0.96f);
            }
            public void Rocks(Vector3 p, float scale, System.Random rng, Region region)
            {
                Color stone = region == Region.Mystic ? C("#B5ADC9") : C("#C2B8C0");
                if (region == Region.Edge) stone = C("#A9A4B5");
                if (region == Region.Pond || region == Region.River) stone = C("#B8BAC4");
                for (int i = 0; i < 3; i++)
                {
                    float a = (float)(rng.NextDouble() * Mathf.PI * 2);
                    float r = 0.11f + i * 0.19f;
                    float s = (0.22f + (float)rng.NextDouble() * 0.25f) * scale;
                    Pebble(p + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r),
                           new Vector3(s, s * 0.56f, s * .79f), stone * (0.85f + i * 0.06f));
                }
                Pebble(p + new Vector3(0.1f, .02f, .20f), new Vector3(.18f, .13f, .17f) * scale,
                       region == Region.Mystic ? C("#96AFA6") : C("#91AF88"));
            }
            public void Garden(Vector3 p, float scale, System.Random rng, Region region)
            {
                Color green = region == Region.ForestDark ? C("#527E69") :
                              region == Region.Mystic ? C("#739D93") : C("#8BB77B");
                Color accent = region == Region.Mystic ? C("#C3A5CE") :
                               region == Region.ForestDark ? C("#B3B6A3") : C("#E3B6C6");
                for (int i = 0; i < 4; i++)
                {
                    float ang = (float)(rng.NextDouble() * Mathf.PI * 2);
                    float rr = (float)rng.NextDouble() * .42f;
                    Vector3 o = p + new Vector3(Mathf.Cos(ang) * rr, 0f, Mathf.Sin(ang) * rr);
                    float r = (0.20f + (float)rng.NextDouble() * .16f) * scale;
                    Pebble(o, new Vector3(r, r * 1.15f, r * .86f), green * (0.88f + i * .04f));
                }
                for (int i = 0; i < 3; i++)
                {
                    float a = i * 2.094f + (float)rng.NextDouble();
                    Vector3 o = p + new Vector3(Mathf.Cos(a) * .45f, 0f, Mathf.Sin(a) * .45f) * scale;
                    Branch(o, o + Vector3.up * (.28f + .07f * i) * scale, .015f, C("#67926B"));
                    Pebble(o + Vector3.up * (.28f + .07f * i) * scale,
                           new Vector3(.09f, .075f, .09f) * scale, i == 0 ? accent : C("#E7D39C"));
                }
            }
            public void Reeds(Vector3 p, float scale, System.Random rng)
            {
                Color leaf = C("#799D81"), head = C("#BAA27E");
                for (int i = 0; i < 5; i++)
                {
                    float ang = (float)(rng.NextDouble() * Mathf.PI * 2);
                    float rr = (float)rng.NextDouble() * .43f;
                    Reed(p + new Vector3(Mathf.Cos(ang) * rr, .015f, Mathf.Sin(ang) * rr),
                         (.44f + (float)rng.NextDouble() * .34f) * scale, leaf, head);
                }
                Pebble(p, new Vector3(.22f, .11f, .21f) * scale, C("#8FAF8D"));
            }
            public void LogPile(Vector3 p, float scale, System.Random rng)
            {
                Color dark = C("#8A644C"), light = C("#C39A75");
                float yaw = (float)rng.NextDouble() * 360f;
                var q = Quaternion.Euler(0f, yaw, 0f);
                for (int i = 0; i < 3; i++)
                {
                    Vector3 o = p + q * new Vector3((i - 1) * .23f * scale, .10f + (i == 1 ? .15f : 0f), 0f);
                    Box(o, new Vector3(.19f, .19f, .72f) * scale, i == 1 ? light : dark, yaw);
                }
                Garden(p + q * new Vector3(.55f * scale, 0f, .14f * scale), .42f * scale, rng, Region.Camp);
            }
            public Mesh Create(string name)
            {
                if (v.Count == 0 || indices.Count == 0) return null;
                var mesh = new Mesh { name = name, indexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                mesh.SetVertices(v); mesh.SetColors(col); mesh.SetTriangles(indices, 0, true);
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
#endif
