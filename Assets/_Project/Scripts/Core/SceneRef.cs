// Assets/_Project/Scripts/Core/SceneRef.cs (НЕ в Editor)
// Кэш ссылки на единственный объект сцены (игрок, инвентарь...). Заменяет россыпь FindFirstObjectByType по всему проекту.
// Get(): живая ссылка отдаётся сразу; если её нет, поиск идёт не чаще раза в 0.1 с (раньше каждый скрипт искал сам).
// Поведение то же, что у FindFirstObjectByType: объект появился позже или пересоздан, ссылка подхватится сама.
using UnityEngine;

public static class SceneRef<T> where T : Object
{
    const float RetryEvery = 0.1f;
    static T cached;
    static float nextFind;

    public static T Get()
    {
        if (cached != null) return cached;
        float t = Time.unscaledTime;
        // nextFind слишком далеко впереди: остаток от прошлого запуска (отключён Domain Reload), игнорируем
        if (t < nextFind && nextFind - t < 1f) return null;
        nextFind = t + RetryEvery;
        cached = Object.FindFirstObjectByType<T>();
        return cached;
    }

    // Объект сам сообщает о себе в Awake: поиск не понадобится вовсе
    public static void Set(T value) { cached = value; }
}
