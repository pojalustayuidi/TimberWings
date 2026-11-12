using UnityEngine;

// Extension to the EXISTING StoryInspection partial class. No copy of its cutscene
// logic, dialogue or camera is needed. V5 can hand back a correct recovery pose.
public sealed partial class StoryInspection
{
    internal bool TWRibbonCameraActive => current == this && subject == Kind.Ribbon &&
        beat != Beat.Approach && !releasing && !SkipRequested && cameraOpened && camera != null;

    internal Camera TWRibbonCamera => camera;
    internal Vector3 TWRibbonWorldFocus => Focus;

    internal void TWRibbonApplyCinematicPose(Vector3 position, Quaternion rotation, float lens)
    {
        if (!TWRibbonCameraActive) return;
        inspectionPose = new CameraPose { position = position, rotation = rotation, lens = lens };
        ApplyCamera(inspectionPose);
    }
}
