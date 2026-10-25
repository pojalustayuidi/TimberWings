// Assets/_Project/Scripts/UI/Construction/BuildWindow.Build.cs (НЕ в Editor)
// BuildWindow, часть «сборка UI» (Candy). Здесь: Build, шапка, свёрнутая полоска, левая карточка со стадиями, правая колонка, кнопки.
// Основной файл (состояние, жизненный цикл, действия): BuildWindow.cs. Обновление данных и анимация: BuildWindow.Fill.cs.
//
//   Build()              канвас, затемнение, UiCandy.Window, четыре блока, подсказка клавиш, свёрнутая полоска
//   BuildHeader()        значок здания слева от названия (название и «×» делает UiCandy.Window)
//   BuildStrip()         свёрнутая полоска (облачная панель, мятная полоса)
//   BuildInfoCard()      левая карточка: значок здания, узлы стадий, название и описание этапа, чипы времени и XP
//   RebuildNodes()       пересобрать узлы стадий (число стадий у здания своё)
//   BuildNeedCard()      «Нужно для этапа»: три строки ресурсов (чип + мятная полоса)
//   BuildStatusCard()    строка состояния и полоса стройки
//   BuildButtons()       «Сдать ресурсы» (персик-лимон) и «Играть» (мята), обе «желе»
//
// Все места, где берутся спрайты UiKit, идут внутри using (UiKit.UseSkin(UiSkin.Candy)): окно собирается Candy, остальные окна ещё латунные.
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using static UiLayout;

public partial class BuildWindow
{
    // ------------------------------------------------------------ сборка UI
    void Build()
    {
        using (UiKit.UseSkin(UiSkin.Candy))
        {
            var cgo = new GameObject("BuildCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            cgo.layer = 5;
            cgo.transform.SetParent(transform, false);
            var canvas = cgo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = UiLayers.WindowLow;
            var sc = cgo.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            sc.matchWidthOrHeight = 0.5f;

            // затемнение: мягкий индиго вместо чёрного, только гасит клики по миру
            var ov = UiKit.MakeImage(cgo.transform, "Overlay", null, false);
            ov.color = new Color(0.17f, 0.18f, 0.36f, 0.4f);
            Stretch(ov.rectTransform);
            ov.raycastTarget = true;   // затемнение гасит клики по миру, но окно НЕ закрывает (промах мимо окна не должен его сбрасывать)
            overlay = ov.gameObject.AddComponent<CanvasGroup>();

            // окно: облачная панель с лавандовой лентой, «×» и CandyPopIn делает UiCandy.Window
            var win = UiCandy.Window(ov.transform, new Vector2(WinW, WinH), "", CloseOrCollapse, "Window");
            panel = win.root;
            titleText = win.title;
            if (win.close != null) win.close.navigation = new Navigation { mode = Navigation.Mode.None };   // Enter и стрелки крестик не жмут
            BuildHeader(win);

            // рабочая область по центру содержимого окна (размеры колонок остались прежними)
            body = NewRT("Body", win.content);
            Anch(body, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(ContentW, ContentH));

            BuildInfoCard(body);
            BuildNeedCard(body);
            BuildStatusCard(body);
            BuildButtons(body);

            // подсказка клавиш: светлая пилюля под окном (на затемнении голый текст не читался бы)
            var hintBg = UiKit.MakeImage(ov.transform, "HintPill", UiKit.Slot, true, 0.7f);
            hintBg.color = new Color(1f, 1f, 1f, 0.92f);
            Anch(hintBg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0, -(WinH * 0.5f + 14f)), new Vector2(800, 46));
            UiKit.MakeLabel(hintBg.transform, "Enter играть   ·   Пробел сдать ресурсы   ·   E или Esc закрыть", 22f, UiCandy.Muted, TextAlignmentOptions.Center, false);

            ov.gameObject.SetActive(false);

            BuildStrip(cgo.transform);
            built = true;
        }
    }

    // значок здания слева от названия (положение подгоняется в Refresh под длину названия)
    void BuildHeader(UiCandy.WindowRefs win)
    {
        headerIcon = UiCandy.Icon(panel, UiIcon.Log, 44f, "TitleIcon");
        headerIcon.preserveAspect = true;
        headerIconRt = headerIcon.rectTransform;
        TLC(headerIconRt, WinW * 0.5f - 112f, 27, 44, 44);
    }

    // ------------------------------------------------------------ свёрнутая полоска
    // не внутри затемнения: затемнения при свёрнутом окне нет, здание видно целиком
    void BuildStrip(Transform canvas)
    {
        stripRt = NewRT("Strip", canvas);
        Anch(stripRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, StripY), new Vector2(StripW, StripH));
        stripGroup = stripRt.gameObject.AddComponent<CanvasGroup>();
        stripGroup.alpha = 0f;
        stripGroup.blocksRaycasts = false;

        var sp = UiKit.MakePanel(stripRt, new Vector2(StripW, StripH), false, 0.7f, "StripPanel");
        Stretch(sp.rectTransform);
        sp.raycastTarget = true;
        // клик (тап) по полоске раскрывает окно
        var click = sp.gameObject.AddComponent<Button>();
        click.transition = Selectable.Transition.None;
        click.targetGraphic = sp;
        click.navigation = new Navigation { mode = Navigation.Mode.None };
        click.onClick.AddListener(Expand);

        stripTitle = Lbl(stripRt, "", 20f, UiCandy.Ink, TextAlignmentOptions.Left, true, 14, 7, StripW - 70f, 26);

        Image fill;
        var bar = UiKit.MakeBar(stripRt, UiBar.Green, new Vector2(StripW - 28f, 26f), out fill, 1f);
        TL(bar.rectTransform, 14, 38, StripW - 28f, 26f);
        stripFill = fill;
        stripFill.fillAmount = 0f;
        stripTime = UiKit.MakeLabel(bar.transform, "", 16f, UiCandy.Ink, TextAlignmentOptions.Center, true);

        // «E» справа вверху: подсказка, чем раскрыть
        var key = UiKit.MakeKeycap(stripRt, MobileInput.ActionKey.ToString(), 30f);
        Anch(key.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -6f), new Vector2(30f, 30f));

        stripRt.gameObject.SetActive(false);
    }

    // ------------------------------------------------------------ левая карточка: здание и этап
    void BuildInfoCard(Transform parent)
    {
        var card = UiKit.MakePanel(parent, new Vector2(LeftW, ContentH), false, 0.6f, "Info");
        card.color = UiCandy.TintOf(UiTone.Lavender);   // чуть темнее фона окна, чтобы карточка читалась
        TL(card.rectTransform, 0, 0, LeftW, ContentH);
        var c = card.transform;

        // крупный значок здания в мягкой «ямке»
        var slot = UiKit.MakeSlot(c, 180f, 1.1f, "BuildingSlot");
        TLC(slot.rectTransform, LeftW * 0.5f, 118, 180, 180);
        bigIcon = UiKit.MakeIcon(slot.transform, UiIcon.Log, 132f, "BuildingIcon");
        bigIcon.rectTransform.anchoredPosition = Vector2.zero;

        // стадии 1 - 2 - 3 нумерованными узлами
        nodesRoot = NewRT("Nodes", c);
        TLC(nodesRoot, LeftW * 0.5f, 252, NodesW, 48);
        stageCaption = Lbl(c, "", 26f, UiCandy.Muted, TextAlignmentOptions.Center, true, 20, 286, LeftW - 40, 34);
        stageCaption.richText = true;

        Divider(c, 30, 336, LeftW - 60);

        // название этапа и описание (описание переносится по словам)
        stageName = Lbl(c, "", 34f, UiCandy.Ink, TextAlignmentOptions.Center, true, 20, 350, LeftW - 40, 48);
        stageDesc = UiKit.MakeLabel(c, "", 23f, UiCandy.Muted, TextAlignmentOptions.Top, false);
        TL(stageDesc.rectTransform, 30, 406, LeftW - 60, 130);
        stageDesc.enableAutoSizing = true;
        stageDesc.fontSizeMin = 16f;
        stageDesc.fontSizeMax = 23f;
        stageDesc.enableWordWrapping = true;

        Divider(c, 30, 548, LeftW - 60);

        // чипы: время этапа и награда (ширину каждого подгоняет UiCandy.Chip, ряд их центрует)
        var row = NewRT("Chips", c);
        TLC(row, LeftW * 0.5f, 602, LeftW - 24f, 52f);
        var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 14f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = hlg.childControlHeight = false;
        hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;
        var tc = UiCandy.Chip(row, UiIcon.Clock, "", 52f, UiTone.Neutral, 120f);
        var xc = UiCandy.Chip(row, UiIcon.Star, "", 52f, UiTone.Lemon, 120f);
        timeText = tc.label;
        xpText = xc.label;
        xpText.richText = true;
        chipXp = xc.rt;
    }

    // нумерованные узлы стадий со звеньями между ними. Вызывается из Refresh (когда меняется число стадий), поэтому скин включаем сами
    void RebuildNodes(int total)
    {
        for (int i = nodesRoot.childCount - 1; i >= 0; i--) Destroy(nodesRoot.GetChild(i).gameObject);
        nodeRing = new Image[total]; nodeFill = new Image[total]; nodeCheck = new Image[total];
        nodeNum = new TMP_Text[total];
        links = new Image[Mathf.Max(0, total - 1)];
        targetNode = null;
        if (total <= 0) return;

        using (UiKit.UseSkin(UiSkin.Candy))
        {
            const float node = 44f, gap = 36f;
            float width = total * node + (total - 1) * gap;
            for (int i = 0; i < total; i++)
            {
                float cx = NodesW * 0.5f - width * 0.5f + node * 0.5f + i * (node + gap);
                if (i > 0)
                {
                    var l = UiKit.MakeImage(nodesRoot, "Link" + i, UiDraw.Pill, true);
                    Anch(l.rectTransform, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(cx - (node + gap) * 0.5f, 0), new Vector2(gap + 6f, 7f));
                    links[i - 1] = l;
                }
                var ring = UiKit.MakeImage(nodesRoot, "Node" + i, UiDraw.Pill, true);
                Anch(ring.rectTransform, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(cx, 0), new Vector2(node, node));
                var fill = UiKit.MakeImage(ring.transform, "Fill", UiDraw.Pill, true);
                Stretch(fill.rectTransform);
                fill.rectTransform.offsetMin = new Vector2(4f, 4f);
                fill.rectTransform.offsetMax = new Vector2(-4f, -4f);
                var num = UiKit.MakeLabel(ring.transform, (i + 1).ToString(), 24f, UiCandy.Ink, TextAlignmentOptions.Center, true);
                var ck = UiKit.MakeIcon(ring.transform, UiIcon.Check, 40f, "Check");   // Candy-галочка сама круглая и мятная: закрывает узел целиком
                ck.rectTransform.anchoredPosition = Vector2.zero;
                nodeRing[i] = ring; nodeFill[i] = fill; nodeNum[i] = num; nodeCheck[i] = ck;
            }
        }
    }

    // ------------------------------------------------------------ правая колонка
    void BuildNeedCard(Transform parent)
    {
        var card = UiKit.MakePanel(parent, new Vector2(RightW, NeedH), false, 0.6f, "Need");
        card.color = UiCandy.TintOf(UiTone.Lavender);
        TL(card.rectTransform, RightX, 0, RightW, NeedH);
        var t = card.transform;

        needTitle = Lbl(t, "Нужно для этапа", 30f, UiCandy.Ink, TextAlignmentOptions.Left, true, 24, 18, RowW, 42);

        rowWood = MakeRow(t, UiIcon.Log);
        rowPlank = MakeRow(t, UiIcon.Plank);
        rowCoin = MakeRow(t, UiIcon.Coin);

        backpackText = Lbl(t, "", 22f, UiCandy.Muted, TextAlignmentOptions.Left, false, 24, NeedH - 50, RowW, 32);
        backpackText.richText = true;
    }

    // строка ресурса: чип (значок и «сдано +несёшь/нужно») и мятная полоса справа
    Row MakeRow(Transform parent, UiIcon icon)
    {
        var r = new Row();
        r.root = NewRT("Row_" + icon, parent);
        Anch(r.root, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -RowTop), new Vector2(RowW, RowH));

        var chip = UiCandy.Chip(r.root, icon, "", 58f, UiTone.Neutral, ChipW);
        Anch(chip.rt, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(ChipW, 58f));
        r.chip = chip.bg;
        r.count = chip.label;
        r.count.richText = true;

        // галочка поверх значка в чипе, когда всё сдано (вне раскладки чипа)
        r.check = UiCandy.Icon(chip.bg.transform, UiIcon.Check, 26f, "Done");
        r.check.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        Anch(r.check.rectTransform, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(46, -14), new Vector2(26, 26));
        r.check.enabled = false;

        // полоса: заливка «сдано» и бледная заливка «сдано + то, что несёшь с собой»
        Image fillImg;
        var bar = UiKit.MakeBar(r.root, UiBar.Green, new Vector2(BarW, 34), out fillImg, 1f);
        TL(bar.rectTransform, ChipW + 14f, 21, BarW, 34);
        fillImg.fillAmount = 0f;
        r.fill = fillImg;

        var pv = UiKit.MakeImage(bar.transform, "Preview", UiKit.BarFill(UiBar.Green), false);
        pv.type = Image.Type.Filled; pv.fillMethod = Image.FillMethod.Horizontal; pv.fillOrigin = 0; pv.fillAmount = 0f;
        pv.color = new Color(1f, 1f, 1f, 0.55f);
        var prt = pv.rectTransform;
        prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
        prt.offsetMin = new Vector2(5f, 5f); prt.offsetMax = new Vector2(-5f, -5f);
        pv.transform.SetSiblingIndex(0);                       // под заливкой «сдано»
        r.preview = pv;
        return r;
    }

    void BuildStatusCard(Transform parent)
    {
        statusCard = UiKit.MakePanel(parent, new Vector2(RightW, StatusH), false, 0.6f, "Status");
        TL(statusCard.rectTransform, RightX, StatusY, RightW, StatusH);   // оттенок меняется в Refresh: мята, лимон, лаванда
        var t = statusCard.transform;

        statusText = Lbl(t, "", 30f, UiCandy.Ink, TextAlignmentOptions.Center, true, 24, 16, RowW, 44);
        statusText.richText = true;

        // полоса стройки: видна, только пока этап идёт
        Image fill;
        barFrame = UiKit.MakeBar(t, UiBar.Green, new Vector2(RowW, 38), out fill, 1f).rectTransform;
        TL(barFrame, 24, 68, RowW, 38);
        barFill = fill;
        barFill.fillAmount = 0f;
        barText = UiKit.MakeLabel(barFrame, "", 24f, UiCandy.Ink, TextAlignmentOptions.Center, true);
        barFrame.gameObject.SetActive(false);
    }

    void BuildButtons(Transform parent)
    {
        deposit = MakeBtn(parent, "Сдать ресурсы", UiBtn.Amber, new Vector2(270, BtnH), 30f, DoDeposit);
        TL(deposit.btn.GetComponent<RectTransform>(), RightX, BtnY, 270, BtnH);
        AddBtnIcon(deposit, UiIcon.Box, 44f);
        AutoSize(deposit.txt, 18f, 30f);

        play = MakeBtn(parent, "Играть", UiBtn.Green, new Vector2(336, BtnH), 46f, DoPlay);
        TL(play.btn.GetComponent<RectTransform>(), RightX + 286f, BtnY, 336, BtnH);
        AddBtnIcon(play, UiIcon.Play, 52f);
        AutoSize(play.txt, 22f, 46f);
    }

    // ------------------------------------------------------------ мелкие помощники
    // кнопка UiKit (в Candy сама получает «желе»): без навигации (иначе Enter и стрелки жали бы выбранную кнопку), индиго-надпись
    Btn MakeBtn(Transform parent, string label, UiBtn style, Vector2 size, float fontSize, UnityAction onClick)
    {
        var b = new Btn();
        b.btn = UiKit.MakeButton(parent, label, style, size, fontSize, 1f);
        b.btn.navigation = new Navigation { mode = Navigation.Mode.None };
        b.btn.onClick.AddListener(onClick);
        b.txt = b.btn.GetComponentInChildren<TMP_Text>();
        b.fg = UiCandy.Ink;
        b.txt.color = b.fg;
        b.juice = b.btn.GetComponent<CandyButtonJuice>();   // недоступная кнопка на клик «мотает головой» сама
        return b;
    }

    // значок слева на кнопке, надпись сдвигается вправо
    static void AddBtnIcon(Btn b, UiIcon icon, float size)
    {
        var ic = UiCandy.Icon(b.btn.transform, icon, size);
        Anch(ic.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(22, 2), new Vector2(size, size));
        b.txt.rectTransform.offsetMin = new Vector2(22 + size, b.txt.rectTransform.offsetMin.y);
    }

    static void Divider(Transform parent, float x, float y, float w)
    {
        var im = UiKit.MakeImage(parent, "Line", null, false);   // тонкая сплошная линия (скруглённый спрайт на 3 px исказился бы)
        im.color = UiKit.CandyLavender;
        TL(im.rectTransform, x, y, w, 3);
    }
}
