// Assets/_Project/Scripts/Camera/OrbitCamera.cs (НЕ в Editor)
// Камера «как в Роблокс»: вращение вокруг игрока, зум колёсиком, столкновения (камера подъезжает ближе, а не прячет объекты).
// Ставится сама после загрузки сцены, в сцену вешать ничего не нужно (как VibeBootstrap и LightingRig).
//
// Режимы: «Свободная» (эта камера, по умолчанию) и «Изометрия» (старая CameraFollow). Переключатель в настройках (Esc) и клавиша F3.
// Выбор хранится в PlayerPrefs, как остальные настройки VibeSettings. Из кода: OrbitCamera.Enabled = true/false.
//
// Как это работает:
//  - В режиме «Свободная» CameraFollow ВЫКЛЮЧАЕТСЯ (его OnDisable сам возвращает скрытые преграды и убирает «окно в листве»),
//    а эта камера каждый кадр пишет свой yaw в PlayerMovement.cameraYaw: персонаж бежит туда, куда смотрит камера.
//  - Показ здания: свободная камера сама уходит в «кинокадр» (revealPitch / revealFov, здание целиком в кадре, облёт шире изометрического,
//    без рывков от столкновений). Доля показа приходит из CameraVibe.RevealShown.
//  - CameraVibe продолжает работать: его focusBias / zoomBias / yawBias мы читаем у CameraFollow. Поэтому в орбите живы
//    наезд на говорящего, притяжение к NPC, «щелчок» зума при рубке и облёт здания при показе стадии.
//  - CameraFollow.Shake(...) пересылается сюда, пока включена орбита (см. правку в CameraFollow).
//  - При возврате в изометрию всё восстанавливается (ortho/FOV, cameraYaw).
//  - ПК: мышь крутит камеру ВСЕГДА, системный курсор скрыт, вместо него ПРИЦЕЛ в центре экрана. Колёсико = зум.
//    Прицел заменяет курсор: ЛКМ по дереву под прицелом рубит его (PlayerInteraction берёт OrbitCamera.PointerPos), прицел светится на дереве.
//    Камера чуть смещается за плечо, чтобы прицел не лежал на голове персонажа.
//    Зажми Alt, чтобы показать обычный курсор (кнопки HUD, миникарта). Курсор сам появляется в окнах, на паузе (Esc) и при Alt+Tab.
//    В настройках «Вращение камеры» можно вернуть старый способ: вращать, только пока зажата правая кнопка мыши.
//  - Телефон: один палец по правой половине экрана (не по кнопке UI) вращает камеру. Щипок для зума пока не сделан.
// Уважает MobileInput.ModalOpen: пока открыто окно, камера не вращается и не зумится.
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DefaultExecutionOrder(-90)]
public class OrbitCamera : MonoBehaviour
{
    const string PrefKey = "tw_cam_orbit_v2";   // новый ключ: у всех по умолчанию «Свободная»

    const string LookKey = "tw_cam_mouselook";

    static int cached = -1;                      // -1 = ещё не читали PlayerPrefs
    static int cachedLook = -1;
    static OrbitCamera instance;

    public static bool Active { get; private set; }   // орбита сейчас реально рулит камерой

    // true: вместо курсора на экране прицел (мышь крутит камеру, курсор скрыт)
    public static bool CrosshairActive { get { return Active && instance != null && instance.crosshairOn; } }

    // «курсор» для игры: при прицеле центр экрана, иначе настоящая мышь. PlayerInteraction целится по нему
    public static Vector2 PointerPos
    {
        get
        {
            if (CrosshairActive) return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            return Input.mousePosition;
        }
    }

    // что выбрано в настройках (по умолчанию да). Читаем PlayerPrefs один раз, дальше из памяти
    public static bool Enabled
    {
        get
        {
            if (cached < 0) cached = PlayerPrefs.GetInt(PrefKey, 1);
            return cached == 1;
        }
        set
        {
            cached = value ? 1 : 0;
            PlayerPrefs.SetInt(PrefKey, cached);
        }
    }

    // true: мышь крутит камеру всегда (курсор скрыт); false: только с зажатой правой кнопкой. По умолчанию true
    public static bool MouseLook
    {
        get
        {
            if (cachedLook < 0) cachedLook = PlayerPrefs.GetInt(LookKey, 1);
            return cachedLook == 1;
        }
        set
        {
            cachedLook = value ? 1 : 0;
            PlayerPrefs.SetInt(LookKey, cachedLook);
        }
    }

    // вызывает CameraFollow.Shake, пока включена орбита
    public static void AddShake(float amplitude, float duration)
    {
        if (instance != null) instance.DoShake(amplitude, duration);
    }

    [Header("Управление")]
    public KeyCode toggleKey = KeyCode.F3;
    public KeyCode freeCursorKey = KeyCode.LeftAlt;   // пока зажата (или RightAlt): курсор виден, камера не крутится
    public float mouseSensitivity = 3f;      // градусов на единицу оси мыши
    public float touchSensitivity = 0.22f;   // градусов на пиксель
    public float scrollStep = 1.2f;          // метров на щелчок колёсика

    [Header("Положение")]
    public float distance = 9f;
    public float distMin = 3f;
    public float distMax = 18f;
    public float pitch = 22f;                // угол сверху вниз, градусы
    public float pitchMin = -5f;
    public float pitchMax = 70f;
    public float fov = 60f;
    public Vector3 focusOffset = new Vector3(0f, 1.6f, 0f);   // на что смотрим: грудь/голова, а не ноги
    public float followSmooth = 0.06f;       // у Роблокс камера почти жёсткая

    [Header("Прицел")]
    public float shoulderOffset = 0.55f;     // метров вбок: камера «за плечом», персонаж слева, прицел свободен
    public float shoulderLift = 0.35f;       // метров вверх: персонаж чуть ниже центра, прицел над головой
    public float crosshairSize = 56f;        // размер в единицах канваса 1920x1080

    [Header("Кино")]
    public float chopZoom = 0.92f;           // во время рубки камера чуть ближе (1 = выключить)
    public bool intro = true;                // вступительный наезд при старте сцены (берёт время и силу из CameraFollow)

    [Header("Показ здания (облёт после этапа стройки)")]
    public float revealPitch = 32f;          // угол сверху вниз во время показа: чуть выше обычного, видно всю постройку
    public float revealFov = 40f;            // поле зрения на показе: уже обычного, меньше перспективных искажений (как в изометрии)
    public float revealFit = 1.35f;          // запас кадра вокруг здания (1 = впритык)
    public float revealMinDist = 6f;         // ближе этого камера на показе не подъезжает
    public float revealSweepMul = 1.6f;      // облёт шире, чем в изометрии (там 25°): в 3D он выглядит живее
    public float revealFocusSmooth = 0.35f;  // фокус на показе плавнее обычного (у обычного 0.06, он слишком жёсткий)

    [Header("Столкновения")]
    public float collisionRadius = 0.3f;
    public float minCamDistance = 0.9f;
    public float ignoreSmallerThan = 0.8f;   // колайдеры меньше (размер bounds) камеру не двигают: цветы, камешки
    public LayerMask collisionMask = ~0;

    Camera cam;
    CameraFollow follow;
    PlayerMovement player;
    PlayerInteraction interaction;
    float nextFind;

    bool applied;

    // что вернуть при выходе в изометрию
    bool wasOrtho;
    float wasFov, wasYaw;

    float yaw;
    float curDist;
    Vector3 focus, focusVel;
    int touchId = -1;
    bool cursorHeld;
    bool crosshairOn;
    float aimT, chT;
    GameObject chGo;
    Image chImg;
    RectTransform chRt;
    static Sprite reticle;

    float chopT, introElapsed;
    float shakeT, shakeDur, shakeAmp;

    static readonly RaycastHit[] hits = new RaycastHit[16];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Active = false; instance = null; cached = -1; cachedLook = -1; reticle = null; snapPending = false; }

    // После телепорта игрока: фокус камеры сразу встаёт на него (зовёт CameraFollow.SnapToTarget)
    static bool snapPending;
    public static void SnapToTarget() { if (instance != null) snapPending = true; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (FindFirstObjectByType<OrbitCamera>() != null) return;
        var go = new GameObject("OrbitCamera");
        DontDestroyOnLoad(go);
        go.AddComponent<OrbitCamera>();
    }

    void Awake() { instance = this; }

    void OnDestroy() { if (instance == this) instance = null; }

    void OnDisable() { ReleaseCursor(); }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey) && !MobileInput.ModalOpen)
        {
            Enabled = !Enabled;
            PlayerPrefs.Save();
        }
    }

    void LateUpdate()
    {
        // сцена сменилась или это меню: камеры/игрока может не быть
        if (cam == null || !cam.isActiveAndEnabled)
        {
            cam = Camera.main;
            follow = null;
            applied = false;
            Active = false;
        }
        if (cam == null) return;

        if (follow == null) follow = cam.GetComponent<CameraFollow>();
        if (follow == null) return;   // нет игровой камеры (главное меню)

        if (player == null)
        {
            if (Time.unscaledTime < nextFind) return;
            nextFind = Time.unscaledTime + 1f;
            player = PlayerMovement.Instance;
            if (player == null) return;
            interaction = player.GetComponent<PlayerInteraction>();
        }

        bool want = Enabled;
        if (want && !applied) Enter();
        else if (!want && applied) Exit();

        if (applied) Step();
    }

    void Enter()
    {
        wasOrtho = cam.orthographic;
        wasFov = cam.fieldOfView;
        wasYaw = player.cameraYaw;

        follow.enabled = false;           // OnDisable вернёт скрытые преграды и уберёт окно в листве
        cam.orthographic = false;
        cam.fieldOfView = fov;

        // старт за спиной персонажа
        yaw = player.model != null ? player.model.eulerAngles.y : follow.yaw;
        curDist = distance;
        focus = player.transform.position + focusOffset;
        focusVel = Vector3.zero;
        shakeT = 0f;

        // вступительный наезд только в начале сцены, а не при переключении в настройках
        introElapsed = (intro && follow.intro && Time.timeSinceLevelLoad < 3f) ? 0f : float.MaxValue;

        applied = true;
        Active = true;
    }

    void Exit()
    {
        applied = false;
        Active = false;
        shakeT = 0f;
        ReleaseCursor();

        if (follow != null) follow.enabled = true;
        if (cam != null)
        {
            cam.orthographic = wasOrtho;
            cam.fieldOfView = wasFov;
        }
        if (player != null) player.cameraYaw = wasYaw;
    }

    void ReleaseCursor()
    {
        crosshairOn = false;
        if (cursorHeld) SetCursorLocked(false);
        if (chGo != null) chGo.SetActive(false);
    }

    void SetCursorLocked(bool locked)
    {
        cursorHeld = locked;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    void DoShake(float amp, float dur)
    {
        if (!applied) return;
        if (VibeSettings.ReduceEffects) amp *= 0.3f;   // GDD 9.3: «Уменьшить тряску и эффекты»
        float current = shakeDur > 0f ? shakeAmp * Mathf.Clamp01(shakeT / shakeDur) : 0f;
        shakeAmp = Mathf.Max(current, amp);
        shakeDur = Mathf.Max(0.05f, dur);
        shakeT = shakeDur;
    }

    // ---------------------------------------------------------------- прицел
    void EnsureCrosshair()
    {
        if (chGo != null) return;

        chGo = new GameObject("OrbitCrosshair", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        chGo.layer = 5;
        chGo.transform.SetParent(transform, false);
        var canvas = chGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 400;               // выше HUD, ниже окон и настроек (у них 1000)
        var sc = chGo.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        sc.matchWidthOrHeight = 0.5f;

        var img = new GameObject("Reticle", typeof(RectTransform), typeof(Image));
        img.layer = 5;
        img.transform.SetParent(chGo.transform, false);
        chImg = img.GetComponent<Image>();
        chImg.sprite = Reticle();
        chImg.raycastTarget = false;
        chRt = chImg.rectTransform;
        chRt.anchorMin = chRt.anchorMax = chRt.pivot = new Vector2(0.5f, 0.5f);
        chRt.anchoredPosition = Vector2.zero;
        chRt.sizeDelta = new Vector2(crosshairSize, crosshairSize);
        chGo.SetActive(false);
    }

    void UpdateCrosshair(float dt)
    {
        bool show = crosshairOn;
        if (show) EnsureCrosshair();
        if (chGo == null) return;
        if (chGo.activeSelf != show) chGo.SetActive(show);
        if (!show) return;

        // на дереве, которое можно рубить, прицел светится золотом и чуть растёт
        bool hover = interaction != null && interaction.HoverItem != null;
        chT = Mathf.MoveTowards(chT, hover ? 1f : 0f, dt / 0.12f);
        chImg.color = Color.Lerp(Color.white, new Color32(0xF5, 0xC5, 0x42, 255), chT);
        chRt.localScale = Vector3.one * Mathf.Lerp(1f, 1.35f, Mathf.SmoothStep(0f, 1f, chT));
    }

    // кольцо с точкой в центре и тёмной обводкой в цвет UI (#1C2A3A), нарисовано кодом: картинок в проект добавлять не нужно
    static Sprite Reticle()
    {
        if (reticle != null) return reticle;
        const int N = 64;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[N * N];
        Color navy = new Color32(0x1C, 0x2A, 0x3A, 255);
        float c = (N - 1) * 0.5f;
        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c));
                float shape = Mathf.Min(Mathf.Abs(d - 15f) - 1.2f, d - 3f);   // расстояние до кольца или точки (< 0 внутри)
                float fill = Mathf.Clamp01(0.5f - shape);
                float edge = Mathf.Clamp01(0.5f - (shape - 1.7f));              // обводка шире на 1.7 px
                Color col = Color.Lerp(navy, Color.white, fill);
                col.a = edge;
                px[y * N + x] = col;
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        reticle = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
        return reticle;
    }

    void Step()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        if (dt < 0.0001f) return;

        // ---------- ввод
        Vector2 delta = Vector2.zero;
        bool modal = MobileInput.ModalOpen;
        bool altHeld = Input.GetKey(freeCursorKey) || Input.GetKey(KeyCode.RightAlt);
        bool mouseLook = MouseLook;

        // мышь крутит камеру: всегда (курсор скрыт; Alt, окна и пауза его освобождают) или только с зажатой ПКМ
        bool wantLock = !modal && Application.isFocused && (mouseLook ? !altHeld : Input.GetMouseButton(1));
        bool lockChanged = wantLock != cursorHeld;
        if (lockChanged) SetCursorLocked(wantLock);
        crosshairOn = wantLock && mouseLook;   // прицел только в режиме «мышью» (с ПКМ курсор нужен как был)

        if (!modal)
        {
            // кадр смены режима пропускаем: курсор «прыгает» в центр, это не движение мыши
            if (wantLock && !lockChanged)
            {
                delta += new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * mouseSensitivity;
                delta = Vector2.ClampMagnitude(delta, 45f);
            }

            // колёсико: с зажатым Alt оно отдано интерфейсу (зум миникарты и т. п.)
            float s = Input.mouseScrollDelta.y;
            if (Mathf.Abs(s) > 0.01f && !(mouseLook && altHeld)) distance = Mathf.Clamp(distance - s * scrollStep, distMin, distMax);

            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);
                if (touchId == -1 && t.phase == TouchPhase.Began && t.position.x > Screen.width * 0.5f &&
                    (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject(t.fingerId)))
                    touchId = t.fingerId;

                if (t.fingerId != touchId) continue;
                if (t.phase == TouchPhase.Moved) delta += t.deltaPosition * touchSensitivity;
                if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) touchId = -1;
            }
        }
        else touchId = -1;

        yaw += delta.x;
        pitch = Mathf.Clamp(pitch - delta.y, pitchMin, pitchMax);

        // ---------- кино: масштаб от CameraVibe, рубки и вступления
        bool chopping = interaction != null && interaction.IsChopping;
        chopT = Mathf.MoveTowards(chopT, chopping ? 1f : 0f, dt / 0.7f);
        float chopMul = Mathf.Lerp(1f, chopZoom, Mathf.SmoothStep(0f, 1f, chopT));

        float introMul = 1f;
        if (introElapsed < follow.introTime)
        {
            introElapsed += dt;
            float k = Mathf.Clamp01(introElapsed / Mathf.Max(0.1f, follow.introTime));
            introMul = Mathf.Lerp(follow.introZoom, 1f, k * k * (3f - 2f * k));
        }

        // ---------- показ здания: плавно уходим в «кинокадр» (доля rv ведёт CameraVibe, она уже сглажена и в начале, и в конце)
        float rv = Mathf.Clamp01(CameraVibe.RevealShown);
        bool reveal = rv > 0.001f;

        float wantDist = distance * Mathf.Max(0.3f, follow.zoomBias) * chopMul * introMul;
        if (reveal)
        {
            // дистанция, на которой здание целиком влезает в кадр при revealFov
            float r = CameraVibe.RevealRadius;
            float fitDist = r > 0.1f
                ? Mathf.Clamp(r * revealFit / Mathf.Tan(revealFov * 0.5f * Mathf.Deg2Rad), revealMinDist, distMax * 1.3f)
                : wantDist;
            wantDist = Mathf.Lerp(wantDist, fitDist, rv);
        }

        // ---------- точка, на которую смотрим (+ сдвиг от CameraVibe: говорящий, NPC, здание при показе)
        Transform target = player.transform;
        aimT = Mathf.MoveTowards(aimT, crosshairOn ? 1f : 0f, dt / 0.25f);
        float aim = Mathf.SmoothStep(0f, 1f, aimT);
        float yawBias = follow.yawBias * (reveal ? Mathf.Lerp(1f, revealSweepMul, rv) : 1f);
        Vector3 shoulder = Quaternion.Euler(0f, yaw + yawBias, 0f) * Vector3.right * (shoulderOffset * aim);
        Vector3 wantFocus = target.position + focusOffset + follow.focusBias + shoulder + Vector3.up * (shoulderLift * aim);
        float focusSmooth = reveal ? Mathf.Lerp(followSmooth, revealFocusSmooth, rv) : followSmooth;
        if (snapPending) { focus = wantFocus; focusVel = Vector3.zero; snapPending = false; }
        focus = Vector3.SmoothDamp(focus, wantFocus, ref focusVel, Mathf.Max(0.01f, focusSmooth), Mathf.Infinity, dt);

        // yawBias: облёт здания при показе стадии. Только на вид, на управление не влияет
        float shownPitch = reveal ? Mathf.Lerp(pitch, revealPitch, rv) : pitch;   // pitch игрока не трогаем: после показа вернётся
        Quaternion rot = Quaternion.Euler(shownPitch, yaw + yawBias, 0f);
        Vector3 back = rot * Vector3.back;

        // ---------- столкновения: камера подъезжает к игроку
        float allowed = wantDist;
        int n = Physics.SphereCastNonAlloc(focus, collisionRadius, back, hits, wantDist, collisionMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var h = hits[i];
            var col = h.collider;
            if (col == null || h.distance <= 0.0001f) continue;          // уже внутри (например, земля под ногами)
            if (col.transform.IsChildOf(target)) continue;               // сам игрок
            if (col.attachedRigidbody != null) continue;                 // питомцы, рабочие
            if (col.bounds.size.magnitude < ignoreSmallerThan) continue; // мелочь
            // на показе само здание (и летящие детали) камеру не двигает: иначе она дёргается, пока облетает стройку
            if (reveal && col.bounds.SqrDistance(CameraVibe.RevealPoint) < CameraVibe.RevealRadius * CameraVibe.RevealRadius) continue;
            if (h.distance < allowed) allowed = h.distance;
        }
        allowed = Mathf.Max(minCamDistance, allowed);

        // ближе к игроку сразу (чтобы не просвечивало), дальше обратно плавно
        // на показе в обе стороны плавно: резкий наезд от препятствия на облёте был бы виден как рывок
        if (reveal) curDist = Mathf.Lerp(curDist, allowed, 1f - Mathf.Exp(-3f * dt));
        else if (allowed < curDist) curDist = allowed;
        else curDist = Mathf.Lerp(curDist, allowed, 1f - Mathf.Exp(-4f * dt));

        // ---------- тряска: поверх готового положения, чтобы не копилась
        Vector3 shake = Vector3.zero;
        if (shakeT > 0f)
        {
            shakeT -= dt;
            float f = Mathf.Clamp01(shakeT / shakeDur);
            Vector2 r = Random.insideUnitCircle * (shakeAmp * f * f);
            shake = rot * new Vector3(r.x, r.y, 0f);
        }

        cam.fieldOfView = reveal ? Mathf.Lerp(fov, revealFov, rv) : fov;
        cam.transform.SetPositionAndRotation(focus + back * curDist + shake, rot);

        UpdateCrosshair(dt);

        // движение персонажа теперь относительно камеры (без облёта yawBias)
        player.cameraYaw = yaw;
    }
}
