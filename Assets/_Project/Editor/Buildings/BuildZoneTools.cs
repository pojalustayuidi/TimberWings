#if UNITY_EDITOR
// Положи в Assets/_Project/Editor/BuildZoneTool.cs
// Меню: Tools -> BuilderSim -> Создать зону сдачи
// Создаёт невидимый блок-триггер со стройкой (метку BuildSiteTagView добавляет сам BuildingController) для любой будущей постройки.
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BuildZoneTool
{
    [MenuItem("Tools/BuilderSim/Создать зону сдачи")]
    static void Create()
    {
        Vector3 pos = Vector3.zero;
        var sv = SceneView.lastActiveSceneView;
        if (sv != null) pos = sv.pivot;

        var zg = new GameObject("BuildZone");
        Undo.RegisterCreatedObjectUndo(zg, "Создать зону сдачи");
        zg.transform.position = pos;

        var col = zg.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(4f, 3f, 4f);
        col.center = new Vector3(0f, 1.5f, 0f);

        var bc = zg.AddComponent<BuildingController>();
        bc.woodRequired = 4;
        bc.useRing = true;
        bc.ringOffset = new Vector3(0f, 3f, 0f);
        bc.completesTutorial = false;

        Selection.activeGameObject = zg;
        EditorSceneManager.MarkSceneDirty(zg.scene);
    }
}
#endif