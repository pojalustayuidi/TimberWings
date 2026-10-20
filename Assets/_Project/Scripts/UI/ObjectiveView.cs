// Assets/_Project/Scripts/UI/ObjectiveView.cs (НЕ в Editor)
// Виджет цели: заголовок + строки-требования с иконкой ресурса, счётчик «7/10» и мини-прогрессбар.
// Создаётся сам (UIManager.SetObjective), садится внутрь панели задания на место текста objectiveText и растягивает панель вниз.
// Счётчики живые: у требования с ObjectiveReq.live число читается каждый кадр, а PlayerInventory.Changed обновляет его сразу.
// При росте числа строка пульсирует, при выполнении становится зелёной, появляется галочка и звучит короткий «звон».
// objectiveText остаётся в сцене выключенным (enabled = false) и хранит простой текст цели:
// его шрифт берут десятки скриптов, а его текст читает QuestGuide.
// Клавиша J по кругу: полное, свёрнутое, скрыто. Сворачивание: кнопка-шеврон на верхней кромке панели (выбор запоминается). Свёрнутое задание: заголовок, одна строка
// «Бревна 5/10 · Доски 0/4» и тонкая общая полоска; панель по высоте сжимается, на экране остаётся меньше плашки.
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ObjectiveView : MonoBehaviour
{
    public static ObjectiveView Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { Instance = null; }

    // Размеры в единицах канваса (эталон 1080p: это и есть пиксели, шрифты не меньше 18).
    // На экранах ниже 1080p всё домножается на k (до 1.2), чтобы после масштаба канваса текст оставался читаемым (720p: ~19 px вместо 16)
    float k = 1f;
    float TitleSize { get { return 24f * k; } }
    float RowSize { get { return 22f * k; } }
    float CountSize { get { return 26f * k; } }
    float NoteSize { get { return 18f * k; } }
    float FooterSize { get { return 18f * k; } }
    float StageSize { get { return 18f * k; } }
    float StageH { get { return 24f * k; } }
    float PipW { get { return 18f * k; } }
    float PipH { get { return 7f * k; } }
    float PipGap { get { return 4f * k; } }
    float IconSize { get { return 32f * k; } }
    float RowTop { get { return 34f * k; } }
    float NoteH { get { return 22f * k; } }
    float BarH { get { return 6f * k; } }
    float RowGap { get { return 6f * k; } }
    float TitleGap { get { return 6f * k; } }
    float BottomPad { get { return 10f * k; } }
    float CountW { get { return 92f * k; } }
    float CheckW { get { return 26f * k; } }
    float CheckGap { get { return 6f * k; } }
    float MinWidth { get { return 360f; } }
    const float SlideDist = 70f;         // на сколько уезжает прежняя цель и откуда выезжает новая
    const float MaxFlyers = 6;           // иконок в полёте за одну сдачу
    int lastScreenH;

    static readonly Color Green = new Color32(0x6B, 0xCB, 0x5A, 255);
    static readonly Color White = Color.white;

    class Row
    {
        public ObjectiveReq req;
        public RectTransform rt, barFillRt;
        public Image icon, check, barBack, barFill;
        public TMP_Text name, count, note;
        public int have = -1;
        public bool done;
        public float punch, punchAmp, checkAnim = 1f, fill, fillTarget;
        public float height;
    }

    // свёрнутый вид
    const string CollapsePref = "objective_collapsed";
    public static KeyCode CollapseKey = KeyCode.J;
    bool collapsed;
    bool hiddenAll;                 // J третий раз: панель задания полностью спрятана (не запоминается между запусками)
    CanvasGroup taskCg;
    Button toggleBtn;
    Image toggleIcon;
    TMP_Text summaryTxt;
    Image sumBack, sumFill;
    float sumFillCur, sumFillTarget, sumPunch;
    int sumKey = int.MinValue;
    readonly System.Text.StringBuilder sb = new System.Text.StringBuilder(96);
    float SumH { get { return 26f * k; } }

    UIManager ui;
    RectTransform task, root;
    CanvasGroup cg;
    TMP_Text titleTxt, stageTxt, footerTxt;
    readonly List<Image> pips = new List<Image>(6);
    readonly List<Row> rows = new List<Row>(4);
    readonly Stack<Row> pool = new Stack<Row>();
    readonly Vector3[] corners = new Vector3[4];

    ObjectiveData cur;
    string sig;
    float baseTaskH = -1f;
    float appear = 1f;        // 0..1: появление новой цели (выезд сверху и проявление)
    float contentH;
    bool placed;
    Vector2 placePos;
    float placeW;

    // полёт иконки сданного ресурса из HUD к строке квеста
    class Flyer
    {
        public RectTransform rt;
        public Image img;
        public Row row;
        public Vector3 from;
        public float delay, t, dur, spin;
        public bool started;
    }
    readonly List<Flyer> flyers = new List<Flyer>(8);
    readonly Stack<Flyer> flyerPool = new Stack<Flyer>();

    // прежняя цель, которая уезжает при смене
    class Ghost { public RectTransform rt; public CanvasGroup cg; public Vector2 from; public float t; }
    readonly List<Ghost> ghosts = new List<Ghost>(2);

    public bool IsShown { get { return root != null && root.gameObject.activeSelf; } }

    // ---------------------------------------------------------------- создание
    public static ObjectiveView Ensure(UIManager ui)
    {
        if (Instance != null) return Instance;
        if (ui == null || ui.objectiveText == null) return null;
        var panel = ui.objectiveText.transform.parent as RectTransform;
        if (panel == null) return null;

        var go = new GameObject("ObjectiveView", typeof(RectTransform), typeof(CanvasGroup));
        go.layer = panel.gameObject.layer;
        go.transform.SetParent(panel, false);
        var v = go.AddComponent<ObjectiveView>();
        v.ui = ui;
        v.task = panel;
        v.root = (RectTransform)go.transform;
        v.cg = go.GetComponent<CanvasGroup>();
        v.cg.blocksRaycasts = false;
        v.cg.interactable = false;
        v.Build();
        v.BuildToggle();
        Instance = v;
        return v;
    }

    void Build()
    {
        var font = ui.objectiveText.font;
        titleTxt = MakeText("Title", root, TitleSize, UiKit.BrassHi, TextAlignmentOptions.TopLeft, font);
        titleTxt.fontStyle = FontStyles.Bold;
        titleTxt.enableWordWrapping = true;
        stageTxt = MakeText("Stage", root, 18f, UiKit.Brass, TextAlignmentOptions.TopLeft, font);
        stageTxt.fontStyle = FontStyles.Bold;
        footerTxt = MakeText("Footer", root, FooterSize, White, TextAlignmentOptions.TopLeft, font);
        footerTxt.fontStyle = FontStyles.Normal;
        footerTxt.enableWordWrapping = true;
        footerTxt.richText = true;

        summaryTxt = MakeText("Summary", root, 22f, White, TextAlignmentOptions.MidlineLeft, font);
        summaryTxt.fontStyle = FontStyles.Bold;
        summaryTxt.richText = true;
        summaryTxt.enableAutoSizing = true;
        summaryTxt.fontSizeMin = 14f;
        summaryTxt.fontSizeMax = 22f;
        sumBack = UiKit.MakeImage(root, "SumBack", null, false);
        sumBack.color = new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.7f);
        sumFill = UiKit.MakeImage(root, "SumFill", null, false);
        sumFill.color = UiKit.Brass;
        summaryTxt.gameObject.SetActive(false); sumBack.gameObject.SetActive(false); sumFill.gameObject.SetActive(false);
        root.gameObject.SetActive(false);
    }

    // кнопка сворачивания живёт прямо на панели задания (корень виджета не ловит клики), на верхней кромке справа
    void BuildToggle()
    {
        try { collapsed = PlayerPrefs.GetInt(CollapsePref, 0) == 1; } catch { collapsed = false; }
        taskCg = task.GetComponent<CanvasGroup>();
        if (taskCg == null) taskCg = task.gameObject.AddComponent<CanvasGroup>();
        toggleBtn = UiKit.MakeButton(task, "", UiBtn.Amber, new Vector2(54f, 32f), 20f, 0.8f);
        toggleBtn.gameObject.name = "ObjectiveToggle";
        toggleBtn.navigation = new Navigation { mode = Navigation.Mode.None };
        var rt = toggleBtn.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.anchoredPosition = new Vector2(-14f, 2f);
        rt.sizeDelta = new Vector2(54f, 32f);
        rt.SetAsLastSibling();
        toggleIcon = UiKit.MakeIcon(toggleBtn.transform, UiIcon.ArrowUp, 20f, "Chevron");
        toggleIcon.rectTransform.anchoredPosition = Vector2.zero;
        toggleBtn.onClick.AddListener(ToggleCollapsed);
        UiClickFallback.Register(toggleBtn, () => IsShown && !hiddenAll, false);
        toggleBtn.gameObject.SetActive(false);
        UpdateToggleIcon();
    }

    void UpdateToggleIcon()
    {
        if (toggleIcon != null) toggleIcon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, collapsed ? 180f : 0f);   // стрелка вниз: «раскрыть»
    }

    public void ToggleCollapsed() { if (hiddenAll) hiddenAll = false; else SetCollapsed(!collapsed); }

    // клавиша J по кругу: полное → свёрнутое → скрыто → полное
    public void CycleMode()
    {
        if (hiddenAll) { hiddenAll = false; SetCollapsed(false); }
        else if (!collapsed) SetCollapsed(true);
        else hiddenAll = true;
    }

    public void SetCollapsed(bool v)
    {
        if (collapsed == v) return;
        collapsed = v;
        try { PlayerPrefs.SetInt(CollapsePref, v ? 1 : 0); } catch { }
        UpdateToggleIcon();
        if (cur != null && IsShown) { Rebuild(cur); sumPunch = collapsed ? 0f : sumPunch; }
    }

    static TMP_Text MakeText(string name, Transform parent, float size, Color color, TextAlignmentOptions align, TMP_FontAsset font)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;                 // сначала шрифт, потом обводка
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        t.enableWordWrapping = false;
        t.overflowMode = TextOverflowModes.Overflow;
        t.outlineWidth = 0.18f;
        t.outlineColor = (Color32)UiKit.Ink;
        return t;
    }

    // ---------------------------------------------------------------- подписка на инвентарь
    void OnEnable()
    {
        PlayerInventory.Changed += OnInventoryChanged;
        BuildingController.ResourceDeposited += OnDeposited;
    }

    void OnDisable()
    {
        PlayerInventory.Changed -= OnInventoryChanged;
        BuildingController.ResourceDeposited -= OnDeposited;
    }
    void OnDestroy() { if (Instance == this) Instance = null; }

    void OnInventoryChanged() { if (IsShown) Poll(true); }

    // ---------------------------------------------------------------- показ
    public void Show(ObjectiveData d)
    {
        if (d == null || !d.HasReqs) { Hide(); return; }

        string s = d.Signature;
        if (IsShown && s == sig)
        {
            // та же цель: только новые данные (живые функции могли смениться), счётчики обновятся с пульсом
            cur = d;
            for (int i = 0; i < rows.Count && i < d.reqs.Count; i++) rows[i].req = d.reqs[i];
            if (footerTxt.text != (d.footer ?? "") && !string.IsNullOrEmpty(d.footer)) footerTxt.text = d.footer;
            Poll(true);
            return;
        }

        // другая цель поверх показанной: прежняя уезжает, новая выезжает
        bool replacing = IsShown && cur != null;
        if (replacing) SpawnGhost();

        cur = d;
        sig = s;
        Rebuild(d);
        appear = 0f;
        cg.alpha = 0f;                      // без вспышки в первом кадре: Update проявит
        root.gameObject.SetActive(true);
        if (toggleBtn != null) toggleBtn.gameObject.SetActive(true);
        if (ui.objectiveText != null) ui.objectiveText.enabled = false;
        MirrorPlain();
    }

    public void Hide()
    {
        cur = null;
        sig = null;
        ClearFlyers();
        ClearGhosts();
        if (root != null) root.gameObject.SetActive(false);
        if (toggleBtn != null) toggleBtn.gameObject.SetActive(false);
        hiddenAll = false;
        if (taskCg != null) { taskCg.alpha = 1f; taskCg.blocksRaycasts = true; }
        RestorePanel();
        placed = false;                    // в следующий раз позиция берётся заново с панели исходного размера
        if (ui != null && ui.objectiveText != null) ui.objectiveText.enabled = true;
    }

    // ---------------------------------------------------------------- сборка строк
    void Rebuild(ObjectiveData d)
    {
        k = Mathf.Clamp(1080f / Mathf.Max(1, Screen.height), 1f, 1.2f);
        lastScreenH = Screen.height;
        ClearFlyers();
        titleTxt.fontSize = TitleSize;
        stageTxt.fontSize = StageSize;
        footerTxt.fontSize = FooterSize;
        for (int i = 0; i < rows.Count; i++) { rows[i].rt.gameObject.SetActive(false); pool.Push(rows[i]); }
        rows.Clear();

        PlaceRoot();
        float w = root.rect.width;

        // заголовок слева, справа индикатор стадий (точки: пройдено зелёным, текущая янтарная, дальше тёмные)
        bool hasStage = d.HasStage;
        float pipsW = hasStage ? d.stageCount * (PipW + PipGap) : 0f;
        float titleW = Mathf.Max(120f, w - pipsW - 8f);
        titleTxt.text = d.title;
        float th = Mathf.Ceil(titleTxt.GetPreferredValues(d.title ?? "", titleW, 0f).y);
        SetTop(titleTxt.rectTransform, 0f, titleW, th);
        LayoutPips(d, w, th);
        float y = th;

        bool showStage = hasStage && !collapsed;
        stageTxt.gameObject.SetActive(showStage);
        if (showStage)
        {
            string st = "Стадия " + d.stage + " из " + d.stageCount + (string.IsNullOrEmpty(d.stageName) ? "" : " · " + d.stageName);
            stageTxt.text = st;
            SetTop(stageTxt.rectTransform, y, w, StageH);
            y += StageH;
        }
        y += TitleGap;

        summaryTxt.gameObject.SetActive(collapsed);
        sumBack.gameObject.SetActive(collapsed);
        sumFill.gameObject.SetActive(collapsed);
        if (collapsed)
        {
            summaryTxt.fontSizeMax = 22f * k;
            SetTop(summaryTxt.rectTransform, y, w, SumH);
            y += SumH;
            var br = sumBack.rectTransform;
            br.anchorMin = br.anchorMax = new Vector2(0f, 1f);
            br.pivot = new Vector2(0f, 1f);
            br.anchoredPosition = new Vector2(0f, -y);
            br.sizeDelta = new Vector2(w, BarH);
            var fr2 = sumFill.rectTransform;
            fr2.anchorMin = fr2.anchorMax = new Vector2(0f, 1f);
            fr2.pivot = new Vector2(0f, 1f);
            fr2.anchoredPosition = new Vector2(0f, -y);
            fr2.sizeDelta = new Vector2(0f, BarH);
            y += BarH + RowGap;
            sumKey = int.MinValue;
            RefreshSummary(false);
            sumFillCur = sumFillTarget;
            fr2.sizeDelta = new Vector2(w * sumFillCur, BarH);
        }

        for (int i = 0; i < d.reqs.Count && !collapsed; i++)
        {
            Row r = pool.Count > 0 ? pool.Pop() : CreateRow();
            r.rt.gameObject.SetActive(true);
            r.rt.SetAsLastSibling();
            r.req = d.reqs[i];
            r.req.Refresh();
            FillRow(r, w);
            SetTop(r.rt, y, w, r.height);
            y += r.height;
            rows.Add(r);
        }

        bool hasFooter = !string.IsNullOrEmpty(d.footer) && !collapsed;
        footerTxt.gameObject.SetActive(hasFooter);
        if (hasFooter)
        {
            footerTxt.text = d.footer;
            float fh = Mathf.Ceil(footerTxt.GetPreferredValues(d.footer, w, 0f).y);
            SetTop(footerTxt.rectTransform, y + 2f, w, fh);
            y += fh + 4f;
        }

        contentH = y;
        root.sizeDelta = new Vector2(root.sizeDelta.x, contentH);
        GrowPanel();
    }

    // верх-лево области внутри panel (на месте objectiveText), ширина берётся оттуда же.
    // Считается один раз, пока панель в исходном размере: после роста панели objectiveText мог уехать (привязка к низу или центру)
    void PlaceRoot()
    {
        if (!placed)
        {
            var ot = ui.objectiveText.rectTransform;
            ot.GetWorldCorners(corners);
            Vector3 tl = task.InverseTransformPoint(corners[1]);
            Vector3 br = task.InverseTransformPoint(corners[3]);
            Rect pr = task.rect;
            placePos = new Vector2(tl.x - pr.xMin, tl.y - pr.yMax);
            placeW = Mathf.Max(MinWidth, br.x - tl.x);
            placed = true;

            // не заходим за безопасную зону экрана (вырез, скругления): правый край списка в пикселях ограничиваем Screen.safeArea
            var canvas = task.GetComponentInParent<Canvas>();
            float scale = canvas != null ? Mathf.Max(0.0001f, canvas.rootCanvas.scaleFactor) : 1f;
            Camera uiCam = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
            Vector2 leftPx = RectTransformUtility.WorldToScreenPoint(uiCam, task.TransformPoint(new Vector3(pr.xMin + placePos.x, 0f, 0f)));
            float rightLimit = Screen.safeArea.xMax - 12f;
            float maxW = (rightLimit - leftPx.x) / scale;
            if (maxW > 160f && placeW > maxW) placeW = maxW;
        }

        root.anchorMin = root.anchorMax = new Vector2(0f, 1f);
        root.pivot = new Vector2(0f, 1f);
        root.anchoredPosition = placePos;
        root.sizeDelta = new Vector2(placeW, Mathf.Max(contentH, 10f));
    }

    void LayoutPips(ObjectiveData d, float w, float lineH)
    {
        int n = d.HasStage ? d.stageCount : 0;
        while (pips.Count < n)
        {
            var im = UiKit.MakeImage(root, "Pip", null, false);
            pips.Add(im);
        }
        for (int i = 0; i < pips.Count; i++)
        {
            var im = pips[i];
            im.gameObject.SetActive(i < n);
            if (i >= n) continue;
            var rt = im.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(1f, 0.5f);
            // справа налево: последняя точка у правого края
            float xr = w - (n - 1 - i) * (PipW + PipGap);
            rt.anchoredPosition = new Vector2(xr, -lineH * 0.5f);
            rt.sizeDelta = new Vector2(PipW, PipH);
            int cur = d.stage - 1;
            im.color = i < cur ? Green : (i == cur ? UiKit.Brass : new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.75f));
        }
    }

    static void SetTop(RectTransform rt, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(0f, -y);
        rt.sizeDelta = new Vector2(w, h);
    }

    Row CreateRow()
    {
        var r = new Row();
        var go = new GameObject("Req", typeof(RectTransform));
        go.layer = root.gameObject.layer;
        go.transform.SetParent(root, false);
        r.rt = (RectTransform)go.transform;

        var font = ui.objectiveText.font;

        var ig = UiKit.MakeImage(r.rt, "Icon", null, false);
        ig.preserveAspect = true;
        r.icon = ig;

        r.name = MakeText("Name", r.rt, RowSize, White, TextAlignmentOptions.MidlineLeft, font);
        r.name.fontStyle = FontStyles.Bold;
        r.count = MakeText("Count", r.rt, CountSize, White, TextAlignmentOptions.MidlineRight, font);
        r.count.fontStyle = FontStyles.Bold;
        r.note = MakeText("Note", r.rt, NoteSize, new Color32(0xC9, 0xD3, 0xE6, 255), TextAlignmentOptions.MidlineLeft, font);
        r.note.fontStyle = FontStyles.Normal;
        r.note.outlineWidth = 0.12f;

        var ck = UiKit.MakeImage(r.rt, "Check", UiKit.Icon(UiIcon.Check), false);
        ck.preserveAspect = true;
        ck.color = Green;
        r.check = ck;

        r.barBack = UiKit.MakeImage(r.rt, "BarBack", null, false);
        r.barBack.color = new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.7f);
        r.barFill = UiKit.MakeImage(r.rt, "BarFill", null, false);
        r.barFillRt = r.barFill.rectTransform;
        return r;
    }

    void FillRow(Row r, float w)
    {
        var q = r.req;
        bool hasNote = q.liveNote != null || !string.IsNullOrEmpty(q.note);   // живая заметка занимает место всегда: строки не прыгают
        r.height = RowTop + (hasNote ? NoteH : 0f) + BarH + RowGap;

        r.name.fontSize = RowSize;
        r.note.fontSize = NoteSize;
        r.count.fontSize = CountSize;
        r.icon.sprite = q.sprite != null ? q.sprite : UiKit.Icon(q.icon);
        var irt = r.icon.rectTransform;
        irt.anchorMin = irt.anchorMax = new Vector2(0f, 1f);
        irt.pivot = new Vector2(0.5f, 0.5f);                       // пульс от центра
        irt.anchoredPosition = new Vector2(IconSize * 0.5f, -RowTop * 0.5f);
        irt.sizeDelta = new Vector2(IconSize, IconSize);

        float rightEdge = CountW + CheckGap + CheckW;
        r.name.text = q.name;
        AnchorStretch(r.name.rectTransform, IconSize + 10f, rightEdge + 6f, 0f, RowTop);
        r.count.rectTransform.anchorMin = new Vector2(1f, 1f);
        r.count.rectTransform.anchorMax = new Vector2(1f, 1f);
        r.count.rectTransform.pivot = new Vector2(1f, 0.5f);       // пульс от правого края, по центру строки
        r.count.rectTransform.anchoredPosition = new Vector2(-(CheckW + CheckGap), -RowTop * 0.5f);
        r.count.rectTransform.sizeDelta = new Vector2(CountW, RowTop);
        r.count.enableAutoSizing = true;
        r.count.fontSizeMin = 20f;
        r.count.fontSizeMax = CountSize;

        r.check.rectTransform.anchorMin = r.check.rectTransform.anchorMax = new Vector2(1f, 1f);
        r.check.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        r.check.rectTransform.anchoredPosition = new Vector2(-CheckW * 0.5f, -RowTop * 0.5f);
        r.check.rectTransform.sizeDelta = new Vector2(CheckW, CheckW);

        r.note.gameObject.SetActive(hasNote);
        if (hasNote)
        {
            r.note.text = q.note ?? "";
            AnchorStretch(r.note.rectTransform, IconSize + 10f, 0f, RowTop - 4f, NoteH);
        }

        float barY = RowTop + (hasNote ? NoteH : 0f);
        AnchorStretch(r.barBack.rectTransform, 0f, 0f, barY, BarH);
        var fr = r.barFillRt;
        fr.anchorMin = new Vector2(0f, 1f);
        fr.anchorMax = new Vector2(0f, 1f);
        fr.pivot = new Vector2(0f, 1f);
        fr.anchoredPosition = new Vector2(0f, -barY);
        fr.sizeDelta = new Vector2(0f, BarH);

        // начальное состояние без пульса и звона: игрок видит цель такой, какая она есть
        r.have = q.Shown;
        r.done = q.Done;
        r.punch = 0f;
        r.checkAnim = 1f;
        r.fillTarget = Frac(q);
        r.fill = r.fillTarget;
        ApplyRowVisual(r, w);
    }

    static void AnchorStretch(RectTransform rt, float left, float right, float top, float h)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.offsetMin = new Vector2(left, -(top + h));
        rt.offsetMax = new Vector2(-right, -top);
    }

    static float Frac(ObjectiveReq q) { return q.need > 0 ? Mathf.Clamp01((float)q.have / q.need) : 0f; }

    // ---------------------------------------------------------------- живые счётчики
    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        // J: свернуть / развернуть задание с клавиатуры (не в окнах и не в диалогах)
        if (IsShown && Input.GetKeyDown(CollapseKey) && !MobileInput.ModalOpen && !DialogueManager.IsBusy) CycleMode();
        if (taskCg != null)
        {
            taskCg.alpha = Mathf.MoveTowards(taskCg.alpha, hiddenAll ? 0f : 1f, dt / 0.15f);
            taskCg.blocksRaycasts = !hiddenAll;
        }

        // сменили разрешение: размеры шрифтов пересчитать
        if (Screen.height != lastScreenH && cur != null) { placed = false; Rebuild(cur); }

        if (appear < 1f)
        {
            appear = Mathf.Min(1f, appear + dt / 0.32f);
            float e = UiDraw.EaseOutBack(appear);
            cg.alpha = Mathf.Clamp01(appear * 2.5f);
            root.anchoredPosition = placePos + new Vector2(SlideDist * (1f - e), 0f);   // новая цель выезжает справа
        }
        else if (root.anchoredPosition != placePos) root.anchoredPosition = placePos;

        TickGhosts(dt);
        TickFlyers(dt);

        Poll(false);

        for (int i = 0; i < rows.Count; i++) Tick(rows[i], dt);
        if (collapsed) TickSummary(dt);
    }

    // ---------------------------------------------------------------- свёрнутая строка
    // animate: при изменении числа строка коротко пульсирует. Текст пересобирается только при смене чисел
    void RefreshSummary(bool animate)
    {
        if (cur == null) return;
        int key = 17, have = 0, need = 0;
        for (int i = 0; i < cur.reqs.Count; i++)
        {
            var q = cur.reqs[i];
            q.Refresh();
            key = key * 31 + q.Shown;
            have += q.Shown; need += q.need;
        }
        sumFillTarget = need > 0 ? Mathf.Clamp01((float)have / need) : 0f;
        if (key == sumKey) return;
        bool had = sumKey != int.MinValue;
        sumKey = key;
        sb.Length = 0;
        for (int i = 0; i < cur.reqs.Count; i++)
        {
            var q = cur.reqs[i];
            if (i > 0) sb.Append("  <color=#8FA0BC>·</color>  ");
            if (q.Done) sb.Append("<color=#6BCB5A>");
            sb.Append(q.name).Append(' ').Append(q.Shown).Append('/').Append(q.need);
            if (q.Done) sb.Append("</color>");
        }
        summaryTxt.text = sb.ToString();
        if (animate && had) { sumPunch = 1f; MirrorPlain(); }
    }

    void TickSummary(float dt)
    {
        sumFillCur = Mathf.Lerp(sumFillCur, sumFillTarget, 1f - Mathf.Exp(-12f * dt));
        if (Mathf.Abs(sumFillCur - sumFillTarget) < 0.002f) sumFillCur = sumFillTarget;
        sumFill.rectTransform.sizeDelta = new Vector2(root.rect.width * sumFillCur, BarH);
        if (sumPunch > 0f)
        {
            sumPunch = Mathf.Max(0f, sumPunch - dt / 0.3f);
            summaryTxt.rectTransform.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin((1f - sumPunch) * Mathf.PI));
        }
        else summaryTxt.rectTransform.localScale = Vector3.one;
    }

    // animate = true: число пришло событием, пульс запускаем сразу
    void Poll(bool force)
    {
        if (collapsed && cur != null) { RefreshSummary(true); return; }
        bool changed = false;
        for (int i = 0; i < rows.Count; i++)
        {
            Row r = rows[i];
            var q = r.req;
            if (q == null) continue;
            q.Refresh();

            int shown = q.Shown;
            if (shown != r.have)
            {
                bool up = shown > r.have;
                bool wasDone = r.done;
                r.have = shown;
                r.done = q.Done;
                r.fillTarget = Frac(q);
                r.punch = 1f;
                r.punchAmp = up ? 0.22f : 0.1f;
                if (r.done && !wasDone)
                {
                    r.checkAnim = 0f;
                    r.punchAmp = 0.4f;
                    SfxManager.Play("hint", 0.8f, 1.35f);      // короткий «звон»: требование выполнено
                }
                else if (!r.done && wasDone) r.checkAnim = 1f;
                changed = true;
                ApplyRowVisual(r, root.rect.width);
            }

            // заметка («в рюкзаке 4») может поменяться и без смены числа
            if (r.note.gameObject.activeSelf)
            {
                string nt = q.note ?? "";
                if (r.note.text != nt) r.note.text = nt;
            }
        }
        if (changed) MirrorPlain();
    }

    void ApplyRowVisual(Row r, float w)
    {
        var q = r.req;
        r.count.text = r.have + "/" + q.need;
        r.count.color = r.done ? Green : White;
        r.check.gameObject.SetActive(r.done);
        r.barFill.color = r.done ? Green : UiKit.Brass;
    }

    void Tick(Row r, float dt)
    {
        // плавный прогрессбар
        r.fill = Mathf.Lerp(r.fill, r.fillTarget, 1f - Mathf.Exp(-12f * dt));
        if (Mathf.Abs(r.fill - r.fillTarget) < 0.002f) r.fill = r.fillTarget;
        r.barFillRt.sizeDelta = new Vector2(root.rect.width * r.fill, BarH);

        // пульс счётчика и иконки
        if (r.punch > 0f)
        {
            r.punch = Mathf.Max(0f, r.punch - dt / 0.32f);
            float k = 1f + r.punchAmp * Mathf.Sin((1f - r.punch) * Mathf.PI);
            r.count.rectTransform.localScale = Vector3.one * k;
            r.icon.rectTransform.localScale = Vector3.one * (1f + (k - 1f) * 0.6f);
        }
        else
        {
            r.count.rectTransform.localScale = Vector3.one;
            r.icon.rectTransform.localScale = Vector3.one;
        }

        // галочка выпрыгивает
        if (r.done && r.checkAnim < 1f)
        {
            r.checkAnim = Mathf.Min(1f, r.checkAnim + dt / 0.35f);
            r.check.rectTransform.localScale = Vector3.one * Mathf.Max(0.01f, UiDraw.EaseOutBack(r.checkAnim));
        }
        else r.check.rectTransform.localScale = Vector3.one;
    }

    // ---------------------------------------------------------------- смена цели: прежняя уезжает
    void SpawnGhost()
    {
        var go = Instantiate(root.gameObject, root.parent);
        go.name = "ObjectiveGhost";
        var comp = go.GetComponent<ObjectiveView>();
        if (comp != null) { comp.enabled = false; Destroy(comp); }    // копия только картинка: без подписок и логики
        var g = new Ghost { rt = (RectTransform)go.transform, cg = go.GetComponent<CanvasGroup>(), from = root.anchoredPosition };
        g.cg.alpha = cg.alpha;
        ghosts.Add(g);
    }

    void TickGhosts(float dt)
    {
        for (int i = ghosts.Count - 1; i >= 0; i--)
        {
            Ghost g = ghosts[i];
            if (g.rt == null) { ghosts.RemoveAt(i); continue; }
            g.t += dt / 0.28f;
            float e = UiDraw.EaseOutCubic(Mathf.Clamp01(g.t));
            g.rt.anchoredPosition = g.from + new Vector2(-SlideDist * e, 0f);
            g.cg.alpha = 1f - e;
            if (g.t >= 1f) { Destroy(g.rt.gameObject); ghosts.RemoveAt(i); }
        }
    }

    void ClearGhosts()
    {
        for (int i = 0; i < ghosts.Count; i++) if (ghosts[i].rt != null) Destroy(ghosts[i].rt.gameObject);
        ghosts.Clear();
    }

    // ---------------------------------------------------------------- сдача в стройку: иконка летит из HUD к строке
    RectTransform flyRoot;

    void OnDeposited(BuildingController b, int kind, int amount)
    {
        if (!IsShown || amount <= 0) return;
        UiIcon icon = kind == 0 ? UiIcon.Log : (kind == 1 ? UiIcon.Plank : UiIcon.Coin);

        Row row = null;
        for (int i = 0; i < rows.Count; i++)
            if (rows[i].req != null && rows[i].req.icon == icon) { row = rows[i]; break; }
        if (row == null) return;

        Vector3 from;
        if (!SourcePoint(kind, out from)) return;

        var canvas = task.GetComponentInParent<Canvas>();
        if (canvas == null) return;
        flyRoot = (RectTransform)canvas.rootCanvas.transform;

        int n = Mathf.Clamp(amount, 1, (int)MaxFlyers);
        for (int i = 0; i < n; i++)
        {
            Flyer f = flyerPool.Count > 0 ? flyerPool.Pop() : CreateFlyer();
            f.rt.SetParent(flyRoot, false);
            f.rt.sizeDelta = Vector2.one * IconSize * 0.9f;
            f.rt.rotation = Quaternion.identity;
            f.rt.localScale = Vector3.zero;
            f.img.sprite = row.icon.sprite;
            f.row = row;
            f.from = from;
            f.delay = i * 0.07f;
            f.t = 0f;
            f.dur = Random.Range(0.5f, 0.7f);
            f.spin = Random.Range(-240f, 240f);
            f.started = false;
            f.rt.gameObject.SetActive(false);
            flyers.Add(f);
        }
    }

    // откуда вылетает: счётчик того же ресурса в HUD (рюкзак/доски/монеты). Те же панели, что использует HudGain/ChopRewards
    static bool SourcePoint(int kind, out Vector3 p)
    {
        p = default(Vector3);
        if (kind == 1)
        {
            if (HudPlanks.Instance == null) return false;
            p = HudPlanks.Instance.FlyPoint;     // центр иконки в слоте досок
            return true;
        }
        var a = HudGainAnchor.Find(kind == 0 ? HudGain.Kind.Wood : HudGain.Kind.Coin);
        if (a == null) return false;
        p = a.FlyPoint;
        return true;
    }

    Flyer CreateFlyer()
    {
        var go = new GameObject("ObjectiveFly", typeof(RectTransform), typeof(Image));
        go.layer = task.gameObject.layer;
        var img = go.GetComponent<Image>();
        img.preserveAspect = true;
        img.raycastTarget = false;
        return new Flyer { rt = (RectTransform)go.transform, img = img };
    }

    void TickFlyers(float dt)
    {
        for (int i = flyers.Count - 1; i >= 0; i--)
        {
            Flyer f = flyers[i];
            if (f.rt == null) { flyers.RemoveAt(i); continue; }
            if (f.row == null || f.row.rt == null || !f.row.rt.gameObject.activeInHierarchy || flyRoot == null)
            {
                RecycleFlyer(i);
                continue;
            }

            f.delay -= dt;
            if (f.delay > 0f) continue;
            if (!f.started)
            {
                f.started = true;
                f.rt.gameObject.SetActive(true);
                f.rt.SetAsLastSibling();
            }

            f.t += dt;
            float u = Mathf.Clamp01(f.t / Mathf.Max(0.05f, f.dur));
            Vector3 tp = f.row.icon.rectTransform.position;       // цель живая: строка может пульсировать или ехать
            float dist = (tp - f.from).magnitude;
            Vector3 ctrl = (f.from + tp) * 0.5f + flyRoot.up * dist * 0.28f;
            float e = Mathf.Pow(u, 1.6f);
            f.rt.position = Bez(f.from, ctrl, tp, e);
            f.rt.localScale = Vector3.one * Mathf.Max(0.01f, UiDraw.EaseOutBack(Mathf.Clamp01(u * 4f)) * Mathf.Lerp(1f, 0.8f, e));
            f.rt.Rotate(0f, 0f, f.spin * dt);

            if (u >= 1f)
            {
                // прилёт: строка подпрыгивает, иконка гаснет
                f.row.punch = 1f;
                f.row.punchAmp = Mathf.Max(f.row.punchAmp, 0.3f);
                RecycleFlyer(i);
            }
        }
    }

    void RecycleFlyer(int i)
    {
        Flyer f = flyers[i];
        flyers.RemoveAt(i);
        if (f.rt == null) return;
        f.rt.gameObject.SetActive(false);
        f.row = null;
        flyerPool.Push(f);
    }

    void ClearFlyers()
    {
        for (int i = flyers.Count - 1; i >= 0; i--) RecycleFlyer(i);
    }

    static Vector3 Bez(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }

    // ---------------------------------------------------------------- вместе с остальным HUD
    // простой текст в objectiveText: QuestGuide по его смене понимает, что игрок продвинулся
    void MirrorPlain()
    {
        if (cur == null || ui == null || ui.objectiveText == null) return;
        string p = cur.ToPlain();
        if (ui.objectiveText.text != p) ui.objectiveText.text = p;
    }

    // панель задания подрастает вниз под список, а потом возвращается к исходной высоте
    void GrowPanel()
    {
        if (task == null) return;
        if (!Mathf.Approximately(task.anchorMin.y, task.anchorMax.y)) return;   // по высоте растянута родителем: не трогаем
        if (baseTaskH < 0f) baseTaskH = task.rect.height;

        float topGap = -placePos.y;      // расстояние от верха панели до верха списка
        float need = topGap + contentH + BottomPad;
        SetPanelHeight(collapsed ? need : Mathf.Max(baseTaskH, need));   // свёрнутое задание может быть ниже исходной панели
    }

    void RestorePanel()
    {
        if (task == null || baseTaskH < 0f) return;
        SetPanelHeight(baseTaskH);
    }

    void SetPanelHeight(float h)
    {
        float d = h - task.rect.height;
        if (Mathf.Abs(d) < 0.5f) return;
        task.sizeDelta = new Vector2(task.sizeDelta.x, task.sizeDelta.y + d);
        // верхний край панели остаётся на месте, даже если pivot не сверху
        task.anchoredPosition += new Vector2(0f, -(1f - task.pivot.y) * d);
    }
}
