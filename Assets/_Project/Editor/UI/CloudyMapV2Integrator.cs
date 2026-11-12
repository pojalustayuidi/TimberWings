#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Repairs the missing integration hooks when a user copied CloudyMap V2 assets or a hotfix
// without running the original installer. No scene rebuild, StoryFlow edits or save changes.
public static class CloudyMapV2Integrator
{
    private const string MenuRoot = "Tools/TimberWings/Map/Cloudy Candy V2/";
    private const string Big = "Assets/_Project/Scripts/UI/Map/HudMinimap.BigMap.cs";
    private const string Main = "Assets/_Project/Scripts/UI/Map/HudMinimap.cs";
    private const string ResourcesRoot = "Assets/_Project/Resources/CloudyMapV2/";
    private static readonly string[] SpriteNames = {
        "Header", "Frame", "WindowPanel", "Pill", "CornerCloud", "Cluster",
        "MysteryCloud", "Portal", "Workyard"
    };

    private sealed class Hook
    {
        public readonly string Path;
        public readonly string Before;
        public readonly string After;

        public Hook(string path, string before, string after)
        {
            Path = path;
            Before = before;
            After = after;
        }
    }

    // Same nine verified anchors as the V2.2 installer, not approximate regex edits.
    private static readonly Hook[] Hooks = {
        new Hook(Big,
            "        BuildFrame(parent);\n        if (codeCompass) StyleCompass();",
            "        BuildFrame(parent);\n        InitCloudyMapV2(parent);\n        if (codeCompass) StyleCompass();"),
        new Hook(Big,
            "        LayoutZoneNames(inv);",
            "        if (cloudyMapV2 && candyStyle) LayoutCloudyMapLabels(inv);\n        else LayoutZoneNames(inv);"),
        new Hook(Big,
            "        if (zoomOutBtn != null) zoomOutBtn.interactable = zoomTarget > minZoom + 0.01f;",
            "        if (zoomOutBtn != null) zoomOutBtn.interactable = zoomTarget > minZoom + 0.01f;\n        UpdateCloudyMapV2(time);"),
        new Hook(Main,
            "        AddBuildingPins(sites);",
            "        AddBuildingPins(sites);\n        CloudyAddWorkyardPoi();"),
        new Hook(Main,
            "        AddDiscoveryPins(used);",
            "        AddDiscoveryPins(used);\n        CloudyAddPortalPoi();"),
        new Hook(Main,
            "        HookBuildings();\n        if (open) RefreshBigMarkers();",
            "        HookBuildings();\n        if (open) RefreshBigMarkers();\n        CloudyAfterRescan();"),
        new Hook(Main,
            "        if (candyStyle) CandyMarker(m, p, big);",
            "        if (candyStyle) CandyMarker(m, p, big);\n        CloudyStyleMarker(m, p, big);"),
        new Hook(Main,
            "        foreach (var graphic in m.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;\n        return m;",
            "        foreach (var graphic in m.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;\n        CloudySetupHover(m, p, big);\n        return m;"),
        new Hook(Main,
            "        for (int i = 0; i < layPois.Count; i++) layPois[i].big.anchoredPosition = layPos[i];",
            "        for (int i = 0; i < layPois.Count; i++) layPois[i].big.anchoredPosition = layPos[i];\n        CloudyRefreshMarkers();"),
    };

    private sealed class SourceState
    {
        public string Text;
        public string Newline;
        public bool Bom;
        public string Original;
    }

    private static int Occurrences(string haystack, string needle)
    {
        int count = 0;
        int at = 0;
        while ((at = haystack.IndexOf(needle, at, StringComparison.Ordinal)) >= 0)
        {
            count++;
            at += needle.Length;
        }
        return count;
    }

    private static string RootPath()
    {
        return Directory.GetParent(Application.dataPath).FullName;
    }

    private static string Absolute(string path)
    {
        return Path.Combine(RootPath(), path.Replace('/', Path.DirectorySeparatorChar));
    }

    private static bool ReadSources(out Dictionary<string, SourceState> states, out string problem)
    {
        states = new Dictionary<string, SourceState>();
        problem = null;
        foreach (string path in new[] { Big, Main })
        {
            string full = Absolute(path);
            if (!File.Exists(full))
            {
                problem = "Не найден скрипт: " + path;
                return false;
            }
            byte[] bytes = File.ReadAllBytes(full);
            bool bom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            string original = Encoding.UTF8.GetString(bytes, bom ? 3 : 0, bytes.Length - (bom ? 3 : 0));
            states.Add(path, new SourceState {
                Original = original,
                Text = original.Replace("\r\n", "\n"),
                Newline = original.Contains("\r\n") ? "\r\n" : "\n",
                Bom = bom
            });
        }
        return true;
    }

    private static bool CheckSprites(out string missing)
    {
        var absent = new List<string>();
        foreach (string name in SpriteNames)
        {
            string path = ResourcesRoot + name + ".png";
            if (!File.Exists(Absolute(path))) absent.Add(path);
        }
        missing = string.Join("\n", absent.ToArray());
        return absent.Count == 0;
    }

    [MenuItem(MenuRoot + "1 - Activate / Repair Cloudy Map V2", false, 100)]
    public static void Activate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Cloudy Map V2", "Сначала останови Play Mode, затем повтори активацию.", "OK");
            return;
        }
        string missing;
        if (!CheckSprites(out missing))
        {
            Debug.LogError("[Cloudy Map V2] Не хватает PNG. Скопируй папку Assets из FIX-пакета:\n" + missing);
            EditorUtility.DisplayDialog("Cloudy Map V2", "Не все ассеты скопированы. Смотри Unity Console.", "OK");
            return;
        }
        if (!File.Exists(Absolute("Assets/_Project/Scripts/UI/Map/HudMinimap.CloudyMapV2.cs"))
            || !File.Exists(Absolute("Assets/_Project/Scripts/UI/Map/MapCloudyHover.cs")))
        {
            Debug.LogError("[Cloudy Map V2] Не хватает скриптов CloudyMapV2/MapCloudyHover. Распакуй всю папку Assets из FIX-пакета.");
            return;
        }

        Dictionary<string, SourceState> originals;
        string problem;
        if (!ReadSources(out originals, out problem))
        {
            Debug.LogError("[Cloudy Map V2] " + problem);
            return;
        }
        var edited = new Dictionary<string, string>();
        foreach (var kv in originals) edited[kv.Key] = kv.Value.Text;
        int already = 0, added = 0;
        // Atomic preflight: refuse to write either source if any anchor is unknown.
        foreach (Hook hook in Hooks)
        {
            string source = edited[hook.Path];
            int installed = Occurrences(source, hook.After);
            if (installed == 1)
            {
                already++;
                continue;
            }
            if (installed != 0 || Occurrences(source, hook.Before) != 1)
            {
                Debug.LogError("[Cloudy Map V2] ОСТАНОВКА: точка подключения изменилась в " + hook.Path +
                    ". Ни один исходный скрипт не изменён. Фрагмент: " + hook.Before.Trim());
                EditorUtility.DisplayDialog("Cloudy Map V2", "Исходники отличаются. Ничего не изменено. Смотри Console.", "OK");
                return;
            }
            edited[hook.Path] = source.Replace(hook.Before, hook.After);
            added++;
        }

        string backup = null;
        if (added > 0)
        {
            backup = Path.Combine(RootPath(), "CloudyMapV2_Backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
            Directory.CreateDirectory(backup);
            // Back up BOTH exact originals before changing either.
            foreach (var kv in originals)
            {
                string target = Path.Combine(backup, kv.Key.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(Absolute(kv.Key), target, true);
            }
            var written = new List<string>();
            try
            {
                foreach (var kv in edited)
                {
                    string original = originals[kv.Key].Text;
                    if (string.Equals(original, kv.Value, StringComparison.Ordinal)) continue;
                    SourceState state = originals[kv.Key];
                    var utf = new UTF8Encoding(state.Bom);
                    File.WriteAllText(Absolute(kv.Key), kv.Value.Replace("\n", state.Newline), utf);
                    written.Add(kv.Key);
                }
            }
            catch (Exception e)
            {
                foreach (string path in written)
                {
                    File.Copy(Path.Combine(backup, path.Replace('/', Path.DirectorySeparatorChar)), Absolute(path), true);
                }
                Debug.LogError("[Cloudy Map V2] Не удалось применить изменения; затронутые скрипты восстановлены. " + e);
                return;
            }
        }

        // Ensure scene objects actually opt in, even when prefab serialization left a flag off.
        int enabled = 0;
        foreach (var map in UnityEngine.Object.FindObjectsByType<HudMinimap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (map == null) continue;
            if (map.cloudyMapV2 && map.candyStyle) continue;
            Undo.RecordObject(map, "Enable Cloudy Map V2");
            map.cloudyMapV2 = true;
            map.candyStyle = true;
            EditorUtility.SetDirty(map);
            if (PrefabUtility.IsPartOfPrefabInstance(map)) PrefabUtility.RecordPrefabInstancePropertyModifications(map);
            if (map.gameObject.scene.IsValid() && map.gameObject.scene.isLoaded)
                EditorSceneManager.MarkSceneDirty(map.gameObject.scene);
            enabled++;
        }
        CloudyMapV2Baker.RepairImports();
        AssetDatabase.SaveAssets();
        Debug.Log("[Cloudy Map V2] АКТИВИРОВАНО: добавлено вызовов " + added + ", уже были " + already +
            ", включено компонентов карты " + enabled +
            (backup != null ? ". Backup: " + backup : ". Исходники уже были подключены.") +
            " Сохрани сцену Ctrl+S. Если C# скрипты изменились, дождись перекомпиляции перед Play.");
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Cloudy Map V2",
            "Подключения проверены: " + Hooks.Length + "/" + Hooks.Length +
            "\nДобавлено: " + added + "\nУже было: " + already +
            "\nФлаги карты включены: " + enabled +
            "\n\nСохрани сцену (Ctrl+S), дождись компиляции и запусти Play → M.", "OK");
    }

    [MenuItem(MenuRoot + "2 - Diagnose Cloudy Map V2", false, 101)]
    public static void Diagnose()
    {
        Dictionary<string, SourceState> sources;
        string problem;
        if (!ReadSources(out sources, out problem))
        {
            Debug.LogError("[Cloudy Map V2] " + problem);
            return;
        }
        int ready = 0;
        foreach (Hook hook in Hooks)
        {
            bool found = Occurrences(sources[hook.Path].Text, hook.After) == 1;
            if (found) ready++;
            else Debug.LogWarning("[Cloudy Map V2] Не подключено: " + hook.Path + " -> " + hook.After.Trim());
        }
        string missing;
        bool sprites = CheckSprites(out missing);
        if (!sprites) Debug.LogWarning("[Cloudy Map V2] Нет ассетов:\n" + missing);
        int mapCount = 0;
        foreach (var map in UnityEngine.Object.FindObjectsByType<HudMinimap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            mapCount++;
            string runtime = "";
            if (Application.isPlaying)
            {
                var header = map.bigWindow != null ? map.bigWindow.Find("CloudyHeader") : null;
                runtime = ", runtime CloudyHeader=" + (header != null ? "YES" : "NO");
            }
            Debug.Log("[Cloudy Map V2] " + map.name + ": enabled=" + map.cloudyMapV2 +
                ", candyStyle=" + map.candyStyle + ", bigWindow=" + (map.bigWindow != null) + runtime, map);
        }
        Debug.Log("[Cloudy Map V2] ДИАГНОСТИКА: hooks=" + ready + "/" + Hooks.Length +
            ", PNG=" + (sprites ? "9/9" : "INCOMPLETE") + ", maps=" + mapCount +
            ". Для исправления выйди из Play и выбери '1 - Activate / Repair Cloudy Map V2'.");
    }
}
#endif
