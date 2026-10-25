// Assets/_Project/Editor/Story/EndingTools.cs (ТОЛЬКО в папке Editor)
// Проверка финальной заставки без постройки моста: Tools > BuilderSim > Показать финал (Play Mode).
using UnityEditor;
using UnityEngine;

public static class EndingTools
{
    [MenuItem("Tools/BuilderSim/Сюжет/Показать финал %#f", false, 720)]
    static void ShowEnding()
    {
        if (!Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Финал", "Сначала запусти игру (Play Mode).", "OK");
            return;
        }
        EndingSequence.Play(() => Debug.Log("EndingSequence: заставка закончена, игрок вернулся в игру."));
    }
}
