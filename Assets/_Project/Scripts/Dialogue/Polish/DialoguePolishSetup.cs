using System;
using System.Collections.Generic;
using UnityEngine;

// Serialized editor migration snapshot; no runtime generation or update loop.
public sealed class DialoguePolishSetup : MonoBehaviour
{
    [Serializable] public class BehaviourState { public Behaviour target; public bool enabled; }
    [Serializable] public class ObjectState { public GameObject target; public bool active; }
    public DialogueManager manager;
    public StoryFlow story;
    public GameObject viewRoot, counterRoot, thoughtsRoot;
    public bool captured, ownsManager, ownsBabble;
    public bool previousBuildingTrade;
    public TradingStall previousMarketTrading;
    public GameObject previousMerchant;
    public DialoguePolishView previousView;
    public DialogueBabble previousBabble;
    public AudioSource previousBabbleSource;
    public List<BehaviourState> behaviours = new List<BehaviourState>();
    public List<ObjectState> objects = new List<ObjectState>();
    public List<Behaviour> addedComponents = new List<Behaviour>();
    public string backup;
}
