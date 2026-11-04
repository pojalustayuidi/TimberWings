#if UNITY_EDITOR
// ISL-020: обратимые настройки мягких цветовых и декоративных переходов зон.
// Не меняет IslandLayout, геометрию, координаты деревьев и физику.
using UnityEditor;
using UnityEngine;

namespace IslandV2
{
    public static class ISL020ZoneTransitions
    {
        const string Menu = "Tools/BuilderSim/Локация/Остров v2/ISL-020/";

        [MenuItem(Menu + "1. Открыть настройки переходов", false, 160)]
        static void Open()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var candy = IslandCandy.GetOrCreate();
            var decor = ForestUnderstoryCandySettings.Load();
            Selection.objects = new Object[] { candy, decor };
            EditorGUIUtility.PingObject(candy);
        }

        [MenuItem(Menu + "2. Включить мягкие переходы (без генерации)", false, 161)]
        static void Apply() { Set(true); }

        [MenuItem(Menu + "3. Отключить мягкие переходы (откат настроек)", false, 162)]
        static void Disable() { Set(false); }

        static void Set(bool enabled)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("ISL-020: сначала выйди из Play Mode.");
                return;
            }
            var candy = IslandCandy.GetOrCreate();
            if (!candy.candyOn)
            {
                Debug.LogError("ISL-020: Candy-земля выключена. Пресет не применён.");
                return;
            }
            var decor = ForestUnderstoryCandySettings.Load();
            Undo.RecordObjects(new Object[] { candy, decor }, "ISL-020: переходы между зонами");
            candy.zoneForestBlend = enabled ? 0.65f : 0f;
            candy.zoneShoreBlend = enabled ? 0.55f : 0f;
            candy.zonePadBlend = enabled ? 0.50f : 0f;
            decor.zoneBlendStrength = enabled ? 0.75f : 0f;
            // Изменяем только силу: размеры feather и Seed остаются пользовательскими.
            EditorUtility.SetDirty(candy);
            EditorUtility.SetDirty(decor);
            AssetDatabase.SaveAssets();
            Debug.Log("ISL-020: настройки " + (enabled ? "включены" : "отключены") +
                      ". Для цвета: Candy-земля: перекрасить (без пересборки). " +
                      "Для изменения кустов/цветов: отдельно пересоздай только Подлесок (пункт 2); " +
                      "старый корень Understory будет заменён, сохрани сцену в Git заранее.");
        }
    }
}
#endif
