// Assets/_Project/Editor/Look/ISL010TwilightPreset.cs
// Обратимый пресет ISL-010: цвета заката и едва заметная коррекция ночи.
// Только Editor; DayNightCycle, LightingRig, FireLightPool и сцены НЕ изменяет.
using UnityEditor;
using UnityEngine;

public static class ISL010TwilightPreset
{
    const string Menu = "Tools/BuilderSim/Визуал и свет/ISL-010/";
    const string StylePath = "Assets/_Project/Resources/CloudCandyStyle.asset";
    const string SettingsPath = "Assets/_Project/Editor/Look/ISL010TwilightSettings.asset";
    const string BackupPath = "Assets/_Project/Editor/Look/ISL010TwilightBackup.asset";

    // Цвета заданы в том же формате Color32/sRGB, что используется в CloudCandyStyle.
    static readonly Color SunHorizonTarget = new Color32(255, 172, 156, 255); // #FFAC9C
    static readonly Color SunLowTarget = new Color32(255, 204, 165, 255);     // #FFCCA5
    static readonly Color DuskSkyTarget = new Color32(186, 168, 223, 255);    // #BAA8DF
    static readonly Color DuskEqTarget = new Color32(254, 195, 169, 255);     // #FEC3A9
    static readonly Color DuskGroundTarget = new Color32(243, 187, 171, 255); // #F3BBAB
    static readonly Color DuskFogTarget = new Color32(249, 199, 203, 255);    // #F9C7CB
    static readonly Color DuskZenithTarget = new Color32(148, 120, 206, 255); // #9478CE
    static readonly Color DuskHorizonTarget = new Color32(255, 201, 195, 255); // #FFC9C3
    static readonly Color DuskFilterTarget = new Color32(255, 229, 216, 255);  // #FFE5D8

    static readonly Color MoonTarget = new Color32(186, 198, 255, 255);       // #BAC6FF
    static readonly Color NightSkyTarget = new Color32(103, 119, 184, 255);  // #6777B8
    static readonly Color NightEqTarget = new Color32(82, 98, 157, 255);     // #52629D
    static readonly Color NightGroundTarget = new Color32(52, 61, 104, 255); // #343D68
    static readonly Color NightFogTarget = new Color32(48, 57, 105, 255);    // #303969
    static readonly Color NightFilterTarget = new Color32(206, 217, 255, 255); // #CED9FF

    [MenuItem(Menu + "1. Открыть настройки заката и ночи", false, 861)]
    static void SelectSettings()
    {
        if (!EditMode()) return;
        var settings = GetSettings();
        if (settings == null) return;
        Selection.activeObject = settings;
        EditorGUIUtility.PingObject(settings);
    }

    [MenuItem(Menu + "2. Применить палитру (без пересборки)", false, 862)]
    static void Apply()
    {
        if (!EditMode()) return;
        CloudCandyStyle style = FindStyle();
        ISL010TwilightSettings settings = GetSettings();
        if (!ValidStyle(style) || settings == null) return;

        var backup = AssetDatabase.LoadAssetAtPath<ISL010TwilightBackup>(BackupPath);
        if (backup == null && AssetDatabase.LoadMainAssetAtPath(BackupPath) != null)
        {
            Debug.LogError("[ISL-010] Путь резервной копии занят другим ассетом: " + BackupPath);
            return;
        }
        if (backup != null && backup.captured && backup.style != style)
        {
            Debug.LogError("[ISL-010] Найден backup от другого CloudCandyStyle. " +
                           "Восстанови старый профиль перед применением нового.");
            return;
        }
        if (backup == null)
        {
            backup = ScriptableObject.CreateInstance<ISL010TwilightBackup>();
            backup.name = "ISL010TwilightBackup";
            backup.Capture(style);
            AssetDatabase.CreateAsset(backup, BackupPath);
            EditorUtility.SetDirty(backup);
        }
        else if (!backup.captured)
        {
            Undo.RecordObject(backup, "ISL-010: сохранить исходные цвета");
            backup.Capture(style);
            EditorUtility.SetDirty(backup);
        }
        // Сначала сохраняем backup на диск, только потом меняем палитру.
        AssetDatabase.SaveAssets();

        float dusk = Mathf.Clamp01(settings.sunsetStrength);
        float night = Mathf.Clamp01(settings.nightStrength);
        Undo.RecordObject(style, "ISL-010: закат и ночь");
        // ВАЖНО: каждый раз смешиваем от первоначального backup, без накопительного дрейфа.
        style.sunHorizon = Color.Lerp(backup.sunHorizon, SunHorizonTarget, dusk);
        style.sunLow = Color.Lerp(backup.sunLow, SunLowTarget, dusk);
        style.ambientDusk.sky = Color.Lerp(backup.ambientDuskSky, DuskSkyTarget, dusk);
        style.ambientDusk.equator = Color.Lerp(backup.ambientDuskEquator, DuskEqTarget, dusk);
        style.ambientDusk.ground = Color.Lerp(backup.ambientDuskGround, DuskGroundTarget, dusk);
        style.fogDusk = Color.Lerp(backup.fogDusk, DuskFogTarget, dusk);
        style.zenithDusk = Color.Lerp(backup.zenithDusk, DuskZenithTarget, dusk);
        style.horizonDusk = Color.Lerp(backup.horizonDusk, DuskHorizonTarget, dusk);
        style.gradeDusk.filter = Color.Lerp(backup.gradeDuskFilter, DuskFilterTarget, dusk);

        style.moonColor = Color.Lerp(backup.moonColor, MoonTarget, night);
        style.ambientNight.sky = Color.Lerp(backup.ambientNightSky, NightSkyTarget, night);
        style.ambientNight.equator = Color.Lerp(backup.ambientNightEquator, NightEqTarget, night);
        style.ambientNight.ground = Color.Lerp(backup.ambientNightGround, NightGroundTarget, night);
        style.fogNight = Color.Lerp(backup.fogNight, NightFogTarget, night);
        style.gradeNight.filter = Color.Lerp(backup.gradeNightFilter, NightFilterTarget, night);

        EditorUtility.SetDirty(style);
        AssetDatabase.SaveAssets();
        SceneView.RepaintAll();
        Selection.activeObject = style;
        EditorGUIUtility.PingObject(style);
        Debug.Log("[ISL-010] Палитра заката и ночи применена к " + AssetDatabase.GetAssetPath(style) +
                  " (закат=" + dusk.ToString("0.00") + ", ночь=" + night.ToString("0.00") +
                  "). Старые цвета: " + BackupPath + ". Пересборка сцены не нужна.");
        if (!style.styleEnabled)
            Debug.LogWarning("[ISL-010] Style Enabled выключен: визуальных изменений в игре не будет.");
    }

    [MenuItem(Menu + "3. Восстановить предыдущие цвета", false, 863)]
    static void Restore()
    {
        if (!EditMode()) return;
        var backup = AssetDatabase.LoadAssetAtPath<ISL010TwilightBackup>(BackupPath);
        if (backup == null || !backup.captured || backup.style == null)
        {
            Debug.LogWarning("[ISL-010] Нет действительной резервной копии.");
            return;
        }
        CloudCandyStyle style = backup.style;
        if (!ValidStyle(style)) return;
        if (!EditorUtility.DisplayDialog("ISL-010: откат палитры",
                "Вернуть цвета заката и ночи из резервной копии? " +
                "Это также отменит ручные изменения этих 15 цветовых полей, сделанные после применения пресета.",
                "Восстановить", "Отмена")) return;

        Undo.RecordObject(style, "ISL-010: вернуть цвета");
        backup.Restore(style);
        EditorUtility.SetDirty(style);
        AssetDatabase.SaveAssets();
        if (!AssetDatabase.DeleteAsset(BackupPath))
            Debug.LogWarning("[ISL-010] Цвета восстановлены, но backup не удалось удалить: " + BackupPath);
        AssetDatabase.SaveAssets();
        SceneView.RepaintAll();
        Selection.activeObject = style;
        Debug.Log("[ISL-010] Закатная и ночная палитра восстановлена. День ISL-009 не изменялся.");
    }

    [MenuItem(Menu + "4. Выбрать CloudCandyStyle", false, 864)]
    static void SelectStyle()
    {
        var style = FindStyle();
        if (style == null) return;
        Selection.activeObject = style;
        EditorGUIUtility.PingObject(style);
    }

    static ISL010TwilightSettings GetSettings()
    {
        var settings = AssetDatabase.LoadAssetAtPath<ISL010TwilightSettings>(SettingsPath);
        if (settings != null) return settings;
        if (AssetDatabase.LoadMainAssetAtPath(SettingsPath) != null)
        {
            Debug.LogError("[ISL-010] Путь настроек занят другим ассетом: " + SettingsPath);
            return null;
        }
        settings = ScriptableObject.CreateInstance<ISL010TwilightSettings>();
        settings.name = "ISL010TwilightSettings";
        AssetDatabase.CreateAsset(settings, SettingsPath);
        AssetDatabase.SaveAssets();
        return settings;
    }

    static bool ValidStyle(CloudCandyStyle style)
    {
        if (style == null) return false;
        if (style.ambientDusk != null && style.ambientNight != null &&
            style.gradeDusk != null && style.gradeNight != null) return true;
        Debug.LogError("[ISL-010] В CloudCandyStyle отсутствуют ambientDusk, ambientNight, gradeDusk или gradeNight. Ничего не изменено.");
        return false;
    }

    static bool EditMode()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode) return true;
        Debug.LogWarning("[ISL-010] Применение и откат доступны только вне Play Mode.");
        return false;
    }

    static CloudCandyStyle FindStyle()
    {
        var primary = AssetDatabase.LoadAssetAtPath<CloudCandyStyle>(StylePath);
        if (primary != null) return primary;

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
                Debug.LogError("[ISL-010] Несколько CloudCandyStyle в Resources. Нельзя выбрать активный профиль автоматически.");
                return null;
            }
            found = candidate;
        }
        if (found == null)
            Debug.LogError("[ISL-010] CloudCandyStyle не найден в Resources. " +
                           "Используй 'Cloud Candy: создать или выбрать стиль'.");
        return found;
    }
}
