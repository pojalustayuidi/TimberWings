using UnityEngine;

internal sealed partial class LeoFirstPersonView
{
    const float Contact = .62f;
    const float Recovery = .29f;
    static readonly Vector3 LeftShoulder = new Vector3(-.22f, -.35f, .060f);
    static readonly Vector3 RightShoulder = new Vector3(.25f, -.35f, .075f);

    struct Pose
    {
        internal readonly Vector3 grip;
        internal readonly Quaternion tool;
        internal readonly float extension;

        internal Pose(Vector3 grip, Quaternion tool, float extension = 0f)
        {
            this.grip = grip;
            this.tool = tool;
            this.extension = extension;
        }

        internal static Pose Blend(Pose a, Pose b, float t) => new Pose(
            Vector3.Lerp(a.grip, b.grip, t), Quaternion.Slerp(a.tool, b.tool, t),
            Mathf.Lerp(a.extension, b.extension, t));
    }

    readonly LeoImpactClock clock = new LeoImpactClock();
    readonly LeoLegacyTiming legacy = new LeoLegacyTiming();
    Pose lastSwing;
    int impactFrame = -1;
    bool suspended, wasSwinging, groundStrike;
    Vector3? contactWorld;
    Vector3 groundCutAxis;
    Transform forecastTarget;
    float forecastDue;
    float equipped, supportWeight, carryUntil, cancelAmount;
    float recoveryStart, recoveryEnd, contactRecoil = 1f;
    float airStart = -1f, airDue = -1f;
    bool airWhooshPlayed, silentDraw;

    internal bool TryAirSwing(float speed)
    {
        float now = Time.time;
        if (suspended || equipped < .83f || wasSwinging || now < recoveryEnd || airDue >= 0f) return false;
        airStart = now;
        airDue = now + .34f / speed;
        airWhooshPlayed = false;
        return true;
    }

    internal void Hit(PlayerInteraction interaction, bool fell)
    {
        if (suspended || interaction == null || !interaction.IsChopping || impactFrame == Time.frameCount) return;
        if ((fell && interaction.TargetItem == null) || (!fell && interaction.CurrentTarget == null)) return;
        impactFrame = Time.frameCount;
        contactRecoil = PlayerInteraction.PreciseChop03Impact && !VibeSettings.ReduceEffects ? 1.18f : 1f;
        groundStrike = interaction.TryReadPrototypeContact(out _, out groundCutAxis);
        contactWorld = interaction.TryReadLeoImpact(out var point) ? point : (Vector3?)null;
    }

    internal void Suspend()
    {
        SetVisible(false);
        if (suspended) return;
        ReleaseMotion();
    }

    void ReleaseMotion()
    {
        suspended = true;
        clock.Clear();
        legacy.Clear();
        wasSwinging = groundStrike = false;
        contactWorld = null;
        forecastTarget = null;
        forecastDue = -1f;
        impactFrame = -1;
        lastSwing = default;
        equipped = supportWeight = carryUntil = cancelAmount = 0f;
        recoveryStart = recoveryEnd = -1f;
        airStart = airDue = -1f;
        silentDraw = false;
        if (stroke != null)
        {
            stroke.emitting = false;
            stroke.Clear();
        }
    }

    internal void Step(PlayerInteraction interaction, bool manualCarry, float dt, float stride, float move, float run, float viewScale)
    {
        SetAxe(interaction != null ? interaction.EquippedAxe : AxeCatalog.Get(AxeId.Old));
        if (suspended) silentDraw = manualCarry;
        suspended = false;
        float now = Time.time;
        bool chopping = interaction != null && interaction.IsChopping;
        if (chopping || !manualCarry) airStart = airDue = -1f;
        bool air = airDue >= 0f;
        bool missed = air && now >= airDue;
        bool hit = impactFrame == Time.frameCount || missed;
        if (air)
        {
            groundStrike = false;
            contactWorld = null;
            contactRecoil = .48f;
            if (!airWhooshPlayed && now >= Mathf.Lerp(airStart, airDue, .63f))
            {
                airWhooshPlayed = true;
                SfxManager.Timber("axeMiss", .58f);
            }
            if (missed) Chop02Feedback.Report(ChopTimingResult.Miss, camera.transform.position + camera.transform.forward);
        }
        if (chopping || hit) carryUntil = now + .37f;
        bool drawAxe = manualCarry || chopping || hit || now < carryUntil;
        bool wasDrawn = equipped >= .32f;
        equipped = Mathf.MoveTowards(equipped, drawAxe ? 1f : 0f, dt / (drawAxe ? .21f : .25f));
        bool isDrawn = equipped >= .32f;
        if (wasDrawn != isDrawn)
        {
            if (!silentDraw) SfxManager.Timber(isDrawn ? "axeDraw" : "axeStow", .49f);
            silentDraw = false;
        }
        if (!drawAxe) silentDraw = false;
        SetAxeVisible(isDrawn);

        float due = chopping ? interaction.FirstPersonImpactDue : airDue;
        float windupStart = chopping ? interaction.FirstPersonWindupStart : airStart;
        if (chopping && due < 0f && interaction.useAnimationEvents && !PlayerInteraction.RhythmChop &&
            !interaction.IsProcessingPrototypeLog)
            due = legacy.Next(interaction.animator, now);
        float swingSpeed = interaction != null ? interaction.AxeSwingSpeed : 1f;
        float phase = ReadPhase(now, due, windupStart, hit, chopping && interaction.IsProcessingPrototypeLog, swingSpeed);
        if (missed) airStart = airDue = -1f;
        bool recovering = now < recoveryEnd;

        float rigScale = Mathf.Max(.01f, Mathf.Abs(source.transform.lossyScale.y) * viewScale * 1.28f);
        if (root.localScale.y != rigScale) root.localScale = Vector3.one * rigScale;
        if (overlay != null && !Mathf.Approximately(overlay.aspect, camera.aspect))
            overlay.aspect = camera.aspect;
        float frame = Mathf.Clamp(camera.aspect / (16f / 9f), .70f, 1.06f);
        float sway = VibeSettings.ReduceEffects ? .22f : 1f;
        float gait = Mathf.Sin(stride) * move * sway;
        float lift = Mathf.Cos(stride * 2f) * move * sway;
        float breath = Mathf.Sin(now * 1.7f) * .005f * sway;
        var carry = new Pose(
            new Vector3(Mathf.Lerp(.240f, .222f, run) * frame + gait * .018f,
                Mathf.Lerp(-.207f, -.235f, run) + breath + lift * Mathf.Lerp(.010f, .015f, run),
                .486f + gait * Mathf.Lerp(.017f, .033f, run)),
            Quaternion.Euler(15f + gait * Mathf.Lerp(3f, 6f, run) - run * 8f,
                -34f + gait * 4f, -30f - run * 12f + lift * 3f));
        float ready = LeoMotionProfile.Smooth(equipped);
        Pose pose = Equip(carry, ready);
        bool swing = hit || phase > 0f;
        if (swing)
        {
            if (!hit && !recovering && interaction != null &&
                (forecastTarget != interaction.CurrentTarget || Mathf.Abs(due - forecastDue) > .03f))
            {
                forecastTarget = interaction.CurrentTarget;
                forecastDue = due;
                groundStrike = interaction.IsProcessingPrototypeLog;
                bool read = groundStrike
                    ? interaction.TryReadPrototypeContact(out var point, out groundCutAxis)
                    : OrbitCamera.TryReadFirstPersonBark(interaction.TargetItem, out point);
                contactWorld = read ? point : (Vector3?)null;
            }
            lastSwing = Pose.Blend(pose, Strike(carry, phase, groundStrike), ready);
            pose = lastSwing;
            cancelAmount = 0f;
        }
        else
        {
            if (wasSwinging)
            {
                cancelAmount = 1f;
                forecastDue = -1f;
            }
            if (cancelAmount > 0f)
            {
                cancelAmount = Mathf.MoveTowards(cancelAmount, 0f, dt * 5.8f);
                pose = Pose.Blend(pose, lastSwing, LeoMotionProfile.Smooth(cancelAmount));
            }
        }
        wasSwinging = swing;
        supportWeight = Mathf.MoveTowards(supportWeight, chopping || swing ? 1f : 0f, dt * (chopping ? 15f : 11f));
        if ((chopping || air) && phase >= .15f && phase <= Contact) supportWeight = 1f;
        float down = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.11f, .81f, -camera.transform.forward.y));
        Apply(pose, ready, gait, lift, run, down);
        SetVisible(true);
        UpdateLighting(now);
        if (stroke != null)
        {
            bool emit = !VibeSettings.ReduceEffects && ready > .85f && phase > .49f && phase <= Contact;
            if (emit && !stroke.emitting) stroke.Clear();
            stroke.emitting = emit;
        }
    }

    float ReadPhase(float now, float due, float windupStart, bool hit, bool ground, float swingSpeed)
    {
        float recovery = Recovery / swingSpeed;
        if (hit)
        {
            // Snapshot the recovery length at contact so releasing the action cannot rewind the pose.
            recoveryStart = now;
            float gap = windupStart > now ? windupStart - now : (due - now) * .48f;
            recoveryEnd = now + (due > now ? Mathf.Clamp(gap, .04f, recovery) : recovery);
            clock.Clear();
            forecastDue = -1f;
            return Contact;
        }
        if (now < recoveryEnd)
        {
            float brace = Mathf.Min(.023f, (recoveryEnd - recoveryStart) * .24f);
            return Mathf.Lerp(Contact, 1f, (now - recoveryStart - brace) / (recoveryEnd - recoveryStart - brace));
        }

        if (due < 0f)
        {
            clock.ForgetForecast();
            return 0f;
        }
        if (windupStart >= 0f) return Contact * Mathf.InverseLerp(windupStart, due, now);
        clock.Forecast(now, due, (ground ? Chop03Balance.GroundWindup : Chop03Balance.StandingWindup) / swingSpeed);
        return clock.Phase(now, Contact, recovery, false);
    }

    static Pose Equip(Pose carry, float ready)
    {
        Vector3 belt = new Vector3(.32f, -.54f, .26f);
        float arc = 4f * ready * (1f - ready);
        return new Pose(Vector3.Lerp(belt, carry.grip, ready) + new Vector3(.06f, -.022f, .075f) * arc,
            Quaternion.Slerp(Quaternion.Euler(42f, -21f, -109f), carry.tool, ready));
    }

    Pose Strike(Pose carry, float phase, bool ground)
    {
        // Meet the shaft low, then carry the head up into the back swing.
        const float braced = .15f;
        float loaded = ground ? .38f : .40f;
        Pose brace = new Pose(new Vector3(.215f, -.167f, .43f), Quaternion.Euler(13f, -23f, -48f), 1f);
        Pose coil = ground
            ? new Pose(new Vector3(.18f, -.016f, .38f), Quaternion.Euler(1f, -14f, -26f), 1f)
            : new Pose(new Vector3(.345f, -.063f, .387f), Quaternion.Euler(-12f, -24f, -39f), 1f);
        if (phase <= braced)
            return Pose.Blend(carry, brace, LeoMotionProfile.Smooth(phase / braced));
        if (phase <= loaded)
            return Pose.Blend(brace, coil, LeoMotionProfile.Smooth((phase - braced) / (loaded - braced)));

        Vector3 edge = ground ? new Vector3(.04f, -.32f, .77f) : new Vector3(.07f, -.12f, .78f);
        Vector3 blade = ground ? new Vector3(-.10f, -.82f, .57f) : Vector3.forward;
        bool projected = false;
        if (contactWorld.HasValue)
        {
            // Recover in camera space from the exact world-space point already used by the VFX.
            Vector3 target = ProjectContact(contactWorld.Value, ground);
            if (target.z > .075f)
            {
                edge = target;
                projected = overlay != null;
                Vector3 direction = Quaternion.Inverse(camera.transform.rotation) *
                    Vector3.ProjectOnPlane(contactWorld.Value - camera.transform.position, Vector3.up);
                if (!ground && direction.sqrMagnitude > .0001f)
                    blade = direction.normalized;
            }
        }

        Vector3 shaft = ground ? new Vector3(.43f, .63f, .61f) : new Vector3(.64f, .42f, .63f);
        if (ground && contactWorld.HasValue && groundCutAxis.sqrMagnitude > .001f)
        {
            Vector3 toward = Vector3.ProjectOnPlane(contactWorld.Value - camera.transform.position, Vector3.up);
            Quaternion acrossCut = Quaternion.Inverse(root.rotation) *
                LeoAxePose.GroundTool(toward, groundCutAxis, camera.transform.right);
            shaft += (acrossCut * Vector3.up) * .10f;
            Vector3 cutDown = -Vector3.ProjectOnPlane(Vector3.up, groundCutAxis).normalized;
            blade = Quaternion.Inverse(root.rotation) * cutDown + new Vector3(-.52f, 0f, .15f);
        }
        shaft.Normalize();
        Quaternion impactTool = Quaternion.LookRotation(Vector3.ProjectOnPlane(blade, shaft), shaft);
        Pose strike = ContactPose(edge, impactTool, projected);
        if (phase <= Contact)
        {
            float t = Mathf.Pow((phase - loaded) / (Contact - loaded), 1.45f);
            Vector3 path = LeoMotionProfile.Bezier(coil.grip,
                coil.grip + new Vector3(-.03f, .10f, -.008f),
                strike.grip + new Vector3(.19f, .13f, -.11f), strike.grip, t);
            return new Pose(path, Quaternion.Slerp(coil.tool, strike.tool, t), 1f);
        }

        float recover = (phase - Contact) / (1f - Contact);
        var rebound = new Pose(strike.grip + new Vector3(.028f, .026f, -.080f) * contactRecoil,
            strike.tool * Quaternion.Euler(-20f * contactRecoil, -7f, 13f), 1f);
        const float recoilEnd = .27f;
        if (recover < recoilEnd)
        {
            float t = 1f - Mathf.Pow(1f - recover / recoilEnd, 2.4f);
            return Pose.Blend(strike, rebound, t);
        }

        float returnT = LeoMotionProfile.Smooth((recover - recoilEnd) / (1f - recoilEnd));
        Pose rest = Pose.Blend(rebound, carry, returnT);
        return new Pose(rest.grip + Vector3.down * (.063f * Mathf.Sin(Mathf.PI * returnT)),
            rest.tool, 1f - LeoMotionProfile.Smooth((returnT - .22f) / .78f));
    }

    void Apply(Pose pose, float draw, float gait, float lift, float run, float down)
    {
        var rightRest = new Vector3(.265f, -.411f + down * .246f + run * .096f + lift * .011f,
            .365f + down * .130f + gait * .047f + run * .055f);
        var leftRest = new Vector3(-.274f, -.471f + down * .226f + run * .085f - lift * .012f,
            .338f + down * .125f - gait * .044f + run * .049f);
        var rightRestRotation = Quaternion.Euler(-58f + gait * 9f, -9f, -14f) * Quaternion.Euler(0f, -77f, 0f);
        var leftRestRotation = Quaternion.Euler(-57f - gait * 9f, 17f, 11f) * Quaternion.Euler(0f, 73f, 0f);

        float rightJoin = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.02f, .216f, draw));
        float leftJoin = LeoMotionProfile.Smooth(supportWeight) * rightJoin;
        Vector3 main = FitAxeGrip(pose, leftJoin, out var rightArm, out var leftArm);
        axe.localRotation = pose.tool;
        axe.localPosition = main - pose.tool * mainGrip.localPosition;
        Vector3 rightGoal = Vector3.Lerp(rightRest, main - rightArm.offset, rightJoin);
        Vector3 support = axe.localPosition + pose.tool * supportGrip.localPosition;
        Vector3 leftGoal = Vector3.Lerp(leftRest, support - leftArm.offset, leftJoin);
        rightArm.AllowBend(rightGoal);
        leftArm.AllowBend(leftGoal);
        right.upper.localPosition = rightArm.shoulder;
        left.upper.localPosition = leftArm.shoulder;
        right.Pose(root, rightGoal, rightArm.pole, Quaternion.Slerp(rightRestRotation, rightArm.wrist, rightJoin), rightJoin);
        left.Pose(root, leftGoal, leftArm.pole, Quaternion.Slerp(leftRestRotation, leftArm.wrist, leftJoin), leftJoin);
    }

    struct ArmPlacement
    {
        internal Vector3 shoulder, pole, offset;
        internal Quaternion wrist;

        internal void AllowBend(Vector3 goal)
        {
            const float minimum = .089f;
            Vector3 delta = goal - shoulder;
            float length = delta.magnitude;
            if (length >= minimum) return;
            Vector3 direction = length > .001f ? delta / length : Vector3.forward;
            Vector3 move = direction * (length - minimum);
            shoulder += move;
            pole += move;
        }
    }

    Vector3 FitAxeGrip(Pose pose, float leftJoin, out ArmPlacement rightArm, out ArmPlacement leftArm)
    {
        Vector3 main = pose.grip;
        Vector3 supportDelta = pose.tool * (supportGrip.localPosition - mainGrip.localPosition);
        Vector3 lean = new Vector3(Mathf.Clamp((main.x - .15f) * .24f, -.08f, .07f),
            Mathf.Clamp((main.y + .17f) * .27f, -.12f, .06f),
            Mathf.Clamp((main.z - .37f) * .50f, 0f, .24f)) * pose.extension;
        Vector3 rightShoulder = RightShoulder + lean;
        Vector3 leftShoulder = LeftShoulder + Vector3.Scale(lean, new Vector3(.52f, .85f, 1.12f));
        Vector3 rightPole = rightShoulder + new Vector3(.20f, -.19f, .062f);
        Vector3 leftPole = leftShoulder + new Vector3(-.17f, -.20f, .055f);
        Vector3 shaft = pose.tool * Vector3.up;
        Quaternion rightWrist = right.GripWrist(main, rightPole, shaft);
        Quaternion leftWrist = left.GripWrist(main + supportDelta, leftPole, -shaft);
        Vector3 rightOffset = rightWrist * right.gripCenter;
        Vector3 leftOffset = leftWrist * left.gripCenter;
        Vector3 rightCenter = rightShoulder + rightOffset;
        Vector3 leftCenter = leftShoulder + leftOffset - supportDelta;
        // Shift the shoulders before moving the tool. Both arms still solve against one handle.
        Vector3 shiftRight = Vector3.ClampMagnitude(main - Limit(main, rightCenter, right.reach), .16f * pose.extension);
        Vector3 shiftLeft = Vector3.ClampMagnitude(main - Limit(main, leftCenter, left.reach), .16f * pose.extension * leftJoin);
        rightArm = new ArmPlacement
        {
            shoulder = rightShoulder + shiftRight, pole = rightPole + shiftRight, offset = rightOffset, wrist = rightWrist
        };
        leftArm = new ArmPlacement
        {
            shoulder = leftShoulder + shiftLeft, pole = leftPole + shiftLeft, offset = leftOffset, wrist = leftWrist
        };
        return leftJoin >= .995f
            ? FitBothGrips(main, rightCenter + shiftRight, right.reach, leftCenter + shiftLeft, left.reach)
            : Limit(main, rightCenter + shiftRight, right.reach);
    }

    Pose ContactPose(Vector3 edge, Quaternion roll, bool projected)
    {
        Vector3 edgeFromGrip = cuttingEdge - mainGrip.localPosition;
        Vector3 edgeOffset = roll * edgeFromGrip;
        var desired = new Pose(edge - edgeOffset, roll, 1f);
        if (!projected) return desired;
        Vector3 fitted = FitAxeGrip(desired, 1f, out _, out _);
        float excess = Vector3.Distance(fitted, desired.grip);
        if (excess <= .00001f) return desired;

        // Fit depth along the same screen ray, then align the blade with it. This keeps
        // the edge at the notch even on an oblique cut that would otherwise overextend an arm.
        Vector3 ray = edge.normalized;
        var comfortable = new Vector3(.08f + Mathf.Clamp(ray.x * .12f, -.06f, .055f),
            -.205f + Mathf.Clamp(ray.y * .16f, -.085f, .035f), .345f);
        Vector3 grip = Vector3.Lerp(fitted, comfortable, Mathf.Clamp01(excess / .095f));
        Quaternion tool = roll;
        for (int i = 0; i < 3; i++)
        {
            float along = Vector3.Dot(grip, ray);
            float discriminant = edgeFromGrip.sqrMagnitude - grip.sqrMagnitude + along * along;
            float depth = along + Mathf.Sqrt(Mathf.Max(0f, discriminant));
            tool = Quaternion.FromToRotation(edgeOffset, ray * depth - grip) * roll;
            var pose = new Pose(grip, tool, 1f);
            fitted = FitAxeGrip(pose, 1f, out _, out _);
            if ((fitted - grip).sqrMagnitude < .0000000001f) return pose;
            grip = Vector3.Lerp(fitted, comfortable, .35f);
        }
        return new Pose(fitted, tool, 1f);
    }

    static Vector3 FitBothGrips(Vector3 grip, Vector3 right, float rightReach, Vector3 left, float leftReach)
    {
        Vector3 onRight = Limit(grip, right, rightReach);
        if ((onRight - left).sqrMagnitude <= leftReach * leftReach) return onRight;
        Vector3 onLeft = Limit(grip, left, leftReach);
        if ((onLeft - right).sqrMagnitude <= rightReach * rightReach) return onLeft;

        // The closest point is on the circle where both arm-reach spheres meet.
        Vector3 span = left - right;
        float distance = span.magnitude;
        if (distance < .001f) return onLeft;
        Vector3 axis = span / distance;
        float slice = (rightReach * rightReach - leftReach * leftReach + span.sqrMagnitude) / (2f * distance);
        Vector3 center = right + axis * slice;
        float radius = Mathf.Sqrt(Mathf.Max(0f, rightReach * rightReach - slice * slice));
        return center + Vector3.ProjectOnPlane(grip - center, axis).normalized * radius;
    }

    static Vector3 Limit(Vector3 value, Vector3 center, float radius) =>
        center + Vector3.ClampMagnitude(value - center, radius);
}
