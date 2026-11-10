using UnityEngine;

// Short authored syllables only while text appears. Never changes dialogue duration.
[DisallowMultipleComponent]
public sealed class DialogueBabble : MonoBehaviour
{
    public AudioSource source;
    DialogueAudioLibrary library;
    DialogueAudioLibrary.Character actor;
    float nextAt, gain, targetGain;
    bool enabledForLine;
    public bool Speaking { get { return source != null && source.isPlaying && targetGain > 0f; } }
    void Awake()
    {
        if (source == null) source = GetComponent<AudioSource>();
        if (source == null) source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false; source.spatialBlend = 0; source.loop = false;
    }
    public void BeginLine(string speaker, bool spoken)
    {
        StopNow(); library = DialogueAudioLibrary.Current;
        actor = library != null ? library.Actor(speaker) : null;
        enabledForLine = !spoken && actor != null && actor.syllables != null && actor.syllables.Length > 0;
        nextAt = Time.unscaledTime;
    }
    public void Letter(string text, int index)
    {
        if (!enabledForLine || source == null || string.IsNullOrEmpty(text) || index < 0 || index >= text.Length) return;
        char c = text[index];
        if (c == '.' || c == ',' || c == '!' || c == '?' || c == '…') { nextAt = Mathf.Max(nextAt, Time.unscaledTime + .12f); return; }
        if (!char.IsLetter(c) || !GameAudio.UseBabble || GameAudio.VoiceLevel <= .001f || Time.unscaledTime < nextAt || source.isPlaying) return;
        int pick = (c * 7 + index * 11) % actor.syllables.Length;
        var clip = actor.syllables[pick]; if (clip == null) return;
        source.clip = clip; source.pitch = .97f + ((index + c) % 4) * .02f;
        gain = targetGain = 1f; source.volume = GameAudio.VoiceLevel * library.babbleVolume;
        source.Play(); nextAt = Time.unscaledTime + Mathf.Max(.1f, clip.length / source.pitch + .025f);
    }
    public void EndLine() { enabledForLine = false; targetGain = 0; }
    public void StopNow() { enabledForLine = false; gain = targetGain = 0; if (source != null) source.Stop(); }
    void Update()
    {
        if (source == null || !source.isPlaying) return;
        if (!GameAudio.UseBabble || GameAudio.VoiceLevel <= .001f) { StopNow(); return; }
        gain = Mathf.MoveTowards(gain, targetGain, Time.unscaledDeltaTime / .09f);
        source.volume = gain * GameAudio.VoiceLevel * (library != null ? library.babbleVolume : .33f);
        if (gain <= .001f) source.Stop();
    }
    void OnDisable() { StopNow(); }
}
