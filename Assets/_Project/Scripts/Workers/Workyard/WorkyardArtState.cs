using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class WorkyardArtState : MonoBehaviour
{
    [Serializable] public struct Hidden {public GameObject target;public bool active;}
    [Serializable] public struct Collision {public Collider target;public bool enabled;}
    [HideInInspector] public GameObject siteArt,builtArt,physicsRoot;
    [HideInInspector] public Hidden[] hidden=Array.Empty<Hidden>();
    [HideInInspector] public Collision[] collisions=Array.Empty<Collision>();
    [HideInInspector] public Transform[] oldCurtains,oldFlags,oldSwingers;
    [HideInInspector] public Light[] oldLights;
    [HideInInspector] public string folder,logicHash;
    [HideInInspector] public bool createdCamp,assignedDatabase;
    [HideInInspector] public GameObject createdManager;
    [HideInInspector] public WorkerDatabase originalDatabase;
    [HideInInspector] public WorkerManager manager;
    [HideInInspector] public WorkerCamp camp;
    [HideInInspector] public WorkyardView view;
}
