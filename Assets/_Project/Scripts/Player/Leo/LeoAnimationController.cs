using UnityEngine;

// The only writer of Leo's new rig. All gameplay systems remain read-only inputs.
[DefaultExecutionOrder(300),DisallowMultipleComponent]
public sealed partial class LeoAnimationController : MonoBehaviour
{
    public LeoRig rig;
    public PlayerMovement player;
    public PlayerInteraction interaction;
    public BackpackTag backpack;
    public Speaker speaker;
    public DialogueManager dialogue;
    [Header("Стопы")]
    public bool plantFeet=true,adaptToGround=true;
    public LayerMask groundMask=~0;
    [Header("Вид")]
    public bool blink=true,carryVisuals=true;
    public TrailRenderer axeTrail;
    public string CurrentState{get;private set;}
    readonly LeoImpactClock impact=new LeoImpactClock();
    readonly LeoLegacyTiming legacy=new LeoLegacyTiming();
    readonly LeoMotionProfile.Gait gait=new LeoMotionProfile.Gait();
    readonly LeoAirWaterMotion airWater = new LeoAirWaterMotion();
    LeoMotionProfile.Book book;
    Vector3 previousPosition;
    Quaternion previousRotation;
    float phase=.25f,move,run,speedSmooth,actionWeight,holster,turn,talk,nextBlink,blinkAt=-10,posePhase,cancelPhase;
    float loadPitch,loadRoll,loadPitchVel,loadRollVel,adjustStart=-10,adjustWeight,fullWeight;
    int lastCarried=-1;bool pendingAdjust;
    Vector3 previousVelocity,smoothAcceleration;
    int hitFrame=-1,quality=1;
    Transform target;
    Vector3? contactPoint;
    bool initialized,trailWasOn,wasChopping,cancelling;
    public bool Initialize()
    {
        if(initialized)return true;if(rig==null)rig=GetComponent<LeoRig>();
        if(rig==null||rig.handR==null||rig.handL==null||rig.ankleR==null||rig.ankleL==null)return false;
        book=LeoMotionProfile.Load();if(book==null||book.version!=1||book.walk==null||book.run==null||book.chop==null||book.chop.Length<2)return false;
        if(player==null)player=GetComponentInParent<PlayerMovement>();
        if(player!=null){if(interaction==null)interaction=player.GetComponent<PlayerInteraction>();if(backpack==null)backpack=player.GetComponent<BackpackTag>();if(speaker==null)speaker=player.GetComponent<Speaker>();}
        if(dialogue==null)dialogue=FindFirstObjectByType<DialogueManager>();
        rig.PrepareAnimationRest();
        feet = new LeoFootPlacement(rig, player != null ? player.transform : transform);
        initialized=true;ResetState();return true;
    }
    void Awake(){if(!Initialize()){Debug.LogError("LEO ANIMATION: нет полного рига или Motion.json.",this);enabled=false;}}
    void OnEnable(){GameEvents.ChopHit+=Hit;GameEvents.TreeChopped+=Felled;GameEvents.ChopQuality+=Quality;SaveSystem.Loaded+=ResetState;SaveSystem.NewGameStarted+=ResetState;}
    void OnDisable()
    {
        GameEvents.ChopHit-=Hit;GameEvents.TreeChopped-=Felled;GameEvents.ChopQuality-=Quality;SaveSystem.Loaded-=ResetState;SaveSystem.NewGameStarted-=ResetState;
        airWater.Reset();
        if(rig!=null&&rig.HasRest){rig.ResetPose();rig.EquipHammer(false);if(backpack!=null)rig.ShowCargo(backpack.Carried,backpack.Capacity);}
        if(axeTrail!=null){axeTrail.emitting=false;axeTrail.Clear();}
    }
    void ResetState()
    {
        impact.Clear();legacy.Clear();phase=.25f;move=run=speedSmooth=actionWeight=holster=turn=talk=posePhase=cancelPhase=0;loadPitch=loadRoll=loadPitchVel=loadRollVel=adjustWeight=fullWeight=0;adjustStart=-10;lastCarried=-1;pendingAdjust=false;previousVelocity=smoothAcceleration=Vector3.zero;hitFrame=-1;target=null;contactPoint=null;wasChopping=cancelling=false;
        previousPosition=transform.position;previousRotation=transform.rotation;nextBlink=Time.time+3;blinkAt=-10;feet?.Reset();CurrentState="Idle";
        airWater.Reset();
        if(rig!=null&&rig.HasRest)rig.ResetPose();if(axeTrail!=null){axeTrail.emitting=false;axeTrail.Clear();}trailWasOn=false;
    }
    void Felled() { if (hitFrame != Time.frameCount) Hit(); }
    void Hit()
    {
        if (interaction == null || !interaction.IsChopping) return;
        impact.Hit(Time.time);
        hitFrame = Time.frameCount;
        quality = 1;
        contactPoint = interaction.TryReadLeoImpact(out Vector3 point) ? point : (Vector3?)null;
    }
    void Quality(int value){if(Time.time-impact.LastImpact<.1f)quality=Mathf.Clamp(value,0,2);}
    bool Speaking()
    {
        if(speaker==null)return false;if(speaker.IsSpeaking)return true;
        if(dialogue==null||dialogue.presentation==null||dialogue.presentation.speakerName==null||dialogue.presentation.HistoryOpen)return false;
        if(!DialogueManager.IsBusy||!string.Equals(dialogue.presentation.speakerName.text,speaker.displayName,System.StringComparison.OrdinalIgnoreCase))return false;
        var body=dialogue.presentation.body;
        return dialogue.babble!=null&&dialogue.babble.Speaking||body!=null&&body.maxVisibleCharacters<body.textInfo.characterCount;
    }
    void LateUpdate()
    {
        if(!Initialize()||Time.deltaTime<=0)return;
        float dt=Mathf.Min(.1f,Time.deltaTime),now=Time.time;Vector3 delta=transform.position-previousPosition;delta.y=0;
        previousPosition=transform.position;float size=Mathf.Max(.01f,transform.lossyScale.y);
        bool teleport=delta.magnitude>Mathf.Max(.8f,size*1.2f);
        if(teleport){ResetState();delta=Vector3.zero;}
        // Cadence is measured in world metres/second. Dividing by visual scale
        // made the 0.5-size Leo take twice as many tiny steps at the same speed.
        float travel=teleport?0:delta.magnitude;
        float speed=teleport?0:delta.magnitude/Mathf.Max(.001f,Time.deltaTime);
        float k=1-Mathf.Exp(-12*dt);speedSmooth=Mathf.Lerp(speedSmooth,Mathf.Min(speed,18),k);
        bool conversation=DialogueSequence.FreezesPlayer;
        bool inspecting = StoryInspection.ReadPose(this, out var inspectionPose);
        LeoAirWaterMotion.Frame motion = airWater.Read(player, dt);
        bool blocked=SaveSystem.IsLoading||BuildingController.AnyRevealing||BuildingController.AnyRestorePending||motion.InMotion||WaterSwim.Swimming||(player!=null&&player.ControlLocked&&!conversation);
        bool chopping=interaction!=null&&interaction.IsChopping&&!blocked&&!conversation&&!StoryInspection.Active;
        var current=chopping?interaction.CurrentTarget:null;
        if(current!=target&&chopping){if(hitFrame!=Time.frameCount)impact.Clear();target=current;}
        if(chopping)
        {
            float due=interaction.AnimationHasScheduledImpact?interaction.AnimationScheduledImpact:!PlayerInteraction.RhythmChop&&interaction.useAnimationEvents?legacy.Next(interaction.animator,now):-1;
            if(due>=0)impact.Forecast(now,due,book.windup);else impact.ForgetForecast();
        }
        else impact.ForgetForecast();
        bool recovering=impact.Recovering(now,book.recovery);
        if (chopping && !recovering)
            contactPoint = LeoChopContact.TryPoint(interaction.TargetItem, transform, out Vector3 point) ? point : (Vector3?)null;
        bool wantAction=!blocked&&!conversation&&!StoryInspection.Active&&(chopping||recovering);
        actionWeight=motion.InMotion ? 0f : Mathf.MoveTowards(actionWeight,wantAction?1:0,dt*(wantAction?12:8));
        if (wantAction && hitFrame == Time.frameCount) actionWeight = 1f;
        float desiredMove=!blocked&&!conversation&&!chopping?LeoMotionProfile.Smooth(speedSmooth/.65f):0;
        move=Mathf.MoveTowards(move,desiredMove,dt*(desiredMove>move?7.5f:5.5f));
        run=Mathf.Lerp(run,LeoMotionProfile.RunBlend(speedSmooth),k);
        LeoMotionProfile.MixInto(gait,book.walk,book.run,run);
        float carry=backpack!=null&&backpack.Capacity>0?Mathf.Clamp01((float)backpack.Carried/backpack.Capacity):0;
        if(move>.001f&&!blocked)
        {
            // Distance-driven phase: one full cycle = gait.stride * scale metres of ground. Feet therefore stay
            // locked to the ground by construction (also while speeding up / slowing down). Cadence is capped.
            float cycleMetres=Mathf.Max(.2f,gait.stride)*size;
            float advance=Mathf.Min(travel/cycleMetres,LeoMotionProfile.MaxCadence(run)*dt);
            phase=Mathf.Repeat(phase+advance,1);
        }
        // Load swing: bundle lags behind acceleration on a damped spring (target tracking, w=9 rad/s, zeta=.45).
        Vector3 velocity=teleport?Vector3.zero:delta/Mathf.Max(.001f,Time.deltaTime);
        Vector3 acceleration=Vector3.ClampMagnitude((velocity-previousVelocity)/Mathf.Max(.001f,dt),40f);previousVelocity=velocity;
        smoothAcceleration=Vector3.Lerp(smoothAcceleration,acceleration,1-Mathf.Exp(-14*dt));
        int carriedNow=backpack!=null?backpack.Carried:0,capacityNow=backpack!=null?backpack.Capacity:0;
        // Heavier tier -> livelier swing: 1.0 / 1.1 (16+) / 1.45 (32+) / 1.8 (64+).
        float tierGain=carriedNow>=LeoRig.TierMassive?1.8f:carriedNow>=LeoRig.TierBig?1.45f:carriedNow>=LeoRig.TierStack?1.1f:1f;
        float amount=carriedNow>0?(.4f+.6f*carry)*tierGain:0;
        float aForward=Vector3.Dot(smoothAcceleration,transform.forward),aRight=Vector3.Dot(smoothAcceleration,transform.right);
        float bounceTarget=amount*run*move*Mathf.Sin(phase*Mathf.PI*4);
        float pitchTarget=Mathf.Clamp(-aForward*.12f*amount+bounceTarget,-8,8),rollTarget=Mathf.Clamp(aRight*.10f*amount+amount*move*Mathf.Cos(phase*Mathf.PI*2)*.8f,-8,8);
        loadPitchVel+=(-81f*(loadPitch-pitchTarget)-8.1f*loadPitchVel)*dt;loadPitch=Mathf.Clamp(loadPitch+loadPitchVel*dt,-11,11);
        loadRollVel+=(-81f*(loadRoll-rollTarget)-8.1f*loadRollVel)*dt;loadRoll=Mathf.Clamp(loadRoll+loadRollVel*dt,-11,11);
        // Milestones. The frame/bundle swap itself is done by LeoRig.ShowCargo; here only the body reacts.
        if(lastCarried>=0&&carriedNow>lastCarried)
        {
            if(lastCarried<LeoRig.TierSmall&&carriedNow>=LeoRig.TierSmall)pendingAdjust=true;           // 4: Leo straightens the bundle
            if(lastCarried<LeoRig.TierStack&&carriedNow>=LeoRig.TierStack)loadPitchVel-=26f;            // 16: stack visibly taller, settles
            if(lastCarried<LeoRig.TierBig&&carriedNow>=LeoRig.TierBig)loadPitchVel-=32f;                // 32: second strap, heavier swing
            if(lastCarried<LeoRig.TierMassive&&carriedNow>=LeoRig.TierMassive)loadPitchVel-=38f;        // 64: massive load
        }
        lastCarried=carriedNow;
        bool free=!blocked&&!conversation&&!chopping&&!StoryInspection.Active&&rig.Celebration.Age<0f;
        if(pendingAdjust&&free&&now-adjustStart>1.2f){pendingAdjust=false;adjustStart=now;}
        float at=now-adjustStart,adjustTarget=free&&at>=0&&at<1.05f?LeoMotionProfile.Smooth(at/.22f)*(1-LeoMotionProfile.Smooth((at-.78f)/.27f)):0;
        adjustWeight=Mathf.MoveTowards(adjustWeight,adjustTarget,dt*9f);
        float tug=at>=.40f&&at<.70f?2.2f*Mathf.Sin(Mathf.PI*(at-.40f)/.30f):0;                          // small pull on the straps
        fullWeight=Mathf.MoveTowards(fullWeight,capacityNow>0&&carriedNow>=capacityNow?1:0,dt*1.6f);
        float yaw=Mathf.DeltaAngle(previousRotation.eulerAngles.y,transform.eulerAngles.y);previousRotation=transform.rotation;
        turn=Mathf.Lerp(turn,Mathf.Clamp(yaw/Mathf.Max(.001f,dt)/180f,-1,1),k);
        bool sheath=conversation||blocked||motion.StowTool||StoryInspection.Active;
        LeoCelebration.Pose cheer = rig.Celebration.Frame;
        float wantedHolster = sheath ? 1f : chopping ? 0f : cheer.holster;
        float holsterRate = chopping || motion.InMotion ? 8f :
            rig.Celebration.Age >= 0f ? 5.5f : 1f / Mathf.Max(.1f, book.holsterSeconds);
        if (!sheath || actionWeight < .05f)
            holster = Mathf.MoveTowards(holster, wantedHolster, dt * holsterRate);
        if (!sheath && hitFrame == Time.frameCount) holster = 0f;
        if(!chopping&&wasChopping&&!recovering){cancelPhase=posePhase;cancelling=true;}
        if(chopping)cancelling=false;
        float actionPhase=cancelling?cancelPhase:impact.Phase(now,book.contact,book.recovery,hitFrame==Time.frameCount);
        posePhase=actionPhase;wasChopping=chopping;
        talk=Mathf.MoveTowards(talk,Speaking()&&!blocked?1:0,dt*7);
        float eye=1;
        if(blink)
        {
            if(now>=nextBlink){blinkAt=now;nextBlink=now+3.2f+Mathf.Repeat(now*.37f,2.4f);}
            float age=(now-blinkAt)/.17f;eye=age>=0&&age<1?Mathf.Lerp(.04f,1,Mathf.Abs(age*2-1)):1;
        }
        var frame=new PoseFrame{time=now,phase=phase,move=move,run=run,carry=carry,carried=backpack!=null?backpack.Carried:0,capacity=backpack!=null?backpack.Capacity:21,action=actionWeight*(1-LeoMotionProfile.Smooth(holster)),actionPhase=actionPhase,holster=holster,turn=turn,talk=talk,eye=eye,dash=player!=null&&player.IsDashing?1:0,quality=quality,loadPitch=loadPitch+tug*adjustWeight,loadRoll=loadRoll,adjust=adjustWeight,full=fullWeight};
        frame.bark = contactPoint;
        frame.cheer = cheer;
        frame.motion = motion;
        if (inspecting && !motion.InMotion) frame.inspection = inspectionPose;
        ApplyPose(frame,plantFeet&&move>.02f&&!blocked,adaptToGround&&!blocked);
        CurrentState = motion.StowTool ? motion.Label : inspecting ? inspectionPose.state : blocked ? "Rest / blocked" :
            rig.Celebration.Age >= 0f ? "Celebrate" : holster > .01f && holster < .99f ? "Holster" :
            frame.action > .05f ? (hitFrame == Time.frameCount ? "Contact" : recovering ? "Recover" : "Chop / ready") :
            move > .1f ? (carry > .65f ? "Heavy move" : run > .5f ? "Run" : "Walk") :
            conversation ? "Listen" : "Idle";
        bool trail=axeTrail!=null&&!VibeSettings.ReduceEffects&&frame.action>.8f&&holster<.01f&&actionPhase>.40f&&actionPhase<.74f;
        if(axeTrail!=null){if(trail&&!trailWasOn)axeTrail.Clear();axeTrail.emitting=trail;if(blocked)axeTrail.Clear();}trailWasOn=trail;
    }
    public static float ClipDuration(string name,LeoMotionProfile.Book book)
    {
        switch (name)
        {
            case "Walk": return .95f;
            case "Run": return .49f;
            case "HeavyWalk": return 1f;
            case "HeavyRun": return .62f;
            case "Dash": return .40f;
            case "AxeReady": return 1.4f;
            case "Chop": return book.windup + book.recovery;
            case "Cancel": return .30f;
            case "Sheathe":
            case "Draw": return book.holsterSeconds;
            case "Celebrate": return LeoCelebration.Duration;
            case "SwimIdle": return 1.22f;
            case "Swim": return .81f;
            case "Fall":
            case "Land": return 1.1f;
            default: return 4f;
        }
    }
#if UNITY_EDITOR
    public bool SamplePreview(string name,float normalized)
    {
        if(!Initialize())return false;normalized=Mathf.Clamp01(normalized);float duration=ClipDuration(name,book);
        bool loop = name == "Idle" || name == "Walk" || name == "Run" || name == "HeavyWalk" ||
            name == "HeavyRun" || name == "AxeReady" || name == "IdleHolstered" || name == "SwimIdle" || name == "Swim";
        var f=new PoseFrame{time=normalized*(loop?4:duration),phase=normalized,eye=1,quality=1};
        switch(name)
        {
            case "Walk":f.move=1;break;
            case "Run":f.move=f.run=1;break;
            case "HeavyWalk":f.move=f.carry=1;break;
            case "HeavyRun":f.move=f.run=f.carry=1;break;
            case "Dash":f.move=f.run=f.dash=1;break;
            case "AxeReady":f.action=1;break;
            case "Chop":
                f.action=1;float t=normalized*duration;f.actionPhase=t<=book.windup?book.contact*LeoMotionProfile.Smooth(t/book.windup):Mathf.Lerp(book.contact,1,LeoMotionProfile.Smooth((t-book.windup)/book.recovery));break;
            case "Cancel":f.action=1-LeoMotionProfile.Smooth(normalized);f.actionPhase=.35f;break;
            case "Sheathe":f.holster=LeoMotionProfile.Smooth(normalized);break;
            case "Draw":f.holster=1-LeoMotionProfile.Smooth(normalized);break;
            case "IdleHolstered":f.holster=1;break;
            case "Celebrate":
                f.cheer = LeoCelebration.Sample(normalized * duration);
                f.holster = f.cheer.holster;
                break;
            case "SwimIdle":
            case "Swim":
                f.holster = 1f;
                f.motion = new LeoAirWaterMotion.Frame { Swim = 1f, Stroke = normalized, Effort = name == "Swim" ? 1f : 0f };
                break;
            case "Fall":
                f.holster = 1f;
                f.motion = new LeoAirWaterMotion.Frame { Fall = 1f, AirTime = normalized * duration };
                break;
            case "Land":
                f.holster = 1f;
                f.motion = new LeoAirWaterMotion.Frame { Landing = 1f, Descent = normalized };
                break;
            default:
                float age=(normalized-.77f)/.045f;f.eye=age>=0&&age<1?Mathf.Lerp(.04f,1,Mathf.Abs(age*2-1)):1;break;
        }
        LeoMotionProfile.MixInto(gait,book.walk,book.run,f.run);ApplyPose(f,false,false);return true;
    }
#endif
}
