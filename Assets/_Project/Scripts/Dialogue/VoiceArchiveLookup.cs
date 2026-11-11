// TimberWings Voice 172: fallback for lines imported from Gemini.
// Normal game lines are loaded by VoiceBank using VoiceId.Make. This lookup is only
// required for the legacy dynamic_* files and speaker aliases; it never replaces
// a clip already found by VoiceBank.
using System;
using System.Collections.Generic;
using UnityEngine;

public static class VoiceArchiveLookup
{
    [Serializable] sealed class Manifest { public Entry[] lines; }
    [Serializable] sealed class Entry { public string id, character, text; }

    static readonly Dictionary<string, string> named = new Dictionary<string, string>(StringComparer.Ordinal);
    static readonly Dictionary<string, string> oskarGeneric = new Dictionary<string, string>(StringComparer.Ordinal);
    static bool loaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { named.Clear(); oskarGeneric.Clear(); loaded = false; }

    static void Ensure()
    {
        if (loaded) return;
        loaded = true;
        var asset = Resources.Load<TextAsset>("Voice21/voice_archive_manifest");
        if (asset == null) return;
        Manifest data;
        try { data = JsonUtility.FromJson<Manifest>(asset.text); }
        catch (Exception e) { Debug.LogWarning("Voice172: manifest invalid: " + e.Message); return; }
        if (data == null || data.lines == null) return;
        foreach (var entry in data.lines)
        {
            if (entry == null || string.IsNullOrEmpty(entry.id) || string.IsNullOrEmpty(entry.text)) continue;
            string text = VoiceId.Normalize(entry.text);
            // These 21 dynamic clips were all recorded with Charon (Oskar).
            // Do not assign his voice to Mira or Rico accidentally.
            if (entry.id.StartsWith("dynamic_", StringComparison.Ordinal))
            {
                oskarGeneric[text] = entry.id;
                continue;
            }
            string speaker = entry.character == "Дядя Гуннар" ? "Гуннар" : entry.character;
            string key = VoiceId.SpeakerKey(speaker);
            if (key == "npc") continue; // Video narrator and unnamed characters have their own playback.
            named[key + "\u001f" + text] = entry.id;
        }
    }

    public static AudioClip Find(string speaker, string text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        Ensure();
        string key = VoiceId.SpeakerKey(speaker);
        if (key == "merchant") key = "rico"; // legacy merchant object is named "Торговец".
        string normalized = VoiceId.Normalize(text);
        string id;
        if (!named.TryGetValue(key + "\u001f" + normalized, out id))
        {
            if ((key != "oskar" && key != "npc") || !oskarGeneric.TryGetValue(normalized, out id)) return null;
        }
        return Resources.Load<AudioClip>("Voice21/" + id);
    }
}
