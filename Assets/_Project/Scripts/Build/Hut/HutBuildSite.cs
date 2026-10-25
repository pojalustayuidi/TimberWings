// Assets/_Project/Scripts/Build/Hut/HutBuildSite.cs   (НЕ в папке Editor!)
// «Стройплощадка живёт»: пока здание между стадиями, строители стучат молотками, пилят доски, крутят лебёдку,
// груз ходит вверх-вниз на верёвке, летят опилки и пыль, на табличке заполняется полоска прогресса.
// Вешается на группу «WorkCrew» в Stage1 и Stage2 (генератор делает сам, все ссылки заполняет он).
//
// КАК ПОДКЛЮЧИТЬ К ТВОЕМУ ТАЙМЕРУ СТРОЙКИ (две строчки):
//   начало стройки:    HutBuildSite.SetWorkingFor(hutStages, true);
//   конец/отмена:      HutBuildSite.SetWorkingFor(hutStages, false);
//   прогресс 0..1:     HutBuildSite.SetProgressFor(hutStages, 0.4f);   // необязательно
// Пока ни один вызов SetWorkingFor не сделан, работает режим «alwaysWorking» (стройплощадка просто всегда занята).
// Первый же вызов SetWorkingFor выключает этот режим. Для анимации персонажа: подпишись на HutBuildSite.WorkingChanged.
using System;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("BuilderSim/Hut Build Site")]
public class HutBuildSite : MonoBehaviour
{
    public enum Wave { Sine, Strike, Spin }

    [Serializable]
    public class Anim
    {
        public Transform t;
        [Tooltip("true = вращать вокруг оси (градусы), false = двигать вдоль оси (метры)")]
        public bool rotate = true;
        public Vector3 axis = Vector3.right;
        [Tooltip("Sine = качание, Strike = удар молотком (подъём, быстрый удар, пауза), Spin = непрерывное вращение")]
        public Wave wave = Wave.Sine;
        public float amp = 20f;
        public float bias;
        [Tooltip("Значение в покое (когда стройка не идёт)")]
        public float rest;
        public float freq = 1f;
        public float phase;
        [Tooltip("Где пыль и стук (удар молотка, место пропила)")]
        public Transform fx;
        [Tooltip("Для Sine: сколько облачков опилок в секунду")]
        public float dustPerSec;

        [NonSerialized] public Quaternion baseRot;
        [NonSerialized] public Vector3 basePos;
        [NonSerialized] public float acc, lastP, dustAcc;
    }

    [Serializable]
    public class Hoist
    {
        public Transform load;           // груз на крюке (начало координат = крюк)
        public Transform top;            // откуда идёт верёвка (шкив)
        public Transform rope;           // вертикальная верёвка (цилиндр, масштабируется по длине)
        public Vector3 low, high;        // положения груза в локальных координатах его родителя
        public float period = 7f;
        public float swayDeg = 3f;
        public float phase;

        [NonSerialized] public Vector3 ropeScale;
        [NonSerialized] public Quaternion loadRot;
    }

    [Header("На каких стадиях площадка работает (совпадает с группой StageVisibility)")]
    public int minStage = 1;
    public int maxStage = 1;

    [Header("Режим")]
    [Tooltip("Пока нет сигнала от таймера стройки, площадка всегда занята. Первый вызов SetWorkingFor выключает.")]
    public bool alwaysWorking = true;
    [Tooltip("Секунд на «разгон»: инструменты плавно вступают в работу")]
    public float ramp = 1.2f;

    [Header("Анимации")]
    public Anim[] anims = new Anim[0];
    public Hoist[] hoists = new Hoist[0];

    [Header("Табличка прогресса (пивот слева, масштаб X = доля)")]
    public Transform progressFill;

    [Header("Звук")]
    [Range(0f, 1f)] public float soundVolume = 0.32f;
    public float soundRange = 28f;

    public static event Action<HutBuildSite, bool> WorkingChanged;

    // ---- служебное
    float k;                 // 0..1: насколько «в работе»
    float time;
    bool external;
    float progress = -1f;
    Vector3 fillBase = Vector3.one;
    ParticleSystem dust;
    HutStages hut;
    bool inited;

    public bool Working { get { return alwaysWorking || external; } }

    void Awake() { Init(); }

    void Init()
    {
        if (inited) return;
        inited = true;
        hut = GetComponentInParent<HutStages>(true);
        for (int i = 0; i < anims.Length; i++)
        {
            var a = anims[i];
            if (a == null || a.t == null) continue;
            a.baseRot = a.t.localRotation;
            a.basePos = a.t.localPosition;
        }
        for (int i = 0; i < hoists.Length; i++)
        {
            var h = hoists[i];
            if (h == null) continue;
            if (h.rope != null) h.ropeScale = h.rope.localScale;
            if (h.load != null) h.loadRot = h.load.localRotation;
        }
        if (progressFill != null) fillBase = progressFill.localScale;
        MakeDust();
    }

    void OnEnable()
    {
        Init();
        time = UnityEngine.Random.value * 5f;
    }

    void OnDisable() { ResetPose(); }

    void OnDestroy()
    {
        if (dust != null)
        {
            var r = dust.GetComponent<ParticleSystemRenderer>();
            if (r != null && r.sharedMaterial != null) Destroy(r.sharedMaterial);
        }
    }

    // ---------------------------------------------------------------- управление
    /// <summary>Строительство началось/закончилось (вызывает твой таймер стройки).</summary>
    public void SetWorking(bool on)
    {
        bool was = Working;
        alwaysWorking = false;
        external = on;
        if (was != Working && WorkingChanged != null) WorkingChanged(this, Working);
    }

    public void SetProgress(float p01) { progress = Mathf.Clamp01(p01); }
    public void ClearProgress() { progress = -1f; }

    public static void SetWorkingFor(Component anyInHut, bool on)
    {
        foreach (var s in SitesOf(anyInHut)) s.SetWorking(on);
    }

    public static void SetProgressFor(Component anyInHut, float p01)
    {
        foreach (var s in SitesOf(anyInHut)) s.SetProgress(p01);
    }

    static HutBuildSite[] SitesOf(Component c)
    {
        if (c == null) return new HutBuildSite[0];
        var hs = c.GetComponentInParent<HutStages>(true);
        var root = hs != null ? hs.transform : c.transform;
        return root.GetComponentsInChildren<HutBuildSite>(true);
    }

    bool InStage()
    {
        if (hut == null) return true;
        int s = hut.CurrentStage;
        return s >= minStage && (maxStage <= 0 || s <= maxStage);
    }

    // ---------------------------------------------------------------- кадр
    void Update()
    {
        if (!InStage()) return;           // стадия сменилась: площадка замирает, StageVisibility уносит детали
        float dt = Time.deltaTime;
        time += dt;
        k = Mathf.MoveTowards(k, Working ? 1f : 0f, dt / Mathf.Max(0.1f, ramp));

        for (int i = 0; i < anims.Length; i++) Step(anims[i], dt);
        for (int i = 0; i < hoists.Length; i++) StepHoist(hoists[i]);

        if (progressFill != null)
        {
            float f = progress >= 0f ? progress : Mathf.Lerp(0.06f, 0.34f + 0.1f * Mathf.Sin(time * 0.9f), k);
            progressFill.localScale = new Vector3(Mathf.Max(0.02f, f) * fillBase.x, fillBase.y, fillBase.z);
        }
    }

    void Step(Anim a, float dt)
    {
        if (a == null || a.t == null) return;
        float val;
        switch (a.wave)
        {
            case Wave.Strike:
            {
                float c = time * a.freq + a.phase;
                float p = c - Mathf.Floor(c);
                float v;
                if (p < 0.62f) { float u = p / 0.62f; v = u * u * (3f - 2f * u); }          // медленный замах
                else if (p < 0.80f) { float u = (p - 0.62f) / 0.18f; v = 1f - u * u; }       // быстрый удар
                else v = 0f;                                                                  // пауза на упоре
                if (k > 0.6f && a.lastP < 0.80f && p >= 0.80f) Hit(a);
                a.lastP = p;
                val = Mathf.Lerp(a.rest, a.bias + a.amp * v, k);
                break;
            }
            case Wave.Spin:
                a.acc += 360f * a.freq * dt * k * Mathf.Sign(a.amp == 0f ? 1f : a.amp);
                val = a.bias + a.acc;
                break;
            default:
            {
                float s = Mathf.Sin((time * a.freq + a.phase) * Mathf.PI * 2f);
                val = Mathf.Lerp(a.rest, a.bias + a.amp * s, k);
                if (a.dustPerSec > 0f && a.fx != null && k > 0.7f)
                {
                    a.dustAcc += dt * a.dustPerSec * (VibeSettings.ReduceEffects ? 0.4f : 1f);
                    while (a.dustAcc >= 1f) { a.dustAcc -= 1f; Burst(a.fx.position, 1); }
                }
                break;
            }
        }

        if (a.rotate) a.t.localRotation = a.baseRot * Quaternion.AngleAxis(val, a.axis);
        else a.t.localPosition = a.basePos + a.axis * val;
    }

    void StepHoist(Hoist h)
    {
        if (h == null || h.load == null) return;
        float c = Mathf.Repeat(time / Mathf.Max(1f, h.period) + h.phase, 1f);
        float v;
        if (c < 0.40f) { float u = c / 0.40f; v = u * u * (3f - 2f * u); }               // подъём
        else if (c < 0.50f) v = 1f;                                                       // наверху
        else if (c < 0.90f) { float u = (c - 0.50f) / 0.40f; v = 1f - u * u * (3f - 2f * u); }   // спуск
        else v = 0f;                                                                      // внизу
        v *= k;

        h.load.localPosition = Vector3.Lerp(h.low, h.high, v);
        float sway = Mathf.Sin(time * 2.1f) * h.swayDeg * (v * (1f - v) * 4f);
        h.load.localRotation = h.loadRot * Quaternion.Euler(0f, 0f, sway);

        if (h.rope != null && h.top != null && h.rope.parent != null)
        {
            Transform par = h.rope.parent;
            Vector3 a = par.InverseTransformPoint(h.top.position);
            Vector3 b = par.InverseTransformPoint(h.load.position);
            h.rope.localPosition = (a + b) * 0.5f;
            h.rope.localRotation = Quaternion.identity;
            h.rope.localScale = new Vector3(h.ropeScale.x, Mathf.Max(0.02f, Vector3.Distance(a, b)), h.ropeScale.z);
        }
    }

    // ---------------------------------------------------------------- удар: пыль, стук, щепки
    void Hit(Anim a)
    {
        Vector3 p = a.fx != null ? a.fx.position : a.t.position;
        Burst(p, VibeSettings.ReduceEffects ? 2 : 5);
        if (ImpactFx.Instance != null && !VibeSettings.ReduceEffects) ImpactFx.Instance.HammerTick(p);
        float vol = VolumeAt(p);
        if (vol > 0.02f) SfxManager.Play("thock", vol, UnityEngine.Random.Range(0.9f, 1.15f));
    }

    float VolumeAt(Vector3 p)
    {
        var cam = Camera.main;
        float d = cam != null ? Vector3.Distance(cam.transform.position, p) : 0f;
        return soundVolume * Mathf.Clamp01(1f - d / Mathf.Max(1f, soundRange));
    }

    void Burst(Vector3 pos, int n)
    {
        if (dust == null) return;
        dust.transform.position = pos;
        dust.Emit(n);
    }

    void ResetPose()
    {
        if (!inited) return;
        for (int i = 0; i < anims.Length; i++)
        {
            var a = anims[i];
            if (a == null || a.t == null) continue;
            a.t.localRotation = a.baseRot;
            a.t.localPosition = a.basePos;
            a.acc = 0f; a.lastP = 0f; a.dustAcc = 0f;
        }
        for (int i = 0; i < hoists.Length; i++)
        {
            var h = hoists[i];
            if (h == null || h.load == null) continue;
            h.load.localPosition = h.low;
            h.load.localRotation = h.loadRot;
        }
        k = 0f;
    }

    void MakeDust()
    {
        if (dust != null) return;
        var go = new GameObject("BuildDust");
        go.transform.SetParent(transform, false);
        dust = go.AddComponent<ParticleSystem>();
        dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = dust.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.18f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.97f, 0.86f, 0.62f, 0.95f), new Color(0.90f, 0.76f, 0.50f, 0.9f));
        main.gravityModifier = 0.25f;
        main.maxParticles = 80;

        var em = dust.emission;
        em.enabled = false;

        var sh = dust.shape;
        sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius = 0.05f;

        var col = dust.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var sz = dust.sizeOverLifetime;
        sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 1.4f)));

        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        rend.sharedMaterial = VibeGfx.ParticleMat(VibeGfx.Soft);
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
        dust.Play();
    }
}
