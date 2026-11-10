using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class PerformanceBoot
{
    public const int TargetFps = 60;

    const string FpsKey = "tw_graphics_fps";
    const string FogKey = "tw_graphics_distance_fog";
    const string ShadowsKey = "tw_graphics_shadows";
    const string ScaleKey = "tw_graphics_render_scale";
    const string FullscreenKey = "tw_graphics_fullscreen";
    const string MouseKey = "tw_camera_mouse_multiplier";
    const string CounterKey = "tw_settings_fps_counter";

    static readonly int[] FrameLimits = { 30, TargetFps, 90, 120 };

    public static bool DefaultFullscreen { get; private set; }
    public static bool SupportsRenderScale => Pipeline != null;
    static UniversalRenderPipelineAsset Pipeline => GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

    public static int FpsIndex
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(FpsKey, 1), 0, FrameLimits.Length - 1);
        set
        {
            PlayerPrefs.SetInt(FpsKey, Mathf.Clamp(value, 0, FrameLimits.Length - 1));
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = FrameLimits[FpsIndex];
        }
    }

    public static bool DistanceFog
    {
        get => PlayerPrefs.GetInt(FogKey, 1) == 1;
        set
        {
            PlayerPrefs.SetInt(FogKey, value ? 1 : 0);
            RenderSettings.fog = value;
        }
    }

    public static bool Shadows
    {
        get => PlayerPrefs.GetInt(ShadowsKey, 1) == 1;
        set
        {
            PlayerPrefs.SetInt(ShadowsKey, value ? 1 : 0);
            if (LightingRig.Instance != null) LightingRig.Instance.RefreshShadowDistance();
        }
    }

    public static float RenderScale
    {
        get
        {
            var pipeline = Pipeline;
            return Mathf.Clamp(PlayerPrefs.GetFloat(ScaleKey, pipeline != null ? pipeline.renderScale : 1f), 0.5f, 1f);
        }
        set
        {
            PlayerPrefs.SetFloat(ScaleKey, Mathf.Clamp(value, 0.5f, 1f));
            ApplyRenderScale();
        }
    }

    public static bool Fullscreen
    {
        get => PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
        set
        {
            PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
            Screen.fullScreen = value;
        }
    }

    public static float MouseMultiplier
    {
        get => Mathf.Clamp(PlayerPrefs.GetFloat(MouseKey, 1f), 0.25f, 2f);
        set => PlayerPrefs.SetFloat(MouseKey, Mathf.Clamp(value, 0.25f, 2f));
    }

    public static bool ShowSettingsFps
    {
        get => PlayerPrefs.GetInt(CounterKey, 1) == 1;
        set => PlayerPrefs.SetInt(CounterKey, value ? 1 : 0);
    }

    static void ApplyRenderScale()
    {
        var pipeline = Pipeline;
        if (pipeline != null && PlayerPrefs.HasKey(ScaleKey))
            pipeline.renderScale = RenderScale;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = FrameLimits[FpsIndex];
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        DefaultFullscreen = Screen.fullScreen;
        if (PlayerPrefs.HasKey(FullscreenKey) && !Application.isMobilePlatform &&
            Application.platform != RuntimePlatform.WebGLPlayer)
            Screen.fullScreen = Fullscreen;
        ApplyRenderScale();
    }
}
