// Assets/_Project/Scripts/Build/BuildSiteTagView.Ui.cs (НЕ в Editor)
// Часть BuildSiteTagView: построение интерфейса метки (Candy) и заполнение строк.
//   Здесь: GetCanvas, NewUI/Stretch/HRow/Size/Txt, BuildUI, MakeRow, RebuildSegments, MakeChip, FormatTime, StepXp, ManyWood, NextStepName, FillRow, Refresh, MissingText.
//   В основном файле BuildSiteTagView.cs: поля, жизненный цикл, DeriveState, Update, LateUpdate, Animate.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class BuildSiteTagView
{
    // ================================================================ создание UI
    static RectTransform GetCanvas()
    {
        if (canvasRoot != null) return canvasRoot;
        var go = new GameObject("BuildSiteTags_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        go.layer = 5;
        canvasCmp = go.GetComponent<Canvas>();
        canvasCmp.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasCmp.sortingOrder = UiLayers.WorldTag;
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

    // текст Candy: круглый шрифт, без обводки (поле светлое), цвет по умолчанию индиго
    TMP_Text Txt(Transform parent, string name, float size, Color col, bool bold)
    {
        var r = NewUI(name, parent);
        var t = r.gameObject.AddComponent<TextMeshProUGUI>();
        CandyTag.Font(t, fnt);
        t.fontSize = size;
        t.color = col;
        t.alignment = TextAlignmentOptions.Center;
        t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        t.raycastTarget = false;
        return t;
    }

    const float PanelW = 330f;

    // весь интерфейс метки собирается в скине Candy (панели, слоты, полосы, иконки), потом скин возвращается прежним
    void BuildUI()
    {
        using (UiKit.UseSkin(UiSkin.Candy)) BuildUIInner();
    }

    void BuildUIInner()
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
        CandyTag.StylePanel(bg, 0.5f);

        var v = rt.GetComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(22, 22, 16, 22);
        v.spacing = 9f;
        v.childAlignment = TextAnchor.UpperCenter;
        v.childControlWidth = v.childControlHeight = true;
        v.childForceExpandWidth = v.childForceExpandHeight = false;
        var fit = rt.GetComponent<ContentSizeFitter>();
        fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        CandyTag.AddTail(rt, TailW);                              // хвостик-указатель под панелью

        // рамка поверх фона
        var fr = NewUI("Frame", rt, typeof(Image), typeof(LayoutElement));
        fr.GetComponent<LayoutElement>().ignoreLayout = true;
        Stretch(fr, -6f);                                       // чуть шире панели: получается мягкое свечение вокруг (мятное «можно сдать», лимонное «строим»)
        frame = fr.GetComponent<Image>();
        frame.sprite = UiDraw.PillRing; frame.type = Image.Type.Sliced; frame.color = new Color(1f, 1f, 1f, 0f); frame.raycastTarget = false;   // свечение: зелёное «можно сдать», янтарное «строим»

        // --- шапка: персиковая лента с названием
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
        title = Txt(head, "Title", 30f, CandyTag.Ink, true);
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
        dv.color = new Color(UiCandy.Lavender.r, UiCandy.Lavender.g, UiCandy.Lavender.b, 0.9f); dv.raycastTarget = false;

        // --- строки ресурсов
        rowWood = MakeRow("Wood", CandyTag.Icon(UiIcon.Log), UiBar.Green);
        rowPlank = MakeRow("Plank", CandyTag.Icon(UiIcon.Plank), UiBar.Blue);
        rowCoin = MakeRow("Coin", CandyTag.Icon(UiIcon.Coin), UiBar.Amber);

        // --- плашки с иконками: сколько строится, награда, сколько брёвен нужно на все стадии
        infoRow = NewUI("InfoRow", rt);
        HRow(infoRow, 10f, TextAnchor.MiddleCenter);
        Size(infoRow, PanelW, 38f);
        chipTime = MakeChip(infoRow, UiIcon.Clock, out txtTime);
        chipXp = MakeChip(infoRow, UiIcon.Star, out txtXp);

        // --- таймер стройки: рамка и персиковая заливка Candy из UiKit
        var timerRow = NewUI("Timer", rt);
        HRow(timerRow, 10f);
        Size(timerRow, PanelW, 28f);
        bar = timerRow;
        Image tf;
        var track = UiKit.MakeBar(timerRow, UiBar.Amber, new Vector2(200f, 22f), out tf, 0.55f);
        Size(track.rectTransform, 200f, 24f, 1f);
        barFill = tf;
        timerText = Txt(timerRow, "TimerText", 22f, CandyTag.Ink, true);
        timerText.alignment = TextAlignmentOptions.MidlineRight;
        Size(timerText.rectTransform, 84f, 24f);

        // --- подсказка в мягкой «ямке» и «ГОТОВО»
        hintRow = NewUI("HintRow", rt, typeof(Image));
        var hl = HRow(hintRow, 0f);
        hl.padding = new RectOffset(14, 14, 8, 10);
        var hbg = hintRow.GetComponent<Image>();
        hbg.sprite = UiKit.Slot; hbg.type = Image.Type.Sliced; hbg.pixelsPerUnitMultiplier = 1f / 0.6f; hbg.raycastTarget = false;
        Size(hintRow, PanelW, -1f);
        hint = Txt(hintRow, "Hint", 23f, CandyTag.Ink, false);
        hint.enableWordWrapping = true;
        Size(hint.rectTransform, PanelW - 28f, -1f);
        doneText = Txt(rt, "Done", 34f, Good, true);
        doneText.text = "ГОТОВО";
    }

    Row MakeRow(string name, Sprite icon, UiBar barColor)
    {
        var row = new Row();
        row.root = NewUI("Row_" + name, rt);
        HRow(row.root, 10f);
        Size(row.root, PanelW, 44f);

        // значок ресурса в мягкой «ямке» Candy
        var slot = UiKit.MakeSlot(row.root, 42f, 0.5f);
        Size(slot.rectTransform, 42f, 42f);
        var ic = NewUI("Icon", slot.transform, typeof(Image));
        Stretch(ic, 5f);
        var im = ic.GetComponent<Image>();
        im.sprite = icon; im.preserveAspect = true; im.raycastTarget = false;

        // полоса Candy из UiKit: заливка «сдано» и бледная заливка «сдано + то, что несёшь с собой»
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

        row.count = Txt(row.root, "Count", 31f, CandyTag.Ink, true);
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
            var num = Txt(n, "Num", 21f, CandyTag.Ink, true);
            Stretch(num.rectTransform, 0f);
            num.text = (i + 1).ToString();
            var ck = NewUI("Check", n, typeof(Image));
            Stretch(ck, 8f);
            var ci = ck.GetComponent<Image>();
            ci.sprite = CandyTag.Icon(UiIcon.Check); ci.preserveAspect = true; ci.raycastTarget = false;
            nodeRing[i] = ring; nodeFill[i] = fi; nodeNum[i] = num; nodeCheck[i] = ci;
        }
    }

    // плашка: значок + текст в мягкой ямке
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
        im.sprite = CandyTag.Icon(icon); im.preserveAspect = true; im.raycastTarget = false;
        label = Txt(chip, "Text", 24f, CandyTag.Ink, true);
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
        return "  <color=#9A5F00>·</color>  всего <color=#9A5F00>" + n + "</color> " + UiDraw.Plural(n, "бревно", "бревна", "бревен");
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

    void FillRow(Row r, int have, int carry, int total, string complete)
    {
        bool on = total > 0;
        SetRow(r.root, on);
        if (!on) return;

        int add = Mathf.Clamp(carry, 0, Mathf.Max(0, total - have));
        r.target = Mathf.Clamp01(have / (float)total);
        r.targetPreview = Mathf.Clamp01((have + add) / (float)total);

        bool done = have >= total;
        string col = done ? "#2A9573" : "#2B2F5C";           // сдано: зелёное, пока не всё; индиго
        string plus = (!done && add > 0) ? " <size=75%><color=#9A5F00>+" + add + "</color></size>" : "";
        r.count.text = "<color=" + col + ">" + have + "</color>" + plus + "<color=#5E6A96>/" + total + "</color>";
    }

    void Refresh(int wood, int planks, int coins, int cur, int tot, float prog)
    {
        bool staged = tot > 1;
        bool done = state == State.Done, locked = state == State.Locked, building = state == State.InProgress;

        bool upgrade = staged && bc != null && bc.TutorialFired && !done && !locked;
        title.text = upgrade ? buildingName + " <size=65%><color=#5E6A96>улучшение</color></size>" : buildingName;
        title.color = locked ? Dim : CandyTag.Ink;

        // стадии: пройденные зелёные с галочкой, следующая янтарная и пульсирует, будущие тусклые
        bool compactView = Compact;
        SetRow(stageRow, staged && !locked && !compactView);
        SetRow(stageCaption, staged && !locked && !compactView);
        targetNode = null;
        for (int i = 0; i < nodeRing.Length; i++)
        {
            bool reached = i < cur;
            bool target = i == cur && !done;
            // пройденные мятные с галочкой, следующая лимонная и «дышит», будущие облачно-лавандовые
            Color ringC = reached ? Good : (target ? UiCandy.PeachDeep : NodeDim);
            Color fillC = reached ? UiCandy.Mint : (target ? UiCandy.LemonLight : UiCandy.Cloud);
            nodeRing[i].color = ringC;
            nodeFill[i].color = fillC;
            nodeCheck[i].enabled = reached;
            nodeNum[i].enabled = !reached;
            nodeNum[i].color = target ? CandyTag.Ink : Dim;
            nodeRing[i].rectTransform.localScale = Vector3.one;
            if (target) targetNode = nodeRing[i].rectTransform;
            if (i > 0) links[i - 1].color = reached ? Good : NodeDim;
        }
        if (staged)
        {
            string next = NextStepName(cur);
            stageCaption.text = done
                ? "<color=#2A9573>Построено полностью</color>"
                : "Стадия " + cur + " из " + tot + (next.Length > 0 ? "  <color=#9A5F00>·</color>  <color=#9A5F00>" + next + "</color>" : ManyWood());
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
            timerText.text = "<size=70%><color=#5E6A96>ещё</color></size> " + FormatTime(left);
        }

        // плашки: сколько будет строиться, что получишь, сколько брёвен нужно на все стадии
        bool showInfo = !locked && !done && !building && bc != null && !compactView;
        SetRow(infoRow, showInfo);
        if (showInfo)
        {
            txtTime.text = FormatTime(bc.BuildTime);
            int xp = StepXp(cur);
            SetRow(chipXp, xp > 0);
            if (xp > 0) txtXp.text = "<color=#9A5F00>+" + xp + " XP</color>";
        }

        canContribute = bc != null && inv != null && !locked && !done && bc.PlayerCanContribute(inv);

        // подсказка
        string h;
        if (Time.unscaledTime < bannerUntil) h = bannerText;
        else if (locked) h = "Откроется после: " + lockedAfter;
        else if (building) h = "Строим…";
        else if (done) h = "";
        else if (Time.unscaledTime < flashUntil)
            h = "<color=#C8365C>Нужно ещё " + flashMissing + " " + UiDraw.Plural(flashMissing, "бревно", "бревна", "бревен") + "</color>";
        else if (compactView && bc != null) h = bc.AllDeposited ? "<color=#2A9573>Всё сдано</color>" : bc.NeedSummary(inv);   // одна строка: «Бревна 3/8 · Доски 0/6»
        else if (canContribute && bc != null && !bc.AllDeposited) h = "<color=#2A9573>Встань рядом, чтобы сдать</color>";
        else h = MissingText();
        hint.text = CandyTag.Readable(h);       // на случай светлых цветов в чужих строках (NeedSummary и т. п.)
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

}
