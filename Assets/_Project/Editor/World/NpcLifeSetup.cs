#if UNITY_EDITOR
// Assets/_Project/Editor/World/NpcLifeSetup.cs
// Меню: Tools > BuilderSim > Мир и персонаж > Оживить NPC
// Вешает NpcLife и NpcBanter на выбранные объекты (или, если ничего не выбрано, на всех Speaker в сцене, кроме игрока)
// и добавляет в их Animator Controller параметры Talking / Wave / Cheer, если их нет.
// Сами анимации (болтает, машет, радуется) назначаются в Animator вручную: параметры уже ждут.
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class NpcLifeSetup
{
    [MenuItem("Tools/BuilderSim/Мир и персонаж/Оживить NPC", false, 403)]
    static void Run()
    {
        var targets = new List<GameObject>();
        foreach (var go in Selection.gameObjects) targets.Add(go);

        if (targets.Count == 0)
        {
            foreach (var s in Object.FindObjectsByType<Speaker>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (s.GetComponent<PlayerMovement>() == null) targets.Add(s.gameObject);
        }

        if (targets.Count == 0)
        {
            EditorUtility.DisplayDialog("Оживить NPC", "Выберите NPC в сцене (или добавьте Speaker).", "OK");
            return;
        }

        var sb = new StringBuilder("=== Оживить NPC ===\n");
        int changes = 0;

        foreach (var go in targets)
        {
            if (go.GetComponent<PlayerMovement>() != null) continue;

            if (go.GetComponent<NpcLife>() == null)
            {
                Undo.AddComponent<NpcLife>(go);
                sb.AppendLine("+ NpcLife на " + go.name);
                changes++;
            }

            if (go.GetComponent<NpcBanter>() == null)
            {
                Undo.AddComponent<NpcBanter>(go);
                sb.AppendLine("+ NpcBanter на " + go.name + " (болтовня, реакции, «поболтать» по E, эмодзи)");
                changes++;
            }

            var anim = go.GetComponentInChildren<Animator>(true);
            if (anim == null)
            {
                sb.AppendLine("  (у " + go.name + " нет Animator: работает только процедурное оживление)");
                continue;
            }

            var ctrl = anim.runtimeAnimatorController as AnimatorController;
            if (ctrl == null)
            {
                sb.AppendLine("  (у " + go.name + " в Animator нет контроллера: создайте через Tools > BuilderSim > Аниматор персонажа)");
                continue;
            }

            changes += EnsureParam(ctrl, "Talking", AnimatorControllerParameterType.Bool, sb);
            changes += EnsureParam(ctrl, "Wave", AnimatorControllerParameterType.Trigger, sb);
            changes += EnsureParam(ctrl, "Cheer", AnimatorControllerParameterType.Trigger, sb);
        }

        if (changes > 0)
        {
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
        sb.AppendLine(changes == 0 ? "Менять нечего." : "Изменений: " + changes + ". Сохраните сцену (Ctrl+S).");
        Debug.Log(sb.ToString());
    }

    static int EnsureParam(AnimatorController c, string name, AnimatorControllerParameterType type, StringBuilder sb)
    {
        foreach (var p in c.parameters)
            if (p.name == name) return 0;
        c.AddParameter(name, type);
        sb.AppendLine("+ параметр " + name + " (" + type + ") в " + c.name);
        return 1;
    }
}
#endif
