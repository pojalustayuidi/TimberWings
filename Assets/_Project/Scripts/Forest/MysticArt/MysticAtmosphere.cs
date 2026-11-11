using System;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class MysticAtmosphere : MonoBehaviour
{
    [Serializable]
    public struct NightAccent
    {
        public Light light;
        public float peakIntensity;
        public Renderer glow;
        public Color glowColor;
        public Renderer groundGlow;
    }

    public float radius = 18f, feather = 5f;
    public Volume dayVolume, nightVolume;
    // Older saved clearings still serialize these two arrays.
    public Light[] lights = Array.Empty<Light>();
    public TextMesh[] nightWords = Array.Empty<TextMesh>();
    public ParticleSystem[] particles = Array.Empty<ParticleSystem>();
    public Renderer[] runes = Array.Empty<Renderer>(), apparitions = Array.Empty<Renderer>();
    public Transform swing, motes;
    public AudioSource ambience;
    [Range(-1f, 1f)] public float previewNight = -1f;

    [Header("Фонари, резьба и светлячки")]
    public NightAccent[] nightAccents = Array.Empty<NightAccent>();
    public Renderer[] engravingSurfaces = Array.Empty<Renderer>();
    public ParticleSystem[] fireflies = Array.Empty<ParticleSystem>();

    [Header("Звуки поляны")]
    [Range(0f, 1f)] public float nightAmbienceLevel = 0.043f;
    public AudioSource[] nightEchoes = Array.Empty<AudioSource>();
    public AudioSource swingCreak;

    [Header("Ночная инкрустация")]
    public Transform signatureAnchor;
    public Renderer[] signature = Array.Empty<Renderer>();
    public ParticleSystem signatureFireflies;
    public Renderer[] crown = Array.Empty<Renderer>();
    [Min(0.5f)] public float signatureNear = 2.5f;
    [Min(1f)] public float signatureFar = 5f;
    public Color nightFoliageTint = new Color(0.72f, 0.86f, 1f);

    const float SwingBreezeDuration = 8.8f;
    float weight, clock, nextCheck, signatureAlpha;
    float nextEchoAt;
    float nextBreezeAt, swingAngle;
    int echoCursor;
    bool swarmsActive, restPoseCaptured, swingAllowed, creakPlayed;
    Quaternion swingRest, motesRest;
    MaterialPropertyBlock block;

    static readonly int Alpha = Shader.PropertyToID("_Alpha");
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    public static float Boundary(float distance, float radius, float feather)
    {
        float x = Mathf.Clamp01((radius - distance) / Mathf.Max(0.1f, feather));
        return x * x * (3f - 2f * x);
    }

    // The secret signature and apparitions use the actual night phase.
    public static float SecretVisibility(bool isNight, float night, float distance, float near, float far)
    {
        if (!isNight) return 0f;
        float darkness = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.8f, night));
        float proximity = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(near, Mathf.Max(near + 0.1f, far), distance));
        return darkness * proximity;
    }

    void OnEnable()
    {
        block = new MaterialPropertyBlock();
        if (swing != null) swingRest = swing.localRotation;
        if (motes != null) motesRest = motes.localRotation;
        restPoseCaptured = true;
        weight = 0f;
        clock = 0f;
        nextCheck = 0f;
        signatureAlpha = 0f;
        swingAngle = 0f;
        swingAllowed = false;
        nextBreezeAt = -1f;
        creakPlayed = false;
        nextEchoAt = -1f;
        echoCursor = 0;
        swarmsActive = false;
        if (swingCreak == null)
        {
            foreach (var source in nightEchoes)
            {
                if (source == null || source.name != "OldForest_SwingCreak") continue;
                swingCreak = source;
                break;
            }
        }
        ResetView();
        if (ambience != null && ambience.clip != null && !ambience.isPlaying)
            ambience.Play();
    }

    void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.1f);
        clock += dt;
        var player = PlayerMovement.Instance;
        Vector3 position = player != null ? player.transform.position : transform.position + Vector3.right * 999f;
        Vector3 delta = position - transform.position;
        delta.y = 0f;
        bool nearZone = player != null && delta.sqrMagnitude < 45f * 45f;
        weight = Mathf.MoveTowards(weight, player != null ? Boundary(delta.magnitude, radius, feather) : 0f, dt / 1.15f);

        float night = previewNight >= 0f ? Mathf.Clamp01(previewNight) : DayNightCycle.Night01;
        bool isNight = previewNight >= 0f ? previewNight >= 0.65f : DayNightCycle.Phase == DayPhase.Night;
        float dark = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.12f, 0.82f, night));
        bool reduce = VibeSettings.ReduceEffects;
        float effects = reduce ? 0.55f : 1f;
        swingAllowed = nearZone && isNight && dark > 0.08f && !reduce && swing != null &&
            (position - swing.position).sqrMagnitude < 19f * 19f;
        SetVolumes(night, weight * effects);
        SetLighting(position, nearZone, dark, reduce, false);
        SetEngravingGlow(nearZone ? dark : 0f);
        SetCrownTint(night);
        UpdateAudio(position, night, isNight ? dark : 0f);

        foreach (var rune in runes) Opacity(rune, Mathf.Lerp(0.12f, 0.72f, night), false);
        float distance = signatureAnchor != null ? Vector3.Distance(position, signatureAnchor.position) : 999f;
        float target = SecretVisibility(isNight, night, distance, signatureNear, signatureFar);
        signatureAlpha = isNight ? Mathf.MoveTowards(signatureAlpha, target, dt * 1.25f) : 0f;
        foreach (var renderer in signature) Opacity(renderer, signatureAlpha, true);
        foreach (var renderer in apparitions)
        {
            if (renderer == null) continue;
            float d = Vector3.Distance(position, renderer.transform.position);
            float seen = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(3.5f, 7f, d));
            float alpha = isNight && !reduce ? weight * night * seen * 0.12f : 0f;
            Opacity(renderer, alpha, true);
        }

        if (motes != null)
            motes.localRotation = motesRest * Quaternion.Euler(0f, reduce ? 0f : clock * 2.3f, 0f);
        UpdateParticles(position, nearZone, isNight, night, dark, effects, distance);
    }

    void FixedUpdate()
    {
        if (swing == null) return;
        float previous = swingAngle;
        if (!swingAllowed)
        {
            nextBreezeAt = -1f;
            creakPlayed = false;
            swingAngle = Mathf.MoveTowards(swingAngle, 0f, 5.5f * Time.fixedDeltaTime);
        }
        else
        {
            float now = Time.fixedTime;
            if (nextBreezeAt < 0f)
                nextBreezeAt = now + UnityEngine.Random.Range(2.3f, 4.8f);

            float elapsed = now - nextBreezeAt;
            if (elapsed >= SwingBreezeDuration)
            {
                nextBreezeAt = now + UnityEngine.Random.Range(12f, 23f);
                creakPlayed = false;
            }
            if (elapsed <= 0f)
                swingAngle = Mathf.MoveTowards(swingAngle, 0f, 5.5f * Time.fixedDeltaTime);
            else
                swingAngle = BreezeAngle(elapsed);

            if (!creakPlayed && elapsed >= 0.6f && elapsed < SwingBreezeDuration)
            {
                creakPlayed = true;
                if (swingCreak != null && swingCreak.isActiveAndEnabled && swingCreak.volume > 0.006f)
                    swingCreak.Play();
            }
        }

        // The seat box is under this pivot and moves in the physics step.
        if (previous != swingAngle)
            swing.localRotation = swingRest * Quaternion.Euler(swingAngle, 0f, 0f);
    }

    static float BreezeAngle(float elapsed)
    {
        if (elapsed <= 0f || elapsed >= SwingBreezeDuration) return 0f;
        float rise = Mathf.SmoothStep(0f, 1f, elapsed / 1.25f);
        float settle = 1f - Mathf.SmoothStep(0f, 1f, (elapsed - 1.25f) / (SwingBreezeDuration - 1.25f));
        return 3.65f * rise * settle * Mathf.Sin(elapsed * (2f * Mathf.PI / 4.25f));
    }

    void SetVolumes(float night, float volumeWeight)
    {
        if (dayVolume != null) dayVolume.weight = volumeWeight * (1f - night);
        if (nightVolume != null) nightVolume.weight = volumeWeight * night;
    }

    void SetLighting(Vector3 position, bool nearby, float darkness, bool steady, bool preview)
    {
        for (int i = 0; i < lights.Length; i++)
            SetLight(lights[i], nearby ? darkness * (i == 0 ? 0.75f : 0.38f) : 0f);

        for (int i = 0; i < nightAccents.Length; i++)
        {
            var accent = nightAccents[i];
            if (accent.light == null) continue;
            float presence = preview ? 1f : Boundary(Vector3.Distance(position, accent.light.transform.position), 25f, 6f);
            float pulse = steady ? 1f : 0.94f + 0.06f * Mathf.Sin(clock * 0.87f + i * 1.9f);
            float strength = nearby ? darkness * presence * pulse : 0f;
            SetLight(accent.light, strength * accent.peakIntensity);
            SetColor(accent.glow, EmissionColor, accent.glowColor * strength);
            Opacity(accent.groundGlow, strength, true);
        }
    }

    static void SetLight(Light light, float intensity)
    {
        if (light == null) return;
        light.enabled = intensity > 0.004f;
        light.intensity = intensity;
    }

    void SetEngravingGlow(float darkness)
    {
        foreach (var surface in engravingSurfaces)
            SetColor(surface, EmissionColor, Color.white * (0.23f * darkness));
    }

    void SetCrownTint(float night)
    {
        Color color = Color.Lerp(Color.white, nightFoliageTint, night);
        foreach (var renderer in crown) SetColor(renderer, BaseColor, color);
    }

    void SetColor(Renderer renderer, int property, Color color)
    {
        if (renderer == null) return;
        renderer.GetPropertyBlock(block);
        block.SetColor(property, color);
        renderer.SetPropertyBlock(block);
    }

    void Opacity(Renderer renderer, float opacity, bool hide)
    {
        if (renderer == null) return;
        renderer.GetPropertyBlock(block);
        block.SetFloat(Alpha, opacity);
        renderer.SetPropertyBlock(block);
        if (hide) renderer.enabled = opacity > 0.001f;
    }

    void HideLegacyFloorText()
    {
        foreach (var text in nightWords)
            if (text != null && text.TryGetComponent<MeshRenderer>(out var renderer))
                renderer.enabled = false;
    }

    void UpdateParticles(Vector3 position, bool nearZone, bool isNight, float night, float dark, float effects, float distance)
    {
        bool active = nearZone && isNight && dark > 0.03f;
        if (active == swarmsActive && Time.unscaledTime < nextCheck) return;
        swarmsActive = active;
        nextCheck = Time.unscaledTime + 0.35f;

        for (int i = 0; i < particles.Length; i++)
        {
            float rate = i == 0 ? 0.66f * dark : i == 1 ? 1.3f * night : 0.35f;
            Rate(particles[i], nearZone ? rate * effects : 0f);
        }
        foreach (var swarm in fireflies)
        {
            if (swarm == null) continue;
            bool close = (position - swarm.transform.position).sqrMagnitude < 21f * 21f;
            SetSwarm(swarm, active && close ? 1.8f * dark * effects : 0f);
        }
        SetSwarm(signatureFireflies, active && distance < 9f ? 0.62f * dark * effects : 0f);
    }

    static void Rate(ParticleSystem ps, float value)
    {
        if (ps == null) return;
        var emission = ps.emission;
        emission.rateOverTime = value;
    }

    static void SetSwarm(ParticleSystem ps, float rate)
    {
        if (ps == null) return;
        Rate(ps, rate);
        if (rate > 0f && !ps.isPlaying)
            ps.Play(false);
        else if (rate <= 0f && !ps.isStopped)
            ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    void UpdateAudio(Vector3 position, float night, float darkness)
    {
        float level = GameAudio.AmbientLevel * weight;
        if (ambience != null)
            ambience.volume = level * Mathf.Lerp(0.026f, nightAmbienceLevel, night);

        float echoLevel = level * darkness * 0.31f;
        UpdateCueVolume(swingCreak, position, swingAllowed ? echoLevel : 0f);
        foreach (var source in nightEchoes)
            if (source != swingCreak)
                UpdateCueVolume(source, position, echoLevel);

        if (echoLevel <= 0.001f || nightEchoes.Length == 0)
        {
            nextEchoAt = -1f;
            return;
        }
        if (nextEchoAt < 0f)
            nextEchoAt = clock + UnityEngine.Random.Range(3.5f, 6.5f);
        if (clock < nextEchoAt) return;

        // One source per cue. Try the other anchor if the player is too far away.
        for (int i = 0; i < nightEchoes.Length; i++)
        {
            var source = nightEchoes[echoCursor];
            echoCursor = (echoCursor + 1) % nightEchoes.Length;
            if (source == null || source == swingCreak || !source.isActiveAndEnabled || source.volume <= 0.006f) continue;
            source.Play();
            nextEchoAt = clock + UnityEngine.Random.Range(14f, 27f);
            return;
        }
        nextEchoAt = clock + 2f;
    }

    static void UpdateCueVolume(AudioSource source, Vector3 position, float level)
    {
        if (source == null) return;
        float distance = Vector3.Distance(position, source.transform.position);
        source.volume = level * Boundary(distance, 15f, 5f);
        if (source.volume <= 0.001f && source.isPlaying) source.Stop();
    }

    // The edit-mode menu previews the same lights, materials and exposure as the scene.
    public void ApplyPreview(float night)
    {
        previewNight = night;
        if (block == null) block = new MaterialPropertyBlock();
        float dark = Mathf.Clamp01(night);
        SetVolumes(dark, night >= 0f ? 1f : 0f);
        SetLighting(Vector3.zero, true, dark, true, true);
        SetEngravingGlow(dark);
        SetCrownTint(dark);
        foreach (var r in signature) Opacity(r, dark >= 0.65f ? dark : 0f, true);
        foreach (var r in apparitions) Opacity(r, dark >= 0.65f ? dark * 0.12f : 0f, true);
        foreach (var r in runes) Opacity(r, Mathf.Lerp(0.12f, 0.72f, dark), false);
        HideLegacyFloorText();
    }

    void ResetView()
    {
        SetVolumes(0f, 0f);
        SetLighting(Vector3.zero, false, 0f, true, false);
        SetEngravingGlow(0f);
        SetCrownTint(0f);
        if (ambience != null) ambience.volume = 0f;
        if (swingCreak != null) swingCreak.Stop();
        foreach (var source in nightEchoes) if (source != null) source.Stop();
        foreach (var ps in particles)
        {
            Rate(ps, 0f);
            if (ps != null) ps.Clear();
        }
        foreach (var swarm in fireflies) SetSwarm(swarm, 0f);
        SetSwarm(signatureFireflies, 0f);
        foreach (var r in signature) Opacity(r, 0f, true);
        foreach (var r in apparitions) Opacity(r, 0f, true);
        HideLegacyFloorText();
    }

    void OnDisable()
    {
        if (block == null) block = new MaterialPropertyBlock();
        ResetView();
        if (ambience != null) ambience.Stop();
        if (restPoseCaptured && swing != null) swing.localRotation = swingRest;
        if (restPoseCaptured && motes != null) motes.localRotation = motesRest;
        restPoseCaptured = false;
    }
}
