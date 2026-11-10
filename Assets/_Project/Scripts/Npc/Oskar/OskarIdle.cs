using UnityEngine;

// One pose writer. Reads dialogue state; never owns input, quests, physics or the camera.
[DefaultExecutionOrder(210),DisallowMultipleComponent]
public sealed class OskarIdle : MonoBehaviour
{
    public OskarRig rig;
    public bool breathe=true,blink=true,look=true;
    public float lookRadius=5f;
    [Header("Характер")]
    public bool greet=true,idleActivities=true,conversationGestures=true;
    public Vector2 activityInterval=new Vector2(16,28);
    [Header("Рабочий карандаш (создаёт редактор)")]
    public Transform workingPencil;
    public Speaker speaker;
    public DialogueSequence dialogue;
    public DialogueManager manager;
    public string CurrentGesture {get{return clock.clip!=null?clock.clip.id:"rest";}}
    readonly OskarMotionProfile.Clock clock=new OskarMotionProfile.Clock();
    readonly OskarMotionProfile.Approach approach=new OskarMotionProfile.Approach();
    OskarMotionProfile.Book book;
    Transform player,pencilParent;
    PlayerInteraction chopping;
    Vector3 pencilRestPosition;
    Quaternion pencilRestRotation,leftBaseRotation,rightBaseRotation;
    float yaw,pitch,nextBlink,blinkStart=-10,nextActivity,quietUntil,talkWeight,talkTime,findAt,phaseOffset;
    int activityIndex;
    bool initialized,wasConversation;
    string requestedGesture;
    static readonly string[] Activities={"plane","think","blueprint","squirrel"};
    void Awake(){Initialize();}
    bool Initialize()
    {
        if(initialized)return true;
        if(rig==null)rig=GetComponent<OskarRig>();
        if(rig==null||rig.head==null||rig.spine==null||rig.handL==null||rig.handR==null){enabled=false;return false;}
        if(speaker==null)speaker=GetComponentInParent<Speaker>();
        if(dialogue==null&&speaker!=null)dialogue=speaker.GetComponent<DialogueSequence>();
        if(manager==null)manager=FindFirstObjectByType<DialogueManager>();
        rig.CaptureRest();book=OskarMotionProfile.Load();
        leftBaseRotation=Quaternion.Inverse(transform.rotation)*rig.handL.rotation;
        rightBaseRotation=Quaternion.Inverse(transform.rotation)*rig.handR.rotation;
        if(workingPencil!=null){pencilParent=workingPencil.parent;pencilRestPosition=workingPencil.localPosition;pencilRestRotation=workingPencil.localRotation;}
        phaseOffset=Mathf.Abs(GetInstanceID()%71)*.07f;
        nextBlink=Time.time+3.3f;nextActivity=Time.time+10+phaseOffset;quietUntil=Time.time+8;
        approach.Reset(Time.time);initialized=true;return true;
    }
    void OnEnable(){SaveSystem.Loaded+=OnLoaded;}
    void OnDisable(){SaveSystem.Loaded-=OnLoaded;Restore();}
    void OnLoaded(){Restore();approach.Reset(Time.time);quietUntil=Time.time+8;nextActivity=Time.time+12;nextBlink=Time.time+3;}
    public void Restore(){clock.Clear();requestedGesture=null;talkWeight=0;wasConversation=false;if(initialized&&rig!=null)rig.ResetPose();}
    void FindPlayer()
    {
        if(player!=null||Time.time<findAt)return;findAt=Time.time+.5f;
        var movement=PlayerMovement.Instance;if(movement==null)return;
        player=movement.transform;chopping=movement.GetComponent<PlayerInteraction>();
    }
    void LateUpdate()
    {
        if(!Initialize()||Time.deltaTime<=0)return;
        FindPlayer();float now=Time.time,dt=Mathf.Min(.1f,Time.deltaTime);
        bool ours=dialogue!=null&&dialogue.IsPlaying;bool speaking=ReadSpeech(ours);
        bool blocked=!Application.isFocused||SaveSystem.IsLoading||MobileInput.ModalOpen||BuildingController.AnyRevealing||BuildingController.AnyRestorePending||
            ((DialogueSequence.AnyPlaying||DialogueManager.IsBusy)&&!ours&&!speaking)||(chopping!=null&&chopping.IsChopping);
        if(blocked||ours||speaking||((VibeSettings.ReduceEffects||!idleActivities)&&clock.clip!=null&&clock.clip.id!="greet"))clock.Return();
        if(wasConversation&&!ours){quietUntil=now+3;nextActivity=now+8;}wasConversation=ours;
        float distance=player!=null?FlatDistance(player.position,transform.position):float.MaxValue;
        bool clear=!blocked&&!ours&&!speaking&&now>=quietUntil;
        if(requestedGesture!=null&&clock.clip==null&&clear){StartGesture(requestedGesture);requestedGesture=null;nextActivity=now+20;}
        if(approach.Enter(distance,lookRadius,now,clear&&greet)&&clock.clip==null)StartGesture("greet");
        if(clock.clip==null&&clear&&idleActivities&&!VibeSettings.ReduceEffects&&distance<12&&now>=nextActivity)
        {
            StartGesture(Activities[activityIndex++%Activities.Length]);
            nextActivity=now+Mathf.Lerp(Mathf.Max(8,activityInterval.x),Mathf.Max(activityInterval.x,activityInterval.y),Mathf.Repeat(activityIndex*.618f+phaseOffset,1));
        }
        clock.Tick(dt);var clip=clock.clip;
        var pose=clip!=null?OskarMotionProfile.Sample(clip,clock.phase):default(OskarMotionProfile.Pose);
        talkWeight=Mathf.MoveTowards(talkWeight,speaking&&!blocked&&conversationGestures&&clip==null?1:0,dt*5);talkTime+=dt;
        float targetYaw=0,targetPitch=0;
        if(look&&player!=null&&!blocked&&distance<=lookRadius)
        {
            Vector3 eyes=rig.eyeL!=null&&rig.eyeR!=null?(rig.eyeL.position+rig.eyeR.position)*.5f:rig.head.position;
            Vector3 local=transform.InverseTransformDirection(player.position+Vector3.up*1.45f-eyes);
            float angle=Mathf.Atan2(local.x,local.z)*Mathf.Rad2Deg;
            if(Mathf.Abs(angle)<100){targetYaw=Mathf.Clamp(angle,-45,45);targetPitch=Mathf.Clamp(-Mathf.Atan2(local.y,new Vector2(local.x,local.z).magnitude)*Mathf.Rad2Deg,-12,15);}
        }
        Speaker partner;
        if(speaker!=null&&Speaker.TryGetConversationPartner(speaker,out partner))
            targetYaw=targetPitch=0;
        float k=1-Mathf.Exp(-3*dt);yaw=Mathf.Lerp(yaw,targetYaw,k);pitch=Mathf.Lerp(pitch,targetPitch,k);
        if(clip!=null)
        {
            float attention=1-Mathf.Min(OskarMotionProfile.Smooth(clock.phase/.18f),OskarMotionProfile.Smooth((1-clock.phase)/.18f));
            pose.head+=new Vector3(pitch,yaw,0)*attention;
        }
        ApplyPose(clip,pose,now,VibeSettings.ReduceEffects?.35f:1,true);
        if(clip==null)
        {
            rig.head.localRotation=rig.RestRotation(rig.head)*Quaternion.Euler(pitch+Mathf.Sin(talkTime*5)*1.7f*talkWeight,yaw,ours&&!speaking?-1.6f:0);
            if(talkWeight>.001f)
            {
                float beat=(Mathf.Sin(talkTime*2.1f)*.5f+.5f)*talkWeight*(VibeSettings.ReduceEffects?.35f:1);
                SolveArm(rig.shoulderL,rig.elbowL,rig.handL,transform.TransformPoint(new Vector3(-.405f,1.03f,.29f)),transform.TransformPoint(new Vector3(-.75f,.93f,-.12f)),beat*.6f,transform.rotation*leftBaseRotation);
                if(rig.mouth!=null)rig.mouth.localScale=Vector3.Scale(rig.RestScale(rig.mouth),new Vector3(1,1+Mathf.Abs(Mathf.Sin(talkTime*9))*.15f*talkWeight,1));
                if(rig.moustache!=null)rig.moustache.localRotation=rig.RestRotation(rig.moustache)*Quaternion.Euler(0,0,Mathf.Sin(talkTime*5)*.6f*talkWeight);
            }
        }
        if(blink)
        {
            if(now>=nextBlink){blinkStart=now;nextBlink=now+3.4f+Mathf.Repeat(now*.53f,2.6f);}
            float age=(now-blinkStart)/.18f;float value=age<0||age>=1?1:Mathf.Lerp(.05f,1,Mathf.Abs(age*2-1));
            if(rig.eyeL!=null)rig.eyeL.localScale=Vector3.Scale(rig.RestScale(rig.eyeL),new Vector3(1,value,1));
            if(rig.eyeR!=null)rig.eyeR.localScale=Vector3.Scale(rig.RestScale(rig.eyeR),new Vector3(1,value,1));
        }
    }
    bool ReadSpeech(bool ours)
    {
        if(speaker==null)return false;bool actor=false;
        if(ours&&dialogue.lines!=null)
        {
            int index=DialogueSequence.ActiveLine-1;
            if(index>=0&&index<dialogue.lines.Length){var line=dialogue.lines[index];actor=line!=null&&(string.IsNullOrEmpty(line.speaker)||string.Equals(line.speaker,speaker.displayName,System.StringComparison.OrdinalIgnoreCase));}
        }
        else if(manager!=null&&manager.presentation!=null&&manager.presentation.speakerName!=null)
            actor=DialogueManager.IsBusy&&string.Equals(manager.presentation.speakerName.text,speaker.displayName,System.StringComparison.OrdinalIgnoreCase);
        if(!actor)return false;
        if(manager!=null&&manager.presentation!=null&&manager.presentation.HistoryOpen)return false;
        if(speaker.IsSpeaking)return true;if(manager==null)return false;
        if(manager.babble!=null&&manager.babble.Speaking)return true;
        var view=manager.presentation;return view!=null&&view.body!=null&&view.body.maxVisibleCharacters<view.body.textInfo.characterCount;
    }
    void StartGesture(string id){var clip=OskarMotionProfile.Find(book,id);if(clip!=null)clock.Start(clip);}
    public void PlayGesture(string id)
    {
        if(!Application.isPlaying||!Initialize()||MobileInput.ModalOpen||OskarMotionProfile.Find(book,id)==null)return;
        requestedGesture=id;clock.Return();
    }
    [ContextMenu("Проверить: рубанок (Play)")]void PreviewPlane(){PlayGesture("plane");}
    [ContextMenu("Проверить: чертёж (Play)")]void PreviewPaper(){PlayGesture("blueprint");}
    [ContextMenu("Проверить: задумался (Play)")]void PreviewThink(){PlayGesture("think");}
    [ContextMenu("Проверить: белка (Play)")]void PreviewSquirrel(){PlayGesture("squirrel");}
    void ApplyPose(OskarMotionProfile.Clip clip,OskarMotionProfile.Pose pose,float time,float amount,bool breathing)
    {
        rig.ResetPose();rig.spine.localRotation=rig.RestRotation(rig.spine)*Quaternion.Euler(pose.spine);
        if(breathe&&breathing)rig.spine.localScale=Vector3.Scale(rig.RestScale(rig.spine),new Vector3(1,1+Mathf.Sin(time*1.65f)*.004f*amount,1));
        rig.head.localRotation=rig.RestRotation(rig.head)*Quaternion.Euler(pose.head);
        if(rig.planerGrip!=null)rig.planerGrip.localRotation=rig.RestRotation(rig.planerGrip)*Quaternion.Euler(pose.planerGrip);
        if(rig.squirrel!=null)rig.squirrel.localRotation=rig.RestRotation(rig.squirrel)*Quaternion.Euler(pose.squirrel);
        if(clip==null)return;
        Quaternion leftRotation=transform.rotation*leftBaseRotation*Quaternion.Euler(pose.leftWrist);
        Quaternion rightRotation=transform.rotation*rightBaseRotation*Quaternion.Euler(pose.rightWrist);
        if(pose.rightWeight>.0001f)SolveArm(rig.shoulderR,rig.elbowR,rig.handR,transform.TransformPoint(OskarMotionProfile.V(clip.rightPoint)),transform.TransformPoint(new Vector3(.75f,.93f,-.12f)),pose.rightWeight,rightRotation);
        Vector3 leftTarget=rig.handL.position;
        if(clip.leftReference=="paper")
        {
            if(rig.blueprint!=null)
            {
                Vector3 paper=rig.blueprint.TransformPoint(OskarMotionProfile.V(clip.leftPoint));
                if(workingPencil!=null&&pencilParent!=null)
                {
                    paper+=rig.blueprint.TransformDirection(Vector3.up*(pose.carry>.99f?Mathf.Sin(time*8)*.0018f:0));
                    Vector3 direction=rig.blueprint.TransformDirection(new Vector3(.75f,-.35f,-.56f)).normalized;
                    float tip=workingPencil.TransformVector(Vector3.up*.1935f).magnitude;
                    Vector3 home=pencilParent.TransformPoint(pencilRestPosition),work=paper-direction*tip;
                    Vector3 gripOffset=leftRotation*(new Vector3(0,0,.055f)*transform.lossyScale.y);
                    leftTarget=Vector3.Lerp(home,work,pose.carry)-gripOffset;
                    SolveArm(rig.shoulderL,rig.elbowL,rig.handL,leftTarget,transform.TransformPoint(new Vector3(-.75f,.93f,-.12f)),pose.leftWeight,leftRotation);
                    workingPencil.position=Vector3.Lerp(home,rig.handL.TransformPoint(new Vector3(0,0,.055f)),pose.grip);
                    workingPencil.rotation=Quaternion.Slerp(pencilParent.rotation*pencilRestRotation,Quaternion.FromToRotation(Vector3.up,direction),pose.carry);
                    return;
                }
                leftTarget=paper+rig.blueprint.TransformDirection(new Vector3(-.04f,0,-.04f));
            }
        }
        else
        {
            Transform reference=clip.leftReference=="planerGrip"?rig.planerGrip:clip.leftReference=="beard"?rig.beard:clip.leftReference=="squirrel"?rig.squirrel:transform;
            if(reference!=null)leftTarget=reference.TransformPoint(OskarMotionProfile.V(clip.leftPoint));
        }
        if(pose.leftWeight>.0001f)SolveArm(rig.shoulderL,rig.elbowL,rig.handL,leftTarget,transform.TransformPoint(new Vector3(-.75f,.93f,-.12f)),pose.leftWeight,leftRotation);
    }
    public static void SolveArm(Transform shoulder,Transform elbow,Transform hand,Vector3 target,Vector3 pole,float weight,Quaternion wristRotation)
    {
        if(shoulder==null||elbow==null||hand==null||weight<=0)return;
        Vector3 a=shoulder.position,b=elbow.position,c=hand.position;Quaternion restHand=hand.rotation;
        float upper=Vector3.Distance(a,b),lower=Vector3.Distance(b,c);if(upper<.001f||lower<.001f)return;
        target=Vector3.Lerp(c,target,Mathf.Clamp01(weight));Vector3 to=target-a;if(to.sqrMagnitude<.000001f)return;
        Vector3 forward=to.normalized;float margin=(upper+lower)*.002f;
        float distance=Mathf.Clamp(to.magnitude,Mathf.Abs(upper-lower)+margin,upper+lower-margin);target=a+forward*distance;
        Vector3 bend=Vector3.ProjectOnPlane(pole-a,forward);
        if(bend.sqrMagnitude<.00001f)bend=Vector3.ProjectOnPlane(b-a,forward);
        if(bend.sqrMagnitude<.00001f)bend=Vector3.Cross(forward,Vector3.up);
        if(bend.sqrMagnitude<.00001f)bend=Vector3.Cross(forward,Vector3.right);
        bend.Normalize();float along=(upper*upper-lower*lower+distance*distance)/(2*distance);
        float height=Mathf.Sqrt(Mathf.Max(0,upper*upper-along*along));Vector3 goal=a+forward*along+bend*height;
        shoulder.rotation=Quaternion.FromToRotation(b-a,goal-a)*shoulder.rotation;
        elbow.rotation=Quaternion.FromToRotation(hand.position-elbow.position,target-elbow.position)*elbow.rotation;
        hand.rotation=Quaternion.Slerp(restHand,wristRotation,Mathf.Clamp01(weight));
    }
    static float FlatDistance(Vector3 a,Vector3 b){a.y=b.y;return Vector3.Distance(a,b);}
#if UNITY_EDITOR
    public bool SamplePreview(string id,float phase)
    {if(!Initialize())return false;var clip=OskarMotionProfile.Find(book,id);if(clip==null)return false;ApplyPose(clip,OskarMotionProfile.Sample(clip,Mathf.Clamp01(phase)),0,1,false);return true;}
#endif
}
