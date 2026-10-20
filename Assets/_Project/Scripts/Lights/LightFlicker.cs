// Assets/_Project/Scripts/Lights/LightFlicker.cs (НЕ в Editor)
// ЕДИНЫЙ источник мерцания в проекте. Раньше мерцание костра жило в двух местах (CampfireFlicker и LocationAmbient.CollectFire),
// и пламя мерцало дважды. Теперь вся математика здесь, остальные только её вызывают:
//   • LightFlicker.Evaluate(пресет, время, seed): множитель яркости ~1 (его берёт FireSource для света, свечения и emissive);
//   • компонент LightFlicker: мерцает свой Light и/или масштаб языков пламени (нужен костру и самодельным сценам).
// Пресеты: Campfire (резкое, живое), Lantern (тёплое, медленное), Window (почти ровное), Garland (лёгкая рябь), Steady (без мерцания).
using UnityEngine;

public enum FlickerPreset { Campfire = 0, Lantern = 1, Window = 2, Garland = 3, Steady = 4 }

[DisallowMultipleComponent]
[AddComponentMenu("BuilderSim/Light Flicker")]
public class LightFlicker : MonoBehaviour
{
    [Header("Пресет")]
    public FlickerPreset preset = FlickerPreset.Campfire;

    [Header("Что мерцает (всё необязательно)")]
    [Tooltip("Свет, чья яркость мерцает. Если у света есть FireSource, яркостью управляет он, а здесь можно оставить пусто")]
    public Light targetLight;
    [Tooltip("Языки пламени: мерцает их масштаб")]
    public Transform[] flames;

    [Header("Свои числа (вместо пресета)")]
    public bool customize;
    [Range(0f, 1f)] public float amount = 0.35f;    // размах яркости: ±amount
    public float speed = 6f;
    public float scaleAmount = 0.08f;               // размах масштаба пламени

    float baseIntensity = 1f;
    Vector3[] baseScale = new Vector3[0];
    float seed;
    bool init;

    // ---------------------------------------------------------------- общая математика
    // множитель яркости около 1 (Campfire примерно 0.65..1.35, Lantern 0.9..1.1, Window 0.95..1.05)
    public static float Evaluate(FlickerPreset p, float t, float seed)
    {
        switch (p)
        {
            case FlickerPreset.Campfire:
                return 1f + (Mathf.PerlinNoise(seed, t * 7f) - 0.5f) * 0.7f + (Mathf.PerlinNoise(seed + 11f, t * 17f) - 0.5f) * 0.25f;
            case FlickerPreset.Lantern:
                return 1f + (Mathf.PerlinNoise(seed, t * 2.2f) - 0.5f) * 0.16f + (Mathf.PerlinNoise(seed + 5f, t * 9f) - 0.5f) * 0.05f;
            case FlickerPreset.Window:
                return 1f + (Mathf.PerlinNoise(seed, t * 0.9f) - 0.5f) * 0.10f;
            case FlickerPreset.Garland:
                return 1f + (Mathf.PerlinNoise(seed, t * 1.6f) - 0.5f) * 0.12f;
            default:
                return 1f;
        }
    }

    // формула старого CampfireFlicker: ±amount вокруг 1
    public static float EvaluateCustom(float t, float seed, float amount, float speed)
    {
        return 1f + (Mathf.PerlinNoise(seed, t * speed) - 0.5f) * 2f * amount;
    }

    // сколько яркости остаётся днём (свет и glow на солнце почти не видны, костёр виден всегда)
    public static float DayFactor(FlickerPreset p)
    {
        switch (p)
        {
            case FlickerPreset.Campfire: return 0.7f;
            case FlickerPreset.Lantern: return 0.25f;
            case FlickerPreset.Window: return 0.05f;
            case FlickerPreset.Garland: return 0.15f;
            default: return 1f;
        }
    }

    // ---------------------------------------------------------------- настройка из кода
    public void Configure(FlickerPreset p, Light light, Transform[] flameTransforms)
    {
        ResetState();
        preset = p;
        targetLight = light;
        flames = flameTransforms;
        customize = false;
    }

    // для CampfireFlicker: те же числа, что были в его полях
    public void Configure(FlickerPreset p, Light light, Transform[] flameTransforms, float amt, float spd, float scale)
    {
        Configure(p, light, flameTransforms);
        customize = true;
        amount = amt;
        speed = spd;
        scaleAmount = scale;
    }

    public float Value(float t)
    {
        return customize ? EvaluateCustom(t, seed, amount, speed) : Evaluate(preset, t, seed);
    }

    // ---------------------------------------------------------------- компонент
    void ResetState()
    {
        RestoreScale();
        init = false;
    }

    void Setup()
    {
        init = true;
        seed = (GetInstanceID() & 1023) * 0.173f;
        baseIntensity = targetLight != null ? targetLight.intensity : 1f;
        int n = flames != null ? flames.Length : 0;
        baseScale = new Vector3[n];
        for (int i = 0; i < n; i++) if (flames[i] != null) baseScale[i] = flames[i].localScale;
    }

    void Update()
    {
        if (!init) Setup();
        float v = Value(Time.time);

        // свет, у которого есть FireSource, выключен и ведётся пулом: его не трогаем
        if (targetLight != null && targetLight.enabled)
            targetLight.intensity = baseIntensity * v;

        if (flames == null) return;
        float d = v - 1f;
        float k = customize ? scaleAmount : 0.08f;
        float xz = 1f + d * k * 2f;
        float y = 1f + d * k * 6f;
        for (int i = 0; i < flames.Length && i < baseScale.Length; i++)
        {
            if (flames[i] == null) continue;
            Vector3 b = baseScale[i];
            flames[i].localScale = new Vector3(b.x * xz, b.y * y, b.z * xz);
        }
    }

    void RestoreScale()
    {
        if (!init || flames == null) return;
        for (int i = 0; i < flames.Length && i < baseScale.Length; i++)
            if (flames[i] != null) flames[i].localScale = baseScale[i];
    }

    void OnDisable() { RestoreScale(); init = false; }
}
