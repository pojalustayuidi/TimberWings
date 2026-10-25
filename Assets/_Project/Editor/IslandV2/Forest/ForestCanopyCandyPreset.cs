#if UNITY_EDITOR
// Timber Wings — ISL-008, этап 2: мягкая палитра крон Cloudy Candy.
// Меняет только общие цвета существующих TreeToon-материалов; деревья и префабы НЕ пересобирает.
// Исходные цвета сохраняются в CanopyCandySettings.asset при ПЕРВОМ применении.
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace IslandV2
{
    public sealed class ForestCanopyCandySettings : ScriptableObject
    {
        public const string AssetPath = "Assets/_Project/IslandV2/CanopyCandySettings.asset";

        [Range(0f, 1f)]
        [Tooltip("0 = исходные цвета до первого применения; 1 = полная палитра ISL-008. Рекомендуется 0.7.")]
        public float paletteStrength = 0.70f;

        [Serializable]
        public sealed class OriginalColor
        {
            public string materialName;
            public Color top;
            public Color bottom;
        }

        // НЕ очищать этот список вручную: из него восстанавливаются исходные цвета.
        [HideInInspector]
        public List<OriginalColor> originals = new List<OriginalColor>();

        public OriginalColor FindOriginal(string name)
        {
            for (int i = 0; i < originals.Count; i++)
                if (originals[i] != null && originals[i].materialName == name)
                    return originals[i];
            return null;
        }

        public static ForestCanopyCandySettings LoadOrCreate()
        {
            var existing = AssetDatabase.LoadAssetAtPath<ForestCanopyCandySettings>(AssetPath);
            if (existing != null) return existing;
            if (!AssetDatabase.IsValidFolder("Assets/_Project/IslandV2"))
            {
                Debug.LogError("ISL-008: нет папки Assets/_Project/IslandV2. Сборка острова не выполнялась.");
                return null;
            }
            var settings = CreateInstance<ForestCanopyCandySettings>();
            AssetDatabase.CreateAsset(settings, AssetPath);
            AssetDatabase.SaveAssets();
            return settings;
        }
    }

    public static class ForestCanopyCandyPreset
    {
        const string Menu = "Tools/BuilderSim/Лес/Красота леса/";

        // Используем только имена лиственных/хвойных материалов из TreeToonSetup.Looks.
        // Цвета нормированы так же, как оригинальная библиотека TreeToonSetup.
        struct Target
        {
            public readonly string name;
            public readonly Color top;
            public readonly Color bottom;

            public Target(string name, Color top, Color bottom)
            {
                this.name = name;
                this.top = top;
                this.bottom = bottom;
            }
        }

        static Color C(float r, float g, float b) { return new Color(r, g, b, 1f); }

        static readonly Target[] Targets =
        {
            new Target("Tree_Leaf_Green",       C(.57f, .82f, .35f), C(.12f, .34f, .20f)),
            new Target("Tree_Leaf_Under",       C(.22f, .50f, .23f), C(.09f, .25f, .15f)),
            new Target("Tree_Leaf_Dark",        C(.26f, .53f, .38f), C(.10f, .25f, .21f)),
            new Target("Tree_Leaf_DarkUnder",   C(.14f, .31f, .26f), C(.06f, .17f, .14f)),
            new Target("Tree_Leaf_DarkTip",     C(.44f, .71f, .39f), C(.13f, .34f, .27f)),
            new Target("Tree_Leaf_Birch",       C(.63f, .85f, .42f), C(.18f, .40f, .23f)),
            new Target("Tree_Leaf_BirchUnder",  C(.45f, .69f, .32f), C(.16f, .34f, .21f)),
            new Target("Tree_Leaf_Oak",         C(.51f, .78f, .31f), C(.11f, .31f, .19f)),
            new Target("Tree_Leaf_OakAlt",      C(.67f, .84f, .33f), C(.15f, .35f, .21f)),
            new Target("Tree_Leaf_OakUnder",    C(.25f, .51f, .24f), C(.10f, .24f, .16f)),
            new Target("Tree_Leaf_Yellow",      C(.92f, .80f, .33f), C(.48f, .40f, .17f)),
            new Target("Tree_Leaf_Orange",      C(.94f, .67f, .31f), C(.55f, .33f, .16f)),
            new Target("Tree_Leaf_UnderYellow", C(.72f, .57f, .20f), C(.37f, .27f, .13f))
        };

        [MenuItem(Menu + "ISL-008: 1. Настройки палитры крон", false, 270)]
        static void OpenSettings()
        {
            var settings = ForestCanopyCandySettings.LoadOrCreate();
            if (settings == null) return;
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
            Debug.Log("ISL-008: настройки крон: " + ForestCanopyCandySettings.AssetPath);
        }

        [MenuItem(Menu + "ISL-008: 2. Применить палитру крон (без пересборки)", false, 271)]
        static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("ISL-008: останови Play Mode перед изменением материалов.");
                return;
            }

            var settings = ForestCanopyCandySettings.LoadOrCreate();
            if (settings == null) return;
            float t = Mathf.Clamp01(settings.paletteStrength);
            int changed = 0, missing = 0;
            var missingNames = new List<string>();

            for (int i = 0; i < Targets.Length; i++)
            {
                var target = Targets[i];
                Material mat = GetExistingLeafMaterial(target.name);
                if (mat == null)
                {
                    missing++;
                    missingNames.Add(target.name);
                    continue;
                }

                var original = settings.FindOriginal(target.name);
                if (original == null)
                {
                    // Запоминаем фактические цвета материала, а не предполагаемые дефолты библиотеки.
                    Undo.RecordObject(settings, "ISL-008: сохранить цвета крон до пресета");
                    original = new ForestCanopyCandySettings.OriginalColor
                    {
                        materialName = target.name,
                        top = mat.GetColor("_BaseColor"),
                        bottom = mat.GetColor("_BottomColor")
                    };
                    settings.originals.Add(original);
                    EditorUtility.SetDirty(settings);
                }

                // Повторное применение детерминированно: смешиваем с исходным снимком,
                // а НЕ с результатом предыдущего применения.
                Undo.RecordObject(mat, "ISL-008: палитра крон " + target.name);
                mat.SetColor("_BaseColor", Color.Lerp(original.top, target.top, t));
                mat.SetColor("_BottomColor", Color.Lerp(original.bottom, target.bottom, t));
                EditorUtility.SetDirty(mat);
                changed++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log("ISL-008: палитра крон применена: " + changed + " материалов TreeToon, сила " + t.ToString("0.00")
                + "; пропущено " + missing + (missing > 0 ? " (" + string.Join(", ", missingNames.ToArray()) + ")" : "")
                + ". Лес, префабы, геометрия, коллайдеры не пересоздавались. Резерв цветов: "
                + ForestCanopyCandySettings.AssetPath);
        }

        [MenuItem(Menu + "ISL-008: 3. Вернуть цвета крон до пресета", false, 272)]
        static void Restore()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("ISL-008: останови Play Mode перед изменением материалов.");
                return;
            }

            // Не создавать новый пустой asset при восстановлении: если резерв потерян, это ошибка.
            var settings = AssetDatabase.LoadAssetAtPath<ForestCanopyCandySettings>(ForestCanopyCandySettings.AssetPath);
            if (settings == null || settings.originals == null || settings.originals.Count == 0)
            {
                Debug.LogWarning("ISL-008: резерв исходных цветов не найден. Восстанови материалы из Git.");
                return;
            }
            int restored = 0;
            for (int i = 0; i < settings.originals.Count; i++)
            {
                var original = settings.originals[i];
                if (original == null) continue;
                var mat = GetExistingLeafMaterial(original.materialName);
                if (mat == null) continue;
                Undo.RecordObject(mat, "ISL-008: вернуть исходный цвет " + original.materialName);
                mat.SetColor("_BaseColor", original.top);
                mat.SetColor("_BottomColor", original.bottom);
                EditorUtility.SetDirty(mat);
                restored++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log("ISL-008: восстановлены цвета " + restored + " материалов. Резерв сохранён для повторного применения.");
        }

        static Material GetExistingLeafMaterial(string name)
        {
            // НЕ вызывать TreeToonSetup.Library: она может создавать отсутствующие материалы.
            string path = TreeToonSetup.MatDir + "/" + name + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null || mat.shader == null || mat.shader.name != "Project/TreeToon"
                || !mat.HasProperty("_BaseColor") || !mat.HasProperty("_BottomColor")) return null;
            return mat;
        }
    }
}
#endif
