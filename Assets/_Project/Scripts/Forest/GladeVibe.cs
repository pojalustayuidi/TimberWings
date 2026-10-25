// Assets/_Project/Scripts/Forest/GladeVibe.cs (НЕ в Editor)  НОВЫЙ ФАЙЛ
// Живость поляны рубки у PAD_Forest (вешает ForestGladeBeauty.cs на корень ChopGladeDecor):
//   • золотая пыль, парящая в солнечных лучах (днём; ночью гаснет);
//   • мягкая тёплая дымка света: несколько больших полупрозрачных пятен, плывущих над поляной (днём и на закате), как лучи сквозь листву;
//   • светлячки (ночью и в сумерки);
//   • редкие зелёные листья, которые падают с деревьев вокруг.
// Всё рисуется кодом: спрайты VibeGfx.Soft / VibeGfx.Leaf и материал VibeGfx.ParticleMat, префабы и текстуры не нужны.
// Если спрайтов или материала нет, эффект тихо пропускается. Дёшево: 4 системы, до ~150 частиц суммарно.
using UnityEngine;

public class GladeVibe : MonoBehaviour
{
    [Tooltip("Радиус поляны, где живут эффекты, м")]
    public float radius = 9f;
    public bool dust = true;
    public bool haze = true;
    public bool fireflies = true;
    public bool leaves = true;
    public float dustRate = 6f;
    public float hazeRate = 0.35f;
    public float flyMaxRate = 7f;
    public float leafRate = 0.7f;

    ParticleSystem dustPs, hazePs, flyPs, leafPs;
    float nextCheck;

    void Start()
    {
        if (dust) dustPs = MakeDust();
        if (haze) hazePs = MakeHaze();
        if (fireflies) flyPs = MakeFireflies();
        if (leaves) leafPs = MakeLeaves();
    }

    void Update()
    {
        if (Time.time < nextCheck) return;
        nextCheck = Time.time + 0.5f;
        float night = DayNightCycle.Night01;
        float dusk = DayNightCycle.Twilight01;
        float dark = Mathf.Clamp01(night + dusk * 0.6f);
        if (dustPs != null) { var em = dustPs.emission; em.rateOverTime = dustRate * (1f - dark); }
        if (hazePs != null) { var em = hazePs.emission; em.rateOverTime = hazeRate * (1f - night); }   // на закате дымка тёплая и ещё видна
        if (flyPs != null) { var em = flyPs.emission; em.rateOverTime = flyMaxRate * dark; }
    }

    // ------------------------------------------------------------ системы
    ParticleSystem MakeDust()
    {
        Material mat = VibeGfx.ParticleMat(VibeGfx.Soft);
        if (mat == null) return null;
        var ps = NewSystem("Dust", new Vector3(0f, 2.6f, 0f));
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.10f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.15f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.92f, 0.62f, 0.75f), new Color(1f, 0.98f, 0.82f, 0.75f));
        main.maxParticles = 70;
        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Box;
        sh.scale = new Vector3(radius * 1.5f, 4.5f, radius * 1.5f);
        Noise(ps, 0.35f, 0.5f);
        Fade(ps);
        Finish(ps, mat);
        return ps;
    }

    // «лучи»: большие мягкие пятна, очень прозрачные, очень медленные
    ParticleSystem MakeHaze()
    {
        Material mat = VibeGfx.ParticleMat(VibeGfx.Soft);
        if (mat == null) return null;
        var ps = NewSystem("Haze", new Vector3(0f, 3.4f, 0f));
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(9f, 14f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.03f, 0.12f);
        main.startSize = new ParticleSystem.MinMaxCurve(5f, 8f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.93f, 0.66f, 0.07f), new Color(1f, 0.86f, 0.55f, 0.10f));
        main.maxParticles = 10;
        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Circle;
        sh.radius = radius * 0.7f;
        sh.rotation = new Vector3(90f, 0f, 0f);
        Noise(ps, 0.2f, 0.12f);
        Fade(ps);
        Finish(ps, mat);
        return ps;
    }

    ParticleSystem MakeFireflies()
    {
        Material mat = VibeGfx.ParticleMat(VibeGfx.Soft);
        if (mat == null) return null;
        var ps = NewSystem("Fireflies", new Vector3(0f, 1.8f, 0f));
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.30f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.95f, 1f, 0.55f, 1f), new Color(1f, 0.85f, 0.45f, 1f));
        main.maxParticles = 50;
        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius = radius;
        Noise(ps, 0.8f, 0.35f);
        Fade(ps);
        Finish(ps, mat);
        return ps;
    }

    ParticleSystem MakeLeaves()
    {
        Material mat = VibeGfx.ParticleMat(VibeGfx.Leaf);
        if (mat == null) return null;
        var ps = NewSystem("Leaves", new Vector3(0f, 9f, 0f));
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 12f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.38f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 0.85f, 0.38f, 0.95f), new Color(0.90f, 0.90f, 0.42f, 0.95f));
        main.gravityModifier = 0.06f;
        main.maxParticles = 25;
        var em = ps.emission;
        em.rateOverTime = leafRate;
        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Circle;
        sh.radius = radius * 1.1f;
        sh.rotation = new Vector3(90f, 0f, 0f);
        Noise(ps, 1.0f, 0.25f);
        var rol = ps.rotationOverLifetime;
        rol.enabled = true;
        rol.z = new ParticleSystem.MinMaxCurve(-2.2f, 2.2f);
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(0.1f, 0.45f);
        vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.1f, 0.15f);
        Fade(ps);
        Finish(ps, mat);
        return ps;
    }

    // ------------------------------------------------------------ помощники
    ParticleSystem NewSystem(string name, Vector3 local)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = local;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission;
        em.rateOverTime = 0f;
        return ps;
    }

    static void Noise(ParticleSystem ps, float strength, float freq)
    {
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = strength;
        noise.frequency = freq;
        noise.scrollSpeed = 0.3f;
    }

    static void Fade(ParticleSystem ps)
    {
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
    }

    static void Finish(ParticleSystem ps, Material mat)
    {
        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        ps.Play();
    }
}
