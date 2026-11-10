using System.Collections.Generic;
using UnityEngine;

// Authored transform rig. The editor creates the model; this component only stores joints/rest pose.
[DisallowMultipleComponent]
public sealed class OskarRig : MonoBehaviour
{
    [Header("Корпус и лицо")]
    public Transform hips, spine, head, eyeL, eyeR, browL, browR, mouth, beard, moustache, hat;
    [Header("Руки")]
    public Transform shoulderL, elbowL, handL, shoulderR, elbowR, handR;
    [Header("Ноги")]
    public Transform hipL, kneeL, ankleL, hipR, kneeR, ankleR;
    [Header("Отдельный реквизит")]
    public Transform apron, pocket, squirrel, squirrelHead, squirrelTail, blueprint, wingCharm;
    public Transform planer, planerKnob, planerGrip, pencil, hammer, chisel, apronKnot;
    [SerializeField,HideInInspector] Transform[] joints;
    [SerializeField,HideInInspector] Vector3[] positions, scales;
    [SerializeField,HideInInspector] Quaternion[] rotations;
    public bool HasRest { get { return joints!=null&&positions!=null&&rotations!=null&&scales!=null&&joints.Length==positions.Length&&joints.Length==rotations.Length&&joints.Length==scales.Length; } }
    public int JointCount { get { return HasRest?joints.Length:0; } }
    public void Bind()
    {
        var map=new Dictionary<string,Transform>();
        foreach(var t in GetComponentsInChildren<Transform>(true))if(t!=transform&&t.GetComponent<MeshFilter>()==null)map[t.name]=t;
        Transform T(string name){Transform result;return map.TryGetValue(name,out result)?result:null;}
        hips=T("Hips");spine=T("Spine");head=T("Head");eyeL=T("EyeL");eyeR=T("EyeR");browL=T("BrowL");browR=T("BrowR");mouth=T("Mouth");beard=T("Beard");moustache=T("Moustache");hat=T("Hat");
        shoulderL=T("ShoulderL");elbowL=T("ElbowL");handL=T("HandL");shoulderR=T("ShoulderR");elbowR=T("ElbowR");handR=T("HandR");
        hipL=T("HipL");kneeL=T("KneeL");ankleL=T("AnkleL");hipR=T("HipR");kneeR=T("KneeR");ankleR=T("AnkleR");
        apron=T("Apron");pocket=T("Pocket");squirrel=T("Squirrel");squirrelHead=T("SquirrelHead");squirrelTail=T("SquirrelTail");blueprint=T("Blueprint");wingCharm=T("WingCharm");
        planer=T("Planer");planerKnob=T("PlanerKnob");planerGrip=T("PlanerGrip");pencil=T("Pencil");hammer=T("Hammer");chisel=T("Chisel");apronKnot=T("ApronKnot");
    }
    public void CaptureRest()
    {
        var list=new List<Transform>();foreach(var t in GetComponentsInChildren<Transform>(true))if(t!=transform&&t.GetComponent<MeshFilter>()==null)list.Add(t);
        joints=list.ToArray();positions=new Vector3[joints.Length];scales=new Vector3[joints.Length];rotations=new Quaternion[joints.Length];
        for(int i=0;i<joints.Length;i++){positions[i]=joints[i].localPosition;rotations[i]=joints[i].localRotation;scales[i]=joints[i].localScale;}
    }
    [ContextMenu("Вернуть позу покоя")]
    public void ResetPose()
    {
        if(!HasRest)return;
        for(int i=0;i<joints.Length;i++)if(joints[i]!=null){joints[i].localPosition=positions[i];joints[i].localRotation=rotations[i];joints[i].localScale=scales[i];}
    }
    public Quaternion RestRotation(Transform joint)
    {if(HasRest)for(int i=0;i<joints.Length;i++)if(joints[i]==joint)return rotations[i];return joint!=null?joint.localRotation:Quaternion.identity;}
    public Vector3 RestScale(Transform joint)
    {if(HasRest)for(int i=0;i<joints.Length;i++)if(joints[i]==joint)return scales[i];return joint!=null?joint.localScale:Vector3.one;}
}
