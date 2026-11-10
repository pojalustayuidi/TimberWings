using System;
using UnityEngine;
using UnityEngine.Rendering;

// Only draws editor-baked meshes. Does not own interaction or alter NPC materials.
[DefaultExecutionOrder(400),DisallowMultipleComponent]
public sealed class NpcDialogueOutline : MonoBehaviour
{
    [Serializable] public sealed class Part { public Mesh mesh; public MeshRenderer renderer; public Transform transform; }
    [Serializable] public sealed class Actor { public Speaker speaker; public Part[] parts; }
    public Actor[] actors;
    public Material halo,edge;
    public DialoguePolishView view;
    [Range(.5f,5f)] public float width=2.2f;
    public Color edgeColor=new Color(1f,.91f,.62f,1f),haloColor=new Color(1f,.66f,.20f,.16f);
    [Range(2f,16f)] public float fadeSpeed=7f;
    MaterialPropertyBlock properties;
    Camera main;
    int selected=-1;
    float opacity;
    int widthId,colorId;
    public Speaker Selected { get { return selected>=0&&actors!=null&&selected<actors.Length&&actors[selected]!=null?actors[selected].speaker:null; } }
    void Awake(){properties=new MaterialPropertyBlock();widthId=Shader.PropertyToID("_NpcWidth");colorId=Shader.PropertyToID("_NpcColor");}
    void OnEnable(){SaveSystem.Loaded+=ResetFocus;}
    void OnDisable(){SaveSystem.Loaded-=ResetFocus;ResetFocus();}
    void ResetFocus(){selected=-1;opacity=0;}
    int Index(Speaker speaker)
    {
        if(speaker==null||speaker.GetComponent<PlayerMovement>()!=null||actors==null)return -1;
        for(int i=0;i<actors.Length;i++)if(actors[i]!=null&&actors[i].speaker==speaker&&speaker.isActiveAndEnabled)return i;
        return -1;
    }
    int Target()
    {
        if(SaveSystem.IsLoading||BuildingController.AnyRevealing||BuildingController.AnyRestorePending)return -1;
        if(DialogueSequence.AnyPlaying)
            return DialogueSequence.Focus!=null?Index(DialogueSequence.Focus.GetComponentInParent<Speaker>()):-1;
        if(DialogueManager.IsBusy)
            return view!=null&&view.speakerName!=null?Index(Speaker.Find(view.speakerName.text)):-1;
        if(MobileInput.ModalOpen)return -1;
        var current=InteractionHub.Current;
        return current!=null&&current.Anchor!=null&&current.CanInteract?Index(current.Anchor.GetComponentInParent<Speaker>()):-1;
    }
    void LateUpdate()
    {
        if(properties==null||edge==null||edge.shader==null||!edge.shader.isSupported)return;
        int wanted=Target();float dt=Mathf.Min(.1f,Time.unscaledDeltaTime);
        if(wanted!=selected)
        {
            opacity=Mathf.MoveTowards(opacity,0,dt*fadeSpeed);
            if(opacity<=.001f){selected=wanted;opacity=0;}
        }
        else opacity=Mathf.MoveTowards(opacity,wanted>=0?1:0,dt*fadeSpeed);
        if(selected<0||opacity<=.001f||Selected==null)return;
        if(main==null||!main.isActiveAndEnabled)main=Camera.main;
        if(main==null)return;
        if(actors[selected].parts==null)return;
        float pixels=width*Mathf.Clamp(main.pixelHeight/1080f,.65f,1.5f);
        // Static highlight: no flashing/pulsing, including with ReduceEffects enabled.
        foreach(var part in actors[selected].parts)
        {
            if(part==null||part.mesh==null||part.renderer==null||part.transform==null||!part.renderer.enabled||!part.renderer.gameObject.activeInHierarchy)continue;
            int layer=part.renderer.gameObject.layer;if((main.cullingMask&(1<<layer))==0)continue;
            if(halo!=null&&!VibeSettings.ReduceEffects)Draw(part,halo,haloColor,pixels+2.1f,layer);
            Draw(part,edge,edgeColor,pixels,layer);
        }
    }
    void Draw(Part part,Material material,Color color,float pixels,int layer)
    {
        properties.Clear();color.a*=opacity;properties.SetColor(colorId,color);properties.SetFloat(widthId,pixels);
        for(int sub=0;sub<part.mesh.subMeshCount;sub++)
            Graphics.DrawMesh(part.mesh,part.transform.localToWorldMatrix,material,layer,main,sub,properties,ShadowCastingMode.Off,false,null,LightProbeUsage.Off);
    }
}
