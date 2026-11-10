using System;
using UnityEngine;

// Editor-authored visual ownership and rollback references. No runtime logic.
[DisallowMultipleComponent]
public sealed class HearthArtState : MonoBehaviour
{
    [Serializable] public struct HiddenObject { public GameObject target; public bool active; }
    [Serializable] public struct HiddenCollider { public Collider target; public bool enabled; }
    [HideInInspector] public Transform campfire;
    [HideInInspector] public GameObject[] visuals = Array.Empty<GameObject>();
    [HideInInspector] public HiddenObject[] hidden = Array.Empty<HiddenObject>();
    [HideInInspector] public HiddenCollider[] colliders = Array.Empty<HiddenCollider>();
    [HideInInspector] public string generatedFolder;
    [HideInInspector] public string logicHash;
}
