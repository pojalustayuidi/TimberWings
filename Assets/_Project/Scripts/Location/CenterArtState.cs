using System;
using UnityEngine;

// Persistent editor rollback/QA references only. No runtime generation or logic.
[DisallowMultipleComponent]
public sealed class CenterArtState : MonoBehaviour
{
    [Serializable]
    public struct HiddenObject
    {
        public GameObject target;
        public bool active;
    }

    [HideInInspector] public int revision;
    [HideInInspector] public string generatedFolder;
    [HideInInspector] public HiddenObject[] hidden = Array.Empty<HiddenObject>();
    [HideInInspector] public Transform campfire;
    [HideInInspector] public string campfireHash;
    [HideInInspector] public Transform marker;
    [HideInInspector] public Vector3 markerPosition;
    [HideInInspector] public int benches;
    [HideInInspector] public int lanterns;
}
