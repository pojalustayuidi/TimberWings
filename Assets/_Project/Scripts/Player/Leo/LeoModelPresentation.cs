using UnityEngine;

// First-stage visual bridge only. Reads existing state/events; never awards resources or drives movement.
[DefaultExecutionOrder(250),DisallowMultipleComponent]
public sealed partial class LeoModelPresentation : MonoBehaviour
{
    public LeoRig rig;
    public PlayerMovement player;
    public PlayerInteraction interaction;
    public BackpackTag backpack;
    public Speaker speaker;
    public bool basicLocomotion=true,blink=true,readBackpack=true;
    float holster,nextBlink=3,blinkAt=-10;
    Vector3 previousPosition;
    bool initialized;
    void Awake(){Initialize();}
    void Initialize()
    {
        if(initialized)return;if(rig==null)rig=GetComponent<LeoRig>();if(rig==null)return;
        if(!rig.HasRest)rig.CaptureRest();if(player==null)player=GetComponentInParent<PlayerMovement>();
        if(player!=null){if(interaction==null)interaction=player.GetComponent<PlayerInteraction>();if(backpack==null)backpack=player.GetComponent<BackpackTag>();if(speaker==null)speaker=player.GetComponent<Speaker>();previousPosition=player.transform.position;}
        rig.ResetPose();
        InitializeLocomotion();
        chopBook = LeoMotionProfile.Load();
        nextBlink=Time.time+3;initialized=true;
    }
    void OnEnable()
    {
        GameEvents.ChopHit += OnHit;
        GameEvents.TreeChopped += OnTreeFelled;
        GameEvents.LevelUp += OnLevelUp;
        GameEvents.BuildRevealStarted += OnBuildReveal;
        SaveSystem.Loaded += OnLoaded;
        SaveSystem.NewGameStarted += OnLoaded;
        if (player != null) previousPosition = player.transform.position;
        ResetLocomotion();
    }
    void OnDisable(){GameEvents.ChopHit-=OnHit;GameEvents.TreeChopped-=OnTreeFelled;GameEvents.LevelUp-=OnLevelUp;GameEvents.BuildRevealStarted-=OnBuildReveal;ResetChopPose();ResetLifePose();SaveSystem.Loaded-=OnLoaded;SaveSystem.NewGameStarted-=OnLoaded;if(rig!=null&&rig.HasRest){rig.ResetPose();rig.ShowCargo(0,1);rig.EquipHammer(false);}}
    void OnLoaded(){if(rig!=null)rig.ResetPose();if(player!=null)previousPosition=player.transform.position;ResetLocomotion();ResetChopPose();ResetLifePose();holster=0;}
    void LateUpdate()
    {
        Initialize();if(!initialized||rig==null||Time.deltaTime<=0)return;
        float dt=Mathf.Min(.1f,Time.deltaTime),now=Time.time;
        float travel=0f;
        if(player!=null)
        {
            Vector3 delta=player.transform.position-previousPosition;delta.y=0;previousPosition=player.transform.position;
            travel=delta.magnitude;
        }
        bool chopping=interaction!=null&&interaction.IsChopping;
        bool conversation=DialogueSequence.FreezesPlayer;
        bool talking=conversation||(speaker!=null&&speaker.IsSpeaking);
        bool blocked=SaveSystem.IsLoading||BuildingController.AnyRevealing||BuildingController.AnyRestorePending||(player!=null&&player.ControlLocked);
        float carry=backpack!=null&&backpack.Capacity>0?Mathf.Clamp01((float)backpack.Carried/backpack.Capacity):0;
        ReadChopPose(now, dt, chopping, blocked || conversation || WaterSwim.Swimming);
        bool canPlaceFeet = player != null && !blocked && !WaterSwim.Swimming;
        ReadLifePose(travel / Mathf.Max(.001f, Time.deltaTime), dt, canPlaceFeet && !conversation && !talking,
            chopping || chopWeight > .02f);
        rig.ResetPose();rig.EquipHammer(false);
        if (basicLocomotion || LifeActive)
        {
            ApplyLocomotion(travel, Time.deltaTime, now, carry,
                basicLocomotion && canPlaceFeet && !chopping && !conversation, canPlaceFeet);
        }
        else
        {
            ResetLocomotion();
            BraceForChop();
        }
        LeoAxePose.RestWrist(rig, moveWeight);
        if (rig.chest != null)
            rig.chest.localScale = Vector3.Scale(rig.RestScale(rig.chest), new Vector3(1f, 1f + Mathf.Sin(now * 1.7f) * .003f, 1f));
        if(talking&&!blocked&&!chopping)
        {
            float voice=speaker!=null&&speaker.IsSpeaking?1:0;
            rig.Rotate(rig.head,new Vector3(Mathf.Sin(now*4)*voice*1.5f,0,0));
            rig.Rotate(rig.jaw,new Vector3(Mathf.Abs(Mathf.Sin(now*9))*voice*3,0,0));
        }
        bool sheath = blocked || (!chopping && (talking || LifeActive));
        holster = Mathf.MoveTowards(holster, sheath ? 1f : 0f, dt * (chopping ? 8f : 4f));
        if (chopHitFrame == Time.frameCount && !sheath) holster = 0f;
        if (workWeight > .001f)
        {
            PlaceWorkHammer();
        }
        else
        {
            PlaceChopTools();
            rig.PlaceHammer();
        }
        if(readBackpack&&backpack!=null)rig.ShowCargo(backpack.Carried,backpack.Capacity);
        if(blink)
        {
            if(now>=nextBlink){blinkAt=now;nextBlink=now+3.1f+Mathf.Repeat(now*.37f,2.5f);}
            float t=(now-blinkAt)/.16f;float amount=t<0||t>=1?1:Mathf.Lerp(.04f,1,Mathf.Abs(t*2-1));
            if(rig.eyeL!=null)rig.eyeL.localScale=Vector3.Scale(rig.RestScale(rig.eyeL),new Vector3(1,amount,1));
            if(rig.eyeR!=null)rig.eyeR.localScale=Vector3.Scale(rig.RestScale(rig.eyeR),new Vector3(1,amount,1));
        }
    }
}
