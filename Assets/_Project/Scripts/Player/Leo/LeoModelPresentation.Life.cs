using UnityEngine;

public sealed partial class LeoModelPresentation
{
    const float WorkCycle = .66f;
    const float WorkContact = .53f;
    static readonly Vector3 HammerGrip = new Vector3(0f, -.039f, 0f);
    static readonly Vector3 HammerFace = new Vector3(0f, .14448f, .10622f);

    float workTime, workWeight;
    int lastWorkTap = -1;
    bool workRequested;

    void ResetWorkPose()
    {
        workTime = workWeight = 0f;
        workRequested = false;
        lastWorkTap = -1;
    }

    void ReadWorkPose(float speed, float dt, bool allowed, bool chopping)
    {
        bool moving = speed > .26f || Mathf.Abs(MobileInput.Horizontal) > .04f || Mathf.Abs(MobileInput.Vertical) > .04f;
        bool free = allowed && !chopping && !MobileInput.ModalOpen && player != null && !player.IsDashing;
        workRequested = free && !moving && rig.hammerRoot != null && rig.handR != null && rig.HammerHolster != null &&
            BuildWorkPose.TryGetFocus(player, out _);
        bool works = workRequested && holster >= .99f;
        workWeight = chopping ? 0f : Mathf.MoveTowards(workWeight, works ? 1f : 0f, dt * (works ? 7f : 9f));
        if (works)
        {
            workTime += dt;
            int strike = Mathf.FloorToInt(workTime / WorkCycle - WorkContact);
            if (strike >= 0 && strike > lastWorkTap && workWeight > .85f)
                SfxManager.Play("build", .38f, strike % 2 == 0 ? 1.08f : .99f);
            lastWorkTap = strike;
        }
        else if (workWeight <= 0f)
        {
            workTime = 0f;
            lastWorkTap = -1;
        }
    }

    void ApplyWorkBody()
    {
        if (workWeight <= .001f) return;
        float phase = Mathf.Repeat(workTime / WorkCycle, 1f);
        float tap = Mathf.Max(0f, 1f - Mathf.Abs(phase - WorkContact) / .17f) * workWeight;
        if (rig.hips != null)
            rig.hips.localPosition += new Vector3(-.015f * workWeight, -.043f * workWeight - .017f * tap, .044f * workWeight);
        rig.Rotate(rig.spine, new Vector3(6f * workWeight + tap * 3f, -3f * workWeight, 0f));
        rig.Rotate(rig.chest, new Vector3(2f * tap, 5f * workWeight, 0f));
        rig.Rotate(rig.head, new Vector3((3f + 5f * tap) * workWeight, 0f, 0f));
        rig.Rotate(rig.upperArmL, new Vector3(-18f * workWeight, 0f, 0f));
        rig.Rotate(rig.elbowL, new Vector3(-24f * workWeight, 0f, 0f));
    }

    void PlaceWorkHammer()
    {
        if (rig.hammerRoot == null || rig.handR == null || rig.HammerHolster == null) return;
        float phase = Mathf.Repeat(workTime / WorkCycle, 1f);
        float raise = LeoMotionProfile.Smooth(phase / .36f);
        float strike = LeoMotionProfile.Smooth((phase - .36f) / (WorkContact - .36f));
        float recoil = LeoMotionProfile.Smooth((phase - WorkContact) / (1f - WorkContact));
        Vector3 ready = new Vector3(.24f, .998f, .24f);
        Vector3 high = new Vector3(.31f, 1.28f, .21f);
        Vector3 tip = new Vector3(.12f, .895f, .497f);
        Vector3 angles = Vector3.Lerp(new Vector3(30f, -3f, -18f), new Vector3(-28f, 15f, -39f), raise);
        Vector3 hitAngles = new Vector3(64f, -8f, -9f);
        Vector3 hit = tip - Quaternion.Euler(hitAngles) * (HammerFace - HammerGrip);
        angles = Vector3.Lerp(Vector3.Lerp(angles, hitAngles, strike), new Vector3(30f, -3f, -18f), recoil);
        Vector3 grip = Vector3.Lerp(Vector3.Lerp(Vector3.Lerp(ready, high, raise), hit, strike), ready, recoil);

        Quaternion tool = transform.rotation * Quaternion.Euler(angles);
        Quaternion wrist = LeoAxePose.RightWrist(tool);
        Vector3 pole = transform.TransformPoint(new Vector3(.69f, 1.00f, -.06f));
        LeoRig.SolveTwoBone(rig.upperArmR, rig.elbowR, rig.handR,
            LeoAxePose.WristGoal(rig.handR, transform.TransformPoint(grip), wrist), pole, wrist, workWeight);

        rig.PlaceAxe(holster);
        UpdateChopTrail(0f);

        // The small hammer shares the across-palm grip. Its own pocket
        // socket is the start and end of the draw.
        Quaternion held = LeoAxePose.ToolRotation(rig.handR);
        Vector3 shaft = Vector3.Scale(HammerGrip, rig.hammerRoot.lossyScale);
        Vector3 origin = LeoAxePose.Grip(rig.handR) - held * shaft;
        float draw = LeoMotionProfile.Smooth(workWeight);
        rig.hammerRoot.SetPositionAndRotation(Vector3.Lerp(rig.HammerHolster.position, origin, draw),
            Quaternion.Slerp(rig.HammerHolster.rotation, held, draw));

        // PlaceAxe opened the hand at the belt. Close it around the hammer during the draw.
        if (rig.fingersR != null)
            foreach (Transform finger in rig.fingersR)
                rig.Rotate(finger, new Vector3(-42f * holster * draw, 0f, 0f));
        if (rig.fingerTipsR != null)
            foreach (Transform joint in rig.fingerTipsR)
                rig.Rotate(joint, new Vector3(-35f * holster * draw, 0f, 0f));
    }
}
