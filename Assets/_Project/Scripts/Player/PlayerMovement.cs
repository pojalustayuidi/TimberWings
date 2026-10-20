using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("Настройки")]
    public float speed = 4f;           // используется, только если useBalanceSpeed выключен
    public bool useBalanceSpeed = true;   // true = брать GameBalance.PlayerSpeed (GDD: 4 м/с)
    public float acceleration = 50f;   // разгон до полной скорости за ~0.1 с
    public float rotationSpeed = 15f;
    public float cameraYaw = 45f;      // как yaw в CameraFollow

    [Header("Анимация")]
    public float runAnimSpeed = 1f;    // подбери так, чтобы ноги не скользили по земле
    [Tooltip("Темп бега следует за реальной скоростью (прокачка, тяжёлый рюкзак): ноги не скользят")]
    public bool animFollowsSpeed = true;

    [Header("Рубка")]
    [Tooltip("Пока идёт рубка, персонаж стоит и смотрит на дерево. Иначе он бежит в анимации бега, а таймер рубки тикает")]
    public bool lockMovementWhileChopping = true;

    [Header("Ссылки")]
    public Transform model;
    public Animator animator;

    private Rigidbody rb;
    private BackpackTag backpack;
    private PlayerInteraction interaction;
    private Vector3 moveDirection;

    // ---- автоход к цели (клик по дереву / E издалека, зовёт PlayerInteraction) ----
    // Прямо к цели, но если впереди сплошной объект (ствол, камень), берём ближайший свободный угол. NavMesh в проекте нет.
    // Любой ручной ввод, разговор, рубка или «застрял на 1.5 с» прерывают ход.
    [Header("Автоход к цели")]
    [Tooltip("Дольше столько секунд к цели не идём (страховка от вечной ходьбы)")]
    public float autoGiveUpSeconds = 15f;

    // Управление отнято: падение с острова и возвращение (IslandEdgeGuard). Не сохраняется
    [System.NonSerialized] public bool ControlLocked;

    public bool AutoWalking { get; private set; }
    Transform autoTarget;
    float autoStop, autoStartedAt, autoCheckAt, autoSteerSign = 1f;
    int autoStuck;
    Vector3 autoLastPos;
    System.Action autoArrive;
    static readonly RaycastHit[] castBuf = new RaycastHit[10];
    static readonly float[] steerAngles = { 0f, 35f, -35f, 70f, -70f, 105f, -105f };

    // stopDistance: на каком расстоянии от центра цели остановиться; onArrive вызовется один раз, когда дошли
    public void WalkTo(Transform target, float stopDistance, System.Action onArrive)
    {
        if (target == null) { CancelWalk(); return; }
        autoTarget = target;
        autoStop = Mathf.Max(0.3f, stopDistance);
        autoArrive = onArrive;
        autoStartedAt = Time.time;
        autoCheckAt = Time.time + 0.5f;
        autoLastPos = transform.position;
        autoStuck = 0;
        AutoWalking = true;
    }

    public void CancelWalk()
    {
        AutoWalking = false;
        autoTarget = null;
        autoArrive = null;
    }

    // true = идём, dir = мировое направление. false = дошли / сдались (ход уже сброшен)
    bool StepAutoWalk(out Vector3 dir)
    {
        dir = Vector3.zero;
        Vector3 to = autoTarget.position - transform.position;
        to.y = 0f;
        float dist = to.magnitude;

        if (dist <= autoStop)
        {
            var cb = autoArrive;
            CancelWalk();
            if (cb != null) cb();
            return false;
        }
        if (Time.time - autoStartedAt > autoGiveUpSeconds) { CancelWalk(); return false; }

        // застрял: за 0.5 с сдвинулись меньше чем на 0.2 м, три раза подряд
        if (Time.time >= autoCheckAt)
        {
            autoCheckAt = Time.time + 0.5f;
            if ((transform.position - autoLastPos).sqrMagnitude < 0.04f)
            {
                if (++autoStuck >= 3)
                {
                    CancelWalk();
                    SaveIndicator.ShowToast("Не пройти к дереву");
                    return false;
                }
            }
            else autoStuck = 0;
            autoLastPos = transform.position;
        }

        dir = Steer(to / dist);
        return true;
    }

    Vector3 Steer(Vector3 want)
    {
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        for (int i = 0; i < steerAngles.Length; i++)
        {
            float a = steerAngles[i] * autoSteerSign;
            Vector3 d = Quaternion.Euler(0f, a, 0f) * want;
            if (PathBlocked(origin, d))
                continue;
            if (i > 0) autoSteerSign = Mathf.Sign(a);   // свернули в эту сторону: дальше держимся её, не мечемся
            return d;
        }
        return want;   // всё закрыто: идём прямо, «застрял» сдастся сам
    }

    bool PathBlocked(Vector3 origin, Vector3 dir)
    {
        int n = Physics.SphereCastNonAlloc(origin, 0.35f, dir, castBuf, 1.1f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var h = castBuf[i];
            var c = h.collider;
            if (c == null || h.distance <= 0.0001f) continue;                 // уже касаемся (земля под ногами)
            if (c.transform.IsChildOf(transform)) continue;                   // сам игрок
            if (autoTarget != null && c.transform.IsChildOf(autoTarget)) continue;   // само дерево-цель: к нему и идём
            if (c.attachedRigidbody != null) continue;                        // питомцы, рабочие: они сами уступают
            if (h.normal.y > 0.6f) continue;                                  // пологий склон, пройти можно
            return true;
        }
        return false;
    }

    // рывок (навык carry_dash): не сохраняется, при Load и новой игре сбрасывается (рывок готов)
    float dashUntil, dashReadyAt;
    Vector3 dashDir;

    public bool HasDash { get { return PlayerSkills.Has(SkillIds.CarryDash); } }
    public bool IsDashing { get { return HasDash && Time.time < dashUntil; } }
    public float DashCooldownLeft { get { return Mathf.Max(0f, dashReadyAt - Time.time); } }

    // Единая точка доступа к игроку: вместо FindFirstObjectByType<PlayerMovement>() в каждом скрипте
    public static PlayerMovement Instance { get { return SceneRef<PlayerMovement>.Get(); } }

    void Awake() { SceneRef<PlayerMovement>.Set(this); }

    void OnEnable()
    {
        PlayerSkills.Changed -= OnSkillsChanged; PlayerSkills.Changed += OnSkillsChanged;
        SaveSystem.Loaded -= ClearDash; SaveSystem.Loaded += ClearDash;
        SaveSystem.NewGameStarted -= ClearDash; SaveSystem.NewGameStarted += ClearDash;
    }

    void OnDisable()
    {
        PlayerSkills.Changed -= OnSkillsChanged;
        SaveSystem.Loaded -= ClearDash;
        SaveSystem.NewGameStarted -= ClearDash;
    }

    // навык сбросили: рывок и перезарядка пропадают сразу (смена уровня их не трогает)
    void OnSkillsChanged() { if (!HasDash) ClearDash(); }

    void ClearDash() { dashUntil = 0f; dashReadyAt = 0f; }

    void TryDash(bool isMoving)
    {
        float left = DashCooldownLeft;
        if (left > 0f)
        {
            SaveIndicator.ShowToast("Рывок восстанавливается: " + Mathf.CeilToInt(left) + " с");
            return;
        }

        Vector3 d = isMoving ? moveDirection : (model != null ? model.forward : transform.forward);
        d.y = 0f;
        if (d.sqrMagnitude < 0.01f) d = transform.forward;
        dashDir = d.normalized;
        dashUntil = Time.time + GameBalance.SkillDashTime;
        dashReadyAt = Time.time + GameBalance.SkillDashCooldown;   // перезарядка идёт с начала рывка
        SfxManager.Play("hint", 0.7f, 1.35f);
    }

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null) rb.interpolation = RigidbodyInterpolation.Interpolate;
        if (animator == null) animator = GetComponentInChildren<Animator>();
        backpack = GetComponent<BackpackTag>();
        interaction = GetComponent<PlayerInteraction>();
    }

    // GDD: базовая скорость × бонус прокачки × (1 − 25% × заполненность рюкзака)
    // «Лёгкая поступь» (carry_light): замедление 15% вместо 25%. «Быстрые ноги» (carry_legs) входят в SpeedMultiplier.
    // Рывок (carry_dash): ×2 на 0.4 с поверх всего остального.
    public float CurrentSpeed
    {
        get
        {
            float v = useBalanceSpeed ? GameBalance.PlayerSpeed : speed;
            var up = PlayerUpgrades.Instance;
            if (up != null) v *= up.SpeedMultiplier;
            if (backpack != null && backpack.Capacity > 0)
            {
                float fill = Mathf.Clamp01((float)backpack.Carried / backpack.Capacity);
                float slow = PlayerSkills.Has(SkillIds.CarryLight) ? GameBalance.SkillCarrySlowdown : GameBalance.FullBackpackSlowdown;
                v *= 1f - slow * fill;
            }
            if (IsDashing) v *= GameBalance.SkillDashMul;
            return v;
        }
    }

    void Update()
    {
        float moveX = MobileInput.Horizontal;
        float moveZ = MobileInput.Vertical;

        Vector3 input = new Vector3(moveX, 0, moveZ).normalized;

        // разговор идёт: стоим на месте и смотрим на собеседника
        bool frozen = DialogueSequence.FreezesPlayer;
        if (frozen)
        {
            input = Vector3.zero;
            var focus = DialogueSequence.Focus;
            if (focus != null && model != null)
            {
                Vector3 d = focus.position - transform.position;
                d.y = 0f;
                if (d.sqrMagnitude > 0.01f)
                    model.rotation = Quaternion.Slerp(model.rotation, Quaternion.LookRotation(d), 1f - Mathf.Exp(-10f * Time.deltaTime));
            }
        }

        // управление отнято (падение с острова): ввода нет, автоход отменён
        if (ControlLocked)
        {
            input = Vector3.zero;
            if (AutoWalking) CancelWalk();
        }

        // рубка: стоим на месте и смотрим на дерево
        bool chopping = interaction != null && interaction.IsChopping;
        if (chopping && lockMovementWhileChopping)
        {
            input = Vector3.zero;
            Transform tree = interaction.CurrentTarget;
            if (tree != null && model != null)
            {
                Vector3 d = tree.position - transform.position;
                d.y = 0f;
                if (d.sqrMagnitude > 0.01f)
                    model.rotation = Quaternion.Slerp(model.rotation, Quaternion.LookRotation(d), 1f - Mathf.Exp(-12f * Time.deltaTime));
            }
        }
        // автоход к дереву: ручной ввод, разговор и рубка его прерывают
        if (AutoWalking)
        {
            if (frozen || chopping || input != Vector3.zero || autoTarget == null || !autoTarget.gameObject.activeInHierarchy)
                CancelWalk();
            else
            {
                Vector3 wd;
                if (StepAutoWalk(out wd))
                    input = Quaternion.Euler(0, -cameraYaw, 0) * wd;   // ниже input снова поворачивается на cameraYaw: получится wd
            }
        }

        bool isMoving = input != Vector3.zero;

        if (isMoving)
        {
            moveDirection = Quaternion.Euler(0, cameraYaw, 0) * input;

            if (model != null)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                model.rotation = Quaternion.Slerp(model.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }
        else
        {
            moveDirection = Vector3.zero;
        }

        // рывок: не во время разговора и рубки
        if (!frozen && !chopping && !ControlLocked && HasDash && !SaveSystem.IsLoading && MobileInput.DashDown)
            TryDash(isMoving);

        if (animator != null)
        {
            animator.SetBool("IsMoving", isMoving);

            float s = 1f;
            if (isMoving)
            {
                s = runAnimSpeed;
                if (animFollowsSpeed)
                {
                    float baseSpeed = useBalanceSpeed ? GameBalance.PlayerSpeed : speed;
                    if (baseSpeed > 0.01f) s *= Mathf.Clamp(CurrentSpeed / baseSpeed, 0.6f, 1.6f);
                }
            }
            else if (chopping) s = interaction.chopAnimSpeed;   // animator.speed общий для всех клипов, пишем его только здесь
            animator.speed = s;
        }
    }

    void FixedUpdate()
    {
        if (rb == null) return;
        if (ControlLocked) return;   // падение: скорость не трогаем, летим по инерции и гравитации (или двигает IslandEdgeGuard)

        Vector3 v = rb.linearVelocity;
        Vector3 horizontal = new Vector3(v.x, 0f, v.z);
        bool dashing = IsDashing;
        Vector3 dir = moveDirection;
        if (dashing && dir == Vector3.zero) dir = dashDir;   // рывок с места: вперёд, куда смотрит персонаж
        Vector3 targetVel = dir * CurrentSpeed;

        // плавный разгон и торможение вместо мгновенного старта (в рывке разгон резкий)
        horizontal = Vector3.MoveTowards(horizontal, targetVel, acceleration * (dashing ? 4f : 1f) * Time.fixedDeltaTime);

        rb.linearVelocity = new Vector3(horizontal.x, v.y, horizontal.z);
    }
}