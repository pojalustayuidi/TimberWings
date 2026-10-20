// Assets/_Project/Scripts/Build/BuildSiteTagView.cs
// Метка над площадкой стройки. Одна на всё: название, стадии, цена в брёвнах, таймер, подсказка.
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
public class BuildSiteTagView : MonoBehaviour
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

    // ---------------------------------------------------------------- цвета
    static readonly Color32 Panel = new Color32(0x1F, 0x2A, 0x44, 235);
    static readonly Color32 Ring = new Color32(0x3A, 0x4A, 0x70, 255);
    static readonly Color32 Outline = new Color32(0x14, 0x1C, 0x30, 255);
    static readonly Color32 Green = new Color32(0x6B, 0xCB, 0x5A, 255);
    static readonly Color32 Dim = new Color32(0xAE, 0xB9, 0xD6, 255);
    static readonly Color32 Slot = new Color32(0xFF, 0xFF, 0xFF, 70);
    static readonly Color NodeDim = new Color32(0x3A, 0x4A, 0x70, 255);        // тип Color (не Color32), чтобы тернарные операторы компилировались
    static readonly Color NodeDone = new Color32(0x3C, 0x8A, 0x3B, 255);

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
        c.sortingOrder = 5;
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
        if (rt == null || fnt == null) return;
        foreach (var t in rt.GetComponentsInChildren<TMP_Text>(true)) t.font = fnt;
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
        bannerText = "<color=#6BCB5A>Стадия " + (bc != null ? bc.StageCurrent : 0) + " готова!</color>";
        bannerUntil = Time.unscaledTime + 1.8f;
        punch = 0f;
        lastState = (State)(-1);
    }

    // ================================================================ создание UI
    static RectTransform GetCanvas()
    {
        if (canvasRoot != null) return canvasRoot;
        var go = new GameObject("BuildSiteTags_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        go.layer = 5;
        canvasCmp = go.GetComponent<Canvas>();
        canvasCmp.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasCmp.sortingOrder = 5;                      // ниже HUD (10) и всплывающих наград (20)
        var sc = go.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        sc.matchWidthOrHeight = 0.5f;
        canvasRoot = (RectTransform)go.transform;
        return canvasRoot;
    }

    static RectTransform NewUI(string name, Transform parent, params System.Type[] comps)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        for (int i = 0; i < comps.Length; i++) go.AddComponent(comps[i]);
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static void Stretch(RectTransform r, float inset)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(inset, inset); r.offsetMax = new Vector2(-inset, -inset);
    }

    static HorizontalLayoutGroup HRow(RectTransform r, float spacing, TextAnchor align = TextAnchor.MiddleCenter)
    {
        var h = r.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = spacing;
        h.childAlignment = align;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = false;
        return h;
    }

    static LayoutElement Size(RectTransform r, float w, float h, float flexW = -1f)
    {
        var le = r.gameObject.GetComponent<LayoutElement>();
        if (le == null) le = r.gameObject.AddComponent<LayoutElement>();
        if (w > 0f) le.preferredWidth = w;
        if (h > 0f) { le.preferredHeight = h; le.minHeight = h; }
        if (flexW >= 0f) le.flexibleWidth = flexW;
        return le;
    }

    TMP_Text Txt(Transform parent, string name, float size, Color col, bool bold)
    {
        var r = NewUI(name, parent);
        var t = r.gameObject.AddComponent<TextMeshProUGUI>();
        if (fnt != null) t.font = fnt;                    // сначала шрифт, потом обводка
        t.fontSize = size;
        t.color = col;
        t.alignment = TextAlignmentOptions.Center;
        t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        t.raycastTarget = false;
        t.outlineWidth = 0.18f;
        t.outlineColor = Outline;
        return t;
    }

    const float PanelW = 330f;

    void BuildUI()
    {
        Transform parent = worldSpace ? CreateWorldHost() : (Transform)GetCanvas();
        rt = NewUI("BuildTag_" + buildingName, parent, typeof(Image), typeof(CanvasGroup),
                   typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        rt.pivot = new Vector2(0.5f, 0f);          // опора снизу: смена текста не двигает метку
        if (worldSpace) { rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.anchoredPosition = Vector2.zero; }   // нижний край стоит ровно в точке над зданием
        else rt.anchorMin = rt.anchorMax = Vector2.zero;       // позицию задаём в пикселях экрана

        cg = rt.GetComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable = false;
        cg.alpha = 0f;

        var bg = rt.GetComponent<Image>();
        bg.sprite = UiKit.Panel; bg.type = Image.Type.Sliced; bg.pixelsPerUnitMultiplier = 1f / 0.62f; bg.color = Color.white; bg.raycastTarget = false;

        var v = rt.GetComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(20, 20, 16, 18);
        v.spacing = 9f;
        v.childAlignment = TextAnchor.UpperCenter;
        v.childControlWidth = v.childControlHeight = true;
        v.childForceExpandWidth = v.childForceExpandHeight = false;
        var fit = rt.GetComponent<ContentSizeFitter>();
        fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // рамка поверх фона
        var fr = NewUI("Frame", rt, typeof(Image), typeof(LayoutElement));
        fr.GetComponent<LayoutElement>().ignoreLayout = true;
        Stretch(fr, -6f);                                       // чуть шире панели: получается мягкое свечение вокруг
        frame = fr.GetComponent<Image>();
        frame.sprite = UiDraw.PillRing; frame.type = Image.Type.Sliced; frame.color = new Color(1f, 1f, 1f, 0f); frame.raycastTarget = false;   // свечение: зелёное «можно сдать», янтарное «строим»

        // --- шапка: деревянная табличка с названием
        var head = NewUI("Head", rt, typeof(Image));
        var hi = head.GetComponent<Image>();
        hi.sprite = UiKit.Plaque; hi.type = Image.Type.Sliced; hi.pixelsPerUnitMultiplier = 1f / 0.7f; hi.raycastTarget = false;
        var hh = HRow(head, 10f, TextAnchor.MiddleCenter);
        hh.padding = new RectOffset(14, 14, 5, 7);
        Size(head, PanelW, 46f);
        if (buildingIcon != null)
        {
            var ic = NewUI("Icon", head, typeof(Image));
            Size(ic, 34f, 34f);
            var im = ic.GetComponent<Image>();
            im.sprite = buildingIcon; im.preserveAspect = true; im.raycastTarget = false;
        }
        title = Txt(head, "Title", 30f, Color.white, true);
        Size(title.rectTransform, -1f, 36f, 1f);

        // --- стадии: 1 - 2 - 3 нумерованными узлами, ниже подпись «Стадия 1 из 3 · дальше: ...»
        stageRow = NewUI("StageRow", rt);
        HRow(stageRow, 0f, TextAnchor.MiddleCenter);
        Size(stageRow, PanelW, 38f);
        segRoot = stageRow;
        stageCaption = Txt(rt, "StageCaption", 21f, Dim, false);
        stageCaption.richText = true;
        stageCaption.enableWordWrapping = false;
        Size(stageCaption.rectTransform, PanelW, -1f);

        // --- тонкая линия под шапкой
        divider = NewUI("Divider", rt, typeof(Image));
        Size(divider, PanelW, 2f);
        var dv = divider.GetComponent<Image>();
        dv.color = new Color32(0xF2, 0xB1, 0x34, 70); dv.raycastTarget = false;

        // --- строки ресурсов
        rowWood = MakeRow("Wood", UiKit.Icon(UiIcon.Log), UiBar.Green);
        rowPlank = MakeRow("Plank", UiKit.Icon(UiIcon.Plank), UiBar.Blue);
        rowCoin = MakeRow("Coin", UiKit.Icon(UiIcon.Coin), UiBar.Amber);

        // --- плашки с иконками: сколько строится, награда, сколько брёвен нужно на все стадии
        infoRow = NewUI("InfoRow", rt);
        HRow(infoRow, 10f, TextAnchor.MiddleCenter);
        Size(infoRow, PanelW, 38f);
        chipTime = MakeChip(infoRow, UiIcon.Clock, out txtTime);
        chipXp = MakeChip(infoRow, UiIcon.Star, out txtXp);

        // --- таймер стройки: латунная рамка и янтарная заливка из UiKit
        var timerRow = NewUI("Timer", rt);
        HRow(timerRow, 10f);
        Size(timerRow, PanelW, 28f);
        bar = timerRow;
        Image tf;
        var track = UiKit.MakeBar(timerRow, UiBar.Amber, new Vector2(200f, 22f), out tf, 0.55f);
        Size(track.rectTransform, 200f, 24f, 1f);
        barFill = tf;
        timerText = Txt(timerRow, "TimerText", 22f, Color.white, true);
        timerText.alignment = TextAlignmentOptions.MidlineRight;
        Size(timerText.rectTransform, 84f, 24f);

        // --- подсказка в тёмной «выемке» и «ГОТОВО»
        hintRow = NewUI("HintRow", rt, typeof(Image));
        var hl = HRow(hintRow, 0f);
        hl.padding = new RectOffset(14, 14, 8, 10);
        var hbg = hintRow.GetComponent<Image>();
        hbg.sprite = UiKit.Slot; hbg.type = Image.Type.Sliced; hbg.pixelsPerUnitMultiplier = 1f / 0.6f; hbg.raycastTarget = false;
        Size(hintRow, PanelW, -1f);
        hint = Txt(hintRow, "Hint", 23f, Color.white, false);
        hint.enableWordWrapping = true;
        Size(hint.rectTransform, PanelW - 28f, -1f);
        doneText = Txt(rt, "Done", 34f, Green, true);
        doneText.text = "ГОТОВО";
    }

    Row MakeRow(string name, Sprite icon, UiBar barColor)
    {
        var row = new Row();
        row.root = NewUI("Row_" + name, rt);
        HRow(row.root, 10f);
        Size(row.root, PanelW, 44f);

        // значок ресурса в тёмной «лунке» UiKit
        var slot = UiKit.MakeSlot(row.root, 42f, 0.5f);
        Size(slot.rectTransform, 42f, 42f);
        var ic = NewUI("Icon", slot.transform, typeof(Image));
        Stretch(ic, 5f);
        var im = ic.GetComponent<Image>();
        im.sprite = icon; im.preserveAspect = true; im.raycastTarget = false;

        // полоса из UiKit: латунная рамка, заливка «сдано» и бледная заливка «сдано + то, что несёшь с собой»
        Image fillImg;
        var track = UiKit.MakeBar(row.root, barColor, new Vector2(120f, 22f), out fillImg, 0.55f);
        Size(track.rectTransform, 120f, 24f, 1f);
        row.fill = fillImg;

        var pv = UiKit.MakeImage(track.transform, "Preview", UiKit.BarFill(barColor), false);
        pv.type = Image.Type.Filled; pv.fillMethod = Image.FillMethod.Horizontal; pv.fillOrigin = 0; pv.fillAmount = 0f;
        pv.color = new Color(1f, 1f, 1f, 0.4f);
        var prt = pv.rectTransform;
        prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
        prt.offsetMin = new Vector2(2.75f, 2.75f); prt.offsetMax = new Vector2(-2.75f, -2.75f);
        pv.transform.SetSiblingIndex(0);                       // под заливкой «сдано»
        row.preview = pv;

        row.count = Txt(row.root, "Count", 31f, Color.white, true);
        row.count.alignment = TextAlignmentOptions.MidlineRight;
        row.count.richText = true;
        row.count.enableWordWrapping = false;
        Size(row.count.rectTransform, 132f, 40f);
        return row;
    }

    // нумерованные узлы стадий со звеньями между ними
    void RebuildSegments(int total)
    {
        for (int i = segRoot.childCount - 1; i >= 0; i--) Destroy(segRoot.GetChild(i).gameObject);
        nodeRing = new Image[total]; nodeFill = new Image[total]; nodeCheck = new Image[total];
        nodeNum = new TMP_Text[total];
        links = new Image[Mathf.Max(0, total - 1)];
        targetNode = null;
        for (int i = 0; i < total; i++)
        {
            if (i > 0)
            {
                var l = NewUI("Link" + i, segRoot, typeof(Image));
                Size(l, 26f, 6f);
                var li = l.GetComponent<Image>();
                li.sprite = UiDraw.Pill; li.type = Image.Type.Sliced; li.raycastTarget = false;
                links[i - 1] = li;
            }
            var n = NewUI("Node" + i, segRoot, typeof(Image));
            Size(n, 36f, 36f);
            var ring = n.GetComponent<Image>();
            ring.sprite = UiDraw.Pill; ring.type = Image.Type.Sliced; ring.raycastTarget = false;
            var f = NewUI("Fill", n, typeof(Image));
            Stretch(f, 3.5f);
            var fi = f.GetComponent<Image>();
            fi.sprite = UiDraw.Pill; fi.type = Image.Type.Sliced; fi.raycastTarget = false;
            var num = Txt(n, "Num", 21f, Color.white, true);
            Stretch(num.rectTransform, 0f);
            num.text = (i + 1).ToString();
            var ck = NewUI("Check", n, typeof(Image));
            Stretch(ck, 8f);
            var ci = ck.GetComponent<Image>();
            ci.sprite = UiKit.Icon(UiIcon.Check); ci.preserveAspect = true; ci.raycastTarget = false;
            nodeRing[i] = ring; nodeFill[i] = fi; nodeNum[i] = num; nodeCheck[i] = ci;
        }
    }

    // плашка: значок + текст в тёмной выемке
    RectTransform MakeChip(Transform parent, UiIcon icon, out TMP_Text label)
    {
        var chip = NewUI("Chip_" + icon, parent, typeof(Image));
        var ci = chip.GetComponent<Image>();
        ci.sprite = UiKit.Slot; ci.type = Image.Type.Sliced; ci.pixelsPerUnitMultiplier = 1f / 0.5f; ci.raycastTarget = false;
        var h = HRow(chip, 6f, TextAnchor.MiddleCenter);
        h.padding = new RectOffset(10, 12, 4, 6);
        var ic = NewUI("Icon", chip, typeof(Image));
        Size(ic, 26f, 26f);
        var im = ic.GetComponent<Image>();
        im.sprite = UiKit.Icon(icon); im.preserveAspect = true; im.raycastTarget = false;
        label = Txt(chip, "Text", 24f, Color.white, true);
        label.richText = true;
        label.enableWordWrapping = false;
        Size(label.rectTransform, -1f, 28f);
        return chip;
    }

    static string FormatTime(float sec)
    {
        if (sec >= 10f) return Mathf.RoundToInt(sec) + " с";
        return sec.ToString("0.0") + " с";
    }

    // награда в опыте за этап, который строим сейчас
    int StepXp(int cur)
    {
        if (bc == null) return 0;
        if (bc.data != null && bc.data.steps != null && bc.data.steps.Length > 0)
        {
            int si = bc.StageTotal > 1 ? cur - Mathf.Max(1, bc.data.startStage) : 0;
            if (si >= 0 && si < bc.data.steps.Length) return bc.data.steps[si].rewardXp;
        }
        return bc.rewardXp;
    }

    // « · всего N брёвен»: сколько нужно на все стадии (если стадий несколько)
    string ManyWood()
    {
        if (bc == null || bc.TotalWoodAllSteps <= bc.CostTotal) return "";
        int n = bc.TotalWoodAllSteps;
        return "  <color=#F2B134>·</color>  всего <color=#FFD66B>" + n + "</color> " + UiDraw.Plural(n, "бревно", "бревна", "бревен");
    }

    // как называется этап, который строим дальше (поле «name» в BuildSiteData; стандартное «Этап» не показываем)
    string NextStepName(int cur)
    {
        if (bc == null || bc.data == null || bc.data.steps == null) return "";
        int si = cur - Mathf.Max(1, bc.data.startStage);
        if (si < 0 || si >= bc.data.steps.Length) return "";
        string n = bc.data.steps[si].name;
        if (string.IsNullOrEmpty(n) || n == "Этап" || n.Length > 22 || n.StartsWith("Стадия", System.StringComparison.OrdinalIgnoreCase)) return "";
        return n;
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
    void FillRow(Row r, int have, int carry, int total, string complete)
    {
        bool on = total > 0;
        SetRow(r.root, on);
        if (!on) return;

        int add = Mathf.Clamp(carry, 0, Mathf.Max(0, total - have));
        r.target = Mathf.Clamp01(have / (float)total);
        r.targetPreview = Mathf.Clamp01((have + add) / (float)total);

        bool done = have >= total;
        string col = done ? "#6BCB5A" : "#FFFFFF";
        string plus = (!done && add > 0) ? " <size=75%><color=#F5C542>+" + add + "</color></size>" : "";
        r.count.text = "<color=" + col + ">" + have + "</color>" + plus + "<color=#AEB9D6>/" + total + "</color>";
    }

    void Refresh(int wood, int planks, int coins, int cur, int tot, float prog)
    {
        bool staged = tot > 1;
        bool done = state == State.Done, locked = state == State.Locked, building = state == State.InProgress;

        bool upgrade = staged && bc != null && bc.TutorialFired && !done && !locked;
        title.text = upgrade ? buildingName + " <size=65%><color=#AEB9D6>улучшение</color></size>" : buildingName;
        title.color = locked ? (Color)Dim : Color.white;

        // стадии: пройденные зелёные с галочкой, следующая янтарная и пульсирует, будущие тусклые
        bool compactView = Compact;
        SetRow(stageRow, staged && !locked && !compactView);
        SetRow(stageCaption, staged && !locked && !compactView);
        targetNode = null;
        for (int i = 0; i < nodeRing.Length; i++)
        {
            bool reached = i < cur;
            bool target = i == cur && !done;
            Color ringC = reached ? UiKit.Good : (target ? UiKit.Brass : NodeDim);
            Color fillC = reached ? NodeDone : (target ? UiKit.Navy : UiKit.Ink);
            nodeRing[i].color = ringC;
            nodeFill[i].color = fillC;
            nodeCheck[i].enabled = reached;
            nodeNum[i].enabled = !reached;
            nodeNum[i].color = target ? (Color)UiKit.BrassHi : (Color)Dim;
            nodeRing[i].rectTransform.localScale = Vector3.one;
            if (target) targetNode = nodeRing[i].rectTransform;
            if (i > 0) links[i - 1].color = reached ? UiKit.Good : NodeDim;
        }
        if (staged)
        {
            string next = NextStepName(cur);
            stageCaption.text = done
                ? "<color=#6BCB5A>Построено полностью</color>"
                : "Стадия " + cur + " из " + tot + (next.Length > 0 ? "  <color=#F2B134>·</color>  <color=#FFD66B>" + next + "</color>" : ManyWood());
        }

        // ресурсы
        bool showCost = !locked && !done && bc != null && !compactView;
        SetRow(divider, showCost || (building && !compactView));
        if (showCost)
        {
            FillRow(rowWood, bc.Deposited, wood, bc.CostTotal, "");
            FillRow(rowPlank, bc.DepositedPlanks, planks, bc.PlankTotal, "");
            int haveCoins = bc.DepositedCoins >= bc.CoinTotal ? bc.CoinTotal : bc.DepositedCoins;
            FillRow(rowCoin, haveCoins, coins, bc.CoinTotal, "");
        }
        else
        {
            SetRow(rowWood.root, false); SetRow(rowPlank.root, false); SetRow(rowCoin.root, false);
        }

        // таймер
        SetRow(bar, building);
        if (building)
        {
            barFill.fillAmount = prog;
            float left = bc != null ? Mathf.Max(0f, (1f - prog) * bc.BuildTime) : 0f;
            timerText.text = "<size=70%><color=#AEB9D6>ещё</color></size> " + FormatTime(left);
        }

        // плашки: сколько будет строиться, что получишь, сколько брёвен нужно на все стадии
        bool showInfo = !locked && !done && !building && bc != null && !compactView;
        SetRow(infoRow, showInfo);
        if (showInfo)
        {
            txtTime.text = FormatTime(bc.BuildTime);
            int xp = StepXp(cur);
            SetRow(chipXp, xp > 0);
            if (xp > 0) txtXp.text = "<color=#FFD66B>+" + xp + " XP</color>";
        }

        canContribute = bc != null && inv != null && !locked && !done && bc.PlayerCanContribute(inv);

        // подсказка
        string h;
        if (Time.unscaledTime < bannerUntil) h = bannerText;
        else if (locked) h = "Откроется после: " + lockedAfter;
        else if (building) h = "Строим…";
        else if (done) h = "";
        else if (Time.unscaledTime < flashUntil)
            h = "<color=#FF8A7A>Нужно ещё " + flashMissing + " " + UiDraw.Plural(flashMissing, "бревно", "бревна", "бревен") + "</color>";
        else if (compactView && bc != null) h = bc.AllDeposited ? "<color=#6BCB5A>Всё сдано</color>" : bc.NeedSummary(inv);   // одна строка: «Бревна 3/8 · Доски 0/6»
        else if (canContribute && bc != null && !bc.AllDeposited) h = "<color=#6BCB5A>Встань рядом, чтобы сдать</color>";
        else h = MissingText();
        hint.text = h;
        SetRow(hintRow, h.Length > 0);

        SetRow(doneText, done);
        if (done && doneTimer < 0f) doneTimer = 0f;

        LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
    }

    // чего не хватает, первым делом брёвен, потом досок, потом монет
    string MissingText()
    {
        if (bc == null) return "";
        int w = bc.CostTotal - bc.Deposited;
        if (w > 0) return "Принеси ещё " + w + " " + UiDraw.Plural(w, "бревно", "бревна", "бревен");
        int p = bc.PlankTotal - bc.DepositedPlanks;
        if (p > 0) return "Принеси ещё " + p + " " + UiDraw.Plural(p, "доску", "доски", "досок");
        int c = bc.CoinTotal - bc.DepositedCoins;
        if (c > 0) return "Нужно ещё " + c + " " + UiDraw.Plural(c, "монета", "монеты", "монет");
        return "";
    }

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
        if (ready) { fc = Green; fc.a = 0.3f + 0.4f * (0.5f + 0.5f * Mathf.Sin(pulse)); }
        else if (state == State.InProgress) { fc = UiKit.Brass; fc.a = 0.55f; }
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
            rt.anchoredPosition = new Vector2(shake, 0f);     // дрожит только при отказе («Нужно ещё N»)
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
        rt.position = new Vector3(curPos.x + shake * scale, curPos.y, 0f);
    }
}