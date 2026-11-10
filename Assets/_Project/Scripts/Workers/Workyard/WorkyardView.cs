using UnityEngine;

[DisallowMultipleComponent]
public sealed class WorkyardView : MonoBehaviour
{
    public TentStages tent;
    public BuildingController build;
    public WorkerCamp camp;
    public GameObject abandoned,frame,built;
    public Transform actorRoot;
    public WorkyardStation[] stations=new WorkyardStation[0];
    public WorkyardActor[] actorPrefabs=new WorkyardActor[3];
    public WorkyardStockDisplay stock;
    public float animationDistance=28f;

    WorkyardActor[] actors;
    WorkyardStation[] assigned;
    string[] ids;
    WorkerRole[] roles;
    float clock,nextAt;
    bool dirty=true;

    void OnEnable()
    {
        WorkerManager.OnRosterChanged+=Changed;WorkerManager.OnCampStockChanged+=Changed;
        WorkerManager.OnWorkerProduced+=Produced;SaveSystem.Loaded+=Changed;dirty=true;
    }
    void OnDisable()
    {
        WorkerManager.OnRosterChanged-=Changed;WorkerManager.OnCampStockChanged-=Changed;
        WorkerManager.OnWorkerProduced-=Produced;SaveSystem.Loaded-=Changed;
        HideActors();
    }
    void OnDestroy()
    {
        if(actors==null)return;
        foreach(var actor in actors)if(actor!=null)Destroy(actor.gameObject);
    }
    void Changed(){dirty=true;}
    void Ensure()
    {
        if(actors!=null)return;
        int count=WorkerBalance.CampSlots;
        actors=new WorkyardActor[count];assigned=new WorkyardStation[count];ids=new string[count];roles=new WorkerRole[count];
    }
    void HideActors()
    {
        if(actors==null)return;
        foreach(var actor in actors)if(actor!=null)actor.gameObject.SetActive(false);
    }
    void Produced(WorkerProduced p)
    {
        dirty=true;
        if(p.worker==null)return;
        var manager=WorkerManager.Instance;
        // A production event can arrive before the first actor refresh or just after reassignment.
        if(p.resource==CampResource.Logs && p.worker.role==WorkerRole.Lumberjack && manager!=null && manager.Sim!=null)
        {
            int index=0;
            for(int i=0;i<WorkerBalance.CampSlots;i++)
            {
                var worker=manager.SlotAt(i);
                if(worker==null || worker.role!=WorkerRole.Lumberjack)continue;
                if(worker.id==p.worker.id)
                {
                    foreach(var station in stations)
                        if(station!=null && station.role==WorkerRole.Lumberjack && station.index==index && station.tree!=null)
                            station.tree.Produced(WorkyardTree.Timing.CycleSeconds(manager.Sim.LumberRate(worker)));
                    break;
                }
                index++;
            }
        }
        if(actors==null)return;
        for(int i=0;i<actors.Length;i++)
            if(ids[i]==p.worker.id && actors[i]!=null)
                actors[i].Produced();
    }

    void LateUpdate()
    {
        Ensure();
        bool constructing=build!=null && !build.IsFinished && (build.IsBuilding || build.Progress>0.0001f);
        if(tent!=null && tent.IsBuilding)constructing=true;
        bool complete=tent!=null && tent.Stage>=2;
        if(abandoned!=null && abandoned.activeSelf==constructing)abandoned.SetActive(!constructing);
        if(frame!=null && frame.activeSelf!=constructing)frame.SetActive(constructing);
        var manager=WorkerManager.Instance;
        bool open=complete && camp!=null && camp.Unlocked && manager!=null && manager.Sim!=null;
        bool nearby=PlayerMovement.Instance!=null && (PlayerMovement.Instance.transform.position-transform.position).sqrMagnitude<=animationDistance*animationDistance;
        if(dirty || Time.unscaledTime>=nextAt)
        {
            dirty=false;nextAt=Time.unscaledTime+0.25f;
            RefreshActors(open?manager:null,nearby);
            if(stock!=null)stock.Refresh(open?manager:null);
        }
        float dt=Mathf.Min(Time.deltaTime,0.25f);clock+=dt;
        foreach(var station in stations)
            if(station!=null){station.Pose(clock,false);if(station.tree!=null)station.tree.Tick(dt);}
        if(!open || !nearby)return;
        for(int i=0;i<actors.Length;i++)
        {
            if(actors[i]==null || assigned[i]==null)continue;
            var worker=manager.SlotAt(i);
            bool working=manager.isActiveAndEnabled && WorkyardReadout.Activity(manager.Sim,worker,true)==WorkyardActivity.Working;
            float phase=worker!=null ? WorkyardTree.Timing.Phase(worker.logAcc) : 0;
            actors[i].Pose(clock,dt,working,i,phase);assigned[i].Pose(clock,working);
            if(assigned[i].tree!=null)assigned[i].tree.SetWork(phase,working);
        }
    }

    void RefreshActors(WorkerManager manager,bool nearby)
    {
        int[] perRole=new int[3];
        for(int i=0;i<actors.Length;i++)
        {
            WorkerState worker=manager!=null?manager.SlotAt(i):null;
            if(worker==null)
            {
                if(actors[i]!=null)actors[i].gameObject.SetActive(false);
                ids[i]=null;assigned[i]=null;continue;
            }
            int role=(int)worker.role;
            if(role<0 || role>=perRole.Length){if(actors[i]!=null)actors[i].gameObject.SetActive(false);continue;}
            int index=perRole[role]++;
            WorkyardStation station=null;
            foreach(var item in stations)if(item!=null && item.role==worker.role && item.index==index){station=item;break;}
            if(station==null || role<0 || role>=actorPrefabs.Length || actorPrefabs[role]==null)
            {if(actors[i]!=null)actors[i].gameObject.SetActive(false);continue;}
            if(actors[i]==null || roles[i]!=worker.role)
            {
                if(actors[i]!=null)Destroy(actors[i].gameObject);
                actors[i]=Instantiate(actorPrefabs[role],actorRoot!=null?actorRoot:transform,false);
                roles[i]=worker.role;
            }
            ids[i]=worker.id;assigned[i]=station;
            actors[i].name="Рабочий_"+worker.DisplayName;
            Transform point=station.actorPoint!=null?station.actorPoint:station.transform;
            actors[i].transform.SetPositionAndRotation(point.position,point.rotation);
            actors[i].gameObject.SetActive(nearby);
        }
    }
}
