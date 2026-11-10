using System.Collections.Generic;
using UnityEngine;

// New bridge placement data. No dependency on the old world generator.
// Attach to GateBridge. The transform is the entrance at the current shore;
// its blue (forward/Z) axis points across the gap towards the new island.
[DisallowMultipleComponent]
public sealed class GateBridgePlacement : MonoBehaviour
{
    [Header("Entrance: this transform's position and forward direction")]
    [Min(2f)] public float shoreRadius = 12f;
    [Min(1f)] public float gateWidth = 4f;
    [Min(0f)] public float edgeInset = 0.3f;
    [Min(0.1f)] public float wallThickness = 0.45f;
    [Tooltip("Optional old gate blocker. It is disabled temporarily during validation.")]
    public Transform GateRoot;
    [Header("Material and colors for the destination island")]
    public Material rockMaterial;
    public Color grassTop = new Color(.39f, .58f, .31f);
    public Color grassDark = new Color(.23f, .41f, .24f);
    public Color soil = new Color(.46f, .34f, .26f);
    public Color rockWarm = new Color(.48f, .43f, .40f);
    public Color rockMid = new Color(.39f, .38f, .42f);
    public Color rockDark = new Color(.25f, .27f, .33f);

    public float GroundY => transform.position.y;

    public Vector2? GetGatePosition() => new Vector2(transform.position.x, transform.position.z);

    // Compatibility contour, only used by the bridge planner. The current
    // island is NOT regenerated or modified in any way.
    public List<Vector2> GetEdgeLoop()
    {
        var forward = transform.forward;
        var n = new Vector2(forward.x, forward.z).normalized;
        if (n.sqrMagnitude < .5f) n = Vector2.up;
        Vector2 center = new Vector2(transform.position.x, transform.position.z) - n * shoreRadius;
        var points = new List<Vector2>(64);
        for (int i = 0; i < 64; i++)
        {
            float a = i * Mathf.PI * 2f / 64;
            points.Add(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * shoreRadius);
        }
        return points;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(transform.position, .45f);
        Gizmos.DrawRay(transform.position, transform.forward * 8f);
    }
}
