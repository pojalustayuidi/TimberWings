#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Safe Edit Mode asset setup. Does not replace scenes, StoryFlow, save files or other gameplay scripts.
public static class CloudyMapV2Baker
{
    private const string Root = "Assets/_Project/Resources/CloudyMapV2/";

    [MenuItem("Tools/TimberWings/Map/Cloudy Candy V2/Repair Sprite Imports")]
    public static void RepairImports()
    {
        string[] names = { "Header", "Frame", "WindowPanel", "Pill", "CornerCloud", "Cluster", "MysteryCloud", "Portal", "Workyard" };
        int changed = 0;
        foreach (string name in names)
        {
            string path = Root + name + ".png";
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning("[Cloudy Map V2] Missing " + path);
                continue;
            }
            bool needsChange = importer.textureType != TextureImporterType.Sprite
                || importer.spriteImportMode != SpriteImportMode.Single
                || importer.mipmapEnabled || importer.textureCompression != TextureImporterCompression.Uncompressed;
            Vector4 border = Border(name);
            if (importer.spriteBorder != border) needsChange = true;
            if (!needsChange) continue;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.spriteBorder = border;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            changed++;
        }
        Debug.Log("[Cloudy Map V2] Sprite import verified. Updated: " + changed);
        AuditScene();
    }

    private static Vector4 Border(string name)
    {
        switch (name)
        {
            case "Frame": return new Vector4(42f, 42f, 42f, 42f);
            case "WindowPanel": return new Vector4(45f, 45f, 45f, 45f);
            case "Pill": return new Vector4(48f, 34f, 48f, 34f);
            default: return Vector4.zero;
        }
    }

    [MenuItem("Tools/TimberWings/Map/Cloudy Candy V2/Audit Active Scene")]
    public static void AuditScene()
    {
        var maps = Object.FindObjectsByType<HudMinimap>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (maps.Length == 0) Debug.LogWarning("[Cloudy Map V2] Scene contains no HudMinimap.");
        else if (maps.Length > 1) Debug.LogWarning("[Cloudy Map V2] Multiple HudMinimap components: " + maps.Length);
        foreach (var map in maps)
        {
            var issues = new List<string>();
            if (map.bigMap == null) issues.Add("bigMap");
            if (map.bigMarkers == null) issues.Add("bigMarkers");
            if (map.bigGroup == null) issues.Add("bigGroup");
            if (map.bigWindow == null) issues.Add("bigWindow");
            if (map.markerTemplate == null) issues.Add("markerTemplate");
            if (issues.Count > 0)
                Debug.LogWarning("[Cloudy Map V2] " + map.name + " missing: " + string.Join(", ", issues), map);
            else
                Debug.Log("[Cloudy Map V2] " + map.name + ": HUD bindings present; runtime playmode still required.", map);
        }
        var ids = new HashSet<string>();
        foreach (var poi in Object.FindObjectsByType<MapPoi>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (string.IsNullOrWhiteSpace(poi.inspectionId)) continue;
            if (!ids.Add(poi.inspectionId)) Debug.LogWarning("[Cloudy Map V2] Duplicate inspection ID: " + poi.inspectionId, poi);
        }
        Debug.Log("[Cloudy Map V2] Audited " + maps.Length + " map(s), " + ids.Count + " explicit inspection IDs.");
    }
}
#endif
