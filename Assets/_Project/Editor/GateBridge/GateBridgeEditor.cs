#if UNITY_EDITOR
// Assets/_Project/Editor/GateBridge/GateBridgeEditor.cs (Editor)
// Инспектор GateBridge: сборка и просмотр видов. Просмотр не трогает данные игры (только включает объекты в сцене).
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[CustomEditor(typeof(GateBridge))]
public class GateBridgeEditor : Editor
{
    [MenuItem("Tools/BuilderSim/Локация/Назначить сцену с мостом игровой", false, 106)]
    static void SetGameplayScene()
    {
        var scene = SceneManager.GetActiveScene();
        var bridge = Object.FindFirstObjectByType<GateBridge>();
        if (string.IsNullOrEmpty(scene.path) || bridge == null || bridge.gameObject.scene != scene || !bridge.HasFinalIsland)
        {
            EditorUtility.DisplayDialog("TimberWings", "Открой сохранённую игровую сцену с подвесным мостом и дальним островом.", "OK");
            return;
        }

        const string menu = "Assets/_Project/Scenes/MainMenu.unity";
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(menu) == null)
        {
            EditorUtility.DisplayDialog("TimberWings", "Сцена MainMenu отсутствует в проекте.", "OK");
            return;
        }

        string previousGame = SceneUtility.GetScenePathByBuildIndex(MenuFlow.GameScene);
        var scenes = new List<EditorBuildSettingsScene>
        {
            new EditorBuildSettingsScene(menu, true),
            new EditorBuildSettingsScene(scene.path, true)
        };
        foreach (var entry in EditorBuildSettings.scenes)
        {
            if (entry.path == menu || entry.path == scene.path) continue;
            scenes.Add(entry.path == previousGame ? new EditorBuildSettingsScene(entry.path, false) : entry);
        }
        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log("TimberWings: главное меню открывает " + scene.path);
    }

    [MenuItem("Tools/BuilderSim/Локация/Назначить сцену с мостом игровой", true)]
    static bool CanSetGameplayScene() => !EditorApplication.isPlayingOrWillChangePlaymode;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var gb = (GateBridge)target;
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Editor Bake: геометрия, коллайдеры, свет, портал и падающие доски сохраняются в сцене. Пересоздаётся только " + GateBridgeBuilder.OwnedRootName + "; пользовательские объекты не удаляются.", MessageType.Info);
        if (!gb.HasBakedFinale)
            EditorGUILayout.HelpBox("Финал ещё не запечён. Выполни сборку в Edit Mode и сохрани сцену.", MessageType.Warning);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button("Собрать мост и остров (Editor Bake)", GUILayout.Height(30)))
            {
                string report = GateBridgeBuilder.Build(gb);
                Debug.Log(report, gb);
            }
        }
        EditorGUILayout.LabelField("Просмотр (вид / сдано этапов)");
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("1: сломан · 0")) gb.Preview(1, 0, false);
        if (GUILayout.Button("2: опоры · 1")) gb.Preview(2, 1, false);
        if (GUILayout.Button("2: настил · 2")) gb.Preview(2, 2, false);
        if (GUILayout.Button("3: готов · 3")) gb.Preview(3, 3, true);
        EditorGUILayout.EndHorizontal();
        if (GUILayout.Button("Проверить мост и финальный остров")) GateBridgeCheck.Run();
        if (GUILayout.Button("Проверить Editor Bake"))
        {
            string report = GateBridgeBakeCheck.Validate(gb);
            Debug.Log(report, gb);
            EditorUtility.DisplayDialog("Editor Bake GateBridge", report, "OK");
        }
        if (GUILayout.Button("Убрать GB_Final и вернуть старые объекты (BuildZone остаётся)")) GateBridgeBuilder.RemoveGenerated(gb);
    }
}
#endif
