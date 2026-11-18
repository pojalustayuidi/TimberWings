using System.Collections.Generic;
using UnityEngine;

// CHOP-01: temporary scene-local resource, never serialized into the production save.
public sealed class ChopPrototypeLog : MonoBehaviour
{
    public static readonly List<ChopPrototypeLog> Active = new List<ChopPrototypeLog>();
    public static bool Enabled { get; private set; }
    public ResourceItem Source { get; private set; }
    public int Remaining { get; private set; }
    public int Xp { get; private set; }
    public bool Ready => Time.time >= readyAt;
    public bool Split => hits >= requiredHits;
    public float Progress => Mathf.Clamp01(hits / requiredHits);
    public Collider Solid { get; private set; }
    float readyAt, hits, requiredHits;
    int initialWood;
    float length, diameter;
    WorldTag tag;
    GameObject visual;
    Material bark, cut, marks;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Active.Clear(); Enabled = false; }

    public static void Toggle()
    {
        Enabled = !Enabled;
        SaveIndicator.ShowToast(Enabled ? "CHOP-01: новая рубка включена" : "CHOP-01: обычная рубка (оставшиеся стволы доступны)");
    }

    public static bool Supports(ResourceItem tree) => Enabled && tree != null && tree.respawn && tree.Size != TreeSize.Young;

    public static bool Pending(ResourceItem item)
    {
        if (item == null) return false;
        foreach (var log in Active) if (log != null && log.Source == item && log.Remaining > 0) return true;
        return false;
    }

    // A short, deterministic proxy trunk for the prototype. Find level ground near the stump.
    public static bool TryCreate(ResourceItem source, Transform actor, int wood, int xp)
    {
        if (source == null || wood <= 0 || Pending(source)) return false;
        Vector3 at = source.transform.position;
        Vector3 away = Camera.main != null ? at - Camera.main.transform.position : at - actor.position;
        away.y = 0f;
        if (away.sqrMagnitude < .001f) away = Vector3.forward;
        away.Normalize();
        float len = source.Size == TreeSize.Old ? 2.8f : 1.9f;
        float dia = Mathf.Clamp(source.TrunkRadius * 1.55f, .28f, .70f);
        Vector3 center = Vector3.zero, dir = away;
        bool found = false;
        foreach (float angle in new[] {0f, 45f, -45f, 90f, -90f, 135f, -135f, 180f})
        {
            Vector3 testDir = Quaternion.Euler(0, angle, 0) * away;
            Vector3 start = at + testDir * (source.TrunkRadius + .32f);
            Vector3 end = start + testDir * len;
            Vector3 mid = (start + end) * .5f;
            if (!Ground(start, at.y, actor, out float a) || !Ground(mid, at.y, actor, out float b) || !Ground(end, at.y, actor, out float c)) continue;
            if (Mathf.Max(a, Mathf.Max(b,c)) - Mathf.Min(a, Mathf.Min(b,c)) > .45f) continue;
            mid.y = Mathf.Max(a, Mathf.Max(b,c)) + dia * .5f + .015f;
            // Avoid spawning a solid trunk in the player or another upright trunk.
            bool occupied = false;
            foreach (var other in ResourceItem.Active)
            {
                if (other == null || other == source) continue;
                Vector3 d = other.transform.position - mid; d.y = 0;
                float along = Mathf.Clamp(Vector3.Dot(d,testDir), -len*.5f,len*.5f);
                if ((d-testDir*along).magnitude < other.TrunkRadius+dia*.5f+.1f) { occupied=true; break; }
            }
            Vector3 playerDelta = actor.position - mid; playerDelta.y = 0;
            if ((playerDelta-testDir*Mathf.Clamp(Vector3.Dot(playerDelta,testDir),-len*.5f,len*.5f)).magnitude < dia*.5f+.45f) occupied=true;
            if (occupied) continue;
            center=mid; dir=testDir; found=true; break;
        }
        if (!found) return false; // old harvesting path, no lost rewards on a cliff
        var go = new GameObject("CHOP01_FallenTrunk");
        go.SetActive(false);
        go.transform.SetPositionAndRotation(center, Quaternion.LookRotation(dir,Vector3.up));
        var log = go.AddComponent<ChopPrototypeLog>();
        log.Source = source; log.Remaining = wood; log.initialWood = wood; log.Xp=xp;
        log.length=len; log.diameter=dia; log.requiredHits=source.Size==TreeSize.Old?4f:2f;
        log.readyAt=Time.time+(source.growVisual ? Mathf.Max(.1f,GameBalance.GrowFallSeconds) : .35f);
        var box=go.AddComponent<BoxCollider>(); box.size=new Vector3(dia,dia,len); box.enabled=false; log.Solid=box;
        log.BuildVisual(false);
        go.SetActive(true);
        return true;
    }

    static bool Ground(Vector3 p, float baseY, Transform actor, out float y)
    {
        y=baseY;
        float best=float.PositiveInfinity;
        foreach(var hit in Physics.RaycastAll(p+Vector3.up*2.0f,Vector3.down,4f,~0,QueryTriggerInteraction.Ignore))
        {
            if (hit.collider==null || hit.collider.transform.IsChildOf(actor) || hit.collider.GetComponentInParent<ResourceItem>()!=null || hit.collider.GetComponentInParent<ChopPrototypeLog>()!=null) continue;
            if (hit.normal.y<.65f || Mathf.Abs(hit.point.y-baseY)>1.1f || hit.distance>=best) continue;
            best=hit.distance; y=hit.point.y;
        }
        return best<float.PositiveInfinity;
    }

    void OnEnable() { Active.Add(this); SaveSystem.Loaded += OnLoad; }
    void OnDisable() { Active.Remove(this); SaveSystem.Loaded -= OnLoad; }
    void OnLoad() { gameObject.SetActive(false); Destroy(gameObject); }
    void OnDestroy() { if(bark!=null) Destroy(bark); if(cut!=null) Destroy(cut); if(marks!=null) Destroy(marks); }

    void Start()
    {
        tag=WorldTag.Add(gameObject,true);
        tag.offset=Vector3.up*.75f;
        tag.SetFontSize(28f);
        Refresh();
    }

    void Update()
    {
        bool show=Ready;
        if (visual!=null && visual.activeSelf!=show) visual.SetActive(show);
        if (Solid!=null) Solid.enabled=show && !Split;
        if (tag!=null) tag.visible=show;
    }

    public Vector3 Closest(Vector3 point)
    {
        Vector3 d=point-transform.position;
        return transform.position+transform.forward*Mathf.Clamp(Vector3.Dot(d,transform.forward),-length*.5f,length*.5f);
    }

    public float Distance(Vector3 point)
    {
        Vector3 d=Closest(point)-point; d.y=0; return d.magnitude;
    }

    public bool Hit(float strength)
    {
        if (!Ready || Split) return false;
        hits=Mathf.Min(requiredHits,hits+Mathf.Max(.1f,strength));
        if (Split)
        {
            Solid.enabled=false;
            BuildVisual(true);
        }
        Refresh();
        if(tag!=null) tag.Punch(.17f);
        return Split;
    }

    public void Collected(int count)
    {
        if (count<=0) return;
        Remaining=Mathf.Max(0,Remaining-count);
        Xp=0;
        if (Remaining==0) { gameObject.SetActive(false); Destroy(gameObject); }
        else { BuildVisual(true); Refresh(); }
    }

    void Refresh()
    {
        if (tag==null) return;
        tag.SetText(Split ? "Брёвна: "+Remaining : "Разделай ствол · ЛКМ / E");
        tag.SetProgress(Split ? 1f : Progress);
    }

    Material MakeMat(Color col)
    {
        Shader sh=Shader.Find("Universal Render Pipeline/Lit");
        if(sh==null) sh=Shader.Find("Standard");
        if(sh==null) return null;
        return new Material(sh){color=col};
    }

    void Cylinder(string label,Transform parent,Vector3 pos,float len,float dia,Material mat)
    {
        var g=GameObject.CreatePrimitive(PrimitiveType.Cylinder); g.name=label;
        var collider=g.GetComponent<Collider>(); if(collider!=null){collider.enabled=false; Destroy(collider);}
        g.transform.SetParent(parent,false);g.transform.localPosition=pos;g.transform.localRotation=Quaternion.Euler(90,0,0);
        g.transform.localScale=new Vector3(dia,len*.5f,dia);
        if(mat!=null)g.GetComponent<Renderer>().sharedMaterial=mat;
    }

    void BuildVisual(bool split)
    {
        if(visual!=null){visual.SetActive(false);Destroy(visual);}
        if(bark==null)bark=MakeMat(new Color(.35f,.20f,.105f));
        if(cut==null)cut=MakeMat(new Color(.73f,.48f,.25f));
        if(marks==null)marks=MakeMat(new Color(.91f,.68f,.34f));
        visual=new GameObject("CHOP01_Visual"); visual.transform.SetParent(transform,false);
        if(!split)
        {
            Cylinder("Bark",visual.transform,Vector3.zero,length,diameter,bark);
            foreach(float end in new[]{-1f,1f}) Cylinder("Cut",visual.transform,Vector3.forward*end*(length*.5f+.005f),.022f,diameter*.88f,cut);
            for(int i=1;i<=3;i++) Cylinder("Cut mark",visual.transform,Vector3.forward*(length*(i/4f-.5f)),.027f,diameter*1.012f,marks);
        }
        else
        {
            int shown=Mathf.Clamp(Remaining,1,6);
            for(int i=0;i<shown;i++)
            {
                float z=(i/2-(shown-1)/4f)*.52f;
                float x=(i%2==0?-.18f:.18f);
                Vector3 p=new Vector3(x,-diameter*.5f+.13f,z);
                Cylinder("Log",visual.transform,p,.44f,.25f,bark);
                foreach(float end in new[]{-1f,1f})Cylinder("End",visual.transform,p+Vector3.forward*end*.225f,.018f,.22f,cut);
            }
        }
        visual.SetActive(Ready);
    }
}
