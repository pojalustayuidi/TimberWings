using UnityEngine;

public partial class PlayerInteraction
{
    public const int LeoReadbackVersion = 1;
    public bool AnimationHasScheduledImpact =>
        IsChopping && (RhythmChop ? rhythmActive && pendingHit : !useAnimationEvents);

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

    LeoModelPresentation leoPresentation;
    LeoAnimationController leoAnimation;
    ResourceItem leoWalkArrival, leoApproachAttempt;
    bool leoEnterRhythmOnArrival;
    int leoImpactFrame = -1;
    bool leoHasImpact;
    Vector3 leoImpact;

    Transform LeoVisual
    {
        get
        {
            if (leoPresentation == null) leoPresentation = GetComponentInChildren<LeoModelPresentation>();
            if (leoPresentation != null && leoPresentation.isActiveAndEnabled) return leoPresentation.transform;
            if (leoAnimation == null) leoAnimation = GetComponentInChildren<LeoAnimationController>();
            return leoAnimation != null && leoAnimation.isActiveAndEnabled ? leoAnimation.transform : null;
        }
    }

    float LeoStopDistance(ResourceItem item)
    {
        var capsule = GetComponent<CapsuleCollider>();
        float playerRadius = capsule != null
            ? capsule.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z))
            : .25f;
        return item.TrunkRadius + playerRadius + .028f;
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
        float stop = LeoStopDistance(TargetItem) + .041f;
        if (toTree.sqrMagnitude <= stop * stop) return false;

        // A held action can wait for the approach; explicit movement still wins.
        if (leoApproachAttempt != TargetItem && Mathf.Abs(MobileInput.Horizontal) < .01f &&
            Mathf.Abs(MobileInput.Vertical) < .01f)
        {
            leoApproachAttempt = TargetItem;
            StartWalk(TargetItem, Mathf.Max(radius + TargetItem.TrunkRadius, Mathf.Max(walkRangeClick, walkRangeE)), RhythmChop);
        }
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
        leoHasImpact = LeoChopContact.TryPoint(item, LeoVisual, out leoImpact);
    }

    void RaiseChopContact(TreeHits tree, float strength)
    {
        ReadLeoImpact(TargetItem);
        if (tree != null) tree.Shake(strength, leoHasImpact ? leoImpact : (Vector3?)null);
        GameEvents.RaiseChopHit();
    }
}
