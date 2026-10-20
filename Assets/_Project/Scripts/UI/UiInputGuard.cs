// Assets/_Project/Scripts/UI/UiInputGuard.cs (НЕ в Editor)
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
        var es = EventSystem.current != null ? EventSystem.current : FindFirstObjectByType<EventSystem>();
        if (es == null)
        {
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(go);
            es = go.GetComponent<EventSystem>();
        }

        if (!es.gameObject.activeSelf) es.gameObject.SetActive(true);
        if (!es.enabled) es.enabled = true;

        // нужен именно StandaloneInputModule (старый Input); любой другой модуль на этом EventSystem выключаем
        var modules = es.GetComponents<BaseInputModule>();
        StandaloneInputModule standalone = null;
        string found = "";
        for (int i = 0; i < modules.Length; i++)
        {
            found += modules[i].GetType().Name + (modules[i].enabled ? "(on) " : "(off) ");
            if (modules[i] is StandaloneInputModule) standalone = (StandaloneInputModule)modules[i];
        }
        if (standalone == null) standalone = es.gameObject.AddComponent<StandaloneInputModule>();
        for (int i = 0; i < modules.Length; i++)
            if (modules[i] != standalone && modules[i].enabled) modules[i].enabled = false;
        if (!standalone.enabled) standalone.enabled = true;
        if (EventSystem.current != es) EventSystem.current = es;

        if (!logged)
        {
            logged = true;
            Debug.Log("[UiInputGuard] EventSystem: " + es.name + ", модули: " + (found.Length > 0 ? found : "нет") + "-> активен StandaloneInputModule");
        }
#endif
    }
}
