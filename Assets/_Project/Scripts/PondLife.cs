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
    public bool useAuthoredFishDepth;
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
        public float heading, speed, turn, retarget, bobPhase, roll, turnRate, fleeT, depth;
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
        var swimmers = new System.Collections.Generic.List<FishState>();
        var mpb = new MaterialPropertyBlock();
        foreach (Transform swimmer in fish ?? System.Array.Empty<Transform>())
        {
            if (swimmer == null || !swimmer.gameObject.activeSelf) continue;
            Vector3 lp = transform.InverseTransformPoint(swimmer.position);
            var f = new FishState
            {
                t = swimmer,
                pos = ConstrainFish(new Vector2(lp.x, lp.z)),
                heading = (Quaternion.Inverse(transform.rotation) * swimmer.rotation).eulerAngles.y,
                bobPhase = Random.value * 6.2832f,
                depth = useAuthoredFishDepth ? Mathf.Clamp(waterY - lp.y, 0.12f, 0.42f) : fishDepth
            };
            PickTarget(f);
            swimmers.Add(f);

            var r = f.t.GetComponent<Renderer>();
            if (r != null)
            {
                r.GetPropertyBlock(mpb);
                mpb.SetFloat(PhaseId, Random.value * 6.2832f);
                mpb.SetFloat(WiggleId, Random.Range(6.5f, 10f));
                r.SetPropertyBlock(mpb);
            }
        }

        fs = swimmers.ToArray();

        // кувшинки
        int pc = pads != null ? pads.Length : 0;
        padBase = new Vector3[pc]; padPhase = new float[pc]; padYaw = new float[pc];
        for (int i = 0; i < pc; i++)
        {
            if (pads[i] == null) continue;
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
        var space = lineTr.parent;
        if (space != null)
        {
            a = space.InverseTransformPoint(a);
            b = space.InverseTransformPoint(b);
        }
        Vector3 d = b - a;
        float len = d.magnitude;
        if (len < 0.001f) return;
        lineTr.localPosition = (a + b) * 0.5f;
        lineTr.localRotation = Quaternion.LookRotation(d / len);
        lineTr.localScale = new Vector3(thickness, thickness, len);
    }

    // ------------------------------------------------------------------ контур пруда
    const float FishLimit = 0.76f;

    float ShoreAt(float theta) => WaterVolume.PondRadius(shoreR, theta);

    Vector2 ConstrainFish(Vector2 position)
    {
        float radius = ShoreAt(Mathf.Atan2(position.y, position.x)) * FishLimit;
        return Vector2.ClampMagnitude(position, radius);
    }

    Vector2 BobberInPond()
    {
        Vector3 local = transform.InverseTransformPoint(bobber.position);
        return new Vector2(local.x, local.z);
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
            em.rateOverTime = fireflyRate * g * g;
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
                    fs[i].pos = ConstrainFish(fs[i].pos + push);
                    fs[j].pos = ConstrainFish(fs[j].pos - push);
                }
            }

        for (int i = 0; i < fs.Length; i++) ApplyFish(fs[i], time);
    }

    void PickTarget(FishState f)
    {
        Vector2 p = RandomInPond(0.7f);
        for (int k = 0; k < 6 && (p - f.pos).sqrMagnitude <= 2f; k++)
        {
            p = RandomInPond(0.7f);
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
            f.pos = ConstrainFish(f.pos + new Vector2(Mathf.Sin(h), Mathf.Cos(h)) * (1.45f * dt));

            float u = Mathf.Clamp01(f.jumpT);
            float y = waterY - f.depth + (f.depth + jumpHeight) * Mathf.Sin(Mathf.PI * u);
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
            Vector2 bp = BobberInPond();
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
        if (Inside(np, FishLimit)) f.pos = np;
        else { f.target = RandomInPond(0.45f); f.retarget = Random.Range(2f, 4f); }
    }

    void ApplyFish(FishState f, float time)
    {
        if (f.jumpT >= 0f)
        {
            float u = Mathf.Clamp01(f.jumpT);
            float y = waterY - f.depth + (f.depth + jumpHeight) * Mathf.Sin(Mathf.PI * u);
            float pitch = -Mathf.Cos(Mathf.PI * u) * 55f;                // нос вверх при взлёте, вниз при падении
            PoseFish(f, y, pitch, 0f);
            return;
        }
        float bob = Mathf.Sin(time * 1.3f + f.bobPhase) * 0.03f;
        f.roll = Mathf.Lerp(f.roll, Mathf.Clamp(-f.turnRate * 0.12f, -25f, 25f), 0.15f);
        PoseFish(f, waterY - f.depth + bob, 0f, f.roll);
    }

    void PoseFish(FishState f, float y, float pitch, float roll)
    {
        f.t.SetPositionAndRotation(transform.TransformPoint(new Vector3(f.pos.x, y, f.pos.y)),
            transform.rotation * Quaternion.Euler(pitch, f.heading, roll));
    }

    void StartJump()
    {
        for (int tries = 0; tries < 6; tries++)
        {
            FishState f = fs[Random.Range(0, fs.Length)];
            if (f.jumpT >= 0f || f.attract) continue;
            float h = f.heading * Mathf.Deg2Rad;
            Vector2 ahead = f.pos + new Vector2(Mathf.Sin(h), Mathf.Cos(h)) * 1.6f;
            if (!Inside(ahead, FishLimit)) continue;                          // не прыгаем в берег
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
                Vector2 bp = BobberInPond();
                for (int i = 0; i < fs.Length; i++)
                {
                    if (fs[i].jumpT >= 0f) continue;
                    float d = (fs[i].pos - bp).sqrMagnitude;
                    if (d < bd) { bd = d; best = fs[i]; }
                }
                if (best != null && Inside(bp, FishLimit)) { best.attract = true; best.attractT = 0f; }
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
