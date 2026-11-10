using UnityEngine;

// NPC-owned scenery. No ResourceItem, player rewards or changes to production.
public sealed class WorkyardTree : MonoBehaviour
{
    public Transform crown;
    public GameObject stump;
    Quaternion restRotation;
    Vector3 restScale;
    bool cached;
    float age,cycle=5f;
    bool falling;
    void Cache()
    {
        if(cached || crown==null)return;
        cached=true;restRotation=crown.localRotation;restScale=crown.localScale;
    }
    public void Produced(float cycleSeconds)
    {
        Cache();cycle=Mathf.Clamp(cycleSeconds,0.08f,120f);age=0;falling=true;
    }
    public void Tick(float dt)
    {
        if(crown==null)return;
        Cache();SetWork(0,false);
        if(!falling)return;
        age+=Mathf.Max(0,dt);
        float fall=Timing.FallSeconds(cycle),grow=Timing.GrowStart(cycle),ready=Timing.ReadySeconds(cycle);
        bool hidden=age>=fall && age<grow;
        crown.gameObject.SetActive(!hidden);
        if(stump!=null)stump.SetActive(age<ready);
        crown.localRotation=restRotation*Quaternion.Euler(age<fall ? Mathf.SmoothStep(0,68,age/fall) : 0,0,0);
        crown.localScale=restScale*(age>=grow && age<ready ? Mathf.Lerp(0.12f,1f,(age-grow)/(ready-grow)) : 1f);
        if(age>=ready)falling=false;
    }
    public void SetWork(float progress,bool working)
    {
        if(crown==null || falling)return;
        Cache();float phase=Timing.Phase(progress);
        crown.localRotation=restRotation*Quaternion.Euler(0,0,working ? Mathf.Sin(phase*6*Mathf.PI)*Mathf.Lerp(0.3f,1.2f,phase) : 0);
    }

    // Kept in an existing runtime script so updates do not depend on importing an extra timing file.
    // These functions only read values; CampSimulation remains the sole production clock.
    public static class Timing
    {
        public static float Phase(double progress)
        {
            if(double.IsNaN(progress) || double.IsInfinity(progress))return 0f;
            return Mathf.Clamp01((float)progress);
        }
        public static float CycleSeconds(double logsPerSecond)
        {
            if(double.IsNaN(logsPerSecond) || double.IsInfinity(logsPerSecond) || logsPerSecond<=0)return 5f;
            return Mathf.Clamp((float)(1.0/logsPerSecond),0.08f,120f);
        }
        public static float FallSeconds(float cycle){return Mathf.Min(cycle*0.10f,0.45f);}
        public static float GrowStart(float cycle){return Mathf.Min(cycle*0.16f,0.75f);}
        public static float ReadySeconds(float cycle){return GrowStart(cycle)+Mathf.Min(cycle*0.12f,0.60f);}
        public static float LumberSwing(float progress)
        {
            float stroke=Mathf.Repeat(Mathf.Clamp01(progress)*3f,1f);
            return stroke<0.7f ? Mathf.Lerp(-15,-80,Mathf.SmoothStep(0,1,stroke/0.7f)) :
                Mathf.Lerp(-80,12,Mathf.SmoothStep(0,1,(stroke-0.7f)/0.3f));
        }
    }
}
