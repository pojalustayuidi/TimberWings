using UnityEngine;

public sealed partial class StoryInspection
{
    internal struct ActorPose
    {
        public string state;
        public float weight, lean, crouch, headPitch, headYaw, headRoll, handWeight;
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
        return true;
    }

    ActorPose SamplePose(LeoRig rig)
    {
        float elapsed = BeatTime;
        bool knife = !engraveBridge && subject == Kind.Knife;
        bool ribbon = !engraveBridge && subject == Kind.Ribbon;
        bool ground = knife || ribbon || (!engraveBridge && subject == Kind.CopperArrow);
        float entry = beat == Beat.Look ? Smooth(elapsed / .38f) : 1f;
        float recover = beat == Beat.Recover ? 1f - Smooth(elapsed / (SkipRequested ? .44f : .62f)) : 1f;
        float turn = beat == Beat.React ? Smooth(elapsed / .47f) : beat == Beat.Recover && wasReacting ? 1f : 0f;
        float crouch = ground ? (knife && (beat == Beat.Detail || beat == Beat.React) ? .73f : 1f) : 0f;
        if (knife && beat == Beat.Detail) crouch = Mathf.Lerp(1f, .73f, Smooth(elapsed / .54f));
        if (knife && beat == Beat.Recover) crouch = Mathf.Lerp(.73f, 1f, Smooth(elapsed / .27f));

        var result = new ActorPose
        {
            state = beat == Beat.Recover ? "InspectRecover" : beat == Beat.React ? "InspectReact" :
                beat == Beat.Reach ? "InspectReach" : knife && knifeInHand ? "InspectHold" :
                ground ? "InspectCrouch" : subject == Kind.Board || subject == Kind.Cart ? "InspectLean" : "InspectLook",
            weight = entry * recover,
            crouch = .29f * crouch,
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
        if (!knife && !ribbon) return result;

        Vector3 restGrip = knife && prop != null ? prop.RestGrip : Focus;
        Quaternion restTool = knife && prop != null ? prop.RestRotation : Quaternion.LookRotation(Vector3.up, rig.transform.right);
        result.grip = restGrip;
        result.tool = restTool;
        if (beat == Beat.Reach) result.handWeight = Smooth(elapsed / .38f);
        else if (beat == Beat.Detail || beat == Beat.React) result.handWeight = 1f;
        else if (beat == Beat.Recover && pickedKnife)
            result.handWeight = 1f - Smooth((elapsed - .35f) / .26f);
        else if (beat == Beat.Recover)
            result.handWeight = 1f - Smooth(elapsed / .30f);

        if (!knife || !knifeInHand || rig.chest == null) return result;
        Transform model = rig.transform;
        Vector3 raised = rig.chest.position + model.TransformVector(new Vector3(.25f, -.18f, .55f));
        Quaternion held = model.rotation * Quaternion.Euler(-20f, 14f + 25f * turn, -32f);
        float lift = beat == Beat.Recover ? 1f - Smooth(elapsed / .27f) :
            beat == Beat.Detail ? Smooth(elapsed / .70f) : 1f;
        result.grip = Vector3.Lerp(restGrip, raised, lift);
        result.tool = Quaternion.Slerp(restTool, held, lift);
        return result;
    }

    internal static void ApplyActorPose(LeoRig rig, Transform root, float holster, ActorPose pose)
    {
        if (rig == null || pose.weight <= 0f) return;
        Transform model = rig.transform;
        float weight = pose.weight;
        if (pose.crouch > 0f && rig.hips != null)
        {
            Vector3 left = rig.ankleL.position;
            Vector3 right = rig.ankleR.position;
            Quaternion leftRotation = rig.ankleL.rotation;
            Quaternion rightRotation = rig.ankleR.rotation;
            rig.hips.position += model.TransformVector(new Vector3(0f, -pose.crouch * weight, -.09f * weight));
            LeoRig.SolveTwoBone(rig.hipL, rig.kneeL, rig.ankleL, left, left + model.forward, leftRotation, 1f);
            LeoRig.SolveTwoBone(rig.hipR, rig.kneeR, rig.ankleR, right, right + model.forward, rightRotation, 1f);
        }

        if (rig.spine != null) rig.spine.localRotation *= Quaternion.Euler(pose.lean * weight, 0f, 0f);
        if (rig.chest != null) rig.chest.localRotation *= Quaternion.Euler(new Vector3(3f, -7f * pose.handWeight, 1f) * weight);
        if (rig.head != null) rig.head.localRotation *= Quaternion.Euler(new Vector3(pose.headPitch, pose.headYaw, pose.headRoll) * weight);
        if (pose.handWeight > 0f && rig.handR != null)
        {
            Quaternion wrist = LeoAxePose.RightWrist(pose.tool);
            Vector3 pole = rig.handR.position + model.right * .55f - model.up * .16f;
            LeoRig.SolveTwoBone(rig.upperArmR, rig.elbowR, rig.handR,
                LeoAxePose.WristGoal(rig.handR, pose.grip, wrist), pole, wrist, pose.handWeight * weight);
        }
        // The holster follows the modified pelvis rather than the pre-inspection pose.
        if (pose.weight > .01f) LeoAxePose.Place(rig, root, holster);
    }
}
