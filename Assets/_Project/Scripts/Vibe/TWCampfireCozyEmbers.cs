using UnityEngine;

/// <summary>
/// Very small optional animation of only the supplemental ember mesh.
/// Mounted by the editor baker under Campfire/Stage3/TW_CozyCampfire_ArtV1.
/// It never moves the root, collisions, camera, stage transforms or the existing fire.
/// </summary>
[DisallowMultipleComponent]
public sealed class TWCampfireCozyEmbers : MonoBehaviour
{
    [SerializeField] private Transform emberVisual;
    [SerializeField, Range(0f, 0.10f)] private float pulseAmount = 0.035f;
    [SerializeField, Range(0.1f, 8f)] private float pulseSpeed = 2.2f;
    [Header("Visual Polish 3.3 — угли")]
    [SerializeField, Range(0f, 0.06f)] private float slowBreathAmount = 0.013f;
    [SerializeField, Range(0.05f, 2f)] private float slowBreathSpeed = 0.28f;
    private Vector3 restScale = Vector3.one;
    private bool ready;
    private ParticleSystem sparks, smoke, hearts;
    private ParticleSystem.MinMaxCurve originalSparks, originalSmoke, originalHearts;
    private bool ratesCaptured, ratesApplied;
    private float nextRatesAt;

    public void Assign(Transform mesh)
    {
        emberVisual = mesh;
        if (mesh != null) { restScale = mesh.localScale; ready = true; }
    }

    private void Awake()
    {
        if (emberVisual == null) emberVisual = transform.Find("Warm_Low_Embers");
        if (emberVisual == null) return;
        restScale = emberVisual.localScale;
        ready = true;
        CaptureParticles();
    }

    private void OnEnable()
    {
        // The stage toggles through HutStages. Never create new particle systems.
        CaptureParticles();
        ApplyQuietRates();
    }

    private void CaptureParticles()
    {
        if (ratesCaptured) return;
        Transform stage = transform.parent;
        if (stage == null) return;
        sparks = GetPS(stage, "Sparks");
        smoke = GetPS(stage, "Smoke");
        hearts = GetPS(stage, "Hearts");
        if (sparks != null) originalSparks = sparks.emission.rateOverTime;
        if (smoke != null) originalSmoke = smoke.emission.rateOverTime;
        if (hearts != null) originalHearts = hearts.emission.rateOverTime;
        // Если старый костёр собран без этих систем, попробуем позднее.
        ratesCaptured = sparks != null || smoke != null || hearts != null;
    }

    private static ParticleSystem GetPS(Transform stage, string key)
    {
        Transform node = stage.Find(key);
        return node != null ? node.GetComponent<ParticleSystem>() : null;
    }

    private static void SetRate(ParticleSystem ps, ParticleSystem.MinMaxCurve source, float factor)
    {
        if (ps == null) return;
        var emission = ps.emission;
        // Multiplying the curve preserves both constants and curves, not just .constant.
        var adjusted = source;
        adjusted.constantMin *= factor;
        adjusted.constantMax *= factor;
        adjusted.curveMultiplier *= factor;
        emission.rateOverTime = adjusted;
    }

    private void ApplyQuietRates()
    {
        if (!Application.isPlaying || !ratesCaptured) return;
        float night = Mathf.Clamp01(DayNightCycle.Night01);
        float reduced = VibeSettings.ReduceEffects ? 0.55f : 1f;
        // Днём спокойный дым, ночью чуть заметнее искры; прежние потоки не ускоряем резко.
        SetRate(sparks, originalSparks, Mathf.Lerp(0.52f, 0.68f, night) * reduced);
        SetRate(smoke, originalSmoke, Mathf.Lerp(0.52f, 0.45f, night) * reduced);
        SetRate(hearts, originalHearts, 0.30f * reduced);
        ratesApplied = true;
    }

    private void RestoreRates()
    {
        if (!ratesApplied) return;
        if (sparks != null) { var e = sparks.emission; e.rateOverTime = originalSparks; }
        if (smoke != null) { var e = smoke.emission; e.rateOverTime = originalSmoke; }
        if (hearts != null) { var e = hearts.emission; e.rateOverTime = originalHearts; }
        ratesApplied = false;
    }

    private void LateUpdate()
    {
        if (!ready || emberVisual == null) return;
        float t = Time.time;
        float small = Mathf.Sin(t * pulseSpeed * 2f * Mathf.PI);
        float slow = Mathf.Sin(t * slowBreathSpeed * 2f * Mathf.PI + 1.2f);
        float visualScale = VibeSettings.ReduceEffects ? 0.55f : 1f;
        float pulse = (pulseAmount * small + slowBreathAmount * slow) * visualScale;
        float vertical = (0.35f * pulseAmount * small + slowBreathAmount * slow) * visualScale;
        emberVisual.localScale = Vector3.Scale(restScale, new Vector3(1f + pulse, 1f + vertical, 1f + pulse));

        // Дорогие ParticleSystem-модули трогаем не каждый кадр; настройки можно менять на ходу.
        if (t >= nextRatesAt)
        {
            nextRatesAt = t + 0.85f;
            if (!ratesCaptured) CaptureParticles();
            ApplyQuietRates();
        }
    }

    private void OnDisable()
    {
        if (ready && emberVisual != null) emberVisual.localScale = restScale;
        RestoreRates();
    }
}
