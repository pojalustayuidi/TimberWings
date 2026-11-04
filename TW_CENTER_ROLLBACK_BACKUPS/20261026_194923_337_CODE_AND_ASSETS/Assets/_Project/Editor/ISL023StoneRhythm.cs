#if UNITY_EDITOR
// Timber Wings / ISL-023 / Stone rhythm for the CAMPFIRE plaza.
// Independent, intentionally small and reversible EDITOR-ONLY decoration layer.
// Does not replace IslandBlockout.Plaza.cs or IslandBlockout.PlazaCandy.cs.
// Its extra mesh has NO COLLIDER and it never moves existing GameObjects.
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
    public static class ISL023StoneRhythm
    {
        private const string Menu = "Tools/BuilderSim/Локация/Остров v2/ISL-023/";
        private const string LayoutPath = "Assets/_Project/IslandV2/IslandLayout.asset";
        private const string PlazaPath = "IslandV2/_Generated/CampPlaza";
        private const string OverlayName = "ISL023_StoneRhythm";
        private const string OverlayMeshName = "ISL023_StoneRhythm_Mesh";
        private const string ReportName = "ISL-023_StoneRhythm_Audit.txt";
        private const float SurfaceOffset = 0.016f; // 16 mm above the original stone surface

        private struct Context
        {
            public Scene scene;
            public IslandLayout layout;
            public Transform plaza;
            public MeshRenderer sourceRenderer;
            public IslandField field;
        }

        [MenuItem(Menu + "1. Аудит площади (без изменений)", false, 231)]
        private static void Audit()
        {
            var log = new StringBuilder();
            log.AppendLine("TIMBER WINGS — ISL-023 / PLAZA STONE RHYTHM AUDIT");
            log.AppendLine("UTC: " + DateTime.UtcNow.ToString("O"));
            log.AppendLine("Unity: " + Application.unityVersion);
            log.AppendLine("No scene, mesh, material, collider or save-game modified by this audit.");
            Context ctx;
            string problem;
            bool valid = TryGetContext(out ctx, out problem);
            Scene scene = SceneManager.GetActiveScene();
            log.AppendLine("Active scene: " + scene.path);
            log.AppendLine("Scene in _Recovery: " + (scene.path.Contains("_Recovery") ? "YES - UNVERIFIED SCENE" : "no"));
            log.AppendLine("Preconditions: " + (valid ? "OK" : "FAILED: " + problem));
            if (valid)
            {
                Transform prior = ctx.plaza.Find(OverlayName);
                float r = Mathf.Max(2.5f, ctx.layout.plazaRadius);
                int rings = Mathf.Max(2, Mathf.RoundToInt(((r - 0.45f) - 1.5f) / 1.05f));
                float outerMid = 1.5f + (rings - 0.5f) * ((r - 0.45f - 1.5f) / rings);
                log.AppendLine("Plaza: " + PathOf(ctx.plaza));
                log.AppendLine("Plaza radius: " + r.ToString("F3") + " m; outer slab ring index: " + (rings - 1));
                log.AppendLine("Plaza center XZ: " + ctx.field.PlazaCenter);
                log.AppendLine("Outer ring center radius: " + outerMid.ToString("F3") + " m");
                log.AppendLine("Source material: " + ctx.sourceRenderer.sharedMaterial.name);
                log.AppendLine("Existing ISL-023 overlay: " + (prior == null ? "none" : PathOf(prior)));
                var candy = ctx.plaza.parent.Find(IslandBlockout.PlazaCandyName);
                log.AppendLine("Candy central mosaic: " + (candy == null ? "NOT FOUND (will not be created)" : "present, untouched"));
                log.AppendLine("Overlay: accents only on outermost existing stone ring; no central mosaic or path modifications.");
                log.AppendLine("Apply replaces ONLY an existing " + PlazaPath + "/" + OverlayName + " child, if present.");
                log.AppendLine("Apply adds one decorative MeshFilter/MeshRenderer (no collider, no new material asset or texture).");
                log.AppendLine("Full plaza rebuild / full island rebuild is NOT part of ISL-023.");
                log.AppendLine("On a full _Generated rebuild, the add-on will disappear; re-apply if desired.");
            }
            string location = Path.Combine(Directory.GetParent(Application.dataPath).FullName, ReportName);
            try { File.WriteAllText(location, log.ToString(), Encoding.UTF8); }
            catch (Exception e) { Debug.LogWarning("ISL-023: cannot write report: " + e.Message); }
            Debug.Log(log.ToString() + "\nReport: " + location);
            if (!valid) Debug.LogWarning("ISL-023: audit found a blocker. Apply is unavailable until resolved.");
        }

        [MenuItem(Menu + "2. Применить мягкий ритм плит (только декор)", false, 232)]
        private static void Apply()
        {
            Context ctx;
            string problem;
            if (!TryGetContext(out ctx, out problem))
            {
                Debug.LogError("ISL-023: cannot apply. " + problem);
                return;
            }
            var old = ctx.plaza.Find(OverlayName);
            string description = "Сцена: " + ctx.scene.path + "\n\n" +
                "Будет " + (old == null ? "создан" : "ЗАМЕНЁН") + " только объект\n" + PlazaPath + "/" + OverlayName +
                "\n\nПоявится один визуальный меш поверх внешнего ряда плит, без Collider. " +
                "Не меняются CampPlaza/Plaza_Mesh, CampPlazaCandy, костёр, NPC, пути, маркеры, свет, Terrain, ассеты, сохранения.\n\n" +
                "Рабочая сцена должна иметь Git/backup. Не запускать полную пересборку.";
            if (ctx.scene.path.Contains("_Recovery"))
                description = "ВНИМАНИЕ: ОТКРЫТА _RECOVERY-СЦЕНА, НЕ ПОДТВЕРЖДЁННАЯ КАК ОСНОВНАЯ.\n" +
                    "Продолжать только если это сознательно выбранная копия сцены.\n\n" + description;
            if (!EditorUtility.DisplayDialog("ISL-023: подтвердить точечное изменение", description, "Применить к ЭТОЙ сцене", "Отмена"))
                return;

            var mesh = BuildAccentMesh(ctx);
            if (mesh == null || mesh.vertexCount < 3)
            {
                if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
                Debug.LogError("ISL-023: no viable accent mesh; old geometry left unchanged.");
                return;
            }
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("ISL-023: stone rhythm decoration");
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            GameObject host = new GameObject(OverlayName);
            Undo.RegisterCreatedObjectUndo(host, "ISL-023: create stone rhythm");
            host.transform.SetParent(ctx.plaza, false);
            host.transform.localPosition = Vector3.zero;
            host.transform.localRotation = Quaternion.identity;
            host.transform.localScale = Vector3.one;
            GameObject visual = new GameObject(OverlayMeshName);
            Undo.RegisterCreatedObjectUndo(visual, "ISL-023: create visual mesh");
            visual.transform.SetParent(host.transform, false);
            MeshFilter mf = Undo.AddComponent<MeshFilter>(visual);
            mf.sharedMesh = mesh; // transient scene mesh, NOT a project asset
            MeshRenderer mr = Undo.AddComponent<MeshRenderer>(visual);
            mr.sharedMaterial = ctx.sourceRenderer.sharedMaterial; // shared, never modified
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = true;
            GameObjectUtility.SetStaticEditorFlags(visual, StaticEditorFlags.BatchingStatic);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(ctx.scene);
            Debug.Log("ISL-023: applied to " + ctx.scene.path + ": " + PathOf(visual.transform) +
                      ", triangles=" + mesh.triangles.Length / 3 + ". 0 colliders; no asset changes. " +
                      "Use menu 3 to remove, or Ctrl+Z immediately to undo. " +
                      "Scene not saved automatically.");
        }

        [MenuItem(Menu + "3. Убрать только декор ISL-023", false, 233)]
        private static void Remove()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("ISL-023: leave Play Mode before removing the overlay.");
                return;
            }
            Scene scene = SceneManager.GetActiveScene();
            var root = GameObject.Find("IslandV2");
            Transform overlay = root == null ? null : root.transform.Find("_Generated/CampPlaza/" + OverlayName);
            if (overlay == null)
            {
                Debug.Log("ISL-023: overlay not found; nothing to remove.");
                return;
            }
            if (overlay.gameObject.scene != scene)
            {
                Debug.LogError("ISL-023: overlay is not in active scene. Aborted.");
                return;
            }
            if (!EditorUtility.DisplayDialog("ISL-023: убрать декор", "Удалить ТОЛЬКО " + PathOf(overlay) + "?\n" +
                "Это визуальное дополнение без коллайдеров. Остальная площадь не затрагивается.", "Удалить дополнение", "Отмена"))
                return;
            Undo.DestroyObjectImmediate(overlay.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("ISL-023: only " + OverlayName + " removed. Original plaza untouched.");
        }

        private static bool TryGetContext(out Context ctx, out string problem)
        {
            ctx = new Context(); problem = null;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                problem = "Exit Play Mode."; return false;
            }
            ctx.scene = SceneManager.GetActiveScene();
            if (!ctx.scene.IsValid() || !ctx.scene.isLoaded || string.IsNullOrEmpty(ctx.scene.path))
            {
                problem = "Open a saved scene."; return false;
            }
            ctx.layout = AssetDatabase.LoadAssetAtPath<IslandLayout>(LayoutPath);
            if (ctx.layout == null)
            {
                problem = "Missing layout asset: " + LayoutPath; return false;
            }
            GameObject root = GameObject.Find("IslandV2");
            if (root == null || root.scene != ctx.scene)
            {
                problem = "Active scene lacks IslandV2 root."; return false;
            }
            ctx.plaza = root.transform.Find("_Generated/CampPlaza");
            if (ctx.plaza == null)
            {
                problem = "Missing " + PlazaPath + ". Never run a full builder for this patch."; return false;
            }
            Transform slab = ctx.plaza.Find("Plaza_Mesh");
            MeshFilter mf = slab == null ? null : slab.GetComponent<MeshFilter>();
            ctx.sourceRenderer = slab == null ? null : slab.GetComponent<MeshRenderer>();
            if (mf == null || mf.sharedMesh == null || ctx.sourceRenderer == null || ctx.sourceRenderer.sharedMaterial == null)
            {
                problem = "Plaza_Mesh or its material is missing."; return false;
            }
            // Source meshes are generated using field/world coordinates, with identity hierarchy.
            Transform gen = ctx.plaza.parent;
            if (!IdentityHierarchy(root.transform) || !IdentityHierarchy(gen) || !IdentityHierarchy(ctx.plaza)
                || !IdentityHierarchy(slab))
            {
                problem = "Non-identity IslandV2/_Generated/CampPlaza/Plaza_Mesh transform; cannot safely align overlay.";
                return false;
            }
            ctx.field = new IslandField(ctx.layout); // read-only calculations
            if (ctx.field.Contour == null || ctx.field.Contour.Count < 3)
            {
                problem = "Island contour unavailable."; return false;
            }
            return true;
        }

        private static bool IdentityHierarchy(Transform tr)
        {
            if (tr == null) return false;
            return tr.localPosition.sqrMagnitude < 0.000001f &&
                   Quaternion.Angle(tr.localRotation, Quaternion.identity) < 0.001f &&
                   (tr.localScale - Vector3.one).sqrMagnitude < 0.000001f;
        }

        private static string PathOf(Transform t)
        {
            string value = t.name;
            while (t.parent != null) { t = t.parent; value = t.name + "/" + value; }
            return value;
        }

        // Accents align exactly with the original generator's outermost slab segmentation
        // (same n/off/height evaluation) and are placed just over the slab tops.
        // Each piece covers only the outer ~0.3 m of an existing slab. Never reaches
        // the Candy mosaic (r <= ~4.08 m at the current radius 5.2 m).
        private static Mesh BuildAccentMesh(Context ctx)
        {
            float R = Mathf.Max(2.5f, ctx.layout.plazaRadius);
            float rIn = 1.5f;
            float rOut = R - 0.45f;
            int rings = Mathf.Max(2, Mathf.RoundToInt((rOut - rIn) / 1.05f));
            float ringW = (rOut - rIn) / rings;
            int i = rings - 1;
            float r0 = rIn + i * ringW;
            float r1 = r0 + ringW;
            float rm = (r0 + r1) * 0.5f;
            int n = Mathf.Max(7, Mathf.RoundToInt(2f * Mathf.PI * rm / 1.25f));
            float off = (i % 2) * 0.5f;
            float gap = 0.05f / rm;
            Vector2 center = ctx.field.PlazaCenter;
            var verts = new List<Vector3>(n * 4);
            var colors = new List<Color>(n * 4);
            var tris = new List<int>(n * 6);
            // Harmonized with ISL-018's adopted warmer, desaturated stone palette.
            Color[] palette = {
                HexLinear("#EFE4D5"), HexLinear("#E7D1BD"),
                HexLinear("#D9C9D6"), HexLinear("#E9DDD0")
            };
            for (int k = 0; k < n; k++)
            {
                float a0 = (k + off) * Mathf.PI * 2f / n;
                float a1 = (k + 1 + off) * Mathf.PI * 2f / n;
                float a = (a0 + a1) * 0.5f;
                float x = center.x + Mathf.Cos(a) * (r1 - 0.19f);
                float z = center.y + Mathf.Sin(a) * (r1 - 0.19f);
                // Preserve four clean route entrances. This is ONLY a visual skip.
                if (ctx.field.PathAxisDistance(x, z) < 1.8f) continue;
                // Grouped pulse: two short highlights / one quiet / one muted warm accent.
                // Visual detail remains soft and secondary to the fire + Candy mosaic.
                int group = k % 6;
                if (group == 3) continue;
                float inner = Mathf.Max(r0 + 0.13f, r1 - (group == 0 ? 0.47f : 0.37f));
                float outer = r1 - 0.105f;
                if (inner >= outer - 0.03f) continue;
                float aPad = gap + 0.035f;
                float theta0 = a0 + aPad, theta1 = a1 - aPad;
                if (theta1 <= theta0) continue;
                // Original slab top is height sampled at slab center, flat per slab.
                float h = ctx.field.Height(center.x + Mathf.Cos(a) * rm,
                                           center.y + Mathf.Sin(a) * rm) + 0.07f + SurfaceOffset;
                int first = verts.Count;
                verts.Add(Polar(center, inner, theta0, h));
                verts.Add(Polar(center, outer, theta0, h));
                verts.Add(Polar(center, outer, theta1, h));
                verts.Add(Polar(center, inner, theta1, h));
                Color col = palette[group == 0 ? 1 : group == 1 ? 0 : group == 2 ? 3 : 2];
                for (int p = 0; p < 4; p++) colors.Add(col);
                // XZ increasing angle winds down; reverse winding so the face looks UP.
                tris.Add(first); tris.Add(first + 2); tris.Add(first + 1);
                tris.Add(first); tris.Add(first + 3); tris.Add(first + 2);
            }
            if (verts.Count < 3) return null;
            Mesh m = new Mesh { name = "ISL023StoneRhythm" };
            m.SetVertices(verts);
            m.SetColors(colors);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        private static Vector3 Polar(Vector2 c, float radius, float a, float y)
        {
            return new Vector3(c.x + Mathf.Cos(a) * radius, y, c.y + Mathf.Sin(a) * radius);
        }

        private static Color HexLinear(string hex)
        {
            Color col;
            if (!ColorUtility.TryParseHtmlString(hex, out col)) return Color.white;
            return col.linear;
        }
    }
}
#endif
