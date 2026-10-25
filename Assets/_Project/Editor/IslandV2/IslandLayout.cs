// Assets/_Project/Editor/IslandV2/IslandLayout.cs
// Остров v2, этап 1 (блокаут): ВСЕ числа раскладки лежат в этом ассете (Assets/_Project/IslandV2/IslandLayout.asset), в коде чисел нет.
// Меню: Tools/BuilderSim/Локация/Остров v2/1. Раскладка: создать или выбрать (приоритет 120).
//
// Части этапа 1:
//   IslandLayout.cs          (этот файл: ассет, все поля, значения по умолчанию)
//   IslandField.cs           (чистая математика: контур, высота, вода, тропы, площадки; без Unity-объектов сцены)
//   IslandBlockout.cs        (меню «Собрать блокаут», корень IslandV2, маркеры, здания-коробки, лес-заглушки)
//   IslandBlockout.Ground.cs (невидимый Terrain-коллайдер и видимые меш-чанки с цветом вершин)
//   IslandCheck.cs           (проверка контракта и проходимости, замер Batches/Tris в Play Mode)
//
// Система координат: x вправо, z вперёд (в сторону «севера» карты), y вверх. Костёр в (0,0,0), Y=0 на земле у костра.
// Углы yaw в градусах вокруг оси Y: 0 = смотрит в +z, 90 = смотрит в +x.
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace IslandV2
{
    public class IslandLayout : ScriptableObject
    {
        public const string AssetFolder = "Assets/_Project/IslandV2";
        public const string AssetPath = AssetFolder + "/IslandLayout.asset";

        // ================================================================ типы данных
        public enum AnchorKind
        {
            [Tooltip("Здание-коробка с коллайдером (BLK_*). Настоящее здание потом ставит билдер на метку PAD_*")] Building = 0,
            [Tooltip("Только метка в Markers, без коробки")] MarkerOnly = 1,
            [Tooltip("Метка на краю острова: позиция считается по углу от центра до контура (мост, отложен)")] EdgePoint = 2
        }

        [Serializable]
        public class Anchor
        {
            [Tooltip("Имя метки в Markers. Это контракт с остальным проектом (PAD_Hut, PAD_Market, CAMPFIRE_Point и т.д.), не переименовывать без правки кода")]
            public string name = "PAD_New";
            public AnchorKind kind = AnchorKind.Building;
            [Tooltip("Позиция на плане, метры (x, z). Для EdgePoint не используется: берётся угол ниже")]
            public Vector2 pos;
            [Tooltip("Поворот вокруг Y, градусы. 0 = лицом к +z. Здания «лицом» к костру")]
            public float yawDeg;
            [Tooltip("Размер коробки на плане: ширина (x) и глубина (z), метры. Это и зона, куда нельзя ставить лес и деревья")]
            public Vector2 footprint = new Vector2(4f, 4f);
            [Tooltip("Высота коробки-заглушки, метры")]
            public float height = 3f;
            [Tooltip("Радиус площадки, которая выравнивается до Y=0 (ровное место под настоящее здание), метры. Дальше идёт плавный переход в холмы")]
            public float flatRadius = 6f;
            [Tooltip("Сколько метров перед «лицом» здания проверка проходимости ищет подход (от края коробки)")]
            public float approachDist = 2.5f;
            [Tooltip("Коробка блокирует проход (коллайдер). Костёр: низкий круг, обходится")]
            public bool blocksWalk = true;
            [Tooltip("Только для EdgePoint: угол от центра острова, градусы (0 = +x, 90 = +z)")]
            public float edgeAngleDeg;
            [Tooltip("Только для EdgePoint: на сколько метров метка отодвинута от контура внутрь острова")]
            public float edgeInset = 2.5f;
            [Tooltip("Серый оттенок коробки (0 = чёрный, 1 = белый)")]
            [Range(0f, 1f)] public float gray = 0.62f;
        }

        [Serializable]
        public class PathLine
        {
            [Tooltip("Название тропы (только для Console и проверки)")]
            public string name = "Path";
            [Tooltip("Опорные точки тропы, метры (x, z). Тропа проходит плавной кривой через них")]
            public List<Vector2> points = new List<Vector2>();
            [Tooltip("Ширина тропы, метры")]
            public float width = 3f;
            [Tooltip("Имя метки, в которую упирается конец тропы (например DEFERRED_Bridge). Пусто = конец в последней точке. Нужно, чтобы тропа не расходилась с меткой, когда контур острова меняется")]
            public string endAnchor = "";
        }

        [Serializable]
        public class Crossing
        {
            [Tooltip("Точка на реке, где брод (метры, x z). Должна лежать на линии реки")]
            public Vector2 pos;
            [Tooltip("Половина длины брода вдоль реки, метры")]
            public float halfLength = 4f;
        }

        [Serializable]
        public class Meadow
        {
            [Tooltip("Центр поляны, метры (x, z)")]
            public Vector2 pos;
            [Tooltip("Радиус поляны без леса, метры")]
            public float radius = 8f;
        }

        [Serializable]
        public class BudgetProfile
        {
            [Tooltip("Сколько Batches блокаут может добавить к сцене (замер «с блокаутом» минус «без»). Лимит проверки")]
            public int maxExtraBatches = 20;
            [Tooltip("Сколько Tris блокаут может добавить. Лимит проверки")]
            public int maxExtraTris = 20000;
            [Tooltip("Лимит числа видимых меш-чанков земли (рендереров) в сцене")]
            public int maxGroundChunks = 14;
        }

        // ================================================================ контур острова
        [Header("Контур острова")]
        [Tooltip("Полуось контура по x, метры (до подгонки площади)")]
        public float radiusX = 82f;
        [Tooltip("Полуось контура по z, метры (до подгонки площади)")]
        public float radiusZ = 63f;
        [Tooltip("Центр контура относительно костра, метры (x, z). Костёр не обязан стоять строго в центре")]
        public Vector2 centerOffset = new Vector2(0f, 0f);
        [Tooltip("Целевая площадь острова, м². После шума контур масштабируется до неё (проверка допускает отклонение ниже)")]
        public float targetArea = 15400f;
        [Tooltip("Допуск проверки по площади, доля (0.03 = ±3%)")]
        [Range(0.005f, 0.2f)] public float areaTolerance = 0.03f;
        [Tooltip("Сколько точек в контуре. Больше = глаже край")]
        [Range(32, 256)] public int contourPoints = 120;
        [Tooltip("Сила неровности края: крупные бухты и мысы, доля радиуса (0.1 = ±10%)")]
        [Range(0f, 0.3f)] public float edgeNoiseBig = 0.09f;
        [Tooltip("Сила мелкой неровности края, доля радиуса")]
        [Range(0f, 0.15f)] public float edgeNoiseSmall = 0.025f;
        [Tooltip("Частота крупного шума края (число «волн» вокруг острова примерно)")]
        public float edgeFreqBig = 1.6f;
        [Tooltip("Частота мелкого шума края")]
        public float edgeFreqSmall = 5.5f;
        [Tooltip("Зерно случайности контура, рельефа и леса. Одно число даёт один и тот же остров")]
        public int seed = 7;

        // ================================================================ рельеф
        [Header("Рельеф (мягкие холмы)")]
        [Tooltip("Высота холмов, метры (размах в обе стороны). Блокаут держит холмы мягкими, чтобы всё проходилось")]
        public float hillAmp = 0.9f;
        [Tooltip("Размер холмов, метры (длина волны шума)")]
        public float hillScale = 38f;
        [Tooltip("Насколько тропа сглаживает холмы вдоль себя (0 = не трогает, 1 = ровная)")]
        [Range(0f, 1f)] public float pathFlatten = 0.6f;
        [Tooltip("Ширина перехода от ровной площадки к холмам, метры")]
        public float flatBlend = 5f;
        [Tooltip("Максимальный уклон, по которому можно ходить, градусы. Игрок ходит через Rigidbody без лимита, это дизайнерское число для проверки")]
        [Range(10f, 60f)] public float maxWalkSlopeDeg = 32f;
        [Tooltip("Радиус игрока для проверки проходимости, метры (коробки и стволы раздуваются на него)")]
        public float playerRadius = 0.45f;

        // ================================================================ река, пруд
        [Header("Река (слева) и пруд (справа внизу)")]
        [Tooltip("Опорные точки реки сверху вниз, метры (x, z). Река течёт через весь остров и уходит за край (водопад потом, этап 6)")]
        public List<Vector2> riverPoints = new List<Vector2>();
        [Tooltip("Ширина воды реки, метры")]
        public float riverWidth = 10f;
        [Tooltip("Ширина пологого берега по каждой стороне, метры")]
        public float riverBank = 2.5f;
        [Tooltip("УСТАРЕЛО (E): больше не используется, глубину задают riverWaterDepth, riverSurfaceBelow и riverDrop ниже")]
        public float riverDepth = 0.55f;
        [Header("Русло и сток реки (E)")]
        [Tooltip("Глубина воды в середине русла, метры. Глубже swimDepth (1 м) = герой плывёт (WaterSwim)")]
        [Range(0.4f, 5f)] public float riverWaterDepth = 3.0f;
        [Tooltip("На сколько метров поверхность воды ниже уровня земли у истока (там, где река входит на остров), метры")]
        [Range(0.1f, 3f)] public float riverSurfaceBelow = 1.3f;
        [Tooltip("На сколько метров вода опускается от истока до устья: так река течёт вниз. Чем больше, тем глубже русло у края и шире откосы")]
        [Range(0f, 4f)] public float riverDrop = 1.6f;
        [Tooltip("Глубина воды на самой кромке (порог водопада), метры: мелко, чтобы вода тонким слоем переливалась через край")]
        [Range(0.1f, 1f)] public float riverLip = 0.45f;
        [Tooltip("Откос берега (стены русла): сколько метров по горизонтали на метр высоты стены над водой. 0,9 = крутая стена около 60°, 2,8 = пологий берег около 28°. Вне брода по стене не выйти: переправа только по броду и мосту")]
        [Range(0.6f, 5f)] public float riverBankRun = 0.9f;
        [Tooltip("То же у брода: там откос должен быть проходимым. Не меньше 2,5 (около 30°)")]
        [Range(1.5f, 5f)] public float riverFordRun = 2.8f;
        [Tooltip("Мельче сетку земли у реки: клетки в полосе вдоль русла делятся на 2x2, чтобы стены русла не срезались сеткой 2 м. Добавляет около 4-5k tris")]
        public bool riverMeshRefine = true;
        [Tooltip("Ширина полосы мелкой сетки по каждую сторону воды, метры")]
        public float riverRefineMargin = 5.5f;
        [Header("Броды и пруд")]
        [Tooltip("Броды: где реку можно перейти. Русло там почти заподлицо с землёй")]
        public List<Crossing> crossings = new List<Crossing>();
        [Tooltip("Остаток русла на броде (доля глубины, 0 = ровно вровень с землёй)")]
        [Range(0f, 1f)] public float fordDepthShare = 0.12f;
        [Tooltip("Центр пруда, метры (x, z)")]
        public Vector2 pondPos = new Vector2(41f, -35f);
        [Tooltip("Радиусы пруда по x и z, метры")]
        public Vector2 pondRadius = new Vector2(11f, 8f);
        [Tooltip("Глубина пруда, метры")]
        public float pondDepth = 0.8f;
        [Tooltip("Ширина берега пруда, метры")]
        public float pondBank = 2.5f;
        [Tooltip("Правило проверки: вода (река вне брода, пруд) считается непроходимой. Выключить = можно ходить вброд где угодно")]
        public bool waterBlocksWalk = true;

        // ================================================================ здания, тропы, поляны
        [Header("Метки, здания-коробки, тропы, поляны")]
        public List<Anchor> anchors = new List<Anchor>();
        public List<PathLine> paths = new List<PathLine>();
        [Tooltip("Поляны без леса (кроме площадок зданий и троп, они очищаются сами)")]
        public List<Meadow> meadows = new List<Meadow>();

        // ================================================================ лес-заглушка
        [Header("Лес-заглушки (серые конусы, настоящие деревья делает этап 5)")]
        [Tooltip("Шаг сетки деревьев, метры. Больше = реже лес и меньше Tris")]
        public float treeSpacing = 5.2f;
        [Tooltip("Насколько дерево сдвигается от узла сетки, доля шага")]
        [Range(0f, 0.5f)] public float treeJitter = 0.35f;
        [Tooltip("Радиус ствола-коллайдера, метры")]
        public float trunkRadius = 0.5f;
        [Tooltip("Высота заглушки дерева, метры")]
        public float treeHeight = 5.5f;
        [Tooltip("Радиус кроны-конуса, метры (только вид)")]
        public float crownRadius = 1.6f;
        [Tooltip("Расстояние от оси тропы, ближе которого деревьев нет (с запасом на ширину тропы), метры")]
        public float pathClear = 3.4f;
        [Tooltip("Расстояние от коробки здания, ближе которого деревьев нет, метры")]
        public float buildingClear = 3.5f;
        [Tooltip("Расстояние от края острова, ближе которого деревьев нет, метры")]
        public float edgeClear = 2.0f;
        [Tooltip("Расстояние от воды, ближе которого деревьев нет, метры")]
        public float waterClear = 2.0f;
        [Tooltip("Порог шума плотности леса (шум 0..1, в среднем около 0.5): больше = лес реже и больше полян, 0 = лес везде")]
        [Range(0f, 1f)] public float forestThreshold = 0.42f;
        [Tooltip("Размер пятен леса, метры")]
        public float forestNoiseScale = 26f;
        [Tooltip("Серый оттенок деревьев")]
        [Range(0f, 1f)] public float treeGray = 0.42f;

        // ================================================================ меш и Terrain
        [Header("Меш земли и Terrain-коллайдер")]
        [Tooltip("Шаг сетки меша земли, метры. Меньше = точнее край и холмы, больше Tris")]
        public float meshCell = 2f;
        [Tooltip("Сколько клеток в стороне одного чанка земли. Чанк = один рендерер. Больше = меньше draw calls")]
        [Range(8, 64)] public int chunkCells = 28;
        [Tooltip("Разрешение карты высот невидимого Terrain (степень двойки плюс 1: 129, 257, 513)")]
        public int terrainResolution = 257;
        [Tooltip("Высота диапазона Terrain, метры")]
        public float terrainHeightRange = 16f;
        [Tooltip("Нижняя граница диапазона Terrain по Y, метры (русла и пруд уходят ниже нуля)")]
        public float terrainBaseY = -6f;
        [Tooltip("Запас Terrain вокруг контура, метры")]
        public float terrainMargin = 4f;
        [Tooltip("Если дыры Terrain вышли наоборот (коллайдер снаружи острова, а не внутри), включи. По документации Unity false = дыра")]
        public bool terrainHolesInverted = false;

        // ================================================================ цвета земли (этап 2, задача F)
        // Все цвета в sRGB (как в инспекторе). Вершинам шейдер отдаёт их в линейном виде (см. IslandBlockout.Ground.GroundColor).
        [Header("Трава: три зелёных пятнами (F)")]
        [Tooltip("Светлая лаймовая трава (солнечные пятна)")]
        public Color grassLight = new Color(0.66f, 0.83f, 0.27f, 1f);
        [Tooltip("Основная трава")]
        public Color grassMid = new Color(0.46f, 0.74f, 0.27f, 1f);
        [Tooltip("Тёмная, чуть бирюзовая трава (тенистые пятна)")]
        public Color grassDeep = new Color(0.30f, 0.60f, 0.28f, 1f);
        [Tooltip("Размер пятен травы, метры (длина волны шума)")]
        public float grassPatchScale = 20f;
        [Tooltip("Контраст пятен: больше = светлых и тёмных пятен больше, средних меньше")]
        [Range(1f, 3f)] public float grassPatchContrast = 1.7f;
        [Tooltip("Порог шума: ниже этого значения трава тёмная")]
        [Range(0.2f, 0.5f)] public float grassDeepBelow = 0.40f;
        [Tooltip("Порог шума: выше этого значения трава светлая")]
        [Range(0.5f, 0.8f)] public float grassLightAbove = 0.60f;
        [Tooltip("Мягкость границы пятен (0 = резкая, 0.1 = размытая)")]
        [Range(0.005f, 0.15f)] public float grassPatchSoft = 0.045f;
        [Tooltip("Мелкая рябь яркости земли, чтобы читался рельеф")]
        [Range(0f, 0.2f)] public float groundVariation = 0.05f;

        [Header("Тёмный лес (F)")]
        [Tooltip("Цвет земли под густым лесом")]
        public Color forestFloor = new Color(0.20f, 0.44f, 0.27f, 1f);
        [Tooltip("Насколько сильно лесное пятно перекрашивает траву (0..1)")]
        [Range(0f, 1f)] public float forestPaintStrength = 0.85f;
        [Tooltip("Размытость границы леса по шуму (доля шума вокруг порога forestThreshold)")]
        [Range(0.01f, 0.2f)] public float forestPaintSoft = 0.07f;

        [Header("Песочная тропа (F)")]
        public Color pathSand = new Color(0.93f, 0.82f, 0.52f, 1f);
        [Tooltip("Кайма тропы: переходная жёлто-зелёная полоса по краю")]
        public Color pathRim = new Color(0.76f, 0.80f, 0.36f, 1f);
        [Tooltip("Ширина каймы снаружи тропы, метры")]
        public float pathRimWidth = 1.0f;
        [Tooltip("Насколько кайма красит траву (0..1)")]
        [Range(0f, 1f)] public float pathRimStrength = 0.85f;
        [Tooltip("Насколько тропа расширяется на развилках, метры (цветом; геометрия и холмы не меняются)")]
        public float pathJunctionWiden = 1.0f;
        [Tooltip("На каком расстоянии друг от друга должны быть края двух троп, чтобы считалось развилкой, метры")]
        public float pathJunctionReach = 3.5f;

        [Header("Площадка у костра (F)")]
        [Tooltip("Радиус круглой каменной площадки вокруг CAMPFIRE_Point, метры")]
        public float plazaRadius = 5.2f;
        public Color plazaStone = new Color(0.88f, 0.82f, 0.66f, 1f);
        [Tooltip("Тёмный ободок по краю площадки")]
        public Color plazaRim = new Color(0.70f, 0.63f, 0.48f, 1f);
        [Tooltip("Ширина ободка внутри площадки, метры")]
        public float plazaRimWidth = 0.55f;

        [Header("Площадки зданий (F)")]
        [Tooltip("Утоптанная земля вокруг здания")]
        public Color padDirt = new Color(0.80f, 0.70f, 0.45f, 1f);
        [Range(0f, 1f)] public float padStrength = 0.55f;

        [Header("Текстура цвета земли (G)")]
        [Tooltip("Размер пикселя текстуры цвета земли, метры. 0.5 = края троп и каймы чёткие; меньше = чётче, но тяжелее (ограничено groundTexMax)")]
        [Range(0.2f, 2f)] public float groundTexelSize = 0.5f;
        [Tooltip("Максимум пикселей по большей стороне (не больше 2048)")]
        [Range(256, 2048)] public int groundTexMax = 1024;
        [Tooltip("Запас текстуры вокруг контура острова, метры")]
        public float groundTexMargin = 4f;

        [Header("Вода, берег, кромка (F)")]
        [Tooltip("Цвет дна под водой (настоящую воду делает этап 6)")]
        public Color waterColor = new Color(0.30f, 0.62f, 0.78f, 1f);
        [Tooltip("Влажная тёмная земля у воды")]
        public Color shoreWet = new Color(0.25f, 0.45f, 0.26f, 1f);
        [Tooltip("Ширина влажного берега, метры")]
        public float shoreWidth = 2.4f;
        [Tooltip("Песок на броде")]
        public Color fordSand = new Color(0.80f, 0.78f, 0.60f, 1f);
        [Tooltip("Ширина затемнения у края острова, метры")]
        public float edgeDarkWidth = 4.5f;
        [Tooltip("На сколько темнеет земля у самой кромки (0 = нет, 0.3 = заметно)")]
        [Range(0f, 0.5f)] public float edgeDarken = 0.18f;

        // ---------------------------------------------------------------- обрыв и подбрюшье (этап 4)
        [Header("Вырез под реку в обрыве (D)")]
        [Tooltip("На сколько метров русло глубже у самого края острова (заготовка под водопад). 0 = выреза нет")]
        [Range(0f, 2f)] public float notchDrop = 0.6f;
        [Tooltip("На каком расстоянии от края русло углубляется, метры")]
        public float notchLength = 7f;
        [Tooltip("Ширина перехода от мокрой скалы к обычной по краям выреза, метры (сверх ширины реки)")]
        public float notchBlend = 3f;
        [Tooltip("Цвет мокрой скалы в вырезе (sRGB)")]
        public Color cliffWet = new Color(0.42f, 0.50f, 0.74f, 1f);

        // ---------------------------------------------------------------- вода реки и водопад (E)
        [Header("Вода реки и водопад (E)")]
        [Tooltip("Цвет скальных стен русла (sRGB)")]
        public Color riverWallColor = new Color(0.64f, 0.58f, 0.74f, 1f);
        [Tooltip("Цвет мокрой скалы у самой воды (sRGB)")]
        public Color riverWallWet = new Color(0.38f, 0.42f, 0.62f, 1f);
        [Tooltip("Строить поверхность воды, водопад и объём воды для плавания (IslandRiverWater)")]
        public bool buildRiverWater = true;
        [Tooltip("Цвет воды на мелководье у берега (sRGB)")]
        public Color riverShallow = new Color(0.40f, 0.78f, 0.84f, 1f);
        [Tooltip("Цвет воды на глубине (sRGB)")]
        public Color riverDeep = new Color(0.07f, 0.32f, 0.60f, 1f);
        [Tooltip("Цвет пены у берега, на пороге и на струях (sRGB)")]
        public Color riverFoam = new Color(0.94f, 0.99f, 1f, 1f);
        [Tooltip("Скорость течения по поверхности, условных единиц в секунду")]
        [Range(0f, 4f)] public float riverFlowSpeed = 1.1f;
        [Tooltip("Ширина водопада в долях ширины реки")]
        [Range(0.4f, 1.2f)] public float fallWidthShare = 0.85f;
        [Tooltip("Насколько водопад отстоит от скалы, метры (нужен запас на неровность граней cliffRough)")]
        [Range(0.3f, 2f)] public float fallOffset = 0.8f;
        [Tooltip("На сколько метров водопад уходит ниже низа обрыва, в облака")]
        [Range(0f, 20f)] public float fallExtra = 6f;

        [Header("Обрыв и подбрюшье (этап 4)")]
        [Tooltip("Строить обрыв и подбрюшье при сборке блокаута")]
        public bool buildCliff = true;
        [Tooltip("Ширина одной колонны скалы по контуру, метры. Меньше = мельче грани и больше треугольников (около 2,5k на 3 м)")]
        public float cliffColumnWidth = 3f;
        [Tooltip("Высота почти вертикальной стенки, метры")]
        [Range(3f, 16f)] public float cliffWall = 8f;
        [Tooltip("На сколько метров ниже стенки остров сужается до острия")]
        [Range(4f, 30f)] public float cliffTaper = 14f;
        [Tooltip("Неровность граней, метры")]
        [Range(0f, 3f)] public float cliffRough = 1.2f;
        [Tooltip("Насколько травяной валик нависает над скалой, метры")]
        [Range(0f, 1.5f)] public float lipOverhang = 0.45f;
        [Tooltip("Глубина сталактитов в подбрюшье, метры")]
        [Range(0f, 6f)] public float cliffSpikes = 3f;
        [Tooltip("Цвета обрыва (sRGB). Сверху вниз: трава кромки, тёмная трава, грунт, скала светлая, средняя, тёмная. Лавандовые, как на референсе")]
        public Color lipTop = new Color(0.56f, 0.78f, 0.24f, 1f);
        public Color lipDark = new Color(0.36f, 0.62f, 0.22f, 1f);
        public Color cliffSoil = new Color(0.60f, 0.48f, 0.46f, 1f);
        public Color rockTop = new Color(0.76f, 0.68f, 0.80f, 1f);
        public Color rockMid = new Color(0.62f, 0.57f, 0.74f, 1f);
        public Color rockDark = new Color(0.43f, 0.39f, 0.60f, 1f);

        // ================================================================ бюджет
        [Header("Бюджет блокаута: три профиля (Low = Mobile Low, Medium = Mobile High, High = ПК)")]
        public BudgetProfile budgetMobileLow = new BudgetProfile { maxExtraBatches = 12, maxExtraTris = 14000, maxGroundChunks = 14 };
        public BudgetProfile budgetMobileHigh = new BudgetProfile { maxExtraBatches = 16, maxExtraTris = 20000, maxGroundChunks = 14 };
        public BudgetProfile budgetPc = new BudgetProfile { maxExtraBatches = 24, maxExtraTris = 30000, maxGroundChunks = 20 };
        [Tooltip("Доля проходимой земли, до которой должен дотянуться игрок от точки появления (без воды). Меньше = в лесу есть замкнутые карманы")]
        [Range(0.5f, 1f)] public float minReachableShare = 0.93f;

        public BudgetProfile GetBudget(LightQualityLevel q)
        {
            if (q == LightQualityLevel.Low) return budgetMobileLow;
            if (q == LightQualityLevel.Medium) return budgetMobileHigh;
            return budgetPc;
        }

        // Контракт с игрой: CAMPFIRE_Point всегда в (0,0) и обычным зданием-площадкой, SPAWN_Player обычной меткой рядом (не на краю).
        // Если в ассете тип метки или позиция съехали (например, EdgePoint), возвращает их на место. Пишет в note, что поправил.
        public bool EnforceContract(out string note)
        {
            note = "";
            var camp = FindAnchor("CAMPFIRE_Point");
            if (camp != null && (camp.kind != AnchorKind.Building || camp.pos.sqrMagnitude > 1e-6f))
            {
                note += "CAMPFIRE_Point: был тип " + camp.kind + ", pos " + camp.pos + "; поставлен Building (0,0). ";
                camp.kind = AnchorKind.Building; camp.pos = Vector2.zero;
            }
            var spawn = FindAnchor("SPAWN_Player");
            if (spawn != null && (spawn.kind == AnchorKind.EdgePoint || Vector2.Distance(spawn.pos, Vector2.zero) > 12f))
            {
                note += "SPAWN_Player: был тип " + spawn.kind + ", pos " + spawn.pos + "; поставлен MarkerOnly (0, -4.2). ";
                spawn.kind = AnchorKind.MarkerOnly; spawn.pos = new Vector2(0f, -4.2f);
            }
            return note.Length > 0;
        }

        public Anchor FindAnchor(string anchorName)
        {
            for (int i = 0; i < anchors.Count; i++) if (anchors[i].name == anchorName) return anchors[i];
            return null;
        }

        // ================================================================ значения по умолчанию
        static float YawToward(Vector2 from, Vector2 to)
        {
            Vector2 d = to - from;
            return Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
        }

        static Anchor A(string n, AnchorKind k, Vector2 p, Vector2 fp, float h, float flat, float gray)
        {
            var a = new Anchor { name = n, kind = k, pos = p, footprint = fp, height = h, flatRadius = flat, gray = gray };
            a.yawDeg = YawToward(p, Vector2.zero);   // лицом к костру
            return a;
        }

        static PathLine P(string n, float w, params float[] xz)
        {
            return PE(n, w, "", xz);
        }

        static PathLine PE(string n, float w, string endAnchor, params float[] xz)
        {
            var pl = new PathLine { name = n, width = w, endAnchor = endAnchor };
            for (int i = 0; i + 1 < xz.Length; i += 2) pl.points.Add(new Vector2(xz[i], xz[i + 1]));
            return pl;
        }

        public void ResetToDefaults()
        {
            anchors.Clear(); paths.Clear(); meadows.Clear(); crossings.Clear(); riverPoints.Clear();

            // метки и здания. Имена PAD_* и CAMPFIRE_Point ждут HudMinimap, IntroFlow, билдеры зданий
            var camp = A("CAMPFIRE_Point", AnchorKind.Building, Vector2.zero, new Vector2(2.6f, 2.6f), 0.5f, 14f, 0.50f);
            camp.blocksWalk = true; camp.approachDist = 1.5f; camp.yawDeg = 0f;
            anchors.Add(camp);
            var spawn = A("SPAWN_Player", AnchorKind.MarkerOnly, new Vector2(0f, -4.2f), new Vector2(1f, 1f), 0f, 3f, 0.5f);
            spawn.yawDeg = 0f; spawn.blocksWalk = false;
            anchors.Add(spawn);

            anchors.Add(A("PAD_Hut", AnchorKind.Building, new Vector2(-24f, 30f), new Vector2(9f, 7f), 5.5f, 9f, 0.58f));
            anchors.Add(A("PAD_Market", AnchorKind.Building, new Vector2(40f, 13f), new Vector2(9f, 7f), 5f, 9f, 0.66f));
            var qb = A("PAD_QuestBoard", AnchorKind.Building, new Vector2(19f, 9f), new Vector2(2.6f, 0.9f), 3f, 4f, 0.72f);
            qb.approachDist = 1.8f;
            anchors.Add(qb);
            anchors.Add(A("PAD_Tent", AnchorKind.Building, new Vector2(-22f, -28f), new Vector2(5f, 4f), 3f, 7f, 0.55f));

            var forest = A("PAD_Forest", AnchorKind.MarkerOnly, new Vector2(6f, 48f), new Vector2(1f, 1f), 0f, 8f, 0.5f);
            forest.blocksWalk = false; forest.approachDist = 0f;
            anchors.Add(forest);
            // вход в лесную зону (ForestZoneLayout берёт его из маркера ForestGate), смотрит на лагерь
            var gate = A("ForestGate", AnchorKind.MarkerOnly, new Vector2(4f, 34f), new Vector2(1f, 1f), 0f, 5f, 0.5f);
            gate.blocksWalk = false; gate.approachDist = 0f;
            anchors.Add(gate);
            // мост на следующую локацию отложен: только метка у края и подход к ней
            var br = A("DEFERRED_Bridge", AnchorKind.EdgePoint, Vector2.zero, new Vector2(1f, 1f), 0f, 6f, 0.5f);
            br.edgeAngleDeg = 8f; br.edgeInset = 2.5f; br.blocksWalk = false; br.approachDist = 0f;
            anchors.Add(br);

            // тропы (от костра к каждой точке; ветки сходятся в перекрёстках)
            paths.Add(P("Хижина", 3.2f, 0f, 0f, -6f, 8f, -14f, 18f, -20f, 25f, -24f, 28f));
            paths.Add(P("Рынок", 3.4f, 0f, 0f, 12f, 3f, 26f, 8f, 35f, 11f));
            paths.Add(P("Доска", 2.6f, 13f, 3f, 17f, 6f, 19f, 8f));
            paths.Add(P("Палатка", 3.0f, 0f, 0f, -2f, -8f, -10f, -18f, -18f, -25f, -22f, -27f));
            paths.Add(P("Пруд", 3.0f, 0f, -6f, 8f, -12f, 20f, -24f, 31f, -31f));
            paths.Add(PE("Лес", 3.4f, "PAD_Forest", 0f, 0f, 2f, 14f, 3f, 30f, 4f, 38f));
            paths.Add(PE("Мост", 3.2f, "DEFERRED_Bridge", 35f, 11f, 50f, 9f, 62f, 9f));
            paths.Add(P("К броду", 3.0f, -24f, 28f, -34f, 26f, -44f, 22f, -52f, 20f));
            paths.Add(P("За рекой", 2.8f, -52f, 20f, -58f, 16f, -64f, 9f));

            // река слева сверху вниз, один брод на тропе от хижины
            float[] rv = { -50f, 74f, -45f, 52f, -51f, 32f, -52f, 20f, -48f, 2f, -43f, -22f, -50f, -44f, -46f, -68f };
            for (int i = 0; i + 1 < rv.Length; i += 2) riverPoints.Add(new Vector2(rv[i], rv[i + 1]));
            crossings.Add(new Crossing { pos = new Vector2(-52f, 20f), halfLength = 4.5f });

            meadows.Add(new Meadow { pos = new Vector2(-64f, 8f), radius = 9f });
            meadows.Add(new Meadow { pos = new Vector2(-30f, -8f), radius = 7f });
            meadows.Add(new Meadow { pos = new Vector2(24f, -6f), radius = 6f });
        }

        void Reset() { ResetToDefaults(); }

        // ================================================================ меню 120
        [MenuItem("Tools/BuilderSim/Локация/Остров v2/1. Раскладка: создать или выбрать", false, 120)]
        static void MenuCreateOrSelect()
        {
            var l = GetOrCreate();
            Selection.activeObject = l;
            EditorGUIUtility.PingObject(l);
            Debug.Log("IslandV2: раскладка " + AssetPath + ". Правь числа в инспекторе, потом пункт «2. Собрать блокаут».");
        }

        public static IslandLayout GetOrCreate()
        {
            var l = AssetDatabase.LoadAssetAtPath<IslandLayout>(AssetPath);
            if (l != null) return l;
            EnsureFolder(AssetFolder);
            l = CreateInstance<IslandLayout>();
            l.ResetToDefaults();
            AssetDatabase.CreateAsset(l, AssetPath);
            AssetDatabase.SaveAssets();
            return l;
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }

    [CustomEditor(typeof(IslandLayout))]
    public class IslandLayoutEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            if (GUILayout.Button("Сбросить раскладку к значениям по умолчанию"))
            {
                var l = (IslandLayout)target;
                if (EditorUtility.DisplayDialog("Остров v2", "Сбросить ВСЕ числа раскладки к значениям по умолчанию?", "Сбросить", "Отмена"))
                {
                    Undo.RecordObject(l, "Reset island layout");
                    var fresh = CreateInstance<IslandLayout>();
                    EditorUtility.CopySerialized(fresh, l);
                    l.ResetToDefaults();
                    DestroyImmediate(fresh);
                    EditorUtility.SetDirty(l);
                }
            }
        }
    }
}
