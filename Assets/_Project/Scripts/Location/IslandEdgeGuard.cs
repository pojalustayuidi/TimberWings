using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

// Assets/_Project/Scripts/Location/IslandEdgeGuard.cs (НЕ в Editor)
// Падение с парящего острова. Добавляется сам: FloatingIsland.Awake вешает его на объект острова.
//
// Как это работает:
//   1. Пока игрок стоит на земле внутри острова (не ближе Safe Edge Margin к краю), раз в 0.2 с запоминается «безопасная точка».
//   2. Игрок шагнул за край: земли под ним нет, он ниже безопасной точки больше чем на Drop Depth. Управление отнимается,
//      игрок падает по инерции и гравитации. Камера отъезжает, по краям экрана наплывает облачная виньетка, звук падения.
//   3. Экран заливается светлым цветом облаков, поверх него появляется плашка «Вы упали!».
//   4. Под заливкой игрок переносится в ТОЧКУ ВОЗВРАЩЕНИЯ и мягко опускается на землю, камера возвращается на место.
//      Предметы и ресурсы не теряются.
//
// Точка возвращения (по порядку): поле Respawn Point → объект IslandRespawn в сцене → объект SPAWN_Player (его ставит генератор локации)
// → последняя безопасная точка → центр острова. Чтобы всегда падать в одно и то же место, поставь пустой объект IslandRespawn
// куда нужно (высоту подбирать не надо, земля ищется лучом вниз). Хочешь возвращаться туда, откуда упал: сними Use Fixed Respawn.
//
// Страховка от багов: тот же путь сработает при рывке через край, загрузке рядом с краем и любом провале под остров.
//
// Из кода: IslandEdgeGuard.Instance.ForceRespawn();  (например, из отладки)
//   ForceRespawn(caption, title, subtitle, tip) делает то же самое с другими надписями на плашке (так возвращает WaterSwim после утопления).
[DisallowMultipleComponent]
public class IslandEdgeGuard : MonoBehaviour
{
    public static IslandEdgeGuard Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Instance = null; vignetteSprite = null; }

    [Header("Падение")]
    [Tooltip("Насколько ниже безопасной точки должен оказаться игрок без земли под ногами, чтобы считалось падением")]
    public float dropDepth = 1.2f;
    [Tooltip("Сколько секунд игрок падает на глазах, потом начинается заливка экрана")]
    public float fallBeforeFade = 0.95f;
    [Tooltip("Длительность заливки экрана, секунды")]
    public float fadeOutSeconds = 0.6f;
    [Tooltip("Цвет заливки: светлый тёплый, как облака на закате")]
    public Color fadeColor = new Color(1f, 0.95f, 0.84f, 1f);
    [Tooltip("Во сколько раз камера отъезжает за время падения (1 = не отъезжает)")]
    public float fallZoomOut = 1.3f;

    [Header("Точка возвращения")]
    [Tooltip("Всегда возвращать в одну точку. Выключи, чтобы возвращать на последнее безопасное место у края")]
    public bool useFixedRespawn = true;
    [Tooltip("Точка возвращения. Пусто = ищем объект IslandRespawn, потом SPAWN_Player")]
    public Transform respawnPoint;
    [Tooltip("С какой высоты игрок мягко опускается, метры")]
    public float respawnHeight = 6f;
    [Tooltip("Не ближе скольки метров от края запоминается безопасная точка")]
    public float safeEdgeMargin = 3f;

    [Header("Возвращение")]
    [Tooltip("Сколько секунд игрок опускается")]
    public float landSeconds = 1.1f;
    [Tooltip("Сколько секунд экран держится залитым, пока читается плашка «Вы упали»")]
    public float holdSeconds = 1.6f;
    [Tooltip("Сколько секунд экран проясняется")]
    public float fadeInSeconds = 0.9f;

    [Header("Надписи")]
    public string caption = "ПАДЕНИЕ";
    public string title = "Вы упали!";
    public string subtitle = "Облака подхватили вас и вернули на остров";
    [Tooltip("Маленькая подсказка под надписью, выбирается случайно. Пусто = без подсказки")]
    public string[] tips =
    {
        "Ресурсы и вещи остались при вас",
        "У края острова лучше смотреть под ноги",
        "Облака ловят всех, но не любят, когда к ним спешат",
    };

    enum State { Idle, Falling, Returning }
    State state;

    IslandSource island;
    List<Vector2> loop;
    PlayerMovement player;
    Rigidbody rb;
    CameraFollow cam;

    Vector3 safe;
    bool hasSafe;
    float nextSample;
    float pivotOffset = 0.05f;   // насколько pivot игрока выше земли (замеряется, пока игрок стоит)
    int fallCount;
    // надписи на один раз (ForceRespawn с текстами): после показа плашки сбрасываются
    string ovCaption, ovTitle, ovSubtitle, ovTip;
    float baseZoom = 1f;   // зум камеры до падения (его возвращаем)

    // UI
    GameObject uiRoot;
    CanvasGroup vignette, cover, card;
    Image coverImage, vignetteImage;
    RectTransform cardRect;
    TMP_Text titleText, captionText, subtitleText, tipText, countText;
    Image barFill;
    static Sprite vignetteSprite;
    static readonly RaycastHit[] hitBuf = new RaycastHit[16];

    public bool IsFalling { get { return state != State.Idle; } }

    void Awake() { Instance = this; }

    void OnEnable()
    {
        SaveSystem.Loaded -= OnLoaded; SaveSystem.Loaded += OnLoaded;
        SaveSystem.NewGameStarted -= OnLoaded; SaveSystem.NewGameStarted += OnLoaded;
    }

    void OnDisable()
    {
        SaveSystem.Loaded -= OnLoaded;
        SaveSystem.NewGameStarted -= OnLoaded;
        // выключили посреди падения: не оставляем игрока без управления
        if (state != State.Idle) Release();
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    // загрузка перенесла игрока: старая безопасная точка уже не нужна
    void OnLoaded() { hasSafe = false; nextSample = 0f; }

    bool Init()
    {
        if (player == null)
        {
            player = PlayerMovement.Instance;
            if (player == null) return false;
            rb = player.GetComponent<Rigidbody>();
        }
        if (loop == null)
        {
            island = IslandSource.For(gameObject);
            if (island == null) return false;
            loop = island.GetEdgeLoop();
            if (loop == null || loop.Count < 3) { loop = null; return false; }
        }
        return true;
    }

    void Update()
    {
        if (state != State.Idle) return;
        if (!Init()) return;
        if (SaveSystem.IsLoading) { hasSafe = false; return; }

        Vector3 p = player.transform.position;

        if (Time.time >= nextSample)
        {
            nextSample = Time.time + 0.2f;
            SampleSafe(p);
        }

        // падение: земли под ногами нет и мы заметно ниже безопасной точки (или улетели совсем глубоко)
        float refY = hasSafe ? safe.y : island.groundY;
        if (p.y < refY - 60f || (p.y < refY - dropDepth && !Grounded(p, 3.5f)))
            StartCoroutine(FallRoutine());
    }

    // ---------- безопасная точка ----------
    void SampleSafe(Vector3 p)
    {
        if (player.ControlLocked) return;
        Vector2 q = new Vector2(p.x, p.z);
        if (!Inside(q)) return;
        if (EdgeDistance(q) < safeEdgeMargin) return;
        WaterVolume.Hit wh;
        if (WaterVolume.TrySample(p, out wh) && wh.submersion > 0.1f) return;   // дно реки не безопасная точка
        RaycastHit h;
        if (!GroundHit(p + Vector3.up * 0.5f, 2.1f, out h)) return;
        pivotOffset = Mathf.Clamp(p.y - h.point.y, 0.02f, 2f);
        safe = p;
        hasSafe = true;
    }

    // есть ли под игроком земля (коллайдер, не он сам) в пределах length метров
    bool Grounded(Vector3 p, float length)
    {
        RaycastHit h;
        return GroundHit(p + Vector3.up * 0.5f, length + 0.5f, out h);
    }

    // ближайшая земля вниз от точки from: не триггер, не игрок, не питомцы и рабочие
    bool GroundHit(Vector3 from, float length, out RaycastHit best)
    {
        best = default(RaycastHit);
        int n = Physics.RaycastNonAlloc(from, Vector3.down, hitBuf, length, ~0, QueryTriggerInteraction.Ignore);
        float bd = float.MaxValue;
        bool found = false;
        for (int i = 0; i < n; i++)
        {
            var c = hitBuf[i].collider;
            if (c == null || c.isTrigger) continue;
            if (player != null && c.transform.IsChildOf(player.transform)) continue;
            if (c.attachedRigidbody != null) continue;   // питомцы и рабочие не земля
            if (hitBuf[i].distance < bd) { bd = hitBuf[i].distance; best = hitBuf[i]; found = true; }
        }
        return found;
    }

    bool Inside(Vector2 p)
    {
        bool inside = false;
        for (int i = 0, j = loop.Count - 1; i < loop.Count; j = i++)
        {
            Vector2 a = loop[i], b = loop[j];
            if (((a.y > p.y) != (b.y > p.y)) && (p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x))
                inside = !inside;
        }
        return inside;
    }

    float EdgeDistance(Vector2 p)
    {
        float best = float.MaxValue;
        for (int i = 0; i < loop.Count; i++)
        {
            Vector2 a = loop[i], b = loop[(i + 1) % loop.Count];
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            float d = (p - (a + ab * t)).sqrMagnitude;
            if (d < best) best = d;
        }
        return Mathf.Sqrt(best);
    }

    Vector3 FallbackPoint()
    {
        Vector2 c = Vector2.zero;
        for (int i = 0; i < loop.Count; i++) c += loop[i];
        c /= loop.Count;
        float y = island.groundY;
        var t = Terrain.activeTerrain;
        if (t != null) y = t.SampleHeight(new Vector3(c.x, 0f, c.y)) + t.GetPosition().y;
        return new Vector3(c.x, y + pivotOffset, c.y);
    }

    // ---------- точка возвращения ----------
    Transform FindRespawnMarker()
    {
        if (respawnPoint != null) return respawnPoint;
        var go = GameObject.Find("IslandRespawn");
        if (go == null) go = GameObject.Find("SPAWN_Player");
        return go != null ? go.transform : null;
    }

    // Куда вернуть игрока: фиксированная точка (если она на острове и под ней есть земля), иначе последняя безопасная
    Vector3 ResolveTarget()
    {
        if (useFixedRespawn)
        {
            var m = FindRespawnMarker();
            if (m != null && Inside(new Vector2(m.position.x, m.position.z)))
            {
                float startY = Mathf.Max(m.position.y, island.groundY) + 6f;
                RaycastHit h;
                if (GroundHit(new Vector3(m.position.x, startY, m.position.z), 60f, out h))
                    return h.point + Vector3.up * pivotOffset;
                return m.position + Vector3.up * pivotOffset;
            }
        }
        return hasSafe ? safe : FallbackPoint();
    }

    // ---------- падение и возвращение ----------
    public void ForceRespawn()
    {
        if (state == State.Idle && Init()) StartCoroutine(FallRoutine());
    }

    // то же с своими надписями (утопление и т.п.). true = возвращение запущено, false = занято или нет острова
    public bool ForceRespawn(string capStr, string titleStr, string subtitleStr, string tipStr)
    {
        if (state != State.Idle || !Init()) return false;
        ovCaption = capStr; ovTitle = titleStr; ovSubtitle = subtitleStr; ovTip = tipStr;
        StartCoroutine(FallRoutine());
        return true;
    }

    static float EaseOut(float k) { k = Mathf.Clamp01(k); return 1f - (1f - k) * (1f - k) * (1f - k); }
    static float EaseOutBack(float k)
    {
        k = Mathf.Clamp01(k);
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(k - 1f, 3f) + c1 * Mathf.Pow(k - 1f, 2f);
    }

    IEnumerator FallRoutine()
    {
        state = State.Falling;
        if (ovTitle == null) fallCount++;   // утопление в счётчик падений не идёт
        player.ControlLocked = true;
        player.CancelWalk();
        SfxManager.Play("fall", 0.9f, 1f);
        CameraFollow.Shake(0.05f, 0.3f);

        EnsureUi();
        PrepareCard();
        if (cam == null) cam = Object.FindFirstObjectByType<CameraFollow>();
        baseZoom = cam != null ? cam.zoomBias : 1f;

        // 1) падаем на глазах: камера отъезжает, по краям экрана наплывает облачная дымка
        float fall = Mathf.Max(0.1f, fallBeforeFade);
        for (float t = 0f; t < fall; t += Time.deltaTime)
        {
            float k = t / fall;
            vignette.alpha = Mathf.SmoothStep(0f, 0.85f, k);
            if (cam != null) cam.zoomBias = baseZoom * Mathf.Lerp(1f, fallZoomOut, EaseOut(k));
            yield return null;
        }

        // 2) заливка экрана облаком
        float fo = Mathf.Max(0.05f, fadeOutSeconds);
        for (float t = 0f; t < fo; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / fo);
            cover.alpha = k;
            vignette.alpha = Mathf.Lerp(0.85f, 0f, k);
            yield return null;
        }
        cover.alpha = 1f;
        vignette.alpha = 0f;

        // 3) под заливкой: переносим игрока над точкой возвращения
        state = State.Returning;
        Vector3 target = ResolveTarget();
        Vector3 start = target + Vector3.up * respawnHeight;
        var prevInterp = RigidbodyInterpolation.None;
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            prevInterp = rb.interpolation;
            rb.interpolation = RigidbodyInterpolation.None;
            rb.isKinematic = true;
            rb.position = start;
        }
        player.transform.position = start;
        Physics.SyncTransforms();
        if (cam != null) cam.zoomBias = baseZoom * fallZoomOut;
        CameraFollow.SnapToTarget();

        // плашка «Вы упали!» выезжает на облачной заливке
        float hold = Mathf.Max(0.3f, holdSeconds);
        const float cardIn = 0.45f, cardOut = 0.4f;
        for (float t = 0f; t < hold; t += Time.deltaTime)
        {
            float k = EaseOutBack(t / cardIn);
            card.alpha = Mathf.Clamp01(t / (cardIn * 0.8f));
            cardRect.localScale = Vector3.one * Mathf.LerpUnclamped(0.82f, 1f, k);
            barFill.fillAmount = Mathf.SmoothStep(0f, 1f, t / hold);
            yield return null;
        }
        barFill.fillAmount = 1f;
        card.alpha = 1f;
        cardRect.localScale = Vector3.one;

        // 4) экран проясняется, плашка тает, игрок мягко опускается, камера возвращается
        float total = Mathf.Max(landSeconds, fadeInSeconds);
        for (float t = 0f; t < total; t += Time.deltaTime)
        {
            float kf = Mathf.SmoothStep(0f, 1f, t / Mathf.Max(0.05f, fadeInSeconds));
            cover.alpha = 1f - kf;
            card.alpha = 1f - Mathf.Clamp01(t / cardOut);
            float k = EaseOut(t / Mathf.Max(0.05f, landSeconds));
            Vector3 pos = Vector3.Lerp(start, target, k);
            if (rb != null) rb.position = pos;
            player.transform.position = pos;
            if (cam != null) cam.zoomBias = Mathf.Lerp(baseZoom * fallZoomOut, baseZoom, Mathf.SmoothStep(0f, 1f, t / total));
            yield return null;
        }
        cover.alpha = 0f;
        card.alpha = 0f;
        if (cam != null) cam.zoomBias = baseZoom;
        if (rb != null) rb.position = target;
        player.transform.position = target;

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.interpolation = prevInterp;
            rb.linearVelocity = Vector3.zero;
        }
        player.ControlLocked = false;
        state = State.Idle;
        nextSample = Time.time + 0.5f;
        hasSafe = hasSafe && Inside(new Vector2(safe.x, safe.z));

        CameraFollow.Shake(0.06f, 0.15f);
        SfxManager.Play("hint", 0.6f, 0.8f);
    }

    void Release()
    {
        StopAllCoroutines();
        if (cover != null) cover.alpha = 0f;
        if (vignette != null) vignette.alpha = 0f;
        if (card != null) card.alpha = 0f;
        if (cam != null) cam.zoomBias = baseZoom;
        if (rb != null && rb.isKinematic) rb.isKinematic = false;
        if (player != null) player.ControlLocked = false;
        state = State.Idle;
    }

    // ---------- интерфейс падения ----------
    static Sprite Vignette()
    {
        if (vignetteSprite != null) return vignetteSprite;
        const int N = 128;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "IslandFallVignette" };
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.4f, 1.25f, d));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply(false, true);
        vignetteSprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
        return vignetteSprite;
    }

    static RectTransform Stretch(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        if (rt == null) rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        return rt;
    }

    CanvasGroup MakeLayer(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Stretch(go);
        var g = go.AddComponent<CanvasGroup>();
        g.alpha = 0f; g.blocksRaycasts = false; g.interactable = false;
        return g;
    }

    void EnsureUi()
    {
        if (uiRoot != null) { SetUiColors(); return; }

        uiRoot = new GameObject("IslandFall_UI");
        uiRoot.transform.SetParent(transform, false);
        var cv = uiRoot.AddComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = UiLayers.Fade;
        var sc = uiRoot.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920f, 1080f);
        sc.matchWidthOrHeight = 0.5f;

        // виньетка по краям экрана (во время падения)
        vignette = MakeLayer("Vignette", uiRoot.transform);
        var vgo = new GameObject("Image", typeof(RectTransform));
        vgo.transform.SetParent(vignette.transform, false);
        Stretch(vgo);
        vignetteImage = vgo.AddComponent<Image>();
        vignetteImage.sprite = Vignette();
        vignetteImage.raycastTarget = false;

        // сплошная облачная заливка
        cover = MakeLayer("Cover", uiRoot.transform);
        var cgo = new GameObject("Color", typeof(RectTransform));
        cgo.transform.SetParent(cover.transform, false);
        Stretch(cgo);
        coverImage = cgo.AddComponent<Image>();
        coverImage.raycastTarget = false;

        // окно «Вы упали!» в стиле остального интерфейса: деревянная рама, латунный медальон, тёмная вставка, голубая полоса
        card = MakeLayer("Card", uiRoot.transform);
        var rgo = new GameObject("Box", typeof(RectTransform));
        rgo.transform.SetParent(card.transform, false);
        cardRect = (RectTransform)rgo.transform;
        cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(900f, 460f);
        cardRect.anchoredPosition = new Vector2(0f, 20f);

        var win = UiKit.MakePanel(cardRect, new Vector2(900f, 380f), true, 1.2f, "Window");
        win.rectTransform.anchoredPosition = new Vector2(0f, -20f);

        // латунный медальон со стрелкой вниз на верхней кромке
        var medal = UiKit.MakeImage(cardRect, "Medal", UiKit.Medallion, false);
        medal.rectTransform.sizeDelta = new Vector2(136f, 136f);
        medal.rectTransform.anchoredPosition = new Vector2(0f, 168f);
        var arrow = UiKit.MakeIcon(medal.transform, UiIcon.ArrowUp, 68f, "Arrow");
        arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 180f);

        captionText = AddText(cardRect, "Caption", 26f, UiKit.BrassHi, new Vector2(0f, 82f), new Vector2(800f, 36f), true);
        captionText.characterSpacing = 10f;
        titleText = AddText(cardRect, "Title", 84f, Color.white, new Vector2(0f, 28f), new Vector2(840f, 100f), true);

        var line = new GameObject("Line", typeof(RectTransform));
        line.transform.SetParent(cardRect, false);
        var lrt = (RectTransform)line.transform;
        lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0.5f);
        lrt.sizeDelta = new Vector2(420f, 5f);
        lrt.anchoredPosition = new Vector2(0f, -30f);
        var lim = line.AddComponent<Image>();
        lim.color = UiKit.Brass; lim.raycastTarget = false;

        var inset = UiKit.MakePanel(cardRect, new Vector2(780f, 118f), false, 0.6f, "Inset");
        inset.rectTransform.anchoredPosition = new Vector2(0f, -100f);
        subtitleText = AddText(cardRect, "Subtitle", 34f, Color.white, new Vector2(0f, -78f), new Vector2(740f, 48f), true);
        tipText = AddText(cardRect, "Tip", 26f, UiKit.Warn, new Vector2(0f, -122f), new Vector2(740f, 38f), false);

        // «облака несут вас назад»: полоса заполняется, пока читается окно
        UiKit.MakeBar(cardRect, UiBar.Blue, new Vector2(520f, 32f), out barFill, 1f).rectTransform.anchoredPosition = new Vector2(0f, -188f);
        countText = AddText(cardRect, "Count", 22f, new Color(1f, 1f, 1f, 0.7f), new Vector2(0f, -226f), new Vector2(740f, 30f), false);

        SetUiColors();
    }

    static TMP_Text AddText(RectTransform parent, string name, float size, Color color, Vector2 pos, Vector2 box, bool bold)
    {
        var t = UiKit.MakeLabel(parent, "", size, color, TextAlignmentOptions.Center, bold);
        t.gameObject.name = name;
        if (bold)
        {
            t.outlineWidth = 0.22f;
            t.outlineColor = new Color32(0x0B, 0x10, 0x20, 255);
        }
        var rt = (RectTransform)t.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = box;
        rt.anchoredPosition = pos;
        return t;
    }

    void SetUiColors()
    {
        if (coverImage != null) coverImage.color = fadeColor;
        if (vignetteImage != null) vignetteImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 1f);
    }

    void PrepareCard()
    {
        bool custom = ovTitle != null;
        captionText.text = custom ? ovCaption : caption;
        titleText.text = custom ? ovTitle : title;
        subtitleText.text = custom ? ovSubtitle : subtitle;
        barFill.fillAmount = 0f;
        string tip = custom ? (ovTip ?? "") : ((tips != null && tips.Length > 0) ? tips[Random.Range(0, tips.Length)] : "");
        ovCaption = ovTitle = ovSubtitle = ovTip = null;
        tipText.text = tip;
        tipText.gameObject.SetActive(!string.IsNullOrEmpty(tip));
        bool showCount = !custom && fallCount > 1;
        countText.text = showCount ? "Падений за сессию: " + fallCount : "";
        countText.gameObject.SetActive(showCount);
        card.alpha = 0f;
        cardRect.localScale = Vector3.one * 0.82f;
        SetUiColors();
    }
}
