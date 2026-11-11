using System;
using TMPro;
using UnityEngine;

// Observes existing state only. Never owns input, moves a camera, or modifies a quest/resource.
[DefaultExecutionOrder(100)]
public sealed class PlayerThoughtController : MonoBehaviour
{
    [Serializable] public class Book { public Line[] lines; }
    [Serializable] public class Line { public string id, text, kind, trigger; public int priority = 2; }
    public StoryFlow story;
    public IntroFlow intro;
    public Transform mysticAnchor;
    public float mysticRadius = 14f;
    public CanvasGroup group;
    public TMP_Text body, caption;
    public AudioSource voice;
    public DialogueBabble babble;
    readonly PlayerThoughtGate gate = new PlayerThoughtGate();
    Book book;
    PlayerInventory inventory;
    PlayerStats stats;
    PlayerInteraction interaction;
    Transform player;
    Line current, pending;
    float nextPoll, pendingUntil, pendingAfter, elapsed, visibleTime, typed;
    int blips, lastWood, lastPlanks, lastXp, lastSupply, lastStages, lastBridgeStages, initialPlanks;
    bool initialized, wasFull, soldPending;
    Transform distanceTarget;
    float bestDistance = float.MaxValue;
    string stage;
    DialogueSpeechMode lastMode;
    static PlayerThoughtController instance;
    public static void CancelActive() { if (instance != null) { instance.Cancel(); if (instance.group != null) instance.group.alpha = 0; } }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { instance = null; }
    void Awake() { instance = this; }
    void Start()
    {
        if (group == null || body == null || caption == null) { Debug.LogWarning("Player thoughts: нет ссылок карточки. Повтори сборщик диалогов."); enabled = false; return; }
        var data = Resources.Load<TextAsset>("DialoguePolish/PlayerReactions");
        if (data != null) { try { book = JsonUtility.FromJson<Book>(data.text); } catch (Exception ex) { Debug.LogWarning("Player thoughts: " + ex.Message); } }
        ConfigureCompactCard();
        gate.Reset(Time.unscaledTime); if (group != null) { group.alpha = 0; group.blocksRaycasts = false; group.interactable = false; }
        if (voice != null) { voice.playOnAwake = false; voice.spatialBlend = 0; }
    }
    // Short caption below the action, not a large dialogue bubble over Leo's head.
    void ConfigureCompactCard()
    {
        if (group == null) return;
        var rect = group.transform as RectTransform;
        if (rect != null)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0f);
            rect.pivot = new Vector2(.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 148f);
            rect.sizeDelta = new Vector2(650f, 122f);
        }
        if (body != null) body.fontSize = 24f;
        if (caption != null) caption.fontSize = 17f;
    }

    void OnEnable() { instance = this; TradingStall.Sold += OnSold; SaveSystem.Loaded += OnLoaded; }
    void OnDisable() { TradingStall.Sold -= OnSold; SaveSystem.Loaded -= OnLoaded; Cancel(); if (group != null) group.alpha = 0; if (instance == this) instance = null; }
    void OnLoaded() { initialized = false; pending = null; Cancel(); gate.Reset(Time.unscaledTime); }
    void OnSold() { soldPending = true; }
    bool Blocked
    {
        get { return !Application.isFocused || SaveSystem.IsLoading || MobileInput.ModalOpen || DialogueManager.IsBusy || DialogueSequence.AnyPlaying ||
            BuildingController.AnyRevealing || BuildingController.AnyRestorePending || (interaction != null && interaction.IsChopping); }
    }
    void LateUpdate()
    {
        if (player == null)
        {
            var movement = PlayerMovement.Instance; if (movement == null) return;
            player = movement.transform; inventory = movement.GetComponent<PlayerInventory>(); stats = movement.GetComponent<PlayerStats>(); interaction = movement.GetComponent<PlayerInteraction>();
        }
        float now = Time.unscaledTime, dt = Mathf.Min(.1f, Time.unscaledDeltaTime);
        if (Input.anyKey || Mathf.Abs(Input.GetAxisRaw("Mouse X")) + Mathf.Abs(Input.GetAxisRaw("Mouse Y")) > .01f) gate.Input(now);
        if (Blocked)
        {
            Cancel(); if (group != null) group.alpha = 0; pending = null;
            if (interaction != null && interaction.IsChopping && !MobileInput.ModalOpen && !DialogueManager.IsBusy && !DialogueSequence.AnyPlaying) gate.Progress(now);
            else gate.Blocked(now);
            nextPoll = now + .5f; return;
        }
        if (current != null && (current.kind == "hint" ? !VibeSettings.GuideEnabled : !DialoguePreferences.PlayerComments)) Cancel();
        if (now >= nextPoll) { nextPoll = now + .5f; Poll(now); }
        if (current == null && pending != null)
        {
            bool hint = pending.kind == "hint";
            if (now > pendingUntil || !StillRelevant(pending.id) || (hint ? !VibeSettings.GuideEnabled : !DialoguePreferences.PlayerComments)) pending = null;
            else if (now >= pendingAfter && gate.CanShow(now,hint)) { var line = pending; pending = null; Show(line,now); }
        }
        bool show = current != null;
        if (group != null) group.alpha = Mathf.MoveTowards(group.alpha, show ? 1f : 0f, dt / .2f);
        if (!show) return;
        elapsed += dt;
        if (GameAudio.Speech != lastMode)
        {
            if (voice != null) voice.Stop(); if (babble != null) babble.BeginLine("Лео",false); lastMode = GameAudio.Speech;
        }
        if (voice != null && voice.isPlaying) { if (!GameAudio.UseRecordedVoices || GameAudio.VoiceLevel <= .001f) voice.Stop(); else voice.volume = GameAudio.VoiceLevel * .75f; }
        typed += 48f * dt; int visible = Mathf.Min(current.text.Length,(int)typed);
        while (blips < visible) { if ((voice == null || voice.clip == null || !GameAudio.UseRecordedVoices) && babble != null) babble.Letter(current.text,blips); blips++; }
        body.maxVisibleCharacters = visible;
        if (elapsed >= visibleTime) Cancel();
    }
    int StageCount()
    {
        if (story == null) return 0;
        return (story.hut != null ? story.hut.StepsDone : 0) + (story.market != null ? story.market.StepsDone : 0) +
            (story.tent != null ? story.tent.StepsDone : 0) + (story.bridge != null ? story.bridge.StepsDone : 0);
    }
    void Poll(float now)
    {
        if (inventory == null || book == null || book.lines == null) return;
        string nextStage = intro != null && intro.Current != IntroFlow.Step.Done ? "intro_" + intro.Current : story != null ? story.CurrentChapterId + "_" + story.CurrentChapterPhase : "";
        if (stage != nextStage) { stage = nextStage; pending = null; distanceTarget = null; bestDistance = float.MaxValue; gate.Stage(stage,now); }
        int xp = stats != null ? stats.currentXP : 0, supply = story != null ? story.SupplyDelivered : 0, stages = StageCount();
        int made = story != null && story.workshop != null ? story.workshop.totalCollected : 0;
        bool full = inventory.FreeSlots <= 0 && inventory.woodCount > 0;
        bool stock = false; var workers = WorkerManager.Instance;
        if (workers != null && workers.Sim != null && workers.WorkingCount > 0)
            stock = (workers.LogCap > 0 && workers.Logs >= workers.LogCap) || (workers.PlankCap > 0 && workers.Planks >= workers.PlankCap);
        if (!initialized)
        {
            initialized = true; lastWood = inventory.woodCount; lastPlanks = inventory.planksCount; lastXp = xp; lastSupply = supply; lastStages = stages;
            initialPlanks = made; lastBridgeStages = story != null && story.bridge != null ? story.bridge.StepsDone : 0; wasFull = full; soldPending = false; return;
        }
        if (lastWood != inventory.woodCount || lastPlanks != inventory.planksCount || lastXp != xp || lastSupply != supply || lastStages != stages) gate.Progress(now);
        lastWood = inventory.woodCount; lastPlanks = inventory.planksCount; lastXp = xp; lastStages = stages;
        var target = QuestEdgeArrow.Target;
        if (target != null)
        {
            float d = Vector3.Distance(player.position,target.position);
            if (distanceTarget != target) { distanceTarget = target; bestDistance = d; }
            else if (d < bestDistance - 1.5f) { bestDistance = d; gate.Progress(now); }
        }
        int bridgeStages = story != null && story.bridge != null ? story.bridge.StepsDone : 0;
        if (bridgeStages > lastBridgeStages && story != null && story.bridge != null && !story.bridge.IsFinished)
            Queue(bridgeStages == 1 ? "bridge_first_stage" : "bridge_last_stage", now);
        lastBridgeStages = bridgeStages;
        if (full && !wasFull) Queue("bag_full",now); wasFull = full;
        if (supply > lastSupply && supply > 0 && supply < GameBalance.SupplyLogs) Queue("supply_half",now); lastSupply = supply;
        if (made > initialPlanks && initialPlanks == 0) { Queue("first_plank",now); initialPlanks = made; }
        if (soldPending) { Queue("first_sale",now); soldPending = false; }
        if (stock && Near(story != null && story.tent != null ? story.tent.transform : null,10f)) Queue("stock_full",now);
        if (Near(mysticAnchor,mysticRadius))
        {
            bool night = DayNightCycle.Night01 > .5f;
            Queue(night ? "mystic_night" : "mystic_first",now);
            if (Near(mysticAnchor,7f) && (night || KnowsOldTree)) Queue(night ? "tree_night" : "old_tree",now);
        }
        if (story != null && story.bridge != null && story.bridge.IsFinished && Near(story.bridge.transform,10f)) Queue("bridge_ready",now);
        if (gate.CanShow(now,true) && VibeSettings.GuideEnabled) Queue(HintId(),now);
    }
    bool Near(Transform point,float radius) { if (point == null || player == null) return false; Vector3 d=point.position-player.position;d.y=0;return d.sqrMagnitude<=radius*radius; }
    bool KnowsOldTree { get { return story != null && (story.AllDone || story.CurrentChapterId == "gunnar" || (story.CurrentChapterId == "bridge" && story.TaskAccepted)); } }
    string HintId()
    {
        if (intro != null && intro.Current != IntroFlow.Step.Done)
        {
            switch (intro.Current)
            {
                case IntroFlow.Step.Move: case IntroFlow.Step.GoToMira: case IntroFlow.Step.Talk: return "help_mira";
                case IntroFlow.Step.Chop: return "help_chop";
                case IntroFlow.Step.Campfire: return "help_fire";
                case IntroFlow.Step.ReturnToMira: case IntroFlow.Step.Talk2: return "help_return";
            }
        }
        if (story == null || story.AllDone) return null;
        if (!story.TaskAccepted)
        {
            if (story.CurrentChapterPhase == "Enter") return inventory.FreeSlots <= 0 ? "help_pack" : "help_level";
            return "help_talk";
        }
        switch (story.CurrentChapterId)
        {
            case "supply": return inventory.woodCount > 0 ? "help_supply_carry" : "help_supply_empty";
            case "upgrade": return "help_upgrade";
            case "planks": return "help_planks";
            case "firstSale": return "help_sale";
            case "pets": return "help_pet";
            case "bridge": return "help_bridge";
            case "gunnar": return "help_note";
            case "hut": case "market": case "tent": return "help_build";
            default: return null;
        }
    }
    bool StillRelevant(string id)
    {
        if (id.StartsWith("help_",StringComparison.Ordinal)) return id == HintId();
        switch (id)
        {
            case "bag_full": return inventory != null && inventory.FreeSlots <= 0;
            case "supply_half": return story != null && story.CurrentChapterId == "supply" && story.SupplyDelivered > 0 && story.SupplyDelivered < GameBalance.SupplyLogs;
            case "stock_full": var w=WorkerManager.Instance;return w!=null&&w.Sim!=null&&((w.LogCap>0&&w.Logs>=w.LogCap)||(w.PlankCap>0&&w.Planks>=w.PlankCap))&&Near(story!=null&&story.tent!=null?story.tent.transform:null,10f);
            case "mystic_first": return Near(mysticAnchor,mysticRadius) && DayNightCycle.Night01 <= .5f;
            case "old_tree": return KnowsOldTree && Near(mysticAnchor,7f) && DayNightCycle.Night01 <= .5f;
            case "mystic_night": case "tree_night": return Near(mysticAnchor,mysticRadius) && DayNightCycle.Night01 > .5f;
            case "bridge_ready": return story!=null&&story.bridge!=null&&story.bridge.IsFinished&&Near(story.bridge.transform,10f);
            default: return true;
        }
    }
    void Queue(string id,float now)
    {
        if (string.IsNullOrEmpty(id) || book == null || current != null) return;
        foreach (var line in book.lines) if (line.id == id)
        {
            bool hint=line.kind=="hint";
            if (hint ? !VibeSettings.GuideEnabled : !DialoguePreferences.PlayerComments || DialogueSeen.Has("leo_thought_v1:"+id)) return;
            if (pending != null && pending.priority <= line.priority) return;
            pending=line;pendingAfter=now+1.2f;pendingUntil=now+30f;return;
        }
    }
    void Show(Line line,float now)
    {
        current=line;elapsed=typed=0;blips=0;body.text=line.text;body.maxVisibleCharacters=0;
        caption.text=line.kind=="hint"?"Лео · мысль о задаче":"Лео · про себя";
        visibleTime=Mathf.Clamp(1.7f+line.text.Length/22f,3.6f,6.5f);lastMode=GameAudio.Speech;
        var clip=GameAudio.UseRecordedVoices?VoiceBank.Find("Лео",line.text):null;
        if(voice!=null){voice.Stop();voice.clip=clip;if(clip!=null&&GameAudio.VoiceLevel>.001f){voice.volume=GameAudio.VoiceLevel*.75f;voice.Play();visibleTime=Mathf.Max(visibleTime,clip.length+.4f);}}
        if(babble!=null)babble.BeginLine("Лео",clip!=null);
        gate.Shown(now,line.kind=="hint");
        if(line.kind!="hint")DialogueSeen.Mark("leo_thought_v1:"+line.id);
    }
    void Cancel() { current=null;if(voice!=null)voice.Stop();if(babble!=null)babble.StopNow(); }
}
