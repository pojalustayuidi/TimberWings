using UnityEngine;

// Lightweight rig, animated by one WorkyardView. No pathfinding or production code.
public sealed class WorkyardActor : MonoBehaviour
{
    public Transform body,leftArm,rightArm,tool;
    public WorkerRole role;
    Vector3 bodyRest;
    Quaternion leftRest,rightRest;
    bool cached;
    float workWeight,pulse;

    void Cache()
    {
        if(cached)return;cached=true;
        if(body!=null)bodyRest=body.localPosition;
        if(leftArm!=null)leftRest=leftArm.localRotation;
        if(rightArm!=null)rightRest=rightArm.localRotation;
    }
    public void Produced(){pulse=1f;}
    public void Pose(float time,float dt,bool working,int slot,float productionPhase=0f)
    {
        Cache();workWeight=Mathf.MoveTowards(workWeight,working?1:0,dt*5f);pulse=Mathf.Max(0,pulse-dt*2.3f);
        float t=time+slot*0.83f;
        float a=role==WorkerRole.Lumberjack ? WorkyardTree.Timing.LumberSwing(productionPhase) : role==WorkerRole.Sawyer ? -24f+Mathf.Sin(t*7f)*17f : -18f+Mathf.Sin(t*3.5f)*9f;
        if(rightArm!=null)rightArm.localRotation=rightRest*Quaternion.Euler(a*workWeight,0,-5f*workWeight);
        if(leftArm!=null)leftArm.localRotation=leftRest*Quaternion.Euler((role==WorkerRole.Clerk?-25f:a*0.64f)*workWeight,0,5f*workWeight);
        if(body!=null)body.localPosition=bodyRest+Vector3.up*(Mathf.Sin(t*2.2f)*0.008f+Mathf.Sin(pulse*Mathf.PI)*0.035f);
    }
}
