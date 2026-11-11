using System.Collections;
using UnityEngine;

public sealed partial class FinalIslandPortal
{
    LeoPortalPose entryPose;
    RigidbodyInterpolation previousInterpolation;
    Quaternion entryFacing;
    bool ownsActor, previousControlLock;
    float entryTime, drawIn, approachPush;

    IEnumerator EnterPortal()
    {
        if (actor == null || body == null || body.isKinematic || actor.ControlLocked) yield break;

        actor.CancelWalk();
        previousControlLock = actor.ControlLocked;
        previousInterpolation = body.interpolation;
        body.linearVelocity = Vector3.zero;
        body.interpolation = RigidbodyInterpolation.None;
        body.isKinematic = true;
        actor.ControlLocked = true;
        ownsActor = true;

        Vector3 start = transform.InverseTransformPoint(body.position);
        Vector3 exit = new Vector3(0f, start.y + .31f, .88f);
        Transform model = actor.model != null ? actor.model : actor.transform;
        entryFacing = model.rotation;
        entryPose = new LeoPortalPose(model.GetComponentInChildren<LeoRig>());
        approachPush = LeoPortalPose.Ease(elapsed, .8f, 5.8f) * .72f;
        Quaternion toward = Quaternion.LookRotation(transform.forward, Vector3.up);
        bool windPlayed = false;

        for (entryTime = 0f; entryTime < LeoPortalPose.Duration; entryTime += Time.unscaledDeltaTime)
        {
            if (actor == null || body == null) yield break;

            charge = Mathf.Lerp(.78f, 1f, LeoPortalPose.Ease(entryTime, .04f, .83f));
            drawIn = LeoPortalPose.Ease(entryTime, .40f, 1.50f);
            if (!windPlayed && entryTime >= .28f)
            {
                windPlayed = true;
                PlayInhale();
            }

            float pull = LeoPortalPose.PullAt(entryTime);
            var at = Vector3.Lerp(start, exit, pull);
            at.z += .09f * LeoPortalPose.Ease(entryTime, .28f, .68f)
                * (1f - LeoPortalPose.Ease(entryTime, .74f, 1.11f));
            at.y += .17f * Mathf.Sin(Mathf.PI * pull);
            Vector3 world = transform.TransformPoint(at);
            body.position = world;
            actor.transform.position = world;

            float twist = 14f * LeoPortalPose.Ease(entryTime, 1.08f, 1.78f);
            Quaternion face = Quaternion.Slerp(entryFacing, toward, LeoPortalPose.Ease(entryTime, 0f, .36f));
            model.rotation = face * Quaternion.Euler(0f, twist, 0f);
            fade.color = new Color(0f, 0f, 0f, LeoPortalPose.Ease(pull, .56f, 1f));
            yield return null;
        }
    }

    void ApplyEntryPose()
    {
        if (ownsActor) entryPose?.Apply(entryTime);
    }

    void RestoreEntry()
    {
        if (!ownsActor) return;
        ownsActor = false;
        entryPose?.Release();
        entryPose = null;
        if (actor != null) actor.ControlLocked = previousControlLock;
        if (body == null) return;
        body.isKinematic = false;
        body.interpolation = previousInterpolation;
    }
}
