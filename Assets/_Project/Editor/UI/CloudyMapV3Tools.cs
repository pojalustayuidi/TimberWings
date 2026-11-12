#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Edit-mode workflow for the V3 appearance. It does not modify existing map/quest/save scripts.
public static class CloudyMapV3Tools
{
    const string MenuRoot = "Tools/TimberWings/Map/Cloudy Candy V3/";
    const string Root = "Assets/_Project/Resources/CloudyMapV2/";
    static readonly string[] Sprites = {
        "RoundButton", "LegendBar", "StateUnknown", "StateFound", "StateStudied", "StateBuilt",
        "IconCampfire", "IconQuest", "IconBridge", "IconDiscovery"
    };

    [MenuItem(MenuRoot + "1 - Enable / Repair Cloudy Map V3", false, 110)]
    public static void EnableV3()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Cloudy Map V3", "Останови Play Mode перед изменением сцены.", "OK");
            return;
        }
        if (!V2HooksPresent())
        {
            Debug.LogWarning("[Cloudy Map V3] Не найдены подключения V2. Сначала воспользуйся меню Cloudy Candy V2 -> 1 - Activate / Repair Cloudy Map V2.");
            EditorUtility.DisplayDialog("Cloudy Map V3", "Сперва активируй V2 (9/9 hooks), затем снова включи V3.\nДетали в Console.", "OK");
            return;
        }
        int updated = 0;
        foreach (HudMinimap map in UnityEngine.Object.FindObjectsByType<HudMinimap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (map == null) continue;
            Undo.RecordObject(map, "Enable Cloudy Map V3");
            map.cloudyMapV2 = true;
            map.cloudyMapV3 = true;
            map.candyStyle = true;
            map.cloudyOverviewLabels = 3;
            EditorUtility.SetDirty(map);
            if (PrefabUtility.IsPartOfPrefabInstance(map)) PrefabUtility.RecordPrefabInstancePropertyModifications(map);
            if (map.gameObject.scene.IsValid() && map.gameObject.scene.isLoaded)
                EditorSceneManager.MarkSceneDirty(map.gameObject.scene);
            updated++;
        }
        RepairSpriteImports();
        AssetDatabase.SaveAssets();
        Debug.Log("[Cloudy Map V3] Готово. Карт в открытых сценах: " + updated + ". Сохрани сцену Ctrl+S, затем Play -> M.");
        EditorUtility.DisplayDialog("Cloudy Map V3", "Оформление V3 включено для " + updated + " карт.\nСохрани сцену (Ctrl+S), открой Play и нажми M.", "OK");
    }

    static bool V2HooksPresent()
    {
        string big = "Assets/_Project/Scripts/UI/Map/HudMinimap.BigMap.cs";
        string main = "Assets/_Project/Scripts/UI/Map/HudMinimap.cs";
        string project = Directory.GetParent(Application.dataPath).FullName;
        string bigPath = Path.Combine(project, big.Replace('/', Path.DirectorySeparatorChar));
        string mainPath = Path.Combine(project, main.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(bigPath) || !File.Exists(mainPath)) return false;
        string b = File.ReadAllText(bigPath), m = File.ReadAllText(mainPath);
        return b.Contains("InitCloudyMapV2(parent);")
            && b.Contains("LayoutCloudyMapLabels(inv);")
            && b.Contains("UpdateCloudyMapV2(time);")
            && m.Contains("CloudyStyleMarker(m, p, big);")
            && m.Contains("CloudySetupHover(m, p, big);")
            && m.Contains("CloudyAddWorkyardPoi();")
            && m.Contains("CloudyAddPortalPoi();");
    }

    [MenuItem(MenuRoot + "2 - Repair V3 Sprite Imports", false, 111)]
    public static void RepairSpriteImports()
    {
        int changed = 0;
        foreach (string name in Sprites)
        {
            string path = Root + name + ".png";
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) { Debug.LogWarning("[Cloudy Map V3] Sprite missing: " + path); continue; }
            Vector4 border = name == "LegendBar" ? new Vector4(42,32,42,32) : Vector4.zero;
            bool dirty = importer.textureType != TextureImporterType.Sprite
                || importer.spriteImportMode != SpriteImportMode.Single
                || importer.spriteBorder != border
                || importer.mipmapEnabled
                || importer.textureCompression != TextureImporterCompression.Uncompressed;
            if (!dirty) continue;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = border;
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            changed++;
        }
        Debug.Log("[Cloudy Map V3] Sprite import verified; updated: " + changed + " / " + Sprites.Length + ".");
    }

    [MenuItem(MenuRoot + "3 - Diagnose V3", false, 112)]
    public static void Diagnose()
    {
        var missing = new List<string>();
        foreach (string s in Sprites)
            if (AssetDatabase.LoadAssetAtPath<Sprite>(Root + s + ".png") == null) missing.Add(s);
        int maps = 0;
        foreach (HudMinimap map in UnityEngine.Object.FindObjectsByType<HudMinimap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (map == null) continue;
            maps++;
            Transform win = map.bigWindow;
            bool surface = win != null && win.Find("CloudyV3Surface") != null;
            bool footer = win != null && win.Find("CloudyV3Footer") != null;
            Transform body = win != null ? win.Find("Body") : null;
            Debug.Log("[Cloudy Map V3] " + map.name
                + " flags=" + map.cloudyMapV2 + "/" + map.cloudyMapV3 + "/" + map.candyStyle
                + " runtime surface=" + surface + ", footer=" + footer
                + ", old Body hidden=" + (body == null || !body.gameObject.activeSelf), map);
        }
        Debug.Log("[Cloudy Map V3] Diagnostic: V2 hooks=" + (V2HooksPresent() ? "READY" : "MISSING")
            + ", V3 sprites=" + (Sprites.Length - missing.Count) + "/" + Sprites.Length
            + ", maps=" + maps
            + (missing.Count > 0 ? ", missing=" + string.Join(",", missing) : ""));
    }
}
#endif
