// Assets/_Project/Editor/IslandV2/IslandBaseline.cs
// Задача A1: прогнать IslandCheck и сохранить отчёт как базу (файл в проекте, его видно в git и можно сравнивать после A3-A11).
// Ничего не меняет в сцене и раскладке: вызывает пункт «3. Проверить блокаут» и забирает его отчёт из буфера обмена.
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace IslandV2
{
    public static class IslandBaseline
    {
        const string CheckMenu = "Tools/BuilderSim/Локация/Остров v2/3. Проверить блокаут (контракт и проходимость)";
        const string RelPath = "_Project/Docs/IslandCheck_baseline.txt";

        [MenuItem("Tools/BuilderSim/Локация/Остров v2/3a. Проверить и сохранить отчёт как базу (A1)", false, 122)]
        static void Run()
        {
            if (Application.isPlaying) { Debug.LogWarning("IslandBaseline: выйди из Play Mode, проверка идёт в редакторе."); return; }

            GUIUtility.systemCopyBuffer = "";
            if (!EditorApplication.ExecuteMenuItem(CheckMenu))
            { Debug.LogError("IslandBaseline: не нашёл пункт меню «3. Проверить блокаут». Имя пункта изменилось?"); return; }

            string report = GUIUtility.systemCopyBuffer;
            if (string.IsNullOrEmpty(report) || !report.StartsWith("IslandV2: проверка блокаута"))
            { Debug.LogError("IslandBaseline: отчёт IslandCheck не получен (буфер обмена пуст или занят). Запусти пункт 3 вручную и повтори."); return; }

            string path = Path.Combine(Application.dataPath, RelPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string head = "База IslandCheck (A1)\n" +
                          "Дата: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "\n" +
                          "Unity: " + Application.unityVersion + ", сцена: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + "\n" +
                          "------------------------------------------------------------\n";
            File.WriteAllText(path, head + report);
            AssetDatabase.Refresh();
            Debug.Log("IslandBaseline: отчёт сохранён в Assets/" + RelPath + ". Строка «Итого» внизу файла: это точка отсчёта для A3-A11.");
        }
    }
}
