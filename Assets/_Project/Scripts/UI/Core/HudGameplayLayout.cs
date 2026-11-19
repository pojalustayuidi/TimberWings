using UnityEngine;
using UnityEngine.UI;

internal static class HudGameplayLayout
{
    internal const float Edge = 32f;
    internal const float ShortcutSize = 70f;
    internal const float ShortcutPitch = 82f;

    internal static bool Blocked => !Application.isFocused || Time.timeScale <= 0f ||
        MobileInput.ModalOpen || DialogueManager.IsBusy || DialogueSequence.AnyPlaying ||
        StoryInspection.Active || ArrivalIntroSequence.Active || EndingSequence.Active ||
        SaveSystem.IsLoading || BuildingController.AnyRestorePending || BuildingController.AnyRevealing ||
        BuildWorkPose.MovementLocked || CameraVibe.RevealActive || CameraVibe.RevealShown > .01f ||
        LeoCelebration.IsPlaying || CampSleep.Sleeping || WaterSwim.Swimming ||
        (IslandEdgeGuard.Instance != null && IslandEdgeGuard.Instance.IsFalling);

    internal static Rect SafeArea
    {
        get
        {
            Rect area = Screen.safeArea;
            return area.width > 0f && area.height > 0f ? area : new Rect(0f, 0f, Screen.width, Screen.height);
        }
    }

    internal static void Configure(CanvasScaler scaler)
    {
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = .5f;
    }

    internal static float Fit(Canvas canvas, float width, float widthShare)
    {
        float max = SafeArea.width * widthShare / (Mathf.Max(.01f, canvas.scaleFactor) * width);
        return Mathf.Max(.25f, Mathf.Min(SettingsAppearance.HudScale, max));
    }

    internal static void Dock(RectTransform rect, Canvas canvas, bool right, float step = 0f)
    {
        Rect safe = SafeArea;
        float scale = Mathf.Max(.01f, canvas.scaleFactor);
        Vector2 anchor = right ? Vector2.right : Vector2.zero;
        rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
        float margin = Mathf.Min(Edge, safe.width * .045f / scale);
        float x = right ? (safe.xMax - Screen.width) / scale - margin - step : safe.xMin / scale + margin;
        rect.anchoredPosition = new Vector2(x, safe.yMin / scale + margin);
    }
}
