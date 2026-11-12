using UnityEngine;

// V5 camera director for ONLY the Ribbon_Memory object. Works after
// StoryInspection.LateUpdate and feeds its resulting pose BACK into the original
// inspection camera, so its existing recovery restores the gameplay camera.
// Trees are NOT moved/disabled: the project's TreeToonPeek globals are reused.
[DisallowMultipleComponent, DefaultExecutionOrder(2500)]
public sealed class TWRibbonInspectionCamera : MonoBehaviour
{
    [Header("Ribbon inspection frame")]
    [Range(2.7f, 5f)] public float distance = 3.65f;
    [Range(0.3f, 2.3f)] public float lift = 1.38f;
    [Range(15f, 80f)] public float sideAngle = 40f;
    [Range(0.08f, 1.2f)] public float smoothTime = .34f;
    [Range(40f, 65f)] public float fieldOfView = 48f;
    [Range(1.2f, 3.0f)] public float orthographicSize = 1.95f;

    StoryInspection inspection;
    bool wasActive;
    Vector3 pos, velocity;
    Quaternion rotation;
    float lens;
    static readonly RaycastHit[] hits = new RaycastHit[32];
    static readonly int PeekDataId = Shader.PropertyToID("_PeekData");
    static readonly int PeekParamsId = Shader.PropertyToID("_PeekParams");

    void Awake() => inspection = GetComponent<StoryInspection>();

    void LateUpdate()
    {
        if (inspection == null) inspection = GetComponent<StoryInspection>();
        if (inspection == null || !inspection.TWRibbonCameraActive)
        {
            wasActive = false;
            return;
        }

        Camera cam = inspection.TWRibbonCamera;
        if (cam == null) return;
        Vector3 focus = inspection.TWRibbonWorldFocus;
        var hero = PlayerMovement.Instance;
        Vector3 fromHero = hero != null ? hero.transform.position - focus : -transform.forward;
        fromHero.y = 0f;
        if (fromHero.sqrMagnitude < .04f) fromHero = -transform.forward;
        fromHero.y = 0f;
        fromHero.Normalize();

        // Always photograph the MARKED (front) side of the ribbon. Leo may
        // approach from behind; his position only chooses the lateral side.
        Vector3 front = transform.forward;
        front.y = 0f;
        if (front.sqrMagnitude < .001f) front = -fromHero;
        front.Normalize();
        float side = Vector3.Cross(front, fromHero).y >= 0f ? 1f : -1f;
        Vector3 baseDir = Quaternion.AngleAxis(sideAngle*side, Vector3.up) * front;
        Vector3 ideal = FindClearShot(focus, baseDir, fromHero, hero);
        if (!wasActive)
        {
            pos = cam.transform.position;
            rotation = cam.transform.rotation;
            lens = cam.orthographic ? cam.orthographicSize : cam.fieldOfView;
            velocity = Vector3.zero;
            wasActive = true;
        }
        float dt = Mathf.Clamp(Time.deltaTime, 0f, .10f);
        pos = Vector3.SmoothDamp(pos, ideal, ref velocity, Mathf.Max(.08f, smoothTime),
            Mathf.Infinity, dt);
        // Do not cut through a collider during the move to the new orbit shot.
        Vector3 look = focus - pos;
        if (look.sqrMagnitude < .04f) return;
        Quaternion desiredRot = Quaternion.LookRotation(look.normalized, Vector3.up);
        rotation = Quaternion.Slerp(rotation, desiredRot, 1f-Mathf.Exp(-7f*dt));
        float desiredLens = cam.orthographic ? orthographicSize : fieldOfView;
        lens = Mathf.Lerp(lens, desiredLens, 1f-Mathf.Exp(-5.8f*dt));
        inspection.TWRibbonApplyCinematicPose(pos, rotation, lens);

        // Reuse TimberWings' existing tree transparency instead of drawing
        // the lens from inside the tree. Focus the shader opening on the prop,
        // not on Leo, so the ribbon itself remains at the focus depth.
        Vector3 vp = cam.WorldToViewportPoint(focus);
        if (vp.z > .15f && !VibeSettings.ReduceEffects)
        {
            float height = cam.orthographic ? 2f*cam.orthographicSize :
                2f*vp.z*Mathf.Tan(cam.fieldOfView*.5f*Mathf.Deg2Rad);
            float radius = 1.15f / Mathf.Max(.2f, height);
            Shader.SetGlobalVector(PeekDataId, new Vector4(vp.x,vp.y,radius,.60f));
            Shader.SetGlobalVector(PeekParamsId,
                new Vector4(vp.z-.48f, cam.aspect, 1f, .07f));
        }
    }

    Vector3 FindClearShot(Vector3 focus, Vector3 direction, Vector3 original, PlayerMovement hero)
    {
        // Prefer a clean over-shoulder three-quarter angle. Try alternate
        // angles before moving the lens closer. Tree colliders are skipped
        // because the existing shader transparency handles tree occlusion.
        float[] angles = { 0f, -20f, 20f, -42f, 42f };
        float bestScore = -999f;
        Vector3 best = focus + direction*distance + Vector3.up*lift;
        foreach (float angle in angles)
        {
            Vector3 horizontal = Quaternion.AngleAxis(angle,Vector3.up)*direction;
            Vector3 target = focus + horizontal*distance + Vector3.up*lift;
            float clear = Clearance(focus,target,hero);
            float score = clear - Mathf.Abs(angle)*.009f;
            if (score > bestScore)
            {
                bestScore=score;
                float full=(target-focus).magnitude;
                float allowed = Mathf.Clamp(clear-.20f, .55f, full);
                best=focus+(target-focus).normalized*allowed;
            }
        }
        return best;
    }

    float Clearance(Vector3 focus, Vector3 desired, PlayerMovement hero)
    {
        Vector3 delta=desired-focus;
        float length=delta.magnitude;
        Vector3 dir=delta/Mathf.Max(.001f,length);
        float clear=length;
        int count=Physics.SphereCastNonAlloc(focus+dir*.18f, .15f, dir, hits,
            Mathf.Max(.05f,length-.18f), ~0, QueryTriggerInteraction.Ignore);
        for(int i=0;i<count;i++)
        {
            Collider c=hits[i].collider;
            if(c==null || c.transform.IsChildOf(transform) ||
                (hero!=null && c.transform.IsChildOf(hero.transform))) continue;
            if(c.GetComponentInParent<ResourceItem>()!=null ||
                c.GetComponentInParent<TreeSpeciesInfo>()!=null) continue;
            // Ignore floor/ground if a ray grazes it near the focus.
            if(hits[i].distance<.17f && hits[i].normal.y>.65f) continue;
            clear=Mathf.Min(clear,hits[i].distance+.18f);
        }
        return clear;
    }

    void OnDisable()
    {
        wasActive=false;
        velocity=Vector3.zero;
    }
}
