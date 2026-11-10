using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace IslandV2
{
    // Owns the river zone only. No IslandAll/IslandBlockout.Build, terrain edits,
    // bridge edits, save IDs, NPCs or changes to the other four zones.
    public static class TWRiverArt
    {
        const string Menu = "Tools/BuilderSim/Локация/Остров v2/RIVER ART/";
        const string WaterShader = "Project/IslandV2/TW_RiverRibbon";
        const string FoamShader = "Project/IslandV2/TW_RiverFoam";

        sealed class Context
        {
            public Scene scene;
            public Transform manual, generated, bridge;
            public IslandLayout layout;
            public TWRiverCtx river;
            public Material groundMaterial;
            public RiverArtState previous;
        }

        [MenuItem(Menu + "1. Audit (без изменений)", false, 181)]
        public static void Audit()
        {
            try
            {
                var c = Inspect(false);
                Debug.Log("RIVER ART: сцена " + c.scene.path + ", река " + c.river.ax.Length.ToString("F1") +
                          " м. Река, исток, песчаный берег и водопад готовы к сборке. " +
                          (c.previous != null ? "Установлена ревизия " + c.previous.revision + "." : "Новый речной комплект пока не установлен."));
            }
            catch (Exception e) { Debug.LogError("RIVER ART: " + e.Message); }
        }

        [MenuItem(Menu + "2. Собрать реку — исток, пляж, водопад", false, 182)]
        public static void Apply()
        {
            Context c;
            try { c = Inspect(true); }
            catch (Exception e) { Debug.LogError("RIVER ART: " + e.Message); return; }

            string folder = null;
            int group = -1;
            try
            {
                string backup = Backup(c.scene);
                Undo.IncrementCurrentGroup(); group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Timber Wings — cohesive river zone");
                EditorUtility.DisplayProgressBar("Timber Wings", "Скальный исток и песчаные берега", 0.1f);
                string sceneID = AssetDatabase.AssetPathToGUID(c.scene.path);
                folder = TWRiverSpec.AssetFolder + "/Scenes/" + sceneID + "/Build_" + Guid.NewGuid().ToString("N").Substring(0, 12);
                IslandLayout.EnsureFolder(folder);

                var root = new GameObject("TW_RIVER_ART__building");
                Undo.RegisterCreatedObjectUndo(root, "Create river zone");
                root.transform.SetParent(c.manual, false);
                var state = root.AddComponent<RiverArtState>();
                state.revision = TWRiverSpec.Revision; state.generatedFolder = folder;
                state.bridge = c.bridge; state.bridgeMatrix = c.bridge.localToWorldMatrix;
                var x = c.river;

                var ground = new Material(c.groundMaterial) { name = "River_Island_Material" };
                AssetDatabase.CreateAsset(ground, folder + "/Ground.mat");
                var data = c.generated.parent.GetComponent<IslandV2Data>();
                float cliff = data != null && data.depth > 3f ? data.depth : 1f + Mathf.Max(2f, c.layout.cliffWall) + Mathf.Max(3f, c.layout.cliffTaper);
                Material[] stoneMaterials = TWRiverDetails.RockMaterials(folder);
                Mesh sourceMesh = SaveMesh(TWRiverSource.Build(x, cliff), folder, "Source", false);
                TWRiverValidation.CheckClosed(sourceMesh);
                var source = MeshObject(root.transform, "Source_Rock", sourceMesh, stoneMaterials[0], true);
                source.GetComponent<MeshRenderer>().sharedMaterials = stoneMaterials;
                // All visible masses, including the deep foundation, collide.
                source.AddComponent<MeshCollider>().sharedMesh = sourceMesh;
                Mesh shadowMesh = SaveMesh(TWRiverSource.Shadow(x), folder, "SourceShadow", true);
                Material fissureMaterial = TWRiverDetails.Lit(folder, "Spring_Fissure", new Color(0.095f, 0.16f, 0.18f));
                var shadow = MeshObject(root.transform, "Source_Shadow", shadowMesh, fissureMaterial, false);
                shadow.AddComponent<MeshCollider>().sharedMesh = shadowMesh;
                state.source = x.P(TWRiverSpec.MouthDist, 0f, x.Yw(TWRiverSpec.MouthDist));
                x.rocks = new TWFaceted();
                TWRiverBanks.Dress(root.transform, x);
                TWRiverFlora.Build(x); TWRiverFoam.Build(x);
                if (x.rocks.Tris > 0)
                    MeshObject(root.transform, "River_Plant_Clusters", SaveMesh(x.rocks.ToMesh("River_Plants"), folder, "Plants", true), ground, true);
                MeshObject(root.transform, "Sand_Shore", SaveMesh(TWRiverShore.Build(x), folder, "Shore", false), ground, false);

                EditorUtility.DisplayProgressBar("Timber Wings", "Каменная кромка и полотно водопада", 0.6f);
                Mesh ends = SaveMesh(TWRiverSource.OutletBanks(x, cliff), folder, "WaterfallBanks", false);
                TWRiverValidation.CheckClosed(ends);
                var outletBanks = MeshObject(root.transform, "Waterfall_Rock_Edges", ends, stoneMaterials[0], true);
                outletBanks.GetComponent<MeshRenderer>().sharedMaterials = stoneMaterials;
                outletBanks.AddComponent<MeshCollider>().sharedMesh = ends;
                var water = TWRiverWater.Build(x, cliff);
                TWRiverValidation.CheckRibbon(water.mesh, water.lipRow);
                var waterMat = new Material(Shader.Find(WaterShader)) { name = "River_Turquoise" };
                waterMat.SetColor("_DeepColor", new Color(0.035f, 0.52f, 0.84f));
                waterMat.SetColor("_ShallowColor", new Color(0.28f, 0.86f, 0.91f));
                waterMat.SetColor("_FoamColor", new Color(0.94f, 0.99f, 1.0f));
                waterMat.SetFloat("_FlowSpeed", Mathf.Clamp(c.layout.riverFlowSpeed, 0.5f, 2f));
                AssetDatabase.CreateAsset(waterMat, folder + "/Water.mat");
                MeshObject(root.transform, "Water_Source_To_Clouds", SaveMesh(water.mesh, folder, "Water", false), waterMat, false);
                state.lipRow = water.lipRow; state.columns = TWRiverWater.Columns;
                state.outlet = x.P(x.ax.Length, 0f, x.Yw(x.ax.Length));
                if (x.foam.Tris > 0)
                {
                    var foam = new Material(Shader.Find(FoamShader)) { name = "River_Quiet_Wakes" };
                    foam.SetColor("_Tint", new Color(0.93f, 0.98f, 0.93f, 0.72f));
                    AssetDatabase.CreateAsset(foam, folder + "/Foam.mat");
                    MeshObject(root.transform, "Boulder_Wakes", SaveMesh(x.foam.ToMesh("River_Wakes"), folder, "Foam", false), foam, false);
                }
                AddMist(root.transform, water.mistPosition, x.half);
                TWRiverBirds.Build(root.transform, x, ground, folder);
                EditorUtility.DisplayProgressBar("Timber Wings", "Тёплые береговые фонари и небольшие секреты", 0.85f);
                int lanterns = TWRiverDetails.Build(root.transform, x, c.generated.parent.GetComponentsInChildren<TerrainCollider>(true), ground, folder);
                foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
                    if (AssetDatabase.GetAssetPath(mf.sharedMesh).StartsWith(folder + "/", StringComparison.Ordinal))
                        TWRiverValidation.CheckMesh(mf.sharedMesh);

                // No scene mutation before geometry and persistent assets have succeeded.
                CaptureAndHide(c, state);
                if (c.previous != null) Undo.DestroyObjectImmediate(c.previous.gameObject);
                root.name = TWRiverSpec.RootName;
                EditorUtility.SetDirty(state);
                AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(c.scene);
                Undo.CollapseUndoOperations(group);
                Physics.SyncTransforms();
                Selection.activeGameObject = root;
                Debug.Log("RIVER ART v3: готово. Гранёный скальный исток, закрытое основание, каменные края водопада. Фонарей: " +
                          lanterns + ". Уточек: " + root.GetComponentsInChildren<RiverDuckMotion>().Length + ". " +
                          "Сохрани сцену Ctrl+S. Проверка — пункт 4; откат — пункт 3. Копия исходной сцены: " + backup);
            }
            catch (Exception e)
            {
                if (group >= 0) Undo.RevertAllDownToGroup(group);
                if (!string.IsNullOrEmpty(folder) && AssetDatabase.IsValidFolder(folder)) AssetDatabase.DeleteAsset(folder);
                Debug.LogError("RIVER ART: сборка отменена, состояние сцены восстановлено. " + e);
            }
            finally { EditorUtility.ClearProgressBar(); }
        }

        [MenuItem(Menu + "3. Rollback — вернуть прежний вид", false, 183)]
        public static void Rollback()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("Сначала останови Play Mode."); return; }
            var states = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<RiverArtState>(true)).ToArray();
            if (states.Length != 1) { Debug.LogError("RIVER ART: в активной сцене должен быть один установленный комплект."); return; }
            var state = states[0];
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Restore previous river");
            foreach (var entry in state.renderers)
                if (entry.renderer != null) { Undo.RecordObject(entry.renderer, "Restore native water"); entry.renderer.enabled = entry.enabled; }
            foreach (var entry in state.objects)
                if (entry.target != null)
                {
                    Undo.RecordObject(entry.target, "Restore previous river art");
                    entry.target.name = entry.originalName; entry.target.SetActive(entry.active);
                }
            Undo.DestroyObjectImmediate(state.gameObject);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("RIVER ART: прежний вид восстановлен. Ctrl+S. Меши сохранены для Ctrl+Z; исходные материалы не менялись.");
        }

        [MenuItem(Menu + "4. QA — геометрия, коллизия, мост", false, 184)]
        public static void QA()
        {
            try
            {
                var c = Inspect(false);
                if (c.previous == null) throw new InvalidOperationException("Сначала собери новый комплект (пункт 2).");
                TWRiverValidation.CheckInstalled(c.previous);
                Debug.Log("RIVER ART QA: замкнутая скала, края водопада, фонари, общий перегиб и неизменность моста проверены. " +
                          "Далее в Play Mode: мост, берега, плавание, обе стороны истока, день/ночь, сохранение/загрузка.");
            }
            catch (Exception e) { Debug.LogError("RIVER ART QA: " + e.Message); }
        }

        [MenuItem(Menu + "5. Показать всю реку", false, 185)]
        static void Frame()
        {
            try
            {
                var c = Inspect(false);
                Vector3 a = c.river.P(c.river.ax.Length * 0.5f, 0f, 0f);
                SceneView.lastActiveSceneView?.LookAt(a, Quaternion.Euler(48f, 35f, 0f), c.river.ax.Length * 0.62f);
            }
            catch (Exception e) { Debug.LogError(e.Message); }
        }

        static Context Inspect(bool requireSaved)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Останови Play Mode и дождись компиляции.");
            var c = new Context { scene = SceneManager.GetActiveScene() };
            if (!c.scene.IsValid() || string.IsNullOrEmpty(c.scene.path)) throw new InvalidOperationException("Открой сохранённую сцену острова.");
            if (requireSaved && c.scene.isDirty) throw new InvalidOperationException("Сначала сохрани текущую сцену Ctrl+S.");
            var roots = c.scene.GetRootGameObjects().Where(g => g.name == IslandBlockout.RootName).ToArray();
            if (roots.Length != 1) throw new InvalidOperationException("В активной сцене нужен ровно один IslandV2.");
            var root = roots[0].transform;
            if (root.position.sqrMagnitude > 0.00001f || Quaternion.Angle(root.rotation, Quaternion.identity) > 0.01f ||
                Vector3.Distance(root.lossyScale, Vector3.one) > 0.001f) throw new InvalidOperationException("IslandV2 должен иметь исходный Transform (0,0,0; масштаб 1).");
            c.manual = root.Find(IslandBlockout.ManualName); c.generated = root.Find(IslandBlockout.GeneratedName);
            if (c.manual == null || c.generated == null) throw new InvalidOperationException("Не найдены _Manual / _Generated.");
            if (c.manual.position.sqrMagnitude > 0.00001f || Quaternion.Angle(c.manual.rotation, Quaternion.identity) > 0.01f ||
                Vector3.Distance(c.manual.lossyScale, Vector3.one) > 0.001f) throw new InvalidOperationException("_Manual имеет нестандартный Transform.");
            foreach (string name in new[] { IslandRiverWater.ObjectName, IslandRiverWater.FallObjectName, IslandCliff.ObjectName, IslandBlockout.BridgeName })
                if (c.generated.Find(name) == null) throw new InvalidOperationException("В _Generated нет " + name + ". Открой актуальную игровую сцену.");
            c.bridge = c.generated.Find(IslandBlockout.BridgeName);
            if (root.GetComponentsInChildren<TerrainCollider>(true).Length == 0) throw new InvalidOperationException("Не найден TerrainCollider.");
            c.layout = AssetDatabase.LoadAssetAtPath<IslandLayout>(IslandLayout.AssetPath);
            c.groundMaterial = AssetDatabase.LoadAssetAtPath<Material>(IslandBlockout.MaterialPath);
            if (c.layout == null || c.groundMaterial == null || !c.groundMaterial.HasProperty("_GroundTex"))
                throw new InvalidOperationException("Нет IslandLayout / материала земли. Проверь загрузку Git LFS.");
            if (c.groundMaterial.GetTexture("_GroundTex") == null) throw new InvalidOperationException("Не загружена текстура земли (Git LFS).");
            var waterShader = Shader.Find(WaterShader); var foamShader = Shader.Find(FoamShader);
            if (waterShader == null || foamShader == null || !waterShader.isSupported || !foamShader.isSupported)
                throw new InvalidOperationException("Речные шейдеры ещё не импортированы или не поддерживаются. Проверь Console.");
            var poolShader = Shader.Find(TWRiverDetails.PoolShader);
            var stoneShader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (poolShader == null || stoneShader == null || !poolShader.isSupported || !stoneShader.isSupported)
                throw new InvalidOperationException("Не импортирован TW_LanternPool / URP Simple Lit. Скопируй все файлы пакета и дождись компиляции.");
            var f = new IslandField(c.layout); var pieces = f.RiverInsidePieces();
            var outlets = f.RiverEdges.Where(e => e.isOutlet).ToArray();
            if (pieces.Count != 1 || outlets.Length != 1 || !TWRiverSpec.HasSource(f))
                throw new InvalidOperationException("Нужна одна река с входом у скалы и одним выходом с острова.");
            var x = new TWRiverCtx { f = f, L = c.layout, half = c.layout.riverWidth * 0.5f, seed = 4004,
                hasSource = true, pal = new TWRiverPalette(c.layout), ax = new TWRiverAxis(pieces[0], 0.7f) };
            if (x.ax.Length < 35f || x.half < 2.5f || x.half > 9f) throw new InvalidOperationException("Неожиданные размеры реки: нужна длина от 35 м, ширина 5–18 м.");
            // Never dress a stale layout over a different saved water mesh.
            var native = c.generated.Find(IslandRiverWater.ObjectName).GetComponent<MeshFilter>();
            if (native == null || native.sharedMesh == null || native.sharedMesh.vertexCount < 4)
                throw new InvalidOperationException("Не загружен исходный меш воды.");
            var nv = native.sharedMesh.vertices;
            Vector3 first = native.transform.TransformPoint((nv[0] + nv[1]) * 0.5f);
            Vector3 last = native.transform.TransformPoint((nv[nv.Length - 2] + nv[nv.Length - 1]) * 0.5f);
            if (Vector3.Distance(first, x.P(0f, 0f, x.Yw(0f) + 0.03f)) > 0.2f ||
                Vector3.Distance(last, x.P(x.ax.Length, 0f, x.Yw(x.ax.Length) + 0.03f)) > 0.2f)
                throw new InvalidOperationException("IslandLayout не совпадает с сохранённой рекой. Сборка остановлена без изменения сцены.");
            if (c.layout.crossings != null)
                foreach (var crossing in c.layout.crossings) x.exclusions.Add(new Vector3(crossing.pos.x, crossing.pos.y, crossing.halfLength + x.half + 2f));
            foreach (var col in c.bridge.GetComponentsInChildren<Collider>(true)) x.protectedBounds.Add(col.bounds);
            float heightDifference = 0f;
            for (float s = 12f; s < x.ax.Length - 8f; s += 4f)
            {
                Vector2 a = x.ax.At(s), side = x.ax.Side(s) * (x.half + 4f);
                heightDifference += f.Height(a.x + side.x, a.y + side.y) - f.Height(a.x - side.x, a.y - side.y);
            }
            x.rockSide = heightDifference >= 0f ? 1 : -1;
            c.river = x;
            var installed = c.manual.GetComponentsInChildren<RiverArtState>(true);
            if (installed.Length > 1) throw new InvalidOperationException("Найдено несколько RiverArtState — сначала устрани дубли комплектов.");
            c.previous = installed.FirstOrDefault();
            return c;
        }

        static void CaptureAndHide(Context c, RiverArtState state)
        {
            if (c.previous != null)
            {
                state.renderers = c.previous.renderers.ToArray(); state.objects = c.previous.objects.ToArray();
                state.bridgeMatrix = c.previous.bridgeMatrix;
            }
            else
            {
                var renderers = new List<RiverArtState.RendererState>();
                foreach (string name in new[] { IslandRiverWater.ObjectName, IslandRiverWater.FallObjectName })
                    foreach (var r in c.generated.Find(name).GetComponentsInChildren<Renderer>(true))
                        renderers.Add(new RiverArtState.RendererState { renderer = r, enabled = r.enabled });
                var objects = new List<RiverArtState.ObjectState>();
                foreach (Transform child in c.generated)
                    if (child.name.StartsWith("Spray_Fall", StringComparison.Ordinal))
                        objects.Add(new RiverArtState.ObjectState { target = child.gameObject, active = child.gameObject.activeSelf, originalName = child.name });
                foreach (string name in TWRiverSpec.LegacyRoots.Concat(new[] { TWRiverSpec.RootName }))
                {
                    var t = c.manual.Find(name);
                    if (t != null) objects.Add(new RiverArtState.ObjectState { target = t.gameObject, active = t.gameObject.activeSelf, originalName = t.name });
                }
                state.renderers = renderers.ToArray(); state.objects = objects.ToArray();
            }
            foreach (var e in state.renderers)
                if (e.renderer != null) { Undo.RecordObject(e.renderer, "Hide native river renderer"); e.renderer.enabled = false; }
            foreach (var e in state.objects)
            {
                if (e.target == null) continue;
                Undo.RecordObject(e.target, "Hide previous river art");
                if (e.target.name == TWRiverSpec.RootName) e.target.name += "__previous";
                e.target.SetActive(false);
            }
        }

        static Mesh SaveMesh(Mesh mesh, string folder, string name, bool linearColours)
        {
            TWRiverValidation.CheckMesh(mesh);
            if (linearColours) mesh.colors = mesh.colors.Select(col => col.linear).ToArray();
            AssetDatabase.CreateAsset(mesh, folder + "/" + name + ".asset");
            return mesh;
        }

        static GameObject MeshObject(Transform parent, string name, Mesh mesh, Material material, bool castShadow)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = material;
            r.shadowCastingMode = castShadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.receiveShadows = true;
            return go;
        }

        static void AddMist(Transform root, Vector3 position, float half)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(IslandRiverWater.SprayMaterialPath);
            if (material != null) CandyWaterFx.Create(root, "Waterfall_Cloud_Mist", root.InverseTransformPoint(position), 0.85f, half * 0.6f, 2, material);
        }

        static string Backup(Scene scene)
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            string dir = Path.Combine(project, "UserSettings/TW_RiverBackups", AssetDatabase.AssetPathToGUID(scene.path));
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + ".unity");
            File.Copy(Path.Combine(project, scene.path), path, false);
            return path;
        }
    }
}
