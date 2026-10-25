#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
public static class BuilderSimPolishSetup
{
    [MenuItem("Tools/BuilderSim/Визуал и свет/Polish Setup", false, 801)]
    static void Setup()
    {
        foreach (var c in Object.FindObjectsByType<BuildingController>(FindObjectsSortMode.None))
        {
            if (c.GetComponent<BuildSiteTagView>() == null) c.gameObject.AddComponent<BuildSiteTagView>();
            if (c.GetComponent<BuildSiteHighlighter>() == null) c.gameObject.AddComponent<BuildSiteHighlighter>();
        }
        var p = Object.FindFirstObjectByType<PlayerInteraction>();
        if (p != null && p.GetComponent<BackpackTagDistinct>() == null) p.gameObject.AddComponent<BackpackTagDistinct>();
        if (Object.FindFirstObjectByType<ResourceHighlighter>() == null) new GameObject("ResourceHighlighter").AddComponent<ResourceHighlighter>();
        if (Object.FindFirstObjectByType<QuestEdgeArrow>() == null) new GameObject("QuestEdgeArrow").AddComponent<QuestEdgeArrow>();
        if (HudSafeAreaRegistry.Instance == null) new GameObject("HudSafeAreaRegistry").AddComponent<HudSafeAreaRegistry>();
        Debug.Log("Polish Setup готово. Сохрани сцену.");
    }
}
#endif