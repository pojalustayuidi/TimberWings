using UnityEngine;

[DisallowMultipleComponent]
public sealed class WorkyardStation : MonoBehaviour
{
    public WorkerRole role;
    public int index;
    public Transform actorPoint;
    public Transform movingSaw;
    public WorkyardTree tree;
    Vector3 sawRest;
    bool cached;

    public void Pose(float time,bool working)
    {
        if(movingSaw==null) return;
        if(!cached){sawRest=movingSaw.localPosition;cached=true;}
        movingSaw.localPosition=sawRest+(working ? Vector3.forward*Mathf.Sin(time*7f)*0.13f : Vector3.zero);
    }
}

