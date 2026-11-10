using UnityEngine;

public static class LeoAxePose
{
    // Mesh coordinates on Leo v3.3: the wrapped handle runs along tool Y.
    // Hand X runs across the curled fingers, with the thumb on the head side.
    static readonly Vector3 Palm = new Vector3(0f, -.061f, .051f);
    static readonly Quaternion AcrossPalm = new Quaternion(0f, 0f, .70710678f, .70710678f);
    public static readonly Vector3 MainGrip = new Vector3(0f, -.046f, 0f);
    public static readonly Vector3 SupportGrip = new Vector3(0f, -.162f, 0f);
    public static readonly Vector3 Edge = new Vector3(0f, .36049f, .24429f);
    static readonly Vector3 GripToEdge = Edge - MainGrip;

    public static Vector3 Grip(Transform hand) => hand.TransformPoint(Palm);
    public static Quaternion ToolRotation(Transform hand, float palmRoll = 0f)
        => hand.rotation * AcrossPalm * Quaternion.AngleAxis(-palmRoll, Vector3.up);

    public static Quaternion RightWrist(Quaternion tool) => tool * Quaternion.Inverse(AcrossPalm);

    public static Vector3 WristGoal(Transform hand, Vector3 grip, Quaternion wrist)
        => grip - wrist * Vector3.Scale(Palm, hand.lossyScale);

    public static void RestWrist(LeoRig rig, float move)
    {
        if (rig.handR != null)
            rig.handR.localRotation *= Quaternion.Euler(0f, 65f - 8f * move, -10f);
    }

    public static float ContactAim(float phase, float contact)
        => LeoMotionProfile.Smooth((phase - (contact - .24f)) / .24f) *
           (1f - LeoMotionProfile.Smooth((phase - contact) / .22f));

    public static Vector3 BodyShift(LeoMotionProfile.ChopPose pose, float aim, Transform model, Vector3? bark)
    {
        // A shallow lunge on a nearby trunk keeps Leo's cheek behind the axe.
        float forward = .085f;
        if (bark.HasValue)
        {
            Vector3 local = model.InverseTransformPoint(bark.Value);
            float distance = Vector3.ProjectOnPlane(local, Vector3.up).magnitude;
            forward = Mathf.Lerp(.043f, .135f, LeoMotionProfile.Smooth((distance - .69f) / .20f));
        }
        return new Vector3(pose.chest.y * .0007f, -.025f - .029f * aim, forward * aim);
    }

    public static float RightArm(LeoRig rig, Transform model, LeoMotionProfile.Book book,
        LeoMotionProfile.ChopPose pose, float phase, float weight, Vector3? bark)
    {
        if (rig.handR == null || rig.axeRoot == null || weight <= 0f) return 0f;
        float aim = ContactAim(phase, book.contact);
        Vector3 angles = pose.rotation;
        Vector3 grip = pose.tool;
        if (bark.HasValue)
        {
            Vector3 localBark = model.InverseTransformPoint(bark.Value);
            Vector3 toward = Vector3.ProjectOnPlane(localBark, Vector3.up).normalized;
            float yaw = Mathf.Clamp(Mathf.Atan2(toward.x, toward.z) * Mathf.Rad2Deg, -45f, 45f);
            angles.y += yaw * aim;
            Vector3 edgeOffset = Quaternion.Euler(angles) * GripToEdge;
            if (aim > 0f)
            {
                var strike = LeoMotionProfile.Chop(book, book.contact);
                Vector3 reference = strike.tool + Quaternion.Euler(strike.rotation) * GripToEdge;
                Vector3 drift = grip + Quaternion.Euler(pose.rotation) * GripToEdge - reference;
                grip = Vector3.Lerp(grip, localBark - edgeOffset + drift, aim);
            }
            // Keep the broad head of the axe clear of Leo's face before turning in.
            float outward = LeoMotionProfile.Smooth((phase - .30f) / .14f) *
                (1f - LeoMotionProfile.Smooth((phase - .53f) / .09f));
            grip.x += .107f * outward;

            // As the blade reaches the front of the trunk, leave a hairline gap
            // until the gameplay hit. Let the wrists recoil and the edge slide down.
            float gap = phase < book.contact ? .017f * (1f - aim) : -.008f;
            float excess = Vector3.Dot(grip + edgeOffset - localBark, toward) + gap;
            grip -= toward * Mathf.Max(0f, excess);
        }

        // The palm rolls around the shaft as the elbow straightens. Compensate
        // in PlaceAxe so this straightens the wrist without rolling the blade.
        float roll = -40f * LeoMotionProfile.Smooth((phase - .43f) / .15f) *
            (1f - LeoMotionProfile.Smooth((phase - .63f) / .15f));
        Quaternion tool = model.rotation * Quaternion.Euler(angles);
        Quaternion wrist = RightWrist(tool * Quaternion.AngleAxis(roll, Vector3.up));
        Vector3 goal = WristGoal(rig.handR, model.TransformPoint(grip), wrist);
        Vector3 pole = model.TransformPoint(new Vector3(.73f + .05f * aim, .94f + .17f * aim, -.13f + .03f * aim));
        LeoRig.SolveTwoBone(rig.upperArmR, rig.elbowR, rig.handR, goal, pole, wrist, weight);
        return roll * weight;
    }

    public static void LeftArm(LeoRig rig, Transform model, float phase, float weight)
    {
        if (rig.handL == null || rig.axeRoot == null || weight <= 0f) return;
        float roll = -18f * LeoMotionProfile.Smooth((phase - .23f) / .13f) *
            (1f - LeoMotionProfile.Smooth((phase - .50f) / .085f));
        Quaternion wrist = RightWrist(rig.axeRoot.rotation * Quaternion.AngleAxis(180f + roll, Vector3.up));
        Vector3 grip = rig.axeRoot.TransformPoint(SupportGrip);
        Vector3 pole = model.TransformPoint(new Vector3(-.73f, 1.01f, .06f));
        LeoRig.SolveTwoBone(rig.upperArmL, rig.elbowL, rig.handL,
            WristGoal(rig.handL, grip, wrist), pole, wrist, weight);
        Curl(rig, rig.fingersL, -38f * weight);
        Curl(rig, rig.fingerTipsL, -31f * weight);
        rig.Rotate(rig.thumbL, new Vector3(-13f * weight, 0f, 0f));
    }

    static void Curl(LeoRig rig, Transform[] joints, float angle)
    {
        if (joints == null) return;
        foreach (Transform joint in joints)
            rig.Rotate(joint, new Vector3(angle, 0f, 0f));
    }
}
