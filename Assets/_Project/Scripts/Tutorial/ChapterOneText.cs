using System;
using System.Collections.Generic;
using UnityEngine;

// Dialogue keeps its stable Russian source key and selects display text and audio at playback.
internal static class ChapterOneText
{
    [Serializable]
    internal sealed class Cue
    {
        public string id, speaker, ru, en, audio, audioEn;
    }

    [Serializable]
    sealed class Translation
    {
        public string id, ru, en;
    }

    [Serializable]
    sealed class Catalog
    {
        public Cue[] lines;
        public Translation[] ui, legacy;
    }

    static readonly Dictionary<string, Cue> ById = new(StringComparer.Ordinal);
    static readonly Dictionary<string, Cue> ByRussian = new(StringComparer.Ordinal);
    static readonly Dictionary<string, Translation> UI = new(StringComparer.Ordinal);
    static readonly Dictionary<string, string> Legacy = new(StringComparer.Ordinal);
    static readonly Dictionary<string, AudioClip> Clips = new(StringComparer.Ordinal);
    static bool loaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        ById.Clear();
        ByRussian.Clear();
        UI.Clear();
        Legacy.Clear();
        Clips.Clear();
        loaded = false;
    }

    static void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;

        var asset = Resources.Load<TextAsset>("ChapterOne/Text");
        if (asset == null) return;

        var data = JsonUtility.FromJson<Catalog>(asset.text);
        if (data == null) return;
        foreach (var cue in data.lines ?? Array.Empty<Cue>())
        {
            ById[cue.id] = cue;
            ByRussian[VoiceId.Make(cue.speaker, cue.ru)] = cue;
        }
        foreach (var item in data.ui ?? Array.Empty<Translation>()) UI[item.id] = item;
        foreach (var item in data.legacy ?? Array.Empty<Translation>()) Legacy[item.ru] = item.en;
    }

    internal static string Pick(string russian, string english) => GameLanguage.English ? english : russian;

    internal static string Text(string id, string russian = "")
    {
        EnsureLoaded();
        if (!UI.TryGetValue(id, out var item)) return russian;
        if (GameLanguage.English) return item.en;
        return string.IsNullOrEmpty(item.ru) ? russian : item.ru;
    }

    internal static string Format(string id, string russian, params (string name, object value)[] parts)
    {
        var result = Text(id, russian);
        foreach (var part in parts)
            result = result.Replace("{" + part.name + "}", Convert.ToString(part.value, System.Globalization.CultureInfo.InvariantCulture));
        return result;
    }

    internal static string TranslateLegacy(string russian)
    {
        EnsureLoaded();
        return Legacy.TryGetValue(russian, out var english) ? english : russian;
    }

    internal static string SpeakerName(string russian)
    {
        if (string.IsNullOrEmpty(russian)) return russian;
        return Text("speaker." + VoiceId.SpeakerKey(russian), GameLanguage.T(russian));
    }

    internal static Cue Find(string id)
    {
        EnsureLoaded();
        return ById.TryGetValue(id, out var cue) ? cue : null;
    }

    internal static Cue Find(string speaker, string russian)
    {
        EnsureLoaded();
        return ByRussian.TryGetValue(VoiceId.Make(speaker, russian), out var cue) ? cue : null;
    }

    internal static string AudioPath(Cue cue) => cue == null ? null : GameLanguage.English ? cue.audioEn : cue.audio;

    internal static AudioClip Audio(Cue cue)
    {
        string path = AudioPath(cue);
        if (string.IsNullOrEmpty(path)) return null;
        if (Clips.TryGetValue(path, out var clip)) return clip;
        clip = Resources.Load<AudioClip>(path);
        Clips[path] = clip;
        return clip;
    }

    internal static string Subtitle(string speaker, string russian)
    {
        if (!GameLanguage.English || string.IsNullOrEmpty(russian)) return russian;
        return Find(speaker, russian)?.en ?? GameLanguage.T(russian);
    }

    internal static DialogueSequence.Line[] Lines(params string[] ids)
    {
        var lines = new List<DialogueSequence.Line>(ids.Length);
        foreach (var id in ids)
        {
            var cue = Find(id);
            if (cue == null) continue;
            lines.Add(new DialogueSequence.Line { speaker = cue.speaker, text = cue.ru, pauseAfter = .25f });
        }
        return lines.ToArray();
    }
}
