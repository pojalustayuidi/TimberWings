#if UNITY_EDITOR
// Assets/_Project/Editor/Island/Core/LocationModules.cs
// Запускает модули после основной сборки. Ошибка одного модуля не ломает остальные.
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class LocationModules
{
    public static void Run(LocationCtx c)
    {
        if (c == null || c.root == null) return;
        try
        {
            // папки и очистка — вне пакетного режима, чтобы они применились сразу
            LocKit.Reset();
            LocKit.BeginBatch();
            try
            {
                Step("Река", 0.95f, c.s.buildRiver, () => LocationRiver.Build(c));
                Step("Центр", 0.96f, c.s.buildCenter, () => LocationCenter.Build(c));
                Step("Поляна", 0.98f, c.s.buildMeadow, () => LocationMeadow.Build(c));
                Step("Доводка", 0.99f, c.s.polishEnabled, () => LocationPolish.Build(c));
            }
            finally
            {
                // импорт всех созданных ассетов одним проходом
                LocKit.EndBatch();
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    static void Step(string label, float progress, bool enabled, Action action)
    {
        if (!enabled) return;
        EditorUtility.DisplayProgressBar("Location Builder", label, progress);
        try { action(); }
        catch (Exception e) { Debug.LogError("[LocationBuilder] Модуль «" + label + "» упал:"); Debug.LogException(e); }
    }
}
#endif