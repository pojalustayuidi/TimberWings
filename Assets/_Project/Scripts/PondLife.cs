// Assets/_Project/Scripts/PondLife.cs
// Рантайм-«жизнь» пруда, который строит PondBuilder (Tools > BuilderSim > Природа > Pond Builder).
// Кладётся в Assets/_Project/Scripts/ (НЕ в Editor).
//
//  * рыбки плавают по пруду, виляют хвостом, поворачиваются плавно и иногда выпрыгивают с брызгами
//  * поклёвка: раз в несколько секунд рыбка подплывает к поплавку, поплавок ныряет, на воде круги. Событие OnBite для игры
//  * кувшинки качаются на воде, фонари мерцают, светлячки включаются к ночи
//  * круги на воде: до 6 колец одновременно уходят в шейдер Project/Pond/Water через глобальный массив _PondRipples
//
// Свет: glow 0 = день, 1 = ночь. Либо ставьте руками (SetGlow), либо включите autoFromSun и перетащите
// главный Directional Light: чем ниже солнце, тем ярче фонари и светлячки.
using UnityEngine;

[DisallowMultipleComponent]
public class PondLife : MonoBehaviour
{
    [Header("Геометрия (заполняет PondBuilder)")]
    public float waterY = 0.4f;          // высота водной глади в локальных координатах пруда
    public float[] shoreR;               // радиус берега по кругу (угол 0 = +X, дальше к +Z), равномерно

    [Header("Рыбки")]
    public Transform[] fish;
    public float fishDepth = 0.17f;                                  // на сколько рыбка ниже глади
    public Vector2 fishSpeed = new Vector2(0.45f, 1.0f);             // м/с, min..max
    public Vector2 jumpInterval = new Vector2(7f, 16f);              // как часто кто-то выпрыгивает
    public float jumpHeight = 0.75f;                                 // высота над водой
    public ParticleSystem splash;

    [Header("Рыбалка")]
    public Transform bobber;
    public Transform rodTip;
    public Transform line;                                           // тонкий цилиндр от кончика удочки до поплавка
    public Vector2 biteInterval = new Vector2(6f, 13f);              // пауза между поклёвками
    public float lineThickness = 0.014f;

    [Header("Кувшинки")]
    public Transform[] pads;

    [Header("Свет")]
    [Range(0f, 1f)] public float glow = 0.65f;
    public bool autoFromSun;
    public Light sun;
    public Light[] lights;               // фонари
    public Transform[] halos;            // ореолы вокруг фонарей (поворачиваются к камере)
    public Light poolLight;              // бирюзовая подсветка пруда
    public ParticleSystem fireflies;
    public Renderer decorRenderer;       // сюда пишется _Glow

    [Header("Рябь")]
    public bool ambientRipples = true;

    /// <summary>Рыбка клюнула на поплавок.</summary>
    public event System.Action OnBite;

    public Vector3 BobberWorldPosition { get { return bobber != null ? bobber.position : transform.position; } }

    static readonly int RipId = Shader.PropertyToID("_PondRipples");
    static readonly int PhaseId = Shader.PropertyToID("_Phase");
    static readonly int WiggleId = Shader.PropertyToID("_WiggleSpeed");
    static readonly int GlowId = Shader.PropertyToID("_Glow");
    const int RippleCount = 6;

    class FishState
    {
        public Transform t;
        public Vector2 pos, target;                 // локальные X, Z
        public float heading, speed, turn, retarget, bobPhase, roll, turnRate, fleeT;
        public float jumpT = -1f, jumpDur;
        public bool splashIn, splashOut;
        public bool attract;
        public float attractT;
    }

    FishState[] fs = new FishState[0];
    readonly Vector4[] ripples = new Vector4[RippleCount];
    int ripIdx;
    float biteTimer, jumpTimer, ambientTimer, dip;
    Vector3 bobberBase;
    Vector3[] padBase;
    float[] padPhase, padYaw;
    float[] lightBase, haloBase;
    float poolBase, fireflyRate;
    MaterialPropertyBlock decorMpb;
    float lastGlow = -1f;
    Camera cam;

    // ------------------------------------------------------------------ жизненный цикл
    void Start()
    {
        if (shoreR == null || shoreR.Length < 8) { enabled = false; return; }

        // рыбки
        int n = fish != null ? fish.Length : 0;
        fs = new FishState[n];
        var mpb = new MaterialPropertyBlock();
        for (int i = 0; i < n; i++)
        {
            var f = new FishState();
            f.t = fish[i];
            Vector3 lp = f.t.localPosition;
            f.pos = new Vector2(lp.x, lp.z);
            f.heading = f.t.localEulerAngles.y;
            f.bobPhase = Random.value * 6.2832f;
            PickTarget(f);
            fs[i] = f;

            var r = f.t.GetComponent<Renderer>();
            if (r != null)
            {
                r.GetPropertyBlock(mpb);
                mpb.SetFloat(PhaseId, Random.value * 6.2832f);
                mpb.SetFloat(WiggleId, Random.Range(6.5f, 10f));
                r.SetPropertyBlock(mpb);
            }
        }

        // кувшинки
        int pc = pads != null ? pads.Length : 0;
        padBase = new Vector3[pc]; padPhase = new float[pc]; padYaw = new float[pc];
        for (int i = 0; i < pc; i++)
        {
            padBase[i] = pads[i].localPosition;
            padYaw[i] = pads[i].localEulerAngles.y;
            padPhase[i] = Random.value * 6.2832f;
        }

        if (bobber != null) bobberBase = bobber.localPosition;

        // свет
        int lc = lights != null ? lights.Length : 0;
        lightBase = new float[lc];
        for (int i = 0; i < lc; i++) lightBase[i] = lights[i] != null ? lights[i].intensity : 0f;
        int hc = halos != null ? halos.Length : 0;
        haloBase = new float[hc];
        for (int i = 0; i < hc; i++) haloBase[i] = halos[i] != null ? halos[i].localScale.x : 1f;
        if (poolLight != null) poolBase = poolLight.intensity;
        if (fireflies != null) fireflyRate = fireflies.emission.rateOverTime.constant;
        if (splash != null && !splash.isPlaying) splash.Play();

        decorMpb = new MaterialPropertyBlock();
        biteTimer = Random.Range(biteInterval.x, biteInterval.y);
        jumpTimer = Random.Range(jumpInterval.x, jumpInterval.y);
        ambientTimer = Random.Range(0.5f, 2f);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        float time = Time.time;
        UpdateGlow();
        UpdateRipples(dt);
        UpdateFish(dt, time);
        UpdateFishing(dt, time);
        UpdatePads(time);
        UpdateLights(time);
    }

    void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        if (cam != null && halos != null)
            for (int i = 0; i < halos.Length; i++)
                if (halos[i] != null) halos[i].rotation = cam.transform.rotation;

        if (line != null && rodTip != null && bobber != null)
            PlaceLine(line, rodTip.position, bobber.position, lineThickness);
    }

    // ------------------------------------------------------------------ публичное API
    public void SetGlow(float value) { glow = Mathf.Clamp01(value); autoFromSun = false; }

    /// <summary>Принудительно устроить поклёвку в ближайшую секунду.</summary>
    public void TriggerBite() { biteTimer = 0f; }

    /// <summary>Круг на воде в мировой точке (сила 0..1).</summary>
    public void AddRipple(Vector3 worldPos, float strength)
    {
        ripples[ripIdx] = new Vector4(worldPos.x, worldPos.z, 0f, Mathf.Clamp01(strength));
        ripIdx = (ripIdx + 1) % RippleCount;
    }

    /// <summary>Ставит тонкий цилиндр (меш по оси Z, длина 1, центр в нуле) между двумя точками. Использует и PondBuilder.</summary>
    public static void PlaceLine(Transform lineTr, Vector3 a, Vector3 b, float thickness)
    {
        Vector3 d = b - a;
        float len = d.magnitude;
        if (len < 0.001f) return;
        lineTr.position = (a + b) * 0.5f;
        lineTr.rotation = Quaternion.LookRotation(d / len);
        lineTr.localScale = new Vector3(thickness, thickness, len);
    }

    // ------------------------------------------------------------------ контур пруда
    float ShoreAt(float theta)
    {
        int n = shoreR.Length;
        float u = theta / (Mathf.PI * 2f);
        u -= Mathf.Floor(u);
        float x = u * n;
        int a = Mathf.Min((int)x, n - 1);
        int b = (a + 1) % n;
        return Mathf.Lerp(shoreR[a], shoreR[b], x - a);
    }

    bool Inside(Vector2 p, float margin)
    {
        return p.magnitude < ShoreAt(Mathf.Atan2(p.y, p.x)) * margin;
    }

    Vector2 RandomInPond(float margin)
    {
        float th = Random.value * Mathf.PI * 2f;
        float rho = Mathf.Sqrt(Random.value) * ShoreAt(th) * margin;
        return new Vector2(Mathf.Cos(th) * rho, Mathf.Sin(th) * rho);
    }

    Vector3 ToWorld(Vector2 p) { return transform.TransformPoint(new Vector3(p.x, waterY, p.y)); }

    // ------------------------------------------------------------------ свет
    void UpdateGlow()
    {
        if (autoFromSun && sun != null)
        {
            float elev = -sun.transform.forward.y;                       // 1 = солнце в зените
            float day = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.02f, 0.45f, elev));
            glow = 1f - day;
        }
        if (decorRenderer != null && Mathf.Abs(glow - lastGlow) > 0.002f)
        {
            decorRenderer.GetPropertyBlock(decorMpb);
            decorMpb.SetFloat(GlowId, glow);
            decorRenderer.SetPropertyBlock(decorMpb);
            lastGlow = glow;
        }
    }

    void UpdateLights(float time)
    {
        float g = glow;
        if (lights != null)
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] == null) continue;
                float flick = 0.93f + 0.07f * Mathf.PerlinNoise(time * 2.3f, i * 7.1f);
                lights[i].intensity = lightBase[i] * Mathf.Lerp(0.45f, 1f, g) * flick;
            }
        if (halos != null)
            for (int i = 0; i < halos.Length; i++)
            {
                if (halos[i] == null) continue;
                float flick = 0.95f + 0.05f * Mathf.PerlinNoise(time * 2.0f, 50f + i * 3.3f);
                halos[i].localScale = Vector3.one * (haloBase[i] * Mathf.Lerp(0.7f, 1.2f, g) * flick);
            }
        if (poolLight != null) poolLight.intensity = poolBase * Mathf.Lerp(0.5f, 1f, g);
        if (fireflies != null)
        {
            var em = fireflies.emission;
            em.rateOverTime = fireflyRate * Mathf.Lerp(0.25f, 1f, g);
        }
    }

    // ------------------------------------------------------------------ круги на воде
    void UpdateRipples(float dt)
    {
        for (int i = 0; i < RippleCount; i++)
        {
            Vector4 r = ripples[i];
            if (r.w <= 0f) continue;
            r.z += dt;
            if (r.z > 2.4f) r.w = 0f;
            ripples[i] = r;
        }

        ambientTimer -= dt;
        if (ambientRipples && ambientTimer <= 0f)
        {
            ambientTimer = Random.Range(1.2f, 3f);
            AddRipple(ToWorld(RandomInPond(0.8f)), 0.45f);
        }

        Shader.SetGlobalVectorArray(RipId, ripples);
    }

    // ------------------------------------------------------------------ рыбки
    void UpdateFish(float dt, float time)
    {
        if (fs.Length == 0) return;

        jumpTimer -= dt;
        if (jumpTimer <= 0f)
        {
            jumpTimer = Random.Range(jumpInterval.x, jumpInterval.y);
            StartJump();
        }

        for (int i = 0; i < fs.Length; i++) StepFish(fs[i], dt);

        // чтобы не слипались
        for (int i = 0; i < fs.Length; i++)
            for (int j = i + 1; j < fs.Length; j++)
            {
                if (fs[i].jumpT >= 0f || fs[j].jumpT >= 0f) continue;
                Vector2 d = fs[i].pos - fs[j].pos;
                float m = d.magnitude;
                if (m < 0.55f && m > 0.0001f)
                {
                    Vector2 push = d / m * ((0.55f - m) * 0.5f);
                    fs[i].pos += push;
                    fs[j].pos -= push;
                }
            }

        for (int i = 0; i < fs.Length; i++) ApplyFish(fs[i], time);
    }

    void PickTarget(FishState f)
    {
        Vector2 p = RandomInPond(0.78f);
        for (int k = 0; k < 8; k++)
        {
            p = RandomInPond(0.78f);
            if ((p - f.pos).sqrMagnitude > 2.0f) break;
        }
        f.target = p;
        f.speed = Random.Range(fishSpeed.x, fishSpeed.y);
        f.turn = Random.Range(70f, 130f);
        f.retarget = Random.Range(2.5f, 6f);
    }

    void StepFish(FishState f, float dt)
    {
        // прыжок: летит по дуге вдоль курса
        if (f.jumpT >= 0f)
        {
            f.jumpT += dt / f.jumpDur;
            float h = f.heading * Mathf.Deg2Rad;
            f.pos += new Vector2(Mathf.Sin(h), Mathf.Cos(h)) * (1.5f * dt);

            float u = Mathf.Clamp01(f.jumpT);
            float y = waterY - fishDepth + (fishDepth + jumpHeight) * Mathf.Sin(Mathf.PI * u);
            if (!f.splashIn && y > waterY) { f.splashIn = true; Splash(f.pos, 0.9f); }
            if (f.splashIn && !f.splashOut && u > 0.5f && y < waterY) { f.splashOut = true; Splash(f.pos, 1f); }
            if (f.jumpT >= 1f) { f.jumpT = -1f; PickTarget(f); }
            return;
        }

        f.retarget -= dt;
        f.fleeT -= dt;

        Vector2 to;
        float dist;
        if (f.attract)
        {
            f.attractT += dt;
            Vector2 bp = new Vector2(bobberBase.x, bobberBase.z);
            f.target = bp;
            to = bp - f.pos;
            dist = to.magnitude;
            if (dist < 0.45f || f.attractT > 9f) FishBites(f, dist < 0.45f);
            to = f.target - f.pos;
            dist = to.magnitude;
        }
        else
        {
            to = f.target - f.pos;
            dist = to.magnitude;
            if (dist < 0.5f || f.retarget <= 0f)
            {
                PickTarget(f);
                to = f.target - f.pos;
                dist = to.magnitude;
            }
        }

        float want = Mathf.Atan2(to.x, to.y) * Mathf.Rad2Deg;
        float old = f.heading;
        f.heading = Mathf.MoveTowardsAngle(f.heading, want, f.turn * dt * (f.fleeT > 0f ? 1.6f : 1f));
        f.turnRate = Mathf.DeltaAngle(old, f.heading) / Mathf.Max(dt, 0.0001f);

        float sp = f.speed * (f.fleeT > 0f ? 2.2f : 1f) * Mathf.Clamp01(dist * 0.8f + 0.35f);
        float hr = f.heading * Mathf.Deg2Rad;
        Vector2 np = f.pos + new Vector2(Mathf.Sin(hr), Mathf.Cos(hr)) * (sp * dt);
        if (f.attract || Inside(np, 0.86f)) f.pos = np;
        else { f.target = RandomInPond(0.45f); f.retarget = Random.Range(2f, 4f); }
    }

    void ApplyFish(FishState f, float time)
    {
        if (f.jumpT >= 0f)
        {
            float u = Mathf.Clamp01(f.jumpT);
            float y = waterY - fishDepth + (fishDepth + jumpHeight) * Mathf.Sin(Mathf.PI * u);
            float pitch = -Mathf.Cos(Mathf.PI * u) * 55f;                // нос вверх при взлёте, вниз при падении
            f.t.localPosition = new Vector3(f.pos.x, y, f.pos.y);
            f.t.localRotation = Quaternion.Euler(pitch, f.heading, 0f);
            return;
        }
        float bob = Mathf.Sin(time * 1.3f + f.bobPhase) * 0.03f;
        f.roll = Mathf.Lerp(f.roll, Mathf.Clamp(-f.turnRate * 0.12f, -25f, 25f), 0.15f);
        f.t.localPosition = new Vector3(f.pos.x, waterY - fishDepth + bob, f.pos.y);
        f.t.localRotation = Quaternion.Euler(0f, f.heading, f.roll);
    }

    void StartJump()
    {
        for (int tries = 0; tries < 6; tries++)
        {
            FishState f = fs[Random.Range(0, fs.Length)];
            if (f.jumpT >= 0f || f.attract) continue;
            float h = f.heading * Mathf.Deg2Rad;
            Vector2 ahead = f.pos + new Vector2(Mathf.Sin(h), Mathf.Cos(h)) * 1.6f;
            if (!Inside(ahead, 0.8f)) continue;                          // не прыгаем в берег
            f.jumpT = 0f;
            f.jumpDur = Random.Range(0.85f, 1.1f);
            f.splashIn = f.splashOut = false;
            AddRipple(ToWorld(f.pos), 0.7f);
            return;
        }
    }

    void Splash(Vector2 localPos, float strength)
    {
        Vector3 w = ToWorld(localPos);
        AddRipple(w, strength);
        if (splash != null)
        {
            splash.transform.position = w;
            splash.Emit(Random.Range(7, 12));
        }
    }

    // ------------------------------------------------------------------ рыбалка
    void FishBites(FishState f, bool real)
    {
        f.attract = false;
        f.fleeT = 1.1f;
        PickTarget(f);
        if (!real || bobber == null) return;
        dip = 1f;
        AddRipple(bobber.position, 1f);
        if (OnBite != null) OnBite();
    }

    void UpdateFishing(float dt, float time)
    {
        if (bobber == null) return;

        biteTimer -= dt;
        if (biteTimer <= 0f)
        {
            bool busy = false;
            for (int i = 0; i < fs.Length; i++) if (fs[i].attract) { busy = true; break; }
            if (!busy)
            {
                FishState best = null;
                float bd = float.MaxValue;
                Vector2 bp = new Vector2(bobberBase.x, bobberBase.z);
                for (int i = 0; i < fs.Length; i++)
                {
                    if (fs[i].jumpT >= 0f) continue;
                    float d = (fs[i].pos - bp).sqrMagnitude;
                    if (d < bd) { bd = d; best = fs[i]; }
                }
                if (best != null) { best.attract = true; best.attractT = 0f; }
                biteTimer = Random.Range(biteInterval.x, biteInterval.y) + 2f;
            }
            else biteTimer = 1f;
        }

        dip = Mathf.MoveTowards(dip, 0f, dt * 0.8f);
        Vector3 p = bobberBase;
        p.y += Mathf.Sin(time * 2.2f) * 0.012f - dip * 0.14f * (0.55f + 0.45f * Mathf.Sin(time * 15f));
        bobber.localPosition = p;
    }

    // ------------------------------------------------------------------ кувшинки
    void UpdatePads(float time)
    {
        if (pads == null) return;
        for (int i = 0; i < pads.Length; i++)
        {
            if (pads[i] == null) continue;
            float ph = padPhase[i];
            Vector3 p = padBase[i];
            p.y += Mathf.Sin(time * 0.9f + ph) * 0.012f;
            pads[i].localPosition = p;
            pads[i].localRotation = Quaternion.Euler(Mathf.Sin(time * 0.7f + ph) * 2.2f, padYaw[i], Mathf.Cos(time * 0.6f + ph * 1.3f) * 2.2f);
        }
    }
}
