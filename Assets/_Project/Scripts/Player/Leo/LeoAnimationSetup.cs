using UnityEngine;

// Editor-installed reversible ownership record. Never generates art at runtime.
[DisallowMultipleComponent]
public sealed class LeoAnimationSetup : MonoBehaviour
{
    public LeoRig rig;
    public LeoAnimationController controller;
    public LeoModelPresentation previousPresentation;
    public bool presentationWasEnabled;
    public GameObject trailObject;
    public string backup;
}
