using System.Collections.Generic;
using UnityEngine;

// All motion is relative to the authored pose. Root position, colliders and legs stay untouched.
[DefaultExecutionOrder(210), DisallowMultipleComponent]
public sealed class MiraMotion : MonoBehaviour
{
    public MiraRig rig;
    public Speaker speaker;
    public DialogueSequence dialogue;
    public DialogueManager manager;
    public MiraPresence presence;
    public float lookRadius = 6f;
    public float headYaw = 48f, bodyYaw = 28f;
    struct Pose
    {
        public Transform t; public Vector3 position, scale; public Quaternion rotation;
        public Pose(Transform node) { t=node;position=node.localPosition;scale=node.localScale;rotation=node.localRotation; }
        public void Restore() { if(t!=null){t.localPosition=position;t.localRotation=rotation;t.localScale=scale;} }
    }
    readonly List<Pose> poses = new List<Pose>();
    readonly List<Transform> flowers = new List<Transform>();
    Transform player, mouth;
    Quaternion modelRest;
    float yaw, pitch, body, attention, nextBlink, blinkStart=-10, nextPot, gestureStart=-10, phase;
    int gesture;
    bool initialized, wasNear, wasTalking;
    public void Wave() { BeginGesture(1); }
    public void Thank() { BeginGesture(2); }
    void BeginGesture(int value) { if (!initialized) return;gesture=value;gestureStart=Time.time; }
    void Awake()
    {
        if(rig==null)rig=GetComponent<MiraRig>();
        if(speaker==null)speaker=GetComponentInParent<Speaker>();
        if(dialogue==null&&speaker!=null)dialogue=speaker.GetComponent<DialogueSequence>();
        if(rig==null||rig.head==null||rig.spine==null){Debug.LogWarning("MIRA: не назначен MiraRig. Повтори сборщик.",this);enabled=false;return;}
        foreach(var t in new[]{rig.spine,rig.head,rig.eyeL,rig.eyeR,rig.shoulderL,rig.shoulderR,rig.elbowL,rig.elbowR,rig.handL,rig.handR,rig.charm,rig.rope}) Add(t);
        foreach(var t in rig.GetComponentsInChildren<Transform>(true))
        {
            if(t.name=="Mouth"){mouth=t;Add(t);}
            if(t.name.StartsWith("Steam")||t.name.StartsWith("FlowerPetal")){flowers.Add(t);Add(t);}
        }
        modelRest=rig.transform.localRotation;phase=Mathf.Abs(GetInstanceID()%97)*.13f;
        initialized=true;nextBlink=Time.time+2f;nextPot=Time.time+13f+phase;
    }
    void Add(Transform t) { if(t==null)return;foreach(var p in poses)if(p.t==t)return;poses.Add(new Pose(t)); }
    Pose Rest(Transform t) { foreach(var p in poses)if(p.t==t)return p;return default(Pose); }
    void Rotate(Transform t, Vector3 euler) { if(t!=null)t.localRotation=Rest(t).rotation*Quaternion.Euler(euler); }
    void LateUpdate()
    {
        if(!initialized)return;
        if(speaker==null||!string.Equals(speaker.displayName,"Мира",System.StringComparison.OrdinalIgnoreCase)){enabled=false;return;}
        float dt=Mathf.Min(.1f,Time.deltaTime);if(dt<=0)return;
        if(player==null&&PlayerMovement.Instance!=null)player=PlayerMovement.Instance.transform;
        bool paused=MobileInput.ModalOpen||SaveSystem.IsLoading||BuildingController.AnyRevealing;
        float now=Time.time, k=1f-Mathf.Exp(-4f*dt), amount=VibeSettings.ReduceEffects?.35f:1f;
        bool near=false;float wantYaw=0,wantPitch=0,wantBody=0;
        bool conversation=dialogue!=null&&dialogue.IsPlaying;
        bool ours=conversation&&DialogueSequence.Focus==speaker.transform;
        bool talking=(presence!=null&&presence.Talking)||(ours&&DialogueManager.IsBusy&&CurrentLineIsMira()&&MainLineTalking());
        if(player!=null&&!paused)
        {
            Vector3 delta=player.position+Vector3.up*1.45f-rig.head.position;
            Vector3 flat=new Vector3(delta.x,0,delta.z);near=flat.sqrMagnitude<lookRadius*lookRadius;
            if(near)
            {
                Quaternion frame=rig.transform.parent!=null?rig.transform.parent.rotation*modelRest:modelRest;
                Vector3 local=Quaternion.Inverse(frame)*flat;
                float angle=Mathf.Atan2(local.x,local.z)*Mathf.Rad2Deg;
                wantBody=Mathf.Clamp(angle*.38f,-bodyYaw,bodyYaw);
                wantYaw=Mathf.Clamp(angle-wantBody,-headYaw,headYaw);
                if(Mathf.Abs(angle)>125f){wantYaw=0;wantBody=0;}
                wantPitch=Mathf.Clamp(-Mathf.Atan2(delta.y,flat.magnitude)*Mathf.Rad2Deg,-15,18);
            }
        }
        // The dialogue system rotates the *whole visual root* to its real
        // partner. Do not add another 28° torso / 48° head turn on top.
        Speaker partner;
        if(speaker!=null&&Speaker.TryGetConversationPartner(speaker,out partner))
            wantBody=wantYaw=wantPitch=0;
        if(near&&!wasNear&&!paused&&!conversation&&gesture!=2) Wave();wasNear=near;
        if(talking&&!wasTalking&&gesture!=2)BeginGesture(4);wasTalking=talking;
        if(!paused&&!conversation&&!talking&&gesture==0&&now>=nextPot){BeginGesture(3);nextPot=now+24f+phase;}
        if(paused){gesture=0;talking=false;}
        foreach(var p in poses)p.Restore();
        body=Mathf.Lerp(body,wantBody,1f-Mathf.Exp(-1.8f*dt));
        yaw=Mathf.Lerp(yaw,wantYaw,k);pitch=Mathf.Lerp(pitch,wantPitch,k);attention=Mathf.Lerp(attention,talking?1:0,k);
        rig.transform.localRotation=modelRest*Quaternion.Euler(0,body,0);
        var spinePose=Rest(rig.spine);
        rig.spine.localPosition=spinePose.position+Vector3.up*(Mathf.Sin(now*1.8f+phase)*.002f*amount);
        rig.spine.localScale=Vector3.Scale(spinePose.scale,new Vector3(1,1+Mathf.Sin(now*1.8f+phase)*.004f*amount,1));
        float nod=Mathf.Sin(now*5.2f)*1.7f*attention*amount;
        Rotate(rig.head,new Vector3(pitch+nod,yaw,Mathf.Sin(now*.7f+phase)*.7f*amount));
        float age=now-gestureStart,duration=gesture==3?3.4f:gesture==1?1.8f:1.3f;
        float envelope=gesture==0?0:Mathf.Sin(Mathf.Clamp01(age/duration)*Mathf.PI);
        if(age>=duration){gesture=0;envelope=0;}
        float g=envelope*amount;
        if(gesture==1){Rotate(rig.shoulderL,new Vector3(-24*g,0,-24*g));Rotate(rig.elbowL,new Vector3(-12*g,Mathf.Sin(age*13)*10*g,0));Rotate(rig.handL,new Vector3(0,0,Mathf.Sin(age*13)*16*g));}
        else if(gesture==2){Rotate(rig.head,new Vector3(pitch+9*g,yaw,-3*g));Rotate(rig.shoulderL,new Vector3(-9*g,0,-8*g));}
        else if(gesture==3){Rotate(rig.head,new Vector3(16*g,yaw*(1-g),-6*g));Rotate(rig.shoulderR,new Vector3(-6*g,0,4*g));Rotate(rig.handR,new Vector3(0,0,2*g));}
        else if(gesture==4){Rotate(rig.shoulderL,new Vector3(-8*g,0,-6*g));Rotate(rig.handL,new Vector3(0,Mathf.Sin(age*4)*6*g,0));}
        foreach(var flower in flowers){var p=Rest(flower);flower.localPosition=p.position+Vector3.up*(Mathf.Sin(now*2+phase)*.003f*amount);flower.localRotation=p.rotation*Quaternion.Euler(0,0,Mathf.Sin(now*1.7f+phase)*2f*amount);}
        Rotate(rig.charm,new Vector3(Mathf.Sin(now*1.4f)*1.5f*amount,0,0));Rotate(rig.rope,new Vector3(0,0,Mathf.Sin(now*1.2f)*1.2f*amount));
        if(now>=nextBlink){blinkStart=now;nextBlink=now+3.2f+Mathf.Repeat(now*.73f,3f);}
        float blink=MiraInteractionRules.Blink(now-blinkStart);
        if(rig.eyeL!=null)rig.eyeL.localScale=Vector3.Scale(Rest(rig.eyeL).scale,new Vector3(1,blink,1));
        if(rig.eyeR!=null)rig.eyeR.localScale=Vector3.Scale(Rest(rig.eyeR).scale,new Vector3(1,blink,1));
        if(mouth!=null&&talking)mouth.localScale=Vector3.Scale(Rest(mouth).scale,new Vector3(1,1+Mathf.Abs(Mathf.Sin(now*9))* .16f*amount,1));
    }
    bool CurrentLineIsMira()
    {
        if(dialogue==null||dialogue.lines==null)return false;
        int i=DialogueSequence.ActiveLine-1;if(i<0||i>=dialogue.lines.Length)return false;
        var line=dialogue.lines[i];return line!=null&&(string.IsNullOrEmpty(line.speaker)||line.speaker==speaker.displayName);
    }
    bool MainLineTalking()
    {
        if(manager!=null&&manager.presentation!=null&&manager.presentation.HistoryOpen)return false;
        if(speaker!=null&&speaker.IsSpeaking)return true;
        if(manager==null)return false;
        if(manager.babble!=null&&manager.babble.Speaking)return true;
        var view=manager.presentation;
        return view!=null&&view.body!=null&&view.body.maxVisibleCharacters<view.body.textInfo.characterCount;
    }
    void OnDisable()
    {
        if(!initialized)return;foreach(var p in poses)p.Restore();if(rig!=null)rig.transform.localRotation=modelRest;
    }
}
