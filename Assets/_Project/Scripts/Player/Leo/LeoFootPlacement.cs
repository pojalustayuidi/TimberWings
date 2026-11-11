using System;
using System.Collections.Generic;
using UnityEngine;

// The same sole geometry and leg solve drive the live model and the baked previews.
sealed class LeoFootPlacement
{
    sealed class Foot
    {
        public Transform hip, knee, ankle;
        public Vector3 rest;
        public Quaternion restRotation;
        public Vector3[] sole;
        public float soleY, legLength;
        public Vector3 anchor, groundUp;
        public float groundY;
        public bool planted, sampled;
    }

    readonly LeoRig rig;
    readonly Transform model, player;
    readonly Foot left, right;
    readonly RaycastHit[] hits = new RaycastHit[16];

    public LeoFootPlacement(LeoRig rig, Transform player)
    {
        this.rig = rig;
        model = rig.transform;
        this.player = player != null ? player : model;
        left = Cache(rig.hipL, rig.kneeL, rig.ankleL);
        right = Cache(rig.hipR, rig.kneeR, rig.ankleR);
    }

    Foot Cache(Transform hip, Transform knee, Transform ankle)
    {
        if (hip == null || knee == null || ankle == null) return null;
        var sole = new List<Vector3>(4);
        float bottom = float.PositiveInfinity;
        foreach (var filter in ankle.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null || !filter.name.EndsWith("__Sole", StringComparison.Ordinal)) continue;
            Bounds bounds = filter.sharedMesh.bounds;
            for (int i = 0; i < 2; i++)
            {
                Vector3 edge = new Vector3(bounds.center.x, bounds.min.y, i == 0 ? bounds.min.z : bounds.max.z);
                Vector3 point = ankle.InverseTransformPoint(filter.transform.TransformPoint(edge));
                sole.Add(point);
                bottom = Mathf.Min(bottom, point.y);
            }
        }
        if (sole.Count == 0)
        {
            bottom = -.1565f;
            sole.Add(new Vector3(0f, bottom, -.125f));
            sole.Add(new Vector3(0f, bottom, .247f));
        }

        Vector3 thigh = model.InverseTransformVector(knee.position - hip.position);
        Vector3 shin = model.InverseTransformVector(ankle.position - knee.position);
        return new Foot
        {
            hip = hip, knee = knee, ankle = ankle, sole = sole.ToArray(), soleY = bottom,
            rest = model.InverseTransformPoint(ankle.position),
            restRotation = Quaternion.Inverse(model.rotation) * ankle.rotation,
            legLength = thigh.magnitude + shin.magnitude
        };
    }

    public void Reset()
    {
        if (left != null) left.planted = left.sampled = false;
        if (right != null) right.planted = right.sampled = false;
    }

    struct Target
    {
        public Vector3 ankle;
        public Quaternion rotation;
        public float pelvisDrop;
    }

    public void Apply(LeoMotionProfile.Gait gait, float phase, float move, float run, float turn, float brace,
        bool plant, bool adapt, LayerMask groundMask, float dt, LeoCelebration.Pose cheer)
    {
        float scale = Mathf.Max(.01f, model.lossyScale.y);
        Target a = ReadTarget(left, gait, phase, move, run, turn, brace, plant, adapt, groundMask, scale, dt, cheer, true);
        Target b = ReadTarget(right, gait, phase + .5f, move, run, turn, brace, plant, adapt, groundMask, scale, dt, cheer, false);

        // Absorb a little extra reach at heel strike and on uneven ground, where
        // a fully straight leg would otherwise drag the planted boot inward.
        if (rig.hips != null)
        {
            float drop = Mathf.Min(.085f * scale, Mathf.Max(a.pelvisDrop, b.pelvisDrop));
            rig.hips.position -= Vector3.up * drop;
        }
        Solve(left, a);
        Solve(right, b);
    }

    Target ReadTarget(Foot foot, LeoMotionProfile.Gait gait, float phase, float move, float run, float turn,
        float brace, bool plant, bool adapt, LayerMask mask, float scale, float dt, LeoCelebration.Pose cheer, bool isLeft)
    {
        if (foot == null) return default;
        var step = LeoMotionProfile.Foot(gait, phase, foot.rest);
        Vector3 local = Vector3.Lerp(foot.rest, step.foot, move);
        float lift = (local.y - foot.rest.y) * scale;
        float passing = Mathf.Sin(step.swing * Mathf.PI) * move;
        // Give each boot a clear track as it passes the other leg.
        float track = Mathf.Lerp(.031f, .044f, run) * move;
        local.x += Mathf.Sign(foot.rest.x) * (.025f * brace + track + .013f * passing);
        local.z += (isLeft ? .053f : -.045f) * brace;
        local.y = foot.rest.y + foot.soleY;
        Vector3 support = model.TransformPoint(local);

        if (plant && step.stance && move > .08f)
        {
            if (!foot.planted) foot.anchor = support;
            foot.planted = true;
            Vector3 drift = Vector3.ProjectOnPlane(support - foot.anchor, Vector3.up);
            float reach = .18f * scale;
            foot.anchor += Vector3.ClampMagnitude(drift, Mathf.Max(0f, drift.magnitude - reach));
            float stance = step.phase / gait.stance;
            float contact = LeoMotionProfile.Smooth(stance / .10f) *
                (1f - LeoMotionProfile.Smooth((stance - .77f) / .23f));
            support -= Vector3.ProjectOnPlane(support - foot.anchor, Vector3.up) * contact *
                LeoMotionProfile.Smooth((move - .08f) / .66f) * (1f - .91f * Mathf.Abs(turn));
        }
        else foot.planted = false;

        Vector3 up = Vector3.up;
        Quaternion flat = model.rotation * foot.restRotation;
        if (adapt && TryGround(support, scale, mask, out RaycastHit ground))
        {
            float amount = foot.sampled ? 1f - Mathf.Exp(-24f * dt) : 1f;
            Vector3 normal = Vector3.Slerp(Vector3.up, ground.normal, .85f);
            foot.groundUp = Vector3.Slerp(foot.sampled ? foot.groundUp : Vector3.up, normal, amount);
            foot.groundY = foot.sampled ? Mathf.Max(ground.point.y, Mathf.Lerp(foot.groundY, ground.point.y, amount)) : ground.point.y;
            foot.sampled = true;
            support.y = foot.groundY;
            up = foot.groundUp;
            flat = Quaternion.FromToRotation(Vector3.up, up) * flat;
        }
        else foot.sampled = false;

        Vector3 hop = cheer.FootOffset(isLeft);
        float pitch = LeoMotionProfile.FootPitch(gait, phase, run) * move + cheer.FootPitch(isLeft);
        Quaternion roll = Quaternion.Euler(pitch, 0f, 0f);
        Vector3 contactPoint = foot.sole[0];
        Vector3 rotated = roll * contactPoint;
        for (int i = 1; i < foot.sole.Length; i++)
        {
            Vector3 point = roll * foot.sole[i];
            if (point.y >= rotated.y) continue;
            contactPoint = foot.sole[i];
            rotated = point;
        }

        Vector3 offset = new Vector3(0f, -rotated.y, contactPoint.z - rotated.z);
        Vector3 goal = support + flat * offset * scale + up * lift + model.TransformVector(hop);
        float drop = 0f;
        if (move > 0f)
        {
            Vector3 reach = foot.hip.position - goal;
            float length = foot.legLength * scale * .995f;
            float vertical = Mathf.Sqrt(Mathf.Max(0f, length * length - reach.x * reach.x - reach.z * reach.z));
            float supportWeight = step.stance ? 1f :
                1f - LeoMotionProfile.Smooth(Mathf.Min(step.swing, 1f - step.swing) / .19f);
            drop = Mathf.Max(0f, reach.y - vertical) * supportWeight * LeoMotionProfile.Smooth(move / .36f);
        }
        return new Target { ankle = goal, rotation = flat * roll, pelvisDrop = drop };
    }

    void Solve(Foot foot, Target target)
    {
        if (foot == null) return;
        Vector3 pole = model.TransformPoint(new Vector3(foot.rest.x * 1.28f, .56f, .64f));
        LeoRig.SolveTwoBone(foot.hip, foot.knee, foot.ankle, target.ankle, pole, target.rotation, 1f);
    }

    bool TryGround(Vector3 support, float scale, LayerMask mask, out RaycastHit best)
    {
        best = default;
        int count = Physics.RaycastNonAlloc(support + Vector3.up * (.31f * scale), Vector3.down,
            hits, .63f * scale, mask, QueryTriggerInteraction.Ignore);
        float nearest = .25f * scale;
        for (int i = 0; i < count; i++)
        {
            var hit = hits[i];
            if (hit.collider == null || hit.normal.y < .65f || hit.collider.transform.IsChildOf(player) ||
                hit.rigidbody != null && !hit.rigidbody.isKinematic) continue;
            float difference = Mathf.Abs(hit.point.y - support.y);
            if (difference >= nearest) continue;
            nearest = difference;
            best = hit;
        }
        return best.collider != null;
    }
}
