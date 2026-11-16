using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("Настройки")]
    public float speed = 4f;           // используется, только если useBalanceSpeed выключен
    public bool useBalanceSpeed = true;   // true = брать GameBalance.PlayerSpeed (GDD: 4 м/с)
    public float acceleration = 50f;   // разгон до полной скорости за ~0.1 с
    public float rotationSpeed = 15f;
    [Header("Stage 4 — плавный разворот модели")]
    [Tooltip("Предел угловой скорости: быстрый ввод не делает резкий поворот на 180 градусов за один кадр")]
    [Range(120f, 900f)] public float maxTurnDegreesPerSecond = 460f;
    [Tooltip("Разворот к собеседнику и дереву, градусов/с; физику игрока не меняем")]
    [Range(120f, 900f)] public float actionTurnDegreesPerSecond = 340f;
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
    private Speaker speaker;
    private Vector3 moveDirection;
    float turnSign = 1f;

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
    bool brakeOnArrival, workApproach;
    float autoSpeedLimit;
    Vector3 autoLastPos;
    System.Action autoArrive;
    const float WorkApproachSpeed = 2.35f;
    static readonly RaycastHit[] castBuf = new RaycastHit[10];
    static readonly float[] steerAngles = { 0f, 35f, -35f, 70f, -70f, 105f, -105f };

    // stopDistance: на каком расстоянии от центра цели остановиться; onArrive вызовется один раз, когда дошли
    public void WalkTo(Transform target, float stopDistance, System.Action onArrive)
        => WalkTo(target, stopDistance, onArrive, false);

    internal void WalkTo(Transform target, float stopDistance, System.Action onArrive, bool softArrival)
        => BeginWalk(target, stopDistance, onArrive, softArrival, false);

    // The construction system already checked the short straight path and the standing footprint.
    internal void WalkToBuildSpot(Transform target) => BeginWalk(target, .02f, null, true, true);

    void BeginWalk(Transform target, float stopDistance, System.Action onArrive, bool softArrival, bool toWorkSpot)
    {
        if (target == null) { CancelWalk(); return; }
        autoTarget = target;
        autoStop = toWorkSpot ? stopDistance : Mathf.Max(0.3f, stopDistance);
        autoArrive = onArrive;
        brakeOnArrival = softArrival;
        workApproach = toWorkSpot;
        autoSpeedLimit = toWorkSpot ? WorkApproachSpeed : float.PositiveInfinity;
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
        workApproach = false;
    }

    internal bool IsWalkingTo(Transform target) => AutoWalking && autoTarget == target;

    internal void CancelWalkTo(Transform target)
    {
        if (!IsWalkingTo(target)) return;
        CancelWalk();
        moveDirection = Vector3.zero;
    }

    // true = идём, dir = мировое направление. false = дошли / сдались (ход уже сброшен)
    bool StepAutoWalk(out Vector3 dir)
    {
        dir = Vector3.zero;
        Vector3 to = autoTarget.position - transform.position;
        to.y = 0f;
        float dist = to.magnitude;

        float remaining = dist - autoStop;
        float arrivalSlack = workApproach ? .008f : brakeOnArrival ? .016f : 0f;
        if (remaining <= arrivalSlack)
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
                    bool showTreeHint = !workApproach;
                    CancelWalk();
                    if (showTreeHint) SaveIndicator.ShowToast("Не пройти к дереву");
                    return false;
                }
            }
            else autoStuck = 0;
            autoLastPos = transform.position;
        }

        if (brakeOnArrival)
        {
            autoSpeedLimit = Mathf.Max(workApproach ? .36f : .42f, Mathf.Sqrt(2f * 11f * remaining));
            if (workApproach) autoSpeedLimit = Mathf.Min(WorkApproachSpeed, autoSpeedLimit);
        }
        dir = workApproach ? to / dist : Steer(to / dist);
        return true;
    }

    Vector3 Steer(Vector3 want)
    {
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        if (!PathBlocked(origin, want)) return want;

        for (int i = 1; i < steerAngles.Length; i++)
        {
            float a = steerAngles[i] * autoSteerSign;
            Vector3 d = Quaternion.Euler(0f, a, 0f) * want;
            if (PathBlocked(origin, d))
                continue;
            autoSteerSign = Mathf.Sign(a);   // свернули в эту сторону: дальше держимся её, не мечемся
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
        CancelWalk(); // a disabled controller must not resume an obsolete automatic walk
        moveDirection = Vector3.zero;
        dashUntil = 0f;
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
        LeoPlayerVisual.Install(this);
        backpack = GetComponent<BackpackTag>();
        interaction = GetComponent<PlayerInteraction>();
        speaker = GetComponent<Speaker>();
        if (GetComponent<WaterSwim>() == null) gameObject.AddComponent<WaterSwim>();   // плавание и утопление в реке (без WaterVolume ничего не делает)
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
            v *= WaterSwim.SpeedMul;   // брод и плавание: медленнее
            return v;
        }
    }

    internal void FaceDirection(Vector3 direction, float maxDegrees)
    {
        direction.y = 0f;
        if (model == null || direction.sqrMagnitude < .0001f) return;

        Vector3 forward = model.forward;
        float yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        float targetYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        float angle = Mathf.DeltaAngle(yaw, targetYaw);
        // Stick to the same side when a target is almost directly behind Leo.
        if (Mathf.Abs(angle) >= 175f) angle = Mathf.Abs(angle) * turnSign;
        else if (Mathf.Abs(angle) > .5f) turnSign = Mathf.Sign(angle);

        float dt = Mathf.Min(Time.deltaTime, .06f);
        float smoothing = 1f - Mathf.Exp(-Mathf.Max(1f, rotationSpeed) * dt);
        float limit = Mathf.Max(120f, maxDegrees) * dt;
        float step = Mathf.Clamp(angle * smoothing, -limit, limit);
        model.rotation = Quaternion.AngleAxis(step, Vector3.up) * model.rotation;
    }

    // Choose one target after interaction and movement updates, before Leo samples his pose.
    void LateUpdate()
    {
        if (ControlLocked || SaveSystem.IsLoading || LeoCelebration.IsPlaying || EndingSequence.Active) return;

        if (StoryInspection.Active)
        {
            Vector3 walk = StoryInspection.WalkDirection;
            if (walk.sqrMagnitude > .0001f)
                FaceDirection(walk, maxTurnDegreesPerSecond);
            return;
        }

        if (DialogueSequence.FreezesPlayer)
        {
            if (TryFaceConversationPartner()) return;
            Transform focus = DialogueSequence.Focus;
            if (focus != null && !focus.IsChildOf(transform))
                FaceDirection(focus.position - transform.position, actionTurnDegreesPerSecond);
            return;
        }

        bool chopping = interaction != null && interaction.IsChopping;
        if (BuildWorkPose.TryGetFacing(this, out var surface, out float speed))
            FaceDirection(surface, speed);
        else if (chopping && lockMovementWhileChopping && interaction.CurrentTarget != null)
            FaceDirection(interaction.CurrentTarget.position - transform.position, actionTurnDegreesPerSecond);
        else if (moveDirection.sqrMagnitude > .0001f)
            FaceDirection(moveDirection, maxTurnDegreesPerSecond);
        else if (!IsDashing && !AutoWalking && !chopping)
            TryFaceConversationPartner();
    }

    bool TryFaceConversationPartner()
    {
        if (!DialogueSequence.TryGetFacingTarget(speaker, out var partner)) return false;
        Vector3 direction = partner.transform.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < .0001f) return false;
        FaceDirection(direction, actionTurnDegreesPerSecond);
        return true;
    }

    void Update()
    {
        float moveX = MobileInput.Horizontal;
        float moveZ = MobileInput.Vertical;

        Vector3 input = Vector3.ClampMagnitude(new Vector3(moveX, 0, moveZ), 1f);

        // разговор идёт: стоим на месте и смотрим на собеседника
        bool inspecting = StoryInspection.Active;
        bool frozen = DialogueSequence.FreezesPlayer || inspecting;
        bool modal = MobileInput.ModalOpen;
        bool swimming = WaterSwim.Swimming;
        bool celebrating = LeoCelebration.IsPlaying;
        if (frozen) input = Vector3.zero;

        // Stage 4.3: while Leo celebrates, accidental WASD/dash does not skip the pose.
        // ControlLocked is deliberately NOT used here: it belongs to fall/respawn safety.
        if (celebrating)
        {
            input = Vector3.zero;
            if (AutoWalking) CancelWalk();
        }

        // управление отнято (падение с острова): ввода нет, автоход отменён
        if (ControlLocked)
        {
            input = Vector3.zero;
            if (AutoWalking) CancelWalk();
        }

        // рубка: стоим на месте и смотрим на дерево
        bool chopping = interaction != null && interaction.IsChopping;
        if (chopping && lockMovementWhileChopping && !inspecting) input = Vector3.zero;
        // автоход к дереву: ручной ввод, разговор и рубка его прерывают
        if (AutoWalking)
        {
            if (frozen || modal || swimming || chopping || input != Vector3.zero ||
                autoTarget == null || !autoTarget.gameObject.activeInHierarchy)
                CancelWalk();
            else
            {
                Vector3 wd;
                if (StepAutoWalk(out wd))
                    input = Quaternion.Euler(0, -cameraYaw, 0) * wd;   // ниже input снова поворачивается на cameraYaw: получится wd
            }
        }

        if (inspecting && !ControlLocked && !celebrating)
            input = Quaternion.Euler(0f, -cameraYaw, 0f) * StoryInspection.WalkDirection;

        bool isMoving = input != Vector3.zero;

        moveDirection = isMoving ? Quaternion.Euler(0, cameraYaw, 0) * input : Vector3.zero;

        bool dashBlocked = frozen || modal || swimming || celebrating || chopping || ControlLocked || SaveSystem.IsLoading;
        if (dashBlocked) dashUntil = 0f;
        else if (HasDash && MobileInput.DashDown)
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
        if (LeoCelebration.IsPlaying)
        {
            // Freeze only horizontal motion. The jump is visual; gravity and ground collisions remain intact.
            rb.linearVelocity = new Vector3(0f, v.y, 0f);
            return;
        }
        if (StoryInspection.Active)
        {
            Vector3 walk = StoryInspection.WalkDirection;
            rb.linearVelocity = new Vector3(walk.x, v.y, walk.z);
            return;
        }
        Vector3 horizontal = new Vector3(v.x, 0f, v.z);
        bool dashing = IsDashing;
        Vector3 dir = moveDirection;
        if (dashing && dir == Vector3.zero) dir = dashDir;   // рывок с места: вперёд, куда смотрит персонаж
        float targetSpeed = CurrentSpeed;
        if (AutoWalking && brakeOnArrival && !dashing)
            targetSpeed = Mathf.Min(targetSpeed, autoSpeedLimit);
        Vector3 targetVel = dir * targetSpeed;

        // Release input and settle the stance a little faster than the normal take-off.
        float response = dashing ? 4f : dir == Vector3.zero ? 1.45f : 1f;
        horizontal = Vector3.MoveTowards(horizontal, targetVel, acceleration * response * Time.fixedDeltaTime);

        rb.linearVelocity = new Vector3(horizontal.x, v.y, horizontal.z);
    }
}
