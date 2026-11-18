using UnityEngine;

internal sealed partial class LeoFirstPersonView
{
    const float Contact = .62f;
    const float Recovery = .29f;
    const float StandingWindup = .44f;
    const float GroundWindup = .42f;
    static readonly Vector3 RightShoulder = new Vector3(.285f, -.30f, .075f);
    static readonly Vector3 LeftShoulder = new Vector3(-.285f, -.30f, .075f);
    // Leading lip of the baked cutting edge at the center of the axe profile.
    static readonly Vector3 CuttingEdge = new Vector3(0f, .23748f, .28564f);
    static readonly Vector3 SupportGrip = new Vector3(0f, -.183f, 0f);

    struct Pose
    {
        internal readonly Vector3 grip;
        internal readonly Quaternion tool;
        internal readonly float support;

        internal Pose(Vector3 grip, Quaternion tool, float support = 0f)
        {
            this.grip = grip;
            this.tool = tool;
            this.support = support;
        }

        internal static Pose Blend(Pose a, Pose b, float t) => new Pose(
            Vector3.Lerp(a.grip, b.grip, t), Quaternion.Slerp(a.tool, b.tool, t),
            Mathf.Lerp(a.support, b.support, t));
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
    float equipped, stowPath, carryUntil, cancelAmount;
    float recoveryStart, recoveryEnd;
    float needleAngle;

    internal void Hit(PlayerInteraction interaction, bool fell)
    {
        if (suspended || interaction == null || !interaction.IsChopping || impactFrame == Time.frameCount) return;
        if ((fell && interaction.TargetItem == null) || (!fell && interaction.CurrentTarget == null)) return;
        impactFrame = Time.frameCount;
        groundStrike = interaction.TryReadPrototypeContact(out _, out groundCutAxis);
        contactWorld = interaction.TryReadLeoImpact(out var point) ? point : (Vector3?)null;
    }

    internal void Suspend()
    {
        if (suspended) return;
        ReleaseMotion();
        SetRendered(false);
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
        equipped = stowPath = carryUntil = cancelAmount = 0f;
        recoveryStart = recoveryEnd = -1f;
        if (stroke != null)
        {
            stroke.emitting = false;
            stroke.Clear();
        }
    }

    internal void Step(PlayerInteraction interaction, bool manualCarry, float dt, float stride, float move, float run, float viewScale)
    {
        suspended = false;
        float now = Time.time;
        bool chopping = interaction != null && interaction.IsChopping;
        bool hit = impactFrame == Time.frameCount;
        if (chopping || hit) carryUntil = now + .80f;
        bool draw = manualCarry || chopping || hit || now < carryUntil;
        equipped = hit ? 1f : Mathf.MoveTowards(equipped, draw ? 1f : 0f, dt / (draw ? .19f : .35f));
        stowPath = Mathf.MoveTowards(stowPath, draw ? 0f : 1f, dt * 7f);

        float due = chopping ? interaction.FirstPersonImpactDue : -1f;
        if (chopping && due < 0f && interaction.useAnimationEvents && !PlayerInteraction.RhythmChop &&
            !interaction.IsProcessingPrototypeLog)
            due = legacy.Next(interaction.animator, now);
        float phase = ReadPhase(now, due, hit, chopping && interaction.IsProcessingPrototypeLog);
        bool recovering = now < recoveryEnd;

        // Keep the weapon at real world depth, with fuller arms for the closer viewpoint.
        float rigScale = Mathf.Max(.01f, Mathf.Abs(source.transform.lossyScale.y) * viewScale * 1.38f);
        if (root.localScale.y != rigScale) root.localScale = Vector3.one * rigScale;
        float frame = Mathf.Clamp(Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f) /
            Mathf.Tan(68f * Mathf.Deg2Rad * .5f), .75f, 1.42f);
        float reduced = VibeSettings.ReduceEffects ? .46f : 1f;
        float gait = Mathf.Sin(stride) * move * reduced;
        float lift = Mathf.Cos(stride * 2f) * move * reduced;
        float breath = Mathf.Sin(now * 1.7f) * .007f * reduced;
        var carry = new Pose(
            new Vector3(Mathf.Lerp(.268f, .25f, run) * frame + gait * .024f,
                Mathf.Lerp(-.190f, -.215f, run) * frame + breath + lift * Mathf.Lerp(.014f, .022f, run),
                .432f + (1f - frame) * .065f + gait * Mathf.Lerp(.024f, .050f, run)),
            Quaternion.Euler(15f + gait * Mathf.Lerp(4f, 8f, run) - run * 12f,
                -35f + gait * 6f, -35f - run * 15f + lift * 4f));
        float ready = LeoMotionProfile.Smooth(equipped);
        Pose pose = Equip(carry, ready, stowPath);
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
        Apply(pose, ready, gait, lift, frame, groundStrike && (swing || cancelAmount > 0f));
        UpdateLighting(now);
        UpdateCompass(dt);
        if (stroke != null)
        {
            bool emit = !VibeSettings.ReduceEffects && ready > .85f && phase > .49f && phase <= Contact;
            if (emit && !stroke.emitting) stroke.Clear();
            stroke.emitting = emit;
        }
    }

    void UpdateCompass(float dt)
    {
        if (compassNeedle == null) return;
        Vector3 north = compassNeedle.parent.InverseTransformDirection(Vector3.forward);
        if (north.x * north.x + north.y * north.y < .018f) return;
        // The brass tip in Leo_Model is authored 57 degrees clockwise from dial up.
        float angle = Mathf.Atan2(-north.x, north.y) * Mathf.Rad2Deg + 57f;
        needleAngle = Mathf.LerpAngle(needleAngle, angle, 1f - Mathf.Exp(-12f * dt));
        compassNeedle.localRotation = Quaternion.AngleAxis(needleAngle, Vector3.forward);
    }

    float ReadPhase(float now, float due, bool hit, bool ground)
    {
        if (hit)
        {
            // Snapshot the recovery length at contact so releasing the action cannot rewind the pose.
            recoveryStart = now;
            recoveryEnd = now + (due > now ? Mathf.Clamp((due - now) * .48f, .04f, Recovery) : Recovery);
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
        clock.Forecast(now, due, ground ? GroundWindup : StandingWindup);
        return clock.Phase(now, Contact, Recovery, false);
    }

    static Pose Equip(Pose carry, float ready, float stow)
    {
        Vector3 belt = new Vector3(.46f, -.75f, .17f);
        float arc = 4f * ready * (1f - ready);
        Vector3 sweep = Vector3.Lerp(new Vector3(.08f, -.018f, .135f), new Vector3(.145f, -.080f, .060f), stow);
        return new Pose(Vector3.Lerp(belt, carry.grip, ready) + sweep * arc,
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
        if (contactWorld.HasValue)
        {
            // Recover in camera space from the exact world-space point already used by the VFX.
            Vector3 target = root.InverseTransformPoint(contactWorld.Value);
            if (target.z > .075f)
            {
                edge = target;
                Vector3 direction = Quaternion.Inverse(camera.transform.rotation) *
                    Vector3.ProjectOnPlane(contactWorld.Value - camera.transform.position, Vector3.up);
                if (!ground && direction.sqrMagnitude > .0001f)
                    blade = direction.normalized;
            }
        }

        Vector3 shaft = ground ? new Vector3(.61f, -.14f, .78f) : new Vector3(.75f, -.13f, .65f);
        Quaternion impactTool = Quaternion.LookRotation(Vector3.ProjectOnPlane(blade, shaft), shaft);
        if (ground && contactWorld.HasValue && groundCutAxis.sqrMagnitude > .001f)
        {
            Vector3 toward = Vector3.ProjectOnPlane(contactWorld.Value - camera.transform.position, Vector3.up);
            impactTool = Quaternion.Inverse(root.rotation) *
                LeoAxePose.GroundTool(toward, groundCutAxis, camera.transform.right);
        }
        var strike = new Pose(edge - impactTool * (CuttingEdge - LeoAxePose.MainGrip), impactTool, 1f);
        if (phase <= Contact)
        {
            float t = Mathf.Pow((phase - loaded) / (Contact - loaded), 1.45f);
            Vector3 path = LeoMotionProfile.Bezier(coil.grip,
                coil.grip + new Vector3(-.03f, .10f, -.008f),
                strike.grip + new Vector3(.19f, .13f, -.11f), strike.grip, t);
            return new Pose(path, Quaternion.Slerp(coil.tool, strike.tool, t), 1f);
        }

        float recover = (phase - Contact) / (1f - Contact);
        var rebound = new Pose(strike.grip + new Vector3(.028f, .026f, -.080f),
            strike.tool * Quaternion.Euler(-20f, -7f, 13f), 1f);
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

    void Apply(Pose pose, float ready, float gait, float lift, float frame, bool lowerSwing)
    {
        Vector3 main = pose.grip;
        float brace = pose.support;
        // At the wider tree stance, reach with the shoulders before extending the wrists.
        float lean = Mathf.Clamp((main.z - .34f) * (lowerSwing ? 1.15f : .96f), 0f, lowerSwing ? .61f : .46f) * brace;
        float shift = Mathf.Clamp((main.x - .15f) * .27f, -.065f, .067f) * brace;
        float dip = Mathf.Clamp((-.24f - main.y) * .32f, 0f, .10f) * brace;
        float rise = Mathf.Clamp((main.y + .12f) * .35f, 0f, .070f) * brace;
        Vector3 shoulderOffset = new Vector3(shift, rise - dip + lift * .012f, lean);
        right.upper.localPosition = RightShoulder + shoulderOffset;
        left.upper.localPosition = LeftShoulder + shoulderOffset;
        Vector3 rightPole = new Vector3(.53f + shift, -.325f, .135f + lean);
        Vector3 leftPole = new Vector3(-.53f + shift + .06f * brace, -.328f - .08f * brace, .12f + lean - .155f * brace);
        Vector3 shaft = pose.tool * Vector3.up;
        Vector3 supportOffset = pose.tool * (SupportGrip - LeoAxePose.MainGrip);
        float clasp = LeoMotionProfile.Smooth(ready / .26f);
        Quaternion wristR = LeoAxePose.RightWrist(pose.tool);
        Quaternion wristL = LeoAxePose.RightWrist(pose.tool * Quaternion.AngleAxis(180f, Vector3.up));
        if (clasp > .001f)
            wristR = right.GripWrist(root, main, rightPole, wristR, shaft);
        if (brace > .001f)
            wristL = left.GripWrist(root, main + supportOffset, leftPole, wristL, shaft);

        Vector3 gripCenter = LeoFirstPersonHandMesh.GripCenter;
        Vector3 rightOffset = -(wristR * gripCenter);
        Vector3 leftOffset = supportOffset - wristL * gripCenter;

        // Keep both wrist targets inside arm reach by moving the tool as a whole.
        // This preserves the spacing of the two grips even near the edge of reach.
        Vector3 centerR = right.upper.localPosition - rightOffset;
        Vector3 centerL = left.upper.localPosition - leftOffset;
        Vector3 fitted = Limit(main, centerR, right.reach);
        Vector3 fittedBoth = fitted;
        if (brace > .001f)
        {
            for (int i = 0; i < 8; i++)
                fittedBoth = Limit(Limit(fittedBoth, centerL, left.reach), centerR, right.reach);
        }
        if (lowerSwing && brace > .001f)
        {
            // As the player bends towards the low log, translate both view
            // shoulders together within a small range. Arm lengths stay fixed.
            Vector3 bend = Vector3.ClampMagnitude(main - fittedBoth, .21f) * brace;
            right.upper.localPosition += bend;
            left.upper.localPosition += bend;
            rightPole += bend;
            leftPole += bend;
            fitted += bend;
            fittedBoth += bend;
        }
        main = Vector3.Lerp(fitted, fittedBoth, LeoMotionProfile.Smooth(brace));
        axe.localRotation = pose.tool;
        axe.localPosition = main - pose.tool * LeoAxePose.MainGrip;
        // The tool drops below the frame before its renderers are turned off.
        bool showAxe = ready > .025f;
        if (axe.gameObject.activeSelf != showAxe) axe.gameObject.SetActive(showAxe);

        Vector3 rightRest = new Vector3(.270f * frame, -.251f * frame + lift * .020f, .407f + gait * .054f);
        Vector3 leftRest = new Vector3(-.257f * frame, -.240f * frame - lift * .016f - ready * .043f,
            .416f - gait * .058f);
        right.Pose(root, Vector3.Lerp(rightRest, main + rightOffset, clasp), rightPole,
            Quaternion.Slerp(RelaxedWrist(1f, gait), wristR, clasp), clasp);
        left.Pose(root, Vector3.Lerp(leftRest, main + leftOffset, brace), leftPole,
            Quaternion.Slerp(RelaxedWrist(-1f, -gait), wristL, brace), brace);

        if (compass != null)
            PoseCompass();
    }

    static Quaternion RelaxedWrist(float side, float swing)
    {
        // Local -Y runs toward the fingertips and -Z faces the knuckles.
        Vector3 fingers = new Vector3(-side * .20f, -.24f + swing * .13f, .96f).normalized;
        Vector3 palm = Vector3.ProjectOnPlane(new Vector3(-side * .36f, -1f, -.06f), fingers);
        return Quaternion.LookRotation(palm, -fingers);
    }

    void PoseCompass()
    {
        Vector3 upArm = (left.elbow.position - left.wrist.position).normalized;
        Vector3 face = Vector3.ProjectOnPlane(-left.wrist.forward, upArm);
        if (face.sqrMagnitude < .01f)
            face = Vector3.ProjectOnPlane(-left.forearm.forward, upArm);
        if (face.sqrMagnitude < .0001f) return;
        compass.SetPositionAndRotation(left.wrist.position + upArm * (.034f * root.lossyScale.y),
            Quaternion.LookRotation(face, upArm));
    }

    static Vector3 Limit(Vector3 value, Vector3 center, float radius) =>
        center + Vector3.ClampMagnitude(value - center, radius);
}
