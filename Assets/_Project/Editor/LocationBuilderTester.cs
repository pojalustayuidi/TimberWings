#if UNITY_EDITOR
// Assets/_Project/Editor/Terrain/LocationBuilderTester.cs
// ОТДЕЛЬНЫЙ генератор острова «с нуля». Не трогает LocationBuilder и его модули (лес, центр, поляна, скалы, FloatingIsland).
// Меню: Tools > BuilderSim > Локация > Location Builder TESTER
//
// Что делает (всё процедурно, по seed):
//   1. Контур острова: скруглённый квадрат + бухты и мысы + «балкон» под смотровую на западе.
//   2. Рельеф (единицы u, 1 u = worldScale м, как в LocationBuilder): луга, чаша, кромка с ровной полосой,
//      лесной массив (2 яруса) у площадки Forest, яма под пруд, СКАЛЬНАЯ ГОРА с водопадом, мелкая широкая река.
//   3. ГОРА: в северо-западном углу отдельный массив 20 x 25 u, высота 12-16 u. В плане выпуклый многогранник (широкие плоские
//      грани, серо-фиолетовые), 2-3 уступа-пласта с травяными полками и мхом, сверху плато с травой, кустами и ёлками,
//      на фасаде (юго-восток) выемка-ниша 3-4 u, из неё одной лентой падает вода на 10-12 u в неглубокую чашу.
//      У подножия осыпь с валунами, пенный круг и брызги. На плато ведёт пологая тропа по краю горы.
//   4. РЕКА: от чаши у подножия горы вдоль западного края острова на юг, широкая (8-10 u), мелкая (0.3-0.8 u), голубо-бирюзовая,
//      русло слегка извилистое, берега пологие (травяной откос, песчаные отмели, галечные косы), много круглых валунов,
//      2-3 островка с травой и деревцем, камыш и кусты по берегам. На краю острова вода падает в пустоту.
//   5. ЗАЩИТА ОТ ОБХОДА: от горы до ближайшего края острова идёт скальный хребет; гора, хребет и река делят остров надвое.
//      Поверх: невидимые стенки над водой (проход под мост оставлен), по хребту и вокруг подножия горы (кроме входа на тропу),
//      потому что игрок двигается через Rigidbody без лимита уклона. После сборки рельеф проверяется поиском пути (CheckBypass).
//   6. Тропы, центр, площадки берутся из LocationBuilder.Placement / Settings.pads и строятся ТЕМ ЖЕ алгоритмом
//      с тем же seed и тем же порядком rng, поэтому формы троп совпадают со старой локацией. Тропа на гору строится после всех
//      остальных (ответвление от тропы к воротам) и не пересекает ни реку, ни другие тропы.
//   7. МУЛЬТЯШНЫЙ СТИЛЬ: бесшовные процедурные текстуры 8 слоёв, яркие плоские цвета (трава с пучками и цветами, тропа с
//      крупной галькой, земля, скала с гранями и трещинами, лесная подстилка, песок, бумага) + шейдер BuilderSim/LocationCardboard (три тона света с фиолетовыми
//      тенями, зерно бумаги, линии между листами картона по высоте, контровой свет) + вода BuilderSim/LocationToonWater.
//      Terrain остаётся коллайдером, а рисует остров меш Surface (шейдер читает те же карты раскраски).
//      Шейдеры и PNG пишутся сами в Assets/_Game/LocationTest/{Shaders,Layers}.
//   8. Низ острова: каменная «юбка» (парящий остров) с тем же картонным шейдером, травяной кант по краю.
//   9. Маркеры: площадки, мост через реку, ворота на север (мост на след. локацию).
//  10. СМОТРОВАЯ ЗА МОСТОМ: деревянный балкон на западной кромке. Настил из досок выступает за край острова на 2-3 u,
//      снизу видны балки и косые стойки, упирающиеся в скалу. Перила (стойки + две жерди) с проёмом к тропе, 2-3 ступени у входа,
//      скамейка, подзорная труба на стойке, флажок, фонари и гирлянда. Тропа от моста плавно поднимается на lookoutHeight.
//      Всё в картонном стиле: MakeCardMat, дерево = tex[3] с тёплым тинтом. Свои коллайдеры (настил, ступени, перила).
// Результат: объект «LocationTest» в сцене + ассеты в Assets/_Game/LocationTest.
//
// Зависимости от проекта: только LocationBuilder.Placement/Settings/PadDef/ZonePos (public) и LocationBuilder.PrefsKey (internal).
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public partial class LocationBuilderTester : EditorWindow
{
    const string PrefsKey = "BuilderSim.LocationBuilderTester.v6";
    const string PrefsKeyOld4 = "BuilderSim.LocationBuilderTester.v5";
    const string PrefsKeyOld3 = "BuilderSim.LocationBuilderTester.v4";
    const string PrefsKeyOld = "BuilderSim.LocationBuilderTester.v3";
    const string PrefsKeyOld2 = "BuilderSim.LocationBuilderTester.v2";
    const string PrefsKeyOld1 = "BuilderSim.LocationBuilderTester.v1";
    const string AssetDir = "Assets/_Game/LocationTest";
    const string RootName = "LocationTest";

    // =====================================================================================================
    //  НАСТРОЙКИ
    // =====================================================================================================

    [Serializable]
    public class Cfg
    {
        public int seed = 1337;
        public float worldScale = 0.5f;
        public Vector3 origin = Vector3.zero;
        public int heightmapRes = 513;           // 513 быстро, 1025 чётче грани горы
        public int alphaRes = 1024;
        public bool autoTerrainSize = true;      // размер Terrain подбирается под контур
        public float terrainSize = 190f;

        // контур
        public float islandRadius = 80f;         // полуразмер «квадрата» по осям, u
        public float roundness = 3.4f;           // 2 = круг, 3.4 = скруглённый квадрат, 6 = почти квадрат
        public float bayDepth = 5f;              // глубина бухт, u
        public float capeSize = 3f;              // мелкие мысы, u
        public float outlineNoise = 1.6f;        // частота изгибов контура
        public float lookoutCape = 5f;           // выступ-балкон на запад под смотровую

        // кромка
        public float rimHeight = 2.6f;
        public float rimWidth = 14f;
        public float rimFlat = 2.2f;             // ровная травяная полоса у самого края
        public float gateClear = 14f;            // вокруг ворот кромка снижается
        public float bowlHeight = 1.2f;

        // низ острова
        public bool underside = true;
        public float underDepth = 42f;
        public float underTaper = 0.9f;          // 0.9 = дно в 10% от размера
        public float underRough = 0.16f;
        public int underSegs = 240;
        public int underRings = 14;

        // луга
        public float meadowSwell = 2.4f;
        public float meadowScale = 0.014f;
        public float meadowFlat = 0.45f;
        public float centerRadius = 8f;

        // лес (площадка Forest)
        public float forestTier1 = 4.5f;
        public float forestTier2 = 8.5f;
        public float forestSlope = 24f;
        public float forestTierSlope = 16f;
        public float forestRelief = 0.4f;

        // пруд (Lake из LocationBuilder.Placement)
        public Vector2 lakePos = LocationBuilder.ZonePos("Lake");
        public float lakeRadius = 11f;
        public float lakeShore = 7f;
        public float lakeDepth = 3.6f;          // глубина ямы под пруд, u

        // река (синяя зона): от чаши у подножия горы вдоль западного края острова на юг, на краю острова падает в пустоту
        public bool river = true;
        public float riverWidth = 9f;            // ширина воды, u (8-10)
        public float depthValley = 3.4f;         // глубина русла, u (1 u = worldScale м; 3.4 u при 0.5 = около 1.7 м: можно плыть)
        public float waterValley = -0.6f;        // уровень воды у подножия горы, u (дальше река плавно опускается к краю)
        public float valleyWall = 4.6f;          // откос: ширина от воды до луга, u (меньше = круче берег; при глубоком русле держи ~1.4 x глубины)
        public float valleyWidth = 16f;          // на сколько формы рельефа затухают к реке
        public float riverMeander = 3.5f;        // извилистость русла, u
        public float widthVar = 0.2f;            // плёсы и перекаты
        public float bridgeGap = 4f;             // от края площадки Bridge до воды
        public float bridgeReach = 12f;          // прямой участок реки у моста
        public bool bridgeBuild = true;          // деревянный мост (false = маркер-куб)
        public float bridgeWidth = 3f;           // ширина настила моста, u (2.6-3)
        public float bridgeArch = 0.5f;          // подъём настила в середине, u (0.4-0.6)
        public bool bridgePosts = true;          // столбики у входов на мост
        public bool bridgeLamps = true;          // фонари на столбиках
        public float fallLength = 18f;           // длина водопада с края острова
        public int islets = 0;                   // островки с травой и деревцем посреди русла (0 = нет)
        public bool pebbleBars = false;          // галечные косы (мели-островки у берега)
        public int riverLookVer = 0;             // версия вида реки: при загрузке старых настроек один раз ставит глубокое русло без островков
        public float boulderDensity = 1f;        // густота валунов в русле
        public float riverFlowSpeed = 1f;        // скорость течения: ленты и полосы в воде (0.2-3)
        public float riverFoamAmount = 1f;       // пена: берег, вокруг камней, перекаты (0 = нет, 2 = много)
        public bool riverLogs = true;            // 2-3 упавших бревна у воды
        public bool riverMist = true;            // брызги и туман под водопадом с края острова
        public bool riverFish = true;            // рыбы-силуэты в воде у островков
        public float riverSparkle = 0.6f;        // солнечные блики на воде (0 = нет, 1 = много)
        public bool riverReeds = true;           // камыш и рогоз у кромки воды
        public bool riverLife = true;            // утиные семьи, стрекозы, бабочки
        public bool riverJetty = true;           // рыбацкие мостки с ведром и удочкой
        public bool rocks = true;                // валуны (русло, подножие горы, берега, край ямы)
        public bool props = true;                // ёлки, кусты, камыш, деревца
        public bool buildPond = false;           // пруд: вода, остров, мостик, пирс (по умолчанию выключен, яма сухая)
        public bool pondIsland = false;          // остров посреди пруда (по умолчанию нет)
        public bool pondBridge = false;          // мостик к острову (нужен остров)
        public float pondIslandRadius = 2.8f;    // радиус острова посреди пруда, u (2.5-3)
        public int pondLilies = 14;              // кувшинки на воде
        public float pondDeepen = 1.8f;          // во сколько раз глубже дно под водой (урез не меняется), 1-3
        public float pondPierLength = 5.5f;      // на сколько пирс выступает в воду, u (5-6)

        // скальная гора (северо-запад) и её водопад
        public Vector2 mountainPos = new Vector2(-58f, 54f);   // центр горы, u (север = +z)
        public Vector2 mountainSize = new Vector2(20f, 25f);   // ширина (x) и длина (z), u
        public float mountainHeight = 14f;       // высота плато, u (12-16)
        public int mountainFacets = 8;           // граней в основании: меньше = крупнее грани
        public float cliffSkirt = 4.2f;          // ширина склона от подошвы до плато, u
        public float apronH = 1f;                // осыпь у подножия, u
        public int terraces = 3;                 // уступов-пластов в склоне (2-3 с травяными полками)
        public float fallWidth = 3.5f;           // ширина ниши и ленты воды, u (3-4)
        public float fallHeight = 11f;           // высота падения воды, u (10-12)
        public float sourceBasinWidth = 7f;      // ширина воды в чаше у подножия, u
        public float sourceFoamR = 2.6f;         // радиус пенного круга, u
        public bool mountainTrail = true;        // тропа на плато по краю горы
        public float mountainTrailGrade = 0.36f; // макс. уклон тропы на гору (при короткой тропе поднимается автоматически)
        public float mountainTrailWidth = 3f;
        public float mountainTrailBank = 2f;     // откос тропы на склоне, u
        public int spruces = 4;                  // ёлки на плато (3-5)

        // смотровая
        public float lookoutHeight = 0.7f;       // высота земли под площадкой, u
        public float lookoutRadius = 5.5f;       // ширина выравнивания земли вокруг площадки и настила, u
        public bool lookoutBuild = true;         // деревянный балкон (false = плоский маркер-цилиндр)
        public float lookoutOverhang = 2.5f;     // насколько настил выступает за кромку острова, u (2-3)
        public float lookoutDeckLen = 9f;        // длина настила от входа до дальнего края, u
        public float lookoutDeckW = 8f;          // ширина настила, u
        public float lookoutDeckRaise = 0.6f;    // насколько настил выше земли, u
        public int lookoutSteps = 3;             // ступеней у входа (2-3)
        public float lookoutRailH = 2f;          // высота перил, u
        public float lookoutGateW = 3.6f;        // проём в перилах у тропы, u
        public bool lookoutBench = true;         // скамейка
        public bool lookoutScope = true;         // бинокль (подзорная труба) на стойке
        public bool lookoutFlag = true;          // флажок
        public int lookoutLamps = 3;             // фонарей на столбах (2-3)
        public bool lookoutGarland = true;       // гирлянда между фонарями
        public Color lookoutWood = new Color(1.28f, 1.08f, 0.86f, 1f);   // тёплый тинт дерева (tex[3])

        // тропы (как в LocationBuilder.Settings)
        public float trailWidth = 2.6f;
        public float trailWidthAtPad = 3.5f;
        public float trailFlat = 3f;
        public float trailMeander = 2.5f;
        public float trailMaxGrade = 0.26f;
        public float trailSmooth = 28f;
        public float trailBank = 8f;
        public float padSkirt = 8f;
        public int blur = 2;

        // защита от обхода реки и горы
        public bool sealSource = true;           // скальный хребет от истока до края острова
        public float ridgeHeight = 11f;          // высота хребта, u
        public float ridgeWidth = 5f;            // ширина у подошвы, u
        public bool riverBarrier = true;         // невидимые коллайдеры по хребту и вокруг подножия горы
        public bool riverWall = false;           // невидимая стенка НАД водой реки (по умолчанию выключена: в реку можно зайти, поплыть и утонуть, см. WaterSwim)
        public float walkSlope = 55f;            // проверка обхода: круче этого угла (град.) путь считается закрытым

        // картонный стиль
        public bool cardboard = true;            // true = меш Surface с картонным шейдером, false = обычный Terrain с 8 слоями
        public bool regenTextures = false;       // пересоздать PNG слоёв при ближайшей сборке
        public int surfaceStep = 1;              // шаг сетки меша: 1 = как Terrain (чёткие уступы горы), 2 = вдвое реже
        public float tileM = 4f;                 // метров на плитку текстуры (крупнее = проще и мультяшнее)
        public float sharpEdges = 5f;            // чёткость границ слоёв
        public float toonWrap = 0.45f;
        public float rimLight = 0.25f;
        public float paperGrain = 0.15f;         // лёгкое зерно, чтобы цвет не был «пластиком»
        public float contourStep = 0.45f;        // толщина «листа картона», м; 0 = без линий
        public float contourAmt = 0.03f;
        public float brightness = 1.08f;
        public float saturation = 1.15f;
        public Color shadowTint = new Color(0.62f, 0.54f, 0.86f, 1f);
        public bool trailEdge = true;            // тёмная кромка троп
        public bool trailRuts = true;            // колеи

        public bool buildWater = true;
        public bool buildMarkers = true;
        public bool showPreview = true;

        public List<LocationBuilder.PadDef> pads = LocationBuilder.Settings.DefaultPads();
    }

    Cfg _c = new Cfg();
    Vector2 _scroll;
    string _log = "";
    bool _fMain = true, _fOutline = true, _fEdge, _fUnder, _fMeadow, _fForest, _fLake, _fRiver = true, _fMtn = true, _fTrail, _fPads, _fLook, _fSeal = true, _fStyle = true;
    readonly List<KeyValuePair<Vector3, string>> _labels = new List<KeyValuePair<Vector3, string>>();
    Layout _preview;
    string _previewKey = "";

    [MenuItem("Tools/BuilderSim/Локация/Location Builder TESTER (остров с нуля)", false, 101)]
    static void Open()
    {
        var w = GetWindow<LocationBuilderTester>("Location TESTER");
        w.minSize = new Vector2(420f, 600f);
        w.Show();
    }

    void OnEnable()
    {
        try
        {
            string json = EditorPrefs.GetString(PrefsKey, "");
            bool migrated = false;
            string fromKey = PrefsKey;
            foreach (var oldKey in new[] { PrefsKeyOld4, PrefsKeyOld3, PrefsKeyOld, PrefsKeyOld2, PrefsKeyOld1 })
            {
                if (!string.IsNullOrEmpty(json)) break;
                json = EditorPrefs.GetString(oldKey, "");
                migrated = !string.IsNullOrEmpty(json);
                if (migrated) fromKey = oldKey;
            }
            if (!string.IsNullOrEmpty(json)) JsonUtility.FromJsonOverwrite(json, _c);
            if (migrated)
            {
                if (fromKey == PrefsKeyOld4) MigrateV5(_c);    // v5 уже с картонным стилем и горой: всё переносится, добавляется только балкон смотровой
                else
                {
                    if (fromKey != PrefsKeyOld3) MigrateOld(_c);   // v4 уже с горой: сбрасываем только стиль
                    ApplyCartoon(_c);
                    MigrateV5(_c);
                }
            }
            if (_c.riverLookVer < 2)
            {
                // старые сохранённые настройки: глубокое русло с крутым берегом, без островков и кос
                _c.riverLookVer = 2;
                _c.depthValley = 1.8f; _c.valleyWall = 2.6f; _c.islets = 0; _c.pebbleBars = false;
            }
            if (_c.riverLookVer < 3)
            {
                // v3: глубокая река под плавание и откос пошире, чтобы можно было выйти из воды
                _c.riverLookVer = 3;
                _c.depthValley = 3.4f; _c.valleyWall = 4.6f; _c.pebbleBars = false;
            }
        }
        catch (Exception) { _c = new Cfg(); }
        SceneView.duringSceneGui += OnSceneGUI;
    }

    // настройки старых версий (v1-v3): поля, у которых изменился смысл, возвращаются к новым умолчаниям,
    // остальное (seed, контур, тропы, площадки, стиль) переносится как есть
    static void MigrateOld(Cfg c)
    {
        var d = new Cfg();
        c.riverWidth = d.riverWidth; c.depthValley = d.depthValley; c.waterValley = d.waterValley;
        c.valleyWall = d.valleyWall; c.valleyWidth = d.valleyWidth; c.riverMeander = d.riverMeander; c.widthVar = d.widthVar;
        c.cliffSkirt = d.cliffSkirt; c.apronH = d.apronH; c.terraces = d.terraces;
        c.ridgeHeight = d.ridgeHeight; c.ridgeWidth = d.ridgeWidth;
        c.lakeDepth = d.lakeDepth; c.surfaceStep = d.surfaceStep;
        if (c.alphaRes < 1024) c.alphaRes = 1024;
        c.regenTextures = true;   // скала стала серо-фиолетовой
    }

    // v5 -> v6: новые поля балкона смотровой берут умолчания, остальные настройки остаются как были
    static void MigrateV5(Cfg c)
    {
        var d = new Cfg();
        c.lookoutBuild = d.lookoutBuild; c.lookoutOverhang = d.lookoutOverhang; c.lookoutDeckLen = d.lookoutDeckLen;
        c.lookoutDeckW = d.lookoutDeckW; c.lookoutDeckRaise = d.lookoutDeckRaise; c.lookoutSteps = d.lookoutSteps;
        c.lookoutRailH = d.lookoutRailH; c.lookoutGateW = d.lookoutGateW; c.lookoutBench = d.lookoutBench;
        c.lookoutScope = d.lookoutScope; c.lookoutFlag = d.lookoutFlag; c.lookoutLamps = d.lookoutLamps;
        c.lookoutGarland = d.lookoutGarland; c.lookoutWood = d.lookoutWood;
    }

    // мультяшный стиль: яркие плоские цвета, крупные детали, три чёткие полосы света с цветными тенями
    static void ApplyCartoon(Cfg c)
    {
        var d = new Cfg();
        c.tileM = d.tileM; c.sharpEdges = d.sharpEdges; c.toonWrap = d.toonWrap; c.rimLight = d.rimLight;
        c.paperGrain = d.paperGrain; c.contourStep = d.contourStep; c.contourAmt = d.contourAmt;
        c.brightness = d.brightness; c.saturation = d.saturation; c.shadowTint = d.shadowTint;
        c.regenTextures = true;
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        EditorPrefs.SetString(PrefsKey, JsonUtility.ToJson(_c));
    }

    // =====================================================================================================
    //  ОКНО
    // =====================================================================================================

    static bool Fold(bool st, string t) { return EditorGUILayout.Foldout(st, t, true); }
    static float Fl(string l, float v) { return EditorGUILayout.FloatField(l, v); }
    static float Sl(string l, float v, float a, float b) { return EditorGUILayout.Slider(l, v, a, b); }

    void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        EditorGUI.BeginChangeCheck();

        EditorGUILayout.HelpBox("Отдельный остров с нуля. Старую локацию (LocationBuilder) не трогает: создаёт объект «" + RootName + "» и ассеты в " + AssetDir + ".", MessageType.Info);

        _fMain = Fold(_fMain, "Общее");
        if (_fMain)
        {
            EditorGUILayout.BeginHorizontal();
            _c.seed = EditorGUILayout.IntField("Seed", _c.seed);
            if (GUILayout.Button("Случайный", GUILayout.Width(80f))) _c.seed = UnityEngine.Random.Range(1, 999999);
            EditorGUILayout.EndHorizontal();
            _c.worldScale = Sl("World Scale", _c.worldScale, 0.1f, 2f);
            _c.origin = EditorGUILayout.Vector3Field("Origin", _c.origin);
            _c.heightmapRes = EditorGUILayout.IntPopup("Heightmap res", _c.heightmapRes, new[] { "513", "1025" }, new[] { 513, 1025 });
            _c.alphaRes = EditorGUILayout.IntPopup("Splatmap res", _c.alphaRes, new[] { "512", "1024" }, new[] { 512, 1024 });
            _c.autoTerrainSize = EditorGUILayout.Toggle("Размер Terrain под контур", _c.autoTerrainSize);
            if (!_c.autoTerrainSize) _c.terrainSize = Fl("Размер Terrain (u)", _c.terrainSize);
            _c.buildWater = EditorGUILayout.Toggle("Вода (река, пруд, водопады)", _c.buildWater);
            _c.buildMarkers = EditorGUILayout.Toggle("Маркеры (площадки, мосты, смотровая)", _c.buildMarkers);
            _c.showPreview = EditorGUILayout.Toggle("Предпросмотр плана в Scene", _c.showPreview);
            if (GUILayout.Button("Импорт из LocationBuilder (seed, scale, тропы, пруд, лес, площадки)")) ImportFromBuilder();
        }

        _fOutline = Fold(_fOutline, "Контур острова");
        if (_fOutline)
        {
            _c.islandRadius = Fl("Полуразмер (u)", _c.islandRadius);
            _c.roundness = Sl("Скругление (2 круг .. 6 квадрат)", _c.roundness, 2f, 6f);
            _c.bayDepth = Fl("Бухты (u)", _c.bayDepth);
            _c.capeSize = Fl("Мелкие мысы (u)", _c.capeSize);
            _c.outlineNoise = Fl("Частота изгибов", _c.outlineNoise);
            _c.lookoutCape = Fl("Балкон под смотровую (u)", _c.lookoutCape);
        }

        _fEdge = Fold(_fEdge, "Кромка");
        if (_fEdge)
        {
            _c.rimHeight = Fl("Высота кромки (u)", _c.rimHeight);
            _c.rimWidth = Fl("Ширина кромки (u)", _c.rimWidth);
            _c.rimFlat = Fl("Ровная полоса у края (u)", _c.rimFlat);
            _c.gateClear = Fl("Снижение у ворот (u)", _c.gateClear);
            _c.bowlHeight = Fl("Подъём чаши к краю (u)", _c.bowlHeight);
        }

        _fUnder = Fold(_fUnder, "Низ острова (скала под краем)");
        if (_fUnder)
        {
            _c.underside = EditorGUILayout.Toggle("Строить", _c.underside);
            _c.underDepth = Fl("Глубина (u)", _c.underDepth);
            _c.underTaper = Sl("Сужение к дну", _c.underTaper, 0.3f, 0.97f);
            _c.underRough = Sl("Неровность скалы", _c.underRough, 0f, 0.4f);
            _c.underSegs = EditorGUILayout.IntSlider("Сегментов по контуру", _c.underSegs, 64, 480);
            _c.underRings = EditorGUILayout.IntSlider("Ярусов вниз", _c.underRings, 6, 28);
        }

        _fMeadow = Fold(_fMeadow, "Луга и центр");
        if (_fMeadow)
        {
            _c.meadowSwell = Fl("Высота холмов (u)", _c.meadowSwell);
            _c.meadowScale = Fl("Частота холмов", _c.meadowScale);
            _c.meadowFlat = Sl("Доля ровных лощин", _c.meadowFlat, 0f, 0.9f);
            _c.centerRadius = Fl("Радиус центра (u)", _c.centerRadius);
            _c.blur = EditorGUILayout.IntSlider("Сглаживание (проходов)", _c.blur, 0, 6);
        }

        _fForest = Fold(_fForest, "Лес (красная зона)");
        if (_fForest)
        {
            _c.forestTier1 = Fl("Ярус 1 (плато у площадки), u", _c.forestTier1);
            _c.forestTier2 = Fl("Ярус 2 (дальняя часть), u", _c.forestTier2);
            _c.forestSlope = Fl("Ширина склонов, u", _c.forestSlope);
            _c.forestTierSlope = Fl("Длина уступа между ярусами, u", _c.forestTierSlope);
            _c.forestRelief = Fl("Бугры на массиве, u", _c.forestRelief);
        }

        _fLake = Fold(_fLake, "Пруд (зелёная зона)");
        if (_fLake)
        {
            _c.lakePos = EditorGUILayout.Vector2Field("Центр (x, z), u", _c.lakePos);
            _c.lakeRadius = Fl("Радиус чаши, u", _c.lakeRadius);
            _c.lakeShore = Fl("Пологий берег, u", _c.lakeShore);
            _c.lakeDepth = Fl("Глубина ямы, u", _c.lakeDepth);
            _c.buildPond = EditorGUILayout.Toggle("Пруд: вода, остров, мостик, пирс", _c.buildPond);
            if (_c.buildPond)
            {
                _c.pondIsland = EditorGUILayout.Toggle("Остров посередине", _c.pondIsland);
                if (_c.pondIsland)
                {
                    _c.pondBridge = EditorGUILayout.Toggle("Мостик к острову", _c.pondBridge);
                    _c.pondIslandRadius = Fl("Радиус острова, u", _c.pondIslandRadius);
                }
                _c.pondLilies = EditorGUILayout.IntSlider("Кувшинки", _c.pondLilies, 0, 40);
                _c.pondPierLength = Fl("Длина пирса в воду, u", _c.pondPierLength);
                _c.pondDeepen = Fl("Углубление дна (x), 1-3", _c.pondDeepen);
            }
        }

        _fMtn = Fold(_fMtn, "Скальная гора и водопад (северо-запад)");
        if (_fMtn)
        {
            _c.mountainPos = EditorGUILayout.Vector2Field("Центр горы (x, z), u", _c.mountainPos);
            _c.mountainSize = EditorGUILayout.Vector2Field("Размер: ширина x длина, u", _c.mountainSize);
            _c.mountainHeight = Sl("Высота плато, u (12-16)", _c.mountainHeight, 8f, 20f);
            _c.mountainFacets = EditorGUILayout.IntSlider("Граней в основании", _c.mountainFacets, 5, 12);
            _c.cliffSkirt = Fl("Ширина склона до плато, u", _c.cliffSkirt);
            _c.terraces = EditorGUILayout.IntSlider("Уступов-пластов в склоне", _c.terraces, 1, 6);
            _c.apronH = Fl("Осыпь у подножия, u", _c.apronH);
            _c.fallWidth = Sl("Ширина ниши и водопада, u (3-4)", _c.fallWidth, 2f, 6f);
            _c.fallHeight = Sl("Высота падения воды, u (10-12)", _c.fallHeight, 6f, 13f);
            _c.sourceBasinWidth = Fl("Ширина воды в чаше у подножия, u", _c.sourceBasinWidth);
            _c.sourceFoamR = Fl("Радиус пенного круга, u", _c.sourceFoamR);
            _c.spruces = EditorGUILayout.IntSlider("Ёлок на плато", _c.spruces, 0, 8);
            _c.mountainTrail = EditorGUILayout.Toggle("Тропа на плато (по краю горы)", _c.mountainTrail);
            if (_c.mountainTrail)
            {
                _c.mountainTrailGrade = Sl("Макс. уклон тропы на гору", _c.mountainTrailGrade, 0.15f, 0.6f);
                _c.mountainTrailWidth = Fl("Ширина тропы на горе, u", _c.mountainTrailWidth);
                _c.mountainTrailBank = Fl("Откос тропы на склоне, u", _c.mountainTrailBank);
            }
            EditorGUILayout.HelpBox("Гора строится вместе с рекой (галочка «Река»). Передняя грань с нишей смотрит к центру острова.", MessageType.None);
        }

        _fRiver = Fold(_fRiver, "Река: от подножия горы вдоль западного края (синяя зона)");
        if (_fRiver)
        {
            _c.river = EditorGUILayout.Toggle("Река (и гора)", _c.river);
            _c.riverWidth = Sl("Ширина воды, u (8-10)", _c.riverWidth, 4f, 14f);
            _c.depthValley = Sl("Глубина русла, u (3.4 = можно плыть)", _c.depthValley, 0.2f, 6f);
            _c.waterValley = Fl("Уровень воды у подножия горы, u", _c.waterValley);
            _c.valleyWall = Fl("Пологий берег (откос), u", _c.valleyWall);
            _c.valleyWidth = Fl("Затухание рельефа у реки, u", _c.valleyWidth);
            _c.riverMeander = Fl("Меандр, u", _c.riverMeander);
            _c.widthVar = Sl("Плёсы и перекаты", _c.widthVar, 0f, 0.4f);
            _c.bridgeGap = Fl("Площадка Bridge -> вода, u", _c.bridgeGap);
            _c.bridgeReach = Fl("Прямой участок у моста, u", _c.bridgeReach);
            _c.bridgeBuild = EditorGUILayout.Toggle("Деревянный мост (false = маркер)", _c.bridgeBuild);
            if (_c.bridgeBuild)
            {
                _c.bridgeWidth = Sl("Ширина моста, u (2.6-3)", _c.bridgeWidth, 2.6f, 3f);
                _c.bridgeArch = Sl("Арка моста, u (0.4-0.6)", _c.bridgeArch, 0.4f, 0.6f);
                _c.bridgePosts = EditorGUILayout.Toggle("Столбики у входов", _c.bridgePosts);
                _c.bridgeLamps = EditorGUILayout.Toggle("Фонари на столбиках", _c.bridgeLamps);
            }
            _c.fallLength = Fl("Длина водопада с края острова, u", _c.fallLength);
            _c.islets = EditorGUILayout.IntSlider("Островков в русле", _c.islets, 0, 4);
            _c.pebbleBars = EditorGUILayout.Toggle("Галечные косы", _c.pebbleBars);
            _c.boulderDensity = Sl("Густота валунов в русле", _c.boulderDensity, 0f, 2.5f);
            _c.riverFlowSpeed = Sl("Скорость течения (ленты)", _c.riverFlowSpeed, 0.2f, 3f);
            _c.riverFoamAmount = Sl("Пена (берег, камни, перекаты)", _c.riverFoamAmount, 0f, 2f);
            _c.riverLogs = EditorGUILayout.Toggle("Упавшие бревна у воды", _c.riverLogs);
            _c.riverMist = EditorGUILayout.Toggle("Брызги и туман под водопадом с края", _c.riverMist);
            _c.riverFish = EditorGUILayout.Toggle("Рыбы-силуэты у островков", _c.riverFish);
            _c.riverSparkle = Sl("Блики солнца на воде", _c.riverSparkle, 0f, 1f);
            _c.riverReeds = EditorGUILayout.Toggle("Камыш и рогоз у кромки", _c.riverReeds);
            _c.riverLife = EditorGUILayout.Toggle("Утки, стрекозы, бабочки", _c.riverLife);
            _c.riverJetty = EditorGUILayout.Toggle("Рыбацкие мостки", _c.riverJetty);
            _c.rocks = EditorGUILayout.Toggle("Валуны (русло, подножие, берега, яма)", _c.rocks);
            _c.props = EditorGUILayout.Toggle("Ёлки, кусты, камыш, деревца", _c.props);
        }

        _fSeal = Fold(_fSeal, "Защита от обхода (хребет от горы, стенки)");
        if (_fSeal)
        {
            _c.sealSource = EditorGUILayout.Toggle("Хребет от горы до края острова", _c.sealSource);
            _c.ridgeHeight = Fl("Высота хребта, u", _c.ridgeHeight);
            _c.ridgeWidth = Fl("Ширина хребта у подошвы, u", _c.ridgeWidth);
            _c.riverBarrier = EditorGUILayout.Toggle("Невидимые стенки (хребет, гора)", _c.riverBarrier);
            _c.riverWall = EditorGUILayout.Toggle("Стенка над водой реки (не нужна: в реке тонут)", _c.riverWall);
            _c.walkSlope = Sl("Проходимый уклон при проверке, °", _c.walkSlope, 20f, 80f);
            EditorGUILayout.HelpBox("После сборки рельеф проверяется поиском пути от центра до смотровой по суше. Если обход есть, в логе будет место, где он проходит.", MessageType.None);
        }

        _fStyle = Fold(_fStyle, "Мультяшный стиль (текстуры и шейдер)");
        if (_fStyle)
        {
            _c.cardboard = EditorGUILayout.Toggle("Картонный шейдер (меш поверх Terrain)", _c.cardboard);
            _c.regenTextures = EditorGUILayout.Toggle("Пересоздать PNG-текстуры при сборке", _c.regenTextures);
            _c.surfaceStep = EditorGUILayout.IntSlider("Шаг сетки меша (1 точнее, 2 легче)", _c.surfaceStep, 1, 4);
            _c.tileM = Sl("Размер плитки текстуры, м", _c.tileM, 1f, 10f);
            _c.sharpEdges = Sl("Чёткость границ слоёв", _c.sharpEdges, 1f, 8f);
            _c.toonWrap = Sl("Мягкость света (wrap)", _c.toonWrap, 0f, 1f);
            _c.rimLight = Sl("Контровой свет", _c.rimLight, 0f, 1f);
            _c.paperGrain = Sl("Зерно", _c.paperGrain, 0f, 1f);
            _c.saturation = Sl("Насыщенность", _c.saturation, 0.6f, 1.8f);
            _c.contourStep = Fl("Толщина листа картона, м (0 = без линий)", _c.contourStep);
            _c.contourAmt = Sl("Сила линий между листами", _c.contourAmt, 0f, 0.6f);
            _c.brightness = Sl("Яркость", _c.brightness, 0.5f, 1.6f);
            _c.shadowTint = EditorGUILayout.ColorField("Цвет теней", _c.shadowTint);
            if (GUILayout.Button("Мультяшный стиль (сбросить стиль к умолчаниям)")) ApplyCartoon(_c);
            _c.trailEdge = EditorGUILayout.Toggle("Тёмная кромка у троп", _c.trailEdge);
            _c.trailRuts = EditorGUILayout.Toggle("Колеи на тропах", _c.trailRuts);
        }

        _fLook = Fold(_fLook, "Смотровая за мостом (деревянный балкон)");
        if (_fLook)
        {
            _c.lookoutBuild = EditorGUILayout.Toggle("Деревянный балкон (выкл = плоский маркер)", _c.lookoutBuild);
            _c.lookoutHeight = Fl("Высота земли под площадкой, u", _c.lookoutHeight);
            _c.lookoutRadius = Fl("Ширина выравнивания земли, u", _c.lookoutRadius);
            if (_c.lookoutBuild)
            {
                _c.lookoutOverhang = Sl("Выступ за кромку острова, u (2-3)", _c.lookoutOverhang, 1f, 4f);
                _c.lookoutDeckLen = Sl("Длина настила, u", _c.lookoutDeckLen, 5f, 16f);
                _c.lookoutDeckW = Sl("Ширина настила, u", _c.lookoutDeckW, 5f, 12f);
                _c.lookoutDeckRaise = Sl("Настил выше земли, u", _c.lookoutDeckRaise, 0.2f, 1.2f);
                _c.lookoutSteps = EditorGUILayout.IntSlider("Ступеней у входа (2-3)", _c.lookoutSteps, 1, 4);
                _c.lookoutRailH = Sl("Высота перил, u", _c.lookoutRailH, 1.4f, 3f);
                _c.lookoutGateW = Sl("Проём в перилах к тропе, u", _c.lookoutGateW, 2f, 6f);
                _c.lookoutBench = EditorGUILayout.Toggle("Скамейка", _c.lookoutBench);
                _c.lookoutScope = EditorGUILayout.Toggle("Бинокль на стойке", _c.lookoutScope);
                _c.lookoutFlag = EditorGUILayout.Toggle("Флажок", _c.lookoutFlag);
                _c.lookoutLamps = EditorGUILayout.IntSlider("Фонарей (2-3)", _c.lookoutLamps, 0, 3);
                _c.lookoutGarland = EditorGUILayout.Toggle("Гирлянда между фонарями", _c.lookoutGarland);
                _c.lookoutWood = EditorGUILayout.ColorField(new GUIContent("Тон дерева (тинт tex[3])"), _c.lookoutWood, true, false, true);
                EditorGUILayout.HelpBox("Балкон ставится на западной кромке за мостом. Тропа от моста заканчивается у ступеней; земля под настилом выравнивается до высоты площадки.", MessageType.None);
            }
        }

        _fTrail = Fold(_fTrail, "Тропы и площадки (как в LocationBuilder)");
        if (_fTrail)
        {
            _c.trailWidth = Fl("Ширина", _c.trailWidth);
            _c.trailWidthAtPad = Fl("Ширина у площадки", _c.trailWidthAtPad);
            _c.trailFlat = Fl("Ширина выравнивания", _c.trailFlat);
            _c.trailMeander = Fl("Изгибы (амплитуда)", _c.trailMeander);
            _c.trailMaxGrade = Fl("Макс. уклон", _c.trailMaxGrade);
            _c.trailSmooth = Fl("Сглаживание профиля, u", _c.trailSmooth);
            _c.trailBank = Fl("Откос, u", _c.trailBank);
            _c.padSkirt = Fl("Переход площадка -> склон, u", _c.padSkirt);
        }

        _fPads = Fold(_fPads, "Площадки (угол от севера по часовой, расстояние)");
        if (_fPads)
        {
            foreach (var p in _c.pads)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(p.name, GUILayout.Width(90f));
                p.angle = EditorGUILayout.FloatField(p.angle, GUILayout.Width(60f));
                p.distance = EditorGUILayout.FloatField(p.distance, GUILayout.Width(60f));
                EditorGUILayout.EndHorizontal();
            }
            if (GUILayout.Button("Сбросить площадки к LocationBuilder.Placement")) _c.pads = LocationBuilder.Settings.DefaultPads();
        }

        bool changed = EditorGUI.EndChangeCheck();
        if (changed) { EditorPrefs.SetString(PrefsKey, JsonUtility.ToJson(_c)); SceneView.RepaintAll(); }

        EditorGUILayout.Space();
        if (GUILayout.Button("ПОСТРОИТЬ ОСТРОВ", GUILayout.Height(34f))) Build();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Удалить " + RootName)) DeleteRoot();
        if (GUILayout.Button("Камера на остров")) FrameRoot();
        EditorGUILayout.EndHorizontal();
        if (!string.IsNullOrEmpty(_log)) EditorGUILayout.HelpBox(_log, MessageType.None);

        EditorGUILayout.EndScrollView();
    }

    void ImportFromBuilder()
    {
        string json = EditorPrefs.GetString(LocationBuilder.PrefsKey, "");
        var s = new LocationBuilder.Settings();
        if (!string.IsNullOrEmpty(json))
        {
            try { JsonUtility.FromJsonOverwrite(json, s); }
            catch (Exception) { }
        }
        _c.seed = s.seed; _c.worldScale = s.worldScale; _c.origin = s.origin;
        _c.centerRadius = s.centerRadius;
        _c.trailWidth = s.trailWidth; _c.trailWidthAtPad = s.trailWidthAtPad; _c.trailFlat = s.trailFlatWidth; _c.trailMeander = s.trailMeander;
        _c.trailMaxGrade = s.trailMaxGrade; _c.trailSmooth = s.trailGradeSmooth; _c.trailBank = s.trailBank; _c.padSkirt = s.padSkirt;
        _c.lakePos = s.lakePos; _c.lakeRadius = s.lakeRadius; _c.lakeShore = s.lakeShore;
        _c.forestTier1 = s.forestTier1; _c.forestTier2 = s.forestTier2; _c.forestSlope = s.forestSlope; _c.forestTierSlope = s.forestTierSlope; _c.forestRelief = s.forestGladeRelief;
        _c.meadowSwell = s.meadowSwell; _c.meadowScale = s.meadowSwellScale; _c.meadowFlat = s.meadowSwellFlat; _c.bowlHeight = s.bowlHeight;
        _c.rimHeight = s.rimHeight; _c.rimWidth = s.rimWidth; _c.rimFlat = s.rimFlatWidth;
        _c.pads = new List<LocationBuilder.PadDef>();
        foreach (var p in s.pads)
            _c.pads.Add(new LocationBuilder.PadDef(p.name, p.angle, p.distance, p.size.x, p.size.y, p.gizmoColor, p.parent, p.forkDistance, p.round) { straight = p.straight });
        _log = "Импортировано из LocationBuilder: seed " + s.seed + ", scale " + s.worldScale + ", площадок " + _c.pads.Count + ".";
    }

    // =====================================================================================================
    //  SCENE: предпросмотр плана и подписи
    // =====================================================================================================

    void OnSceneGUI(SceneView sv)
    {
        var rootGo = GameObject.Find(RootName);
        if (rootGo != null)
        {
            var st = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.white }, fontSize = 11 };
            foreach (var kv in _labels) Handles.Label(kv.Key + rootGo.transform.position, kv.Value, st);
            return;
        }
        if (!_c.showPreview) return;
        string key = JsonUtility.ToJson(_c);
        if (_preview == null || key != _previewKey)
        {
            try { _preview = BuildLayout(_c); _previewKey = key; }
            catch (Exception) { _preview = null; return; }
        }
        DrawLayout(_preview);
    }

    void DrawLayout(Layout L)
    {
        float ws = _c.worldScale;
        Func<Vector2, float, Vector3> W = (p, y) => _c.origin + new Vector3(p.x * ws, y * ws, p.y * ws);
        Handles.color = Color.yellow;
        var pts = new List<Vector3>();
        foreach (var p in L.outline.pts) pts.Add(W(p, 0f));
        pts.Add(pts[0]);
        Handles.DrawAAPolyLine(4f, pts.ToArray());
        if (L.river != null)
        {
            var rp = new List<Vector3>();
            foreach (var p in L.river.p) rp.Add(W(p, 0.2f));
            Handles.color = new Color(0.2f, 0.6f, 1f);
            Handles.DrawAAPolyLine(6f, rp.ToArray());
            Handles.Label(W(L.river.p[0], 1f), "ПОДНОЖИЕ ВОДОПАДА (начало реки)");
            Handles.Label(W(L.river.lip, 1f), "ВОДОПАД с края");
        }
        if (L.mtn != null)
        {
            var mp = new List<Vector3>();
            foreach (var v in L.mtn.v) mp.Add(W(v, 0.3f));
            mp.Add(mp[0]);
            Handles.color = new Color(0.62f, 0.55f, 0.78f);
            Handles.DrawAAPolyLine(5f, mp.ToArray());
            Handles.Label(W(L.mtn.c, 2f), "ГОРА (скальный массив, плато)");
            Handles.DrawWireDisc(W(L.mtn.foot, 0f), Vector3.up, 2f * ws);
        }
        if (L.ridge != null)
        {
            var gp = new List<Vector3>();
            foreach (var p in L.ridge) gp.Add(W(p, 0.4f));
            Handles.color = new Color(0.9f, 0.3f, 0.2f);
            Handles.DrawAAPolyLine(5f, gp.ToArray());
            Handles.Label(W(L.ridge[L.ridge.Count / 2], 1.5f), "ХРЕБЕТ (закрывает обход горы)");
        }
        foreach (var t in L.trails)
        {
            var tp = new List<Vector3>();
            foreach (var p in t.p) tp.Add(W(p, 0.15f));
            Handles.color = new Color(0.95f, 0.8f, 0.5f);
            Handles.DrawAAPolyLine(3f, tp.ToArray());
        }
        foreach (var p in L.pads)
        {
            Handles.color = p.color;
            Handles.DrawWireDisc(W(p.pos, 0f), Vector3.up, p.rad * ws);
            Handles.Label(W(p.pos, 1f), p.name);
        }
        Handles.color = Color.white;
        Handles.DrawWireDisc(W(L.gate, 0f), Vector3.up, 3f * ws);
        Handles.Label(W(L.gate, 1f), "ВОРОТА (мост на след. локацию)");
        if (L.river != null && L.bridge != null)
        {
            Handles.DrawLine(W(L.bridge.pos - L.bridge.fwd * L.bridge.hd, 0.5f), W(L.landing, 0.5f));
            Handles.Label(W((L.bridge.pos + L.landing) * 0.5f, 1.5f), "МОСТ через реку");
            Handles.DrawWireDisc(W(L.lookout, 0f), Vector3.up, _c.lookoutRadius * ws);
            Handles.Label(W(L.lookout, 1.5f), "СМОТРОВАЯ");
            if (L.deckOn)
            {
                Handles.color = new Color(0.9f, 0.65f, 0.3f);
                Handles.DrawAAPolyLine(4f, new[]
                {
                    W(new Vector2(L.deckX1, L.deckZ - L.deckHW), 0.5f), W(new Vector2(L.deckX0, L.deckZ - L.deckHW), 0.5f),
                    W(new Vector2(L.deckX0, L.deckZ + L.deckHW), 0.5f), W(new Vector2(L.deckX1, L.deckZ + L.deckHW), 0.5f),
                    W(new Vector2(L.deckX1, L.deckZ - L.deckHW), 0.5f)
                });
                Handles.Label(W(new Vector2(L.deckX1, L.deckZ), 1.5f), "БАЛКОН (настил)");
            }
        }
        Handles.color = new Color(0.2f, 0.5f, 1f);
        Handles.DrawWireDisc(W(L.lakePos, 0f), Vector3.up, _c.lakeRadius * ws * 0.6f);
        Handles.Label(W(L.lakePos, 1f), "ЯМА под пруд");
    }

    // =====================================================================================================
    //  УТИЛИТЫ
    // =====================================================================================================

    static float SS(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    static Vector2 CR(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    static List<Vector2> CatmullDense(List<Vector2> ctrl, int steps)
    {
        int m = ctrl.Count;
        Vector2 g0 = ctrl[0] - (ctrl[1] - ctrl[0]);
        Vector2 gN = ctrl[m - 1] + (ctrl[m - 1] - ctrl[m - 2]);
        var dense = new List<Vector2>();
        for (int k = 0; k < m - 1; k++)
        {
            Vector2 p0 = k == 0 ? g0 : ctrl[k - 1];
            Vector2 p1 = ctrl[k], p2 = ctrl[k + 1];
            Vector2 p3 = k + 2 < m ? ctrl[k + 2] : gN;
            for (int st = 0; st < steps; st++) dense.Add(CR(p0, p1, p2, p3, st / (float)steps));
        }
        dense.Add(ctrl[m - 1]);
        return dense;
    }

    static List<Vector2> Resample(List<Vector2> dense, float step)
    {
        var pts = new List<Vector2> { dense[0] };
        float acc = 0f, nextS = step;
        for (int k = 1; k < dense.Count; k++)
        {
            float seg = (dense[k] - dense[k - 1]).magnitude;
            if (seg < 1e-5f) continue;
            while (acc + seg >= nextS)
            {
                float t = (nextS - acc) / seg;
                pts.Add(Vector2.Lerp(dense[k - 1], dense[k], t));
                nextS += step;
            }
            acc += seg;
        }
        return pts;
    }

    sealed class Noise
    {
        public readonly float ox, oz;
        public readonly System.Random rng;
        // порядок вызовов rng как в LocationBuilder.Gen: ox, oz, затем seedOff каждой тропы
        public Noise(int seed)
        {
            rng = new System.Random(seed);
            ox = 1000f + (float)rng.NextDouble() * 5000f;
            oz = 1000f + (float)rng.NextDouble() * 5000f;
        }
        public float P(float x, float z, float off) { return Mathf.PerlinNoise(x + ox + off, z + oz + off); }
    }

    sealed class Grid
    {
        public int N;
        public float half, cell;
        public float At(float[] f, float x, float z)
        {
            float fx = Mathf.Clamp((x + half) / cell, 0f, N - 1.001f);
            float fz = Mathf.Clamp((z + half) / cell, 0f, N - 1.001f);
            int i0 = (int)fx, j0 = (int)fz;
            float tx = fx - i0, tz = fz - j0;
            float a = f[j0 * N + i0], b = f[j0 * N + i0 + 1], c = f[(j0 + 1) * N + i0], d = f[(j0 + 1) * N + i0 + 1];
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), tz);
        }
    }

    delegate void CellHit(int idx, Vector2 p, float t, float dist);

    static void Stamp(Grid g, Vector2 a, Vector2 b, float reach, CellHit cb)
    {
        int i0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.x, b.x) - reach + g.half) / g.cell));
        int i1 = Mathf.Min(g.N - 1, Mathf.CeilToInt((Mathf.Max(a.x, b.x) + reach + g.half) / g.cell));
        int j0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.y, b.y) - reach + g.half) / g.cell));
        int j1 = Mathf.Min(g.N - 1, Mathf.CeilToInt((Mathf.Max(a.y, b.y) + reach + g.half) / g.cell));
        Vector2 ab = b - a;
        float l2 = Mathf.Max(ab.sqrMagnitude, 1e-6f);
        for (int j = j0; j <= j1; j++)
        {
            float z = -g.half + j * g.cell;
            for (int i = i0; i <= i1; i++)
            {
                var p = new Vector2(-g.half + i * g.cell, z);
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2);
                float d = (p - (a + ab * t)).magnitude;
                if (d <= reach) cb(j * g.N + i, p, t, d);
            }
        }
    }

    static float Quantile01(float[] v, float q)
    {
        const int B = 512;
        var hist = new int[B];
        for (int i = 0; i < v.Length; i++) hist[Mathf.Clamp((int)(v[i] * B), 0, B - 1)]++;
        int target = Mathf.Max(1, (int)(Mathf.Clamp01(q) * v.Length)), acc = 0;
        for (int b = 0; b < B; b++)
        {
            acc += hist[b];
            if (acc >= target) return (b + 1f) / B;
        }
        return 1f;
    }

    static void Blur(float[] a, int N, int passes)
    {
        var tmp = new float[N * N];
        for (int pass = 0; pass < passes; pass++)
        {
            for (int j = 0; j < N; j++)
            {
                int jm = Mathf.Max(j - 1, 0), jp = Mathf.Min(j + 1, N - 1);
                for (int i = 0; i < N; i++)
                {
                    int im = Mathf.Max(i - 1, 0), ip = Mathf.Min(i + 1, N - 1);
                    tmp[j * N + i] = (a[j * N + i] * 4f + a[j * N + im] + a[j * N + ip] + a[jm * N + i] + a[jp * N + i]) / 8f;
                }
            }
            Array.Copy(tmp, a, a.Length);
        }
    }

    static float PolySegDist(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
        return (p - (a + ab * t)).magnitude;
    }

    // =====================================================================================================
    //  КОНТУР
    // =====================================================================================================

    sealed class Outline
    {
        public const int M = 720;
        public readonly float[] r = new float[M];
        public readonly Vector2[] pts = new Vector2[M];
        public float maxExt;

        public Outline(Cfg c, Noise n)
        {
            float twoPi = Mathf.PI * 2f;
            for (int i = 0; i < M; i++)
            {
                float th = i * twoPi / M;
                float co = Mathf.Cos(th), si = Mathf.Sin(th);
                float p = c.roundness;
                float sup = c.islandRadius / Mathf.Pow(Mathf.Pow(Mathf.Abs(co), p) + Mathf.Pow(Mathf.Abs(si), p), 1f / p);
                float low = (n.P(co * 1.3f * c.outlineNoise + 7f, si * 1.3f * c.outlineNoise + 7f, 20f) - 0.5f) * 2f;
                float fine = (n.P(co * 4.2f + 3f, si * 4.2f + 3f, 40f) - 0.5f) * 2f;
                float da = Mathf.Abs(Mathf.DeltaAngle(th * Mathf.Rad2Deg, 180f)) * Mathf.Deg2Rad;
                float balcony = c.lookoutCape * Mathf.Exp(-(da / 0.24f) * (da / 0.24f));
                r[i] = sup + c.bayDepth * low + c.capeSize * 0.6f * fine + balcony;
            }
            for (int pass = 0; pass < 3; pass++)
            {
                var t = (float[])r.Clone();
                for (int i = 0; i < M; i++) r[i] = 0.25f * t[(i + M - 1) % M] + 0.5f * t[i] + 0.25f * t[(i + 1) % M];
            }
            for (int i = 0; i < M; i++)
            {
                float th = i * twoPi / M;
                pts[i] = new Vector2(Mathf.Cos(th), Mathf.Sin(th)) * r[i];
                maxExt = Mathf.Max(maxExt, Mathf.Abs(pts[i].x), Mathf.Abs(pts[i].y));
            }
        }

        public float R(float th)
        {
            float twoPi = Mathf.PI * 2f;
            th = th % twoPi; if (th < 0f) th += twoPi;
            float f = th / twoPi * M;
            int i0 = Mathf.FloorToInt(f) % M;
            float t = f - Mathf.Floor(f);
            return Mathf.Lerp(r[i0], r[(i0 + 1) % M], t);
        }

        public bool Inside(float x, float z)
        {
            return Mathf.Sqrt(x * x + z * z) < R(Mathf.Atan2(z, x));
        }

        public Vector2 Pt(float th) { float rr = R(th); return new Vector2(Mathf.Cos(th), Mathf.Sin(th)) * rr; }
    }

    // =====================================================================================================
    //  ПЛАН: площадки, река, тропы
    // =====================================================================================================

    sealed class Pad
    {
        public string name, parent = "";
        public Vector2 pos, fwd, right, entry, fork, forkTan;
        public float hw, hd, rad, forkDist = 12f, endH;
        public bool round, straight, branch;
        public Color color = Color.white;
        public Trail trail;

        public void Orient(Vector2 f)
        {
            fwd = f.normalized;
            right = new Vector2(fwd.y, -fwd.x);
            entry = pos + fwd * hd;
        }

        public float SD(Vector2 p)
        {
            if (round) return (p - pos).magnitude - rad;
            Vector2 d = p - pos;
            float dx = Mathf.Abs(Vector2.Dot(d, right)) - hw, dz = Mathf.Abs(Vector2.Dot(d, fwd)) - hd;
            var o = new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dz, 0f));
            return o.magnitude + Mathf.Min(Mathf.Max(dx, dz), 0f);
        }
    }

    sealed class Trail
    {
        public string name;
        public Pad target;
        public readonly List<Vector2> p = new List<Vector2>();
        public readonly List<float> half = new List<float>();
        public float len;
        public float[] h;
        public Trail from;           // тропа, на конце которой начинается эта (старт по высоте)
        public float grade;          // свой лимит уклона (0 = trailMaxGrade)
        public float bank = -1f;     // свой откос (< 0 = trailBank)
    }

    sealed class River
    {
        public readonly Cfg c;
        public readonly List<Vector2> p = new List<Vector2>();
        public readonly List<float> s = new List<float>();
        public float lipS, L, bridgeS = -999f;
        public Vector2 lip, lipDir;

        public River(Cfg c) { this.c = c; }

        // чаша у подножия водопада: плавный колокол на s = 2.6
        public static float PoolK(float t) { float u = (t - 2.6f) / 2.4f; return Mathf.Exp(-u * u); }

        // перекаты: светлые полосы пены поперёк реки (пенный круг под водопадом считается отдельно, по Layout.foamC)
        public float[] FoamSpots() { return new[] { 0.42f * lipS, 0.74f * lipS }; }

        public float HW(float t)
        {
            // у подножия горы чаша, ниже по течению вода сразу широкая
            float w = Mathf.Lerp(c.sourceBasinWidth * 0.5f, c.riverWidth * 0.5f, SS(1.5f, 14f, t));
            w += 0.5f * PoolK(t);
            w *= 1f + c.widthVar * Mathf.Sin(t * 0.11f + 1.3f) * SS(16f, 36f, t) * SS(c.bridgeReach, c.bridgeReach + 14f, Mathf.Abs(t - bridgeS));
            w *= Mathf.Lerp(1f, 1.25f, SS(lipS - 26f, lipS, t));
            return w;
        }

        // ширина пологого берега: near = положительная сторона (восток при течении на юг)
        public float Wall(float t, bool near)
        {
            float w = c.valleyWall * (near ? 1f : 0.8f);
            return w * Mathf.Lerp(1f, 1.35f, SS(lipS - 24f, lipS, t));
        }

        public float WaterY(float t)
        {
            return c.waterValley - 0.7f * SS(0.08f * lipS, lipS, t) - 0.3f * SS(lipS - 30f, lipS, t);
        }

        public float Depth(float t)
        {
            float d = c.depthValley + 0.35f * PoolK(t);
            return Mathf.Lerp(d, 0.3f, SS(lipS - 14f, lipS - 1f, t));
        }

        // индекс точки по длине дуги
        public int Index(float t)
        {
            int lo = 0, hi = s.Count - 1;
            while (lo < hi) { int mid = (lo + hi + 1) / 2; if (s[mid] <= t) lo = mid; else hi = mid - 1; }
            return lo;
        }

        public Vector2 Dir(int i)
        {
            Vector2 d = p[Mathf.Min(i + 1, p.Count - 1)] - p[Mathf.Max(i - 1, 0)];
            return d.sqrMagnitude < 1e-8f ? new Vector2(0f, -1f) : d.normalized;
        }
    }

    // островок посреди русла: вытянут вдоль течения (1.4 : 0.85)
    sealed class Islet
    {
        public Vector2 pos, dir;
        public float r, topY;
    }

    // скальный массив: выпуклый многогранник в плане (пересечение полуплоскостей), грани широкие и плоские.
    // Передняя грань (индекс 0) смотрит к центру острова; в ней ниша и жёлоб водопада.
    sealed class Mtn
    {
        public Vector2 c, front, foot, gate;   // центр, наружу из передней грани, подножие ниши, вход на тропу
        public int n;
        public float[] ca, sa, d;              // нормали граней и расстояния до них от центра
        public Vector2[] v;                    // вершины основания
        public float frontAng, lipY, lipA, nicheLen = 7f, bedY, trailTop;
        public bool hasGate;

        // sd < 0 внутри
        public float SD(Vector2 p)
        {
            float dx = p.x - c.x, dz = p.y - c.y, m = -1e9f;
            for (int i = 0; i < n; i++) { float s = dx * ca[i] + dz * sa[i] - d[i]; if (s > m) m = s; }
            return m;
        }

        // расстояние от центра до линии sd = -depth вдоль луча под углом th (depth < 0 = снаружи)
        public float RayAt(float th, float depth)
        {
            float cx = Mathf.Cos(th), sx = Mathf.Sin(th), best = 1e9f;
            for (int i = 0; i < n; i++)
            {
                float k = cx * ca[i] + sx * sa[i];
                if (k < 0.0001f) continue;
                float r = (d[i] - depth) / k;
                if (r < best) best = r;
            }
            return best;
        }

        // вершины основания, сдвинутого наружу на off
        public Vector2[] Verts(float off)
        {
            var res = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                float di = d[i] + off, dj = d[j] + off;
                float det = ca[i] * sa[j] - sa[i] * ca[j];
                if (Mathf.Abs(det) < 1e-5f) det = 1e-5f;
                res[i] = c + new Vector2((di * sa[j] - dj * sa[i]) / det, (ca[i] * dj - ca[j] * di) / det);
            }
            return res;
        }
    }

    sealed class Layout
    {
        public Cfg c;
        public Noise n;
        public Outline outline;
        public float terrainSize, half;
        public readonly List<Pad> pads = new List<Pad>();
        public readonly List<Trail> trails = new List<Trail>();
        public Pad forest, bridge, lookoutPad, landingPad;
        public Vector2 gate, landing, lookout, lakePos;
        public bool deckOn;                                   // деревянный балкон смотровой
        public float deckX0, deckX1, deckZ, deckHW, deckTop, deckStepD;   // x0 = восточный край (вход), x1 = дальний западный край, z = ось, hw = полуширина
        public int deckSteps;
        public Vector2 deckEnd;                               // дальний конец настила (x1, z)
        public River river;
        public PondPlan pond;                                 // пруд (null, пока buildPond выключена)
        public List<Vector2> ridge;
        public Mtn mtn;
        public Vector2 foamC;
        public readonly List<Islet> islets = new List<Islet>();
        public readonly List<RiverObst> obst = new List<RiverObst>();   // валуны в русле: следы течения и пена (LocationBuilderTester.River.cs)
        public readonly List<string> warn = new List<string>();
    }

    static Layout BuildLayout(Cfg c)
    {
        var L = new Layout { c = c };
        L.n = new Noise(c.seed);
        L.outline = new Outline(c, L.n);
        L.terrainSize = c.autoTerrainSize ? Mathf.Ceil((L.outline.maxExt + 8f) * 2f) : c.terrainSize;
        L.half = L.terrainSize * 0.5f;
        L.lakePos = c.lakePos;

        // площадки (как SolveLayout)
        float cr = c.centerRadius;
        for (int i = 0; i < c.pads.Count; i++)
        {
            var d = c.pads[i];
            var p = new Pad { name = d.name, parent = d.parent, forkDist = d.forkDistance, round = d.round, straight = d.straight, color = d.gizmoColor };
            p.hw = d.round ? d.size.x : d.size.x * 0.5f;
            p.hd = d.round ? d.size.x : d.size.y * 0.5f;
            p.rad = d.round ? d.size.x : Mathf.Max(p.hw, p.hd);
            float a = d.angle * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
            float dist = Mathf.Max(d.distance, cr + 0.5f + p.hd);
            for (int it = 0; it < 120; it++)
            {
                Vector2 pos = dir * dist;
                bool ok = true;
                for (int j = 0; j < L.pads.Count; j++)
                    if ((pos - L.pads[j].pos).magnitude < p.rad + L.pads[j].rad + 6f) { ok = false; break; }
                if (ok) break;
                dist += 0.5f;
            }
            p.pos = dir * dist;
            p.Orient(-dir);
            if (p.round && L.forest == null) L.forest = p;
            if (p.name == "Bridge") L.bridge = p;
            L.pads.Add(p);
            if (L.outline.R(Mathf.Atan2(p.pos.y, p.pos.x)) - p.pos.magnitude < p.rad + 6f)
                L.warn.Add("Площадка " + p.name + " слишком близко к краю острова: увеличь Полуразмер.");
        }

        L.gate = L.outline.Pt(Mathf.PI * 0.5f);   // ворота: северный край, x = 0

        if (c.river) BuildRiver(L);
        BuildTrails(L);
        PondPlanBase(L);
        return L;
    }

    static void BuildRiver(Layout L)
    {
        var c = L.c; var o = L.outline;
        BuildMountain(L);
        var mt = L.mtn;
        var r = new River(c);
        float hwV = c.riverWidth * 0.5f;
        float rb = L.bridge != null ? (L.bridge.pos - L.bridge.fwd * L.bridge.hd).x - c.bridgeGap - hwV : -60f;
        Vector2 P0 = mt.foot + mt.front * 0.4f;     // начало воды: у подножия передней грани
        Vector2 o1 = P0 + mt.front * 5f;
        float zIn = c.bridgeReach * 2f;
        var ctrl = new List<Vector2>
        {
            P0, o1,
            new Vector2(Mathf.Lerp(o1.x, rb, 0.3f), Mathf.Lerp(o1.y, zIn, 0.4f)),
            new Vector2(Mathf.Lerp(o1.x, rb, 0.75f), Mathf.Lerp(o1.y, zIn, 0.8f)),
            new Vector2(rb, zIn),
            new Vector2(rb, c.bridgeReach),
            new Vector2(rb, 0f),
            new Vector2(rb, -c.bridgeReach),
            new Vector2(rb - 3f, -30f),
            new Vector2(rb + 2f, -52f),
            new Vector2(rb - 4f, -(o.maxExt + 20f))
        };
        var pts = Resample(CatmullDense(ctrl, 12), 1f);

        // меандр: гасится у чаши и на прямом участке у моста
        var cum = new float[pts.Count];
        for (int i = 1; i < pts.Count; i++) cum[i] = cum[i - 1] + (pts[i] - pts[i - 1]).magnitude;
        var np = new Vector2[pts.Count];
        for (int i = 0; i < pts.Count; i++)
        {
            Vector2 tan = (pts[Mathf.Min(i + 1, pts.Count - 1)] - pts[Mathf.Max(i - 1, 0)]).normalized;
            Vector2 nrm = new Vector2(-tan.y, tan.x);
            float env = SS(6f, 22f, cum[i]) * SS(c.bridgeReach, c.bridgeReach + 16f, Mathf.Abs(pts[i].y));
            float off = (L.n.P(cum[i] * 0.045f, 5.5f, 690f) * 2f - 1f) * c.riverMeander * env;
            np[i] = pts[i] + nrm * off;
        }
        for (int pass = 0; pass < 3; pass++)
        {
            var t = (Vector2[])np.Clone();
            for (int i = 1; i < np.Length - 1; i++) np[i] = 0.25f * t[i - 1] + 0.5f * t[i] + 0.25f * t[i + 1];
        }

        // обрезка по контуру: река кончается на краю острова
        int cut = -1;
        for (int i = 10; i < np.Length; i++) if (!o.Inside(np[i].x, np[i].y)) { cut = i; break; }
        if (cut < 0) { L.warn.Add("Река не дошла до края острова: увеличь остров или измени точки."); cut = np.Length - 1; }
        Vector2 a = np[cut - 1], b = np[cut];
        for (int it = 0; it < 12; it++)
        {
            Vector2 m = (a + b) * 0.5f;
            if (o.Inside(m.x, m.y)) a = m; else b = m;
        }
        for (int i = 0; i < cut; i++) r.p.Add(np[i]);
        r.p.Add(a);
        r.lip = a;
        r.lipDir = (a - np[cut - 2]).normalized;
        r.p.Add(a + r.lipDir * 6f);   // продолжение за кромку (там дырка в Terrain)
        r.s.Add(0f);
        for (int i = 1; i < r.p.Count; i++) r.s.Add(r.s[i - 1] + (r.p[i] - r.p[i - 1]).magnitude);
        r.lipS = r.s[r.p.Count - 2];
        r.L = r.s[r.p.Count - 1];
        if (L.bridge != null)
        {
            float bz = float.MaxValue;
            for (int i = 0; i < r.p.Count; i++)
            {
                float dz = Mathf.Abs(r.p[i].y - L.bridge.pos.y);
                if (dz < bz) { bz = dz; r.bridgeS = r.s[i]; }
            }
        }
        L.river = r;
        BuildIslets(L);
        if (c.sealSource) BuildRidge(L);

        if (r.lipS < 60f) L.warn.Add("Река короткая: до края только " + r.lipS.ToString("0") + " u. Подвинь гору или увеличь остров.");

        // мост через реку, посадка на дальнем берегу, смотровая
        if (L.bridge != null)
        {
            float landX = rb - hwV - c.valleyWall * 0.7f - 0.5f;
            L.landing = new Vector2(landX, L.bridge.pos.y);
            Vector2 lk = PlaceLookout(L, landX);
            L.lookout = lk;
            L.lookoutPad = new Pad { name = "Lookout", pos = lk, hw = 2f, hd = 2f, rad = c.lookoutRadius, straight = true, endH = c.lookoutHeight };
            L.lookoutPad.Orient(L.landing - lk);
            L.landingPad = new Pad { name = "Landing", pos = L.landing, hw = 3f, hd = 3f, rad = 3f, round = true };
        }
    }

    // ---- смотровая: деревянный балкон на западной кромке ----

    // x кромки острова на западе при данном z (идём от центра на запад до границы контура)
    static float EdgeWest(Outline o, float z)
    {
        float lo = 0f, hi = -(o.maxExt + 12f);
        for (int it = 0; it < 26; it++)
        {
            float m = (lo + hi) * 0.5f;
            if (o.Inside(m, z)) lo = m; else hi = m;
        }
        return lo;
    }

    // ширина выравнивания земли вокруг площадки (не меньше полуширины настила + запас)
    static float LookRad(Layout L)
    {
        return L.deckOn ? Mathf.Max(L.c.lookoutRadius, L.deckHW + 1.5f) : L.c.lookoutRadius;
    }

    // расстояние до смотровой: до точки (старый маркер) или до оси «вход -> дальний край настила» (балкон)
    static float LookDist(Layout L, Vector2 q)
    {
        if (!L.deckOn) return Vector2.Distance(q, L.lookout);
        Vector2 a = L.lookout, ab = L.deckEnd - a;
        float l2 = ab.sqrMagnitude;
        float t = l2 > 1e-4f ? Mathf.Clamp01(Vector2.Dot(q - a, ab) / l2) : 0f;
        return Vector2.Distance(q, a + ab * t);
    }

    // центр площадки Lookout (конец тропы = ступени балкона). Настил выступает за кромку на lookoutOverhang u.
    static Vector2 PlaceLookout(Layout L, float landX)
    {
        var c = L.c; var o = L.outline;
        float zc = L.bridge.pos.y;
        if (!c.lookoutBuild)
        {
            Vector2 oldPos = new Vector2(landX - 7.5f, zc);
            float edgeX = o.R(Mathf.PI);
            float maxX = -(edgeX - c.lookoutRadius - 3f);
            if (oldPos.x < maxX) { oldPos.x = maxX; L.warn.Add("Смотровая сдвинута к центру: у края острова мало места (увеличь «Балкон»)."); }
            return oldPos;
        }
        float hw = Mathf.Clamp(c.lookoutDeckW, 5f, 14f) * 0.5f;
        float minEdge = float.MaxValue;
        for (float zz = zc - hw; zz <= zc + hw + 0.01f; zz += 1f) minEdge = Mathf.Min(minEdge, EdgeWest(o, zz));
        int steps = Mathf.Clamp(c.lookoutSteps, 1, 4);
        const float stepD = 0.9f;
        float x1 = minEdge - Mathf.Clamp(c.lookoutOverhang, 1f, 5f);   // самая западная точка кромки + выступ: за кромкой на всей ширине
        float len = Mathf.Clamp(c.lookoutDeckLen, 5f, 20f);
        float entryMax = landX - 6f;                                    // тропа от моста не короче 6 u
        float entryX = x1 + len + steps * stepD + 0.4f;
        if (entryX > entryMax)
        {
            len = Mathf.Max(5f, len - (entryX - entryMax));
            entryX = x1 + len + steps * stepD + 0.4f;
            if (entryX > entryMax) L.warn.Add("Балкон близко к мосту: тропа к нему короче 6 u. Увеличь остров или «Балкон под смотровую», либо уменьши ступени.");
            else L.warn.Add("Настил балкона укорочен до " + len.ToString("0.0") + " u: за мостом мало места.");
        }
        L.deckOn = true;
        L.deckX1 = x1; L.deckX0 = x1 + len; L.deckZ = zc; L.deckHW = hw;
        L.deckSteps = steps; L.deckStepD = stepD;
        L.deckTop = c.lookoutHeight + Mathf.Clamp(c.lookoutDeckRaise, 0.2f, 1.5f);
        L.deckEnd = new Vector2(x1, zc);
        return new Vector2(entryX - 2f, zc);   // у площадки hd = 2: конец тропы (entry) приходится на подножие ступеней
    }

    // скальный массив в северо-западном углу: выпуклый многогранник, передняя грань смотрит к центру острова
    static void BuildMountain(Layout L)
    {
        var c = L.c; var o = L.outline; var nz = L.n;
        int fn = Mathf.Clamp(c.mountainFacets, 5, 12);
        var m = new Mtn { n = fn, ca = new float[fn], sa = new float[fn], d = new float[fn] };
        Vector2 toC = -c.mountainPos;
        if (toC.sqrMagnitude < 1f) toC = new Vector2(1f, -1f);
        m.frontAng = Mathf.Atan2(toC.y, toC.x);
        float rx = Mathf.Max(3f, c.mountainSize.x * 0.5f), rz = Mathf.Max(3f, c.mountainSize.y * 0.5f);
        for (int i = 0; i < fn; i++)
        {
            float a = m.frontAng + i * Mathf.PI * 2f / fn;
            if (i > 0) a += (nz.P(i * 1.7f + 5f, 11.3f, 930f) - 0.5f) * 0.3f;
            m.ca[i] = Mathf.Cos(a); m.sa[i] = Mathf.Sin(a);
            float sup = Mathf.Sqrt(rx * rx * m.ca[i] * m.ca[i] + rz * rz * m.sa[i] * m.sa[i]);   // опорная функция эллипса rx x rz
            m.d[i] = sup * (0.84f + 0.14f * nz.P(i * 2.3f + 1f, 7.7f, 940f));
        }
        m.c = c.mountainPos;
        Vector2 startPos = m.c;
        for (int it = 0; it < 80; it++)
        {
            m.v = m.Verts(0f);
            bool ok = true;
            for (int k = 0; k < fn && ok; k++)
            {
                Vector2 dv = m.v[k] - m.c;
                Vector2 pp = m.v[k] + (dv.sqrMagnitude > 1e-6f ? dv.normalized : Vector2.up) * 3f;
                if (!o.Inside(pp.x, pp.y)) ok = false;
            }
            if (ok) break;
            m.c -= (m.c.sqrMagnitude > 1e-4f ? m.c.normalized : Vector2.up) * 0.75f;
        }
        m.v = m.Verts(0f);
        if ((m.c - startPos).magnitude > 0.1f)
            L.warn.Add("Гора не помещалась у края и сдвинута к центру на " + (m.c - startPos).magnitude.ToString("0.0") + " u.");
        m.front = new Vector2(m.ca[0], m.sa[0]);
        m.foot = m.c + m.front * m.d[0];
        m.bedY = c.waterValley - (c.depthValley + 0.35f) * 0.95f;
        m.lipY = c.waterValley + Mathf.Clamp(c.fallHeight, 4f, Mathf.Max(4.5f, c.mountainHeight - 2f));
        m.lipA = Mathf.Max(2f, c.cliffSkirt * 0.95f);
        L.mtn = m;
        L.foamC = m.foot + m.front * 1.8f;
    }

    // 2-3 островка в русле: вдали от моста и от истока, ближе к оси реки
    static void BuildIslets(Layout L)
    {
        var c = L.c; var r = L.river;
        int want = Mathf.Clamp(c.islets, 0, 4);
        if (want == 0 || r == null) return;
        float s0 = 34f, s1 = r.lipS - 30f;
        if (s1 - s0 < 12f) return;
        for (int k = 0; k < want; k++)
        {
            float s = Mathf.Lerp(s0, s1, (k + 0.5f) / want) + (L.n.P(k * 3.1f + 2f, 8.2f, 950f) - 0.5f) * 8f;
            if (L.bridge != null && Mathf.Abs(s - r.bridgeS) < 17f) s = r.bridgeS + (s < r.bridgeS ? -17f : 17f);
            if (s < s0 || s > s1) continue;
            int i = r.Index(s);
            Vector2 dv = r.Dir(i);
            Vector2 nr = new Vector2(-dv.y, dv.x);
            float hw = r.HW(s);
            float rad = 1.5f + 0.7f * L.n.P(k * 1.9f + 4f, 3.3f, 960f);
            float off = (L.n.P(k * 2.3f + 9f, 6.1f, 970f) * 2f - 1f) * Mathf.Max(0f, hw * 0.25f - rad * 0.2f);
            L.islets.Add(new Islet { pos = r.p[i] + nr * off, dir = dv, r = rad, topY = r.WaterY(s) + 0.55f });
        }
    }

    // Хребет от горы к ближайшему краю острова. Вместе с горой и рекой он делит остров на две части без прохода:
    // гору нельзя обойти ни через воду, ни по полоске суши между скалой и краем.
    static void BuildRidge(Layout L)
    {
        var c = L.c; var o = L.outline; var r = L.river;
        if (r == null || r.p.Count < 2) return;
        Vector2 H = L.mtn != null ? L.mtn.c : r.p[0];
        int bk = 0; float bd = float.MaxValue;
        for (int k = 0; k < Outline.M; k++)
        {
            float d = (o.pts[k] - H).sqrMagnitude;
            if (d < bd) { bd = d; bk = k; }
        }
        Vector2 Q = o.pts[bk];
        Vector2 dir = Q - H;
        float len = dir.magnitude;
        if (len < 1f) return;
        dir /= len;
        Vector2 nrm = new Vector2(-dir.y, dir.x);
        float w1 = (L.n.P(H.x * 0.1f, H.y * 0.1f, 850f) * 2f - 1f) * 2.5f;
        float w2 = (L.n.P(Q.x * 0.1f, Q.y * 0.1f, 860f) * 2f - 1f) * 2.5f;
        var ctrl = new List<Vector2>
        {
            H,
            H + dir * (len * 0.33f) + nrm * w1,
            H + dir * (len * 0.66f) + nrm * w2,
            Q + dir * 4f
        };
        L.ridge = Resample(CatmullDense(ctrl, 10), 1f);
        if (len > 40f) L.warn.Add("Гора далеко от края острова (" + len.ToString("0") + " u): хребет получается длинным. Придвинь гору к краю.");
    }

    static void BuildTrails(Layout L)
    {
        var c = L.c;
        float cr = c.centerRadius;
        // тропы от центра
        foreach (var p in L.pads)
        {
            bool isBranch = !string.IsNullOrEmpty(p.parent) && L.pads.Exists(q => q.name == p.parent && q != p);
            if (isBranch) continue;
            Vector2 dir = p.pos.normalized;
            p.trail = MakeTrail(L, p, dir * (cr - 1f), dir, false, "Trail_" + p.name);
            L.trails.Add(p.trail);
        }
        // прямая тропа к воротам
        {
            Vector2 g = L.gate;
            Vector2 dir = g.normalized;
            var gp = new Pad { name = "Gate", pos = g - dir * 1f, hw = 0.5f, hd = 0.5f, rad = 0.5f, straight = true };
            gp.Orient(-dir);
            gp.trail = MakeTrail(L, gp, dir * (cr - 1f), dir, false, "Trail_Gate");
            L.trails.Add(gp.trail);
        }
        // тропа за мостом к смотровой
        if (L.lookoutPad != null)
        {
            Vector2 dir = (L.lookout - L.landing).normalized;
            L.lookoutPad.trail = MakeTrail(L, L.lookoutPad, L.landing, dir, false, "Trail_Lookout");
            L.trails.Add(L.lookoutPad.trail);
        }
        // ответвления
        foreach (var p in L.pads)
        {
            if (string.IsNullOrEmpty(p.parent)) continue;
            Pad par = L.pads.Find(q => q.name == p.parent && q != p);
            if (par == null || par.trail == null) continue;
            var pt = par.trail;
            int fi = Mathf.RoundToInt(p.forkDist - (cr - 1f));
            fi = Mathf.Clamp(fi, 2, Mathf.Max(2, pt.p.Count - 8));
            Vector2 fork = pt.p[fi];
            Vector2 tan = (pt.p[Mathf.Min(fi + 1, pt.p.Count - 1)] - pt.p[fi - 1]).normalized;
            p.branch = true;
            p.fork = fork;
            p.forkTan = tan;
            p.Orient(fork - p.pos);
            p.trail = MakeTrail(L, p, fork, tan, true, "Trail_" + p.name);
            L.trails.Add(p.trail);
        }
        if (c.mountainTrail && L.mtn != null) BuildMountainTrail(L);
    }

    // Тропа на плато: ответвление от тропы к воротам к восточному подножию горы, дальше пологий подъём по краю горы
    // против часовой стрелки (восток -> север -> запад). Строится ПОСЛЕДНЕЙ, чтобы не менять rng остальных троп.
    static void BuildMountainTrail(Layout L)
    {
        var c = L.c; var m = L.mtn;
        Trail gateTr = L.trails.Find(q0 => q0.name == "Trail_Gate");
        if (gateTr == null || gateTr.p.Count < 14) return;

        float th0 = m.frontAng + 1.15f;
        Vector2 dir0 = new Vector2(Mathf.Cos(th0), Mathf.Sin(th0));
        Vector2 foot0 = m.c + dir0 * (m.RayAt(th0, 0f) + 1.6f);
        if (!L.outline.Inside(foot0.x, foot0.y)) return;

        var head = new Pad { name = "MtnHead", parent = "Gate", pos = foot0, hw = 0.5f, hd = 0.5f, rad = 0.5f, branch = true, endH = c.apronH * 0.85f };
        int fi = Mathf.Clamp(Mathf.RoundToInt(foot0.y - gateTr.p[0].y), 6, gateTr.p.Count - 10);
        Vector2 fork = gateTr.p[fi];
        Vector2 tan = (gateTr.p[fi + 1] - gateTr.p[fi - 1]).normalized;
        head.fork = fork; head.forkTan = tan;
        head.Orient(fork - foot0);
        var link = MakeTrail(L, head, fork, tan, true, "Trail_MountainLink");
        L.trails.Add(link);
        m.gate = foot0; m.hasGate = true;

        const int steps = 240;
        var raw = new List<Vector2> { link.p[link.p.Count - 1] };
        for (int k = 1; k <= steps; k++)
        {
            float u = k / (float)steps;
            float th = th0 + 3.5f * u;
            float depth = Mathf.Lerp(-1.2f, c.cliffSkirt + 1.6f, 0.5f * SS(0f, 1f, u) + 0.5f * u);
            raw.Add(m.c + new Vector2(Mathf.Cos(th), Mathf.Sin(th)) * m.RayAt(th, depth));
        }
        var pts = Resample(raw, 1f);
        if (pts.Count < 12) return;
        for (int pass = 0; pass < 3; pass++)
        {
            var tmp = pts.ToArray();
            for (int i = 1; i < pts.Count - 1; i++) pts[i] = 0.25f * tmp[i - 1] + 0.5f * tmp[i] + 0.25f * tmp[i + 1];
        }
        Vector2 pe = pts[pts.Count - 1];
        float topH = c.mountainHeight * (0.86f + 0.28f * L.n.P(pe.x * 0.07f, pe.y * 0.07f, 800f));
        var top = new Pad { name = "MtnTop", pos = pe, hw = 0.5f, hd = 0.5f, rad = 0.5f, endH = topH };
        var tr = new Trail { name = "Trail_Mountain", target = top, from = link, bank = c.mountainTrailBank };
        float len = 0f;
        for (int i = 1; i < pts.Count; i++) len += (pts[i] - pts[i - 1]).magnitude;
        for (int i = 0; i < pts.Count; i++) { tr.p.Add(pts[i]); tr.half.Add(c.mountainTrailWidth * 0.5f); }
        tr.len = len;
        tr.grade = Mathf.Max(c.mountainTrailGrade, 1.1f * Mathf.Max(1f, topH - head.endH) / Mathf.Max(8f, len));
        L.trails.Add(tr);
        m.trailTop = topH;
    }

    // порт LocationBuilder.Gen.MakeTrail (без via)
    static Trail MakeTrail(Layout L, Pad target, Vector2 start, Vector2 startDir, bool branch, string nm)
    {
        var c = L.c;
        var tr = new Trail { name = nm, target = target };
        Vector2 entry = target.entry, fwd = target.fwd;
        var ctrl = new List<Vector2> { start };
        float direct = Vector2.Distance(start, entry);
        if (direct > 12f)
        {
            Vector2 a = start;
            if (branch) { a = start + startDir * 6f; ctrl.Add(a); }
            float appr = Mathf.Min(5f, direct * 0.3f);
            Vector2 b = entry + fwd * appr;
            int n = Mathf.FloorToInt(Vector2.Distance(a, b) / 8f);
            for (int k = 1; k <= n; k++) ctrl.Add(Vector2.Lerp(a, b, k / (float)(n + 1)));
            ctrl.Add(b);
        }
        ctrl.Add(entry);

        var pts = Resample(CatmullDense(ctrl, 12), 1f);
        if (Vector2.Distance(pts[pts.Count - 1], entry) > 0.3f || pts.Count < 2) pts.Add(entry);
        else pts[pts.Count - 1] = entry;

        int cnt = pts.Count;
        var cum = new float[cnt];
        for (int i = 1; i < cnt; i++) cum[i] = cum[i - 1] + (pts[i] - pts[i - 1]).magnitude;
        float len = cum[cnt - 1];

        float seedOff = (float)L.n.rng.NextDouble() * 500f;
        var np = new Vector2[cnt];
        for (int i = 0; i < cnt; i++)
        {
            Vector2 tan = (pts[Mathf.Min(i + 1, cnt - 1)] - pts[Mathf.Max(i - 1, 0)]).normalized;
            Vector2 nrm = new Vector2(-tan.y, tan.x);
            float env = SS(0f, branch ? 10f : 6f, cum[i]) * SS(0f, 6f, len - cum[i]);
            float off = target.straight ? 0f : (L.n.P(cum[i] * 0.07f, seedOff, 0f) * 2f - 1f) * c.trailMeander * env;
            np[i] = pts[i] + nrm * off;
        }

        for (int i = 0; i < cnt; i++)
        {
            Vector2 p = np[i];
            foreach (var q in L.pads)
            {
                if (q == target || target.straight) continue;
                float R = q.rad + 3.5f;
                Vector2 dv = p - q.pos;
                float dm = dv.magnitude;
                if (dm >= R || dm < 1e-3f) continue;
                float w = SS(0f, 8f, cum[i]) * SS(0f, 8f, len - cum[i]);
                if (branch) w *= SS(0.05f * len, 0.3f * len, cum[i]);
                p += dv / dm * (R - dm) * w;
            }
            np[i] = p;
        }

        for (int pass = 0; pass < 3; pass++)
        {
            var tmp = (Vector2[])np.Clone();
            for (int i = 1; i < cnt - 1; i++) np[i] = 0.25f * tmp[i - 1] + 0.5f * tmp[i] + 0.25f * tmp[i + 1];
        }

        float Ltot = 0f;
        for (int i = 1; i < cnt; i++) Ltot += (np[i] - np[i - 1]).magnitude;
        float run = 0f;
        float baseHalf = c.trailWidth * 0.5f;
        float padHalf = target.round ? 3.0f : c.trailWidthAtPad * 0.5f;
        float widen = target.round ? 14f : 8f;
        for (int i = 0; i < cnt; i++)
        {
            if (i > 0) run += (np[i] - np[i - 1]).magnitude;
            tr.p.Add(np[i]);
            tr.half.Add(Mathf.Lerp(baseHalf, padHalf, SS(Ltot - widen, Ltot, run)));
        }
        tr.len = Ltot;
        return tr;
    }

    // =====================================================================================================
    //  РЕЛЬЕФ
    // =====================================================================================================

    sealed class Field
    {
        public Grid g;
        public float[] H, edge, dR, sR, side, B, dT, tW, tH, massH, forestW, dRg, mSd, tBk;
        public bool[] inside;
        public float minH, maxH, lakeWaterY, lakeWaterR;
    }

    // 0..1 -> 0..1 ступенями: у каждой крутой уступ и более пологая полка (слои картона)
    static float Terr(float x, int steps)
    {
        steps = Mathf.Max(1, steps);
        float y = Mathf.Clamp01(x) * steps;
        float k = Mathf.Floor(y);
        if (k >= steps) return 1f;
        float f = y - k;
        float rise = SS(0f, 0.4f, f) * 0.86f + f * 0.14f;
        return (k + rise) / steps;
    }

    // высота скального «стола»: sd < 0 внутри. Снаружи осыпь, внутри обрыв ступенями до top
    static float MesaH(Cfg c, float sd, float top, float groove)
    {
        if (sd >= 0f) return c.apronH * (1f - SS(0f, 5.5f, sd));
        float x0 = Mathf.Clamp01(-sd / Mathf.Max(1f, c.cliffSkirt));
        float x = Mathf.Clamp01(x0 + groove * SS(0.04f, 0.3f, x0));
        return c.apronH + (top - c.apronH) * Terr(x, c.terraces);
    }

    // яма под пруд: ровное дно и ступенчатые уступы от кромки (кромка на уровне земли)
    static float LakeBowl(Cfg c, float dl)
    {
        float R = c.lakeRadius, Rb = R * 0.42f;
        if (dl >= R) return 0f;
        float t = Mathf.Clamp01((R - dl) / Mathf.Max(0.5f, R - Rb));
        return -c.lakeDepth * Terr(t, 3);
    }

    static void GradeTrails(Layout L, Grid g, float[] B)
    {
        var c = L.c;
        foreach (var tr in L.trails)
        {
            int cnt = tr.p.Count;
            var h = new float[cnt];
            var cum = new float[cnt];
            for (int i = 1; i < cnt; i++) cum[i] = cum[i - 1] + (tr.p[i] - tr.p[i - 1]).magnitude;
            float len = cum[cnt - 1];
            for (int i = 0; i < cnt; i++) h[i] = g.At(B, tr.p[i].x, tr.p[i].y);

            int r = Mathf.Max(1, Mathf.RoundToInt(c.trailSmooth * 0.25f));
            for (int pass = 0; pass < 3; pass++)
            {
                var t = (float[])h.Clone();
                for (int i = 0; i < cnt; i++)
                {
                    float sum = 0f;
                    for (int k = -r; k <= r; k++) sum += t[Mathf.Clamp(i + k, 0, cnt - 1)];
                    h[i] = sum / (2 * r + 1);
                }
            }

            float startVal = 0f;
            Pad tp = tr.target;
            if (tp != null && tp.branch)
            {
                Trail par = L.trails.Find(q => q.name == "Trail_" + tp.parent && q != tr);
                if (par != null && par.h != null)
                {
                    float bd = float.MaxValue; int bi = 0;
                    for (int i = 0; i < par.p.Count; i++)
                    {
                        float dd = (par.p[i] - tp.fork).sqrMagnitude;
                        if (dd < bd) { bd = dd; bi = i; }
                    }
                    startVal = par.h[bi];
                }
            }
            if (tr.name == "Trail_Lookout") startVal = 0f;
            if (tr.from != null && tr.from.h != null) startVal = tr.from.h[tr.from.h.Length - 1];
            float endVal = tp != null && tp.round
                ? (g.At(B, tr.p[cnt - 1].x, tr.p[cnt - 1].y) + g.At(B, tr.p[cnt - 1].x + 3f, tr.p[cnt - 1].y) + g.At(B, tr.p[cnt - 1].x - 3f, tr.p[cnt - 1].y)
                   + g.At(B, tr.p[cnt - 1].x, tr.p[cnt - 1].y + 3f) + g.At(B, tr.p[cnt - 1].x, tr.p[cnt - 1].y - 3f)) / 5f
                : (tp != null ? tp.endH : 0f);

            float bl = Mathf.Min(14f, len * 0.45f);
            float d0 = startVal - h[0], d1 = endVal - h[cnt - 1];
            for (int i = 0; i < cnt; i++)
                h[i] += d0 * (1f - SS(0f, bl, cum[i])) + d1 * (1f - SS(0f, bl, len - cum[i]));

            float gr = Mathf.Max(0.05f, tr.grade > 0f ? tr.grade : c.trailMaxGrade);
            for (int it = 0; it < 4; it++)
            {
                h[0] = startVal;
                for (int i = 1; i < cnt; i++)
                {
                    float st = cum[i] - cum[i - 1];
                    h[i] = Mathf.Clamp(h[i], h[i - 1] - gr * st, h[i - 1] + gr * st);
                }
                h[cnt - 1] = endVal;
                for (int i = cnt - 2; i >= 0; i--)
                {
                    float st = cum[i + 1] - cum[i];
                    h[i] = Mathf.Clamp(h[i], h[i + 1] - gr * st, h[i + 1] + gr * st);
                }
            }
            for (int i = 0; i < cnt; i++) h[i] = Mathf.Max(0f, h[i]);

            float worst = 0f;
            for (int i = 1; i < cnt; i++) worst = Mathf.Max(worst, Mathf.Abs(h[i] - h[i - 1]) / Mathf.Max(0.01f, cum[i] - cum[i - 1]));
            if (worst > gr * 1.25f) L.warn.Add("Тропа " + tr.name + ": уклон " + worst.ToString("0.00") + " больше лимита " + gr.ToString("0.00") + ".");
            tr.h = h;
        }
    }

    // жёлоб водопада: высота дна (u) на расстоянии a от подножия вглубь горы
    static float ChuteY(Mtn m, float a)
    {
        float slotY = m.lipY - 0.35f;
        if (a >= m.lipA) return slotY;
        return Mathf.Lerp(m.bedY, slotY, SS(0f, m.lipA, a));
    }

    // ниша и жёлоб в передней грани: борозда шириной fallWidth, гладкий крутой спуск от слота-ниши (уровень lipY) до чаши.
    // Слот уходит вглубь горы на nicheLen. Жёлоб всегда ниже ступенчатого склона, поэтому лента воды его не пересекает.
    static float Chute(Cfg c, Mtn m, Vector2 q, float mH)
    {
        Vector2 rel = q - m.foot;
        float a = -(rel.x * m.front.x + rel.y * m.front.y);
        float l = Mathf.Abs(-rel.x * m.front.y + rel.y * m.front.x);
        float hw = c.fallWidth * 0.5f;
        float aEnd = m.lipA + m.nicheLen;
        if (l > hw + 1.0f || a < -0.8f || a > aEnd + 0.5f) return mH;
        float mask = (1f - SS(hw, hw + 0.9f, l)) * SS(-0.3f, 0.4f, a) * (1f - SS(aEnd - 1.5f, aEnd, a));
        if (mask <= 0f) return mH;
        return Mathf.Lerp(mH, Mathf.Min(mH, ChuteY(m, a)), mask);
    }

    // расстояние до центра островка с учётом вытянутости (меньше il.r = внутри)
    static float IsletD(Islet il, Vector2 q, float nz)
    {
        Vector2 dv = q - il.pos;
        float al = (dv.x * il.dir.x + dv.y * il.dir.y) / 1.4f;
        float ac = (-dv.x * il.dir.y + dv.y * il.dir.x) / 0.85f;
        return Mathf.Sqrt(al * al + ac * ac) + (nz - 0.5f) * 0.6f;
    }

    static Field Compute(Layout L)
    {
        var c = L.c; var n = L.n; var o = L.outline;
        var g = new Grid { N = c.heightmapRes, half = L.half, cell = L.terrainSize / (c.heightmapRes - 1) };
        int N = g.N, NN = N * N;
        float half = g.half, cell = g.cell;
        var f = new Field { g = g };
        f.H = new float[NN]; f.edge = new float[NN]; f.dR = new float[NN]; f.sR = new float[NN]; f.side = new float[NN];
        f.B = new float[NN]; f.dT = new float[NN]; f.tW = new float[NN]; f.tH = new float[NN]; f.massH = new float[NN]; f.forestW = new float[NN]; f.dRg = new float[NN];
        f.mSd = new float[NN]; f.tBk = new float[NN];
        f.inside = new bool[NN];
        var mt = L.mtn;

        // ---- расстояние до края острова
        float reachE = c.rimWidth + c.rimFlat + 8f;
        var bestE = new float[NN];
        for (int j = 0; j < N; j++)
            for (int i = 0; i < N; i++)
            {
                int idx = j * N + i;
                bool ins = o.Inside(-half + i * cell, -half + j * cell);
                f.inside[idx] = ins;
                f.edge[idx] = ins ? reachE : -reachE;
                bestE[idx] = float.MaxValue;
            }
        for (int k = 0; k < Outline.M; k++)
            Stamp(g, o.pts[k], o.pts[(k + 1) % Outline.M], reachE, (idx, p, t, d) => { if (d < bestE[idx]) bestE[idx] = d; });
        for (int idx = 0; idx < NN; idx++)
            if (bestE[idx] < reachE) f.edge[idx] = f.inside[idx] ? bestE[idx] : -bestE[idx];

        // ---- расстояние до оси реки, длина дуги, сторона
        var river = L.river;
        float reachR = c.riverWidth * 0.7f + c.valleyWall * 1.4f + c.valleyWidth + 6f;
        for (int i = 0; i < NN; i++) { f.dR[i] = 999f; f.sR[i] = 0f; f.side[i] = 1f; }
        if (river != null)
        {
            for (int k = 0; k < river.p.Count - 1; k++)
            {
                Vector2 ra = river.p[k], rb = river.p[k + 1];
                Vector2 rdir = (rb - ra).normalized;
                float s0 = river.s[k], s1 = river.s[k + 1];
                Stamp(g, ra, rb, reachR, (idx, p, t, d) =>
                {
                    if (d >= f.dR[idx]) return;
                    f.dR[idx] = d;
                    f.sR[idx] = Mathf.Lerp(s0, s1, t);
                    Vector2 q = ra + (rb - ra) * t;
                    f.side[idx] = Mathf.Sign(rdir.x * (p.y - q.y) - rdir.y * (p.x - q.x));
                });
            }
        }

        // ---- расстояние до хребта (запечатывает обход горы)
        float reachG = c.ridgeWidth * 0.5f + c.cliffSkirt * 0.6f + 7f;
        for (int i = 0; i < NN; i++) f.dRg[i] = 999f;
        if (L.ridge != null)
        {
            for (int k = 0; k < L.ridge.Count - 1; k++)
                Stamp(g, L.ridge[k], L.ridge[k + 1], reachG, (idx, p, t, d) => { if (d < f.dRg[idx]) f.dRg[idx] = d; });
        }

        // ---- расстояние до контура горы (sd < 0 внутри)
        for (int j = 0; j < N; j++)
            for (int i = 0; i < N; i++)
                f.mSd[j * N + i] = mt != null ? mt.SD(new Vector2(-half + i * cell, -half + j * cell)) : 999f;

        // ---- шум лугов
        var mN = new float[NN];
        for (int j = 0; j < N; j++)
            for (int i = 0; i < N; i++)
            {
                float x = -half + i * cell, z = -half + j * cell;
                float fq = c.meadowScale;
                float wx = x + (n.P(x * 0.011f, z * 0.011f, 710f) - 0.5f) * 30f;
                float wz = z + (n.P(x * 0.011f, z * 0.011f, 740f) - 0.5f) * 30f;
                mN[j * N + i] = 0.65f * n.P(wx * fq, wz * fq, 700f) + 0.35f * n.P(wx * fq * 2.3f, wz * fq * 2.3f, 760f);
            }
        float mLo = Quantile01(mN, c.meadowFlat), mHi = Quantile01(mN, Mathf.Min(0.98f, c.meadowFlat + 0.38f));
        if (mHi - mLo < 0.02f) mHi = mLo + 0.02f;

        // ---- форма B(x,z)
        Pad forest = L.forest;
        Vector2 fc = forest != null ? forest.pos : Vector2.zero;
        Vector2 axis = fc.sqrMagnitude > 1e-4f ? fc.normalized : Vector2.up;
        Vector2 axisR = new Vector2(axis.y, -axis.x);
        float hwForest = forest != null ? forest.rad : 1f;
        float cr = c.centerRadius;

        for (int j = 0; j < N; j++)
            for (int i = 0; i < N; i++)
            {
                int idx = j * N + i;
                float x = -half + i * cell, z = -half + j * cell;
                Vector2 q = new Vector2(x, z);
                float eIn = Mathf.Max(0f, f.edge[idx]);
                float rC = Mathf.Sqrt(x * x + z * z);

                float valley = 1f;
                if (river != null)
                {
                    float hwr = river.HW(f.sR[idx]);
                    valley = SS(hwr + c.valleyWall, hwr + c.valleyWall + c.valleyWidth, f.dR[idx]);
                }

                // скальная гора: многогранник с обрывом ступенями-пластами (как листы картона) + хребет-запечатыватель
                float mH = 0f;
                {
                    float nA = n.P(x * 0.05f, z * 0.05f, 860f) - 0.5f, nB = n.P(x * 0.17f, z * 0.17f, 870f) - 0.5f;
                    float wobR = nA * 7f + nB * 2.2f;                                      // рваный контур хребта
                    float wobM = nA * 2.4f + nB * 0.9f;                                    // у горы грани остаются плоскими, контур чуть неровный
                    float rockN = 0.86f + 0.28f * n.P(x * 0.07f, z * 0.07f, 800f);        // неровная вершина
                    float grooveN = (n.P(x * 0.45f, z * 0.45f, 810f) - 0.5f) * 0.22f;     // борозды-промоины на стенах
                    if (mt != null)
                    {
                        mH = MesaH(c, f.mSd[idx] + wobM, c.mountainHeight * rockN, grooveN * 0.7f);
                        mH = Chute(c, mt, q, mH);
                    }
                    if (L.ridge != null && f.dRg[idx] < reachG)
                        mH = Mathf.Max(mH, MesaH(c, f.dRg[idx] - (c.ridgeWidth * 0.5f + c.cliffSkirt * 0.6f) + wobR * 0.5f, c.ridgeHeight * rockN, grooveN));
                }

                // лесной массив
                float massif = 0f, massifW = 0f;
                if (forest != null)
                {
                    Vector2 rel = q - fc;
                    float d = rel.magnitude;
                    float rad = hwForest;
                    float along = Vector2.Dot(rel, axis);
                    float lateral = Mathf.Abs(Vector2.Dot(rel, axisR));
                    float dEff = Mathf.Lerp(lateral, d, SS(-0.25f * rad, 0.25f * rad, along));
                    float mask = 1f - SS(rad * 0.9f, rad * 0.9f + c.forestSlope, dEff);
                    float ramp = SS(-1.5f * rad, -0.1f * rad, along);
                    float t2 = SS(rad * 0.25f, rad * 0.25f + c.forestTierSlope, along) * (1f - SS(rad * 0.7f, rad * 1.3f, lateral));
                    float body = mask * ramp;
                    massif = body * Mathf.Lerp(c.forestTier1, c.forestTier2, t2);
                    massif *= 0.94f + 0.12f * n.P(x * 0.05f, z * 0.05f, 770f);
                    massif += c.forestRelief * (n.P(x * 0.08f, z * 0.08f, 300f) * 2f - 1f) * body;
                    massif = Mathf.Max(0f, massif);
                    massifW = mask;
                }

                float meadow = c.meadowSwell * SS(mLo, mHi, mN[idx]) * (1f - 0.75f * massifW) * SS(cr + 2f, cr + 16f, rC) * (1f - SS(0f, 3f, mH));
                float bowl = c.bowlHeight * SS(cr + 6f, 70f, rC);

                float rim = 0f;
                if (c.rimHeight > 0.01f)
                {
                    float rt = 1f - Mathf.Clamp01(Mathf.Max(0f, eIn - c.rimFlat) / Mathf.Max(1f, c.rimWidth));
                    float prof = rt * rt * (3f - 2f * rt);
                    float rn = (0.65f + 0.7f * n.P(x * 0.05f, z * 0.05f, 520f)) * (1f + 0.25f * (n.P(x * 0.11f, z * 0.11f, 260f) * 2f - 1f));
                    float gateF = SS(c.gateClear * 0.6f, c.gateClear * 1.6f, Vector2.Distance(q, L.gate));
                    float lookF = L.lookoutPad != null ? SS(7f, 15f, LookDist(L, q)) : 1f;
                    float riverF = river != null ? SS(5f, 16f, f.dR[idx]) : 1f;
                    rim = c.rimHeight * rn * prof * gateF * lookF * riverF;
                }

                float shape = Mathf.Max(rim, massif) + 0.35f * Mathf.Min(rim, massif);
                float h = (meadow + bowl) * valley + shape * Mathf.Lerp(1f, valley, 0.7f) + mH;

                float dl = Vector2.Distance(q, L.lakePos) + (n.P(x * 0.12f, z * 0.12f, 880f) - 0.5f) * 1.8f;   // рваная кромка ямы
                if (dl < c.lakeRadius + c.lakeShore)
                {
                    h *= SS(c.lakeRadius, c.lakeRadius + c.lakeShore, dl);
                    if (dl < c.lakeRadius) h = LakeBowl(c, dl);
                }

                f.B[idx] = h; f.massH[idx] = mH; f.forestW[idx] = massifW;
            }
        Blur(f.B, N, c.blur);

        // ---- тропы
        GradeTrails(L, g, f.B);
        for (int i = 0; i < NN; i++) { f.dT[i] = 999f; f.tW[i] = 0f; f.tH[i] = 0f; f.tBk[i] = c.trailBank; }
        float reachT = c.trailBank + c.trailFlat + 8f;
        foreach (var tr in L.trails)
        {
            if (tr.h == null) continue;
            float bk = tr.bank > 0f ? tr.bank : c.trailBank;
            for (int k = 0; k < tr.p.Count - 1; k++)
            {
                float ha = tr.h[k], hb = tr.h[k + 1], wa = tr.half[k], wb = tr.half[k + 1];
                Stamp(g, tr.p[k], tr.p[k + 1], reachT, (idx, p, t, d) =>
                {
                    if (d >= f.dT[idx]) return;
                    f.dT[idx] = d; f.tH[idx] = Mathf.Lerp(ha, hb, t); f.tW[idx] = Mathf.Lerp(wa, wb, t); f.tBk[idx] = bk;
                });
            }
        }

        // ---- сборка высот: B -> тропы -> площадки -> центр -> смотровая -> река
        float minH = float.MaxValue, maxH = float.MinValue;
        for (int j = 0; j < N; j++)
            for (int i = 0; i < N; i++)
            {
                int idx = j * N + i;
                float x = -half + i * cell, z = -half + j * cell;
                Vector2 q = new Vector2(x, z);
                float h = f.B[idx];

                float tw = Mathf.Max(f.tW[idx], c.trailFlat * 0.5f);
                float wT = 1f - SS(tw, tw + f.tBk[idx], f.dT[idx]);
                if (f.dT[idx] < 900f) h = Mathf.Lerp(h, f.tH[idx], wT);

                float keep = 0f;
                foreach (var p in L.pads)
                {
                    if (p.round) continue;
                    if ((q - p.pos).sqrMagnitude > (p.rad + c.padSkirt + 2f) * (p.rad + c.padSkirt + 2f)) continue;
                    float sd = p.SD(q);
                    h = Mathf.Lerp(h, 0f, 1f - SS(0f, c.padSkirt, sd));
                    keep = Mathf.Max(keep, 1f - SS(0f, 1.6f, sd));
                }
                float rC = Mathf.Sqrt(x * x + z * z);
                h = Mathf.Lerp(h, 0f, 1f - SS(cr, cr + 6f, rC));

                if (L.lookoutPad != null)
                {
                    float dlk = LookDist(L, q);
                    float lkR = LookRad(L);
                    h = Mathf.Lerp(h, c.lookoutHeight, 1f - SS(lkR, lkR + 6f, dlk));
                    keep = Mathf.Max(keep, 1f - SS(lkR, lkR + 1.5f, dlk));
                    float dld = Vector2.Distance(q, L.landing);
                    h = Mathf.Lerp(h, 0f, 1f - SS(3f, 8f, dld));
                    keep = Mathf.Max(keep, 1f - SS(3f, 4.5f, dld));
                }

                float dlk2 = Vector2.Distance(q, L.lakePos) + (n.P(x * 0.12f, z * 0.12f, 880f) - 0.5f) * 1.8f;
                if (dlk2 < c.lakeRadius) h = LakeBowl(c, dlk2);
                h = PondRaise(L, q, PondDeepen(L, h), n.P(x * 0.2f, z * 0.2f, 890f));   // остров пруда (без пруда ничего не меняет)

                if (river != null && f.dR[idx] < reachR)
                {
                    float t = f.sR[idx], d = f.dR[idx];
                    float hw = river.HW(t);
                    bool near = f.side[idx] > 0f;
                    float wall = river.Wall(t, near);
                    float nLip = n.P(x * 0.31f, z * 0.31f, 655f) - 0.5f;
                    float nW = n.P(x * 0.17f, z * 0.17f, 640f);
                    float wallW = wall * (0.95f + 0.1f * nW);
                    float tt = Mathf.Clamp01((d + nLip * 0.5f - hw) / Mathf.Max(0.3f, wallW));
                    float w = SS(0f, 1f, tt);                 // пологий берег на всей длине реки, без стенок
                    float wy = river.WaterY(t);
                    float dep = river.Depth(t);
                    float bed = wy - dep * (0.92f + 0.16f * n.P(x * 0.12f, z * 0.12f, 610f));
                    bed += 0.2f * SS(hw * 0.5f, hw, d);       // небольшая мель у самого берега
                    if (c.pebbleBars)                          // галечные косы: дно поднимается почти до воды
                        bed += 0.75f * dep * SS(0.60f, 0.72f, n.P(x * 0.075f, z * 0.075f, 620f)) * SS(hw * 0.35f, hw * 0.85f, d);
                    for (int ik = 0; ik < L.islets.Count; ik++)   // островки: холмики из высот русла
                    {
                        var il = L.islets[ik];
                        float di = IsletD(il, q, n.P(x * 0.4f, z * 0.4f, 630f));
                        if (di < il.r) bed = Mathf.Max(bed, Mathf.Lerp(bed, il.topY, 1f - SS(il.r * 0.45f, il.r, di)));
                    }
                    float k = (1f - w) * (1f - keep) * SS(-0.4f, 0.8f, f.mSd[idx]);   // скалу горы река не подрезает
                    if (k > 0f) h = Mathf.Lerp(h, bed, k);
                }

                f.H[idx] = h;
                if (h < minH) minH = h;
                if (h > maxH) maxH = h;
            }
        f.minH = minH; f.maxH = maxH;
        f.lakeWaterY = -0.4f * c.lakeDepth;
        // водный радиус: где профиль чаши достигает уровня воды
        float rwA = c.lakeRadius * 0.6f;
        f.lakeWaterR = rwA;
        return f;
    }

    // =====================================================================================================
    //  РАСКРАСКА (splatmap)
    // =====================================================================================================

    const int LayerCount = 8;   // 0 трава, 1 трава светлая (и площадки), 2 тропа, 3 земля, 4 скала, 5 лесная подстилка, 6 песок, 7 тёмная тропа (кромка, колеи)

    static void Mix(float[] w, int layer, float a)
    {
        a = Mathf.Clamp01(a);
        for (int k = 0; k < w.Length; k++) w[k] *= (1f - a);
        w[layer] += a;
    }

    static float[,,] BuildAlpha(Layout L, Field f)
    {
        var c = L.c; var n = L.n; var g = f.g; var river = L.river;
        int A = c.alphaRes;
        var a = new float[A, A, LayerCount];
        float step = L.terrainSize / A;
        var w = new float[LayerCount];
        float cr = c.centerRadius;
        float lw = f.lakeWaterY;
        for (int j = 0; j < A; j++)
        {
            float z = -L.half + (j + 0.5f) * step;
            for (int i = 0; i < A; i++)
            {
                float x = -L.half + (i + 0.5f) * step;
                Vector2 q = new Vector2(x, z);
                float h = g.At(f.H, x, z);
                float gx = (g.At(f.H, x + g.cell, z) - g.At(f.H, x - g.cell, z)) / (2f * g.cell);
                float gz = (g.At(f.H, x, z + g.cell) - g.At(f.H, x, z - g.cell)) / (2f * g.cell);
                float slope = Mathf.Sqrt(gx * gx + gz * gz);
                float edge = g.At(f.edge, x, z);
                float mass = g.At(f.massH, x, z);
                float mS = g.At(f.mSd, x, z);
                float fW = g.At(f.forestW, x, z);

                for (int k = 0; k < LayerCount; k++) w[k] = 0f;
                w[0] = 1f;

                float patch = SS(0.5f, 0.65f, n.P(x * 0.045f, z * 0.045f, 90f)) * 0.7f + SS(1.5f, 4f, h) * 0.35f;
                Mix(w, 1, patch);
                Mix(w, 5, fW * 0.9f);

                float inM = SS(0.4f, 3f, mass);                   // тело горы
                float wRock = SS(0.55f, 0.9f, slope);
                wRock = Mathf.Max(wRock, SS(3f, 6.5f, mass) * 0.85f * SS(0.12f, 0.45f, slope));
                wRock = Mathf.Lerp(wRock, SS(1.1f, 1.9f, slope), inM);   // на горе пологие полки остаются травой, отвесные грани скала
                float wSoil = SS(0.30f, 0.55f, slope) * (1f - wRock) * (1f - inM);
                wSoil = Mathf.Max(wSoil, (1f - SS(0.4f, 1.6f, edge)) * 0.9f);
                Mix(w, 3, wSoil);
                // мох на полках-уступах и осыпь у подножия горы
                float shelf = inM * SS(0.08f, 0.3f, slope) * (1f - SS(0.8f, 1.3f, slope));
                Mix(w, 5, shelf * 0.6f);
                float scree = (1f - SS(0.3f, 5f, mS)) * SS(-0.5f, 0.3f, mS);
                Mix(w, 3, scree * 0.45f);
                Mix(w, 4, scree * SS(0.45f, 0.62f, n.P(x * 0.3f, z * 0.3f, 425f)) * 0.8f);

                // песок: отмели у берегов реки (1-3 u), галечные косы в русле, трава на островках; песок у пруда
                float wSand = 0f, wSpit = 0f, wIsle = 0f;
                if (river != null)
                {
                    float d = g.At(f.dR, x, z), t = g.At(f.sR, x, z);
                    float wy = river.WaterY(t);
                    float hwr = river.HW(t);
                    float reach = hwr + river.Wall(t, true) * 1.2f;
                    wSand = (1f - SS(wy + 0.35f, wy + 1.15f, h)) * (1f - SS(reach, reach * 1.4f, d));
                    if (c.pebbleBars)
                        wSpit = SS(0.60f, 0.72f, n.P(x * 0.075f, z * 0.075f, 620f)) * SS(hwr * 0.35f, hwr * 0.85f, d) * (1f - SS(hwr + 0.3f, hwr + 1.2f, d));
                    for (int ik = 0; ik < L.islets.Count; ik++)
                    {
                        float di = IsletD(L.islets[ik], q, n.P(x * 0.4f, z * 0.4f, 630f));
                        wIsle = Mathf.Max(wIsle, 1f - SS(L.islets[ik].r * 0.5f, L.islets[ik].r * 0.85f, di));
                    }
                }
                float dl = Vector2.Distance(q, L.lakePos);
                if (dl < c.lakeRadius + 2f) wSand = Mathf.Max(wSand, 1f - SS(lw + 0.1f, lw + 0.7f, h));
                Mix(w, 6, wSand);
                Mix(w, 2, wSpit * 0.9f);
                Mix(w, 0, wIsle);
                Mix(w, 4, wRock);
                PondAlpha(L, q, w, h, slope);

                // ---- площадки, центр, тропы: как в старой локации (LocationGen.BuildSplat): ровный цвет, тёмная кромка, колеи, рваный край
                float rC = Mathf.Sqrt(x * x + z * z);
                float sdP = 999f;
                foreach (var p in L.pads)
                {
                    if (p.round || (q - p.pos).sqrMagnitude > (p.rad + 6f) * (p.rad + 6f)) continue;
                    sdP = Mathf.Min(sdP, p.SD(q));
                }
                float padN = (n.P(x * 0.35f, z * 0.35f, 470f) - 0.5f) * 0.7f;
                Mix(w, 1, (1f - SS(-0.2f, 0.5f, sdP + padN)) * 0.9f);

                // центр: утоптанная площадь с тёмным кантом
                Mix(w, 2, 1f - SS(cr - 1.5f, cr + 0.5f, rC));
                Mix(w, 7, (1f - SS(0f, 0.7f, Mathf.Abs(rC - (cr - 0.2f)))) * 0.8f);

                float dT = g.At(f.dT, x, z), tw = g.At(f.tW, x, z);
                if (dT < 900f)
                {
                    float cMask = SS(cr - 0.8f, cr + 0.2f, rC);
                    float padFree = SS(0.2f, 1.0f, sdP);
                    float sdT = dT - tw;                                    // < 0 внутри тропы
                    Mix(w, 2, (1f - SS(0f, 0.5f, sdT)) * cMask);           // ровная тропа с мягким краем 0.5 u
                    if (c.trailEdge)                                         // тёмная кромка
                    {
                        float edgeD = SS(-0.55f, -0.15f, sdT) * (1f - SS(0.1f, 0.45f, sdT)) * cMask * padFree;
                        Mix(w, 7, edgeD * 0.75f);
                    }
                    if (c.trailRuts)                                         // две колеи, рваные по шуму
                    {
                        float inside = 1f - SS(-0.8f, -0.4f, sdT);
                        float rutOff = Mathf.Max(0.45f, tw * 0.45f);
                        float rut = (1f - SS(0.06f, 0.2f, Mathf.Abs(dT - rutOff))) * inside
                                    * SS(0.35f, 0.6f, n.P(x * 0.5f, z * 0.5f, 520f)) * cMask * padFree;
                        Mix(w, 7, rut * 0.5f);
                    }
                }

                float sum = 0f;
                for (int k = 0; k < LayerCount; k++) sum += w[k];
                if (sum < 1e-5f) { w[0] = 1f; sum = 1f; }
                for (int k = 0; k < LayerCount; k++) a[j, i, k] = w[k] / sum;
            }
        }
        return a;
    }

    // =====================================================================================================
    //  ПРОВЕРКА: можно ли обойти гору / перейти реку без моста
    // =====================================================================================================

    // true = обхода нет: от центра до смотровой (за рекой) по суше не добраться.
    // Идём по сетке рельефа (шаг в 2 клетки): проходимы клетки внутри острова, не в воде реки и с уклоном не круче walkSlope.
    static bool CheckBypass(Layout L, Field F, out string msg)
    {
        msg = "";
        if (L.river == null || L.lookoutPad == null) return true;
        var c = L.c; var g = F.g; var r = L.river;
        int N = g.N, st = 2, M = (N - 1) / st + 1;
        float lim = Mathf.Tan(Mathf.Clamp(c.walkSlope, 10f, 80f) * Mathf.Deg2Rad);
        float dist = g.cell * st;
        var ok = new bool[M * M];
        for (int j = 0; j < M; j++)
            for (int i = 0; i < M; i++)
            {
                int idx = (j * st) * N + i * st;
                bool walk = F.inside[idx];
                if (walk && F.dR[idx] < 900f && F.dR[idx] < r.HW(F.sR[idx])) walk = false;   // вода реки
                if (walk && L.ridge != null && F.dRg[idx] < c.ridgeWidth * 0.5f + c.cliffSkirt * 0.6f) walk = false;   // хребет (там же невидимая стенка)
                if (walk && L.mtn != null && F.mSd[idx] < 1.2f) walk = false;   // гора (вокруг подножия невидимая стенка, вход только на тропу)
                ok[j * M + i] = walk;
            }

        Func<float, int> toI = v => Mathf.Clamp(Mathf.RoundToInt((v + g.half) / dist), 0, M - 1);
        int start = toI(0f) + toI(0f) * M;
        int goal = toI(L.lookout.x) + toI(L.lookout.y) * M;
        if (!ok[start] || !ok[goal]) return true;

        var par = new int[M * M];
        for (int k = 0; k < par.Length; k++) par[k] = -1;
        var queue = new Queue<int>();
        par[start] = start;
        queue.Enqueue(start);
        while (queue.Count > 0 && par[goal] < 0)
        {
            int cur = queue.Dequeue();
            int ci = cur % M, cj = cur / M;
            float hc = F.H[(cj * st) * N + ci * st];
            for (int dj = -1; dj <= 1; dj++)
                for (int di = -1; di <= 1; di++)
                {
                    if (di == 0 && dj == 0) continue;
                    int ni = ci + di, nj = cj + dj;
                    if (ni < 0 || nj < 0 || ni >= M || nj >= M) continue;
                    int nidx = nj * M + ni;
                    if (!ok[nidx] || par[nidx] >= 0) continue;
                    float dh = Mathf.Abs(F.H[(nj * st) * N + ni * st] - hc);
                    float dd = (di != 0 && dj != 0 ? 1.4142f : 1f) * dist;
                    if (dh / dd > lim) continue;
                    par[nidx] = cur;
                    queue.Enqueue(nidx);
                }
        }
        if (par[goal] < 0) return true;

        // обход есть: показываем точку пути, ближайшую к истоку
        Vector2 head = r.p[0], bp = Vector2.zero;
        float best = float.MaxValue;
        int w = goal, guard = 0;
        while (w != start && guard++ < M * M)
        {
            Vector2 p = new Vector2(-g.half + (w % M) * dist, -g.half + (w / M) * dist);
            float d = (p - head).magnitude;
            if (d < best) { best = d; bp = p; }
            w = par[w];
        }
        msg = "К смотровой можно дойти по суше без моста (уклон до " + c.walkSlope.ToString("0") + "°). Путь идёт около (" + bp.x.ToString("0") + ", " + bp.y.ToString("0")
            + ") u, в " + best.ToString("0") + " u от истока.";
        return false;
    }

    // тропы (кроме тропы на гору) не должны заходить в воду реки и в гору; мост через реку маркер, не тропа
    static void CheckTrails(Layout L)
    {
        foreach (var tr in L.trails)
        {
            if (tr.name.StartsWith("Trail_Mountain")) continue;
            bool bad = false;
            for (int i = 0; i < tr.p.Count && !bad; i++)
            {
                Vector2 pt = tr.p[i];
                if (L.mtn != null && L.mtn.SD(pt) < 1.5f) bad = true;
                else if (L.river != null && RiverDist(L, pt) < tr.half[i] + 0.3f) bad = true;
            }
            if (bad) L.warn.Add("Тропа " + tr.name + " задевает реку или гору: измени площадки или положение горы.");
        }
    }

    // =====================================================================================================
    //  МЕШИ
    // =====================================================================================================

    sealed class MB
    {
        public readonly List<Vector3> v = new List<Vector3>(), n = new List<Vector3>();
        public readonly List<Vector2> uv = new List<Vector2>();
        public readonly List<List<int>> t = new List<List<int>>();
        public MB(int subs) { for (int i = 0; i < subs; i++) t.Add(new List<int>()); }

        public void Tri(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 facing)
        {
            TriUV(sub, a, b, c, Vector2.zero, Vector2.zero, Vector2.zero, facing);
        }

        public void TriUV(int sub, Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc, Vector3 facing)
        {
            Vector3 nr = Vector3.Cross(b - a, c - a);
            if (nr.sqrMagnitude < 1e-12f) return;
            if (Vector3.Dot(nr, facing) < 0f)
            {
                Vector3 tmp = b; b = c; c = tmp;
                Vector2 tu = ub; ub = uc; uc = tu;
                nr = -nr;
            }
            nr.Normalize();
            int i0 = v.Count;
            v.Add(a); v.Add(b); v.Add(c);
            n.Add(nr); n.Add(nr); n.Add(nr);
            uv.Add(ua); uv.Add(ub); uv.Add(uc);
            t[sub].Add(i0); t[sub].Add(i0 + 1); t[sub].Add(i0 + 2);
        }

        public void Quad(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 facing)
        {
            Tri(sub, a, b, c, facing);
            Tri(sub, a, c, d, facing);
        }

        public void QuadUV(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud, Vector3 facing)
        {
            TriUV(sub, a, b, c, ua, ub, uc, facing);
            TriUV(sub, a, c, d, ua, uc, ud, facing);
        }

        public Mesh Build(string name)
        {
            var m = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetUVs(0, uv);
            m.subMeshCount = t.Count;
            for (int i = 0; i < t.Count; i++) m.SetTriangles(t[i], i);
            m.RecalculateBounds();
            return m;
        }
    }

    // видимая поверхность острова: сетка по высотам Terrain (общие вершины, плавные нормали). Terrain остаётся коллайдером.
    // uv0 = координаты splat-карты (0..1 по Terrain), цвет вершины = затемнение в впадинах + лёгкая вариация тона.
    static Mesh BuildSurface(Layout L, Field F)
    {
        var c = L.c; var g = F.g; int N = g.N; float ws = c.worldScale;
        int st = Mathf.Max(1, c.surfaceStep);
        while ((N - 1) % st != 0) st--;
        int M = (N - 1) / st + 1;
        float cw = g.cell * st;
        float inv = 1f / L.terrainSize;
        int o = Mathf.Max(4, st);
        var vid = new int[M * M];
        for (int k = 0; k < vid.Length; k++) vid[k] = -1;
        var vs = new List<Vector3>(); var ns = new List<Vector3>(); var uvs = new List<Vector2>(); var cols = new List<Color32>();
        var tris = new List<int>();

        Func<int, int, int> V = (i, j) =>
        {
            int key = j * M + i;
            if (vid[key] >= 0) return vid[key];
            int ix = i * st, jx = j * st;
            float x = -g.half + ix * g.cell, z = -g.half + jx * g.cell;
            float h = F.H[jx * N + ix];
            int il = Mathf.Max(ix - st, 0), ir = Mathf.Min(ix + st, N - 1), jd = Mathf.Max(jx - st, 0), ju = Mathf.Min(jx + st, N - 1);
            float dx = (F.H[jx * N + ir] - F.H[jx * N + il]) / ((ir - il) * g.cell);
            float dz = (F.H[ju * N + ix] - F.H[jd * N + ix]) / ((ju - jd) * g.cell);
            Vector3 nn = new Vector3(-dx, 1f, -dz).normalized;
            int ol = Mathf.Max(ix - o, 0), orr = Mathf.Min(ix + o, N - 1), od = Mathf.Max(jx - o, 0), ou = Mathf.Min(jx + o, N - 1);
            float avg = 0.25f * (F.H[jx * N + ol] + F.H[jx * N + orr] + F.H[od * N + ix] + F.H[ou * N + ix]);
            float curv = (avg - h) / (o * g.cell);
            float ao = 1f - 0.32f * Mathf.Clamp01(curv * 2.2f) + 0.07f * Mathf.Clamp01(-curv * 2.2f);
            float mt = 0.95f + 0.1f * L.n.P(x * 0.03f, z * 0.03f, 333f);
            byte gv = (byte)Mathf.RoundToInt(Mathf.Clamp01(ao * mt) * 255f);
            vs.Add(new Vector3(x * ws, h * ws, z * ws));
            ns.Add(nn);
            uvs.Add(new Vector2((x + g.half) * inv, (z + g.half) * inv));
            cols.Add(new Color32(gv, gv, gv, 255));
            vid[key] = vs.Count - 1;
            return vid[key];
        };

        for (int j = 0; j < M - 1; j++)
            for (int i = 0; i < M - 1; i++)
            {
                float cx = -g.half + (i + 0.5f) * cw, cz = -g.half + (j + 0.5f) * cw;
                if (!L.outline.Inside(cx, cz)) continue;     // как дырки в Terrain: клетка с центром вне контура не рисуется
                int a = V(i, j), b = V(i + 1, j), cc = V(i + 1, j + 1), d = V(i, j + 1);
                tris.Add(a); tris.Add(d); tris.Add(cc);
                tris.Add(a); tris.Add(cc); tris.Add(b);
            }

        var m = new Mesh { name = "LocationTest_Surface", indexFormat = IndexFormat.UInt32 };
        m.SetVertices(vs);
        m.SetNormals(ns);
        m.SetUVs(0, uvs);
        m.SetColors(cols);
        m.SetTriangles(tris, 0);
        m.RecalculateBounds();
        return m;
    }

    // splat-карта для шейдера: 4 слоя в RGBA (линейная текстура, без сжатия)
    static Texture2D MakeCtrl(string name, float[,,] a, int A, int k0)
    {
        var t = new Texture2D(A, A, TextureFormat.RGBA32, false, true) { name = name };
        var px = new Color32[A * A];
        for (int j = 0; j < A; j++)
            for (int i = 0; i < A; i++)
            {
                byte r = (byte)Mathf.RoundToInt(Mathf.Clamp01(a[j, i, k0]) * 255f);
                byte gg = (byte)Mathf.RoundToInt(Mathf.Clamp01(a[j, i, k0 + 1]) * 255f);
                byte b = (byte)Mathf.RoundToInt(Mathf.Clamp01(a[j, i, k0 + 2]) * 255f);
                byte al = (byte)Mathf.RoundToInt(Mathf.Clamp01(a[j, i, k0 + 3]) * 255f);
                px[j * A + i] = new Color32(r, gg, b, al);
            }
        t.SetPixels32(px);
        t.wrapMode = TextureWrapMode.Clamp;
        t.filterMode = FilterMode.Bilinear;
        t.Apply(false, false);
        SaveAsset(t, AssetDir + "/Terrain/" + name + ".asset");
        return t;
    }

    // каменная «юбка» под краем острова. Подмеши: 0 травяной кант, 1 земля, 2 скала A, 3 скала B, 4 глубина
    static Mesh BuildUnderside(Layout L, Field f)
    {
        var c = L.c; var o = L.outline; var n = L.n; float ws = c.worldScale;
        int S = c.underSegs, K = c.underRings;
        int R = K + 4;   // число колец: 4 верхних (кант) + K вниз
        float lipIn = 0.55f + 0.35f * Mathf.Max(0, c.surfaceStep - 1);   // внутренний край канта заходит под сетку меша поверхности
        var pos = new Vector3[R, S];
        float meanTop = 0f;
        var topY = new float[S];
        var dirs = new Vector2[S];
        var rad = new float[S];
        for (int s = 0; s < S; s++)
        {
            float th = s * Mathf.PI * 2f / S;
            dirs[s] = new Vector2(Mathf.Cos(th), Mathf.Sin(th));
            rad[s] = o.R(th);
            Vector2 pin = dirs[s] * (rad[s] - 0.5f);
            topY[s] = f.g.At(f.H, pin.x, pin.y);
            meanTop += topY[s] / S;
        }
        for (int s = 0; s < S; s++)
        {
            float th = s * Mathf.PI * 2f / S;
            float cs = Mathf.Cos(th * 3f), sn = Mathf.Sin(th * 3f);
            for (int k = 0; k < R; k++)
            {
                float offU, depth, y, scale = 1f;
                if (k == 0) { offU = -lipIn; depth = -0.05f; }
                else if (k == 1) { offU = 0.35f; depth = -0.05f; }
                else if (k == 2) { offU = 0.35f; depth = 0.4f; }
                else if (k == 3) { offU = -0.1f; depth = 1.2f; }
                else
                {
                    float u = (k - 3) / (float)K;
                    depth = 1.2f + (c.underDepth - 1.2f) * Mathf.Pow(u, 1.15f);
                    float taper = c.underTaper * Mathf.Pow(u, 1.6f);
                    float step = Mathf.Floor(u * 5f) / 5f;
                    taper = Mathf.Lerp(taper, c.underTaper * Mathf.Pow(step, 1.6f), 0.3f);
                    float jit = (n.P(cs * 3f + k * 0.7f, sn * 3f + k * 0.7f, 900f) - 0.5f) * 2f * c.underRough * SS(0.05f, 0.3f, u) * (1f - 0.5f * u);
                    scale = Mathf.Max(0.03f, 1f - taper + jit);
                    offU = -0.1f;
                }
                float yTop = Mathf.Lerp(topY[s], meanTop, SS(0f, 0.3f, k < 4 ? 0f : (k - 3) / (float)K));
                y = (k < 2 ? topY[s] + 0.05f : yTop) - Mathf.Max(0f, depth) - (k < 2 ? 0f : 0f);
                if (k >= 4) y += (n.P(cs * 2f + k, sn * 2f + k, 910f) - 0.5f) * 0.8f * SS(0.05f, 0.3f, (k - 3) / (float)K);
                float rr = (rad[s] * scale) + offU;
                Vector2 p2 = dirs[s] * Mathf.Max(0.2f, rr);
                pos[k, s] = new Vector3(p2.x * ws, y * ws, p2.y * ws);
            }
        }

        var mb = new MB(5);
        Vector3 center = new Vector3(0f, 0f, 0f);
        for (int k = 0; k < R - 1; k++)
            for (int s = 0; s < S; s++)
            {
                int s2 = (s + 1) % S;
                Vector3 a = pos[k, s], b = pos[k, s2], cc = pos[k + 1, s2], d = pos[k + 1, s];
                Vector3 mid = (a + b + cc + d) * 0.25f;
                Vector3 facing = new Vector3(mid.x, 0f, mid.z);
                if (k == 0) facing = Vector3.up;   // верх канта смотрит вверх
                if (facing.sqrMagnitude < 1e-6f) facing = Vector3.down;
                int sub;
                if (k <= 1) sub = 0;
                else if (k == 2) sub = 1;
                else if (k >= R - 1 - Mathf.Max(2, K / 5)) sub = 4;
                else
                {
                    float hsh = Mathf.Abs(Mathf.Sin(s * 12.9898f + k * 78.233f) * 43758.5453f);
                    sub = (hsh - Mathf.Floor(hsh)) > 0.5f ? 2 : 3;
                }
                mb.Quad(sub, a, b, cc, d, facing);
            }
        // дно: веер к нижней точке
        Vector3 apex = new Vector3(0f, (meanTop - c.underDepth * 1.08f) * ws, 0f);
        for (int s = 0; s < S; s++)
        {
            int s2 = (s + 1) % S;
            mb.Tri(4, pos[R - 1, s], pos[R - 1, s2], apex, Vector3.down);
        }
        return mb.Build("LocationTest_Underside");
    }

    // лента воды вдоль реки между tA и tB. uv.x = глубина воды (u) по рельефу, uv.y = длина дуги течения (u).
    // fall = true: каскад / водопад: uv.x от 0.2 по краям до 1 в середине (пена идёт полосами, не по берегу)
    static Mesh BuildRiverRibbon(Layout L, Field F, float tA, float tB, string name, float widthK, bool fall)
    {
        var r = L.river; var c = L.c; float ws = c.worldScale;
        const int COLS = 11;
        int iA = r.Index(tA), iB = Mathf.Min(r.Index(tB), r.p.Count - 1);
        float[] foam = r.FoamSpots();
        float fa = Mathf.Clamp(c.riverFoamAmount, 0f, 2f);   // множитель пены на перекатах
        var vs = new List<Vector3>(); var uvs = new List<Vector2>(); var uv2 = new List<Vector2>(); var tris = new List<int>();
        var cols = new List<Color32>();                       // вершинная маска пены (тир Low шейдера CandyWater)
        var obstAll = fall ? null : AllRiverObst(L);
        float acc = 0f; Vector3 prevC = Vector3.zero;
        for (int i = iA; i <= iB; i++)
        {
            Vector3 left, right;
            RibbonPair(L, i, widthK, out left, out right);
            Vector3 cen = (left + right) * 0.5f;
            if (i > iA) acc += (cen - prevC).magnitude / ws;   // длина пути по воде, включая падение
            prevC = cen;
            float sI = r.s[i];
            Vector2 dI = r.Dir(i);
            Vector2 nI = new Vector2(-dI.y, dI.x);          // поперёк течения: для лент вдоль потока
            float hwI = r.HW(sI) * widthK;
            float near = 9f;
            for (int q = 0; q < foam.Length; q++) near = Mathf.Min(near, Mathf.Abs(sI - foam[q]));
            float foamK = fa < 0.05f ? 0f : 1f - SS(0.8f * fa, 2.2f * fa, near);
            for (int k = 0; k < COLS; k++)
            {
                float f = k / (float)(COLS - 1);
                Vector3 p = Vector3.Lerp(left, right, f);
                float depth = p.y / ws - F.g.At(F.H, p.x / ws, p.z / ws);
                float dF = (new Vector2(p.x / ws, p.z / ws) - L.foamC).magnitude;          // пенный круг у подножия водопада
                float ringK = 1f - SS(c.sourceFoamR * 0.75f, c.sourceFoamR * 1.25f, dF);
                float nrF = Mathf.Min(near, dF * 0.6f);
                float fk = Mathf.Max(foamK, ringK);
                float u = fall ? 0.2f + 0.8f * (1f - Mathf.Abs(2f * f - 1f)) : Mathf.Lerp(depth, Mathf.Min(depth, 0.04f + nrF * 0.1f), fk);
                vs.Add(p);
                uvs.Add(new Vector2(u, acc));
                cols.Add(fall ? new Color32(0, 0, 0, 255) : RiverFoamVertex(obstAll, new Vector2(p.x / ws, p.z / ws)));
                float lat = Vector2.Dot(new Vector2(p.x / ws - r.p[i].x, p.z / ws - r.p[i].y), nI);
                uv2.Add(new Vector2(lat, hwI));   // uv1: положение поперёк русла и полуширина воды (для лент течения)
            }
        }
        int rows = iB - iA + 1;
        for (int j = 0; j < rows - 1; j++)
            for (int k = 0; k < COLS - 1; k++)
            {
                int a = j * COLS + k, b = a + 1, cc = a + COLS + 1, d = a + COLS;
                tris.Add(a); tris.Add(b); tris.Add(cc);
                tris.Add(a); tris.Add(cc); tris.Add(d);
            }
        var m = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
        m.SetVertices(vs);
        m.SetUVs(0, uvs);
        m.SetUVs(1, uv2);
        m.SetColors(cols);
        m.SetTriangles(tris, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    // объём воды реки для игры: ось русла, уровень воды и дальность берега; вниз по течению до кромки водопада
    static void AddWaterVolume(GameObject host, Layout L)
    {
        var r = L.river; if (r == null) return;
        float ws = L.c.worldScale;
        var pts = new List<Vector3>(); var rch = new List<float>();
        for (int i = 0; i < r.p.Count; i++)
        {
            float t = r.s[i];
            if (t > r.lipS - 4f) break;
            pts.Add(new Vector3(r.p[i].x * ws, r.WaterY(t) * ws, r.p[i].y * ws));
            rch.Add((r.HW(t) + Mathf.Max(r.Wall(t, true), r.Wall(t, false))) * ws);
        }
        if (pts.Count < 2) return;
        var wv = host.GetComponent<WaterVolume>();
        if (wv == null) wv = host.AddComponent<WaterVolume>();
        wv.SetData(pts.ToArray(), rch.ToArray());
        EditorUtility.SetDirty(wv);
    }

    static void RibbonPair(Layout L, int i, float widthK, out Vector3 left, out Vector3 right)
    {
        var r = L.river; var c = L.c; float ws = c.worldScale;
        float t = r.s[i];
        Vector2 d = r.Dir(i);
        Vector2 nr = new Vector2(-d.y, d.x);   // положительная сторона (восток при течении на юг)
        float hw = r.HW(t) * widthK;
        float hn = hw + 0.6f * r.Wall(t, true) * widthK;
        float hf = hw + 0.6f * r.Wall(t, false) * widthK;
        float y = r.WaterY(t) * ws;
        Vector2 p = r.p[i];
        Vector2 pl = p - nr * hf, pr = p + nr * hn;
        left = new Vector3(pl.x * ws, y, pl.y * ws);
        right = new Vector3(pr.x * ws, y, pr.y * ws);
    }

    // водопад с кромки: лист воды, уходящий вниз. uv.y = путь вниз по листу (u)
    static Mesh BuildLipFall(Layout L)
    {
        var r = L.river; var c = L.c; float ws = c.worldScale;
        int i = r.p.Count - 2;
        float t = r.s[i];
        Vector2 d = r.lipDir;
        Vector2 nr = new Vector2(-d.y, d.x);
        float hw = r.HW(t) + 0.3f;
        float y0 = r.WaterY(t);
        const int rows = 8, COLS = 5;
        var vs = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
        for (int k = 0; k <= rows; k++)
        {
            float u = k / (float)rows;
            float outD = 0.35f + 2.2f * Mathf.Pow(u, 1.7f);
            float drop = c.fallLength * Mathf.Pow(u, 1.1f);
            float wk = hw * (1f + 0.3f * u);
            Vector2 pc = r.lip + d * outD;
            for (int q = 0; q < COLS; q++)
            {
                float f = q / (float)(COLS - 1);
                Vector2 pp = pc + nr * (wk * (2f * f - 1f));
                vs.Add(new Vector3(pp.x * ws, (y0 - drop) * ws, pp.y * ws));
                uvs.Add(new Vector2(0.2f + 0.8f * (1f - Mathf.Abs(2f * f - 1f)), drop));
            }
        }
        for (int k = 0; k < rows; k++)
            for (int q = 0; q < COLS - 1; q++)
            {
                int a = k * COLS + q, b = a + 1, cc = a + COLS + 1, dd = a + COLS;
                tris.Add(a); tris.Add(b); tris.Add(cc);
                tris.Add(a); tris.Add(cc); tris.Add(dd);
            }
        var m = new Mesh { name = "LocationTest_FallLip", indexFormat = IndexFormat.UInt32 };
        m.SetVertices(vs);
        m.SetUVs(0, uvs);
        m.SetTriangles(tris, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    // ---------------------------------------------------------------- валуны (плоские грани, как остальной low-poly)
    const float Phi = 1.6180339f;
    static readonly Vector3[] IcoV =
    {
        new Vector3(-1f, Phi, 0f), new Vector3(1f, Phi, 0f), new Vector3(-1f, -Phi, 0f), new Vector3(1f, -Phi, 0f),
        new Vector3(0f, -1f, Phi), new Vector3(0f, 1f, Phi), new Vector3(0f, -1f, -Phi), new Vector3(0f, 1f, -Phi),
        new Vector3(Phi, 0f, -1f), new Vector3(Phi, 0f, 1f), new Vector3(-Phi, 0f, -1f), new Vector3(-Phi, 0f, 1f)
    };
    static readonly int[] IcoF =
    {
        0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
        3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
    };

    static float Rf(System.Random r) { return (float)r.NextDouble(); }

    // один валун: сплюснутый икосаэдр со случайными вершинами; pos, y и size в u, меш в метрах корня
    static void AddRock(MB mb, Vector2 pos, float y, float size, System.Random rnd, float ws)
    {
        float yawDeg = Rf(rnd) * 360f;
        float sx = size * (0.8f + Rf(rnd) * 0.5f), sy = size * (0.55f + Rf(rnd) * 0.35f), sz = size * (0.8f + Rf(rnd) * 0.5f);
        var vs = new Vector3[12];
        for (int k = 0; k < 12; k++)
        {
            Vector3 v = IcoV[k].normalized * (0.78f + Rf(rnd) * 0.4f);
            v = Quaternion.Euler(0f, yawDeg, 0f) * new Vector3(v.x * sx, v.y * sy, v.z * sz);
            vs[k] = new Vector3((pos.x + v.x) * ws, (y + v.y) * ws, (pos.y + v.z) * ws);
        }
        Vector3 cen = new Vector3(pos.x * ws, y * ws, pos.y * ws);
        for (int f = 0; f < IcoF.Length; f += 3)
        {
            Vector3 a = vs[IcoF[f]], b = vs[IcoF[f + 1]], c2 = vs[IcoF[f + 2]];
            mb.Tri(0, a, b, c2, (a + b + c2) / 3f - cen);
        }
    }

    // водопад горы: одна лента воды вдоль гладкого жёлоба в передней грани (uv.x ярче в середине, uv.y = путь вниз, u)
    static Mesh BuildMountainFall(Layout L)
    {
        var c = L.c; var m = L.mtn; float ws = c.worldScale;
        const int rows = 16, COLS = 5;
        float hw = c.fallWidth * 0.5f - 0.15f;
        Vector2 inD = -m.front, perp = new Vector2(-m.front.y, m.front.x);
        float wBase = L.river.WaterY(0f);
        var vs = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
        float acc = 0f; Vector3 prevC = Vector3.zero;
        for (int k = 0; k <= rows; k++)
        {
            float u = k / (float)rows;
            float a = Mathf.Lerp(m.lipA, -0.9f, u);
            float y = Mathf.Max(ChuteY(m, a) + 0.25f, wBase + 0.03f);
            Vector2 pc = m.foot + inD * a + m.front * 0.1f;
            float wk = hw * (1f + 0.25f * u);
            Vector3 cen = new Vector3(pc.x * ws, y * ws, pc.y * ws);
            if (k > 0) acc += (cen - prevC).magnitude / ws;
            prevC = cen;
            for (int q = 0; q < COLS; q++)
            {
                float f = q / (float)(COLS - 1);
                Vector2 pp = pc + perp * (wk * (2f * f - 1f));
                vs.Add(new Vector3(pp.x * ws, y * ws, pp.y * ws));
                uvs.Add(new Vector2(0.2f + 0.8f * (1f - Mathf.Abs(2f * f - 1f)), acc));
            }
        }
        for (int k = 0; k < rows; k++)
            for (int q = 0; q < COLS - 1; q++)
            {
                int a0 = k * COLS + q, b0 = a0 + 1, c0 = a0 + COLS + 1, d0 = a0 + COLS;
                tris.Add(a0); tris.Add(b0); tris.Add(c0);
                tris.Add(a0); tris.Add(c0); tris.Add(d0);
            }
        var mesh = new Mesh { name = "LocationTest_MountainFall", indexFormat = IndexFormat.UInt32 };
        mesh.SetVertices(vs);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // ручей в нише над водопадом: плоская лента воды в слоте (родник у задней стенки)
    static Mesh BuildSlotWater(Layout L)
    {
        var c = L.c; var m = L.mtn; float ws = c.worldScale;
        const int rows = 6, COLS = 5;
        float hw = c.fallWidth * 0.5f - 0.15f;
        Vector2 inD = -m.front, perp = new Vector2(-m.front.y, m.front.x);
        float y = (m.lipY - 0.1f) * ws;
        float a0 = m.lipA - 0.2f, a1 = m.lipA + m.nicheLen - 1.2f;
        var vs = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
        for (int k = 0; k <= rows; k++)
        {
            float a = Mathf.Lerp(a1, a0, k / (float)rows);
            Vector2 pc = m.foot + inD * a;
            for (int q = 0; q < COLS; q++)
            {
                float f = q / (float)(COLS - 1);
                Vector2 pp = pc + perp * (hw * (2f * f - 1f));
                vs.Add(new Vector3(pp.x * ws, y, pp.y * ws));
                uvs.Add(new Vector2(0.28f + 0.22f * (1f - Mathf.Abs(2f * f - 1f)), a1 - a));
            }
        }
        for (int k = 0; k < rows; k++)
            for (int q = 0; q < COLS - 1; q++)
            {
                int a2 = k * COLS + q, b2 = a2 + 1, c2 = a2 + COLS + 1, d2 = a2 + COLS;
                tris.Add(a2); tris.Add(b2); tris.Add(c2);
                tris.Add(a2); tris.Add(c2); tris.Add(d2);
            }
        var mesh = new Mesh { name = "LocationTest_SourceWater", indexFormat = IndexFormat.UInt32 };
        mesh.SetVertices(vs);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static Vector3 W3(Vector2 p, float y, float ws) { return new Vector3(p.x * ws, y * ws, p.y * ws); }

    static Vector3 Jit(Vector3 u, int seed)
    {
        float h = Hash2(Mathf.RoundToInt(u.x * 1000f), Mathf.RoundToInt(u.y * 1000f) + Mathf.RoundToInt(u.z * 1000f) * 7, seed);
        return u * (1f + 0.08f * (h - 0.5f));
    }

    // круглый гладкий валун: икосаэдр, поделённый один раз (80 граней), сплюснутый; dia = диаметр в u
    static void AddBoulder(MB mb, int sub, Vector2 pos, float y, float dia, System.Random rnd, float ws)
    {
        float yawDeg = Rf(rnd) * 360f;
        Vector3 rad = new Vector3(dia * 0.5f * (0.92f + Rf(rnd) * 0.2f), dia * 0.5f * (0.62f + Rf(rnd) * 0.2f), dia * 0.5f * (0.92f + Rf(rnd) * 0.2f));
        int seed = (int)(Rf(rnd) * 9999f);
        Quaternion rot = Quaternion.Euler(0f, yawDeg, 0f);
        Vector3 cen = new Vector3(pos.x * ws, y * ws, pos.y * ws);
        Func<Vector3, Vector3> T = u => cen + rot * Vector3.Scale(Jit(u, seed), rad) * ws;
        for (int f = 0; f < IcoF.Length; f += 3)
        {
            Vector3 a = IcoV[IcoF[f]].normalized, b = IcoV[IcoF[f + 1]].normalized, c2 = IcoV[IcoF[f + 2]].normalized;
            Vector3 ab = ((a + b) * 0.5f).normalized, bc = ((b + c2) * 0.5f).normalized, ca = ((c2 + a) * 0.5f).normalized;
            BoulderTri(mb, sub, T(a), T(ab), T(ca), cen);
            BoulderTri(mb, sub, T(ab), T(b), T(bc), cen);
            BoulderTri(mb, sub, T(ca), T(bc), T(c2), cen);
            BoulderTri(mb, sub, T(ab), T(bc), T(ca), cen);
        }
    }

    static void BoulderTri(MB mb, int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 cen)
    {
        mb.Tri(sub, a, b, c, (a + b + c) / 3f - cen);
    }

    static bool InIslet(Layout L, Vector2 p, float pad)
    {
        for (int i = 0; i < L.islets.Count; i++)
            if (IsletD(L.islets[i], p, 0.5f) < L.islets[i].r + pad) return true;
        return false;
    }

    // расстояние от точки до кромки воды реки (< 0 = в воде)
    static float RiverDist(Layout L, Vector2 p)
    {
        var r = L.river;
        if (r == null) return 999f;
        float best = 999f;
        for (int i = 0; i < r.p.Count; i++)
        {
            float d = (r.p[i] - p).magnitude - r.HW(r.s[i]);
            if (d < best) best = d;
        }
        return best;
    }

    // валуны: в русле (круглые, 0.5-1.5 u, часть торчит из воды), глыбы у чаши, осыпь у подножия горы, камни на берегах, край ямы
    static Mesh BuildRocks(Layout L, Field F)
    {
        var c = L.c; float ws = c.worldScale; var r = L.river; var mt = L.mtn;
        var mb = new MB(1);
        var rnd = new System.Random(c.seed * 31 + 7);
        if (r != null)
        {
            float dens = Mathf.Clamp(c.boulderDensity, 0f, 3f);
            for (float s = 6f; s < r.lipS - 5f; s += 1.1f)
            {
                if (Rf(rnd) > 0.5f * dens) continue;
                int i = r.Index(s);
                Vector2 dv = r.Dir(i);
                Vector2 nr = new Vector2(-dv.y, dv.x);
                float hw = r.HW(s);
                float dia = 0.5f + 1.0f * Mathf.Pow(Rf(rnd), 1.3f);
                Vector2 pos = r.p[i] + nr * ((Rf(rnd) * 2f - 1f) * hw * 0.88f);
                if (!L.outline.Inside(pos.x, pos.y) || InIslet(L, pos, dia * 0.5f + 0.6f)) continue;
                float wy = r.WaterY(s);
                float bedY = F.g.At(F.H, pos.x, pos.y);
                float halfH = dia * 0.5f * 0.72f;
                bool stick = Rf(rnd) < 0.55f;
                if (stick && wy - bedY > dia * 0.55f) stick = false;   // глубоко: камень лежит на дне, а не висит над ним
                float y = stick ? Mathf.Max(bedY + halfH * 0.7f, wy + dia * 0.06f) : bedY + halfH * 0.85f;
                AddObst(L, pos, dia * 0.5f, stick ? 1f : 0.55f);   // след течения и пена вокруг валуна (карта в LocationBuilderTester.River.cs)
                AddBoulder(mb, 0, pos, y, dia, rnd, ws);
            }

            // камни на берегах (пара на каждый десяток u)
            for (float s = 10f; s < r.lipS - 8f; s += 7f + Rf(rnd) * 4f)
            {
                if (L.bridge != null && Mathf.Abs(s - r.bridgeS) < 7f) continue;
                int i = r.Index(s);
                Vector2 dv = r.Dir(i);
                Vector2 nr = new Vector2(-dv.y, dv.x);
                float side = Rf(rnd) < 0.5f ? -1f : 1f;
                Vector2 pos = r.p[i] + nr * (side * (r.HW(s) + 0.5f + Rf(rnd) * r.Wall(s, side > 0f) * 0.5f));
                if (!L.outline.Inside(pos.x, pos.y) || F.g.At(F.dT, pos.x, pos.y) < 4f) continue;
                float size = 0.45f + Rf(rnd) * 0.7f;
                AddRock(mb, pos, F.g.At(F.H, pos.x, pos.y) + size * 0.2f, size, rnd, ws);
            }
        }
        if (mt != null)
        {
            // глыбы вокруг чаши водопада
            for (int k = 0; k < 9; k++)
            {
                float a = (k + Rf(rnd) * 0.6f) / 9f * Mathf.PI * 2f;
                Vector2 pos = L.foamC + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (c.sourceBasinWidth * 0.5f + 1.2f + Rf(rnd) * 1.8f);
                if (mt.SD(pos) < 1.5f || !L.outline.Inside(pos.x, pos.y)) continue;
                float dia = 0.9f + Rf(rnd) * 0.9f;
                AddBoulder(mb, 0, pos, F.g.At(F.H, pos.x, pos.y) + dia * 0.18f, dia, rnd, ws);
            }
            // осыпь у подножия горы
            for (int k = 0; k < 18; k++)
            {
                float th = Rf(rnd) * Mathf.PI * 2f;
                Vector2 pos = mt.c + new Vector2(Mathf.Cos(th), Mathf.Sin(th)) * mt.RayAt(th, -(1.0f + Rf(rnd) * 3.5f));
                if (!L.outline.Inside(pos.x, pos.y)) continue;
                if (Vector2.Distance(pos, mt.foot) < 6f) continue;
                if (mt.hasGate && Vector2.Distance(pos, mt.gate) < 5f) continue;
                if (F.g.At(F.dT, pos.x, pos.y) < 4f || RiverDist(L, pos) < 0.5f) continue;
                float dia = 0.6f + Rf(rnd) * 1.0f;
                AddBoulder(mb, 0, pos, F.g.At(F.H, pos.x, pos.y) + dia * 0.18f, dia, rnd, ws);
            }
        }
        if (L.pond != null) PondRocks(L, F, mb, rnd, ws);   // с прудом: 8-12 валунов у воды и камни острова
        else if (c.lakeRadius > 0.5f)
        {
            for (int k = 0; k < 16; k++)
            {
                float a = (k + Rf(rnd) * 0.7f) / 16f * Mathf.PI * 2f;
                float rr = c.lakeRadius + 0.8f + Rf(rnd) * 1.8f;
                Vector2 pos = L.lakePos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr;
                if (!L.outline.Inside(pos.x, pos.y)) continue;
                float size = 0.5f + Rf(rnd) * 0.9f;
                AddRock(mb, pos, F.g.At(F.H, pos.x, pos.y) + size * 0.2f, size, rnd, ws);
            }
        }
        return mb.Build("LocationTest_Rocks");
    }

    // брызги: светлые комки пены над чашей у подножия водопада
    static Mesh BuildSpray(Layout L, Field F)
    {
        var c = L.c; float ws = c.worldScale;
        var mb = new MB(1);
        if (L.river == null || L.mtn == null) return mb.Build("LocationTest_Spray");
        var rnd = new System.Random(c.seed * 17 + 3);
        float wy = L.river.WaterY(0f);
        for (int k = 0; k < 16; k++)
        {
            float a = Rf(rnd) * Mathf.PI * 2f;
            float rr = c.sourceFoamR * (0.35f + 0.8f * Rf(rnd));
            Vector2 pos = L.foamC + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr;
            if (L.mtn.SD(pos) < 0.2f) continue;
            float dia = 0.22f + Rf(rnd) * 0.34f;
            float up = k < 6 ? 0.5f + Rf(rnd) * 0.9f : 0.05f + Rf(rnd) * 0.15f;
            AddBoulder(mb, 0, pos, wy + up, dia, rnd, ws);
        }
        return mb.Build("LocationTest_Spray");
    }

    // ---------------------------------------------------------------- ёлки, кусты, камыш, деревца (низкополигональные)
    // подмеши: 0 стволы, 1 хвоя, 2 кроны и кусты, 3 камыш
    static void AddCone(MB mb, int sub, Vector3 baseC, float rad, float h, int sides, float yaw)
    {
        Vector3 tip = baseC + new Vector3(0f, h, 0f);
        for (int k = 0; k < sides; k++)
        {
            float a0 = yaw + k * Mathf.PI * 2f / sides, a1 = yaw + (k + 1) * Mathf.PI * 2f / sides;
            Vector3 p0 = baseC + new Vector3(Mathf.Cos(a0) * rad, 0f, Mathf.Sin(a0) * rad);
            Vector3 p1 = baseC + new Vector3(Mathf.Cos(a1) * rad, 0f, Mathf.Sin(a1) * rad);
            Vector3 mid = (p0 + p1) * 0.5f;
            mb.Tri(sub, p0, p1, tip, new Vector3(mid.x - baseC.x, 0.25f, mid.z - baseC.z));
            mb.Tri(sub, p1, p0, baseC, Vector3.down);
        }
    }

    static void AddSpruce(MB mb, Vector2 pos, float y, float hU, System.Random rnd, float ws)
    {
        Vector3 p = W3(pos, y, ws);
        float h = hU * ws, yaw = Rf(rnd) * 6.28f;
        AddCone(mb, 0, p, h * 0.05f, h * 0.22f, 4, yaw);
        float[] rr = { 0.30f, 0.23f, 0.15f };
        float[] y0 = { 0.12f, 0.36f, 0.58f };
        float[] hh = { 0.42f, 0.36f, 0.42f };
        for (int k = 0; k < 3; k++) AddCone(mb, 1, p + new Vector3(0f, h * y0[k], 0f), h * rr[k], h * hh[k], 7, yaw + k * 0.5f);
    }

    static void AddBush(MB mb, Vector2 pos, float y, float dia, System.Random rnd, float ws)
    {
        AddBoulder(mb, 2, pos, y + dia * 0.2f, dia, rnd, ws);
    }

    static void AddTreeSmall(MB mb, Vector2 pos, float y, float hU, System.Random rnd, float ws)
    {
        Vector3 p = W3(pos, y, ws);
        float h = hU * ws;
        AddCone(mb, 0, p, h * 0.05f, h * 0.65f, 4, Rf(rnd) * 6.28f);
        AddBoulder(mb, 2, pos, y + hU * 0.72f, hU * 0.62f, rnd, ws);
    }

    static void AddReeds(MB mb, Vector2 pos, float y, System.Random rnd, float ws)
    {
        int cnt = 6 + (int)(Rf(rnd) * 4f);
        for (int k = 0; k < cnt; k++)
        {
            float a = Rf(rnd) * 6.28f, rr = Rf(rnd) * 0.55f;
            Vector2 bp = pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr;
            float hgt = 0.9f + Rf(rnd) * 0.9f;
            float lean = a + 3.14f;
            Vector3 b0 = W3(bp, y - 0.05f, ws);
            Vector3 tip = W3(bp + new Vector2(Mathf.Cos(lean), Mathf.Sin(lean)) * 0.22f, y + hgt, ws);
            Vector3 side = Vector3.Cross(Vector3.up, tip - b0).normalized * 0.07f * ws;
            if (side.sqrMagnitude < 1e-8f) continue;
            Vector3 nrm = Vector3.Cross(tip - (b0 + side), tip - (b0 - side)).normalized;
            mb.Tri(3, b0 - side, b0 + side, tip, nrm);
            mb.Tri(3, b0 - side, b0 + side, tip, -nrm);
        }
    }

    // точка на оси ниши (слот и жёлоб) с запасом pad: здесь ёлки не ставим
    static bool OnSlot(Cfg c, Mtn m, Vector2 p, float pad)
    {
        Vector2 rel = p - m.foot;
        float a = -(rel.x * m.front.x + rel.y * m.front.y);
        float l = Mathf.Abs(-rel.x * m.front.y + rel.y * m.front.x);
        return a > -1f && a < m.lipA + m.nicheLen + pad && l < c.fallWidth * 0.5f + pad;
    }

    static Mesh BuildProps(Layout L, Field F)
    {
        var c = L.c; float ws = c.worldScale; var r = L.river; var mt = L.mtn;
        var mb = new MB(4);
        var rnd = new System.Random(c.seed * 53 + 11);
        Func<Vector2, float> Hy = pt => F.g.At(F.H, pt.x, pt.y);
        Func<Vector2, bool> trailFree = pt => F.g.At(F.dT, pt.x, pt.y) > 4.5f;

        if (mt != null)
        {
            // ёлки и кусты на плато
            var placed = new List<Vector2>();
            float inner = c.cliffSkirt + 2.1f;
            int want = Mathf.Clamp(c.spruces, 0, 8), got = 0;
            for (int tries = 0; tries < 500 && got < want; tries++)
            {
                float a = Rf(rnd) * Mathf.PI * 2f, rr = Rf(rnd) * 9f;
                Vector2 pos = mt.c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr;
                if (mt.SD(pos) > -inner || !trailFree(pos) || OnSlot(c, mt, pos, 3.2f)) continue;
                bool far = true;
                foreach (var q in placed) if ((q - pos).magnitude < 4.5f) { far = false; break; }
                if (!far) continue;
                placed.Add(pos); got++;
                AddSpruce(mb, pos, Hy(pos) - 0.1f, 3.2f + Rf(rnd) * 1.6f, rnd, ws);
            }
            int bushes = 0;
            for (int tries = 0; tries < 500 && bushes < 8; tries++)
            {
                float a = Rf(rnd) * Mathf.PI * 2f, rr = Rf(rnd) * 10f;
                Vector2 pos = mt.c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr;
                if (mt.SD(pos) > -(c.cliffSkirt + 1.2f) || !trailFree(pos) || OnSlot(c, mt, pos, 1.8f)) continue;
                bool far = true;
                foreach (var q in placed) if ((q - pos).magnitude < 2.2f) { far = false; break; }
                if (!far) continue;
                placed.Add(pos); bushes++;
                AddBush(mb, pos, Hy(pos), 0.9f + Rf(rnd) * 0.7f, rnd, ws);
            }
        }
        if (r != null)
        {
            // кусты по берегам
            for (float s = 4f; s < r.lipS - 4f; s += 5f + Rf(rnd) * 4f)
            {
                if (L.bridge != null && Mathf.Abs(s - r.bridgeS) < 8f) continue;
                int i = r.Index(s);
                Vector2 dv = r.Dir(i);
                Vector2 nr = new Vector2(-dv.y, dv.x);
                float side = Rf(rnd) < 0.5f ? -1f : 1f;
                Vector2 pos = r.p[i] + nr * (side * (r.HW(s) + r.Wall(s, side > 0f) * (0.7f + Rf(rnd) * 0.5f)));
                if (!L.outline.Inside(pos.x, pos.y) || !trailFree(pos) || RiverDist(L, pos) < 0.6f) continue;
                if (mt != null && mt.SD(pos) < 1f) continue;
                if (L.lookoutPad != null && LookDist(L, pos) < LookRad(L) + 2f) continue;
                AddBush(mb, pos, Hy(pos), 0.9f + Rf(rnd) * 0.9f, rnd, ws);
            }
            // камыш на мелководье у кромки
            for (float s = 8f; s < r.lipS - 6f; s += 3f + Rf(rnd) * 3f)
            {
                if (L.bridge != null && Mathf.Abs(s - r.bridgeS) < 6f) continue;
                int i = r.Index(s);
                Vector2 dv = r.Dir(i);
                Vector2 nr = new Vector2(-dv.y, dv.x);
                float side = Rf(rnd) < 0.5f ? -1f : 1f;
                Vector2 pos = r.p[i] + nr * (side * (r.HW(s) - 0.1f + Rf(rnd) * 0.5f));
                float yy = Hy(pos), wy = r.WaterY(s);
                if (!L.outline.Inside(pos.x, pos.y) || yy < wy - 0.35f || yy > wy + 0.6f || InIslet(L, pos, 0.5f)) continue;
                AddReeds(mb, pos, yy, rnd, ws);
            }
        }
        // деревце, куст и камыш на каждом островке
        foreach (var il in L.islets)
        {
            Vector2 perp = new Vector2(-il.dir.y, il.dir.x);
            AddTreeSmall(mb, il.pos, Hy(il.pos), 2.4f + Rf(rnd) * 0.6f, rnd, ws);
            Vector2 bp = il.pos + il.dir * 1.0f;
            AddBush(mb, bp, Hy(bp) - 0.05f, 0.8f, rnd, ws);
            Vector2 rp = il.pos - il.dir * 1.3f + perp * 0.4f;
            AddReeds(mb, rp, Hy(rp) - 0.1f, rnd, ws);
        }
        return mb.Build("LocationTest_Props");
    }

    // невидимая стенка-коллайдер: центр в локальных метрах корня, dir = направление вдоль стенки (XZ), size = (поперёк, высота, вдоль), м
    static void AddWall(Transform parent, string name, Vector3 center, Vector2 dir, Vector3 size)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = center;
        g.transform.localRotation = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y), Vector3.up);
        g.AddComponent<BoxCollider>().size = size;
    }

    // невидимые стенки: над водой реки (на месте моста проход оставлен, как в старой локации) и по хребту у истока.
    // Игрок двигается через Rigidbody без лимита уклона, поэтому одной крутизной склонов его не остановить.
    static void BuildBarriers(Layout L, Field F, Transform root)
    {
        var c = L.c; float ws = c.worldScale;
        var go = new GameObject("Barriers");
        go.transform.SetParent(root, false);
        var river = L.river;
        if (river != null && c.riverWall)
        {
            var rg = new GameObject("RiverWall");
            rg.transform.SetParent(go.transform, false);
            const float hM = 3.4f;
            for (int i = 0; i + 2 < river.p.Count; i += 2)
            {
                Vector2 a = river.p[i], b = river.p[i + 2];
                if (!L.outline.Inside(a.x, a.y) && !L.outline.Inside(b.x, b.y)) continue;
                float sm = river.s[i + 1];
                if (L.bridge != null && Mathf.Abs(sm - river.bridgeS) < 2.5f) continue;   // проход под мост
                Vector2 dir = b - a;
                float len = dir.magnitude;
                if (len < 0.01f) continue;
                dir /= len;
                Vector2 mid = (a + b) * 0.5f;
                float wy = river.WaterY(sm);
                float width = (river.HW(sm) + 0.7f) * 2f;
                AddWall(rg.transform, "Seg", new Vector3(mid.x * ws, wy * ws + hM * 0.5f - 0.4f, mid.y * ws), dir, new Vector3(width * ws, hM, (len + 0.4f) * ws));
            }
        }
        if (L.ridge != null)
        {
            var gg = new GameObject("RidgeWall");
            gg.transform.SetParent(go.transform, false);
            float hM = (c.ridgeHeight + 3f) * ws;
            for (int i = 0; i + 2 < L.ridge.Count; i += 2)
            {
                Vector2 a = L.ridge[i], b = L.ridge[i + 2];
                if (!L.outline.Inside(a.x, a.y) && !L.outline.Inside(b.x, b.y)) continue;
                Vector2 dir = b - a;
                float len = dir.magnitude;
                if (len < 0.01f) continue;
                dir /= len;
                Vector2 mid = (a + b) * 0.5f;
                if (L.mtn != null && L.mtn.SD(mid) < 0.5f) continue;   // внутри горы стенка не нужна, она закрыла бы тропу на плато
                float baseY = F.g.At(F.H, mid.x, mid.y) * ws - 1f;
                AddWall(gg.transform, "Seg", new Vector3(mid.x * ws, baseY + hM * 0.5f, mid.y * ws), dir, new Vector3(2.2f * ws, hM, (len + 0.4f) * ws));
            }
        }
        if (L.mtn != null)
        {
            // кольцо вокруг подножия горы (1 u от скалы): вход оставлен только у начала тропы на плато
            var mg = new GameObject("MountainWall");
            mg.transform.SetParent(go.transform, false);
            var m = L.mtn;
            Vector2[] ov = m.Verts(1.0f);
            float hMm = (c.mountainHeight + 5f) * ws;
            for (int e = 0; e < m.n; e++)
            {
                Vector2 a = ov[e], b = ov[(e + 1) % m.n];
                int cnt = Mathf.Max(1, Mathf.CeilToInt((b - a).magnitude / 2f));
                for (int k = 0; k < cnt; k++)
                {
                    Vector2 pa = Vector2.Lerp(a, b, k / (float)cnt), pb = Vector2.Lerp(a, b, (k + 1) / (float)cnt);
                    Vector2 mid = (pa + pb) * 0.5f;
                    if (m.hasGate && Vector2.Distance(mid, m.gate) < 3.2f) continue;
                    if (!L.outline.Inside(mid.x, mid.y)) continue;
                    Vector2 dir = pb - pa;
                    float sl = dir.magnitude;
                    if (sl < 0.01f) continue;
                    dir /= sl;
                    float baseY = (F.g.At(F.H, mid.x, mid.y) - 2f) * ws;
                    AddWall(mg.transform, "Seg", new Vector3(mid.x * ws, baseY + hMm * 0.5f, mid.y * ws), dir, new Vector3(1.4f * ws, hMm, (sl + 0.3f) * ws));
                }
            }
        }
    }

    // =====================================================================================================
    //  АССЕТЫ
    // =====================================================================================================

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    static void SaveAsset(UnityEngine.Object obj, string path)
    {
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(obj, path);
    }

    // =====================================================================================================
    //  КАРТОННЫЙ СТИЛЬ: бесшовные текстуры слоёв (процедурно), шейдеры, материалы
    // =====================================================================================================

    const int TexN = 256;
    static Texture2D _grainTex, _paperTex;

    static int Mod(int a, int m) { int r = a % m; return r < 0 ? r + m : r; }

    static float Hash2(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777215f;
        }
    }

    // периодический value-noise: тайлится по u, v в [0,1) с периодом per
    static float VNoise(float u, float v, int per, int seed)
    {
        float x = u * per, y = v * per;
        int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
        float fx = x - xi, fy = y - yi;
        fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
        int x0 = Mod(xi, per), x1 = Mod(xi + 1, per), y0 = Mod(yi, per), y1 = Mod(yi + 1, per);
        float a = Hash2(x0, y0, seed), b = Hash2(x1, y0, seed), c = Hash2(x0, y1, seed), d = Hash2(x1, y1, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    static float Fbm(float u, float v, int per, int oct, int seed)
    {
        float s = 0f, amp = 0.5f, norm = 0f;
        for (int o = 0; o < oct; o++)
        {
            s += amp * VNoise(u, v, per << o, seed + o * 17);
            norm += amp; amp *= 0.5f;
        }
        return s / norm;
    }

    // периодический Вороной: f1 и f2 - расстояния до ближайшей и второй точки (в долях ячейки)
    static void Worley(float u, float v, int per, int seed, out float f1, out float f2)
    {
        float x = u * per, y = v * per;
        int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
        f1 = 9f; f2 = 9f;
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int cx = xi + dx, cy = yi + dy;
                int hx = Mod(cx, per), hy = Mod(cy, per);
                float px = cx + Hash2(hx, hy, seed), py = cy + Hash2(hx, hy, seed + 101);
                float d = Mathf.Sqrt((px - x) * (px - x) + (py - y) * (py - y));
                if (d < f1) { f2 = f1; f1 = d; }
                else if (d < f2) f2 = d;
            }
    }

    static Color Shift(Color c, float k)
    {
        return new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), 1f);
    }

    static void PxBlend(Color[] px, int x, int y, Color c, float a)
    {
        int i = Mod(y, TexN) * TexN + Mod(x, TexN);
        px[i] = Color.Lerp(px[i], c, Mathf.Clamp01(a));
    }

    // тонкий штрих (травинка, волокно картона); края обёрнуты, текстура остаётся бесшовной
    static void Stroke(Color[] px, float x0, float y0, float ang, float len, float wid, Color c, float a)
    {
        float dx = Mathf.Cos(ang), dy = Mathf.Sin(ang);
        int steps = Mathf.Max(1, Mathf.CeilToInt(len * 2f));
        for (int s = 0; s <= steps; s++)
        {
            float t = s / (float)steps;
            float ww = wid * (0.5f + 0.5f * Mathf.Sin(t * Mathf.PI));
            float x = x0 + dx * len * t, y = y0 + dy * len * t;
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            for (int oy = 0; oy <= 1; oy++)
                for (int ox = 0; ox <= 1; ox++)
                {
                    float cov = (1f - Mathf.Abs(x - (ix + ox))) * (1f - Mathf.Abs(y - (iy + oy)));
                    if (cov > 0f) PxBlend(px, ix + ox, iy + oy, c, a * cov * ww);
                }
        }
    }

    static void Blob(Color[] px, float cx, float cy, float rx, float ry, float ang, Color c, float a)
    {
        float ca = Mathf.Cos(ang), sa = Mathf.Sin(ang);
        int R = Mathf.CeilToInt(Mathf.Max(rx, ry)) + 1;
        int bx = Mathf.RoundToInt(cx), by = Mathf.RoundToInt(cy);
        for (int oy = -R; oy <= R; oy++)
            for (int ox = -R; ox <= R; ox++)
            {
                float lx = ox * ca + oy * sa, ly = -ox * sa + oy * ca;
                float d = Mathf.Sqrt((lx / rx) * (lx / rx) + (ly / ry) * (ly / ry));
                if (d >= 1f) continue;
                PxBlend(px, bx + ox, by + oy, c, a * (1f - SS(0.55f, 1f, d)));
            }
    }

    static void Fibers(Color[] px, int seed, int count, float lenMin, float lenMax, float a, float lightK)
    {
        var rnd = new System.Random(seed);
        for (int i = 0; i < count; i++)
        {
            float x = (float)rnd.NextDouble() * TexN, y = (float)rnd.NextDouble() * TexN;
            float ang = (float)rnd.NextDouble() * Mathf.PI * 2f;
            float len = lenMin + (float)rnd.NextDouble() * (lenMax - lenMin);
            bool light = rnd.NextDouble() > 0.5;
            Color b = px[Mod((int)y, TexN) * TexN + Mod((int)x, TexN)];
            Stroke(px, x, y, ang, len, 0.7f, Shift(b, light ? lightK : 1f / lightK), a);
        }
    }

    // чистая основа low-poly: ровный цвет слоя, едва заметное крупное «дыхание» тона и лёгкая неидеальность
    static Color[] PaperBase(Color col, int seed, float patch, float grain, float flute)
    {
        var px = new Color[TexN * TexN];
        for (int y = 0; y < TexN; y++)
            for (int x = 0; x < TexN; x++)
            {
                float u = (x + 0.5f) / TexN, v = (y + 0.5f) / TexN;
                float n = Fbm(u, v, 3, 3, seed) - 0.5f;
                float k = 1f + patch * 1.6f * n;
                float gr = 1f + grain * 0.5f * (Hash2(x, y, seed + 5) - 0.5f);
                px[y * TexN + x] = Shift(col, k * gr);
            }
        return px;
    }

    // Вороной с номером ячейки: плоские грани скал
    static void WorleyId(float u, float v, int per, int seed, out float f1, out float f2, out float id)
    {
        float x = u * per, y = v * per;
        int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
        f1 = 9f; f2 = 9f; id = 0f;
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int cx = xi + dx, cy = yi + dy;
                int hx = Mod(cx, per), hy = Mod(cy, per);
                float px = cx + Hash2(hx, hy, seed), py = cy + Hash2(hx, hy, seed + 101);
                float d = Mathf.Sqrt((px - x) * (px - x) + (py - y) * (py - y));
                if (d < f1) { f2 = f1; f1 = d; id = Hash2(hx, hy, seed + 303); }
                else if (d < f2) f2 = d;
            }
    }

    // трава: ровный зелёный и редкие крупные пучки; на светлой траве несколько ромашек
    static Color[] GenGrass(Color col, int seed, bool flowers)
    {
        var px = PaperBase(col, seed, 0.05f, 0.008f, 0f);
        var rnd = new System.Random(seed * 7 + 1);
        Color dark = new Color(col.r * 0.78f, col.g * 0.88f, col.b * 0.86f, 1f);
        Color lite = Color.Lerp(Shift(col, 1.14f), new Color(0.95f, 1f, 0.60f), 0.2f);
        for (int i = 0; i < 14; i++)
        {
            float x = (float)rnd.NextDouble() * TexN, y = (float)rnd.NextDouble() * TexN;
            for (int b = -1; b <= 1; b++)
            {
                float ang = Mathf.PI * 0.5f + b * 0.5f;
                Stroke(px, x + 1.4f, y - 1.4f, ang, 9f, 2.2f, dark, 0.45f);
                Stroke(px, x, y, ang, 9f, 2.0f, lite, 0.80f);
            }
        }
        if (flowers)
        {
            Color[] pet = { new Color(1f, 0.98f, 0.92f), new Color(1f, 0.88f, 0.40f), new Color(1f, 0.70f, 0.78f) };
            for (int i = 0; i < 5; i++)
            {
                float x = (float)rnd.NextDouble() * TexN, y = (float)rnd.NextDouble() * TexN;
                Color p = pet[rnd.Next(pet.Length)];
                for (int k = 0; k < 5; k++)
                {
                    float a = k * Mathf.PI * 2f / 5f;
                    Blob(px, x + Mathf.Cos(a) * 2.4f, y + Mathf.Sin(a) * 2.4f, 2.2f, 2.2f, 0f, p, 1f);
                }
                Blob(px, x, y, 1.9f, 1.9f, 0f, new Color(1f, 0.80f, 0.20f), 1f);
            }
        }
        return px;
    }

    // тропа: ровный тёплый цвет и несколько камешков
    static Color[] GenTrail(Color col, int seed, float pebbles, float flute)
    {
        var px = PaperBase(col, seed, 0.03f, 0.008f, 0f);
        var rnd = new System.Random(seed * 5 + 2);
        int cnt = Mathf.RoundToInt(12f * pebbles);
        for (int i = 0; i < cnt; i++)
        {
            float x = (float)rnd.NextDouble() * TexN, y = (float)rnd.NextDouble() * TexN;
            float r = 2.6f + (float)rnd.NextDouble() * 2.2f, ang = (float)rnd.NextDouble() * 0.6f;
            Blob(px, x + 1.2f, y - 1.4f, r * 1.1f, r * 0.8f, ang, Shift(col, 0.80f), 0.8f);
            Blob(px, x, y, r * 1.1f, r * 0.8f, ang, Shift(col, 1.08f), 1f);
            Blob(px, x - 0.6f, y + 0.6f, r * 0.55f, r * 0.4f, ang, Shift(col, 1.18f), 0.7f);
        }
        return px;
    }

    static Color[] GenSoil(Color col, int seed)
    {
        var px = PaperBase(col, seed, 0.05f, 0.008f, 0f);
        var rnd = new System.Random(seed * 3 + 1);
        for (int i = 0; i < 10; i++)
        {
            float x = (float)rnd.NextDouble() * TexN, y = (float)rnd.NextDouble() * TexN;
            float r = 3f + (float)rnd.NextDouble() * 3f;
            Blob(px, x, y, r * 1.2f, r * 0.8f, (float)rnd.NextDouble() * 3f, Shift(col, rnd.NextDouble() > 0.5 ? 1.12f : 0.88f), 0.9f);
        }
        return px;
    }

    // скала: ровный мягкий цвет (грани рисует сама низкополигональная сетка) и редкие светлые крапинки
    static Color[] GenRock(Color col, int seed)
    {
        var px = PaperBase(col, seed, 0.05f, 0.008f, 0f);
        var rnd = new System.Random(seed * 5 + 3);
        for (int i = 0; i < 10; i++)
            Blob(px, (float)rnd.NextDouble() * TexN, (float)rnd.NextDouble() * TexN, 2f + (float)rnd.NextDouble() * 2f, 1.6f, (float)rnd.NextDouble() * 3f, Shift(col, 1.12f), 0.45f);
        return px;
    }

    static Color[] GenForest(Color col, int seed)
    {
        var px = PaperBase(col, seed, 0.06f, 0.008f, 0f);
        var rnd = new System.Random(seed * 9 + 4);
        Color[] leaf = { new Color(1f, 0.66f, 0.24f), new Color(0.88f, 0.46f, 0.18f), new Color(0.52f, 0.80f, 0.28f) };
        for (int i = 0; i < 14; i++)
        {
            float x = (float)rnd.NextDouble() * TexN, y = (float)rnd.NextDouble() * TexN;
            Blob(px, x, y, 3.8f + (float)rnd.NextDouble() * 1.6f, 2.2f, (float)rnd.NextDouble() * Mathf.PI, leaf[rnd.Next(leaf.Length)], 0.95f);
        }
        return px;
    }

    static Color[] GenSand(Color col, int seed)
    {
        var px = PaperBase(col, seed, 0.03f, 0.008f, 0f);
        for (int y = 0; y < TexN; y++)
            for (int x = 0; x < TexN; x++)
            {
                float v = (y + 0.5f) / TexN, u = (x + 0.5f) / TexN;
                float rip = Mathf.Sin((v * 5f + 1.5f * Fbm(u, v, 4, 2, seed + 7)) * Mathf.PI * 2f);
                px[y * TexN + x] = Shift(px[y * TexN + x], 1f - 0.05f * SS(0.85f, 0.98f, rip));
            }
        var rnd = new System.Random(seed * 3 + 5);
        for (int i = 0; i < 14; i++)
            Blob(px, (float)rnd.NextDouble() * TexN, (float)rnd.NextDouble() * TexN, 1.6f, 1.4f, 0f, Shift(col, rnd.NextDouble() > 0.5 ? 1.08f : 0.90f), 0.8f);
        return px;
    }

    // зерно для шейдера: почти ровное серое, только слабая крупная вариация
    static Color[] GenGrain(int seed)
    {
        var px = new Color[TexN * TexN];
        for (int y = 0; y < TexN; y++)
            for (int x = 0; x < TexN; x++)
            {
                float u = (x + 0.5f) / TexN, v = (y + 0.5f) / TexN;
                float g = 0.5f + 0.25f * (Fbm(u, v, 8, 2, seed) - 0.5f) + 0.08f * (Hash2(x, y, seed + 2) - 0.5f);
                px[y * TexN + x] = new Color(g, g, g, 1f);
            }
        return px;
    }

    static Color[] GenPaper(int seed)
    {
        return PaperBase(new Color(0.98f, 0.97f, 0.94f), seed, 0.02f, 0.01f, 0f);
    }

    static Texture2D SaveTex(string name, Color[] px, bool force)
    {
        string path = AssetDir + "/Layers/" + name + "_v4.png";
        if (!force)
        {
            var ex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (ex != null) return ex;
        }
        var t = new Texture2D(TexN, TexN, TextureFormat.RGBA32, true);
        t.SetPixels(px);
        t.Apply();
        string full = Path.Combine(Directory.GetParent(Application.dataPath).FullName, path);
        File.WriteAllBytes(full, t.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti != null)
        {
            ti.wrapMode = TextureWrapMode.Repeat;
            ti.filterMode = FilterMode.Trilinear;
            ti.anisoLevel = 4;
            ti.mipmapEnabled = true;
            ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // 8 слоёв Terrain/шейдера + зерно и «бумага» для маркеров. Файлы переиспользуются, force = пересоздать
    static Texture2D[] MakeAllTextures(bool force)
    {
        var t = new Texture2D[LayerCount];
        t[0] = SaveTex("C0_Grass", GenGrass(new Color(0.44f, 0.76f, 0.36f), 11, false), force);
        t[1] = SaveTex("C1_GrassLight", GenGrass(new Color(0.62f, 0.83f, 0.40f), 23, true), force);
        t[2] = SaveTex("C2_Trail", GenTrail(new Color(0.94f, 0.79f, 0.56f), 35, 1f, 0f), force);
        t[3] = SaveTex("C3_Soil", GenSoil(new Color(0.60f, 0.42f, 0.30f), 47), force);
        t[4] = SaveTex("C4_Rock", GenRock(new Color(0.72f, 0.68f, 0.78f), 59), force);
        t[5] = SaveTex("C5_ForestFloor", GenForest(new Color(0.30f, 0.57f, 0.33f), 71), force);
        t[6] = SaveTex("C6_Sand", GenSand(new Color(0.97f, 0.88f, 0.64f), 83), force);
        t[7] = SaveTex("C7_TrailDark", GenTrail(new Color(0.78f, 0.60f, 0.42f), 95, 1.2f, 0f), force);
        _grainTex = SaveTex("C_Grain", GenGrain(107), force);
        _paperTex = SaveTex("C_Paper", GenPaper(119), force);
        return t;
    }

    // ---------------------------------------------------------------- шейдеры (пишутся в Assets/_Game/LocationTest/Shaders)
    // Обратные кавычки в тексте шейдеров заменяются на обычные при записи файла.

    const string CardShaderSrc = @"Shader `BuilderSim/LocationCardboard`
{
    Properties
    {
        _Tint (`Tint`, Color) = (1,1,1,1)
        _Shade (`Shadow tint`, Color) = (0.56,0.48,0.70,1)
        _MainTex (`Texture (solid mode)`, 2D) = `white` {}
        _Tex0 (`Layer 0`, 2D) = `white` {}
        _Tex1 (`Layer 1`, 2D) = `white` {}
        _Tex2 (`Layer 2`, 2D) = `white` {}
        _Tex3 (`Layer 3`, 2D) = `white` {}
        _Tex4 (`Layer 4 (rock, triplanar)`, 2D) = `white` {}
        _Tex5 (`Layer 5`, 2D) = `white` {}
        _Tex6 (`Layer 6`, 2D) = `white` {}
        _Tex7 (`Layer 7`, 2D) = `white` {}
        _Ctrl0 (`Splat 0-3`, 2D) = `white` {}
        _Ctrl1 (`Splat 4-7`, 2D) = `black` {}
        _Grain (`Paper grain`, 2D) = `gray` {}
        _TileM (`Metres per tile`, Float) = 3
        _Sharp (`Edge crispness`, Float) = 3
        _Wrap (`Light wrap`, Range(0,1)) = 0.35
        _Rim (`Rim light`, Range(0,1)) = 0.15
        _GrainAmt (`Paper grain`, Range(0,1)) = 0.6
        _ContourStep (`Cardboard sheet height, m`, Float) = 0.45
        _ContourAmt (`Sheet line strength`, Range(0,1)) = 0.14
        _ContourBase (`Sheet line origin (world Y), m`, Float) = 0
        _Bright (`Brightness`, Range(0.5,1.6)) = 1
        _Sat (`Saturation`, Range(0.5,2)) = 1.2
        _MossCol (`Moss colour`, Color) = (0.52,0.78,0.36,1)
        _MossAmt (`Moss on top faces`, Range(0,1)) = 0
    }

    SubShader
    {
        Tags { `RenderType`=`Opaque` `RenderPipeline`=`UniversalPipeline` `Queue`=`Geometry` }

        Pass
        {
            Name `ForwardLit`
            Tags { `LightMode`=`UniversalForward` }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ _SPLAT_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            #include `Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl`
            #include `Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl`

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half4 _Shade;
                float _TileM;
                float _Sharp;
                half _Wrap;
                half _Rim;
                half _GrainAmt;
                float _ContourStep;
                half _ContourAmt;
                float _ContourBase;
                half _Bright;
                half _Sat;
                half4 _MossCol;
                half _MossAmt;
            CBUFFER_END

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_Tex0); SAMPLER(sampler_Tex0);
            TEXTURE2D(_Tex1);
            TEXTURE2D(_Tex2);
            TEXTURE2D(_Tex3);
            TEXTURE2D(_Tex4);
            TEXTURE2D(_Tex5);
            TEXTURE2D(_Tex6);
            TEXTURE2D(_Tex7);
            TEXTURE2D(_Ctrl0); SAMPLER(sampler_Ctrl0);
            TEXTURE2D(_Ctrl1);
            TEXTURE2D(_Grain); SAMPLER(sampler_Grain);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 color : COLOR;
                float2 uv : TEXCOORD2;
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.color = i.color;
                o.uv = i.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half3 n = normalize(i.normalWS);
                float3 pw = i.positionWS;
                float tile = max(_TileM, 0.1);
                float2 tuv = pw.xz / tile;

                half3 bw = pow(abs(n), 4.0);
                bw = bw / (bw.x + bw.y + bw.z + 0.0001);

            #if defined(_SPLAT_ON)
                half4 c0 = SAMPLE_TEXTURE2D(_Ctrl0, sampler_Ctrl0, i.uv);
                half4 c1 = SAMPLE_TEXTURE2D(_Ctrl1, sampler_Ctrl0, i.uv);
                float sp = max(_Sharp, 1.0);
                float w0 = pow(max(c0.r, 0.0001), sp);
                float w1 = pow(max(c0.g, 0.0001), sp);
                float w2 = pow(max(c0.b, 0.0001), sp);
                float w3 = pow(max(c0.a, 0.0001), sp);
                float w4 = pow(max(c1.r, 0.0001), sp);
                float w5 = pow(max(c1.g, 0.0001), sp);
                float w6 = pow(max(c1.b, 0.0001), sp);
                float w7 = pow(max(c1.a, 0.0001), sp);
                float wsum = w0 + w1 + w2 + w3 + w4 + w5 + w6 + w7;
                half3 acc = SAMPLE_TEXTURE2D(_Tex0, sampler_Tex0, tuv).rgb * w0;
                acc += SAMPLE_TEXTURE2D(_Tex1, sampler_Tex0, tuv).rgb * w1;
                acc += SAMPLE_TEXTURE2D(_Tex2, sampler_Tex0, tuv).rgb * w2;
                acc += SAMPLE_TEXTURE2D(_Tex3, sampler_Tex0, tuv).rgb * w3;
                acc += SAMPLE_TEXTURE2D(_Tex5, sampler_Tex0, tuv).rgb * w5;
                acc += SAMPLE_TEXTURE2D(_Tex6, sampler_Tex0, tuv).rgb * w6;
                acc += SAMPLE_TEXTURE2D(_Tex7, sampler_Tex0, tuv).rgb * w7;
                half3 rockC = SAMPLE_TEXTURE2D(_Tex4, sampler_Tex0, pw.zy / tile).rgb * bw.x
                            + SAMPLE_TEXTURE2D(_Tex4, sampler_Tex0, tuv).rgb * bw.y
                            + SAMPLE_TEXTURE2D(_Tex4, sampler_Tex0, pw.xy / tile).rgb * bw.z;
                acc += rockC * w4;
                half3 albedo = acc / max(wsum, 0.0001);
            #else
                half3 albedo = (SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, pw.zy / tile).rgb * bw.x
                              + SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, tuv).rgb * bw.y
                              + SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, pw.xy / tile).rgb * bw.z) * _Tint.rgb;
            #endif

                // крупная вариация тона: чтобы плитка текстуры не читалась
                half macroN = SAMPLE_TEXTURE2D(_Grain, sampler_Grain, pw.xz * 0.021).r;
                albedo *= 0.96 + 0.08 * macroN;
                half lumA = dot(albedo, half3(0.299, 0.587, 0.114));
                albedo = saturate(lerp(half3(lumA, lumA, lumA), albedo, _Sat));
                // мох на верхних гранях камней (рваный край от шума)
                half mn = SAMPLE_TEXTURE2D(_Grain, sampler_Grain, pw.xz * 0.4).r;
                half mossK = smoothstep(0.45, 0.62, smoothstep(0.5, 0.85, n.y) + (mn - 0.5) * 0.7) * _MossAmt;
                albedo = lerp(albedo, _MossCol.rgb, mossK);

                // листы картона: тёмная линия через каждые _ContourStep метров высоты + светлая фаска под ней; на крутом сильнее
                half contour = 1.0;
                if (_ContourStep > 0.001)
                {
                    float fr = frac((pw.y - _ContourBase) / max(_ContourStep, 0.001));
                    half dark = 1.0 - smoothstep(0.0, 0.09, fr);
                    half bevel = smoothstep(0.09, 0.14, fr) * (1.0 - smoothstep(0.14, 0.26, fr));
                    half steep = saturate((1.0 - n.y) * 2.5);
                    half k = 0.15 + 0.85 * steep;
                    contour = 1.0 - _ContourAmt * dark * k + _ContourAmt * 0.55 * bevel * k;
                }

                float2 guv = pw.xz * 0.8 + pw.y * float2(0.55, 0.35);
                half g1 = SAMPLE_TEXTURE2D(_Grain, sampler_Grain, guv).r;
                half g2 = SAMPLE_TEXTURE2D(_Grain, sampler_Grain, guv * 3.7 + 0.37).r;

                float4 sc = TransformWorldToShadowCoord(pw);
                Light l = GetMainLight(sc);
                half ndl = dot(n, l.direction);
                // мультяшный свет: три чёткие полосы (цветная тень / середина / тёплый свет), без грязных градиентов
                half w = saturate((ndl + _Wrap) / (1.0 + _Wrap)) + (g1 - 0.5) * 0.05 * _GrainAmt;
                w = lerp(min(w, 0.30), w, l.shadowAttenuation);
                half b1 = smoothstep(0.28, 0.37, w);
                half b2 = smoothstep(0.60, 0.70, w);
                half3 midC = lerp(_Shade.rgb, half3(1, 1, 1), 0.66);
                half3 lit = lerp(_Shade.rgb, midC, b1);
                lit = lerp(lit, half3(1.07, 1.03, 0.92), b2);
                half3 lc = lerp(half3(1, 1, 1), saturate(l.color), 0.5);
                half hemi = lerp(0.95, 1.04, n.y * 0.5 + 0.5);
                half3 col = albedo * lit * lc * contour * _Bright * hemi;
            #if defined(_SPLAT_ON)
                col *= lerp(half3(1, 1, 1), i.color.rgb, 0.55);
            #endif
                col *= lerp(1.0, 0.97 + 0.06 * g2, _GrainAmt);

                half3 v = normalize(GetWorldSpaceViewDir(pw));
                half rim = pow(1.0 - saturate(dot(n, v)), 3.0) * _Rim;
                col += rim * lc * half3(1.0, 0.93, 0.82) * 0.3;

                half fogF = ComputeFogFactor(i.positionCS.z);
                col = MixFog(col, fogF);
                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name `ShadowCaster`
            Tags { `LightMode`=`ShadowCaster` }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vertS
            #pragma fragment fragS
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include `Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl`
            #include `Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl`

            float3 _LightDirection;
            float3 _LightPosition;

            float4 vertS(float4 positionOS : POSITION, float3 normalOS : NORMAL) : SV_POSITION
            {
                float3 positionWS = TransformObjectToWorld(positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return positionCS;
            }

            half4 fragS() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name `DepthOnly`
            Tags { `LightMode`=`DepthOnly` }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vertD
            #pragma fragment fragD

            #include `Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl`

            float4 vertD(float4 positionOS : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(positionOS.xyz);
            }

            half fragD() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}";

    // вода: мультяшные «бумажные» полосы течения, пена у берега по глубине (uv.x = глубина воды в u, uv.y = путь вдоль течения)
    const string WaterShaderSrc = @"Shader `BuilderSim/LocationToonWater`
{
    Properties
    {
        _Deep (`Deep colour`, Color) = (0.16,0.50,0.80,1)
        _Shallow (`Shallow colour`, Color) = (0.42,0.82,0.92,1)
        _Foam (`Foam colour`, Color) = (0.95,0.99,1,1)
        _Alpha (`Alpha`, Range(0,1)) = 0.82
        _Speed (`Flow speed`, Float) = 1.4
        _Freq (`Stripes per unit of flow`, Float) = 0.7
        _Lines (`Stripe strength`, Range(0,1)) = 0.5
        _FoamDepth (`Shore foam depth, u`, Float) = 0.35
        _DepthScale (`Depth for deep colour, u`, Float) = 1.2
        _Caustic (`Caustic light on the bed`, Range(0,1)) = 0
        _Ribbons (`Flow ribbons along the stream`, Range(0,1)) = 0
        _ShallowAlpha (`Alpha in the shallows (-1 = off)`, Range(-1,1)) = -1
        _WakeMap (`Wake map: R edge dist, G V-wake, B speed-up`, 2D) = `black` {}
        _WakeRect (`Wake map rect (x0 z0 1/w 1/h, object space)`, Vector) = (0,0,1,1)
        _WakeAmt (`Foam around rocks and V-wakes`, Range(0,2)) = 0
        _Sparkle (`Sun glints on the water`, Range(0,1)) = 0
        _Streak (`Waterfall streaks (uv1 = lateral pos, half width)`, Range(0,1)) = 0
        _FallLen (`Waterfall path length, u (foam at the foot)`, Float) = 0
        _FallFade (`Fade the sheet into mist at the bottom`, Range(0,1)) = 0
        _Pulse (`Soft pulsing of the alpha (mist)`, Range(0,1)) = 0
    }

    SubShader
    {
        Tags { `RenderType`=`Transparent` `Queue`=`Transparent` `RenderPipeline`=`UniversalPipeline` }

        Pass
        {
            Name `Water`
            Tags { `LightMode`=`UniversalForward` }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include `Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl`

            CBUFFER_START(UnityPerMaterial)
                half4 _Deep;
                half4 _Shallow;
                half4 _Foam;
                half _Alpha;
                float _Speed;
                float _Freq;
                half _Lines;
                float _FoamDepth;
                float _DepthScale;
                half _Caustic;
                half _Ribbons;
                half _ShallowAlpha;
                float4 _WakeRect;
                half _WakeAmt;
                half _Sparkle;
                half _Streak;
                float _FallLen;
                half _FallFade;
                half _Pulse;
            CBUFFER_END

            TEXTURE2D(_WakeMap);
            SAMPLER(sampler_WakeMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float2 uv2 : TEXCOORD2;
                float2 posOS : TEXCOORD3;
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.uv = i.uv;
                o.uv2 = i.uv2;
                o.posOS = i.positionOS.xz;
                return o;
            }

            float FHash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // мягкий value-noise 0..1 без ступенек: из него сделаны струи водопада
            float FNoise(float2 p)
            {
                float2 ip = floor(p);
                float2 fp = frac(p);
                fp = fp * fp * (3.0 - 2.0 * fp);
                float na = FHash(ip);
                float nb = FHash(ip + float2(1.0, 0.0));
                float nc = FHash(ip + float2(0.0, 1.0));
                float nd = FHash(ip + float2(1.0, 1.0));
                return lerp(lerp(na, nb, fp.x), lerp(nc, nd, fp.x), fp.y);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float depth = i.uv.x;
                float flow = i.uv.y;
                float d01 = saturate(depth / max(_DepthScale, 0.01));
                half3 col = lerp(_Shallow.rgb, _Deep.rgb, d01);
                half3 colSmooth = col;
                col = floor(col * 8.0 + 0.5) / 8.0;
                if (_Streak > 0.001) col = colSmooth;   // водопад: плавный градиент без ступенек цвета

                float wob = sin(flow * _Freq * 6.2831 - _Time.y * _Speed + sin(i.positionWS.x * 1.3 + i.positionWS.z * 0.9) * 1.6);
                float strip = smoothstep(0.64, 0.80, wob);
                col = lerp(col, lerp(col, _Foam.rgb, 0.5), strip * _Lines * (0.4 + 0.6 * d01) * (_Streak > 0.001 ? 0.0 : 1.0));

                float ew = 0.5 + 0.5 * sin(flow * 3.1 + _Time.y * 0.8 + i.positionWS.x * 2.0);
                float shore = 1.0 - smoothstep(_FoamDepth * 0.6 * (0.6 + 0.4 * ew), _FoamDepth * (0.8 + 0.5 * ew), depth);
                shore *= step(0.001, _FoamDepth);
                col = lerp(col, _Foam.rgb, shore);

                float foamExtra = 0.0;
                if (_Ribbons > 0.001 || _WakeAmt > 0.001)
                {
                    float2 wuv = (i.posOS - _WakeRect.xy) * _WakeRect.zw;
                    float4 wk = SAMPLE_TEXTURE2D_LOD(_WakeMap, sampler_WakeMap, wuv, 0);

                    if (_Ribbons > 0.001)
                    {
                        float lat = i.uv2.x;
                        float hwr = max(i.uv2.y, 0.5);
                        float wl = lat + sin(flow * 0.35 + lat * 0.8) * 0.22;
                        float lane = floor(wl / 1.05);
                        float fl = frac(wl / 1.05) - 0.5;
                        float h = frac(sin(lane * 12.9898) * 43758.5453);
                        float al = frac(flow * 0.22 - _Time.y * _Speed * 0.16 + h);
                        float ln = 0.22 + 0.30 * h + 0.25 * wk.b;
                        float tt = saturate(al / ln);
                        float wd = lerp(0.04, 0.17 + 0.07 * h, sin(3.14159 * tt));
                        float rb = (1.0 - step(ln, al)) * (1.0 - smoothstep(wd * 0.75, wd, abs(fl) * 1.05));
                        rb *= 1.0 - smoothstep(0.65, 1.0, abs(lat) / hwr);
                        rb *= smoothstep(0.06, 0.2, depth);
                        col = lerp(col, lerp(_Shallow.rgb, _Foam.rgb, 0.4), rb * _Ribbons);
                    }

                    if (_WakeAmt > 0.001)
                    {
                        float edge = wk.r * 1.2;
                        float thick = (0.28 + 0.12 * ew) * _WakeAmt;
                        float ring = 1.0 - smoothstep(thick * 0.55, thick, edge);
                        float vk = smoothstep(0.30, 0.50, wk.g) * (0.75 + 0.25 * sin(flow * 2.2 - _Time.y * _Speed * 1.6)) * _WakeAmt;
                        foamExtra = saturate(max(ring, vk * 0.85));
                        col = lerp(col, _Foam.rgb, foamExtra);
                    }
                }

                if (_Caustic > 0.001)
                {
                    float2 cuv = i.positionWS.xz * 0.9;
                    float2 cip = floor(cuv);
                    float2 cfp = frac(cuv);
                    float m1 = 8.0;
                    float m2 = 8.0;
                    for (int cy = -1; cy <= 1; cy++)
                    {
                        for (int cx = -1; cx <= 1; cx++)
                        {
                            float2 cg = float2(cx, cy);
                            float2 cc = cip + cg;
                            float2 ch = frac(sin(float2(dot(cc, float2(127.1, 311.7)), dot(cc, float2(269.5, 183.3)))) * 43758.5453);
                            ch = 0.5 + 0.5 * sin(_Time.y * 0.7 + 6.2831 * ch);
                            float2 cr = cg + ch - cfp;
                            float cd = dot(cr, cr);
                            if (cd < m1) { m2 = m1; m1 = cd; }
                            else if (cd < m2) { m2 = cd; }
                        }
                    }
                    float cl = 1.0 - smoothstep(0.0, 0.10, sqrt(m2) - sqrt(m1));
                    col = lerp(col, _Shallow.rgb + 0.25, cl * _Caustic * (1.0 - 0.6 * d01));
                }

                float fallFoam = 0.0;
                float fallEdge = 1.0;
                if (_Streak > 0.001)
                {
                    // водопад: мягкие струи вдоль падения (три слоя шума, едут вниз с разной скоростью), без ступенек и резких границ.
                    // Вверху вода гладкая и прозрачно-голубая, чем ниже по падению, тем белее и рыхлее; у края лист плавно тает.
                    float flc = i.uv2.x;
                    float fhw = max(i.uv2.y, 0.5);
                    float fside = saturate(abs(flc) / fhw);
                    float ftime = _Time.y * _Speed;
                    float s1 = FNoise(float2(flc * 1.6, flow * 0.16 - ftime * 0.30));
                    float s2 = FNoise(float2(flc * 3.4 + 7.3, flow * 0.28 - ftime * 0.55));
                    float s3 = FNoise(float2(flc * 7.0 + 19.1, flow * 0.55 - ftime * 0.95));
                    float sn = s1 * 0.50 + s2 * 0.32 + s3 * 0.18;
                    float fstreak = smoothstep(0.46, 0.74, sn);
                    float fdark = 1.0 - smoothstep(0.30, 0.50, sn);
                    float faer = smoothstep(0.0, max(_FallLen, 1.0) * 0.9, flow);
                    col = lerp(col, col * 0.86, fdark * 0.45 * _Streak);
                    col = lerp(col, _Foam.rgb, saturate(fstreak * (0.30 + 0.45 * faer) + fside * fside * 0.35) * _Streak);
                    float ftop = (1.0 - smoothstep(0.0, 1.4, flow)) * (0.55 + 0.45 * FNoise(float2(flc * 2.0, ftime * 0.5)));
                    float fbot = 0.0;
                    if (_FallLen > 0.01 && _FallFade < 0.5)
                        fbot = smoothstep(_FallLen - 2.2, _FallLen - 0.2, flow) * (0.65 + 0.35 * FNoise(float2(flc * 2.5, ftime * 0.8)));
                    fallFoam = saturate(max(ftop * 0.8, fbot)) * _Streak;
                    col = lerp(col, _Foam.rgb, fallFoam);
                    float fedgeN = FNoise(float2(flc * 2.3, flow * 0.35 - ftime * 0.2));
                    float fedge = 1.0 - smoothstep(0.62 + 0.18 * fedgeN, 1.0, fside);
                    fallEdge = lerp(1.0, fedge, _Streak);
                    fallEdge *= lerp(1.0, 0.78 + 0.22 * saturate(sn * 1.4), _Streak * 0.6);
                    if (_FallFade > 0.5 && _FallLen > 0.01)
                        fallEdge *= 1.0 - smoothstep(_FallLen * 0.55, _FallLen * 0.98, flow);
                }

                float glint = 0.0;
                if (_Sparkle > 0.001)
                {
                    // две сетки мерцающих искр, плывущих по течению: на глубине ярче, у берега и в пене гаснут
                    for (int gk = 0; gk < 2; gk++)
                    {
                        float gsc = gk == 0 ? 2.6 : 4.1;
                        float2 gp = i.positionWS.xz * gsc + float2(_Time.y * 0.12, -_Time.y * 0.18 * _Speed) * (1.0 + gk);
                        float2 gi = floor(gp);
                        float2 gf = frac(gp);
                        float gh = frac(sin(dot(gi, float2(41.3, 289.1)) + gk * 17.0) * 43758.5453);
                        float2 gc = 0.2 + 0.6 * float2(frac(gh * 7.3), frac(gh * 13.7));
                        float gtw = pow(saturate(sin(_Time.y * (2.0 + gh * 3.0) + gh * 40.0) * 0.5 + 0.5), 6.0);
                        float gd = length(gf - gc);
                        glint = max(glint, (1.0 - smoothstep(0.05, 0.12, gd)) * gtw * step(0.6, gh));
                    }
                    glint *= smoothstep(0.15, 0.6, d01) * (1.0 - foamExtra) * (1.0 - shore) * _Sparkle;
                    col = lerp(col, half3(1.0, 1.0, 1.0), glint);
                }

                col = lerp(col, col * half3(1.05, 1.0, 0.91), 0.6);   // золотой час: вода чуть теплее
                half aBase = _Alpha;
                if (_ShallowAlpha >= 0.0) aBase = lerp(_ShallowAlpha, _Alpha, saturate(d01 * 1.6));
                half a = aBase * smoothstep(-0.02, 0.06, depth);
                a = max(a, shore * 0.95 * smoothstep(-0.02, 0.02, depth));
                a = max(a, foamExtra * 0.95 * smoothstep(-0.02, 0.02, depth));
                a = max(a, glint * 0.9 * smoothstep(-0.02, 0.02, depth));
                a = max(a, fallFoam * 0.95);
                a *= fallEdge;
                a *= 1.0 - _Pulse * (0.5 + 0.5 * sin(_Time.y * 1.3 + i.positionWS.x * 0.7 + i.positionWS.z * 0.9));

                half fogF = ComputeFogFactor(i.positionCS.z);
                col = MixFog(col, fogF);
                return half4(col, a);
            }
            ENDHLSL
        }
    }
}";

    static Shader _cardShader, _waterShader;

    static Shader EnsureShader(string name, string src)
    {
        EnsureFolder(AssetDir + "/Shaders");
        string path = AssetDir + "/Shaders/" + name + ".shader";
        string full = Path.Combine(Directory.GetParent(Application.dataPath).FullName, path);
        string text = src.Replace('`', '"');
        if (!File.Exists(full) || File.ReadAllText(full) != text)
        {
            File.WriteAllText(full, text);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        }
        var sh = AssetDatabase.LoadAssetAtPath<Shader>(path);
        if (sh == null || !sh.isSupported)
        {
            Debug.LogError("[LocationTester] Шейдер " + path + " не скомпилировался (см. ошибки шейдера в Console). Использую URP/Lit.");
            return null;
        }
        return sh;
    }

    static Shader CardShader()
    {
        if (_cardShader == null) _cardShader = EnsureShader("LocCardboard", CardShaderSrc);
        return _cardShader;
    }

    static Shader WaterShader()
    {
        if (_waterShader == null)
        {
            // Project/CandyWater: те же свойства, что у LocToonWater, плюс тиры качества (Shaders/Candy/CandyWater.shader).
            // Нет шейдера или он не собрался: старый встроенный шейдер воды.
            var cw = Shader.Find("Project/CandyWater");
            _waterShader = (cw != null && cw.isSupported) ? cw : EnsureShader("LocToonWater", WaterShaderSrc);
        }
        return _waterShader;
    }

    // картонный материал: solid-режим (трипланарный по _MainTex * tint) или splat-режим (8 слоёв из карт раскраски)
    static Material MakeCardMat(Cfg c, string name, Color tint, Texture2D tex, float tileM, float layerStep, float layerLine, bool splat,
                                Texture2D[] layers = null, Texture2D ctrl0 = null, Texture2D ctrl1 = null)
    {
        var sh = CardShader();
        if (sh == null) return MakeMat(name, tint, false, 0f);
        var m = new Material(sh) { name = name };
        m.SetColor("_Tint", tint);
        m.SetColor("_Shade", c.shadowTint);
        if (tex != null) m.SetTexture("_MainTex", tex);
        if (_grainTex != null) m.SetTexture("_Grain", _grainTex);
        m.SetFloat("_TileM", tileM);
        m.SetFloat("_Sharp", c.sharpEdges);
        m.SetFloat("_Wrap", c.toonWrap);
        m.SetFloat("_Rim", c.rimLight);
        m.SetFloat("_GrainAmt", c.paperGrain);
        m.SetFloat("_ContourStep", layerStep);
        m.SetFloat("_ContourAmt", layerLine);
        m.SetFloat("_ContourBase", c.origin.y - 0.5f * layerStep);   // локальный Y = 0 (площадки, центр) приходится на середину листа: без линий на ровном
        m.SetFloat("_Bright", c.brightness);
        m.SetFloat("_Sat", c.saturation);
        if (splat)
        {
            m.EnableKeyword("_SPLAT_ON");
            if (layers != null)
                for (int k = 0; k < layers.Length && k < 8; k++) m.SetTexture("_Tex" + k, layers[k]);
            if (ctrl0 != null) m.SetTexture("_Ctrl0", ctrl0);
            if (ctrl1 != null) m.SetTexture("_Ctrl1", ctrl1);
        }
        SaveAsset(m, AssetDir + "/Materials/" + name + ".mat");
        return m;
    }

    static Material MakeWaterMat(string name, Color deep, Color shallow, float alpha, float speed, float freq, float lines, float foamDepth, float depthScale = 1.2f)
    {
        var sh = WaterShader();
        if (sh == null) return MakeMat(name, new Color(deep.r, deep.g, deep.b, alpha), true, 0.9f);
        var m = new Material(sh) { name = name };
        m.SetColor("_Deep", deep);
        m.SetColor("_Shallow", shallow);
        m.SetFloat("_Alpha", alpha);
        m.SetFloat("_Speed", speed);
        m.SetFloat("_Freq", freq);
        m.SetFloat("_Lines", lines);
        m.SetFloat("_FoamDepth", foamDepth);
        m.SetFloat("_DepthScale", depthScale);
        m.renderQueue = (int)RenderQueue.Transparent;
        SaveAsset(m, AssetDir + "/Materials/" + name + ".mat");
        return m;
    }

    static TerrainLayer MakeLayer(string name, Texture2D tex, float tile)
    {
        var tl = new TerrainLayer();
        tl.diffuseTexture = tex;
        tl.tileSize = new Vector2(tile, tile);
        tl.smoothness = 0f;
        tl.metallic = 0f;
        SaveAsset(tl, AssetDir + "/Layers/" + name + ".terrainlayer");
        return tl;
    }

    static Material MakeMat(string name, Color col, bool transparent = false, float smooth = 0.1f)
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        var m = new Material(sh) { name = name };
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
        if (m.HasProperty("_Color")) m.SetColor("_Color", col);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        if (transparent)
        {
            m.SetOverrideTag("RenderType", "Transparent");
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_SrcBlendAlpha")) m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            if (m.HasProperty("_DstBlendAlpha")) m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetShaderPassEnabled("ShadowCaster", false);
        }
        SaveAsset(m, AssetDir + "/Materials/" + name + ".mat");
        return m;
    }

    // =====================================================================================================
    //  СБОРКА В СЦЕНЕ
    // =====================================================================================================

    void DeleteRoot()
    {
        var old = GameObject.Find(RootName);
        if (old != null) Undo.DestroyObjectImmediate(old);
        _labels.Clear();
    }

    void FrameRoot()
    {
        var go = GameObject.Find(RootName);
        var sv = SceneView.lastActiveSceneView;
        if (go == null || sv == null) return;
        var b = new Bounds(go.transform.position, Vector3.zero);
        foreach (var r in go.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
        var t = go.GetComponentInChildren<Terrain>();
        if (t != null) b.Encapsulate(t.terrainData.bounds.size * 0.5f + t.transform.position);
        sv.Frame(b, false);
    }

    void Build()
    {
        try
        {
            var c = _c;
            EditorUtility.DisplayProgressBar("Location TESTER", "План: контур, площадки, река, хребет, тропы", 0.05f);
            var L = BuildLayout(c);

            EditorUtility.DisplayProgressBar("Location TESTER", "Рельеф", 0.2f);
            var F = Compute(L);
            PondPlanGeometry(L, F);

            string bypass;
            bool closed = CheckBypass(L, F, out bypass);
            if (!closed) L.warn.Add(bypass);
            CheckTrails(L);
            CheckPond(L, F);

            EditorUtility.DisplayProgressBar("Location TESTER", "Раскраска: тропы, кромки, колеи", 0.4f);
            var alpha = BuildAlpha(L, F);

            EditorUtility.DisplayProgressBar("Location TESTER", "Текстуры и шейдеры", 0.55f);
            EnsureFolder(AssetDir + "/Layers");
            EnsureFolder(AssetDir + "/Materials");
            EnsureFolder(AssetDir + "/Meshes");
            EnsureFolder(AssetDir + "/Terrain");
            EnsureFolder(AssetDir + "/Shaders");
            var tex = MakeAllTextures(c.regenTextures);
            c.regenTextures = false;
            DeleteRoot();

            EditorUtility.DisplayProgressBar("Location TESTER", "Сборка в сцене", 0.7f);
            float ws = c.worldScale;
            int N = c.heightmapRes;
            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Build Location TESTER");
            root.transform.position = c.origin;

            // ---- Terrain (коллайдер; в картонном режиме сам не рисуется, вместо него меш Surface)
            float baseH = F.minH - 0.3f;
            float rangeU = (F.maxH - baseH) + 0.5f;
            var td = new TerrainData();
            td.heightmapResolution = N;
            td.size = new Vector3(L.terrainSize * ws, rangeU * ws, L.terrainSize * ws);
            var hm = new float[N, N];
            for (int j = 0; j < N; j++)
                for (int i = 0; i < N; i++) hm[j, i] = Mathf.Clamp01((F.H[j * N + i] - baseH) / rangeU);
            td.SetHeights(0, 0, hm);

            TerrainLayer[] layers;
            if (c.cardboard)
                layers = new[] { MakeLayer("L0_Grass", tex[0], c.tileM) };
            else
            {
                layers = new TerrainLayer[LayerCount];
                string[] ln = { "L0_Grass", "L1_GrassLight", "L2_Trail", "L3_Soil", "L4_Rock", "L5_ForestFloor", "L6_Sand", "L7_TrailDark" };
                for (int k = 0; k < LayerCount; k++) layers[k] = MakeLayer(ln[k], tex[k], c.tileM);
            }
            td.alphamapResolution = c.alphaRes;
            td.terrainLayers = layers;
            if (!c.cardboard) td.SetAlphamaps(0, 0, alpha);

            int hr = td.holesResolution;
            var holes = new bool[hr, hr];
            float hcell = L.terrainSize / hr;
            for (int j = 0; j < hr; j++)
                for (int i = 0; i < hr; i++)
                    holes[j, i] = L.outline.Inside(-L.half + (i + 0.5f) * hcell, -L.half + (j + 0.5f) * hcell);   // true = земля, false = дырка
            td.SetHoles(0, 0, holes);

            SaveAsset(td, AssetDir + "/Terrain/LocationTest_TerrainData.asset");
            var tgo = Terrain.CreateTerrainGameObject(td);
            tgo.name = "Terrain";
            tgo.transform.SetParent(root.transform, false);
            tgo.transform.localPosition = new Vector3(-L.half * ws, baseH * ws, -L.half * ws);
            var terrain = tgo.GetComponent<Terrain>();
            terrain.heightmapPixelError = 1f;
            terrain.drawInstanced = false;
            terrain.drawHeightmap = !c.cardboard;
            terrain.drawTreesAndFoliage = false;

            // ---- Видимая поверхность: меш с картонным шейдером и 8 слоями раскраски
            if (c.cardboard)
            {
                EditorUtility.DisplayProgressBar("Location TESTER", "Поверхность (меш и карты раскраски)", 0.75f);
                var sm = BuildSurface(L, F);
                SaveAsset(sm, AssetDir + "/Meshes/LocationTest_Surface.asset");
                var ctrl0 = MakeCtrl("LocationTest_Splat0", alpha, c.alphaRes, 0);
                var ctrl1 = MakeCtrl("LocationTest_Splat1", alpha, c.alphaRes, 4);
                var tmat = MakeCardMat(c, "Card_Terrain", Color.white, null, c.tileM, c.contourStep, c.contourAmt, true, tex, ctrl0, ctrl1);
                var sg = new GameObject("Surface");
                sg.transform.SetParent(root.transform, false);
                sg.AddComponent<MeshFilter>().sharedMesh = sm;
                var smr = sg.AddComponent<MeshRenderer>();
                smr.sharedMaterial = tmat;
                smr.shadowCastingMode = ShadowCastingMode.On;
                smr.receiveShadows = true;
            }

            // ---- Низ острова
            if (c.underside)
            {
                EditorUtility.DisplayProgressBar("Location TESTER", "Низ острова", 0.82f);
                var um = BuildUnderside(L, F);
                SaveAsset(um, AssetDir + "/Meshes/LocationTest_Underside.asset");
                var ug = new GameObject("Underside");
                ug.transform.SetParent(root.transform, false);
                ug.AddComponent<MeshFilter>().sharedMesh = um;
                Material[] umats;
                if (c.cardboard)
                {
                    float ls = c.contourStep * 2f, la = c.contourAmt * 1.4f, tr = c.tileM * 1.3f;
                    umats = new[]
                    {
                        MakeCardMat(c, "Under_GrassLip", Color.white, tex[0], c.tileM, c.contourStep, c.contourAmt, false),
                        MakeCardMat(c, "Under_Soil", Color.white, tex[3], c.tileM, c.contourStep, c.contourAmt, false),
                        MakeCardMat(c, "Under_RockA", new Color(1f, 0.96f, 0.92f), tex[4], tr, ls, la, false),
                        MakeCardMat(c, "Under_RockB", new Color(0.92f, 0.90f, 1.02f), tex[4], tr, ls, la, false),
                        MakeCardMat(c, "Under_Deep", new Color(0.74f, 0.70f, 0.90f), tex[4], c.tileM * 1.6f, ls, la, false)
                    };
                }
                else
                {
                    umats = new[]
                    {
                        MakeMat("Under_GrassLip", new Color(0.38f, 0.70f, 0.25f), false, 0f),
                        MakeMat("Under_Soil", new Color(0.42f, 0.30f, 0.20f), false, 0f),
                        MakeMat("Under_RockA", new Color(0.52f, 0.50f, 0.52f), false, 0f),
                        MakeMat("Under_RockB", new Color(0.45f, 0.44f, 0.48f), false, 0f),
                        MakeMat("Under_Deep", new Color(0.30f, 0.30f, 0.36f), false, 0f)
                    };
                }
                ug.AddComponent<MeshRenderer>().sharedMaterials = umats;
                ug.AddComponent<MeshCollider>().sharedMesh = um;
            }

            _labels.Clear();
            if (c.lakeRadius > 0.5f) _labels.Add(new KeyValuePair<Vector3, string>(new Vector3(L.lakePos.x * ws, 2f * ws, L.lakePos.y * ws), "ЯМА под пруд с островом"));

            // ---- Валуны
            if (c.rocks)
            {
                var rkm = BuildRocks(L, F);
                SaveAsset(rkm, AssetDir + "/Meshes/LocationTest_Rocks.asset");
                Material rockMat = c.cardboard
                    ? MakeCardMat(c, "Card_Boulder", new Color(1.04f, 1f, 1.08f), tex[4], c.tileM * 0.8f, 0f, 0f, false)
                    : MakeMat("Boulder", new Color(0.52f, 0.50f, 0.52f), false, 0f);
                if (c.cardboard && rockMat.HasProperty("_MossAmt"))
                {
                    rockMat.SetFloat("_MossAmt", 0.75f);
                    rockMat.SetColor("_MossCol", new Color(0.52f, 0.78f, 0.36f));
                }
                var rg = MakeMeshGo(root.transform, "Boulders", rkm, rockMat);
                rg.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On;

                if (L.mtn != null && L.river != null)
                {
                    var spm = BuildSpray(L, F);
                    SaveAsset(spm, AssetDir + "/Meshes/LocationTest_Spray.asset");
                    Material sprayMat = c.cardboard
                        ? MakeCardMat(c, "Card_Spray", Color.white, _paperTex, c.tileM * 0.6f, 0f, 0f, false)
                        : MakeMat("Spray", new Color(0.95f, 0.98f, 1f), false, 0f);
                    MakeMeshGo(root.transform, "Spray", spm, sprayMat);
                }
            }

            // ---- Ёлки, кусты, камыш, деревца на островках
            if (c.props)
            {
                var prm = BuildProps(L, F);
                SaveAsset(prm, AssetDir + "/Meshes/LocationTest_Props.asset");
                Material[] pmats;
                if (c.cardboard)
                    pmats = new[]
                    {
                        MakeCardMat(c, "Card_PropWood", new Color(1.25f, 1.1f, 0.95f), tex[3], c.tileM * 0.6f, 0f, 0f, false),
                        MakeCardMat(c, "Card_PropSpruce", new Color(0.42f, 0.78f, 0.90f), tex[0], c.tileM * 0.8f, 0f, 0f, false),
                        MakeCardMat(c, "Card_PropLeaf", new Color(0.95f, 1.1f, 0.75f), tex[0], c.tileM * 0.8f, 0f, 0f, false),
                        MakeCardMat(c, "Card_PropReed", new Color(1.25f, 1.15f, 0.60f), tex[0], c.tileM * 0.5f, 0f, 0f, false)
                    };
                else
                    pmats = new[]
                    {
                        MakeMat("Prop_Wood", new Color(0.45f, 0.30f, 0.18f), false, 0f),
                        MakeMat("Prop_Spruce", new Color(0.13f, 0.38f, 0.16f), false, 0f),
                        MakeMat("Prop_Leaf", new Color(0.34f, 0.65f, 0.24f), false, 0f),
                        MakeMat("Prop_Reed", new Color(0.70f, 0.65f, 0.30f), false, 0f)
                    };
                var prg = new GameObject("Props");
                prg.transform.SetParent(root.transform, false);
                prg.AddComponent<MeshFilter>().sharedMesh = prm;
                var prr = prg.AddComponent<MeshRenderer>();
                prr.sharedMaterials = pmats;
                prr.shadowCastingMode = ShadowCastingMode.On;
            }

            // ---- Берега реки: трава с цветами, отмели, бревна, кувшинки, рыбы, брызги (LocationBuilderTester.River.cs)
            if (L.river != null)
            {
                var rdm = BuildRiverDecor(L, F);
                SaveAsset(rdm, AssetDir + "/Meshes/LocationTest_RiverDecor.asset");
                var rdg = new GameObject("RiverDecor");
                rdg.transform.SetParent(root.transform, false);
                rdg.AddComponent<MeshFilter>().sharedMesh = rdm;
                var rdr = rdg.AddComponent<MeshRenderer>();
                rdr.sharedMaterials = MakeRiverDecorMats(c, tex);
                rdr.shadowCastingMode = ShadowCastingMode.Off;
            }

            // ---- Смотровая: деревянный балкон
            if (c.lookoutBuild && L.deckOn)
            {
                var lkm = BuildLookout(L);
                SaveAsset(lkm, AssetDir + "/Meshes/LocationTest_Lookout.asset");
                Color wd = c.lookoutWood;
                Material[] lmats;
                if (c.cardboard)
                {
                    float lt = c.tileM * 0.5f;
                    lmats = new[]
                    {
                        MakeCardMat(c, "Lookout_PlankA", wd, tex[3], lt, 0f, 0f, false),
                        MakeCardMat(c, "Lookout_PlankB", new Color(wd.r * 1.1f, wd.g * 1.05f, wd.b * 0.9f, 1f), tex[3], lt, 0f, 0f, false),
                        MakeCardMat(c, "Lookout_WoodDark", new Color(wd.r * 0.62f, wd.g * 0.52f, wd.b * 0.46f, 1f), tex[3], lt, 0f, 0f, false),
                        MakeCardMat(c, "Lookout_Cloth", new Color(1.1f, 0.30f, 0.26f, 1f), _paperTex, c.tileM * 0.6f, 0f, 0f, false),
                        MakeCardMat(c, "Lookout_Glow", new Color(1.7f, 1.45f, 0.65f, 1f), _paperTex, c.tileM * 0.6f, 0f, 0f, false),
                        MakeCardMat(c, "Lookout_Teal", new Color(0.30f, 0.85f, 0.90f, 1f), _paperTex, c.tileM * 0.6f, 0f, 0f, false),
                        MakeCardMat(c, "Lookout_Metal", new Color(0.36f, 0.50f, 0.66f, 1f), _paperTex, c.tileM * 0.6f, 0f, 0f, false)
                    };
                }
                else
                    lmats = new[]
                    {
                        MakeMat("Lookout_PlankA", new Color(0.66f, 0.44f, 0.24f), false, 0f),
                        MakeMat("Lookout_PlankB", new Color(0.72f, 0.50f, 0.28f), false, 0f),
                        MakeMat("Lookout_WoodDark", new Color(0.40f, 0.26f, 0.14f), false, 0f),
                        MakeMat("Lookout_Cloth", new Color(0.90f, 0.22f, 0.20f), false, 0f),
                        MakeMat("Lookout_Glow", new Color(1f, 0.85f, 0.40f), false, 0f),
                        MakeMat("Lookout_Teal", new Color(0.20f, 0.70f, 0.75f), false, 0f),
                        MakeMat("Lookout_Metal", new Color(0.30f, 0.38f, 0.50f), false, 0f)
                    };
                var lkg = new GameObject("Lookout");
                lkg.transform.SetParent(root.transform, false);
                lkg.AddComponent<MeshFilter>().sharedMesh = lkm;
                var lkr = lkg.AddComponent<MeshRenderer>();
                lkr.sharedMaterials = lmats;
                lkr.shadowCastingMode = ShadowCastingMode.On;
                AddLookoutColliders(L, lkg.transform);
            }

            // ---- Вода
            if (c.buildWater)
            {
                EditorUtility.DisplayProgressBar("Location TESTER", "Вода", 0.9f);
                var wgo = new GameObject("Water");
                wgo.transform.SetParent(root.transform, false);
                Material riverMat, fallMat, pondMat, springMat, lipMat;
                if (c.cardboard)
                {
                    // плёсы тёмно-бирюзовые, мелководье светло-бирюзовое и прозрачное (дно просвечивает), пена чисто белая
                    riverMat = MakeWaterMat("Water_River", new Color(0.06f, 0.56f, 0.92f), new Color(0.50f, 0.93f, 0.95f), 0.92f,
                        1.4f * Mathf.Max(0.05f, c.riverFlowSpeed), 0.7f, 0.32f, 0.20f * Mathf.Clamp(c.riverFoamAmount, 0f, 2f), Mathf.Max(1.9f, c.depthValley * 0.9f));
                    ConfigureRiverWater(riverMat, L);   // карта следов за камнями, ленты течения (LocationBuilderTester.River.cs)
                    springMat = MakeWaterMat("Water_Spring", new Color(0.25f, 0.72f, 0.97f), new Color(0.92f, 0.99f, 1f), 0.94f, 3.4f, 1.2f, 0.9f, 0.15f);
                    // падающая вода: синий центр, белёсые края, струи рисует шейдер (ConfigureFallWater в LocationBuilderTester.Falls.cs)
                    fallMat = MakeWaterMat("Water_Fall", new Color(0.20f, 0.68f, 0.96f), new Color(0.98f, 0.99f, 0.96f), 0.93f, 3.4f, 1.2f, 0.12f, 0.0005f, 1.1f);
                    lipMat = MakeWaterMat("Water_FallEdge", new Color(0.20f, 0.68f, 0.96f), new Color(0.98f, 0.99f, 0.96f), 0.93f, 3.8f, 1.2f, 0.12f, 0.0005f, 1.1f);
                    pondMat = MakeWaterMat("Water_Pond", new Color(0.16f, 0.55f, 0.78f), new Color(0.62f, 0.92f, 0.88f), 0.82f, 0.8f, 0.5f, 0.22f, 0.6f, 3.6f);   // бирюза, мелко, пена у берега, круги от острова
                }
                else
                {
                    riverMat = MakeMat("Water", new Color(0.25f, 0.62f, 0.85f, 0.7f), true, 0.9f);
                    fallMat = MakeMat("WaterFall", new Color(0.92f, 0.97f, 1f, 0.8f), true, 0.5f);
                    springMat = fallMat; lipMat = fallMat;
                    pondMat = riverMat;
                }

                if (pondMat != null) pondMat.SetFloat("_Caustic", c.cardboard ? 0.25f : 0f);   // блики-рябь на дне пруда

                if (L.river != null)
                {
                    var rm = BuildRiverRibbon(L, F, 0f, L.river.lipS, "LocationTest_River", 1f, false);
                    SaveAsset(rm, AssetDir + "/Meshes/LocationTest_River.asset");
                    MakeMeshGo(wgo.transform, "River", rm, riverMat);
                    AddWaterVolume(wgo, L);   // объём воды для плавания (Scripts/Location/WaterVolume.cs)
                    if (L.mtn != null)
                    {
                        var mf = BuildMountainFallV2(L);
                        SaveAsset(mf, AssetDir + "/Meshes/LocationTest_MountainFall.asset");
                        ConfigureFallWater(fallMat, FallMeshLen(mf), false, 1f);
                        MakeMeshGo(wgo.transform, "Waterfall_Mountain", mf, fallMat);
                        var sw = BuildSlotWater(L);
                        SaveAsset(sw, AssetDir + "/Meshes/LocationTest_SourceWater.asset");
                        MakeMeshGo(wgo.transform, "Spring_Niche", sw, springMat);
                        _labels.Add(new KeyValuePair<Vector3, string>(new Vector3(L.mtn.c.x * ws, (c.mountainHeight + 3f) * ws, L.mtn.c.y * ws), "ГОРА (плато, ёлки, тропа)"));
                        _labels.Add(new KeyValuePair<Vector3, string>(new Vector3(L.mtn.foot.x * ws, (L.mtn.lipY + 2f) * ws, L.mtn.foot.y * ws), "ВОДОПАД горы"));
                    }
                    var fm = BuildLipFallV2(L);
                    SaveAsset(fm, AssetDir + "/Meshes/LocationTest_FallLip.asset");
                    ConfigureFallWater(lipMat, FallMeshLen(fm), true, 1f);   // лист растворяется внизу в туман
                    MakeMeshGo(wgo.transform, "Waterfall_Edge", fm, lipMat);
                    if (c.riverMist)
                    {
                        var mm = BuildFallMist(L);
                        SaveAsset(mm, AssetDir + "/Meshes/LocationTest_FallMist.asset");
                        Material mistMat = c.cardboard
                            ? MakeWaterMat("Water_Mist", new Color(0.86f, 0.96f, 1f), Color.white, 0.42f, 0.2f, 0.5f, 0f, 0.0005f, 1f)
                            : MakeMat("Water_Mist", new Color(1f, 1f, 1f, 0.35f), true, 0f);
                        if (c.cardboard) mistMat.SetFloat("_Pulse", 0.3f);   // туман слегка дышит
                        MakeMeshGo(wgo.transform, "Mist_Edge", mm, mistMat);
                        if (L.mtn != null)
                        {
                            var fmm = BuildFootMist(L);   // туман в чаше у подножия горы
                            SaveAsset(fmm, AssetDir + "/Meshes/LocationTest_FootMist.asset");
                            MakeMeshGo(wgo.transform, "Mist_Mountain", fmm, mistMat);
                        }
                    }
                    _labels.Add(new KeyValuePair<Vector3, string>(new Vector3(L.river.lip.x * ws, (L.river.WaterY(L.river.lipS) + 2f) * ws, L.river.lip.y * ws), "ВОДОПАД с края"));
                    if (c.riverMist) BuildWaterFx(L, root.transform);   // брызги у подножий, до 40 частиц на весь остров
                }
                if (L.pond != null)
                {
                    var pm = BuildPond(L, F, "LocationTest_Pond");
                    SaveAsset(pm, AssetDir + "/Meshes/LocationTest_Pond.asset");
                    MakeMeshGo(wgo.transform, "Pond", pm, pondMat);
                    BuildPondStructures(L, F, root.transform, tex);
                    _labels.Add(new KeyValuePair<Vector3, string>(new Vector3(L.pond.center.x * ws, 4f * ws, L.pond.center.y * ws), "ПРУД (пирс, старая лодка)"));
                }
            }

            // ---- Невидимые стенки (река, хребет)
            if (c.riverBarrier) BuildBarriers(L, F, root.transform);

            // ---- Маркеры
            if (c.buildMarkers) BuildMarkers(L, F, root.transform);

            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Готово. seed=" + c.seed + ", Terrain " + L.terrainSize.ToString("0") + " u, троп: " + L.trails.Count + ", высота " + F.minH.ToString("0.0") + " .. " + F.maxH.ToString("0.0") + " u.");
            sb.AppendLine(closed ? "Обход закрыт: к смотровой по суше без моста не дойти (проверено по сетке рельефа)." : "Обход ЕСТЬ, см. предупреждение ниже.");
            var tnames = new List<string>();
            foreach (var trn in L.trails) tnames.Add(trn.name);
            sb.AppendLine("Тропы (" + tnames.Count + "): " + string.Join(", ", tnames));
            if (L.mtn != null) sb.AppendLine("Гора: " + L.mtn.c.x.ToString("0") + ", " + L.mtn.c.y.ToString("0") + ", плато " + c.mountainHeight.ToString("0") + " u, водопад " + (L.mtn.lipY - L.river.WaterY(0f)).ToString("0.0") + " u, островков " + L.islets.Count + ".");
            foreach (var w in L.warn) sb.AppendLine("! " + w);
            _log = sb.ToString().TrimEnd();
            foreach (var w in L.warn) Debug.LogWarning("[LocationTester] " + w);
            Debug.Log("[LocationTester] " + _log);
            FrameRoot();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            _log = "Ошибка: " + e.Message;
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    // =====================================================================================================
    //  СМОТРОВАЯ: деревянный балкон (меш в u, в конце масштабируется на worldScale)
    // =====================================================================================================

    static readonly int[][] BoxFaces = { new[] { 1, 3, 7, 5 }, new[] { 0, 4, 6, 2 }, new[] { 2, 6, 7, 3 }, new[] { 0, 1, 5, 4 }, new[] { 4, 5, 7, 6 }, new[] { 0, 2, 3, 1 } };
    static readonly Vector3[] BoxNormals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };

    // параллелепипед: центр, полуразмеры, поворот; грани плоские (у каждой свои вершины)
    static void LkBox(MB mb, int sub, Vector3 ctr, Vector3 h, Quaternion q)
    {
        var p = new Vector3[8];
        for (int i = 0; i < 8; i++)
            p[i] = ctr + q * new Vector3((i & 1) == 0 ? -h.x : h.x, (i & 2) == 0 ? -h.y : h.y, (i & 4) == 0 ? -h.z : h.z);
        for (int f = 0; f < 6; f++)
        {
            var fi = BoxFaces[f];
            mb.Quad(sub, p[fi[0]], p[fi[1]], p[fi[2]], p[fi[3]], q * BoxNormals[f]);
        }
    }

    // балка от точки a до точки b квадратного сечения thick
    static void LkBeam(MB mb, int sub, Vector3 a, Vector3 b, float thick)
    {
        Vector3 d = b - a;
        float l = d.magnitude;
        if (l < 0.01f) return;
        Vector3 dn = d / l;
        Quaternion q = Quaternion.LookRotation(dn, Mathf.Abs(dn.y) > 0.98f ? Vector3.right : Vector3.up);
        LkBox(mb, sub, (a + b) * 0.5f, new Vector3(thick * 0.5f, thick * 0.5f, l * 0.5f), q);
    }

    // гирлянда: провисающая верёвка и лампочки трёх цветов
    static void LkGarland(MB mb, Vector2 ga, Vector2 gb, float gy, int subRope, int subA, int subB, int subC)
    {
        float gl = Vector2.Distance(ga, gb);
        int nb = Mathf.Max(3, Mathf.RoundToInt(gl / 0.9f));
        Vector3 prev = new Vector3(ga.x, gy, ga.y);
        for (int gi = 1; gi <= nb; gi++)
        {
            float gt = gi / (float)nb;
            Vector2 pxz = Vector2.Lerp(ga, gb, gt);
            Vector3 cur = new Vector3(pxz.x, gy - 0.7f * 4f * gt * (1f - gt), pxz.y);
            LkBeam(mb, subRope, prev, cur, 0.06f);
            if (gi < nb)
            {
                int sb = gi % 3 == 0 ? subA : (gi % 3 == 1 ? subB : subC);
                LkBox(mb, sb, cur + new Vector3(0f, -0.18f, 0f), new Vector3(0.14f, 0.18f, 0.14f), Quaternion.identity);
            }
            prev = cur;
        }
    }

    static Mesh BuildLookout(Layout L)
    {
        var c = L.c; float ws = c.worldScale;
        var mb = new MB(7);
        var rnd = new System.Random(c.seed * 31 + 7);
        const int PA = 0, PB = 1, WD = 2, RED = 3, GLO = 4, TEA = 5, MET = 6;
        float x0 = L.deckX0, x1 = L.deckX1, z0 = L.deckZ, hw = L.deckHW, top = L.deckTop, gnd = c.lookoutHeight;
        float len = x0 - x1, midX = (x0 + x1) * 0.5f;
        float railH = Mathf.Clamp(c.lookoutRailH, 1.2f, 3.5f);
        float gw = Mathf.Clamp(c.lookoutGateW, 2f, Mathf.Max(2f, hw * 2f - 2f));
        Quaternion qi = Quaternion.identity;

        // ---- настил: доски поперёк, чуть разной длины, чередуются два тона
        int np = Mathf.Max(3, Mathf.FloorToInt(len / 0.95f));
        float pStep = len / np;
        for (int i = 0; i < np; i++)
        {
            float lz = hw + 0.15f + (Rf(rnd) - 0.5f) * 0.12f;
            LkBox(mb, i % 2 == 0 ? PA : PB, new Vector3(x1 + (i + 0.5f) * pStep, top - 0.1f, z0), new Vector3(pStep * 0.5f - 0.05f, 0.1f, lz), qi);
        }

        // ---- каркас под настилом: лаги вдоль, боковые балки, торцевые балки
        float jy = top - 0.2f - 0.25f;
        float[] jz = { -0.55f * hw, 0f, 0.55f * hw };
        foreach (float j in jz)
            LkBox(mb, WD, new Vector3(midX, jy, z0 + j), new Vector3(len * 0.5f + 0.1f, 0.25f, 0.17f), qi);
        for (int sd0 = -1; sd0 <= 1; sd0 += 2)
            LkBox(mb, WD, new Vector3(midX, top - 0.47f, z0 + sd0 * (hw - 0.1f)), new Vector3(len * 0.5f + 0.1f, 0.27f, 0.24f), qi);
        LkBox(mb, WD, new Vector3(x1 + 0.05f, top - 0.47f, z0), new Vector3(0.27f, 0.27f, hw + 0.15f), qi);
        LkBox(mb, WD, new Vector3(x0 - 0.05f, top - 0.47f, z0), new Vector3(0.27f, 0.27f, hw + 0.15f), qi);

        // ---- косые опорные стойки: от дальнего края настила вниз и назад, в скалу под кромкой (там не видны), + поперечные связи
        float[] strutZ = { -(hw - 0.5f), 0f, hw - 0.5f };
        var ties = new List<Vector3>();
        foreach (float so in strutZ)
        {
            float zz = z0 + so;
            float ex = EdgeWest(L.outline, zz);
            Vector3 sa = new Vector3(x1 + 0.5f, top - 0.8f, zz);
            Vector3 sb = new Vector3(ex + 2.4f, top - 3.8f, zz);
            LkBeam(mb, WD, sa, sb, 0.45f);
            float plateT = Mathf.Clamp01((ex + 1.3f - sa.x) / Mathf.Max(0.1f, sb.x - sa.x));
            Vector3 pl = Vector3.Lerp(sa, sb, plateT);
            LkBox(mb, WD, new Vector3(ex + 1.3f, pl.y + 0.1f, zz), new Vector3(0.3f, 0.7f, 0.55f), qi);   // планка-кронштейн у стены
            ties.Add(Vector3.Lerp(sa, sb, 0.62f));
        }
        for (int ti = 0; ti + 1 < ties.Count; ti++) LkBeam(mb, WD, ties[ti], ties[ti + 1], 0.3f);

        // ---- ступени у входа (восточный край): блоки со ступенчатой высотой и две косые тетивы по бокам
        int sn = L.deckSteps; float sd = L.deckStepD;
        float rise = (top - gnd) / (sn + 1f);
        for (int k = 1; k <= sn; k++)
        {
            float topY = top - k * rise, botY = gnd - 0.25f;
            LkBox(mb, k % 2 == 1 ? PB : PA, new Vector3(x0 + (k - 0.5f) * sd, (topY + botY) * 0.5f, z0), new Vector3(sd * 0.5f - 0.02f, (topY - botY) * 0.5f, gw * 0.5f), qi);
        }
        for (int sgn = -1; sgn <= 1; sgn += 2)
        {
            float zz = z0 + sgn * (gw * 0.5f + 0.14f);
            LkBeam(mb, WD, new Vector3(x0 - 0.05f, top - 0.1f, zz), new Vector3(x0 + sn * sd + 0.05f, gnd + 0.05f, zz), 0.28f);
        }

        // ---- перила: стойки и две жерди по трём сторонам, с проёмом к тропе на восточной стороне
        float ins = 0.3f;
        float rx0 = x0 - ins, rx1 = x1 + ins, rzN = z0 + hw - ins, rzS = z0 - hw + ins;
        var segs = new List<Vector2[]>();
        segs.Add(new[] { new Vector2(rx1, rzS), new Vector2(rx1, rzN) });
        segs.Add(new[] { new Vector2(rx1, rzN), new Vector2(rx0, rzN) });
        segs.Add(new[] { new Vector2(rx1, rzS), new Vector2(rx0, rzS) });
        float gz = gw * 0.5f;
        if (rzN - (z0 + gz) > 0.8f)
        {
            segs.Add(new[] { new Vector2(rx0, rzN), new Vector2(rx0, z0 + gz) });
            segs.Add(new[] { new Vector2(rx0, rzS), new Vector2(rx0, z0 - gz) });
        }
        var posts = new List<Vector2>();
        float yTopRail = top + railH - 0.2f, yMidRail = top + railH * 0.5f - 0.1f;
        foreach (var sg in segs)
        {
            float sl = Vector2.Distance(sg[0], sg[1]);
            int n = Mathf.Max(1, Mathf.CeilToInt(sl / 2.6f));
            for (int i = 0; i <= n; i++)
            {
                Vector2 pt = Vector2.Lerp(sg[0], sg[1], i / (float)n);
                bool dup = false;
                foreach (var ex2 in posts) if ((ex2 - pt).sqrMagnitude < 0.01f) { dup = true; break; }
                if (!dup) posts.Add(pt);
            }
            LkBeam(mb, WD, new Vector3(sg[0].x, yTopRail, sg[0].y), new Vector3(sg[1].x, yTopRail, sg[1].y), 0.3f);
            LkBeam(mb, WD, new Vector3(sg[0].x, yMidRail, sg[0].y), new Vector3(sg[1].x, yMidRail, sg[1].y), 0.26f);
        }
        float postBot = top - 0.25f, postTop = top + railH + 0.1f;
        foreach (var pp in posts)
        {
            LkBox(mb, WD, new Vector3(pp.x, (postBot + postTop) * 0.5f, pp.y), new Vector3(0.22f, (postTop - postBot) * 0.5f, 0.22f), qi);
            LkBox(mb, WD, new Vector3(pp.x, postTop + 0.06f, pp.y), new Vector3(0.3f, 0.07f, 0.3f), qi);
        }

        // ---- фонари на высоких столбах (дальние углы и северо-восточный), гирлянда между ними
        int lamps = Mathf.Clamp(c.lookoutLamps, 0, 3);
        Vector2[] lampCorner = { new Vector2(rx1, rzN), new Vector2(rx1, rzS), new Vector2(rx0, rzN) };
        float lampTop = top + railH + 1.1f;
        for (int i = 0; i < lamps; i++)
        {
            Vector2 lp = lampCorner[i];
            LkBox(mb, WD, new Vector3(lp.x, (top + railH + lampTop) * 0.5f, lp.y), new Vector3(0.17f, (lampTop - top - railH) * 0.5f + 0.1f, 0.17f), qi);
            Vector2 inward = (new Vector2(midX, z0) - lp).normalized;
            Vector3 armA = new Vector3(lp.x, lampTop - 0.15f, lp.y);
            Vector3 armB = armA + new Vector3(inward.x, 0f, inward.y) * 1.0f;
            LkBeam(mb, WD, armA, armB, 0.16f);
            Vector3 lc = new Vector3(armB.x, armB.y - 0.8f, armB.z);
            LkBox(mb, WD, new Vector3(armB.x, armB.y - 0.17f, armB.z), new Vector3(0.04f, 0.17f, 0.04f), qi);
            LkBox(mb, GLO, lc, new Vector3(0.34f, 0.45f, 0.34f), qi);
            LkBox(mb, WD, lc + new Vector3(0f, 0.55f, 0f), new Vector3(0.46f, 0.1f, 0.46f), qi);
            LkBox(mb, WD, lc - new Vector3(0f, 0.5f, 0f), new Vector3(0.3f, 0.06f, 0.3f), qi);
            for (int bx2 = -1; bx2 <= 1; bx2 += 2)
                for (int bz2 = -1; bz2 <= 1; bz2 += 2)
                    LkBox(mb, WD, lc + new Vector3(bx2 * 0.34f, 0f, bz2 * 0.34f), new Vector3(0.045f, 0.45f, 0.045f), qi);
        }
        if (c.lookoutGarland && lamps >= 2)
        {
            LkGarland(mb, lampCorner[1], lampCorner[0], lampTop - 0.4f, WD, GLO, RED, TEA);
            if (lamps >= 3) LkGarland(mb, lampCorner[0], lampCorner[2], lampTop - 0.4f, WD, GLO, RED, TEA);
        }

        // ---- флажок на высоком древке (юго-восточный угол), полотнище развевается на запад над пустотой
        if (c.lookoutFlag)
        {
            Vector2 fp = new Vector2(rx0, rzS);
            float poleTop = top + railH + 5.2f;
            LkBox(mb, WD, new Vector3(fp.x, (top - 0.25f + poleTop) * 0.5f, fp.y), new Vector3(0.14f, (poleTop - top + 0.25f) * 0.5f, 0.14f), qi);
            LkBox(mb, GLO, new Vector3(fp.x, poleTop + 0.15f, fp.y), new Vector3(0.22f, 0.22f, 0.22f), qi);
            float fy1 = poleTop - 0.3f, fy2 = poleTop - 2.0f;
            Vector3 fa = new Vector3(fp.x, fy1, fp.y), fb = new Vector3(fp.x, fy2, fp.y);
            Vector3 fc = new Vector3(fp.x - 1.7f, fy1 - 0.05f, fp.y + 0.45f), fd = new Vector3(fp.x - 1.7f, fy2 + 0.25f, fp.y + 0.45f);
            Vector3 ft = new Vector3(fp.x - 3.2f, (fy1 + fy2) * 0.5f + 0.1f, fp.y - 0.15f);
            mb.Quad(RED, fa, fb, fd, fc, Vector3.forward);
            mb.Quad(RED, fa, fb, fd, fc, Vector3.back);
            mb.Tri(RED, fc, fd, ft, Vector3.forward);
            mb.Tri(RED, fc, fd, ft, Vector3.back);
        }

        // ---- скамейка у дальнего края, спинкой к входу (сидящий смотрит на запад), южная половина
        if (c.lookoutBench)
        {
            float bl = Mathf.Min(3f, hw * 0.7f);
            float bx = x1 + 1.7f, bz = z0 - (hw - ins - 0.22f - 0.2f - bl * 0.5f);
            float seatY = top + 0.95f;
            LkBox(mb, PA, new Vector3(bx - 0.27f, seatY - 0.09f, bz), new Vector3(0.24f, 0.09f, bl * 0.5f), qi);
            LkBox(mb, PB, new Vector3(bx + 0.27f, seatY - 0.09f, bz), new Vector3(0.24f, 0.09f, bl * 0.5f), qi);
            LkBox(mb, WD, new Vector3(bx, seatY - 0.3f, bz), new Vector3(0.06f, 0.06f, bl * 0.5f - 0.1f), qi);
            for (int bsx = -1; bsx <= 1; bsx += 2)
                for (int bsz = -1; bsz <= 1; bsz += 2)
                    LkBox(mb, WD, new Vector3(bx + bsx * 0.38f, top + 0.43f, bz + bsz * (bl * 0.5f - 0.25f)), new Vector3(0.11f, 0.43f, 0.11f), qi);
            for (int bsz = -1; bsz <= 1; bsz += 2)
                LkBox(mb, WD, new Vector3(bx + 0.56f, top + 0.95f, bz + bsz * (bl * 0.5f - 0.25f)), new Vector3(0.09f, 0.95f, 0.09f), qi);
            LkBox(mb, PB, new Vector3(bx + 0.56f, top + 1.35f, bz), new Vector3(0.06f, 0.16f, bl * 0.5f), qi);
            LkBox(mb, PA, new Vector3(bx + 0.56f, top + 1.75f, bz), new Vector3(0.06f, 0.16f, bl * 0.5f), qi);
        }

        // ---- бинокль (подзорная труба) на стойке с распорками, смотрит на запад-юго-запад и чуть вниз, северная половина
        if (c.lookoutScope)
        {
            float sx0 = x1 + 1.9f, sz0 = z0 + hw * 0.5f;
            LkBox(mb, WD, new Vector3(sx0, top + 0.07f, sz0), new Vector3(0.7f, 0.07f, 0.7f), qi);
            LkBox(mb, MET, new Vector3(sx0, top + 1.2f, sz0), new Vector3(0.14f, 1.05f, 0.14f), qi);
            for (int tl = 0; tl < 3; tl++)
            {
                float ang = tl * 2.0944f + 0.5f;
                LkBeam(mb, WD, new Vector3(sx0, top + 1.6f, sz0), new Vector3(sx0 + Mathf.Cos(ang) * 0.75f, top + 0.12f, sz0 + Mathf.Sin(ang) * 0.75f), 0.1f);
            }
            Vector3 pv = new Vector3(sx0, top + 2.5f, sz0);
            LkBox(mb, MET, pv - new Vector3(0f, 0.2f, 0f), new Vector3(0.22f, 0.12f, 0.22f), qi);
            float pitch = 10f * Mathf.Deg2Rad, yaw = 16f * Mathf.Deg2Rad;
            Vector3 dv = new Vector3(-Mathf.Cos(pitch) * Mathf.Cos(yaw), -Mathf.Sin(pitch), -Mathf.Cos(pitch) * Mathf.Sin(yaw));
            LkBeam(mb, MET, pv - dv * 0.9f, pv + dv * 1.6f, 0.5f);
            LkBeam(mb, GLO, pv + dv * 1.6f, pv + dv * 2.05f, 0.72f);
            LkBeam(mb, GLO, pv + dv * 0.5f, pv + dv * 0.68f, 0.64f);
            LkBeam(mb, GLO, pv - dv * 0.9f, pv - dv * 1.25f, 0.32f);
        }

        for (int vi = 0; vi < mb.v.Count; vi++) mb.v[vi] = mb.v[vi] * ws;
        return mb.Build("LocationTest_Lookout");
    }

    static void AddBoxCol(Transform parent, string name, Vector3 centerU, Vector3 sizeU, float ws)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = centerU * ws;
        g.AddComponent<BoxCollider>().size = sizeU * ws;
    }

    // коллайдеры балкона: настил, ступени, перила (игрок на Rigidbody не должен упасть за край)
    static void AddLookoutColliders(Layout L, Transform lookout)
    {
        var c = L.c; float ws = c.worldScale;
        var cg = new GameObject("Colliders");
        cg.transform.SetParent(lookout, false);
        float x0 = L.deckX0, x1 = L.deckX1, z0 = L.deckZ, hw = L.deckHW, top = L.deckTop, gnd = c.lookoutHeight;
        float len = x0 - x1;
        float railH = Mathf.Clamp(c.lookoutRailH, 1.2f, 3.5f);
        float gw = Mathf.Clamp(c.lookoutGateW, 2f, Mathf.Max(2f, hw * 2f - 2f));

        AddBoxCol(cg.transform, "Deck", new Vector3((x0 + x1) * 0.5f, top - 0.3f, z0), new Vector3(len + 0.2f, 0.6f, hw * 2f + 0.3f), ws);

        int sn = L.deckSteps; float sd = L.deckStepD;
        float rise = (top - gnd) / (sn + 1f);
        for (int k = 1; k <= sn; k++)
        {
            float topY = top - k * rise, botY = gnd - 0.25f;
            AddBoxCol(cg.transform, "Step" + k, new Vector3(x0 + (k - 0.5f) * sd, (topY + botY) * 0.5f, z0), new Vector3(sd, topY - botY, gw), ws);
        }

        float ins = 0.3f;
        float rx0 = x0 - ins, rx1 = x1 + ins, rzN = z0 + hw - ins, rzS = z0 - hw + ins;
        float rh = railH + 1.2f, ry = top + rh * 0.5f - 0.3f;   // коллайдер выше визуальных перил
        AddBoxCol(cg.transform, "RailWest", new Vector3(rx1, ry, z0), new Vector3(0.5f, rh, hw * 2f), ws);
        AddBoxCol(cg.transform, "RailNorth", new Vector3((rx0 + rx1) * 0.5f, ry, rzN), new Vector3(rx0 - rx1 + 0.5f, rh, 0.5f), ws);
        AddBoxCol(cg.transform, "RailSouth", new Vector3((rx0 + rx1) * 0.5f, ry, rzS), new Vector3(rx0 - rx1 + 0.5f, rh, 0.5f), ws);
        float gz = gw * 0.5f;
        if (rzN - (z0 + gz) > 0.8f)
        {
            float lenE = rzN - (z0 + gz);
            AddBoxCol(cg.transform, "RailEastN", new Vector3(rx0, ry, z0 + gz + lenE * 0.5f), new Vector3(0.5f, rh, lenE + 0.3f), ws);
            AddBoxCol(cg.transform, "RailEastS", new Vector3(rx0, ry, z0 - gz - lenE * 0.5f), new Vector3(0.5f, rh, lenE + 0.3f), ws);
        }
    }


    static GameObject MakeMeshGo(Transform parent, string name, Mesh m, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = m;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        return go;
    }

    GameObject Prim(Transform parent, string name, PrimitiveType type, Vector3 worldPos, Vector3 scale, Color col)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        var col2 = go.GetComponent<Collider>();
        if (col2 != null) UnityEngine.Object.DestroyImmediate(col2);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = worldPos;
        go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = MakeMat("Marker_" + name, col, false, 0f);
        return go;
    }

    void BuildMarkers(Layout L, Field F, Transform root)
    {
        var c = L.c; float ws = c.worldScale;
        var mk = new GameObject("Markers");
        mk.transform.SetParent(root, false);
        Func<Vector2, float> Hy = p => F.g.At(F.H, p.x, p.y);

        // центр
        Prim(mk.transform, "Center", PrimitiveType.Cylinder, new Vector3(0f, (Hy(Vector2.zero) + 0.06f) * ws, 0f),
            new Vector3(c.centerRadius * 2f * ws, 0.03f * ws, c.centerRadius * 2f * ws), new Color(0.85f, 0.8f, 0.7f));
        _labels.Add(new KeyValuePair<Vector3, string>(new Vector3(0f, 2f * ws, 0f), "ЦЕНТР"));

        foreach (var p in L.pads)
        {
            if (p.round) { _labels.Add(new KeyValuePair<Vector3, string>(new Vector3(p.pos.x * ws, (Hy(p.pos) + 6f) * ws, p.pos.y * ws), "ЛЕС (" + p.name + ")")); continue; }
            Vector3 wp = new Vector3(p.pos.x * ws, (Hy(p.pos) + 0.08f) * ws, p.pos.y * ws);
            var go = Prim(mk.transform, "Pad_" + p.name, PrimitiveType.Cube, wp, new Vector3(p.hw * 2f * ws, 0.06f * ws, p.hd * 2f * ws), p.color);
            go.transform.rotation = Quaternion.LookRotation(new Vector3(p.fwd.x, 0f, p.fwd.y), Vector3.up) * Quaternion.identity;
            _labels.Add(new KeyValuePair<Vector3, string>(wp + Vector3.up * 1.5f * ws, p.name));
        }

        // мост через реку + смотровая
        if (L.bridge != null && L.river != null)
        {
            Vector2 a = L.bridge.pos - L.bridge.fwd * L.bridge.hd, b = L.landing;
            Vector2 mid = (a + b) * 0.5f;
            if (c.bridgeBuild) BuildBridge(L, F, root);   // деревянный мост (LocationBuilderTester.Bridge.cs); при неудаче сам оставит маркер-куб
            else
            {
                float len = Vector2.Distance(a, b);
                var br = Prim(mk.transform, "Bridge_River", PrimitiveType.Cube, new Vector3(mid.x * ws, (Hy(a) + 0.5f) * ws, mid.y * ws),
                    new Vector3(len * ws, 0.25f * ws, 3f * ws), Color.white);
                br.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            }
            _labels.Add(new KeyValuePair<Vector3, string>(new Vector3(mid.x * ws, (Hy(a) + 3f) * ws, mid.y * ws), "МОСТ"));

            if (c.lookoutBuild && L.deckOn)
            {
                Vector2 dc = new Vector2((L.deckX0 + L.deckX1) * 0.5f, L.deckZ);
                _labels.Add(new KeyValuePair<Vector3, string>(new Vector3(dc.x * ws, (L.deckTop + 6.5f) * ws, dc.y * ws), "СМОТРОВАЯ (балкон)"));
            }
            else
            {
                Prim(mk.transform, "Lookout", PrimitiveType.Cylinder, new Vector3(L.lookout.x * ws, (c.lookoutHeight + 0.1f) * ws, L.lookout.y * ws),
                    new Vector3(c.lookoutRadius * 2f * ws, 0.1f * ws, c.lookoutRadius * 2f * ws), new Color(0.95f, 0.9f, 0.8f));
                _labels.Add(new KeyValuePair<Vector3, string>(new Vector3(L.lookout.x * ws, (c.lookoutHeight + 3f) * ws, L.lookout.y * ws), "СМОТРОВАЯ"));
            }
        }

        if (L.mtn != null && L.mtn.hasGate)
            _labels.Add(new KeyValuePair<Vector3, string>(new Vector3(L.mtn.gate.x * ws, (Hy(L.mtn.gate) + 3f) * ws, L.mtn.gate.y * ws), "ТРОПА НА ГОРУ"));

        // ворота: мост на следующую локацию, уходит за северный край
        {
            Vector2 dir = L.gate.normalized;
            Vector2 mid = L.gate + dir * 4f;
            var gb = Prim(mk.transform, "Bridge_Gate", PrimitiveType.Cube, new Vector3(mid.x * ws, (Hy(L.gate - dir * 2f) + 0.4f) * ws, mid.y * ws),
                new Vector3(3f * ws, 0.25f * ws, 8f * ws), Color.white);
            gb.transform.rotation = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y), Vector3.up);
            _labels.Add(new KeyValuePair<Vector3, string>(new Vector3(mid.x * ws, (Hy(L.gate - dir * 2f) + 3f) * ws, mid.y * ws), "ВОРОТА / мост на след. локацию"));
        }
    }
}
#endif
