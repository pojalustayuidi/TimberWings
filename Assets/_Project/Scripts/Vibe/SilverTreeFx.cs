// Assets/_Project/Scripts/Vibe/SilverTreeFx.cs (НЕ в Editor)
// «Серебряное дерево»: ночной бонус без давления (раз за ночь, см. NightBonus). Даёт GameBalance.SilverTreeLogs брёвен и
// GameBalance.SilverTreeXp опыта сверх обычного. Пропустил: ничего не теряется, к рассвету оно просто гаснет.
// Вид: холодное серебристо-голубое свечение листвы, лунные искры, тонкий столб света (видно через весь остров),
// мягкий огонёк у кроны и табличка. Всё рисуется кодом, префабы не нужны.
//
// Компонент вешает NightBonus и снимает сам: на рассвете, когда дерево срублено или спрятано. Цвет и объекты возвращаются как были.
// Связь с рубкой (две строки, уже внесены):
//   LumberSkills.BaseWood        серебряное дерево даёт не меньше SilverTreeLogs брёвен
//   PlayerInteraction.CompleteChop   к XP добавляется SilverTree.TakeBonusXp(дерево), там же звук и тост
using System.Collections.Generic;
using UnityEngine;

public static class SilverTree
{
    public static bool Is(ResourceItem tree)
    {
        return tree != null && tree.GetComponent<SilverTreeFx>() != null;
    }

    // зовёт PlayerInteraction.CompleteChop до того, как дерево упадёт. Возвращает бонусный опыт (0, если дерево обычное)
    public static int TakeBonusXp(ResourceItem tree)
    {
        if (!Is(tree)) return 0;
        SfxManager.Play("silver", 0.9f, 1f);
        SaveIndicator.ShowToast("Серебряное дерево: +" + GameBalance.SilverTreeLogs + " брёвен и +" + GameBalance.SilverTreeXp + " опыта");
        return GameBalance.SilverTreeXp;
    }
}

public sealed class SilverTreeFx : MonoBehaviour
{
    public static readonly Color Silver = new Color32(0xB8, 0xD8, 0xFF, 255);

    struct Tint { public Renderer r; public string prop; public Color orig; }

    static Material particleMat, beamMat;

    readonly List<Tint> tints = new List<Tint>();
    MaterialPropertyBlock mpb;
    WorldTag wtag;
    GameObject sparkles, beam, glow;
    float born;

    public static SilverTreeFx Attach(ResourceItem tree)
    {
        var fx = tree.GetComponent<SilverTreeFx>();
        if (fx == null) fx = tree.gameObject.AddComponent<SilverTreeFx>();
        return fx;
    }

    void Awake()
    {
        born = Time.time;
        mpb = new MaterialPropertyBlock();
        CollectRenderers();
        BuildSparkles();
        BuildBeam();
        BuildGlow();
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
        float fadeIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.time - born) / 2.5f));   // серебро проявляется плавно
        float k = (0.62f + 0.16f * Mathf.Sin(Time.time * 2.2f)) * fadeIn;
        for (int i = 0; i < tints.Count; i++)
        {
            var t = tints[i];
            if (t.r == null) continue;
            t.r.GetPropertyBlock(mpb);
            mpb.SetColor(t.prop, Color.Lerp(t.orig, Silver, k));
            t.r.SetPropertyBlock(mpb);
        }
    }

    // дерево срублено и спрятано (ResourceItem.ChopDown) или объект выключен: серебро не переживает это
    void OnDisable()
    {
        if (!gameObject.activeInHierarchy) Destroy(this);
    }

    void OnDestroy()
    {
        for (int i = 0; i < tints.Count; i++)
            if (tints[i].r != null) tints[i].r.SetPropertyBlock(null);   // цвет возвращается к цвету материала
        if (sparkles != null) Destroy(sparkles);
        if (beam != null) Destroy(beam);
        if (glow != null) Destroy(glow);
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

        sparkles = new GameObject("SilverSparkles");
        sparkles.transform.SetParent(transform, false);
        sparkles.transform.localPosition = LocalPos(1.8f);

        var ps = sparkles.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);   // иначе нельзя менять настройки
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = 2.2f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.10f, 0.24f);
        main.startColor = new ParticleSystem.MinMaxGradient(Silver, new Color32(0xF2, 0xF8, 0xFF, 255));
        main.gravityModifier = -0.06f;                                    // искры лениво плывут вверх
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.maxParticles = 60;

        var em = ps.emission;
        em.rateOverTime = VibeSettings.ReduceEffects ? 5f : 14f;

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius = 1.1f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);

        var r = sparkles.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = particleMat;
        ps.Play();
    }

    // тонкий полупрозрачный столб: дерево видно через весь остров даже ночью
    void BuildBeam()
    {
        if (beamMat == null)
        {
            var sh = Shader.Find("Sprites/Default");
            if (sh == null) return;
            beamMat = new Material(sh) { name = "SilverBeam" };
            beamMat.color = new Color(Silver.r, Silver.g, Silver.b, 0.18f);
        }

        beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beam.name = "SilverBeam";
        var c = beam.GetComponent<Collider>();
        if (c != null) DestroyImmediate(c);   // столб не должен ни мешать, ни попадать в поиск дерева
        beam.transform.SetParent(transform, false);

        Vector3 ls = transform.lossyScale;
        float sx = Mathf.Max(0.01f, ls.x), sy = Mathf.Max(0.01f, ls.y), sz = Mathf.Max(0.01f, ls.z);
        beam.transform.localPosition = LocalPos(3.5f);
        beam.transform.localScale = new Vector3(0.45f / sx, 3.5f / sy, 0.45f / sz);   // цилиндр высотой 2 м при масштабе 1: получится 7 м

        var r = beam.GetComponent<Renderer>();
        r.sharedMaterial = beamMat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    // мягкий холодный огонёк у кроны: вокруг дерева ночью светлее. Без теней, один на всю игру (серебряное дерево одно за раз)
    void BuildGlow()
    {
        glow = new GameObject("SilverGlow");
        glow.transform.SetParent(transform, false);
        glow.transform.localPosition = LocalPos(2.2f);
        var l = glow.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = Silver;
        l.intensity = 1.6f;
        l.range = 6f;
        l.shadows = LightShadows.None;
    }

    void BuildTag()
    {
        wtag = WorldTag.Add(gameObject, false);
        wtag.offset = new Vector3(0f, 4.4f, 0f);
        wtag.SetFontSize(30f);
        wtag.SetText("<color=#B8D8FF>★</color> Серебряное дерево: " + GameBalance.SilverTreeLogs + " брёвен");
    }
}
