using System;
using TMPro;
using UnityEngine;

// Reads existing quests; owns E only for non-blocking reminders or optional chat.
[DefaultExecutionOrder(200), DisallowMultipleComponent]
public sealed class MiraPresence : MonoBehaviour, IInteractable, IPriorityInteractable
{
    [Serializable] public sealed class Book { public Line[] lines; }
    [Serializable] public sealed class Line { public string id, kind, text, condition; }
    public Speaker speaker;
    public DialogueSequence dialogue;
    public MiraMotion motion;
    public StoryFlow story;
    public IntroFlow intro;
    public PlayerThoughtController thoughts;
    public CanvasGroup group;
    public RectTransform card;
    public TMP_Text caption, body;
    public AudioSource voice;
    public DialogueBabble babble;
    public NpcMarker marker;
    public Sprite talkIcon, deliverIcon, reportIcon, chatIcon;
    public float radius=3f, greetRadius=4.5f;
    public bool approachComments=true;
    public bool Speaking { get { return current!=null; } }
    public bool Talking { get { return current!=null&&(typed<current.text.Length||(voice!=null&&voice.isPlaying)||(babble!=null&&babble.Speaking)); } }
    public Transform Anchor { get { return transform; } }
    public Vector3 PromptPoint { get { return speaker!=null?speaker.BubblePoint+Vector3.up*.5f:transform.position+Vector3.up*2.7f; } }
    public float Radius { get { return radius; } }
    public bool Priority { get { return true; } }
    public string Prompt { get { return mode==MiraInteractionRules.Mode.Free?"поговорить с Мирой":"уточнить цель у Миры"; } }
    public bool CanInteract
    {
        get
        {
            if(!isActiveAndEnabled||Blocked||Time.time<nextManual)return false;
            // Inventory/story may change between the 150 ms presentation polls.
            var supply=GetComponent<SupplyPoint>();
            if(supply!=null&&supply.isActiveAndEnabled&&supply.Accepting&&inventory!=null&&inventory.woodCount>0)return false;
            if(dialogue!=null&&dialogue.isActiveAndEnabled&&!dialogue.isQuestReminder&&dialogue.CanInteract)return false;
            return MiraInteractionRules.OwnsInteraction(mode);
        }
    }
    public MiraInteractionRules.Mode CurrentMode { get { return mode; } }
    readonly MiraInteractionRules.Approach approach=new MiraInteractionRules.Approach();
    Book book;
    PlayerInventory inventory;
    PlayerInteraction chopping;
    Transform player;
    Camera cam;
    Canvas canvas;
    MiraInteractionRules.Mode mode;
    Line current;
    string taskKey, statusText, lastLine;
    float nextPoll, nextManual, quietUntil, lineUntil, typed;
    int letters, delivered=-1, chatCounter;
    int freeTalkStreak; float lastFreeTalk = -999f; bool alternateAnnoyed;
    bool ready, automaticLine;
    DialogueSpeechMode speech;
    void Awake()
    {
        if(speaker==null)speaker=GetComponent<Speaker>();if(dialogue==null)dialogue=GetComponent<DialogueSequence>();
        if(intro==null)intro=IntroFlow.Instance;if(story==null)story=StoryFlow.Instance;
        var asset=Resources.Load<TextAsset>("MiraLife/MiraLines");
        if(asset!=null)try{book=JsonUtility.FromJson<Book>(asset.text);}catch(Exception e){Debug.LogWarning("MIRA: "+e.Message,this);}
        if(group==null||card==null||body==null||caption==null||book==null||speaker==null||dialogue==null)
        {Debug.LogWarning("MIRA: нет ссылок модели/карточки/реплик. Запусти сборщик Миры.",this);enabled=false;return;}
        canvas=card.GetComponentInParent<Canvas>();group.alpha=0;group.blocksRaycasts=false;group.interactable=false;
        approach.Reset(Time.time);quietUntil=Time.time+12f;
        if(voice!=null){voice.playOnAwake=false;voice.spatialBlend=0;}
        ready=true;
    }
    void OnEnable()
    {
        Interactables.Register(this);
        if(dialogue==null)dialogue=GetComponent<DialogueSequence>();
        if(dialogue!=null)dialogue.reminderHandledExternally=true;
        SaveSystem.Loaded+=OnLoaded;
    }
    void OnDisable()
    {
        Interactables.Unregister(this);
        SaveSystem.Loaded-=OnLoaded;
        if(dialogue!=null)dialogue.reminderHandledExternally=false;
        StopLine();if(group!=null)group.alpha=0;
    }
    void OnLoaded(){delivered=-1;taskKey=null;StopLine();approach.Reset(Time.time);quietUntil=Time.time+12;nextPoll=0;}
    bool Blocked
    {
        get{return !Application.isFocused||Time.timeScale<=0||SaveSystem.IsLoading||MobileInput.ModalOpen||DialogueManager.IsBusy||DialogueSequence.AnyPlaying||BuildingController.AnyRevealing||BuildingController.AnyRestorePending||
            (chopping!=null&&chopping.IsChopping)||(thoughts!=null&&thoughts.group!=null&&thoughts.group.alpha>.01f);}
    }
    public void Interact()
    {
        Refresh();if(!CanInteract)return;nextManual=Time.time+2.2f;
        var line=mode==MiraInteractionRules.Mode.Free?NextChat():Find(taskKey);
        if(line!=null)Show(line);
    }
    void LateUpdate()
    {
        if(!ready)return;
        // Legacy StoryFlow may clone Mira and rename the clone to Oskar after Awake.
        if(!string.Equals(speaker.displayName,"Мира",StringComparison.OrdinalIgnoreCase)){enabled=false;return;}
        if(player==null)
        {
            var pm=PlayerMovement.Instance;if(pm==null)return;player=pm.transform;inventory=pm.GetComponent<PlayerInventory>();chopping=pm.GetComponent<PlayerInteraction>();
        }
        if(story==null)story=StoryFlow.Instance;if(intro==null)intro=IntroFlow.Instance;
        float now=Time.time,dt=Mathf.Min(.1f,Time.unscaledDeltaTime);
        if(now>=nextPoll){nextPoll=now+.15f;Refresh();}
        Vector3 delta=player.position-transform.position;delta.y=0;float distance=delta.magnitude;
        bool blocked=Blocked;
        bool entered=approach.Step(distance,greetRadius,now,!blocked&&now>=quietUntil);
        if(blocked)
        {
            StopLine();quietUntil=now+3f;if(group!=null)group.alpha=0;
        }
        else
        {
            if(current!=null&&(!Eligible(current)||now>=lineUntil||distance>greetRadius+1.8f||
                (automaticLine&&(!DialoguePreferences.PlayerComments||(current.kind=="task"&&!VibeSettings.GuideEnabled)))))StopLine();
            if(entered&&approachComments&&DialoguePreferences.PlayerComments&&current==null)
            {
                if(story!=null&&story.bridge!=null&&story.bridge.IsFinished&&!DialogueSeen.Has("mira_bridge_voice_once")){var greeting=Find("greet_bridge_done_new");if(greeting!=null){Show(greeting,true);DialogueSeen.Mark("mira_bridge_voice_once");}}
                else if(mode==MiraInteractionRules.Mode.Free){var greeting=NextGreeting();if(greeting!=null)Show(greeting,true);}
                else if(mode==MiraInteractionRules.Mode.Reminder&&VibeSettings.GuideEnabled){var reminder=Find(taskKey);if(reminder!=null)Show(reminder,true);}
            }
            if(current!=null)TickSpeech(dt);
            bool status=mode==MiraInteractionRules.Mode.Deliver||(mode==MiraInteractionRules.Mode.Reminder&&VibeSettings.GuideEnabled);
            bool visible=distance<=greetRadius&&(current!=null||status)&&PlaceCard();
            group.alpha=Mathf.MoveTowards(group.alpha,visible?1:0,dt/.18f);
            if(current==null&&status){caption.text="Мира · "+(mode==MiraInteractionRules.Mode.Deliver?"Припасы":"Текущая цель");body.text=statusText;body.maxVisibleCharacters=int.MaxValue;}
        }
        UpdateMarker(blocked);
    }
    void Refresh()
    {
        var supply=GetComponent<SupplyPoint>();bool pending=supply!=null&&supply.isActiveAndEnabled&&supply.Accepting;
        bool introTask=intro!=null&&intro.Current!=IntroFlow.Step.Done;
        bool task=introTask||(story!=null&&!story.AllDone);
        bool main=dialogue!=null&&dialogue.isActiveAndEnabled&&!dialogue.isQuestReminder&&dialogue.CanInteract;
        mode=MiraInteractionRules.Resolve(Blocked,pending,inventory!=null&&inventory.woodCount>0,main,story!=null&&story.CurrentChapterPhase=="Report",task);
        taskKey=TaskKey();var line=Find(taskKey);statusText=line!=null?line.text:"Загляни в список заданий — там следующий шаг.";
        if(pending)
        {
            statusText="Сдано "+supply.delivered+"/"+supply.need+" брёвен · в рюкзаке "+(inventory!=null?inventory.woodCount:0)+"\n"+(inventory!=null&&inventory.woodCount>0?"Нажми E, чтобы передать брёвна.":"Наруби ещё и принеси сюда.");
            if(delivered>=0&&supply.delivered>delivered){if(motion!=null)motion.Thank();if(!Blocked){var thanks=Find("thanks");if(thanks!=null)Show(thanks);}}
        }
        // Also detect the final delivery after Accepting turns false; never react on load.
        else if(supply!=null&&delivered>=0&&supply.delivered>delivered){if(motion!=null)motion.Thank();}
        delivered=supply!=null?supply.delivered:0;
    }
    string TaskKey()
    {
        if(intro!=null&&intro.Current!=IntroFlow.Step.Done)
        {
            switch(intro.Current)
            {
                case IntroFlow.Step.Chop:return "task_chop";
                case IntroFlow.Step.Campfire:return "task_fire";
                case IntroFlow.Step.ReturnToMira:case IntroFlow.Step.Talk2:return "task_return";
                default:return "task_meet";
            }
        }
        if(story==null||story.AllDone)return null;
        if(!story.TaskAccepted)return story.CurrentChapterPhase=="Enter"?"task_level":"task_talk";
        switch(story.CurrentChapterId)
        {
            case "supply":return inventory!=null&&inventory.woodCount>0?"task_supply_carry":"task_supply_empty";
            case "upgrade":return "task_upgrade";
            case "hut":return "task_hut";
            case "planks":return "task_planks";
            case "market":return "task_market";
            case "firstSale":return "task_sale";
            case "tent":return "task_tent";
            case "pets":return "task_pet";
            case "bridge":return "task_bridge";
            case "gunnar":return "task_note";
            default:return "task_talk";
        }
    }
    Line Find(string id){if(book==null||book.lines==null||string.IsNullOrEmpty(id))return null;foreach(var line in book.lines)if(line!=null&&line.id==id)return line;return null;}
    bool Eligible(Line line)
    {
        if(line.kind=="task")return line.id==taskKey;
        if(line.kind=="thanks")return mode==MiraInteractionRules.Mode.Deliver||mode==MiraInteractionRules.Mode.Reminder;
        if(mode!=MiraInteractionRules.Mode.Free)return false;
        bool night=DayNightCycle.Night01>.5f;
        return line.condition=="night"?night:line.condition=="day"?!night:true;
    }
    Line NextGreeting()
    {
        if(book==null||book.lines==null)return null;
        for(int i=0;i<book.lines.Length;i++){int n=(chatCounter+i)%book.lines.Length;var line=book.lines[n];if(line.kind=="greet"&&line.id!=lastLine&&Eligible(line)){chatCounter=n+1;return line;}}
        return null;
    }
    Line NextChat()
    {
        if(book==null||book.lines==null)return null;
        for(int i=0;i<book.lines.Length;i++)
        {
            int n=(chatCounter+i)%book.lines.Length;var line=book.lines[n];
            if(line.kind!="chat"||line.id==lastLine||!Eligible(line))continue;
            if(line.condition=="once"&&DialogueSeen.Has("mira_life_v1:"+line.id))continue;
            chatCounter=n+1;return line;
        }
        return Find("chat_flower");
    }
    void Show(Line line,bool automatic=false)
    {
        if(line==null||string.IsNullOrEmpty(line.text)||Blocked)return;
        StopLine();current=line;automaticLine=automatic;lastLine=line.id;typed=letters=0;
        caption.text=line.kind=="task"?"Мира · По делу":"Мира";body.text=line.text;body.maxVisibleCharacters=0;
        float duration=Mathf.Clamp(1.8f+line.text.Length/23f,3.5f,6.5f);
        var clip=GameAudio.UseRecordedVoices?VoiceBank.Find(speaker.displayName,line.text):null;
        if(voice!=null){voice.clip=clip;if(clip!=null&&GameAudio.VoiceLevel>.001f){voice.volume=GameAudio.VoiceLevel*.65f;voice.Play();duration=Mathf.Max(duration,clip.length+.3f);}}
        if(babble!=null)babble.BeginLine(speaker.displayName,clip!=null);
        speech=GameAudio.Speech;lineUntil=Time.time+duration;
        if(line.condition=="once")DialogueSeen.Mark("mira_life_v1:"+line.id);
        DialogueLog.Add(speaker.displayName,line.text,false);
        if(motion!=null&&line.kind!="thanks")motion.Wave();
    }
    void TickSpeech(float dt)
    {
        if(GameAudio.Speech!=speech){if(voice!=null)voice.Stop();if(babble!=null)babble.BeginLine(speaker.displayName,false);speech=GameAudio.Speech;}
        if(voice!=null&&voice.isPlaying){if(!GameAudio.UseRecordedVoices||GameAudio.VoiceLevel<=.001f)voice.Stop();else voice.volume=GameAudio.VoiceLevel*.65f;}
        typed+=45f*dt;int count=Mathf.Min(current.text.Length,(int)typed);
        while(letters<count){if((voice==null||voice.clip==null||!GameAudio.UseRecordedVoices)&&babble!=null)babble.Letter(current.text,letters);letters++;}
        body.maxVisibleCharacters=count;
    }
    void StopLine(){current=null;if(voice!=null)voice.Stop();if(babble!=null)babble.StopNow();}
    bool PlaceCard()
    {
        if(cam==null)cam=Camera.main;if(cam==null||canvas==null)return false;
        Vector3 screen=cam.WorldToScreenPoint(speaker.BubblePoint);if(screen.z<=0)return false;
        Rect safe=Screen.safeArea;if(!safe.Contains(new Vector2(screen.x,screen.y)))return false;
        Vector2 position;var root=canvas.transform as RectTransform;
        if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(root,screen,null,out position))return false;
        position+=new Vector2(-205,65);Rect bounds=root.rect;
        float factor=canvas.scaleFactor;
        float left=bounds.xMin+safe.xMin/factor,right=bounds.xMin+safe.xMax/factor;
        float bottom=bounds.yMin+safe.yMin/factor,top=bounds.yMin+safe.yMax/factor;
        position.x=Mathf.Clamp(position.x,left+card.rect.width*.5f+12,right-card.rect.width*.5f-12);
        position.y=Mathf.Clamp(position.y,bottom+card.rect.height*.5f+12,top-card.rect.height*.5f-12);
        card.anchoredPosition=position;return true;
    }
    void UpdateMarker(bool blocked)
    {
        if(marker==null)return;
        bool actionable=mode==MiraInteractionRules.Mode.Talk||mode==MiraInteractionRules.Mode.Report||mode==MiraInteractionRules.Mode.Deliver;
        marker.SetVisible(!blocked&&(actionable||mode==MiraInteractionRules.Mode.Free));
        var renderer=marker.GetComponent<SpriteRenderer>();if(renderer==null)return;
        Sprite icon=mode==MiraInteractionRules.Mode.Deliver?deliverIcon:mode==MiraInteractionRules.Mode.Report?reportIcon:mode==MiraInteractionRules.Mode.Free?chatIcon:talkIcon;
        if(icon!=null)renderer.sprite=icon;
    }
}
