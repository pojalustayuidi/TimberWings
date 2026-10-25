// Assets/_Project/Scripts/Core/DayNightCycle.cs (НЕ в Editor)
// Смена суток. Создаётся сам после загрузки сцены с игроком (как LightingRig), в сцену ничего вешать не нужно.
//
//  Время: time01 0..1 (0 = полночь, 0.25 = раннее утро, 0.5 = полдень, 0.75 = вечер). Хранит TimeOfDaySaveData (ключ «time»).
//  Длина суток: GameBalance.DayLengthMinutes реальных минут, множитель из настройки «Скорость суток» (GameSettings.DaySpeed):
//      Обычная = 1, Медленная = 0.5, Фиксированный день = время плавно уходит к полудню и стоит там.
//  Пауза: при Time.timeScale == 0 часы стоят. Диалоги часы НЕ останавливают.
//  Фазы: Рассвет / День / Закат / Ночь (границы в GameBalance.TimeDawnStart ... TimeNightStart).
//
//  Что крутит каждый кадр (всё считается из ОДНОЙ высоты солнца, поэтому скачков нет):
//    • Солнце: поворот (тени идут по кругу), цвет и сила по высоте. Ниже горизонта солнце выключено.
//    • Луна: вторая слабая холодная Directional. Ночью она становится главным светом и даёт мягкие тени.
//      RenderSettings.sun всегда остаётся СОЛНЦЕМ: FireLightPool и ForestAtmosphere считают по нему «ночь» (фонари, окна, пятна леса).
//    • Ambient (Trilight), цвет и плотность тумана, цвет фона камеры (чтобы за краем карты не было шва).
//    • Три глобальных Volume: «DayNight Dawn / Dusk / Night» (цветокоррекция). Приоритеты 16 / 17 / 18: выше «Island Light» (15),
//      ниже лесного «Vibe Forest» (20), так что лес по-прежнему блендится поверх. Днём их вес 0 и картинка как раньше.
//
//  Другие скрипты:
//    DayNightCycle.Phase / Time01 / Night01 / Twilight01  статический доступ (работает и без объекта в сцене)
//    GameEvents.TimeOfDayChanged(DayPhase)                сменилась фаза
//    DayNightCycle.Instance.WarpTo(время, секунды)        быстро домотать время вперёд (сон у костра, переход в «Фиксированный день»)
//
//  LightingRig: если он есть, DayNightCycle выставляет ему dayNightDriven = true. Тогда LightingRig не трогает цвет и поворот солнца,
//  ambient и туман (тени и Volume «Island Light» остаются за ним). Если LightingRig нет, солнце, туман и ambient создаются здесь сами.
//
//  Дополнительно создаёт: NightBonus (серебряное дерево раз за ночь) и CampSleep (сон у костра).
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public enum DayPhase { Dawn = 0, Day = 1, Dusk = 2, Night = 3 }

[DefaultExecutionOrder(-100)]   // раньше ForestLook: тот считает наш туман «базой» и подмешивает свой оттенок леса
public class DayNightCycle : MonoBehaviour
{
    public static DayNightCycle Instance { get; private set; }

    // ------------------------------------------------------------------ статический доступ
    public static float Time01
    {
        get { return TimeOfDaySaveData.Instance != null ? TimeOfDaySaveData.Instance.time01 : GameBalance.TimeOfDayDefault; }
    }

    public static DayPhase Phase { get { return PhaseAt(Time01); } }

    // 0 день ... 1 глубокая ночь, плавно (по высоте солнца)
    public static float Night01
    {
        get { float d, n, w; Weights(SunElevation(Time01), out d, out n, out w); return n; }
    }

    // 0..1: насколько сейчас сумерки (солнце у горизонта). Нужен для тёплого оттенка HUD
    public static float Twilight01
    {
        get { float d, n, w; Weights(SunElevation(Time01), out d, out n, out w); return w; }
    }

    // множитель скорости, который выставляют другие скрипты (CampSleep: ночью у костра). Сюда пишут 1 или больше, DayNightCycle разгоняется плавно
    public static float ExternalSpeed = 1f;

    // отладка (окно Tools > BuilderSim > Время суток и клавиши DayNightDebug): 0 = часы стоят, 1 = как есть, 60 = минута сутки за ~11 с
    public static float DebugSpeed = 1f;
    public static float DebugBoost = 1f;   // временный множитель (F7 зажата)

    public static int DayNumber
    {
        get { return TimeOfDaySaveData.Instance != null ? Mathf.Max(1, TimeOfDaySaveData.Instance.day) : 1; }
    }

    public static string PhaseName(DayPhase p)
    {
        switch (p)
        {
            case DayPhase.Dawn: return "Рассвет";
            case DayPhase.Dusk: return "Закат";
            case DayPhase.Night: return "Ночь";
            default: return "";   // днём подпись не нужна: «День 3» уже говорит всё
        }
    }

    public static bool Warping { get { return Instance != null && Instance.warping; } }

    // направления НА солнце и НА луну (единичные векторы, в мире). Для SkyDome: диск на небе стоит там же, откуда идёт свет и тени.
    // Луна здесь настоящая (может быть под горизонтом), а не зажатая moonMinPitch, как у света
    public bool GetSkyDirections(out Vector3 toSun, out Vector3 toMoon)
    {
        float e = SunElevation(t);
        float yaw = sunYawNoon + DeltaDay(t, 0.5f) * sunYawPerDay;
        toSun = -(Quaternion.Euler(e, yaw, 0f) * Vector3.forward);

        float em = MoonElevation(t);
        float my = moonYawMidnight + DeltaDay(t, 0f) * sunYawPerDay;
        toMoon = -(Quaternion.Euler(em, my, 0f) * Vector3.forward);
        return true;
    }

    // оттенок для HUD и миникарты: чуть теплее на закате, чуть темнее и холоднее ночью. Умножается на исходный цвет элемента
    public static Color HudTint
    {
        get
        {
            float d, n, w;
            Weights(SunElevation(Time01), out d, out n, out w);
            Color c = Color.Lerp(Color.white, new Color(1f, 0.90f, 0.84f), w * 0.6f);
            return Color.Lerp(c, new Color(0.62f, 0.69f, 0.86f), n);
        }
    }

    public static DayPhase PhaseAt(float t)
    {
        t = Mathf.Repeat(t, 1f);
        if (t >= GameBalance.TimeNightStart || t < GameBalance.TimeDawnStart) return DayPhase.Night;
        if (t < GameBalance.TimeDayStart) return DayPhase.Dawn;
        if (t < GameBalance.TimeDuskStart) return DayPhase.Day;
        return DayPhase.Dusk;
    }

    // высота солнца над горизонтом, градусы (ниже нуля = под горизонтом). Круговая орбита с подъёмом: день длиннее ночи
    const float SunMaxElev = 55f;
    const float OrbitOffset = 0.25f;   // 0 = день и ночь поровну; больше = день длиннее
    public static float SunElevation(float t)
    {
        float s = Mathf.Sin(2f * Mathf.PI * (t - 0.25f));
        return SunMaxElev * (s + OrbitOffset) / (1f + OrbitOffset);
    }

    // луна ходит на пол-суток позже солнца (в полночь она выше всего)
    public static float MoonElevation(float t)
    {
        float s = Mathf.Sin(2f * Mathf.PI * (t - 0.75f));
        return 50f * (s + OrbitOffset) / (1f + OrbitOffset);
    }

    // веса по высоте солнца: день / ночь / сумерки. Сумма примерно 1
    static void Weights(float elev, out float day, out float night, out float twi)
    {
        day = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-4f, 22f, elev));
        night = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-17f, -3f, elev));
        twi = Mathf.Clamp01(1f - day - night);
    }

    // разность времени в сутках, -0.5..0.5
    static float DeltaDay(float t, float from)
    {
        return Mathf.Repeat(t - from + 0.5f, 1f) - 0.5f;
    }

    // ------------------------------------------------------------------ настройки (Inspector)
    [Header("Солнце")]
    public float sunMaxIntensity = 1.25f;                 // как у LightingRig.sunIntensity
    [Tooltip("Куда светит солнце в полдень (yaw). У LightingRig было -20: свет чуть сбоку даёт объём при камере yaw 45")]
    public float sunYawNoon = -20f;
    [Tooltip("Насколько солнце поворачивается за сутки, градусов. 200 = за день тени уходят примерно на ±58°. Больше = тени бегают заметнее, но утром свет бьёт в камеру")]
    public float sunYawPerDay = 200f;

    [Header("Луна")]
    public Color moonColor = new Color(0.60f, 0.70f, 1.00f);
    public float moonIntensity = 0.65f;                   // ночью деревья и дома должны читаться: если темно, подними
    public float moonYawMidnight = -35f;                  // луна светит из-за камеры чуть слева: объекты спереди освещены
    public float moonMinPitch = 14f;                      // луна не опускается ниже: иначе тени вытянутся на весь остров
    [Range(0f, 1f)] public float moonShadowStrength = 0.55f;

    [Header("Ambient (Trilight): день / сумерки (утро) / сумерки (вечер) / ночь")]
    public Color ambDaySky = new Color(0.55f, 0.71f, 0.89f);
    public Color ambDayEq = new Color(0.91f, 0.78f, 0.60f);
    public Color ambDayGround = new Color(0.37f, 0.35f, 0.23f);
    public Color ambDawnSky = new Color(0.62f, 0.58f, 0.74f);
    public Color ambDawnEq = new Color(0.88f, 0.62f, 0.50f);
    public Color ambDawnGround = new Color(0.32f, 0.28f, 0.26f);
    public Color ambDuskSky = new Color(0.55f, 0.48f, 0.68f);
    public Color ambDuskEq = new Color(0.92f, 0.55f, 0.38f);
    public Color ambDuskGround = new Color(0.30f, 0.25f, 0.22f);
    public Color ambNightSky = new Color(0.26f, 0.34f, 0.58f);
    public Color ambNightEq = new Color(0.20f, 0.26f, 0.43f);
    public Color ambNightGround = new Color(0.11f, 0.13f, 0.20f);

    [Header("Туман и фон камеры")]
    public Color fogDay = new Color(0.89f, 0.82f, 0.71f);
    public Color fogDawn = new Color(0.90f, 0.70f, 0.62f);
    public Color fogDusk = new Color(0.86f, 0.58f, 0.46f);
    public Color fogNight = new Color(0.08f, 0.12f, 0.24f);
    public float fogDensityDay = 0.010f;
    public float fogDensityNight = 0.009f;

    [Header("Цветокоррекция (Volume)")]
    public float volumeDawnPriority = 16f;
    public float volumeDuskPriority = 17f;
    public float volumeNightPriority = 18f;

    // ------------------------------------------------------------------ состояние
    float t;                          // текущее время суток 0..1
    float extMul = 1f;                // сглаженный ExternalSpeed
    DayPhase lastPhase;
    bool bound, started;
    float bindTimer;

    Light sun, moon;
    bool sunCreated;
    LightingRig rig;
    Camera cam;
    float camAt;

    Volume dawnVol, duskVol, nightVol;
    VolumeProfile dawnProf, duskProf, nightProf;
    ColorAdjustments dawnCA, duskCA, nightCA;   // Cloud Candy: значения грейда берём из CloudCandyStyle и обновляем при его правке
    int gradeRev = -1;
    bool gradeStyled;

    // перемотка вперёд (сон, «Фиксированный день»)
    bool warping;
    float warpFrom, warpDelta, warpT, warpDur;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { Instance = null; ExternalSpeed = 1f; DebugSpeed = 1f; DebugBoost = 1f; }

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
        if (Object.FindFirstObjectByType<DayNightCycle>() != null) return;
        new GameObject("_DayNightCycle").AddComponent<DayNightCycle>();
    }

    // ------------------------------------------------------------------ жизнь
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        SnapFromSave();
        lastPhase = PhaseAt(t);
    }

    void OnEnable()
    {
        GameSettings.Changed += OnSettings;
        SaveSystem.Loaded += OnSaveLoaded;
        SaveSystem.NewGameStarted += OnSaveLoaded;
    }

    void OnDisable()
    {
        GameSettings.Changed -= OnSettings;
        SaveSystem.Loaded -= OnSaveLoaded;
        SaveSystem.NewGameStarted -= OnSaveLoaded;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (rig != null) rig.dayNightDriven = false;
        if (sunCreated && sun != null) Destroy(sun.gameObject);
        if (dawnProf != null) Destroy(dawnProf);
        if (duskProf != null) Destroy(duskProf);
        if (nightProf != null) Destroy(nightProf);
    }

    void Start()
    {
        started = true;
        Bind();
    }

    // время берём из сохранения (или по умолчанию с утра). «Фиксированный день»: сразу полдень, без перемотки
    void SnapFromSave()
    {
        var sd = TimeOfDaySaveData.Instance;
        t = Mathf.Repeat(sd != null ? sd.time01 : GameBalance.TimeOfDayDefault, 1f);
        if (GameSettings.DayFrozen) t = GameSettings.FixedDayTime;
        warping = false;
        if (sd != null) sd.time01 = t;
    }

    // загрузили сохранение или начали заново: время встаёт сразу
    void OnSaveLoaded()
    {
        SnapFromSave();
        lastPhase = PhaseAt(t);
    }

    void OnSettings()
    {
        // включили «Фиксированный день»: время плавно домотается до полудня
        if (GameSettings.DayFrozen && !warping && Mathf.Abs(DeltaDay(t, GameSettings.FixedDayTime)) > 0.002f)
            WarpTo(GameSettings.FixedDayTime, 3f);
    }

    // поставить время сразу (тесты, окно Editor). Фаза, счётчик ночей и события отрабатывают как обычно
    public void SetTime(float t01)
    {
        warping = false;
        t = Mathf.Repeat(t01, 1f);
        StepPhase();
        Store();
        ApplyLook();
    }

    // ------------------------------------------------------------------ перемотка
    public void WarpTo(float target, float seconds)
    {
        target = Mathf.Repeat(target, 1f);
        warpFrom = t;
        warpDelta = Mathf.Repeat(target - t, 1f);   // только вперёд: время назад не идёт
        if (warpDelta > 0.998f) warpDelta = 0f;     // уже на месте (разница в пределах ошибки float): не крутим целые сутки
        warpT = 0f;
        warpDur = Mathf.Max(0.05f, seconds);
        warping = warpDelta > 0.0005f;
    }

    // сразу доводит перемотку до конца (пропуск сна клавишей)
    public void FinishWarp()
    {
        if (!warping) return;
        warping = false;
        t = Mathf.Repeat(warpFrom + warpDelta, 1f);
        StepPhase();
        Store();
    }

    // ------------------------------------------------------------------ привязка к свету
    void Bind()
    {
        if (bound) return;

        rig = LightingRig.Instance != null ? LightingRig.Instance : FindFirstObjectByType<LightingRig>();
        if (rig != null) rig.dayNightDriven = true;

        sun = RenderSettings.sun;
        if (sun == null || sun.type != LightType.Directional)
        {
            sun = null;
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type != LightType.Directional) continue;
                if (sun == null || l.intensity > sun.intensity) sun = l;
            }
        }
        if (sun == null)
        {
            // LightingRig'а нет, света в сцене нет: минимальный Directional + RenderSettings
            var go = new GameObject("Sun (DayNight)");
            sun = go.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.8f;
            sunCreated = true;
        }
        RenderSettings.sun = sun;

        var mg = new GameObject("Moon (DayNight)");
        mg.transform.SetParent(transform, false);
        moon = mg.AddComponent<Light>();
        moon.type = LightType.Directional;
        moon.color = moonColor;
        moon.intensity = 0f;
        moon.shadows = LightShadows.Soft;
        moon.shadowStrength = 0f;
        moon.enabled = false;

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;

        BuildVolumes();

        // помощники: серебряное дерево и сон у костра
        if (GetComponent<NightBonus>() == null) gameObject.AddComponent<NightBonus>();
        if (GetComponent<CampSleep>() == null) gameObject.AddComponent<CampSleep>();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (GetComponent<DayNightDebug>() == null) gameObject.AddComponent<DayNightDebug>();   // F6/F7/F8/F9, только в редакторе и Development Build
#endif

        bound = true;
        ApplyLook();
    }

    void BuildVolumes()
    {
        dawnVol = MakeVolume("DayNight Dawn", volumeDawnPriority, out dawnProf);
        duskVol = MakeVolume("DayNight Dusk", volumeDuskPriority, out duskProf);
        nightVol = MakeVolume("DayNight Night", volumeNightPriority, out nightProf);

        dawnCA = dawnProf.Add<ColorAdjustments>(false);
        duskCA = duskProf.Add<ColorAdjustments>(false);
        nightCA = nightProf.Add<ColorAdjustments>(false);
        gradeRev = -1;
        ApplyGrades(CloudCandyStyle.Current);
    }

    // Значения грейда суток. Со стилем Cloud Candy берутся из него, без стиля остаются прежними (откат).
    void ApplyGrades(CloudCandyStyle st)
    {
        if (dawnCA == null || duskCA == null || nightCA == null) return;
        if (st != null)
        {
            SetGrade(dawnCA, st.gradeDawn.postExposure, st.gradeDawn.contrast, st.gradeDawn.saturation, st.gradeDawn.filter);
            SetGrade(duskCA, st.gradeDusk.postExposure, st.gradeDusk.contrast, st.gradeDusk.saturation, st.gradeDusk.filter);
            SetGrade(nightCA, st.gradeNight.postExposure, st.gradeNight.contrast, st.gradeNight.saturation, st.gradeNight.filter);
        }
        else
        {
            // рассвет: чуть розовее и мягче
            SetGrade(dawnCA, 0.08f, 8f, 12f, new Color(1f, 0.90f, 0.88f));
            // закат: тёплый, насыщенный
            SetGrade(duskCA, 0.05f, 10f, 20f, new Color(1f, 0.86f, 0.76f));
            // ночь: синий фильтр, выдержку приподнимаем, чтобы деревья и дома читались
            SetGrade(nightCA, 0.30f, 8f, -8f, new Color(0.72f, 0.82f, 1f));
        }
        gradeStyled = st != null;
        gradeRev = CloudCandyStyle.Revision;
    }

    static void SetGrade(ColorAdjustments ca, float exposure, float contrast, float saturation, Color filter)
    {
        ca.postExposure.Override(exposure);
        ca.contrast.Override(contrast);
        ca.saturation.Override(saturation);
        ca.colorFilter.Override(filter);
    }

    Volume MakeVolume(string volumeName, float priority, out VolumeProfile profile)
    {
        var go = new GameObject(volumeName);
        go.transform.SetParent(transform, false);
        var v = go.AddComponent<Volume>();
        v.isGlobal = true;
        v.priority = priority;
        v.weight = 0f;
        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = volumeName;
        v.sharedProfile = profile;
        return v;
    }

    // ------------------------------------------------------------------ кадр
    void Update()
    {
        if (!started) return;
        if (!bound) Bind();

        if (rig == null)
        {
            // LightingRig мог появиться позже нас
            rig = LightingRig.Instance;
            if (rig != null) rig.dayNightDriven = true;
        }

        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        bool running = Time.timeScale > 0f && !SaveSystem.IsLoading;   // timeScale 0 (пауза): часы стоят. Диалоги часы не останавливают

        if (running)
        {
            if (warping)
            {
                warpT += dt;
                float u = Mathf.Clamp01(warpT / warpDur);
                float e = u * u * (3f - 2f * u);
                t = Mathf.Repeat(warpFrom + warpDelta * e, 1f);
                if (u >= 1f) warping = false;
            }
            else if (!GameSettings.DayFrozen)
            {
                extMul = Mathf.MoveTowards(extMul, Mathf.Max(1f, ExternalSpeed), dt * 6f);   // разгон и торможение без рывка
                float speed = GameSettings.DaySpeedMultiplier * extMul * DebugSpeed * DebugBoost / Mathf.Max(1f, GameBalance.DayLengthMinutes * 60f);
                t = Mathf.Repeat(t + dt * speed, 1f);
            }
            StepPhase();
            Store();
        }

        ApplyLook();
    }

    void Store()
    {
        var sd = TimeOfDaySaveData.Instance;
        if (sd != null) sd.time01 = t;
    }

    // сменилась фаза: считаем ночи, шлём событие, просим сохранить время
    void StepPhase()
    {
        DayPhase p = PhaseAt(t);
        if (p == lastPhase) return;

        var sd = TimeOfDaySaveData.Instance;
        if (p == DayPhase.Night && sd != null) sd.nights++;
        if (p == DayPhase.Dawn && lastPhase == DayPhase.Night && sd != null) sd.day++;   // новый день: только после ночи, ползунок и тесты не накручивают
        lastPhase = p;
        GameEvents.RaiseTimeOfDayChanged(p);
        SaveSystem.RequestSave("time");
    }

    // ------------------------------------------------------------------ внешний вид
    void ApplyLook()
    {
        if (sun == null || moon == null) return;

        // Cloud Candy: цвета и числа из ассета стиля (нет ассета или он выключен = прежние значения полей)
        CloudCandyStyle st = CloudCandyStyle.Current;
        if (st != null) st.ApplyTo(this);
        if (gradeRev != CloudCandyStyle.Revision || gradeStyled != (st != null)) ApplyGrades(st);

        float e = SunElevation(t);
        float wd, wn, wt;
        Weights(e, out wd, out wn, out wt);
        float dusk = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.4f, 0.6f, t));   // 0 утро ... 1 вечер

        // --- солнце: ниже горизонта выключено, тогда главным светом (и источником теней) становится луна
        float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-1.5f, 16f, e));
        float sunI = sunMaxIntensity * k;
        sun.intensity = sunI;
        sun.color = SunColor(e, st);
        if (st != null) sun.shadowStrength = st.sunShadowStrength;
        bool sunOn = sunI > 0.005f;
        if (sun.enabled != sunOn) sun.enabled = sunOn;
        float yaw = sunYawNoon + DeltaDay(t, 0.5f) * sunYawPerDay;
        sun.transform.rotation = Quaternion.Euler(e, yaw, 0f);   // ниже горизонта pitch отрицательный: forward.y > 0, FireLightPool так и читает «ночь»

        // --- луна
        float em = MoonElevation(t);
        float mk = wn * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-2f, 10f, em));
        moon.color = moonColor;
        moon.intensity = moonIntensity * mk;
        bool moonOn = mk > 0.005f;
        if (moon.enabled != moonOn) moon.enabled = moonOn;
        LightShadows ms = sun.shadows != LightShadows.None ? sun.shadows : LightShadows.Hard;   // качество теней то же, что у солнца (его ставит LightingRig)
        if (moon.shadows != ms) moon.shadows = ms;
        moon.shadowStrength = moonShadowStrength * mk;
        float moonYaw = moonYawMidnight + DeltaDay(t, 0f) * sunYawPerDay;
        moon.transform.rotation = Quaternion.Euler(Mathf.Max(em, moonMinPitch), moonYaw, 0f);

        // --- ambient
        Color skyTw = Color.Lerp(ambDawnSky, ambDuskSky, dusk);
        Color eqTw = Color.Lerp(ambDawnEq, ambDuskEq, dusk);
        Color grTw = Color.Lerp(ambDawnGround, ambDuskGround, dusk);
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = Mix(ambDaySky, skyTw, ambNightSky, wd, wt, wn);
        RenderSettings.ambientEquatorColor = Mix(ambDayEq, eqTw, ambNightEq, wd, wt, wn);
        RenderSettings.ambientGroundColor = Mix(ambDayGround, grTw, ambNightGround, wd, wt, wn);

        // --- туман и фон камеры
        Color fogTw = Color.Lerp(fogDawn, fogDusk, dusk);
        Color fog = Mix(fogDay, fogTw, fogNight, wd, wt, wn);
        RenderSettings.fog = true;
        RenderSettings.fogMode = st != null ? st.fogMode : FogMode.ExponentialSquared;
        RenderSettings.fogColor = fog;
        RenderSettings.fogDensity = Mathf.Lerp(fogDensityDay, fogDensityNight, wn);

        if (Time.unscaledTime >= camAt || cam == null)
        {
            camAt = Time.unscaledTime + 1f;
            cam = Camera.main;
        }
        if (cam != null && cam.clearFlags == CameraClearFlags.SolidColor) cam.backgroundColor = fog;

        // --- цветокоррекция
        if (dawnVol != null) dawnVol.weight = wt * (1f - dusk);
        if (duskVol != null) duskVol.weight = wt * dusk;
        if (nightVol != null) nightVol.weight = wn;
    }

    // цвет солнца по высоте: красноватое у горизонта, тёплое золото днём (как «золотой час» LightingRig)
    static Color SunColor(float e, CloudCandyStyle st)
    {
        if (st != null) return st.SunColorAt(e);
        Color low = new Color(1.00f, 0.45f, 0.25f);   // у горизонта
        Color mid = new Color(1.00f, 0.72f, 0.45f);   // ~8°
        Color gold = new Color(1.00f, 0.843f, 0.627f);   // ~20°, #FFD7A0
        Color high = new Color(1.00f, 0.90f, 0.74f);  // полдень
        if (e <= 0f) return low;
        if (e < 8f) return Color.Lerp(low, mid, Mathf.SmoothStep(0f, 1f, e / 8f));
        if (e < 20f) return Color.Lerp(mid, gold, Mathf.SmoothStep(0f, 1f, (e - 8f) / 12f));
        return Color.Lerp(gold, high, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(20f, 50f, e)));
    }

    internal static Color Mix(Color day, Color twi, Color night, float wd, float wt, float wn)
    {
        float s = wd + wt + wn;
        if (s < 0.0001f) return day;
        Color c = (day * wd + twi * wt + night * wn) / s;
        c.a = 1f;
        return c;
    }

    // ------------------------------------------------------------------ тест (ПКМ по заголовку компонента, в Play Mode)
    [ContextMenu("Тест: полдень")] void TestNoon() { WarpTo(0.5f, 2f); }
    [ContextMenu("Тест: закат")] void TestDusk() { WarpTo(0.78f, 2f); }
    [ContextMenu("Тест: полночь")] void TestMidnight() { WarpTo(0f, 2f); }
    [ContextMenu("Тест: рассвет")] void TestDawn() { WarpTo(0.22f, 2f); }
}
