// Assets/_Project/Editor/Intro/IntroArrivalSpawnEditor.cs
// Один маркер сцены: пользователь перетаскивает его к месту высадки у входа в лагерь.
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class IntroArrivalSpawnEditor
{
    [MenuItem("Tools/Timber Wings/Intro/Создать точку прибытия Лео")]
    static void CreateMarker()
    {
        var found = GameObject.Find(ArrivalIntroSequence.SpawnMarkerName);
        if (found != null) { Selection.activeGameObject = found; EditorGUIUtility.PingObject(found); return; }
        var go = new GameObject(ArrivalIntroSequence.SpawnMarkerName);
        Undo.RegisterCreatedObjectUndo(go, "Создать точку прибытия Лео");
        var leo = Object.FindFirstObjectByType<PlayerMovement>(FindObjectsInactive.Include);
        if (leo != null)
        {
            go.transform.position = leo.transform.position;
            go.transform.rotation = leo.model != null ? leo.model.rotation : leo.transform.rotation;
        }
        else if (SceneView.lastActiveSceneView != null)
            go.transform.position = SceneView.lastActiveSceneView.pivot;
        Selection.activeGameObject = go;
        EditorGUIUtility.PingObject(go);
        EditorSceneManager.MarkSceneDirty(go.scene);
        Debug.Log("Intro: передвинь IntroArrivalSpawn к входу в лагерь, поставь на землю и направь синюю ось Z (+Z) в сторону костра.");
    }
}
#endif
