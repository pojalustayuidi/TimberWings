// Assets/_Project/Scripts/Forest/MysticGlade/MysticGladeVibe.cs (НЕ в Editor)
// Живость таинственной поляны (вешает MysticGladeBuilder на корень зоны):
//   • два мягких света: фиолетовый над кроной (дышит) и бирюзовый у земли; ночью ярче;
//   • споры: бирюзово-розовые огоньки поднимаются от земли;
//   • светлячки: сиреневые, больше ночью;
//   • лепестки: розовые и лавандовые падают из кроны;
//   • туман: низкие мягкие облака у земли;
//   • кружащие огоньки: медленно ходят хороводом вокруг дерева по кольцу скамеек.
// Всё рисуется кодом (VibeGfx.Soft / VibeGfx.Leaf, VibeGfx.ParticleMat), префабы не нужны. До ~250 частиц.
// «Уменьшить эффекты» (VibeSettings.ReduceEffects): частиц вдвое меньше. Дальше effectDistance от камеры всё спит.
using UnityEngine;

public class MysticGladeVibe : MonoBehaviour
{
    public float radius = 15f;
    public float ringRadius = 7.2f;
    public float crownHeight = 15f;
    public float effectDistance = 70f;
    public Color violet = new Color(0.78f, 0.55f, 1f);
    public Color cyan = new Color(0.45f, 1f, 0.92f);
    public Color pink = new Color(1f, 0.66f, 0.82f);

    Light magic, accent;
    ParticleSystem spores, flies, petals, mist, orbit;
    float nextCheck, dark;

    void Start()
    {
        magic = MakeLight("MagicLight", new Vector3(0f, 5f, 0f), violet, 20f);
        accent = MakeLight("AccentLight", new Vector3(0f, 1.4f, 0f), cyan, 10f);
        spores = MakeSpores();
        flies = MakeFlies();
        petals = MakePetals();
        mist = MakeMist();
        orbit = MakeOrbit();
    }

    void Update()
    {
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 0.7f);
        if (magic != null) magic.intensity = Mathf.Lerp(1.4f, 3.4f, dark) * (0.85f + 0.3f * pulse);
        if (accent != null) accent.intensity = Mathf.Lerp(0.8f, 2.0f, dark) * (0.8f + 0.4f * (1f - pulse));

        if (Time.time < nextCheck) return;
        nextCheck = Time.time + 0.5f;
        dark = Mathf.Clamp01(DayNightCycle.Night01 + DayNightCycle.Twilight01 * 0.6f);
        var cam = Camera.main;
        bool near = cam == null || (cam.transform.position - transform.position).sqrMagnitude < effectDistance * effectDistance;
        float k = near ? (VibeSettings.ReduceEffects ? 0.5f : 1f) : 0f;
        Rate(spores, Mathf.Lerp(5f, 14f, dark) * k);
        Rate(flies, Mathf.Lerp(1f, 9f, dark) * k);
        Rate(petals, 1.4f * k);
        Rate(mist, Mathf.Lerp(0.5f, 0.9f, dark) * k);
        Rate(orbit, Mathf.Lerp(0.7f, 1.6f, dark) * k);
        if (magic != null) magic.enabled = near;
        if (accent != null) accent.enabled = near;
    }

    static void Rate(ParticleSystem ps, float r) { if (ps == null) return; var em = ps.emission; em.rateOverTime = r; }

    Light MakeLight(string n, Vector3 local, Color c, float range)
    {
        var go = new GameObject(n);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = local;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point; l.color = c; l.range = range; l.intensity = 2f; l.shadows = LightShadows.None;
        return l;
    }

    // ------------------------------------------------------------ системы
    ParticleSystem MakeSpores()
    {
        Material m = VibeGfx.ParticleMat(VibeGfx.Soft);
        if (m == null) return null;
        var ps = NewSystem("Spores", new Vector3(0f, 0.2f, 0f));
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
        main.startColor = new ParticleSystem.MinMaxGradient(cyan, pink);
        main.maxParticles = 90;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = radius * 0.9f; sh.rotation = new Vector3(-90f, 0f, 0f);   // вверх
        var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f); vel.y = new ParticleSystem.MinMaxCurve(0.15f, 0.4f); vel.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
        Noise(ps, 0.4f, 0.4f); Fade(ps); Finish(ps, m);
        return ps;
    }

    ParticleSystem MakeFlies()
    {
        Material m = VibeGfx.ParticleMat(VibeGfx.Soft);
        if (m == null) return null;
        var ps = NewSystem("Fireflies", new Vector3(0f, 1.8f, 0f));
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.03f, 0.12f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.10f, 0.20f);
        main.startColor = new ParticleSystem.MinMaxGradient(violet, new Color(1f, 0.95f, 1f));
        main.maxParticles = 60;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(radius * 1.5f, 3.2f, radius * 1.5f);
        Noise(ps, 0.8f, 0.5f); Fade(ps); Finish(ps, m);
        return ps;
    }

    ParticleSystem MakePetals()
    {
        Material m = VibeGfx.ParticleMat(VibeGfx.Leaf);
        if (m == null) return null;
        var ps = NewSystem("Petals", new Vector3(0f, crownHeight, 0f));
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(7f, 11f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.30f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
        main.startColor = new ParticleSystem.MinMaxGradient(pink, new Color(0.82f, 0.72f, 1f));
        main.gravityModifier = 0.06f;
        main.maxParticles = 30;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 7f; sh.rotation = new Vector3(90f, 0f, 0f);   // вниз
        Noise(ps, 1.0f, 0.25f);
        var rol = ps.rotationOverLifetime; rol.enabled = true; rol.z = new ParticleSystem.MinMaxCurve(-2.2f, 2.2f);
        var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(0.1f, 0.45f); vel.y = new ParticleSystem.MinMaxCurve(0f, 0f); vel.z = new ParticleSystem.MinMaxCurve(-0.1f, 0.15f);
        Fade(ps); Finish(ps, m);
        return ps;
    }

    ParticleSystem MakeMist()
    {
        Material m = VibeGfx.ParticleMat(VibeGfx.Soft);
        if (m == null) return null;
        var ps = NewSystem("Mist", new Vector3(0f, 0.6f, 0f));
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(10f, 14f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.03f, 0.10f);
        main.startSize = new ParticleSystem.MinMaxCurve(5f, 8f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.80f, 0.70f, 1f, 0.10f), new Color(0.92f, 0.88f, 1f, 0.16f));
        main.maxParticles = 14;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(radius * 1.6f, 0.5f, radius * 1.6f);
        Noise(ps, 0.3f, 0.2f); Fade(ps); Finish(ps, m);
        return ps;
    }

    // огоньки хороводом: частицы в локальном пространстве, орбитальная скорость вокруг вертикали
    ParticleSystem MakeOrbit()
    {
        Material m = VibeGfx.ParticleMat(VibeGfx.Soft);
        if (m == null) return null;
        var ps = NewSystem("Orbit", new Vector3(0f, 1.5f, 0f));
        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(12f, 18f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
        main.startColor = new ParticleSystem.MinMaxGradient(cyan, new Color(0.9f, 1f, 1f));
        main.maxParticles = 24;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = ringRadius; sh.radiusThickness = 0.15f; sh.rotation = new Vector3(90f, 0f, 0f);
        var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = new ParticleSystem.MinMaxCurve(0f); vel.y = new ParticleSystem.MinMaxCurve(0f); vel.z = new ParticleSystem.MinMaxCurve(0f);
        vel.orbitalX = new ParticleSystem.MinMaxCurve(0f); vel.orbitalY = new ParticleSystem.MinMaxCurve(0.22f); vel.orbitalZ = new ParticleSystem.MinMaxCurve(0f);
        Noise(ps, 0.25f, 0.5f); Fade(ps); Finish(ps, m);
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
        main.loop = true; main.playOnAwake = true; main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission; em.rateOverTime = 0f;
        return ps;
    }

    static void Noise(ParticleSystem ps, float strength, float freq)
    {
        var n = ps.noise; n.enabled = true; n.strength = strength; n.frequency = freq; n.scrollSpeed = 0.3f;
    }

    static void Fade(ParticleSystem ps)
    {
        var col = ps.colorOverLifetime; col.enabled = true;
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
