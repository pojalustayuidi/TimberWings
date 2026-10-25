#if UNITY_EDITOR
// Assets/_Project/Editor/World/TreeAndPlayerFix.cs
// Меню: Tools > BuilderSim > Починить деревья и игрока
// Что делает в открытой сцене:
//   1) У каждого дерева с тегом Resource убирает слетевшие скрипты (Missing) и вешает ResourceItem, если его нет.
//      Модель дерева ResourceItem сам сгенерирует при запуске (TreeMeshFactory), префабы не нужны.
//   2) У игрока добавляет PlayerInventory, PlayerInteraction (вместе с BackpackTag) и ChopProgress, если их нет.
//      Слетевшие скрипты на игроке НЕ удаляет.
// Ничего другого не трогает. После запуска сохрани сцену (Ctrl+S), в Console будет строка с итогом.
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TreeAndPlayerFix
{
    [MenuItem("Tools/BuilderSim/Мир и персонаж/Починить деревья и игрока", false, 411)]
    static void Run()
    {
        Scene scene = SceneManager.GetActiveScene();
        int treesFixed = 0, missingRemoved = 0;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                GameObject go = all[i].gameObject;
                if (!go.CompareTag("Resource")) continue;
                if (go.GetComponentInParent<ResourceItem>() != null) continue;   // у дерева (или его родителя) уже есть

                missingRemoved += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                Undo.AddComponent<ResourceItem>(go);
                treesFixed++;
            }
        }

        string playerNote = "игрок не найден (нет PlayerStats)";
        PlayerStats stats = Object.FindFirstObjectByType<PlayerStats>();
        if (stats != null)
        {
            GameObject p = stats.gameObject;
            string added = "";
            if (p.GetComponent<PlayerInventory>() == null) { Undo.AddComponent<PlayerInventory>(p); added += " PlayerInventory"; }
            if (p.GetComponent<PlayerInteraction>() == null) { Undo.AddComponent<PlayerInteraction>(p); added += " PlayerInteraction"; }
            if (p.GetComponent<ChopProgress>() == null) { Undo.AddComponent<ChopProgress>(p); added += " ChopProgress"; }
            playerNote = added.Length > 0 ? "добавлено на «" + p.name + "»:" + added : "у «" + p.name + "» всё уже есть";
        }

        if (treesFixed > 0 || playerNote.StartsWith("добавлено")) EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[TreeAndPlayerFix] деревьев починено: " + treesFixed + " (слетевших скриптов убрано: " + missingRemoved + "); " + playerNote + ". Сохрани сцену (Ctrl+S).");
    }
}
#endif
