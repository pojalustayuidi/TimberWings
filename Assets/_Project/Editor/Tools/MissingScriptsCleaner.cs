#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Repairs only the active saved scene. Never modifies prefab assets or other open scenes.
public static class MissingScriptsCleaner
{
    const string MenuPath = "Tools/BuilderSim/Отладка и тесты/Убрать слетевшие скрипты (Missing)";

    [MenuItem(MenuPath, false, 1001)]
    static void Run()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(scene.path) ||
            EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            EditorUtility.DisplayDialog("Слетевшие скрипты",
                "Открой сохранённую игровую сцену в Edit Mode (не Prefab Mode).", "OK");
            return;
        }

        var missingObjects = new List<GameObject>();
        var buildingZones = new List<BuildingController>();
        int missing = 0;
        int skippedPrefabObjects = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                GameObject go = transform.gameObject;
                int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                if (count <= 0) continue;

                // Removing missing components from prefab instances can create fragile
                // overrides. Fix those in the source prefab, outside this scene utility.
                if (PrefabUtility.IsPartOfPrefabInstance(go))
                {
                    skippedPrefabObjects++;
                    continue;
                }
                missingObjects.Add(go);
                missing += count;
            }
            buildingZones.AddRange(root.GetComponentsInChildren<BuildingController>(true));
        }

        var needHighlighter = new List<BuildingController>();
        foreach (BuildingController zone in buildingZones)
        {
            if (zone != null && zone.GetComponent<Collider>() != null &&
                zone.GetComponent<BuildSiteHighlighter>() == null)
                needHighlighter.Add(zone);
        }

        if (missing == 0 && needHighlighter.Count == 0)
        {
            EditorUtility.DisplayDialog("Слетевшие скрипты",
                "В сцене нет доступных для очистки Missing-компонентов и пропущенных подсветок." +
                (skippedPrefabObjects > 0 ? "\nПропущено prefab-объектов с Missing: " + skippedPrefabObjects + "." : ""), "OK");
            return;
        }

        string prompt = "Сцена: " + scene.name + "\nУдалить Missing-компонентов: " + missing +
            "\nДобавить BuildSiteHighlighter: " + needHighlighter.Count +
            (skippedPrefabObjects > 0 ? "\nПропустить prefab-объектов с Missing: " + skippedPrefabObjects : "") +
            "\n\nИзменения затронут только эту сцену. Продолжить?";
        if (!EditorUtility.DisplayDialog("Очистка сцены", prompt, "Применить", "Отмена")) return;

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Remove missing scripts and restore build highlights");

        int removed = 0;
        var report = new StringBuilder("=== Очистка сцены ").Append(scene.name).AppendLine(" ===");
        foreach (GameObject go in missingObjects)
        {
            if (go == null) continue;
            Undo.RegisterFullObjectHierarchyUndo(go, "Remove missing scripts");
            int count = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            removed += count;
            report.AppendLine("Missing: " + go.name + " (" + count + ")");
        }
        int added = 0;
        foreach (BuildingController zone in needHighlighter)
        {
            if (zone == null || zone.GetComponent<BuildSiteHighlighter>() != null) continue;
            Undo.AddComponent<BuildSiteHighlighter>(zone.gameObject);
            added++;
        }
        Undo.CollapseUndoOperations(undoGroup);
        if (removed > 0 || added > 0) EditorSceneManager.MarkSceneDirty(scene);

        report.AppendLine("Итог: удалено " + removed + ", добавлено подсветок " + added +
            ", пропущено prefab-объектов " + skippedPrefabObjects + ".");
        Debug.Log(report.ToString());
        EditorUtility.DisplayDialog("Очистка сцены", "Удалено Missing: " + removed +
            "\nДобавлено подсветок: " + added +
            "\nPrefab-объекты пропущены: " + skippedPrefabObjects +
            "\nПроверь результат и сохрани сцену вручную.", "OK");
    }

    [MenuItem(MenuPath, true)]
    static bool ValidateRun() => !EditorApplication.isPlayingOrWillChangePlaymode;
}
#endif
