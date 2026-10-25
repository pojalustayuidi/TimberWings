// Assets/_Project/Scripts/Core/Style/CloudCandyStyle.cs (НЕ в Editor)
// Стиль «Cloud Candy»: все цвета и числа света, неба, тумана и постобработки в одном ScriptableObject.
//
// Как работает:
//   • Ассет лежит в папке Resources (Tools > BuilderSim > Визуал и свет > Cloud Candy: создать или выбрать стиль).
//   • DayNightCycle, LightingRig, SkyDome и IslandAbyss берут значения отсюда каждый кадр или при смене Revision.
//     Меняешь число в инспекторе, в том числе в Play Mode, и картинка меняется сразу, без перекомпиляции.
//   • Ассета нет или снят флаг «Включён»: все четыре скрипта работают на своих прежних значениях (откат без правки кода).
//   • Три качества (Низкое / Среднее / Высокое) лежат здесь же, их выбирает GameSettings.LightQuality, как и раньше.
//
// Цвета в инспекторе обычные sRGB, как у остальных цветов проекта.
//
// Части (partial): CloudCandyStyle.cs (этот файл: цвета суток, постобработка, три качества), CloudCandyStyle.Island.cs
// (Остров v2, этап 3: дымка, фон облаков, профили бюджета Mobile Low / Mobile High / ПК, тон теней земли, направление солнца).
using UnityEngine;

[CreateAssetMenu(fileName = "CloudCandyStyle", menuName = "BuilderSim/Cloud Candy Style")]
public partial class CloudCandyStyle : ScriptableObject
{
    public const string ResourceName = "CloudCandyStyle";

    // ------------------------------------------------------------------ доступ
    static CloudCandyStyle cached;
    static float nextTry;

    // Растёт при любой правке в инспекторе (только редактор). Скрипты с «тяжёлым» применением (Volume) сравнивают с ним
    public static int Revision { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { cached = null; nextTry = 0f; Revision = 0; }

    // null = стиль выключен или ассета нет: вызывающий код остаётся на своих прежних значениях
    public static CloudCandyStyle Current
    {
        get
        {
            if (cached == null && Time.realtimeSinceStartup >= nextTry)
            {
                nextTry = Time.realtimeSinceStartup + 1f;   // ассет могли создать уже после старта: пробуем раз в секунду, а не каждый кадр
                cached = Resources.Load<CloudCandyStyle>(ResourceName);
            }
            return cached != null && cached.styleEnabled ? cached : null;
        }
    }

    void OnValidate() { Revision++; }

    [ContextMenu("Сбросить всё к значениям Cloud Candy")]
    void ResetToDefaults()
    {
        var fresh = CreateInstance<CloudCandyStyle>();
        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(fresh), this);
        DestroyImmediate(fresh);
        Revision++;
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    // разбор #RRGGBB вручную, без обращений к движку: конструкторы полей вызываются и при десериализации
    static Color H(string hex)
    {
        if (hex == null || hex.Length != 7 || hex[0] != '#') return Color.magenta;
        return new Color(
            System.Convert.ToInt32(hex.Substring(1, 2), 16) / 255f,
            System.Convert.ToInt32(hex.Substring(3, 2), 16) / 255f,
            System.Convert.ToInt32(hex.Substring(5, 2), 16) / 255f, 1f);
    }

    // ------------------------------------------------------------------ данные
    [System.Serializable]
    public class Trio
    {
        public Color sky, equator, ground;
        public Trio() { }
        public Trio(string s, string e, string g) { sky = H(s); equator = H(e); ground = H(g); }
    }

    [System.Serializable]
    public class Grade
    {
        public float postExposure, contrast, saturation;
        public Color filter;
        public Grade() { }
        public Grade(float e, float c, float s, string f) { postExposure = e; contrast = c; saturation = s; filter = H(f); }
    }

    [System.Serializable]
    public class Quality
    {
        public bool bloomOn;
        public float bloom, bloomThreshold, bloomScatter, vignette;
        [Tooltip("Множитель дальности теней")] public float shadowDistMul;
        public LightShadows sun, lantern;
        [Tooltip("false = у прочих точечных и прожекторных теней нет")] public bool otherLightShadows;
    }

    [Header("Общее")]
    [Tooltip("Выключить = DayNightCycle, LightingRig и SkyDome работают на своих прежних значениях (откат)")]
    public bool styleEnabled = true;

    [Header("Солнце (цвет по высоте над горизонтом)")]
    public float sunMaxIntensity = 1.2f;
    [Range(0f, 1f)] public float sunShadowStrength = 0.75f;
    [Tooltip("Солнце на горизонте")] public Color sunHorizon = H("#FFA98A");
    [Tooltip("Около 8 градусов")] public Color sunLow = H("#FFCBA0");
    [Tooltip("Около 20 градусов")] public Color sunGold = H("#FFE0AE");
    [Tooltip("Полдень")] public Color sunNoon = H("#FFE9B8");

    [Header("Луна")]
    public Color moonColor = H("#AEBDFF");
    public float moonIntensity = 0.7f;
    [Range(0f, 1f)] public float moonShadowStrength = 0.45f;

    [Header("Ambient (Trilight): тени получают цвет неба, поэтому небо чуть сине-фиолетовое")]
    public Trio ambientDay = new Trio("#B4CCFF", "#E8D8F0", "#FFD8BE");
    public Trio ambientDawn = new Trio("#CDB8EB", "#FFC7B3", "#F2BFB3");
    public Trio ambientDusk = new Trio("#B39EE0", "#FFAD99", "#EBAD9E");
    public Trio ambientNight = new Trio("#5C6BB8", "#475594", "#2E3361");

    [Header("Туман и фон камеры")]
    public FogMode fogMode = FogMode.Exponential;
    public Color fogDay = H("#CFE9FF");
    public Color fogDawn = H("#F7D5E6");
    public Color fogDusk = H("#F8C6C0");
    public Color fogNight = H("#2A3366");
    [Tooltip("Плотность до множителя IslandAbyss (он режет её примерно втрое). Для Exponential: 0.006 даёт лёгкую дымку на дальних облаках")]
    public float fogDensityDay = 0.006f;
    public float fogDensityNight = 0.006f;

    [Header("Небо (SkyDome): зенит и горизонт")]
    public Color zenithDay = H("#73BDFF");
    public Color zenithDawn = H("#9E9EF2");
    public Color zenithDusk = H("#8573D1");
    public Color zenithNight = H("#121A47");
    public Color horizonDay = H("#CFE9FF");
    public Color horizonDawn = H("#FFD1CC");
    public Color horizonDusk = H("#FFB8AD");
    public Color horizonNight = H("#334080");
    [Range(0f, 1f)] public float cloudCover = 0.4f;
    [Tooltip("Цвет тени на облаках днём (умножается на цвет света облака)")]
    public Color cloudShadeDay = H("#BDC2FA");

    [Header("Volume «Island Light» (общий грейд дня)")]
    public float postExposure = 0.3f;
    public float contrast = 6f;
    public float saturation = 12f;
    public Color colorFilter = H("#FFFAF0");
    public Color bloomTint = H("#FFF0E0");
    public Color vignetteColor = H("#3B2E61");
    [Range(0.01f, 1f)] public float vignetteSmoothness = 0.45f;

    [Header("Volume суток (DayNight Dawn / Dusk / Night)")]
    public Grade gradeDawn = new Grade(0.10f, 4f, 12f, "#FFEBF2");
    public Grade gradeDusk = new Grade(0.10f, 6f, 16f, "#FFE0D1");
    public Grade gradeNight = new Grade(0.35f, 4f, -4f, "#C7D6FF");

    [Header("Три качества (GameSettings.LightQuality). Bloom с порогом около 1 виден только при включённом HDR в URP Asset")]
    public Quality low = new Quality
    {
        bloomOn = false, bloom = 0f, bloomThreshold = 1.0f, bloomScatter = 0.6f, vignette = 0.18f,
        shadowDistMul = 0.6f, sun = LightShadows.Hard, lantern = LightShadows.None, otherLightShadows = false
    };
    public Quality medium = new Quality
    {
        bloomOn = true, bloom = 0.40f, bloomThreshold = 1.0f, bloomScatter = 0.65f, vignette = 0.20f,
        shadowDistMul = 0.85f, sun = LightShadows.Soft, lantern = LightShadows.Hard, otherLightShadows = true
    };
    public Quality high = new Quality
    {
        bloomOn = true, bloom = 0.55f, bloomThreshold = 1.0f, bloomScatter = 0.7f, vignette = 0.20f,
        shadowDistMul = 1.0f, sun = LightShadows.Soft, lantern = LightShadows.Soft, otherLightShadows = true
    };

    public Quality GetQuality(LightQualityLevel q)
    {
        switch (q)
        {
            case LightQualityLevel.Low: return low;
            case LightQualityLevel.High: return high;
            default: return medium;
        }
    }

    // ------------------------------------------------------------------ применение к скриптам (копируют публичные поля)
    public void ApplyTo(DayNightCycle d)
    {
        d.sunMaxIntensity = sunMaxIntensity;
        d.sunYawNoon = sunYawNoon;   // Остров v2: сторона света солнца в полдень (поле в CloudCandyStyle.Island.cs)
        d.moonColor = moonColor;
        d.moonIntensity = moonIntensity;
        d.moonShadowStrength = moonShadowStrength;

        d.ambDaySky = ambientDay.sky; d.ambDayEq = ambientDay.equator; d.ambDayGround = ambientDay.ground;
        d.ambDawnSky = ambientDawn.sky; d.ambDawnEq = ambientDawn.equator; d.ambDawnGround = ambientDawn.ground;
        d.ambDuskSky = ambientDusk.sky; d.ambDuskEq = ambientDusk.equator; d.ambDuskGround = ambientDusk.ground;
        d.ambNightSky = ambientNight.sky; d.ambNightEq = ambientNight.equator; d.ambNightGround = ambientNight.ground;

        d.fogDay = fogDay; d.fogDawn = fogDawn; d.fogDusk = fogDusk; d.fogNight = fogNight;
        // плотность тумана при парящем острове ведёт IslandAbyss (берёт базу отсюда же и умножает на fogMul)
        if (IslandAbyss.Instance == null)
        {
            d.fogDensityDay = fogDensityDay;
            d.fogDensityNight = fogDensityNight;
        }
    }

    public void ApplyTo(SkyDome s)
    {
        s.zenithDay = zenithDay; s.zenithDawn = zenithDawn; s.zenithDusk = zenithDusk; s.zenithNight = zenithNight;
        s.horizonDay = horizonDay; s.horizonDawn = horizonDawn; s.horizonDusk = horizonDusk; s.horizonNight = horizonNight;
        s.cloudCover = cloudCover;
        s.cloudScale = skyCloudScale;   // Остров v2: размер и скорость облаков неба (CloudCandyStyle.Island.cs)
        s.cloudSpeed = skyCloudSpeed;
    }

    // цвет солнца по высоте (градусы). Те же пороги, что были в DayNightCycle: 0 / 8 / 20 / 50
    public Color SunColorAt(float e)
    {
        if (e <= 0f) return sunHorizon;
        if (e < 8f) return Color.Lerp(sunHorizon, sunLow, Mathf.SmoothStep(0f, 1f, e / 8f));
        if (e < 20f) return Color.Lerp(sunLow, sunGold, Mathf.SmoothStep(0f, 1f, (e - 8f) / 12f));
        return Color.Lerp(sunGold, sunNoon, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(20f, 50f, e)));
    }
}
