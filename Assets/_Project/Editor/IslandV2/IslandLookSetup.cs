// Assets/_Project/Editor/IslandV2/IslandLookSetup.cs
// Остров v2, этап 3 (свет, небо, дымка): подготовка. Меню Tools/BuilderSim/Локация/Остров v2/ (приоритеты 124, 125).
// Части этапа 3: IslandLookSetup.cs (этот файл), IslandLookCheck.cs (проверка и замер, пункт 126).
//
//   5. Свет и дымка: создать или выбрать стиль Cloud Candy (124)
//      находит ассет CloudCandyStyle (он же хранит ВСЕ числа этапа 3) или создаёт его в Assets/_Project/Resources и выделяет.
//      Существующий ассет не перезаписывается: новые поля (дымка, профили бюджета, фон облаков) подхватят значения по умолчанию сами.
//   6. Свет и дымка: шейдеры в сборку и проверка URP (125)
//      добавляет шейдер Project/IslandV2/Haze в Always Included Shaders (его ищут через Shader.Find) и пишет в Console настройки URP
//      по каждому уровню качества Unity (HDR, мягкие тени, каскады, дальность). Ничего в URP не меняет.
// Шейдер дымки добавляется и сам при загрузке редактора (как SkyDomeSetup делает это для неба).
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace IslandV2
{
    [InitializeOnLoad]
    public static class IslandLookSetup
    {
        public const string HazeShaderName = "Project/IslandV2/Haze";

        static IslandLookSetup()
        {
            EditorApplication.delayCall += () => EnsureShaderIncluded(HazeShaderName, false);
        }

        [MenuItem("Tools/BuilderSim/Локация/Остров v2/5. Свет и дымка: создать или выбрать стиль Cloud Candy", false, 124)]
        static void CreateOrSelectStyle()
        {
            bool created;
            var style = CloudCandyStyleSetup.GetOrCreate(out created);
            Selection.activeObject = style;
            EditorGUIUtility.PingObject(style);
            Debug.Log(created
                ? "IslandV2: создан ассет Cloud Candy (" + AssetDatabase.GetAssetPath(style) + "). Блоки «Остров v2: ...» в инспекторе: дымка, профили бюджета, фон облаков, тон теней."
                : "IslandV2: ассет Cloud Candy уже есть (" + AssetDatabase.GetAssetPath(style) + "), он выделен. Новые поля этапа 3 подхватили значения по умолчанию, старые числа не тронуты.");
        }

        [MenuItem("Tools/BuilderSim/Локация/Остров v2/6. Свет и дымка: шейдеры в сборку и проверка URP", false, 125)]
        static void ShadersAndUrp()
        {
            var sb = new StringBuilder("IslandV2: шейдеры и URP (этап 3)\n");
            sb.AppendLine("  " + (EnsureShaderIncluded(HazeShaderName, true) ? "[ок] шейдер дымки в Always Included Shaders" : "[НЕТ] шейдер дымки не найден или не добавился"));
            sb.AppendLine("  " + (Shader.Find("Project/IslandV2/VertexColor") != null ? "[ок] шейдер земли Project/IslandV2/VertexColor найден" : "[НЕТ] шейдер земли не найден"));
            AppendUrp(sb);
            Debug.Log(sb.ToString());
            GUIUtility.systemCopyBuffer = sb.ToString();
        }

        // true = шейдер уже в списке или добавлен
        public static bool EnsureShaderIncluded(string shaderName, bool verbose)
        {
            var sh = Shader.Find(shaderName);
            if (sh == null) return false;

            var all = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (all == null || all.Length == 0) return false;
            var so = new SerializedObject(all[0]);
            var arr = so.FindProperty("m_AlwaysIncludedShaders");
            if (arr == null) return false;

            for (int i = 0; i < arr.arraySize; i++)
                if (arr.GetArrayElementAtIndex(i).objectReferenceValue == sh) return true;

            arr.arraySize++;
            arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = sh;
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            if (verbose) Debug.Log("[IslandV2] Шейдер " + shaderName + " добавлен в Always Included Shaders.");
            return true;
        }

        // URP Asset каждого уровня качества Unity: что включено и сколько каскадов теней
        public static void AppendUrp(StringBuilder sb)
        {
            string[] names = QualitySettings.names;
            for (int i = 0; i < names.Length; i++)
            {
                RenderPipelineAsset rp = QualitySettings.GetRenderPipelineAssetAt(i);
                if (rp == null) rp = GraphicsSettings.defaultRenderPipeline;
                if (rp == null) { sb.AppendLine("  уровень «" + names[i] + "»: URP Asset не назначен"); continue; }

                var so = new SerializedObject(rp);
                sb.AppendLine("  уровень качества «" + names[i] + "», URP Asset " + rp.name + ": HDR " + Read(so, "m_SupportsHDR") +
                              ", мягкие тени " + Read(so, "m_SoftShadowsSupported") + ", тени солнца " + Read(so, "m_MainLightShadowsSupported") +
                              ", каскадов " + Read(so, "m_ShadowCascadeCount") + ", дальность теней " + Read(so, "m_ShadowDistance") + " м");
            }
            var st = CloudCandyStyle.Current;
            if (st != null)
                sb.AppendLine("  цель по профилям (CloudCandyStyle): Mobile Low " + st.budgetMobileLow.shadowCascades + " каскад(а), " + st.budgetMobileLow.shadowDistance.ToString("0") + " м; " +
                              "Mobile High " + st.budgetMobileHigh.shadowCascades + ", " + st.budgetMobileHigh.shadowDistance.ToString("0") + " м; " +
                              "ПК " + st.budgetPc.shadowCascades + ", " + st.budgetPc.shadowDistance.ToString("0") + " м. Каскады в URP Asset скрипты не меняют: выставь руками.");
        }

        static string Read(SerializedObject so, string field)
        {
            var p = so.FindProperty(field);
            if (p == null) return "?";
            switch (p.propertyType)
            {
                case SerializedPropertyType.Boolean: return p.boolValue ? "да" : "НЕТ";
                case SerializedPropertyType.Integer: return p.intValue.ToString();
                case SerializedPropertyType.Float: return p.floatValue.ToString("0.#");
                default: return "?";
            }
        }
    }
}
