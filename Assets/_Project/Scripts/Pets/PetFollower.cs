// Assets/_Project/Scripts/Pets/PetFollower.cs (НЕ в Editor)
// Питомец в мире: бежит за игроком и держится в своей точке рядом с героем.
// ВСЁ настраивается в инспекторе префаба (Assets/_Project/Prefabs/Pets/Pet_*.prefab):
//   slotOffsets  где стоит питомец в каждом из 3 слотов относительно героя (x вправо, y вперёд, метры)
//   heightOffset поднять над землёй (для летающих или сидящих на плече)
//   Idle Clip / Move Clip  какие клипы играть (можно перетащить любые из FBX)
//   дочерний объект Model  двигай и крути, чтобы поправить посадку модели и размер
// Чтобы видеть точки: выдели префаб (или его экземпляр на сцене), на сцене появятся жёлтые шарики слотов.
// Анимация идёт напрямую через Playables, контроллер не нужен. Если клипы не заданы, используется Animator Controller
// с параметром Speed (float), как раньше.
//
// Оживление (всё включается галочками, работает без дополнительных префабов, эффекты рисуются кодом):
//   облачко при появлении, мягкая тень под питомцем,
//   в покое иногда оглядывается или подпрыгивает,
//   на новый уровень подпрыгивает с искрами,
//   когда герой рубит дерево, питомцы подбегают к дереву, радуются каждому удару и сильнее, когда дерево упало.
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

public class PetFollower : MonoBehaviour
{
    [Header("Где стоит рядом с героем")]
    [Tooltip("По слотам 0, 1, 2, 3: x = вправо от героя, y = вперёд (минус = позади), в метрах")]
    public Vector2[] slotOffsets =
    {
        new Vector2(-1.1f, -1.8f),
        new Vector2(1.1f, -1.8f),
        new Vector2(0f, -3.4f),
        new Vector2(1.8f, -4.6f)
    };
    [Tooltip("Подъём над землёй (м)")]
    public float heightOffset = 0f;
    [Tooltip("Когда стоит, смотрит на героя")]
    public bool faceHeroWhenIdle = false;

    [Header("Бег")]
    [Tooltip("Обычная скорость (м/с). У игрока 4")]
    public float moveSpeed = 4.6f;
    [Tooltip("Скорость, когда питомец отстал")]
    public float catchUpSpeed = 9f;
    [Tooltip("Ближе этого расстояния до своей точки питомец стоит (м)")]
    public float stopRadius = 0.25f;
    public float turnSpeed = 12f;
    [Tooltip("Дальше этого расстояния питомец сразу переносится к герою (м)")]
    public float teleportDistance = 14f;

    [Header("Анимация")]
    [Tooltip("Пусто = ищется в детях")]
    public Animator animator;
    [Tooltip("Клип, когда стоит. Заполняет меню «Собрать префабы», можно заменить")]
    public AnimationClip idleClip;
    [Tooltip("Клип, когда бежит. Пусто = питомец только стоит в Idle")]
    public AnimationClip moveClip;
    [Tooltip("Множитель скорости клипа бега")]
    public float moveAnimSpeed = 1f;
    [Tooltip("Скорость клипа бега зависит от реальной скорости питомца")]
    public bool matchMoveAnimToSpeed = true;
    [Tooltip("При такой скорости клип бега идёт с обычной скоростью (м/с)")]
    public float referenceSpeed = 4.6f;
    [Tooltip("Время плавного перехода Idle <-> бег (с)")]
    public float blendTime = 0.15f;
    [Tooltip("Только для режима с Animator Controller: имя параметра скорости")]
    public string speedParam = "Speed";

    [Header("Эффекты")]
    [Tooltip("Облачко при появлении")]
    public bool spawnPuff = true;
    [Tooltip("Размер мягкой тени под питомцем (м). 0 = без тени")]
    public float shadowSize = 0.9f;
    [Range(0f, 1f)] public float shadowAlpha = 0.3f;

    [Header("Живость")]
    [Tooltip("В покое иногда оглядывается или подпрыгивает")]
    public bool idleFidgets = true;
    public float fidgetMinDelay = 4f;
    public float fidgetMaxDelay = 9f;
    [Tooltip("Высота обычного прыжка (м). Остальные прыжки считаются от неё")]
    public float hopHeight = 0.3f;
    [Tooltip("Подпрыгивает с искрами на новый уровень")]
    public bool hopOnLevelUp = true;

    [Header("Помощь на рубке")]
    [Tooltip("Когда герой рубит дерево, питомец подбегает к нему и радуется ударам")]
    public bool reactToChopping = true;
    [Tooltip("Как близко к дереву встаёт питомец (м)")]
    public float workDistance = 1.8f;
    [Tooltip("Подбегает, только если дерево не дальше этого от героя (м)")]
    public float workRadius = 7f;

    static readonly RaycastHit[] hits = new RaycastHit[8];
    static Material fxMat;

    Transform target;
    int slot;
    Vector3 baseScale = Vector3.one;
    float popT;
    float speedNow;
    float moveWeight;

    int speedHash;
    bool hasSpeedParam;

    PlayableGraph graph;
    AnimationMixerPlayable mixer;
    AnimationClipPlayable idlePlayable;
    AnimationClipPlayable movePlayable;
    bool usePlayables;

    // прыжки
    float hopH;
    float hopClock;
    float hopDelay;
    int hopsLeft;
    float hopY;
    const float HopDuration = 0.32f;

    // покой
    float idleHeading;
    float fidgetYaw;
    float fidgetHold;
    float fidgetTimer;

    // рубка
    PlayerInteraction chopper;
    float nextChopperFind;
    float lastWorkTime = -99f;

    // тень
    Transform shadow;
    float groundRefY;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { fxMat = null; }

    // вызывает PetWorld сразу после создания
    public void Init(Transform followTarget, int slotIndex)
    {
        target = followTarget;
        slot = slotIndex;
        baseScale = transform.localScale;
        transform.localScale = baseScale * 0.001f;
        popT = 0f;
        idleHeading = transform.eulerAngles.y;
        groundRefY = transform.position.y - heightOffset;

        if (spawnPuff)
            Burst(transform.position + Vector3.up * 0.25f * Mathf.Max(0.5f, baseScale.y),
                new Color(1f, 1f, 0.95f, 0.85f), 12, 0.32f * Mathf.Max(0.5f, baseScale.y), 1.1f, 0.15f, 0.25f);
    }

    void Awake()
    {
        // питомец не должен мешать игроку: колайдеры выключаем
        Collider[] cols = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++) cols[i].enabled = false;

        fidgetTimer = Random.Range(2f, 5f);
        CreateShadow();

        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator == null) return;

        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        if (idleClip != null)
        {
            SetupPlayables();
        }
        else if (animator.runtimeAnimatorController != null)
        {
            speedHash = Animator.StringToHash(speedParam);
            AnimatorControllerParameter[] ps = animator.parameters;
            for (int i = 0; i < ps.Length; i++)
                if (ps[i].nameHash == speedHash && ps[i].type == AnimatorControllerParameterType.Float) hasSpeedParam = true;
        }
    }

    void OnEnable()
    {
        GameEvents.LevelUp += OnLevelUp;
        GameEvents.ChopHit += OnChopHit;
        GameEvents.TreeChopped += OnTreeChopped;
    }

    void OnDisable()
    {
        GameEvents.LevelUp -= OnLevelUp;
        GameEvents.ChopHit -= OnChopHit;
        GameEvents.TreeChopped -= OnTreeChopped;
    }

    void SetupPlayables()
    {
        animator.runtimeAnimatorController = null;

        graph = PlayableGraph.Create("Pet_" + name);
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "PetAnim", animator);

        mixer = AnimationMixerPlayable.Create(graph, 2);
        idlePlayable = AnimationClipPlayable.Create(graph, idleClip);
        movePlayable = AnimationClipPlayable.Create(graph, moveClip != null ? moveClip : idleClip);
        graph.Connect(idlePlayable, 0, mixer, 0);
        graph.Connect(movePlayable, 0, mixer, 1);
        mixer.SetInputWeight(0, 1f);
        mixer.SetInputWeight(1, 0f);
        output.SetSourcePlayable(mixer);
        graph.Play();
        usePlayables = true;
    }

    void OnDestroy()
    {
        if (usePlayables && graph.IsValid()) graph.Destroy();
    }

    void Update()
    {
        if (target == null) return;

        float dt = Time.deltaTime;
        Vector3 pos = transform.position;
        pos.y -= hopY;                              // считаем движение без прыжка, прыжок добавим в конце

        bool working;
        Vector3 workLook;
        Vector3 goal = GoalPoint(out working, out workLook);
        if (working) lastWorkTime = Time.time;
        goal.y = pos.y;
        Vector3 to = goal - pos;
        float dist = to.magnitude;

        if (dist > teleportDistance)
        {
            pos = goal;
            dist = 0f;
        }

        float spd = 0f;
        if (dist > stopRadius && dt > 0f)
        {
            float want = Mathf.Lerp(moveSpeed, catchUpSpeed, Mathf.Clamp01((dist - 1.5f) / 4f));
            spd = Mathf.Min(want, dist / dt);
            Vector3 dir = to / dist;
            pos += dir * spd * dt;
            TurnTo(dir, dt);
            idleHeading = transform.eulerAngles.y;
            fidgetYaw = 0f;
            fidgetHold = 0f;
        }
        else
        {
            bool free = true;
            if (working) { LookAt(workLook, pos); free = false; }
            else if (faceHeroWhenIdle) { LookAt(target.position, pos); free = false; }
            TickFidget(dt, free);
            RotateToYaw(idleHeading + fidgetYaw, dt);
        }

        float groundY;
        if (GroundAt(pos, out groundY))
        {
            pos.y = Mathf.Lerp(pos.y, groundY + heightOffset, 1f - Mathf.Exp(-15f * dt));
            groundRefY = groundY;
        }
        else
        {
            groundRefY = pos.y - heightOffset;
        }

        hopY = TickHop(dt);
        pos.y += hopY;
        transform.position = pos;

        speedNow = Mathf.Lerp(speedNow, spd, 1f - Mathf.Exp(-12f * dt));
        UpdateAnimation(dt);
        UpdateShadow(pos);

        // появление: рост с лёгким перелётом
        if (popT < 1f)
        {
            popT = Mathf.Min(1f, popT + dt / 0.35f);
            float t = popT - 1f;
            float k = 1f + 2.70158f * t * t * t + 1.70158f * t * t;
            transform.localScale = baseScale * Mathf.Max(0.001f, k);
        }
    }

    // ---------------------------------------------------------------- цель движения

    // Обычно это своя точка рядом с героем. Пока герой рубит близкое дерево, это точка возле дерева.
    Vector3 GoalPoint(out bool working, out Vector3 lookAt)
    {
        working = false;
        lookAt = Vector3.zero;

        if (reactToChopping)
        {
            if (chopper == null && Time.unscaledTime >= nextChopperFind)
            {
                nextChopperFind = Time.unscaledTime + 1f;
                chopper = FindFirstObjectByType<PlayerInteraction>();
            }

            if (chopper != null && chopper.IsChopping && chopper.CurrentTarget != null && chopper.CurrentTarget.gameObject.activeInHierarchy)
            {
                Vector3 tp = chopper.CurrentTarget.position;
                Vector3 fromTree = target.position - tp;
                fromTree.y = 0f;
                if (fromTree.magnitude < workRadius)
                {
                    Vector3 dir = fromTree.sqrMagnitude > 0.01f ? fromTree.normalized : Vector3.back;
                    float ang = slot == 0 ? -55f : (slot == 1 ? 55f : (slot == 3 ? 100f : 0f));
                    float far = slot == 3 ? workDistance * 1.4f : (slot >= 2 ? workDistance * 1.7f : workDistance);
                    working = true;
                    lookAt = tp;
                    return tp + Quaternion.Euler(0f, ang, 0f) * dir * far;
                }
            }
        }

        return AnchorFor(target, slot, false);
    }

    Vector3 AnchorFor(Transform reference, int slotIndex, bool withHeight)
    {
        Vector3 fwd = reference.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
        fwd.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, fwd);

        Vector2 o = Vector2.zero;
        if (slotOffsets != null && slotOffsets.Length > 0)
        {
            int last = slotOffsets.Length - 1;
            o = slotOffsets[Mathf.Clamp(slotIndex, 0, last)];
            // в префабах, сохранённых до 4-го слота, массив из трёх: лишним слотам сдвигаем место, чтобы питомцы не слипались
            if (slotIndex > last) { o.x += 1.8f * (slotIndex - last); o.y -= 1.2f * (slotIndex - last); }
        }

        Vector3 p = reference.position + right * o.x + fwd * o.y;
        if (withHeight) p += Vector3.up * heightOffset;
        return p;
    }

    // ---------------------------------------------------------------- поворот и покой

    void TurnTo(Vector3 dir, float dt)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        Quaternion look = Quaternion.LookRotation(dir.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, 1f - Mathf.Exp(-turnSpeed * dt));
    }

    void RotateToYaw(float yaw, float dt)
    {
        Quaternion q = Quaternion.Euler(0f, yaw, 0f);
        transform.rotation = Quaternion.Slerp(transform.rotation, q, 1f - Mathf.Exp(-turnSpeed * dt));
    }

    void LookAt(Vector3 point, Vector3 from)
    {
        Vector3 d = point - from;
        d.y = 0f;
        if (d.sqrMagnitude < 0.01f) return;
        idleHeading = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        fidgetYaw = 0f;
        fidgetHold = 0f;
    }

    // Раз в несколько секунд в покое: оглядывается или один раз подпрыгивает
    void TickFidget(float dt, bool allowed)
    {
        if (!idleFidgets || !allowed || popT < 1f)
        {
            fidgetYaw = 0f;
            fidgetHold = 0f;
            return;
        }

        if (fidgetHold > 0f)
        {
            fidgetHold -= dt;
            if (fidgetHold <= 0f) fidgetYaw = 0f;
            return;
        }

        fidgetTimer -= dt;
        if (fidgetTimer > 0f) return;
        fidgetTimer = Random.Range(fidgetMinDelay, Mathf.Max(fidgetMinDelay, fidgetMaxDelay));

        if (Random.value < 0.6f)
        {
            fidgetYaw = Random.Range(40f, 80f) * (Random.value < 0.5f ? -1f : 1f);
            fidgetHold = Random.Range(0.9f, 1.6f);
        }
        else
        {
            StartHop(hopHeight * 0.6f, 1, 0f);
        }
    }

    // ---------------------------------------------------------------- прыжки и реакции

    void StartHop(float height, int count, float delay)
    {
        if (hopsLeft > 0 && height < hopH) return;      // слабый прыжок не перебивает сильный
        hopH = height;
        hopsLeft = count;
        hopClock = 0f;
        hopDelay = delay;
    }

    float TickHop(float dt)
    {
        if (hopsLeft <= 0) return 0f;
        if (hopDelay > 0f) { hopDelay -= dt; return 0f; }

        hopClock += dt;
        float u = hopClock / HopDuration;
        if (u >= 1f)
        {
            hopsLeft--;
            hopClock = 0f;
            return 0f;
        }
        return 4f * hopH * u * (1f - u);
    }

    void OnLevelUp(int level)
    {
        if (!hopOnLevelUp || popT < 1f) return;
        StartHop(hopHeight * 1.6f, 3, slot * 0.12f);
        Burst(transform.position + Vector3.up * 0.6f * Mathf.Max(0.5f, baseScale.y),
            new Color(1f, 0.82f, 0.25f, 1f), 14, 0.16f, 1.8f, 0.3f, 0.3f);
    }

    bool RecentlyWorking { get { return reactToChopping && Time.time - lastWorkTime < 0.8f; } }

    void OnChopHit()
    {
        if (!RecentlyWorking) return;
        StartHop(hopHeight * 0.5f, 1, slot * 0.07f);
    }

    void OnTreeChopped()
    {
        if (!RecentlyWorking) return;
        StartHop(hopHeight * 1.3f, 2, slot * 0.1f);
        Burst(transform.position + Vector3.up * 0.5f * Mathf.Max(0.5f, baseScale.y),
            new Color(1f, 0.9f, 0.4f, 1f), 8, 0.14f, 1.4f, 0.25f, 0.25f);
    }

    // ---------------------------------------------------------------- анимация

    void UpdateAnimation(float dt)
    {
        if (usePlayables)
        {
            float wantMove = (moveClip != null && speedNow > 0.15f) ? 1f : 0f;
            moveWeight = Mathf.MoveTowards(moveWeight, wantMove, dt / Mathf.Max(0.01f, blendTime));
            mixer.SetInputWeight(0, 1f - moveWeight);
            mixer.SetInputWeight(1, moveWeight);

            if (moveClip != null)
            {
                float k = moveAnimSpeed;
                if (matchMoveAnimToSpeed) k *= Mathf.Clamp(speedNow / Mathf.Max(0.1f, referenceSpeed), 0.5f, 2f);
                movePlayable.SetSpeed(k);
            }

            // если у клипа выключен Loop Time, закольцовываем сами
            LoopIfNeeded(idlePlayable, idleClip);
            if (moveClip != null) LoopIfNeeded(movePlayable, moveClip);
        }
        else if (animator != null && hasSpeedParam)
        {
            animator.SetFloat(speedHash, speedNow);
        }
    }

    static void LoopIfNeeded(AnimationClipPlayable p, AnimationClip c)
    {
        if (c == null || c.isLooping || c.length <= 0f) return;
        if (p.GetTime() >= c.length) p.SetTime(0.0);
    }

    // ---------------------------------------------------------------- тень

    void CreateShadow()
    {
        if (shadowSize <= 0f || shadowAlpha <= 0f) return;
        Sprite soft = VibeGfx.Soft;
        if (soft == null) return;

        GameObject go = new GameObject("Shadow");
        go.transform.SetParent(transform, false);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = soft;
        sr.color = new Color(0f, 0f, 0f, shadowAlpha);
        sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        sr.receiveShadows = false;
        shadow = go.transform;
    }

    // тень лежит на земле под питомцем (не прыгает вместе с ним) и сжимается в прыжке
    void UpdateShadow(Vector3 pos)
    {
        if (shadow == null) return;

        float hopK = hopH > 0.001f ? Mathf.Clamp01(hopY / (hopH * 1.2f)) : 0f;
        float worldSize = shadowSize * (1f - 0.35f * hopK);
        float parentScale = Mathf.Max(0.0001f, transform.lossyScale.x);

        shadow.position = new Vector3(pos.x, groundRefY + 0.03f, pos.z);
        shadow.rotation = Quaternion.Euler(90f, 0f, 0f);
        shadow.localScale = Vector3.one * (worldSize / 0.32f / parentScale);    // у спрайта Soft размер 0.32 м
    }

    // ---------------------------------------------------------------- искры и облачка

    // Разовый всплеск частиц кодом (без префабов). rise: насколько частицы всплывают вверх.
    static void Burst(Vector3 pos, Color color, int count, float size, float speed, float rise, float radius)
    {
        if (fxMat == null) fxMat = VibeGfx.ParticleMat(VibeGfx.Soft);
        if (fxMat == null) return;

        GameObject go = new GameObject("PetFx");
        go.transform.position = pos;
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.duration = 1f;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = count + 4;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
        main.startColor = color;
        main.gravityModifier = -rise;

        ParticleSystem.EmissionModule em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)count) });

        ParticleSystem.ShapeModule sh = ps.shape;
        sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius = radius;

        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        ParticleSystem.SizeOverLifetimeModule sz = ps.sizeOverLifetime;
        sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.6f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.2f)));

        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = fxMat;

        ps.Play();
        Destroy(go, 2f);
    }

    // ---------------------------------------------------------------- земля и гизмо

    // Земля под точкой. Луч идёт с высоты +1 м, поэтому на крыши и высокие объекты питомец не залезает.
    bool GroundAt(Vector3 p, out float y)
    {
        y = p.y;
        int n = Physics.RaycastNonAlloc(p + Vector3.up * 1f, Vector3.down, hits, 5f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        bool found = false;
        for (int i = 0; i < n; i++)
        {
            Transform h = hits[i].transform;
            if (h.IsChildOf(target) || h.IsChildOf(transform)) continue;
            if (hits[i].distance < best)
            {
                best = hits[i].distance;
                y = hits[i].point.y;
                found = true;
            }
        }
        return found;
    }

    // Точки слотов на сцене. В игре считаются от героя, в редакторе от самого префаба (как будто он герой).
    void OnDrawGizmosSelected()
    {
        Transform r = target != null ? target : transform;
        int n = slotOffsets != null ? slotOffsets.Length : 0;
        for (int i = 0; i < n; i++)
        {
            Vector3 p = AnchorFor(r, i, true);
            bool mine = target != null && i == slot;
            Gizmos.color = mine ? Color.green : Color.yellow;
            Gizmos.DrawWireSphere(p, 0.2f);
            Gizmos.DrawLine(r.position, p);
        }
    }
}
