// Assets/_Project/Scripts/UI/Construction/PlankWorkshopWindow.cs (НЕ в Editor)
// Окно пилорамы в стиле Candy (UiCandy.Window, Stepper, Chip-палитра, желейные кнопки, мятная полоса). Всё строится из кода, настраивать в сцене
// ничего не нужно (создаётся при первом E у пилы).
//   Слева: рецепт в слотах [бревна] → [доска], время на доску, уровень «Скорость пилы» шкалой из делений и кнопка в окно прокачки.
//   Справа сверху: сколько досок сделать (Stepper «− число +» и «МАКС»), сколько спишется бревен, кнопка «Начать».
//   Справа снизу: полоса работы пилы (идёт и при закрытом окне), слот со стопкой готовых досок (бейдж с числом) и кнопка «Забрать».
// Подсказка, почему нельзя начать (ws.BlockReason), показывается лимонной плашкой, не красной.
// Бревна берутся из рюкзака, окно можно закрыть: пила продолжает работать (логика в PlankWorkshop).
// Клавиши: E, Esc или крестик закрыть, ← → количество, Enter «Начать», Пробел «Забрать».
// Окно само закрывается, если отойти от пилы, начался разговор или открылась прокачка.
// CANDY: окно выскакивает через CandyPopIn (он на панели UiCandy.Window), поэтому масштаб панели здесь не трогаем, плавно меняется только
// прозрачность затемнения. Все сборщики UiKit идут внутри using (UiKit.UseSkin(UiSkin.Candy)).
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UiLayout;

public class PlankWorkshopWindow : MonoBehaviour
{
    public static PlankWorkshopWindow Instance { get; private set; }

    public bool IsOpen { get { return open; } }
    public static bool IsOpenFor(PlankWorkshop w) { return Instance != null && Instance.open && Instance.ws == w; }

    public static void Toggle(PlankWorkshop w)
    {
        var win = GetOrCreate();
        if (win.open && win.ws == w) win.Close(); else win.Open(w);
    }

    static PlankWorkshopWindow GetOrCreate()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("PlankWorkshopWindow");
        return go.AddComponent<PlankWorkshopWindow>();   // Awake выставит Instance
    }

    // ------------------------------------------------------------ цвета (палитра UiCandy; для rich text нужны hex)
    const string HexDone = "#2A9573";    // UiCandy.Done
    const string HexGold = "#9A5F00";    // UiCandy.GoldText

    // ------------------------------------------------------------ размеры (единицы Canvas 1920x1080)
    const float ChromeH = 15f * UiCandy.WindowScale + 10f + 32f * UiCandy.WindowScale + 10f;   // «юбка» снизу и лента сверху
    const float LeftW = 430f, RightW = 712f, PanelH = 737f; // внутренняя область 1162 x 737
    const float WinW = LeftW + 20f + RightW + 48f;          // окно: содержимое + поля UiCandy.Window по 24
    const float WinH = PanelH + ChromeH;
    const float TopH = 437f, GapH = 14f;                    // верхняя правая панель и зазор; нижняя = остаток

    class Btn
    {
        public Button btn;
        public TMP_Text txt;
        public Color fg;
    }

    // лимонная плашка с короткой фразой (подсказка, а не ошибка)
    class Plaque
    {
        public Image bg;
        public TMP_Text txt;
    }

    // ------------------------------------------------------------ состояние
    PlankWorkshop ws;
    bool open, built;
    float openK, refreshAt;
    int count;

    CanvasGroup overlay;
    RectTransform panel;
    Image barFill, readyIcon;
    GameObject readyBadge;
    TMP_Text readyBadgeText;
    Image[] pipFill;
    TMP_Text backpackText, recipeLogs, recipePlank, timeText, sawLevelText, upgradeCaption;
    TMP_Text summaryText, barText, readyText;
    UiCandy.StepperRefs stepper;
    Plaque reasonPlaque;
    Btn start, collect, max, upgrade;
    PlayerUpgrades up;

    // ------------------------------------------------------------ жизненный цикл
    void Awake() { Instance = this; UiModal.Register(this, () => IsOpen, Close); }
    void OnDestroy() { UiModal.Unregister(this); if (Instance == this) Instance = null; }

    public void Open(PlankWorkshop w)
    {
        if (w == null) return;
        if (UpgradeWindow.Instance != null && UpgradeWindow.Instance.IsOpen) return;   // два окна сразу не нужны
        EnsureBuilt();
        ws = w;
        open = true;
        count = w.MaxOrder;       // по умолчанию из всех бревен рюкзака, дальше можно убавить
        Refresh();
    }

    public void Close() { open = false; }

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
            if (ws == null || !ws.unlocked || DialogueSequence.AnyPlaying || upgradeOpen || ws.DistanceToPlayer() > ws.radius * 1.5f)
                open = false;
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

        if (ws != null)
        {
            if (Time.unscaledTime >= refreshAt) Refresh();
            UpdateBar();
        }
    }

    void Hotkeys()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) { open = false; return; }
        if (Input.GetKeyDown(KeyCode.LeftArrow)) Step(-1);
        if (Input.GetKeyDown(KeyCode.RightArrow)) Step(1);
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) DoStart();
        if (Input.GetKeyDown(KeyCode.Space)) DoCollect();
    }

    // ------------------------------------------------------------ действия
    void Step(int d)
    {
        if (ws == null) return;
        count = Mathf.Clamp(count + d, 0, ws.MaxOrder);
        Refresh();
    }

    void SetMax()
    {
        if (ws == null) return;
        count = ws.MaxOrder;
        Refresh();
    }

    void DoStart()
    {
        if (ws == null || count <= 0) return;
        ws.TryOrder(count);
        count = Mathf.Min(count, ws.MaxOrder);
        Refresh();
    }

    void DoCollect()
    {
        if (ws == null) return;
        ws.Collect();
        Refresh();
    }

    void OpenUpgrades()
    {
        var uw = UpgradeWindow.Instance;
        if (uw == null || !uw.IsUnlocked) return;
        uw.OpenFor((int)UpgradeType.SawSpeed);   // откроет окно прокачки на строке «Скорость пилы»; наше окно закроется само
    }

    // ------------------------------------------------------------ обновление
    static void Set(TMP_Text t, string s) { if (t != null && t.text != s) t.text = s; }

    static void SetBtn(Btn b, string label, bool enabled)
    {
        Set(b.txt, label);
        b.txt.color = enabled ? b.fg : UiCandy.Muted;
        b.btn.interactable = enabled;
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
        if (!built || ws == null) return;
        if (up == null) up = PlayerUpgrades.Instance != null ? PlayerUpgrades.Instance : FindFirstObjectByType<PlayerUpgrades>();

        int recipe = ws.LogsPerPlank;
        int maxOrder = ws.MaxOrder;
        count = Mathf.Clamp(count, 0, maxOrder);
        int logs = ws.LogsInBackpack;
        float sec = ws.SecondsPerPlank;

        // ----- слева: рецепт и пила
        Set(recipeLogs, recipe + " " + UiDraw.Plural(recipe, "бревно", "бревна", "бревен"));
        Set(recipePlank, "1 доска");
        Set(timeText, sec.ToString("0.0") + " с");

        int lvl = up != null ? up.Level(UpgradeType.SawSpeed) : 0;
        int lvlMax = GameBalance.UpgradeMaxLevel[(int)UpgradeType.SawSpeed];
        Set(sawLevelText, "ур. " + lvl + "/" + lvlMax);
        if (pipFill != null)
            for (int i = 0; i < pipFill.Length; i++)
                pipFill[i].color = i < lvl ? UiCandy.Mint : new Color(1f, 1f, 1f, 0f);

        var uw = UpgradeWindow.Instance;
        if (up != null && lvl >= lvlMax)
        {
            Set(upgradeCaption, "Быстрее некуда");
            SetBtn(upgrade, "Пила на максимуме", false);
        }
        else if (up != null && !up.IsUnlocked(UpgradeType.SawSpeed))
        {
            Set(upgradeCaption, "Улучшение пилы откроется позже");
            SetBtn(upgrade, "С " + GameBalance.SawUnlockLevel + "-го уровня", false);
        }
        else
        {
            float next = up != null ? up.ValueAt(UpgradeType.SawSpeed, lvl + 1) : GameBalance.PlankTime * Mathf.Pow(GameBalance.SawTimeFactor, lvl + 1);   // с учётом талантов
            int cost = GameBalance.UpgradeCost((int)UpgradeType.SawSpeed, lvl);
            Set(upgradeCaption, sec.ToString("0.0") + " с  →  <color=" + HexDone + ">" + next.ToString("0.0") + " с</color>  <size=85%><color=" + HexGold + ">(" + cost + " монет)</color></size>");
            bool can = uw != null && uw.IsUnlocked;
            SetBtn(upgrade, can ? "Прокачать пилу" : "Прокачка закрыта", can);
        }

        // ----- справа: выбор количества
        Set(backpackText, "В рюкзаке: <color=" + HexDone + ">" + logs + "</color> " + UiDraw.Plural(logs, "бревно", "бревна", "бревен"));

        // счётчик: «+» гаснет на максимуме, «−» на нуле (это делает сам Stepper); число пишем только при изменении
        if (stepper.Value != count || stepper.Max != maxOrder)
        {
            stepper.Max = maxOrder;
            stepper.Set(count);
        }
        SetBtn(max, "МАКС", count < maxOrder);

        string reason = ws.BlockReason;
        if (count > 0)
        {
            Set(summaryText, "Спишется <color=" + HexGold + ">" + (count * recipe) + "</color> " + UiDraw.Plural(count * recipe, "бревно", "бревна", "бревен")
                             + " · пила будет работать ~<color=" + HexGold + ">" + PlankWorkshop.FormatTime(count * sec) + "</color>"
                             + (PlayerSkills.Has(SkillIds.BossSawyer) ? "  ·  <color=" + HexDone + ">шанс " + Mathf.RoundToInt(GameBalance.SkillSawyerChance * 100f) + "% на доску без бревен</color>" : ""));
            SetPlaque(reasonPlaque, "");
            SetBtn(start, "Начать", true);
        }
        else
        {
            Set(summaryText, maxOrder > 0 ? "Нажми «+», чтобы выбрать, сколько досок сделать" : "");
            SetPlaque(reasonPlaque, reason ?? "");
            SetBtn(start, "Начать", false);
        }

        // ----- готовые доски
        int ready = ws.readyPlanks;
        Set(readyText, ready > 0 ? "Готово: <color=" + HexDone + ">" + ready + "</color> " + UiDraw.Plural(ready, "доска", "доски", "досок") : "Готовых досок нет");
        SetBtn(collect, "Забрать", ready > 0);
        if (readyIcon != null) readyIcon.color = ready > 0 ? Color.white : new Color(1f, 1f, 1f, 0.3f);
        if (readyBadge != null && readyBadge.activeSelf != (ready > 0)) readyBadge.SetActive(ready > 0);
        Set(readyBadgeText, ready.ToString());
    }

    // полоса работы пилы обновляется каждый кадр: она плавная
    void UpdateBar()
    {
        int q = ws.queuedPlanks;
        float p = q > 0 ? ws.progress : 0f;
        barFill.fillAmount = Mathf.Clamp01(p);
        barFill.gameObject.SetActive(q > 0 && p > 0.02f);

        if (q > 0)
        {
            Set(barText, "В работе: " + q + " " + UiDraw.Plural(q, "доска", "доски", "досок") + " · осталось " + PlankWorkshop.FormatTime(ws.SecondsLeft));
            barText.color = UiCandy.GoldText;
        }
        else
        {
            Set(barText, "Пила свободна");
            barText.color = UiCandy.Ink;
        }
    }

    // ------------------------------------------------------------ сборка UI
    void Build()
    {
        using (UiKit.UseSkin(UiSkin.Candy))
        {
            var cgo = new GameObject("PlankCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
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
            var win = UiCandy.Window(ov.transform, new Vector2(WinW, WinH), "ПИЛОРАМА", Close, "Window");
            panel = win.root;
            if (win.close != null) win.close.navigation = new Navigation { mode = Navigation.Mode.None };   // Enter и стрелки крестик не жмут
            BuildHeader();

            // рабочая область по центру содержимого окна
            var body = NewRT("Body", win.content);
            Anch(body, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(LeftW + 20f + RightW, PanelH));

            BuildRecipeCard(body);
            BuildOrderArea(body);

            // подсказка клавиш: светлая пилюля под окном (на затемнении голый текст не читался бы)
            var hintBg = UiKit.MakeImage(ov.transform, "HintPill", UiKit.Slot, true, 0.7f);
            hintBg.color = new Color(1f, 1f, 1f, 0.92f);
            Anch(hintBg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0, -(WinH * 0.5f + 14f)), new Vector2(1000, 46));
            UiKit.MakeLabel(hintBg.transform, "← → количество   ·   Enter начать   ·   Пробел забрать   ·   E или Esc закрыть", 22f, UiCandy.Muted, TextAlignmentOptions.Center, false);

            ov.gameObject.SetActive(false);
            built = true;
        }
    }

    // значок пилорамы слева от названия (название и «×» делает UiCandy.Window)
    void BuildHeader()
    {
        var icon = UiCandy.Icon(panel, UiIcon.Sawmill, 44f, "TitleIcon");
        TLC(icon.rectTransform, WinW * 0.5f - 135f, 27, 44, 44);
    }

    // ------------------------------------------------------------ левая колонка: рецепт и пила
    void BuildRecipeCard(Transform parent)
    {
        var card = UiKit.MakePanel(parent, new Vector2(LeftW, PanelH), false, 0.6f, "Recipe");
        card.color = UiCandy.TintOf(UiTone.Lavender);   // чуть темнее фона окна, чтобы карточка читалась
        TL(card.rectTransform, 0, 0, LeftW, PanelH);
        var c = card.transform;

        // рецепт: [бревна] -> [доска], каждый в своём слоте
        Lbl(c, "Нужно на 1 доску", 21f, UiCandy.Muted, TextAlignmentOptions.Center, false, 8, 26, 176, 30);
        Lbl(c, "Получишь", 21f, UiCandy.Muted, TextAlignmentOptions.Center, false, 262, 26, 160, 30);

        var logSlot = UiKit.MakeSlot(c, 132f, 1.1f, "LogSlot");
        TLC(logSlot.rectTransform, 96, 140, 132, 132);
        var logIcon = UiKit.MakeIcon(logSlot.transform, UiIcon.Log, 98f, "LogIcon");
        logIcon.rectTransform.anchoredPosition = Vector2.zero;

        var arrow = UiKit.MakeIcon(c, UiIcon.ArrowRight, 60f, "Arrow");
        TLC(arrow.rectTransform, 215, 140, 60, 60);

        var plankSlot = UiKit.MakeSlot(c, 132f, 1.1f, "PlankSlot");
        TLC(plankSlot.rectTransform, 334, 140, 132, 132);
        var plankIcon = UiKit.MakeIcon(plankSlot.transform, UiIcon.Plank, 100f, "PlankIcon");
        plankIcon.rectTransform.anchoredPosition = Vector2.zero;

        recipeLogs = Lbl(c, "", 34f, UiCandy.Ink, TextAlignmentOptions.Center, true, 6, 214, 180, 44);
        recipePlank = Lbl(c, "", 34f, UiCandy.GoldText, TextAlignmentOptions.Center, true, 254, 214, 160, 44);
        Divider(c, 30, 280, LeftW - 60);

        // время на доску
        var clock = UiKit.MakeIcon(c, UiIcon.Clock, 72f, "ClockIcon");
        TLC(clock.rectTransform, 76, 346, 72, 72);
        Lbl(c, "Время на доску", 22f, UiCandy.Muted, TextAlignmentOptions.Left, false, 126, 308, 290, 28);
        timeText = Lbl(c, "", 54f, UiCandy.Ink, TextAlignmentOptions.Left, true, 126, 334, 290, 66);
        Divider(c, 30, 420, LeftW - 60);

        // уровень пилы: надпись, номер и шкала из делений
        Lbl(c, "Скорость пилы", 24f, UiCandy.Ink, TextAlignmentOptions.Left, true, 34, 438, 250, 32);
        sawLevelText = Lbl(c, "", 24f, UiCandy.GoldText, TextAlignmentOptions.Right, true, 270, 438, 126, 32);

        int n = GameBalance.UpgradeMaxLevel[(int)UpgradeType.SawSpeed];
        pipFill = new Image[Mathf.Max(0, n)];
        float pipW = Mathf.Min(40f, (LeftW - 68f - Mathf.Max(0, n - 1) * 8f) / Mathf.Max(1, n));
        float total = n * pipW + Mathf.Max(0, n - 1) * 8f;
        float x0 = (LeftW - total) * 0.5f;
        for (int i = 0; i < n; i++)
        {
            var back = UiKit.MakeImage(c, "Pip" + i, Dot, false);
            back.color = UiCandy.Lavender;
            TLC(back.rectTransform, x0 + i * (pipW + 8f) + pipW * 0.5f, 498, pipW, pipW);
            var fill = UiKit.MakeImage(back.transform, "Fill", Dot, false);
            fill.rectTransform.sizeDelta = new Vector2(pipW - 8f, pipW - 8f);
            fill.color = new Color(1f, 1f, 1f, 0f);
            pipFill[i] = fill;
        }

        upgradeCaption = Lbl(c, "", 23f, UiCandy.Muted, TextAlignmentOptions.Center, false, 20, 534, LeftW - 40, 32);

        upgrade = MakeBtn(c, "Прокачать пилу", UiBtn.Amber, new Vector2(370, 78), 30f, OpenUpgrades);
        Anch(upgrade.btn.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 32), new Vector2(370, 78));
        AddBtnIcon(upgrade, UiIcon.ArrowUp, 40f);
    }

    // ------------------------------------------------------------ правая колонка: заказ и результат
    void BuildOrderArea(Transform parent)
    {
        float botH = PanelH - TopH - GapH;

        // ----- верх: сколько досок сделать
        var top = UiKit.MakePanel(parent, new Vector2(RightW, TopH), false, 0.6f, "Order");
        top.color = UiCandy.TintOf(UiTone.Lavender);
        TL(top.rectTransform, LeftW + 20f, 0, RightW, TopH);
        var t = top.transform;

        var ring = UiKit.MakeImage(t, "DiscRing", Dot, false);
        ring.color = UiCandy.PeachDeep;
        TLC(ring.rectTransform, 128, 126, 228, 228);
        var disc = UiKit.MakeImage(t, "Disc", Dot, false);
        disc.color = UiCandy.Cloud;
        TLC(disc.rectTransform, 128, 126, 214, 214);
        var saw = UiKit.MakeIcon(t, UiIcon.Sawmill, 184f, "Sawmill");
        TLC(saw.rectTransform, 128, 126, 184, 184);
        backpackText = Lbl(t, "", 21f, UiCandy.Muted, TextAlignmentOptions.Center, false, 24, 246, 208, 30);

        Lbl(t, "Сколько досок сделать?", 31f, UiCandy.Ink, TextAlignmentOptions.Left, true, 250, 24, 440, 42);

        // «− число +» (клики вешаем сами: те же шаги, что у клавиш) и «МАКС»
        stepper = UiCandy.Stepper(t, 0, 0, 0, null, 64f, 340f, false);
        TL(stepper.root, 240, 94, 340, 64);
        stepper.Format = v => v + " " + UiDraw.Plural(v, "доска", "доски", "досок");
        AutoSize(stepper.label, 16f, 33f);
        stepper.Set(0);
        stepper.minus.navigation = new Navigation { mode = Navigation.Mode.None };
        stepper.plus.navigation = new Navigation { mode = Navigation.Mode.None };
        stepper.minus.onClick.AddListener(() => Step(-1));
        stepper.plus.onClick.AddListener(() => Step(1));

        max = MakeBtn(t, "МАКС", UiBtn.Amber, new Vector2(104, 64), 24f, SetMax);
        TL(max.btn.GetComponent<RectTransform>(), 592, 94, 104, 64);
        AutoSize(max.txt, 13f, 24f);

        summaryText = Lbl(t, "", 21f, UiCandy.Muted, TextAlignmentOptions.Left, false, 240, 176, 456, 30);
        reasonPlaque = MakePlaque(t, 240, 214, 456, 38, 21f);

        start = MakeBtn(t, "Начать", UiBtn.Green, new Vector2(664, 92), 42f, DoStart);
        TL(start.btn.GetComponent<RectTransform>(), 24, 316, 664, 92);
        AddBtnIcon(start, UiIcon.Play, 46f);

        // ----- низ: работа пилы и готовые доски
        var bot = UiKit.MakePanel(parent, new Vector2(RightW, botH), false, 0.6f, "Result");
        bot.color = UiCandy.TintOf(UiTone.Lavender);
        TL(bot.rectTransform, LeftW + 20f, TopH + GapH, RightW, botH);
        var b = bot.transform;

        Image fill;
        var bar = UiKit.MakeBar(b, UiBar.Green, new Vector2(664, 38), out fill, 1f);
        TL(bar.rectTransform, 24, 26, 664, 38);
        barFill = fill;
        barFill.gameObject.SetActive(false);
        barText = Lbl(b, "", 24f, UiCandy.Ink, TextAlignmentOptions.Center, true, 24, 72, 664, 34);

        var band = UiKit.MakeImage(b, "Band", UiKit.Slot, true, 1f);
        TL(band.rectTransform, 24, 120, 664, botH - 120 - 34);
        var rs = UiKit.MakeSlot(band.transform, 100f, 1f, "ReadySlot");
        Anch(rs.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(16, 0), new Vector2(100, 100));
        readyIcon = UiKit.MakeIcon(rs.transform, UiIcon.Planks, 78f, "ReadyIcon");
        readyIcon.rectTransform.anchoredPosition = Vector2.zero;

        var badge = UiKit.MakeImage(rs.transform, "Badge", UiKit.Medallion, false);
        Anch(badge.rectTransform, new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-6, -6), new Vector2(46, 46));
        readyBadgeText = UiKit.MakeLabel(badge.transform, "0", 24f, UiCandy.Ink, TextAlignmentOptions.Center, true);
        AutoSize(readyBadgeText, 13f, 24f);
        readyBadge = badge.gameObject;
        readyBadge.SetActive(false);

        readyText = UiKit.MakeLabel(band.transform, "", 28f, UiCandy.Ink, TextAlignmentOptions.Left, true);
        Anch(readyText.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(128, 0), new Vector2(234, 50));
        AutoSize(readyText, 16f, 28f);

        collect = MakeBtn(band.transform, "Забрать", UiBtn.Green, new Vector2(270, 76), 32f, DoCollect);
        Anch(collect.btn.GetComponent<RectTransform>(), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-18, 0), new Vector2(270, 76));
        AddBtnIcon(collect, UiIcon.Box, 40f);
    }

    // ------------------------------------------------------------ мелкие помощники
    static Sprite dot;

    // круг-подложка под картинку пилорамы
    static Sprite Dot
    {
        get
        {
            if (dot != null) return dot;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var cols = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f));
                    cols[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(n * 0.5f - d));
                }
            tex.SetPixels(cols);
            tex.Apply();
            dot = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            dot.name = "PlankWindow_Dot";
            return dot;
        }
    }

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

    // значок слева на кнопке, надпись сдвигается вправо
    static void AddBtnIcon(Btn b, UiIcon icon, float size)
    {
        var ic = UiCandy.Icon(b.btn.transform, icon, size, "Icon");
        Anch(ic.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(22, 2), new Vector2(size, size));
        b.txt.rectTransform.offsetMin = new Vector2(22 + size, b.txt.rectTransform.offsetMin.y);
    }

    // тонкая сплошная линия (скруглённый спрайт на 3 px исказился бы)
    static void Divider(Transform parent, float x, float y, float w)
    {
        var im = UiKit.MakeImage(parent, "Line", null, false);
        im.color = UiKit.CandyLavender;
        TL(im.rectTransform, x, y, w, 3);
    }
}
