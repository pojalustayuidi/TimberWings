using UnityEngine;
using UnityEngine.SceneManagement;

// Assets/_Project/Scripts/Location/IslandAbyss.cs (НЕ в Editor)
// Пропасть вокруг парящего острова должна читаться, а не тонуть в тёплой дымке.
// Откуда дымка: DayNightCycle каждый кадр включает туман ExponentialSquared (плотность 0.010: на 60 м это ~30%),
// а FloatingIsland.FitGround красит фон камеры в цвет этого тумана. За краем карты остаётся ровная розово-бежевая заливка без глубины.
// Что делает скрипт: создаётся сам, когда в сцене есть парящий остров (как CloudSea), и один раз уменьшает плотность тумана
// у DayNightCycle (поля fogDensityDay и fogDensityNight) в fogMul раз. Ничего в сцене настраивать не нужно.
// Облака CloudSea и скалы под островом становятся видны на всю глубину, вдали дымка остаётся (красиво уходит в горизонт).
//
// Настройка: поле fogMul у объекта _IslandAbyss (по умолчанию 0.3). 1 = как раньше, 0.1 = почти без тумана.
// Выключить: IslandAbyss.Instance.enabledAbyss = false (прежние значения вернутся сами).
[DisallowMultipleComponent]
[DefaultExecutionOrder(-90)]   // после DayNightCycle (-100) и до ForestLook: лес берёт уже уменьшенный туман за базу
public class IslandAbyss : MonoBehaviour
{
    public static IslandAbyss Instance { get; private set; }

    public bool enabledAbyss = true;
    [Tooltip("Во сколько раз уменьшить плотность тумана парящего острова. 1 = как раньше")]
    [Range(0.05f, 1f)] public float fogMul = 0.3f;

    DayNightCycle bound;
    float baseDay, baseNight;
    bool applied;

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
        if (PlayerMovement.Instance == null) return;                         // меню и пустые сцены не трогаем
        // тестовый остров (LocationBuilderTester) собирается без FloatingIsland: корень зовётся LocationTest
        bool floating = IslandSource.Find() != null || GameObject.Find("LocationTest") != null;
        if (!floating) return;                                               // туман прежний, пока остров не парящий
        if (Object.FindFirstObjectByType<IslandAbyss>() != null) return;
        new GameObject("_IslandAbyss").AddComponent<IslandAbyss>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Update()
    {
        var dn = DayNightCycle.Instance;
        if (dn == null) return;

        if (dn != bound)
        {
            bound = dn;
            baseDay = dn.fogDensityDay;
            baseNight = dn.fogDensityNight;
            applied = false;
        }

        if (enabledAbyss)
        {
            // Cloud Candy: база плотности из ассета стиля (правится в инспекторе), иначе та, что была у DayNightCycle
            // Остров v2, этап 3: множитель тоже из ассета (st.fogMulIsland), поле fogMul остаётся откатом без стиля
            var st = CloudCandyStyle.Current;
            float mul = st != null ? st.fogMulIsland : fogMul;
            dn.fogDensityDay = (st != null ? st.fogDensityDay : baseDay) * mul;
            dn.fogDensityNight = (st != null ? st.fogDensityNight : baseNight) * mul;
            applied = true;
        }
        else Restore();
    }

    void Restore()
    {
        if (!applied || bound == null) return;
        bound.fogDensityDay = baseDay;
        bound.fogDensityNight = baseNight;
        applied = false;
    }

    void OnDestroy()
    {
        Restore();
        if (Instance == this) Instance = null;
    }
}
