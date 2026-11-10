using UnityEngine;

public sealed partial class LeoModelPresentation
{
    const float WorkCycle = .66f;
    const float WorkContact = .53f;
    const float HopDuration = .62f;
    static readonly Vector3 HammerGrip = new Vector3(0f, -.039f, 0f);
    static readonly Vector3 HammerFace = new Vector3(0f, .14448f, .10622f);

    float workTime, workWeight;
    int lastWorkTap = -1;
    float cheerQueue, cheerTime = -1f, cheerWeight, hopLift, hopSquat, cheerArms;
    bool LifeActive => workWeight > .01f || cheerWeight > .01f;

    void OnLevelUp(int level) => QueueCheer();

    void OnBuildReveal(BuildingController site)
    {
        if (site != null && site.RevealIsFinal && site.PlayerInside)
            QueueCheer();
    }

    void QueueCheer()
    {
        if (!SaveSystem.IsLoading && cheerTime < 0f)
            cheerQueue = 2.5f;
    }

    void ResetLifePose()
    {
        workTime = workWeight = cheerQueue = cheerWeight = hopLift = hopSquat = cheerArms = 0f;
        lastWorkTap = -1;
        cheerTime = -1f;
    }

    void ReadLifePose(float speed, float dt, bool allowed, bool chopping)
    {
        bool moving = speed > .26f || Mathf.Abs(MobileInput.Horizontal) > .04f || Mathf.Abs(MobileInput.Vertical) > .04f;
        bool free = allowed && !chopping && !MobileInput.ModalOpen && player != null && !player.IsDashing;
        bool works = free && !moving && rig.hammerRoot != null && rig.handR != null && rig.HammerHolster != null && BuildWorkPose.TryGetFocus(player, out _);
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

        if (cheerTime < 0f && cheerQueue > 0f && free && workWeight < .02f)
        {
            cheerQueue = Mathf.Max(0f, cheerQueue - dt);
            if (!moving && moveWeight < .13f && IsOnGroundForCheer())
            {
                cheerTime = 0f;
                cheerQueue = 0f;
                cheerWeight = 1f;
            }
        }

        if (cheerTime < 0f || chopping || !allowed)
        {
            cheerTime = -1f;
            cheerWeight = hopLift = hopSquat = cheerArms = 0f;
            return;
        }

        cheerTime += dt;
        if (moving || works)
            cheerWeight = Mathf.MoveTowards(cheerWeight, 0f, dt * 7f);
        if (cheerTime >= HopDuration || cheerWeight <= 0f)
        {
            cheerTime = -1f;
            cheerWeight = hopLift = hopSquat = cheerArms = 0f;
            return;
        }

        float air = Mathf.Clamp01((cheerTime - .15f) / .28f);
        float spring = 4f * air * (1f - air);
        float ready = LeoMotionProfile.Smooth(cheerTime / .15f) *
            (1f - LeoMotionProfile.Smooth((cheerTime - .15f) / .080f));
        float landing = Mathf.Sin(Mathf.PI * Mathf.Clamp01((cheerTime - .43f) / .19f));
        hopLift = .151f * spring * cheerWeight;
        hopSquat = (.077f * ready + .049f * landing) * cheerWeight;
        cheerArms = LeoMotionProfile.Smooth((cheerTime - .08f) / .15f) *
            (1f - LeoMotionProfile.Smooth((cheerTime - .32f) / .22f)) * cheerWeight;
    }

    bool IsOnGroundForCheer()
    {
        if (leftFoot == null) return false;
        float scale = Mathf.Max(.01f, transform.lossyScale.y);
        Vector3 sole = transform.TransformPoint(leftFoot.rest + Vector3.up * leftFoot.soleY);
        return SampleGround(sole, scale, out _);
    }

    void ApplyLifeBody()
    {
        if (!LifeActive) return;
        float phase = Mathf.Repeat(workTime / WorkCycle, 1f);
        float tap = Mathf.Max(0f, 1f - Mathf.Abs(phase - WorkContact) / .17f) * workWeight;
        if (rig.hips != null)
            rig.hips.localPosition += new Vector3(-.015f * workWeight, hopLift - hopSquat - .043f * workWeight - .017f * tap, .044f * workWeight);
        rig.Rotate(rig.spine, new Vector3(6f * workWeight + tap * 3f + hopSquat * 44f, -3f * workWeight, 0f));
        rig.Rotate(rig.chest, new Vector3(2f * tap, 5f * workWeight, 6f * cheerArms));
        rig.Rotate(rig.head, new Vector3((3f + 5f * tap) * workWeight - 8f * cheerArms, 0f, 3f * cheerArms));
        rig.Rotate(rig.upperArmL, new Vector3(-18f * workWeight - 100f * cheerArms, 0f, -15f * cheerArms));
        rig.Rotate(rig.elbowL, new Vector3(-24f * workWeight - 58f * cheerArms, 0f, 0f));
        if (rig.fingersL != null)
            foreach (Transform finger in rig.fingersL)
                rig.Rotate(finger, new Vector3(-32f * cheerArms, 0f, 0f));
        if (rig.fingerTipsL != null)
            foreach (Transform tip in rig.fingerTipsL)
                rig.Rotate(tip, new Vector3(-27f * cheerArms, 0f, 0f));
        rig.Rotate(rig.browL, new Vector3(0f, 0f, 8f * cheerArms));
        rig.Rotate(rig.browR, new Vector3(0f, 0f, -8f * cheerArms));
        rig.Rotate(rig.jaw, new Vector3(11f * cheerArms, 0f, 0f));
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

        // The axe is going onto the belt, which opens the fingers in PlaceAxe.
        // Close them around the hammer again as it reaches the palm.
        if (rig.fingersR != null)
            foreach (Transform finger in rig.fingersR)
                rig.Rotate(finger, new Vector3(-42f * holster * draw, 0f, 0f));
        if (rig.fingerTipsR != null)
            foreach (Transform joint in rig.fingerTipsR)
                rig.Rotate(joint, new Vector3(-35f * holster * draw, 0f, 0f));
    }
}
