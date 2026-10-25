// Assets/_Project/Scripts/Build/Hut/SawmillAnimator.cs   (НЕ в папке Editor!)
// Живая пилорама. Читает PlankWorkshop (очередь и готовые доски) и показывает работу «по-настоящему»:
//   • идёт заказ (queuedPlanks > 0): диск пилы раскручивается, водяное колесо крутится быстрее, каретка с брёвном ходит
//     «рез вперёд, пауза, назад», в точке реза летят опилки, при каждой готовой доске стук и облачко;
//   • заказа нет: пила плавно останавливается, каретка стоит в начале, колесо лениво плывёт (вода течёт всегда);
//   • сырые брёвна у начала рельсов: их столько, сколько досок в работе (до queueLogs.Length);
//   • готовые доски: стопки на складе вырастают по числу readyPlanks (readyStacks.Length ступеней), забрал, стопки исчезают.
// Пока хижина не достроена (PlankWorkshop.unlocked = false) пила стоит: это Stage2 со скелетом, а Stage3 с рабочей пилой ещё не показан.
// Вешается на объект «Sawmill» в Stage3 (генератор делает сам). Все ссылки заполняет генератор.
// «Уменьшить эффекты»: опилок втрое меньше.
//
// Что нового (для рабочего HutSawyer): наружу торчат IsWorking, CarriageK (0..1 где каретка), CarriagePhase (0 рез, 1 пауза, 2 возврат, 3 пауза)
// и событие PlankDone (готова очередная доска). Возврат каретки замедлен до 2.2 с: рабочий идёт за ней шагом, а не бежит.
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("BuilderSim/Sawmill Animator")]
public class SawmillAnimator : MonoBehaviour
{
    [Header("Источник данных (если пусто, ищется в родителях)")]
    public PlankWorkshop workshop;

    [Header("Вращение (ось Z локальной системы детали)")]
    public Transform blade;
    public Transform wheel;
    public Transform[] pulleys = new Transform[0];
    public float bladeSpeed = 1100f;         // градусов в секунду на полном ходу
    public float wheelIdle = 22f;
    public float wheelWork = 95f;
    public float spinUp = 1.6f;              // «разгон» диска, 1/с (больше = быстрее набирает обороты)

    [Header("Каретка с брёвном")]
    public Transform carriage;
    public Vector3 carriageStart;            // локальные позиции каретки (начало и конец рельсов)
    public Vector3 carriageEnd;
    public float cutTime = 3.0f;             // сек на рез вперёд
    public float returnTime = 2.2f;          // сек на возврат (рабочий идёт рядом, поэтому не слишком быстро)
    public float pauseTime = 0.35f;

    [Header("Сырьё и готовые доски")]
    [Tooltip("Брёвна в очереди у начала рельсов: i-е видно, если досок в работе больше i")]
    public GameObject[] queueLogs = new GameObject[0];
    [Tooltip("Ступени склада: сколько стопок видно, зависит от доли заполнения склада готовых досок")]
    public GameObject[] readyStacks = new GameObject[0];
    [Tooltip("Отдельные доски склада: i-я видна, когда готовых досок больше i (лежит ровно столько, сколько готово). Если заполнено, readyStacks не используется")]
    public GameObject[] readySlots = new GameObject[0];

    [Header("Опилки")]
    public Transform sawdustPoint;
    public Color sawdustColor = new Color(0.97f, 0.84f, 0.58f, 0.95f);

    // ---- наружу (для рабочего и всего, что хочет «жить» вместе с пилорамой)
    /// <summary>Пилорама сейчас режет: хижина достроена и в очереди есть доски.</summary>
    public bool IsWorking { get { return Working; } }
    /// <summary>Где каретка: 0 = начало рельсов, 1 = конец.</summary>
    public float CarriageK { get { return carriageK; } }
    /// <summary>0 рез вперёд, 1 пауза в конце, 2 возврат, 3 пауза в начале.</summary>
    public int CarriagePhase { get { return phase; } }
    /// <summary>Готова очередная доска (вызывается при росте readyPlanks).</summary>
    public event System.Action PlankDone;
    /// <summary>Сколько досок уже лежит на складе (последнюю рабочий ещё может нести: она появится, когда он её положит).</summary>
    public int LandedCount { get { return landed; } }
    /// <summary>Рабочий положил доску на склад: она становится видимой.</summary>
    public void PlankLanded()
    {
        if (readySlots.Length == 0) return;
        landed = Mathf.Min(landed + 1, Mathf.Max(0, lastReady));
        ShowSlots();
    }

    // ---- служебное
    float bladeVel;
    float wheelAngle, bladeAngle;
    Quaternion bladeBase, wheelBase;
    Quaternion[] pulleyBase;
    float phaseT;           // 0..1 по циклу каретки
    float carriageK;        // итоговое положение каретки 0..1
    int phase;              // 0 рез, 1 пауза в конце, 2 возврат, 3 пауза в начале
    float phaseClock;
    ParticleSystem dust;
    int lastReady, lastQueue;
    int landed;             // сколько досок склада показано
    float landWait;         // сколько ещё ждём рабочего, прежде чем показать доски сами
    bool inited;

    void Awake() { Init(); }

    void Init()
    {
        if (inited) return;
        inited = true;
        if (workshop == null) workshop = GetComponentInParent<PlankWorkshop>(true);
        if (blade != null) bladeBase = blade.localRotation;
        if (wheel != null) wheelBase = wheel.localRotation;
        pulleyBase = new Quaternion[pulleys != null ? pulleys.Length : 0];
        for (int i = 0; i < pulleyBase.Length; i++) pulleyBase[i] = pulleys[i] != null ? pulleys[i].localRotation : Quaternion.identity;
        MakeDust();
    }

    void OnEnable()
    {
        Init();
        if (workshop != null) workshop.Changed += Refresh;
        lastReady = lastQueue = -999;
        Refresh();
    }

    void OnDisable()
    {
        if (workshop != null) workshop.Changed -= Refresh;
    }

    // число досок в работе/готовых поменялось: обновляем сырьё и склад (без анимации: тут только видимость)
    void Refresh()
    {
        int q = workshop != null ? workshop.queuedPlanks : 0;
        int r = workshop != null ? workshop.readyPlanks : 0;

        if (q != lastQueue)
        {
            lastQueue = q;
            for (int i = 0; i < queueLogs.Length; i++)
                if (queueLogs[i] != null) queueLogs[i].SetActive(q > i);
        }
        if (r != lastReady)
        {
            bool rose = r > lastReady && lastReady >= 0;
            if (rose) OnPlankDone();
            lastReady = r;

            if (readySlots.Length > 0)
            {
                // доски склада: убрали лишние сразу; новую показываем, когда рабочий её положит (если рабочего нет, сразу)
                if (r < landed) landed = r;
                if (rose && PlankDone != null) landWait = 2.2f;
                else if (landWait <= 0f) landed = r;
                ShowSlots();
            }
            else
            {
                int cap = Mathf.Max(1, PlankWorkshop.OutputCap);
                int n = readyStacks.Length;
                int show = r <= 0 ? 0 : Mathf.Clamp(Mathf.CeilToInt(r / (float)cap * n), 1, n);
                for (int i = 0; i < n; i++)
                    if (readyStacks[i] != null) readyStacks[i].SetActive(i < show);
            }
        }
    }

    void ShowSlots()
    {
        for (int i = 0; i < readySlots.Length; i++)
            if (readySlots[i] != null) readySlots[i].SetActive(i < landed);
    }

    // готова очередная доска: сначала сообщаем рабочему (он бросит её на склад), потом стук и облачко у выхода
    void OnPlankDone()
    {
        if (PlankDone != null) PlankDone();
        if (sawdustPoint == null) return;
        if (ImpactFx.Instance != null && !VibeSettings.ReduceEffects) ImpactFx.Instance.HammerTick(sawdustPoint.position - Vector3.up * 0.9f);
        SfxManager.Play("thock", 0.3f, Random.Range(0.85f, 1.1f));
    }

    bool Working
    {
        get { return workshop != null && workshop.unlocked && workshop.queuedPlanks > 0; }
    }

    void Update()
    {
        float dt = Time.deltaTime;
        bool work = Working;

        if (landed < lastReady && readySlots.Length > 0)
        {
            landWait -= dt;
            if (landWait <= 0f) { landed = lastReady; ShowSlots(); }   // рабочий не успел: доски всё равно должны лежать
        }

        // --- обороты диска: плавный разгон и остановка
        float target = work ? bladeSpeed : 0f;
        bladeVel = Mathf.Lerp(bladeVel, target, 1f - Mathf.Exp(-spinUp * dt));
        if (bladeVel < 0.5f && !work) bladeVel = 0f;
        bladeAngle = (bladeAngle + bladeVel * dt) % 360f;
        if (blade != null) blade.localRotation = bladeBase * Quaternion.AngleAxis(bladeAngle, Vector3.forward);

        // --- колесо и шкивы: вода течёт всегда, при работе быстрее
        float ws = Mathf.Lerp(wheelIdle, wheelWork, Mathf.Clamp01(bladeVel / Mathf.Max(1f, bladeSpeed)));
        wheelAngle = (wheelAngle + ws * dt) % 360f;
        if (wheel != null) wheel.localRotation = wheelBase * Quaternion.AngleAxis(-wheelAngle, Vector3.forward);
        for (int i = 0; i < pulleyBase.Length; i++)
            if (pulleys[i] != null) pulleys[i].localRotation = pulleyBase[i] * Quaternion.AngleAxis(bladeAngle * 0.6f * (i % 2 == 0 ? 1f : -1f), Vector3.forward);

        // --- каретка
        TickCarriage(work, dt);

        // --- опилки: только пока каретка режет и диск разогнан
        bool cutting = work && phase == 0 && bladeVel > bladeSpeed * 0.6f;
        if (dust != null)
        {
            var em = dust.emission;
            em.rateOverTime = cutting ? (VibeSettings.ReduceEffects ? 9f : 26f) : 0f;
        }
    }

    void TickCarriage(bool work, float dt)
    {
        if (carriage == null) return;

        if (!work)
        {
            // заказа нет: каретка спокойно возвращается и стоит в начале
            phase = 3; phaseClock = 0f;
            phaseT = Mathf.MoveTowards(phaseT, 0f, dt / Mathf.Max(0.2f, returnTime));
            carriageK = Ease(phaseT);
            carriage.localPosition = Vector3.Lerp(carriageStart, carriageEnd, carriageK);
            return;
        }

        phaseClock += dt;
        switch (phase)
        {
            case 0:   // рез вперёд
                phaseT = Mathf.Clamp01(phaseClock / Mathf.Max(0.2f, cutTime));
                if (phaseT >= 1f) { phase = 1; phaseClock = 0f; }
                break;
            case 1:   // пауза в конце
                if (phaseClock >= pauseTime) { phase = 2; phaseClock = 0f; }
                break;
            case 2:   // возврат
                phaseT = 1f - Mathf.Clamp01(phaseClock / Mathf.Max(0.2f, returnTime));
                if (phaseT <= 0f) { phase = 3; phaseClock = 0f; }
                break;
            default:  // пауза в начале, потом новый рез
                if (phaseClock >= pauseTime) { phase = 0; phaseClock = 0f; }
                break;
        }
        // во время реза ход линейный (равномерная подача), на возврате с мягким торможением
        carriageK = phase == 0 ? phaseT : Ease(phaseT);
        carriage.localPosition = Vector3.Lerp(carriageStart, carriageEnd, carriageK);
    }

    static float Ease(float k) { return k * k * (3f - 2f * k); }

    // ---------------------------------------------------------------- опилки (частицы кодом, как у ImpactFx)
    void MakeDust()
    {
        if (sawdustPoint == null || dust != null) return;
        var go = new GameObject("Sawdust");
        go.transform.SetParent(sawdustPoint, false);
        go.transform.localPosition = Vector3.zero;
        dust = go.AddComponent<ParticleSystem>();
        dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = dust.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
        main.startColor = new ParticleSystem.MinMaxGradient(sawdustColor, new Color(0.88f, 0.70f, 0.42f, 0.9f));
        main.gravityModifier = 0.7f;
        main.maxParticles = 120;

        var em = dust.emission;
        em.enabled = true;
        em.rateOverTime = 0f;

        var sh = dust.shape;
        sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle = 28f;
        sh.radius = 0.06f;
        sh.rotation = new Vector3(-90f, 0f, 0f);   // струя вверх и чуть в стороны

        var col = dust.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var sz = dust.sizeOverLifetime;
        sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 1.3f)));

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
