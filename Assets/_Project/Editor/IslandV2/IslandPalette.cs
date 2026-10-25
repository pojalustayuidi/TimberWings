// Assets/_Project/Editor/IslandV2/IslandPalette.cs
// Задача A2: цвета земли IslandLayout.asset под референс (мягкий лайм/мята/песок вместо оливкового).
// Меняет только 5 цветов, остальные поля раскладки не трогает. Откат: Ctrl+Z сразу после, либо git revert файла .asset.
// Цвета земли запекаются при сборке блокаута, поэтому после замены нужен пункт «2. Собрать блокаут» (скрипт предложит сам).
using UnityEditor;
using UnityEngine;

namespace IslandV2
{
    public static class IslandPalette
    {
        const string BuildMenu = "Tools/BuilderSim/Локация/Остров v2/2. Собрать блокаут";

        // sRGB, как в инспекторе
        static readonly (string field, string hex)[] Palette =
        {
            ("grassLight",  "#B5EE6E"),
            ("grassMid",    "#9BE564"),
            ("grassDeep",   "#6DD6A0"),
            ("forestFloor", "#4FBF8A"),
            ("pathSand",    "#F2DDA0"),
        };

        [MenuItem("Tools/BuilderSim/Локация/Остров v2/Палитра земли: Cloud Candy (A2)", false, 131)]
        static void Apply()
        {
            var layout = AssetDatabase.LoadAssetAtPath<IslandLayout>(IslandLayout.AssetPath);
            if (layout == null) { Debug.LogError("IslandPalette: нет раскладки " + IslandLayout.AssetPath + " (пункт 1 создаст)."); return; }

            var so = new SerializedObject(layout);
            var log = new System.Text.StringBuilder("IslandPalette: цвета земли (было -> стало)\n");
            int changed = 0;
            foreach (var (field, hex) in Palette)
            {
                var prop = so.FindProperty(field);
                Color c;
                if (prop == null || prop.propertyType != SerializedPropertyType.Color) { log.AppendLine("  [!] нет цветового поля " + field); continue; }
                if (!ColorUtility.TryParseHtmlString(hex, out c)) { log.AppendLine("  [!] не разобрал " + hex); continue; }
                log.AppendLine("  " + field + ": #" + ColorUtility.ToHtmlStringRGB(prop.colorValue) + " -> " + hex);
                prop.colorValue = c;
                changed++;
            }
            so.ApplyModifiedProperties();   // с Undo
            EditorUtility.SetDirty(layout);
            AssetDatabase.SaveAssets();
            Debug.Log(log.ToString());

            if (changed == 0) return;
            if (EditorUtility.DisplayDialog("Палитра земли", "Цвета записаны в IslandLayout.asset (" + changed + " из " + Palette.Length + ").\nПересобрать блокаут сейчас, чтобы увидеть их на острове?", "Пересобрать", "Позже"))
                EditorApplication.ExecuteMenuItem(BuildMenu);
        }
    }
}
