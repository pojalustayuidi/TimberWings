using UnityEngine;

// The jump is a pose on the visual rig. Physics and player input keep running.
sealed class LeoCelebration
{
    internal const float Duration = 2.08f;
    const float Takeoff = .59f, Landing = 1.04f;
    static LeoCelebration current;

    readonly RaycastHit[] groundHits = new RaycastHit[8];
    LeoRig rig;
    PlayerMovement player;
    PlayerInteraction interaction;
    Speaker speaker;
    Rigidbody body;
    CapsuleCollider capsule;
    float queuedUntil, settled, age = -1f, blend;
    bool orbit, interrupted;
    Vector3 startPosition;

    internal int Sequence { get; private set; }
    internal float Age => age;
    internal Transform Model => rig.transform;
    internal bool WantsShot => orbit && age >= 0f && !interrupted;
    internal Pose Frame { get; private set; }

    internal static LeoCelebration ForTarget(Transform target)
    {
        return current != null && current.IsPlayerModel && current.player.transform == target ? current : null;
    }

    bool IsPlayerModel => rig != null && player != null && player.model != null &&
        (player.model == rig.transform || rig.transform.IsChildOf(player.model));

    public void Enable(LeoRig owner)
    {
        rig = owner;
        player = owner.GetComponentInParent<PlayerMovement>();
        if (player == null) return;
        interaction = player.GetComponent<PlayerInteraction>();
        speaker = player.GetComponent<Speaker>();
        body = player.GetComponent<Rigidbody>();
        capsule = player.GetComponent<CapsuleCollider>();
        current = this;
        GameEvents.LevelUp += OnLevelUp;
        GameEvents.BuildRevealFinished += OnBuildFinished;
        SaveSystem.Loaded += Reset;
        SaveSystem.NewGameStarted += Reset;
    }

    public void Disable()
    {
        if (current == this) current = null;
        GameEvents.LevelUp -= OnLevelUp;
        GameEvents.BuildRevealFinished -= OnBuildFinished;
        SaveSystem.Loaded -= Reset;
        SaveSystem.NewGameStarted -= Reset;
        Reset();
    }

    void OnLevelUp(int level) => Queue();

    void OnBuildFinished(BuildingController site)
    {
        if (site != null && site.RevealIsFinal && site.PlayerInside) Queue();
    }

    void Queue()
    {
        if (!IsPlayerModel || SaveSystem.IsLoading || age >= 0f) return;
        queuedUntil = Time.time + 5.5f;
    }

    void Reset()
    {
        queuedUntil = settled = 0f;
        End();
    }

    void End()
    {
        age = -1f;
        blend = 0f;
        orbit = interrupted = false;
        Frame = default;
    }

    internal void Tick(float frameTime)
    {
        if (frameTime <= 0f || (age < 0f && queuedUntil == 0f)) return;
        if (!IsPlayerModel || SaveSystem.IsLoading)
        {
            Reset();
            return;
        }

        bool blocked = player.ControlLocked || WaterSwim.Swimming || MobileInput.ModalOpen || BuildingController.AnyRevealing ||
            DialogueSequence.FreezesPlayer || DialogueManager.IsBusy || (speaker != null && speaker.IsSpeaking) ||
            BuildingController.AnyRestorePending || BuildWorkPose.TryGetFocus(player, out _);
        bool chopping = interaction != null && interaction.IsChopping;
        Vector3 velocity = body != null ? body.linearVelocity : Vector3.zero;
        bool moving = player.AutoWalking || player.IsDashing || Mathf.Abs(velocity.y) > .65f ||
            velocity.x * velocity.x + velocity.z * velocity.z > .22f * .22f ||
            Mathf.Abs(MobileInput.Horizontal) > .04f || Mathf.Abs(MobileInput.Vertical) > .04f;

        float dt = Mathf.Min(frameTime, .05f);
        bool acted = MobileInput.ActionDown || Input.GetMouseButtonDown(0);
        if (age < 0f)
        {
            if (Time.time >= queuedUntil)
            {
                Reset();
                return;
            }
            if (blocked || chopping || moving || acted || CameraVibe.RevealShown > .05f || !Grounded())
            {
                settled = 0f;
                return;
            }
            settled += dt;
            if (settled < .17f) return;
            age = 0f;
            startPosition = player.transform.position;
            blend = 1f;
            orbit = !VibeSettings.ReduceEffects;
            queuedUntil = settled = 0f;
            Sequence++;
        }
        else if (blocked || chopping || (player.transform.position - startPosition).sqrMagnitude > .45f * .45f)
        {
            End();
            return;
        }

        age += dt;
        interrupted |= moving || acted;
        if (interrupted) blend = Mathf.MoveTowards(blend, 0f, dt / .15f);
        if (age >= Duration || blend <= 0f)
        {
            End();
            return;
        }
        Frame = Sample(age, blend);
    }

    bool Grounded()
    {
        if (capsule == null || !capsule.enabled) return false;
        Bounds bounds = capsule.bounds;
        Vector3 origin = new Vector3(bounds.center.x, bounds.min.y + .19f, bounds.center.z);
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, groundHits, .33f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            var hit = groundHits[i];
            if (hit.collider != null && !hit.collider.transform.IsChildOf(player.transform) &&
                hit.normal.y >= .65f) return true;
        }
        return false;
    }

    internal struct Pose
    {
        public float lift, crouch, tuck, push, leftFist, rightFist, smile, twist, holster;

        public Vector3 FootOffset(bool left) => new Vector3(0f, lift + (left ? .039f : .079f) * tuck,
            (left ? .072f : -.062f) * tuck);

        public float FootPitch(bool left) => 21f * push + (left ? 8f : 19f) * tuck;

        public void ApplyBody(LeoRig rig, float load)
        {
            if (rig.hips != null)
                rig.hips.localPosition += new Vector3(-.029f * twist, lift - crouch, -.066f * crouch);
            Add(rig.hips, 0f, -5f * twist, -3f * twist);
            Add(rig.spine, 100f * crouch - 15f * lift + 2f * load * smile, 7f * twist, 4f * twist);
            Add(rig.chest, -5f * smile, 8f * twist, 3f * twist);
            Add(rig.head, -9f * smile - 20f * crouch, -7f * twist, -3f * twist);
            Add(rig.upperArmL, 95f * crouch - 108f * leftFist, -4f * leftFist, -24f * leftFist);
            Add(rig.elbowL, -49f * leftFist, 0f, 0f);
            Add(rig.upperArmR, -72f * rightFist, 7f * rightFist, 23f * rightFist);
            Add(rig.elbowR, -67f * rightFist, 0f, 0f);
            Add(rig.jaw, 12f * smile, 0f, 0f);
            Add(rig.browL, 0f, 0f, 8f * smile);
            Add(rig.browR, 0f, 0f, -8f * smile);
            if (smile > 0f && rig.mouth != null)
                rig.mouth.localScale = Vector3.Scale(rig.mouth.localScale, new Vector3(1f + .11f * smile, 1f, 1f));
        }

        public void ApplyHands(LeoRig rig, float stowed)
        {
            Curl(rig.fingersL, -36f * leftFist);
            Curl(rig.fingerTipsL, -29f * leftFist);
            Add(rig.thumbL, -12f * leftFist, 0f, 0f);
            float grip = rightFist * LeoMotionProfile.Smooth((stowed - .84f) / .16f);
            Curl(rig.fingersR, -48f * grip);
            Curl(rig.fingerTipsR, -40f * grip);
            Add(rig.thumbR, -10f * grip, 0f, 0f);
        }

        static void Add(Transform joint, float x, float y, float z)
        {
            if (joint != null && (x != 0f || y != 0f || z != 0f)) joint.localRotation *= Quaternion.Euler(x, y, z);
        }

        static void Curl(Transform[] fingers, float angle)
        {
            if (fingers == null || angle == 0f) return;
            foreach (Transform finger in fingers) Add(finger, angle, 0f, 0f);
        }
    }

    internal static Pose Sample(float time, float weight = 1f)
    {
        float air = Mathf.Clamp01((time - Takeoff) / (Landing - Takeoff));
        float flight = 4f * air * (1f - air);
        float prepare = Ease(time, .23f, .47f) * (1f - Ease(time, .47f, Takeoff));
        float absorb = Ease(time, Landing, 1.145f) * (1f - Ease(time, 1.145f, 1.42f));
        float liftFist = Ease(time, .43f, .76f) * (1f - Ease(time, 1.10f, 1.60f));
        float pumpFist = Ease(time, .55f, .85f) * (1f - Ease(time, 1.00f, 1.48f));
        return new Pose
        {
            lift = .32f * flight * weight,
            tuck = flight * flight * weight,
            crouch = (.109f * prepare + .087f * absorb) * weight,
            push = Ease(time, .48f, .61f) * (1f - Ease(time, .62f, .74f)) * weight,
            leftFist = liftFist * weight,
            rightFist = pumpFist * weight,
            twist = (.75f * liftFist + .25f * prepare) * weight,
            smile = Ease(time, .32f, .73f) * (1f - Ease(time, 1.28f, 1.78f)) * weight,
            holster = Ease(time, .02f, .48f) * (1f - Ease(time, 1.54f, 2.03f)) * Mathf.Clamp01(weight * 2.25f)
        };
    }

    static float Ease(float time, float from, float to) => LeoMotionProfile.Smooth((time - from) / (to - from));
}
