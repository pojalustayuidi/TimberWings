using System;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class LeoModelPresentation
{
    sealed class Foot
    {
        public Transform hip, knee, ankle;
        public Vector3 rest;
        public Quaternion restRotation, ankleBindRotation;
        public Vector3[] sole;
        public float soleY;
        public bool groundSampled;
        public float groundY;
        public Vector3 groundUp;
        public bool planted;
        public Vector3 anchor;
    }

    readonly LeoMotionProfile.Gait gait = new LeoMotionProfile.Gait();
    readonly RaycastHit[] footHits = new RaycastHit[12];
    LeoMotionProfile.Book locomotion;
    Foot leftFoot, rightFoot;
    Quaternion previousFacing;
    float cycle = .25f;
    float motionSpeed, jogWeight, moveWeight, turnWeight;

    void InitializeLocomotion()
    {
        locomotion = new LeoMotionProfile.Book();
        LeoMotionProfile.Polish(locomotion);
        leftFoot = CacheFoot(rig.hipL, rig.kneeL, rig.ankleL);
        rightFoot = CacheFoot(rig.hipR, rig.kneeR, rig.ankleR);
        ResetLocomotion();
    }

    Foot CacheFoot(Transform hip, Transform knee, Transform ankle)
    {
        if (hip == null || knee == null || ankle == null) return null;
        var points = new List<Vector3>(8);
        float soleY = float.PositiveInfinity;
        foreach (var filter in ankle.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null || !filter.name.EndsWith("__Sole", StringComparison.Ordinal))
                continue;

            Bounds b = filter.sharedMesh.bounds;
            for (int end = 0; end < 2; end++)
            {
                var corner = new Vector3(b.center.x, b.min.y, end == 0 ? b.min.z : b.max.z);
                Vector3 point = ankle.InverseTransformPoint(filter.transform.TransformPoint(corner));
                points.Add(point);
                soleY = Mathf.Min(soleY, point.y);
            }
        }
        if (points.Count == 0)
        {
            soleY = -.1565f;
            points.Add(new Vector3(0f, soleY, -.115f));
            points.Add(new Vector3(0f, soleY, .247f));
        }
        return new Foot
        {
            hip = hip,
            knee = knee,
            ankle = ankle,
            rest = transform.InverseTransformPoint(ankle.position),
            restRotation = Quaternion.Inverse(transform.rotation) * ankle.rotation,
            ankleBindRotation = ankle.localRotation,
            sole = points.ToArray(),
            soleY = soleY
        };
    }

    void ResetLocomotion()
    {
        previousFacing = transform.rotation;
        cycle = .25f;
        motionSpeed = jogWeight = moveWeight = turnWeight = 0f;
        if (leftFoot != null) leftFoot.planted = leftFoot.groundSampled = false;
        if (rightFoot != null) rightFoot.planted = rightFoot.groundSampled = false;
    }

    void ApplyLocomotion(float travel, float frameTime, float now, float carry, bool animate, bool probeGround)
    {
        float dt = Mathf.Min(.1f, frameTime);
        if (travel > Mathf.Max(.8f, frameTime * 20f))
        {
            ResetLocomotion();
            travel = 0f;
        }

        float speed = animate ? travel / Mathf.Max(.001f, frameTime) : 0f;
        if (speed < .05f) speed = 0f;
        motionSpeed = Mathf.Lerp(motionSpeed, Mathf.Min(speed, 18f), 1f - Mathf.Exp(-12f * dt));
        float targetWeight = animate ? LeoMotionProfile.Smooth((motionSpeed - .055f) / .72f) : 0f;
        moveWeight = Mathf.MoveTowards(moveWeight, targetWeight, dt * (targetWeight > moveWeight ? 8f : 5.5f));
        jogWeight = Mathf.Lerp(jogWeight, LeoMotionProfile.Smooth((motionSpeed - 1.08f) / 2.62f), 1f - Mathf.Exp(-8f * dt));
        LeoMotionProfile.MixInto(gait, locomotion.walk, locomotion.run, jogWeight);

        float scale = Mathf.Max(.01f, transform.lossyScale.y);
        if (animate && speed > 0f && moveWeight > .001f)
        {
            float advance = Mathf.Min(travel / (gait.stride * scale), LeoMotionProfile.MaxCadence(jogWeight) * dt);
            cycle = Mathf.Repeat(cycle + advance, 1f);
        }
        else if (moveWeight <= .001f)
        {
            cycle = .25f;
        }

        float yaw = Mathf.DeltaAngle(previousFacing.eulerAngles.y, transform.eulerAngles.y);
        previousFacing = transform.rotation;
        float turn = Mathf.Clamp(yaw / Mathf.Max(.001f, frameTime) / 235f, -1f, 1f);
        turnWeight = Mathf.Lerp(turnWeight, turn, 1f - Mathf.Exp(-11f * dt));
        float release = Mathf.Max(Mathf.Abs(turn), Mathf.Abs(turnWeight));
        float strideAngle = cycle * Mathf.PI * 2f;
        float swing = Mathf.Cos(strideAngle);
        float sway = Mathf.Sin(strideAngle);
        float breath = Mathf.Sin(now * 1.75f);
        float idle = 1f - moveWeight;
        float stepBob = Mathf.Cos((cycle - gait.stance * .5f) * Mathf.PI * 4f) * (1f - 1.68f * jogWeight);
        float bodyLife = animate ? 1f : 0f;

        if (rig.hips != null)
        {
            var offset = new Vector3(.009f * breath * idle - .022f * sway * moveWeight,
                -.007f * idle + (-gait.drop + gait.bob * stepBob) * moveWeight, 0f);
            rig.hips.localPosition = rig.RestPosition(rig.hips) + offset;
            rig.Rotate(rig.hips, new Vector3(0f, 4f * swing * moveWeight, (-2f * sway - 1.6f * turnWeight * bodyLife) * moveWeight));
        }
        float lean = (1.4f + 3.1f * jogWeight + 2.4f * carry) * moveWeight + 1.5f * carry;
        rig.Rotate(rig.spine, new Vector3(lean + breath * idle * .55f, -1.5f * swing * moveWeight, .6f * breath * idle + 1.2f * sway * moveWeight));
        rig.Rotate(rig.chest, new Vector3(0f, -4.4f * swing * moveWeight, .9f * turnWeight * moveWeight * bodyLife));
        float gaze = animate ? Mathf.Sin(now * .93f) * 1.8f * idle - turnWeight * 3f * moveWeight : breath * idle;
        rig.Rotate(rig.head, new Vector3(-lean * (animate ? .70f : .60f), gaze, -sway * moveWeight * .65f));
        rig.Rotate(rig.upperArmL, new Vector3(Mathf.Cos(strideAngle - .16f) * gait.arm * moveWeight * (1f - carry * .25f), 0f, 0f));
        rig.Rotate(rig.elbowL, new Vector3(-(5f + (1f - Mathf.Cos(strideAngle - .55f)) * (5f + 3f * jogWeight)) * moveWeight, 0f, 0f));
        rig.Rotate(rig.upperArmR, new Vector3(-swing * (5f + 2f * jogWeight) * moveWeight, 0f, 0f));
        rig.Rotate(rig.elbowR, new Vector3(-5f - (2f + 4f * jogWeight) * moveWeight, 0f, 0f));
        rig.Rotate(rig.backpack, new Vector3(.9f * swing * moveWeight, 0f, -1.5f * sway * moveWeight));

        BraceForChop();
        ApplyLifeBody();
        PlaceFoot(leftFoot, cycle, scale, release, probeGround, dt);
        PlaceFoot(rightFoot, cycle + .5f, scale, release, probeGround, dt);
    }

    void PlaceFoot(Foot foot, float phase, float scale, float turn, bool probeGround, float dt)
    {
        if (foot == null) return;
        var step = LeoMotionProfile.Foot(gait, phase, foot.rest);
        Vector3 local = Vector3.Lerp(foot.rest, step.foot, moveWeight);
        float lift = (local.y - foot.rest.y) * scale;
        float brace = Mathf.Max(chopWeight, workWeight * .66f) * (1f - moveWeight);
        local.x += Mathf.Sign(local.x) * .025f * brace;
        local.z += (foot == leftFoot ? .053f : -.045f) * brace;
        local.y = foot.rest.y + foot.soleY;
        Vector3 support = transform.TransformPoint(local);

        if (probeGround && step.stance && moveWeight > .08f)
        {
            if (!foot.planted) foot.anchor = support;
            foot.planted = true;
            // Keep each contact in world space through the middle of stance.
            // Release it near toe-off or a fast turn instead of pulling the leg sideways.
            Vector3 drift = support - foot.anchor;
            drift.y = 0f;
            float limit = .17f * scale;
            foot.anchor += Vector3.ClampMagnitude(drift, Mathf.Max(0f, drift.magnitude - limit));
            float exit = 1f - LeoMotionProfile.Smooth((step.phase / gait.stance - .76f) / .24f);
            float hold = LeoMotionProfile.Smooth((moveWeight - .15f) / .67f) * exit * (1f - .88f * turn);
            support -= Vector3.ProjectOnPlane(support - foot.anchor, Vector3.up) * hold;
        }
        else
        {
            foot.planted = false;
        }

        Vector3 up = Vector3.up;
        Quaternion flat = transform.rotation * foot.restRotation;
        if (probeGround && SampleGround(support, scale, out RaycastHit ground))
        {
            Vector3 normal = Vector3.Slerp(Vector3.up, ground.normal, .72f);
            float smoothGround = foot.groundSampled ? 1f - Mathf.Exp(-23f * dt) : 1f;
            // Keep small steps in the terrain from snapping the ankle, while
            // bringing the sole up immediately onto higher ground.
            foot.groundY = foot.groundSampled
                ? Mathf.Max(ground.point.y, Mathf.Lerp(foot.groundY, ground.point.y, smoothGround))
                : ground.point.y;
            foot.groundUp = foot.groundSampled ? Vector3.Slerp(foot.groundUp, normal, smoothGround) : normal;
            foot.groundSampled = true;
            support.y = foot.groundY;
            up = foot.groundUp;
            flat = Quaternion.FromToRotation(Vector3.up, up) * flat;
        }
        else
        {
            foot.groundSampled = false;
        }

        float pitch = LeoMotionProfile.FootPitch(gait, phase, jogWeight) * moveWeight * .58f;
        Quaternion roll = Quaternion.Euler(pitch, 0f, 0f);
        Vector3 contact = foot.sole[0];
        Vector3 turned = roll * contact;
        for (int i = 1; i < foot.sole.Length; i++)
        {
            Vector3 sample = roll * foot.sole[i];
            if (sample.y >= turned.y) continue;
            contact = foot.sole[i];
            turned = sample;
        }
        Vector3 ankleOffset = new Vector3(0f, -turned.y, contact.z - turned.z);
        Vector3 ankleGoal = support + flat * ankleOffset * scale + up * lift + Vector3.up * (hopLift * scale);
        Vector3 pole = transform.TransformPoint(new Vector3(foot.rest.x * 1.14f, .56f, .64f));
        Quaternion pitchRotation = flat * roll;
        LeoRig.SolveTwoBone(foot.hip, foot.knee, foot.ankle, ankleGoal, pole, pitchRotation, 1f);

        if (step.swing > 0f && lift > .001f)
            FollowShinInSwing(foot, pitchRotation, support, up, step.swing, lift, scale);
    }

    void FollowShinInSwing(Foot foot, Quaternion pitchRotation, Vector3 support, Vector3 up, float swing, float lift, float scale)
    {
        float blend = Mathf.Sin(swing * Mathf.PI) * Mathf.Lerp(.22f, .38f, jogWeight);
        blend *= Mathf.Clamp01(lift / (.045f * scale));
        Quaternion shinRotation = foot.knee.rotation * foot.ankleBindRotation;
        Quaternion candidate = Quaternion.Slerp(pitchRotation, shinRotation, blend);
        float ankleHeight = Vector3.Dot(foot.ankle.position - support, up);
        if (SoleHeight(foot, candidate, up, scale) + ankleHeight < 0f)
        {
            // Reduce the tuck near the ground, keeping the existing ankle target.
            float low = 0f, high = blend;
            for (int i = 0; i < 4; i++)
            {
                float mid = (low + high) * .5f;
                Quaternion test = Quaternion.Slerp(pitchRotation, shinRotation, mid);
                if (SoleHeight(foot, test, up, scale) + ankleHeight >= 0f)
                    low = mid;
                else
                    high = mid;
            }
            candidate = Quaternion.Slerp(pitchRotation, shinRotation, low);
        }
        foot.ankle.rotation = candidate;
    }

    static float SoleHeight(Foot foot, Quaternion rotation, Vector3 up, float scale)
    {
        float lowest = float.PositiveInfinity;
        foreach (Vector3 point in foot.sole)
            lowest = Mathf.Min(lowest, Vector3.Dot(rotation * point, up) * scale);
        return lowest;
    }

    bool SampleGround(Vector3 support, float scale, out RaycastHit best)
    {
        best = default;
        int count = Physics.RaycastNonAlloc(support + Vector3.up * (.34f * scale), Vector3.down,
            footHits, .70f * scale, ~0, QueryTriggerInteraction.Ignore);
        float nearest = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = footHits[i];
            if (hit.collider == null || hit.normal.y < .65f || hit.collider.attachedRigidbody != null ||
                hit.collider.transform.IsChildOf(player.transform))
                continue;
            float difference = Mathf.Abs(hit.point.y - support.y);
            if (difference > .18f * scale || difference >= nearest) continue;
            nearest = difference;
            best = hit;
        }
        return best.collider != null;
    }
}
