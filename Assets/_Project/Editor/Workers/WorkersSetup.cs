#if UNITY_EDITOR
// Assets/_Project/Editor/World/WorkersSetup.cs
// Меню: Tools > BuilderSim > Настроить рабочих (WorkerManager + лагерь)
// Делает на открытой сцене ровно два дела и ничего больше:
//   1) создаёт объект «WorkerManager» с компонентом WorkerManager, если на сцене его ещё нет;
//   2) вешает WorkerCamp на объект палатки (TentStages, а если его нет, на StoryFlow.tent), если там его ещё нет.
// Повторный запуск безопасен: найденное не дублируется. Всё идёт через Undo (Ctrl+Z), после запуска сохрани сцену (Ctrl+S).
// Окно «Рабочие» на сцену класть не нужно: оно строится из кода при первом E у лагеря.
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class WorkersSetup
{
    [MenuItem("Tools/BuilderSim/Настроить рабочих (WorkerManager + лагерь)")]
    static void Run()
    {
        var sb = new StringBuilder();
        bool changed = false;

        // ---------- менеджер
        var managers = Object.FindObjectsByType<WorkerManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (managers.Length == 0)
        {
            var go = new GameObject("WorkerManager");
            Undo.RegisterCreatedObjectUndo(go, "Создать WorkerManager");
            go.AddComponent<WorkerManager>();
            changed = true;
            sb.AppendLine("FIX  Создан объект «WorkerManager» (компонент WorkerManager).");
        }
        else
        {
            sb.AppendLine("OK   WorkerManager уже есть: " + Path(managers[0].transform));
            if (managers.Length > 1) sb.AppendLine("WARN WorkerManager на сцене " + managers.Length + ", оставьте один.");
        }

        // ---------- база рабочих в поле database менеджера
        var mgr = Object.FindFirstObjectByType<WorkerManager>(FindObjectsInactive.Include);
        if (mgr != null && mgr.database == null)
        {
            var db = WorkerDatabaseTools.FindDatabase();
            if (db != null)
            {
                Undo.RecordObject(mgr, "Назначить базу рабочих");
                mgr.database = db;
                EditorUtility.SetDirty(mgr);
                changed = true;
                sb.AppendLine("FIX  WorkerManager.database = " + db.name + ".");
            }
            else sb.AppendLine("ERR  Нет ассета WorkerDatabase. Tools > BuilderSim > Рабочие > Создать базу рабочих, затем запустите это меню ещё раз.");
        }
        else if (mgr != null) sb.AppendLine("OK   WorkerManager.database назначена: " + mgr.database.name);

        // ---------- лагерь: на объект палатки
        GameObject tent = null;
        var stages = Object.FindFirstObjectByType<TentStages>(FindObjectsInactive.Include);
        if (stages != null) tent = stages.gameObject;
        if (tent == null)
        {
            var story = Object.FindFirstObjectByType<StoryFlow>(FindObjectsInactive.Include);
            if (story != null && story.tent != null) tent = story.tent.gameObject;
        }

        var camps = Object.FindObjectsByType<WorkerCamp>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (camps.Length > 0)
        {
            sb.AppendLine("OK   WorkerCamp уже есть: " + Path(camps[0].transform));
            if (camps.Length > 1) sb.AppendLine("WARN WorkerCamp на сцене " + camps.Length + ", оставьте один.");
        }
        else if (tent == null)
        {
            sb.AppendLine("ERR  Палатка не найдена (нет TentStages и StoryFlow.tent). Соберите палатку: Tools > BuilderSim > Tent Builder, затем запустите это меню ещё раз.");
        }
        else
        {
            Undo.AddComponent<WorkerCamp>(tent);
            changed = true;
            sb.AppendLine("FIX  WorkerCamp добавлен на «" + Path(tent.transform) + "». Радиус и текст подсказки меняются в Inspector.");
        }

        if (changed) EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        string report = sb.ToString();
        Debug.Log("=== Настроить рабочих ===\n" + report + (changed ? "Сохраните сцену (Ctrl+S)." : ""));
        EditorUtility.DisplayDialog("Рабочие", report + (changed ? "\nСохраните сцену (Ctrl+S)." : "\nНичего менять не пришлось."), "OK");
    }

    static string Path(Transform t)
    {
        string p = t.name;
        for (var q = t.parent; q != null; q = q.parent) p = q.name + "/" + p;
        return p;
    }
}
#endif
