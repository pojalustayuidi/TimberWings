#if UNITY_EDITOR
// Assets/_Project/Editor/Tools/MissingScriptsCleaner.cs
// Меню: Tools > BuilderSim > Убрать слетевшие скрипты (Missing)
// «Слетевший» скрипт = пустой компонент «Missing (Mono Script)» на объекте сцены. Он ничего не делает, только
// ругается в Console при запуске. Возникает, когда файл скрипта удалили и создали заново (у него новый GUID),
// переименовали класс, или у файла в имени «(1)» (имя файла MonoBehaviour должно совпадать с именем класса).
// Что делает меню (всё откатывается Ctrl+Z):
//   1) убирает все пустые компоненты со всех объектов открытой сцены (с настоящими компонентами ничего не происходит);
//   2) на каждую рабочую зону сдачи (есть триггер-коллайдер) возвращает BuildSiteHighlighter (кольцо и луч), если его нет.
// Игрок, Мира, IntroFlow, StoryFlow проверяются в Setup Doctor: если там OK, ничего важного не потеряно.
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MissingScriptsCleaner
{
    [MenuItem("Tools/BuilderSim/Убрать слетевшие скрипты (Missing)")]
    static void Run()
    {
        var targets = new System.Collections.Generic.List<GameObject>();
        int total = 0;
        foreach (var tr in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var go = tr.gameObject;
            if (!go.scene.IsValid() || EditorUtility.IsPersistent(go)) continue;
            int n = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
            if (n > 0) { targets.Add(go); total += n; }
        }

        if (total == 0 && !NeedHighlighters())
        {
            EditorUtility.DisplayDialog("Слетевшие скрипты", "Пустых компонентов (Missing) на сцене нет, зоны сдачи в порядке.", "OK");
            return;
        }

        if (total > 0 && !EditorUtility.DisplayDialog("Слетевшие скрипты",
                "Найдено пустых компонентов: " + total + " на объектах: " + targets.Count + ".\nУбрать их? (Откат: Ctrl+Z)", "Убрать", "Отмена"))
            return;

        var sb = new StringBuilder("=== Слетевшие скрипты ===\n");
        int removed = 0;
        foreach (var go in targets)
        {
            Undo.RegisterCompleteObjectUndo(go, "Убрать слетевшие скрипты");
            int r = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            removed += r;
            sb.AppendLine("— «" + go.name + "»: убрано " + r + (MissingCount(go) > 0 ? " (часть осталась: объект из префаба, чисти сам префаб)" : ""));
        }

        int added = 0;
        foreach (var bc in Object.FindObjectsByType<BuildingController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (bc.GetComponent<Collider>() == null || bc.GetComponent<BuildSiteHighlighter>() != null) continue;
            Undo.AddComponent<BuildSiteHighlighter>(bc.gameObject);
            added++;
            sb.AppendLine("+ BuildSiteHighlighter на «" + bc.name + "» (" + (bc.transform.parent != null ? bc.transform.parent.name : "-") + ")");
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        sb.AppendLine("Итого: убрано пустых компонентов " + removed + ", возвращено лучей " + added + ". Сохраните сцену (Ctrl+S).");
        Debug.Log(sb.ToString());
        EditorUtility.DisplayDialog("Слетевшие скрипты", sb.ToString(), "OK");
    }

    static int MissingCount(GameObject go) { return GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go); }

    static bool NeedHighlighters()
    {
        foreach (var bc in Object.FindObjectsByType<BuildingController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (bc.GetComponent<Collider>() != null && bc.GetComponent<BuildSiteHighlighter>() == null) return true;
        return false;
    }
}
#endif
