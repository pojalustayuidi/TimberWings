using System;
using System.Collections.Generic;
using UnityEngine;

// Data only. Chapter order, rewards and save keys stay in IntroFlow/StoryFlow.
public static class DialogueStorybook
{
    [Serializable] public class Book { public Scene[] scenes; }
    [Serializable] public class Scene { public string id, title, npc; public Entry[] lines; }
    [Serializable] public class Entry { public bool hero; public string text; public int priority = 2; public string direction; }
    static Book book;
    static bool loaded;
    static readonly Dictionary<string, Scene> byId = new Dictionary<string, Scene>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { book = null; loaded = false; byId.Clear(); }

    public static Book Data
    {
        get
        {
            if (!loaded)
            {
                loaded = true;
                var asset = Resources.Load<TextAsset>("DialoguePolish/Script");
                if (asset != null)
                {
                    try { book = JsonUtility.FromJson<Book>(asset.text); }
                    catch (Exception ex) { Debug.LogWarning("Dialogue script: " + ex.Message); }
                }
                if (book != null && book.scenes != null)
                    foreach (var scene in book.scenes) if (scene != null && !string.IsNullOrEmpty(scene.id)) byId[scene.id] = scene;
            }
            return book;
        }
    }

    public static DialogueSequence.Line[] Lines(string id, string hero, DialogueSequence.Line[] fallback)
    {
        var data = Data;
        Scene scene;
        if (data == null || !byId.TryGetValue(id, out scene) || scene.lines == null || scene.lines.Length == 0) return fallback;
        var result = new List<DialogueSequence.Line>(scene.lines.Length);
        foreach (var line in scene.lines)
            if (line != null && !string.IsNullOrWhiteSpace(line.text))
                result.Add(new DialogueSequence.Line { speaker = line.hero ? hero : "", text = line.text, pauseAfter = .15f });
        return result.Count > 0 ? result.ToArray() : fallback;
    }

    public static string TitleFor(DialogueSequence.Line[] lines)
    {
        var data = Data;
        if (data == null || data.scenes == null || lines == null || lines.Length == 0 || lines[0] == null) return "";
        foreach (var scene in data.scenes)
            if (scene != null && scene.lines != null && scene.lines.Length > 0 && scene.lines[0] != null && scene.lines[0].text == lines[0].text)
                return scene.title ?? "";
        return "";
    }
}
