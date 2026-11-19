using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using static IslandV2.TWRuneForgeModel;
using Object = UnityEngine.Object;

namespace IslandV2
{
    internal static class TWRuneForgeBaker
    {
        const string Menu = "Tools/BuilderSim/Здания/Рунная кузница/";
        internal const string Folder = "Assets/_Project/IslandV2/RuneForge";
        internal const string RootName = "TW_RuneForge";
        internal const string RootPath = Folder + "/" + RootName + ".prefab";
        internal const string BuildZoneName = "BuildZone";
        const string UndoName = "TimberWings — рунная кузница";
        internal const int Planks = 8;
        internal const int Coins = 60;

        [MenuItem(Menu + "1. Запечь в рабочем дворе", false, 330)]
        static void BakeMenu() => Run(() =>
        {
            var forge = Bake();
            Selection.activeGameObject = forge.gameObject;
            Debug.Log("TW06: кузница запечена. Сохрани игровую сцену (Ctrl+S).", forge);
        });

        [MenuItem(Menu + "2. Запечь у выбранной точки", false, 331)]
        static void BakeAtSelection() => Run(() =>
        {
            var marker = Selection.activeTransform;
            if (marker == null || marker.gameObject.scene != SceneManager.GetActiveScene()
                || marker.GetComponentInParent<RuneForge>() != null)
                throw new InvalidOperationException("Выбери объект в игровой сцене как точку установки. Вход будет смотреть на рабочий двор.");
            var forge = Bake(marker);
            Selection.activeGameObject = forge.gameObject;
            Debug.Log("TW06: кузница запечена в выбранной точке. Сохрани сцену (Ctrl+S).", forge);
        });

        [MenuItem(Menu + "3. Запечь дважды и проверить", false, 332)]
        static void RunRepeatCheck() => Run(TWRuneForgeBakeCheck.CheckRepeat);

        static void Run(Action operation)
        {
            try { operation(); }
            catch (Exception error) { Debug.LogError("TW06 · Кузница. " + error); }
        }

        internal static RuneForge Bake(Transform marker = null)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Запекание доступно в Edit Mode после компиляции.");
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(scene.path)
                || PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("Открой сохранённую игровую сцену острова.");

            var forges = SceneObjects<RuneForge>(scene);
            if (forges.Length > 1)
                throw new InvalidOperationException("В сцене несколько кузниц. Оставь один экземпляр с RuneForge.");
            var forge = forges.SingleOrDefault();
            if (forge == null && scene.GetRootGameObjects().Any(root => root.name == RootName))
                throw new InvalidOperationException("В корне сцены уже есть " + RootName + ". Проверь его компонент RuneForge.");
            if (forge != null && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(forge.gameObject) != RootPath)
                throw new InvalidOperationException("Экземпляр кузницы должен быть связан с префабом " + RootName + ".");

            var story = SceneObjects<StoryFlow>(scene).FirstOrDefault(component => component.isActiveAndEnabled);
            var sites = SceneObjects<BuildingController>(scene);
            var hut = story != null && story.hut != null ? story.hut : StoryFlow.FindSite(StoryFlow.Site.Hut, sites);
            var workyard = story != null && story.tent != null ? story.tent : StoryFlow.FindSite(StoryFlow.Site.Tent, sites);
            if (hut == null || workyard == null || hut == workyard || hut.gameObject.scene != scene || workyard.gameObject.scene != scene)
                throw new InvalidOperationException("Нужна игровая сцена с двумя стройками: хижиной и рабочим двором.");

            Pose pose = default;
            if (forge == null || marker != null)
            {
                Transform yard = workyard.GetComponentInParent<TentStages>()?.transform ?? workyard.transform;
                pose = TWRuneForgePlacement.Choose(scene, yard, forge, marker);
            }

            GameObject recipe = BakeAssets();
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);
            try
            {
                if (forge == null)
                {
                    var placed = (GameObject)PrefabUtility.InstantiatePrefab(recipe, scene);
                    Undo.RegisterCreatedObjectUndo(placed, UndoName);
                    forge = placed.GetComponent<RuneForge>();
                    SetPose(forge.transform, pose);
                }
                else if (marker != null)
                    SetPose(forge.transform, pose);

                var sceneZone = forge.transform.Find(BuildZoneName);
                if (sceneZone == null)
                    throw new InvalidOperationException("В экземпляре кузницы отсутствует дочерний объект BuildZone.");
                var bakedData = recipe.GetComponent<RuneForge>().build.data;
                ApplyBuildZone(forge, sceneZone, bakedData, recordUndo: true);

                if (forge.hut != hut || forge.workyard != workyard)
                {
                    Undo.RecordObject(forge, UndoName);
                    forge.hut = hut;
                    forge.workyard = workyard;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(forge);
                }
                TWRuneForgeBakeCheck.Validate(forge);
                EditorSceneManager.MarkSceneDirty(scene);
                Undo.CollapseUndoOperations(group);
                return forge;
            }
            catch
            {
                Undo.RevertAllDownToGroup(group);
                throw;
            }
        }

        internal static T[] SceneObjects<T>(Scene scene) where T : Component
            => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        static void SetPose(Transform transform, Pose pose)
        {
            Undo.RecordObject(transform, UndoName);
            transform.SetPositionAndRotation(pose.position, pose.rotation);
            PrefabUtility.RecordPrefabInstancePropertyModifications(transform);
        }

        static GameObject BakeAssets()
        {
            IslandLayout.EnsureFolder(Folder);
            Shader toon = Shader.Find("Project/CandyToon");
            Shader particle = Shader.Find("Project/CandyParticle");
            if (toon == null || particle == null)
                throw new InvalidOperationException("В проекте нужны шейдеры CandyToon и CandyParticle.");

            var timber = SaveMaterial("Forge_TimberAndStone", toon, Color.black);
            var amber = SaveMaterial("Forge_AmberGlow", toon, SoftMesh.C(0xFFC168) * .60f);
            var jade = SaveMaterial("Forge_RuneGlow", toon, SoftMesh.C(0x83E7C3) * .72f);
            var particles = SaveMaterial("Forge_Particles", particle, Color.black);
            var data = Store(BuildData(), "RuneForge_Restoration.asset");
            var ruined = BakePhase(Phase.Ruined, timber, amber, jade, particles);
            var frame = BakePhase(Phase.Frame, timber, amber, jade, particles);
            var restored = BakePhase(Phase.Restored, timber, amber, jade, particles);
            var floorMesh = Store(Base().ToMesh("Forge_Foundation"), "Forge_Foundation.asset");
            var standMesh = Store(RepairStand().ToMesh("Forge_RepairStand"), "Forge_RepairStand.asset");

            var prefab = EditPrefab(RootName, root =>
            {
                var forge = GetOrAdd<RuneForge>(root.transform);
                MeshOn(root.transform, "Foundation", floorMesh, timber);
                AddFoundationCollision(Child(root.transform, "GroundCollision"));

                forge.ruined = Nested(root.transform, ruined, true);
                forge.repairFrame = Nested(root.transform, frame, false);
                forge.restored = Nested(root.transform, restored, false);

                var points = Child(root.transform, "Points");
                forge.entrance = Anchor(points, "Entrance", new Vector3(0f, Floor, -3.2f));
                forge.interior = Anchor(points, "Interior", new Vector3(0f, Floor, -.36f));
                forge.oskarPoint = Anchor(points, "Oskar", new Vector3(2.27f, Floor, -3.12f),
                    Quaternion.Euler(0f, -122f, 0f));
                forge.mapPoint = Anchor(points, "MapMarker", new Vector3(0f, Floor, -.30f));

                var outside = Anchor(points, "InspectRuins", new Vector3(-1.94f, Floor, -3.38f));
                var ruinsInspect = GetOrAdd<RuneForgeInspection>(outside);
                ruinsInspect.forge = forge;
                ruinsInspect.inside = false;
                var inside = Anchor(points, "InspectTable", new Vector3(.91f, Floor, .10f));
                var tableInspect = GetOrAdd<RuneForgeInspection>(inside);
                tableInspect.forge = forge;
                tableInspect.inside = true;
                ConfigureBuildZone(root.transform, forge, data, standMesh, timber);
            });
            AssetDatabase.SaveAssets();
            return prefab;
        }

        static BuildSiteData BuildData()
        {
            var data = ScriptableObject.CreateInstance<BuildSiteData>();
            data.name = "RuneForge_Restoration";
            data.buildingName = RuneForge.Title;
            data.startStage = 1;
            data.steps = new[]
            {
                new BuildSiteData.Step
                {
                    name = "Розжиг горна",
                    description = "Почини навес и верни огонь в горн Гуннара.",
                    woodCost = 0,
                    plankCost = Planks,
                    coinCost = Coins,
                    buildTime = 4.2f,
                    rewardXp = 0
                }
            };
            return data;
        }

        static void ConfigureBuildZone(Transform root, RuneForge forge, BuildSiteData data, Mesh standMesh, Material timber)
        {
            var zone = Anchor(root, BuildZoneName, new Vector3(0f, -.13f, -4.62f));
            MeshOn(zone, "RepairStand", standMesh, timber);
            Box(zone, "StandPost", new Vector3(1.66f, .40f, .19f), new Vector3(.20f, 1.8f, .20f));
            Box(zone, "StandBoard", new Vector3(1.66f, .97f, .10f), new Vector3(.61f, .38f, .20f));
            ApplyBuildZone(forge, zone, data, recordUndo: false);
        }

        static void ApplyBuildZone(RuneForge forge, Transform zone, BuildSiteData data, bool recordUndo)
        {
            // Prefab updates leave existing scene overrides in place. Reapply the generated settings to this zone.
            if (!zone.gameObject.activeSelf)
            {
                if (recordUndo) Undo.RecordObject(zone.gameObject, UndoName);
                zone.gameObject.SetActive(true);
            }
            var trigger = GetOrAdd<BoxCollider>(zone, recordUndo);
            trigger.center = new Vector3(0f, 1.15f, 0f);
            trigger.size = new Vector3(2.50f, 3.0f, 1.96f);
            trigger.isTrigger = true;
            trigger.enabled = true;

            var build = GetOrAdd<BuildingController>(zone, recordUndo);
            build.enabled = true;
            build.data = data;
            build.locked = true;
            build.useWindow = true;
            build.storyManaged = true;
            build.completesTutorial = false;
            build.lockPlayerWhileBuilding = true;
            build.finishOnStrike = false;
            build.finishedBuilding = forge.restored;
            build.useRing = false;
            if (forge.build != build)
            {
                if (recordUndo) Undo.RecordObject(forge, UndoName);
                forge.build = build;
            }

            var prompt = GetOrAdd<BuildSiteInteractable>(zone, recordUndo);
            prompt.enabled = true;
            prompt.radius = 2.4f;
            prompt.promptOffset = Vector3.up * 1.2f;
            var tag = GetOrAdd<BuildSiteTagView>(zone, recordUndo);
            tag.buildingName = RuneForge.Title;
            tag.lockedAfter = "хижина и рабочий двор";

            if (recordUndo)
                foreach (Object item in new Object[] { zone.gameObject, trigger, build, prompt, tag, forge })
                    PrefabUtility.RecordPrefabInstancePropertyModifications(item);
        }

        static GameObject BakePhase(Phase phase, Material timber, Material amber, Material jade, Material particles)
        {
            string name = "Forge_" + phase;
            var architecture = Store(Architecture(phase).ToMesh(name + "_Architecture"), name + "_Architecture.asset");
            var equipment = Store(Equipment(phase).ToMesh(name + "_Equipment"), name + "_Equipment.asset");
            return EditPrefab(name, root =>
            {
                MeshOn(root.transform, "Architecture", architecture, timber);
                MeshOn(root.transform, "Equipment", equipment, timber);
                AddStageCollision(Child(root.transform, "StructureCollision"), phase);
                if (phase != Phase.Restored) return;

                var warm = Store(WarmGlow().ToMesh("Forge_WarmGlow"), "Forge_WarmGlow.asset");
                var rune = Store(RuneGlow().ToMesh("Forge_RuneGlow"), "Forge_RuneGlow.asset");
                MeshOn(root.transform, "AmberGlow", warm, amber, shadows: false);
                MeshOn(root.transform, "RuneGlow", rune, jade, shadows: false);

                var fx = Child(root.transform, "HearthVfx");
                PointLight(fx, "HearthLight", Fire + new Vector3(0f, .16f, -.32f),
                    new Color(1f, .59f, .31f), 1.60f, 4.75f);
                PointLight(fx, "LanternLight", Lantern + Vector3.back * .11f,
                    new Color(1f, .75f, .50f), .55f, 3.6f);
                Emitter(fx, "Embers", Fire + new Vector3(0f, .14f, -.04f), particles, smoke: false);
                Emitter(fx, "ChimneySmoke", Chimney + Vector3.up * .30f, particles, smoke: true);
            });
        }

        static void AddFoundationCollision(Transform physics)
        {
            float depth = Back - DeckFront;
            var deck = new Vector3(HalfWidth * 2f, .76f, depth + .012f);
            Box(physics, "Deck", new Vector3(0f, Floor - deck.y / 2f, (Back + DeckFront) / 2f), deck);
            var toe = new Vector3(0f, RampLow, RampEnd);
            var lip = new Vector3(0f, Floor, DeckFront);
            Quaternion slope = Quaternion.LookRotation(lip - toe, Vector3.up);
            Box(physics, "Ramp", (toe + lip) * .5f - slope * Vector3.up * .095f,
                new Vector3(RampHalfWidth * 2f, .19f, Vector3.Distance(toe, lip) + .015f), slope);
        }

        static void AddStageCollision(Transform physics, Phase phase)
        {
            bool ready = phase == Phase.Restored;
            float wallHeight = ready ? 2.52f : 1.02f;
            float wallMid = Floor + wallHeight * .5f;
            foreach (int side in new[] { -1, 1 })
            {
                string suffix = side < 0 ? "Left" : "Right";
                float wing = Wall - DoorHalfWidth;
                Box(physics, "FrontWall" + suffix, new Vector3(side * (DoorHalfWidth + wing * .5f), wallMid, Front),
                    new Vector3(wing + .06f, wallHeight, .26f));
                float sill = ready ? SideSillTop - Floor : wallHeight;
                Box(physics, "SideWall" + suffix, new Vector3(side * Wall, Floor + sill * .5f, (Back + Front) * .5f),
                    new Vector3(ready ? .34f : .24f, sill, Back - Front));
                if (!ready) continue;

                for (int i = 0; i < 3; i++)
                {
                    float z = Mathf.Lerp(Front, Back - .11f, i / 2f);
                    Box(physics, "SidePost" + suffix + i, new Vector3(side * Wall, (SideSillTop + 2.95f) * .5f, z),
                        new Vector3(.26f, 2.95f - SideSillTop, .26f));
                }
                Vector3 eave = Eave(side);
                Box(physics, "Roof" + suffix, (eave + Ridge) * .5f, new Vector3(Vector3.Distance(eave, Ridge), .15f, 6.46f),
                    RoofRotation(side));
            }

            Box(physics, "BackWall", new Vector3(0f, wallMid, Back - .10f), new Vector3(6f, wallHeight, .23f));
            if (!ready)
            {
                Box(physics, "EntryBarrier", new Vector3(0f, .98f, Front - .07f), new Vector3(2.58f, 1.46f, .24f));
                if (phase == Phase.Ruined)
                    Box(physics, "FallenRoof", (FallenRoofTop + FallenRoofBottom) * .5f,
                        new Vector3(Vector3.Distance(FallenRoofTop, FallenRoofBottom), .17f, 2.67f), FallenRoofRotation);
                return;
            }

            Box(physics, "Canopy", new Vector3(0f, 2.78f, -3.25f), new Vector3(6.36f, .135f, 1.22f),
                Quaternion.Euler(-13f, 0f, 0f));
            Box(physics, "DoorHeader", new Vector3(0f, 2.77f, Front), new Vector3(2.54f, .16f, .24f));
            Box(physics, "Hearth", new Vector3(Fire.x, 1.02f, 1.80f), new Vector3(1.94f, 1.57f, 1.79f));
            Box(physics, "AnvilPost", Anvil + Vector3.up * .34f, new Vector3(.81f, .68f, .80f));
            Box(physics, "AnvilHead", Anvil + new Vector3(.15f, .92f, 0f), new Vector3(1.21f, .30f, .54f));
            Box(physics, "RuneTable", Workbench + Vector3.up * .49f, new Vector3(1.7f, .98f, 1.24f));
        }

        static void Box(Transform parent, string name, Vector3 center, Vector3 size, Quaternion? rotation = null)
        {
            var child = Child(parent, name);
            child.localPosition = center;
            child.localRotation = rotation ?? Quaternion.identity;
            var collider = GetOrAdd<BoxCollider>(child);
            collider.center = Vector3.zero;
            collider.size = size;
            collider.isTrigger = false;
        }

        static void PointLight(Transform parent, string name, Vector3 position, Color color, float intensity, float range)
        {
            var point = Child(parent, name);
            point.localPosition = position;
            var lamp = GetOrAdd<Light>(point);
            lamp.type = LightType.Point;
            lamp.color = color;
            lamp.intensity = intensity;
            lamp.range = range;
            lamp.shadows = LightShadows.None;
        }

        static void Emitter(Transform parent, string name, Vector3 position, Material material, bool smoke)
        {
            var source = Child(parent, name);
            source.localPosition = position;
            source.localRotation = Quaternion.LookRotation(Vector3.up, Vector3.back);
            var ps = GetOrAdd<ParticleSystem>(source);
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = smoke ? 24 : 32;
            main.startLifetime = new ParticleSystem.MinMaxCurve(smoke ? 1.15f : .45f, smoke ? 2.05f : 1.20f);
            main.startSize = new ParticleSystem.MinMaxCurve(smoke ? .18f : .027f, smoke ? .38f : .075f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(smoke ? .52f : .28f, smoke ? .85f : .78f);
            main.startColor = smoke
                ? new ParticleSystem.MinMaxGradient(new Color(.69f, .66f, .63f, .25f), new Color(.82f, .76f, .70f, .36f))
                : new ParticleSystem.MinMaxGradient(new Color(1f, .55f, .22f), new Color(1f, .83f, .42f));

            var emission = ps.emission;
            emission.rateOverTime = smoke ? 6f : 10f;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = smoke ? 22f : 13f;
            shape.radius = smoke ? .085f : .20f;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .15f), new GradientAlphaKey(0f, 1f) });
            var color = ps.colorOverLifetime;
            color.enabled = true;
            color.color = fade;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        static Material SaveMaterial(string name, Shader shader, Color emission)
        {
            var material = new Material(shader) { name = name };
            if (material.HasProperty("_VertexColorStrength"))
            {
                material.SetColor("_BaseColor", Color.white);
                material.SetColor("_EmissionColor", emission);
                material.SetFloat("_VertexColorStrength", 1f);
                material.SetFloat("_PosVary", .036f);
                material.SetFloat("_GlossStrength", .035f);
                material.SetFloat("_RimStrength", .07f);
            }
            return Store(material, name + ".mat");
        }

        static void MeshOn(Transform parent, string name, Mesh mesh, Material material, bool shadows = true)
        {
            var child = Child(parent, name);
            GetOrAdd<MeshFilter>(child).sharedMesh = mesh;
            var renderer = GetOrAdd<MeshRenderer>(child);
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = shadows;
        }

        static T Store<T>(T fresh, string filename) where T : Object
        {
            string path = Folder + "/" + filename;
            var current = AssetDatabase.LoadAssetAtPath<T>(path);
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

        static GameObject EditPrefab(string name, Action<GameObject> edit)
        {
            string path = Folder + "/" + name + ".prefab";
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
            GameObject root = exists ? PrefabUtility.LoadPrefabContents(path) : new GameObject(name);
            try
            {
                edit(root);
                var output = PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
                if (!success || output == null)
                    throw new InvalidOperationException("Не удалось сохранить префаб " + path);
                return output;
            }
            finally
            {
                if (exists) PrefabUtility.UnloadPrefabContents(root);
                else Object.DestroyImmediate(root);
            }
        }

        static GameObject Nested(Transform parent, GameObject prefab, bool active)
        {
            var existing = parent.Find(prefab.name);
            var instance = existing != null ? existing.gameObject : (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instance) != AssetDatabase.GetAssetPath(prefab))
                throw new InvalidOperationException("В префабе чужой объект с именем " + prefab.name);
            instance.SetActive(active);
            return instance;
        }

        static Transform Child(Transform parent, string name) => Anchor(parent, name, Vector3.zero);

        static Transform Anchor(Transform parent, string name, Vector3 position, Quaternion? rotation = null)
        {
            var existing = parent.Find(name);
            if (existing != null) return existing;
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            child.localPosition = position;
            child.localRotation = rotation ?? Quaternion.identity;
            return child;
        }

        static T GetOrAdd<T>(Transform host, bool recordUndo = false) where T : Component
        {
            if (!host.TryGetComponent<T>(out var current))
                return recordUndo ? Undo.AddComponent<T>(host.gameObject) : host.gameObject.AddComponent<T>();
            if (recordUndo) Undo.RecordObject(current, UndoName);
            return current;
        }
    }
}
