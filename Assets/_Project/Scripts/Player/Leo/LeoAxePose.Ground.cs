using UnityEngine;

public static partial class LeoAxePose
{
    internal static Quaternion GroundTool(Vector3 toward, Vector3 logAxis, Vector3 handSide, float shaftDrop = .39f)
    {
        Vector3 up = Vector3.ProjectOnPlane(Vector3.up, logAxis).normalized;
        Vector3 across = Vector3.ProjectOnPlane(toward, logAxis).normalized;
        Vector3 side = Vector3.Dot(handSide, logAxis) >= 0f ? logAxis : -logAxis;
        Vector3 shaft = (across * .94f - up * shaftDrop - side * .26f).normalized;
        return Quaternion.LookRotation(Vector3.ProjectOnPlane(-up, shaft), shaft);
    }

    internal static void GroundBrace(LeoRig rig, float phase, float contact, float weight)
    {
        if (weight <= 0f) return;
        float load = SmoothBetween(phase, contact - .24f, contact) *
            (1f - SmoothBetween(phase, contact + .025f, .91f));
        if (rig.hips != null)
            rig.hips.localPosition += new Vector3(0f, -.057f - .093f * load, .041f + .419f * load) * weight;
        rig.Rotate(rig.spine, new Vector3(12f + 43f * load, 0f, 0f) * weight);
        rig.Rotate(rig.chest, new Vector3(6f + 7f * load, -6f * Mathf.Sin(phase * Mathf.PI), 0f) * weight);
        rig.Rotate(rig.head, new Vector3(15f - 8f * load, 0f, 0f) * weight);
    }

    internal static void GroundArms(LeoRig rig, Transform model, float phase, float contact, float weight,
        float holster, Vector3 edge, Vector3 sectionAxis)
    {
        if (weight <= 0f || rig.handR == null || rig.axeRoot == null)
        {
            Place(rig, model, holster);
            return;
        }

        Vector3 toward = Vector3.ProjectOnPlane(edge - model.position, Vector3.up).normalized;
        float height = model.InverseTransformPoint(edge).y;
        float shaftDrop = Mathf.Lerp(.41f, -.45f, Mathf.InverseLerp(.4f, 1.13f, height));
        Quaternion strike = GroundTool(toward, sectionAxis, model.right, shaftDrop);
        Vector3 impactGrip = edge - strike * Vector3.Scale(GripToEdge, rig.axeRoot.lossyScale);
        var ready = model.TransformPoint(new Vector3(.263f, .79f, .174f));
        var windup = model.TransformPoint(new Vector3(.31f, 1.14f, .14f));
        Quaternion readyTool = model.rotation * Quaternion.Euler(21f, -31f, -50f);
        Quaternion raisedTool = model.rotation * Quaternion.Euler(-11f, -27f, -27f);
        const float Loaded = .36f;
        Vector3 grip;
        Quaternion tool;
        if (phase < Loaded)
        {
            float t = SmoothBetween(phase, .035f, Loaded);
            grip = Vector3.Lerp(ready, windup, t);
            tool = Quaternion.Slerp(readyTool, raisedTool, t);
        }
        else if (phase <= contact)
        {
            float t = Mathf.Pow(Mathf.InverseLerp(Loaded, contact, phase), 1.27f);
            grip = LeoMotionProfile.Bezier(windup,
                windup + model.TransformVector(new Vector3(.10f, .085f, .07f)),
                impactGrip + model.TransformVector(new Vector3(.075f, .105f, -.067f)),
                impactGrip, t);
            tool = Quaternion.Slerp(raisedTool, strike, t);
        }
        else
        {
            float recovery = Mathf.InverseLerp(contact, 1f, phase);
            Vector3 recoil = impactGrip + model.TransformVector(new Vector3(.03f, .13f, -.055f));
            Quaternion recoilTool = strike * Quaternion.Euler(-23f, 0f, 8f);
            float returnT = SmoothBetween(recovery, .27f, 1f);
            float bounce = 1f - Mathf.Pow(1f - Mathf.Clamp01(recovery / .27f), 2f);
            grip = Vector3.Lerp(Vector3.Lerp(impactGrip, recoil, bounce), ready, returnT);
            tool = Quaternion.Slerp(Quaternion.Slerp(strike, recoilTool, bounce), readyTool, returnT);
        }

        float support = SmoothBetween(phase, .04f, .24f) * (1f - SmoothBetween(phase, .80f, 1f));
        Vector3 rightPole = model.TransformPoint(new Vector3(.65f, .89f, -.14f));
        Quaternion wrist = RightWrist(tool);
        LeoRig.SolveTwoBone(rig.upperArmR, rig.elbowR, rig.handR, WristGoal(rig.handR, grip, wrist),
            rightPole, wrist, weight);
        Place(rig, model, holster);
        LeftArm(rig, model, phase, weight * support);
    }

    static float SmoothBetween(float value, float start, float end) =>
        LeoMotionProfile.Smooth(Mathf.InverseLerp(start, end, value));
}
