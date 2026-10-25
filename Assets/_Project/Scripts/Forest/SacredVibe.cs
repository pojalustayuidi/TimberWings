// Assets/_Project/Scripts/Forest/SacredVibe.cs (НЕ в Editor)  НОВЫЙ ФАЙЛ
// Живость «деревьев-хранителей»: нерубимых деревьев-достопримечательностей (облачное, цветущее, кристальное, фонарное, сонное).
// Вешает ForestSacred.cs (Editor) на корень префаба Sacred_<Вид>. Руками ничего настраивать не нужно.
//
// ЕДИНЫЙ ЗНАК «ЭТО НЕ РУБИТСЯ» (одинаковый у всех видов, читается без слов):
//   1. каменное кольцо у корней с бирюзовыми светящимися рунами (у рубимых деревьев кольца нет никогда);
//   2. парящий над кроной бирюзовый кристалл-знак: медленно вращается и покачивается, виден издалека;
//   3. цвета не как у рубимых: пастель (облачно-белый, розовый, мятно-лиловый), у рубимых обычная зелень, берёза и осень.
// Свои «фишки» у каждого вида: облачко искр и клочья облаков, лепестки, мерцание кристаллов, огоньки-фонарики, спящее дыхание.
//
// Что делает сам:
//   • руны, кристалл-знак и (у кристального и фонарного) светящиеся части «дышат» яркостью, ночью горят сильнее (MaterialPropertyBlock);
//   • дерево чуть покачивается на ветру, у сонного ещё и «дышит» (масштаб ±1%);
//   • частицы по виду; дальше effectDistance от камеры всё выключается;
//   • Burst() вызывает LandmarkTree при нажатии E: вспышка рун, кристалл раскручивается, сыплется горсть частиц.
// Всё рисуется кодом (спрайты VibeGfx.Soft / VibeGfx.Leaf, материал VibeGfx.ParticleMat). Если их нет, частицы тихо пропускаются.
// «Уменьшить эффекты» (VibeSettings.ReduceEffects): частиц вдвое меньше, покачивания нет.
using System.Collections.Generic;
using UnityEngine;

public enum SacredKind { Cloud = 0, Blossom = 1, Crystal = 2, Lantern = 3, Sleeper = 4 }

[DisallowMultipleComponent]
public class SacredVibe : MonoBehaviour
{
    [Header("Вид")]
    public SacredKind kind = SacredKind.Cloud;

    [Header("Части (заполняет ForestSacred.cs)")]
    [Tooltip("Дочерний объект с мешами дерева: покачивается и «дышит»")]
    public Transform visual;
    [Tooltip("Парящий кристалл-знак над кроной")]
    public Transform gem;
    [Tooltip("Высота кристалла над землёй (локально), м")]
    public float gemHeight = 9f;
    [Tooltip("Рендерер каменного кольца: в нём руны лежат отдельным материалом")]
    public Renderer runes;
    public int runeMaterialIndex = 1;
    public Renderer gemRenderer;
    public int gemMaterialIndex = 0;
    [Tooltip("Рендерер дерева, у которого один из материалов светится (кристаллы, фонарики). Пусто = не светится")]
    public Renderer glowRenderer;
    public int glowMaterialIndex = 2;

    [Header("Крона (где живут частицы)")]
    public float crownHeight = 5f;
    public float crownRadius = 2.5f;

    [Header("Живость")]
    [Tooltip("Покачивание на ветру, градусы")]
    public float swayDeg = 0.7f;
    [Tooltip("«Дыхание»: на сколько доли меняется высота (0 = нет). У сонного 0.012")]
    public float breathe = 0f;
    [Tooltip("Дальше этого расстояния от камеры эффекты спят, м")]
    public float effectDistance = 55f;

    struct Glow { public Renderer r; public int idx; public Color top, bottom; public bool ok, hasBottom; }

    // все хранители сцены: по ним ForestMist подкрашивает дымку (рядом с цветущим она розовеет, у кристального мятная и т.д.)
    public static readonly List<SacredVibe> All = new List<SacredVibe>(12);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { All.Clear(); }

    void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    void OnDisable() { All.Remove(this); }

    // цвет «воздуха» вокруг дерева (пастель по виду)
    public Color MistTint
    {
        get
        {
            switch (kind)
            {
                case SacredKind.Cloud:   return new Color(0.92f, 0.95f, 1.00f);
                case SacredKind.Blossom: return new Color(1.00f, 0.82f, 0.92f);
                case SacredKind.Crystal: return new Color(0.72f, 1.00f, 0.96f);
                case SacredKind.Lantern: return new Color(1.00f, 0.90f, 0.70f);
                default:                 return new Color(0.80f, 1.00f, 0.86f);
            }
        }
    }

    Glow gRunes, gGem, gBody;
    ParticleSystem mainPs, accentPs;
    MaterialPropertyBlock mpb;
    float phase, nextCheck, nextGlow, flash, spinKick, gemAngle;
    bool near = true;
    Transform cam;

    static readonly int BaseId = Shader.PropertyToID("_BaseColor");
    static readonly int BottomId = Shader.PropertyToID("_BottomColor");

    void Start()
    {
        phase = Random.value * 6.28f + transform.position.x * 0.37f;
        gRunes = Prepare(runes, runeMaterialIndex);
        gGem = Prepare(gemRenderer, gemMaterialIndex);
        gBody = Prepare(glowRenderer, glowMaterialIndex);
        BuildParticles();
    }

    void Update()
    {
        if (Time.time >= nextCheck)
        {
            nextCheck = Time.time + 0.5f;
            near = IsNear();
            ApplyRates();
        }
        if (!near) return;

        float t = Time.time;

        if (visual != null)
        {
            float sw = VibeSettings.ReduceEffects ? 0f : swayDeg;
            visual.localRotation = Quaternion.Euler(Mathf.Sin(t * 0.7f + phase) * sw, 0f, Mathf.Sin(t * 0.53f + phase * 1.7f) * sw);
            if (breathe > 0f)
            {
                float k = 1f + Mathf.Sin(t * 0.9f + phase) * breathe;
                visual.localScale = new Vector3(1f + (k - 1f) * 0.5f, k, 1f + (k - 1f) * 0.5f);
            }
        }

        spinKick = Mathf.MoveTowards(spinKick, 0f, Time.deltaTime * 140f);
        if (gem != null)
        {
            gemAngle += (36f + spinKick) * Time.deltaTime;
            gem.localPosition = new Vector3(0f, gemHeight + Mathf.Sin(t * 1.1f + phase) * 0.22f, 0f);
            gem.localRotation = Quaternion.Euler(0f, gemAngle, 0f);
        }

        if (t >= nextGlow)
        {
            nextGlow = t + 0.05f;
            flash = Mathf.MoveTowards(flash, 0f, 0.05f * 0.9f);
            float dark = Mathf.Clamp01(DayNightCycle.Night01 + DayNightCycle.Twilight01 * 0.6f);
            float breath = 0.5f + 0.5f * Mathf.Sin(t * 1.3f + phase);
            float k = Mathf.Lerp(0.72f, 1.15f, breath) * (1f + 0.55f * dark) + flash * 1.4f;
            Apply(gRunes, k);
            Apply(gGem, k * 1.1f);
            float kb = Mathf.Lerp(0.85f, 1.2f, 0.5f + 0.5f * Mathf.Sin(t * 1.9f + phase * 2f)) * (1f + 0.5f * dark) + flash;
            Apply(gBody, kb);
        }
    }

    // E на дереве (LandmarkTree.Interact): вспышка, кристалл раскручивается, горсть частиц
    public void Burst()
    {
        flash = 1f;
        spinKick = 360f;
        if (mainPs != null) mainPs.Emit(kind == SacredKind.Blossom ? 26 : 18);
        if (accentPs != null) accentPs.Emit(4);
    }

    bool IsNear()
    {
        if (cam == null)
        {
            var c = Camera.main;
            if (c == null) return true;
            cam = c.transform;
        }
        return (cam.position - transform.position).sqrMagnitude < effectDistance * effectDistance;
    }

    // ------------------------------------------------------------ свечение материала
    static Glow Prepare(Renderer r, int idx)
    {
        var g = new Glow { r = r, idx = idx };
        if (r == null) return g;
        var mats = r.sharedMaterials;
        if (idx < 0 || idx >= mats.Length || mats[idx] == null) return g;
        var m = mats[idx];
        if (!m.HasProperty(BaseId)) return g;
        g.top = m.GetColor(BaseId);
        g.hasBottom = m.HasProperty(BottomId);   // нижний цвет необязателен: без него светится только верх
        if (g.hasBottom) g.bottom = m.GetColor(BottomId);
        g.ok = true;
        return g;
    }

    void Apply(Glow g, float k)
    {
        if (!g.ok || g.r == null) return;
        if (mpb == null) mpb = new MaterialPropertyBlock();
        g.r.GetPropertyBlock(mpb, g.idx);
        mpb.SetColor(BaseId, new Color(g.top.r * k, g.top.g * k, g.top.b * k, 1f));
        if (g.hasBottom) mpb.SetColor(BottomId, new Color(g.bottom.r * k, g.bottom.g * k, g.bottom.b * k, 1f));
        g.r.SetPropertyBlock(mpb, g.idx);
    }

    // ------------------------------------------------------------ частицы
    void ApplyRates()
    {
        float mul = near ? (VibeSettings.ReduceEffects ? 0.5f : 1f) : 0f;
        float dark = Mathf.Clamp01(DayNightCycle.Night01 + DayNightCycle.Twilight01 * 0.6f);
        float rm = 0f, ra = 0f;
        switch (kind)
        {
            case SacredKind.Cloud:   rm = 4f; ra = 0.7f; break;
            case SacredKind.Blossom: rm = 2.6f; break;
            case SacredKind.Crystal: rm = Mathf.Lerp(5f, 11f, dark); break;
            case SacredKind.Lantern: rm = Mathf.Lerp(1.5f, 6f, dark); break;
            case SacredKind.Sleeper: rm = 2.2f; ra = 0.5f; break;
        }
        SetRate(mainPs, rm * mul);
        SetRate(accentPs, ra * mul);
    }

    static void SetRate(ParticleSystem ps, float rate)
    {
        if (ps == null) return;
        var em = ps.emission;
        em.rateOverTime = rate;
    }

    void BuildParticles()
    {
        Sprite soft = VibeGfx.Soft, leaf = VibeGfx.Leaf;
        switch (kind)
        {
            case SacredKind.Cloud:
                // искорки света над облачной кроной и мягкие клочья облаков
                mainPs = Make("Motes", new Vector3(0f, crownHeight, 0f), soft, 4f, 7f, 0.05f, 0.18f, 0.08f, 0.20f,
                    new Color(1f, 1f, 1f, 0.95f), new Color(0.82f, 0.88f, 1f, 0.95f), -0.01f, 40, crownRadius, false, 0.5f, 0.35f, false, false);
                accentPs = Make("Wisps", new Vector3(0f, crownHeight - 0.4f, 0f), soft, 8f, 12f, 0.04f, 0.12f, 1.3f, 2.2f,
                    new Color(1f, 1f, 1f, 0.16f), new Color(0.88f, 0.90f, 1f, 0.22f), 0f, 14, crownRadius * 0.9f, false, 0.25f, 0.2f, false, false);
                break;
            case SacredKind.Blossom:
                mainPs = Make("Petals", new Vector3(0f, crownHeight, 0f), leaf, 7f, 11f, 0f, 0f, 0.18f, 0.32f,
                    new Color(1f, 0.78f, 0.88f, 0.95f), new Color(1f, 0.92f, 0.96f, 0.95f), 0.045f, 60, crownRadius, true, 0.9f, 0.22f, false, true);
                break;
            case SacredKind.Crystal:
                mainPs = Make("Sparkles", new Vector3(0f, crownHeight - 0.5f, 0f), soft, 1.6f, 3.2f, 0.05f, 0.25f, 0.08f, 0.22f,
                    new Color(0.60f, 0.95f, 1f, 1f), new Color(0.78f, 0.66f, 1f, 1f), 0f, 50, crownRadius, false, 0.3f, 0.6f, true, false);
                break;
            case SacredKind.Lantern:
                mainPs = Make("Embers", new Vector3(0f, crownHeight - 1.2f, 0f), soft, 3f, 5f, 0.05f, 0.20f, 0.07f, 0.16f,
                    new Color(1f, 0.82f, 0.45f, 1f), new Color(1f, 0.62f, 0.30f, 1f), -0.035f, 40, crownRadius * 0.7f, false, 0.4f, 0.4f, false, false);
                break;
            case SacredKind.Sleeper:
                mainPs = Make("Spores", new Vector3(0f, crownHeight - 0.5f, 0f), soft, 6f, 9f, 0.05f, 0.18f, 0.10f, 0.26f,
                    new Color(0.70f, 1f, 0.85f, 0.9f), new Color(0.85f, 1f, 0.95f, 0.9f), -0.012f, 40, crownRadius, false, 0.45f, 0.3f, false, false);
                accentPs = Make("Leaves", new Vector3(0f, crownHeight, 0f), leaf, 8f, 12f, 0f, 0f, 0.20f, 0.34f,
                    new Color(0.55f, 0.85f, 0.40f, 0.95f), new Color(0.86f, 0.92f, 0.45f, 0.95f), 0.06f, 20, crownRadius * 0.9f, true, 1.0f, 0.25f, false, true);
                break;
        }
        ApplyRates();
    }

    // общий конструктор системы частиц. flatDisc: круг над кроной (падают вниз), иначе шар. windSpin: лёгкий боковой ветер и вращение (листья, лепестки)
    ParticleSystem Make(string goName, Vector3 local, Sprite sprite, float lifeMin, float lifeMax, float spdMin, float spdMax,
        float sizeMin, float sizeMax, Color c0, Color c1, float gravity, int max, float shapeRadius, bool flatDisc,
        float noiseStrength, float noiseFreq, bool sizeBell, bool windSpin)
    {
        if (sprite == null) return null;
        Material mat = VibeGfx.ParticleMat(sprite);
        if (mat == null) return null;

        var go = new GameObject(goName);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = local;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
        main.startSpeed = new ParticleSystem.MinMaxCurve(spdMin, spdMax);
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
        main.startColor = new ParticleSystem.MinMaxGradient(c0, c1);
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = max;

        var em = ps.emission;
        em.rateOverTime = 0f;

        var sh = ps.shape;
        if (flatDisc)
        {
            sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = shapeRadius;
            sh.rotation = new Vector3(90f, 0f, 0f);
        }
        else
        {
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = shapeRadius;
        }

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = noiseStrength;
        noise.frequency = noiseFreq;
        noise.scrollSpeed = 0.3f;

        if (windSpin)
        {
            var rol = ps.rotationOverLifetime;
            rol.enabled = true;
            rol.z = new ParticleSystem.MinMaxCurve(-2.0f, 2.0f);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0.15f, 0.5f);
            vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.1f, 0.15f);
        }

        if (sizeBell)
        {
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            var curve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.35f, 1f), new Keyframe(1f, 0f));
            sol.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        ps.Play();
        return ps;
    }
}
