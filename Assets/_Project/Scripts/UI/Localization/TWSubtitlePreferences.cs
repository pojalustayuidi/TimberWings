// TimberWings Settings 2.0 — persistent subtitle accessibility settings.
using UnityEngine;

public static class TWSubtitlePreferences
{
    const string ScaleKey = "tw_subtitles_scale";
    const string NamesKey = "tw_subtitles_speaker_names";

    public static float Scale
    {
        get { return Mathf.Clamp(PlayerPrefs.GetFloat(ScaleKey, 1f), .8f, 1.5f); }
        set { PlayerPrefs.SetFloat(ScaleKey, Mathf.Clamp(value, .8f, 1.5f)); }
    }

    public static bool ShowSpeakerNames
    {
        get { return PlayerPrefs.GetInt(NamesKey, 1) != 0; }
        set { PlayerPrefs.SetInt(NamesKey, value ? 1 : 0); }
    }
}
