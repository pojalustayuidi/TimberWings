// Assets/_Project/Scripts/Forest/ForestAtmosphere.cs (НЕ в Editor)
// Атмосфера леса: пятна солнца на земле, лучи света и светлячки. Создаётся инструментом Tools → Forest → Декор леса,
// вешается на объект ForestDecor/Fx. Сам ничего не рисует: только плавно меняет яркость (_Intensity у материала ForestDecor/Glow)
// в зависимости от времени суток: днём видны пятна и лучи, к вечеру они тают, а светлячки, наоборот, разгораются.
// «Ночь» считается тем же способом, что в FireLightPool: по высоте солнца (RenderSettings.sun).
using UnityEngine;

[DisallowMultipleComponent]
public class ForestAtmosphere : MonoBehaviour
{
    [System.Serializable]
    public class Group
    {
        public string name;
        public Renderer[] renderers = new Renderer[0];
        [Tooltip("Яркость днём (множитель _Intensity)")] [Range(0f, 2f)] public float day = 1f;
        [Tooltip("Яркость ночью")] [Range(0f, 2f)] public float night = 1f;
    }

    public Group[] groups = new Group[0];
    [Tooltip("Если солнца в сцене нет: какой считать ночь")] public float noSunNight = 0.35f;
    [Tooltip("Солнце выше этого sin(высоты) = полный день")] public float fullDaySunElevation = 0.45f;
    [Tooltip("Скорость смены дня и ночи (1 = примерно 4 секунды на полный переход)")] public float response = 1f;

    [Header("Visual Polish 3.3 — светлячки")]
    [Tooltip("Ненавязчивое мерцание уже существующих светлячков. Новые объекты не создаются.")]
    public bool softFireflyTwinkle = true;
    [Range(0f, 0.35f)] public float twinkleStrength = 0.13f;
    [Range(0.1f, 2f)] public float twinkleSpeed = 0.60f;

    [Header("Visual Polish 3.3.2 — только кедровник ночью")]
    [Tooltip("Светлячки ForestDecor видны только внутри подзоны ForestBiome.Cedar")]
    public bool restrictFirefliesToCedar = true;
    [Tooltip("Внешние огоньки становятся видимыми только ближе к ночи")]
    [Range(0.3f, 0.95f)] public float firefliesNightStart = 0.55f;

    [Header("Тест")]
    public bool forceNight;
    [Range(0f, 1f)] public float forceNightLevel = 1f;

    static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    MaterialPropertyBlock mpb;
    float night = -1f;
    float nextCheck;
    bool firefliesPrepared;

    void OnEnable()
    {
        night = -1f;
        firefliesPrepared = false;
        Tick(true);
    }

    void Update()
    {
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + 0.2f;
        Tick(false);
    }

    float TargetNight()
    {
        if (forceNight) return forceNightLevel;
        Light sun = RenderSettings.sun;
        if (sun == null) return noSunNight;
        float elev = -sun.transform.forward.y;
        float day = Mathf.Clamp01(Mathf.InverseLerp(0.02f, Mathf.Max(0.1f, fullDaySunElevation), elev)) * Mathf.Clamp01(sun.intensity / 0.6f);
        return 1f - day;
    }

    void Tick(bool snap)
    {
        float t = TargetNight();
        float prev = night;
        night = (snap || night < 0f) ? t : Mathf.MoveTowards(night, t, 0.2f * 0.25f * Mathf.Max(0.05f, response));
        if (!snap && Mathf.Abs(night - prev) < 0.002f && !HasTwinkleGroups()) return;
        Apply();
    }

    static bool IsFireflyGroup(Group group)
    {
        if (group == null || string.IsNullOrEmpty(group.name)) return false;
        return group.name.IndexOf("светля", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               group.name.IndexOf("firefl", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    bool HasTwinkleGroups()
    {
        if (!softFireflyTwinkle || twinkleStrength <= 0f || groups == null) return false;
        for (int i = 0; i < groups.Length; i++)
            if (IsFireflyGroup(groups[i])) return true;
        return false;
    }

    void PrepareFireflies()
    {
        if (firefliesPrepared || groups == null) return;
        firefliesPrepared = true;
        for (int g = 0; g < groups.Length; g++)
        {
            var gr = groups[g];
            if (!IsFireflyGroup(gr) || gr.renderers == null) continue;
            foreach (var renderer in gr.renderers)
            {
                if (renderer == null) continue;
                var ps = renderer.GetComponent<ParticleSystem>();
                if (ps == null) continue;
                var main = ps.main;
                main.prewarm = false;
                main.maxParticles = Mathf.Min(main.maxParticles, 6);
                main.startSize = new ParticleSystem.MinMaxCurve(0.018f, 0.045f);
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(1f, 0.96f, 0.7f, 0.32f), new Color(0.85f, 1f, 0.74f, 0.20f));
                var emission = ps.emission;
                emission.rateOverTime = 0f;
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }

    void Apply()
    {
        if (groups == null) return;
        PrepareFireflies();
        if (mpb == null) mpb = new MaterialPropertyBlock();
        float nightFactor = Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(firefliesNightStart, Mathf.Min(1f, firefliesNightStart + 0.32f), night));
        for (int g = 0; g < groups.Length; g++)
        {
            var gr = groups[g];
            if (gr == null || gr.renderers == null) continue;
            bool isFirefly = IsFireflyGroup(gr);
            float baseIntensity = Mathf.Lerp(gr.day, gr.night, night);
            float twinkle = 1f;
            if (isFirefly && softFireflyTwinkle)
            {
                float t = Time.time * twinkleSpeed;
                float wave = Mathf.Sin(t * 2.3f + g * 1.77f) * 0.65f +
                             Mathf.Sin(t * 3.9f + g * 3.17f) * 0.35f;
                twinkle = Mathf.Max(0f, 1f + twinkleStrength * wave * (VibeSettings.ReduceEffects ? 0.35f : 1f));
            }
            for (int i = 0; i < gr.renderers.Length; i++)
            {
                var r = gr.renderers[i];
                if (r == null) continue;
                float k = baseIntensity;
                if (isFirefly)
                {
                    ForestZone zone = ForestZone.At(r.transform.position);
                    bool inCedar = !restrictFirefliesToCedar || (zone != null && zone.biome == ForestBiome.Cedar);
                    bool visible = inCedar && nightFactor > 0.01f;
                    k = visible ? baseIntensity * 0.26f * nightFactor * twinkle : 0f;
                    var ps = r.GetComponent<ParticleSystem>();
                    if (ps != null)
                    {
                        var emission = ps.emission;
                        emission.rateOverTime = visible ? (VibeSettings.ReduceEffects ? 0.12f : 0.45f) * nightFactor : 0f;
                        if (visible)
                        {
                            if (!ps.isPlaying) ps.Play();
                        }
                        else if (ps.isPlaying || ps.particleCount > 0)
                        {
                            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                        }
                    }
                }
                r.GetPropertyBlock(mpb);
                mpb.SetFloat(IntensityId, k);
                r.SetPropertyBlock(mpb);
            }
        }
    }

    [ContextMenu("Тест: ночь")] void TestNight() { forceNight = true; forceNightLevel = 1f; }
    [ContextMenu("Тест: день")] void TestDay() { forceNight = true; forceNightLevel = 0f; }
    [ContextMenu("Тест: по солнцу")] void TestAuto() { forceNight = false; }
}
