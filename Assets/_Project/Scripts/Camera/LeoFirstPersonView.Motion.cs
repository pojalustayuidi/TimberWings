using UnityEngine;

internal sealed partial class LeoFirstPersonView
{
    const float Contact = .62f;
    const float Recovery = .29f;
    static readonly Vector3 RightShoulder = new Vector3(.27f, -.32f, .075f);

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
    float equipped, stowPath, carryUntil, cancelAmount;
    float recoveryStart, recoveryEnd, contactRecoil = 1f;

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
        SetAxe(interaction != null ? interaction.EquippedAxe : AxeCatalog.Get(AxeId.Old));
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
        float swingSpeed = interaction != null ? interaction.AxeSwingSpeed : 1f;
        float phase = ReadPhase(now, due, hit, chopping && interaction.IsProcessingPrototypeLog, swingSpeed);
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
        bool visible = ready > .04f;
        if (!visible)
        {
            SetVisible(false);
            return;
        }
        Apply(pose, lift, groundStrike && (swing || cancelAmount > 0f));
        SetVisible(true);
        UpdateLighting(now);
        if (stroke != null)
        {
            bool emit = !VibeSettings.ReduceEffects && ready > .85f && phase > .49f && phase <= Contact;
            if (emit && !stroke.emitting) stroke.Clear();
            stroke.emitting = emit;
        }
    }

    float ReadPhase(float now, float due, bool hit, bool ground, float swingSpeed)
    {
        float recovery = Recovery / swingSpeed;
        if (hit)
        {
            // Snapshot the recovery length at contact so releasing the action cannot rewind the pose.
            recoveryStart = now;
            recoveryEnd = now + (due > now ? Mathf.Clamp((due - now) * .48f, .04f, recovery) : recovery);
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
        clock.Forecast(now, due, (ground ? Chop03Balance.GroundWindup : Chop03Balance.StandingWindup) / swingSpeed);
        return clock.Phase(now, Contact, recovery, false);
    }

    static Pose Equip(Pose carry, float ready, float stow)
    {
        Vector3 belt = new Vector3(.31f, -.72f, .25f);
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
            Vector3 target = ProjectContact(contactWorld.Value);
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
        var strike = new Pose(edge - impactTool * (cuttingEdge - LeoAxePose.MainGrip), impactTool, 1f);
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

    void Apply(Pose pose, float lift, bool lowerSwing)
    {
        Vector3 main = pose.grip;
        float extension = pose.extension;
        float lean = Mathf.Clamp((main.z - .40f) * 1.04f, 0f, lowerSwing ? .67f : .52f) * extension;
        float shift = Mathf.Clamp((main.x - .19f) * .35f, -.10f, .07f) * extension;
        float drop = Mathf.Clamp((-.14f - main.y) * .38f, -.06f, .18f) * extension;
        right.upper.localPosition = RightShoulder + new Vector3(shift, -drop + lift * .012f, lean);
        Vector3 pole = right.upper.localPosition + new Vector3(.19f, -.16f, .07f);
        Vector3 shaft = pose.tool * Vector3.up;
        Quaternion wrist = right.GripWrist(root, main, pole, LeoAxePose.RightWrist(pose.tool), shaft);
        Vector3 gripOffset = wrist * LeoFirstPersonHandMesh.GripCenter;
        Vector3 fitted = Limit(main, right.upper.localPosition + gripOffset, right.reach);
        // Lean into the cut without stretching the forearm or letting the palm leave the handle.
        Vector3 bend = Vector3.ClampMagnitude(main - fitted, lowerSwing ? .31f : .20f) * extension;
        right.upper.localPosition += bend;
        main = fitted + bend;
        axe.localRotation = pose.tool;
        axe.localPosition = main - pose.tool * LeoAxePose.MainGrip;
        right.Pose(root, main - gripOffset, pole + bend, wrist, 1f);
    }

    static Vector3 Limit(Vector3 value, Vector3 center, float radius) =>
        center + Vector3.ClampMagnitude(value - center, radius);
}
