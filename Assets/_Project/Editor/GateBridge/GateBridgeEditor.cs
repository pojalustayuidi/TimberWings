#if UNITY_EDITOR
// Assets/_Project/Editor/GateBridge/GateBridgeEditor.cs (Editor)
// Инспектор GateBridge: сборка и просмотр видов. Просмотр не трогает данные игры (только включает объекты в сцене).
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GateBridge))]
public class GateBridgeEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var gb = (GateBridge)target;
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Сборка пересоздаёт только корень " + GateBridgeBuilder.OwnedRootName + ". Старые объекты прежней сборки выключаются, не удаляются.", MessageType.Info);
        if (GUILayout.Button("Собрать мост и остров", GUILayout.Height(30)))
        {
            string report = GateBridgeBuilder.Build(gb);
            Debug.Log(report, gb);
        }
        EditorGUILayout.LabelField("Просмотр (вид / сдано этапов)");
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("1: сломан · 0")) gb.Preview(1, 0, false);
        if (GUILayout.Button("2: опоры · 1")) gb.Preview(2, 1, false);
        if (GUILayout.Button("2: настил · 2")) gb.Preview(2, 2, false);
        if (GUILayout.Button("3: готов · 3")) gb.Preview(3, 3, true);
        EditorGUILayout.EndHorizontal();
        if (GUILayout.Button("Проверить мост и финальный остров")) GateBridgeCheck.Run();
        if (GUILayout.Button("Убрать сгенерированное и вернуть старые объекты")) GateBridgeBuilder.RemoveGenerated(gb);
    }
}
#endif
