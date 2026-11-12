using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent, DefaultExecutionOrder(400)]
public sealed partial class StoryInspection : MonoBehaviour
{
    public enum Kind
    {
        Knife, Ribbon, Board, Finale, BenchMarks, Witness, Arch, Stump, ThreeMarks, CopperArrow, Duck, Cart
    }

    enum Beat { Approach, Look, Reach, Detail, React, Recover }

    public const string FinaleLine = "«Седьмую скамью я всё-таки унёс»... Дядя, о чём ты?";

    public Kind kind;
    public MonoBehaviour source;
    public Transform detail;
    public Transform bridgeEngraving;
    public Vector3 detailOffset;
    public AudioClip atmosphere;

    [HideInInspector] public bool superseded;
    [HideInInspector] public bool ownsGroveSource, ownsGroveObject;
    [HideInInspector] public Transform copiedDetail, copiedBridge;

    static StoryInspection current;
    public static bool Active => current != null;
    internal bool SkipRequested { get; private set; }
    internal static Vector3 WalkDirection => Active ? current.walkDirection : Vector3.zero;
    internal static bool SuppressPlayerAction
    {
        get
        {
            if (suppressHeldAction && !MobileInput.ActionHeld) suppressHeldAction = false;
            return Active || Time.frameCount == releasedFrame || suppressHeldAction;
        }
    }
    static bool suppressHeldAction;
    static int releasedFrame = -1;

    PlayerMovement player;
    CapsuleCollider playerCapsule;
    LeoRig actorRig;
    LeoModelPresentation legacyPresentation;
    Speaker actorSpeaker;
    DialogueSequence sequence;
    Camera camera;
    StoryInspectionProp prop;
    Coroutine scene;
    Kind subject;
    Beat beat;
    Vector3 walkDirection;
    float beatStart, hold, expires, pickedAt;
    bool engraveBridge, accepted, releasing, actionArmed, knifeInHand, arrowTurned, pickedKnife, wasReacting, triedProp;
    AudioSource ambience;

    internal bool ShowBridgeOnMap => !superseded && bridgeEngraving != null && source is DialogueSequence knife && knife.InspectionSeen;

    public string MapTitle => kind switch
    {
        Kind.Knife => "Старый нож Гуннара",
        Kind.Ribbon => "Оборванная лента",
        Kind.Board => "Дощечка у скамьи",
        Kind.Finale => "Зарубка Гуннара",
        Kind.BenchMarks => "Зарубки на скамье",
        Kind.Witness => "Камень-свидетель",
        _ => source is GroveChopBlock grove ? grove.title : "Осмотрено"
    };

    internal static bool WasDiscovered(Transform target, string key)
    {
        if (DialogueSeen.Has("inspection/" + key)) return true;
        return target.TryGetComponent<StoryInspection>(out var found) && found.source is DialogueSequence seq &&
            !string.IsNullOrEmpty(seq.saveId) && DialogueSeen.Has(seq.saveId);
    }

    Vector3 Focus
    {
        get
        {
            if (engraveBridge && bridgeEngraving != null) return Centre(bridgeEngraving);
            if (subject == Kind.Knife && prop != null) return prop.RestGrip;
            return detail != null ? detail.position : transform.TransformPoint(detailOffset);
        }
    }

    static Vector3 Centre(Transform point)
    {
        var renderer = point.GetComponent<Renderer>();
        return renderer != null ? renderer.bounds.center : point.position;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        current = null;
        releasedFrame = -1;
        suppressHeldAction = false;
    }

    internal static bool IsSuperseded(MonoBehaviour interaction)
    {
        if (!interaction.TryGetComponent<StoryInspection>(out var binding) || !binding.superseded) return false;
        return binding.source == interaction || (binding.kind == Kind.Knife && interaction is MysticProp);
    }

    internal static StoryInspection For(MonoBehaviour interaction)
    {
        var found = interaction.GetComponent<StoryInspection>();
        if (found == null || !found.isActiveAndEnabled || found.superseded) return null;
        if (found.source == interaction) return found;
        return found.kind == Kind.Knife && interaction is MysticProp && found.bridgeEngraving != null ? found : null;
    }

    internal static bool AllowsReplay(DialogueSequence interaction)
    {
        var found = For(interaction);
        return found != null && found.source == interaction && (found.kind != Kind.Knife || found.bridgeEngraving == null);
    }

    internal static bool CanInspectEngraving(MysticProp interaction)
    {
        var found = For(interaction);
        if (found == null || found.source == interaction) return true;
        var knife = found.source as DialogueSequence;
        return knife == null || !knife.isActiveAndEnabled || knife.InspectionSeen;
    }

    void OnEnable()
    {
        SaveSystem.Loaded += OnProgressReset;
        SaveSystem.NewGameStarted += OnProgressReset;
    }

    void Start() => PrepareProp();

    void OnProgressReset()
    {
        Abort();
        if (source is DialogueSequence seq) seq.SyncInspectionSeen();
    }

    public bool HasMovableMesh() => StoryInspectionProp.CanSeparate(transform, kind);

    void PrepareProp()
    {
        if (superseded || triedProp || (kind != Kind.Knife && kind != Kind.CopperArrow && kind != Kind.Ribbon && kind != Kind.Board)) return;
        triedProp = true;
        prop = StoryInspectionProp.Create(transform, kind);
        if (prop == null) Debug.LogWarning($"Story Inspection: не удалось отделить визуальную деталь {kind}. Используется исходная модель.", this);
    }

    void OnDisable()
    {
        SaveSystem.Loaded -= OnProgressReset;
        SaveSystem.NewGameStarted -= OnProgressReset;
        Abort();
        prop?.Dispose();
        prop = null;
        triedProp = false;
    }

    internal void TryPlay(MonoBehaviour interaction, string line = null)
    {
        TryPlay(interaction, () => line);
    }

    internal void TryPlay(MonoBehaviour interaction, Func<string> line)
    {
        if (Active || DialogueManager.IsBusy || DialogueSequence.AnyPlaying || DialogueManager.BlocksUntilRelease ||
            SaveSystem.IsLoading || BuildingController.AnyRevealing || BuildingController.AnyRestorePending ||
            MobileInput.ModalOpen || WaterSwim.Swimming || Time.timeScale <= 0f) return;

        var actor = PlayerMovement.Instance;
        var cam = Camera.main;
        if (actor == null || actor.ControlLocked || actor.IsDashing || cam == null ||
            Mathf.Abs(actor.transform.position.y - transform.position.y) > 2.1f) return;
        var body = actor.GetComponent<Rigidbody>();
        if (body != null && Mathf.Abs(body.linearVelocity.y) > 1.1f) return;

        player = actor;
        playerCapsule = actor.GetComponent<CapsuleCollider>();
        Transform visual = actor.model != null ? actor.model : actor.transform;
        var controller = visual.GetComponentInChildren<LeoAnimationController>();
        legacyPresentation = visual.GetComponentInChildren<LeoModelPresentation>();
        if (controller != null && controller.isActiveAndEnabled && controller.rig != null)
        {
            actorRig = controller.rig;
            legacyPresentation = null;
        }
        else if (legacyPresentation != null && legacyPresentation.isActiveAndEnabled)
            actorRig = legacyPresentation.rig;
        actorSpeaker = actor.GetComponent<Speaker>();
        camera = cam;
        sequence = interaction as DialogueSequence;
        subject = kind;
        engraveBridge = kind == Kind.Knife && interaction != source;
        accepted = releasing = actionArmed = SkipRequested = knifeInHand = arrowTurned = pickedKnife = false;
        wasReacting = false;
        hold = 0f;
        suppressHeldAction = false;
        current = this;
        expires = Time.time + 24f;
        try
        {
            actor.CancelWalk();
            PrepareProp();
            if (kind == Kind.Knife) prop?.ResetPose();
            OpenCamera();
            scene = StartCoroutine(PlaySafely(line()));
        }
        catch (Exception error)
        {
            Debug.LogException(error, this);
            Abort();
        }
    }

    IEnumerator PlaySafely(string line)
    {
        var playback = Play(line);
        while (true)
        {
            Exception failure = null;
            bool more = false;
            try { more = playback.MoveNext(); }
            catch (Exception error) { failure = error; }
            if (failure != null)
            {
                scene = null;
                Debug.LogException(failure, this);
                Abort();
            }
            if (!more) yield break;
            yield return playback.Current;
        }
    }

    IEnumerator Play(string line)
    {
        SetBeat(Beat.Approach);
        bool reach = !engraveBridge && (subject == Kind.Knife || subject == Kind.Ribbon);
        if (reach && actorRig != null)
        {
            bool knife = subject == Kind.Knife;
            float stop = knife ? .36f : .48f;
            Vector3 initial = transform.InverseTransformPoint(player.transform.position);
            int detour = knife && bridgeEngraving != null && initial.z < .02f && Mathf.Abs(initial.x) < .52f ? 1 : 0;
            float side = initial.x > .12f ? .56f : -.56f;
            float approachTime = detour > 0 ? 2.25f : 1.55f;
            while (!SkipRequested && BeatTime < approachTime)
            {
                Vector3 position = player.transform.position;
                Vector3 goal = Focus;
                if (detour > 0)
                {
                    Vector3 local = transform.InverseTransformPoint(position);
                    if (detour == 1 && Mathf.Abs(local.x) >= .50f) detour = 2;
                    if (detour == 2 && local.z >= .27f) detour = 0;
                    if (detour > 0) goal = transform.TransformPoint(new Vector3(side, local.y, detour == 1 ? local.z : .33f));
                }
                Vector3 direction = goal - position;
                direction.y = 0f;
                if (direction.magnitude <= (detour == 0 ? stop : .04f) || !GroundAhead(direction.normalized)) break;
                walkDirection = direction.normalized * 1.40f;
                yield return null;
            }
            walkDirection = Vector3.zero;
        }

        SetBeat(Beat.Look);
        PlayAtmosphere();
        while (!SkipRequested && BeatTime < (reach ? .38f : .42f)) yield return null;

        if (reach && !SkipRequested)
        {
            SetBeat(Beat.Reach);
            while (!SkipRequested && BeatTime < .46f) yield return null;
        }

        SetBeat(Beat.Detail);
        if (sequence != null && sequence.isActiveAndEnabled)
        {
            sequence.PlayInspection(this);
            while (sequence != null && sequence.InspectionOwner == this)
            {
                if (sequence.lines.Length > 1 && DialogueSequence.ActiveLine >= 2 && beat != Beat.React)
                    SetBeat(Beat.React);
                yield return null;
            }
        }
        else if (!string.IsNullOrWhiteSpace(line) && SkipRequested)
        {
            DialogueLog.Add("Лео", line, true);
        }
        else if (!string.IsNullOrWhiteSpace(line))
        {
            bool done = false;
            float reading = !engraveBridge && subject == Kind.Finale ? 0f : Mathf.Clamp(1.35f + line.Length / 39f, 2.25f, 3.2f);
            DialogueManager.SayInspection(this, actorSpeaker, line, () => done = true, reading);
            while (!done && !SkipRequested)
            {
                if (beat == Beat.Detail && BeatTime > 1.25f && !engraveBridge &&
                    (subject == Kind.Duck || subject == Kind.CopperArrow || subject == Kind.Finale))
                    SetBeat(Beat.React);
                yield return null;
            }
        }

        accepted = true;
        Transform discovered = engraveBridge ? bridgeEngraving : transform;
        if (discovered != null)
        {
            var poi = discovered.GetComponent<MapPoi>();
            string id = MapPoi.GetInspectionId(discovered, poi != null ? poi.inspectionId : null);
            DialogueSeen.Mark("inspection/" + id);
            MapPoi.ReportInspection(discovered);
        }
        wasReacting = beat == Beat.React;
        releasePose = inspectionPose;
        SetBeat(Beat.Recover);
        releasing = true;
        while (BeatTime < (SkipRequested ? .46f : .72f)) yield return null;
        scene = null;
        Close();
    }

    float BeatTime => Time.time - beatStart;

    void SetBeat(Beat next)
    {
        beat = next;
        beatStart = Time.time;
    }

    void PlayAtmosphere()
    {
        if (atmosphere == null || engraveBridge) return;
        if (ambience == null)
        {
            ambience = gameObject.AddComponent<AudioSource>();
            ambience.playOnAwake = false;
            ambience.spatialBlend = 1f;
            ambience.rolloffMode = AudioRolloffMode.Linear;
            ambience.maxDistance = 7f;
        }
        ambience.volume = .18f * GameAudio.AmbientLevel;
        ambience.clip = atmosphere;
        ambience.Play();
    }

    void Update()
    {
        if (current != this) return;
        if (player == null || !player.isActiveAndEnabled || player.ControlLocked || camera == null ||
            !camera.isActiveAndEnabled || Time.time > expires || SaveSystem.IsLoading || BuildingController.AnyRevealing ||
            WaterSwim.Swimming || (source != null && !source.isActiveAndEnabled))
        {
            Abort();
            return;
        }
        if (Time.timeScale <= 0f) return;

        if (!MobileInput.ActionHeld)
        {
            actionArmed = true;
            hold = 0f;
        }
        else if (actionArmed && !MobileInput.ModalOpen && !releasing)
        {
            hold += Time.unscaledDeltaTime;
            if (hold >= .60f) RequestSkip();
        }

        if (walkDirection == Vector3.zero && !releasing && player.model != null)
        {
            Vector3 direction = Focus - player.transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > .008f)
                player.model.rotation = Quaternion.Slerp(player.model.rotation, Quaternion.LookRotation(direction), 1f - Mathf.Exp(-12f * Time.deltaTime));
        }
    }

    internal void RequestSkip()
    {
        if (current != this || releasing || SkipRequested) return;
        SkipRequested = true;
        DialogueManager.StopInspection(this);
    }

    void LateUpdate()
    {
        if (current != this || camera == null) return;
        if (prop != null && !engraveBridge)
        {
            if (subject == Kind.Knife) PlaceKnife();
            if (subject == Kind.CopperArrow && !arrowTurned && beat == Beat.React && BeatTime >= .43f)
            {
                prop.TurnArrow();
                arrowTurned = true;
            }
        }
        UpdateCamera();
    }

    void PlaceKnife()
    {
        if (actorRig == null || actorRig.handR == null) return;
        Transform hand = actorRig.handR;
        Vector3 grip = LeoAxePose.Grip(hand);
        if (!knifeInHand && !pickedKnife && (beat == Beat.Reach || beat == Beat.Detail) &&
            Vector3.Distance(grip, prop.RestGrip) < .085f)
        {
            knifeInHand = pickedKnife = true;
            pickedAt = Time.time;
        }
        if (!knifeInHand) return;
        if (beat == Beat.Recover && BeatTime > .26f)
        {
            prop.Release(Smooth((BeatTime - .26f) / .15f));
            if (BeatTime >= .41f) knifeInHand = false;
            return;
        }
        float attach = Smooth((Time.time - pickedAt) / .10f);
        prop.Hold(Vector3.Lerp(prop.RestGrip, grip, attach),
            Quaternion.Slerp(prop.RestRotation, LeoAxePose.ToolRotation(hand), attach));
    }

    void Abort()
    {
        if (current != this) return;
        if (scene != null) StopCoroutine(scene);
        scene = null;
        if (sequence != null) sequence.CancelInspection(this);
        Close();
    }

    void Close()
    {
        DialogueManager.StopInspection(this);
        if (ambience != null) ambience.Stop();
        if (prop != null && (subject == Kind.Knife || !accepted)) prop.ResetPose();
        RestoreCamera();
        current = null;
        releasedFrame = Time.frameCount;
        suppressHeldAction = MobileInput.ActionHeld;
        walkDirection = Vector3.zero;
        player = null;
        playerCapsule = null;
        actorRig = null;
        legacyPresentation = null;
        actorSpeaker = null;
        sequence = null;
        camera = null;
    }

    static float Smooth(float x) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(x));
}
