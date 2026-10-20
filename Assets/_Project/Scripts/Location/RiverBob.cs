using UnityEngine;

/// <summary>Лёгкое покачивание на воде (кувшинки, упавшие доски).</summary>
public class RiverBob : MonoBehaviour
{
    public float amplitude = 0.03f;
    public float speed = 1.2f;
    public float tilt = 2f;

    Vector3 p0;
    Quaternion r0;
    float phase;

    void Start()
    {
        p0 = transform.localPosition;
        r0 = transform.localRotation;
        phase = Random.value * 6.283f;
    }

    void Update()
    {
        float t = Time.time * speed + phase;
        transform.localPosition = p0 + Vector3.up * (Mathf.Sin(t) * amplitude);
        transform.localRotation = r0 * Quaternion.Euler(Mathf.Sin(t * 0.8f) * tilt, 0f, Mathf.Cos(t * 0.7f) * tilt);
    }
}