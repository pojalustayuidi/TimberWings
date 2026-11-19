using UnityEngine;
using UnityEngine.SceneManagement;

public partial class PlayerInteraction
{
    static readonly float[] ChopStanceAngles = { 0f, 22f, -22f, 40f, -40f };
    readonly Collider[] chopStanceOverlaps = new Collider[24];
    Transform chop03WalkSpot;
    bool chop03Walking, chop03Arrived;

    float StandingStop(ResourceItem tree) =>
        LeoStopDistance(tree) + (OrbitCamera.FirstPersonActive ? 0f : .07f);

    bool InTreeStance(ResourceItem tree)
    {
        Vector3 fromTree = Vector3.ProjectOnPlane(transform.position - tree.transform.position, Vector3.up);
        if (fromTree.sqrMagnitude < .001f || fromTree.magnitude > StandingStop(tree) + .11f) return false;
        if (Mathf.Abs(tree.transform.position.y - LogFeetHeight) > .74f) return false;
        if (tree.TryGetStandingSide(out var side) && Vector3.Dot(fromTree.normalized, side) < .64f) return false;

        Vector3 eye = transform.position;
        eye.y = LogFeetHeight + 1.04f;
        Vector3 delta = TreePromptPoint(tree) - eye;
        float distance = delta.magnitude;
        var hit = ClosestPrototypeHit(new Ray(eye, delta / Mathf.Max(.001f, distance)), distance, out _);
        return hit == null || hit.GetComponentInParent<ResourceItem>() == tree;
    }

    void TryStartChop03Approach()
    {
        bool tree = chop03Tree != null;
        bool inStance = tree ? InTreeStance(chop03Tree) : InCutStance(chop03Log);
        bool moveBack = tree && inStance &&
            PlanarDistance(chop03Tree.transform.position, transform.position) < StandingStop(chop03Tree) - .075f;
        if (inStance && !moveBack) return;

        if (walkToTree && movement != null)
        {
            if (TryFindChopStance(out var foot))
            {
                if (chop03WalkSpot == null)
                {
                    var go = new GameObject("Chop approach point");
                    SceneManager.MoveGameObjectToScene(go, gameObject.scene);
                    chop03WalkSpot = go.transform;
                }
                chop03WalkSpot.position = foot;
                chop03Arrived = false;
                chop03Walking = true;
                WalkTarget = chop03Tree;
                movement.WalkToChopSpot(chop03WalkSpot, ArriveAtChop03Spot);
                return;
            }
        }

        // A blocked step backwards still leaves the axe usable in the current close stance.
        if (inStance) return;
        CancelChop03Request();
        SaveIndicator.ShowToast(ChopPrototypeLog.Text(
            "Нет свободного подхода · попробуй ближе", "No clear approach · move closer"));
    }

    bool TryFindChopStance(out Vector3 foot)
    {
        foot = default;
        if (chop03Tree != null)
        {
            Vector3 center = chop03Tree.transform.position;
            Vector3 side = Vector3.ProjectOnPlane(transform.position - center, Vector3.up).normalized;
            if (chop03Tree.TryGetStandingSide(out var notchSide)) side = notchSide;
            if (side.sqrMagnitude < .01f) side = Vector3.forward;
            return TryStanceArc(center, side, StandingStop(chop03Tree), out foot);
        }

        var log = chop03Log.log;
        Vector3 cut = chop03Log.Point(transform.position);
        Vector3 logSide = Vector3.ProjectOnPlane(log.transform.right, Vector3.up).normalized;
        if (Vector3.Dot(transform.position - cut, logSide) < 0f) logSide = -logSide;
        float reach = Mathf.Clamp(log.Layout.radius * .85f + ActorRadius + .21f, .53f, .99f);
        return TryStanceArc(cut, logSide, reach, out foot) ||
            TryStanceArc(cut, -logSide, reach, out foot);
    }

    bool TryStanceArc(Vector3 center, Vector3 side, float offset, out Vector3 foot)
    {
        foreach (float angle in ChopStanceAngles)
        {
            Vector3 candidate = center + Quaternion.AngleAxis(angle, Vector3.up) * side * offset;
            if (!TryChopGround(candidate, center.y, out foot) || !ClearChopPath(foot)) continue;
            if (chop03Tree == null && (center.y - foot.y < -.02f || center.y - foot.y > 1.02f)) continue;
            return true;
        }
        foot = default;
        return false;
    }

    bool TryChopGround(Vector3 point, float expectedY, out Vector3 foot)
    {
        point.y = expectedY + .65f;
        var hit = ClosestPrototypeHit(new Ray(point, Vector3.down), 1.50f, out foot, out var normal);
        return hit != null && ChopPrototypeLog.IsGround(hit) && normal.y >= .64f &&
            (!WaterVolume.TrySample(foot, out var sample) || sample.submersion < -.035f);
    }

    bool ClearChopPath(Vector3 foot)
    {
        Vector3 previous = transform.position;
        previous.y = LogFeetHeight;
        Vector3 start = previous;
        int steps = Mathf.Clamp(Mathf.CeilToInt(PlanarDistance(start, foot) / .60f), 1, 32);
        float radius = Mathf.Max(.12f, ActorRadius - .018f);
        float height = logActorCapsule != null ? logActorCapsule.bounds.size.y : 1.45f;
        Vector3 low = Vector3.up * (radius + .055f);
        Vector3 high = Vector3.up * Mathf.Max(radius + .055f, height - radius - .025f);

        int occupied = Physics.OverlapCapsuleNonAlloc(foot + low, foot + high, radius, chopStanceOverlaps,
            ~0, QueryTriggerInteraction.Ignore);
        if (occupied >= chopStanceOverlaps.Length) return false;
        for (int i = 0; i < occupied; i++)
        {
            Collider c = chopStanceOverlaps[i];
            if (c != null && !c.transform.IsChildOf(transform) && !ChopPrototypeLog.IsGround(c)) return false;
        }

        for (int i = 1; i <= steps; i++)
        {
            if (!TryChopGround(Vector3.Lerp(start, foot, i / (float)steps), previous.y, out var next) ||
                Mathf.Abs(next.y - previous.y) > .34f) return false;
            Vector3 delta = next - previous;
            float distance = delta.magnitude;
            if (distance > .001f)
            {
                int count = Physics.CapsuleCastNonAlloc(previous + low, previous + high, radius, delta / distance,
                    prototypeHits, distance, ~0, QueryTriggerInteraction.Ignore);
                if (count >= prototypeHits.Length) return false;
                for (int j = 0; j < count; j++)
                {
                    var h = prototypeHits[j];
                    if (h.collider == null || h.collider.transform.IsChildOf(transform)) continue;
                    if (ChopPrototypeLog.IsGround(h.collider) && h.normal.y >= .64f) continue;
                    return false;
                }
            }
            previous = next;
        }
        return true;
    }

    void ArriveAtChop03Spot() => chop03Arrived = true;

    bool TickChop03Approach()
    {
        if (!chop03Walking) return false;
        if (chop03Tree != null ? !chop03Tree.isActiveAndEnabled : !chop03Log.Valid)
        {
            CancelChop03Approach();
            CancelChop03Request();
            return false;
        }
        if (movement != null && movement.IsWalkingTo(chop03WalkSpot)) return true;
        if (!chop03Arrived) CancelChop03Request();
        chop03Walking = chop03Arrived = false;
        WalkTarget = null;
        nextPrototypePick = 0f;
        return false;
    }

    void CancelChop03Approach()
    {
        if (!chop03Walking) return;
        movement?.CancelWalkTo(chop03WalkSpot);
        chop03Walking = chop03Arrived = false;
        WalkTarget = null;
    }
}
