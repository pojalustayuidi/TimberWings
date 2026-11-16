using UnityEngine;

public sealed partial class StoryInspection
{
    internal struct ActorPose
    {
        public string state;
        public float weight, lean, crouch, headPitch, headYaw, headRoll, handWeight;
        public float lift, turn, palmRoll, fingerClose;
        public bool knife;
        public Vector3 grip;
        public Quaternion tool;
    }

    internal static bool ReadPose(LeoAnimationController actor, out ActorPose pose) =>
        ReadPose(actor.rig, out pose);

    internal static bool UsesLegacyPose(LeoModelPresentation actor) =>
        current != null && current.legacyPresentation == actor;

    internal static bool ReadPose(LeoRig rig, out ActorPose pose)
    {
        pose = default;
        if (rig == null || current == null || current.actorRig != rig || current.beat == Beat.Approach) return false;
        pose = current.SamplePose(rig);
        if (current.AuthoredKnife || current.AuthoredRibbon) current.lastForestPose = pose;
        return true;
    }

    ActorPose SamplePose(LeoRig rig)
    {
        if (AuthoredKnife || AuthoredRibbon) return SampleForestPose(rig);
        float elapsed = BeatTime;
        bool knife = !engraveBridge && subject == Kind.Knife;
        bool ribbon = !engraveBridge && subject == Kind.Ribbon;
        bool ground = knife || ribbon || (!engraveBridge && subject == Kind.CopperArrow);
        float entry = beat == Beat.Look ? Smooth(elapsed / .38f) : 1f;
        float recover = beat == Beat.Recover ? 1f - Smooth(elapsed / (SkipRequested ? .44f : .62f)) : 1f;
        float turn = beat == Beat.React ? Smooth(elapsed / .47f) : beat == Beat.Recover && wasReacting ? 1f : 0f;

        var result = new ActorPose
        {
            state = beat == Beat.Recover ? "InspectRecover" : beat == Beat.React ? "InspectReact" :
                beat == Beat.Reach ? "InspectReach" :
                ground ? "InspectCrouch" : subject == Kind.Board || subject == Kind.Cart ? "InspectLean" : "InspectLook",
            weight = entry * recover,
            crouch = ground ? .29f : 0f,
            lean = ground ? 18f : subject == Kind.Board || subject == Kind.Cart ? 13f : 5f,
            headPitch = subject == Kind.Arch && !engraveBridge ? -22f : ground ? 18f : 9f,
            headYaw = turn * (subject == Kind.Board ? -33f : subject == Kind.Duck ? 13f : 6f),
            headRoll = turn * (subject == Kind.Duck ? -12f : 3f)
        };

        bool stump = !engraveBridge && subject == Kind.Stump;
        if (stump && (beat == Beat.Detail || beat == Beat.Recover))
        {
            result.grip = Focus;
            result.tool = rig.transform.rotation * Quaternion.Euler(-28f, 15f, -35f);
            result.handWeight = .72f * (beat == Beat.Recover ? recover : Smooth(elapsed / .4f));
        }
        if (!ribbon) return result;

        result.grip = Focus;
        result.tool = Quaternion.LookRotation(Vector3.up, rig.transform.right);
        if (beat == Beat.Reach) result.handWeight = Smooth(elapsed / .38f);
        else if (beat == Beat.Detail || beat == Beat.React) result.handWeight = 1f;
        else if (beat == Beat.Recover)
            result.handWeight = 1f - Smooth(elapsed / .30f);

        return result;
    }

    float knifePalmRoll;

    ActorPose SampleForestPose(LeoRig rig)
    {
        float elapsed = BeatTime;
        if (beat == Beat.Recover)
        {
            var recovery = recoveryActorPose;
            float ease = 1f - Smooth(elapsed / (SkipRequested ? .43f : .65f));
            recovery.state = "InspectRecover";
            recovery.weight *= ease;
            recovery.handWeight *= 1f - Smooth(elapsed / .28f);
            recovery.lift = 0f;
            recovery.fingerClose *= 1f - Smooth(elapsed / .20f);
            return recovery;
        }

        bool knife = AuthoredKnife;
        float lift = !knife || !knifeInHand ? 0f : beat switch
        {
            Beat.Reach => 0f,
            Beat.Lift => Smooth(elapsed / .67f),
            Beat.Replace => knifeReplaceFromLift * (1f - Smooth(elapsed / KnifeReplaceTime)),
            _ => 1f
        };
        float display = beat switch
        {
            Beat.Lift => Smooth((elapsed - .47f) / .42f),
            Beat.React => 1f + .40f * Smooth(elapsed / .65f),
            Beat.Replace => knifeReplaceFromTurn,
            _ => 1f
        };
        var pose = new ActorPose
        {
            state = beat == Beat.Look ? "InspectCrouch" : beat == Beat.Reach ? "InspectReach" :
                beat == Beat.Replace ? "InspectReplace" : knife && knifeInHand ? "InspectHold" : "InspectLook",
            weight = beat == Beat.Look ? Smooth(elapsed / .38f) : 1f,
            crouch = knife ? Mathf.Lerp(.32f, .16f, lift) : .26f,
            lean = Mathf.Lerp(knife ? 22f : 17f, 9f, lift),
            headPitch = Mathf.Lerp(knife ? 21f : 17f, 7f, lift),
            headYaw = knife ? 11f * lift : 0f
        };
        if (!knife) return pose;

        float reachWeight = beat == Beat.Reach ? Smooth(elapsed / .43f) : knifeInHand ? 1f : 0f;
        var model = rig.transform;
        Vector3 up = model.up + model.right * (.56f + .32f * display);
        Vector3 front = model.forward + model.right * (.49f - .28f * display) + model.up * .23f;
        Quaternion held = Quaternion.LookRotation(front, up);
        pose.knife = true;
        pose.handWeight = reachWeight;
        pose.fingerClose = knifeInHand ? Smooth((Time.time - pickedAt) / .11f) :
            beat == Beat.Reach ? .50f * Smooth((elapsed - .30f) / .13f) : 0f;
        pose.grip = prop.RestGrip;
        pose.tool = Quaternion.Slerp(prop.RestRotation, held, lift);
        pose.palmRoll = Mathf.Lerp(162f, 8f, lift);
        pose.lift = lift;
        pose.turn = display;
        knifePalmRoll = pose.palmRoll;
        return pose;
    }

    internal static void ApplyActorPose(LeoRig rig, Transform root, float holster, ActorPose pose)
    {
        if (rig == null || pose.weight <= 0f) return;
        Transform model = rig.transform;
        float weight = pose.weight;
        if (pose.crouch > 0f && rig.hips != null && rig.ankleL != null && rig.ankleR != null)
        {
            Vector3 left = rig.ankleL.position;
            Vector3 right = rig.ankleR.position;
            Quaternion leftRotation = rig.ankleL.rotation;
            Quaternion rightRotation = rig.ankleR.rotation;
            float pelvisDepth = pose.knife ? Mathf.Lerp(-.015f, -.075f, pose.lift) : -.09f;
            rig.hips.position += model.TransformVector(new Vector3(0f, -pose.crouch * weight, pelvisDepth * weight));
            LeoRig.SolveTwoBone(rig.hipL, rig.kneeL, rig.ankleL, left, left + model.forward, leftRotation, 1f);
            LeoRig.SolveTwoBone(rig.hipR, rig.kneeR, rig.ankleR, right, right + model.forward, rightRotation, 1f);
        }

        if (rig.spine != null) rig.spine.localRotation *= Quaternion.Euler(pose.lean * weight, 0f, 0f);
        if (rig.chest != null) rig.chest.localRotation *= Quaternion.Euler(new Vector3(3f, -7f * pose.handWeight, 1f) * weight);
        if (rig.head != null) rig.head.localRotation *= Quaternion.Euler(new Vector3(pose.headPitch, pose.headYaw, pose.headRoll) * weight);
        // Place the axe on the moving pelvis before the inspection takes the right arm.
        if (pose.knife && pose.weight > .01f) LeoAxePose.Place(rig, root, holster);
        if (pose.handWeight > 0f && rig.handR != null)
        {
            Quaternion wrist = LeoAxePose.RightWrist(pose.tool * Quaternion.AngleAxis(pose.palmRoll, Vector3.up));
            Vector3 pole = rig.handR.position + model.right * .55f - model.up * .16f;
            Vector3 grip = pose.grip;
            if (pose.knife && rig.upperArmR != null)
            {
                Vector3 raised = rig.upperArmR.position + model.TransformVector(new Vector3(.105f, -.22f, .22f));
                Vector3 clearance = model.TransformVector(new Vector3(.045f, .11f, 0f));
                grip = Vector3.Lerp(grip, raised, pose.lift) + clearance * (4f * pose.lift * (1f - pose.lift));
            }
            LeoRig.SolveTwoBone(rig.upperArmR, rig.elbowR, rig.handR,
                LeoAxePose.WristGoal(rig.handR, grip, wrist), pole, wrist, pose.handWeight * weight);
        }
        if (!pose.knife)
        {
            if (pose.weight > .01f) LeoAxePose.Place(rig, root, holster);
            return;
        }

        float grasp = pose.fingerClose * weight;
        if (rig.fingersR != null)
            foreach (Transform finger in rig.fingersR)
                rig.Rotate(finger, new Vector3(Mathf.Lerp(39f, -32f, grasp), 0f, 0f));
        if (rig.fingerTipsR != null)
            foreach (Transform tip in rig.fingerTipsR)
                rig.Rotate(tip, new Vector3(Mathf.Lerp(34f, -27f, grasp), 0f, 0f));
        rig.Rotate(rig.thumbR, new Vector3(-11f * grasp, 0f, 0f));
    }
}
