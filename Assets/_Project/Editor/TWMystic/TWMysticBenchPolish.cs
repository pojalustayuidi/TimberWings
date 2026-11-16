using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace IslandV2
{
    internal static class TWMysticBenchPolish
    {
        const string Models = "Assets/_Project/Art/MysticBenches/";
        const string Generated = "Assets/_Project/IslandV2/MysticArt/Scenes/";
        const string Backboard = "Signature_Backboard";
        const string UndoLabel = "Update mystic forest benches";

        static readonly string[] BenchKeys = { "Bench_0", "Bench_1", "Bench_2", "Bench_3", "Bench_4", "Bench_5", "SecretBench" };

        internal static Mesh Load(string key)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Models + key + ".asset");
            if (mesh == null)
                throw new InvalidOperationException("Не найден меш " + Models + key + ".asset");
            if (!mesh.isReadable || mesh.subMeshCount != 1 ||
                !mesh.HasVertexAttribute(VertexAttribute.Normal) || !mesh.HasVertexAttribute(VertexAttribute.Color))
                throw new InvalidOperationException("Неверный формат меша скамьи " + key);
            return mesh;
        }

        [MenuItem("Tools/BuilderSim/Лес/MYSTIC FOREST/9. Обновить модели семи скамеек", false, 358)]
        static void UpdateBakedBenches()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("MYSTIC FOREST: выйди из Play Mode перед обновлением скамеек.");
                return;
            }

            try
            {
                var scene = SceneManager.GetActiveScene();
                var states = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<MysticArtState>(true))
                    .Where(state => state.art != null && state.art.scene == scene).ToArray();
                if (states.Length != 1)
                    throw new InvalidOperationException("Открой игровой уровень с одним уже собранным Кругом молчания.");

                var props = states[0].art.GetComponentsInChildren<MysticProp>(true);
                var benches = BenchKeys.Select(key =>
                {
                    var matches = props.Where(prop => prop.key == key).ToArray();
                    if (matches.Length != 1)
                        throw new InvalidOperationException($"Для {key} ожидается одна скамья, найдено {matches.Length}.");
                    return matches[0].transform;
                }).ToArray();

                var sources = BenchKeys.Append(Backboard).Select(Load).ToArray();
                foreach (var source in sources)
                    TWMysticValidation.Mesh(source);

                var targets = benches.Select((bench, i) => GetTarget(bench, BenchKeys[i]))
                    .Append(GetTarget(benches[benches.Length - 1].Find(Backboard), Backboard)).ToArray();
                if (targets.Distinct().Count() != targets.Length)
                    throw new InvalidOperationException("Несколько объектов ссылаются на один меш скамьи.");

                ReplaceMeshes(sources, targets);
                Debug.Log("MYSTIC FOREST: обновлены семь скамеек и спинка седьмой. Меши сохранены.");
            }
            catch (Exception error)
            {
                Debug.LogError("MYSTIC FOREST: не удалось обновить скамьи. " + error);
            }
        }

        static Mesh GetTarget(Transform target, string key)
        {
            if (target == null || !target.TryGetComponent<MeshFilter>(out var filter) ||
                !target.TryGetComponent<MeshRenderer>(out var renderer) || renderer.sharedMaterials.Length != 1 ||
                filter.sharedMesh == null)
                throw new InvalidOperationException("В собранной сцене нет модели " + key);

            var mesh = filter.sharedMesh;
            var path = AssetDatabase.GetAssetPath(mesh);
            if (!path.StartsWith(Generated, StringComparison.Ordinal) || Path.GetFileName(path) != key + ".asset" ||
                !AssetDatabase.IsMainAsset(mesh))
                throw new InvalidOperationException($"У {key} неожиданный путь меша: {path}.");

            return mesh;
        }

        static void ReplaceMeshes(Mesh[] sources, Mesh[] targets)
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(UndoLabel);
            Undo.RegisterCompleteObjectUndo(targets, UndoLabel);
            try
            {
                // Copy into the same asset so the scene keeps the existing GUID and local file ID.
                for (int i = 0; i < targets.Length; i++)
                {
                    string name = targets[i].name;
                    EditorUtility.CopySerialized(sources[i], targets[i]);
                    targets[i].name = name;
                    EditorUtility.SetDirty(targets[i]);
                }

                foreach (var target in targets)
                    AssetDatabase.SaveAssetIfDirty(target);
            }
            catch
            {
                Undo.RevertAllDownToGroup(group);
                foreach (var target in targets)
                {
                    EditorUtility.SetDirty(target);
                    AssetDatabase.SaveAssetIfDirty(target);
                }
                throw;
            }

            Undo.CollapseUndoOperations(group);
        }
    }
}
