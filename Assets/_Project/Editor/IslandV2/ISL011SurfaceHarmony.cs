#if UNITY_EDITOR
// Timber Wings | ISL-011 | Обратимая настройка существующих материалов поверхности.
// Не изменяет IslandLayout, IslandCandy, запечённые текстуры, меши и географию.
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace IslandV2
{
    public sealed class ISL011SurfaceHarmonySettings : ScriptableObject
    {
        [Range(0f, 1f)] public float strength = 0.65f;
        public bool tuneGroundHighlights = true;
        public bool tuneCliff = true;
        public bool tuneForestRocks = true;
    }

    [Serializable]
    public sealed class ISL011SurfaceValue
    {
        public string assetPath;
        public string shaderName;
        public string propertyName;
        public bool isColor;
        public Color originalColor;
        public float originalFloat;
    }

    public sealed class ISL011SurfaceHarmonyBackup : ScriptableObject
    {
        public bool captured;
        public List<ISL011SurfaceValue> values = new List<ISL011SurfaceValue>();
    }

    public static class ISL011SurfaceHarmony
    {
        const string Root = "Assets/_Project/IslandV2/";
        const string SettingsPath = Root + "ISL011SurfaceHarmonySettings.asset";
        const string BackupPath = Root + "ISL011_SurfaceHarmonyBackup.asset";
        const string Ground = Root + "Mat_IslandV2.mat";
        const string Cliff = Root + "Mat_IslandRock.mat";
        const string Trees = "Assets/_Project/Prefabs/Trees/Generated/Materials/";
        const string TreeS = "Assets/_Project/Prefabs/TreeS/Generated/Materials/";

        sealed class Target
        {
            public string path;
            public string shader;
            public string property;
            public bool color;
            public Color colorGoal;
            public float floatGoal;
            public int group;
        }

        static Color Hex(string hex)
        {
            Color c;
            if (!ColorUtility.TryParseHtmlString(hex, out c))
                throw new ArgumentException("Bad color " + hex);
            return c;
        }

        static void AddFloat(List<Target> list, string path, string shader,
            string property, float goal, int group)
        {
            list.Add(new Target { path = path, shader = shader, property = property,
                floatGoal = goal, group = group });
        }

        static void AddColor(List<Target> list, string path, string shader,
            string property, string hex, int group)
        {
            list.Add(new Target { path = path, shader = shader, property = property,
                color = true, colorGoal = Hex(hex), group = group });
        }

        static void AddForestStone(List<Target> list, string root, string name,
            string upper, string lower, float rim)
        {
            string path = root + name + ".mat";
            const string shader = "Project/TreeToon";
            AddColor(list, path, shader, "_BaseColor", upper, 2);
            AddColor(list, path, shader, "_BottomColor", lower, 2);
            AddFloat(list, path, shader, "_RimStrength", rim, 2);
        }

        static List<Target> Targets()
        {
            var list = new List<Target>();
            // IMPORTANT: НЕ менять IslandCandy.glossPath. Его альфа участвует в pathness шейдера.
            AddFloat(list, Ground, "Project/IslandV2/VertexColor", "_Gloss", 0.43f, 0);
            AddFloat(list, Cliff, "Project/IslandRock", "_Rim", 0.16f, 1);
            AddColor(list, Cliff, "Project/IslandRock", "_Shade", "#948AAE", 1);
            // Используем обе реально встречающиеся вариации папок Trees/TreeS.
            foreach (string dir in new[] { Trees, TreeS })
            {
                AddForestStone(list, dir, "Under_Rock", "#C9C4D8", "#807A91", 0.12f);
                AddForestStone(list, dir, "Grove_StoneMoss", "#BBC0B5", "#606962", 0.10f);
            }
            // Grandpa_Stone и Sacred_Stone намеренно оставлены как сюжетные акценты.
            return list;
        }

        static ISL011SurfaceHarmonySettings Settings(bool create)
        {
            var result = AssetDatabase.LoadAssetAtPath<ISL011SurfaceHarmonySettings>(SettingsPath);
            if (result == null && create)
            {
                result = ScriptableObject.CreateInstance<ISL011SurfaceHarmonySettings>();
                AssetDatabase.CreateAsset(result, SettingsPath);
                AssetDatabase.SaveAssets();
            }
            return result;
        }

        static ISL011SurfaceHarmonyBackup Backup(bool create)
        {
            var result = AssetDatabase.LoadAssetAtPath<ISL011SurfaceHarmonyBackup>(BackupPath);
            if (result == null && create)
            {
                result = ScriptableObject.CreateInstance<ISL011SurfaceHarmonyBackup>();
                AssetDatabase.CreateAsset(result, BackupPath);
                AssetDatabase.SaveAssets();
            }
            return result;
        }

        static bool Compatible(Material material, string shader)
        {
            return material != null && material.shader != null && material.shader.name == shader;
        }

        static string Key(string path, string prop) { return path + "|" + prop; }

        [MenuItem("Tools/BuilderSim/Локация/Остров v2/ISL-011/1. Открыть настройки гармонии")]
        public static void OpenSettings()
        {
            var obj = Settings(true);
            Selection.activeObject = obj;
            EditorGUIUtility.PingObject(obj);
            Debug.Log("ISL-011: настройки открыты: " + SettingsPath);
        }

        [MenuItem("Tools/BuilderSim/Локация/Остров v2/ISL-011/2. Применить материалы (без пересборки)")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("ISL-011: выйдите из Play Mode перед применением.");
                return;
            }
            var main = AssetDatabase.LoadAssetAtPath<Material>(Ground);
            if (!Compatible(main, "Project/IslandV2/VertexColor") || !main.HasProperty("_Gloss"))
            {
                Debug.LogError("ISL-011: основной материал не найден или шейдер изменён: " + Ground);
                return;
            }

            var settings = Settings(true);
            var backup = Backup(true);
            var specs = Targets();
            if (!backup.captured)
            {
                backup.values.Clear();
                foreach (var t in specs)
                {
                    var m = AssetDatabase.LoadAssetAtPath<Material>(t.path);
                    if (m == null) continue; // необязательные дубли папок Trees/TreeS
                    if (!Compatible(m, t.shader) || !m.HasProperty(t.property))
                    {
                        Debug.LogWarning("ISL-011: пропуск неподходящего материала/свойства: " + Key(t.path, t.property));
                        continue;
                    }
                    backup.values.Add(new ISL011SurfaceValue
                    {
                        assetPath = t.path, shaderName = t.shader, propertyName = t.property,
                        isColor = t.color,
                        originalColor = t.color ? m.GetColor(t.property) : Color.white,
                        originalFloat = t.color ? 0f : m.GetFloat(t.property)
                    });
                }
                if (backup.values.Count == 0)
                {
                    Debug.LogError("ISL-011: не найдены подходящие свойства материалов. Ничего не изменено.");
                    return;
                }
                backup.captured = true;
                EditorUtility.SetDirty(backup);
                AssetDatabase.SaveAssets(); // резервные значения сохраняются ДО изменения материалов
                Debug.Log("ISL-011: резервная копия сохранена: " + BackupPath
                    + ", свойств: " + backup.values.Count);
            }

            var originals = new Dictionary<string, ISL011SurfaceValue>();
            foreach (var v in backup.values)
                originals[Key(v.assetPath, v.propertyName)] = v;

            int updated = 0, skipped = 0;
            foreach (var t in specs)
            {
                ISL011SurfaceValue old;
                if (!originals.TryGetValue(Key(t.path, t.property), out old))
                {
                    skipped++;
                    continue; // новые материалы после создания backup не меняем без их исходных значений
                }
                var m = AssetDatabase.LoadAssetAtPath<Material>(t.path);
                if (!Compatible(m, t.shader) || !m.HasProperty(t.property))
                {
                    Debug.LogWarning("ISL-011: материал изменён или отсутствует, пропуск: " + t.path);
                    skipped++;
                    continue;
                }
                bool enabled = t.group == 0 ? settings.tuneGroundHighlights
                             : t.group == 1 ? settings.tuneCliff
                             : settings.tuneForestRocks;
                float weight = enabled ? Mathf.Clamp01(settings.strength) : 0f;
                Undo.RecordObject(m, "ISL-011 Surface Harmony");
                if (t.color) m.SetColor(t.property, Color.Lerp(old.originalColor, t.colorGoal, weight));
                else m.SetFloat(t.property, Mathf.Lerp(old.originalFloat, t.floatGoal, weight));
                EditorUtility.SetDirty(m);
                updated++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log("ISL-011: применено " + updated + " свойств, пропущено " + skipped
                + "; сила " + settings.strength.ToString("0.00")
                + ". Текстуры земли, геометрия и IslandLayout не затронуты.");
            if (AssetDatabase.LoadAssetAtPath<Material>("Assets/patch-dirt.mat") != null)
                Debug.Log("ISL-011: Assets/patch-dirt.mat (URP Lit) обнаружен, не изменён: нужна отдельная визуальная проверка его 141 слотов.");
            var cliff = AssetDatabase.LoadAssetAtPath<Material>(Cliff);
            if (cliff != null && cliff.HasProperty("_UseCandyGround") && cliff.GetFloat("_UseCandyGround") < 0.5f &&
                cliff.HasProperty("_GroundTex") && cliff.GetTexture("_GroundTex") == null)
                Debug.LogWarning("ISL-011: у обрыва отключён _UseCandyGround и не назначена _GroundTex. Проверьте кромку скалы и берег: режим может быть намеренным.");
        }

        [MenuItem("Tools/BuilderSim/Локация/Остров v2/ISL-011/3. Восстановить исходные материалы")]
        public static void Restore()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("ISL-011: выйдите из Play Mode перед восстановлением.");
                return;
            }
            var backup = Backup(false);
            if (backup == null || !backup.captured)
            {
                Debug.LogWarning("ISL-011: резервная копия не найдена. Ничего не изменено.");
                return;
            }
            int restored = 0;
            foreach (var old in backup.values)
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(old.assetPath);
                if (!Compatible(m, old.shaderName) || !m.HasProperty(old.propertyName))
                {
                    Debug.LogWarning("ISL-011: не удалось восстановить " + Key(old.assetPath, old.propertyName));
                    continue;
                }
                Undo.RecordObject(m, "ISL-011 Restore Surface Materials");
                if (old.isColor) m.SetColor(old.propertyName, old.originalColor);
                else m.SetFloat(old.propertyName, old.originalFloat);
                EditorUtility.SetDirty(m);
                restored++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log("ISL-011: восстановлено свойств: " + restored
                + ". Резервная копия оставлена на диске.");
        }
    }
}
#endif
