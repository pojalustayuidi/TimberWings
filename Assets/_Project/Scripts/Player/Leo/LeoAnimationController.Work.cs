using UnityEngine;

public sealed partial class LeoAnimationController
{
    readonly LeoBuildMotion work = new LeoBuildMotion();

    [Tooltip("Показывать в консоли причины, по которым Лео не начинает удар")]
    public bool debugWork;
}

// Both presentation modes use the same draw, contact and recoil. The clock samples
// the contact pose on the frame that crosses it, including at a low frame rate.
internal sealed class LeoBuildMotion
{
    internal const float ContactPhase = .53f;
    static readonly float[] Cycles = { .64f, .70f, .79f };
    static readonly Vector3 Grip = new Vector3(0f, -.039f, 0f);
    static readonly Vector3 Face = new Vector3(0f, .14448f, .10622f);
    static readonly Vector3 ReadyGrip = new Vector3(.26f, .98f, .22f);

    float elapsed, nextLogAt;
    int beat;
    bool wasWorking, contactFrame;
    BuildWorkSpot target;

    internal float Weight { get; private set; }
    internal float Phase { get; private set; }

    internal static bool HasHammer(LeoRig rig) => rig != null && rig.hammerRoot != null &&
        rig.HammerHolster != null && rig.handR != null && rig.upperArmR != null && rig.elbowR != null;

    internal void Reset()
    {
        elapsed = Weight = Phase = 0f;
        wasWorking = contactFrame = false;
        beat = 0;
        target = default;
    }

    internal void Read(PlayerMovement player, LeoRig rig, float speed, float dt, float holster,
        bool allowed, bool debug)
    {
        bool ready = BuildWorkPose.TryGetStrikeTarget(player, out var contact);
        bool playing = ready && allowed && speed < .58f && holster >= .99f && HasHammer(rig);
        if (playing) target = contact;
        Advance(dt, playing);
        if (debug && BuildWorkPose.HasWork(player) && Time.time >= nextLogAt)
        {
            nextLogAt = Time.time + 1f;
            Debug.Log($"[Leo work] target={ready} allowed={allowed} speed={speed:F2} holster={holster:F2} swing={playing}", rig);
        }
    }

    internal void Advance(float dt, bool playing)
    {
        contactFrame = false;
        Weight = Mathf.MoveTowards(Weight, playing ? 1f : 0f, dt * (playing ? 6.5f : 7.5f));
        if (!playing)
        {
            wasWorking = false;
            if (Weight <= 0f) Reset();
            return;
        }

        if (!wasWorking)
        {
            wasWorking = true;
            elapsed = 0f;
            beat = 0;
        }
        float cycle = Cycles[beat % Cycles.Length];
        float contact = cycle * ContactPhase;
        contactFrame = elapsed < contact && elapsed + dt >= contact;
        elapsed += dt;
        if (elapsed >= cycle)
        {
            elapsed -= cycle;
            beat++;
            cycle = Cycles[beat % Cycles.Length];
        }
        Phase = contactFrame ? ContactPhase : Mathf.Clamp01(elapsed / cycle);
    }

    static float LowTarget(float height) => Mathf.Clamp01((.91f - height) / .28f);

    internal void ApplyBody(LeoRig rig, Transform model)
    {
        Vector3 localTarget = model.InverseTransformPoint(target.hit);
        float low = LowTarget(localTarget.y);
        float tap = Mathf.Max(0f, 1f - Mathf.Abs(Phase - ContactPhase) / .19f);
        bool heavy = beat % Cycles.Length == Cycles.Length - 1;
        float impact = tap * (heavy ? 1.25f : 1f);
        float reach = Mathf.Clamp01((localTarget.z - .55f) / .25f);
        if (rig.hips != null)
            rig.hips.localPosition += Weight * new Vector3(-.014f, -.035f - .10f * low - .024f * impact,
                .074f + .05f * reach + .052f * impact);

        AddRotation(rig.spine, Weight * new Vector3(7f + 6f * low + 4f * impact, -3f, 0f));
        AddRotation(rig.chest, Weight * new Vector3(4f * impact, 4f, 1f * impact));
        AddRotation(rig.head, Weight * new Vector3(4f + 8f * low + 3f * impact, 4f, 0f));
        AddRotation(rig.upperArmL, Weight * new Vector3(-17f, 0f, -6f));
        AddRotation(rig.elbowL, Weight * new Vector3(-23f, 0f, 0f));
    }

    static void AddRotation(Transform joint, Vector3 euler)
    {
        if (joint != null) joint.localRotation *= Quaternion.Euler(euler);
    }

    internal void PlaceHammer(LeoRig rig, Transform model, PlayerMovement player)
    {
        if (!HasHammer(rig)) return;
        bool heavy = beat % Cycles.Length == Cycles.Length - 1;
        float raise = LeoMotionProfile.Smooth(Phase / .35f);
        float swing = LeoMotionProfile.Smooth((Phase - .35f) / (ContactPhase - .35f));
        float recoil = LeoMotionProfile.Smooth((Phase - ContactPhase) / (1f - ContactPhase));
        Quaternion ready = model.rotation * Quaternion.Euler(28f, -5f, -19f);
        Quaternion high = model.rotation * Quaternion.Euler(-31f, 13f, -34f);
        float low = LowTarget(model.InverseTransformPoint(target.hit).y);
        Quaternion impact = Quaternion.LookRotation(-target.normal, Vector3.up) *
            Quaternion.Euler(Mathf.Lerp(5f, 22f, low), 0f, 0f);
        Quaternion tool = Quaternion.Slerp(Quaternion.Slerp(Quaternion.Slerp(ready, high, raise), impact, swing), ready, recoil);

        Vector3 face = rig.hammerStrike != null && rig.hammerStrike.parent == rig.hammerRoot ? rig.hammerStrike.localPosition : Face;
        Vector3 gripToFace = Vector3.Scale(face - Grip, rig.hammerRoot.lossyScale);
        Vector3 readyWorld = model.TransformPoint(ReadyGrip);
        Vector3 highWorld = model.TransformPoint(new Vector3(.30f, heavy ? 1.38f : 1.26f, .17f));
        Vector3 impactGrip = target.hit - impact * gripToFace;
        Vector3 grip = Vector3.Lerp(Vector3.Lerp(Vector3.Lerp(readyWorld, highWorld, raise), impactGrip, swing), readyWorld, recoil);
        float bounce = Mathf.Sin(Mathf.PI * Mathf.Clamp01((Phase - ContactPhase) / .21f));
        grip += target.normal * (bounce * .055f * model.lossyScale.y);

        Transform holster = rig.HammerHolster;
        float draw = LeoMotionProfile.Smooth(Weight);
        Vector3 drawGrip = Vector3.Lerp(holster.TransformPoint(Grip), grip, draw);
        Quaternion drawRotation = Quaternion.Slerp(holster.rotation, tool, draw);
        Quaternion wrist = LeoAxePose.RightWrist(drawRotation);
        Vector3 pole = model.TransformPoint(new Vector3(.69f, 1.01f, -.07f));
        LeoRig.SolveTwoBone(rig.upperArmR, rig.elbowR, rig.handR,
            LeoAxePose.WristGoal(rig.handR, drawGrip, wrist), pole, wrist, Weight);

        Quaternion held = LeoAxePose.ToolRotation(rig.handR);
        Vector3 origin = LeoAxePose.Grip(rig.handR) - held * Vector3.Scale(Grip, rig.hammerRoot.lossyScale);
        rig.hammerRoot.SetPositionAndRotation(Vector3.Lerp(holster.position, origin, draw),
            Quaternion.Slerp(holster.rotation, held, draw));

        if (rig.fingersR != null)
            foreach (Transform finger in rig.fingersR)
                rig.Rotate(finger, new Vector3(Mathf.Lerp(42f, -42f, draw), 0f, 0f));
        if (rig.fingerTipsR != null)
            foreach (Transform joint in rig.fingerTipsR)
                rig.Rotate(joint, new Vector3(Mathf.Lerp(35f, -35f, draw), 0f, 0f));

        if (!contactFrame) return;
        contactFrame = false;
        BuildWorkPose.NotifyStrike(player, rig.hammerRoot.TransformPoint(face));
    }
}
