// Assets/_Project/Scripts/UI/Pets/PetCrateWindow.Build.cs (НЕ в Editor)
// ЧАСТЬ класса PetCrateWindow (partial): построение окна из кода: экраны 1-4 и «Шансы», помощники.
// Это кусок одного класса, поля и методы видны из других частей. Файл с объявлением и состоянием: PetCrateWindow.cs.
// Остальные части: PetCrateWindow.Screens.cs, PetCrateWindow.Update.cs.
// Методы этой части: Build, NewScreen, BuildFx, BuildHeader, BuildPrepare, BuildRoll, PointerBar, BuildResult, OpenContents, CloseContents, BuildContents, PopulateContents, OpenOdds, CloseOdds, BuildOdds, RefreshOddsTable, SelectPet, MakeBtn, AddBtn, Anch.

using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UiLayout;

public partial class PetCrateWindow
{
    // ============================================================ построение окна
    void Build()
    {
        if (built) return;

        var cgo = new GameObject("PetCrateCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
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

        // затемнение: только гасит клики по миру
        var ov = UiKit.MakeImage(cgo.transform, "Overlay", null, false);
        ov.color = new Color(0f, 0f, 0f, 0.6f);
        Stretch(ov.rectTransform);
        ov.raycastTarget = true;   // затемнение гасит клики по миру, но окно НЕ закрывает (промах мимо окна не должен его сбрасывать)
        overlay = ov.gameObject.AddComponent<CanvasGroup>();

        // окно: деревянная планка сверху
        var win = UiKit.MakePanel(ov.transform, new Vector2(WinW, WinH), true, BS, "Window");
        win.raycastTarget = true;                                   // клик по окну не закрывает его
        panel = win.rectTransform;
        Anch(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(WinW, WinH));
        BuildHeader();

        var content = NewRT("Content", panel);
        content.anchorMin = Vector2.zero; content.anchorMax = Vector2.one;
        content.offsetMin = new Vector2(26f * BS, 26f * BS);
        content.offsetMax = new Vector2(-26f * BS, -36f * BS);

        var inner = UiKit.MakePanel(content, new Vector2(InW, InH), false, 0.6f, "Inner");
        Stretch(inner.rectTransform);

        prepareGo = NewScreen("Prepare", inner.transform);
        rollGo = NewScreen("Roll", inner.transform);
        resultGo = NewScreen("Result", inner.transform);
        contentsGo = NewScreen("Contents", inner.transform);
        oddsGo = NewScreen("Odds", inner.transform);
        BuildPrepare(prepareGo.transform);
        BuildRoll(rollGo.transform);
        BuildResult(resultGo.transform);
        BuildContents(contentsGo.transform);
        BuildOdds(oddsGo.transform);
        BuildFx(inner.transform);

        // цветная вспышка поверх окна (для редких дропов), клики пропускает
        screenFlash = UiKit.MakeImage(ov.transform, "ScreenFlash", null, false);
        Stretch(screenFlash.rectTransform);
        screenFlash.color = Clear;

        hintText = UiKit.MakeLabel(ov.transform, "", 22f, Dim, TextAlignmentOptions.Center, false);
        Anch(hintText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0, -(WinH * 0.5f + 12f)), new Vector2(1000, 34));

        ov.gameObject.SetActive(false);
        built = true;
        step = Step.Prepare;
        SetStep(Step.Prepare);
    }

    static GameObject NewScreen(string name, Transform parent)
    {
        var rt = NewRT(name, parent);
        Stretch(rt);
        return rt.gameObject;
    }

    // слой искр поверх всех экранов, координаты как у TL: от левого верхнего угла окна вниз
    void BuildFx(Transform inner)
    {
        fx = NewRT("Fx", inner);
        Stretch(fx);
        for (int i = 0; i < sparks.Length; i++)
        {
            var im = UiKit.MakeImage(fx, "Spark", PetUi.SparkSprite, false);
            Anch(im.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16f, 16f));
            im.gameObject.SetActive(false);
            sparks[i] = new Spark { img = im, rt = im.rectTransform };
        }
    }

    void BuildHeader()
    {
        // монеты слева на деревянной планке
        var coin = UiKit.MakeIcon(panel, UiIcon.Coin, 36f, "CoinIcon");
        TLC(coin.rectTransform, 64, 27, 36, 36);
        coinsText = UiKit.MakeLabel(panel, "0", 30f, WoodText, TextAlignmentOptions.Left, true);
        TL(coinsText.rectTransform, 90, 8, 200, 38);

        // заголовок по центру
        titleText = UiKit.MakeLabel(panel, "КЛЕТЬ", 34f, WoodText, TextAlignmentOptions.Center, true);
        Anch(titleText.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -8), new Vector2(520, 40));

        // закрыть (на рулетке пропускает её)
        var close = UiKit.MakeButton(panel, "", UiBtn.Amber, new Vector2(64, 48), 20f, 1f);
        Anch(close.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-34, -5), new Vector2(64, 48));
        close.onClick.AddListener(OnCloseButton);
        var x = UiKit.MakeIcon(close.transform, UiIcon.Cross, 30f, "X");
        x.rectTransform.anchoredPosition = new Vector2(0, 2);
    }

    // ------------------------------------------------------------ экран 1
    void BuildPrepare(Transform p)
    {
        // слева: лучи, ореол, ящик, цена
        crateRays = UiKit.MakeImage(p, "CrateRays", PetUi.RaysSprite, false);
        TLC(crateRays.rectTransform, CrateCx, CrateCy, 440f, 440f);
        crateRays.color = Clear;
        crateHalo = UiKit.MakeImage(p, "Halo", PetUi.GlowSprite, false);
        TLC(crateHalo.rectTransform, CrateCx, CrateCy, 460f, 460f);
        crateHalo.color = Clear;

        crateImg = UiKit.MakeIcon(p, UiIcon.Box, 210f, "CrateIcon");
        TLC(crateImg.rectTransform, CrateCx, CrateCy, 210f, 210f);

        priceCoin = UiKit.MakeIcon(p, UiIcon.Coin, 46f, "PriceCoin");
        TLC(priceCoin.rectTransform, 150f, 402f, 46f, 46f);
        priceText = UiKit.MakeLabel(p, "0", 46f, UiKit.BrassHi, TextAlignmentOptions.Left, true);
        TL(priceText.rectTransform, 182f, 378f, 220f, 50f);

        // справа: шансы выпадения (камень, имя, полоса, процент)
        var head = UiKit.MakeLabel(p, "Шансы выпадения", 26f, Soft, TextAlignmentOptions.Left, true);
        TL(head.rectTransform, 470f, 30f, 500f, 34f);
        for (int i = 0; i < 4; i++)
        {
            var rt = NewRT("Odds" + i, p);
            TL(rt, 470f, 76f + i * 62f, 500f, 54f);
            oddsRow[i] = rt.gameObject;
            oddsRt[i] = rt;
            oddsCg[i] = rt.gameObject.AddComponent<CanvasGroup>();

            var bg = UiKit.MakeImage(rt, "Bg", UiKit.Slot, true, 0.45f);
            Stretch(bg.rectTransform);

            oddsGem[i] = UiKit.MakeIcon(rt, UiIcon.Gem, 38f, "Gem");
            TLC(oddsGem[i].rectTransform, 34f, 27f, 38f, 38f);

            oddsName[i] = UiKit.MakeLabel(rt, "", 28f, Color.white, TextAlignmentOptions.Left, true);
            TL(oddsName[i].rectTransform, 64f, 0f, 204f, 54f);
            PetUi.AutoSize(oddsName[i], 16f, 28f);

            // полоса шанса: тёмная рамка и заливка цветом редкости, ширина = процент
            var barBg = UiKit.MakeImage(rt, "BarBg", UiKit.BarFrame, true, 0.5f);
            TL(barBg.rectTransform, 272f, 17f, 130f, 20f);
            oddsBar[i] = UiKit.MakeImage(barBg.rectTransform, "BarFill", null, false);
            var fr = oddsBar[i].rectTransform;
            fr.anchorMin = new Vector2(0f, 0f);
            fr.anchorMax = new Vector2(1f, 1f);
            fr.pivot = new Vector2(0f, 0.5f);
            fr.offsetMin = new Vector2(4f, 4f);
            fr.offsetMax = new Vector2(-4f, -4f);

            oddsPct[i] = UiKit.MakeLabel(rt, "", 28f, Color.white, TextAlignmentOptions.Right, true);
            TL(oddsPct[i].rectTransform, 406f, 0f, 84f, 54f);
        }

        // гарантия: подпись и синяя полоса
        guaranteeText = UiKit.MakeLabel(p, "", 26f, Soft, TextAlignmentOptions.Left, false);
        TL(guaranteeText.rectTransform, 470f, 338f, 500f, 34f);
        Image pf;
        var pityBar = UiKit.MakeBar(p, UiBar.Blue, new Vector2(400f, 30f), out pf, 0.6f);
        TL(pityBar.rectTransform, 470f, 378f, 400f, 30f);
        pityFill = pf;

        // причина блокировки жёлтым
        reasonText = UiKit.MakeLabel(p, "", 26f, UiKit.Warn, TextAlignmentOptions.Left, true);
        TL(reasonText.rectTransform, 470f, 420f, 500f, 70f);

        // что может выпасть
        TMP_Text tmpL;
        MakeBtn(p, "Что внутри?", UiBtn.Green, 14f, 440f, 205f, 60f, 24f, OpenContents, out tmpL);
        MakeBtn(p, "Шансы", UiBtn.Amber, 229f, 440f, 205f, 60f, 24f, OpenOdds, out tmpL);

        openBtn = MakeBtn(p, "Открыть", UiBtn.Amber, (InW - 380f) * 0.5f, 508f, 380f, 92f, 36f, StartRoll, out openLabel);
    }

    // ------------------------------------------------------------ экран 2
    void BuildRoll(Transform p)
    {
        rollCg = p.gameObject.AddComponent<CanvasGroup>();

        rollTitle = UiKit.MakeLabel(p, "", 38f, Color.white, TextAlignmentOptions.Center, true);
        TL(rollTitle.rectTransform, 0f, 74f, InW, 50f);

        var view = UiKit.MakeImage(p, "Viewport", UiKit.Slot, true, 1f);
        TL(view.rectTransform, (InW - ViewW) * 0.5f, 150f, ViewW, ViewH);

        var mask = NewRT("Mask", view.transform);
        Stretch(mask);
        mask.offsetMin = new Vector2(MaskInset, MaskInset);
        mask.offsetMax = new Vector2(-MaskInset, -MaskInset);
        mask.gameObject.AddComponent<RectMask2D>();

        stripRt = NewRT("Strip", mask);
        Anch(stripRt, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(CardCount * Pitch, CardH));

        // карточки создаются один раз, при каждом броске только меняются данные
        var mid = new Vector2(0.5f, 0.5f);
        for (int i = 0; i < CardCount; i++)
        {
            var cv = PetCardView.Create(stripRt, CardW, CardH, "Card" + i, true);
            Anch(cv.rt, new Vector2(0, 0.5f), mid, new Vector2(i * Pitch + CardW * 0.5f, 0f), new Vector2(CardW, CardH));
            cv.rt.localScale = new Vector3(0.84f, 0.84f, 1f);
            stripCards[i] = cv;
        }

        // края ленты гаснут в цвет окна
        var fadeCol = SlotDark;
        var fl = UiKit.MakeImage(view.transform, "FadeL", PetUi.EdgeFadeSprite, false);
        fl.color = fadeCol;
        Anch(fl.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(MaskInset, 0f), new Vector2(150f, ViewH - 2f * MaskInset));
        var fr = UiKit.MakeImage(view.transform, "FadeR", PetUi.EdgeFadeSprite, false);
        fr.color = fadeCol;
        Anch(fr.rectTransform, new Vector2(1f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-MaskInset, 0f), new Vector2(150f, ViewH - 2f * MaskInset));
        fr.rectTransform.localScale = new Vector3(-1f, 1f, 1f);          // зеркало вокруг левой кромки: уходит внутрь окна

        // неподвижная рамка-указатель по центру (в группе, чтобы «пульсировать» целиком)
        pointerRt = NewRT("PointerGroup", view.transform);
        Anch(pointerRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        float fw = CardW + 14f, fh = CardH + 14f, t = 5f;
        PointerBar(pointerRt, new Vector2(0, fh * 0.5f - t * 0.5f), new Vector2(fw, t));
        PointerBar(pointerRt, new Vector2(0, -fh * 0.5f + t * 0.5f), new Vector2(fw, t));
        PointerBar(pointerRt, new Vector2(-fw * 0.5f + t * 0.5f, 0), new Vector2(t, fh));
        PointerBar(pointerRt, new Vector2(fw * 0.5f - t * 0.5f, 0), new Vector2(t, fh));

        var tailTop = UiKit.MakeImage(view.transform, "TailTop", UiKit.Tail, false);
        Anch(tailTop.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0, 2), new Vector2(43, 26));
        var tailBot = UiKit.MakeImage(view.transform, "TailBottom", UiKit.Tail, false);
        Anch(tailBot.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0, -2), new Vector2(43, 26));
        tailBot.rectTransform.localEulerAngles = new Vector3(0, 0, 180f);

        // имя и редкость карточки под указателем, меняются на ходу
        liveName = UiKit.MakeLabel(p, "", 42f, Color.white, TextAlignmentOptions.Center, true);
        TLC(liveName.rectTransform, InW * 0.5f, 443f, InW, 50f);
        PetUi.AutoSize(liveName, 24f, 42f);
        liveRarity = UiKit.MakeLabel(p, "", 26f, Color.white, TextAlignmentOptions.Center, true);
        TLC(liveRarity.rectTransform, InW * 0.5f, 487f, InW, 34f);
    }

    void PointerBar(Transform parent, Vector2 pos, Vector2 size)
    {
        var img = UiKit.MakeImage(parent, "Pointer", null, false);
        img.color = UiKit.Brass;
        Anch(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
        pointerBars.Add(img);
    }

    // ------------------------------------------------------------ экран 3
    void BuildResult(Transform p)
    {
        // лучи, свечение и вспышка за карточкой. Лежат в маске, чтобы не вылезать за рамку окна
        const float inset = 10f;
        var clip = NewRT("RaysClip", p);
        Stretch(clip);
        clip.offsetMin = new Vector2(inset, inset);
        clip.offsetMax = new Vector2(-inset, -inset);
        clip.gameObject.AddComponent<RectMask2D>();

        rays = UiKit.MakeImage(clip, "Rays", PetUi.RaysSprite, false);
        TLC(rays.rectTransform, CardCx - inset, CardCy - inset, 780f, 780f);
        rays.color = Clear;
        glowIdle = UiKit.MakeImage(clip, "GlowIdle", PetUi.GlowSprite, false);
        TLC(glowIdle.rectTransform, CardCx - inset, CardCy - inset, 560f, 560f);
        glowIdle.color = Clear;
        burst = UiKit.MakeImage(clip, "Burst", PetUi.GlowSprite, false);
        TLC(burst.rectTransform, CardCx - inset, CardCy - inset, 560f, 560f);
        burst.color = Clear;

        resultCard = PetCardView.Create(p, 220f, 270f, "ResultCard", true);
        resultCard.EnableLive();     // выпавший питомец вращается живой 3D-моделью
        TLC(resultCard.rt, CardCx, CardCy, 220f, 270f);

        // рубашка (видна до переворота) и вспышка поверх карточки
        var back = UiKit.MakeImage(resultCard.rt, "Back", UiKit.Slot, true, 1f);
        Stretch(back.rectTransform);
        backGo = back.gameObject;
        var boxIcon = UiKit.MakeIcon(back.transform, UiIcon.Box, 120f, "BackIcon");
        boxIcon.rectTransform.anchoredPosition = Vector2.zero;

        cardFlash = UiKit.MakeImage(resultCard.rt, "CardFlash", null, false);
        Stretch(cardFlash.rectTransform);
        cardFlash.rectTransform.offsetMin = new Vector2(7f, 7f);
        cardFlash.rectTransform.offsetMax = new Vector2(-7f, -7f);
        cardFlash.color = Clear;

        // тег «НОВЫЙ!» на уголке карточки (деревянная табличка, чуть наклонена)
        newTag = UiKit.MakePlaque(p, new Vector2(140f, 58f), 0.9f, "NewTag");
        TLC(newTag.rectTransform, CardCx + 135f, CardCy - 105f, 140f, 58f);
        newTag.rectTransform.localEulerAngles = new Vector3(0f, 0f, -9f);
        newTagLabel = UiKit.MakeLabel(newTag.transform, "НОВЫЙ!", 30f, WoodText, TextAlignmentOptions.Center, true);
        PetUi.AutoSize(newTagLabel, 16f, 30f);
        newTag.gameObject.SetActive(false);

        // текст и кнопки появляются после переворота
        var info = NewRT("Info", p);
        Stretch(info);
        resultInfo = info.gameObject.AddComponent<CanvasGroup>();
        resultInfo.alpha = 0f;

        nameText = UiKit.MakeLabel(info, "", 46f, Color.white, TextAlignmentOptions.Center, true);
        TL(nameText.rectTransform, 0f, 296f, InW, 52f);

        // редкость между двумя камнями
        rarityText = UiKit.MakeLabel(info, "", 28f, Color.white, TextAlignmentOptions.Center, true);
        TL(rarityText.rectTransform, 0f, 346f, InW, 34f);
        gemL = UiKit.MakeIcon(info, UiIcon.Gem, 32f, "GemL");
        TLC(gemL.rectTransform, CardCx - 140f, 363f, 32f, 32f);
        gemR = UiKit.MakeIcon(info, UiIcon.Gem, 32f, "GemR");
        TLC(gemR.rectTransform, CardCx + 140f, 363f, 32f, 32f);

        // строка стата: медальон с иконкой и текст (позиции задаёт ShowResult по ширине текста)
        statBadge = UiKit.MakeImage(info, "StatBadge", UiKit.Medallion, false);
        TLC(statBadge.rectTransform, CardCx - 100f, 401f, 46f, 46f);
        statBadgeIcon = UiKit.MakeImage(statBadge.rectTransform, "StatIcon", null, false);
        statBadgeIcon.preserveAspect = true;
        Anch(statBadgeIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(29f, 29f));
        statText = UiKit.MakeLabel(info, "", 30f, Color.white, TextAlignmentOptions.Left, false);
        TL(statText.rectTransform, CardCx - 40f, 382f, 400f, 38f);

        dupText = UiKit.MakeLabel(info, "", 24f, Soft, TextAlignmentOptions.Center, false);
        TL(dupText.rectTransform, 0f, 424f, InW, 30f);
        slotNote = UiKit.MakeLabel(info, "", 24f, Soft, TextAlignmentOptions.Center, false);
        TL(slotNote.rectTransform, 0f, 456f, InW, 34f);

        float bx = (InW - (ResBtnW * 3f + ResBtnGap * 2f)) * 0.5f;
        TMP_Text tmp;
        equipBtn = MakeBtn(info, "Взять с собой", UiBtn.Amber, bx, ResBtnY, ResBtnW, ResBtnH, 30f, OnEquip, out equipLabel);
        MakeBtn(info, "В коллекцию", UiBtn.Green, bx + ResBtnW + ResBtnGap, ResBtnY, ResBtnW, ResBtnH, 30f, DoClose, out tmp);
        MakeBtn(info, "Открыть ещё", UiBtn.Yellow, bx + (ResBtnW + ResBtnGap) * 2f, ResBtnY, ResBtnW, ResBtnH, 30f, OnAgain, out tmp);
    }

    // ------------------------------------------------------------ экран 4: что может выпасть
    const int ContCols = 7;
    const float ContCardW = 120f, ContCardH = 142f, ContPitchX = 136f, ContRowH = 192f, ContHeadH = 46f;

    void OpenContents()
    {
        if (step != Step.Prepare) return;
        PopulateContents();
        SetStep(Step.Contents);
        SfxManager.Play("hint", 0.5f, 1.2f);
    }

    void CloseContents()
    {
        if (step != Step.Contents) return;
        SetStep(Step.Prepare);
        Refresh();
    }

    void BuildContents(Transform p)
    {
        var head = UiKit.MakeLabel(p, "Что может выпасть", 30f, Color.white, TextAlignmentOptions.Center, true);
        TL(head.rectTransform, 0f, 22f, InW, 40f);

        float vw = InW - 40f;
        var view = UiKit.MakeImage(p, "View", UiKit.Slot, true, 1f);
        TL(view.rectTransform, 20f, 70f, vw, 372f);

        // окно прокрутки: маска, невидимая картинка ловит перетаскивание, внутри длинный контент
        var vp = NewRT("Viewport", view.transform);
        Stretch(vp);
        vp.offsetMin = new Vector2(8f, 8f);
        vp.offsetMax = new Vector2(-8f, -8f);
        vp.gameObject.AddComponent<RectMask2D>();
        var vpImg = vp.gameObject.AddComponent<Image>();
        vpImg.color = Clear;
        vpImg.raycastTarget = true;

        contentsRt = NewRT("Content", vp);
        contentsRt.anchorMin = new Vector2(0f, 1f);
        contentsRt.anchorMax = new Vector2(1f, 1f);
        contentsRt.pivot = new Vector2(0.5f, 1f);
        contentsRt.anchoredPosition = Vector2.zero;
        contentsRt.sizeDelta = new Vector2(0f, 100f);

        var sr = view.gameObject.AddComponent<ScrollRect>();
        sr.viewport = vp;
        sr.content = contentsRt;
        sr.horizontal = false;
        sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.scrollSensitivity = 40f;
        sr.inertia = true;

        // полоса с описанием выбранного питомца
        var bar = UiKit.MakeImage(p, "Detail", UiKit.Slot, true, 0.6f);
        TL(bar.rectTransform, 20f, 450f, vw, 62f);
        var badge = UiKit.MakeImage(bar.transform, "Badge", UiKit.Medallion, false);
        TLC(badge.rectTransform, 40f, 31f, 46f, 46f);
        detailBadgeIcon = UiKit.MakeImage(badge.transform, "StatIcon", null, false);
        detailBadgeIcon.preserveAspect = true;
        Anch(detailBadgeIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(29f, 29f));
        detailGem = UiKit.MakeIcon(bar.transform, UiIcon.Gem, 32f, "Gem");
        TLC(detailGem.rectTransform, 90f, 31f, 32f, 32f);
        detailName = UiKit.MakeLabel(bar.transform, "", 28f, Color.white, TextAlignmentOptions.Left, true);
        TL(detailName.rectTransform, 114f, 0f, 330f, 62f);
        PetUi.AutoSize(detailName, 16f, 28f);
        detailStat = UiKit.MakeLabel(bar.transform, "", 26f, Color.white, TextAlignmentOptions.Left, false);
        TL(detailStat.rectTransform, 450f, 0f, vw - 470f, 62f);
        PetUi.AutoSize(detailStat, 16f, 26f);

        TMP_Text tmpL;
        MakeBtn(p, "Назад", UiBtn.Green, (InW - 300f) * 0.5f, 530f, 300f, 76f, 32f, CloseContents, out tmpL);
    }

    // заполняется при каждом открытии: питомцы по группам редкости (редкие сверху), шанс на каждый вид
    void PopulateContents()
    {
        for (int i = contentsRt.childCount - 1; i >= 0; i--)
        {
            var ch = contentsRt.GetChild(i).gameObject;
            ch.SetActive(false);
            Destroy(ch);
        }
        contentCards.Clear();
        selectedCard = null;
        SelectPet(null, null);

        var pm = PetManager.Instance;
        float[] odds = GameBalance.PetCrateOdds[(int)crate];
        float inW = InW - 40f - 16f;
        float left = (inW - (ContCols * ContPitchX - 16f)) * 0.5f;
        float y = 8f;
        bool any = false;

        if (pm != null && pm.database != null)
        {
            for (int r = 3; r >= 0; r--)
            {
                float v = r < odds.Length ? odds[r] : 0f;
                if (v <= 0f) continue;
                pm.database.CollectByRarity((PetRarity)r, tmpPets);
                if (tmpPets.Count == 0) continue;
                any = true;
                Color acc = RarityAccent((PetRarity)r);
                tmpPets.Sort((a, b) => string.CompareOrdinal(PetUi.NameOf(a), PetUi.NameOf(b)));
                int n = tmpPets.Count;

                var g = UiKit.MakeIcon(contentsRt, UiIcon.Gem, 34f, "HeadGem");
                TLC(g.rectTransform, 30f, y + 22f, 34f, 34f);
                g.color = acc;
                var hl = UiKit.MakeLabel(contentsRt, PetText.Rarity((PetRarity)r)
                    + "  <size=65%><color=#" + Hex(Dim) + ">шанс " + v.ToString("0.#", CultureInfo.InvariantCulture) + "%  ·  "
                    + n + " " + UiDraw.Plural(n, "вид", "вида", "видов") + "</color></size>", 28f, acc, TextAlignmentOptions.Left, true);
                TL(hl.rectTransform, 56f, y, 760f, 44f);
                y += ContHeadH;

                string each = (v / n).ToString("0.##", CultureInfo.InvariantCulture) + "%";
                for (int i = 0; i < n; i++)
                {
                    float x = left + (i % ContCols) * ContPitchX;
                    float cy = y + (i / ContCols) * ContRowH;
                    PetData d = tmpPets[i];

                    var cv = PetCardView.Create(contentsRt, ContCardW, ContCardH, "Pet", false);
                    Anch(cv.rt, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f),
                         new Vector2(x + ContCardW * 0.5f, -(cy + ContCardH * 0.5f)), new Vector2(ContCardW, ContCardH));
                    cv.Set(d);
                    contentCards.Add(cv);

                    cv.frame.raycastTarget = true;
                    var btn = cv.frame.gameObject.AddComponent<Button>();
                    btn.transition = Selectable.Transition.None;
                    btn.targetGraphic = cv.frame;
                    PetCardView capCard = cv;
                    PetData capPet = d;
                    btn.onClick.AddListener(() => SelectPet(capCard, capPet));

                    var cl = UiKit.MakeLabel(contentsRt, each, 22f, acc, TextAlignmentOptions.Center, true);
                    TL(cl.rectTransform, x, cy + ContCardH + 4f, ContCardW, 30f);
                }
                y += Mathf.Ceil(n / (float)ContCols) * ContRowH + 6f;
            }
        }

        if (!any)
        {
            var none = UiKit.MakeLabel(contentsRt, "Питомцы недоступны: в базе нет подходящих видов", 26f, UiKit.Warn, TextAlignmentOptions.Center, true);
            TL(none.rectTransform, 0f, y + 40f, inW, 40f);
            y += 90f;
        }
        else
        {
            var note = UiKit.MakeLabel(contentsRt, "Шанс на вид указан без учёта гарантии Редкого", 20f, Dim, TextAlignmentOptions.Center, false);
            TL(note.rectTransform, 0f, y, inW, 30f);
            y += 34f;
        }

        contentsRt.sizeDelta = new Vector2(0f, y + 8f);
        contentsRt.anchoredPosition = Vector2.zero;
    }

    // ------------------------------------------------------------ экран «Шансы»: таблица GameBalance.PetCrateOdds по всем клетям
    readonly TMP_Text[,] oddsCell = new TMP_Text[4, 3];
    readonly Image[] oddsColBg = new Image[3];
    TMP_Text oddsNote;

    void OpenOdds()
    {
        if (step != Step.Prepare) return;
        RefreshOddsTable();
        SetStep(Step.Odds);
        SfxManager.Play("hint", 0.5f, 1.2f);
    }

    void CloseOdds()
    {
        if (step != Step.Odds) return;
        SetStep(Step.Prepare);
        Refresh();
    }

    void BuildOdds(Transform p)
    {
        var head = UiKit.MakeLabel(p, "Шансы выпадения", 30f, Color.white, TextAlignmentOptions.Center, true);
        TL(head.rectTransform, 0f, 22f, InW, 40f);

        const float labW = 260f, colW = 200f, rowH = 70f, top = 140f;
        float x0 = (InW - (labW + 3f * colW)) * 0.5f;

        for (int c = 0; c < 3; c++)
        {
            oddsColBg[c] = UiKit.MakeImage(p, "ColBg" + c, UiKit.Slot, true, 0.6f);
            TL(oddsColBg[c].rectTransform, x0 + labW + c * colW + 4f, top - 54f, colW - 8f, 54f + 4f * rowH + 8f);
            oddsColBg[c].raycastTarget = false;

            var t = UiKit.MakeLabel(p, PetText.Crate((CrateType)c), 24f, Color.white, TextAlignmentOptions.Center, true);
            TL(t.rectTransform, x0 + labW + c * colW + 4f, top - 48f, colW - 8f, 44f);
            PetUi.AutoSize(t, 14f, 24f);
            t.color = PetUi.CrateAccent((CrateType)c);
        }

        for (int r = 0; r < 4; r++)
        {
            PetRarity rar = (PetRarity)r;
            var gem = UiKit.MakeIcon(p, UiIcon.Gem, 36f, "Gem" + r);
            gem.color = RarityAccent(rar);
            TLC(gem.rectTransform, x0 + 28f, top + r * rowH + rowH * 0.5f, 36f, 36f);
            var name = UiKit.MakeLabel(p, PetText.Rarity(rar), 28f, RarityAccent(rar), TextAlignmentOptions.Left, true);
            TL(name.rectTransform, x0 + 56f, top + r * rowH, labW - 60f, rowH);
            PetUi.AutoSize(name, 16f, 28f);

            for (int c = 0; c < 3; c++)
            {
                var v = UiKit.MakeLabel(p, "", 32f, Color.white, TextAlignmentOptions.Center, true);
                TL(v.rectTransform, x0 + labW + c * colW + 4f, top + r * rowH, colW - 8f, rowH);
                oddsCell[r, c] = v;
            }
        }

        oddsNote = UiKit.MakeLabel(p, "", 24f, Soft, TextAlignmentOptions.Center, false);
        TL(oddsNote.rectTransform, 40f, top + 4f * rowH + 26f, InW - 80f, 70f);
        oddsNote.enableWordWrapping = true;

        TMP_Text tmpL;
        MakeBtn(p, "Назад", UiBtn.Green, (InW - 300f) * 0.5f, 530f, 300f, 76f, 32f, CloseOdds, out tmpL);
    }

    void RefreshOddsTable()
    {
        for (int c = 0; c < 3; c++)
        {
            float[] odds = GameBalance.PetCrateOdds[c];
            bool cur = (int)crate == c;
            oddsColBg[c].color = cur ? new Color(1f, 1f, 1f, 1f) : new Color(1f, 1f, 1f, 0.55f);
            for (int r = 0; r < 4; r++)
            {
                float v = r < odds.Length ? odds[r] : 0f;
                var t = oddsCell[r, c];
                if (v <= 0f) { Set(t, "—"); t.color = Dim; }
                else
                {
                    Set(t, v.ToString("0.#", CultureInfo.InvariantCulture) + "%");
                    t.color = cur ? RarityAccent((PetRarity)r) : Soft;
                }
            }
        }

        var pm = PetManager.Instance;
        string guarantee = "Гарантия: каждая " + GameBalance.PetGuaranteeEvery + "-я клеть одного типа без Редкого даёт Редкого или выше.";
        if (pm != null) guarantee += "\nДля этой клети: <b>До Редкого: " + pm.OpensUntilGuarantee(crate) + "</b>";
        Set(oddsNote, guarantee);
    }

    void SelectPet(PetCardView card, PetData d)
    {
        if (selectedCard != null) selectedCard.rt.localScale = Vector3.one;
        selectedCard = card;

        bool has = d != null;
        detailGem.gameObject.SetActive(has);
        detailBadgeIcon.transform.parent.gameObject.SetActive(has);
        if (!has)
        {
            detailName.color = Dim;
            Set(detailName, "Нажми на питомца: покажу, что он усиливает");
            Set(detailStat, "");
            return;
        }

        card.rt.localScale = new Vector3(1.06f, 1.06f, 1f);
        Color acc = RarityAccent(d.rarity);
        detailGem.color = acc;
        detailBadgeIcon.sprite = UiKit.Icon(PetUi.StatIcon(d.stat));
        detailName.color = acc;
        Set(detailName, PetUi.NameOf(d));
        Set(detailStat, PetUi.StatLine(d));
        SfxManager.Play("ui", 0.3f, 1.2f);
    }

    // ------------------------------------------------------------ мелкие помощники
    static Button MakeBtn(Transform parent, string label, UiBtn style, float x, float y, float w, float h, float font,
                          UnityAction onClick, out TMP_Text labelText)
    {
        var b = UiKit.MakeButton(parent, label, style, new Vector2(w, h), font, 1f);
        TL(b.GetComponent<RectTransform>(), x, y, w, h);
        b.onClick.AddListener(onClick);
        labelText = b.GetComponentInChildren<TMP_Text>();
        if (labelText != null) PetUi.AutoSize(labelText, 16f, font);
        return b;
    }

    static void AddBtn(Image img, UnityAction a)
    {
        img.raycastTarget = true;
        var b = img.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        b.targetGraphic = img;
        b.onClick.AddListener(a);
    }

    static void Anch(RectTransform r, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        PetUi.Anchor(r, anchor, pivot, pos, size);
    }
}
