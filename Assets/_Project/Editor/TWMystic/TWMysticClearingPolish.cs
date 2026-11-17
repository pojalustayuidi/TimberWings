using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace IslandV2
{
    internal static class TWMysticClearingPolish
    {
        internal const string Art = "Assets/_Project/Art/MysticForestPolish/";
        internal const string BoardVisual = "BenchBoard_Visual";
        internal const string CedarGround = "Cedar_Root_Details";
        internal const string UndoName = "Полировка Круга молчания";
        const string Audio = "Assets/_Project/Art/MysticCedar/NightDetail/";

        static readonly string[] BenchKeys =
            { "Bench_0", "Bench_1", "Bench_2", "Bench_3", "Bench_4", "Bench_5", "SecretBench" };

        // Behind and to either side of the trunk, clear of the swing lane.
        static readonly RootSite[] RootSites =
        {
            new RootSite("Мох между корнями", "Roots_Moss", -1.71f, -.84f, -16f),
            new RootSite("Камни и лишайник", "Roots_WeatheredStone", 1.48f, -.98f, 27f),
            new RootSite("Листья и щепки", "Roots_Litter", -2.48f, .38f, 48f),
            new RootSite("Опавшая хвоя", "Roots_Needles", 1.95f, .39f, -24f)
        };

        readonly struct RootSite
        {
            internal readonly string name, asset;
            internal readonly Vector3 offset;
            internal readonly float yaw;

            internal RootSite(string name, string asset, float x, float z, float yaw)
            {
                this.name = name;
                this.asset = asset;
                offset = new Vector3(x, 0f, z);
                this.yaw = yaw;
            }
        }

        internal sealed class Targets
        {
            internal Scene Scene;
            internal Transform[] Benches, Signs;
            internal Transform Cedar, SecretBack, Board, Witness, Slab, Ribbon;
            internal MeshCollider Ground;
            internal DialogueSequence BoardDialogue;
            internal StoryInspection RibbonInspection;
        }

        internal static Targets FindTargets()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(scene.path))
                throw new InvalidOperationException("Открой сохранённый игровой уровень TimberWings.");

            var roots = scene.GetRootGameObjects();
            var states = roots.SelectMany(go => go.GetComponentsInChildren<MysticArtState>(true))
                .Where(state => state.art != null && state.art.activeInHierarchy && state.art.scene == scene)
                .ToArray();
            if (states.Length != 1)
                throw new InvalidOperationException($"Нужен один собранный Круг молчания. В сцене найдено: {states.Length}.");

            var forest = states[0].art.transform;
            var props = forest.GetComponentsInChildren<MysticProp>(true);
            var bindings = roots.SelectMany(go => go.GetComponentsInChildren<StoryInspection>(true))
                .Where(inspection => inspection.isActiveAndEnabled && !inspection.superseded &&
                    inspection.source != null && inspection.source.isActiveAndEnabled)
                .ToArray();

            var benches = BenchKeys.Select(key => OneProp(props, key).transform).ToArray();
            var board = StoryOwner(props, bindings, "Bench_Memory", StoryInspection.Kind.Board);
            var witness = StoryOwner(props, bindings, "Witness_1", StoryInspection.Kind.Witness);
            var ribbon = StoryOwner(props, bindings, "Ribbon_Memory", StoryInspection.Kind.Ribbon);
            var gunnar = StoryOwner(props, bindings, "Gunnar_Memory", StoryInspection.Kind.Knife);
            var stockGunnar = OneProp(props, "Gunnar_Memory").transform;
            var signBinding = gunnar.GetComponent<StoryInspection>();
            var signs = new[]
            {
                signBinding != null ? signBinding.bridgeEngraving : null,
                signBinding != null ? signBinding.copiedBridge : null,
                gunnar.Find("Bridge_Engraving"),
                stockGunnar.Find("Bridge_Engraving")
            }.Where(sign => sign != null && sign.gameObject.scene == scene &&
                sign.name == "Bridge_Engraving" && sign.TryGetComponent<MeshFilter>(out _))
                .Distinct().ToArray();
            if (signs.Length == 0)
                throw new InvalidOperationException("В сцене не найдена существующая геометрия Bridge_Engraving.");
            if (!board.TryGetComponent<DialogueSequence>(out var boardDialogue))
                throw new InvalidOperationException("У старой дощечки отсутствует DialogueSequence.");
            var ground = forest.Find("00_Conforming_Ground");

            return new Targets
            {
                Scene = scene,
                Benches = benches,
                Signs = signs,
                SecretBack = RequireChild(benches[6], "Signature_Backboard"),
                Cedar = RequireChild(forest, "Cedar_Landmark"),
                Ground = ground != null ? ground.GetComponent<MeshCollider>() : null,
                Board = board,
                BoardDialogue = boardDialogue,
                Witness = witness,
                Slab = RequireChild(witness, "Engraving_Witness"),
                Ribbon = ribbon,
                RibbonInspection = ribbon.GetComponent<StoryInspection>()
            };
        }

        static MysticProp OneProp(MysticProp[] props, string key)
        {
            var matches = props.Where(p => p.key == key).ToArray();
            if (matches.Length != 1)
                throw new InvalidOperationException($"В Круге молчания нужен один {key}. Найдено: {matches.Length}.");
            return matches[0];
        }

        static Transform StoryOwner(MysticProp[] props, StoryInspection[] bindings, string key, StoryInspection.Kind kind)
        {
            var active = bindings.Where(binding => binding.kind == kind &&
                binding.source.TryGetComponent<MysticProp>(out var prop) && prop.key == key)
                .Select(binding => binding.source.transform).Distinct().ToArray();
            if (active.Length > 1)
                throw new InvalidOperationException($"У {key} несколько активных осмотров. Проверь привязки в сцене.");
            return active.Length == 1 ? active[0] : OneProp(props, key).transform;
        }

        static Transform RequireChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child == null)
                throw new InvalidOperationException($"Не найден {name} внутри {parent.name}.");
            return child;
        }

        internal static T Asset<T>(string key) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(Art + key);
            if (asset == null)
                throw new InvalidOperationException("Не импортирован ресурс " + Art + key);
            return asset;
        }

        internal static string MaterialFolder(Scene scene) =>
            Art + "SceneOverrides/" + AssetDatabase.AssetPathToGUID(scene.path);

        internal static void Apply()
        {
            var terrain = TWMysticArt.Inspect();
            var targets = FindTargets();
            var benchMeshes = BenchKeys.Select(TWMysticBenchPolish.Load).ToArray();
            var secretBack = TWMysticBenchPolish.Load("Signature_Backboard");
            var boardMesh = Asset<Mesh>("Bench_Board.asset");
            var signMesh = Asset<Mesh>("Gunnar_Bridge_Sign.asset");
            var rootMeshes = RootSites.Select(site => Asset<Mesh>(site.asset + ".asset")).ToArray();
            var wood = Asset<Material>("Clearing_Wood.mat");
            var mask = Asset<Texture2D>("Ribbon_Tallies.png");

            foreach (var mesh in benchMeshes.Concat(rootMeshes).Concat(new[] { secretBack, boardMesh, signMesh }))
                TWMysticValidation.Mesh(mesh);
            var benchFilters = targets.Benches.Select(MeshAt).ToArray();
            var backFilter = MeshAt(targets.SecretBack);
            var signFilters = targets.Signs.Select(MeshAt).ToArray();
            var slabRenderer = targets.Slab.GetComponent<MeshRenderer>();
            var boardRenderer = targets.Board.GetComponent<MeshRenderer>();
            if (slabRenderer == null || boardRenderer == null)
                throw new InvalidOperationException("Не найден MeshRenderer дощечки или каменной плиты.");
            RequireCompatibleBinding(targets.Board, targets.BoardDialogue, StoryInspection.Kind.Board);
            RequireCompatibleBinding(targets.Witness, targets.Witness.GetComponent<MysticProp>(), StoryInspection.Kind.Witness);

            string folder = MaterialFolder(targets.Scene);
            var ribbon = TWMysticRibbonInk.Prepare(targets.Ribbon, targets.RibbonInspection);
            var createdAssets = new List<string>();
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);
            try
            {
                for (int i = 0; i < benchFilters.Length; i++)
                    UseMesh(benchFilters[i], benchMeshes[i]);
                UseMesh(backFilter, secretBack);
                foreach (var filter in signFilters)
                    UseMesh(filter, signMesh);

                BakeBoard(terrain, targets, boardRenderer, boardMesh, wood);
                BakeCedarGround(terrain, targets.Cedar, targets.Ground, rootMeshes, wood);
                Bind(targets.Witness, targets.Witness.GetComponent<MysticProp>(),
                    StoryInspection.Kind.Witness, targets.Slab, "Witness_Echo");
                TWMysticClearingSurface.PolishSlab(slabRenderer, folder, createdAssets);
                TWMysticRibbonInk.Apply(ribbon, mask, folder, createdAssets);

                EditorSceneManager.MarkSceneDirty(targets.Scene);
                Undo.CollapseUndoOperations(group);
                SceneView.RepaintAll();
                Debug.Log("Круг молчания: полировка применена. Сохрани сцену Ctrl+S.", targets.Cedar);
            }
            catch
            {
                Undo.RevertAllDownToGroup(group);
                for (int i = createdAssets.Count - 1; i >= 0; i--)
                    AssetDatabase.DeleteAsset(createdAssets[i]);
                throw;
            }
        }

        static MeshFilter MeshAt(Transform target)
        {
            if (!target.TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh == null ||
                !target.TryGetComponent<MeshRenderer>(out var renderer) || renderer.sharedMaterials.Length != 1)
                throw new InvalidOperationException("Ожидался существующий меш с одним материалом у " + target.name);
            return filter;
        }

        static void UseMesh(MeshFilter filter, Mesh mesh)
        {
            if (filter.sharedMesh == mesh) return;
            Undo.RecordObject(filter, UndoName);
            filter.sharedMesh = mesh;
            PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
        }

        internal static Transform Child(Transform parent, string name, Vector3 localPosition, Quaternion localRotation)
        {
            var existing = parent.Find(name);
            if (existing != null) return existing;
            var node = new GameObject(name) { layer = parent.gameObject.layer };
            Undo.RegisterCreatedObjectUndo(node, UndoName);
            Undo.SetTransformParent(node.transform, parent, UndoName);
            Undo.RecordObject(node.transform, UndoName);
            node.transform.SetLocalPositionAndRotation(localPosition, localRotation);
            node.transform.localScale = Vector3.one;
            return node.transform;
        }

        static void AttachArt(Transform visual, Mesh mesh, Material material)
        {
            if (!visual.TryGetComponent<MeshFilter>(out var filter))
                filter = Undo.AddComponent<MeshFilter>(visual.gameObject);
            UseMesh(filter, mesh);

            if (visual.TryGetComponent<MeshRenderer>(out var renderer))
            {
                if (renderer.sharedMaterial != null) return;
            }
            else
                renderer = Undo.AddComponent<MeshRenderer>(visual.gameObject);
            TWMysticClearingSurface.ApplyMaterial(renderer, material);
        }

        static void BakeBoard(TWMysticContext terrain, Targets targets, MeshRenderer oldRenderer, Mesh mesh, Material material)
        {
            var board = targets.Board;
            bool firstBake = board.Find(BoardVisual) == null;
            var prop = board.GetComponent<MysticProp>();
            if (firstBake && (board.position - prop.defaultPosition).sqrMagnitude < .015f * .015f &&
                Quaternion.Angle(board.rotation, prop.defaultRotation) < .1f)
            {
                var bench = targets.Benches[4];
                var candidate = bench.TransformPoint(new Vector3(-1.04f, 0f, -.88f));
                if (TrySurface(terrain, targets.Ground, candidate, out var ground) &&
                    Mathf.Abs(candidate.y - ground.y) < .32f)
                {
                    Undo.RecordObject(board, UndoName);
                    board.position = ground;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(board);
                }
            }

            var visual = Child(board, BoardVisual, new Vector3(0f, .067f, 0f), Quaternion.Euler(9f, -16f, -4f));
            AttachArt(visual, mesh, material);
            if (oldRenderer.enabled)
            {
                Undo.RecordObject(oldRenderer, UndoName);
                oldRenderer.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(oldRenderer);
            }
            if (firstBake && (targets.BoardDialogue.promptOffset - Vector3.up * 1.3f).sqrMagnitude < .001f)
            {
                Undo.RecordObject(targets.BoardDialogue, UndoName);
                targets.BoardDialogue.promptOffset = Vector3.up * .50f;
                PrefabUtility.RecordPrefabInstancePropertyModifications(targets.BoardDialogue);
            }
            Bind(board, targets.BoardDialogue, StoryInspection.Kind.Board, visual, "Forest_Breath");
        }

        static bool TrySurface(TWMysticContext terrain, MeshCollider floor, Vector3 position, out Vector3 ground)
        {
            if (!terrain.TryGround(terrain.Local(position), out ground)) return false;
            if (floor != null && floor.Raycast(new Ray(ground + Vector3.up * .8f, Vector3.down), out var hit, 1.6f))
                ground = hit.point;
            return true;
        }

        static void BakeCedarGround(TWMysticContext terrain, Transform cedar, MeshCollider floor, Mesh[] meshes,
            Material material)
        {
            var root = Child(cedar, CedarGround, Vector3.zero, Quaternion.identity);
            for (int i = 0; i < RootSites.Length; i++)
            {
                var site = RootSites[i];
                var visual = root.Find(site.name);
                if (visual == null)
                {
                    Vector3 candidate = cedar.TransformPoint(site.offset);
                    if (!TrySurface(terrain, floor, candidate, out var ground))
                    {
                        Debug.LogWarning("Круг молчания: нет поверхности у корня для «" + site.name + "».", cedar);
                        continue;
                    }
                    visual = Child(root, site.name, root.InverseTransformPoint(ground),
                        Quaternion.Euler(0f, site.yaw, 0f));
                }
                AttachArt(visual, meshes[i], material);
            }
        }

        static void RequireCompatibleBinding(Transform owner, MonoBehaviour source, StoryInspection.Kind kind)
        {
            if (source == null)
                throw new InvalidOperationException("Для осмотра отсутствует исходный компонент у " + owner.name);
            var existing = owner.GetComponent<StoryInspection>();
            if (existing != null && (existing.superseded || existing.kind != kind ||
                (existing.source != null && existing.source != source)))
                throw new InvalidOperationException("Другая привязка StoryInspection у " + owner.name);
        }

        static void Bind(Transform owner, MonoBehaviour source, StoryInspection.Kind kind, Transform detail, string ambience)
        {
            var inspection = owner.GetComponent<StoryInspection>();
            if (inspection == null)
            {
                inspection = Undo.AddComponent<StoryInspection>(owner.gameObject);
                Undo.RecordObject(inspection, UndoName);
                inspection.kind = kind;
                inspection.detailOffset = owner.InverseTransformPoint(detail.position);
                inspection.atmosphere = AssetDatabase.LoadAssetAtPath<AudioClip>(Audio + ambience + ".wav");
            }
            Undo.RecordObject(inspection, UndoName);
            inspection.source = source;
            inspection.detail = detail;
            PrefabUtility.RecordPrefabInstancePropertyModifications(inspection);
        }
    }
}
