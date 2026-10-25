// Assets/_Project/Editor/Look/ISL009DaylightPreset.cs
// ISL-009: первый, обратимый шаг настройки дневного освещения Cloud Candy.
// Не создаёт источников света и не изменяет DayNightCycle, LightingRig, URP или сцены.
// Работает ТОЛЬКО в Edit Mode. Резервная копия хранится отдельным Editor-only asset.
using UnityEditor;
using UnityEngine;

public static class ISL009DaylightPreset
{
    const string Menu = "Tools/BuilderSim/Визуал и свет/ISL-009/";
    const string StylePath = "Assets/_Project/Resources/CloudCandyStyle.asset";
    const string BackupPath = "Assets/_Project/Editor/Look/ISL009DaylightBackup.asset";

    // Деликатный дневной Cloudy Candy: немного светлее тени под кронами,
    // немного меньше желтизны в полдень. Коэффициент 0.65 защищает пользовательскую палитру.
    const float Blend = 0.65f;
    static readonly Color TargetSky = new Color32(193, 217, 253, 255);    // #C1D9FD
    static readonly Color TargetEq = new Color32(240, 224, 236, 255);     // #F0E0EC
    static readonly Color TargetGround = new Color32(255, 225, 201, 255); // #FFE1C9
    static readonly Color TargetSunNoon = new Color32(255, 237, 205, 255); // #FFEDCD

    [MenuItem(Menu + "1. Применить мягкий дневной пресет", false, 851)]
    static void Apply()
    {
        if (!EnsureEditMode()) return;
        CloudCandyStyle style = FindStyle();
        if (style == null) return;
        if (style.ambientDay == null)
        {
            Debug.LogError("[ISL-009] ambientDay == null: ничего не изменено.");
            return;
        }

        var backup = AssetDatabase.LoadAssetAtPath<ISL009DaylightBackup>(BackupPath);
        if (backup == null && AssetDatabase.LoadMainAssetAtPath(BackupPath) != null)
        {
            Debug.LogError("[ISL-009] Путь резервной копии занят другим ассетом: " + BackupPath);
            return;
        }
        if (backup != null && backup.captured && backup.style != style)
        {
            Debug.LogError("[ISL-009] Резервная копия принадлежит другому CloudCandyStyle. " +
                           "Сначала восстанови исходный профиль. Ничего не изменено.");
            return;
        }
        if (backup == null)
        {
            backup = ScriptableObject.CreateInstance<ISL009DaylightBackup>();
            backup.name = "ISL009DaylightBackup";
            AssetDatabase.CreateAsset(backup, BackupPath);
        }
        if (!backup.captured)
        {
            Undo.RecordObject(backup, "ISL-009: сохранить дневную палитру");
            backup.style = style;
            backup.ambientSky = style.ambientDay.sky;
            backup.ambientEquator = style.ambientDay.equator;
            backup.ambientGround = style.ambientDay.ground;
            backup.sunNoon = style.sunNoon;
            backup.captured = true;
            EditorUtility.SetDirty(backup);
        }

        Undo.RecordObject(style, "ISL-009: дневная палитра");
        // Повторные применения детерминированы и не усиливают эффект:
        // исходные значения каждый раз берутся из первой резервной копии.
        style.ambientDay.sky = Color.Lerp(backup.ambientSky, TargetSky, Blend);
        style.ambientDay.equator = Color.Lerp(backup.ambientEquator, TargetEq, Blend);
        style.ambientDay.ground = Color.Lerp(backup.ambientGround, TargetGround, Blend);
        style.sunNoon = Color.Lerp(backup.sunNoon, TargetSunNoon, Blend);
        EditorUtility.SetDirty(style);
        AssetDatabase.SaveAssets();
        SceneView.RepaintAll();
        Selection.activeObject = style;
        EditorGUIUtility.PingObject(style);

        Debug.Log("[ISL-009] Дневной пресет применён к " + AssetDatabase.GetAssetPath(style) +
                  ". Создана/сохранена резервная копия " + BackupPath +
                  ". Важно: вечер/ночь не переписаны, но их переход к дневному ambient может слегка измениться.");
        if (!style.styleEnabled)
            Debug.LogWarning("[ISL-009] Style Enabled = false: профиль не используется во время игры.");
    }

    [MenuItem(Menu + "2. Восстановить предыдущие дневные цвета", false, 852)]
    static void Restore()
    {
        if (!EnsureEditMode()) return;
        var backup = AssetDatabase.LoadAssetAtPath<ISL009DaylightBackup>(BackupPath);
        if (backup == null || !backup.captured || backup.style == null)
        {
            Debug.LogWarning("[ISL-009] Нет сохранённой резервной копии дневных цветов.");
            return;
        }
        var style = backup.style;
        if (style.ambientDay == null)
        {
            Debug.LogError("[ISL-009] ambientDay == null, восстановление отменено.");
            return;
        }
        Undo.RecordObject(style, "ISL-009: откат дневной палитры");
        style.ambientDay.sky = backup.ambientSky;
        style.ambientDay.equator = backup.ambientEquator;
        style.ambientDay.ground = backup.ambientGround;
        style.sunNoon = backup.sunNoon;
        EditorUtility.SetDirty(style);
        AssetDatabase.SaveAssets();
        if (!AssetDatabase.DeleteAsset(BackupPath))
            Debug.LogWarning("[ISL-009] Цвета восстановлены, но резервный asset не удалось удалить: " + BackupPath);
        AssetDatabase.SaveAssets();
        SceneView.RepaintAll();
        Selection.activeObject = style;
        Debug.Log("[ISL-009] Дневные цвета восстановлены. Остальные настройки стиля не менялись.");
    }

    [MenuItem(Menu + "3. Выбрать CloudCandyStyle", false, 853)]
    static void SelectStyle()
    {
        var style = FindStyle();
        if (style == null) return;
        Selection.activeObject = style;
        EditorGUIUtility.PingObject(style);
    }

    static bool EnsureEditMode()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode) return true;
        Debug.LogWarning("[ISL-009] Пресет и откат запускаются только вне Play Mode, " +
                         "чтобы изменения ассета сохранялись предсказуемо.");
        return false;
    }

    static CloudCandyStyle FindStyle()
    {
        // Приоритет — стандартному пути, который создаёт CloudCandyStyleSetup.
        var primary = AssetDatabase.LoadAssetAtPath<CloudCandyStyle>(StylePath);
        if (primary != null) return primary;

        // Не создаём новый профиль поверх пользовательского и не выбираем случайный.
        var ids = AssetDatabase.FindAssets("t:CloudCandyStyle");
        CloudCandyStyle found = null;
        foreach (string id in ids)
        {
            string path = AssetDatabase.GUIDToAssetPath(id).Replace('\\', '/');
            if (!path.Contains("/Resources/")) continue;
            var candidate = AssetDatabase.LoadAssetAtPath<CloudCandyStyle>(path);
            if (candidate == null) continue;
            if (found != null && found != candidate)
            {
                Debug.LogError("[ISL-009] Несколько профилей CloudCandyStyle в Resources. " +
                               "Нужно явно выбрать активный — ничего не изменено.");
                return null;
            }
            found = candidate;
        }
        if (found == null)
            Debug.LogError("[ISL-009] Не найден CloudCandyStyle в Resources. " +
                           "Используй меню 'Cloud Candy: создать или выбрать стиль'.");
        return found;
    }
}
