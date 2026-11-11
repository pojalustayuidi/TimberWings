using UnityEngine;

// Applies the same shot over the normal pose in either gameplay camera mode.
sealed class LeoCelebrationCamera
{
    const float ShotFov = 41f;
    readonly RaycastHit[] hits = new RaycastHit[16];
    LeoCelebration source;
    int sequence;
    bool skipped, orbitMode;
    float weight, startYaw, angle, sweep, sweepDirection, radius;
    Vector3 center;

    public bool IsShowing => weight > .001f;

    public void Reset()
    {
        source = null;
        weight = 0f;
    }

    public bool Apply(Camera cam, Transform target, Vector3 focus, float dt, bool userLook, bool isOrbit, LayerMask mask)
    {
        LeoCelebration celebration = LeoCelebration.ForTarget(target);
        if (VibeSettings.ReduceEffects || SaveSystem.IsLoading ||
            (source != null && celebration != source))
        {
            Reset();
            return false;
        }
        bool playing = celebration != null && celebration.WantsShot;
        if (playing && (source != celebration || sequence != celebration.Sequence))
        {
            source = celebration;
            sequence = celebration.Sequence;
            orbitMode = isOrbit;
            skipped = userLook || !ChooseArc(cam, target, mask);
        }

        if (source == null) return false;
        if (userLook || orbitMode != isOrbit || CameraVibe.RevealActive || DialoguePreferences.ConversationOwnsCamera)
            skipped = true;

        if (playing && !skipped)
        {
            float time = source.Age;
            weight = LeoMotionProfile.Smooth(time / .77f) *
                (1f - LeoMotionProfile.Smooth((time - 1.32f) / (LeoCelebration.Duration - 1.32f)));
            center = source.Model.TransformPoint(Vector3.up * (1.02f + .32f * source.Frame.lift));
            sweep = LeoMotionProfile.Smooth((time - .38f) / 1.02f) * sweepDirection * 20f;
        }
        else weight = Mathf.MoveTowards(weight, 0f, dt / .22f);

        if (!IsShowing) return false;
        Vector3 view = Vector3.Lerp(focus, center, weight);
        Vector3 regularAngles = cam.transform.eulerAngles;
        float regularDistance = Vector3.Distance(focus, cam.transform.position);
        float yaw = regularAngles.y + (angle + sweep - Mathf.DeltaAngle(startYaw, regularAngles.y)) * weight;
        Quaternion rotation = Quaternion.Euler(Mathf.LerpAngle(regularAngles.x, 13f, weight), yaw, 0f);
        Vector3 back = rotation * Vector3.back;
        float distance = Mathf.Lerp(regularDistance, radius, weight);
        distance = Clearance(view, back, distance, cam, target, mask);
        if (distance < cam.nearClipPlane + .28f)
        {
            skipped = true;
            weight = 0f;
            return false;
        }

        if (cam.orthographic)
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, source.Model.lossyScale.y * 1.52f, weight);
        else cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, ShotFov, weight);
        cam.transform.SetPositionAndRotation(view + back * distance, rotation);
        return true;
    }

    bool ChooseArc(Camera cam, Transform target, LayerMask mask)
    {
        Transform model = source.Model;
        startYaw = cam.transform.eulerAngles.y;
        center = model.TransformPoint(Vector3.up * 1.02f);
        radius = Mathf.Max(1.45f, model.lossyScale.y * 4.05f);
        float facing = model.eulerAngles.y;
        float left = Mathf.DeltaAngle(startYaw, facing - 133f);
        float right = Mathf.DeltaAngle(startYaw, facing + 133f);
        float clearLeft = ArcClearance(cam, target, mask, left);
        float clearRight = ArcClearance(cam, target, mask, right);
        float scoreLeft = clearLeft - .005f * Mathf.Abs(left);
        float scoreRight = clearRight - .005f * Mathf.Abs(right);
        bool goLeft = scoreLeft > scoreRight;
        angle = goLeft ? left : right;
        sweepDirection = goLeft ? -1f : 1f;
        return (goLeft ? clearLeft : clearRight) >= radius * .57f;
    }

    float ArcClearance(Camera cam, Transform target, LayerMask mask, float delta)
    {
        float clear = radius;
        for (int i = 1; i <= 3; i++)
        {
            float yaw = startYaw + delta * (i / 3f);
            Vector3 back = Quaternion.Euler(13f, yaw, 0f) * Vector3.back;
            clear = Mathf.Min(clear, Clearance(center, back, radius, cam, target, mask));
        }
        return clear;
    }

    float Clearance(Vector3 focus, Vector3 direction, float distance, Camera cam, Transform target, LayerMask mask)
    {
        float radius = Mathf.Max(.18f, cam.nearClipPlane);
        int count = Physics.SphereCastNonAlloc(focus, radius, direction, hits, distance,
            mask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            var hit = hits[i];
            if (hit.collider == null || hit.distance <= .001f || hit.collider.transform.IsChildOf(target) ||
                hit.rigidbody != null && !hit.rigidbody.isKinematic) continue;
            distance = Mathf.Min(distance, hit.distance - .08f);
        }
        return distance;
    }
}
