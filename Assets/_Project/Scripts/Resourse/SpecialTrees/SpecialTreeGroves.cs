using UnityEngine;

[DisallowMultipleComponent]
public sealed class SpecialTreeGroves : MonoBehaviour
{
    public RuneForge forge;
    public GameObject courtyard;
    public GameObject runeTrees;
    public GameObject[] goldenSites;

    public static SpecialTreeGroves Current { get; private set; }
    internal static bool HasAuthoredGold => Current != null && Current.goldenSites != null && Current.goldenSites.Length > 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() => Current = null;

    void OnEnable()
    {
        Current = this;
        if (forge != null && forge.build != null) forge.build.Finished += Refresh;
        GameEvents.BuildingRestored += OnBuildingRestored;
        PlayerSkills.Changed += Refresh;
        SaveSystem.Loaded += Refresh;
        SaveSystem.NewGameStarted += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        if (Current == this) Current = null;
        if (forge != null && forge.build != null) forge.build.Finished -= Refresh;
        GameEvents.BuildingRestored -= OnBuildingRestored;
        PlayerSkills.Changed -= Refresh;
        SaveSystem.Loaded -= Refresh;
        SaveSystem.NewGameStarted -= Refresh;
    }

    void OnBuildingRestored(string id, int stage)
    {
        if (forge != null && forge.build != null && id == forge.build.SaveId) Refresh();
    }

    void Refresh()
    {
        if (SaveSystem.IsLoading) return;
        Show(courtyard != null ? courtyard : runeTrees, forge != null && forge.IsRestored);
        if (goldenSites == null || goldenSites.Length == 0) return;

        bool unlocked = PlayerSkills.Has(SkillIds.LumberGolden);
        int slot = ActiveGoldenSlot();
        for (int i = 0; i < goldenSites.Length; i++) Show(goldenSites[i], unlocked && i == slot);
    }

    int ActiveGoldenSlot() => Mathf.Clamp(ChopPrototypeSave.GoldenSlot, 0, goldenSites.Length - 1);

    internal void OnTreeRegrown(SpecialHarvestTree tree)
    {
        if (tree == null || !tree.IsGolden || goldenSites == null || goldenSites.Length < 2) return;
        int slot = ActiveGoldenSlot();
        GameObject site = goldenSites[slot];
        if (site == null || !tree.transform.IsChildOf(site.transform)) return;
        ChopPrototypeSave.SetGoldenSlot((slot + 1) % goldenSites.Length);
        Refresh();
    }

    static void Show(GameObject group, bool visible)
    {
        if (group != null && group.activeSelf != visible) group.SetActive(visible);
    }
}
