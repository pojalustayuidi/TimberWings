#if UNITY_EDITOR
// Assets/_Project/Editor/Forest/ForestTool.cs (Editor)
// Единственное окно леса: Tools → Forest → Лес.
//   1. Создать зону леса     2. Добавить подзоны     3. Создать деревья (карта плотности или старые рощицы)
// Остальное (расстановка вручную) делается обычными объектами: бросай их в ForestTrees/Manual, генератор их не удаляет.
using UnityEditor;
using UnityEngine;

public class ForestTool : EditorWindow
{
    ForestSettings settings;
    Editor settingsEditor;
    Vector2 scroll;
    bool showSettings;
    string log = "";

    [MenuItem("Tools/Forest/Лес")]
    static void Open() { var w = GetWindow<ForestTool>("Лес"); w.minSize = new Vector2(340f, 420f); }

    void OnEnable() { settings = ForestSettings.Load(); }

    void Run(string title, System.Func<string> action)
    {
        if (Application.isPlaying) { log = "Во время Play лес не меняется: выйди из Play."; return; }
        try { log = action(); }
        catch (System.Exception e) { Debug.LogException(e); log = title + ": ошибка, см. Console."; }
        SceneView.RepaintAll();
        Repaint();
    }

    void OnGUI()
    {
        if (settings == null) settings = ForestSettings.Load();
        scroll = EditorGUILayout.BeginScrollView(scroll);

        var master = ForestZones.Find(ForestBiome.Forest);
        int trees = 0;
        foreach (var b in ForestSettings.Biomes) trees += settings.CountTrees(b);
        EditorGUILayout.HelpBox(
            "Зона леса: " + (master != null ? "«" + master.Map.zoneName + "»" : "нет") +
            "\nПодзоны: " + CountSub() + " из 3\nДеревьев сгенерировано: " + trees, MessageType.None);

        GUILayout.Space(4f);
        if (GUILayout.Button("1. Создать зону леса", GUILayout.Height(30f)))
            Run("Зона", ForestZones.CreateMaster);
        EditorGUILayout.LabelField("Берёт выделенную MapZone, иначе площадку леса из LocationBuilder, иначе создаёт круг.", EditorStyles.miniLabel);

        if (GUILayout.Button("2. Добавить подзоны (кедр, берёза, осень)", GUILayout.Height(30f)))
            Run("Подзоны", ForestZones.CreateSubZones);

        if (GUILayout.Button("3. Создать деревья", GUILayout.Height(34f)))
            Run("Деревья", () => settings.Generate(null));

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Очистить деревья"))
            Run("Очистка", () => "Удалено деревьев: " + settings.Clear(null) + " (Manual не тронут).");
        if (GUILayout.Button("Камера в лесу"))
            Run("Камера", ForestZones.CreateCameraZone);
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("4. Декор леса: арка, фонари, пропсы, свет", GUILayout.Height(30f)))
            EditorApplication.ExecuteMenuItem("Tools/Forest/Декор леса");
        EditorGUILayout.LabelField("Ставит декор по тропам и полянам. Сначала деревья и тропы.", EditorStyles.miniLabel);

        if (settings.useDensityMap)
        {
            GUILayout.Space(6f);
            EditorGUILayout.LabelField("Маски плотности (круги, радиус = масштаб/2)", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ Чаща")) Run("Маска", () => ForestMaskTools.CreateMarker(ForestMaskKind.Thicket, 14f));
            if (GUILayout.Button("+ Редколесье")) Run("Маска", () => ForestMaskTools.CreateMarker(ForestMaskKind.Sparse, 14f));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ Поляна")) Run("Маска", () => ForestMaskTools.CreateMarker(ForestMaskKind.Clearing, 8f));
            if (GUILayout.Button("+ Опушка")) Run("Маска", () => ForestMaskTools.CreateMarker(ForestMaskKind.Fringe, 10f));
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("Лесопилка: вырез в центр Scene View"))
                Run("Лесопилка", () =>
                {
                    var sv = SceneView.lastActiveSceneView;
                    if (sv == null) return "Нет окна Scene.";
                    Undo.RecordObject(settings, "Sawmill clearing");
                    settings.sawmillEnabled = true;
                    settings.sawmillCenter = new Vector2(sv.pivot.x, sv.pivot.z);
                    EditorUtility.SetDirty(settings);
                    return "Поляна лесопилки: центр (" + sv.pivot.x.ToString("0.#") + "; " + sv.pivot.z.ToString("0.#") + "), радиус " + settings.sawmillRadius + " м.";
                });

            GUILayout.Space(6f);
            EditorGUILayout.LabelField("Тропы (ground_pathOpen_*, под корнем ForestTrails)", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Главная от арки к лесопилке, ответвления, тупиковая поляна, обочины, маски.", EditorStyles.miniLabel);
            if (GUILayout.Button("План (предпросмотр в Scene View)"))
                EditorApplication.ExecuteMenuItem("Tools/Forest/Тропы: план (предпросмотр)");
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Тропы", GUILayout.Height(26f))) Run("Тропы", () => ForestTrails.Build(settings));
            if (GUILayout.Button("Убрать тропы", GUILayout.Height(26f))) Run("Тропы", ForestTrails.Remove);
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("Тропы + пересобрать лес", GUILayout.Height(30f)))
                Run("Тропы + лес", () => ForestTrails.BuildAndForest(settings));
            if (GUILayout.Button("Скриншоты: сверху + от арки + у лесопилки", GUILayout.Height(26f)))
                Run("Скриншоты", () => ForestTrails.Shots(settings));

            GUILayout.Space(6f);
            EditorGUILayout.LabelField("Результат: скриншоты (Screenshots/Forest)", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Сверху", GUILayout.Height(26f))) Run("Скриншот", () => ForestShots.Top(settings));
            if (GUILayout.Button("Игровая камера", GUILayout.Height(26f))) Run("Скриншот", () => ForestShots.Game(settings));
            EditorGUILayout.EndHorizontal();
        }

        GUILayout.Space(6f);
        EditorGUILayout.LabelField("Вручную", EditorStyles.boldLabel);
        if (GUILayout.Button("Выделенное → ForestTrees/Manual"))
            Run("Manual", ForestZones.SelectionToManual);

        GUILayout.Space(6f);
        EditorGUILayout.LabelField("Миграция", EditorStyles.boldLabel);
        if (GUILayout.Button("Старый лес → перенести деревья в ForestTrees"))
            Run("Миграция", ForestZones.MigrateOld);

        GUILayout.Space(6f);
        showSettings = EditorGUILayout.Foldout(showSettings, "Настройки генерации", true);
        if (showSettings)
        {
            if (settingsEditor == null || settingsEditor.target != settings) settingsEditor = Editor.CreateEditor(settings);
            EditorGUI.BeginChangeCheck();
            settingsEditor.OnInspectorGUI();
            if (EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(settings);
            if (GUILayout.Button("Состав видов по пресету")) Run("Виды", settings.ApplySpeciesPreset);
        }

        if (!string.IsNullOrEmpty(log))
        {
            GUILayout.Space(6f);
            EditorGUILayout.HelpBox(log, MessageType.Info);
        }
        EditorGUILayout.EndScrollView();
    }

    static int CountSub()
    {
        int n = 0;
        if (ForestZones.Find(ForestBiome.Cedar) != null) n++;
        if (ForestZones.Find(ForestBiome.Birch) != null) n++;
        if (ForestZones.Find(ForestBiome.Autumn) != null) n++;
        return n;
    }

    void OnDisable() { if (settingsEditor != null) DestroyImmediate(settingsEditor); }
}
#endif
