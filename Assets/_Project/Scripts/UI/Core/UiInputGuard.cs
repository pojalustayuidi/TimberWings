// Assets/_Project/Scripts/UI/Core/UiInputGuard.cs (НЕ в Editor)
// Страховка для ВСЕХ окон и меню: следит, чтобы в игре был ровно один рабочий EventSystem с модулем ввода,
// который понимает старый Input (проект на нём: Input.GetKeyDown и т.д.).
// Типичная поломка: в сцене EventSystem с InputSystemUIInputModule (новая Input System) или вообще без модуля,
// из-за этого ни одна кнопка ни в одном окне не нажимается.
// Создаётся сама после загрузки сцены, ничего вешать не нужно. В Console пишет одну строку [UiInputGuard] с тем, что нашла.
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class UiInputGuard : MonoBehaviour
{
    static UiInputGuard instance;
    float nextCheck;
    bool logged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { instance = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (instance != null) return;
        var go = new GameObject("UiInputGuard");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<UiInputGuard>();
    }

    /// <summary>Единая точка создания и починки EventSystem; окна вызывают её перед созданием UI.</summary>
    public static void EnsureEventSystem()
    {
        var es = EventSystem.current != null ? EventSystem.current : FindFirstObjectByType<EventSystem>();
        if (es == null)
        {
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(go);
            es = go.GetComponent<EventSystem>();
        }
        if (!es.gameObject.activeSelf) es.gameObject.SetActive(true);
        if (!es.enabled) es.enabled = true;

        // Всегда оставляем единственный активный модуль старого ввода.
        // Ранее EnsureEventSystem мог включить несовместимый InputSystemUIInputModule.
        var modules = es.GetComponents<BaseInputModule>();
        StandaloneInputModule standalone = null;
        foreach (var module in modules)
            if (module is StandaloneInputModule legacy) { standalone = legacy; break; }
        if (standalone == null) standalone = es.gameObject.AddComponent<StandaloneInputModule>();
        foreach (var module in modules)
            if (module != null && module != standalone && module.enabled) module.enabled = false;
        if (!standalone.enabled) standalone.enabled = true;
        if (EventSystem.current != es) EventSystem.current = es;
    }

    void Awake() { SceneManager.sceneLoaded += OnSceneLoaded; }
    void OnDestroy() { SceneManager.sceneLoaded -= OnSceneLoaded; if (instance == this) instance = null; }
    void OnSceneLoaded(Scene s, LoadSceneMode m) { Check(); }
    void Start() { Check(); }

    void Update()
    {
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + 1f;
        Check();
    }

    void Check()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        EnsureEventSystem();
        // Диагностическая строка нужна только при первом запуске, а не каждую секунду.
        if (!logged)
        {
            logged = true;
            var es = EventSystem.current;
            var modules = es.GetComponents<BaseInputModule>();
            var found = new System.Text.StringBuilder(80);
            foreach (var module in modules)
                if (module != null) found.Append(module.GetType().Name)
                    .Append(module.enabled ? "(on) " : "(off) ");
            Debug.Log("[UiInputGuard] EventSystem: " + es.name + ", модули: " +
                (found.Length > 0 ? found.ToString() : "нет") + "-> активен StandaloneInputModule");
        }
#endif
    }
}
