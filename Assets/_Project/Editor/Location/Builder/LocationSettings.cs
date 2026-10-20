#if UNITY_EDITOR
// Assets/_Project/Editor/Location/Builder/LocationSettings.cs
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

        public PadDef() { }

        public PadDef(string n, float a, float d, float w, float h, Color c, string par = "", float fork = 12f, bool isRound = false)
        {
            name = n; angle = a; distance = d; size = new Vector2(w, h); gizmoColor = c;
            parent = par; forkDistance = fork; round = isRound;
        }
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

        // модули
        public bool buildRiver = true;
        public int bridgeStage = 1;
        public float bridgeWidth = 3.0f;
        public bool hasBridge = false;         // v9: мост через реку по умолчанию выключен (BrokenBridge не строится)
        public bool waterBarrier = true;
        public bool buildCliffs = true;
        public float cliffHeight = 1f;
        public float cliffDensity = 1f;
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
        // Единицы u (1 u = worldScale м). reliefStyle по умолчанию 0: сохранённые локации не меняются, пока не нажат пресет.
        public int reliefStyle = 0;            // 0 = «Старый» (шум Perlin, LocationGen.Terrain.cs), 1 = «Выразительный»
        // чаша и кромка острова
        public bool rimFollowIsland = true;    // кромка идёт по контуру FloatingIsland (если острова нет в сцене: по краю Terrain)
        public float rimHeight = 3.2f;
        public float rimWidth = 18f;
        public float rimGateClear = 14f;       // вокруг ворот к мосту кромка снижается
        public float bowlHeight = 1.2f;        // подъём от центра к краю
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
        public bool lakeEnabled = false;
        public Vector2 lakePos = new Vector2(28f, -46f);
        public float lakeRadius = 11f;         // видимая вода примерно 0.6 от радиуса чаши
        public float lakeShore = 7f;           // пологий берег вокруг чаши
        public float lakeDepth = 1.2f;
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
        // v10: ущелье. Глубокое русло с отвесными скальными стенками, вода сильно ниже берега, у края острова порог под водопад
        public bool riverGorge = true;
        public float gorgeWall = 2.2f;         // ширина стенки со стороны площадок, u (меньше = отвеснее)
        public float gorgeFarWall = 1.7f;      // то же со стороны кромки острова
        public float gorgeRough = 0.55f;       // неровность кромки и стенок 0..1
        public bool riverHead = true;          // исток: скальный лоб с водопадиком, ручей берёт начало из скалы, исток не обойти
        public float headHeight = 15.5f;       // высота горы у истока над уровнем земли, u (12..18 норм; без меш-скалы ограничена maxHeight)
        public float headHalfWidth = 6.5f;     // полуширина горы у подножия, u (гора идёт от истока на север до края острова)
        public bool buildHeadMountain = true;  // слоистая меш-скала поверх ядра рельефа (LocationHeadMountain); false = только рельеф
        public float headCoreFrac = 0.5f;      // высота ядра в рельефе, доля от headHeight (ядро спрятано внутри меша, держит коллизию)
        public int headStrata = 7;             // число пластов (уступов) скалы
        public float headRough = 0.6f;         // рваность граней и ширины 0..1
        public float headFallWidth = 1.6f;     // ширина струйки, u
        public float gorgeLipLength = 18f;     // длина порога у края острова (дно поднимается к водопаду), u
        public bool buildSpringRock = true;    // исток-каскад: скала из гранёных плит с гротом и ручьём по ступеням (LocationSpring); заменяет гору у истока
        public int springSteps = 3;            // число каменных ступеней каскада
        public float springHeight = 6.5f;      // высота родника над водой реки, u

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
            return new List<PadDef>
            {
                new PadDef("Tent",      335f, 17f, 5f, 5f, new Color(0.95f, 0.60f, 0.20f)),
                new PadDef("Hut",       20f,  19f, 8f, 8f, new Color(0.90f, 0.30f, 0.30f)),
                new PadDef("Market",    78f,  17f, 8f, 7f, new Color(0.30f, 0.70f, 1.00f)),
                new PadDef("QuestBoard",150f, 10f, 3f, 3f, new Color(1.00f, 0.90f, 0.20f)),
                new PadDef("Forest",    210f, 40f, 22f, 22f, new Color(0.20f, 0.80f, 0.30f), "", 12f, true),
                new PadDef("Bridge",    270f, 48f, 6f, 8f, new Color(0.40f, 0.60f, 1.00f)),
                new PadDef("Order1",    300f, 32f, 6f, 6f, new Color(0.80f, 0.40f, 1.00f), "Tent", 12f),
                new PadDef("Order2",    47f,  34f, 6f, 6f, new Color(0.80f, 0.40f, 1.00f), "Hut", 14f),
                new PadDef("Order3",    108f, 32f, 6f, 6f, new Color(0.80f, 0.40f, 1.00f), "Market", 12f),
            };
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
                forestTier1 = d.forestTier1; forestTier2 = d.forestTier2; forestSlope = d.forestSlope; forestTierSlope = d.forestTierSlope; forestGladeRelief = d.forestGladeRelief;
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
            riverDepth = Mathf.Clamp(riverDepth, 0.4f, 8f);
            gorgeWall = Mathf.Clamp(gorgeWall, 0.8f, 8f);
            gorgeFarWall = Mathf.Clamp(gorgeFarWall, 0.8f, 8f);
            gorgeRough = Mathf.Clamp01(gorgeRough);
            gorgeLipLength = Mathf.Clamp(gorgeLipLength, 4f, 40f);
            if (settingsVersion < 11)
            {
                // v11: гора у истока. Старые дефолты (5.5 / 4.5) заменяем, ручные значения не трогаем.
                settingsVersion = 11;
                if (headHeight < 12f) headHeight = 15.5f;
                if (Mathf.Abs(headHalfWidth - 4.5f) < 0.01f) headHalfWidth = 6.5f;
            }
            headHeight = Mathf.Clamp(headHeight, 2f, 30f);
            headHalfWidth = Mathf.Clamp(headHalfWidth, 2f, 16f);
            headCoreFrac = Mathf.Clamp(headCoreFrac, 0.25f, 0.8f);
            headStrata = Mathf.Clamp(headStrata, 3, 10);
            headRough = Mathf.Clamp01(headRough);
            headFallWidth = Mathf.Clamp(headFallWidth, 0.6f, 5f);
            springSteps = Mathf.Clamp(springSteps, 2, 5);
            springHeight = Mathf.Clamp(springHeight, 3f, 12f);
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
            cliffDensity = Mathf.Clamp(cliffDensity, 0f, 2f);
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
