using UnityEngine;

// Decorative family following a baked loop inside a quiet stretch of river.
// No physics, spawning, searches, material allocations or save data at runtime.
[DisallowMultipleComponent]
public sealed class RiverDuckMotion : MonoBehaviour
{
    public Vector3[] route;
    public float secondsPerLoop = 75f;
    public float phase;

    void LateUpdate() { PlaceAt(Time.timeSinceLevelLoad); }

    public void PlaceAt(float seconds)
    {
        if (route == null || route.Length < 4) return;
        float cycle = seconds / Mathf.Max(20f, secondsPerLoop) + phase;
        Vector3 p = Sample(cycle);
        Vector3 tangent = Sample(cycle + 0.002f) - p;
        tangent.y = 0f;
        p.y += Mathf.Sin(seconds * 1.7f + phase * 20f) * 0.012f;
        transform.localPosition = p;
        if (tangent.sqrMagnitude > 0.00001f)
            transform.localRotation = Quaternion.LookRotation(tangent, Vector3.up) *
                                      Quaternion.Euler(0f, 0f, Mathf.Sin(seconds * 1.4f + phase * 13f) * 1.4f);
    }

    Vector3 Sample(float cycle)
    {
        float f = Mathf.Repeat(cycle, 1f) * route.Length;
        int i = Mathf.FloorToInt(f);
        float t = f - i, t2 = t * t, t3 = t2 * t;
        Vector3 a = route[(i + route.Length - 1) % route.Length], b = route[i];
        Vector3 c = route[(i + 1) % route.Length], d = route[(i + 2) % route.Length];
        return 0.5f * (2f * b + (-a + c) * t + (2f * a - 5f * b + 4f * c - d) * t2 +
                       (-a + 3f * b - 3f * c + d) * t3);
    }
}
