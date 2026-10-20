// Assets/_Project/Scripts/Lights/FireSource.cs (НЕ в Editor)
// Один «огонь» в мире: фонарь, окно, гирлянда, костёр. Это ЛОГИЧЕСКАЯ точка света, а не Light.
// Видимость огня всегда дешёвая (имитация):
//   • emissive: MaterialPropertyBlock на мешах лампы/стекла (без новых материалов на каждый огонь);
//   • glow: мягкий спрайт с additive-материалом (billboard к камере).
// Настоящий Light огню даёт FireLightPool, и только ближайшим к игроку (бюджет 3, пул из 4, плавный fade).
// Огонь живёт, пока активен его GameObject, поэтому стадии построек (SetActive у Stage-объектов) включают и гасят его сами.
// BuildingLightLink дополнительно управляет «включён/выключен» и плавностью: после StageBuilt/Finished огонь разгорается,
// после загрузки сохранения (GameEvents.BuildingRestored) встаёт сразу в нужное состояние без анимации.
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class FireSource : MonoBehaviour
{
    [Header("Вид")]
    public FlickerPreset preset = FlickerPreset.Lantern;
    public Color color = new Color(1f, 0.78f, 0.45f);
    public float intensity = 2.2f;                  // яркость настоящего света, когда он есть
    public float range = 5f;
    public Vector3 lightOffset = Vector3.zero;      // сдвиг настоящего света от точки огня (локально)

    [Header("Имитация")]
    public bool simOnly;                            // настоящий Light не получает никогда (окна, гирлянда)
    public Renderer[] emissive = new Renderer[0];   // меши, которые светятся (лампа, стекло, лампочки)
    public float emissiveStrength = 2.2f;
    public bool glow = true;
    public float glowSize = 0.9f;                   // диаметр свечения, метры
    public Vector3[] glowPoints = new Vector3[0];   // локальные точки свечения; пусто = одна в нуле

    [Header("Исходный Light из сцены (если огонь сделан из него)")]
    [Tooltip("Остаётся в сцене выключенным, чтобы все ссылки на него (TentStages.lights, CampfireFlicker) не ломались")]
    public Light authoredLight;

    // --- состояние (читает и пишет FireLightPool)
    internal float level;               // 0..1 разгорание
    internal float target = 1f;         // 1 горит, 0 погашен
    internal float realWeight;          // 0..1 доля настоящего света
    internal bool wantReal;
    internal int realRank;
    internal Light poolLight;
    internal float sortKey;

    float seed;
    float lastEmissive = -1f;
    bool prepared;
    readonly List<SpriteRenderer> glows = new List<SpriteRenderer>(4);

    public const float FadeTime = 0.7f;
    const float GlowMaxAlpha = 0.5f;
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    static MaterialPropertyBlock mpb;
    static readonly Dictionary<Material, Material> emissiveClones = new Dictionary<Material, Material>();
    static Material glowMat;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        mpb = null;
        emissiveClones.Clear();
        glowMat = null;
    }

    public Vector3 WorldPos { get { return transform.position; } }
    public Vector3 LightWorldPos { get { return transform.TransformPoint(lightOffset); } }
    public bool IsOn { get { return target > 0.5f; } }

    // ---------------------------------------------------------------- управление
    // on: гореть или нет; animate=false: встать в состояние сразу (загрузка сохранения)
    public void SetOn(bool on, bool animate)
    {
        target = on ? 1f : 0f;
        if (!animate) level = target;
    }

    void OnEnable()
    {
        seed = (GetInstanceID() & 1023) * 0.173f;
        // в первую секунду сцены (старт игры) не разгораемся с нуля; позже (стройка) разгораемся плавно
        level = Time.timeSinceLevelLoad < 1f ? target : 0f;
        lastEmissive = -1f;
        FireLightPool.Register(this);
        // Prepare() не здесь: при AddComponent из кода OnEnable срабатывает раньше, чем заданы поля. Готовим в первом Tick
    }

    void OnDisable()
    {
        FireLightPool.Unregister(this);
        for (int i = 0; i < glows.Count; i++) if (glows[i] != null) glows[i].enabled = false;
        SetEmissive(0f, true);
    }

    // ---------------------------------------------------------------- подготовка
    void Prepare()
    {
        if (prepared) return;
        prepared = true;

        // у материала должен быть включён _EMISSION, иначе MaterialPropertyBlock ничего не покажет: даём общий клон (один на материал)
        if (emissive != null)
            for (int i = 0; i < emissive.Length; i++)
            {
                var r = emissive[i];
                if (r == null) continue;
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int m = 0; m < mats.Length; m++)
                {
                    var mat = mats[m];
                    if (mat == null || !mat.HasProperty(EmissionId) || mat.IsKeywordEnabled("_EMISSION")) continue;
                    mats[m] = EmissiveClone(mat);
                    changed = true;
                }
                if (changed) r.sharedMaterials = mats;
            }

        if (glow && VibeGfx.Soft != null)
        {
            if (glowPoints == null || glowPoints.Length == 0) glowPoints = new[] { Vector3.zero };
            for (int i = 0; i < glowPoints.Length; i++) glows.Add(MakeGlow(glowPoints[i]));
        }
    }

    static Material EmissiveClone(Material src)
    {
        Material c;
        if (emissiveClones.TryGetValue(src, out c) && c != null) return c;
        c = new Material(src) { name = src.name + " (Emissive)" };
        c.EnableKeyword("_EMISSION");
        c.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        c.SetColor(EmissionId, Color.black);
        emissiveClones[src] = c;
        return c;
    }

    SpriteRenderer MakeGlow(Vector3 localPos)
    {
        var go = new GameObject("Glow");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = VibeGfx.Soft;
        sr.sharedMaterial = GlowMaterial();
        sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        sr.receiveShadows = false;
        sr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        sr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        sr.enabled = false;
        float native = Mathf.Max(0.01f, VibeGfx.Soft.bounds.size.x);
        go.transform.localScale = Vector3.one * (glowSize / native) / Mathf.Max(0.0001f, MaxLossyScale());
        return sr;
    }

    float MaxLossyScale()
    {
        Vector3 s = transform.lossyScale;
        return Mathf.Max(0.0001f, Mathf.Max(Mathf.Abs(s.x), Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z))));
    }

    // Additive даёт свечение; если шейдер вырезан из билда, берём обычный полупрозрачный (тот же путь, что у ImpactFx)
    static Material GlowMaterial()
    {
        if (glowMat != null) return glowMat;
        string[] names = { "Legacy Shaders/Particles/Additive", "Mobile/Particles/Additive" };
        for (int i = 0; i < names.Length; i++)
        {
            Shader sh = Shader.Find(names[i]);
            if (sh == null) continue;
            glowMat = new Material(sh) { name = "FireGlow (Additive)" };
            if (VibeGfx.Soft != null) glowMat.mainTexture = VibeGfx.Soft.texture;
            return glowMat;
        }
        glowMat = VibeGfx.ParticleMat(VibeGfx.Soft);
        return glowMat;
    }

    // ---------------------------------------------------------------- кадр (зовёт FireLightPool.Update)
    // night 0..1 (1 = ночь). Возвращает итоговую яркость огня (для настоящего света)
    internal float Tick(float dt, float t, float night, Quaternion camRot)
    {
        if (!prepared) Prepare();
        level = Mathf.MoveTowards(level, target, dt / FadeTime);
        float flick = LightFlicker.Evaluate(preset, t, seed);
        float lum = Mathf.Lerp(LightFlicker.DayFactor(preset), 1f, night) * level * flick;

        SetEmissive(lum, false);
        UpdateGlows(lum, flick, camRot);
        return lum;
    }

    void SetEmissive(float lum, bool force)
    {
        if (emissive == null || emissive.Length == 0) return;
        if (!force && Mathf.Abs(lum - lastEmissive) < 0.01f) return;
        lastEmissive = lum;
        if (mpb == null) mpb = new MaterialPropertyBlock();
        Color c = color * (emissiveStrength * lum);
        c.a = 1f;
        for (int i = 0; i < emissive.Length; i++)
        {
            var r = emissive[i];
            if (r == null) continue;
            r.GetPropertyBlock(mpb);
            mpb.SetColor(EmissionId, c);
            r.SetPropertyBlock(mpb);
        }
    }

    void UpdateGlows(float lum, float flick, Quaternion camRot)
    {
        if (glows.Count == 0) return;
        // когда рядом горит настоящий свет, спрайт чуть слабее, чтобы не пересвечивать
        float a = GlowMaxAlpha * Mathf.Clamp01(lum) * (1f - 0.3f * realWeight);
        bool vis = a > 0.01f;
        Color col = new Color(color.r, color.g, color.b, a);
        float pulse = 0.94f + 0.06f * flick;
        for (int i = 0; i < glows.Count; i++)
        {
            var g = glows[i];
            if (g == null) continue;
            if (g.enabled != vis) g.enabled = vis;
            if (!vis) continue;
            g.color = col;
            g.transform.rotation = camRot;
            // пульс размера без аллокаций: масштаб считаем от базового
            Vector3 s = g.transform.localScale;
            float baseScale = (glowSize / Mathf.Max(0.01f, VibeGfx.Soft.bounds.size.x)) / MaxLossyScale();
            g.transform.localScale = new Vector3(baseScale * pulse, baseScale * pulse, s.z);
        }
    }
}
