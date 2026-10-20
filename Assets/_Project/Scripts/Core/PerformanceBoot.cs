// Assets/_Project/Scripts/Core/PerformanceBoot.cs (НЕ в Editor)
// В проекте нигде не задавался Application.targetFrameRate. На Android и iOS Unity тогда работает на 30 кадрах,
// и любая анимация (бег, рубка, интерфейс) выглядит рваной. Включается сама перед загрузкой первой сцены.
using UnityEngine;

public static class PerformanceBoot
{
    // 60 хватает всем, 90/120 только греют телефон. Хочешь больше: поменяй число или привяжи к настройкам.
    public const int TargetFps = 60;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        QualitySettings.vSyncCount = 0;                 // иначе targetFrameRate игнорируется
        Application.targetFrameRate = TargetFps;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;  // экран не гаснет посреди рубки
    }
}
