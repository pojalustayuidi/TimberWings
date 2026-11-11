using UnityEngine;

internal sealed class LeoPortalPose
{
    internal const float Duration = 1.86f;
    internal const float PullStarts = .91f;

    readonly LeoRig rig;
    readonly Transform longScarf;
    readonly Transform shortScarf;
    readonly Vector3 leftFoot;
    readonly Vector3 rightFoot;
    readonly Quaternion leftSole;
    readonly Quaternion rightSole;

    internal LeoPortalPose(LeoRig rig)
    {
        this.rig = rig;
        if (rig == null) return;
        if (rig.neck != null)
        {
            longScarf = rig.neck.Find("ScarfTail");
            shortScarf = rig.neck.Find("ScarfShortTail");
        }
        if (rig.ankleL != null)
        {
            leftFoot = rig.ankleL.position;
            leftSole = rig.ankleL.rotation;
        }
        if (rig.ankleR != null)
        {
            rightFoot = rig.ankleR.position;
            rightSole = rig.ankleR.rotation;
        }
    }

    internal static float Ease(float time, float from, float to) =>
        Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, time));

    internal static float PullAt(float time) =>
        Mathf.Pow(Mathf.InverseLerp(PullStarts, Duration, time), 2.05f);

    internal void Apply(float time)
    {
        if (rig == null || !rig.HasRest) return;
        float react = Ease(time, 0f, .28f);
        float reach = Ease(time, .12f, .60f) * (1f - Ease(time, .64f, 1.04f));
        float brace = Ease(time, .57f, .86f) * (1f - Ease(time, .92f, 1.24f));
        float pulled = Ease(time, .94f, 1.56f);
        float step = Ease(time, 1.02f, 1.38f) * (1f - Ease(time, 1.46f, Duration));
        float tuck = Ease(time, 1.40f, 1.78f);

        if (rig.hips != null)
        {
            var shift = new Vector3(.022f * reach - .037f * brace, -.042f * brace, .042f * reach - .064f * brace);
            rig.hips.localPosition = Vector3.Lerp(rig.hips.localPosition, rig.RestPosition(rig.hips) + shift, react);
        }

        Pose(rig.hips, 5f * reach - 9f * brace + 19f * pulled, -6f * reach + 8f * pulled, -6f * brace + 9f * pulled, react);
        Pose(rig.spine, 13f * reach - 16f * brace + 20f * pulled, 10f * reach - 9f * pulled, -7f * brace, react);
        Pose(rig.chest, 8f * reach - 7f * brace + 9f * pulled, 13f * reach + 11f * pulled, 5f * pulled, react);
        Pose(rig.head, -12f * reach + 13f * brace - 22f * pulled, -14f * reach - 10f * pulled, 8f * reach + 5f * pulled, react);
        Pose(rig.jaw, 2f * reach + 12f * brace + 11f * pulled, 0f, 0f, react);
        Pose(rig.browL, 0f, 0f, 6f * brace + 4f * pulled, react);
        Pose(rig.browR, 0f, 0f, -7f * brace - 4f * pulled, react);

        Pose(rig.upperArmR, -12f - 67f * reach - 32f * brace - 45f * pulled, -12f * reach, 16f + 16f * brace + 19f * pulled, react);
        Pose(rig.elbowR, -12f - 18f * reach - 42f * brace - 8f * pulled, 3f, 4f * pulled, react);
        Pose(rig.handR, -18f * reach + 12f * brace, 19f * reach, -14f * reach - 9f * pulled, react);
        Pose(rig.upperArmL, -8f - 37f * brace + 20f * pulled, 10f * brace, -13f - 25f * brace - 24f * pulled, react);
        Pose(rig.elbowL, -12f - 48f * brace - 20f * pulled, -4f * brace, -5f * pulled, react);
        Pose(rig.handL, 10f * brace + 15f * pulled, -12f * brace, 7f * pulled, react);
        Pose(rig.thumbR, 5f * reach, -10f * reach, -6f * brace, react);
        Curl(rig.fingersR, -16f * brace - 8f * pulled, react);
        Curl(rig.fingerTipsR, -12f * brace, react);

        Pose(rig.hipL, -9f * brace - 35f * step + 15f * tuck, 0f, -6f * brace, react);
        Pose(rig.kneeL, 13f * brace + 33f * step + 32f * tuck, 0f, 0f, react);
        Pose(rig.ankleL, -12f * step + 18f * tuck, 0f, 0f, react);
        Pose(rig.hipR, -4f * brace + 24f * pulled, 0f, 4f * pulled, react);
        Pose(rig.kneeR, 12f * brace + 36f * pulled, 0f, 0f, react);
        Pose(rig.ankleR, 24f * pulled, 0f, 0f, react);

        float plant = react * (1f - Ease(time, PullStarts, 1.14f));
        Plant(rig.hipL, rig.kneeL, rig.ankleL, leftFoot, leftSole, plant);
        Plant(rig.hipR, rig.kneeR, rig.ankleR, rightFoot, rightSole, plant);
        rig.UpdateJacketHem();

        if (longScarf != null)
        {
            float flutter = Mathf.Sin(time * 22f) * (4f * reach + 12f * pulled);
            Pose(longScarf, 35f * reach + 112f * pulled, 15f * brace, 11f * pulled + flutter, react);
            Pose(shortScarf, 22f * reach + 72f * pulled, -10f * pulled, flutter * .65f, react);
        }
    }

    void Plant(Transform hip, Transform knee, Transform ankle, Vector3 point, Quaternion rotation, float weight)
    {
        if (hip == null || knee == null || ankle == null || weight <= 0f) return;
        var pole = hip.position + rig.transform.TransformVector(new Vector3(0f, -.2f, .6f));
        LeoRig.SolveTwoBone(hip, knee, ankle, point, pole, rotation, weight);
    }

    void Curl(Transform[] joints, float angle, float weight)
    {
        if (joints == null) return;
        foreach (var joint in joints) Pose(joint, angle, 0f, 0f, weight);
    }

    void Pose(Transform joint, float x, float y, float z, float weight)
    {
        if (joint == null) return;
        var target = rig.RestRotation(joint) * Quaternion.Euler(x, y, z);
        joint.localRotation = Quaternion.Slerp(joint.localRotation, target, weight);
    }

    internal void Release()
    {
        if (rig != null) rig.ResetPose();
    }
}
