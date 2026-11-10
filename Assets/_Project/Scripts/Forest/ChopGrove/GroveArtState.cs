using System;
using UnityEngine;

// Authored scenery ownership and editor rollback data. No runtime rebuilding.
[DisallowMultipleComponent]
public sealed class GroveArtState : MonoBehaviour
{
    [Serializable] public struct HiddenObject { public GameObject target; public bool active; }
    [HideInInspector] public Transform artRoot;
    [HideInInspector] public string generatedFolder;
    [HideInInspector] public HiddenObject[] hidden = Array.Empty<HiddenObject>();
    [HideInInspector] public bool hadBuilder;
    [HideInInspector] public string builderJson;
    [HideInInspector] public string protectedHash;
    [HideInInspector] public int resourceTrees;
    [HideInInspector] public int revision;

    public static bool HasAuthoredArt(GameObject marker)
    {
        var state = marker.GetComponent<GroveArtState>();
        return state != null && state.artRoot != null;
    }
}
