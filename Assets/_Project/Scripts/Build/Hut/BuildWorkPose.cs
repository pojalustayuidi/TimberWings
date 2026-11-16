using UnityEngine;

// Owns one player's approach and hammer target until the site releases them.
[DisallowMultipleComponent]
[DefaultExecutionOrder(900)]
public class BuildWorkPose : MonoBehaviour
{
    static BuildWorkPose active;

    [Tooltip("Максимальная скорость разворота к рабочей поверхности, градусов/с")]
    public float turnSpeed = 420f;
    [Tooltip("Время на короткий подход к рабочему месту, в секундах")]
    public float approachTimeout = 2f;

    PlayerMovement movement;
    PlayerInteraction interaction;
    CapsuleCollider capsule;
    Rigidbody body;
    Animator animator;
    string fallbackParam;
    LeoRig rig;
    BuildingController site, lastSite;
    BuildWorkSpot spot;
    Transform standAnchor;
    GameObject workPlank;
    Mesh plankMesh;
    Material plankMaterial;
    bool lockMove, approaching;
    float approachUntil, lockUntil, stowUntil;
    const float StowTimeout = 1.75f;
    int strikes;
    float lastStrikeAt;
    float lastEndedAt = -100f, nodStartedAt = -100f;

    internal static BuildingController LockedSite => active != null && active.lockMove ? active.site : null;
    public static bool MovementLocked => LockedSite != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() => active = null;

    static bool Owns(BuildingController owner) => owner != null && active != null && active.site == owner;

    internal static bool IsActiveFor(BuildingController owner) => Owns(owner);
    internal static bool KeepsPlayerAt(BuildingController owner) => Owns(owner) && active.lockMove;
    internal static bool HasHammerRig(BuildingController owner) => Owns(owner) && active.rig != null;
    internal static bool HasWorkTarget(BuildingController owner) => Owns(owner) && active.HasSurface;
    public static bool IsApproaching(BuildingController owner) => Owns(owner) && active.approaching;
    internal static bool IsPreparing(BuildingController owner) => Owns(owner) &&
        (active.approaching || (active.rig != null && !active.rig.IsAxeStowed && Time.time < active.stowUntil));
    public static int ContactCount(BuildingController owner) => Owns(owner) ? active.strikes : 0;

    bool HasSurface => spot.surface != null && spot.surface.enabled && spot.surface.gameObject.activeInHierarchy;

    internal static bool IsPaused =>
        SaveSystem.IsLoading || BuildingController.AnyRestorePending || BuildingController.AnyRevealing ||
        MobileInput.ModalOpen || DialogueSequence.FreezesPlayer || StoryInspection.Active || LeoCelebration.IsPlaying;

    public static bool TryGetSpot(BuildingController owner, out BuildWorkSpot result)
    {
        result = default;
        if (!HasWorkTarget(owner)) return false;
        result = active.spot;
        return true;
    }

    internal static bool HasWork(PlayerMovement player) =>
        player != null && active != null && active.site != null && active.movement == player;

    bool HoldingStill => movement != null && !approaching && !movement.AutoWalking && !movement.IsDashing &&
        (interaction == null || !interaction.IsChopping) &&
        (lockMove || (Mathf.Abs(MobileInput.Horizontal) <= .03f && Mathf.Abs(MobileInput.Vertical) <= .03f));

    internal static bool TryGetFacing(PlayerMovement player, out Vector3 direction, out float degreesPerSecond)
    {
        direction = Vector3.zero;
        degreesPerSecond = 0f;
        if (!HasWork(player) || IsPaused || !active.HoldingStill || (active.rig != null && !active.HasSurface))
            return false;

        direction = active.Facing;
        degreesPerSecond = active.turnSpeed;
        return direction.sqrMagnitude > .001f;
    }

    internal static bool TryGetStrikeTarget(PlayerMovement player, out BuildWorkSpot result)
    {
        result = default;
        if (!HasWork(player) || active.approaching || !active.HasSurface || player.model == null ||
            player.ControlLocked || player.AutoWalking || player.IsDashing || WaterSwim.Swimming || IsPaused)
            return false;

        var visual = player.model;
        Vector3 localHit = visual.InverseTransformPoint(active.spot.hit);
        if (Mathf.Abs(localHit.x) > .36f || localHit.y < .54f || localHit.y > 1.24f ||
            localHit.z < .32f || localHit.z > .86f ||
            Vector3.Dot(visual.forward, active.Facing) < .96f) return false;
        result = active.spot;
        return true;
    }

    internal static bool TryBegin(BuildingController owner, PlayerInventory inventory)
    {
        if (active != null && active.site != null) return Owns(owner);
        if (inventory == null || !inventory.isActiveAndEnabled || IsPaused) return false;

        var actor = inventory.GetComponentInParent<PlayerMovement>();
        if (actor != null && (!actor.isActiveAndEnabled || actor.ControlLocked || actor.IsDashing || WaterSwim.Swimming))
            return false;
        var host = actor != null ? actor.gameObject : inventory.gameObject;
        var job = host.GetComponent<BuildWorkPose>();
        if (job == null) job = host.AddComponent<BuildWorkPose>();
        if (!job.isActiveAndEnabled) return false;
        active = job;
        job.Begin(owner, actor);
        return true;
    }

    internal static void Release(BuildingController owner)
    {
        if (Owns(owner)) active.End();
    }

    internal static bool NotifyStrike(PlayerMovement player, Vector3 face)
    {
        if (!TryGetStrikeTarget(player, out var contact)) return false;
        float tolerance = .06f * player.model.lossyScale.y;
        if ((face - contact.hit).sqrMagnitude > tolerance * tolerance ||
            Time.time - active.lastStrikeAt < .13f) return false;
        active.Strike();
        return true;
    }

    void OnEnable()
    {
        GameEvents.BuildRevealStarted += OnRevealStarted;
        GameEvents.BuildRevealFinished += OnRevealFinished;
    }

    void Begin(BuildingController owner, PlayerMovement actor)
    {
        movement = actor;
        interaction = GetComponent<PlayerInteraction>();
        capsule = GetComponent<CapsuleCollider>();
        body = GetComponent<Rigidbody>();
        animator = interaction != null && interaction.animator != null
            ? interaction.animator : GetComponentInChildren<Animator>();
        rig = FindActiveRig(actor);
        fallbackParam = null;
        if (rig == null && animator != null && animator.runtimeAnimatorController != null)
        {
            string name = interaction == null ? "isChopping" : interaction.chopBool;
            foreach (var parameter in animator.parameters)
                if (parameter.type == AnimatorControllerParameterType.Bool &&
                    string.Equals(parameter.name, name, System.StringComparison.OrdinalIgnoreCase))
                    fallbackParam = parameter.name;
        }

        site = owner;
        lastSite = null;
        nodStartedAt = -100f;
        strikes = 0;
        lastStrikeAt = -100f;
        lockMove = owner.lockPlayerWhileBuilding && movement != null;
        stowUntil = Time.time + StowTimeout;
        lockUntil = Time.time + owner.BuildTime + owner.lockSafetySeconds + approachTimeout;
        if (lockMove) movement.CancelWalk();

        if (!LeoBuildMotion.HasHammer(rig) || capsule == null || !capsule.enabled) return;
        if (BuildWorkSpotFinder.TryFind(owner, actor, capsule, out spot))
        {
            if (NearStand(.04f)) return;
            if (lockMove)
            {
                if (standAnchor == null) standAnchor = new GameObject("BuildStandSpot").transform;
                standAnchor.position = spot.stand;
                movement.WalkToBuildSpot(standAnchor);
                approaching = true;
                approachUntil = Time.time + Mathf.Max(.3f, approachTimeout);
                return;
            }
        }
        UseWorkPlank();
    }

    static LeoRig FindActiveRig(PlayerMovement actor)
    {
        Transform visual = actor != null ? actor.model : null;
        if (visual == null) return null;
        var controller = visual.GetComponentInChildren<LeoAnimationController>();
        if (controller != null && controller.isActiveAndEnabled) return controller.rig;
        var presentation = visual.GetComponentInChildren<LeoModelPresentation>();
        return presentation != null && presentation.isActiveAndEnabled ? presentation.rig : null;
    }

    void UseWorkPlank()
    {
        spot = default;
        if (!BuildWorkSpotFinder.TryPlacePlank(site, movement, capsule, out var candidate, out var origin, out var rotation))
        {
            lockMove = false;
            return;
        }

        if (plankMaterial == null)
        {
            var shader = Shader.Find("Project/CandyToon");
            if (shader == null) { lockMove = false; return; }
            plankMaterial = new Material(shader) { name = "Construction wood" };
            plankMesh = BuildWorkSpotFinder.CreateWorkPlank();
        }

        workPlank = new GameObject("Construction work plank");
        workPlank.transform.SetPositionAndRotation(origin, rotation);
        workPlank.transform.localScale = Vector3.one * movement.model.lossyScale.y;
        workPlank.AddComponent<MeshFilter>().sharedMesh = plankMesh;
        var renderer = workPlank.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = plankMaterial;
        spot = candidate;
        spot.surface = renderer;
    }

    Vector3 Facing
    {
        get
        {
            Vector3 direction = HasSurface ? -spot.normal : site.transform.position - transform.position;
            direction.y = 0f;
            return direction.normalized;
        }
    }

    bool NearStand(float radius)
    {
        Vector3 delta = spot.stand - transform.position;
        delta.y = 0f;
        return delta.sqrMagnitude <= radius * radius;
    }

    void Update()
    {
        if (site == null)
        {
            if (active == this) End();
            return;
        }
        if (!site.isActiveAndEnabled || movement != null &&
            (!movement.isActiveAndEnabled || movement.ControlLocked || WaterSwim.Swimming))
        {
            End();
            return;
        }
        if (IsPaused)
        {
            lockUntil += Time.deltaTime;
            stowUntil += Time.deltaTime;
            if (fallbackParam != null && animator != null) animator.SetBool(fallbackParam, false);
            return;
        }

        if (lockMove && Time.time > lockUntil) lockMove = false;
        if (approaching)
        {
            if (lockMove && movement.IsWalkingTo(standAnchor) && Time.time < approachUntil) return;
            movement.CancelWalkTo(standAnchor);
            approaching = false;
            if (!NearStand(.06f) || !HasSurface) UseWorkPlank();
        }

        if (fallbackParam != null && animator != null) animator.SetBool(fallbackParam, HoldingStill);
        if (movement != null && movement.model == null && body != null && !body.isKinematic &&
            TryGetFacing(movement, out Vector3 direction, out float speed))
        {
            body.MoveRotation(Quaternion.RotateTowards(body.rotation, Quaternion.LookRotation(direction), speed * Time.deltaTime));
        }
    }

    void Strike()
    {
        int index = strikes++;
        lastStrikeAt = Time.time;
        float progress = site.Progress;
        bool accent = index % 3 == 2;
        float power = Mathf.Lerp(.85f, 1.35f, progress) * (accent ? 1.20f : 1f);
        SfxManager.Play("build", accent ? .62f : .43f, accent ? .92f : index % 2 == 0 ? 1.09f : 1.01f);
        BuildStrikes.Raise(new BuildStrikeInfo
        {
            site = site,
            point = spot.hit + spot.normal * .012f,
            normal = spot.normal,
            ground = new Vector3(spot.hit.x, spot.groundY, spot.hit.z),
            power = power,
            progress = progress,
            index = index
        });
    }

    void End()
    {
        if (active == this) active = null;
        if (movement != null && standAnchor != null) movement.CancelWalkTo(standAnchor);
        if (lockMove) MobileInput.SuppressHeldActionUntilRelease();
        lastSite = site;
        lastEndedAt = Time.time;
        site = null;
        approaching = lockMove = false;
        spot = default;
        if (fallbackParam != null && animator != null) animator.SetBool(fallbackParam, false);
        if (workPlank != null)
        {
            workPlank.SetActive(false);
            Destroy(workPlank);
            workPlank = null;
        }
    }

    void OnRevealStarted(BuildingController owner)
    {
        if (owner == null || owner != lastSite || !owner.RevealIsFinal || Time.time - lastEndedAt > .40f ||
            movement == null || movement.ControlLocked || DialogueSequence.FreezesPlayer || VibeSettings.ReduceEffects) return;
        nodStartedAt = Time.time;
    }

    void OnRevealFinished(BuildingController owner)
    {
        if (owner == lastSite) nodStartedAt = -100f;
    }

    // Runs after either pose writer, so the final look at the building survives the pose reset.
    void LateUpdate()
    {
        if (nodStartedAt < 0f || !BuildingController.AnyRevealing || VibeSettings.ReduceEffects ||
            rig == null || movement == null || movement.ControlLocked || EndingSequence.Active ||
            DialogueManager.IsBusy || DialogueSequence.FreezesPlayer) return;
        float k = (Time.time - nodStartedAt - .60f) / .90f;
        if (k <= 0f || k >= 1f) return;
        float bow = Mathf.Sin(k * Mathf.PI);
        rig.Rotate(rig.head, new Vector3(7f * bow, 0f, 0f));
        rig.Rotate(rig.chest, new Vector3(2f * bow, 0f, 0f));
    }

    void OnDisable()
    {
        GameEvents.BuildRevealStarted -= OnRevealStarted;
        GameEvents.BuildRevealFinished -= OnRevealFinished;
        if (active == this || site != null) End();
        nodStartedAt = -100f;
    }

    void OnDestroy()
    {
        if (standAnchor != null) Destroy(standAnchor.gameObject);
        if (plankMesh != null) Destroy(plankMesh);
        if (plankMaterial != null) Destroy(plankMaterial);
        if (active == this) active = null;
    }
}
