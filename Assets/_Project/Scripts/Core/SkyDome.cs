// Assets/_Project/Scripts/Core/SkyDome.cs (НЕ в Editor)
// Небо. Создаётся само после загрузки сцены с игроком (как LightingRig и DayNightCycle), в сцену ничего вешать не нужно.
//
// Что рисует (шейдер Project/SkyDome):
//   • градиент неба по времени суток (утро, день, закат, ночь), у горизонта дымка цвета тумана сцены (шва с краем карты нет);
//   • солнце с тёплым ореолом и луну с фазами (сменяются по дням) ровно там, откуда идут свет и тени (DayNightCycle.GetSkyDirections);
//   • звёзды ночью, мультяшные двухтонные облака, которые медленно плывут и розовеют на закате.
//
// Как работает: купол (внутренняя сфера) каждый кадр ставится в позицию главной камеры и рисуется Graphics.DrawMesh ТОЛЬКО для этой камеры
// (миникарта и прочие камеры неба не увидят). Шейдер идёт после непрозрачных объектов и там, где есть геометрия, не считается.
// В изометрии (pitch 50, FOV 35) неба в кадре почти нет, оно живёт в основном в свободной камере (OrbitCamera).
//
// Качество берётся из GameSettings.LightQuality: Низкое = 2 октавы облаков и без звёзд, Среднее = 3, Высокое = 4 и звёзды.
// Выключить небо: SkyDome.Instance.enabledSky = false (останется обычный фон камеры, как раньше).
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(200)]   // после камер (CameraFollow -100, OrbitCamera -90): купол берёт уже готовое положение камеры
public class SkyDome : MonoBehaviour
{
    public static SkyDome Instance { get; private set; }

    [Header("Общее")]
    public bool enabledSky = true;

    [Header("Облака")]
    [Range(0f, 1f)] public float cloudCover = 0.45f;     // доля неба под облаками (0 = ясно, 1 = пасмурно)
    public float cloudCoverWobble = 0.06f;               // облачность медленно «дышит» ± это число
    public float cloudSpeed = 0.012f;                    // скорость дрейфа
    public float cloudScale = 1.1f;                      // больше = мельче облака

    [Header("Звёзды и луна")]
    [Range(0f, 2f)] public float stars = 1f;
    public float moonSize = 0.045f;                      // угловой радиус, радианы (~2.6°)
    public int moonCycleDays = 8;                        // за сколько дней луна проходит фазы

    [Header("Цвета неба (зенит / горизонт): день, утро, закат, ночь")]
    public Color zenithDay = new Color(0.30f, 0.58f, 0.93f);
    public Color zenithDawn = new Color(0.40f, 0.46f, 0.80f);
    public Color zenithDusk = new Color(0.33f, 0.27f, 0.58f);
    public Color zenithNight = new Color(0.025f, 0.045f, 0.13f);
    public Color horizonDay = new Color(0.70f, 0.85f, 0.97f);
    public Color horizonDawn = new Color(0.95f, 0.72f, 0.70f);
    public Color horizonDusk = new Color(0.98f, 0.60f, 0.50f);
    public Color horizonNight = new Color(0.07f, 0.11f, 0.24f);

    Mesh mesh;
    Material mat;
    Camera cam;
    float camAt;

    static readonly int FogId = Shader.PropertyToID("_Fog");
    static readonly int HorizonId = Shader.PropertyToID("_Horizon");
    static readonly int ZenithId = Shader.PropertyToID("_Zenith");
    static readonly int SunDirId = Shader.PropertyToID("_SunDir");
    static readonly int SunColorId = Shader.PropertyToID("_SunColor");
    static readonly int MoonDirId = Shader.PropertyToID("_MoonDir");
    static readonly int WeightsId = Shader.PropertyToID("_Weights");
    static readonly int MoonId = Shader.PropertyToID("_Moon");
    static readonly int CloudId = Shader.PropertyToID("_Cloud");
    static readonly int StarsId = Shader.PropertyToID("_Stars");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { Instance = null; }

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
        if (Object.FindFirstObjectByType<SkyDome>() != null) return;
        new GameObject("_SkyDome").AddComponent<SkyDome>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        var sh = Shader.Find("Project/SkyDome");
        if (sh == null)
        {
            Debug.LogWarning("[SkyDome] Шейдер Project/SkyDome не найден (файл Shaders/Sky/SkyDome.shader). Небо выключено.");
            enabledSky = false;
            return;
        }
        mat = new Material(sh) { name = "SkyDome (runtime)", hideFlags = HideFlags.DontSave };
        mesh = BuildDome();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (mat != null) Destroy(mat);
        if (mesh != null) Destroy(mesh);
    }

    // внутренняя полусфера (от -25° до зенита): чуть ниже горизонта, чтобы не было щели над землёй
    static Mesh BuildDome()
    {
        const int rings = 28, segs = 40;
        const float latMin = -25f * Mathf.Deg2Rad, latMax = 90f * Mathf.Deg2Rad;
        var verts = new Vector3[(rings + 1) * (segs + 1)];
        for (int r = 0; r <= rings; r++)
        {
            float lat = Mathf.Lerp(latMin, latMax, r / (float)rings);
            float y = Mathf.Sin(lat), rr = Mathf.Cos(lat);
            for (int s = 0; s <= segs; s++)
            {
                float a = s / (float)segs * Mathf.PI * 2f;
                verts[r * (segs + 1) + s] = new Vector3(Mathf.Cos(a) * rr, y, Mathf.Sin(a) * rr);
            }
        }
        var tris = new int[rings * segs * 6];
        int k = 0;
        for (int r = 0; r < rings; r++)
            for (int s = 0; s < segs; s++)
            {
                int a = r * (segs + 1) + s, b = a + 1, c = a + segs + 1, d = c + 1;
                tris[k++] = a; tris[k++] = c; tris[k++] = b;
                tris[k++] = b; tris[k++] = c; tris[k++] = d;
            }
        var m = new Mesh { name = "SkyDome" };
        m.vertices = verts;
        m.triangles = tris;
        m.bounds = new Bounds(Vector3.zero, Vector3.one * 2.2f);
        return m;
    }

    void LateUpdate()
    {
        if (!enabledSky || mat == null || mesh == null) return;

        if (cam == null || !cam.isActiveAndEnabled || Time.unscaledTime >= camAt)
        {
            camAt = Time.unscaledTime + 1f;
            cam = Camera.main;
        }
        if (cam == null) return;

        UpdateMaterial();

        // радиус чуть меньше дальней плоскости: купол должен целиком попасть в кадр по глубине
        float radius = Mathf.Max(20f, cam.farClipPlane * 0.9f);
        var matrix = Matrix4x4.TRS(cam.transform.position, Quaternion.identity, Vector3.one * radius);
        Graphics.DrawMesh(mesh, matrix, mat, 0, cam, 0, null, ShadowCastingMode.Off, false);
    }

    void UpdateMaterial()
    {
        var dn = DayNightCycle.Instance;
        float t = DayNightCycle.Time01;
        float night = DayNightCycle.Night01;
        float tw = DayNightCycle.Twilight01;
        float day = Mathf.Clamp01(1f - night - tw);
        float dusk = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.4f, 0.6f, t));   // 0 утро ... 1 вечер (как в DayNightCycle)

        // направления: настоящие из DayNightCycle, иначе по RenderSettings.sun
        Vector3 toSun, toMoon;
        if (dn != null) dn.GetSkyDirections(out toSun, out toMoon);
        else
        {
            var sun = RenderSettings.sun;
            toSun = sun != null ? -sun.transform.forward : new Vector3(0.3f, 0.8f, 0.3f).normalized;
            toMoon = -toSun;
        }

        Color sunCol = RenderSettings.sun != null ? RenderSettings.sun.color : new Color(1f, 0.85f, 0.6f);
        float sunVis = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.14f, 0.03f, toSun.y));     // уходит чуть позже диска: закатное свечение
        float moonVis = night * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.05f, 0.12f, toMoon.y));

        Color zenTw = Color.Lerp(zenithDawn, zenithDusk, dusk);
        Color horTw = Color.Lerp(horizonDawn, horizonDusk, dusk);
        Color zenith = DayNightCycle.Mix(zenithDay, zenTw, zenithNight, day, tw, night);
        Color horizon = DayNightCycle.Mix(horizonDay, horTw, horizonNight, day, tw, night);

        // качество: Низкое проще (2 октавы, без звёзд)
        float detail, starAmt = stars;
        switch (GameSettings.LightQuality)
        {
            case LightQualityLevel.Low: detail = 0.0f; starAmt = 0f; break;
            case LightQualityLevel.High: detail = 1.0f; break;
            default: detail = 0.5f; break;
        }

        // фаза луны: по номеру дня; |cut| не меньше 0.9, иначе в новолуние луны не видно совсем
        int cycle = Mathf.Max(2, moonCycleDays);
        float ph = (DayNightCycle.DayNumber % cycle) / (float)cycle * Mathf.PI * 2f;
        float cut = 2.2f * Mathf.Cos(ph);
        if (Mathf.Abs(cut) < 0.9f) cut = cut < 0f ? -0.9f : 0.9f;

        float cover = Mathf.Clamp01(cloudCover + Mathf.Sin(Time.time * 0.03f) * cloudCoverWobble);

        mat.SetColor(FogId, RenderSettings.fogColor);
        mat.SetColor(HorizonId, horizon);
        mat.SetColor(ZenithId, zenith);
        mat.SetVector(SunDirId, toSun);
        mat.SetColor(SunColorId, sunCol);
        mat.SetVector(MoonDirId, toMoon);
        mat.SetVector(WeightsId, new Vector4(day, night, tw, sunVis));
        mat.SetVector(MoonId, new Vector4(cut, moonSize, moonVis, 0f));
        mat.SetVector(CloudId, new Vector4(cover, cloudSpeed, cloudScale, detail));
        mat.SetVector(StarsId, new Vector4(starAmt, 0f, 0f, 0f));
    }

}
