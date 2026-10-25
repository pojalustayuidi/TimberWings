// Assets/_Project/Scripts/Forest/ForestMist.cs (НЕ в Editor)  НОВЫЙ ФАЙЛ
// «Облачная дымка» леса: редкие мягкие клочья облаков медленно плывут между стволами на уровне плеч и выше.
// Зачем: лес из отдельных деревьев читается как одно целое, когда его соединяет общий воздух. Это ещё и тот самый воздушный
// «облачный» вайб: остров парит, и облака заходят в чащу. Ничего не вешать и не настраивать: объект создаётся сам при запуске.
//
//   • плотность дымки идёт по весу леса (ForestZone.Weight под игроком): на поляне и у костра дымки нет, в чаще она есть;
//   • вес меняется плавно (полсекунды на 0.25), дымка проявляется и тает без щелчков;
//   • ночью чуть тусклее; «Уменьшить эффекты» (VibeSettings.ReduceEffects) режет количество вдвое;
//   • одна система частиц (до 48 штук), следует за игроком, в сцене без игрока или без лесных зон ничего не делает;
//   • клочья спавнятся выше пояса (1.6-3.2 м над ногами игрока), поэтому не режутся о землю.
using UnityEngine;

[DisallowMultipleComponent]
public class ForestMist : MonoBehaviour
{
    [Tooltip("Сколько метров вокруг игрока живёт дымка")]
    public float radius = 15f;
    [Tooltip("Клочьев в секунду в самой чаще (при весе леса 1)")]
    public float maxRate = 2.0f;

    [Tooltip("С какого расстояния до дерева-хранителя дымка начинает окрашиваться, м")]
    public float TintRadius = 24f;

    ParticleSystem ps;
    float weight, nextCheck, tint;
    Color tintColor = new Color(0.92f, 0.95f, 1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Object.FindFirstObjectByType<ForestMist>() != null) return;
        var go = new GameObject("ForestMist");
        go.AddComponent<ForestMist>();
        DontDestroyOnLoad(go);   // меню и игра в разных сценах: объект живёт, а в меню просто молчит (нет игрока и лесных зон)
    }

    void Awake()
    {
        var sprite = VibeGfx.Soft;
        if (sprite == null) return;
        Material mat = VibeGfx.ParticleMat(sprite);
        if (mat == null) return;

        ps = gameObject.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(12f, 18f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.03f, 0.12f);
        main.startSize = new ParticleSystem.MinMaxCurve(3.2f, 5.5f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.92f, 0.95f, 1f, 0.10f), new Color(0.88f, 0.86f, 1f, 0.12f));
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 48;

        var em = ps.emission;
        em.rateOverTime = 0f;

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Box;
        sh.position = new Vector3(0f, 2.4f, 0f);
        sh.scale = new Vector3(radius * 2f, 1.6f, radius * 2f);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.35f;
        noise.frequency = 0.12f;
        noise.scrollSpeed = 0.1f;

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(0.10f, 0.28f);   // общий лёгкий ветер: все клочья плывут в одну сторону
        vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.08f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var r = GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        ps.Play();
    }

    void Update()
    {
        if (ps == null) return;

        var pm = PlayerMovement.Instance;
        float target = 0f;
        if (pm != null && ForestZone.All.Count > 0)
        {
            Vector3 p = pm.transform.position;
            transform.position = p;
            var z = ForestZone.At(p);
            if (z != null && !z.IsNeutral) target = z.Weight(p);
        }
        weight = Mathf.MoveTowards(weight, target, Time.deltaTime * 0.25f);

        if (Time.time >= nextCheck)
        {
            nextCheck = Time.time + 0.5f;
            float mul = VibeSettings.ReduceEffects ? 0.5f : 1f;
            var em = ps.emission;
            em.rateOverTime = maxRate * weight * mul;

            // ночью дымка тусклее
            float day = 1f - 0.5f * DayNightCycle.Night01;

            // у дерева-хранителя дымка подкрашивается в его пастель (плавно, чтобы лес «переливался» от одного к другому)
            Color baseA = new Color(0.92f, 0.95f, 1f), baseB = new Color(0.88f, 0.86f, 1f);
            Color want = tintColor;
            float near = 0f;
            if (pm != null)
            {
                Vector3 pp = pm.transform.position;
                for (int i = 0; i < SacredVibe.All.Count; i++)
                {
                    var s = SacredVibe.All[i];
                    if (s == null) continue;
                    Vector3 d = s.transform.position - pp; d.y = 0f;
                    float k = 1f - Mathf.Clamp01(d.magnitude / TintRadius);
                    if (k > near) { near = k; want = s.MistTint; }
                }
            }
            tint = Mathf.MoveTowards(tint, near, 0.25f);   // раз в 0.5 с: смена цвета занимает пару секунд
            if (near > 0f) tintColor = Color.Lerp(tintColor, want, 0.6f);
            float tk = tint * tint * (3f - 2f * tint);
            Color a = Color.Lerp(baseA, tintColor, tk * 0.85f), b = Color.Lerp(baseB, tintColor, tk * 0.85f);
            float boost = 1f + 0.6f * tk;   // у хранителя дымка чуть плотнее и заметнее

            var main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(a.r, a.g, a.b, 0.10f * day * boost), new Color(b.r, b.g, b.b, 0.12f * day * boost));
        }
    }
}
