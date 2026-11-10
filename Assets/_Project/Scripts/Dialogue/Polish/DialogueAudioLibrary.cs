using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class DialogueAudioLibrary : ScriptableObject
{
    [Serializable] public class Voice { public string id; public AudioClip clip; }
    [Serializable] public class Character
    {
        public string key, role;
        public AudioClip[] syllables;
        public Color tint = Color.white;
        public Sprite emblem;
    }
    public bool replaceLegacyVoices = true;
    [Range(0f, 1f)] public float babbleVolume = .33f;
    public Voice[] spoken;
    public Character[] characters;
    readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
    bool indexed;
    static DialogueAudioLibrary instance;
    static bool loaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { if (instance != null) instance.Invalidate(); instance = null; loaded = false; }
    public static DialogueAudioLibrary Current
    {
        get
        {
            if (!loaded) { loaded = true; instance = Resources.Load<DialogueAudioLibrary>("DialoguePolish/AudioLibrary"); }
            return instance;
        }
    }
    public AudioClip Find(string id)
    {
        if (!indexed)
        {
            indexed = true; cache.Clear();
            if (spoken != null) foreach (var entry in spoken)
                if (entry != null && !string.IsNullOrEmpty(entry.id) && entry.clip != null) cache[entry.id] = entry.clip;
        }
        AudioClip clip; return cache.TryGetValue(id, out clip) ? clip : null;
    }
    public Character Actor(string displayName)
    {
        string key = VoiceId.SpeakerKey(displayName);
        if (characters != null) foreach (var actor in characters) if (actor != null && actor.key == key) return actor;
        return null;
    }
    public void Invalidate() { indexed = false; cache.Clear(); }
    void OnEnable() { Invalidate(); }
}
