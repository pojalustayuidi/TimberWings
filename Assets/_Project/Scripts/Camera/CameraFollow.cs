// Assets/_Project/Scripts/Camera/CameraFollow.cs (заменить старый)
// Изометрическая камера в «уютном» стиле:
//  - двухступенчатое сглаживание (мягкий старт и мягкая остановка, без рывков)
//  - взгляд вперёд зависит от скорости игрока (а не включается/выключается)
//  - лёгкий «разгон» поля зрения на бегу и еле заметное дыхание камеры
//  - зум колёсиком, приближение при рубке, вступительный наезд, тряска (CameraFollow.Shake)
//  - деревья, закрывающие игрока: мягкое «окно» в листве вокруг игрока (дизеринг в шейдере, см. TreeToonPeek.hlsl), деревья не пропадают целиком
//  - остальные преграды (стены, крыши): как раньше, перестают рисоваться (остаётся тень), но без мерцания (минимальное время скрытия)
// yaw должен совпадать с cameraYaw в PlayerMovement. Камера его сама не меняет.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// камера считается раньше остального, а метки в мире (BuildSiteTagView) позже: тогда метка берёт уже готовое положение камеры
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    public Transform target;   // если пусто, найдёт игрока сам

    [Header("Положение (изометрия)")]
    public float distance = 10.5f;
    public float pitch = 50f;
    public float yaw = 45f;        // должен совпадать с cameraYaw в PlayerMovement
    public Vector3 lookOffset = new Vector3(0f, 1f, 0f);

    [Header("Внешние смещения (CameraVibe и другие скрипты сами сюда пишут)")]
    public Vector3 focusBias = Vector3.zero;   // сдвиг точки, на которую смотрит камера
    public float zoomBias = 1f;                // множитель зума: < 1 ближе, > 1 дальше
    public float yawBias = 0f;                 // добавка к yaw в градусах (облёт вокруг здания при показе стадии, CameraVibe.SetReveal). Движение игрока не трогает

    [Header("Масштаб кадра")]
    public float fieldOfView = 35f;   // если камера Perspective
    public float orthoSize = 7f;      // если камера Orthographic

    [Header("Плавность (больше = мягче и медленнее)")]
    public float smoothTime = 0.38f;          // общее запаздывание камеры по земле
    public float verticalSmoothTime = 0.55f;  // по высоте ещё мягче (холмы, спуски)

    [Header("Взгляд вперёд")]
    public float lookAhead = 0.9f;            // метров вперёд на полной скорости
    public float lookAheadSmooth = 0.7f;      // чем больше, тем плавнее камера «подхватывает» направление
    public float fullSpeed = 5f;              // скорость игрока, при которой вынос максимальный

    [Header("Ощущение скорости")]
    public float speedFovBoost = 2.5f;        // на сколько градусов раскрывается FOV на бегу (Perspective)
    public float speedFovSmooth = 0.6f;

    [Header("Дыхание камеры")]
    public bool sway = true;
    public float swayAmount = 0.22f;          // градусы, очень лёгкое покачивание
    public float swaySpeed = 0.35f;

    [Header("Зум")]
    public bool allowScrollZoom = true;
    public float zoomMin = 0.7f;
    public float zoomMax = 1.3f;
    public float zoomStep = 0.08f;
    public float chopZoom = 0.9f;             // во время рубки камера чуть приближается (1 = выключить)

    [Header("Вступление")]
    public bool intro = true;
    public float introTime = 2.8f;
    public float introZoom = 1.35f;

    [Header("Прозрачность преград")]
    public bool hideBlockers = true;
    public float blockerRadius = 0.7f;
    public float blockerMinHeight = 1.5f;
    public float blockerStopShort = 2.5f;
    public float blockerRestoreDelay = 0.6f;
    [Tooltip("Скрытый объект не вернётся раньше этого времени: убирает мерцание, когда игрок идёт вдоль края преграды")]
    public float blockerMinHideTime = 0.8f;
    public LayerMask blockerMask = ~0;

    [Header("Окно в листве (деревья)")]
    [Tooltip("Деревья не прячутся целиком: вокруг игрока в их кроне и стволе мягко появляется дырка (дизеринг). Нужен патч шейдера TreeToon (TreeToonPeek.hlsl). Нет патча: сработает старое скрытие")]
    public bool peekHole = true;
    [Tooltip("Радиус окна в метрах = blockerRadius × это число (в лесу blockerRadius расширяет ForestLook)")]
    public float peekRadiusMul = 1.7f;
    [Range(0.05f, 1f)] public float peekSoftness = 0.55f;   // доля радиуса, на которой край окна тает
    [Range(0f, 0.6f)] public float peekMinAlpha = 0.0f;     // 0 = в центре окна дерево полностью прозрачно, 0.2 = остаётся призрак
    public float peekDepthMargin = 0.7f;                    // только то, что ближе к камере игрока минимум на столько метров

    static CameraFollow instance;

    static readonly int PeekDataId = Shader.PropertyToID("_PeekData");
    static readonly int PeekParamsId = Shader.PropertyToID("_PeekParams");
    static readonly int PeekSupportId = Shader.PropertyToID("_PeekSupport");

    Camera cam;
    PlayerInteraction interaction;
    Rigidbody targetRb;
    Transform cachedFor;

    // сглаживание фокуса: две ступени подряд дают мягкий старт и мягкий финиш
    Vector3 stage1, stage1Vel, focus, focusVel;
    bool snapped;

    Vector3 lastTargetPos, ahead, aheadVel;
    bool hasLast;
    float speed01, speed01Vel;

    float userZoom = 1f, zoomSmoothed = 1f, chopT, introElapsed;
    float shakeT, shakeDur, shakeAmp;

    Vector3 camPos;   // положение камеры без тряски (для проверки преград)
    Vector3 basePos;  // «спокойное» положение и поворот: без тряски и дыхания (для меток UI в мире)
    Quaternion baseRot = Quaternion.identity;

    // ---- преграды
    class Hidden { public ShadowCastingMode mode; public float lastSeen; public float since; }
    readonly Dictionary<Collider, bool> treeCache = new Dictionary<Collider, bool>();
    bool peekChecked, peekSupported;
    float peekNextCheck, peekStrength;
    readonly Dictionary<Renderer, Hidden> hidden = new Dictionary<Renderer, Hidden>();
    readonly Dictionary<Collider, Renderer[]> rendererCache = new Dictionary<Collider, Renderer[]>();
    readonly RaycastHit[] hits = new RaycastHit[32];
    readonly List<Renderer> toRestore = new List<Renderer>();

    void Awake()
    {
        cam = GetComponent<Camera>();
        instance = this;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
        RestoreAll();
        ClearPeek();
    }

    void OnDisable() { RestoreAll(); ClearPeek(); }

    static void ClearPeek()
    {
        Shader.SetGlobalVector(PeekParamsId, new Vector4(0f, 1f, 0f, 0f));   // сила 0: окно выключено
    }

    // После телепорта игрока: камера мгновенно встаёт на новое место, а не едет туда через весь остров
    public static void SnapToTarget()
    {
        OrbitCamera.SnapToTarget();
        if (instance != null) instance.snapped = false;
    }

    // Вызывай откуда угодно: CameraFollow.Shake(0.1f, 0.2f);
    public static void Shake(float amplitude = 0.12f, float duration = 0.2f)
    {
        if (OrbitCamera.Active) { OrbitCamera.AddShake(amplitude, duration); return; }   // свободная камера трясётся сама
        if (instance != null) instance.AddShake(amplitude, duration);
    }

    // Проекция точки мира на экран по «спокойной» камере: без тряски и дыхания.
    // Метки над объектами используют её, чтобы не плавать вместе с покачиванием камеры.
    public static bool TryWorldToScreenStable(Vector3 world, out Vector3 screen)
    {
        screen = default;
        if (instance == null || !instance.enabled || !instance.snapped || instance.cam == null) return false;   // выключена (работает OrbitCamera): метки берут обычную проекцию
        var c = instance.cam;
        Matrix4x4 w2c = Matrix4x4.TRS(instance.basePos, instance.baseRot, new Vector3(1f, 1f, -1f)).inverse;
        Vector3 cp = w2c.MultiplyPoint3x4(world);
        Vector4 clip = c.projectionMatrix * w2c * new Vector4(world.x, world.y, world.z, 1f);
        if (Mathf.Abs(clip.w) < 1e-5f) return false;
        screen = new Vector3(
            (clip.x / clip.w * 0.5f + 0.5f) * c.pixelWidth,
            (clip.y / clip.w * 0.5f + 0.5f) * c.pixelHeight,
            -cp.z);                       // z > 0: точка перед камерой, как у WorldToScreenPoint
        return true;
    }

    void AddShake(float amp, float dur)
    {
        if (VibeSettings.ReduceEffects) amp *= 0.3f;   // GDD 9.3: «Уменьшить тряску и эффекты»
        float current = shakeDur > 0f ? shakeAmp * Mathf.Clamp01(shakeT / shakeDur) : 0f;
        shakeAmp = Mathf.Max(current, amp);
        shakeDur = Mathf.Max(0.05f, dur);
        shakeT = shakeDur;
    }

    static Vector3 Damp(Vector3 cur, Vector3 want, ref Vector3 vel, float tXZ, float tY, float dt)
    {
        cur.x = Mathf.SmoothDamp(cur.x, want.x, ref vel.x, tXZ, Mathf.Infinity, dt);
        cur.z = Mathf.SmoothDamp(cur.z, want.z, ref vel.z, tXZ, Mathf.Infinity, dt);
        cur.y = Mathf.SmoothDamp(cur.y, want.y, ref vel.y, tY, Mathf.Infinity, dt);
        return cur;
    }

    void LateUpdate()
    {
        if (target == null)
        {
            var p = PlayerMovement.Instance;
            if (p != null) target = p.transform;
            else return;
        }

        // ограничиваем dt, чтобы после подвисания камера не «выстреливала»
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        if (dt < 0.0001f) return;

        if (cachedFor != target)
        {
            cachedFor = target;
            interaction = target.GetComponent<PlayerInteraction>();
            targetRb = target.GetComponent<Rigidbody>();
        }

        // ---------- зум
        if (allowScrollZoom)
        {
            float s = Input.mouseScrollDelta.y;
            if (Mathf.Abs(s) > 0.01f) userZoom = Mathf.Clamp(userZoom - s * zoomStep, zoomMin, zoomMax);
        }
        zoomSmoothed = Mathf.Lerp(zoomSmoothed, userZoom, 1f - Mathf.Exp(-6f * dt));

        bool chopping = interaction != null && interaction.IsChopping;
        chopT = Mathf.MoveTowards(chopT, chopping ? 1f : 0f, dt / 0.7f);
        float chopMul = Mathf.Lerp(1f, chopZoom, Mathf.SmoothStep(0f, 1f, chopT));

        float introMul = 1f;
        if (intro && introElapsed < introTime)
        {
            introElapsed += dt;
            float k = Mathf.Clamp01(introElapsed / Mathf.Max(0.1f, introTime));
            float e = k * k * (3f - 2f * k);           // smoothstep: мягко стартует и мягко заканчивает
            introMul = Mathf.Lerp(introZoom, 1f, e);
        }

        float zoom = zoomSmoothed * chopMul * introMul * Mathf.Max(0.3f, zoomBias);

        // ---------- скорость игрока (из физики, она ровнее, чем разница позиций по кадрам)
        Vector3 pos = target.position;
        if (!hasLast) { lastTargetPos = pos; hasLast = true; }

        Vector3 vel;
        if (targetRb != null) vel = targetRb.linearVelocity;
        else vel = (pos - lastTargetPos) / dt;
        lastTargetPos = pos;
        vel.y = 0f;
        float spd = vel.magnitude;
        if (spd > 50f) { vel = Vector3.zero; spd = 0f; }   // телепорт, а не бег

        float k01 = Mathf.Clamp01(spd / Mathf.Max(0.1f, fullSpeed));
        Vector3 want = spd > 0.3f ? vel / spd * (lookAhead * k01) : Vector3.zero;
        ahead = Vector3.SmoothDamp(ahead, want, ref aheadVel, Mathf.Max(0.05f, lookAheadSmooth), Mathf.Infinity, dt);

        speed01 = Mathf.SmoothDamp(speed01, k01, ref speed01Vel, Mathf.Max(0.05f, speedFovSmooth), Mathf.Infinity, dt);

        // ---------- масштаб кадра
        if (cam.orthographic) cam.orthographicSize = orthoSize * zoom;
        else cam.fieldOfView = fieldOfView + speedFovBoost * speed01;

        // ---------- мягкое следование за фокусом
        Vector3 wantFocus = pos + lookOffset + ahead + focusBias;
        if (!snapped)
        {
            stage1 = focus = wantFocus;
            stage1Vel = focusVel = Vector3.zero;
            snapped = true;
        }
        else
        {
            float half = Mathf.Max(0.02f, smoothTime * 0.5f);
            float halfY = Mathf.Max(0.02f, verticalSmoothTime * 0.5f);
            stage1 = Damp(stage1, wantFocus, ref stage1Vel, half, halfY, dt);
            focus = Damp(focus, stage1, ref focusVel, half, halfY, dt);
        }

        // ---------- положение и поворот
        Quaternion rot = Quaternion.Euler(pitch, yaw + yawBias, 0f);
        float dist = cam.orthographic ? distance : distance * zoom;
        camPos = focus - rot * Vector3.forward * dist;
        basePos = camPos;
        baseRot = rot;

        // тряска (поверх плавного положения, чтобы не копилась)
        Vector3 shake = Vector3.zero;
        if (shakeT > 0f)
        {
            shakeT -= dt;
            float f = Mathf.Clamp01(shakeT / shakeDur);
            Vector2 r = Random.insideUnitCircle * (shakeAmp * f * f);
            shake = rot * new Vector3(r.x, r.y, 0f);
        }

        // дыхание: крошечный плавный поворот шумом Перлина
        Quaternion swayRot = Quaternion.identity;
        if (sway && swayAmount > 0f)
        {
            float t = Time.time * swaySpeed;
            float sx = (Mathf.PerlinNoise(t, 0.37f) - 0.5f) * 2f;
            float sy = (Mathf.PerlinNoise(0.71f, t) - 0.5f) * 2f;
            swayRot = Quaternion.Euler(sx * swayAmount, sy * swayAmount, 0f);
        }

        transform.rotation = rot * swayRot;
        transform.position = camPos + shake;

        UpdatePeek(pos);
        if (hideBlockers) HandleBlockers(pos + Vector3.up * 1f);
    }

    // ------------------------------------------------------------ окно в листве
    // Каждый кадр отдаём шейдеру деревьев (глобально) точку на экране, где стоит игрок, радиус окна и глубину игрока.
    // Шейдер сам дизерит пиксели, которые лежат внутри окна И ближе к камере, чем игрок. Ни рендереры, ни материалы не трогаем:
    // SRP Batcher и инстансинг целы, а дерево никогда не «выключается» (поэтому нет мерцания).
    bool PeekSupported()
    {
        if (peekChecked) return peekSupported;
        if (Time.unscaledTime < peekNextCheck) return false;
        peekNextCheck = Time.unscaledTime + 1f;

        var list = ResourceItem.Active;
        for (int i = 0; i < list.Count; i++)
        {
            var it = list[i];
            if (it == null) continue;
            var rs = it.GetComponentsInChildren<MeshRenderer>(true);
            for (int j = 0; j < rs.Length; j++)
            {
                string n = rs[j].gameObject.name;
                if (n == "Outline" || n == "Hull" || n == "HighlightRing") continue;
                var m = rs[j].sharedMaterial;
                if (m == null) continue;
                peekSupported = m.HasProperty(PeekSupportId);   // свойство добавляет патч TreeToon
                peekChecked = true;
                return peekSupported;
            }
        }
        return false;
    }

    void UpdatePeek(Vector3 playerPos)
    {
        bool on = peekHole && PeekSupported();
        peekStrength = Mathf.MoveTowards(peekStrength, on ? 1f : 0f, Time.unscaledDeltaTime / 0.25f);
        if (peekStrength <= 0.001f) { ClearPeek(); return; }

        Vector3 chest = playerPos + Vector3.up * 1f;
        Vector3 vp = cam.WorldToViewportPoint(chest);
        float worldR = Mathf.Max(0.3f, blockerRadius * peekRadiusMul);
        float viewH = cam.orthographic
            ? cam.orthographicSize * 2f
            : 2f * Mathf.Max(0.1f, vp.z) * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float r = worldR / Mathf.Max(0.01f, viewH);   // радиус в долях высоты экрана

        Shader.SetGlobalVector(PeekDataId, new Vector4(vp.x, vp.y, r, peekSoftness));
        Shader.SetGlobalVector(PeekParamsId, new Vector4(vp.z - peekDepthMargin, cam.aspect, peekStrength, peekMinAlpha));
    }

    bool IsTreeCollider(Collider col)
    {
        bool tree;
        if (treeCache.TryGetValue(col, out tree)) return tree;
        tree = col.GetComponentInParent<ResourceItem>() != null || col.GetComponentInParent<TreeSpeciesInfo>() != null;
        treeCache[col] = tree;
        return tree;
    }

    // ------------------------------------------------------------ прозрачность преград
    void HandleBlockers(Vector3 targetPoint)
    {
        Vector3 from = camPos;
        Vector3 dir = targetPoint - from;
        float dist = dir.magnitude;
        float now = Time.time;
        bool peekActive = peekHole && peekSupported && peekStrength > 0.5f;

        if (dist > blockerStopShort + 0.5f)
        {
            dir /= dist;
            int n = Physics.SphereCastNonAlloc(from, blockerRadius, dir, hits, dist - blockerStopShort, blockerMask,
                                               QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                var col = hits[i].collider;
                if (col == null || col is TerrainCollider) continue;
                if (col.transform.IsChildOf(target)) continue;
                if (peekActive && IsTreeCollider(col)) continue;   // деревья: мягкое окно в шейдере, не пропадают

                Bounds bb = col.bounds;
                if (bb.size.y < blockerMinHeight) continue;
                if (bb.size.x > 30f || bb.size.z > 30f) continue;   // полы и большие площадки

                if (!rendererCache.TryGetValue(col, out var rs))
                {
                    rs = col.GetComponentsInChildren<Renderer>();
                    rendererCache[col] = rs;
                }

                for (int j = 0; j < rs.Length; j++)
                {
                    var r = rs[j];
                    if (r == null || !r.enabled) continue;
                    if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                    if (r.transform.IsChildOf(target)) continue;

                    Vector3 c = r.bounds.center;
                    float along = Mathf.Clamp(Vector3.Dot(c - from, dir), 0f, dist - blockerStopShort);
                    Vector3 onLine = from + dir * along;
                    if (r.bounds.SqrDistance(onLine) > blockerRadius * blockerRadius) continue;

                    if (!hidden.TryGetValue(r, out var h))
                    {
                        h = new Hidden { mode = r.shadowCastingMode, since = now };
                        hidden[r] = h;
                        r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;   // сам объект пропадает, тень остаётся
                    }
                    h.lastSeen = now;
                }
            }
        }

        if (hidden.Count == 0) return;
        toRestore.Clear();
        foreach (var kv in hidden)
            if (kv.Key == null || (now - kv.Value.lastSeen > blockerRestoreDelay && now - kv.Value.since > blockerMinHideTime)) toRestore.Add(kv.Key);
        for (int i = 0; i < toRestore.Count; i++)
        {
            var r = toRestore[i];
            if (r != null) r.shadowCastingMode = hidden[r].mode;
            hidden.Remove(r);
        }
    }

    void RestoreAll()
    {
        foreach (var kv in hidden)
            if (kv.Key != null) kv.Key.shadowCastingMode = kv.Value.mode;
        hidden.Clear();
    }
}
