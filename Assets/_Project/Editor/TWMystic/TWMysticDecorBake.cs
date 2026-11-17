using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace IslandV2
{
    internal static class TWMysticDecorBake
    {
        const string Menu = "Tools/BuilderSim/Лес/MYSTIC FOREST/Декоративные деревья/";
        const string PrefabFolder = "Assets/_Project/Prefabs/MysticDecor";
        const string MeshFolder = PrefabFolder + "/Meshes";
        const string MaterialPath = PrefabFolder + "/MysticDecor.mat";
        const string UpdateMenu = Menu + "1. Обновить модели хранителей";
        const string PrefabMenu = Menu + "2. Запечь префабы для расстановки";

        [MenuItem(UpdateMenu, true)]
        [MenuItem(PrefabMenu, true)]
        static bool CanBake() => !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling;

        [MenuItem(UpdateMenu, false, 380)]
        static void UpdateGuardians()
        {
            var bodies = new List<(Mesh mesh, string path)>();
            try
            {
                EditorAssets.EnsureFolder(ForestGuardLite.Dir);
                foreach (SacredKind kind in Enum.GetValues(typeof(SacredKind)))
                {
                    Queue(kind, 0, ForestSacred.Dir, $"Sacred_{kind}_LOD0");
                    Queue(kind, 1, ForestSacred.Dir, $"Sacred_{kind}_LOD1");
                    if (kind != SacredKind.Sleeper)
                        Queue(kind, 1, ForestGuardLite.Dir, $"Lite_{kind}");
                }
                var meshes = new Dictionary<string, Mesh>(bodies.Count);
                foreach (var body in bodies)
                    meshes.Add(body.path, SaveMesh(body.mesh, body.path));

                int prefabs = 0;
                foreach (SacredKind kind in Enum.GetValues(typeof(SacredKind)))
                {
                    string sacred = $"{ForestSacred.Dir}/Sacred_{kind}";
                    prefabs += RefreshGuardian(sacred, kind, meshes[sacred + "_LOD0.asset"], meshes[sacred + "_LOD1.asset"]);
                    if (kind != SacredKind.Sleeper)
                    {
                        var lite = meshes[$"{ForestGuardLite.Dir}/Lite_{kind}.asset"];
                        prefabs += RefreshGuardian($"{ForestGuardLite.Dir}/Lite_{kind}", kind, lite);
                        prefabs += RefreshGuardian($"{ForestGuardLite.Dir}/LiteS_{kind}", kind, lite);
                    }
                }

                SceneView.RepaintAll();
                Debug.Log($"MYSTIC FOREST: крепления и LOD обновлены. Мешей — {bodies.Count}, префабов — {prefabs}.");
            }
            catch (Exception e)
            {
                Debug.LogError("MYSTIC FOREST: не удалось обновить модели хранителей. " + e.Message);
                Debug.LogException(e);
            }
            finally
            {
                foreach (var body in bodies)
                    if (body.mesh != null && !AssetDatabase.Contains(body.mesh))
                        Object.DestroyImmediate(body.mesh);
            }

            void Queue(SacredKind kind, int lod, string folder, string name)
            {
                string path = $"{folder}/{name}.asset";
                var geometry = SacredMeshes.Body(kind, lod, name);
                bodies.Add((geometry, path));
                TWMysticValidation.Mesh(geometry);
                if (geometry.subMeshCount != 4)
                    throw new InvalidOperationException("Некорректные слоты материалов " + path);
            }
        }

        static int RefreshGuardian(string path, SacredKind kind, Mesh nearMesh, Mesh farMesh = null)
        {
            path += ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                Debug.LogWarning("MYSTIC FOREST: не найден префаб " + path);
                return 0;
            }

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var group = root.GetComponent<LODGroup>();
                if (group == null)
                    group = root.AddComponent<LODGroup>();
                var gem = root.transform.Find("Gem");
                var gemRenderer = gem != null ? gem.GetComponent<MeshRenderer>() : null;
                if (gemRenderer == null)
                    throw new InvalidOperationException("В префабе отсутствует кристалл Gem: " + path);
                var mark = root.transform.Find("Mark");
                var markRenderer = mark != null ? mark.GetComponent<MeshRenderer>() : null;

                if (farMesh != null)
                {
                    var vibe = root.GetComponent<SacredVibe>();
                    if (vibe == null || markRenderer == null)
                        throw new InvalidOperationException("В префабе нет компонентов хранителя: " + path);
                    var near = Bind("Visual/Body_LOD0", nearMesh);
                    var far = Bind("Visual/Body_LOD1", farMesh);
                    ForestSacred.PlaceRootCrystal(gem, kind, false);
                    vibe.gem = null;
                    vibe.gemRenderer = gemRenderer;
                    ForestSacred.SetModelLODs(group, near, far, markRenderer, gemRenderer);

                    if (kind == SacredKind.Crystal)
                    {
                        var lights = root.transform.Find("Light_Crystals");
                        if (lights != null && lights.TryGetComponent<FireSource>(out var fire))
                            fire.glowPoints = SacredMeshes.CrystalSpots();
                    }
                }
                else
                {
                    var lite = root.GetComponent<LiteGuardian>();
                    if (lite == null)
                        throw new InvalidOperationException("В префабе нет LiteGuardian: " + path);
                    var body = Bind("Visual/Body", nearMesh);
                    ForestSacred.PlaceRootCrystal(gem, kind, lite.tier == LiteGuardian.Tier.Small);
                    lite.gem = null;
                    ForestGuardLite.SetModelLOD(group, body, markRenderer, gemRenderer);
                }

                PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
                if (!saved)
                    throw new InvalidOperationException("Unity не сохранила префаб " + path);
                return 1;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            MeshRenderer Bind(string relativePath, Mesh mesh)
            {
                var child = root.transform.Find(relativePath);
                if (child == null || !child.TryGetComponent<MeshRenderer>(out var renderer))
                    throw new InvalidOperationException("В префабе нет модели " + relativePath + ": " + path);
                if (!child.TryGetComponent<MeshFilter>(out var filter))
                    filter = child.gameObject.AddComponent<MeshFilter>();
                filter.sharedMesh = mesh;
                var materials = renderer.sharedMaterials;
                if (materials.Length != mesh.subMeshCount || Array.Exists(materials, m => m == null))
                    renderer.sharedMaterials = materials = ForestSacred.Mats(kind);
                ForestSacred.AlignBodyWind(materials);
                foreach (var material in materials)
                    if (material != null)
                        AssetDatabase.SaveAssetIfDirty(material);
                return renderer;
            }
        }

        [MenuItem(PrefabMenu, false, 381)]
        static void BakePrefabs()
        {
            try
            {
                EditorAssets.EnsureFolder(MeshFolder);
                EditorAssets.EnsureFolder(PrefabFolder + "/Trees");
                EditorAssets.EnsureFolder(PrefabFolder + "/Rocks");
                EditorAssets.EnsureFolder(PrefabFolder + "/Greenery");
                var material = GetMaterial();

                BakeTree("Tree_SilverFork", TWMysticDecorModels.SilverFork, material, 5.05f, 0.52f, 3.20f, new Vector2(-0.16f, 0f));
                BakeTree("Tree_DuskWillow", TWMysticDecorModels.DuskWillow, material, 4.30f, 0.56f, 3.28f, new Vector2(0.21f, 0.05f));
                BakeTree("Tree_Whisper", TWMysticDecorModels.WhisperTree, material, 4.80f, 0.65f, 3.55f, new Vector2(-0.30f, -0.12f), AddWhisper);

                BakeProp("Rocks", "Rock_MossBoulder", TWMysticDecorModels.MossBoulder(), material, new Vector3(0, 0.29f, 0), new Vector3(0.99f, 0.68f, 0.83f));
                BakeProp("Rocks", "Rock_SlateShelf", TWMysticDecorModels.SlateShelf(), material, new Vector3(-0.02f, 0.17f, 0f), new Vector3(1.52f, 0.35f, 0.63f));
                BakeProp("Rocks", "Rock_LooseStones", TWMysticDecorModels.LooseStones(), material);

                BakeProp("Greenery", "Green_Fern", TWMysticDecorModels.Fern(), material);
                BakeProp("Greenery", "Green_SilverUnderbrush", TWMysticDecorModels.SilverUnderbrush(), material);
                BakeProp("Greenery", "Green_Heather", TWMysticDecorModels.Heather(), material);
                BakeProp("Greenery", "Green_MossPatch", TWMysticDecorModels.MossPatch(), material);

                var folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(PrefabFolder);
                Selection.activeObject = folder;
                EditorGUIUtility.PingObject(folder);
                Debug.Log("MYSTIC FOREST: готовы 3 дерева, 3 набора камней и 4 растения в " + PrefabFolder);
            }
            catch (Exception e)
            {
                Debug.LogError("MYSTIC FOREST: не удалось запечь префабы. " + e.Message);
                Debug.LogException(e);
            }
        }

        static void BakeTree(string name, Action<SoftMesh, SoftMesh, bool> model, Material material,
            float crownHeight, float radius, float height, Vector2 trunkCenter, Action<GameObject> finish = null)
        {
            var root = new GameObject(name);
            try
            {
                var visual = new GameObject("Visual");
                visual.transform.SetParent(root.transform, false);
                var crown = new GameObject("Crown");
                crown.transform.SetParent(visual.transform, false);
                var pivot = Vector3.up * crownHeight;
                crown.transform.localPosition = pivot;
                var sway = visual.AddComponent<ToonSway>();
                sway.angle = 0.38f;
                sway.speed = 0.56f;

                var lods = new LOD[2];
                for (int lod = 0; lod < lods.Length; lod++)
                {
                    var trunkModel = new SoftMesh();
                    var crownModel = new SoftMesh();
                    bool near = lod == 0;
                    model(trunkModel, crownModel, near);
                    for (int i = 0; i < crownModel.v.Count; i++)
                        crownModel.v[i] -= pivot;
                    string trunkName = $"Trunk_LOD{lod}";
                    string crownName = $"Leaves_LOD{lod}";
                    var trunkRenderer = AddRenderer(visual.transform, trunkName, BakeMesh(trunkModel, $"{name}_{trunkName}"), material, near);
                    var crownRenderer = AddRenderer(crown.transform, crownName, BakeMesh(crownModel, $"{name}_{crownName}"), material, near);
                    lods[lod] = new LOD(near ? 0.09f : 0.013f, new Renderer[] { trunkRenderer, crownRenderer });
                }
                var group = root.AddComponent<LODGroup>();
                group.SetLODs(lods);
                group.RecalculateBounds();

                var trunk = root.AddComponent<CapsuleCollider>();
                trunk.radius = radius;
                trunk.height = height;
                trunk.center = new Vector3(trunkCenter.x, height * 0.5f - 0.10f, trunkCenter.y);
                var slip = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(ForestMats.MatDir + "/TreeTrunk_Slip.asset");
                if (slip != null)
                    trunk.sharedMaterial = slip;
                finish?.Invoke(root);
                SavePrefab(root, "Trees");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static void AddWhisper(GameObject root)
        {
            AddBox(root, TWMysticDecorModels.WhisperStone, new Vector3(0.55f, 0.89f, 0.30f));
            var landmark = root.AddComponent<LandmarkTree>();
            landmark.title = "Шепчущее дерево";
            landmark.caption = "УЗОР НА СТАРОМ КАМНЕ";
            landmark.prompt = "прислушаться";
            landmark.radius = 3.2f;
            landmark.promptHeight = 1.95f;
            landmark.toastColor = new Color(0.65f, 0.87f, 0.83f);
            landmark.greetings = new[]
            {
                "В КОРНЯХ ШУРШИТ СУХАЯ ЛИСТВА.",
                "ВЕТЕР ПРОХОДИТ СКВОЗЬ РАЗВИЛКУ И ТИХО СВИСТИТ.",
                "ЗНАК НА КАМНЕ ПОКРЫЛСЯ МОХОМ, НО ЛИНИИ ЕЩЁ ВИДНЫ."
            };
        }

        static void BakeProp(string folder, string name, SoftMesh model, Material material,
            Vector3 colliderCenter = default, Vector3 colliderSize = default)
        {
            var root = new GameObject(name);
            try
            {
                AddRenderer(root.transform, "Geometry", BakeMesh(model, name), material, true);
                if (colliderSize != Vector3.zero)
                    AddBox(root, colliderCenter, colliderSize);
                SavePrefab(root, folder);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static void AddBox(GameObject root, Vector3 center, Vector3 size)
        {
            var collider = root.AddComponent<BoxCollider>();
            collider.center = center;
            collider.size = size;
        }

        static MeshRenderer AddRenderer(Transform parent, string name, Mesh mesh, Material material, bool castsShadows)
        {
            var geometry = new GameObject(name);
            geometry.transform.SetParent(parent, false);
            geometry.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = geometry.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castsShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return renderer;
        }

        static Material GetMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null)
                return material;
            var shader = Shader.Find("Project/CandyToon");
            if (shader == null)
                throw new InvalidOperationException("В проекте отсутствует шейдер Project/CandyToon.");
            material = new Material(shader) { name = "MysticDecor" };
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_VertexColorStrength", 1f);
            material.SetFloat("_PosVary", 0.035f);
            material.SetFloat("_RimStrength", 0.075f);
            material.SetFloat("_GlossStrength", 0.026f);
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }

        static Mesh BakeMesh(SoftMesh model, string name)
        {
            var mesh = model.ToMesh(name);
            try
            {
                TWMysticValidation.Mesh(mesh);
                return SaveMesh(mesh, $"{MeshFolder}/{name}.asset");
            }
            finally
            {
                if (mesh != null && !AssetDatabase.Contains(mesh))
                    Object.DestroyImmediate(mesh);
            }
        }

        static Mesh SaveMesh(Mesh mesh, string path)
        {
            var asset = ForestMats.SaveMesh(mesh, path);
            AssetDatabase.SaveAssetIfDirty(asset);
            return asset;
        }

        static void SavePrefab(GameObject root, string folder)
        {
            string path = $"{PrefabFolder}/{folder}/{root.name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
            if (!success)
                throw new InvalidOperationException("Unity не сохранила префаб " + path);
        }
    }
}
