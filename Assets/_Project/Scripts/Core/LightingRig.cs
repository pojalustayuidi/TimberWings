// Assets/_Project/Scripts/Core/LightingRig.cs (НЕ в Editor)
// Общее освещение острова. Создаётся сам после загрузки сцены с игроком (как VibeBootstrap), в сцену ничего вешать не нужно.
//
// Что делает:
//  1. Солнце «золотой час»: Directional, угол ~38°, цвет #FFD7A0, мягкие тени, дальность теней под размер острова.
//     Если в сцене уже есть Directional Light (RenderSettings.sun или самый яркий), берёт его, второй не создаёт.
//  2. Трёхцветный ambient (Trilight): небо холодное голубое, экватор тёплый, земля зелёно-коричневая. Лёгкий туман.
//     Цвет фона камеры (FloatingIsland.FitGround кладёт туда fogColor) подтягивается к новому туману, чтобы за краем карты не было шва.
//  3. Глобальный Volume «Island Light» (Bloom слабый с порогом выше 1, Vignette, Color Adjustments, Tonemapping).
//     Приоритет 15: выше общего «Vibe Grade» (10), ниже лесного «Vibe Forest» (20), поэтому лес по-прежнему блендится поверх.
//  4. Качество берёт из GameSettings.LightQuality и пересчитывает сразу по событию GameSettings.Changed:
//       Низкое  : без Bloom (даже в лесу), тени солнца жёсткие и короче, у фонарей и других точечных/прожекторных нет реальных теней
//       Среднее : слабый Bloom, мягкие тени солнца, у фонарей жёсткие тени
//       Высокое : Bloom чуть сильнее, тени дальше, у фонарей мягкие тени
//
// Остров v2 (этап 3): если в ассете Cloud Candy включён useBudgetShadowDistance, дальность теней берётся из профиля бюджета
// (Mobile Low / Mobile High / ПК = качество света Низкое / Среднее / Высокое), а не считается по радиусу острова.
//
// Заметки по URP-ассету (проверь один раз): Bloom с порогом выше 1 виден только при включённом HDR;
// мягкие тени и тени дополнительных источников света должны быть разрешены в URP Asset (Lighting > Additional Lights > Cast Shadows).
// Дальность теней в URP живёт в ассете: скрипт меняет её на время игры и возвращает прежнюю при выходе из сцены.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public class LightingRig : MonoBehaviour
{
    public static LightingRig Instance { get; private set; }

    // true: цвет, силу и поворот солнца, ambient, туман и фон камеры ведёт DayNightCycle. Тени и Volume «Island Light» остаются за нами
    [HideInInspector] public bool dayNightDriven;

    [Header("Солнце: золотой час")]
    public bool controlSunTransform = true;                 // false = не трогать поворот солнца (например, когда появится DayNightCycle)
    [Range(10f, 80f)] public float sunPitch = 38f;          // высота над горизонтом, градусы
    public float sunYaw = -20f;                             // сторона света; камера смотрит под yaw 45, свет чуть сбоку даёт объём
    public Color sunColor = new Color(1f, 0.843f, 0.627f);  // #FFD7A0
    public float sunIntensity = 1.25f;
    [Range(0f, 1f)] public float sunShadowStrength = 0.8f;
    [Tooltip("Дальность теней = радиус острова × это число (потом × множитель качества)")]
    public float shadowDistanceFactor = 1.6f;
    public float shadowDistanceMin = 40f;
    public float shadowDistanceMax = 140f;

    [Header("Ambient (Trilight)")]
    public Color ambientSky = new Color(0.55f, 0.71f, 0.89f);       // холодное голубое
    public Color ambientEquator = new Color(0.91f, 0.78f, 0.60f);   // тёплое
    public Color ambientGround = new Color(0.37f, 0.35f, 0.23f);    // зелёно-коричневое
    [Range(0f, 1f)] public float ambientScale = 1f;                 // множитель яркости ambient (вечер: меньше, чтобы огни были заметны)

    [Header("Лёгкий туман")]
    public Color fogColor = new Color(0.89f, 0.82f, 0.71f);         // тёплая дымка
    public float fogDensity = 0.010f;                               // ExponentialSquared: на 30 м ~9%, на 60 м ~30%
    public bool syncCameraBackground = true;

    [Header("Volume «Island Light»")]
    public float volumePriority = 15f;                              // Vibe Grade = 10, Vibe Forest = 20
    public float capPriority = 100f;                                // «потолок»: гасит Bloom на Низком поверх всех

    [Header("Фонари")]
    public float lightRescanInterval = 3f;                          // как часто искать новые фонари, секунды (реальное время)

    struct Preset
    {
        public float bloom, bloomThreshold, bloomScatter, vignette, shadowDistMul;
        public bool bloomOn;
        public LightShadows sun, lantern;
        public bool otherLightShadows;     // false = у прочих точечных/прожекторных тени выключены, true = как задумал автор
    }

    static Preset GetPreset(LightQualityLevel q)
    {
        // Cloud Candy: три качества лежат в ассете стиля
        var st = CloudCandyStyle.Current;
        if (st != null)
        {
            var c = st.GetQuality(q);
            return new Preset { bloomOn = c.bloomOn, bloom = c.bloom, bloomThreshold = c.bloomThreshold, bloomScatter = c.bloomScatter,
                vignette = c.vignette, shadowDistMul = c.shadowDistMul, sun = c.sun, lantern = c.lantern, otherLightShadows = c.otherLightShadows };
        }
        switch (q)
        {
            case LightQualityLevel.Low:
                return new Preset { bloomOn = false, bloom = 0f, bloomThreshold = 1.1f, bloomScatter = 0.6f, vignette = 0.22f,
                    shadowDistMul = 0.6f, sun = LightShadows.Hard, lantern = LightShadows.None, otherLightShadows = false };
            case LightQualityLevel.High:
                return new Preset { bloomOn = true, bloom = 0.40f, bloomThreshold = 1.05f, bloomScatter = 0.7f, vignette = 0.28f,
                    shadowDistMul = 1.0f, sun = LightShadows.Soft, lantern = LightShadows.Soft, otherLightShadows = true };
            default:
                return new Preset { bloomOn = true, bloom = 0.25f, bloomThreshold = 1.1f, bloomScatter = 0.6f, vignette = 0.25f,
                    shadowDistMul = 0.85f, sun = LightShadows.Soft, lantern = LightShadows.Hard, otherLightShadows = true };
        }
    }

    // ---------------------------------------------------------------- создание
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Init();
    }

    static void OnSceneLoaded(Scene s, LoadSceneMode m) { Init(); }

    static void Init()
    {
        if (PlayerMovement.Instance == null) return;   // меню и пустые сцены не трогаем
        if (Object.FindFirstObjectByType<LightingRig>() != null) return;
        new GameObject("_LightingRig").AddComponent<LightingRig>();
    }

    // ---------------------------------------------------------------- состояние
    Light sun;
    bool sunCreated;
    Volume gradeVolume, capVolume;
    VolumeProfile gradeProfile, capProfile;
    Bloom gradeBloom, capBloom;
    Vignette vignette;
    ColorAdjustments colorAdj;
    Tonemapping tonemap;

    readonly Dictionary<Light, LightShadows> authored = new Dictionary<Light, LightShadows>();
    readonly List<Light> dead = new List<Light>();
    float nextRescan;

    UniversalRenderPipelineAsset urp;
    float urpShadowDistOrig = -1f;
    float qsShadowDistOrig = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { Instance = null; }

    void Awake()
    {
        Instance = this;
        BuildVolumes();
    }

    void OnEnable()
    {
        GameSettings.Changed += Apply;
        Apply();
    }

    void OnDisable()
    {
        GameSettings.Changed -= Apply;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (urp != null && urpShadowDistOrig >= 0f) urp.shadowDistance = urpShadowDistOrig;
        if (qsShadowDistOrig >= 0f) QualitySettings.shadowDistance = qsShadowDistOrig;
        if (sunCreated && sun != null) Destroy(sun.gameObject);
        if (gradeProfile != null) Destroy(gradeProfile);
        if (capProfile != null) Destroy(capProfile);
    }

    int styleRev = -1;

    void Update()
    {
        // правка стиля в инспекторе (в том числе в Play Mode): пересчитываем сразу
        if (styleRev != CloudCandyStyle.Revision) { styleRev = CloudCandyStyle.Revision; Apply(); }

        // фонари могут появиться позже (стройка): подхватываем их с текущим качеством
        if (Time.unscaledTime < nextRescan) return;
        nextRescan = Time.unscaledTime + Mathf.Max(0.5f, lightRescanInterval);
        ApplyPointLights(GetPreset(GameSettings.LightQuality));
    }

    // ---------------------------------------------------------------- Volume
    void BuildVolumes()
    {
        gradeVolume = MakeVolume("Island Light", volumePriority, out gradeProfile);
        gradeBloom = gradeProfile.Add<Bloom>(false);
        vignette = gradeProfile.Add<Vignette>(false);
        colorAdj = gradeProfile.Add<ColorAdjustments>(false);
        tonemap = gradeProfile.Add<Tonemapping>(false);

        // Tonemapping тот же Neutral, что в «Vibe Forest»: режим не интерполируется, иначе в лесу картинка щёлкнет
        tonemap.mode.Override(TonemappingMode.Neutral);

        colorAdj.postExposure.Override(0.10f);
        colorAdj.contrast.Override(10f);
        colorAdj.saturation.Override(10f);
        colorAdj.colorFilter.Override(new Color(1f, 0.95f, 0.88f));

        vignette.color.Override(Color.black);
        vignette.smoothness.Override(0.45f);
        vignette.rounded.Override(false);

        gradeBloom.tint.Override(new Color(1f, 0.92f, 0.80f));
        gradeBloom.highQualityFiltering.Override(false);   // телефон

        // «потолок» только про Bloom: на Низком вес 1 и интенсивность 0 поверх всех, в том числе лесного грейда
        capVolume = MakeVolume("Island Light Cap", capPriority, out capProfile);
        capBloom = capProfile.Add<Bloom>(false);
        capBloom.intensity.Override(0f);
        capVolume.weight = 0f;
    }

    Volume MakeVolume(string volumeName, float priority, out VolumeProfile profile)
    {
        var go = new GameObject(volumeName);
        go.transform.SetParent(transform, false);
        var v = go.AddComponent<Volume>();
        v.isGlobal = true;
        v.priority = priority;
        v.weight = 1f;
        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = volumeName;
        v.sharedProfile = profile;
        return v;
    }

    // ---------------------------------------------------------------- применение
    void Apply()
    {
        Preset p = GetPreset(GameSettings.LightQuality);

        ApplySun(p);
        ApplyEnvironment();
        ApplyGrade();
        ApplyVolume(p);
        ApplyPointLights(p);
        EnsurePostProcessing();
    }

    void ApplySun(Preset p)
    {
        if (sun == null) sun = FindSun();
        if (sun == null)
        {
            var go = new GameObject("Sun (LightingRig)");
            sun = go.AddComponent<Light>();
            sun.type = LightType.Directional;
            sunCreated = true;
        }
        RenderSettings.sun = sun;

        sun.shadows = p.sun;
        if (!dayNightDriven)   // при смене суток цвет, силу и поворот солнца считает DayNightCycle каждый кадр
        {
            var cs = CloudCandyStyle.Current;
            sun.color = cs != null ? cs.sunNoon : sunColor;
            sun.intensity = cs != null ? cs.sunMaxIntensity : sunIntensity;
            sun.shadowStrength = cs != null ? cs.sunShadowStrength : sunShadowStrength;
            if (controlSunTransform) sun.transform.rotation = Quaternion.Euler(sunPitch, sunYaw, 0f);
        }

        float dist = Mathf.Clamp(IslandRadius() * shadowDistanceFactor, shadowDistanceMin, shadowDistanceMax) * p.shadowDistMul;
        // Остров v2, этап 3: дальность теней из профиля бюджета (Mobile Low / Mobile High / ПК) в ассете Cloud Candy
        var budgetStyle = CloudCandyStyle.Current;
        if (budgetStyle != null && budgetStyle.useBudgetShadowDistance)
            dist = Mathf.Max(10f, budgetStyle.GetBudget(GameSettings.LightQuality).shadowDistance);
        SetShadowDistance(dist);
    }

    static Light FindSun()
    {
        Light s = RenderSettings.sun;
        if (s != null && s.type == LightType.Directional) return s;

        Light best = null;
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (l.type != LightType.Directional) continue;
            if (best == null || l.intensity > best.intensity) best = l;
        }
        return best;
    }

    // радиус острова: контур FloatingIsland -> рельеф (Terrain) -> запасное значение
    static float IslandRadius()
    {
        IslandSource fi = IslandSource.Find();
        if (fi != null) return fi.Radius;

        Terrain t = Terrain.activeTerrain;
        if (t != null && t.terrainData != null) return Mathf.Max(t.terrainData.size.x, t.terrainData.size.z) * 0.5f;

        return 50f;
    }

    void SetShadowDistance(float d)
    {
        if (urp == null) urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp != null)
        {
            if (urpShadowDistOrig < 0f) urpShadowDistOrig = urp.shadowDistance;
            urp.shadowDistance = d;
        }
        if (qsShadowDistOrig < 0f) qsShadowDistOrig = QualitySettings.shadowDistance;
        QualitySettings.shadowDistance = d;
    }

    void ApplyEnvironment()
    {
        if (dayNightDriven) return;   // ambient, туман и фон камеры ведёт DayNightCycle

        var cs = CloudCandyStyle.Current;
        Color aSky = cs != null ? cs.ambientDay.sky : ambientSky;
        Color aEq = cs != null ? cs.ambientDay.equator : ambientEquator;
        Color aGr = cs != null ? cs.ambientDay.ground : ambientGround;
        Color fCol = cs != null ? cs.fogDay : fogColor;

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = aSky * ambientScale;
        RenderSettings.ambientEquatorColor = aEq * ambientScale;
        RenderSettings.ambientGroundColor = aGr * ambientScale;

        RenderSettings.fog = true;
        RenderSettings.fogMode = cs != null ? cs.fogMode : FogMode.ExponentialSquared;
        RenderSettings.fogColor = fCol;
        RenderSettings.fogDensity = cs != null ? cs.fogDensityDay : fogDensity;

        if (syncCameraBackground)
        {
            Camera cam = Camera.main;
            if (cam != null && cam.clearFlags == CameraClearFlags.SolidColor) cam.backgroundColor = fCol;
        }
    }

    // Общий грейд дня. Со стилем Cloud Candy значения из ассета, без него прежние (откат)
    void ApplyGrade()
    {
        var cs = CloudCandyStyle.Current;
        colorAdj.postExposure.Override(cs != null ? cs.postExposure : 0.10f);
        colorAdj.contrast.Override(cs != null ? cs.contrast : 10f);
        colorAdj.saturation.Override(cs != null ? cs.saturation : 10f);
        colorAdj.colorFilter.Override(cs != null ? cs.colorFilter : new Color(1f, 0.95f, 0.88f));
        vignette.color.Override(cs != null ? cs.vignetteColor : Color.black);
        vignette.smoothness.Override(cs != null ? cs.vignetteSmoothness : 0.45f);
        gradeBloom.tint.Override(cs != null ? cs.bloomTint : new Color(1f, 0.92f, 0.80f));
    }

    void ApplyVolume(Preset p)
    {
        gradeBloom.intensity.Override(p.bloom);
        gradeBloom.threshold.Override(p.bloomThreshold);   // выше 1: светятся только действительно яркие места
        gradeBloom.scatter.Override(p.bloomScatter);
        vignette.intensity.Override(p.vignette);

        capVolume.weight = p.bloomOn ? 0f : 1f;
    }

    // фонари и прочие точечные/прожекторные: Низкое = без реальных теней
    void ApplyPointLights(Preset p)
    {
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (l.type != LightType.Point && l.type != LightType.Spot) continue;
            // огни ведёт FireLightPool (Lights пула и выключенные исходные Light фонарей): тени у них решает пул
            if (l.GetComponent<FirePoolLight>() != null || l.GetComponent<FireSource>() != null) continue;

            LightShadows orig;
            if (!authored.TryGetValue(l, out orig)) { orig = l.shadows; authored[l] = orig; }

            if (IsLantern(l)) l.shadows = p.lantern;
            else l.shadows = p.otherLightShadows ? orig : LightShadows.None;
        }

        dead.Clear();
        foreach (var kv in authored) if (kv.Key == null) dead.Add(kv.Key);
        foreach (var k in dead) authored.Remove(k);
    }

    static bool IsLantern(Light l)
    {
        string n = l.name.ToLowerInvariant();
        return n.Contains("lantern") || n.Contains("lamp") || n.Contains("torch") || n.Contains("фонар");
    }

    // ---------------------------------------------------------------- тест вечера (ПКМ по заголовку компонента, в Play Mode)
    // проверка огней у построек: солнце низко, света и ambient меньше, огни из Scripts/Lights сами становятся ярче
    [ContextMenu("Тест: вечер (опустить солнце)")]
    void TestEvening()
    {
        sunPitch = 3f;
        sunIntensity = 0.25f;
        ambientScale = 0.3f;
        Apply();
    }

    [ContextMenu("Тест: вернуть день")]
    void TestDay()
    {
        sunPitch = 38f;
        sunIntensity = 1.25f;
        ambientScale = 1f;
        Apply();
    }

    // без этого Volume не сработает; обычно CameraVibeSetup уже включил
    static void EnsurePostProcessing()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        var data = cam.GetUniversalAdditionalCameraData();
        if (data != null && !data.renderPostProcessing) data.renderPostProcessing = true;
    }
}
