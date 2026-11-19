using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using static IslandV2.TWSpecialTreeArt;
using Object = UnityEngine.Object;
using MaterialPhysics = UnityEngine.PhysicsMaterial;

namespace IslandV2
{
    internal static partial class TWSpecialTreeBaker
    {
        const string Menu = "Tools/BuilderSim/Лес/TW07 · Особые деревья/";
        const string Assets = "Assets/_Project/IslandV2/SpecialTrees";
        const string GardenName = "TW07_RuneGrove";
        const string GoldName = "TW07_GoldenTreeSites";
        const string CourtyardName = "Courtyard";
        const string EditName = "TimberWings — рунная роща и золотые деревья";

        [MenuItem(Menu + "1. Запечь у кузницы и в лесу", false, 354)]
        static void BakeMenu() => Run(() => Bake());

        [MenuItem(Menu + "2. Запечь у выбранной точки сбоку", false, 355)]
        static void BakeSelected() => Run(() =>
        {
            var marker = Selection.activeTransform;
            if (marker == null || marker.gameObject.scene != SceneManager.GetActiveScene()
                || marker.GetComponentInParent<SpecialTreeGroves>() != null)
                throw new InvalidOperationException("Выбери отдельную точку игровой сцены сбоку от кузницы.");
            Bake(marker);
        });

        [MenuItem(Menu + "3. Запечь дважды и проверить", false, 356)]
        static void RepeatMenu() => Run(() =>
        {
            var first = Bake();
            var trees = Trees(first);
            int[] objects = trees.Select(tree => tree.GetInstanceID()).ToArray();
            Vector3[] positions = trees.Select(tree => tree.transform.position).ToArray();
            int[] sceneParts = CourtyardObjects(first);
            string[] guids = AssetGuids();
            var second = Bake();
            Require(first == second && objects.SequenceEqual(Trees(second).Select(tree => tree.GetInstanceID()))
                && positions.Zip(Trees(second), (position, tree) => Vector3.Distance(position, tree.transform.position) < .001f).All(same => same)
                && sceneParts.SequenceEqual(CourtyardObjects(second)) && guids.SequenceEqual(AssetGuids()),
                "При повторном запуске изменились экземпляры, размещение или GUID.");
            Debug.Log("TW07 · Два запекания. Кузница, площадка и деревья сохранили экземпляры, положение и GUID.", second);
        });

        [MenuItem(Menu + "4. Переключить предпросмотр готовой кузницы", false, 357)]
        static void PreviewMenu() => Run(ToggleCourtyardPreview);

        static void Run(Action work)
        {
            try { work(); }
            catch (Exception error) { Debug.LogError("TW07 · " + error.Message); }
        }

        static string[] AssetGuids() => AssetDatabase.FindAssets("", new[] { Assets, TWRuneForgeBaker.Folder })
            .OrderBy(guid => guid).ToArray();

        static int[] CourtyardObjects(SpecialTreeGroves grove) => grove.forge.GetComponentsInChildren<Transform>(true)
            .Select(part => part.GetInstanceID()).OrderBy(id => id).ToArray();

        static SpecialTreeGroves Bake(Transform chosen = null)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling
                || PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("Открой игровую сцену в Edit Mode.");
            Scene scene = SceneManager.GetActiveScene();
            Require(scene.IsValid() && scene.isLoaded && !string.IsNullOrEmpty(scene.path), "Сначала сохрани игровую сцену.");

            var instances = TWRuneForgeBaker.SceneObjects<RuneForge>(scene);
            Require(instances.Length <= 1, "В сцене несколько кузниц TW06.");
            RuneForge forge = instances.SingleOrDefault();
            if (forge == null) forge = TWRuneForgeBaker.Bake();
            var garden = forge.transform.Find(GardenName);
            var gold = scene.GetRootGameObjects().Where(root => root.name == GoldName).ToArray();
            Require(gold.Length <= 1, "В сцене продублированы золотые посадки TW07.");
            Transform goldParent = gold.SingleOrDefault()?.transform;

            var placement = new TWSpecialTreePlacement(scene, garden, goldParent);
            var site = placement.Grove(forge, chosen, garden);

            var anchors = TWRuneForgeBaker.SceneObjects<Transform>(scene);
            var cedar = anchors.FirstOrDefault(t => t.name == "PAD_Forest");
            var south = anchors.FirstOrDefault(t => t.name == "PAD_GroveSouth");
            Vector3 goldA = placement.Golden(cedar, forge.transform.position + Vector3.forward * 19f, null);
            Vector3 goldB = placement.Golden(south, forge.transform.position + Vector3.right * 22f, goldA);
            if (instances.Length == 1) TWRuneForgeBaker.Bake();

            IslandLayout.EnsureFolder(Assets);
            string groundFolder = Assets + "/" + AssetDatabase.AssetPathToGUID(scene.path);
            IslandLayout.EnsureFolder(groundFolder);
            Shader toon = Shader.Find("Project/CandyToon");
            Shader particleShader = Shader.Find("Project/CandyParticle");
            Require(toon != null && particleShader != null, "Нужны шейдеры CandyToon и CandyParticle.");
            Material surface = MakeMaterial(toon, "TW07_Wood_Leaves_And_Soil", Color.black);
            Material runeLight = MakeMaterial(toon, "TW07_RuneLight", SoftMesh.C(0x76D6B2) * .69f);
            Material courtyardLight = MakeMaterial(toon, "TW07_CourtyardGlow", new Color(.063f, .135f, .13f));
            Material goldenLight = MakeMaterial(toon, "TW07_GoldenLight", SoftMesh.C(0xFFCA7C) * .57f);
            Material motes = SaveAsset(new Material(particleShader) { name = "TW07_Motes" }, Assets + "/TW07_Motes.mat");
            MaterialPhysics slip = MakeSlip();
            GameObject rune = BakeTree(false, surface, runeLight, motes, slip);
            GameObject golden = BakeTree(true, surface, goldenLight, motes, slip);

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(EditName);
            try
            {
                if (garden == null) garden = SceneChild(forge.transform, GardenName);
                Undo.RecordObject(garden, EditName);
                garden.SetPositionAndRotation(site.Pose.position, site.Pose.rotation);

                Transform courtyard = SceneChild(garden, CourtyardName);
                Transform runeGroup = MoveGeneratedChild(garden, courtyard, "RuneTrees");
                BakeCourtyard(courtyard, runeGroup, garden, forge, site, placement, groundFolder, surface, courtyardLight, motes);
                PlaceTree(runeGroup, "RuneTree_01", rune, TWRuneSoilArt.TreeRoot(site.Side, 0), -14f, "tw07.forge.rune.01");
                PlaceTree(runeGroup, "RuneTree_02", rune, TWRuneSoilArt.TreeRoot(site.Side, 1), 21f, "tw07.forge.rune.02");

                if (goldParent == null)
                {
                    var root = new GameObject(GoldName);
                    SceneManager.MoveGameObjectToScene(root, scene);
                    Undo.RegisterCreatedObjectUndo(root, EditName);
                    goldParent = root.transform;
                }
                Transform slotA = SceneChild(goldParent, "CedarForest");
                Transform slotB = SceneChild(goldParent, "SouthGrove");
                SetWorld(slotA, goldA);
                SetWorld(slotB, goldB);
                PlaceTree(slotA, "GoldenTree_01", golden, Vector3.zero, 22f, "tw07.forest.golden.01");
                PlaceTree(slotB, "GoldenTree_02", golden, Vector3.zero, -45f, "tw07.forest.golden.02");

                var binding = SceneComponent<SpecialTreeGroves>(garden);
                binding.forge = forge;
                binding.courtyard = courtyard.gameObject;
                binding.runeTrees = runeGroup.gameObject;
                binding.goldenSites = new[] { slotA.gameObject, slotB.gameObject };
                Undo.RecordObject(binding.runeTrees, EditName);
                binding.runeTrees.SetActive(true);
                SetCourtyardPreview(binding, false);
                foreach (GameObject inactive in binding.goldenSites)
                {
                    Undo.RecordObject(inactive, EditName);
                    inactive.SetActive(false);
                }
                Validate(binding);
                ValidateCourtyard(binding, site);

                AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(scene);
                Undo.CollapseUndoOperations(group);
                Selection.activeGameObject = garden.gameObject;
                Debug.Log("TW07 · Кузница и рунная терраса запечены. Сохрани сцену через Ctrl+S. Готовый вид можно включить пунктом 4.", binding);
                return binding;
            }
            catch
            {
                Undo.RevertAllDownToGroup(group);
                throw;
            }
        }

        static void Validate(SpecialTreeGroves grove)
        {
            var trees = Trees(grove);
            Require(trees.Length == 4 && trees.Select(t => t.saveId).Distinct().Count() == 4
                && trees.Count(t => t.IsGolden) == 2, "Число точек или ключи сейва отличаются от схемы TW07.");
            Require(grove.transform.IsChildOf(grove.forge.transform)
                && TWRuneForgeBaker.SceneObjects<SpecialTreeGroves>(grove.gameObject.scene).Length == 1,
                "В сцене должна быть одна роща у кузницы.");
            foreach (var tree in trees)
            {
                var resource = tree.GetComponent<ResourceItem>();
                var trunk = tree.transform.Find(TreeCollision.TrunkName);
                Require(resource != null && resource.respawn && AssetDatabase.Contains(resource.stumpPrefab)
                    && AssetDatabase.Contains(tree.sectionPrefab) && trunk != null &&
                    trunk.TryGetComponent<CapsuleCollider>(out var solid) && !solid.isTrigger,
                    "У дерева отсутствует запечённая секция, пень или коллайдер ствола.");
            }
        }

        static SpecialHarvestTree[] Trees(SpecialTreeGroves grove)
            => grove.GetComponentsInChildren<SpecialHarvestTree>(true)
                .Concat(grove.goldenSites.SelectMany(slot => slot.GetComponentsInChildren<SpecialHarvestTree>(true)))
                .OrderBy(tree => tree.saveId, StringComparer.Ordinal).ToArray();

        static void SetWorld(Transform host, Vector3 position)
        {
            Undo.RecordObject(host, EditName);
            host.SetPositionAndRotation(position, Quaternion.identity);
        }

        static void PlaceTree(Transform parent, string name, GameObject prefab, Vector3 position, float yaw, string id)
        {
            Transform existing = parent.Find(name);
            GameObject instance = existing != null ? existing.gameObject : (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            if (existing == null)
            {
                Undo.RegisterCreatedObjectUndo(instance, EditName);
                instance.name = name;
                PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
            }
            Require(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instance) == AssetDatabase.GetAssetPath(prefab),
                "Имя точки уже занято другим объектом — " + name);
            var profile = instance.GetComponent<SpecialHarvestTree>();
            Require(profile != null, "У дерева отсутствует SpecialHarvestTree — " + name);
            Undo.RecordObjects(new Object[] { instance.transform, profile }, EditName);
            instance.transform.localPosition = position;
            instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            profile.saveId = id;
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(profile);
        }

        static GameObject BakeTree(bool gold, Material surface, Material light, Material motes, MaterialPhysics slip)
        {
            string name = gold ? "Golden" : "Rune";
            var stump = SaveMesh(Stump(gold), Assets, name + "_Stump");
            var log = SaveMesh(Section(gold), Assets, name + "_LogSection");
            var logGlow = SaveMesh(SectionGlow(gold), Assets, name + "_LogVeins");
            var stumpPrefab = Prefab("TW07_" + name + "_Stump", root =>
            {
                MeshOn(root.transform, "CutSurface", stump, surface);
                var collider = Component<CapsuleCollider>(root.transform);
                collider.center = Vector3.up * .14f;
                collider.radius = .36f;
                collider.height = .74f;
                collider.sharedMaterial = slip;
            });
            var section = Prefab("TW07_" + name + "_Section", root =>
            {
                MeshOn(root.transform, "EndGrainAndBark", log, surface);
                MeshOn(root.transform, "Veins", logGlow, light, false);
            });
            var bark = SaveMesh(Bark(gold), Assets, name + "_Bark");
            var leaves = SaveMesh(Leaves(gold), Assets, name + "_Canopy");
            var veins = SaveMesh(Glow(gold), Assets, name + "_StandingVeins");
            return Prefab("TW07_" + name + "_Tree", root =>
            {
                root.tag = "Resource";
                MeshOn(root.transform, "Bark", bark, surface);
                MeshOn(root.transform, "Crown", leaves, surface);
                MeshOn(root.transform, "RuneAndVeins", veins, light, false);
                Motes(Child(root.transform, "CanopyMotes"), motes, gold);

                var solid = Component<CapsuleCollider>(Child(root.transform, TreeCollision.TrunkName));
                solid.center = Vector3.up * 1.07f;
                solid.height = 2.2f;
                solid.radius = TrunkRadius;
                solid.isTrigger = false;
                solid.sharedMaterial = slip;
                var pick = Component<CapsuleCollider>(root.transform);
                pick.center = Vector3.up * 2.24f;
                pick.height = Height;
                pick.radius = 1.19f;
                pick.isTrigger = true;

                var info = Component<TreeSpeciesInfo>(root.transform);
                info.species = name;
                info.pickHeight = Height;
                info.pickRadius = pick.radius;
                info.trunkHeight = solid.height;
                info.trunkRadius = solid.radius;
                var resource = Component<ResourceItem>(root.transform);
                resource.respawn = true;
                resource.autoSize = false;
                resource.useGeneratedModel = false;
                resource.overrideSize = true;
                resource.forcedSize = TreeSize.Mature;
                resource.growVisual = true;
                resource.stumpPrefab = stumpPrefab;

                var profile = Component<SpecialHarvestTree>(root.transform);
                profile.kind = gold ? SpecialTreeKind.Golden : SpecialTreeKind.Rune;
                profile.sectionPrefab = section;
            });
        }

        static void Motes(Transform at, Material material, bool gold)
        {
            at.localPosition = new Vector3(0f, 2.12f, 0f);
            ConfigureMotes(Component<ParticleSystem>(at), material, .90f,
                gold ? new Color(1f, .75f, .36f, .82f) : new Color(.56f, .92f, .82f, .74f));
        }

        static void ConfigureMotes(ParticleSystem emitter, Material material, float radius, Color tint)
        {
            emitter.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = emitter.main;
            main.playOnAwake = true;
            main.loop = true;
            main.maxParticles = 12;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.3f, 2.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.09f, .24f);
            main.startSize = new ParticleSystem.MinMaxCurve(.023f, .062f);
            main.startColor = tint;
            var shape = emitter.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;
            var rate = emitter.emission;
            rate.rateOverTime = 3f;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(.86f, .24f), new GradientAlphaKey(0f, 1f) });
            var color = emitter.colorOverLifetime;
            color.enabled = true;
            color.color = fade;
            var renderer = emitter.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        static Material MakeMaterial(Shader shader, string name, Color emission)
        {
            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", Color.white);
            material.SetColor("_EmissionColor", emission);
            material.SetFloat("_VertexColorStrength", 1f);
            material.SetFloat("_PosVary", .043f);
            material.SetFloat("_GlossStrength", .026f);
            return SaveAsset(material, Assets + "/" + name + ".mat");
        }

        static MaterialPhysics MakeSlip()
        {
            var material = new MaterialPhysics("TW07_Trunk")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum
            };
            return SaveAsset(material, Assets + "/TW07_Trunk.physicMaterial");
        }

        static Mesh SaveMesh(SoftMesh model, string folder, string name)
            => SaveAsset(model.ToMesh(name), folder + "/" + name + ".asset");

        static T SaveAsset<T>(T fresh, string path) where T : Object
        {
            T current = AssetDatabase.LoadAssetAtPath<T>(path);
            if (current == null)
            {
                AssetDatabase.CreateAsset(fresh, path);
                return fresh;
            }
            EditorUtility.CopySerialized(fresh, current);
            EditorUtility.SetDirty(current);
            Object.DestroyImmediate(fresh);
            return current;
        }

        static GameObject Prefab(string name, Action<GameObject> edit)
        {
            string path = Assets + "/" + name + ".prefab";
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
            var root = exists ? PrefabUtility.LoadPrefabContents(path) : new GameObject(name);
            try
            {
                edit(root);
                var saved = PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
                Require(success && saved != null, "Ошибка сохранения префаба — " + name);
                return saved;
            }
            finally
            {
                if (exists) PrefabUtility.UnloadPrefabContents(root);
                else Object.DestroyImmediate(root);
            }
        }

        static void SceneMesh(Transform parent, string name, Mesh mesh, Material material)
        {
            var child = SceneChild(parent, name);
            var filter = SceneComponent<MeshFilter>(child);
            var renderer = SceneComponent<MeshRenderer>(child);
            filter.sharedMesh = mesh;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        static void MeshOn(Transform parent, string name, Mesh mesh, Material material, bool shadows = true)
        {
            var child = Child(parent, name);
            Component<MeshFilter>(child).sharedMesh = mesh;
            var renderer = Component<MeshRenderer>(child);
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        }

        static Transform SceneChild(Transform parent, string name)
        {
            var item = parent.Find(name);
            if (item != null) return item;
            item = Child(parent, name);
            Undo.RegisterCreatedObjectUndo(item.gameObject, EditName);
            return item;
        }

        static Transform Child(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null) return existing;
            var created = new GameObject(name).transform;
            created.SetParent(parent, false);
            return created;
        }

        static T Component<T>(Transform host) where T : Component
            => host.TryGetComponent<T>(out var existing) ? existing : host.gameObject.AddComponent<T>();

        static T SceneComponent<T>(Transform host) where T : Component
        {
            if (!host.TryGetComponent<T>(out var existing)) return Undo.AddComponent<T>(host.gameObject);
            Undo.RecordObject(existing, EditName);
            return existing;
        }

        static void Require(bool state, string error)
        {
            if (!state) throw new InvalidOperationException(error);
        }
    }
}
