// Assets/_Project/Scripts/Location/IslandLook.cs (НЕ в Editor)
// Остров v2, этап 3 (свет, небо, дымка): единая точка «настроение острова». Создаётся само, когда в сцене есть FloatingIsland и игрок
// (как CloudSea и IslandAbyss), в сцену ничего вешать не нужно.
//
// Части (partial): IslandLook.cs (этот файл: создание, жизнь, глобальные переменные шейдеров, качество),
//                  IslandLook.Haze.cs (раскладка и рисование карточек дымки).
//
// Что делает этот файл:
//   • каждую секунду и при правке ассета ставит глобальные переменные шейдера земли Project/IslandV2/VertexColor (_IslandShadowTint);
//   • следит за качеством света (GameSettings.LightQuality) и перестраивает дымку под профиль бюджета (Mobile Low / Mobile High / ПК);
//   • отдаёт наружу: IslandLook.Instance, IslandLook.Instance.HazeCardCount, IslandLook.Instance.hazeDebugEnabled (выключатель для замеров).
//
// Чего он НЕ делает (всё это уже есть, повторять нельзя):
//   • цвет и силу солнца, ambient, туман, фон камеры: DayNightCycle (числа из CloudCandyStyle);
//   • тени солнца и общий Volume «Island Light» (Bloom, Vignette, Tonemapping): LightingRig (профили качества из CloudCandyStyle);
//   • небо: SkyDome; облака под островом: CloudSea; плотность тумана острова: IslandAbyss; свет в лесу: ForestLook и Volume «Vibe Forest».
// Нет ассета CloudCandyStyle или он выключен (styleEnabled = false): IslandLook ничего не рисует и ничего не ставит (откат без правки кода).
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[DefaultExecutionOrder(215)]   // после камер (-100), SkyDome (200) и CloudSea (210): положение камеры уже финальное
public partial class IslandLook : MonoBehaviour
{
    public static IslandLook Instance { get; private set; }

    [Header("Отладка")]
    [Tooltip("Выключить: дымка не рисуется (нужно для замера «с дымкой / без дымки» в меню Остров v2 / 7). Обычно включено")]
    public bool hazeDebugEnabled = true;

    static readonly int ShadowTintId = Shader.PropertyToID("_IslandShadowTint");

    CloudCandyStyle style;
    int styleRev = -1;
    int builtQuality = -1;
    float nextCheck;
    Camera cam;
    float camAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic()
    {
        Instance = null;
        // глобальная переменная живёт между запусками Play Mode в редакторе: сбрасываем, чтобы тон теней не «залипал» без IslandLook
        Shader.SetGlobalColor(ShadowTintId, new Color(1f, 1f, 1f, 0f));
    }

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
        if (PlayerMovement.Instance == null) return;                          // меню и пустые сцены не трогаем
        if (IslandSource.Find() == null) return;   // дымка только у парящего острова
        if (Object.FindFirstObjectByType<IslandLook>() != null) return;
        new GameObject("_IslandLook").AddComponent<IslandLook>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        HazeAwake();
    }

    void OnEnable()
    {
        GameSettings.Changed += OnSettings;
    }

    void OnDisable()
    {
        GameSettings.Changed -= OnSettings;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        Shader.SetGlobalColor(ShadowTintId, new Color(1f, 1f, 1f, 0f));
        HazeDestroy();
    }

    void OnSettings() { builtQuality = -1; }   // качество света сменили в настройках: дымка перестроится в ближайшем кадре

    void LateUpdate()
    {
        style = CloudCandyStyle.Current;
        if (style == null)
        {
            Shader.SetGlobalColor(ShadowTintId, new Color(1f, 1f, 1f, 0f));
            return;   // стиль выключен: ни тона теней, ни дымки
        }

        // глобальные переменные шейдера: при правке ассета и раз в секунду (на случай сброса после перезагрузки шейдеров)
        if (styleRev != CloudCandyStyle.Revision || Time.unscaledTime >= nextCheck)
        {
            styleRev = CloudCandyStyle.Revision;
            nextCheck = Time.unscaledTime + 1f;
            Color t = style.groundShadowTint;
            Shader.SetGlobalColor(ShadowTintId, new Color(t.r, t.g, t.b, style.groundShadowStrength));
        }

        if (cam == null || !cam.isActiveAndEnabled || Time.unscaledTime >= camAt)
        {
            camAt = Time.unscaledTime + 1f;
            cam = Camera.main;
        }
        if (cam == null) return;

        int q = (int)GameSettings.LightQuality;
        if (builtQuality != q || styleRev != hazeBuiltRev)
        {
            builtQuality = q;
            RebuildHaze(style.GetBudget(GameSettings.LightQuality), style.haze);
        }

        DrawHaze(style, cam);
    }
}
