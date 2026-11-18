using UnityEngine;

public sealed partial class ChopPrototypeLog
{
    const float SectionGap = .125f;
    const float EndClearance = .16f;
    const float ActorClearance = .02f;
    static readonly float[] FallAngles = { 0f, 30f, -30f, 60f, -60f, 90f, -90f, 120f, -120f, 150f, -150f, 180f };
    static readonly float[] LengthScales = { 1f, .80f, .66f };
    static readonly RaycastHit[] GroundHits = new RaycastHit[24];
    static readonly Collider[] ClearanceHits = new Collider[32];

    internal static bool TryPlan(ResourceItem source, Transform actor, out ChopPrototypeSave.Layout layout)
    {
        layout = default;
        if (source == null || actor == null || string.IsNullOrEmpty(source.ChopSaveId) ||
            !source.gameObject.activeInHierarchy) return false;

        Vector3 root = source.transform.position;
        Vector3 away = Vector3.ProjectOnPlane(root - actor.position, Vector3.up).normalized;
        if (away.sqrMagnitude < .01f) away = actor.forward;
        away = Vector3.ProjectOnPlane(away, Vector3.up).normalized;
        float size = Mathf.Clamp(Mathf.Abs(source.transform.lossyScale.y), .72f, 1.35f);
        float fullLength = (source.Size == TreeSize.Old ? 3.0f : source.Size == TreeSize.Mature ? 2.06f : 1.35f) * size;
        float radius = Mathf.Clamp(source.TrunkRadius * .78f, .135f, .38f);
        var crown = source.GetComponent<TreeSpeciesInfo>();
        float height = (crown != null ? crown.pickHeight : 4.3f) * Mathf.Abs(source.transform.lossyScale.y);
        foreach (var renderer in source.GetComponentsInChildren<MeshRenderer>())
        {
            if (!renderer.enabled || renderer.name == "Outline" || renderer.name == "Hull" ||
                renderer.name == "HighlightRing") continue;
            height = Mathf.Max(height, renderer.bounds.max.y - root.y);
        }
        float crownRadius = (crown != null ? crown.pickRadius : .78f) * Mathf.Abs(source.transform.lossyScale.x);
        height = Mathf.Clamp(height, fullLength + .12f, 8f);
        crownRadius = Mathf.Clamp(crownRadius * .75f, radius, 1.4f);
        if (!GroundAt(root, root.y, source, actor, out var baseHit)) return false;

        // Prefer a full tree starting away from Leo. Recheck shorter trunk spans in tight groves.
        foreach (float scale in LengthScales)
        {
            foreach (float angle in FallAngles)
            {
                Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * away;
                float length = fullLength * scale;
                Vector3 start = root + dir * (source.TrunkRadius + .24f);
                Vector3 end = start + dir * length;
                if (!GroundAt(start, baseHit.point.y, source, actor, out var foot) ||
                    !GroundAt(end, baseHit.point.y, source, actor, out var tip)) continue;

                Vector3 along = (tip.point - foot.point).normalized;
                if (Mathf.Abs(along.y) > .36f || along.sqrMagnitude < .01f) continue;
                var rotation = Quaternion.LookRotation(along, Vector3.up);
                Vector3 up = rotation * Vector3.up;
                if (!TrySupport(foot.point, tip.point, rotation, radius, source, actor, out float lift)) continue;

                Vector3 center = (foot.point + tip.point) * .5f + up * radius + Vector3.up * (lift + .012f);
                float realLength = Vector3.Distance(foot.point, tip.point);
                var candidate = new ChopPrototypeSave.Layout
                {
                    center = center, rotation = rotation, length = realLength, radius = radius,
                    fallPivot = center - along * (realLength * .5f + .12f),
                    fallHeight = height, crownRadius = crownRadius
                };
                if (!ClearLanding(candidate, source) ||
                    !ClearFall(candidate, root, source, actor)) continue;

                layout = candidate;
                return true;
            }
        }
        return false;
    }

    static bool TrySupport(Vector3 start, Vector3 end, Quaternion rotation, float radius,
        ResourceItem source, Transform actor, out float lift)
    {
        lift = 0f;
        Vector3 along = rotation * Vector3.forward;
        Vector3 across = rotation * Vector3.right;
        start -= along * EndClearance;
        end += along * EndClearance;
        for (int i = 0; i <= 8; i++)
        {
            Vector3 sample = Vector3.Lerp(start, end, i / 8f);
            for (int side = -1; side <= 1; side++)
            {
                Vector3 point = sample + across * (radius * .60f * side);
                if (!GroundAt(point, sample.y, source, actor, out var ground)) return false;
                float offset = ground.point.y - sample.y;
                if (offset < -.15f || offset > .14f) return false;
                // The curved sides clear shallow cross-slopes without lifting the entire trunk.
                lift = Mathf.Max(lift, offset - (side == 0 ? 0f : radius * .14f));
            }
        }
        return true;
    }

    static bool GroundAt(Vector3 point, float baseY, ResourceItem source, Transform actor, out RaycastHit best)
    {
        best = default;
        point.y = baseY + 1.65f;
        int count = Physics.RaycastNonAlloc(point, Vector3.down, GroundHits, 3.35f, ~0, QueryTriggerInteraction.Ignore);
        float nearest = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            var hit = GroundHits[i];
            Collider c = hit.collider;
            if (c == null || c.transform.IsChildOf(source.transform) || c.transform.IsChildOf(actor) ||
                hit.distance >= nearest) continue;
            nearest = hit.distance;
            best = hit;
        }
        if (best.collider == null || !IsGround(best.collider) || best.normal.y < .65f) return false;
        return !WaterVolume.TrySample(best.point, out var water) || water.submersion < -.035f;
    }

    internal static bool IsGround(Collider c) =>
        c is TerrainCollider || c.tag == "Ground" || c.name == "Ground" ||
        c.name.StartsWith("Ground_", System.StringComparison.Ordinal);

    static bool ClearLanding(ChopPrototypeSave.Layout placement, ResourceItem source)
    {
        Vector3 half = new Vector3(placement.radius, placement.radius * .88f, placement.length * .5f + EndClearance);
        int count = Physics.OverlapBoxNonAlloc(placement.center, half, ClearanceHits, placement.rotation,
            ~0, QueryTriggerInteraction.Ignore);
        if (count == ClearanceHits.Length) return false;
        for (int i = 0; i < count; i++)
        {
            var c = ClearanceHits[i];
            if (c != null && !c.transform.IsChildOf(source.transform) && !IsGround(c)) return false;
        }
        // The original log has no collider during the fall, but reserves the eventual landing spot.
        foreach (var other in Active)
        {
            if (other == null || other.Ready) continue;
            Vector3 along = placement.rotation * Vector3.forward;
            for (int sample = 0; sample < 5; sample++)
            {
                Vector3 p = placement.center + along * (placement.length * (sample / 4f - .5f));
                Vector3 q = other.Closest(p);
                float clearance = placement.radius + other.state.layout.radius + EndClearance;
                if ((p - q).sqrMagnitude <= clearance * clearance) return false;
            }
        }
        return true;
    }

    internal static Vector3 FallAxis(ChopPrototypeSave.Layout layout, float tilt) =>
        Vector3.Slerp(Vector3.up, layout.rotation * Vector3.forward, tilt);

    internal static Vector3 FallBase(ChopPrototypeSave.Layout layout, Vector3 origin, float tilt)
    {
        Vector3 pivot = Vector3.Lerp(origin, layout.fallPivot, tilt * tilt);
        pivot.y += Mathf.Sin(tilt * Mathf.PI) * layout.radius * .55f;
        return pivot;
    }

    internal static float CanopySettle(float tilt) =>
        Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.70f, 1f, tilt));

    static Vector3 CrownCenter(ChopPrototypeSave.Layout layout, Vector3 pivot, Vector3 up, float tilt)
    {
        float height = Mathf.Lerp(layout.fallHeight, layout.length + .12f, CanopySettle(tilt));
        return pivot + up * (.82f * height);
    }

    static float CrownClearance(ChopPrototypeSave.Layout layout, float tilt) =>
        Mathf.Lerp(layout.crownRadius, layout.radius * .56f, CanopySettle(tilt));

    static bool ClearFall(ChopPrototypeSave.Layout layout, Vector3 root, ResourceItem source, Transform actor)
    {
        var capsule = actor.GetComponent<CapsuleCollider>();
        for (int i = 1; i <= 6; i++)
        {
            float t = i / 6f;
            Vector3 up = FallAxis(layout, t);
            Vector3 pivot = FallBase(layout, root, t);
            Vector3 start = pivot + up * .22f;
            Vector3 end = pivot + up * (layout.length + .12f);
            if (capsule != null && capsule.enabled &&
                IntersectsActor(pivot, end, layout.radius + ActorClearance, capsule)) return false;
            if (!ClearSweep(start, end, layout.radius * .82f, source)) return false;

            Vector3 crown = CrownCenter(layout, pivot, up, t);
            if (!ClearSweep(crown, crown, CrownClearance(layout, t), source)) return false;
        }
        return true;
    }

    static bool ClearSweep(Vector3 from, Vector3 to, float radius, ResourceItem source)
    {
        int count = Physics.OverlapCapsuleNonAlloc(from, to, radius, ClearanceHits, ~0, QueryTriggerInteraction.Ignore);
        if (count == ClearanceHits.Length) return false;
        for (int i = 0; i < count; i++)
        {
            var c = ClearanceHits[i];
            if (c != null && !c.transform.IsChildOf(source.transform) && !IsGround(c)) return false;
        }
        return true;
    }

    static bool IntersectsActor(Vector3 start, Vector3 end, float radius, CapsuleCollider capsule)
    {
        Vector3 axis = end - start;
        for (int i = 0; i <= 6; i++)
        {
            Vector3 p = start + axis * (i / 6f);
            if ((capsule.ClosestPoint(p) - p).sqrMagnitude < radius * radius) return true;
        }
        return false;
    }

    internal bool ClearOfPlayer(Vector3 origin, float tilt)
    {
        var actor = PlayerMovement.Instance;
        if (actor == null || !actor.TryGetComponent<CapsuleCollider>(out var capsule) || !capsule.enabled)
            return true;
        var layout = state.layout;
        Vector3 up = FallAxis(layout, tilt);
        Vector3 pivot = FallBase(layout, origin, tilt);
        if (IntersectsActor(pivot, pivot + up * (layout.length + .15f), layout.radius + ActorClearance, capsule))
            return false;
        Vector3 crown = CrownCenter(layout, pivot, up, tilt);
        return !IntersectsActor(crown, crown, CrownClearance(layout, tilt), capsule);
    }
}
