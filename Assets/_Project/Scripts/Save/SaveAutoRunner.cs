// Assets/_Project/Scripts/Save/SaveAutoRunner.cs (НЕ в Editor)
// Создаётся сам до загрузки сцены. Читает сохранение при старте (GameBalance.SaveAutoLoadOnStart),
// гонит дебаунс RequestSave и автосохранение по таймеру, пишет файл при сворачивании и выходе.
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
        if (GameBalance.SaveAutoLoadOnStart) SaveSystem.Load();   // секции систем, которых ещё нет, применятся в их Register
    }

    void Update()
    {
        SaveSystem.Tick(Time.unscaledDeltaTime, Time.timeScale > 0f);
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
