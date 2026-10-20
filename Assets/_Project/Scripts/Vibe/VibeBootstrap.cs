// Assets/_Project/Scripts/Vibe/VibeBootstrap.cs (НЕ в Editor)
// Включает «вайб»-системы сам, ничего вешать в сцену не нужно: звук, живой воздух (светлячки, листья, ветер, птицы)
// и праздник за выполненное задание создаются один раз после загрузки сцены, если в ней есть игрок.
// Подсказки к цели (QuestEdgeArrow + QuestGuide) создают IntroFlow и StoryFlow, как и раньше.
using UnityEngine;
using UnityEngine.SceneManagement;

// Настройки игрока. Окно настроек (GDD 10.5) может просто читать и писать эти свойства.
public static class VibeSettings
{
    const string KReduce = "tw_reduce_fx";
    const string KGuide = "tw_guide";

    // GDD 9.3: «Уменьшить тряску и эффекты»: меньше частиц, без конфетти, слабая тряска камеры
    public static bool ReduceEffects
    {
        get { return PlayerPrefs.GetInt(KReduce, 0) == 1; }
        set { PlayerPrefs.SetInt(KReduce, value ? 1 : 0); }
    }

    // громкость звуков и природы теперь живёт в GameAudio (там же общая громкость и «выключить всё»); эти свойства оставлены для старого кода
    public static float SfxVolume
    {
        get { return GameAudio.Sfx; }
        set { GameAudio.Sfx = value; }
    }

    public static float AmbientVolume
    {
        get { return GameAudio.Ambient; }
        set { GameAudio.Ambient = value; }
    }

    // автоподсказки и дорожка к цели (клавиша H работает всегда)
    public static bool GuideEnabled
    {
        get { return PlayerPrefs.GetInt(KGuide, 1) == 1; }
        set { PlayerPrefs.SetInt(KGuide, value ? 1 : 0); }
    }
}

public static class VibeBootstrap
{
    // AfterSceneLoad срабатывает ТОЛЬКО на первой сцене. Раньше после «Начать заново» объект _Vibe умирал вместе со сценой
    // и больше не создавался: пропадали все эффекты звука, ветер, птицы. Теперь проверяем после каждой загрузки сцены.
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
        if (Object.FindFirstObjectByType<SfxManager>() != null) return;

        var go = new GameObject("_Vibe");
        go.AddComponent<SfxManager>();
        go.AddComponent<AmbientLife>();
        go.AddComponent<QuestCelebration>();
        go.AddComponent<ImpactFx>();   // искры, пыль, листопад, кольца наград, огонь костра
    }
}
