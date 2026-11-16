using UnityEngine;

public sealed partial class StoryInspection
{
    public const string GunnarKnifeModelName = "Gunnar_Inspection_Knife";
    public const string GunnarEngravingFocusName = "Engraving_Focus";

    const float KnifeLiftTime = .90f;
    const float KnifeReplaceTime = .68f;
    const float KnifeContact = .018f;
    const float KnifeSeatTime = .17f;
    float knifeReplaceFromLift, knifeReplaceFromTurn;

    internal bool UsesAuthoredRibbonCamera => AuthoredRibbon;

    static Vector3 Flat(Vector3 vector) => new Vector3(vector.x, 0f, vector.z);

    Vector3 approachCorner;
    bool hasApproachCorner;

    Vector3 ChooseStance()
    {
        Vector3 root = player.transform.position;
        Vector3 item = Focus;
        Vector3 outward = Flat(AuthoredKnife ? transform.forward : detail.forward).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, -outward);
        float scale = Mathf.Abs(actorRig.transform.lossyScale.y);
        float stop = AuthoredKnife ? Mathf.Clamp(.44f * scale, .21f, .35f) : .69f;
        float lateral = AuthoredKnife ? -.15f * scale : .13f;
        Vector3 stance = item + outward * stop + right * lateral;
        stance.y = root.y;
        approachFacing = AuthoredKnife ? -outward : Flat(item - stance).normalized;

        // Route around the low prop when entering from its back. The waypoint
        // is outside the artwork footprint and the actual walk still uses physics.
        Vector3 itemToPlayer = Flat(root - item);
        float side = Vector3.Dot(itemToPlayer, right) >= 0f ? 1f : -1f;
        hasApproachCorner = Vector3.Dot(itemToPlayer, outward) < -.12f;
        approachCorner = item + right * (side * .68f) + outward * .16f;
        approachCorner.y = root.y;
        return stance;
    }

    bool StepToStance(Vector3 stance)
    {
        Vector3 delta = Flat((hasApproachCorner ? approachCorner : stance) - player.transform.position);
        if (hasApproachCorner && delta.sqrMagnitude < .10f * .10f)
        {
            hasApproachCorner = false;
            delta = Flat(stance - player.transform.position);
        }
        float distance = delta.magnitude;
        if (distance <= .014f) return false;
        Vector3 direction = delta / distance;
        if (!GroundAhead(direction, Mathf.Min(.19f, distance)) ||
            !ClearForPlayer(direction, Mathf.Min(.23f, distance))) return false;

        // Avoid overshooting the pickup mark at low frame rates.
        float speed = Mathf.Min(1.45f, distance / Mathf.Max(Time.deltaTime, Time.fixedDeltaTime));
        walkDirection = direction * speed;
        return true;
    }

    bool ClearForPlayer(Vector3 direction, float step)
    {
        if (playerCapsule == null) return true;
        Bounds bounds = playerCapsule.bounds;
        float radius = Mathf.Max(.04f, Mathf.Min(bounds.extents.x, bounds.extents.z) - .025f);
        Vector3 feet = bounds.center + Vector3.up * (radius - bounds.extents.y + .06f);
        Vector3 crown = bounds.center + Vector3.up * Mathf.Max(0f, bounds.extents.y - radius);
        int count = Physics.CapsuleCastNonAlloc(feet, crown, radius, direction, castHits, step, ~0,
            QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; ++i)
        {
            var hit = castHits[i];
            if (hit.collider != null && !hit.collider.transform.IsChildOf(player.transform) && hit.normal.y < .55f)
                return false;
        }
        return true;
    }

    void PlaceAuthoredKnife()
    {
        if (actorRig == null || actorRig.handR == null) return;

        Transform hand = actorRig.handR;
        Vector3 grip = LeoAxePose.Grip(hand);
        Quaternion tool = LeoAxePose.ToolRotation(hand, knifePalmRoll);
        float gap = Vector3.Distance(grip, prop.RestGrip);

        if (!knifeInHand)
        {
            if (pickedKnife || SkipRequested || beat != Beat.Reach || BeatTime < .41f || gap > KnifeContact ||
                Quaternion.Angle(tool, prop.RestRotation) > 12f) return;
            knifeInHand = pickedKnife = true;
            pickedAt = Time.time;
        }

        if (beat == Beat.Replace && BeatTime >= KnifeReplaceTime)
        {
            float progress = Smooth((BeatTime - KnifeReplaceTime) / KnifeSeatTime);
            prop.Release(progress);
            if (progress >= 1f) knifeInHand = false;
            return;
        }

        float contact = Smooth((Time.time - pickedAt) / .09f);
        prop.Hold(Vector3.Lerp(prop.RestGrip, grip, contact),
            Quaternion.Slerp(prop.RestRotation, tool, contact));
    }
}
