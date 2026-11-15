// Automatically created save runner. Keeps all existing SaveSystem behaviour;
// SettingsAppearance.PeriodicAutosave only controls periodic interval saves.
using UnityEngine;

public sealed class SaveAutoRunner : MonoBehaviour
{
    static SaveAutoRunner instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { instance = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        if (instance != null) return;
        var go = new GameObject("SaveAutoRunner");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<SaveAutoRunner>();
        if (GameBalance.SaveAutoLoadOnStart) SaveSystem.Load();
    }

    void Update()
    {
        SaveSystem.Tick(Time.unscaledDeltaTime, Time.timeScale > 0f && SettingsAppearance.PeriodicAutosave);
    }

    void OnApplicationPause(bool paused)
    {
        if (paused) SaveSystem.FlushIfDirty();
    }

    void OnApplicationQuit()
    {
        SaveSystem.FlushIfDirty();
    }
}
