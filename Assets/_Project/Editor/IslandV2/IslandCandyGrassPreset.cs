// Assets/_Project/Editor/IslandV2/IslandCandyGrassPreset.cs
// ISL-007: пресет ТОЛЬКО визуальных параметров травы и лесной подстилки.
// Работает поверх ISL-006: цвета/ширина троп, IslandLayout, меши и шейдер НЕ изменяются.
using UnityEditor;
using UnityEngine;

namespace IslandV2
{
    public static class IslandCandyGrassPreset
    {
        [MenuItem("Tools/BuilderSim/Локация/Остров v2/Candy-земля: ISL-007 пресет травы", false, 136)]
        static void Apply()
        {
            if (Application.isPlaying)
            {
                Debug.LogError("ISL-007: сначала выйди из Play Mode.");
                return;
            }

            IslandCandy s = IslandCandy.GetOrCreate();
            if (s == null || !s.candyOn)
            {
                Debug.LogError("ISL-007: Candy-земля выключена или ассет не найден. Пресет не применён.");
                return;
            }

            Color mint, green, lime, sun, forest, cool;
            if (!ColorUtility.TryParseHtmlString("#70CBA0", out mint) ||
                !ColorUtility.TryParseHtmlString("#83D77D", out green) ||
                !ColorUtility.TryParseHtmlString("#A9E17B", out lime) ||
                !ColorUtility.TryParseHtmlString("#CCE990", out sun) ||
                !ColorUtility.TryParseHtmlString("#5BB68B", out forest) ||
                !ColorUtility.TryParseHtmlString("#70BE95", out cool))
            {
                Debug.LogError("ISL-007: ошибка чтения цветов. Ассет не изменён.");
                return;
            }

            Undo.RecordObject(s, "ISL-007: палитра травы Cloudy Candy");
            s.grassMint = mint;
            s.grassGreen = green;
            s.grassLime = lime;
            s.grassSun = sun;
            s.grassScale = 25f;
            s.grassContrast = 1.55f;
            s.grassGrain = 0.025f;
            s.grassVariation = 0.82f;
            s.grassCoolShade = cool;
            s.grassCoolShadeStrength = 0.08f;
            s.grassCoolShadeScale = 11f;
            s.forestFloor = forest;
            s.forestStrength = 0.58f;
            s.forestDappleStrength = 0.10f;

            EditorUtility.SetDirty(s);
            AssetDatabase.SaveAssets();
            Selection.activeObject = s;
            EditorGUIUtility.PingObject(s);
            Debug.Log("ISL-007: палитра травы и лесной подстилки обновлена в " + IslandCandy.AssetPath +
                      ". Тропинки ISL-006, IslandLayout и материал не затронуты. " +
                      "Теперь запусти Candy-земля: перекрасить (без пересборки).");
        }
    }
}
