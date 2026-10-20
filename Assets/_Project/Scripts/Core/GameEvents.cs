// Assets/_Project/Scripts/Core/GameEvents.cs (НЕ в Editor)
// Общая «шина» событий. Скрипты не вызывают друг друга напрямую: тот, кто делает дело, кидает событие,
// а кто интересуется (обучение, квесты, статистика, звук), подписывается.
//
//   void OnEnable()  { GameEvents.TreeChopped += OnTree; }
//   void OnDisable() { GameEvents.TreeChopped -= OnTree; }   // ОБЯЗАТЕЛЬНО отписываться
using System;
using UnityEngine;

public static class GameEvents
{
    public static event Action TreeChopped;
    public static event Action ChopHit;                // каждый удар топора: звук, тряска дерева, щепки
    public static event Action<int> ChopQuality;       // качество удара в ритм-рубке: 0 слабый, 1 хороший, 2 идеальный (звук «дзинь», вспышка)
    public static event Action<DialogueSequence> DialogueFinished;
    public static event Action<int> LevelUp;
    public static event Action<string> QuestDone;      // закрыта глава StoryFlow или вступление (аргумент: id главы). Слушают звук и праздник
    public static event Action<BuildingController> BuildRevealStarted;    // сдан этап: начался показ стадии (площадка заблокирована, камера на здании)
    public static event Action<BuildingController> BuildRevealFinished;   // показ закончился или пропущен: площадка свободна, StoryFlow идёт дальше
    public static event Action<DayPhase> TimeOfDayChanged;   // сменилась фаза суток (рассвет / день / закат / ночь). Слушают музыка, звуки природы, свет
    public static event Action<string, int> BuildingRestored;   // стройка восстановлена из сохранения (id, стадия). Без наград, катсцен и звуков: слушают те, кому нужно только обновить вид (ворота, NPC, свет, миникарта)

    public static void RaiseTreeChopped() { if (TreeChopped != null) TreeChopped(); }
    public static void RaiseChopHit() { if (ChopHit != null) ChopHit(); }
    public static void RaiseChopQuality(int quality) { if (ChopQuality != null) ChopQuality(quality); }
    public static void RaiseDialogueFinished(DialogueSequence s) { if (DialogueFinished != null) DialogueFinished(s); }
    public static void RaiseLevelUp(int level) { if (LevelUp != null) LevelUp(level); }
    public static void RaiseQuestDone(string id) { if (QuestDone != null) QuestDone(id); }
    public static void RaiseBuildRevealStarted(BuildingController b) { if (BuildRevealStarted != null) BuildRevealStarted(b); }
    public static void RaiseBuildRevealFinished(BuildingController b) { if (BuildRevealFinished != null) BuildRevealFinished(b); }
    public static void RaiseTimeOfDayChanged(DayPhase p)
    {
        if (TimeOfDayChanged == null) return;
        foreach (Delegate d in TimeOfDayChanged.GetInvocationList())
        {
            try { ((Action<DayPhase>)d)(p); }
            catch (Exception e) { Debug.LogException(e); }   // один упавший подписчик не ломает остальных
        }
    }
    public static void RaiseBuildingRestored(string id, int stage) { if (BuildingRestored != null) BuildingRestored(id, stage); }

    // при выключенном Domain Reload статика переживает остановку игры: чистим подписчиков сами
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetAll()
    {
        TreeChopped = null;
        ChopHit = null;
        ChopQuality = null;
        DialogueFinished = null;
        LevelUp = null;
        QuestDone = null;
        BuildRevealStarted = null;
        BuildRevealFinished = null;
        BuildingRestored = null;
        TimeOfDayChanged = null;
    }
}
