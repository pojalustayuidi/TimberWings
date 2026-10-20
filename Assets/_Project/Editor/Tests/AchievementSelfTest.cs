// Assets/_Project/Editor/Tests/AchievementSelfTest.cs (папка Editor)
// Tools > BuilderSim > Достижения > Проверить таблицу и бюджет наград
// Проверяет: уникальные id, пороги > 0, у глав есть chapterId, бюджет наград первых 30 минут (см. AchievementDefs.BudgetReport).
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class AchievementSelfTest
{
    [MenuItem("Tools/BuilderSim/Достижения/Проверить таблицу и бюджет наград")]
    public static void Run()
    {
        var ids = new HashSet<string>();
        int errors = 0;
        foreach (var d in AchievementDefs.All)
        {
            if (!ids.Add(d.id)) { Debug.LogError("Достижения: повторяется id «" + d.id + "»"); errors++; }
            if (d.goal <= 0) { Debug.LogError("Достижения: «" + d.id + "» порог должен быть больше 0"); errors++; }
            if (d.metric == AchMetric.Chapter && string.IsNullOrEmpty(d.chapterId)) { Debug.LogError("Достижения: «" + d.id + "» глава без chapterId"); errors++; }
            if (d.coins < 0 || d.xp < 0) { Debug.LogError("Достижения: «" + d.id + "» отрицательная награда"); errors++; }
        }
        bool ok;
        string report = AchievementDefs.BudgetReport(out ok);
        if (!ok) errors++;
        if (errors == 0) Debug.Log("Достижения: проверка пройдена.\n" + report);
        else Debug.LogWarning("Достижения: найдено проблем: " + errors + ".\n" + report);
    }
}
