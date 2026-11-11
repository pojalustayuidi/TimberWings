using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MysticArtState : MonoBehaviour
{
    [Serializable] public struct Hidden { public GameObject target; public bool active; }
    [Serializable] public struct Disabled { public Behaviour target; public bool enabled; }
    [Serializable] public struct ColliderRecord { public Collider target; public string json; }
    [Serializable] public struct RendererRecord { public Renderer target; public bool enabled; }
    [Serializable] public struct LodRecord { public LODGroup target; public bool enabled; }
    [Serializable] public struct EconomyRecord
    {
        public bool valid;
        public float gatherTime;
        public bool respawn,useBalanceRespawn,growVisual;
        public Vector2 respawnSeconds;
    }
    [Serializable] public sealed class TreeRecord
    {
        public ResourceItem target;
        public Transform parent;
        public Vector3 position,scale;
        public Quaternion rotation;
        public Vector3 worldPosition,worldScale;
        public Quaternion worldRotation;
        public string json,speciesJson;
        public bool created,hadSpecies,active;
        public TreeSize size;
        public EconomyRecord economy;
        public ColliderRecord[] collisions=new ColliderRecord[0];
        public RendererRecord[] renderers=new RendererRecord[0];
        public LodRecord[] lods=new LodRecord[0];
        public UnityEngine.Object[] added=new UnityEngine.Object[0];
    }
    public LodRecord[] elderLods=new LodRecord[0];
    public GameObject art;
    public LandmarkTree elder;
    public string elderJson,folder;
    public float groundRadiusX=18f,groundRadiusZ=17f,zoneRadius=18f;
    public Camera effectCamera;
    public bool adjustedCamera,originalPostProcessing,createdCameraData;
    public int originalVolumeMask;
    public Hidden[] hidden=new Hidden[0];
    public Disabled[] disabled=new Disabled[0];
    public ColliderRecord[] worldCollisions=new ColliderRecord[0];
    public RendererRecord[] worldRenderers=new RendererRecord[0];
    public TreeRecord[] trees=new TreeRecord[0];
}
