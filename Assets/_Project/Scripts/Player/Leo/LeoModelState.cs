using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class LeoModelState : MonoBehaviour
{
    [Serializable]public class RendererState{public Renderer target;public bool enabled;}
    [Serializable]public class AnimatorState{public Animator target;public AnimatorCullingMode culling;public bool rootMotion;}
    public PlayerMovement player;
    public Transform previousModel,previousTurnRoot;
    public GameObject model;
    public Speaker speaker;
    public bool captured;
    public string backup;
    public List<RendererState> renderers=new List<RendererState>();
    public List<AnimatorState> animators=new List<AnimatorState>();
}
