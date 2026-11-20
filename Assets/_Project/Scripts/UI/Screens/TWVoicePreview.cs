// TimberWings Settings 2.0 — independent RU/EN voice samples.
// Optional audio files: Assets/Resources/TimberWings/VoicePreview/ru.wav and en.wav
using UnityEngine;

public static class TWVoicePreview
{
    static AudioSource source;

    // Returns true if the requested language has a real, playable preview clip.
    public static bool Play(bool english)
    {
        var clip = Resources.Load<AudioClip>(english ? "TimberWings/VoicePreview/en" : "TimberWings/VoicePreview/ru");
        if (clip == null) return false;
        if (source == null)
        {
            var go = new GameObject("TWVoicePreviewAudio");
            Object.DontDestroyOnLoad(go);
            source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
        }
        source.Stop();
        source.clip = clip;
        source.volume = Mathf.Max(0f, GameAudio.VoiceLevel);
        source.Play();
        return true;
    }

    public static void Stop()
    {
        if (source != null) source.Stop();
    }
}
