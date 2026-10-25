// Assets/_Project/Scripts/Forest/ChopGrove/ChopGroveBootstrap.cs (НЕ в Editor)
// Сам вешает ChopGroveBuilder на метку поляны (объект PAD_GroveSouth), ничего ставить в сцену вручную не нужно.
// Хочешь настроить поляну руками: добавь ChopGroveBuilder на метку сам, бутстрап увидит и не будет дублировать.
// Имя метки можно поменять в padNames (например добавить PAD_Forest).
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ChopGroveBootstrap
{
    public static readonly string[] padNames = { "PAD_GroveSouth" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Attach();
    }

    static void OnSceneLoaded(Scene s, LoadSceneMode m) { Attach(); }

    static void Attach()
    {
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t == null || System.Array.IndexOf(padNames, t.name) < 0) continue;
            if (t.GetComponent<ChopGroveBuilder>() != null) continue;
            t.gameObject.AddComponent<ChopGroveBuilder>();
        }
    }
}
