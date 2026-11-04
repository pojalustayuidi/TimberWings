// ISL-015: безопасная корректировка вертикального масштаба существующей палатки.
// Файл класть в Assets/_Project/Editor/IslandV2/ (только Unity Editor).
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ISL015TentVisualScale
{
    const float HeightFactor = 1.32f;
    const string RootName = "CampTent";

    [MenuItem("Tools/BuilderSim/Локация/Остров v2/ISL-015/1. Палатка: поднять визуально (без пересборки)")]
    static void Apply() { SetTentHeight(HeightFactor); }

    [MenuItem("Tools/BuilderSim/Локация/Остров v2/ISL-015/2. Палатка: вернуть прежнюю высоту")]
    static void Restore() { SetTentHeight(1f); }

    static void SetTentHeight(float target)
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("ISL-015: выйди из Play Mode перед изменением масштаба.");
            return;
        }
        GameObject tent = null;
        foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
            if (go.name == RootName) { tent = go; break; }
        if (tent == null)
        {
            Debug.LogError("ISL-015: CampTent не найден в активной сцене.");
            return;
        }
        var stages = tent.GetComponent<TentStages>();
        if (stages == null || stages.stage1 == null || stages.stage2 == null)
        {
            Debug.LogError("ISL-015: отсутствуют TentStages / Stage1 / Stage2. Изменения отменены.");
            return;
        }
        if (stages.stage1.transform.parent != tent.transform || stages.stage2.transform.parent != tent.transform)
        {
            Debug.LogError("ISL-015: иерархия стадий отличается от штатной. Изменения отменены.");
            return;
        }
        Transform[] parts = { stages.stage1.transform, stages.stage2.transform };
        foreach (var t in parts)
        {
            float current = t.localScale.y;
            if (Mathf.Abs(current - 1f) > 0.02f && Mathf.Abs(current - HeightFactor) > 0.02f)
            {
                Debug.LogError("ISL-015: у " + t.name + " нестандартный Scale.Y=" + current + ". Автозамена отменена.");
                return;
            }
        }
        foreach (var t in parts)
        {
            Undo.RecordObject(t, "ISL-015: высота палатки");
            var s = t.localScale;
            s.y = target;
            t.localScale = s;
            EditorUtility.SetDirty(t);
        }
        EditorSceneManager.MarkSceneDirty(tent.scene);
        Debug.Log("[ISL-015] Палатка: Scale.Y стадий = " + target + "; корень CampTent, XZ, позиция и компоненты сохранены. Перепроверь вход и коллайдеры.");
    }
}
