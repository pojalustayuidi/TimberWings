using UnityEngine;

public partial class PlayerInteraction
{
    public const int LeoReadbackVersion = 1;
    public bool AnimationHasScheduledImpact =>
        IsChopping && !waitingForChopAxe && (RhythmChop ? rhythmActive && pendingHit : !useAnimationEvents);

    public float AnimationScheduledImpact
    {
        get
        {
            if (!AnimationHasScheduledImpact) return -1f;
            if (RhythmChop) return pendingDue;
            float fallAt = Time.time + Mathf.Max(0f, GatherTime - holdTime);
            return Mathf.Min(nextHitAt, fallAt);
        }
    }

    // View animation reads the prototype timer without changing the original rig's clock.
    internal float FirstPersonImpactDue
    {
        get
        {
            if (!IsChopping || waitingForChopAxe) return -1f;
            if (IsProcessingPrototypeLog || (!useAnimationEvents && PrototypeStanding(TargetItem)))
                return nextHitAt;
            return AnimationScheduledImpact;
        }
    }

    internal bool TryReadPrototypeContact(out Vector3 point)
    {
        point = default;
        if (processingLog == null) return false;
        point = processingLog.Closest(transform.position);
        if (OrbitCamera.FirstPersonActive && processingLog.Solid != null)
        {
            Collider surface = processingLog.Solid;
            point = surface.ClosestPoint(new Vector3(point.x, surface.bounds.max.y + .1f, point.z));
        }
        return true;
    }

    LeoModelPresentation leoPresentation;
    LeoAnimationController leoAnimation;
    ResourceItem leoWalkArrival, leoApproachAttempt;
    bool leoEnterRhythmOnArrival;
    int leoImpactFrame = -1;
    bool leoHasImpact;
    Vector3 leoImpact;

    LeoRig LeoVisualRig
    {
        get
        {
            Transform model = movement != null && movement.model != null ? movement.model : transform;
            if (leoAnimation == null || !leoAnimation.transform.IsChildOf(model))
                leoAnimation = model.GetComponentInChildren<LeoAnimationController>();
            if (leoAnimation != null && leoAnimation.isActiveAndEnabled && leoAnimation.rig != null)
                return leoAnimation.rig;

            if (leoPresentation == null || !leoPresentation.transform.IsChildOf(model))
                leoPresentation = model.GetComponentInChildren<LeoModelPresentation>();
            return leoPresentation != null && leoPresentation.isActiveAndEnabled ? leoPresentation.rig : null;
        }
    }

    Transform LeoVisual
    {
        get
        {
            LeoRig rig = LeoVisualRig;
            return rig != null ? rig.transform : null;
        }
    }

    bool LeoAxeReady
    {
        get
        {
            LeoRig rig = LeoVisualRig;
            return rig == null || rig.IsAxeInHand;
        }
    }

    const float FirstPersonTreeClearance = .18f;

    float LeoStopDistance(ResourceItem item)
    {
        var capsule = GetComponent<CapsuleCollider>();
        float playerRadius = capsule != null
            ? capsule.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z))
            : .25f;
        float clearance = OrbitCamera.FirstPersonActive ? FirstPersonTreeClearance : 0f;
        return item.TrunkRadius + playerRadius + .028f + clearance;
    }

    bool TryApproachLeoTree()
    {
        if (!PressHeld || IsChopping) leoApproachAttempt = null;
        if (IsChopping || !walkToTree || movement == null || LeoVisual == null || movement.ControlLocked)
            return false;
        if (WalkTarget != null && movement.AutoWalking) return true;
        if (!(RhythmChop ? PressDown : PressHeld)) return false;

        Vector3 toTree = TargetItem.transform.position - transform.position;
        toTree.y = 0f;
        float distance = toTree.magnitude;
        float stop = LeoStopDistance(TargetItem);
        bool tooFar = distance > stop + .041f;
        bool tooClose = OrbitCamera.FirstPersonActive && distance < stop - .045f;
        if (!tooFar && !tooClose) return false;

        if (leoApproachAttempt == TargetItem || Mathf.Abs(MobileInput.Horizontal) >= .01f ||
            Mathf.Abs(MobileInput.Vertical) >= .01f)
            return tooFar;

        var item = TargetItem;
        leoApproachAttempt = item;
        if (tooClose)
        {
            bool enterRhythm = RhythmChop;
            if (!movement.TryStepBackForChop(item.transform, stop, () => OnWalkArrived(item, enterRhythm)))
                return false;
            WalkTarget = item;
        }
        else
            StartWalk(item, Mathf.Max(radius + item.TrunkRadius, Mathf.Max(walkRangeClick, walkRangeE)), RhythmChop);
        return true;
    }

    public bool TryReadLeoImpact(out Vector3 point)
    {
        point = leoImpact;
        return leoHasImpact && leoImpactFrame == Time.frameCount;
    }

    void ReadLeoImpact(ResourceItem item)
    {
        leoImpactFrame = Time.frameCount;
        leoHasImpact = OrbitCamera.TryReadFirstPersonBark(item, out leoImpact) ||
            LeoChopContact.TryPoint(item, LeoVisual, out leoImpact);
    }

    void RaiseChopContact(TreeHits tree, float strength)
    {
        ReadLeoImpact(TargetItem);
        if (tree != null) tree.Shake(strength, leoHasImpact ? leoImpact : (Vector3?)null);
        GameEvents.RaiseChopHit();
    }
}
