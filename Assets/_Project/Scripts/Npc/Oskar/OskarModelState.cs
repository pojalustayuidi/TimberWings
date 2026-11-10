using System;
using System.Collections.Generic;
using UnityEngine;

// Snapshot for safe editor replacement/rollback. Does nothing in Play Mode.
public sealed class OskarModelState : MonoBehaviour
{
    [Serializable]public sealed class RendererState {public Renderer target;public bool enabled;}
    [Serializable]public sealed class BehaviourState {public Behaviour target;public bool enabled;}
    public Speaker target;
    public GameObject model;
    public bool createdNpc,captured,previousFace;
    public Transform previousTurnRoot;
    public Vector3 previousBubble,markerPosition;
    public NpcMarker marker;
    public float markerSize,markerBob;
    public string backup;
    public List<RendererState> renderers=new List<RendererState>();
    public List<BehaviourState> behaviours=new List<BehaviourState>();
    public List<Component> added=new List<Component>();
}
