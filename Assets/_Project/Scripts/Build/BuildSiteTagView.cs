// Assets/_Project/Scripts/Build/BuildSiteTagView.cs
// Метка над площадкой стройки (Candy: светлое облачко с хвостиком, индиго-текст, мятные и персиковые полосы).
// Одна на всё: название, стадии, цена в брёвнах, таймер, подсказка.
// Файл разбит на части (partial): здесь поля, жизненный цикл, состояние, анимация и позиция; построение интерфейса и заполнение строк в BuildSiteTagView.Ui.cs.
//
//   ┌────────────────────────────────┐
//   │ Костёр                  ● ● ○  │   название и стадии (кружки)
//   │ ───────────────────────────────│
//   │ [бревно] ▓▓▓▓▓░░░░   4 +3 / 8  │   по строке на каждый ресурс: сдано, несёшь с собой, нужно
//   │ [доска]  ▓▓░░░░░░░   2 / 6     │
//   │ [монета] ▓▓▓▓▓▓▓▓▓  150 / 150  │
//   │      Принеси ещё 4 бревна      │   подсказка
//   └────────────────────────────────┘
//
//  * Состояние берётся из BuildingController (Locked / Next / InProgress / Done), метку никто не дёргает вручную.
//  * Стадии: «1/3», сегменты, «До стадии 2». Для обычных построек стадии прячутся.
//  * Метка сама встаёт на экране так, чтобы не налезать на HUD (HudSafeAreaRegistry) и не уезжала за край.
//  * Рисуется на своём канвасе НИЖЕ HUD, поэтому панели HUD всегда сверху.
//  * «Нужно ещё N» приходит событием BuildingController.NotEnoughWood.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(100)]      // после камеры
public partial class BuildSiteTagView : MonoBehaviour
{
    public enum State { Locked, Next, InProgress, Done }

    [Header("Здание")]
    public string buildingName = "Костёр";
    public Sprite buildingIcon;
    public string lockedAfter = "Костёр";

    [Header("Состояние (берётся из BuildingController, руками менять не нужно)")]
    public State state = State.Next;

    [Header("Положение")]
    public Vector3 offset = new Vector3(0f, 3.2f, 0f);
    [Tooltip("Общий размер метки на экране (1 = крупно, 0.75 = компактно)")]
    [Range(0.4f, 1.2f)] public float uiScale = 0.72f;
    [Tooltip("Компактная метка: только название и одна строка («Бревна 3/8»). Подробности (стадии, ресурсы, время, награда) показывает окно стройки (BuildWindow). " +
             "Работает, только если у BuildingController включено useWindow.")]
    public bool compact = true;
    public float doneVisibleTime = 2.5f;       // сколько висит «ГОТОВО» после окончания
    public float farDistance = 45f;            // дальше этого метка бледнеет и пропадает
    public float screenMargin = 14f;           // отступ от края экрана (единицы канваса)
    public float deadZone = 28f;               // метка стоит на месте, пока цель не сдвинулась дальше этого (px канваса)
    [Tooltip("Метка стоит в мире над зданием как настоящий объект: никуда не двигается, не крутится за камерой и не липнет к краям экрана. " +
             "Если выключить, вернётся старая метка поверх экрана.")]
    public bool worldSpace = true;
    [Tooltip("Размер метки в мире: единиц мира на один пиксель панели. Больше число = крупнее метка")]
    public float worldScale = 0.012f;
    [Tooltip("Насколько камера может уйти вбок, прежде чем метка плавно довернётся к ней (градусы). Меньше = метка чаще поворачивается, больше = дольше стоит неподвижно. Больше 80 нельзя: появится зеркальный текст")]
    [Range(10f, 80f)] public float faceThreshold = 30f;
    [Tooltip("Скорость доворота метки к камере")]
    public float faceTurnSpeed = 5f;
    [Tooltip("С этого расстояния до камеры метка имеет базовый размер; дальше растёт, ближе чуть уменьшается, чтобы текст читался и при отдалённой свободной камере")]
    public float readableDistance = 18f;
    [Tooltip("Статичная метка (только для режима поверх экрана): не ездит за камерой. Пока камера крутится или едет за игроком, метка стоит на экране как приклеенная. " +
             "Переезжает на новое место только когда камера успокоилась.")]
    public bool pinned = true;
    [Tooltip("Сколько секунд камера должна стоять спокойно, прежде чем метка можно переставить")]
    public float settleDelay = 0.6f;

    // ---------------------------------------------------------------- цвета (Candy: тёмные цвета на светлом поле)
    static readonly Color Good = UiCandy.Done;               // «можно сдать», «выполнено»
    static readonly Color Dim = UiCandy.Muted;               // второстепенный текст
    static readonly Color NodeDim = UiCandy.Lavender;        // кольцо будущей стадии
    const float TailW = 40f;                                 // ширина хвостика-указателя (единицы панели)

    // ---------------------------------------------------------------- ссылки
    BuildingController bc;
    PlayerInventory inv;
    Camera cam;

    RectTransform rt;
    CanvasGroup cg;
    Image frame, accent;
    TMP_Text title, hint, doneText, timerText;
    RectTransform segRoot, bar, hintRow, divider;
    Image barFill;
    // стадии: нумерованные «узлы» и звенья между ними
    RectTransform stageRow, targetNode;
    RectTransform infoRow, chipTime, chipXp;     // плашки: время стройки, награда, всего брёвен
    TMP_Text txtTime, txtXp;
    TMP_Text stageCaption;
    Image[] nodeRing = new Image[0], nodeFill = new Image[0], nodeCheck = new Image[0], links = new Image[0];
    TMP_Text[] nodeNum = new TMP_Text[0];

    // строка ресурса: иконка, полоска (сдано + несёшь с собой), счётчик
    class Row
    {
        public RectTransform root;
        public Image fill, preview;
        public TMP_Text count;
        public float shown, target, shownPreview, targetPreview;
    }
    Row rowWood, rowPlank, rowCoin;

    static RectTransform canvasRoot;
    static Canvas canvasCmp;
    static TMP_FontAsset fnt;

    // ---------------------------------------------------------------- динамика
    bool manualLocked;
    State lastState = (State)(-1);
    int lastInvKey = int.MinValue, lastNeedKey = int.MinValue, lastCur = -1, lastTot = -1, lastPct = -1;
    bool canContribute;
    float flashUntil;
    int flashMissing;
    float bannerUntil;
    string bannerText;
    float pulse, punch = 1f;
    float shown;                 // 0..1 видимость (плавно)
    float doneTimer = -1f;
    bool placed;
    Vector2 curPos;
    bool moving;
    Transform host;               // мировой холст этой метки (режим worldSpace)
    bool facingSet;               // метка уже развёрнута к камере хотя бы раз
    bool turning;                 // идёт плавный доворот
    Vector3 lastCamPos; Quaternion lastCamRot; float stillT;   // для режима pinned: как давно камера не двигается
    int pushSide;                // с какой стороны от панели HUD метка сейчас стоит (0 = ни с какой)

    // метка в «тонком» виде: подробности вынесены в окно стройки
    bool Compact { get { return compact && bc != null && bc.UseWindow; } }

    // ================================================================ жизненный цикл
    void Awake()
    {
        bc = GetComponent<BuildingController>();

        TryGetFont();
        BuildUI();
        inv = PlayerInventory.Instance;
        if (HudSafeAreaRegistry.Instance == null)
            new GameObject("HudSafeAreaRegistry").AddComponent<HudSafeAreaRegistry>();
    }

    void Start()
    {
        // UIManager мог проснуться позже метки: подхватываем шрифт теперь
        if (fnt == null && TryGetFont()) ApplyFont();
    }

    void OnEnable()
    {
        if (bc == null) bc = GetComponent<BuildingController>();
        if (bc != null)
        {
            bc.StageBuilt += OnStageBuilt;
            bc.NotEnoughWood += Flash;
        }
    }

    void OnDisable()
    {
        if (bc != null)
        {
            bc.StageBuilt -= OnStageBuilt;
            bc.NotEnoughWood -= Flash;
        }
    }

    void OnDestroy()
    {
        if (host != null) Destroy(host.gameObject);
        else if (rt != null) Destroy(rt.gameObject);
    }

    // Мгновенно развернуть метку к камере (например, после телепорта камеры).
    [ContextMenu("Reface To Camera")]
    public void RefaceToCamera() { facingSet = false; }

    // свой мировой холст на метку: стоит в точке здания, никуда не едет
    Transform CreateWorldHost()
    {
        var go = new GameObject("BuildTagWorld_" + buildingName, typeof(RectTransform), typeof(Canvas));
        go.layer = 5;
        var c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.WorldSpace;
        c.worldCamera = Camera.main;
        c.sortingOrder = UiLayers.WorldTag;
        var r = (RectTransform)go.transform;
        r.sizeDelta = new Vector2(400f, 400f);
        r.localScale = Vector3.one * worldScale;
        r.position = transform.position + offset;
        host = r;
        return r;
    }

    static bool TryGetFont()
    {
        if (fnt != null) return true;
        if (UIManager.Instance != null && UIManager.Instance.objectiveText != null)
            fnt = UIManager.Instance.objectiveText.font;
        return fnt != null;
    }

    void ApplyFont()
    {
        if (rt == null) return;
        var f = CandyTag.BestFont(fnt);        // круглый шрифт Candy главнее шрифта HUD
        if (f == null) return;
        foreach (var t in rt.GetComponentsInChildren<TMP_Text>(true)) t.font = f;
    }

    // метка дёргается и пишет «Нужно ещё N»
    public void Flash(int missing)
    {
        flashMissing = missing;
        flashUntil = Time.unscaledTime + 1.1f;
        punch = 0f;
        lastState = (State)(-1);
    }

    void OnStageBuilt()
    {
        if (bc != null && bc.IsFinished) return;     // для последнего этапа есть «ГОТОВО»
        bannerText = "<color=#2A9573>Стадия " + (bc != null ? bc.StageCurrent : 0) + " готова!</color>";
        bannerUntil = Time.unscaledTime + 1.8f;
        punch = 0f;
        lastState = (State)(-1);
    }

    // ================================================================ состояние
    State DeriveState()
    {
        if (bc == null) return state;
        if (bc.IsFinished) return State.Done;
        if (bc.IsBuilding) return State.InProgress;
        if (bc.IsLocked || manualLocked) return State.Locked;
        return State.Next;
    }

    void Update()
    {
        if (rt == null) return;
        if (inv == null) inv = PlayerInventory.Instance;

        state = DeriveState();

        int wood = inv != null ? inv.woodCount : 0;
        int planks = inv != null ? inv.planksCount : 0;
        int coins = inv != null ? inv.coinsCount : 0;
        int cur = bc != null ? bc.StageCurrent : 0;
        int tot = bc != null ? bc.StageTotal : 1;
        float prog = bc != null ? bc.Progress : 0f;
        int pct = Mathf.RoundToInt(prog * 100f);
        int needKey = (bc != null && inv != null) ? bc.NeedKey(inv) : 0;
        int invKey = wood * 1000003 + planks * 1009 + coins;

        if (tot != lastTot) { RebuildSegments(tot > 1 ? tot : 0); lastTot = tot; lastState = (State)(-1); }

        bool flashing = Time.unscaledTime < flashUntil;
        bool banner = Time.unscaledTime < bannerUntil;
        if (invKey != lastInvKey || needKey != lastNeedKey || cur != lastCur || state != lastState || (state == State.InProgress && pct != lastPct)
            || flashing || banner || (!flashing && flashUntil > 0f) || (!banner && bannerUntil > 0f))
        {
            if (!flashing) flashUntil = 0f;
            if (!banner) bannerUntil = 0f;
            Refresh(wood, planks, coins, cur, tot, prog);
            lastInvKey = invKey; lastNeedKey = needKey; lastCur = cur; lastState = state; lastPct = pct;
        }

        if (state == State.InProgress) barFill.fillAmount = prog;

        // плавное наполнение полосок
        float k = 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime);
        AnimRow(rowWood, k); AnimRow(rowPlank, k); AnimRow(rowCoin, k);
    }

    static void AnimRow(Row r, float k)
    {
        if (r == null || !r.root.gameObject.activeSelf) return;
        r.shown = Mathf.Lerp(r.shown, r.target, k);
        r.shownPreview = Mathf.Lerp(r.shownPreview, r.targetPreview, k);
        r.fill.fillAmount = r.shown;
        r.preview.fillAmount = r.shownPreview;
    }

    // позицию считаем в LateUpdate, когда камера уже встала на место
    void LateUpdate()
    {
        if (rt != null) Animate();
    }

    void SetRow(Component c, bool on)
    {
        if (c != null && c.gameObject.activeSelf != on) c.gameObject.SetActive(on);
    }

    // заполнить строку ресурса. have = сдано, carry = несёт с собой, total = нужно
    // ================================================================ анимация и позиция
    void Animate()
    {
        float dt = Time.unscaledDeltaTime;
        float scale = canvasCmp != null ? Mathf.Max(0.01f, canvasCmp.scaleFactor) : 1f;

        if (cam == null) cam = Camera.main;
        if (cam == null) { cg.alpha = 0f; return; }

        // камера двигается? (поворот, езда за игроком)
        Transform ct = cam.transform;
        bool camMoved = (ct.position - lastCamPos).sqrMagnitude > 0.0009f || Quaternion.Angle(ct.rotation, lastCamRot) > 0.2f;
        lastCamPos = ct.position; lastCamRot = ct.rotation;
        stillT = camMoved ? 0f : stillT + dt;

        Vector3 wp = transform.position + offset;
        Vector3 sp;
        if (!CameraFollow.TryWorldToScreenStable(wp, out sp)) sp = cam.WorldToScreenPoint(wp);
        bool front = sp.z > 0.1f;
        float dist = Vector3.Distance(cam.transform.position, wp);

        bool offScreen = !front || sp.x < -60f || sp.x > Screen.width + 60f || sp.y < -60f || sp.y > Screen.height + 60f;
        bool want = !offScreen && dist < farDistance;
        // название здания уже есть в окне стройки, в полоске с прогрессом и в плашке показа: мировую метку на это время прячем (раньше было два названия сразу)
        if (bc != null && (bc.IsRevealing || BuildWindow.IsOpenFor(bc))) want = false;
        if (state == State.Done)
        {
            doneTimer += dt;
            if (doneTimer > doneVisibleTime) want = false;
        }
        shown = Mathf.MoveTowards(shown, want ? 1f : 0f, dt / 0.2f);
        cg.alpha = shown * (state == State.Locked ? 0.75f : 1f);
        if (shown <= 0.001f) { placed = false; moving = false; return; }   // спрятана: при возвращении встанет заново, уже на актуальное место

        // пульс рамки, когда можно строить
        pulse += dt * 2.4f;
        bool ready = state == State.Next && canContribute;
        Color fc = new Color(1f, 1f, 1f, 0f);
        if (ready) { fc = UiCandy.Mint; fc.a = 0.45f + 0.45f * (0.5f + 0.5f * Mathf.Sin(pulse)); }
        else if (state == State.InProgress) { fc = UiCandy.Lemon; fc.a = 0.8f; }
        frame.color = Color.Lerp(frame.color, fc, 1f - Mathf.Exp(-10f * dt));
        if (targetNode != null) targetNode.localScale = Vector3.one * (1f + 0.14f * (0.5f + 0.5f * Mathf.Sin(pulse * 1.6f)));   // следующая стадия «дышит»

        punch = Mathf.Min(1f, punch + dt / 0.3f);
        float s = (worldSpace ? 1f : Mathf.Lerp(1f, 0.82f, Mathf.InverseLerp(14f, farDistance, dist))) * Mathf.Max(0.3f, uiScale);   // в мире размер уменьшает сама перспектива
        s *= 1f + 0.1f * Mathf.Sin(punch * Mathf.PI) * (1f - punch);
        float shake = (Time.unscaledTime < flashUntil) ? Mathf.Sin(Time.unscaledTime * 55f) * 4f : 0f;
        rt.localScale = Vector3.one * s * Mathf.Lerp(0.9f, 1f, UiDraw.EaseOutBack(shown));

        // мировая метка: стоит над зданием, один раз повернулась к камере и больше не вращается и не двигается
        if (worldSpace && host != null)
        {
            host.position = transform.position + offset;
            host.localScale = Vector3.one * worldScale * Mathf.Clamp(dist / Mathf.Max(1f, readableDistance), 0.8f, 2.2f);

            // Положение жёстко в мире. Поворот к камере: стоим, пока камера не ушла за порог, потом плавно довернуться.
            // Так метка не крутится при каждом движении камеры, но и обратной стороной (зеркальный текст) не станет.
            Quaternion faceRot = cam.transform.rotation;
            if (!facingSet) { host.rotation = faceRot; facingSet = true; turning = false; }
            else
            {
                float ang = Quaternion.Angle(host.rotation, faceRot);
                if (!turning && ang > faceThreshold) turning = true;
                if (turning)
                {
                    host.rotation = Quaternion.Slerp(host.rotation, faceRot, 1f - Mathf.Exp(-faceTurnSpeed * dt));
                    if (ang < 1.5f) turning = false;
                }
            }
            rt.anchoredPosition = new Vector2(shake, CandyTag.TailHeight(TailW));     // дрожит только при отказе («Нужно ещё N»); кончик хвостика стоит в точке над зданием
            return;
        }

        // позиция: над точкой, потом вытолкнуть из-под HUD и удержать на экране.
        // Опора метки - нижний край, поэтому смена текста её не двигает.
        Vector2 size = rt.rect.size * scale * s;
        Vector2 center = new Vector2(sp.x, sp.y + 6f * scale + size.y * 0.5f);

        var reg = HudSafeAreaRegistry.Instance;
        if (reg != null)
        {
            bool pushed = false;
            for (int i = 0; i < 3; i++)
            {
                Rect safe;
                if (!reg.GetSafeRect(center, size, out safe)) break;
                center = reg.PushOutOfSafe(center, safe, size, ref pushSide);
                pushed = true;
            }
            if (!pushed) pushSide = 0;
        }
        float m = screenMargin * scale;
        center.x = Mathf.Clamp(center.x, size.x * 0.5f + m, Screen.width - size.x * 0.5f - m);
        center.y = Mathf.Clamp(center.y, size.y * 0.5f + m, Screen.height - size.y * 0.5f - m);
        Vector2 target = center - new Vector2(0f, size.y * 0.5f);   // обратно к нижнему краю

        if (!placed) { curPos = target; placed = true; }
        else
        {
            // «спокойный» режим: метка стоит, пока цель не уйдёт за мёртвую зону, потом плавно переезжает
            bool canRetarget = !pinned || stillT >= settleDelay;
            if (pinned && !canRetarget) moving = false;                    // камера снова поехала: замираем
            if (!moving && canRetarget && (target - curPos).magnitude > deadZone * scale) moving = true;
            if (moving)
            {
                curPos = Vector2.Lerp(curPos, target, 1f - Mathf.Exp(-6f * dt));
                if ((target - curPos).magnitude < 1.5f * scale) moving = false;
            }
        }
        rt.position = new Vector3(curPos.x + shake * scale, curPos.y + CandyTag.TailHeight(TailW) * scale * s, 0f);   // хвостик внизу: поднимаем панель на его высоту
    }
}