using System;
using System.Collections.Generic;
using UnityEngine;

// Scene setup snapshot only. Does not generate anything during play.
public sealed class OskarAnimationState : MonoBehaviour
{
    [Serializable]public sealed class BehaviourState{public Behaviour target;public bool enabled;}
    public OskarRig rig;
    public OskarIdle actor;
    public GameObject workingPencil;
    public bool captured,createdActor,previousEnabled;
    public Transform previousPencil;
    public Speaker previousSpeaker;
    public DialogueSequence previousDialogue;
    public DialogueManager previousManager;
    public string backup;
    public List<BehaviourState> disabled=new List<BehaviourState>();
}
