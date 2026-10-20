// Assets/_Project/Scripts/Tutorial/QuestGuide.cs (НЕ в Editor)
// «Если человек тупит»: следит, идёт ли игрок к цели, и ведёт его мягко, без экранов с текстом (GDD 10.4, столп «Читаемость»).
// Цель берётся из QuestEdgeArrow.Target, ставится она так же, как раньше (IntroFlow, StoryFlow).
//
// Прогрессом считается: поменялся текст цели (счётчик 3/8, новый шаг), поменялись бревна или доски в рюкзаке,
// крупный скачок монет, игрок стал ближе к цели на 3 м. Пока идёт разговор или игра на паузе, таймер стоит.
//   нет прогресса hintAfter (20 с)  -> плашка внизу с подсказкой именно под эту цель (+ взгляд камеры к цели, если она за кадром)
//   нет прогресса trailAfter (40 с) -> ещё и золотая дорожка из шевронов по земле от героя к цели
//   дальше раз в repeatEvery (30 с) повтор дорожки
//   клавиша H в любой момент: плашка + дорожка сразу
// Выключается в настройках: VibeSettings.GuideEnabled (H работает всегда).
// Дорожка идёт по прямой: NavMesh в проекте нет. Если на пути река или обрыв, шевроны всё равно показывают сторону.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class QuestGuide : MonoBehaviour
{
    public static QuestGuide Instance { get; private set; }

    [Header("Когда подсказывать")]
    public KeyCode helpKey = KeyCode.H;
    public float hintAfter = 20f;
    public float trailAfter = 40f;
    public float repeatEvery = 30f;

    [Header("Дорожка")]
    public int markCount = 16;
    public float spacing = 1.7f;
    public float trailLength = 26f;
    public float trailSeconds = 8f;
    public float groundOffset = 0.1f;

    // слежение
    Transform player, lastTarget;
    PlayerInventory inv;
    BackpackTag pack;
    Camera cam;
    float stuck, bestDist = -1f, pollAt, findAt;
    int level;
    bool peeked, invInit;
    int lastWood, lastPlanks, lastCoins;
    string lastObj;

    // дорожка
    GameObject trailRoot;
    SpriteRenderer[] marks;
    float trailT, trailAlpha;
    static readonly RaycastHit[] hits = new RaycastHit[8];

    // плашка
    RectTransform canvasRt, panel;
    CanvasGroup toastGroup;
    TMP_Text toastMain, toastSub;
    float toastUntil, toastK, subAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { Instance = null; }

    void Awake() { Instance = this; }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (canvasRt != null) Destroy(canvasRt.gameObject);
        if (trailRoot != null) Destroy(trailRoot);
    }

    // ------------------------------------------------------------ каждый кадр
    void Update()
    {
        float dt = Time.deltaTime;
        Transform tgt = QuestEdgeArrow.Target;

        if (player == null || inv == null) FindRefs();
        bool active = tgt != null && tgt.gameObject.activeInHierarchy && player != null;

        if (!active)
        {
            lastTarget = null;
            ResetStuck();
            bestDist = -1f;
            trailT = 0f;
        }
        else
        {
            if (tgt != lastTarget)
            {
                lastTarget = tgt;
                ResetStuck();
                bestDist = -1f;
                peeked = false;
            }
            Track(tgt, dt);
            if (Input.GetKeyDown(helpKey)) ShowHelp(tgt, true, true);
        }

        TickTrail(tgt, active, dt);
        TickToast();
    }

    void FindRefs()
    {
        if (Time.unscaledTime < findAt) return;
        findAt = Time.unscaledTime + 1f;
        var pm = PlayerMovement.Instance;
        if (pm != null)
        {
            player = pm.transform;
            if (inv == null) inv = pm.GetComponent<PlayerInventory>();
            if (pack == null) pack = pm.GetComponent<BackpackTag>();
        }
    }

    void ResetStuck() { stuck = 0f; level = 0; }

    // ------------------------------------------------------------ слежение за прогрессом
    void Track(Transform tgt, float dt)
    {
        // разговор, пауза: игрок не завис, он занят
        if (DialogueManager.IsBusy || DialogueSequence.FreezesPlayer || Time.timeScale <= 0.001f) return;

        if (Time.unscaledTime >= pollAt)
        {
            pollAt = Time.unscaledTime + 0.25f;
            Poll();
        }

        float dist = FlatDist(tgt);
        if (bestDist < 0f) bestDist = dist;
        else if (dist < bestDist - 3f)
        {
            // идёт к цели: он и так справляется, подсказки откладываем
            bestDist = dist;
            stuck = Mathf.Min(stuck, hintAfter * 0.3f);
        }
        else if (dist > bestDist + 10f) bestDist = dist;      // ушёл далеко в сторону: отсчёт от новой точки

        stuck += dt;
        if (!VibeSettings.GuideEnabled) return;

        if (level == 0 && stuck >= hintAfter) { level = 1; ShowHelp(tgt, false, false); }
        else if (level == 1 && stuck >= trailAfter) { level = 2; ShowHelp(tgt, true, false); }
        else if (level >= 2 && stuck >= trailAfter + repeatEvery) { stuck = trailAfter; ShowHelp(tgt, true, false); }
    }

    void Poll()
    {
        // цель в HUD поменялась: игрок что-то сделал или квест перешёл на следующий шаг
        string o = null;
        if (UIManager.Instance != null && UIManager.Instance.objectiveText != null) o = UIManager.Instance.objectiveText.text;
        if (o != lastObj) { lastObj = o; ResetStuck(); }

        if (inv == null) return;
        if (!invInit)
        {
            invInit = true;
            lastWood = inv.woodCount; lastPlanks = inv.planksCount; lastCoins = inv.coinsCount;
            return;
        }
        // монеты считаем только крупным скачком (продажа, награда, покупка): тонкий ручеёк дохода бригады не должен гасить подсказки
        bool coinJump = Mathf.Abs(inv.coinsCount - lastCoins) >= 5;
        if (inv.woodCount != lastWood || inv.planksCount != lastPlanks || coinJump) ResetStuck();
        lastWood = inv.woodCount; lastPlanks = inv.planksCount; lastCoins = inv.coinsCount;
    }

    float FlatDist(Transform tgt)
    {
        Vector3 d = tgt.position - player.position;
        d.y = 0f;
        return d.magnitude;
    }

    // ------------------------------------------------------------ показать помощь
    void ShowHelp(Transform tgt, bool withTrail, bool manual)
    {
        float dist = FlatDist(tgt);
        if (withTrail) trailT = trailSeconds;

        bool onScreen = OnScreen(tgt);
        if (toastMain == null) BuildToast();
        toastMain.text = Advice(tgt, dist);
        toastSub.text = "Цель: " + Mathf.RoundToInt(dist) + " м" + (onScreen ? "" : ", " + Side(tgt)) + "   ·   " + helpKey + ": показать путь";
        toastUntil = Time.unscaledTime + (withTrail ? 7f : 6f);
        SfxManager.Play("hint", 0.7f);

        // цель далеко за кадром: камера на пару секунд заглядывает в её сторону
        if (!onScreen && dist > 8f && (manual || !peeked))
        {
            peeked = true;
            CameraVibe.Peek(tgt.position);
        }
    }

    bool OnScreen(Transform tgt)
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return true;
        Vector3 s = cam.WorldToScreenPoint(tgt.position);
        return s.z > 0f && s.x > 40f && s.x < Screen.width - 40f && s.y > 40f && s.y < Screen.height - 40f;
    }

    static readonly string[] SideNames = { "вверху", "вверху справа", "справа", "внизу справа", "внизу", "внизу слева", "слева", "вверху слева" };

    // сторона цели относительно экрана
    string Side(Transform tgt)
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return "";
        Vector3 s = cam.WorldToScreenPoint(tgt.position);
        Vector2 d = new Vector2(s.x - Screen.width * 0.5f, s.y - Screen.height * 0.5f);
        if (s.z < 0f) d = -d;
        float ang = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;      // от «вверх» по часовой
        int sector = Mathf.RoundToInt(ang / 45f);
        sector = ((sector % 8) + 8) % 8;
        return SideNames[sector];
    }

    // совет под тип цели: что это и что с ним делать прямо сейчас
    string Advice(Transform t, float dist)
    {
        var sp = t.GetComponentInParent<Speaker>();
        if (sp != null)
            return dist < 4f ? "Ты рядом с " + sp.displayName + ": нажми <color=#6BCB5A>E</color>, чтобы поговорить"
                             : "Иди к " + sp.displayName + ": золотая метка покажет дорогу. Рядом нажми <color=#6BCB5A>E</color>";

        var ws = t.GetComponentInParent<PlankWorkshop>();
        if (ws != null)
            return "Пила у хижины: подойди и нажми <color=#6BCB5A>E</color>, закинь брёвна и забери доски";

        var b = t.GetComponentInParent<BuildingController>();
        if (b != null)
        {
            if (b.IsLocked) return "Здесь пока закрыто: выполни текущее задание";
            // режим окна: ресурсы сдаются в окне стройки, а не сами
            if (b.UseWindow && (b.AllDeposited || (inv != null && b.PlayerCanContribute(inv))))
                return dist < 5f ? "Нажми <color=#6BCB5A>E</color> на площадке: сдай ресурсы и жми «Играть»" : "Ресурсы уже есть: неси их к площадке (золотая метка)";
            if (inv != null && b.PlayerCanContribute(inv))
                return dist < 5f ? "Встань в зону стройки: ресурсы из рюкзака сдадутся" : "Ресурсы уже есть: неси их к площадке (золотая метка)";
            return "Не хватает ресурсов: " + (inv != null ? b.NeedSummary(inv) : "") + ". Руби деревья и возвращайся";
        }

        var tree = t.GetComponentInParent<ResourceItem>();
        if (tree != null)
        {
            if (pack != null && pack.IsFull) return "Рюкзак полон: сначала сдай брёвна, потом руби дальше";
            return dist < 3f ? (PlayerInteraction.RhythmChop ? "Нажми <color=#6BCB5A>E</color> у дерева и бей <color=#6BCB5A>E</color>, когда маркер в зелёной зоне" : "Удерживай <color=#6BCB5A>E</color> у дерева, пока не заполнится полоска")
                             : (PlayerInteraction.RhythmChop ? "Подойди к дереву с золотой меткой и нажми <color=#6BCB5A>E</color>" : "Подойди к дереву с золотой меткой и удерживай <color=#6BCB5A>E</color>");
        }

        return "Иди к золотой метке. Путь можно показать клавишей " + helpKey;
    }

    // ------------------------------------------------------------ дорожка по земле
    void BuildTrail()
    {
        trailRoot = new GameObject("QuestTrail");
        marks = new SpriteRenderer[markCount];
        for (int i = 0; i < markCount; i++)
        {
            var go = new GameObject("Mark" + i);
            go.transform.SetParent(trailRoot.transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = VibeGfx.Chevron;
            sr.color = new Color(1f, 1f, 1f, 0f);
            go.transform.localScale = Vector3.one * 0.9f;
            go.SetActive(false);
            marks[i] = sr;
        }
    }

    void HideMarks()
    {
        if (marks == null) return;
        for (int i = 0; i < marks.Length; i++)
            if (marks[i] != null && marks[i].gameObject.activeSelf) marks[i].gameObject.SetActive(false);
    }

    void TickTrail(Transform tgt, bool active, float dt)
    {
        if (trailT > 0f) trailT -= dt;
        bool want = active && trailT > 0f;
        trailAlpha = Mathf.MoveTowards(trailAlpha, want ? 1f : 0f, dt * 3f);

        if (trailAlpha <= 0.01f) { HideMarks(); return; }
        if (!active) { HideMarks(); return; }
        if (marks == null) BuildTrail();

        Vector3 from = player.position;
        Vector3 dir = tgt.position - from;
        dir.y = 0f;
        float len = dir.magnitude;
        if (len < 3.5f) { HideMarks(); return; }                  // уже на месте
        dir /= len;

        float span = Mathf.Min(len - 2f, trailLength);
        int n = Mathf.Clamp(Mathf.FloorToInt(span / spacing), 0, marks.Length);
        float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        Quaternion rot = Quaternion.Euler(90f, yaw, 0f);          // шеврон лежит на земле и смотрит вперёд
        float time = Time.time;

        for (int i = 0; i < marks.Length; i++)
        {
            var sr = marks[i];
            if (i >= n) { if (sr.gameObject.activeSelf) sr.gameObject.SetActive(false); continue; }

            Vector3 p = from + dir * (1.6f + i * spacing);
            p.y = GroundY(p, from.y) + groundOffset;

            // волна яркости бежит к цели, хвост дорожки гаснет
            float w = 0.5f + 0.5f * Mathf.Sin(time * 5f - i * 0.7f);
            float a = trailAlpha * (0.25f + 0.65f * w) * Mathf.Clamp01((n - i) / 3f);

            sr.transform.SetPositionAndRotation(p, rot);
            sr.color = new Color(1f, 1f, 1f, a);
            if (!sr.gameObject.activeSelf) sr.gameObject.SetActive(true);
        }
    }

    // высота земли под точкой: луч вниз от уровня героя, героя и триггеры не считаем
    float GroundY(Vector3 p, float refY)
    {
        Vector3 o = new Vector3(p.x, refY + 3f, p.z);
        int c = Physics.RaycastNonAlloc(o, Vector3.down, hits, 8f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        float y = refY;
        for (int i = 0; i < c; i++)
        {
            var col = hits[i].collider;
            if (col == null || col.transform.IsChildOf(player)) continue;
            if (hits[i].distance < best) { best = hits[i].distance; y = hits[i].point.y; }
        }
        return y;
    }

    // ------------------------------------------------------------ плашка
    void BuildToast()
    {
        var cgo = new GameObject("QuestGuide_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        cgo.layer = 5;
        var canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 7;
        var sc = cgo.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        sc.matchWidthOrHeight = 0.5f;
        canvasRt = (RectTransform)cgo.transform;

        var pgo = new GameObject("Toast", typeof(RectTransform), typeof(CanvasGroup));
        pgo.layer = 5;
        pgo.transform.SetParent(canvasRt, false);
        panel = (RectTransform)pgo.transform;
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
        panel.pivot = new Vector2(0.5f, 0f);
        panel.sizeDelta = new Vector2(900f, 108f);
        toastGroup = pgo.GetComponent<CanvasGroup>();
        toastGroup.blocksRaycasts = false;
        toastGroup.interactable = false;

        AddImage(panel, "Bg", UiDraw.Pill, new Color32(0x1C, 0x2A, 0x3A, 235), true, Vector2.zero, Vector2.zero);
        AddImage(panel, "Ring", UiDraw.PillRing, new Color32(0xF5, 0xC5, 0x42, 255), true, Vector2.zero, Vector2.zero);
        AddImage(panel, "Icon", UiDraw.Star, Color.white, false, new Vector2(-400f, 0f), new Vector2(56f, 56f));

        TMP_FontAsset font = null;
        if (UIManager.Instance != null && UIManager.Instance.objectiveText != null) font = UIManager.Instance.objectiveText.font;
        toastMain = AddText(panel, "Main", font, 28f, Color.white, new Vector2(30f, 14f), new Vector2(790f, 46f), FontStyles.Bold);
        toastSub = AddText(panel, "Sub", font, 20f, new Color32(0xAE, 0xB9, 0xD6, 255), new Vector2(30f, -26f), new Vector2(790f, 30f), FontStyles.Normal);
        panel.gameObject.SetActive(false);
    }

    static void AddImage(RectTransform parent, string name, Sprite sp, Color c, bool stretch, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        var im = go.GetComponent<Image>();
        im.sprite = sp;
        im.color = c;
        im.raycastTarget = false;
        if (stretch)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            im.type = UnityEngine.UI.Image.Type.Sliced;
        }
        else { rt.anchoredPosition = pos; rt.sizeDelta = size; }
    }

    static TMP_Text AddText(RectTransform parent, string name, TMP_FontAsset font, float size, Color c, Vector2 pos, Vector2 box, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = pos;
        rt.sizeDelta = box;
        var tx = go.GetComponent<TextMeshProUGUI>();
        if (font != null) tx.font = font;
        tx.fontSize = size;
        tx.fontStyle = style;
        tx.alignment = TextAlignmentOptions.Left;
        tx.color = c;
        tx.raycastTarget = false;
        tx.enableAutoSizing = true;
        tx.fontSizeMin = 16f;
        tx.fontSizeMax = size;
        return tx;
    }

    void TickToast()
    {
        if (panel == null) return;
        float want = Time.unscaledTime < toastUntil ? 1f : 0f;
        toastK = Mathf.MoveTowards(toastK, want, Time.unscaledDeltaTime * 4f);

        bool vis = toastK > 0.01f;
        if (panel.gameObject.activeSelf != vis) panel.gameObject.SetActive(vis);
        if (!vis) return;

        float e = UiDraw.EaseOutCubic(toastK);
        panel.anchoredPosition = new Vector2(0f, Mathf.Lerp(150f, 190f, e));
        toastGroup.alpha = toastK;

        // расстояние в подписи живое: пересобираем 3 раза в секунду
        if (Time.unscaledTime >= subAt && lastTarget != null && player != null)
        {
            subAt = Time.unscaledTime + 0.33f;
            float dist = FlatDist(lastTarget);
            toastSub.text = "Цель: " + Mathf.RoundToInt(dist) + " м" + (OnScreen(lastTarget) ? "" : ", " + Side(lastTarget)) + "   ·   " + helpKey + ": показать путь";
        }
    }
}
