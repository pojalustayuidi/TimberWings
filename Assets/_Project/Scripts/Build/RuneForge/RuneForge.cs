using UnityEngine;

[DisallowMultipleComponent]
public sealed class RuneForge : MonoBehaviour
{
    public const string SaveId = "tw.rune-forge.workyard";
    public const string Title = "Рунная кузница";

    [Header("Стройка и условия открытия")]
    public BuildingController build;
    public BuildingController hut;
    public BuildingController workyard;

    [Header("Запечённые состояния")]
    public GameObject ruined;
    public GameObject repairFrame;
    public GameObject restored;

    [Header("Точки в сцене")]
    public Transform entrance;
    public Transform interior;
    public Transform oskarPoint;
    public Transform mapPoint;

    public bool IsRestored => build != null && build.IsFinished;
    public bool CanInspect => isActiveAndEnabled && build != null && !build.IsBuilding && !build.IsRevealing;

    void OnEnable()
    {
        if (build != null) build.StageBuilt += RefreshVisuals;
        if (hut != null) hut.Finished += RefreshAccess;
        if (workyard != null) workyard.Finished += RefreshAccess;
        GameEvents.BuildingRestored += OnBuildingRestored;
        RefreshAccess();
        RefreshVisuals();
    }

    void OnDisable()
    {
        if (build != null) build.StageBuilt -= RefreshVisuals;
        if (hut != null) hut.Finished -= RefreshAccess;
        if (workyard != null) workyard.Finished -= RefreshAccess;
        GameEvents.BuildingRestored -= OnBuildingRestored;
    }

    // The work frame follows the active construction timer, including pauses and resumed work.
    void LateUpdate() => RefreshVisuals();

    void RefreshAccess()
    {
        if (build != null)
            build.SetLocked(hut == null || workyard == null || !hut.IsFinished || !workyard.IsFinished);
    }

    void OnBuildingRestored(string id, int stage)
    {
        if ((hut != null && id == hut.SaveId) || (workyard != null && id == workyard.SaveId))
            RefreshAccess();
        if (build != null && id == build.SaveId)
            RefreshVisuals();
    }

    void RefreshVisuals()
    {
        bool ready = IsRestored;
        bool repairing = !ready && build != null && build.IsBuilding;
        SetStageVisible(ruined, !ready && !repairing);
        SetStageVisible(repairFrame, repairing);
        SetStageVisible(restored, ready);
    }

    static void SetStageVisible(GameObject stage, bool visible)
    {
        if (stage != null && stage.activeSelf != visible) stage.SetActive(visible);
    }
}
