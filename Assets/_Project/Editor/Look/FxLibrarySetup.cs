// Assets/_Project/Editor/Look/FxLibrarySetup.cs
// Tools > BuilderSim > Link Cartoon FX: ищет в проекте префабы CFXR..., раскладывает их по слотам FxLibrary
// (удар, падение дерева, уровень, квест, костёр) и сохраняет Assets/_Project/Resources/FxLibrary.asset.
// Подбор идёт по ключевым словам в имени, поэтому после запуска загляни в Инспектор и поправь слоты, если выбор не по вкусу.
// В Консоль выводится весь список найденных префабов, чтобы было из чего выбирать.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using static EditorAssets;

public static class FxLibrarySetup
{
    const string AssetPath = "Assets/_Project/Resources/FxLibrary.asset";

    [MenuItem("Tools/BuilderSim/Link Cartoon FX")]
    public static void Run()
    {
        var found = new List<GameObject>();
        foreach (var guid in AssetDatabase.FindAssets("CFXR t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.IndexOf("demo", StringComparison.OrdinalIgnoreCase) >= 0) continue;
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go != null && go.name.StartsWith("CFXR", StringComparison.OrdinalIgnoreCase)) found.Add(go);
        }

        if (found.Count == 0)
        {
            EditorUtility.DisplayDialog("Cartoon FX не найден",
                "В проекте нет префабов CFXR. Сначала импортируй Cartoon FX Remaster Free: Window > Package Manager > My Assets > Cartoon FX Remaster Free > Import.", "Понятно");
            return;
        }

        EnsureFolder("Assets/_Project/Resources");
        var lib = AssetDatabase.LoadAssetAtPath<FxLibrary>(AssetPath);
        if (lib == null)
        {
            lib = ScriptableObject.CreateInstance<FxLibrary>();
            AssetDatabase.CreateAsset(lib, AssetPath);
        }

        // ключевые слова по приоритету: сначала ищем первое, если нет — следующее
        lib.chopHit = Pick(found, new[] { "wood", "hit", "impact" }, new[] { "explosion", "fireball", "fire", "firework" });
        lib.treeFall = Pick(found, new[] { "dust", "poof", "smoke" }, new[] { "fire", "explosion" });
        lib.levelUp = Pick(found, new[] { "level", "star", "sparkle", "magic", "heal", "aura" }, new[] { "explosion", "fire" });
        lib.questDone = Pick(found, new[] { "firework", "confetti", "sparkle", "star", "magic" }, new string[0]);
        lib.campfire = Pick(found, new[] { "campfire", "torch", "flame", "fire" }, new[] { "fireball", "firework", "explosion", "ball", "shot" });

        lib.stageBuilt = Pick(found, new[] { "poof", "puff", "smoke", "dust" }, new[] { "fire", "explosion" });
        lib.buildDone = Pick(found, new[] { "firework", "confetti", "magic", "sparkle", "star" }, new string[0]);

        EditorUtility.SetDirty(lib);
        AssetDatabase.SaveAssets();
        Selection.activeObject = lib;
        EditorGUIUtility.PingObject(lib);

        var names = new List<string>();
        foreach (var g in found) names.Add(g.name);
        names.Sort();
        Debug.Log("[FxLibrary] Выбрано: удар = " + N(lib.chopHit) + ", падение = " + N(lib.treeFall) + ", уровень = " + N(lib.levelUp)
                  + ", квест = " + N(lib.questDone) + ", костёр = " + N(lib.campfire)
                  + ", стадия = " + N(lib.stageBuilt) + ", стройка готова = " + N(lib.buildDone)
                  + "\nВсе найденные префабы (" + names.Count + "):\n" + string.Join("\n", names));
    }

    static string N(GameObject g) { return g != null ? g.name : "(пусто)"; }

    static GameObject Pick(List<GameObject> all, string[] keys, string[] exclude)
    {
        foreach (var k in keys)
        {
            GameObject best = null;
            foreach (var g in all)
            {
                string n = g.name.ToLowerInvariant();
                if (!n.Contains(k)) continue;
                bool bad = false;
                foreach (var e in exclude) if (n.Contains(e)) { bad = true; break; }
                if (bad) continue;
                if (best == null || g.name.Length < best.name.Length) best = g;   // самое короткое имя = базовый вариант
            }
            if (best != null) return best;
        }
        return null;
    }

}
