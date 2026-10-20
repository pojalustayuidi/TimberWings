// Assets/_Project/Scripts/UI/SellWindow.cs (НЕ в Editor)
// Окно продажи у NPC-торговца в стиле UiKit (как окно пилорамы). Всё строится из кода, настраивать в сцене ничего не нужно
// (создаётся при первом E у торговца, см. TradingStall.Interact).
//   Строки: бревно и доска (рыба и брус позже: строка добавляется в Awake и SellItem/Pricing).
//   Колонки: иконка, «в рюкзаке», сколько продать (− / число / + / МАКС), цена за штуку с учётом «Торга».
//   Резерв: доски, которые ещё нужны стройкам (BuildingController.PlanksWanted), по умолчанию не продаются.
//     При открытии ничего не выбрано (0): игрок сам решает, сколько продать, окно не предлагает «продать всё» заранее.
//     «МАКС» = всё, кроме резерва; когда выбран максимум, кнопка превращается в «СБРОС» (в 0). «+» можно зайти в резерв, строка предупредит.
//     «+» и «−» при удержании повторяются и ускоряются. Рядом с ценой строка показывает сумму за выбранное.
//   Внизу «Итого» и три кнопки: «Продать выбранное» (Enter), «Продать лишнее» (Пробел, всё кроме резерва),
//   «Продать всё» (отдельная кнопка; если трогает резерв, просит нажать второй раз в течение 3 секунд).
// Клавиши: ↑↓ строка, ←→ количество (зажатие повторяет, с Shift по 5), Пробел МАКС/сброс для строки, Enter продать выбранное, E или Esc закрыть.
// Окно само закрывается, если отойти от торговца (радиус × 1,5), начался разговор или открылась прокачка.
// Пока окно открыто, MobileInput.ModalOpen = true: персонаж стоит, рубка и E «сквозь» окно не работают.
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

    // ------------------------------------------------------------ цвета
    static readonly Color Soft = new Color(0.72f, 0.78f, 0.88f, 1f);
    static readonly Color Dim = new Color(0.50f, 0.56f, 0.66f, 1f);
    static readonly Color WoodText = new Color32(0x2A, 0x16, 0x08, 255);
    static readonly Color OffText = new Color(0.86f, 0.88f, 0.92f, 1f);
    static readonly Color Sep = new Color(0.22f, 0.30f, 0.42f, 1f);
    static readonly Color Danger = new Color(1f, 0.60f, 0.45f, 1f);

    // ------------------------------------------------------------ размеры (единицы Canvas 1920x1080)
    const float WinW = 1100f, BS = 1.5f;                 // окно и масштаб его рамки
    const float InnerW = 1022f;                          // WinW − 2 × 26 × BS
    const float HeadH = 52f, RowH = 120f, RowsPad = 14f; // шапка колонок, строка, отступ снизу карточки строк
    const float GapH = 14f, FootH = 250f;                // зазор и нижняя карточка (итог и кнопки)

    class Btn
    {
        public Button btn;
        public TMP_Text txt;
        public Color fg;
    }

    class Row
    {
        public SellItem item;
        public UiIcon icon;
        public string title;
        public int count;                 // сколько продаём
        public Image sel;                 // подсветка выбранной строки
        public TMP_Text haveText, countText, priceText, reserveText, subText;
        public Btn minus, plus, max;
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
    TMP_Text totalText, summaryText, reasonText;
    Btn sellSel, sellSurplus, sellAll;

    // ------------------------------------------------------------ жизненный цикл
    void Awake()
    {
        Instance = this;
        rows = new[]
        {
            new Row { item = SellItem.Log, icon = UiIcon.Log, title = "Бревно" },
            new Row { item = SellItem.Plank, icon = UiIcon.Plank, title = "Доска" }
        };
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

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
        if (FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
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
        if (overlay.gameObject.activeSelf != show) overlay.gameObject.SetActive(show);
        if (!show) return;

        overlay.alpha = openK;
        panel.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, UiDraw.EaseOutCubic(openK));

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
        stall.ReportSold(total);

        ResetCounts();
        Refresh();
    }

    // ------------------------------------------------------------ обновление
    static void Set(TMP_Text t, string s) { if (t != null && t.text != s) t.text = s; }

    static void SetBtn(Btn b, string label, bool enabled)
    {
        Set(b.txt, label);
        b.txt.color = enabled ? b.fg : OffText;
        b.btn.interactable = enabled;
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
            r.haveText.color = have > 0 ? Color.white : Dim;
            Set(r.countText, r.count.ToString());
            Set(r.priceText, Pricing.UnitPrice(r.item).ToString("0.##"));

            // строка про резерв: жёлтым «оставить», красноватым, если продаём и часть резерва
            int into = Mathf.Max(0, r.count - free);
            if (res > 0)
            {
                if (into > 0)
                {
                    Set(r.reserveText, "Заберёте у стройки: " + into);
                    r.reserveText.color = Danger;
                }
                else
                {
                    Set(r.reserveText, "Оставить " + res + " на стройку");
                    r.reserveText.color = UiKit.Warn;
                }
            }
            else Set(r.reserveText, "");

            SetBtn(r.minus, "−", r.count > 0);
            SetBtn(r.plus, "+", r.count < have);
            SetBtn(r.max, r.count > 0 && r.count >= free ? "СБРОС" : "МАКС", free > 0 || r.count > 0);
            Set(r.subText, r.count > 0 ? "= " + Pricing.Price(r.item, r.count) : "");

            var sc = UiKit.Brass;
            r.sel.color = new Color(sc.r, sc.g, sc.b, i == selected ? 0.16f : 0f);

            totalCoins += Pricing.Price(r.item, r.count);
            selQty += r.count;
            haveQty += have;
            freeQty += free;
            reserveQty += res;
            takenQty += into;
        }

        totalCoins = Pricing.ApplySale(totalCoins, selQty);   // «Оптовик» и «Базарный день»
        Set(totalText, "Итого: <color=#F2B134>" + totalCoins + "</color> " + UiDraw.Plural(totalCoins, "монета", "монеты", "монет"));

        if (haveQty <= 0)
        {
            Set(summaryText, "Нечего продавать");
            Set(reasonText, "");
        }
        else if (selQty <= 0)
        {
            Set(summaryText, freeQty > 0 ? "Выбери, сколько продать: «+» или «МАКС»" : "Всё, что есть, нужно для стройки");
            Set(reasonText, "");
        }
        else
        {
            string note = Pricing.SaleNote(selQty);
            Set(summaryText, "Выбрано: " + selQty + " шт." + (note.Length > 0 ? "  ·  <color=#6DBE6A>" + note + "</color>" : ""));
            Set(reasonText, takenQty > 0 ? "Из них у стройки: " + takenQty + ". Доски придётся делать заново" : "");
        }
        reasonText.color = Danger;

        SetBtn(sellSel, "Продать выбранное", selQty > 0);
        SetBtn(sellSurplus, "Продать лишнее", freeQty > 0);
        SetBtn(sellAll, confirmAll ? "Точно? Продать всё" : "Продать всё", haveQty > 0);

        if (confirmAll && haveQty > 0 && reserveQty > 0)
        {
            Set(reasonText, "Это заберёт и доски для стройки (" + reserveQty + "). Нажми ещё раз, чтобы подтвердить");
            reasonText.color = Danger;
        }
    }

    // ------------------------------------------------------------ сборка UI
    void Build()
    {
        int n = rows.Length;
        float rowsH = HeadH + n * RowH + RowsPad;
        float innerH = rowsH + GapH + FootH;
        float winH = innerH + 26f * BS + 36f * BS;

        var cgo = new GameObject("SellCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cgo.layer = 5;
        cgo.transform.SetParent(transform, false);
        var canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 29;     // ниже окна прокачки (30)
        var sc = cgo.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        sc.matchWidthOrHeight = 0.5f;

        // затемнение: только гасит клики по миру
        var ov = UiKit.MakeImage(cgo.transform, "Overlay", null, false);
        ov.color = new Color(0f, 0f, 0f, 0.45f);
        Stretch(ov.rectTransform);
        ov.raycastTarget = true;   // затемнение гасит клики по миру, но окно НЕ закрывает (промах мимо окна не должен его сбрасывать)
        overlay = ov.gameObject.AddComponent<CanvasGroup>();

        // окно: деревянная планка сверху, внутри две карточки
        var win = UiKit.MakePanel(ov.transform, new Vector2(WinW, winH), true, BS, "Window");
        win.raycastTarget = true;                                // клик по окну не закрывает его
        panel = win.rectTransform;
        Anch(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(WinW, winH));
        BuildHeader();

        var content = NewRT("Content", panel);
        content.anchorMin = Vector2.zero; content.anchorMax = Vector2.one;
        content.offsetMin = new Vector2(26f * BS, 26f * BS);
        content.offsetMax = new Vector2(-26f * BS, -36f * BS);

        BuildRows(content, rowsH);
        BuildFooter(content, rowsH + GapH);

        var hint = UiKit.MakeLabel(ov.transform, "↑↓ строка   ·   ← → количество (Shift ×5)   ·   Пробел МАКС / сброс   ·   Enter продать выбранное   ·   E или Esc закрыть", 22f, Dim, TextAlignmentOptions.Center, false);
        Anch(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0, -(winH * 0.5f + 12f)), new Vector2(1500, 34));

        ov.gameObject.SetActive(false);
        built = true;
    }

    void BuildHeader()
    {
        var icon = UiKit.MakeIcon(panel, UiIcon.Coin, 44f, "TitleIcon");
        TLC(icon.rectTransform, WinW * 0.5f - 112f, 27, 44, 44);
        var title = UiKit.MakeLabel(panel, "ТОРГОВЕЦ", 34f, WoodText, TextAlignmentOptions.Center, true);
        Anch(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(24, -8), new Vector2(260, 40));

        var close = UiKit.MakeButton(panel, "", UiBtn.Amber, new Vector2(64, 48), 20f, 1f);
        Anch(close.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-34, -5), new Vector2(64, 48));
        close.navigation = new Navigation { mode = Navigation.Mode.None };
        close.onClick.AddListener(Close);
        UiClickFallback.Register(close, () => open && openK > 0.5f, true);
        var x = UiKit.MakeIcon(close.transform, UiIcon.Cross, 30f, "X");
        x.rectTransform.anchoredPosition = new Vector2(0, 2);
    }

    // ------------------------------------------------------------ карточка со строками товаров
    void BuildRows(Transform parent, float rowsH)
    {
        var card = UiKit.MakePanel(parent, new Vector2(InnerW, rowsH), false, 0.6f, "Rows");
        TL(card.rectTransform, 0, 0, InnerW, rowsH);
        var c = card.transform;

        // шапка колонок
        Lbl(c, "Товар", 20f, Dim, TextAlignmentOptions.Left, false, 124, 14, 200, 28);
        Lbl(c, "В рюкзаке", 20f, Dim, TextAlignmentOptions.Center, false, 410, 14, 120, 28);
        Lbl(c, "Сколько продать", 20f, Dim, TextAlignmentOptions.Center, false, 536, 14, 320, 28);
        Lbl(c, "Цена за шт.", 20f, Dim, TextAlignmentOptions.Center, false, 862, 14, 150, 28);
        Divider(c, 24, HeadH - 6f, InnerW - 48f);

        for (int i = 0; i < rows.Length; i++)
        {
            int idx = i;                      // для замыканий кнопок
            var r = rows[i];
            float y0 = HeadH + i * RowH;

            // подсветка выбранной строки лежит под всем остальным; клик по ней выбирает строку
            r.sel = UiKit.MakeImage(c, "Sel" + i, null, false);
            r.sel.color = new Color(1f, 1f, 1f, 0f);
            TL(r.sel.rectTransform, 8, y0 + 4, InnerW - 16, RowH - 8);
            AddClick(r.sel, () => Select(idx));

            var slot = UiKit.MakeSlot(c, 88f, 1f, "Slot" + i);
            TLC(slot.rectTransform, 70, y0 + 60, 88, 88);
            var ic = UiKit.MakeIcon(slot.transform, r.icon, 66f, "Icon");
            ic.rectTransform.anchoredPosition = Vector2.zero;

            Lbl(c, r.title, 32f, Color.white, TextAlignmentOptions.Left, true, 124, y0 + 22, 280, 40);
            r.reserveText = Lbl(c, "", 21f, UiKit.Warn, TextAlignmentOptions.Left, false, 124, y0 + 66, 285, 30);

            r.haveText = Lbl(c, "", 38f, Color.white, TextAlignmentOptions.Center, true, 410, y0 + 36, 120, 48);

            r.minus = MakeBtn(c, "−", UiBtn.Amber, new Vector2(58, 58), 38f, null);
            AddHold(r.minus, () => { selected = idx; Step(-1); });
            TL(r.minus.btn.GetComponent<RectTransform>(), 536, y0 + 31, 58, 58);

            var box = UiKit.MakeImage(c, "CountBox" + i, UiKit.Slot, true, 1f);
            TL(box.rectTransform, 602, y0 + 28, 92, 64);
            r.countText = UiKit.MakeLabel(box.transform, "", 32f, Color.white, TextAlignmentOptions.Center, true);
            AutoSize(r.countText, 16f, 32f);

            r.plus = MakeBtn(c, "+", UiBtn.Amber, new Vector2(58, 58), 38f, null);
            AddHold(r.plus, () => { selected = idx; Step(1); });
            TL(r.plus.btn.GetComponent<RectTransform>(), 702, y0 + 31, 58, 58);

            r.max = MakeBtn(c, "МАКС", UiBtn.Amber, new Vector2(88, 58), 22f, () => SetMax(idx));
            TL(r.max.btn.GetComponent<RectTransform>(), 768, y0 + 31, 88, 58);
            AutoSize(r.max.txt, 13f, 22f);

            var coin = UiKit.MakeIcon(c, UiIcon.Coin, 34f, "CoinIcon" + i);
            TLC(coin.rectTransform, 884, y0 + 48, 34, 34);
            r.priceText = Lbl(c, "", 30f, UiKit.BrassHi, TextAlignmentOptions.Left, true, 908, y0 + 28, 100, 40);
            r.subText = Lbl(c, "", 22f, Soft, TextAlignmentOptions.Left, false, 884, y0 + 68, 128, 28);

            if (i < rows.Length - 1) Divider(c, 24, y0 + RowH - 1f, InnerW - 48f);
        }
    }

    // ------------------------------------------------------------ нижняя карточка: итог и кнопки
    void BuildFooter(Transform parent, float y)
    {
        var card = UiKit.MakePanel(parent, new Vector2(InnerW, FootH), false, 0.6f, "Footer");
        TL(card.rectTransform, 0, y, InnerW, FootH);
        var f = card.transform;

        var coin = UiKit.MakeIcon(f, UiIcon.Coin, 54f, "CoinIcon");
        TLC(coin.rectTransform, 56, 52, 54, 54);
        totalText = Lbl(f, "", 44f, Color.white, TextAlignmentOptions.Left, true, 98, 22, 800, 58);
        summaryText = Lbl(f, "", 22f, Soft, TextAlignmentOptions.Left, false, 26, 88, 970, 30);
        reasonText = Lbl(f, "", 22f, Danger, TextAlignmentOptions.Left, false, 26, 118, 970, 30);

        sellSel = MakeBtn(f, "Продать выбранное", UiBtn.Green, new Vector2(400, 76), 30f, SellSelected);
        TL(sellSel.btn.GetComponent<RectTransform>(), 24, 156, 400, 76);
        AutoSize(sellSel.txt, 16f, 30f);

        sellSurplus = MakeBtn(f, "Продать лишнее", UiBtn.Amber, new Vector2(300, 76), 30f, SellSurplus);
        TL(sellSurplus.btn.GetComponent<RectTransform>(), 436, 156, 300, 76);
        AutoSize(sellSurplus.txt, 16f, 30f);

        sellAll = MakeBtn(f, "Продать всё", UiBtn.Gray, new Vector2(250, 76), 30f, SellAll);
        TL(sellAll.btn.GetComponent<RectTransform>(), 748, 156, 250, 76);
        AutoSize(sellAll.txt, 14f, 30f);
    }

    // ------------------------------------------------------------ мелкие помощники (как в PlankWorkshopWindow)
    // кнопка UiKit: без навигации (иначе Enter и стрелки жали бы выбранную кнопку), цвет надписи под стиль
    Btn MakeBtn(Transform parent, string label, UiBtn style, Vector2 size, float fontSize, UnityAction onClick)
    {
        var b = new Btn();
        b.btn = UiKit.MakeButton(parent, label, style, size, fontSize, 1f);
        b.btn.navigation = new Navigation { mode = Navigation.Mode.None };
        if (onClick != null) b.btn.onClick.AddListener(onClick);
        UiClickFallback.Register(b.btn, () => open && openK > 0.5f, true);
        b.txt = b.btn.GetComponentInChildren<TMP_Text>();
        b.fg = style == UiBtn.Gray ? Color.white : UiKit.Navy;
        b.txt.color = b.fg;
        return b;
    }

    // «+» и «−»: шаг сразу при нажатии, при удержании повтор и разгон
    void AddHold(Btn b, System.Action step)
    {
        var h = b.btn.gameObject.AddComponent<HoldRepeat>();
        h.btn = b.btn;
        h.step = step;
        h.gate = () => open && openK > 0.5f;
    }

    static void Divider(Transform parent, float x, float y, float w)
    {
        var im = UiKit.MakeImage(parent, "Line", null, false);
        im.color = Sep;
        TL(im.rectTransform, x, y, w, 2);
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
