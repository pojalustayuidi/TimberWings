#if UNITY_EDITOR
// TIMBER WINGS / CENTER COMPLETE SAFE V2
// Unity 6 / URP | one-file Editor tool, NO original source replacement.
// Built against the uploaded Scripts(5).zip and Center Complete v1 package.
// Strong safety rule: NO added Light, NO emissive/particle materials,
// NO changes to built-in Campfire geometry, colliders, saving, NPC or paths.
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
    public static class TWCenterCompleteSafeV2
    {
        private const string Menu = "Tools/BuilderSim/Локация/Остров v2/ЦЕНТР — SAFE v2/";
        private const string OldArtName = "TW_CenterComplete_Art";
        private const string OldFXName = "TW_CenterComplete_Atmosphere";
        private const string NewArtName = "TW_CenterSafeV2_Art";
        private const string NewSoundName = "TW_CenterSafeV2_Audio";
        private const string AssetsFolder = "Assets/_Project/Generated/TWCenterSafeV2";
        private const string InlayAssetPath = AssetsFolder + "/TW_CenterSafeV2_Inlay.asset";
        private const string BotanicalsAssetPath = AssetsFolder + "/TW_CenterSafeV2_Botanicals.asset";
        private const string AudioAssetPath = "Assets/_Project/Audio/TW_WarmCampfireLoop.wav";
        private const string ReportName = "TW_CENTER_SAFE_V2_Audit.txt";

        private struct Ctx
        {
            public Scene scene;
            public Transform campfire;
            public Transform stage3;
            public Transform plaza;
            public Transform oldArt;
            public Transform oldFX;
            public Material stoneMaterial;
            public AudioClip sound;
            public IslandField field;
            public Vector3 center;
        }

        [MenuItem(Menu + "1. Аудит перед сборкой (read-only)", false, 390)]
        private static void Audit()
        {
            Ctx c;
            string issue;
            bool valid = TryContext(out c, out issue);
            var lines = new StringBuilder();
            lines.AppendLine("TIMBER WINGS / CENTER COMPLETE SAFE V2 — READ ONLY");
            lines.AppendLine("UTC: " + DateTime.UtcNow.ToString("O"));
            lines.AppendLine("Unity: " + Application.unityVersion);
            var scene = SceneManager.GetActiveScene();
            lines.AppendLine("Active scene: " + scene.path);
            lines.AppendLine("Recovery scene: " + (scene.path.Contains("_Recovery") ? "YES / not verified as release scene" : "no"));
            lines.AppendLine("PRECONDITIONS: " + (valid ? "OK" : "FAILED: " + issue));
            if (valid)
            {
                lines.AppendLine("Campfire root: " + PathOf(c.campfire));
                lines.AppendLine("Plaza root: " + PathOf(c.plaza));
                lines.AppendLine("Legacy Complete art: " + Describe(c.oldArt));
                lines.AppendLine("Legacy Complete atmosphere: " + Describe(c.oldFX));
                lines.AppendLine("SAFE V2 art: " + Describe(c.plaza.Find(NewArtName)));
                lines.AppendLine("SAFE V2 audio: " + Describe(c.stage3.Find(NewSoundName)));
                lines.AppendLine("Plaza material: " + c.stoneMaterial.name + " / " + c.stoneMaterial.shader.name);
                lines.AppendLine("Existing Sparks: " + Describe(c.stage3.Find("Sparks")));
                lines.AppendLine("Existing Smoke: " + Describe(c.stage3.Find("Smoke")));
                lines.AppendLine("Existing CampLight: " + Describe(c.stage3.Find("CampLight")));
                lines.AppendLine("Audio clip: " + (c.sound == null ? "not available (will skip)" : c.sound.name));
                lines.AppendLine("Light components CREATED by this tool: 0");
                lines.AppendLine("Particle components CREATED by this tool: 0");
                lines.AppendLine("Emissive materials CREATED by this tool: 0");
                lines.AppendLine("Collider components CREATED by this tool: 0");
                lines.AppendLine("APPLY deactivates ONLY old v1 roots, then creates/replaces 2 SAFE V2 roots.");
                lines.AppendLine("APPLY NEVER edits base Campfire MeshFilter references, its lights, its stage scripts, terrain or save IDs.");
                var campLight = c.stage3.Find("CampLight");
                if (campLight != null)
                {
                    var light = campLight.GetComponent<Light>();
                    if (light != null)
                        lines.AppendLine("Existing CampLight: enabled=" + light.enabled + " intensity=" + light.intensity.ToString("0.###") + " range=" + light.range.ToString("0.###"));
                }
                lines.AppendLine("Scene Light inventory (do not assume combined brightness is safe):");
                int count = 0;
                foreach (var l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (l.gameObject.scene != scene || Vector3.Distance(l.transform.position, c.center) > 15f) continue;
                    lines.AppendLine(" - " + PathOf(l.transform) + ": enabled=" + l.enabled + ", active=" + l.gameObject.activeInHierarchy +
                        ", intensity=" + l.intensity.ToString("0.##") + ", range=" + l.range.ToString("0.##"));
                    count++;
                    if (count >= 32) break;
                }
            }
            lines.AppendLine("This is an Editor inventory, NOT a Play Mode / actual brightness / FPS validation.");
            string dst = Path.Combine(Directory.GetParent(Application.dataPath).FullName, ReportName);
            try { File.WriteAllText(dst, lines.ToString(), new UTF8Encoding(false)); }
            catch (Exception ex) { Debug.LogWarning("TW Center Safe V2: report save failed: " + ex.Message); }
            Debug.Log(lines.ToString() + "\nReport: " + dst);
        }

        [MenuItem(Menu + "2. АВАРИЙНО выключить старый COMPLETE (без удаления)", false, 391)]
        private static void EmergencyDisable()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("CENTER SAFE V2: exit Play Mode first. The button edits the scene, not runtime state.");
                return;
            }
            Ctx c;
            string error;
            if (!TryContext(out c, out error)) { Debug.LogError("CENTER SAFE V2: " + error); return; }
            if (c.oldArt == null && c.oldFX == null)
            {
                Debug.Log("CENTER SAFE V2: no old Complete roots to deactivate."); return;
            }
            if (!Confirm(c.scene, "Emergency OFF: old COMPLETE", 
                "Will SET ACTIVE FALSE ONLY for:\n" + LegacyPaths() +
                "\nNo object or asset will be deleted. Existing CampLight, base flames, sparks and smoke stay unchanged."))
                return;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Center Safe V2: mute legacy Complete");
            bool changed = DeactivateLegacy(c);
            Undo.CollapseUndoOperations(group);
            if (changed) EditorSceneManager.MarkSceneDirty(c.scene);
            Debug.Log("CENTER SAFE V2: all old CENTER COMPLETE visual/audio roots disabled (not deleted). Test Game View at same camera angle.");
        }

        [MenuItem(Menu + "3. СОБРАТЬ уютный центр SAFE v2", false, 392)]
        private static void Build()
        {
            Ctx c;
            string issue;
            if (!TryContext(out c, out issue)) { Debug.LogError("CENTER SAFE V2: " + issue); return; }
            string scope = "DISABLE ONLY (retain for rollback):\n" + LegacyPaths() +
                "\nDELETE/REPLACE ONLY existing OWNED V2 objects, when present:\n" +
                " IslandV2/_Generated/CampPlaza/" + NewArtName + "\n" +
                " Campfire/Stage3/" + NewSoundName + "\n" +
                "\nCREATE/UPDATE ONLY these mesh ASSETS (GUID retained on update):\n" +
                InlayAssetPath + "\n" + BotanicalsAssetPath + "\n" +
                "\nNo new lights, particles, emissive materials, colliders, Camera, terrain or changes to base Campfire stages.\n" +
                "Existing fire/smoke/sparks/lanterns remain. Optional 3D audio uses existing WAV.\n" +
                "Backup current scene/Git first. Save scene manually after visual check.";
            if (!Confirm(c.scene, "Build Center SAFE V2", scope)) return;

            Mesh inlay = null, botanical = null;
            try
            {
                inlay = MakeInlay(c);
                botanical = MakeBotanicals(c);
                if (inlay == null || botanical == null || inlay.vertexCount < 3 || botanical.vertexCount < 3)
                    throw new InvalidOperationException("No valid decorative geometry. Nothing changed.");
                EnsureFolder();
                Mesh inlayAsset = PersistMesh(InlayAssetPath, inlay);
                Mesh botanicalAsset = PersistMesh(BotanicalsAssetPath, botanical);
                AssetDatabase.SaveAssetIfDirty(inlayAsset);
                AssetDatabase.SaveAssetIfDirty(botanicalAsset);

                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Center SAFE V2: coherent matte environment");
                try
                {
                    DeactivateLegacy(c);
                    DeleteOwned(c.plaza, NewArtName);
                    DeleteOwned(c.stage3, NewSoundName);
                    var art = NewChild(c.plaza, NewArtName);
                    art.position = c.center;
                    AddMeshChild(art, "01_MatteStoneInlay", inlayAsset, c.stoneMaterial);
                    AddMeshChild(art, "02_SoftOuterBotanicals", botanicalAsset, c.stoneMaterial);
                    if (c.sound != null)
                    {
                        var soundObj = NewChild(c.stage3, NewSoundName);
                        soundObj.localPosition = new Vector3(0f, 0.5f, 0f);
                        AudioSource audio = Undo.AddComponent<AudioSource>(soundObj.gameObject);
                        audio.clip = c.sound;
                        audio.loop = true;
                        audio.playOnAwake = true;
                        audio.volume = 0.075f;
                        audio.spatialBlend = 1f;
                        audio.rolloffMode = AudioRolloffMode.Logarithmic;
                        audio.minDistance = 1.4f;
                        audio.maxDistance = 10f;
                        audio.dopplerLevel = 0f;
                    }
                    Undo.CollapseUndoOperations(group);
                    EditorSceneManager.MarkSceneDirty(c.scene);
                    Selection.activeGameObject = art.gameObject;
                    Debug.Log("CENTER SAFE V2: installed 2 matte meshes and optional quiet 3D sound. " +
                        "Legacy Complete roots disabled. No added lights/particles/emission. " +
                        "Now inspect daytime + nighttime Game View; this code was NOT Play Mode tested here.");
                }
                catch (Exception ex)
                {
                    Undo.RevertAllDownToGroup(group);
                    Debug.LogError("CENTER SAFE V2: editor changes rolled back on error: " + ex);
                }
            }
            catch (Exception ex) { Debug.LogError("CENTER SAFE V2: build aborted: " + ex); }
            finally
            {
                if (inlay != null) UnityEngine.Object.DestroyImmediate(inlay);
                if (botanical != null) UnityEngine.Object.DestroyImmediate(botanical);
            }
        }

        [MenuItem(Menu + "4. УДАЛИТЬ только SAFE v2 (v1 оставить выключенным)", false, 393)]
        private static void RemoveV2()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("Exit Play Mode first."); return; }
            Ctx c; string issue;
            if (!TryContext(out c, out issue)) { Debug.LogError("CENTER SAFE V2: " + issue); return; }
            Transform art = c.plaza.Find(NewArtName);
            Transform sound = c.stage3.Find(NewSoundName);
            if (art == null && sound == null) { Debug.Log("CENTER SAFE V2: nothing to remove."); return; }
            if (!Confirm(c.scene, "Remove Center SAFE V2", 
                "DELETE ONLY:\n IslandV2/_Generated/CampPlaza/" + NewArtName +
                "\n Campfire/Stage3/" + NewSoundName +
                "\n\nOld COMPLETE roots remain disabled to prevent reintroducing overexposure. " +
                "Two generated mesh assets remain for Undo/reapplication.")) return;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Center SAFE V2: remove owned objects");
            if (art != null) Undo.DestroyObjectImmediate(art.gameObject);
            if (sound != null) Undo.DestroyObjectImmediate(sound.gameObject);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(c.scene);
            Debug.Log("CENTER SAFE V2: removed own scene objects. Legacy Complete remains OFF. Asset files retained.");
        }

        [MenuItem(Menu + "5. ДИАГНОСТИКА в Play: временно отключить CampLight", false, 394)]
        private static void PlayTestDisableLight()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("CENTER SAFE V2: this diagnostic can only run in Play Mode, so scene Light is not persisted.");
                return;
            }
            var scene = SceneManager.GetActiveScene();
            Transform camp = FindUniqueRoot(scene, "Campfire");
            Transform campLight = camp != null ? camp.Find("Stage3/CampLight") : null;
            Light light = campLight != null ? campLight.GetComponent<Light>() : null;
            if (light == null) { Debug.LogWarning("CENTER SAFE V2: no Campfire/Stage3/CampLight Light found."); return; }
            light.enabled = false;
            Debug.Log("CENTER SAFE V2: disabled original CampLight in Play Mode ONLY. " +
                "If the overexposure disappears, inspect FireLightPool, CampLight and post-processing. " +
                "Exiting Play Mode reverts this test automatically.");
        }

        private static bool TryContext(out Ctx c, out string error)
        {
            c = new Ctx(); error = null;
            if (EditorApplication.isPlayingOrWillChangePlaymode) { error = "Stop Play Mode for scene modifications/audit."; return false; }
            c.scene = SceneManager.GetActiveScene();
            if (!c.scene.IsValid() || !c.scene.isLoaded || string.IsNullOrEmpty(c.scene.path))
            { error = "Open and save the intended scene first."; return false; }
            c.campfire = FindUniqueRoot(c.scene, "Campfire");
            c.plaza = FindScenePath(c.scene, "IslandV2/_Generated/CampPlaza");
            c.stage3 = c.campfire != null ? c.campfire.Find("Stage3") : null;
            if (c.campfire == null || c.plaza == null || c.stage3 == null)
            { error = "Expected unique Campfire root and IslandV2/_Generated/CampPlaza."; return false; }
            var plazaMesh = c.plaza.Find("Plaza_Mesh");
            MeshRenderer mr = plazaMesh != null ? plazaMesh.GetComponent<MeshRenderer>() : null;
            if (mr == null || mr.sharedMaterial == null)
            { error = "Plaza_Mesh or its material missing. No silent fallback to emissive material."; return false; }
            c.stoneMaterial = mr.sharedMaterial;
            c.center = c.campfire.position;
            if (Mathf.Abs(c.center.x) > 0.6f || Mathf.Abs(c.center.z) > 0.6f)
            { error = "Campfire center displaced unexpectedly from approved (0,0) XZ."; return false; }
            if (c.stage3.Find("Sparks") == null || c.stage3.Find("Smoke") == null)
            { error = "Original campfire Sparks/Smoke unavailable; inspect scene before continuing."; return false; }
            var layout = AssetDatabase.LoadAssetAtPath<IslandLayout>("Assets/_Project/IslandV2/IslandLayout.asset");
            if (layout == null) { error = "IslandLayout.asset missing; no safe placement without paths."; return false; }
            c.field = new IslandField(layout);
            c.oldArt = c.plaza.Find(OldArtName);
            c.oldFX = c.stage3.Find(OldFXName);
            c.sound = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioAssetPath);
            return true;
        }

        private static bool Confirm(Scene scene, string title, string scope)
        {
            string msg = "SCENE: " + scene.path + "\n\n" + scope;
            if (scene.path.Contains("_Recovery"))
                msg = "WARNING: ACTIVE SCENE IS IN _RECOVERY (UNVERIFIED RELEASE SCENE). " +
                    "Back up the scene before edits.\n\n" + msg;
            return EditorUtility.DisplayDialog(title, msg, "Apply to THIS scene", "Cancel");
        }

        private static string Describe(Transform tr)
        {
            return tr == null ? "not found" : (PathOf(tr) + ", activeSelf=" + tr.gameObject.activeSelf);
        }

        private static string LegacyPaths()
        {
            return " IslandV2/_Generated/CampPlaza/" + OldArtName + "\n Campfire/Stage3/" + OldFXName + "\n";
        }

        private static bool DeactivateLegacy(Ctx c)
        {
            bool changed = false;
            Transform[] roots = { c.oldArt, c.oldFX };
            foreach (Transform tr in roots)
            {
                if (tr == null || !tr.gameObject.activeSelf) continue;
                Undo.RecordObject(tr.gameObject, "CENTER SAFE V2: deactivate legacy glow source");
                tr.gameObject.SetActive(false);
                EditorUtility.SetDirty(tr.gameObject);
                changed = true;
            }
            return changed;
        }

        private static Transform FindUniqueRoot(Scene scene, string name)
        {
            Transform found = null;
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                if (go.name != name) continue;
                if (found != null) return null;
                found = go.transform;
            }
            return found;
        }

        private static Transform FindScenePath(Scene scene, string path)
        {
            string[] parts = path.Split('/');
            Transform at = FindUniqueRoot(scene, parts[0]);
            for (int i = 1; i < parts.Length && at != null; i++) at = at.Find(parts[i]);
            return at;
        }

        private static string PathOf(Transform tr)
        {
            var names = new List<string>();
            while (tr != null) { names.Add(tr.name); tr = tr.parent; }
            names.Reverse();
            return string.Join("/", names.ToArray());
        }

        private static Transform NewChild(Transform parent, string name)
        {
            GameObject go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "CENTER SAFE V2: create " + name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static void DeleteOwned(Transform parent, string name)
        {
            Transform old = parent.Find(name);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        }

        private static void AddMeshChild(Transform parent, string name, Mesh mesh, Material material)
        {
            Transform obj = NewChild(parent, name);
            MeshFilter mf = Undo.AddComponent<MeshFilter>(obj.gameObject);
            mf.sharedMesh = mesh;
            MeshRenderer renderer = Undo.AddComponent<MeshRenderer>(obj.gameObject);
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            // NO Collider, Light, FireSource, ParticleSystem or dynamic update component.
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Generated"))
                AssetDatabase.CreateFolder("Assets/_Project", "Generated");
            if (!AssetDatabase.IsValidFolder(AssetsFolder))
                AssetDatabase.CreateFolder("Assets/_Project/Generated", "TWCenterSafeV2");
        }

        private static Mesh PersistMesh(string path, Mesh source)
        {
            Mesh old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (old != null)
            {
                Undo.RecordObject(old, "CENTER SAFE V2: refresh owned generated mesh");
                EditorUtility.CopySerialized(source, old);
                old.name = Path.GetFileNameWithoutExtension(path);
                EditorUtility.SetDirty(old);
                return old;
            }
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                throw new InvalidOperationException("Different asset occupies " + path);
            Mesh copy = UnityEngine.Object.Instantiate(source);
            copy.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(copy, path);
            return copy;
        }

        private static Color Palette(string hex)
        {
            Color c;
            return ColorUtility.TryParseHtmlString(hex, out c) ? c.linear : Color.white;
        }

        private static Mesh MakeInlay(Ctx c)
        {
            var g = new Geo();
            Color ivory = Palette("#E9DCC7");
            Color sand = Palette("#DCC5A8");
            Color lavender = Palette("#CEBDD3");
            // Subtle geometric crescent accents in 4 sectors, leaving the paths clear.
            for (int quadrant = 0; quadrant < 4; quadrant++)
            {
                float centerAngle = (quadrant * 90f + 45f) * Mathf.Deg2Rad;
                for (int j = 0; j < 8; j++)
                {
                    float a0 = centerAngle - 0.33f + 0.66f * j / 8f;
                    float a1 = centerAngle - 0.33f + 0.66f * (j + 1) / 8f;
                    float a = (a0 + a1) * 0.5f;
                    float r = 3.75f;
                    float x = c.center.x + Mathf.Cos(a) * r;
                    float z = c.center.z + Mathf.Sin(a) * r;
                    if (c.field.PathAxisDistance(x, z) < 1.95f) continue;
                    float y = c.field.Height(x, z) + 0.087f - c.center.y;
                    g.Arc(3.725f, 3.775f, a0, a1, y, j % 4 == 0 ? lavender : sand);
                }
            }
            // Sparse smaller inlaid plaques just outside the fireplace, no raised objects.
            for (int j = 0; j < 12; j++)
            {
                float angle = (j * 30f + 13f) * Mathf.Deg2Rad;
                float r = 2.30f;
                float x = c.center.x + Mathf.Cos(angle) * r;
                float z = c.center.z + Mathf.Sin(angle) * r;
                if (c.field.PathAxisDistance(x, z) < 1.7f) continue;
                float y = c.field.Height(x, z) + 0.089f - c.center.y;
                g.Diamond(new Vector3(x - c.center.x, y, z - c.center.z), 0.062f, j % 3 == 0 ? lavender : ivory);
            }
            return g.Create("TW_CenterSafeV2_Inlay");
        }

        private static Mesh MakeBotanicals(Ctx c)
        {
            var g = new Geo();
            Color rock = Palette("#C8BCC5");
            Color pale = Palette("#E7D7C4");
            Color mint = Palette("#86B87A");
            Color leaf = Palette("#B3CC92");
            Color pink = Palette("#DFA5AF");
            Color wood = Palette("#967157");
            int clusters = 0;
            for (int i = 0; i < 36 && clusters < 5; i++)
            {
                float a = (i * 59f + 19f) * Mathf.Deg2Rad;
                float r = 5.72f + (i % 3) * 0.20f;
                float worldX = c.center.x + Mathf.Cos(a) * r;
                float worldZ = c.center.z + Mathf.Sin(a) * r;
                if (!c.field.Inside(worldX, worldZ) || c.field.IsWater(worldX, worldZ) ||
                    c.field.PathAxisDistance(worldX, worldZ) < 2.85f) continue;
                float y = c.field.Height(worldX, worldZ) - c.center.y;
                var p = new Vector3(worldX - c.center.x, y + 0.03f, worldZ - c.center.z);
                // Three low, coherent elements per cluster, never obstructing NPC prompts.
                g.Octa(p + Vector3.up * 0.12f, 0.23f, rock);
                g.Octa(p + new Vector3(0.22f, 0.10f, 0.11f), 0.14f, pale);
                for (int j = 0; j < 3; j++)
                {
                    float phi = a + j * 2.094f;
                    var tip = p + new Vector3(Mathf.Cos(phi) * 0.34f, 0f, Mathf.Sin(phi) * 0.34f);
                    g.LeafBlade(tip, 0.22f + 0.06f * (j % 2), j % 2 == 0 ? mint : leaf);
                }
                if (clusters == 1 || clusters == 4)
                {
                    g.SmallCrate(p + new Vector3(-0.30f, 0.12f, -0.12f), 0.20f, wood, pale);
                }
                else
                {
                    g.Octa(p + new Vector3(-0.25f, 0.17f, -0.15f), 0.08f, pink);
                }
                clusters++;
            }
            // Safe fallback only if field had no placement: a very small hidden-underlay inlay is not created.
            if (clusters == 0) g.Octa(new Vector3(0f, -0.35f, 0f), 0.025f, pale);
            return g.Create("TW_CenterSafeV2_Botanicals");
        }

        private sealed class Geo
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<int> triangles = new List<int>();
            private readonly List<Color> colors = new List<Color>();
            private int V(Vector3 p, Color c)
            {
                int index = vertices.Count;
                vertices.Add(p);
                colors.Add(c);
                return index;
            }
            private void Tri(int a, int b, int c) { triangles.Add(a); triangles.Add(b); triangles.Add(c); }
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color tint)
            {
                int ia = V(a, tint), ib = V(b, tint), ic = V(c, tint), id = V(d, tint);
                Tri(ia, ib, ic); Tri(ia, ic, id);
                // Two-sided because some Toon shaders use culling.
                Tri(ic, ib, ia); Tri(id, ic, ia);
            }
            public void Arc(float r0, float r1, float a0, float a1, float y, Color color)
            {
                Quad(new Vector3(Mathf.Cos(a0) * r0, y, Mathf.Sin(a0) * r0),
                     new Vector3(Mathf.Cos(a0) * r1, y, Mathf.Sin(a0) * r1),
                     new Vector3(Mathf.Cos(a1) * r1, y, Mathf.Sin(a1) * r1),
                     new Vector3(Mathf.Cos(a1) * r0, y, Mathf.Sin(a1) * r0), color);
            }
            public void Diamond(Vector3 p, float radius, Color color)
            {
                Quad(p + Vector3.forward * radius, p + Vector3.right * radius,
                     p - Vector3.forward * radius, p - Vector3.right * radius, color);
            }
            public void Octa(Vector3 p, float size, Color color)
            {
                var peak = p + Vector3.up * size;
                var bottom = p - Vector3.up * size * 0.58f;
                for (int i = 0; i < 6; i++)
                {
                    float a = i * Mathf.PI * 2f / 6f;
                    float b = (i + 1) * Mathf.PI * 2f / 6f;
                    Vector3 pa = p + new Vector3(Mathf.Cos(a) * size, 0f, Mathf.Sin(a) * size);
                    Vector3 pb = p + new Vector3(Mathf.Cos(b) * size, 0f, Mathf.Sin(b) * size);
                    Tri(V(peak, color), V(pa, color), V(pb, color));
                    Tri(V(bottom, color * 0.80f), V(pb, color * 0.80f), V(pa, color * 0.80f));
                }
            }
            public void LeafBlade(Vector3 p, float height, Color color)
            {
                for (int i = 0; i < 3; i++)
                {
                    float a = i * Mathf.PI * 2f / 3f;
                    var o = new Vector3(Mathf.Cos(a) * 0.09f, 0f, Mathf.Sin(a) * 0.09f);
                    Vector3 tip = p + Vector3.up * height + o * 0.28f;
                    Quad(p, p + o, tip, p + Vector3.up * height * 0.45f, color);
                }
            }
            public void SmallCrate(Vector3 p, float radius, Color wood, Color cap)
            {
                Vector3 a = p + new Vector3(-radius, 0f, -radius);
                Vector3 b = p + new Vector3(radius, 0f, -radius);
                Vector3 c = p + new Vector3(radius, 0f, radius);
                Vector3 d = p + new Vector3(-radius, 0f, radius);
                Vector3 h = Vector3.up * 0.22f;
                Quad(a, b, b + h, a + h, wood);
                Quad(b, c, c + h, b + h, wood);
                Quad(c, d, d + h, c + h, wood);
                Quad(d, a, a + h, d + h, wood);
                Quad(a + h, b + h, c + h, d + h, cap);
            }
            public Mesh Create(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(vertices);
                mesh.SetColors(colors);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
#endif
