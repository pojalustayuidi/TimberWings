using UnityEngine;

/// <summary>
/// Compatibility stub for the old hut safety patch.
/// IMPORTANT: The old version teleported the player when the player pivot was
/// lower than an arbitrary yard height, even while the player was safe.
/// This component intentionally NEVER moves the player or changes Rigidbody state.
/// An editor repair pass removes it from the hut; the stub makes even un-repaired
/// scenes safe immediately after replacing the source file.
/// The island's existing IslandEdgeGuard is responsible for genuine falls.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("")]
public sealed class HutLocalFallGuard : MonoBehaviour
{
    // Retained for compatibility with already serialized scene components.
    public HutStages hut;
    [Min(0.2f)] public float recoveryDepth = 0.65f;
}
