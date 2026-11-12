// Assets/_Project/Scripts/Build/Hut/BuildWorkPose.cs   (НЕ в папке Editor!)
// Герой на стройке. Вызывает BuildingController (BeginBuilding / StopBuilding / CompleteStep):
//   BuildWorkPose.Hold(inside, стройка.transform, true, стройка);   // начали
//   BuildWorkPose.Hold(null, null, false);                          // закончили / отмена
// Компонент сам вешается на героя при первом вызове.
//
// Что делает:
//   1. ПОДХОД. Считает рабочее место (BuildWorkSpotFinder): стена здания и точка на ней. Герой сам подходит и встаёт у стены
//      (PlayerMovement.WalkTo). Пока он идёт, IsReady = false: таймер стройки ждёт, чтобы все удары попадали по зданию.
//   2. БЛОКИРОВКА. Пока этап идёт, уйти нельзя (MovementLocked читает MobileInput: ходьба и рывок молчат).
//      Страховки: блокировка снимается сама через BuildingController.lockSafetySeconds сверх времени этапа,
//      при остановке стройки и при выключении компонента.
//   3. ЦЕЛЬ. Лицом к точке на стене, а не к центру здания (TryGetFocus / TryGetHit). Молоток в LeoModelPresentation целится туда же.
//   4. УДАР. В кадр контакта молотка LeoModelPresentation зовёт NotifyStrike: уходит событие BuildStrikes.Struck
//      (эффекты, тряска, «отскок» здания), а StrikeCount растёт (BuildingController закрывает этап на ударе).
// Порядок выполнения 900: PlayerInteraction.StopChop сбрасывает isChopping каждый кадр, когда рядом нет дерева,
// поэтому мы пишем флаг ПОСЛЕ него, иначе анимация мигала бы.
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(900)]
public class BuildWorkPose : MonoBehaviour
{
    static BuildWorkPose inst;

    [Tooltip("Градусов в секунду: как быстро герой поворачивается к стройке")]
    public float turnSpeed = 420f;
    [Tooltip("Сколько секунд даём герою дойти до рабочего места. Не дошёл: стройка начнётся там, где он стоит")]
    public float approachTimeout = 1.65f;

    Animator anim;
    string param = "isChopping";
    bool hasParam, hasMoving;
    Transform focus;
    Rigidbody rb;
    PlayerInteraction interaction;
    PlayerMovement movement;
    bool on, leoActive;

    BuildingController site;
    BuildWorkSpot spot;
    bool hasSpot;
    Transform standAnchor, hitAnchor;
    bool approaching;
    float approachUntil;
    bool lockMove;
    float lockUntil;
    int strikes;
    float lastStrikeAt = -10f;
    // Tiny final-stage nod: only animated bones, never Rigidbody or player position.
    BuildingController lastSite;
    float lastEndedAt = -100f, nodStartedAt = -100f;

    // ---------------------------------------------------------------- что спрашивают другие скрипты
    public static bool TryGetFocus(PlayerMovement player, out Transform target)
    {
        target = null;
        if (inst == null || !inst.on || player == null || inst.gameObject != player.gameObject) return false;
        target = inst.focus;
        return target != null;
    }

    // точка на стене, куда бьёт молоток (мир). false = стройки нет или точка не посчитана
    public static bool TryGetHit(PlayerMovement player, out Vector3 point)
    {
        point = Vector3.zero;
        if (inst == null || !inst.on || !inst.hasSpot || player == null || inst.gameObject != player.gameObject) return false;
        point = inst.spot.hit;
        return true;
    }

    // рабочее место этой стройки (камера, эффекты)
    public static bool TryGetSpot(BuildingController b, out BuildWorkSpot s)
    {
        s = default(BuildWorkSpot);
        if (inst == null || !inst.on || !inst.hasSpot || inst.site != b) return false;
        s = inst.spot;
        return true;
    }

    // герой на месте (или стройки нет): можно запускать таймер
    public static bool IsReady { get { return inst == null || !inst.on || !inst.approaching; } }
    // уйти нельзя: ходьба и рывок молчат (MobileInput)
    public static bool MovementLocked { get { return inst != null && inst.on && inst.lockMove; } }
    // бьёт ли герой настоящим молотком (а не старая анимация без молотка)
    public static bool UsesHammer { get { return inst != null && inst.on && inst.leoActive; } }
    public static int StrikeCount { get { return inst != null ? inst.strikes : 0; } }
    public static float LastStrikeTime { get { return inst != null ? inst.lastStrikeAt : -10f; } }

    public static void Hold(Component player, Transform focus, bool on)
    {
        Hold(player, focus, on, null);
    }

    public static void Hold(Component player, Transform focus, bool on, BuildingController site)
    {
        if (on)
        {
            if (player == null) return;
            var pi = player.GetComponentInParent<PlayerInteraction>();
            GameObject go = pi != null ? pi.gameObject : player.gameObject;
            var b = go.GetComponent<BuildWorkPose>();
            if (b == null) b = go.AddComponent<BuildWorkPose>();
            // Если по ошибке одновременно заработали две площадки, освобождаем прошлую позу:
            // нельзя оставлять героя запертым у предыдущего объекта.
            if (inst != null && inst != b) inst.End();
            b.Begin(pi, focus, site);
            inst = b;
        }
        else if (inst != null)
        {
            inst.End();
        }
    }

    // молоток коснулся здания: face = мировая точка бойка. Зовёт LeoModelPresentation в кадр контакта
    public static void NotifyStrike(PlayerMovement player, Vector3 face)
    {
        if (inst == null || !inst.on || inst.site == null) return;
        if (player != null && inst.gameObject != player.gameObject) return;
        inst.Strike(face);
    }

    // Existing reveal events own timing; the nod is optional and never holds the reveal open.
    void OnEnable()
    {
        GameEvents.BuildRevealStarted += OnRevealStarted;
        GameEvents.BuildRevealFinished += OnRevealFinished;
    }

    void OnRevealStarted(BuildingController b)
    {
        if (b == null || b != lastSite || !b.RevealIsFinal ||
            Time.time - lastEndedAt > 0.4f || DialogueSequence.FreezesPlayer ||
            movement == null || movement.ControlLocked || VibeSettings.ReduceEffects) return;
        nodStartedAt = Time.time;
    }

    void OnRevealFinished(BuildingController b)
    {
        if (b != null && b == lastSite) nodStartedAt = -100f;
    }

    // ---------------------------------------------------------------- старт и конец
    void Begin(PlayerInteraction pi, Transform f, BuildingController s)
    {
        anim = (pi != null && pi.animator != null) ? pi.animator : GetComponentInChildren<Animator>();
        if (pi != null && !string.IsNullOrEmpty(pi.chopBool)) param = pi.chopBool;
        rb = GetComponent<Rigidbody>();
        movement = GetComponent<PlayerMovement>();
        interaction = pi;
        var presentation = GetComponentInChildren<LeoModelPresentation>();
        leoActive = presentation != null && presentation.isActiveAndEnabled;
        focus = f;
        site = s;
        lastSite = null;
        nodStartedAt = -100f;
        strikes = 0;
        lastStrikeAt = -10f;
        approaching = false;
        hasSpot = false;
        hasParam = false;
        hasMoving = false;
        if (anim != null && anim.runtimeAnimatorController != null)
        {
            foreach (var p in anim.parameters)
            {
                if (p.type != AnimatorControllerParameterType.Bool) continue;
                if (string.Equals(p.name, param, System.StringComparison.OrdinalIgnoreCase)) { param = p.name; hasParam = true; }
                if (p.name == "IsMoving") hasMoving = true;
            }
        }
        if (!hasParam && !leoActive) Debug.LogWarning("[BuildWorkPose] В аниматоре героя нет bool-параметра «" + param + "»: рабочая анимация не включится.", this);

        lockMove = s != null && s.lockPlayerWhileBuilding;
        lockUntil = Time.time + (s != null ? s.BuildTime + s.lockSafetySeconds : 0f) + approachTimeout;

        if (s != null)
        {
            Transform visual = (movement != null && movement.model != null) ? movement.model : transform;
            spot = BuildWorkSpotFinder.Compute(s, transform.position, visual.position.y);
            hasSpot = spot.valid;
        }
        if (hasSpot)
        {
            EnsureAnchors();
            hitAnchor.position = spot.hit;
            standAnchor.position = spot.stand;
            focus = hitAnchor;   // лицом к стене, а не к центру здания
            if (lockMove) StartApproach();   // идти к стене сам герой должен только когда он «заперт»; со свободным героем стройка бьёт откуда он стоит
        }
        on = true;
    }

    void End()
    {
        if (!on) return;
        if (approaching && movement != null && movement.AutoWalking) movement.CancelWalk();
        on = false;
        if (site != null) { lastSite = site; lastEndedAt = Time.time; }
        focus = null;
        site = null;
        approaching = false;
        lockMove = false;
        hasSpot = false;
        if (!leoActive && anim != null && hasParam) anim.SetBool(param, false);
    }

    void EnsureAnchors()
    {
        if (standAnchor == null) standAnchor = new GameObject("BuildStandSpot").transform;
        if (hitAnchor == null) hitAnchor = new GameObject("BuildHitPoint").transform;
    }

    // герой сам идёт к стене (с обходом препятствий и мягкой остановкой, как «автоход» к дереву)
    void StartApproach()
    {
        if (movement == null || !spot.approachAllowed) return;
        Vector3 d = spot.stand - transform.position;
        d.y = 0f;
        // Не пытаемся идти на 20-40 см, особенно если между Лео и целью стоят
        // камни костра / коллайдеры. Это давало почти 3 секунды топтания на месте.
        // На небольшой дистанции персонаж просто разворачивается к точке удара.
        Vector3 hitDelta = spot.hit - transform.position;
        hitDelta.y = 0f;
        if (hitDelta.sqrMagnitude < 1.35f * 1.35f) return;
        if (d.sqrMagnitude < 1.15f * 1.15f) return;
        if (d.sqrMagnitude > 2.7f * 2.7f) return;
        if (movement.ControlLocked) return;
        // Старт строительства отменяет предыдущую цель автохода (например, дерево).
        if (movement.AutoWalking) movement.CancelWalk();

        // Чуть раньше завершаем движение, чем коллайдер тела коснётся декора.
        movement.WalkTo(standAnchor, 0.58f, OnArrived, true);
        approaching = true;
        approachUntil = Time.time + Mathf.Clamp(approachTimeout, 0.9f, 1.9f);
    }

    void OnArrived() { approaching = false; }

    void Strike(Vector3 face)
    {
        // One logical hit per hammer contact; protects against duplicate animation callbacks.
        if (Time.time - lastStrikeAt < 0.13f) return;
        int index = strikes;
        strikes++;
        lastStrikeAt = Time.time;

        Vector3 n = hasSpot ? spot.normal : Vector3.up;
        Vector3 p = face;
        // молоток дошёл до стены: эффект садим на поверхность (чуть наружу, чтобы не утонул в модели)
        if (hasSpot && (face - spot.hit).sqrMagnitude < 0.7f * 0.7f) p = spot.hit + n * 0.03f;

        float progress = site != null ? site.Progress : 0f;
        float power = Mathf.Lerp(0.85f, 1.35f, progress);
        if (index % 3 == 2) power *= 1.2f;   // каждый третий удар тяжелее: ритм «тук-тук-БАМ»

        var info = new BuildStrikeInfo();
        info.site = site;
        info.point = p;
        info.normal = n;
        info.ground = hasSpot ? new Vector3(p.x, spot.groundY, p.z) : new Vector3(p.x, p.y - 0.9f, p.z);
        info.power = power;
        info.progress = progress;
        info.index = index;
        BuildStrikes.Raise(info);
    }

    // ---------------------------------------------------------------- кадр
    void Update()
    {
        if (!on) return;

        // страховка: блокировка не должна зависнуть, даже если стройка по какой-то причине не закончилась
        if (lockMove && Time.time > lockUntil) lockMove = false;

        // Приблизились к безопасной позиции — останавливаем автоход без дрожания ног.
        // Если путь заблокирован, не заставляем Лео три секунды упираться в объект.
        if (approaching)
        {
            Vector3 delta = standAnchor != null ? standAnchor.position - transform.position : Vector3.zero;
            delta.y = 0f;
            bool near = delta.sqrMagnitude < 0.62f * 0.62f;
            if (near || movement == null || !movement.AutoWalking || Time.time > approachUntil)
            {
                if (movement != null && movement.AutoWalking) movement.CancelWalk();
                approaching = false;
            }
        }

        if (leoActive && (interaction != null && interaction.IsChopping ||
            DialogueSequence.FreezesPlayer || movement != null && movement.ControlLocked)) return;
        if (!leoActive && anim != null && hasParam) anim.SetBool(param, true);

        // лицом к стройке, но только пока герой стоит (если пошёл, не крутим)
        if (focus == null || approaching) return;
        if (hasMoving && anim != null && anim.GetBool("IsMoving")) return;
        Vector3 d = focus.position - transform.position;
        d.y = 0f;
        if (d.sqrMagnitude < 0.01f) return;
        Quaternion want = Quaternion.LookRotation(d.normalized, Vector3.up);
        if (leoActive && movement != null && movement.model != null && movement.model != transform)
        {
            if (Mathf.Abs(MobileInput.Horizontal) > .03f || Mathf.Abs(MobileInput.Vertical) > .03f) return;
            Transform visual = movement.model;
            visual.rotation = Quaternion.RotateTowards(visual.rotation, want, turnSpeed * Time.deltaTime);
            return;
        }
        Quaternion q = Quaternion.RotateTowards(transform.rotation, want, turnSpeed * Time.deltaTime);
        if (rb != null && !rb.isKinematic) rb.MoveRotation(q); else transform.rotation = q;
    }

    // Order 900 means this runs AFTER LeoModelPresentation (250) resets/poses bones.
    // It is intentionally tiny and does not change the animator, physics or camera.
    void LateUpdate()
    {
        if (nodStartedAt < 0f || !BuildingController.AnyRevealing || VibeSettings.ReduceEffects ||
            movement == null || movement.ControlLocked || EndingSequence.Active ||
            DialogueManager.IsBusy || DialogueSequence.FreezesPlayer ||
            interaction != null && interaction.IsChopping) return;
        float elapsed = Time.time - nodStartedAt;
        if (elapsed > 2.1f) { nodStartedAt = -100f; return; }
        float k = Mathf.Clamp01((elapsed - .8f) / .95f);
        if (k <= 0f || k >= 1f) return;
        var leo = GetComponentInChildren<LeoModelPresentation>();
        if (leo == null || !leo.isActiveAndEnabled || leo.rig == null) return;
        float bow = Mathf.Sin(k * Mathf.PI);
        leo.rig.Rotate(leo.rig.head, new Vector3(7f * bow, 0f, 0f));
        leo.rig.Rotate(leo.rig.chest, new Vector3(2f * bow, 0f, 0f));
    }

    void OnDisable()
    {
        GameEvents.BuildRevealStarted -= OnRevealStarted;
        GameEvents.BuildRevealFinished -= OnRevealFinished;
        nodStartedAt = -100f;
        End();
    }

    void OnDestroy()
    {
        if (standAnchor != null) Destroy(standAnchor.gameObject);
        if (hitAnchor != null) Destroy(hitAnchor.gameObject);
        if (inst == this) inst = null;
    }
}
