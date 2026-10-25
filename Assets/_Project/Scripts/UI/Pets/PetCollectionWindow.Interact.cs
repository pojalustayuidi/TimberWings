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

        Vector2 c = InnerToFx(DetX + DetW * 0.5f, DetY + 10f + 110f);
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
                fx.Burst(c, UiKit.BrassHi, 22, 340f, 26f, 280f, 0.9f);
                fx.FloatText(c + new Vector2(0f, 90f), PetTiers[t1], UiKit.BrassHi, 36f);
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
                petBubble.rectTransform.anchoredPosition = new Vector2(0f, -34f + 2f * Mathf.Sin(bubbleT * 6f));
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
    Image gridBase, gridGlow, detGlow, detRays, equipIco;
    AchSparks fx;
    readonly int[] lastUid = { -2, -2, -2, -2 };
    int lastClickIdx = -1;
    float lastClickT;
    AchMotes gridMotes;
    Color tintCur = new Color(1f, 0.84f, 0.42f, 1f), detTintCur = new Color(1f, 0.84f, 0.42f, 1f);
    float raysAng;
    int rarFilter = -1;
    readonly Image[] rarImg = new Image[5];
    readonly Button[] rarBtn = new Button[5];

    void AnimateFx(float dt)
    {
        float tm = Time.unscaledTime;
        var si = tab == Tab.Collection ? FindInstance(selectedUid) : null;
        Color target = rarFilter >= 0 ? PetUi.RarityAccent((PetRarity)rarFilter) : (si != null ? PetUi.RarityAccent(si.data.rarity) : UiKit.Brass);
        tintCur = Color.Lerp(tintCur, target, 1f - Mathf.Exp(-8f * dt));
        float breathe = 0.5f + 0.5f * Mathf.Sin(tm * 0.8f);
        if (gridBase != null) gridBase.color = Color.Lerp(UiKit.Ink, tintCur, 0.16f);
        if (gridGlow != null)
        {
            gridGlow.color = new Color(tintCur.r, tintCur.g, tintCur.b, 0.14f + 0.07f * breathe);
            gridGlow.rectTransform.localScale = Vector3.one * (1f + 0.04f * breathe);
        }
        if (gridMotes != null) gridMotes.SetColor(tintCur);

        // свечение и лучи за большой карточкой выбранного
        Color dt2 = si != null ? PetUi.RarityAccent(si.data.rarity) : UiKit.Brass;
        detTintCur = Color.Lerp(detTintCur, dt2, 1f - Mathf.Exp(-10f * dt));
        if (detGlow != null)
        {
            detGlow.gameObject.SetActive(si != null);
            detGlow.color = new Color(detTintCur.r, detTintCur.g, detTintCur.b, 0.30f + 0.16f * Mathf.Sin(tm * 2.2f));
        }
        if (detRays != null)
        {
            bool legend = si != null && si.data.rarity == PetRarity.Legendary;
            detRays.gameObject.SetActive(legend);
            raysAng += dt * 16f;
            detRays.rectTransform.localEulerAngles = new Vector3(0f, 0f, -raysAng);
            detRays.color = new Color(detTintCur.r, detTintCur.g, detTintCur.b, 0.38f);
        }

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
            var hc = UiKit.Brass; hc.a = 0.55f * UiDraw.EaseOutCubic(hoverK);
            hoverFrame.color = hc;
        }

        // рамка выбора плавно перелетает к новой карточке и мягко «дышит»
        if (selFrame != null && selFrame.gameObject.activeSelf)
        {
            selFrame.rectTransform.anchoredPosition = Vector2.Lerp(selFrame.rectTransform.anchoredPosition, selTarget, 1f - Mathf.Exp(-18f * dt));
            selFrame.color = Color.Lerp(UiKit.Brass, UiKit.BrassHi, 0.5f + 0.5f * Mathf.Sin(tm * 3f));
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
            bonusPanel.color = Color.Lerp(Color.white, UiKit.Good, 0.6f * bonusFlash);
        }

        var pm = PetManager.Instance;
        bool canPlace = pm != null && tab == Tab.Collection && selectedUid >= 0 && !pm.IsEquipped(selectedUid);
        float pulse = 0.5f + 0.5f * Mathf.Sin(tm * 4f);
        for (int i = 0; i < slots.Length; i++)
        {
            var sl = slots[i];
            if (sl == null) continue;
            Color c = sl.baseCol;
            if (canPlace && pm.IsSlotOpen(i) && pm.GetEquipped(i) == null) c = Color.Lerp(Color.white, UiKit.Good, 0.25f + 0.35f * pulse);
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
    static Vector2 InnerToFx(float x, float y) { return new Vector2(39f + x - WinW * 0.5f, WinH * 0.5f - (54f + y)); }

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

    // коллекция: вкладки, сортировка, фильтр, окно прокрутки с пулом карточек (карточки привязывает LayoutCells)
    void BuildGrid(Transform p)
    {
        // верхняя полоса
        var colBtn = MakeBtn(p, "Коллекция", UiBtn.Green, 20f, 8f, 210f, 48f, 22f, () => SetTab(Tab.Collection), out tabColLabel);
        tabColImg = colBtn.GetComponent<Image>();
        var bookBtn = MakeBtn(p, "Книга", UiBtn.Green, 238f, 8f, 210f, 48f, 22f, () => SetTab(Tab.Book), out tabBookLabel);
        tabBookImg = bookBtn.GetComponent<Image>();
        sortBtn = MakeBtn(p, "Сорт.", UiBtn.Amber, 456f, 8f, 230f, 48f, 20f, OnSortClick, out sortLabel);
        filterBtn = MakeBtn(p, "Фильтр", UiBtn.Amber, 694f, 8f, 230f, 48f, 20f, OnFilterClick, out filterLabel);
        autoBtn = MakeBtn(p, "Подобрать лучших", UiBtn.Yellow, DetX, 8f, 290f, 48f, 22f, OnAutoClick, out autoLabel);
        BtnIco(colBtn, UiKit.Icon(UiIcon.Paw), 30f, 18f);
        BtnIco(bookBtn, UiKit.Icon(UiIcon.Book), 30f, 18f);
        BtnIco(sortBtn, PetIcons.Get(PetIco.Sort), 28f, 16f);
        BtnIco(filterBtn, PetIcons.Get(PetIco.Filter), 28f, 16f);
        BtnIco(autoBtn, PetIcons.Get(PetIco.Wand), 34f, 16f);
        Tip(colBtn, "Твои питомцы: выбирай, бери с собой, сливай дубликаты (Tab)");
        Tip(bookBtn, "Книга: все виды питомцев и награда за полный набор редкости (Tab)");
        Tip(sortBtn, "Порядок карточек: клик переключает (редкость, характеристика, имя, новые)");
        Tip(filterBtn, "Показать только питомцев одной характеристики: клик переключает");
        Tip(autoBtn, "Сам подберёт команду с наибольшим суммарным бонусом. Ctrl+Z вернёт прежний состав");

        // «Отмена»: то же, что Ctrl+Z, но видно глазами (над колонкой «Наборы»)
        undoBtn = MakeBtn(p, "Отмена", UiBtn.Gray, PrX, 8f, PrW, 48f, 22f, OnUndoClick, out undoLabel);
        BtnIco(undoBtn, PetIcons.Get(PetIco.Swap), 26f, 12f);
        Tip(undoBtn, "Вернуть прежний состав команды (Ctrl+Z). Повторное нажатие вернёт обратно");

        // фильтр по редкости (над коллекцией)
        string[] rn = { "Все", "Обычн.", "Необыч.", "Редкие", "Легенд." };
        for (int j = 0; j < 5; j++)
        {
            int jj = j;
            TMP_Text rl;
            var cb = MakeBtn(p, rn[j], UiBtn.Amber, GridX + j * 104f, ChipY, 100f, ChipH, 17f, () => OnRarClick(jj), out rl);
            rarBtn[j] = cb;
            rarImg[j] = cb.GetComponent<Image>();
            var gem = UiKit.MakeIcon(cb.transform, j == 0 ? UiIcon.Paw : UiIcon.Gem, 20f, "G");
            gem.color = j == 0 ? Color.white : PetUi.RarityAccent((PetRarity)(j - 1));
            gem.raycastTarget = false;
            Anch(gem.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(15f, 0f), new Vector2(20f, 20f));
            if (rl != null)
            {
                rl.rectTransform.offsetMin = new Vector2(28f, rl.rectTransform.offsetMin.y);
                rl.rectTransform.offsetMax = new Vector2(-4f, rl.rectTransform.offsetMax.y);
            }
        }

        var view = UiKit.MakeImage(p, "View", UiKit.Slot, true, 1f);
        TL(view.rectTransform, GridX, GridY, GridW, GridH);

        // сцена как у «Достижений»: тёмная подложка цвета редкости, дышащее свечение, пылинки
        gridBase = UiKit.MakeImage(view.transform, "Base", null, false);
        Stretch(gridBase.rectTransform);
        gridBase.rectTransform.offsetMin = new Vector2(8f, 8f);
        gridBase.rectTransform.offsetMax = new Vector2(-8f, -8f);
        gridBase.raycastTarget = false;
        gridGlow = UiKit.MakeImage(view.transform, "Glow", PetUi.GlowSprite, false);
        gridGlow.raycastTarget = false;
        Anch(gridGlow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(GridW * 1.1f, GridH * 0.8f));
        gridMotes = AchMotes.Create(view.transform, GridW, GridH);

        var vp = NewRT("Viewport", view.transform);
        Stretch(vp);
        vp.offsetMin = new Vector2(8f, 8f);
        vp.offsetMax = new Vector2(-8f, -8f);
        vp.gameObject.AddComponent<RectMask2D>();
        var vpImg = vp.gameObject.AddComponent<Image>();
        vpImg.color = Clear;
        vpImg.raycastTarget = true;                                  // ловит перетаскивание

        contentsRt = NewRT("Content", vp);
        contentsRt.anchorMin = new Vector2(0f, 1f);
        contentsRt.anchorMax = new Vector2(1f, 1f);
        contentsRt.pivot = new Vector2(0.5f, 1f);
        contentsRt.anchoredPosition = Vector2.zero;
        contentsRt.sizeDelta = new Vector2(0f, 100f);

        scroll = view.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = vp;
        scroll.content = contentsRt;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;
        scroll.inertia = true;

        // рамка выбранной карточки: лежит под всеми карточками, выглядывает по краям
        selFrame = UiKit.MakeImage(contentsRt, "Select", UiKit.Slot, true, 1f);
        selFrame.color = UiKit.BrassHi;
        selFrame.raycastTarget = false;
        Anch(selFrame.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CardW + 14f, CardH + 14f));
        selFrame.gameObject.SetActive(false);

        // подсветка под курсором (под карточками, как рамка выбранного)
        hoverFrame = UiKit.MakeImage(contentsRt, "Hover", UiKit.Slot, true, 1f);
        hoverFrame.raycastTarget = false;
        Anch(hoverFrame.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CardW + 10f, CardH + 10f));
        hoverFrame.gameObject.SetActive(false);

        // пул карточек: создаётся один раз
        for (int k = 0; k < PoolSize; k++)
        {
            var cv = PetCardView.Create(contentsRt, CardW, CardH, "Pet", false);
            var pc = new PoolCard { view = cv };
            pc.cg = cv.rt.gameObject.AddComponent<CanvasGroup>();
            pc.mark = AddEquippedMark(cv);
            cv.frame.gameObject.AddComponent<PetRightClick>().cb = () => OnCardRightClick(pc);
            cv.frame.raycastTarget = true;
            var btn = cv.frame.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = cv.frame;
            btn.onClick.AddListener(() => OnCardClick(pc));
            var hov = cv.frame.gameObject.AddComponent<PetHover>();
            hov.cb = on => { if (on) hoverPc = pc; else if (hoverPc == pc) hoverPc = null; };
            cv.rt.gameObject.SetActive(false);
            pool.Add(pc);
        }

        var vig = UiKit.MakeImage(view.transform, "Vignette", AchUi.Vignette, false);
        vig.raycastTarget = false;
        Stretch(vig.rectTransform);

        // пустая коллекция: подсказка поверх окна прокрутки
        emptyGrid = UiKit.MakeLabel(view.transform, "", 26f, Dim, TextAlignmentOptions.Center, false);
        Anch(emptyGrid.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(GridW - 80f, 140f));
        emptyGrid.enableWordWrapping = true;
        emptyGrid.gameObject.SetActive(false);

        TMP_Text rl2;
        resetBtn = MakeBtn(view.transform, "Сбросить фильтры", UiBtn.Amber, (GridW - 260f) * 0.5f, GridH * 0.5f + 56f, 260f, 50f, 22f, OnResetFilters, out rl2);
        resetBtn.gameObject.SetActive(false);

        // индикатор прокрутки
        scrollTrack = UiKit.MakeImage(view.transform, "ScrollTrack", null, false);
        scrollTrack.raycastTarget = false;
        scrollTrack.color = new Color(0f, 0f, 0f, 0.35f);
        Anch(scrollTrack.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-3f, -14f), new Vector2(5f, GridH - 28f));
        scrollHandle = UiKit.MakeImage(scrollTrack.transform, "Handle", null, false);
        scrollHandle.raycastTarget = false;
        scrollHandle.color = new Color(UiKit.BrassHi.r, UiKit.BrassHi.g, UiKit.BrassHi.b, 0.85f);
        Anch(scrollHandle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(5f, 40f));
        scrollTrack.gameObject.SetActive(false);
    }

    // правая панель: выбранный питомец крупно, что он даёт, кнопки
    void BuildDetail(Transform p)
    {
        var det = UiKit.MakeImage(p, "Detail", UiKit.Slot, true, 1f);
        TL(det.rectTransform, DetX, DetY, DetW, DetH);

        detRoot = NewRT("DetailRoot", det.transform);
        Stretch(detRoot);
        detCg = detRoot.gameObject.AddComponent<CanvasGroup>();      // плавное появление при смене питомца
        var t = detRoot;

        // свечение и лучи за карточкой (цвет редкости, лучи только у легендарных)
        const float CW = 200f, CH = 236f;
        float cx = DetW * 0.5f, cyc = 10f + CH * 0.5f;
        detRays = UiKit.MakeImage(t, "Rays", PetUi.RaysSprite, false);
        detRays.raycastTarget = false;
        Anch(detRays.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(cx, -cyc), new Vector2(520f, 520f));
        detRays.gameObject.SetActive(false);
        detGlow = UiKit.MakeImage(t, "Glow", PetUi.GlowSprite, false);
        detGlow.raycastTarget = false;
        Anch(detGlow.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(cx, -cyc), new Vector2(380f, 380f));

        detCard = PetCardView.Create(t, CW, CH, "DetailCard", false);
        detCard.EnableLive();        // питомец вращается живой 3D-моделью (стадия роста по PetManager.GrowthStage)
        Anch(detCard.rt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(CW, CH));

        float w = DetW - 32f;
        detName = UiKit.MakeLabel(t, "", 32f, Color.white, TextAlignmentOptions.Center, true);
        TL(detName.rectTransform, 16f, 254f, w, 40f);
        PetUi.AutoSize(detName, 16f, 32f);

        detRarity = UiKit.MakeLabel(t, "", 24f, Color.white, TextAlignmentOptions.Center, true);
        TL(detRarity.rectTransform, 16f, 294f, w, 28f);
        PetUi.AutoSize(detRarity, 14f, 24f);

        detExtra = UiKit.MakeLabel(t, "", 20f, Soft, TextAlignmentOptions.Top, false);
        TL(detExtra.rectTransform, 16f, 322f, w, 26f);
        detExtra.enableWordWrapping = true;

        // блок «что даёт»
        var box = UiKit.MakeImage(t, "StatBox", UiKit.Slot, true, 0.6f);
        TL(box.rectTransform, 10f, 352f, DetW - 20f, 162f);
        box.raycastTarget = false;

        detStat = UiKit.MakeLabel(t, "", 26f, Color.white, TextAlignmentOptions.Center, true);
        TL(detStat.rectTransform, 16f, 358f, w, 30f);
        PetUi.AutoSize(detStat, 14f, 26f);

        detHead = UiKit.MakeLabel(t, "", 20f, Dim, TextAlignmentOptions.Center, false);
        TL(detHead.rectTransform, 16f, 390f, w, 22f);
        PetUi.AutoSize(detHead, 12f, 20f);

        detD1 = UiKit.MakeLabel(t, "", 22f, Color.white, TextAlignmentOptions.Center, false);
        TL(detD1.rectTransform, 16f, 413f, w, 24f);
        PetUi.AutoSize(detD1, 12f, 22f);

        detD2 = UiKit.MakeLabel(t, "", 22f, Color.white, TextAlignmentOptions.Center, false);
        TL(detD2.rectTransform, 16f, 437f, w, 24f);
        PetUi.AutoSize(detD2, 12f, 22f);

        detLive = UiKit.MakeLabel(t, "", 22f, Color.white, TextAlignmentOptions.Center, false);
        TL(detLive.rectTransform, 16f, 461f, w, 24f);
        PetUi.AutoSize(detLive, 12f, 22f);

        detNote = UiKit.MakeLabel(t, "", 20f, Color.white, TextAlignmentOptions.Center, false);
        TL(detNote.rectTransform, 16f, 485f, w, 24f);
        PetUi.AutoSize(detNote, 12f, 20f);

        detEmpty = UiKit.MakeLabel(t, "Выбери питомца", 28f, Dim, TextAlignmentOptions.Center, true);
        Anch(detEmpty.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(DetW - 30f, 60f));

        // «Взять с собой» («Заменить») и «Снять» делят одно место: показывается одна из двух
        float bw = DetW - 40f;
        equipBtn = MakeBtn(t, "Взять с собой", UiBtn.Green, 20f, 528f, bw, 58f, 28f, OnEquipClick, out equipLabel);
        unequipBtn = MakeBtn(t, "Снять", UiBtn.Amber, 20f, 528f, bw, 58f, 28f, OnUnequipClick, out unequipLabel);
        mergeBtn = MakeBtn(t, "Слить", UiBtn.Amber, 20f, 594f, bw, 44f, 22f, OnMergeClick, out mergeLabel);
        releaseBtn = MakeBtn(t, "Отпустить", UiBtn.Gray, 20f, 646f, bw, 42f, 22f, OnReleaseClick, out releaseLabel);
        releaseColor = releaseLabel.color;
        equipIco = BtnIco(equipBtn, PetIcons.Get(PetIco.Plus), 38f, 22f);
        BtnIco(unequipBtn, PetIcons.Get(PetIco.Minus), 38f, 22f);
        BtnIco(mergeBtn, PetIcons.Get(PetIco.Merge), 30f, 16f);
        BtnIco(releaseBtn, PetIcons.Get(PetIco.Release), 28f, 16f);
        Tip(equipBtn, "Взять питомца в команду: он начнёт усиливать тебя (Enter, двойной клик или ПКМ)");
        Tip(unequipBtn, "Убрать питомца из команды: он останется в коллекции (Enter, двойной клик или ПКМ)");
        Tip(mergeBtn, GameBalance.PetMergeCount + " одинаковых питомца сливаются в одного с +1 звездой: его бонус растёт");
        Tip(releaseBtn, "Отпустить навсегда: питомец исчезнет. Нужно подтвердить вторым нажатием");

        // полоса таймера подтверждения «Отпустить» (сжимается за ArmTime секунд)
        armBar = UiKit.MakeImage(releaseBtn.transform, "ArmBar", null, false);
        armBar.raycastTarget = false;
        armBar.color = UiKit.Warn;
        var arb = armBar.rectTransform;
        arb.anchorMin = new Vector2(0f, 0f);
        arb.anchorMax = new Vector2(1f, 0f);
        arb.pivot = new Vector2(0f, 0f);
        arb.offsetMin = new Vector2(12f, 7f);
        arb.offsetMax = new Vector2(-12f, 11f);
        armBar.gameObject.SetActive(false);

        // ---- интерактив: питомца можно погладить (клик) и покрутить (перетаскивание)
        var zone = UiKit.MakeImage(detCard.rt, "PetZone", null, false);
        zone.color = new Color(1f, 1f, 1f, 0f);
        zone.raycastTarget = true;
        Stretch(zone.rectTransform);
        petZone = zone.gameObject.AddComponent<PetPetZone>();
        petZone.onPat = OnPetPat;
        petZone.onDrag = OnPetDrag;
        zone.gameObject.AddComponent<PetHover>().tip = "Клик: погладить питомца  ·  потяни мышью: покрутить";

        // реплика питомца над карточкой (пузырь появляется и гаснет сам)
        petBubble = UiKit.MakeImage(t, "PetBubble", UiKit.Slot, true, 0.7f);
        Anch(petBubble.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -34f), new Vector2(150f, 38f));
        petBubble.raycastTarget = false;
        petBubbleText = UiKit.MakeLabel(petBubble.transform, "", 20f, UiKit.BrassHi, TextAlignmentOptions.Center, true);
        PetUi.AutoSize(petBubbleText, 12f, 20f);
        petBubble.gameObject.SetActive(false);

        // строка внизу: сердечко и уровень привязанности
        patHeart = UiKit.MakeImage(t, "PatHeart", PetHeartFx.HeartSprite, false);
        patHeart.color = PetHeartFx.Pink;
        patHeart.preserveAspect = true;
        patHeart.raycastTarget = false;
        TLC(patHeart.rectTransform, 34f, 705f, 24f, 24f);
        patText = UiKit.MakeLabel(t, "", 20f, Soft, TextAlignmentOptions.Left, false);
        TL(patText.rectTransform, 52f, 692f, DetW - 70f, 26f);
        PetUi.AutoSize(patText, 12f, 20f);

        BuildBook(det.transform);
    }

    // Книга питомцев: общий прогресс, по строке на каждую редкость
    void BuildBook(Transform p)
    {
        bookRoot = NewRT("BookRoot", p);
        Stretch(bookRoot);
        var t = bookRoot;

        var title = UiKit.MakeLabel(t, "Книга питомцев", 28f, Color.white, TextAlignmentOptions.Center, true);
        TL(title.rectTransform, 10f, 10f, DetW - 20f, 34f);

        bookTotal = UiKit.MakeLabel(t, "", 24f, Soft, TextAlignmentOptions.Center, false);
        TL(bookTotal.rectTransform, 10f, 46f, DetW - 20f, 30f);

        var barBg = UiKit.MakeImage(t, "BarBg", UiKit.Slot, true, 0.5f);
        TL(barBg.rectTransform, 20f, 82f, DetW - 40f, 20f);
        barBg.raycastTarget = false;
        var inner = NewRT("BarInner", barBg.transform);
        Stretch(inner);
        inner.offsetMin = new Vector2(5f, 5f);
        inner.offsetMax = new Vector2(-5f, -5f);
        bookBarFill = UiKit.MakeImage(inner, "BarFill", null, false);
        bookBarFill.color = UiKit.Good;
        bookBarFill.raycastTarget = false;
        bookBarFill.rectTransform.anchorMin = Vector2.zero;
        bookBarFill.rectTransform.anchorMax = new Vector2(0f, 1f);
        bookBarFill.rectTransform.offsetMin = bookBarFill.rectTransform.offsetMax = Vector2.zero;

        for (int r = 3; r >= 0; r--)
        {
            float y0 = 130f + (3 - r) * 100f;
            PetRarity rar = (PetRarity)r;
            var gem = UiKit.MakeIcon(t, UiIcon.Gem, 28f, "Gem" + r);
            gem.color = PetUi.RarityAccent(rar);
            TLC(gem.rectTransform, 32f, y0 + 15f, 28f, 28f);

            bookName[r] = UiKit.MakeLabel(t, "", 24f, Color.white, TextAlignmentOptions.Left, true);
            TL(bookName[r].rectTransform, 54f, y0, 140f, 30f);
            PetUi.AutoSize(bookName[r], 13f, 24f);

            bookCount[r] = UiKit.MakeLabel(t, "", 24f, Color.white, TextAlignmentOptions.Right, true);
            TL(bookCount[r].rectTransform, DetW - 100f, y0, 80f, 30f);

            bookCheck[r] = UiKit.MakeIcon(t, UiIcon.Check, 24f, "Done" + r);
            bookCheck[r].color = UiKit.Good;
            TLC(bookCheck[r].rectTransform, DetW - 118f, y0 + 15f, 24f, 24f);

            bookBonus[r] = UiKit.MakeLabel(t, "", 18f, Dim, TextAlignmentOptions.Left, false);
            TL(bookBonus[r].rectTransform, 20f, y0 + 38f, DetW - 40f, 44f);
            bookBonus[r].enableWordWrapping = true;
            PetUi.AutoSize(bookBonus[r], 12f, 20f);
        }

        var note = UiKit.MakeLabel(t, "Рюкзак Книга не усиливает. Сумма с питомцами не выше +" + PetUi.Pct(GameBalance.PetBonusCap) + "%.", 17f, Dim, TextAlignmentOptions.Center, false);
        TL(note.rectTransform, 14f, 560f, DetW - 28f, 60f);
        note.enableWordWrapping = true;
    }
}
