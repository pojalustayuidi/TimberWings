// Assets/_Project/Scripts/Location/CandyWaterFx.cs (НЕ в Editor)
// Брызги у подножия водопадов. Все эмиттеры острова делят общий бюджет частиц: не больше 40 живых одновременно
// (на Low 16, на Mid 28, на High 40, см. CandyWaterQuality.ParticleBudget). Бюджет делится по весам эмиттеров.
// Один ParticleSystem = 1 draw call на эмиттер, материал Project/CandyParticle (круг считается в шейдере, текстур нет).
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(ParticleSystem))]
public class CandyWaterFx : MonoBehaviour
{
    public const int HardCap = 40;

    [Range(1, 40)] public int weight = 1;
    public float lifetime = 1.1f;

    static readonly List<CandyWaterFx> All = new List<CandyWaterFx>();
    ParticleSystem ps;

    void OnEnable()
    {
        ps = GetComponent<ParticleSystem>();
        All.Add(this);
        CandyWaterQuality.Changed += Rebalance;
        Rebalance();
    }

    void OnDisable()
    {
        All.Remove(this);
        CandyWaterQuality.Changed -= Rebalance;
        Rebalance();
    }

    // раздаёт бюджет: сумма maxParticles по всем эмиттерам никогда не больше min(HardCap, бюджет тира)
    public static void Rebalance()
    {
        int budget = Mathf.Min(HardCap, CandyWaterQuality.ParticleBudget);
        int sum = 0;
        for (int i = 0; i < All.Count; i++) sum += Mathf.Max(1, All[i].weight);
        if (sum <= 0) return;
        for (int i = 0; i < All.Count; i++)
            All[i].SetCount(Mathf.FloorToInt(budget * (float)Mathf.Max(1, All[i].weight) / sum));
    }

    void SetCount(int n)
    {
        if (ps == null) return;
        var main = ps.main;
        main.maxParticles = Mathf.Max(0, n);
        var em = ps.emission;
        em.rateOverTime = n / Mathf.Max(0.2f, lifetime);
        if (n <= 0) { if (ps.isPlaying) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); }
        else if (!ps.isPlaying) ps.Play();
    }

    // Фабрика (вызывается из редактора при сборке острова). scale = worldScale (размеры в u умножаются на него),
    // halfWidth = полуширина струи/круга в u. Материал передаётся готовый (ассет), чтобы он сохранился в проекте.
    public static CandyWaterFx Create(Transform parent, string name, Vector3 localPos, float scale, float halfWidth, int weight, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);   // ось конуса вверх

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);   // иначе нельзя менять настройки

        var fx = go.AddComponent<CandyWaterFx>();
        fx.weight = weight;

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.prewarm = true;
        main.duration = 5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(fx.lifetime * 0.7f, fx.lifetime * 1.3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f * scale * 2f, 1.6f * scale * 2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.45f * scale * 2f, 0.9f * scale * 2f);
        main.startColor = new Color(1f, 1f, 1f, 0.85f);
        main.gravityModifier = 0.35f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.maxParticles = 8;

        var em = ps.emission;
        em.rateOverTime = 8f;

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle = 28f;
        sh.radius = Mathf.Max(0.1f, halfWidth * scale);

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.35f, 1f), new Keyframe(1f, 1.5f)));

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.85f, 0.95f, 1f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.15f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

        ps.Play();
        return fx;
    }
}
