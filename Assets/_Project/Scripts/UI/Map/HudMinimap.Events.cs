// Assets/_Project/Scripts/UI/Map/HudMinimap.Events.cs (НЕ в Editor)
// Что заставляет карту перепечататься: любая стройка (этап сдан / достроено / показ закончен / восстановление из сохранения),
// открытие прохода к мосту (FloatingIsland.GateOpened), смена фазы суток, загрузка сохранения, возврат в приложение.
// Сама печать и ограничение «не чаще раза в rebakeInterval» живут в HudMinimap.Bake.cs (RequestRebake).
using System.Collections.Generic;
using UnityEngine;

public partial class HudMinimap
{
    readonly HashSet<BuildingController> hooked = new HashSet<BuildingController>();
    bool eventsOn, rescanPending, firstBakeDone;

    // Расширения для следующих частей (туман, маршрут, след): вызываются после каждой печати
    partial void OnMapBakedExt(bool first);
    partial void OnBigOpenedExt();

    void SubscribeMapEvents()
    {
        if (eventsOn) return;
        eventsOn = true;
        GameEvents.QuestDone += OnQuestDone;
        MapPoi.Inspected += OnPoiInspected;
        GameEvents.BuildRevealFinished += OnRevealFinished;
        GameEvents.BuildingRestored += OnRestored;
        GameEvents.TimeOfDayChanged += OnPhaseChanged;
        SaveSystem.Loaded += OnSaveLoaded;
        FloatingIsland.GateOpened += OnGateOpened;
        HookBuildings();
    }

    void UnsubscribeMapEvents()
    {
        if (!eventsOn) return;
        eventsOn = false;
        GameEvents.QuestDone -= OnQuestDone;
        MapPoi.Inspected -= OnPoiInspected;
        GameEvents.BuildRevealFinished -= OnRevealFinished;
        GameEvents.BuildingRestored -= OnRestored;
        GameEvents.TimeOfDayChanged -= OnPhaseChanged;
        SaveSystem.Loaded -= OnSaveLoaded;
        FloatingIsland.GateOpened -= OnGateOpened;
        foreach (var b in hooked)
        {
            if (b == null) continue;
            b.StageBuilt -= OnBuildingChanged;
            b.Finished -= OnBuildingChanged;
            b.LockChanged -= OnBuildingLockChanged;
        }
        hooked.Clear();
    }

    // Стройки подписываются каждая на свои StageBuilt и Finished (событий-шины для них нет). Вызывается из Rescan
    void HookBuildings()
    {
        if (!eventsOn) return;
        hooked.RemoveWhere(IsMissingBuilding);
        foreach (var b in FindObjectsByType<BuildingController>(FindObjectsSortMode.None))
        {
            // HashSet.Add одновременно регистрирует новый объект и отсекает повторные подписки.
            if (b == null || !hooked.Add(b)) continue;
            b.StageBuilt += OnBuildingChanged;
            b.Finished += OnBuildingChanged;
            b.LockChanged += OnBuildingLockChanged;
        }
    }

    static bool IsMissingBuilding(BuildingController b) => b == null;

    void OnQuestDone(string id) { rescanPending = true; }

    void UpdateProgress()
    {
        if (!progressReady) return;
        var story = StoryFlow.Instance;
        if (story != null && (story.MapFinaleInspected != finaleWasInspected || story.PetsOpen != petsWereOpen)) rescanPending = true;
        if (rescanPending)
        {
            rescanPending = false;
            Rescan();
        }
        UpdateQuestPin();
        UpdateDiscoveryHints();
    }

    void OnBuildingLockChanged() { rescanPending = true; }
    void OnBuildingChanged() { rescanPending = true; RequestRebake(rebakeDelay); }
    void OnRevealFinished(BuildingController b) { rescanPending = true; RequestRebake(rebakeDelay); }
    void OnRestored(string id, int stage) { rescanPending = true; RequestRebake(0.6f); }
    void OnGateOpened() { rescanPending = true; RequestRebake(2f); }                       // камни ещё опускаются 1.6 с
    void OnSaveLoaded() { rescanPending = true; RequestRebake(0.8f); }
    void OnPhaseChanged(DayPhase ph) { if (rebakeOnTimeOfDay) RequestRebake(0.2f); }

    // Вызывается из Bake.cs после каждой удачной печати мини-карты
    void OnMapBaked()
    {
        bool first = !firstBakeDone;
        if (first)
        {
            firstBakeDone = true;
            // границы известны только теперь (запасной вариант без FloatingIsland считается при первом разборе): строим слои заново
            BuildZoneLayer();
            if (!bigPrefetched && bigMap != null) { bigMap.texture = rtMini; bigMap.enabled = true; }   // до печати большой карты показываем мини-снимок
        }
        if (first) rescanPending = true;
        if (open) RefreshBigMarkers();
        OnMapBakedExt(first);
    }

    void OnBigOpened()
    {
        if (mapReady && bigDirty && !baking) StartCoroutine(BakeBigOnly());
        OnBigOpenedExt();
    }
}
