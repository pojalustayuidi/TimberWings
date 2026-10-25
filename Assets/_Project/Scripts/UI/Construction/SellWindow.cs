// Assets/_Project/Scripts/UI/Construction/SellWindow.cs (НЕ в Editor)
// Окно продажи у NPC-торговца в стиле Candy (UiCandy.Window, Card, Stepper, Chip, желейные кнопки). Всё строится из кода, настраивать в сцене
// ничего не нужно (создаётся при первом E у торговца, см. TradingStall.Interact).
//   Строки: бревно и доска (рыба и брус позже: строка добавляется в Awake и SellItem/Pricing). Каждая строка это UiCandy.Card.
//   Колонки: иконка, «в рюкзаке», сколько продать (Stepper «− число +» и «МАКС»), цена за штуку чипом с монетой (с учётом «Торга»).
//   Резерв: доски, которые ещё нужны стройкам (BuildingController.PlanksWanted), по умолчанию не продаются.
//     При открытии ничего не выбрано (0): игрок сам решает, сколько продать, окно не предлагает «продать всё» заранее.
//     «МАКС» = всё, кроме резерва; когда выбран максимум, кнопка превращается в «СБРОС» (в 0, серая). «+» можно зайти в резерв.
//     Предупреждение про резерв: лимонная плашка под названием («Оставить N на стройку» / «Заберёте у стройки: N»). Красного нет.
//     «+» и «−» при удержании повторяются и ускоряются (HoldRepeat). Рядом с ценой строка показывает сумму за выбранное.
//   Внизу крупный чип «Итого» с монетой и три кнопки: «Продать выбранное» (Enter), «Продать лишнее» (Пробел, всё кроме резерва),
//   «Продать всё» (если трогает резерв, просит нажать второй раз в течение 3 секунд; сверху лимонная плашка).
// Клавиши: ↑↓ строка, ←→ количество (зажатие повторяет, с Shift по 5), Пробел МАКС/сброс для строки, Enter продать выбранное, E или Esc закрыть.
// Окно само закрывается, если отойти от торговца (радиус × 1,5), начался разговор или открылась прокачка.
// Пока окно открыто, MobileInput.ModalOpen = true: персонаж стоит, рубка и E «сквозь» окно не работают.
// CANDY: окно выскакивает через CandyPopIn (он на панели UiCandy.Window), поэтому масштаб панели здесь не трогаем, плавно меняется только
// прозрачность затемнения. Все сборщики UiKit идут внутри using (UiKit.UseSkin(UiSkin.Candy)).
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UiLayout;

public class SellWindow : MonoBehaviour
{
    public static SellWindow Instance { get; private set; }

    public bool IsOpen { get { return open; } }
    public static bool IsOpenFor(TradingStall s) { return Instance != null && Instance.open && Instance.stall == s; }

    public static void Toggle(TradingStall s)
    {
        var win = GetOrCreate();
        if (win.open && win.stall == s) win.Close(); else win.Open(s);
    }

    static SellWindow GetOrCreate()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("SellWindow");
        return go.AddComponent<SellWindow>();   // Awake выставит Instance
    }

    // ------------------------------------------------------------ цвета (палитра UiCandy; для rich text нужны hex)
    const string HexDone = "#2A9573";    // UiCandy.Done
    const string HexGold = "#9A5F00";    // UiCandy.GoldText

    // ------------------------------------------------------------ размеры (единицы Canvas 1920x1080)
    const float WinW = 1100f;                            // окно
    const float InnerW = WinW - 48f;                     // ширина содержимого UiCandy.Window (поля по 24)
    const float ChromeH = 15f * UiCandy.WindowScale + 10f + 32f * UiCandy.WindowScale + 10f;   // «юбка» снизу и лента сверху
    const float HeadH = 40f, RowH = 120f, RowGap = 10f;  // шапка колонок, строка-карточка, зазор между строками
    const float GapH = 14f, FootH = 250f;                // зазор и нижняя карточка (итог и кнопки)

    class Btn
    {
        public Button btn;
        public TMP_Text txt;
        public Color fg;
        public UiBtn style;
    }

    // лимонная плашка с короткой фразой (предупреждение, а не ошибка)
    class Plaque
    {
        public Image bg;
        public TMP_Text txt;
    }

    class Row
    {
        public SellItem item;
        public UiIcon icon;
        public string title;
        public int count;                 // сколько продаём
        public UiCandy.CardRefs card;     // строка-карточка (выбранная подкрашивается лавандой)
        public TMP_Text haveText, subText;
        public UiCandy.StepperRefs stepper;
        public UiCandy.ChipRefs priceChip;
        public Plaque reserve;
        public Btn max;
    }

    // ------------------------------------------------------------ состояние
    TradingStall stall;
    PlayerInventory inv;
    Row[] rows;
    bool open, built, confirmAll;
    float openK, refreshAt, holdAt, confirmUntil;
    int selected, openFrame = -1;

    CanvasGroup overlay;
    RectTransform panel;
    TMP_Text summaryText;
    UiCandy.ChipRefs totalChip;
    Plaque reasonPlaque;
    Btn sellSel, sellSurplus, sellAll;

    // ------------------------------------------------------------ жизненный цикл
    void Awake()
    {
        Instance = this;
        UiModal.Register(this, () => IsOpen, Close);
        rows = new[]
        {
            new Row { item = SellItem.Log, icon = UiIcon.Log, title = "Бревно" },
            new Row { item = SellItem.Plank, icon = UiIcon.Plank, title = "Доска" }
        };
    }

    void OnDestroy() { UiModal.Unregister(this); if (Instance == this) Instance = null; }

    public void Open(TradingStall s)
    {
        if (s == null) return;
        if (UpgradeWindow.Instance != null && UpgradeWindow.Instance.IsOpen) return;     // два окна сразу не нужны
        if (PlankWorkshopWindow.Instance != null && PlankWorkshopWindow.Instance.IsOpen) return;
        inv = s.Inventory;
        if (inv == null) return;

        EnsureBuilt();
        stall = s;
        open = true;
        openFrame = Time.frameCount;      // E, которой открыли окно, его же не закроет
        selected = 0;
        confirmAll = false;
        ResetCounts();
        Refresh();
    }

    public void Close() { open = false; confirmAll = false; }

    void EnsureBuilt()
    {
        if (built) return;
        UiInputGuard.EnsureEventSystem();
        Build();
    }

    void Update()
    {
        if (!built) return;

        if (open)
        {
            bool upgradeOpen = UpgradeWindow.Instance != null && UpgradeWindow.Instance.IsOpen;
            if (stall == null || !stall.isActiveAndEnabled || !stall.Unlocked || inv == null
                || DialogueManager.IsBusy || DialogueSequence.AnyPlaying || upgradeOpen
                || stall.DistanceToPlayer() > stall.Radius * 1.5f)
                Close();
            else
            {
                // выбранная кнопка реагировала бы ещё и на Enter: снимаем выделение, Enter обрабатывает только Hotkeys
                var es = EventSystem.current;
                if (es != null && es.currentSelectedGameObject != null) es.SetSelectedGameObject(null);
                Hotkeys();
            }
        }

        openK = Mathf.MoveTowards(openK, open ? 1f : 0f, Time.unscaledDeltaTime / 0.15f);
        bool show = openK > 0.001f;
        if (overlay.gameObject.activeSelf != show) overlay.gameObject.SetActive(show);   // включение запускает CandyPopIn окна
        if (!show) return;

        overlay.alpha = openK;

        if (open && Time.unscaledTime >= refreshAt) Refresh();
    }

    void Hotkeys()
    {
        if (Time.frameCount == openFrame) return;
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(MobileInput.ActionKey)) { Close(); return; }

        if (Input.GetKeyDown(KeyCode.UpArrow)) MoveSel(-1);
        if (Input.GetKeyDown(KeyCode.DownArrow)) MoveSel(1);
        RepeatKey(KeyCode.LeftArrow, -1);
        RepeatKey(KeyCode.RightArrow, 1);
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) SellSelected();
        if (Input.GetKeyDown(KeyCode.Space)) SetMax(selected);       // пробел больше ничего не продаёт сам: только МАКС / сброс
    }

    // стрелка: шаг сразу, при удержании повтор через 0,35 с и дальше каждые 0,05 с
    void RepeatKey(KeyCode k, int d)
    {
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        d *= shift ? 5 : 1;
        if (Input.GetKeyDown(k)) { Step(d); holdAt = Time.unscaledTime + 0.35f; }
        else if (Input.GetKey(k) && Time.unscaledTime >= holdAt) { Step(d); holdAt = Time.unscaledTime + 0.05f; }
    }

    // ------------------------------------------------------------ данные строки
    int Have(Row r)
    {
        if (inv == null) return 0;
        return r.item == SellItem.Log ? inv.woodCount : inv.planksCount;
    }

    // сколько штук держим для стройки (сейчас только доски)
    int Reserve(Row r)
    {
        if (r.item != SellItem.Plank) return 0;
        return Mathf.Min(BuildingController.PlanksWanted(), Have(r));
    }

    int Free(Row r) { return Have(r) - Reserve(r); }

    void ResetCounts()
    {
        for (int i = 0; i < rows.Length; i++) rows[i].count = 0;      // выбирает игрок
    }

    // ------------------------------------------------------------ действия
    void Select(int i)
    {
        selected = Mathf.Clamp(i, 0, rows.Length - 1);
        confirmAll = false;
        Refresh();
    }

    void MoveSel(int d) { Select(selected + d); }

    void Step(int d)
    {
        var r = rows[selected];
        r.count = Mathf.Clamp(r.count + d, 0, Have(r));
        confirmAll = false;
        Refresh();
    }

    void SetMax(int i)
    {
        selected = i;
        var r = rows[i];
        int free = Free(r);
        r.count = (r.count > 0 && r.count >= free) ? 0 : free;       // второй раз подряд = сброс
        confirmAll = false;
        Refresh();
    }

    void SellSelected()
    {
        var amt = new int[rows.Length];
        for (int i = 0; i < rows.Length; i++) amt[i] = rows[i].count;
        DoSell(amt);
    }

    void SellSurplus()
    {
        var amt = new int[rows.Length];
        for (int i = 0; i < rows.Length; i++) amt[i] = Free(rows[i]);
        DoSell(amt);
    }

    void SellAll()
    {
        int reserve = 0;
        var amt = new int[rows.Length];
        for (int i = 0; i < rows.Length; i++) { amt[i] = Have(rows[i]); reserve += Reserve(rows[i]); }

        // трогает резерв: нужно подтверждение вторым нажатием
        if (reserve > 0 && !(confirmAll && Time.unscaledTime <= confirmUntil))
        {
            confirmAll = true;
            confirmUntil = Time.unscaledTime + 3f;
            Refresh();
            return;
        }
        DoSell(amt);
    }

    void DoSell(int[] amt)
    {
        if (stall == null || inv == null) return;

        int total = 0, any = 0;
        for (int i = 0; i < rows.Length; i++)
        {
            amt[i] = Mathf.Clamp(amt[i], 0, Have(rows[i]));   // RemoveWood молча ничего не делает, если просить больше, чем есть
            any += amt[i];
            total += Pricing.Price(rows[i].item, amt[i]);
        }
        confirmAll = false;
        if (any <= 0 || total <= 0) { Refresh(); return; }

        bool bazaar = HostEffects.BazaarReady;     // фиксируем до расчёта: итог и расход бонуса должны совпасть
        total = Pricing.ApplySale(total, any);

        for (int i = 0; i < rows.Length; i++)
        {
            if (amt[i] <= 0) continue;
            if (rows[i].item == SellItem.Log) inv.RemoveWood(amt[i]);
            else inv.RemovePlanks(amt[i]);
        }
        inv.AddCoins(total);
        if (bazaar) HostEffects.ConsumeBazaar();   // продажа состоялась: Базарный день уходит на перезарядку

        var popups = RewardPopups.Instance;
        if (popups != null) popups.ShowText(stall.PromptPoint, "+" + total + " " + UiDraw.Plural(total, "монета", "монеты", "монет"), stall.coinColor, 48f);
        if (HudGainAnchor.Find(HudGain.Kind.Coin) != null) HudGain.Show(HudGain.Kind.Coin, total);
        int soldLogs = 0, soldPlanks = 0;
        for (int i = 0; i < rows.Length; i++)
        {
            if (rows[i].item == SellItem.Log) soldLogs += amt[i];
            else soldPlanks += amt[i];
        }
        stall.ReportSold(total, soldLogs, soldPlanks);

        ResetCounts();
        Refresh();
    }

    // ------------------------------------------------------------ обновление
    static void Set(TMP_Text t, string s) { if (t != null && t.text != s) t.text = s; }

    static void SetBtn(Btn b, string label, bool enabled)
    {
        Set(b.txt, label);
        b.txt.color = enabled ? b.fg : UiCandy.Muted;
        b.btn.interactable = enabled;
    }

    // цвет готовой кнопки (МАКС жёлтая, СБРОС серая); спрайты меняем только при смене стиля
    static void Restyle(Btn b, UiBtn style)
    {
        if (b.style == style) return;
        b.style = style;
        UiCandy.SetStyle(b.btn, style);
    }

    static void SetPlaque(Plaque p, string s)
    {
        bool on = !string.IsNullOrEmpty(s);
        if (p.bg.gameObject.activeSelf != on) p.bg.gameObject.SetActive(on);
        if (on) Set(p.txt, s);
    }

    void Refresh()
    {
        refreshAt = Time.unscaledTime + 0.1f;
        if (!built || stall == null || inv == null) return;
        if (confirmAll && Time.unscaledTime > confirmUntil) confirmAll = false;

        int totalCoins = 0, selQty = 0, haveQty = 0, freeQty = 0, reserveQty = 0, takenQty = 0;

        for (int i = 0; i < rows.Length; i++)
        {
            var r = rows[i];
            int have = Have(r), res = Reserve(r), free = have - res;
            r.count = Mathf.Clamp(r.count, 0, have);

            Set(r.haveText, have.ToString());
            r.haveText.color = have > 0 ? UiCandy.Ink : UiCandy.Muted;
            Set(r.priceChip.label, Pricing.UnitPrice(r.item).ToString("0.##"));

            // счётчик: «+» гаснет на «сколько есть», «−» на нуле (это делает сам Stepper); число пишем только при изменении
            if (r.stepper.Value != r.count || r.stepper.Max != have)
            {
                r.stepper.Max = have;
                r.stepper.Set(r.count);
            }

            // строка про резерв: лимонная плашка («оставить» и «заберёте у стройки» одного цвета, без красного)
            int into = Mathf.Max(0, r.count - free);
            if (res > 0) SetPlaque(r.reserve, into > 0 ? "Заберёте у стройки: " + into : "Оставить " + res + " на стройку");
            else SetPlaque(r.reserve, "");

            bool isReset = r.count > 0 && r.count >= free;
            SetBtn(r.max, isReset ? "СБРОС" : "МАКС", free > 0 || r.count > 0);
            Restyle(r.max, isReset ? UiBtn.Gray : UiBtn.Amber);
            Set(r.subText, r.count > 0 ? "= " + Pricing.Price(r.item, r.count) : "");

            r.card.bg.color = UiCandy.TintOf(i == selected ? UiTone.Lavender : UiTone.Neutral);

            totalCoins += Pricing.Price(r.item, r.count);
            selQty += r.count;
            haveQty += have;
            freeQty += free;
            reserveQty += res;
            takenQty += into;
        }

        totalCoins = Pricing.ApplySale(totalCoins, selQty);   // «Оптовик» и «Базарный день»
        Set(totalChip.label, "Итого: <color=" + HexGold + ">" + totalCoins + "</color> " + UiDraw.Plural(totalCoins, "монета", "монеты", "монет"));

        string reason = "";
        if (haveQty <= 0)
        {
            Set(summaryText, "Нечего продавать");
        }
        else if (selQty <= 0)
        {
            Set(summaryText, freeQty > 0 ? "Выбери, сколько продать: «+» или «МАКС»" : "Всё, что есть, нужно для стройки");
        }
        else
        {
            string note = Pricing.SaleNote(selQty);
            Set(summaryText, "Выбрано: " + selQty + " шт." + (note.Length > 0 ? "  ·  <color=" + HexDone + ">" + note + "</color>" : ""));
            if (takenQty > 0) reason = "Из них у стройки: " + takenQty + ". Доски придётся делать заново";
        }

        SetBtn(sellSel, "Продать выбранное", selQty > 0);
        SetBtn(sellSurplus, "Продать лишнее", freeQty > 0);
        SetBtn(sellAll, confirmAll ? "Точно? Продать всё" : "Продать всё", haveQty > 0);

        if (confirmAll && haveQty > 0 && reserveQty > 0)
            reason = "Это заберёт и доски для стройки (" + reserveQty + "). Нажми ещё раз, чтобы подтвердить";
        SetPlaque(reasonPlaque, reason);
    }

    // ------------------------------------------------------------ сборка UI
    void Build()
    {
        using (UiKit.UseSkin(UiSkin.Candy))
        {
            int n = rows.Length;
            float rowsH = HeadH + n * RowH + (n - 1) * RowGap;
            float innerH = rowsH + GapH + FootH;
            float winH = innerH + ChromeH;

            var cgo = new GameObject("SellCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
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

            // окно: облачная панель с лавандовой лентой, «×» и CandyPopIn делает UiCandy.Window (клик по окну его не закрывает)
            var win = UiCandy.Window(ov.transform, new Vector2(WinW, winH), "ТОРГОВЕЦ", Close, "Window");
            panel = win.root;
            if (win.close != null)
            {
                win.close.navigation = new Navigation { mode = Navigation.Mode.None };   // Enter и стрелки крестик не жмут
                UiClickFallback.Register(win.close, () => open && openK > 0.5f, true);
            }
            BuildHeader();

            // рабочая область по центру содержимого окна
            var body = NewRT("Body", win.content);
            Anch(body, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(InnerW, innerH));

            BuildRows(body);
            BuildFooter(body, rowsH + GapH);

            // подсказка клавиш: светлая пилюля под окном (на затемнении голый текст не читался бы)
            var hintBg = UiKit.MakeImage(ov.transform, "HintPill", UiKit.Slot, true, 0.7f);
            hintBg.color = new Color(1f, 1f, 1f, 0.92f);
            Anch(hintBg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0, -(winH * 0.5f + 14f)), new Vector2(1560, 46));
            UiKit.MakeLabel(hintBg.transform, "↑↓ строка   ·   ← → количество (Shift ×5)   ·   Пробел МАКС / сброс   ·   Enter продать выбранное   ·   E или Esc закрыть", 22f, UiCandy.Muted, TextAlignmentOptions.Center, false);

            ov.gameObject.SetActive(false);
            built = true;
        }
    }

    // значок монеты слева от названия (название и «×» делает UiCandy.Window)
    void BuildHeader()
    {
        var icon = UiCandy.Icon(panel, UiIcon.Coin, 44f, "TitleIcon");
        TLC(icon.rectTransform, WinW * 0.5f - 135f, 27, 44, 44);
    }

    // ------------------------------------------------------------ строки товаров: по карточке на товар
    void BuildRows(Transform body)
    {
        // шапка колонок: те же x, что у содержимого карточек
        Lbl(body, "Товар", 20f, UiCandy.Muted, TextAlignmentOptions.Left, false, 126, 6, 200, 28);
        Lbl(body, "В рюкзаке", 20f, UiCandy.Muted, TextAlignmentOptions.Center, false, 404, 6, 120, 28);
        Lbl(body, "Сколько продать", 20f, UiCandy.Muted, TextAlignmentOptions.Center, false, 528, 6, 352, 28);
        Lbl(body, "Цена за шт.", 20f, UiCandy.Muted, TextAlignmentOptions.Center, false, 890, 6, 150, 28);

        for (int i = 0; i < rows.Length; i++)
        {
            int idx = i;                      // для замыканий кнопок
            var r = rows[i];
            float y0 = HeadH + i * (RowH + RowGap);

            // карточка: медальон с иконкой и название; клик по ней выбирает строку
            var card = UiCandy.Card(body, RowH, r.title, null, r.icon, UiTone.Neutral, 0f, "Row" + i);
            r.card = card;
            TL(card.rt, 0, y0, InnerW, RowH);
            AddClick(card.bg, () => Select(idx));
            var c = card.bg.transform;

            // название сверху, под ним лимонная плашка про резерв (место для неё всегда занято)
            TL(card.title.rectTransform, 126, 12, 280, 40);
            card.title.alignment = TextAlignmentOptions.MidlineLeft;
            r.reserve = MakePlaque(c, 126, 58, 276, 34, 20f);

            r.haveText = Lbl(c, "", 38f, UiCandy.Ink, TextAlignmentOptions.Center, true, 414, 30, 100, 48);

            // «− число +»: клики вешаем сами через HoldRepeat (шаг сразу, при удержании повтор и разгон)
            r.stepper = UiCandy.Stepper(c, 0, 0, 0, null, 56f, 236f, false);
            TL(r.stepper.root, 528, 26, 236, 56);
            AutoSize(r.stepper.label, 16f, 29f);
            PrepStepperButton(r.stepper.minus, () => { selected = idx; Step(-1); });
            PrepStepperButton(r.stepper.plus, () => { selected = idx; Step(1); });

            r.max = MakeBtn(c, "МАКС", UiBtn.Amber, new Vector2(104, 56), 22f, () => SetMax(idx));
            TL(r.max.btn.GetComponent<RectTransform>(), 776, 26, 104, 56);
            AutoSize(r.max.txt, 13f, 22f);

            // цена за штуку: чип с монетой, под ним сумма за выбранное
            r.priceChip = UiCandy.Chip(c, UiIcon.Coin, "", 40f, UiTone.Peach, 120f);
            TL(r.priceChip.rt, 896, 16, 120, 40);
            r.priceChip.label.color = UiCandy.GoldText;
            r.subText = Lbl(c, "", 22f, UiCandy.GoldText, TextAlignmentOptions.Left, true, 900, 62, 136, 28);
        }
    }

    void PrepStepperButton(Button b, System.Action step)
    {
        b.navigation = new Navigation { mode = Navigation.Mode.None };
        UiClickFallback.Register(b, () => open && openK > 0.5f, true);
        AddHold(b, step);
    }

    // ------------------------------------------------------------ нижняя карточка: итог и кнопки
    void BuildFooter(Transform parent, float y)
    {
        var card = UiKit.MakePanel(parent, new Vector2(InnerW, FootH), false, 0.6f, "Footer");
        card.color = UiCandy.TintOf(UiTone.Lavender);   // чуть темнее фона окна, чтобы карточка читалась
        TL(card.rectTransform, 0, y, InnerW, FootH);
        var f = card.transform;

        // «Итого» крупным чипом с монетой (ширину подгоняет UiCandy.Chip)
        totalChip = UiCandy.Chip(f, UiIcon.Coin, "", 72f, UiTone.Peach, 400f);
        TL(totalChip.rt, 24, 10, 400, 72);

        summaryText = Lbl(f, "", 22f, UiCandy.Muted, TextAlignmentOptions.Left, false, 28, 86, 990, 28);
        reasonPlaque = MakePlaque(f, 24, 118, 1004, 34, 22f);

        sellSel = MakeBtn(f, "Продать выбранное", UiBtn.Green, new Vector2(420, 76), 30f, SellSelected);
        TL(sellSel.btn.GetComponent<RectTransform>(), 24, 158, 420, 76);
        AutoSize(sellSel.txt, 16f, 30f);

        sellSurplus = MakeBtn(f, "Продать лишнее", UiBtn.Amber, new Vector2(310, 76), 30f, SellSurplus);
        TL(sellSurplus.btn.GetComponent<RectTransform>(), 456, 158, 310, 76);
        AutoSize(sellSurplus.txt, 16f, 30f);

        sellAll = MakeBtn(f, "Продать всё", UiBtn.Gray, new Vector2(262, 76), 30f, SellAll);
        TL(sellAll.btn.GetComponent<RectTransform>(), 778, 158, 262, 76);
        AutoSize(sellAll.txt, 14f, 30f);
    }

    // ------------------------------------------------------------ мелкие помощники
    // кнопка UiKit (в Candy сама получает «желе»): без навигации (иначе Enter и стрелки жали бы выбранную кнопку), индиго-надпись
    Btn MakeBtn(Transform parent, string label, UiBtn style, Vector2 size, float fontSize, UnityAction onClick)
    {
        var b = new Btn { style = style };
        b.btn = UiKit.MakeButton(parent, label, style, size, fontSize, 1f);
        b.btn.navigation = new Navigation { mode = Navigation.Mode.None };
        if (onClick != null) b.btn.onClick.AddListener(onClick);
        UiClickFallback.Register(b.btn, () => open && openK > 0.5f, true);
        b.txt = b.btn.GetComponentInChildren<TMP_Text>();
        b.fg = UiCandy.Ink;
        b.txt.color = b.fg;
        return b;
    }

    // лимонная плашка: скрыта, пока в ней нет текста (SetPlaque)
    static Plaque MakePlaque(Transform parent, float x, float y, float w, float h, float fontSize)
    {
        var p = new Plaque();
        p.bg = UiKit.MakeImage(parent, "Plaque", UiKit.Slot, true, h / 40f);
        p.bg.color = UiCandy.TintOf(UiTone.Lemon);
        TL(p.bg.rectTransform, x, y, w, h);
        p.txt = UiKit.MakeLabel(p.bg.transform, "", fontSize, UiCandy.Ink, TextAlignmentOptions.Left, true);
        Stretch(p.txt.rectTransform);
        p.txt.rectTransform.offsetMin = new Vector2(12f, 0f);
        p.txt.rectTransform.offsetMax = new Vector2(-12f, 0f);
        AutoSize(p.txt, 12f, fontSize);
        p.bg.gameObject.SetActive(false);
        return p;
    }

    // «+» и «−»: шаг сразу при нажатии, при удержании повтор и разгон
    void AddHold(Button b, System.Action step)
    {
        var h = b.gameObject.AddComponent<HoldRepeat>();
        h.btn = b;
        h.step = step;
        h.gate = () => open && openK > 0.5f;
    }
}

// Удержание кнопки: первый шаг сразу, потом повтор (через 0,4 с, дальше чаще). Нажатие учитывает interactable кнопки
public class HoldRepeat : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public Button btn;
    public System.Action step;
    public System.Func<bool> gate;
    bool held;
    float t0, next;

    public void OnPointerDown(PointerEventData e)
    {
        if (step == null || (btn != null && !btn.interactable)) return;
        held = true;
        t0 = Time.unscaledTime;
        next = t0 + 0.4f;
        step();
    }

    public void OnPointerUp(PointerEventData e) { held = false; }
    public void OnPointerExit(PointerEventData e) { held = false; }
    void OnDisable() { held = false; }

    // страховка без EventSystem: нажатие и отпускание ловим по координатам курсора (если EventSystem сработал, held уже true)
    void Poll()
    {
        if (btn == null) return;
        var rt = (RectTransform)btn.transform;
        var cv = btn.targetGraphic != null ? btn.targetGraphic.canvas : null;
        Camera cam = cv != null && cv.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? cv.rootCanvas.worldCamera : null;
        Vector2 pos = Input.touchCount > 0 ? Input.GetTouch(0).position : (Vector2)Input.mousePosition;
        bool inside = RectTransformUtility.RectangleContainsScreenPoint(rt, pos, cam);
        bool down = Input.touchCount > 0 ? Input.GetTouch(0).phase == TouchPhase.Began : Input.GetMouseButtonDown(0);
        bool still = Input.touchCount > 0 || Input.GetMouseButton(0);
        if (!held && down && inside && (gate == null || gate()) && btn.IsInteractable() && step != null)
        {
            held = true; t0 = Time.unscaledTime; next = t0 + 0.4f; step();
        }
        else if (held && (!still || !inside)) held = false;
    }

    void Update()
    {
        Poll();
        if (!held) return;
        if (btn != null && !btn.interactable) { held = false; return; }
        float now = Time.unscaledTime;
        if (now < next) return;
        float age = now - t0;
        next = now + (age > 2f ? 0.03f : age > 1f ? 0.06f : 0.11f);
        step();
    }
}
