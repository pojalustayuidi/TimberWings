// Assets/_Project/Scripts/Core/Style/CloudCandyStyle.Island.cs (НЕ в Editor)
// Остров v2, этап 3 (свет, небо, дымка): ВСЕ новые числа лежат здесь, в том же ассете CloudCandyStyle, что и остальной свет.
// Часть partial-класса CloudCandyStyle. Остальные части: CloudCandyStyle.cs (цвета суток, постобработка, три качества).
//
// Что внутри:
//   • sunYawNoon, skyCloudScale, skyCloudSpeed: сторона света солнца и облака неба (читают DayNightCycle и SkyDome через ApplyTo в основной части);
//   • sea*: цвета и покрытие «моря облаков» под островом как фон (читает CloudSea, метод ApplyTo(CloudSea) ниже);
//   • groundShadow*: холодный тон теней земли (шейдер Project/IslandV2/VertexColor читает через глобальные переменные, их ставит IslandLook);
//   • fogMulIsland: во сколько раз IslandAbyss режет плотность тумана (раньше было полем на runtime-объекте и терялось);
//   • haze: мягкие карточки дымки у краёв и в низинах (рисует IslandLook.Haze.cs, один instanced draw call);
//   • три профиля бюджета: Mobile Low = качество «Низкое», Mobile High = «Среднее», ПК = «Высокое» (GameSettings.LightQuality).
//     Профиль задаёт число карточек дымки, дальность теней и лимиты для проверки «Остров v2 / 7. Замер света и дымки».
// Значения по умолчанию подобраны «на глаз» под референс: их нужно смотреть в Unity и подкручивать в инспекторе (в том числе в Play Mode).
using UnityEngine;

public partial class CloudCandyStyle
{
    // ------------------------------------------------------------------ типы
    [System.Serializable]
    public class HazeSettings
    {
        [Tooltip("Выключить = карточек дымки нет вообще (только обычный туман)")]
        public bool enabled = true;
        [Tooltip("Рисовать дымку только для главной камеры. Включено: миникарта и служебные камеры дымку не видят")]
        public bool onlyMainCamera = true;
        [Tooltip("Насколько метров внутрь острова от края сдвинуты карточки «кольца» дымки")]
        public float edgeInset = 5f;
        [Tooltip("Доля карточек, которые лежат не по краю, а внутри острова (низины, берега). 0 = только кольцо по краю")]
        [Range(0f, 1f)] public float innerShare = 0.35f;
        [Tooltip("Размер карточки в метрах: от и до (диаметр мягкого пятна)")]
        public Vector2 size = new Vector2(16f, 30f);
        [Tooltip("Высота центра карточки над землёй (GroundY), метры: от и до")]
        public Vector2 height = new Vector2(1.5f, 6f);
        [Tooltip("Непрозрачность в центре карточки днём. Больше 0.5 уже похоже на туман, а не на лёгкую дымку")]
        [Range(0f, 1f)] public float alpha = 0.30f;
        [Tooltip("Насколько цвет дымки светлее цвета тумана сцены (0 = цвет тумана, 1 = белый)")]
        [Range(0f, 1f)] public float whiten = 0.30f;
        [Tooltip("Насколько дымка подкрашена цветом солнца (на закате розовеет сильнее)")]
        [Range(0f, 1f)] public float sunTint = 0.15f;
        [Tooltip("Во сколько раз дымка слабее ночью (1 = так же, 0 = ночью исчезает)")]
        [Range(0f, 1f)] public float nightAlphaMul = 0.6f;
        [Tooltip("Расстояние от камеры, где карточка начинает проявляться (метры). Ближе этого дымки нет: игрок и тропа читаются чисто")]
        public float nearFadeStart = 14f;
        [Tooltip("Расстояние от камеры, где карточка проявилась полностью (метры)")]
        public float nearFadeEnd = 32f;
        [Tooltip("Дальше этого расстояния карточка начинает таять (метры)")]
        public float farFadeStart = 160f;
        [Tooltip("Дальше этого расстояния карточки не видно (метры)")]
        public float farFadeEnd = 280f;
        [Tooltip("Скорость дрейфа карточки, метры в секунду. 0 = стоят")]
        public float driftSpeed = 0.30f;
        [Tooltip("Насколько метров карточка уплывает от своего места и возвращается")]
        public float driftRadius = 4f;
        [Tooltip("Зерно случайности раскладки: одно число даёт одну и ту же раскладку каждый запуск")]
        public int seed = 11;
    }

    // Профиль бюджета. Один на каждое качество света
    [System.Serializable]
    public class IslandBudget
    {
        [Tooltip("Сколько карточек дымки рисовать (все в одном instanced draw call, 2 треугольника на карточку)")]
        [Range(0, 64)] public int hazeCards = 16;
        [Tooltip("Дальность теней солнца в метрах (поверх расчёта LightingRig по радиусу острова, если включён флаг ниже)")]
        public float shadowDistance = 55f;
        [Tooltip("Можно ли профилю Bloom. Реально Bloom включает блок «Три качества» (bloomOn); если там включён, а здесь нет, проверка покажет предупреждение")]
        public bool bloomAllowed = true;
        [Tooltip("Лимит для проверки: насколько Batches может вырасти из-за дымки (замер «с дымкой» минус «без дымки»)")]
        public int maxExtraBatches = 2;
        [Tooltip("Лимит для проверки: насколько Tris может вырасти из-за дымки")]
        public int maxExtraTris = 300;
        [Tooltip("Лимит для проверки: дальность теней не больше этого числа (метры). Тени дальше стоят дорого на телефоне")]
        public float shadowDistanceMax = 60f;
        [Tooltip("Нужное число каскадов теней в URP Asset этого профиля. Скрипт его НЕ меняет, только сверяет в проверке (меняется в URP Asset руками)")]
        [Range(1, 4)] public int shadowCascades = 2;
    }

    // ------------------------------------------------------------------ поля
    [Header("Остров v2: солнце и облака неба")]
    [Tooltip("Куда светит солнце в полдень (градусы, yaw). Камера смотрит под yaw 45; свет чуть сбоку даёт объём домикам и деревьям. На картинке-референсе тени падают вправо-вниз")]
    public float sunYawNoon = -20f;
    [Tooltip("Размер облаков на небе (больше = мельче). Небо видно в основном сверху, в свободной камере")]
    public float skyCloudScale = 1.1f;
    [Tooltip("Скорость дрейфа облаков неба")]
    public float skyCloudSpeed = 0.012f;

    [Header("Остров v2: туман острова")]
    [Tooltip("Во сколько раз IslandAbyss уменьшает плотность тумана (fogDensityDay и fogDensityNight выше). 1 = без уменьшения. Меньше = ясный воздух и резкие облака, больше = мягкая дымка вдали")]
    [Range(0.05f, 1f)] public float fogMulIsland = 0.4f;

    [Header("Остров v2: облака под островом (только фон, сами облака делает этап 4)")]
    [Tooltip("Цвет верха облаков днём. Почти белый, чуть голубой")]
    public Color seaTop = H("#F4F8FF");
    [Tooltip("Цвет теней облаков днём: светлый лавандово-голубой, как на референсе, а не тёмно-синий")]
    public Color seaShade = H("#A9B8F0");
    [Tooltip("Доля покрытия слоёв (0 = пусто, 1 = сплошь)")]
    [Range(0f, 1f)] public float seaCover = 0.48f;
    [Tooltip("Насколько нижние слои темнее верхних (0 = одинаковые). Меньше = светлее и воздушнее")]
    [Range(0f, 1f)] public float seaLayerDarken = 0.40f;

    [Header("Остров v2: тон теней на земле (шейдер Project/IslandV2/VertexColor)")]
    [Tooltip("Цвет-множитель, в который уходит земля в тени. Светлый лавандовый: тени «цветные», как на референсе, а не серые")]
    public Color groundShadowTint = H("#B7ADE8");
    [Tooltip("Сила тона теней. 0 = земля в тени просто темнее без оттенка")]
    [Range(0f, 1f)] public float groundShadowStrength = 0.55f;

    [Header("Остров v2: дымка (карточки у краёв и в низинах)")]
    public HazeSettings haze = new HazeSettings();

    [Header("Остров v2: профили бюджета (качество света Низкое / Среднее / Высокое)")]
    [Tooltip("Включить: LightingRig берёт дальность теней из профиля (shadowDistance), а не считает по радиусу острова. Остров v2 с радиусом около 75 м иначе даёт тени на 100+ метров")]
    public bool useBudgetShadowDistance = true;
    [Tooltip("Mobile Low = качество света «Низкое»: дымка минимальная, тени короткие, без Bloom")]
    public IslandBudget budgetMobileLow = new IslandBudget
    { hazeCards = 8, shadowDistance = 35f, bloomAllowed = false, maxExtraBatches = 2, maxExtraTris = 160, shadowDistanceMax = 40f, shadowCascades = 1 };
    [Tooltip("Mobile High = качество света «Среднее»")]
    public IslandBudget budgetMobileHigh = new IslandBudget
    { hazeCards = 16, shadowDistance = 55f, bloomAllowed = true, maxExtraBatches = 2, maxExtraTris = 300, shadowDistanceMax = 60f, shadowCascades = 2 };
    [Tooltip("ПК = качество света «Высокое»")]
    public IslandBudget budgetPc = new IslandBudget
    { hazeCards = 32, shadowDistance = 90f, bloomAllowed = true, maxExtraBatches = 3, maxExtraTris = 600, shadowDistanceMax = 100f, shadowCascades = 4 };

    // ------------------------------------------------------------------ доступ
    public IslandBudget GetBudget(LightQualityLevel q)
    {
        switch (q)
        {
            case LightQualityLevel.Low: return budgetMobileLow;
            case LightQualityLevel.High: return budgetPc;
            default: return budgetMobileHigh;
        }
    }

    public static string BudgetName(LightQualityLevel q)
    {
        switch (q)
        {
            case LightQualityLevel.Low: return "Mobile Low";
            case LightQualityLevel.High: return "ПК";
            default: return "Mobile High";
        }
    }

    // CloudSea хранит цвета фона публичными полями и сам их не знает из ассета: копируем перед рисованием
    public void ApplyTo(CloudSea s)
    {
        s.dayTop = seaTop;
        s.dayShade = seaShade;
        s.cover = seaCover;
        s.layerDarken = seaLayerDarken;
    }
}
