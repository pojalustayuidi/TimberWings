using System;
using System.Collections.Generic;
using UnityEngine;

// Scene snapshot for the editor builder. No runtime generation.
public sealed class MiraLifeSetup : MonoBehaviour
{
    [Serializable] public class BehaviourState { public Behaviour target; public bool enabled; }
    public Speaker target;
    public MiraRig rig;
    public MiraPresence presence;
    public MiraMotion motion;
    public bool captured, previousFace;
    public Vector3 previousBubble, previousMarkerPosition;
    public float previousMarkerSize, previousMarkerBob;
    public NpcMarker previousMarker, marker;
    public Sprite previousSprite;
    public string backup;
    public List<BehaviourState> disabled=new List<BehaviourState>();
    public List<Component> added=new List<Component>();
    public List<GameObject> generated=new List<GameObject>();
    void Start()
    {
        // This patch belongs only to Mira, including when legacy StoryFlow clones her as Oskar.
        if(target==null||string.Equals(target.displayName,"Мира",StringComparison.OrdinalIgnoreCase))return;
        if(presence!=null)presence.enabled=false;if(motion!=null)motion.enabled=false;
        target.faceNearbyPlayer=previousFace;target.bubbleOffset=previousBubble;
        var seq=target.GetComponent<DialogueSequence>();if(seq!=null){seq.marker=previousMarker;seq.reminderHandledExternally=false;}
        if(marker!=null){marker.size=previousMarkerSize;marker.bobHeight=previousMarkerBob;marker.transform.localPosition=previousMarkerPosition;marker.GetComponent<SpriteRenderer>().sprite=previousSprite;}
        foreach(var go in generated)if(go!=null)go.SetActive(false);
        foreach(var state in disabled)if(state.target!=null)state.target.enabled=state.enabled;
    }
}
