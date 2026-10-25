// Assets/_Project/Scripts/Build/Hut/HutSawyer.cs   (НЕ в папке Editor!)
// Рабочий-пильщик в хижине лесоруба (стадия 3). Модель собирает генератор (Tools > BuilderSim > Хижина лесоруба v2), тут только жизнь.
//
//   • Заказа на доски нет: стоит у козел и пилит бревно ручной пилой (летят опилки). Раз в ~14 секунд пауза на чай из кружки.
//     Если игрок подошёл ближе greetRadius, рабочий поворачивает голову и машет рукой.
//   • Заказ пошёл (SawmillAnimator.IsWorking): идёт к пилораме, встаёт у каретки и ведёт её рукоятку вместе с ней:
//     толкает на резе, тянет на возврате (шаги в такт скорости каретки).
//   • Готова доска (SawmillAnimator.PlankDone): она летит дугой на склад готовых досок, стук и облачко.
//   • Заказ кончился: дожидается, пока каретка встанет, и возвращается к козлам.
//
// Всё считается от позы родителя (Sawmill), поэтому поворот и масштаб хижины ему не мешают.
// Пока стадия 3 выключена, ничего не считается. Рабочий без аниматора: суставы крутятся только кодом, Animator не нужен.
// «Уменьшить эффекты»: опилки втрое реже.
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("BuilderSim/Hut Sawyer")]
public class HutSawyer : MonoBehaviour
{
    public enum Mode { Rest, GoToWork, Work, GoRest }

    [Header("Пилорама (если пусто, ищется в родителях)")]
    public SawmillAnimator saw;

    [Header("Суставы (заполняет генератор)")]
    public Transform hips;
    public Transform head;
    public Transform shoulderL, elbowL, shoulderR, elbowR;
    public Transform hipL, kneeL, hipR, kneeR;

    [Header("Реквизит")]
    [Tooltip("Ручная пила в правой руке (видна только у козел)")]
    public GameObject handSaw;
    [Tooltip("Пила, воткнутая в бревно (видна, пока рабочий ушёл)")]
    public GameObject logSaw;
    [Tooltip("Кружка в левой руке (во время чая)")]
    public GameObject mug;
    [Tooltip("Доска, которая летит на склад")]
    public Transform tossPlank;
    [Tooltip("Точка реза на козлах: отсюда летят опилки")]
    public Transform cutPoint;

    [Header("Места (в координатах родителя)")]
    public Vector3 restPos = new Vector3(2.9f, 0.05f, 0.15f);
    public float restYaw = 0f;
    [Tooltip("Z, по которому рабочий идёт за кареткой")]
    public float laneZ = 1.0f;
    public float followX = 0f;
    public float groundY = 0.05f;
    public float deckY = 0.13f;
    [Tooltip("С какого X начинается настил (там рабочий поднимается на него)")]
    public float deckX0 = 3.8f;

    [Header("Ходьба и реакции")]
    public float walkSpeed = 2.0f;
    public float turnSpeed = 420f;
    public float greetRadius = 4.5f;
    public float teaPeriod = 14f;
    public Color sawdustColor = new Color(0.97f, 0.84f, 0.58f, 0.95f);

    // ---- состояние
    Mode mode = Mode.Rest;
    float yaw, phase, vel, clock, restClock, teaK, greetT, greetK, nearK, throwK;
    bool wasNear, inited, subscribed;
    Vector3 hipsBase;
    bool propHand = true, propLog, propMug;
    ParticleSystem dust;
    Transform playerT;
    float nextPlayerFind;

    // плавные суставы
    const int HipsPitch = 0, HipsY = 1, HeadYaw = 2, HeadPitch = 3, ShR = 4, ElR = 5, ShL = 6, ElL = 7, LegLA = 8, KneeLA = 9, LegRA = 10, KneeRA = 11, N = 12;
    readonly float[] cur = new float[N];
    readonly float[] tgt = new float[N];

    // летящая доска
    float tossT = 1f;
    Vector3 tossFrom, tossTo;
    Quaternion tossToRot = Quaternion.identity;

    public Mode CurrentMode { get { return mode; } }

    void Awake() { Init(); }

    void Init()
    {
        if (inited) return;
        inited = true;
        if (saw == null) saw = GetComponentInParent<SawmillAnimator>(true);
        if (hips != null) hipsBase = hips.localPosition;
        MakeDust();
    }

    void OnEnable()
    {
        Init();
        if (saw != null && !subscribed) { saw.PlankDone += OnPlankDone; subscribed = true; }
        Snap();
    }

    void OnDisable()
    {
        if (saw != null && subscribed) saw.PlankDone -= OnPlankDone;
        subscribed = false;
        if (tossPlank != null) tossPlank.gameObject.SetActive(false);
        if (tossT < 1f && saw != null) saw.PlankLanded();
        tossT = 1f;
    }

    // стадию только что показали (или загрузили сохранение): сразу встаём в нужное место, без перехода
    void Snap()
    {
        bool want = saw != null && saw.IsWorking;
        mode = want ? Mode.Work : Mode.Rest;
        Vector3 p = want ? new Vector3(CarX() + followX, 0f, laneZ) : restPos;
        p.y = GroundAt(p.x);
        transform.localPosition = p;
        yaw = want ? 180f : restYaw;
        transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        restClock = Random.Range(0f, teaPeriod * 0.5f);
        for (int i = 0; i < N; i++) tgt[i] = cur[i] = 0f;
        propHand = propLog = propMug = !want;   // принудительно обновить реквизит в первом кадре
        propHand = !want; propLog = want;
        SetProps(true);
    }

    float CarX()
    {
        if (saw == null) return restPos.x;
        return saw.carriage != null ? saw.carriage.localPosition.x : saw.carriageStart.x;
    }

    float GroundAt(float x)
    {
        return Mathf.Lerp(groundY, deckY, Mathf.InverseLerp(deckX0 - 0.15f, deckX0, x));
    }

    // ---------------------------------------------------------------- кадр
    void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        if (dt <= 0f || saw == null || hips == null) return;
        clock += dt;
        bool want = saw.IsWorking;

        Vector3 p = transform.localPosition;
        Vector3 prev = p;
        float desiredYaw = yaw;

        // ---- перемещение
        switch (mode)
        {
            case Mode.Rest:
                desiredYaw = restYaw;
                if (want) mode = Mode.GoToWork;
                break;

            case Mode.GoToWork:
            {
                if (!want) { mode = Mode.GoRest; break; }
                Vector3 tp = new Vector3(CarX() + followX, 0f, laneZ);
                Vector3 d = new Vector3(tp.x - p.x, 0f, tp.z - p.z);
                float dist = d.magnitude;
                float step = walkSpeed * dt * (dist > 2.5f ? 1.5f : 1f);
                if (dist <= Mathf.Max(step, 0.06f)) { p.x = tp.x; p.z = tp.z; mode = Mode.Work; desiredYaw = 180f; }
                else
                {
                    p.x += d.x / dist * step;
                    p.z += d.z / dist * step;
                    desiredYaw = dist < 0.6f ? 180f : Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                }
                break;
            }

            case Mode.Work:
                desiredYaw = 180f;
                p.x = Mathf.Lerp(p.x, CarX() + followX, 1f - Mathf.Exp(-22f * dt));
                p.z = Mathf.MoveTowards(p.z, laneZ, dt);
                if (!want && saw.CarriageK < 0.03f) mode = Mode.GoRest;
                break;

            case Mode.GoRest:
            {
                if (want) { mode = Mode.GoToWork; break; }
                Vector3 d = new Vector3(restPos.x - p.x, 0f, restPos.z - p.z);
                float dist = d.magnitude;
                float step = walkSpeed * dt;
                if (dist <= Mathf.Max(step, 0.05f)) { p.x = restPos.x; p.z = restPos.z; mode = Mode.Rest; desiredYaw = restYaw; }
                else
                {
                    p.x += d.x / dist * step;
                    p.z += d.z / dist * step;
                    desiredYaw = dist < 0.45f ? restYaw : Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                }
                break;
            }
        }
        p.y = GroundAt(p.x);
        transform.localPosition = p;

        float speed = new Vector2(p.x - prev.x, p.z - prev.z).magnitude / dt;
        vel = Mathf.Lerp(vel, speed, 1f - Mathf.Exp(-12f * dt));
        yaw = Mathf.MoveTowardsAngle(yaw, desiredYaw, turnSpeed * dt);
        transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

        // ---- шаги
        phase += vel * 6.0f * dt;
        float walk = Mathf.Clamp01(vel / 0.9f);
        float amp = Mathf.Clamp(vel / 1.4f, 0.6f, 1.5f);
        float s = Mathf.Sin(phase);

        for (int i = 0; i < N; i++) tgt[i] = 0f;
        // у каретки рабочий скользит боком: шаг ногами вперёд-назад там выглядел бы как «лунная походка», поэтому слабее
        if (mode == Mode.Work) { walk *= 0.3f; }
        tgt[LegLA] = s * 36f * walk * amp;
        tgt[LegRA] = -tgt[LegLA];
        tgt[KneeLA] = 4f + Mathf.Max(0f, Mathf.Sin(phase + 1.5f)) * 42f * walk;
        tgt[KneeRA] = 4f + Mathf.Max(0f, Mathf.Sin(phase + 1.5f + Mathf.PI)) * 42f * walk;
        tgt[HipsY] = -Mathf.Abs(Mathf.Cos(phase)) * 0.03f * walk;

        tgt[ShR] = -3f; tgt[ElR] = -8f; tgt[ShL] = -3f; tgt[ElL] = -8f;

        if (mode == Mode.GoToWork || mode == Mode.GoRest)
        {
            tgt[ShR] = -s * 30f * walk; tgt[ShL] = s * 30f * walk;
            tgt[ElR] = tgt[ElL] = -18f;
            tgt[HipsPitch] = 4f;
        }
        else if (mode == Mode.Work)
        {
            int ph = saw.CarriagePhase;
            bool pushing = ph == 0, pulling = ph == 2;
            tgt[HipsPitch] = pushing ? 13f : (pulling ? -7f : 5f);
            tgt[ShR] = tgt[ShL] = pushing ? -80f : (pulling ? -72f : -78f);   // руки на рукоятке каретки
            tgt[ElR] = tgt[ElL] = pushing ? -4f : (pulling ? -14f : -8f);
            tgt[HeadPitch] = pushing ? 8f : 0f;
            tgt[HipsY] += Mathf.Sin(clock * 9f) * 0.006f;                       // дыхание
        }
        else
        {
            RestPose(dt);
        }

        // бросок готовой доски: правая рука взмахивает
        throwK = Mathf.MoveTowards(throwK, 0f, dt / 0.45f);
        if (mode == Mode.Work) tgt[ShR] -= 38f * throwK;

        // ---- реквизит и опилки
        SetProps(false);
        TickToss(dt);

        // ---- применить позу (плавно)
        float kSlow = 1f - Mathf.Exp(-10f * dt), kFast = 1f - Mathf.Exp(-22f * dt);
        for (int i = 0; i < N; i++)
        {
            bool leg = i == LegLA || i == KneeLA || i == LegRA || i == KneeRA;
            cur[i] = Mathf.Lerp(cur[i], tgt[i], leg ? kFast : kSlow);
        }
        hips.localPosition = hipsBase + new Vector3(0f, cur[HipsY], 0f);
        hips.localRotation = Quaternion.Euler(cur[HipsPitch], 0f, 0f);
        if (head != null) head.localRotation = Quaternion.Euler(cur[HeadPitch], cur[HeadYaw], 0f);
        if (shoulderR != null) shoulderR.localRotation = Quaternion.Euler(cur[ShR], 0f, 5f);
        if (elbowR != null) elbowR.localRotation = Quaternion.Euler(cur[ElR], 0f, 0f);
        if (shoulderL != null) shoulderL.localRotation = Quaternion.Euler(cur[ShL], 0f, -5f);
        if (elbowL != null) elbowL.localRotation = Quaternion.Euler(cur[ElL], 0f, 0f);
        if (hipL != null) hipL.localRotation = Quaternion.Euler(cur[LegLA], 0f, 0f);
        if (kneeL != null) kneeL.localRotation = Quaternion.Euler(cur[KneeLA], 0f, 0f);
        if (hipR != null) hipR.localRotation = Quaternion.Euler(cur[LegRA], 0f, 0f);
        if (kneeR != null) kneeR.localRotation = Quaternion.Euler(cur[KneeRA], 0f, 0f);
    }

    // ---------------------------------------------------------------- поза у козел: пилит, пьёт чай, машет игроку
    void RestPose(float dt)
    {
        restClock += dt;
        float c = restClock % Mathf.Max(6f, teaPeriod);
        float tea = Mathf.Clamp01(Mathf.Min((c - (teaPeriod - 4.5f)) / 0.7f, (teaPeriod - c) / 0.7f));
        teaK = Mathf.Lerp(teaK, Mathf.SmoothStep(0f, 1f, tea), 1f - Mathf.Exp(-10f * dt));

        // игрок рядом: поворачивает голову и машет (один раз при подходе)
        Transform pl = Player();
        bool near = false;
        float lookYaw = 0f;
        if (pl != null)
        {
            Vector3 d = pl.position - transform.position;
            d.y = 0f;
            near = d.sqrMagnitude < greetRadius * greetRadius;
            Vector3 loc = transform.InverseTransformPoint(pl.position);
            lookYaw = Mathf.Clamp(Mathf.Atan2(loc.x, loc.z) * Mathf.Rad2Deg, -70f, 70f);
        }
        if (near && !wasNear) greetT = 2.8f;
        wasNear = near;
        greetT = Mathf.Max(0f, greetT - dt);
        greetK = Mathf.MoveTowards(greetK, greetT > 0.01f ? 1f : 0f, dt / 0.25f);
        nearK = Mathf.MoveTowards(nearK, near ? 1f : 0f, dt / 0.5f);

        float sawing = (1f - teaK) * (1f - greetK);
        float stroke = Mathf.Sin(restClock * 5.4f) * sawing;

        // правая рука с пилой: ход вперёд и назад
        tgt[ShR] = -55f - 16f * stroke;
        tgt[ElR] = -35f + 25f * stroke;
        tgt[HipsPitch] = 9f + 6f * stroke;

        // левая рука: на бревне, чай или приветствие
        float shL = -50f, elL = -42f;
        shL = Mathf.Lerp(shL, -42f, teaK);
        elL = Mathf.Lerp(elL, -128f, teaK);
        float wave = greetK * (1f - teaK);
        shL = Mathf.Lerp(shL, -158f, wave);
        elL = Mathf.Lerp(elL, -20f + Mathf.Sin(clock * 10f) * 26f, wave);
        tgt[ShL] = shL;
        tgt[ElL] = elL;

        // голова: смотрит на бревно, при чае запрокидывает, рядом с игроком поворачивается к нему
        float idleLook = Mathf.Sin(clock * 0.7f) * 14f;
        tgt[HeadYaw] = Mathf.Lerp(idleLook * (1f - sawing), lookYaw, nearK);
        tgt[HeadPitch] = 12f * sawing - 14f * teaK;

        // опилки: пока пила идёт вперёд
        if (dust != null)
        {
            var em = dust.emission;
            em.rateOverTime = (sawing > 0.6f && stroke > 0.25f) ? (VibeSettings.ReduceEffects ? 6f : 18f) : 0f;
        }
    }

    Transform Player()
    {
        if (playerT == null && Time.unscaledTime >= nextPlayerFind)
        {
            nextPlayerFind = Time.unscaledTime + 2f;
            var pm = PlayerMovement.Instance;
            if (pm != null) playerT = pm.transform;
        }
        return playerT;
    }

    // ---------------------------------------------------------------- реквизит: что в руках в какой момент
    void SetProps(bool force)
    {
        bool atRest = mode == Mode.Rest;
        bool wantMug = atRest && teaK > 0.05f;
        if (force || atRest != propHand) { propHand = atRest; if (handSaw != null) handSaw.SetActive(atRest); }
        if (force || atRest == propLog) { propLog = !atRest; if (logSaw != null) logSaw.SetActive(!atRest); }
        if (force || wantMug != propMug) { propMug = wantMug; if (mug != null) mug.SetActive(wantMug); }
        if (!atRest && dust != null)
        {
            var em = dust.emission;
            em.rateOverTime = 0f;
        }
    }

    // ---------------------------------------------------------------- доска летит на склад
    void OnPlankDone()
    {
        if (tossPlank == null || !isActiveAndEnabled) return;
        if (tossT < 1f && saw != null) saw.PlankLanded();   // прошлая доска ещё летела: считаем её положенной, новая полетит в следующую ячейку
        Vector3 from = (saw != null && saw.carriage != null ? saw.carriage.position : transform.position) + Vector3.up * 0.55f;
        Vector3 to = from + transform.forward * 0.6f;
        tossToRot = transform.parent != null ? transform.parent.rotation : Quaternion.identity;
        if (saw != null && saw.readySlots != null && saw.readySlots.Length > 0)
        {
            int idx = Mathf.Clamp(saw.LandedCount, 0, saw.readySlots.Length - 1);
            var slot = saw.readySlots[idx];
            if (slot != null) { to = slot.transform.position; tossToRot = slot.transform.rotation; }
        }
        else if (saw != null && saw.readyStacks != null && saw.readyStacks.Length > 0)
        {
            int n = saw.readyStacks.Length;
            int r = saw.workshop != null ? saw.workshop.readyPlanks : 1;
            int cap = Mathf.Max(1, PlankWorkshop.OutputCap);
            int idx = Mathf.Clamp(Mathf.CeilToInt(r / (float)cap * n) - 1, 0, n - 1);
            if (saw.readyStacks[idx] != null) to = saw.readyStacks[idx].transform.position + Vector3.up * 0.3f;
        }
        tossFrom = from;
        tossTo = to;
        tossT = 0f;
        throwK = 1f;
        tossPlank.gameObject.SetActive(true);
        tossPlank.position = from;
    }

    void TickToss(float dt)
    {
        if (tossPlank == null || tossT >= 1f) return;
        tossT += dt / 0.65f;
        if (tossT >= 1f)
        {
            tossT = 1f;
            tossPlank.gameObject.SetActive(false);
            if (saw != null) saw.PlankLanded();   // доска легла на склад: теперь она видна там
            if (ImpactFx.Instance != null && !VibeSettings.ReduceEffects) ImpactFx.Instance.HammerTick(tossTo);
            SfxManager.Play("thock", 0.28f, Random.Range(0.95f, 1.15f));
            return;
        }
        float k = tossT;
        Vector3 pos = Vector3.Lerp(tossFrom, tossTo, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 1.1f);
        tossPlank.position = pos;
        // кувыркается в полёте и перед посадкой ложится ровно, как лежат доски в стопке
        Quaternion baseRot = transform.parent != null ? transform.parent.rotation : Quaternion.identity;
        Quaternion tumble = baseRot * Quaternion.Euler(0f, 0f, k * 540f);
        tossPlank.rotation = Quaternion.Slerp(tumble, tossToRot, Mathf.SmoothStep(0.65f, 1f, k));
    }

    // ---------------------------------------------------------------- опилки у козел (частицы кодом)
    void MakeDust()
    {
        if (cutPoint == null || dust != null) return;
        var go = new GameObject("HandSawdust");
        go.transform.SetParent(cutPoint, false);
        go.transform.localPosition = Vector3.zero;
        dust = go.AddComponent<ParticleSystem>();
        dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = dust.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
        main.startColor = new ParticleSystem.MinMaxGradient(sawdustColor, new Color(0.88f, 0.70f, 0.42f, 0.9f));
        main.gravityModifier = 0.8f;
        main.maxParticles = 40;

        var em = dust.emission;
        em.enabled = true;
        em.rateOverTime = 0f;

        var sh = dust.shape;
        sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle = 35f;
        sh.radius = 0.04f;
        sh.rotation = new Vector3(-90f, 0f, 0f);

        var col = dust.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        rend.sharedMaterial = VibeGfx.ParticleMat(VibeGfx.Soft);
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
        dust.Play();
    }

    void OnDestroy()
    {
        if (dust != null && dust.GetComponent<ParticleSystemRenderer>() != null)
        {
            var m = dust.GetComponent<ParticleSystemRenderer>().sharedMaterial;
            if (m != null) Destroy(m);
        }
    }
}
