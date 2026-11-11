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
    readonly LeoAirWaterMotion airWater = new LeoAirWaterMotion();
    float holster, nextBlink = 3f, blinkAt = -10f;
    Vector3 previousPosition;
    bool initialized;

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
        rig.PrepareAnimationRest();
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
        ResetWorkPose();
        airWater.Reset();
        if (rig == null || !rig.HasRest) return;
        rig.ResetPose();
        rig.ShowCargo(0, 1);
        rig.EquipHammer(false);
    }

    void OnLoaded()
    {
        if (rig != null) rig.ResetPose();
        if (player != null) previousPosition = player.transform.position;
        ResetLocomotion();
        ResetChopPose();
        ResetWorkPose();
        airWater.Reset();
        holster = 0f;
    }

    void LateUpdate()
    {
        Initialize();
        if (!initialized || rig == null || Time.deltaTime <= 0f) return;
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
        bool chopping = interaction != null && interaction.IsChopping;
        bool conversation = DialogueSequence.FreezesPlayer;
        bool talking = conversation || (speaker != null && speaker.IsSpeaking);
        bool blocked = SaveSystem.IsLoading || BuildingController.AnyRevealing || BuildingController.AnyRestorePending ||
            (player != null && player.ControlLocked);
        float carry = backpack != null && backpack.Capacity > 0 ? Mathf.Clamp01((float)backpack.Carried / backpack.Capacity) : 0f;

        LeoAirWaterMotion.Frame motion = airWater.Read(player, dt);
        ReadChopPose(now, dt, chopping, blocked || conversation || motion.InMotion || WaterSwim.Swimming);
        bool canPlaceFeet = player != null && !blocked && !motion.InMotion && !WaterSwim.Swimming;
        if (motion.InMotion) ResetWorkPose();
        else ReadWorkPose(travel / Mathf.Max(.001f, frameTime), dt, canPlaceFeet && !talking, chopping || chopWeight > .02f);
        LeoCelebration.Pose cheer = rig.Celebration.Frame;

        rig.ResetPose();
        rig.EquipHammer(false);
        if (basicLocomotion || workWeight > .001f || rig.Celebration.Age >= 0f)
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
        bool sheath = blocked || motion.StowTool || (!chopping && (talking || workRequested || workWeight > .001f));
        float wantedHolster = sheath ? 1f : chopping ? 0f : cheer.holster;
        holster = Mathf.MoveTowards(holster, wantedHolster, dt * (chopping || motion.InMotion ? 8f : 5.5f));
        if (chopHitFrame == Time.frameCount && !sheath) holster = 0f;
        motion.Apply(rig, holster);
        if (workWeight > .001f)
        {
            PlaceWorkHammer();
        }
        else
        {
            PlaceChopTools();
            rig.PlaceHammer();
            cheer.ApplyHands(rig, holster);
        }
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
