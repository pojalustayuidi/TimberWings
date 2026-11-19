using UnityEngine;

[DefaultExecutionOrder(250), DisallowMultipleComponent]
public sealed partial class LeoModelPresentation : MonoBehaviour
{
    public LeoRig rig;
    public PlayerMovement player;
    public PlayerInteraction interaction;
    public BackpackTag backpack;
    public Speaker speaker;
    public bool basicLocomotion = true, blink = true, readBackpack = true;
    [Tooltip("Пауза после рубки перед тем, как убрать топор на пояс. 0 = убрать после завершения взмаха.")]
    public float autoSheatheDelay = 0f;
    float lastToolUse = -100f;
    readonly LeoAirWaterMotion airWater = new LeoAirWaterMotion();
    float holster = 1f, nextBlink = 3f, blinkAt = -10f;
    Vector3 previousPosition;
    LeoAnimationController primary;
    bool initialized;

    bool PrimaryOwnsRig => primary != null && primary.isActiveAndEnabled;

    void Awake() => Initialize();

    void Initialize()
    {
        if (initialized) return;
        if (rig == null) rig = GetComponent<LeoRig>();
        if (rig == null) return;
        if (player == null) player = GetComponentInParent<PlayerMovement>();
        if (player != null)
        {
            if (interaction == null) interaction = player.GetComponent<PlayerInteraction>();
            if (backpack == null) backpack = player.GetComponent<BackpackTag>();
            if (speaker == null) speaker = player.GetComponent<Speaker>();
            previousPosition = player.transform.position;
        }
        primary = rig.GetComponent<LeoAnimationController>();
        rig.PrepareAnimationRest();
        if (!PrimaryOwnsRig) rig.ResetTools();
        chopBook = LeoMotionProfile.Load();
        InitializeLocomotion();
        nextBlink = Time.time + 3f;
        initialized = true;
    }

    void OnEnable()
    {
        GameEvents.ChopHit += OnHit;
        GameEvents.TreeChopped += OnTreeFelled;
        SaveSystem.Loaded += OnLoaded;
        SaveSystem.NewGameStarted += OnLoaded;
        if (player != null) previousPosition = player.transform.position;
        ResetLocomotion();
    }

    void OnDisable()
    {
        GameEvents.ChopHit -= OnHit;
        GameEvents.TreeChopped -= OnTreeFelled;
        SaveSystem.Loaded -= OnLoaded;
        SaveSystem.NewGameStarted -= OnLoaded;
        ResetChopPose();
        work.Reset();
        airWater.Reset();
        if (PrimaryOwnsRig || rig == null || !rig.HasRest) return;
        holster = 1f;
        rig.ResetPose();
        rig.ShowCargo(0, 1);
        rig.ResetTools();
    }

    void OnLoaded()
    {
        if (!PrimaryOwnsRig && rig != null)
        {
            rig.ResetPose();
            rig.ResetTools();
        }
        if (player != null) previousPosition = player.transform.position;
        ResetLocomotion();
        ResetChopPose();
        work.Reset();
        airWater.Reset();
        holster = 1f;
        lastToolUse = -100f;
    }

    void LateUpdate()
    {
        Initialize();
        if (!initialized || rig == null || PrimaryOwnsRig || Time.deltaTime <= 0f) return;
        if (interaction != null && rig.SyncAxe(interaction.EquippedAxe)) ResetChopPose();
        float frameTime = Time.deltaTime;
        float dt = Mathf.Min(.1f, frameTime), now = Time.time;
        float travel = 0f;
        if (player != null)
        {
            Vector3 delta = player.transform.position - previousPosition;
            delta.y = 0f;
            previousPosition = player.transform.position;
            travel = delta.magnitude;
        }
        bool inspecting = StoryInspection.UsesLegacyPose(this);
        bool building = BuildWorkPose.HasWork(player);
        bool conversation = DialogueSequence.FreezesPlayer;
        bool chopping = interaction != null && interaction.IsChopping &&
            !StoryInspection.Active && !conversation && !BuildWorkPose.MovementLocked;
        bool talking = conversation || (speaker != null && speaker.IsSpeaking);
        bool blocked = SaveSystem.IsLoading || BuildingController.AnyRevealing || BuildingController.AnyRestorePending ||
            (player != null && player.ControlLocked);
        float carry = backpack != null && backpack.Capacity > 0 ? Mathf.Clamp01((float)backpack.Carried / backpack.Capacity) : 0f;

        LeoAirWaterMotion.Frame motion = airWater.Read(player, dt);
        ReadChopPose(now, dt, chopping && rig.IsAxeInHand, blocked || conversation || motion.InMotion || WaterSwim.Swimming ||
            building && BuildWorkPose.MovementLocked);
        bool canPlaceFeet = player != null && !blocked && !motion.InMotion && !WaterSwim.Swimming;
        LeoCelebration.Pose cheer = rig.Celebration.Frame;
        if (chopping || chopWeight > .02f) lastToolUse = now;
        bool idleSheathe = !chopping && now - lastToolUse >= Mathf.Max(0f, autoSheatheDelay);
        bool sheath = blocked || motion.StowTool || StoryInspection.Active || work.Weight > .001f ||
            (!chopping && (talking || building || idleSheathe));
        if (OrbitCamera.FirstPersonCarryAxe && !blocked && !talking && !motion.StowTool &&
            !StoryInspection.Active && !building && work.Weight <= .001f && rig.Celebration.Age < 0f) sheath = false;
        float wantedHolster = sheath ? 1f : chopping || OrbitCamera.FirstPersonCarryAxe ? 0f : cheer.holster;
        if (!sheath || chopWeight < .05f)
            holster = Mathf.MoveTowards(holster, wantedHolster, dt * (chopping || motion.InMotion ? 8f : 5.5f));
        if (motion.InMotion) work.Reset();
        else work.Read(player, rig, travel / Mathf.Max(.001f, frameTime), dt, holster,
            canPlaceFeet && !conversation && !StoryInspection.Active && !chopping && chopWeight < .02f, debugWork);

        rig.ResetPose();
        rig.EquipHammer(false);
        if (basicLocomotion || work.Weight > .001f || rig.Celebration.Age >= 0f)
        {
            ApplyLocomotion(travel, frameTime, now, carry,
                basicLocomotion && canPlaceFeet && !chopping && !conversation, canPlaceFeet, cheer);
        }
        else
        {
            ResetLocomotion();
            BraceForChop();
        }
        LeoAxePose.RestWrist(rig, moveWeight);
        if (rig.chest != null)
            rig.chest.localScale = Vector3.Scale(rig.RestScale(rig.chest),
                new Vector3(1f, 1f + Mathf.Sin(now * 1.7f) * .003f, 1f));
        if (talking && !blocked && !chopping && !motion.InMotion)
        {
            float voice = speaker != null && speaker.IsSpeaking ? 1f : 0f;
            rig.Rotate(rig.head, new Vector3(Mathf.Sin(now * 4f) * voice * 1.5f, 0f, 0f));
            rig.Rotate(rig.jaw, new Vector3(Mathf.Abs(Mathf.Sin(now * 9f)) * voice * 3f, 0f, 0f));
        }
        motion.Apply(rig, holster);
        PlaceChopTools();
        if (work.Weight > .001f)
        {
            work.PlaceHammer(rig, transform, player);
        }
        else
        {
            rig.PlaceHammer();
            cheer.ApplyHands(rig, holster);
        }
        if (inspecting && !motion.InMotion && holster >= 1f && rig.IsReadyForInspection &&
            StoryInspection.ReadPose(rig, out var inspectionPose))
        {
            StoryInspection.ApplyActorPose(rig, transform, holster, inspectionPose);
            rig.PlaceHammer();
        }
        rig.RecordToolPose(holster, work.Weight > .001f);
        if (readBackpack && backpack != null) rig.ShowCargo(backpack.Carried, backpack.Capacity);
        if (blink)
        {
            if (now >= nextBlink)
            {
                blinkAt = now;
                nextBlink = now + 3.1f + Mathf.Repeat(now * .37f, 2.5f);
            }
            float t = (now - blinkAt) / .16f;
            float amount = t < 0f || t >= 1f ? 1f : Mathf.Lerp(.04f, 1f, Mathf.Abs(t * 2f - 1f));
            if (rig.eyeL != null) rig.eyeL.localScale = Vector3.Scale(rig.RestScale(rig.eyeL), new Vector3(1f, amount, 1f));
            if (rig.eyeR != null) rig.eyeR.localScale = Vector3.Scale(rig.RestScale(rig.eyeR), new Vector3(1f, amount, 1f));
        }
    }
}
