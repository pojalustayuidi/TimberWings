// Assets/_Project/Editor/IslandV2/IslandCandySprinklePreset.cs
// ISL-013: обратимый Editor-пресет плотности мелкой посыпки в маске земли.
// Не затрагивает ForestUnderstory, сцену, шейдер и IslandLayout.
using UnityEditor;
using UnityEngine;

namespace IslandV2
{
    public static class IslandCandySprinklePreset
    {
        [MenuItem("Tools/BuilderSim/Локация/Остров v2/ISL-013/1. Группировать мелкую посыпку", false, 138)]
        static void Enable()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("ISL-013: сначала останови Play Mode.");
                return;
            }
            IslandCandy s = IslandCandy.GetOrCreate();
            if (!s.candyOn)
            {
                Debug.LogError("ISL-013: IslandCandy.candyOn выключен; пресет не применён.");
                return;
            }

            Undo.RecordObject(s, "ISL-013: группы мелкой посыпки");
            s.sprinkleClustering = 1f;
            s.sprinkleClusterSize = 9f;
            s.sprinkleCleanThreshold = 0.43f;
            s.sprinklePatchDensity = 0.75f;
            s.sprinklePathClearance = 0.70f;
            s.sprinkleSeed = 13013;
            EditorUtility.SetDirty(s);
            AssetDatabase.SaveAssets();
            Selection.activeObject = s;
            EditorGUIUtility.PingObject(s);
            Debug.Log("ISL-013: настройки группировки сохранены в IslandCandy.asset. " +
                      "Теперь выбери Candy-земля: перекрасить (без пересборки). " +
                      "Объекты подлеска, маршруты, материалы и шейдер не менялись.");
        }

        [MenuItem("Tools/BuilderSim/Локация/Остров v2/ISL-013/2. Отключить группировку посыпки", false, 139)]
        static void Disable()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("ISL-013: сначала останови Play Mode.");
                return;
            }
            IslandCandy s = IslandCandy.GetOrCreate();
            Undo.RecordObject(s, "ISL-013: отключить группировку посыпки");
            s.sprinkleClustering = 0f; // в CandyPixel — точная старая маска посыпки
            EditorUtility.SetDirty(s);
            AssetDatabase.SaveAssets();
            Selection.activeObject = s;
            Debug.Log("ISL-013: группировка отключена. Чтобы вернуть прежнюю посыпку, " +
                      "выполни Candy-земля: перекрасить (без пересборки).");
        }
    }
}
