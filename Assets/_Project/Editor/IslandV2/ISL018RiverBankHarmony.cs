// Assets/_Project/Editor/IslandV2/ISL018RiverBankHarmony.cs
// ISL-018: локальная цветовая коррекция склона реки. Геометрия и IslandLayout не изменяются.
using UnityEditor;
using UnityEngine;

namespace IslandV2
{
    public static class ISL018RiverBankHarmony
    {
        const string MenuRoot = "Tools/BuilderSim/Локация/Остров v2/ISL-018/";

        [MenuItem(MenuRoot + "4. Речной склон: применить мягкую палитру", false, 150)]
        static void Apply()
        {
            if (Application.isPlaying)
            {
                Debug.LogError("ISL-018: выйди из Play Mode перед настройкой речного склона.");
                return;
            }

            var s = IslandCandy.GetOrCreate();
            if (!s.candyOn)
            {
                Debug.LogError("ISL-018: Candy-земля выключена; речной пресет не применён.");
                return;
            }

            if (!ColorUtility.TryParseHtmlString("#C5C0BA", out var dry) ||
                !ColorUtility.TryParseHtmlString("#829DA4", out var wet))
            {
                Debug.LogError("ISL-018: не удалось прочитать целевые цвета речного камня.");
                return;
            }

            Undo.RecordObject(s, "ISL-018: речной склон — мягкая палитра");
            s.riverBankHarmony = 0.70f;
            s.riverBankDry = dry;
            s.riverBankWet = wet;
            EditorUtility.SetDirty(s);
            AssetDatabase.SaveAssets();
            Selection.activeObject = s;
            EditorGUIUtility.PingObject(s);
            Debug.Log("ISL-018: настройки речного склона применены (0.70). " +
                      "Запусти «Candy-земля: перекрасить (без пересборки)». " +
                      "IslandLayout, река, мост и коллайдеры не изменялись.");
        }

        [MenuItem(MenuRoot + "5. Речной склон: отключить коррекцию", false, 151)]
        static void Disable()
        {
            if (Application.isPlaying)
            {
                Debug.LogError("ISL-018: выйди из Play Mode перед отключением коррекции.");
                return;
            }
            var s = IslandCandy.GetOrCreate();
            Undo.RecordObject(s, "ISL-018: вернуть цвет речного склона");
            s.riverBankHarmony = 0f;
            EditorUtility.SetDirty(s);
            AssetDatabase.SaveAssets();
            Selection.activeObject = s;
            Debug.Log("ISL-018: коррекция речного склона выключена. " +
                      "Перекрась Candy-землю, чтобы вернуться к исходной окраске.");
        }
    }
}
