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
    public float distance = 6.5f;            // уютная дистанция: Лео крупнее (было 9)
    public float distMin = 2.5f;
    public float distMax = 18f;
    public float pitch = 15f;                // угол сверху вниз, градусы: ниже, лес «стоит» стеной за героем (было 22)
    public float pitchMin = -5f;
    public float pitchMax = 70f;
    public float fov = 50f;                  // уже, чем было (60): меньше «рыбьего» плоского кадра, лес собирается в слои
    public Vector3 focusOffset = new Vector3(0f, 1.3f, 0f);   // на что смотрим: грудь, Лео ближе к центру кадра (было 1.6)
    public float followSmooth = 0.08f;       // отзывчивая камера в покое

    [Header("Camera Polish: плавность следования")]
    [Tooltip("Небольшая инерция фокуса при беге, без задержки вращения мышью")]
    [Range(.05f,.3f)] public float movingFollowSmooth = .13f;
    [Tooltip("Отдельное сглаживание высоты: убирает подпрыгивание камеры на неровной земле")]
    [Range(.05f,.4f)] public float verticalFollowSmooth = .19f;
    [Tooltip("Насколько камера показывает пространство впереди бегущего Лео, метры")]
    [Range(0f,.8f)] public float movingLookAhead = .35f;
    [Tooltip("Плавность смещения фокуса в сторону движения")]
    [Range(.08f,.6f)] public float lookAheadSmooth = .24f;
    [Tooltip("Скорость, при которой смещение вперёд достигает максимума, м/с")]
    [Min(1f)] public float lookAheadFullSpeed = 4.5f;

    [Header("Camera Polish: плавность зума")]
    [Tooltip("Сглаживание намеренного зума колёсиком, не задерживает спасение от стен")]
    [Range(.06f,.4f)] public float manualZoomSmooth = .16f;

    [Header("Camera Polish: остановка, прицел, возврат управления")]
    [Tooltip("Как плавно следование переключается между «стоим» и «бежим», с. Больше = мягче остановка, без подбегания камеры")]
    [Range(.05f,.8f)] public float followStopBlend = .35f;
    [Tooltip("Во сколько раз слабее тряска, пока включён прицел: цель не уходит из-под него")]
    [Range(.1f,1f)] public float aimShakeScale = .5f;
    [Tooltip("Пауза перед возвращением вращения мышью после окна, диалога, показа стройки и Alt+Tab, с")]
    [Range(0f,.5f)] public float lookResumeDelay = .12f;
    [Tooltip("Фокус не заходит в стену: плечо и взгляд вперёд укорачиваются, если рядом коллайдер")]
    public bool focusWallClamp = true;
    [Tooltip("Запас до стены для смещённого фокуса, м")]
    [Range(0f,.4f)] public float focusWallPadding = .12f;

    [Tooltip("Добавка к FOV на бегу, градусы: ощущение скорости и глубины. В режиме уменьшенных эффектов выключена")]
    [Range(0f,6f)] public float runFovBoost = 2.5f;

    [Header("Camera Polish: дуга в покое")]
    [Tooltip("Лео стоит, вокруг есть место: камера проходит небольшую дугу туда и обратно. Выключается в режиме уменьшенных эффектов")]
    public bool idleArc = true;
    [Tooltip("Сколько секунд полного покоя до дуги")]
    [Min(1f)] public float idleArcDelay = 6f;
    [Tooltip("Размах дуги в одну сторону, градусы")]
    [Range(4f,30f)] public float idleArcAngle = 14f;
    [Tooltip("Длительность дуги, с")]
    [Range(2f,10f)] public float idleArcDuration = 5f;
    [Tooltip("Пауза до следующей дуги, с")]
    [Min(0f)] public float idleArcCooldown = 25f;
    [Tooltip("Как быстро камера возвращается, если дугу отменили движением, мышью или стеной, с")]
    [Range(.1f,.6f)] public float idleArcCancelSmooth = .25f;

    [Header("Прицел")]
    public float shoulderOffset = 0.7f;      // метров вбок: камера «за плечом», персонаж слева, прицел свободен
    public float shoulderLift = 0.2f;        // метров вверх: персонаж чуть ниже центра, прицел над головой
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
    [Tooltip("Минимальная дистанция на открытом месте. Препятствие может поджать камеру ближе.")]
    public float minCamDistance = 0.9f;
    public float ignoreSmallerThan = 0.8f;   // колайдеры меньше (размер bounds) камеру не двигают: цветы, камешки
    public LayerMask collisionMask = ~0;
    [Tooltip("Небольшой безопасный зазор перед стеной или стволом")]
    [Range(0f,.3f)] public float collisionPadding = .08f;
    [Tooltip("Пауза после выхода из-за препятствия, чтобы камера не дёргалась возле края")]
    [Range(0f,.3f)] public float collisionClearDelay = .10f;
    [Tooltip("Плавность возвращения камеры назад после столкновения")]
    [Range(.08f,.8f)] public float collisionReturnSmooth = .30f;

    [Header("ISL-016: видимость героя под кронами")]
    [Tooltip("Мягкое окно в объектах с Project/TreeToon перед героем. В первую очередь помогает с кронами; коллайдеры не трогает.")]
    public bool treePeek = true;
    [Tooltip("Радиус открываемой области в мировых метрах на глубине героя")]
    [Range(0.5f, 2f)] public float treePeekRadius = 1.05f;
    [Tooltip("Мягкость перехода от прозрачной части к полной кроне")]
    [Range(0.05f, 1f)] public float treePeekSoftness = 0.55f;
    [Tooltip("Непрозрачность в самом центре окна: 0 = не видно перекрывающую крону")]
    [Range(0f, 0.4f)] public float treePeekMinOpacity = 0.08f;
    [Tooltip("Запас глубины перед героем, чтобы не затрагивать кроны у него за спиной")]
    [Range(0f, 1f)] public float treePeekDepthMargin = 0.35f;
    [Tooltip("Высота центра окна над основанием персонажа")]
    [Range(0.3f, 1.5f)] public float treePeekPlayerHeight = 0.83f;
    [Tooltip("Время плавного появления и исчезновения окна")]
    [Range(0.05f, 1f)] public float treePeekFadeTime = 0.22f;

    static readonly int TreePeekDataId = Shader.PropertyToID("_PeekData");
    static readonly int TreePeekParamsId = Shader.PropertyToID("_PeekParams");
    float treePeekBlend;
    Vector3 treePeekLastViewport;

    Camera cam;
    CameraFollow follow;
    PlayerMovement player;
    Rigidbody playerBody;
    PlayerInteraction interaction;
    float nextFind;

    bool applied;

    // что вернуть при выходе в изометрию
    bool wasOrtho;
    float wasFov, wasYaw;

    float yaw;
    float curDist;
    float manualDist,manualDistVel,curDistVel,collisionHold;
    Vector3 focus, focusVel;
    Vector3 ahead,aheadVel;
    int touchId = -1;
    bool cursorHeld;
    bool wasConversation;
    float resumeLookAt, dialogueYawOffset;
    bool crosshairOn;
    float aimT, chT;
    GameObject chGo;
    Image chImg;
    RectTransform chRt;
    static Sprite reticle;

    float chopT, introElapsed;
    float moveBlend;
    bool wasModal;
    Vector3 lastPlayerPos;
    float idleT, arcT = -1f, arcSide = 1f, arcCool, arcOffset, arcOffsetVel;
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

    void OnDestroy()
    {
        if (applied) ClearTreePeek();
        if (instance == this) instance = null;
    }

    void OnDisable()
    {
        ReleaseCursor();
        if (applied) ClearTreePeek();
    }

    // Shader-глобали разделяются с CameraFollow. Записываем их только когда
    // именно OrbitCamera управляет игровой камерой; при выходе очищаем.
    static void ClearTreePeek()
    {
        Shader.SetGlobalVector(TreePeekParamsId, new Vector4(0f, 1f, 0f, 0f));
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey) && !MobileInput.ModalOpen && !DialoguePreferences.ConversationOwnsCamera)
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
            if (applied) ClearTreePeek();
            treePeekBlend = 0f;
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
            playerBody = player.GetComponent<Rigidbody>();
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
        manualDist = Mathf.Clamp(distance, distMin, distMax);
        manualDistVel = curDistVel = collisionHold = 0f;
        focus = player.transform.position + focusOffset;
        focusVel = Vector3.zero;
        ahead = aheadVel = Vector3.zero;
        moveBlend = 0f;
        idleT = 0f; arcT = -1f; arcOffset = arcOffsetVel = 0f; arcCool = 0f;
        lastPlayerPos = player.transform.position;
        wasModal = true;   // вход в режим: мышь оживает после короткой паузы, без рывка курсора
        playerBody = player.GetComponent<Rigidbody>();
        shakeT = 0f;

        // вступительный наезд только в начале сцены, а не при переключении в настройках
        introElapsed = (intro && follow.intro && Time.timeSinceLevelLoad < 3f) ? 0f : float.MaxValue;

        treePeekBlend = 0f;
        applied = true;
        Active = true;
    }

    void Exit()
    {
        ClearTreePeek();
        treePeekBlend = 0f;
        applied = false;
        Active = false;
        shakeT = 0f;
        collisionHold = 0f;
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
        if (crosshairOn) amp *= aimShakeScale;          // с прицелом цель должна оставаться под ним
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
        canvas.sortingOrder = UiLayers.OrbitCameraHud;
        var sc = chGo.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        sc.matchWidthOrHeight = 0.5f;

        var img = new GameObject("Reticle", typeof(RectTransform), typeof(Image));
        img.layer = 5;
        img.transform.SetParent(chGo.transform, false);
        chImg = img.GetComponent<Image>();
        chImg.sprite = HudPolishRoot.Skin != null ? HudPolishRoot.Skin.reticle : Reticle();
        chImg.raycastTarget = false;
        chRt = chImg.rectTransform;
        chRt.anchorMin = chRt.anchorMax = chRt.pivot = new Vector2(0.5f, 0.5f);
        chRt.anchoredPosition = Vector2.zero;
        chRt.sizeDelta = new Vector2(HudPolishRoot.Skin != null ? 38f : crosshairSize, HudPolishRoot.Skin != null ? 38f : crosshairSize);
        chGo.SetActive(false);
    }

    void UpdateCrosshair(float dt)
    {
        bool show = crosshairOn && (follow == null || !follow.LeoShotShowing);
        if (show) EnsureCrosshair();
        if (chGo == null) return;
        if (chGo.activeSelf != show) chGo.SetActive(show);
        if (!show) return;

        // на дереве, которое можно рубить, прицел светится золотом и чуть растёт
        bool hover = interaction != null && interaction.HoverItem != null;
        chT = Mathf.MoveTowards(chT, hover ? 1f : 0f, dt / 0.12f);
        chImg.color = Color.Lerp(Color.white, HudPolishRoot.Skin != null ? new Color32(164, 255, 213, 255) : new Color32(0xF5, 0xC5, 0x42, 255), chT);
        chRt.localScale = Vector3.one * Mathf.Lerp(1f, VibeSettings.ReduceEffects ? 1f : 1.18f, Mathf.SmoothStep(0f, 1f, chT));
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

    // Повторно используем готовый dithering из TreeToonPeek.hlsl.
    // Работает без Physics.Raycast, Materials/MPB и переключения Renderer.enabled.
    // Как и CameraFollow, меняет только отрисовку перекрывающих фрагментов,
    // оставляя ShadowCaster и физические взаимодействия нетронутыми.
    void UpdateTreePeek(bool cinematicReveal, float dt)
    {
        bool desired = treePeek && !cinematicReveal && cam != null && player != null;
        Vector3 vp = Vector3.zero;
        if (desired)
        {
            Vector3 chest = player.transform.position + Vector3.up * treePeekPlayerHeight;
            vp = cam.WorldToViewportPoint(chest);
            desired = vp.z > cam.nearClipPlane && vp.x > -0.5f && vp.x < 1.5f && vp.y > -0.5f && vp.y < 1.5f;
        }

        if (desired) treePeekLastViewport = vp;
        else vp = treePeekLastViewport; // без скачка окна в угол при плавном отключении

        treePeekBlend = Mathf.MoveTowards(treePeekBlend, desired ? 1f : 0f,
            dt / Mathf.Max(0.05f, treePeekFadeTime));
        if (treePeekBlend <= 0.001f)
        {
            ClearTreePeek();
            return;
        }

        // Радиус шейдера задан в долях высоты кадра. Согласуем его с
        // перспективной камерой по вертикальному FOV и глубине груди игрока.
        float viewHeight = 2f * Mathf.Max(0.1f, vp.z) *
            Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float radius = treePeekRadius / Mathf.Max(0.05f, viewHeight);

        Shader.SetGlobalVector(TreePeekDataId,
            new Vector4(vp.x, vp.y, radius, treePeekSoftness));
        Shader.SetGlobalVector(TreePeekParamsId,
            new Vector4(vp.z - treePeekDepthMargin, cam.aspect, treePeekBlend, treePeekMinOpacity));
    }

    // Ближайшее препятствие на луче (хит-дистанция) или +inf. Те же правила, что у камеры: мелочь, питомцы, игрок и собеседник не мешают
    float CastLimit(Vector3 origin, float radius, Vector3 dir, float castDist, Transform target, bool revealHold)
    {
        float limit = float.PositiveInfinity;
        int n = Physics.SphereCastNonAlloc(origin, radius, dir, hits, castDist, collisionMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var h = hits[i];
            var col = h.collider;
            if (col == null || h.distance <= 0.0001f) continue;          // уже внутри (например, земля под ногами)
            if (CameraVibe.DialogueShown > .01f && CameraVibe.DialoguePartner != null && col.transform.IsChildOf(CameraVibe.DialoguePartner)) continue;
            if (col.transform.IsChildOf(target)) continue;               // сам игрок
            if (col.attachedRigidbody != null) continue;                 // питомцы, рабочие
            if (col.bounds.size.magnitude < ignoreSmallerThan) continue; // мелочь
            // на показе само здание (и летящие детали) камеру не двигает: иначе она дёргается, пока облетает стройку
            if (revealHold && col.bounds.SqrDistance(CameraVibe.RevealPoint) < CameraVibe.RevealRadius * CameraVibe.RevealRadius) continue;
            if (h.distance < limit) limit = h.distance;
        }
        return limit;
    }

    // Смещение фокуса от головы героя (плечо, взгляд вперёд, подъём) обрезается у стены, чтобы камера не смотрела из-за неё
    Vector3 ClampFocusOffset(Vector3 pivot, Vector3 offset, Transform target)
    {
        float len = offset.magnitude;
        if (len < .05f) return offset;
        Vector3 dir = offset / len;
        float hit = CastLimit(pivot, .15f, dir, len + focusWallPadding, target, false);
        if (hit == float.PositiveInfinity) return offset;
        return dir * Mathf.Clamp(hit - focusWallPadding, 0f, len);
    }

    // Дуга в покое: смещение yaw 0 -> max -> 0 (только вид, yaw управления не меняется). Любое движение, ввод, эффект или стена отменяют её
    void TickIdleArc(bool calm, float dt, float wantDist)
    {
        arcCool = Mathf.Max(0f, arcCool - dt);
        if (!calm)
        {
            idleT = 0f;
            arcT = -1f;
        }
        else if (arcT < 0f)
        {
            idleT += dt;
            if (idleT >= idleArcDelay && arcCool <= 0f)
            {
                if (curDist >= wantDist - .15f && PickArcSide(wantDist)) arcT = 0f;
                else arcCool = 3f;   // нет места: проверим позже
            }
        }
        else
        {
            arcT += dt / Mathf.Max(1f, idleArcDuration);
            if (arcT >= 1f) { arcT = -1f; arcCool = idleArcCooldown; idleT = 0f; }
        }

        if (arcT >= 0f)
        {
            arcOffset = arcSide * idleArcAngle * .5f * (1f - Mathf.Cos(arcT * 2f * Mathf.PI));   // нулевая скорость на концах
            arcOffsetVel = 0f;
        }
        else arcOffset = Mathf.SmoothDamp(arcOffset, 0f, ref arcOffsetVel, Mathf.Max(.05f, idleArcCancelSmooth), Mathf.Infinity, dt);
    }

    // сторона дуги: чередуем; нужна свободная дорога на половине и на полном размахе, иначе пробуем другую
    bool PickArcSide(float wantDist)
    {
        float dist = Mathf.Max(minCamDistance, wantDist);
        for (int k = 0; k < 2; k++)
        {
            float sign = k == 0 ? -arcSide : arcSide;
            bool free = true;
            for (int j = 1; j <= 2 && free; j++)
            {
                Vector3 back = Quaternion.Euler(pitch, yaw + sign * idleArcAngle * j * .5f, 0f) * Vector3.back;
                free = CastLimit(focus, collisionRadius, back, dist + .3f, player.transform, false) > dist + collisionPadding + .05f;
            }
            if (free) { arcSide = sign; return true; }
        }
        return false;
    }

    void Step()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        // Cursor ownership is updated even when Settings pauses Time.timeScale.
        bool conversation = DialoguePreferences.ConversationOwnsCamera;
        if (conversation != wasConversation)
        {
            resumeLookAt = Time.unscaledTime + lookResumeDelay;
            if (conversation)
            {
                Vector3 d = DialogueSequence.Focus.position - player.transform.position; d.y = 0;
                float side = d.sqrMagnitude > .01f ? Vector3.Dot(d.normalized, Quaternion.Euler(0,yaw,0) * Vector3.right) : 1f;
                dialogueYawOffset = Mathf.Abs(side) < .4f ? (side < 0 ? -28f : 28f) : 0f;
            }
            wasConversation = conversation;
        }
        // ---------- ввод
        Vector2 delta = Vector2.zero;
        bool modal = MobileInput.ModalOpen || conversation || !Application.isFocused;
        // окно, диалог, показ стройки или возврат фокуса закончились: мышь оживает через короткую паузу
        if (wasModal && !modal) resumeLookAt = Mathf.Max(resumeLookAt, Time.unscaledTime + lookResumeDelay);
        wasModal = modal;
        bool altHeld = Input.GetKey(freeCursorKey) || Input.GetKey(KeyCode.RightAlt);
        bool mouseLook = MouseLook;

        // мышь крутит камеру: всегда (курсор скрыт; Alt, окна и пауза его освобождают) или только с зажатой ПКМ
        bool wantLock = !modal && Time.unscaledTime >= resumeLookAt && (mouseLook ? !altHeld : Input.GetMouseButton(1));
        bool lockChanged = wantLock != cursorHeld;
        if (lockChanged) SetCursorLocked(wantLock);
        crosshairOn = wantLock && mouseLook;   // прицел только в режиме «мышью» (с ПКМ курсор нужен как был)

        if (dt < .0001f) { UpdateCrosshair(Mathf.Min(Time.unscaledDeltaTime,.05f)); return; }

        if (!modal)
        {
            // кадр смены режима пропускаем: курсор «прыгает» в центр, это не движение мыши
            if (wantLock && !lockChanged)
            {
                delta += new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * (mouseSensitivity * PerformanceBoot.MouseMultiplier);
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
        // Мягкий режим столкновений нужен, пока показ идёт. В хвосте возврата (rv ещё не 0) работают обычные столкновения и окно в кронах
        bool revealHold = CameraVibe.RevealActive;

        // Зум колёсиком должен быть плавным. Без этого уменьшение distance
        // воспринималось как столкновение и камера мгновенно прыгала вперёд.
        manualDist = Mathf.SmoothDamp(manualDist, Mathf.Clamp(distance, distMin, distMax),
            ref manualDistVel, Mathf.Max(.01f, manualZoomSmooth), Mathf.Infinity, dt);

        // Лёгкий взгляд вперёд зависит от реальной скорости, а не от нажатой
        // клавиши. При разговоре, рубке и показе здания он плавно исчезает.
        Vector3 velocity = playerBody != null ? playerBody.linearVelocity : Vector3.zero;
        velocity.y = 0f;
        float speed = velocity.magnitude;
        float move01 = Mathf.Clamp01(speed / Mathf.Max(1f, lookAheadFullSpeed));
        // режим следования («стоим» / «бежим») меняется плавно: при остановке камера не подбегает рывком
        moveBlend = Mathf.Lerp(moveBlend, move01, 1f - Mathf.Exp(-dt / Mathf.Max(.05f, followStopBlend)));
        Vector3 wantedAhead = speed > .2f && !conversation && !reveal && !chopping
            ? velocity / speed * (movingLookAhead * move01) : Vector3.zero;
        ahead = Vector3.SmoothDamp(ahead, wantedAhead, ref aheadVel,
            Mathf.Max(.01f, lookAheadSmooth), Mathf.Infinity, dt);

        float wantDist = manualDist * Mathf.Max(0.3f, follow.zoomBias) * chopMul * introMul;
        if (reveal)
        {
            // дистанция, на которой здание целиком влезает в кадр при revealFov
            float r = CameraVibe.RevealRadius;
            float fitDist = r > 0.1f
                ? Mathf.Clamp(r * revealFit / Mathf.Tan(revealFov * 0.5f * Mathf.Deg2Rad), revealMinDist, distMax * 1.3f)
                : wantDist;
            wantDist = Mathf.Lerp(wantDist, fitDist, rv);
        }

        // ---------- дуга в покое: стоим, нет ввода и эффектов, вокруг свободно
        float movedSpeed = (player.transform.position - lastPlayerPos).magnitude / dt;
        lastPlayerPos = player.transform.position;
        bool anyInput = delta.sqrMagnitude > .0001f || Mathf.Abs(Input.mouseScrollDelta.y) > .01f || Input.anyKey || Input.touchCount > 0;
        bool calm = idleArc && !VibeSettings.ReduceEffects && !modal && !reveal && !chopping && shakeT <= 0f
                    && Mathf.Max(speed, movedSpeed) < .15f && !anyInput && introMul > .999f
                    && !CameraVibe.PeekActive && !CameraVibe.AccentActive && !follow.LeoShotShowing && CameraVibe.DialogueShown < .01f;
        TickIdleArc(calm, dt, wantDist);

        // ---------- точка, на которую смотрим (+ сдвиг от CameraVibe: говорящий, NPC, здание при показе)
        Transform target = player.transform;
        aimT = Mathf.MoveTowards(aimT, crosshairOn ? 1f : 0f, dt / 0.25f);
        float aim = Mathf.SmoothStep(0f, 1f, aimT);
        float dialogueMix = reveal || VibeSettings.ReduceEffects ? 0f : Mathf.Clamp01(CameraVibe.DialogueShown);
        float yawBias = follow.yawBias * (reveal ? Mathf.Lerp(1f, revealSweepMul, rv) : 1f) + dialogueYawOffset * dialogueMix + arcOffset;
        Vector3 shoulder = Quaternion.Euler(0f, yaw + yawBias, 0f) * Vector3.right * (shoulderOffset * aim);
        Vector3 pivot = target.position + focusOffset;
        Vector3 personal = ahead + shoulder + Vector3.up * (shoulderLift * aim);
        if (focusWallClamp && !reveal) personal = ClampFocusOffset(pivot, personal, target);   // плечо и взгляд вперёд не уводят фокус в стену
        Vector3 wantFocus = pivot + personal + follow.focusBias;
        float horizontalSmooth = Mathf.Lerp(followSmooth, movingFollowSmooth, moveBlend);
        float heightSmooth = verticalFollowSmooth;
        if (reveal)
        {
            horizontalSmooth = Mathf.Lerp(horizontalSmooth, revealFocusSmooth, rv);
            heightSmooth = Mathf.Lerp(heightSmooth, revealFocusSmooth, rv);
        }
        if (snapPending) { focus = wantFocus; focusVel = Vector3.zero; snapPending = false; }
        focus.x = Mathf.SmoothDamp(focus.x, wantFocus.x, ref focusVel.x, Mathf.Max(.01f, horizontalSmooth), Mathf.Infinity, dt);
        focus.z = Mathf.SmoothDamp(focus.z, wantFocus.z, ref focusVel.z, Mathf.Max(.01f, horizontalSmooth), Mathf.Infinity, dt);
        focus.y = Mathf.SmoothDamp(focus.y, wantFocus.y, ref focusVel.y, Mathf.Max(.01f, heightSmooth), Mathf.Infinity, dt);

        // yawBias: облёт здания при показе стадии. Только на вид, на управление не влияет
        float shownPitch = reveal ? Mathf.Lerp(pitch, revealPitch, rv) : Mathf.Lerp(pitch, Mathf.Clamp(pitch,14f,38f),dialogueMix);   // pitch игрока не трогаем: после показа вернётся
        Quaternion rot = Quaternion.Euler(shownPitch, yaw + yawBias, 0f);
        Vector3 back = rot * Vector3.back;

        // ---------- столкновения: камера подъезжает к игроку
        if (!revealHold) wantDist = Mathf.Max(minCamDistance, wantDist);
        // Во время плавного зума камера ещё может стоять дальше новой цели.
        // Проверяем весь этот отрезок, чтобы по дороге не оказаться за стеной.
        float castDist = revealHold ? wantDist : Mathf.Max(wantDist, curDist);
        float collisionLimit = castDist;
        float hitDist = CastLimit(focus, collisionRadius, back, castDist, target, revealHold);
        if (hitDist < float.PositiveInfinity) collisionLimit = Mathf.Min(castDist, hitDist - collisionPadding);
        // стена или ствол на пути дуги отменяют её: камера плавно возвращается
        if (arcT >= 0f && collisionLimit < Mathf.Min(wantDist, curDist) - .2f) { arcT = -1f; arcCool = idleArcCooldown * .5f; }
        float allowed = Mathf.Clamp(collisionLimit, 0f, wantDist);

        // Разделяем ЗУМ пользователя и защиту от столкновения:
        // камера отпрыгивает от стены сразу, но намеренный зум не телепортирует её.
        // Короткая задержка после выхода из-за ствола убирает постоянные
        // скачки «приблизилась — отдалилась» при движении по краю коллайдера.
        if (revealHold)
        {
            collisionHold = 0f;
            curDistVel = 0f;
            curDist = Mathf.Lerp(curDist, Mathf.Max(minCamDistance, allowed), 1f - Mathf.Exp(-3f * dt));
        }
        else if (collisionLimit < curDist)
        {
            collisionHold = collisionClearDelay;
            curDistVel = 0f;
            curDist = Mathf.Max(0f, collisionLimit);
        }
        else
        {
            bool blocked = allowed < wantDist - .03f;
            if (blocked) collisionHold = collisionClearDelay;
            else if (collisionHold > 0f)
            {
                collisionHold = Mathf.Max(0f, collisionHold - dt);
                if (allowed > curDist) allowed = curDist;
            }
            float smooth = allowed < curDist ? Mathf.Min(.12f, collisionReturnSmooth) : collisionReturnSmooth;
            curDist = Mathf.SmoothDamp(curDist, allowed, ref curDistVel,
                Mathf.Max(.01f, smooth), Mathf.Infinity, dt);
        }

        // ---------- тряска: поверх готового положения, чтобы не копилась
        Vector3 shake = Vector3.zero;
        if (shakeT > 0f)
        {
            shakeT -= dt;
            float f = Mathf.Clamp01(shakeT / shakeDur);
            Vector2 r = Random.insideUnitCircle * (shakeAmp * f * f);
            shake = rot * new Vector3(r.x, r.y, 0f);
        }

        float runFov = VibeSettings.ReduceEffects || chopping || conversation ? 0f : runFovBoost * moveBlend;
        cam.fieldOfView = reveal ? Mathf.Lerp(fov, revealFov, rv) : fov + runFov;
        cam.transform.SetPositionAndRotation(focus + back * curDist + shake, rot);

        bool userLook = delta.sqrMagnitude > .09f || Mathf.Abs(Input.mouseScrollDelta.y) > .01f ||
            Input.touchCount > 0 || introMul > 1.001f || modal;
        follow.ApplyLeoShot(target, focus, dt, userLook, true, collisionMask);

        UpdateTreePeek(revealHold, dt);
        UpdateCrosshair(dt);

        // движение персонажа теперь относительно камеры (без облёта yawBias)
        player.cameraYaw = yaw;
    }
}
