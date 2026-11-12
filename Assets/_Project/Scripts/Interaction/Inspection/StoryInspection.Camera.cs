using UnityEngine;

public sealed partial class StoryInspection
{
    struct CameraPose
    {
        public Vector3 position;
        public Quaternion rotation;
        public float lens;
    }

    readonly RaycastHit[] castHits = new RaycastHit[24];
    CameraPose gameplayPose, inspectionPose, releasePose;
    bool orthographic, cameraOpened;

    void OpenCamera()
    {
        orthographic = camera.orthographic;
        gameplayPose = ReadCamera();
        inspectionPose = gameplayPose;
        cameraOpened = true;
    }

    CameraPose ReadCamera() => new CameraPose
    {
        position = camera.transform.position,
        rotation = camera.transform.rotation,
        lens = orthographic ? camera.orthographicSize : camera.fieldOfView
    };

    void ApplyCamera(CameraPose pose)
    {
        camera.transform.SetPositionAndRotation(pose.position, pose.rotation);
        if (orthographic) camera.orthographicSize = pose.lens;
        else camera.fieldOfView = pose.lens;
    }

    void RestoreCamera()
    {
        if (cameraOpened && camera != null) ApplyCamera(gameplayPose);
        cameraOpened = false;
    }

    void UpdateCamera()
    {
        if (Time.deltaTime <= 0f)
        {
            ApplyCamera(inspectionPose);
            return;
        }
        gameplayPose = ReadCamera();
        if (beat == Beat.Approach)
        {
            inspectionPose = gameplayPose;
            return;
        }

        Vector3 focus = Focus;
        CameraPose target = FrameShot(ref focus);
        if (releasing)
        {
            float t = Smooth(BeatTime / (SkipRequested ? .43f : .69f));
            inspectionPose = Blend(releasePose, gameplayPose, t);
            if (t < .99f) inspectionPose.position = ClearPosition(focus, inspectionPose.position);
        }
        else
        {
            float t = 1f - Mathf.Exp(-Time.deltaTime * (beat == Beat.Look ? 6f : 7.5f));
            Vector3 from = inspectionPose.position - focus;
            Vector3 to = target.position - focus;
            inspectionPose = Blend(inspectionPose, target, t);
            Vector3 arc = focus + Vector3.Slerp(from, to, t);
            inspectionPose.position = ClearPosition(focus, arc);
        }
        ApplyCamera(inspectionPose);
    }

    static CameraPose Blend(CameraPose a, CameraPose b, float t) => new CameraPose
    {
        position = Vector3.Lerp(a.position, b.position, t),
        rotation = Quaternion.Slerp(a.rotation, b.rotation, t),
        lens = Mathf.Lerp(a.lens, b.lens, t)
    };

    CameraPose FrameShot(ref Vector3 focus)
    {
        Vector3 item = Focus;
        var rig = actorRig;
        Vector3 hero = rig != null && rig.head != null ? rig.head.position : player.transform.position + Vector3.up * .8f;
        Vector3 back = player.transform.position - item;
        back.y = 0f;
        back = back.sqrMagnitude < .01f ? -player.transform.forward : back.normalized;
        float distance = 2.5f;
        float angle = 25f;
        float elevation = .40f;
        float size = .85f;
        float fov = 38f;

        if (beat == Beat.Look)
        {
            focus = Vector3.Lerp(item, hero, .30f);
            distance = 3.6f;
            size = 1.35f;
        }
        else if (engraveBridge)
        {
            back = transform.forward;
            distance = 1.9f; angle = 11f; size = .62f;
        }
        else
        {
            switch (subject)
            {
                case Kind.Knife:
                    if (beat == Beat.Reach)
                    {
                        focus = Vector3.Lerp(item, hero, .24f);
                        distance = 2.3f; angle = 39f; size = .72f;
                    }
                    else if (beat == Beat.Detail)
                    {
                        if (knifeInHand && prop != null)
                        {
                            focus = Vector3.Lerp(prop.Grip, hero, .12f);
                            back = -back;
                        }
                        distance = 1.10f; angle = 28f; elevation = .40f; size = .30f; fov = 34f;
                    }
                    else if (beat == Beat.React)
                    {
                        focus = Vector3.Lerp(knifeInHand && prop != null ? prop.Grip : item, hero, .72f);
                        distance = 1.85f; angle = 52f; elevation = .26f; size = .62f;
                        back = -back;
                    }
                    break;
                case Kind.Ribbon:
                    focus = Vector3.Lerp(item, hero, beat == Beat.React ? .58f : .12f);
                    distance = beat == Beat.React ? 1.95f : 1.58f;
                    angle = beat == Beat.React ? -55f : -25f; size = .64f;
                    if (beat == Beat.React) back = -back;
                    break;
                case Kind.Board:
                    focus = Vector3.Lerp(item, hero, beat == Beat.React ? .60f : .08f);
                    distance = 1.9f; angle = beat == Beat.React ? 56f : 16f; size = .68f;
                    if (beat == Beat.React) back = -back;
                    break;
                case Kind.Finale:
                    back = -transform.forward;
                    focus = Vector3.Lerp(item, hero, beat == Beat.React ? .43f : .02f);
                    distance = 2.15f; angle = beat == Beat.React ? -43f : 10f; size = .72f;
                    break;
                case Kind.BenchMarks:
                case Kind.Witness:
                    if (detail != null)
                    {
                        Vector3 forward = Vector3.ProjectOnPlane(-detail.forward, Vector3.up);
                        if (forward.sqrMagnitude > .01f) back = forward.normalized;
                    }
                    distance = 1.9f; angle = 6f; elevation = .70f; size = .78f;
                    break;
                case Kind.Arch:
                    back = -transform.forward;
                    distance = 4.9f; angle = 10f; elevation = -.25f; size = 1.45f;
                    break;
                case Kind.Stump:
                    angle = -32f; distance = 2.1f; size = .73f;
                    break;
                case Kind.ThreeMarks:
                    back = -transform.forward;
                    angle = 17f; distance = 1.9f; size = .70f;
                    break;
                case Kind.CopperArrow:
                    bool lookingAway = beat == Beat.React && BeatTime < .66f;
                    focus = lookingAway ? hero : item;
                    if (lookingAway) back = -back;
                    angle = lookingAway ? 48f : -22f;
                    distance = lookingAway ? 1.6f : 1.75f; size = lookingAway ? .39f : .62f;
                    break;
                case Kind.Duck:
                    focus = Vector3.Lerp(item, hero, beat == Beat.React ? .47f : .08f);
                    angle = beat == Beat.React ? -45f : 12f; distance = 1.9f; size = .70f;
                    break;
                case Kind.Cart:
                    back = -transform.right;
                    angle = 15f; distance = 2.35f; size = .78f;
                    break;
            }
        }

        if (VibeSettings.ReduceEffects)
        {
            angle *= .55f;
            distance *= 1.12f;
        }

        Vector3 view = Quaternion.AngleAxis(angle, Vector3.up) * back;
        Vector3 desired = focus + (view + Vector3.up * elevation).normalized * distance;
        Vector3 pos = ClearPosition(focus, desired);
        Vector3 look = focus - pos;
        if (look.sqrMagnitude < .005f) return gameplayPose;
        return new CameraPose
        {
            position = pos,
            rotation = Quaternion.LookRotation(look),
            lens = orthographic ? size : fov
        };
    }

    Vector3 ClearPosition(Vector3 focus, Vector3 desired)
    {
        Vector3 delta = desired - focus;
        float length = delta.magnitude;
        if (length < .23f) return gameplayPose.position;
        Vector3 direction = delta / length;
        const float radius = .15f;
        Vector3 start = focus + direction * .10f;
        int count = Physics.SphereCastNonAlloc(start, radius, direction, castHits, length - .10f, ~0, QueryTriggerInteraction.Ignore);
        float distance = length;
        for (int i = 0; i < count; i++)
        {
            var hit = castHits[i];
            if (hit.collider == null || hit.collider.transform.IsChildOf(player.transform)) continue;
            distance = Mathf.Min(distance, hit.distance + .04f);
        }

        if (distance >= Mathf.Max(.28f, camera.nearClipPlane + .12f))
            return focus + direction * distance;

        // A plaque can sit against a solid backing. Keep the live gameplay framing
        // if the requested side has no room for a lens.
        return gameplayPose.position;
    }

    bool GroundAhead(Vector3 direction)
    {
        Vector3 root = player.transform.position;
        float sole = playerCapsule != null ? playerCapsule.bounds.min.y : root.y;
        Vector3 check = root + direction * .24f;
        check.y = sole + .55f;
        int count = Physics.RaycastNonAlloc(check, Vector3.down, castHits, 1.15f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            var hit = castHits[i];
            if (hit.collider != null && !hit.collider.transform.IsChildOf(player.transform) &&
                hit.normal.y > .50f && hit.point.y < sole + .33f) return true;
        }
        return false;
    }
}
