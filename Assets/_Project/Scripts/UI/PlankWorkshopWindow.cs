// Assets/_Project/Scripts/UI/PlankWorkshopWindow.cs (НЕ в Editor)
// Окно пилорамы в стиле UiKit (как окно прокачки). Всё строится из кода, настраивать в сцене ничего не нужно (создаётся при первом E у пилы).
//   Слева: рецепт в слотах [бревна] → [доска], время на доску, уровень «Скорость пилы» шкалой из делений и кнопка в окно прокачки.
//   Справа сверху: сколько досок сделать (− / число / + / МАКС), сколько спишется бревен, кнопка «Начать».
//   Справа снизу: полоса работы пилы (идёт и при закрытом окне), слот со стопкой готовых досок (бейдж с числом) и кнопка «Забрать».
// Бревна берутся из рюкзака, окно можно закрыть: пила продолжает работать (логика в PlankWorkshop).
// Клавиши: E, Esc или крестик закрыть, ← → количество, Enter «Начать», Пробел «Забрать».
// Окно само закрывается, если отойти от пилы, начался разговор или открылась прокачка.
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

    // ------------------------------------------------------------ цвета
    static readonly Color Soft = new Color(0.72f, 0.78f, 0.88f, 1f);
    static readonly Color Dim = new Color(0.50f, 0.56f, 0.66f, 1f);
    static readonly Color WoodText = new Color32(0x2A, 0x16, 0x08, 255);
    static readonly Color PlankText = new Color32(0xE8, 0xC1, 0x7A, 255);
    static readonly Color OffText = new Color(0.86f, 0.88f, 0.92f, 1f);
    static readonly Color Sep = new Color(0.22f, 0.30f, 0.42f, 1f);

    // ------------------------------------------------------------ размеры (единицы Canvas 1920x1080)
    const float WinW = 1240f, WinH = 830f, BS = 1.5f;       // окно и масштаб его рамки
    const float LeftW = 430f, RightW = 712f, PanelH = 737f; // внутренняя область 1162 x 737
    const float TopH = 437f, GapH = 14f;                    // верхняя правая панель и зазор; нижняя = остаток

    class Btn
    {
        public Button btn;
        public TMP_Text txt;
        public Color fg;
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
    TMP_Text countText, summaryText, reasonText, barText, readyText;
    Btn start, collect, minus, plus, max, upgrade;
    PlayerUpgrades up;

    // ------------------------------------------------------------ жизненный цикл
    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }

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
        if (overlay.gameObject.activeSelf != show) overlay.gameObject.SetActive(show);
        if (!show) return;

        overlay.alpha = openK;
        panel.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, UiDraw.EaseOutCubic(openK));

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
        b.txt.color = enabled ? b.fg : OffText;
        b.btn.interactable = enabled;
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
                pipFill[i].color = i < lvl ? UiKit.Brass : new Color(1f, 1f, 1f, 0f);

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
            Set(upgradeCaption, sec.ToString("0.0") + " с  →  <color=#6DBE6A>" + next.ToString("0.0") + " с</color>  <size=85%><color=#6DBE6A>(" + cost + " монет)</color></size>");
            bool can = uw != null && uw.IsUnlocked;
            SetBtn(upgrade, can ? "Прокачать пилу" : "Прокачка закрыта", can);
        }

        // ----- справа: выбор количества
        Set(backpackText, "В рюкзаке: <color=#6DBE6A>" + logs + "</color> " + UiDraw.Plural(logs, "бревно", "бревна", "бревен"));
        Set(countText, count + " " + UiDraw.Plural(count, "доска", "доски", "досок"));

        SetBtn(minus, "−", count > 0);
        SetBtn(plus, "+", count < maxOrder);
        SetBtn(max, "МАКС", count < maxOrder);

        string reason = ws.BlockReason;
        if (count > 0)
        {
            Set(summaryText, "Спишется <color=#F2B134>" + (count * recipe) + "</color> " + UiDraw.Plural(count * recipe, "бревно", "бревна", "бревен")
                             + " · пила будет работать ~<color=#F2B134>" + PlankWorkshop.FormatTime(count * sec) + "</color>"
                             + (PlayerSkills.Has(SkillIds.BossSawyer) ? "  ·  <color=#6DBE6A>шанс " + Mathf.RoundToInt(GameBalance.SkillSawyerChance * 100f) + "% на доску без бревен</color>" : ""));
            Set(reasonText, "");
            SetBtn(start, "Начать", true);
        }
        else
        {
            Set(summaryText, maxOrder > 0 ? "Нажми «+», чтобы выбрать, сколько досок сделать" : "");
            Set(reasonText, reason ?? "");
            SetBtn(start, "Начать", false);
        }

        // ----- готовые доски
        int ready = ws.readyPlanks;
        Set(readyText, ready > 0 ? "Готово: <color=#6DBE6A>" + ready + "</color> " + UiDraw.Plural(ready, "доска", "доски", "досок") : "Готовых досок нет");
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
            barText.color = UiKit.BrassHi;
        }
        else
        {
            Set(barText, "Пила свободна");
            barText.color = Color.white;
        }
    }

    // ------------------------------------------------------------ сборка UI
    void Build()
    {
        var cgo = new GameObject("PlankCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
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

        // окно: деревянная планка сверху, внутри две колонки
        var win = UiKit.MakePanel(ov.transform, new Vector2(WinW, WinH), true, BS, "Window");
        win.raycastTarget = true;                                // клик по окну не закрывает его
        panel = win.rectTransform;
        Anch(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(WinW, WinH));
        BuildHeader();

        var content = NewRT("Content", panel);
        content.anchorMin = Vector2.zero; content.anchorMax = Vector2.one;
        content.offsetMin = new Vector2(26f * BS, 26f * BS);
        content.offsetMax = new Vector2(-26f * BS, -36f * BS);

        BuildRecipeCard(content);
        BuildOrderArea(content);

        var hint = UiKit.MakeLabel(ov.transform, "← → количество   ·   Enter начать   ·   Пробел забрать   ·   E или Esc закрыть", 22f, Dim, TextAlignmentOptions.Center, false);
        Anch(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0, -(WinH * 0.5f + 12f)), new Vector2(1100, 34));

        ov.gameObject.SetActive(false);
        built = true;
    }

    void BuildHeader()
    {
        var icon = UiKit.MakeIcon(panel, UiIcon.Sawmill, 44f, "TitleIcon");
        TLC(icon.rectTransform, WinW * 0.5f - 112f, 27, 44, 44);
        var title = UiKit.MakeLabel(panel, "ПИЛОРАМА", 34f, WoodText, TextAlignmentOptions.Center, true);
        Anch(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(24, -8), new Vector2(260, 40));

        var close = UiKit.MakeButton(panel, "", UiBtn.Amber, new Vector2(64, 48), 20f, 1f);
        Anch(close.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-34, -5), new Vector2(64, 48));
        close.navigation = new Navigation { mode = Navigation.Mode.None };
        close.onClick.AddListener(Close);
        var x = UiKit.MakeIcon(close.transform, UiIcon.Cross, 30f, "X");
        x.rectTransform.anchoredPosition = new Vector2(0, 2);
    }

    // ------------------------------------------------------------ левая колонка: рецепт и пила
    void BuildRecipeCard(Transform parent)
    {
        var card = UiKit.MakePanel(parent, new Vector2(LeftW, PanelH), false, 0.6f, "Recipe");
        TL(card.rectTransform, 0, 0, LeftW, PanelH);
        var c = card.transform;

        // рецепт: [бревна] -> [доска], каждый в своём слоте
        Lbl(c, "Нужно на 1 доску", 21f, Soft, TextAlignmentOptions.Center, false, 8, 26, 176, 30);
        Lbl(c, "Получишь", 21f, Soft, TextAlignmentOptions.Center, false, 262, 26, 160, 30);

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

        recipeLogs = Lbl(c, "", 34f, Color.white, TextAlignmentOptions.Center, true, 6, 214, 180, 44);
        recipePlank = Lbl(c, "", 34f, PlankText, TextAlignmentOptions.Center, true, 254, 214, 160, 44);
        Divider(c, 30, 280, LeftW - 60);

        // время на доску
        var clock = UiKit.MakeIcon(c, UiIcon.Clock, 72f, "ClockIcon");
        TLC(clock.rectTransform, 76, 346, 72, 72);
        Lbl(c, "Время на доску", 22f, Soft, TextAlignmentOptions.Left, false, 126, 308, 290, 28);
        timeText = Lbl(c, "", 54f, UiKit.BrassHi, TextAlignmentOptions.Left, true, 126, 334, 290, 66);
        Divider(c, 30, 420, LeftW - 60);

        // уровень пилы: надпись, номер и шкала из делений
        Lbl(c, "Скорость пилы", 24f, Color.white, TextAlignmentOptions.Left, true, 34, 438, 250, 32);
        sawLevelText = Lbl(c, "", 24f, UiKit.BrassHi, TextAlignmentOptions.Right, true, 270, 438, 126, 32);

        int n = GameBalance.UpgradeMaxLevel[(int)UpgradeType.SawSpeed];
        pipFill = new Image[Mathf.Max(0, n)];
        float pipW = Mathf.Min(40f, (LeftW - 68f - Mathf.Max(0, n - 1) * 8f) / Mathf.Max(1, n));
        float total = n * pipW + Mathf.Max(0, n - 1) * 8f;
        float x0 = (LeftW - total) * 0.5f;
        for (int i = 0; i < n; i++)
        {
            var back = UiKit.MakeImage(c, "Pip" + i, Dot, false);
            back.color = new Color(0.04f, 0.07f, 0.12f, 0.95f);
            TLC(back.rectTransform, x0 + i * (pipW + 8f) + pipW * 0.5f, 498, pipW, pipW);
            var fill = UiKit.MakeImage(back.transform, "Fill", Dot, false);
            fill.rectTransform.sizeDelta = new Vector2(pipW - 8f, pipW - 8f);
            fill.color = new Color(1f, 1f, 1f, 0f);
            pipFill[i] = fill;
        }

        upgradeCaption = Lbl(c, "", 23f, Soft, TextAlignmentOptions.Center, false, 20, 534, LeftW - 40, 32);

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
        TL(top.rectTransform, LeftW + 20f, 0, RightW, TopH);
        var t = top.transform;

        var ring = UiKit.MakeImage(t, "DiscRing", Dot, false);
        ring.color = new Color(0.72f, 0.45f, 0.10f, 0.95f);
        TLC(ring.rectTransform, 128, 126, 228, 228);
        var disc = UiKit.MakeImage(t, "Disc", Dot, false);
        disc.color = new Color(0.05f, 0.09f, 0.15f, 0.75f);
        TLC(disc.rectTransform, 128, 126, 214, 214);
        var saw = UiKit.MakeIcon(t, UiIcon.Sawmill, 184f, "Sawmill");
        TLC(saw.rectTransform, 128, 126, 184, 184);
        backpackText = Lbl(t, "", 21f, Soft, TextAlignmentOptions.Center, false, 24, 246, 208, 30);

        Lbl(t, "Сколько досок сделать?", 31f, Color.white, TextAlignmentOptions.Left, true, 250, 24, 440, 42);

        var strip = UiKit.MakeImage(t, "Strip", UiKit.Slot, true, 1f);
        TL(strip.rectTransform, 240, 78, 456, 96);
        var st = strip.transform;

        minus = MakeBtn(st, "−", UiBtn.Amber, new Vector2(64, 60), 40f, () => Step(-1));
        TL(minus.btn.GetComponent<RectTransform>(), 14, 18, 64, 60);

        var box = UiKit.MakeImage(st, "CountBox", UiKit.Slot, true, 1f);
        TL(box.rectTransform, 90, 16, 170, 64);
        countText = UiKit.MakeLabel(box.transform, "", 32f, Color.white, TextAlignmentOptions.Center, true);
        AutoSize(countText, 18f, 32f);

        plus = MakeBtn(st, "+", UiBtn.Amber, new Vector2(64, 60), 40f, () => Step(1));
        TL(plus.btn.GetComponent<RectTransform>(), 272, 18, 64, 60);

        max = MakeBtn(st, "МАКС", UiBtn.Amber, new Vector2(94, 60), 22f, SetMax);
        TL(max.btn.GetComponent<RectTransform>(), 348, 18, 94, 60);
        AutoSize(max.txt, 13f, 22f);

        summaryText = Lbl(t, "", 21f, Soft, TextAlignmentOptions.Left, false, 240, 186, 456, 30);
        reasonText = Lbl(t, "", 21f, UiKit.BrassHi, TextAlignmentOptions.Left, false, 240, 220, 456, 30);

        start = MakeBtn(t, "Начать", UiBtn.Green, new Vector2(664, 92), 42f, DoStart);
        TL(start.btn.GetComponent<RectTransform>(), 24, 316, 664, 92);
        AddBtnIcon(start, UiIcon.Play, 46f);

        // ----- низ: работа пилы и готовые доски
        var bot = UiKit.MakePanel(parent, new Vector2(RightW, botH), false, 0.6f, "Result");
        TL(bot.rectTransform, LeftW + 20f, TopH + GapH, RightW, botH);
        var b = bot.transform;

        Image fill;
        var bar = UiKit.MakeBar(b, UiBar.Amber, new Vector2(664, 38), out fill, 1f);
        TL(bar.rectTransform, 24, 26, 664, 38);
        barFill = fill;
        barFill.gameObject.SetActive(false);
        barText = Lbl(b, "", 24f, Color.white, TextAlignmentOptions.Center, true, 24, 72, 664, 34);

        var band = UiKit.MakeImage(b, "Band", UiKit.Slot, true, 1f);
        TL(band.rectTransform, 24, 120, 664, botH - 120 - 34);
        var rs = UiKit.MakeSlot(band.transform, 100f, 1f, "ReadySlot");
        Anch(rs.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(16, 0), new Vector2(100, 100));
        readyIcon = UiKit.MakeIcon(rs.transform, UiIcon.Planks, 78f, "ReadyIcon");
        readyIcon.rectTransform.anchoredPosition = Vector2.zero;

        var badge = UiKit.MakeImage(rs.transform, "Badge", UiKit.Medallion, false);
        Anch(badge.rectTransform, new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-6, -6), new Vector2(46, 46));
        readyBadgeText = UiKit.MakeLabel(badge.transform, "0", 24f, Color.white, TextAlignmentOptions.Center, true);
        AutoSize(readyBadgeText, 13f, 24f);
        readyBadge = badge.gameObject;
        readyBadge.SetActive(false);

        readyText = UiKit.MakeLabel(band.transform, "", 28f, Color.white, TextAlignmentOptions.Left, true);
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

    // кнопка UiKit: без навигации (иначе Enter и стрелки жали бы выбранную кнопку), цвет надписи под стиль
    Btn MakeBtn(Transform parent, string label, UiBtn style, Vector2 size, float fontSize, UnityAction onClick)
    {
        var b = new Btn();
        b.btn = UiKit.MakeButton(parent, label, style, size, fontSize, 1f);
        b.btn.navigation = new Navigation { mode = Navigation.Mode.None };
        b.btn.onClick.AddListener(onClick);
        b.txt = b.btn.GetComponentInChildren<TMP_Text>();
        b.fg = style == UiBtn.Gray ? Color.white : UiKit.Navy;
        b.txt.color = b.fg;
        return b;
    }

    // значок слева на кнопке, надпись сдвигается вправо
    static void AddBtnIcon(Btn b, UiIcon icon, float size)
    {
        var ic = UiKit.MakeIcon(b.btn.transform, icon, size, "Icon");
        Anch(ic.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(22, 2), new Vector2(size, size));
        b.txt.rectTransform.offsetMin = new Vector2(22 + size, b.txt.rectTransform.offsetMin.y);
    }

    static void Divider(Transform parent, float x, float y, float w)
    {
        var im = UiKit.MakeImage(parent, "Line", null, false);
        im.color = Sep;
        TL(im.rectTransform, x, y, w, 2);
    }

}
