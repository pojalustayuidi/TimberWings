// Assets/_Project/Scripts/Skills/GoldenTreeFx.cs (НЕ в Editor)
// Вид золотого дерева (lumber_golden): пульсирующий золотой цвет, искры, столб света (виден издалека) и табличка.
// Создаётся и удаляется сам из ResourceItem.SetGolden(true/false), вешать вручную не нужно.
// Всё рисуется кодом, префабы не нужны. Когда компонент удаляется (дерево срублено, навык сброшен, Load), цвет и объекты возвращаются как были.
using System.Collections.Generic;
using UnityEngine;

public sealed class GoldenTreeFx : MonoBehaviour
{
    public static readonly Color Gold = new Color32(0xF5, 0xC5, 0x42, 255);

    struct Tint { public Renderer r; public string prop; public Color orig; }

    static Material particleMat, beamMat;

    readonly List<Tint> tints = new List<Tint>();
    MaterialPropertyBlock mpb;
    WorldTag wtag;
    GameObject sparkles, beam;

    public static GoldenTreeFx Attach(ResourceItem tree)
    {
        var fx = tree.GetComponent<GoldenTreeFx>();
        if (fx == null) fx = tree.gameObject.AddComponent<GoldenTreeFx>();
        return fx;
    }

    void Awake()
    {
        mpb = new MaterialPropertyBlock();
        CollectRenderers();
        BuildSparkles();
        BuildBeam();
        BuildTag();
    }

    // рендереры самого дерева. Контур и кольцо ResourceHighlighter (Hull, HighlightRing) и частицы не трогаем
    void CollectRenderers()
    {
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (r == null || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            if (r.gameObject.name == "Hull" || r.gameObject.name == "HighlightRing") continue;
            var m = r.sharedMaterial;
            if (m == null) continue;
            string prop = m.HasProperty("_BaseColor") ? "_BaseColor" : (m.HasProperty("_Color") ? "_Color" : null);
            if (prop == null) continue;
            tints.Add(new Tint { r = r, prop = prop, orig = m.GetColor(prop) });
        }
    }

    void Update()
    {
        float k = 0.72f + 0.2f * Mathf.Sin(Time.time * 3f);   // медленный пульс золота
        for (int i = 0; i < tints.Count; i++)
        {
            var t = tints[i];
            if (t.r == null) continue;
            t.r.GetPropertyBlock(mpb);
            mpb.SetColor(t.prop, Color.Lerp(t.orig, Gold, k));
            t.r.SetPropertyBlock(mpb);
        }
    }

    void OnDestroy()
    {
        for (int i = 0; i < tints.Count; i++)
            if (tints[i].r != null) tints[i].r.SetPropertyBlock(null);   // цвет возвращается к цвету материала
        if (sparkles != null) Destroy(sparkles);
        if (beam != null) Destroy(beam);
        if (wtag != null) Destroy(wtag);
    }

    // локальная позиция/масштаб так, чтобы в мире получились нужные метры, даже если у дерева масштаб не 1
    Vector3 LocalPos(float worldY)
    {
        float s = Mathf.Max(0.01f, transform.lossyScale.y);
        return new Vector3(0f, worldY / s, 0f);
    }

    void BuildSparkles()
    {
        if (particleMat == null) particleMat = VibeGfx.ParticleMat(VibeGfx.Soft);
        if (particleMat == null) return;   // нет шейдера: лучше без искр, чем розовые квадраты

        sparkles = new GameObject("GoldenSparkles");
        sparkles.transform.SetParent(transform, false);
        sparkles.transform.localPosition = LocalPos(1.6f);

        var ps = sparkles.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);   // иначе нельзя менять настройки
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = 1.6f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.26f);
        main.startColor = new ParticleSystem.MinMaxGradient(Gold, new Color32(0xFF, 0xF0, 0xA0, 255));
        main.gravityModifier = -0.1f;                                     // искры плывут вверх
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Local;

        var em = ps.emission;
        em.rateOverTime = 12f;

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius = 0.9f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);

        var r = sparkles.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = particleMat;
        ps.Play();
    }

    // тонкий полупрозрачный столб: золотое дерево видно через весь остров
    void BuildBeam()
    {
        if (beamMat == null)
        {
            var sh = Shader.Find("Sprites/Default");
            if (sh == null) return;
            beamMat = new Material(sh) { name = "GoldenBeam" };
            beamMat.color = new Color(Gold.r, Gold.g, Gold.b, 0.22f);
        }

        beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beam.name = "GoldenBeam";
        var c = beam.GetComponent<Collider>();
        if (c != null) DestroyImmediate(c);   // столб не должен ни мешать, ни попадать в поиск дерева
        beam.transform.SetParent(transform, false);

        Vector3 ls = transform.lossyScale;
        float sx = Mathf.Max(0.01f, ls.x), sy = Mathf.Max(0.01f, ls.y), sz = Mathf.Max(0.01f, ls.z);
        beam.transform.localPosition = LocalPos(4f);
        beam.transform.localScale = new Vector3(0.5f / sx, 4f / sy, 0.5f / sz);   // цилиндр высотой 2 м при масштабе 1: получится 8 м

        var r = beam.GetComponent<Renderer>();
        r.sharedMaterial = beamMat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    void BuildTag()
    {
        wtag = WorldTag.Add(gameObject, false);
        wtag.offset = new Vector3(0f, 4.4f, 0f);
        wtag.SetFontSize(30f);
        wtag.SetText("<color=#F5C542>★</color> Золотое дерево: " + GameBalance.SkillGoldenWood + " брёвен");
    }
}
