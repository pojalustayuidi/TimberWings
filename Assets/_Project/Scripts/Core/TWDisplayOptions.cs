// TimberWings Settings 2.0 — desktop display preferences with reusable resolution list.
using System;
using System.Collections.Generic;
using UnityEngine;

public static class TWDisplayOptions
{
    const string ModeKey = "tw_display_mode_v2";
    const string WidthKey = "tw_display_width_v2";
    const string HeightKey = "tw_display_height_v2";
    const string VSyncKey = "tw_display_vsync_v2";

    // 0 Window, 1 Borderless, 2 Exclusive fullscreen.
    public static int DisplayMode
    {
        get { return Mathf.Clamp(PlayerPrefs.GetInt(ModeKey, Screen.fullScreen ? 1 : 0), 0, 2); }
        set { PlayerPrefs.SetInt(ModeKey, Mathf.Clamp(value, 0, 2)); ApplyStored(); }
    }
    public static bool VSync
    {
        get { return PlayerPrefs.GetInt(VSyncKey, 0) != 0; }
        set { PlayerPrefs.SetInt(VSyncKey, value ? 1 : 0); QualitySettings.vSyncCount = value ? 1 : 0; }
    }

    static List<Vector2Int> GetResolutions()
    {
        var result = new List<Vector2Int>();
        var found = new HashSet<string>();
        foreach (var resolution in Screen.resolutions)
        {
            if (resolution.width < 800 || resolution.height < 600) continue;
            string key = resolution.width + "x" + resolution.height;
            if (found.Add(key)) result.Add(new Vector2Int(resolution.width, resolution.height));
        }
        if (result.Count == 0) result.Add(new Vector2Int(Mathf.Max(800, Screen.width), Mathf.Max(600, Screen.height)));
        result.Sort((a, b) => { int cmp = a.x.CompareTo(b.x); return cmp == 0 ? a.y.CompareTo(b.y) : cmp; });
        return result;
    }

    public static string[] ResolutionLabels()
    {
        var resolutions = GetResolutions();
        var labels = new string[resolutions.Count];
        for (int i = 0; i < resolutions.Count; i++) labels[i] = resolutions[i].x + " × " + resolutions[i].y;
        return labels;
    }
    public static int ResolutionIndex
    {
        get
        {
            var list = GetResolutions();
            int w = PlayerPrefs.GetInt(WidthKey, Screen.width);
            int h = PlayerPrefs.GetInt(HeightKey, Screen.height);
            for (int i = 0; i < list.Count; i++) if (list[i].x == w && list[i].y == h) return i;
            int best = 0;
            long diff = long.MaxValue;
            for (int i = 0; i < list.Count; i++)
            {
                long dx = (long)list[i].x - w, dy = (long)list[i].y - h;
                long d = dx * dx + dy * dy;
                if (d < diff) { diff = d; best = i; }
            }
            return best;
        }
        set
        {
            var list = GetResolutions();
            int i = Mathf.Clamp(value, 0, list.Count - 1);
            PlayerPrefs.SetInt(WidthKey, list[i].x);
            PlayerPrefs.SetInt(HeightKey, list[i].y);
            ApplyStored();
        }
    }

    public static void Restore(int oldMode, int oldResolutionIndex)
    {
        // Setting these independently is safe: the final operation applies the exact previous pair.
        DisplayMode = oldMode;
        ResolutionIndex = oldResolutionIndex;
        PlayerPrefs.Save();
    }

    public static void ApplyStored()
    {
        if (Application.isMobilePlatform || Application.platform == RuntimePlatform.WebGLPlayer) return;
        if (!PlayerPrefs.HasKey(ModeKey) && !PlayerPrefs.HasKey(WidthKey))
        {
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            return;
        }
        int mode = Mathf.Clamp(PlayerPrefs.GetInt(ModeKey, Screen.fullScreen ? 1 : 0), 0, 2);
        FullScreenMode fullscreen = mode == 2 ? FullScreenMode.ExclusiveFullScreen
            : mode == 1 ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        var r = GetResolutions()[ResolutionIndex];
        // Borderless uses desktop dimensions on platforms that ignore a requested resolution.
        Screen.SetResolution(r.x, r.y, fullscreen);
        QualitySettings.vSyncCount = VSync ? 1 : 0;
    }
}
