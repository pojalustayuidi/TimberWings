using System;
using UnityEngine;

// Serialized rollback data only. Generation lives in Editor/TWRiver.
// References survive scene save/reload; no Start/Awake or runtime rebuilding.
[DisallowMultipleComponent]
public sealed class RiverArtState : MonoBehaviour
{
    [Serializable]
    public struct RendererState
    {
        public Renderer renderer;
        public bool enabled;
    }

    [Serializable]
    public struct ObjectState
    {
        public GameObject target;
        public bool active;
        public string originalName;
    }

    [HideInInspector] public int revision;
    [HideInInspector] public string generatedFolder;
    [HideInInspector] public RendererState[] renderers = Array.Empty<RendererState>();
    [HideInInspector] public ObjectState[] objects = Array.Empty<ObjectState>();
    [HideInInspector] public Transform bridge;
    [HideInInspector] public Matrix4x4 bridgeMatrix;
    [HideInInspector] public int lipRow;
    [HideInInspector] public int columns;
    [HideInInspector] public Vector3 outlet;
    [HideInInspector] public Vector3 source;
}
