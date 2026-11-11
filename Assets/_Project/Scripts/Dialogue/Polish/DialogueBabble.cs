using System.Collections.Generic;
using UnityEngine;

// Expressive nonverbal character chatter. Cached sounds; no synthesis on the audio thread.
// DialogueManager, MiraPresence and PlayerThoughtController call Letter as text appears.
[DisallowMultipleComponent]
public sealed class DialogueBabble : MonoBehaviour
{
    [Header("Character chatter")]
    public AudioSource source;
    [Tooltip("Use the old imported syllable WAVs (if available) instead of new voices.")]
    public bool useOriginalSyllables;
    [Range(0.5f, 1.5f)] public float characterVolume = 1f;
    [Range(0f, 1f)] public float expressiveness = 0.85f;

    const int SampleRate = 22050;
    const int Variants = 8;
    const float FadeSeconds = 0.065f;

    struct VoiceStyle
    {
        public float pitch, airy, rough, bright, tempo, level;
        public VoiceStyle(float pitch, float airy, float rough, float bright, float tempo, float level)
        {
            this.pitch = pitch; this.airy = airy; this.rough = rough;
            this.bright = bright; this.tempo = tempo; this.level = level;
        }
    }

    static readonly Dictionary<string, AudioClip[]> generated = new Dictionary<string, AudioClip[]>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ClearCache() { generated.Clear(); }

    static VoiceStyle Profile(string key)
    {
        switch (key)
        {
            case "leo":      return new VoiceStyle(205f, 0.05f, 0.02f, 0.53f, 1.00f, 0.92f); // warm, curious
            case "mira":     return new VoiceStyle(292f, 0.13f, 0.00f, 0.83f, 0.90f, 0.85f); // light, melodic
            case "oskar":    return new VoiceStyle(145f, 0.03f, 0.07f, 0.29f, 1.23f, 0.98f); // low, wooden
            case "rico":     return new VoiceStyle(255f, 0.07f, 0.035f, 0.92f, 0.82f, 0.82f); // cheeky, agile
            case "gunnar":   return new VoiceStyle(118f, 0.035f, 0.13f, 0.27f, 1.38f, 0.98f); // deep, weathered
            case "tais":     return new VoiceStyle(270f, 0.10f, 0.01f, 0.69f, 1.02f, 0.85f);
            case "merchant": return new VoiceStyle(180f, 0.035f, 0.04f, 0.72f, 0.91f, 0.90f);
            default:         return new VoiceStyle(196f, 0.06f, 0.03f, 0.49f, 1.05f, 0.87f);
        }
    }

    // Vowel-shaped harmonic voices, gentle attack/release and a little breath/grit.
    // Rendering eight tiny variations per character avoids the old repetitive triangle beep.
    static AudioClip Render(string key, VoiceStyle style, int variant)
    {
        float length = (0.088f + (variant % 3) * 0.012f) * style.tempo;
        int count = Mathf.CeilToInt(length * SampleRate);
        float[] samples = new float[count];
        float[] vowel1 = { 440f, 700f, 320f, 520f, 360f, 650f, 480f, 800f };
        float[] vowel2 = { 1040f, 1260f, 1880f, 920f, 1450f, 1650f, 1120f, 1810f };
        float f0 = style.pitch * (1f + (variant % 5 - 2) * 0.034f);
        float formant1 = vowel1[variant], formant2 = vowel2[variant];
        float[] weights = new float[14];
        float sum = 0f;
        for (int h = 1; h <= weights.Length; h++)
        {
            float hz = h * f0;
            float a = Mathf.Exp(-Mathf.Pow((hz - formant1) / 290f, 2f));
            float b = Mathf.Exp(-Mathf.Pow((hz - formant2) / 410f, 2f));
            float w = (0.13f + a + (0.45f + style.bright * 0.75f) * b) / Mathf.Pow(h, 0.72f);
            weights[h - 1] = w;
            sum += w;
        }
        uint noiseState = (uint)(12817 + variant * 1297 + (int)(style.pitch * 19f));
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)SampleRate;
            float u = t / length;
            float glide = variant % 4 == 0 ? 0.13f : variant % 4 == 1 ? -0.11f : 0.03f;
            float phase = 2f * Mathf.PI * f0 * (t + glide * t * u * 0.5f);
            float voice = 0f;
            for (int h = 1; h <= weights.Length; h++)
                voice += weights[h - 1] * Mathf.Sin(phase * h + (h % 3) * 0.13f);
            voice /= Mathf.Max(0.01f, sum);
            noiseState ^= noiseState << 13; noiseState ^= noiseState >> 17; noiseState ^= noiseState << 5;
            float noise = (noiseState / (float)uint.MaxValue) * 2f - 1f;
            float wobble = 0.92f + 0.08f * Mathf.Sin(2f * Mathf.PI * (6f + variant % 3) * t);
            float attack = Mathf.Clamp01(t / 0.009f);
            float release = Mathf.Clamp01((length - t) / 0.027f);
            float envelope = attack * release * (0.90f + 0.10f * Mathf.Sin(Mathf.PI * u));
            float grit = style.rough * Mathf.Sin(phase * 0.51f) * voice;
            samples[i] = Mathf.Clamp((voice * wobble + grit + noise * style.airy * 0.24f)
                                      * envelope * 0.77f, -0.93f, 0.93f);
        }
        var clip = AudioClip.Create("TW_Chatter_" + key + "_" + variant, count, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    static AudioClip[] Generated(string key)
    {
        AudioClip[] clips;
        if (generated.TryGetValue(key, out clips)) return clips;
        var style = Profile(key);
        clips = new AudioClip[Variants];
        for (int i = 0; i < clips.Length; i++) clips[i] = Render(key, style, i);
        generated.Add(key, clips);
        return clips;
    }

    // The options preview uses the same sound as a real dialogue line.
    public static AudioClip PreviewClip(string speaker)
    {
        return Generated(VoiceId.SpeakerKey(speaker))[3];
    }

    DialogueAudioLibrary library;
    DialogueAudioLibrary.Character actor;
    string voiceKey;
    VoiceStyle style;
    int lettersSinceSound, phraseIndex, lastVariant = -1, punctuationRise;
    float nextAt, gain, targetGain;
    bool enabledForLine, paused;
    public bool Speaking { get { return source != null && source.isPlaying && gain > 0.015f; } }

    void Awake()
    {
        if (source == null) source = GetComponent<AudioSource>();
        if (source == null) source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.loop = false;
        source.dopplerLevel = 0f;
    }

    public void BeginLine(string speaker, bool spoken)
    {
        StopNow();
        library = DialogueAudioLibrary.Current;
        actor = library != null ? library.Actor(speaker) : null;
        voiceKey = VoiceId.SpeakerKey(speaker);
        style = Profile(voiceKey);
        enabledForLine = !spoken;
        lettersSinceSound = phraseIndex = 0;
        punctuationRise = 0;
        lastVariant = -1;
        nextAt = Time.unscaledTime + 0.045f;
    }

    public void Letter(string text, int index)
    {
        if (!enabledForLine || paused || source == null || string.IsNullOrEmpty(text)
            || index < 0 || index >= text.Length || !GameAudio.UseBabble
            || GameAudio.VoiceLevel <= 0.001f || Time.timeScale <= 0f) return;

        char c = text[index];
        float now = Time.unscaledTime;
        if (c == ',' || c == ';' || c == ':' || c == '—' || c == '–')
        {
            nextAt = Mathf.Max(nextAt, now + 0.16f * style.tempo);
            lettersSinceSound = 0;
            return;
        }
        if (c == '.' || c == '!' || c == '?' || c == '…')
        {
            nextAt = Mathf.Max(nextAt, now + 0.25f * style.tempo);
            punctuationRise = c == '?' ? 2 : c == '!' ? 1 : -1;
            lettersSinceSound = 0;
            return;
        }
        if (!char.IsLetter(c)) return;

        lettersSinceSound++;
        int lettersPerSound = 2 + ((phraseIndex + (int)style.pitch) % 3);
        if (lettersSinceSound < lettersPerSound || now < nextAt || source.isPlaying) return;

        AudioClip[] clips = null;
        if (useOriginalSyllables && actor != null) clips = actor.syllables;
        if (clips == null || clips.Length == 0) clips = Generated(voiceKey);
        int choice = (index * 7 + phraseIndex * 13 + c) & 0x7fffffff;
        int variant = choice % clips.Length;
        if (variant == lastVariant && clips.Length > 1) variant = (variant + 1) % clips.Length;
        AudioClip clip = clips[variant];
        if (clip == null) clip = Generated(voiceKey)[variant % Variants];
        lastVariant = variant;

        float swing = Mathf.Sin(phraseIndex * 1.92f) * (0.068f * expressiveness);
        int ending = punctuationRise;
        // The question mark arrives after the spoken letters, so anticipate it.
        for (int i = index + 1; i < text.Length && i <= index + 7; i++)
        {
            if (text[i] == '?') { ending = 2; break; }
            if (text[i] == '!') { ending = 1; break; }
            if (text[i] == '.' || text[i] == '…') { ending = -1; break; }
        }
        float emphasis = ending * 0.035f * expressiveness;
        float inflection = char.IsUpper(c) && index > 0 ? 0.035f : 0f;
        source.clip = clip;
        source.pitch = Mathf.Clamp(1f + swing + emphasis + inflection, 0.82f, 1.19f);
        gain = targetGain = 1f;
        source.volume = Volume();
        source.Play();
        phraseIndex++;
        punctuationRise = 0;
        lettersSinceSound = 0;
        nextAt = now + (0.070f + (phraseIndex % 3) * 0.016f) * style.tempo;
    }

    float Volume()
    {
        return GameAudio.VoiceLevel * (library != null ? library.babbleVolume : 0.33f)
               * characterVolume * style.level * gain;
    }

    public void SetPaused(bool value)
    {
        if (source == null || paused == value) return;
        paused = value;
        if (paused) source.Pause();
        else source.UnPause();
    }

    public void EndLine()
    {
        enabledForLine = false;
        targetGain = 0f;
    }

    public void StopNow()
    {
        enabledForLine = false;
        gain = targetGain = 0f;
        paused = false;
        if (source != null) { source.Stop(); source.clip = null; }
    }

    void Update()
    {
        if (source == null) return;
        if (!GameAudio.UseBabble || GameAudio.VoiceLevel <= 0.001f) { StopNow(); return; }
        if (!source.isPlaying || paused) return;
        gain = Mathf.MoveTowards(gain, targetGain, Time.unscaledDeltaTime / FadeSeconds);
        source.volume = Volume();
        if (gain <= 0.001f) source.Stop();
    }

    void OnDisable() { StopNow(); }
}
