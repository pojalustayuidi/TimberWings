// Assets/_Project/Scripts/Build/Hut/HutFlicker.cs   (НЕ в папке Editor!)
// Живое пламя: мягко «дышит» масштабом и чуть покачивается. Для очага в хижине и костровой ямы во дворе.
// Свет мерцает сам: Point Light с именем «LanternLight…» подхватывает твоя система FireSource.
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("BuilderSim/Hut Flicker")]
public class HutFlicker : MonoBehaviour
{
    public Transform[] flames = new Transform[0];
    public float speed = 6f;
    [Range(0f, 0.6f)] public float amount = 0.22f;
    public float sway = 5f;

    Vector3[] baseScale;
    Quaternion[] baseRot;
    float seed;

    void Awake()
    {
        seed = Random.value * 20f;
        baseScale = new Vector3[flames.Length];
        baseRot = new Quaternion[flames.Length];
        for (int i = 0; i < flames.Length; i++)
        {
            if (flames[i] == null) continue;
            baseScale[i] = flames[i].localScale;
            baseRot[i] = flames[i].localRotation;
        }
    }

    void OnDisable()
    {
        if (baseScale == null) return;
        for (int i = 0; i < flames.Length; i++)
        {
            if (flames[i] == null) continue;
            flames[i].localScale = baseScale[i];
            flames[i].localRotation = baseRot[i];
        }
    }

    void Update()
    {
        float t = Time.time * speed + seed;
        for (int i = 0; i < flames.Length; i++)
        {
            var f = flames[i];
            if (f == null) continue;
            float n = Mathf.PerlinNoise(t + i * 3.1f, i * 1.7f);
            float s = 1f + (n - 0.5f) * 2f * amount;
            Vector3 b = baseScale[i];
            f.localScale = new Vector3(b.x * (1f + (1f - s) * 0.4f), b.y * s, b.z * (1f + (1f - s) * 0.4f));
            float r = (Mathf.PerlinNoise(t * 0.7f + 11f + i, 3.3f) - 0.5f) * 2f * sway;
            f.localRotation = baseRot[i] * Quaternion.Euler(r, 0f, -r * 0.6f);
        }
    }
}
