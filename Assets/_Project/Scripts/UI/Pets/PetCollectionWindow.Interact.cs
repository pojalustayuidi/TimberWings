// Assets/_Project/Scripts/UI/Pets/PetCollectionWindow.Interact.cs (НЕ в Editor)
// ЧАСТЬ класса PetCollectionWindow (partial): мелкие анимации и интерактив «погладить питомца».
// Это кусок одного класса, поля и методы видны из других частей. Файл с объявлением и состоянием: PetCollectionWindow.cs.
// Остальные части: PetCollectionWindow.Actions.cs, PetCollectionWindow.Fill.cs, PetCollectionWindow.DeltaBook.cs, PetCollectionWindow.Build.cs, PetCollectionWindow.Presets.cs, PetCollectionWindow.HudHelpers.cs.
// Методы этой части: AnimateMisc, PatCount, TierOf, ShowBubble, RefreshPat, OnPetPat, OnPetDrag, AnimatePat, AnimateFx, AnimateCards, InnerToFx, OnRarClick, OnResetFilters, BuildGrid, BuildDetail, BuildBook.

using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UiLayout;

public partial class PetCollectionWindow
{
    // ------------------------------------------------------------ мелкие анимации
    // полоски бонусов плавно доезжают, кнопка «Взять с собой» дышит, пока есть свободный слот
    void AnimateMisc(float dt)
    {
        float k = 1f - Mathf.Exp(-12f * dt);
        for (int i = 0; i < bonusBarFill.Length; i++)
        {
            if (bonusBarTrack[i] == null || !bonusBarTrack[i].gameObject.activeSelf) continue;
            bonusBarCur[i] = Mathf.Lerp(bonusBarCur[i], bonusBarTgt[i], k);
            bonusBarFill[i].rectTransform.anchorMax = new Vector2(bonusBarCur[i], 1f);
        }

        var pm = PetManager.Instance;
        if (equipBtn != null && equipBtn.gameObject.activeSelf)
        {
            bool ready = tab == Tab.Collection && pm != null && equipBtn.interactable && pm.EquippedCount < pm.SlotCount;
            float s = ready ? 1f + 0.025f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f)) : 1f;
            equipBtn.transform.localScale = Vector3.one * Mathf.Lerp(equipBtn.transform.localScale.x, s, Mathf.Clamp01(dt * 10f));
        }
    }

    // ------------------------------------------------------------ интерактив: погладить питомца
    static readonly string[] PetTiers = { "Присматривается", "Доверяет", "Любит тебя", "Лучший друг" };
    static readonly int[] TierAt = { 0, 5, 15, 30 };
    static readonly string[] PatPhrases = { "Мур!", "Хи-хи!", "Ещё!", "Ура!", "Тепло~", "Я рад!", "Ого!", "Люблю!" };
    static readonly string[] GreetPhrases = { "Привет!", "Я тут!", "Идём?", "Готов!" };

    int PatCount(int uid)
    {
        int n;
        return pats.TryGetValue(uid, out n) ? n : 0;
    }

    static int TierOf(int n)
    {
        int t = 0;
        for (int i = 1; i < TierAt.Length; i++) if (n >= TierAt[i]) t = i;
        return t;
    }

    void ShowBubble(string text)
    {
        if (petBubble == null || tab != Tab.Collection) return;
        petBubbleText.text = text;
        bubbleT = 0f;
    }

    // строка под кнопками: уровень привязанности выбранного питомца
    void RefreshPat()
    {
        if (patText == null) return;
        PetInstance sel = tab == Tab.Collection ? FindInstance(selectedUid) : null;
        bool on = sel != null;
        patHeart.gameObject.SetActive(on);
        patText.gameObject.SetActive(on);
        if (!on) return;
        int n = PatCount(sel.uid);
        int t = TierOf(n);
        if (n == 0) { Set(patText, "<color=#" + PetUi.Hex(Dim) + ">Нажми на питомца: погладить</color>"); return; }
        string next = t + 1 < TierAt.Length ? "  <color=#" + PetUi.Hex(Dim) + ">" + n + "/" + TierAt[t + 1] + "</color>" : "  <color=#" + PetUi.Hex(Dim) + ">" + n + "</color>";
        Set(patText, "<b>" + PetTiers[t] + "</b>" + next);
    }

    // клик (тап) по питомцу
    void OnPetPat()
    {
        PetInstance sel = tab == Tab.Collection ? FindInstance(selectedUid) : null;
        if (sel == null || hearts == null || detCard == null) return;
        if (Time.unscaledTime < patCool) return;
        patCool = Time.unscaledTime + 0.12f;

        int before = PatCount(sel.uid), after = before + 1;
        pats[sel.uid] = after;
        patCombo = Time.unscaledTime - patComboT < 0.8f ? patCombo + 1 : 1;      // серия быстрых поглаживаний
        patComboT = Time.unscaledTime;
        patPulse = 1f;

        PetLiveView live = detCard.Live;
        if (live != null) live.Hop();

        Vector2 c = InnerToFx(DetX + DetW * 0.5f, DetY + 249f);
        hearts.Burst(c, patCombo >= 5 ? 7 : 3, 60f, patCombo >= 5 ? 38f : 30f);
        SfxManager.Play("ui", 0.35f, Mathf.Min(1.9f, 1.2f + 0.07f * patCombo));  // с серией звук становится выше
        ShowBubble(patCombo >= 5 ? "Мурррр!" : PatPhrases[Random.Range(0, PatPhrases.Length)]);

        PetRarity r = sel.data.rarity;
        if (fx != null && r >= PetRarity.Rare)
            fx.Burst(c, PetUi.RarityAccent(r), 6, 220f, 18f, 300f, 0.6f);

        int t0 = TierOf(before), t1 = TierOf(after);
        if (t1 > t0)                                                              // новый уровень привязанности
        {
            if (fx != null)
            {
                fx.Burst(c, TWPetCollectionSkin.Accent, 22, 340f, 26f, 280f, 0.9f);
                fx.FloatText(c + new Vector2(0f, 90f), PetTiers[t1], TWPetCollectionSkin.Accent, 36f);
            }
            hearts.Burst(c, 10, 90f, 40f);
            SfxManager.Play("coin", 0.7f, 1.3f);
            Toast(PetUi.NameOf(sel.data) + ": " + PetTiers[t1].ToLowerInvariant(), ToastKind.Good);
        }
        RefreshPat();
    }

    // перетаскивание по питомцу: покрутить рукой
    void OnPetDrag(float dx)
    {
        if (detCard == null) return;
        PetLiveView live = detCard.Live;
        if (live != null) live.Drag(dx * 0.6f);
    }

    // пузырь с репликой: выезжает пружинкой, висит около полутора секунд, гаснет; сердечко в строке привязанности бьётся
    void AnimatePat(float dt)
    {
        if (petBubble == null) return;
        patPulse = Mathf.MoveTowards(patPulse, 0f, dt / 0.35f);

        bool vis = bubbleT >= 0f && tab == Tab.Collection;
        if (!vis)
        {
            if (petBubble.gameObject.activeSelf) petBubble.gameObject.SetActive(false);
            bubbleT = -1f;
        }
        else
        {
            const float Life = 1.7f;
            bubbleT += dt;
            if (bubbleT >= Life)
            {
                bubbleT = -1f;
                petBubble.gameObject.SetActive(false);
            }
            else
            {
                if (!petBubble.gameObject.activeSelf) petBubble.gameObject.SetActive(true);
                float inK = UiDraw.EaseOutBack(Mathf.Clamp01(bubbleT / 0.2f));
                float a = Mathf.Clamp01((Life - bubbleT) / 0.3f);
                petBubble.rectTransform.localScale = Vector3.one * Mathf.LerpUnclamped(0.5f, 1f, inK);
                petBubble.rectTransform.anchoredPosition = new Vector2(74f, -155f + 2f * Mathf.Sin(bubbleT * 6f));
                petBubble.color = new Color(1f, 1f, 1f, a);
                petBubbleText.alpha = a;
            }
        }

        if (patHeart != null && patHeart.gameObject.activeSelf)
        {
            float beat = Mathf.Max(0f, Mathf.Sin(Time.unscaledTime * 4f));
            patHeart.rectTransform.localScale = Vector3.one * (1f + 0.10f * beat + 0.35f * patPulse);
        }
    }

    // фон и живые эффекты: цвет сцены по редкости, дыхание свечения, лучи легендарных, подсветка под курсором, пульс пустых слотов
    AchSparks fx;
    readonly int[] lastUid = { -2, -2, -2, -2 };
    int lastClickIdx = -1;
    float lastClickT;
    int rarFilter = -1;
    readonly Image[] rarImg = new Image[5];
    readonly Button[] rarBtn = new Button[5];

    void AnimateFx(float dt)
    {
        float tm = Time.unscaledTime;
        if (hoverFrame != null)
        {
            bool on = hoverPc != null && hoverPc.index >= 0 && hoverPc.view.rt.gameObject.activeSelf && tab == Tab.Collection;
            hoverK = Mathf.MoveTowards(hoverK, on ? 1f : 0f, dt / 0.12f);
            hoverFrame.gameObject.SetActive(hoverK > 0.01f);
            if (on)
            {
                int idx = hoverPc.index;
                float x = CellLeft + (idx % Cols) * PitchX;
                float cy = 8f + (idx / Cols) * PitchY;
                hoverFrame.rectTransform.anchoredPosition = new Vector2(x + CardW * 0.5f, -(cy + CardH * 0.5f));
            }
            var hc = TWPetCollectionSkin.Selection; hc.a = 0.55f * UiDraw.EaseOutCubic(hoverK);
            hoverFrame.color = hc;
        }

        // рамка выбора плавно перелетает к новой карточке и мягко «дышит»
        if (selFrame != null && selFrame.gameObject.activeSelf)
        {
            selFrame.rectTransform.anchoredPosition = Vector2.Lerp(selFrame.rectTransform.anchoredPosition, selTarget, 1f - Mathf.Exp(-18f * dt));
            selFrame.color = TWPetCollectionSkin.Selection;
        }

        // описание выбранного: короткий «выезд» при смене питомца
        detT = Mathf.MoveTowards(detT, 1f, dt / 0.22f);
        if (detCg != null) detCg.alpha = Mathf.Lerp(0.3f, 1f, Mathf.Clamp01(detT * 1.6f));
        if (detCard != null && detCard.rt.gameObject.activeSelf)
            detCard.rt.localScale = Vector3.one * (Mathf.LerpUnclamped(0.9f, 1f, UiDraw.EaseOutBack(detT)) * (1f + 0.08f * patPulse));

        // панель бонусов вспыхивает зелёным, когда бонусы изменились
        if (bonusPanel != null)
        {
            bonusFlash = Mathf.MoveTowards(bonusFlash, 0f, dt / 0.6f);
            bonusPanel.color = Color.Lerp(Color.white, new Color(0.76f, 0.96f, 0.85f), 0.6f * bonusFlash);
        }

        var pm = PetManager.Instance;
        bool canPlace = pm != null && tab == Tab.Collection && selectedUid >= 0 && !pm.IsEquipped(selectedUid);
        float pulse = 0.5f + 0.5f * Mathf.Sin(tm * 4f);
        for (int i = 0; i < slots.Length; i++)
        {
            var sl = slots[i];
            if (sl == null || !sl.bg.gameObject.activeSelf) continue;
            Color c = sl.baseCol;
            if (canPlace && pm.IsSlotOpen(i) && pm.GetEquipped(i) == null) c = Color.Lerp(Color.white, new Color(0.70f, 0.93f, 0.82f), 0.35f + 0.25f * pulse);
            sl.bg.color = Color.Lerp(sl.bg.color, c, Mathf.Clamp01(dt * 14f));
            if (slotIn[i] < 1f) slotIn[i] = Mathf.Min(1f, slotIn[i] + dt);
            float inS = Mathf.LerpUnclamped(0.55f, 1f, UiDraw.EaseOutBack(Mathf.Clamp01(slotIn[i] / 0.32f)));   // выезд при открытии
            if (sl.card != null && sl.card.rt.gameObject.activeSelf)
            {
                sl.pop = Mathf.MoveTowards(sl.pop, 0f, dt / 0.4f);
                sl.card.rt.localScale = Vector3.one * (sl.baseScale * (1f + 0.22f * sl.pop * sl.pop) * inS);
            }
            if (sl.icon != null && sl.icon.gameObject.activeSelf)        // пустой слот: лапка тихо покачивается
            {
                sl.icon.rectTransform.anchoredPosition = new Vector2(0f, 16f + 3f * Mathf.Sin(tm * 2f + i * 1.3f));
                sl.icon.rectTransform.localScale = Vector3.one * inS;
            }
        }
    }

    // карточки: появление волной, плавное увеличение выбранной и карточки под курсором; индикатор прокрутки
    void AnimateCards(float dt)
    {
        for (int k = 0; k < pool.Count; k++)
        {
            var pc = pool[k];
            if (pc.index < 0 || pc.index >= items.Count) continue;
            if (pc.delay > 0f)
            {
                pc.delay -= dt;
                if (pc.cg != null) pc.cg.alpha = 0f;
                pc.view.rt.localScale = Vector3.one * 0.7f;
                continue;
            }
            pc.appear = Mathf.MoveTowards(pc.appear, 1f, dt / 0.24f);
            bool sel = IsSelectedEntry(items[pc.index]);
            bool hov = pc == hoverPc && tab == Tab.Collection;
            float target = sel ? 1.06f : (hov ? 1.03f : 1f);
            pc.sc = Mathf.Lerp(pc.sc, target, 1f - Mathf.Exp(-16f * dt));
            if (pc.cg != null) pc.cg.alpha = Mathf.Clamp01(pc.appear * 2.4f);
            pc.view.rt.localScale = Vector3.one * (pc.sc * Mathf.LerpUnclamped(0.7f, 1f, UiDraw.EaseOutBack(pc.appear)));
        }

        // тонкая полоса справа: видно, что список длиннее окна, и где мы в нём
        if (scrollTrack != null)
        {
            float vh = GridH - 16f, ch = contentsRt.sizeDelta.y;
            bool need = ch > vh + 1f && items.Count > 0;
            if (scrollTrack.gameObject.activeSelf != need) scrollTrack.gameObject.SetActive(need);
            if (need)
            {
                float trackH = scrollTrack.rectTransform.sizeDelta.y;
                float handleH = Mathf.Clamp(trackH * vh / ch, 28f, trackH);
                float t01 = Mathf.Clamp01(contentsRt.anchoredPosition.y / Mathf.Max(1f, ch - vh));
                scrollHandle.rectTransform.sizeDelta = new Vector2(5f, handleH);
                scrollHandle.rectTransform.anchoredPosition = new Vector2(0f, -t01 * (trackH - handleH));
            }
        }
    }

    // координаты внутри внутренней панели (от её левого верхнего угла) -> координаты искр (от центра окна)
    static Vector2 InnerToFx(float x, float y) { return new Vector2(ContentX + x - WinW * 0.5f, WinH * 0.5f - (ContentY + y)); }

    void OnRarClick(int j)
    {
        int f = j - 1;
        if (rarFilter == f || tab != Tab.Collection) return;
        rarFilter = f;
        contentsRt.anchoredPosition = Vector2.zero;
        armedUntil = 0f;
        animateNext = true;
        revealPending = true;
        SfxManager.Play("ui", 0.3f, 1.1f);
        Populate();
    }

    // кнопка в пустом списке: убрать все фильтры
    void OnResetFilters()
    {
        filterStat = -1;
        rarFilter = -1;
        contentsRt.anchoredPosition = Vector2.zero;
        animateNext = true;
        SfxManager.Play("ui", 0.3f, 1.1f);
        Populate();
    }

    void BuildGrid(Transform parent)
    {
        var col = MakeBtn(parent, "Коллекция", UiBtn.Gray, GridX, 6f, 210f, 50f, 23f, () => SetTab(Tab.Collection), out tabColLabel);
        var book = MakeBtn(parent, "Книга", UiBtn.Gray, GridX + 222f, 6f, 210f, 50f, 23f, () => SetTab(Tab.Book), out tabBookLabel);
        tabColImg = col.GetComponent<Image>();
        tabBookImg = book.GetComponent<Image>();
        filtersToggle = MakeBtn(parent, "Фильтры", UiBtn.Gray, GridX + 444f, 6f, 140f, 50f, 20f,
            () => SetPopup(popup == Popup.Filters ? Popup.None : Popup.Filters), out filtersCaption);
        TMP_Text presetLabel;
        presetsToggle = MakeBtn(parent, "Наборы", UiBtn.Gray, GridX + 596f, 6f, 128f, 50f, 20f,
            () => SetPopup(Popup.Presets), out presetLabel);
        Tip(book, "Все виды питомцев, открытия и награды за коллекцию");
        Tip(presetsToggle, "Сохранить команду или выбрать сохранённый набор");

        var view = TWPetCollectionSkin.Surface(parent, "Collection");
        TL(view.rectTransform, GridX, GridY, GridW, GridH);
        var viewport = NewRT("Viewport", view.transform);
        Stretch(viewport);
        viewport.offsetMin = new Vector2(8f, 8f);
        viewport.offsetMax = new Vector2(-8f, -8f);
        viewport.gameObject.AddComponent<RectMask2D>();
        var image = viewport.gameObject.AddComponent<Image>();
        image.color = Clear;
        image.raycastTarget = true;
        contentsRt = NewRT("Cards", viewport);
        contentsRt.anchorMin = new Vector2(0f, 1f);
        contentsRt.anchorMax = Vector2.one;
        contentsRt.pivot = new Vector2(0.5f, 1f);
        contentsRt.anchoredPosition = Vector2.zero;
        contentsRt.sizeDelta = new Vector2(0f, 100f);
        scroll = view.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport; scroll.content = contentsRt;
        scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 50f;

        selFrame = TWPetCollectionSkin.Surface(contentsRt, "Selected");
        selFrame.color = TWPetCollectionSkin.Selection;
        Anch(selFrame.rectTransform, new Vector2(0f, 1f), Vector2.one * 0.5f, Vector2.zero, new Vector2(CardW + 14f, CardH + 14f));
        selFrame.gameObject.SetActive(false);
        hoverFrame = TWPetCollectionSkin.Surface(contentsRt, "Hovered");
        Anch(hoverFrame.rectTransform, new Vector2(0f, 1f), Vector2.one * 0.5f, Vector2.zero, new Vector2(CardW + 8f, CardH + 8f));
        hoverFrame.gameObject.SetActive(false);
        for (int i = 0; i < PoolSize; i++)
        {
            var cv = PetCardView.Create(contentsRt, CardW, CardH, "Pet", false);
            cv.UseCloudStyle(TWPetCollectionSkin.Current);
            var card = new PoolCard { view = cv, cg = cv.rt.gameObject.AddComponent<CanvasGroup>() };
            card.mark = AddEquippedMark(cv);
            cv.frame.gameObject.AddComponent<PetRightClick>().cb = () => OnCardRightClick(card);
            AddBtn(cv.frame, () => OnCardClick(card));
            var hover = cv.frame.gameObject.AddComponent<PetHover>();
            hover.cb = over => { if (over) hoverPc = card; else if (hoverPc == card) hoverPc = null; };
            cv.rt.gameObject.SetActive(false);
            pool.Add(card);
        }
        emptyGrid = TWPetCollectionSkin.Label(view.transform, "", 25f, Dim, TextAlignmentOptions.Center, false);
        Anch(emptyGrid.rectTransform, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(GridW - 80f, 120f));
        emptyGrid.enableWordWrapping = true;
        TMP_Text reset;
        resetBtn = MakeBtn(view.transform, "Сбросить фильтры", UiBtn.Gray, (GridW - 280f) * 0.5f, GridH * 0.5f + 76f, 280f, 48f, 22f, OnResetFilters, out reset);
        resetBtn.gameObject.SetActive(false);
        scrollTrack = UiKit.MakeImage(view.transform, "ScrollTrack", null, false);
        scrollTrack.color = new Color(0.55f, 0.50f, 0.75f, 0.15f);
        Anch(scrollTrack.rectTransform, Vector2.one, Vector2.one, new Vector2(-4f, -14f), new Vector2(5f, GridH - 28f));
        scrollHandle = UiKit.MakeImage(scrollTrack.transform, "Handle", null, false);
        scrollHandle.color = TWPetCollectionSkin.Selection;
        Anch(scrollHandle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(5f, 40f));
        scrollTrack.gameObject.SetActive(false);
    }

    void BuildDetail(Transform parent)
    {
        var detail = TWPetCollectionSkin.Surface(parent, "SelectedPet");
        TL(detail.rectTransform, DetX, DetY, DetW, DetH);
        detRoot = NewRT("Details", detail.transform);
        Stretch(detRoot);
        detCg = detRoot.gameObject.AddComponent<CanvasGroup>();
        detName = TWPetCollectionSkin.Label(detRoot, "", 34f, TWPetCollectionSkin.Ink, TextAlignmentOptions.Left);
        TL(detName.rectTransform, 24f, 18f, DetW - 138f, 44f);
        PetUi.AutoSize(detName, 25f, 34f);
        TMP_Text more;
        moreActions = MakeBtn(detRoot, "Ещё", UiBtn.Gray, DetW - 102f, 20f, 78f, 40f, 18f, () => SetPopup(Popup.Actions), out more);
        detRarity = TWPetCollectionSkin.Label(detRoot, "", 21f, Dim, TextAlignmentOptions.Left, false);
        TL(detRarity.rectTransform, 24f, 66f, DetW - 48f, 30f);
        detExtra = TWPetCollectionSkin.Label(detRoot, "", 18f, Dim, TextAlignmentOptions.Left, false);
        TL(detExtra.rectTransform, 24f, 98f, DetW - 48f, 28f);
        detCard = PetCardView.Create(detRoot, 286f, 246f, "PetModel", false);
        detCard.UseCloudStyle(TWPetCollectionSkin.Current, true);
        detCard.EnableLive();
        Anch(detCard.rt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -126f), new Vector2(286f, 246f));
        var zone = UiKit.MakeImage(detCard.rt, "PetTouch", null, false);
        zone.color = Clear; zone.raycastTarget = true;
        Stretch(zone.rectTransform);
        petZone = zone.gameObject.AddComponent<PetPetZone>();
        petZone.onPat = OnPetPat; petZone.onDrag = OnPetDrag;
        zone.gameObject.AddComponent<PetHover>().tip = "Клик — погладить   ·   Потяни мышью — покрутить";
        var skin = TWPetCollectionSkin.Current;
        petBubble = UiKit.MakeImage(detRoot, "PetSpeech", skin != null ? skin.speech : UiKit.Slot, false);
        Anch(petBubble.rectTransform, new Vector2(0.5f, 1f), Vector2.one * 0.5f, new Vector2(74f, -155f), new Vector2(132f, 68f));
        petBubbleText = TWPetCollectionSkin.Label(petBubble.transform, "", 19f, TWPetCollectionSkin.Ink);
        petBubbleText.rectTransform.offsetMin = new Vector2(8f, 15f);
        petBubbleText.rectTransform.offsetMax = new Vector2(-8f, -8f);
        petBubble.gameObject.SetActive(false);
        patHeart = UiKit.MakeImage(detRoot, "Heart", PetHeartFx.HeartSprite, false);
        patHeart.color = PetHeartFx.Pink;
        TLC(patHeart.rectTransform, 42f, 384f, 23f, 23f);
        patText = TWPetCollectionSkin.Label(detRoot, "", 18f, Dim, TextAlignmentOptions.Left, false);
        TL(patText.rectTransform, 60f, 370f, DetW - 82f, 30f);
        PetUi.AutoSize(patText, 16f, 18f);

        detailStatBox = TWPetCollectionSkin.Surface(detRoot, "Help");
        TL(detailStatBox.rectTransform, 16f, 416f, DetW - 32f, 136f);
        detailStatBox.color = new Color(0.96f, 0.95f, 1f);
        detStat = TWPetCollectionSkin.Label(detailStatBox.transform, "", 25f, TWPetCollectionSkin.Ink, TextAlignmentOptions.Left);
        TL(detStat.rectTransform, 20f, 14f, DetW - 72f, 34f);
        PetUi.AutoSize(detStat, 21f, 25f);
        detHead = TWPetCollectionSkin.Label(detailStatBox.transform, "", 18f, Dim, TextAlignmentOptions.Left, false);
        TL(detHead.rectTransform, 20f, 52f, DetW - 72f, 24f);
        detLive = TWPetCollectionSkin.Label(detailStatBox.transform, "", 22f, TWPetCollectionSkin.Ink, TextAlignmentOptions.Left, false);
        TL(detLive.rectTransform, 20f, 80f, DetW - 72f, 28f);
        PetUi.AutoSize(detLive, 18f, 22f);
        detD1 = TWPetCollectionSkin.Label(detailStatBox.transform, "", 18f, Dim);
        detD1.gameObject.SetActive(false);
        detD2 = TWPetCollectionSkin.Label(detailStatBox.transform, "", 17f, Dim, TextAlignmentOptions.Left, false);
        TL(detD2.rectTransform, 20f, 108f, DetW - 72f, 24f);
        PetUi.AutoSize(detD2, 15f, 17f);
        detNote = TWPetCollectionSkin.Label(detRoot, "", 19f, Dim, TextAlignmentOptions.Center, false);
        TL(detNote.rectTransform, 22f, 560f, DetW - 44f, 36f);
        PetUi.AutoSize(detNote, 16f, 19f);
        detEmpty = TWPetCollectionSkin.Label(detRoot, "Выбери питомца", 26f, Dim);
        Anch(detEmpty.rectTransform, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(DetW - 64f, 120f));
        detEmpty.enableWordWrapping = true;
        equipBtn = MakeBtn(detRoot, "Взять с собой", UiBtn.Yellow, 24f, 606f, DetW - 48f, 60f, 27f, OnEquipClick, out equipLabel);
        unequipBtn = MakeBtn(detRoot, "Убрать из команды", UiBtn.Gray, 24f, 606f, DetW - 48f, 60f, 24f, OnUnequipClick, out unequipLabel);
        mergeBtn = MakeBtn(detRoot, "Объединить копии", UiBtn.Gray, 24f, 676f, DetW - 48f, 44f, 21f, OnMergeClick, out mergeLabel);
        Tip(unequipBtn, "Питомец останется в коллекции");
        Tip(mergeBtn, GameBalance.PetMergeCount + " одинаковых питомца превращаются в одного с дополнительной звездой");
        BuildBook(detail.transform);
    }

    void BuildBook(Transform parent)
    {
        bookRoot = NewRT("Book", parent); Stretch(bookRoot);
        var title = TWPetCollectionSkin.Label(bookRoot, "Книга питомцев", 29f, TWPetCollectionSkin.Ink);
        TL(title.rectTransform, 20f, 24f, DetW - 40f, 40f);
        bookTotal = TWPetCollectionSkin.Label(bookRoot, "", 24f, Dim);
        TL(bookTotal.rectTransform, 20f, 80f, DetW - 40f, 34f);
        var track = TWPetCollectionSkin.Surface(bookRoot, "Progress");
        TL(track.rectTransform, 32f, 130f, DetW - 64f, 14f);
        var inner = NewRT("Inner", track.transform); Stretch(inner);
        inner.offsetMin = new Vector2(4f, 4f); inner.offsetMax = new Vector2(-4f, -4f);
        bookBarFill = UiKit.MakeImage(inner, "Fill", null, false);
        bookBarFill.color = TWPetCollectionSkin.Selection;
        Stretch(bookBarFill.rectTransform);
        for (int r = 3; r >= 0; r--)
        {
            float y = 186f + (3 - r) * 104f;
            bookName[r] = TWPetCollectionSkin.Label(bookRoot, "", 24f, TWPetCollectionSkin.Ink, TextAlignmentOptions.Left);
            TL(bookName[r].rectTransform, 28f, y, DetW - 142f, 32f);
            bookCount[r] = TWPetCollectionSkin.Label(bookRoot, "", 24f, TWPetCollectionSkin.Ink, TextAlignmentOptions.Right);
            TL(bookCount[r].rectTransform, DetW - 100f, y, 72f, 32f);
            bookCheck[r] = PetIcon(bookRoot, UiIcon.Check, 22f, "Complete");
            TLC(bookCheck[r].rectTransform, DetW - 120f, y + 16f, 22f, 22f);
            bookBonus[r] = TWPetCollectionSkin.Label(bookRoot, "", 18f, Dim, TextAlignmentOptions.Left, false);
            TL(bookBonus[r].rectTransform, 28f, y + 38f, DetW - 56f, 42f);
            bookBonus[r].enableWordWrapping = true;
        }
        var note = TWPetCollectionSkin.Label(bookRoot, "Открывай новые виды, чтобы получать бонусы коллекции.", 20f, Dim, TextAlignmentOptions.Center, false);
        TL(note.rectTransform, 30f, 642f, DetW - 60f, 60f);
        note.enableWordWrapping = true;
    }
}
