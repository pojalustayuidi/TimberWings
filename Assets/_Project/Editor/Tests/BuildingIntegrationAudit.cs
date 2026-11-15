#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Read-only validation of the active scene and required script assets.
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

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError("[Building QA] No valid active scene is loaded.");
            return;
        }
        var buildings = new List<BuildingController>();
        foreach (GameObject root in scene.GetRootGameObjects())
            buildings.AddRange(root.GetComponentsInChildren<BuildingController>(true));

        var ids = new HashSet<string>();
        foreach (BuildingController building in buildings)
        {
            if (building == null) continue;
            var zone = building.GetComponent<Collider>();
            if (zone == null)
            {
                Debug.LogWarning("[Building QA] No Collider on construction zone: " + building.name, building);
                issues++;
            }
            else if (!zone.isTrigger)
            {
                Debug.LogWarning("[Building QA] Construction Collider must be a trigger: " + building.name, building);
                issues++;
            }
            if (building.lockSafetySeconds < 0f)
            {
                Debug.LogWarning("[Building QA] lockSafetySeconds cannot be negative: " + building.name, building);
                issues++;
            }
            if (Application.isPlaying && building.GetComponent<BuildFxHook>() == null)
            {
                Debug.LogWarning("[Building QA] Missing BuildFxHook on active construction zone: " + building.name, building);
                issues++;
            }
            string id = building.SaveId;
            if (!string.IsNullOrEmpty(id) && !ids.Add(id))
            {
                Debug.LogWarning("[Building QA] Duplicate SaveId: " + id, building);
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
        string summary = "[Building QA] Active scene '" + scene.name + "': " + buildings.Count +
            " construction zones checked, issues: " + issues + ". " +
            (Application.isPlaying ? "Play Mode references checked." : "Gameplay still needs Play Mode testing.");
        if (issues == 0) Debug.Log(summary);
        else Debug.LogWarning(summary);
    }
}
#endif
