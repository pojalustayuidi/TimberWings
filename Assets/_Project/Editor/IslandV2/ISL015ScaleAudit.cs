// Assets/_Project/Editor/IslandV2/ISL015ScaleAudit.cs
// Timber Wings / ISL-015. Read-only scale audit for the currently open scene.
// No generators, prefabs, scenes, transforms, colliders, or materials are modified.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IslandV2
{
    public static class ISL015ScaleAudit
    {
        const string Menu = "Tools/BuilderSim/Локация/Остров v2/ISL-015/1. Отчёт масштаба (только чтение)";
        const string ReportFile = "ISL-015_ScaleAudit.txt";
        const int MaxDetails = 160;

        static readonly string[] StructureNames =
        {
            "LumberjackHut", "TradingStall", "CampTent", "River_Bridge",
            "ChopGladeDecor", "BLK_Hut", "BLK_Market", "BLK_Tent",
            "BLK_QuestBoard", "BLK_Campfire"
        };

        static readonly string[] DetailTokens =
        {
            "door", "entrance", "threshold", "step", "stair", "bench", "seat",
            "rail", "fence", "counter", "window", "table", "lantern", "lamp_post",
            "lamp post", "handrail", "parapet", "col_gladebench", "col_parkbench"
        };

        [MenuItem(Menu, false, 215)]
        static void Run()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("[ISL-015] Нет загруженной активной сцены. Отчёт не создан.");
                return;
            }

            var sb = new StringBuilder(26000);
            sb.AppendLine("TIMBER WINGS / ISL-015 / OBJECT SCALE AUDIT");
            sb.AppendLine("Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            sb.AppendLine("Unity: " + Application.unityVersion);
            sb.AppendLine("Scene: " + scene.path + " (" + scene.name + ")");
            sb.AppendLine("Mode: " + (EditorApplication.isPlaying ? "PLAY MODE (measurements reflect current animation/stage)" : "EDIT MODE"));
            sb.AppendLine("Units: world units (assumed meters, not independently calibrated)");
            sb.AppendLine("Measurement: world axis-aligned renderer bounds (rotated objects may appear wider)");
            sb.AppendLine("Only active, enabled MeshRenderer/SkinnedMeshRenderer objects are used for visual bounds.");
            sb.AppendLine("All changes to scenes/assets/prefabs: NONE. Only report TXT is written outside Assets.");
            sb.AppendLine();

            var roots = scene.GetRootGameObjects();
            var transforms = new List<Transform>(4096);
            var players = new List<PlayerMovement>(3);
            foreach (GameObject go in roots)
            {
                transforms.AddRange(go.GetComponentsInChildren<Transform>(true));
                players.AddRange(go.GetComponentsInChildren<PlayerMovement>(true));
            }
            sb.AppendLine("SCENE TOTALS: roots=" + roots.Length + "; transforms=" + transforms.Count + "; PlayerMovement=" + players.Count);
            if (scene.path.Contains("_Recovery"))
                sb.AppendLine("WARNING: active scene is in _Recovery; verify it is your actual game scene.");
            sb.AppendLine();

            float heroHeight = 0f;
            sb.AppendLine("[1] PLAYER REFERENCE");
            if (players.Count == 0)
                sb.AppendLine("NOT FOUND: PlayerMovement component in the current loaded scene.");
            for (int i = 0; i < players.Count; i++)
            {
                PlayerMovement pm = players[i];
                Transform body = pm.model != null ? pm.model : pm.transform;
                Bounds visual;
                bool hasVisual = TryBounds(body, out visual);
                sb.AppendLine("Player " + (i + 1) + ": " + PathOf(pm.transform));
                sb.AppendLine("  Model reference: " + (pm.model != null ? PathOf(pm.model) : "not assigned; using PlayerMovement root"));
                sb.AppendLine("  Root localScale: " + V(pm.transform.localScale) + "; lossyScale: " + V(pm.transform.lossyScale));
                if (hasVisual)
                {
                    sb.AppendLine("  Visual bounds (W x H x D): " + V(visual.size));
                    sb.AppendLine("  Visual bottom/top Y: " + N(visual.min.y) + " / " + N(visual.max.y));
                    if (heroHeight <= 0f && visual.size.y >= 0.25f && visual.size.y <= 5f)
                        heroHeight = visual.size.y;
                }
                else sb.AppendLine("  Visual bounds: unavailable (model may be hidden or procedural at runtime).");

                var cap = pm.GetComponent<CapsuleCollider>();
                if (cap != null)
                    sb.AppendLine("  Root CapsuleCollider: height=" + N(cap.height * Mathf.Abs(cap.transform.lossyScale.y))
                        + "; diameter≈" + N(cap.radius * 2f * Mathf.Max(Mathf.Abs(cap.transform.lossyScale.x), Mathf.Abs(cap.transform.lossyScale.z)))
                        + "; isTrigger=" + cap.isTrigger);
                var col = pm.GetComponent<Collider>();
                if (cap == null && col != null)
                    sb.AppendLine("  Root collider: " + col.GetType().Name + "; bounds=" + V(col.bounds.size));
            }
            sb.AppendLine("REFERENCE HERO HEIGHT: " + (heroHeight > 0f ? N(heroHeight) + " m (visual bounds)" : "UNKNOWN"));
            sb.AppendLine("NOTE: animated/disabled children, weapon, hair, or HUD can bias measurements; inspect visually.");
            sb.AppendLine();

            sb.AppendLine("[2] MAJOR BUILDINGS AND BRIDGE (INCLUDING PLACEHOLDERS)");
            int structureCount = 0;
            foreach (Transform t in transforms)
            {
                if (!IsStructureName(t.name)) continue;
                // Avoid selecting accidentally named children inside another same-name structure.
                if (HasMatchingParent(t)) continue;
                structureCount++;
                AppendVisual(sb, t, heroHeight, "  ");
                var allCols = t.GetComponentsInChildren<Collider>(true);
                sb.AppendLine("     Colliders total=" + allCols.Length);
            }
            if (structureCount == 0) sb.AppendLine("  NOT FOUND: no known root names in this scene. Report cannot infer intended building scale.");
            sb.AppendLine();

            sb.AppendLine("[3] RIVER BRIDGE: PHYSICAL RAIL AND DECK COLLIDERS");
            Transform bridge = FirstNamed(transforms, "River_Bridge");
            if (bridge == null) sb.AppendLine("  River_Bridge not found.");
            else
            {
                int deckCount = 0, railCount = 0;
                foreach (var col in bridge.GetComponentsInChildren<BoxCollider>(true))
                {
                    if (col.name.StartsWith("Deck_", StringComparison.Ordinal))
                    {
                        deckCount++;
                        if (deckCount == 1) sb.AppendLine("  First deck collision box (world size approx): " + ScaledBox(col));
                    }
                    if (col.name.StartsWith("Rail_", StringComparison.Ordinal))
                    {
                        railCount++;
                        if (railCount == 1) sb.AppendLine("  First invisible safety rail (world size approx): " + ScaledBox(col));
                    }
                }
                sb.AppendLine("  Deck colliders=" + deckCount + "; rail colliders=" + railCount);
                sb.AppendLine("  SOURCE REFERENCE (not measured): BrWidth=3.2, BrRailH=1.1, BrLampH=2.0 (IslandBlockout.Bridge.cs)");
            }
            sb.AppendLine();

            sb.AppendLine("[4] NAMED DETAILS: DOORS / BENCHES / STEPS / RAILS / WINDOWS / COUNTERS");
            sb.AppendLine("  Note: when a generator combines parts into one mesh, individual dimensions CANNOT be recovered by name.");
            int detailCount = 0, detailOverflow = 0;
            foreach (Transform t in transforms)
            {
                if (!LooksLikeDetail(t.name)) continue;
                if (IsInsidePlayer(t, players)) continue;
                if (IsStructureName(t.name)) continue;
                if (detailCount >= MaxDetails) { detailOverflow++; continue; }
                Bounds b;
                var col = t.GetComponent<Collider>();
                bool vis = TryBounds(t, out b);
                if (!vis && col == null) continue;
                sb.AppendLine("  " + PathOf(t));
                if (vis) sb.AppendLine("    Visible W/H/D=" + V(b.size) + R(heroHeight, b.size.y));
                if (col != null) sb.AppendLine("    Collider " + col.GetType().Name + " W/H/D=" + V(col.bounds.size));
                detailCount++;
            }
            sb.AppendLine("  Detail entries=" + detailCount + (detailOverflow > 0 ? "; omitted candidates=" + detailOverflow : ""));
            sb.AppendLine();

            sb.AppendLine("[5] SELECTED TREE REFERENCES (SCENE ONLY)");
            sb.AppendLine("  Tree bounds are representative, not a standard for all species/LODs.");
            int treeCount = 0;
            foreach (Transform t in transforms)
            {
                if (treeCount >= 18) break;
                // Detect specific gameplay tree roots, not every 'leaf' child or every forest parent.
                if (t.GetComponent<ResourceItem>() == null) continue;
                Bounds b;
                if (!TryBounds(t, out b)) continue;
                sb.AppendLine("  " + PathOf(t) + " W/H/D=" + V(b.size) + R(heroHeight, b.size.y));
                treeCount++;
            }
            if (treeCount == 0) sb.AppendLine("  No renderable ResourceItem tree found in this scene.");
            sb.AppendLine();

            sb.AppendLine("[6] SOURCE DEFAULTS (NOT PROOF OF CURRENT SCENE DIMENSIONS)");
            sb.AppendLine("  TentBuilder.Settings: width=2.4, length=2.9, height=2.2, doorWidth=1.0 m");
            sb.AppendLine("  ShopBuilderV2.Settings: width=3.6, depth=2.6; PostH=2.2 m");
            sb.AppendLine("  HutBuilder.Settings: wallHeight=2.5 m");
            sb.AppendLine("  ForestGladeBeauty.Bench: board centre y≈0.41 m (source model)");
            sb.AppendLine("  These values come from Scripts(4).zip; assets and saved Editor settings may differ.");
            sb.AppendLine();

            sb.AppendLine("[7] HOW TO ASSESS");
            sb.AppendLine("  Compare avatar visual height against: door opening, step rise, bench seat, counter, bridge rail and roof eave.");
            sb.AppendLine("  Use one fixed Game View camera, same build stages, and test walking/interactions before any scaling.");
            sb.AppendLine("  If a name-only detail is absent, provide its generator/mesh asset; do not automatically scale a whole building.");
            sb.AppendLine("  World-axis-aligned bounds can overestimate width/depth for objects rotated in the scene.");
            sb.AppendLine("  No NavMesh, colliders, interaction distances, prefab links, save data or scene objects were touched.");

            string reportPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ReportFile));
            try
            {
                File.WriteAllText(reportPath, sb.ToString(), new UTF8Encoding(false));
                Debug.Log("[ISL-015] Отчёт масштаба создан: " + reportPath
                    + ". Зданий/мостов: " + structureCount + ", деталей: " + detailCount
                    + ", референс роста героя: " + (heroHeight > 0f ? N(heroHeight) + " м" : "не найден")
                    + ". Сцена и ассеты НЕ изменялись.");
                EditorUtility.RevealInFinder(reportPath);
            }
            catch (Exception ex)
            {
                Debug.LogError("[ISL-015] Не удалось записать отчёт: " + ex.Message);
            }
        }

        static bool IsStructureName(string name)
        {
            foreach (var n in StructureNames) if (name == n) return true;
            return false;
        }

        static bool HasMatchingParent(Transform t)
        {
            Transform p = t.parent;
            while (p != null) { if (IsStructureName(p.name)) return true; p = p.parent; }
            return false;
        }

        static bool IsInsidePlayer(Transform t, List<PlayerMovement> players)
        {
            foreach (var pm in players) if (pm != null && t.IsChildOf(pm.transform)) return true;
            return false;
        }

        static bool LooksLikeDetail(string name)
        {
            string s = name.ToLowerInvariant();
            foreach (string token in DetailTokens) if (s.Contains(token)) return true;
            return false;
        }

        static Transform FirstNamed(List<Transform> list, string name)
        {
            foreach (Transform t in list) if (t.name == name) return t;
            return null;
        }

        static bool TryBounds(Transform root, out Bounds result)
        {
            result = new Bounds();
            bool any = false;
            foreach (Renderer rend in root.GetComponentsInChildren<Renderer>(true))
            {
                if (rend == null || !rend.enabled || !rend.gameObject.activeInHierarchy) continue;
                if (!(rend is MeshRenderer) && !(rend is SkinnedMeshRenderer)) continue;
                if (rend is MeshRenderer)
                {
                    var mf = rend.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;
                }
                Bounds b = rend.bounds;
                Vector3 s = b.size;
                if (s.sqrMagnitude < 0.000001f ||
                    float.IsNaN(s.x) || float.IsNaN(s.y) || float.IsNaN(s.z) ||
                    float.IsInfinity(s.x) || float.IsInfinity(s.y) || float.IsInfinity(s.z)) continue;
                if (!any) { result = b; any = true; }
                else result.Encapsulate(b);
            }
            return any;
        }

        static void AppendVisual(StringBuilder sb, Transform t, float heroH, string indent)
        {
            sb.AppendLine(indent + PathOf(t));
            sb.AppendLine(indent + "  worldPosition=" + V(t.position) + "; lossyScale=" + V(t.lossyScale));
            Bounds b;
            if (TryBounds(t, out b))
                sb.AppendLine(indent + "  visible W/H/D=" + V(b.size) + R(heroH, b.size.y));
            else sb.AppendLine(indent + "  visible bounds unavailable (disabled/hidden/combined geometry).");
        }

        static string ScaledBox(BoxCollider b)
        {
            Vector3 s = b.transform.lossyScale;
            return V(new Vector3(Mathf.Abs(s.x * b.size.x), Mathf.Abs(s.y * b.size.y), Mathf.Abs(s.z * b.size.z)));
        }

        static string PathOf(Transform t)
        {
            var names = new List<string>(12);
            while (t != null) { names.Add(t.name); t = t.parent; }
            names.Reverse();
            return string.Join("/", names.ToArray());
        }

        static string R(float heroH, float objectH)
        {
            return heroH > 0f ? "; height / player=" + N(objectH / heroH) + "x" : "; ratio unavailable (no player visual height)";
        }

        static string V(Vector3 v) { return N(v.x) + " x " + N(v.y) + " x " + N(v.z); }
        static string N(float n) { return n.ToString("0.###", CultureInfo.InvariantCulture); }
    }
}
#endif
