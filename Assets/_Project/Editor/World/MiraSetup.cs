#if UNITY_EDITOR
// Assets/_Project/Editor/World/MiraSetup.cs
// Меню: Tools > BuilderSim > Настроить старт игры
// Одним нажатием доводит сцену до рабочего состояния для Миры и вступления:
//   игрок: PlayerStats, InteractionHub, Speaker «Лео»;
//   Мира: Speaker, DialogueSequence (по E), коллайдер (торговля теперь у отдельного NPC-торговца, TradingStall Мире не добавляется);
//   сцена: IntroFlow.
// Ничего не удаляет и не трогает то, что уже настроено. Откат: Ctrl+Z.
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MiraSetup
{
    [MenuItem("Tools/BuilderSim/Настроить старт игры")]
    static void Run()
    {
        var sb = new StringBuilder("=== Настроить старт игры ===\n");
        int changes = 0;

        // ---------- игрок
        var pm = Object.FindFirstObjectByType<PlayerMovement>();
        if (pm == null)
        {
            EditorUtility.DisplayDialog("Старт игры", "В сцене не найден игрок (PlayerMovement).", "OK");
            return;
        }
        var player = pm.gameObject;
        changes += Ensure<PlayerStats>(player, sb);
        changes += Ensure<InteractionHub>(player, sb);

        var leo = player.GetComponent<Speaker>();
        if (leo == null)
        {
            leo = Undo.AddComponent<Speaker>(player);
            leo.displayName = "Лео";
            leo.nameColor = new Color32(0x6B, 0xCB, 0x5A, 255);
            leo.bubbleOffset = new Vector3(0f, 2.6f, 0f);
            sb.AppendLine("+ Speaker «Лео» на игроке");
            changes++;
        }

        // ---------- Мира
        Speaker mira = null;
        foreach (var s in Object.FindObjectsByType<Speaker>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (s.displayName == "Мира") { mira = s; break; }

        if (mira == null)
        {
            var go = GameObject.Find("NPC_Мира");
            if (go == null) go = GameObject.Find("Мира");
            if (go != null)
            {
                mira = Undo.AddComponent<Speaker>(go);
                mira.displayName = "Мира";
                mira.nameColor = new Color32(0xF6, 0xA1, 0x5A, 255);
                mira.faceNearbyPlayer = true;
                sb.AppendLine("+ Speaker «Мира» на " + go.name);
                changes++;
            }
        }

        if (mira == null)
        {
            sb.AppendLine("! Миры в сцене нет. Создайте её: Tools > BuilderSim > NPC Builder (пресет Mira), потом запустите это меню снова.");
        }
        else
        {
            var mg = mira.gameObject;

            var seq = mg.GetComponent<DialogueSequence>();
            if (seq == null)
            {
                seq = Undo.AddComponent<DialogueSequence>(mg);
                sb.AppendLine("+ DialogueSequence на Мире");
                changes++;
            }
            if (seq.trigger != DialogueSequence.Trigger.OnInteract || !seq.once)
            {
                Undo.RecordObject(seq, "Mira dialogue");
                seq.trigger = DialogueSequence.Trigger.OnInteract;
                seq.once = true;
                seq.radius = Mathf.Max(seq.radius, 3f);
                sb.AppendLine("~ Разговор Миры теперь по E");
                changes++;
            }

            if (mg.GetComponentInChildren<Collider>() == null)
            {
                var col = Undo.AddComponent<CapsuleCollider>(mg);
                col.height = 1.8f;
                col.center = new Vector3(0f, 0.9f, 0f);
                col.radius = 0.4f;
                sb.AppendLine("+ CapsuleCollider на Мире (игрок не проходит сквозь неё)");
                changes++;
            }

        }

        // ---------- вступление
        if (Object.FindFirstObjectByType<IntroFlow>() == null)
        {
            var go = new GameObject("IntroFlow");
            Undo.RegisterCreatedObjectUndo(go, "IntroFlow");
            go.AddComponent<IntroFlow>();
            sb.AppendLine("+ IntroFlow (ходьба, Мира, рубка, костёр, возврат к Мире)");
            changes++;
        }
        if (GameObject.Find("CAMPFIRE_Point") == null)
            sb.AppendLine("! Нет объекта CAMPFIRE_Point: стрелка к костру не появится.");

        if (changes > 0) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        sb.AppendLine(changes == 0 ? "Менять нечего, всё уже на месте." : "Изменений: " + changes + ". Сохраните сцену (Ctrl+S).");

        Debug.Log(sb.ToString());
        EditorUtility.DisplayDialog("Старт игры", sb.ToString(), "OK");
    }

    static int Ensure<T>(GameObject go, StringBuilder sb) where T : Component
    {
        if (go.GetComponent<T>() != null) return 0;
        Undo.AddComponent<T>(go);
        sb.AppendLine("+ " + typeof(T).Name + " на " + go.name);
        return 1;
    }
}
#endif
