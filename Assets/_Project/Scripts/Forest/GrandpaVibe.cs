// Assets/_Project/Scripts/Forest/GrandpaVibe.cs (НЕ в Editor)
// Живость «Дедушки» v4 «Cloud Candy» (древний хранитель, без рун):
//   • кристаллы у дупла и между корнями «дышат»: яркость плавно ходит вверх-вниз, ночью ярче (MaterialPropertyBlock, новых материалов нет);
//   • парящие кристаллы (Motes) медленно кружат вокруг ствола и покачиваются вверх-вниз;
//   • духи-огоньки: светящиеся шарики поднимаются от дупла и вьются вокруг ствола (ночью и в сумерки больше);
//   • золотая пыль в «солнечных» лучах под кроной (днём), пастельные листья и лепестки (розовые и лавандовые);
//   • светлячки ночью, качели, которые качаются.
// Всё рисуется кодом: спрайты VibeGfx.Leaf / VibeGfx.Soft, материал VibeGfx.ParticleMat (Sprites/Default), префабы и текстуры не нужны.
// Вешает GrandpaTree.cs (Editor) на корень Landmark_Grandpa. Если спрайтов или материала нет, эффект тихо пропускается.
// Burst() вызывает LandmarkTree при приветствии: сыплется горсть лепестков, вспыхивают кристаллы и вылетают духи.
using UnityEngine;

public class GrandpaVibe : MonoBehaviour
{
    [Header("Качели")]
    public Transform swing;
    public float swingAmp = 5f;
    public float swingSpeed = 0.85f;

    [Header("Лепестки и листья")]
    public bool petals = true;
    public float crownHeight = 17f;
    public float crownRadius = 9f;
    public float petalRate = 2.2f;
    public float leafRate = 0.9f;

    [Header("Светлячки")]
    public bool fireflies = true;
    public float flyRadius = 8f;
    public float flyMaxRate = 7f;

    [Header("Кристаллы и духи")]
    [Tooltip("Рендерер с мешем «Mystic», где светящиеся кристаллы лежат в отдельном сабмеше")]
    public Renderer glow;
    [Tooltip("Номер сабмеша (материала) с кристаллами в этом рендерере")]
    public int glowMaterialIndex = 1;
    [Tooltip("Парящие кристаллы: корень, который крутится вокруг ствола")]
    public Transform motes;
    [Tooltip("Рендерер парящих кристаллов (материал 0), чтобы они дышали светом вместе с остальными")]
    public Renderer motesGlow;
    [Tooltip("Скорость кружения парящих кристаллов, градусов в секунду")]
    public float motesSpin = 5f;
    public float motesBobAmp = 0.35f;
    public bool spirits = true;
    [Tooltip("Где у дупла рождаются духи (локально, корень префаба)")]
    public Vector3 hollowLocal = new Vector3(0.1f, 1.8f, 2.3f);
    public float spiritMaxRate = 3.2f;
    public bool dust = true;

    ParticleSystem petalPs, leafPs, flyPs, spiritPs, dustPs;
    float phase, nextCheck, nextRune, flash;
    MaterialPropertyBlock mpb;
    Color glowTop, glowBottom, moteTop, moteBottom;
    bool glowReady, moteReady;
    Vector3 motesBase;
    static readonly int BaseId = Shader.PropertyToID("_BaseColor");
    static readonly int BottomId = Shader.PropertyToID("_BottomColor");

    void Start()
    {
        phase = Random.value * 6.28f;
        if (petals) { petalPs = MakePetals(); leafPs = MakeLeaves(); }
        if (fireflies) flyPs = MakeFireflies();
        if (spirits) spiritPs = MakeSpirits();
        if (dust) dustPs = MakeDust();
        PrepareGlow();
        if (motes != null) motesBase = motes.localPosition;
    }

    void Update()
    {
        if (swing != null)
        {
            float t = Time.time * swingSpeed + phase;
            swing.localRotation = Quaternion.Euler(0f, 55f, 0f) * Quaternion.Euler(Mathf.Sin(t) * swingAmp, 0f, Mathf.Sin(t * 0.63f) * swingAmp * 0.25f);
        }

        float night = DayNightCycle.Night01;
        float dusk = DayNightCycle.Twilight01;
        float dark = Mathf.Clamp01(night + dusk * 0.6f);

        if (Time.time >= nextCheck)
        {
            nextCheck = Time.time + 0.5f;
            if (flyPs != null) { var em = flyPs.emission; em.rateOverTime = flyMaxRate * dark; }
            if (spiritPs != null) { var em = spiritPs.emission; em.rateOverTime = spiritMaxRate * Mathf.Lerp(0.35f, 1f, dark); }
            if (dustPs != null) { var em = dustPs.emission; em.rateOverTime = 5f * (1f - dark); }   // пыль в лучах только днём
        }

        if (motes != null)
        {
            motes.localRotation = Quaternion.Euler(0f, Time.time * motesSpin + phase * 20f, 0f);
            motes.localPosition = motesBase + Vector3.up * (Mathf.Sin(Time.time * 0.7f + phase) * motesBobAmp);
        }

        if ((glowReady || moteReady) && Time.time >= nextRune)
        {
            nextRune = Time.time + 0.05f;
            flash = Mathf.MoveTowards(flash, 0f, 0.05f * 0.9f);
            float breathe = 0.5f + 0.5f * Mathf.Sin(Time.time * 1.25f + phase);
            float k = Mathf.Lerp(0.78f, 1.15f, breathe) * (1f + 0.55f * dark) + flash * 1.4f;
            if (mpb == null) mpb = new MaterialPropertyBlock();
            if (glowReady) Pulse(glow, glowMaterialIndex, glowTop, glowBottom, k);
            if (moteReady) Pulse(motesGlow, 0, moteTop, moteBottom, k * (0.9f + 0.2f * Mathf.Sin(Time.time * 0.9f + phase * 2f)));
        }
    }

    void Pulse(Renderer r, int idx, Color top, Color bottom, float k)
    {
        r.GetPropertyBlock(mpb, idx);
        mpb.SetColor(BaseId, new Color(top.r * k, top.g * k, top.b * k, 1f));
        mpb.SetColor(BottomId, new Color(bottom.r * k, bottom.g * k, bottom.b * k, 1f));
        r.SetPropertyBlock(mpb, idx);
    }

    public void Burst()
    {
        if (petalPs != null) petalPs.Emit(28);
        if (leafPs != null) leafPs.Emit(6);
        if (spiritPs != null) spiritPs.Emit(10);
        flash = 1f;
    }

    // ------------------------------------------------------------ свечение кристаллов
    static bool ReadColors(Renderer r, int idx, out Color top, out Color bottom)
    {
        top = bottom = Color.white;
        if (r == null) return false;
        var mats = r.sharedMaterials;
        if (idx < 0 || idx >= mats.Length || mats[idx] == null) return false;
        var m = mats[idx];
        if (!m.HasProperty(BaseId) || !m.HasProperty(BottomId)) return false;
        top = m.GetColor(BaseId);
        bottom = m.GetColor(BottomId);
        return true;
    }

    void PrepareGlow()
    {
        glowReady = ReadColors(glow, glowMaterialIndex, out glowTop, out glowBottom);
        moteReady = ReadColors(motesGlow, 0, out moteTop, out moteBottom);
    }

    // ------------------------------------------------------------ частицы
    ParticleSystem MakePetals()
    {
        Material mat = VibeGfx.ParticleMat(VibeGfx.Leaf);
        if (mat == null) return null;
        var go = new GameObject("Petals");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, crownHeight, 0f);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(7f, 11f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.20f, 0.36f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.78f, 0.90f, 0.95f), new Color(0.82f, 0.76f, 1f, 0.95f));   // розовые и лавандовые лепестки под цвет кроны
        main.gravityModifier = 0.045f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 60;

        var em = ps.emission;
        em.rateOverTime = petalRate;

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Circle;
        sh.radius = crownRadius;
        sh.rotation = new Vector3(90f, 0f, 0f);

        SoftWind(ps, 0.9f, 0.22f);
        Finish(go, ps, mat);
        return ps;
    }

    // редкие мятные листики: живая пастельная крона
    ParticleSystem MakeLeaves()
    {
        Material mat = VibeGfx.ParticleMat(VibeGfx.Leaf);
        if (mat == null) return null;
        var go = new GameObject("Leaves");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, crownHeight - 2f, 0f);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 12f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.38f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.70f, 0.96f, 0.86f, 0.95f), new Color(1f, 0.97f, 0.98f, 0.95f));   // мятные и сливочные листики
        main.gravityModifier = 0.06f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 30;

        var em = ps.emission;
        em.rateOverTime = leafRate;

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Circle;
        sh.radius = crownRadius * 0.9f;
        sh.rotation = new Vector3(90f, 0f, 0f);

        SoftWind(ps, 1.1f, 0.25f);
        var rol = ps.rotationOverLifetime;
        rol.enabled = true;
        rol.z = new ParticleSystem.MinMaxCurve(-2.2f, 2.2f);
        Finish(go, ps, mat);
        return ps;
    }

    ParticleSystem MakeFireflies()
    {
        Material mat = VibeGfx.ParticleMat(VibeGfx.Soft);
        if (mat == null) return null;
        var go = new GameObject("Fireflies");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 2.2f, 0f);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.30f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.95f, 1f, 0.55f, 1f), new Color(1f, 0.85f, 0.45f, 1f));
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 50;

        var em = ps.emission;
        em.rateOverTime = 0f;

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius = flyRadius;

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.8f;
        noise.frequency = 0.35f;
        noise.scrollSpeed = 0.4f;

        FadeInOut(ps);
        Finish(go, ps, mat, false);
        return ps;
    }

    // духи: бирюзовые огоньки, плывут вверх по спирали вокруг ствола
    ParticleSystem MakeSpirits()
    {
        Material mat = VibeGfx.ParticleMat(VibeGfx.Soft);
        if (mat == null) return null;
        var go = new GameObject("Spirits");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = hollowLocal;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.20f, 0.42f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 1f, 0.90f, 1f), new Color(0.70f, 0.95f, 1f, 1f));
        main.gravityModifier = -0.012f;   // чуть-чуть вверх
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 36;

        var em = ps.emission;
        em.rateOverTime = 1f;

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle = 28f;
        sh.radius = 0.5f;
        sh.rotation = new Vector3(-60f, 0f, 0f);   // из дупла вперёд и вверх

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 1.1f;
        noise.frequency = 0.28f;
        noise.scrollSpeed = 0.35f;

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        var curve = new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(0.25f, 1f), new Keyframe(0.7f, 0.9f), new Keyframe(1f, 0f));
        sol.size = new ParticleSystem.MinMaxCurve(1f, curve);

        FadeInOut(ps);
        Finish(go, ps, mat, false);
        return ps;
    }

    // золотая пыль в лучах: мелкие, медленные, видны у самой земли и под кроной
    ParticleSystem MakeDust()
    {
        Material mat = VibeGfx.ParticleMat(VibeGfx.Soft);
        if (mat == null) return null;
        var go = new GameObject("Dust");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 5f, 0f);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.10f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.15f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.92f, 0.62f, 0.75f), new Color(1f, 0.98f, 0.82f, 0.75f));
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 60;

        var em = ps.emission;
        em.rateOverTime = 5f;

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Box;
        sh.scale = new Vector3(crownRadius * 1.6f, 5f, crownRadius * 1.6f);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.35f;
        noise.frequency = 0.5f;
        noise.scrollSpeed = 0.2f;

        FadeInOut(ps);
        Finish(go, ps, mat, false);
        return ps;
    }

    // ------------------------------------------------------------ общие помощники
    static void SoftWind(ParticleSystem ps, float strength, float freq)
    {
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = strength;
        noise.frequency = freq;
        noise.scrollSpeed = 0.25f;

        var rol = ps.rotationOverLifetime;
        rol.enabled = true;
        rol.z = new ParticleSystem.MinMaxCurve(-1.6f, 1.6f);

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(0.15f, 0.5f);   // лёгкий ветер
        vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.1f, 0.15f);

        FadeInOut(ps);
    }

    static void Finish(GameObject go, ParticleSystem ps, Material mat) { Finish(go, ps, mat, true); }

    static void Finish(GameObject go, ParticleSystem ps, Material mat, bool billboard)
    {
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        ps.Play();
    }

    static void FadeInOut(ParticleSystem ps)
    {
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
    }
}
