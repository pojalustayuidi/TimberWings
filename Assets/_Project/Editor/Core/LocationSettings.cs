#if UNITY_EDITOR
// Assets/_Project/Editor/Island/Core/LocationSettings.cs
// Данные генератора: настройки, площадки, таблица палитры окружения.
using System;
using System.Collections.Generic;
using UnityEngine;

public partial class LocationBuilder
{
    // ------------------------------------------------------------------ палитра (индексы в envColors)
    // Индексы используются и модулями (Cliffs берёт 0,1,2,16,17,22; Center 23,26,29...), порядок не менять.
    const int M_ROCK0 = 0, M_ROCK1 = 1, M_ROCK2 = 2, M_BUSH0 = 3, M_BUSH1 = 4, M_GRASS = 5, M_STEM = 6,
        M_FW = 7, M_FR = 8, M_FY = 9, M_WOOD = 10, M_WOODL = 11, M_FBUSH = 12, M_FERN = 13, M_CAP = 14, M_MSTEM = 15,
        M_ROCK3 = 16, M_MOSS = 17, M_REED = 18, M_PEBBLE = 19, M_SLAB = 20, M_CATTAIL = 21,
        M_ROCK4 = 22, M_COBBLE = 23, M_FLAME = 24, M_FLAMEIN = 25, M_CHARCOAL = 26, M_LILY = 27,
        M_LILYF = 28, M_LANTERN = 29, M_FOAM = 30;

    // мелочь: идёт в отдельные чанки без теней (дешевле на отрисовке)
    static bool IsSmall(int m)
    {
        return m == M_GRASS || m == M_STEM || m == M_FW || m == M_FR || m == M_FY || m == M_FERN ||
               m == M_CAP || m == M_MSTEM || m == M_REED || m == M_PEBBLE || m == M_CATTAIL ||
               m == M_SLAB || m == M_LILY || m == M_LILYF || m == M_FOAM;
    }

    static readonly string[] MatNames =
    {
        "Rock0", "Rock1", "Rock2", "Bush0", "Bush1", "GrassTuft", "FlowerStem", "FlowerWhite", "FlowerRed",
        "FlowerYellow", "Wood", "WoodLight", "ForestBush", "Fern", "MushroomCap", "MushroomStem",
        "Rock3", "Moss", "Reed", "Pebble", "Slab", "Cattail",
        "Rock4", "Cobble", "Flame", "FlameInner", "Charcoal", "Lily", "LilyFlower", "Lantern", "Foam"
    };

    // ------------------------------------------------------------------ площадки

    [Serializable]
    public class PadDef
    {
        public string name = "Pad";
        public float angle;
        public float distance;
        public Vector2 size = new Vector2(6f, 6f); // x = ширина, y = глубина (вдоль подхода). Для round: x = радиус
        public bool round;
        public string parent = "";                 // пусто = тропа от центра; иначе ответвление от тропы этой площадки
        public float forkDistance = 12f;           // расстояние развилки от центра по родительской тропе
        public Color gizmoColor = Color.white;
        public bool straight;                      // тропа к площадке строго прямая, без изгибов (Exit)

        public PadDef() { }

        public PadDef(string n, float a, float d, float w, float h, Color c, string par = "", float fork = 12f, bool isRound = false)
        {
            name = n; angle = a; distance = d; size = new Vector2(w, h); gizmoColor = c;
            parent = par; forkDistance = fork; round = isRound;
        }
    }

    // ------------------------------------------------------------------ карта размещения (Р1)
    // ЕДИНАЯ таблица зон острова в u (1 u = worldScale м): центр острова (0,0), +z = север (вверх на виде сверху), +x = восток.
    // DefaultPads() строит площадки из строк с pad = true, lakePos берётся из строки Lake.
    // Строки с pad = false: только данные для других модулей (скала-исток, смотровая, озеро). Площадки у них нет:
    // исток строит riverHead, озеро делает рельеф (lakeEnabled), смотровая стоит за краем Terrain (площадка туда не встанет).
    // Прочие модули берут позиции через ZonePos("Имя"), а не держат свои числа.
    [Serializable]
    public class Zone
    {
        public string name;
        public Vector2 pos;
        public float w, h;                 // прямоугольник w x h, у круга w = радиус
        public bool round, pad = true, straight;
        public string parent = "";
        public float fork = 12f;
        public Color color = Color.white;
    }

    static Vector2 Pol(float angDeg, float dist)   // угол от севера по часовой, как в SolveLayout
    {
        float a = angDeg * Mathf.Deg2Rad;
        return new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * dist;
    }

    static Zone Z(string n, Vector2 pos, float w, float h, Color c, bool round = false, string parent = "", float fork = 12f, bool pad = true, bool straight = false)
    {
        return new Zone { name = n, pos = pos, w = w, h = h, color = c, round = round, parent = parent, fork = fork, pad = pad, straight = straight };
    }

    // Старые площадки (Tent, Hut, Market, QuestBoard, Forest, Bridge, Order1..3) стоят там же, где и стояли: лес на юго-западе
    // (место под кедр уже внутри), мост и река на западе. Новое: Pond (пирс озера). Тропа к воротам острова (север) строится без площадки.
    public static readonly Zone[] Placement =
    {
        Z("Center",     Vector2.zero, 16f, 16f, Color.white, pad: false),   // кострище, ровно Y = 0 (радиус = centerRadius)
        Z("Tent",       Pol(335f, 17f), 5f,  5f,  new Color(0.95f, 0.60f, 0.20f)),
        Z("Hut",        Pol(20f,  19f), 8f,  8f,  new Color(0.90f, 0.30f, 0.30f)),
        Z("Market",     Pol(78f,  17f), 8f,  7f,  new Color(0.30f, 0.70f, 1.00f)),
        Z("QuestBoard", Pol(150f, 10f), 3f,  3f,  new Color(1.00f, 0.90f, 0.20f)),
        Z("Forest",     Pol(210f, 40f), 22f, 22f, new Color(0.20f, 0.80f, 0.30f), round: true),
        Z("Bridge",     Pol(270f, 48f), 6f,  8f,  new Color(0.40f, 0.60f, 1.00f)),
        Z("Order1",     Pol(300f, 32f), 6f,  6f,  new Color(0.80f, 0.40f, 1.00f), parent: "Tent",   fork: 12f),
        Z("Order2",     Pol(47f,  34f), 6f,  6f,  new Color(0.80f, 0.40f, 1.00f), parent: "Hut",    fork: 14f),
        Z("Order3",     Pol(108f, 32f), 6f,  6f,  new Color(0.80f, 0.40f, 1.00f), parent: "Market", fork: 12f),
        Z("Pond",       new Vector2(22f, -31f), 6f, 8f, new Color(0.20f, 0.50f, 1.00f)),                  // пирс на северном берегу озера
        // только данные (площадок нет)
        Z("Gate",       Pol(0f, 60f), 6f, 8f, new Color(0.10f, 0.10f, 0.10f), pad: false),                 // запасная позиция ворот (мост на след. локацию), если FloatingIsland нет в сцене; тропа к воротам строится без площадки
        Z("Lake",       new Vector2(22f, -44f), 11f, 11f, Color.blue, pad: false),                        // центр чаши озера (= lakePos), w = lakeRadius, юго-восток
        Z("Source",     new Vector2(-62f, 60f), 8f, 8f, Color.gray, pad: false),                          // скала с водопадом, северо-запад, начало реки (riverSourceZ)
        Z("Dock",       new Vector2(-80f, 0f),  10f, 8f, new Color(0.55f, 0.35f, 0.15f), pad: false),     // смотровая и дирижабли: за мостом и рекой, у самой кромки на западе
    };

    public static Zone FindZone(string name)
    {
        foreach (var z in Placement) if (z.name == name) return z;
        return null;
    }

    public static Vector2 ZonePos(string name)
    {
        var z = FindZone(name);
        return z != null ? z.pos : Vector2.zero;
    }

    static PadDef ZoneToPad(Zone z)
    {
        float a = Mathf.Atan2(z.pos.x, z.pos.y) * Mathf.Rad2Deg;   // угол от севера по часовой, как в SolveLayout
        if (a < 0f) a += 360f;
        var p = new PadDef(z.name, a, z.pos.magnitude, z.w, z.h, z.color, z.parent, z.fork, z.round);
        p.straight = z.straight;
        return p;
    }

    // ------------------------------------------------------------------ настройки

    [Serializable]
    public class Settings
    {
        public int settingsVersion = 0;   // для миграции сохранённых настроек

        public int seed = 1337;
        public float worldScale = 0.5f;
        public Vector3 origin = Vector3.zero;
        public bool showHandles = true;
        public bool setupLighting = true;
        public bool useFog = false;
        public bool rebuildIslandInWorld = true;   // после рельефа пересобирать FloatingIsland (дырки в Terrain, обрыв, водопад): новый Terrain их теряет

        // модули
        public bool buildRiver = true;    // река: русло в Terrain + вода, камыш, кувшинки, водопадик (LocationRiver)
        public float bridgeWidth = 3.0f;
        public bool hasBridge = true;     // мост через реку (RiverBridge, 3 стадии: сломан, починен, готов)
        [Range(1, 3)] public int bridgeStage = 1;
        public bool waterBarrier = true;  // невидимая стенка над водой (на месте моста проход)
        public float barrierEdgeGap = 6f; // стенка кончается за столько u до кромки, там вода падает вниз
        public float headFallWidth = 2.2f; // ширина ручья у истока, u
        public bool buildCenter = true;
        public bool buildMeadow = true;

        // центр (LocationCenter)
        public bool centerSeats = true;
        public bool centerLamps = true;
        public bool centerGarland = true;
        public bool centerPlanters = true;
        public bool centerProps = true;
        public bool centerSign = true;
        public float centerDetail = 1f;

        // лес (площадка): рельеф и мусор в глэйде. Деревья, подзоны и камеру делает Tools → Forest → Лес
        public float forestRelief = 0.6f;
        public float forestRidgeHeight = 6f;
        public float forestClutter = 0.35f;        // 0..1.5: случайные пни, брёвна, камни и кусты в глэйде

        // поляна (LocationMeadow)
        public int meadowSpots = 8;
        public float meadowFlowers = 1f;
        public bool meadowLife = true;       // искры у кармашков (фей, кристаллов)

        // доводка (LocationPolish)
        public bool polishEnabled = true;
        public float polishGrass = 1f;
        public float polishFlowers = 1f;
        public float polishSway = 0.12f;
        public int polishButterflies = 10;
        public bool polishParticles = true;
        public bool polishFootDust = true;

        // рельеф
        public float terrainSize = 140f;
        public int heightmapRes = 513;
        public int alphaRes = 512;
        public float maxHeight = 14f;
        public float hillAmplitude = 3f;
        public float hillFrequency = 0.022f;
        public float hillClear = 3f;
        public float hillFade = 18f;
        public float ringWidth = 24f;
        public float ringMin = 8f;
        public int blurPasses = 3;

        // ---- Рельеф «Выразительный» (LocationGen.Relief.cs). Значения по умолчанию = пресет «Выразительный».
        // Единицы u (1 u = worldScale м). reliefStyle по умолчанию 1 (с v13); старый рельеф включается пресетом «Старый».
        public int reliefStyle = 1;            // 0 = «Старый» (шум Perlin, LocationGen.Terrain.cs), 1 = «Выразительный» (по умолчанию с v13)
        // чаша и кромка острова
        public bool rimFollowIsland = true;    // кромка идёт по контуру FloatingIsland (если острова нет в сцене: по краю Terrain)
        public float rimHeight = 3.2f;
        public float rimWidth = 18f;
        public float rimGateClear = 14f;       // вокруг ворот к мосту кромка снижается
        public float bowlHeight = 1.2f;

        // v13 (задача 1): ровная кромка и контур по раскладке
        public float rimFlatWidth = 2.5f;      // ровная травяная полоса у самого края, u (0 = как раньше)
        public float outlineBay = 4f;          // глубина бухт контура, u
        public float outlineCape = 2.5f;       // вынос мысов, u
        public float outlineRound = 6f;        // 2 = круг, 6 = скруглённый квадрат, 12 = почти квадрат
        public Vector2 dockPos = new Vector2(-80f, 0f);   // Dock, u
        public Vector2 dockSize = new Vector2(10f, 8f);
        // подъём от центра к краю
        // луга
        public float meadowSwell = 2.6f;       // высота пологих холмов
        public float meadowSwellScale = 0.014f;// частота (меньше = крупнее холмы)
        public float meadowSwellFlat = 0.45f;  // доля луга без холмов, 0..0.9
        // лесной массив
        public float forestTier1 = 4.5f;       // высота яруса 1 (плато у площадки леса)
        public float forestTier2 = 8.5f;       // высота яруса 2 (дальняя от лагеря часть); = Tier1 для одного яруса
        public float forestSlope = 26f;        // ширина боковых склонов массива
        public float forestTierSlope = 16f;    // длина уступа между ярусами
        public float forestGladeRelief = 0.4f; // мелкие бугры на самом массиве
        // река и озеро (параметры для задач «Река» и «Озеро»; озеро по умолчанию выключено)
        public float valleyWidth = 16f;        // ширина долины: на столько формы рельефа затухают к руслу
        public bool lakeEnabled = true;        // с v18 чаша озера включена (Pond на юге, строка Lake в таблице размещения)
        public Vector2 lakePos = ZonePos("Lake");   // из карты размещения (строка Lake)
        public float lakeRadius = 11f;         // видимая вода примерно 0.6 от радиуса чаши
        public float lakeShore = 7f;           // пологий берег вокруг чаши
        public float lakeDepth = 1.2f;
        public float lakeWaterFrac = 0.6f;     // Р4: водный радиус = lakeRadius * это; дальше пологий берег до бровки чаши
        public float LakeWaterY { get { return -0.4f * lakeDepth; } }   // Р4: уровень воды озера, u (модуль воды берёт отсюда)
        // тропы и площадки на склоне
        public float trailGradeSmooth = 28f;   // сглаживание профиля тропы, u
        public float trailMaxGrade = 0.26f;    // максимальный уклон тропы (0.26 ~ 14.5°)
        public float trailBank = 8f;           // ширина откоса от тропы к склону
        public float padSkirt = 8f;            // ширина перехода от площадки к склону
        // раскраска
        public float paintCrest = 1f;
        public float paintWet = 1f;
        public float paintSoilSlope = 0.34f;   // уклон (dh/dx), с которого появляется тёмная земля
        public float paintRockSlope = 0.62f;   // уклон, с которого появляется скала

        // центр и площадки
        public float centerRadius = 8f;
        public float minPadGap = 6f;

        // тропы
        public float trailWidth = 2.6f;
        public float trailWidthAtPad = 3.5f;
        public float trailFlatWidth = 3.0f;
        public float trailMeander = 2.5f;

        // река
        public float riverWidth = 8f;
        public float riverDepth = 1.2f;
        public float riverBank = 3f;
        public float riverCurve = 3f;
        // v9: долина
        public bool riverFullLength = true;    // русло по всей длине острова (false = старая короткая вмятина |z|<48..60)
        public float riverSlope = 5f;          // доп. ширина пологого дальнего склона (со стороны кромки), u
        public float riverMeander = 2.5f;      // амплитуда мягкого меандра, u (гасится у площадки моста)
        public float valleyCut = 0.5f;         // общее понижение земли вокруг русла, u (безопасно < 0.3*riverDepth)
        public float valleyReach = 12f;        // на каком расстоянии от берега затухает понижение, u
        public float riverSourceZ = 50f;       // где начинается ручей (север = +z), u; севернее русла нет
        public float riverOffsetX = 0f;        // сдвиг русла по X (0 = как раньше, у площадки Bridge)
        // v17 (Р3): новая река. Ось от Source к мосту и к южной кромке, ширина растёт к югу, профиль: дно / берег / ровная бровка. false = старая река
        public bool newRiver = true;
        public float riverEndWidth = 1.15f;    // множитель ширины у южной кромки (у истока 0.4)
        public float riverBridgeReach = 14f;   // прямой участок оси и постоянная ширина у моста, |z| до, u
        public float riverFloorFrac = 0.55f;   // плоское дно: доля полуширины русла
        public float riverBankRun = 4.5f;      // длина берега от воды до бровки на ближней стороне, u
        public float riverFarBankFrac = 0.7f;  // то же для дальнего берега (к кромке), доля от riverBankRun
        public float riverBrowLevel = 0f;      // высота ровной бровки, u (0 = как площадки, мост на ровных берегах)
        public float riverBrowWidth = 2f;      // ширина ровной бровки, u
        public float riverBrowBlend = 6f;      // плавный выход бровки в рельеф, u
        public float riverSouthDrift = 4f;     // смещение выхода реки за южную кромку по X (к центру), u: на западе мало места до края Terrain
        // v10: ущелье. Глубокое русло с отвесными скальными стенками, вода сильно ниже берега, у края острова порог под водопад
        public bool riverGorge = true;
        public float gorgeWall = 2.2f;         // ширина стенки со стороны площадок, u (меньше = отвеснее)
        public float gorgeFarWall = 1.7f;      // то же со стороны кромки острова
        public float gorgeRough = 0.55f;       // неровность кромки и стенок 0..1
        public bool riverHead = true;          // исток: скальный лоб с водопадиком, ручей берёт начало из скалы, исток не обойти
        public float headHalfWidth = 6.5f;     // полуширина горы у подножия, u (гора идёт от истока на север до края острова)
        public float gorgeLipLength = 18f;     // длина порога у края острова (дно поднимается к водопаду), u
        // v12: единая скала у истока (гора) + рельеф вокруг неё + русло + конец реки

        // мультяшный стиль горы и ущелья (v13)
        public float headFlatLevel = 0.5f;     // высота ровной площадки у горы, u (ровно над руслом: бровка ущелья)
        public float riverWidthVar = 0.22f;    // ширина русла плавает вдоль течения на ±этую долю 0..0.4 (у площадки z=0 гасится)

        // окружение
        public float envDensity = 1f;
        public float chunkSize = 20f;
        public float boundaryRocks = 1f;
        public bool padLanterns = true;

        // палитра
        public Color grassA = new Color(0.38f, 0.70f, 0.25f);
        public Color grassB = new Color(0.47f, 0.65f, 0.21f);
        public Color trailColor = new Color(0.80f, 0.66f, 0.45f);
        public Color padColor = new Color(0.52f, 0.76f, 0.36f);
        public Color beachColor = new Color(0.90f, 0.82f, 0.58f);
        public Color forestColor = new Color(0.17f, 0.46f, 0.17f);
        public Color waterColor = new Color(0.25f, 0.62f, 0.85f, 0.62f);   // запасной цвет, если шейдер воды не собрался
        public Color trailDarkColor = new Color(0.67f, 0.53f, 0.35f);
        public Color rockColor = new Color(0.50f, 0.50f, 0.54f);    // слои «Выразительного» рельефа
        public Color soilColor = new Color(0.42f, 0.33f, 0.21f);
        public Color crestColor = new Color(0.60f, 0.80f, 0.30f);
        public Color wetColor = new Color(0.22f, 0.55f, 0.38f);
        public Color[] envColors = DefaultEnvColors();

        public List<PadDef> pads = DefaultPads();

        public static Color[] DefaultEnvColors()
        {
            return new[]
            {
                new Color(0.58f, 0.58f, 0.60f), // rock0
                new Color(0.45f, 0.46f, 0.49f), // rock1
                new Color(0.68f, 0.67f, 0.63f), // rock2
                new Color(0.22f, 0.56f, 0.20f), // bush0
                new Color(0.34f, 0.65f, 0.24f), // bush1
                new Color(0.42f, 0.75f, 0.27f), // grass tuft
                new Color(0.30f, 0.60f, 0.22f), // flower stem
                new Color(0.97f, 0.97f, 0.92f), // flower white
                new Color(0.90f, 0.22f, 0.20f), // flower red
                new Color(0.98f, 0.82f, 0.20f), // flower yellow
                new Color(0.45f, 0.30f, 0.18f), // wood
                new Color(0.68f, 0.50f, 0.31f), // wood light
                new Color(0.13f, 0.38f, 0.16f), // forest bush
                new Color(0.18f, 0.52f, 0.24f), // fern
                new Color(0.85f, 0.25f, 0.20f), // mushroom cap
                new Color(0.93f, 0.90f, 0.80f), // mushroom stem
                new Color(0.36f, 0.37f, 0.41f), // rock3 (тёмный)
                new Color(0.36f, 0.58f, 0.24f), // moss
                new Color(0.38f, 0.60f, 0.26f), // reed
                new Color(0.76f, 0.74f, 0.68f), // pebble
                new Color(0.70f, 0.69f, 0.64f), // slab
                new Color(0.42f, 0.27f, 0.15f), // cattail
                new Color(0.66f, 0.58f, 0.50f), // rock4 (тёплый)
                new Color(0.74f, 0.72f, 0.66f), // cobble
                new Color(1.00f, 0.52f, 0.12f), // flame
                new Color(1.00f, 0.86f, 0.28f), // flame inner
                new Color(0.15f, 0.14f, 0.14f), // charcoal
                new Color(0.28f, 0.66f, 0.30f), // lily
                new Color(0.98f, 0.62f, 0.78f), // lily flower
                new Color(1.00f, 0.85f, 0.45f), // lantern
                new Color(0.96f, 0.98f, 1.00f), // foam
            };
        }

        public static List<PadDef> DefaultPads()
        {
            var l = new List<PadDef>();
            foreach (var z in Placement)
                if (z.pad) l.Add(ZoneToPad(z));
            return l;
        }

        // ------------------------------------------------------------------ пресеты рельефа

        public const int ReliefOld = 0, ReliefExpressive = 1;

        // Значения берутся из инициализаторов полей (new Settings()): то, что написано выше, и есть пресет.
        // Площадки, тропы, палитра, seed и всё остальное не трогаются.
        public void ApplyReliefPreset(int style)
        {
            var d = new Settings();
            // общие поля: у обоих пресетов одинаковые базовые значения
            maxHeight = d.maxHeight; blurPasses = d.blurPasses;
            if (style == ReliefExpressive)
            {
                reliefStyle = ReliefExpressive;
                rimFollowIsland = d.rimFollowIsland; rimHeight = d.rimHeight; rimWidth = d.rimWidth; rimGateClear = d.rimGateClear; bowlHeight = d.bowlHeight;
                meadowSwell = d.meadowSwell; meadowSwellScale = d.meadowSwellScale; meadowSwellFlat = d.meadowSwellFlat;
                forestTier1 = d.forestTier1; forestTier2 = d.forestTier2; forestSlope = d.forestSlope; forestTierSlope = d.forestTierSlope; forestGladeRelief = d.forestGladeRelief; rimFlatWidth = d.rimFlatWidth;
                valleyWidth = d.valleyWidth;
                trailGradeSmooth = d.trailGradeSmooth; trailMaxGrade = d.trailMaxGrade; trailBank = d.trailBank; padSkirt = d.padSkirt;
                paintCrest = d.paintCrest; paintWet = d.paintWet; paintSoilSlope = d.paintSoilSlope; paintRockSlope = d.paintRockSlope;
                rockColor = d.rockColor; soilColor = d.soilColor; crestColor = d.crestColor; wetColor = d.wetColor;
                // озеро и река принадлежат своим задачам: пресет их не включает и не двигает
            }
            else
            {
                reliefStyle = ReliefOld;
                hillAmplitude = d.hillAmplitude; hillFrequency = d.hillFrequency; hillClear = d.hillClear; hillFade = d.hillFade;
                ringWidth = d.ringWidth; ringMin = d.ringMin;
                forestRelief = d.forestRelief; forestRidgeHeight = d.forestRidgeHeight;
            }
        }

        // Сценичный пресет: крупные читаемые формы, более мягкая долина и выраженная кромка.
        // Не меняет координаты объектов/площадок и не включает дополнительные мосты.
        public void ApplyScenicPreset()
        {
            ApplyReliefPreset(ReliefExpressive);
            rimHeight = 3.8f;
            rimWidth = 21f;
            rimGateClear = 16f;
            bowlHeight = 0.9f;
            meadowSwell = 2.9f;
            meadowSwellScale = 0.011f;
            meadowSwellFlat = 0.52f;
            forestTier1 = 4.2f;
            forestTier2 = 7.6f;
            forestSlope = 31f;
            forestTierSlope = 19f;
            forestGladeRelief = 0.28f;
            valleyWidth = 19f;
            trailGradeSmooth = 22f;
            trailMaxGrade = 0.24f;
            trailBank = 8f;
            padSkirt = 5f;
            riverFullLength = true;
            riverSlope = 6.5f;
            riverMeander = 3.2f;
            valleyCut = 0.42f;
            valleyReach = 15f;
            riverGorge = true;
            gorgeWall = 2.0f;
            gorgeFarWall = 1.8f;
            gorgeRough = 0.62f;
            gorgeLipLength = 20f;
            // Не добавляем отдельное озеро/мост: существующие river module toggles остаются как есть.
        }

        // сколько места займёт рельеф при текущих разрешениях (для окна)
        public float HeightStepCm() { return terrainSize * worldScale / Mathf.Max(1, heightmapRes - 1) * 100f; }
        public float HeightmapMB() { return heightmapRes * (float)heightmapRes * 2f / 1048576f; }   // TerrainData хранит 16 бит на точку
        public int SplatLayers() { return reliefStyle == 1 ? 11 : 7; }
        public float SplatMB()
        {
            int tex = (SplatLayers() + 3) / 4;                                                     // RGBA на 4 слоя
            float basemap = Mathf.Clamp(alphaRes * 2, 256, 2048);
            return (alphaRes * (float)alphaRes * 4f * tex + basemap * basemap * 4f) * 1.33f / 1048576f;   // + мипы
        }

        public void Validate()
        {
            if (settingsVersion < 7)
            {
                settingsVersion = 7;
                forestClutter = 0.35f;
            }
            if (settingsVersion < 8)
            {
                // «Рельеф: Выразительный». Старые сохранения остаются на старом рельефе (reliefStyle = 0),
                // новые поля уже взяли значения по умолчанию из инициализаторов. Включается пресетом в окне.
                settingsVersion = 8;
                reliefStyle = 0;
            }
            if (settingsVersion < 9)
            {
                // v9: долина. Мост выключен; значения реки меняем только если они всё ещё старые дефолты (ручные правки не трогаем).
                settingsVersion = 9;
                hasBridge = false;
                riverFullLength = true;
                if (Mathf.Abs(riverDepth - 1.2f) < 0.01f) riverDepth = 2.2f;
                if (Mathf.Abs(riverWidth - 8f) < 0.01f) riverWidth = 9f;
                if (Mathf.Abs(riverBank - 3f) < 0.01f) riverBank = 3.5f;
                if (Mathf.Abs(riverCurve - 3f) < 0.01f) riverCurve = 3.5f;
            }
            if (settingsVersion < 10)
            {
                // v10: ущелье. Старые дефолты реки заменяем на глубокие, ручные значения не трогаем.
                settingsVersion = 10;
                riverGorge = true;
                if (riverDepth < 4f) riverDepth = 4.5f;
                if (Mathf.Abs(riverWidth - 9f) < 0.01f) riverWidth = 8f;
                if (Mathf.Abs(riverSlope - 5f) < 0.01f) riverSlope = 2f;
            }
            if (settingsVersion < 13)
            {
                // v13 (Р1): «Выразительный» рельеф по умолчанию.
                settingsVersion = 13;
                reliefStyle = ReliefExpressive;
            }
            if (settingsVersion < 14)
            {
                // v14 (Р1, исправление): раскладка и озеро берутся из карты размещения. Первая версия v13 переставила площадки
                // неверно, поэтому раскладка сбрасывается на таблицу (старые позиции + Exit и Pond).
                settingsVersion = 14;
                pads = DefaultPads();
                lakePos = ZonePos("Lake");
            }
            if (settingsVersion < 15)
            {
                // v15: тропа к Exit (разрушенный мост) прямая
                settingsVersion = 15;
                foreach (var q in pads) if (q.name == "Exit") q.straight = true;
            }
            if (settingsVersion < 16)
            {
                // v16: площадки Exit больше нет (вырезала землю у кромки): прямая тропа идёт к воротам острова без площадки
                settingsVersion = 16;
                pads.RemoveAll(q => q.name == "Exit");
            }
            if (settingsVersion < 17)
            {
                // v17 (Р3): новая река по умолчанию (поля уже взяли значения из инициализаторов), старая включается галкой
                settingsVersion = 17;
                newRiver = true;
            }
            if (settingsVersion < 18)
            {
                // v18 (Р4): озеро включено, центр и радиус из таблицы размещения (Lake: 22, -44, R = 11)
                settingsVersion = 18;
                lakeEnabled = true;
                lakePos = ZonePos("Lake");
                lakeRadius = FindZone("Lake").w;
            }
            if (settingsVersion < 19)
            {
                // v19: река и мост вернулись в генератор (вода, камыш, мост с тремя стадиями)
                settingsVersion = 19;
                buildRiver = true;
                hasBridge = true;
            }
            bridgeStage = Mathf.Clamp(bridgeStage, 1, 3);
            lakeWaterFrac = Mathf.Clamp(lakeWaterFrac, 0.3f, 0.85f);
            riverEndWidth = Mathf.Clamp(riverEndWidth, 0.6f, 2f);
            riverBridgeReach = Mathf.Clamp(riverBridgeReach, 4f, 30f);
            riverFloorFrac = Mathf.Clamp(riverFloorFrac, 0.2f, 0.85f);
            riverBankRun = Mathf.Clamp(riverBankRun, 1.5f, 10f);
            riverFarBankFrac = Mathf.Clamp(riverFarBankFrac, 0.3f, 1f);
            riverBrowWidth = Mathf.Clamp(riverBrowWidth, 0.5f, 6f);
            riverBrowBlend = Mathf.Clamp(riverBrowBlend, 1f, 14f);
            riverDepth = Mathf.Clamp(riverDepth, 0.4f, 8f);
            gorgeWall = Mathf.Clamp(gorgeWall, 0.8f, 8f);
            gorgeFarWall = Mathf.Clamp(gorgeFarWall, 0.8f, 8f);
            gorgeRough = Mathf.Clamp01(gorgeRough);
            gorgeLipLength = Mathf.Clamp(gorgeLipLength, 4f, 40f);
            if (settingsVersion < 13) settingsVersion = 13;   // v13: новые поля берутся из инициализаторов, старые сохранения не ломаются
            headFlatLevel = Mathf.Clamp(headFlatLevel, -0.2f, 3f);
            riverWidthVar = Mathf.Clamp(riverWidthVar, 0f, 0.4f);
            headHalfWidth = Mathf.Clamp(headHalfWidth, 2f, 16f);
            riverSlope = Mathf.Clamp(riverSlope, 0f, 14f);
            riverMeander = Mathf.Clamp(riverMeander, 0f, 6f);
            valleyCut = Mathf.Clamp(valleyCut, 0f, 0.3f * riverDepth);
            valleyReach = Mathf.Clamp(valleyReach, 2f, 40f);
            riverOffsetX = Mathf.Clamp(riverOffsetX, -30f, 30f);
            riverSourceZ = Mathf.Clamp(riverSourceZ, -200f, 200f);
            reliefStyle = Mathf.Clamp(reliefStyle, 0, 1);
            rimHeight = Mathf.Clamp(rimHeight, 0f, 12f);
            rimWidth = Mathf.Clamp(rimWidth, 2f, 60f);
            rimGateClear = Mathf.Clamp(rimGateClear, 0f, 40f);
            bowlHeight = Mathf.Clamp(bowlHeight, 0f, 8f);
            rimFlatWidth = Mathf.Clamp(rimFlatWidth, 0f, 6f);
            outlineBay = Mathf.Clamp(outlineBay, 0f, 10f);
            outlineCape = Mathf.Clamp(outlineCape, 0f, 8f);
            outlineRound = Mathf.Clamp(outlineRound, 2f, 12f);
            dockSize.x = Mathf.Clamp(dockSize.x, 2f, 30f);
            dockSize.y = Mathf.Clamp(dockSize.y, 2f, 30f);
            meadowSwell = Mathf.Clamp(meadowSwell, 0f, 8f);
            meadowSwellScale = Mathf.Clamp(meadowSwellScale, 0.004f, 0.06f);
            meadowSwellFlat = Mathf.Clamp(meadowSwellFlat, 0f, 0.9f);
            forestTier1 = Mathf.Clamp(forestTier1, 0f, 14f);
            forestTier2 = Mathf.Clamp(forestTier2, forestTier1, 14f);
            forestSlope = Mathf.Clamp(forestSlope, 4f, 60f);
            forestTierSlope = Mathf.Clamp(forestTierSlope, 2f, 60f);
            forestGladeRelief = Mathf.Clamp(forestGladeRelief, 0f, 3f);
            valleyWidth = Mathf.Clamp(valleyWidth, 2f, 60f);
            lakeRadius = Mathf.Clamp(lakeRadius, 2f, 40f);
            lakeShore = Mathf.Clamp(lakeShore, 1f, 30f);
            lakeDepth = Mathf.Clamp(lakeDepth, 0.2f, 4f);
            trailGradeSmooth = Mathf.Clamp(trailGradeSmooth, 4f, 80f);
            trailMaxGrade = Mathf.Clamp(trailMaxGrade, 0.05f, 0.7f);
            trailBank = Mathf.Clamp(trailBank, 1f, 30f);
            padSkirt = Mathf.Clamp(padSkirt, 1f, 30f);
            paintCrest = Mathf.Clamp(paintCrest, 0f, 1.5f);
            paintWet = Mathf.Clamp(paintWet, 0f, 1.5f);
            paintSoilSlope = Mathf.Clamp(paintSoilSlope, 0.05f, 1.5f);
            paintRockSlope = Mathf.Clamp(paintRockSlope, paintSoilSlope + 0.05f, 2f);
            forestClutter = Mathf.Clamp(forestClutter, 0f, 1.5f);
            worldScale = Mathf.Clamp(worldScale, 0.1f, 4f);
            terrainSize = Mathf.Clamp(terrainSize, 60f, 400f);
            heightmapRes = Mathf.Clamp(Mathf.ClosestPowerOfTwo(Mathf.Max(32, heightmapRes - 1)) + 1, 129, 1025);
            alphaRes = Mathf.Clamp(Mathf.ClosestPowerOfTwo(Mathf.Max(64, alphaRes)), 128, 1024);
            maxHeight = Mathf.Max(4f, maxHeight);
            ringMin = Mathf.Clamp(ringMin, 1f, maxHeight);
            blurPasses = Mathf.Clamp(blurPasses, 0, 8);
            meadowSpots = Mathf.Clamp(meadowSpots, 0, 14);
            meadowFlowers = Mathf.Clamp(meadowFlowers, 0f, 2f);
            polishGrass = Mathf.Clamp(polishGrass, 0f, 2f);
            polishFlowers = Mathf.Clamp(polishFlowers, 0f, 2f);
            polishSway = Mathf.Clamp(polishSway, 0f, 0.4f);
            polishButterflies = Mathf.Clamp(polishButterflies, 0, 30);
            chunkSize = Mathf.Clamp(chunkSize, 8f, 80f);
            envDensity = Mathf.Clamp(envDensity, 0f, 3f);
            centerDetail = Mathf.Clamp(centerDetail, 0f, 2f);
            riverWidth = Mathf.Clamp(riverWidth, 2f, 16f);
            riverBank = Mathf.Max(0.5f, riverBank);
            hillFade = Mathf.Max(1f, hillFade);
            trailFlatWidth = Mathf.Max(trailWidth, trailFlatWidth);
            if (envColors == null || envColors.Length != MatNames.Length) envColors = DefaultEnvColors();
            if (pads == null || pads.Count == 0) pads = DefaultPads();
            foreach (var p in pads)
            {
                if (p.name == null) p.name = "Pad";
                if (p.parent == null) p.parent = "";
                p.size = new Vector2(Mathf.Max(1f, p.size.x), Mathf.Max(1f, p.size.y));
            }
        }
    }
}
#endif
