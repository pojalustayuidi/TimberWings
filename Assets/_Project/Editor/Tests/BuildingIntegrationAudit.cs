#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Не меняет сцену: только выводит в Console проблемные стройки и скрипты.
public static class BuildingIntegrationAudit
{
    [MenuItem("Tools/TimberWings/QA/Check Building Integration")]
    public static void Check()
    {
        int issues = 0;
        string[] required =
        {
            "Assets/_Project/Scripts/Build/BuildingController.cs",
            "Assets/_Project/Scripts/Build/Hut/BuildWorkPose.cs",
            "Assets/_Project/Scripts/Build/Hut/BuildWorkSpot.cs",
            "Assets/_Project/Scripts/Core/MobileInput.cs",
            "Assets/_Project/Scripts/Player/Leo/LeoModelPresentation.Life.cs",
            "Assets/_Project/Scripts/Vibe/BuildFx.cs",
            "Assets/_Project/Scripts/Vibe/BuildReveal.cs",
            "Assets/_Project/Scripts/Vibe/ImpactFx.cs"
        };
        foreach (string path in required)
        {
            if (AssetDatabase.LoadAssetAtPath<MonoScript>(path) != null) continue;
            Debug.LogError("[Building QA] Missing or not imported: " + path);
            issues++;
        }
        var buildings = Object.FindObjectsByType<BuildingController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var ids = new HashSet<string>();
        foreach (var b in buildings)
        {
            if (b == null) continue;
            var zone = b.GetComponent<Collider>();
            if (zone == null)
            {
                Debug.LogWarning("[Building QA] No Collider on construction zone: " + b.name, b);
                issues++;
            }
            else if (!zone.isTrigger)
            {
                Debug.LogWarning("[Building QA] Construction Collider must be a trigger: " + b.name, b);
                issues++;
            }
            if (b.lockSafetySeconds < 0f)
            {
                Debug.LogWarning("[Building QA] lockSafetySeconds cannot be negative: " + b.name, b);
                issues++;
            }
            if (Application.isPlaying && b.GetComponent<BuildFxHook>() == null)
            {
                Debug.LogWarning("[Building QA] Missing BuildFxHook on active construction zone: " + b.name, b);
                issues++;
            }
            string id = b.SaveId;
            if (!string.IsNullOrEmpty(id) && !ids.Add(id))
            {
                Debug.LogWarning("[Building QA] Duplicate SaveId: " + id, b);
                issues++;
            }
        }
        if (Application.isPlaying)
        {
            if (ImpactFx.Instance == null)
            {
                Debug.LogWarning("[Building QA] Scene has no ImpactFx: hammer chips / sparks / dust will not render.");
                issues++;
            }
            if (SfxManager.Instance == null)
            {
                Debug.LogWarning("[Building QA] Scene has no SfxManager: hammer/build sounds will not play.");
                issues++;
            }
        }
        if (issues == 0) Debug.Log("[Building QA] Script imports OK; " + buildings.Length + " scene construction zones checked. Gameplay still needs Play Mode testing.");
        else Debug.LogWarning("[Building QA] Problems: " + issues + " (construction zones checked: " + buildings.Length + "). See Console.");
    }
}
#endif
