// Assets/_Project/Scripts/UI/Workers/WorkersWindow.Build.cs (НЕ в Editor)
// ЧАСТЬ класса WorkersWindow (partial): помощники кнопок и построение окна из кода.
// Это кусок одного класса, поля и методы видны из других частей. Файл с объявлением и состоянием: WorkersWindow.cs.
// Остальные части: WorkersWindow.Tabs.cs, WorkersWindow.PackOpen.cs.
// CANDY: окно собирается под using UseSkin(Candy) (Build -> BuildCore); кнопки «желе» получают сами, цвета текста индиго.
// Методы этой части: MakeBtn, SetBtn, Restyle, Wrap, Icon, Build, BuildHeader, BuildPackCaseArt, BuildBrigade, BuildDetail, BuildStock, BuildPacks, BuildCollectionShell, BuildCollection, BuildOpenOverlay, BuildOddsOverlay.

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UiLayout;

public partial class WorkersWindow
{
    // ============================================================ помощники кнопок
    Btn MakeBtn(Transform parent, string label, UiBtn style, float x, float y, float w, float h, float fs, UnityEngine.Events.UnityAction a)
    {
        var b = UiKit.MakeButton(parent, label, style, new Vector2(w, h), fs, 1f);
        TL((RectTransform)b.transform, x, y, w, h);
        b.onClick.AddListener(a);
        var t = b.GetComponentInChildren<TMP_Text>();
        AutoSize(t, fs * 0.55f, fs);
        return new Btn { b = b, t = t, style = style, fg = Ink };
    }

    static void SetBtn(Btn b, bool on, string text)
    {
        if (b == null) return;
        b.b.interactable = on;
        if (text != null) b.t.text = text;
        b.t.color = on ? b.fg : Soft;
    }

    // сменить цвет кнопки (выбранная вкладка персиковая, остальные облачные). UiCandy.SetStyle сам берёт спрайты Candy
    static void Restyle(Btn b, UiBtn style)
    {
        if (b.style == style) return;
        b.style = style;
        UiCandy.SetStyle(b.b, style);
        b.fg = Ink;
        b.t.color = Ink;
    }

    // подпись с переносом строк и автоподбором размера
    static TMP_Text Wrap(Transform p, string text, float size, Color c, TextAlignmentOptions al, bool bold, float x, float y, float w, float h)
    {
        var t = UiKit.MakeLabel(p, text, size, c, al, bold);
        TL(t.rectTransform, x, y, w, h);
        t.enableAutoSizing = true;
        t.fontSizeMin = Mathf.Max(10f, size * 0.55f);
        t.fontSizeMax = size;
        t.enableWordWrapping = true;
        return t;
    }

    Image Icon(Transform p, UiIcon icon, float cx, float cy, float size)
    {
        var im = UiKit.MakeIcon(p, icon, size, "Icon");
        TLC(im.rectTransform, cx, cy, size, size);
        im.raycastTarget = false;
        return im;
    }

    // ============================================================ построение окна
    void Build()
    {
        using (UiKit.UseSkin(UiSkin.Candy)) BuildCore();
    }

    void BuildCore()
    {
        if (built) return;

        var cgo = new GameObject("WorkersCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cgo.layer = 5;
        cgo.transform.SetParent(transform, false);
        var canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = UiLayers.Window;
        var sc = cgo.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        sc.matchWidthOrHeight = 0.5f;

        // затемнение гасит клики по миру, но окно НЕ закрывает (промах мимо окна не должен его сбрасывать)
        var ov = UiKit.MakeImage(cgo.transform, "Overlay", null, false);
        ov.color = new Color(0.17f, 0.18f, 0.36f, 0.5f);   // мягкий индиго вместо чёрного
        Stretch(ov.rectTransform);
        ov.raycastTarget = true;
        overlay = ov.gameObject.AddComponent<CanvasGroup>();

        // окно: облачная панель с лавандовой лентой, «×» и CandyPopIn делает UiCandy.Window
        var win = UiCandy.Window(ov.transform, new Vector2(WinW, WinH), "Лагерь", OnCloseButton, "Window");
        panel = win.root;
        titleText = win.title;
        if (win.close != null) win.close.navigation = new Navigation { mode = Navigation.Mode.None };   // Enter и стрелки крестик не жмут
        BuildHeader();

        // внутренняя светлая панель: все координаты вкладок и страниц остались прежними (от её левого верхнего угла)
        var innerImg = UiKit.MakePanel(win.content, new Vector2(1322f, 807f), false, 0.6f, "Inner");
        innerImg.color = UiCandy.TintOf(UiTone.Lavender);
        Anch(innerImg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1322f, 807f));
        inner = innerImg.rectTransform;

        // вкладки
        string[] names = { "Бригада", "Паки", "Коллекция" };
        UiIcon[] icons = { UiIcon.Person, UiIcon.Box, UiIcon.Book };
        for (int i = 0; i < 3; i++)
        {
            int ti = i;
            tabBtns[i] = MakeBtn(inner, "     " + names[i] + " [" + (i + 1) + "]", i == 0 ? UiBtn.Amber : UiBtn.Gray, PageX + i * 262f, 14f, 252f, 52f, 26f, () => { if (phase == Phase.None && !oddsOpen) SetTab((Tab)ti); });
            var ic = UiKit.MakeIcon(tabBtns[i].b.transform, icons[i], 30f, "TabIcon");
            Anch(ic.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(34f, 2f), new Vector2(30f, 30f));
            ic.raycastTarget = false;
        }

        for (int i = 0; i < 3; i++)
        {
            pages[i] = NewRT("Page" + i, inner);
            TL(pages[i], PageX, PageY, PageW, PageH);
        }
        BuildBrigade(pages[0]);
        BuildPacks(pages[1]);
        BuildCollectionShell(pages[2]);
        BuildOpenOverlay();
        table = new WorkerPackTable(inner);
        table.onClosed = OnTableClosed;
        table.onPaid = MarkDirty;
        table.onShake = () => { shakeT = 0.45f; };
        BuildOddsOverlay();

        ov.gameObject.SetActive(false);
        built = true;
        open = false;
        SetTab(Tab.Brigade);
    }

    // монеты: чип слева на ленте (название и «×» делает UiCandy.Window)
    void BuildHeader()
    {
        var cc = UiCandy.Chip(panel, UiIcon.Coin, "0", 40f, UiTone.Peach, 96f);
        Anch(cc.rt, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(34f, -26f), new Vector2(96f, 40f));
        coinsText = cc.label;
        coinsText.color = UiCandy.GoldText;
    }

    // подарочная коробка пака в цвете пака: мягкая тень, корпус, крышка, белая лента с бантом-камнем, блик
    void BuildPackCaseArt(Transform parent, Color accent, float x, float y)
    {
        var glow = UiKit.MakeImage(parent, "CaseHalo", PetUi.GlowSprite, false);
        glow.color = new Color(accent.r, accent.g, accent.b, 0.3f);
        glow.raycastTarget = false;
        TLC(glow.rectTransform, x, y, 228, 220);

        var shadow = UiKit.MakeImage(parent, "CaseShadow", UiKit.Slot, true, 0.8f);
        shadow.color = new Color(Ink.r, Ink.g, Ink.b, 0.16f);
        shadow.raycastTarget = false;
        TLC(shadow.rectTransform, x + 3, y + 40, 150, 40);

        var body = UiKit.MakeImage(parent, "CaseBox", UiKit.Panel, true, 0.5f);
        body.color = Color.Lerp(Color.white, accent, 0.55f);
        body.raycastTarget = false;
        TLC(body.rectTransform, x, y + 6, 124, 92);

        var lid = UiKit.MakeImage(parent, "CaseLid", UiKit.Panel, true, 0.4f);
        lid.color = Color.Lerp(Color.white, accent, 0.9f);
        lid.raycastTarget = false;
        TLC(lid.rectTransform, x, y - 36, 140, 38);

        var band = UiKit.MakeImage(parent, "CaseRibbon", UiDraw.Pill, true, 0.3f);
        band.color = new Color(1f, 1f, 1f, 0.92f);
        band.raycastTarget = false;
        TLC(band.rectTransform, x, y + 2, 18, 100);

        var gem = UiKit.MakeIcon(parent, UiIcon.Gem, 40f, "CaseRarityGem");
        gem.raycastTarget = false;
        TLC(gem.rectTransform, x, y - 30, 40, 40);

        var shine = UiKit.MakeImage(parent, "CaseGlint", UiDraw.Pill, true, 0.2f);
        shine.color = new Color(1f, 1f, 1f, 0.8f);
        shine.raycastTarget = false;
        TLC(shine.rectTransform, x - 46, y + 14, 8, 44);
    }

    // ------------------------------------------------------------ Бригада
    void BuildBrigade(RectTransform page)
    {
        slotsLabel = Lbl(page, "", 24f, Ink, TextAlignmentOptions.Left, true, 0, 0, 800, 28);

        // 6 слотов: 3 в ряд
        for (int i = 0; i < CampSimulation.Slots; i++)
        {
            int si = i;
            float x = (i % 3) * 274f, y = 32f + (i / 3) * 250f;

            var card = WorkerCardView.Create(page, 252f, 240f, "Slot" + i);
            TL(card.rt, x, y, 252f, 240f);
            AddClick(card.frame, () => ClickSlot(si));
            slotCards[i] = card;

            var empty = UiKit.MakeImage(page, "EmptySlot" + i, UiKit.Slot, true, 1f);
            empty.color = new Color(0.86f, 0.89f, 0.98f, 0.9f);
            TL(empty.rectTransform, x, y, 252f, 240f);
            AddClick(empty, () => ClickSlot(si));
            var plus = UiKit.MakeIcon(empty.transform, UiIcon.Person, 64f, "Ghost");
            Anch(plus.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(64, 64));
            plus.color = new Color(1f, 1f, 1f, 0.3f);
            plus.raycastTarget = false;
            var tx = UiKit.MakeLabel(empty.transform, "Свободный слот", 22f, Dim, TextAlignmentOptions.Center, false);
            Anch(tx.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 14), new Vector2(230, 80));
            tx.enableWordWrapping = true;
            slotEmpty[i] = empty; slotEmptyText[i] = tx;
        }

        reserveLabel = Lbl(page, "Запас", 24f, Ink, TextAlignmentOptions.Left, true, 0, 528, 800, 28);
        var vp = NewRT("ReserveView", page);
        TL(vp, 0, 558, 800, 157);
        var vpImg = vp.gameObject.AddComponent<Image>();
        vpImg.color = new Color(0f, 0f, 0f, 0.001f);
        vp.gameObject.AddComponent<RectMask2D>();
        reserveContent = NewRT("Content", vp);
        Anch(reserveContent, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(800, 156));
        var sr = vp.gameObject.AddComponent<ScrollRect>();
        sr.viewport = vp; sr.content = reserveContent;
        sr.horizontal = true; sr.vertical = false;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.scrollSensitivity = 40f;
        // ScrollRect's horizontal mode doesn't consume the mouse wheel by default.
        var wheel = vp.gameObject.AddComponent<WorkerReserveWheel>();
        wheel.Target = reserveContent;

        BuildDetail(page);
        BuildStock(page);
    }

    void BuildDetail(RectTransform page)
    {
        var p = UiKit.MakePanel(page, new Vector2(476, 490), false, 0.6f, "Detail");
        TL(p.rectTransform, 814, 0, 476, 490);
        var root = p.rectTransform;

        detailEmpty = NewRT("Empty", root).gameObject;
        Stretch((RectTransform)detailEmpty.transform);
        Wrap(detailEmpty.transform, "В лагере пока никого.\nОткрой пак на вкладке «Паки»: первый бесплатный подарок за палатку.", 24f, Soft, TextAlignmentOptions.Center, false, 30, 120, 416, 120);
        MakeBtn(detailEmpty.transform, "К пакам", UiBtn.Amber, 110, 270, 256, 56, 28f, () => SetTab(Tab.Packs));

        detailBody = NewRT("Body", root).gameObject;
        Stretch((RectTransform)detailBody.transform);
        var b = detailBody.transform;

        detailCard = WorkerCardView.Create(b, 132f, 176f, "DetailCard");
        detailCard.showRole = false;
        TL(detailCard.rt, 16, 16, 132, 176);

        dName = Lbl(b, "", 30f, Ink, TextAlignmentOptions.Left, true, 164, 12, 300, 40);
        dAge = Lbl(b, "", 21f, Soft, TextAlignmentOptions.Left, false, 164, 52, 300, 26);
        dGem = Icon(b, UiIcon.Gem, 180, 100, 28);
        dRarity = Lbl(b, "", 24f, Ink, TextAlignmentOptions.Left, true, 200, 84, 130, 32);
        for (int i = 0; i < 5; i++) dStars[i] = Icon(b, UiIcon.Star, 352 + i * 24, 100, 22);
        dRoleIcon = Icon(b, UiIcon.Coin, 180, 134, 28);
        dRole = Lbl(b, "", 24f, Ink, TextAlignmentOptions.Left, true, 200, 118, 260, 32);
        dLevel = Lbl(b, "", 20f, Soft, TextAlignmentOptions.Left, false, 164, 154, 300, 26);
        Image xf;
        var bar = UiKit.MakeBar(b, UiBar.Green, new Vector2(296, 24), out xf, 0.8f);   // опыт: мятная полоса
        TL(bar.rectTransform, 164, 182, 296, 24);
        dXpFill = xf;
        dXp = UiKit.MakeLabel(bar.transform, "", 15f, Ink, TextAlignmentOptions.Center, true);

        dProd = Wrap(b, "", 20f, Ink, TextAlignmentOptions.Left, false, 16, 214, 444, 30);
        dStatusIcon = Icon(b, UiIcon.Clock, 30, 262, 26);
        dStatus = Lbl(b, "", 21f, Ink, TextAlignmentOptions.Left, true, 50, 248, 410, 28);

        for (int i = 0; i < 5; i++)
        {
            float y = 282f + i * 30f;
            perkIcon[i] = Icon(b, UiIcon.Check, 30, y + 15, 24);
            perkName[i] = Lbl(b, "", 20f, Ink, TextAlignmentOptions.Left, true, 50, y, 150, 30);
            perkDesc[i] = Wrap(b, "", 17f, Good, TextAlignmentOptions.Left, false, 204, y, 262, 30);
        }

        dAction = MakeBtn(b, "Убрать из слота", UiBtn.Yellow, 100, 438, 276, 46, 24f, () => { });
    }

    void BuildStock(RectTransform page)
    {
        var p = UiKit.MakePanel(page, new Vector2(476, 217), false, 0.6f, "Stock");
        TL(p.rectTransform, 814, 498, 476, 217);
        var b = p.transform;

        Icon(b, UiIcon.Tent, 34, 26, 32);
        Lbl(b, "Склад лагеря", 24f, Ink, TextAlignmentOptions.Left, true, 58, 8, 170, 36);
        stockMsg = Lbl(b, "", 17f, Warn, TextAlignmentOptions.Right, true, 230, 8, 230, 36);

        Icon(b, UiIcon.Log, 34, 70, 34);
        Image lf;
        var lb = UiKit.MakeBar(b, UiBar.Amber, new Vector2(270, 26), out lf, 0.8f);
        TL(lb.rectTransform, 58, 56, 270, 26);
        logFill = lf;
        logText = Lbl(b, "", 21f, Ink, TextAlignmentOptions.Left, true, 338, 54, 130, 30);

        Icon(b, UiIcon.Plank, 34, 108, 34);
        Image pf;
        var pb = UiKit.MakeBar(b, UiBar.Green, new Vector2(270, 26), out pf, 0.8f);
        TL(pb.rectTransform, 58, 94, 270, 26);
        plankFill = pf;
        plankText = Lbl(b, "", 21f, Ink, TextAlignmentOptions.Left, true, 338, 92, 130, 30);

        stockPreview = Wrap(b, "", 17f, Soft, TextAlignmentOptions.Left, false, 16, 126, 446, 26);
        takeBtn = MakeBtn(b, "Забрать  [Пробел]", UiBtn.Green, 14, 156, 222, 48, 21f, DoTake);
        sellBtn = MakeBtn(b, "Продать −" + Mathf.RoundToInt((1f - WorkerBalance.SellRate) * 100f) + "%", UiBtn.Yellow, 240, 156, 222, 48, 22f, DoSell);
    }

    // ------------------------------------------------------------ Паки
    void BuildPacks(RectTransform page)
    {
        for (int t = 0; t < 3; t++)
        {
            int ti = t;
            var type = (WorkerPackType)t;
            var u = new PackUi();
            packUi[t] = u;
            Color pc = WorkerUi.PackColor(type);

            var holder = NewRT("Pack" + t, page);
            TL(holder, t * 437f, 0, 416, 715);

            u.hl = UiKit.MakeImage(holder, "Highlight", UiKit.Slot, true, 1f);
            u.hl.rectTransform.anchorMin = Vector2.zero; u.hl.rectTransform.anchorMax = Vector2.one;
            u.hl.rectTransform.offsetMin = new Vector2(-7, -7); u.hl.rectTransform.offsetMax = new Vector2(7, 7);
            u.hl.raycastTarget = false;

            var pn = UiKit.MakePanel(holder, new Vector2(416, 715), false, 0.6f, "Panel");
            Stretch(pn.rectTransform);
            AddClick(pn, () => { packSel = ti; dirty = true; });
            var b = pn.transform;

            Lbl(b, WorkerText.Pack(type), 32f, WorkerUi.PackText(type), TextAlignmentOptions.Center, true, 16, 12, 384, 42);

            u.artGlow = UiKit.MakeImage(b, "Glow", PetUi.GlowSprite, false);
            u.artGlow.color = new Color(pc.r, pc.g, pc.b, 0.5f);
            u.artGlow.raycastTarget = false;
            TLC(u.artGlow.rectTransform, 208, 140, 280, 280);
            // Custom camp-style pack case: no legacy Box icon; layered wood, brass bands and a rarity gem.
            BuildPackCaseArt(b, pc, 208f, 140f);

            u.ribbon = UiKit.MakeImage(b, "Ribbon", UiDraw.Pill, true, 0.3f);
            u.ribbon.color = UiCandy.Mint;
            TL(u.ribbon.rectTransform, 268, 66, 130, 34);
            u.ribbon.raycastTarget = false;
            u.ribbonText = UiKit.MakeLabel(u.ribbon.transform, "ПОДАРОК", 20f, Ink, TextAlignmentOptions.Center, true);

            u.priceIcon = Icon(b, UiIcon.Coin, 78, 252, 34);
            u.priceText = Lbl(b, "", 26f, Ink, TextAlignmentOptions.Left, true, 102, 234, 300, 36);

            Lbl(b, "Шансы выпадения", 19f, WorkerUi.Dim, TextAlignmentOptions.Left, false, 16, 284, 384, 24);
            for (int r = 0; r < 5; r++)
            {
                float y = 310f + r * 34f;
                u.oddIcon[r] = Icon(b, UiIcon.Gem, 36, y + 17, 26);
                u.oddName[r] = Lbl(b, WorkerText.Rarity((WorkerRarity)r), 21f, Ink, TextAlignmentOptions.Left, true, 56, y, 132, 34);
                RectTransform f;
                var bar = WorkerUi.MiniBar(b, WorkerUi.BarBack, WorkerUi.RarityAccent((WorkerRarity)r), out f);
                TL(bar.rectTransform, 192, y + 11, 130, 13);
                u.oddFill[r] = f;
                u.oddPct[r] = Lbl(b, "", 21f, Ink, TextAlignmentOptions.Right, true, 326, y, 76, 34);
            }

            Lbl(b, "Гарантии", 19f, WorkerUi.Dim, TextAlignmentOptions.Left, false, 16, 486, 384, 24);
            u.guar[0] = Lbl(b, "", 19f, Soft, TextAlignmentOptions.Left, false, 16, 510, 384, 26);
            u.guar[1] = Lbl(b, "", 19f, Soft, TextAlignmentOptions.Left, false, 16, 536, 384, 26);
            u.guar[2] = Lbl(b, "", 19f, Soft, TextAlignmentOptions.Left, true, 16, 562, 384, 26);
            u.mythBack = WorkerUi.MiniBar(b, WorkerUi.BarBack, WorkerUi.RarityAccent(WorkerRarity.Mythic), out u.mythFill);
            TL(u.mythBack.rectTransform, 16, 592, 384, 12);

            u.reason = Lbl(b, "", 19f, Warn, TextAlignmentOptions.Center, true, 16, 610, 384, 26);
            u.open = MakeBtn(b, "Открыть", UiBtn.Amber, 16, 644, 236, 58, 27f, () => { packSel = ti; StartOpen(type); });
            u.odds = MakeBtn(b, "Шансы", UiBtn.Gray, 262, 644, 138, 58, 24f, () => OpenOdds(type));
        }
    }

    // ------------------------------------------------------------ Коллекция
    void BuildCollectionShell(RectTransform page)
    {
        collCount = Lbl(page, "Собрано 0/27", 26f, Ink, TextAlignmentOptions.Left, true, 0, 0, 500, 36);
        Lbl(page, "Нажми на карточку, чтобы посмотреть подробности", 19f, Dim, TextAlignmentOptions.Right, false, 600, 6, 690, 28);

        // подписи рядов и подробности строятся сразу, карточки по базе: при первом показе
        for (int r = 0; r < 3; r++)
        {
            float y = 44f + r * 164f;
            Icon(page, WorkerUi.RoleIcon((WorkerRole)r), 35, y + 44, 44);
            Lbl(page, WorkerUi.RolePlural((WorkerRole)r), 15f, Ink, TextAlignmentOptions.Center, true, 0, y + 70, 72, 22);
            collRoleCount[r] = Lbl(page, "0/9", 18f, Soft, TextAlignmentOptions.Center, true, 0, y + 94, 72, 24);
        }

        var dp = UiKit.MakePanel(page, new Vector2(PageW, 179), false, 0.6f, "CollDetail");
        TL(dp.rectTransform, 0, 536, PageW, 179);
        cdBody = NewRT("Body", dp.rectTransform).gameObject;
        Stretch((RectTransform)cdBody.transform);
        var b = cdBody.transform;

        cdCard = WorkerCardView.Create(b, 112f, 150f, "CollDetailCard");
        cdCard.showRole = false;
        TL(cdCard.rt, 16, 14, 112, 150);
        cdName = Lbl(b, "", 32f, Ink, TextAlignmentOptions.Left, true, 146, 8, 500, 42);
        cdLine = Lbl(b, "", 21f, Soft, TextAlignmentOptions.Left, false, 146, 50, 500, 26);
        cdGem = Icon(b, UiIcon.Gem, 162, 98, 28);
        cdRarity = Lbl(b, "", 23f, Ink, TextAlignmentOptions.Left, true, 184, 82, 340, 32);
        for (int i = 0; i < 5; i++) cdStars[i] = Icon(b, UiIcon.Star, 540 + i * 22, 98, 20);
        cdState = Wrap(b, "", 21f, Ink, TextAlignmentOptions.Left, false, 146, 118, 520, 52);
        cdPerks = Wrap(b, "", 19f, Ink, TextAlignmentOptions.TopLeft, false, 690, 10, 580, 100);
        cdBio = Wrap(b, "", 19f, Soft, TextAlignmentOptions.TopLeft, false, 690, 114, 580, 56);
        cdBio.fontStyle = FontStyles.Italic;
    }

    void BuildCollection(WorkerManager m)
    {
        if (m.database == null || m.database.workers == null || m.database.workers.Count == 0)
        {
            collCount.text = "База рабочих не назначена (Tools > BuilderSim > Настроить рабочих)";
            cdBody.SetActive(false);
            return;
        }

        // ряд на роль, внутри по редкости (порядок в базе сохраняется)
        var page = pages[2];
        for (int r = 0; r < 3; r++)
        {
            var list = new List<WorkerDefinition>();
            for (int k = 0; k < 5; k++)
                for (int i = 0; i < m.database.workers.Count; i++)
                {
                    var d = m.database.workers[i];
                    if (d != null && d.role == (WorkerRole)r && d.rarity == (WorkerRarity)k) list.Add(d);
                }
            for (int i = 0; i < list.Count && i < 9; i++)
            {
                var d = list[i];
                var c = WorkerCardView.Create(page, 118f, 150f, "Coll_" + d.id);
                c.showRole = false;
                TL(c.rt, 78f + i * 126f, 44f + r * 164f + 4f, 118f, 150f);
                var dd = d;
                AddClick(c.frame, () => { selDef = dd; dirty = true; });
                collDefs.Add(d);
                collCards.Add(c);
            }
        }
        collBuilt = true;
    }

    // ------------------------------------------------------------ экран открытия пака
    void BuildOpenOverlay()
    {
        openRoot = NewRT("Opening", inner).gameObject;
        var rt = (RectTransform)openRoot.transform;
        Stretch(rt);
        var dimImg = UiKit.MakeImage(rt, "Dim", null, false);
        dimImg.color = new Color(0.96f, 0.97f, 1f, 0.97f);   // светлое облако: лучи и вспышки редкости читаются, текст индиго
        Stretch(dimImg.rectTransform);
        dimImg.raycastTarget = true;

        var mid = new Vector2(0.5f, 0.5f);
        openRays = UiKit.MakeImage(rt, "Rays", PetUi.RaysSprite, false);
        raysRt = openRays.rectTransform;
        Anch(raysRt, mid, mid, CardPos, new Vector2(860, 860));
        openRays.raycastTarget = false;

        openGlow = UiKit.MakeImage(rt, "Glow", PetUi.GlowSprite, false);
        glowRt = openGlow.rectTransform;
        Anch(glowRt, mid, mid, CardPos, new Vector2(620, 620));
        openGlow.raycastTarget = false;

        openArt = UiKit.MakeIcon(rt, UiIcon.Box, 230f, "PackArt");
        artRt = openArt.rectTransform;
        Anch(artRt, mid, mid, CardPos, new Vector2(230, 230));
        openArt.raycastTarget = false;

        resCard = WorkerCardView.Create(rt, 300f, 400f, "ResultCard");
        Anch(resCard.rt, mid, mid, CardPos, new Vector2(300, 400));

        fxRt = NewRT("Fx", rt);
        Stretch(fxRt);
        for (int i = 0; i < sparks.Length; i++)
        {
            var im = UiKit.MakeImage(fxRt, "Spark", PetUi.SparkSprite, false);
            im.raycastTarget = false;
            Anch(im.rectTransform, mid, mid, Vector2.zero, new Vector2(16, 16));
            im.gameObject.SetActive(false);
            sparks[i] = new Spark { img = im, rt = im.rectTransform };
        }

        waitText = UiKit.MakeLabel(rt, "", 38f, Soft, TextAlignmentOptions.Center, true);
        Anch(waitText.rectTransform, mid, mid, new Vector2(260, 20), new Vector2(640, 60));

        // информация о рабочем (справа), координаты TL внутри блока 640x520
        var info = NewRT("Info", rt);
        Anch(info, mid, mid, new Vector2(260, 24), new Vector2(640, 520));
        infoCg = info.gameObject.AddComponent<CanvasGroup>();
        var b = info.transform;

        rTag = UiKit.MakeImage(b, "Tag", null, false);
        TL(rTag.rectTransform, 0, 0, 330, 44);
        rTag.raycastTarget = false;
        rTagText = UiKit.MakeLabel(rTag.transform, "", 26f, Color.white, TextAlignmentOptions.Center, true);
        AutoSize(rTagText, 14f, 26f);

        rName = Lbl(b, "", 40f, Ink, TextAlignmentOptions.Left, true, 0, 56, 640, 54);
        rGem = Icon(b, UiIcon.Gem, 20, 138, 34);
        rRarity = Lbl(b, "", 30f, Ink, TextAlignmentOptions.Left, true, 46, 118, 230, 40);
        for (int i = 0; i < 5; i++) rStars[i] = Icon(b, UiIcon.Star, 300 + i * 32, 138, 28);
        rRoleIcon = Icon(b, UiIcon.Coin, 20, 184, 32);
        rRole = Lbl(b, "", 26f, Ink, TextAlignmentOptions.Left, true, 46, 166, 590, 36);
        rLevel = Lbl(b, "", 24f, Soft, TextAlignmentOptions.Left, false, 0, 208, 640, 32);
        rLine = Wrap(b, "", 25f, Ink, TextAlignmentOptions.TopLeft, true, 0, 246, 640, 84);
        rPerks = Wrap(b, "", 21f, Soft, TextAlignmentOptions.TopLeft, false, 0, 336, 640, 56);
        rBio = Wrap(b, "", 20f, Soft, TextAlignmentOptions.TopLeft, false, 0, 394, 640, 60);
        rBio.fontStyle = FontStyles.Italic;
        againBtn = MakeBtn(b, "Ещё", UiBtn.Amber, 0, 464, 320, 56, 26f, OpenAgain);
        doneBtn = MakeBtn(b, "Готово", UiBtn.Gray, 340, 464, 200, 56, 26f, CloseResult);

        flash = UiKit.MakeImage(rt, "Flash", null, false);
        Stretch(flash.rectTransform);
        flash.color = new Color(1f, 1f, 1f, 0f);
        flash.raycastTarget = false;

        openHint = UiKit.MakeLabel(rt, "", 22f, WorkerUi.Dim, TextAlignmentOptions.Center, false);
        Anch(openHint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 16), new Vector2(900, 34));

        openRoot.SetActive(false);
    }

    // ------------------------------------------------------------ окно шансов
    void BuildOddsOverlay()
    {
        oddsGo = NewRT("Odds", inner).gameObject;
        var rt = (RectTransform)oddsGo.transform;
        Stretch(rt);
        var dimImg = UiKit.MakeImage(rt, "Dim", null, false);
        dimImg.color = new Color(0.17f, 0.18f, 0.36f, 0.5f);
        Stretch(dimImg.rectTransform);
        dimImg.raycastTarget = true;

        var mid = new Vector2(0.5f, 0.5f);
        var pn = UiKit.MakePanel(rt, new Vector2(800, 690), true, 1f, "OddsPanel");
        Anch(pn.rectTransform, mid, mid, Vector2.zero, new Vector2(800, 690));
        pn.raycastTarget = true;
        var b = pn.transform;

        oTitle = Lbl(b, "Шансы", 36f, Ink, TextAlignmentOptions.Center, true, 30, 24, 740, 46);

        Lbl(b, "Редкость", 18f, WorkerUi.Dim, TextAlignmentOptions.Left, false, 70, 76, 200, 24);
        Lbl(b, "Шанс", 18f, WorkerUi.Dim, TextAlignmentOptions.Left, false, 290, 76, 100, 24);
        Lbl(b, "Перки", 18f, WorkerUi.Dim, TextAlignmentOptions.Center, false, 560, 76, 80, 24);
        Lbl(b, "Дубликат", 18f, WorkerUi.Dim, TextAlignmentOptions.Right, false, 640, 76, 130, 24);
        for (int r = 0; r < 5; r++)
        {
            float y = 104f + r * 46f;
            var rar = (WorkerRarity)r;
            oGem[r] = Icon(b, UiIcon.Gem, 50, y + 22, 30);
            oName[r] = Lbl(b, WorkerText.Rarity(rar), 24f, Ink, TextAlignmentOptions.Left, true, 70, y, 200, 44);
            RectTransform f;
            var bar = WorkerUi.MiniBar(b, WorkerUi.BarBack, WorkerUi.RarityAccent(rar), out f);
            TL(bar.rectTransform, 290, y + 14, 140, 16);
            oFill[r] = f;
            oPct[r] = Lbl(b, "", 24f, Ink, TextAlignmentOptions.Right, true, 436, y, 100, 44);
            oPerks[r] = Lbl(b, rar == WorkerRarity.Mythic ? "все+" : WorkerBalance.PerkCount(rar).ToString(), 24f, Soft, TextAlignmentOptions.Center, false, 560, y, 80, 44);
            oXp[r] = Lbl(b, "+" + WorkerBalance.DuplicateXpFor(rar) + " XP", 22f, Soft, TextAlignmentOptions.Right, false, 640, y, 130, 44);
        }

        Lbl(b, "Гарантии (считаются отдельно для каждого пака)", 20f, WorkerUi.Dim, TextAlignmentOptions.Left, false, 30, 346, 740, 28);
        for (int i = 0; i < 3; i++) oGuarLine[i] = Lbl(b, "", 21f, Soft, TextAlignmentOptions.Left, false, 30, 376 + i * 32, 740, 30);
        var mb = WorkerUi.MiniBar(b, WorkerUi.BarBack, WorkerUi.RarityAccent(WorkerRarity.Mythic), out oMythFill);
        TL(mb.rectTransform, 30, 474, 740, 12);

        string coins = "";
        for (int r = 0; r < 5; r++) { if (r > 0) coins += " / "; coins += WorkerBalance.DuplicateCoinsFor((WorkerRarity)r); }
        oDup = Wrap(b, "Дубликат даёт уже найденному рабочему опыт (столбец «Дубликат»). Если он на максимальном уровне, вместо опыта выплачиваются монеты: " + coins + ". Подарочный пак гарантирует первого рабочего.",
            19f, Soft, TextAlignmentOptions.TopLeft, false, 30, 504, 740, 90);

        MakeBtn(b, "Закрыть", UiBtn.Amber, 270, 610, 260, 56, 28f, CloseOdds);
        oddsGo.SetActive(false);
    }
}
