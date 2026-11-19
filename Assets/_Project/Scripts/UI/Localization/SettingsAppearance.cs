// TimberWings Settings A: preferences used by the pause/settings window.
// These affect real systems: periodic timed saves (SaveAutoRunner), window size,
// dim opacity, opening animations and the settings window's pastel appearance.
using UnityEngine;

public static class SettingsAppearance
{
    const string ThemeKey = "tw_settings_a_theme";
    const string ScaleKey = "tw_settings_a_scale";
    const string HudScaleKey = "tw_settings_hud_scale";
    const string DimKey = "tw_settings_a_dim";
    const string AnimationKey = "tw_settings_a_motion";
    const string PeriodicSaveKey = "tw_settings_a_periodic_save";
    static float hudScale;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetHudScaleCache() => hudScale = 0f;

    public static int Theme
    {
        get { return Mathf.Clamp(PlayerPrefs.GetInt(ThemeKey, 0), 0, 1); }
        set { PlayerPrefs.SetInt(ThemeKey, Mathf.Clamp(value, 0, 1)); }
    }

    public static float MenuScale
    {
        get { return Mathf.Clamp(PlayerPrefs.GetFloat(ScaleKey, 1f), .85f, 1.05f); }
        set { PlayerPrefs.SetFloat(ScaleKey, Mathf.Clamp(value, .85f, 1.05f)); }
    }

    public static float HudScale
    {
        get
        {
            if (hudScale == 0f) hudScale = Mathf.Clamp(PlayerPrefs.GetFloat(HudScaleKey, 1f), .85f, 1.15f);
            return hudScale;
        }
        set
        {
            hudScale = Mathf.Clamp(value, .85f, 1.15f);
            PlayerPrefs.SetFloat(HudScaleKey, hudScale);
        }
    }

    public static float Backdrop
    {
        get { return Mathf.Clamp(PlayerPrefs.GetFloat(DimKey, .52f), .30f, .80f); }
        set { PlayerPrefs.SetFloat(DimKey, Mathf.Clamp(value, .30f, .80f)); }
    }

    public static bool AnimateOpen
    {
        get { return PlayerPrefs.GetInt(AnimationKey, 1) != 0; }
        set { PlayerPrefs.SetInt(AnimationKey, value ? 1 : 0); }
    }

    // Disables only periodic time-based save. Event/debounce saves and writes
    // before return-to-menu/exit remain enabled for progress safety.
    public static bool PeriodicAutosave
    {
        get { return PlayerPrefs.GetInt(PeriodicSaveKey, 1) != 0; }
        set { PlayerPrefs.SetInt(PeriodicSaveKey, value ? 1 : 0); }
    }
}
