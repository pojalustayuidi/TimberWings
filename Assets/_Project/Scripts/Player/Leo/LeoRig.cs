using System.Collections.Generic;
using UnityEngine;

// Transform rig: authored meshes follow articulated joints. No runtime mesh generation.
[DefaultExecutionOrder(350), DisallowMultipleComponent]
public sealed class LeoRig : MonoBehaviour
{
    [Header("Корпус и лицо")]
    public Transform hips,spine,chest,neck,head,eyeL,eyeR,browL,browR,jaw,mouth,hair,bangs,cowlick,faceFocus;
    [Header("Руки")]
    public Transform shoulderL,upperArmL,elbowL,forearmTwistL,handL,shoulderR,upperArmR,elbowR,forearmTwistR,handR;
    public Transform[] fingersL,fingerTipsL,fingersR,fingerTipsR;
    public Transform thumbL,thumbTipL,thumbR,thumbTipR;
    [Header("Ноги")]
    public Transform hipL,kneeL,ankleL,toeL,hipR,kneeR,ankleR,toeR;
    [Header("Подол куртки (Leo v3.4)")]
    public Transform hemFrontR,hemFrontL,hemBackR,hemBackL;
    [Header("Реквизит и точки крепления")]
    public Transform backpack,backpackFlap,backpackPocket,backpackAnchor,cargoRoot,cargoTies,logUpgrade,gloves;
    public Transform[] cargo;
    [Header("Рама переноски (ступени вместимости) и вторая стяжка")]
    public Transform[] frames;
    public Transform cargoTies2;
    public Transform handGripR,handGripL,axeHolster,axeRoot,hammerRoot,compass,compassNeedle,buildGripL,carryGripL,carryGripR;
    public Transform axeBladeTip,hammerStrike;
    [SerializeField,HideInInspector]Transform[] joints;
    [SerializeField,HideInInspector]Vector3[] positions,scales;
    [SerializeField,HideInInspector]Quaternion[] rotations;
    float[] cargoReveal;
    Transform hammerHolster;
    bool hammerEquipped;
    Transform scarfTail, scarfShortTail;
    Quaternion scarfTailRest, scarfShortTailRest;
    Vector3 scarfLastPosition;
    Vector3 scarfSway;

    void OnEnable()
    {
        if (neck == null) return;

        scarfTail = neck.Find("ScarfTail");
        scarfShortTail = neck.Find("ScarfShortTail");
        scarfTailRest = RestRotation(scarfTail);
        scarfShortTailRest = RestRotation(scarfShortTail);
        scarfLastPosition = transform.position;
        scarfSway = Vector3.zero;
    }

    void LateUpdate()
    {
        // Run after both existing pose controllers reset the rig for this frame.
        UpdateJacketHem();
        float dt = Time.deltaTime;
        if (scarfTail == null || scarfShortTail == null || dt <= 0f) return;

        Vector3 travel = transform.position - scarfLastPosition;
        scarfLastPosition = transform.position;
        Vector3 velocity = travel.sqrMagnitude < .64f
            ? transform.InverseTransformDirection(travel / Mathf.Max(.01f, dt))
            : Vector3.zero;
        float run = Mathf.Clamp01(new Vector2(velocity.x, velocity.z).magnitude / 4f);
        float time = Time.time;
        var target = new Vector3(
            1.9f * Mathf.Sin(time * 2.1f) - 9f * run,
            Mathf.Clamp(-velocity.x * 1.1f, -4f, 4f) + 1.4f * Mathf.Sin(time * 1.9f),
            (2f + 2.2f * run) * Mathf.Sin(time * 3.1f));
        scarfSway = Vector3.Lerp(scarfSway, target, 1f - Mathf.Exp(-9f * Mathf.Min(dt, .1f)));
        scarfTail.localRotation = scarfTailRest * Quaternion.Euler(scarfSway);

        // The shorter end catches less wind and trails a little behind.
        var shortSway = new Vector3(scarfSway.x * .60f,
            scarfSway.y * .7f, scarfSway.z * .64f + Mathf.Sin(time * 2.8f - .8f) * 1.6f);
        scarfShortTail.localRotation = scarfShortTailRest * Quaternion.Euler(shortSway);
    }

    void OnDisable()
    {
        if (scarfTail != null) scarfTail.localRotation = scarfTailRest;
        if (scarfShortTail != null) scarfShortTail.localRotation = scarfShortTailRest;
    }

    // ---- Jacket hem ------------------------------------------------------------------------------------
    // The hem is four rigid panels on the pelvis (front/back for each leg). A panel swings about its top edge
    // with the thigh on its side, so the cloth is pushed clear of the leg instead of cutting through it.
    // Rotation is recomputed from rest every frame, so nothing accumulates and ResetPose() restores it.
    public const float HemFollow = .85f, HemDeadZone = 6f, HemMaxAngle = 55f;
    public void UpdateJacketHem()
    {
        if (hips == null) return;
        FollowHem(hipR, kneeR, hemFrontR, hemBackR);
        FollowHem(hipL, kneeL, hemFrontL, hemBackL);
    }
    void FollowHem(Transform hip, Transform knee, Transform front, Transform back)
    {
        if (hip == null || knee == null || (front == null && back == null)) return;
        // Thigh pitch in the pelvis frame: positive when the knee is in front of the hip.
        Vector3 thigh = hips.InverseTransformDirection(knee.position - hip.position);
        if (thigh.sqrMagnitude < 1e-8f) return;
        float pitch = Mathf.Atan2(thigh.z, -thigh.y) * Mathf.Rad2Deg;
        float forward = Mathf.Clamp(HemFollow * (pitch - HemDeadZone), 0f, HemMaxAngle);
        float backward = Mathf.Clamp(HemFollow * (-pitch - HemDeadZone), 0f, HemMaxAngle);
        if (front != null) front.localRotation = RestRotation(front) * Quaternion.Euler(-forward, 0f, 0f);
        if (back != null) back.localRotation = RestRotation(back) * Quaternion.Euler(backward, 0f, 0f);
    }

    public Transform HammerHolster
    {
        get
        {
            if (hammerHolster == null && hips != null)
                hammerHolster = hips.Find("HammerHolster");

            return hammerHolster;
        }
    }
    public bool HasRest{get{return joints!=null&&positions!=null&&rotations!=null&&scales!=null&&joints.Length==positions.Length&&joints.Length==rotations.Length&&joints.Length==scales.Length;}}
    public int JointCount{get{return HasRest?joints.Length:0;}}
    public void Bind()
    {
        var map=new Dictionary<string,Transform>();foreach(var t in GetComponentsInChildren<Transform>(true))if(t!=transform&&t.GetComponent<MeshFilter>()==null)map[t.name]=t;
        Transform T(string name){Transform result;return map.TryGetValue(name,out result)?result:null;}
        hips=T("Hips");spine=T("Spine");chest=T("Chest");neck=T("Neck");head=T("Head");eyeL=T("EyeL");eyeR=T("EyeR");browL=T("BrowL");browR=T("BrowR");jaw=T("Jaw");mouth=T("Mouth");hair=T("Hair");bangs=T("Bangs");cowlick=T("Cowlick");faceFocus=T("FaceFocus");
        shoulderL=T("ShoulderL");upperArmL=T("UpperArmL");elbowL=T("ElbowL");forearmTwistL=T("ForearmTwistL");handL=T("HandL");shoulderR=T("ShoulderR");upperArmR=T("UpperArmR");elbowR=T("ElbowR");forearmTwistR=T("ForearmTwistR");handR=T("HandR");
        hipL=T("HipL");kneeL=T("KneeL");ankleL=T("AnkleL");toeL=T("ToeL");hipR=T("HipR");kneeR=T("KneeR");ankleR=T("AnkleR");toeR=T("ToeR");
        backpack=T("Backpack");backpackFlap=T("BackpackFlap");backpackPocket=T("BackpackPocket");backpackAnchor=T("BackpackAnchor");cargoRoot=T("CargoRoot");cargoTies=T("CargoTies");logUpgrade=T("LogUpgrade");gloves=T("Gloves");
        handGripR=T("HandGripR");handGripL=T("HandGripL");axeHolster=T("AxeHolster");axeRoot=T("AxeRoot");hammerRoot=T("HammerRoot");compass=T("Compass");compassNeedle=T("CompassNeedle");buildGripL=T("BuildGripL");carryGripL=T("CarryGripL");carryGripR=T("CarryGripR");
        axeBladeTip=T("AxeBladeTip");hammerStrike=T("HammerStrike");hammerHolster=T("HammerHolster");
        hemFrontR=T("JacketHemFrontR");hemFrontL=T("JacketHemFrontL");hemBackR=T("JacketHemBackR");hemBackL=T("JacketHemBackL");
        thumbL=T("ThumbL");thumbTipL=T("ThumbLTip");thumbR=T("ThumbR");thumbTipR=T("ThumbRTip");
        fingersL=new Transform[4];fingersR=new Transform[4];fingerTipsL=new Transform[4];fingerTipsR=new Transform[4];
        for(int i=0;i<4;i++){fingersL[i]=T("FingerL"+i);fingersR[i]=T("FingerR"+i);fingerTipsL[i]=T("FingerL"+i+"Tip");fingerTipsR[i]=T("FingerR"+i+"Tip");}
        var visibleCargo=new List<Transform>();
        for(int i=0;i<12;i++){var slot=T("Cargo"+i);if(slot!=null)visibleCargo.Add(slot);}
        frames=new[]{T("FrameT1"),T("FrameT2"),T("FrameT3")};cargoTies2=T("CargoTies2");
        cargo=visibleCargo.ToArray();cargoReveal=new float[cargo.Length];
    }
    public void CaptureRest()
    {
        var all=new List<Transform>();foreach(var t in GetComponentsInChildren<Transform>(true))if(t!=transform&&t.GetComponent<MeshFilter>()==null)all.Add(t);
        joints=all.ToArray();positions=new Vector3[joints.Length];rotations=new Quaternion[joints.Length];scales=new Vector3[joints.Length];
        for(int i=0;i<joints.Length;i++){positions[i]=joints[i].localPosition;rotations[i]=joints[i].localRotation;scales[i]=joints[i].localScale;}
    }
#if UNITY_EDITOR
    [ContextMenu("Запомнить отредактированную позу (Edit Mode)")]
    void CaptureEditedRest()
    {
        if(Application.isPlaying){Debug.LogWarning("LEO: позу покоя запоминай вне Play Mode.");return;}
        UnityEditor.Undo.RecordObject(this,"Leo edited rest pose");CaptureRest();UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
    [ContextMenu("Вернуть позу покоя")]
    public void ResetPose(){if(!HasRest)return;for(int i=0;i<joints.Length;i++)if(joints[i]!=null){joints[i].localPosition=positions[i];joints[i].localRotation=rotations[i];joints[i].localScale=scales[i];}}
    public Quaternion RestRotation(Transform joint){if(HasRest)for(int i=0;i<joints.Length;i++)if(joints[i]==joint)return rotations[i];return joint!=null?joint.localRotation:Quaternion.identity;}
    public Vector3 RestPosition(Transform joint){if(HasRest)for(int i=0;i<joints.Length;i++)if(joints[i]==joint)return positions[i];return joint!=null?joint.localPosition:Vector3.zero;}
    public Vector3 RestScale(Transform joint){if(HasRest)for(int i=0;i<joints.Length;i++)if(joints[i]==joint)return scales[i];return joint!=null?joint.localScale:Vector3.one;}
    public void Rotate(Transform joint,Vector3 euler){if(joint!=null)joint.localRotation=RestRotation(joint)*Quaternion.Euler(euler);}
    // ---- Carrier tiers ----------------------------------------------------------------------------------
    // FRAME (permanent, follows the capacity upgrade; stays when logs are sold):
    //   capacity <16 : belt shelf + hook lugs only        16-31 : wooden posts + top bar
    //   32-63        : rear posts, side rails, steel brackets      64+ : taller posts, extra row
    // LOAD (follows logs carried): 0-3 empty mounts | 4-15 small bundle (2-6) | 16-31 full stack (6-8)
    //   32-63 big bundle (8-10) | 64+ massive, extra row (12). Second tie from 32 logs.
    public const float LogPitch=.081f,TieBottom=-.0405f;
    public const int TierSmall=4,TierStack=16,TierBig=32,TierMassive=64;
    public static int FrameTier(int capacity){return capacity>=TierMassive?3:capacity>=TierBig?2:capacity>=TierStack?1:0;}
    public static int LoadTier(int carried){return carried>=TierMassive?4:carried>=TierBig?3:carried>=TierStack?2:carried>=TierSmall?1:0;}
    public static int CargoSections(int carried,int capacity,int sections=12)
    {
        if(carried<TierSmall||capacity<=0||sections<=0)return 0;
        int count;
        if(carried>=TierMassive)count=12;
        else if(carried>=TierBig)count=8+Mathf.RoundToInt((carried-TierBig)/31f*2f);
        else if(carried>=TierStack)count=6+Mathf.RoundToInt((carried-TierStack)/15f*2f);
        else count=2+Mathf.RoundToInt((carried-TierSmall)/11f*4f);
        return Mathf.Clamp(count,1,sections);
    }
    public void ShowCargo(int carried,int capacity)
    {
        if(cargo==null)return;int count=CargoSections(carried,capacity,cargo.Length);
        // Compatibility pivot from earlier patches: never show an upgrade frame.
        if(logUpgrade!=null&&logUpgrade.gameObject.activeSelf)logUpgrade.gameObject.SetActive(false);
        if(frames!=null)
        {
            int tier=FrameTier(capacity);
            for(int i=0;i<frames.Length;i++)if(frames[i]!=null&&frames[i].gameObject.activeSelf!=(i<tier))frames[i].gameObject.SetActive(i<tier);
        }
        if(cargoReveal==null||cargoReveal.Length!=cargo.Length)cargoReveal=new float[cargo.Length];
        for(int i=0;i<cargo.Length;i++)
        {
            var log=cargo[i];if(log==null)continue;bool show=i<count;
            if(log.gameObject.activeSelf!=show)
            {
                log.gameObject.SetActive(show);
                cargoReveal[i]=show&&Application.isPlaying?0f:1f;
            }
            if(show)
            {
                cargoReveal[i]=Application.isPlaying?Mathf.MoveTowards(cargoReveal[i],1f,Time.deltaTime*7f):1f;
                float ease=cargoReveal[i]*cargoReveal[i]*(3f-2f*cargoReveal[i]);
                // A small pop up from the back makes each newly received log
                // clearly noticeable without reordering other pieces.
                log.localScale=RestScale(log)*Mathf.Lerp(.70f,1f,ease);
                log.localPosition=RestPosition(log)+Vector3.down*(.032f*(1-ease));
            }
        }
        float top=0,bottom=TieBottom,middle=0;bool tiesOn=count>0;
        if(tiesOn)
        {
            // Logs lie two per row; ties run from the belt shelf to just above the top row.
            int rows=(count+1)/2;
            top=(rows-1)*LogPitch+LogPitch*.5f+.012f;middle=(top+bottom)*.5f;
        }
        if(cargoTies!=null)
        {
            if(cargoTies.gameObject.activeSelf!=tiesOn)cargoTies.gameObject.SetActive(tiesOn);
            if(tiesOn)
            {
                Vector3 rest=RestPosition(cargoTies);
                cargoTies.localPosition=new Vector3(rest.x,middle,rest.z);
                cargoTies.localScale=Vector3.Scale(RestScale(cargoTies),new Vector3(1,(top-bottom)/.455f,1));
            }
        }
        if(cargoTies2!=null)
        {
            bool second=tiesOn&&carried>=TierBig;
            if(cargoTies2.gameObject.activeSelf!=second)cargoTies2.gameObject.SetActive(second);
            if(second){Vector3 rest=RestPosition(cargoTies2);cargoTies2.localPosition=new Vector3(rest.x,middle,rest.z);}
        }
    }
    public void PlaceAxe(float holstered) => PlaceAxe(holstered, 0f);

    internal void PlaceAxe(float holstered, float palmRoll)
    {
        if (axeRoot == null || handR == null || axeHolster == null) return;
        float t = Mathf.Clamp01(holstered);
        Quaternion tool = LeoAxePose.ToolRotation(handR, palmRoll);
        Vector3 gripOffset = Vector3.Scale(LeoAxePose.MainGrip, axeRoot.lossyScale);
        Vector3 held = LeoAxePose.Grip(handR) - tool * gripOffset;
        axeRoot.SetPositionAndRotation(Vector3.Lerp(held, axeHolster.position, t),
            Quaternion.Slerp(tool, axeHolster.rotation, t));
        if(fingersR!=null)for(int i=0;i<fingersR.Length;i++)Rotate(fingersR[i],new Vector3(42*t,0,0));
        if(fingerTipsR!=null)for(int i=0;i<fingerTipsR.Length;i++)Rotate(fingerTipsR[i],new Vector3(35*t,0,0));
    }
    public void EquipHammer(bool equipped)
    {
        hammerEquipped = equipped;
        bool showHammer = equipped || HammerHolster != null;
        PlaceHammer();

        if (hammerRoot != null && hammerRoot.gameObject.activeSelf != showHammer)
            hammerRoot.gameObject.SetActive(showHammer);
        if (axeRoot != null && axeRoot.gameObject.activeSelf == equipped)
            axeRoot.gameObject.SetActive(!equipped);
    }

    // The original hierarchy path stays valid for the preview clips. Pose
    // owners call this after the pelvis and the hand reach their final poses.
    public void PlaceHammer()
    {
        Transform socket = hammerEquipped ? handGripR : HammerHolster;
        if (hammerRoot != null && socket != null)
            hammerRoot.SetPositionAndRotation(socket.position, socket.rotation);
    }

    // Rotation-only two-bone IK. Segment lengths and all collider/root transforms stay unchanged.
    internal static void SolveTwoBone(Transform upper,Transform middle,Transform end,Vector3 goal,Vector3 pole,Quaternion rotation,float weight)
    {
        if(upper==null||middle==null||end==null||weight<=0)return;weight=Mathf.Clamp01(weight);
        Vector3 a=upper.position,b=middle.position,c=end.position;Quaternion oldEnd=end.rotation;
        float first=Vector3.Distance(a,b),second=Vector3.Distance(b,c);Vector3 delta=goal-a;
        if(first<.0001f||second<.0001f||delta.sqrMagnitude<.0000001f)return;
        float distance=Mathf.Clamp(delta.magnitude,Mathf.Abs(first-second)+.0001f,first+second-.0001f);
        Vector3 direction=delta.normalized,bend=Vector3.ProjectOnPlane(pole-a,direction);
        if(bend.sqrMagnitude<.000001f)bend=Vector3.ProjectOnPlane(upper.forward,direction);
        if(bend.sqrMagnitude<.000001f)bend=Vector3.Cross(direction,Vector3.right);
        bend.Normalize();float along=(first*first-second*second+distance*distance)/(2*distance);
        Vector3 elbow=a+direction*along+bend*Mathf.Sqrt(Mathf.Max(0,first*first-along*along));
        upper.rotation=Quaternion.Slerp(upper.rotation,Quaternion.FromToRotation(b-a,elbow-a)*upper.rotation,weight);
        Vector3 target=a+direction*distance;
        middle.rotation=Quaternion.Slerp(middle.rotation,Quaternion.FromToRotation(end.position-middle.position,target-middle.position)*middle.rotation,weight);
        end.rotation=Quaternion.Slerp(oldEnd,rotation,weight);
    }
}
