#if UNITY_EDITOR
// Assets/_Project/Editor/Island/LocationBuilder.cs
// Окно и запуск генератора. Unity 6000.3, URP. Меню: Tools > BuilderSim > Location Builder
//
// Данные (Settings, PadDef, палитра)  -> Builder/LocationSettings.cs
// Генератор (Gen, части)              -> Builder/LocationGen.*.cs, LocationGeo.cs
// Общие утилиты и модули              -> Common/*, Modules/*
//
// Внутренние единицы генератора ("u") = размеры «как у домов» при масштабе 1.0.
// Всё итоговое в мире = u * worldScale. Север = +Z, восток = +X.
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public partial class LocationBuilder : EditorWindow
{
    internal const string PrefsKey = "BuilderSim.LocationBuilder.Settings.v2";
    const string AssetDir = "Assets/_Game/Location";
    const string RootName = "Location";

    Settings _s = new Settings();
    Vector2 _scroll;
    GameObject _rootCache;
    // Лес живёт в этом же окне: настройки (ассет), их инспектор и последнее сообщение
    ForestSettings _forestSettings;
    Editor _forestSettingsEditor;
    string _forestLog = "";
    bool _fMain = true, _fModules = true, _fTerrain, _fRelief, _fTrail, _fCenter, _fForest, _fMeadow,
         _fPolish, _fColors, _fEnv, _fPads = true;

    [MenuItem("Tools/BuilderSim/Локация/Location Builder (окно)", false, 100)]
    static void Open()
    {
        var w = GetWindow<LocationBuilder>("Location Builder");
        w.minSize = new Vector2(420f, 600f);
        w.Show();
    }

    void OnEnable()
    {
        Load();
        SceneView.duringSceneGui += OnSceneGUI;
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        if (_forestSettingsEditor != null) { DestroyImmediate(_forestSettingsEditor); _forestSettingsEditor = null; }
        Save();
    }

    void Load()
    {
        _s = new Settings();
        string json = EditorPrefs.GetString(PrefsKey, "");
        if (!string.IsNullOrEmpty(json))
        {
            try { JsonUtility.FromJsonOverwrite(json, _s); }
            catch (Exception) { _s = new Settings(); }
        }
        _s.Validate();
    }

    void Save()
    {
        EditorPrefs.SetString(PrefsKey, JsonUtility.ToJson(_s));
    }

    // ------------------------------------------------------------------ окно

    static bool Fold(bool state, string title)
    {
        return EditorGUILayout.Foldout(state, title, true);
    }

    void OnGUI()
    {
        EditorGUI.BeginChangeCheck();
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        _fMain = Fold(_fMain, "Общее");
        if (_fMain)
        {
            EditorGUILayout.BeginHorizontal();
            _s.seed = EditorGUILayout.IntField("Seed", _s.seed);
            if (GUILayout.Button("Случайный", GUILayout.Width(80f))) _s.seed = UnityEngine.Random.Range(1, 999999);
            EditorGUILayout.EndHorizontal();
            _s.worldScale = EditorGUILayout.Slider("World Scale", _s.worldScale, 0.1f, 2f);
            _s.origin = EditorGUILayout.Vector3Field("Origin", _s.origin);
            _s.showHandles = EditorGUILayout.Toggle("Рамка и ручки в Scene", _s.showHandles);
            _s.setupLighting = EditorGUILayout.Toggle("Свет / ambient / skybox", _s.setupLighting);
            _s.useFog = EditorGUILayout.Toggle("Туман", _s.useFog);
            _s.rebuildIslandInWorld = EditorGUILayout.Toggle("Пересобирать парящий остров после рельефа", _s.rebuildIslandInWorld);
        }

        _fModules = Fold(_fModules, "Модули");
        if (_fModules)
        {
            _s.buildRiver = EditorGUILayout.Toggle("Река (вода, камыш, кувшинки)", _s.buildRiver);
            if (_s.buildRiver)
            {
                EditorGUI.indentLevel++;
                _s.hasBridge = EditorGUILayout.Toggle("Мост через реку", _s.hasBridge);
                if (_s.hasBridge)
                {
                    _s.bridgeStage = EditorGUILayout.IntSlider("Стадия моста (1 сломан .. 3 готов)", _s.bridgeStage, 1, 3);
                    if (GUILayout.Button("Переключить стадию в сцене (без пересборки)")) LocationRiver.SetStage(_s.bridgeStage);
                }
                _s.waterBarrier = EditorGUILayout.Toggle("Невидимая стенка над водой", _s.waterBarrier);
                EditorGUI.indentLevel--;
            }
            _s.buildCenter = EditorGUILayout.Toggle("Центр (площадь)", _s.buildCenter);
            _s.buildMeadow = EditorGUILayout.Toggle("Поляна (кармашки)", _s.buildMeadow);
            _s.polishEnabled = EditorGUILayout.Toggle("Доводка (трава, бабочки)", _s.polishEnabled);
        }

        _fTerrain = Fold(_fTerrain, "Terrain: размер, разрешение, старый шум");
        if (_fTerrain)
        {
            _s.terrainSize = EditorGUILayout.FloatField("Размер (u)", _s.terrainSize);
            _s.heightmapRes = EditorGUILayout.IntField("Heightmap res", _s.heightmapRes);
            _s.alphaRes = EditorGUILayout.IntField("Splatmap res", _s.alphaRes);
            _s.maxHeight = EditorGUILayout.FloatField("Макс. высота (u)", _s.maxHeight);
            _s.hillAmplitude = EditorGUILayout.FloatField("Амплитуда холмов", _s.hillAmplitude);
            _s.hillFrequency = EditorGUILayout.FloatField("Частота холмов", _s.hillFrequency);
            _s.hillClear = EditorGUILayout.FloatField("Холмы: ровная зона (u)", _s.hillClear);
            _s.hillFade = EditorGUILayout.FloatField("Холмы: нарастание (u)", _s.hillFade);
            _s.ringWidth = EditorGUILayout.FloatField("Ширина кольца гор", _s.ringWidth);
            _s.ringMin = EditorGUILayout.FloatField("Мин. высота кольца", _s.ringMin);
            _s.blurPasses = EditorGUILayout.IntSlider("Blur проходов", _s.blurPasses, 0, 8);
            _s.centerRadius = EditorGUILayout.FloatField("Радиус центра", _s.centerRadius);
            _s.minPadGap = EditorGUILayout.FloatField("Мин. зазор площадок", _s.minPadGap);
        }

        int presetClicked = -1;
        _fRelief = Fold(_fRelief, "Рельеф");
        if (_fRelief) DrawReliefSection(ref presetClicked);

        _fTrail = Fold(_fTrail, "Тропы");
        if (_fTrail)
        {
            _s.trailWidth = EditorGUILayout.FloatField("Ширина", _s.trailWidth);
            _s.trailWidthAtPad = EditorGUILayout.FloatField("Ширина у площадки", _s.trailWidthAtPad);
            _s.trailFlatWidth = EditorGUILayout.FloatField("Ширина выравнивания", _s.trailFlatWidth);
            _s.trailMeander = EditorGUILayout.FloatField("Изгибы (амплитуда)", _s.trailMeander);
        }

        DrawOutlineSection();
        _fCenter = Fold(_fCenter, "Центр");
        if (_fCenter)
        {
            _s.centerSeats = EditorGUILayout.Toggle("Лавки и брёвна", _s.centerSeats);
            _s.centerLamps = EditorGUILayout.Toggle("Фонари", _s.centerLamps);
            _s.centerGarland = EditorGUILayout.Toggle("Гирлянда", _s.centerGarland);
            _s.centerPlanters = EditorGUILayout.Toggle("Клумбы", _s.centerPlanters);
            _s.centerProps = EditorGUILayout.Toggle("Бочки, кэрн", _s.centerProps);
            _s.centerSign = EditorGUILayout.Toggle("Указатель", _s.centerSign);
            _s.centerDetail = EditorGUILayout.Slider("Детализация", _s.centerDetail, 0f, 2f);
        }

        _fForest = Fold(_fForest, "Лес (площадка)");
        if (_fForest)
        {
            _s.forestRelief = EditorGUILayout.FloatField("Рельеф глэйда ± (только «Старый»)", _s.forestRelief);
            _s.forestRidgeHeight = EditorGUILayout.FloatField("Гряда по краю (только «Старый»)", _s.forestRidgeHeight);
            _s.forestClutter = EditorGUILayout.Slider("Мусор в глэйде (пни, камни)", _s.forestClutter, 0f, 1.5f);
            EditorGUILayout.HelpBox("Лес управляется здесь же, вместе с рельефом, рекой и тропами. Генерация деревьев учитывает созданные зоны и фильтры размещения.", MessageType.None);
            if (_forestSettings == null) _forestSettings = ForestSettings.Load();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Создать зону леса")) RunForest("Зона", ForestZones.CreateMaster);
            if (GUILayout.Button("Создать подзоны")) RunForest("Подзоны", ForestZones.CreateSubZones);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Сгенерировать деревья", GUILayout.Height(28f))) RunForest("Деревья", () => _forestSettings.Generate(null));
            if (GUILayout.Button("Очистить деревья", GUILayout.Height(28f))) RunForest("Очистка", () => "Удалено деревьев: " + _forestSettings.Clear(null) + " (ручные объекты сохранены).");
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Построить тропы")) RunForest("Тропы", () => ForestTrails.Build(_forestSettings));
            if (GUILayout.Button("Тропы + пересборка леса")) RunForest("Тропы + лес", () => ForestTrails.BuildAndForest(_forestSettings));
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("Камера в лесу")) RunForest("Камера", ForestZones.CreateCameraZone);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("План троп (предпросмотр)")) RunForest("Предпросмотр", () => { ForestTrailPreview.Show(); return "План троп нарисован в Scene View."; });
            if (GUILayout.Button("Убрать предпросмотр")) RunForest("Предпросмотр", () => { ForestTrailPreview.Hide(); return "Предпросмотр убран."; });
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Скриншот сверху")) RunForest("Скриншот", () => ForestShots.Top(_forestSettings));
            if (GUILayout.Button("Скриншот игровой камерой")) RunForest("Скриншот", () => ForestShots.Game(_forestSettings));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("Настройки деревьев и биомов", EditorStyles.boldLabel);
            if (_forestSettingsEditor == null || _forestSettingsEditor.target != _forestSettings)
            {
                if (_forestSettingsEditor != null) DestroyImmediate(_forestSettingsEditor);
                _forestSettingsEditor = Editor.CreateEditor(_forestSettings);
            }
            if (_forestSettingsEditor != null)
            {
                EditorGUI.BeginChangeCheck();
                _forestSettingsEditor.OnInspectorGUI();
                if (EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(_forestSettings);
            }
            if (GUILayout.Button("Применить пресет видов")) RunForest("Виды", _forestSettings.ApplySpeciesPreset);
            if (!string.IsNullOrEmpty(_forestLog)) EditorGUILayout.HelpBox(_forestLog, MessageType.Info);
        }

        _fMeadow = Fold(_fMeadow, "Поляна");
        if (_fMeadow)
        {
            _s.meadowSpots = EditorGUILayout.IntSlider("Кармашков", _s.meadowSpots, 0, 14);
            _s.meadowFlowers = EditorGUILayout.Slider("Цветы", _s.meadowFlowers, 0f, 2f);
            _s.meadowLife = EditorGUILayout.Toggle("Искры у кармашков", _s.meadowLife);
        }

        _fPolish = Fold(_fPolish, "Доводка");
        if (_fPolish)
        {
            _s.polishGrass = EditorGUILayout.Slider("Трава", _s.polishGrass, 0f, 2f);
            _s.polishFlowers = EditorGUILayout.Slider("Цветы", _s.polishFlowers, 0f, 2f);
            _s.polishSway = EditorGUILayout.Slider("Качание", _s.polishSway, 0f, 0.4f);
            _s.polishButterflies = EditorGUILayout.IntSlider("Бабочки", _s.polishButterflies, 0, 30);
            _s.polishParticles = EditorGUILayout.Toggle("Частицы", _s.polishParticles);
            _s.polishFootDust = EditorGUILayout.Toggle("Пыль от шагов", _s.polishFootDust);
        }

        _fColors = Fold(_fColors, "Палитра");
        if (_fColors)
        {
            _s.grassA = EditorGUILayout.ColorField("Трава А", _s.grassA);
            _s.grassB = EditorGUILayout.ColorField("Трава Б", _s.grassB);
            _s.trailColor = EditorGUILayout.ColorField("Тропа", _s.trailColor);
            _s.trailDarkColor = EditorGUILayout.ColorField("Тропа (тёмная кромка)", _s.trailDarkColor);
            _s.padColor = EditorGUILayout.ColorField("Площадка", _s.padColor);
            _s.beachColor = EditorGUILayout.ColorField("Берег", _s.beachColor);
            _s.forestColor = EditorGUILayout.ColorField("Лес", _s.forestColor);
            _s.waterColor = EditorGUILayout.ColorField("Вода (запасной)", _s.waterColor);
            if (_s.envColors != null && _s.envColors.Length == MatNames.Length)
                for (int i = 0; i < MatNames.Length; i++)
                    _s.envColors[i] = EditorGUILayout.ColorField(MatNames[i], _s.envColors[i]);
            if (GUILayout.Button("Сбросить цвета окружения")) _s.envColors = Settings.DefaultEnvColors();
        }

        _fEnv = Fold(_fEnv, "Окружение");
        if (_fEnv)
        {
            _s.envDensity = EditorGUILayout.Slider("Плотность", _s.envDensity, 0f, 3f);
            _s.chunkSize = EditorGUILayout.FloatField("Размер чанка (u)", _s.chunkSize);
            _s.boundaryRocks = EditorGUILayout.Slider("Камни по границе", _s.boundaryRocks, 0f, 2f);
            _s.padLanterns = EditorGUILayout.Toggle("Фонарики у площадок", _s.padLanterns);
        }

        _fPads = Fold(_fPads, "Площадки (угол, расстояние, размер)");
        if (_fPads)
        {
            int remove = -1;
            for (int i = 0; i < _s.pads.Count; i++)
            {
                var p = _s.pads[i];
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                p.name = EditorGUILayout.TextField(p.name);
                p.gizmoColor = EditorGUILayout.ColorField(GUIContent.none, p.gizmoColor, false, false, false, GUILayout.Width(46f));
                if (GUILayout.Button("X", GUILayout.Width(22f))) remove = i;
                EditorGUILayout.EndHorizontal();
                p.angle = EditorGUILayout.FloatField("Угол (° от севера)", p.angle);
                p.distance = EditorGUILayout.FloatField("Расстояние (u)", p.distance);
                p.round = EditorGUILayout.Toggle("Круг (size.x = радиус)", p.round);
                p.size = EditorGUILayout.Vector2Field(p.round ? "Радиус" : "Ширина / Глубина", p.size);
                p.parent = EditorGUILayout.TextField("Родитель (пусто = от центра)", p.parent);
                p.straight = EditorGUILayout.Toggle("Прямая тропа", p.straight);
                if (!string.IsNullOrEmpty(p.parent)) p.forkDistance = EditorGUILayout.FloatField("Развилка (u от центра)", p.forkDistance);
                EditorGUILayout.EndVertical();
            }
            if (remove >= 0) _s.pads.RemoveAt(remove);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ Площадка")) _s.pads.Add(new PadDef("Pad" + _s.pads.Count, 0f, 20f, 6f, 6f, Color.white));
            if (GUILayout.Button("Сбросить раскладку")) _s.pads = Settings.DefaultPads();
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
        if (presetClicked >= 0)
        {
            // применяем после прохода по окну: набор полей в секции зависит от стиля, менять его посреди OnGUI нельзя
            if (presetClicked == 2) _s.ApplyScenicPreset(); else _s.ApplyReliefPreset(presetClicked);
            _s.Validate();
            Save();
            GUI.FocusControl(null);
            Repaint();
        }
        if (EditorGUI.EndChangeCheck())
        {
            Save();
            SceneView.RepaintAll();
        }

        EditorGUILayout.Space(4f);
        if (GUILayout.Button("Сгенерировать весь мир", GUILayout.Height(38f))) BuildWorld();
        EditorGUILayout.LabelField("Рельеф → модули локации → парящий остров → зоны леса → тропы → деревья.", EditorStyles.miniLabel);
        if (GUILayout.Button("Только локация (без пересборки леса)", GUILayout.Height(26f))) Build();
        if (GUILayout.Button("Указатели и названия зон (на готовой локации)")) LocationSigns.BuildExisting();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Удалить из сцены")) { RemoveRoot(); _rootCache = null; SceneView.RepaintAll(); }
        if (GUILayout.Button("Показать обзор")) FrameView(_s);
        EditorGUILayout.EndHorizontal();
    }

    // ------------------------------------------------------------------ секция «Рельеф»

    static void Sub(string title)
    {
        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
    }

    void DrawReliefSection(ref int presetClicked)
    {
        EditorGUI.indentLevel++;
        var prev = GUI.backgroundColor;
        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = _s.reliefStyle == Settings.ReliefOld ? new Color(0.6f, 1f, 0.6f) : prev;
        if (GUILayout.Button("Пресет: Старый", GUILayout.Height(24f))) presetClicked = Settings.ReliefOld;
        GUI.backgroundColor = _s.reliefStyle == Settings.ReliefExpressive ? new Color(0.6f, 1f, 0.6f) : prev;
        if (GUILayout.Button("Пресет: Выразительный", GUILayout.Height(24f))) presetClicked = Settings.ReliefExpressive;
        if (GUILayout.Button("Сценичный", GUILayout.Height(24f))) presetClicked = 2;
        GUI.backgroundColor = prev;
        EditorGUILayout.EndHorizontal();

        if (_s.reliefStyle == Settings.ReliefOld)
            EditorGUILayout.HelpBox("Сейчас «Старый» рельеф: один шум (параметры в секции Terrain). «Выразительный» собирает большие формы: массив леса, холмы и лощины, чашу острова, долину реки, тропы по склону. Нажми пресет и пересобери локацию. Параметры ниже сохраняются и в старом режиме.", MessageType.Info);
        else
            EditorGUILayout.HelpBox("«Выразительный» рельеф. Площадки и центр остаются ровными на Y = 0, ничего не двигается. Единицы u (1 u = World Scale м).", MessageType.None);

        EditorGUI.BeginDisabledGroup(_s.reliefStyle == Settings.ReliefOld);

        Sub("Чаша и кромка острова");
        _s.rimFollowIsland = EditorGUILayout.Toggle("Кромка по контуру острова", _s.rimFollowIsland);
        _s.rimHeight = EditorGUILayout.Slider("Высота кромки", _s.rimHeight, 0f, 8f);
        _s.rimWidth = EditorGUILayout.Slider("Ширина кромки", _s.rimWidth, 4f, 40f);
        _s.rimGateClear = EditorGUILayout.Slider("Свободно у ворот", _s.rimGateClear, 0f, 30f);
        _s.bowlHeight = EditorGUILayout.Slider("Подъём к краю (чаша)", _s.bowlHeight, 0f, 4f);

        Sub("Луга: холмы и лощины");
        _s.meadowSwell = EditorGUILayout.Slider("Высота холмов", _s.meadowSwell, 0f, 6f);
        _s.meadowSwellScale = EditorGUILayout.Slider("Частота (меньше = крупнее)", _s.meadowSwellScale, 0.006f, 0.04f);
        _s.meadowSwellFlat = EditorGUILayout.Slider("Доля ровных лощин", _s.meadowSwellFlat, 0f, 0.9f);

        Sub("Лесной массив");
        _s.forestTier1 = EditorGUILayout.Slider("Ярус 1: высота", _s.forestTier1, 0f, 10f);
        _s.forestTier2 = EditorGUILayout.Slider("Ярус 2: высота (= ярус 1: один ярус)", _s.forestTier2, 0f, 14f);
        _s.forestSlope = EditorGUILayout.Slider("Склоны массива: ширина", _s.forestSlope, 8f, 50f);
        _s.forestTierSlope = EditorGUILayout.Slider("Уступ между ярусами", _s.forestTierSlope, 4f, 40f);
        _s.forestGladeRelief = EditorGUILayout.Slider("Бугры на массиве", _s.forestGladeRelief, 0f, 2f);

        Sub("Озеро");
        _s.lakeEnabled = EditorGUILayout.Toggle("Чаша озера", _s.lakeEnabled);
        if (_s.lakeEnabled)
        {
            EditorGUI.indentLevel++;
            _s.lakePos = EditorGUILayout.Vector2Field("Центр (x, z), u", _s.lakePos);
            _s.lakeRadius = EditorGUILayout.Slider("Радиус чаши", _s.lakeRadius, 3f, 30f);
            _s.lakeShore = EditorGUILayout.Slider("Берег", _s.lakeShore, 2f, 20f);
            _s.lakeDepth = EditorGUILayout.Slider("Глубина", _s.lakeDepth, 0.3f, 3f);
            _s.lakeWaterFrac = EditorGUILayout.Slider("Водный радиус (доля чаши)", _s.lakeWaterFrac, 0.3f, 0.85f);
            EditorGUILayout.HelpBox("Это только рельеф: чаша, мелкий берег и ровное место под Pond. Воду, пирс и лодку делают позже отдельным модулем: уровень воды Y = " + _s.LakeWaterY.ToString("0.00") + " u, водный радиус " + (_s.lakeRadius * _s.lakeWaterFrac).ToString("0.0") + " u (внутри земля везде ниже воды). Чаша не должна касаться площадок, троп и реки: при пересечении будет предупреждение в Console.", MessageType.None);
            EditorGUI.indentLevel--;
        }

        Sub("Тропы и площадки на склоне");
        _s.trailMaxGrade = EditorGUILayout.Slider("Макс. уклон тропы", _s.trailMaxGrade, 0.1f, 0.5f);
        _s.trailGradeSmooth = EditorGUILayout.Slider("Сглаживание профиля тропы (u)", _s.trailGradeSmooth, 8f, 60f);
        _s.trailBank = EditorGUILayout.Slider("Откос от тропы (u)", _s.trailBank, 3f, 20f);
        _s.padSkirt = EditorGUILayout.Slider("Переход площадки в склон (u)", _s.padSkirt, 3f, 20f);

        Sub("Раскраска");
        _s.paintCrest = EditorGUILayout.Slider("Светлая трава на гребнях", _s.paintCrest, 0f, 1.5f);
        _s.paintWet = EditorGUILayout.Slider("Влажный оттенок в низинах", _s.paintWet, 0f, 1.5f);
        _s.paintSoilSlope = EditorGUILayout.Slider("Земля с уклона", _s.paintSoilSlope, 0.1f, 1f);
        _s.paintRockSlope = EditorGUILayout.Slider("Скала с уклона", _s.paintRockSlope, 0.2f, 1.5f);
        _s.rockColor = EditorGUILayout.ColorField("Цвет: скала", _s.rockColor);
        _s.soilColor = EditorGUILayout.ColorField("Цвет: тёмная земля", _s.soilColor);
        _s.crestColor = EditorGUILayout.ColorField("Цвет: гребень", _s.crestColor);
        _s.wetColor = EditorGUILayout.ColorField("Цвет: низина", _s.wetColor);

        EditorGUI.EndDisabledGroup();

        Sub("Детализация и память");
        float stepCm = _s.HeightStepCm();
        EditorGUILayout.LabelField("Шаг heightmap", stepCm.ToString("0.0") + " см  (res " + _s.heightmapRes + ")");
        EditorGUILayout.LabelField("Heightmap (16 бит)", _s.HeightmapMB().ToString("0.0") + " МБ");
        EditorGUILayout.LabelField("Splat + basemap (" + _s.SplatLayers() + " слоёв)", "~" + _s.SplatMB().ToString("0.0") + " МБ");
        EditorGUILayout.HelpBox(stepCm <= 20f
            ? "Хватает: самая мелкая форма рельефа (откос тропы, переход площадки) шире 4 м, это десятки клеток. 1025 даст шаг вдвое мельче, но heightmap и коллайдер вырастут в 4 раза, а картинка сверху не изменится."
            : "Шаг больше 20 см: края площадок и откосы троп будут ступенчатыми. Подними Heightmap res в секции Terrain до 513 или 1025.",
            stepCm <= 20f ? MessageType.Info : MessageType.Warning);
        EditorGUI.indentLevel--;
    }

    // ------------------------------------------------------------------ действия

    // Полный сценарий генерации. Отдельные генераторы остаются модулями,
    // но запускаются в согласованном порядке из одного окна.
    void BuildWorld()
    {
        if (Application.isPlaying)
        {
            _forestLog = "Во время Play генерация недоступна.";
            return;
        }

        _s.Validate();
        Save();
        var report = new List<string>();

        try
        {
            EditorUtility.DisplayProgressBar("World Generator", "1/4: рельеф и локация", 0.05f);
            var g = new Gen(_s);
            g.Run();
            _rootCache = null;
            FrameView(_s);

            // Gen.Run завершает основную локацию и её модули до создания леса.
            if (FindRoot() == null)
                throw new InvalidOperationException("Основная локация не создана. Проверь Console и ошибки генерации.");

            RebuildIsland(report, 0.66f);

            if (_forestSettings == null) _forestSettings = ForestSettings.Load();

            EditorUtility.DisplayProgressBar("World Generator", "2/4: основная зона леса", 0.72f);
            report.Add(ForestZones.CreateMaster());
            if (ForestZones.Find(ForestBiome.Forest) == null)
                throw new InvalidOperationException("Не удалось найти основную зону леса. " + string.Join(" ", report.ToArray()));

            EditorUtility.DisplayProgressBar("World Generator", "3/4: подзоны и тропы", 0.80f);
            report.Add(ForestZones.CreateSubZones());

            EditorUtility.DisplayProgressBar("World Generator", "4/4: деревья", 0.90f);
            string forestResult = ForestTrails.BuildAndForest(_forestSettings);
            report.Add(forestResult);
            _forestLog = "Генерация мира завершена.\n" + string.Join("\n", report.ToArray());

            Debug.Log("[WorldGenerator] " + _forestLog);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
        catch (Exception e)
        {
            _forestLog = "Генерация остановлена: " + e.Message + " Подробности в Console.";
            Debug.LogException(e);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            SceneView.RepaintAll();
            Repaint();
        }
    }

    void RunForest(string title, Func<string> action)
    {
        if (Application.isPlaying) { _forestLog = "Во время Play генерация недоступна."; return; }
        try { _forestLog = action(); }
        catch (Exception e) { Debug.LogException(e); _forestLog = title + ": ошибка, подробности в Console."; }
        SceneView.RepaintAll();
        Repaint();
    }

    void Build()
    {
        _s.Validate();
        Save();
        try
        {
            var g = new Gen(_s);
            g.Run();
            _rootCache = null;
            FrameView(_s);
            RebuildIsland(null, 0.9f);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    // Парящий остров читает контур из своих точек PathPoints и режет дырки в Terrain. Terrain пересоздан, значит остров надо собрать заново.
    void RebuildIsland(List<string> report, float progress)
    {
        if (!_s.rebuildIslandInWorld) return;
        var fi = UnityEngine.Object.FindFirstObjectByType<FloatingIsland>();
        if (fi == null) return;
        EditorUtility.DisplayProgressBar("World Generator", "Парящий остров: дырки в Terrain, обрыв, водопад", progress);
        fi.Build();
        if (report != null) report.Add("Парящий остров пересобран.");
    }

    static GameObject FindRoot()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var sc = SceneManager.GetSceneAt(i);
            if (!sc.isLoaded) continue;
            foreach (var go in sc.GetRootGameObjects())
                if (go.name == RootName) return go;
        }
        return null;
    }

    static void RemoveRoot()
    {
        GameObject r;
        while ((r = FindRoot()) != null)
            Undo.DestroyObjectImmediate(r);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    static void FrameView(Settings s)
    {
        var sv = SceneView.lastActiveSceneView;
        if (sv == null) return;
        sv.LookAt(s.origin, Quaternion.Euler(50f, 20f, 0f), s.terrainSize * s.worldScale * 0.55f, false, false);
    }

    // ------------------------------------------------------------------ Scene View

    void OnSceneGUI(SceneView sv)
    {
        float ws = _s.worldScale;
        float S = _s.terrainSize * ws;
        Vector3 o = _s.origin;

        if (_s.showHandles)
        {
            Handles.color = new Color(1f, 0.85f, 0.2f, 0.95f);
            Vector3 a = o + new Vector3(-S * 0.5f, 0f, -S * 0.5f);
            Vector3 b = o + new Vector3(S * 0.5f, 0f, -S * 0.5f);
            Vector3 c = o + new Vector3(S * 0.5f, 0f, S * 0.5f);
            Vector3 d = o + new Vector3(-S * 0.5f, 0f, S * 0.5f);
            Handles.DrawAAPolyLine(3f, a, b, c, d, a);
            Handles.Label(d, "Location  " + S.ToString("0.#") + " m  (scale " + ws.ToString("0.##") + ")");

            EditorGUI.BeginChangeCheck();
            Vector3 np = Handles.PositionHandle(o, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                _s.origin = np;
                Save();
                Repaint();
            }

            Vector3 ep = o + new Vector3(S * 0.5f, 0f, 0f);
            EditorGUI.BeginChangeCheck();
            Vector3 ne = Handles.Slider(ep, Vector3.right, HandleUtility.GetHandleSize(ep) * 0.8f, Handles.ConeHandleCap, 0f);
            if (EditorGUI.EndChangeCheck())
            {
                float newHalf = ne.x - o.x;
                _s.worldScale = Mathf.Clamp(Mathf.Round(newHalf * 2f / _s.terrainSize * 20f) / 20f, 0.1f, 4f);
                Save();
                Repaint();
            }

            Quaternion vr = Quaternion.Euler(50f, 20f, 0f);
            Vector3 cam = o + vr * new Vector3(0f, 0f, -S * 1.15f);
            Handles.color = new Color(0.4f, 0.9f, 1f, 0.9f);
            Handles.DrawLine(cam, o);
            Handles.DrawWireDisc(cam, vr * Vector3.forward, S * 0.03f);
            Handles.Label(cam, "Рекомендуемый обзор (50°)");
        }

        if (_rootCache == null) _rootCache = FindRoot();
        if (_rootCache == null) return;
        var rt = _rootCache.transform;

        var padsT = rt.Find("Pads");
        if (padsT != null)
        {
            foreach (var d in _s.pads)
            {
                var t = padsT.Find("PAD_" + d.name);
                if (t == null) continue;
                using (new Handles.DrawingScope(d.gizmoColor, Matrix4x4.TRS(t.position, t.rotation, Vector3.one)))
                {
                    if (d.round)
                    {
                        Handles.DrawWireDisc(new Vector3(0f, 0.05f, 0f), Vector3.up, d.size.x * ws);
                    }
                    else
                    {
                        float w = d.size.x * ws * 0.5f, h = d.size.y * ws * 0.5f;
                        Handles.DrawAAPolyLine(3f,
                            new Vector3(-w, 0.05f, -h), new Vector3(-w, 0.05f, h), new Vector3(w, 0.05f, h),
                            new Vector3(w, 0.05f, -h), new Vector3(-w, 0.05f, -h));
                        Handles.DrawLine(new Vector3(0f, 0.05f, 0f), new Vector3(0f, 0.05f, h * 1.3f));
                    }
                }
                Handles.color = d.gizmoColor;
                Handles.Label(t.position + Vector3.up * 0.6f, "PAD_" + d.name);
            }
        }

        var markers = rt.Find("Markers");
        if (markers != null)
        {
            Handles.color = new Color(1f, 1f, 1f, 0.9f);
            foreach (Transform m in markers)
            {
                Handles.DrawWireDisc(m.position, Vector3.up, 0.5f * ws + 0.3f);
                Handles.DrawLine(m.position, m.position + m.forward * (1.2f * ws + 0.5f));
                Handles.Label(m.position + Vector3.up * 0.8f, m.name);
            }
        }
    }
}
#endif
