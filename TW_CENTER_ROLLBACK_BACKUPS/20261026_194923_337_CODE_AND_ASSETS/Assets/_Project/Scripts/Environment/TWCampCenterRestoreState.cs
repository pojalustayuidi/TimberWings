using UnityEngine;

/// <summary>Serialized reference snapshot for exact Undo/Remove of the art-only compact pass.
/// No runtime behavior. Does not affect saves, gameplay or player controls.</summary>
[DisallowMultipleComponent]
public sealed class TWCampCenterRestoreState : MonoBehaviour
{
    public MeshFilter stage2, stage3, tripod, flame;
    public Mesh priorStage2, priorStage3, priorTripod, priorFlame;
    public MeshRenderer duplicateKettle;
    public bool duplicateKettleWasEnabled;
}
