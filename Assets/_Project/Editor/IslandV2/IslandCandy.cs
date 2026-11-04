// Assets/_Project/Editor/IslandV2/IslandCandy.cs
// Остров v2: настройки «Candy-земли» (сахарный мультяшный вид травы и троп). Ассет Assets/_Project/IslandV2/IslandCandy.asset.
// Отдельный ассет, а не поля IslandLayout: раскладка (контур, тропы, здания) не трогается, откат = галка candyOn.
//
// Что здесь: цвета травы (4 остановки градиента мята -> зелень -> лайм -> солнечный), леса, троп (кремовая лента с персиковым краем и белой кромкой),
// берега, площадок, и числа раскраски (ширина «подушки» тропы, дрожание края, контактная тень). Всё запекается в текстуры земли
// (IslandBlockout.GroundTex): после правки нажми «Candy-земля: перекрасить» (меню 133), пересобирать блокаут не нужно.
// Живые ползунки (объём тропы, холмики, блеск, посыпка, искры, облака) лежат НЕ здесь, а на материале Mat_IslandV2 (шейдер IslandVertexColor).
// Reset в контекстном меню ассета возвращает значения по умолчанию.
using UnityEditor;
using UnityEngine;

namespace IslandV2
{
    public class IslandCandy : ScriptableObject
    {
        public const string AssetPath = IslandLayout.AssetFolder + "/IslandCandy.asset";

        [Header("Общее")]
        [Tooltip("Выключить = вернуться к старой раскраске земли (по IslandLayout), без маски и эффектов.")]
        public bool candyOn = true;
        [Tooltip("Метров на пиксель текстуры земли. Меньше = резче скругление тропы, дольше запекание (0.4 хватает).")]
        [Range(0.2f, 1f)] public float texelSize = 0.4f;

        [Header("Трава: четыре остановки, плавно (без резких пятен)")]
        public Color grassMint = H("#6FDB9E");    // низины, прохладная мята
        public Color grassGreen = H("#8CE56E");   // основной свежий зелёный
        public Color grassLime = H("#BCEF70");    // лайм
        public Color grassSun = H("#DBF58E");     // солнечные пятна, тёплый кремовый лайм
        public float grassScale = 22f;
        [Range(1f, 3f)] public float grassContrast = 1.9f;
        [Range(0f, 0.1f)] public float grassGrain = 0.04f;

        [Header("Трава: мягкие полутона ISL-007")]
        [Tooltip("0 = одна середина палитры, 1 = исходный диапазон оттенков. Не влияет на тропинки и геометрию.")]
        [Range(0f, 1f)] public float grassVariation = 1f;
        [Tooltip("Редкие прохладные полутона травы. 0 = без дополнительных пятен (поведение до ISL-007).")]
        [Range(0f, 0.25f)] public float grassCoolShadeStrength = 0f;
        [Tooltip("Средний размер мягких прохладных пятен, в метрах.")]
        [Range(5f, 40f)] public float grassCoolShadeScale = 11f;
        public Color grassCoolShade = H("#70BE95");

        [Header("Лес (под деревьями)")]
        public Color forestFloor = H("#4DC48A");
        [Range(0f, 1f)] public float forestStrength = 0.7f;
        [Tooltip("Мягкие световые переливы земли под деревьями. 0.18 = исходное поведение.")]
        [Range(0f, 0.3f)] public float forestDappleStrength = 0.18f;

        [Header("Тропа: кремовая лента с «подушкой»")]
        public Color pathCream = H("#FBE9B7");
        public Color pathEdge = H("#F2C98E");
        public Color pathBlush = H("#F6C2B4");
        public Color pathRim = H("#FFF3D2");
        [Range(0f, 1f)] public float pathRimStrength = 0.55f;
        [Tooltip("Визуальная мягкость границы тропы в метрах. Ширину проходимого коридора не меняет.")]
        [Range(0.25f, 1.25f)] public float pathFeather = 0.75f;
        [Tooltip("Живой край: амплитуда визуальной неровности, м. Ось и ширина PathLine не меняются.")]
        [Range(0f, 1.5f)] public float pathWobble = 0.6f;
        [Tooltip("Доля мелкого шума на краю (0 = только плавные изгибы).")]
        [Range(0f, 0.6f)] public float pathFineNoise = 0.20f;
        [Tooltip("Сила редких розово-персиковых пятен на тропе (0 = нет).")]
        [Range(0f, 0.5f)] public float pathBlushStrength = 0.14f;
        [Tooltip("Ширина скругления края «подушки», м (от неё зависит мягкость бликов на краю).")]
        [Range(0.5f, 2.5f)] public float pathPillow = 1.2f;
        public Color contactColor = H("#45BE92");
        [Range(0f, 1f)] public float contactShadow = 0.35f;
        [Range(0.3f, 3f)] public float contactWidth = 1.3f;

        [Header("ISL-012: мягкие сопряжения тропинок (только визуальная окраска)")]
        [Tooltip("Радиус мягкого объединения двух ближайших лент, метры. 0 = прежняя форма; добавочное расширение не более четверти значения.")]
        [Range(0f, 0.9f)] public float pathJunctionRoundness = 0f;
        [Tooltip("Дополнительная мягкость цветовой границы возле пересечений, метры. На одиночных участках не действует.")]
        [Range(0f, 0.4f)] public float pathJunctionFeatherBoost = 0f;
        [Tooltip("Насколько маска псевдорельефа края совпадает с цветовым переходом: 0 = старая подушка, 1 = точно по покрытию.")]
        [Range(0f, 1f)] public float pathMaskEdgeMatch = 0f;

        [Header("Берег, площадки, брод")]
        public Color beachColor = H("#F2E2A6");
        [Range(0f, 1f)] public float beachStrength = 0.6f;
        [Range(0.3f, 3f)] public float beachWidth = 1.1f;
        public Color padColor = H("#F3E3B0");
        [Range(0f, 1f)] public float padStrength = 0.5f;
        public Color plazaStone = H("#F2E2B8");
        public Color plazaRim = H("#E3C99A");
        public Color fordSand = H("#F1E6B5");

        [Header("ISL-013: группы мелкой Candy-посыпки (только маска земли)")]
        [Tooltip("0 = вид до ISL-013; 1 = островки декоративных штрихов с чистыми промежутками. Требует перекраски земли.")]
        [Range(0f, 1f)] public float sprinkleClustering = 0f;
        [Tooltip("Средний размер групп посыпки в мировых метрах; не влияет на группы кустов и цветов.")]
        [Range(3f, 28f)] public float sprinkleClusterSize = 9f;
        [Tooltip("Порог шума: больше значение = больше чистых мест.")]
        [Range(0.2f, 0.7f)] public float sprinkleCleanThreshold = 0.43f;
        [Tooltip("Множитель плотности внутри групп: 0 = полностью чистая поверхность.")]
        [Range(0f, 1f)] public float sprinklePatchDensity = 0.75f;
        [Tooltip("Чистая полоса вдоль внешней границы тропы в метрах (только посыпка, не ширина тропы).")]
        [Range(0f, 2f)] public float sprinklePathClearance = 0.70f;
        [Tooltip("Детерминированный сдвиг крупных групп. Не изменяет Seed генератора подлеска.")]
        [Range(0, 99999)] public int sprinkleSeed = 13013;

        [Header("ISL-018: речной склон — гармония камня (только цвет)")]
        [Tooltip("0 = исходная окраска речного склона. 1 = полное смешивание к новой палитре и более мягкому переходу. Требует перекраски Candy-земли.")]
        [Range(0f, 1f)] public float riverBankHarmony = 0f;
        [Tooltip("Сухой камень выше уровня воды. Используется только при riverBankHarmony > 0.")]
        public Color riverBankDry = H("#C5C0BA");
        [Tooltip("Камень непосредственно у воды. Используется только при riverBankHarmony > 0.")]
        public Color riverBankWet = H("#829DA4");

        [Header("ISL-020: мягкие переходы между зонами (Editor bake)")]
        [Tooltip("Лёгкий промежуточный оттенок у границы леса и открытой травы. 0 = прежняя палитра.")]
        [Range(0f, 1f)] public float zoneForestBlend = 0f;
        [Tooltip("Насколько плавнее выходит песочная береговая полоса на траву, дополнительно до 0.7 м.")]
        [Range(0f, 1f)] public float zoneShoreBlend = 0f;
        [Tooltip("Дополнительное визуальное растушёвывание краёв площадок зданий, до 0.8 м. Не меняет footprint.")]
        [Range(0f, 1f)] public float zonePadBlend = 0f;

        [Header("Блеск и кромка")]
        [Range(0f, 1f)] public float glossGrass = 0.12f;
        [Range(0f, 1f)] public float glossPath = 0.85f;
        [Tooltip("Множитель к IslandLayout.edgeDarken: затемнение у кромки обрыва (меньше = светлее, воздушнее).")]
        [Range(0f, 1.5f)] public float edgeDarkScale = 0.7f;

        // трава по параметру t 0..1: мята -> зелень -> лайм -> солнечный
        public Color GrassAt(float t)
        {
            t = Mathf.Clamp01(t);
            if (t < 0.38f) return Color.Lerp(grassMint, grassGreen, Smooth(t / 0.38f));
            if (t < 0.70f) return Color.Lerp(grassGreen, grassLime, Smooth((t - 0.38f) / 0.32f));
            return Color.Lerp(grassLime, grassSun, Smooth((t - 0.70f) / 0.30f));
        }

        static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        // чистый C# без вызовов Unity: безопасно в инициализаторах полей
        static Color H(string hex)
        {
            int v = System.Convert.ToInt32(hex.TrimStart('#'), 16);
            return new Color(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f, 1f);
        }

        public static IslandCandy GetOrCreate()
        {
            var s = AssetDatabase.LoadAssetAtPath<IslandCandy>(AssetPath);
            if (s != null) return s;
            IslandLayout.EnsureFolder(IslandLayout.AssetFolder);
            s = CreateInstance<IslandCandy>();
            AssetDatabase.CreateAsset(s, AssetPath);
            AssetDatabase.SaveAssets();
            return s;
        }
    }
}
