using UnityEngine;

public sealed partial class LeoModelPresentation
{
    readonly LeoImpactClock chopClock = new LeoImpactClock();
    readonly LeoLegacyTiming legacyTiming = new LeoLegacyTiming();
    LeoMotionProfile.Book chopBook;
    LeoMotionProfile.ChopPose chopPose;
    ResourceItem chopTarget;
    Vector3? chopBark;
    int chopHitFrame = -1;
    float chopWeight, chopPhase;
    TrailRenderer chopTrail;
    bool trailAttempted;

    void ResetChopPose()
    {
        chopClock.Clear();
        legacyTiming.Clear();
        chopTarget = null;
        chopBark = null;
        chopHitFrame = -1;
        chopWeight = chopPhase = 0f;
        if (chopTrail != null)
        {
            chopTrail.emitting = false;
            chopTrail.Clear();
        }
    }

    void OnHit()
    {
        if (interaction == null || !interaction.IsChopping) return;
        chopHitFrame = Time.frameCount;
        chopClock.Hit(Time.time);
        chopTarget = interaction.TargetItem;
        chopBark = interaction.TryReadLeoImpact(out Vector3 point) ? point : (Vector3?)null;
    }

    void OnTreeFelled()
    {
        if (chopHitFrame != Time.frameCount) OnHit();
    }

    void ReadChopPose(float now, float dt, bool chopping, bool blocked)
    {
        if (chopBook == null) return;
        if (blocked)
        {
            ResetChopPose();
            return;
        }

        bool impactFrame = chopHitFrame == Time.frameCount;
        if (chopping && !impactFrame && chopTarget != interaction.TargetItem)
        {
            ResetChopPose();
            chopTarget = interaction.TargetItem;
        }

        float due = chopping && interaction.AnimationHasScheduledImpact ? interaction.AnimationScheduledImpact : -1f;
        if (chopping && due < 0f && interaction.useAnimationEvents && !PlayerInteraction.RhythmChop)
            due = legacyTiming.Next(interaction.animator, now);
        if (due >= 0f) chopClock.Forecast(now, due, chopBook.windup);
        else chopClock.ForgetForecast();

        bool recovering = chopClock.Recovering(now, chopBook.recovery);
        if (chopping || recovering || impactFrame)
        {
            chopPhase = chopClock.Phase(now, chopBook.contact, chopBook.recovery, impactFrame);
            chopWeight = impactFrame ? 1f : Mathf.MoveTowards(chopWeight, 1f, dt * 12f);
            if (!impactFrame && !recovering)
                chopBark = LeoChopContact.TryPoint(chopTarget, transform, out Vector3 point) ? point : (Vector3?)null;
        }
        else
        {
            chopWeight = Mathf.MoveTowards(chopWeight, 0f, dt * 8f);
            if (chopWeight == 0f) chopPhase = 0f;
        }

        chopPose = LeoMotionProfile.Chop(chopBook, chopPhase);
    }

    void BraceForChop()
    {
        if (chopWeight <= 0f) return;
        float aim = LeoAxePose.ContactAim(chopPhase, chopBook.contact);
        if (rig.hips != null)
            rig.hips.localPosition += LeoAxePose.BodyShift(chopPose, aim, transform, chopBark) * chopWeight;
        rig.Rotate(rig.hips, new Vector3(0f, chopPose.chest.y * .23f, 0f) * chopWeight);
        rig.Rotate(rig.chest, chopPose.chest * chopWeight);
        rig.Rotate(rig.head, chopPose.head * chopWeight);
    }

    void PlaceChopTools()
    {
        float action = chopWeight * (1f - holster);
        float roll = action > 0f
            ? LeoAxePose.RightArm(rig, transform, chopBook, chopPose, chopPhase, action, chopBark)
            : 0f;
        LeoAxePose.Place(rig, transform, holster, roll);
        LeoAxePose.LeftArm(rig, transform, chopPhase, action * chopPose.left);
        UpdateChopTrail(action);
    }

    void UpdateChopTrail(float action)
    {
        bool emit = action > .8f && chopPhase > .43f && chopPhase < .66f && !VibeSettings.ReduceEffects;
        if (emit && !trailAttempted)
        {
            trailAttempted = true;
            Material mat = Resources.Load<Material>("LeoAnimation/Leo_AxeStroke");
            if (mat != null && mat.shader != null && mat.shader.isSupported && rig.axeRoot != null)
            {
                var tip = new GameObject("Leo Axe Stroke");
                tip.layer = rig.axeRoot.gameObject.layer;
                tip.transform.SetParent(rig.axeRoot, false);
                tip.transform.localPosition = LeoAxePose.Edge;
                chopTrail = tip.AddComponent<TrailRenderer>();
                chopTrail.sharedMaterial = mat;
                chopTrail.time = .055f;
                chopTrail.minVertexDistance = .006f;
                chopTrail.startWidth = .063f * Mathf.Abs(transform.lossyScale.y);
                chopTrail.endWidth = 0f;
                chopTrail.startColor = new Color(1f, .83f, .52f, .27f);
                chopTrail.endColor = new Color(1f, .72f, .41f, 0f);
                chopTrail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                chopTrail.receiveShadows = false;
                chopTrail.emitting = false;
            }
        }
        if (chopTrail == null) return;
        if (emit && !chopTrail.emitting) chopTrail.Clear();
        chopTrail.emitting = emit;
    }
}
