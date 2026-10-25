#if UNITY_EDITOR
// Assets/_Project/Editor/Look/CloudCandyStyleSetup.cs
// Меню для стиля Cloud Candy (этап 1: свет, небо, туман, постобработка):
//   Tools > BuilderSim > Визуал и свет > Cloud Candy: создать или выбрать стиль
//       создаёт Assets/_Project/Resources/CloudCandyStyle.asset (если его ещё нет) и выделяет его в Project.
//       Ассет должен лежать в папке Resources: рантайм-скрипты берут его через Resources.Load.
//   Tools > BuilderSim > Визуал и свет > Cloud Candy: проверить URP
//       только читает настройки URP Asset и пишет в Console, что включить для Bloom и мягких теней. Ничего не меняет.
// Откат стиля: снять галочку «Style Enabled» в ассете или удалить ассет, скрипты вернутся к своим прежним значениям.
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class CloudCandyStyleSetup
{
    const string Folder = "Assets/_Project/Resources";
    const string AssetPath = Folder + "/CloudCandyStyle.asset";

    [MenuItem("Tools/BuilderSim/Визуал и свет/Cloud Candy: создать или выбрать стиль", false, 840)]
    static void CreateOrSelect()
    {
        var existing = FindExisting();
        if (existing != null)
        {
            string p = AssetDatabase.GetAssetPath(existing);
            if (!p.Replace('\\', '/').Contains("/Resources/"))
                Debug.LogWarning("[Cloud Candy] Ассет лежит вне папки Resources (" + p + "): в игре его не найдут. Перенеси его в любую папку с именем Resources.");
            Select(existing);
            return;
        }

        EnsureFolder("Assets/_Project");
        EnsureFolder(Folder);
        var style = ScriptableObject.CreateInstance<CloudCandyStyle>();
        AssetDatabase.CreateAsset(style, AssetPath);
        AssetDatabase.SaveAssets();
        Debug.Log("[Cloud Candy] Создан " + AssetPath + ". Цвета и числа правятся в инспекторе, в том числе в Play Mode.");
        Select(style);
    }

    // Для других инструментов (Остров v2, этап 3): найти ассет стиля или создать его в Resources. created = true, если создан только что
    public static CloudCandyStyle GetOrCreate(out bool created)
    {
        created = false;
        var existing = FindExisting();
        if (existing != null) return existing;

        EnsureFolder("Assets/_Project");
        EnsureFolder(Folder);
        var style = ScriptableObject.CreateInstance<CloudCandyStyle>();
        AssetDatabase.CreateAsset(style, AssetPath);
        AssetDatabase.SaveAssets();
        created = true;
        return style;
    }

    [MenuItem("Tools/BuilderSim/Визуал и свет/Cloud Candy: проверить URP", false, 841)]
    static void CheckUrp()
    {
        var asset = GraphicsSettings.currentRenderPipeline;
        if (asset == null) { Debug.LogWarning("[Cloud Candy] Активный URP Asset не найден (Project Settings > Graphics)."); return; }

        var so = new SerializedObject(asset);
        var sb = new System.Text.StringBuilder("[Cloud Candy] URP Asset: " + asset.name + "\n");
        Report(so, sb, "m_SupportsHDR", "HDR", "Bloom с порогом около 1 виден только с HDR: включи Quality > HDR в URP Asset");
        Report(so, sb, "m_SoftShadowsSupported", "Мягкие тени", "Без этого пункта мягкие тени на Среднем и Высоком не работают");
        Report(so, sb, "m_MainLightShadowsSupported", "Тени главного света", "Без них нет теней от солнца");
        Report(so, sb, "m_AdditionalLightShadowsSupported", "Тени доп. источников", "Нужны для теней от фонарей (на Низком они и так выключены)");
        Debug.Log(sb.ToString());
    }

    static void Report(SerializedObject so, System.Text.StringBuilder sb, string field, string label, string hint)
    {
        var p = so.FindProperty(field);
        if (p == null) { sb.AppendLine("  " + label + ": поле " + field + " не найдено в этой версии URP, проверь вручную"); return; }
        bool on = p.propertyType == SerializedPropertyType.Boolean ? p.boolValue : p.intValue != 0;
        sb.AppendLine("  " + label + ": " + (on ? "включено" : "ВЫКЛЮЧЕНО. " + hint));
    }

    static CloudCandyStyle FindExisting()
    {
        var guids = AssetDatabase.FindAssets("t:CloudCandyStyle");
        for (int i = 0; i < guids.Length; i++)
        {
            var a = AssetDatabase.LoadAssetAtPath<CloudCandyStyle>(AssetDatabase.GUIDToAssetPath(guids[i]));
            if (a != null) return a;
        }
        return null;
    }

    static void Select(Object o)
    {
        Selection.activeObject = o;
        EditorGUIUtility.PingObject(o);
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
#endif
