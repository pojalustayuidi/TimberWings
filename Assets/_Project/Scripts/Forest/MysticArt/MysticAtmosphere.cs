using UnityEngine;
using UnityEngine.Rendering;

// Animates references baked by the editor. Never creates geometry, effects, lights or profiles.
[DisallowMultipleComponent]
public sealed class MysticAtmosphere : MonoBehaviour
{
    public float radius=18f,feather=5f;
    public Volume dayVolume,nightVolume;
    public Light[] lights=new Light[0];
    public ParticleSystem[] particles=new ParticleSystem[0];
    public Renderer[] runes=new Renderer[0],apparitions=new Renderer[0];
    public TextMesh[] nightWords=new TextMesh[0];
    public Transform swing,motes;
    public AudioSource ambience;
    public float previewNight=-1f;
    float weight,clock,nextCheck;
    Quaternion swingRest,motesRest;
    bool cached;
    MaterialPropertyBlock block;
    static readonly int Alpha=Shader.PropertyToID("_Alpha");
    public static float Boundary(float distance,float radius,float feather)
    {
        float x=Mathf.Clamp01((radius-distance)/Mathf.Max(0.1f,feather));return x*x*(3-2*x);
    }
    void OnEnable()
    {
        weight=0;clock=0;cached=false;
        if(block==null)block=new MaterialPropertyBlock();
        ResetView();
    }
    void Cache()
    {
        if(cached)return;cached=true;
        if(swing!=null)swingRest=swing.localRotation;
        if(motes!=null)motesRest=motes.localRotation;
    }
    void Update()
    {
        Cache();float dt=Mathf.Min(Time.deltaTime,0.1f);clock+=dt;
        var player=PlayerMovement.Instance;
        Vector3 p=player!=null?player.transform.position:transform.position+Vector3.right*999;
        Vector3 delta=p-transform.position;delta.y=0;
        float target=player!=null?Boundary(delta.magnitude,radius,feather):0;
        weight=Mathf.MoveTowards(weight,target,dt/1.15f);
        float night=previewNight>=0?Mathf.Clamp01(previewNight):Mathf.Clamp01(DayNightCycle.Night01+DayNightCycle.Twilight01*0.45f);
        float effects=VibeSettings.ReduceEffects?0.55f:1f;
        if(dayVolume!=null)dayVolume.weight=weight*(1-night)*effects;
        if(nightVolume!=null)nightVolume.weight=weight*night*effects;
        if(ambience!=null)ambience.volume=weight*Mathf.Lerp(0.035f,0.085f,night)*VibeSettings.AmbientVolume;
        for(int i=0;i<lights.Length;i++)if(lights[i]!=null)
        {
            lights[i].enabled=delta.sqrMagnitude<46*46;
            lights[i].intensity=Mathf.Lerp(0.30f,1.55f,night)*(0.92f+0.08f*Mathf.Sin(clock*0.62f+i*1.7f));
        }
        foreach(var r in runes)Set(r,Alpha,Mathf.Lerp(0.25f,0.8f,night));
        foreach(var text in nightWords)if(text!=null)
        {Color c=text.color;c.a=weight*Mathf.SmoothStep(0,1,Mathf.InverseLerp(0.35f,0.85f,night))*0.64f;text.color=c;}
        foreach(var r in apparitions)if(r!=null)
        {
            float distance=Vector3.Distance(p,r.transform.position);
            float seen=Mathf.SmoothStep(0,1,Mathf.InverseLerp(3.5f,7f,distance));
            Set(r,Alpha,weight*night*seen*(VibeSettings.ReduceEffects?0:0.14f));
        }
        if(swing!=null)swing.localRotation=swingRest*Quaternion.Euler(Mathf.Sin(clock*0.72f)*Mathf.Lerp(0.6f,2.6f,night),0,0);
        if(motes!=null)motes.localRotation=motesRest*Quaternion.Euler(0,clock*2.3f,0);
        if(Time.unscaledTime<nextCheck)return;nextCheck=Time.unscaledTime+0.4f;
        bool near=player!=null && delta.sqrMagnitude<45*45;
        for(int i=0;i<particles.Length;i++)if(particles[i]!=null)
        {
            var emission=particles[i].emission;
            emission.rateOverTime=near?((i==0?0.8f:i==1?2.2f:0.6f)*Mathf.Lerp(0.6f,1.25f,night)*effects):0;
        }
    }
    void Set(Renderer renderer,int property,float value)
    {
        if(renderer==null)return;
        renderer.GetPropertyBlock(block);block.SetFloat(property,value);renderer.SetPropertyBlock(block);
    }
    void ResetView()
    {
        if(dayVolume!=null)dayVolume.weight=0;if(nightVolume!=null)nightVolume.weight=0;
        if(ambience!=null)ambience.volume=0;
        foreach(var light in lights)if(light!=null)light.enabled=false;
        foreach(var ps in particles)if(ps!=null){var emission=ps.emission;emission.rateOverTime=0;}
        if(block!=null)foreach(var r in apparitions)Set(r,Alpha,0);
        foreach(var text in nightWords)if(text!=null){Color c=text.color;c.a=0;text.color=c;}
    }
    void OnDisable()
    {
        ResetView();if(!cached)return;
        if(swing!=null)swing.localRotation=swingRest;if(motes!=null)motes.localRotation=motesRest;
    }
}
