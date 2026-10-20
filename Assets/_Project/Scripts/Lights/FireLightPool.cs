// Assets/_Project/Scripts/Lights/FireLightPool.cs (НЕ в Editor)
// Бюджет настоящего света. Создаётся сам после загрузки сцены с игроком (как LightingRig), в сцену ничего вешать не нужно.
//
//  • Настоящий Light получают только ближайшие к игроку maxRealLights (3) источника FireSource в пределах maxRealDistance.
//    Остальные горят имитацией (emissive + glow), её ведёт сам FireSource.
//  • Lights берутся из пула на PoolSize = 4 штуки: 3 горят + 1 догорает/разгорается при смене ближайших. Больше 4 настоящих
//    источников от нас одновременно не бывает. Передача плавная: intensity растёт/гаснет, у края дальности свет тоже тает.
//  • Лимит Additional Lights в URP: размер пула и число настоящих ограничиваются «Per Object Limit» ассета;
//    если Additional Lights выключены (Disabled), настоящих нет вовсе, остаётся имитация.
//  • Качество (GameSettings.LightQuality, событие Changed): Низкое = не больше 2 настоящих и без теней,
//    Среднее = 3 и жёсткая тень у самого ближнего, Высокое = 3 и мягкая тень у самого ближнего (если тени доп. света включены в URP).
//  • Время суток: «ночь» берётся из солнца (RenderSettings.sun из LightingRig): чем ниже солнце, тем ярче огни. Днём они приглушены.
//
// Тест вечера: ПКМ по LightingRig -> «Тест: вечер», либо на этом объекте (_FireLights) включи Force Night.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// метка: Light из пула. LightingRig не трогает их тени, ими управляет только пул
public class FirePoolLight : MonoBehaviour { }

public class FireLightPool : MonoBehaviour
{
    public static FireLightPool Instance { get; private set; }
    public static readonly List<FireSource> Sources = new List<FireSource>(32);

    public const int PoolSize = 4;                  // жёсткий потолок настоящих Light от пула

    [Header("Бюджет")]
    [Range(0, 3)] public int maxRealLights = 3;
    public float maxRealDistance = 16f;             // дальше этого настоящий свет не нужен
    public float fadeBand = 4f;                     // у края дальности свет тает на этой ширине
    public float fadeIn = 0.45f;                    // секунд на разгорание настоящего света
    public float fadeOut = 0.3f;                    // секунд на угасание
    public float reselectInterval = 0.15f;
    public float hysteresis = 1.5f;                 // метров бонуса тому, кто уже светит (чтобы не дёргались на равных)

    [Header("Время суток")]
    [Tooltip("Солнце выше этого sin(высоты) = полный день")]
    public float fullDaySunElevation = 0.45f;
    public float noSunNight = 0.35f;                // если солнца нет в сцене

    [Header("Тест")]
    public bool forceNight;
    [Range(0f, 1f)] public float forceNightLevel = 1f;

    // состояние
    readonly List<Light> free = new List<Light>(PoolSize);
    readonly List<Light> all = new List<Light>(PoolSize);
    readonly List<FireSource> candidates = new List<FireSource>(32);
    static readonly System.Comparison<FireSource> ByKey = (a, b) => a.sortKey.CompareTo(b.sortKey);

    int poolSize = PoolSize;
    int realCap = 3;
    LightShadows rank0Shadows = LightShadows.None;
    Transform player;
    float nextSelect, nextPlayerFind;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Instance = null; Sources.Clear(); }

    // ---------------------------------------------------------------- жизнь
    void Awake()
    {
        Instance = this;
        poolSize = PoolSize;
        for (int i = 0; i < PoolSize; i++)
        {
            var go = new GameObject("FireLight_" + i);
            go.transform.SetParent(transform, false);
            go.AddComponent<FirePoolLight>();
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.shadows = LightShadows.None;
            l.intensity = 0f;
            l.enabled = false;
            all.Add(l);
            free.Add(l);
        }
    }

    void OnEnable()
    {
        GameSettings.Changed += ApplyQuality;
        ApplyQuality();
    }

    void OnDisable() { GameSettings.Changed -= ApplyQuality; }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start() { StartCoroutine(ScanRoutine()); }

    // стройки и их свет собираем сразу в Start (до первого Update: мерцание и TentStages ещё не тронули яркость Light),
    // плюс ещё раз позже, на случай объектов, появившихся в чужом Start
    IEnumerator ScanRoutine()
    {
        FireLightScan.Run();
        yield return new WaitForSecondsRealtime(1.5f);
        FireLightScan.Run();
    }

    // ---------------------------------------------------------------- регистрация источников
    internal static void Register(FireSource s)
    {
        if (!Sources.Contains(s)) Sources.Add(s);
    }

    internal static void Unregister(FireSource s)
    {
        Sources.Remove(s);
        if (Instance != null) Instance.Release(s);
        s.wantReal = false;
        s.realWeight = 0f;
    }

    // ---------------------------------------------------------------- качество и лимит URP
    void ApplyQuality()
    {
        LightQualityLevel q = GameSettings.LightQuality;
        int cap = Mathf.Min(maxRealLights, q == LightQualityLevel.Low ? 2 : 3);
        rank0Shadows = q == LightQualityLevel.High ? LightShadows.Soft : q == LightQualityLevel.Medium ? LightShadows.Hard : LightShadows.None;

        // лимит Additional Lights в URP
        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp != null)
        {
            if (urp.additionalLightsRenderingMode == LightRenderingMode.Disabled)
            {
                cap = 0;
            }
            else
            {
                int limit = Mathf.Clamp(urp.maxAdditionalLightsCount, 1, PoolSize);
                poolSize = limit;
                // нужен запас в один Light, чтобы передавать огонь без «дыры»; при лимите 1 передаём по очереди
                cap = Mathf.Min(cap, limit > 1 ? limit - 1 : 1);
            }
        }
        else poolSize = PoolSize;

        realCap = Mathf.Max(0, cap);
    }

    // сколько Light пула сейчас можно занять (при лимите URP меньше 4 лишние держим свободными и выключенными)
    int Busy { get { return all.Count - free.Count; } }

    Light Acquire()
    {
        if (Busy >= poolSize || free.Count == 0) return null;
        var l = free[free.Count - 1];
        free.RemoveAt(free.Count - 1);
        l.intensity = 0f;
        l.enabled = true;
        return l;
    }

    void Release(FireSource s)
    {
        var l = s.poolLight;
        if (l == null) { s.poolLight = null; return; }
        s.poolLight = null;
        s.realWeight = 0f;
        if (l == null) return;
        l.intensity = 0f;
        l.shadows = LightShadows.None;
        l.enabled = false;
        if (!free.Contains(l)) free.Add(l);
    }

    // ---------------------------------------------------------------- время суток
    float Night()
    {
        if (forceNight) return forceNightLevel;
        Light sun = RenderSettings.sun;
        if (sun == null) return noSunNight;
        float elev = -sun.transform.forward.y;   // sin высоты солнца над горизонтом
        float day = Mathf.Clamp01(Mathf.InverseLerp(0.02f, Mathf.Max(0.1f, fullDaySunElevation), elev)) * Mathf.Clamp01(sun.intensity / 0.6f);
        return 1f - day;
    }

    // ---------------------------------------------------------------- игрок
    Vector3 PlayerPos()
    {
        if (player == null && Time.unscaledTime >= nextPlayerFind)
        {
            nextPlayerFind = Time.unscaledTime + 2f;
            player = LocationAmbient.Player;
            if (player == null)
            {
                var pm = PlayerMovement.Instance;
                if (pm != null) player = pm.transform;
            }
        }
        if (player != null) return player.position;
        Camera cam = Camera.main;
        return cam != null ? cam.transform.position : Vector3.zero;
    }

    // ---------------------------------------------------------------- кадр
    void Update()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        float t = Time.time;
        float night = Night();
        Vector3 pp = PlayerPos();

        if (Time.unscaledTime >= nextSelect)
        {
            nextSelect = Time.unscaledTime + Mathf.Max(0.05f, reselectInterval);
            Reselect(pp);
        }

        Camera cam = Camera.main;
        Quaternion camRot = cam != null ? cam.transform.rotation : Quaternion.identity;

        for (int i = 0; i < Sources.Count; i++)
        {
            var s = Sources[i];
            if (s == null) continue;
            float lum = s.Tick(dt, t, night, camRot);
            UpdateReal(s, lum, pp, dt);
        }
    }

    // кто из огней должен получить настоящий свет: ближайшие realCap в пределах maxRealDistance
    void Reselect(Vector3 pp)
    {
        candidates.Clear();
        for (int i = 0; i < Sources.Count; i++)
        {
            var s = Sources[i];
            if (s == null) continue;
            s.wantReal = false;
            if (s.simOnly || s.target < 0.5f) continue;
            float d = (s.WorldPos - pp).magnitude;
            s.sortKey = d - (s.poolLight != null ? hysteresis : 0f);
            candidates.Add(s);
        }
        candidates.Sort(ByKey);

        int n = 0;
        for (int i = 0; i < candidates.Count && n < realCap; i++)
        {
            var c = candidates[i];
            if (c.sortKey > maxRealDistance) break;
            c.wantReal = true;
            c.realRank = n;
            n++;
        }
    }

    void UpdateReal(FireSource s, float lum, Vector3 pp, float dt)
    {
        float targetW = 0f;
        if (realCap > 0 && s.wantReal && !s.simOnly && s.level > 0.01f)
        {
            float d = (s.WorldPos - pp).magnitude;
            targetW = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(maxRealDistance - fadeBand, maxRealDistance, d));
        }

        if (s.poolLight == null)
        {
            if (targetW <= 0.001f) return;
            s.poolLight = Acquire();
            if (s.poolLight == null) return;   // пул занят догорающими: подождём несколько кадров
        }

        float rate = targetW > s.realWeight ? 1f / Mathf.Max(0.05f, fadeIn) : 1f / Mathf.Max(0.05f, fadeOut);
        s.realWeight = Mathf.MoveTowards(s.realWeight, targetW, dt * rate);

        if (s.realWeight <= 0.001f && targetW <= 0.001f)
        {
            Release(s);
            return;
        }

        var l = s.poolLight;
        l.transform.position = s.LightWorldPos;
        l.color = s.color;
        l.range = s.range;
        l.intensity = s.intensity * lum * s.realWeight;

        // тень только у самого ближнего и только на Среднем/Высоком
        LightShadows want = (s.realRank == 0 && s.wantReal) ? rank0Shadows : LightShadows.None;
        if (l.shadows != want) l.shadows = want;
        if (want != LightShadows.None) l.shadowStrength = 0.8f * s.realWeight;
    }

    // ---------------------------------------------------------------- тест
    [ContextMenu("Тест: ночь для огней")] void TestNight() { forceNight = true; forceNightLevel = 1f; }
    [ContextMenu("Тест: вернуть время суток по солнцу")] void TestAuto() { forceNight = false; }

    // сколько настоящих Light от пула сейчас включено (для отладки: должно быть не больше 4)
    public int ActiveRealLights
    {
        get
        {
            int n = 0;
            for (int i = 0; i < all.Count; i++) if (all[i] != null && all[i].enabled) n++;
            return n;
        }
    }
}

public static class FireLightBootstrap
{
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
        if (FireLightPool.Instance != null) return;
        new GameObject("_FireLights").AddComponent<FireLightPool>();
    }
}
